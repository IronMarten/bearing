namespace Bearing.Tests;

/// <summary>
/// Tarjan's SCC over synthetic graphs, run against <b>both</b> implementations. Fast, no Roslyn,
/// no workspace load — and it pins the behaviour circular-reference detection depends on.
///
/// Every case runs twice because Core's copy is a port and a port is where a subtle difference
/// hides: the fixture's cycles are one namespace component and one type tangle, so the fixture
/// alone would not notice a min-size that was off by one, a self-loop that started counting, or
/// a dangling edge that threw. At R2 the probe row comes out of the theory data and the cases
/// stay exactly as they are.
///
/// Job A needs one thing neither implementation provides: a traversable cycle PATH. Tarjan
/// returns component membership ("these six namespaces are entangled"), and what a user can act
/// on is A to B to C to A. See TECHREQ-job-a.md 5.1.
/// </summary>
public sealed class GraphTests
{
    public static TheoryData<string> Implementations => ["probe", "core"];

    private static Dictionary<string, List<string>> Graph(params (string From, string[] To)[] edges) =>
        edges.ToDictionary(e => e.From, e => e.To.ToList(), StringComparer.Ordinal);

    private static IReadOnlyList<IReadOnlyList<string>> StronglyConnected(
        string implementation, Dictionary<string, List<string>> graph, int minSize) =>
        implementation switch
        {
            "probe" => ArchProbe.Graphs.StronglyConnected(graph, minSize),
            "core" => IronMarten.Bearing.Graphs.StronglyConnected(
                graph.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal),
                minSize),
            _ => throw new ArgumentOutOfRangeException(nameof(implementation), implementation, null),
        };

    [Theory]
    [MemberData(nameof(Implementations))]
    public void A_simple_chain_has_no_components(string implementation)
    {
        var g = Graph(("a", ["b"]), ("b", ["c"]), ("c", []));
        Assert.Empty(StronglyConnected(implementation, g, 2));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void A_mutual_pair_is_found_at_min_size_two(string implementation)
    {
        var g = Graph(("a", ["b"]), ("b", ["a"]));

        var component = Assert.Single(StronglyConnected(implementation, g, 2));

        Assert.Equal(["a", "b"], component.Order());
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Min_size_suppresses_smaller_components(string implementation)
    {
        // Mutual pairs and triples are ordinary C# — parent/child, visitor/visited. Listing
        // them buries the signal under things nobody will act on, which is why type tangles
        // are gated at 4 and namespace cycles at 2.
        var g = Graph(("a", ["b"]), ("b", ["a"]), ("c", ["d"]), ("d", ["c"]));

        Assert.Equal(2, StronglyConnected(implementation, g, 2).Count);
        Assert.Empty(StronglyConnected(implementation, g, 3));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Two_independent_cycles_are_reported_separately(string implementation)
    {
        var g = Graph(
            ("a", ["b"]), ("b", ["c"]), ("c", ["a"]),
            ("x", ["y"]), ("y", ["x"]));

        var found = StronglyConnected(implementation, g, 2);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, c => c.Count == 3);
        Assert.Contains(found, c => c.Count == 2);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void A_self_loop_alone_is_not_a_component(string implementation)
    {
        // A type referencing itself is not a circular dependency worth reporting.
        var g = Graph(("a", ["a"]));
        Assert.Empty(StronglyConnected(implementation, g, 2));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Edges_to_unknown_nodes_do_not_throw(string implementation)
    {
        // Real input has dangling targets: an edge into an excluded or unloaded type.
        var g = Graph(("a", ["b", "missing"]), ("b", ["a"]));

        var component = Assert.Single(StronglyConnected(implementation, g, 2));

        Assert.Equal(["a", "b"], component.Order());
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Deep_chains_do_not_overflow_the_stack(string implementation)
    {
        // Iterative on purpose: recursion depth is bounded by the longest dependency path,
        // and a large solution can exceed the stack long before anything else.
        const int depth = 50_000;
        var g = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var i = 0; i < depth; i++) g[$"n{i}"] = [$"n{i + 1}"];
        g[$"n{depth}"] = ["n0"];

        var component = Assert.Single(StronglyConnected(implementation, g, 2));

        Assert.Equal(depth + 1, component.Count);
    }

    /// <summary>
    /// The partition is a property of the graph; the order it is discovered in is not.
    /// </summary>
    /// <remarks>
    /// Both implementations sort their output for this reason, and neither is exercised by the
    /// fixture's single component. Reversing the insertion order changes which root the outer
    /// loop reaches first, which is the whole of what the canonical form defends against.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Implementations))]
    public void The_result_does_not_depend_on_insertion_order(string implementation)
    {
        (string, string[])[] edges =
            [("a", ["b"]), ("b", ["a"]), ("m", ["n"]), ("n", ["m"]), ("x", ["y"]), ("y", ["x"])];

        var forward = StronglyConnected(implementation, Graph(edges), 2);
        var backward = StronglyConnected(implementation, Graph([.. edges.Reverse()]), 2);

        Assert.Equal(forward, backward);
    }
}

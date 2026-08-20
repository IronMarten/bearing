namespace Bearing.Tests;

/// <summary>
/// Tarjan's SCC over synthetic graphs. Fast, no Roslyn, no workspace load — and it pins the
/// behaviour circular-reference detection depends on.
///
/// Every case here earns its place from the fixture, not from the probe. The fixture's cycles
/// are one namespace component and one type tangle, so the fixture alone would not notice a
/// min-size that was off by one, a self-loop that started counting, or a dangling edge that
/// threw. Each of those is a case below.
///
/// R2 took the second row out of the theory data. These ran twice — once against the probe,
/// once against Core — for as long as Core's copy was a port being diffed against its
/// original; the cases are unchanged, and what has gone is the comparison, not the coverage.
/// The traversable PATH cases below never had a probe row at all: A to B to C to A is what a
/// user can act on where component membership is not, and it is Core's alone, shipped at A3.
/// </summary>
public sealed class GraphTests
{
    private static Dictionary<string, List<string>> Graph(params (string From, string[] To)[] edges) =>
        edges.ToDictionary(e => e.From, e => e.To.ToList(), StringComparer.Ordinal);

    private static IReadOnlyList<IReadOnlyList<string>> StronglyConnected(
        Dictionary<string, List<string>> graph, int minSize) =>
        IronMarten.Bearing.Graphs.StronglyConnected(
            graph.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal),
            minSize);

    [Fact]
    public void A_simple_chain_has_no_components()
    {
        var g = Graph(("a", ["b"]), ("b", ["c"]), ("c", []));
        Assert.Empty(StronglyConnected(g, 2));
    }

    [Fact]
    public void A_mutual_pair_is_found_at_min_size_two()
    {
        var g = Graph(("a", ["b"]), ("b", ["a"]));

        var component = Assert.Single(StronglyConnected(g, 2));

        Assert.Equal(["a", "b"], component.Order());
    }

    [Fact]
    public void Min_size_suppresses_smaller_components()
    {
        // Mutual pairs and triples are ordinary C# — parent/child, visitor/visited. Listing
        // them buries the signal under things nobody will act on, which is why type tangles
        // are gated at 4 and namespace cycles at 2.
        var g = Graph(("a", ["b"]), ("b", ["a"]), ("c", ["d"]), ("d", ["c"]));

        Assert.Equal(2, StronglyConnected(g, 2).Count);
        Assert.Empty(StronglyConnected(g, 3));
    }

    [Fact]
    public void Two_independent_cycles_are_reported_separately()
    {
        var g = Graph(
            ("a", ["b"]), ("b", ["c"]), ("c", ["a"]),
            ("x", ["y"]), ("y", ["x"]));

        var found = StronglyConnected(g, 2);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, c => c.Count == 3);
        Assert.Contains(found, c => c.Count == 2);
    }

    [Fact]
    public void A_self_loop_alone_is_not_a_component()
    {
        // A type referencing itself is not a circular dependency worth reporting.
        var g = Graph(("a", ["a"]));
        Assert.Empty(StronglyConnected(g, 2));
    }

    [Fact]
    public void Edges_to_unknown_nodes_do_not_throw()
    {
        // Real input has dangling targets: an edge into an excluded or unloaded type.
        var g = Graph(("a", ["b", "missing"]), ("b", ["a"]));

        var component = Assert.Single(StronglyConnected(g, 2));

        Assert.Equal(["a", "b"], component.Order());
    }

    [Fact]
    public void Deep_chains_do_not_overflow_the_stack()
    {
        // Iterative on purpose: recursion depth is bounded by the longest dependency path,
        // and a large solution can exceed the stack long before anything else.
        const int depth = 50_000;
        var g = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var i = 0; i < depth; i++) g[$"n{i}"] = [$"n{i + 1}"];
        g[$"n{depth}"] = ["n0"];

        var component = Assert.Single(StronglyConnected(g, 2));

        Assert.Equal(depth + 1, component.Count);
    }

    /// <summary>
    /// The partition is a property of the graph; the order it is discovered in is not.
    /// </summary>
    /// <remarks>
    /// Core sorts its output for this reason, and the fixture's single component cannot exercise
    /// it. Reversing the insertion order changes which root the outer loop reaches first, which
    /// is the whole of what the canonical form defends against.
    /// </remarks>
    [Fact]
    public void The_result_does_not_depend_on_insertion_order()
    {
        (string, string[])[] edges =
            [("a", ["b"]), ("b", ["a"]), ("m", ["n"]), ("n", ["m"]), ("x", ["y"]), ("y", ["x"])];

        var forward = StronglyConnected(Graph(edges), 2);
        var backward = StronglyConnected(Graph([.. edges.Reverse()]), 2);

        Assert.Equal(forward, backward);
    }

    // ------------------------------------------------------ the traversable path, A3 ----

    private static IReadOnlyList<string> Loop(Dictionary<string, List<string>> graph, string seed) =>
        IronMarten.Bearing.Graphs.ShortestCycleThrough(
            graph.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal),
            seed);

    /// <summary>The walk is the cycle in order, and it does not repeat the seed at the end.</summary>
    /// <remarks>
    /// The closing edge is implied — <c>[a, b, c]</c> means <c>a → b → c → a</c> — because a walk
    /// that carried its own last step would let a renderer print it twice or a consumer read the
    /// length as one too many. Every caller closes the loop itself.
    /// </remarks>
    [Fact]
    public void A_loop_is_returned_in_traversal_order()
    {
        Assert.Equal(["a", "b", "c"], Loop(Graph(("a", ["b"]), ("b", ["c"]), ("c", ["a"])), "a"));
    }

    /// <summary>Shortest, not first-found — the whole reason it is breadth-first.</summary>
    /// <remarks>
    /// Depth-first from <c>a</c> visiting neighbours in order would take the four-node detour and
    /// return it, which is a real cycle and the wrong one to show: a reader shown the long way
    /// round has more edges to consider than the problem needs.
    /// </remarks>
    [Fact]
    public void The_shortest_loop_wins_over_the_first_one_found()
    {
        var g = Graph(
            ("a", ["b", "z"]),
            ("b", ["c"]), ("c", ["a"]),
            ("z", ["a"]));

        Assert.Equal(["a", "z"], Loop(g, "a"));
    }

    /// <summary>
    /// Two loops of equal length resolve the same way every run.
    /// </summary>
    /// <remarks>
    /// Breadth-first settles the length and neighbour order settles the tie, and neither can be
    /// left to dictionary enumeration: a representative that moved between runs would make an
    /// acknowledged finding come back as new, which is what <c>SubjectRef.ForSet</c> exists to
    /// prevent one level up.
    /// </remarks>
    [Fact]
    public void Equal_length_loops_are_broken_by_identity_and_not_by_insertion_order()
    {
        (string, string[])[] edges = [("a", ["m", "b"]), ("b", ["a"]), ("m", ["a"])];

        Assert.Equal(["a", "b"], Loop(Graph(edges), "a"));
        Assert.Equal(["a", "b"], Loop(Graph([.. edges.Reverse()]), "a"));
    }

    /// <summary>A node on no cycle has no loop, and saying so is not an exception.</summary>
    [Fact]
    public void A_node_on_no_cycle_has_no_loop()
    {
        Assert.Empty(Loop(Graph(("a", ["b"]), ("b", [])), "a"));
        Assert.Empty(Loop(Graph(("a", ["b"]), ("b", [])), "absent"));
    }

    /// <summary>A self-reference is not a loop this tool reports.</summary>
    /// <remarks>
    /// The same judgement as <see cref="A_self_loop_alone_is_not_a_component"/>, and it has to be
    /// made twice: Tarjan drops the component at min-size, and this walks a subgraph it was
    /// handed. A seed with an edge to itself would otherwise return a one-step path, which reads
    /// as a circular dependency between a type and itself.
    /// </remarks>
    [Fact]
    public void A_self_reference_is_not_a_loop()
    {
        Assert.Empty(Loop(Graph(("a", ["a"])), "a"));
        Assert.Equal(["a", "b"], Loop(Graph(("a", ["a", "b"]), ("b", ["a"])), "a"));
    }
}

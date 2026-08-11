using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// Tarjan's SCC over synthetic graphs. Fast, no Roslyn, no workspace load — and it pins the
/// behaviour circular-reference detection depends on before that code moves.
///
/// Job A needs one thing this does not yet provide: a traversable cycle PATH. Tarjan returns
/// component membership ("these six namespaces are entangled"), and what a user can act on
/// is A to B to C to A. See TECHREQ-job-a.md 5.1.
/// </summary>
public sealed class GraphTests
{
    private static Dictionary<string, List<string>> Graph(params (string From, string[] To)[] edges) =>
        edges.ToDictionary(e => e.From, e => e.To.ToList(), StringComparer.Ordinal);

    [Fact]
    public void A_simple_chain_has_no_components()
    {
        var g = Graph(("a", ["b"]), ("b", ["c"]), ("c", []));
        Assert.Empty(Graphs.StronglyConnected(g, 2));
    }

    [Fact]
    public void A_mutual_pair_is_found_at_min_size_two()
    {
        var g = Graph(("a", ["b"]), ("b", ["a"]));

        var found = Graphs.StronglyConnected(g, 2);

        var component = Assert.Single(found);
        Assert.Equal(["a", "b"], component.Order());
    }

    [Fact]
    public void Min_size_suppresses_smaller_components()
    {
        // Mutual pairs and triples are ordinary C# — parent/child, visitor/visited. Listing
        // them buries the signal under things nobody will act on, which is why type tangles
        // are gated at 4 and namespace cycles at 2.
        var g = Graph(("a", ["b"]), ("b", ["a"]), ("c", ["d"]), ("d", ["c"]));

        Assert.Equal(2, Graphs.StronglyConnected(g, 2).Count);
        Assert.Empty(Graphs.StronglyConnected(g, 3));
    }

    [Fact]
    public void Two_independent_cycles_are_reported_separately()
    {
        var g = Graph(
            ("a", ["b"]), ("b", ["c"]), ("c", ["a"]),
            ("x", ["y"]), ("y", ["x"]));

        var found = Graphs.StronglyConnected(g, 2);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, c => c.Count == 3);
        Assert.Contains(found, c => c.Count == 2);
    }

    [Fact]
    public void A_self_loop_alone_is_not_a_component()
    {
        // A type referencing itself is not a circular dependency worth reporting.
        var g = Graph(("a", ["a"]));
        Assert.Empty(Graphs.StronglyConnected(g, 2));
    }

    [Fact]
    public void Edges_to_unknown_nodes_do_not_throw()
    {
        // Real input has dangling targets: an edge into an excluded or unloaded type.
        var g = Graph(("a", ["b", "missing"]), ("b", ["a"]));

        var component = Assert.Single(Graphs.StronglyConnected(g, 2));
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

        var component = Assert.Single(Graphs.StronglyConnected(g, 2));
        Assert.Equal(depth + 1, component.Count);
    }
}

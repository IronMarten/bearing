using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The layered, folded project graph the architecture diagram draws — A7.
/// </summary>
/// <remarks>
/// <para>
/// Built from primitives, for the reason <c>ProjectCycleTests</c> is: the shapes worth asserting
/// about a layered graph are not in the fixture. TestBed has three projects in a chain, which
/// exercises neither the fold, nor a wide layer, nor a cycle — and a cycle is the case that made
/// the spike's layering non-deterministic, so it is the one that most needs a test.
/// </para>
/// <para>
/// <c>SPIKE-job-a-prior-art.md</c> §7 is the source of the numbers these are written against: a
/// layering that produced a widest layer of <b>8, 12 or 20</b> across twelve trials of the same
/// data, and a fold that took twenty-seven boxes to ten.
/// </para>
/// </remarks>
public sealed class ProjectGraphTests
{
    private static ProjectGraph Graph(
        (string TypeId, string Project)[] types,
        params (string From, string To)[] edges) =>
        ProjectGraph.Of(types, edges);

    /// <summary>A chain layers bottom-up: what depends on nothing is layer 0.</summary>
    [Fact]
    public void A_chain_layers_by_depth()
    {
        var graph = Graph(
            [("Api.T", "Api"), ("Svc.T", "Svc"), ("Data.T", "Data")],
            ("Api.T", "Svc.T"), ("Svc.T", "Data.T"));

        Assert.Equal(3, graph.Depth);
        Assert.Equal(0, Layer(graph, "Data"));
        Assert.Equal(1, Layer(graph, "Svc"));
        Assert.Equal(2, Layer(graph, "Api"));
    }

    /// <summary>Depth is the longest path, not the shortest.</summary>
    /// <remarks>
    /// A project that reaches the foundation both directly and through two others belongs above
    /// both, or an edge would have to run sideways — which the drawing has no way to show.
    /// </remarks>
    [Fact]
    public void Depth_follows_the_longest_path()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"), ("A.T", "C.T"));

        Assert.Equal(2, Layer(graph, "A"));
    }

    /// <summary>
    /// Projects with the same dependencies <b>and</b> the same dependents become one box.
    /// </summary>
    /// <remarks>
    /// The nopCommerce shape: seven plugins each depending on exactly Core and Services and
    /// depended on by nothing. The spike measured what saying so is worth — twenty-seven boxes to
    /// ten, 1444px to 574px — and the run at A7 reproduced it at ten boxes and 580px.
    /// </remarks>
    [Fact]
    public void Projects_of_the_same_shape_fold_into_one_box()
    {
        var graph = Graph(
            [("Core.T", "Core"), ("P1.T", "P1"), ("P2.T", "P2"), ("P3.T", "P3")],
            ("P1.T", "Core.T"), ("P2.T", "Core.T"), ("P3.T", "Core.T"));

        var folded = Assert.Single(graph.Groups, g => g.Size > 1);

        Assert.Equal(["P1", "P2", "P3"], folded.Projects);
        Assert.Equal(["Core"], folded.DependsOn);
        Assert.False(folded.IsCycle);
    }

    /// <summary>
    /// The same dependencies is not enough — the dependents have to match too.
    /// </summary>
    /// <remarks>
    /// Folding on outbound edges alone would merge two projects used by different parts of the
    /// system, and a box labelled "and 1 more" gives the reader no way to recover the difference.
    /// </remarks>
    [Fact]
    public void Two_projects_used_differently_do_not_fold()
    {
        var graph = Graph(
            [("Core.T", "Core"), ("A.T", "A"), ("B.T", "B"), ("Top.T", "Top")],
            ("A.T", "Core.T"), ("B.T", "Core.T"),
            ("Top.T", "A.T"));

        Assert.DoesNotContain(graph.Groups, g => g.Size > 1 && g.Projects.Contains("A"));
    }

    // ------------------------------------------------------------------ the cycle ----

    /// <summary>
    /// A cycle is one box, flagged as a cycle rather than as a coincidence.
    /// </summary>
    /// <remarks>
    /// "These three are alike" and "these three are stuck together" are opposite messages, and one
    /// box would say the first unless something distinguishes them.
    /// </remarks>
    [Fact]
    public void A_cycle_becomes_one_box_and_says_so()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("Core.T", "Core")],
            ("A.T", "B.T"), ("B.T", "A.T"),
            ("A.T", "Core.T"));

        var cycle = Assert.Single(graph.Groups, g => g.IsCycle);

        Assert.Equal(["A", "B"], cycle.Projects);
        Assert.Equal(["Core"], cycle.DependsOn);
    }

    /// <summary>
    /// Layering is a function of the graph, cycle or no cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion the spike's failure asks for.</b> §7 recorded a widest layer of 8,
    /// 12 or 20 across twelve trials over identical data, because longest-path over a graph
    /// containing a cycle depends on which node the walk starts from — and
    /// <c>docs/ARCHITECTURE.md</c> §5 requires analysis to be a function.
    /// </para>
    /// <para>
    /// Reversing the input order changes exactly that starting point, which is why it is the
    /// perturbation used here and in <c>GraphTests</c>. Condensing each cycle before layering is
    /// what makes it hold.
    /// </para>
    /// </remarks>
    [Fact]
    public void Layering_does_not_depend_on_the_order_projects_arrive_in()
    {
        (string, string)[] types =
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C"), ("D.T", "D"), ("Core.T", "Core")];

        (string, string)[] edges =
        [
            ("A.T", "B.T"), ("B.T", "C.T"), ("C.T", "A.T"),
            ("A.T", "Core.T"), ("D.T", "A.T"),
        ];

        var forward = ProjectGraph.Of(types, edges);
        var backward = ProjectGraph.Of(types.Reverse(), edges.Reverse());

        Assert.Equal(
            forward.Groups.Select(g => (string.Join(",", g.Projects), g.Layer)),
            backward.Groups.Select(g => (string.Join(",", g.Projects), g.Layer)));

        Assert.Equal(forward.Depth, backward.Depth);
        Assert.Equal(forward.WidestLayer, backward.WidestLayer);
    }

    /// <summary>
    /// A ring of three is one box, and it contains no mutual pair.
    /// </summary>
    /// <remarks>
    /// <b>The case the first implementation missed entirely.</b> It decided "is in a cycle" by
    /// looking for a project that its own dependency also depends on — a mutual <i>pair</i> — which
    /// is true of <c>A ↔ B</c> and false of every member of <c>A → B → C → A</c>. It also keyed the
    /// group on shape <i>as well as</i> on the cycle, which split the pair back apart, since the
    /// members of a cycle by definition do not share dependencies. Both are fixed by using the
    /// strongly-connected component that the layering already computes.
    /// </remarks>
    [Fact]
    public void A_ring_of_three_is_one_box_even_though_no_two_are_mutual()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"), ("C.T", "A.T"));

        var cycle = Assert.Single(graph.Groups);

        Assert.True(cycle.IsCycle);
        Assert.Equal(["A", "B", "C"], cycle.Projects);
        Assert.Empty(cycle.DependsOn);
    }

    /// <summary>Everything in a cycle sits at one depth, because a cycle has no internal order.</summary>
    [Fact]
    public void A_cycles_members_share_a_layer()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("Core.T", "Core")],
            ("A.T", "B.T"), ("B.T", "A.T"), ("A.T", "Core.T"), ("B.T", "Core.T"));

        Assert.Equal(1, Layer(graph, "A"));
        Assert.Equal(1, Layer(graph, "B"));
    }

    // ------------------------------------------------------------------- the width ----

    /// <summary>
    /// Width is what the acceptance criterion is about, and it is askable directly.
    /// </summary>
    /// <remarks>
    /// A plugin host defeats layering by depth — twenty of nopCommerce's twenty-seven projects sit
    /// at one level — so depth says nothing about whether the drawing fits. The fold is what
    /// reduces this, and it is the number to check when a diagram comes out too wide.
    /// </remarks>
    [Fact]
    public void The_widest_layer_is_reported_after_folding()
    {
        var plugins = Enumerable.Range(1, 20).Select(i => ($"P{i}.T", $"P{i}")).ToList();
        var edges = Enumerable.Range(1, 20).Select(i => ($"P{i}.T", "Core.T")).ToArray();

        var graph = ProjectGraph.Of([("Core.T", "Core"), .. plugins], edges);

        // Twenty identical plugins are one fact, so the widest layer is one box and not twenty.
        Assert.Equal(1, graph.WidestLayer);
        Assert.Equal(2, graph.Groups.Count);
    }

    /// <summary>An edge to an unanalysed type contributes no dependency.</summary>
    /// <remarks><c>docs/DEFECTS.md</c> §7, and §1 is what guessing a project would repeat.</remarks>
    [Fact]
    public void An_edge_to_an_unanalysed_type_is_dropped()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B")],
            ("A.T", "Gone.T"), ("A.T", "B.T"));

        Assert.Equal(["B"], graph.Dependencies.Single(d => d.Project == "A").DependsOn);
    }

    private static int Layer(ProjectGraph graph, string project) =>
        graph.Groups.Single(g => g.Projects.Contains(project)).Layer;
}

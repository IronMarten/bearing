using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

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
    /// <summary>
    /// The reduction as project-name pairs, ordinally ordered so a test states the set rather than
    /// the order it happens to be drawn in.
    /// </summary>
    private static (string From, string To)[] Drawn(ProjectGraph graph) =>
        [.. graph.Reduction
            .Select(e => (From: graph.Groups[e.From].Projects[0], To: graph.Groups[e.To].Projects[0]))
            .OrderBy(e => e.From, StringComparer.Ordinal)
            .ThenBy(e => e.To, StringComparer.Ordinal)];

    /// <summary>Which box each project is in — a folded box holds several.</summary>
    private static Dictionary<string, int> BoxOf(ProjectGraph graph)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < graph.Groups.Count; i++)
            foreach (var project in graph.Groups[i].Projects)
                index[project] = i;

        return index;
    }

    /// <summary>What a box reaches, over every dependency or over the reduction alone.</summary>
    private static IReadOnlyList<string> Reaches(ProjectGraph graph, string project, bool whole)
    {
        var index = BoxOf(graph);

        var edges = whole
            ? graph.Groups.SelectMany((g, i) => g.DependsOn.Select(d => (From: i, To: index[d])))
            : graph.Reduction.Select(e => (e.From, e.To));

        var adjacency = edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());

        var seen = new HashSet<int>();
        var queue = new Queue<int>([index[project]]);

        while (queue.Count > 0)
            foreach (var next in adjacency.GetValueOrDefault(queue.Dequeue(), []))
                if (seen.Add(next)) queue.Enqueue(next);

        return [.. seen.Select(i => graph.Groups[i].Projects[0]).Order(StringComparer.Ordinal)];
    }

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

    /// <summary>
    /// A dependency another path already carries is not in the reduction, and is counted.
    /// </summary>
    /// <remarks>
    /// <c>A</c> depends on <c>B</c>, <c>B</c> on <c>C</c>, and
    /// <c>A</c> on <c>C</c> directly. All three dependencies are real; the drawing only has to
    /// carry two, because the third is what a reader traces through the first two. This is the
    /// shape the whole defect is: on the real solutions the skipping edge was drawn, was cut in
    /// half by the box it passed behind, and read as a chain that already existed.
    /// </remarks>
    [Fact]
    public void A_dependency_another_path_carries_is_left_to_that_path()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"), ("A.T", "C.T"));

        Assert.Equal(1, graph.Implied);
        Assert.Equal([("A", "B"), ("B", "C")], Drawn(graph));
    }

    /// <summary>A chain implies nothing: every edge is the only path that carries it.</summary>
    [Fact]
    public void A_chain_leaves_nothing_implied()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"));

        Assert.Equal(0, graph.Implied);
        Assert.Equal([("A", "B"), ("B", "C")], Drawn(graph));
    }

    /// <summary>
    /// The reduction reaches exactly what the whole graph reaches.
    /// </summary>
    /// <remarks>
    /// <b>The property the fix rests on, asserted rather than assumed.</b> Drawing fewer edges is
    /// only honest if <i>what depends on what</i> is unchanged, and on a DAG the transitive
    /// reduction is the largest set of edges for which that holds. A diamond with a shortcut is
    /// the smallest graph where the two could differ: <c>A</c> reaches <c>D</c> four ways and the
    /// reduction keeps none of the direct edge.
    /// </remarks>
    [Fact]
    public void The_reduction_reaches_everything_the_whole_graph_reaches()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C"), ("D.T", "D")],
            ("A.T", "B.T"), ("A.T", "C.T"), ("A.T", "D.T"),
            ("B.T", "D.T"), ("C.T", "D.T"));

        Assert.Equal(1, graph.Implied);
        Assert.DoesNotContain(("A", "D"), Drawn(graph));

        foreach (var box in graph.Groups)
            Assert.Equal(
                Reaches(graph, box.Projects[0], whole: true),
                Reaches(graph, box.Projects[0], whole: false));
    }

    /// <summary>
    /// A layer wider than one row becomes several, and every row after the first says so.
    /// </summary>
    /// <remarks>
    /// <b>The wrapped layer</b>, and the reason
    /// <see cref="IronMarten.Bearing.Cli.ArchitectureDiagram.Rows"/> takes a graph: Jellyfin's
    /// widest layer holds eleven boxes and the fixture holds three projects in a chain, so the
    /// case cannot be reached from a walk. Six siblings over one foundation is the smallest graph
    /// that wraps.
    /// </remarks>
    [Fact]
    public void A_layer_too_wide_for_one_row_is_drawn_as_several()
    {
        // Six siblings over one foundation would FOLD into one box -- same dependencies and same
        // dependents is one fact six times, which is the compression the map exists for. Giving
        // each its own consumer is what keeps them six boxes and makes the layer wide.
        var graph = Graph(
            [("F.T", "F"),
             ("P1.T", "P1"), ("P2.T", "P2"), ("P3.T", "P3"),
             ("P4.T", "P4"), ("P5.T", "P5"), ("P6.T", "P6"),
             ("C1.T", "C1"), ("C2.T", "C2"), ("C3.T", "C3"),
             ("C4.T", "C4"), ("C5.T", "C5"), ("C6.T", "C6")],
            ("P1.T", "F.T"), ("P2.T", "F.T"), ("P3.T", "F.T"),
            ("P4.T", "F.T"), ("P5.T", "F.T"), ("P6.T", "F.T"),
            ("C1.T", "P1.T"), ("C2.T", "P2.T"), ("C3.T", "P3.T"),
            ("C4.T", "P4.T"), ("C5.T", "P5.T"), ("C6.T", "P6.T"));

        Assert.Equal(6, graph.WidestLayer);

        var rows = ArchitectureDiagram.Rows(graph);

        Assert.True(ArchitectureDiagram.Wraps(graph));

        // Layer 2 and layer 1 hold six each, so each is drawn as five and one; layer 0 holds F.
        Assert.Equal([false, true, false, true, false], rows.Select(r => r.Continues));
        Assert.Equal([5, 1, 5, 1, 1], rows.Select(r => r.Boxes.Count));

        // A continuing row is the SAME layer as the one above it, and that is the whole
        // misstatement: everywhere else on this drawing the gap above a row means "depends on".
        Assert.Equal(rows[0].Layer, rows[1].Layer);
        Assert.NotEqual(rows[1].Layer, rows[2].Layer);
        Assert.Equal(rows[2].Layer, rows[3].Layer);

        // One non-continuing row per layer, which is what makes the rules countable.
        Assert.Equal(graph.Depth, rows.Count(r => !r.Continues));
    }

    /// <summary>
    /// Where nothing is too wide, no row continues another and every gap is a layer boundary.
    /// </summary>
    /// <remarks>
    /// The half that keeps the fix from spending ink where there is no ambiguity — nopCommerce and
    /// Umbraco draw no rules, because on them every gap already means what a gap means.
    /// </remarks>
    [Fact]
    public void A_layer_that_fits_does_not_wrap()
    {
        var graph = Graph(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"));

        Assert.False(ArchitectureDiagram.Wraps(graph));
        Assert.All(ArchitectureDiagram.Rows(graph), row => Assert.False(row.Continues));
        Assert.Equal(graph.Depth, ArchitectureDiagram.Rows(graph).Count);
    }

    /// <summary>An edge to an unanalysed type contributes no dependency.</summary>
    /// <remarks>Guessing a project from a name is what this would repeat.</remarks>
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

using System.Globalization;

namespace IronMarten.Bearing;

/// <summary>
/// One box in the architecture diagram: the projects that are the same shape, taken together.
/// </summary>
/// <param name="Projects">Every project in the group, ordered by name. Usually one.</param>
/// <param name="DependsOn">The projects the group depends on, ordered by name.</param>
/// <param name="Layer">How deep it sits — 0 depends on nothing, and each layer above depends on one below.</param>
/// <param name="IsCycle">
/// Whether the group is a set of projects that depend on each other rather than a set that are
/// merely alike.
/// </param>
public readonly record struct ProjectGroup(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> DependsOn,
    int Layer,
    bool IsCycle)
{
    /// <summary>How many projects this box stands for.</summary>
    public int Size => Projects.Count;
}

/// <summary>
/// The project dependency graph, layered and folded — what the architecture diagram draws.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aggregated from type references, not read from project references.</b> Same graph
/// <see cref="Cycles.AmongProjects(SolutionModel)"/> works over, and finer than the reference list:
/// a project reference that nothing actually uses is not a dependency this shows.
/// </para>
/// <para>
/// <b>Layering is a graph computation and lives here; drawing is the renderer's.</b> It has to be a
/// function of the graph and nothing else — <c>SPIKE-job-a-prior-art.md</c> §7 recorded a layering
/// that produced a widest layer of <b>8, 12 or 20</b> across twelve trials of the same data,
/// because longest-path over a graph with a cycle in it depends on where the walk starts. That is
/// <c>docs/ARCHITECTURE.md</c> §5 failing — same inputs, different output — and it is fixed here by
/// condensing each cycle to a single node before layering, so the thing being layered is a DAG by
/// construction rather than by hope.
/// </para>
/// <para>
/// <b>The fold is a claim, which is why it is in Core.</b> Seven plugins that each depend on
/// exactly <c>Core</c> and <c>Services</c> and are depended on by nothing are not seven facts, they
/// are one fact seven times — and the spike measured what saying so is worth: twenty-seven boxes
/// became ten, and 1444px became 574px. It is also *truer*, which is the better argument: the
/// drawing then says "seven plugins, all the same shape" where before it said seven separate
/// things and left the reader to notice.
/// </para>
/// </remarks>
public sealed class ProjectGraph
{
    private ProjectGraph(
        IReadOnlyList<(string Project, IReadOnlyList<string> DependsOn)> dependencies,
        IReadOnlyList<ProjectGroup> groups,
        IReadOnlyList<(int From, int To)> reduction,
        int implied)
    {
        Dependencies = dependencies;
        Groups = groups;
        Reduction = reduction;
        Implied = implied;
    }

    /// <summary>Every analysed project and what it depends on, both ordered by name.</summary>
    public IReadOnlyList<(string Project, IReadOnlyList<string> DependsOn)> Dependencies { get; }

    /// <summary>
    /// The boxes to draw: folded, layered, ordered by layer and then by first project name.
    /// </summary>
    public IReadOnlyList<ProjectGroup> Groups { get; }

    /// <summary>
    /// The dependencies between boxes that carry reachability nothing else carries — the
    /// transitive reduction, as index pairs into <see cref="Groups"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A claim about the graph, which is why it is here and not in the renderer</b> — the same
    /// argument the fold is in Core on. The transitive reduction of a DAG is unique and preserves
    /// reachability exactly, so a drawing of it says precisely what a drawing of every edge says
    /// about what depends on what. It is not a suppression and there is no threshold in it.
    /// </para>
    /// <para>
    /// Boxes are painted after edges and are opaque, so an edge
    /// that skips a layer is not drawn <i>over</i> the box in between — it is cut in half by it,
    /// and the two visible stubs read as <c>A → C</c> and <c>C → B</c>. A direct dependency
    /// becomes a chain through a project it never names. Measured on three solutions, the edges
    /// that get cut are almost exactly the edges that skip a layer: <b>18 of 29 on nopCommerce, 81
    /// of 98 on Jellyfin, 27 of 44 on Umbraco</b>. Every one of them is transitively implied,
    /// which is why the reduction is the fix rather than routing — nopCommerce and Umbraco fall to
    /// <b>0 and 2</b>, and the reader loses no reachability to get there.
    /// </para>
    /// <para>
    /// <b>Routing was measured first and does not fit.</b> An edge crossing a row has to pass
    /// through the gutters between its boxes, and on all three solutions some band is
    /// oversubscribed: nopCommerce needs 7 lines through 6 lanes, Umbraco 12 through 7, and
    /// Jellyfin 41 through 8. There is no routing of every edge that this geometry can hold, so
    /// drawing fewer edges is not a shortcut past the layout engine — it is the only thing left
    /// that does not lie.
    /// </para>
    /// </remarks>
    public IReadOnlyList<(int From, int To)> Reduction { get; }

    /// <summary>
    /// How many box-to-box dependencies <see cref="Reduction"/> leaves for a path to carry.
    /// </summary>
    /// <remarks>
    /// <b>The drawing has to disclose this and both renderers need the same number.</b> A picture
    /// that quietly shows a third of the edges is a familiar shape — an
    /// artifact telling a reader it holds everything when it holds a subset. The count is the
    /// model's so that the sentence cannot disagree with the drawing.
    /// </remarks>
    public int Implied { get; }

    /// <summary>How many layers deep the solution is. A flat solution is 1.</summary>
    public int Depth => Groups.Count == 0 ? 0 : Groups.Max(g => g.Layer) + 1;

    /// <summary>The widest layer, in boxes — what actually sets the drawing's width.</summary>
    /// <remarks>
    /// <c>SPIKE-job-a-prior-art.md</c> §7: a plugin host defeats layering by depth, because twenty
    /// of twenty-seven projects sit at one level. Width is the number that decides whether the
    /// diagram survives being pasted somewhere, and it is worth being able to ask for directly.
    /// </remarks>
    public int WidestLayer => Groups.Count == 0
        ? 0
        : Groups.GroupBy(g => g.Layer).Max(layer => layer.Count());

    /// <summary>Builds the graph for a solution.</summary>
    /// <remarks>
    /// Primitives for the reason <see cref="Cycles.AmongProjects(IEnumerable{ValueTuple{string,
    /// string}}, IEnumerable{ValueTuple{string, string}})"/> and <see cref="ProjectReachability"/>
    /// take them: the shapes worth asserting about a layered graph — a cycle in it, a layer twenty
    /// wide, siblings that fold — are not all in the fixture, and a test that could only run
    /// against three projects in a chain would be asserting the easy case.
    /// </remarks>
    public static ProjectGraph Of(
        IEnumerable<(string TypeId, string Project)> types,
        IEnumerable<(string From, string To)> edges)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(edges);

        var projectOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var dependsOn = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (typeId, project) in types)
        {
            projectOf[typeId] = project;
            if (!dependsOn.ContainsKey(project))
                dependsOn[project] = new SortedSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, to) in edges)
        {
            // Both endpoints analysed, or the edge has no project, and
            // inventing one from a name is what §1 did.
            if (!projectOf.TryGetValue(from, out var source)) continue;
            if (!projectOf.TryGetValue(to, out var target)) continue;

            if (!string.Equals(source, target, StringComparison.Ordinal)) dependsOn[source].Add(target);
        }

        var dependencies = dependsOn
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => (p.Key, (IReadOnlyList<string>)p.Value.ToList()))
            .ToList();

        var (layers, componentOf, componentSize) = Layers(dependsOn);
        var groups = Fold(dependsOn, layers, componentOf, componentSize);
        var (reduction, implied) = Reduce(groups);

        return new ProjectGraph(dependencies, groups, reduction, implied);
    }

    /// <summary>
    /// Depth per project: 0 depends on nothing, and each layer above depends on one below.
    /// </summary>
    /// <remarks>
    /// <b>Cycles are condensed first, and everything in one keeps the same depth.</b> A cycle has
    /// no internal ordering to draw — that is what makes it a cycle — so asking which of its
    /// members is "lower" has no answer, and any layering that produces one produced it from
    /// traversal order. Condensing is what makes the result a function of the graph.
    /// </remarks>
    private static (Dictionary<string, int> Layers,
                    Dictionary<string, int> ComponentOf,
                    Dictionary<int, int> ComponentSize)
        Layers(Dictionary<string, SortedSet<string>> dependsOn)
    {
        var adjacency = dependsOn.ToDictionary(
            p => p.Key,
            p => (IReadOnlyList<string>)p.Value.ToList(),
            StringComparer.Ordinal);

        // minSize 1, so every project comes back — a component of one is the ordinary case.
        var components = Graphs.StronglyConnected(adjacency, 1);

        var componentOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < components.Count; i++)
            foreach (var project in components[i])
                componentOf[project] = i;

        // The condensation's edges, which cannot cycle.
        var above = new Dictionary<int, HashSet<int>>();
        for (var i = 0; i < components.Count; i++) above[i] = [];

        foreach (var (project, targets) in dependsOn)
            foreach (var target in targets)
                if (componentOf[project] != componentOf[target])
                    above[componentOf[project]].Add(componentOf[target]);

        var depth = new Dictionary<int, int>();

        int DepthOf(int component)
        {
            if (depth.TryGetValue(component, out var known)) return known;

            // Marked before recursing so a malformed graph terminates rather than stack-overflows.
            // The condensation is acyclic, so this can only be reached if that ever stops holding.
            depth[component] = 0;

            var deepest = 0;
            foreach (var next in above[component]) deepest = Math.Max(deepest, DepthOf(next) + 1);

            return depth[component] = deepest;
        }

        var layers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (project, component) in componentOf) layers[project] = DepthOf(component);

        var sizes = components.Select((c, i) => (i, c.Count)).ToDictionary(c => c.i, c => c.Count);

        return (layers, componentOf, sizes);
    }

    /// <summary>
    /// Which dependencies between boxes a drawing has to show, and how many it may leave to a path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The box graph is a DAG by construction and does not need condensing again: every dependency
    /// runs to a strictly lower layer, because layering already condensed the cycles and a box's
    /// own members are excluded from its <see cref="ProjectGroup.DependsOn"/>. Two boxes in one
    /// layer therefore cannot reach each other, which is what makes the reachability walk below
    /// terminate without a visited set doing the work.
    /// </para>
    /// <para>
    /// An edge is dropped when some <i>other</i> dependency of the same box already reaches its
    /// target. That is the transitive reduction, and on a DAG it is unique — so this is a function
    /// of the graph in the sense <c>docs/ARCHITECTURE.md</c> §5 means, with no tie to break and no
    /// traversal order to inherit.
    /// </para>
    /// </remarks>
    private static (List<(int From, int To)> Reduction, int Implied) Reduce(List<ProjectGroup> groups)
    {
        var boxOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < groups.Count; i++)
            foreach (var project in groups[i].Projects)
                boxOf[project] = i;

        var edges = new List<HashSet<int>>(groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            var targets = new HashSet<int>();
            foreach (var dependency in groups[i].DependsOn)
                if (boxOf.TryGetValue(dependency, out var box) && box != i)
                    targets.Add(box);

            edges.Add(targets);
        }

        var reach = new Dictionary<int, HashSet<int>>();

        HashSet<int> Reaches(int box)
        {
            if (reach.TryGetValue(box, out var known)) return known;

            var all = new HashSet<int>();
            foreach (var next in edges[box])
            {
                all.Add(next);
                all.UnionWith(Reaches(next));
            }

            return reach[box] = all;
        }

        var reduction = new List<(int From, int To)>();
        var implied = 0;

        for (var i = 0; i < groups.Count; i++)
            foreach (var target in edges[i].OrderBy(t => t))
            {
                // Reachable through a sibling dependency, so a path already carries it.
                if (edges[i].Any(other => other != target && Reaches(other).Contains(target)))
                {
                    implied++;
                    continue;
                }

                reduction.Add((i, target));
            }

        return (reduction, implied);
    }

    /// <summary>
    /// Projects that are the same shape become one box.
    /// </summary>
    /// <remarks>
    /// <b>The same shape means the same dependencies and the same dependents</b>, not merely the
    /// same dependencies. Folding on outbound edges alone would merge two projects that are used by
    /// different parts of the system, which is a difference the drawing exists to show — and the
    /// reader has no way to recover it from a box labelled "and 6 more".
    /// <para>
    /// A cycle is folded too, and flagged, because its members genuinely cannot be separated. That
    /// is a different reason for one box holding several projects, so
    /// <see cref="ProjectGroup.IsCycle"/> distinguishes them: "these six are alike" and "these six
    /// are stuck together" are opposite messages.
    /// </para>
    /// </remarks>
    private static List<ProjectGroup> Fold(
        Dictionary<string, SortedSet<string>> dependsOn,
        Dictionary<string, int> layers,
        Dictionary<string, int> componentOf,
        Dictionary<int, int> componentSize)
    {
        var dependents = dependsOn.Keys.ToDictionary(
            p => p,
            _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var (project, targets) in dependsOn)
            foreach (var target in targets)
                if (dependents.TryGetValue(target, out var set)) set.Add(project);

        // In a cycle means in a strongly-connected component of more than one - the same answer
        // Cycles.AmongProjects gives, and already computed for the layering. Testing for a mutual
        // *pair* instead misses A -> B -> C -> A entirely, which is the shape a three-project
        // cycle actually has.
        bool InACycle(string project) => componentSize[componentOf[project]] > 1;

        return dependsOn.Keys
            .GroupBy(project => InACycle(project)
                // A cycle is one box whatever its members' individual shapes are. Keying on shape
                // as well would split it straight back up, because its members do not share
                // dependencies - that is what having a cycle means.
                ? (Layer: layers[project],
                   Cycle: componentOf[project].ToString(CultureInfo.InvariantCulture),
                   Out: "",
                   In: "")
                : (Layer: layers[project],
                   Cycle: "",
                   Out: string.Join(" ", dependsOn[project]),
                   In: string.Join(" ", dependents[project])))
            .Select(group =>
            {
                var projects = group.OrderBy(p => p, StringComparer.Ordinal).ToList();

                return new ProjectGroup(
                    projects,
                    // A folded box does not depend on its own members.
                    [.. projects.SelectMany(p => dependsOn[p])
                        .Where(t => !projects.Contains(t, StringComparer.Ordinal))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(t => t, StringComparer.Ordinal)],
                    group.Key.Layer,
                    group.Key.Cycle.Length > 0);
            })
            .OrderBy(g => g.Layer)
            .ThenBy(g => g.Projects[0], StringComparer.Ordinal)
            .ToList();
    }
}

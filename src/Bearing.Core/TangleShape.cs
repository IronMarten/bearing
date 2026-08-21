namespace IronMarten.Bearing;

/// <summary>
/// What holds a type tangle together, which decides how it should be read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same question as <see cref="CycleShape"/>, and deliberately not the same answer.</b>
/// Namespace cycles turned out to be mostly artefacts of aggregation — 21 of nopCommerce's 22
/// were folder layout or shared models. Type tangles are not: of the five across nopCommerce and
/// Jellyfin, four survive with every inheritance and implementation edge removed, at 58, 20, 4 and
/// 4 types. They are real, and the section is right to report them.
/// </para>
/// <para>
/// <b>One of the five is not, and it is the only one nopCommerce has.</b>
/// <c>BaseDataProvider</c> with its three concrete providers, <c>DataProviderManager</c> and
/// <c>NopDbStartup</c> dissolves completely — largest remnant 1 — the moment the hierarchy edges
/// come out. Told that "none of them can be tested or changed in isolation", a reader looking at a
/// base class beside its own implementations concludes the section does not know what a class
/// hierarchy is, and stops reading. That is the whole cost of the mislabel, and it is why this
/// says which one it is rather than dropping it.
/// </para>
/// </remarks>
public enum TangleShape
{
    /// <summary>
    /// Types that reach each other by holding, calling and constructing one another. The finding:
    /// none of them can be tested or changed alone, and no inheritance relationship explains it.
    /// </summary>
    /// <remarks>
    /// First, so it is the fallback. A hierarchy reported as a tangle wastes a reader's time; a
    /// tangle excused as a hierarchy loses the finding.
    /// </remarks>
    Entangled,

    /// <summary>
    /// A base type and its own descendants. Remove the inheritance and implementation edges and
    /// nothing mutually dependent is left.
    /// </summary>
    /// <remarks>
    /// Ordinary object orientation, and unavoidable: a base that names its subtypes — a factory
    /// method, a switch over kinds, a registration table — closes a loop with every one of them.
    /// Still reported, because it is a real component and on nopCommerce it is the only one; only
    /// the claim about it changes.
    /// </remarks>
    Hierarchy,
}

/// <summary>
/// A type tangle, what holds it together, and the heaviest pair inside it.
/// </summary>
/// <param name="Tangle">The component itself, unchanged.</param>
/// <param name="Shape">Whether inheritance alone accounts for it.</param>
/// <param name="Kinds">
/// The reference kinds closing it, heaviest first — what a reader would have to unpick.
/// </param>
/// <param name="Heaviest">
/// The two members with the most references between them, or <see langword="null"/> when the
/// tangle has no internal edge the walk attributed. The loop line says the tangle exists; this
/// says where to start.
/// </param>
public sealed record ShapedTangle(
    Cycle Tangle,
    TangleShape Shape,
    IReadOnlyList<EdgeKind> Kinds,
    TanglePair? Heaviest);

/// <summary>Two members of a tangle and how many references run between them, both ways.</summary>
/// <param name="First">The ordinally lower member.</param>
/// <param name="Second">The other one.</param>
/// <param name="Weight">References in both directions together.</param>
public readonly record struct TanglePair(SubjectRef First, SubjectRef Second, int Weight);

/// <summary>
/// Reads each type tangle for what holds it together.
/// </summary>
public static class TangleShapes
{
    /// <summary>
    /// Edges that exist because one type is a kind of another, rather than because it uses one.
    /// </summary>
    /// <remarks>
    /// The pair that a hierarchy is made of. Everything else in <see cref="EdgeKind"/> is a type
    /// choosing to depend on another; these two are a type declaring what it is.
    /// </remarks>
    private static readonly EdgeKind[] Hierarchy =
        [EdgeKind.Inheritance, EdgeKind.InterfaceImplementation];

    /// <summary>
    /// Each tangle, in the order given, with what holds it together.
    /// </summary>
    /// <param name="tangles">The components, from <see cref="Cycles.AmongTypes"/>.</param>
    /// <param name="edges">Every dependency, aggregated per type pair with its kinds.</param>
    public static IReadOnlyList<ShapedTangle> OfTypes(
        IReadOnlyList<Cycle> tangles, IReadOnlyList<Edge> edges)
    {
        ArgumentNullException.ThrowIfNull(tangles);
        ArgumentNullException.ThrowIfNull(edges);

        if (tangles.Count == 0) return [];

        return tangles.Select(t => Shape(t, edges)).ToList();
    }

    private static ShapedTangle Shape(Cycle tangle, IReadOnlyList<Edge> edges)
    {
        var members = tangle.Members.Select(m => m.Canonical).ToHashSet(StringComparer.Ordinal);

        var inner = edges
            .Where(e => members.Contains(e.From.Canonical) && members.Contains(e.To.Canonical))
            .ToList();

        var kinds = inner
            .SelectMany(e => e.References)
            .GroupBy(r => r.Kind)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();

        var byId = tangle.Members.ToDictionary(m => m.Canonical, m => m, StringComparer.Ordinal);

        var heaviest = inner
            .GroupBy(e => Unordered(e.From.Canonical, e.To.Canonical))
            .Select(g => new TanglePair(byId[g.Key.Low], byId[g.Key.High], g.Sum(e => e.Weight)))
            .OrderByDescending(p => p.Weight)
            .ThenBy(p => p.First.Canonical, StringComparer.Ordinal)
            .Cast<TanglePair?>()
            .FirstOrDefault();

        return new ShapedTangle(tangle, Read(members, Uses(inner)), kinds, heaviest);
    }

    /// <summary>
    /// Whether anything mutually dependent survives once inheritance is set aside.
    /// </summary>
    /// <param name="members">The types in the tangle.</param>
    /// <param name="uses">
    /// The edges between them that are not inheritance or implementation, as adjacency.
    /// </param>
    /// <remarks>
    /// <b>Dissolving is the test, not counting hierarchy edges.</b> A tangle can be mostly
    /// inheritance and still hold together without it — Jellyfin's four state classes carry four
    /// inheritance edges and sixteen constructions, and the constructions alone keep all four
    /// mutually dependent. Counting would have called that a hierarchy. Removing the edges and
    /// asking what is left does not.
    /// </remarks>
    public static TangleShape Read(
        IReadOnlySet<string> members, IReadOnlyDictionary<string, IReadOnlyList<string>> uses)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(uses);

        var adjacency = members.ToDictionary(
            m => m,
            m => uses.TryGetValue(m, out var reaches) ? reaches : (IReadOnlyList<string>)[],
            StringComparer.Ordinal);

        // Two, matching Cycles.SmallestRealCycle: what is being asked is whether any mutual
        // dependency at all survives, not whether one large enough to report does. A pair left
        // behind is still a pair the hierarchy did not explain.
        return Graphs.StronglyConnected(adjacency, 2).Count == 0
            ? TangleShape.Hierarchy
            : TangleShape.Entangled;
    }

    private static Dictionary<string, IReadOnlyList<string>> Uses(IReadOnlyList<Edge> inner)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var edge in inner)
        {
            // Any, not all. Every derived-to-base edge on nopCommerce's providers reads
            // Inheritance;Invocation, and those invocations are base.X() calls — they exist
            // because the type is a subclass, not as a dependency it chose. "From is a kind of
            // To" explains everything written on the edge, so the edge goes whole. The reverse
            // direction never carries Inheritance and is kept: a base naming its subtypes is a
            // factory or a registry, which is a choice.
            if (edge.References.Any(r => Hierarchy.Contains(r.Kind))) continue;

            if (!adjacency.TryGetValue(edge.From.Canonical, out var reaches))
                adjacency[edge.From.Canonical] = reaches = [];

            reaches.Add(edge.To.Canonical);
        }

        return adjacency.ToDictionary(
            kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
    }

    private static (string Low, string High) Unordered(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}

/// <summary>
/// The types that actually close a project cycle, in one direction.
/// </summary>
/// <param name="From">The project the references are written in.</param>
/// <param name="To">The project they name.</param>
/// <param name="Weight">How many references there are.</param>
/// <param name="Example">The heaviest single type-to-type dependency among them.</param>
public readonly record struct ProjectLink(string From, string To, int Weight, Edge? Example);

/// <summary>
/// Which types hold a project cycle together.
/// </summary>
/// <remarks>
/// <para>
/// <b>Evidence, and deliberately not a suppression.</b> Namespaces and type tangles both needed a
/// reading that could set an instance aside; this does not, and the difference is not a judgement
/// call. The assembly is the unit anyone extracts, deploys and versions, so two of them naming
/// each other is a finding at any weight — there is no "it is all one component really" available
/// the way there is for a plugin's own folders.
/// </para>
/// <para>
/// <b>What was missing is where to start.</b> The section names the projects and one walk between
/// them and stops, so a cycle closed by a single enum reference and one closed by forty service
/// fields read identically. Both are findings; they are not the same morning's work.
/// </para>
/// <para>
/// <b>Unexercised on both measured solutions, and that is expected rather than untested.</b>
/// nopCommerce and Jellyfin each report no project cycle at all, because an ordinary cross-project
/// edge follows a project reference and MSBuild forbids those from cycling. See
/// <see cref="Cycles.AmongProjects(IEnumerable{ValueTuple{string, string}},
/// IEnumerable{ValueTuple{string, string}})"/> for why the primitives overload exists: this is
/// tested against a constructed cycle, because neither real solution can supply one.
/// </para>
/// </remarks>
public static class ProjectLinks
{
    /// <summary>
    /// Each ordered pair inside <paramref name="cycle"/> that carries at least one reference,
    /// heaviest first.
    /// </summary>
    /// <param name="cycle">A project cycle, from <see cref="Cycles.AmongProjects"/>.</param>
    /// <param name="types">Each analysed type's identity and the project that declares it.</param>
    /// <param name="edges">Every dependency, aggregated per type pair.</param>
    public static IReadOnlyList<ProjectLink> Closing(
        Cycle cycle,
        IEnumerable<(string TypeId, string Project)> types,
        IReadOnlyList<Edge> edges)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(edges);

        var projectOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (typeId, project) in types) projectOf[typeId] = project;

        var members = cycle.Members
            .Select(m => m.Canonical.Replace("project|", "", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var links = new Dictionary<(string, string), List<Edge>>();

        foreach (var edge in edges)
        {
            // Both endpoints analysed, exactly as AmongProjects requires. An edge to a type the
            // walk never declared has no project, and inventing one is docs/DEFECTS.md §1.
            if (!projectOf.TryGetValue(edge.From.Canonical, out var from)) continue;
            if (!projectOf.TryGetValue(edge.To.Canonical, out var to)) continue;

            if (string.Equals(from, to, StringComparison.Ordinal)) continue;
            if (!members.Contains(from) || !members.Contains(to)) continue;

            if (!links.TryGetValue((from, to), out var carrying)) links[(from, to)] = carrying = [];
            carrying.Add(edge);
        }

        return links
            .Select(kv => new ProjectLink(
                kv.Key.Item1,
                kv.Key.Item2,
                kv.Value.Sum(e => e.Weight),
                kv.Value.OrderByDescending(e => e.Weight)
                    .ThenBy(e => e.From.Canonical, StringComparer.Ordinal)
                    .First()))
            .OrderByDescending(l => l.Weight)
            .ThenBy(l => l.From, StringComparer.Ordinal)
            .ThenBy(l => l.To, StringComparer.Ordinal)
            .ToList();
    }
}

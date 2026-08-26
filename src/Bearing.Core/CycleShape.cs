namespace IronMarten.Bearing;

/// <summary>
/// What closes a namespace cycle, which decides whether it is a finding at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The section had one headline and three populations under it.</b> On nopCommerce it opens
/// with 22 mutually-dependent namespace groups and the claim that they "cannot be layered,
/// understood, or extracted independently". Of those 22, one is a coupling problem. Fourteen are
/// a single assembly whose namespaces come from its folders, and the rest are peers that share an
/// entity or a view model and inject nothing. Reporting them at one volume is not conservatism —
/// a reader who checks the first two and finds them harmless has been taught to skip the section,
/// and the finding that was real goes with it.
/// </para>
/// <para>
/// <b>The discriminator is what the closing edges are, not how many there are.</b> Weight cannot
/// separate these populations: a plugin's root class naming its own view component is one
/// reference and so is a service holding another service. What separates them is whether the
/// namespaces hold each other as state, and whether they are peers or a folder and its parent.
/// </para>
/// </remarks>
public enum CycleShape
{
    /// <summary>
    /// Sibling namespaces that hold each other as state. Neither can be extracted, understood or
    /// tested without the other, and no file move fixes it.
    /// </summary>
    /// <remarks>
    /// Deliberately first, so it is what an unclassifiable cycle falls back to. A cycle reported
    /// that turns out to be benign costs a reader one check; a cycle silenced that turns out to be
    /// real costs them the finding, and they never learn it was there.
    /// </remarks>
    Coupling,

    /// <summary>
    /// One assembly, and one namespace in the cycle contains all the others. Folder structure
    /// surfacing as namespaces, not a layering violation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A project with no <c>&lt;RootNamespace&gt;</c> gets its namespaces from its directories, so
    /// <c>/Components</c>, <c>/Factories</c> and <c>/Services</c> become namespaces whether or not
    /// anyone intended them as boundaries. The root then holds the settings class and the entry
    /// point, the folders reference the settings, and the root references the folders. That is a
    /// cycle by construction, and it is what a cohesive component looks like.
    /// </para>
    /// <para>
    /// <b>The assembly is why this is safe to silence.</b> The unit anyone can actually extract is
    /// the <c>.dll</c>, and a cycle inside one is not an obstacle to extracting it. That is also
    /// why the single-project test is a condition and not a heuristic: without it the claim would
    /// be about namespaces nobody ships separately.
    /// </para>
    /// </remarks>
    FolderLayout,

    /// <summary>
    /// Peers that name each other's entities, models or enums and hold none of them. Nothing is
    /// entangled at run time; there is no object graph here to cycle.
    /// </summary>
    /// <remarks>
    /// The shape a view-model or domain-entity layer takes. <c>Nop.Web.Models.Boards</c> and
    /// <c>Nop.Web.Models.Common</c> each name a type in the other and neither holds a service;
    /// so does a root abstraction that returns the domain types its own subfolders declare.
    /// Reporting it asks a reader to break a dependency that costs nothing to keep.
    /// </remarks>
    SharedTypes,
}

/// <summary>
/// Two namespaces in a cycle that each hold the other as state, and how much of it there is.
/// </summary>
/// <param name="First">The ordinally lower namespace, so the pair has one identity.</param>
/// <param name="Second">The other one.</param>
/// <param name="FirstHolds">Held references from <paramref name="First"/> to <paramref name="Second"/>.</param>
/// <param name="SecondHolds">Held references the other way.</param>
public readonly record struct HeldPair(string First, string Second, int FirstHolds, int SecondHolds)
{
    /// <summary>Both directions together — how entangled the pair is.</summary>
    public int Weight => FirstHolds + SecondHolds;
}

/// <summary>
/// What a component's membership says about it, with no <see cref="Cycle"/> attached.
/// </summary>
/// <param name="Shape">What closes it.</param>
/// <param name="Projects">The assemblies its members are declared in, ordinally ordered.</param>
/// <param name="Anchor">The member containing every other, or <see langword="null"/> for peers.</param>
/// <param name="Pairs">Sibling namespaces holding each other, heaviest first.</param>
/// <remarks>
/// Separate from <see cref="ShapedCycle"/> so the judgement can be exercised over hand-written
/// members and weights, the way <see cref="Cycles.AmongProjects"/> takes primitives and for the
/// same reason: <see cref="Cycle"/> cannot be constructed outside this assembly, so a reading
/// that only existed attached to one would be testable only against whatever the fixture happens
/// to contain — and the fixture's single namespace cycle is a folder layout, which is the case
/// this classification exists to set aside rather than the one it exists to find.
/// </remarks>
public sealed record ShapeReading(
    CycleShape Shape,
    IReadOnlyList<string> Projects,
    string? Anchor,
    IReadOnlyList<HeldPair> Pairs)
{
    /// <summary>
    /// The namespaces the <see cref="CycleShape.Coupling"/> reading is actually about: those named
    /// in <c>Pairs</c>, ordinally ordered. Empty for every other shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §58.</b> The shape is decided by whether <i>any</i> sibling pair
    /// holds, and was then applied to the whole component: Umbraco's namespace graph has one
    /// strongly-connected component of <b>363</b>, and the section called all of them siblings that
    /// hold each other as state on the evidence of 14 pairs spanning <b>15</b> namespaces — 4% of
    /// what the sentence covered. The same gap is on the other two solutions, so it is the reading
    /// rather than Umbraco: nopCommerce 10 of 30, Jellyfin 2 of 18.
    /// </para>
    /// <para>
    /// <b>The remedy is to bound the claim, not to re-judge the component.</b> Whether a component
    /// that large is a fact about the codebase or a finding about it is a question with no
    /// measurement behind it; which namespaces the evidence covers is arithmetic. So the component
    /// keeps its size and its membership — it is true that all 363 reach each other — and the
    /// sentence about holding state is made over exactly the set that holds.
    /// </para>
    /// <para>
    /// <b>The framework namespace falls out of the headline as a consequence.</b>
    /// <c>Microsoft.Extensions.Hosting</c> is in Umbraco's component by the model's definition and
    /// stays there, but it is in no holding pair, so it is no longer named as evidence of coupling
    /// in Umbraco's own architecture. That was §58's second face and it needed no rule of its own.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Coupled => CycleShapes.Coupled(Pairs);
}

/// <summary>
/// A namespace cycle and the reading of it.
/// </summary>
/// <param name="Cycle">The component itself, unchanged.</param>
/// <param name="Shape">What closes it.</param>
/// <param name="Projects">The assemblies its members are declared in, ordinally ordered.</param>
/// <param name="Anchor">
/// The member that contains every other, or <see langword="null"/> when the members are peers.
/// </param>
/// <param name="Pairs">
/// Sibling namespaces holding each other as state, heaviest first. Empty unless
/// <paramref name="Shape"/> is <see cref="CycleShape.Coupling"/>, and it is the evidence for it.
/// </param>
public sealed record ShapedCycle(
    Cycle Cycle,
    CycleShape Shape,
    IReadOnlyList<string> Projects,
    string? Anchor,
    IReadOnlyList<HeldPair> Pairs)
{
    /// <summary>Whether this is the kind of cycle the section exists to report.</summary>
    public bool IsReportable => Shape == CycleShape.Coupling;

    /// <summary>
    /// The namespaces the <see cref="CycleShape.Coupling"/> reading is actually about: those named
    /// in <c>Pairs</c>, ordinally ordered. Empty for every other shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §58.</b> The shape is decided by whether <i>any</i> sibling pair
    /// holds, and was then applied to the whole component: Umbraco's namespace graph has one
    /// strongly-connected component of <b>363</b>, and the section called all of them siblings that
    /// hold each other as state on the evidence of 14 pairs spanning <b>15</b> namespaces — 4% of
    /// what the sentence covered. The same gap is on the other two solutions, so it is the reading
    /// rather than Umbraco: nopCommerce 10 of 30, Jellyfin 2 of 18.
    /// </para>
    /// <para>
    /// <b>The remedy is to bound the claim, not to re-judge the component.</b> Whether a component
    /// that large is a fact about the codebase or a finding about it is a question with no
    /// measurement behind it; which namespaces the evidence covers is arithmetic. So the component
    /// keeps its size and its membership — it is true that all 363 reach each other — and the
    /// sentence about holding state is made over exactly the set that holds.
    /// </para>
    /// <para>
    /// <b>The framework namespace falls out of the headline as a consequence.</b>
    /// <c>Microsoft.Extensions.Hosting</c> is in Umbraco's component by the model's definition and
    /// stays there, but it is in no holding pair, so it is no longer named as evidence of coupling
    /// in Umbraco's own architecture. That was §58's second face and it needed no rule of its own.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Coupled => CycleShapes.Coupled(Pairs);

}

/// <summary>
/// Reads each namespace cycle for what closes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pass over the detected set, not a filter inside <see cref="Cycles"/>.</b> The graph and
/// the components are unchanged and still available — <see cref="SolutionModel.NamespaceCycles"/>
/// answers "what is mutually dependent" exactly as before. This answers the separate question of
/// which of those a reader should be shown first, and keeping them apart is what lets the
/// suppressed set be disclosed and listed rather than silently dropped.
/// </para>
/// </remarks>
public static class CycleShapes
{
    /// <summary>
    /// What it takes for one namespace to hold another rather than merely name it: a field or
    /// property whose type is the other namespace's abstraction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves were needed, and the second one only after reading real output.</b> The
    /// field test alone is the state-holding relation, which is the right idea —
    /// <see cref="EdgeKind.Parameter"/> looks more direct and is not, since the walk takes it from
    /// <c>ParameterSyntax</c> and cannot tell a constructor parameter from a method argument. But
    /// on nopCommerce the field test alone reported <c>Nop.Web.Areas.Admin.Models.Catalog</c>
    /// against <c>.Settings</c>: four namespaces of view models whose properties are each other's
    /// view models. That is composition of data, and it is exactly the population
    /// <see cref="CycleShape.SharedTypes"/> exists to keep quiet.
    /// </para>
    /// <para>
    /// <b>Requiring the target to be an abstraction is what separates them.</b> A service holding
    /// another namespace's interface is a wired dependency in the object graph; a model holding a
    /// concrete model is a shape. The cost is a service injected as a concrete class, which this
    /// will not see — and that is the direction that loses a finding, so it is worth saying
    /// plainly rather than only counting as a win. It is tolerable here because the test is
    /// mutual: the pair has to hold in both directions, and a component wired concretely in one
    /// direction is not the layering trap the section is looking for.
    /// </para>
    /// <para>
    /// <b>Measured against the direct signal before being trusted.</b> Counting nopCommerce's
    /// <c>protected readonly</c> declarations gives 13 mutually-holding namespaces in
    /// <c>Nop.Services</c>; reading constructor parameter lists gives 17, over the same heaviest
    /// pairs. Both agree on which namespaces are entangled, which is the accuracy this needs — it
    /// asks whether any sibling pair holds, not how many.
    /// </para>
    /// </remarks>
    private static bool IsHeld(TypeReference reference, TypeNode target) =>
        reference.Kind == EdgeKind.Field && target.IsAbstractOrInterface;

    /// <summary>
    /// Each namespace cycle, in the order given, with what closes it.
    /// </summary>
    /// <param name="cycles">The components, from <see cref="Cycles.AmongNamespaces"/>.</param>
    /// <param name="types">Every analysed type, for its namespace and declaring project.</param>
    /// <param name="edges">Every dependency, aggregated per type pair with its kinds.</param>
    public static IReadOnlyList<ShapedCycle> OfNamespaces(
        IReadOnlyList<Cycle> cycles,
        IReadOnlyList<TypeNode> types,
        IReadOnlyList<Edge> edges)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(edges);

        if (cycles.Count == 0) return [];

        var byId = types.ToDictionary(t => t.Subject.Canonical, StringComparer.Ordinal);
        var nameOf = types
            .Select(NamespaceOf)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(SubjectRef.ForNamespace, n => n);

        var projects = types
            .GroupBy(NamespaceOf, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Select(t => t.Project).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        var holds = HeldReferences(byId, edges);

        return cycles.Select(cycle => Shape(cycle, nameOf, projects, holds)).ToList();
    }

    /// <summary>
    /// How many held references run from each namespace to each other one.
    /// </summary>
    /// <remarks>
    /// Aggregated once for the whole solution rather than per cycle: the components partition the
    /// namespaces, so no pair is counted twice, and doing it inside the loop would re-walk every
    /// edge for each component.
    /// </remarks>
    private static Dictionary<(string From, string To), int> HeldReferences(
        Dictionary<string, TypeNode> byId,
        IReadOnlyList<Edge> edges)
    {
        var holds = new Dictionary<(string, string), int>();

        foreach (var edge in edges)
        {
            // A dangling endpoint has no namespace, exactly as in Cycles.AmongNamespaces. Guessing
            // one from the canonical name is how a cycle gets invented rather than found.
            if (!byId.TryGetValue(edge.From.Canonical, out var from)) continue;
            if (!byId.TryGetValue(edge.To.Canonical, out var to)) continue;

            var weight = edge.References.Count(r => IsHeld(r, to));
            if (weight == 0) continue;

            var key = (NamespaceOf(from), NamespaceOf(to));
            if (string.Equals(key.Item1, key.Item2, StringComparison.Ordinal)) continue;

            holds[key] = holds.GetValueOrDefault(key) + weight;
        }

        return holds;
    }

    /// <summary>
    /// Reads one component's membership: what closes it, what it spans, and the pairs that hold.
    /// </summary>
    /// <param name="members">The namespaces in the component.</param>
    /// <param name="projects">Which assemblies declare types in each namespace.</param>
    /// <param name="held">
    /// How many held references run between each ordered pair of namespaces. A pair absent from
    /// this is a pair that names but does not hold.
    /// </param>
    public static ShapeReading Read(
        IReadOnlyList<string> members,
        IReadOnlyDictionary<string, IReadOnlySet<string>> projects,
        IReadOnlyDictionary<(string From, string To), int> held)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(held);

        var spans = members
            .SelectMany(m => projects.TryGetValue(m, out var p) ? p : NoProjects)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var anchor = members.FirstOrDefault(candidate =>
            members.All(other =>
                string.Equals(other, candidate, StringComparison.Ordinal) ||
                other.StartsWith(candidate + ".", StringComparison.Ordinal)));

        var mutual = 0;
        var siblings = new List<HeldPair>();

        foreach (var first in members)
        foreach (var second in members)
        {
            if (string.CompareOrdinal(first, second) >= 0) continue;
            if (!held.TryGetValue((first, second), out var forward) || forward == 0) continue;
            if (!held.TryGetValue((second, first), out var back) || back == 0) continue;

            mutual++;
            if (AreSiblings(first, second)) siblings.Add(new HeldPair(first, second, forward, back));
        }

        siblings = siblings
            .OrderByDescending(p => p.Weight)
            .ThenBy(p => p.First, StringComparer.Ordinal)
            .ToList();

        var shape =
            siblings.Count > 0 ? CycleShape.Coupling
            : spans.Count == 1 && anchor is not null ? CycleShape.FolderLayout
            : mutual == 0 ? CycleShape.SharedTypes
            // Held both ways between a namespace and one it contains, across more than one
            // assembly. Rare, and not a shape with a reading — so it takes the fallback rather
            // than a label that would be a guess.
            : CycleShape.Coupling;

        return new ShapeReading(shape, spans, anchor, siblings);
    }

    private static readonly IReadOnlySet<string> NoProjects =
        new HashSet<string>(StringComparer.Ordinal);

    private static ShapedCycle Shape(
        Cycle cycle,
        IReadOnlyDictionary<SubjectRef, string> nameOf,
        IReadOnlyDictionary<string, IReadOnlySet<string>> projects,
        Dictionary<(string From, string To), int> holds)
    {
        var members = cycle.Members
            .Select(m => nameOf.GetValueOrDefault(m, m.Canonical))
            .ToList();

        var reading = Read(members, projects, holds);

        return new ShapedCycle(cycle, reading.Shape, reading.Projects, reading.Anchor, reading.Pairs);
    }

    /// <summary>
    /// Whether two namespaces are peers, rather than one containing the other.
    /// </summary>
    /// <remarks>
    /// The distinction the folder-layout reading rests on. A plugin's root holding its own
    /// <c>.Services</c> and that service holding the root's settings is one component wired to
    /// itself; two sibling feature namespaces holding each other is two components that cannot be
    /// separated. Both are cycles and only the second is a finding.
    /// </remarks>
    /// <summary>
    /// The distinct namespaces named in <paramref name="pairs"/>, ordinally ordered.
    /// </summary>
    /// <remarks>
    /// One implementation for <see cref="ShapeReading.Coupled"/> and
    /// <see cref="ShapedCycle.Coupled"/>, so the reading and the cycle it is attached to cannot
    /// come to disagree about which namespaces the evidence covers — the arrangement
    /// <c>docs/DEFECTS.md</c> §46 came out of, avoided here by there being one of it.
    /// </remarks>
    public static IReadOnlyList<string> Coupled(IReadOnlyList<HeldPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        return [.. pairs
            .SelectMany(pair => new[] { pair.First, pair.Second })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    private static bool AreSiblings(string first, string second) =>
        !first.StartsWith(second + ".", StringComparison.Ordinal) &&
        !second.StartsWith(first + ".", StringComparison.Ordinal);

    private static string NamespaceOf(TypeNode type) =>
        string.IsNullOrEmpty(type.Namespace) ? "<global>" : type.Namespace;
}

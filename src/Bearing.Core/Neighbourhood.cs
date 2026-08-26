namespace IronMarten.Bearing;

/// <summary>One project's share of a subject's neighbours, and the types in it.</summary>
/// <param name="Project">The declaring project.</param>
/// <param name="Types">Its types, ordered by name.</param>
public sealed record NeighbourGroup(string Project, IReadOnlyList<TypeNode> Types);

/// <summary>
/// What one type depends on and what depends on it, one hop out, grouped by project.
/// </summary>
/// <remarks>
/// <para>
/// <b>A8, and it is a list rather than a drawing because the population made it one.</b> The
/// drill-down is only ever opened from a finding, and a finding is a claim that its subject is
/// extreme, so the neighbourhoods it has to render are the largest in the codebase by
/// construction. Measured over `Selection.Exemplars` on the three reference solutions: a one-hop
/// median of 66, 100 and 88 against 5-7 for types generally, and 19 of the 28 distinct leads over
/// 50 nodes — <c>BaseEntity</c> at 458, <c>BaseItem</c> at 358,
/// <c>UmbracoBuilderExtensions</c> at 708. Drawing those is the hairball
/// <c>TECHREQ-job-a.md</c> §5.5 exists to refuse; listing them is 64-122 KB and withholds nothing.
/// <c>MEASURE-ego-reach.md</c>.
/// </para>
/// <para>
/// <b>The bar it is aimed at is completeness, not speed.</b> A11 round 2's T5 is the only measured
/// thing the report does that reading the source does not — <i>"they weren't unsure how to get the
/// answer, they were just trying to make sure their answer was complete"</i> — and round 1's own
/// result was that the searchable inventory beat the structural diagram at the navigation task. So
/// <b>nothing here truncates.</b> A long group carries its count and a renderer may fold it; a
/// renderer may not show the first n and stop, because that is the one failure this feature exists
/// to avoid.
/// </para>
/// <para>
/// <b>Grouped by project because that is the question the acceptance sentence asks</b> — <i>"what
/// does this depend on, and what depends on it"</i> — and because it is legible at the top: four
/// to eight groups per lead on the three solutions. It is not legible at the bottom, where one
/// group can hold 294 or 420 rows, which is what the fold is for.
/// </para>
/// <para>
/// <b>Built from the edge sets and never from <see cref="TypeNode.FanOut"/>.</b> Those disagreed
/// until D63 was fixed on 2026-08-26, and a count printed beside a list of a different length is
/// D50's defect in a new place. They reconcile now; building from the edges keeps it true if they
/// ever stop.
/// </para>
/// <para>
/// <b>A type in both directions appears in both.</b> A mutual dependency is two facts about the
/// subject and collapsing it to one would understate a direction the reader asked about, so
/// <see cref="Neighbourhood.Distinct"/> is offered separately for anyone who needs the node count
/// rather than the edge count.
/// </para>
/// </remarks>
/// <param name="Subject">The type the neighbourhood is about.</param>
/// <param name="DependsOn">Its outbound neighbours, grouped by project, largest group first.</param>
/// <param name="DependedOnBy">Its inbound neighbours, grouped the same way.</param>
public sealed record Neighbourhood(
    TypeNode Subject,
    IReadOnlyList<NeighbourGroup> DependsOn,
    IReadOnlyList<NeighbourGroup> DependedOnBy)
{
    /// <summary>How many types it depends on.</summary>
    public int DependsOnCount => DependsOn.Sum(g => g.Types.Count);

    /// <summary>How many types depend on it.</summary>
    public int DependedOnByCount => DependedOnBy.Sum(g => g.Types.Count);

    /// <summary>Distinct types in the neighbourhood, counting a mutual dependency once.</summary>
    public int Distinct => DependsOn.Concat(DependedOnBy)
        .SelectMany(g => g.Types)
        .Select(t => t.Subject.Canonical)
        .Distinct(StringComparer.Ordinal)
        .Count();
}

/// <summary>Builds a <see cref="Neighbourhood"/> from the model.</summary>
public static class Neighbourhoods
{
    /// <summary>
    /// The one-hop neighbourhood of <paramref name="subject"/>, or null if it is not a type in
    /// this model.
    /// </summary>
    /// <remarks>
    /// Returns null rather than an empty neighbourhood for a subject the model does not carry,
    /// because those are different answers: a member-level finding has no type neighbourhood to
    /// show, and a type with no neighbours is a fact worth printing.
    /// </remarks>
    public static Neighbourhood? Of(SolutionModel model, SubjectRef subject)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(subject);

        if (model.Find(subject) is not { } node) return null;

        return new Neighbourhood(node, Group(model, node.Outbound), Group(model, node.Inbound));
    }

    private static IReadOnlyList<NeighbourGroup> Group(SolutionModel model, IReadOnlySet<SubjectRef> refs) =>
    [
        .. refs
            .Select(model.Find)
            .OfType<TypeNode>()
            .GroupBy(t => t.Project, StringComparer.Ordinal)
            // Largest group first, because the answer to "what does this depend on" is usually a
            // project rather than a type. Ties break on name so two runs of one solution order
            // identically -- the goldens are compared byte for byte.
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new NeighbourGroup(
                g.Key,
                [.. g.OrderBy(t => t.Name, StringComparer.Ordinal)
                     .ThenBy(t => t.FullyQualifiedName, StringComparer.Ordinal)]))
    ];
}

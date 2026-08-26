namespace IronMarten.Bearing;

/// <summary>
/// Readings over a cycle finding's <see cref="Finding.Relations"/> — one derivation, called by both
/// renderers.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because two renderers drifted apart, and it is the shape that closes that.</b>
/// The cycle family carried its evidence as three hand-rolled pair types, computed inside the
/// shape classifiers and read separately by each renderer — so a change could subtract a
/// population from both and add the evidence replacing it to one, which is exactly what drift was.
/// The finding now carries the relations, and every aggregate a renderer wants is derived here.
/// </para>
/// <para>
/// <b>The relations are type-level and the aggregates are not, which is the decision this
/// implements.</b> A project cycle's finding carries the type→type references that cross a project
/// boundary inside it, because that is what was measured; <i>ProjA → ProjB: 4 references, heaviest
/// TypeX → TypeY</i> is a reading of them. Storing the aggregate instead would have meant either a
/// fourth field on <see cref="Relation"/> that is null for every other kind, or an ordering
/// contract between a link and its exemplar that nothing could enforce.
/// </para>
/// <para>
/// <b>The renderers call this rather than each aggregating</b> — the sentence above is only true if
/// the derivation has one home, and moving it into two renderers would have rebuilt that mechanism
/// while removing its instance.
/// </para>
/// </remarks>
public static class CycleEvidence
{
    /// <summary>
    /// Project-to-project links inside a project cycle, heaviest first, each naming the single
    /// heaviest type-level reference that carries it.
    /// </summary>
    /// <param name="model">The analysed solution, for the type-to-project lookup.</param>
    /// <param name="relations">A <see cref="FindingKind.ProjectCycle"/> finding's relations.</param>
    public static IReadOnlyList<ProjectLink> ProjectLinks(SolutionModel model, IReadOnlyList<Relation> relations)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(relations);

        // The project of a type is read from the model rather than from the canonical's assembly
        // segment. They agree on both reference solutions and they are not the same thing: the
        // canonical is keyed on assembly because type identity needs it to be, and a project
        // is what a .csproj declares. Deriving one from the other would be a new assumption in a
        // file whose whole subject is evidence.
        var projectOf = model.Types.ToDictionary(
            t => t.Subject.Canonical, t => t.Project, StringComparer.Ordinal);

        var links = new Dictionary<(string From, string To), List<Relation>>();

        foreach (var relation in relations)
        {
            if (!projectOf.TryGetValue(relation.From.Canonical, out var from)) continue;
            if (!projectOf.TryGetValue(relation.To.Canonical, out var to)) continue;
            if (string.Equals(from, to, StringComparison.Ordinal)) continue;

            if (!links.TryGetValue((from, to), out var carrying)) links[(from, to)] = carrying = [];
            carrying.Add(relation);
        }

        return
        [
            .. links
                .Select(kv => new ProjectLink(
                    kv.Key.From,
                    kv.Key.To,
                    kv.Value.Sum(r => r.Weight),
                    kv.Value
                        .OrderByDescending(r => r.Weight)
                        .ThenBy(r => r.From.Canonical, StringComparer.Ordinal)
                        .First()))
                .OrderByDescending(l => l.Weight)
                .ThenBy(l => l.From, StringComparer.Ordinal)
                .ThenBy(l => l.To, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// The heaviest unordered pair inside a tangle, summing both directions, or
    /// <see langword="null"/> when the tangle carries no attributable reference.
    /// </summary>
    public static TanglePair? HeaviestPair(IReadOnlyList<Relation> relations)
    {
        ArgumentNullException.ThrowIfNull(relations);

        return relations
            .GroupBy(r => Unordered(r.From, r.To))
            .Select(g => new TanglePair(g.Key.Low, g.Key.High, g.Sum(r => r.Weight)))
            .OrderByDescending(p => p.Weight)
            .ThenBy(p => p.First.Canonical, StringComparer.Ordinal)
            .Cast<TanglePair?>()
            .FirstOrDefault();
    }

    private static (SubjectRef Low, SubjectRef High) Unordered(SubjectRef a, SubjectRef b) =>
        string.CompareOrdinal(a.Canonical, b.Canonical) <= 0 ? (a, b) : (b, a);
}

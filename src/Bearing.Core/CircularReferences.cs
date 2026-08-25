namespace IronMarten.Bearing;

/// <summary>
/// The three circular-reference findings — <c>TECHREQ-job-b.md</c> §3.12.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a new detector.</b> Circular references shipped as a Job A deliverable
/// (<c>TECHREQ-job-a.md</c> §5.1) before §3 existed, and were never folded back in. Every
/// judgement below already ran; what is new is that it arrives as a <see cref="Finding"/>, so it
/// acquires identity, receipts, qualifiers, relations and suppression like every other claim.
/// The reason is <c>SCHEMA-findings-export.md</c> §1: the report renders these, so the export must
/// carry them, so they must be findings.
/// </para>
/// <para>
/// <b>Three kinds and not one.</b> They gate differently — <c>MinTangle</c> applies only to
/// tangles — suppress differently — <see cref="CycleShape"/> applies only to namespaces — and the
/// report already renders three sections. One kind carrying three subject shapes would put a
/// <c>switch</c> in every consumer.
/// </para>
/// <para>
/// <b>The subject is a <see cref="SubjectKind.Set"/>, which is what it was built for and what
/// nothing had used.</b> <see cref="SubjectRef.ForSet"/> sorts and de-duplicates, so the same
/// component discovered from a different entry point keeps one identity — written for
/// acknowledgment memory, on something that could not be acknowledged until now.
/// </para>
/// <para>
/// <b>These do not compete for the lead or the census</b>, and that is
/// <see cref="Cli.Claims.CompetesForLead"/>'s job rather than this file's. They are claims —
/// <c>IsRiskClaim</c> is true for all three — and the distinction is the whole reason there are two
/// predicates.
/// </para>
/// </remarks>
public static class CircularReferences
{
    /// <summary>
    /// Sibling namespaces that hold each other as state. Ungated by size: two namespaces holding
    /// each other is a finding.
    /// </summary>
    /// <remarks>
    /// The <see cref="CycleShape"/> reading arrives as a qualifier rather than as a filter, which
    /// is <c>TECHREQ-job-b.md</c> §4's rule: the shapes that are not reported are <b>suppressed</b>,
    /// with the reason named, and a suppression that cannot be observed to withhold anything is
    /// worse than none. Reading them here and returning early would make the withholding
    /// unobservable, which is exactly what the fold was warned against.
    /// </remarks>
    public static IEnumerable<Finding> AmongNamespaces(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var found = new List<(int Weight, Finding Finding)>();

        foreach (var shaped in model.ShapedNamespaceCycles)
        {
            var held = shaped.Pairs.Sum(pair => pair.Weight);

            found.Add((held, new Finding(
                new FindingKey(FindingKind.NamespaceCycle, shaped.Cycle.Subject),
                [
                    // Ungated, and deliberately: Receipt.Gated has to name a policy value and
                    // there is none. Cycles.SmallestRealCycle is 2 and is not movable.
                    Receipt.Of("Members", shaped.Cycle.Size),
                    Receipt.Of("HeldReferences", held),
                ],
                [
                    new Qualifier(Qualifiers.OneAssemblysOwnFolders, shaped.Shape == CycleShape.FolderLayout),
                    new Qualifier(Qualifiers.PeersNamingSharedTypes, shaped.Shape == CycleShape.SharedTypes),
                ],
                [],
                Holding(shaped))));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Weight), f => f.Finding);
    }

    /// <summary>
    /// Two projects each naming a type in the other. Legal MSBuild — only <i>project references</i>
    /// cannot cycle, and this is the type graph aggregated, which is finer than the references are.
    /// Ungated: the assembly is the unit anyone extracts, so any weight is a finding.
    /// </summary>
    public static IEnumerable<Finding> AmongProjects(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.ProjectCycles.Count == 0) return [];

        var projectOf = model.Types.ToDictionary(
            t => t.Subject.Canonical, t => t.Project, StringComparer.Ordinal);
        var found = new List<(int Weight, Finding Finding)>();

        foreach (var cycle in model.ProjectCycles)
        {
            var crossing = Crossing(cycle, projectOf, model.Edges);
            var carried = crossing.Sum(r => r.Weight);

            found.Add((carried, new Finding(
                new FindingKey(FindingKind.ProjectCycle, cycle.Subject),
                [
                    Receipt.Of("Members", cycle.Size),
                    Receipt.Of("References", carried),
                ],
                [],
                [],
                crossing)));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Weight), f => f.Finding);
    }

    /// <summary>
    /// <c>MinTangle</c> or more types that all reach each other. Mutual pairs and triples are
    /// ordinary C#; this is the family's one threshold, and it is <c>TECHREQ-job-a.md</c> §5.1's
    /// rather than one of §5's.
    /// </summary>
    public static IEnumerable<Finding> AmongTypes(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var found = new List<(int Size, Finding Finding)>();

        foreach (var shaped in model.ShapedTypeTangles)
        {
            found.Add((shaped.Tangle.Size, new Finding(
                new FindingKey(FindingKind.TypeTangle, shaped.Tangle.Subject),
                [
                    Receipt.Gated("Members", shaped.Tangle.Size, nameof(AnalysisPolicy.MinTangle)),
                ],
                [
                    new Qualifier(Qualifiers.ATypeHierarchy, shaped.Shape == TangleShape.Hierarchy),
                ],
                [],
                Holding(shaped, model.Edges))));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Size), f => f.Finding);
    }

    /// <summary>
    /// Every type-level reference that crosses a project boundary inside the cycle.
    /// </summary>
    /// <remarks>
    /// <b>Type level, not project level, and that is the decision.</b> <i>ProjA → ProjB: 4
    /// references, heaviest TypeX → TypeY</i> is a reading of these rather than a thing to store:
    /// storing it would need either a fourth field on <see cref="Relation"/> that is null for every
    /// other kind, or an ordering contract between a link and its exemplar that nothing enforces.
    /// <see cref="CycleEvidence.ProjectLinks"/> is the reading, and it has one home.
    /// </remarks>
    private static IReadOnlyList<Relation> Crossing(
        Cycle cycle,
        Dictionary<string, string> projectOf,
        IReadOnlyList<Edge> edges)
    {
        var members = cycle.Members
            .Select(m => m.Canonical.Replace("project|", "", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. edges
                .Where(e =>
                    projectOf.TryGetValue(e.From.Canonical, out var from) &&
                    projectOf.TryGetValue(e.To.Canonical, out var to) &&
                    !string.Equals(from, to, StringComparison.Ordinal) &&
                    members.Contains(from) && members.Contains(to))
                .Select(e => new Relation(e.From, e.To, e.Weight))
        ];
    }

    /// <summary>
    /// A held pair becomes <b>two</b> directed relations rather than one flagged as mutual.
    /// </summary>
    /// <remarks>
    /// <see cref="HeldPair"/> already carries the two directions separately and sums them only for
    /// display, so this removes the directed/undirected distinction rather than encoding it — and
    /// it exposes more than the report shows: <i>Common → Orders 5, Orders → Common 4</i> rather
    /// than a flat 9. <c>SCHEMA-findings-export.md</c> §6.
    /// </remarks>
    private static IReadOnlyList<Relation> Holding(ShapedCycle shaped) =>
    [
        .. shaped.Pairs.SelectMany(pair => new[]
        {
            new Relation(SubjectRef.ForNamespace(pair.First), SubjectRef.ForNamespace(pair.Second), pair.FirstHolds),
            new Relation(SubjectRef.ForNamespace(pair.Second), SubjectRef.ForNamespace(pair.First), pair.SecondHolds),
        })
    ];

    /// <summary>
    /// Every reference running between two members of the tangle, by direction.
    /// </summary>
    /// <remarks>
    /// <b>Wider than what the section prints, and that is the point of carrying it here.</b> The
    /// renderers name only the heaviest pair (<see cref="CycleEvidence.HeaviestPair"/>), which is a
    /// display choice; the finding carries the evidence the choice was made from, so a second
    /// renderer cannot arrive at a different one. That is <c>docs/DEFECTS.md</c> §46's mechanism
    /// removed rather than repaired.
    /// </remarks>
    private static IReadOnlyList<Relation> Holding(ShapedTangle shaped, IReadOnlyList<Edge> edges)
    {
        var members = shaped.Tangle.Members
            .Select(m => m.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. edges
                .Where(e => members.Contains(e.From.Canonical) && members.Contains(e.To.Canonical))
                .OrderByDescending(e => e.Weight)
                .ThenBy(e => e.From.Canonical, StringComparer.Ordinal)
                .ThenBy(e => e.To.Canonical, StringComparer.Ordinal)
                .Select(e => new Relation(e.From, e.To, e.Weight))
        ];
    }
}

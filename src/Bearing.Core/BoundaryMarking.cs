namespace IronMarten.Bearing;

/// <summary>
/// Job B's half of the boundary section. <c>TECHREQ-job-b.md</c> §3.10.
/// </summary>
/// <remarks>
/// <para>
/// <b>The section replaced an enumeration of every controller, and must not regress into one.</b>
/// Listing contact points fires on 100% of a category the reader already knows about, and a flag
/// that never discriminates is one people learn to skip. What is worth saying is which individual
/// boundaries are unusual — and the two ways a boundary can be unusual are different enough to be
/// separate claims.
/// </para>
/// <para>
/// <b>The section has two owners and this class is one of them.</b> The count of contact points
/// and the integration map are properties of the solution rather than claims about any subject,
/// so they are Job A's and live on the model — <see cref="SolutionModel.ContactPoints"/> and
/// <see cref="SolutionModel.Integrations"/>. What is here is Job B's: the two ways an individual
/// boundary can be unusual, each of which can be right or wrong about a particular type and
/// therefore has to be suppressible. The renderer assembles one section from both halves; nothing
/// else joins them, and neither half may be derived from the other.
/// </para>
/// <para>
/// The contact-point statement — that consumer impact of changes at any of them is outside what
/// static analysis can see — is invariant 4 and is not optional wording. It belongs to the count,
/// so it belongs to Job A.
/// </para>
/// </remarks>
public static class BoundaryMarking
{
    /// <summary>The kinds that are external contact points.</summary>
    private static readonly string[] Boundaries = [TypeKinds.ApiBoundary, TypeKinds.ExternalCall];

    /// <summary>Boundaries with real decisions inside them.</summary>
    /// <remarks>
    /// <para>
    /// Decisions at an external edge are the hardest kind to change later: the consumers are on
    /// the other side of the boundary, so the tool cannot see who depends on the behaviour and
    /// neither, usually, can the team.
    /// </para>
    /// <para>
    /// <b>Two conditions, and the second one is why this fires as rarely as it should.</b> The complexity
    /// floor alone fired on <b>19.5% of nopCommerce's 672 boundaries and 33.3% of jellyfin's
    /// 174</b> — a claim about a third of the population it filters is describing that population,
    /// not finding an anomaly in it, and <i>"boundaries carrying real logic"</i> said about a third
    /// of all boundaries is close to saying <i>"boundaries"</i>. The rank condition makes the
    /// selectivity explicit and constant across codebases: <b>34 on nopCommerce and 9 on
    /// jellyfin</b>.
    /// </para>
    /// <para>
    /// <b>The rule is the one written for this file's other half.</b>
    /// <see cref="WidestSurfaces"/> carries it — a section prints only when it discriminates —
    /// and it was articulated there and never applied here, immediately above it.
    /// </para>
    /// <para>
    /// <b>The floor stays, and it is what lets this find nothing.</b> A rank gate on its own
    /// nominates the top 5% of boundaries however tame they all are, which is
    /// <c>docs/ARCHITECTURE.md</c> §9's gate that cannot fail. Both conditions are gated receipts,
    /// so a reader can see which one a boundary cleared and which one kept it out.
    /// </para>
    /// </remarks>
    public static IEnumerable<Finding> CarryingRealLogic(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var boundaries = model.Types.Where(IsBoundary).ToList();
        if (boundaries.Count == 0) return [];

        // The population is the boundaries, which is the set the claim is about — measured over
        // the whole solution it would be a different gate, and one that says nothing about how
        // unusual a boundary is among boundaries.
        var logic = Distribution.Of(boundaries.Select(type => (double)type.MaxMemberCyclomatic));
        var topRank = logic.TopRankLimit(policy.BoundaryTopFraction);

        var found = new List<(int Complexity, Finding Finding)>();

        foreach (var type in boundaries)
        {
            if (type.MaxMemberCyclomatic < policy.HighCc) continue;

            var rank = logic.RankOf(type.MaxMemberCyclomatic);
            if (rank > topRank) continue;

            found.Add((type.MaxMemberCyclomatic, new Finding(
                new FindingKey(FindingKind.BoundaryCarriesLogic, type.Subject),
                [
                    Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.HighCc)),
                    Receipt.Gated("MaxMemberCyclomaticRank", rank, nameof(AnalysisPolicy.BoundaryTopFraction)),
                    // The limit and the population it was taken over, for the reason BlastRadius
                    // gives: a rank alone does not say what it had to beat, and it moves with the
                    // number of boundaries rather than with the policy.
                    Receipt.Of("BoundaryTopRankLimit", topRank),
                    Receipt.Of("BoundaryCount", boundaries.Count),
                    Receipt.Of("MedianBoundaryCyclomatic", logic.Median),
                    Receipt.Of("DataShape", type.DataShape),
                    Receipt.Of("FanIn", type.FanIn),
                    Receipt.Of("FanOut", type.FanOut),
                ],
                [],
                type.MostComplexMember is { } member ? [member.Subject] : [])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Complexity), f => f.Finding);
    }

    /// <summary>
    /// Boundaries whose contract surface is unusually wide for this solution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Whether this may be said at all is decided in <see cref="Suppression"/>, not here</b>,
    /// and the reason is a suppression that could never fire. The rule is that the section prints only when
    /// it discriminates, and the probe expresses that as a proportion — suppress when the
    /// qualifying set exceeds half the boundaries. It can never fire. The qualifying filter is
    /// <c>DataShape &gt;= 1.5 × median</c>, which is proportional to the same distribution the
    /// ceiling is measured against, so the set lands on the threshold and never crosses; and above
    /// ten boundaries the probe's <c>Take(5)</c> caps it besides. Adding qualifiers makes it
    /// <i>less</i> able to fire, which the fixture now demonstrates at seven.
    /// </para>
    /// <para>
    /// The replacement is an absolute count, <see cref="AnalysisPolicy.MaxNamedSurfaces"/>. What
    /// goes wrong is not a proportion at all: the section promises to name what stands out and
    /// instead reads a list, and a count is what bounds a list. It also turns the probe's
    /// <c>Take(5)</c> from a silent truncation into the gate itself — past the ceiling the section
    /// says nothing rather than naming an arbitrary five of the qualifiers.
    /// </para>
    /// <para>
    /// No cap is applied here. Core emits every finding and lets suppression remove the set as a
    /// set; a detector that truncated would leave the renderer unable to say how much it dropped,
    /// which is truncation nobody is told about.
    /// </para>
    /// </remarks>
    public static IEnumerable<Finding> WidestSurfaces(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var boundaries = model.Types.Where(IsBoundary).ToList();
        if (boundaries.Count == 0) return [];

        var surface = Distribution.Of(boundaries.Select(type => (double)type.DataShape));
        var threshold = policy.SurfaceOutlierThreshold(surface.Median);

        var found = new List<(int Surface, Finding Finding)>();

        foreach (var type in boundaries)
        {
            if (type.DataShape < threshold) continue;

            found.Add((type.DataShape, new Finding(
                new FindingKey(FindingKind.WidestContractSurface, type.Subject),
                [
                    Receipt.Gated("DataShape", type.DataShape, nameof(AnalysisPolicy.SurfaceOutlierMultiple)),
                    // The bar and what set it, because "12 fields" means nothing without them and
                    // because the median is the half of this gate a reader cannot see.
                    Receipt.Of("SurfaceThreshold", threshold),
                    Receipt.Of("MedianBoundarySurface", surface.Median),
                    Receipt.Of("BoundaryCount", boundaries.Count),
                    Receipt.Of("PublicMemberCount", type.PublicMemberCount),
                ],
                [],
                [])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Surface), f => f.Finding);
    }

    private static bool IsBoundary(TypeNode type) =>
        Boundaries.Contains(type.Classification.Kind, StringComparer.Ordinal);
}

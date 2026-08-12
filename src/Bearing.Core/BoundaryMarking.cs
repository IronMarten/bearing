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
/// The count of contact points, split inbound and outbound, is the section's third part and is
/// not a claim about any subject. It is a property of the solution and the renderer reads it from
/// the model, the way it reads coverage. Its accompanying statement — that consumer impact of
/// changes at any of them is outside what static analysis can see — is invariant 4 and is not
/// optional wording.
/// </para>
/// </remarks>
public static class BoundaryMarking
{
    /// <summary>The kinds that are external contact points.</summary>
    private static readonly string[] Boundaries = ["ApiBoundary", "ExternalCall"];

    /// <summary>Boundaries with real decisions inside them.</summary>
    /// <remarks>
    /// Decisions at an external edge are the hardest kind to change later: the consumers are on
    /// the other side of the boundary, so the tool cannot see who depends on the behaviour and
    /// neither, usually, can the team.
    /// </remarks>
    public static IEnumerable<Finding> CarryingRealLogic(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(int Complexity, Finding Finding)>();

        foreach (var type in model.Types)
        {
            if (!IsBoundary(type)) continue;
            if (type.MaxMemberCyclomatic < policy.HighCc) continue;

            found.Add((type.MaxMemberCyclomatic, new Finding(
                new FindingKey(FindingKind.BoundaryCarriesLogic, type.Subject),
                [
                    Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.HighCc)),
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
    /// and the reason is <c>docs/DEFECTS.md</c> §12. The rule is that the section prints only when
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
    /// which is <c>docs/DEFECTS.md</c> §3.
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

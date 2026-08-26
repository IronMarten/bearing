namespace IronMarten.Bearing;

/// <summary>
/// <i>"N internal callers depend on this contract. External consumers are not visible."</i>
/// <c>TECHREQ-job-b.md</c> §3.5.
/// </summary>
/// <remarks>
/// <para>
/// <b>The worst-saturating finding measured, and the first one converted.</b> On two real
/// solutions it ran to 4.5% and 7.9% of every type — 252 candidates on the larger — and it moved
/// the <i>wrong</i> way as the codebase grew, because the gate was a kind filter plus an absolute
/// fan-in floor and one of those solutions was controller-heavy at 20.3% <c>ApiBoundary</c>. A
/// flag on 7.9% of a codebase is a roll-call, which is invariant 2 failing at scale.
/// <c>SPIKE-job-a-prior-art.md</c> §7.3–7.4.
/// </para>
/// <para>
/// <b>The gate is a share of the whole solution, and "the whole solution" is the decision.</b>
/// Blast radius takes its share within each cohort, and that is right for it: it asks how far a
/// defect propagates <i>compared with a type's peers</i>. This finding asks a different question
/// with a different reader in mind. Within-cohort would answer <i>"which controller is riskiest to
/// change"</i> — a maintainer's question, useful to someone who already knows the codebase.
/// Solution-wide answers <i>"which part of this application is riskiest to change"</i>, which is
/// what someone arriving at an unfamiliar codebase is asking, and it is the reading §3.5 was
/// written for: <b>not cohort-gated, it runs over all types</b>. Nothing drops out for want of a
/// peer group, so a lone contract with thirty callers still speaks.
/// </para>
/// <para>
/// <b>Both readings are real and only one is implemented.</b> The maintainer's view is a second
/// nomination set rather than a wording change, so it is a board decision and not a quiet
/// addition here.
/// </para>
/// <para>
/// <b>Two defects close with this port.</b> The floor was
/// <see cref="AnalysisPolicy.MinCohort"/>, a cohort-size threshold used as a fan-in floor on a
/// finding that has no cohort in it — the two share a default of 5, which is what hid it for the
/// whole probe build. It is <see cref="AnalysisPolicy.MinFanIn"/> now, and the two are pinned
/// apart. The absolute gate is joined by a proportional one, which is the only thing that
/// makes a threshold hold still across codebases.
/// </para>
/// <para>
/// <b>The floor stays, and that is not a hedge.</b> §3.4 keeps its absolute floor beside its rank
/// gate for invariant 1's reason: a share alone crowns the top of a population where nothing is
/// tall. In a solution where the most-depended-on type has two callers, the top 5% is still
/// somebody.
/// </para>
/// </remarks>
public static class ChangeCost
{
    /// <summary>
    /// The kinds whose consumers may lie outside the solution — which is why the finding says so.
    /// </summary>
    private static readonly string[] Eligible = [TypeKinds.Contract, TypeKinds.ApiBoundary];

    /// <summary>Nominates the types an edit is most distributed across.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(int FanIn, Finding Finding)>();

        // Over every type, cohort or no cohort. The population is the application, because that
        // is what the claim is about.
        var solution = Distribution.Of(model.Types.Select(type => (double)type.FanIn));
        var limit = solution.TopRankLimit(policy.ChangeCostTopFraction);

        foreach (var type in model.Types)
        {
            if (!Eligible.Contains(type.Classification.Kind, StringComparer.Ordinal)) continue;

            // MinFanIn, never MinCohort — there is no cohort here for a
            // cohort threshold to be a threshold on.
            if (type.FanIn < policy.MinFanIn) continue;

            if (solution.Read(type.FanIn) is not { } reading) continue;
            if (reading.Rank > limit) continue;

            found.Add((type.FanIn, new Finding(
                new FindingKey(FindingKind.ChangeCost, type.Subject),
                [
                    Receipt.Gated("FanIn", type.FanIn, nameof(AnalysisPolicy.MinFanIn)),
                    Receipt.Gated("FanInSolutionRank", reading.Rank, nameof(AnalysisPolicy.ChangeCostTopFraction)),
                    // The limit travels because a rank alone does not say what it had to beat, and
                    // this one scales with the size of the solution rather than of a peer group.
                    Receipt.Of("FanInSolutionRankLimit", limit),
                    Receipt.Of("FanInSolutionPctl", reading.Percentile),
                    Receipt.Of("SolutionTypeCount", model.Types.Count),
                    // How much there is to get wrong at the point of contact. The sentence quotes
                    // it, and it is the measure §3.10's widest-contract-surface reads too.
                    Receipt.Of("DataShape", type.DataShape),
                    Receipt.Of("InboundReferenceCount", type.InboundReferenceCount),
                ],
                [],
                // Invariant 7: who the distributed edit is distributed across. The whole cost of
                // the change is these types, and naming them is the difference between a number
                // and a work estimate.
                [.. type.Inbound.OrderBy(caller => caller.Canonical, StringComparer.Ordinal)])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.FanIn), f => f.Finding);
    }
}

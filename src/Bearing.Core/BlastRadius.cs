namespace IronMarten.Bearing;

/// <summary>
/// <i>"Widely depended on relative to its peers, and internally complex — a bug here
/// propagates."</i> <c>TECHREQ-job-b.md</c> §3.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four conditions, all required, and the redundancy between them is the design.</b> This is
/// invariant 1's canonical case: an early build gated on the fan-in percentile alone, ranked
/// eight normalizers with one caller each at the 100th percentile, and fired on all eight. A
/// percentile ranks but does not scale, so it will happily crown the tallest member of a cohort
/// where nothing is tall. The absolute floor is what stops it, and the multiple-of-median is
/// what stops a cohort of near-identical values reading as a spread.
/// </para>
/// <para>
/// <b>Distinct from load-bearing-and-intricate, not a variant of it</b> (<c>PRD-free-tier.md</c>
/// §7.2). This asks how far a defect propagates and answers it relative to peers; load-bearing
/// asks how insulated a type is and answers it in absolute terms, cohort-free. Both may fire on
/// one type, and unlike breaks-alone and concealed decision that is not a contradiction, so
/// there is no suppression between them.
/// </para>
/// <para>
/// Cohort-relative, so gated by <see cref="AnalysisPolicy.MinCohort"/> — row 7 of the
/// suppression matrix. Types below that floor belong to the coverage finding.
/// </para>
/// </remarks>
public static class BlastRadius
{
    /// <summary>Nominates types whose failure reaches an unusual amount of the system.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(int FanIn, Finding Finding)>();

        foreach (var group in model.Types.GroupBy(t => t.Cohort.Key, StringComparer.Ordinal))
        {
            var peers = group.ToList();
            if (peers.Count < policy.MinCohort) continue;

            var fanIn = Distribution.Of(peers.Select(t => (double)t.FanIn));
            var complexity = Distribution.Of(peers.Select(t => (double)t.Cyclomatic));

            // Computed once per cohort, not per type: it is a property of the group's size.
            var topRank = fanIn.TopRankLimit(policy.BlastTopFraction);

            foreach (var type in peers)
            {
                // The absolute floor, first and on its own terms. Everything below is relative,
                // and relative measures discard magnitude — which is the point of them and also
                // how this finding failed.
                if (type.FanIn < policy.MinFanIn) continue;

                if (fanIn.Read(type.FanIn) is not { } inbound) continue;
                if (complexity.Read(type.Cyclomatic) is not { } cc) continue;

                if (inbound.TimesMedian < policy.BlastFanInMultiple) continue;

                // "Top of its peer group", as a rank rather than a percentile. Identical to the
                // probe's FanInPctl >= 95 in every cohort of ten or more, and reachable below
                // ten where that gate was not. docs/DEFECTS.md §14.
                if (inbound.Rank > topRank) continue;

                if (cc.Percentile < policy.BlastComplexityPercentile) continue;

                found.Add((type.FanIn, new Finding(
                    new FindingKey(FindingKind.BugBlastRadius, type.Subject),
                    [
                        Receipt.Gated("CohortSize", peers.Count, nameof(AnalysisPolicy.MinCohort)),
                        Receipt.Gated("FanIn", type.FanIn, nameof(AnalysisPolicy.MinFanIn)),
                        Receipt.Gated("FanInXMedian", inbound.TimesMedian, nameof(AnalysisPolicy.BlastFanInMultiple)),
                        Receipt.Gated("FanInRank", inbound.Rank, nameof(AnalysisPolicy.BlastTopFraction)),
                        Receipt.Gated("CyclomaticPctl", cc.Percentile, nameof(AnalysisPolicy.BlastComplexityPercentile)),
                        // The limit travels with the finding because the rank alone does not say
                        // what it had to beat, and it varies with cohort size — a reader given
                        // "rank 3" and a policy of 0.05 would have to recompute it to check.
                        Receipt.Of("FanInTopRankLimit", topRank),
                        Receipt.Of("FanInPctl", inbound.Percentile),
                        Receipt.Of("Cyclomatic", type.Cyclomatic),
                        Receipt.Of("FanOut", type.FanOut),
                        Receipt.Of("InboundReferenceCount", type.InboundReferenceCount),
                    ],
                    [],
                    // Invariant 7. The claim is that a bug here propagates; the callers are where
                    // it propagates to, and naming them is the difference between an argument and
                    // a list of places to look.
                    [.. type.Inbound.OrderBy(s => s.Canonical, StringComparer.Ordinal)])));
            }
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.FanIn), f => f.Finding);
    }
}

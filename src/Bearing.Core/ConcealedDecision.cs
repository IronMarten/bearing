namespace IronMarten.Bearing;

/// <summary>
/// <i>"Looks like plumbing, but is a complexity outlier among its peers — and is probably tested
/// like plumbing."</i> <c>TECHREQ-job-b.md</c> §3.2 and §3.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>The method-level nomination is the primary of the two, not a drill-down of the type-level
/// one.</b> On real code the type-level version came back empty while method-level found the
/// right thing (<c>CARRY-FORWARD.md</c> §6): a type whose total complexity is ordinary can still
/// hide one 47-branch method, and any roll-up to the type averages it away. Both are here, and
/// neither is derived from the other.
/// </para>
/// <para>
/// Both are cohort-relative, so both are gated by <see cref="AnalysisPolicy.MinCohort"/> — row 7
/// of the suppression matrix. A type below that floor is not dropped: it belongs to the coverage
/// finding, which states that no peer comparison was possible rather than staying silent.
/// </para>
/// </remarks>
public static class ConcealedDecision
{
    /// <summary>
    /// §3.3 — the same signal on one method, read against the other methods of its type's peer
    /// group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The peer group is the declaring type's, but <b>the population is methods</b>: a method is
    /// being compared against other methods, so the cohort floor counts methods too. In a peer
    /// group of five types the method population is usually many times that, which is why this
    /// nomination survives on solutions where the type-level one is starved.
    /// </para>
    /// <para>
    /// Methods and constructors only. A property with a computed body can conceal a decision and
    /// is not considered here — that is the probe's population and it is carried forward
    /// deliberately, because widening it mid-extraction would change what the tool says while
    /// the oracle diff is the only thing checking it. <c>docs/TESTING.md</c> §6.
    /// </para>
    /// </remarks>
    public static IEnumerable<Finding> AtMethodLevel(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(double Rank, double Absolute, Finding Finding)>();

        var population = model.Types
            .SelectMany(type => type.Members
                .Where(member => member.IsMethodLike)
                .Select(member => (Type: type, Member: member)));

        foreach (var group in population.GroupBy(x => x.Type.Cohort.Key, StringComparer.Ordinal))
        {
            var peers = group.ToList();
            if (peers.Count < policy.MinCohort) continue;

            var complexity = Distribution.Of(peers.Select(p => (double)p.Member.Cyclomatic));

            // Capped share, not a bare fraction: 5% of this cohort is 146 methods on nopCommerce's
            // largest. AnalysisPolicy.ConcealedTopShare carries the measurement.
            var limit = Math.Min(policy.ConcealedTopRank, complexity.TopRankLimit(policy.ConcealedTopShare));

            foreach (var (_, member) in peers)
            {
                // Below this there is no decision to conceal: cc 1 is one linear path and
                // literally zero decision points, and in a cohort of property bags with a
                // median of 0 that would make every single-assignment constructor an infinite
                // outlier. SESSION-NOTES.md #25.
                if (member.Cyclomatic < policy.MinDecisionCc) continue;

                if (complexity.Read(member.Cyclomatic) is not { } reading) continue;

                // Rank is the gate the ratio could not be. A multiple of a cohort median that
                // sits on the floor is not an outlier test: nopCommerce's method medians are 1
                // in 56 of 70 cohorts, so `3x median` evaluates to 3, `cc >= 5` decides 82% of
                // nominations by itself, and the count then grows with the size of the codebase
                // rather than with how unusual anything is — 1,091 nominations on one solution,
                // of which the report can show 15.
                //
                // X16, 2026-08-25: the ratio is no longer the other half of a conjunction, it is
                // evidence. Measured on three solutions, dropping it moves the shipped output
                // +5%, +6% and +11% — it was the least load-bearing of the three gates — and at
                // a zero median `TimesMedian >= OutlierFactor` is satisfied by definition, which
                // is docs/DEFECTS.md §61 and dies here rather than by substituting a constant for
                // the infinity. What it widened is cohorts of 5–24, where a fixed top-3 is a
                // large share of the cohort, and that is what the share below takes back.
                if (reading.Rank > limit) continue;

                // And dispersion decides whether the gap is worth a sentence, which rank cannot:
                // rank is ordinal, so the top of a cohort whose median is already high clears it
                // while being barely above its peers. TestBed's planted evaluators are that case
                // at 1.14x, and P0 planted them as complex code that is NOT anomalous.
                if (!Outlies(complexity, member.Cyclomatic, policy)) continue;

                found.Add((reading.TimesMedian, member.Cyclomatic, new Finding(
                    new FindingKey(FindingKind.ConcealedDecisionMethod, member.Subject),
                    [
                        Receipt.Gated("CohortSize", peers.Count, nameof(AnalysisPolicy.MinCohort)),
                        Receipt.Gated("Cyclomatic", member.Cyclomatic, nameof(AnalysisPolicy.MinDecisionCc)),
                        // Evidence, not a gate, since X16. It still leads the sentence and still
                        // orders the section; what it no longer does is decide — which is also
                        // what stops docs/DEFECTS.md §61 recurring, because a gated receipt is
                        // never null now.
                        Receipt.Of("CyclomaticXMedian", reading.TimesMedian),
                        Receipt.Gated("CyclomaticRank", reading.Rank, nameof(AnalysisPolicy.ConcealedTopRank)),
                        // Both limits travel with the finding because neither can be recomputed
                        // from what is printed, and both now vary with the cohort. BlastRadius
                        // publishes FanInTopRankLimit for the same reason.
                        Receipt.Of("CyclomaticTopRankLimit", limit),
                        Receipt.Gated("CyclomaticOutlierFloor", OutlierFloor(complexity, policy), nameof(AnalysisPolicy.ConcealedDispersionFactor)),
                        Receipt.Of("CohortCyclomaticMad", complexity.MedianAbsoluteDeviation),
                        // The median the ratio was taken against, which is the half of this gate a
                        // reader cannot otherwise see — WidestSurfaces' precedent. It matters more
                        // here than there: on nopCommerce 58 of 70 usable cohorts have a method
                        // median of 1 or 0, so "93x the median" is "cc 93 against a median of 1"
                        // and the multiplication hides which of the two numbers is doing the work.
                        Receipt.Of("MedianCohortCyclomatic", complexity.Median),
                        Receipt.Of("CyclomaticPctl", reading.Percentile),
                        Receipt.Of("Dsm", member.Dsm),
                        Receipt.Of("MaxNestingDepth", member.MaxNestingDepth),
                        Receipt.Of("LinesOfCode", member.LinesOfCode),
                    ],
                    [],
                    [])));
            }
        }

        return Ranked(found);
    }

    /// <summary>
    /// §3.2 — a type whose complexity is far above its peers while its connectivity is ordinary.
    /// </summary>
    /// <remarks>
    /// <b>Connectivity is tested as a ratio, not a percentile, on purpose.</b> In a tied cohort a
    /// fan-out of 5 against peers of 4 lands at the 93rd percentile while being, in substance,
    /// identical — so a percentile would read "unusually connected" off a difference of one.
    /// </remarks>
    public static IEnumerable<Finding> AtTypeLevel(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(double Rank, double Absolute, Finding Finding)>();

        foreach (var group in model.Types.GroupBy(t => t.Cohort.Key, StringComparer.Ordinal))
        {
            var peers = group.ToList();
            if (peers.Count < policy.MinCohort) continue;

            var complexity = Distribution.Of(peers.Select(t => (double)t.MaxMemberCyclomatic));
            var fanIn = Distribution.Of(peers.Select(t => (double)t.FanIn));
            var fanOut = Distribution.Of(peers.Select(t => (double)t.FanOut));

            // X16, 2026-08-25. This arm has no rank gate and does not gain one: the ratio is
            // swapped for dispersion one for one, which is the whole change here. A rank gate was
            // tried beside it and over-cut — 78 -> 38 on nopCommerce against 69 for the swap alone
            // — because two volume-ish conditions stack where the ratio was one. Dispersion also
            // halves the roll-call on its own: suffix:Service falls from 21 findings to 10.
            foreach (var type in peers)
            {
                if (type.MaxMemberCyclomatic < policy.MinDecisionCc) continue;

                if (complexity.Read(type.MaxMemberCyclomatic) is not { } cc) continue;
                if (!Outlies(complexity, type.MaxMemberCyclomatic, policy)) continue;

                if (fanIn.Read(type.FanIn) is not { } inbound) continue;
                if (fanOut.Read(type.FanOut) is not { } outbound) continue;
                if (inbound.TimesMedian > policy.ConcealedFanInCeiling) continue;
                if (outbound.TimesMedian > policy.ConcealedFanOutCeiling) continue;

                found.Add((cc.TimesMedian, type.MaxMemberCyclomatic, new Finding(
                    new FindingKey(FindingKind.ConcealedDecisionType, type.Subject),
                    [
                        Receipt.Gated("CohortSize", peers.Count, nameof(AnalysisPolicy.MinCohort)),
                        Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.MinDecisionCc)),
                        // Evidence since X16, not a gate — see the method arm.
                        // Evidence since X16 — see the method arm.
                        Receipt.Of("MaxMemberCyclomaticXMedian", cc.TimesMedian),
                        Receipt.Gated("MaxMemberCyclomaticOutlierFloor", OutlierFloor(complexity, policy), nameof(AnalysisPolicy.ConcealedDispersionFactor)),
                        Receipt.Of("CohortMaxMemberCyclomaticMad", complexity.MedianAbsoluteDeviation),
                        Receipt.Gated("FanInXMedian", inbound.TimesMedian, nameof(AnalysisPolicy.ConcealedFanInCeiling)),
                        Receipt.Gated("FanOutXMedian", outbound.TimesMedian, nameof(AnalysisPolicy.ConcealedFanOutCeiling)),
                        Receipt.Of("MaxMemberCyclomaticPctl", cc.Percentile),
                        Receipt.Of("FanIn", type.FanIn),
                        Receipt.Of("FanOut", type.FanOut),
                        Receipt.Of("Dsm", type.Dsm),
                    ],
                    [
                        // Row 6 of the suppression matrix, as a fact about the type rather than
                        // as a branch inside a WriteLine. The finding fires either way; what
                        // this decides is whether the sentence may say "looks like plumbing".
                        new Qualifier(
                            Qualifiers.LowAbsoluteConnectivity,
                            type.FanIn < policy.MinFanIn,
                            nameof(AnalysisPolicy.MinFanIn)),
                    ],
                    // Invariant 7. The claim is about one member's complexity, so the finding
                    // names it — the alternative is a reader grepping for what the tool knew.
                    type.MostComplexMember is { } member ? [member.Subject] : [])));
            }
        }

        return Ranked(found);
    }

    /// <summary>Strongest outlier first. See <see cref="Nomination"/> for the final tiebreak.</summary>
    /// <remarks>
    /// <para>
    /// <b>A ratio against a zero median is undefined, not infinite, and must not outrank a
    /// measured one.</b> <c>MinDecisionCc</c> already stops a cohort of property bags making every
    /// constructor an infinite outlier (<c>SESSION-NOTES.md</c> #25), but what survives that floor
    /// still divides by zero: on nopCommerce, 10 of 79 type-level nominations sit in a cohort whose
    /// median is 0. Ordering on the ratio alone put all ten at the top of the section, tied, ahead
    /// of every type whose extremity was actually measured — and <see cref="Nomination"/>'s
    /// tiebreak then settled them alphabetically, which is deterministic and carries no evidence.
    /// </para>
    /// <para>
    /// So the undefined ones rank last and among themselves by absolute complexity, which is the
    /// only thing left that was measured. The section still says <i>"the only complexity among its
    /// N peers"</i> for them, which is true and is a weaker claim than <i>"93x the median"</i>
    /// rather than a stronger one: a cohort of zeros is cleared by any complexity at all.
    /// </para>
    /// <para>
    /// The absolute is a secondary key for the finite ones too. Two types equally extreme against
    /// their own peers are not equally interesting, and alphabetical order says nothing about
    /// which. This matters beyond the section: <c>ARCHITECTURE.md</c> §10 makes each kind's top
    /// row the exemplar the report leads with, so a tiebreak with no evidence in it chooses what
    /// a reader sees first.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether a value is an outlier among its peers, on the spread they actually have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two branches, because a group with no spread supports a different claim rather than a
    /// weaker one.</b> With spread, the question is whether the gap exceeds the gaps already in the
    /// group — <see cref="AnalysisPolicy.ConcealedDispersionFactor"/> deviations above the median.
    /// Without it, any complexity at all is the whole of the group's variation, and the sentence
    /// says so: <i>"the only complexity among the 6 types whose name ends in Trait"</i>.
    /// </para>
    /// <para>
    /// <b>The second branch is only safe because a rank gate stands beside it.</b> <c>x &gt;
    /// median</c> at <c>MAD = 0</c> admits everything above the median, which is
    /// <c>ARCHITECTURE.md</c> §11's trap and is measured at 1.5–1.8x what ships. What bounds it is
    /// <see cref="AnalysisPolicy.ConcealedTopShare"/>, not a constant here. Dispersion decides
    /// whether a gap is meaningful; rank decides how many may say so.
    /// </para>
    /// </remarks>
    private static bool Outlies(Distribution peers, double value, AnalysisPolicy policy) =>
        value > OutlierFloor(peers, policy);

    /// <summary>
    /// The value a member has to beat to be an outlier among these peers. Travels with the finding
    /// because a reader cannot recompute it — the same reason
    /// <see cref="BlastRadius"/> publishes <c>FanInTopRankLimit</c>.
    /// </summary>
    private static double OutlierFloor(Distribution peers, AnalysisPolicy policy)
    {
        var spread = peers.MedianAbsoluteDeviation;

        return spread > 0
            ? peers.Median + (policy.ConcealedDispersionFactor * spread)
            : peers.Median;
    }

    private static List<Finding> Ranked(IEnumerable<(double Rank, double Absolute, Finding Finding)> found) =>
        Nomination.Ranked(
            found.OrderByDescending(f => double.IsFinite(f.Rank))
                .ThenByDescending(f => f.Rank)
                .ThenByDescending(f => f.Absolute),
            f => f.Finding);
}

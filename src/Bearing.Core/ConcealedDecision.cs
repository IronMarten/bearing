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

            foreach (var (_, member) in peers)
            {
                // Below this there is no decision to conceal: cc 1 is one linear path and
                // literally zero decision points, and in a cohort of property bags with a
                // median of 0 that would make every single-assignment constructor an infinite
                // outlier. SESSION-NOTES.md #25.
                if (member.Cyclomatic < policy.MinDecisionCc) continue;

                if (complexity.Read(member.Cyclomatic) is not { } reading) continue;
                if (reading.TimesMedian < policy.OutlierFactor) continue;

                found.Add((reading.TimesMedian, member.Cyclomatic, new Finding(
                    new FindingKey(FindingKind.ConcealedDecisionMethod, member.Subject),
                    [
                        Receipt.Gated("CohortSize", peers.Count, nameof(AnalysisPolicy.MinCohort)),
                        Receipt.Gated("Cyclomatic", member.Cyclomatic, nameof(AnalysisPolicy.MinDecisionCc)),
                        Receipt.Gated("CyclomaticXMedian", reading.TimesMedian, nameof(AnalysisPolicy.OutlierFactor)),
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

            foreach (var type in peers)
            {
                if (type.MaxMemberCyclomatic < policy.MinDecisionCc) continue;

                if (complexity.Read(type.MaxMemberCyclomatic) is not { } cc) continue;
                if (cc.TimesMedian < policy.OutlierFactor) continue;

                if (fanIn.Read(type.FanIn) is not { } inbound) continue;
                if (fanOut.Read(type.FanOut) is not { } outbound) continue;
                if (inbound.TimesMedian > policy.ConcealedFanInCeiling) continue;
                if (outbound.TimesMedian > policy.ConcealedFanOutCeiling) continue;

                found.Add((cc.TimesMedian, type.MaxMemberCyclomatic, new Finding(
                    new FindingKey(FindingKind.ConcealedDecisionType, type.Subject),
                    [
                        Receipt.Gated("CohortSize", peers.Count, nameof(AnalysisPolicy.MinCohort)),
                        Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.MinDecisionCc)),
                        Receipt.Gated("MaxMemberCyclomaticXMedian", cc.TimesMedian, nameof(AnalysisPolicy.OutlierFactor)),
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
    private static List<Finding> Ranked(IEnumerable<(double Rank, double Absolute, Finding Finding)> found) =>
        Nomination.Ranked(
            found.OrderByDescending(f => double.IsFinite(f.Rank))
                .ThenByDescending(f => f.Rank)
                .ThenByDescending(f => f.Absolute),
            f => f.Finding);
}

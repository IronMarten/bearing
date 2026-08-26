namespace IronMarten.Bearing;

/// <summary>
/// Where one type sits in its peer group, and in the solution — the thirteen numbers every
/// relative claim in this tool is made from.
/// </summary>
/// <param name="FanInPercentile">Fan-in against the cohort, midrank. Null where the cohort cannot support a reading.</param>
/// <param name="FanInTimesMedian">Fan-in as a multiple of the cohort median. Null where that median is zero.</param>
/// <param name="FanOutPercentile">Fan-out against the cohort.</param>
/// <param name="FanOutTimesMedian">Fan-out as a multiple of the cohort median.</param>
/// <param name="CyclomaticPercentile">Total complexity against the cohort.</param>
/// <param name="CyclomaticTimesMedian">Total complexity as a multiple of the cohort median.</param>
/// <param name="MaxMemberCyclomaticPercentile">Worst single member against the cohort.</param>
/// <param name="MaxMemberCyclomaticTimesMedian">Worst single member as a multiple of the cohort median.</param>
/// <param name="DsmPercentile">Data-structure manipulation against the cohort.</param>
/// <param name="DsmTimesMedian">Data-structure manipulation as a multiple of the cohort median.</param>
/// <param name="DataShapePercentile">Public surface shape against the cohort.</param>
/// <param name="SolutionFanInPercentile">
/// Fan-in against every analysed type. Never null: the solution is always a population, which is
/// why this is the reading a peerless type still gets.
/// </param>
/// <param name="SolutionMaxMemberCyclomaticPercentile">The same, for the worst single member.</param>
public readonly record struct CohortStatistics(
    double? FanInPercentile,
    double? FanInTimesMedian,
    double? FanOutPercentile,
    double? FanOutTimesMedian,
    double? CyclomaticPercentile,
    double? CyclomaticTimesMedian,
    double? MaxMemberCyclomaticPercentile,
    double? MaxMemberCyclomaticTimesMedian,
    double? DsmPercentile,
    double? DsmTimesMedian,
    double? DataShapePercentile,
    double SolutionFanInPercentile,
    double SolutionMaxMemberCyclomaticPercentile);

/// <summary>
/// The cohort statistics for every analysed type — X9.
/// </summary>
/// <remarks>
/// <para>
/// <b>These existed only in the probe, and only at print time.</b> Its <c>types.csv</c> carried
/// thirteen of them computed inside its renderer; Bearing's carried none, because the model does
/// not hold them and A3–A5 were scoped to the model. That was survivable while the probe was
/// around to produce them and stops being survivable at <c>R2</c>, which is why X9 had a deadline
/// rather than a preference: <b>the only capability the free tool would have given up without
/// noticing.</b>
/// </para>
/// <para>
/// <b>A projection rather than new analysis.</b> Every number here is a reading off
/// <see cref="Distribution"/>, which the detectors already use — this computes nothing they do not
/// and decides nothing at all. It is on the model for the reason
/// <see cref="SolutionModel.ProjectCouplings"/> is: a renderer that derived it would be computing,
/// and two renderers deriving it separately would be <c>docs/ARCHITECTURE.md</c> §3.
/// </para>
/// <para>
/// <b>Blank where there is no peer group, and the rule is the report's own.</b> The <c>NO PEER
/// GROUP</c> section says it in words — <i>"no peer comparison was possible for these, so their
/// percentile and multiple-of-median readings are blank rather than zero"</i> — and an export that
/// wrote <c>50</c> and <c>1</c> there would contradict the page it ships beside. It is also
/// invariant 6: a measurement that may not exist is absent, never a stand-in that sorts.
/// </para>
/// <para>
/// <b>Two deliberate differences from the probe, both narrowing.</b> The probe blanks these only
/// for a cohort of one; this blanks below <see cref="AnalysisPolicy.MinCohort"/>, because a
/// percentile taken over two peers is arithmetic rather than a comparison and the tool already
/// refuses to make claims there. And an <b>undefined</b> ratio — a multiple of a median of zero —
/// is blank here where the probe writes <c>inf</c>, which is that
/// infinity is the absence of a measurement rather than a large one, and it sorts like a large one
/// in every tool that reads this file. The percentile survives in that case and carries the
/// reading.
/// </para>
/// <para>
/// <b>Why the two solution-wide readings are not nullable.</b> They are taken over every analysed
/// type, so they exist whenever the solution does — and they are what a peerless type still gets,
/// which is the whole of what <c>Qualifiers.GloballyExtremeFanIn</c> is built on.
/// </para>
/// </remarks>
public static class CohortStatisticsSet
{
    /// <summary>
    /// Every analysed type's statistics, keyed by <see cref="SubjectRef.Canonical"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, CohortStatistics> ForSolution(
        IReadOnlyList<TypeNode> types, AnalysisPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(policy);

        var solutionFanIn = Distribution.Of(types.Select(t => (double)t.FanIn));
        var solutionMaxCc = Distribution.Of(types.Select(t => (double)t.MaxMemberCyclomatic));

        var statistics = new Dictionary<string, CohortStatistics>(StringComparer.Ordinal);

        foreach (var group in types.GroupBy(t => t.Cohort.Key, StringComparer.Ordinal))
        {
            var peers = group.ToList();

            // The report's rule, applied to the export: below the floor there is no comparison to
            // report, so every cohort-relative column is blank rather than computed.
            var comparable = peers.Count >= policy.MinCohort;

            var fanIn = Distribution.Of(peers.Select(t => (double)t.FanIn));
            var fanOut = Distribution.Of(peers.Select(t => (double)t.FanOut));
            var cyclomatic = Distribution.Of(peers.Select(t => (double)t.Cyclomatic));
            var maxMember = Distribution.Of(peers.Select(t => (double)t.MaxMemberCyclomatic));
            var dsm = Distribution.Of(peers.Select(t => (double)t.Dsm));
            var dataShape = Distribution.Of(peers.Select(t => (double)t.DataShape));

            foreach (var type in peers)
            {
                statistics[type.Subject.Canonical] = new CohortStatistics(
                    Percentile(comparable, fanIn, type.FanIn),
                    TimesMedian(comparable, fanIn, type.FanIn),
                    Percentile(comparable, fanOut, type.FanOut),
                    TimesMedian(comparable, fanOut, type.FanOut),
                    Percentile(comparable, cyclomatic, type.Cyclomatic),
                    TimesMedian(comparable, cyclomatic, type.Cyclomatic),
                    Percentile(comparable, maxMember, type.MaxMemberCyclomatic),
                    TimesMedian(comparable, maxMember, type.MaxMemberCyclomatic),
                    Percentile(comparable, dsm, type.Dsm),
                    TimesMedian(comparable, dsm, type.Dsm),
                    Percentile(comparable, dataShape, type.DataShape),
                    solutionFanIn.PercentileOf(type.FanIn),
                    solutionMaxCc.PercentileOf(type.MaxMemberCyclomatic));
            }
        }

        return statistics;
    }

    private static double? Percentile(bool comparable, Distribution distribution, double value) =>
        comparable ? distribution.PercentileOf(value) : null;

    /// <summary>
    /// The multiple, or nothing where the median is zero.
    /// </summary>
    /// <remarks>
    /// <see cref="Distribution.TimesMedianOf"/> answers
    /// <see cref="double.PositiveInfinity"/> there, deliberately and correctly — the quantity is
    /// undefined and collapsing it to a number would hide that. An export is where it has to become
    /// an absence instead: every consumer of a CSV sorts it, and infinity sorts to the top of a
    /// column as though it were the largest measurement rather than the missing one.
    /// </remarks>
    private static double? TimesMedian(bool comparable, Distribution distribution, double value)
    {
        if (!comparable) return null;

        var times = distribution.TimesMedianOf(value);
        return double.IsFinite(times) ? times : null;
    }
}

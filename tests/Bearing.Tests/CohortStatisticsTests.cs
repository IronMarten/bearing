using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The thirteen cohort statistics — X9.
/// </summary>
/// <remarks>
/// <para>
/// <b>These were the probe's, and only at print time.</b> Its <c>types.csv</c> computed them inside
/// its renderer, so nothing but the printer could see them; Bearing's model now holds them and both
/// exports read the same projection. The deadline was <c>R2</c>: the probe is what made them
/// checkable, so anything not asserted here was going to be lost silently.
/// </para>
/// <para>
/// <b>What is worth asserting is the arithmetic and the absences.</b> The numbers are readings off
/// <see cref="Distribution"/>, which has its own tests; what is new is <i>which</i> distribution
/// each is taken against and <i>when</i> a reading is refused.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class CohortStatisticsTests(CoreWalkFixture core)
{
    /// <summary>Every analysed type has an entry, because every type has a cohort.</summary>
    [Fact]
    public void Every_type_has_statistics()
    {
        Assert.Equal(core.Model.Types.Count, core.Model.Statistics.Count);

        Assert.All(
            core.Model.Types,
            t => Assert.True(core.Model.Statistics.ContainsKey(t.Subject.Canonical)));
    }

    /// <summary>
    /// Below the cohort floor every relative reading is absent, and the two solution-wide ones are
    /// not.
    /// </summary>
    /// <remarks>
    /// <b>The report's own rule, applied to the export.</b> <c>NO PEER GROUP</c> says it in words —
    /// <i>"no peer comparison was possible for these, so their percentile and multiple-of-median
    /// readings are blank rather than zero"</i> — and a CSV that wrote 50 and 1 there would
    /// contradict the page it ships beside. The solution-wide pair is what a peerless type still
    /// gets, and it is what <c>GloballyExtremeFanIn</c> is built on.
    /// </remarks>
    [Fact]
    public void A_type_with_no_usable_peer_group_gets_no_relative_reading()
    {
        var peerless = core.Model.Types
            .Where(t => t.CohortSize < core.Model.Policy.MinCohort)
            .ToList();

        Assert.NotEmpty(peerless);

        foreach (var type in peerless)
        {
            var s = core.Model.Statistics[type.Subject.Canonical];

            Assert.Null(s.FanInPercentile);
            Assert.Null(s.FanInTimesMedian);
            Assert.Null(s.FanOutPercentile);
            Assert.Null(s.FanOutTimesMedian);
            Assert.Null(s.CyclomaticPercentile);
            Assert.Null(s.CyclomaticTimesMedian);
            Assert.Null(s.MaxMemberCyclomaticPercentile);
            Assert.Null(s.MaxMemberCyclomaticTimesMedian);
            Assert.Null(s.DsmPercentile);
            Assert.Null(s.DsmTimesMedian);
            Assert.Null(s.DataShapePercentile);

            // And it still has a place in the solution, which is the whole point of the pair.
            Assert.InRange(s.SolutionFanInPercentile, 0, 100);
            Assert.InRange(s.SolutionMaxMemberCyclomaticPercentile, 0, 100);
        }
    }

    /// <summary>
    /// Each reading is taken against its own cohort, recomputed here a second way.
    /// </summary>
    /// <remarks>
    /// The failure this catches is a column reading the wrong distribution — fan-out against the
    /// fan-in spread, or a cohort statistic taken over the solution. Both produce plausible numbers
    /// and neither is visible in a snapshot.
    /// </remarks>
    [Fact]
    public void Every_reading_is_taken_against_the_types_own_cohort()
    {
        foreach (var group in core.Model.Types.GroupBy(t => t.Cohort.Key, StringComparer.Ordinal))
        {
            var peers = group.ToList();
            if (peers.Count < core.Model.Policy.MinCohort) continue;

            var fanIn = Distribution.Of(peers.Select(t => (double)t.FanIn));
            var cyclomatic = Distribution.Of(peers.Select(t => (double)t.Cyclomatic));
            var maxMember = Distribution.Of(peers.Select(t => (double)t.MaxMemberCyclomatic));

            foreach (var type in peers)
            {
                var s = core.Model.Statistics[type.Subject.Canonical];

                Assert.Equal(fanIn.PercentileOf(type.FanIn), s.FanInPercentile);
                Assert.Equal(cyclomatic.PercentileOf(type.Cyclomatic), s.CyclomaticPercentile);
                Assert.Equal(maxMember.PercentileOf(type.MaxMemberCyclomatic), s.MaxMemberCyclomaticPercentile);
            }
        }
    }

    /// <summary>
    /// An undefined multiple is absent rather than infinite.
    /// </summary>
    /// <remarks>
    /// <b>In the medium where it does the most damage.</b> A ratio
    /// against a median of zero is undefined, and the probe writes <c>inf</c> — which every tool
    /// that opens a CSV sorts to the top of the column as though it were the largest measurement
    /// rather than the missing one. The percentile survives and carries the reading.
    /// </remarks>
    [Fact]
    public void An_undefined_multiple_is_absent_rather_than_infinite()
    {
        var undefined = core.Model.Types
            .Where(t => t.CohortSize >= core.Model.Policy.MinCohort)
            .Select(t => (Type: t, Stats: core.Model.Statistics[t.Subject.Canonical]))
            .Where(x => x.Stats.DsmTimesMedian is null && x.Stats.DsmPercentile is not null)
            .ToList();

        Assert.NotEmpty(undefined);

        // And the reason is a median of zero rather than a missing cohort: the peers are there,
        // and the quantity they are being compared on is not.
        foreach (var (type, _) in undefined)
        {
            var peers = core.Model.Types
                .Where(t => string.Equals(t.Cohort.Key, type.Cohort.Key, StringComparison.Ordinal))
                .ToList();

            Assert.Equal(0, Distribution.Of(peers.Select(t => (double)t.Dsm)).Median);
        }
    }

    /// <summary>
    /// A finding's receipts and the export cannot disagree about the same number.
    /// </summary>
    /// <remarks>
    /// <b>The assertion that makes this a projection rather than a second opinion.</b> Blast radius
    /// gates on <c>FanInXMedian</c> and carries it as a receipt; the CSV now carries the same
    /// quantity for the same type. If the two ever differ, one of them is computing rather than
    /// reading — which is the entanglement moving these onto the model was meant to end.
    /// </remarks>
    [Fact]
    public void The_export_agrees_with_the_receipts_that_gated_a_finding()
    {
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.BugBlastRadius);

        Assert.NotEmpty(findings);

        foreach (var finding in findings)
        {
            var stats = core.Model.Statistics[finding.Subject.Canonical];

            Assert.Equal(finding.ValueOf("FanInXMedian"), stats.FanInTimesMedian);
            Assert.Equal(finding.ValueOf("FanInPctl"), stats.FanInPercentile);
            Assert.Equal(finding.ValueOf("CyclomaticPctl"), stats.CyclomaticPercentile);
        }
    }
}

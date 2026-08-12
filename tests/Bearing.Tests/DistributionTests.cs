using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The comparative substrate, tested without Roslyn. Every finding Bearing makes is a claim
/// about one of these numbers, so the arithmetic is pinned here rather than inferred from the
/// solution-level goldens.
/// </summary>
public sealed class DistributionTests
{
    [Fact]
    public void A_tied_group_puts_everyone_at_the_midpoint()
    {
        // The reason for midrank. Counting "at or below" would put all eight at the 100th
        // percentile, so eight normalizers with one caller each read as top-percentile
        // outliers and the alert fires on the unremarkable majority.
        var d = Distribution.Of(Enumerable.Repeat(1.0, 8));

        Assert.Equal(50.0, d.PercentileOf(1));
    }

    [Fact]
    public void A_unique_maximum_cannot_reach_the_top_of_the_scale()
    {
        // (n-0.5)/n*100. This is docs/DEFECTS.md §14 stated as arithmetic: a gate at 95 is
        // unsatisfiable below ten members, whatever the members look like.
        Assert.Equal(90.0, Distribution.Of([1, 1, 1, 1, 9]).PercentileOf(9));
        Assert.Equal(94.44, Math.Round(Distribution.Of([1, 1, 1, 1, 1, 1, 1, 1, 9]).PercentileOf(9), 2));
        Assert.Equal(95.0, Distribution.Of([1, 1, 1, 1, 1, 1, 1, 1, 1, 9]).PercentileOf(9));
    }

    [Fact]
    public void A_group_of_one_has_no_reading_at_all()
    {
        // The invariant-6 rule, in the model rather than in a writer. Left to arithmetic, the
        // single member ties with itself at midrank 50 and divides by its own median for a
        // ratio of 1.0 — the most extreme outlier in a codebase reading as exactly average.
        var d = Distribution.Of([42]);

        Assert.False(d.IsComparable);
        Assert.Null(d.Read(42));

        // The raw statistics are still computable, and still meaningless. Nothing stops a
        // caller reaching past Read; the point is that the default is honest.
        Assert.Equal(50.0, d.PercentileOf(42));
        Assert.Equal(1.0, d.TimesMedianOf(42));
    }

    [Fact]
    public void An_empty_group_has_no_reading_either()
    {
        var d = Distribution.Of(Array.Empty<double>());

        Assert.Equal(0, d.Count);
        Assert.False(d.IsComparable);
        Assert.Null(d.Read(1));
    }

    [Fact]
    public void Two_values_are_enough_to_be_comparable()
    {
        var d = Distribution.Of([2, 4]);

        var reading = d.Read(4);

        Assert.NotNull(reading);
        Assert.Equal(75.0, reading.Value.Percentile);
        Assert.Equal(3.0, d.Median);
        Assert.Equal(4.0 / 3.0, reading.Value.TimesMedian);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 2.0)]          // odd: the middle value
    [InlineData(new[] { 1, 2, 3, 4 }, 2.5)]       // even: the mean of the two middle values
    [InlineData(new[] { 5 }, 5.0)]
    public void Median_is_the_conventional_one(int[] values, double expected) =>
        Assert.Equal(expected, Distribution.Of(values.Select(v => (double)v)).Median);

    [Fact]
    public void Input_order_does_not_affect_anything()
    {
        var ascending = Distribution.Of([1, 2, 3, 4, 9]);
        var descending = Distribution.Of([9, 4, 3, 2, 1]);

        Assert.Equal(ascending.Median, descending.Median);
        Assert.Equal(ascending.PercentileOf(3), descending.PercentileOf(3));
    }

    [Fact]
    public void A_median_of_zero_yields_infinity_rather_than_a_large_number()
    {
        // "Its peers all measure zero" is a real statement and a different one from "999x the
        // peer median", which reads as a measurement and sorts to the top of any spreadsheet.
        // The model says infinity; how to render that is the renderer's problem.
        var d = Distribution.Of([0, 0, 0, 0, 7]);

        Assert.Equal(0, d.Median);
        Assert.Equal(double.PositiveInfinity, d.TimesMedianOf(7));
    }

    [Fact]
    public void Zero_against_a_zero_median_is_typical_not_infinite()
    {
        // The degenerate case of the above: a value of zero among zeroes is exactly ordinary,
        // and 0/0 must not become infinity or NaN.
        Assert.Equal(1.0, Distribution.Of([0, 0, 0]).TimesMedianOf(0));
    }

    [Fact]
    public void A_value_need_not_belong_to_the_group_it_is_read_against()
    {
        // Global percentiles read a type against the whole solution rather than its cohort,
        // and drift reads today's value against an archived distribution.
        var d = Distribution.Of([1, 2, 3, 4]);

        Assert.Equal(100.0, d.PercentileOf(99));
        Assert.Equal(0.0, d.PercentileOf(-1));
    }

    // ------------------------------------------------------------ rank, and defect 14 ----

    /// <summary>
    /// Rank and percentile are one statistic, so a gate on either admits the same set.
    /// </summary>
    /// <remarks>
    /// <c>rank = n·(100 − pctl)/100 + 0.5</c> is an identity, not an approximation, and it is
    /// what lets <c>docs/DEFECTS.md</c> §14 be repaired without moving a golden: at
    /// <c>fraction = 0.05</c> the rank gate admits exactly what <c>FanInPctl &gt;= 95</c>
    /// admitted, in every cohort where that gate was satisfiable at all. If this ever fails, the
    /// repair has quietly become a retune.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(28)]
    [InlineData(100)]
    public void Rank_is_the_percentile_from_the_other_end(int count)
    {
        // Heavy ties on purpose: the identity has to hold for fractional ranks, which is where
        // an off-by-a-half in either direction would otherwise hide.
        var values = Enumerable.Range(0, count).Select(i => (double)(i % 3)).ToArray();
        var d = Distribution.Of(values);

        foreach (var v in values.Distinct())
            Assert.Equal(
                (count * (100 - d.PercentileOf(v)) / 100) + 0.5,
                d.RankOf(v),
                precision: 9);
    }

    /// <summary>
    /// The floor of 1 is the whole of the defect 14 repair, and only cohorts below ten feel it.
    /// </summary>
    /// <remarks>
    /// <b>This is where the fix is observed.</b> Nothing on TestBed exercises it — the cohorts of
    /// five to nine that the percentile gate stranded all fail blast radius on complexity or on
    /// the fan-in multiple instead, so <c>Math.Max</c> could be deleted and the fixture would
    /// stay green. Recorded in <c>FixtureCoverageTests</c> as a plant still owed.
    /// </remarks>
    [Theory]
    [InlineData(5, 1.0)]     // percentile form gives 0.75 — no rank can satisfy it
    [InlineData(9, 1.0)]     // 0.95, the last stranded size
    [InlineData(10, 1.0)]    // 1.0 exactly: the first size that never needed the floor
    [InlineData(28, 1.9)]
    [InlineData(100, 5.5)]
    public void The_top_rank_limit_never_drops_below_one(int count, double expected)
    {
        var d = Distribution.Of(Enumerable.Range(1, count).Select(i => (double)i));

        Assert.Equal(expected, d.TopRankLimit(0.05), precision: 9);
    }

    /// <summary>
    /// A cohort of nine can now nominate its maximum, and a tie for that maximum still cannot.
    /// </summary>
    /// <remarks>
    /// The defect stated as behaviour rather than as arithmetic. The second half matters as much
    /// as the first: the repair was not meant to make the gate generous, only reachable, and "the
    /// top" of a small group is one type or it is nobody.
    /// </remarks>
    [Fact]
    public void A_cohort_of_nine_can_reach_the_top_rank_but_a_tie_for_it_cannot()
    {
        var unique = Distribution.Of([1, 1, 1, 1, 1, 1, 1, 1, 9]);
        Assert.True(unique.RankOf(9) <= unique.TopRankLimit(0.05));
        Assert.True(unique.PercentileOf(9) < 95, "the gate this replaced admitted nobody here");

        var tied = Distribution.Of([1, 1, 1, 1, 1, 1, 1, 9, 9]);
        Assert.False(tied.RankOf(9) <= tied.TopRankLimit(0.05));
    }

    /// <summary>
    /// Ties keep a rank gate from becoming the roll-call a naive "top N" would be.
    /// </summary>
    /// <remarks>
    /// Invariant 2, and the reason <see cref="Reading.Rank"/> is midrank rather than competition
    /// ranking. Forty types tied at the cohort maximum are one fact about the codebase, not forty
    /// findings — and under <c>1 + strictly-greater</c> all forty would rank 1 and clear any top
    /// fraction. This is defect 3's eight normalizers arriving by a different door.
    /// </remarks>
    [Fact]
    public void A_mass_tie_at_the_maximum_is_not_the_top_of_anything()
    {
        var d = Distribution.Of(
            Enumerable.Repeat(1.0, 60).Concat(Enumerable.Repeat(50.0, 40)).ToArray());

        Assert.Equal(20.5, d.RankOf(50));
        Assert.True(d.RankOf(50) > d.TopRankLimit(0.05));
    }
}

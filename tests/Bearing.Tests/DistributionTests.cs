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
}

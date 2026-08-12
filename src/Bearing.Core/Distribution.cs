namespace IronMarten.Bearing;

/// <summary>
/// One value's standing within its peer group: where it sits, and how far from typical.
/// </summary>
/// <remarks>
/// <para>
/// Both halves are needed because each is blind where the other sees. A percentile ranks but
/// does not scale — in a cohort where every member is unremarkable, the tallest is still the
/// 95th percentile. A multiple of the median scales but does not rank, and collapses when the
/// median is zero. Every finding that reads as credible on a real codebase tests both.
/// </para>
/// </remarks>
public readonly record struct Reading
{
    internal Reading(double percentile, double timesMedian)
    {
        Percentile = percentile;
        TimesMedian = timesMedian;
    }

    /// <summary>
    /// Midrank percentile: strictly-below plus half the ties, in 0..100.
    /// </summary>
    public double Percentile { get; }

    /// <summary>
    /// The value as a multiple of the peer median.
    /// </summary>
    /// <remarks>
    /// <see cref="double.PositiveInfinity"/> when the peer median is zero and the value is not.
    /// That is a real statement — "its peers all measure zero" — and it is deliberately not
    /// collapsed to a large number here. A renderer that prints it as <c>999x</c> turns "the
    /// ratio is undefined" into what reads as a measurement, and one that sorts well.
    /// </remarks>
    public double TimesMedian { get; }
}

/// <summary>
/// A set of measurements over a peer group, and the reading of any one value against it.
/// </summary>
/// <remarks>
/// <para>
/// This is the substrate of every comparative claim the tool makes, which is why it is the
/// first computation to live in Core. In the probe it sits inside <c>Report.cs</c> — the
/// renderer — so the test fixture has to call the print layer by hand or every cohort reading
/// comes back zero. See <c>docs/ARCHITECTURE.md</c> §3.
/// </para>
/// <para>
/// <b>A distribution of fewer than two values has no readings.</b> <see cref="Read"/> returns
/// <see langword="null"/> rather than a number, and that is the model enforcing invariant 6
/// — *blank, never fake* — rather than each renderer remembering to. A cohort of one has a
/// median equal to its only member, so every ratio is 1.0 and midrank puts the single element
/// tying with itself at exactly 50: the most extreme outlier in a codebase would read as
/// perfectly average. The probe computes those numbers and relies on the CSV writer to blank
/// them, which means every other renderer — JSON, HTML, graph tooltips — emits them.
/// </para>
/// </remarks>
public sealed class Distribution
{
    private readonly double[] _sorted;

    private Distribution(double[] sorted, double median)
    {
        _sorted = sorted;
        Median = median;
    }

    /// <summary>How many measurements the group contains.</summary>
    public int Count => _sorted.Length;

    /// <summary>
    /// The median. Zero for an empty distribution, which is a convention rather than a
    /// measurement — <see cref="Read"/> returns nothing there, so nothing depends on it.
    /// </summary>
    public double Median { get; }

    /// <summary>
    /// Whether readings against this distribution mean anything, i.e. whether it holds at
    /// least two values.
    /// </summary>
    public bool IsComparable => _sorted.Length >= 2;

    /// <summary>Builds a distribution from a set of measurements.</summary>
    public static Distribution Of(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var sorted = values.ToArray();
        Array.Sort(sorted);
        return new Distribution(sorted, MedianOf(sorted));
    }

    /// <summary>
    /// Reads one value against this group, or <see langword="null"/> when the group is too
    /// small for the reading to mean anything.
    /// </summary>
    /// <remarks>
    /// The value need not be a member of the distribution — a global percentile reads a type
    /// against the whole solution, and a drift comparison reads today's value against an
    /// archived group.
    /// </remarks>
    public Reading? Read(double value) =>
        IsComparable ? new Reading(PercentileOf(value), TimesMedianOf(value)) : null;

    /// <summary>
    /// Midrank percentile: strictly-below plus half the ties.
    /// </summary>
    /// <remarks>
    /// Counting "at or below" instead puts every member of a fully-tied group at the 100th
    /// percentile — so eight normalizers with exactly one caller each read as top-percentile
    /// outliers and the alert fires on all of them. Ties are the normal case in a real peer
    /// group, and an alert that fires on the unremarkable majority is the one developers mute.
    /// <para>
    /// The consequence is worth knowing before gating on this: a unique maximum tops out at
    /// <c>(n-0.5)/n·100</c>, so a threshold of 95 is unsatisfiable for any group smaller than
    /// ten. See <c>docs/DEFECTS.md</c> §14 — a percentile floor can be unreachable by
    /// arithmetic rather than by tuning.
    /// </para>
    /// </remarks>
    public double PercentileOf(double value)
    {
        if (_sorted.Length == 0) return 0;

        var below = 0;
        var equal = 0;
        foreach (var x in _sorted)
        {
            if (x < value) below++;
            else if (x == value) equal++;
        }

        return 100.0 * (below + (0.5 * equal)) / _sorted.Length;
    }

    /// <summary>The value as a multiple of the median. See <see cref="Reading.TimesMedian"/>.</summary>
    public double TimesMedianOf(double value) =>
        Median <= 0
            ? (value > 0 ? double.PositiveInfinity : 1)
            : value / Median;

    private static double MedianOf(double[] sorted)
    {
        if (sorted.Length == 0) return 0;

        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}

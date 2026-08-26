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
    internal Reading(double percentile, double timesMedian, double rank)
    {
        Percentile = percentile;
        TimesMedian = timesMedian;
        Rank = rank;
    }

    /// <summary>
    /// Midrank percentile: strictly-below plus half the ties, in 0..100.
    /// </summary>
    public double Percentile { get; }

    /// <summary>
    /// Midrank position counting from the top: 1 for a unique maximum, and fractional when
    /// values tie.
    /// </summary>
    /// <remarks>
    /// <b>The same statistic as <see cref="Percentile"/>, expressed from the other end</b> —
    /// <c>rank = count·(100 − percentile)/100 + 0.5</c> exactly, for every tie configuration.
    /// It exists because a threshold on it can be made reachable and a threshold on the
    /// percentile cannot: see <see cref="Distribution.TopRankLimit"/>, which is
    /// satisfiable at every group size.
    /// <para>
    /// Fractional ranks are the tie behaviour, not a rounding artefact, and they are what stops
    /// a rank gate degenerating into a roll-call. Forty types tied at the cohort maximum sit at
    /// rank 20.5, not rank 1 — so "top 5%" excludes them, which is the same protection midrank
    /// percentile gives and the reason defect 3's eight normalizers are not eight findings.
    /// </para>
    /// </remarks>
    public double Rank { get; }

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
    private double? _mad;

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

    /// <summary>
    /// Median absolute deviation — the median of each value's distance from
    /// <see cref="Median"/>. Zero means the group has no spread at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The variable a cohort-relative claim should be gated on, and the reason is that size is
    /// not.</b> A multiple of the median asks <i>how many times its peers is it</i>, which on a
    /// cohort whose median is 1 evaluates to the value itself — 59% of nopCommerce's method-like
    /// members sit at <c>cc</c> 0 or 1, so <c>3x median</c> is <c>cc &gt;= 3</c> and the claim is
    /// absolute wearing relative words. This asks <i>is the gap larger than the gaps already in
    /// this group</i>, which is the question the sentence makes.
    /// </para>
    /// <para>
    /// <b>Zero is a real answer and not a degenerate one, so callers must branch on it.</b> At
    /// <c>MAD = 0</c> the scale estimate collapses and <c>median + k·MAD</c> is the median, so a
    /// naive gate admits everything above it — measured at 1.5–1.8x what ships on the three
    /// reference solutions, and 5.6–18.5x at type level. That is <c>ARCHITECTURE.md</c> §11's
    /// recorded trap and it is real. What defuses it is not a substitute constant but a volume
    /// gate beside this one: a rank limit bounds a group with no spread to its top few, and the
    /// claim there degrades from a multiple to a count — <i>"the only complexity among its N
    /// peers"</i>. Dispersion decides whether a gap is meaningful; rank decides how many may say
    /// so. Neither does the other's job.
    /// </para>
    /// <para>
    /// Zero for a distribution of fewer than two values, on the same convention as
    /// <see cref="Median"/>: <see cref="Read"/> answers nothing there.
    /// </para>
    /// </remarks>
    public double MedianAbsoluteDeviation
    {
        get
        {
            if (_mad is { } cached) return cached;

            var deviations = new double[_sorted.Length];
            for (var i = 0; i < _sorted.Length; i++) deviations[i] = Math.Abs(_sorted[i] - Median);
            Array.Sort(deviations);

            _mad = MedianOf(deviations);
            return _mad.Value;
        }
    }

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
        IsComparable
            ? new Reading(PercentileOf(value), TimesMedianOf(value), RankOf(value))
            : null;

    /// <summary>
    /// Midrank position from the top: strictly-above, plus half the ties, plus a half.
    /// </summary>
    /// <remarks>
    /// The half at the end is what makes a unique maximum rank 1 rather than 0, and it is the
    /// same offset midrank percentile carries at the other end. See <see cref="Reading.Rank"/>
    /// for the identity between the two.
    /// </remarks>
    public double RankOf(double value)
    {
        if (_sorted.Length == 0) return 0;

        var above = 0;
        var equal = 0;
        foreach (var x in _sorted)
        {
            if (x > value) above++;
            else if (x == value) equal++;
        }

        return above + (0.5 * equal) + 0.5;
    }

    /// <summary>
    /// The largest <see cref="Reading.Rank"/> still inside the top <paramref name="fraction"/> of
    /// this group — <b>never smaller than 1</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>fraction·n + 0.5</c> is not an approximation of a percentile threshold, it is one.
    /// Substituting <c>rank = n − n·pctl/100 + 0.5</c> into <c>pctl ≥ p</c> gives
    /// <c>rank ≤ n(100−p)/100 + 0.5</c> identically, for every value and every tie
    /// configuration. So at <c>fraction = 0.05</c> this admits exactly what
    /// <c>FanInPctl &gt;= 95</c> admits — and the probe's goldens cannot move because of it.
    /// </para>
    /// <para>
    /// <b><see cref="Math.Max(double,double)"/> against 1 is the whole of the defect 14 fix.</b>
    /// Below <c>n = 10</c> the percentile form yields a limit under 1, which no rank can satisfy
    /// — the gate is unreachable by arithmetic rather than by tuning, whatever the cohort looks
    /// like. Flooring it at 1 admits the cohort maximum and nothing else, and a two-way tie for
    /// the maximum ranks 1.5 and is still correctly refused: "the top" of a small group is one
    /// type or it is nobody.
    /// </para>
    /// </remarks>
    public double TopRankLimit(double fraction) => Math.Max(1, (fraction * _sorted.Length) + 0.5);

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
    /// ten: a percentile floor can be unreachable by
    /// arithmetic rather than by tuning. <b>Gate on <see cref="TopRankLimit"/> instead</b>, which
    /// says the same thing in a form where the floor is visible and can be raised to 1.
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

    /// <summary>
    /// The share of the group at or above a value, as a percentage — the statistic a
    /// <i>"top N%"</i> sentence is making a claim about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Distinct from <see cref="PercentileOf"/> on purpose, and the two answer different
    /// questions.</b> Midrank is a <i>position</i>: it splits the ties around a value, which is
    /// what makes it the right thing to sort and gate on. This is a <i>population</i>: how many of
    /// the group are at least this extreme. A reader told <i>"top 8%"</i> takes it as one in
    /// twelve, and the claim it is attached to is one of six.
    /// </para>
    /// <para>
    /// <b>The gap is midrank counting half of the value's own tie band as below it.</b> A unique
    /// maximum of six has five below and one equal, so midrank reports
    /// <c>(5 + 0.5)/6 = 92nd</c> percentile and the sentence prints the remaining 8%. It is the
    /// midpoint of that member's own band rather than its top edge, which is correct for ordering
    /// and false as a share. Measured on the three reference solutions: 60, 60 and 136 type-level
    /// findings print a percentage that moves under this, the median printed share going 13% → 16%,
    /// 13% → 17% and 9% → 12%.
    /// </para>
    /// <para>
    /// <b>It also degrades honestly where midrank flattered.</b> A cohort of three yields 33% and a
    /// cohort of two yields 50%, so a thin peer group reports its own thinness and no threshold has
    /// to decide when a percentage stops being worth printing — <c>ARCHITECTURE.md</c> §11's
    /// argument against gating on cohort size, arriving in the sentence layer. And where
    /// <see cref="PercentileOf"/>'s remarks worry about eight tied normalizers reading as
    /// top-percentile, this reports them as <b>100%</b>, which is what they are.
    /// </para>
    /// </remarks>
    public double TopShareOf(double value)
    {
        if (_sorted.Length == 0) return 0;

        var atOrAbove = 0;
        foreach (var x in _sorted)
            if (x >= value)
                atOrAbove++;

        return 100.0 * atOrAbove / _sorted.Length;
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

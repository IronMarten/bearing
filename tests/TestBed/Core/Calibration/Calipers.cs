// PLANT: P7's near-miss band, part three — the two gates on BUG BLAST RADIUS.
//
// --blast-fan-in-multiple and --blast-complexity-percentile both reported `-` in each direction.
// The fixture's one blast nomination clears both by a mile — ShipmentLedger is 11x its cohort's
// fan-in median — so neither constant had a reachable other branch and the sweep could not tell a
// live gate from a dead one.
//
// SpanCaliper sits exactly on both:
//
//   FanIn                >= --min-fan-in (5)                    5      ok
//   FanInXMedian         >= --blast-fan-in-multiple (2.0)       2.0    ON THE GATE
//   CyclomaticPctl       >= --blast-complexity-percentile (70)  70.0   ON THE GATE
//   FanInRank            <= top --blast-top-fraction (0.05)     1      ok, and unique
//
// At defaults it is one ordinary nomination. At --blast-fan-in-multiple 2.1 it disappears, and at
// --blast-complexity-percentile 71 it disappears, each for its own reason.
//
// WHY TEN AND NOT FIVE, WHICH WOULD HAVE BEEN CHEAPER. The two gates want different cohort shapes
// and ten is the smallest size that gives both:
//
//   * a fan-in ratio of exactly 2.0 with fan-in at the floor of 5 needs a median of 2.5, so the
//     cohort has to be EVEN — the median is the average of the middle pair.
//   * a midrank percentile of exactly 70.0 needs (below + ties/2) / n = 0.7 with `below` an
//     integer and `ties` at least 1, which is only satisfiable when n is a multiple of 5.
//
// Ten is the first number that is both. At n = 10 the percentile lands on 70.0 with six types
// strictly below Span and exactly one tie beside it, which is KerfCaliper.
//
// THE TWO PROFILES, both read off a run rather than counted by eye:
//
//   fan-in      0,0,2,2,2,3,3,3,4,5      median 2.5   Span 5      5 / 2.5 = 2.0
//   cyclomatic  1,1,1,1,1,1,4,4,5,6      Span 4       six below, one tie -> 70.0
//
// THE REFERENCE GRAPH IS A DAG AND HAS TO BE. Every edge runs from a later name to an earlier one
// — Land and Mark reference eight each, Kerf five, Jaw two, Height one, and nothing points back.
// A mutual reference anywhere in here closes an SCC, and a second type tangle is P8's plant to
// place, not this one's: the fixture has exactly one today and three assertions rest on that.
//
// WHAT THIS PLANT MUST NOT DO. No inbound edge to anything outside this namespace — Bridges.cs
// carries the rule. Nothing here is named *Gauge, *Meter or *Sonde: those cohorts have medians
// other plants were built against, and this one needs its own.

namespace TestBed.Core.Calibration;

/// <summary>
/// The near miss, and the only type here with a finding. Five callers against a cohort fan-in
/// median of 2.5, and a complexity that sits on the seventieth percentile of its ten peers.
/// </summary>
public class SpanCaliper
{
    public int Measure(int reading, bool metric)
    {
        var value = reading;

        if (metric) value *= 10;
        if (value > 500) value = 500;

        return value;
    }

    public int Zero() => 0;
}

// The nine that set the two medians. Six of them carry a single trivial method, which is what puts
// six values strictly below Span's cyclomatic of 4; Kerf ties with it at 4; Land and Mark sit
// above, and they are also the two that hold the fan-in median at 2.5 by taking none themselves.

/// <summary>Fan-in 4 — the one place below Span, so its rank stays unique.</summary>
public class BoreCaliper
{
    public int Read() => 1;
}

/// <summary>Fan-in 3.</summary>
public class EdgeCaliper
{
    public int Read() => 1;
}

/// <summary>Fan-in 3.</summary>
public class FaceCaliper
{
    public int Read() => 1;
}

/// <summary>Fan-in 3.</summary>
public class GapCaliper
{
    public int Read() => 1;
}

/// <summary>Fan-in 2.</summary>
public class HeightCaliper
{
    private readonly SpanCaliper _span = new();

    public int Read() => _span.Zero();
}

/// <summary>Fan-in 2.</summary>
public class JawCaliper
{
    private readonly SpanCaliper _span = new();
    private readonly BoreCaliper _bore = new();

    public int Read() => _span.Zero() + _bore.Read();
}

/// <summary>Fan-in 2, and the tie that puts Span's percentile on 70.0 rather than above it.</summary>
public class KerfCaliper
{
    private readonly SpanCaliper _span = new();
    private readonly BoreCaliper _bore = new();
    private readonly EdgeCaliper _edge = new();
    private readonly FaceCaliper _face = new();
    private readonly GapCaliper _gap = new();

    public int Read(int seed, bool wide)
    {
        var value = seed + _span.Zero() + _bore.Read();

        if (wide) value += _edge.Read();
        if (value > 10) value = 10;
        if (value < 0) value = 0;

        return value + _face.Read() + _gap.Read();
    }
}

/// <summary>Fan-in 0, fan-out 8 — one of the two that hold the fan-in median at 2.5.</summary>
public class LandCaliper
{
    private readonly SpanCaliper _span = new();
    private readonly BoreCaliper _bore = new();
    private readonly EdgeCaliper _edge = new();
    private readonly FaceCaliper _face = new();
    private readonly GapCaliper _gap = new();
    private readonly HeightCaliper _height = new();
    private readonly JawCaliper _jaw = new();
    private readonly KerfCaliper _kerf = new();

    public int Sweep(int seed, bool wide, bool deep)
    {
        var value = seed + _span.Zero() + _bore.Read() + _edge.Read();

        if (wide) value += _face.Read();
        if (deep) value += _gap.Read();
        if (value > 20) value = 20;
        if (value < 0) value = 0;

        return value + _height.Read() + _jaw.Read() + _kerf.Read(seed, wide);
    }
}

/// <summary>Fan-in 0, fan-out 8 — the second, and the most complex of the ten.</summary>
public class MarkCaliper
{
    private readonly SpanCaliper _span = new();
    private readonly BoreCaliper _bore = new();
    private readonly EdgeCaliper _edge = new();
    private readonly FaceCaliper _face = new();
    private readonly GapCaliper _gap = new();
    private readonly HeightCaliper _height = new();
    private readonly JawCaliper _jaw = new();
    private readonly KerfCaliper _kerf = new();

    public int Sweep(int seed, bool wide, bool deep, bool inverted)
    {
        var value = seed + _span.Zero() + _bore.Read() + _edge.Read();

        if (wide) value += _face.Read();
        if (deep) value += _gap.Read();
        if (inverted) value = -value;
        if (value > 20) value = 20;
        if (value < 0) value = 0;

        return value + _height.Read() + _jaw.Read() + _kerf.Read(seed, wide);
    }
}

// PLANT: P7's near-miss band, part two — the two ratio gates on CONCEALED DECISION.
//
// Every plant before P7 answered "does this finding fire?". This family answers "what is the
// number at which it stops firing?", which is the question the sweep asks and could not get an
// answer to: --outlier-factor and --concealed-fan-in-ceiling both reported `-` in each direction,
// because nothing in the fixture sat near either of them.
//
// DriftSonde is built to sit EXACTLY on both, and the other four exist to put the medians where
// they have to be for that to be true:
//
//   MaxMemberCyclomatic         >= --min-decision-cc (5)     6      ok
//   MaxMemberCyclomaticXMedian  >= --outlier-factor (3.0)    3.0    ON THE GATE
//   FanInXMedian                <= --concealed-fan-in (2.0)  2.0    ON THE GATE
//   FanOutXMedian               <= --concealed-fan-out (2.0) 0      ok
//   CohortSize                  >= --min-cohort (5)          5      ok
//
// So at defaults this is one ordinary nomination. At --outlier-factor 3.1 it disappears, and at
// --concealed-fan-in-ceiling 1.9 it disappears — each for its own reason, which is what makes both
// constants observable. Neither had a reachable other branch in this fixture before.
//
// Six and not seven: the first draft returned through a ternary, which is a decision point, and
// the ratio came out 3.5 rather than 3.0. Every number in this file was read off a run of the tool
// rather than counted by eye, which is the only way a plant that sits ON a gate can be trusted.
//
// THE ARITHMETIC, because it is the whole plant and it is not self-evident from the code:
//
//   max-member cc   Drift 6, Wake 3, Echo 2, Pulse 2, Trace 1   -> median 2, and 6 / 2 = 3.0
//   fan-in          Drift 2, Echo 1, Pulse 1, Trace 0, Wake 0   -> median 1, and 2 / 1 = 2.0
//
// Drift's two inbound edges are Echo and Pulse; Trace and Wake feed those two so the fan-in median
// lands on 1 rather than 0. Drift itself references nothing: giving it an outbound edge to any
// sibling closes a loop through the same five types, and the fixture's cycle assertions are P8's
// to move rather than this plant's.
//
// WHY A NEW FAMILY RATHER THAN A NUDGE. Both gates are ratios against a cohort median, so a near
// miss needs a cohort whose median is a chosen number — 2 for the complexity ratio to land on 3.0
// with integer cyclomatic, 1 for the fan-in ratio to land on 2.0. No existing cohort has that
// shape, and moving one to get it would change the medians every other plant in that cohort was
// built against. An isolated family is the cheaper half of that trade.
//
// WHAT THIS PLANT MUST NOT DO. No inbound edge to anything outside this namespace — Bridges.cs
// carries the rule and the reason. The names end in Sonde because nothing else in the fixture does,
// so the suffix cohort is exactly these five: naming them *Gauge or *Meter would have joined a
// planted cohort and moved the medians three other assertions rest on.

namespace TestBed.Core.Calibration;

/// <summary>
/// The near miss. Six decision points against a cohort median of two, and two callers against a
/// cohort median of one — both ratios land exactly on their gate.
/// </summary>
public class DriftSonde
{
    public int Correct(int reading, string band, bool inverted)
    {
        var value = reading;

        if (band == "low") value -= 1;
        else if (band == "high") value += 1;

        if (inverted) value = -value;
        if (value > 100) value = 100;
        if (value < -100) value = -100;

        return value;
    }
}

// The four that set the medians. Their complexity is chosen so the cohort's max-member median is
// 2 — 1, 2, 2 and 3 around Drift's 6 — and their references are chosen so the fan-in median is 1.

/// <summary>Feeds Drift, and takes its own caller from Trace.</summary>
public class EchoSonde
{
    private readonly DriftSonde _drift = new();

    public int Sample(int reading, bool doubled)
    {
        var value = _drift.Correct(reading, "low", false);
        return doubled ? value * 2 : value;
    }
}

/// <summary>Feeds Drift, and takes its own caller from Wake.</summary>
public class PulseSonde
{
    private readonly DriftSonde _drift = new();

    public int Sample(int reading, bool doubled)
    {
        var value = _drift.Correct(reading, "high", false);
        return doubled ? value * 2 : value;
    }
}

/// <summary>The thin one, and the reason the complexity median is 2 rather than 2.5.</summary>
public class TraceSonde
{
    private readonly EchoSonde _echo = new();

    public int Sample(int reading) => _echo.Sample(reading, false);
}

/// <summary>The second-most complex, which is what keeps Drift's ratio at 3.0 and not higher.</summary>
public class WakeSonde
{
    private readonly PulseSonde _pulse = new();

    public int Sample(int reading, string band)
    {
        var value = _pulse.Sample(reading, false);

        if (band == "low") value -= 1;
        if (band == "high") value += 1;

        return value;
    }
}

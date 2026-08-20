// PLANT: suppression matrix row 6 — the "plumbing" wording branch of CONCEALED DECISION.
//
// This row is not a suppression of a finding, it is a suppression of a CLAIM inside one. The
// nomination fires either way; what changes is the sentence (Report.cs, PrintNominations):
//
//     t.FanIn < opt.MinFanIn
//         ? "looks like plumbing but is in the top "
//         : "connectivity is unremarkable for its peers, but it is in the top "
//
// The filter that selects concealed decisions tests connectivity RELATIVE to peers
// (FanInXMedian <= 2.0). In a cohort where everything is heavily used, "ordinary for its peers"
// still means widely depended on, and calling that plumbing is an overclaim a developer will
// rightly challenge — issue #17, invariant 7. So the absolute floor decides the wording.
//
// Every concealed decision in the fixture had fan-in 0 or 1, so the second branch had never
// executed. Four nominations, four "looks like plumbing", and the branch could have been deleted
// with the goldens staying byte-identical.
//
// ThroughputGauge is the first type to take the other branch:
//
//   MaxMemberCyclomatic >= --min-decision-cc (5)      8    ok
//   MaxMemberCyclomaticXMedian >= --outlier (3.0)     8.0  ok
//   FanInXMedian <= 2.0                               1.0  ok   (5 against a cohort median of 5)
//   FanOutXMedian <= 2.0                              1.0  ok
//   CohortSize >= --min-cohort (5)                    5    ok
//   FanIn >= --min-fan-in (5)                         5    THE WORDING
//
// Fan-in 5 sits exactly on the floor, which is where a threshold should be tested: the branch is
// `<`, so 5 takes the non-plumbing sentence and 4 would not. SuppressionTests re-renders with
// MinFanIn = 6 and asserts the same type reverts to "looks like plumbing" — same metrics, one
// threshold, both branches proven live. RateReconciler is the standing contrast at fan-in 1.
//
// WHY cc 9 AND NOT MORE — AND WHY NOT 8, WHICH IS WHAT IT WAS. LOAD-BEARING AND INTRICATE fires
// at instability <= 0.2, fan-in >= --min-fan-in and a method at or above --high-cc (10).
// ThroughputGauge already satisfies the first two — fan-in 5 against fan-out 1 is instability
// 0.167 — so cc 10 would nominate it twice and this plant would stop being about one thing.
//
// P7 moved it from 8 to 9, which is the near-miss band: everything about this type qualifies for
// LOAD-BEARING except one point of cyclomatic complexity. At the default it is still one finding,
// so row 6 is untouched; at --high-cc 9 it becomes two, which is what makes the constant
// observable. Before this, moving --high-cc a notch either way changed nothing in the entire
// fixture and the sweep reported `-` in both directions — a gate the suite could not see.
// docs/TESTING.md §6, and it is the first of P7's band.
//
// WHY FIVE METERS. Fan-in counts distinct referencing types, so a fan-in of 5 needs five of them
// and there is no cheaper construction. They are new rather than borrowed because every existing
// cohort's fan-out median is load-bearing for a plant already in the fixture — the Reconcilers
// hold theirs at 2 deliberately, and the Ledgers at 1.
//
// Cohorts: suffix:Gauge (5) and suffix:Meter (5), both at the --min-cohort floor.

namespace TestBed.Core;

/// <summary>
/// The row 6 target. Widely depended on in absolute terms and unremarkable against its peers, so
/// the nomination has to describe it without calling it plumbing.
/// </summary>
public class ThroughputGauge
{
    private readonly NormalizeStep _normalize = new();

    public int Sample(int reading, string window, bool smoothed)
    {
        var value = reading;
        if (window == "1m") value += 1;
        else if (window == "5m") value += 2;
        else if (window == "1h") value += 3;
        else value += 4;

        if (smoothed) value /= 2;
        if (value > 1000) value = 1000;
        if (value < 0) value = 0;
        if (_normalize.Apply(value) > 500) value += 1;

        // P7's near miss. This line is the ninth decision point and its only job is to sit one
        // below --high-cc, so lowering that constant by a notch nominates this type as
        // LOAD-BEARING AND INTRICATE and the sweep can see the gate at all.
        if (value % 2 == 1) value -= 1;

        return value;
    }
}

// The four thin gauges. Max-member cc 1 holds the cohort's complexity median at 1 so
// ThroughputGauge reaches 9x it; fan-out 1 apiece holds the fan-out median at 1. All five carry
// the same fan-in, which is what makes ThroughputGauge ordinary for its peers and extreme in
// absolute terms at the same time — the exact shape the wording branch exists for.

public class LatencyGauge
{
    private readonly NormalizeStep _normalize = new();

    public int Sample(int reading) => reading + 1;
}

public class DepthGauge
{
    private readonly NormalizeStep _normalize = new();

    public int Sample(int reading) => reading + 2;
}

public class ErrorGauge
{
    private readonly NormalizeStep _normalize = new();

    public int Sample(int reading) => reading + 3;
}

public class QueueGauge
{
    private readonly NormalizeStep _normalize = new();

    public int Sample(int reading) => reading + 4;
}

// The five meters. Each reads every gauge, which is what gives all five gauges a fan-in of
// exactly 5 and holds their fan-in median there too.

public class IngressMeter
{
    private readonly ThroughputGauge _throughput = new();
    private readonly LatencyGauge _latency = new();
    private readonly DepthGauge _depth = new();
    private readonly ErrorGauge _error = new();
    private readonly QueueGauge _queue = new();

    public int Read(int reading) =>
        _throughput.Sample(reading, "1m", false) + _latency.Sample(reading)
        + _depth.Sample(reading) + _error.Sample(reading) + _queue.Sample(reading);
}

public class EgressMeter
{
    private readonly ThroughputGauge _throughput = new();
    private readonly LatencyGauge _latency = new();
    private readonly DepthGauge _depth = new();
    private readonly ErrorGauge _error = new();
    private readonly QueueGauge _queue = new();

    public int Read(int reading) =>
        _throughput.Sample(reading, "5m", false) + _latency.Sample(reading)
        + _depth.Sample(reading) + _error.Sample(reading) + _queue.Sample(reading);
}

public class BacklogMeter
{
    private readonly ThroughputGauge _throughput = new();
    private readonly LatencyGauge _latency = new();
    private readonly DepthGauge _depth = new();
    private readonly ErrorGauge _error = new();
    private readonly QueueGauge _queue = new();

    public int Read(int reading) =>
        _throughput.Sample(reading, "1h", false) + _latency.Sample(reading)
        + _depth.Sample(reading) + _error.Sample(reading) + _queue.Sample(reading);
}

public class RetryMeter
{
    private readonly ThroughputGauge _throughput = new();
    private readonly LatencyGauge _latency = new();
    private readonly DepthGauge _depth = new();
    private readonly ErrorGauge _error = new();
    private readonly QueueGauge _queue = new();

    public int Read(int reading) =>
        _throughput.Sample(reading, "1m", true) + _latency.Sample(reading)
        + _depth.Sample(reading) + _error.Sample(reading) + _queue.Sample(reading);
}

public class SaturationMeter
{
    private readonly ThroughputGauge _throughput = new();
    private readonly LatencyGauge _latency = new();
    private readonly DepthGauge _depth = new();
    private readonly ErrorGauge _error = new();
    private readonly QueueGauge _queue = new();

    public int Read(int reading) =>
        _throughput.Sample(reading, "5m", true) + _latency.Sample(reading)
        + _depth.Sample(reading) + _error.Sample(reading) + _queue.Sample(reading);
}

/// <summary>
/// Gives every meter one inbound reference, for the same reason StorageCatalog does: a cohort
/// where every member has fan-in 0 reads as five dead types rather than as a peer group.
/// </summary>
public class GaugeCatalog
{
    private readonly IngressMeter _ingress = new();
    private readonly EgressMeter _egress = new();
    private readonly BacklogMeter _backlog = new();
    private readonly RetryMeter _retry = new();
    private readonly SaturationMeter _saturation = new();

    public int ReadAll(int reading) =>
        _ingress.Read(reading) + _egress.Read(reading) + _backlog.Read(reading)
        + _retry.Read(reading) + _saturation.Read(reading);
}

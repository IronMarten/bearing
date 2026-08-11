// PLANT: the BREAKS ALONE case, plus one companion for each of its three suppressions and one
// for the suppression the matrix now requires and the probe does not yet apply.
//
// Breaks alone is the reassuring message — "if it breaks, it breaks alone" — and therefore the
// finding with the most ways to be dangerously wrong. It carries three of Job B's seven
// suppression rules, and removing any of them turned empty output into empty output, so nothing
// failed. That is what this plant fixes.
//
// It fires when (Report.cs, PrintNominations):
//
//   Kind not in (ApiBoundary, ExternalCall, Contract)   suppression 1, invariant 4
//   not already nominated as a concealed decision       suppression 2, invariant 3
//   FanIn >= 1                                          suppression 3
//   Instability >= 0.8
//   MaxMemberCyclomatic >= --high-cc (10)
//
// TWO THINGS THAT ARE EASY TO GET WRONG HERE, both learned by getting them wrong:
//
// 1. Instability is computed from FanOutEffective, NOT FanOut, and effective fan-out excludes
//    dependencies on Contract-kind types. A reconciler that depends on five DTOs has an
//    effective fan-out of zero and an instability of zero. Hence the *Step family below: they
//    carry behaviour, so they are Internal rather than Contract and they count.
//
// 2. The concealed-decision companion has to satisfy breaks alone AND be nominated as
//    concealed, and concealed requires FanOutXMedian <= 2.0 — which is computed from RAW
//    fan-out. With FanIn 1, instability >= 0.8 forces effective fan-out >= 4, so the cohort's
//    fan-out median must be at least 2. Every pre-existing cohort has a fan-out median of 1, so
//    the pair is unsatisfiable anywhere else in the fixture. The five thin siblings exist to
//    hold this cohort's medians at fan-out 2, fan-in 1, max-member-cc 3.
//
// Cohort: suffix:Reconciler, nine members.
//
//   TariffReconciler   FIRES. fan-out 5 -> FanOutXMedian 2.5, so not concealed
//   RateReconciler     suppressed by concealed decision. fan-out 4 -> exactly 2.0
//   AuditReconciler    suppressed by FanIn 0 — unreferenced code is a different finding
//   MethodReconciler   SHOULD be suppressed and is not. See KnownDefectTests
//   five thin siblings max-member-cc 3, below --high-cc, so they never qualify
//
// ReconciliationController is the boundary companion and sits in the ControllerBase cohort.

namespace TestBed.Core;

// The step family. Behaviour, not data, so Kind is Internal and they count toward effective
// fan-out. Named *Step so they form their own cohort of six rather than diluting
// ns:TestBed.Core — and a cohort of six can never produce a blast-radius finding anyway, since
// FanInPctl tops out at 91.67 there.

public class NormalizeStep
{
    public int Apply(int amount) => amount + 1;
}

public class ValidateStep
{
    public int Apply(int amount) => amount + 2;
}

public class EnrichStep
{
    public int Apply(int amount) => amount + 3;
}

public class PriceStep
{
    public int Apply(int amount) => amount + 4;
}

public class RouteStep
{
    public int Apply(int amount) => amount + 5;
}

public class AuditStep
{
    public int Apply(int amount) => amount + 6;
}

/// <summary>
/// The BREAKS ALONE target. Isolated, complex, and genuinely safe to say so about.
/// </summary>
public class TariffReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();
    private readonly RouteStep _route = new();

    public int Reconcile(int amount, string channel, bool expedited)
    {
        var total = amount;
        if (channel == "air") total += 1;
        else if (channel == "ocean") total += 2;
        else if (channel == "ground") total += 3;
        else if (channel == "rail") total += 4;
        else if (channel == "parcel") total += 5;
        else total += 6;

        if (expedited) total += 10;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

/// <summary>
/// Suppression 2. Qualifies for breaks alone on every structural count, and is nominated as a
/// concealed decision — so saying both about it would contradict itself.
/// </summary>
public class RateReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();

    public int Reconcile(int amount, string channel, bool expedited, bool international)
    {
        var total = amount;
        if (channel == "air") total += 1;
        else if (channel == "ocean") total += 2;
        else if (channel == "ground") total += 3;
        else if (channel == "rail") total += 4;
        else if (channel == "parcel") total += 5;
        else if (channel == "freight") total += 6;
        else total += 7;

        if (expedited) total += 10;
        if (international) total += 20;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

/// <summary>
/// Suppression 3. Nothing references it, and fan-in of zero is unreferenced code — a different
/// finding — rather than reassurance.
/// </summary>
public class AuditReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();
    private readonly RouteStep _route = new();

    public int Reconcile(int amount, string channel, bool expedited)
    {
        var total = amount;
        if (channel == "air") total += 1;
        else if (channel == "ocean") total += 2;
        else if (channel == "ground") total += 3;
        else if (channel == "rail") total += 4;
        else if (channel == "parcel") total += 5;
        else total += 6;

        if (expedited) total += 10;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

/// <summary>
/// The case the amended suppression matrix covers and the probe does not: its Reconcile method
/// is nominated as a concealed decision at METHOD level, while the type is not nominated at type
/// level — so a type-level-only suppression misses it and breaks alone fires anyway.
/// </summary>
public class MethodReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();
    private readonly RouteStep _route = new();
    private readonly AuditStep _audit = new();

    public int Reconcile(int amount, string channel, bool expedited, bool international)
    {
        var total = amount;
        if (channel == "air") total += 1;
        else if (channel == "ocean") total += 2;
        else if (channel == "ground") total += 3;
        else if (channel == "rail") total += 4;
        else if (channel == "parcel") total += 5;
        else if (channel == "freight") total += 6;
        else total += 7;

        if (expedited) total += 10;
        if (international) total += 20;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

// The five thin siblings. Fan-out 2 and max-member-cc 3 apiece, which is what holds the cohort
// medians where the companions above need them. Below --high-cc, so none of them qualifies.

public class QuoteReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Reconcile(int amount)
    {
        if (amount < 0) return 0;
        if (_normalize.Apply(amount) > 500) return amount;
        return amount + 1;
    }
}

public class TransitReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Reconcile(int amount)
    {
        if (amount < 0) return 0;
        if (_normalize.Apply(amount) > 500) return amount;
        return amount + 2;
    }
}

public class SurchargeReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Reconcile(int amount)
    {
        if (amount < 0) return 0;
        if (_normalize.Apply(amount) > 500) return amount;
        return amount + 3;
    }
}

public class DocumentReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Reconcile(int amount)
    {
        if (amount < 0) return 0;
        if (_normalize.Apply(amount) > 500) return amount;
        return amount + 4;
    }
}

public class TrackingReconciler
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Reconcile(int amount)
    {
        if (amount < 0) return 0;
        if (_normalize.Apply(amount) > 500) return amount;
        return amount + 5;
    }
}

/// <summary>
/// Suppression 1, invariant 4. Structurally identical to TariffReconciler, but it is an
/// ApiBoundary — the probe cannot see external consumers, so "safe to change" is the one claim
/// it must not make here. Lives in the ControllerBase cohort, deliberately.
/// </summary>
public class ReconciliationController : ControllerBase
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();
    private readonly RouteStep _route = new();

    public int Reconcile(int amount, string channel, bool expedited)
    {
        var total = amount;
        if (channel == "air") total += 1;
        else if (channel == "ocean") total += 2;
        else if (channel == "ground") total += 3;
        else if (channel == "rail") total += 4;
        else if (channel == "parcel") total += 5;
        else total += 6;

        if (expedited) total += 10;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

/// <summary>
/// Gives every reconciler except AuditReconciler exactly one inbound reference, which holds the
/// cohort's fan-in median at 1 and leaves AuditReconciler at zero on purpose.
/// </summary>
public class ReconciliationCatalog
{
    private readonly TariffReconciler _tariff = new();
    private readonly RateReconciler _rate = new();
    private readonly MethodReconciler _method = new();
    private readonly QuoteReconciler _quote = new();
    private readonly TransitReconciler _transit = new();
    private readonly SurchargeReconciler _surcharge = new();
    private readonly DocumentReconciler _document = new();
    private readonly TrackingReconciler _tracking = new();
    private readonly ReconciliationController _controller = new();

    public int ReconcileAll(int amount)
    {
        return _tariff.Reconcile(amount, "air", false)
             + _rate.Reconcile(amount, "air", false, false)
             + _method.Reconcile(amount, "air", false, false)
             + _quote.Reconcile(amount)
             + _transit.Reconcile(amount)
             + _surcharge.Reconcile(amount)
             + _document.Reconcile(amount)
             + _tracking.Reconcile(amount)
             + _controller.Reconcile(amount, "air", false);
    }
}

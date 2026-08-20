namespace TestBed.Core.Projections;

// P3 — the dependency-inversion exclusion, made observable.
//
// WHAT THIS PLANT IS FOR. LoadBearing reads EffectiveFanOut rather than raw FanOut, so a type that
// depends only on abstractions counts as depending on nothing: SESSION-NOTES.md #22. Until this
// file, neither load-bearing nominee in the fixture depended on an abstraction at all — for both
// ShipmentLedger and TariffCalculator, EffectiveFanOut equals FanOut — so the exclusion subtracted
// nothing anywhere and could have been deleted with the suite green.
//
// HOW THE EXCLUSION IS FORCED TO DECIDE. SettlementProjection depends on four interfaces and
// nothing concrete, and five rosters depend on it:
//
//     effective   0 / (5 + 0) = 0.00   <= StableThreshold, so it is nominated
//     raw         4 / (5 + 4) = 0.44   >  StableThreshold, so it would not be
//
// Read either way it is the same type with the same callers; what differs is whether four
// interface dependencies count against it. Delete the exclusion and this nomination disappears,
// which is the whole of what P3 was owed.
//
// It also reaches a sentence nothing else does. The finding carries both fan-outs because the
// report distinguishes "depends on nothing" from "depends on nothing concrete", and until now
// every nominee was the first kind.
//
// WHY THE ROSTERS ARE PROPERTY BAGS. They exist to supply fan-in and nothing else. With no
// executable members they classify as Contract, which keeps them out of every finding that needs
// complexity, and their own cohort of five sits exactly on MinCohort so none of them is peerless.
// They carry no methods, so they cannot become a second zero-median concealed decision — P9 owns
// that case and its test asserts there is exactly one.
//
// NO EXISTING TYPE GAINS FAN-IN: the four interfaces, the projection and the five rosters are all
// new and reference only each other.
//
// IT IS NOT INERT ELSEWHERE, AND THAT CLAIM WAS WRONG WHEN FIRST WRITTEN HERE. Measured after it
// landed, SettlementProjection is also a bug blast radius — five callers against a cohort median
// of one, and complex — and its Settle is a method-level concealed decision against a cohort whose
// median is zero. Both are true of the shape rather than accidents of it: a type five things
// depend on, carrying the only complexity among its peers, is what those two findings are for.
//
// The sweep says the side effects are worth having. PolicySweepTests moved two constants from
// one-directional to `moves` both ways: HighCc, because Settle sits exactly on it at cc 10, and
// HubMin, because min(fan-in 5, fan-out 4) is 4 and lowering the floor to 4 makes this a hub. A
// near miss on a boundary is P7's technique, and this plant lands on two of them by construction.

/// <summary>Regional pricing rules.</summary>
public interface IPricingFacet
{
    bool Applies(string? region);
}

/// <summary>Whether a posting is blocked.</summary>
public interface IPostingFacet
{
    bool Blocks(decimal amount);
}

/// <summary>Where settlement decisions are recorded.</summary>
public interface IAuditFacet
{
    void Record(string entry);
}

/// <summary>Settlement windows.</summary>
public interface ITimingFacet
{
    bool IsWithinWindow(decimal amount);
}

/// <summary>
/// Settles a consignment. Depended on by every roster, and depends on nothing concrete.
/// </summary>
/// <remarks>
/// The four fields are the plant. Every one is an interface, so the effective fan-out is zero
/// while the raw fan-out is four, and the two readings fall on opposite sides of
/// <c>StableThreshold</c>.
/// </remarks>
public sealed class SettlementProjection
{
    private readonly IPricingFacet _pricing;
    private readonly IPostingFacet _posting;
    private readonly IAuditFacet _audit;
    private readonly ITimingFacet _timing;

    public SettlementProjection(
        IPricingFacet pricing, IPostingFacet posting, IAuditFacet audit, ITimingFacet timing)
    {
        _pricing = pricing;
        _posting = posting;
        _audit = audit;
        _timing = timing;
    }

    /// <summary>Net settlement for a consignment.</summary>
    public decimal Settle(decimal gross, int attempts, string? region, bool audited, bool expedited)
    {
        if (gross <= 0m) return 0m;
        if (attempts > 3 && !audited) return 0m;

        var net = gross;

        if (region is "EU") net -= 10m;
        else if (region is "US") net -= 5m;

        if (expedited) net -= 2m;
        if (_timing.IsWithinWindow(net)) net += 1m;
        if (_pricing.Applies(region)) net -= 3m;
        if (_posting.Blocks(net)) net -= 4m;

        _audit.Record($"settled {net}");

        return net;
    }
}

/// <summary>Daily settlement view.</summary>
public sealed class DailyRoster
{
    public SettlementProjection? Settlement { get; init; }

    public int Day { get; init; }
}

/// <summary>Weekly settlement view.</summary>
public sealed class WeeklyRoster
{
    public SettlementProjection? Settlement { get; init; }

    public int Week { get; init; }
}

/// <summary>Monthly settlement view.</summary>
public sealed class MonthlyRoster
{
    public SettlementProjection? Settlement { get; init; }

    public int Month { get; init; }
}

/// <summary>Quarterly settlement view.</summary>
public sealed class QuarterlyRoster
{
    public SettlementProjection? Settlement { get; init; }

    public int Quarter { get; init; }
}

/// <summary>Annual settlement view.</summary>
public sealed class AnnualRoster
{
    public SettlementProjection? Settlement { get; init; }

    public int Year { get; init; }
}

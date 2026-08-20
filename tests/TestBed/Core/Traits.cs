namespace TestBed.Core.Traits;

// P9 — a cohort whose median complexity is zero.
//
// WHAT THIS PLANT IS FOR. ConcealedDecision ranks its nominations by how far a type's complexity
// exceeds its peer median. Where that median is 0 the ratio is undefined rather than infinite, and
// ConcealedDecision.Ranked sorts those last and among themselves by absolute complexity — because
// ordering on the ratio alone put all ten of nopCommerce's undefined cases at the top of the
// section, tied, ahead of every type whose extremity was actually measured. Until this file, no
// cohort in TestBed had a median of zero, so that rule shipped exercised only by real solutions:
// 10 of nopCommerce's 79 type-level nominations, and 0 of TestBed's. It is also the only case that
// reaches D28's `undefined` rendering.
//
// HOW THE SHAPE IS FORCED. Six types sharing the `Trait` suffix, which nothing else in the fixture
// uses — a suffix already in use would have pulled an existing type into this cohort and shrunk an
// unrelated peer population, which is what naming a plant `*Handler` did once before. Five carry
// properties and no methods, so their MaxMemberCyclomatic is 0; CustomsTrait carries one method, so
// the cohort's median is 0 and its own reading divides by it.
//
// WHY THEY REFERENCE EACH OTHER IN A CHAIN. The concealed-decision gates also require the
// candidate's fan-in and fan-out to be ORDINARY for its peers — at most twice the cohort median —
// and a median of zero there would make those ratios undefined too and refuse the finding for the
// wrong reason. A chain gives every member fan-in and fan-out of 0 or 1, so both medians are 1 and
// CustomsTrait sits at 1x and 0x. A ring would have done the same and would also have been a type
// tangle, which is a finding this plant has no business adding.
//
// AND NOTHING ELSE. No existing type gains fan-in — the plant constraint in TASKS.md — because the
// chain closes over its own six members. CustomsTrait's method is cc 6: over MinDecisionCc, which
// is what makes it a decision at all, and under HighCc, which keeps it out of load-bearing and
// breaks alone. Its fan-in of 1 keeps it out of blast radius and change cost, and min(1, 0) keeps
// it out of hubs.
//
// WHAT THIS PLANT DOES NOT DO. TASKS.md gave P9 a second job — making GlobalComplexityFloor
// observable — and it cannot. That floor gates NoPeerGroup, where a peerless type needs
// MaxMemberCyclomatic > 1 while sitting at or above the 90th percentile of the whole solution. On
// TestBed the smallest complexity reaching that percentile is 11, and lifting cc 1 to it would take
// 940 more property bags: a 179-type fixture becoming 1,119, five sixths of it empty. The board's
// own phrase for the distribution that reaches it — "a solution of property bags" — is exact, and a
// six-member cohort is not one.

/// <summary>Mass, and the next trait in the chain.</summary>
public sealed class WeightTrait
{
    public decimal Kilograms { get; init; }

    public VolumeTrait? Volume { get; init; }
}

/// <summary>Displacement.</summary>
public sealed class VolumeTrait
{
    public decimal CubicMetres { get; init; }

    public FragileTrait? Fragile { get; init; }
}

/// <summary>Handling restrictions that are not hazardous, only breakable.</summary>
public sealed class FragileTrait
{
    public bool RequiresUprightHandling { get; init; }

    public HazmatTrait? Hazmat { get; init; }
}

/// <summary>Dangerous goods classification.</summary>
public sealed class HazmatTrait
{
    public string? UnNumber { get; init; }

    public PerishableTrait? Perishable { get; init; }
}

/// <summary>Shelf life, for anything that spoils.</summary>
public sealed class PerishableTrait
{
    public int ShelfLifeHours { get; init; }

    public CustomsTrait? Customs { get; init; }
}

/// <summary>
/// The one member of the cohort that decides anything.
/// </summary>
/// <remarks>
/// The band assignment is the plant. Its peers have no methods at all, so this is not "more
/// complex than its peers" by any measurable multiple — it is the only complexity in the group,
/// which is a weaker claim than a ratio and is the one the report is required to make here.
/// </remarks>
public sealed class CustomsTrait
{
    public string? TariffCode { get; init; }

    public string? OriginCountry { get; init; }

    /// <summary>Duty band for a consignment, from its declared value and its status.</summary>
    public int DutyBand(decimal declaredValue, bool preferentialOrigin, bool restrictedGoods)
    {
        if (restrictedGoods) return 4;
        if (declaredValue <= 0m) return 0;
        if (preferentialOrigin && declaredValue < 1000m) return 1;
        if (declaredValue < 10000m) return 2;

        return 3;
    }
}

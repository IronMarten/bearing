// PLANT: P8, part one — a nine-type tangle, which is three things the fixture could not do.
//
// The fixture had exactly one type tangle, of eight types, and that single fact left three
// branches of the cycles section unreachable:
//
//   * THE NAME TRUNCATION. StructureSections.TypesPerTangle is 8 and the existing tangle has
//     exactly 8 members, so `names.Count > limit` has always been false and the "6 of 10 shown"
//     arm — the remedy for the probe's bare ellipsis — had never rendered. Nine is
//     the smallest number that fires it.
//   * THE LARGEST-FIRST ORDERING. One tangle sorts identically under every comparator. Nine
//     against the existing eight and Loop.cs's four is an order that a wrong comparator changes.
//   * THE COVERING ARM. Both fixture cycles are the PARTIAL case, where the representative loop
//     visits some members and the section says "5 of the 8; all 8 reach each other". A3 wrote the
//     other arm — a loop that covers every member, printed with no qualifier — and nothing has
//     ever executed it. A ring covers by construction: every member has exactly one way in and
//     one way out, so the loop through it is the whole SCC.
//
// WHY A RING AND NOT A DENSER TANGLE. A ring is the only shape where the covering arm is certain
// rather than incidental. Add one chord and the SCC is the same nine types while the shortest
// representative loop is shorter than nine, which puts this back on the partial arm and silently
// un-plants the third case.
//
// WHAT THIS PLANT MUST NOT DO. It must not touch the existing tangle. The Normalizers reach each
// other through Router and ShipmentCoordinator, and an edge from here into any of them would merge
// the two SCCs into one of seventeen — losing the ordering case, the eight-member tangle three
// assertions pin, and this plant, all at once. Nothing here references anything outside this file.

namespace TestBed.Core.Tangles;

/// <summary>The ring's entry, and no more special than any other member.</summary>
public class AlphaNode
{
    private readonly BetaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Second of nine.</summary>
public class BetaNode
{
    private readonly GammaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Third of nine.</summary>
public class GammaNode
{
    private readonly DeltaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Fourth of nine.</summary>
public class DeltaNode
{
    private readonly EpsilonNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Fifth of nine.</summary>
public class EpsilonNode
{
    private readonly ZetaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Sixth of nine.</summary>
public class ZetaNode
{
    private readonly EtaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Seventh of nine.</summary>
public class EtaNode
{
    private readonly ThetaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Eighth of nine.</summary>
public class ThetaNode
{
    private readonly IotaNode _next = new();

    public int Step(int value) => _next.Step(value) + 1;
}

/// <summary>Ninth of nine, and the edge that closes the ring.</summary>
public class IotaNode
{
    private readonly AlphaNode _next = new();

    public int Step(int value) => value > 0 ? _next.Step(value - 1) : 0;
}

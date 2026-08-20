// PLANT: P8, part two — a four-type tangle, which is what makes --min-tangle observable.
//
// The floor was dead in both directions. Downward there was nothing between 3 and 4 to admit;
// upward the only tangle was eight types, so raising the floor a notch changed nothing. Four is
// the size that sits ON the floor: at --min-tangle 5 this tangle stops being reported, and the
// nine-member ring next door does not, which is the difference between a gate that discriminates
// and a gate nothing has ever tested.
//
// It is a ring for the same reason Ring.cs is: the covering arm of A3's loop sentence is certain
// on a ring and incidental on anything denser. Two covering cases is not redundancy — this one is
// four members and Ring.cs is nine, and the sentence they exercise is the same either way.
//
// WHY NOT THREE, WHICH WOULD HAVE MADE THE FLOOR MOVE DOWNWARD INSTEAD. A three-ring is invisible
// at the default: it would exist in the fixture, be reported by nothing, and appear only when
// somebody lowers a constant. Four is visible in the default output, which means the golden
// carries it and a regression that stops detecting it fails a snapshot rather than a sweep row.
//
// AND A MUTUAL PAIR, WHICH IS THE FLOOR'S OTHER SIDE. Raising --min-tangle past four proves the
// floor excludes; nothing proved it ADMITS, because the fixture had no component below four at
// all — lowering the constant to 2 changed nothing. TwinLatch and BoltCatch reference each other
// and nothing else, so they are invisible at the default and arrive the moment the floor drops.
// That is the judgement the constant exists to make: a mutual pair is not a tangle, and the
// fixture now contains one to not-report.
//
// WHAT THIS PLANT MUST NOT DO. No edge into Ring.cs or into the Normalizers. Any of them merges
// the SCCs and takes both plants with it.

namespace TestBed.Core.Tangles;

/// <summary>First of four.</summary>
public class NorthLink
{
    private readonly EastLink _next = new();

    public int Hop(int value) => _next.Hop(value) + 1;
}

/// <summary>Second of four.</summary>
public class EastLink
{
    private readonly SouthLink _next = new();

    public int Hop(int value) => _next.Hop(value) + 1;
}

/// <summary>Third of four.</summary>
public class SouthLink
{
    private readonly WestLink _next = new();

    public int Hop(int value) => _next.Hop(value) + 1;
}

/// <summary>Fourth of four, and the edge that closes the loop.</summary>
public class WestLink
{
    private readonly NorthLink _next = new();

    public int Hop(int value) => value > 0 ? _next.Hop(value - 1) : 0;
}

/// <summary>Half of the mutual pair the floor exists to exclude.</summary>
public class TwinLatch
{
    private BoltCatch _other;

    public void Bind(BoltCatch other) => _other = other;

    public int Depth() => _other == null ? 0 : 1;
}

/// <summary>The other half. Two types that reference each other are not a tangle.</summary>
public class BoltCatch
{
    private TwinLatch _other;

    public void Bind(TwinLatch other) => _other = other;

    public int Depth() => _other == null ? 0 : 1;
}

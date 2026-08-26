// PLANT (P4): the boundary population that makes WIDEST CONTRACT SURFACE's suppression reachable.
//
// The current gate suppresses the section when the qualifying set exceeds
// half the boundaries, and the qualifying filter is `DataShape >= 1.5 * median` — proportional to
// the same distribution the ceiling is measured against. It lands on the threshold at every
// boundary count and never crosses, so the suppression cannot fire on any solution at any size.
// The decided replacement is an absolute count: suppress when more than MaxNamedSurfaces (5)
// boundaries qualify, because what goes wrong is not a proportion — the section promises to name
// what stands out and instead reads a list, and a count is what bounds a list.
//
// Reachable is not observable. The fixture had ten boundaries and exactly ONE qualifier
// (ShipmentController at 12 against a threshold of 11.25), so the new ceiling was as untestable
// as the old one: nothing could push the qualifying set past five.
//
// THE ARITHMETIC. Five endpoints of surface 1 drag the median down far enough that the existing
// spread clears the bar:
//
//   before  shapes 2,3,4,6,7,8,8,8,8,12          median 7.5  threshold 11.25  qualifying 1
//   after   shapes 1,1,1,1,1,2,3,4,6,7,8,8,8,8,12 median 4    threshold 6      qualifying 7
//
// Seven against a ceiling of five, so the section is suppressed at defaults and prints when
// MaxNamedSurfaces is raised past seven. Both branches are reachable from the fixture, which is
// what the old proportional gate could never offer.
//
// Note which direction this plant works in. It does NOT add wide contract surfaces; it adds
// NARROW ones, and the median does the rest. That is the defect stated as a construction: a
// filter proportional to the median can be driven from below by boilerplate, so a codebase with
// many thin endpoints makes its own broad surfaces "stand out" without any of them changing.
// Five health endpoints are the most ordinary thing in a web solution.
//
// WHAT THIS PLANT MUST NOT DO:
//
//   * No base class. Deriving from ControllerBase would take that cohort from eight to thirteen
//     and its fan-out median from 6.5 to 5, and would take ControllerBase's own fan-in from 8 to
//     13 — close enough to blast radius' gates to need checking rather than asserting. The
//     `name-suffix:Controller` rule classifies these as ApiBoundary on its own, and it leaves the
//     seven existing controllers in base:ControllerBase where precedence puts them. These five
//     form their own suffix:Controller cohort of five.
//   * Nothing depends on them and they depend on nothing, so no fan-in, no fan-out, no kind span,
//     and no instability that any finding reads. Entry points with no callers are what
//     controllers are.
//   * One trivial member each. A type with no executable member is classified Contract
//     ("shape:no executable members"), which would take them out of the boundary count entirely
//     and quietly undo the plant.

namespace TestBed.Core;

/// <summary>Liveness probe. Surface of one, on purpose — see the header.</summary>
public class PingController
{
    public int Get() => 1;
}

/// <inheritdoc cref="PingController"/>
public class HealthController
{
    public int Get() => 2;
}

/// <inheritdoc cref="PingController"/>
public class ReadyController
{
    public int Get() => 3;
}

/// <inheritdoc cref="PingController"/>
public class LiveController
{
    public int Get() => 4;
}

/// <inheritdoc cref="PingController"/>
public class VersionController
{
    public int Get() => 5;
}

// PLANT: the half of CHANGE COST that has never decided anything.
//
// TECHREQ-job-b.md §3.5 gates on `Kind is Contract or ApiBoundary`. All four nominations on this
// fixture are Contract, and the highest fan-in on any boundary is 1 — QuoteController — so the
// `or ApiBoundary` arm could be deleted outright and no output would move. That is not an
// accident of the fixture either: almost nothing in real code references a controller, which is
// exactly why every boundary here sits at fan-in 0 or 1, and why the arm needs a case built
// rather than found.
//
// WHAT MAKES THIS ONE REALISTIC. A callback endpoint is the boundary internal code genuinely does
// depend on: each dispatcher has to name the endpoint it hands the carrier as a return address.
// The dependency runs the unusual way round — inward, from internal components to the edge — and
// that is the whole shape the finding is about. Changing this endpoint's route or payload is a
// distributed edit across five dispatchers, AND has consumers outside the solution that no static
// analysis can see. Invariant 4's sentence is not optional here; it is the point.
//
// THE CONSTRAINT THIS PLANT LIFTS, AND WHY IT WAS NEVER REAL.
//
// Bridges.cs, Dispatch.cs and TASKS.md all forbade a new ApiBoundary or ExternalCall type, each
// giving a different reason and none of them correct:
//
//   * Dispatch.cs and docs/TESTING.md: "row 5's suppression stops being reachable at ten."
//   * TASKS.md X1: "ten makes D12's suppression reachable, disarming its pin."
//
// Opposite claims about the same number. The measurement settles it: WIDEST CONTRACT SURFACE
// can never be suppressed AT ANY BOUNDARY COUNT, because the qualifying filter is proportional to
// the same distribution the ceiling is measured against, and the Take(5) caps it besides. Nine was
// not a cliff edge. Its pin is a synthetic proof over the distributions that MAXIMISE the
// qualifying set, plus one assertion that the fixture has nine boundaries — so going to ten
// updates a literal and disarms nothing. Recorded as decision X1.
//
// Going to ten is in fact a step TOWARD the plant D12's decided fix needs: an absolute ceiling of
// five fires at six qualifying boundaries, which takes twelve. That is P4.
//
// THE ARITHMETIC, and it was checked against the cohort before the code was written:
//
//   DispatchCallbackController  fan-in 5 (the dispatchers), fan-out 0   -> ApiBoundary at the
//                               cc 1, one int of surface                   change-cost floor
//   five *Dispatcher            fan-out 2 -> 3, fan-in unchanged at 0   -> instability stays 1.0
//
// It joins base:ControllerBase, taking that cohort from seven to eight, and NO median moves that
// any gate reads:
//
//   fan-in    {0,0,0,0,0,1,1} median 0  ->  {0,0,0,0,0,1,1,5} median 0    unchanged
//   max cc    {1,1,1,1,5,11,12} median 1 -> {1,1,1,1,1,5,11,12} median 1  unchanged
//   fan-out   {5,5,6,7,7,7,7} median 7  ->  {0,5,5,6,7,7,7,7} median 6.5  crosses nothing
//
// The fan-in median is the one that matters and the one that nearly bit: at median 0.5,
// ReconciliationController's FanInXMedian would fall from infinity to exactly 2.0, clear the
// mapper-shaped ceiling it currently fails, and appear as a brand-new concealed decision. Eight
// members keep the median at zero because the median of eight is the mean of the fourth and
// fifth, and both are still zero. A ninth member would not.
//
// This type is deliberately inert everywhere else: cc 1 keeps it out of load-bearing and out of
// "boundaries carrying real logic"; fan-out 0 keeps min(fan-in, fan-out) at 0 so it is not a hub;
// instability 0 keeps it out of breaks alone; one significant kind keeps it out of SPANS; and one
// int of surface keeps the WIDEST CONTRACT SURFACE median low enough that ShipmentController is
// still the only qualifier.

namespace TestBed.Core.Dispatch;

/// <summary>
/// The return address carriers post dispatch results back to.
/// </summary>
/// <remarks>
/// Depended on from inside the solution, which is unusual for a boundary and is the case
/// CHANGE COST's <c>ApiBoundary</c> arm exists for. Its external consumers — the carriers
/// themselves — are not visible here at all.
/// </remarks>
public class DispatchCallbackController : TestBed.Core.ControllerBase
{
    public int Accept(int code) => code;
}

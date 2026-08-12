// PLANT: suppression matrix row 4 — the roll-call collapse on SPANS ARCHITECTURAL LAYERS.
//
// "Spans layers is suppressed for a signature shared by more than top / 3 types, because that is
// a layering pattern and not N anomalies." Invariant 2, and the same discipline that produced
// the original false-positive report: a list of near-identical findings teaches nothing and
// costs the section its readers.
//
// The branch (Report.cs, PrintLayerSpan):
//
//     if (members.Count > opt.Top / 3)      // 15 / 3 = 5, so six or more
//         ...one summary line with four examples...
//     else
//         ...per-type detail, one block each...
//
// Before this plant the fixture had exactly ONE spanning type, AuthenticationMiddleware, so the
// collapse had never run. Six is the smallest population that fires it. Four came from giving
// the Get controllers a CarrierGateway each (Controllers.cs); PolicyBridge is the sixth, and it
// is deliberately unlike the other five — Internal rather than ApiBoundary, and reaching its
// three kinds through dependencies rather than by being one of them. The grouping key is the
// kind signature alone, so a type that resembles nothing else in the group still lands in it.
//
// WHAT THIS PLANT MUST NOT DO, and the reason it is built from existing types:
//
//   * ~~No new ApiBoundary or ExternalCall types, because row 5's suppression stops being
//     reachable at ten boundaries.~~ WITHDRAWN — the claim was false, and it was stated in three
//     places with two different and mutually contradictory justifications. DEFECTS.md §12 proves
//     row 5 is unreachable AT EVERY boundary count, and KnownDefectTests proves it over arbitrary
//     distributions rather than over this fixture, so nine was never a cliff edge. Callback.cs
//     takes the count to ten and nothing about the suppression moved. Decision X1.
//   * No new fan-in on ShipmentController or AuthenticationMiddleware. Both are nominated as
//     concealed decisions with fan-in 0, and the cohort's fan-in median is 0, so any inbound
//     edge sends FanInXMedian to infinity and drops them out of the finding. PolicyBridge
//     references QuoteController instead, whose nomination there is nothing to lose.
//
// ONE THING WORTH WATCHING. There are exactly three SignificantKinds and --min-kind-span is 3,
// so every spanning type has the same signature and GroupBy can only ever produce one group.
// The grouping is written for a generality that cannot occur at current defaults; it becomes
// real only if the kind taxonomy grows or the floor drops. That is a live question — see the
// edge-kind taxonomy decision — and it is why the control test moves --top rather than trying
// to build a second group, which is not currently possible.

namespace TestBed.Core;

/// <summary>
/// The sixth spanning type, and the one that makes the group a group rather than a family.
/// Internal, so its own kind contributes nothing — it reaches ApiBoundary, DataAccess and
/// ExternalCall entirely through what it depends on.
/// </summary>
public class PolicyBridge
{
    private readonly QuoteController _quotes = new();
    private readonly TenantStore _tenants = new();
    private readonly CarrierGateway _carrier = new();

    // Deliberately signature-free. Naming RawResponse or NormalizedResponse here would add
    // inbound edges to two Contract types whose fan-in is pinned by StructureTests and, through
    // CHANGE COST, by KnownDefectTests — the three fields above are what this plant needs, and
    // anything beyond them is collateral.
    public int Apply(int amount) => amount + 1;
}

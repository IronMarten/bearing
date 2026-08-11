// PLANT: the interaction the suppression matrix does not cover — the cohort gate silently
// disables a suppression, and the report contradicts itself as a result.
//
// Row 7 suppresses cohort-relative findings below --min-cohort, which is right: no peer group,
// no relative claim. But BREAKS ALONE is not cohort-relative — its own heading says "no cohort
// required" — and it reads its row 2 suppression straight out of the concealed-decision list:
//
//     var concealedIds = new HashSet<string>(concealed.Select(t => t.Id), ...);   // <- from `eligible`
//     var breaksAlone = result.Types                                              // <- NOT from `eligible`
//         .Where(t => !concealedIds.Contains(t.Id))
//
// `concealed` is built from the cohort-gated set. `breaksAlone` is not. So a type whose peer
// group is too small is dropped from concealed decision, which removes it from concealedIds,
// which re-enables breaks alone on it. Suppressing one finding switched another one ON.
//
// RoutingDepot is that type. In a cohort of five it would be nominated as a concealed decision
// and breaks alone would stay quiet. In a cohort of three the tool says "if it breaks, it breaks
// alone" about a component it would otherwise have called a business judgement — invariant 3,
// reached by a route the amended row 2 does not cover, because that amendment was about
// method-level versus type-level and this is about eligibility.
//
// Both halves are deliberate, and the arithmetic is tight enough that either is easy to lose:
//
//   breaks alone         Kind Internal, FanIn 1, Instability 0.8, MaxMemberCyclomatic 12 >= 10
//   concealed decision   maxcc 12 >= 5, XMedian 12.0, FanInXMedian 1.0, FanOutXMedian 2.0
//   the gate             CohortSize 3 < 5, so the second list never contains it
//
// Instability is FanOutEffective / (FanIn + FanOutEffective) and effective fan-out excludes
// Contract-kind dependencies. With FanIn 1, reaching 0.8 needs effective fan-out of exactly 4 —
// hence four *Step fields, which are Internal and therefore count. That same 4 has to stay
// within 2.0x the cohort's fan-out median for the concealed-decision half to hold, so the two
// thin siblings carry two Step fields each and pin that median at 2. 4/2 = 2.0, and the test is
// `<= 2.0`, so this sits exactly on the boundary the way RateReconciler does in the Reconciler
// cohort. Moving any sibling's fan-out to 1 drops the median to 2... and to 3 raises it, which
// breaks the concealed half and takes the defect with it.
//
// Pinned in KnownDefectTests. The fix is in Core: suppression has to be a declared relationship
// between findings evaluated before rendering, so that "would have been a concealed decision but
// for its cohort" is still expressible. Deleting that test is the event worth seeing.
//
// Cohort: suffix:Depot, three members, in its own namespace for the reason Vaults.cs explains.

namespace TestBed.Core.Depots;

/// <summary>
/// Nominated as breaking alone precisely because its peer group is too small to nominate it as
/// a concealed decision. The two sentences are about one type, in one report.
/// </summary>
public class RoutingDepot
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();
    private readonly EnrichStep _enrich = new();
    private readonly PriceStep _price = new();

    public int Route(int amount, string lane, bool rush, bool international)
    {
        var total = amount;
        if (lane == "air") total += 1;
        else if (lane == "ocean") total += 2;
        else if (lane == "ground") total += 3;
        else if (lane == "rail") total += 4;
        else if (lane == "parcel") total += 5;
        else total += 6;

        if (rush) total += 10;
        if (international) total += 20;
        if (total > 100) total -= 5;
        if (total > 200) total -= 10;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

// The two thin siblings. Max-member cc 1 holds the complexity median at 1; fan-out 2 apiece
// holds the fan-out median at 2, which is what keeps RoutingDepot's FanOutXMedian at exactly
// the 2.0 boundary the concealed-decision filter allows.

public class LabelDepot
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Label(int amount) => amount + 1;
}

public class ManifestDepot
{
    private readonly NormalizeStep _normalize = new();
    private readonly ValidateStep _validate = new();

    public int Manifest(int amount) => amount + 2;
}

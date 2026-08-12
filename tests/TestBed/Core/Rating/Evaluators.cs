// PLANT: complex code that is not anomalous, which is the case every other complex type in this
// fixture fails to be.
//
// Three gates had nothing to bite on before this cohort existed, and all three had the same
// cause. Every complex type in TestBed is *also* a concealed decision — it concentrates its
// complexity in one method that stands out against its peers — so any rule that fires on
// "complex" was masked by a rule that fires on "concealed decision", and the mask was invisible
// because both remove the same types.
//
//   1. BREAKS ALONE's instability gate. `Instability >= 0.8` is the *isolated* in "complex
//      inside but isolated"; without it the finding claims nothing more than "complex". Every
//      type it held back was a concealed decision, so suppression row 2 removed each one before
//      the difference could show. Deleting the gate changed no output. What it would have said:
//      ShipmentLedger, fan-in 11, already nominated as a bug blast radius AND as load-bearing,
//      would also be told that if it breaks, it breaks alone — invariant 3's exact failure,
//      prevented only by an unrelated row.
//
//   2. Suppression row 3, `breaks-alone-is-unreferenced`. Both types reaching the finding with
//      no callers were taken first — ShipmentController by the boundary row, AuditReconciler by
//      the concealed-decision row — so the row silenced nothing and could be deleted outright.
//
//   3. Defect 15's surviving control. RoutingDepot survives breaks alone only because defect 10
//      is live: its cohort of three strips the concealed-decision nomination that would have
//      suppressed it. So fixing defect 10 would empty the finding and take the control with it.
//
// The cohort is uniformly complex, and that is the whole mechanism. Concealed decision fires on
// `CyclomaticXMedian >= 3.0` against the peer group's *method* population. Six types with two
// substantial methods each put twelve comparable values in that population, so the median sits
// where the members sit and no member is three times it. Type level fails for the same reason.
// This is not a trick to dodge a detector: it is what a genuinely uniform family of rule
// evaluators looks like, and the tool staying quiet about it is correct behaviour.
//
// The arithmetic is tight and each number is load-bearing. Instability is
// FanOutEffective / (FanIn + FanOutEffective):
//
//   LaneEvaluator        fan-in 2, effective fan-out 2  ->  0.5   un-masks gate 1
//   DetentionEvaluator   fan-in 0, effective fan-out 2  ->  1.0   gives row 3 a case of its own
//   SurchargeEvaluator   fan-in 1, effective fan-out 4  ->  0.8   the replacement control
//   FuelEvaluator        fan-in 2, effective fan-out 0  ->  0
//   TransitEvaluator     fan-in 2, effective fan-out 0  ->  0
//   PolicyEvaluator      fan-in 1, effective fan-out 0  ->  0
//
// LaneEvaluator is the one that un-masks: complex, connected, not a boundary, not a concealed
// decision, and referenced — so with the instability gate present it is silent, and without it
// the finding claims it breaks alone. That difference is the gate becoming observable.
//
// SurchargeEvaluator sits exactly on 0.8 the way RoutingDepot does. Moving its fan-out to 3
// drops it to 0.75 and the control disappears; moving any evaluator's reference into an
// interface or a Contract-kind type drops it out of *effective* fan-out and does the same
// silently. Add references here, do not re-route the existing ones.
//
// Its cohort is six, comfortably above --min-cohort, which is the point: RoutingDepot's survival
// depends on a defect and this one does not.
//
// Own namespace, for the reason Vaults.cs explains.

namespace TestBed.Core.Rating;

/// <summary>
/// Base fuel surcharge. Depends on nothing, so it is stable and unremarkable.
/// </summary>
public class FuelEvaluator
{
    public int Assess(int miles, string region, bool peak, bool contracted)
    {
        var rate = 0;
        if (miles > 100) rate += 2;
        if (miles > 500) rate += 4;
        if (miles > 1500) rate += 8;
        if (region == "north") rate += 3;
        else if (region == "south") rate += 2;
        else if (region == "coastal") rate += 5;
        else if (region == "inland") rate += 1;
        if (peak) rate += 6;
        if (contracted) rate -= 4;
        if (peak && contracted) rate -= 2;
        return rate;
    }

    public int Reconcile(int billed, int assessed, bool audited, string terms)
    {
        var delta = billed - assessed;
        if (delta > 50) delta -= 10;
        if (delta > 200) delta -= 25;
        if (delta < -50) delta += 10;
        if (terms == "prepaid") delta += 2;
        else if (terms == "collect") delta += 4;
        else if (terms == "thirdparty") delta += 6;
        if (audited) delta /= 2;
        if (audited && delta > 100) delta -= 5;
        return delta;
    }
}

/// <summary>
/// Transit-time banding. Also a leaf.
/// </summary>
public class TransitEvaluator
{
    public int Band(int days, string service, bool guaranteed, bool weekend)
    {
        var band = 0;
        if (days <= 1) band = 5;
        else if (days <= 3) band = 4;
        else if (days <= 5) band = 3;
        else if (days <= 10) band = 2;
        else band = 1;
        if (service == "express") band += 2;
        if (service == "economy") band -= 1;
        if (guaranteed) band += 3;
        if (weekend) band += 1;
        if (guaranteed && weekend) band += 2;
        return band;
    }

    public int Penalty(int promised, int actual, bool excused, string reason)
    {
        var late = actual - promised;
        if (late <= 0) return 0;
        if (late > 1) late *= 2;
        if (late > 5) late *= 3;
        if (reason == "weather") late -= 2;
        else if (reason == "customs") late -= 1;
        else if (reason == "carrier") late += 4;
        if (excused) late = 0;
        if (!excused && late > 20) late = 20;
        return late;
    }
}

/// <summary>
/// Policy eligibility. A leaf, referenced only by the surcharge evaluator.
/// </summary>
public class PolicyEvaluator
{
    public int Eligibility(int tenureMonths, string tier, bool arrears, bool disputed)
    {
        var score = 0;
        if (tenureMonths > 6) score += 1;
        if (tenureMonths > 24) score += 2;
        if (tenureMonths > 60) score += 3;
        if (tier == "gold") score += 5;
        else if (tier == "silver") score += 3;
        else if (tier == "bronze") score += 1;
        if (arrears) score -= 4;
        if (disputed) score -= 2;
        if (arrears && disputed) score -= 3;
        return score;
    }

    public int Exposure(int openBalance, int creditLimit, bool secured, string history)
    {
        var exposure = openBalance - creditLimit;
        if (exposure > 0) exposure *= 2;
        if (exposure > 5000) exposure += 500;
        if (exposure > 20000) exposure += 2000;
        if (history == "clean") exposure -= 250;
        else if (history == "mixed") exposure += 250;
        else if (history == "poor") exposure += 1000;
        if (secured) exposure /= 2;
        if (secured && exposure < 0) exposure = 0;
        return exposure;
    }
}

/// <summary>
/// Lane pricing. <b>Complex, connected, and nothing anomalous about it</b> — the case that makes
/// breaks-alone's instability gate observable.
/// </summary>
public class LaneEvaluator
{
    private readonly FuelEvaluator _fuel = new();
    private readonly TransitEvaluator _transit = new();

    public int Price(int weight, string lane, bool rush, bool hazmat)
    {
        var price = weight;
        if (weight > 100) price += 10;
        if (weight > 500) price += 40;
        if (weight > 2000) price += 150;
        if (lane == "air") price += 400;
        else if (lane == "ocean") price += 120;
        else if (lane == "rail") price += 80;
        else if (lane == "road") price += 60;
        if (rush) price += 200;
        if (hazmat) price += 350;
        if (rush && hazmat) price += 100;
        return price;
    }

    public int Adjust(int quoted, int miles, int days, bool guaranteed)
    {
        var adjusted = quoted + _fuel.Assess(miles, "inland", false, true);
        adjusted += _transit.Band(days, "economy", guaranteed, false);
        if (miles > 800) adjusted += 25;
        if (miles > 2500) adjusted += 90;
        if (days < 2) adjusted += 60;
        if (days > 14) adjusted -= 30;
        if (guaranteed) adjusted += 45;
        if (quoted > 5000) adjusted -= 100;
        if (guaranteed && quoted > 5000) adjusted += 20;
        return adjusted;
    }
}

/// <summary>
/// Accessorial surcharges. <b>The replacement control for defect 15</b>: nominated at neither
/// concealed-decision level, isolated enough to break alone, and — unlike RoutingDepot — sitting
/// in a peer group large enough that its survival does not depend on defect 10.
/// </summary>
public class SurchargeEvaluator
{
    private readonly LaneEvaluator _lane = new();
    private readonly FuelEvaluator _fuel = new();
    private readonly TransitEvaluator _transit = new();
    private readonly PolicyEvaluator _policy = new();

    public int Apply(int basePrice, string accessorial, bool residential, bool liftgate)
    {
        var total = basePrice;
        if (accessorial == "inside") total += 45;
        else if (accessorial == "appointment") total += 30;
        else if (accessorial == "notify") total += 15;
        else if (accessorial == "redelivery") total += 65;
        if (residential) total += 90;
        if (liftgate) total += 120;
        if (residential && liftgate) total += 25;
        if (basePrice > 2000) total += 50;
        if (basePrice > 10000) total += 200;
        return total;
    }

    public int Quote(int weight, int miles, int days, bool premium)
    {
        var quote = _lane.Price(weight, "road", premium, false);
        quote += _fuel.Assess(miles, "north", premium, false);
        quote += _transit.Band(days, "express", premium, false);
        quote += _policy.Eligibility(12, "silver", false, false);
        if (weight > 1000) quote += 75;
        if (miles > 1200) quote += 110;
        if (days < 3) quote += 95;
        if (premium) quote += 300;
        if (premium && weight > 1000) quote += 40;
        return quote;
    }
}

/// <summary>
/// Detention and demurrage. <b>Unreferenced, and neither a boundary nor a concealed decision</b>
/// — so suppression row 3 is the only rule that reaches it.
/// </summary>
public class DetentionEvaluator
{
    private readonly LaneEvaluator _lane = new();
    private readonly SurchargeEvaluator _surcharge = new();

    public int Detention(int freeHours, int usedHours, bool weekend, string equipment)
    {
        var chargeable = usedHours - freeHours;
        if (chargeable <= 0) return 0;
        if (chargeable > 4) chargeable += 2;
        if (chargeable > 24) chargeable += 12;
        if (equipment == "reefer") chargeable *= 3;
        else if (equipment == "flatbed") chargeable *= 2;
        else if (equipment == "dryvan") chargeable += 1;
        else if (equipment == "tanker") chargeable *= 4;
        if (weekend) chargeable /= 2;
        if (usedHours > 72) chargeable += 6;
        if (weekend && chargeable > 48) chargeable = 48;
        return chargeable;
    }

    public int Demurrage(int weight, int miles, int days, bool premium)
    {
        var owed = _lane.Price(weight, "rail", false, false);
        owed += _surcharge.Apply(owed, "appointment", false, premium);
        if (days > 3) owed += 100;
        if (days > 7) owed += 400;
        if (days > 21) owed += 1500;
        if (miles > 600) owed += 50;
        if (premium) owed -= 75;
        if (weight > 5000) owed += 250;
        if (premium && days > 7) owed -= 50;
        return owed;
    }
}

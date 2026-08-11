// PLANT: the cohort-size gate — suppression matrix row 7, the last of the seven.
//
// "Every cohort-relative finding is suppressed when CohortSize < minCohort, because no peer
// group means no relative claim." Every existing type in a sub-floor cohort fails some OTHER
// condition too, so asserting absence proves nothing: OrderRepository is the closest thing the
// fixture had, and its MaxMemberCyclomaticXMedian is 1.8 against an outlier factor of 3.0. A
// test written on it would pass for the wrong reason, which is the exact failure mode the
// suppression suite exists to prevent.
//
// PricingVault is built so the cohort gate is the ONLY thing standing between it and a
// CONCEALED DECISION nomination (Report.cs, PrintNominations):
//
//   MaxMemberCyclomatic >= --min-decision-cc (5)      8    ok
//   MaxMemberCyclomaticXMedian >= --outlier (3.0)     8.0  ok  (8 against a cohort median of 1)
//   FanInXMedian <= 2.0                               1.0  ok
//   FanOutXMedian <= 2.0                              1.0  ok
//   CohortSize >= --min-cohort (5)                    3    THE GATE
//
// The control in SuppressionTests re-renders with MinCohort = 3 and asserts it appears. That is
// what makes this a test of the gate rather than a test of absence — flip the gate, the finding
// comes back; nothing else moved.
//
// WHY A SEPARATE NAMESPACE. Cohorts.Reconcile re-homes any type stranded below the floor into
// another candidate cohort that COULD reach the floor. Candidates are interface, base type, name
// suffix, architectural kind and namespace — and "Internal" is excluded from the kind candidates
// (SolutionAnalyzer.cs), so for these three the only alternative to suffix:Vault is their
// namespace. In TestBed.Core that is a large cohort and the plant would dissolve on contact.
// TestBed.Core.Vaults holds three, below the floor, so nothing can move and CohortSize stays 3.
// SurchargeTable in TestBed.Core.Pricing is the same arrangement, arrived at by accident.
//
// WHY cc 8 AND NOT MORE. --high-cc is 10, and breaks alone fires at or above it. Keeping
// PricingVault below that leaves this cohort testing one thing. The Depots cohort next door
// crosses the line deliberately — see Depots.cs, which is a defect rather than a requirement.
//
// Cohort: suffix:Vault, three members, fan-in 1 and fan-out 1 apiece so both medians sit at 1.

namespace TestBed.Core.Vaults;

/// <summary>
/// The row 7 target. Satisfies every condition of CONCEALED DECISION and is excluded solely
/// because its peer group is three types rather than five.
/// </summary>
public class PricingVault
{
    private readonly NormalizeStep _normalize = new();

    public int Price(int amount, string tier, bool rush)
    {
        var total = amount;
        if (tier == "gold") total += 1;
        else if (tier == "silver") total += 2;
        else if (tier == "bronze") total += 3;
        else total += 4;

        if (rush) total += 10;
        if (total > 100) total -= 5;
        if (total < 0) total = 0;
        if (_normalize.Apply(total) > 500) total += 1;

        return total;
    }
}

// The two thin siblings. Max-member cc 1 apiece, which is what holds the cohort's complexity
// median at 1 and lets PricingVault reach 8x it. Fan-out 1 each holds that median at 1 too.

public class TokenVault
{
    private readonly NormalizeStep _normalize = new();

    public int Read(int amount) => amount + 1;
}

public class SessionVault
{
    private readonly NormalizeStep _normalize = new();

    public int Read(int amount) => amount + 2;
}

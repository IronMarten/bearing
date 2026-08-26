namespace IronMarten.Bearing;

/// <summary>
/// Types coupled heavily in <b>both</b> directions. <c>TECHREQ-job-b.md</c> §3.8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both magnitudes, because a ratio structurally cannot see this.</b> High fan-in plus high
/// fan-out gives an instability of about 0.5 — the exact middle, indistinguishable from a trivial
/// one-in one-out leaf. <c>SESSION-NOTES.md</c> #14. Every other coupling finding in §3 reads a
/// normalized measure; this is the one that cannot, and taking the minimum of the two counts is
/// what makes it a different finding rather than a rephrasing of load-bearing.
/// </para>
/// <para>
/// <b>The split names two different dangers, and it is not a severity band.</b> A hub carrying
/// real logic is risky to <i>reason about</i>; a hub that is only wiring is risky to
/// <i>re-route</i>. Both are worth knowing and they call for opposite responses, which is why the
/// two arms are carried as separate qualifying facts rather than as one "is a god object" boolean.
/// </para>
/// <para>
/// <b>That separation is the fix.</b> The probe's disjunction has
/// two arms and one sentence — <i>"it both depends on and is depended on by much of the system,
/// AND carries real logic"</i> — printed for either. On the size arm the claim is false by
/// construction, because that arm exists precisely for types with bulk and no logic:
/// <c>DispatchRegistry</c> is told it carries real logic in a sentence whose own receipts say
/// twenty-three members and a worst method of cc 1. Invariant 5 puts interpretation first and math
/// as receipts; there the interpretation contradicts its own receipts. Two qualifiers means a
/// renderer can say what each arm actually means, and cannot say the wrong one by accident.
/// </para>
/// <para>
/// The standing note that routers, mediators and composition roots legitimately live here belongs
/// with the rendered section (§3.8 requires it kept). It is not a suppression and must not become
/// one: those are exactly the components not to change lightly, and the note is what stops the
/// threshold being tuned away instead of the known ones being marked.
/// </para>
/// <para>
/// Cohort-free. Both magnitudes are absolute counts and mean the same thing with or without peers.
/// </para>
/// </remarks>
public static class HubOrGodObject
{
    /// <summary>Nominates types heavily coupled in both directions at once.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(int Coupling, Finding Finding)>();

        foreach (var type in model.Types)
        {
            var coupling = Math.Min(type.FanIn, type.FanOut);
            if (coupling < policy.HubMin) continue;

            found.Add((coupling, new Finding(
                new FindingKey(FindingKind.HubOrGodObject, type.Subject),
                [
                    // Both counts carry the gate: the condition is on the smaller of the two, and
                    // which one that is varies by type. A receipt naming the gate on only the
                    // deciding one would read as though the other were context.
                    Receipt.Gated("FanIn", type.FanIn, nameof(AnalysisPolicy.HubMin)),
                    Receipt.Gated("FanOut", type.FanOut, nameof(AnalysisPolicy.HubMin)),
                    Receipt.Of("Coupling", coupling),
                    Receipt.Of("MemberCount", type.MemberCount),
                    Receipt.Of("MaxMemberCyclomatic", type.MaxMemberCyclomatic),
                    Receipt.Of("Dsm", type.Dsm),
                    // The measure that cannot see this finding, carried so the claim can be
                    // checked against it. #14 is an argument about a number, and the number is
                    // this one.
                    Receipt.Of("Instability", type.Instability ?? double.NaN),
                ],
                [
                    new Qualifier(
                        Qualifiers.CarriesRealLogic,
                        type.MaxMemberCyclomatic >= policy.HighCc,
                        nameof(AnalysisPolicy.HighCc)),
                    new Qualifier(
                        Qualifiers.TooLargeToHold,
                        type.MemberCount >= policy.GodObjectMembers,
                        nameof(AnalysisPolicy.GodObjectMembers)),
                ],
                // Invariant 7. "Carries real logic" is arguable until the method is named — and
                // where it is the size arm that fired, naming the worst method is what shows the
                // reader that there is no such method.
                type.MostComplexMember is { } member ? [member.Subject] : [])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Coupling), f => f.Finding);
    }
}

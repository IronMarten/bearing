namespace IronMarten.Bearing;

/// <summary>
/// The findings layer: <c>model → claims</c>.
/// </summary>
/// <remarks>
/// <para>
/// The second half of <c>docs/ARCHITECTURE.md</c> §5. The walk answers <c>(solution, policy) →
/// model</c>; this answers what the model means, and it is a function of the model alone — the
/// policy travels on it, so the same model produces the same findings every time.
/// </para>
/// <para>
/// <b>Detection and suppression are separate passes, and that is the point.</b> Every detector
/// sees the model and nothing else, so no detector can depend on having run after another one.
/// Relationships between findings — a claim that must not be made about a component another
/// claim was already made about — are resolved afterwards against the whole
/// <see cref="FindingSet"/>. In the probe they are resolved by where the code sits in a
/// 1,066-line method, which means reordering it breaks invariant 3 without failing anything.
/// </para>
/// <para>
/// Ten of §3's findings are wired up — layer span, both concealed-decision nominations, blast
/// radius, change cost, load-bearing, breaks alone, hubs, shared mutable state and both halves of
/// boundary marking — and six of §4's seven rows: breaks alone's three and the contract-surface
/// ceiling as suppressions, and rows 4 and 6 as qualifiers, because each of those silences a
/// <i>sentence</i> rather than a claim. Only coverage is left, and it arrives the same way. Note that blast radius and load-bearing overlap on "widely depended on and
/// complex" and are still two findings, both allowed to fire on one type; that is a
/// <c>PRD-free-tier.md</c> §7.2 decision, not an oversight here.
/// </para>
/// </remarks>
public static class Analysis
{
    /// <summary>
    /// Every detector, in the order their findings are emitted.
    /// </summary>
    /// <remarks>
    /// Method-level concealed decision comes first because it is the primary of the two, not
    /// because anything downstream depends on the order. A renderer decides what order to show
    /// sections in; the set is not that decision.
    /// </remarks>
    private static readonly Func<SolutionModel, IEnumerable<Finding>>[] Detectors =
    [
        SpansArchitecturalLayers.Detect,
        ConcealedDecision.AtMethodLevel,
        ConcealedDecision.AtTypeLevel,
        BlastRadius.Detect,
        ChangeCost.Detect,
        LoadBearing.Detect,
        BreaksAlone.Detect,
        HubOrGodObject.Detect,
        SharedMutableState.Detect,
        BoundaryMarking.CarryingRealLogic,
        BoundaryMarking.WidestSurfaces,
    ];

    /// <summary>The claims this model supports, with the suppression matrix applied.</summary>
    public static FindingSet FindingsFor(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var detected = Detected(model);

        return FindingSet.Of(
            detected.All.Where(finding => Suppression.Silencing(finding, detected, model) is null));
    }

    /// <summary>
    /// Every claim the detectors made, <b>before</b> suppression.
    /// </summary>
    /// <remarks>
    /// Exposed because the two sets have to be comparable. A suppression that stops working
    /// produces more output, which reads as a working tool, so the only way to assert a row is
    /// doing anything is to show a finding present here and absent from
    /// <see cref="FindingsFor"/>. <c>TECHREQ-job-b.md</c> §4: "a suppression that cannot fail is
    /// worse than no suppression."
    /// <para>
    /// It is not what a renderer should read. No renderer may emit a finding in isolation, and
    /// every finding in this set is one that has not yet been checked against the others.
    /// </para>
    /// </remarks>
    public static FindingSet Detected(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return FindingSet.Of(Detectors.SelectMany(detect => detect(model)));
    }
}

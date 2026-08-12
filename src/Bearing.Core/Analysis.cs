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
/// Four of §3's findings are wired up: both concealed-decision nominations, blast radius and
/// load-bearing. The rest arrive as detectors, and §4's suppressions as a pass over the set —
/// neither of which changes this shape. Note that blast radius and load-bearing overlap on
/// "widely depended on and complex" and are still two findings, both allowed to fire on one
/// type; that is a <c>PRD-free-tier.md</c> §7.2 decision, not an oversight here.
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
        ConcealedDecision.AtMethodLevel,
        ConcealedDecision.AtTypeLevel,
        BlastRadius.Detect,
        LoadBearing.Detect,
    ];

    /// <summary>Runs every detector over the model and indexes the result.</summary>
    public static FindingSet FindingsFor(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return FindingSet.Of(Detectors.SelectMany(detect => detect(model)));
    }
}

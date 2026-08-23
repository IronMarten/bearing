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
/// <b>All eleven of §3's findings are wired up</b>, and six of §4's seven rows: breaks alone's
/// three and the contract-surface ceiling as suppressions, and rows 4 and 6 as qualifiers, because
/// each of those silences a <i>sentence</i> rather than a claim. The seventh is row 7, which is
/// not a rule over findings at all — it is the cohort floor inside each cohort-relative detector,
/// and the types it removes are exactly the population <see cref="NoPeerGroup"/> reports. Note that blast radius and load-bearing overlap on "widely depended on and
/// complex" and are still two findings, both allowed to fire on one type; that is a
/// <c>PRD-free-tier.md</c> §7.2 decision, not an oversight here.
/// </para>
/// </remarks>
/// <summary>
/// One claim, and the suppression row that silenced it — or <see langword="null"/> if none did.
/// </summary>
/// <param name="Finding">The claim.</param>
/// <param name="SilencedBy">
/// The first row that applied, or <see langword="null"/> when the finding survives. First rather
/// than every row: <see cref="Suppression.Silencing"/> stops at the first match, and a finding is
/// removed once however many reasons there are to remove it.
/// </param>
public sealed record Judged(Finding Finding, SuppressionRule? SilencedBy)
{
    /// <summary>Whether this claim survived the suppression matrix.</summary>
    public bool IsReported => SilencedBy is null;
}

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
        NoStaticReferences.Detect,
        NoPeerGroup.Detect,
    ];

    /// <summary>The claims this model supports, with the suppression matrix applied.</summary>
    /// <remarks>
    /// A projection of <see cref="Judge"/>, kept because it is what every renderer reads and
    /// because the reported set is the common question. Nothing about its result has changed.
    /// </remarks>
    public static FindingSet FindingsFor(SolutionModel model) =>
        FindingSet.Of(Judge(model).Where(j => j.IsReported).Select(j => j.Finding));

    /// <summary>
    /// Every claim the detectors made, each with the row that silenced it or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule was already computed and then thrown away.</b> <see cref="FindingsFor"/> called
    /// <see cref="Suppression.Silencing"/> for its truth value and discarded which row answered,
    /// which is the one thing that method's own remark says must not happen: <i>"a finding removed
    /// for the wrong reason and a finding removed for the right one are indistinguishable from the
    /// surviving set alone."</i> They were indistinguishable, because nothing downstream could see
    /// the difference.
    /// </para>
    /// <para>
    /// <b>Two things need what was discarded.</b> The report's <c>Mutually dependent, not
    /// reported</c> list says why each component was set aside, and reconstructing that in a
    /// renderer is the rule-in-a-renderer that <c>docs/ARCHITECTURE.md</c> §3 forbids. And a
    /// consumer of the export cannot otherwise tell a finding that was <i>muted</i> from one that
    /// was <i>fixed</i> — which reads as an improvement it did not earn.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Judged> Judge(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var detected = Detected(model);

        return
        [
            .. detected.All.Select(finding =>
                new Judged(finding, Suppression.Silencing(finding, detected, model)))
        ];
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

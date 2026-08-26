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
/// One claim, and whatever stopped it being reported — a suppression row, or the user.
/// </summary>
/// <param name="Finding">The claim.</param>
/// <param name="SilencedBy">
/// The first row that applied, or <see langword="null"/> when the finding survives. First rather
/// than every row: <see cref="Suppression.Silencing"/> stops at the first match, and a finding is
/// removed once however many reasons there are to remove it.
/// </param>
/// <param name="Acknowledged">
/// The user's entry marking this claim <i>known and fine</i>, or <see langword="null"/> if there is
/// none — <see cref="Acknowledgments"/>.
/// </param>
/// <remarks>
/// <para>
/// <b>Two fields and not one, because they are two different facts about a claim.</b> Suppressed
/// means Bearing decided the claim would be wrong. Acknowledged means Bearing stands by it and the
/// user has dismissed it. One "went quiet" field would tell a consumer of the export that the tool
/// withheld something it actually said, which is the class of lie
/// <c>SCHEMA-findings-export.md</c> §1 exists to make impossible.
/// </para>
/// <para>
/// <b>The matrix is asked first, and a claim can carry both.</b> Acknowledging a claim the tool was
/// never going to make is a no-op, so the row answers before the file does; the entry is still
/// recorded, because an acknowledgment that has gone inert is what
/// <see cref="Judgement.Unmatched"/> exists to surface. <see cref="IsSuppressed"/> and
/// <see cref="IsAcknowledged"/> are therefore not exclusive, and only <see cref="IsReported"/> is a
/// renderer's question.
/// </para>
/// </remarks>
public sealed record Judged(
    Finding Finding,
    SuppressionRule? SilencedBy,
    Acknowledgment? Acknowledged = null)
{
    /// <summary>Whether this claim reaches the reader — the only question a renderer asks.</summary>
    public bool IsReported => SilencedBy is null && Acknowledged is null;

    /// <summary>Whether a suppression row decided the claim would be wrong.</summary>
    public bool IsSuppressed => SilencedBy is not null;

    /// <summary>Whether the user marked this claim known and fine.</summary>
    public bool IsAcknowledged => Acknowledged is not null;
}

/// <summary>
/// Everything one run judged: every claim the detectors made, and what stopped each of the ones
/// that went quiet.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what a renderer is handed</b>, and saying so is the answer to the decision
/// <c>docs/ARCHITECTURE.md</c> §11 held open. A renderer takes its <i>population</i> and its
/// <i>reported-or-withheld</i> decision from here, and goes to the <see cref="SolutionModel"/> only
/// for display detail, looked up by subject. The alternative — recovering a withheld population
/// from the model, which is how the circular-references sections drew both their lists — leaves two
/// undeclared routes by which a renderer learns that a claim went quiet, and nothing holding two
/// renderers to the same one.
/// </para>
/// <para>
/// <b>The rule is worth more than the tidiness, because the model cannot answer the question.</b>
/// <c>ShapedCycle.IsReportable</c> is a property of the shape and it re-derives, in a renderer, a
/// decision the suppression matrix already made — which is the rule-in-a-renderer
/// <c>docs/ARCHITECTURE.md</c> §3 forbids, and which agrees with the matrix only for as long as
/// nobody adds a row. Every input to <i>whether this claim is reported</i> that is not a property of
/// the shape is invisible to it.
/// </para>
/// </remarks>
public sealed class Judgement
{
    private readonly Dictionary<string, List<Judged>> _withheldByKind;

    /// <summary>Indexes a run's judgements.</summary>
    /// <remarks>
    /// Public, because this is a projection and not a decision — everything here is derived from
    /// the list, so a caller holding judgements it has altered can ask the same questions of them.
    /// The suite does exactly that: re-judging one silenced finding as reported and re-rendering is
    /// how the export is shown to follow the matrix without a second walk.
    /// </remarks>
    /// <param name="all">Every claim the run made, judged.</param>
    /// <param name="acknowledgments">
    /// The file it was judged against, or <see langword="null"/> for none.
    /// </param>
    public Judgement(IReadOnlyList<Judged> all, Acknowledgments? acknowledgments = null)
    {
        ArgumentNullException.ThrowIfNull(all);

        All = all;
        Acknowledgments = acknowledgments ?? Acknowledgments.None;
        Reported = FindingSet.Of(all.Where(j => j.IsReported).Select(j => j.Finding));
        Withheld = [.. all.Where(j => !j.IsReported)];

        _withheldByKind = Withheld
            .GroupBy(j => j.Finding.Kind.ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var claimed = all.Select(j => j.Finding.Key.Canonical).ToHashSet(StringComparer.Ordinal);
        Unmatched = [.. Acknowledgments.All.Where(a => !claimed.Contains(a.Key))];
    }

    /// <summary>Every claim the detectors made, judged.</summary>
    public IReadOnlyList<Judged> All { get; }

    /// <summary>The claims that reach the reader.</summary>
    public FindingSet Reported { get; }

    /// <summary>The claims that do not.</summary>
    public IReadOnlyList<Judged> Withheld { get; }

    /// <summary>The file this run was judged against.</summary>
    public Acknowledgments Acknowledgments { get; }

    /// <summary>
    /// Entries in that file that matched no claim this run made.
    /// </summary>
    /// <remarks>
    /// <b>Surfaced rather than ignored, because the commonest cause is a rename.</b>
    /// <see cref="FindingKey"/> records the trade: a rename produces a new key, so the
    /// acknowledgment is lost and the finding comes back as new — the right default for drift and
    /// the wrong one here. An entry that has stopped matching is the user's only signal that it
    /// happened, and a file that silently accumulates dead lines stops being reviewable, which is
    /// the one property that made it worth committing.
    /// </remarks>
    public IReadOnlyList<Acknowledgment> Unmatched { get; }

    /// <summary>How many claims the user's file silenced this run.</summary>
    /// <remarks>
    /// Claims a row would have withheld anyway are not counted. What this reports is what the
    /// reader would otherwise have seen.
    /// </remarks>
    public int AcknowledgedCount => All.Count(j => j.IsAcknowledged && !j.IsSuppressed);

    /// <summary>The withheld claims of one kind, in emission order.</summary>
    public IReadOnlyList<Judged> WithheldOfKind(FindingKind kind) =>
        _withheldByKind.TryGetValue(kind.ToString(), out var found) ? found : [];
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
        // TECHREQ-job-b.md §3.12. Last because the enum is, and for the same reason: these are
        // claims that render in their own section rather than in the enumeration above it.
        CircularReferences.AmongNamespaces,
        CircularReferences.AmongProjects,
        CircularReferences.AmongTypes,
    ];

    /// <summary>The claims this model supports, with the suppression matrix applied.</summary>
    /// <remarks>
    /// A projection of <see cref="Judge"/>, kept because it is what every renderer reads and
    /// because the reported set is the common question. Nothing about its result has changed.
    /// </remarks>
    public static FindingSet FindingsFor(SolutionModel model) =>
        Judge(model).Reported;

    /// <summary>
    /// Every claim the detectors made, each with whatever stopped it being reported.
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
    /// <param name="model">The solution to judge.</param>
    /// <param name="acknowledgments">
    /// What the user has already marked known and fine, or <see langword="null"/> for nothing — a
    /// run with no file, which is every first run. A default rather than a required argument
    /// because an unacknowledged run is a whole run and not a degraded one.
    /// </param>
    public static Judgement Judge(SolutionModel model, Acknowledgments? acknowledgments = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var known = acknowledgments ?? Acknowledgments.None;
        var detected = Detected(model);

        return new Judgement(
            [
                .. detected.All.Select(finding =>
                    new Judged(
                        finding,
                        Suppression.Silencing(finding, detected, model),
                        known.For(finding.Key)))
            ],
            known);
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

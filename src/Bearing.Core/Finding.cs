namespace IronMarten.Bearing;

/// <summary>
/// One measurement a finding rests on.
/// </summary>
/// <param name="Name">What was measured — <c>Cyclomatic</c>, <c>FanInXMedian</c>.</param>
/// <param name="Value">The measurement.</param>
/// <param name="Gate">
/// The <see cref="AnalysisPolicy"/> value this had to clear, or <see langword="null"/> when the
/// measurement is context rather than a condition.
/// </param>
/// <remarks>
/// <para>
/// <c>docs/ARCHITECTURE.md</c> §6: the first thing anyone does with a claim that looks wrong is
/// ask why it was made, and a claim whose basis is not available is worthless even when it is
/// correct. Receipts are that basis, carried by the finding rather than reconstructed by a
/// renderer that happens to still have the numbers to hand.
/// </para>
/// <para>
/// <b>The gate is a name, not a value.</b> The threshold itself lives on the policy the model
/// was produced under, so naming it here keeps one source of truth and makes the mapping
/// checkable — every gate a finding cites has to resolve against
/// <see cref="AnalysisPolicy.Values"/>, which is a test rather than a convention.
/// </para>
/// </remarks>
public readonly record struct Receipt(string Name, double Value, string? Gate = null)
{
    /// <summary>A measurement that explains the claim without deciding it.</summary>
    public static Receipt Of(string name, double value) => new(name, value);

    /// <summary>A measurement that had to clear a named policy value for the finding to fire.</summary>
    public static Receipt Gated(string name, double value, string gate) => new(name, value, gate);
}

/// <summary>
/// Something true about the subject that changes what can honestly be said about it, without
/// changing whether the finding fires.
/// </summary>
/// <param name="Name">One of <see cref="Qualifiers"/>.</param>
/// <param name="Holds">Whether it is true of this subject.</param>
/// <param name="Gate">The <see cref="AnalysisPolicy"/> value that decided it.</param>
/// <remarks>
/// <para>
/// The suppression matrix has a row that suppresses a <i>sentence</i> rather than a finding
/// (<c>TECHREQ-job-b.md</c> §4, row 6), and until now there was no model surface carrying the
/// distinction — so the only thing that could be tested was the probe's wording. That is the
/// rule living in the renderer, and <c>docs/ARCHITECTURE.md</c> §3 is explicit that a rule
/// enforced in a renderer is a rule that does not exist.
/// </para>
/// <para>
/// So Core decides whether the qualifying fact holds and Cli decides what words to put it in.
/// Both branches stay reachable, and which one applies is assertable against the model.
/// </para>
/// </remarks>
public readonly record struct Qualifier(string Name, bool Holds, string? Gate = null);

/// <summary>
/// Two of a finding's participants, and how much runs between them.
/// </summary>
/// <param name="From">The end the references run from.</param>
/// <param name="To">The end they run to.</param>
/// <param name="Weight">How many of them there are.</param>
/// <remarks>
/// <para>
/// <b>A receipt measures the subject; a relation measures a pair inside it.</b> Some claims are
/// about a component rather than a component's number — a cycle's evidence is not "size 30", it is
/// <i>which two of the thirty hold each other and by how much</i>, because that is the pair someone
/// breaks first. <see cref="Receipt"/> is <c>(name, value, gate)</c> and
/// <see cref="Finding.Participants"/> is a bare list, so neither can carry it.
/// </para>
/// <para>
/// <b>It consolidates three types rather than adding a fourth.</b> <c>HeldPair</c>,
/// <c>ProjectLink</c> and <c>TanglePair</c> were each written for one cycle kind and each invented
/// this shape again — two of them keying their members on bare strings, which is
/// <c>docs/DEFECTS.md</c> §13 and §39's mistake in a third place. Measured before being proposed:
/// every kind that needed a weighted pair was a cycle kind, and all three needed it.
/// </para>
/// <para>
/// <b>Always directed, with no flag.</b> An unordered pair that holds both ways is two relations,
/// and a renderer that wants one number sums them — which is what <c>HeldPair.Weight</c> already
/// did behind the scenes. Encoding directedness as a boolean would make every consumer branch on
/// it; encoding it in the data makes the two cases the same case, and says more than the report
/// shows today: <i>Common → Orders 5, Orders → Common 4</i> rather than a flat 9.
/// </para>
/// <para>
/// <b>Members are <see cref="SubjectRef"/>, not names.</b> Identity is the whole reason the finding
/// model exists, and a relation whose ends cannot be joined to a type row is evidence a consumer
/// has to re-resolve by string matching.
/// </para>
/// </remarks>
public readonly record struct Relation(SubjectRef From, SubjectRef To, int Weight);

/// <summary>The qualifying facts findings can carry.</summary>
public static class Qualifiers
{
    /// <summary>
    /// Several of this type's data members are unreferenced, so the claim is about the type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The signal that a type is filled by something the walk cannot see, and it is a shape
    /// rather than a name.</b> Attributes were the obvious candidate and were measured to be
    /// backwards: <b>none</b> of Jellyfin's forty nominations carried one, because
    /// <c>System.Text.Json</c> matches on property names, while three of nopCommerce's five did.
    /// A rule keyed on attributes would fire exactly where the problem is smallest.
    /// </para>
    /// <para>
    /// <b>What separates them is concentration.</b> 17 of <c>SearchResult</c>'s 23 data members
    /// have no reader, and 1 of <c>MatroskaConstants</c>' 17 does — the first is a carrier nothing
    /// reads and the second is one unused constant. <b>Stated as "more than one", with no
    /// threshold</b>, because 74%, 42% and 30% are a continuum and any cut through it would be a
    /// number nobody could defend. The rule is "say it once per type", which cannot drift.
    /// </para>
    /// <para>
    /// It changes how a renderer <i>groups</i> and never whether the finding fires: Core still
    /// emits one per member, so <c>--json</c>, <c>--csv</c> and <c>--full</c> keep every one.
    /// </para>
    /// </remarks>
    public const string PartOfAnUnreadGroup = "part-of-an-unread-group";

    /// <summary>
    /// An attribute on the member may direct something outside this solution to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A qualifier and not an exclusion, and the reason is <c>[Obsolete]</c>.</b> Most
    /// attributes on an unreferenced member are something addressing it — a serialisation
    /// callback, a message handler, a test — but not all of them are, and an obsolete private
    /// method nothing calls is exactly as dead as it looks. Excluding on any attribute would hide
    /// it; naming the category is what §5.6 actually asks for.
    /// </para>
    /// <para>
    /// <b>Measured before it was added</b>, which is why it is not an exclusion on volume either:
    /// none of Jellyfin's nominations carried an attribute and three of nopCommerce's five did, so
    /// this is a correctness gap rather than a noise one. The case it closes is a private
    /// <c>[OnDeserialized]</c> callback — non-public, so no other exclusion reaches it.
    /// </para>
    /// </remarks>
    public const string AnAttributeMayDirectIt = "an-attribute-may-direct-it";

    /// <summary>
    /// A test project was skipped, so usage from tests was never visible to this walk.
    /// </summary>
    /// <remarks>
    /// <b>Certain rather than suspected, which is what makes it worth carrying.</b> Test projects
    /// are excluded by default because test code inflates fan-in on exactly the types it covers
    /// best — but the consequence is that a member used only by a test reads as used by nothing.
    /// §5.6 names test-only usage as its own category for this reason, and the fixture plants
    /// <c>FixtureBuilder</c> as the case.
    /// </remarks>
    public const string TestUsageUnobservable = "test-usage-unobservable";

    /// <summary>
    /// Connectivity is low in <b>absolute</b> terms, not merely relative to peers — so the
    /// subject can be described as plumbing.
    /// </summary>
    /// <remarks>
    /// Concealed decision selects on <c>FanInXMedian</c>, which is relative, and in a cohort
    /// where every member is heavily used "ordinary for its peers" still means widely depended
    /// on. Calling that plumbing is an overclaim a developer will rightly challenge, so the
    /// absolute floor decides the claim while the relative measure decides the finding.
    /// <c>SESSION-NOTES.md</c> #17.
    /// </remarks>
    public const string LowAbsoluteConnectivity = "low-absolute-connectivity";

    /// <summary>
    /// There is real logic inside, so the risk is in reasoning about the subject rather than in
    /// re-routing it.
    /// </summary>
    /// <remarks>
    /// One arm of §3.8's disjunction, and half of <c>docs/DEFECTS.md</c> §16's repair. The probe
    /// prints <i>"AND carries real logic"</i> for either arm, which is false by construction on
    /// the other one — a type reaches the size arm precisely by having bulk and no logic.
    /// </remarks>
    public const string CarriesRealLogic = "carries-real-logic";

    /// <summary>
    /// The subject is large enough that no one holds it in their head, whatever is or is not
    /// inside it.
    /// </summary>
    /// <remarks>
    /// §3.8's other arm. It is a different danger from <see cref="CarriesRealLogic"/> rather than
    /// a weaker grade of it, and the two are independent: a type may hold both, either, or — for a
    /// hub — neither, which is what makes it wiring.
    /// </remarks>
    public const string TooLargeToHold = "too-large-to-hold";

    /// <summary>
    /// Enough other components do the same thing, in the same way, that the subject is an instance
    /// of a pattern rather than an anomaly — so its detail may be collapsed into one line.
    /// </summary>
    /// <remarks>
    /// Row 4 of the suppression matrix, carried as a qualifier for the same reason as row 6: it
    /// silences <i>detail</i> and not the claim. The probe keeps every collapsed type named in its
    /// examples list, which is the proof that the finding is not withdrawn — what it loses is the
    /// per-kind breakdown, and §3.1 says that breakdown is the finding. See
    /// <see cref="SpansArchitecturalLayers"/> for what makes two subjects the same instance, which
    /// is <c>docs/DEFECTS.md</c> §11.
    /// </remarks>
    public const string PartOfALayeringPattern = "part-of-a-layering-pattern";

    /// <summary>
    /// The subject has no peer group, but is extreme by fan-in against the <b>whole solution</b> —
    /// so something can still be said, with the comparison named.
    /// </summary>
    /// <remarks>
    /// Weaker evidence than a peer-relative claim, because it compares unlike things, and the
    /// wording has to carry that rather than borrow the confidence of a cohort. The alternative is
    /// silence about a lone <c>DbContext</c> that half the system depends on, which is invariant 8
    /// failing in the one section that exists to prevent it. <c>SESSION-NOTES.md</c> #7.
    /// </remarks>
    public const string GloballyExtremeFanIn = "globally-extreme-fan-in";

    /// <summary>The same, by complexity.</summary>
    /// <remarks>
    /// Gated by a percentile <b>and</b> an absolute floor, and the qualifier names only the first
    /// because it carries one gate — the floor travels as a receipt. Without it, in a codebase
    /// where most types have no branching, a max-member complexity of 1 lands at a high midrank
    /// percentile and the tool says <i>"top 86% by complexity, cc 1"</i>. <c>SESSION-NOTES.md</c>
    /// #8.
    /// </remarks>
    public const string GloballyExtremeComplexity = "globally-extreme-complexity";
}

/// <summary>
/// A claim about one component, with everything it rests on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately smaller than the finding model.</b> <c>TECHREQ-job-a.md</c> §1 defers that
/// until the HTML findings pane says what a finding must carry, and <c>TECHREQ-job-b.md</c> §7
/// records why Job B cannot wait for all of it: suppression is a relationship between findings,
/// so a finding has to be an addressable thing rather than a <c>WriteLine</c> that already
/// happened. This carries identity, the receipts behind the claim, the qualifying facts that
/// decide how it can be worded, and the named participants — and nothing about presentation.
/// </para>
/// <para>
/// Rank and position are absent on purpose. So is any notion of severity: banding it would make
/// identity depend on a threshold, and a retune would then invalidate every stored
/// acknowledgment. See <see cref="FindingKey"/>.
/// </para>
/// </remarks>
public sealed class Finding
{
    /// <summary>Creates a finding.</summary>
    /// <param name="key">What is being claimed, about what.</param>
    /// <param name="receipts">The measurements behind it.</param>
    /// <param name="qualifiers">Facts that decide how it can be worded.</param>
    /// <param name="participants">
    /// The named specifics — invariant 7. <i>"Spans 3 architectural kinds"</i> is arguable;
    /// <i>"why is authentication calling TenantStore?"</i> is not.
    /// </param>
    /// <param name="relations">
    /// Weighted pairs inside <paramref name="participants"/>, heaviest first. Optional because most
    /// claims are about one subject and have none — a cycle is the case that does.
    /// </param>
    public Finding(
        FindingKey key,
        IReadOnlyList<Receipt> receipts,
        IReadOnlyList<Qualifier> qualifiers,
        IReadOnlyList<SubjectRef> participants,
        IReadOnlyList<Relation>? relations = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(qualifiers);
        ArgumentNullException.ThrowIfNull(participants);

        Key = key;
        Receipts = receipts;
        Qualifiers = qualifiers;
        Participants = participants;
        Relations = relations ?? [];
    }

    /// <summary>Identity: what kind of claim, about which subject.</summary>
    public FindingKey Key { get; }

    /// <summary>The claim being made.</summary>
    public FindingKind Kind => Key.Kind;

    /// <summary>What the claim is about.</summary>
    public SubjectRef Subject => Key.Subject;

    /// <summary>Everything the claim rests on.</summary>
    public IReadOnlyList<Receipt> Receipts { get; }

    /// <summary>Facts that decide what can honestly be said, without deciding whether it fires.</summary>
    public IReadOnlyList<Qualifier> Qualifiers { get; }

    /// <summary>The specifics the claim names.</summary>
    public IReadOnlyList<SubjectRef> Participants { get; }

    /// <summary>
    /// Weighted pairs inside <see cref="Participants"/>, heaviest first. Empty for every claim
    /// whose evidence is about the subject rather than about a pair inside it.
    /// </summary>
    public IReadOnlyList<Relation> Relations { get; }

    /// <summary>One receipt's value, or <see langword="null"/> when the finding does not carry it.</summary>
    public double? ValueOf(string receiptName)
    {
        foreach (var receipt in Receipts)
            if (string.Equals(receipt.Name, receiptName, StringComparison.Ordinal))
                return receipt.Value;

        return null;
    }

    /// <summary>
    /// Whether a qualifying fact holds. A qualifier the finding does not carry does not hold —
    /// a renderer asking about one is asking whether it may make a stronger claim, and the
    /// answer when nothing established it is no.
    /// </summary>
    public bool Holds(string qualifierName)
    {
        foreach (var qualifier in Qualifiers)
            if (string.Equals(qualifier.Name, qualifierName, StringComparison.Ordinal))
                return qualifier.Holds;

        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => Key.Canonical;
}

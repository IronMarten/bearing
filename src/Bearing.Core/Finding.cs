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

/// <summary>The qualifying facts findings can carry.</summary>
public static class Qualifiers
{
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
    public Finding(
        FindingKey key,
        IReadOnlyList<Receipt> receipts,
        IReadOnlyList<Qualifier> qualifiers,
        IReadOnlyList<SubjectRef> participants)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(qualifiers);
        ArgumentNullException.ThrowIfNull(participants);

        Key = key;
        Receipts = receipts;
        Qualifiers = qualifiers;
        Participants = participants;
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

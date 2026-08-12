namespace IronMarten.Bearing;

/// <summary>
/// The findings this tool can make. Sourced from <c>TECHREQ-job-b.md</c> §3 — this enum is a
/// transcription of that section, not a design, and it stays in step with it.
/// </summary>
public enum FindingKind
{
    /// <summary>§3.1. A component doing work that belongs to more than one architectural layer.</summary>
    SpansArchitecturalLayers,

    /// <summary>§3.2. A type that decides something, described as if it did not.</summary>
    ConcealedDecisionType,

    /// <summary>§3.3. The same, nominated at method level. Method-level analysis is primary.</summary>
    ConcealedDecisionMethod,

    /// <summary>§3.4. A change here reaches an unusual amount of the system.</summary>
    BugBlastRadius,

    /// <summary>§3.5. Expensive to change relative to its peers.</summary>
    ChangeCost,

    /// <summary>§3.6. Heavily depended on and internally intricate.</summary>
    LoadBearingAndIntricate,

    /// <summary>§3.7. Structurally isolated — the claim §4 suppresses hardest, because it is the
    /// one the tool must not get wrong at a boundary.</summary>
    BreaksAlone,

    /// <summary>§3.8. Hubs and god objects.</summary>
    HubOrGodObject,

    /// <summary>§3.9. Shared mutable state.</summary>
    SharedMutableState,

    /// <summary>
    /// §3.10. A boundary that carries real logic — decisions at an external edge are the hardest
    /// kind to change later.
    /// </summary>
    BoundaryCarriesLogic,

    /// <summary>§3.10. A boundary with an unusually wide contract surface.</summary>
    /// <remarks>
    /// <b>§3.10 is one section and two claims, so it is two kinds.</b> A finding is identified by
    /// <c>(kind, subject)</c> and nothing else, and one boundary can be both — on this fixture
    /// <c>ShipmentController</c> is, at cc 12 and a surface of 12. One kind would make those a
    /// duplicate key, which <see cref="FindingSet"/> rejects rather than merges, and merging is
    /// what would lose one of the two claims. The section's third part, the contact-point count,
    /// is not a claim about any subject and is computed by the renderer from the model.
    /// </remarks>
    WidestContractSurface,

    /// <summary>§3.11. What the run could not see. Part of the output, not a footnote.</summary>
    Coverage,
}

/// <summary>
/// The identity of a finding: <b>what kind of claim, about which subject</b>. Nothing else.
///
/// <para>
/// This is deliberately smaller than the finding model. <c>TECHREQ-job-a.md</c> §1 deferred that
/// model until the HTML findings pane says what a finding must carry, and that reasoning still
/// holds for the full record. But three documents independently need identity before then —
/// <c>TECHREQ-job-b.md</c> §7, <c>CARRY-FORWARD.md</c> §2, and the retention argument in the
/// prior-art spike — so the identity key is settled now and the record stays deferred.
/// </para>
///
/// <para><b>Two things depend on it, and they want different properties.</b></para>
///
/// <para>
/// <i>Suppression</i> (<c>TECHREQ-job-b.md</c> §4) needs equality <i>within</i> one run. Today
/// suppression works by capturing nominations earlier in the same method and testing membership
/// later, which makes renderer ordering load-bearing: reorder the renderer and invariant 3
/// breaks silently. With a key, suppression becomes a declared relationship between findings,
/// evaluated before anything renders.
/// </para>
///
/// <para>
/// <i>Acknowledgment memory</i> needs equality <i>across</i> runs. "Known and fine" has to attach
/// to something that is still the same thing next run, and a re-run is only informative if a
/// finding can be <i>new</i>. Without it, run N+1 restates what the user already dismissed,
/// which is the alert fatigue invariant 2 exists to prevent, arriving by the back door.
/// </para>
///
/// <para><b>What is deliberately excluded, and why that is the whole point.</b></para>
///
/// <para>
/// File, line, metric values, threshold values, rank, and position under <c>--top</c> are all
/// out. Every one of them moves when nothing meaningful changed — a type shifts down its file,
/// a threshold is retuned, a bigger offender appears above it — and any of them in the key would
/// discard an acknowledgment for a reason the user would not recognise as a reason.
/// </para>
///
/// <para>
/// Magnitude is excluded too, and that is a real trade rather than an oversight: acknowledge a
/// god object and it stays acknowledged if it doubles in size. The alternative — banding
/// severity into the key — makes the key depend on a threshold, so a retune would invalidate
/// every stored acknowledgment, and a subject sitting on a band edge would re-alert on every
/// run. Escalation is better handled by storing the metrics beside the acknowledgment and
/// deciding later; that decision does not have to be made now, and this key does not foreclose
/// it.
/// </para>
///
/// <para>
/// <b>A rename produces a new key.</b> The acknowledgment is lost and the finding returns as
/// new. That is the right default for drift, which should surface renames as events, and a
/// slightly wrong one for acknowledgment memory. It is the price of not building rename
/// detection now, and it is recorded rather than hidden.
/// </para>
/// </summary>
public sealed class FindingKey : IEquatable<FindingKey>
{
    private const string Separator = "|";

    /// <summary>Creates a key for a claim of <paramref name="kind"/> about <paramref name="subject"/>.</summary>
    public FindingKey(FindingKind kind, SubjectRef subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        Kind = kind;
        Subject = subject;
        Canonical = string.Concat(kind.ToString(), Separator, subject.Canonical);
    }

    /// <summary>The claim being made.</summary>
    public FindingKind Kind { get; }

    /// <summary>What the claim is about.</summary>
    public SubjectRef Subject { get; }

    /// <summary>
    /// The stable, round-trippable identity — the form acknowledgment memory persists.
    /// </summary>
    /// <remarks>
    /// The kind is rendered by name rather than by its numeric value. Enum values renumber when
    /// a member is inserted, and a stored acknowledgment that silently changes meaning across a
    /// tool upgrade is worse than one that fails to match.
    /// </remarks>
    public string Canonical { get; }

    /// <inheritdoc/>
    public bool Equals(FindingKey? other) =>
        other is not null && string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FindingKey);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Canonical);

    /// <inheritdoc/>
    public override string ToString() => Canonical;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(FindingKey? left, FindingKey? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(FindingKey? left, FindingKey? right) => !(left == right);
}

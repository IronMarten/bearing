namespace IronMarten.Bearing;

/// <summary>What kind of thing a finding is about.</summary>
public enum SubjectKind
{
    /// <summary>A single type.</summary>
    TypeDeclaration,

    /// <summary>A single member of a type. Method-level analysis is primary, not a detail.</summary>
    Member,

    /// <summary>A project.</summary>
    Project,

    /// <summary>A namespace.</summary>
    Namespace,

    /// <summary>An unordered set — a cycle is about its members jointly, not about any one.</summary>
    Set,

    /// <summary>The analysed solution itself. Coverage findings have no narrower subject.</summary>
    Solution,
}

/// <summary>
/// A stable reference to whatever a finding is about.
///
/// <para>
/// This is the <c>subject</c> half of <see cref="FindingKey"/>, and it exists as its own type
/// because the subject is not always a type. <c>TECHREQ-job-b.md</c> §3.2 and §3.3 split
/// concealed decision across type level and method level; §3.11 reports coverage about the
/// solution; cycles are about a set of projects or namespaces jointly.
/// </para>
///
/// <para>
/// <b>Types are keyed by <c>(assembly, fully-qualified name)</c>, never by name alone.</b>
/// .NET permits the same FQN in two assemblies and plugin architectures use it deliberately.
/// Keying on the name merges the two declarations into one row with their metrics summed,
/// which on nopCommerce fabricated a five-project circular reference — a shipping finding,
/// computed on conflated numbers. This is the fix for that defect, and it is the one place
/// extraction is permitted to change behaviour (<c>TECHREQ-job-b.md</c> §8, criterion 8).
/// </para>
///
/// <para>
/// Equality is ordinal string equality over <see cref="Canonical"/>. That form is also the
/// persistence format: acknowledgment memory has to recognise a subject across runs and across
/// tool versions, so the identity has to survive a round trip through a file.
/// </para>
/// </summary>
public sealed class SubjectRef : IEquatable<SubjectRef>
{
    private const string Separator = "|";
    private const string EscapedSeparator = "\\|";
    private const string EscapeChar = "\\";
    private const string EscapedEscapeChar = "\\\\";

    private SubjectRef(SubjectKind kind, string canonical, IReadOnlyList<SubjectRef> members, SubjectRef? declaringType)
    {
        Kind = kind;
        Canonical = canonical;
        Members = members;
        DeclaringType = declaringType;
    }

    /// <summary>What kind of thing this refers to.</summary>
    public SubjectKind Kind { get; }

    /// <summary>
    /// The stable, round-trippable identity. Two subjects are the same subject exactly when
    /// these strings are ordinally equal.
    /// </summary>
    public string Canonical { get; }

    /// <summary>
    /// Members of a <see cref="SubjectKind.Set"/> subject, sorted and de-duplicated. Empty for
    /// every other kind.
    /// </summary>
    public IReadOnlyList<SubjectRef> Members { get; }

    /// <summary>
    /// For a <see cref="SubjectKind.Member"/> subject, the type that declares it; otherwise
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Suppression needs this. <c>TECHREQ-job-b.md</c> §4 suppresses breaks-alone for a type
    /// "already nominated as a concealed decision", and concealed decision can be nominated at
    /// method level — so the rule has to be able to walk from a member back to its type. Which
    /// way that particular rule should resolve is still open; see <c>docs/ARCHITECTURE.md</c>.
    /// </remarks>
    public SubjectRef? DeclaringType { get; }

    /// <summary>The solution as a whole. Coverage findings are about this.</summary>
    public static SubjectRef Solution { get; } =
        new(SubjectKind.Solution, "solution", [], null);

    /// <summary>References a type by assembly and fully-qualified name.</summary>
    public static SubjectRef ForType(string assembly, string fullyQualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedName);

        return new SubjectRef(
            SubjectKind.TypeDeclaration,
            Compose("type", assembly, fullyQualifiedName),
            [],
            null);
    }

    /// <summary>References a member by its declaring type and signature.</summary>
    /// <remarks>
    /// The signature, not the bare name: overloads are different members and a finding about
    /// one must not silence a finding about another.
    /// </remarks>
    public static SubjectRef ForMember(string assembly, string declaringTypeFullyQualifiedName, string signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaringTypeFullyQualifiedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        return new SubjectRef(
            SubjectKind.Member,
            Compose("member", assembly, declaringTypeFullyQualifiedName, signature),
            [],
            ForType(assembly, declaringTypeFullyQualifiedName));
    }

    /// <summary>References a project.</summary>
    public static SubjectRef ForProject(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SubjectRef(SubjectKind.Project, Compose("project", name), [], null);
    }

    /// <summary>References a namespace.</summary>
    public static SubjectRef ForNamespace(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SubjectRef(SubjectKind.Namespace, Compose("namespace", name), [], null);
    }

    /// <summary>
    /// References a set of subjects jointly — the members of a cycle, for instance.
    /// </summary>
    /// <remarks>
    /// Sorted and de-duplicated, because the same cycle discovered from a different entry point
    /// is the same cycle. Tarjan returns component membership, and membership has no inherent
    /// order; letting traversal order reach the identity would make a finding "new" because the
    /// walk started somewhere else. That is the same class of defect as the layout that renders
    /// three ways from one dataset.
    /// </remarks>
    public static SubjectRef ForSet(IEnumerable<SubjectRef> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var supplied = members.ToList();
        if (supplied.Exists(m => m is null))
        {
            throw new ArgumentException("A set subject cannot contain a null member.", nameof(members));
        }

        var canonicalMembers = supplied
            .DistinctBy(m => m.Canonical, StringComparer.Ordinal)
            .OrderBy(m => m.Canonical, StringComparer.Ordinal)
            .ToList();

        if (canonicalMembers.Count == 0)
        {
            throw new ArgumentException("A set subject needs at least one member.", nameof(members));
        }

        var parts = new List<string>(canonicalMembers.Count + 1) { "set" };
        parts.AddRange(canonicalMembers.Select(m => m.Canonical));

        return new SubjectRef(SubjectKind.Set, Compose([.. parts]), canonicalMembers, null);
    }

    /// <inheritdoc/>
    public bool Equals(SubjectRef? other) =>
        other is not null && string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SubjectRef);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Canonical);

    /// <inheritdoc/>
    public override string ToString() => Canonical;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(SubjectRef? left, SubjectRef? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(SubjectRef? left, SubjectRef? right) => !(left == right);

    /// <summary>
    /// Joins components with a separator that cannot be forged, so that
    /// <c>("a|b", "c")</c> and <c>("a", "b|c")</c> are different identities.
    /// </summary>
    /// <remarks>
    /// Method signatures and generic type names contain punctuation freely, and a set's
    /// canonical form nests other canonical forms inside itself. Without escaping, two
    /// genuinely different subjects could produce one string — which would silently merge two
    /// findings, the same failure mode as keying types on name alone.
    /// </remarks>
    private static string Compose(params string[] components) =>
        string.Join(Separator, components.Select(Escape));

    private static string Escape(string component) =>
        component
            .Replace(EscapeChar, EscapedEscapeChar, StringComparison.Ordinal)
            .Replace(Separator, EscapedSeparator, StringComparison.Ordinal);
}

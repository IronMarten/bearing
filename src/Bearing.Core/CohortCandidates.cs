namespace IronMarten.Bearing;

/// <summary>
/// The shape of a type, as far as grouping it is concerned.
/// </summary>
/// <param name="Name">The simple type name, e.g. <c>OrderNormalizer</c>.</param>
/// <param name="Namespace">Containing namespace, or empty for the global namespace.</param>
/// <param name="IsInterface">Whether the type is itself an interface.</param>
/// <param name="InSolutionInterfaces">
/// Fully-qualified names of the interfaces it implements that are part of this solution, in
/// declaration order. Interfaces from outside the solution say nothing about peer groups here.
/// </param>
/// <param name="InSolutionBaseType">
/// Fully-qualified name of its base type when that is part of this solution and is not
/// <c>object</c>; otherwise null.
/// </param>
public readonly record struct TypeShape(
    string Name,
    string Namespace,
    bool IsInterface,
    IReadOnlyList<string> InSolutionInterfaces,
    string? InSolutionBaseType);

/// <summary>
/// Derives the ways a type could be grouped with its peers.
/// </summary>
/// <remarks>
/// Separated from <see cref="CohortSet"/> because this half needs to know what a type is and
/// that half does not. Everything here is a decision about meaning — which interfaces group
/// anything, what counts as a name suffix — and is therefore Core's business rather than a
/// walker's incidental output.
/// </remarks>
public static class CohortCandidates
{
    /// <summary>
    /// Framework marker interfaces, excluded because they group everything and mean nothing.
    /// </summary>
    /// <remarks>
    /// Every disposable type in a solution implementing <c>IDisposable</c> is not evidence that
    /// they are peers; it is evidence that they hold resources. A cohort built on one of these
    /// is a cross-section of the whole codebase, and every percentile taken against it is a
    /// comparison between unlike things.
    /// </remarks>
    public static IReadOnlySet<string> UninformativeInterfaces { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "IDisposable", "IAsyncDisposable", "IEquatable", "IComparable", "ICloneable",
            "IEnumerable", "IEnumerator", "ISerializable", "IFormattable", "INotifyPropertyChanged",
        };

    /// <summary>
    /// The minimum length of a trailing word for it to be a useful grouping.
    /// </summary>
    /// <remarks>
    /// Below this the "suffix" is noise — <c>Db</c>, <c>Id</c>, <c>To</c> — and it would group
    /// types that have nothing to do with one another.
    /// </remarks>
    public const int MinSuffixLength = 3;

    /// <summary>
    /// Every way this type could be grouped, most specific first.
    /// </summary>
    /// <remarks>
    /// Always returns at least the namespace candidate, which is what makes assignment total.
    /// The architectural-kind candidate is <b>not</b> included: a type's kind is not known until
    /// its external references have been walked, so it is appended afterwards with
    /// <see cref="ForArchitecturalKind"/>.
    /// </remarks>
    public static IReadOnlyList<CohortCandidate> For(TypeShape type)
    {
        var candidates = new List<CohortCandidate>();

        foreach (var iface in type.InSolutionInterfaces ?? [])
        {
            if (UninformativeInterfaces.Contains(SimpleName(iface))) continue;
            candidates.Add(new CohortCandidate("impl:" + iface, "interface", CohortBasis.Interface));
        }

        if (!string.IsNullOrEmpty(type.InSolutionBaseType))
            candidates.Add(new CohortCandidate("base:" + type.InSolutionBaseType, "base type", CohortBasis.BaseType));

        if (TrailingWord(type.Name, type.IsInterface) is { } suffix)
            candidates.Add(new CohortCandidate("suffix:" + suffix, "name suffix", CohortBasis.NameSuffix));

        var ns = string.IsNullOrEmpty(type.Namespace) ? "<global>" : type.Namespace;
        candidates.Add(new CohortCandidate("ns:" + ns, "namespace", CohortBasis.Namespace));

        return candidates;
    }

    /// <summary>
    /// The architectural-kind candidate, or null when the kind cannot group anything.
    /// </summary>
    /// <remarks>
    /// A solution with a single DbContext and two repositories has no repository cohort, but it
    /// does have a data-access one — architectural role is a real peer group for things with no
    /// structural one. <c>Internal</c> is excluded: it is the catch-all, no more meaningful than
    /// the namespace it would displace.
    /// </remarks>
    public static CohortCandidate? ForArchitecturalKind(string kind) =>
        string.IsNullOrEmpty(kind) || string.Equals(kind, "Internal", StringComparison.Ordinal)
            ? null
            : new CohortCandidate("kind:" + kind, "architectural kind", CohortBasis.ArchitecturalKind);

    /// <summary>
    /// The last PascalCase word of a type name: <c>OrderNormalizer</c> gives
    /// <c>Normalizer</c>. Null when the name is a single word or the trailing word is too short
    /// to group on.
    /// </summary>
    /// <remarks>
    /// A leading <c>I</c> is stripped from interface names first, so <c>INormalizer</c> and
    /// <c>OrderNormalizer</c> land in the same suffix group rather than in two groups of one.
    /// </remarks>
    public static string? TrailingWord(string name, bool isInterface)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (isInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            name = name[1..];

        var start = 0;
        for (var i = 1; i < name.Length; i++)
            if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                start = i;

        if (start == 0) return null;   // single word — not a useful cohort

        var word = name[start..];
        return word.Length >= MinSuffixLength ? word : null;
    }

    private static string SimpleName(string fullyQualified)
    {
        var generic = fullyQualified.IndexOf('<', StringComparison.Ordinal);
        if (generic >= 0) fullyQualified = fullyQualified[..generic];

        var lastDot = fullyQualified.LastIndexOf('.');
        return lastDot >= 0 ? fullyQualified[(lastDot + 1)..] : fullyQualified;
    }
}

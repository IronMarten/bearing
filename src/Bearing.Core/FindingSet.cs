namespace IronMarten.Bearing;

/// <summary>
/// Every finding one run made, indexed so that findings can be asked about each other.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what stops suppression being ordering.</b> In the probe, "breaks alone is
/// suppressed for anything already nominated as a concealed decision" works by capturing the
/// nominations made earlier in the same method and testing membership later — so reordering the
/// renderer breaks invariant 3 silently, and breaking it produces <i>more</i> output, which
/// reads as a working tool. <c>TECHREQ-job-b.md</c> §4 requires suppression to become a declared
/// relationship between findings, evaluated before anything renders. A relationship needs both
/// findings to exist first, which is what this holds.
/// </para>
/// <para>
/// Identity is unique within a set: two findings with one key would make an acknowledgment
/// ambiguous about which claim it dismissed, so it is rejected rather than merged.
/// </para>
/// <para>
/// The order findings arrive in is preserved. Each detector emits in a total order of its own —
/// strongest evidence first, broken by identity — and nothing here re-sorts them, because the
/// set has no basis on which to compare a concealed decision against a boundary marking.
/// </para>
/// </remarks>
public sealed class FindingSet
{
    private readonly Dictionary<string, List<Finding>> _byKind;
    private readonly Dictionary<string, List<Finding>> _bySubject;
    private readonly HashSet<string> _keys;
    private readonly HashSet<string> _keysIncludingMembers;

    private FindingSet(
        IReadOnlyList<Finding> findings,
        Dictionary<string, List<Finding>> byKind,
        Dictionary<string, List<Finding>> bySubject,
        HashSet<string> keys,
        HashSet<string> keysIncludingMembers)
    {
        All = findings;
        _byKind = byKind;
        _bySubject = bySubject;
        _keys = keys;
        _keysIncludingMembers = keysIncludingMembers;
    }

    /// <summary>Every finding, in the order the detectors emitted them.</summary>
    public IReadOnlyList<Finding> All { get; }

    /// <summary>How many findings the run made.</summary>
    public int Count => All.Count;

    /// <summary>The empty set.</summary>
    public static FindingSet Empty { get; } = Of([]);

    /// <summary>Indexes a run's findings.</summary>
    /// <exception cref="ArgumentException">Two findings share one key.</exception>
    public static FindingSet Of(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var all = findings.ToList();
        var byKind = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
        var bySubject = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var including = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in all)
        {
            if (finding is null) throw new ArgumentException("A finding set cannot contain null.", nameof(findings));

            if (!keys.Add(finding.Key.Canonical))
                throw new ArgumentException(
                    $"Two findings share the key '{finding.Key.Canonical}'. A finding is identified by " +
                    "(kind, subject) and nothing else, so a duplicate is two claims an acknowledgment " +
                    "could not tell apart.",
                    nameof(findings));

            Add(byKind, finding.Kind.ToString(), finding);
            Add(bySubject, finding.Subject.Canonical, finding);

            including.Add(finding.Key.Canonical);

            // The member -> declaring type walk, which is the whole reason SubjectRef carries
            // one. A concealed decision nominated on a method is a concealed decision about the
            // type that declares it, because the reason the suppression exists is behavioural
            // and behaviour lives in methods.
            if (finding.Subject.DeclaringType is { } declaring)
                including.Add(new FindingKey(finding.Kind, declaring).Canonical);
        }

        return new FindingSet(all, byKind, bySubject, keys, including);
    }

    /// <summary>Whether the run made exactly this claim.</summary>
    public bool Contains(FindingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _keys.Contains(key.Canonical);
    }

    /// <summary>Whether the run made this claim about this subject.</summary>
    public bool Contains(FindingKind kind, SubjectRef subject) =>
        Contains(new FindingKey(kind, subject));

    /// <summary>
    /// Whether the run made this claim about this subject <b>or about anything it declares</b>.
    /// </summary>
    /// <remarks>
    /// The query the suppression matrix needs. "Already nominated as a concealed decision" does
    /// not say whether a nomination on one of the type's methods counts; it does, and the level
    /// that happened to nominate it does not change whether the decision is there. Asking with
    /// <see cref="Contains(FindingKind, SubjectRef)"/> instead is the type-level-only bug —
    /// the report saying "this method is making business judgements" and "if it breaks, it
    /// breaks alone" about one component.
    /// </remarks>
    public bool ContainsAbout(FindingKind kind, SubjectRef subject) =>
        _keysIncludingMembers.Contains(new FindingKey(kind, subject).Canonical);

    /// <summary>Every finding of one kind, in emission order.</summary>
    public IReadOnlyList<Finding> OfKind(FindingKind kind) =>
        _byKind.TryGetValue(kind.ToString(), out var found) ? found : [];

    /// <summary>Every finding about one subject, in emission order.</summary>
    public IReadOnlyList<Finding> About(SubjectRef subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return _bySubject.TryGetValue(subject.Canonical, out var found) ? found : [];
    }

    private static void Add(Dictionary<string, List<Finding>> index, string key, Finding finding)
    {
        if (!index.TryGetValue(key, out var bucket)) index[key] = bucket = [];
        bucket.Add(finding);
    }
}

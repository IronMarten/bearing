namespace IronMarten.Bearing;

/// <summary>
/// One way a type could be grouped with its peers, and how specific that way is.
/// </summary>
/// <param name="Key">The group identity, e.g. <c>impl:global::App.INormalizer</c>.</param>
/// <param name="Basis">How the group was arrived at, for the report to explain itself.</param>
/// <param name="Precedence">Lower is more specific. See <see cref="CohortBasis"/>.</param>
public readonly record struct CohortCandidate(string Key, string Basis, int Precedence);

/// <summary>
/// The ways a type can be grouped, most specific first.
/// </summary>
/// <remarks>
/// Specificity is the whole design. A type takes the most specific grouping that still has
/// enough members to compare against — never the largest, because the largest is always the
/// namespace and that collapses every cohort into one.
/// </remarks>
public static class CohortBasis
{
    /// <summary>Shares an in-solution interface. The strongest statement of "supposed to be alike".</summary>
    public const int Interface = 0;

    /// <summary>Shares a base type.</summary>
    public const int BaseType = 1;

    /// <summary>Shares a trailing name word — <c>OrderNormalizer</c> and <c>RateNormalizer</c>.</summary>
    public const int NameSuffix = 2;

    /// <summary>
    /// Shares an architectural kind. Appended only after the walk, because a type's kind is not
    /// known until its external references have been collected.
    /// </summary>
    public const int ArchitecturalKind = 3;

    /// <summary>Shares a namespace. The least specific, and the fallback that makes assignment total.</summary>
    public const int Namespace = 4;
}

/// <summary>A type and the ways it could be grouped.</summary>
/// <remarks>
/// <paramref name="Candidates"/> order is significant: candidates of equal precedence are
/// resolved first-wins, so the caller must produce them deterministically. Declaration order —
/// which is what Roslyn yields for interfaces — is the intended tiebreak.
/// </remarks>
public readonly record struct CohortSubject(string Id, IReadOnlyList<CohortCandidate> Candidates);

/// <summary>The peer group a type was assigned to.</summary>
public readonly record struct Cohort(string Key, string Basis);

/// <summary>
/// Assigns every type to the peer group it should be judged against.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the substrate.</b> Everything the tool reports is relative to a peer group and
/// nothing is absolute, so an error here does not break a finding's condition — it quietly
/// changes what every finding is comparing against. That is why it is specified and tested
/// separately from the findings that rest on it.
/// </para>
/// <para>
/// The algorithm is pure: candidates in, assignment out, no Roslyn. Deriving the candidates
/// needs symbols; choosing between them does not, and separating the two is what makes the
/// interesting cases — stranding, reconciliation, starvation — testable directly instead of
/// through a solution that happens to exhibit them.
/// </para>
/// </remarks>
public sealed class CohortSet
{
    private readonly Dictionary<string, Cohort> _assignments;
    private readonly Dictionary<string, int> _sizes;

    private CohortSet(Dictionary<string, Cohort> assignments, Dictionary<string, int> sizes)
    {
        _assignments = assignments;
        _sizes = sizes;
    }

    /// <summary>How many types ended up in each group.</summary>
    public IReadOnlyDictionary<string, int> Sizes => _sizes;

    /// <summary>The group a type was assigned to.</summary>
    public Cohort this[string typeId] => _assignments[typeId];

    /// <summary>How many peers a type ended up with, itself included.</summary>
    public int SizeOf(string typeId) => _sizes[_assignments[typeId].Key];

    /// <summary>
    /// Assigns every subject to a peer group.
    /// </summary>
    /// <param name="subjects">
    /// Every type, with its candidates. Each subject must carry at least one candidate —
    /// candidate derivation always yields the namespace, which is what makes assignment total.
    /// </param>
    /// <param name="minCohort">The smallest group that can support a comparative reading.</param>
    public static CohortSet Assign(IEnumerable<CohortSubject> subjects, int minCohort)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        var all = subjects.ToList();
        foreach (var s in all)
        {
            if (s.Candidates is null || s.Candidates.Count == 0)
                throw new ArgumentException(
                    $"'{s.Id}' has no cohort candidates. Derivation must always yield at least " +
                    "the namespace, or assignment cannot be total.",
                    nameof(subjects));
        }

        // Candidate counts over-estimate: they count everyone who *could* join a group, not
        // everyone who does. Good enough to choose with, and corrected below.
        var candidateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in all)
            foreach (var c in s.Candidates)
                candidateCounts[c.Key] = candidateCounts.TryGetValue(c.Key, out var n) ? n + 1 : 1;

        var assignments = new Dictionary<string, Cohort>(StringComparer.Ordinal);
        foreach (var s in all)
        {
            // Most specific basis that still yields a usable group. Largest-group-wins would
            // always pick the namespace, since it is the most inclusive candidate available.
            var viable = s.Candidates
                .Where(c => candidateCounts[c.Key] >= minCohort)
                .OrderBy(c => c.Precedence)
                .Take(1)
                .ToList();

            var best = viable.Count > 0
                ? viable[0]
                : s.Candidates
                    .OrderByDescending(c => candidateCounts[c.Key])
                    .ThenBy(c => c.Precedence)
                    .First();

            assignments[s.Id] = new Cohort(best.Key, best.Basis);
        }

        Reconcile(all, assignments, minCohort);

        return new CohortSet(assignments, SizesOf(assignments));
    }

    /// <summary>
    /// Re-homes types the candidate counts stranded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine types share a <c>Normalizer</c> suffix; eight of them also share an interface and
    /// leave for it; the ninth is stranded alone despite having the highest fan-in in the
    /// solution. A cohort of one is the worst outcome available — every relative statistic
    /// compares the type against itself.
    /// </para>
    /// <para>
    /// Re-homing counts <b>potential</b> size rather than current size, because a more specific
    /// cohort forming can starve a coarser one below the floor: five contracts leaving for
    /// <c>kind:Contract</c> drops the namespace from nine to four, and then every stranded type
    /// is stuck, since none can move into a group that no longer qualifies. Counting fellow
    /// strandees means several arriving together is what makes the group viable — which is
    /// usually exactly what happens.
    /// </para>
    /// </remarks>
    private static void Reconcile(
        List<CohortSubject> subjects,
        Dictionary<string, Cohort> assignments,
        int minCohort)
    {
        const int maxPasses = 3;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            var actual = SizesOf(assignments);
            var stranded = subjects.Where(s => actual[assignments[s.Id].Key] < minCohort).ToList();
            if (stranded.Count == 0) break;

            var potential = new Dictionary<string, int>(actual, StringComparer.Ordinal);
            foreach (var s in stranded)
                foreach (var c in s.Candidates)
                {
                    if (string.Equals(c.Key, assignments[s.Id].Key, StringComparison.Ordinal)) continue;
                    potential[c.Key] = potential.TryGetValue(c.Key, out var n) ? n + 1 : 1;
                }

            var moved = false;
            foreach (var s in stranded)
            {
                var current = assignments[s.Id].Key;
                CohortCandidate? best = null;

                foreach (var c in s.Candidates)
                {
                    if (string.Equals(c.Key, current, StringComparison.Ordinal)) continue;
                    if (!potential.TryGetValue(c.Key, out var size) || size < minCohort) continue;
                    if (best is { } b && c.Precedence >= b.Precedence) continue;
                    best = c;
                }

                if (best is not { } chosen) continue;   // genuinely has no peers — reported as such

                assignments[s.Id] = new Cohort(chosen.Key, chosen.Basis);
                moved = true;
            }

            if (!moved) break;
        }
    }

    private static Dictionary<string, int> SizesOf(Dictionary<string, Cohort> assignments)
    {
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cohort in assignments.Values)
            sizes[cohort.Key] = sizes.TryGetValue(cohort.Key, out var n) ? n + 1 : 1;
        return sizes;
    }
}

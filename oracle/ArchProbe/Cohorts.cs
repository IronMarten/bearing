using Microsoft.CodeAnalysis;

namespace ArchProbe;

/// <summary>
/// Assigns each type to a structural peer cohort — the group of things it is supposed to
/// be like. Everything the probe reports is relative to this group, never absolute.
///
/// Candidates are gathered per type, then each type takes whichever of its candidates
/// produces the largest group. So 56 types that all implement IResponseNormalizer cohort
/// on the interface; 56 that merely end in "Normalizer" cohort on the suffix.
/// </summary>
static class Cohorts
{
    // Framework marker interfaces that group everything and mean nothing.
    static readonly HashSet<string> UselessInterfaces = new(StringComparer.Ordinal)
    {
        "IDisposable", "IAsyncDisposable", "IEquatable", "IComparable", "ICloneable",
        "IEnumerable", "IEnumerator", "ISerializable", "IFormattable", "INotifyPropertyChanged"
    };

    public static void Assign(IReadOnlyCollection<TypeMetrics> types,
                              IReadOnlyDictionary<string, List<Candidate>> candidatesByType,
                              int minCohort)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var list in candidatesByType.Values)
            foreach (var c in list)
                counts[c.Key] = counts.TryGetValue(c.Key, out var n) ? n + 1 : 1;

        foreach (var t in types)
        {
            if (!candidatesByType.TryGetValue(t.Id, out var candidates) || candidates.Count == 0)
            {
                t.Cohort = "ns:" + (string.IsNullOrEmpty(t.Namespace) ? "<global>" : t.Namespace);
                t.CohortBasis = "namespace";
                continue;
            }

            // Most SPECIFIC basis that still yields a statistically usable group.
            // (Largest-group-wins would always pick the namespace, since it is the most
            // inclusive candidate — that collapses every cohort into one and makes the
            // percentiles meaningless.)
            var best = candidates
                .Where(c => counts[c.Key] >= minCohort)
                .OrderBy(c => c.Precedence)
                .FirstOrDefault();

            if (best.Key is null)
                best = candidates
                    .OrderByDescending(c => counts[c.Key])
                    .ThenBy(c => c.Precedence)
                    .First();

            t.Cohort = best.Key;
            t.CohortBasis = best.Basis;
        }

        Reconcile(types, candidatesByType, minCohort);
    }

    /// <summary>
    /// The size gate above tests CANDIDATE counts, which over-estimate: nine types may
    /// share the "Normalizer" suffix, but if eight of them also share an interface they
    /// leave for that more specific cohort and strand the ninth alone. A cohort of one is
    /// the worst outcome available — every relative statistic compares the type to
    /// itself, so it reports as exactly median no matter how extreme it really is.
    ///
    /// So re-home anything left below the floor, this time against ACTUAL group sizes.
    /// </summary>
    static void Reconcile(IReadOnlyCollection<TypeMetrics> types,
                          IReadOnlyDictionary<string, List<Candidate>> candidatesByType,
                          int minCohort)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            var actual = types.GroupBy(t => t.Cohort, StringComparer.Ordinal)
                              .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var moved = false;

            var stranded = types.Where(t => actual[t.Cohort] < minCohort).ToList();

            // Potential size, not current size. A more specific cohort forming can starve
            // a coarser one below the floor — five contracts leaving for kind:Contract
            // drops the namespace from 9 to 4 — and then every stranded type is stuck,
            // because none of them can move into a group that no longer qualifies. So
            // count the strandees that COULD move somewhere alongside each other: several
            // of them arriving together is often exactly what makes the group viable.
            var potential = new Dictionary<string, int>(actual, StringComparer.Ordinal);
            foreach (var t in stranded)
            {
                if (!candidatesByType.TryGetValue(t.Id, out var cands)) continue;
                foreach (var c in cands)
                {
                    if (string.Equals(c.Key, t.Cohort, StringComparison.Ordinal)) continue;
                    potential[c.Key] = potential.TryGetValue(c.Key, out var n) ? n + 1 : 1;
                }
            }

            foreach (var t in stranded)
            {
                if (!candidatesByType.TryGetValue(t.Id, out var candidates)) continue;

                var found = false;
                var bestKey = ""; var bestBasis = ""; var bestPrecedence = int.MaxValue;

                foreach (var c in candidates)
                {
                    if (string.Equals(c.Key, t.Cohort, StringComparison.Ordinal)) continue;
                    if (!potential.TryGetValue(c.Key, out var size) || size < minCohort) continue;
                    if (c.Precedence >= bestPrecedence) continue;
                    bestKey = c.Key; bestBasis = c.Basis; bestPrecedence = c.Precedence;
                    found = true;
                }

                if (!found) continue;   // genuinely has no peers — reported as such
                t.Cohort = bestKey;
                t.CohortBasis = bestBasis;
                moved = true;
            }

            if (!moved) break;
        }
    }

    public readonly record struct Candidate(string Key, string Basis, int Precedence);

    public static List<Candidate> CandidatesFor(INamedTypeSymbol type, Func<INamedTypeSymbol, bool> isInSolution)
    {
        var result = new List<Candidate>();

        foreach (var iface in type.Interfaces)
        {
            if (!isInSolution(iface)) continue;
            if (UselessInterfaces.Contains(iface.Name)) continue;
            result.Add(new Candidate("impl:" + Fq(iface.OriginalDefinition), "interface", 0));
        }

        var bt = type.BaseType;
        if (bt != null && bt.SpecialType != SpecialType.System_Object && isInSolution(bt))
            result.Add(new Candidate("base:" + Fq(bt.OriginalDefinition), "base type", 1));

        var suffix = TrailingWord(type.Name, type.TypeKind == TypeKind.Interface);
        if (suffix != null)
            result.Add(new Candidate("suffix:" + suffix, "name suffix", 2));

        // Precedence 3 is reserved for architectural Kind, appended after analysis
        // (Kind isn't known until the type's external references have been walked).

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "<global>";
        result.Add(new Candidate("ns:" + ns, "namespace", 4));

        return result;
    }

    static string Fq(ISymbol s) => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Last PascalCase word of a type name: OrderNormalizer -> Normalizer.</summary>
    static string TrailingWord(string name, bool isInterface)
    {
        if (isInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            name = name.Substring(1);

        var start = 0;
        for (var i = 1; i < name.Length; i++)
            if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                start = i;

        if (start == 0) return null;               // single word — not a useful cohort
        var word = name.Substring(start);
        return word.Length >= 3 ? word : null;
    }
}

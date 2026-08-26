namespace IronMarten.Bearing.Cli;

/// <summary>
/// The bridge between a judgement and the shapes a cycle section draws.
/// </summary>
/// <remarks>
/// <para>
/// <b>Population from the <see cref="Judgement"/>, display detail from the
/// <see cref="SolutionModel"/>.</b> That is the rule <c>docs/ARCHITECTURE.md</c> §11 settles, and
/// the circular-references sections are the reason it needed settling: both of them used to take
/// their population from <c>model.ShapedNamespaceCycles</c> and split it on
/// <c>ShapedCycle.IsReportable</c>, which is a renderer re-deciding, from the shape alone, a
/// question the suppression matrix had already answered over the whole finding set.
/// </para>
/// <para>
/// <b>Two renderers agreeing today is not the same as one rule.</b> They agreed because
/// <c>IsReportable</c> and the two cycle suppression rows happen to test the same shape; nothing
/// held them together, and a row keyed on anything other than the shape — the user's own file being
/// the first — is invisible to <c>IsReportable</c> and to every section built on it.
/// </para>
/// <para>
/// <b>The model's order is preserved.</b> These filter the list the model already sorted rather
/// than re-ordering by the finding set, because the order a cycle section shows is a property of
/// the model's own ranking and is not a judgement about anything.
/// </para>
/// </remarks>
internal static class CycleViews
{
    /// <summary>The members of <paramref name="all"/> whose claim of this kind reached the reader.</summary>
    internal static IReadOnlyList<T> Reported<T>(
        Judgement judgement, FindingKind kind, IEnumerable<T> all, Func<T, SubjectRef> subject)
    {
        var reported = judgement.Reported.OfKind(kind)
            .Select(f => f.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        return [.. all.Where(item => reported.Contains(subject(item).Canonical))];
    }

    /// <summary>
    /// The members of <paramref name="all"/> whose claim of this kind did not, each with the
    /// judgement that stopped it.
    /// </summary>
    internal static IReadOnlyList<(T Shape, Judged Judged)> Withheld<T>(
        Judgement judgement, FindingKind kind, IEnumerable<T> all, Func<T, SubjectRef> subject)
    {
        var withheld = judgement.WithheldOfKind(kind)
            .ToDictionary(j => j.Finding.Subject.Canonical, j => j, StringComparer.Ordinal);

        return
        [
            .. all
                .Where(item => withheld.ContainsKey(subject(item).Canonical))
                .Select(item => (item, withheld[subject(item).Canonical]))
        ];
    }
}

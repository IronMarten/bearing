using System.Text.RegularExpressions;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// Rules the report's own English has to satisfy, checkable over rendered output.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because reading is the only instrument that has ever found these, and reading
/// does not converge.</b> The count-verb defect was the fifth recurrence of one class — a
/// sentence disagreeing with the number in it — and each of the previous four was fixed at the
/// site that happened to be read. Two of the five were sitting on the nopCommerce report through
/// both A11 rounds, in front of seven developers, because they were given tasks rather than asked
/// to proofread.
/// </para>
/// <para>
/// <b>Rules over rendered text, not over the helpers.</b> <see cref="Sentences.Plural"/> and
/// <see cref="Sentences.Do"/> are correct; every instance of it is a <i>call site</i> that
/// hardcoded a verb beside a helper that got the number right. A test of the helpers passes while
/// the page is wrong, which is how this survived four fixes.
/// </para>
/// <para>
/// <b>Deliberately heuristic, and it fails loudly rather than quietly.</b> These are patterns over
/// English, so a legitimate sentence can trip one — the answer is to reword the sentence or narrow
/// the rule, not to weaken it into something that finds nothing. Every violation this has found so
/// far was real.
/// </para>
/// </remarks>
public static class Prose
{
    /// <summary>A rule, and what it found.</summary>
    public readonly record struct Violation(string Rule, string Line);

    /// <summary>
    /// <c>1 file … were not read</c> — a singular count followed by a plural verb or pronoun.
    /// </summary>
    private static readonly Regex SingularWithPlural = new(
        @"(?<![\d.])1\s+[A-Za-z][A-Za-z.]*\s+(?:\w+\s+){0,6}?(?:were|are|have|their|them|they)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// <c>2 projects … every reference it names</c> — a plural count against a singular verb or
    /// pronoun. <see cref="SingularWithPlural"/> in the mirror.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A disclosure wrote one of these while being written, which is the whole
    /// argument for the rule.</b> The disclosure shipped its first draft as <i>"2 projects did NOT
    /// resolve every reference <b>it</b> names"</i>. That is the same defect with the number and the
    /// verb swapped, and the existing rule could not see it: it anchors on a literal <c>1</c> and
    /// looks forward for a <i>plural</i>. Reading caught it. Reading does not converge, which is
    /// why <see cref="Prose"/> exists at all.
    /// </para>
    /// <para>
    /// <b>Two narrowings, and the corpus decided both.</b> A bare <c>it</c> is the report's most
    /// common object pronoun — <i>"38 types depend on it"</i>, <i>"0 types call into it"</i> —
    /// and admitting it produced <b>98 hits across nopCommerce, Jellyfin and Umbraco, every one of
    /// them correct English</b>. So <c>it</c> counts only where it is the <i>subject</i> of a
    /// third-person verb (<c>it names</c>), which is the shape the defect takes.
    /// </para>
    /// <para>
    /// <b>And the window stops at a word that brings its own subject.</b> Without that,
    /// <i>"11 kinds fired and <b>each has</b> one claim below"</i> trips it — correct English,
    /// because <c>has</c> agrees with <c>each</c> rather than with <c>11 kinds</c>. Narrowed rather
    /// than weakened, which is this file's standing instruction: the rule still fires on the
    /// sentence it was written for, and on <b>nothing</b> in either renderer across all three
    /// reference solutions or any fixture golden.
    /// </para>
    /// </remarks>
    private static readonly Regex PluralWithSingular = new(
        @"(?<![\d.])(?!1\s)\d+\s+[A-Za-z][A-Za-z.]*s\b\s+(?:(?!(?:each|one|which|who|whose|that|this|there|nothing|something|everything|anything|he|she)\b)\w+\s+){0,6}?(?:(?:was|is|has|its)\b|it\s+[a-z]+s\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// <c>37 boundarys</c> — a consonant-plus-y noun pluralised by appending <c>s</c>.
    /// </summary>
    /// <remarks>
    /// Anchored on a preceding number so that it reads the tool's own generated plurals rather
    /// than any identifier it happens to quote: a type genuinely named <c>Keys</c> or a namespace
    /// ending in <c>ys</c> is not this defect, and <c>--top</c> disclosures are.
    /// </remarks>
    private static readonly Regex NaivePlural = new(
        @"\b\d+\s+[A-Za-z]*[bcdfghjklmnpqrstvwxz]ys\b",
        RegexOptions.Compiled);

    /// <summary>
    /// A stack frame. A diagnostic belongs in the report; the frames
    /// underneath it belong to whoever wrote the task that threw.
    /// </summary>
    private static readonly Regex StackFrame = new(
        @"^\s*at\s+[A-Za-z_][\w.<>`+]*\([^)]*\)",
        RegexOptions.Compiled);

    /// <summary>Every rule violated by <paramref name="report"/>, in the order they appear.</summary>
    /// <param name="report">A rendered report — either renderer's, or any text the tool emits.</param>
    public static IReadOnlyList<Violation> Violations(string report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var found = new List<Violation>();

        foreach (var line in report.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0) continue;

            if (SingularWithPlural.IsMatch(text)) found.Add(new("singular-count-plural-verb", text.Trim()));
            if (PluralWithSingular.IsMatch(text)) found.Add(new("plural-count-singular-verb", text.Trim()));
            if (NaivePlural.IsMatch(text)) found.Add(new("naive-plural", text.Trim()));
            if (StackFrame.IsMatch(text)) found.Add(new("stack-frame", text.Trim()));
        }

        return found;
    }
}

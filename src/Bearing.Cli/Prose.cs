using System.Text.RegularExpressions;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// Rules the report's own English has to satisfy, checkable over rendered output.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because reading is the only instrument that has ever found these, and reading
/// does not converge.</b> <c>docs/DEFECTS.md</c> §55 is the fifth recurrence of one class — a
/// sentence disagreeing with the number in it — and each of the previous four was fixed at the
/// site that happened to be read. Two of the five were sitting on the nopCommerce report through
/// both A11 rounds, in front of seven developers, because they were given tasks rather than asked
/// to proofread.
/// </para>
/// <para>
/// <b>Rules over rendered text, not over the helpers.</b> <see cref="Sentences.Plural"/> and
/// <see cref="Sentences.Do"/> are correct; every instance of §55 is a <i>call site</i> that
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
    /// A stack frame — <c>docs/DEFECTS.md</c> §59. A diagnostic belongs in the report; the frames
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
            if (NaivePlural.IsMatch(text)) found.Add(new("naive-plural", text.Trim()));
            if (StackFrame.IsMatch(text)) found.Add(new("stack-frame", text.Trim()));
        }

        return found;
    }
}

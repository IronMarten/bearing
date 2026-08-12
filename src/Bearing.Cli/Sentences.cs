using System.Globalization;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The small phrasing decisions every section shares.
/// </summary>
/// <remarks>
/// Presentation only. Nothing here decides whether a claim may be made — that is settled before
/// a finding reaches the renderer — and nothing here reads the model. These turn numbers already
/// on a finding into the words the probe used for them, so that moving the report does not also
/// move the report's voice.
/// </remarks>
internal static class Sentences
{
    /// <summary>Two decimal places at most, invariant, with infinity spelled out.</summary>
    internal static string Number(double value) =>
        double.IsInfinity(value) ? "inf" : value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Three decimal places at most — instability, where the third digit carries meaning.</summary>
    internal static string Ratio(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Whole numbers, invariant.</summary>
    internal static string Whole(double value) =>
        value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary><c>1 type</c> / <c>3 types</c>.</summary>
    internal static string Plural(double count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{Whole(count)} {noun}s";

    /// <summary>
    /// A percentile as a "top N%" phrase, floored at 1%.
    /// </summary>
    /// <remarks>
    /// Floored because "top 0%" reads as a rounding artefact rather than as the strongest
    /// possible claim, and the strongest possible claim is what it is.
    /// </remarks>
    internal static string TopPercent(double percentile) =>
        $"{Math.Max(1, Math.Round(100 - percentile)):0}%";

    /// <summary>
    /// What a list dropped, said out loud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §3, and it is the renderer's half of the fix.</b> Core does not
    /// truncate — every detector emits every finding it made, and the display cap lives here
    /// because how many lines fit on a screen is a presentation decision. But a capped list that
    /// does not say it was capped reports "15 of 106" as though it were all of them, and a reader
    /// cannot tell a short list from a shortened one.
    /// </para>
    /// <para>
    /// Returns nothing when nothing was dropped, so the disclosure appears exactly when it is
    /// true. Invariant 8.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string> Truncation(int total, int shown, string noun, string indent = "   ")
    {
        if (total <= shown) yield break;

        yield return $"{indent}({Plural(total - shown, noun)} not shown of {Whole(total)} — "
                     + $"raise --top to see {(total - shown == 1 ? "it" : "them")}.)";
    }

    /// <summary>Applies the display cap and reports what it cost, in one pass.</summary>
    internal static (IReadOnlyList<T> Shown, IReadOnlyList<string> Disclosure) Cap<T>(
        IReadOnlyList<T> items, int limit, string noun, string indent = "   ")
    {
        if (items.Count <= limit) return (items, []);

        return (items.Take(limit).ToList(), Truncation(items.Count, limit, noun, indent).ToList());
    }
}

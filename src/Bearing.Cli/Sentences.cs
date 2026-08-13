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

    /// <summary>A member named under its declaring type, in words a reader can search for.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §24.</b> A constructor's member name <i>is</i> <c>.ctor</c>, so
    /// joining type and member with a dot produced <c>CustomerInfoValidator..ctor</c>. It was
    /// filed as cosmetic and only visible on real code; it is now the first row of nopCommerce's
    /// concealed-decision section, because the ranking fix moved a pair of constructors to the top.
    /// </para>
    /// <para>
    /// Spelled out rather than trimmed to <c>ctor</c>. The name is here so a reader can find the
    /// thing, and <i>constructor</i> is what they would call it — <c>.ctor</c> is what the runtime
    /// calls it, which is the same mistake one level down as printing <c>MaxMemberCyclomaticPctl</c>
    /// at somebody (§27).
    /// </para>
    /// </remarks>
    internal static string Member(string owner, string member) => member switch
    {
        ".ctor" => $"{owner} constructor",
        ".cctor" => $"{owner} static constructor",
        _ => $"{owner}.{member}",
    };

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
    /// A peer group, named the way the reader would name it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The template this replaces had grammar for one basis out of five.</b> It read
    /// <c>"among your {size} {last segment of the cohort key}"</c>, which was written against
    /// <c>suffix:Normalizer</c> and a solution with 56 of them. Every other basis produced
    /// nonsense: a namespace cohort rendered as <i>"among your 63 Bearing"</i> and <i>"your 17
    /// ArchProbe"</i>, a base-type cohort as <i>"your 8 ControllerBase"</i>, an architectural-kind
    /// cohort as <i>"your 1 ApiBoundary"</i>. <c>PRD-free-tier.md</c> §4 names this sentence as the
    /// one thing to get right, and on any solution that is not organised by name suffix it was the
    /// first thing a reader saw.
    /// </para>
    /// <para>
    /// <b>Each basis gets its own phrase rather than a plural rule</b>, because the bases are not
    /// grammatically alike: a suffix is a pattern, a namespace is a place, a base type and an
    /// interface are relationships, and a kind is a classification this tool assigned rather than
    /// something the code says about itself. The last of those is worth wording carefully — a
    /// reader who sees "the 5 types we classified as DataAccess" can tell the classifier was
    /// involved, and <c>docs/DEFECTS.md</c> §5 is the reason that matters.
    /// </para>
    /// <para>
    /// The <c>Basis</c> strings come from <see cref="CohortBasis"/> by way of
    /// <c>CohortCandidates</c>; an unrecognised one falls back to naming the key, which is
    /// wrong-but-honest rather than silently ungrammatical.
    /// </para>
    /// </remarks>
    internal static string PeerGroup(Cohort cohort, int size)
    {
        ArgumentNullException.ThrowIfNull(cohort);

        var name = ShortName(cohort.Key);

        return cohort.Basis switch
        {
            "name suffix" => $"the {Plural(size, "type")} whose name ends in {name}",
            "namespace" => $"the {Plural(size, "type")} in {FullName(cohort.Key)}",
            "base type" => $"the {Plural(size, "type")} deriving from {name}",
            "interface" => size == 1
                ? $"the 1 implementation of {name}"
                : $"the {Whole(size)} implementations of {name}",
            "architectural kind" => $"the {Plural(size, "type")} classified as {name}",
            _ => $"the {Plural(size, "type")} in {name}",
        };
    }

    /// <summary>
    /// The same group, described without counting it — <c>types whose name ends in Depot</c>.
    /// </summary>
    /// <remarks>
    /// A second switch rather than <see cref="PeerGroup"/> with the count removed, because the two
    /// forms are wanted in sentences that count different things. A finding says <i>"among the 8
    /// types deriving from ControllerBase"</i>, counting the subject itself, which is right when
    /// the claim is about where it sits in that population. The coverage list says how many
    /// <i>peers</i> a type has, which is one fewer, and is the number that section exists to
    /// report — <c>"the 1 type classified as ApiBoundary"</c> is a true sentence about a type that
    /// has no peers at all, and reads as though it had one.
    /// </remarks>
    internal static string PeerGroupNoun(Cohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);

        var name = ShortName(cohort.Key);

        return cohort.Basis switch
        {
            "name suffix" => $"types whose name ends in {name}",
            "namespace" => $"types in {FullName(cohort.Key)}",
            "base type" => $"types deriving from {name}",
            "interface" => $"implementations of {name}",
            "architectural kind" => $"types classified as {name}",
            _ => $"types in {name}",
        };
    }

    /// <summary><c>base:global::App.ControllerBase</c> becomes <c>ControllerBase</c>.</summary>
    internal static string ShortName(string cohortKey)
    {
        var afterPrefix = FullName(cohortKey);

        var lastDot = afterPrefix.LastIndexOf('.');
        return lastDot >= 0 ? afterPrefix[(lastDot + 1)..] : afterPrefix;
    }

    /// <summary>
    /// The cohort key with its basis prefix and <c>global::</c> removed, but nothing else — a
    /// namespace is only itself when it is complete.
    /// </summary>
    internal static string FullName(string cohortKey)
    {
        ArgumentNullException.ThrowIfNull(cohortKey);

        var afterPrefix = cohortKey.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0
            ? cohortKey[(colon + 1)..]
            : cohortKey;

        return afterPrefix.StartsWith("global::", StringComparison.Ordinal)
            ? afterPrefix["global::".Length..]
            : afterPrefix;
    }

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

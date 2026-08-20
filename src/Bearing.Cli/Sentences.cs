using System.Globalization;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The small phrasing decisions every section shares.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public for the reason <see cref="Html"/> is public.</b> These are the rules whose failure is
/// silent and whose cases a fixture cannot always reach — <c>docs/DEFECTS.md</c> §32 is three
/// sentences that agreed a verb with a number no solution in this repository makes singular, so the
/// rule has to be assertable without a solution that exercises it. <c>Bearing.Cli</c> packs as a
/// tool and not as a library, so nothing about its surface is a contract.
/// </para>
/// <para>
/// Presentation only. Nothing here decides whether a claim may be made — that is settled before
/// a finding reaches the renderer — and nothing here reads the model. These turn numbers already
/// on a finding into the words the probe used for them, so that moving the report does not also
/// move the report's voice.
/// </para>
/// </remarks>
public static class Sentences
{
    /// <summary>Two decimal places at most, invariant, with an undefined ratio said so.</summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §28. Infinity here is always a ratio against a zero median, which
    /// is undefined and not enormous. The sections that can produce one mostly branch before they
    /// get here — <i>"the only complexity among its 37 peers"</i> — and this is what is left.
    /// </remarks>
    public static string Number(double value) =>
        double.IsInfinity(value) ? "undefined" : value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Whole numbers, invariant.</summary>
    public static string Whole(double value) =>
        value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary><c>1 type</c> / <c>3 types</c>.</summary>
    public static string Plural(double count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{Whole(count)} {noun}s";

    /// <summary>
    /// The verb that agrees with a counted noun — <c>1 type <b>calls</b></c>, <c>3 types
    /// <b>call</b></c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §32, and it is the third instance of one mistake.</b> The register
    /// already carries <i>"the other 1 are entangled too"</i>, reworded at A3 to carry no verb at
    /// all, with the note that <i>"the next such number is a defect waiting on the right input"</i>.
    /// It was: three sentences agreed a verb with a plural that a real solution made singular —
    /// shared mutable state's <i>"1 type call into it"</i>, change cost's <i>"1 internal caller
    /// depend on this contract"</i>, and load-bearing's <i>"1 type depend on it"</i>.
    /// </para>
    /// <para>
    /// <b>Removing the verb was the right fix once and the wrong fix three times.</b> A rule that
    /// says <i>do not write verbs after computed numbers</i> is a rule nobody can follow while
    /// writing the sentences this report is made of — <c>PRD-free-tier.md</c> §4 asks for sentences,
    /// not for phrases. Breaks-alone already had the conditional inline and got it right, which is
    /// the proof this is a missing helper rather than a missing discipline.
    /// </para>
    /// </remarks>
    public static string Do(double count, string singular, string plural) =>
        count == 1 ? singular : plural;

    /// <summary>
    /// What this run means by <i>type</i>, in the reader's words — <c>classes, interfaces and
    /// enums</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The word has two readings and a reader outside the build hit the wrong one</b>: <i>type</i>
    /// as a category of thing, against <i>type</i> as the C# declaration. Every count in this report
    /// is the second, and nothing said so — the report opened on <i>"3,209 types"</i> and left the
    /// reader to guess which. It is the same defect class as <c>docs/DEFECTS.md</c> §26: a phrase
    /// that is perfectly clear once you know what job it is doing.
    /// </para>
    /// <para>
    /// <b>Derived from the run rather than written down, so it cannot be wrong.</b> A fixed list
    /// would say <i>records</i> on a solution that has none and omit <i>enums</i>, which are 91 of
    /// nopCommerce's 3,209 and the third-largest group. Largest group first, so the list opens on
    /// the kind that carries the count. Said once, at the first mention in each renderer; after
    /// that the word is defined and <i>type</i> stands on its own.
    /// </para>
    /// </remarks>
    public static string TypeKinds(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var kinds = model.Types
            .GroupBy(t => t.TypeKeyword, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Kinds(g.Key.ToLowerInvariant()))
            .ToList();

        return List(kinds);
    }

    /// <summary>
    /// A type keyword, pluralised — <c>class</c> takes <c>es</c> and <see cref="Plural"/> would
    /// have written <c>classs</c>.
    /// </summary>
    private static string Kinds(string keyword) =>
        keyword.EndsWith('s') || keyword.EndsWith('x') || keyword.EndsWith("ch", StringComparison.Ordinal)
            ? $"{keyword}es"
            : $"{keyword}s";

    /// <summary>
    /// A list, as English writes one — <c>a</c>, <c>a and b</c>, <c>a, b and c</c>.
    /// </summary>
    public static string List(IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Count switch
        {
            0 => "",
            1 => items[0],
            2 => $"{items[0]} and {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
        };
    }

    /// <summary>
    /// A count of the shapes crossing a public surface, agreeing with itself.
    /// </summary>
    /// <remarks>
    /// The same defect in a compound noun, where the conditional cannot be written inline without
    /// repeating the number: <c>1 fields/params</c> shipped on nopCommerce's opening change-cost
    /// row, which is <c>BaseEntity</c> — 458 callers, and the first contract a reader meets.
    /// </remarks>
    public static string Surface(double count) =>
        count == 1 ? "1 field/param" : $"{Whole(count)} fields/params";

    /// <summary>Where an external namespace came from, as a trailing phrase, or nothing.</summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §30. Worded rather than coloured, because the terminal has no colour
    /// to spend and the HTML should say the same thing as the text it was generated beside.
    /// <see cref="ExternalOrigin.Unknown"/> says nothing at all: a row with no marker is one this
    /// tool could not place, and inventing a third label for it would be the guess the origin
    /// exists to avoid.
    /// </remarks>
    public static string Origin(ExternalOrigin origin) => origin switch
    {
        ExternalOrigin.Framework => "  (framework)",
        ExternalOrigin.Package => "  (package)",
        _ => "",
    };

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
    public static string Member(string owner, string member) => member switch
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
    public static string TopPercent(double percentile) =>
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
    public static string PeerGroup(Cohort cohort, int size)
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
    public static string PeerGroupNoun(Cohort cohort)
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
    public static string ShortName(string cohortKey)
    {
        var afterPrefix = FullName(cohortKey);

        var lastDot = afterPrefix.LastIndexOf('.');
        return lastDot >= 0 ? afterPrefix[(lastDot + 1)..] : afterPrefix;
    }

    /// <summary>
    /// The cohort key with its basis prefix and <c>global::</c> removed, but nothing else — a
    /// namespace is only itself when it is complete.
    /// </summary>
    public static string FullName(string cohortKey)
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
    public static IEnumerable<string> Truncation(int total, int shown, string noun, string indent = "   ")
    {
        if (total <= shown) yield break;

        yield return $"{indent}({Plural(total - shown, noun)} not shown of {Whole(total)} — "
                     + $"raise --top to see {(total - shown == 1 ? "it" : "them")}.)";
    }

    /// <summary>Applies the display cap and reports what it cost, in one pass.</summary>
    public static (IReadOnlyList<T> Shown, IReadOnlyList<string> Disclosure) Cap<T>(
        IReadOnlyList<T> items, int limit, string noun, string indent = "   ")
    {
        if (items.Count <= limit) return (items, []);

        return (items.Take(limit).ToList(), Truncation(items.Count, limit, noun, indent).ToList());
    }
}

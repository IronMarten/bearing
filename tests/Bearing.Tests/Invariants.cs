namespace Bearing.Tests;

/// <summary>
/// Vocabulary an invariant forbids, in one place because two tests forbid it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariant 4 — never imply safety at a boundary.</b> <c>TECHREQ-job-a.md</c> §7 names it as a
/// Job A acceptance criterion and §5.6 forbids the word "dead" outright. Bearing cannot see
/// external consumers, so any sentence that reads as permission to remove something is a claim it
/// has no evidence for.
/// </para>
/// <para>
/// <b>It lives here because the list was about to be written twice.</b>
/// <c>NoStaticReferencesTests</c> holds it over the rendered surfaces and
/// <c>CyclesAndCouplingTests</c> holds it over the main-sequence zone wording, which no render can
/// reach. Two copies of a list is the failure this repository spends most of its documentation on:
/// the second copy goes stale, and here it would go stale by being the shorter one — a word
/// dropped from one list and not the other is a word that is forbidden in the report and allowed
/// in the label.
/// </para>
/// </remarks>
public static class Invariants
{
    /// <summary>The five strings no Bearing output may contain.</summary>
    public static readonly string[] SafetyVocabulary =
    [
        "safe to delete",
        "safe to remove",
        "dead code",
        "unused",
        "unreachable",
    ];

    /// <summary>The same list, for <c>[MemberData]</c>.</summary>
    public static TheoryData<string> ImplyingSafety
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var word in SafetyVocabulary) data.Add(word);
            return data;
        }
    }
}

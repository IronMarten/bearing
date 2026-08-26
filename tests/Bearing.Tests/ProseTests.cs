using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The report's English, checked as a property rather than by reading.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a property and not five fixes.</b> A sentence disagreeing with its own number has
/// now been found five times: one was three of them at once, and the count-verb defect is two more. Every previous
/// fix was applied to the site somebody happened to be looking at, and the entry recorded the prediction
/// out loud — <i>"the next such number is a defect waiting on the right input"</i>. It was right,
/// and the input was Umbraco.
/// </para>
/// <para>
/// <b>The helpers were never the problem.</b> <see cref="Sentences.Plural"/> and
/// <see cref="Sentences.Do"/> get the number right; each instance is a call site that hardcoded a
/// verb next to one. So a test of the helpers passes while the page is wrong, which is exactly what
/// happened four times, and the check has to run over <i>rendered output</i>.
/// </para>
/// <para>
/// <b>What the fixture can and cannot do here.</b> This asserts over TestBed on every build, which
/// is cheap and catches a regression the moment it is written. It does <b>not</b> prove the rules
/// hold on real solutions — TestBed has no unparseable file and does not cap its boundary list, so
/// neither known instance is reachable from it. That is what running the same rules over
/// nopCommerce, Jellyfin and Umbraco is for, and it is why <see cref="Prose"/> lives in the shipped
/// assembly rather than in this one.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ProseTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Neither renderer emits a sentence that disagrees with its own number.</summary>
    [Fact]
    public void Both_renderers_speak_english()
    {
        var findings = Analysis.FindingsFor(core.Model);

        var terminal = string.Join("\n", Report.For(core.Model, Analysis.Judge(core.Model)));
        var html = HtmlReport.Render(core.Model, Analysis.Judge(core.Model), Instant, full: true);

        foreach (var (surface, text) in new[] { ("terminal", terminal), ("html", html) })
        {
            var violations = Prose.Violations(text);

            Assert.True(
                violations.Count == 0,
                $"{surface}: " + string.Join(
                    "; ", violations.Select(v => $"[{v.Rule}] {v.Line}")));
        }
    }

    /// <summary>
    /// The rules catch what they were written for, on text that stands in for the real reports.
    /// </summary>
    /// <remarks>
    /// <b>A rule that cannot fail is worse than no rule</b> — <c>TECHREQ-job-b.md</c> §4's argument
    /// about suppression rows, applied to this file. TestBed reaches neither known instance, so
    /// without this the test above would pass on an empty check and go on passing if a regex were
    /// broken by an edit. The three strings are the verbatim lines from the Umbraco run that
    /// produced §55 and §59.
    /// </remarks>
    [Theory]
    [InlineData("   1 file could not be parsed and were not read:", "singular-count-plural-verb")]
    [InlineData("   2 projects did NOT resolve every reference it names, so edges are MISSING", "plural-count-singular-verb")]
    [InlineData("   3 files were not read and its contents are missing.", "plural-count-singular-verb")]
    [InlineData("     (39 boundarys not shown of 54 — raise --top to see them.)", "naive-plural")]
    [InlineData("   at Nerdbank.GitVersioning.VersionOracle..ctor(GitContext context)", "stack-frame")]
    public void The_rules_catch_what_they_were_written_for(string line, string rule)
    {
        var violation = Assert.Single(Prose.Violations(line));

        Assert.Equal(rule, violation.Rule);
    }

    /// <summary>
    /// And they do not fire on the sentences the report legitimately writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controls matter more than the catches: these rules are patterns over English, and one
    /// that flags correct prose gets weakened until it finds nothing. <c>1 type calls into it</c>
    /// and <c>only 1 type depends on it</c> are its own fixes, still correct on Umbraco.
    /// </para>
    /// <para>
    /// <b>The mirror rule's controls were not guessed — they are what it flagged.</b> Its first
    /// draft matched <b>98 lines</b> across both renderers on all three reference solutions, all of
    /// them correct, and every one turned on a bare object <c>it</c>. Its second flagged
    /// <i>"each has"</i>. Both narrowings are pinned below, because a rule narrowed for a reason
    /// that is not written down gets widened again by the next person who reads it.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("   FriendlyPublishedContentExtensions — 34 writes to static state, and 1 type calls into it.")]
    [InlineData("   QueuingEventDispatcherBase — only 1 type depends on it. Complex inside (cc 23) but isolated.")]
    [InlineData("   1 reference reaches SiteDomainMapper itself.")]
    [InlineData("     107 of this solution's 3,209 types sit in a group too small to compare them against")]
    [InlineData("   3 of them do still appear in the nominations above: the findings that need no cohort")]
    [InlineData("     Umbraco.Cms.Api.Management.ViewModels — 12 types, 4 of them entities")]
    // The six controls below are the mirror rule's, and each is a wording the corpus actually
    // carries. The first is why a bare "it" does not count: it is an OBJECT here, and admitting it
    // flagged 98 correct sentences across the three reference solutions. The second is why the
    // window stops at a new subject -- "has" agrees with "each", not with "11 kinds". The last is
    // the singular form of the sentence the rule was written for, which must stay silent.
    [InlineData("   SemVersion — 20 types depend on it, it depends on nothing.")]
    [InlineData("   The strongest row of the rarest kind. 11 kinds fired and each has one claim below.")]
    [InlineData("   ExamineEvents — 4 writes to static state, and 7 types call into it.")]
    [InlineData("   88% of this codebase has nothing said about it")]
    [InlineData("     System.Data                                3 types  (ships with .NET)")]
    [InlineData("   1 project did NOT resolve every type it names, so edges are MISSING")]
    public void The_rules_leave_correct_sentences_alone(string line) =>
        Assert.Empty(Prose.Violations(line));
}

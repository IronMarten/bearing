using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The shared wording — A13 tier 2's extraction.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole reason <c>Claims</c> exists is that two renderers were about to become three.</b>
/// So the assertion that matters is not what any one sentence says — the snapshots hold that — but
/// that the terminal and the page cannot say different things about one finding. A drifted sentence
/// is not a crash and not a wrong number; it is two artifacts from one run disagreeing, which is
/// the failure a reader has no way to diagnose and every reason to blame on the tool.
/// </para>
/// <para>
/// The per-kind wording is covered by <c>ReportTests</c> and <c>HtmlReportTests</c>, which snapshot
/// both renderers over the fixture. What is here is the properties those snapshots cannot state.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ClaimsTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    /// <summary>Every finding the fixture produces can be worded.</summary>
    /// <remarks>
    /// <b>A kind with no arm returns <c>Claim.None</c> and renders as nothing at all</b>, which is
    /// the silent-omission failure invariant 8 exists to prevent — a finding that fired, was
    /// suppressed by nobody, and then simply did not appear. The `switch` has no compiler
    /// obligation to be exhaustive, so this is what makes adding a kind without wording it a test
    /// failure rather than a gap in the output.
    /// </remarks>
    [Fact]
    public void Every_finding_the_fixture_makes_has_a_sentence()
    {
        foreach (var finding in Findings.All)
        {
            var claim = Claims.For(core.Model, finding);

            Assert.True(claim.Exists, $"{finding.Kind} on {finding.Subject.Canonical} words to nothing.");
            Assert.NotEqual("", claim.Subject);
            Assert.NotEqual("", claim.Sentence);
        }
    }

    /// <summary>Every kind is named and described, including the ones the fixture never produces.</summary>
    [Fact]
    public void Every_kind_is_named_and_described()
    {
        foreach (var kind in Enum.GetValues<FindingKind>())
        {
            Assert.NotEqual("", Claims.KindName(kind));
            Assert.NotEqual("", Claims.KindBlurb(kind));

            // The name is the reader's, not the enum's. A kind rendering as its own identifier is
            // docs/DEFECTS.md §27 — MaxMemberCyclomaticPctl printed at somebody — one level up.
            Assert.NotEqual(kind.ToString(), Claims.KindName(kind));
        }
    }

    /// <summary>
    /// The terminal and the page make the same claim about the same finding.
    /// </summary>
    /// <remarks>
    /// <b>The anti-drift assertion, and the reason the extraction happened at all.</b> Asserted
    /// through the rendered artifacts rather than by calling <c>Claims</c> twice, which would only
    /// prove that one function is deterministic: what is under test is that neither renderer has
    /// quietly grown a second copy of a sentence.
    /// </remarks>
    [Fact]
    public void The_terminal_and_the_page_word_a_lead_claim_identically()
    {
        var findings = Findings;
        var terminal = string.Join("\n", Report.For(core.Model, findings));
        var page = HtmlReport.Render(
            core.Model, findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var leading = Selection.Exemplars(findings).Where(f => Claims.IsRiskClaim(f.Kind)).ToList();

        Assert.NotEmpty(leading);

        foreach (var finding in leading)
        {
            var sentence = Claims.For(core.Model, finding).Sentence;

            Assert.Contains(sentence, terminal, StringComparison.Ordinal);
            Assert.Contains(Html.Text(sentence), page, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A verb agrees with a number a real solution made singular — <c>docs/DEFECTS.md</c> §32.
    /// </summary>
    /// <remarks>
    /// <b>Asserted as the property, because the fixture cannot reach any of the three cases.</b>
    /// TestBed has no shared-mutable-state type with exactly one caller and no contract with a
    /// one-field surface, so a test over the fixture's output would pass without exercising the
    /// fix — the same shape §24 was left in, and named here rather than assumed.
    /// </remarks>
    [Fact]
    public void A_count_of_one_takes_a_singular_verb()
    {
        Assert.Equal("calls", Sentences.Do(1, "calls", "call"));
        Assert.Equal("call", Sentences.Do(2, "calls", "call"));
        Assert.Equal("call", Sentences.Do(0, "calls", "call"));

        Assert.Equal("1 field/param", Sentences.Surface(1));
        Assert.Equal("2 fields/params", Sentences.Surface(2));
        Assert.Equal("0 fields/params", Sentences.Surface(0));
    }

    /// <summary>
    /// Coverage is a disclosure and is not led with as a risk.
    /// </summary>
    /// <remarks>
    /// It is invariant 8's record that a population got no comparative reading. Putting <i>"no peer
    /// group"</i> in a list headed <i>risk</i> asserts something about a type whose entire entry
    /// says nothing could be asserted about it. This does not narrow X10 — the selection still
    /// returns it, and the page still discloses the count.
    /// </remarks>
    [Fact]
    public void Coverage_is_selected_but_is_not_a_risk_claim()
    {
        var findings = Findings;

        Assert.False(Claims.IsRiskClaim(FindingKind.Coverage));
        Assert.All(
            Enum.GetValues<FindingKind>().Where(k => k != FindingKind.Coverage),
            k => Assert.True(Claims.IsRiskClaim(k)));

        // Still selected: the rule is unchanged, only where a renderer puts the result.
        if (findings.OfKind(FindingKind.Coverage).Count > 0)
            Assert.Contains(Selection.Exemplars(findings), f => f.Kind == FindingKind.Coverage);
    }
}

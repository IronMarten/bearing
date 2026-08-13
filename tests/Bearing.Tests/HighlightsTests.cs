using System.Text.RegularExpressions;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The risk highlights — A13 tier 2.
/// </summary>
/// <remarks>
/// What a test can hold is the honesty of the selection, not whether it helps: whether it helps is
/// A11 round 2's question and no assertion reaches it. So these cover the four things that would
/// make the section dishonest — leading with a kind that is not a claim, leading with something
/// other than the strongest row, hiding how much was left out, and presenting an ordering as a
/// severity.
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class HighlightsTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    private string Terminal => string.Join("\n", Report.For(core.Model, Findings));

    private string Page => HtmlReport.Render(
        core.Model, Findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Both renderers lead with every risk kind that fired, and with no other.</summary>
    [Fact]
    public void Both_renderers_lead_with_one_claim_per_risk_kind()
    {
        var findings = Findings;

        var expected = findings.All
            .Select(f => f.Kind)
            .Where(Claims.IsRiskClaim)
            .Distinct()
            .Count();

        Assert.Equal(expected, Regex.Matches(Page, """<div class="card lead">""").Count);
        Assert.Contains($"{expected} claims, one for each kind of risk this run found", Terminal, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lead item of each kind is that kind's strongest row, not a re-pick.
    /// </summary>
    /// <remarks>
    /// <c>SelectionTests</c> holds that <c>Exemplars</c> takes the head; this holds that the section
    /// renders what it was given. A renderer that re-sorted would produce a defensible-looking list
    /// with no rule behind it, which is the thing X10 spent a decision refusing.
    /// </remarks>
    [Fact]
    public void The_lead_of_each_kind_is_that_kinds_first_emitted_finding()
    {
        var findings = Findings;
        var start = Terminal.IndexOf("-- START HERE", StringComparison.Ordinal);
        var end = Terminal.IndexOf("-- CONCEALED DECISION", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var section = Terminal[start..end];

        foreach (var finding in Selection.Exemplars(findings).Where(f => Claims.IsRiskClaim(f.Kind)))
            Assert.Contains(Claims.For(core.Model, findings.OfKind(finding.Kind)[0]).Sentence,
                section, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every kind says how many of it there are.
    /// </summary>
    /// <remarks>
    /// <b>A lead item with no count reads as the only one of its kind.</b> On nopCommerce that is
    /// true of layer span and false of the 1,091 concealed decisions, and telling those two apart
    /// is the entire triage this section performs. <c>PRD-free-tier.md</c> §9's anti-metric is that
    /// more findings is worse, which makes a large count a thing to state rather than to soften.
    /// </remarks>
    [Fact]
    public void Every_lead_says_how_many_more_there_are()
    {
        var findings = Findings;

        foreach (var finding in Selection.Exemplars(findings).Where(f => Claims.IsRiskClaim(f.Kind)))
        {
            var total = findings.OfKind(finding.Kind).Count;

            var terminal = total == 1 ? "and it is the only one" : $"1 of {total}";
            var page = total == 1
                ? "This is the only one in this codebase."
                : $"{total} of these were found; this is the strongest.";

            Assert.Contains(terminal, Terminal, StringComparison.Ordinal);
            Assert.Contains(page, Page, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The ordering is stated wherever the list is shown.
    /// </summary>
    /// <remarks>
    /// X10: <i>"the ordering is stated in the text, because a top-down list reads as ranked whatever
    /// the model believes, and rarity is not severity."</i> The sentence is the deliverable, so its
    /// absence is a defect and not a wording preference.
    /// </remarks>
    [Fact]
    public void The_ordering_is_stated_in_both_renderers()
    {
        const string Ordering = "ordered by how uncommon each kind is in this codebase";

        Assert.Contains(Ordering, Terminal, StringComparison.Ordinal);
        Assert.Contains(Ordering, Page, StringComparison.Ordinal);

        // And says what it is not. An order with no disclaimer is read as a ranking.
        Assert.Contains("not a severity", Terminal, StringComparison.Ordinal);
        Assert.Contains("not a severity", Page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The default page does not enumerate, and <c>--full</c> does.
    /// </summary>
    /// <remarks>
    /// <b>Tier 4, and the count is the point.</b> The enumeration is what A11 round 1 called "a wall
    /// of text"; it still ships, behind a flag, and the default page has to say where it went — a
    /// page quietly showing nine findings of 1,642 would be <c>docs/DEFECTS.md</c> §3 at the scale
    /// of a whole artifact.
    /// </remarks>
    [Fact]
    public void The_enumeration_moves_behind_a_flag_and_the_page_says_so()
    {
        var findings = Findings;
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var brief = HtmlReport.Render(core.Model, findings, at);
        var full = HtmlReport.Render(core.Model, findings, at, full: true);

        Assert.Contains("<h2>Everything else</h2>", brief, StringComparison.Ordinal);
        Assert.Contains("--full", brief, StringComparison.Ordinal);
        Assert.Contains($"{findings.Count} findings", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Findings</h2>", brief, StringComparison.Ordinal);

        Assert.Contains("<h2>Findings</h2>", full, StringComparison.Ordinal);
        Assert.Contains("<h2>Components named above</h2>", full, StringComparison.Ordinal);
        Assert.True(full.Length > brief.Length);
    }

    /// <summary>
    /// The coverage disclosure survives the enumeration moving.
    /// </summary>
    /// <remarks>
    /// Invariant 8 is the one thing a shorter report is not allowed to buy its brevity with. The
    /// count of types that got no peer reading is on the default page, in prose, whether or not the
    /// section that listed them is rendered.
    /// </remarks>
    [Fact]
    public void What_could_not_be_judged_is_disclosed_without_the_enumeration()
    {
        var findings = Findings;
        var coverage = findings.OfKind(FindingKind.Coverage);

        if (coverage.Count == 0) return;

        var brief = HtmlReport.Render(
            core.Model, findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains($"{coverage.Count} of this solution's", brief, StringComparison.Ordinal);
        Assert.Contains("too small to compare them against", brief, StringComparison.Ordinal);
    }
}

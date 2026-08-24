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

    /// <summary>
    /// No sentence puts an "x" after a ratio that does not exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §38. A ratio against a zero peer median is undefined, and
    /// <c>Sentences.Number</c> renders it as the word <i>undefined</i> — which is right, and which
    /// two call sites then followed with a literal <c>x</c>. Concealed decision branches for it;
    /// blast radius did not, and shipped <i>"89 distinct callers (undefinedx its peer median)"</i>
    /// on nopCommerce's <c>BaseController</c> — in the frozen A11 round 2 materials, until this.
    /// </para>
    /// <para>
    /// <b>Asserted on constructed findings because the fixture cannot reach it.</b> A cohort whose
    /// fan-in median is zero and which still clears blast radius needs a shape TestBed does not
    /// have, and building one to protect a sentence would be a large plant for a small branch —
    /// P5 was discarded for that reason. What a synthetic finding cannot do is prove the detector
    /// produces such a value; what it can do is prove the renderer survives one, which is the half
    /// that was broken.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(FindingKind.BugBlastRadius, "FanInXMedian")]
    [InlineData(FindingKind.ConcealedDecisionType, "MaxMemberCyclomaticXMedian")]
    public void An_undefined_ratio_never_renders_as_a_multiple(FindingKind kind, string ratio)
    {
        var subject = core.Model.Types.Single(t => t.Name == "ShipmentLedger");

        var finding = new Finding(
            new FindingKey(kind, subject.Subject),
            [
                Receipt.Gated(ratio, double.PositiveInfinity, nameof(AnalysisPolicy.OutlierFactor)),
                Receipt.Of("MedianCohortCyclomatic", 0),
                Receipt.Gated("CohortSize", 6, nameof(AnalysisPolicy.MinCohort)),
            ],
            [],
            []);

        var claim = Claims.For(core.Model, finding);

        Assert.NotEqual(Claim.None, claim);
        Assert.DoesNotContain("undefinedx", claim.Sentence, StringComparison.Ordinal);

        // And not by dropping the word either — "0x" or "∞x" would each be a measurement the
        // tool cannot support, which is the failure D28 fixed one level down.
        Assert.DoesNotContain("∞", claim.Sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", claim.Sentence, StringComparison.Ordinal);
    }

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
        // Every kind that competes, which is every kind this pane is made of. The cycle kinds
        // render in Circular references and have no card, so requiring a Claim of them would be
        // requiring a sentence for a shape that is never asked for one — and writing three to
        // satisfy this would put unread prose in Claims.For for a test to find.
        foreach (var finding in Findings.All.Where(f => Claims.CompetesForLead(f.Kind)))
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

    /// <summary>
    /// Layer span's evidence is the per-kind breakdown, and it counts what the section names.
    /// </summary>
    /// <remarks>
    /// <b><c>TECHREQ-job-b.md</c> §3.1 makes the breakdown the finding</b>, and the claim record
    /// carried none of it until A13 tier 3 enlarged one card and found the numbers missing from
    /// under it. Asserted against the participants rather than against a literal, so a claim that
    /// started counting something else — references instead of distinct types, or every
    /// participant regardless of kind — fails here rather than reading plausibly on a page.
    /// </remarks>
    [Fact]
    public void Layer_span_carries_the_kinds_it_reaches_and_how_many_of_each()
    {
        var found = Findings.OfKind(FindingKind.SpansArchitecturalLayers);

        Assert.NotEmpty(found);

        foreach (var finding in found)
        {
            var claim = Claims.For(core.Model, finding);

            var byKind = finding.Participants
                .Select(core.Model.Find)
                .Where(t => t is not null)
                .GroupBy(t => t!.Classification.Kind, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(t => t!.Name).Distinct(StringComparer.Ordinal).Count());

            foreach (var (kind, count) in byKind)
                Assert.Contains($"{count} {kind}", claim.Evidence, StringComparison.Ordinal);

            // The type's own role is stated as itself, never as a count — a component is not one
            // of its own dependencies, and "1 ApiBoundary" for the type in hand would be a lie a
            // reader could not check.
            if ((finding.ValueOf("KindSpan") ?? 0) > byKind.Count)
                Assert.Contains(
                    $"{core.Model.Find(finding.Subject)!.Classification.Kind} itself",
                    claim.Evidence, StringComparison.Ordinal);
        }
    }
}

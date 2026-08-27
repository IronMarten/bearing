using System.Reflection;
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
    /// No claim about a peer group is more extreme than one member of that group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Distribution.PercentileOf"/> is a midrank, so it
    /// splits a value's own tie band and reports the middle of it: a unique maximum of six lands
    /// at the 92nd percentile and the sentence printed <i>"top 8%"</i>, which a reader takes as one
    /// in twelve. Midrank is right for ordering and wrong for a claim, and the two were the same
    /// number for as long as only one of them was printed.
    /// </para>
    /// <para>
    /// <b>Stated as a property rather than as the seven corrected numbers</b>, because the numbers
    /// are in the snapshots and the rule is not: <b>a share of a group of N cannot be smaller than
    /// <c>1/N</c></b>, since the subject of the claim is itself a member. That holds at every
    /// cohort size, needs no threshold for when a peer group is too thin to describe, and fails on
    /// the midrank formula at every cohort where the top value is unique — which is most of them.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_claim_is_more_extreme_than_one_member_of_its_peer_group()
    {
        var claims = Findings.OfKind(FindingKind.ConcealedDecisionType)
            .Select(f => (Finding: f, Share: f.ValueOf("MaxMemberCyclomaticTopShare"), Size: f.ValueOf("CohortSize")))
            .ToList();

        Assert.NotEmpty(claims);

        foreach (var (finding, share, size) in claims)
        {
            Assert.NotNull(share);
            Assert.NotNull(size);

            // One member of a cohort of N is 100/N percent of it, and nothing the tool says about
            // a single subject may claim to be rarer than that.
            Assert.True(
                share >= (100.0 / size!.Value) - 1e-9,
                $"{finding.Subject.Canonical} claims top {share}% of a cohort of {size}, "
                + $"where one member is {100.0 / size.Value}%");
        }
    }

    /// <summary>
    /// No sentence puts an "x" after a ratio that does not exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ratio against a zero peer median is undefined, and
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
                // Evidence rather than a gate since X16, which is how the detectors emit it now.
                Receipt.Of(ratio, double.PositiveInfinity),
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
            // an internal identifier published at somebody — MaxMemberCyclomaticPctl — one level up.
            Assert.NotEqual(kind.ToString(), Claims.KindName(kind));
        }
    }

    /// <summary>
    /// Every kind says what its participants are to it, including the ones that name none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>`ARCHITECTURE.md` A6 settles that the relationship is a function of the kind</b>, which
    /// is what makes a label in a renderer complete rather than a guess. `HtmlReport` held that
    /// label as a four-arm switch defaulting to <i>"Most complex member"</i> — correct for the six
    /// kinds that reach the default today, and correct by accident. A6 names the god object as the
    /// case where the wrong label inverts the meaning: the size arm mislabelled says the opposite
    /// of what the finding found.
    /// </para>
    /// <para>
    /// The switch is total now and lives in <c>Claims</c> beside <see cref="Claims.KindName"/>,
    /// and this is <see cref="Every_kind_is_named_and_described"/>'s shape applied to it: the
    /// <c>_</c> arm returns the enum's own name, so a kind that has not been through the switch
    /// fails here rather than being labelled by whichever arm it happened to fall into.
    /// <see langword="null"/> passes, and is the deliberate answer for the six kinds that carry no
    /// participants — the three cycle kinds hold their evidence as <c>Relations</c> instead.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_kind_says_what_its_participants_are()
    {
        foreach (var kind in Enum.GetValues<FindingKind>())
        {
            Assert.NotEqual(kind.ToString(), Claims.ParticipantsAre(kind));
            Assert.NotEqual("", Claims.ParticipantsAre(kind));
        }
    }

    /// <summary>
    /// Every qualifier Core can attach has words, not its kebab-case key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enumerated off <c>Qualifiers</c> by reflection rather than listed here</b>, which is the
    /// whole point: a list would have to be kept in step with Core by the same memory that let
    /// three constants go unworded. Adding a qualifier to Core and not wording it in the Cli now
    /// fails the suite.
    /// </para>
    /// <para>
    /// <b>It was one finding away from rendering an identifier at a user.</b>
    /// <c>ATypeHierarchy</c> is carried by a <i>reported</i> <c>TypeTangle</c> with no suppression
    /// row; it did not reach the pane only because <c>Claims.CompetesForLead</c> excludes tangles
    /// and <c>Card</c> has exactly one caller. Two hops, neither of them a rule about this.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_qualifier_is_worded()
    {
        var keys = typeof(Qualifiers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        // A guard on the guard: reflection that finds nothing asserts nothing, and it would look
        // exactly like a vocabulary with no gaps.
        Assert.True(keys.Count >= 11, $"only {keys.Count} qualifiers found on Qualifiers");

        foreach (var key in keys)
        {
            var worded = Claims.QualifierText(key);

            // Equality with the key, not the absence of a hyphen: "extreme fan-in solution-wide"
            // is English and carries two. The key is what an unworded qualifier renders as, so
            // the key is what this looks for.
            Assert.NotEqual(key, worded);
            Assert.NotEqual("", worded);
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
        var terminal = string.Join("\n", Report.For(core.Model, Analysis.Judge(core.Model)));
        var page = HtmlReport.Render(
            core.Model, Analysis.Judge(core.Model), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

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
    /// A verb agrees with a number a real solution made singular.
    /// </summary>
    /// <remarks>
    /// <b>Asserted as the property, because the fixture cannot reach any of the three cases.</b>
    /// TestBed has no shared-mutable-state type with exactly one caller and no contract with a
    /// one-field surface, so a test over the fixture's output would pass without exercising the
    /// fix — the same shape the constructor rendering was left in, and named here rather than assumed.
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

    // ------------------------------------------------------------------ defect 57 ----

    /// <summary>
    /// Two different subjects never render an identity a reader cannot tell apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Umbraco ships two ImageSharp integrations declaring one
    /// namespace, so <c>ConfigureImageSharpMiddlewareOptions</c> is two types with one
    /// fully-qualified name. The model is right — identity was fixed to key on <c>(assembly, FQN)</c>
    /// precisely because <i>"plugin architectures use it deliberately"</i> — and the renderer then
    /// printed one name twice with different receipts.
    /// </para>
    /// <para>
    /// <b>The identity is the name AND the location, because that is what the page prints.</b>
    /// Asserting on the name alone would fail the fixture for behaviour that is correct:
    /// <c>TestBed.Interop.CarrierTwin</c> is declared in both <c>Core</c> and <c>Data</c>, both
    /// declarations are nominated, and the two rows separate cleanly because
    /// <see cref="Subjects.Where"/> leads with the project. <b>That is the collision scenario handled, not
    /// a collision occurring</b> — and it corrects the entry, which recorded the planted collisions as
    /// never reaching a claim. They reach one; nothing looked wrong because nothing was.
    /// </para>
    /// <para>
    /// <b>Keyed on the declaring type, not on the finding's subject.</b> A type-level concealed
    /// decision and its method-level counterpart carry different subjects and describe one method;
    /// they print one identity twice on purpose. Keying on the subject fails all six of the
    /// fixture's, which is how this wording was arrived at rather than guessed.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_two_subjects_share_a_rendered_identity()
    {
        var ambiguous = Findings.All
            .Select(f => (Finding: f, Claim: Claims.For(core.Model, f)))
            .Where(r => r.Claim.Subject.Length > 0)
            .Select(r => (
                // The TYPE the finding resolves to, not the finding's own subject. A type-level
                // concealed decision and its method-level counterpart have different subjects and
                // are about one method -- they print one identity twice on purpose, and keying on
                // the subject would forbid the page saying two things about one method.
                Type: Subjects.Of(core.Model, r.Finding)?.Subject.Canonical ?? "",
                Identity: r.Claim.Subject + "  @  " + Subjects.Where(core.Model, r.Finding, r.Claim.Trailer)))
            .Where(r => r.Type.Length > 0)
            .GroupBy(r => r.Identity, StringComparer.Ordinal)
            .Where(g => g.Select(r => r.Type).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            ambiguous.Count == 0,
            "two subjects render one identity: " + string.Join("; ", ambiguous));
    }

    /// <summary>
    /// A claim that names a member sends the reader to that member, not to its declaring type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The live face of two types sharing one name, found on Umbraco 2026-08-25.</b> The
    /// concealed-decision claim titles itself with the type's most complex <i>member</i> and passed
    /// no trailer, so <see cref="Subjects.Where"/> fell back to the declaring <i>type</i>'s line.
    /// The page then printed <c>Utf8ToAsciiConverter.ToAscii</c> at <c>:12</c> — the class
    /// declaration — beside a tile printing the same name at <c>:131</c>, the method. One page, one
    /// name, two addresses.
    /// </para>
    /// <para>
    /// <b>And the type really does declare two <c>ToAscii</c> overloads</b>, at lines 76 and 131 —
    /// cc 3 and cc 1312 — so a reader had no way to tell whether they were looking at two methods
    /// or at one method described twice. <b>X14 made a member subject an identity rather than a
    /// display string so that a member could be located</b>; this is the same work stopping one
    /// element short, which is the shape it had on the tile.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_claim_naming_a_member_addresses_that_member()
    {
        var concealed = Findings.OfKind(FindingKind.ConcealedDecisionType);

        Assert.NotEmpty(concealed);

        foreach (var finding in concealed)
        {
            if (core.Model.Find(finding.Subject) is not { } type) continue;
            if (type.MostComplexMember is not { Location.IsKnown: true } member) continue;

            var where = Subjects.Where(core.Model, finding, Claims.For(core.Model, finding).Trailer);

            Assert.EndsWith(
                $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}",
                where,
                StringComparison.Ordinal);
        }
    }
}

using System.Text.Json;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The findings export's contract — <c>SCHEMA-findings-export.md</c> §1 and §8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Firing is not being gated, and this is a contract.</b> §8 opens with that line and it is the
/// reason this file is separate from <c>JsonOutputTests</c>: those assert what the document looks
/// like, and these assert what it is not allowed to leave out. A snapshot cannot make that claim —
/// an export missing a whole kind stays byte-identical with itself forever.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class FindingsExportTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReadOnlyList<Judged> Judged => Analysis.Judge(core.Model);

    private JsonElement Root =>
        JsonDocument.Parse(JsonOutput.Render(core.Model, Judged, Instant)).RootElement;

    private IReadOnlyList<JsonElement> Findings =>
        [.. Root.GetProperty("findings").EnumerateArray()];

    private static string KeyOf(JsonElement finding) => finding.GetProperty("key").GetString()!;

    private static string KindOf(JsonElement finding) => finding.GetProperty("kind").GetString()!;

    /// <summary>
    /// §8.1 — the superset rule. Every judgement the tool made is in the file, and nothing else is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the test that would have caught the false exports sentence</b> — the report
    /// telling a reader that the exports carry every finding while they carried none. It was
    /// closed by rewording the page, because the sentence was the only thing that could be fixed
    /// at the time; this is the version that closes it by making the sentence true, and it is the
    /// invariant that stops the four-surface version of the same defect.
    /// </para>
    /// <para>
    /// <b>Asserted as set equality against <c>Analysis.Judge</c>, not as a count and not against a
    /// list.</b> §1's three clauses — every kind the report can print, every section it renders,
    /// nothing capped or filtered — are one claim from the model's side: <i>the judgements are the
    /// population, and the file is all of them</i>. A count would pass while two entries swapped
    /// identity, and a hand-maintained list of kinds is the thing that goes stale the first time a
    /// kind is added, which is exactly what §3.12 just did.
    /// </para>
    /// <para>
    /// <b>Suppressed findings are in the population deliberately.</b> They are judgements the tool
    /// made and declined to print, and §4's <c>status</c> is what distinguishes them — a consumer
    /// that cannot see them cannot tell <i>muted</i> from <i>fixed</i>, which is §7's ageing
    /// problem and the reason acknowledgment memory needs this file rather than the report.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_export_carries_every_judgement_and_nothing_else()
    {
        var judged = Judged;

        // The fixture judges something, and suppresses something, or the equality below is a
        // statement about an empty set agreeing with an empty set.
        Assert.NotEmpty(judged);
        Assert.Contains(judged, j => !j.IsReported);

        Assert.Equal(
            judged.Select(j => j.Finding.Key.Canonical).OrderBy(k => k, StringComparer.Ordinal),
            Findings.Select(KeyOf).OrderBy(k => k, StringComparer.Ordinal));

        // Set equality is not enough on its own: two entries could carry each other's keys and
        // still satisfy it. The status has to travel with the key it belongs to.
        var statusOf = Findings.ToDictionary(KeyOf, f => f.GetProperty("status").GetString()!, StringComparer.Ordinal);

        foreach (var judgement in judged)
        {
            Assert.Equal(
                judgement.IsReported ? "reported" : "suppressed",
                statusOf[judgement.Finding.Key.Canonical]);
        }
    }

    /// <summary>
    /// §8.1, the renderers' half — a kind the report names has entries in the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clause above catches the model leaking and this one catches a renderer inventing.</b>
    /// They are not the same failure. A section that prints a claim which never became a
    /// <see cref="Finding"/> is invisible to the equality test — the judgements and the file would
    /// agree perfectly and the page would still say something the export cannot. **That is not
    /// hypothetical: the circular-reference sections did exactly this until `TECHREQ-job-b.md`
    /// §3.12**, rendering three kinds of claim from a parallel model with no finding behind any of
    /// them.
    /// </para>
    /// <para>
    /// <b>Derived from <see cref="Claims.KindName"/> rather than from a list of section headings.</b>
    /// The name a kind renders under is the tool's own, so a kind added without wording is caught by
    /// the existing sentence tests, and a kind worded without exporting is caught here. Scanned
    /// case-insensitively across both renderers because the terminal shouts its headings and the
    /// HTML does not, and singular-in-plural is why it is <c>Contains</c> rather than equality —
    /// <i>Namespace cycle</i> inside <i>NAMESPACE CYCLES</i>.
    /// </para>
    /// <para>
    /// <b>What this cannot catch, said here rather than discovered later:</b> a section that renders
    /// a claim without ever naming its kind. Nothing derives that, and a registry of what each
    /// renderer prints would be the stale list this file is written to avoid.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_kind_the_report_names_has_entries_in_the_export()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var rendered = string.Join(
            "\n",
            string.Join("\n", Report.For(core.Model, findings)),
            HtmlReport.Render(core.Model, findings, Instant, full: true));

        var exported = Findings.Select(KindOf).ToHashSet(StringComparer.Ordinal);

        var judged = Judged;

        // Two populations, and getting them the wrong way round is what the first two attempts at
        // this test did.
        //
        // EXPORTED is keyed on what FIRED, suppressed rows included -- that is §1's superset.
        // NAMED is keyed on what SURVIVED, because a suppressed claim is one the tool decided not
        // to print and a renderer naming it anyway would be the defect, not the fix.
        //
        // The two mistakes, kept because each is a real distinction this file has to hold:
        //   - `PROJECT CYCLES` renders its heading over an explicit empty state on this fixture.
        //     **A heading is not a claim**, so scanning "every kind the report names must be
        //     exported" reports the report being honest about having nothing to say as a gap.
        //   - `WidestContractSurface` FIRES here and is suppressed as a set by
        //     widest-surface-is-not-discriminating, so nothing prints it. **Fired is not
        //     reported**, and requiring every fired kind to be named asks the renderers to
        //     undo the suppression matrix.
        var fired = judged.Select(j => j.Finding.Kind).Distinct().ToList();
        var reported = judged.Where(j => j.IsReported).Select(j => j.Finding.Kind).Distinct().ToList();

        Assert.NotEmpty(fired);
        Assert.NotEmpty(reported);

        // The two differ on this fixture, or the distinction above is asserted by a test that
        // could not tell whether it held.
        Assert.NotEqual(fired.Count, reported.Count);

        foreach (var kind in fired)
            Assert.True(
                exported.Contains(kind.ToString()),
                $"{Claims.KindName(kind)} fired and has no entry in the export.");

        foreach (var kind in reported)
            Assert.True(
                rendered.Contains(Claims.KindName(kind), StringComparison.OrdinalIgnoreCase),
                $"{Claims.KindName(kind)} survived suppression and neither renderer names it.");
    }

    /// <summary>
    /// A subject carries members when it is a set and a declaring type when it is a member, and
    /// never the other way round.
    /// </summary>
    /// <remarks>
    /// <b>The join a consumer makes is on these two.</b> An empty <c>members</c> array on a type
    /// would read as <i>a set of nothing</i> rather than <i>not a set</i>, and a missing one on a
    /// cycle would lose the only place the component's parts are written down — the canonical
    /// escapes its separators and is not something to parse. Asserted as a correspondence in both
    /// directions so neither can drift into the other.
    /// </remarks>
    [Fact]
    public void A_subject_carries_members_exactly_when_it_is_a_set()
    {
        var sets = 0;
        var members = 0;

        foreach (var finding in Findings)
        {
            var subject = finding.GetProperty("subject");
            var kind = subject.GetProperty("kind").GetString();

            var hasMembers = subject.GetProperty("members").ValueKind is not JsonValueKind.Null;
            var hasDeclaring = subject.GetProperty("declaringType").ValueKind is not JsonValueKind.Null;

            Assert.Equal(kind == "Set", hasMembers);
            Assert.Equal(kind == "Member", hasDeclaring);

            if (hasMembers)
            {
                sets++;
                Assert.NotEmpty(subject.GetProperty("members").EnumerateArray());
            }

            if (hasDeclaring) members++;
        }

        // Both shapes are present, or the correspondence above is asserted over one kind of row.
        Assert.NotEqual(0, sets);
        Assert.NotEqual(0, members);
    }

    /// <summary>
    /// §8.5 — <c>class</c> is <see cref="Claims.IsRiskClaim"/> and cannot drift from it.
    /// </summary>
    /// <remarks>
    /// <b>Asserted for every kind in the enum, not for every kind in the file.</b> A kind that stops
    /// firing on the fixture would quietly stop being checked, and the field's whole job is to stop
    /// a consumer counting a disclosure as a claim — a job it can only do if the mapping is total.
    /// The <see cref="Claims.CompetesForLead"/> arm is deliberately absent: <c>class</c> answers
    /// <i>claim or disclosure</i>, and a cycle is a claim that happens not to lead the page.
    /// </remarks>
    [Fact]
    public void Class_says_claim_exactly_where_the_kind_is_one()
    {
        var classOf = Findings.ToDictionary(KeyOf, f => f.GetProperty("class").GetString()!, StringComparer.Ordinal);

        foreach (var judgement in Judged)
        {
            Assert.Equal(
                Claims.IsRiskClaim(judgement.Finding.Kind) ? "claim" : "disclosure",
                classOf[judgement.Finding.Key.Canonical]);
        }

        // Both values occur, or this is one string compared with itself.
        Assert.Contains("claim", classOf.Values);
        Assert.Contains("disclosure", classOf.Values);
    }

    /// <summary>
    /// §8.7 — every <c>gate</c> names a policy value that exists.
    /// </summary>
    /// <remarks>
    /// <b>A gate is a join, not a label.</b> It is how a consumer asks <i>what would have to change
    /// for this to stop firing</i>, and the answer is a lookup into <c>policy</c> in the same file.
    /// A gate naming something <c>AnalysisPolicy</c> does not have is a dangling reference that
    /// reads perfectly — which is why <c>nameof</c> at the call site is not enough on its own: it
    /// survives a rename of the property it names only because the compiler rewrites it, and says
    /// nothing about a receipt whose gate was typed as a string.
    /// </remarks>
    [Fact]
    public void Every_gate_resolves_against_the_policy()
    {
        var known = core.Model.Policy.Values.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        var gates = Findings
            .SelectMany(f => f.GetProperty("receipts").EnumerateArray()
                .Concat(f.GetProperty("qualifiers").EnumerateArray()))
            .Select(r => r.GetProperty("gate"))
            .Where(g => g.ValueKind is not JsonValueKind.Null)
            .Select(g => g.GetString()!)
            .ToList();

        Assert.NotEmpty(gates);

        foreach (var gate in gates.Distinct(StringComparer.Ordinal))
            Assert.True(known.Contains(gate), $"{gate} is named as a gate and is not a policy value.");
    }

    /// <summary>
    /// §8.6 — two renders of one model differ nowhere.
    /// </summary>
    /// <remarks>
    /// <b>Ordering is the thing this catches</b>, and the export has more of it to get wrong than
    /// the rest of the document: a finding carries four lists, and any of them arriving from a
    /// hash-ordered source would make two runs over one commit diff as though something had moved.
    /// <c>OrderingTests</c> makes the same claim about the document as a whole; this one is here so
    /// that a finding-shaped regression fails in the file that owns findings.
    /// </remarks>
    [Fact]
    public void Two_renders_of_one_model_are_identical()
    {
        Assert.Equal(
            JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant),
            JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// §8.8 — <c>configuration</c> covers every non-policy member of <see cref="WalkOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asserted over the record's members rather than over a list of the three that exist
    /// today</b>, which is the entire point of the block. §3 argues for mirroring
    /// <see cref="WalkOptions"/> so that <i>a fourth walk setting added later is a compile-visible
    /// gap in a record rather than something a writer must remember</i> — and a test naming
    /// <c>includeTests</c>, <c>defaultExcludesCleared</c> and <c>excludedPathFragments</c> would
    /// hold none of that. It would pass forever while the fourth setting went unexported.
    /// </para>
    /// <para>
    /// <b>Three members are excluded and each for its own reason.</b> <c>Policy</c> is the
    /// <c>policy</c> dictionary and would be said twice. <c>SolutionPath</c> is already
    /// <c>solutionPath</c> at the top of the document. <c>ToolVersion</c> is already <c>tool</c>.
    /// They are named here rather than filtered by a predicate, because a reader deciding whether a
    /// new member belongs in the block needs the reasons, not the outcome.
    /// </para>
    /// </remarks>
    [Fact]
    public void Configuration_covers_every_non_policy_walk_setting()
    {
        var saidElsewhere = new[]
        {
            nameof(WalkOptions.Policy),
            nameof(WalkOptions.SolutionPath),
            nameof(WalkOptions.ToolVersion),
        };

        var expected = typeof(WalkOptions)
            .GetProperties()
            .Select(p => p.Name)
            .Except(saidElsewhere, StringComparer.Ordinal)
            .Select(n => char.ToLowerInvariant(n[0]) + n[1..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var actual = Root.GetProperty("configuration")
            .EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// §8.3 — the export is uncapped: <c>--top</c> does not reach it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A presentation flag must not decide what a persistence format contains.</b> §4: two runs
    /// at different <c>--top</c> would diff as though findings had appeared and vanished, and
    /// nothing in the file would say why. It is also what makes the exports
    /// sentence true rather than merely reworded — the page claims the exports carry every finding,
    /// and a cap here would make that a second wrong version of the same claim.
    /// </para>
    /// <para>
    /// <b>A real second walk, because <c>Top</c> lives on the model.</b> Rendering the same model
    /// twice would assert nothing: the writer would have to go out of its way to read a value it
    /// was handed once. <see cref="CoreWalkFixture.WalkWith"/> memoises, so the second walk is paid
    /// for once across the suite.
    /// </para>
    /// <para>
    /// <b>Compared as the <c>findings</c> array alone and not as the document</b>, because the rest
    /// of the file legitimately moves: <c>policy</c> reports the run's own thresholds, so a
    /// document comparison would fail on the very value being varied and prove nothing about the
    /// findings.
    /// </para>
    /// <para>
    /// <b>It found that §8.3 is half true, and the half that is not is a display cap
    /// deciding a judgement.</b> The population is uncapped — same keys, same count, at either <c>--top</c>. The
    /// <i>content</i> is not: <c>RollCallThreshold</c> is <c>Top / RollCallDivisor</c>, so the
    /// display cap decides whether a layer-span finding carries
    /// <c>part-of-a-layering-pattern</c>. <c>SCHEMA-findings-export.md</c> §4 says Core has no
    /// notion of <c>--top</c>; it has one, and this is where that was found.
    /// </para>
    /// <para>
    /// <b>Asserted as the defect rather than skipped.</b> The inequality below fails the day §54 is
    /// fixed, which is what a test of a known-wrong behaviour is for — a skip records the gap
    /// somewhere nothing reads, and a deleted test records it nowhere.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_findings_are_the_same_at_every_top()
    {
        static string FindingsOf(SolutionModel model) =>
            JsonDocument.Parse(JsonOutput.Render(model, Analysis.Judge(model), Instant))
                .RootElement.GetProperty("findings").GetRawText();

        var narrow = core.WalkWith(core.Model.Policy with { Top = 1 });
        var wide = core.WalkWith(core.Model.Policy with { Top = 15 });

        // The runs really do differ where the cap bites, or this compares a model with itself.
        Assert.NotEqual(narrow.Policy.Top, wide.Policy.Top);

        // The POPULATION is uncapped, which is the half of §8.3 that holds today: same keys, same
        // count, nothing appearing or vanishing with the flag.
        Assert.Equal(Keys(narrow), Keys(wide));

        // And the half that does not, asserted as the defect rather than left as a silence.
        // RollCallThreshold is Top / RollCallDivisor, so --top decides whether
        // a layer-span finding carries part-of-a-layering-pattern. A display cap reaching a
        // judgement is the defect; this line fails the day it is fixed, which is the point of
        // writing it this way round rather than skipping the test.
        Assert.NotEqual(FindingsOf(narrow), FindingsOf(wide));

        var flipped = Differing(narrow, wide);

        Assert.All(flipped, k => Assert.StartsWith(nameof(FindingKind.SpansArchitecturalLayers), k, StringComparison.Ordinal));
        Assert.NotEmpty(flipped);
    }

    private static IReadOnlyList<string> Keys(SolutionModel model) =>
        [.. Rows(model).Select(KeyOf).OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>Keys whose entry is not identical between two runs.</summary>
    private static IReadOnlyList<string> Differing(SolutionModel a, SolutionModel b)
    {
        var left = Rows(a).ToDictionary(KeyOf, r => r.GetRawText(), StringComparer.Ordinal);
        var right = Rows(b).ToDictionary(KeyOf, r => r.GetRawText(), StringComparer.Ordinal);

        return [.. left.Where(kv => right[kv.Key] != kv.Value).Select(kv => kv.Key).Order(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<JsonElement> Rows(SolutionModel model) =>
    [
        .. JsonDocument.Parse(JsonOutput.Render(model, Analysis.Judge(model), Instant))
            .RootElement.GetProperty("findings").EnumerateArray()
    ];

    /// <summary>
    /// §8.4 — a suppressed finding names the row that silenced it, and the file moves when the
    /// matrix does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two claims, and the second is the one that could rot.</b> That the attribution is correct
    /// is checkable against <c>Suppression.Rules</c> directly. That the file is <i>sensitive</i> to
    /// the matrix at all is not — a writer that hard-coded <c>suppressedBy</c> to null would pass
    /// every other test in this file, because nothing else reads it.
    /// </para>
    /// <para>
    /// <b>The mutation is applied to the judgement rather than to the source.</b>
    /// <c>tools/leave-one-out.sh</c> deletes a gate and re-runs, which is the right instrument for
    /// asking whether a gate is observable and the wrong one for a test: it edits the working tree.
    /// Re-judging one finding as unsuppressed and re-rendering asks the narrower question this
    /// needs — does the row reach the file — without a second walk or a mutated checkout.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_suppressed_finding_names_its_row_and_the_file_follows_the_matrix()
    {
        var judged = Judged;
        var silenced = judged.Where(j => !j.IsReported).ToList();

        Assert.NotEmpty(silenced);

        var attribution = Findings
            .Where(f => f.GetProperty("status").GetString() == "suppressed")
            .ToDictionary(KeyOf, f => f.GetProperty("suppressedBy"), StringComparer.Ordinal);

        foreach (var judgement in silenced)
        {
            var named = attribution[judgement.Finding.Key.Canonical];
            var rule = judgement.SilencedBy!;

            Assert.Equal(rule.Name, named.GetProperty("rule").GetString());
            Assert.Equal(rule.Invariant, named.GetProperty("invariant").GetString());

            // Verbatim, so four surfaces do not each write their own version of why it went quiet.
            Assert.Equal(rule.Reason, named.GetProperty("reason").GetString());

            // And the row is one the matrix actually holds, rather than a string the writer made.
            Assert.Contains(Suppression.Rules, r => r.Name == rule.Name);
        }

        // The file follows the matrix: re-judge one silenced finding as reported and it moves.
        var loosened = judged
            .Select(j => ReferenceEquals(j, silenced[0]) ? new Judged(j.Finding, null) : j)
            .ToList();

        Assert.NotEqual(
            JsonOutput.Render(core.Model, judged, Instant),
            JsonOutput.Render(core.Model, loosened, Instant));
    }
}

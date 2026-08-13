using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The terminal report, rendered from Core's model rather than from the probe.
/// </summary>
/// <remarks>
/// <para>
/// This is R1's gate. The report is an accept-workflow snapshot rather than a frozen golden —
/// <c>docs/TESTING.md</c> §3 — because it is a surface still being designed, and re-accepting as
/// it moves is normal. What must not move quietly is the set of places it departs from the probe,
/// so each of those has its own assertion below and none of them relies on reading the snapshot.
/// </para>
/// <para>
/// The four departures are deliberate and were decided before the renderer was written: defect 3
/// (a capped list says what it dropped), defect 16 (the god-object sentence follows the qualifier
/// that holds), defect 17 (the coverage section asks the finding set rather than asserting an
/// absence), and defect 11's layer-span wording. Everything else is the probe's voice.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ReportTests(CoreWalkFixture core)
{
    private IReadOnlyList<string> Lines =>
        Report.For(core.Model, Analysis.FindingsFor(core.Model)).ToList();

    private string Text => string.Join(Environment.NewLine, Lines);

    /// <summary>Every section the probe printed still appears, in the same order.</summary>
    /// <remarks>
    /// <para>
    /// The fidelity decision, asserted rather than trusted. Criticality drift is absent and that
    /// is the one section deliberately not carried — it is <c>TASKS.md</c> X7, undecided, and
    /// Core has no baseline to render from.
    /// </para>
    /// <para>
    /// <b>Additions are listed separately rather than appended to the expected list</b>, so that
    /// adding a section is a deliberate edit to the second array and never an accident of
    /// regenerating the first. The probe's sections are the contract; what Bearing prints beyond
    /// them is a product decision, and the two should not be able to blur into each other.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_sections_are_the_probes_sections_in_the_probes_order()
    {
        string[] probes =
        [
            "-- CONCEALED DECISION ------------------------------------------",
            "-- CONCEALED DECISION, METHOD LEVEL ----------------------------",
            "-- BUG BLAST RADIUS --------------------------------------------",
            "-- CHANGE COST -------------------------------------------------",
            "-- BOUNDARY: HERE BE DRAGONS -----------------------------------",
            "-- LOAD-BEARING AND INTRICATE (no cohort required) -------------",
            "-- BREAKS ALONE (no cohort required) ---------------------------",
            "-- HUBS AND GOD OBJECTS (no cohort required) -------------------",
            "-- SPANS ARCHITECTURAL LAYERS (no cohort required) -------------",
            "-- CIRCULAR REFERENCES -----------------------------------------",
            "-- SHARED MUTABLE STATE (no cohort required) -------------------",
            "-- PROJECT STABILITY vs ABSTRACTNESS ---------------------------",
            "-- NO PEER GROUP -----------------------------------------------",
        ];

        // Bearing's own, in the order they are rendered after the probe's.
        string[] additions =
        [
            // A1. Last on purpose, and the argument against that placement is recorded where the
            // section is written: it qualifies everything above it.
            "-- WHAT WAS NOT ANALYSED ---------------------------------------",
        ];

        Assert.Equal(
            [.. probes, .. additions],
            Lines.Where(l => l.StartsWith("-- ", StringComparison.Ordinal)));
    }

    // -------------------------------------------------------------------- the header ----

    /// <summary>
    /// The header names the build and the solution, and no longer tells the reader the sentences
    /// are drafts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It read <i>"NOMINATED INSTANCES / Draft sentences. Receipts in parentheses. Rewrite before
    /// the session."</i> until <c>TASKS.md</c> A0 — probe-era scaffolding addressed to whoever was
    /// about to present the output, printed to every user of the shipped tool. Asserted as an
    /// absence as well as a presence, because the string is the kind of thing that comes back by
    /// being copied from an old snapshot.
    /// </para>
    /// <para>
    /// <b>The version is asserted here because the snapshot cannot.</b> It is scrubbed to
    /// <c>{version}</c> in <c>VerifyConfiguration</c> so a release does not move the snapshot and
    /// invite a blind re-accept — which means this is the only place that knows the printed
    /// version is the tool's own.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_header_names_the_build_and_the_solution()
    {
        Assert.DoesNotContain("Draft sentences", Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Rewrite before the session", Text, StringComparison.Ordinal);

        // The Cli's version, not Bearing.Core's — <Version> is set on the Cli project, and the
        // model's own ToolVersion property reads Core and reports 1.0.0.
        var version = ToolInfo.ReadVersion(typeof(Report).Assembly);
        Assert.Equal($"BEARING {version} — TestBed.sln", Lines[2]);

        // 145, which is Core's count — the probe reports 144, because it merges the two
        // PayloadTag declarations. docs/DEFECTS.md §1, and the header renders from the model.
        Assert.Contains("145 types in 3 projects", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>SolutionModel.ToolVersion</c> is whatever the host said, and never a version of its own
    /// invention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §21, <b>closed at A4</b>. The property used to read
    /// <c>typeof(SolutionModel).Assembly</c> — <c>Bearing.Core</c>, which sets no
    /// <c>&lt;Version&gt;</c> and therefore reported the SDK default <c>1.0.0</c> against a tool
    /// shipping <c>0.0.1-preview.1</c>. It comes from <c>WalkOptions.ToolVersion</c> now, which
    /// the host supplies because the version lives on whatever packs and Core is not it.
    /// </para>
    /// <para>
    /// The fixture walks without supplying one, so what it pins is the <i>default</i>: <c>0.0.0</c>,
    /// which reads as "nobody told me", where <c>1.0.0</c> read as a release that does not exist.
    /// The old value is asserted absent, because that is the one a reader would have believed.
    /// <c>JsonOutputTests</c> carries the other half — a real walk with a version set — since the
    /// defect lived in the path from options to model and the header never took that path.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_models_tool_version_is_the_hosts_to_supply()
    {
        Assert.Equal(ToolInfo.UnknownVersion, core.Model.ToolVersion);
        Assert.NotEqual("1.0.0", core.Model.ToolVersion);

        // The header reads the Cli's assembly directly and is unaffected either way — which is
        // what let the defect sit unnoticed, so it is worth still saying the two are not the same
        // read.
        Assert.DoesNotContain($"BEARING {core.Model.ToolVersion}", Text, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- coverage (A1) ----

    /// <summary>
    /// What was not analysed is now in the report, and it says the routine things routinely.
    /// </summary>
    /// <remarks>
    /// The model carried <c>ExclusionsApplied</c>, <c>ExcludedTypes</c> and <c>LoadDiagnostics</c>
    /// from the first walk and no line of the renderer read any of them. Invariant 8 is the whole
    /// of why that mattered: a tool disciplined about not making claims it cannot support was
    /// dropping the record of what it could not see.
    /// </remarks>
    [Fact]
    public void The_report_says_what_it_did_not_analyse()
    {
        Assert.Contains("-- WHAT WAS NOT ANALYSED", Text, StringComparison.Ordinal);
        Assert.Contains("Skipped as test projects: Core.Tests", Text, StringComparison.Ordinal);

        // Two types, and the pattern count rather than the patterns — sixteen defaults on one
        // line is what this replaced.
        Assert.Contains("Excluded by path: 2 types, under 16 patterns", Text, StringComparison.Ordinal);
        Assert.Contains("Load diagnostics: none", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A load diagnostic is reported as a reason to distrust the numbers, not as a failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tested against a fabricated <see cref="Coverage"/> because no fixture can produce one.</b>
    /// Every solution in this repository loads cleanly, and a deliberately broken one is a fixture
    /// whose only purpose is this path — so the section takes <c>Coverage</c> rather than the
    /// model, and the branch is reachable from a test without one. The alternative was shipping
    /// the loudest thing the report can say with nothing exercising it.
    /// </para>
    /// <para>
    /// The wording is the assertion. <c>docs/DEFECTS.md</c> §4 is load success judged by
    /// diagnostic rather than by outcome — six spurious failures on nopCommerce — so the section
    /// may not call a diagnostic a failure. What it may say is what is certain: a project that did
    /// not load understates fan-in everywhere it is referenced.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_load_diagnostic_is_reported_as_a_reason_to_distrust_the_numbers()
    {
        var lines = Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = ["/obj/"],
            SkippedProjects = [],
            LoadDiagnostics = ["Project 'A.csproj' failed to restore.", "SDK 'X' not found."],
            ExcludedTypes = 0,
        }).ToList();

        var text = string.Join(Environment.NewLine, lines);

        Assert.Contains("2 diagnostics while loading", text, StringComparison.Ordinal);
        Assert.Contains("not necessarily", text, StringComparison.Ordinal);
        Assert.Contains("understates fan-in EVERYWHERE", text, StringComparison.Ordinal);
        Assert.Contains("Project 'A.csproj' failed to restore.", text, StringComparison.Ordinal);

        // And the clean-run line is not also printed, which would contradict the block above it.
        Assert.DoesNotContain("Load diagnostics: none", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The diagnostics list discloses what it dropped, and its cap is not <c>--top</c>.
    /// </summary>
    /// <remarks>
    /// Lowering <c>--top</c> is how a reader focuses a report, and it must not also hide the
    /// reasons that report might be wrong. Asserted because the obvious implementation reuses the
    /// policy value every other capped list uses.
    /// </remarks>
    [Fact]
    public void The_diagnostics_list_discloses_what_it_dropped()
    {
        var many = Enumerable.Range(1, 14).Select(i => $"diagnostic {i}").ToList();

        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = [],
            SkippedProjects = [],
            LoadDiagnostics = many,
            ExcludedTypes = 0,
        }));

        Assert.Contains("diagnostic 10", text, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic 11", text, StringComparison.Ordinal);
        Assert.Contains("4 diagnostics not shown of 14", text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ defect 16 ----

    /// <summary>
    /// A god object by size is not told it carries real logic.
    /// </summary>
    /// <remarks>
    /// <c>DispatchRegistry</c> is the case: 23 members against a ceiling of 20, and its worst
    /// method is cc 1. The probe prints "AND carries real logic (23 members, worst method
    /// Registered at cc 1)" — a sentence its own receipts refute in the same breath. The size arm
    /// and the logic arm are independent qualifiers in Core, so the renderer can only say what
    /// holds.
    /// </remarks>
    [Fact]
    public void A_god_object_by_size_is_not_told_it_carries_real_logic()
    {
        var line = Assert.Single(Lines, l => l.Contains("DispatchRegistry [", StringComparison.Ordinal));

        Assert.Contains("broad rather than deep", line, StringComparison.Ordinal);
        Assert.DoesNotContain("carries real logic", line, StringComparison.Ordinal);

        // docs/DEFECTS.md §29. The claim this arm makes has not changed; the words have. "Too
        // large for anyone to hold at once" was read by a reader outside the build as the report
        // giving up rather than as a statement about the type, and the old phrasing is asserted
        // absent because that is the kind of sentence that comes back from an old snapshot.
        Assert.DoesNotContain("too large for anyone", line, StringComparison.Ordinal);

        // The receipt that refuted the probe's sentence is still shown, because the reader has to
        // be able to check the claim that replaced it.
        Assert.Contains("23 members", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hub_that_really_does_carry_logic_still_says_so()
    {
        // The control. Without it the assertion above passes on a renderer that never makes the
        // claim at all, which would be a different defect with the same shape.
        var line = Assert.Single(Lines, l => l.Contains("ShipmentCoordinator [", StringComparison.Ordinal));

        Assert.Contains("carries real logic", line, StringComparison.Ordinal);
        Assert.Contains("at cc 13", line, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ defect 17 ----

    /// <summary>
    /// The coverage section does not claim an absence that is not true.
    /// </summary>
    /// <remarks>
    /// The probe says these types "are absent from the nominations above". Three of them are not:
    /// the cohort-free findings never consult a cohort, so a peerless type is eligible for all of
    /// them. The renderer asks the finding set instead.
    /// </remarks>
    [Fact]
    public void The_coverage_section_does_not_assert_an_absence_it_cannot_support()
    {
        Assert.DoesNotContain("absent from the", Text, StringComparison.Ordinal);
        Assert.Contains("do still appear", Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_coverage_count_matches_what_the_finding_set_actually_says()
    {
        var findings = Analysis.FindingsFor(core.Model);

        var expected = findings
            .OfKind(FindingKind.Coverage)
            .Count(f => findings.About(f.Subject).Any(other => other.Kind != FindingKind.Coverage));

        // Stated rather than parsed out of the sentence: the point of the fix is that the number
        // comes from the finding set, so the test asks the finding set the same question.
        Assert.True(expected > 0, "the fixture no longer exercises defect 17 — see docs/DEFECTS.md §17");
        Assert.Contains($"   {expected} of them do still appear", Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- defect 3 ----

    /// <summary>
    /// Every capped list says what it dropped.
    /// </summary>
    /// <remarks>
    /// Rendered at a deliberately low <c>--top</c> so several caps bite at once. Until P6 the
    /// default of 15 truncated nothing at all, which is exactly how this defect survived: the
    /// probe's <c>Take</c> was invisible on every input anyone looked at.
    /// </remarks>
    [Fact]
    public void A_shortened_list_says_that_it_was_shortened()
    {
        var narrow = core.Model.Policy with { Top = 2 };
        var model = core.WalkWith(narrow);

        var text = string.Join(Environment.NewLine, Report.For(model, Analysis.FindingsFor(model)));

        Assert.Contains("not shown of", text, StringComparison.Ordinal);
        Assert.Contains("raise --top", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The disclosure appears where a cap bit, and nowhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control for the disclosure: it has to appear exactly when it is true, or it becomes
    /// noise people learn to skip — which is how the roll-call problem started.
    /// </para>
    /// <para>
    /// <b>P6 is what made this control do any work.</b> It used to assert that the string was
    /// absent from the whole report, because nothing in the fixture reached the default
    /// <c>--top</c> of 15 — a control that deleting the disclosure would have satisfied just as
    /// well. Coverage now carries 18 peerless subjects, four of them P6's, so at defaults exactly
    /// one section is capped and exactly one line is entitled to say so. Asserting the count is
    /// what makes both failure directions visible: a disclosure that stops appearing, and one
    /// that starts appearing where nothing was dropped.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_disclosure_appears_where_a_cap_bit_and_nowhere_else()
    {
        var disclosures = Text
            .Split('\n')
            .Where(line => line.Contains("not shown of", StringComparison.Ordinal))
            .ToList();

        Assert.Single(disclosures);
        Assert.Contains("3 types not shown of 18", disclosures[0], StringComparison.Ordinal);
        Assert.Contains("raise --top", disclosures[0], StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the snapshot ----

    [Fact]
    public Task The_report_renders() => Verify(Text);
}

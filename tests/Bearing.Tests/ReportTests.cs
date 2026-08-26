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
        Report.For(core.Model, Analysis.Judge(core.Model)).ToList();

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
        // A13 tier 2, and above the probe's first section on purpose: the report already led with
        // findings, and the first line was still one of 1,091 rows of one kind.
        string[] leading =
        [
            "-- START HERE --------------------------------------------------",
        ];

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
            "-- NO STATIC REFERENCES FOUND (no cohort required) -------------",
            "-- CIRCULAR REFERENCES -----------------------------------------",
            "-- SHARED MUTABLE STATE (no cohort required) -------------------",
            "-- PROJECT STABILITY vs ABSTRACTNESS ---------------------------",
            "-- NO PEER GROUP -----------------------------------------------",
        ];

        // NO STATIC REFERENCES FOUND is Bearing's own and sits inside the probe's list rather than
        // after it, which is the one place this test's shape has to bend. A9's section is a claim
        // about components, so it belongs with the claims; putting it after PROJECT STABILITY to
        // keep the probe's block contiguous would separate it from every other finding by two
        // structure sections. The probe's order is preserved and one section is interleaved.

        // Bearing's own, in the order they are rendered after the probe's.
        string[] additions =
        [
            // A1. Last on purpose, and the argument against that placement is recorded where the
            // section is written: it qualifies everything above it.
            "-- WHAT WAS NOT ANALYSED ---------------------------------------",
        ];

        Assert.Equal(
            [.. leading, .. probes, .. additions],
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

        // Keyed on (assembly, FQN), so both PayloadTag declarations are counted.
        // StructureTests.Fixture_shape_is_stable owns this number and its history; the point
        // here is only that the header renders it from the model rather than recounting.
        Assert.Contains("206 types — classes and interfaces — in 3 projects", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>SolutionModel.ToolVersion</c> is whatever the host said, and never a version of its own
    /// invention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>closed at A4</b>. The property used to read
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
    /// The wording is the assertion. Load success used to be judged by
    /// diagnostic rather than by outcome — six spurious failures on nopCommerce — so the section
    /// may not call a diagnostic a failure. **Fixed 2026-08-20**: the warning moved onto
    /// <c>Coverage.ProjectsNotLoaded</c>, which is the only fact that supports it, and the two
    /// halves are asserted separately here and below.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_load_diagnostic_is_shown_without_being_called_a_failure()
    {
        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = ["/obj/"],
            SkippedProjects = [],
            LoadDiagnostics = ["Project 'A.csproj' failed to restore.", "SDK 'X' not found."],
            ProjectsNotLoaded = [],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [],
        }));

        Assert.Contains("2 diagnostics while loading", text, StringComparison.Ordinal);
        Assert.Contains("Project 'A.csproj' failed to restore.", text, StringComparison.Ordinal);

        // The lower-bound warning is NOT attached to them. This is the whole of it: every project
        // compiled, so there is nothing to read as a lower bound, however alarming the diagnostics
        // sound. On nopCommerce this block is six NuGet advisories and 3,209 types loaded.
        Assert.DoesNotContain("lower bound", text, StringComparison.Ordinal);
        Assert.DoesNotContain("understates fan-in", text, StringComparison.Ordinal);

        // And the outcome is stated positively rather than left to the absence of a warning.
        Assert.Contains("Every project selected for analysis produced a compilation", text, StringComparison.Ordinal);

        // The clean-run line is not also printed, which would contradict the block above it.
        Assert.DoesNotContain("Load diagnostics: none", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A project that did not load is what carries the lower-bound warning.
    /// </summary>
    /// <remarks>
    /// The other half of that. The claim has to be made by something, and this
    /// is the only fact that supports it: a project that produced no compilation declares no
    /// types, so every type it referenced is short an inbound edge. It is named, because "one
    /// project did not load" is not actionable and "Nop.Data did not load" is.
    /// </remarks>
    [Fact]
    public void A_project_that_did_not_load_is_what_bounds_the_numbers()
    {
        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = [],
            SkippedProjects = [],
            LoadDiagnostics = ["No compilation for Widgets"],
            ProjectsNotLoaded = ["Widgets"],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [],
        }));

        Assert.Contains("1 project did not load: Widgets.", text, StringComparison.Ordinal);
        Assert.Contains("understates fan-in EVERYWHERE", text, StringComparison.Ordinal);
        Assert.Contains("lower bound", text, StringComparison.Ordinal);

        // And the reassurance is absent, because it would be false.
        Assert.DoesNotContain("Every project selected for analysis produced a compilation", text, StringComparison.Ordinal);
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
            ProjectsNotLoaded = [],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [],
        }));

        Assert.Contains("diagnostic 10", text, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic 11", text, StringComparison.Ordinal);
        Assert.Contains("4 diagnostics not shown of 14", text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ defect 56 ----

    /// <summary>
    /// A project that compiled without resolving its references is disclosed, and named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>README.md</c> warns that an unrestored solution loads
    /// with missing references and understates the results; the artifact said <i>"Every project
    /// selected for analysis produced a compilation"</i>, which is true and reads as reassurance.
    /// <b>A compilation with unresolved references is still a compilation</b>, so the sentence
    /// above cannot carry this and a new one has to.
    /// </para>
    /// <para>
    /// <b>The direction is part of the claim.</b> A missing reference is a missing edge and never a
    /// spurious one, so every number moves one way — measured on Umbraco with three of twenty-five
    /// projects unrestored: 37,118 edges against 37,241 restored, and types identical either way
    /// because Roslyn parses syntax without references. The reader is told which way to read the
    /// bias, not merely that something is wrong.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_project_that_did_not_resolve_its_references_is_disclosed()
    {
        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = [],
            SkippedProjects = [],
            LoadDiagnostics = [],
            ProjectsNotLoaded = [],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [new UnresolvedReferences("Widgets", 412)],
        }));

        Assert.Contains("1 project did NOT resolve every type it names", text, StringComparison.Ordinal);
        Assert.Contains("Widgets — 412 unresolved type names", text, StringComparison.Ordinal);
        Assert.Contains("lower bound", text, StringComparison.Ordinal);

        // The clean line is not also printed, which would contradict the block above it.
        Assert.DoesNotContain("Every project resolved every reference it names", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A clean run says so, because the absence of a warning is not an assurance.
    /// </summary>
    /// <remarks>
    /// Invariant 8, and the half that is easy to skip. Every other incompleteness this section
    /// reports states its "none" — skipped projects, exclusions, dangling edges — because a reader
    /// scanning for a warning cannot distinguish "nothing was wrong" from "nothing was checked".
    /// The unresolved-reference disclosure exists because this one was the exception.
    /// </remarks>
    [Fact]
    public void A_run_that_resolved_everything_says_so()
    {
        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = [],
            SkippedProjects = [],
            LoadDiagnostics = [],
            ProjectsNotLoaded = [],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [],
        }));

        Assert.Contains("Every project resolved every reference it names.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("did NOT resolve", text, StringComparison.Ordinal);
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

        // The claim this arm makes has not changed; the words have. "Too
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
        //
        // Every line that names it, and there are two since A13 tier 2 — the section's row and the
        // lead above it. Asserting over both is the stronger test rather than a concession to the
        // new one: the whole point of extracting the wording is that the two cannot disagree, so a
        // renderer that softened the claim in one place would fail here.
        var lines = Lines.Where(l => l.Contains("ShipmentCoordinator [", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line =>
        {
            Assert.Contains("carries real logic", line, StringComparison.Ordinal);
            Assert.Contains("at cc 13", line, StringComparison.Ordinal);
        });
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
        Assert.True(expected > 0, "the fixture no longer exercises the NO PEER GROUP disclosure");
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

        var text = string.Join(Environment.NewLine, Report.For(model, Analysis.Judge(model)));

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

        // Four caps bite on this fixture and all four say so. It was one until A9, which added
        // two: the dead-code section caps its collapsed carriers and its individual members
        // separately, because they are two lists and one shared cap would let either crowd the
        // other out. D10 added the fourth — letting thin cohorts nominate takes method-level
        // concealed decision to 16 against a --top of 15, so the section drops one and discloses
        // it. Asserted as a set rather than loosened to "at least one", because a section that
        // silently stopped disclosing would still pass that.
        Assert.Equal(4, disclosures.Count);
        // P10 takes the peerless population 22 -> 25: ScaleHead alone under suffix:Head, and the
        // two *Window types under a suffix cohort of two. IScaleHead is NOT among them — it is
        // cohorted by architectural kind, into kind:Contract, which is the one existing cohort
        // this plant moves.
        // P11 takes it 25 -> 27, and that both of its types land here is the ordinary cost of a
        // plant with fresh trailing words: BerthPlacard and YardDocket are each alone in a suffix
        // cohort of one. Moving no existing cohort is the point of choosing words nothing else
        // ends in, and the peerless count is where that choice shows up.
        Assert.Contains(disclosures, d => d.Contains("12 types not shown of 27", StringComparison.Ordinal));
        Assert.Contains(disclosures, d => d.Contains("types not shown of 18", StringComparison.Ordinal));
        Assert.Contains(disclosures, d => d.Contains("nominations not shown of 23", StringComparison.Ordinal));
        Assert.All(disclosures, d => Assert.Contains("raise --top", d, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ the snapshot ----

    [Fact]
    public Task The_report_renders() => Verify(Text);
}

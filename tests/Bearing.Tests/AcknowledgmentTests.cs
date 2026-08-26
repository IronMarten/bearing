using System.Text.Json;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// Acknowledgment memory — <c>PRD-free-tier.md</c> §10.3, and success metric 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>The loop these assert is the whole feature</b>: mark a finding known and fine in a committed
/// file, and it stays quiet next run. What has to be true for that to be worth anything is that the
/// claim goes quiet on <i>every</i> surface a reader sees, stays in the export, and that the run
/// still says it withheld something.
/// </para>
/// <para>
/// <b>The file is built from a key the run produced</b>, never from a string written here. A key
/// typed into a test is a test of the writer's memory of the format; a key read off a finding is a
/// test of the round trip the feature is, which is the only thing <c>FindingKey.Canonical</c>
/// promises.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class AcknowledgmentTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ the file ----

    [Fact]
    public void Blank_lines_and_comments_are_not_entries()
    {
        var known = Acknowledgments.Of(
        [
            "# what this file is",
            "",
            "   ",
            "HubOrGodObject|type|A|A.B",
            "# trailing note",
        ]);

        Assert.Equal(1, known.Count);
        Assert.Equal("HubOrGodObject|type|A|A.B", known.All[0].Key);
        Assert.Equal(4, known.All[0].Line);
    }

    [Fact]
    public void A_note_after_a_tab_is_kept_and_is_not_part_of_the_key()
    {
        var known = Acknowledgments.Of(["HubOrGodObject|type|A|A.B\tknown, splitting it is a Q3 job"]);

        Assert.Equal("HubOrGodObject|type|A|A.B", known.All[0].Key);
        Assert.Equal("known, splitting it is a Q3 job", known.All[0].Note);
    }

    /// <summary>
    /// A duplicate is a merge, not a corruption, so the file is read rather than rejected.
    /// </summary>
    [Fact]
    public void The_first_of_two_entries_with_one_key_wins()
    {
        var known = Acknowledgments.Of(
        [
            "HubOrGodObject|type|A|A.B\tours",
            "HubOrGodObject|type|A|A.B\ttheirs",
        ]);

        Assert.Equal(1, known.Count);
        Assert.Equal("ours", known.All[0].Note);
    }

    [Fact]
    public void A_file_that_is_not_there_is_not_an_error()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bearing-no-such-{Guid.NewGuid():N}");

        Assert.Same(Acknowledgments.None, Acknowledgments.Read(path));
        Assert.Null(Acknowledgments.None.Path);
        Assert.Equal(0, Acknowledgments.None.Count);
    }

    // ------------------------------------------------------------- the judgement ----

    [Fact]
    public void An_acknowledged_finding_is_not_reported_and_is_still_judged()
    {
        var (judgement, silenced) = OneAcknowledged();

        Assert.DoesNotContain(
            silenced.Key.Canonical,
            judgement.Reported.All.Select(f => f.Key.Canonical),
            StringComparer.Ordinal);

        // Still judged, and its receipts are intact: an acknowledgment withholds a claim from a
        // reader and does not withdraw it.
        var judged = judgement.All.Single(j =>
            string.Equals(j.Finding.Key.Canonical, silenced.Key.Canonical, StringComparison.Ordinal));

        Assert.True(judged.IsAcknowledged);
        Assert.False(judged.IsSuppressed);
        Assert.False(judged.IsReported);
        Assert.Equal(silenced.Receipts.Count, judged.Finding.Receipts.Count);
    }

    /// <summary>
    /// Acknowledging a claim the matrix already withheld changes nothing about the claim.
    /// </summary>
    /// <remarks>
    /// The rule that keeps the two axes apart. If the file could turn a suppressed finding into an
    /// acknowledged one, a consumer of the export would read the tool as having stood by a claim it
    /// declined to make.
    /// </remarks>
    [Fact]
    public void A_row_answers_before_the_file_does()
    {
        var baseline = Analysis.Judge(core.Model);
        var suppressed = baseline.Withheld.First(j => j.IsSuppressed).Finding;

        var judgement = Analysis.Judge(
            core.Model, Acknowledgments.Of([suppressed.Key.Canonical]));

        var judged = judgement.All.Single(j =>
            string.Equals(j.Finding.Key.Canonical, suppressed.Key.Canonical, StringComparison.Ordinal));

        Assert.True(judged.IsSuppressed);
        Assert.True(judged.IsAcknowledged);
        Assert.Equal(0, judgement.AcknowledgedCount);
    }

    [Fact]
    public void An_entry_matching_no_claim_is_reported_as_unmatched()
    {
        var judgement = Analysis.Judge(
            core.Model, Acknowledgments.Of(["HubOrGodObject|type|NoSuchAssembly|No.Such.Type"]));

        Assert.Equal(
            "HubOrGodObject|type|NoSuchAssembly|No.Such.Type",
            Assert.Single(judgement.Unmatched).Key);
        Assert.Equal(0, judgement.AcknowledgedCount);
    }

    // --------------------------------------------------------------- the surfaces ----

    /// <summary>
    /// Quiet on every surface, or the feature does not exist.
    /// </summary>
    /// <remarks>
    /// <b>This is the test the seam work was for.</b> A renderer that recovered its population from
    /// the model rather than from the judgement would keep printing the claim, with nothing failing
    /// — and the circular-reference sections did exactly that until <c>docs/ARCHITECTURE.md</c> §11
    /// was settled. Every reported kind is acknowledged in turn rather than one representative,
    /// because the defect this catches is per-section.
    /// </remarks>
    [Fact]
    public void An_acknowledged_finding_of_any_kind_goes_quiet_on_both_renderers()
    {
        var baseline = Analysis.Judge(core.Model);
        var kinds = baseline.Reported.All.Select(f => f.Kind).Distinct().Order().ToList();

        // A guard on the loop: the fixture has to report the cycle kinds, because they are the ones
        // whose sections drew themselves from the model and the reason this test exists.
        Assert.Contains(FindingKind.NamespaceCycle, kinds);
        Assert.Contains(FindingKind.TypeTangle, kinds);

        var named = new List<FindingKind>();

        foreach (var kind in kinds)
        {
            // Whichever of this kind's findings the rest of the reported set does not also name. A
            // component two claims both name cannot show that one of them went quiet, so it is no
            // evidence; where every finding of a kind is like that, only the population assertion
            // below runs, and `named` records which kinds got more than that.
            var finding = baseline.Reported.OfKind(kind)
                .FirstOrDefault(f => Names(baseline, f).Count > 0);

            var subject = finding ?? baseline.Reported.OfKind(kind)[0];
            var judgement = Analysis.Judge(core.Model, Acknowledgments.Of([subject.Key.Canonical]));

            // True for every kind: what the renderers are handed is one claim shorter.
            Assert.Empty(judgement.Unmatched);
            Assert.Equal(baseline.Reported.Count - 1, judgement.Reported.Count);

            if (finding is null) continue;

            var terminal = string.Join("\n", Report.For(core.Model, judgement));

            // --full, because the default page leads with one finding per kind and enumerates the
            // rest behind the flag — so most kinds are never named on the short page at all, and an
            // assertion about their names there passes by having nothing to find.
            var page = HtmlReport.Render(core.Model, judgement, Instant, full: true);

            // Fewer mentions, not none. A component can be named by a section that makes no claim
            // about it -- the project and coupling sections list types the findings never nominate
            // -- and asserting the name vanishes outright asserts something A10 never promised. What
            // it promises is that the claim stops being made, and the claim is the mention that
            // goes. A renderer recovering this kind's population from the model instead of from the
            // reported set would leave the count where it was, which is what this catches.
            foreach (var name in Names(baseline, finding))
            {
                Assert.True(
                    Occurrences(terminal, name) < Occurrences(BaselineTerminal, name),
                    $"{kind}: the terminal report names {name} as often after acknowledging it");

                Assert.True(
                    Occurrences(page, name) < Occurrences(BaselinePage, name),
                    $"{kind}: the HTML report names {name} as often after acknowledging it");
            }

            named.Add(kind);
        }

        // The guard on the loop, and it is not a tolerance. The kinds this test was written for are
        // the ones whose sections drew their own population from the model; if either of them
        // stopped producing a name of its own, the assertion that matters would fall silently to
        // the population check every kind already gets.
        Assert.Contains(FindingKind.NamespaceCycle, named);
        Assert.Contains(FindingKind.TypeTangle, named);
    }

    /// <summary>
    /// What the report would call this finding's subject — the strings a section would print.
    /// </summary>
    /// <remarks>
    /// <b>Only names that are this finding's alone.</b> A component named by a second, still
    /// reported claim would fail the assertion above for a reason that has nothing to do with
    /// acknowledgment, so a name the rest of the reported set also carries is not evidence and is
    /// dropped. What survives is checked by the caller, which refuses to assert on an empty list.
    /// </remarks>
    private IReadOnlyList<string> Names(Judgement baseline, Finding finding)
    {
        var elsewhere = baseline.Reported.All
            .Where(f => !ReferenceEquals(f, finding))
            .SelectMany(Subjects)
            .ToHashSet(StringComparer.Ordinal);

        return [.. Subjects(finding).Where(name => !elsewhere.Contains(name)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// A subject's printable names, for every subject kind rather than for types alone.
    /// </summary>
    /// <remarks>
    /// A set subject is a cycle or a tangle, and its members are what the section names. Resolving
    /// only through <c>SolutionModel.Find</c> covers types and members and quietly returns nothing
    /// for a namespace or a project, which would leave the cycle sections — the whole reason this
    /// test exists — asserting on an empty list.
    /// </remarks>
    private IEnumerable<string> Subjects(Finding finding) =>
        (finding.Subject.Members.Count > 0 ? finding.Subject.Members : [finding.Subject])
        .Select(NameOf)
        .OfType<string>();

    private string BaselineTerminal =>
        string.Join("\n", Report.For(core.Model, Analysis.Judge(core.Model)));

    private string BaselinePage =>
        HtmlReport.Render(core.Model, Analysis.Judge(core.Model), Instant, full: true);

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    private string? NameOf(SubjectRef id)
    {
        if (core.Model.Find(id) is { } type) return type.Name;

        foreach (var ns in core.Model.Namespaces)
            if (SubjectRef.ForNamespace(ns.Namespace).Equals(id))
                return ns.Namespace;

        foreach (var project in core.Model.Projects)
            if (SubjectRef.ForProject(project.Name).Equals(id))
                return project.Name;

        return null;
    }

    /// <summary>
    /// The report says it withheld something, and says how much.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-b.md</c> §4: a suppression that cannot be observed to withhold anything is
    /// worse than none. A user's own file is the case where that matters most — the run that reads
    /// it may be months after the run that wrote it, and on a shared report the reader is often not
    /// the author.
    /// </remarks>
    [Fact]
    public void Both_surfaces_disclose_what_the_file_kept_out()
    {
        var (judgement, _) = OneAcknowledged();

        var terminal = string.Join("\n", Report.For(core.Model, judgement));
        var page = HtmlReport.Render(core.Model, judgement, Instant);

        Assert.Contains("1 finding marked known and fine", terminal, StringComparison.Ordinal);
        Assert.Contains("is not shown above", terminal, StringComparison.Ordinal);
        Assert.Contains(Acknowledgments.DefaultFileName, terminal, StringComparison.Ordinal);

        Assert.Contains("1 finding", page, StringComparison.Ordinal);
        Assert.Contains("not shown on this page", page, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_with_no_file_says_nothing_about_acknowledgments()
    {
        var judgement = Analysis.Judge(core.Model);

        var terminal = string.Join("\n", Report.For(core.Model, judgement));
        var page = HtmlReport.Render(core.Model, judgement, Instant);

        Assert.DoesNotContain("-- ACKNOWLEDGED", terminal, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Acknowledged</h2>", page, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the export ----

    /// <summary>
    /// <c>SCHEMA-findings-export.md</c> §1: the export is a superset of what the report renders.
    /// </summary>
    /// <remarks>
    /// The contract test applied to the one mechanism built to remove things from a report. An
    /// acknowledgment that also shrank the export would make the paid tier blind to exactly the
    /// judgements a user has opinions about — which are the interesting ones.
    /// </remarks>
    [Fact]
    public void An_acknowledged_finding_is_in_the_export_with_its_status_and_note()
    {
        var baseline = Analysis.Judge(core.Model);
        var finding = baseline.Reported.All[0];

        var judgement = Analysis.Judge(
            core.Model,
            Acknowledgments.Of([finding.Key.Canonical + "\tknown, and fine"]));

        var root = JsonDocument.Parse(JsonOutput.Render(core.Model, judgement, Instant)).RootElement;

        Assert.Equal(
            baseline.All.Count,
            root.GetProperty("findings").GetArrayLength());

        var block = root.GetProperty("findings").EnumerateArray()
            .Single(f => f.GetProperty("key").GetString() == finding.Key.Canonical);

        Assert.Equal("acknowledged", block.GetProperty("status").GetString());
        Assert.Equal("known, and fine", block.GetProperty("acknowledgedBy").GetProperty("note").GetString());
        Assert.Equal(1, block.GetProperty("acknowledgedBy").GetProperty("line").GetInt32());

        // The receipts survive, which is what makes re-raising on material change a decision that
        // can still be taken later against data already in the file.
        Assert.NotEmpty(block.GetProperty("receipts").EnumerateArray());
    }

    [Fact]
    public void The_export_carries_the_file_the_run_was_judged_against()
    {
        var judgement = Analysis.Judge(
            core.Model,
            Acknowledgments.Of(
            [
                Analysis.Judge(core.Model).Reported.All[0].Key.Canonical,
                "HubOrGodObject|type|NoSuchAssembly|No.Such.Type\tstale",
            ]));

        var root = JsonDocument.Parse(JsonOutput.Render(core.Model, judgement, Instant)).RootElement;
        var block = root.GetProperty("acknowledgments");

        Assert.Equal(2, block.GetProperty("entries").GetInt32());
        Assert.Equal(1, block.GetProperty("silenced").GetInt32());

        var unmatched = block.GetProperty("unmatched").EnumerateArray().Single();

        Assert.Equal("HubOrGodObject|type|NoSuchAssembly|No.Such.Type", unmatched.GetProperty("key").GetString());
        Assert.Equal("stale", unmatched.GetProperty("note").GetString());
    }

    [Fact]
    public void A_run_with_no_file_still_writes_the_block()
    {
        var root = JsonDocument
            .Parse(JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant))
            .RootElement;

        var block = root.GetProperty("acknowledgments");

        Assert.Equal(JsonValueKind.Null, block.GetProperty("path").ValueKind);
        Assert.Equal(0, block.GetProperty("entries").GetInt32());
        Assert.Empty(block.GetProperty("unmatched").EnumerateArray());
    }

    // ------------------------------------------------------------- the command line ----

    [Fact]
    public void The_default_file_sits_beside_the_solution()
    {
        var invocation = CommandLine.Parse([RepoPaths.TestBedSolution]);

        Assert.Equal(
            Path.Combine(
                Path.GetDirectoryName(RepoPaths.TestBedSolution)!, Acknowledgments.DefaultFileName),
            invocation.AcknowledgePath);

        Assert.False(invocation.AcknowledgeExplicit);
    }

    [Fact]
    public void A_named_file_is_recorded_as_named()
    {
        var invocation = CommandLine.Parse([RepoPaths.TestBedSolution, "--acknowledge", "known.txt"]);

        Assert.Equal(Path.GetFullPath("known.txt"), invocation.AcknowledgePath);
        Assert.True(invocation.AcknowledgeExplicit);
    }

    [Fact]
    public void The_flag_is_in_the_usage_text() =>
        Assert.Contains(
            CommandLine.Usage("0.0.1"),
            line => line.Contains("--acknowledge", StringComparison.Ordinal));

    // ------------------------------------------------------------------------ ----

    /// <summary>The fixture judged with its first reported finding acknowledged.</summary>
    private (Judgement Judgement, Finding Silenced) OneAcknowledged()
    {
        var finding = Analysis.Judge(core.Model).Reported.All[0];

        var path = Path.Combine(
            Path.GetDirectoryName(RepoPaths.TestBedSolution)!, Acknowledgments.DefaultFileName);

        return (
            Analysis.Judge(core.Model, Acknowledgments.Of([finding.Key.Canonical], path)),
            finding);
    }
}

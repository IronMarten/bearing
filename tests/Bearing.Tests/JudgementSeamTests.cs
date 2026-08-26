using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// A renderer draws what the judgement reported, and nothing it recovered for itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule <c>docs/ARCHITECTURE.md</c> §11 settles, asserted rather than described.</b> Both
/// circular-reference sections used to take their population from
/// <c>model.ShapedNamespaceCycles</c> and split it on <c>ShapedCycle.IsReportable</c> — a renderer
/// deciding, from the shape alone, a question the suppression matrix had already answered over the
/// whole finding set. They agreed with the matrix because both cycle rows happen to test the same
/// shape, and nothing held them to it.
/// </para>
/// <para>
/// <b>Each test below withholds a claim for a reason the shape cannot see.</b> That is the whole
/// design: the rule these use is not one of the matrix's, so a section reading
/// <c>IsReportable</c> would still draw the cycle as reported and every assertion here would fail.
/// A section reading the judgement cannot. <c>docs/TESTING.md</c> §9 — the point of these is that
/// they are able to fail, and re-deriving the population is the failure they are able to catch.
/// </para>
/// <para>
/// The mutation is applied to the judgement and not to the matrix, for the reason
/// <c>FindingsExportTests</c> gives: it asks the narrow question without a second walk or a
/// mutated checkout.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class JudgementSeamTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A row no cycle rule resembles, so that nothing about the shape can predict it.
    /// </summary>
    private static readonly SuppressionRule Arbitrary = new(
        "not-a-matrix-row",
        FindingKind.NamespaceCycle,
        Invariant: "0",
        "a reason no shape carries, so a renderer re-deciding from the shape gets this wrong")
    {
        Applies = (_, _, _) => true,
    };

    [Fact]
    public void A_withheld_namespace_cycle_leaves_the_reported_list_in_both_renderers()
    {
        var judgement = Analysis.Judge(core.Model);
        var cycle = judgement.Reported.OfKind(FindingKind.NamespaceCycle)[0];

        // The one reported namespace cycle on the fixture is CycleShape.Coupling, which is exactly
        // what IsReportable tests for -- so no reading of the shape can move it.
        var withheld = Withhold(judgement, cycle.Key.Canonical);

        var loop = LoopLineOf(cycle);

        Assert.Contains(loop, Terminal(judgement), StringComparison.Ordinal);
        Assert.DoesNotContain(loop, Terminal(withheld), StringComparison.Ordinal);

        Assert.Contains(FirstMemberOf(cycle), Page(judgement), StringComparison.Ordinal);
        Assert.DoesNotContain(LoopHtmlOf(cycle), Page(withheld), StringComparison.Ordinal);
    }

    [Fact]
    public void A_withheld_namespace_cycle_joins_the_not_reported_list_in_both_renderers()
    {
        var judgement = Analysis.Judge(core.Model);
        var cycle = judgement.Reported.OfKind(FindingKind.NamespaceCycle)[0];
        var withheld = Withhold(judgement, cycle.Key.Canonical);

        // Listed, not merely dropped. The section names what it set aside so a reader can disagree
        // with the suppression, and a claim that goes quiet without appearing there is the silent
        // truncation the section exists to avoid.
        Assert.Equal(
            NotReportedEntries(Terminal(judgement)) + 1,
            NotReportedEntries(Terminal(withheld)));

        Assert.Equal(
            Occurrences(Page(judgement), "namespace(s),") + 1,
            Occurrences(Page(withheld), "namespace(s),"));
    }

    /// <summary>
    /// The two kinds with no suppression row of their own are on the same seam.
    /// </summary>
    /// <remarks>
    /// No row silences a type tangle today, so this kind's population never differed from the
    /// model's — which is precisely why it was the one most likely to be left reading the model. A
    /// claim can go quiet for a reason the matrix does not hold.
    /// </remarks>
    [Fact]
    public void A_withheld_type_tangle_is_drawn_by_neither_renderer()
    {
        var judgement = Analysis.Judge(core.Model);
        var tangle = judgement.Reported.OfKind(FindingKind.TypeTangle)
            .OrderBy(f => f.Subject.Members.Count)
            .First();

        var withheld = Withhold(judgement, tangle.Key.Canonical);
        var name = core.Model.Find(tangle.Subject.Members[0])!.Name;

        Assert.Contains(name, TangleSection(Terminal(judgement)), StringComparison.Ordinal);
        Assert.DoesNotContain(name, TangleSection(Terminal(withheld)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Re-judging one claim moves that claim and nothing else.
    /// </summary>
    /// <remarks>
    /// The guard on the three above. Each of them asserts that something disappeared, and a
    /// renderer that dropped its whole cycle section would satisfy every one of them.
    /// </remarks>
    [Fact]
    public void Withholding_one_claim_moves_one_claim()
    {
        var judgement = Analysis.Judge(core.Model);
        var cycle = judgement.Reported.OfKind(FindingKind.NamespaceCycle)[0];
        var withheld = Withhold(judgement, cycle.Key.Canonical);

        Assert.Equal(judgement.All.Count, withheld.All.Count);
        Assert.Equal(judgement.Reported.Count - 1, withheld.Reported.Count);
        Assert.Equal(judgement.Withheld.Count + 1, withheld.Withheld.Count);

        // And the sections either side of the cycles are untouched, so nothing above asserts the
        // absence of a whole report.
        Assert.Equal(
            Section(Terminal(judgement), "-- SHARED MUTABLE STATE"),
            Section(Terminal(withheld), "-- SHARED MUTABLE STATE"));
    }

    private static Judgement Withhold(Judgement judgement, string key) =>
        new(
        [
            .. judgement.All.Select(j =>
                string.Equals(j.Finding.Key.Canonical, key, StringComparison.Ordinal)
                    ? new Judged(j.Finding, Arbitrary)
                    : j)
        ]);

    private string Terminal(Judgement judgement) =>
        string.Join("\n", Report.For(core.Model, judgement));

    private string Page(Judgement judgement) =>
        HtmlReport.Render(core.Model, judgement, Instant);

    /// <summary>The loop line a reported cycle draws, built the way the renderer builds it.</summary>
    private string LoopLineOf(Finding cycle) =>
        "loop: " + string.Join(" -> ", PathOf(cycle).Select(NameOf))
                 + " -> " + NameOf(PathOf(cycle)[0]);

    private string LoopHtmlOf(Finding cycle) =>
        string.Join(" → ", PathOf(cycle).Select(NameOf)) + " → " + NameOf(PathOf(cycle)[0]);

    private string FirstMemberOf(Finding cycle) => NameOf(cycle.Subject.Members[0]);

    private IReadOnlyList<SubjectRef> PathOf(Finding cycle) =>
        core.Model.ShapedNamespaceCycles
            .First(c => c.Cycle.Subject.Equals(cycle.Subject))
            .Cycle.Path;

    private string NameOf(SubjectRef id) =>
        core.Model.Namespaces
            .Where(ns => SubjectRef.ForNamespace(ns.Namespace).Equals(id))
            .Select(ns => ns.Namespace)
            .FirstOrDefault() ?? id.Canonical;

    private static int NotReportedEntries(string terminal) =>
        Occurrences(Section(terminal, "   MUTUALLY DEPENDENT, NOT REPORTED ABOVE"), " namespaces, ")
        + Occurrences(Section(terminal, "   MUTUALLY DEPENDENT, NOT REPORTED ABOVE"), " namespace, ");

    private static string TangleSection(string terminal) => Section(terminal, "   TYPE TANGLES");

    /// <summary>One section of the terminal report, from its heading to the next blank-line break.</summary>
    private static string Section(string terminal, string heading)
    {
        var lines = terminal.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith(heading, StringComparison.Ordinal));

        if (start < 0) return "";

        var end = Array.FindIndex(lines, start + 1, string.IsNullOrWhiteSpace);

        return string.Join("\n", lines[start..(end < 0 ? lines.Length : end)]);
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
}

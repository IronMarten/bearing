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
    /// The fidelity decision, asserted rather than trusted. Criticality drift is absent and that
    /// is the one section deliberately not carried — it is <c>TASKS.md</c> X7, undecided, and
    /// Core has no baseline to render from.
    /// </remarks>
    [Fact]
    public void The_sections_are_the_probes_sections_in_the_probes_order()
    {
        string[] expected =
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

        Assert.Equal(expected, Lines.Where(l => l.StartsWith("-- ", StringComparison.Ordinal)));
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

        Assert.Contains("too large for anyone to hold at once", line, StringComparison.Ordinal);
        Assert.DoesNotContain("carries real logic", line, StringComparison.Ordinal);

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
    /// Rendered at a deliberately low <c>--top</c> so the caps bite. At the default of 15 nothing
    /// in the fixture is truncated, which is exactly how this defect survived: the probe's
    /// <c>Take</c> was invisible on every input anyone looked at.
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

    [Fact]
    public void Nothing_claims_to_be_shortened_when_it_is_not()
    {
        // The control for the disclosure: it has to appear exactly when it is true, or it becomes
        // noise people learn to skip — which is how the roll-call problem started. No second walk
        // needed, because nothing in the fixture reaches the default --top of 15.
        Assert.DoesNotContain("not shown of", Text, StringComparison.Ordinal);
        Assert.DoesNotContain("shown", Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the snapshot ----

    [Fact]
    public Task The_report_renders() => Verify(Text);
}

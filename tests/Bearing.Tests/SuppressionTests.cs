using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// The suppression matrix — <c>TECHREQ-job-b.md</c> §4 — tested as behaviour rather than trusted
/// as ordering.
/// </summary>
/// <remarks>
/// <para>
/// Suppression is the part of Job B most likely to be lost in extraction and least likely to
/// fail loudly when it is. A suppression that stops working produces <b>more</b> output, and
/// more output reads as a working tool. Until the fixture had cases that fire, removing any of
/// these rules turned empty output into empty output and nothing failed.
/// </para>
/// <para>
/// Each test below names a companion that satisfies <b>every other condition</b> of the finding,
/// and asserts both that it is absent and that the conditions it does meet are met. Without the
/// second half, a companion that quietly stopped qualifying for an unrelated reason would still
/// pass, and the suppression would be untested again without anyone noticing.
/// </para>
/// <para>
/// Ordering is currently load-bearing in the implementation: breaks alone captures the
/// concealed-decision nominations from earlier in the same method and tests membership. These
/// tests are what makes that safe to change — §4 requires suppression to become a declared
/// relationship between findings, evaluated before rendering, and <c>FindingKey</c> is what it
/// will be expressed against.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class SuppressionTests(FixtureRun run)
{
    /// <summary>Row 1: never imply safety at a boundary. Invariant 4.</summary>
    /// <remarks>
    /// The probe cannot see external consumers, so "if it breaks, it breaks alone" is the one
    /// claim it must not make about a type on the outside edge. A tool that says "safe to
    /// remove" about something six customers depend on has caused the burn it claimed to
    /// prevent.
    /// </remarks>
    [Fact]
    public void Breaks_alone_is_suppressed_at_a_boundary()
    {
        var boundary = Type("ReconciliationController");

        // Everything except Kind says it qualifies.
        Assert.Equal("ApiBoundary", boundary.Kind);
        Assert.True(boundary.FanIn >= 1);
        Assert.True(boundary.Instability >= 0.8);
        Assert.True(boundary.MaxMemberCyclomatic >= run.Options.HighCc);

        Assert.DoesNotContain("ReconciliationController", BreaksAlone());
    }

    /// <summary>Row 2: never contradict yourself about one component. Invariant 3.</summary>
    /// <remarks>
    /// Structural isolation is not safety when a component <i>decides</i> something — a
    /// normalizer that picks the wrong option propagates into the data going out the door, not
    /// through the call graph. Saying "breaks alone" and "this is making business judgements"
    /// about one type discredits both.
    /// </remarks>
    [Fact]
    public void Breaks_alone_is_suppressed_for_a_concealed_decision()
    {
        var concealed = Type("RateReconciler");

        Assert.Equal("Internal", concealed.Kind);
        Assert.True(concealed.FanIn >= 1);
        Assert.True(concealed.Instability >= 0.8);
        Assert.True(concealed.MaxMemberCyclomatic >= run.Options.HighCc);

        // It is genuinely nominated as a concealed decision, which is the reason for the
        // suppression rather than a coincidence.
        Assert.Contains("RateReconciler", ConcealedDecisions());
        Assert.DoesNotContain("RateReconciler", BreaksAlone());
    }

    /// <summary>Row 3: fan-in of zero is unreferenced code, not reassurance.</summary>
    [Fact]
    public void Breaks_alone_is_suppressed_when_nothing_references_it()
    {
        var orphan = Type("AuditReconciler");

        Assert.Equal("Internal", orphan.Kind);
        Assert.Equal(0, orphan.FanIn);
        Assert.True(orphan.Instability >= 0.8);
        Assert.True(orphan.MaxMemberCyclomatic >= run.Options.HighCc);

        // And it is not suppressed by row 2 instead — that would make this test pass for the
        // wrong reason.
        Assert.DoesNotContain("AuditReconciler", ConcealedDecisions());
        Assert.DoesNotContain("AuditReconciler", BreaksAlone());
    }

    /// <summary>
    /// The control: with the three suppressions accounted for, the finding still fires on the
    /// type it should.
    /// </summary>
    /// <remarks>
    /// A suppression suite that only asserts absence would pass just as happily if the finding
    /// were deleted outright.
    /// </remarks>
    [Fact]
    public void Breaks_alone_still_fires_on_the_type_that_earns_it()
    {
        Assert.Contains("TariffReconciler", BreaksAlone());
    }

    private TypeMetrics Type(string name) =>
        run.Result.Types.Single(t => t.Name == name);

    private string[] BreaksAlone() =>
        NominationText.SubjectsUnder(
            NominationText.Render(run.Result, run.Options), "-- BREAKS ALONE");

    /// <summary>
    /// Type-level concealed-decision subjects. The section renders <c>Type.Member</c>, so the
    /// subject is trimmed back to the type.
    /// </summary>
    private string[] ConcealedDecisions() =>
        NominationText.SubjectsUnder(
                NominationText.Render(run.Result, run.Options), "-- CONCEALED DECISION -")
            .Select(s => s.Split('.')[0])
            .ToArray();
}

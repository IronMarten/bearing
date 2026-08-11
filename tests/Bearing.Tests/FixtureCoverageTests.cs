namespace Bearing.Tests;

/// <summary>
/// What the fixture does <b>not</b> cover, asserted rather than assumed.
/// </summary>
/// <remarks>
/// Invariant 8 says silence must never read as a clean bill of health. That applies to this
/// suite as much as to the tool: a green run says every assertion held, not that everything
/// is checked. These tests name the holes so they stay visible, and fail the day a hole is
/// filled — at which point the known answer gets recorded in <c>docs/TESTING.md</c> §6 and
/// the assertion here is narrowed.
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class FixtureCoverageTests(FixtureRun run)
{
    /// <summary>
    /// BUG BLAST RADIUS and BREAKS ALONE nominate nothing on TestBed, so the frozen goldens
    /// carry no record of how either one behaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the important consequence, and it is not obvious: a section that emits no
    /// output produces the same bytes whatever its thresholds are. Five of the thirteen
    /// unnamed gate literals in Job B live inside these two findings — blast radius'
    /// <c>2.0</c> fan-in multiple, its <c>95</c> and <c>70</c> percentile floors, and breaks
    /// alone's <c>0.8</c> instability floor and <c>1</c> fan-in floor. Every one of them
    /// could be changed to any other value, or the findings deleted outright, and
    /// <c>OracleGoldenTests</c> would still pass byte-for-byte.
    /// </para>
    /// <para>
    /// Breaks alone is the worse of the two: it carries three of Job B's seven suppression
    /// rules (<c>TECHREQ-job-b.md</c> §4), including the invariant-4 boundary exclusion and
    /// the invariant-3 concealed-decision exclusion. Nothing currently fails if a suppression
    /// is removed, because removing it changes empty output into empty output.
    /// </para>
    /// <para>
    /// The fix is fixture cases, not test changes — <c>TECHREQ-job-b.md</c> §8 and
    /// <c>docs/TESTING.md</c> §6. Add, do not reshape.
    /// </para>
    /// </remarks>
    [Fact]
    public void Blast_radius_and_breaks_alone_have_no_fixture_case()
    {
        var text = NominationText.Render(run.Result, run.Options);

        Assert.Empty(NominationText.SubjectsUnder(text, "-- BUG BLAST RADIUS"));
        Assert.Empty(NominationText.SubjectsUnder(text, "-- BREAKS ALONE"));

        // Asserted alongside so this reads as a gap in two findings rather than a fact about
        // two arbitrary strings. If these ever empty out, the parser broke, not the tool.
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CONCEALED DECISION -"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CONCEALED DECISION, METHOD LEVEL"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CHANGE COST"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- LOAD-BEARING AND INTRICATE"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- SHARED MUTABLE STATE"));
    }
}

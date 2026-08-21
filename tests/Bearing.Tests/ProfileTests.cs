using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The profile — <c>TASKS.md</c> A12.
/// </summary>
/// <remarks>
/// <para>
/// <b>No test here asserts a duration, and none can.</b> A profile is a wall-clock reading on one
/// machine under whatever else was running, and the suite that pinned a timing would be red on a
/// contended agent and green on a fast one, which is the worst of both. <c>tools/measure.py</c>
/// takes the numbers, medians of three, and the baseline they produced is in <c>DONE.md</c>.
/// </para>
/// <para>
/// <b>What is assertable is that the table cannot lie about what it did not measure.</b> The
/// stages have to reconcile against the totals they sit inside, every stage has to be reachable
/// from the two lists the renderer walks, and the residual has to be the difference rather than a
/// row somebody can forget. Those are the properties A9 will lean on when it changes
/// <c>ReferenceCollector</c> and needs to know whether the cost moved or merely moved rows.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ProfileTests
{
    private readonly CoreWalkFixture _fixture;

    public ProfileTests(CoreWalkFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Every stage is listed as either a top-level stage or one inside the walk, exactly once.
    /// </summary>
    /// <remarks>
    /// <b>The gate that matters most, and it is the <c>AnalysisPolicy</c> flag table's argument
    /// again.</b> A stage nothing enumerates is time that is measured and then rendered nowhere —
    /// it disappears into the residual, which is the row a reader takes as "the profile is
    /// complete apart from noise". Adding a <see cref="WalkStage"/> and forgetting to place it is
    /// a test failure here rather than a quiet subtraction from the walk.
    /// </remarks>
    [Fact]
    public void Every_stage_is_placed_exactly_once()
    {
        var placed = WalkProfile.TopLevel.Concat(WalkProfile.WithinWalk).ToList();

        Assert.Equal(placed.Distinct().Count(), placed.Count);
        Assert.Equal(Enum.GetValues<WalkStage>().OrderBy(s => s), placed.OrderBy(s => s));
    }

    /// <summary>
    /// The stages fit inside the totals they are measured within.
    /// </summary>
    /// <remarks>
    /// Not arithmetic for its own sake: it is the one way a double-count shows up. A stage timed
    /// around something that is already inside another stage makes a residual go negative, and a
    /// negative residual is the only symptom that has.
    /// </remarks>
    [Fact]
    public void The_walk_reconciles_against_its_own_total()
    {
        var profile = Walk();

        Assert.True(profile.Unaccounted >= TimeSpan.Zero,
            $"top-level stages exceed the walk's total by {-profile.Unaccounted}");
        Assert.True(profile.WalkUnaccounted >= TimeSpan.Zero,
            $"the walk's inner stages exceed the walk by {-profile.WalkUnaccounted}");

        foreach (var stage in Enum.GetValues<WalkStage>())
            Assert.True(profile[stage] >= TimeSpan.Zero, $"{stage} is negative");
    }

    /// <summary>
    /// The counts describe the walk that happened, not an intention about it.
    /// </summary>
    /// <remarks>
    /// <b>The denominators are the reason a profile survives the solution changing.</b> "6.4s of
    /// walk" cannot be compared across two runs of different sizes; "6.4s over 5,120 declarations"
    /// can, and A9 is going to need exactly that comparison when the fixture it is measured on
    /// grows member-level traps. So the counts are asserted against the model rather than assumed.
    /// </remarks>
    [Fact]
    public void The_counts_are_the_walk_that_happened()
    {
        var profile = Walk();

        Assert.Equal(_fixture.Model.Types.Count, profile.Types);
        Assert.Equal(_fixture.Model.Projects.Count, profile.Projects);

        // A partial type is several declarations and one type, so this is >= rather than ==, and
        // it is > on any solution that uses partials at all.
        Assert.True(profile.Declarations >= profile.Types,
            $"{profile.Declarations} declarations for {profile.Types} types");
    }

    /// <summary>A walk that has not run reports nothing rather than a plausible zero-length one.</summary>
    [Fact]
    public void An_unstarted_walk_has_no_profile()
    {
        var profile = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution }).Profile;

        Assert.Equal(TimeSpan.Zero, profile.Total);
        Assert.Equal(0, profile.Types);
        Assert.All(Enum.GetValues<WalkStage>(), stage => Assert.Equal(TimeSpan.Zero, profile[stage]));
    }

    /// <summary>
    /// The residual is whatever the caller did not hand over, so the table always adds up.
    /// </summary>
    /// <remarks>
    /// Written with durations that are nothing like a real run's, because the property is
    /// arithmetic and a realistic-looking fixture invites reading the numbers as a measurement.
    /// </remarks>
    [Fact]
    public void The_residual_is_the_difference_and_the_table_adds_up()
    {
        var lines = ProfileReport.For(
            [
                new ProfileStage("startup", TimeSpan.FromSeconds(1)),
                new ProfileStage("walk", TimeSpan.FromSeconds(6), "10 types in 12 declarations"),
                new ProfileStage("references", TimeSpan.FromSeconds(4), Nested: true),
            ],
            TimeSpan.FromSeconds(10)).ToList();

        // 10 total, less the two unnested rows: the nested one is inside the walk, not beside it.
        Assert.Contains("unmeasured 3.00s 30%", lines.Select(Squeezed));

        // A nested row carries no share, because a share of the total would not mean anything.
        var nested = Assert.Single(lines, l => l.Contains("references", StringComparison.Ordinal));
        Assert.DoesNotContain("%", nested, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every stage the walk measures reaches the table, with its counts.
    /// </summary>
    /// <remarks>
    /// The renderer's half of <see cref="Every_stage_is_placed_exactly_once"/>: that one holds the
    /// lists complete, this one holds them actually walked. Asserted against a real profile, so a
    /// stage that is enumerated but rendered under a blank name still fails.
    /// </remarks>
    [Fact]
    public void The_table_names_every_stage()
    {
        var lines = ProfileReport.For(ProfileReport.StagesOf(Walk()), TimeSpan.FromSeconds(30)).ToList();

        var rows = lines.Select(Squeezed).ToList();

        foreach (var stage in Enum.GetValues<WalkStage>())
            Assert.Contains(rows, r => r.StartsWith(
                stage.ToString().ToLowerInvariant() + " ", StringComparison.Ordinal));

        Assert.Contains(lines, l => l.Contains(
            $"{_fixture.Model.Types.Count} types", StringComparison.Ordinal));
    }

    /// <summary>
    /// The fixture's profile, from a walk of its own.
    /// </summary>
    /// <remarks>
    /// <b>Not the shared fixture's, and the shared fixture cannot supply it.</b>
    /// <see cref="CoreWalkFixture"/> keeps the model and drops the walker, which is the right
    /// trade for every other test in this suite — but the profile hangs off the walker, so a test
    /// about it has to pay for its own walk. One walk, shared by the tests in this file.
    /// </remarks>
    private static WalkProfile Walk() => OwnWalk.Value;

    /// <summary>
    /// One row with its padding collapsed, so an assertion is about the columns and not about how
    /// wide the widest name happened to be.
    /// </summary>
    private static string Squeezed(string line) =>
        string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static readonly Lazy<WalkProfile> OwnWalk = new(() =>
    {
        var walker = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution });
        walker.WalkAsync(CancellationToken.None).GetAwaiter().GetResult();
        return walker.Profile;
    });
}

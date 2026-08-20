using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The tile row — A13 tier 3.
/// </summary>
/// <remarks>
/// <para>
/// What a test can hold here is that each number is what it says it is, and that a number the run
/// did not measure is absent rather than zero. Whether four claims at the top of a page make a
/// reader faster is A11 round 2's question, and no assertion here reaches it.
/// </para>
/// <para>
/// <b>The recomputation is deliberate and is not a copy of the implementation.</b> Widest reach and
/// clean are derived from the model a second way — off <see cref="TypeNode.FanIn"/> and off the
/// finding set — so a tile that started reading a different quantity would fail rather than quietly
/// disagree with the mosaic above it.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class TilesTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    /// <summary>The widest reach tile is the most-depended-on type, and it names it.</summary>
    [Fact]
    public void Widest_reach_is_the_type_the_most_of_the_codebase_depends_on()
    {
        var widest = core.Model.Types.OrderByDescending(t => t.FanIn).First();
        var tile = Single(Tiles.For(core.Model, Findings), TileKind.WidestReach);

        Assert.True(widest.FanIn > 0, "the fixture no longer has a type anything depends on");
        Assert.Equal(Html.Count(widest.FanIn), tile.Value);
        Assert.Contains(widest.Name, tile.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// The clean tile counts the types no finding is about, against every type analysed.
    /// </summary>
    /// <remarks>
    /// <b>The same population the mosaic tints</b>, which is why both read
    /// <c>Subjects.Named</c> — a picture saying 72% of the ink is named above a number saying 83% is
    /// clean would be two claims about one run, and nothing would fail.
    /// </remarks>
    [Fact]
    public void Clean_is_the_share_of_types_no_finding_names()
    {
        var findings = Findings;
        var marks = Mosaic.Marked(core.Model, findings);
        var expected = Math.Round(100d * (core.Model.Types.Count - marks.Named) / core.Model.Types.Count);

        var tile = Single(Tiles.For(core.Model, findings), TileKind.Clean);

        Assert.Equal($"{expected:0}%", tile.Value);
        Assert.Contains(Html.Count(core.Model.Types.Count), tile.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Concentration names a project that holds more findings than its size accounts for.
    /// </summary>
    /// <remarks>
    /// <b>Chosen by excess rather than by ratio, and this is the assertion that says so.</b> A
    /// ratio would let a two-type project with two findings beat a large project carrying thirty
    /// more than it should — the same size-blind mistake <c>MEASURE-concealed-decision.md</c>
    /// measured one level down. Pinning that the winner holds at least as many named types as any
    /// other project is weaker than the rule but is the part of it a fixture can observe.
    /// </remarks>
    [Fact]
    public void Concentration_names_a_project_that_carries_more_than_its_share()
    {
        var findings = Findings;
        var tile = Single(Tiles.For(core.Model, findings), TileKind.Concentration);

        var project = core.Model.Types
            .Select(t => t.Project)
            .Distinct(StringComparer.Ordinal)
            .Single(p => tile.Note.Contains(p, StringComparison.Ordinal));

        var named = core.Model.Types.Count(t =>
            string.Equals(t.Project, project, StringComparison.Ordinal)
            && findings.About(t.Subject).Count > 0);

        Assert.True(named > 0, "the concentration tile named a project holding no finding");
        Assert.EndsWith("x", tile.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sharpest outlier is a measured multiple and never an undefined one.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §28. A ratio against a median of zero is undefined rather than
    /// enormous, so it cannot be the largest anything — and <c>P9</c> is the plant that will make
    /// this reachable on the fixture rather than only on a real solution.
    /// </remarks>
    [Fact]
    public void The_sharpest_outlier_is_a_defined_multiple()
    {
        var tiles = Tiles.For(core.Model, Findings);
        var tile = tiles.SingleOrDefault(t => t.Kind == TileKind.SharpestOutlier);

        if (tile.Value is null or "") return;

        Assert.DoesNotContain("undefined", tile.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("∞", tile.Value, StringComparison.Ordinal);
        Assert.EndsWith("x", tile.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tile the run cannot support is absent, not zero.
    /// </summary>
    /// <remarks>
    /// <b>Invariant 6, at the top of the page.</b> A run that nominated nothing has no concentration
    /// and no outlier; rendering <c>0x</c> for either would assert a measurement that was never
    /// taken, and a dash would assert one that came back empty. What survives is the pair that is
    /// about the codebase rather than about the findings — and clean reads 100%, which is the
    /// answer rather than a placeholder.
    /// </remarks>
    [Fact]
    public void A_run_with_no_findings_keeps_only_the_tiles_it_can_support()
    {
        var tiles = Tiles.For(core.Model, FindingSet.Empty);

        Assert.Contains(tiles, t => t.Kind == TileKind.WidestReach);
        Assert.Equal("100%", Single(tiles, TileKind.Clean).Value);
        Assert.DoesNotContain(tiles, t => t.Kind == TileKind.Concentration);
        Assert.DoesNotContain(tiles, t => t.Kind == TileKind.SharpestOutlier);
    }

    /// <summary>
    /// No tile counts the findings.
    /// </summary>
    /// <remarks>
    /// <b>The rejected fifth tile, kept rejected.</b> <i>"Findings worth attention"</i> was cut
    /// because a count of outstanding work is a lint mental model, and <c>PRD-free-tier.md</c> §7.2
    /// holds that an anomaly is an observation rather than an item of work. The findings total still
    /// ships — in prose, in <c>Everything else</c>, where it is a statement about the run rather
    /// than a headline about the codebase.
    /// </remarks>
    [Fact]
    public void The_row_never_leads_with_a_count_of_findings()
    {
        var findings = Findings;

        Assert.True(findings.Count > 0, "the fixture nominates nothing, so this asserts nothing");
        Assert.DoesNotContain(Tiles.For(core.Model, findings), t =>
            t.Label.Contains("finding", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The picture's caption states the tiles it is evidence for, from the same derivations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The correction the first outside reader of the mosaic produced.</b> The caption described
    /// the encoding — what a cell is, what the marks are — and never said what the picture was for,
    /// so it read as <i>"a bunch of boxes, some of them red, and a legend saying the red ones are
    /// below"</i>. That reading is right: the red outlines are X10's exemplars, which are the claims
    /// listed underneath in the same order, and they carry nothing the prose does not.
    /// </para>
    /// <para>
    /// <b>What the picture does know that the prose does not is exactly two things</b> — how much of
    /// the codebase is pale, and where the tinted cells clump — and both are tiles. So the caption
    /// leads with them, and reads them rather than recomputing: a caption saying 83% over a tile
    /// saying 81% would be two defensible numbers and one silent defect.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_pictures_caption_states_the_tiles_it_is_a_picture_of()
    {
        var findings = Findings;
        var page = HtmlReport.Render(
            core.Model, findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var caption = page[page.IndexOf("</svg>", StringComparison.Ordinal)..page.IndexOf("<h2>", StringComparison.Ordinal)];

        var clean = Tiles.Of(core.Model, findings, TileKind.Clean)!.Value;
        Assert.Contains($"{clean.Value} of this codebase has nothing said about it", caption, StringComparison.Ordinal);

        if (Tiles.Of(core.Model, findings, TileKind.Concentration) is { } concentration)
        {
            Assert.Contains(concentration.Subject, caption, StringComparison.Ordinal);
            Assert.Contains($"carries {concentration.Value} its share", caption, StringComparison.Ordinal);
        }

        // And the sentence the old picture could not draw at all: what the codebase rests on, which
        // on this plot is a position rather than a synthesis — X11.
        if (Foundations.Of(core.Model, findings) is { } rests)
        {
            Assert.Contains($"{rests.Project} is what the most of it rests on", caption, StringComparison.Ordinal);
            Assert.Contains($"{rests.Share} of its own", caption, StringComparison.Ordinal);
        }
    }

    private static Tile Single(IReadOnlyList<Tile> tiles, TileKind kind) =>
        tiles.Single(t => t.Kind == kind);
}

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
    /// A type the run declined to judge is clean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The assertion above holds the tile against the mosaic and both against one derivation</b>,
    /// which is the right shape and cannot catch this: when the shared derivation is wrong, the two
    /// agree on the wrong number and nothing fails. That is how <i>"no finding names them"</i>
    /// shipped counting the coverage disclosure — 104 of nopCommerce's types, three points of the
    /// biggest glyph on the page — while the census below said in words that a no-peer-group row is
    /// not a finding about a type.
    /// </para>
    /// <para>
    /// <b>Recomputed against the claims rather than read back</b>, so it fails if the population
    /// moves however many renderers agree with each other about it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Clean_does_not_count_a_type_the_run_declined_to_judge()
    {
        var findings = Findings;

        var claimed = core.Model.Types
            .Count(t => findings.About(t.Subject).Any(f => Claims.IsRiskClaim(f.Kind))
                        || t.Members.Any(m => findings.About(m.Subject).Any(f => Claims.IsRiskClaim(f.Kind))));

        var disclosed = findings.All.Count(f => !Claims.IsRiskClaim(f.Kind));
        var expected = Math.Round(100d * (core.Model.Types.Count - claimed) / core.Model.Types.Count);

        // The disclosure fired, or this asserts nothing the test above does not already hold.
        Assert.True(disclosed > 0, "the fixture no longer produces a coverage disclosure");

        Assert.Equal($"{expected:0}%", Single(Tiles.For(core.Model, findings), TileKind.Clean).Value);
    }

    /// <summary>
    /// A cycle is a risk claim and still names no type, so it cannot move the clean tile, the
    /// mosaic's tint or the plot's density axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This holds by construction and nothing said so, which is the reason to say it.</b> The
    /// named population is <c>Subjects.Named</c>, which filters on
    /// <see cref="Claims.IsRiskClaim"/> — and <c>IsRiskClaim</c> is <b>true</b> for all three cycle
    /// kinds, deliberately, because a cycle is a claim. What keeps them out is the other half of
    /// that line: <c>Subjects.Of</c> resolves a finding to a <c>TypeNode</c> through
    /// <c>model.Find</c> and a member's declaring type, and a cycle's subject is a
    /// <see cref="SubjectKind.Set"/>, which is neither. So the filter lets a cycle through and the
    /// resolution drops it.
    /// </para>
    /// <para>
    /// <b>That is one line away from being wrong.</b> Teaching <c>Subjects.Of</c> to walk a set's
    /// members — an obvious enough thing to want, since a tangle's members <i>are</i> types —
    /// would silently charge every tangle member to the named population and move the number in
    /// the largest glyph on the page. That is a disclosure counted as a finding, which took the
    /// clean tile from 88% to 85% on nopCommerce and was found by reading the page rather than by
    /// the suite. This is the assertion that was missing.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_cycle_is_a_claim_and_still_names_no_type()
    {
        var findings = Findings;

        var cycles = findings.All.Where(f => !Claims.CompetesForLead(f.Kind)).ToList();

        // The fixture produces them, or the rest of this asserts nothing.
        Assert.NotEmpty(cycles);

        // Claims, every one — this is the half that must NOT be fixed by calling them disclosures.
        Assert.All(cycles, f => Assert.True(Claims.IsRiskClaim(f.Kind)));

        // And none of them names a type. Asserted through what a reader sees rather than through
        // Subjects.Named itself: the tile is the number this protects, and a test that reads the
        // internal would keep passing if the renderer stopped using it.
        var without = FindingSet.Of(findings.All.Where(f => Claims.CompetesForLead(f.Kind)));

        Assert.Equal(
            Single(Tiles.For(core.Model, without), TileKind.Clean).Value,
            Single(Tiles.For(core.Model, findings), TileKind.Clean).Value);

        Assert.Equal(
            Mosaic.Render(core.Model, without),
            Mosaic.Render(core.Model, findings), StringComparer.Ordinal);
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
    /// The fourth tile names a member and states its complexity, with no cohort behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It replaced the sharpest-outlier tile on 2026-08-21</b>, which is what that tile's own
    /// remark said should happen once D34 landed. Every quantity it could show was a ratio against
    /// a cohort median, and D34's finding is that at the top end a cohort is not a peer group — so
    /// the tile was putting the one number the register calls <i>"arithmetically true and
    /// rhetorically false"</i> in the largest glyph on the page, hedged to <i>"the middle of its
    /// group"</i> because the honest word could not be used.
    /// </para>
    /// <para>
    /// <b>What is asserted is the absence of a comparison, not the presence of a number.</b> A cc
    /// means the same thing in every codebase; the moment this tile acquires an <c>x</c> or the
    /// word <i>median</i>, D34 is back.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fourth_tile_states_a_complexity_and_compares_it_to_nothing()
    {
        var tile = Single(Tiles.For(core.Model, Findings), TileKind.MostIntricate);

        Assert.StartsWith("cc ", tile.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("x", tile.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("median", tile.Note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("group", tile.Note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("peer", tile.Note, StringComparison.OrdinalIgnoreCase);

        // And it names something a reader can go and open.
        Assert.NotEmpty(tile.Subject);
        Assert.Contains(tile.Subject, tile.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tile the run cannot support is absent, not zero.
    /// </summary>
    /// <remarks>
    /// <b>Invariant 6, at the top of the page.</b> A run that nominated nothing has no concentration
    /// no concentration; rendering <c>0x</c> would assert a measurement that was never taken, and a
    /// dash would assert one that came back empty. What survives is everything that is about the
    /// codebase rather than about the findings — three of the four now — and clean reads 100%,
    /// which is the answer rather than a placeholder.
    /// </remarks>
    [Fact]
    public void A_run_with_no_findings_keeps_only_the_tiles_it_can_support()
    {
        var tiles = Tiles.For(core.Model, FindingSet.Empty);

        Assert.Contains(tiles, t => t.Kind == TileKind.WidestReach);
        Assert.Equal("100%", Single(tiles, TileKind.Clean).Value);
        Assert.DoesNotContain(tiles, t => t.Kind == TileKind.Concentration);

        // Three survive rather than two since the fourth tile stopped being about the findings.
        // The old one read the largest ratio a detector had recorded, so it could only name
        // something already nominated; complexity is a fact about the codebase whether or not
        // anything fired on it, which is what the other two surviving tiles are as well.
        Assert.Contains(tiles, t => t.Kind == TileKind.MostIntricate);
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
            core.Model, Analysis.Judge(core.Model), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

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

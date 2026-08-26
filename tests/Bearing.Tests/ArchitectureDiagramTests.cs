using System.Text.RegularExpressions;
using System.Xml.Linq;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The project map — <c>TECHREQ-job-a.md</c> §5.4, shipped at A7.
/// </summary>
/// <remarks>
/// <para>
/// The layering and the fold are <c>ProjectGraphTests</c>'; these are about the drawing. The
/// acceptance criterion §5.4 sets — <i>legible at screenshot size on a 30-project solution with no
/// interaction</i> — is not something a test can assert, so what is asserted here is the two
/// mechanical properties legibility rests on: it is valid SVG, and no two boxes carry the same
/// label.
/// </para>
/// <para>
/// The real measurement is a real solution, and it reproduced the spike's number: nopCommerce
/// draws at <b>580 × 642px in 10 boxes</b> where the spike's unfolded map was <b>1444px at 27</b>.
/// Jellyfin, which is not a plugin host and folds almost not at all, comes out 952 × 1074px at 21.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ArchitectureDiagramTests(CoreWalkFixture core)
{
    private string Svg => ArchitectureDiagram.Render(core.Model);

    [Fact]
    public Task The_diagram_renders() => Verify(Svg, extension: "svg");

    /// <summary>
    /// Every edge is painted after every box, so a line that crosses one stays a line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The paint order was the whole mechanism.</b> Boxes are
    /// opaque. Painted last, they cut any line that passes behind them into two stubs, and two
    /// stubs either side of a box read as a dependency into it and another out of it — so a direct
    /// dependency reads as a chain through a project it never names. It is not a rare case:
    /// drawing every edge put <b>18 of 29</b> lines through a box on nopCommerce, <b>81 of 98</b>
    /// on Jellyfin and <b>27 of 44</b> on Umbraco.
    /// </para>
    /// <para>
    /// <b>The fixture cannot show the misreading</b> — three projects in a chain, no line has
    /// anything to cross — so this asserts the property the fix is, which is document order.
    /// <c>ProjectGraphTests</c> holds the other half, the reduction that makes the remaining
    /// crossings few enough to live with.
    /// </para>
    /// </remarks>
    [Fact]
    public void Edges_are_painted_over_the_boxes_rather_than_under_them()
    {
        var svg = Svg;

        var lastBox = svg.LastIndexOf("<rect", StringComparison.Ordinal);
        var firstEdge = svg.IndexOf("<path class=\"ed\"", StringComparison.Ordinal);

        Assert.True(lastBox >= 0, "the fixture draws boxes");
        Assert.True(firstEdge >= 0, "the fixture draws at least one dependency");
        Assert.True(firstEdge > lastBox, "an edge painted before a box is hidden by it");
    }

    /// <summary>
    /// Nothing wraps on the fixture, so the drawing spends no ink saying which gaps are real.
    /// </summary>
    /// <remarks>
    /// The dashed rules exist to tell a layer boundary from a
    /// wrapped one, and where no layer wrapped there is no such distinction to draw — the same
    /// rule <see cref="ArchitectureDiagram.Tinted"/> follows, which is to key what fired rather
    /// than what exists. nopCommerce and Umbraco draw none; Jellyfin draws seven.
    /// <c>ProjectGraphTests</c> holds the wrapping case, which needs a layer the fixture cannot
    /// have.
    /// </remarks>
    [Fact]
    public void A_drawing_with_no_wrapped_layer_draws_no_layer_rules()
    {
        Assert.False(ArchitectureDiagram.Wraps(core.Model.ProjectGraph));
        Assert.DoesNotContain("class=\"lr\"", Svg, StringComparison.Ordinal);
    }

    /// <summary>It is well-formed XML, which is what makes it an image rather than a string.</summary>
    /// <remarks>
    /// An SVG with an unescaped project name in it does not render badly — it fails to parse and
    /// shows nothing at all, and the artifact whose whole job is being pasted somewhere is blank.
    /// </remarks>
    [Fact]
    public void The_diagram_is_well_formed_xml()
    {
        var document = XDocument.Parse(Svg);

        Assert.Equal("svg", document.Root!.Name.LocalName);
        Assert.Equal("http://www.w3.org/2000/svg", document.Root.Name.NamespaceName);
    }

    /// <summary>
    /// No two boxes carry the same label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found on Jellyfin, which declares both <c>Emby.Server.Implementations</c> and
    /// <c>Jellyfin.Server.Implementations</c>.</b> Shortening each to its last segment drew two
    /// different projects under one label, in a picture with nothing else on it to tell them
    /// apart — a walker's mistake made by a renderer instead: a
    /// display name that is not an identity.
    /// </para>
    /// <para>
    /// The fixture cannot show it — three projects, no shared tail — so this asserts the property
    /// rather than the case, and <c>ProjectGraphTests</c> holds the shapes.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_two_boxes_share_a_label()
    {
        var labels = Labels(Svg);

        Assert.NotEmpty(labels);
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every project analysed appears in exactly one box.</summary>
    /// <remarks>
    /// A folded box stands for several projects, so the count of boxes is not the count of
    /// projects — but every project has to be inside one of them, or the drawing is quietly
    /// omitting part of the solution while looking complete.
    /// </remarks>
    [Fact]
    public void Every_project_is_in_exactly_one_box()
    {
        var placed = core.Model.ProjectGraph.Groups.SelectMany(g => g.Projects).ToList();

        Assert.Equal(placed.Count, placed.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            core.Model.ProjectCouplings.Select(c => c.Project).OrderBy(p => p, StringComparer.Ordinal),
            placed.OrderBy(p => p, StringComparer.Ordinal));
    }

    /// <summary>
    /// Martin's numbers appear as words, never as a number on a box.
    /// </summary>
    /// <remarks>
    /// §5.4 is explicit: <i>"stable and concrete: everything depends on it, nothing can extend it
    /// without modifying it", never <c>D = 0.42</c></i>. A number on a box invites ranking boxes
    /// against each other, which is what this tool declines to do everywhere else — so the absence
    /// is the assertion.
    /// </remarks>
    [Fact]
    public void The_diagram_carries_no_scores()
    {
        var svg = Svg;

        Assert.DoesNotContain("D =", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("I =", svg, StringComparison.Ordinal);

        // Any decimal in text content would be a metric leaking into a label; the geometry is
        // whole pixels, so there should be none anywhere a reader looks.
        foreach (var text in Regex.Matches(svg, "<text[^>]*>([^<]*)</text>").Select(m => m.Groups[1].Value))
            Assert.DoesNotMatch(@"\d\.\d", text);
    }

    /// <summary>The drawing fetches nothing, like the page that embeds it.</summary>
    [Fact]
    public void The_diagram_requests_nothing_from_the_network()
    {
        Assert.DoesNotContain("http://www.w3.org/1999/xlink", Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<image", Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("@import", Svg, StringComparison.Ordinal);
    }

    /// <summary>The same drawing appears in the HTML report, from one renderer.</summary>
    /// <remarks>
    /// Two copies would drift, and the one people screenshot is not necessarily the one that gets
    /// looked at first — <c>docs/ARCHITECTURE.md</c> §3.
    /// </remarks>
    [Fact]
    public void The_html_report_embeds_the_same_drawing()
    {
        var page = HtmlReport.Render(
            core.Model, Analysis.FindingsFor(core.Model), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains(Svg, page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every project in a folded box is named inside that box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reopened 2026-08-22. The first fix put the names in a legend
    /// beside the diagram <i>instead of</i> in the boxes, and read A11 round 1's T2 result — the
    /// flat Projects list beating the map at finding tax — as evidence that readers navigate by
    /// list. <b>Both of nopCommerce's tax projects were inside folded boxes whose labels named
    /// neither</b>, so the map was graded on a task whose answer it had hidden.
    /// </para>
    /// <para>
    /// <b>Asserted against the drawing, not the legend</b>, because the legend is
    /// <c>HtmlReport</c>'s and the standalone <c>--diagram</c> export has none — which is where
    /// hiding a name costs the most, §5.4 asking that file to survive being pasted into Slack.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_project_in_a_folded_box_is_named_in_the_drawing()
    {
        var svg = Svg;
        var folded = core.Model.ProjectGraph.Groups.Where(g => g.Size > 1).ToList();

        Assert.True(folded.Count > 0, "the fixture folds no boxes, so this asserts nothing");

        var members = Regex
            .Matches(svg, """<text class="mb"[^>]*>([^<]*)</text>""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        // One line per project in a folded box, and nothing else drawn as a member.
        Assert.Equal(folded.Sum(g => g.Size), members.Count);
        Assert.Equal(members.Count, members.Distinct(StringComparer.Ordinal).Count());

        foreach (var group in folded)
            foreach (var project in group.Projects)
                Assert.Contains(
                    members,
                    m => project.EndsWith(m.TrimEnd('…'), StringComparison.Ordinal)
                         || project.StartsWith(m.TrimEnd('…'), StringComparison.Ordinal));
    }

    /// <summary>
    /// A folded box grows downward and never sideways.
    /// </summary>
    /// <remarks>
    /// The measurement the reopening turned on. <see cref="ArchitectureDiagram"/> shortens a
    /// label to twenty characters, so a member line cannot outrun the box it sits in — which is
    /// why naming everyone costs height and no width. nopCommerce went 580 × 642 to 580 × 841.
    /// If a future label rule lets a name run wider, this fails rather than the drawing quietly
    /// overflowing its boxes.
    /// </remarks>
    [Fact]
    public void Naming_the_members_costs_height_and_not_width()
    {
        var boxes = Regex
            .Matches(Svg, """<rect class="[^"]*" x="(\d+)" y="\d+" width="(\d+)" height="(\d+)""")
            .Select(m => (X: int.Parse(m.Groups[1].Value), W: int.Parse(m.Groups[2].Value),
                          H: int.Parse(m.Groups[3].Value)))
            .ToList();

        Assert.NotEmpty(boxes);
        Assert.Single(boxes.Select(b => b.W).Distinct());
        Assert.True(boxes.Any(b => b.H > boxes.Min(x => x.H)), "no box grew, so this asserts nothing");
    }

    private static List<string> Labels(string svg) =>
        [.. Regex.Matches(svg, """<text class="nm"[^>]*>([^<]*)</text>""").Select(m => m.Groups[1].Value)];

    /// <summary>
    /// Every tint the map can draw has a name and a meaning to key it with.
    /// </summary>
    /// <remarks>
    /// <b>Asserted rather than observed.</b> The defect was an orange
    /// box with no key; the fix is a caption built from <see cref="ArchitectureDiagram.Tinted"/>,
    /// which reports what was drawn. That caption is only as good as the words behind each zone,
    /// and <b>the <c>useless</c> tint fires on none of nopCommerce, Jellyfin, Umbraco or TestBed</b>
    /// — it was filed as <i>unobserved rather than fine</i>, and it still is. So the property is
    /// stated over the enum instead of over a run: a zone a box can be tinted for that has no name
    /// or no gloss would ship a keyless colour again, and nothing in a golden would say so.
    /// </remarks>
    [Fact]
    public void Every_tint_the_map_can_draw_is_keyed()
    {
        foreach (var zone in new[] { MainSequenceZone.Pain, MainSequenceZone.Uselessness })
        {
            Assert.False(string.IsNullOrWhiteSpace(Sentences.Zone(zone)), $"{zone} has no name");
            Assert.False(string.IsNullOrWhiteSpace(Sentences.ZoneMeans(zone)), $"{zone} has no gloss");
        }
    }

    /// <summary>The key describes the tints that were drawn, and no others.</summary>
    /// <remarks>
    /// A fixed two-entry key would define a colour that is not on the page, and a reader hunting
    /// for a described colour they cannot find concludes they missed something rather than that
    /// the key is over-complete. TestBed tints one zone, so this holds it to one.
    /// </remarks>
    [Fact]
    public void The_key_lists_only_the_tints_that_fired()
    {
        var tinted = ArchitectureDiagram.Tinted(core.Model);

        Assert.NotEmpty(tinted);
        Assert.All(tinted, zone => Assert.Contains($"bx {Css(zone)}", Svg, StringComparison.Ordinal));

        foreach (var absent in new[] { MainSequenceZone.Pain, MainSequenceZone.Uselessness }.Except(tinted))
            Assert.DoesNotContain($"bx {Css(absent)}", Svg, StringComparison.Ordinal);
    }

    private static string Css(MainSequenceZone zone) =>
        zone == MainSequenceZone.Pain ? "pain" : "useless";
}

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
    /// apart — <c>docs/DEFECTS.md</c> §1's mistake made by a renderer rather than a walker: a
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
    /// <c>docs/DEFECTS.md</c> §31, reopened 2026-08-22. The first fix put the names in a legend
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
    /// The measurement §31's reopening turned on. <see cref="ArchitectureDiagram"/> shortens a
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
}

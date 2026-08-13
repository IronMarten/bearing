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

    private static List<string> Labels(string svg) =>
        [.. Regex.Matches(svg, """<text class="nm"[^>]*>([^<]*)</text>""").Select(m => m.Groups[1].Value)];
}

using System.Text.RegularExpressions;
using System.Xml.Linq;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The plot at the top of the report — X11, candidate A.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a test can hold is that the axes mean what the caption says they mean.</b> Whether two
/// measured axes make a newcomer faster than a mosaic is A11 round 2's question and no assertion
/// here reaches it — what these cover is the way this picture could lie: a dot in the wrong place,
/// a project missing from a drawing that claims to hold all of them, a name silently dropped, and
/// the shading that would turn two measurements into a score.
/// </para>
/// <para>
/// <b>The mosaic's tests are the model for these</b>, because the two artifacts make the same
/// promises — self-contained, no score, and honest about what it left out.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ReachPlotTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    private string Svg => ReachPlot.Render(core.Model, Findings);

    /// <summary>The drawing is well-formed XML.</summary>
    [Fact]
    public void The_plot_is_well_formed_xml()
    {
        var svg = XDocument.Parse(Svg).Root!;

        Assert.Equal("svg", svg.Name.LocalName);
        Assert.Equal("http://www.w3.org/2000/svg", svg.Name.NamespaceName);
    }

    /// <summary>
    /// Every project that declares an analysed type is on the picture, once.
    /// </summary>
    /// <remarks>
    /// The population is <see cref="SolutionModel.ProjectCouplings"/> and not
    /// <see cref="SolutionModel.Projects"/>, which are not the same list: a project excluded down
    /// to nothing has no density to plot and no coupling to place it by. A picture that quietly
    /// dropped one would be <c>docs/DEFECTS.md</c> §3 in a new medium.
    /// </remarks>
    [Fact]
    public void Every_project_that_declares_a_type_is_one_dot()
    {
        var points = ReachPlot.Points(core.Model, Findings);

        Assert.Equal(core.Model.ProjectCouplings.Count, points.Count);
        Assert.Equal(points.Count, Regex.Matches(Svg, "<circle").Count);
        Assert.Equal(points.Count, points.Select(p => p.Project).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The axes carry the quantities the caption claims, and the counts are the model's.
    /// </summary>
    /// <remarks>
    /// <b>Recomputed a second way rather than read back off the point.</b> Density is named over
    /// declared and reach is dependents over everything outside — so a plot that started dividing
    /// by the whole solution, or counting lines instead of types, fails here rather than looking
    /// plausible. Counting types is the whole reason this picture replaced the mosaic.
    /// </remarks>
    [Fact]
    public void Both_axes_are_shares_of_types_and_not_of_anything_else()
    {
        var findings = Findings;
        var named = core.Model.Types
            .Where(t => findings.About(t.Subject).Count > 0
                        || t.Members.Any(m => findings.About(m.Subject).Count > 0))
            .Select(t => t.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var point in ReachPlot.Points(core.Model, findings))
        {
            var here = core.Model.Types
                .Where(t => string.Equals(t.Project, point.Project, StringComparison.Ordinal))
                .ToList();

            Assert.Equal(here.Count, point.Types);
            Assert.Equal(here.Count(t => named.Contains(t.Subject.Canonical)), point.Named);
            Assert.Equal(100d * point.Named / point.Types, point.Density, 6);
            Assert.Equal(100d * point.Dependents / (core.Model.Types.Count - point.Types), point.Reach, 6);
        }
    }

    /// <summary>
    /// More dependents is further right, always.
    /// </summary>
    /// <remarks>
    /// The axis is a <i>share</i> of the types outside the project, so a large project needs more
    /// dependents than a small one to reach the same position — and that is the intended reading.
    /// What must never happen is the order inverting, which would make the picture argue the
    /// opposite of the table beside it.
    /// </remarks>
    [Fact]
    public void A_project_more_of_the_codebase_depends_on_sits_further_right()
    {
        var points = ReachPlot.Points(core.Model, Findings)
            .OrderBy(p => p.Dependents)
            .ToList();

        Assert.True(points.Count > 1, "the fixture has one project, so this asserts nothing");

        for (var i = 1; i < points.Count; i++)
            if (points[i].Dependents > points[i - 1].Dependents)
                Assert.True(
                    points[i].Reach > points[i - 1].Reach,
                    $"{points[i].Project} has more dependents than {points[i - 1].Project} and sits left of it");
    }

    /// <summary>
    /// Only the projects something depends on are named, and any that would not fit are disclosed.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §31 in a second picture. The leaves are a population rather than a
    /// list — 22 of nopCommerce's 27 — and naming them would bury the handful the picture is about;
    /// their count is on the drawing instead. What must not happen is a name being dropped for want
    /// of pixels and nothing saying so.
    /// </remarks>
    [Fact]
    public void A_name_that_did_not_fit_is_named_beside_the_picture()
    {
        var findings = Findings;
        var svg = Svg;

        var depended = ReachPlot.Points(core.Model, findings).Where(p => p.Dependents > 0).ToList();
        var dropped = ReachPlot.Unlabelled(core.Model, findings);

        Assert.True(depended.Count > 0, "nothing in the fixture is depended on, so this asserts nothing");

        foreach (var point in depended)
        {
            var drawn = svg.Contains($">{point.Project}</text>", StringComparison.Ordinal);

            Assert.True(
                drawn ^ dropped.Contains(point.Project, StringComparer.Ordinal),
                $"{point.Project} is neither drawn nor disclosed, or is both");
        }

        // A leaf is never named on the picture: it is one of a crowd, and the crowd is counted.
        foreach (var leaf in ReachPlot.Points(core.Model, findings).Where(p => p.Dependents == 0))
            Assert.DoesNotContain($">{leaf.Project}</text>", svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two axes, and nothing that turns them into a verdict.
    /// </summary>
    /// <remarks>
    /// <b>The specific risk a two-axis picture runs.</b> Shaded quadrants, a danger corner or a
    /// colour ramp would be <c>PRD-free-tier.md</c> §8's composite arriving as a graphic — a
    /// severity model the tool does not have and cannot defend. The marks carry two states,
    /// <i>something depends on this</i> and <i>nothing does</i>, and both are yes-or-no.
    /// </remarks>
    [Fact]
    public void The_plot_carries_no_score()
    {
        var svg = Svg;

        Assert.DoesNotContain("Gradient", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("filter", svg, StringComparison.Ordinal);

        // The marks carry two states and no third. Counting hexes would count the text colours and
        // both colour schemes; what a ramp would actually look like is a third class on a circle.
        var marks = Regex.Matches(svg, "<circle class=\"([a-z]+)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, marks.Count);

        // One rect, and it is the ground. A shaded quadrant would be the second.
        Assert.Single(Regex.Matches(svg, "<rect"));
    }

    /// <summary>The drawing fetches nothing, like the page that embeds it.</summary>
    [Fact]
    public void The_plot_requests_nothing_from_the_network()
    {
        var svg = Svg;

        Assert.DoesNotContain("http://www.w3.org/1999/xlink", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<image", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("@import", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", svg, StringComparison.Ordinal);
    }

    /// <summary>The same drawing appears in the HTML report, from one renderer.</summary>
    [Fact]
    public void The_html_report_embeds_the_same_drawing()
    {
        var page = HtmlReport.Render(
            core.Model, Findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains(Svg, page, StringComparison.Ordinal);
    }
}

using System.Globalization;
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

    /// <summary>
    /// Nothing in the header strip overlaps anything else in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §36, and the reason it needs a test rather than a corrected
    /// constant. The y-axis title was placed by hand at <c>x = Left - 78</c>, which put it at
    /// x = 18 running about 215px — straight through the subtitle at x = 96, four pixels of
    /// baseline apart. It collided on **every run whatever the data**, and the suite could not see
    /// it: the SVG stayed well-formed, every element was present, and no assertion here was about
    /// where two pieces of chrome sat relative to each other.
    /// </para>
    /// <para>
    /// <b>The lesson is that the collision discipline was applied to the data and not to the
    /// furniture.</b> <see cref="ReachPlot.Unlabelled"/> exists because a project label that fits
    /// nowhere must be disclosed rather than drawn over something; the title, subtitle and axis
    /// titles got no such treatment. This asserts the same rule for the furniture, using the same
    /// width estimate the layout itself uses — so a title that grows, or a constant that moves,
    /// fails here rather than on a screenshot.
    /// </para>
    /// <para>
    /// Rotated text is excluded rather than measured. The y-axis title runs up its own axis now,
    /// which is what took it out of this strip, and estimating a rotated box would be asserting
    /// against a second implementation of the layout rather than against the layout.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_two_pieces_of_header_furniture_overlap()
    {
        // The same estimate ReachPlot lays out with. Over-estimating spreads text and
        // under-estimating overlaps it, so a test that guessed narrower would pass on a collision.
        const double CharWidth = 0.56;

        var boxes = XDocument.Parse(Svg).Root!
            .Descendants()
            .Where(e => e.Name.LocalName == "text")
            .Where(e => e.Attribute("transform") is null)
            .Select(e => new
            {
                Text = e.Value,
                X = double.Parse(e.Attribute("x")!.Value, CultureInfo.InvariantCulture),
                Y = double.Parse(e.Attribute("y")!.Value, CultureInfo.InvariantCulture),
                Size = e.Attribute("class")?.Value switch
                {
                    "ti" => 15.0,
                    "ax" => 12.0,
                    _ => 11.0,
                },
                Anchor = e.Attribute("text-anchor")?.Value ?? "start",
            })
            // The strip above the plot area, which is where the two collided.
            .Where(t => t.Y < 64)
            .Select(t =>
            {
                var w = t.Text.Length * t.Size * CharWidth;
                var left = t.Anchor switch
                {
                    "end" => t.X - w,
                    "middle" => t.X - w / 2,
                    _ => t.X,
                };
                return (t.Text, L: left, R: left + w, T: t.Y - t.Size, B: t.Y + 4);
            })
            .ToList();

        Assert.NotEmpty(boxes);

        foreach (var a in boxes)
            foreach (var b in boxes)
            {
                if (ReferenceEquals(a.Text, b.Text) && a.L == b.L && a.T == b.T) continue;

                var overlaps = a.L < b.R && b.L < a.R && a.T < b.B && b.T < a.B;

                Assert.False(
                    overlaps,
                    $"Header furniture overlaps: \"{a.Text}\" at x {a.L:F0}-{a.R:F0} y {a.T:F0}-{a.B:F0} "
                    + $"and \"{b.Text}\" at x {b.L:F0}-{b.R:F0} y {b.T:F0}-{b.B:F0}.");
            }
    }

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
    /// <para>
    /// <b>Claims and not findings</b>, <c>docs/DEFECTS.md</c> §41. This recomputation said
    /// <c>About(...).Count > 0</c>, which is where the y-axis got the coverage disclosure — the one
    /// kind whose entry says nothing could be judged about the type — counted as a finding naming
    /// it. A second derivation only checks the first if it means the same thing.
    /// </para>
    /// </remarks>
    [Fact]
    public void Both_axes_are_shares_of_types_and_not_of_anything_else()
    {
        var findings = Findings;
        var named = core.Model.Types
            .Where(t => findings.About(t.Subject).Any(f => Claims.IsRiskClaim(f.Kind))
                        || t.Members.Any(m => findings.About(m.Subject).Any(f => Claims.IsRiskClaim(f.Kind))))
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

using System.Globalization;
using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// One project, placed by what depends on it and by how much of it a finding names.
/// </summary>
/// <param name="Project">The project.</param>
/// <param name="Types">Types it declares.</param>
/// <param name="Named">How many of those some finding is about.</param>
/// <param name="Dependents">Types outside it that reach into it.</param>
/// <param name="Reach">
/// <paramref name="Dependents"/> as a share of every type outside the project, 0 to 100 — the
/// horizontal position. A count would make the axis a property of the solution's size rather than
/// of its shape, and two runs could not be read the same way.
/// </param>
/// <param name="Density">
/// <paramref name="Named"/> over <paramref name="Types"/>, 0 to 100 — the vertical position.
/// </param>
public readonly record struct PlotPoint(
    string Project, int Types, int Named, int Dependents, double Reach, double Density);

/// <summary>
/// The picture at the top of the report — X11, candidate A.
/// </summary>
/// <remarks>
/// <para>
/// <b>It replaces the mosaic in this position because the mosaic was measured and it misleads.</b>
/// Cell area there is lines of code while every claim on the page is a count of types, and on
/// nopCommerce <b>17% of the types are named and they hold 58% of the ink</b>. A reader assembling
/// the claim this page exists for — <i>which project is dense with findings <b>and</b> holds
/// everything else up</i> — got it wrong three times, each time by reading area exactly as drawn:
/// <c>Nop.Web</c> looks worst and is the least dense of the five with 31 dependents,
/// <i>"almost all of it is red"</i> is 26%, and <c>Nop.Web.Framework</c> — densest at 29% and most
/// depended on at 1,280 — goes unmentioned because 235 types is a small tile.
/// </para>
/// <para>
/// <b>So the two quantities become one position.</b> Across: how much of the rest of the codebase
/// reaches into this project. Up: how much of it a finding names. Area of the dot: how many types
/// it declares. The claim a reader had to assemble out of two pictures and a table is now a place
/// on a plot, which is what a picture is for. <c>BRIEF-job-a-picture.md</c> carries the brief and
/// the candidates it beat.
/// </para>
/// <para>
/// <b>No score, and the risk is real enough to name.</b> A two-axis picture invites shaded
/// quadrants and a danger corner, which would be <c>PRD-free-tier.md</c> §8's composite arriving
/// as a graphic. There are no zones, no shading, no ramp and no quadrant labels here: two stated
/// measured axes, and the reader makes the trade-off. Dot area is the third measured quantity and
/// not an importance.
/// </para>
/// <para>
/// <b>What it gives up against the mosaic is the completeness claim</b> — <i>all of your code is on
/// this page and most of it is pale</i>. That survives twice over: the <c>clean</c> tile states it
/// as a number, and the mosaic still ships, lower down the page and standalone as
/// <c>--mosaic</c>, where <c>PRD-free-tier.md</c> §9's third metric actually lives.
/// </para>
/// <para>
/// <b>Labels are placed deterministically and the ones that do not fit are disclosed.</b> An
/// SVG renderer cannot measure text, so widths are estimated and each label takes the first of four
/// offsets that collides with nothing already placed. A label that fits nowhere is dropped and
/// named beside the picture instead — <c>docs/DEFECTS.md</c> §31's rule, that a reader scanning a
/// picture for a name reads its absence as an omission rather than as a shortage of pixels.
/// </para>
/// </remarks>
public static class ReachPlot
{
    private const int Width = 1000;
    private const int Height = 560;
    private const int Left = 96;
    private const int Right = 40;
    private const int Top = 64;
    private const int Bottom = 76;

    /// <summary>Point size, in characters, for the two label lines and the axis furniture.</summary>
    private const double NameSize = 14;

    private const double SubSize = 11.5;

    /// <summary>Where the y-axis title sits in the left gutter, rotated up its own axis.</summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §36. The gutter is <see cref="Left"/> wide; the tick labels are
    /// anchored at <c>Left - 10</c> and the widest is three characters, so nothing else claims the
    /// space to the left of about x = 68.
    /// </remarks>
    private const int AxisTitleX = 30;

    /// <summary>
    /// Estimated width of one character at one point size.
    /// </summary>
    /// <remarks>
    /// SVG has no text metrics before layout, so collision detection needs a number. 0.56 is the
    /// conservative side for the system sans stack — over-estimating spreads labels, and
    /// under-estimating overlaps them, which is the failure that gets reported.
    /// </remarks>
    private const double CharWidth = 0.56;

    /// <summary>Renders the plot as a standalone SVG document.</summary>
    public static string Render(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var points = Points(model, findings);
        if (points.Count == 0) return Empty("No project declared an analysed type.");

        var xmax = Bound(points.Max(p => p.Reach));
        var ymax = Bound(points.Max(p => p.Density));
        var biggest = Math.Max(1, points.Max(p => p.Types));

        double X(double v) => Left + (Width - Left - Right) * v / xmax;
        double Y(double v) => Height - Bottom - (Height - Top - Bottom) * v / ymax;
        double R(int types) => Math.Max(5, 34 * Math.Sqrt(types / (double)biggest));

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"rp\" viewBox=\"0 0 {Width} {Height}\" width=\"{Width}\" height=\"{Height}\" font-family=\"system-ui,-apple-system,Segoe UI,Roboto,sans-serif\">\n");

        svg.Append("<style>\n");
        svg.Append(".rp .bg{fill:#f4f3f0}\n");
        svg.Append(".rp .gr{stroke:#dedcd7;stroke-width:1}\n");
        svg.Append(".rp .ax{fill:#6b6b6b;font-size:12px}\n");
        svg.Append(".rp .tk{fill:#8d8b86;font-size:11px}\n");
        svg.Append(".rp .dot{fill:#b4483c;fill-opacity:.85;stroke:#f4f3f0;stroke-width:1.5}\n");
        svg.Append(".rp .lf{fill:#c3c4c1;fill-opacity:.8;stroke:#f4f3f0;stroke-width:1}\n");
        svg.Append(CultureInfo.InvariantCulture, $".rp .nm{{fill:#1a1a1a;font-size:{NameSize}px;font-weight:600}}\n");
        svg.Append(CultureInfo.InvariantCulture, $".rp .sb{{fill:#6b6b6b;font-size:{SubSize}px}}\n");
        svg.Append(".rp .ti{font-size:15px;font-weight:600;fill:#1a1a1a}\n");
        svg.Append(".rp .lg{font-size:11px;fill:#6b6b6b}\n");
        svg.Append("@media(prefers-color-scheme:dark){.rp .bg{fill:#16171a}.rp .gr{stroke:#2e3036}");
        svg.Append(".rp .ax,.rp .sb,.rp .lg{fill:#9a9a97}.rp .tk{fill:#82817d}.rp .dot{fill:#d9615a;stroke:#16171a}");
        svg.Append(".rp .lf{fill:#42454b;stroke:#16171a}.rp .nm,.rp .ti{fill:#e9e8e6}}\n");
        svg.Append("</style>\n");

        svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"bg\" width=\"{Width}\" height=\"{Height}\"/>\n");

        var solution = Path.GetFileNameWithoutExtension(model.SolutionPath);
        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"ti\" x=\"{Left}\" y=\"28\">{Html.Text(solution)} — where the findings sit</text>\n");
        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"lg\" x=\"{Left}\" y=\"46\">One dot per project, sized by how many types it declares. Bearing {Html.Text(model.ToolVersion)}</text>\n");

        // The grid is the only chrome, and it is drawn under everything.
        for (var v = 0; v <= xmax; v += Step(xmax))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<line class=\"gr\" x1=\"{X(v):F0}\" y1=\"{Top}\" x2=\"{X(v):F0}\" y2=\"{Height - Bottom}\"/>\n");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"tk\" x=\"{X(v):F0}\" y=\"{Height - Bottom + 18}\" text-anchor=\"middle\">{v}%</text>\n");
        }

        for (var v = 0; v <= ymax; v += Step(ymax))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<line class=\"gr\" x1=\"{Left}\" y1=\"{Y(v):F0}\" x2=\"{Width - Right}\" y2=\"{Y(v):F0}\"/>\n");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"tk\" x=\"{Left - 10}\" y=\"{Y(v) + 4:F0}\" text-anchor=\"end\">{v}%</text>\n");
        }

        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"ax\" x=\"{Left}\" y=\"{Height - 18}\">how much of the rest of the codebase reaches into it →</text>\n");
        // The y-axis title runs up its own axis — docs/DEFECTS.md §36. Laid out horizontally
        // at x = Left - 78 it began at x = 18 and ran about 215px, straight through the subtitle
        // at x = 96, four pixels of baseline apart: fixed geometry, so it collided on every run
        // whatever the data. Rotating it removes the collision by construction rather than by
        // re-tuning the constant that caused it, and it is where a y-axis title belongs.
        //
        // The arrow rotates with the text. A right-pointing arrow turned -90° points up, and it
        // sits at the end of the string, which after rotation is the top of the axis — so the
        // two axis titles stay symmetric and each still states its own direction.
        var axisMiddle = (Top + Height - Bottom) / 2;
        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"ax\" transform=\"rotate(-90 {AxisTitleX} {axisMiddle})\" x=\"{AxisTitleX}\" y=\"{axisMiddle}\" text-anchor=\"middle\">how much of it a finding names →</text>\n");

        // Context first, so a leaf never sits on top of the projects the report is about.
        foreach (var p in points.Where(p => p.Dependents == 0))
            svg.Append(CultureInfo.InvariantCulture,
                $"<circle class=\"lf\" cx=\"{X(p.Reach):F1}\" cy=\"{Y(p.Density):F1}\" r=\"{R(p.Types):F1}\"/>\n");

        foreach (var p in points.Where(p => p.Dependents > 0))
            svg.Append(CultureInfo.InvariantCulture,
                $"<circle class=\"dot\" cx=\"{X(p.Reach):F1}\" cy=\"{Y(p.Density):F1}\" r=\"{R(p.Types):F1}\"/>\n");

        foreach (var (point, x, y, anchor) in Labels(points, X, Y, R))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"nm\" x=\"{x:F0}\" y=\"{y:F0}\" text-anchor=\"{anchor}\">{Html.Text(point.Project)}</text>\n");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"sb\" x=\"{x:F0}\" y=\"{y + 15:F0}\" text-anchor=\"{anchor}\">{point.Named} of {point.Types} named</text>\n");
        }

        var leaves = points.Count(p => p.Dependents == 0);
        if (leaves > 0)
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"lg\" x=\"{X(0) + 8:F0}\" y=\"{Top + 16}\">{leaves} {Sentences.Do(leaves, "project", "projects")} nothing depends on</text>\n");

        svg.Append("</svg>\n");
        return svg.ToString();
    }

    /// <summary>Renders the plot and writes it to <paramref name="path"/>.</summary>
    public static void Write(string path, SolutionModel model, FindingSet findings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, Render(model, findings), new UTF8Encoding(false));
    }

    /// <summary>
    /// Every project that declares an analysed type, ordered by what depends on it.
    /// </summary>
    /// <remarks>
    /// <b>Ordered by a total key</b> — dependents descending, then name — so two projects that
    /// nothing depends on cannot swap drawing order between runs of an unchanged codebase, which is
    /// <c>docs/ARCHITECTURE.md</c> §10's rule for every emitted artifact.
    /// </remarks>
    public static IReadOnlyList<PlotPoint> Points(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var named = Subjects.Named(model, findings);
        var total = model.Types.Count;

        var byProject = model.Types
            .GroupBy(t => t.Project, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (Types: g.Count(), Named: g.Count(t => named.Contains(t.Subject.Canonical))),
                StringComparer.Ordinal);

        return
        [
            .. model.ProjectCouplings
                .Where(c => byProject.ContainsKey(c.Project))
                .Select(c =>
                {
                    var (types, flagged) = byProject[c.Project];
                    var outside = total - types;

                    return new PlotPoint(
                        c.Project,
                        types,
                        flagged,
                        c.TypesElsewhereReachingIn,
                        outside > 0 ? 100d * c.TypesElsewhereReachingIn / outside : 0,
                        types > 0 ? 100d * flagged / types : 0);
                })
                .OrderByDescending(p => p.Dependents)
                .ThenBy(p => p.Project, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// The projects on the picture whose label would not fit anywhere, largest first.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §31, and the same remedy <see cref="Mosaic.Unlabelled"/> uses: the
    /// layout is recomputed rather than cached, so a caption and the drawing it explains cannot
    /// disagree about which dots got a name.
    /// </remarks>
    public static IReadOnlyList<string> Unlabelled(SolutionModel model, FindingSet findings)
    {
        var points = Points(model, findings);
        if (points.Count == 0) return [];

        var xmax = Bound(points.Max(p => p.Reach));
        var ymax = Bound(points.Max(p => p.Density));
        var biggest = Math.Max(1, points.Max(p => p.Types));

        double X(double v) => Left + (Width - Left - Right) * v / xmax;
        double Y(double v) => Height - Bottom - (Height - Top - Bottom) * v / ymax;
        double R(int types) => Math.Max(5, 34 * Math.Sqrt(types / (double)biggest));

        var placed = Labels(points, X, Y, R).Select(l => l.Point.Project).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. points
                .Where(p => p.Dependents > 0 && !placed.Contains(p.Project))
                .OrderByDescending(p => p.Dependents)
                .ThenBy(p => p.Project, StringComparer.Ordinal)
                .Select(p => p.Project)
        ];
    }

    /// <summary>
    /// Where each label goes, and which ones had to be dropped.
    /// </summary>
    /// <remarks>
    /// <b>Only the projects something depends on are labelled.</b> The leaves are a population
    /// rather than a list — 22 of nopCommerce's 27 — and naming them would bury the five the
    /// picture is about under twenty-two plugins. Their count is stated on the picture instead.
    /// </remarks>
    private static List<(PlotPoint Point, double X, double Y, string Anchor)> Labels(
        IReadOnlyList<PlotPoint> points, Func<double, double> x, Func<double, double> y, Func<int, double> radius)
    {
        var placed = new List<(PlotPoint Point, double X, double Y, string Anchor)>();
        var boxes = new List<(double L, double T, double R, double B)>();

        foreach (var point in points.Where(p => p.Dependents > 0))
        {
            var cx = x(point.Reach);
            var cy = y(point.Density);
            var r = radius(point.Types);

            var name = point.Project.Length * NameSize * CharWidth;
            var sub = $"{point.Named} of {point.Types} named".Length * SubSize * CharWidth;
            var wide = Math.Max(name, sub);

            foreach (var (dx, dy, anchor) in new[]
                     {
                         (r + 10, -r - 8, "start"), (-r - 10, -r - 8, "end"),
                         (r + 10, r + 22, "start"), (-r - 10, r + 22, "end"),
                     })
            {
                var lx = cx + dx;
                var ly = cy + dy;
                var box = anchor == "end"
                    ? (lx - wide, ly - NameSize, lx, ly + 18)
                    : (lx, ly - NameSize, lx + wide, ly + 18);

                if (box.Item1 < 4 || box.Item3 > Width - 4 || box.Item2 < Top - 20 || box.Item4 > Height - Bottom + 4)
                    continue;

                if (boxes.Any(b => box.Item1 < b.R && b.L < box.Item3 && box.Item2 < b.B && b.T < box.Item4))
                    continue;

                boxes.Add((box.Item1, box.Item2, box.Item3, box.Item4));
                placed.Add((point, lx, ly, anchor));
                break;
            }
        }

        return placed;
    }

    /// <summary>
    /// The axis bound: the next ten above the largest value, and never zero.
    /// </summary>
    /// <remarks>
    /// Arithmetic about the canvas rather than a judgement about the codebase — the same kind of
    /// constant as <see cref="Mosaic"/>'s label-fits width. An axis fixed at 100% would put every
    /// project on nopCommerce into the bottom-left corner of the picture.
    /// </remarks>
    private static int Bound(double largest) => Math.Max(10, (int)(Math.Ceiling(largest / 10) * 10));

    private static int Step(int bound) => Math.Max(5, bound / 5);

    private static string Empty(string why) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 320 40\" width=\"320\" height=\"40\">"
        + $"<text x=\"0\" y=\"20\" font-size=\"12\" fill=\"#6b6b6b\">{Html.Text(why)}</text></svg>\n";
}

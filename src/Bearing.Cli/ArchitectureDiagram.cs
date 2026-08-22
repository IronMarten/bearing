using System.Globalization;
using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The screenshot artifact: the project map as one static SVG — <c>TECHREQ-job-a.md</c> §5.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Projects, not types.</b> This one has to fit on a screen and survive being pasted into
/// Slack, and that constraint decides the content rather than the styling.
/// </para>
/// <para>
/// <b>Static, and that is a decision already taken</b> — <c>docs/ARCHITECTURE.md</c> §10 records
/// both graph artifacts as static, which takes elkjs, cytoscape and d3-force off the critical path
/// and with them §10's "largest single technical fork". Nothing here needs a layout engine: the
/// layering is <see cref="ProjectGraph"/>'s and the rest is arithmetic over box widths.
/// </para>
/// <para>
/// <b>Martin's numbers appear as labels, never as scores.</b> §5.4 is explicit — "stable and
/// concrete: everything depends on it, nothing can extend it without modifying it", never
/// <c>D = 0.42</c>. A number on a box invites ranking boxes against each other, which is the thing
/// this tool does not do.
/// </para>
/// <para>
/// <b>Width is the acceptance criterion, not depth.</b> The spike's project map came out
/// <b>1966px wide at 21 projects</b> because a plugin host defeats layering — twenty of
/// twenty-seven projects sit at one level — so the fold in <see cref="ProjectGraph"/> is what makes
/// this legible, and <see cref="MaxPerRow"/> is the backstop for when even the folded layer is
/// wide.
/// </para>
/// <para>
/// <b>Height is not the acceptance criterion, and that is what lets a folded box name its
/// members</b> — <c>docs/DEFECTS.md</c> §31, reopened. A reader arrives holding a name and
/// looks for it; on nopCommerce seventeen of twenty-seven projects were not in the picture,
/// including both tax plugins, which is the task A11 round 1 watched the map lose. Naming them
/// costs height and no width at all.
/// </para>
/// </remarks>
public static class ArchitectureDiagram
{
    private const int BoxWidth = 168;
    private const int BoxHeight = 62;
    private const int GapX = 18;
    private const int GapY = 46;
    private const int Margin = 20;

    /// <summary>
    /// One line per project named inside a folded box, and the space under the box's two
    /// header lines that the list sits in.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §31, reopened. <b>Only the height moves.</b> The names come from
    /// <see cref="Labels"/>, which is already capped at twenty characters, so the widest member
    /// line is about 123px inside a 144px box interior — <b>a folded box never grows wider than
    /// an unfolded one</b>, and the drawing's width is still set by how many boxes sit in a row.
    /// </remarks>
    private const int MemberLine = 15;

    private const int MembersTop = 56;

    /// <summary>
    /// How many boxes may sit in one row before a layer wraps onto another.
    /// </summary>
    /// <remarks>
    /// A layer of twenty folded boxes would be 3,700px wide and unreadable at screenshot size, so a
    /// wide layer wraps rather than growing.
    /// <para>
    /// <b>Wrapping misrepresents the layout</b> — the second row is the same layer, drawn as though
    /// it were below one — and nothing on the drawing says so. <b>It fires on Jellyfin</b>, whose
    /// widest layer holds ten boxes: <c>docs/DEFECTS.md</c> §45, where the measurement is. The
    /// remark that stood here until 2026-08-22 said this shipped unexercised on real input, which
    /// was true of nopCommerce and never checked against the other reference solution.
    /// </para>
    /// </remarks>
    private const int MaxPerRow = 5;

    /// <summary>How tall a box has to be to hold what it says.</summary>
    private static int HeightOf(ProjectGroup group) =>
        group.Size <= 1 ? BoxHeight : MembersTop + (group.Size * MemberLine) + 8;

    /// <summary>Renders the diagram as a standalone SVG document.</summary>
    public static string Render(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var graph = model.ProjectGraph;
        var zones = model.ProjectCouplings.ToDictionary(c => c.Project, c => c.Zone, StringComparer.Ordinal);

        if (graph.Groups.Count == 0)
            return Empty("No project declared an analysed type.");

        var placed = Place(graph);
        var width = placed.Max(p => p.X + BoxWidth) + Margin;
        var height = placed.Max(p => p.Y + HeightOf(p.Group)) + Margin;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"ad\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" font-family=\"system-ui,-apple-system,Segoe UI,Roboto,sans-serif\">\n");
        svg.Append("<style>\n");
        svg.Append(".ad .bx{fill:#fff;stroke:#c9c7c2;stroke-width:1.5}\n");
        svg.Append(".ad .bx.pain{fill:#fdf1e7;stroke:#c88a4a}\n");
        svg.Append(".ad .bx.useless{fill:#f2f1f6;stroke:#9a95b5}\n");
        svg.Append(".ad .bx.cycle{stroke:#b4483c;stroke-dasharray:5 3}\n");
        svg.Append(".ad .nm{font-size:13px;font-weight:600;fill:#1a1a1a}\n");
        svg.Append(".ad .sm{font-size:10.5px;fill:#6b6b6b}\n");
        svg.Append(".ad .mb{font-size:11px;fill:#3a3a3a}\n");
        svg.Append(".ad .ed{stroke:#c2c0bb;stroke-width:1.2;fill:none}\n");
        svg.Append("@media(prefers-color-scheme:dark){.ad .bx{fill:#1d1e22;stroke:#3c3f47}.ad .bx.pain{fill:#2c2219;stroke:#c88a4a}");
        svg.Append(".ad .bx.useless{fill:#232230;stroke:#9a95b5}.ad .nm{fill:#e9e8e6}.ad .sm{fill:#9a9a97}.ad .mb{fill:#c9c8c5}.ad .ed{stroke:#4a4d55}}\n");
        svg.Append("</style>\n");

        Edges(svg, placed);
        Boxes(svg, placed, zones, Labels(graph));

        svg.Append("</svg>\n");
        return svg.ToString();
    }

    /// <summary>Renders the diagram and writes it to <paramref name="path"/>.</summary>
    public static void Write(string path, SolutionModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, Render(model), new UTF8Encoding(false));
    }

    private static string Empty(string why) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 320 40\" width=\"320\" height=\"40\">"
        + $"<text x=\"0\" y=\"20\" font-size=\"12\" fill=\"#6b6b6b\">{Html.Text(why)}</text></svg>\n";

    /// <summary>
    /// Where each box goes. Deepest layer at the top, so dependencies point downward.
    /// </summary>
    /// <remarks>
    /// The convention every architecture drawing already uses: applications above, foundations
    /// below, arrows falling. Layer 0 depends on nothing, so it is the bottom row.
    /// </remarks>
    private static List<Placed> Place(ProjectGraph graph)
    {
        var placed = new List<Placed>();
        var byLayer = graph.Groups.GroupBy(g => g.Layer).OrderByDescending(l => l.Key).ToList();
        var y = Margin;

        foreach (var layer in byLayer)
        {
            var groups = layer.ToList();

            for (var start = 0; start < groups.Count; start += MaxPerRow)
            {
                var row = groups.Skip(start).Take(MaxPerRow).ToList();
                var rowWidth = (row.Count * BoxWidth) + ((row.Count - 1) * GapX);
                var x = Margin + Math.Max(0, (Widest(byLayer) - rowWidth) / 2);

                foreach (var group in row)
                {
                    placed.Add(new Placed(group, x, y));
                    x += BoxWidth + GapX;
                }

                // The row is as tall as its tallest box, so a folded box listing seven projects
                // pushes the layer below it down rather than overlapping it.
                y += row.Max(HeightOf) + GapY;
            }
        }

        return placed;
    }

    private static int Widest(List<IGrouping<int, ProjectGroup>> layers)
    {
        var widest = layers.Max(l => Math.Min(l.Count(), MaxPerRow));
        return (widest * BoxWidth) + ((widest - 1) * GapX);
    }

    /// <summary>
    /// One line per dependency between boxes.
    /// </summary>
    /// <remarks>
    /// <b>No arrowheads and no routing.</b> Every edge runs from a higher box to a lower one
    /// because that is what the layering means, so an arrowhead would restate the layout at the
    /// cost of ink — and orthogonal routing on a folded graph is the layout engine §10 decided
    /// against. A straight line between box edges is honest about being a straight line.
    /// </remarks>
    private static void Edges(StringBuilder svg, List<Placed> placed)
    {
        var boxOf = new Dictionary<string, Placed>(StringComparer.Ordinal);
        foreach (var box in placed)
            foreach (var project in box.Group.Projects)
                boxOf[project] = box;

        var drawn = new HashSet<(int, int)>();

        foreach (var box in placed)
            foreach (var target in box.Group.DependsOn)
            {
                if (!boxOf.TryGetValue(target, out var into)) continue;
                if (ReferenceEquals(into.Group.Projects, box.Group.Projects)) continue;

                // Two projects in one folded box reaching the same target is one line, not several.
                var key = (placed.IndexOf(box), placed.IndexOf(into));
                if (!drawn.Add(key)) continue;

                var x1 = box.X + (BoxWidth / 2);
                var y1 = box.Y + HeightOf(box.Group);
                var x2 = into.X + (BoxWidth / 2);
                var y2 = into.Y;

                svg.Append(CultureInfo.InvariantCulture, $"<path class=\"ed\" d=\"M{x1} {y1} C{x1} {y1 + 20} {x2} {y2 - 20} {x2} {y2}\"/>\n");
            }
    }

    private static void Boxes(
        StringBuilder svg,
        List<Placed> placed,
        IReadOnlyDictionary<string, MainSequenceZone> zones,
        IReadOnlyDictionary<string, string> labels)
    {
        foreach (var box in placed)
        {
            var group = box.Group;
            var zone = group.Projects
                .Select(p => zones.GetValueOrDefault(p, MainSequenceZone.None))
                .FirstOrDefault(z => z is MainSequenceZone.Pain or MainSequenceZone.Uselessness);

            var css = zone switch
            {
                MainSequenceZone.Pain => "bx pain",
                MainSequenceZone.Uselessness => "bx useless",
                _ => "bx",
            };

            if (group.IsCycle) css += " cycle";

            svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"{css}\" x=\"{box.X}\" y=\"{box.Y}\" width=\"{BoxWidth}\" height=\"{HeightOf(group)}\" rx=\"7\"/>\n");

            var cx = box.X + (BoxWidth / 2);
            svg.Append(CultureInfo.InvariantCulture, $"<text class=\"nm\" x=\"{cx}\" y=\"{box.Y + 25}\" text-anchor=\"middle\">{Html.Text(Title(group, labels))}</text>\n");

            if (Note(group, zone) is { Length: > 0 } note)
                svg.Append(CultureInfo.InvariantCulture, $"<text class=\"sm\" x=\"{cx}\" y=\"{box.Y + 43}\" text-anchor=\"middle\">{Html.Text(note)}</text>\n");

            // Every project in a folded box is named in it — docs/DEFECTS.md §31, reopened. The
            // box is the only place a reader looking for their own project will look, and it is
            // the whole of the standalone --diagram export.
            if (group.Size <= 1) continue;

            for (var i = 0; i < group.Projects.Count; i++)
                svg.Append(
                    CultureInfo.InvariantCulture,
                    $"<text class=\"mb\" x=\"{box.X + 14}\" y=\"{box.Y + MembersTop + 10 + (i * MemberLine)}\">{Html.Text(labels[group.Projects[i]])}</text>\n");
        }
    }

    private static string Title(ProjectGroup group, IReadOnlyDictionary<string, string> labels)
    {
        var first = labels[group.Projects[0]];
        return group.Size == 1 ? first : $"{first} +{group.Size - 1}";
    }

    /// <summary>
    /// Every box that stands for more than one project, with the label it carries and the
    /// projects inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §31.</b> The fold is this artifact's best compression — 27
    /// projects to 10 boxes on nopCommerce, against 1444px unfolded — and to a first reader it
    /// reads as an omission. Asked <i>"why isn't Nop.Plugin in the graph?"</i> while looking at the
    /// projects table directly below it, where the plugin names are. The caption says folding
    /// happened; nothing connected a folded box to the names inside it.
    /// </para>
    /// <para>
    /// <b>The names are now in the boxes as well, and this legend is the searchable copy</b> —
    /// §31 reopened on 2026-08-22. The original fix put them here <i>instead</i>, on the grounds
    /// that a picture cannot be searched and that a box growing to fit its members is what the
    /// fold exists to prevent. The second of those was measured and is not true of this drawing:
    /// <see cref="Labels"/> caps a name at twenty characters, so only the height moves.
    /// <see cref="Title"/> is still shared with the boxes, so a legend cannot disagree with the
    /// label it explains.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string Label, IReadOnlyList<string> Projects)> Folded(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var graph = model.ProjectGraph;
        var labels = Labels(graph);

        return [.. graph.Groups
            .Where(g => g.Size > 1)
            .Select(g => (Title(g, labels), (IReadOnlyList<string>)g.Projects))];
    }

    /// <summary>
    /// The second line: what the box is, in words.
    /// </summary>
    /// <remarks>
    /// §5.4 — the Martin data as a label and never as a score. A box saying <c>D = 0.42</c> invites
    /// a reader to rank boxes against each other, which is the one thing this tool refuses to do
    /// everywhere else.
    /// </remarks>
    private static string Note(ProjectGroup group, MainSequenceZone zone)
    {
        if (group.IsCycle) return $"{group.Size} projects, mutually dependent";
        if (group.Size > 1) return $"{group.Size} projects, same shape";

        return zone switch
        {
            MainSequenceZone.Pain => "stable and concrete",
            MainSequenceZone.Uselessness => "abstract, unused",
            _ => "",
        };
    }

    /// <summary>
    /// Short labels, chosen so that no two projects get the same one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Nop.Plugin.Tax.Avalara</c> in a 168px box renders as most of a namespace and none of a
    /// name, so a long name is shortened to its last segment — the head is on every other box too.
    /// </para>
    /// <para>
    /// <b>But only where that segment is unique, and Jellyfin is why.</b> It declares both
    /// <c>Emby.Server.Implementations</c> and <c>Jellyfin.Server.Implementations</c>, and
    /// shortening each to its tail drew two different projects under one label — a reader looking
    /// at that map cannot tell which box is which, and there is nothing on the page to reveal it.
    /// That is <c>docs/DEFECTS.md</c> §1's mistake made by a renderer instead of a walker: a
    /// display name that is not an identity. Where the tail collides the full name is kept and
    /// truncated instead, which is uglier and true.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> Labels(ProjectGraph graph)
    {
        const int Fits = 20;

        var projects = graph.Groups.SelectMany(g => g.Projects).ToList();

        var tails = projects
            .GroupBy(p => p[(p.LastIndexOf('.') + 1)..], StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        return projects.ToDictionary(
            project => project,
            project =>
            {
                if (project.Length <= Fits) return project;

                var tail = project[(project.LastIndexOf('.') + 1)..];
                if (tail.Length is > 0 and <= Fits && tails.Contains(tail)) return tail;

                return project[..(Fits - 1)] + "…";
            },
            StringComparer.Ordinal);
    }

    private readonly record struct Placed(ProjectGroup Group, int X, int Y);
}

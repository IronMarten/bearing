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
/// </remarks>
public static class ArchitectureDiagram
{
    private const int BoxWidth = 168;
    private const int BoxHeight = 62;
    private const int GapX = 18;
    private const int GapY = 46;
    private const int Margin = 20;

    /// <summary>
    /// How many boxes may sit in one row before a layer wraps onto another.
    /// </summary>
    /// <remarks>
    /// A layer of twenty folded boxes would be 3,700px wide and unreadable at screenshot size, so a
    /// wide layer wraps rather than growing.
    /// <para>
    /// <b>Wrapping does misrepresent the layout</b> — the second row is the same layer, drawn as
    /// though it were below one — and nothing on the drawing currently says so. It is reachable
    /// only above five folded boxes in one layer, which neither reference solution produces
    /// (nopCommerce's widest folded layer is three), so it ships unexercised on real input and is
    /// recorded here rather than solved with a band nobody has seen the need for.
    /// </para>
    /// </remarks>
    private const int MaxPerRow = 5;

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
        var height = placed.Max(p => p.Y + BoxHeight) + Margin;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"ad\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" font-family=\"system-ui,-apple-system,Segoe UI,Roboto,sans-serif\">\n");
        svg.Append("<style>\n");
        svg.Append(".ad .bx{fill:#fff;stroke:#c9c7c2;stroke-width:1.5}\n");
        svg.Append(".ad .bx.pain{fill:#fdf1e7;stroke:#c88a4a}\n");
        svg.Append(".ad .bx.useless{fill:#f2f1f6;stroke:#9a95b5}\n");
        svg.Append(".ad .bx.cycle{stroke:#b4483c;stroke-dasharray:5 3}\n");
        svg.Append(".ad .nm{font-size:13px;font-weight:600;fill:#1a1a1a}\n");
        svg.Append(".ad .sm{font-size:10.5px;fill:#6b6b6b}\n");
        svg.Append(".ad .ed{stroke:#c2c0bb;stroke-width:1.2;fill:none}\n");
        svg.Append("@media(prefers-color-scheme:dark){.ad .bx{fill:#1d1e22;stroke:#3c3f47}.ad .bx.pain{fill:#2c2219;stroke:#c88a4a}");
        svg.Append(".ad .bx.useless{fill:#232230;stroke:#9a95b5}.ad .nm{fill:#e9e8e6}.ad .sm{fill:#9a9a97}.ad .ed{stroke:#4a4d55}}\n");
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

                y += BoxHeight + GapY;
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
                var y1 = box.Y + BoxHeight;
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

            svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"{css}\" x=\"{box.X}\" y=\"{box.Y}\" width=\"{BoxWidth}\" height=\"{BoxHeight}\" rx=\"7\"/>\n");

            var cx = box.X + (BoxWidth / 2);
            svg.Append(CultureInfo.InvariantCulture, $"<text class=\"nm\" x=\"{cx}\" y=\"{box.Y + 25}\" text-anchor=\"middle\">{Html.Text(Title(group, labels))}</text>\n");

            if (Note(group, zone) is { Length: > 0 } note)
                svg.Append(CultureInfo.InvariantCulture, $"<text class=\"sm\" x=\"{cx}\" y=\"{box.Y + 43}\" text-anchor=\"middle\">{Html.Text(note)}</text>\n");
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
    /// Offered as data rather than drawn into the SVG. Names in a picture are unsearchable and
    /// force the box to grow to fit them, which is the thing the fold exists to prevent — so the
    /// caller renders these as text beside the diagram. <see cref="Title"/> is shared with the
    /// boxes, so a legend cannot disagree with the label it explains.
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

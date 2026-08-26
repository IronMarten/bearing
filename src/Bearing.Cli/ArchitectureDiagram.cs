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
/// members</b>. A reader arrives holding a name and
/// looks for it; on nopCommerce seventeen of twenty-seven projects were not in the picture,
/// including both tax plugins, which is the task A11 round 1 watched the map lose. Naming them
/// costs height and no width at all.
/// </para>
/// </remarks>
/// <summary>
/// One row of the project map.
/// </summary>
/// <param name="Layer">The layer its boxes belong to.</param>
/// <param name="Boxes">Each box on it, named by its first project, left to right.</param>
/// <param name="Continues">
/// Whether this row continues the layer above rather than depending on it.
/// False for every row of a layer that fitted in one, and for the first row of one that did
/// not.
/// </param>
public readonly record struct Row(int Layer, IReadOnlyList<string> Boxes, bool Continues);

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
    /// <b>Only the height moves.</b> The names come from
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
    /// widest layer holds ten boxes. The
    /// remark that stood here until 2026-08-22 said this shipped unexercised on real input, which
    /// was true of nopCommerce and never checked against the other reference solution.
    /// </para>
    /// </remarks>
    private const int MaxPerRow = 5;

    /// <summary>
    /// The gap between two rows of the <i>same</i> layer, against <see cref="GapY"/> between one
    /// layer and the next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row means <i>depends on the row below</i> everywhere
    /// else on this drawing, so a wrapped layer drawn at the ordinary gap states a dependency the
    /// code does not have. Jellyfin's layer 4 holds eleven boxes and wraps into three rows, which
    /// is most of the extra height in a ten-row drawing of eight layers.
    /// </para>
    /// <para>
    /// <b>The two boundaries are distinguishable with certainty, which is why nothing here is a
    /// judgement.</b> Between two genuinely adjacent layers there is always at least one edge —
    /// <c>DepthOf</c> is a longest path, so a box at depth <i>d</i> has a dependency at
    /// <i>d</i>-1. Between two rows of one layer there are never any, because an edge between
    /// boxes at equal depth would make the depths differ, and a mutual pair is one box already.
    /// Measured on Jellyfin: 1, 1, 2, 11, 1, 2 and 2 edges across the seven layer boundaries, and
    /// 0 across all six ordered pairs of the wrapped rows.
    /// </para>
    /// <para>
    /// <b>The width bound was measured and rejected.</b> Bounding the layering at five removes the
    /// wrap by drawing seven of Jellyfin's twenty-one boxes deeper than they are —
    /// <c>MediaBrowser.XbmcMetadata</c> by two layers — which trades a misstatement a reader can
    /// check against the edges for one that leaves no trace. The counter-example this defect was
    /// filed with pointed the other way and was measured on the wrong graph: it bounds
    /// nopCommerce's twenty-seven <i>projects</i>, where the drawing lays out ten <i>boxes</i>
    /// whose widest layer is three, so the bound never engages there at all.
    /// </para>
    /// </remarks>
    private const int WrapGapY = 12;

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
        svg.Append(".ad .ed{stroke:#b9b7b2;stroke-width:1.1;fill:none;opacity:.72}\n");
        svg.Append(".ad .lr{stroke:#dedcd7;stroke-width:1;stroke-dasharray:2 6}\n");
        svg.Append("@media(prefers-color-scheme:dark){.ad .bx{fill:#1d1e22;stroke:#3c3f47}.ad .bx.pain{fill:#2c2219;stroke:#c88a4a}");
        svg.Append(".ad .bx.useless{fill:#232230;stroke:#9a95b5}.ad .nm{fill:#e9e8e6}.ad .sm{fill:#9a9a97}.ad .mb{fill:#c9c8c5}.ad .ed{stroke:#5a5e68}.ad .lr{stroke:#33363d}}\n");
        svg.Append("</style>\n");

        LayerRules(svg, placed, width);

        // Boxes first, then edges over them. The paint order was the whole
        // mechanism: an opaque box painted last cuts a line that skips a layer into two stubs, and
        // two stubs either side of a box read as a dependency into it and another out. A line
        // drawn over the box is continuous, so a reader can trace it to the box it actually names.
        // It crosses a label to do that, which is uglier and true -- the trade Labels already
        // makes for the same reason.
        Boxes(svg, placed, zones, Labels(graph));
        Edges(svg, graph, placed);

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
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < graph.Groups.Count; i++) indexOf[graph.Groups[i].Projects[0]] = i;

        var byLayer = graph.Groups.GroupBy(g => g.Layer).OrderByDescending(l => l.Key).ToList();
        var y = Margin;
        var first = true;

        // The plan is Rows', so the geometry and the caption that explains it come from one
        // reading of the graph rather than two -- the arrangement that let two renderers drift, avoided.
        foreach (var row in Rows(graph))
        {
            // The second and later rows of one layer are the same layer, so
            // the gap above them must not be the gap that means "depends on".
            if (!first) y += row.Continues ? WrapGapY : GapY;
            first = false;

            var boxes = row.Boxes.Select(b => graph.Groups[indexOf[b]]).ToList();
            var rowWidth = (boxes.Count * BoxWidth) + ((boxes.Count - 1) * GapX);
            var x = Margin + Math.Max(0, (Widest(byLayer) - rowWidth) / 2);

            foreach (var group in boxes)
            {
                placed.Add(new Placed(group, x, y, indexOf[group.Projects[0]], row.Continues));
                x += BoxWidth + GapX;
            }

            // The row is as tall as its tallest box, so a folded box listing seven projects
            // pushes the layer below it down rather than overlapping it.
            y += boxes.Max(HeightOf);
        }

        return placed;
    }

    /// <summary>
    /// A faint rule across each boundary that really is a layer boundary, drawn only when some
    /// layer wrapped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The half of it a smaller gap cannot carry.</b> Tightening
    /// the gap inside a wrapped layer stops the drawing <i>asserting</i> a dependency there; it
    /// does not tell a reader which of the remaining gaps to trust. The rule is that sentence, in
    /// the picture rather than under it — everything above one depends on something below it, and
    /// there is a rule at every boundary where that holds.
    /// </para>
    /// <para>
    /// <b>What wrapped rather than what exists</b>, which is the rule
    /// <see cref="Tinted"/> already follows. Where no layer is wider than
    /// <see cref="MaxPerRow"/> every gap is a layer boundary and the geometry says so on its own,
    /// so ink spent distinguishing them would be ink spent on a distinction that is not there.
    /// nopCommerce and Umbraco draw no rules; Jellyfin draws seven.
    /// </para>
    /// </remarks>
    private static void LayerRules(StringBuilder svg, List<Placed> placed, int width)
    {
        if (!placed.Any(p => p.Continues)) return;

        // One rule per boundary, not one per box: a row of five non-continuing boxes shares a Y.
        var tops = placed.Where(p => !p.Continues).Select(p => p.Y).Distinct().Order().Skip(1);

        foreach (var top in tops)
        {
            var y = top - (GapY / 2);
            svg.Append(CultureInfo.InvariantCulture, $"<path class=\"lr\" d=\"M0 {y} L{width} {y}\"/>\n");
        }
    }

    private static int Widest(List<IGrouping<int, ProjectGroup>> layers)
    {
        var widest = layers.Max(l => Math.Min(l.Count(), MaxPerRow));
        return (widest * BoxWidth) + ((widest - 1) * GapX);
    }

    /// <summary>
    /// One line per dependency the drawing has to carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No arrowheads and no routing.</b> Every edge runs from a higher box to a lower one
    /// because that is what the layering means, so an arrowhead would restate the layout at the
    /// cost of ink — and orthogonal routing on a folded graph is the layout engine
    /// <c>docs/ARCHITECTURE.md</c> §10 decided against. A straight line between box edges is
    /// honest about being a straight line.
    /// </para>
    /// <para>
    /// <b>Only <see cref="ProjectGraph.Reduction"/> is drawn.</b>
    /// Boxes are painted after edges and are opaque, so a line that skips a layer is cut in half
    /// by the box it passes behind, and the two stubs read as a chain through a project the
    /// dependency never names. Drawing every edge put 18 of 29 lines through a box on nopCommerce,
    /// 81 of 98 on Jellyfin and 27 of 44 on Umbraco. The reduction is not a filter over that: it
    /// is every dependency whose reachability no other path already carries, so what a reader can
    /// trace out of the picture is unchanged, and nopCommerce and Umbraco come down to 0 and 2.
    /// </para>
    /// <para>
    /// <b>The rest are disclosed, not dropped</b> — <see cref="ProjectGraph.Implied"/> is what the
    /// caption says, and it is the model's number so that the sentence and the drawing cannot come
    /// to disagree. The shape being avoided is an artifact that shows
    /// a subset while telling the reader it shows everything.
    /// </para>
    /// </remarks>
    private static void Edges(StringBuilder svg, ProjectGraph graph, List<Placed> placed)
    {
        var geometry = placed.ToDictionary(p => p.Index);

        foreach (var (from, to) in graph.Reduction)
        {
            if (!geometry.TryGetValue(from, out var box)) continue;
            if (!geometry.TryGetValue(to, out var into)) continue;

            var x1 = box.X + (BoxWidth / 2);
            var y1 = box.Y + HeightOf(box.Group);
            var x2 = into.X + (BoxWidth / 2);
            var y2 = into.Y;

            svg.Append(CultureInfo.InvariantCulture, $"<path class=\"ed\" d=\"M{x1} {y1} C{x1} {y1 + 20} {x2} {y2 - 20} {x2} {y2}\"/>\n");
        }
    }

    /// <summary>The zone a box is tinted for: the first extreme any project inside it sits in.</summary>
    private static MainSequenceZone ZoneOf(
        ProjectGroup group,
        IReadOnlyDictionary<string, MainSequenceZone> zones) =>
        group.Projects
            .Select(p => zones.GetValueOrDefault(p, MainSequenceZone.None))
            .FirstOrDefault(z => z is MainSequenceZone.Pain or MainSequenceZone.Uselessness);

    private static void Boxes(
        StringBuilder svg,
        List<Placed> placed,
        IReadOnlyDictionary<string, MainSequenceZone> zones,
        IReadOnlyDictionary<string, string> labels)
    {
        foreach (var box in placed)
        {
            var group = box.Group;
            var zone = ZoneOf(group, zones);

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

            // Every project in a folded box is named in it. The
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
    /// The fold is this artifact's best compression — 27
    /// projects to 10 boxes on nopCommerce, against 1444px unfolded — and to a first reader it
    /// reads as an omission. Asked <i>"why isn't Nop.Plugin in the graph?"</i> while looking at the
    /// projects table directly below it, where the plugin names are. The caption says folding
    /// happened; nothing connected a folded box to the names inside it.
    /// </para>
    /// <para>
    /// <b>The names are now in the boxes as well, and this legend is the searchable copy</b> —
    /// Reopened on 2026-08-22. The original fix put them here <i>instead</i>, on the grounds
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

        // The words are Sentences.Zone's, so the box and the caption that
        // keys it cannot come to spell one measure two ways -- which is what they were doing.
        return zone is MainSequenceZone.Pain or MainSequenceZone.Uselessness ? Sentences.Zone(zone) : "";
    }

    /// <summary>
    /// The zones this map actually tinted, in reading order.
    /// </summary>
    /// <remarks>
    /// A reader met an orange box with no key: the definition
    /// lived 170 lines further down under <c>Projects</c>, and A11 round 2 asked about it by name
    /// immediately after T1. The caption already explains the direction convention and the folded
    /// boxes, so the key belongs beside them.
    /// <para>
    /// <b>What fired rather than what exists.</b> The <c>useless</c> tint did not appear on
    /// nopCommerce, so a fixed two-entry key would spend a sentence defining a colour that is not
    /// on the page — and a reader looking for it would conclude they had missed something. The
    /// layout is recomputed here rather than cached for the same reason
    /// <see cref="Folded"/> recomputes: a key and the drawing it explains must not be able to
    /// disagree about which boxes got tinted.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MainSequenceZone> Tinted(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var graph = model.ProjectGraph;
        if (graph.Groups.Count == 0) return [];

        var zones = model.ProjectCouplings.ToDictionary(c => c.Project, c => c.Zone, StringComparer.Ordinal);
        var drawn = graph.Groups.Select(g => ZoneOf(g, zones)).ToHashSet();

        return [.. new[] { MainSequenceZone.Pain, MainSequenceZone.Uselessness }.Where(drawn.Contains)];
    }

    /// <summary>
    /// Whether any layer was too wide for one row, so the drawing had to wrap it.
    /// </summary>
    /// <remarks>
    /// The caption that explains the dashed rules must appear
    /// exactly when the rules do, and this is recomputed from the graph for the same reason
    /// <see cref="Tinted"/> and <see cref="Folded"/> are: a caption and the drawing it explains
    /// must not be able to disagree about whether the thing being explained is on the page.
    /// </remarks>
    public static bool Wraps(ProjectGraph graph) => Rows(graph).Any(row => row.Continues);

    /// <summary>
    /// The rows the drawing lays out, deepest layer first: which boxes are on each, which layer
    /// they belong to, and whether the row continues the one above rather than sitting under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Public, and taking a <see cref="ProjectGraph"/> rather than a
    /// <see cref="SolutionModel"/>, for the reason <see cref="ProjectGraph.Of"/> takes
    /// primitives</b> — the shapes worth asserting about this layout are not in the fixture and
    /// cannot be put there. The fixture has three projects; the case
    /// this exists for is a layer of eleven, and a test that could only run
    /// against three would be asserting that nothing wraps.
    /// </para>
    /// <para>
    /// Recomputed by every caller rather than cached, which is the arrangement
    /// <see cref="Folded"/> and <see cref="Tinted"/> already use: the drawing, the rules on it and
    /// the caption under it must not be able to disagree about which boundaries are real.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Row> Rows(ProjectGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var rows = new List<Row>();

        foreach (var layer in graph.Groups.GroupBy(g => g.Layer).OrderByDescending(l => l.Key))
        {
            var groups = layer.ToList();

            for (var start = 0; start < groups.Count; start += MaxPerRow)
                rows.Add(new Row(
                    layer.Key,
                    [.. groups.Skip(start).Take(MaxPerRow).Select(g => g.Projects[0])],
                    start > 0));
        }

        return rows;
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
    /// That is a walker's mistake made by a renderer instead: a
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

    /// <param name="Index">Which of <see cref="ProjectGraph.Groups"/> this box is, so the
    /// reduction's index pairs can be resolved to geometry.</param>
    /// <param name="Continues">
    /// Whether this box is on a second or later row of a layer that was too wide for one — so the
    /// gap above it is not a layer boundary and means nothing.
    /// </param>
    private readonly record struct Placed(ProjectGroup Group, int X, int Y, int Index, bool Continues);
}

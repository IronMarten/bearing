using System.Globalization;
using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The growth artifact: every analysed type as one cell — A13 tier 1.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is judged by <c>PRD-free-tier.md</c> §9's third metric — installs and referral share —
/// and not by its first.</b> §4's rule that a number must end in a sentence somebody changes their
/// behaviour over is an orientation rule; applying it here would strangle the one artifact whose
/// job is to be worth posting. Tiers 2 and 3 answer to §4 and this one does not, which is a
/// difference stated up front rather than discovered later. What it must still obey is §8: no
/// composite, no grade, and nothing that reads as one.
/// </para>
/// <para>
/// <b>Why it is not <see cref="ArchitectureDiagram"/> again.</b> That draws projects — ten boxes on
/// nopCommerce, and A11 round 1 watched people take them in correctly and scroll past. Ten boxes
/// is also a picture a developer can draw on a whiteboard from memory, so it cannot be the thing
/// that makes somebody send a link. This draws the population underneath: <b>3,209 cells against
/// ten boxes</b>, which is the half of the walk nobody can reproduce by hand.
/// </para>
/// <para>
/// <b>Area is lines of code and the marks are categorical — three states, no magnitude.</b> A cell
/// is left alone when no finding names its type, tinted when one does, and marked when it is one of
/// <see cref="Selection.Exemplars"/>. A mosaic shaded <i>by degree</i> would be a heat map, and a
/// heat map is a score with better manners.
/// </para>
/// <para>
/// <b>Two marks rather than one, because one was measured and it lied.</b> Marking every
/// finding-named type is true cell by cell and false as a picture: findings select large complex
/// components and area is lines, so the two correlate hard — on nopCommerce <b>651 of 3,209 cells,
/// 20% by count, came out 72% of the ink</b>. A picture three-quarters in one alarm colour asserts
/// a verdict over a whole codebase that no finding in it makes, and it is
/// <c>PRD-free-tier.md</c> §9's anti-metric — <i>number of findings; more is worse</i> — rendered
/// as a wash. The tint keeps the volume legible, which is A11 round 1's complaint; the exemplars
/// are 2.6% of the ink and are the triage, which is A13's answer to it. Neither is a threshold:
/// one is <i>some finding names this</i> and the other is X10's selection, which carries no
/// constant.
/// </para>
/// <para>
/// <b>Static, self-contained, and one <c>&lt;path&gt;</c> per class of cell.</b> Three thousand
/// <c>&lt;rect&gt;</c> elements would cost around 190KB against a page that is 286KB whole, and
/// <c>TECHREQ-job-a.md</c> §6 makes bundle size a real budget. The cells are accumulated into two
/// path strings instead — see <see cref="Cells"/>.
/// </para>
/// </remarks>
public static class Mosaic
{
    private const int Width = 1000;
    private const int Height = 600;

    /// <summary>
    /// The strip above the mosaic, and the one below it.
    /// </summary>
    /// <remarks>
    /// <b>A picture that travels has to carry its own caption.</b> The report states what a cell is
    /// in prose underneath, and this file is written so it can be pasted somewhere the prose is
    /// not — which is the whole of <c>PRD-free-tier.md</c> §9's third metric. An unlabelled mosaic
    /// arriving in a channel is a pattern, not an artifact, and nobody clicks through to find out
    /// what tool made it.
    /// </remarks>
    private const int TitleStrip = 30;

    private const int LegendStrip = 26;

    /// <summary>Space between two project blocks. Cells inside a block are separated by one pixel.</summary>
    private const int BlockGap = 4;

    /// <summary>Height of the strip a project's name is written into.</summary>
    private const int LabelStrip = 16;

    /// <summary>
    /// The block below which a name is not written on the picture.
    /// </summary>
    /// <remarks>
    /// Not a threshold about the project — it is the point at which text stops fitting inside a
    /// rectangle, which is arithmetic about this canvas and nothing about the codebase. What the
    /// unlabelled blocks are is <see cref="Unlabelled"/>'s answer, and it is <c>docs/DEFECTS.md</c>
    /// §31 that makes saying so mandatory rather than nice: a reader scanning a picture for a
    /// project name reads its absence as an omission, not as a shortage of pixels.
    /// </remarks>
    private const int NameFitsWidth = 76;

    /// <summary>The same, vertically: the strip plus enough left over to still be a block.</summary>
    private const int NameFitsHeight = 44;

    /// <summary>Renders the mosaic as a standalone SVG document.</summary>
    /// <param name="model">The analysed solution.</param>
    /// <param name="findings">Its findings, already suppressed — what decides which cells are marked.</param>
    public static string Render(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        if (model.Types.Count == 0) return Empty("No type was analysed.");

        var blocks = Blocks(model);
        var marks = Marks(model, findings);
        var height = TitleStrip + Height + LegendStrip;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {Width} {height}\" width=\"{Width}\" height=\"{height}\" font-family=\"system-ui,-apple-system,Segoe UI,Roboto,sans-serif\">\n");
        svg.Append("<style>\n");
        // The tint separates from the plain cell by HUE and not by weight, and that is the whole
        // repair. Findings cover 72% of the ink on a real solution, so a tint that is darker or
        // stronger becomes the foreground and the picture asserts what the one-mark version
        // asserted. Equal lightness, cool against warm: one texture at a glance, two populations on
        // inspection, and the only thing with contrast against both is the ten cells that matter.
        svg.Append(".bg{fill:#f4f3f0}\n");
        svg.Append(".bl{fill:#e9e7e2}\n");
        svg.Append(".c{fill:#c3c4c1}\n");
        svg.Append(".n{fill:#c6bba4}\n");
        svg.Append(".f{fill:#b4483c;stroke:#6f2820;stroke-width:1.5}\n");
        svg.Append(".pn{font-size:11px;font-weight:600;fill:#4a4844}\n");
        svg.Append(".ti{font-size:15px;font-weight:600;fill:#1a1a1a}\n");
        svg.Append(".lg{font-size:11px;fill:#6b6b6b}\n");
        svg.Append("@media(prefers-color-scheme:dark){.bg{fill:#16171a}.bl{fill:#212329}");
        svg.Append(".c{fill:#42454b}.n{fill:#4d4738}.f{fill:#d9615a;stroke:#f2b3ae}");
        svg.Append(".pn{fill:#a9a7a2}.ti{fill:#e9e8e6}.lg{fill:#9a9a97}}\n");
        svg.Append("</style>\n");

        svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"bg\" width=\"{Width}\" height=\"{height}\"/>\n");

        Title(svg, model);

        foreach (var block in blocks)
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<rect class=\"bl\" x=\"{block.Area.X}\" y=\"{block.Area.Y + TitleStrip}\" width=\"{block.Area.W}\" height=\"{block.Area.H}\" rx=\"2\"/>\n");

            if (block.IsNamed)
                svg.Append(CultureInfo.InvariantCulture,
                    $"<text class=\"pn\" x=\"{block.Area.X + 4}\" y=\"{block.Area.Y + TitleStrip + 12}\">{Html.Text(block.Label)}</text>\n");
        }

        Cells(svg, blocks, marks);
        Legend(svg, height);

        svg.Append("</svg>\n");
        return svg.ToString();
    }

    /// <summary>Renders the mosaic and writes it to <paramref name="path"/>.</summary>
    public static void Write(string path, SolutionModel model, FindingSet findings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, Render(model, findings), new UTF8Encoding(false));
    }

    /// <summary>
    /// How many cells carry each mark.
    /// </summary>
    /// <remarks>
    /// Offered so a caption can state the counts rather than leaving a reader to estimate an area
    /// by eye, which is the thing area encodings are worst at — and it is the measurement that
    /// showed one mark was not enough. See the remarks on <see cref="Mosaic"/>.
    /// </remarks>
    public static MosaicMarks Marked(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var (named, leading) = Marks(model, findings);

        // The share of the DRAWN AREA the tint covers, which is not the share of the cells — cell
        // area is lines of code, and the types a finding names are systematically the large ones.
        // On nopCommerce that is 17% of the types holding 58% of the code, and a caption that reads
        // the count out over a picture drawn by size is telling a reader something the picture in
        // front of them contradicts. docs/TESTING.md's third real run measured this for the mark
        // rule; this is the same measurement, offered so the caption can state it instead of
        // walking into it.
        var lines = (double)model.Types.Sum(t => t.LinesOfCode);
        var tinted = model.Types.Where(t => named.Contains(t.Subject.Canonical)).Sum(t => t.LinesOfCode);

        return new MosaicMarks(named.Count, leading.Count, lines > 0 ? tinted / lines : 0);
    }

    /// <summary>
    /// The projects whose block came out too small to write a name inside, largest first.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §31, and the same remedy <see cref="ArchitectureDiagram.Folded"/>
    /// uses: the picture cannot be searched, so the names go beside it as text. The layout is
    /// recomputed rather than cached, so a caption and the drawing it explains cannot disagree
    /// about which blocks got a label.
    /// </remarks>
    public static IReadOnlyList<string> Unlabelled(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return [.. Blocks(model).Where(b => !b.IsNamed).Select(b => b.Label)];
    }

    private static string Empty(string why) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 320 40\" width=\"320\" height=\"40\">"
        + $"<text x=\"0\" y=\"20\" font-size=\"12\" fill=\"#6b6b6b\">{Html.Text(why)}</text></svg>\n";

    // ------------------------------------------------------------------------ cells ----

    /// <summary>
    /// Every cell, as two path strings: the ones a finding names and the ones it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two elements for three thousand cells.</b> A <c>&lt;rect&gt;</c> apiece is roughly 60
    /// bytes of attributes; <c>M12 34h7v7h-7z</c> is a sixth of that and carries the same rectangle,
    /// so the drawing costs tens of kilobytes rather than hundreds. It also removes the only thing
    /// that made the count of cells expensive, which is what lets this draw the whole population
    /// instead of a sample — and a sampled mosaic would be <c>docs/DEFECTS.md</c> §3 in a picture.
    /// </para>
    /// <para>
    /// <b>Whole pixels, and a floor of one.</b> Fractional coordinates would cost more bytes than
    /// the geometry is worth at this scale, and a type of one line rounds to a cell too small to
    /// see rather than to no cell at all — every analysed type is on the picture, which is the
    /// claim the caption makes.
    /// </para>
    /// </remarks>
    private static void Cells(StringBuilder svg, IReadOnlyList<Block> blocks, Marking marks)
    {
        var plain = new StringBuilder();
        var named = new StringBuilder();
        var leading = new StringBuilder();

        foreach (var block in blocks)
            foreach (var (type, cell) in block.Cells)
            {
                var id = type.Subject.Canonical;
                var w = Math.Max(1, (int)cell.W - 1);
                var h = Math.Max(1, (int)cell.H - 1);

                var into = marks.Leading.Contains(id) ? leading
                    : marks.Named.Contains(id) ? named
                    : plain;

                into.Append(CultureInfo.InvariantCulture,
                    $"M{(int)cell.X} {(int)cell.Y + TitleStrip}h{w}v{h}h-{w}z");
            }

        // Drawn weakest mark first, so a strong cell is never clipped by a neighbour's edge.
        if (plain.Length > 0) svg.Append("<path class=\"c\" d=\"").Append(plain).Append("\"/>\n");
        if (named.Length > 0) svg.Append("<path class=\"n\" d=\"").Append(named).Append("\"/>\n");
        if (leading.Length > 0) svg.Append("<path class=\"f\" d=\"").Append(leading).Append("\"/>\n");
    }

    /// <summary>
    /// Which types carry which mark: the ones a finding is about, and the ones the report leads
    /// with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Subjects, walked to the declaring type, and deliberately not participants.</b> Same rule
    /// <see cref="HtmlReport"/>'s drill-down applies and for the same reason: a type a finding
    /// merely <i>names</i> is not a type the finding is a claim about, and marking it would say the
    /// tool nominated something it did not. The member walk is
    /// <see cref="SubjectRef.DeclaringType"/>'s job — method level is the primary level, so a
    /// concealed decision on a method has to land on the type that declares it, or the 1,091
    /// findings that dominate a real run would mark nothing at all.
    /// </para>
    /// <para>
    /// <b>The leading set is a subset of the named set by construction</b> — an exemplar is one of
    /// its kind's findings — so the picture cannot show a type as led-with but not named, and
    /// <see cref="Cells"/> tests for the stronger mark first.
    /// </para>
    /// </remarks>
    private static Marking Marks(SolutionModel model, FindingSet findings)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        var leading = new HashSet<string>(StringComparer.Ordinal);

        foreach (var canonical in Subjects.Named(model, findings))
            named.Add(canonical);

        foreach (var exemplar in Selection.Exemplars(findings))
            if (Subjects.Of(model, exemplar) is { } type) leading.Add(type.Subject.Canonical);

        return new Marking(named, leading);
    }

    // ----------------------------------------------------------------------- layout ----

    /// <summary>
    /// One block per project that declares an analysed type, with its types laid out inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two levels of the same treemap: projects into the canvas, then a project's types into the
    /// rectangle that came back. Both are weighted by lines of code, so a block's area is the
    /// share of the codebase that project holds and a cell's area is the share that type holds —
    /// one unit throughout, rather than a count at one level and a size at the other.
    /// </para>
    /// <para>
    /// <b>Ordered by a total key</b> — weight descending, then project name — because a stable sort
    /// on a non-total key reproduces on one machine without being a property of the tool, which is
    /// <c>docs/ARCHITECTURE.md</c> §10's rule for every emitted artifact. Two projects of identical
    /// size would otherwise swap places between runs and make a re-generated picture look like a
    /// changed codebase.
    /// </para>
    /// </remarks>
    private static List<Block> Blocks(SolutionModel model)
    {
        var projects = model.Types
            .GroupBy(t => t.Project, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Types: g.OrderByDescending(t => t.LinesOfCode)
                .ThenBy(t => t.Subject.Canonical, StringComparer.Ordinal).ToList()))
            .Select(p => (p.Name, p.Types, Weight: (double)p.Types.Sum(t => t.LinesOfCode)))
            .OrderByDescending(p => p.Weight)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var areas = Squarify(
            projects.Select(p => p.Weight).ToList(),
            new Rect(0, 0, Width, Height));

        var blocks = new List<Block>(projects.Count);

        for (var i = 0; i < projects.Count; i++)
        {
            var (name, types, _) = projects[i];
            var outer = Inset(areas[i], BlockGap / 2.0);
            var named = outer.W >= NameFitsWidth && outer.H >= NameFitsHeight;

            // The label strip is taken out of the drawable area rather than drawn over the cells,
            // so a name never sits on top of a type it is not about.
            var inner = named
                ? new Rect(outer.X + 2, outer.Y + LabelStrip, Math.Max(1, outer.W - 4), Math.Max(1, outer.H - LabelStrip - 2))
                : Inset(outer, 1);

            var cells = Squarify(types.Select(t => (double)t.LinesOfCode).ToList(), inner);

            blocks.Add(new Block(
                name,
                outer,
                named,
                [.. types.Select((t, n) => (t, cells[n]))]));
        }

        return blocks;
    }

    private static Rect Inset(Rect rect, double by) =>
        new(rect.X + by, rect.Y + by, Math.Max(1, rect.W - (2 * by)), Math.Max(1, rect.H - (2 * by)));

    /// <summary>
    /// Squarified treemap — Bruls, Huizing and van Wijk, 2000.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rectangles are laid in rows across the shorter side of what is left, and a row closes when
    /// adding the next item would make its worst aspect ratio worse rather than better. That is the
    /// whole algorithm, and it is arithmetic — <c>docs/ARCHITECTURE.md</c> §10 already took the
    /// decision that both graph artifacts are static, and this needs a layout engine no more than
    /// the project map did.
    /// </para>
    /// <para>
    /// <b>Slice-and-dice was the alternative and it is worse here for a measurable reason:</b> at
    /// 1,218 types in one project it produces slivers a pixel wide and a block tall, which is not a
    /// mosaic of a codebase so much as a barcode of one. Squarifying keeps cells near square, which
    /// is what makes a small one visible at all.
    /// </para>
    /// <para>
    /// Returns one rectangle per weight, in the order the weights were given, so a caller can zip
    /// them back against the items they came from.
    /// </para>
    /// </remarks>
    private static List<Rect> Squarify(IReadOnlyList<double> weights, Rect area)
    {
        var placed = new Rect[weights.Count];
        var total = weights.Sum();

        // A project of no lines cannot be sized by lines. It also cannot happen — every analysed
        // type spans at least one line — so this is the arm that keeps a division honest rather
        // than one that renders anything anybody will see.
        if (total <= 0)
        {
            for (var i = 0; i < placed.Length; i++) placed[i] = new Rect(area.X, area.Y, 1, 1);
            return [.. placed];
        }

        var scale = area.W * area.H / total;
        var remaining = area;
        var index = 0;

        while (index < weights.Count)
        {
            var side = Math.Min(remaining.W, remaining.H);
            var row = new List<int>();
            double sum = 0, min = double.MaxValue, max = 0;

            while (index < weights.Count)
            {
                var next = weights[index] * scale;
                var worstNow = row.Count == 0 ? double.MaxValue : Worst(sum, min, max, side);
                var worstWith = Worst(sum + next, Math.Min(min, next), Math.Max(max, next), side);

                if (row.Count > 0 && worstWith > worstNow) break;

                row.Add(index++);
                sum += next;
                min = Math.Min(min, next);
                max = Math.Max(max, next);
            }

            remaining = LayOut(weights, scale, row, sum, remaining, placed);
        }

        return [.. placed];
    }

    /// <summary>The worst aspect ratio in a row, which is what the algorithm minimises.</summary>
    private static double Worst(double sum, double min, double max, double side)
    {
        if (sum <= 0 || side <= 0 || min <= 0) return double.MaxValue;

        var length = sum / side;
        return Math.Max(length / (min / length), max / length / length);
    }

    /// <summary>Places one row along the shorter side and returns what is left over.</summary>
    private static Rect LayOut(
        IReadOnlyList<double> weights, double scale, List<int> row, double sum, Rect area, Rect[] placed)
    {
        var horizontal = area.W <= area.H;
        var side = horizontal ? area.W : area.H;
        var depth = side <= 0 ? 0 : sum / side;
        var offset = 0.0;

        foreach (var item in row)
        {
            var length = depth <= 0 ? 0 : weights[item] * scale / depth;

            placed[item] = horizontal
                ? new Rect(area.X + offset, area.Y, length, depth)
                : new Rect(area.X, area.Y + offset, depth, length);

            offset += length;
        }

        return horizontal
            ? new Rect(area.X, area.Y + depth, area.W, Math.Max(0, area.H - depth))
            : new Rect(area.X + depth, area.Y, Math.Max(0, area.W - depth), area.H);
    }

    // ------------------------------------------------------------------------ words ----

    /// <summary>
    /// What solution this is a picture of, and how big it is.
    /// </summary>
    /// <remarks>
    /// <b>Counts, and no measurement.</b> Two whole numbers a reader can check against the report
    /// are not the thing <c>PRD-free-tier.md</c> §8 rules out; a ratio, a share or a grade would be,
    /// and a picture that travels without its caption is exactly where one would do the most harm.
    /// </remarks>
    private static void Title(StringBuilder svg, SolutionModel model)
    {
        var solution = Path.GetFileNameWithoutExtension(model.SolutionPath);
        var projects = model.Types.Select(t => t.Project).Distinct(StringComparer.Ordinal).Count();

        svg.Append(CultureInfo.InvariantCulture, $"<text class=\"ti\" x=\"2\" y=\"18\">{Html.Text(solution)}</text>\n");
        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"lg\" x=\"{Width - 2}\" y=\"18\" text-anchor=\"end\">"
            + $"{Html.Count(model.Types.Count)} types in {Html.Count(projects)} projects · one cell each, sized by lines</text>\n");
    }

    /// <summary>What the three states mean, so the file explains itself when it is pasted alone.</summary>
    private static void Legend(StringBuilder svg, int height)
    {
        var y = height - 9;
        var x = 2;

        foreach (var (css, label) in new[]
        {
            ("f", "the findings lead with this"),
            ("n", "a finding names it"),
            ("c", "no finding names it"),
        })
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<rect class=\"{css}\" x=\"{x}\" y=\"{y - 9}\" width=\"10\" height=\"10\"/>\n");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text class=\"lg\" x=\"{x + 15}\" y=\"{y}\">{Html.Text(label)}</text>\n");

            x += 24 + (label.Length * 6);
        }

        svg.Append(CultureInfo.InvariantCulture,
            $"<text class=\"lg\" x=\"{Width - 2}\" y=\"{y}\" text-anchor=\"end\">Bearing · github.com/ironmarten/bearing</text>\n");
    }

    private readonly record struct Rect(double X, double Y, double W, double H);

    private sealed record Block(
        string Label,
        Rect Area,
        bool IsNamed,
        IReadOnlyList<(TypeNode Type, Rect Cell)> Cells);

    private sealed record Marking(HashSet<string> Named, HashSet<string> Leading);
}

/// <summary>
/// How many of the mosaic's cells carry each mark.
/// </summary>
/// <param name="Named">Types some finding is about.</param>
/// <param name="Leading">
/// Types the report leads with — <see cref="Selection.Exemplars"/>, and a subset of
/// <paramref name="Named"/>.
/// </param>
/// <param name="NamedInk">
/// The share of the drawn area those named cells cover, from 0 to 1. <b>It is not
/// <paramref name="Named"/> over the type count and the gap is the point</b>: area is lines of
/// code, findings select large components, and on nopCommerce 17% of the types are 58% of the ink.
/// A caption stating a count over a picture drawn by size is contradicted by the picture.
/// </param>
public readonly record struct MosaicMarks(int Named, int Leading, double NamedInk);

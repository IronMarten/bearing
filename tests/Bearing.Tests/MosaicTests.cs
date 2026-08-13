using System.Text.RegularExpressions;
using System.Xml.Linq;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The mosaic — A13 tier 1.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a test can hold here is narrower than usual, and worth naming.</b> Tier 1 is judged by
/// <c>PRD-free-tier.md</c> §9's third metric — whether anybody shares it — which no assertion
/// reaches. What is asserted is the four mechanical properties the artifact would be worthless
/// without: it is valid SVG, every analysed type is on it exactly once, the marks are the findings
/// and nothing else, and it carries no score.
/// </para>
/// <para>
/// The fixture is 132 types in three projects, so it exercises the layout and not the scale the
/// picture exists for. The real measurement is a real solution and it is recorded in
/// <c>docs/TESTING.md</c> §7.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class MosaicTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    private string Svg => Mosaic.Render(core.Model, Findings);

    [Fact]
    public Task The_mosaic_renders() => Verify(Svg, extension: "svg");

    /// <summary>It is well-formed XML, which is what makes it an image rather than a string.</summary>
    /// <remarks>
    /// The lesson is <c>ArchitectureDiagramTests</c>': an SVG with an unescaped name in it does not
    /// render badly, it fails to parse and shows nothing at all — and an artifact whose whole job is
    /// being pasted somewhere is then blank.
    /// </remarks>
    [Fact]
    public void The_mosaic_is_well_formed_xml()
    {
        var document = XDocument.Parse(Svg);

        Assert.Equal("svg", document.Root!.Name.LocalName);
        Assert.Equal("http://www.w3.org/2000/svg", document.Root.Name.NamespaceName);
    }

    /// <summary>
    /// Every analysed type is on the picture, exactly once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The caption says <i>every one of the types this run analysed</i>, so this is the sentence
    /// under test rather than a property of the layout.</b> A mosaic that quietly dropped the tail
    /// of a large project would look complete — that is the whole failure mode of an area encoding,
    /// and it is <c>docs/DEFECTS.md</c> §3 in a medium where nobody can count the elements by eye.
    /// </para>
    /// <para>
    /// Counted as path commands rather than as elements, because the cells are accumulated into two
    /// paths to keep the drawing inside §6's bundle budget. One <c>M</c> is one cell.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_analysed_type_is_one_cell() =>
        Assert.Equal(core.Model.Types.Count, Cells("c") + Cells("n") + Cells("f"));

    /// <summary>
    /// A cell is tinted when a finding is about that type, and not when one merely names it.
    /// </summary>
    /// <remarks>
    /// The same rule <see cref="HtmlReport"/>'s drill-down applies. Marking a participant would say
    /// the tool nominated a component it did not, which is a claim rather than a shading choice —
    /// and the member walk has to happen or a real run's 1,091 method-level findings mark nothing.
    /// </remarks>
    [Fact]
    public void The_tinted_cells_are_the_types_a_finding_is_about()
    {
        var findings = Findings;

        var expected = Types(findings.All).Count;
        var tinted = Cells("n") + Cells("f");

        Assert.Equal(expected, tinted);
        Assert.Equal(expected, Mosaic.Marked(core.Model, findings).Named);

        // The fixture is deliberately bad code, so a mosaic with nothing marked would mean the
        // resolution silently failed rather than that the codebase is clean.
        Assert.True(tinted > 0);
    }

    /// <summary>
    /// The strong mark is X10's selection and nothing else — one exemplar per kind that fired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two marks rather than one is a measurement, not a taste.</b> Marking every finding-named
    /// type put 651 of nopCommerce's 3,209 cells in the alarm colour — 20% by count and
    /// <b>72% by area</b>, because findings select large components and area is lines. This asserts
    /// the repair: the strong mark is bounded by the number of kinds that fired, which is what makes
    /// it self-scaling rather than capped.
    /// </para>
    /// <para>
    /// A count and not a set of names, because <c>SelectionTests</c> holds which findings are
    /// chosen. What is this test's business is that the drawing marks those and no others.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_strong_mark_is_the_selection_the_findings_pane_leads_with()
    {
        var findings = Findings;
        var exemplars = Selection.Exemplars(findings);

        Assert.Equal(Types(exemplars).Count, Cells("f"));
        Assert.Equal(Types(exemplars).Count, Mosaic.Marked(core.Model, findings).Leading);

        // A subset of the tint by construction: an exemplar is one of its kind's findings.
        Assert.True(Cells("f") > 0);
        Assert.True(Cells("f") <= exemplars.Count);
        Assert.True(Cells("f") + Cells("n") <= core.Model.Types.Count);
    }

    /// <summary>
    /// Every project that declares an analysed type is either named on the picture or named beside
    /// it.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §31: a reader scanning a picture for a project name reads its absence
    /// as an omission. A block too small for text is a shortage of pixels, and the caption has to
    /// carry what the block cannot.
    /// </remarks>
    [Fact]
    public void Every_project_is_named_on_the_picture_or_beside_it()
    {
        var drawn = Regex.Matches(Svg, """<text class="pn"[^>]*>([^<]*)</text>""")
            .Select(m => m.Groups[1].Value)
            .Concat(Mosaic.Unlabelled(core.Model))
            .ToList();

        Assert.Equal(drawn.Count, drawn.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            core.Model.Types.Select(t => t.Project).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal),
            drawn.OrderBy(p => p, StringComparer.Ordinal));
    }

    /// <summary>
    /// No measurement reaches the drawing as text.
    /// </summary>
    /// <remarks>
    /// The same assertion <c>ArchitectureDiagramTests</c> makes, and it binds harder here: this
    /// artifact exists to be posted somewhere without its caption, so a number on it is a number
    /// with no sentence attached — <c>PRD-free-tier.md</c> §4 and §8. The only text is a project
    /// name.
    /// </remarks>
    [Fact]
    public void The_mosaic_carries_no_scores()
    {
        foreach (var text in Regex.Matches(Svg, "<text[^>]*>([^<]*)</text>").Select(m => m.Groups[1].Value))
        {
            Assert.DoesNotMatch(@"\d\.\d", text);
            Assert.DoesNotMatch(@"\d\s*%", text);
        }
    }

    /// <summary>The drawing fetches nothing, like the page that embeds it.</summary>
    [Fact]
    public void The_mosaic_requests_nothing_from_the_network()
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
            core.Model, Findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains(Svg, page, StringComparison.Ordinal);
    }

    /// <summary>A solution with no analysed type says so rather than drawing an empty canvas.</summary>
    /// <remarks>
    /// <para>
    /// <b>Worth a second workspace load, which is the suite's cost centre.</b> The layout divides
    /// by a total weight, and an empty model is the one input where that total is zero — so the
    /// guard against it is the arm most likely to ship unexercised and least likely to fail
    /// quietly: a division by zero here produces <c>NaN</c> coordinates, and an SVG with a
    /// <c>NaN</c> in a path renders as a blank rectangle rather than as an error.
    /// </para>
    /// <para>
    /// Reached by excluding every C# file rather than by constructing a model, because a
    /// <see cref="SolutionModel"/> can only be produced by a walk — <c>Report.NotAnalysed</c>
    /// records the same constraint and takes the other way around it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_empty_solution_draws_nothing_and_says_so()
    {
        var model = await new SolutionWalker(new WalkOptions
        {
            SolutionPath = RepoPaths.TestBedSolution,
            ExcludedPathFragments = [".cs"],
        }).WalkAsync();

        Assert.Empty(model.Types);

        var svg = Mosaic.Render(model, FindingSet.Empty);

        Assert.Contains("No type was analysed", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<path", svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// How many cells one class holds. One <c>M</c> is one cell — see
    /// <see cref="Every_analysed_type_is_one_cell"/> for why the drawing is batched into paths.
    /// </summary>
    private int Cells(string css) =>
        Regex.Matches(Svg, $"""<path class="{css}" d="([^"]*)"/>""")
            .Sum(m => m.Groups[1].Value.Count(c => c == 'M'));

    /// <summary>The distinct analysed types a set of findings is about.</summary>
    private HashSet<string> Types(IEnumerable<Finding> findings) =>
    [
        .. findings
            .Select(f => core.Model.Find(f.Subject)
                         ?? (f.Subject.DeclaringType is { } d ? core.Model.Find(d) : null))
            .Where(t => t is not null)
            .Select(t => t!.Subject.Canonical)
    ];
}

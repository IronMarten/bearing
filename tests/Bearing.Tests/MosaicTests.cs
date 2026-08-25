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
    /// A cell is tinted when a claim is about that type, and not when one merely names it.
    /// </summary>
    /// <remarks>
    /// The same rule <see cref="HtmlReport"/>'s drill-down applies. Marking a participant would say
    /// the tool nominated a component it did not, which is a claim rather than a shading choice —
    /// and the member walk has to happen or a real run's 1,091 method-level findings mark nothing.
    /// </remarks>
    [Fact]
    public void The_tinted_cells_are_the_types_a_claim_is_about()
    {
        var findings = Findings;

        var expected = Types(findings.All.Where(f => Claims.IsRiskClaim(f.Kind))).Count;
        var tinted = Cells("n") + Cells("f");

        Assert.Equal(expected, tinted);
        Assert.Equal(expected, Mosaic.Marked(core.Model, findings).Named);

        // The fixture is deliberately bad code, so a mosaic with nothing marked would mean the
        // resolution silently failed rather than that the codebase is clean.
        Assert.True(tinted > 0);
    }

    /// <summary>
    /// A type whose only entry is the coverage disclosure is not tinted — <c>docs/DEFECTS.md</c>
    /// §41.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The assertion above cannot fail on its own, and that is why this one exists.</b> It
    /// compares the picture against <see cref="Subjects.Named"/>'s population; both are one
    /// derivation, so both moved together for four releases while the caption, the clean tile and
    /// the plot's y-axis all said something the census contradicted in words. What pins the meaning
    /// is the <i>difference</i> between the two populations, so this names it and requires it to be
    /// non-empty.
    /// </para>
    /// <para>
    /// <b>The fixture reaches this without a plant.</b> Its cohort floor leaves types with no peer
    /// group, most of which carry no other claim — the same shape nopCommerce has at 104 types, two
    /// orders of magnitude up. If a fixture change ever empties this set the assertion fails rather
    /// than passing vacuously, which is the property <c>docs/TESTING.md</c> §8 asks a gate for.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_disclosure_alone_does_not_tint_a_cell()
    {
        var findings = Findings;

        var disclosedOnly = Types(findings.All.Where(f => !Claims.IsRiskClaim(f.Kind)))
            .Except(Types(findings.All.Where(f => Claims.IsRiskClaim(f.Kind))), StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(disclosedOnly);
        Assert.Equal(
            Types(findings.All).Count - disclosedOnly.Count,
            Mosaic.Marked(core.Model, findings).Named);
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
        var claims = Selection.Exemplars(findings).Where(f => Claims.IsRiskClaim(f.Kind)).ToList();

        Assert.Equal(Types(claims).Count, Cells("f"));
        Assert.Equal(Types(claims).Count, Mosaic.Marked(core.Model, findings).Leading);

        // A subset of the tint by construction: an exemplar is one of its kind's findings.
        Assert.True(Cells("f") > 0);
        Assert.True(Cells("f") <= claims.Count);
        Assert.True(Cells("f") + Cells("n") <= core.Model.Types.Count);
    }

    /// <summary>
    /// The caption's count of outlined cells is the number of claims the page prints above it —
    /// <c>docs/DEFECTS.md</c> §40.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two numbers were built by different selectors and never met.</b>
    /// <see cref="Selection.Exemplars"/> returns one per kind that fired, coverage included;
    /// <see cref="Highlights"/> drops coverage because it is a disclosure. The mosaic outlined the
    /// first set and its caption named the second, so on every real run it said <i>N</i> and meant
    /// <i>N − 1</i> — never noticed, because nothing on the page put the counts side by side. This
    /// test is that side-by-side.
    /// </para>
    /// <para>
    /// <b>Asserted against the count the caption is built from</b> rather than against a literal:
    /// a number pinned here would have to be retuned every time the fixture gains a kind, and a
    /// retuned number is not a gate.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_outlined_cells_are_exactly_the_claims_the_page_leads_with()
    {
        var findings = Findings;
        var exemplars = Selection.Exemplars(findings);
        var claims = exemplars.Where(f => Claims.IsRiskClaim(f.Kind)).ToList();

        var claimed = Types(claims);
        var everything = Types(exemplars);

        // The disclosure fired here and landed on a type no claim did, or the two counts are equal
        // for a reason that has nothing to do with the fix and this passes vacuously.
        Assert.True(
            everything.Count > claimed.Count,
            "the fixture no longer has a disclosure exemplar the claims do not also name");

        Assert.Equal(claimed.Count, Mosaic.Marked(core.Model, findings).Leading);
        Assert.Equal(claimed.Count, Cells("f"));
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

    /// <summary>
    /// The caption states the tinted <i>area</i> as well as the tinted <i>count</i>, because they
    /// are not the same number and the reader can see the difference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The measurement that forced this.</b> On nopCommerce <b>17% of the types are named and
    /// they hold 58% of the code</b>, so a caption reading the count out over a picture drawn by
    /// size tells a reader something the picture in front of them contradicts — and the first
    /// reader to try to synthesise a claim from the drawing produced three wrong ones, every one of
    /// them the area encoding read exactly as drawn: the biggest project looked worst and is the
    /// <i>least</i> dense, the densest project went unmentioned because it is small, and a project
    /// at 26% read as <i>"almost all of it"</i>.
    /// </para>
    /// <para>
    /// <b>The encoding question was answered by X11 and this pins what is left.</b> The mosaic no
    /// longer opens the report — <see cref="ReachPlot"/> does, in counts — and what the mosaic
    /// keeps is the claim that every analysed type is on the page. It still has to say that its
    /// area means lines, because a reader comparing it with the plot above would otherwise find
    /// them disagreeing with no way to tell which one was lying.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_caption_says_the_tinted_area_is_not_the_tinted_count()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var marks = Mosaic.Marked(core.Model, findings);

        var byCount = (double)marks.Named / core.Model.Types.Count;

        Assert.True(
            marks.NamedInk > byCount,
            "the fixture no longer paints more ink than cells, so this asserts nothing");

        var page = HtmlReport.Render(
            core.Model, findings, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("Read it by count and not by area", page, StringComparison.Ordinal);
        Assert.Contains(
            $"{Math.Round(100 * marks.NamedInk):0}% of this drawing", page, StringComparison.Ordinal);
        Assert.Contains(
            $"{Math.Round(100.0 * marks.Named / core.Model.Types.Count):0}% of the types",
            page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The legend's three counts partition the analysed types, and say so in <i>types</i>.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/DEFECTS.md</c> §50.</b> Both of the mosaic's marks are per type and readers
    /// counted both as findings — <i>"counts of findings across types"</i>, two of five in A11
    /// round 2's T9, answering independently and in writing. The remedy is counts and a named
    /// population on every swatch, and the property that keeps it honest is that the tinted count
    /// plus the plain count is the whole population: a legend whose numbers do not add up to the
    /// picture is <c>docs/DEFECTS.md</c> §40 with better manners.
    /// </remarks>
    [Fact]
    public void The_legend_partitions_the_types_it_draws()
    {
        var marks = Mosaic.Marked(core.Model, Findings);
        var total = core.Model.Types.Count;

        var named = Number("some finding names");
        var plain = Number("no finding names");
        var leading = Number("the report leads with");

        Assert.Equal(marks.Named, named);
        Assert.Equal(marks.Leading, leading);
        Assert.Equal(total, named + plain);
    }

    /// <summary>
    /// Every swatch names its population, so a mark cannot be read as a count of findings.
    /// </summary>
    /// <remarks>
    /// The wording is what <c>docs/DEFECTS.md</c> §50 diagnosed: the old legend had <i>a
    /// finding</i> as the subject of a per-<i>type</i> mark. <i>type</i> or <i>types</i> on each
    /// swatch is the half of the fix a golden would not notice going away.
    /// </remarks>
    [Fact]
    public void Every_swatch_names_a_population_of_types()
    {
        var labels = Regex.Matches(Svg, @"<text class=""lg""[^>]*>([^<]*)</text>")
            .Select(m => m.Groups[1].Value)
            .Where(text => text.Contains("finding", StringComparison.Ordinal)
                           || text.Contains("leads with", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, labels.Count);
        Assert.All(labels, label => Assert.Matches(@"^[\d,]+ types? ", label));
    }

    /// <summary>The number on a swatch, read back off the rendered picture.</summary>
    private int Number(string tail)
    {
        var match = Regex.Match(Svg, @">([\d,]+) types? " + Regex.Escape(tail) + "<");

        Assert.True(match.Success, $"no swatch reading '... {tail}'");

        return int.Parse(match.Groups[1].Value.Replace(",", "", StringComparison.Ordinal),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}

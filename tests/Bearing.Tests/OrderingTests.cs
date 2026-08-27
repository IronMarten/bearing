using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// Every emitted artifact must be a function of the analysis, not of the order the analysis
/// happened to arrive in.
/// </summary>
/// <remarks>
/// <para>
/// The probe's goldens reproduced perfectly long before this test existed — six separate
/// processes, byte-identical every time. That proved nothing. Every writer sorted on a non-total
/// key, so most rows were positioned by the enumeration order of a <c>Dictionary</c>, which is
/// insertion order, which is project load order times Roslyn's symbol order. 98.5% of
/// <c>edges.csv</c> and 82.4% of <c>types.csv</c> sat in a tie group. It reproduced because
/// nothing had perturbed it yet, and <b>reversing the project declaration order in
/// <c>TestBed.sln</c> — an edit with no semantic content — was enough to move all of it.</b>
/// </para>
/// <para>
/// <b>R2 turned that sentence into the test.</b> Until the probe was retired this shuffled the
/// probe's <c>AnalysisResult</c> in memory and re-rendered, which worked because that model was a
/// bag of public lists anyone could permute. Core's is not: its constructor is internal and
/// <c>ModelBuilder</c> canonicalises as it builds, so an in-memory shuffle would have had to
/// reach past the very defence it was meant to test. The perturbation moved to where the remark
/// above always described it — the solution file — and TestBed is now walked twice, once as
/// declared and once with its four project lines reversed.
/// </para>
/// <para>
/// <b>It is a stronger test than the one it replaces, and it found something on its first run.</b>
/// A shuffle could only perturb what the renderer was handed; this perturbs the workspace load,
/// so it covers the walk, the model and the renderers together. <c>types.csv</c>,
/// <c>edges.csv</c>, <c>members.csv</c> and the terminal report were already stable. The JSON
/// export was not — its <c>projects</c> array came out in declaration order, because
/// <c>SolutionModel.Projects</c> was the one collection <c>ModelBuilder</c> did not sort. That is
/// Fixed in the commit that brought this file across.
/// </para>
/// <para>
/// The control is unchanged: remove an <c>OrderBy</c> that a renderer or the builder relies on
/// and this fails while the snapshots stay green, because a snapshot only ever sees one order.
/// </para>
/// <para>
/// <b>Which <c>OrderBy</c>, measured when the four graph artifacts were added.</b> Not every sort
/// is load-bearing on a four-project fixture, and writing down which are is what stops a future
/// green run being read as more than it is. Three mutations, run:
/// </para>
/// <list type="bullet">
/// <item>Deleting <c>ModelBuilder</c>'s sort of <c>SolutionModel.Projects</c> — the one R2 found —
/// fails <c>Json</c> and nothing else. The drawings never read that collection.</item>
/// <item>Deleting the <c>ThenBy</c> tiebreaks in <c>ProjectGraph.Fold</c> or <c>Mosaic.Blocks</c>
/// fails nothing, because no two projects here tie on layer or on weight. Those tiebreaks are
/// correct and this fixture cannot exercise them; that is a gap in the fixture, not in them.</item>
/// <item>Making <c>Mosaic</c> enumerate <c>model.Projects</c> rather than grouping
/// <c>model.Types</c>, with that sort also removed, fails the mosaic and both pages. That is the
/// regression these four assertions exist for, and it fires.</item>
/// </list>
/// <para>
/// So the four drawings are stable <b>by construction</b>: they derive from <c>Types</c> and
/// <c>Edges</c>, which the model canonicalises, and never from an insertion-ordered collection.
/// That is the property being pinned, and it is worth pinning precisely because it is invisible —
/// reaching for <c>model.Projects</c> is the natural thing to write, and it is what
/// <c>JsonOutput</c> did until R2.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class OrderingTests(CoreWalkFixture core)
{
    /// <summary>
    /// TestBed, walked from a solution file that declares its projects in the opposite order.
    /// </summary>
    /// <remarks>
    /// <b>Static, and that is load-bearing.</b> xunit builds a fresh instance of a test class for
    /// every test method, so an instance field here would mean five <c>MSBuildWorkspace</c> loads
    /// — and the workspace load is the whole cost of this suite. One <c>Lazy</c> shared by the
    /// class buys every assertion in it.
    /// </remarks>
    private static readonly Lazy<SolutionModel> ReversedWalk = new(WalkReversed);

    private static SolutionModel Reversed => ReversedWalk.Value;

    [Fact]
    public void Types_csv_does_not_depend_on_declaration_order() =>
        AssertStable(CsvOutput.Types);

    [Fact]
    public void Edges_csv_does_not_depend_on_declaration_order() =>
        AssertStable(CsvOutput.Edges);

    [Fact]
    public void Members_csv_does_not_depend_on_declaration_order() =>
        AssertStable(CsvOutput.Members);

    /// <summary>
    /// The JSON export — the one that was not stable.
    /// </summary>
    /// <remarks>
    /// <c>generatedAt</c> is a clock reading, so both renders are stamped with the same instant;
    /// it is an argument to the renderer and not a product of the analysis.
    /// </remarks>
    [Fact]
    public void Json_does_not_depend_on_declaration_order()
    {
        var stamp = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        AssertStable(model => JsonOutput.Render(model, Analysis.Judge(model), stamp));
    }

    /// <summary>
    /// The one that matters most: the report is the interpretation layer, and it is what a reader
    /// actually sees.
    /// </summary>
    [Fact]
    public void The_report_does_not_depend_on_declaration_order() =>
        AssertStable(model => string.Join("\n", Report.For(model, Analysis.Judge(model))));

    /// <summary>
    /// The page, in both its shapes.
    /// </summary>
    /// <remarks>
    /// <b>Both, because they are different artifacts.</b> The default page shows one finding per
    /// kind, so an unstable ordering inside a section it does not enumerate would not appear in
    /// it at all; <c>--full</c> is where every list is rendered. <c>HtmlReportTests</c> keeps two
    /// snapshots for the same reason.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_page_does_not_depend_on_declaration_order(bool full)
    {
        var stamp = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        AssertStable(model => HtmlReport.Render(model, Analysis.Judge(model), stamp, full));
    }

    /// <summary>
    /// The project map — the artifact this test was built to catch, arriving four writers late.
    /// </summary>
    /// <remarks>
    /// <b>It groups and lays out by project, and project enumeration order is precisely what this
    /// file exists for.</b> The remark at the top records that reversing <c>TestBed.sln</c> moved
    /// 98.5% of <c>edges.csv</c>; a drawing whose every box is positioned from a project list is
    /// the same exposure with no rows to diff. <c>TESTING.md</c> §5 gives the standing instruction
    /// — <i>when you add a writer or a nomination list, give it a total key</i> — and four writers
    /// were added without any of them being added to the instrument that checks the instruction
    /// was followed.
    /// </remarks>
    [Fact]
    public void The_diagram_does_not_depend_on_declaration_order() =>
        AssertStable(ArchitectureDiagram.Render);

    /// <summary>Every type as one cell, grouped by project.</summary>
    [Fact]
    public void The_mosaic_does_not_depend_on_declaration_order() =>
        AssertStable(model => Mosaic.Render(model, Analysis.FindingsFor(model)));

    /// <summary>Projects by reach and density — the other one laid out from a project list.</summary>
    [Fact]
    public void The_plot_does_not_depend_on_declaration_order() =>
        AssertStable(model => ReachPlot.Render(model, Analysis.FindingsFor(model)));

    /// <summary>
    /// Renders both walks and requires the two to be byte-identical.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The solution's name is normalised out of the reversed render first. The two walks were
    /// handed different files on purpose, and a renderer that prints the path it was given is
    /// reporting an argument rather than an ordering — <c>JsonOutput</c> and the report header
    /// both do.
    /// </para>
    /// <para>
    /// <b>Normalised on the stem rather than the file name, and that is what adding the four
    /// graph artifacts found.</b> This replaced <c>TestBed.ordering-probe.sln</c> only, and the
    /// mosaic and the plot title themselves <c>TestBed</c> — no extension — so all four new
    /// assertions failed on a name in a caption while every ordering in them was already stable.
    /// The stem is the more general of the two and subsumes it: replacing
    /// <c>TestBed.ordering-probe</c> turns the bare title and the file name and any absolute path
    /// into the straight walk's form in one pass.
    /// </para>
    /// <para>
    /// It is worth being precise about what that was. It was a gap in this instrument, not a
    /// defect in a renderer — the drawings were stable the first time they were asked. What the
    /// four assertions cost was one normalisation; what they buy is that the two artifacts laid
    /// out from a project list are now watched by the test built for exactly that exposure.
    /// </para>
    /// </remarks>
    private void AssertStable(Func<SolutionModel, string> render)
    {
        var straight = render(core.Model);

        var reversed = render(Reversed).Replace(
            Path.GetFileNameWithoutExtension(Reversed.SolutionPath),
            Path.GetFileNameWithoutExtension(core.Model.SolutionPath),
            StringComparison.Ordinal);

        Assert.Equal(straight, reversed);

        // Guard against the assertion passing vacuously — an empty render would satisfy it.
        Assert.NotEmpty(straight);
    }

    /// <summary>
    /// Writes the reversed solution, walks it, and removes it again.
    /// </summary>
    /// <remarks>
    /// Written beside the real one rather than into a temp directory, because a <c>.sln</c>
    /// addresses its projects by relative path and a copy anywhere else names nothing. Deleted as
    /// soon as the walk returns — the model holds the path as a string and never reopens it — so
    /// the only window in which a stray file exists is one workspace load wide.
    /// </remarks>
    private static SolutionModel WalkReversed()
    {
        var path = Path.Combine(
            Path.GetDirectoryName(RepoPaths.TestBedSolution)!, "TestBed.ordering-probe.sln");

        File.WriteAllText(path, ReverseProjectDeclarations(File.ReadAllText(RepoPaths.TestBedSolution)));

        try
        {
            return new SolutionWalker(new WalkOptions
            {
                SolutionPath = path,
                Policy = AnalysisPolicy.Default,
            }).WalkAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The <c>Project(...) ... EndProject</c> blocks in the opposite order, everything else
    /// untouched.
    /// </summary>
    /// <remarks>
    /// A deliberately literal edit. Rewriting the file through a solution-file library would put a
    /// second implementation of the format in the test, and the point is to make the edit a
    /// maintainer might plausibly make by hand — moving a project up the list — not to prove
    /// anything about parsing.
    /// </remarks>
    private static string ReverseProjectDeclarations(string solution)
    {
        const string end = "EndProject\r\n";

        var first = solution.IndexOf("Project(\"", StringComparison.Ordinal);
        var last = solution.LastIndexOf(end, StringComparison.Ordinal) + end.Length;

        Assert.True(first > 0 && last > first, "TestBed.sln has no project declarations to reverse.");

        var blocks = solution[first..last]
            .Split(end, StringSplitOptions.RemoveEmptyEntries)
            .Select(block => block + end)
            .Reverse()
            .ToList();

        // Four of them. Fewer would let this whole file pass by having nothing to reorder, which
        // is the one way a test like this fails silently.
        Assert.Equal(4, blocks.Count);

        return solution[..first] + string.Concat(blocks) + solution[last..];
    }
}

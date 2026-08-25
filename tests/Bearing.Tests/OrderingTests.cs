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
/// <c>docs/DEFECTS.md</c> §37, fixed in the commit that brought this file across.
/// </para>
/// <para>
/// The control is unchanged: remove an <c>OrderBy</c> that a renderer or the builder relies on
/// and this fails while the snapshots stay green, because a snapshot only ever sees one order.
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
        AssertStable(model => string.Join("\n", Report.For(model, Analysis.FindingsFor(model))));

    /// <summary>
    /// Renders both walks and requires the two to be byte-identical.
    /// </summary>
    /// <remarks>
    /// The solution's file name is normalised out of the reversed render first. The two walks were
    /// handed different files on purpose, and a renderer that prints the path it was given is
    /// reporting an argument rather than an ordering — <c>JsonOutput</c> and the report header
    /// both do.
    /// </remarks>
    private void AssertStable(Func<SolutionModel, string> render)
    {
        var straight = render(core.Model);

        var reversed = render(Reversed).Replace(
            Path.GetFileName(Reversed.SolutionPath),
            Path.GetFileName(core.Model.SolutionPath),
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

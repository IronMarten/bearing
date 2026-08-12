using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// Every emitted artifact must be a function of the analysis, not of the order the analysis
/// happened to arrive in.
/// </summary>
/// <remarks>
/// <para>
/// The goldens reproduced perfectly long before this test existed — six separate processes,
/// byte-identical every time. That proved nothing. Every writer sorted on a non-total key, so
/// most rows were positioned by the enumeration order of a <c>Dictionary</c>, which is
/// insertion order, which is project load order times Roslyn's symbol order. 98.5% of
/// <c>edges.csv</c> and 82.4% of <c>types.csv</c> sat in a tie group. It reproduced because
/// nothing had perturbed it yet, and reversing the project declaration order in
/// <c>TestBed.sln</c> — an edit with no semantic content — was enough to move all of it.
/// </para>
/// <para>
/// This matters for phase 1 specifically. <c>Bearing.Core</c> is a reimplementation, not a
/// port, and it will not reproduce this probe's incidental insertion order however correct its
/// numbers are. Without a total order the oracle diff would have gone red on ordering the
/// moment Core computed anything, and a real regression would have been invisible underneath
/// the noise. The oracle only separates "I broke it" from "I changed it on purpose" if its
/// output is stable for reasons that survive being rewritten.
/// </para>
/// <para>
/// So the test is not "does it reproduce" — it is "would a correct implementation that
/// enumerated differently produce the same bytes". Shuffling the result and re-rendering
/// answers exactly that question, and it is the control: remove any <c>ThenBy</c> in
/// <c>Report.cs</c> and this fails while the goldens stay green.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class OrderingTests(FixtureRun run)
{
    [Fact]
    public void Types_csv_does_not_depend_on_enumeration_order() =>
        AssertStable(r => WriteAndRead(p => Report.WriteTypesCsv(p, r.Types)));

    [Fact]
    public void Edges_csv_does_not_depend_on_enumeration_order() =>
        AssertStable(r => WriteAndRead(p => Report.WriteEdgesCsv(p, r.Edges)));

    [Fact]
    public void Methods_csv_does_not_depend_on_enumeration_order() =>
        AssertStable(r => WriteAndRead(p => Report.WriteMethodsCsv(p, r.Methods)));

    [Fact]
    public void Prediction_sheet_does_not_depend_on_enumeration_order() =>
        AssertStable(r => WriteAndRead(p => Report.WritePredictionSheet(p, r.Types, run.Options)));

    /// <summary>
    /// The one that matters most: nominations is the interpretation layer, and it is the
    /// artifact phase 1 is rewriting.
    /// </summary>
    [Fact]
    public void Nominations_do_not_depend_on_enumeration_order() =>
        AssertStable(r =>
        {
            using var writer = new StringWriter();
            Report.PrintNominations(r, run.Options, writer);
            return writer.ToString();
        });

    /// <summary>
    /// Renders once from the real result and once from a shuffled view of it, and requires the
    /// two to be byte-identical. The shuffle permutes the collections only — every
    /// <see cref="TypeMetrics"/> instance is shared, so nothing is recomputed and any
    /// difference is attributable to ordering alone.
    /// </summary>
    private void AssertStable(Func<AnalysisResult, string> render)
    {
        var straight = render(run.Result);
        var shuffled = render(Shuffled(run.Result));

        Assert.Equal(straight, shuffled);

        // Guard against the assertion passing vacuously — an empty render would satisfy it.
        Assert.NotEmpty(straight);
    }

    /// <summary>
    /// A fixed-seed permutation. Seeded so a failure is reproducible: an ordering bug that
    /// only appeared on some runs would be worse than the one this replaces.
    /// </summary>
    private static AnalysisResult Shuffled(AnalysisResult source)
    {
        var rng = new Random(20260811);

        List<T> Permute<T>(List<T> items)
        {
            var copy = new List<T>(items);
            for (var i = copy.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }

        return new AnalysisResult
        {
            Types = Permute(source.Types),
            Methods = Permute(source.Methods),
            Edges = Permute(source.Edges),
            Projects = Permute(source.Projects),
            SkippedProjects = Permute(source.SkippedProjects),
            LoadWarnings = new List<string>(source.LoadWarnings),
            ExcludedTypes = source.ExcludedTypes,
            BaselineRows = source.BaselineRows,
        };
    }

    private static string WriteAndRead(Action<string> write)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bearing-ordering-{Guid.NewGuid():N}.csv");
        try
        {
            write(path);
            return File.ReadAllText(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

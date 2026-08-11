using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// The extraction gate.
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 moves computation out of <c>Report.cs</c> — 997 lines where the interpretation
/// and the formatting are the same statement. The rule while that happens is that the
/// probe's output must not move: byte-identical, or the extraction changed behaviour and
/// somebody has to say why out loud.
/// </para>
/// <para>
/// That rule already existed. It was a shell command in <c>oracle/README.md</c> that a human
/// had to remember to run. Roughly 32 defects were found during the probe build and several
/// were <i>reintroductions</i> of a failure already fixed elsewhere — caught the second and
/// third time only by manual vigilance, which is exactly what does not survive a
/// restructure. So it is a test.
/// </para>
/// <para>
/// These snapshots are the <b>frozen</b> regime (<c>docs/TESTING.md</c>). Accepting a change
/// here is not routine maintenance: it is a claim that the tool's behaviour changed on
/// purpose, and it belongs in a commit message that says so.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class OracleGoldenTests(FixtureRun run)
{
    [Fact]
    public Task Nominations_text_is_unchanged()
    {
        // The findings themselves: every sentence the probe is willing to say about the
        // fixture, with its receipts. This is the highest-value snapshot in the suite —
        // it covers the interpretation layer, which is the part being rewritten.
        using var writer = new StringWriter();
        Report.PrintNominations(run.Result, run.Options, writer);

        return Verify(writer.ToString(), extension: "txt")
            .UseDirectory("golden")
            .UseFileName("nominations");
    }

    [Fact]
    public Task Types_csv_is_unchanged()
    {
        // 41 columns per type. Broad rather than deep: it will not tell you what broke, but
        // nothing about a type's metrics can move without it noticing.
        var csv = WriteAndRead(path => Report.WriteTypesCsv(path, run.Result.Types));

        return Verify(csv, extension: "csv")
            .UseDirectory("golden")
            .UseFileName("types");
    }

    [Fact]
    public Task Edges_csv_is_unchanged()
    {
        // The dependency graph itself, which every Job A deliverable renders from.
        var csv = WriteAndRead(path => Report.WriteEdgesCsv(path, run.Result.Edges));

        return Verify(csv, extension: "csv")
            .UseDirectory("golden")
            .UseFileName("edges");
    }

    /// <summary>
    /// Runs a writer that only knows how to target a file, and returns what it wrote.
    /// </summary>
    /// <remarks>
    /// The CSV writers take a path, not a <see cref="TextWriter"/> — a small instance of the
    /// same entanglement phase 1 exists to undo. Bridging it here rather than editing the
    /// probe keeps the oracle verbatim, which is the only thing that makes it an oracle.
    /// </remarks>
    private static string WriteAndRead(Action<string> write)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bearing-oracle-{Guid.NewGuid():N}.csv");
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

using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// Reads the oracle's nomination text.
/// </summary>
/// <remarks>
/// <para>
/// This suite asserts against the model, never against report wording
/// (<c>docs/TESTING.md</c> §5). This helper exists for the two cases where there is no model
/// to assert against: a threshold that is a literal inside <c>PrintNominations</c>, and a
/// finding that emits nothing at all. Both are conditions worth pinning and neither is
/// reachable any other way — the absence of a model surface is the thing being reported.
/// </para>
/// <para>
/// Only subject names are read, never the sentence around them, so the wording stays free to
/// move without breaking these tests.
/// </para>
/// </remarks>
internal static class NominationText
{
    /// <summary>Renders the shared analysis under <paramref name="policy"/>.</summary>
    /// <remarks>
    /// Re-renders rather than re-analysing: cohorts are assigned during the run and do not
    /// move, and the workspace load is the suite's whole cost.
    /// </remarks>
    internal static string Render(AnalysisResult result, Options policy)
    {
        using var writer = new StringWriter();
        Report.PrintNominations(result, policy, writer);
        return writer.ToString();
    }

    /// <summary>
    /// The subjects nominated under <paramref name="header"/> — one name per component, with
    /// the section's own parenthetical and its <c>(none)</c> placeholder both dropped.
    /// </summary>
    /// <remarks>
    /// Valid for sections whose body is subjects. Sections that also print standing notes —
    /// HUBS AND GOD OBJECTS, NO PEER GROUP, BOUNDARY — would return those too, so do not use
    /// it on them without filtering.
    /// </remarks>
    internal static string[] SubjectsUnder(string text, string header) =>
        text.Split('\n')
            .SkipWhile(line => !line.StartsWith(header, StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.TrimStart().StartsWith("-- ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('('))
            .Select(line => line.Split(' ')[0])
            .ToArray();
}

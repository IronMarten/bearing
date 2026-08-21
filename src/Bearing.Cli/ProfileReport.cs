using System.Globalization;

namespace IronMarten.Bearing.Cli;

/// <summary>One measured stage of a run.</summary>
/// <param name="Name">What the stage is called. The first token on its line, so the table parses.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="Detail">What it had to do, or nothing.</param>
/// <param name="Nested">Whether it is part of the stage above rather than a peer of it.</param>
/// <remarks>
/// <b>A nested stage is inside its parent's time, not beside it.</b> Only the unnested rows carry
/// a share and only they are expected to sum to the total, which is why the flag is on the row
/// rather than left to the reader to infer from indentation.
/// </remarks>
public sealed record ProfileStage(string Name, TimeSpan Duration, string Detail = "", bool Nested = false);

/// <summary>
/// Where a run's time went.
/// </summary>
/// <remarks>
/// <para>
/// <b>A12, and the item it answers was a sentence rather than a bug:</b> <i>"32s for 3,209 types
/// is not obviously the walk rather than MSBuild, and nobody has looked."</i> Both reference
/// solutions are inside metric 4's sixty seconds and at parity with the probe, so this exists to
/// make a regression judgeable, not to fix a known problem — and the regression it is waiting for
/// is A9's, which changes <c>ReferenceCollector</c> and therefore exactly one row of this table.
/// </para>
/// <para>
/// <b>Printed to stderr, like every other note about the run.</b> The report is what a user pipes;
/// a profile arriving in the middle of it would be a defect of the same family as §25.
/// </para>
/// <para>
/// <b>The table is meant to be read by a person and parsed by <c>tools/measure.py</c>.</b> That is
/// why the stage name is the first token on its line and the duration the second: a median across
/// repeats is the only form of these numbers worth quoting, and a shape that needs a parser
/// rewritten each time it changes will not get one.
/// </para>
/// </remarks>
public static class ProfileReport
{
    /// <summary>The walk's own stages, nested where the profile nests them.</summary>
    /// <remarks>
    /// <b>The walk's residual is rendered even when it is zero</b>, for the reason
    /// <see cref="For"/> gives about the run's: a residual line that appears only when it is large
    /// is a residual line nobody trusts when it does appear. The run's own residual is not added
    /// here — this method reports the walk, and what surrounds the walk is the caller's to know.
    /// </remarks>
    public static IReadOnlyList<ProfileStage> StagesOf(WalkProfile walk)
    {
        ArgumentNullException.ThrowIfNull(walk);

        var stages = new List<ProfileStage>();

        foreach (var stage in WalkProfile.TopLevel)
        {
            stages.Add(new ProfileStage(Name(stage), walk[stage], DetailOf(stage, walk)));

            if (stage != WalkStage.Walk) continue;

            foreach (var inner in WalkProfile.WithinWalk)
                stages.Add(new ProfileStage(Name(inner), walk[inner], Nested: true));

            stages.Add(new ProfileStage("unmeasured", walk.WalkUnaccounted, Nested: true));
        }

        return stages;
    }

    /// <summary>Renders the table.</summary>
    /// <param name="measured">Every stage that was measured, in the order it happened.</param>
    /// <param name="total">Wall clock for the whole run, which the shares are taken against.</param>
    /// <remarks>
    /// <b>The residual is this method's own, and it is always printed.</b> Whatever the caller did
    /// not measure is the difference between the total and the stages it handed over, so the table
    /// reconciles by construction and an unmeasured stage becomes visible rather than absorbed
    /// into a neighbour. A caller cannot suppress it by forgetting a row, which is the only kind
    /// of residual worth having.
    /// </remarks>
    public static IEnumerable<string> For(IReadOnlyList<ProfileStage> measured, TimeSpan total)
    {
        ArgumentNullException.ThrowIfNull(measured);

        var stages = measured
            .Append(new ProfileStage(
                "unmeasured",
                total - measured.Where(s => !s.Nested).Aggregate(TimeSpan.Zero, (running, s) => running + s.Duration)))
            .ToList();

        yield return "";
        yield return "-- PROFILE";
        yield return "   Where the time went, measured rather than estimated. 'open' is MSBuild";
        yield return "   evaluating every project and 'compile' is Roslyn parsing and binding it;";
        yield return "   everything after those is this tool, asking Roslyn questions.";
        yield return "";

        var names = stages.Select(NameOf).ToList();
        var width = names.Max(n => n.Length);

        foreach (var (stage, name) in stages.Zip(names))
        {
            var share = stage.Nested ? "     " : Share(stage.Duration, total);
            var detail = string.IsNullOrEmpty(stage.Detail) ? "" : "   " + stage.Detail;

            yield return $"   {name.PadRight(width)}  {Seconds(stage.Duration),7}  {share}{detail}".TrimEnd();
        }

        yield return $"   {new string('-', width + 15)}";
        yield return $"   {"total".PadRight(width)}  {Seconds(total),7}";
        yield return "";
    }

    /// <summary>
    /// The stage's name, lower-cased, which is what a reader greps and what the parser keys on.
    /// </summary>
    private static string Name(WalkStage stage) => stage.ToString().ToLowerInvariant();

    /// <summary>The name as it appears in the table, indented if the stage is inside another.</summary>
    private static string NameOf(ProfileStage stage) => (stage.Nested ? "  " : "") + stage.Name;

    private static string DetailOf(WalkStage stage, WalkProfile walk) => stage switch
    {
        WalkStage.Open or WalkStage.Compile => Sentences.Plural(walk.Projects, "project"),
        WalkStage.Walk => $"{Sentences.Plural(walk.Types, "type")} in "
                          + Sentences.Plural(walk.Declarations, "declaration"),
        _ => "",
    };

    /// <summary>
    /// Seconds to two places, at every magnitude.
    /// </summary>
    /// <remarks>
    /// One unit throughout rather than milliseconds for the small rows: a column a reader has to
    /// check the unit of before comparing two entries is a column that will be compared without
    /// checking. Two places keeps a 40ms stage visible without giving an 18-second one a precision
    /// that repeats do not support.
    /// </remarks>
    private static string Seconds(TimeSpan duration) =>
        duration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";

    /// <summary>
    /// The stage's share of the run, and never a rounded-down zero for something that happened.
    /// </summary>
    private static string Share(TimeSpan duration, TimeSpan total)
    {
        if (total <= TimeSpan.Zero) return "     ";

        var percent = duration.TotalSeconds / total.TotalSeconds * 100;
        if (percent is > 0 and < 0.5) return "  <1%";

        return $"{Math.Round(percent),4:0}%";
    }
}

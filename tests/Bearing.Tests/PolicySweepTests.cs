using System.Globalization;
using System.Text;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The whole-policy nudge sweep, run by the suite rather than by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the hand-run version went stale twice and nothing said so.</b>
/// <c>docs/TESTING.md</c> §6 carried a table of constants the fixture cannot see, measured once
/// over 23 values while the policy had grown to 26 — and which three were missing was recorded
/// nowhere, because "23" counted what was nudged rather than the policy at the time. That is not
/// recoverable from history. It is recoverable by measuring again, which is what this does, over
/// whatever <see cref="AnalysisPolicy.Values"/> currently holds.
/// </para>
/// <para>
/// <b>The nudge asks whether the <i>constant</i> decides.</b> Move each value one notch each way
/// and compare the finding set. Its twin — leave-one-out, delete each <c>if (…) continue;</c> and
/// see whether the suite notices — asks whether the <i>condition</i> decides, which is a different
/// question that a gate can pass while failing this one. <c>MinKindSpan</c> is the case that shows
/// it: deleting the condition admits every type in the solution, so it looks observed, while
/// moving the floor from 3 to 2 changes nothing at all. Leave-one-out needs source edits and is
/// still run by hand.
/// </para>
/// <para>
/// <b>What counts as output moving.</b> The finding set as an ordered sequence of
/// (kind, subject, the qualifiers that hold). Receipts are excluded deliberately — most of them
/// quote the gate that produced them, so a nudge would move every receipt it touches and the
/// sweep would report every value as observable. Order is included, because a threshold that
/// reorders a section has changed what a reader sees first.
/// </para>
/// <para>
/// <b>The snapshot is the table.</b> §6 quotes it rather than restating it, so a value that
/// changes direction — dead becoming observable, or observable going quiet — is a diff to accept
/// rather than a claim that quietly stops being true. That is the whole of what went wrong with
/// the version this replaces.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class PolicySweepTests(CoreWalkFixture core)
{
    /// <summary>
    /// One notch, per value. Written out rather than derived from the value's magnitude, because
    /// the notch is a judgement about what a meaningful change to that gate looks like and a rule
    /// that guessed it would be wrong silently.
    /// </summary>
    /// <remarks>
    /// These are the increments the original hand sweep used, recovered from the rows it left in
    /// §6: <c>GodObjectMembers</c> is recorded as observable at ±4 and not ±1,
    /// <c>SurfaceOutlierMultiple</c> as moving at 1.6 and not 1.4, <c>MinTangle</c> as unmoved at
    /// 3 and 5, <c>SurfaceOutlierFloor</c> as unmoved at 0 and 2, and <c>Top</c> as 14 and 16.
    /// </remarks>
    private static readonly Dictionary<string, double> Notches = new(StringComparer.Ordinal)
    {
        // Counts and floors — the smallest change that is a different number of things.
        [nameof(AnalysisPolicy.MinCohort)] = 1,
        [nameof(AnalysisPolicy.MinFanIn)] = 1,
        [nameof(AnalysisPolicy.Top)] = 1,
        [nameof(AnalysisPolicy.HighCc)] = 1,
        [nameof(AnalysisPolicy.MinDecisionCc)] = 1,
        [nameof(AnalysisPolicy.ConcealedTopRank)] = 1,
        [nameof(AnalysisPolicy.HubMin)] = 1,
        [nameof(AnalysisPolicy.GodObjectMembers)] = 1,
        [nameof(AnalysisPolicy.MinKindSpan)] = 1,
        [nameof(AnalysisPolicy.BreaksAloneMinFanIn)] = 1,
        [nameof(AnalysisPolicy.RollCallDivisor)] = 1,
        [nameof(AnalysisPolicy.MaxNamedSurfaces)] = 1,
        [nameof(AnalysisPolicy.GlobalComplexityFloor)] = 1,
        [nameof(AnalysisPolicy.MinTangle)] = 1,
        [nameof(AnalysisPolicy.SurfaceOutlierFloor)] = 1,

        // Percentiles, in percentile points.
        [nameof(AnalysisPolicy.BlastComplexityPercentile)] = 1,
        [nameof(AnalysisPolicy.GlobalFanInPercentile)] = 1,
        [nameof(AnalysisPolicy.GlobalComplexityPercentile)] = 1,

        // Multiples and ratios.
        [nameof(AnalysisPolicy.OutlierFactor)] = 0.1,
        [nameof(AnalysisPolicy.StableThreshold)] = 0.1,
        [nameof(AnalysisPolicy.IsolatedThreshold)] = 0.1,
        [nameof(AnalysisPolicy.ConcealedFanInCeiling)] = 0.1,
        [nameof(AnalysisPolicy.ConcealedFanOutCeiling)] = 0.1,
        [nameof(AnalysisPolicy.BlastFanInMultiple)] = 0.1,
        [nameof(AnalysisPolicy.SurfaceOutlierMultiple)] = 0.1,

        // Shares of a population, where a notch of 0.1 would be a doubling.
        [nameof(AnalysisPolicy.BlastTopFraction)] = 0.01,
        [nameof(AnalysisPolicy.ChangeCostTopFraction)] = 0.01,
    };

    /// <summary>
    /// Every value carries a notch, or the sweep silently skips a gate.
    /// </summary>
    /// <remarks>
    /// The failure mode this closes is the one that produced the stale table: a value added to the
    /// policy after the sweep ran, covered by nothing, and reported by nothing as uncovered.
    /// <c>AnalysisPolicyTests</c> pins the count; this pins that the sweep reaches all of them.
    /// </remarks>
    [Fact]
    public void Every_policy_value_is_swept()
    {
        Assert.Equal(
            AnalysisPolicy.Default.Values.Select(v => v.Name).Order(StringComparer.Ordinal),
            Notches.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public Task The_whole_policy_swept_one_notch_each_way()
    {
        var baseline = Fingerprint(core.Model);
        var table = new StringBuilder();

        table.AppendLine("Each value moved one notch each way, against the finding set at defaults.");
        table.AppendLine("'moves' = the finding set differs.  'rejected' = the policy refuses the value.");
        table.AppendLine();
        table.AppendLine($"{"value",-28} {"default",9} {"notch",7}   {"down",-10} up");
        table.AppendLine(new string('-', 72));

        foreach (var (name, value) in AnalysisPolicy.Default.Values)
        {
            var notch = Notches[name];

            table.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} {1,9} {2,7}   {3,-10} {4}",
                name,
                Format(value),
                Format(notch),
                Outcome(name, value - notch, baseline),
                Outcome(name, value + notch, baseline)));
        }

        return Verify(table.ToString());
    }

    private string Outcome(string name, double to, string baseline)
    {
        AnalysisPolicy moved;

        try
        {
            moved = Set(AnalysisPolicy.Default, name, to);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A value the policy will not accept is not an unobserved gate — it is a gate with a
            // boundary, and saying so is more useful than reporting "no change".
            return "rejected";
        }

        return Fingerprint(core.WalkWith(moved)) == baseline ? "-" : "moves";
    }

    /// <summary>
    /// The finding set, as the sweep compares it: kind, subject, and the qualifiers that hold.
    /// </summary>
    private static string Fingerprint(SolutionModel model) =>
        string.Join(
            "\n",
            Analysis.FindingsFor(model).All.Select(f => string.Join(
                "|",
                f.Kind,
                f.Subject.Canonical,
                string.Join(",", f.Qualifiers.Where(q => q.Holds).Select(q => q.Name).Order(StringComparer.Ordinal)))));

    private static string Format(double value) =>
        value == Math.Floor(value)
            ? ((int)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Sets one value by name. A switch rather than reflection, because a record's positional
    /// setter is not reachable by name and a typo would read as an unswept gate.
    /// </summary>
    private static AnalysisPolicy Set(AnalysisPolicy policy, string name, double v) => name switch
    {
        nameof(AnalysisPolicy.MinCohort) => policy with { MinCohort = (int)v },
        nameof(AnalysisPolicy.OutlierFactor) => policy with { OutlierFactor = v },
        nameof(AnalysisPolicy.MinFanIn) => policy with { MinFanIn = (int)v },
        nameof(AnalysisPolicy.Top) => policy with { Top = (int)v },
        nameof(AnalysisPolicy.HighCc) => policy with { HighCc = (int)v },
        nameof(AnalysisPolicy.MinDecisionCc) => policy with { MinDecisionCc = (int)v },
        nameof(AnalysisPolicy.ConcealedTopRank) => policy with { ConcealedTopRank = (int)v },
        nameof(AnalysisPolicy.HubMin) => policy with { HubMin = (int)v },
        nameof(AnalysisPolicy.GodObjectMembers) => policy with { GodObjectMembers = (int)v },
        nameof(AnalysisPolicy.MinKindSpan) => policy with { MinKindSpan = (int)v },
        nameof(AnalysisPolicy.StableThreshold) => policy with { StableThreshold = v },
        nameof(AnalysisPolicy.IsolatedThreshold) => policy with { IsolatedThreshold = v },
        nameof(AnalysisPolicy.BreaksAloneMinFanIn) => policy with { BreaksAloneMinFanIn = (int)v },
        nameof(AnalysisPolicy.ConcealedFanInCeiling) => policy with { ConcealedFanInCeiling = v },
        nameof(AnalysisPolicy.ConcealedFanOutCeiling) => policy with { ConcealedFanOutCeiling = v },
        nameof(AnalysisPolicy.BlastFanInMultiple) => policy with { BlastFanInMultiple = v },
        nameof(AnalysisPolicy.BlastTopFraction) => policy with { BlastTopFraction = v },
        nameof(AnalysisPolicy.BlastComplexityPercentile) => policy with { BlastComplexityPercentile = v },
        nameof(AnalysisPolicy.ChangeCostTopFraction) => policy with { ChangeCostTopFraction = v },
        nameof(AnalysisPolicy.RollCallDivisor) => policy with { RollCallDivisor = (int)v },
        nameof(AnalysisPolicy.SurfaceOutlierMultiple) => policy with { SurfaceOutlierMultiple = v },
        nameof(AnalysisPolicy.SurfaceOutlierFloor) => policy with { SurfaceOutlierFloor = v },
        nameof(AnalysisPolicy.MaxNamedSurfaces) => policy with { MaxNamedSurfaces = (int)v },
        nameof(AnalysisPolicy.GlobalFanInPercentile) => policy with { GlobalFanInPercentile = v },
        nameof(AnalysisPolicy.GlobalComplexityPercentile) => policy with { GlobalComplexityPercentile = v },
        nameof(AnalysisPolicy.GlobalComplexityFloor) => policy with { GlobalComplexityFloor = (int)v },
        nameof(AnalysisPolicy.MinTangle) => policy with { MinTangle = (int)v },
        _ => throw new ArgumentException($"'{name}' is not a policy value the sweep can set.", nameof(name)),
    };
}

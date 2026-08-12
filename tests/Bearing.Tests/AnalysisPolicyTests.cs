using ArchProbe;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The threshold surface, and the claim that it is complete.
/// </summary>
/// <remarks>
/// Ten of these values were already flags. Thirteen were literals inside conditions in
/// <c>Report.cs</c> — not reviewable, and several of them not covered by any test, because the
/// findings they gate emitted nothing on the fixture until the plants landed. A policy carrying
/// ten of twenty-three misrepresents which policy produced a finding, so the tests that matter
/// here are the ones asserting nothing was left behind.
/// </remarks>
public sealed class AnalysisPolicyTests
{
    /// <summary>
    /// The thirteen that had no name, by their proposed names. Listed literally so that
    /// deleting one is a visible edit to this file rather than a silent shrinking of the
    /// policy surface.
    /// </summary>
    private static readonly string[] PreviouslyUnnamed =
    [
        "ConcealedFanInCeiling",
        "ConcealedFanOutCeiling",
        "BlastFanInMultiple",
        // Named as BlastFanInPercentile when the thirteen were catalogued; renamed when
        // docs/DEFECTS.md §14 was decided, because the gate it names is no longer a percentile.
        // The literal it replaced is still one of the thirteen.
        "BlastTopFraction",
        "BlastComplexityPercentile",
        "IsolatedThreshold",
        "BreaksAloneMinFanIn",
        "RollCallDivisor",
        "SurfaceOutlierMultiple",
        "SurfaceDiscriminationDivisor",
        "GlobalFanInPercentile",
        "GlobalComplexityPercentile",
        "GlobalComplexityFloor",
    ];

    [Fact]
    public void Every_gate_that_had_no_name_now_has_one()
    {
        var named = AnalysisPolicy.Default.Values.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(PreviouslyUnnamed, gate => Assert.Contains(gate, named));
    }

    [Fact]
    public void The_flags_that_already_existed_match_the_probe_exactly()
    {
        // Mechanical, and the point: if someone retunes a flag in one place and not the other,
        // the policy stops describing the run it claims to describe.
        var probe = new Options();
        var policy = AnalysisPolicy.Default;

        Assert.Equal(probe.MinCohort, policy.MinCohort);
        Assert.Equal(probe.OutlierFactor, policy.OutlierFactor);
        Assert.Equal(probe.MinFanIn, policy.MinFanIn);
        Assert.Equal(probe.StableThreshold, policy.StableThreshold);
        Assert.Equal(probe.HighCc, policy.HighCc);
        Assert.Equal(probe.MinDecisionCc, policy.MinDecisionCc);
        Assert.Equal(probe.HubMin, policy.HubMin);
        Assert.Equal(probe.GodObjectMembers, policy.GodObjectMembers);
        Assert.Equal(probe.MinKindSpan, policy.MinKindSpan);
        Assert.Equal(probe.MinTangle, policy.MinTangle);
        Assert.Equal(probe.Top, policy.Top);
    }

    [Fact]
    public void The_thirteen_carry_the_values_the_probe_gates_on()
    {
        // Transcribed from the conditions in Report.cs. Behavioural coverage of these gates
        // lives in SuppressionTests and the goldens; this pins the transcription itself, which
        // is the step where a hunt through 997 lines can quietly get one wrong.
        var p = AnalysisPolicy.Default;

        Assert.Equal(2.0, p.ConcealedFanInCeiling);              // FanInXMedian <= 2.0
        Assert.Equal(2.0, p.ConcealedFanOutCeiling);             // FanOutXMedian <= 2.0
        Assert.Equal(2.0, p.BlastFanInMultiple);                 // FanInXMedian >= 2.0
        Assert.Equal(0.05, p.BlastTopFraction);                  // was FanInPctl >= 95; §14
        Assert.Equal(70, p.BlastComplexityPercentile);           // CyclomaticPctl >= 70
        Assert.Equal(0.8, p.IsolatedThreshold);                  // Instability >= 0.8
        Assert.Equal(1, p.BreaksAloneMinFanIn);                  // FanIn >= 1
        Assert.Equal(3, p.RollCallDivisor);                      // members.Count > Top / 3
        Assert.Equal(1.5, p.SurfaceOutlierMultiple);             // DataShape >= median * 1.5
        Assert.Equal(1, p.SurfaceOutlierFloor);                  // ...floored at 1
        Assert.Equal(2, p.SurfaceDiscriminationDivisor);         // count <= boundaries / 2
        Assert.Equal(90, p.GlobalFanInPercentile);               // GlobalFanInPctl >= 90
        Assert.Equal(90, p.GlobalComplexityPercentile);          // GlobalMaxCcPctl >= 90
        Assert.Equal(1, p.GlobalComplexityFloor);                // MaxMemberCyclomatic > 1
    }

    [Fact]
    public void The_derived_gates_reproduce_the_probes_arithmetic()
    {
        // The two that are relations rather than constants. Reproduced here against the
        // formulas they replace, at the defaults and away from them.
        var p = AnalysisPolicy.Default;

        Assert.Equal(p.Top / 3, p.RollCallThreshold);
        Assert.Equal(5, p.RollCallThreshold);

        foreach (var boundaries in new[] { 0, 1, 2, 5, 10, 61 })
            Assert.Equal(Math.Max(1, boundaries / 2), p.MaxDiscriminatingSurfaces(boundaries));

        foreach (var median in new[] { 0.0, 0.5, 4.0, 12.0 })
            Assert.Equal(Math.Max(median * 1.5, 1), p.SurfaceOutlierThreshold(median));
    }

    [Fact]
    public void Stable_and_isolated_are_independent_knobs()
    {
        // The suspected coupling, decided. 0.2 and 0.8 look like 1-x mirrors and are not
        // derived from one another: they gate different findings over different populations,
        // and someone loosening what counts as stable has no reason to loosen what counts as
        // isolated. Deriving one from the other would make one flag move two findings — the
        // shape of the change-cost defect, where two values agree at defaults and only one is
        // reachable.
        var tuned = AnalysisPolicy.Default with { StableThreshold = 0.35 };

        Assert.Equal(0.35, tuned.StableThreshold);
        Assert.Equal(AnalysisPolicy.Default.IsolatedThreshold, tuned.IsolatedThreshold);
    }

    [Fact]
    public void A_tuned_policy_reports_what_was_tuned()
    {
        // "Which policy produced this finding" has to be answerable without reprinting
        // twenty-five numbers nobody changed.
        Assert.True(AnalysisPolicy.Default.IsDefault);
        Assert.Empty(AnalysisPolicy.Default.Overrides);

        var tuned = AnalysisPolicy.Default with { MinCohort = 3, HighCc = 12 };

        Assert.False(tuned.IsDefault);
        Assert.Equal(
            [("HighCc", 12.0, 10.0), ("MinCohort", 3.0, 5.0)],
            tuned.Overrides.OrderBy(o => o.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void Defaults_are_valid() => AnalysisPolicy.Default.Validate();

    [Theory]
    [MemberData(nameof(InvalidPolicies))]
    public void Arithmetic_that_could_not_have_been_meant_is_rejected(AnalysisPolicy policy) =>
        Assert.Throws<ArgumentOutOfRangeException>(policy.Validate);

    public static TheoryData<AnalysisPolicy> InvalidPolicies =>
    [
        AnalysisPolicy.Default with { BlastComplexityPercentile = 101 },
        // A share of a cohort, so above 1 is not a share. It is also the value that keeps blast
        // radius self-limiting, and 2 would quietly make it a roll-call.
        AnalysisPolicy.Default with { BlastTopFraction = 1.5 },
        AnalysisPolicy.Default with { GlobalComplexityPercentile = -1 },
        AnalysisPolicy.Default with { RollCallDivisor = 0 },
        AnalysisPolicy.Default with { SurfaceDiscriminationDivisor = 0 },
        AnalysisPolicy.Default with { MinCohort = 1 },
        AnalysisPolicy.Default with { OutlierFactor = double.NaN },
        AnalysisPolicy.Default with { StableThreshold = double.PositiveInfinity },
    ];

    [Fact]
    public void An_unusual_but_meaningful_threshold_is_the_users_call()
    {
        // Validation guards the arithmetic, not the judgement. A very high complexity bar or a
        // very large cohort floor is a choice someone may want; refusing it would be the tool
        // overriding a decision it has no standing to make.
        var severe = AnalysisPolicy.Default with { HighCc = 500, MinCohort = 200, OutlierFactor = 50 };

        severe.Validate();
    }
}

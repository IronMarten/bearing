namespace IronMarten.Bearing;

/// <summary>
/// What the run could not compare. <c>TECHREQ-job-b.md</c> §3.11, invariant 8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Part of the output, not a footnote.</b> Every other finding here is a claim; this one is the
/// disclosure that makes the others readable. A finding is only as good as the population it was
/// computed over, and a reader who cannot see what was excluded has no way to know whether silence
/// about a component means it is fine or means nobody looked. Invariant 8: silence must never read
/// as a clean bill of health.
/// </para>
/// <para>
/// <b>The weaker claim is the point of the section.</b> A type with no peers can still be extreme
/// against the whole solution, and going quiet about it is not an option — a lone
/// <c>DbContext</c> or a pair of repositories are often the most central things in a system. So
/// where a below-floor type is at the top of the solution by fan-in or by complexity, that is said
/// with the comparison named: it is weaker evidence, because it compares unlike things, and the
/// wording has to carry that rather than borrow the confidence of a peer-relative claim.
/// <c>SESSION-NOTES.md</c> #7.
/// </para>
/// <para>
/// <b>The complexity claim has a floor beside its percentile, and #8 is why.</b> In a codebase
/// where most types have no branching at all, a max-member complexity of 1 lands at a high midrank
/// percentile — <i>"top 86% by complexity, cc 1"</i> is both absurd and corrosive. The floor is
/// what stops the percentile speaking on its own.
/// </para>
/// <para>
/// <b>Solution-wide distributions, computed here rather than carried on the model.</b> The two
/// percentiles exist for this finding and for nothing else so far, and a model field that one
/// detector reads is a field every renderer has to be told to ignore.
/// </para>
/// </remarks>
public static class NoPeerGroup
{
    /// <summary>Every type that had no viable peer group, and what could still be said about it.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var fanIn = Distribution.Of(model.Types.Select(type => (double)type.FanIn));
        var complexity = Distribution.Of(model.Types.Select(type => (double)type.MaxMemberCyclomatic));

        var found = new List<(bool Extreme, int FanIn, Finding Finding)>();

        foreach (var type in model.Types)
        {
            if (type.CohortSize >= policy.MinCohort) continue;

            var inbound = fanIn.Read(type.FanIn);
            var worst = complexity.Read(type.MaxMemberCyclomatic);

            var extremeFanIn = inbound is { } i && i.Percentile >= policy.GlobalFanInPercentile;

            // The percentile and the floor together, and the floor is applied exclusively:
            // complexity must be strictly greater than it. SESSION-NOTES.md #8.
            var extremeComplexity =
                worst is { } w &&
                w.Percentile >= policy.GlobalComplexityPercentile &&
                type.MaxMemberCyclomatic > policy.GlobalComplexityFloor;

            found.Add((extremeFanIn || extremeComplexity, type.FanIn, new Finding(
                new FindingKey(FindingKind.Coverage, type.Subject),
                [
                    Receipt.Gated("CohortSize", type.CohortSize, nameof(AnalysisPolicy.MinCohort)),
                    Receipt.Of("GlobalFanInPctl", inbound?.Percentile ?? double.NaN),
                    Receipt.Of("GlobalMaxCcPctl", worst?.Percentile ?? double.NaN),
                    Receipt.Of("FanIn", type.FanIn),
                    Receipt.Of("MaxMemberCyclomatic", type.MaxMemberCyclomatic),
                    // Carried so a reader can see why a type high on the percentile still holds no
                    // complexity claim. It is the other half of that qualifier's condition, and a
                    // qualifier names one gate.
                    Receipt.Of("GlobalComplexityFloor", policy.GlobalComplexityFloor),
                    Receipt.Of("SolutionTypeCount", model.Types.Count),
                ],
                [
                    new Qualifier(
                        Qualifiers.GloballyExtremeFanIn,
                        extremeFanIn,
                        nameof(AnalysisPolicy.GlobalFanInPercentile)),
                    new Qualifier(
                        Qualifiers.GloballyExtremeComplexity,
                        extremeComplexity,
                        nameof(AnalysisPolicy.GlobalComplexityPercentile)),
                ],
                type.MostComplexMember is { } member ? [member.Subject] : [])));
        }

        // The ones something can still be said about come first; the rest are the roll-call this
        // section is deliberately allowed to be, because completeness is the whole claim.
        return Nomination.Ranked(
            found.OrderByDescending(f => f.Extreme).ThenByDescending(f => f.FanIn),
            f => f.Finding);
    }
}

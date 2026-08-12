namespace IronMarten.Bearing;

/// <summary>
/// <i>"Complex inside but isolated — if it breaks, it breaks alone."</i>
/// <c>TECHREQ-job-b.md</c> §3.7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reassuring message, and therefore the one with the most suppressions.</b> Three of the
/// matrix's seven rows are this finding's, and none of them is optional: every other finding in
/// §3 is wrong by overstating a risk, which costs the reader time. This one is wrong by
/// understating it, which costs them the incident. A false "safe to change" is the only output
/// the tool can produce that is worse than no output.
/// </para>
/// <para>
/// <b>None of the three is in this detector.</b> They are rows in <see cref="Suppression"/>,
/// evaluated over the whole set after every detector has run — so this fires on boundaries and on
/// unreferenced types and lets the pass take them away. That is the point: in the probe the
/// exclusions are <c>Where</c> clauses reading a variable assigned earlier in the same method,
/// which makes renderer ordering load-bearing for invariant 3, and a suppression that stops
/// working produces <i>more</i> output — which reads as a working tool.
/// </para>
/// <para>
/// Cohort-free, like load-bearing and for the same reason: instability is a ratio and is defined
/// without peers.
/// </para>
/// </remarks>
public static class BreaksAlone
{
    /// <summary>Nominates complex types that almost nothing depends on.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(int Complexity, Finding Finding)>();

        foreach (var type in model.Types)
        {
            if (type.Instability is not { } instability) continue;
            if (instability < policy.IsolatedThreshold) continue;
            if (type.MaxMemberCyclomatic < policy.HighCc) continue;

            found.Add((type.MaxMemberCyclomatic, new Finding(
                new FindingKey(FindingKind.BreaksAlone, type.Subject),
                [
                    Receipt.Gated("Instability", instability, nameof(AnalysisPolicy.IsolatedThreshold)),
                    Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.HighCc)),
                    // Not a gate here — the rule that reads it is a suppression row, and a
                    // receipt naming a gate this detector did not apply would misstate what
                    // decided the finding. It travels because the sentence says "only N types
                    // depend on it".
                    Receipt.Of("FanIn", type.FanIn),
                    Receipt.Of("FanOut", type.FanOut),
                    Receipt.Of("EffectiveFanOut", type.EffectiveFanOut),
                ],
                [],
                type.MostComplexMember is { } member ? [member.Subject] : [])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Complexity), f => f.Finding);
    }
}

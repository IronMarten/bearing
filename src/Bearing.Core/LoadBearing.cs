namespace IronMarten.Bearing;

/// <summary>
/// <i>"Much depends on it, it depends on little, and it is intricate enough to hide a bug."</i>
/// <c>TECHREQ-job-b.md</c> §3.6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cohort-free, deliberately.</b> Instability is a ratio, so it is defined for a type with no
/// peer group at all — and a singleton that everything depends on is exactly the type this
/// finding exists to catch. It is the one nomination in §3 that says something about a component
/// the coverage section would otherwise have to apologise for.
/// </para>
/// <para>
/// <b>Instability is computed over effective fan-out</b> — abstractions and in-solution data
/// contracts excluded — because depending on an abstraction is the mechanism dependency
/// inversion uses to <i>reduce</i> exposure to change, so counting it as coupling penalises the
/// practice that exists to avoid the risk. Verified on a controlled pair: two identical classes,
/// one on four concrete services and one on four interfaces, raw instability 0.8 for both,
/// effective 0.8 and 0. The exclusion does 100% of the discriminating. <c>SESSION-NOTES.md</c>
/// #22.
/// </para>
/// <para>
/// <b>The consequence has to reach the output, not just the code.</b> On a DIP-heavy codebase low
/// instability becomes common, and there it means <i>insulated</i> rather than
/// <i>load-bearing</i>. Only fan-in magnitude separates the two, which is why the floor below is
/// a condition rather than a decoration.
/// </para>
/// </remarks>
public static class LoadBearing
{
    /// <summary>Nominates stable, heavily-depended-on types with real logic inside.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var found = new List<(double Instability, int FanIn, Finding Finding)>();

        foreach (var type in model.Types)
        {
            // Undefined rather than zero: a type nothing depends on and which depends on nothing
            // has no ratio, and the probe's NaN is this null. Reading it as 0 would make every
            // unconnected type maximally stable — the most load-bearing thing in the solution.
            if (type.Instability is not { } instability) continue;
            if (instability > policy.StableThreshold) continue;

            // The ratio hides magnitude. I = 0 with one caller scores identically to I = 0 with
            // five hundred, and only one of those is load-bearing. SESSION-NOTES.md #11.
            if (type.FanIn < policy.MinFanIn) continue;

            if (type.MaxMemberCyclomatic < policy.HighCc) continue;

            found.Add((instability, type.FanIn, new Finding(
                new FindingKey(FindingKind.LoadBearingAndIntricate, type.Subject),
                [
                    Receipt.Gated("Instability", instability, nameof(AnalysisPolicy.StableThreshold)),
                    Receipt.Gated("FanIn", type.FanIn, nameof(AnalysisPolicy.MinFanIn)),
                    Receipt.Gated("MaxMemberCyclomatic", type.MaxMemberCyclomatic, nameof(AnalysisPolicy.HighCc)),
                    // Both fan-outs, because the sentence distinguishes "depends on nothing" from
                    // "depends on nothing concrete" and a renderer cannot tell them apart from
                    // the effective count alone.
                    Receipt.Of("EffectiveFanOut", type.EffectiveFanOut),
                    Receipt.Of("FanOut", type.FanOut),
                    Receipt.Of("InstabilityRaw", type.InstabilityRaw ?? double.NaN),
                    Receipt.Of("Cyclomatic", type.Cyclomatic),
                ],
                [],
                // Invariant 7: "intricate enough to hide a bug" is arguable until the method is
                // named, and then it is checkable.
                type.MostComplexMember is { } member ? [member.Subject] : [])));
        }

        // Most stable first, then most depended on — the probe's order, and the one that puts the
        // hardest-to-change type at the top rather than the most complex one.
        return Nomination.Ranked(
            found.OrderBy(f => f.Instability).ThenByDescending(f => f.FanIn),
            f => f.Finding);
    }
}

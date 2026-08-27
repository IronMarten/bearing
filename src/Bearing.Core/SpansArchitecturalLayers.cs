namespace IronMarten.Bearing;

/// <summary>
/// <i>"A thing named for one narrow concern that reaches across several."</i>
/// <c>TECHREQ-job-b.md</c> §3.1 — the headline finding.
/// </summary>
/// <remarks>
/// <para>
/// The finding that produced the validated result: a component named authentication middleware
/// that also did key validation, customer lookup, tenant routing and audit. The tool cannot know
/// what a component is <i>for</i>. It can see that its dependencies do not match a single concern,
/// and name them so that a human can judge.
/// </para>
/// <para>
/// <b>Three kinds are significant.</b> <c>Contract</c> is excluded because nearly everything
/// touches DTOs, and counting it would put every type in the report; <c>Internal</c> because it is
/// the catch-all, and depending on ordinary code is not cross-cutting. <b>A type's own kind counts
/// alongside its dependencies'</b> — a boundary component that also does data access spans layers
/// even if that is its only significant dependency.
/// </para>
/// <para>
/// <b>The named dependencies per kind are the finding, not the count.</b> Invariant 7, stated by
/// §3.1 in as many words: <i>"spans 3 architectural kinds"</i> is arguable, and <i>"why is
/// authentication calling TenantStore?"</i> is not. They are carried as participants, uncapped —
/// §3.1's six-names-per-kind is a display cap and belongs with <see cref="AnalysisPolicy.Top"/> in
/// the renderer.
/// </para>
/// <para>
/// <b>Two subjects are instances of one pattern when they reach the same kinds through the same
/// dependencies</b>, which is the second deliberate divergence from
/// the oracle. The probe groups on the kind signature alone, and that assumes a shared signature
/// means a shared phenomenon. It does not: four boilerplate controllers wired to a store and a
/// gateway carry the identical signature to a middleware reaching into customer lookup and an
/// audit service, and the collapse absorbs the anomaly — losing exactly the detail block that made
/// it actionable. The repair falls out of §3.1's own sentence. If the names are the finding, the
/// names are what makes two findings the same finding; grouping on the count discards the thing
/// the section says is the point. Under the probe's rule all six fixture types are one pattern.
/// Under this one the four controllers are, and the middleware and the bridge are anomalies with
/// their detail intact.
/// </para>
/// <para>
/// <b>The collapse is a qualifier, not a suppression</b>, for the same reason row 6 is: it
/// silences detail rather than the claim. The probe keeps every collapsed type named in its
/// examples line, which is the proof that the finding itself is not withdrawn.
/// </para>
/// <para>
/// Cohort-free. Reaching across layers means the same thing with or without peers.
/// </para>
/// </remarks>
public static class SpansArchitecturalLayers
{
    /// <summary>
    /// The architecturally significant kinds.
    /// </summary>
    /// <remarks>
    /// Three of them, and <c>TASKS.md</c> X4 is open on whether that is enough: with three kinds
    /// and a <see cref="AnalysisPolicy.MinKindSpan"/> of three, "spans the minimum" and "spans
    /// everything" are the same condition, so the gate cannot discriminate at any solution size and
    /// every spanning type necessarily carries one signature. Not a policy value while that is
    /// undecided — §5 names twenty-three values and this is not among them, and adding a knob to a
    /// list whose membership is the open question would settle it by accident.
    /// </remarks>
    private static readonly string[] SignificantKinds = [TypeKinds.ApiBoundary, TypeKinds.DataAccess, TypeKinds.ExternalCall];

    /// <summary>Nominates components whose reach crosses architectural layers.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var policy = model.Policy;
        var spanning = new List<Candidate>();

        foreach (var type in model.Types)
        {
            var kinds = new SortedSet<string>(StringComparer.Ordinal);
            var reached = new List<(string Kind, SubjectRef Subject)>();

            // Its own position counts. A boundary that also does data access is spanning even
            // when that is its only significant dependency.
            var ownKind = IsSignificant(type) ? type.Classification.Kind : null;
            if (ownKind is not null) kinds.Add(ownKind);

            foreach (var outbound in type.Outbound)
            {
                if (model.Find(outbound) is not { } dependency) continue;
                if (!IsSignificant(dependency)) continue;

                kinds.Add(dependency.Classification.Kind);
                reached.Add((dependency.Classification.Kind, dependency.Subject));
            }

            if (kinds.Count < policy.MinKindSpan) continue;

            // Identity, never the simple name: two types may share a name across assemblies, and
            // a pattern key built from names would merge components that have nothing to do with
            // each other.
            var participants = reached
                .DistinctBy(r => r.Subject.Canonical, StringComparer.Ordinal)
                .OrderBy(r => r.Kind, StringComparer.Ordinal)
                .ThenBy(r => r.Subject.Canonical, StringComparer.Ordinal)
                .ToList();

            spanning.Add(new Candidate(
                type,
                kinds.Count,
                reached.Select(r => r.Kind).Distinct(StringComparer.Ordinal).Count(),
                [.. participants.Select(r => r.Subject)],
                PatternKey(ownKind, participants)));
        }

        // The group is what the qualifier reads, so it is computed over the whole spanning set
        // before any finding is built. A detector may not depend on having run after another one;
        // depending on its own earlier iterations is the same hazard in miniature.
        var patterns = spanning
            .GroupBy(candidate => candidate.PatternKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var found = spanning.Select(candidate =>
        {
            var groupSize = patterns[candidate.PatternKey];

            return (GroupSize: groupSize, candidate.Type.FanIn, Finding: new Finding(
                new FindingKey(FindingKind.SpansArchitecturalLayers, candidate.Type.Subject),
                [
                    Receipt.Gated("KindSpan", candidate.KindSpan, nameof(AnalysisPolicy.MinKindSpan)),
                    // The span reached without the type's own role counted. Where this is one
                    // below KindSpan, the component's own position is what makes it cross-cutting
                    // — which is a different sentence from reaching three kinds through
                    // dependencies alone, and the only way a renderer can tell them apart.
                    Receipt.Of("KindsThroughDependencies", candidate.KindsThroughDependencies),
                    Receipt.Of("PatternGroupSize", groupSize),
                    Receipt.Of("FanIn", candidate.Type.FanIn),
                ],
                // No qualifier. `part-of-a-layering-pattern` lived here until 2026-08-26 and was
                // removed with D54: it asked whether groupSize exceeded `Top / RollCallDivisor`,
                // which is a judgement scaled by a display cap. Measuring it to pick a better
                // threshold found there is no threshold to pick — every group is size 1 on all
                // three reference solutions, under identity keying and under every relaxation of
                // it, so the qualifier was False on all nine real findings and its collapse line
                // had never printed. The PatternGroupSize receipt stays: it is evidence a reader
                // can act on, and the ordering below still reads it.
                [],
                candidate.Participants));
        });

        // Rarer patterns first — §3.1's discipline, in the model rather than in the renderer.
        // Within one pattern the members are equivalent by construction, so fan-in and then
        // identity are a tiebreak between things the finding does not distinguish, rather than
        // the inverse-of-interestingness ordering recorded against the probe.
        return Nomination.Ranked(
            found.OrderBy(f => f.GroupSize).ThenByDescending(f => f.FanIn),
            f => f.Finding);
    }

    private static bool IsSignificant(TypeNode type) =>
        SignificantKinds.Contains(type.Classification.Kind, StringComparer.Ordinal);

    /// <summary>
    /// What makes two spanning components the same finding: the role they occupy, and the
    /// specific components they reach.
    /// </summary>
    private static string PatternKey(string? ownKind, IEnumerable<(string Kind, SubjectRef Subject)> participants) =>
        string.Join(
            "|",
            new[] { ownKind ?? "-" }.Concat(
                participants.Select(r => string.Concat(r.Kind, ":", r.Subject.Canonical))));

    private readonly record struct Candidate(
        TypeNode Type,
        int KindSpan,
        int KindsThroughDependencies,
        IReadOnlyList<SubjectRef> Participants,
        string PatternKey);
}

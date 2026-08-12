namespace IronMarten.Bearing.Cli;

/// <summary>
/// What got no comparative reading at all, and what could still be said about it.
/// </summary>
/// <remarks>
/// Invariant 8. A tool that quietly drops the types it could not judge reports a clean bill of
/// health for a population it never examined, and the reader has no way to tell the difference.
/// </remarks>
internal static class CoverageSection
{
    internal static IEnumerable<string> NoPeerGroup(SolutionModel model, FindingSet findings)
    {
        var coverage = findings.OfKind(FindingKind.Coverage);
        var total = model.Types.Count;
        var share = total == 0 ? 0 : 100.0 * coverage.Count / total;

        yield return "";
        yield return "-- NO PEER GROUP -----------------------------------------------";
        yield return $"   {coverage.Count} of {total} types ({share:0.#}%) "
                     + $"sit in cohorts below --min-cohort ({model.Policy.MinCohort}).";

        foreach (var line in WhatThatDoesAndDoesNotMean(findings, coverage)) yield return line;

        if (coverage.Count == 0) yield break;

        // A weaker claim, but a real one: no peers to compare against, so compare against the
        // whole solution and say plainly that that is what happened. A lone DbContext is often
        // among the most central things in a system, and going silent on it is not an option.
        var globallyExtreme = coverage
            .Where(f => f.Holds(Qualifiers.GloballyExtremeFanIn) || f.Holds(Qualifiers.GloballyExtremeComplexity))
            .ToList();

        if (globallyExtreme.Count > 0)
        {
            var (shown, disclosure) = Sentences.Cap(globallyExtreme, model.Policy.Top, "type", "     ");

            yield return "";
            yield return "   Extreme against the WHOLE SOLUTION despite having no peer group.";
            yield return "   Weaker evidence — this compares unlike things — but not nothing:";

            foreach (var finding in shown)
            {
                if (model.Find(finding.Subject) is not { } type) continue;

                yield return $"     {type.Name} [{type.Classification.Kind}] — "
                             + $"{string.Join(" and ", Claims(finding, type))}, "
                             + "solution-wide. (no cohort to compare against)";
            }

            foreach (var line in disclosure) yield return line;
        }

        var byFanIn = coverage
            .Select(f => model.Find(f.Subject))
            .Where(t => t is not null)
            .OrderByDescending(t => t!.FanIn)
            .ThenBy(t => t!.Subject.Canonical, StringComparer.Ordinal)
            .ToList();

        var (listed, listDisclosure) = Sentences.Cap(byFanIn, model.Policy.Top, "type", "     ");

        yield return "";
        yield return "   All types with no usable peer group, by fan-in:";

        foreach (var type in listed)
            yield return $"     {type!.Name} — fan-in {type.FanIn}, cc {type.Cyclomatic}, "
                         + $"cohort '{ShortCohort(type.Cohort.Key)}' (size {type.CohortSize})";

        foreach (var line in listDisclosure) yield return line;

        yield return "";
        yield return "   NOTE: a type with no peers still has its own METHODS as a cohort.";
        yield return "   And its real comparison is its own history, which is the temporal";
        yield return "   signal a single snapshot cannot give you.";
    }

    /// <summary>
    /// The sentence that used to be wrong.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/DEFECTS.md</c> §17.</b> The probe states that these types "are absent from the
    /// nominations above", and three of them are not: the cohort-free findings do not consult a
    /// cohort, so a peerless type is eligible for every one of them. No nomination was wrong —
    /// the sentence describing them was, and it was wrong in the direction that tells a reader to
    /// stop looking.
    /// <para>
    /// The repair is to ask rather than assert. The finding set knows exactly which of these
    /// subjects carry other claims, so the renderer reports the answer instead of predicting it.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> WhatThatDoesAndDoesNotMean(
        FindingSet findings, IReadOnlyList<Finding> coverage)
    {
        yield return "   No PEER comparison was possible for these, so their percentile and";
        yield return "   multiple-of-median readings are blank rather than zero.";

        var alsoNominated = coverage
            .Select(f => findings.About(f.Subject).Count(other => other.Kind != FindingKind.Coverage))
            .Count(claims => claims > 0);

        if (alsoNominated == 0)
        {
            yield return "   None of them appears in the nominations above.";
            yield break;
        }

        yield return $"   {alsoNominated} of them {(alsoNominated == 1 ? "does" : "do")} still appear "
                     + "in the nominations above:";
        yield return "   the findings that need no cohort judge a peerless type like any other.";
    }

    /// <summary>
    /// Only the dimension that actually qualifies is stated.
    /// </summary>
    /// <remarks>
    /// In a codebase where most types have no callers, a fan-in of zero lands at a high midrank
    /// percentile — <i>"top 86% by fan-in, 0 callers"</i> is both absurd and corrosive. Core
    /// decides which dimension survives that check and carries it as a qualifier; this only picks
    /// the words for the ones that did.
    /// </remarks>
    private static IEnumerable<string> Claims(Finding finding, TypeNode type)
    {
        if (finding.Holds(Qualifiers.GloballyExtremeFanIn))
        {
            yield return $"top {Sentences.TopPercent(finding.ValueOf("GlobalFanInPctl") ?? 0)} by fan-in "
                         + $"({Sentences.Plural(type.FanIn, "caller")})";
        }

        if (finding.Holds(Qualifiers.GloballyExtremeComplexity))
        {
            yield return $"top {Sentences.TopPercent(finding.ValueOf("GlobalMaxCcPctl") ?? 0)} by complexity "
                         + $"(cc {type.MaxMemberCyclomatic} in {type.MostComplexMember?.Name})";
        }
    }

    private static string ShortCohort(string cohort)
    {
        var afterPrefix = cohort.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0
            ? cohort[(colon + 1)..]
            : cohort;

        var lastDot = afterPrefix.LastIndexOf('.');
        return lastDot >= 0 ? afterPrefix[(lastDot + 1)..] : afterPrefix;
    }
}

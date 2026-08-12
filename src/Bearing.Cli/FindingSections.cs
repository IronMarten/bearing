namespace IronMarten.Bearing.Cli;

/// <summary>
/// Job B's sections: the claims, worded.
/// </summary>
/// <remarks>
/// <para>
/// Every sentence here is built from what the finding already carries. Nothing re-derives a
/// number from the model, because a renderer that recomputes can disagree with the claim it is
/// printing — and nothing decides whether a claim may be made, because suppression settled that
/// before the set arrived.
/// </para>
/// <para>
/// <b>Where this deliberately differs from the probe</b>: defect 16 (the god-object sentence is
/// chosen from the qualifier that actually holds), defect 17 (the coverage section asks the
/// finding set instead of asserting an absence), defect 11's layer-span wording, and defect 3
/// (every capped list says what it dropped). Everything else is the probe's voice, on purpose —
/// so that a reader comparing the two sees one tool, and every difference is one that was chosen.
/// </para>
/// </remarks>
internal static class FindingSections
{
    internal static IEnumerable<string> ConcealedDecisionAtTypeLevel(
        SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CONCEALED DECISION ------------------------------------------";
        yield return "   (complexity far above peers, connectivity ordinary)";

        var found = findings.OfKind(FindingKind.ConcealedDecisionType);
        if (found.Count == 0)
            yield return "   (none nominated — see NOTES in README if this is empty)";

        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            var member = type.MostComplexMember;
            var times = finding.ValueOf("MaxMemberCyclomaticXMedian") ?? 0;
            var percentile = finding.ValueOf("MaxMemberCyclomaticPctl") ?? 0;

            // "Looks like plumbing" only holds when connectivity is low in ABSOLUTE terms. The
            // gate is relative, so in a cohort where every member is heavily used "ordinary for
            // its peers" still means widely depended on. Core decides which is true; this picks
            // the words.
            var opening = finding.Holds(Qualifiers.LowAbsoluteConnectivity)
                ? "looks like plumbing but is in the top "
                : "connectivity is unremarkable for its peers, but it is in the top ";

            var basis = double.IsInfinity(times)
                ? "(its peers all measure 0; cc "
                : $"({Sentences.Number(times)}x the peer median; cc ";

            yield return $"   {type.Name}.{member?.Name} — {opening}"
                         + $"{Sentences.TopPercent(percentile)} of internal complexity among your "
                         + $"{type.CohortSize} {ShortCohort(type.Cohort.Key)}. "
                         + basis
                         + $"{type.MaxMemberCyclomatic}, dsm {type.Dsm}, "
                         + $"fan-in {type.FanIn}, fan-out {type.FanOut})";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> ConcealedDecisionAtMethodLevel(
        SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CONCEALED DECISION, METHOD LEVEL ----------------------------";

        var found = findings.OfKind(FindingKind.ConcealedDecisionMethod);
        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            var (type, member) = Member(model, finding.Subject);
            if (type is null || member is null) continue;

            var times = finding.ValueOf("CyclomaticXMedian") ?? 0;
            var peers = finding.ValueOf("CohortSize") ?? 0;

            var basis = double.IsInfinity(times)
                ? "the only complexity among its "
                : $"{Sentences.Number(times)}x the median complexity of its ";

            yield return $"   {type.Name}.{member.Name} — {basis}"
                         + $"{Sentences.Whole(peers)} peers "
                         + $"(cc {member.Cyclomatic}, dsm {member.Dsm}, "
                         + $"nesting {member.MaxNestingDepth}, {member.LinesOfCode} lines) — "
                         + $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> BlastRadius(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- BUG BLAST RADIUS --------------------------------------------";
        yield return "   (widely depended on AND internally complex)";

        var (shown, disclosure) = Sentences.Cap(
            findings.OfKind(FindingKind.BugBlastRadius), model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} — {Sentences.Plural(type.FanIn, "distinct caller")} "
                         + $"({Sentences.Number(finding.ValueOf("FanInXMedian") ?? 0)}x its peer median) and "
                         + "internally complex. A bug here propagates widely. "
                         + $"(cc {type.Cyclomatic}, fan-out {type.FanOut}, "
                         + $"{Sentences.Plural(type.InboundReferenceCount, "call site")})";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> ChangeCost(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CHANGE COST -------------------------------------------------";
        yield return "   (many internal callers on a contract-shaped type)";

        var (shown, disclosure) = Sentences.Cap(
            findings.OfKind(FindingKind.ChangeCost), model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} — {Sentences.Plural(type.FanIn, "internal caller")} "
                         + $"depend on this contract ({type.DataShape} fields/params of surface). "
                         + "Changing it is a distributed edit, not a local one. "
                         + "EXTERNAL consumers are not visible to this analysis. "
                         + $"({type.Classification.Kind})";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> LoadBearing(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- LOAD-BEARING AND INTRICATE (no cohort required) -------------";
        yield return $"   (instability <= {Sentences.Number(model.Policy.StableThreshold)} — much depends on it, it depends on";
        yield return $"    little — AND a method above cc {model.Policy.HighCc})";

        var found = findings.OfKind(FindingKind.LoadBearingAndIntricate);
        if (found.Count == 0) yield return "   (none)";

        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            // Effective fan-out excludes abstractions, so "depends on nothing" and "depends on
            // nothing concrete" are different claims and the second one names the difference.
            var dependsOn = type.EffectiveFanOut == 0
                ? type.FanOut == 0 ? "nothing" : $"nothing concrete ({type.FanOut} abstractions/contracts)"
                : type.EffectiveFanOut == type.FanOut
                    ? $"{type.EffectiveFanOut}"
                    : $"{type.EffectiveFanOut} concrete types ({type.FanOut} total)";

            yield return $"   {type.Name} — instability {Sentences.Ratio(finding.ValueOf("Instability") ?? 0)}: "
                         + $"{Sentences.Plural(type.FanIn, "type")} depend on it, it depends on {dependsOn}. "
                         + $"And {type.MostComplexMember?.Name} is cc {type.MaxMemberCyclomatic}. "
                         + "Hard to change safely, and intricate enough to hide a bug.";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> BreaksAlone(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- BREAKS ALONE (no cohort required) ---------------------------";
        yield return "   (complex, but almost nothing depends on it — the reassuring message)";

        var found = findings.OfKind(FindingKind.BreaksAlone);
        if (found.Count == 0) yield return "   (none)";

        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} — instability {Sentences.Ratio(finding.ValueOf("Instability") ?? 0)}: "
                         + $"only {Sentences.Plural(type.FanIn, "type")} "
                         + $"{(type.FanIn == 1 ? "depends" : "depend")} on it. "
                         + $"Complex inside (cc {type.MaxMemberCyclomatic}) but isolated — "
                         + "if it breaks, it breaks alone.";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> HubsAndGodObjects(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- HUBS AND GOD OBJECTS (no cohort required) -------------------";
        yield return $"   (fan-in AND fan-out both >= {model.Policy.HubMin} — a ratio cannot see these, since";
        yield return "    high-in + high-out lands mid-range, same as a trivial one-in one-out leaf)";

        var found = findings.OfKind(FindingKind.HubOrGodObject);
        if (found.Count == 0) yield return "   (none)";

        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} [{type.Classification.Kind}] — "
                         + $"fan-in {type.FanIn}, fan-out {type.FanOut}, "
                         + $"instability {Sentences.Ratio(finding.ValueOf("Instability") ?? 0)}. "
                         + Verdict(finding, type);
        }

        foreach (var line in disclosure) yield return line;

        if (found.Count > 0)
        {
            yield return "   NOTE: routers, mediators and composition roots legitimately live here. That";
            yield return "   does not make the flag wrong — those are exactly the things not to change";
            yield return "   lightly. Mark the known ones rather than tuning them away.";
        }
    }

    /// <summary>
    /// Which danger this hub actually presents.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/DEFECTS.md</c> §16.</b> The probe treats the two arms as one disjunction and
    /// prints "AND carries real logic" whenever either fires — which is false by construction on
    /// the size arm, since a type reaches it precisely by having bulk and no logic. The receipts
    /// in the same sentence then refute the sentence: <i>"carries real logic (23 members, worst
    /// method at cc 1)"</i>. Core carries the two as independent qualifiers, so the sentence is
    /// chosen from what actually holds and cannot contradict its own evidence.
    /// </remarks>
    private static string Verdict(Finding finding, TypeNode type)
    {
        var logic = finding.Holds(Qualifiers.CarriesRealLogic);
        var size = finding.Holds(Qualifiers.TooLargeToHold);
        var worst = type.MostComplexMember;

        if (!logic && !size)
        {
            return "Wiring hub: high coupling both ways but little logic inside "
                   + $"(worst method cc {type.MaxMemberCyclomatic}). Risky to re-route, not to reason about.";
        }

        var what = (logic, size) switch
        {
            (true, true) => $"AND carries real logic in something too large to hold at once "
                            + $"({type.MemberCount} members, worst method {worst?.Name} at cc "
                            + $"{type.MaxMemberCyclomatic}, dsm {type.Dsm})",
            (true, false) => $"AND carries real logic ({type.MemberCount} members, worst method "
                             + $"{worst?.Name} at cc {type.MaxMemberCyclomatic}, dsm {type.Dsm})",
            // The size arm alone. No claim about logic, because the receipts would refute it.
            _ => $"AND is too large for anyone to hold at once ({type.MemberCount} members, and "
                 + $"no method above cc {type.MaxMemberCyclomatic})",
        };

        return "Architectural bottleneck: it both depends on and is depended on by much of "
               + $"the system, {what}. Cross-domain orchestration and shared state tend to collect here.";
    }

    internal static IEnumerable<string> SharedMutableState(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- SHARED MUTABLE STATE (no cohort required) -------------------";
        yield return "   (writes to static mutable state — every caller on every thread shares these)";

        var found = findings.OfKind(FindingKind.SharedMutableState);
        if (found.Count == 0) yield return "   (none)";

        var (shown, disclosure) = Sentences.Cap(found, model.Policy.Top, "nomination");

        foreach (var finding in shown)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} — {Sentences.Plural(type.StaticMutations, "write")} to static state, "
                         + $"and {Sentences.Plural(type.FanIn, "type")} call into it. Whether these are "
                         + "genuinely contended is a runtime question this analysis cannot answer — "
                         + "but the sharing is certain from the code.";
        }

        foreach (var line in disclosure) yield return line;
    }

    internal static IEnumerable<string> SpansArchitecturalLayers(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- SPANS ARCHITECTURAL LAYERS (no cohort required) -------------";
        yield return $"   (dependencies reaching across {model.Policy.MinKindSpan}+ architectural kinds — cross-cutting";
        yield return "    work, whatever the component is named)";

        var found = findings.OfKind(FindingKind.SpansArchitecturalLayers);
        if (found.Count == 0)
        {
            yield return "   (none)";
            yield break;
        }

        // Collapsed and detailed findings are the same claim worded two ways, and which one
        // applies is the qualifier's answer rather than a count taken here. docs/DEFECTS.md §11:
        // a pattern is a shared dependency set, so the renderer can no longer decide this by
        // grouping on the kind signature.
        var collapsed = found.Where(f => f.Holds(Qualifiers.PartOfALayeringPattern)).ToList();
        var detailed = found.Where(f => !f.Holds(Qualifiers.PartOfALayeringPattern)).ToList();

        foreach (var group in collapsed
                     .GroupBy(f => Signature(model, f), StringComparer.Ordinal)
                     .OrderBy(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var names = group
                .Select(f => model.Find(f.Subject)?.Name ?? "")
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var examples = names.Take(4).ToList();

            yield return $"   {group.Count()} types span {group.Key} — a layering pattern rather than an";
            yield return $"     anomaly. Examples: {string.Join(", ", examples)}"
                         + (names.Count > examples.Count
                             ? $" — {examples.Count} of {names.Count} named"
                             : "");
        }

        foreach (var finding in detailed)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"   {type.Name} [{type.Classification.Kind}] — reaches across "
                         + $"{Sentences.Whole(finding.ValueOf("KindSpan") ?? 0)} kinds:";

            foreach (var line in ByKind(model, finding, type)) yield return line;

            yield return "       Check that the name still describes what it does.";
        }
    }

    /// <summary>
    /// How many dependency names one kind may show before the line stops being readable.
    /// </summary>
    private const int NamesPerKind = 6;

    /// <summary>
    /// The named dependencies, grouped under the architectural kind each one belongs to.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-b.md</c> §3.1: the per-kind breakdown <i>is</i> the finding. "Spans three
    /// kinds" is arguable and a reader cannot check it; <i>"why is authentication calling
    /// TenantStore?"</i> is neither. The kinds come from the participants' own classifications
    /// rather than from the finding, because a participant is a subject and its role is a
    /// property of that subject.
    /// </remarks>
    private static IEnumerable<string> ByKind(SolutionModel model, Finding finding, TypeNode type)
    {
        var byKind = finding.Participants
            .Select(model.Find)
            .Where(t => t is not null)
            .GroupBy(t => t!.Classification.Kind, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t!.Name).Distinct(StringComparer.Ordinal)
                      .OrderBy(n => n, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var kinds = new SortedSet<string>(byKind.Keys, StringComparer.Ordinal);

        // The type's own role counts toward the span. Where it does, the span exceeds the number
        // of kinds reached through dependencies, and the difference is what "itself" names — the
        // component is cross-cutting partly by being where it is.
        if ((finding.ValueOf("KindSpan") ?? 0) > byKind.Count) kinds.Add(type.Classification.Kind);

        foreach (var kind in kinds)
        {
            if (!byKind.TryGetValue(kind, out var names))
            {
                yield return $"       {kind,-14} itself";
                continue;
            }

            var shown = names.Take(NamesPerKind).ToList();

            yield return $"       {kind,-14} {string.Join(", ", shown)}"
                         + (names.Count > shown.Count ? $" — {shown.Count} of {names.Count} shown" : "");
        }
    }

    /// <summary>The kind signature a collapsed group is named by.</summary>
    private static string Signature(SolutionModel model, Finding finding) =>
        string.Join("+", finding.Participants
            .Select(model.Find)
            .Where(t => t is not null)
            .Select(t => t!.Classification.Kind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal));

    /// <summary>
    /// <c>base:global::App.ControllerBase</c> becomes <c>ControllerBase</c>.
    /// </summary>
    private static string ShortCohort(string cohort)
    {
        var afterPrefix = cohort.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0
            ? cohort[(colon + 1)..]
            : cohort;

        var lastDot = afterPrefix.LastIndexOf('.');
        return lastDot >= 0 ? afterPrefix[(lastDot + 1)..] : afterPrefix;
    }

    /// <summary>Resolves a member subject back to its declaring type and the member itself.</summary>
    private static (TypeNode? Type, Member? Member) Member(SolutionModel model, SubjectRef subject)
    {
        if (subject.DeclaringType is not { } declaring) return (null, null);
        if (model.Find(declaring) is not { } type) return (null, null);

        return (type, type.Members.FirstOrDefault(m => m.Subject == subject));
    }
}

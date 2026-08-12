namespace IronMarten.Bearing.Cli;

/// <summary>
/// Job A's sections: what the solution is, rather than what is unusual about it.
/// </summary>
/// <remarks>
/// Every one of these reads a projection on <see cref="SolutionModel"/> and makes no claim about
/// any subject. None of them can be suppressed, because there is nothing to be wrong about — a
/// cycle either exists or does not.
/// </remarks>
internal static class StructureSections
{
    /// <summary>
    /// How many members of one cycle are named before the list is cut.
    /// </summary>
    /// <remarks>
    /// A display cap, so it lives here and not on the policy — and unlike the probe's, it says
    /// what it cost. <c>docs/DEFECTS.md</c> §3.
    /// </remarks>
    private const int NamespacesPerCycle = 6;

    /// <summary>The same, for the type graph, where names are shorter.</summary>
    private const int TypesPerTangle = 8;

    internal static IEnumerable<string> ContactPoints(SolutionModel model)
    {
        var contact = model.ContactPoints;

        yield return "";
        yield return "-- BOUNDARY: HERE BE DRAGONS -----------------------------------";
        yield return $"   {contact.Count} external contact point(s): "
                     + $"{contact.Inbound.Count} inbound API, "
                     + $"{contact.Outbound.Count} outbound. Consumer impact of";
        yield return "   changes at ANY of these is outside what static analysis can see.";
    }

    internal static IEnumerable<string> IntegrationMap(SolutionModel model)
    {
        var map = model.Integrations;

        yield return "";
        yield return "   INTEGRATION MAP — external systems, by how many types touch them:";

        if (map.Systems.Count == 0)
            yield return "     (none detected outside language/framework plumbing)";

        var (shown, disclosure) = Sentences.Cap(map.Systems, model.Policy.Top, "system", "     ");

        foreach (var system in shown)
            yield return $"     {system.Namespace,-42} {Sentences.Plural(system.TypesTouching, "type")}";

        foreach (var line in disclosure) yield return line;

        if (map.PlumbingReferences > 0)
            yield return $"     ({map.PlumbingReferences} language/runtime references omitted as plumbing)";
    }

    internal static IEnumerable<string> CircularReferences(SolutionModel model)
    {
        yield return "";
        yield return "-- CIRCULAR REFERENCES -----------------------------------------";
        yield return "   NAMESPACE CYCLES — mutually dependent namespaces cannot be layered,";
        yield return "   understood, or extracted independently:";

        var cycles = model.NamespaceCycles;
        if (cycles.Count == 0) yield return "     (none)";

        var (shownCycles, cycleDisclosure) = Sentences.Cap(cycles, model.Policy.Top, "cycle", "     ");

        // Recovered by asking the model which namespaces it has rather than by taking the
        // canonical form apart. A SubjectRef's string is an identity, not a display name, and
        // reaching into it here would couple the report to that encoding.
        var namespaceNames = model.Namespaces
            .ToDictionary(n => SubjectRef.ForNamespace(n.Namespace), n => n.Namespace);

        foreach (var cycle in shownCycles)
            yield return "     " + Members(
                cycle, "namespaces", NamespacesPerCycle, " <-> ",
                id => namespaceNames.GetValueOrDefault(id, id.Canonical));

        foreach (var line in cycleDisclosure) yield return line;

        yield return "";
        yield return $"   TYPE TANGLES — {model.Policy.MinTangle}+ types that all reach each other, so none of";
        yield return "   them can be tested or changed in isolation:";

        var tangles = model.TypeTangles;
        if (tangles.Count == 0)
            yield return "     (none — mutual pairs and triples are ordinary and not reported)";

        var (shownTangles, tangleDisclosure) = Sentences.Cap(tangles, model.Policy.Top, "tangle", "     ");

        foreach (var tangle in shownTangles)
            yield return "     " + Members(
                tangle, "types", TypesPerTangle, ", ", id => model.Find(id)?.Name ?? id.Canonical);

        foreach (var line in tangleDisclosure) yield return line;
    }

    /// <summary>
    /// One cycle as a line, naming as many members as fit and saying so when they do not.
    /// </summary>
    /// <remarks>
    /// The probe writes <c>", ..."</c> here, which leaves the count recoverable and the names
    /// not. Saying "6 of 10 shown" costs the same space and answers the question the ellipsis
    /// raises. <c>docs/DEFECTS.md</c> §3.
    /// </remarks>
    private static string Members(
        Cycle cycle, string noun, int limit, string separator, Func<SubjectRef, string> name)
    {
        var names = cycle.Members.Select(name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var shown = names.Take(limit).ToList();

        var line = $"{cycle.Size} {noun}: {string.Join(separator, shown)}";

        return names.Count > limit ? $"{line} — {shown.Count} of {names.Count} shown" : line;
    }

    internal static IEnumerable<string> ProjectStability(SolutionModel model)
    {
        yield return "";
        yield return "-- PROJECT STABILITY vs ABSTRACTNESS ---------------------------";
        yield return "   I = Ce/(Ce+Ca), low = much depends on it. A = share of types that are";
        yield return "   abstract or interfaces. D = |A + I - 1|, distance from the main sequence.";
        yield return "   Stable AND concrete is the zone of pain: hard to change, hard to extend.";

        foreach (var coupling in model.ProjectCouplings)
        {
            if (coupling.Instability is not { } instability)
            {
                yield return $"     {coupling.Project} — no cross-project coupling; "
                             + $"A {Sentences.Number(coupling.Abstractness)}";
                continue;
            }

            yield return $"     {coupling.Project} — I {Sentences.Number(instability)}, "
                         + $"A {Sentences.Number(coupling.Abstractness)}, "
                         + $"D {Sentences.Number(coupling.DistanceFromMainSequence ?? 0)}  "
                         + $"(Ca {coupling.TypesElsewhereReachingIn} depend on it, "
                         + $"Ce {coupling.TypesHereReachingOut} reach out, "
                         + $"{coupling.AbstractTypes}/{coupling.TotalTypes} abstract)"
                         + Zone(coupling.Zone);
        }

        // The section is computed over types, so a project that declared none is not in the list
        // above at all. Silence there reads as "nothing to report about it" when what happened is
        // that it was never measured — TASKS.md R1, and the same species as defect 3.
        var measured = model.ProjectCouplings.Select(c => c.Project).ToHashSet(StringComparer.Ordinal);
        var unmeasured = model.Projects
            .Where(p => !measured.Contains(p.Name))
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (unmeasured.Count > 0)
        {
            yield return $"     ({Sentences.Plural(unmeasured.Count, "project")} declared no analysed type "
                         + "and cannot be placed: " + string.Join(", ", unmeasured) + ")";
        }
    }

    private static string Zone(MainSequenceZone zone) => zone switch
    {
        MainSequenceZone.Pain => "  <-- ZONE OF PAIN (stable and concrete)",
        MainSequenceZone.Uselessness => "  <-- zone of uselessness (abstract, unused)",
        MainSequenceZone.NearMainSequence => "  (near the main sequence)",
        _ => "",
    };

    internal static IEnumerable<string> UnreferencedProjects(SolutionModel model)
    {
        var dead = model.UnreferencedProjects;

        yield return "";
        yield return "   UNREFERENCED PROJECTS — no other project depends on these:";

        if (dead.Count == 0)
        {
            yield return "     (none)";
            yield break;
        }

        foreach (var project in dead) yield return $"     {project.Name}";

        yield return "     Entry points, executables and API hosts are excluded — a root is not dead.";
        yield return "     But test projects are skipped by default, so anything used ONLY by tests";
        yield return "     appears here. Verify before deleting.";

        // Which ones, when there are any. The probe states the caveat in the abstract; naming the
        // skipped projects is what turns it into something a reader can check.
        if (model.Coverage.SkippedProjects.Count > 0)
            yield return $"     Skipped this run: {string.Join(", ", model.Coverage.SkippedProjects)}.";
    }
}

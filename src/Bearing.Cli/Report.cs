namespace IronMarten.Bearing.Cli;

/// <summary>
/// The terminal report: the order of the sections, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Kept to the sequencing on purpose. The probe's equivalent grew to 997 lines because each
/// section's text and each section's <i>computation</i> ended up in the same method, and the
/// per-project I/A/D numbers are still calculated inside a <c>WriteLine</c> there. Here the
/// numbers arrive already decided, the sentences live with their section, and this file holds the
/// one decision that genuinely spans them: what comes first.
/// </para>
/// <para>
/// <b>Lines, not a writer.</b> Returning an enumerable rather than writing to a
/// <see cref="TextWriter"/> means the report can be asserted on without a file or a console —
/// which matters more than it sounds, because this is the layer carrying defects 3, 16 and 17.
/// </para>
/// <para>
/// <b>The boundary section has two owners</b> and is assembled here from both: its count and
/// integration map are properties of the solution, its two nominations are claims about
/// particular types. That join is this file's business and nothing else's.
/// </para>
/// </remarks>
public static class Report
{
    /// <summary>Renders the whole report.</summary>
    public static IEnumerable<string> For(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        foreach (var line in Header()) yield return line;

        foreach (var line in FindingSections.ConcealedDecisionAtTypeLevel(model, findings)) yield return line;
        foreach (var line in FindingSections.ConcealedDecisionAtMethodLevel(model, findings)) yield return line;
        foreach (var line in FindingSections.BlastRadius(model, findings)) yield return line;
        foreach (var line in FindingSections.ChangeCost(model, findings)) yield return line;

        // --- the boundary section, both halves ---
        foreach (var line in StructureSections.ContactPoints(model)) yield return line;
        foreach (var line in StructureSections.IntegrationMap(model)) yield return line;
        foreach (var line in BoundaryNominations(model, findings)) yield return line;

        foreach (var line in FindingSections.LoadBearing(model, findings)) yield return line;
        foreach (var line in FindingSections.BreaksAlone(model, findings)) yield return line;
        foreach (var line in FindingSections.HubsAndGodObjects(model, findings)) yield return line;
        foreach (var line in FindingSections.SpansArchitecturalLayers(model, findings)) yield return line;

        foreach (var line in StructureSections.CircularReferences(model)) yield return line;
        foreach (var line in FindingSections.SharedMutableState(model, findings)) yield return line;
        foreach (var line in StructureSections.ProjectStability(model)) yield return line;
        foreach (var line in StructureSections.UnreferencedProjects(model)) yield return line;

        foreach (var line in CoverageSection.NoPeerGroup(model, findings)) yield return line;

        yield return "";
    }

    private static IEnumerable<string> Header()
    {
        yield return "";
        yield return "================================================================";
        yield return "NOMINATED INSTANCES";
        yield return "Draft sentences. Receipts in parentheses. Rewrite before the session.";
        yield return "================================================================";
    }

    private static IEnumerable<string> BoundaryNominations(SolutionModel model, FindingSet findings)
    {
        var logic = findings.OfKind(FindingKind.BoundaryCarriesLogic);

        yield return "";
        yield return "   BOUNDARIES CARRYING REAL LOGIC — decisions made at the edge:";

        if (logic.Count == 0)
            yield return "     (none — logic lives behind the boundary, which is what you want)";

        var (shownLogic, logicDisclosure) = Sentences.Cap(logic, model.Policy.Top, "boundary", "     ");

        foreach (var finding in shownLogic)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"     {type.Name} — {type.MostComplexMember?.Name} is cc "
                         + $"{type.MaxMemberCyclomatic}. Business decisions at an external edge "
                         + "are the hardest kind to change later.";
        }

        foreach (var line in logicDisclosure) yield return line;

        // Suppressed as a set when it stops discriminating, so an empty list here is a decision
        // rather than an absence of candidates — docs/DEFECTS.md §12. Saying nothing at all is
        // what the rule asks for; the section simply does not appear.
        var surfaces = findings.OfKind(FindingKind.WidestContractSurface);
        if (surfaces.Count == 0) yield break;

        yield return "";
        yield return "   WIDEST CONTRACT SURFACE — most to get wrong, most to break:";

        var (shownSurfaces, surfaceDisclosure) = Sentences.Cap(surfaces, model.Policy.Top, "boundary", "     ");

        foreach (var finding in shownSurfaces)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            yield return $"     {type.Name} — {type.DataShape} fields/params across "
                         + $"{Sentences.Plural(type.PublicMemberCount, "public member")}.";
        }

        foreach (var line in surfaceDisclosure) yield return line;
    }
}

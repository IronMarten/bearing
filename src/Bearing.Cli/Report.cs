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

        foreach (var line in Header(model)) yield return line;

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

        // Last, and the placement is argued in the section itself: it qualifies everything above
        // it, which is a reason to lead with it, and on a clean run it is bookkeeping, which is a
        // reason not to.
        foreach (var line in StructureSections.NotAnalysed(model.Coverage)) yield return line;

        yield return "";
    }

    /// <summary>
    /// The coverage section, rendered from <see cref="Coverage"/> alone.
    /// </summary>
    /// <remarks>
    /// <b>Public where no other section is, and the reason is a distinction rather than a
    /// concession to testing.</b> Every other section reads a <see cref="SolutionModel"/>, whose
    /// constructor is internal — a model can only be produced by a walk — so those can only be
    /// exercised through a real solution. This one is a pure function of a public type, and the
    /// branch that matters most in it is the one no solution in this repository can produce: every
    /// solution here loads cleanly, so a load diagnostic would otherwise be the loudest thing the
    /// report can say with nothing exercising it.
    /// </remarks>
    public static IEnumerable<string> NotAnalysed(Coverage coverage) =>
        StructureSections.NotAnalysed(coverage);

    /// <summary>
    /// What was analysed, and how to read what follows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaces probe-era scaffolding that was printing to every user.</b> The old header
    /// read <i>"NOMINATED INSTANCES / Draft sentences. Receipts in parentheses. Rewrite before the
    /// session."</i> — an instruction to whoever was about to present the output in a review, from
    /// a period when the only reader was the person who ran it. Shipping it told a first-time user
    /// that the tool did not consider its own sentences finished.
    /// </para>
    /// <para>
    /// <b>The version and the solution name are here because the first thing a bug report needs is
    /// which build said this about what.</b> The counts are here because a reader cannot judge
    /// "top 5%" without knowing 5% of what, and the alternative is discovering the scale four
    /// sections later.
    /// </para>
    /// <para>
    /// <b>The version is read from this assembly and not from
    /// <see cref="SolutionModel.ToolVersion"/>, which is wrong.</b> That property reads
    /// <c>Bearing.Core</c>'s assembly, and <c>&lt;Version&gt;</c> is set on <c>Bearing.Cli</c> —
    /// so the model reports <c>1.0.0</c> where the shipped tool is <c>0.0.1-preview.1</c>. Nothing
    /// rendered it before, so nothing caught it. The version a user quotes has to be the version
    /// they installed, which is this one; the model's copy still needs settling before it reaches
    /// the JSON writer, where it becomes a field somebody parses.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Header(SolutionModel model)
    {
        yield return "";
        yield return "================================================================";
        yield return $"BEARING {ToolInfo.ReadVersion(typeof(Report).Assembly)} — "
                     + Path.GetFileName(model.SolutionPath);
        yield return $"{Sentences.Plural(model.Types.Count, "type")} in "
                     + $"{Sentences.Plural(model.Projects.Count, "project")}. "
                     + "Unusual findings first, then structure.";
        yield return "Every claim shows the numbers behind it.";
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

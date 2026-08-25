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

    /// <summary>
    /// Mutually-holding pairs named under a reported cycle.
    /// </summary>
    /// <remarks>
    /// The evidence for the finding rather than the finding itself, so it is capped harder than
    /// the membership line: a reader needs to see that the coupling is real and where it is
    /// heaviest, not every instance of it. Over the cap the count is stated.
    /// </remarks>
    private const int HeldPairsPerCycle = 6;

    /// <summary>
    /// Directed links named under a reported project cycle.
    /// </summary>
    /// <remarks>
    /// A cycle of four projects has at most a handful of links that close it, and naming them all
    /// is the point — this is a cap against a pathological graph, not against the ordinary one.
    /// </remarks>
    private const int ProjectLinksPerCycle = 6;

    /// <summary>
    /// Reference kinds named when saying what holds a tangle together.
    /// </summary>
    private const int KindsPerTangle = 3;

    /// <summary>The same, for the type graph, where names are shorter.</summary>
    private const int TypesPerTangle = 8;

    /// <summary>
    /// The same again for projects. Lower because a solution has far fewer projects than
    /// namespaces, so a cycle naming more than four of them is already the whole story.
    /// </summary>
    private const int ProjectsPerCycle = 4;

    /// <summary>
    /// The cycles that are real and are not findings, named with what closes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Listed, not counted.</b> A line saying "21 suppressed" is a claim the reader cannot
    /// check, and the suppression is the part of this section most likely to be wrong — it rests
    /// on a reading of what the closing edges are, and a reading can misfire. Naming each one and
    /// why costs a line apiece and makes disagreeing with the tool possible.
    /// </para>
    /// <para>
    /// Under its own heading rather than indented beneath the findings, because these are not
    /// lesser findings. They are components the graph really contains, about which the section
    /// has nothing to ask anyone to do.
    /// </para>
    /// </remarks>
    /// <summary>
    /// What holds a tangle together — the line that decides whether it is worth unpicking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both arms are claims about the same component, and only one of them is a defect.</b>
    /// nopCommerce's single tangle is <c>BaseDataProvider</c> beside its own three providers, and
    /// it dissolves entirely once inheritance is set aside. Saying "none of them can be tested or
    /// changed in isolation" about a class hierarchy is how a reader decides the section does not
    /// know what one is.
    /// </para>
    /// <para>
    /// It is still listed. Unlike a namespace cycle there is nothing misleading about the
    /// membership — those six types really do reach each other — so what changes is the sentence
    /// beside it, not whether it appears.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A cycle finding's relations, or empty when the cycle was suppressed and has no finding in
    /// the reported set.
    /// </summary>
    /// <remarks>
    /// <b>Empty is a real answer here and not a miss.</b> The namespace group renders suppressed
    /// cycles in its <i>not reported</i> list, and a suppressed finding is not in the set the
    /// renderers are handed — so this returns nothing for those, which is why the namespace side
    /// still reads its held pairs off the shape rather than off a finding.
    /// </remarks>
    private static IReadOnlyList<Relation> Relations(FindingSet findings, FindingKind kind, Cycle cycle) =>
        findings.OfKind(kind).FirstOrDefault(f => f.Subject.Equals(cycle.Subject))?.Relations ?? [];

    private static string Holds(
        ShapedTangle tangle, IReadOnlyList<Relation> relations, Func<SubjectRef, string> name)
    {
        if (tangle.Shape == TangleShape.Hierarchy)
            return "a type hierarchy: set the inheritance aside and nothing mutually "
                   + "dependent is left, so this is a base and its own implementations";

        var kinds = string.Join(", ", tangle.Kinds.Take(KindsPerTangle));
        var held = kinds.Length == 0 ? "held by references the walk could not attribute" : $"held by {kinds}";

        return CycleEvidence.HeaviestPair(relations) is { } pair
            ? $"{held}; heaviest: {name(pair.First)} <-> {name(pair.Second)}, "
              + Sentences.Plural(pair.Weight, "reference")
            : held;
    }

    private static IEnumerable<string> NotLayering(
        IReadOnlyList<ShapedCycle> cycles, int top, Func<SubjectRef, string> name)
    {
        if (cycles.Count == 0) yield break;

        yield return "";
        yield return "   MUTUALLY DEPENDENT, NOT REPORTED ABOVE — these components are real, and";
        yield return "   none of them is a layering problem. The assembly is what gets extracted:";

        var (shown, disclosure) = Sentences.Cap(cycles, top, "cycle", "     ");

        foreach (var cycle in shown)
        {
            var label = cycle.Anchor ?? name(cycle.Cycle.Members[0]);

            var reason = cycle.Shape switch
            {
                CycleShape.FolderLayout =>
                    $"one assembly's own folders, all in {cycle.Projects[0]}",
                CycleShape.SharedTypes =>
                    "peers naming each other's entities or models, holding none of them",

                // Coupling never reaches here — IsReportable is exactly that case — and the arm
                // exists so that adding a shape without deciding how to say it fails visibly
                // rather than printing an empty reason.
                _ => "unclassified",
            };

            yield return $"     {label} — {Sentences.Plural(cycle.Cycle.Size, "namespace")}, {reason}";
        }

        foreach (var line in disclosure) yield return line;
    }

    /// <summary>
    /// The types at this solution's edge — and, when there are none, why that is worth doubting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The zero case is not a smaller version of the non-zero case.</b> This section used to
    /// print <c>"0 external contact point(s): 0 inbound API, 0 outbound"</c> four lines above an
    /// integration map naming six external systems, under a heading reading HERE BE DRAGONS. Both
    /// numbers were right and they measure different things — a contact point is a type in <i>this
    /// solution</i> that the classifier put at the edge, and the map counts external namespaces
    /// this solution <i>references</i> — but nothing said so, and adjacent lines saying "0" and
    /// "six" read as the tool contradicting itself.
    /// </para>
    /// <para>
    /// <b>It is also the most likely place for <c>docs/DEFECTS.md</c> §5 to show up.</b> The
    /// classifier recognises an external call by a hardcoded list of namespace prefixes —
    /// <c>System.Net.Http</c>, <c>Azure.</c>, <c>Stripe</c> and a handful more. Run Bearing on
    /// itself and it reports zero outbound contact points while listing Roslyn and MSBuild in the
    /// map, because a compiler API is not on that list. Rather than print a confident zero, the
    /// zero case names the two explanations and lets the reader pick, which is what invariant 8
    /// asks of any absence this tool cannot distinguish from ignorance.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string> ContactPoints(SolutionModel model)
    {
        var contact = model.ContactPoints;

        yield return "";
        yield return "-- BOUNDARY: HERE BE DRAGONS -----------------------------------";

        if (contact.Count == 0)
        {
            yield return "   No type in this solution was classified as an API boundary or an";
            yield return "   external call. Either this codebase has no edge of its own — a";
            yield return "   library called only by its own tests would look like this — or the";
            yield return "   frameworks it uses are not ones this tool recognises. The";
            yield return "   integration map below is the check: entries there with nothing here";
            yield return "   means the second.";
            yield break;
        }

        yield return $"   {Sentences.Plural(contact.Count, "external contact point")}: "
                     + $"{contact.Inbound.Count} inbound API, "
                     + $"{contact.Outbound.Count} outbound. Consumer impact of";
        yield return "   changes at ANY of these is outside what static analysis can see.";
    }

    internal static IEnumerable<string> IntegrationMap(SolutionModel model)
    {
        var map = model.Integrations;

        yield return "";

        // Named as a different measurement from the contact points above, because it is one:
        // these are namespaces this solution references, not types in it. Printed adjacently and
        // undistinguished, the two counts read as a contradiction whenever they disagree.
        yield return "   INTEGRATION MAP — external systems this solution calls into,";
        yield return "   by how many of its types touch them:";

        if (map.Systems.Count == 0)
            yield return "     (none detected outside language/framework plumbing)";

        var (shown, disclosure) = Sentences.Cap(map.Systems, model.Policy.Top, "system", "     ");

        // docs/DEFECTS.md §30. The row says what somebody could change: the framework is not a
        // dependency anybody is going to rewrite, and a package is. Origin decides the label and
        // deliberately not the list -- see ExternalSurface.Integrations for what that cost when
        // it was tried the other way round.
        foreach (var system in shown)
            yield return $"     {system.Namespace,-42} {Sentences.Plural(system.TypesTouching, "type")}"
                         + Sentences.Origin(system.Origin);

        foreach (var line in disclosure) yield return line;

        if (map.PlumbingReferences > 0)
            yield return $"     ({map.PlumbingReferences} language/runtime references omitted as plumbing)";
    }

    internal static IEnumerable<string> CircularReferences(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CIRCULAR REFERENCES -----------------------------------------";
        yield return "   NAMESPACE CYCLES — sibling namespaces that hold each other as state,";
        yield return "   so neither can be layered, understood or extracted without the other:";

        // Recovered by asking the model which namespaces it has rather than by taking the
        // canonical form apart. A SubjectRef's string is an identity, not a display name, and
        // reaching into it here would couple the report to that encoding.
        var namespaceNames = model.Namespaces
            .ToDictionary(ns => SubjectRef.ForNamespace(ns.Namespace), ns => ns.Namespace);

        string NamespaceName(SubjectRef id) => namespaceNames.GetValueOrDefault(id, id.Canonical);

        var shaped = model.ShapedNamespaceCycles;
        var reportable = shaped.Where(c => c.IsReportable).ToList();
        var setAside = shaped.Where(c => !c.IsReportable).ToList();

        if (reportable.Count == 0)
            yield return "     (none — no two peer namespaces hold each other)";

        var (shownCycles, cycleDisclosure) = Sentences.Cap(reportable, model.Policy.Top, "cycle", "     ");

        foreach (var shapedCycle in shownCycles)
        {
            yield return "     " + Members(shapedCycle.Cycle, "namespaces", NamespacesPerCycle, " <-> ", NamespaceName);
            yield return "       " + Loop(shapedCycle.Cycle, NamespaceName);

            foreach (var pair in shapedCycle.Pairs.Take(HeldPairsPerCycle))
                yield return $"       {pair.First} <-> {pair.Second} — "
                             + Sentences.Plural(pair.Weight, "held reference");

            if (shapedCycle.Pairs.Count > HeldPairsPerCycle)
                yield return $"       ({shapedCycle.Pairs.Count - HeldPairsPerCycle} more mutually-holding "
                             + "pairs in this cycle)";
        }

        foreach (var line in cycleDisclosure) yield return line;

        foreach (var line in NotLayering(setAside, model.Policy.Top, NamespaceName)) yield return line;

        yield return "";
        yield return "   PROJECT CYCLES — two projects each naming a type in the other. Legal";
        yield return "   MSBuild: only project references cannot cycle, and this is the type";
        yield return "   graph aggregated, which is finer than the references are:";

        var projectCycles = model.ProjectCycles;
        if (projectCycles.Count == 0)
            yield return "     (none — every cross-project dependency runs one way)";

        var (shownProjects, projectDisclosure) = Sentences.Cap(projectCycles, model.Policy.Top, "cycle", "     ");

        var projectNames = model.Projects
            .ToDictionary(p => SubjectRef.ForProject(p.Name), p => p.Name);

        string ProjectName(SubjectRef id) => projectNames.GetValueOrDefault(id, id.Canonical);

        foreach (var cycle in shownProjects)
        {
            yield return "     " + Members(cycle, "projects", ProjectsPerCycle, " <-> ", ProjectName);
            yield return "       " + Loop(cycle, ProjectName);

            // No suppression here, unlike the namespace cycles: the assembly is the unit anyone
            // extracts, so two of them naming each other is a finding at any weight. What was
            // missing is where to start, which is the heaviest link and the type that carries it.
            //
            // Read off the finding rather than recomputed from model.Edges, and derived by
            // CycleEvidence rather than here — docs/DEFECTS.md §46 was one renderer keeping
            // evidence the other lost, so neither of these two may hold its own copy of either.
            foreach (var link in CycleEvidence
                         .ProjectLinks(model, Relations(findings, FindingKind.ProjectCycle, cycle))
                         .Take(ProjectLinksPerCycle))
            {
                var carrier = link.Example is { } via
                    ? $" — heaviest: {TypeName(via.From)} -> {TypeName(via.To)}"
                    : "";

                yield return $"       {link.From} -> {link.To}: "
                             + Sentences.Plural(link.Weight, "reference") + carrier;
            }
        }

        foreach (var line in projectDisclosure) yield return line;

        yield return "";
        yield return $"   TYPE TANGLES — {model.Policy.MinTangle}+ types that all reach each other, so none of";
        yield return "   them can be tested or changed in isolation:";

        if (model.TypeTangles.Count == 0)
            yield return "     (none — mutual pairs and triples are ordinary and not reported)";

        var (shownTangles, tangleDisclosure) = Sentences.Cap(
            model.ShapedTypeTangles, model.Policy.Top, "tangle", "     ");

        string TypeName(SubjectRef id) => model.Find(id)?.Name ?? id.Canonical;

        foreach (var shapedTangle in shownTangles)
        {
            var tangle = shapedTangle.Tangle;
            yield return "     " + Members(tangle, "types", TypesPerTangle, ", ", TypeName);
            yield return "       " + Loop(tangle, TypeName);
            yield return "       " + Holds(
                shapedTangle, Relations(findings, FindingKind.TypeTangle, tangle), TypeName);
        }

        foreach (var line in tangleDisclosure) yield return line;
    }

    /// <summary>
    /// One traversable loop through a cycle — <c>A -&gt; B -&gt; C -&gt; A</c> — saying so when
    /// it is smaller than the entanglement it came out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line above this one gives the extent of the problem and this one gives an instance of
    /// it, which is <c>TECHREQ-job-a.md</c> §5.1's ask: "these six namespaces are mutually
    /// entangled" is true and cannot be acted on, and a named edge can.
    /// </para>
    /// <para>
    /// <b>The qualifier is the point, not decoration.</b> A component of six can have a loop of
    /// two inside it, and a two-name loop printed with nothing to say otherwise reads as the
    /// whole cycle — so it reads as "delete this one edge and the entanglement is gone", which is
    /// false and is invariant 4's failure mode. When the walk covers everything, there is nothing
    /// to disclose and nothing is said.
    /// </para>
    /// <para>
    /// It is deliberately not capped the way the membership line is. A loop with a member removed
    /// is not a shorter loop, it is not a loop — so if this is ever long enough to need cutting,
    /// the fix is to say the length and drop the line, not to print most of a path.
    /// </para>
    /// </remarks>
    private static string Loop(Cycle cycle, Func<SubjectRef, string> name)
    {
        var steps = cycle.Path.Select(name).ToList();
        var loop = $"loop: {string.Join(" -> ", steps)} -> {steps[0]}";

        // "N of the M" is the same idiom the membership line above uses, and it is phrased this
        // way to dodge a verb: the first version read "the other 1 are entangled too" wherever a
        // component was exactly one larger than its loop, which happened ten times across
        // Jellyfin and nopCommerce and cannot happen on the fixture, whose two remainders are 2
        // and 5. Agreeing a verb with a computed number is a defect waiting on the right input;
        // not having one is not. A cycle is always two or more, so "all M" never has the problem.
        return cycle.PathCoversEveryMember
            ? loop
            : $"{loop} — {steps.Count} of the {cycle.Size}; all {cycle.Size} reach each other";
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

    /// <summary>
    /// What the analysis did not see — invariant 8, and the section every number above depends on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The model has carried this since the walk and nothing rendered it.</b> <c>Coverage</c>
    /// holds <c>ExclusionsApplied</c>, <c>ExcludedTypes</c> and <c>LoadDiagnostics</c>, and until
    /// <c>TASKS.md</c> A1 no line of <c>Bearing.Cli</c> read any of the three — only
    /// <c>SkippedProjects</c>, and only inside the unreferenced-projects caveat. A tool whose
    /// entire discipline is not making claims it cannot support was silently dropping the record
    /// of what it could not see.
    /// </para>
    /// <para>
    /// <b>Load diagnostics come first inside the section and are worded as diagnostics.</b>
    /// <c>Coverage</c> says in as many words that they are not necessarily failures, and
    /// <c>docs/DEFECTS.md</c> §4 is the reason to take that seriously — load success is judged by
    /// diagnostic rather than by outcome, which produced six spurious failures on nopCommerce. So
    /// the wording says what is certain (a project that did not load understates fan-in everywhere
    /// it is referenced) without asserting that any particular diagnostic means that happened.
    /// </para>
    /// <para>
    /// <b>Placement is provisional and the argument against it should be recorded.</b> A failed
    /// load makes every number above it wrong, which argues for the top of the report rather than
    /// the end. It is here because on a clean run this section is routine bookkeeping — two
    /// exclusions and a skipped test project — and leading with that pushes the findings down for
    /// nothing. The case for promoting it needs a run where a load actually fails, which is
    /// <c>TASKS.md</c> A2 and does not exist yet.
    /// </para>
    /// <para>
    /// Takes <see cref="Coverage"/> rather than the model, so the diagnostics path can be tested
    /// without a deliberately broken solution to walk.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string> NotAnalysed(Coverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        yield return "";
        yield return "-- WHAT WAS NOT ANALYSED ---------------------------------------";
        yield return "   Every number above is relative to what was read. This is the rest.";

        // The warning belongs to the outcome, not to the diagnostics — docs/DEFECTS.md §4. It
        // used to hang off LoadDiagnostics, which on nopCommerce meant telling a reader to rule
        // out six NuGet advisories before trusting any number on the page, when all 27 projects
        // had compiled. What bounds fan-in is a project that did not load, and now that is what
        // says so.
        if (coverage.ProjectsNotLoaded.Count > 0)
        {
            yield return "";
            yield return $"   {Sentences.Plural(coverage.ProjectsNotLoaded.Count, "project")} did not load: "
                         + string.Join(", ", coverage.ProjectsNotLoaded) + ".";
            yield return "   A project that did not load understates fan-in EVERYWHERE it is";
            yield return "   referenced, so read every number above as a lower bound.";
        }

        // docs/DEFECTS.md §42. A tripwire rather than a feature: silent when nothing failed to
        // parse, which is every run on healthy code and both reference solutions. It sits above
        // the load diagnostics because it bounds the numbers the same way ProjectsNotLoaded does
        // — a file that was not read is types and edges that are not there.
        if (coverage.UnreadableFiles.Count > 0)
        {
            yield return "";
            yield return $"   {Sentences.Plural(coverage.UnreadableFiles.Count, "file")} could not be parsed and "
                         + "were not read:";

            var (files, capped) = Sentences.Cap(coverage.UnreadableFiles, DiagnosticsShown, "file", "     ");

            foreach (var file in files) yield return $"     {file}";
            foreach (var line in capped) yield return line;

            yield return "   Their types and edges are absent, so fan-in understates wherever they";
            yield return "   reached. This is a C# the parser did not accept, not a build error.";
        }

        if (coverage.LoadDiagnostics.Count > 0)
        {
            yield return "";
            yield return $"   {Sentences.Plural(coverage.LoadDiagnostics.Count, "diagnostic")} while loading. "
                         + "MSBuild raises a package";
            yield return "   vulnerability advisory this way, so these are usually not failures:";

            var (shown, disclosure) = Sentences.Cap(
                coverage.LoadDiagnostics, DiagnosticsShown, "diagnostic", "     ");

            foreach (var diagnostic in shown) yield return $"     {diagnostic}";
            foreach (var line in disclosure) yield return line;
        }

        // Directly under the diagnostics when there are any, which is the only place it answers
        // the question they raise. Stated either way: "every project loaded" is the reassurance a
        // reader needs before trusting a fan-in, and the absence of a warning is not that.
        if (coverage.ProjectsNotLoaded.Count == 0)
        {
            if (coverage.LoadDiagnostics.Count > 0) yield return "";
            yield return "   Every project selected for analysis produced a compilation.";
        }

        yield return "";

        yield return coverage.SkippedProjects.Count > 0
            ? $"   Skipped as test projects: {string.Join(", ", coverage.SkippedProjects)}"
            : "   Skipped as test projects: none";

        // The count of types, and the number of patterns in force — not the patterns themselves.
        // ExclusionsApplied is the set that was ACTIVE rather than the set that matched anything,
        // so listing it printed sixteen defaults on one unreadable line and did it identically on
        // a run where nothing was excluded at all. The patterns are the user's own input, on
        // --help and on their command line; the count is the part the report knows and they do not.
        yield return coverage.ExcludedTypes > 0
            ? $"   Excluded by path: {Sentences.Plural(coverage.ExcludedTypes, "type")}, under "
              + $"{Sentences.Plural(coverage.ExclusionsApplied.Count, "pattern")} "
              + "(--exclude-path, --no-default-excludes)"
            : $"   Excluded by path: none matched, under "
              + $"{Sentences.Plural(coverage.ExclusionsApplied.Count, "pattern")}";

        if (coverage.LoadDiagnostics.Count == 0)
            yield return "   Load diagnostics: none";

        // Stated whether or not any were dropped, because "none" is the reassurance and the
        // absence of a line is not. docs/DEFECTS.md §7.
        yield return coverage.EdgesToUnanalysedTypes > 0
            ? $"   Dependencies pointing outside the analysed set: "
              + $"{Sentences.Whole(coverage.EdgesToUnanalysedTypes)} (fan-in is a lower bound)"
            : "   Dependencies pointing outside the analysed set: none";
    }

    /// <summary>
    /// A display cap for diagnostics, deliberately not <c>--top</c>.
    /// </summary>
    /// <remarks>
    /// <c>--top</c> is how many <i>findings</i> a reader wants to see, and lowering it to focus a
    /// report should not also hide the reasons that report might be wrong. Fixed, and it discloses
    /// what it dropped like every other capped list — <c>docs/DEFECTS.md</c> §3.
    /// </remarks>
    private const int DiagnosticsShown = 10;
}

using System.Globalization;
using System.Text;

namespace ArchProbe;

static class Report
{
    public static void ComputeCohortStats(AnalysisResult result)
    {
        ComputeKindSpans(result);

        var allFanIn = result.Types.Select(t => (double)t.FanIn).ToArray();
        var allMaxCc = result.Types.Select(t => (double)t.MaxMemberCyclomatic).ToArray();
        foreach (var t in result.Types)
        {
            t.GlobalFanInPctl = Percentile(allFanIn, t.FanIn);
            t.GlobalMaxCcPctl = Percentile(allMaxCc, t.MaxMemberCyclomatic);
        }

        foreach (var group in result.Types.GroupBy(t => t.Cohort, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var size = members.Count;

            var fanIn = members.Select(m => (double)m.FanIn).ToArray();
            var fanOut = members.Select(m => (double)m.FanOut).ToArray();
            var cc = members.Select(m => (double)m.Cyclomatic).ToArray();
            var maxCc = members.Select(m => (double)m.MaxMemberCyclomatic).ToArray();
            var dsm = members.Select(m => (double)m.Dsm).ToArray();
            var shape = members.Select(m => (double)m.DataShape).ToArray();

            var medFanIn = Median(fanIn);
            var medFanOut = Median(fanOut);
            var medCc = Median(cc);
            var medMaxCc = Median(maxCc);
            var medDsm = Median(dsm);

            foreach (var m in members)
            {
                m.CohortSize = size;
                m.FanInPctl = Percentile(fanIn, m.FanIn);
                m.FanOutPctl = Percentile(fanOut, m.FanOut);
                m.CyclomaticPctl = Percentile(cc, m.Cyclomatic);
                m.MaxMemberCyclomaticPctl = Percentile(maxCc, m.MaxMemberCyclomatic);
                m.DsmPctl = Percentile(dsm, m.Dsm);
                m.DataShapePctl = Percentile(shape, m.DataShape);

                m.FanInXMedian = Ratio(m.FanIn, medFanIn);
                m.FanOutXMedian = Ratio(m.FanOut, medFanOut);
                m.CyclomaticXMedian = Ratio(m.Cyclomatic, medCc);
                m.MaxMemberCyclomaticXMedian = Ratio(m.MaxMemberCyclomatic, medMaxCc);
                m.DsmXMedian = Ratio(m.Dsm, medDsm);
            }
        }

        foreach (var group in result.Methods.GroupBy(m => m.Cohort, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var cc = members.Select(m => (double)m.Cyclomatic).ToArray();
            var dsm = members.Select(m => (double)m.Dsm).ToArray();
            var medCc = Median(cc);
            var medDsm = Median(dsm);

            foreach (var m in members)
            {
                m.CohortSize = members.Count;
                m.CyclomaticPctl = Percentile(cc, m.Cyclomatic);
                m.DsmPctl = Percentile(dsm, m.Dsm);
                m.CyclomaticXMedian = Ratio(m.Cyclomatic, medCc);
                m.DsmXMedian = Ratio(m.Dsm, medDsm);
            }
        }
    }

    /// <summary>
    /// The set of architecturally significant kinds a type reaches, its own position
    /// included. Stored so a later run can diff it — a component that starts touching a
    /// kind it never touched before is an architectural event, not gradual drift.
    /// </summary>
    static void ComputeKindSpans(AnalysisResult result)
    {
        var byId = result.Types.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);
        foreach (var t in result.Types)
        {
            var kinds = new SortedSet<string>(StringComparer.Ordinal);
            if (SignificantKinds.Contains(t.Kind)) kinds.Add(t.Kind);
            foreach (var id in t.OutboundTypes)
                if (byId.TryGetValue(id, out var dep) && SignificantKinds.Contains(dep.Kind))
                    kinds.Add(dep.Kind);
            t.KindSpan = string.Join("+", kinds);
        }
    }

    static double Ratio(double value, double median) => median <= 0 ? (value > 0 ? double.PositiveInfinity : 1) : value / median;

    static double Median(double[] values)
    {
        if (values.Length == 0) return 0;
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    /// <summary>
    /// Midrank percentile: strictly-below plus half the ties.
    ///
    /// Counting "at or below" instead puts every member of a fully-tied cohort at the
    /// 100th percentile — so eight normalizers that all have exactly one caller each
    /// read as top-percentile outliers and the alert fires on all of them. Ties are the
    /// normal case in a real peer cohort, and an alert that fires on the unremarkable
    /// majority is the one developers mute.
    /// </summary>
    static double Percentile(double[] values, double v)
    {
        if (values.Length == 0) return 0;
        var below = values.Count(x => x < v);
        var equal = values.Count(x => x == v);
        return 100.0 * (below + 0.5 * equal) / values.Length;
    }

    // ---------------------------------------------------------------- CSV ----

    public static void WriteTypesCsv(string path, IEnumerable<TypeMetrics> types)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine(string.Join(",",
            "Name", "Namespace", "Project", "Kind", "KindSpan", "Cohort", "CohortBasis", "CohortSize",
            "FanIn", "FanInPctl", "FanInXMedian", "InboundRefs",
            "FanOut", "FanOutEffective", "FanOutPctl", "FanOutXMedian",
            "Instability", "InstabilityRaw", "ExternalNamespaces",
            "Cyclomatic", "CyclomaticPctl", "CyclomaticXMedian",
            "MaxMemberCyclomatic", "MaxMemberCyclomaticPctl", "MaxMemberCyclomaticXMedian", "MaxMember",
            "Dsm", "DsmPctl", "DsmXMedian", "Transform", "StaticMutations",
            "ParamCount", "DataShape", "DataShapePctl",
            "GlobalFanInPctl", "GlobalMaxCcPctl",
            "MemberCount", "Loc", "File", "Line", "Id"));

        foreach (var t in types.OrderBy(t => t.Cohort, StringComparer.Ordinal)
                               .ThenByDescending(t => t.MaxMemberCyclomaticXMedian))
        {
            w.WriteLine(string.Join(",",
                Esc(t.Name), Esc(t.Namespace), Esc(t.Project), Esc(t.Kind), Esc(t.KindSpan),
                Esc(t.Cohort), Esc(t.CohortBasis), t.CohortSize,
                t.FanIn, Rel(t.FanInPctl, t.CohortSize), Rel(t.FanInXMedian, t.CohortSize), t.InboundRefCount,
                t.FanOut, t.FanOutEffective, Rel(t.FanOutPctl, t.CohortSize), Rel(t.FanOutXMedian, t.CohortSize),
                double.IsNaN(t.Instability) ? "" : t.Instability.ToString("0.###", CultureInfo.InvariantCulture),
                double.IsNaN(t.InstabilityRaw) ? "" : t.InstabilityRaw.ToString("0.###", CultureInfo.InvariantCulture),
                Esc(string.Join(";", t.ExternalNamespaces.OrderBy(x => x, StringComparer.Ordinal))),
                t.Cyclomatic, Rel(t.CyclomaticPctl, t.CohortSize), Rel(t.CyclomaticXMedian, t.CohortSize),
                t.MaxMemberCyclomatic, Rel(t.MaxMemberCyclomaticPctl, t.CohortSize),
                Rel(t.MaxMemberCyclomaticXMedian, t.CohortSize), Esc(t.MaxMemberName),
                t.Dsm, Rel(t.DsmPctl, t.CohortSize), Rel(t.DsmXMedian, t.CohortSize),
                t.Transform, t.StaticMutations,
                t.ParamCount, t.DataShape, Rel(t.DataShapePctl, t.CohortSize),
                N(t.GlobalFanInPctl), N(t.GlobalMaxCcPctl),
                t.MemberCount, t.Loc, Esc(t.File), t.Line, Esc(t.Id)));
        }
    }

    public static void WriteMethodsCsv(string path, IEnumerable<MethodMetrics> methods)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine(string.Join(",",
            "Method", "DeclaringType", "Project", "Cohort", "CohortSize", "Accessibility",
            "Cyclomatic", "CyclomaticPctl", "CyclomaticXMedian",
            "Dsm", "DsmPctl", "DsmXMedian", "Transform", "StaticMutations",
            "ParamCount", "MaxNesting", "Loc", "File", "Line"));

        foreach (var m in methods.OrderByDescending(m => m.CyclomaticXMedian).ThenByDescending(m => m.Cyclomatic))
        {
            w.WriteLine(string.Join(",",
                Esc(m.Name), Esc(m.DeclaringType), Esc(m.Project), Esc(m.Cohort), m.CohortSize, Esc(m.Accessibility),
                m.Cyclomatic, Rel(m.CyclomaticPctl, m.CohortSize), Rel(m.CyclomaticXMedian, m.CohortSize),
                m.Dsm, Rel(m.DsmPctl, m.CohortSize), Rel(m.DsmXMedian, m.CohortSize),
                m.Transform, m.StaticMutations,
                m.ParamCount, m.MaxNestingDepth, m.Loc, Esc(m.File), m.Line));
        }
    }

    /// <summary>
    /// The predict-then-reveal artifact. Deliberately carries NO metrics and no ordering
    /// signal — devs commit their own read on this before they see anything else.
    /// </summary>
    public static void WritePredictionSheet(string path, IEnumerable<TypeMetrics> types, Options opt)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("Cohort,Component,Namespace,Project,NervousToChange_YN,RiskRank_1to5,Notes");

        foreach (var t in types.Where(t => t.CohortSize >= opt.MinCohort)
                               .OrderBy(t => t.Cohort, StringComparer.Ordinal)
                               .ThenBy(t => t.Name, StringComparer.Ordinal))
        {
            w.WriteLine(string.Join(",",
                Esc(ShortCohort(t.Cohort)), Esc(t.Name), Esc(t.Namespace), Esc(t.Project), "", "", ""));
        }
    }

    public static void WriteEdgesCsv(string path, IEnumerable<(string From, string To, int Weight)> edges)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("From,To,Weight");
        foreach (var e in edges.OrderByDescending(e => e.Weight))
            w.WriteLine($"{Esc(e.From)},{Esc(e.To)},{e.Weight}");
    }

    // Infinity renders as "inf", never as a number. "999x the peer median" reads as a
    // measurement and sorts to the top of any spreadsheet, when all it actually means is
    // that the peer median was zero and the ratio is undefined.
    static string N(double d) =>
        double.IsNaN(d) ? "" :
        double.IsInfinity(d) ? "inf" : d.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// A relative statistic, blanked when there is nothing to be relative to. A cohort of
    /// one yields median == the value itself, so every ratio is 1.0 and every midrank
    /// percentile is 50 — the most extreme outlier in the codebase would read as exactly
    /// average. Emitting nothing is honest; emitting 1.0 is a lie that sorts well.
    /// </summary>
    static string Rel(double d, int cohortSize) => cohortSize < 2 ? "" : N(d);

    static string Esc(string s)
    {
        s ??= "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    // ------------------------------------------------- nominated instances ----

    /// <summary>
    /// Prints candidate instances per message type. These are DRAFT sentences with
    /// receipts — the point is that the metric nominates the component, not memory.
    /// Rewrite the wording by hand before showing anyone.
    /// </summary>
    public static void PrintNominations(AnalysisResult result, Options opt, TextWriter w)
    {
        var eligible = result.Types.Where(t => t.CohortSize >= opt.MinCohort).ToList();

        w.WriteLine();
        w.WriteLine("================================================================");
        w.WriteLine("NOMINATED INSTANCES  (cohorts of >= " + opt.MinCohort + " members)");
        w.WriteLine("Draft sentences. Receipts in parentheses. Rewrite before the session.");
        w.WriteLine("================================================================");

        // --- concealed decision: complexity outlier behind an ordinary interface ---
        w.WriteLine();
        w.WriteLine("-- CONCEALED DECISION ------------------------------------------");
        w.WriteLine("   (complexity far above peers, connectivity ordinary)");
        var concealed = eligible
            // A method with cc below this cannot be concealing a decision: cc 1 means one
            // linear path and literally zero decision points, so the claim contradicts
            // itself. Without the floor, a cohort of pure property bags (median 0) makes
            // any constructor with a single assignment an infinite-times outlier.
            .Where(t => t.MaxMemberCyclomatic >= opt.MinDecisionCc)
            .Where(t => t.MaxMemberCyclomaticXMedian >= opt.OutlierFactor)
            // "Mapper-shaped" = connectivity near the peer median. Ratio, not percentile:
            // in a tied cohort a fan-out of 5 against peers of 4 lands at the 93rd
            // percentile while being, in substance, identical.
            .Where(t => t.FanInXMedian <= 2.0 && t.FanOutXMedian <= 2.0)
            .OrderByDescending(t => t.MaxMemberCyclomaticXMedian)
            .Take(opt.Top)
            .ToList();
        foreach (var t in concealed)
            // "Looks like plumbing" only holds when connectivity is low in ABSOLUTE terms.
            // The filter tests connectivity relative to peers, and in a cohort where every
            // member is heavily used, "ordinary for its peers" still means widely depended
            // on — calling that plumbing is an overclaim a dev will rightly challenge.
            w.WriteLine($"   {t.Name}.{t.MaxMemberName} — " +
                        (t.FanIn < opt.MinFanIn
                            ? "looks like plumbing but is in the top "
                            : "connectivity is unremarkable for its peers, but it is in the top ") +
                        $"{Math.Max(1, Math.Round(100 - t.MaxMemberCyclomaticPctl))}% of internal complexity among your " +
                        $"{t.CohortSize} {ShortCohort(t.Cohort)}. " +
                        (double.IsInfinity(t.MaxMemberCyclomaticXMedian)
                            ? "(its peers all measure 0; cc "
                            : $"({N(t.MaxMemberCyclomaticXMedian)}x the peer median; cc ") +
                        $"{t.MaxMemberCyclomatic}, " +
                        $"dsm {t.Dsm}, fan-in {t.FanIn}, fan-out {t.FanOut})");
        if (concealed.Count == 0) w.WriteLine("   (none nominated — see NOTES in README if this is empty)");

        // --- method-level version of the same signal ---
        w.WriteLine();
        w.WriteLine("-- CONCEALED DECISION, METHOD LEVEL ----------------------------");
        var concealedMethods = result.Methods
            .Where(m => m.CohortSize >= opt.MinCohort)
            .Where(m => m.Cyclomatic >= opt.MinDecisionCc)
            .Where(m => m.CyclomaticXMedian >= opt.OutlierFactor)
            .OrderByDescending(m => m.CyclomaticXMedian)
            .Take(opt.Top);
        foreach (var m in concealedMethods)
            w.WriteLine($"   {m.DeclaringType}.{m.Name} — " +
                        (double.IsInfinity(m.CyclomaticXMedian)
                            ? "the only complexity among its "
                            : $"{N(m.CyclomaticXMedian)}x the median complexity of its ") +
                        $"{m.CohortSize} peers (cc {m.Cyclomatic}, dsm {m.Dsm}, nesting {m.MaxNestingDepth}, " +
                        $"{m.Loc} lines) — {Path.GetFileName(m.File)}:{m.Line}");

        // --- bug blast radius ---
        w.WriteLine();
        w.WriteLine("-- BUG BLAST RADIUS --------------------------------------------");
        w.WriteLine("   (widely depended on AND internally complex)");
        // Three conditions, all required. "Widely depended on" has to mean widely in
        // absolute terms too — a percentile alone will happily crown the tallest member
        // of a cohort where nothing is tall.
        var blast = eligible
            .Where(t => t.FanIn >= opt.MinFanIn)
            .Where(t => t.FanInXMedian >= 2.0)
            .Where(t => t.FanInPctl >= 95 && t.CyclomaticPctl >= 70)
            .OrderByDescending(t => t.FanIn)
            .Take(opt.Top);
        foreach (var t in blast)
            w.WriteLine($"   {t.Name} — {Plural(t.FanIn, "distinct caller")} ({N(t.FanInXMedian)}x its peer median) and " +
                        $"internally complex. A bug here propagates widely. " +
                        $"(cc {t.Cyclomatic}, fan-out {t.FanOut}, {Plural(t.InboundRefCount, "call site")})");

        // --- change cost ---
        w.WriteLine();
        w.WriteLine("-- CHANGE COST -------------------------------------------------");
        w.WriteLine("   (many internal callers on a contract-shaped type)");
        var changeCost = result.Types
            .Where(t => t.Kind is "Contract" or "ApiBoundary")
            .Where(t => t.FanIn >= opt.MinCohort)
            .OrderByDescending(t => t.FanIn)
            .Take(opt.Top);
        foreach (var t in changeCost)
            w.WriteLine($"   {t.Name} — {Plural(t.FanIn, "internal caller")} depend on this contract " +
                        $"({t.DataShape} fields/params of surface). Changing it is a distributed edit, " +
                        $"not a local one. EXTERNAL consumers are not visible to this probe. ({t.Kind})");

        PrintBoundaries(result, opt, w);

        // --- cohort-free: instability is a ratio, so singletons are covered too ---
        w.WriteLine();
        w.WriteLine("-- LOAD-BEARING AND INTRICATE (no cohort required) -------------");
        w.WriteLine($"   (instability <= {opt.StableThreshold:0.##} — much depends on it, it depends on");
        w.WriteLine($"    little — AND a method above cc {opt.HighCc})");
        var loadBearing = result.Types
            .Where(t => !double.IsNaN(t.Instability) && t.Instability <= opt.StableThreshold)
            .Where(t => t.FanIn >= opt.MinFanIn)                 // ratio hides magnitude
            .Where(t => t.MaxMemberCyclomatic >= opt.HighCc)
            .OrderBy(t => t.Instability).ThenByDescending(t => t.FanIn)
            .Take(opt.Top)
            .ToList();
        foreach (var t in loadBearing)
        {
            var dependsOn = t.FanOutEffective == 0
                ? (t.FanOut == 0 ? "nothing" : $"nothing concrete ({t.FanOut} abstractions/contracts)")
                : t.FanOutEffective == t.FanOut
                    ? $"{t.FanOutEffective}"
                    : $"{t.FanOutEffective} concrete types ({t.FanOut} total)";
            w.WriteLine($"   {t.Name} — instability {t.Instability:0.###}: " +
                        $"{Plural(t.FanIn, "type")} depend on it, it depends on {dependsOn}. " +
                        $"And {t.MaxMemberName} is cc {t.MaxMemberCyclomatic}. Hard to change safely, " +
                        $"and intricate enough to hide a bug.");
        }
        if (loadBearing.Count == 0) w.WriteLine("   (none)");

        w.WriteLine();
        w.WriteLine("-- BREAKS ALONE (no cohort required) ---------------------------");
        w.WriteLine($"   (complex, but almost nothing depends on it — the reassuring message)");
        var concealedIds = new HashSet<string>(concealed.Select(t => t.Id), StringComparer.Ordinal);
        var breaksAlone = result.Types
            // Never say this about anything on the outside edge: the probe cannot see
            // external consumers, and "safe to change" is the one claim it must not get
            // wrong at a boundary.
            .Where(t => t.Kind is not ("ApiBoundary" or "ExternalCall" or "Contract"))
            // Nor about anything already flagged as a concealed decision. Structural
            // isolation is not safety when the component decides something: a normalizer
            // that picks the wrong option doesn't propagate through the call graph, it
            // propagates into the data going out the door. Saying "breaks alone" and
            // "this is making business judgements" about the same type discredits both.
            .Where(t => !concealedIds.Contains(t.Id))
            // Fan-in of zero isn't reassurance, it's unreferenced code — a different
            // finding. Review those in types.csv rather than reading them as safe.
            .Where(t => t.FanIn >= 1)
            .Where(t => !double.IsNaN(t.Instability) && t.Instability >= 0.8)
            .Where(t => t.MaxMemberCyclomatic >= opt.HighCc)
            .OrderByDescending(t => t.MaxMemberCyclomatic)
            .Take(opt.Top)
            .ToList();
        foreach (var t in breaksAlone)
            w.WriteLine($"   {t.Name} — instability {t.Instability:0.###}: only {Plural(t.FanIn, "type")} " +
                        $"{(t.FanIn == 1 ? "depends" : "depend")} on it. Complex inside " +
                        $"(cc {t.MaxMemberCyclomatic}) but isolated — if it breaks, it breaks alone.");
        if (breaksAlone.Count == 0) w.WriteLine("   (none)");

        // --- both magnitudes high at once: hub or god object ---
        w.WriteLine();
        w.WriteLine("-- HUBS AND GOD OBJECTS (no cohort required) -------------------");
        w.WriteLine($"   (fan-in AND fan-out both >= {opt.HubMin} — a ratio cannot see these, since");
        w.WriteLine("    high-in + high-out lands mid-range, same as a trivial one-in one-out leaf)");
        var hubs = result.Types
            .Where(t => Math.Min(t.FanIn, t.FanOut) >= opt.HubMin)
            .OrderByDescending(t => Math.Min(t.FanIn, t.FanOut))
            .Take(opt.Top)
            .ToList();
        foreach (var t in hubs)
        {
            // The internal dimension separates two genuinely different dangers: wiring
            // that is risky to re-route, versus logic that is risky to get wrong.
            var isGodObject = t.MaxMemberCyclomatic >= opt.HighCc || t.MemberCount >= opt.GodObjectMembers;
            var verdict = isGodObject
                ? $"Architectural bottleneck: it both depends on and is depended on by much of " +
                  $"the system, AND carries real logic ({t.MemberCount} members, worst method " +
                  $"{t.MaxMemberName} at cc {t.MaxMemberCyclomatic}, dsm {t.Dsm}). Cross-domain " +
                  $"orchestration and shared state tend to collect here."
                : $"Wiring hub: high coupling both ways but little logic inside " +
                  $"(worst method cc {t.MaxMemberCyclomatic}). Risky to re-route, not to reason about.";
            w.WriteLine($"   {t.Name} [{t.Kind}] — fan-in {t.FanIn}, fan-out {t.FanOut}, " +
                        $"instability {t.Instability:0.###}. {verdict}");
        }
        if (hubs.Count == 0) w.WriteLine("   (none)");
        if (hubs.Count > 0)
        {
            w.WriteLine("   NOTE: routers, mediators and composition roots legitimately live here. That");
            w.WriteLine("   does not make the flag wrong — those are exactly the things not to change");
            w.WriteLine("   lightly. Mark the known ones rather than tuning them away.");
        }

        PrintLayerSpan(result, opt, w);
        PrintCycles(result, opt, w);

        // --- shared mutable state: the one sharing static analysis is sure about ---
        w.WriteLine();
        w.WriteLine("-- SHARED MUTABLE STATE (no cohort required) -------------------");
        w.WriteLine("   (writes to static mutable state — every caller on every thread shares these)");
        var staticMutators = result.Types
            .Where(t => t.StaticMutations > 0)
            .OrderByDescending(t => t.StaticMutations)
            .Take(opt.Top)
            .ToList();
        foreach (var t in staticMutators)
            w.WriteLine($"   {t.Name} — {Plural(t.StaticMutations, "write")} to static state, " +
                        $"and {Plural(t.FanIn, "type")} call into it. Whether these are genuinely " +
                        $"contended is a runtime question this probe cannot answer — but the sharing " +
                        $"is certain from the code.");
        if (staticMutators.Count == 0) w.WriteLine("   (none)");

        PrintProjectInstability(result, w);

        // --- coverage: what got no comparative reading at all ---
        var orphans = result.Types.Where(t => t.CohortSize < opt.MinCohort).ToList();
        w.WriteLine();
        w.WriteLine("-- NO PEER GROUP -----------------------------------------------");
        w.WriteLine($"   {orphans.Count} of {result.Types.Count} types " +
                    $"({(result.Types.Count == 0 ? 0 : 100.0 * orphans.Count / result.Types.Count):0.#}%) " +
                    $"sit in cohorts below --min-cohort ({opt.MinCohort}).");
        w.WriteLine("   No PEER comparison was possible for these. They are absent from the");
        w.WriteLine("   nominations above and from prediction-sheet.csv, and their Pctl/XMedian");
        w.WriteLine("   columns are blank where a cohort of one made them meaningless.");
        if (orphans.Count > 0)
        {
            // A weaker claim, but a real one: no peers to compare against, so compare
            // against the whole solution instead and say plainly that that is what
            // happened. A lone DbContext or a pair of repositories are often the most
            // central things in a system — going silent on them is not an option.
            // Only ever state the dimension that actually qualifies. In a codebase where
            // most types have no callers at all, a fan-in of zero lands at a high midrank
            // percentile — "top 86% by fan-in, 0 callers" is both absurd and corrosive.
            var globalOutliers = orphans
                .Select(t => (Type: t, Claims: GlobalClaims(t, opt)))
                .Where(x => x.Claims.Count > 0)
                .OrderByDescending(x => Math.Max(x.Type.GlobalFanInPctl, x.Type.GlobalMaxCcPctl))
                .Take(opt.Top)
                .ToList();

            if (globalOutliers.Count > 0)
            {
                w.WriteLine();
                w.WriteLine("   Extreme against the WHOLE SOLUTION despite having no peer group.");
                w.WriteLine("   Weaker evidence — this compares unlike things — but not nothing:");
                foreach (var (t, claims) in globalOutliers)
                    w.WriteLine($"     {t.Name} [{t.Kind}] — {string.Join(" and ", claims)}, " +
                                $"solution-wide. (no cohort to compare against)");
            }

            w.WriteLine();
            w.WriteLine("   All types with no usable peer group, by fan-in:");
            foreach (var t in orphans.OrderByDescending(t => t.FanIn).Take(opt.Top))
                w.WriteLine($"     {t.Name} — fan-in {t.FanIn}, cc {t.Cyclomatic}, " +
                            $"cohort '{ShortCohort(t.Cohort)}' (size {t.CohortSize})");

            w.WriteLine();
            w.WriteLine("   NOTE: a type with no peers still has its own METHODS as a cohort —");
            w.WriteLine("   check methods.csv for it. And its real comparison is its own history,");
            w.WriteLine("   which is the temporal signal a single snapshot cannot give you.");
        }

        PrintDrift(result, opt, w);
        w.WriteLine();
    }

    /// <summary>
    /// Instability at the assembly level, which is where Martin defined it and where it
    /// maps to something operational: projects are deployment units, so "what depends on
    /// this" is also "what ships when this changes".
    /// </summary>
    static void PrintProjectInstability(AnalysisResult result, TextWriter w)
    {
        var projectOf = result.Types.ToDictionary(t => t.Id, t => t.Project, StringComparer.Ordinal);

        var afferent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var efferent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var p in result.Types.Select(t => t.Project).Distinct(StringComparer.Ordinal))
        {
            afferent[p] = new HashSet<string>(StringComparer.Ordinal);
            efferent[p] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, to, _) in result.Edges)
        {
            if (!projectOf.TryGetValue(from, out var pFrom)) continue;
            if (!projectOf.TryGetValue(to, out var pTo)) continue;
            if (string.Equals(pFrom, pTo, StringComparison.Ordinal)) continue;
            efferent[pFrom].Add(from);   // types here that reach out
            afferent[pTo].Add(from);     // types elsewhere that reach in
        }

        w.WriteLine();
        w.WriteLine("-- PROJECT STABILITY vs ABSTRACTNESS ---------------------------");
        w.WriteLine("   I = Ce/(Ce+Ca), low = much depends on it. A = share of types that are");
        w.WriteLine("   abstract or interfaces. D = |A + I - 1|, distance from the main sequence.");
        w.WriteLine("   Stable AND concrete is the zone of pain: hard to change, hard to extend.");

        foreach (var p in afferent.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var ca = afferent[p].Count;
            var ce = efferent[p].Count;
            var types = result.Types.Where(t => string.Equals(t.Project, p, StringComparison.Ordinal)).ToList();
            var abstractCount = types.Count(t => t.IsAbstract || t.TypeKeyword == "Interface");
            var a = types.Count == 0 ? 0 : (double)abstractCount / types.Count;

            if (ca + ce == 0)
            {
                w.WriteLine($"     {p} — no cross-project coupling; A {a:0.##}");
                continue;
            }

            var i = (double)ce / (ca + ce);
            var d = Math.Abs(a + i - 1);
            var zone = i <= 0.3 && a <= 0.3 ? "  <-- ZONE OF PAIN (stable and concrete)"
                     : i >= 0.7 && a >= 0.7 ? "  <-- zone of uselessness (abstract, unused)"
                     : d <= 0.3 ? "  (near the main sequence)"
                     : "";
            w.WriteLine($"     {p} — I {i:0.##}, A {a:0.##}, D {d:0.##}  " +
                        $"(Ca {ca} depend on it, Ce {ce} reach out, {abstractCount}/{types.Count} abstract){zone}");
        }

        PrintDeadProjects(result, afferent.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal), w);
    }

    // Architecturally significant kinds. Contract is excluded because nearly everything
    // touches DTOs — counting it would put every type in the report. Internal is excluded
    // because it is the catch-all: depending on ordinary code is not cross-cutting.
    static readonly string[] SignificantKinds = { "ApiBoundary", "DataAccess", "ExternalCall" };

    /// <summary>
    /// Components whose dependencies span architectural kinds. A thing named for one
    /// narrow concern that reaches across several is doing cross-cutting work whatever it
    /// is called — an "authentication middleware" that also reaches into customer lookup
    /// and an audit service is a gateway policy engine wearing an auth name.
    ///
    /// The tool cannot know what a component is FOR. It can see that its dependencies
    /// don't match a single concern, and name them so a human can judge.
    /// </summary>
    static void PrintLayerSpan(AnalysisResult result, Options opt, TextWriter w)
    {
        var byId = result.Types.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

        var spanning = new List<(TypeMetrics Type, SortedSet<string> Kinds, Dictionary<string, List<string>> Deps)>();
        foreach (var t in result.Types)
        {
            var kinds = new SortedSet<string>(StringComparer.Ordinal);
            var deps = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            // Its own position counts: a boundary component that also does data access is
            // spanning layers even if that is its only significant dependency.
            if (SignificantKinds.Contains(t.Kind)) kinds.Add(t.Kind);

            foreach (var id in t.OutboundTypes)
            {
                if (!byId.TryGetValue(id, out var dep)) continue;
                if (!SignificantKinds.Contains(dep.Kind)) continue;
                kinds.Add(dep.Kind);
                if (!deps.TryGetValue(dep.Kind, out var list)) deps[dep.Kind] = list = new List<string>();
                list.Add(dep.Name);
            }

            if (kinds.Count >= opt.MinKindSpan) spanning.Add((t, kinds, deps));
        }

        w.WriteLine();
        w.WriteLine("-- SPANS ARCHITECTURAL LAYERS (no cohort required) -------------");
        w.WriteLine($"   (dependencies reaching across {opt.MinKindSpan}+ architectural kinds — cross-cutting");
        w.WriteLine("    work, whatever the component is named)");

        if (spanning.Count == 0)
        {
            w.WriteLine("   (none)");
            return;
        }

        // Same discipline as everywhere else: repeated across many types it is a layering
        // PATTERN and belongs in one line; rare, it is an anomaly and deserves the detail.
        foreach (var group in spanning
                     .GroupBy(x => string.Join("+", x.Kinds), StringComparer.Ordinal)
                     .OrderBy(g => g.Count()))
        {
            var members = group.OrderByDescending(x => x.Type.FanIn).ToList();

            if (members.Count > opt.Top / 3)
            {
                w.WriteLine($"   {members.Count} types span {group.Key} — a layering pattern rather than an");
                w.WriteLine($"     anomaly. Examples: {string.Join(", ", members.Take(4).Select(x => x.Type.Name))}" +
                            (members.Count > 4 ? ", ..." : ""));
                continue;
            }

            foreach (var (type, kinds, deps) in members)
            {
                w.WriteLine($"   {type.Name} [{type.Kind}] — reaches across {kinds.Count} kinds:");
                foreach (var kind in kinds)
                {
                    var names = deps.TryGetValue(kind, out var list)
                        ? string.Join(", ", list.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).Take(6))
                        : "itself";
                    w.WriteLine($"       {kind,-14} {names}");
                }
                w.WriteLine("       Check that the name still describes what it does.");
            }
        }
    }

    // Language and framework plumbing. Present in nearly every file, so listing them in an
    // integration map buries the three lines that actually matter under System.Linq.
    static readonly string[] PlumbingNamespaces =
    {
        "System.Collections", "System.Linq", "System.Text", "System.Threading",
        "System.Runtime", "System.Reflection", "System.Globalization", "System.ComponentModel",
        "System.Diagnostics", "System.Numerics", "Microsoft.Extensions", "Microsoft.CSharp",
    };

    static bool IsPlumbing(string ns) =>
        ns == "System" || PlumbingNamespaces.Any(p => ns.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// Boundaries, as a map and a shortlist rather than a roll-call.
    ///
    /// Enumerating every controller is not a finding — it fires on 100% of a category the
    /// reader already knows about, and a flag that never discriminates is one people learn
    /// to skip. What is worth saying: which external systems this codebase actually
    /// touches and how widely, and which individual boundaries are unusual.
    /// </summary>
    static void PrintBoundaries(AnalysisResult result, Options opt, TextWriter w)
    {
        var boundaries = result.Types.Where(t => t.Kind is "ApiBoundary" or "ExternalCall").ToList();

        w.WriteLine();
        w.WriteLine("-- BOUNDARY: HERE BE DRAGONS -----------------------------------");
        w.WriteLine($"   {boundaries.Count} external contact point(s): " +
                    $"{boundaries.Count(t => t.Kind == "ApiBoundary")} inbound API, " +
                    $"{boundaries.Count(t => t.Kind == "ExternalCall")} outbound. " +
                    "Consumer impact of");
        w.WriteLine("   changes at ANY of these is outside what static analysis can see.");

        // --- integration map: what this codebase talks to, and how widely ---
        var touches = new Dictionary<string, int>(StringComparer.Ordinal);
        var plumbing = 0;
        foreach (var t in result.Types)
            foreach (var ns in t.ExternalNamespaces)
            {
                if (IsPlumbing(ns)) { plumbing++; continue; }
                touches[ns] = touches.TryGetValue(ns, out var n) ? n + 1 : 1;
            }

        w.WriteLine();
        w.WriteLine("   INTEGRATION MAP — external systems, by how many types touch them:");
        if (touches.Count == 0)
            w.WriteLine("     (none detected outside language/framework plumbing)");
        foreach (var (ns, count) in touches.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(opt.Top))
            w.WriteLine($"     {ns,-42} {Plural(count, "type")}");
        if (plumbing > 0)
            w.WriteLine($"     ({plumbing} language/runtime references omitted as plumbing)");

        // --- the boundaries that are actually unusual ---
        var withLogic = boundaries
            .Where(t => t.MaxMemberCyclomatic >= opt.HighCc)
            .OrderByDescending(t => t.MaxMemberCyclomatic)
            .Take(opt.Top)
            .ToList();

        w.WriteLine();
        w.WriteLine("   BOUNDARIES CARRYING REAL LOGIC — decisions made at the edge:");
        if (withLogic.Count == 0) w.WriteLine("     (none — logic lives behind the boundary, which is what you want)");
        foreach (var t in withLogic)
            w.WriteLine($"     {t.Name} — {t.MaxMemberName} is cc {t.MaxMemberCyclomatic}. " +
                        $"Business decisions at an external edge are the hardest kind to change later.");

        // Only worth saying if it distinguishes. Four controllers tied at the same surface
        // is the roll-call problem again in miniature: the list is longer, the reader
        // learns nothing, and the section stops being read at all.
        var surfaceMedian = Median(boundaries.Select(t => (double)t.DataShape).ToArray());
        var bigSurface = boundaries
            .Where(t => t.DataShape >= Math.Max(surfaceMedian * 1.5, 1))
            .OrderByDescending(t => t.DataShape)
            .Take(5)
            .ToList();

        if (bigSurface.Count > 0 && bigSurface.Count <= Math.Max(1, boundaries.Count / 2))
        {
            w.WriteLine();
            w.WriteLine("   WIDEST CONTRACT SURFACE — most to get wrong, most to break:");
            foreach (var t in bigSurface)
                w.WriteLine($"     {t.Name} — {t.DataShape} fields/params across {Plural(t.PublicMemberCount, "public member")}.");
        }
    }

    /// <summary>
    /// Drift against an earlier run. The whole intelligence here is knowing when NOT to
    /// fire: growth is normal, and a tool that announces every delta gets muted inside a
    /// sprint, taking the one alert that mattered with it.
    ///
    /// So the headline message is not "this grew" — it is "this CROSSED", from
    /// unremarkable into the top tier of the codebase. That is self-calibrating, it is the
    /// creep nobody decides, and a service that was always critical or that merely gained
    /// a couple of callers never triggers it.
    /// </summary>
    static void PrintDrift(AnalysisResult result, Options opt, TextWriter w)
    {
        w.WriteLine();
        w.WriteLine("-- CRITICALITY DRIFT -------------------------------------------");

        if (result.BaselineRows == null)
        {
            w.WriteLine("   No baseline given. Archive this run's types.csv and pass it as");
            w.WriteLine("   --baseline next time, or check out an older commit and run there:");
            w.WriteLine("   git history means you can backfill this today, not in six months.");
            return;
        }

        var baseline = result.BaselineRows;
        var matched = new List<(TypeMetrics Now, BaselineRow Was)>();
        var appeared = new List<TypeMetrics>();

        foreach (var t in result.Types)
        {
            if (baseline.TryGetValue(t.Id, out var was)) matched.Add((t, was));
            else appeared.Add(t);
        }

        var currentIds = new HashSet<string>(result.Types.Select(t => t.Id), StringComparer.Ordinal);
        var disappeared = baseline.Values.Where(b => !currentIds.Contains(b.Id)).ToList();

        w.WriteLine($"   {matched.Count} type(s) present in both runs, {appeared.Count} new, " +
                    $"{disappeared.Count} gone.");

        // --- the money message: crossed from unremarkable into the top tier ---
        // Arrival AND movement, rather than a hard percentile cutoff. A fixed "crossed 90"
        // line is brittle: percentiles are midrank, so ties compress them, and the whole
        // distribution shifts as the codebase grows — a component that went from 1 caller
        // to 7 landed at 88.8 and would have been missed by a point. What the message
        // actually claims is "climbed a long way and ended up high", so test both.
        var crossed = matched
            .Where(x => x.Now.FanIn >= opt.MinFanIn)
            .Where(x => x.Now.FanIn - x.Was.FanIn >= opt.MinDriftDelta)
            .Where(x => x.Now.GlobalFanInPctl >= 80)
            .Where(x => x.Now.GlobalFanInPctl - x.Was.GlobalFanInPctl >= 20
                        || x.Now.FanIn >= x.Was.FanIn * 2)
            .OrderByDescending(x => x.Now.FanIn - x.Was.FanIn)
            .Take(opt.Top)
            .ToList();

        w.WriteLine();
        w.WriteLine("   CREPT INTO CRITICALITY — was unremarkable, now load-bearing:");
        if (crossed.Count == 0)
            w.WriteLine("     (none — nothing crossed from the ordinary majority into the top tier)");
        foreach (var (now, was) in crossed)
            w.WriteLine($"     {now.Name} — callers {was.FanIn} -> {now.FanIn}. Was in the bottom " +
                        $"{Math.Round(was.GlobalFanInPctl)}% of your codebase by fan-in, now the top " +
                        $"{Math.Max(1, Math.Round(100 - now.GlobalFanInPctl))}%. Nobody decided this in one PR.");

        // --- a component starting to touch a layer it never touched ---
        var reached = matched
            .Where(x => x.Was.HasKindSpan)
            .Select(x => (x.Now, x.Was, New: KindsAdded(x.Was.KindSpan, x.Now.KindSpan)))
            .Where(x => x.New.Count > 0)
            .OrderByDescending(x => x.Now.FanIn)
            .Take(opt.Top)
            .ToList();

        w.WriteLine();
        w.WriteLine("   NEW ARCHITECTURAL REACH — started touching a layer it did not before:");
        if (!matched.Any(x => x.Was.HasKindSpan))
            w.WriteLine("     (baseline predates the KindSpan column — re-baseline to enable this)");
        else if (reached.Count == 0)
            w.WriteLine("     (none)");
        // Six normalizers all gaining ExternalCall in one window is ONE decision applied
        // across a layer, not six findings. Same collapse as everywhere else.
        foreach (var group in reached
                     .GroupBy(x => string.Join("+", x.New), StringComparer.Ordinal)
                     .OrderBy(g => g.Count()))
        {
            var members = group.ToList();
            if (members.Count > 2)
            {
                w.WriteLine($"     {members.Count} types started reaching {group.Key} — one decision applied");
                w.WriteLine($"       across a layer, not {members.Count} separate events. " +
                            $"{string.Join(", ", members.Take(4).Select(x => x.Now.Name))}" +
                            (members.Count > 4 ? ", ..." : ""));
                continue;
            }

            foreach (var (now, was, added) in members)
                w.WriteLine($"     {now.Name} — now reaches {string.Join(" and ", added)} " +
                            $"(was {(string.IsNullOrEmpty(was.KindSpan) ? "no significant kinds" : was.KindSpan)}). " +
                            $"An architectural event, not gradual drift — someone did this deliberately.");
        }

        // --- internal volatility growth, gated on the thing mattering at all ---
        var grewComplex = matched
            .Where(x => x.Now.FanIn >= opt.MinFanIn)
            .Where(x => x.Now.MaxMemberCyclomatic >= opt.HighCc)
            .Where(x => x.Now.MaxMemberCyclomatic - x.Was.MaxMemberCyclomatic >= opt.MinDriftDelta)
            .OrderByDescending(x => x.Now.MaxMemberCyclomatic - x.Was.MaxMemberCyclomatic)
            .Take(opt.Top)
            .ToList();

        w.WriteLine();
        w.WriteLine("   GREW MORE INTRICATE — and enough depends on it to matter:");
        if (grewComplex.Count == 0) w.WriteLine("     (none)");
        foreach (var (now, was) in grewComplex)
            w.WriteLine($"     {now.Name}.{now.MaxMemberName} — cc {was.MaxMemberCyclomatic} -> " +
                        $"{now.MaxMemberCyclomatic}, with {Plural(now.FanIn, "caller")}.");

        // --- structural events, surfaced rather than silently reconciled ---
        // A lower bar than drift: appearing and vanishing are rare and consequential, so
        // anything that had or has a caller at all is worth a line.
        var appearedShown = appeared.Where(t => t.FanIn >= 1)
                                    .OrderByDescending(t => t.FanIn).Take(opt.Top).ToList();
        var goneShown = disappeared.Where(b => b.FanIn >= 1)
                                   .OrderByDescending(b => b.FanIn).Take(opt.Top).ToList();

        if (appearedShown.Count > 0 || goneShown.Count > 0)
        {
            w.WriteLine();
            w.WriteLine("   STRUCTURAL EVENTS — renames, splits and extractions land here:");
            w.WriteLine("     A type is matched by fully-qualified name, so a rename reads as one");
            w.WriteLine("     disappearance plus one appearance. That is deliberate: a refactor is");
            w.WriteLine("     an event worth seeing, not continuity to fake.");

            foreach (var t in appearedShown)
                w.WriteLine($"     + {t.Name} [{t.Kind}] — new, and already has {Plural(t.FanIn, "caller")}.");

            foreach (var b in goneShown)
                w.WriteLine($"     - {b.Name} [{b.Kind}] — gone; had {Plural(b.FanIn, "caller")}.");
        }
    }

    /// <summary>
    /// Circular references, at the two levels where they mean something.
    ///
    /// Namespace cycles are the architectural finding: namespaces are how a .NET codebase
    /// expresses its layering, so a cycle between them is a layering violation you can
    /// point at. Type-level cycles are reported only as large tangles — mutual pairs are
    /// ordinary C# (parent/child, visitor/visited) and listing them would bury the signal.
    /// Project-level cycles are not checked: MSBuild cannot build one.
    /// </summary>
    static void PrintCycles(AnalysisResult result, Options opt, TextWriter w)
    {
        var byId = result.Types.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

        // --- namespace graph ---
        var nsEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in result.Types)
        {
            var from = string.IsNullOrEmpty(t.Namespace) ? "<global>" : t.Namespace;
            if (!nsEdges.TryGetValue(from, out var set))
                nsEdges[from] = set = new HashSet<string>(StringComparer.Ordinal);

            foreach (var id in t.OutboundTypes)
            {
                if (!byId.TryGetValue(id, out var dep)) continue;
                var to = string.IsNullOrEmpty(dep.Namespace) ? "<global>" : dep.Namespace;
                if (!string.Equals(from, to, StringComparison.Ordinal)) set.Add(to);
            }
        }

        var nsAdjacency = nsEdges.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
        var nsCycles = Graphs.StronglyConnected(nsAdjacency, 2);

        w.WriteLine();
        w.WriteLine("-- CIRCULAR REFERENCES -----------------------------------------");
        w.WriteLine("   NAMESPACE CYCLES — mutually dependent namespaces cannot be layered,");
        w.WriteLine("   understood, or extracted independently:");
        if (nsCycles.Count == 0)
            w.WriteLine("     (none)");
        foreach (var cycle in nsCycles.OrderByDescending(c => c.Count).Take(opt.Top))
            w.WriteLine($"     {cycle.Count} namespaces: " +
                        string.Join(" <-> ", cycle.OrderBy(n => n, StringComparer.Ordinal).Take(6)) +
                        (cycle.Count > 6 ? ", ..." : ""));

        // --- type graph, large tangles only ---
        var typeAdjacency = result.Types.ToDictionary(
            t => t.Id,
            t => t.OutboundTypes.Where(byId.ContainsKey).ToList(),
            StringComparer.Ordinal);

        var tangles = Graphs.StronglyConnected(typeAdjacency, opt.MinTangle);

        w.WriteLine();
        w.WriteLine($"   TYPE TANGLES — {opt.MinTangle}+ types that all reach each other, so none of");
        w.WriteLine("   them can be tested or changed in isolation:");
        if (tangles.Count == 0)
            w.WriteLine($"     (none — mutual pairs and triples are ordinary and not reported)");
        foreach (var tangle in tangles.OrderByDescending(t => t.Count).Take(opt.Top))
        {
            var names = tangle.Select(id => byId[id].Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            w.WriteLine($"     {names.Count} types: {string.Join(", ", names.Take(8))}" +
                        (names.Count > 8 ? ", ..." : ""));
        }
    }

    /// <summary>
    /// Projects nothing depends on. A root is not dead, so hosts and entry points are
    /// excluded — and because test projects are skipped by default, a library used only by
    /// tests would otherwise read as unreferenced.
    /// </summary>
    static void PrintDeadProjects(AnalysisResult result, IReadOnlyDictionary<string, int> afferent, TextWriter w)
    {
        var apiProjects = new HashSet<string>(
            result.Types.Where(t => t.Kind == "ApiBoundary").Select(t => t.Project),
            StringComparer.Ordinal);

        var dead = result.Projects
            .Where(p => afferent.TryGetValue(p.Name, out var ca) && ca == 0)
            .Where(p => !p.HasEntryPoint)          // a Main makes it a root
            .Where(p => p.IsLibrary)               // an exe is a root by definition
            .Where(p => !apiProjects.Contains(p.Name))  // a web host is a root too
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        w.WriteLine();
        w.WriteLine("   UNREFERENCED PROJECTS — no other project depends on these:");
        if (dead.Count == 0)
        {
            w.WriteLine("     (none)");
            return;
        }
        foreach (var name in dead) w.WriteLine($"     {name}");
        w.WriteLine("     Entry points, executables and API hosts are excluded — a root is not dead.");
        w.WriteLine("     But test projects are skipped by default, so anything used ONLY by tests");
        w.WriteLine("     appears here. Verify before deleting.");
    }

    static List<string> KindsAdded(string before, string after)
    {
        var had = new HashSet<string>((before ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries),
                                      StringComparer.Ordinal);
        return (after ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)
                            .Where(k => !had.Contains(k))
                            .ToList();
    }

    static string Plural(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>A percentile as a "top N%" phrase, floored at 1%.</summary>
    static string Pct(double percentile) => $"{Math.Max(1, Math.Round(100 - percentile)):0}%";

    /// <summary>
    /// Solution-wide claims that survive an absolute sanity check. A percentile alone will
    /// happily rank the tallest of a field of zeroes.
    /// </summary>
    static List<string> GlobalClaims(TypeMetrics t, Options opt)
    {
        var claims = new List<string>();

        if (t.GlobalFanInPctl >= 90 && t.FanIn >= opt.MinFanIn)
            claims.Add($"top {Pct(t.GlobalFanInPctl)} by fan-in ({Plural(t.FanIn, "caller")})");

        if (t.GlobalMaxCcPctl >= 90 && t.MaxMemberCyclomatic > 1)
            claims.Add($"top {Pct(t.GlobalMaxCcPctl)} by complexity " +
                       $"(cc {t.MaxMemberCyclomatic} in {t.MaxMemberName})");

        return claims;
    }

    static string ShortCohort(string cohort)
    {
        var idx = cohort.IndexOf(':');
        var rest = idx >= 0 ? cohort.Substring(idx + 1) : cohort;
        var lastDot = rest.LastIndexOf('.');
        return lastDot >= 0 ? rest.Substring(lastDot + 1) : rest;
    }
}

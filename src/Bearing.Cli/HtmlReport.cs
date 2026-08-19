using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The shareable artifact: one self-contained HTML file — <c>TECHREQ-job-a.md</c> §6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Orientation, then findings, then drill-down</b>, which is the order §6 asks for and is the
/// opposite of the terminal report's. That is deliberate rather than an inconsistency: the
/// terminal is read by somebody who already ran the tool on purpose and wants the answer without
/// scrolling (<c>PRD-free-tier.md</c> §7.3), and this is opened by somebody who was sent a link and
/// does not yet know what system they are looking at. Leading a newcomer with nominations is
/// leading with claims about components they cannot place.
/// </para>
/// <para>
/// <b>No external requests and no script.</b> Everything is inlined; collapsing is
/// <c>&lt;details&gt;</c> rather than JavaScript, so the page prints, works with script disabled,
/// and gives a corporate proxy nothing to block. See <see cref="HtmlStyle"/>.
/// </para>
/// <para>
/// <b>The drill-down covers components a finding names, not every type.</b> nopCommerce has 3,209
/// types and a row each would make the artifact several megabytes — and §6 makes bundle size a
/// real budget. The bound is the finding set rather than a cap, so it scales with what there is to
/// say rather than with the size of the codebase, and the section says what it left out and where
/// the rest is. A silent subset would be <c>docs/DEFECTS.md</c> §3 again in a new medium.
/// </para>
/// <para>
/// <b>What building this said about the finding record</b> — which is the job §6 assigns it —
/// is recorded at <see cref="Claim"/> and in <c>docs/ARCHITECTURE.md</c> §4.
/// </para>
/// </remarks>
public static class HtmlReport
{
    /// <summary>Renders the whole report as one HTML document.</summary>
    /// <param name="model">The analysed solution.</param>
    /// <param name="findings">Its findings, already suppressed.</param>
    /// <param name="generatedAt">
    /// When the run happened — a parameter, not a clock read, for the reason
    /// <see cref="JsonOutput.Render"/> gives.
    /// </param>
    public static string Render(
        SolutionModel model, FindingSet findings, DateTimeOffset generatedAt, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var page = new StringBuilder();
        var solution = Path.GetFileName(model.SolutionPath);

        page.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n");
        page.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        page.Append($"<title>Bearing — {Html.Text(solution)}</title>\n");
        page.Append("<style>\n").Append(HtmlStyle.Css).Append("\n</style>\n</head>\n<body>\n<div class=\"wrap\">\n");

        Header(page, model, solution, findings, generatedAt);
        Picture(page, model, findings);
        Risks(page, model, findings);
        Orientation(page, model);

        // Tier 4. The enumeration is the artifact A11 round 1 called "a wall of text", and every
        // row of it is still reachable — in --json, in --csv, and here behind a flag for CI and for
        // whoever wants it. `PRD-free-tier.md` §9's anti-metric is that more findings is worse.
        if (full)
        {
            Findings(page, model, findings);
            DrillDown(page, model, findings);
        }
        else
        {
            Everything(page, model, findings);
        }

        Footer(page, model);

        page.Append("</div>\n</body>\n</html>\n");
        return page.ToString();
    }

    /// <summary>Renders the report and writes it to <paramref name="path"/>.</summary>
    public static void Write(
        string path, SolutionModel model, FindingSet findings, DateTimeOffset generatedAt, bool full = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, Render(model, findings, generatedAt, full), new UTF8Encoding(false));
    }

    // ------------------------------------------------------------------------ header ----

    private static void Header(
        StringBuilder page, SolutionModel model, string solution, FindingSet findings, DateTimeOffset at)
    {
        page.Append($"<h1>{Html.Text(solution)}</h1>\n");
        page.Append($"<p class=\"sub\">Bearing {Html.Text(model.ToolVersion)} · ");
        page.Append($"{Html.Text(at.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture))}</p>\n");

        // The census, demoted to a line of prose — A13 tier 3. These are the three numbers the tile
        // row used to carry, and they are worth stating once: they are the denominator every claim
        // below is measured against. What they are not is a headline, because a reader learns
        // nothing from them they could act on.
        page.Append($"<p class=\"sub\">{Html.Count(model.Types.Count)} ");
        page.Append($"{Sentences.Do(model.Types.Count, "type", "types")} in {Html.Count(model.Projects.Count)} ");
        page.Append($"{Sentences.Do(model.Projects.Count, "project", "projects")}, {Html.Count(model.Edges.Count)} ");
        page.Append($"{Sentences.Do(model.Edges.Count, "dependency", "dependencies")} between them.</p>\n");

        page.Append("<p class=\"lede\">A map of this solution and a short list of the components that are ");
        page.Append("unusual <em>for what they are</em> — measured against their structural peers, never scored. ");
        page.Append("Start with the picture; the findings are further down and they assume it.</p>\n");

        var tiles = Tiles.For(model, findings);
        if (tiles.Count == 0) return;

        page.Append("<div class=\"tiles\">\n");
        foreach (var tile in tiles)
            page.Append($"<div class=\"tile\"><b>{Html.Text(tile.Value)}</b>")
                .Append($"<span class=\"tl\">{Html.Text(tile.Label)}</span>")
                .Append($"<span class=\"tn\">{Html.Text(tile.Note)}</span></div>\n");
        page.Append("</div>\n");
    }

    // ---------------------------------------------------------------------- picture ----

    /// <summary>
    /// The mosaic — A13 tier 1, and the first thing on the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>First because of what it is for, which is not what the rest of the page is for.</b> The
    /// tiers below it answer to <c>PRD-free-tier.md</c> §4 — a number that does not end in a
    /// sentence somebody changes their behaviour over does not ship. This one answers to §9's third
    /// metric instead, and the difference is stated here rather than left for a reader to infer
    /// from the fact that it makes no claim.
    /// </para>
    /// <para>
    /// <b>The caption does the work §4 would otherwise do.</b> An area encoding is read badly by
    /// eye, so what a cell is, what its size means, what the mark means and how many cells carry it
    /// are all said in words underneath — and the projects too small to hold a name are listed,
    /// which is <c>docs/DEFECTS.md</c> §31's lesson applied before a reader has to find it again.
    /// </para>
    /// </remarks>
    private static void Picture(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        if (model.Types.Count == 0) return;

        var marks = Mosaic.Marked(model, findings);

        page.Append("<div class=\"picture\">\n").Append(Mosaic.Render(model, findings)).Append("</div>\n");

        page.Append($"<p class=\"sub\">Every one of the {Html.Count(model.Types.Count)} types this run analysed, ");
        page.Append("one cell each, sized by how many lines it spans and grouped into the project that declares it — ");
        page.Append("biggest project first. ");
        page.Append($"Some finding below is about {Html.Count(marks.Named)} of them, which is the tint; ");
        page.Append($"<strong>the {Html.Count(marks.Leading)} outlined in red are where to start</strong>. ");
        page.Append("Both marks are a yes or a no and never a degree — a mosaic shaded by <em>how</em> unusual a ");
        page.Append("component is would be a score, and this tool does not have one.</p>\n");

        // The lead is X10's selection and nothing else, so the picture and the findings pane cannot
        // disagree about which components a reader should open first. Named here as well as drawn,
        // because a reader who wants to find one has to be able to search for it — the same reason
        // docs/DEFECTS.md §31 puts the folded project names beside the project map.
        var leading = Selection.Exemplars(findings)
            .Select(f => model.Find(f.Subject) ?? (f.Subject.DeclaringType is { } d ? model.Find(d) : null))
            .Where(t => t is not null)
            .Select(t => t!.Name)
            .ToList();

        if (leading.Count > 0)
            page.Append("<p class=\"sub\">Those are one per kind of finding this run made, ")
                .Append("<em>ordered by how uncommon each kind is in this codebase</em> — which is an ordering and ")
                .Append("not a severity, because the tool has no way to say a hub is worse than a cycle: ")
                .Append(Html.Text(string.Join(", ", leading)))
                .Append(".</p>\n");

        var unlabelled = Mosaic.Unlabelled(model);
        if (unlabelled.Count > 0)
            page.Append($"<p class=\"sub\">{Html.Count(unlabelled.Count)} project(s) are on the picture but too small ")
                .Append("to hold a name at this size: ")
                .Append(Html.Text(string.Join(", ", unlabelled)))
                .Append(".</p>\n");
    }

    // ------------------------------------------------------------------------ risks ----

    /// <summary>
    /// The risk highlights — A13 tier 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same selection and the same sentences the terminal leads with</b>, from
    /// <see cref="Selection.Exemplars"/> and <see cref="Claims"/>, because two renderers wording one
    /// claim differently is how a reader learns not to trust either. What differs is the shape: this
    /// medium can afford a card, a peer group on its own line and a link into the section.
    /// </para>
    /// <para>
    /// <b>Above orientation, which reverses the order §6 asks for, and the reversal is the item.</b>
    /// §6 puts orientation first so a newcomer is not led with claims about components they cannot
    /// place — and A11 round 1 tested exactly that arrangement and found people placing the
    /// components correctly and not knowing why any of it mattered. The picture above now does the
    /// placing, in one image rather than five sections, which is what makes leading with nine claims
    /// something other than what §6 warned against.
    /// </para>
    /// </remarks>
    private static void Risks(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        var leading = Selection.Exemplars(findings)
            .Where(f => Claims.IsRiskClaim(f.Kind))
            .ToList();

        page.Append("<h2>Start here</h2>\n");

        if (leading.Count == 0)
        {
            page.Append("<p class=\"empty\">Nothing was nominated. That is a real answer, not an error — ");
            page.Append("every threshold this run used is listed at the foot of the page.</p>\n");
            return;
        }

        page.Append($"<p class=\"lede\">{Html.Count(leading.Count)} claims, one for each kind of risk this run found, ");
        page.Append("<em>ordered by how uncommon each kind is in this codebase</em>. That is an ordering and not a ");
        page.Append("severity — this tool has no way to say a hub is worse than a cycle, and does not pretend to. ");
        page.Append("Each one is the strongest row of its section.</p>\n");

        foreach (var finding in leading)
        {
            var claim = Claims.For(model, finding);
            if (!claim.Exists) continue;

            var type = model.Find(finding.Subject)
                       ?? (finding.Subject.DeclaringType is { } d ? model.Find(d) : null);
            var total = findings.OfKind(finding.Kind).Count;

            page.Append("<div class=\"card lead\">\n");
            page.Append($"<h4>{Html.Text(claim.Subject)}</h4>\n");

            // Where it is. The claim's own location wins where it has one — a member-level finding
            // knows where the member is, and sending a reader to the top of the file instead is
            // docs/DEFECTS.md §24's mistake in a different element.
            if (type is not null)
            {
                var at = claim.Trailer.Length > 0
                    ? claim.Trailer
                    : type.Location.IsKnown
                        ? $"{Path.GetFileName(type.Location.File)}:{Html.Count(type.Location.Line)}"
                        : "";

                page.Append($"<p class=\"where\">{Html.Text(type.Project)}");
                if (at.Length > 0) page.Append($" · {Html.Text(at)}");
                page.Append("</p>\n");
            }

            page.Append($"<p class=\"claim\">{Html.Text(claim.Sentence)}</p>\n");

            if (claim.Evidence.Length > 0)
                page.Append($"<p class=\"sub mono\">{Html.Text(claim.Evidence)}</p>\n");

            // What kind this is, and how many more of it there are. A lead item with no count reads
            // as the only one of its kind — true of layer span on nopCommerce and false of the
            // 1,091 concealed decisions, and telling those apart is the whole job of this section.
            page.Append("<p class=\"sub\">");
            page.Append($"<strong>{Html.Text(Claims.KindName(finding.Kind))}</strong> — ");
            page.Append(Html.Text(Claims.KindBlurb(finding.Kind)));
            page.Append(total == 1
                ? " This is the only one in this codebase."
                : $" {Html.Count(total)} of these were found; this is the strongest.");
            page.Append("</p>\n");

            page.Append("</div>\n");
        }
    }

    /// <summary>
    /// Where the rest is — tier 4, which was already built.
    /// </summary>
    /// <remarks>
    /// <b>A pointer rather than a fourth document.</b> A13 tier 4 is explicit that the full
    /// population already ships twice, in <c>--json</c> and <c>--csv</c>, and that writing another
    /// one is the mistake. What this has to do is make the omission visible: a page that quietly
    /// showed nine findings out of 1,642 would be <c>docs/DEFECTS.md</c> §3 at the scale of a whole
    /// artifact, and invariant 8 says silence is never a clean bill.
    /// </remarks>
    private static void Everything(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        page.Append("<h2>Everything else</h2>\n");

        var kinds = findings.All.Select(f => f.Kind).Distinct().Count();

        page.Append($"<p class=\"lede\">This run made {Html.Count(findings.Count)} findings across ");
        page.Append($"{Html.Count(kinds)} kinds, and the page above leads with the strongest of each. ");
        page.Append("<strong>The rest are not hidden and not summarised away</strong> — they are in the exports, ");
        page.Append("which carry every finding, every type, every member and every dependency:</p>\n");

        page.Append("<ul class=\"sub\">\n");
        page.Append("<li><span class=\"mono\">--json</span> — the whole model, with a schema version, ")
            .Append("for anything that reads it back.</li>\n");
        page.Append("<li><span class=\"mono\">--csv</span> — <span class=\"mono\">types.csv</span>, ")
            .Append("<span class=\"mono\">members.csv</span> and <span class=\"mono\">edges.csv</span>, which join ")
            .Append("on identity rather than on a name.</li>\n");
        page.Append("<li><span class=\"mono\">--full</span> — this page with every section enumerated, ")
            .Append($"capped at <span class=\"mono\">--top</span> ({Html.Count(model.Policy.Top)}) per kind. ")
            .Append("For CI, and for whoever wants it.</li>\n");
        page.Append("</ul>\n");

        // The disclosure, which is not a risk claim and is not led with as one — but which invariant
        // 8 will not let the page drop just because the enumeration moved behind a flag.
        var coverage = findings.OfKind(FindingKind.Coverage);
        if (coverage.Count > 0)
            page.Append($"<p class=\"note\"><strong>{Html.Count(coverage.Count)} of this solution's ")
                .Append($"{Html.Count(model.Types.Count)} types sit in a group too small to compare them against</strong>, ")
                .Append($"so no peer reading was possible for them and none is implied above. That is not a finding ")
                .Append("about those types — it is a record that the tool stayed quiet about them, which is the ")
                .Append("one thing it is not allowed to do silently. They carry a row each in the exports.</p>\n");
    }

    // ------------------------------------------------------------------- orientation ----

    private static void Orientation(StringBuilder page, SolutionModel model)
    {
        page.Append("<h2>Orientation</h2>\n");

        Diagram(page, model);
        Projects(page, model);
        Integrations(page, model);
        Cycles(page, model);
        Coverage(page, model);
    }

    /// <summary>
    /// The project map, inline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// First, because §6 puts diagrams at the top of orientation and because it is the only thing
    /// on the page a reader can take in without reading. It is the same SVG <c>--diagram</c> writes
    /// standalone, from one renderer — a second copy drawn for the page would be
    /// <c>docs/ARCHITECTURE.md</c> §3's failure in a new place.
    /// </para>
    /// <para>
    /// <b>Inline SVG rather than an <c>&lt;img&gt;</c></b>, which would be a second file and break
    /// the one promise this artifact makes. It costs 4–9KB on the two reference solutions, against
    /// a page of 220–275KB.
    /// </para>
    /// </remarks>
    private static void Diagram(StringBuilder page, SolutionModel model)
    {
        var graph = model.ProjectGraph;
        if (graph.Groups.Count == 0) return;

        page.Append("<h3>The shape of it</h3>\n");
        page.Append("<p class=\"sub\">Projects, and what depends on what. Dependencies run downward, ");
        page.Append("so the bottom row is what everything rests on. ");

        var folded = graph.Groups.Count(g => g.Size > 1 && !g.IsCycle);
        if (folded > 0)
            page.Append($"{Html.Count(folded)} box(es) hold several projects that are the same shape — ")
                .Append("same dependencies and same dependents — because that is one fact rather than several. ");

        page.Append($"{Html.Count(graph.Depth)} layer(s) deep.</p>\n");

        page.Append("<div class=\"scroll\">\n").Append(ArchitectureDiagram.Render(model)).Append("</div>\n");

        // docs/DEFECTS.md §31. Saying that folding happened is not the same as saying what is
        // inside, and a reader scanning for a project name reads a fold as an omission. The names
        // go here rather than into the boxes: a picture cannot be searched, and a box that grows
        // to fit its members is the thing the fold exists to prevent.
        foreach (var (label, projects) in ArchitectureDiagram.Folded(model))
        {
            page.Append($"<p class=\"sub\"><strong>{Html.Text(label)}</strong> holds ");
            page.Append(Html.Text(string.Join(", ", projects)));
            page.Append("</p>\n");
        }
    }

    private static void Projects(StringBuilder page, SolutionModel model)
    {
        page.Append("<h3>Projects</h3>\n");
        page.Append("<p class=\"sub\">I = Ce/(Ce+Ca), low means much depends on it. A = share of types that are ");
        page.Append("abstract or interfaces. D = distance from the main sequence. ");
        page.Append("<em>Stable and concrete</em> is the zone of pain: hard to change, hard to extend.</p>\n");

        var unreferenced = model.UnreferencedProjects.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        page.Append("<div class=\"scroll\"><table>\n<tr><th>Project</th><th class=\"n\">Types</th>");
        page.Append("<th class=\"n\">Ca</th><th class=\"n\">Ce</th><th class=\"n\">A</th><th class=\"n\">I</th>");
        page.Append("<th class=\"n\">D</th><th>Zone</th></tr>\n");

        foreach (var c in model.ProjectCouplings)
        {
            var flag = unreferenced.Contains(c.Project)
                ? " <span class=\"tag\">nothing depends on it</span>"
                : "";

            page.Append($"<tr><td>{Html.Text(c.Project)}{flag}</td>");
            page.Append($"<td class=\"n\">{Html.Count(c.TotalTypes)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(c.TypesElsewhereReachingIn)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(c.TypesHereReachingOut)}</td>");
            page.Append($"<td class=\"n\">{Html.Number(c.Abstractness)}</td>");
            page.Append($"<td class=\"n\">{Optional(c.Instability)}</td>");
            page.Append($"<td class=\"n\">{Optional(c.DistanceFromMainSequence)}</td>");
            page.Append($"<td>{Html.Text(Zone(c.Zone))}</td></tr>\n");
        }

        page.Append("</table></div>\n");

        // Two lists that are not the same list, and saying so is the whole point of the sentence:
        // a project declaring no analysed type has no metrics rather than metrics of zero.
        var empty = model.Projects.Count - model.ProjectCouplings.Count;
        if (empty > 0)
            page.Append($"<p class=\"sub\">{Html.Count(empty)} project(s) declared no analysed type and have no ")
                .Append("metrics to report — an empty project has no abstractness, not an abstractness of zero.</p>\n");
    }

    /// <summary>A measurement that may not exist. Blank, never a stand-in — invariant 6.</summary>
    private static string Optional(double? value) => value is { } d ? Html.Number(d) : "<span class=\"empty\">—</span>";

    /// <summary>
    /// The zone, worded for this medium.
    /// </summary>
    /// <remarks>
    /// Which zone a project is in is Core's judgement; the words are the renderer's, so this says
    /// the same things as the terminal's version without borrowing its <c>&lt;--</c> arrows, which
    /// exist to point along a fixed-width row and mean nothing in a table cell.
    /// </remarks>
    private static string Zone(MainSequenceZone zone) => zone switch
    {
        MainSequenceZone.Pain => "zone of pain — stable and concrete",
        MainSequenceZone.Uselessness => "zone of uselessness — abstract, unused",
        MainSequenceZone.NearMainSequence => "near the main sequence",
        _ => "",
    };

    private static void Integrations(StringBuilder page, SolutionModel model)
    {
        var map = model.Integrations;
        var contact = model.ContactPoints;

        page.Append("<h3>What it talks to</h3>\n");
        page.Append($"<p class=\"sub\">{Html.Count(contact.Inbound.Count)} way(s) in and ");
        page.Append($"{Html.Count(contact.Outbound.Count)} way(s) out. The map below counts what this solution ");
        page.Append("<em>calls into</em>, which is not the same question.</p>\n");

        if (map.Systems.Count == 0)
        {
            page.Append("<p class=\"empty\">No external system recognised. That means either this solution ");
            page.Append("genuinely calls nothing out, or it uses frameworks this classifier does not know.</p>\n");
        }
        else
        {
            page.Append("<div class=\"scroll\"><table>\n<tr><th>External system</th>")
                .Append("<th class=\"n\">Types touching it</th><th>Provided by</th></tr>\n");
            foreach (var system in map.Systems)
                page.Append($"<tr><td class=\"mono\">{Html.Text(system.Namespace)}</td>")
                    .Append($"<td class=\"n\">{Html.Count(system.TypesTouching)}</td>")
                    .Append($"<td>{Html.Text(Provider(system.Origin))}</td></tr>\n");
            page.Append("</table></div>\n");
        }

        if (map.PlumbingReferences > 0)
            page.Append($"<p class=\"sub\">{Html.Count(map.PlumbingReferences)} reference(s) to language and ")
                .Append("framework plumbing were filtered out of that list rather than dropped silently.</p>\n");
    }

    private static void Cycles(StringBuilder page, SolutionModel model)
    {
        page.Append("<h3>Circular references</h3>\n");

        CycleGroup(page, "Namespaces", model.NamespaceCycles,
            "Mutually dependent namespaces cannot be layered, understood or extracted independently.",
            id => Name(model, id));

        CycleGroup(page, "Projects", model.ProjectCycles,
            "Two projects each naming a type in the other. Legal MSBuild — only project references cannot cycle.",
            id => Name(model, id));

        CycleGroup(page, $"Type tangles ({Html.Number(model.Policy.MinTangle)}+)", model.TypeTangles,
            "Groups of types that all reach each other, so none of them can be tested or changed alone.",
            id => Name(model, id));
    }

    private static void CycleGroup(
        StringBuilder page, string title, IReadOnlyList<Cycle> cycles, string blurb, Func<SubjectRef, string> name)
    {
        page.Append($"<p><strong>{Html.Text(title)}</strong> — <span class=\"sub\">{Html.Text(blurb)}</span></p>\n");

        if (cycles.Count == 0)
        {
            page.Append("<p class=\"empty\">None.</p>\n");
            return;
        }

        foreach (var cycle in cycles)
        {
            page.Append($"<p class=\"claim\">{Html.Count(cycle.Size)}: ");
            page.Append(Html.Text(string.Join(", ", cycle.Members.Select(name))));
            page.Append("</p>\n");

            var loop = string.Join(" → ", cycle.Path.Select(name));
            var closes = cycle.Path.Count > 0 ? name(cycle.Path[0]) : "";

            page.Append($"<p class=\"loop\">loop: {Html.Text(loop)} → {Html.Text(closes)}");
            if (!cycle.PathCoversEveryMember)
                page.Append($" — {Html.Count(cycle.Path.Count)} of the {Html.Count(cycle.Size)}; ")
                    .Append($"all {Html.Count(cycle.Size)} reach each other");
            page.Append("</p>\n");
        }
    }

    private static void Coverage(StringBuilder page, SolutionModel model)
    {
        var coverage = model.Coverage;

        page.Append("<h3>What was not analysed</h3>\n");
        page.Append("<p class=\"sub\">Every number above is computed over what was actually read. ");
        page.Append("This is the rest.</p>\n");

        page.Append("<ul class=\"sub\">\n");

        page.Append(coverage.SkippedProjects.Count == 0
            ? "<li>No project was skipped as a test project.</li>\n"
            : $"<li>Skipped as test projects: {Html.Text(string.Join(", ", coverage.SkippedProjects))}. "
              + "A library used only by tests therefore has no visible consumer here.</li>\n");

        page.Append($"<li>{Html.Count(coverage.ExcludedTypes)} type(s) dropped by ")
            .Append($"{Html.Count(coverage.ExclusionsApplied.Count)} path exclusion(s) — generated and scaffolded code.</li>\n");

        page.Append(coverage.EdgesToUnanalysedTypes == 0
            ? "<li>Every dependency found had both endpoints in the analysed set.</li>\n"
            : $"<li><strong>{Html.Count(coverage.EdgesToUnanalysedTypes)} dependency reference(s)</strong> pointed at "
              + "types the walk never analysed and were dropped. Read fan-in as a lower bound.</li>\n");

        if (coverage.LoadDiagnostics.Count > 0)
        {
            page.Append($"<li>{Html.Count(coverage.LoadDiagnostics.Count)} diagnostic(s) while loading. ");
            page.Append("These are <em>not</em> reliably failures — on one reference solution every one of them ");
            page.Append("was a NuGet vulnerability advisory, and 3,209 types loaded anyway.</li>\n");
        }

        page.Append("</ul>\n");
    }

    // --------------------------------------------------------------------- findings ----

    /// <summary>
    /// The findings, grouped by claim.
    /// </summary>
    /// <remarks>
    /// <b>Grouped rather than ranked, and that is a consequence of the record rather than a layout
    /// preference.</b> <see cref="Finding"/> carries no severity and no rank, deliberately —
    /// <c>docs/ARCHITECTURE.md</c> §4 excludes them because banding severity into identity would
    /// make a retune invalidate every stored acknowledgment. So there is no honest global order to
    /// sort by, and inventing one here would be a renderer manufacturing a judgement Core refused
    /// to make. Within a kind the order is the model's, which is by subject identity.
    /// </remarks>
    private static void Findings(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        page.Append("<h2>Findings</h2>\n");

        if (findings.Count == 0)
        {
            page.Append("<p class=\"empty\">Nothing was nominated. That is a real answer, not an error — ");
            page.Append("every threshold this run used is listed at the foot of the page.</p>\n");
            return;
        }

        page.Append("<p class=\"lede\">Each of these is a claim about one component, with the measurements it ");
        page.Append("rests on. <strong>None of them is a score and they are not ranked against each other</strong> — ");
        page.Append("the tool does not have a severity model, and a list sorted by an invented one reads as though ");
        page.Append("it did.</p>\n");

        foreach (var group in findings.All
                     .GroupBy(f => f.Kind)
                     .OrderBy(g => g.Key))
        {
            var all = group.ToList();
            var shown = all.Take(model.Policy.Top).ToList();

            page.Append($"<h3>{Html.Text(Claims.KindName(group.Key))} — {Html.Count(all.Count)}</h3>\n");
            page.Append($"<p class=\"sub\">{Html.Text(Claims.KindBlurb(group.Key))}</p>\n");

            foreach (var finding in shown)
                Card(page, model, finding);

            if (shown.Count < all.Count)
                page.Append($"<p class=\"note\">Showing {Html.Count(shown.Count)} of ")
                    .Append($"{Html.Count(all.Count)}. Raise <span class=\"mono\">--top</span> to see more, ")
                    .Append("or read the JSON or CSV export, which carry every one.</p>\n");
        }
    }

    private static void Card(StringBuilder page, SolutionModel model, Finding finding)
    {
        var type = model.Find(finding.Subject) ?? model.Find(finding.Subject.DeclaringType ?? finding.Subject);

        page.Append($"<div class=\"card\" id=\"{Html.Text(Html.Anchor(finding.Subject.Canonical))}\">\n");
        page.Append($"<h4>{Html.Text(Display(model, finding.Subject))}</h4>\n");

        if (type is not null)
        {
            // docs/DEFECTS.md §26. This line used to join three facts with middots, and the third
            // was not the same kind of thing as the first two: project and file are addresses,
            // the cohort is the population the claim is measured against — the whole basis of the
            // finding. Readers understood the phrase and guessed at its job ("project membership?
            // definition location? caller set?"). Two addresses stay together; the comparison gets
            // its own line and says what it is.
            page.Append($"<p class=\"where\">{Html.Text(type.Project)}");
            if (type.Location.IsKnown)
                page.Append($" · {Html.Text(Path.GetFileName(type.Location.File))}:{Html.Count(type.Location.Line)}");
            page.Append("</p>\n");

            // And only where the finding actually consulted a cohort. §3.6 to §3.9 are cohort-free
            // by design, so printing a peer group on those cards claims a relative reading the
            // finding never made — defect 17's mistake in a different element. The gated
            // CohortSize receipt is what distinguishes them, and it is the detector's own record
            // rather than a list of kinds kept in the renderer.
            if (finding.Receipts.Any(r => string.Equals(r.Name, "CohortSize", StringComparison.Ordinal)))
                page.Append("<p class=\"sub\">Compared against "
                            + $"{Html.Text(Sentences.PeerGroup(type.Cohort, type.CohortSize))}</p>\n");
        }

        var holding = finding.Qualifiers.Where(q => q.Holds).ToList();
        if (holding.Count > 0)
        {
            page.Append("<div class=\"tags\">\n");
            foreach (var qualifier in holding)
                page.Append($"<span class=\"tag\">{Html.Text(QualifierText(qualifier.Name))}</span>\n");
            page.Append("</div>\n");
        }

        if (finding.Participants.Count > 0)
        {
            page.Append($"<p class=\"claim sub\">{Html.Text(ParticipantsAre(finding.Kind))}: ");
            page.Append(Html.Text(string.Join(", ", finding.Participants.Select(p => Display(model, p)))));
            page.Append("</p>\n");
        }

        if (finding.Receipts.Count > 0)
        {
            page.Append("<details>\n<summary>Why this fired</summary>\n");
            page.Append("<table class=\"receipts\">\n<tr><th>Measured</th><th class=\"n\">Value</th><th>Had to clear</th></tr>\n");

            foreach (var receipt in finding.Receipts)
            {
                page.Append($"<tr><td>{Html.Text(receipt.Name)}</td>");
                page.Append($"<td class=\"n\">{Html.Number(receipt.Value)}</td>");
                page.Append("<td>");

                // The gate is a name; the number comes from the policy this run used, so a finding
                // and the policy cannot disagree about what gated it.
                if (receipt.Gate is { } gate)
                {
                    var threshold = model.Policy.Values
                        .Where(v => string.Equals(v.Name, gate, StringComparison.Ordinal))
                        .Select(v => (double?)v.Value)
                        .FirstOrDefault();

                    page.Append($"<span class=\"mono\">{Html.Text(gate)}</span>");
                    if (threshold is { } value) page.Append($" = {Html.Number(value)}");
                }
                else
                {
                    page.Append("<span class=\"empty\">context</span>");
                }

                page.Append("</td></tr>\n");
            }

            page.Append("</table>\n</details>\n");
        }

        page.Append("</div>\n");
    }

    // -------------------------------------------------------------------- drill-down ----

    /// <summary>
    /// One row per component a shown finding is <i>about</i>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Subjects, and deliberately not participants.</b> Including the types a finding merely
    /// names took nopCommerce from 125 rendered cards to 1,701 rows and made the drill-down
    /// two-thirds of the whole file — 525KB of the 765KB — for components that are not themselves
    /// nominated. §6 makes bundle size a real budget, and it exists to leave room for the diagrams
    /// A7 and A8 will inline; spending it on context is spending it on the wrong thing.
    /// </para>
    /// <para>
    /// Nothing is lost that the page did not already have: a participant is named in the card that
    /// names it, which is where invariant 7's <i>"why is authentication calling TenantStore?"</i>
    /// actually lives. What a participant does not get here is its own row of numbers, and the
    /// section says where those are.
    /// </para>
    /// <para>
    /// Bounded by the finding set rather than by a cap, so it grows with what there is to say
    /// rather than with the size of the codebase.
    /// </para>
    /// </remarks>
    private static void DrillDown(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);

        // The findings actually rendered, not every finding: a component named only by a card the
        // pane dropped has nothing on this page pointing at it, and a row for it would be an
        // answer to a question the reader was never shown.
        foreach (var finding in findings.All
                     .GroupBy(f => f.Kind)
                     .SelectMany(g => g.Take(model.Policy.Top)))
        {
            Include(finding.Subject);
        }

        void Include(SubjectRef subject)
        {
            var type = model.Find(subject) ?? (subject.DeclaringType is { } d ? model.Find(d) : null);
            if (type is not null) named.Add(type.Subject.Canonical);
        }

        var components = model.Types.Where(t => named.Contains(t.Subject.Canonical)).ToList();

        page.Append("<h2>Components named above</h2>\n");

        if (components.Count == 0)
        {
            page.Append("<p class=\"empty\">No finding named a component.</p>\n");
            return;
        }

        page.Append($"<p class=\"lede\">The {Html.Count(components.Count)} component(s) the findings above are ");
        page.Append("<em>about</em>. Types those findings merely <em>name</em> are named in the card that names ");
        page.Append("them and do not get a row here. ");
        page.Append($"<strong>This is not every type</strong> — the solution has {Html.Count(model.Types.Count)}, ");
        page.Append("and a row each would make this file too large to send. The CSV and JSON exports carry every ");
        page.Append("type, every member and every dependency.</p>\n");

        page.Append("<div class=\"scroll\"><table>\n<tr><th>Component</th><th>Project</th><th>Role</th>");
        page.Append("<th class=\"n\">Fan-in</th><th class=\"n\">Fan-out</th><th class=\"n\">Cc</th>");
        page.Append("<th class=\"n\">Max member</th><th class=\"n\">Members</th><th class=\"n\">Lines</th></tr>\n");

        foreach (var type in components)
        {
            page.Append($"<tr><td id=\"{Html.Text(Html.Anchor(type.Subject.Canonical))}\">{Html.Text(type.Name)}");
            if (type.Location.IsKnown)
                page.Append($"<br><span class=\"where\">{Html.Text(Path.GetFileName(type.Location.File))}:")
                    .Append($"{Html.Count(type.Location.Line)}</span>");
            page.Append("</td>");
            page.Append($"<td>{Html.Text(type.Project)}</td>");
            page.Append($"<td>{Html.Text(type.Classification.Kind)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.FanIn)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.FanOut)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.Cyclomatic)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.MaxMemberCyclomatic)}");
            if (type.MostComplexMember is { } member)
                page.Append($"<br><span class=\"where\">{Html.Text(member.Name)}</span>");
            page.Append("</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.MemberCount)}</td>");
            page.Append($"<td class=\"n\">{Html.Count(type.LinesOfCode)}</td></tr>\n");
        }

        page.Append("</table></div>\n");
    }

    // ----------------------------------------------------------------------- footer ----

    private static void Footer(StringBuilder page, SolutionModel model)
    {
        page.Append("<h2>The thresholds this run used</h2>\n");
        page.Append("<p class=\"lede\">Every number a finding was tested against. They are all movable from the ");
        page.Append("command line, and a finding that looks wrong is often a threshold that is wrong for this ");
        page.Append("codebase rather than a claim that is wrong about the component.</p>\n");

        page.Append("<details>\n<summary>Show all thresholds</summary>\n<div class=\"scroll\"><table>\n");
        page.Append("<tr><th>Value</th><th class=\"n\">Setting</th><th>Flag</th></tr>\n");

        foreach (var (name, value) in model.Policy.Values)
            page.Append($"<tr><td>{Html.Text(name)}</td><td class=\"n\">{Html.Number(value)}</td>")
                .Append($"<td class=\"mono\">{Html.Text(CommandLine.FlagFor(name))}</td></tr>\n");

        page.Append("</table></div>\n</details>\n");

        page.Append("<footer>Generated by Bearing ").Append(Html.Text(model.ToolVersion));
        page.Append(" from ").Append(Html.Text(model.SolutionPath));
        page.Append(". This file is self-contained: it makes no network requests and runs no script.</footer>\n");
    }

    // ------------------------------------------------------------------------ words ----

    /// <summary><c>docs/DEFECTS.md</c> §30 — what a reader could change, said in the row.</summary>
    private static string Provider(ExternalOrigin origin) => origin switch
    {
        ExternalOrigin.Framework => "the framework",
        ExternalOrigin.Package => "a package",
        _ => "not determined",
    };

    private static string Display(SolutionModel model, SubjectRef subject)
    {
        if (model.Find(subject) is { } type) return type.Name;

        if (subject.DeclaringType is { } declaring && model.Find(declaring) is { } owner)
        {
            var member = owner.Members
                .FirstOrDefault(m => string.Equals(m.Subject.Canonical, subject.Canonical, StringComparison.Ordinal));

            // docs/DEFECTS.md §24, and one rule for both renderers now. This medium had its own
            // half-fix that skipped the dot and produced "CustomerInfoValidator ctor", which is
            // not wrong so much as still addressed to the runtime rather than to a reader.
            if (member is not null) return Sentences.Member(owner.Name, member.Name);
        }

        return subject.Kind switch
        {
            SubjectKind.Set => string.Join(" + ", subject.Members.Select(m => Display(model, m))),
            SubjectKind.Solution => "this solution",
            _ => subject.Canonical,
        };
    }

    private static string Name(SolutionModel model, SubjectRef subject)
    {
        if (model.Find(subject) is { } type) return type.Name;

        // A namespace or project subject has no node to look up; its canonical form is
        // "kind|name", and the name is what a reader recognises.
        var separator = subject.Canonical.IndexOf('|', StringComparison.Ordinal);
        return separator < 0 ? subject.Canonical : subject.Canonical[(separator + 1)..];
    }

    /// <summary>
    /// What a kind's participants <i>are</i> — the relationship, not just the names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the second thing building the pane said about the finding record, and it is the
    /// one worth acting on.</b> <see cref="Finding.Participants"/> is an untyped
    /// <c>SubjectRef</c> list, and across the eleven kinds it holds at least four unrelated
    /// relationships: the <i>dependencies that make the span</i> for layer span, the <i>members
    /// that write</i> for shared mutable state, the <i>callers</i> for change cost and blast
    /// radius, and the <i>most complex member</i> for the six that name one. Rendering them all as
    /// "Names: …" is wrong in a specific way — for a god object nominated on the size arm, the
    /// named member exists to show the reader there is <i>no</i> method carrying the weight, and
    /// listing it beside a dependency set says the opposite.
    /// </para>
    /// <para>
    /// <b>A per-kind renderer could not have found this and a generic one cannot avoid it.</b> The
    /// terminal writes a bespoke sentence per section, so each one wraps its participants in words
    /// that happen to fit; this pane renders every kind through one path, which is what makes the
    /// mismatch visible. That is precisely the job <c>TECHREQ-job-a.md</c> §6 assigns the HTML
    /// pane.
    /// </para>
    /// <para>
    /// <b>The record does not need a role field yet, and the reason is a constraint rather than an
    /// accident:</b> every kind carries exactly one relationship, so the relationship is a function
    /// of the kind and a label here is complete. **The day a kind carries two** — a hub naming both
    /// its callers and its worst method, say — this table cannot express it and
    /// <c>Participant(Subject, Role)</c> becomes necessary in Core. Recorded in
    /// <c>docs/ARCHITECTURE.md</c> §4 so the constraint is written down rather than rediscovered
    /// by whoever breaks it.
    /// </para>
    /// </remarks>
    private static string ParticipantsAre(FindingKind kind) => kind switch
    {
        FindingKind.SpansArchitecturalLayers => "Reaches",
        FindingKind.SharedMutableState => "Written by",
        FindingKind.ChangeCost => "Changing it reaches",
        FindingKind.BugBlastRadius => "A defect here reaches",
        _ => "Most complex member",
    };

    private static string QualifierText(string qualifier) => qualifier switch
    {
        Qualifiers.LowAbsoluteConnectivity => "genuinely low connectivity",
        Qualifiers.CarriesRealLogic => "carries real logic",
        Qualifiers.TooLargeToHold => "too large to hold at once",
        Qualifiers.PartOfALayeringPattern => "one of a repeated pattern",
        Qualifiers.GloballyExtremeFanIn => "extreme fan-in solution-wide",
        Qualifiers.GloballyExtremeComplexity => "extreme complexity solution-wide",
        _ => qualifier,
    };
}

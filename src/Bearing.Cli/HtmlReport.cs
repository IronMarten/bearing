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
/// the rest is. A silent subset would be undisclosed truncation again, in a new medium.
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
    /// <param name="judgement">Every claim the run made, and what stopped the quiet ones.</param>
    /// <param name="generatedAt">
    /// When the run happened — a parameter, not a clock read, for the reason
    /// <see cref="JsonOutput.Render"/> gives.
    /// </param>
    public static string Render(
        SolutionModel model, Judgement judgement, DateTimeOffset generatedAt, bool full = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(judgement);

        // The survivors, for every section but one. docs/ARCHITECTURE.md §11: the judgement is what
        // a renderer is handed, and Cycles() is the section that needs the other half of it.
        var findings = judgement.Reported;

        var page = new StringBuilder();
        var solution = Path.GetFileName(model.SolutionPath);

        page.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n");
        page.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        page.Append($"<title>Bearing — {Html.Text(solution)}</title>\n");
        page.Append("<style>\n").Append(HtmlStyle.Css).Append("\n</style>\n</head>\n<body>\n<div class=\"wrap\">\n");

        Header(page, model, solution, findings, generatedAt);
        Picture(page, model, findings);
        Risks(page, model, findings);
        Orientation(page, model, judgement);

        // Tier 4. The enumeration is the artifact A11 round 1 called "a wall of text", and it lives
        // behind a flag for CI and for whoever wants it. `PRD-free-tier.md` §9's anti-metric is that
        // more findings is worse.
        //
        // This comment used to say every row was "still reachable — in --json,
        // in --csv, and here behind a flag", which is three untruths. The exports carry the model
        // and not the findings, and --full is capped at --top like every other section. Closing
        // that for real is the findings export (`SCHEMA-findings-export.md`), not a wording change.
        if (full)
        {
            Findings(page, model, findings);
            DrillDown(page, model, findings);
        }
        else
        {
            Everything(page, model, findings);
        }

        Acknowledged(page, model, judgement);
        Footer(page, model);

        page.Append("</div>\n</body>\n</html>\n");
        return page.ToString();
    }

    /// <summary>Renders the report and writes it to <paramref name="path"/>.</summary>
    public static void Write(
        string path, SolutionModel model, Judgement judgement, DateTimeOffset generatedAt, bool full = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, Render(model, judgement, generatedAt, full), new UTF8Encoding(false));
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
        // "Type" is said once with its meaning attached, because the word has two readings and a
        // reader outside the build took the wrong one — a category of thing, rather than the C#
        // declaration every count here is made of. Sentences.TypeKinds derives the list from the
        // run, so it cannot claim records on a solution that has none.
        page.Append($"<p class=\"sub\">{Html.Count(model.Types.Count)} ");
        page.Append($"{Sentences.Do(model.Types.Count, "type", "types")} — {Html.Text(Sentences.TypeKinds(model))} — ");
        page.Append($"in {Html.Count(model.Projects.Count)} ");
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

    /// <summary>A share of one, as a whole percentage.</summary>
    private static string Percent(double share) =>
        $"{Sentences.Whole(Math.Round(100 * share))}%";

    // ---------------------------------------------------------------------- picture ----

    /// <summary>
    /// The plot — X11, candidate A, and the first thing on the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is here because the mosaic was measured in this position and it misled.</b> A reader
    /// assembling the claim the page exists for — <i>which project is dense with findings <b>and</b>
    /// holds everything else up</i> — had to combine the mosaic, the project graph and a table, and
    /// still got it wrong three times. <see cref="ReachPlot"/> carries the measurement and the
    /// three misreads; <c>BRIEF-job-a-picture.md</c> carries the candidates this one beat.
    /// </para>
    /// <para>
    /// <b>The caption states the same three numbers the tiles do, from the same derivations.</b>
    /// Nothing here recomputes: a caption that says 83% over a tile that says 81% is two defensible
    /// numbers and one silent defect.
    /// </para>
    /// </remarks>
    private static void Picture(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        if (model.Types.Count == 0) return;

        page.Append("<div class=\"picture\">\n").Append(ReachPlot.Render(model, findings)).Append("</div>\n");

        page.Append("<p class=\"lede\">");

        if (Tiles.Of(model, findings, TileKind.Clean) is { } clean)
            page.Append($"<strong>{Html.Text(clean.Value)} of this codebase has nothing said about it</strong> — ")
                .Append("the pale dots, and most of the picture. ");

        if (Tiles.Of(model, findings, TileKind.Concentration) is { } where)
            page.Append("<strong>What is named clusters rather than spreading</strong>: ")
                .Append($"{Html.Text(where.Subject)} carries {Html.Text(where.Value)} its share of them. ");

        // The sentence the old picture could not draw and this one can: it is the top-right of the
        // plot. Two measured facts and never their product, which would be a severity model with
        // no unit — see Foundations.
        if (Foundations.Of(model, findings) is { } rests)
            page.Append($"<strong>{Html.Text(rests.Project)} is what the most of it rests on</strong> — ")
                .Append($"{Html.Count(rests.Dependents)} types outside it reach in, and ")
                .Append($"{Html.Text(rests.Share)} of its own {Html.Count(rests.Types)} are named, ")
                .Append("which is the dot furthest right and how high it sits. ");

        page.Append("</p>\n");

        page.Append("<p class=\"sub\">One dot per project. Across, how much of the rest of the solution ");
        page.Append("reaches into it; up, how much of it some finding below names; the area of the dot is how ");
        page.Append("many types it declares. <strong>There is no bad corner and nothing is shaded</strong> — ");
        page.Append("both axes are measurements, and which trade-off matters is the reader's call, because the ");
        page.Append("tool has no severity model to make it with.</p>\n");

        // The folded-box lesson again, in a second picture: a name that is not on the drawing reads
        // as an omission rather than as a shortage of pixels.
        var unlabelled = ReachPlot.Unlabelled(model, findings);
        if (unlabelled.Count > 0)
            page.Append($"<p class=\"sub\">{Html.Count(unlabelled.Count)} ")
                .Append(Sentences.Do(unlabelled.Count, "project has", "projects have"))
                .Append(" a dot but no room for a name: ")
                .Append(Html.Text(string.Join(", ", unlabelled)))
                .Append(".</p>\n");
    }

    /// <summary>
    /// The mosaic, below the project graph — A13 tier 1, in the position X11 left it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What survives is the claim no number makes as well: every type is on this page.</b> It
    /// led the report until the plot replaced it, and the reason is recorded on
    /// <see cref="ReachPlot"/> — area here is lines of code, every claim on the page is a count of
    /// types, and on nopCommerce the named 17% hold 58% of the ink. That gap is stated rather than
    /// left for a reader to walk into, and it is why the caption no longer asks this picture to
    /// answer <i>where is the risk</i>.
    /// </para>
    /// <para>
    /// <b>It is still judged by <c>PRD-free-tier.md</c> §9's third metric</b> and still ships
    /// standalone as <c>--mosaic</c>, which is where the share-me artifact actually lives.
    /// </para>
    /// </remarks>
    private static void Census(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        if (model.Types.Count == 0) return;

        var marks = Mosaic.Marked(model, findings);

        page.Append("<h3>All of it, at once</h3>\n");

        page.Append($"<p class=\"sub\">Every one of the {Html.Count(model.Types.Count)} types this run analysed, ");
        page.Append("one cell each, sized by how many lines it spans and grouped into the project that declares ");
        page.Append($"it — biggest project first. Some finding is about {Html.Count(marks.Named)} of them, which ");
        page.Append($"is the tint; the {Html.Count(marks.Leading)} outlined in red are the types the claims ");
        page.Append("above are about, in the same order. Both marks are a yes or a no and never a degree — a mosaic shaded by ");
        page.Append("<em>how</em> unusual a component is would be a score, and this tool does not have one.</p>\n");

        page.Append("<div class=\"picture\">\n").Append(Mosaic.Render(model, findings)).Append("</div>\n");

        // Said plainly, because a reader who compares this picture with the plot above it will
        // otherwise find them disagreeing and have no way to tell which one is lying.
        page.Append($"<p class=\"sub\"><strong>Read it by count and not by area.</strong> Cells are sized by lines ");
        page.Append("of code and the components a finding names are the large ones, so the tinted cells are ");
        page.Append($"{Html.Text(Percent(marks.NamedInk))} of this drawing and ");
        page.Append($"{Html.Text(Percent(marks.Named / (double)model.Types.Count))} of the types. The plot at the ");
        page.Append("top of this page is drawn in counts, which is what every claim here is measured in.</p>\n");

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
            .Select(f => (Finding: f, Claim: Claims.For(model, f)))
            .Where(x => x.Claim.Exists)
            .ToList();

        page.Append("<h2>Top finding</h2>\n");

        if (leading.Count == 0)
        {
            page.Append("<p class=\"empty\">Nothing was nominated. That is a real answer, not an error — ");
            page.Append("every threshold this run used is listed at the foot of the page.</p>\n");
            return;
        }

        page.Append("<p class=\"lede\">The strongest row of the rarest kind this run found. ");
        page.Append($"{Html.Count(leading.Count)} {Sentences.Do(leading.Count, "kind", "kinds")} fired and each has ");
        page.Append("one claim below, <em>ordered by how uncommon each kind is in this codebase</em> — an ordering ");
        page.Append("and not a severity, because this tool has no way to say a hub is worse than a cycle.</p>\n");

        Annotated(page, model, findings, leading[0].Finding, leading[0].Claim);

        if (leading.Count == 1) return;

        page.Append($"<p class=\"sub\">The other {Html.Count(leading.Count - 1)}, in the same order — one per kind, ");
        page.Append("each the strongest row of its own.</p>\n");

        page.Append("<div class=\"rail\">\n");
        foreach (var (finding, claim) in leading.Skip(1))
            Row(page, model, findings, finding, claim);
        page.Append("</div>\n");
    }

    /// <summary>
    /// The lead finding, with its own anatomy labelled — A13 tier 3, candidate E.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gutter teaches the card once, and the card is the screenshot frame.</b> A11 round 1's
    /// whole feedback was <i>"I don't know what I'm supposed to get out of it"</i> — not that the
    /// claims were wrong, and not that the components were misplaced: participants navigated
    /// correctly and did not know why any of it mattered. Four labels naming what each line of one
    /// card is for cost four short phrases and are the cheapest thing on the page that attacks
    /// comprehension directly. The brief's B1 puts a single finding card in the frame, so this is
    /// the part that has to stand alone when it is pasted into a thread with no page around it.
    /// </para>
    /// <para>
    /// <b>The kind's definition sits under the claim on this card and nowhere else in this
    /// section.</b> It reads as a repetition beside a strong claim — <i>"a defect here
    /// propagates"</i> under <i>"341 distinct callers and internally complex"</i> — and as the whole
    /// interpretation beside a terse one: layer span's claim on nopCommerce is <i>"reaches across 3
    /// kinds"</i>, which is evidence rather than meaning. Once is the cost of the second case; ten
    /// times was the wall of text.
    /// </para>
    /// </remarks>
    private static void Annotated(
        StringBuilder page, SolutionModel model, FindingSet findings, Finding finding, Claim claim)
    {
        page.Append("<div class=\"anat\">\n");

        Label(page, "what it is called<br>and what kind");
        page.Append($"<div class=\"fld\"><b class=\"name\">{Html.Text(claim.Subject)}</b>");
        page.Append($"<span class=\"tag\">{Html.Text(Claims.KindName(finding.Kind))}</span>");
        page.Append($"<span class=\"sub\">{Html.Text(Rank(findings, finding))}</span></div>\n");

        if (Subjects.Where(model, finding, claim.Trailer) is { Length: > 0 } where)
        {
            Label(page, "where to open it");
            page.Append($"<div class=\"fld where\">{Html.Text(where)}</div>\n");
        }

        Label(page, "what we think it means");
        page.Append($"<div class=\"fld\"><p class=\"claim big\">{Html.Text(claim.Sentence)}</p>");
        page.Append($"<p class=\"sub\"><strong>{Html.Text(Claims.KindName(finding.Kind))}</strong> — ");
        page.Append($"{Html.Text(Claims.KindBlurb(finding.Kind))}</p></div>\n");

        if (claim.Evidence.Length > 0)
        {
            Label(page, "the numbers behind it");
            page.Append($"<div class=\"fld\"><p class=\"sub mono\">{Html.Text(claim.Evidence)}</p></div>\n");
        }

        HtmlNeighbours.Render(page, model, finding);

        page.Append("</div>\n");
    }

    /// <summary>
    /// One claim per remaining kind, against the same gutter the card taught.
    /// </summary>
    /// <remarks>
    /// <b>The teaching column goes back to work as a kind and rank rail.</b> Nine cards of equal
    /// weight are a list a reader scrolls; nine rows whose left column names the kind and says how
    /// many more of it there are is a list a reader scans — and it is the same 148 pixels, so the
    /// page has one grid rather than two layouts. What a row drops against the card is the
    /// evidence line and the kind's definition, which is the brief's C1: one claim deep, the rest
    /// listed. Every kind's definition is still on the page, once each, in <c>Everything else</c>.
    /// </remarks>
    private static void Row(
        StringBuilder page, SolutionModel model, FindingSet findings, Finding finding, Claim claim)
    {
        page.Append("<div class=\"row\">\n");

        page.Append($"<div class=\"lbl\"><span class=\"kind\">{Html.Text(Claims.KindName(finding.Kind))}</span>");
        page.Append($"<span class=\"rank\">{Html.Text(Rank(findings, finding))}</span></div>\n");

        page.Append($"<div class=\"fld\"><b class=\"name\">{Html.Text(claim.Subject)}</b>");

        if (Subjects.Where(model, finding, claim.Trailer) is { Length: > 0 } where)
            page.Append($"<p class=\"where\">{Html.Text(where)}</p>");

        page.Append($"<p class=\"claim\">{Html.Text(claim.Sentence)}</p></div>\n");

        HtmlNeighbours.Render(page, model, finding);

        page.Append("</div>\n");
    }

    private static void Label(StringBuilder page, string what) =>
        page.Append($"<div class=\"lbl\">{what}</div>\n");

    /// <summary>
    /// Which of its kind this is, and how many there are.
    /// </summary>
    /// <remarks>
    /// <b>A lead item with no count reads as the only one of its kind</b> — true of layer span on
    /// nopCommerce and false of the 103 concealed decisions, and telling those apart is the whole
    /// triage this section performs. The two renderers word it differently on purpose: the terminal
    /// runs it into a line of prose, and here it is a chip and a rail entry with nothing around it
    /// to carry the grammar.
    /// </remarks>
    private static string Rank(FindingSet findings, Finding finding) =>
        findings.OfKind(finding.Kind).Count is var total && total == 1
            ? "the only one in this codebase"
            : $"1 of {Html.Count(total)}";

    /// <summary>
    /// Where the rest is — tier 4, which was already built.
    /// </summary>
    /// <remarks>
    /// <b>A pointer rather than a fourth document.</b> A13 tier 4 is explicit that the full
    /// population already ships twice, in <c>--json</c> and <c>--csv</c>, and that writing another
    /// one is the mistake. What this has to do is make the omission visible: a page that quietly
    /// showed nine findings out of 1,642 would be undisclosed truncation at the scale of a whole
    /// artifact, and invariant 8 says silence is never a clean bill.
    /// </remarks>
    private static void Everything(StringBuilder page, SolutionModel model, FindingSet findings)
    {
        page.Append("<h2>Everything else</h2>\n");

        // One derivation, and that is the point of computing it this way rather than the shorter
        // way. This read `findings.All.Select(f => f.Kind).Distinct().Count()` for the kind count
        // and `findings.Count` for the total, while the list immediately below enumerated
        // Selection.Exemplars — two derivations of one population, agreeing by coincidence. That
        // is that shape exactly: a caption asserting a correspondence
        // with the thing under it that nothing guarantees. Both numbers now come off the same
        // enumeration the list does, so the sentence cannot describe a different set from the one
        // it introduces.
        var enumerated = Selection.Exemplars(findings);
        var kinds = enumerated.Count;
        var counted = enumerated.Sum(exemplar => findings.OfKind(exemplar.Kind).Count);

        page.Append($"<p class=\"lede\">This run made {Html.Count(counted)} findings across ");
        page.Append($"{Html.Count(kinds)} kinds, and the page above leads with the strongest of each.</p>\n");

        // A13 tier 3's headline: per-kind counts and what each kind means, which is where the
        // within-solution baseline becomes visible. 103 concealed decisions against 3,209 types is
        // a different statement from 103 against 300, and until this shipped the page said neither
        // — the count of a kind was only ever visible beside the one row that led it.
        //
        // Rarest first, from the same selection the claims above use, so the page has one ordering
        // rather than two. Coverage is in this list and not in that one: a census of what fired is
        // exactly where a disclosure belongs, and Claims.IsRiskClaim carries why it is not a lead.
        page.Append("<h3>What fired, and what each kind means</h3>\n");
        page.Append("<p class=\"sub\">Every kind this run found, rarest first — the same ordering the claims ");
        page.Append("above use, and for the same reason it is not a severity.</p>\n");

        page.Append("<ul class=\"sub\">\n");
        foreach (var exemplar in enumerated)
        {
            var total = findings.OfKind(exemplar.Kind).Count;

            page.Append($"<li><strong>{Html.Count(total)} {Html.Text(Claims.KindName(exemplar.Kind))}</strong> — ");
            page.Append($"{Html.Text(Claims.KindBlurb(exemplar.Kind))}</li>\n");
        }
        page.Append("</ul>\n");

        // This said the exports carry "every finding", and no export carries
        // any: the JSON document's twelve top-level keys are model, and --full is prose capped at
        // --top. What is complete is the counting above, so that is what the sentence claims now.
        page.Append("<p class=\"lede\"><strong>Nothing above is a quiet subset</strong> — the count beside each ");
        page.Append("kind is every finding of it this run made. What is capped is the enumeration, not the ");
        page.Append("counting:</p>\n");

        page.Append("<ul class=\"sub\">\n");
        page.Append("<li><span class=\"mono\">--json</span> — the model every claim was computed from: ")
            .Append("each type, member and dependency, with a schema version. It carries the model ")
            .Append("rather than the claims.</li>\n");
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

    private static void Orientation(StringBuilder page, SolutionModel model, Judgement judgement)
    {
        page.Append("<h2>Orientation</h2>\n");

        Diagram(page, model);

        // The mosaic, below the graph — X11. It stopped being the report's argument the moment the
        // plot took that job, and what is left is the one claim it makes better than any number:
        // every type this run analysed is on the page, and most of it is pale. It sits under the
        // project graph because both are pictures of the whole solution, and the graph is the one
        // people ask for.
        Census(page, model, judgement.Reported);

        Projects(page, model);
        Integrations(page, model);
        Cycles(page, model, judgement);
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

        // Both sentences are about what the drawing is not saying,
        // and both numbers come from the model rather than from counting the SVG, so the caption
        // cannot drift away from the picture it explains -- renderer drift in the direction where
        // the prose is the half that goes stale.
        if (graph.Implied > 0)
            page.Append("<p class=\"sub\">Lines show the dependencies that carry the shape: ")
                .Append(Html.Text(Sentences.Plural(graph.Implied, "further dependency")))
                .Append($" {Sentences.Do(graph.Implied, "runs", "run")} between boxes ")
                .Append("that a path here already joins, so a line for each would cross the ")
                .Append("boxes in between and add no reachability.</p>\n");

        if (ArchitectureDiagram.Wraps(graph))
            page.Append("<p class=\"sub\">A layer too wide for one row is drawn as several, set ")
                .Append("close together with no rule between them. The dashed rules mark the ")
                .Append("boundaries that are real: everything above one depends on something ")
                .Append("below it.</p>\n");

        Key(page, model);

        page.Append("<div class=\"scroll\">\n").Append(ArchitectureDiagram.Render(model)).Append("</div>\n");

        // Saying that folding happened is not the same as saying what is
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

    /// <summary>
    /// What the map's tints mean, beside the map.
    /// </summary>
    /// <remarks>
    /// <b>Two defects with one fix.</b> The caption above already
    /// explains the direction convention and the folded boxes and said nothing about the colour;
    /// the definition sat 170 lines down under <c>Projects</c>, and <i>main sequence</i> — the
    /// measure both surfaces spell — was used twenty-five times on nopCommerce and never unpacked
    /// once. This is that sentence, at the first place the vocabulary appears.
    /// <para>
    /// <b>Only the tints that fired.</b> <see cref="ArchitectureDiagram.Tinted"/> reads the layout
    /// rather than the enum, so the key cannot define a colour that is not on the page — the
    /// <c>useless</c> tint is absent on nopCommerce, and a reader hunting a described colour they
    /// cannot find concludes they missed something.
    /// </para>
    /// </remarks>
    private static void Key(StringBuilder page, SolutionModel model)
    {
        var tinted = ArchitectureDiagram.Tinted(model);
        if (tinted.Count == 0) return;

        // The measure first and the colour second. Opening on the tint name asks a reader to
        // hold a phrase they cannot yet place; opening on the balance gives them somewhere to
        // put it. Singular throughout -- "a tinted box" reads correctly whether one zone fired
        // or both, so the sentence needs no count and cannot disagree with one.
        page.Append("<p class=\"sub\">A tinted box sits at an extreme of the <em>main sequence</em>, ");
        page.Append(Html.Text(Sentences.MainSequence)).Append(". ");

        foreach (var zone in tinted)
            page.Append("<em>").Append(Html.Text(Sentences.Capitalise(Sentences.Zone(zone)))).Append("</em> — ")
                .Append(Html.Text(Sentences.ZoneMeans(zone))).Append(". ");

        page.Append("The numbers behind the tint are in <em>Projects</em>, below.</p>\n");
    }

    private static void Projects(StringBuilder page, SolutionModel model)
    {
        page.Append("<h3>Projects</h3>\n");
        // The caption used to define a COLUMN in terms of the main sequence
        // and leave the term itself alone, so twenty-four of twenty-seven rows read "near the main
        // sequence" against no definition anywhere on the page.
        page.Append("<p class=\"sub\">I = Ce/(Ce+Ca), low means much depends on it. A = share of types that are ");
        page.Append("abstract or interfaces. The <em>main sequence</em> is ");
        page.Append(Html.Text(Sentences.MainSequence));
        page.Append(" — a project much depended on earns that by being abstract, and one that depends on much ");
        page.Append("can afford to be concrete. D = |A + I - 1|, how far off that balance it sits. ");
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
        MainSequenceZone.Pain => $"zone of pain — {Sentences.Zone(zone)}",
        MainSequenceZone.Uselessness => $"zone of uselessness — {Sentences.Zone(zone)}",
        _ => Sentences.Zone(zone),
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

    private static void Cycles(StringBuilder page, SolutionModel model, Judgement judgement)
    {
        page.Append("<h3>Circular references</h3>\n");

        // Both halves off the judgement rather than off ShapedCycle.IsReportable, and by the same
        // helper the terminal renderer uses -- docs/ARCHITECTURE.md §11, argued in CycleViews. These
        // two sections agreeing was a coincidence of both re-deriving one shape test.
        var shaped = model.ShapedNamespaceCycles;
        var reportable = CycleViews.Reported(
            judgement, FindingKind.NamespaceCycle, shaped, c => c.Cycle.Subject);
        var held = reportable.ToDictionary(c => c.Cycle, c => c);

        CycleGroup(page, "Namespaces", [.. reportable.Select(c => c.Cycle)],
            // The blurb made the holding claim over every name listed
            // under it, and the list is a component: 363 names on Umbraco, 15 of them in a
            // holding pair. Holding is now claimed where Holding() states it, over the set
            // that holds.
            "Namespaces that reach each other, and the sibling pairs inside them that hold "
            + "each other as state. The pairs are the part that no file move fixes.",
            id => Name(model, id),
            cycle => held.TryGetValue(cycle, out var shapedCycle) ? Holding(shapedCycle) : []);

        NotLayering(
            page,
            CycleViews.Suppressed(judgement, FindingKind.NamespaceCycle, shaped, c => c.Cycle.Subject),
            id => Name(model, id));

        // Off the finding, through CycleEvidence, exactly as the terminal renderer does it. Both
        // used to recompute this from model.Edges independently, which is the arrangement
        // that let the two renderers drift apart.
        CycleGroup(page, "Projects",
            CycleViews.Reported(judgement, FindingKind.ProjectCycle, model.ProjectCycles, c => c.Subject),
            "Two projects each naming a type in the other. Legal MSBuild — only project references cannot cycle.",
            id => Name(model, id),
            cycle => CycleEvidence
                .ProjectLinks(model, Relations(judgement, FindingKind.ProjectCycle, cycle))
                .Take(6)
                .Select(link => $"{link.From} → {link.To}: {link.Weight} reference(s)"
                                + (link.Example is { } via
                                    ? $" — heaviest: {Name(model, via.From)} → {Name(model, via.To)}"
                                    : "")));

        var holds = model.ShapedTypeTangles.ToDictionary(t => t.Tangle, t => t);

        CycleGroup(page, $"Type tangles ({Html.Number(model.Policy.MinTangle)}+)",
            CycleViews.Reported(judgement, FindingKind.TypeTangle, model.TypeTangles, c => c.Subject),
            "Groups of types that all reach each other, so none of them can be tested or changed alone.",
            id => Name(model, id),
            cycle => holds.TryGetValue(cycle, out var shaped)
                ? [Holds(shaped, Relations(judgement, FindingKind.TypeTangle, cycle), id => Name(model, id))]
                : []);
    }

    /// <summary>
    /// The components that are real and are not findings, with what closes each one.
    /// </summary>
    /// <remarks>
    /// Rendered rather than counted, for the reason the text report lists them: the suppression
    /// is the part of this section most likely to be wrong, and a reader who cannot see what was
    /// set aside cannot disagree with it. Kept visually quieter than the findings above, because
    /// they are not lesser findings — they are components about which there is nothing to do.
    /// </remarks>
    private static void NotLayering(
        StringBuilder page,
        IReadOnlyList<(ShapedCycle Shape, Judged Judged)> cycles,
        Func<SubjectRef, string> name)
    {
        if (cycles.Count == 0) return;

        page.Append("<p><strong>Mutually dependent, not reported</strong> — <span class=\"sub\">")
            .Append("these components are real, and none of them is a layering problem. ")
            .Append("The assembly is what gets extracted.</span></p>\n");

        page.Append("<ul class=\"sub\">\n");

        foreach (var (cycle, judged) in cycles)
        {
            var label = cycle.Anchor ?? name(cycle.Cycle.Members[0]);

            // The row that fired, not the shape it happens to test -- the same switch the terminal
            // renderer makes, on the same input, which is what stops the two drifting.
            var reason = judged.SilencedBy?.Name switch
            {
                "cycle-is-folder-layout" => $"one assembly's own folders, all in {cycle.Projects[0]}",
                "cycle-is-shared-types" => "peers naming each other's entities or models, holding none of them",
                _ => "not reported",
            };

            page.Append($"<li>{Html.Text(label)} — {Html.Count(cycle.Cycle.Size)} namespace(s), ")
                .Append($"{Html.Text(reason)}</li>\n");
        }

        page.Append("</ul>\n");
    }

    /// <summary>
    /// The sibling pairs that hold a namespace cycle together, heaviest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cycle above this is a membership — thirty names and one example loop on nopCommerce —
    /// and a membership is not somewhere to start. These pairs are, and they are the reason
    /// <see cref="ShapedCycle.Pairs"/> is computed at all: the heaviest two namespaces are where
    /// the entanglement actually is, and breaking one pair is a Monday-morning-sized piece of work
    /// where breaking a thirty-namespace component is not.
    /// </para>
    /// <para>
    /// <b>Why this exists at all, which is worth recording.</b> The commit that set the folder-layout
    /// cycles aside as not-findings touched both renderers, and gave the pairs to the text report
    /// only. So this section lost twenty-one entries here and gained nothing back, while the text
    /// report lost the same twenty-one and gained the evidence — and the HTML is the artifact a
    /// newcomer is handed with the <c>.txt</c> withheld. A suppression is allowed to make a section
    /// shorter; it is not allowed to make it emptier in one renderer than the other.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Holding(ShapedCycle cycle)
    {
        // The bound first, in the model's words, so this renderer and the
        // text one cannot state the two populations differently.
        yield return Sentences.CoupledWithin(cycle.Coupled.Count, cycle.Cycle.Size, cycle.Pairs.Count);

        // Six, as the text report shows six: enough to see where the weight sits without the pair
        // list outgrowing the membership it is evidence for. The remainder is stated rather than
        // dropped, which is the rule for every capped list.
        foreach (var pair in cycle.Pairs.Take(6))
            yield return $"{pair.First} ↔ {pair.Second} — {pair.Weight} held reference(s)";

        if (cycle.Pairs.Count > 6)
            yield return $"({cycle.Pairs.Count - 6} more mutually-holding pairs in this cycle)";
    }

    /// <summary>
    /// What holds a tangle together, worded as the text report words it.
    /// </summary>
    /// <remarks>
    /// Duplicated deliberately rather than shared through a helper the two renderers both call:
    /// the text section builds its line with the text section's idioms, and the one thing that
    /// must not drift is the judgement, which is in Core. If these sentences diverge in phrasing
    /// nothing breaks; if the shape did, that would be two reports disagreeing about the code.
    /// </remarks>
    /// <summary>
    /// A cycle finding's relations, asked of every judgement rather than of the reported half.
    /// <c>StructureSections</c> carries the same helper and the same reason.
    /// </summary>
    private static IReadOnlyList<Relation> Relations(Judgement judgement, FindingKind kind, Cycle cycle) =>
        judgement.All
            .Select(j => j.Finding)
            .FirstOrDefault(f => f.Kind == kind && f.Subject.Equals(cycle.Subject))?.Relations ?? [];

    private static string Holds(
        ShapedTangle tangle, IReadOnlyList<Relation> relations, Func<SubjectRef, string> name)
    {
        if (tangle.Shape == TangleShape.Hierarchy)
            return "A type hierarchy: set the inheritance aside and nothing mutually dependent "
                   + "is left, so this is a base and its own implementations.";

        var kinds = string.Join(", ", tangle.Kinds.Take(3));
        var held = kinds.Length == 0 ? "Held by references the walk could not attribute" : $"Held by {kinds}";

        return CycleEvidence.HeaviestPair(relations) is { } pair
            ? $"{held}; heaviest: {name(pair.First)} ↔ {name(pair.Second)}, {pair.Weight} reference(s)."
            : held + ".";
    }

    private static void CycleGroup(
        StringBuilder page, string title, IReadOnlyList<Cycle> cycles, string blurb,
        Func<SubjectRef, string> name, Func<Cycle, IEnumerable<string>>? annotate = null)
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

            foreach (var line in annotate?.Invoke(cycle) ?? [])
                page.Append($"<p class=\"sub\">{Html.Text(line)}</p>\n");
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

        // This used to justify itself with an anecdote about another
        // solution; it states the outcome of this run instead.
        page.Append(coverage.ProjectsNotLoaded.Count == 0
            ? "<li>Every project selected for analysis produced a compilation.</li>\n"
            : $"<li><strong>{Html.Count(coverage.ProjectsNotLoaded.Count)} project(s) did not load</strong> — "
              + Html.Text(string.Join(", ", coverage.ProjectsNotLoaded))
              + ". Fan-in understates everywhere they are referenced, so read every number as a "
              + "lower bound.</li>\n");

        // Directly after the compilation sentence, because that is the
        // sentence it qualifies -- a compilation with unresolved references is still a
        // compilation, so "produced a compilation" is true and cannot mean what it looks like.
        page.Append(coverage.ProjectsWithUnresolvedReferences.Count == 0
            ? "<li>Every project resolved every reference it names.</li>\n"
            : $"<li><strong>{Html.Count(coverage.ProjectsWithUnresolvedReferences.Count)} project(s) did not resolve every type they name</strong> — "
              + Html.Text(string.Join(", ", coverage.ProjectsWithUnresolvedReferences
                  .Select(p => $"{p.Project} ({Html.Count(p.Diagnostics)})")))
              + ". Edges are missing, so every number on this page is a lower bound. Usually an unrestored solution; sometimes a name the source uses and does not declare.</li>\n");

        if (coverage.LoadDiagnostics.Count > 0)
        {
            page.Append($"<li>{Html.Count(coverage.LoadDiagnostics.Count)} diagnostic(s) while loading. ");
            page.Append("MSBuild raises a package vulnerability advisory this way, so these are usually not ");
            page.Append("failures — the line above is what says whether anything was missed.</li>\n");
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

        // Claims.CompetesForLead, for the same reason the census above uses it: --full enumerates
        // the kinds this pane is the enumeration of, and a cycle's section is Circular references.
        // Without the filter the pane would render each cycle kind twice over — once here as a
        // list of set subjects with no card shape to put them in, and once in its own section.
        foreach (var group in findings.All
                     .Where(f => Claims.CompetesForLead(f.Kind))
                     .GroupBy(f => f.Kind)
                     .OrderBy(g => g.Key))
        {
            var all = group.ToList();
            var shown = all.Take(model.Policy.Top).ToList();

            page.Append($"<h3>{Html.Text(Claims.KindName(group.Key))} — {Html.Count(all.Count)}</h3>\n");
            page.Append($"<p class=\"sub\">{Html.Text(Claims.KindBlurb(group.Key))}</p>\n");

            // Decision X13: an absolute gate says what share it took here and
            // that the share does not travel. Only where the gate is a fixed count — on a
            // comparative gate the share IS the gate.
            if (Claims.GateIsAbsolute(group.Key))
                page.Append($"<p class=\"note\">{Html.Text(Claims.ShareCaveat(all.Count, model.Types.Count))}</p>\n");

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
            // This line used to join three facts with middots, and the third
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
            // Settled by A13 tier 3. The pane used to be headed "Why this
            // fired", which promises the explanation — and participants who opened it understood
            // LESS than before, because what it delivers is 65 field names. Two things changed and
            // neither is a rename of the sixty-five: tier 4 took the whole enumeration off the
            // default page, so nobody meets this without asking for it, and the summary now says
            // what the table is. A reader checking a claim wants these names unchanged, because
            // they are the join to the threshold table at the foot of the page; a reader trying to
            // understand the finding was never supposed to be sent here.
            page.Append("<details>\n<summary>The receipts behind this claim</summary>\n");
            page.Append("<p class=\"sub\">The tool's own field names, unchanged, so a number here can be matched ");
            page.Append("to the thresholds at the foot of the page and checked against the code. These are ");
            page.Append("evidence rather than an explanation — the sentence above is the claim, and nothing in ");
            page.Append("this table adds to it.</p>\n");
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
                     .Where(f => Claims.CompetesForLead(f.Kind))
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

    /// <summary>
    /// What the user's own file kept off this page — the same disclosure the terminal makes.
    /// </summary>
    /// <remarks>
    /// <b>Both surfaces or neither.</b> A shareable artifact that quietly omits what the person who
    /// ran it had already dismissed is the worse half of the pair: the reader of an HTML report is
    /// usually not the person who wrote the acknowledgment file. <c>Report.Acknowledged</c> carries
    /// the rest of the argument, including why nothing is said when there is nothing to say.
    /// </remarks>
    private static void Acknowledged(StringBuilder page, SolutionModel model, Judgement judgement)
    {
        var known = judgement.Acknowledgments;
        if (known.Count == 0) return;

        var silenced = judgement.AcknowledgedCount;
        var file = Acknowledgments.Naming(known.Path, model.SolutionPath);

        page.Append("<h2>Acknowledged</h2>\n");

        page.Append(silenced == 0
            ? $"<p class=\"lede\">Nothing was silenced by <span class=\"mono\">{Html.Text(file)}</span> "
              + $"this run, though it holds {Html.Count(known.Count)} "
              + $"{Sentences.Do(known.Count, "entry", "entries")}.</p>\n"
            : $"<p class=\"lede\">{Html.Count(silenced)} {Sentences.Do(silenced, "finding", "findings")} "
              + $"marked known and fine in <span class=\"mono\">{Html.Text(file)}</span> "
              + $"{Sentences.Do(silenced, "is", "are")} not shown on this page. Delete a line from that "
              + "file to see its finding again.</p>\n");

        if (judgement.Unmatched.Count == 0) return;

        page.Append($"<p class=\"sub\">{Html.Count(judgement.Unmatched.Count)} ")
            .Append($"{Sentences.Do(judgement.Unmatched.Count, "entry", "entries")} in that file matched ")
            .Append("nothing this run. A rename produces a new key, so a finding one of them silenced ")
            .Append("may be above under a new one.</p>\n");
    }

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

    /// <summary>
    /// What a reader could change, said in the row — and said as a provenance rather than
    /// as a category.
    /// </summary>
    private static string Provider(ExternalOrigin origin) => origin switch
    {
        ExternalOrigin.Framework => "ships with .NET",
        ExternalOrigin.Package => "a NuGet package",
        _ => "not determined",
    };

    private static string Display(SolutionModel model, SubjectRef subject)
    {
        if (model.Find(subject) is { } type) return type.Name;

        if (subject.DeclaringType is { } declaring && model.Find(declaring) is { } owner)
        {
            var member = owner.Members
                .FirstOrDefault(m => string.Equals(m.Subject.Canonical, subject.Canonical, StringComparison.Ordinal));

            // One rule for both renderers now. This medium had its own
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
        Qualifiers.PartOfAnUnreadGroup => "one of several unread on its type",
        Qualifiers.AnAttributeMayDirectIt => "an attribute may direct something to it",
        Qualifiers.TestUsageUnobservable => "test usage not visible here",
        Qualifiers.GloballyExtremeFanIn => "extreme fan-in solution-wide",
        Qualifiers.GloballyExtremeComplexity => "extreme complexity solution-wide",
        _ => qualifier,
    };
}

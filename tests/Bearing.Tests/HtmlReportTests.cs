using System.Text.RegularExpressions;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The shareable artifact — <c>TECHREQ-job-a.md</c> §6, shipped at A6.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot carries the wording. These assert the three promises the page makes that a
/// snapshot cannot see: that it asks the network for nothing, that a type name cannot break it,
/// and that it stays small enough to send.
/// </para>
/// <para>
/// <b>The size assertion is against the fixture and is therefore weak on its own.</b> What sets
/// the budget is a real solution — nopCommerce rendered at 2.4MB before the pane applied
/// <c>Top</c> and the drill-down dropped participants, and at 274KB after. The fixture cannot show
/// that; what it can do is fail if the shape of a row grows by an order of magnitude, which is the
/// regression this catches.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class HtmlReportTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private string Page => HtmlReport.Render(core.Model, Analysis.FindingsFor(core.Model), Instant);

    /// <summary>
    /// Every rule an inlined drawing brings with it is scoped to that drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §35. <b>An SVG <c>&lt;style&gt;</c> block inlined into HTML is
    /// page-wide, not local to its drawing</b>, and the last one wins. The page inlines three —
    /// the plot, the project map and the mosaic — so a class name used by two of them is one
    /// silently restyling the other. It happened: the plot's label class was <c>nm</c>, which is
    /// also the diagram's, and the two fought over font-size and weight.
    /// </para>
    /// <para>
    /// <b>This is the assertion the defect existed for want of.</b> Each drawing was well-formed,
    /// kept every element, and looked right when written standalone with <c>--mosaic</c> or
    /// <c>--diagram</c> — so nothing in the suite could see it, and the only symptom was on the
    /// composed page. Asserted here rather than in each drawing's own tests for the same reason:
    /// the fault is a property of the page, and a drawing on its own cannot have it.
    /// </para>
    /// <para>
    /// The rule is mechanical and so is the check: an inline <c>&lt;svg&gt;</c> carries a class on
    /// its root, and every selector in its stylesheet begins with it. That also makes the next
    /// drawing safe by construction rather than by everyone remembering.
    /// </para>
    /// </remarks>
    [Fact]
    public void Each_inlined_drawing_scopes_its_own_stylesheet()
    {
        var page = Page;
        var drawings = Regex.Matches(page, @"<svg\b[^>]*>.*?</svg>", RegexOptions.Singleline);

        Assert.True(drawings.Count >= 3, $"expected the page to inline three drawings, found {drawings.Count}");

        var checkedAny = false;

        foreach (Match drawing in drawings)
        {
            var style = Regex.Match(drawing.Value, @"<style>(.*?)</style>", RegexOptions.Singleline);
            if (!style.Success) continue;

            var root = Regex.Match(drawing.Value, @"<svg\b[^>]*\bclass=""([\w-]+)""");
            Assert.True(
                root.Success,
                "An inlined drawing carries a stylesheet but no class on its root, so every rule in "
                + "it applies to the whole page: " + Head(drawing.Value));

            var prefix = "." + root.Groups[1].Value + " ";

            // Selectors are what precede a declaration block; @media wrappers are stepped over.
            var body = style.Groups[1].Value.Replace("@media(prefers-color-scheme:dark){", string.Empty);

            foreach (Match rule in Regex.Matches(body, @"([^{}]+)\{"))
            {
                foreach (var selector in rule.Groups[1].Value.Split(','))
                {
                    var s = selector.Trim();
                    if (s.Length == 0 || s.StartsWith('@')) continue;

                    Assert.True(
                        s.StartsWith(prefix, StringComparison.Ordinal),
                        $"Rule \"{s}\" in the {root.Groups[1].Value} drawing is not scoped to \"{prefix.Trim()}\", "
                        + "so it applies to the whole page and to the other drawings on it.");
                    checkedAny = true;
                }
            }
        }

        // Guard against the loop passing by never running — every drawing having no stylesheet
        // would satisfy every assertion above.
        Assert.True(checkedAny, "no inlined drawing carried a stylesheet, so nothing was checked");
    }

    private static string Head(string svg) => svg.Length <= 120 ? svg : svg[..120] + "...";

    /// <summary>
    /// The page with every section enumerated — <c>--full</c>.
    /// </summary>
    /// <remarks>
    /// <b>A13 tier 2 moved the enumeration behind a flag</b>, so the assertions about cards,
    /// receipts and the drill-down read this rather than the default page. They are not weaker for
    /// it: what they cover is what <c>--full</c> renders, which is exactly the artifact those
    /// sections are. What the default page does instead is <c>HighlightsTests</c>'.
    /// </remarks>
    private string FullPage =>
        HtmlReport.Render(core.Model, Analysis.FindingsFor(core.Model), Instant, full: true);

    [Fact]
    public Task The_report_renders() => Verify(Page, extension: "html");

    /// <summary>
    /// The same page with every section enumerated — <c>--full</c>.
    /// </summary>
    /// <remarks>
    /// <b>A second snapshot rather than a wider first one, because the two are different
    /// artifacts.</b> A13 tier 2 moved the enumeration behind a flag, and snapshotting only the
    /// default page would leave the cards, the receipts table and the drill-down — the wording three
    /// of A11 round 1's defects were found in — with nothing watching them move.
    /// </remarks>
    [Fact]
    public Task The_full_report_renders() => Verify(FullPage, extension: "html");

    // ------------------------------------------------------------------ defect 26 ----

    /// <summary>
    /// A card names its peer group only where the finding consulted one, and the count is
    /// derived from the findings rather than read off the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §26.</b> The cohort used to be middot-joined to the project and
    /// the file location, which are addresses — so the population a claim is measured against read
    /// as a third address, and readers guessed at what it was for. It has its own line now, and it
    /// says what it is.
    /// </para>
    /// <para>
    /// <b>The half that is a correctness fix rather than a labelling one</b>: it was printed on
    /// every card, including the cohort-free findings. §3.6 to §3.9 carry <i>"no cohort required"</i>
    /// in their own headings, so a peer group on those cards claimed a relative reading the finding
    /// never made — defect 17's mistake in a different element. On this fixture that is most of
    /// them. Change cost is the one worth watching: it is solution-wide by X2's decision rather
    /// than cohort-relative, so it correctly names no peer group despite being about a population.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_card_names_a_peer_group_only_where_the_finding_used_one()
    {
        var findings = Analysis.FindingsFor(core.Model);

        // Mirrors two things the pane does for reasons of its own — it groups by kind and applies
        // Top, and it resolves a member subject to its declaring type. Neither is what this test
        // is about; both have to be reproduced or the count is of a different population. The
        // claim being pinned is only that the CohortSize receipt is what decides.
        var expected = findings.All
            .GroupBy(f => f.Kind)
            .SelectMany(g => g.Take(core.Model.Policy.Top))
            .Count(f =>
                f.Receipts.Any(r => string.Equals(r.Name, "CohortSize", StringComparison.Ordinal))
                && (core.Model.Find(f.Subject)
                    ?? core.Model.Find(f.Subject.DeclaringType ?? f.Subject)) is not null);

        var rendered = FullPage.Split("Compared against").Length - 1;

        Assert.True(expected > 0, "the fixture no longer nominates anything cohort-relative");
        Assert.True(
            findings.All.Count > expected,
            "the fixture no longer nominates anything cohort-free, so this asserts nothing");
        Assert.Equal(expected, rendered);
    }

    // ------------------------------------------------------------- self-contained ----

    /// <summary>
    /// Nothing on the page is fetched.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-a.md</c> §6's first requirement, and it is about corporate networks and
    /// offline use rather than about performance: a report whose stylesheet does not arrive is not
    /// a slower report, it is an unreadable one, and it fails in exactly the environment the
    /// artifact exists to be opened in. Asserted as an absence because that is what the promise is.
    /// </remarks>
    [Fact]
    public void The_page_requests_nothing_from_the_network()
    {
        var page = Page;

        var external = Regex.Matches(page, """(?:src|href)\s*=\s*["'](?!#)([^"']+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Empty(external);

        // No CSS fetch either: url() and @import are the two ways a stylesheet reaches out, and
        // the page's styles are inline where nobody looks for them.
        Assert.DoesNotContain("url(", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<image", page, StringComparison.OrdinalIgnoreCase);

        // A URL that is not in a fetching position. `xmlns="http://www.w3.org/2000/svg"` is an
        // XML namespace — an identifier that happens to be spelled as a URL, which nothing
        // resolves and no browser has ever requested — so the assertion is on where a URL
        // appears rather than on whether the characters are present anywhere.
        var fetched = Regex.Matches(page, """https?://""")
            .Count(m => !InAnXmlNamespace(page, m.Index));

        Assert.Equal(0, fetched);
    }

    private static bool InAnXmlNamespace(string page, int index)
    {
        var from = Math.Max(0, index - 40);
        return page[from..index].Contains("xmlns", StringComparison.Ordinal);
    }

    /// <summary>
    /// There is no script, and nothing that behaves like script.
    /// </summary>
    /// <remarks>
    /// Collapsing is <c>&lt;details&gt;</c>, so the page needs none — which means it prints, it
    /// survives script being disabled, and a proxy scanning attachments finds nothing to object
    /// to. Pinned rather than left as an implementation habit, because the first interactive
    /// feature anyone adds will reach for a <c>&lt;script&gt;</c> and this is where that decision
    /// should be made deliberately.
    /// </remarks>
    [Fact]
    public void The_page_runs_no_script()
    {
        Assert.DoesNotContain("<script", Page, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Regex.Matches(Page, """\son[a-z]+\s*=\s*["']""", RegexOptions.IgnoreCase));
    }

    /// <summary>The page opens and closes every element it opens.</summary>
    [Fact]
    public void The_markup_is_balanced()
    {
        string[] paired = ["html", "head", "body", "div", "table", "tr", "td", "th", "details", "p", "span"];

        foreach (var tag in paired)
        {
            var open = Regex.Matches(Page, $"<{tag}[ >]", RegexOptions.IgnoreCase).Count;
            var close = Regex.Matches(Page, $"</{tag}>", RegexOptions.IgnoreCase).Count;

            Assert.True(open == close, $"<{tag}> opened {open} times and closed {close}.");
        }
    }

    // ------------------------------------------------------------------- escaping ----

    /// <summary>
    /// A type name cannot break the page, and the fixture has one that would.
    /// </summary>
    /// <remarks>
    /// Generic arity renders as <c>&lt;T&gt;</c> in a display name, and an unescaped angle bracket
    /// does not visibly corrupt a page — it eats the rest of the line, which reads as a rendering
    /// glitch and hides a real component. Asserted by finding a name the model actually carries
    /// that needs escaping, so the test fails if the fixture stops containing one rather than
    /// passing vacuously.
    /// </remarks>
    [Fact]
    public void Every_angle_bracket_from_a_type_name_is_escaped()
    {
        var page = Page;

        // Whatever tags the renderer emits are known and closed; what must not appear is an
        // element name that is not one of them, which is what an unescaped name produces.
        var tags = Regex.Matches(page, "</?([a-zA-Z][a-zA-Z0-9]*)")
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        string[] known =
        [
            "doctype", "html", "head", "meta", "title", "style", "body", "div", "h1", "h2", "h3",
            "h4", "p", "b", "em", "strong", "span", "table", "tr", "td", "th", "ul", "li",
            "details", "summary", "footer", "br", "a", "code",

            // The embedded project map — A7 — and the plot and mosaic, X11 and A13 tier 1. Inline
            // SVG, so their elements are part of this document rather than a separate one, and they
            // belong on this list for the same reason the rest do: anything here that is not one of
            // these came out of a name.
            "svg", "rect", "text", "path", "line", "circle",
        ];

        Assert.Empty(tags.Except(known, StringComparer.Ordinal));
    }

    /// <summary>The escaper handles the four characters that matter, in both contexts.</summary>
    [Fact]
    public void The_escaper_covers_the_characters_a_name_can_contain()
    {
        Assert.Equal("List&lt;T&gt;", Html.Text("List<T>"));
        Assert.Equal("a &amp; b", Html.Text("a & b"));
        Assert.Equal("&quot;q&quot; &#39;s&#39;", Html.Text("\"q\" 's'"));

        // Ampersand first, or an escaped bracket is escaped a second time into &amp;lt;.
        Assert.Equal("&amp;lt;", Html.Text("&lt;"));
    }

    // ---------------------------------------------------------------- what it says ----

    /// <summary>
    /// The findings pane applies <c>Top</c> and says when it bit.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §3 in this medium. Core emits every finding and the renderer caps —
    /// and a cap that does not disclose is the probe's <c>Take</c>, which went unnoticed for
    /// months. Asserted at a <c>Top</c> low enough to bite on the fixture, since at the default
    /// nothing here truncates.
    /// </remarks>
    [Fact]
    public void A_capped_findings_list_says_what_it_dropped()
    {
        var model = core.WalkWith(AnalysisPolicy.Default with { Top = 2 });
        var page = HtmlReport.Render(model, Analysis.FindingsFor(model), Instant, full: true);

        Assert.Contains("Showing 2 of", page, StringComparison.Ordinal);
        Assert.Contains("--top", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The drill-down says it is not every type.
    /// </summary>
    /// <remarks>
    /// The same disclosure discipline for the other bounded list. It is bounded by the finding set
    /// rather than a cap, which is not obvious to a reader who counts the rows and finds fewer
    /// than the solution has types.
    /// </remarks>
    [Fact]
    public void The_drill_down_says_it_is_not_every_type()
    {
        Assert.Contains("This is not every type", FullPage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Findings are not ranked, and the page says so.
    /// </summary>
    /// <remarks>
    /// <c>docs/ARCHITECTURE.md</c> §4 excludes severity and rank from the finding record on
    /// purpose, so there is no honest order to sort by. A findings pane that silently listed them
    /// top-down would be read as ranked whatever the model says, which is a renderer manufacturing
    /// a judgement Core refused to make — so the absence is stated rather than left to be inferred.
    /// </remarks>
    [Fact]
    public void The_page_says_the_findings_are_not_ranked()
    {
        Assert.Contains("not ranked against each other", FullPage, StringComparison.Ordinal);
    }

    /// <summary>Every threshold the run used is on the page, all twenty-six.</summary>
    /// <remarks>
    /// The same commitment the command line makes: a threshold a finding cites but a reader cannot
    /// see is only half-exposed. Counted against <c>AnalysisPolicy.Values</c> so a value added to
    /// the policy and forgotten here fails.
    /// </remarks>
    [Fact]
    public void Every_threshold_is_listed()
    {
        var page = Page;

        foreach (var (name, _) in core.Model.Policy.Values)
        {
            Assert.Contains($">{name}<", page, StringComparison.Ordinal);
            Assert.Contains(CommandLine.FlagFor(name), page, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A receipt's gate is shown with the value it was actually tested against.
    /// </summary>
    /// <remarks>
    /// §6 — a claim whose basis is not available is worthless even when correct, and "cleared
    /// <c>HighCc</c>" without the number is not a basis. The number comes from the policy the model
    /// was produced under rather than from a copy, so the finding and the policy cannot disagree.
    /// </remarks>
    [Fact]
    public void A_receipt_names_its_gate_and_the_value_behind_it()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var gate = findings.All
            .SelectMany(f => f.Receipts)
            .Select(r => r.Gate)
            .First(g => g is not null)!;

        var value = core.Model.Policy.Values.First(v => string.Equals(v.Name, gate, StringComparison.Ordinal));

        Assert.Contains($">{gate}</span> = {Html.Number(value.Value)}", FullPage, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- the budget ----

    /// <summary>
    /// The page stays small enough to send.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §6 makes bundle size a real budget, and the budget exists to leave room for the diagrams A7
    /// and A8 will inline — a shell that has already spent it is a shell those cannot land in.
    /// </para>
    /// <para>
    /// <b>The number that matters is a real solution's</b>, and this is not one: nopCommerce
    /// rendered at 2.4MB before the pane applied <c>Top</c> and the drill-down stopped carrying
    /// participants, and at 274KB after. On 145 types this can only catch a row or a card growing
    /// by an order of magnitude, which is worth catching and is not the same assurance.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fixtures_page_stays_within_a_sane_size()
    {
        Assert.True(Page.Length < 250_000, $"The fixture's report is {Page.Length:N0} characters.");
    }

    /// <summary>The written file is UTF-8 with no byte-order mark.</summary>
    /// <remarks>
    /// The page declares <c>&lt;meta charset="utf-8"&gt;</c>, so a BOM is at best redundant — and
    /// a browser reading a BOM before the doctype is one of the ways a page silently drops into
    /// quirks mode.
    /// </remarks>
    [Fact]
    public void The_written_file_has_no_byte_order_mark()
    {
        var directory = Directory.CreateTempSubdirectory("bearing-html");
        try
        {
            var path = Path.Combine(directory.FullName, "report.html");
            HtmlReport.Write(path, core.Model, Analysis.FindingsFor(core.Model), Instant);

            Assert.NotEqual<byte[]>([0xEF, 0xBB, 0xBF], File.ReadAllBytes(path).Take(3).ToArray());
            Assert.StartsWith("<!doctype html>", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------------ defect 27 ----

    /// <summary>
    /// The receipts table is reachable only where a reader asked for everything, and it says it is
    /// evidence rather than an explanation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §27, settled at A13 tier 3.</b> The pane was headed <i>"why this
    /// fired"</i> — the promise of an explanation over a table of 65 field names, which
    /// participants said left them understanding less than before. The names stay unchanged,
    /// because they are the join to the threshold table at the foot of the page and renaming them
    /// would break the one thing the pane is good for. What changed is where it lives and what it
    /// claims to be.
    /// </para>
    /// <para>
    /// <b>Asserted as an absence on the default page as well as a presence on <c>--full</c></b>,
    /// because half of this fix is tier 4's and a page that started rendering cards again would
    /// undo it silently.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_receipts_table_is_evidence_and_only_where_it_was_asked_for()
    {
        // The threshold table at the foot is also a <details> and is on both pages on purpose,
        // so this names the pane rather than the element.
        Assert.DoesNotContain("The receipts behind this claim", Page, StringComparison.Ordinal);
        Assert.DoesNotContain("Why this fired", FullPage, StringComparison.Ordinal);

        Assert.Contains("<summary>The receipts behind this claim</summary>", FullPage, StringComparison.Ordinal);
        Assert.Contains("evidence rather than an explanation", FullPage, StringComparison.Ordinal);

        // The names themselves are deliberately not translated — this is what makes the table
        // checkable against the policy, and it is why the pane moved rather than being reworded.
        Assert.Contains("MaxMemberCyclomatic", FullPage, StringComparison.Ordinal);
    }
}

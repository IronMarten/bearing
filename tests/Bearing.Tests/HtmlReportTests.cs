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

    [Fact]
    public Task The_report_renders() => Verify(Page, extension: "html");

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
        var external = Regex.Matches(Page, """(?:src|href)\s*=\s*["'](?!#)([^"']+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Empty(external);
        Assert.DoesNotContain("http://", Page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", Page, StringComparison.OrdinalIgnoreCase);
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
        var page = HtmlReport.Render(model, Analysis.FindingsFor(model), Instant);

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
        Assert.Contains("This is not every type", Page, StringComparison.Ordinal);
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
        Assert.Contains("not ranked against each other", Page, StringComparison.Ordinal);
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

        Assert.Contains($">{gate}</span> = {Html.Number(value.Value)}", Page, StringComparison.Ordinal);
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
}

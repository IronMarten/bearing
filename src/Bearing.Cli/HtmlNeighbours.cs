using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// A8 — what a lead connects to, one hop out, grouped by project and complete.
/// </summary>
/// <remarks>
/// <para>
/// <b>A list and not a drawing, because the leads are the worst cases in the codebase.</b> A lead
/// is the top row of its kind, so its neighbourhood is the largest by construction — a one-hop
/// median of 66, 100 and 88 on the three reference solutions against 5-7 for types generally, and
/// <c>BaseEntity</c> at 458, <c>BaseItem</c> at 358, <c>UmbracoBuilderExtensions</c> at 708.
/// Drawing those is the hairball <c>TECHREQ-job-a.md</c> §5.5 exists to refuse; listing them is
/// 64-122 KB and withholds nothing. <see cref="Neighbourhoods"/> carries the derivation and
/// <c>MEASURE-ego-reach.md</c> the measurement.
/// </para>
/// <para>
/// <b>The project breakdown sits outside the fold and the type names inside it</b>, which is two
/// decisions rather than one. Four to eight groups per lead is a short index and is the
/// granularity §5.5's acceptance sentence actually asks about — <i>"what does this project depend
/// on, and what depends on it"</i> — so it is always visible. And <c>details</c> is
/// <c>display:none</c> under <c>@media print</c>, so anything that has to survive a screenshot
/// cannot live inside one. The type names can, because one group can hold 294 of them.
/// </para>
/// <para>
/// <b>Nothing here truncates, and the sentence says so.</b> The bar is A11 round 2's T5 — knowing
/// when an answer is finished, which is the only measured thing the report does that reading the
/// source does not. A count a reader cannot check against the list beneath it would defeat that,
/// so every group prints its count and every name is present.
/// </para>
/// <para>
/// <b>It renders under every lead, including the rail rows.</b> One drill-down per kind that fired
/// is the whole population — nine or ten on the reference solutions, not the 105-126 rendered
/// subjects and not the 349-808 nominated ones.
/// </para>
/// </remarks>
internal static class HtmlNeighbours
{
    /// <summary>Renders the neighbourhood block, or nothing if the subject has no type node.</summary>
    internal static void Render(StringBuilder page, SolutionModel model, Finding finding)
    {
        if (Neighbourhoods.Of(model, finding.Subject) is not { } hood) return;

        page.Append("<div class=\"lbl\">what it connects to</div>\n");
        page.Append("<div class=\"fld nb\">\n");
        page.Append("<p class=\"sub\">One hop out, and complete — every name is listed and nothing ");
        page.Append("is capped.</p>\n");

        var name = hood.Subject.Name;

        Direction(page, $"{name} depends on", hood.DependsOn, hood.DependsOnCount);
        Direction(page, $"{name} is depended on by", hood.DependedOnBy, hood.DependedOnByCount);

        page.Append("</div>\n");
    }

    private static void Direction(
        StringBuilder page,
        string heading,
        IReadOnlyList<NeighbourGroup> groups,
        int total)
    {
        page.Append("<p class=\"nbh\"><b>");
        page.Append(Html.Text(heading));
        page.Append("</b> — ");

        if (total == 0)
        {
            page.Append("nothing in this solution.</p>\n");
            return;
        }

        page.Append(Html.Count(total));
        page.Append(' ');
        page.Append(Sentences.Do(total, "type", "types"));
        page.Append(" in ");
        page.Append(Html.Count(groups.Count));
        page.Append(' ');
        page.Append(Sentences.Do(groups.Count, "project", "projects"));
        page.Append(": ");
        page.Append(string.Join(", ", groups.Select(Chip)));
        page.Append("</p>\n");

        page.Append("<details class=\"nbd\"><summary>the ");
        page.Append(Html.Count(total));
        page.Append(' ');
        page.Append(Sentences.Do(total, "name", "names"));
        page.Append("</summary>\n");

        foreach (var group in groups)
        {
            page.Append("<p class=\"nbt\"><b>");
            page.Append(Html.Text(group.Project));
            page.Append("</b> ");
            page.Append(string.Join(" · ", group.Types.Select(t => Html.Text(t.Name))));
            page.Append("</p>\n");
        }

        page.Append("</details>\n");
    }

    private static string Chip(NeighbourGroup group) =>
        $"{Html.Text(group.Project)} <span class=\"nbc\">{Html.Count(group.Types.Count)}</span>";
}

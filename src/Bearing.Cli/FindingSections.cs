namespace IronMarten.Bearing.Cli;

/// <summary>
/// Job B's sections: the claims, laid out.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wording moved to <see cref="Claims"/> at A13 tier 2; the layout stayed here.</b> Every
/// section used to weave the two together, which was fine for as long as one renderer printed
/// them — the page then needed the same claims in a different shape, and a second copy of a
/// sentence is a sentence that will disagree with itself. What is left in this file is the
/// sequence, the headings, the caps and the two sections whose *structure* is the finding.
/// </para>
/// <para>
/// Nothing here decides whether a claim may be made, because suppression settled that before the
/// set arrived, and nothing re-derives a number from the model.
/// </para>
/// <para>
/// <b>Where this deliberately differs from the probe</b>: defect 16 (the god-object sentence is
/// chosen from the qualifier that actually holds), defect 17 (the coverage section asks the
/// finding set instead of asserting an absence), defect 11's layer-span wording, defect 3 (every
/// capped list says what it dropped) and defect 32 (a verb agrees with a number a real solution
/// made singular). Everything else is the probe's voice, on purpose — so that a reader comparing
/// the two sees one tool, and every difference is one that was chosen.
/// </para>
/// </remarks>
internal static class FindingSections
{
    internal static IEnumerable<string> ConcealedDecisionAtTypeLevel(
        SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CONCEALED DECISION ------------------------------------------";
        yield return "   (complexity far above peers, connectivity ordinary)";

        var found = findings.OfKind(FindingKind.ConcealedDecisionType);
        if (found.Count == 0)
            yield return "   (none — no type's complexity stands that far above its peers)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> ConcealedDecisionAtMethodLevel(
        SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CONCEALED DECISION, METHOD LEVEL ----------------------------";

        var found = findings.OfKind(FindingKind.ConcealedDecisionMethod);
        if (found.Count == 0)
            yield return "   (none — no method's complexity stands that far above its peers)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> BlastRadius(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- BUG BLAST RADIUS --------------------------------------------";
        yield return "   (widely depended on AND internally complex)";

        var found = findings.OfKind(FindingKind.BugBlastRadius);
        if (found.Count == 0)
            yield return "   (none — nothing is both widely depended on and internally complex)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> ChangeCost(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- CHANGE COST -------------------------------------------------";
        yield return "   (many internal callers on a contract-shaped type)";

        var found = findings.OfKind(FindingKind.ChangeCost);
        if (found.Count == 0)
            yield return "   (none — no contract carries enough of the solution's callers)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> LoadBearing(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- LOAD-BEARING AND INTRICATE (no cohort required) -------------";
        yield return $"   (instability <= {Sentences.Number(model.Policy.StableThreshold)} — much depends on it, it depends on";
        yield return $"    little — AND a method above cc {model.Policy.HighCc})";

        var found = findings.OfKind(FindingKind.LoadBearingAndIntricate);
        if (found.Count == 0) yield return "   (none)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> BreaksAlone(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- BREAKS ALONE (no cohort required) ---------------------------";
        yield return "   (complex, but almost nothing depends on it — the reassuring message)";

        var found = findings.OfKind(FindingKind.BreaksAlone);
        if (found.Count == 0) yield return "   (none)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    internal static IEnumerable<string> HubsAndGodObjects(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- HUBS AND GOD OBJECTS (no cohort required) -------------------";
        yield return $"   (fan-in AND fan-out both >= {model.Policy.HubMin} — a ratio cannot see these, since";
        yield return "    high-in + high-out lands mid-range, same as a trivial one-in one-out leaf.";

        // Both counts are on every row and neither of them is the sort key, so the order is
        // invisible without this line: nopCommerce puts fan-in 89 third, under 28 and 24, which
        // reads as a mistake. Said once per section rather than added to every row, because the
        // rows are already the densest thing in the report.
        yield return "    Ordered by the smaller of the two: a thing is only as much a hub as its narrower side.)";

        var found = findings.OfKind(FindingKind.HubOrGodObject);
        if (found.Count == 0) yield return "   (none)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;

        if (found.Count > 0)
        {
            yield return "   NOTE: routers, mediators and composition roots legitimately live here. That";
            yield return "   does not make the flag wrong — those are exactly the things not to change";
            yield return "   lightly. Mark the known ones rather than tuning them away.";
        }
    }

    internal static IEnumerable<string> SharedMutableState(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- SHARED MUTABLE STATE (no cohort required) -------------------";
        yield return "   (writes to static mutable state — every caller on every thread shares these)";

        var found = findings.OfKind(FindingKind.SharedMutableState);
        if (found.Count == 0) yield return "   (none)";

        foreach (var line in Rows(model, found, model.Policy.Top)) yield return line;
    }

    /// <summary>
    /// Members nothing in the solution refers to — <c>TECHREQ-job-a.md</c> §5.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The heading, the rows and the note are all constrained by one rule</b>: §5.6 forbids the
    /// word "dead" and forbids implying safety, so nothing here says unused, unreachable or
    /// removable. The section says what was looked for, what was found, and what could not be
    /// looked at.
    /// </para>
    /// <para>
    /// <b>The exclusion counts are the section, not a footnote under it.</b> They remove 98–99% of
    /// the members that have no inbound reference — 3,837 down to 48 on Jellyfin — and a reader
    /// shown the survivors without being shown that number has been handed a list they cannot
    /// calibrate. It is the same argument as the coverage section's, at the scale of one finding:
    /// invariant 8, silence is not a clean bill of health.
    /// </para>
    /// <para>
    /// <b>The categories are counted independently and say so.</b> A public override implementing
    /// an interface member is in three of them, so they do not add up to the total removed, and a
    /// reader doing the arithmetic and finding it wrong would be right to distrust the rest.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string> NoStaticReferencesFound(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- NO STATIC REFERENCES FOUND (no cohort required) -------------";
        yield return "   (nothing in this solution refers to these members. VERIFY BEFORE DELETING —";
        yield return "    what this analysis cannot see is named on each row.)";

        var excluded = NoStaticReferences.Excluded(model);
        var found = findings.OfKind(FindingKind.NoStaticReferences);

        if (excluded.Considered == 0)
        {
            yield return "   (none — every member this run analysed is referred to by something)";
            yield break;
        }

        if (found.Count == 0) yield return "   (none)";

        // Collapsed and individual are the same claim grouped two ways, and which one applies is
        // the qualifier's answer rather than a count taken here — SpansArchitecturalLayers'
        // precedent, and docs/ARCHITECTURE.md §3's rule that a rule enforced in a renderer is a
        // rule that does not exist. Seventeen of SearchResult's twenty-three properties having no
        // reader is one thing worth knowing, not seventeen.
        var grouped = found.Where(f => f.Holds(Qualifiers.PartOfAnUnreadGroup)).ToList();
        var separate = found.Where(f => !f.Holds(Qualifiers.PartOfAnUnreadGroup)).ToList();

        foreach (var line in Carriers(model, grouped, model.Policy.Top)) yield return line;
        foreach (var line in Rows(model, separate, model.Policy.Top)) yield return line;

        yield return "";
        yield return $"   {Sentences.Plural(excluded.Considered, "member")} had no inbound reference. "
                     + $"{Sentences.Whole(excluded.Excluded)} of those are not asked about,";
        yield return "   because being referred to by nothing here is what each of them looks like:";
        yield return $"     {excluded.RuntimeInvoked,6}  invoked by the runtime — an entry point, or a static constructor";
        yield return $"     {excluded.InterfaceImplementations,6}  implements an interface — callers reach it through the contract";
        yield return $"     {excluded.Overrides,6}  overrides a base member — same";
        yield return $"     {excluded.ExternallyVisible,6}  visible outside this assembly — the caller may not be in this solution";
        yield return $"     {excluded.SoleConstructors,6}  the type's only constructor — whatever creates the type calls it";
        yield return "   (a member can be in several of those, so they do not sum to the number set aside)";
    }

    /// <summary>
    /// One row per type whose data members are mostly unread, rather than one row per member.
    /// </summary>
    /// <remarks>
    /// <b>Ordered by how much of the type is unread, not by how many members it has.</b> A carrier
    /// where every field is unread is a stronger claim than one where a third are, and the raw
    /// count would put a large half-read type above a small wholly-unread one.
    /// </remarks>
    private static IEnumerable<string> Carriers(SolutionModel model, IReadOnlyList<Finding> grouped, int top)
    {
        var carriers = grouped
            .GroupBy(f => f.Subject.DeclaringType?.Canonical ?? "", StringComparer.Ordinal)
            .Select(g => new
            {
                Type = model.Find(g.First().Subject.DeclaringType ?? g.First().Subject),
                Unread = g.Count(),
                Total = (int)(g.First().ValueOf("DeclaringTypeDataMembers") ?? 0),
            })
            .Where(c => c.Type is not null && c.Total > 0)
            .OrderByDescending(c => (double)c.Unread / c.Total)
            .ThenByDescending(c => c.Unread)
            .ThenBy(c => c.Type!.Name, StringComparer.Ordinal)
            .ToList();

        if (carriers.Count == 0) yield break;

        var (shown, disclosure) = Sentences.Cap(carriers, top, "type");

        // Said once, above the group, because it is true of every row in it. The first draft put
        // these three lines under each carrier and printed them four times on Jellyfin, which is
        // the section's own repetition problem one level up.
        yield return "   Types whose data members are mostly unread, named once rather than per member —";
        yield return "   something writes them that this analysis cannot see: a serialiser, an ORM, a binder.";

        foreach (var carrier in shown)
        {
            yield return $"     {carrier.Type!.Name} — {carrier.Unread} of its "
                         + $"{Sentences.Plural(carrier.Total, "data member")} "
                         + $"{Sentences.Do(carrier.Unread, "has", "have")} no reader.";
        }

        foreach (var line in disclosure) yield return line;
        yield return "";
    }

    internal static IEnumerable<string> SpansArchitecturalLayers(SolutionModel model, FindingSet findings)
    {
        yield return "";
        yield return "-- SPANS ARCHITECTURAL LAYERS (no cohort required) -------------";
        yield return $"   (dependencies reaching across {model.Policy.MinKindSpan}+ architectural kinds — cross-cutting";
        yield return "    work, whatever the component is named)";

        var found = findings.OfKind(FindingKind.SpansArchitecturalLayers);
        if (found.Count == 0)
        {
            yield return "   (none)";
            yield break;
        }

        // Collapsed and detailed findings are the same claim worded two ways, and which one
        // applies is the qualifier's answer rather than a count taken here. docs/DEFECTS.md §11:
        // a pattern is a shared dependency set, so the renderer can no longer decide this by
        // grouping on the kind signature.
        var collapsed = found.Where(f => f.Holds(Qualifiers.PartOfALayeringPattern)).ToList();
        var detailed = found.Where(f => !f.Holds(Qualifiers.PartOfALayeringPattern)).ToList();

        foreach (var group in collapsed
                     .GroupBy(f => Signature(model, f), StringComparer.Ordinal)
                     .OrderBy(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var names = group
                .Select(f => model.Find(f.Subject)?.Name ?? "")
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var examples = names.Take(4).ToList();

            yield return $"   {group.Count()} types span {group.Key} — a layering pattern rather than an";
            yield return $"     anomaly. Examples: {string.Join(", ", examples)}"
                         + (names.Count > examples.Count
                             ? $" — {examples.Count} of {names.Count} named"
                             : "");
        }

        // The one section whose rows are not one line, because §3.1 says the per-kind breakdown
        // IS the finding. The headline is Claims'; everything under it is this section's.
        foreach (var finding in detailed)
        {
            if (model.Find(finding.Subject) is not { } type) continue;

            var claim = Claims.For(model, finding);
            yield return $"   {claim.Subject} — {claim.Sentence}:";

            foreach (var line in ByKind(model, finding, type)) yield return line;

            yield return "       Check that the name still describes what it does.";
        }
    }

    /// <summary>
    /// How many dependency names one kind may show before the line stops being readable.
    /// </summary>
    private const int NamesPerKind = 6;

    /// <summary>
    /// The named dependencies, grouped under the architectural kind each one belongs to.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-b.md</c> §3.1: the per-kind breakdown <i>is</i> the finding. "Spans three
    /// kinds" is arguable and a reader cannot check it; <i>"why is authentication calling
    /// TenantStore?"</i> is neither. The kinds come from the participants' own classifications
    /// rather than from the finding, because a participant is a subject and its role is a
    /// property of that subject.
    /// </remarks>
    private static IEnumerable<string> ByKind(SolutionModel model, Finding finding, TypeNode type)
    {
        var byKind = finding.Participants
            .Select(model.Find)
            .Where(t => t is not null)
            .GroupBy(t => t!.Classification.Kind, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t!.Name).Distinct(StringComparer.Ordinal)
                      .OrderBy(n => n, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var kinds = new SortedSet<string>(byKind.Keys, StringComparer.Ordinal);

        // The type's own role counts toward the span. Where it does, the span exceeds the number
        // of kinds reached through dependencies, and the difference is what "itself" names — the
        // component is cross-cutting partly by being where it is.
        if ((finding.ValueOf("KindSpan") ?? 0) > byKind.Count) kinds.Add(type.Classification.Kind);

        foreach (var kind in kinds)
        {
            if (!byKind.TryGetValue(kind, out var names))
            {
                yield return $"       {kind,-14} itself";
                continue;
            }

            var shown = names.Take(NamesPerKind).ToList();

            yield return $"       {kind,-14} {string.Join(", ", shown)}"
                         + (names.Count > shown.Count ? $" — {shown.Count} of {names.Count} shown" : "");
        }
    }

    /// <summary>The kind signature a collapsed group is named by.</summary>
    private static string Signature(SolutionModel model, Finding finding) =>
        string.Join("+", finding.Participants
            .Select(model.Find)
            .Where(t => t is not null)
            .Select(t => t!.Classification.Kind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal));

    /// <summary>
    /// A capped list of nominations, as the terminal prints them.
    /// </summary>
    /// <remarks>
    /// <b>The wording is <see cref="Claims"/>' and the layout is this file's</b>, which is the
    /// split that lets the page make the same claims without borrowing the fixed-width shape they
    /// were written for. The cap and its disclosure stay here because how many lines fit on a
    /// screen is a property of this medium — <c>docs/DEFECTS.md</c> §3.
    /// </remarks>
    internal static IEnumerable<string> Rows(
        SolutionModel model,
        IReadOnlyList<Finding> found,
        int top,
        string indent = "   ",
        string noun = "nomination")
    {
        var (shown, disclosure) = Sentences.Cap(found, top, noun, indent);

        foreach (var finding in shown)
        {
            var claim = Claims.For(model, finding);
            if (!claim.Exists) continue;

            var evidence = claim.Evidence.Length > 0 ? $" ({claim.Evidence})" : "";
            var trailer = claim.Trailer.Length > 0 ? $" — {claim.Trailer}" : "";

            yield return $"{indent}{claim.Subject} — {claim.Sentence}{evidence}{trailer}";
        }

        foreach (var line in disclosure) yield return line;
    }
}

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The risk highlights — A13 tier 2, and the first findings a reader meets.
/// </summary>
/// <remarks>
/// <para>
/// <b>What A11 round 1 measured is that nothing in any artifact does triage.</b> nopCommerce
/// renders 1,642 findings and 1,091 of them are one kind; every participant asked a version of
/// <i>"what am I supposed to do with this?"</i>, and the section that drew interest was the one
/// with a single finding in it. This is the answer: <see cref="Selection.Exemplars"/>, one per kind
/// that fired, rarest first, each carrying the sentence its section would have given it.
/// </para>
/// <para>
/// <b>They are labelled as risk because that is what the findings claim.</b> <c>TECHREQ-job-b.md</c>
/// §7.2 is <c>[proven]</c> and the sentences already assert it — <i>"looks like plumbing but is 37x
/// the median internal complexity of the 96 types deriving from BaseNopValidator"</i> is a risk
/// claim however it is filed. What the tool cannot do is order findings by severity <i>across</i>
/// kinds, which is a narrower thing than not asserting risk, and X10 records why.
/// </para>
/// <para>
/// <b>So the ordering is stated, every time, in the text.</b> A top-down list reads as ranked
/// whatever the model believes, and rarity is not severity — the rarest kind here is not the worst
/// one, it is the one whose section a reader can still read to the end. Saying so is the difference
/// between an order and a claim.
/// </para>
/// <para>
/// <b>Coverage is deliberately not in this list.</b> It is a disclosure rather than a claim, and
/// <see cref="Claims.IsRiskClaim"/> carries the reason. It keeps its own section, which is where
/// invariant 8 wants it.
/// </para>
/// </remarks>
internal static class Highlights
{
    /// <summary>The section, as the terminal prints it.</summary>
    /// <remarks>
    /// <b>Above everything, including the structure sections.</b> <c>PRD-free-tier.md</c> §7.3 asks
    /// this medium for <i>"findings first, no scrolling to reach the first useful line"</i>, and
    /// the report already led with findings — but the first line was one of 1,091 rows of one kind,
    /// which satisfies the letter of that and not one word of its intent.
    /// </remarks>
    internal static IEnumerable<string> For(SolutionModel model, FindingSet findings)
    {
        var leading = Selection.Exemplars(findings)
            .Where(f => Claims.IsRiskClaim(f.Kind))
            .ToList();

        yield return "";
        yield return "-- START HERE --------------------------------------------------";

        if (leading.Count == 0)
        {
            yield return "   (nothing was nominated — every threshold this run used is at the foot";
            yield return "    of this report, and the structure sections below still apply)";
            yield break;
        }

        yield return $"   {Sentences.Plural(leading.Count, "claim")}, one for each kind of risk this run found,";
        yield return "   ordered by how uncommon each kind is in this codebase. That is an ordering";
        yield return "   and not a severity: this tool has no way to say a hub is worse than a";
        yield return "   cycle, and does not pretend to.";
        yield return "";

        foreach (var finding in leading)
        {
            var claim = Claims.For(model, finding);
            if (!claim.Exists) continue;

            var total = findings.OfKind(finding.Kind).Count;

            yield return $"   {claim.Subject} — {claim.Sentence}";
            yield return $"     {Where(model, finding, claim)}{Rest(finding.Kind, total)}";
        }

        yield return "";
        yield return "   Each one is the strongest row of its section. The rest of that section is";
        yield return "   below, and every finding this run made is in --json and --csv.";
    }

    /// <summary>
    /// Which section this came from, and how much of it is not shown.
    /// </summary>
    /// <remarks>
    /// <b>The count is the honest half of a selection.</b> A lead item with nothing beside it reads
    /// as the only one of its kind, which is true of layer span on nopCommerce and false of the
    /// 1,091 concealed decisions — and telling those two apart is exactly the triage this section
    /// exists to do. <c>PRD-free-tier.md</c> §9's anti-metric is that more findings is worse, so a
    /// large number here is a thing to say plainly rather than to hide.
    /// </remarks>
    private static string Rest(FindingKind kind, int total) =>
        total == 1
            ? $" · {Claims.KindName(kind).ToLowerInvariant()}, and it is the only one"
            : $" · {Claims.KindName(kind).ToLowerInvariant()}, 1 of {Sentences.Whole(total)}";

    /// <summary>
    /// Where the component is, so the claim can be checked against the code.
    /// </summary>
    /// <remarks>
    /// One derivation, shared with the page — <see cref="Subjects.Where"/>, which carries the
    /// reason the claim's own location wins over the declaring type's.
    /// </remarks>
    private static string Where(SolutionModel model, Finding finding, Claim claim) =>
        Subjects.Where(model, finding, claim.Trailer);
}

namespace IronMarten.Bearing.Cli;

/// <summary>
/// Which findings a report leads with — decision X10, and the whole of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Findings are selected, never ranked, and the selection carries no constant.</b> There is no
/// order <i>across</i> kinds and there cannot be one: the only candidate common unit is extremity
/// within one's own cohort, and <c>TECHREQ-job-b.md</c> §3.6–§3.9 are cohort-free by design — load
/// bearing, breaks alone, hubs and god objects and shared mutable state all carry <i>"no cohort
/// required"</i> in their own headings. A cross-kind order would mean giving half the findings a
/// population they were deliberately built not to need. Shared mutable state is true or it is not;
/// it does not get truer by peer comparison.
/// </para>
/// <para>
/// <b>What ships instead:</b> one exemplar per kind that fired, rarest first, each being that
/// kind's own top row. <b>Rarity is an ordering and never a category</b> — nothing anywhere says
/// <i>"this fired rarely"</i>, so there is no threshold to define, tune or drift, and the number of
/// exemplars self-scales with how many kinds the run produced rather than with a cap. A11 round 1
/// is the evidence: the section that drew interest had <b>one</b> finding and was described as
/// sounding like the biggest problem to go look at, while the section with 1,091 was <i>"a wall of
/// text"</i>.
/// </para>
/// <para>
/// <b>Derived here, and tested as derived.</b> X10's own words: an order that is not computed from
/// the run is a constant wearing a sort's clothing. It lives in <c>Bearing.Cli</c> because it is a
/// render-time reading of a finished finding set and Core makes no such choice — but it is the one
/// thing in this assembly that is not words, so it is here rather than inside a renderer, and both
/// the mosaic and the findings pane read this and not a copy of it.
/// </para>
/// </remarks>
public static class Selection
{
    /// <summary>
    /// One exemplar per kind that fired, rarest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>First()</c> is that kind's strongest row and not an accident of iteration.</b>
    /// <see cref="FindingSet"/>'s contract is that each detector emits in a total order of its own —
    /// strongest evidence first, broken by identity — and that nothing re-sorts them. Taking the
    /// head is therefore reading a decision the detector already made, which is why this needs no
    /// measure of its own and could not honestly have one: the measure that ranks a concealed
    /// decision does not exist for shared mutable state.
    /// </para>
    /// <para>
    /// <b>The tiebreak is the kind's name, and it is load-bearing rather than tidy.</b> Two kinds
    /// firing the same number of times is ordinary on a small solution, and an order that settled
    /// them by hash or by enum order would move the lead item between runs on an unchanged
    /// codebase — which is <c>docs/ARCHITECTURE.md</c> §10's total-key rule, and the reason A3's
    /// representative cycle is chosen the way it is.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Finding> Exemplars(FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return
        [
            .. findings.All
                // The enumeration the page leads with and counts, which is not every kind that
                // fired: Claims.CompetesForLead carries why the cycle kinds render in their own
                // section instead. The filter is here rather than in the four callers for the
                // reason Subjects gives in full -- one derivation, because two of them disagree
                // silently, and a disclosure counted as a finding is what that costs.
                .Where(f => Claims.CompetesForLead(f.Kind))
                .GroupBy(f => f.Kind)
                .OrderBy(g => g.Count())
                .ThenBy(g => g.Key.ToString(), StringComparer.Ordinal)
                .Select(g => g.First())
        ];
    }
}

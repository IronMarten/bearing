namespace IronMarten.Bearing.Cli;

/// <summary>
/// One headline number, what it is called, and the claim it makes.
/// </summary>
/// <param name="Value">The number, formatted — the biggest glyph on the page.</param>
/// <param name="Label">Two or three words naming what was measured.</param>
/// <param name="Note">
/// The sentence fragment that makes the number a claim about the reader's system rather than a
/// statistic. A tile without one is a census count, which is what these replaced.
/// </param>
public readonly record struct Tile(string Value, string Label, string Note);

/// <summary>
/// The four numbers at the top of the report — A13 tier 3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one is a claim about the reader's system, and none is a tool input.</b> What shipped
/// before was <i>types / projects / dependencies / findings</i>: three census counts a reader
/// already knows and a total that measures the tool. The brief's B3 asks which four numbers deserve
/// the biggest glyphs on the page, and a number nobody changes their behaviour over does not earn
/// one — <c>PRD-free-tier.md</c> §4.
/// </para>
/// <para>
/// <b>The fifth candidate was cut, and it is the one worth remembering.</b> <i>"Findings worth
/// attention"</i> was rejected because a count of outstanding work is a lint mental model: §7.2's
/// whole position is that an anomaly is an observation, and observations do not accumulate into a
/// backlog. That is also why the findings total moved off the tile row rather than being restyled.
/// </para>
/// <para>
/// <b>Selected by a quantity that cannot be gamed by size, and never by a threshold.</b> There is
/// no constant in this file. Widest reach and sharpest outlier are maxima; clean is a share of the
/// whole; concentration picks the project with the largest <i>excess</i> of named types over its
/// proportional share rather than the largest ratio, because a ratio lets a two-type project win
/// with two findings — the same defect class as <c>MEASURE-concealed-decision.md</c>'s, one level
/// up. Excesses sum to zero across projects by construction, so the maximum is never negative and
/// a small project cannot carry a large one.
/// </para>
/// <para>
/// <b>A tile that cannot be supported is not rendered.</b> An empty solution has no widest reach
/// and a run with no findings has no outlier; a placeholder, a dash or a zero would each assert
/// something the run did not measure — invariant 6.
/// </para>
/// </remarks>
public static class Tiles
{
    /// <summary>The tile row for one run, in reading order.</summary>
    public static IReadOnlyList<Tile> For(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var named = Subjects.Named(model, findings);

        return
        [
            .. new[] { WidestReach(model), Clean(model, named), Concentration(model, named), Sharpest(model, findings) }
                .Where(t => t is not null)
                .Select(t => t!.Value)
        ];
    }

    /// <summary>
    /// What the most of this codebase depends on.
    /// </summary>
    /// <remarks>
    /// Direct dependents, which is what <see cref="TypeNode.FanIn"/> counts — a reachability
    /// closure would be a larger and less checkable number, and a reader cannot open a file to
    /// verify a transitive count. Ties break on identity, because two types of equal fan-in would
    /// otherwise swap places between runs of an unchanged codebase.
    /// </remarks>
    private static Tile? WidestReach(SolutionModel model)
    {
        var widest = model.Types
            .OrderByDescending(t => t.FanIn)
            .ThenBy(t => t.Subject.Canonical, StringComparer.Ordinal)
            .FirstOrDefault();

        if (widest is null || widest.FanIn == 0) return null;

        return new Tile(
            Html.Count(widest.FanIn),
            "Widest reach",
            $"{Sentences.Do(widest.FanIn, "type depends", "types depend")} on {widest.Name}");
    }

    /// <summary>
    /// How much of the codebase no finding says anything about.
    /// </summary>
    /// <remarks>
    /// <b>The only tile that is good news, and it is here on purpose.</b> A report that can only
    /// count what is wrong reads as a backlog however its sections are worded, and A11 round 1's
    /// complaint was that the page never said what it thought was fine. It is also the honest
    /// denominator for every other number here: 103 findings against 3,209 types is a different
    /// statement from 103 against 300.
    /// </remarks>
    private static Tile? Clean(SolutionModel model, IReadOnlySet<string> named)
    {
        if (model.Types.Count == 0) return null;

        var clean = model.Types.Count - named.Count;

        return new Tile(
            $"{Sentences.Whole(Math.Round(100d * clean / model.Types.Count))}%",
            "Clean",
            $"of {Html.Count(model.Types.Count)} types, no finding names them");
    }

    /// <summary>
    /// Which project holds more of the findings than its size accounts for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one tile that is about the shape of the solution rather than about a component.</b>
    /// Findings spread evenly across a codebase say something different from findings piled into
    /// one project, and nothing else on the page can say which of the two this is.
    /// </para>
    /// <para>
    /// <b>Chosen by excess and reported as a ratio</b>, for the reason on <see cref="Tiles"/>. The
    /// ratio is what a reader can act on; the excess is what makes the pick stable. Where findings
    /// are spread exactly in proportion the answer is <c>1x</c>, which is a real reading of an even
    /// spread and not a missing measurement.
    /// </para>
    /// </remarks>
    private static Tile? Concentration(SolutionModel model, IReadOnlySet<string> named)
    {
        if (named.Count == 0 || model.Types.Count == 0) return null;

        var top = model.Types
            .GroupBy(t => t.Project, StringComparer.Ordinal)
            .Select(g => new
            {
                Project = g.Key,
                Named = g.Count(t => named.Contains(t.Subject.Canonical)),
                Expected = named.Count * (double)g.Count() / model.Types.Count,
            })
            .OrderByDescending(p => p.Named - p.Expected)
            .ThenBy(p => p.Project, StringComparer.Ordinal)
            .First();

        if (top.Named == 0 || top.Expected <= 0) return null;

        return new Tile(
            $"{Sentences.Number(top.Named / top.Expected)}x",
            "Concentration",
            $"findings in {top.Project}, against its share of the types");
    }

    /// <summary>
    /// The largest multiple of a group median this run measured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Provisional, and the reason is <c>D34</c> rather than the layout.</b> Every quantity here
    /// is a ratio against the median of a cohort, and at the top end a cohort is not a peer group —
    /// <c>suffix:Service</c> holds 2,909 of nopCommerce's 9,219 method-like members, so
    /// <i>"93x the median of its 2,909 peers"</i> is a global ranking wearing a peer comparison's
    /// clothes. The number is arithmetically sound and the word <i>peers</i> is what is not, so the
    /// note says <i>group</i> and claims nothing about who is in it. When D34 lands this tile is
    /// expected to change or to go; the rest of the row does not depend on it.
    /// </para>
    /// <para>
    /// <b>Read off the receipts rather than recomputed</b>, so the tile and the claim that produced
    /// it cannot disagree — the multiple is whatever the detector recorded. An undefined ratio is
    /// excluded rather than treated as enormous, which is <c>docs/DEFECTS.md</c> §28: a median of
    /// zero makes the quantity undefined, and <c>undefined</c> is not the sharpest anything.
    /// </para>
    /// </remarks>
    private static Tile? Sharpest(SolutionModel model, FindingSet findings)
    {
        var sharpest = findings.All
            .Select(f => (Finding: f, Multiple: Multiple(f)))
            .Where(x => x.Multiple is not null)
            .OrderByDescending(x => x.Multiple!.Value.Times)
            .ThenBy(x => x.Finding.Key.Canonical, StringComparer.Ordinal)
            .Select(x => (x.Finding, x.Multiple!.Value.Times, x.Multiple!.Value.Quantity, Claim: Claims.For(model, x.Finding)))
            .FirstOrDefault(x => x.Claim.Exists);

        if (sharpest.Finding is null) return null;

        return new Tile(
            $"{Sentences.Number(sharpest.Times)}x",
            "Sharpest outlier",
            $"{sharpest.Claim.Subject}'s {sharpest.Quantity}, against the middle of its group");
    }

    /// <summary>
    /// The largest defined multiple-of-a-median a finding recorded, and what was multiplied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The quantity is carried out with the number because the maximum is taken across
    /// quantities.</b> A fan-in ratio and a complexity ratio are both <i>x times the middle of a
    /// group</i> and are not the same measurement, so a bare <i>"sharpest"</i> would be an order
    /// across kinds by an invented common unit — the thing <c>X10</c> spent a decision refusing.
    /// Naming what was multiplied costs four words and makes the tile a statement rather than a
    /// ranking. It measured 126x on nopCommerce, and it is fan-in.
    /// </para>
    /// <para>
    /// <b>A receipt this does not have words for is skipped rather than printed.</b> Four names is a
    /// vocabulary; the sixty-five in <c>docs/DEFECTS.md</c> §27 are why one is not allowed to grow
    /// silently, and an unrecognised receipt reaching a reader is that defect starting again.
    /// </para>
    /// </remarks>
    private static (double Times, string Quantity)? Multiple(Finding finding)
    {
        (double Times, string Quantity)? largest = null;

        foreach (var receipt in finding.Receipts)
        {
            if (!double.IsFinite(receipt.Value)) continue;
            if (Quantity(receipt.Name) is not { } quantity) continue;
            if (largest is { } current && current.Times >= receipt.Value) continue;

            largest = (receipt.Value, quantity);
        }

        return largest;
    }

    /// <summary>What a multiple-of-a-median receipt multiplied, in the reader's words.</summary>
    private static string? Quantity(string receipt) => receipt switch
    {
        "FanInXMedian" => "fan-in",
        "FanOutXMedian" => "fan-out",
        "CyclomaticXMedian" => "internal complexity",
        "MaxMemberCyclomaticXMedian" => "internal complexity",
        _ => null,
    };
}

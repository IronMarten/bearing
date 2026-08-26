namespace IronMarten.Bearing.Cli;

/// <summary>
/// Which of the four a tile is.
/// </summary>
/// <remarks>
/// <b>So a caller can ask for one without matching on the words it displays.</b> The mosaic's
/// caption states the same clean share and the same concentration the tiles do — that is the whole
/// of what the picture knows that the prose does not — and a renderer that found them by label
/// would break silently the day a label is reworded.
/// </remarks>
public enum TileKind
{
    /// <summary>What the most of this codebase depends on.</summary>
    WidestReach,

    /// <summary>The share of types no finding is about.</summary>
    Clean,

    /// <summary>The project holding more findings than its size accounts for.</summary>
    Concentration,

    /// <summary>The most intricate member in the solution.</summary>
    MostIntricate,
}

/// <summary>
/// One headline number, what it is called, and the claim it makes.
/// </summary>
/// <param name="Kind">Which of the four this is, for a caller that needs one by name.</param>
/// <param name="Value">The number, formatted — the biggest glyph on the page.</param>
/// <param name="Label">Two or three words naming what was measured.</param>
/// <param name="Subject">
/// What the number is about — a type, a project, a member — or empty where the tile is about the
/// whole solution. Carried separately from <paramref name="Note"/> because a second renderer needs
/// the name without the sentence around it, and slicing it back out of prose is how a caption comes
/// to say <i>"Nop.Services, against its share of the types carries 1.57x its share"</i>.
/// </param>
/// <param name="Note">
/// The sentence fragment that makes the number a claim about the reader's system rather than a
/// statistic. A tile without one is a census count, which is what these replaced.
/// </param>
public readonly record struct Tile(TileKind Kind, string Value, string Label, string Subject, string Note);

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
/// no constant in this file. Widest reach and most intricate are maxima; clean is a share of the
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
            .. new[] { WidestReach(model), Clean(model, named), Concentration(model, named), MostIntricate(model) }
                .Where(t => t is not null)
                .Select(t => t!.Value)
        ];
    }

    /// <summary>One tile by name, or null where this run could not support it.</summary>
    /// <remarks>
    /// For the mosaic's caption, which states the clean share and the concentration as the two
    /// things the picture knows and the prose does not. Reading the tile rather than recomputing is
    /// what stops the caption and the number above it disagreeing — the failure mode is silent,
    /// because both would be defensible on their own.
    /// </remarks>
    public static Tile? Of(SolutionModel model, FindingSet findings, TileKind kind)
    {
        foreach (var tile in For(model, findings))
            if (tile.Kind == kind)
                return tile;

        return null;
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
            TileKind.WidestReach,
            Html.Count(widest.FanIn),
            "Widest reach",
            widest.Name,
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
            TileKind.Clean,
            $"{Sentences.Whole(Math.Round(100d * clean / model.Types.Count))}%",
            "Clean",
            "",
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
            TileKind.Concentration,
            $"{Sentences.Number(top.Named / top.Expected)}x",
            "Concentration",
            top.Project,
            $"findings in {top.Project}, against its share of the types");
    }

    /// <summary>
    /// The most intricate member in the solution, named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaced the sharpest-outlier tile on 2026-08-21, which is what that tile's own
    /// remark said should happen.</b> It read <i>"when D34 lands this tile is expected to change or
    /// to go"</i>, and D34 has landed. Every quantity the old tile could show was a ratio against a
    /// cohort median, and D34's finding is that at the top end a cohort is not a peer group —
    /// <c>suffix:Service</c> holds 2,909 of nopCommerce's 9,219 method-like members. The tile was
    /// therefore putting the single number D34 calls <i>"arithmetically true and rhetorically
    /// false"</i> in the largest glyph on the page, hedged to <i>"the middle of its group"</i>
    /// because the honest word could not be used.
    /// </para>
    /// <para>
    /// <b>Cyclomatic complexity needs no cohort, so there is nothing to hedge.</b> cc 176 is a
    /// property of one method and means the same thing in every codebase — which is the same shape
    /// as <see cref="WidestReach"/>, the tile beside it that has never needed a caveat. It also
    /// keeps A13 tier 3's four claims rather than dropping the row to three.
    /// </para>
    /// <para>
    /// <b>Read off the model rather than off the findings.</b> The old tile took the largest ratio
    /// any detector <i>recorded</i>, so it could only name something already nominated; the most
    /// intricate member is a fact about the codebase whether or not a finding fired on it, and
    /// <c>TypeNode.MostComplexMember</c> is where the model already holds it.
    /// </para>
    /// <para>
    /// <b>And it says where the member is.</b> Every other rendering
    /// of a subject on the page carries <c>project · file:line</c>; this one rendered the name and
    /// dropped the rest of the identity. <b>A11 round 2's participants placed it by guessing</b> —
    /// <i>"almost assuredly in either Nop.Services or Nop.Core"</i> — and then scrolled to another
    /// finding to confirm. The tile row is the first screen and the confirmation was several
    /// screens down, which is <c>X14</c>'s identity work stopping one element short: it made a
    /// member subject an identity rather than a display string <i>precisely</i> so a member could
    /// be located.
    /// </para>
    /// </remarks>
    private static Tile? MostIntricate(SolutionModel model)
    {
        var worst = model.Types
            .Where(t => t.MostComplexMember is not null && t.MaxMemberCyclomatic > 0)
            .OrderByDescending(t => t.MaxMemberCyclomatic)
            .ThenBy(t => t.Subject.Canonical, StringComparer.Ordinal)
            .FirstOrDefault();

        if (worst?.MostComplexMember is not { } member) return null;

        var named = Sentences.Member(worst.Name, member.Name);

        // The member's own location, not its declaring type's -- the same rule
        // Subjects.Where follows and for the same reason: a type line sends a reader to the top of
        // a 3,000-line file to hunt for a method 800 lines down. The line number is unformatted
        // because it is an address somebody retypes.
        var at = member.Location.IsKnown
            ? $"{worst.Project} · {Path.GetFileName(member.Location.File)}:{member.Location.Line}"
            : worst.Project;

        return new Tile(
            TileKind.MostIntricate,
            $"cc {worst.MaxMemberCyclomatic}",
            "Most intricate",
            named,
            $"{named}, the most complex member in this solution — {at}");
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
    /// vocabulary; the sixty-five internal identifiers once published are why one may not grow
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

namespace IronMarten.Bearing.Cli;

/// <summary>
/// What the most of this solution rests on, and how much of it a finding names.
/// </summary>
/// <param name="Project">The project the most types outside it depend on.</param>
/// <param name="Dependents">How many types outside it reach into it.</param>
/// <param name="Types">How many types it declares.</param>
/// <param name="Named">How many of those some finding is about.</param>
/// <param name="Share">
/// <paramref name="Named"/> over <paramref name="Types"/>, formatted — the density a reader is
/// trying to judge by eye and cannot.
/// </param>
public readonly record struct Foundation(string Project, int Dependents, int Types, int Named, string Share);

/// <summary>
/// The half of the report's own picture that the picture cannot show.
/// </summary>
/// <remarks>
/// <para>
/// <b>Built because a reader assembled this claim by hand and got it wrong.</b> Reading the mosaic
/// against the project map, the reading was: <i>"the big two have the most findings, and one of
/// them sits near the bottom of the dependency tree — that is where I am going to get hurt."</i>
/// The method is right and it is the question `PRD-free-tier.md` §2's user is asking. The instance
/// was wrong, three times over, and every one of the three is the area encoding read exactly as it
/// is drawn:
/// </para>
/// <para>
/// On nopCommerce, <c>Nop.Web</c> carries the joint-most findings and by far the most tinted area
/// and is the <b>least dense large project at 12%</b>, with <b>31</b> dependents — the leaf, and by
/// that reader's own logic the safest place to work. <c>Nop.Services</c> reads as <i>"almost all
/// red"</i> and is <b>26%</b> of its types, because cell area is lines of code and large complex
/// types own the ink. And <c>Nop.Web.Framework</c> — <b>densest at 29%</b> and <b>most depended on
/// at 1,280</b> — goes unmentioned, because 235 types is a small tile.
/// </para>
/// <para>
/// <b>Two measured facts in one sentence, and never their product.</b> <c>PRD-free-tier.md</c> §8
/// forbids a composite, and <i>density times dependents</i> would be one — a severity model with no
/// unit, arrived at by multiplying a share by a count. What ships instead is a selection by one
/// quantity and a statement of the other: the project the most of the codebase depends on, and how
/// much of that project is named. A reader who wants the trade-off makes it themselves, which is
/// the same division of labour every claim in <see cref="Claims"/> already keeps.
/// </para>
/// <para>
/// <b>Selected on an absolute count, which is why it needs no floor.</b> The most-depended-on
/// project cannot be won by being small — that is the failure mode <see cref="Tiles"/> avoids with
/// excess, and it does not arise here, because a project nothing reaches into scores zero however
/// few types it declares.
/// </para>
/// </remarks>
public static class Foundations
{
    /// <summary>
    /// The project the most types depend on, or null where nothing depends on anything.
    /// </summary>
    public static Foundation? Of(SolutionModel model, FindingSet findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var top = model.ProjectCouplings
            .OrderByDescending(c => c.TypesElsewhereReachingIn)
            .ThenBy(c => c.Project, StringComparer.Ordinal)
            .FirstOrDefault();

        if (top is null || top.TypesElsewhereReachingIn == 0) return null;

        var named = Subjects.Named(model, findings);

        var here = model.Types
            .Where(t => string.Equals(t.Project, top.Project, StringComparison.Ordinal))
            .ToList();

        if (here.Count == 0) return null;

        var flagged = here.Count(t => named.Contains(t.Subject.Canonical));

        return new Foundation(
            top.Project,
            top.TypesElsewhereReachingIn,
            here.Count,
            flagged,
            $"{Sentences.Whole(Math.Round(100d * flagged / here.Count))}%");
    }
}

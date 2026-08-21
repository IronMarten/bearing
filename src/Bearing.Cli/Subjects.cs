namespace IronMarten.Bearing.Cli;

/// <summary>
/// Which analysed type a finding is about.
/// </summary>
/// <remarks>
/// <para>
/// <b>One derivation, because two of them disagree silently.</b> Every renderer needs this and each
/// one wrote it again — the mosaic to decide which cells carry a mark, the highlights to print a
/// location, the drill-down to decide which components get a row. A finding whose subject is a
/// member resolves to its declaring type in four places, and the day one of them stops doing so the
/// picture and the prose describe different populations without anything failing.
/// </para>
/// <para>
/// <b>A finding with no type here is not a finding that was lost.</b> A cycle is about a set of
/// namespaces; it has no cell, no location and no row, and callers are expected to drop it rather
/// than to invent one. <b>Coverage is not in that class and never was</b> — §3.11 nominates a type
/// apiece and every one of them resolves — which is exactly why
/// <see cref="Named"/> had to say what it counts rather than rely on a subject failing to resolve.
/// </para>
/// </remarks>
internal static class Subjects
{
    /// <summary>The analysed type a finding is about, or null where its subject is not one.</summary>
    internal static TypeNode? Of(SolutionModel model, Finding finding) =>
        model.Find(finding.Subject)
        ?? (finding.Subject.DeclaringType is { } declaring ? model.Find(declaring) : null);

    /// <summary>
    /// Where to open the component a finding is about — <c>project · file:line</c>.
    /// </summary>
    /// <remarks>
    /// <b>The claim's own location wins where it has one, and method level is why.</b> Resolving a
    /// member subject to its declaring type and printing that type's line sends a reader to the top
    /// of a 3,000-line file to look for a method 800 lines down — on nopCommerce,
    /// <c>ProductService.cs:26</c> for a claim about something at <c>:826</c>. A finding that names
    /// a member knows where the member is; the project has to come from the type either way. The
    /// line number is unformatted on purpose: this is an address a reader retypes, and
    /// <c>Program.cs:1,204</c> is not one.
    /// </remarks>
    internal static string Where(SolutionModel model, Finding finding, string trailer)
    {
        if (Of(model, finding) is not { } type) return "";

        var at = trailer.Length > 0
            ? trailer
            : type.Location.IsKnown
                ? $"{Path.GetFileName(type.Location.File)}:{type.Location.Line}"
                : "";

        return at.Length > 0 ? $"{type.Project} · {at}" : type.Project;
    }

    /// <summary>
    /// The identity of every type some <i>claim</i> in the set is about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The population the mosaic tints, the population the clean tile counts as not-clean, the
    /// density the plot puts up its y-axis and the share <see cref="Foundations"/> states. They have
    /// to be one population or the picture and the numbers around it are separate claims about one
    /// run, and none of them would fail.
    /// </para>
    /// <para>
    /// <b>Claims, not findings, and that is defect 41.</b> This walked <c>findings.All</c>, so a
    /// coverage entry counted as a finding naming its type — and coverage is the one kind that
    /// asserts the opposite: <i>"nothing comparable enough to judge these against"</i>. On
    /// nopCommerce it charged 104 types to the named population on the strength of the tool having
    /// declined to judge them, taking the clean tile from 88% to 85% while the census two screens
    /// down said in words that a no-peer-group row <i>"is not a finding about those types"</i>. The
    /// same page, disagreeing with itself, in the number set in the largest glyph on it.
    /// </para>
    /// <para>
    /// <b>The filter belongs here and not in the four callers.</b> That is the whole argument of
    /// this file — one derivation, because two of them disagree silently — and a fix applied per
    /// renderer would have left the fifth to be written wrong later.
    /// <see cref="Claims.IsRiskClaim"/> is the same predicate <see cref="Highlights"/> and the HTML
    /// findings pane already use, so <i>named</i> now means what <i>claim</i> means everywhere.
    /// </para>
    /// </remarks>
    internal static IReadOnlySet<string> Named(SolutionModel model, FindingSet findings)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in findings.All)
            if (Claims.IsRiskClaim(finding.Kind) && Of(model, finding) is { } type)
                named.Add(type.Subject.Canonical);

        return named;
    }
}

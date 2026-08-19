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
/// <b>A finding with no type here is not a finding that was lost.</b> Coverage is about the
/// solution and a cycle is about a set of namespaces; neither has a cell, a location or a row, and
/// callers are expected to drop them rather than to invent one.
/// </para>
/// </remarks>
internal static class Subjects
{
    /// <summary>The analysed type a finding is about, or null where its subject is not one.</summary>
    internal static TypeNode? Of(SolutionModel model, Finding finding) =>
        model.Find(finding.Subject)
        ?? (finding.Subject.DeclaringType is { } declaring ? model.Find(declaring) : null);

    /// <summary>
    /// The identity of every type some finding in the set is about.
    /// </summary>
    /// <remarks>
    /// The population the mosaic tints and the population the concentration tile counts, which have
    /// to be the same population or the picture and the number above it are two different claims.
    /// </remarks>
    internal static IReadOnlySet<string> Named(SolutionModel model, FindingSet findings)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in findings.All)
            if (Of(model, finding) is { } type)
                named.Add(type.Subject.Canonical);

        return named;
    }
}

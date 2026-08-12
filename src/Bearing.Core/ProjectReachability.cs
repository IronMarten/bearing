namespace IronMarten.Bearing;

/// <summary>
/// Which projects nothing depends on.
/// </summary>
/// <remarks>
/// Takes what it needs rather than a <see cref="SolutionModel"/>, for the reason
/// <see cref="ProjectCoupling.ForSolution"/> does: the whole of this class is three exclusions,
/// and the fixture applies none of them — its two unreferenced projects are plain libraries with
/// no entry point and no boundary. A judgement only testable through a real solution walk is one
/// whose arms are asserted by whatever the fixture happens to contain.
/// </remarks>
public static class ProjectReachability
{
    /// <summary>
    /// Projects that no other project depends on and that are not roots, ordered by name.
    /// </summary>
    /// <param name="projects">Every project, with what makes it a root.</param>
    /// <param name="coupling">
    /// Coupling for the projects that declare an analysed type. A project absent from here is not
    /// a candidate: Ca is counted over types, so a project with none has no Ca to be zero.
    /// </param>
    /// <param name="projectsHostingAnApiBoundary">
    /// Projects declaring at least one <c>ApiBoundary</c> type. A web host is a root — the
    /// requests arrive from outside the solution, where no static analysis can see the caller.
    /// </param>
    /// <remarks>
    /// <b>A root is not dead.</b> Something has to be depended on by nothing or the solution does
    /// not run, so an entry point, an executable and an API host are all excluded. What is left is
    /// a library nothing reaches, which is the case worth raising — and even that is only "nothing
    /// <i>in the analysed solution</i> reaches it", because test projects are skipped by default
    /// and a library used only by tests looks identical from here.
    /// </remarks>
    public static IReadOnlyList<string> Unreferenced(
        IEnumerable<(string Name, bool HasEntryPoint, bool IsLibrary)> projects,
        IEnumerable<ProjectCoupling> coupling,
        IEnumerable<string> projectsHostingAnApiBoundary)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(coupling);
        ArgumentNullException.ThrowIfNull(projectsHostingAnApiBoundary);

        var measured = coupling.ToList();
        var candidates = measured
            .Where(c => c.TypesElsewhereReachingIn == 0)
            .Select(c => c.Project)
            .ToHashSet(StringComparer.Ordinal);

        var hosts = projectsHostingAnApiBoundary.ToHashSet(StringComparer.Ordinal);

        return projects
            .Where(p => candidates.Contains(p.Name))
            .Where(p => !p.HasEntryPoint)
            .Where(p => p.IsLibrary)
            .Where(p => !hosts.Contains(p.Name))
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}

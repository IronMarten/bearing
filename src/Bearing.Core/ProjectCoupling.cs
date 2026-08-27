namespace IronMarten.Bearing;

/// <summary>
/// Where a project sits relative to the main sequence.
/// </summary>
/// <remarks>
/// This is a judgement, so it lives in Core. The <i>wording</i> a renderer wraps around it is
/// presentation; which zone a project is in is not.
/// </remarks>
public enum MainSequenceZone
{
    /// <summary>Neither notably close to the main sequence nor in either extreme.</summary>
    None,

    /// <summary>Within 0.3 of the main sequence — the balance is reasonable.</summary>
    NearMainSequence,

    /// <summary>
    /// Stable and concrete: much depends on it and it is hard to extend. Hard to change and
    /// hard to extend at once, which is why it is the zone worth naming.
    /// </summary>
    Pain,

    /// <summary>Abstract and depended on by nothing.</summary>
    Uselessness,
}

/// <summary>
/// Martin's coupling metrics for one project: Ca, Ce, A, I and D.
/// </summary>
/// <remarks>
/// <para>
/// Model data, not a print-time calculation. In the probe these are computed inside
/// <c>PrintProjectInstability</c> and exist only as the sentence that is written out of them,
/// which is <c>docs/ARCHITECTURE.md</c> §3's failure mode in its purest form: no other renderer
/// can show them, and nothing can assert on them without asserting on prose.
/// </para>
/// <para>
/// <b>On the counts.</b> These are Martin's original definitions — Ca is the types outside that
/// depend on something inside, Ce is the types <i>inside</i> that depend on something outside.
/// The properties are named for what they count anyway, because a common variant (NDepend among
/// others) reads Ce as the number of <i>external</i> types depended upon, which is a different
/// number from the same edges. Spelling it out means a reader comparing Bearing's I against
/// another tool's can see immediately whether they are measuring the same thing.
/// </para>
/// </remarks>
public sealed class ProjectCoupling
{
    private ProjectCoupling(
        string project,
        int typesElsewhereReachingIn,
        int typesHereReachingOut,
        int abstractTypes,
        int totalTypes)
    {
        Project = project;
        TypesElsewhereReachingIn = typesElsewhereReachingIn;
        TypesHereReachingOut = typesHereReachingOut;
        AbstractTypes = abstractTypes;
        TotalTypes = totalTypes;
    }

    /// <summary>The project these metrics describe.</summary>
    public string Project { get; }

    /// <summary>
    /// Afferent coupling (Ca): distinct types in other projects that reach into this one.
    /// </summary>
    public int TypesElsewhereReachingIn { get; }

    /// <summary>
    /// Efferent coupling as this tool counts it (Ce): distinct types in this project that
    /// reach into another. See the note on <see cref="ProjectCoupling"/> — this is not Martin's
    /// definition.
    /// </summary>
    public int TypesHereReachingOut { get; }

    /// <summary>Types in this project that are abstract or interfaces.</summary>
    public int AbstractTypes { get; }

    /// <summary>Types in this project.</summary>
    public int TotalTypes { get; }

    /// <summary>
    /// Abstractness (A): the share of this project's types that are abstract or interfaces,
    /// in 0..1. Zero for an empty project.
    /// </summary>
    public double Abstractness => TotalTypes == 0 ? 0 : (double)AbstractTypes / TotalTypes;

    /// <summary>
    /// Instability (I) in 0..1, or <see langword="null"/> when this project has no
    /// cross-project coupling at all.
    /// </summary>
    /// <remarks>
    /// Null rather than zero, and for the same reason <see cref="Distribution.Read"/> returns
    /// null: <c>Ce/(Ce+Ca)</c> has no value when both are zero, and a project that participates
    /// in nothing is not maximally stable — it is unmeasured. Emitting 0 would place every
    /// isolated project in the zone of pain.
    /// </remarks>
    public double? Instability =>
        TypesElsewhereReachingIn + TypesHereReachingOut == 0
            ? null
            : (double)TypesHereReachingOut / (TypesElsewhereReachingIn + TypesHereReachingOut);

    /// <summary>
    /// Distance from the main sequence (D): <c>|A + I - 1|</c>, or <see langword="null"/> when
    /// <see cref="Instability"/> is undefined.
    /// </summary>
    public double? DistanceFromMainSequence =>
        Instability is { } i ? Math.Abs(Abstractness + i - 1) : null;

    /// <summary>
    /// Which zone this project falls in. <see cref="MainSequenceZone.None"/> when instability
    /// is undefined — an unmeasured project is not classified.
    /// </summary>
    public MainSequenceZone Zone
    {
        get
        {
            if (Instability is not { } i) return MainSequenceZone.None;

            var a = Abstractness;

            // These three are not Bearing's to tune, and that is why they are not in
            // AnalysisPolicy. 0.3 / 0.7 / 0.3 are the published main-sequence bands — the
            // boundaries Martin defines the two zones and the tolerable distance by, and the same
            // numbers any other reader of that work has in mind. Every threshold in the policy
            // object was set by measurement against a false positive this tool produced; these
            // were set by somebody else, before it existed, and moving them would mean this
            // tool's "zone of pain" stopped meaning what the phrase means everywhere else.
            //
            // The consequence is deliberate: PolicySweepTests cannot see them and no flag can
            // move them. A team that disagrees with the bands is disagreeing with the measure
            // rather than with a setting, and the honest answer is to say so rather than offer a
            // dial. If that ever changes they become three named policy values carrying their own
            // measurement, and this comment is the record of what was traded for keeping them.
            if (i <= 0.3 && a <= 0.3) return MainSequenceZone.Pain;
            if (i >= 0.7 && a >= 0.7) return MainSequenceZone.Uselessness;

            return DistanceFromMainSequence <= 0.3
                ? MainSequenceZone.NearMainSequence
                : MainSequenceZone.None;
        }
    }

    /// <summary>
    /// Computes coupling for every project in a solution.
    /// </summary>
    /// <param name="types">
    /// Every analysed type, as <c>(id, project, isAbstractOrInterface)</c>. The id must be the
    /// same identity the edges use.
    /// </param>
    /// <param name="edges">
    /// Type-to-type dependencies as <c>(fromId, toId)</c>. Edges whose endpoints are not among
    /// <paramref name="types"/> are ignored — an edge into an excluded or unloaded type says
    /// nothing about project coupling. Weight is irrelevant here: these are counts of distinct
    /// types, not of references.
    /// </param>
    /// <returns>One entry per project, ordered by project name.</returns>
    public static IReadOnlyList<ProjectCoupling> ForSolution(
        IEnumerable<(string Id, string Project, bool IsAbstractOrInterface)> types,
        IEnumerable<(string From, string To)> edges)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(edges);

        var typeList = types.ToList();
        var projectOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in typeList) projectOf[t.Id] = t.Project;

        var reachingIn = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var reachingOut = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var project in typeList.Select(t => t.Project).Distinct(StringComparer.Ordinal))
        {
            reachingIn[project] = new HashSet<string>(StringComparer.Ordinal);
            reachingOut[project] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, to) in edges)
        {
            if (!projectOf.TryGetValue(from, out var fromProject)) continue;
            if (!projectOf.TryGetValue(to, out var toProject)) continue;
            if (string.Equals(fromProject, toProject, StringComparison.Ordinal)) continue;

            reachingOut[fromProject].Add(from);
            reachingIn[toProject].Add(from);
        }

        var byProject = typeList
            .GroupBy(t => t.Project, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        return reachingIn.Keys
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p =>
            {
                var members = byProject[p];
                return new ProjectCoupling(
                    p,
                    reachingIn[p].Count,
                    reachingOut[p].Count,
                    members.Count(t => t.IsAbstractOrInterface),
                    members.Count);
            })
            .ToList();
    }
}

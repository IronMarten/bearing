using System.Text.Json;
using System.Text.Json.Serialization;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The structure model as JSON — <c>TECHREQ-job-a.md</c> §3, <c>docs/ARCHITECTURE.md</c> §9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written out by hand rather than reflected off the model.</b> Serialising
/// <see cref="SolutionModel"/> directly would make every property of every Core type a published
/// field the moment it was added, and would publish them in whatever order the compiler emitted —
/// so a rename inside Core would be a breaking change nobody noticed making, and a new internal
/// helper property would leak. The records below <i>are</i> the schema, and changing one is a
/// visible edit to a file whose whole purpose is to be that.
/// </para>
/// <para>
/// <b>Versioned from the first release</b> (<see cref="SchemaVersion"/>) because it is free now
/// and a breaking change later. That is separate from whether the schema is a public contract —
/// <c>ARCHITECTURE.md</c> §11 still has that open, and "documented as unstable" is a valid answer
/// to it that does not change a line here.
/// </para>
/// <para>
/// <b>Everything emitted is already ordered by a total key</b>, because the model orders it. This
/// writer sorts nothing and re-derives nothing: two runs over one commit differ only in
/// <c>generatedAt</c>, which is why that is a parameter rather than a call to
/// <c>DateTimeOffset.UtcNow</c> inside it.
/// </para>
/// </remarks>
public static class JsonOutput
{
    /// <summary>
    /// The schema's version, moved when a consumer would have to change to keep reading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Independent of the tool's version: the tool ships far more often than this shape moves,
    /// and a consumer pinning against a tool version would re-pin on every release for nothing.
    /// </para>
    /// <para>
    /// <b>1.1 adds <c>statistics</c> to every type — X9.</b> A reader of 1.0 keeps working, since
    /// an unknown field is ignorable and nothing was removed or renamed; what moved the minor is
    /// that a consumer <i>wanting</i> those readings has to know a 1.0 file cannot have them. That
    /// is the case the version exists to answer, and the paid service is the consumer that will
    /// ask it, of files produced by tool versions it never saw.
    /// </para>
    /// </remarks>
    public const string SchemaVersion = "1.1";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Renders the whole model as a JSON document.</summary>
    /// <param name="model">The analysis to serialise.</param>
    /// <param name="generatedAt">
    /// When the run happened. A parameter and not a clock reading, so the output is a function of
    /// its input — the same rule <see cref="ToolInfo.ReadVersion"/> is written to, and the reason
    /// this file can be snapshotted at all.
    /// </param>
    public static string Render(SolutionModel model, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(model);

        return JsonSerializer.Serialize(DocumentFor(model, generatedAt), Options);
    }

    /// <summary>Renders the model and writes it to <paramref name="path"/>.</summary>
    public static void Write(string path, SolutionModel model, DateTimeOffset generatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // UTF-8 with no BOM. A BOM is what makes a JSON file that parses everywhere except in
        // the one tool the user reaches for first.
        File.WriteAllText(path, Render(model, generatedAt), new System.Text.UTF8Encoding(false));
    }

    private static Document DocumentFor(SolutionModel model, DateTimeOffset generatedAt) =>
        new(
            SchemaVersion,
            new Tool("bearing", model.ToolVersion),
            generatedAt,
            model.SolutionPath,
            model.Policy.Values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal),
            Coverage(model.Coverage),
            Projects(model),
            [.. model.Types.Select(t => Type(t, model.Statistics[t.Subject.Canonical]))],
            [.. model.Edges.Select(Edge)],
            Cycles(model),
            [.. model.ExternalDependencies.Select(d => new External(d.Namespace, d.TypesTouching))],
            Boundary(model));

    private static CoverageBlock Coverage(Coverage coverage) =>
        new(
            coverage.ExclusionsApplied,
            coverage.SkippedProjects,
            coverage.LoadDiagnostics,
            coverage.ProjectsNotLoaded,
            coverage.ExcludedTypes,
            coverage.EdgesToUnanalysedTypes);

    /// <summary>
    /// Projects, with Martin's metrics folded in where there are any.
    /// </summary>
    /// <remarks>
    /// One array rather than two, because <c>Projects</c> and <c>ProjectCouplings</c> are not the
    /// same list and a consumer joining them itself is a consumer that will get it wrong: a
    /// project declaring no analysed type has no abstractness to report and no edges to read an
    /// instability from, and appears here with those fields null rather than zero. Zero is a
    /// measurement; null is the absence of one.
    /// </remarks>
    private static IReadOnlyList<ProjectBlock> Projects(SolutionModel model)
    {
        var coupling = model.ProjectCouplings.ToDictionary(c => c.Project, StringComparer.Ordinal);
        var unreferenced = model.UnreferencedProjects.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. model.Projects.Select(p => new ProjectBlock(
                p.Name,
                p.HasEntryPoint,
                p.IsLibrary,
                unreferenced.Contains(p.Name),
                coupling.TryGetValue(p.Name, out var c)
                    ? new Coupling(
                        c.TypesElsewhereReachingIn,
                        c.TypesHereReachingOut,
                        c.AbstractTypes,
                        c.TotalTypes,
                        c.Abstractness,
                        c.Instability,
                        c.DistanceFromMainSequence,
                        c.Zone)
                    : null)),
        ];
    }

    private static TypeBlock Type(TypeNode type, CohortStatistics statistics) =>
        new(
            type.Subject.Canonical,
            type.Name,
            type.FullyQualifiedName,
            type.Namespace,
            type.Assembly,
            type.Project,
            type.TypeKeyword,
            type.IsAbstract,
            Site(type.Location),
            type.Classification.Kind,
            type.Classification.Evidence,
            type.Cohort.Key,
            type.Cohort.Basis,
            type.CohortSize,
            Statistics(statistics),
            type.FanIn,
            type.FanOut,
            type.EffectiveFanOut,
            type.InboundReferenceCount,
            type.Instability,
            type.InstabilityRaw,
            type.Cyclomatic,
            type.MaxMemberCyclomatic,
            type.MostComplexMember?.Subject.Canonical,
            type.Dsm,
            type.Transform,
            type.StaticMutations,
            type.MemberCount,
            type.PublicMemberCount,
            type.ExecutableMemberCount,
            type.ParameterCount,
            type.DataShape,
            type.LinesOfCode,
            [.. type.ExternalNamespaces],
            [.. type.Members.Select(Member)]);

    private static MemberBlock Member(Member member) =>
        new(
            member.Subject.Canonical,
            member.Name,
            member.Kind,
            member.Accessibility,
            Site(member.Location),
            member.Cyclomatic,
            member.Dsm,
            member.Transform,
            member.StaticMutations,
            member.MaxNestingDepth,
            member.ParameterCount,
            member.LinesOfCode);

    /// <summary>
    /// One edge, with one representative site rather than all of them.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-a.md</c> §3 asks for "file and line for at least one representative
    /// reference per edge", and the model's <see cref="Edge.PrimarySite"/> already picks it by
    /// file then line so it does not depend on the order the walk found them. Emitting every
    /// site would multiply the file by the average edge weight to answer a question — "where
    /// else" — that nothing asks yet.
    /// </remarks>
    private static EdgeBlock Edge(Edge edge) =>
        new(
            edge.From.Canonical,
            edge.To.Canonical,
            edge.Weight,
            [.. edge.Kinds.Order()],
            Site(edge.PrimarySite));

    private static CycleBlocks Cycles(SolutionModel model) =>
        new(
            [.. model.NamespaceCycles.Select(Cycle)],
            [.. model.ProjectCycles.Select(Cycle)],
            [.. model.TypeTangles.Select(Cycle)]);

    private static CycleBlock Cycle(Cycle cycle) =>
        new(
            cycle.Subject.Canonical,
            cycle.Size,
            [.. cycle.Members.Select(m => m.Canonical)],
            [.. cycle.Path.Select(m => m.Canonical)],
            cycle.PathCoversEveryMember);

    private static BoundaryBlock Boundary(SolutionModel model) =>
        new(
            [.. model.ContactPoints.Inbound.Select(t => t.Subject.Canonical)],
            [.. model.ContactPoints.Outbound.Select(t => t.Subject.Canonical)],
            [.. model.Integrations.Systems.Select(d => new External(d.Namespace, d.TypesTouching))],
            model.Integrations.PlumbingReferences);

    /// <summary>
    /// A location, or null when there is not one.
    /// </summary>
    /// <remarks>
    /// Null rather than <c>{"file": "", "line": 0}</c>: line 0 of the empty file is not a place,
    /// and a consumer that renders it as a link produces a link to nowhere. Invariant 4's shape —
    /// never let an absence read as a measurement.
    /// </remarks>
    private static SiteBlock? Site(SourceLocation location) =>
        location.IsKnown ? new SiteBlock(location.File, location.Line) : null;

    // ------------------------------------------------------------------ the schema ----
    //
    // These records are the published shape. Property names become camelCase field names, so a
    // rename here is a breaking change and should move SchemaVersion with it.

    private sealed record Document(
        string SchemaVersion,
        Tool Tool,
        DateTimeOffset GeneratedAt,
        string SolutionPath,
        IReadOnlyDictionary<string, double> Policy,
        CoverageBlock Coverage,
        IReadOnlyList<ProjectBlock> Projects,
        IReadOnlyList<TypeBlock> Types,
        IReadOnlyList<EdgeBlock> Edges,
        CycleBlocks Cycles,
        IReadOnlyList<External> ExternalDependencies,
        BoundaryBlock Boundary);

    private sealed record Tool(string Name, string Version);

    /// <summary>
    /// The thirteen — X9, and the shape a consumer reads a run's comparability from.
    /// </summary>
    /// <remarks>
    /// Every cohort-relative reading is nullable and they are absent together: a type whose cohort
    /// is below the floor has no comparison to report, and one whose peers all measure zero has no
    /// multiple. The two solution-wide readings are not nullable, because the solution is always a
    /// population — they are what a peerless type still has.
    /// </remarks>
    private sealed record StatisticsBlock(
        double? FanInPercentile,
        double? FanInTimesMedian,
        double? FanOutPercentile,
        double? FanOutTimesMedian,
        double? CyclomaticPercentile,
        double? CyclomaticTimesMedian,
        double? MaxMemberCyclomaticPercentile,
        double? MaxMemberCyclomaticTimesMedian,
        double? DsmPercentile,
        double? DsmTimesMedian,
        double? DataShapePercentile,
        double SolutionFanInPercentile,
        double SolutionMaxMemberCyclomaticPercentile);

    private static StatisticsBlock Statistics(CohortStatistics s) => new(
        s.FanInPercentile, s.FanInTimesMedian,
        s.FanOutPercentile, s.FanOutTimesMedian,
        s.CyclomaticPercentile, s.CyclomaticTimesMedian,
        s.MaxMemberCyclomaticPercentile, s.MaxMemberCyclomaticTimesMedian,
        s.DsmPercentile, s.DsmTimesMedian,
        s.DataShapePercentile,
        s.SolutionFanInPercentile, s.SolutionMaxMemberCyclomaticPercentile);

    private sealed record CoverageBlock(
        IReadOnlyList<string> ExclusionsApplied,
        IReadOnlyList<string> SkippedProjects,
        IReadOnlyList<string> LoadDiagnostics,
        IReadOnlyList<string> ProjectsNotLoaded,
        int ExcludedTypes,
        int EdgesToUnanalysedTypes);

    private sealed record ProjectBlock(
        string Name,
        bool HasEntryPoint,
        bool IsLibrary,
        bool Unreferenced,
        Coupling? Coupling);

    private sealed record Coupling(
        int Ca,
        int Ce,
        int AbstractTypes,
        int TotalTypes,
        double Abstractness,
        double? Instability,
        double? DistanceFromMainSequence,
        MainSequenceZone Zone);

    private sealed record TypeBlock(
        string Id,
        string Name,
        string FullyQualifiedName,
        string Namespace,
        string Assembly,
        string Project,
        string Keyword,
        bool IsAbstract,
        SiteBlock? Location,
        string Kind,
        string KindEvidence,
        string Cohort,
        string CohortBasis,
        int CohortSize,

        /// <summary>
        /// Where this type sits in its cohort and in the solution — X9.
        /// </summary>
        /// <remarks>
        /// Nested rather than thirteen more fields on a record that already has thirty, and because
        /// they are one thing: the readings, which are absent together when the cohort cannot
        /// support them. <c>CohortStatisticsSet</c> carries what is null and why.
        /// </remarks>
        StatisticsBlock Statistics,
        int FanIn,
        int FanOut,
        int EffectiveFanOut,
        int InboundReferences,
        double? Instability,
        double? InstabilityRaw,
        int Cyclomatic,
        int MaxMemberCyclomatic,
        string? MostComplexMember,
        int Dsm,
        int Transform,
        int StaticMutations,
        int MemberCount,
        int PublicMemberCount,
        int ExecutableMemberCount,
        int ParameterCount,
        int DataShape,
        int LinesOfCode,
        IReadOnlyList<string> ExternalNamespaces,
        IReadOnlyList<MemberBlock> Members);

    private sealed record MemberBlock(
        string Id,
        string Name,
        MemberKind Kind,
        string Accessibility,
        SiteBlock? Location,
        int Cyclomatic,
        int Dsm,
        int Transform,
        int StaticMutations,
        int MaxNestingDepth,
        int ParameterCount,
        int LinesOfCode);

    private sealed record EdgeBlock(
        string From,
        string To,
        int Weight,
        IReadOnlyList<EdgeKind> Kinds,
        SiteBlock? Site);

    private sealed record CycleBlocks(
        IReadOnlyList<CycleBlock> Namespaces,
        IReadOnlyList<CycleBlock> Projects,
        IReadOnlyList<CycleBlock> TypeTangles);

    private sealed record CycleBlock(
        string Id,
        int Size,
        IReadOnlyList<string> Members,
        IReadOnlyList<string> Path,
        bool PathCoversEveryMember);

    private sealed record BoundaryBlock(
        IReadOnlyList<string> Inbound,
        IReadOnlyList<string> Outbound,
        IReadOnlyList<External> Integrations,
        int PlumbingReferences);

    private sealed record External(string Namespace, int TypesTouching);

    private sealed record SiteBlock(string File, int Line);
}

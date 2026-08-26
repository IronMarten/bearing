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
    /// <para>
    /// <b>2.0 changes what a member's <c>id</c> is — X14, and it is the first major.</b> The value
    /// is now Roslyn's documentation comment ID rather than a display string, so a consumer holding
    /// ids from a 1.x file cannot join them against a 2.0 one: the same member has a different id,
    /// and nothing in the file says so. That is the definition of a major, and it is the reason the
    /// minor would have been the wrong call — a field being added is ignorable, a key changing
    /// value is not, and the old member ids were changing.
    /// </para>
    /// <para>
    /// The readable form did not disappear with it: <c>signature</c> is new in 2.0 and carries what
    /// <c>id</c> used to look like. It is not a key, and members can share one.
    /// </para>
    /// <para>
    /// <b>2.2 adds acknowledgment memory — A10.</b> <c>status</c> emits the third value §4 reserved
    /// for it, findings gain <c>acknowledgedBy</c>, and a top-level <c>acknowledgments</c> block
    /// carries the file the run was judged against. Additive, so a 2.1 reader keeps working — but a
    /// 2.1 reader treating <c>status</c> as a two-value enum will not, which is exactly why §4
    /// wrote the value down before anything emitted it.
    /// </para>
    /// <para>
    /// <b>The block is not optional, and §1 is the reason.</b> The report says how many findings the
    /// user's file kept out of it and how many entries matched nothing; a file that carried neither
    /// would leave the export a subset of what the report rendered, on the one axis where the
    /// difference is invisible — a consumer cannot tell a solution with three findings from one with
    /// eleven and eight acknowledged.
    /// </para>
    /// </remarks>
    public const string SchemaVersion = "2.2";

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
    /// <param name="judgement">
    /// Every judgement the run made, reported and withheld alike — <c>Analysis.Judge</c>.
    /// <c>SCHEMA-findings-export.md</c> §1: the export is a superset of what the report renders, so
    /// it takes the judgement rather than the surviving set.
    /// </param>
    /// <param name="options">
    /// How the run was configured, for the <c>configuration</c> block. Optional because a caller
    /// with a model and no options gets a faithful projection of the defaults.
    /// </param>
    public static string Render(
        SolutionModel model,
        Judgement judgement,
        DateTimeOffset generatedAt,
        WalkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(judgement);

        return JsonSerializer.Serialize(DocumentFor(model, judgement, generatedAt, options), Options);
    }

    /// <summary>Renders the model and writes it to <paramref name="path"/>.</summary>
    public static void Write(
        string path,
        SolutionModel model,
        Judgement judgement,
        DateTimeOffset generatedAt,
        WalkOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // UTF-8 with no BOM. A BOM is what makes a JSON file that parses everywhere except in
        // the one tool the user reaches for first.
        File.WriteAllText(
            path, Render(model, judgement, generatedAt, options), new System.Text.UTF8Encoding(false));
    }

    private static Document DocumentFor(
        SolutionModel model,
        Judgement judgement,
        DateTimeOffset generatedAt,
        WalkOptions? options) =>
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
            Boundary(model),
            [.. judgement.All.Select(Finding)],
            Acknowledgments(judgement),
            Configuration(options ?? new WalkOptions { SolutionPath = model.SolutionPath }));

    private static CoverageBlock Coverage(Coverage coverage) =>
        new(
            coverage.ExclusionsApplied,
            coverage.SkippedProjects,
            coverage.LoadDiagnostics,
            coverage.ProjectsNotLoaded,
            coverage.ExcludedTypes,
            coverage.EdgesToUnanalysedTypes,
            coverage.UnreadableFiles);

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
            member.Signature,
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
            [.. model.ShapedNamespaceCycles.Select(c => Cycle(c.Cycle) with { Shape = c.Shape })],
            [.. model.ProjectCycles.Select(Cycle)],
            [.. model.ShapedTypeTangles.Select(t => Cycle(t.Tangle) with { Holds = t.Shape })]);

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
        BoundaryBlock Boundary,
        IReadOnlyList<FindingBlock> Findings,
        AcknowledgmentsBlock Acknowledgments,
        ConfigurationBlock Configuration);

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
        int EdgesToUnanalysedTypes,
        // Additive, so SchemaVersion does not move: the rule above is about
        // renames. Empty on healthy code, which is every run on both reference solutions.
        IReadOnlyList<string> UnreadableFiles);

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
        string Signature,
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

    /// <param name="Shape">
    /// What closes the cycle, for namespace cycles. Null on the project and type graphs, which
    /// this vocabulary does not describe.
    /// </param>
    /// <param name="Holds">
    /// What holds the component together, for type tangles. Null elsewhere. Two fields rather
    /// than one, because the two vocabularies answer different questions and a consumer that
    /// could not tell which it had would be guessing: a namespace cycle is set aside when its
    /// shape is benign, while every tangle is reported and only its sentence changes.
    /// </param>
    private sealed record CycleBlock(
        string Id,
        int Size,
        IReadOnlyList<string> Members,
        IReadOnlyList<string> Path,
        bool PathCoversEveryMember,
        CycleShape? Shape = null,
        TangleShape? Holds = null);

    private sealed record BoundaryBlock(
        IReadOnlyList<string> Inbound,
        IReadOnlyList<string> Outbound,
        IReadOnlyList<External> Integrations,
        int PlumbingReferences);

    private sealed record External(string Namespace, int TypesTouching);

    // ---------------------------------------------------------------------- findings ----

    /// <summary>
    /// One judgement — <c>SCHEMA-findings-export.md</c> §3 and §4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A <see cref="Judged"/> and not a <c>Finding</c>, which is the whole of §1 in one
    /// parameter.</b> The export is a superset of what the report renders, and the report renders
    /// the surviving set — so taking the survivors would make the file a subset of the judgements
    /// by construction, and a consumer could not tell a claim that was <i>silenced</i> from one
    /// that was <i>never made</i>. That distinction is what <c>status</c> and
    /// <c>suppressedBy</c> carry, and §7's ageing needs both.
    /// </para>
    /// <para>
    /// <b><c>class</c> comes from <see cref="Claims.IsRiskClaim"/> and not from
    /// <see cref="Claims.CompetesForLead"/>.</b> They are different questions and the file wants
    /// the first one: a cycle is a <i>claim</i> that happens not to lead the page, and writing
    /// <c>"disclosure"</c> here to describe a layout decision would be the lie §6 spent a section
    /// refusing.
    /// </para>
    /// </remarks>
    private static FindingBlock Finding(Judged judged) =>
        new(
            judged.Finding.Key.Canonical,
            // By name, never ordinal: §4. An enum insert must not re-point a stored acknowledgment,
            // and FindingKind gained three members the day before this shipped.
            judged.Finding.Kind.ToString(),
            Claims.IsRiskClaim(judged.Finding.Kind) ? "claim" : "disclosure",
            // Three values, and the order is the order the judgement is made in: a row answers
            // before the user's file does, so a claim the tool was never going to make reads as
            // suppressed even when an entry also names it. §4, and Judged carries the argument.
            judged switch
            {
                { IsSuppressed: true } => "suppressed",
                { IsAcknowledged: true } => "acknowledged",
                _ => "reported",
            },
            judged.SilencedBy is { } rule
                ? new SuppressedByBlock(rule.Name, rule.Invariant, rule.Reason)
                : null,
            // Emitted even when the row above already silenced the claim, because an entry that has
            // gone inert is a fact about the user's file rather than about this run.
            judged.Acknowledged is { } entry
                ? new AcknowledgedByBlock(entry.Note, entry.Line)
                : null,
            Subject(judged.Finding.Subject),
            [.. judged.Finding.Receipts.Select(Receipt)],
            [.. judged.Finding.Qualifiers.Select(q => new QualifierBlock(q.Name, q.Holds, q.Gate))],
            [.. judged.Finding.Participants.Select(p => p.Canonical)],
            [.. judged.Finding.Relations.Select(
                r => new RelationBlock(r.From.Canonical, r.To.Canonical, r.Weight))]);

    /// <summary>
    /// A subject, carrying whichever of its two optional parts it actually has.
    /// </summary>
    /// <remarks>
    /// <b>Members and declaringType are <c>null</c> rather than empty when they do not apply.</b>
    /// Null, not omitted — this document writes its nulls, as <c>CycleBlock.Holds</c> already does,
    /// and a writer that dropped keys here would make the finding shape vary by subject kind. What
    /// matters is that they are not <b>empty</b>: an empty members array would read as <i>a set of
    /// nothing</i> rather than <i>not a set</i>, which is invariant 6's distinction in the one place
    /// a consumer joins on. Only <see cref="SubjectKind.Set"/> has members and only
    /// <see cref="SubjectKind.Member"/> has a declaring type, and <c>FindingsExportTests</c> asserts
    /// that correspondence rather than trusting it.
    /// </remarks>
    private static SubjectBlock Subject(SubjectRef subject) =>
        new(
            subject.Kind,
            subject.Canonical,
            subject.Members.Count > 0 ? [.. subject.Members.Select(m => m.Canonical)] : null,
            subject.DeclaringType?.Canonical);

    /// <summary>
    /// How the run was configured — the settings that change what was analysed and are not
    /// thresholds.
    /// </summary>
    /// <remarks>
    /// <b>A block beside <c>policy</c> and not a widened <c>policy</c>.</b> The policy dictionary is
    /// a faithful projection of <c>AnalysisPolicy</c>, where every value is numeric and carries a
    /// flag; these are its siblings on <see cref="WalkOptions"/> and are neither. Widening would
    /// make the export's <c>policy</c> mean something the model's does not, which is the drift
    /// <c>BEARING-OUTPUT-CONTRACT.md</c> §9 is about. Mirroring the record instead makes the
    /// omission structural: a fourth walk setting is a compile-visible gap here rather than
    /// something a writer has to remember.
    /// </remarks>
    /// <summary>
    /// The acknowledgment file, as data — <c>SCHEMA-findings-export.md</c> §4.
    /// </summary>
    /// <remarks>
    /// <b>Present on every run, with nulls and zeroes when there was no file.</b> The alternative is
    /// a key that appears only sometimes, which makes <i>nothing acknowledged</i> and <i>an older
    /// tool wrote this</i> the same observation to a consumer joining across runs.
    /// </remarks>
    private static AcknowledgmentsBlock Acknowledgments(Judgement judgement) =>
        new(
            judgement.Acknowledgments.Path,
            judgement.Acknowledgments.Count,
            judgement.AcknowledgedCount,
            [.. judgement.Unmatched.Select(a => new UnmatchedBlock(a.Key, a.Note, a.Line))]);

    private static ConfigurationBlock Configuration(WalkOptions options) =>
        new(options.IncludeTests, options.DefaultExcludesCleared, options.ExcludedPathFragments);

    private sealed record FindingBlock(
        string Key,
        string Kind,
        string Class,
        string Status,
        SuppressedByBlock? SuppressedBy,
        AcknowledgedByBlock? AcknowledgedBy,
        SubjectBlock Subject,
        IReadOnlyList<ReceiptBlock> Receipts,
        IReadOnlyList<QualifierBlock> Qualifiers,
        IReadOnlyList<string> Participants,
        IReadOnlyList<RelationBlock> Relations);

    private sealed record SubjectBlock(
        SubjectKind Kind,
        string Canonical,
        IReadOnlyList<string>? Members,
        string? DeclaringType);

    /// <summary>
    /// A receipt, with a non-finite measurement written as an absence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A ratio against a zero median is undefined, not infinite, and this is the third place
    /// that has had to say so.</b> The report used to print <c>∞</c> as a
    /// measured value; <c>CohortStatistics.TimesMedian</c> answers <see langword="null"/> for the
    /// same reason and states it — <i>infinity sorts to the top of a column as though it were the
    /// largest measurement rather than the missing one</i>. <see cref="Receipt.Value"/> is a bare
    /// <c>double</c> and carries the infinity through, because until there was an export nothing
    /// downstream of it had to be a number a machine reads.
    /// </para>
    /// <para>
    /// <b>The value is still right for the gate it fed.</b> <c>BlastRadius</c> tests
    /// <c>TimesMedian &lt; BlastFanInMultiple</c> and an infinite multiple correctly fails to be
    /// less than anything — the quantity is meaningful as a comparison and meaningless as a
    /// published number, which is exactly the split invariant 6 draws. So this withholds the number
    /// and keeps the <c>gate</c> that names what it was tested against.
    /// </para>
    /// <para>
    /// <b>It also cannot be serialised.</b> <c>System.Text.Json</c> refuses non-finite doubles
    /// outright, and the alternative — <c>AllowNamedFloatingPointLiterals</c> — writes the string
    /// <c>"Infinity"</c> where the schema declares a number, which moves the problem into every
    /// consumer instead of solving it here.
    /// </para>
    /// </remarks>
    private static ReceiptBlock Receipt(Receipt receipt) =>
        new(receipt.Name, double.IsFinite(receipt.Value) ? receipt.Value : null, receipt.Gate);

    /// <param name="Value">
    /// The measurement, or null where it is undefined — see <see cref="Receipt"/>.
    /// </param>
    /// <param name="Gate">
    /// The policy value this was tested against, or null when the receipt is ungated. Every
    /// non-null one resolves against <c>AnalysisPolicy.Values</c>, which §8.7 asserts.
    /// </param>
    private sealed record ReceiptBlock(string Name, double? Value, string? Gate);

    private sealed record QualifierBlock(string Name, bool Holds, string? Gate);

    private sealed record RelationBlock(string From, string To, int Weight);

    /// <param name="Reason">
    /// The rule's own <c>Reason</c> string, verbatim — so four surfaces do not each re-derive why
    /// something went quiet.
    /// </param>
    private sealed record SuppressedByBlock(string Rule, string Invariant, string Reason);

    /// <summary>
    /// Where the acknowledgment came from — <c>SCHEMA-findings-export.md</c> §4.
    /// </summary>
    /// <param name="Note">
    /// Why the user said it was fine, or <see langword="null"/> if they did not say. The field this
    /// block exists for: a consumer counting acknowledgments learns how many, and only the note
    /// says whether the reason still holds.
    /// </param>
    /// <param name="Line">
    /// Which line of the file, 1-based. Provenance and not identity — the finding's own
    /// <c>key</c> is the identity, and a line number in a key is the class of thing
    /// <c>FindingKey</c> excludes.
    /// </param>
    private sealed record AcknowledgedByBlock(string? Note, int Line);

    /// <summary>
    /// The acknowledgment file this run was judged against.
    /// </summary>
    /// <param name="Path">Where it was read from, or <see langword="null"/> when there was none.</param>
    /// <param name="Entries">How many acknowledgments it holds.</param>
    /// <param name="Silenced">
    /// How many claims it kept out of the report — entries a suppression row would have withheld
    /// anyway are not counted, so this is what the reader would otherwise have seen.
    /// </param>
    /// <param name="Unmatched">
    /// Entries that matched no claim this run made. A rename produces a new key, so these are how a
    /// consumer tells a component the user dismissed from one that has come back under a new name.
    /// </param>
    private sealed record AcknowledgmentsBlock(
        string? Path,
        int Entries,
        int Silenced,
        IReadOnlyList<UnmatchedBlock> Unmatched);

    private sealed record UnmatchedBlock(string Key, string? Note, int Line);

    private sealed record ConfigurationBlock(
        bool IncludeTests,
        bool DefaultExcludesCleared,
        IReadOnlyList<string> ExcludedPathFragments);

    private sealed record SiteBlock(string File, int Line);
}

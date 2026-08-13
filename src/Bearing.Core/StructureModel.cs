namespace IronMarten.Bearing;

/// <summary>
/// One type-to-type dependency, at one syntactic site.
/// </summary>
/// <param name="From">The referring type.</param>
/// <param name="To">The referenced type.</param>
/// <param name="Kind">How it refers to it.</param>
/// <param name="Site">
/// Where the reference is. This is what makes "who actually calls this" clickable rather than a
/// claim, and it is nearly free during the walk.
/// </param>
public readonly record struct TypeReference(SubjectRef From, SubjectRef To, EdgeKind Kind, SourceLocation Site);

/// <summary>
/// A dependency between two types, aggregated over every site that produced it.
/// </summary>
public sealed class Edge
{
    internal Edge(SubjectRef from, SubjectRef to, IReadOnlyList<TypeReference> references)
    {
        From = from;
        To = to;
        References = references;
    }

    /// <summary>The referring type.</summary>
    public SubjectRef From { get; }

    /// <summary>The referenced type.</summary>
    public SubjectRef To { get; }

    /// <summary>Every individual reference that makes up this edge.</summary>
    public IReadOnlyList<TypeReference> References { get; }

    /// <summary>How many references there are. The edge's weight.</summary>
    public int Weight => References.Count;

    /// <summary>The distinct kinds of reference involved.</summary>
    public IReadOnlySet<EdgeKind> Kinds => References.Select(r => r.Kind).ToHashSet();

    /// <summary>
    /// A representative site, for a renderer that shows one. The first by file then line, so it
    /// does not depend on the order the walk happened to find them.
    /// </summary>
    public SourceLocation PrimarySite => References
        .Where(r => r.Site.IsKnown)
        .OrderBy(r => r.Site.File, StringComparer.Ordinal)
        .ThenBy(r => r.Site.Line)
        .Select(r => r.Site)
        .DefaultIfEmpty(SourceLocation.None)
        .First();
}

/// <summary>Why a type was classified the way it was.</summary>
/// <param name="Kind">The architectural role.</param>
/// <param name="Evidence">
/// What decided it — <c>attribute:ApiController</c>, <c>base:DbContext</c>,
/// <c>external-ns:Azure.Messaging</c>. Stored beside the value and never the value alone,
/// because a classification a developer cannot check is one they will not trust.
/// </param>
public readonly record struct TypeClassification(string Kind, string Evidence)
{
    /// <summary>The catch-all: nothing identified this type as playing an architectural role.</summary>
    public static TypeClassification Internal { get; } = new(TypeKinds.Internal, "no classifying evidence");
}

/// <summary>What kind of member a declaration is.</summary>
/// <remarks>
/// Carried because the findings do not all read the same population. Concealed decision at
/// method level is about methods and constructors — a property is a member, and a member with a
/// computed body can hide a decision, but it is not what "the method is 4x the median
/// complexity of its peers" is comparing against. Without this the model can only offer "has an
/// executable body", which admits property accessors and excludes abstract declarations, and is
/// therefore a different set from the one the claim is about.
/// </remarks>
public enum MemberKind
{
    /// <summary>A method.</summary>
    Method,

    /// <summary>An instance or static constructor.</summary>
    Constructor,

    /// <summary>A property.</summary>
    Property,

    /// <summary>A field.</summary>
    Field,

    /// <summary>An event.</summary>
    Event,

    /// <summary>An indexer, operator, finalizer, or anything else a type can declare.</summary>
    Other,
}

/// <summary>One member of a type.</summary>
public sealed class Member
{
    internal Member(
        SubjectRef subject,
        string name,
        MemberKind kind,
        string accessibility,
        SourceLocation location,
        int cyclomatic,
        int dsm,
        int transform,
        int staticMutations,
        int maxNestingDepth,
        int parameterCount,
        int linesOfCode)
    {
        Subject = subject;
        Name = name;
        Kind = kind;
        Accessibility = accessibility;
        Location = location;
        Cyclomatic = cyclomatic;
        Dsm = dsm;
        Transform = transform;
        StaticMutations = staticMutations;
        MaxNestingDepth = maxNestingDepth;
        ParameterCount = parameterCount;
        LinesOfCode = linesOfCode;
    }

    /// <summary>
    /// Stable identity, qualified by the declaring type.
    /// </summary>
    /// <remarks>
    /// Qualified because a bare member name is not an identifier: the fixture alone has twelve
    /// types declaring <c>Apply</c>. See <c>docs/DEFECTS.md</c> §13 for what keying on the bare
    /// name would merge.
    /// </remarks>
    public SubjectRef Subject { get; }

    /// <summary>The member's own name, e.g. <c>Reconcile</c> or <c>.ctor</c>.</summary>
    public string Name { get; }

    /// <summary>What kind of declaration it is.</summary>
    public MemberKind Kind { get; }

    /// <summary>
    /// Whether this is the kind of member a method-level finding is about — a method or a
    /// constructor.
    /// </summary>
    public bool IsMethodLike => Kind is MemberKind.Method or MemberKind.Constructor;

    /// <summary>Declared accessibility.</summary>
    public string Accessibility { get; }

    /// <summary>Where it is declared.</summary>
    public SourceLocation Location { get; }

    /// <summary>Cyclomatic complexity, including the base of 1 for an executable body.</summary>
    public int Cyclomatic { get; }

    /// <summary>Destructive mutation of existing state.</summary>
    public int Dsm { get; }

    /// <summary>Non-destructive data shaping — object initializers and <c>with</c> expressions.</summary>
    public int Transform { get; }

    /// <summary>Writes to static mutable state, outside a static constructor.</summary>
    public int StaticMutations { get; }

    /// <summary>Deepest nesting reached.</summary>
    public int MaxNestingDepth { get; }

    /// <summary>Declared parameters.</summary>
    public int ParameterCount { get; }

    /// <summary>Lines spanned by the declaration.</summary>
    public int LinesOfCode { get; }
}

/// <summary>One analysed type.</summary>
public sealed class TypeNode
{
    private readonly HashSet<SubjectRef> _inbound = [];
    private readonly HashSet<SubjectRef> _outbound = [];

    internal TypeNode(SubjectRef subject, string assembly, string fullyQualifiedName, string name, string @namespace, string project, string typeKeyword, bool isAbstract, SourceLocation location)
    {
        Subject = subject;
        Assembly = assembly;
        FullyQualifiedName = fullyQualifiedName;
        Name = name;
        Namespace = @namespace;
        Project = project;
        TypeKeyword = typeKeyword;
        IsAbstract = isAbstract;
        Location = location;
    }

    /// <summary>
    /// Identity: <c>(assembly, fully-qualified name)</c>, never the name alone.
    /// </summary>
    /// <remarks>
    /// .NET permits one FQN in two assemblies and plugin architectures use it deliberately.
    /// Keying on the name merges the declarations and sums their metrics — see
    /// <c>docs/DEFECTS.md</c> §1. This is the one place extraction is permitted to change
    /// behaviour relative to the probe.
    /// </remarks>
    public SubjectRef Subject { get; }

    /// <summary>The assembly that declares it — the half of the identity a name alone omits.</summary>
    public string Assembly { get; }

    /// <summary>Fully-qualified name, including the global namespace alias.</summary>
    public string FullyQualifiedName { get; }

    /// <summary>Simple name.</summary>
    public string Name { get; }

    /// <summary>Containing namespace, or empty for the global namespace.</summary>
    public string Namespace { get; }

    /// <summary>The project that declared it.</summary>
    public string Project { get; }

    /// <summary>class / struct / interface / record / enum.</summary>
    public string TypeKeyword { get; }

    /// <summary>Whether the type is abstract.</summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Whether this type counts toward its project's abstractness: abstract, or an interface.
    /// </summary>
    /// <remarks>
    /// The second arm is redundant against Roslyn, which reports every interface as abstract, and
    /// it is kept because the metric's definition is "abstract classes and interfaces" and a
    /// reader checking the code against Martin should find both halves of it written down.
    /// </remarks>
    public bool IsAbstractOrInterface =>
        IsAbstract || string.Equals(TypeKeyword, "Interface", StringComparison.Ordinal);

    /// <summary>Where it is declared. For a partial type, the first declaration found.</summary>
    public SourceLocation Location { get; }

    /// <summary>Architectural role, with the evidence that decided it.</summary>
    public TypeClassification Classification { get; internal set; } = TypeClassification.Internal;

    /// <summary>The peer group this type is judged against.</summary>
    public Cohort Cohort { get; internal set; }

    /// <summary>How many types are in that peer group, this one included.</summary>
    public int CohortSize { get; internal set; }

    /// <summary>Its members.</summary>
    public List<Member> Members { get; } = [];

    /// <summary>Out-of-solution namespaces it touches, e.g. <c>Microsoft.EntityFrameworkCore</c>.</summary>
    public SortedSet<string> ExternalNamespaces { get; } = new(StringComparer.Ordinal);

    /// <summary>Types that refer to this one.</summary>
    public IReadOnlySet<SubjectRef> Inbound => _inbound;

    /// <summary>Types this one refers to.</summary>
    public IReadOnlySet<SubjectRef> Outbound => _outbound;

    /// <summary>Distinct types that refer to this one.</summary>
    public int FanIn => _inbound.Count;

    /// <summary>Distinct types this one refers to.</summary>
    public int FanOut => _outbound.Count;

    /// <summary>Total references into this type, not just distinct referrers.</summary>
    public int InboundReferenceCount { get; internal set; }

    /// <summary>
    /// Fan-out excluding abstractions and data contracts.
    /// </summary>
    /// <remarks>
    /// Depending on an abstraction is the mechanism dependency inversion uses to reduce
    /// exposure to change, so counting it as coupling risk penalises the practice that exists
    /// to avoid the risk.
    /// </remarks>
    public int EffectiveFanOut { get; internal set; }

    /// <summary>Cyclomatic complexity summed over members.</summary>
    public int Cyclomatic => Members.Sum(m => m.Cyclomatic);

    /// <summary>The most complex single member, or zero when there are none.</summary>
    public int MaxMemberCyclomatic => Members.Count == 0 ? 0 : Members.Max(m => m.Cyclomatic);

    /// <summary>
    /// The member <see cref="MaxMemberCyclomatic"/> measures, or <see langword="null"/> when the
    /// type declares none.
    /// </summary>
    /// <remarks>
    /// Invariant 7: a finding about a type whose complexity is concentrated in one member has to
    /// be able to name it, or the reader has to go looking for what the tool already knew.
    /// <para>
    /// The tie-break is total and grounded in source position. The probe keeps the first member
    /// to reach the maximum, which is declaration order — fine within one file, and for a
    /// partial type it is the order Roslyn hands back the declarations, which is not a property
    /// of the code. Two members tied at the maximum is the normal case in a type with no
    /// branching at all.
    /// </para>
    /// </remarks>
    public Member? MostComplexMember => Members.Count == 0
        ? null
        : Members
            .OrderByDescending(m => m.Cyclomatic)
            .ThenBy(m => m.Location.File, StringComparer.Ordinal)
            .ThenBy(m => m.Location.Line)
            .ThenBy(m => m.Subject.Canonical, StringComparer.Ordinal)
            .First();

    /// <summary>Destructive mutation summed over members.</summary>
    public int Dsm => Members.Sum(m => m.Dsm);

    /// <summary>Non-destructive shaping summed over members.</summary>
    public int Transform => Members.Sum(m => m.Transform);

    /// <summary>Writes to static mutable state summed over members.</summary>
    public int StaticMutations => Members.Sum(m => m.StaticMutations);

    /// <summary>Lines spanned by the type's declarations.</summary>
    public int LinesOfCode { get; internal set; }

    /// <summary>Declared members, including those with no body.</summary>
    public int MemberCount { get; internal set; }

    /// <summary>Publicly accessible members.</summary>
    public int PublicMemberCount { get; internal set; }

    /// <summary>Members with a real body — behaviour, rather than shape.</summary>
    public int ExecutableMemberCount { get; internal set; }

    /// <summary>Parameters summed over public members.</summary>
    public int ParameterCount { get; internal set; }

    /// <summary>Depth-1 expansion of the shapes crossing this type's public surface.</summary>
    public int DataShape { get; internal set; }

    /// <summary>
    /// Martin's instability over effective fan-out, or null when the type is unconnected and
    /// the ratio is undefined.
    /// </summary>
    public double? Instability =>
        FanIn + EffectiveFanOut == 0 ? null : (double)EffectiveFanOut / (FanIn + EffectiveFanOut);

    /// <summary>The same ratio over raw fan-out, kept for audit.</summary>
    public double? InstabilityRaw =>
        FanIn + FanOut == 0 ? null : (double)FanOut / (FanIn + FanOut);

    internal void AddInbound(SubjectRef from) => _inbound.Add(from);

    internal void AddOutbound(SubjectRef to) => _outbound.Add(to);
}

/// <summary>A project in the analysed solution.</summary>
/// <param name="Name">Project name.</param>
/// <param name="HasEntryPoint">Whether it declares a Main — a host, rather than a library.</param>
/// <param name="IsLibrary">Whether it builds a library.</param>
public readonly record struct ProjectNode(string Name, bool HasEntryPoint, bool IsLibrary);

/// <summary>An out-of-solution namespace, and who touches it.</summary>
/// <param name="Namespace">The namespace label, e.g. <c>System.Net.Http</c>.</param>
/// <param name="TypesTouching">How many analysed types reference it.</param>
/// <summary>
/// Where an external namespace came from, as the SDK resolved it.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>docs/DEFECTS.md</c> §30.</b> A reader asked for the list to separate what the project
/// built from what the language and framework provide, with the reason attached: <i>"I'm not going
/// to change any of those, so I'm not worried about them."</i> That is the list divided by what
/// somebody could act on, which is the axis it did not carry.
/// </para>
/// <para>
/// <b>Read off the resolved reference path rather than off the name.</b> Classifying by name is
/// §5's defect — a curated list that silently mis-sorts whatever it has not heard of — and it is
/// wrong in a specific way here: <c>System.Text.Json</c> ships in the shared framework on one
/// target and as a package on another, so the same name has two different answers and only the
/// resolution knows which. Framework assemblies resolve out of the targeting packs and the shared
/// framework; package assemblies resolve out of the NuGet cache. Both are structural facts about
/// how the SDK resolves, not a list of names to maintain.
/// </para>
/// <para>
/// <see cref="Unknown"/> is honest rather than a third guess: it means the path was neither, and
/// the name-based plumbing filter still gets its say for those.
/// </para>
/// </remarks>
public enum ExternalOrigin
{
    /// <summary>The reference resolved from somewhere this tool does not recognise.</summary>
    Unknown = 0,

    /// <summary>The targeting pack or shared framework — the platform, not a dependency.</summary>
    Framework,

    /// <summary>A NuGet package: a dependency somebody chose and could change.</summary>
    Package,
}

/// <param name="Namespace">The namespace, as written.</param>
/// <param name="TypesTouching">How many analysed types reference it.</param>
/// <param name="Origin">Where the SDK resolved it from — <see cref="ExternalOrigin"/>.</param>
public readonly record struct ExternalDependency(
    string Namespace,
    int TypesTouching,
    ExternalOrigin Origin = ExternalOrigin.Unknown);

/// <summary>
/// What the analysis did not see.
/// </summary>
/// <remarks>
/// Part of the output rather than a footnote. A finding is only as good as the population it
/// was computed over, so a reader has to be able to see what was excluded before trusting a
/// percentile taken against it.
/// </remarks>
public sealed class Coverage
{
    /// <summary>Path fragments that excluded a file from analysis.</summary>
    public required IReadOnlyList<string> ExclusionsApplied { get; init; }

    /// <summary>Projects skipped because they looked like test projects.</summary>
    public required IReadOnlyList<string> SkippedProjects { get; init; }

    /// <summary>Diagnostics emitted while loading. Not necessarily failures.</summary>
    public required IReadOnlyList<string> LoadDiagnostics { get; init; }

    /// <summary>Types dropped because they matched an exclusion.</summary>
    public required int ExcludedTypes { get; init; }

    /// <summary>
    /// Dependencies whose endpoint the walk never declared, and which are therefore absent from
    /// the graph.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §7. A reference can resolve to a symbol that belongs to this solution
    /// while pointing at a type no node was built for — excluded by path, in a skipped project, or
    /// compiler-generated. Those cannot be edges, and a graph quietly missing some of them
    /// understates fan-in exactly where a reader would not think to look. Set during
    /// <c>ModelBuilder.Build</c>, which is the only place that can know it.
    /// </remarks>
    public int EdgesToUnanalysedTypes { get; internal set; }
}

/// <summary>
/// The one object every deliverable renders from.
/// </summary>
/// <remarks>
/// Deliberately not the finding model. This is the substrate — smaller, far less contentious,
/// and the thing the terminal output, the JSON, the HTML report and both graph artifacts all
/// read rather than five parallel reimplementations of the same sentences.
/// </remarks>
public sealed class SolutionModel
{
    internal SolutionModel(
        string solutionPath,
        AnalysisPolicy policy,
        string toolVersion,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<TypeNode> types,
        IReadOnlyList<Edge> edges,
        Coverage coverage,
        IReadOnlyDictionary<string, ExternalOrigin>? externalOrigins = null)
    {
        _externalOrigins = externalOrigins;
        SolutionPath = solutionPath;
        Policy = policy;
        ToolVersion = toolVersion;
        Projects = projects;
        Types = types;
        Edges = edges;
        Coverage = coverage;
    }

    /// <summary>The solution that was analysed.</summary>
    public string SolutionPath { get; }

    /// <summary>The thresholds this analysis was produced under.</summary>
    public AnalysisPolicy Policy { get; }

    /// <summary>
    /// The version of the tool that produced this analysis, as the host reported it.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §21. This used to read <c>typeof(SolutionModel).Assembly</c>, which
    /// is <c>Bearing.Core</c> — a project that sets no version and therefore reported the SDK
    /// default <c>1.0.0</c> against a tool shipping <c>0.0.1-preview.1</c>. It comes from
    /// <see cref="WalkOptions.ToolVersion"/> now, because the version belongs to whatever packs
    /// and Core has no way to find that out that is not a guess.
    /// </remarks>
    public string ToolVersion { get; }

    /// <summary>Every project analysed.</summary>
    public IReadOnlyList<ProjectNode> Projects { get; }

    /// <summary>Every type analysed, ordered by identity.</summary>
    public IReadOnlyList<TypeNode> Types { get; }

    /// <summary>Every dependency between analysed types, ordered by endpoint.</summary>
    public IReadOnlyList<Edge> Edges { get; }

    /// <summary>What was not seen.</summary>
    public Coverage Coverage { get; }

    /// <summary>
    /// Martin's coupling metrics for every project that declares an analysed type, ordered by
    /// project name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model's own reading of <see cref="ProjectCoupling"/>. The class could always compute
    /// this; until now nothing but a test could call it, because feeding it meant knowing that
    /// the ids it wants are <see cref="SubjectRef.Canonical"/> on both the types and the edge
    /// endpoints. A renderer should not have to know that, and two renderers deriving it
    /// separately is <c>docs/ARCHITECTURE.md</c> §3 in miniature.
    /// </para>
    /// <para>
    /// <b>A project that declares no analysed type does not appear here</b>, which is the probe's
    /// behaviour too — every project it lists comes from the type table. It is not the same list
    /// as <see cref="Projects"/>: a project excluded down to nothing, or one that is only a host,
    /// has no abstractness to report and no edges to read an instability from. A renderer that
    /// wants to say a project was analysed and found empty should ask <see cref="Projects"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProjectCoupling> ProjectCouplings =>
        _projectCouplings ??= ProjectCoupling.ForSolution(
            Types.Select(t => (t.Subject.Canonical, t.Project, t.IsAbstractOrInterface)),
            Edges.Select(e => (e.From.Canonical, e.To.Canonical)));

    /// <summary>Mutually dependent namespaces, largest cycle first.</summary>
    public IReadOnlyList<Cycle> NamespaceCycles => _namespaceCycles ??= Cycles.AmongNamespaces(this);

    /// <summary>
    /// Mutually dependent projects, largest cycle first.
    /// </summary>
    /// <remarks>
    /// Usually empty, and that is the expected answer rather than a broken one: an ordinary
    /// cross-project edge follows a project reference, and MSBuild forbids those from cycling, so
    /// aggregating the type graph reproduces the reference DAG. A cycle here means an analysed
    /// assembly was reached some way other than a project reference — see
    /// <see cref="Cycles.AmongProjects(SolutionModel)"/>, and note what
    /// <c>docs/DEFECTS.md</c> §1 fabricated here before <see cref="SubjectRef"/> keyed types by
    /// assembly.
    /// </remarks>
    public IReadOnlyList<Cycle> ProjectCycles => _projectCycles ??= Cycles.AmongProjects(this);

    /// <summary>
    /// The project dependency graph, layered and folded — what the architecture diagram draws.
    /// </summary>
    public ProjectGraph ProjectGraph => _projectGraph ??= ProjectGraph.Of(this);

    /// <summary>
    /// Groups of types that all reach each other, largest first. Gated at
    /// <see cref="AnalysisPolicy.MinTangle"/>.
    /// </summary>
    public IReadOnlyList<Cycle> TypeTangles => _typeTangles ??= Cycles.AmongTypes(this);

    /// <summary>
    /// Projects no other project depends on, ordered by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A root is not dead.</b> Something has to be depended on by nothing, or the solution
    /// does not run — so an entry point, an executable and a project holding an API boundary are
    /// all excluded. What is left is a library nothing reaches, which is the case worth raising.
    /// </para>
    /// <para>
    /// <b>It cannot see test projects, and that changes what the answer means.</b> They are
    /// skipped by default, so a library used only by tests has no visible consumer and appears
    /// here. The claim this list supports is "nothing in the analysed solution depends on these",
    /// which is not the same as "these are unused" — a renderer that shortens it to the second is
    /// telling a reader it is safe to delete working code. <c>Coverage.SkippedProjects</c> is
    /// what makes the difference statable.
    /// </para>
    /// <para>
    /// Only projects that declare an analysed type are candidates, since Ca is counted over
    /// types — see <see cref="ProjectCouplings"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProjectNode> UnreferencedProjects => _unreferencedProjects ??= Unreferenced();

    private List<ProjectNode> Unreferenced()
    {
        var unreferenced = ProjectReachability.Unreferenced(
            Projects.Select(p => (p.Name, p.HasEntryPoint, p.IsLibrary)),
            ProjectCouplings,
            Types
                .Where(t => string.Equals(t.Classification.Kind, TypeKinds.ApiBoundary, StringComparison.Ordinal))
                .Select(t => t.Project));

        var byName = Projects.ToDictionary(p => p.Name, StringComparer.Ordinal);
        return unreferenced.Select(name => byName[name]).ToList();
    }

    /// <summary>
    /// The solution's external contact points, split inbound and outbound.
    /// </summary>
    public ContactPoints ContactPoints => _contactPoints ??= ExternalSurface.Of(this);

    /// <summary>
    /// External systems this codebase talks to, with the plumbing filtered out and counted.
    /// </summary>
    /// <remarks>
    /// The judged view of <see cref="ExternalDependencies"/>, which stays unfiltered — deciding
    /// that <c>System.Linq</c> is not an integration is a judgement, and the raw list is what
    /// makes the judgement checkable.
    /// </remarks>
    public IntegrationMap Integrations => _integrations ??= ExternalSurface.Integrations(this);

    /// <summary>Every namespace outside the solution that analysed types touch.</summary>
    public IReadOnlyList<ExternalDependency> ExternalDependencies =>
        _externalDependencies ??= Types
            .SelectMany(t => t.ExternalNamespaces)
            .GroupBy(ns => ns, StringComparer.Ordinal)
            .Select(g => new ExternalDependency(g.Key, g.Count(), OriginOf(g.Key)))
            .OrderByDescending(d => d.TypesTouching)
            .ThenBy(d => d.Namespace, StringComparer.Ordinal)
            .ToList();

    /// <summary>Namespaces declared inside the solution, with the types they contain.</summary>
    public IReadOnlyList<(string Namespace, IReadOnlyList<TypeNode> Types)> Namespaces =>
        _namespaces ??= Types
            .GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? "<global>" : t.Namespace, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, (IReadOnlyList<TypeNode>)g.ToList()))
            .ToList();

    /// <summary>Looks a type up by identity.</summary>
    public TypeNode? Find(SubjectRef subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        _byId ??= Types.ToDictionary(t => t.Subject.Canonical, StringComparer.Ordinal);
        return _byId.GetValueOrDefault(subject.Canonical);
    }

    // ---------------------------------------------------------------- memoisation ----
    //
    // Every projection above is a pure function of the model, and the model is frozen: each of
    // TypeNode's `internal set` accessors is written inside ModelBuilder.Build, which finishes
    // before `new SolutionModel(...)` is reached. So a computed answer cannot go stale, and the
    // only question is how many times a renderer pays for it.
    //
    // It pays more than once. R4's three CSVs, R3's HTML and the terminal output all read the
    // same model, the stability section and the unreferenced-projects section are two reads of
    // ProjectCouplings, and Integrations reads ExternalDependencies twice inside one call. On the
    // 132-type fixture that is invisible; on the solutions this tool is aimed at it is a linear
    // pass per read, repeated for no reason.
    //
    // Not thread-safe, deliberately: a torn read here recomputes a pure function and assigns an
    // equal value, so the cost of a race is one wasted pass rather than a wrong answer. Locking
    // every projection to save that would be the more expensive mistake. If a renderer ever
    // parallelises across sections, this is the note to revisit.
    private Dictionary<string, TypeNode>? _byId;
    private IReadOnlyList<ProjectCoupling>? _projectCouplings;
    private IReadOnlyList<Cycle>? _namespaceCycles;
    private IReadOnlyList<Cycle>? _projectCycles;
    private ProjectGraph? _projectGraph;
    private IReadOnlyList<Cycle>? _typeTangles;
    private IReadOnlyList<ProjectNode>? _unreferencedProjects;
    private readonly IReadOnlyDictionary<string, ExternalOrigin>? _externalOrigins;

    /// <summary>Where a namespace resolved from, or <see cref="ExternalOrigin.Unknown"/>.</summary>
    /// <remarks>
    /// A model built without origins — every test that constructs one by hand — answers Unknown
    /// for everything, which is the answer that changes no behaviour: the name-based plumbing
    /// filter still decides, exactly as it did before origins existed.
    /// </remarks>
    public ExternalOrigin OriginOf(string @namespace) =>
        _externalOrigins is not null && _externalOrigins.TryGetValue(@namespace, out var origin)
            ? origin
            : ExternalOrigin.Unknown;

    private IReadOnlyList<ExternalDependency>? _externalDependencies;
    private IReadOnlyList<(string Namespace, IReadOnlyList<TypeNode> Types)>? _namespaces;
    private ContactPoints? _contactPoints;
    private IntegrationMap? _integrations;
}

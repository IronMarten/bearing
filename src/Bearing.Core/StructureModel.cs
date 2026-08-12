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
    public static TypeClassification Internal { get; } = new("Internal", "no classifying evidence");
}

/// <summary>One member of a type.</summary>
public sealed class Member
{
    internal Member(
        SubjectRef subject,
        string name,
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

    /// <summary>Where it is declared. For a partial type, the first declaration found.</summary>
    public SourceLocation Location { get; }

    /// <summary>Architectural role, with the evidence that decided it.</summary>
    public TypeClassification Classification { get; internal set; } = TypeClassification.Internal;

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
public readonly record struct ExternalDependency(string Namespace, int TypesTouching);

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
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<TypeNode> types,
        IReadOnlyList<Edge> edges,
        Coverage coverage)
    {
        SolutionPath = solutionPath;
        Policy = policy;
        Projects = projects;
        Types = types;
        Edges = edges;
        Coverage = coverage;
    }

    /// <summary>The solution that was analysed.</summary>
    public string SolutionPath { get; }

    /// <summary>The thresholds this analysis was produced under.</summary>
    public AnalysisPolicy Policy { get; }

    /// <summary>The tool version that produced it.</summary>
    public string ToolVersion { get; } =
        ToolInfo.ReadVersion(typeof(SolutionModel).Assembly);

    /// <summary>Every project analysed.</summary>
    public IReadOnlyList<ProjectNode> Projects { get; }

    /// <summary>Every type analysed, ordered by identity.</summary>
    public IReadOnlyList<TypeNode> Types { get; }

    /// <summary>Every dependency between analysed types, ordered by endpoint.</summary>
    public IReadOnlyList<Edge> Edges { get; }

    /// <summary>What was not seen.</summary>
    public Coverage Coverage { get; }

    /// <summary>Every namespace outside the solution that analysed types touch.</summary>
    public IReadOnlyList<ExternalDependency> ExternalDependencies =>
        Types
            .SelectMany(t => t.ExternalNamespaces)
            .GroupBy(ns => ns, StringComparer.Ordinal)
            .Select(g => new ExternalDependency(g.Key, g.Count()))
            .OrderByDescending(d => d.TypesTouching)
            .ThenBy(d => d.Namespace, StringComparer.Ordinal)
            .ToList();

    /// <summary>Namespaces declared inside the solution, with the types they contain.</summary>
    public IReadOnlyList<(string Namespace, IReadOnlyList<TypeNode> Types)> Namespaces =>
        Types
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

    private Dictionary<string, TypeNode>? _byId;
}

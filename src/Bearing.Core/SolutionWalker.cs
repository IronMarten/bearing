using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace IronMarten.Bearing;

/// <summary>What to analyse, and what to leave out.</summary>
public sealed record WalkOptions
{
    /// <summary>Path to the solution.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Thresholds. Only <see cref="AnalysisPolicy.MinCohort"/> affects the walk itself.</summary>
    public AnalysisPolicy Policy { get; init; } = AnalysisPolicy.Default;

    /// <summary>Whether to analyse projects that look like test projects.</summary>
    public bool IncludeTests { get; init; }

    /// <summary>
    /// The version of the tool doing the analysing, which the host supplies because Core cannot
    /// know it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §21. <see cref="SolutionModel.ToolVersion"/> used to read
    /// <c>typeof(SolutionModel).Assembly</c> — <c>Bearing.Core</c>, which sets no version and so
    /// reported the SDK default <c>1.0.0</c> against a tool shipping <c>0.0.1-preview.1</c>. The
    /// version lives on whatever packs, and Core is not it.
    /// </para>
    /// <para>
    /// <b>The default is <see cref="ToolInfo.UnknownVersion"/> rather than Core's own.</b> A host
    /// that does not say produces <c>0.0.0</c>, which reads as "nobody told me" — where
    /// <c>1.0.0</c> read as a release that does not exist, and did so in a field a consumer was
    /// about to parse and compare. <see cref="Assembly.GetEntryAssembly"/> is not the answer for
    /// the reason <see cref="ToolInfo.ReadVersion"/> gives: under a test host it is the runner.
    /// </para>
    /// </remarks>
    public string ToolVersion { get; init; } = ToolInfo.UnknownVersion;

    /// <summary>
    /// Path fragments that exclude a file from analysis.
    /// </summary>
    /// <remarks>
    /// Scaffolded and tool-generated code is real C# and it compiles, but it is nobody's design,
    /// so it pollutes every cohort it lands in and produces nominations no one can act on. EF
    /// migrations alone can be hundreds of files carrying enormous methods.
    /// <para>
    /// Directory patterns are written with forward slashes and matched against a normalized
    /// path, so they hold regardless of which separator the workspace hands back — and so that a
    /// stray backslash in a literal cannot silently disable one.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> ExcludedPathFragments { get; init; } =
    [
        "/migrations/", "/helppage/", "/obj/", "/bin/",
        "/connected services/", "/service references/", "/web references/",
        "/.nuget/", "/packages/",
        ".designer.cs", ".generated.cs", ".g.cs", ".g.i.cs",
        "reference.cs", "assemblyinfo.cs", "globalusings.cs",
    ];

    internal bool IsExcluded(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var normalized = Normalize(path);
        return ExcludedPathFragments.Any(f => normalized.Contains(Normalize(f), StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Whether a project name reads as a test project.
    /// </summary>
    /// <remarks>
    /// Excluded by default because test code inflates fan-in on exactly the types it covers
    /// best, which inverts the signal: the better-tested a component is, the more load-bearing
    /// it appears.
    /// </remarks>
    internal static bool LooksLikeTestProject(string name) => TestProjectName.IsMatch(name);

    private static readonly System.Text.RegularExpressions.Regex TestProjectName =
        new(@"(^|\.)(tests?|specs?|unittests?|integrationtests?)($|\.)|tests?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}

/// <summary>
/// The solution could not be read at all — so there is nothing to analyse and no coverage to
/// report.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from a load <i>diagnostic</i>, which <see cref="Coverage.LoadDiagnostics"/> carries
/// and which does not stop the walk. This is the case where the workspace never opened: an
/// unsupported format, a file that is not a solution, a path the process may not read. See
/// <c>docs/DEFECTS.md</c> §23.
/// </para>
/// <para>
/// <b>The cause is kept rather than classified.</b> Core knows the load failed and what threw;
/// what that means to a user — "that is a project file, not a solution" — is a judgement about
/// wording and belongs to the renderer, which is why <see cref="Exception.InnerException"/> is
/// preserved and the path is a property rather than only a substring of the message.
/// </para>
/// </remarks>
public sealed class SolutionLoadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SolutionLoadException() { }

    /// <summary>Creates the exception.</summary>
    public SolutionLoadException(string message) : base(message) { }

    /// <summary>Creates the exception.</summary>
    public SolutionLoadException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The solution that could not be read.</summary>
    public string SolutionPath { get; init; } = "";
}

/// <summary>
/// Loads a solution and produces the structure model.
/// </summary>
/// <remarks>
/// <para>
/// <b>One walk.</b> Fan-in and fan-out come out of the same traversal rather than from N calls
/// to <c>FindReferencesAsync</c>. That reads like an implementation detail and is not one: it
/// sets what a run costs on a large solution, and the product commits to a first finding inside
/// sixty seconds cold.
/// </para>
/// <para>
/// The caller registers the MSBuild SDK before constructing this — a library that registers a
/// process-wide singleton on load cannot be composed, so that stays the host's job.
/// </para>
/// </remarks>
public sealed class SolutionWalker
{
    private readonly WalkOptions _options;

    /// <summary>Creates a walker.</summary>
    public SolutionWalker(WalkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Policy.Validate();
        _options = options;
    }

    /// <summary>Loads the solution and walks it.</summary>
    /// <remarks>
    /// <b>Five steps, named, and it used to be one method.</b> At cc 25 over 109 lines with four
    /// levels of nesting it was the worst method in this codebase on all three measures at once —
    /// which is the tool's own verdict on itself, and the reason it was split. Nothing about the
    /// order or the work changed; what changed is that each step can be read without the other
    /// four in view. The two closures stay closures because each is a projection of
    /// <c>compilations</c> that the builder holds for the length of the walk.
    /// </remarks>
    public async Task<SolutionModel> WalkAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        var skipped = new List<string>();

        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        workspace.WorkspaceFailed += (_, e) =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                diagnostics.Add(e.Diagnostic.Message);
        };

        var solution = await OpenAsync(workspace, cancellationToken).ConfigureAwait(false);
        var projects = SelectProjects(solution, skipped);

        var (compilations, projectNodes, notLoaded) =
            await CompileAsync(projects, diagnostics, cancellationToken).ConfigureAwait(false);

        var builder = new ModelBuilder(
            _options, InSolutionOf(compilations), OriginOfAssembly(compilations));

        await WalkTypesAsync(builder, compilations, cancellationToken).ConfigureAwait(false);

        return builder.Build(_options.SolutionPath, projectNodes, new Coverage
        {
            ExclusionsApplied = _options.ExcludedPathFragments,
            SkippedProjects = skipped,
            LoadDiagnostics = diagnostics,
            ProjectsNotLoaded = notLoaded,
            ExcludedTypes = builder.ExcludedTypes,
        });
    }

    /// <summary>
    /// The C# projects to analyse, recording into <paramref name="skipped"/> the ones excluded
    /// for looking like tests.
    /// </summary>
    /// <remarks>
    /// The skip is recorded rather than silent because invariant 8 says so, and because a test
    /// project's absence understates fan-in for everything it uses — <c>FixtureBuilder</c> in the
    /// fixture is exactly that case, and it is planted to look like dead code because of it.
    /// </remarks>
    private List<Project> SelectProjects(Solution solution, List<string> skipped)
    {
        var projects = solution.Projects
            .Where(p => p.Language == LanguageNames.CSharp)
            .Where(p =>
            {
                if (_options.IncludeTests || !WalkOptions.LooksLikeTestProject(p.Name)) return true;
                skipped.Add(p.Name);
                return false;
            })
            .ToList();

        if (projects.Count == 0)
            throw new InvalidOperationException(
                $"No C# projects loaded from '{_options.SolutionPath}'. Is the solution restored?");

        return projects;
    }

    /// <summary>
    /// Compiles each project, and records what each one is while the compilation is in hand.
    /// </summary>
    /// <remarks>
    /// A project that will not compile is dropped and disclosed, never guessed at: its types
    /// would be missing either way, and a reader who sees no diagnostic is entitled to assume
    /// the graph is complete.
    /// </remarks>
    private static async Task<(List<(Project Project, Compilation Compilation)> Compilations,
                              List<ProjectNode> Nodes,
                              List<string> NotLoaded)> CompileAsync(
        List<Project> projects, List<string> diagnostics, CancellationToken cancellationToken)
    {
        var compilations = new List<(Project Project, Compilation Compilation)>();
        var nodes = new List<ProjectNode>();
        var notLoaded = new List<string>();

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                // Both, deliberately: the diagnostic is the detail and the name is the outcome.
                // Only the second bounds the numbers — docs/DEFECTS.md §4.
                diagnostics.Add($"No compilation for {project.Name}");
                notLoaded.Add(project.Name);
                continue;
            }

            compilations.Add((project, compilation));
            nodes.Add(new ProjectNode(
                project.Name,
                compilation.GetEntryPoint(cancellationToken) is not null,
                compilation.Options.OutputKind == OutputKind.DynamicallyLinkedLibrary));
        }

        return (compilations, nodes, notLoaded);
    }

    /// <summary>Whether a symbol is declared by one of the assemblies being analysed.</summary>
    /// <remarks>
    /// By assembly name rather than by project: this is the question "is this ours", and the
    /// answer has to be the same for a type reached through a reference as for one reached
    /// through source.
    /// </remarks>
    private static Func<ISymbol?, bool> InSolutionOf(
        List<(Project Project, Compilation Compilation)> compilations)
    {
        var assemblies = compilations
            .Select(c => c.Compilation.Assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        return symbol =>
            symbol?.ContainingAssembly is not null
            && assemblies.Contains(symbol.ContainingAssembly.Name);
    }

    /// <summary>Where an external symbol's assembly was resolved from.</summary>
    /// <remarks>
    /// Read off the reference paths once, up front, because the same assembly is referenced by
    /// many projects and the answer cannot differ between them. Package beats Framework beats
    /// Unknown, so a NuGet copy of something that also ships in the SDK reads as a package.
    /// </remarks>
    private static Func<ISymbol?, ExternalOrigin> OriginOfAssembly(
        List<(Project Project, Compilation Compilation)> compilations)
    {
        var byAssembly = new Dictionary<string, ExternalOrigin>(StringComparer.Ordinal);

        foreach (var (_, compilation) in compilations)
        {
            foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;

                var origin = OriginOfPath(reference.FilePath);
                if (origin == ExternalOrigin.Unknown) continue;
                if (byAssembly.TryGetValue(assembly.Name, out var seen) && seen >= origin) continue;

                byAssembly[assembly.Name] = origin;
            }
        }

        return symbol =>
            symbol?.ContainingAssembly is { } a && byAssembly.TryGetValue(a.Name, out var origin)
                ? origin
                : ExternalOrigin.Unknown;
    }

    /// <summary>Walks every analysable type in every compilation into the builder.</summary>
    /// <remarks>
    /// One pass, and the reason is in this class's own summary: fan-in and fan-out come out of
    /// this traversal rather than from N calls to <c>FindReferencesAsync</c>, which is what sets
    /// the cost of a run on a large solution.
    /// </remarks>
    private async Task WalkTypesAsync(
        ModelBuilder builder,
        List<(Project Project, Compilation Compilation)> compilations,
        CancellationToken cancellationToken)
    {
        foreach (var (project, compilation) in compilations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace))
            {
                if (!ShouldAnalyse(type)) { builder.CountExclusion(); continue; }

                var node = builder.GetOrAdd(type, compilation.Assembly.Name, project.Name);

                foreach (var declaration in type.DeclaringSyntaxReferences)
                {
                    var syntax = await declaration.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                    if (syntax is not TypeDeclarationSyntax and not EnumDeclarationSyntax) continue;
                    if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree)) continue;

                    builder.Walk(node, type, syntax, compilation.GetSemanticModel(syntax.SyntaxTree));
                }

                ModelBuilder.Classify(node, type);
            }
        }
    }

    /// <summary>
    /// Opens the workspace, turning any failure to read the solution into
    /// <see cref="SolutionLoadException"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Narrow in scope rather than narrow in type, and deliberately.</b> The catch covers one
    /// call — "open this file as a solution" — and everything the walk does afterwards is outside
    /// it, so this cannot swallow an analysis bug. Within that one call the failure modes are
    /// MSBuild's and open-ended: <c>docs/DEFECTS.md</c> §23 was found as an
    /// <c>InvalidProjectFileException</c>, and a bad path, an unreadable file and a format
    /// MSBuild does not parse all arrive as different types from assemblies Core deliberately
    /// does not reference. Listing the types we happen to have seen is how the next unlisted one
    /// reaches the user as a stack trace.
    /// </para>
    /// <para>
    /// Cancellation is not a load failure and passes through.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Framework or package, decided by where the SDK resolved the assembly from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §30, and the reason it is not a list of names.</b> §5 is the
    /// standing example of what a curated list costs: it decides a classification, so anything the
    /// list has not heard of is silently sorted wrong. Names are also genuinely ambiguous here —
    /// <c>System.Text.Json</c> is in the shared framework on one target framework and a package on
    /// another, so the name cannot answer the question and the resolution always can.
    /// </para>
    /// <para>
    /// <b>What each path means.</b> The SDK resolves framework references out of the targeting
    /// packs (<c>packs/Microsoft.NETCore.App.Ref/…</c>) and the shared framework
    /// (<c>shared/Microsoft.NETCore.App/…</c>); NuGet restores packages into its global cache,
    /// which is <c>NUGET_PACKAGES</c> when set and <c>~/.nuget/packages</c> otherwise. Those are
    /// facts about how restore works rather than about what anything is called.
    /// </para>
    /// <para>
    /// Anything else is <see cref="ExternalOrigin.Unknown"/> and stays unknown — a solution-local
    /// <c>packages/</c> folder, a checked-in lib directory, a reference assembly somebody points at
    /// directly. Guessing there would reintroduce exactly the failure this avoids, and the
    /// name-based plumbing filter still applies to whatever lands here.
    /// </para>
    /// </remarks>
    private static ExternalOrigin OriginOfPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return ExternalOrigin.Unknown;

        var normalized = path.Replace('\\', '/');

        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(cache)
            && normalized.StartsWith(cache.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return ExternalOrigin.Package;
        }

        if (normalized.Contains("/.nuget/packages/", StringComparison.OrdinalIgnoreCase))
            return ExternalOrigin.Package;

        if (normalized.Contains("/packs/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/shared/microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalOrigin.Framework;
        }

        return ExternalOrigin.Unknown;
    }

    private async Task<Solution> OpenAsync(MSBuildWorkspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            return await workspace
                .OpenSolutionAsync(_options.SolutionPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // see the remarks above: the scope is one call, and the types are MSBuild's to change
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new SolutionLoadException($"'{_options.SolutionPath}' could not be read as a solution.", ex)
            {
                SolutionPath = _options.SolutionPath,
            };
        }
    }

    private bool ShouldAnalyse(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length == 0) return false;
        if (type.IsImplicitlyDeclared) return false;
        if (type.TypeKind == TypeKind.Delegate) return false;

        return !type.DeclaringSyntaxReferences.Any(r => _options.IsExcluded(r.SyntaxTree.FilePath ?? ""));
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in EnumerateNested(type)) yield return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
            foreach (var type in EnumerateTypes(child))
                yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in EnumerateNested(nested)) yield return deeper;
        }
    }
}

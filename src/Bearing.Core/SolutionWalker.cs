using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace IronMarten.Bearing;

/// <summary>What to analyse, and what to leave out.</summary>
public sealed record WalkOptions
{
    /// <summary>Path to the solution.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Thresholds. Almost none of them affects the walk itself.</summary>
    /// <remarks>
    /// <b>Exactly one of the 29 values is read while the model is built:</b>
    /// <see cref="AnalysisPolicy.CohortBasisFloor"/>, which <c>ModelBuilder</c> hands to
    /// <c>CohortSet.Assign</c> and which is then written onto every <c>TypeNode.Cohort</c>.
    /// <see cref="AnalysisPolicy.MinCohort"/> and <see cref="AnalysisPolicy.MinTangle"/> are read
    /// by lazy caches on <see cref="SolutionModel"/>, and the other 26 only by detectors — so
    /// <see cref="SolutionModel.WithPolicy"/> can re-judge a finished model under a new policy
    /// without re-reading the solution, and refuses only when the basis floor moves.
    /// <para>
    /// This line said <i>"only <c>MinCohort</c> affects the walk itself"</i> until 2026-08-27.
    /// <c>MinCohort</c> is not read here at all, and the audit finding that proposed lifting
    /// cohort assignment out of the walk was quoting this comment as its evidence.
    /// <b>Cohort assignment was never in the walk</b> — it is the last step of the build, after
    /// the expensive work is done.
    /// </para>
    /// </remarks>
    public AnalysisPolicy Policy { get; init; } = AnalysisPolicy.Default;

    /// <summary>Whether to analyse projects that look like test projects.</summary>
    public bool IncludeTests { get; init; }

    /// <summary>
    /// Whether the built-in exclusions were dropped rather than added to — <c>--no-default-excludes</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk does not read this, and it is configuration all the same.</b> The host resolves
    /// the flag into <see cref="ExcludedPathFragments"/> before the walk is handed anything, so by
    /// the time Core sees a run there is only a list and no record of how it was arrived at.
    /// <see cref="ToolVersion"/> is carried here for the same reason and is read by nothing in the
    /// walk either: this record is <i>how the run was configured</i>, not <i>what the walker
    /// branches on</i>.
    /// </para>
    /// <para>
    /// <b>It is here because the findings export needs it and must not infer it.</b> It is
    /// recoverable from the fragment list — the defaults are absent when it is set — and
    /// <c>SCHEMA-findings-export.md</c> §3 rejects exactly that move: reading configuration off a
    /// side effect is the same mistake as reading a suppression off a finding's absence, which is
    /// what <c>suppressedBy</c> exists to stop.
    /// </para>
    /// </remarks>
    public bool DefaultExcludesCleared { get; init; }

    /// <summary>
    /// The version of the tool doing the analysing, which the host supplies because Core cannot
    /// know it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SolutionModel.ToolVersion"/> used to read
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
    /// Where NuGet restores packages to, when it is not the default <c>~/.nuget/packages</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is an argument because §5 forbids the alternative.</b> This decides
    /// <see cref="ExternalOrigin.Package"/> versus <see cref="ExternalOrigin.Unknown"/> for every
    /// external reference, which reaches the integration map and the external-surface section.
    /// Core read <c>NUGET_PACKAGES</c> out of the environment to get it, so two machines analysing
    /// the same solution classified it differently — the one property §5 opens by requiring, "same
    /// inputs, same output, every time", broken by the only ambient read in Core. Reading the
    /// environment is the host's job, the same call <c>Bearing.Cli.csproj</c> records for
    /// <c>MSBuildLocator</c>.
    /// </para>
    /// <para>
    /// <b>Null is not "no cache".</b> The default <c>~/.nuget/packages</c> is matched by path
    /// regardless, because that is a fact about NuGet rather than about this machine. This carries
    /// only the relocation, so a host that says nothing gets the behaviour the variable being
    /// unset always gave.
    /// </para>
    /// </remarks>
    public string? NuGetCachePath { get; init; }

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
/// what a raw stack trace looked like.
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

    /// <summary>
    /// How long each stage of the last completed walk took.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On the walker rather than on the model, and that is the point of it.</b> A profile is a
    /// fact about one run on one machine; the model is the thing two runs over one commit are
    /// asserted to produce identically. Hanging a wall-clock reading off the model would put a
    /// number that changes every run inside the artifact whose whole value is that it does not.
    /// </para>
    /// <para>
    /// <see cref="WalkProfile.None"/> until <see cref="WalkAsync"/> returns, and replaced whole
    /// when it does — a walk that threw leaves the previous answer rather than a partial one.
    /// </para>
    /// </remarks>
    public WalkProfile Profile { get; private set; } = WalkProfile.None;

    /// <summary>Loads the solution and walks it.</summary>
    /// <remarks>
    /// <para>
    /// <b>Five steps, named, and it used to be one method.</b> At cc 25 over 109 lines with four
    /// levels of nesting it was the worst method in this codebase on all three measures at once —
    /// which is the tool's own verdict on itself, and the reason it was split. Nothing about the
    /// order or the work changed; what changed is that each step can be read without the other
    /// four in view. The two closures stay closures because each is a projection of
    /// <c>compilations</c> that the builder holds for the length of the walk.
    /// </para>
    /// <para>
    /// <b>Those five steps are also the five stages of <see cref="Profile"/></b>, which is not a
    /// coincidence worth preserving by accident: a step that stops being its own method stops
    /// being its own reading, and A12 exists because "32s" with no seam in it could not be
    /// argued about.
    /// </para>
    /// </remarks>
    public async Task<SolutionModel> WalkAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        var skipped = new List<string>();
        var clock = new WalkClock();

        // Creating the workspace is inside the Open stage rather than before it: it is where
        // MSBuild's assemblies load, and on a small solution that costs more than opening the
        // file does. Timed from outside it, the stage understates MSBuild and the difference
        // lands in the residual, which is the one row that explains nothing.
        var opened = WalkClock.Now();

        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        workspace.WorkspaceFailed += (_, e) =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                diagnostics.Add(e.Diagnostic.Message);
        };

        var solution = await OpenAsync(workspace, cancellationToken).ConfigureAwait(false);
        var projects = SelectProjects(solution, skipped);
        clock.Add(WalkStage.Open, opened);

        var compiled = WalkClock.Now();
        var (compilations, projectNodes, notLoaded, unreadable, unresolved) =
            await CompileAsync(projects, diagnostics, cancellationToken).ConfigureAwait(false);
        clock.Add(WalkStage.Compile, compiled);
        clock.Projects = compilations.Count;

        var indexed = WalkClock.Now();
        var builder = new ModelBuilder(
            _options,
            InSolutionOf(compilations),
            OriginOfAssembly(compilations, _options.NuGetCachePath),
            clock);
        clock.Add(WalkStage.Index, indexed);

        var walked = WalkClock.Now();
        await WalkTypesAsync(builder, compilations, clock, cancellationToken).ConfigureAwait(false);
        clock.Add(WalkStage.Walk, walked);

        var built = WalkClock.Now();
        var model = builder.Build(_options.SolutionPath, projectNodes, new Coverage
        {
            ExclusionsApplied = _options.ExcludedPathFragments,
            SkippedProjects = skipped,
            LoadDiagnostics = diagnostics,
            ProjectsNotLoaded = notLoaded,
            ExcludedTypes = builder.ExcludedTypes,
            UnreadableFiles = unreadable,
            ProjectsWithUnresolvedReferences = unresolved,
        });
        clock.Add(WalkStage.Build, built);

        Profile = clock.Freeze();
        return model;
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
    /// <para>
    /// <b>The same rule one level down, for a file rather than a project</b> —
    /// A tree that did not parse is removed from the compilation
    /// before anything walks it, because what came out of walking one was not a missing type but a
    /// type recorded under the wrong namespace.
    /// </para>
    /// </remarks>
    private static async Task<(List<(Project Project, Compilation Compilation)> Compilations,
                              List<ProjectNode> Nodes,
                              List<string> NotLoaded,
                              List<string> Unreadable,
                              List<UnresolvedReferences> Unresolved)> CompileAsync(
        List<Project> projects, List<string> diagnostics, CancellationToken cancellationToken)
    {
        var compilations = new List<(Project Project, Compilation Compilation)>();
        var nodes = new List<ProjectNode>();
        var notLoaded = new List<string>();
        var unreadable = new List<string>();
        var unresolved = new List<UnresolvedReferences>();

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                // Both, deliberately: the diagnostic is the detail and the name is the outcome.
                // Only the second bounds the numbers.
                diagnostics.Add($"No compilation for {project.Name}");
                notLoaded.Add(project.Name);
                continue;
            }

            // Syntax diagnostics only. A project whose packages did not restore is full of
            // semantic errors and parses perfectly; refusing on those would refuse most real
            // solutions, and none of them puts a type under the wrong namespace.
            var broken = compilation.SyntaxTrees
                .Where(tree => tree.GetDiagnostics(cancellationToken)
                    .Any(d => d.Severity == DiagnosticSeverity.Error))
                .ToList();

            if (broken.Count > 0)
            {
                compilation = compilation.RemoveSyntaxTrees(broken);
                unreadable.AddRange(broken
                    .Select(tree => tree.FilePath)
                    .Where(path => !string.IsNullOrEmpty(path)));
            }

            // Counted here because this is the one place the compilation is
            // already in hand, and it is the only signal separating "compiled" from "compiled
            // against everything it names".
            //
            // GetDiagnostics binds every method body, which sounds like a second semantic pass and
            // is mostly a RESCHEDULED one: the walk pays for that binding lazily, one question at
            // a time. Measured on both reference solutions, before and after, with --profile:
            //
            //   nopCommerce   compile 10.5s -> 16.4s,  walk 15.5s -> 12.1s,  total 33.7s -> 35.2s
            //   Umbraco       compile  3.5s ->  8.2s,  walk 21.1s -> 12.4s,  total 30.4s -> 26.5s
            //
            // So it costs ~+1.5s on one and ~-3.9s on the other, against a 60s cold budget. Do not
            // "optimise" this into GetDeclarationDiagnostics: that skips method bodies, where a
            // CS0246 from an unrestored package is most of the count, and the saving is a stage
            // boundary rather than work.
            var unresolvedHere = compilation.GetDiagnostics(cancellationToken)
                .Count(d => d.Severity == DiagnosticSeverity.Error
                            && MissingReference.Contains(d.Id, StringComparer.Ordinal));

            if (unresolvedHere > 0) unresolved.Add(new UnresolvedReferences(project.Name, unresolvedHere));

            compilations.Add((project, compilation));
            nodes.Add(new ProjectNode(
                project.Name,
                compilation.GetEntryPoint(cancellationToken) is not null,
                compilation.Options.OutputKind == OutputKind.DynamicallyLinkedLibrary));
        }

        return (compilations, nodes, notLoaded, unreadable, unresolved);
    }

    /// <summary>
    /// The three errors a compilation emits when a reference did not resolve.
    /// </summary>
    /// <remarks>
    /// <c>CS0246</c> is <i>type or namespace not found</i>,
    /// <c>CS0234</c> is <i>does not exist in the namespace</i>, and <c>CS0012</c> is <i>defined in
    /// an assembly that is not referenced</i>. Three ids rather than "any error", because a
    /// project can have ordinary compile errors and still have restored — this is a question about
    /// the reference closure, not about whether the code is finished.
    /// <para>
    /// <b>An unrestored solution is the usual cause and not the only one, and Umbraco is both
    /// cases at once.</b> <c>Umbraco.JsonSchema</c> has no <c>project.assets.json</c> and cannot
    /// find <c>CommandLine</c>, <c>Namotion</c> or <c>NJsonSchema</c> — restore fixes it.
    /// <c>Umbraco.Core</c> is fully restored and still emits one, because
    /// <c>UmbracoBuilder.cs:325</c> registers <c>AddUnique&lt;IElementContainerService,
    /// ElementContainerService&gt;()</c> and <b>no file in the solution declares
    /// <c>ElementContainerService</c></b>. <b>Both are missing edges and the consequence is
    /// identical</b>, so both are counted — but the sentence that reports them must not promise
    /// that restoring closes the gap, because on the second it will not.
    /// </para>
    /// </remarks>
    private static readonly string[] MissingReference = ["CS0246", "CS0234", "CS0012"];

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
        List<(Project Project, Compilation Compilation)> compilations,
        string? nuGetCachePath)
    {
        var byAssembly = new Dictionary<string, ExternalOrigin>(StringComparer.Ordinal);

        foreach (var (_, compilation) in compilations)
        {
            foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;

                var origin = OriginOfPath(reference.FilePath, nuGetCachePath);
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
        WalkClock clock,
        CancellationToken cancellationToken)
    {
        foreach (var (project, compilation) in compilations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read here rather than in CompileAsync, which already asks the same question for
            // ProjectNode: the answer is a symbol there and a boolean by the time it is stored.
            builder.NoteEntryPoint(compilation.GetEntryPoint(cancellationToken));

            foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace))
            {
                if (!ShouldAnalyse(type)) { builder.CountExclusion(); continue; }

                var node = builder.GetOrAdd(type, compilation.Assembly.Name, project.Name);
                clock.Types++;

                foreach (var declaration in type.DeclaringSyntaxReferences)
                {
                    var fetched = WalkClock.Now();
                    var syntax = await declaration.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                    clock.Add(WalkStage.Syntax, fetched);

                    if (syntax is not TypeDeclarationSyntax and not EnumDeclarationSyntax) continue;
                    if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree)) continue;

                    var bound = WalkClock.Now();
                    var semantics = compilation.GetSemanticModel(syntax.SyntaxTree);
                    clock.Add(WalkStage.Semantics, bound);

                    clock.Declarations++;
                    builder.Walk(node, type, syntax, semantics);
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
    /// MSBuild's and open-ended: the crash was found as an
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
    /// <b>The reason it is not a list of names.</b> §5 is the
    /// standing example of what a curated list costs: it decides a classification, so anything the
    /// list has not heard of is silently sorted wrong. Names are also genuinely ambiguous here —
    /// <c>System.Text.Json</c> is in the shared framework on one target framework and a package on
    /// another, so the name cannot answer the question and the resolution always can.
    /// </para>
    /// <para>
    /// <b>What each path means.</b> The SDK resolves framework references out of the targeting
    /// packs (<c>packs/Microsoft.NETCore.App.Ref/…</c>) and the shared framework
    /// (<c>shared/Microsoft.NETCore.App/…</c>); NuGet restores packages into its global cache,
    /// which is the default <c>~/.nuget/packages</c> unless it was relocated. Those are facts
    /// about how restore works rather than about what anything is called.
    /// </para>
    /// <para>
    /// <b>The relocated cache arrives as an argument.</b> <paramref name="nuGetCachePath"/> is
    /// <see cref="WalkOptions.NuGetCachePath"/> and the host reads <c>NUGET_PACKAGES</c> to fill
    /// it; Core used to read the variable here, which made the classification a function of the
    /// machine rather than of the inputs. The default path below is a fact about NuGet and holds
    /// without asking anyone.
    /// </para>
    /// <para>
    /// Anything else is <see cref="ExternalOrigin.Unknown"/> and stays unknown — a solution-local
    /// <c>packages/</c> folder, a checked-in lib directory, a reference assembly somebody points at
    /// directly. Guessing there would reintroduce exactly the failure this avoids, and the
    /// name-based plumbing filter still applies to whatever lands here.
    /// </para>
    /// </remarks>
    private static ExternalOrigin OriginOfPath(string? path, string? nuGetCachePath)
    {
        if (string.IsNullOrEmpty(path)) return ExternalOrigin.Unknown;

        var normalized = path.Replace('\\', '/');

        var cache = nuGetCachePath;
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

    /// <summary>
    /// The solution formats MSBuild's own parser cannot read, which this opens project by project.
    /// </summary>
    /// <remarks>
    /// One entry, and it is a list so that adding the next format is a line rather than a second
    /// branch. <c>.slnx</c> is XML and MSBuild's solution parser rejects it outright —
    /// <c>MSB4068: The element &lt;Solution&gt; is unrecognized</c>, because MSBuild reads it as a
    /// project file — so <c>OpenSolutionAsync</c> cannot be given one at any version currently
    /// resolvable here.
    /// </remarks>
    private static readonly string[] ReadDirectly = [".slnx"];

    private async Task<Solution> OpenAsync(MSBuildWorkspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            return ReadDirectly.Contains(Path.GetExtension(_options.SolutionPath), StringComparer.OrdinalIgnoreCase)
                ? await OpenProjectsAsync(workspace, cancellationToken).ConfigureAwait(false)
                : await workspace
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

    /// <summary>
    /// A solution whose container MSBuild will not parse, opened one project at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the container is new; the projects are ordinary.</b> A <c>.slnx</c> lists the same
    /// <c>.csproj</c> files a <c>.sln</c> does, and MSBuild evaluates each of those exactly as it
    /// always has — so the gap is a file format, not a toolchain. Reading it with the serializer
    /// Visual Studio and the SDK use, then handing the paths to <c>OpenProjectAsync</c>, closes
    /// the <c>.slnx</c> gap without moving off the Roslyn version every golden was measured
    /// against.
    /// </para>
    /// <para>
    /// <b>Project references come in on their own.</b> <c>OpenProjectAsync</c> follows them, so a
    /// project named by another but missing from the solution file still arrives — the same
    /// behaviour <c>OpenSolutionAsync</c> has, and the reason this returns
    /// <see cref="Workspace.CurrentSolution"/> at the end rather than accumulating what each call
    /// returned.
    /// </para>
    /// <para>
    /// <b>A solution with no projects in it is opened, not refused.</b> An empty <c>&lt;Solution
    /// /&gt;</c> parses, and what comes back is a walk over nothing — which is what an empty
    /// <c>.sln</c> already produces. Treating it as unreadable would be the tool disagreeing with
    /// the file about whether the file is valid.
    /// </para>
    /// </remarks>
    private async Task<Solution> OpenProjectsAsync(
        MSBuildWorkspace workspace, CancellationToken cancellationToken)
    {
        var serializer = SolutionSerializers.GetSerializerByMoniker(_options.SolutionPath)
                         ?? throw new InvalidOperationException(
                             $"No solution serializer handles '{_options.SolutionPath}'.");

        var model = await serializer.OpenAsync(_options.SolutionPath, cancellationToken).ConfigureAwait(false);

        var directory = Path.GetDirectoryName(Path.GetFullPath(_options.SolutionPath)) ?? ".";

        foreach (var project in model.SolutionProjects)
        {
            var full = Path.GetFullPath(Path.Combine(directory, project.FilePath));

            // Solution folders and shared-project entries have no .csproj behind them, and
            // SkipUnrecognizedProjects only covers what the workspace was asked to open.
            if (!File.Exists(full)) continue;

            // Already here because something opened before it referenced it. OpenProjectAsync
            // throws rather than no-opping on a second open — "'Core' is already part of the
            // workspace" — so the transitive pull described above has to be checked for, not just
            // relied on. Any solution whose projects reference each other hits this, which is
            // most of them.
            if (Opened(workspace).Contains(full)) continue;

            await workspace.OpenProjectAsync(full, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return workspace.CurrentSolution;
    }

    /// <summary>
    /// The project files the workspace already holds, by full path.
    /// </summary>
    /// <remarks>
    /// Read fresh on each iteration rather than accumulated, because opening one project can add
    /// several — a set this method maintained itself would know only about the ones it asked for.
    /// Case-insensitive: the path in a solution file and the path MSBuild resolved differ in case
    /// often enough on Windows, and a miss here is an exception rather than a duplicate.
    /// </remarks>
    private static HashSet<string> Opened(Workspace workspace) =>
        workspace.CurrentSolution.Projects
            .Select(p => p.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetFullPath(p!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

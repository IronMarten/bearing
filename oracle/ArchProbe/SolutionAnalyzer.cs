using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace ArchProbe;

sealed class AnalysisResult
{
    public List<TypeMetrics> Types = new();
    public List<MethodMetrics> Methods = new();
    public List<(string From, string To, int Weight)> Edges = new();
    public List<string> SkippedProjects = new();
    public List<string> LoadWarnings = new();
    public int ExcludedTypes;
    public Dictionary<string, BaselineRow> BaselineRows;
    public List<ProjectInfo> Projects = new();
}

/// <summary>
/// Enough about a project to tell a genuinely unreferenced one from a root. A host with no
/// inbound dependencies is the top of the tree, not dead code.
/// </summary>
sealed class ProjectInfo
{
    public string Name = "";
    public bool HasEntryPoint;      // a Main - console, worker, modern web host
    public bool IsLibrary;
}

sealed class SolutionAnalyzer
{
    readonly Options _opt;

    public SolutionAnalyzer(Options opt) => _opt = opt;

    public async Task<AnalysisResult> RunAsync(CancellationToken ct)
    {
        var result = new AnalysisResult();

        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        workspace.WorkspaceFailed += (_, e) =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                result.LoadWarnings.Add(e.Diagnostic.Message);
        };

        Console.Error.WriteLine($"Opening {_opt.SolutionPath} ...");
        var solution = await workspace.OpenSolutionAsync(
            _opt.SolutionPath,
            new ConsoleProgress(),
            ct);

        var projects = solution.Projects
            .Where(p => p.Language == LanguageNames.CSharp)
            .Where(p =>
            {
                if (_opt.IncludeTests) return true;
                if (Options.LooksLikeTestProject(p.Name))
                {
                    result.SkippedProjects.Add(p.Name);
                    return false;
                }
                return true;
            })
            .ToList();

        if (projects.Count == 0)
            throw new InvalidOperationException("No C# projects loaded. Is the solution restored?");

        Console.Error.WriteLine($"Analyzing {projects.Count} project(s)...");

        // Pass 0: compile everything, so we know which assemblies are "ours".
        var compilations = new List<(Project Project, Compilation Compilation)>();
        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null)
            {
                result.LoadWarnings.Add($"No compilation for {project.Name}");
                continue;
            }
            compilations.Add((project, compilation));
            result.Projects.Add(new ProjectInfo
            {
                Name = project.Name,
                HasEntryPoint = compilation.GetEntryPoint(ct) != null,
                IsLibrary = compilation.Options.OutputKind == OutputKind.DynamicallyLinkedLibrary,
            });
        }

        var solutionAssemblies = new HashSet<string>(
            compilations.Select(c => c.Compilation.Assembly.Name), StringComparer.Ordinal);

        bool IsInSolution(ISymbol s) =>
            s?.ContainingAssembly != null && solutionAssemblies.Contains(s.ContainingAssembly.Name);

        // Pass 1: per-type metrics and outbound edges.
        var byId = new Dictionary<string, TypeMetrics>(StringComparer.Ordinal);
        var candidates = new Dictionary<string, List<Cohorts.Candidate>>(StringComparer.Ordinal);
        var edgeWeights = new Dictionary<(string, string), int>();

        foreach (var (project, compilation) in compilations)
        {
            ct.ThrowIfCancellationRequested();
            Console.Error.WriteLine($"  {project.Name}");

            foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace))
            {
                if (!ShouldAnalyze(type)) { result.ExcludedTypes++; continue; }

                var id = Fq(type);
                if (!byId.TryGetValue(id, out var tm))
                {
                    tm = NewTypeMetrics(type, project.Name);
                    byId[id] = tm;
                    candidates[id] = Cohorts.CandidatesFor(type, t => IsInSolution(t));
                }

                foreach (var declRef in type.DeclaringSyntaxReferences)
                {
                    var node = await declRef.GetSyntaxAsync(ct);
                    if (node is not TypeDeclarationSyntax && node is not EnumDeclarationSyntax) continue;

                    var tree = node.SyntaxTree;
                    if (!compilation.ContainsSyntaxTree(tree)) continue;
                    var model = compilation.GetSemanticModel(tree);

                    AnalyzeDeclaration(tm, type, node, model, id, IsInSolution, edgeWeights, result, project.Name);
                }
            }
        }

        // Pass 2: invert edges into fan-in.
        foreach (var ((from, to), weight) in edgeWeights)
        {
            if (byId.TryGetValue(to, out var target))
            {
                target.InboundTypes.Add(from);
                target.InboundRefCount += weight;
            }
            result.Edges.Add((from, to, weight));
        }

        result.Types = byId.Values.ToList();

        // Effective fan-out: drop dependencies on abstractions and data contracts. An
        // interface reference is what dependency inversion produces, not a coupling risk,
        // and a data contract is inert shape with no behaviour to break.
        var insulating = new HashSet<string>(
            byId.Values
                .Where(t => t.IsAbstract || t.TypeKeyword == "Interface" || t.Kind == "Contract")
                .Select(t => t.Id),
            StringComparer.Ordinal);

        foreach (var t in result.Types)
            t.FanOutEffective = t.OutboundTypes.Count(id => !insulating.Contains(id));

        // Architectural role is a real peer group for things that have no structural one:
        // a solution with a single DbContext and two repositories has no repository
        // cohort, but it does have a data-access cohort. "Internal" is excluded — it's a
        // catch-all, no more meaningful than the namespace it would displace.
        foreach (var t in result.Types)
            if (t.Kind != "Internal" && candidates.TryGetValue(t.Id, out var list))
                list.Add(new Cohorts.Candidate("kind:" + t.Kind, "architectural kind", 3));

        Cohorts.Assign(result.Types, candidates, _opt.MinCohort);

        foreach (var m in result.Methods)
            if (byId.TryGetValue(m.DeclaringTypeId, out var owner))
                m.Cohort = owner.Cohort;

        return result;
    }

    void AnalyzeDeclaration(
        TypeMetrics tm,
        INamedTypeSymbol type,
        SyntaxNode node,
        SemanticModel model,
        string typeId,
        Func<ISymbol, bool> isInSolution,
        Dictionary<(string, string), int> edgeWeights,
        AnalysisResult result,
        string projectName)
    {
        // ---- outbound references -------------------------------------------------
        var walker = new ReferenceWalker(model, node, symbol =>
        {
            var target = ResolveToNamedType(symbol);
            if (target == null) return;

            if (isInSolution(target))
            {
                var targetId = Fq(target);
                if (targetId == typeId) return;
                tm.OutboundTypes.Add(targetId);
                var key = (typeId, targetId);
                edgeWeights[key] = edgeWeights.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            else
            {
                var ns = ExternalNamespaceLabel(target);
                if (ns != null) tm.ExternalNamespaces.Add(ns);
            }
        });
        walker.Visit(node);

        // ---- per-member complexity ----------------------------------------------
        var span = node.GetLocation().GetLineSpan();
        tm.Loc += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        if (node is TypeDeclarationSyntax typeDecl)
        {
            foreach (var member in typeDecl.Members)
            {
                if (member is BaseTypeDeclarationSyntax) continue; // nested type: own row

                var decl = model.GetDeclaredSymbol(member);
                tm.MemberCount++;
                if (decl != null && decl.DeclaredAccessibility == Accessibility.Public)
                    tm.PublicMemberCount++;

                var cw = new ComplexityWalker(model, member);
                cw.Visit(member);

                var hasBody = HasExecutableBody(member);
                if (hasBody) tm.ExecutableMembers++;
                var memberCc = cw.Cyclomatic + (hasBody ? 1 : 0);
                tm.Cyclomatic += memberCc;
                tm.Dsm += cw.Dsm;
                tm.Transform += cw.Transform;
                tm.StaticMutations += cw.StaticMutations;

                if (memberCc > tm.MaxMemberCyclomatic)
                {
                    tm.MaxMemberCyclomatic = memberCc;
                    tm.MaxMemberName = MemberName(member);
                }

                AccumulateSurface(tm, decl, isInSolution);

                if (member is MethodDeclarationSyntax or ConstructorDeclarationSyntax)
                {
                    var mspan = member.GetLocation().GetLineSpan();
                    result.Methods.Add(new MethodMetrics
                    {
                        Id = decl != null ? Fq(decl) : typeId + "." + MemberName(member),
                        Name = MemberName(member),
                        DeclaringType = tm.Name,
                        DeclaringTypeId = typeId,
                        Project = projectName,
                        File = mspan.Path,
                        Line = mspan.StartLinePosition.Line + 1,
                        Accessibility = decl?.DeclaredAccessibility.ToString() ?? "",
                        Cyclomatic = memberCc,
                        Dsm = cw.Dsm,
                        Transform = cw.Transform,
                        StaticMutations = cw.StaticMutations,
                        MaxNestingDepth = cw.MaxNesting,
                        ParamCount = (member as MethodDeclarationSyntax)?.ParameterList?.Parameters.Count
                                     ?? (member as ConstructorDeclarationSyntax)?.ParameterList?.Parameters.Count
                                     ?? 0,
                        Loc = mspan.EndLinePosition.Line - mspan.StartLinePosition.Line + 1
                    });
                }
            }
        }

        tm.Kind = ClassifyKind(type, tm);
    }

    /// <summary>
    /// Data-parameters, two ways: raw parameter count, and a depth-1 expansion of the
    /// shapes crossing the boundary (a DTO with 30 properties is a bigger contract than
    /// an int, even though both are "one parameter").
    /// </summary>
    static void AccumulateSurface(TypeMetrics tm, ISymbol member, Func<ISymbol, bool> isInSolution)
    {
        if (member == null || member.DeclaredAccessibility != Accessibility.Public) return;

        switch (member)
        {
            case IMethodSymbol m:
                tm.ParamCount += m.Parameters.Length;
                foreach (var p in m.Parameters) tm.DataShape += ShapeBreadth(p.Type, isInSolution);
                tm.DataShape += ShapeBreadth(m.ReturnType, isInSolution);
                break;

            case IPropertySymbol p2:
                tm.ParamCount += 1;
                tm.DataShape += ShapeBreadth(p2.Type, isInSolution);
                break;
        }
    }

    static int ShapeBreadth(ITypeSymbol type, Func<ISymbol, bool> isInSolution)
    {
        if (type == null || type.SpecialType != SpecialType.None) return 1;

        // Unwrap Task<T>, IEnumerable<T>, arrays, nullables.
        type = Unwrap(type);
        if (type is not INamedTypeSymbol named) return 1;
        if (!isInSolution(named)) return 1;

        var props = named.GetMembers()
            .Count(m => m.DeclaredAccessibility == Accessibility.Public
                        && m is IPropertySymbol or IFieldSymbol);

        return Math.Max(1, Math.Min(props, 100)); // cap so one giant DTO can't dominate
    }

    static ITypeSymbol Unwrap(ITypeSymbol type)
    {
        for (var i = 0; i < 4; i++)
        {
            if (type is IArrayTypeSymbol arr) { type = arr.ElementType; continue; }
            if (type is INamedTypeSymbol n && n.IsGenericType && n.TypeArguments.Length == 1)
            {
                var name = n.OriginalDefinition.Name;
                if (name is "Task" or "ValueTask" or "Nullable" or "IEnumerable" or "List"
                         or "IList" or "ICollection" or "IReadOnlyList" or "IReadOnlyCollection"
                         or "IAsyncEnumerable")
                {
                    type = n.TypeArguments[0];
                    continue;
                }
            }
            break;
        }
        return type;
    }

    static string ClassifyKind(INamedTypeSymbol type, TypeMetrics tm)
    {
        var attrs = type.GetAttributes().Select(a => a.AttributeClass?.Name ?? "").ToList();
        var baseNames = new List<string>();
        for (var b = type.BaseType; b != null; b = b.BaseType) baseNames.Add(b.Name);

        bool AnyExternal(params string[] prefixes) =>
            tm.ExternalNamespaces.Any(ns => prefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal)));

        if (attrs.Any(a => a is "ApiControllerAttribute" or "RouteAttribute")
            || baseNames.Any(b => b is "ControllerBase" or "Controller" or "ApiController")
            || type.Name.EndsWith("Controller", StringComparison.Ordinal)
            || AnyExternal("Microsoft.AspNetCore"))
            return "ApiBoundary";

        if (baseNames.Contains("DbContext")
            || AnyExternal("Microsoft.EntityFrameworkCore", "System.Data", "Dapper", "NHibernate"))
            return "DataAccess";

        if (AnyExternal("System.Net.Http", "Azure.", "Amazon.", "RabbitMQ", "Confluent.Kafka",
                        "MassTransit", "Stripe", "Twilio", "SendGrid", "Polly"))
            return "ExternalCall";

        // Property bag with no behaviour — a contract, not a component.
        //
        // "No ordinary methods" alone is too loose: a service exposing only a computed
        // property (an IClock with `UtcNow => DateTime.UtcNow`, a config accessor, an
        // options wrapper) has no methods either, and misreading it as a contract would
        // silently drop it from every consumer's effective fan-out. So require that the
        // members carry no bodies at all — a real contract is auto-properties and shape.
        if (tm.MemberCount > 0 && tm.PublicMemberCount > 0 && tm.ExecutableMembers == 0)
        {
            var behaviouralMembers = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Count(m => m.MethodKind == MethodKind.Ordinary);
            if (behaviouralMembers == 0) return "Contract";
        }

        return "Internal";
    }

    static INamedTypeSymbol ResolveToNamedType(ISymbol symbol)
    {
        var candidate = symbol switch
        {
            INamedTypeSymbol nt => nt,
            IArrayTypeSymbol arr => arr.ElementType as INamedTypeSymbol,
            null => null,
            _ => symbol.ContainingType
        };

        if (candidate == null) return null;
        if (candidate.SpecialType != SpecialType.None) return null;   // int, string, object...
        if (candidate.TypeKind == TypeKind.Error) return null;
        return (INamedTypeSymbol)candidate.OriginalDefinition;
    }

    /// <summary>
    /// Namespace label for an out-of-solution type: "Stripe", "Azure.Messaging",
    /// "System.Net.Http". Platform roots get three segments because their taxonomies are
    /// deep and two would collapse System.Net.Http into System.Net — which silently broke
    /// the Kind classification, since the prefixes it matches on are three deep.
    /// </summary>
    static string ExternalNamespaceLabel(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns) || ns == "<global namespace>") return null;

        var parts = ns.Split('.');
        var depth = parts[0] is "System" or "Microsoft" ? 3 : 2;
        return string.Join(".", parts.Take(Math.Min(depth, parts.Length)));
    }

    bool ShouldAnalyze(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length == 0) return false;
        if (type.IsImplicitlyDeclared) return false;
        if (type.TypeKind == TypeKind.Delegate) return false;

        foreach (var r in type.DeclaringSyntaxReferences)
            if (_opt.IsExcludedPath(r.SyntaxTree.FilePath ?? "")) return false;

        return true;
    }

    static TypeMetrics NewTypeMetrics(INamedTypeSymbol type, string project)
    {
        var loc = type.Locations.FirstOrDefault(l => l.IsInSource);
        var span = loc?.GetLineSpan();
        return new TypeMetrics
        {
            Id = Fq(type),
            Name = type.Name,
            Namespace = type.ContainingNamespace?.ToDisplayString() ?? "",
            Project = project,
            File = span?.Path ?? "",
            Line = (span?.StartLinePosition.Line ?? 0) + 1,
            TypeKeyword = type.TypeKind.ToString(),
            IsAbstract = type.IsAbstract
        };
    }

    static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in EnumerateNested(t)) yield return nested;
        }
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var t in EnumerateTypes(child))
                yield return t;
    }

    static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol type)
    {
        foreach (var t in type.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in EnumerateNested(t)) yield return nested;
        }
    }

    static bool HasExecutableBody(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Body != null || m.ExpressionBody != null,
        ConstructorDeclarationSyntax => true,
        PropertyDeclarationSyntax p => p.ExpressionBody != null
                                       || (p.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) ?? false),
        OperatorDeclarationSyntax => true,
        ConversionOperatorDeclarationSyntax => true,
        _ => false
    };

    static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.ValueText,
        ConstructorDeclarationSyntax c => ".ctor",
        PropertyDeclarationSyntax p => p.Identifier.ValueText,
        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "<field>",
        EventDeclarationSyntax e => e.Identifier.ValueText,
        _ => member.Kind().ToString()
    };

    static string Fq(ISymbol s) => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    sealed class ConsoleProgress : IProgress<ProjectLoadProgress>
    {
        public void Report(ProjectLoadProgress value)
        {
            if (value.Operation == ProjectLoadOperation.Resolve)
                Console.Error.WriteLine($"  loaded {Path.GetFileNameWithoutExtension(value.FilePath)}");
        }
    }
}

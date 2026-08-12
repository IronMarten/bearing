using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IronMarten.Bearing;

/// <summary>
/// Accumulates the structure model as the walk proceeds.
/// </summary>
/// <remarks>
/// Separated from <see cref="SolutionWalker"/> so that the workspace plumbing and the
/// measurement are not the same method. The builder never touches a workspace; the walker never
/// decides what anything means.
/// </remarks>
internal sealed class ModelBuilder
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// Member signatures include the containing type and the parameters.
    /// </summary>
    /// <remarks>
    /// <c>FullyQualifiedFormat</c> qualifies type symbols and leaves member symbols bare, which
    /// is why the probe's member ids collapse twelve <c>Apply</c> methods into one — see
    /// <c>docs/DEFECTS.md</c> §13.
    /// </remarks>
    private static readonly SymbolDisplayFormat MemberFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType
                           | SymbolDisplayMemberOptions.IncludeParameters)
        .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType);

    private readonly WalkOptions _options;
    private readonly Func<ISymbol?, bool> _isInSolution;
    private readonly Dictionary<string, TypeNode> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CohortCandidate>> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To), List<TypeReference>> _references = [];
    private readonly Dictionary<string, SubjectRef> _subjects = new(StringComparer.Ordinal);

    internal ModelBuilder(WalkOptions options, Func<ISymbol?, bool> isInSolution)
    {
        _options = options;
        _isInSolution = isInSolution;
    }

    internal int ExcludedTypes { get; private set; }

    internal void CountExclusion() => ExcludedTypes++;

    internal TypeNode GetOrAdd(INamedTypeSymbol type, string assembly, string project)
    {
        var fqn = Fq(type);
        var subject = SubjectRef.ForType(assembly, fqn);
        var key = subject.Canonical;

        if (_types.TryGetValue(key, out var existing)) return existing;

        var location = type.Locations.FirstOrDefault(l => l.IsInSource)?.GetLineSpan();
        var node = new TypeNode(
            subject,
            assembly,
            fqn,
            type.Name,
            type.ContainingNamespace?.ToDisplayString() ?? "",
            project,
            type.TypeKind.ToString(),
            type.IsAbstract,
            location is { } span ? new SourceLocation(span.Path ?? "", span.StartLinePosition.Line + 1) : SourceLocation.None);

        _types[key] = node;
        _subjects[key] = subject;
        _candidates[key] = CandidatesFor(type).ToList();

        return node;
    }

    /// <summary>Walks one declaration of a type — a partial type has several.</summary>
    internal void Walk(TypeNode node, INamedTypeSymbol type, SyntaxNode syntax, SemanticModel model)
    {
        CollectReferences(node, syntax, model);

        var span = syntax.GetLocation().GetLineSpan();
        node.LinesOfCode += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        if (syntax is not TypeDeclarationSyntax declaration) return;

        foreach (var member in declaration.Members)
        {
            if (member is BaseTypeDeclarationSyntax) continue;   // nested type: its own node

            var symbol = model.GetDeclaredSymbol(member);
            node.MemberCount++;
            if (symbol?.DeclaredAccessibility == Accessibility.Public) node.PublicMemberCount++;

            var complexity = new ComplexityCollector(model, member);
            complexity.Visit(member);

            var hasBody = HasExecutableBody(member);
            if (hasBody) node.ExecutableMemberCount++;

            AccumulateSurface(node, symbol);

            var memberSpan = member.GetLocation().GetLineSpan();
            node.Members.Add(new Member(
                MemberSubject(node, symbol, member),
                MemberName(member),
                symbol?.DeclaredAccessibility.ToString() ?? "",
                new SourceLocation(memberSpan.Path ?? "", memberSpan.StartLinePosition.Line + 1),
                complexity.Cyclomatic + (hasBody ? 1 : 0),
                complexity.Dsm,
                complexity.Transform,
                complexity.StaticMutations,
                complexity.MaxNesting,
                ParameterCountOf(member),
                memberSpan.EndLinePosition.Line - memberSpan.StartLinePosition.Line + 1));
        }
    }

    private void CollectReferences(TypeNode node, SyntaxNode syntax, SemanticModel model)
    {
        var collector = new ReferenceCollector(model, syntax, (symbol, kind, site) =>
        {
            var target = ResolveToNamedType(symbol);
            if (target is null) return;

            if (_isInSolution(target))
            {
                var targetSubject = SubjectRef.ForType(target.ContainingAssembly!.Name, Fq(target));
                if (string.Equals(targetSubject.Canonical, node.Subject.Canonical, StringComparison.Ordinal)) return;

                node.AddOutbound(targetSubject);

                var key = (node.Subject.Canonical, targetSubject.Canonical);
                if (!_references.TryGetValue(key, out var list))
                    _references[key] = list = [];

                list.Add(new TypeReference(node.Subject, targetSubject, kind, site));
            }
            else if (ExternalNamespaceLabel(target) is { } ns)
            {
                node.ExternalNamespaces.Add(ns);
            }
        });

        collector.Visit(syntax);
    }

    /// <summary>
    /// Assigns the architectural role, with the evidence that decided it.
    /// </summary>
    /// <remarks>
    /// Called after the declarations are walked, because most of the evidence is the set of
    /// out-of-solution namespaces the type touches, and that is not known until then.
    /// </remarks>
    internal static void Classify(TypeNode node, INamedTypeSymbol type)
    {
        var attributes = type.GetAttributes().Select(a => a.AttributeClass?.Name ?? "").ToList();

        var bases = new List<string>();
        for (var b = type.BaseType; b is not null; b = b.BaseType) bases.Add(b.Name);

        string? ExternalMatch(params string[] prefixes) => node.ExternalNamespaces
            .FirstOrDefault(ns => prefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal)));

        var attribute = attributes.FirstOrDefault(a => a is "ApiControllerAttribute" or "RouteAttribute");
        if (attribute is not null)
        {
            node.Classification = new TypeClassification("ApiBoundary", "attribute:" + attribute);
            return;
        }

        var baseType = bases.FirstOrDefault(b => b is "ControllerBase" or "Controller" or "ApiController");
        if (baseType is not null)
        {
            node.Classification = new TypeClassification("ApiBoundary", "base:" + baseType);
            return;
        }

        if (type.Name.EndsWith("Controller", StringComparison.Ordinal))
        {
            node.Classification = new TypeClassification("ApiBoundary", "name-suffix:Controller");
            return;
        }

        if (ExternalMatch("Microsoft.AspNetCore") is { } web)
        {
            node.Classification = new TypeClassification("ApiBoundary", "external-ns:" + web);
            return;
        }

        if (bases.Contains("DbContext"))
        {
            node.Classification = new TypeClassification("DataAccess", "base:DbContext");
            return;
        }

        if (ExternalMatch("Microsoft.EntityFrameworkCore", "System.Data", "Dapper", "NHibernate") is { } data)
        {
            node.Classification = new TypeClassification("DataAccess", "external-ns:" + data);
            return;
        }

        if (ExternalMatch("System.Net.Http", "Azure.", "Amazon.", "RabbitMQ", "Confluent.Kafka",
                          "MassTransit", "Stripe", "Twilio", "SendGrid", "Polly") is { } external)
        {
            node.Classification = new TypeClassification("ExternalCall", "external-ns:" + external);
            return;
        }

        // A property bag with no behaviour is a contract, not a component. "No ordinary methods"
        // alone is too loose: a service exposing only a computed property — an IClock with
        // `UtcNow => DateTime.UtcNow`, a config accessor, an options wrapper — has no methods
        // either, and misreading it as a contract would drop it from every consumer's effective
        // fan-out. So require that no member carries a body at all.
        if (node.MemberCount > 0 && node.PublicMemberCount > 0 && node.ExecutableMemberCount == 0
            && !type.GetMembers().OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Ordinary))
        {
            node.Classification = new TypeClassification("Contract", "shape:no executable members");
        }
    }

    internal SolutionModel Build(string solutionPath, IReadOnlyList<ProjectNode> projects, Coverage coverage)
    {
        // Inbound is the inverse of outbound, from the same walk — never a second traversal.
        foreach (var (key, references) in _references)
        {
            if (!_types.TryGetValue(key.To, out var target)) continue;
            target.AddInbound(_subjects[key.From]);
            target.InboundReferenceCount += references.Count;
        }

        var insulating = _types.Values
            .Where(t => t.IsAbstract
                        || string.Equals(t.TypeKeyword, "Interface", StringComparison.Ordinal)
                        || string.Equals(t.Classification.Kind, "Contract", StringComparison.Ordinal))
            .Select(t => t.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var type in _types.Values)
            type.EffectiveFanOut = type.Outbound.Count(o => !insulating.Contains(o.Canonical));

        // Architectural role is a real peer group for things with no structural one, and it is
        // only known now — which is why it is appended rather than derived up front.
        foreach (var type in _types.Values)
            if (CohortCandidates.ForArchitecturalKind(type.Classification.Kind) is { } kind)
                _candidates[type.Subject.Canonical].Add(kind);

        var edges = _references
            .Select(kv => new Edge(_subjects[kv.Key.From], _subjects[kv.Key.To], kv.Value))
            .OrderBy(e => e.From.Canonical, StringComparer.Ordinal)
            .ThenBy(e => e.To.Canonical, StringComparer.Ordinal)
            .ToList();

        var types = _types.Values
            .OrderBy(t => t.Subject.Canonical, StringComparer.Ordinal)
            .ToList();

        return new SolutionModel(solutionPath, _options.Policy, projects, types, edges, coverage);
    }

    /// <summary>The cohort candidates for every analysed type, for <see cref="CohortSet"/>.</summary>
    internal IEnumerable<CohortSubject> CohortSubjects() =>
        _candidates.Select(kv => new CohortSubject(kv.Key, kv.Value));

    private IEnumerable<CohortCandidate> CandidatesFor(INamedTypeSymbol type)
    {
        var interfaces = type.Interfaces
            .Where(i => _isInSolution(i))
            .Select(i => Fq(i.OriginalDefinition))
            .ToList();

        var baseType = type.BaseType is { } b && b.SpecialType != SpecialType.System_Object && _isInSolution(b)
            ? Fq(b.OriginalDefinition)
            : null;

        return CohortCandidates.For(new TypeShape(
            type.Name,
            type.ContainingNamespace?.ToDisplayString() ?? "",
            type.TypeKind == TypeKind.Interface,
            interfaces,
            baseType));
    }

    private static SubjectRef MemberSubject(TypeNode node, ISymbol? symbol, MemberDeclarationSyntax member) =>
        SubjectRef.ForMember(
            node.Assembly,
            node.FullyQualifiedName,
            symbol is not null ? symbol.ToDisplayString(MemberFormat) : MemberName(member));

    /// <summary>
    /// Data parameters, two ways: the raw count, and a depth-1 expansion of the shapes crossing
    /// the boundary. A DTO with thirty properties is a bigger contract than an int, even though
    /// both are one parameter.
    /// </summary>
    private void AccumulateSurface(TypeNode node, ISymbol? member)
    {
        if (member is null || member.DeclaredAccessibility != Accessibility.Public) return;

        switch (member)
        {
            case IMethodSymbol method:
                node.ParameterCount += method.Parameters.Length;
                foreach (var p in method.Parameters) node.DataShape += ShapeBreadth(p.Type);
                node.DataShape += ShapeBreadth(method.ReturnType);
                break;

            case IPropertySymbol property:
                node.ParameterCount += 1;
                node.DataShape += ShapeBreadth(property.Type);
                break;
        }
    }

    private int ShapeBreadth(ITypeSymbol? type)
    {
        if (type is null || type.SpecialType != SpecialType.None) return 1;

        type = Unwrap(type);
        if (type is not INamedTypeSymbol named || !_isInSolution(named)) return 1;

        var properties = named.GetMembers()
            .Count(m => m.DeclaredAccessibility == Accessibility.Public && m is IPropertySymbol or IFieldSymbol);

        return Math.Max(1, Math.Min(properties, 100));   // cap, so one giant DTO cannot dominate
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type)
    {
        for (var i = 0; i < 4; i++)
        {
            if (type is IArrayTypeSymbol array) { type = array.ElementType; continue; }

            if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1
                && named.OriginalDefinition.Name is "Task" or "ValueTask" or "Nullable" or "IEnumerable"
                    or "List" or "IList" or "ICollection" or "IReadOnlyList" or "IReadOnlyCollection"
                    or "IAsyncEnumerable")
            {
                type = named.TypeArguments[0];
                continue;
            }

            break;
        }

        return type;
    }

    private static INamedTypeSymbol? ResolveToNamedType(ISymbol? symbol)
    {
        var candidate = symbol switch
        {
            INamedTypeSymbol named => named,
            IArrayTypeSymbol array => array.ElementType as INamedTypeSymbol,
            null => null,
            _ => symbol.ContainingType,
        };

        if (candidate is null) return null;
        if (candidate.SpecialType != SpecialType.None) return null;      // int, string, object...
        if (candidate.TypeKind == TypeKind.Error) return null;

        return (INamedTypeSymbol)candidate.OriginalDefinition;
    }

    /// <summary>
    /// Namespace label for an out-of-solution type.
    /// </summary>
    /// <remarks>
    /// Platform roots get three segments because their taxonomies are deep, and two would
    /// collapse <c>System.Net.Http</c> into <c>System.Net</c> — which silently broke
    /// classification, since the prefixes it matches on are three deep.
    /// </remarks>
    private static string? ExternalNamespaceLabel(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns) || ns == "<global namespace>") return null;

        var parts = ns.Split('.');
        var depth = parts[0] is "System" or "Microsoft" ? 3 : 2;
        return string.Join(".", parts.Take(Math.Min(depth, parts.Length)));
    }

    private static bool HasExecutableBody(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Body is not null || m.ExpressionBody is not null,
        ConstructorDeclarationSyntax => true,
        PropertyDeclarationSyntax p => p.ExpressionBody is not null
                                       || (p.AccessorList?.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null) ?? false),
        OperatorDeclarationSyntax => true,
        ConversionOperatorDeclarationSyntax => true,
        _ => false,
    };

    private static int ParameterCountOf(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.ParameterList.Parameters.Count,
        ConstructorDeclarationSyntax c => c.ParameterList.Parameters.Count,
        _ => 0,
    };

    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.ValueText,
        ConstructorDeclarationSyntax => ".ctor",
        PropertyDeclarationSyntax p => p.Identifier.ValueText,
        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "<field>",
        EventDeclarationSyntax e => e.Identifier.ValueText,
        _ => member.Kind().ToString(),
    };

    private static string Fq(ISymbol symbol) => symbol.ToDisplayString(TypeFormat);
}

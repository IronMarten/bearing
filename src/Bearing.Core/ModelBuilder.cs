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
    private readonly Func<ISymbol?, ExternalOrigin> _originOf;

    /// <summary>
    /// Where the two halves of <see cref="Walk"/> are charged — <see cref="WalkProfile"/>.
    /// </summary>
    /// <remarks>
    /// The builder times itself rather than being timed from outside because the split that
    /// matters is inside one method: collecting references is the stage A9 changes, and the member
    /// metrics beside it are not. A caller holding the stopwatch could only report their sum.
    /// </remarks>
    private readonly WalkClock _clock;

    /// <summary>
    /// Where each external namespace resolved from — <c>docs/DEFECTS.md</c> §30.
    /// </summary>
    /// <remarks>
    /// One namespace can be reached through more than one assembly, and the answers can differ:
    /// a package that also ships in the shared framework is the ordinary case. <b>Package wins.</b>
    /// The question the origin answers is "could somebody change this", and if any route to the
    /// namespace is a package reference then somebody could.
    /// </remarks>
    private readonly Dictionary<string, ExternalOrigin> _externalOrigins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeNode> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CohortCandidate>> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To), List<TypeReference>> _references = [];

    /// <summary>Inbound member references, keyed on the member they point at — A9's first layer.</summary>
    private readonly Dictionary<string, List<MemberReference>> _memberReferences = new(StringComparer.Ordinal);

    /// <summary>Member subjects already built, so a signature is generated once rather than per reference.</summary>
    private readonly Dictionary<ISymbol, SubjectRef?> _memberSubjects = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string, SubjectRef> _subjects = new(StringComparer.Ordinal);

    internal ModelBuilder(
        WalkOptions options,
        Func<ISymbol?, bool> isInSolution,
        Func<ISymbol?, ExternalOrigin>? originOf = null,
        WalkClock? clock = null)
    {
        _originOf = originOf ?? (_ => ExternalOrigin.Unknown);
        _options = options;
        _isInSolution = isInSolution;
        _clock = clock ?? new WalkClock();
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
        var collecting = WalkClock.Now();
        CollectReferences(node, syntax, model);
        _clock.Add(WalkStage.References, collecting);

        var measuring = WalkClock.Now();
        WalkMembers(node, syntax, model);
        _clock.Add(WalkStage.Members, measuring);
    }

    /// <summary>The per-declaration metrics: how big the declaration is, and what each member costs.</summary>
    /// <remarks>
    /// Split out of <see cref="Walk"/> so that the two halves of a declaration's cost can be
    /// charged separately — see <see cref="_clock"/>. The work is unchanged and the order is the
    /// order it was in.
    /// </remarks>
    private void WalkMembers(TypeNode node, SyntaxNode syntax, SemanticModel model)
    {
        var span = syntax.GetLocation().GetLineSpan();
        node.LinesOfCode += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        if (syntax is not TypeDeclarationSyntax declaration) return;

        foreach (var member in declaration.Members)
        {
            if (member is BaseTypeDeclarationSyntax) continue;   // nested type: its own node

            foreach (var (symbol, declarator) in DeclaredBy(member, model))
                AddMember(node, member, symbol, declarator, model);
        }
    }

    /// <summary>
    /// The members one declaration declares, with the syntax that declares each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usually one, and for a field declaration it is one per variable</b> — <c>int a, b;</c> is
    /// two fields, with two names, two accessibilities and two sets of callers. It used to be one
    /// member named <c>a</c>, which is <c>docs/DEFECTS.md</c> §39: a dead-code claim cannot be made
    /// about a member the model never separated from its neighbour.
    /// </para>
    /// <para>
    /// <b>This is also the only place a field can get a symbol at all.</b>
    /// <c>GetDeclaredSymbol</c> answers <see langword="null"/> for a
    /// <c>BaseFieldDeclarationSyntax</c> — the declaration is the variable under it — which is why
    /// every field shipped with a blank accessibility and no contribution to the public surface.
    /// </para>
    /// </remarks>
    private static IEnumerable<(ISymbol? Symbol, SyntaxNode Declarator)> DeclaredBy(
        MemberDeclarationSyntax member, SemanticModel model)
    {
        if (member is not BaseFieldDeclarationSyntax field)
        {
            yield return (model.GetDeclaredSymbol(member), member);
            yield break;
        }

        foreach (var variable in field.Declaration.Variables)
            yield return (model.GetDeclaredSymbol(variable), variable);
    }

    /// <summary>Records one member, with the metrics of the syntax that declares it.</summary>
    /// <remarks>
    /// <paramref name="member"/> decides what kind of thing this is and whether it has a body;
    /// <paramref name="declarator"/> is what gets measured, so two initialisers on one field line
    /// are not charged to each other.
    /// </remarks>
    private void AddMember(
        TypeNode node,
        MemberDeclarationSyntax member,
        ISymbol? symbol,
        SyntaxNode declarator,
        SemanticModel model)
    {
        node.MemberCount++;
        if (symbol?.DeclaredAccessibility == Accessibility.Public) node.PublicMemberCount++;

        var complexity = new ComplexityCollector(model, declarator);
        complexity.Visit(declarator);

        var hasBody = HasExecutableBody(member);
        if (hasBody) node.ExecutableMemberCount++;

        AccumulateSurface(node, symbol);

        var memberSpan = declarator.GetLocation().GetLineSpan();
        node.Members.Add(new Member(
            MemberSubject(node, symbol, member),
            MemberName(symbol, member),
            SignatureOf(symbol, member),
            KindOf(member),
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

    private void CollectReferences(TypeNode node, SyntaxNode syntax, SemanticModel model)
    {
        var collector = new ReferenceCollector(model, syntax, found =>
        {
            var target = ResolveToNamedType(found.Symbol);
            if (target is null) return;

            if (_isInSolution(target))
            {
                var targetSubject = SubjectRef.ForType(target.ContainingAssembly!.Name, Fq(target));

                // The member graph is recorded first, and before the self-edge guard below — A9,
                // and see MemberReference. A private helper's only caller is usually a sibling on
                // its own type, and that is exactly the reference the type graph must not contain
                // and the dead-code question cannot do without.
                RecordMemberReference(node, target, found);

                if (string.Equals(targetSubject.Canonical, node.Subject.Canonical, StringComparison.Ordinal)) return;

                node.AddOutbound(targetSubject);

                var key = (node.Subject.Canonical, targetSubject.Canonical);
                if (!_references.TryGetValue(key, out var list))
                    _references[key] = list = [];

                list.Add(new TypeReference(node.Subject, targetSubject, found.Kind, found.Site));
            }
            else if (ExternalNamespaceLabel(target) is { } ns)
            {
                node.ExternalNamespaces.Add(ns);
                RecordOrigin(ns, _originOf(target));
            }
        });

        collector.Visit(syntax);
    }

    /// <summary>
    /// Records one reference in the member graph, when both it and its target are members.
    /// </summary>
    /// <remarks>
    /// <b>A reference to a type is not a reference to a member and is dropped here.</b>
    /// <c>Foo x</c> names a type; <c>new Foo()</c> names its constructor and is kept. Keeping the
    /// first would give every member of <c>Foo</c> an inbound reference it does not have, which is
    /// invariant 4's "safe to delete" inverted.
    /// </remarks>
    private void RecordMemberReference(TypeNode node, INamedTypeSymbol target, FoundReference found)
    {
        if (MemberSubjectOf(found.Symbol, target) is not { } to) return;

        var from = found.Within is null ? null : SubjectOfDeclared(node, found.Within);

        if (!_memberReferences.TryGetValue(to.Canonical, out var list))
            _memberReferences[to.Canonical] = list = [];

        list.Add(new MemberReference(from, to, found.Kind, found.Site));
    }

    /// <summary>
    /// The subject of the member a reference points at, or <see langword="null"/> if it points at
    /// something that is not one.
    /// </summary>
    /// <remarks>
    /// <b>Cached on the symbol, because this runs once per reference and the walk's other member
    /// work does not.</b> nopCommerce produces 112,124 references over 25,165 members, so the same
    /// signature is generated forty times over otherwise — and a documentation comment ID is built
    /// rather than read off. The declaring type comes from <paramref name="target"/>, which
    /// <see cref="ResolveToNamedType"/> has already reduced to its original definition, so a
    /// reference through <c>Foo&lt;int&gt;</c> and one through <c>Foo&lt;string&gt;</c> land on the
    /// same member.
    /// </remarks>
    private SubjectRef? MemberSubjectOf(ISymbol symbol, INamedTypeSymbol target)
    {
        if (symbol is not (IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol)) return null;

        var definition = symbol.OriginalDefinition;
        if (_memberSubjects.TryGetValue(definition, out var cached)) return cached;

        var id = definition.GetDocumentationCommentId();
        var subject = id is null || target.ContainingAssembly is null
            ? null
            : SubjectRef.ForMember(target.ContainingAssembly.Name, Fq(target), id);

        return _memberSubjects[definition] = subject;
    }

    /// <summary>
    /// The subject of a member being <i>declared</i> in the type currently being walked.
    /// </summary>
    /// <remarks>
    /// Built from <paramref name="node"/> rather than from the symbol's own containing type, so it
    /// is the same string <see cref="MemberSubject"/> produced when the member was recorded. The
    /// two have to join or the member graph points at nothing.
    /// </remarks>
    private SubjectRef? SubjectOfDeclared(TypeNode node, ISymbol member)
    {
        if (_memberSubjects.TryGetValue(member, out var cached)) return cached;

        var id = member.GetDocumentationCommentId();
        var subject = id is null
            ? null
            : SubjectRef.ForMember(node.Assembly, node.FullyQualifiedName, id);

        return _memberSubjects[member] = subject;
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

        // The lists and the matching rule are FrameworkNamespaces, so both can be asserted —
        // docs/DEFECTS.md §5 could be undone with the suite green until they were. TASKS.md P10.
        string? ExternalMatch(IReadOnlyList<string> prefixes) =>
            FrameworkNamespaces.Match(node.ExternalNamespaces, prefixes);

        var attribute = attributes.FirstOrDefault(a => a is "ApiControllerAttribute" or "RouteAttribute");
        if (attribute is not null)
        {
            node.Classification = new TypeClassification(TypeKinds.ApiBoundary, "attribute:" + attribute);
            return;
        }

        var baseType = bases.FirstOrDefault(b => b is "ControllerBase" or "Controller" or "ApiController");
        if (baseType is not null)
        {
            node.Classification = new TypeClassification(TypeKinds.ApiBoundary, "base:" + baseType);
            return;
        }

        if (type.Name.EndsWith("Controller", StringComparison.Ordinal))
        {
            node.Classification = new TypeClassification(TypeKinds.ApiBoundary, "name-suffix:Controller");
            return;
        }

        if (ExternalMatch(FrameworkNamespaces.ApiBoundary) is { } web)
        {
            node.Classification = new TypeClassification(TypeKinds.ApiBoundary, "external-ns:" + web);
            return;
        }

        if (bases.Contains("DbContext"))
        {
            node.Classification = new TypeClassification(TypeKinds.DataAccess, "base:DbContext");
            return;
        }

        // docs/DEFECTS.md §5. LinqToDB and FluentMigrator were missing, and on nopCommerce that
        // was not a silence — it was 114 of Nop.Data's 129 Internal types, the *Builder mapping
        // layer under Nop.Data/Mapping/Builders, one per entity. The 23 that did classify were
        // caught by System.Data rather than by any ORM rule: right by coincidence.
        //
        // FluentMigrator is schema migration rather than querying, and it belongs here for the
        // reason TypeKinds.DataAccess states — "reaches a database or persistence framework". A
        // type that defines how an entity maps to a table is data access by any reading a
        // developer would give it.
        if (ExternalMatch(FrameworkNamespaces.DataAccess) is { } data)
        {
            node.Classification = new TypeClassification(TypeKinds.DataAccess, "external-ns:" + data);
            return;
        }

        if (ExternalMatch(FrameworkNamespaces.ExternalCall) is { } external)
        {
            node.Classification = new TypeClassification(TypeKinds.ExternalCall, "external-ns:" + external);
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
            node.Classification = new TypeClassification(TypeKinds.Contract, "shape:no executable members");
        }
    }

    internal SolutionModel Build(string solutionPath, IReadOnlyList<ProjectNode> projects, Coverage coverage)
    {
        // Inbound is the inverse of outbound, from the same walk — never a second traversal.
        foreach (var (key, references) in _references)
        {
            if (!_types.TryGetValue(key.To, out var target)) continue;
            if (!_subjects.TryGetValue(key.From, out var source)) continue;

            target.AddInbound(source);
            target.InboundReferenceCount += references.Count;
        }

        var insulating = _types.Values
            .Where(t => t.IsAbstractOrInterface
                        || string.Equals(t.Classification.Kind, TypeKinds.Contract, StringComparison.Ordinal))
            .Select(t => t.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var type in _types.Values)
            type.EffectiveFanOut = type.Outbound.Count(o => !insulating.Contains(o.Canonical));

        // Architectural role is a real peer group for things with no structural one, and it is
        // only known now — which is why it is appended rather than derived up front.
        foreach (var type in _types.Values)
            if (CohortCandidates.ForArchitecturalKind(type.Classification.Kind) is { } kind)
                _candidates[type.Subject.Canonical].Add(kind);

        var cohorts = CohortSet.Assign(CohortSubjects(), _options.Policy.MinCohort);
        foreach (var type in _types.Values)
        {
            type.Cohort = cohorts[type.Subject.Canonical];
            type.CohortSize = cohorts.SizeOf(type.Subject.Canonical);
        }

        // An endpoint the walk never declared cannot be an edge in the report, and looking one up
        // unguarded is what crashed Bearing on both reference solutions — docs/DEFECTS.md §7, whose
        // consequence turned out to be a KeyNotFoundException rather than a small inaccuracy.
        //
        // `_isInSolution` answers "does this symbol belong to a project in this solution", which is
        // not the same question as "did the walk produce a node for it": a type is skipped when its
        // file matches an exclusion, when it lives in a skipped project, or when it is compiler
        // territory rather than anyone's design. Every one of those still resolves to a symbol a
        // reference can point at.
        //
        // Dropped rather than invented, and counted rather than dropped silently — invariant 8. A
        // reader who sees no disclosure is entitled to assume the graph is complete.
        var edges = new List<Edge>(_references.Count);
        var unresolved = 0;

        foreach (var (key, references) in _references)
        {
            if (_subjects.TryGetValue(key.From, out var from) && _subjects.TryGetValue(key.To, out var to))
                edges.Add(new Edge(from, to, references));
            else
                unresolved++;
        }

        coverage.EdgesToUnanalysedTypes = unresolved;

        edges = [.. edges
            .OrderBy(e => e.From.Canonical, StringComparer.Ordinal)
            .ThenBy(e => e.To.Canonical, StringComparer.Ordinal)];

        var types = _types.Values
            .OrderBy(t => t.Subject.Canonical, StringComparer.Ordinal)
            .ToList();

        AttachMemberReferences(types, coverage);

        // Projects are canonicalised for the same reason types and edges above them are, and were
        // not until R2 — docs/DEFECTS.md §37. They arrive in workspace load order, which is the
        // order the .sln declares them in, so reversing four lines of a solution file with no
        // semantic content reordered the projects array in the JSON export. The terminal and HTML
        // reports sorted for themselves and were never affected, which is exactly why nothing
        // noticed: one renderer out of three was reading an order the model never promised.
        var ordered = projects
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        return new SolutionModel(
            solutionPath, _options.Policy, _options.ToolVersion, ordered, types, edges, coverage,
            _externalOrigins);
    }

    /// <summary>
    /// Hands each member the references pointing at it, and counts the ones that point nowhere.
    /// </summary>
    /// <remarks>
    /// <b>The same shape as the type graph's unresolved count, and for the same reason.</b>
    /// <c>_isInSolution</c> answers "does this symbol belong to a project here", which is not "did
    /// the walk record a member for it" — an excluded file, a skipped test project and a
    /// compiler-generated member all resolve to symbols a reference can name. Counted rather than
    /// dropped silently: invariant 8, and A9 is going to claim that a member has no inbound
    /// references, so how many references failed to land on one is part of whether that claim can
    /// be trusted.
    /// </remarks>
    private void AttachMemberReferences(List<TypeNode> types, Coverage coverage)
    {
        var byCanonical = new Dictionary<string, Member>(StringComparer.Ordinal);
        foreach (var type in types)
            foreach (var member in type.Members)
                byCanonical[member.Subject.Canonical] = member;

        var unresolved = 0;

        foreach (var (canonical, references) in _memberReferences)
        {
            if (!byCanonical.TryGetValue(canonical, out var member))
            {
                unresolved += references.Count;
                continue;
            }

            foreach (var reference in references) member.AddInbound(reference);
        }

        coverage.MemberReferencesToUnanalysedMembers = unresolved;
    }

    /// <summary>Package beats Framework beats Unknown. See <see cref="_externalOrigins"/>.</summary>
    private void RecordOrigin(string @namespace, ExternalOrigin origin)
    {
        if (_externalOrigins.TryGetValue(@namespace, out var seen) && seen >= origin) return;
        _externalOrigins[@namespace] = origin;
    }

    private IEnumerable<CohortSubject> CohortSubjects() =>
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

    /// <summary>
    /// The member's identity: its declaring type, and Roslyn's documentation comment ID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Decision X14, and <c>docs/DEFECTS.md</c> §39 is what it replaces.</b> The signature used
    /// to be <c>symbol.ToDisplayString(MemberFormat)</c>, and a display string is not an identity:
    /// it drops <c>ref</c>, <c>out</c> and <c>in</c>, renders a static constructor exactly like an
    /// instance one, gives an explicit interface implementation the containing type's name, and —
    /// where the symbol was <see langword="null"/>, which was every field — was not a signature at
    /// all. The documentation comment ID separates all four by construction, because it is the form
    /// the compiler emits for cross-assembly references and has to.
    /// </para>
    /// <para>
    /// <b>The display string is kept as <see cref="Member.Signature"/> rather than dropped.</b>
    /// <c>M:Nop.Core.WebAppTypeFinder.#cctor</c> is exact and nobody wants to read a column of it.
    /// </para>
    /// <para>
    /// <b>The fallback is the display string, and it is reached only where Roslyn declines to
    /// answer.</b> <c>GetDocumentationCommentId</c> returns <see langword="null"/> for a symbol
    /// that cannot be referenced from documentation — and for a member with no symbol at all, which
    /// after <see cref="DeclaredBy"/> means a declaration this walk did not expect. Falling back
    /// keeps the model buildable; it does not restore the guarantee, so anything relying on
    /// uniqueness has to say so.
    /// </para>
    /// </remarks>
    private static SubjectRef MemberSubject(TypeNode node, ISymbol? symbol, MemberDeclarationSyntax member) =>
        SubjectRef.ForMember(
            node.Assembly,
            node.FullyQualifiedName,
            symbol?.GetDocumentationCommentId() ?? SignatureOf(symbol, member));

    /// <summary>The member as a developer would write it. Readable, and not unique.</summary>
    private static string SignatureOf(ISymbol? symbol, MemberDeclarationSyntax member) =>
        symbol is not null ? symbol.ToDisplayString(MemberFormat) : MemberName(symbol, member);

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

            // A public field is contract surface by this model's own definition — ShapeBreadth
            // counts one when it measures somebody else's type. Until X14 no field reached here
            // at all, because a field declaration has no symbol and this method takes one, so the
            // two halves disagreed: a public field widened the contract of every type that
            // depended on it and not the contract of the type that declared it.
            //
            // Events are deliberately not here, and for the same reason rather than in spite of
            // it: ShapeBreadth counts properties and fields, because the question is how much
            // data crosses the boundary. An event is a callback, and counting it would make
            // "widest contract surface" mean two things at once.
            case IFieldSymbol field:
                node.ParameterCount += 1;
                node.DataShape += ShapeBreadth(field.Type);
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

        // An anonymous type belongs to the compilation, so `_isInSolution` accepts it, and it then
        // becomes a reference target with a canonical name of `global::<anonymous type: int id>`
        // that no walk ever declared. On nopCommerce that crashed the build outright. It is also
        // not a component in any sense the report means: a reader cannot navigate to it, name it,
        // or change it, and the type that projected it is already the subject.
        if (candidate.IsAnonymousType) return null;

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

    /// <summary>
    /// The member's own name.
    /// </summary>
    /// <remarks>
    /// <b>The symbol first, and the syntax only when there is none.</b> The syntactic list had no
    /// arm for <c>EventFieldDeclarationSyntax</c>, so every event in a type was named
    /// <c>EventFieldDeclaration</c> — all 81 of Jellyfin's, collapsed into 15 subjects
    /// (<c>docs/DEFECTS.md</c> §39). A list of syntax kinds is a list that goes on being incomplete;
    /// the symbol knows its own name for every kind of member there is.
    /// </remarks>
    private static string MemberName(ISymbol? symbol, MemberDeclarationSyntax member) => symbol?.Name switch
    {
        null or "" => member switch
        {
            MethodDeclarationSyntax m => m.Identifier.ValueText,
            ConstructorDeclarationSyntax => ".ctor",
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "<field>",
            EventDeclarationSyntax e => e.Identifier.ValueText,
            _ => member.Kind().ToString(),
        },
        var name => name,
    };

    /// <summary>
    /// Which population a member belongs to.
    /// </summary>
    /// <remarks>
    /// <c>EventFieldDeclarationSyntax</c> is the ordinary <c>event Action Changed;</c> form and
    /// <c>EventDeclarationSyntax</c> is the one with accessors; they are different syntax nodes
    /// for the same kind of member, and separating them here would put half the events in
    /// <see cref="MemberKind.Other"/> for a reason no reader of the model could guess.
    /// </remarks>
    private static MemberKind KindOf(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax => MemberKind.Method,
        ConstructorDeclarationSyntax => MemberKind.Constructor,
        PropertyDeclarationSyntax => MemberKind.Property,
        FieldDeclarationSyntax => MemberKind.Field,
        EventDeclarationSyntax or EventFieldDeclarationSyntax => MemberKind.Event,
        _ => MemberKind.Other,
    };

    private static string Fq(ISymbol symbol) => symbol.ToDisplayString(TypeFormat);
}

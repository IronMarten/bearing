using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IronMarten.Bearing;

/// <summary>
/// Collects outbound type references from one type declaration, with the kind and location of
/// each.
/// </summary>
/// <remarks>
/// <para>
/// Walking identifier and generic names catches invocations, member access, object creation,
/// attributes and base lists, because each contains a resolvable name node. Nested type
/// declarations are not descended into — they are analysed as their own types.
/// </para>
/// <para>
/// The kind comes from the syntactic context the name appears in, walked upward from the name
/// node. It has to be collected here: reconstructing it later means a second full traversal of
/// the solution, and an edge without it can only be filtered by weight.
/// </para>
/// </remarks>
internal sealed class ReferenceCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel _model;
    private readonly SyntaxNode _root;
    private readonly Action<ISymbol, EdgeKind, SourceLocation> _onReference;

    internal ReferenceCollector(SemanticModel model, SyntaxNode root, Action<ISymbol, EdgeKind, SourceLocation> onReference)
        : base(SyntaxWalkerDepth.Node)
    {
        _model = model;
        _root = root;
        _onReference = onReference;
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node is null) return;
        if (node != _root && node is BaseTypeDeclarationSyntax) return;
        base.Visit(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node) => Resolve(node);

    public override void VisitGenericName(GenericNameSyntax node)
    {
        Resolve(node);
        base.VisitGenericName(node);   // the type arguments still need visiting
    }

    private void Resolve(SyntaxNode node)
    {
        var info = _model.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol is null) return;

        var span = node.GetLocation().GetLineSpan();
        var site = new SourceLocation(span.Path ?? "", span.StartLinePosition.Line + 1);

        _onReference(symbol, KindOf(node, symbol), site);
    }

    /// <summary>
    /// Classifies a reference by where it sits, walking outward from the name node.
    /// </summary>
    /// <remarks>
    /// Order matters: the innermost enclosing construct wins, so a type argument inside a
    /// parameter is a generic argument rather than a parameter. The walk stops at the first
    /// construct that says something, and at any member declaration, so a name in a method body
    /// is never attributed to the enclosing type's base list.
    /// </remarks>
    private static EdgeKind KindOf(SyntaxNode node, ISymbol referenced)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case TypeArgumentListSyntax:
                    return EdgeKind.GenericArgument;

                case AttributeSyntax:
                    return EdgeKind.Attribute;

                // A base list holds both, and only the referenced symbol says which is which —
                // C# does not distinguish them syntactically, and the base class need not come
                // first.
                case BaseTypeSyntax:
                    return referenced is INamedTypeSymbol { TypeKind: TypeKind.Interface }
                        ? EdgeKind.InterfaceImplementation
                        : EdgeKind.Inheritance;

                case ObjectCreationExpressionSyntax:
                case ImplicitObjectCreationExpressionSyntax:
                    return EdgeKind.Construction;

                case ParameterSyntax:
                    return EdgeKind.Parameter;

                case FieldDeclarationSyntax:
                case EventFieldDeclarationSyntax:
                    return EdgeKind.Field;

                // A property's own type is state; anything inside its accessors is not.
                case PropertyDeclarationSyntax property:
                    return property.Type.Span.Contains(node.Span) ? EdgeKind.Field : EdgeKind.Other;

                case MethodDeclarationSyntax method:
                    return method.ReturnType.Span.Contains(node.Span) ? EdgeKind.ReturnType : EdgeKind.Other;

                case InvocationExpressionSyntax invocation:
                    // Only the callee position is the call; arguments are their own references.
                    return invocation.Expression.Span.Contains(node.Span) ? EdgeKind.Invocation : EdgeKind.Other;

                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return EdgeKind.Other;
            }
        }

        return EdgeKind.Other;
    }
}

/// <summary>
/// Cyclomatic complexity and mutation counts for one member body.
/// </summary>
/// <remarks>
/// Cyclomatic counts decision points; the base of 1 for an executable body is added by the
/// caller. The mutation counters separate "this moves data around" from "this decides
/// something", which are different kinds of risk and are not usefully summed.
/// </remarks>
internal sealed class ComplexityCollector : CSharpSyntaxWalker
{
    private static readonly HashSet<string> MutatingCalls = new(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll",
        "RemoveRange", "Clear", "Push", "Pop", "Enqueue", "Dequeue", "TryAdd", "Sort",
    };

    private readonly SemanticModel _model;
    private readonly SyntaxNode _root;
    private readonly bool _inStaticConstructor;
    private readonly bool _inInstanceConstructor;
    private int _nesting;

    internal ComplexityCollector(SemanticModel model, SyntaxNode root)
        : base(SyntaxWalkerDepth.Node)
    {
        _model = model;
        _root = root;

        var isConstructor = root is ConstructorDeclarationSyntax;
        _inStaticConstructor = root is ConstructorDeclarationSyntax ctor
                               && ctor.Modifiers.Any(SyntaxKind.StaticKeyword);
        _inInstanceConstructor = isConstructor && !_inStaticConstructor;
    }

    /// <summary>Decision points, excluding the base of 1.</summary>
    internal int Cyclomatic { get; private set; }

    /// <summary>Destructive mutation: writes to existing state, and collection mutation.</summary>
    internal int Dsm { get; private set; }

    /// <summary>
    /// Non-destructive shaping: object initializers and <c>with</c> expressions.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="Dsm"/> rather than folded in at a fractional weight,
    /// because the two answer different questions — how dangerous is this (aliasing,
    /// thread-safety, where <c>with</c> scores zero) versus how much data-path work happens
    /// here (where it scores full). Merged at any weight, a high number no longer says which
    /// one you are looking at, and the weight becomes the thing people argue about.
    /// </remarks>
    internal int Transform { get; private set; }

    /// <summary>Writes to static mutable state, outside a static constructor.</summary>
    internal int StaticMutations { get; private set; }

    /// <summary>Deepest nesting reached.</summary>
    internal int MaxNesting { get; private set; }

    public override void Visit(SyntaxNode? node)
    {
        if (node is null) return;

        if (node != _root && node is BaseTypeDeclarationSyntax) return;
        if (node != _root && node is BaseMethodDeclarationSyntax) return;
        if (node != _root && node is BasePropertyDeclarationSyntax) return;

        var opensScope = node is BlockSyntax or SwitchStatementSyntax or SwitchExpressionSyntax;
        if (opensScope)
        {
            _nesting++;
            if (_nesting > MaxNesting) MaxNesting = _nesting;
        }

        CountDecisionPoints(node);
        CountMutations(node);

        base.Visit(node);

        if (opensScope) _nesting--;
    }

    private void CountDecisionPoints(SyntaxNode node)
    {
        switch (node)
        {
            case IfStatementSyntax:
            case WhileStatementSyntax:
            case DoStatementSyntax:
            case ForStatementSyntax:
            case ForEachStatementSyntax:
            case ForEachVariableStatementSyntax:
            case CaseSwitchLabelSyntax:
            case CasePatternSwitchLabelSyntax:
            case SwitchExpressionArmSyntax:
            case CatchClauseSyntax:
            case ConditionalExpressionSyntax:
            case ConditionalAccessExpressionSyntax:
            case WhenClauseSyntax:
            case BinaryPatternSyntax:
                Cyclomatic++;
                break;

            case BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.LogicalAndExpression)
                  || binary.IsKind(SyntaxKind.LogicalOrExpression)
                  || binary.IsKind(SyntaxKind.CoalesceExpression):
                Cyclomatic++;
                break;

            case AssignmentExpressionSyntax assignment
                when assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression):
                Cyclomatic++;
                break;
        }
    }

    private void CountMutations(SyntaxNode node)
    {
        switch (node)
        {
            case AssignmentExpressionSyntax assignment:
                if (assignment.Parent is InitializerExpressionSyntax initializer
                    && (initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
                        || initializer.IsKind(SyntaxKind.WithInitializerExpression)))
                {
                    Transform++;   // shaping a value, not mutating an existing one
                    break;
                }

                CountStateWrite(assignment.Left);
                break;

            // `_counter++` on static state is a non-atomic read-modify-write, the most classic
            // thread-unsafe update there is, so it routes through the same check as assignment.
            case PrefixUnaryExpressionSyntax prefix
                when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                  || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                CountStateWrite(prefix.Operand);
                break;

            case PostfixUnaryExpressionSyntax postfix
                when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                  || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                CountStateWrite(postfix.Operand);
                break;

            case InvocationExpressionSyntax invocation:
                if (invocation.Expression is MemberAccessExpressionSyntax access
                    && MutatingCalls.Contains(access.Name.Identifier.ValueText))
                    Dsm++;
                break;
        }
    }

    private void CountStateWrite(ExpressionSyntax target)
    {
        if (!IsPersistentState(target)) return;

        if (IsStaticState(target))
        {
            Dsm++;
            if (!_inStaticConstructor) StaticMutations++;
            return;
        }

        // Assigning your own instance members in a constructor is initialization, not mutation:
        // nothing else can observe the object yet, so there is no aliasing or ordering risk to
        // measure. Writes through some *other* object's reference still count.
        if (_inInstanceConstructor && IsOwnInstanceMember(target))
        {
            Transform++;
            return;
        }

        Dsm++;
    }

    private static bool IsOwnInstanceMember(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax access => access.Expression is ThisExpressionSyntax,
        _ => false,
    };

    private bool IsPersistentState(ExpressionSyntax expression)
    {
        // Element access (list[i] = x, dict[k] = v) is always a structure mutation.
        if (expression is ElementAccessExpressionSyntax) return true;

        return _model.GetSymbolInfo(expression).Symbol is IFieldSymbol or IPropertySymbol;
    }

    private bool IsStaticState(ExpressionSyntax expression) =>
        _model.GetSymbolInfo(expression).Symbol switch
        {
            IFieldSymbol f => f.IsStatic && !f.IsConst,
            IPropertySymbol p => p.IsStatic,
            _ => false,
        };
}

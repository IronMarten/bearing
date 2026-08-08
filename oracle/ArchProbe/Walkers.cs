using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchProbe;

/// <summary>
/// Collects outbound type references from a single type declaration, without descending
/// into nested type declarations (those are analyzed as their own rows).
/// Walking identifier and generic names catches invocations, member access, object
/// creation, attributes and base lists, because each contains a resolvable name node.
/// </summary>
sealed class ReferenceWalker : CSharpSyntaxWalker
{
    readonly SemanticModel _model;
    readonly SyntaxNode _root;
    readonly Action<ISymbol> _onSymbol;

    public ReferenceWalker(SemanticModel model, SyntaxNode root, Action<ISymbol> onSymbol)
        : base(SyntaxWalkerDepth.Node)
    {
        _model = model;
        _root = root;
        _onSymbol = onSymbol;
    }

    public override void Visit(SyntaxNode node)
    {
        // Don't descend into nested types — they get their own walk.
        if (node != _root && node is BaseTypeDeclarationSyntax) return;
        base.Visit(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node) => Resolve(node);

    public override void VisitGenericName(GenericNameSyntax node)
    {
        Resolve(node);
        base.VisitGenericName(node); // type arguments still need visiting
    }

    void Resolve(SyntaxNode node)
    {
        var info = _model.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol != null) _onSymbol(symbol);
    }
}

/// <summary>
/// Cyclomatic complexity + a data-structure-manipulation (DSM) proxy for one member body.
///
/// Cyclomatic counts decision points; the base 1 is added by the caller.
/// DSM counts mutations of persistent state (fields/properties, object initializers,
/// collection mutation calls) rather than local scratch variables — the intent is to
/// separate "this moves fields around" from "this decides something".
/// </summary>
sealed class ComplexityWalker : CSharpSyntaxWalker
{
    readonly SemanticModel _model;
    readonly SyntaxNode _root;

    public int Cyclomatic { get; private set; }

    /// <summary>Destructive mutation: writes to existing state, and collection mutation.</summary>
    public int Dsm { get; private set; }

    /// <summary>
    /// Non-destructive data shaping: object initializers and `with` expressions. Kept
    /// separate rather than folded into Dsm at a fractional weight, because the two
    /// answer different questions — see the note on CountMutations.
    /// </summary>
    public int Transform { get; private set; }

    /// <summary>Writes to static mutable state, outside a static constructor.</summary>
    public int StaticMutations { get; private set; }

    public int MaxNesting { get; private set; }

    readonly bool _inStaticConstructor;
    readonly bool _inInstanceConstructor;
    int _nesting;

    static readonly HashSet<string> MutatingCalls = new(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll",
        "RemoveRange", "Clear", "Push", "Pop", "Enqueue", "Dequeue", "TryAdd", "Sort"
    };

    public ComplexityWalker(SemanticModel model, SyntaxNode root)
        : base(SyntaxWalkerDepth.Node)
    {
        _model = model;
        _root = root;
        var isCtor = root is ConstructorDeclarationSyntax;
        var isStaticCtor = root is ConstructorDeclarationSyntax ctor
                           && ctor.Modifiers.Any(SyntaxKind.StaticKeyword);
        _inStaticConstructor = isStaticCtor;
        _inInstanceConstructor = isCtor && !isStaticCtor;
    }

    public override void Visit(SyntaxNode node)
    {
        if (node == null) return;

        // Don't descend into nested types or nested member declarations.
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

    void CountDecisionPoints(SyntaxNode node)
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

            case BinaryExpressionSyntax bin
                when bin.IsKind(SyntaxKind.LogicalAndExpression)
                  || bin.IsKind(SyntaxKind.LogicalOrExpression)
                  || bin.IsKind(SyntaxKind.CoalesceExpression):
                Cyclomatic++;
                break;

            case AssignmentExpressionSyntax asg
                when asg.IsKind(SyntaxKind.CoalesceAssignmentExpression):
                Cyclomatic++;
                break;
        }
    }

    /// <summary>
    /// Two counters, deliberately not one.
    ///
    /// Construction (`new Foo { A = 1 }`) is excluded entirely — it says nothing about
    /// risk in C#, and a genuinely complicated constructor is already caught by cc.
    ///
    /// `with` expressions are counted as Transform, not Dsm, and NOT at a fractional
    /// weight. A fraction would merge two signals that answer different questions: how
    /// dangerous is this (aliasing, thread-safety — where `with` scores zero) and how much
    /// data-path work is happening here (where it scores full). Merged at any weight, a
    /// high number no longer tells you which one you are looking at, and the weight itself
    /// becomes the thing people argue about. Two counters cost the same and stay legible:
    /// high Transform with zero Dsm reads as a functional pipeline doing real work.
    /// </summary>
    void CountMutations(SyntaxNode node)
    {
        switch (node)
        {
            case AssignmentExpressionSyntax asg:
                if (asg.Parent is InitializerExpressionSyntax init)
                {
                    // Shaping a value, not mutating an existing one.
                    if (init.IsKind(SyntaxKind.ObjectInitializerExpression)
                        || init.IsKind(SyntaxKind.WithInitializerExpression))
                    {
                        Transform++;
                        break;
                    }
                }

                CountStateWrite(asg.Left);
                break;

            // `_counter++` on static state is a non-atomic read-modify-write — the most
            // classic thread-unsafe update there is, so it has to route through the same
            // check as a plain assignment rather than only counting toward Dsm.
            case PrefixUnaryExpressionSyntax pre
                when pre.IsKind(SyntaxKind.PreIncrementExpression)
                  || pre.IsKind(SyntaxKind.PreDecrementExpression):
                CountStateWrite(pre.Operand);
                break;

            case PostfixUnaryExpressionSyntax post
                when post.IsKind(SyntaxKind.PostIncrementExpression)
                  || post.IsKind(SyntaxKind.PostDecrementExpression):
                CountStateWrite(post.Operand);
                break;

            // Object/with initializers are handled in the assignment case above, where
            // they route to Transform. Counting the InitializerExpressionSyntax here too
            // would double-count every one of them.

            case InvocationExpressionSyntax inv:
                if (inv.Expression is MemberAccessExpressionSyntax ma
                    && MutatingCalls.Contains(ma.Name.Identifier.ValueText))
                    Dsm++;
                break;
        }
    }

    void CountStateWrite(ExpressionSyntax target)
    {
        if (!IsPersistentState(target)) return;

        if (IsStaticState(target))
        {
            Dsm++;
            if (!_inStaticConstructor) StaticMutations++;
            return;
        }

        // Assigning your own instance members inside a constructor is initialization, not
        // mutation — nothing else can observe the object yet, so there is no aliasing or
        // ordering risk to measure. Counting it made every `Properties = new Collection()`
        // look like destructive state change. Writes through some *other* object's
        // reference still count, even in a constructor.
        if (_inInstanceConstructor && IsOwnInstanceMember(target))
        {
            Transform++;
            return;
        }

        Dsm++;
    }

    /// <summary>`Foo = x` or `this.Foo = x`, as opposed to `other.Foo = x`.</summary>
    static bool IsOwnInstanceMember(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax ma => ma.Expression is ThisExpressionSyntax,
        _ => false
    };

    bool IsPersistentState(ExpressionSyntax expr)
    {
        // Element access (list[i] = x, dict[k] = v) is always a structure mutation.
        if (expr is ElementAccessExpressionSyntax) return true;

        var symbol = _model.GetSymbolInfo(expr).Symbol;
        return symbol is IFieldSymbol or IPropertySymbol;
    }

    /// <summary>
    /// Static mutable state — a write that every caller on every thread shares. Whether
    /// the object is genuinely contended is a runtime question this cannot answer, but
    /// static is the one case where sharing is certain from the code alone.
    /// </summary>
    bool IsStaticState(ExpressionSyntax expr)
    {
        var symbol = _model.GetSymbolInfo(expr).Symbol;
        return symbol switch
        {
            IFieldSymbol f => f.IsStatic && !f.IsConst,
            IPropertySymbol p => p.IsStatic,
            _ => false
        };
    }
}

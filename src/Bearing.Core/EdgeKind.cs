namespace IronMarten.Bearing;

/// <summary>
/// How one type refers to another.
/// </summary>
/// <remarks>
/// <para>
/// Collected during the walk, because reconstructing it afterwards costs a second full
/// traversal of the solution. Without it the only filter a dependency-graph view can offer is
/// edge weight, which is the least interesting one available — and hiding abstraction and
/// contract edges is what makes a DIP-heavy codebase legible at all. On the two solutions
/// measured, those account for 39–50% of all out-edges.
/// </para>
/// <para>
/// <b>Fixed rather than extensible</b>, decided when the walker moved. The set is closed by the
/// language: there is a finite number of syntactic ways one type can name another, and this
/// enumerates them. An open taxonomy would push the cost onto every renderer, which has to
/// decide what to do with a kind it has never seen — and in practice that means showing
/// everything, which is the failure the filter exists to prevent. Adding a member later is a
/// compatible change, so the escape hatch is intact if the language grows one.
/// </para>
/// <para>
/// A reference can be several of these at once — a generic argument inside a parameter type
/// inside a method that is also invoked. Each syntactic site produces its own edge record, so
/// the same pair of types can be connected by several edges of different kinds. Weight is the
/// count of all of them.
/// </para>
/// </remarks>
public enum EdgeKind
{
    /// <summary>
    /// A reference the walk could not attribute to a more specific site: a cast, a local
    /// declaration, a <c>typeof</c>, a static member access.
    /// </summary>
    /// <remarks>
    /// Deliberately first, so it is the default. An edge that exists but is unattributed is
    /// still an edge, and dropping it would change the graph rather than the labelling.
    /// </remarks>
    Other,

    /// <summary>Extends a base class.</summary>
    Inheritance,

    /// <summary>Implements an interface.</summary>
    InterfaceImplementation,

    /// <summary>The declared type of a field or property — state this type holds.</summary>
    Field,

    /// <summary>The type of a method or constructor parameter.</summary>
    Parameter,

    /// <summary>A method or property return type.</summary>
    ReturnType,

    /// <summary>Constructed with <c>new</c>. The strongest form of concrete coupling.</summary>
    Construction,

    /// <summary>A method on the type is called.</summary>
    Invocation,

    /// <summary>Supplied as a type argument, e.g. <c>IEnumerable&lt;Order&gt;</c>.</summary>
    GenericArgument,

    /// <summary>Applied as an attribute.</summary>
    Attribute,
}

/// <summary>
/// Where in the source something is. Every clickable artifact depends on this existing.
/// </summary>
/// <param name="File">Absolute path to the source file.</param>
/// <param name="Line">One-based line number.</param>
public readonly record struct SourceLocation(string File, int Line)
{
    /// <summary>A location that is not known.</summary>
    public static SourceLocation None { get; } = new("", 0);

    /// <summary>Whether this refers to a real place in a real file.</summary>
    public bool IsKnown => !string.IsNullOrEmpty(File) && Line > 0;

    /// <inheritdoc/>
    public override string ToString() => IsKnown ? $"{File}:{Line}" : "(unknown)";
}

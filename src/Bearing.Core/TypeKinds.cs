namespace IronMarten.Bearing;

/// <summary>
/// The architectural roles a type can be classified as.
/// </summary>
/// <remarks>
/// <para>
/// <b>Constants rather than an enum, for now.</b> <see cref="TypeClassification.Kind"/> is a
/// string because it is written to CSV and JSON, and from 1.0 these exact strings are part of that
/// contract — <c>docs/ARCHITECTURE.md</c> §10. An enum is still the stronger form in code, and
/// adopting one stays mechanical as long as it serialises to these same names; renaming a value is
/// 2.0.
/// </para>
/// <para>
/// <b>What this closes.</b> The five values were spelled out as literals at eighteen sites across
/// eight files — assigned in <see cref="ModelBuilder"/> and matched against in seven other places,
/// each with its own private copy. A mistyped literal does not fail to compile: it silently never
/// matches, and the detector that depends on it quietly stops firing, which is the failure mode
/// this codebase is least able to notice. A mistyped constant is a build error.
/// </para>
/// <para>
/// <b>Not an open taxonomy.</b> A classifier is only useful if the set of answers is small enough
/// to reason about, and every consumer here switches on the whole set — see <see cref="EdgeKind"/>
/// for the same decision taken with the same reasoning. Adding a role is a deliberate change to
/// every detector that reads one, and it should look like one.
/// </para>
/// </remarks>
public static class TypeKinds
{
    /// <summary>Receives calls from outside the solution.</summary>
    public const string ApiBoundary = "ApiBoundary";

    /// <summary>Reaches a database or persistence framework.</summary>
    public const string DataAccess = "DataAccess";

    /// <summary>Calls out of the solution.</summary>
    public const string ExternalCall = "ExternalCall";

    /// <summary>Carries shape rather than behaviour — a DTO, a message, a request body.</summary>
    public const string Contract = "Contract";

    /// <summary>
    /// The catch-all: nothing identified an architectural role.
    /// </summary>
    /// <remarks>
    /// Excluded wherever "architecturally significant" is the question, because depending on
    /// ordinary code is not cross-cutting and counting it would put every type in the report.
    /// </remarks>
    public const string Internal = "Internal";
}

namespace IronMarten.Bearing;

/// <summary>
/// The external systems this solution talks to, and what was left out of that list.
/// </summary>
/// <param name="Systems">
/// Namespaces that represent an integration, most widely touched first. Ordered by how many types
/// touch them, then by name.
/// </param>
/// <param name="PlumbingReferences">
/// How many type-to-namespace references were omitted as language or framework plumbing. Reported
/// rather than dropped: a filtered list that does not say it filtered is a list a reader cannot
/// calibrate — <c>docs/DEFECTS.md</c> §3.
/// </param>
public readonly record struct IntegrationMap(
    IReadOnlyList<ExternalDependency> Systems,
    int PlumbingReferences);

/// <summary>
/// Where the solution meets everything outside it.
/// </summary>
/// <remarks>
/// Job A's half of the boundary section. The other half — which individual boundaries are
/// unusual — is <see cref="BoundaryMarking"/>, and the two are separate because they answer
/// different questions: this one is a property of the solution and makes no claim about any
/// subject, while those are findings that can be right or wrong about a particular type.
/// </remarks>
public static class ExternalSurface
{
    /// <summary>Types that receive calls from outside the solution.</summary>
    private const string Inbound = TypeKinds.ApiBoundary;

    /// <summary>Types that make calls to outside the solution.</summary>
    private const string Outbound = TypeKinds.ExternalCall;

    /// <summary>
    /// Language and framework namespaces, which are not integrations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Present in nearly every file, so listing them buries the two or three lines that actually
    /// matter under <c>System.Linq</c>. The point of the map is "what does this codebase talk
    /// to", and the answer is never the collections library.
    /// </para>
    /// <para>
    /// <b>A hardcoded list, like <c>docs/DEFECTS.md</c> §5's four ORMs, and it fails more
    /// gently.</b> §5's list decides a classification, so a missing entry silently changes what a
    /// type <i>is</i>; this one only decides what appears in a list whose omissions are counted
    /// out loud. A package this list has never heard of shows up as an integration, which is the
    /// safe direction to be wrong in.
    /// </para>
    /// </remarks>
    private static readonly string[] Plumbing =
    [
        "System.Collections", "System.Linq", "System.Text", "System.Threading",
        "System.Runtime", "System.Reflection", "System.Globalization", "System.ComponentModel",
        "System.Diagnostics", "System.Numerics", "Microsoft.Extensions", "Microsoft.CSharp",
    ];

    /// <summary>
    /// Whether a namespace is language or framework plumbing rather than an integration.
    /// </summary>
    /// <remarks>
    /// Prefix matching without a separator boundary, so <c>System.Text.Json</c> is plumbing by
    /// way of <c>System.Text</c>. That is the probe's behaviour and it is kept — but it is a
    /// looser match than it looks, and the reason it is tolerable is that only namespaces from
    /// outside the solution reach here. A team's own <c>System.TextProcessing</c> is never a
    /// candidate.
    /// </remarks>
    public static bool IsPlumbing(string @namespace)
    {
        ArgumentNullException.ThrowIfNull(@namespace);

        return string.Equals(@namespace, "System", StringComparison.Ordinal)
            || Array.Exists(Plumbing, prefix => @namespace.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// What this codebase talks to, and how widely.
    /// </summary>
    /// <summary>
    /// What this codebase talks to, and how widely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Origin does not decide this, and the fixture's known answer is why.</b>
    /// <c>docs/DEFECTS.md</c> §30 was read once as "framework-resolved means plumbing", which
    /// emptied the fixture's integration map: <c>System.Data</c> and <c>System.Net.Http</c> both
    /// resolve from the framework, and both are exactly how that solution reaches a database and
    /// the network. `CoreEquivalenceTests` caught it in one run.
    /// </para>
    /// <para>
    /// <b>The two questions are different.</b> Origin answers <i>"could somebody change this
    /// dependency"</i>; this filter answers <i>"does this reach outside the process"</i>. A reader
    /// asked for the first — they are not going to rewrite <c>System.IO</c> — and the answer to it
    /// is a label on the row, not a reason to drop the row. §5's list still makes this judgement,
    /// with its failure mode unchanged and stated where it is defined.
    /// </para>
    /// </remarks>
    public static IntegrationMap Integrations(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var everything = model.ExternalDependencies;

        return new IntegrationMap(
            everything.Where(d => !IsPlumbing(d.Namespace)).ToList(),
            everything.Where(d => IsPlumbing(d.Namespace)).Sum(d => d.TypesTouching));
    }

    /// <summary>
    /// Every type that sits on the edge of the solution, split by which way the calls run.
    /// </summary>
    /// <remarks>
    /// <b>A count, deliberately, and not a nomination.</b> Enumerating every controller fires on
    /// 100% of a category the reader already knows about, and a flag that never discriminates is
    /// one people learn to skip — which is exactly what this section replaced. The types are
    /// carried anyway because a renderer that can only say "15" cannot let a reader check the
    /// number, and because invariant 4's statement is about all of them: consumer impact of a
    /// change at any contact point is outside what static analysis can see.
    /// </remarks>
    public static ContactPoints Of(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ContactPoints(
            Kind(model, Inbound),
            Kind(model, Outbound));
    }

    private static List<TypeNode> Kind(SolutionModel model, string kind) =>
        model.Types
            .Where(t => string.Equals(t.Classification.Kind, kind, StringComparison.Ordinal))
            .OrderBy(t => t.Subject.Canonical, StringComparer.Ordinal)
            .ToList();
}

/// <summary>
/// The solution's external contact points.
/// </summary>
/// <param name="Inbound">Types outside callers reach into — API boundaries.</param>
/// <param name="Outbound">Types that call out of the solution.</param>
public readonly record struct ContactPoints(
    IReadOnlyList<TypeNode> Inbound,
    IReadOnlyList<TypeNode> Outbound)
{
    /// <summary>How many contact points there are in total.</summary>
    public int Count => Inbound.Count + Outbound.Count;
}

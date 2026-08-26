namespace IronMarten.Bearing;

/// <summary>
/// The external namespaces that identify an architectural role, and the rule for matching them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Extracted so it can be asserted.</b> These were three <c>params string[]</c> literals inline
/// in <c>ModelBuilder.Classify</c>, and nothing in the suite could see them. A later fix
/// added <c>LinqToDB</c> and <c>FluentMigrator</c> on the evidence of two real-solution runs —
/// 134 reclassifications on nopCommerce — and the fixture references neither, so the list could be
/// trimmed back to what it was with every test green. That is <c>TASKS.md</c> P10, and the entry
/// itself said making the list assertable was likely to beat planting a type that needs a package
/// reference for a classification rule.
/// </para>
/// <para>
/// <b>Matching is by namespace segment, not by string prefix, and that closes a second gap.</b>
/// The old rule was <c>ns.StartsWith(prefix)</c>, so <c>System.Data</c> would have matched a
/// hypothetical <c>System.Database</c> — the same mistake <c>StructureTests</c> pins one level up
/// for namespace <i>collection</i>, where <c>System.Net.Http</c> was once truncated to
/// <c>System.Net</c> and an HttpClient gateway stopped being a boundary. Measured before the
/// change: no namespace on nopCommerce, jellyfin or TestBed matched any prefix except at a segment
/// boundary, so this preserves behaviour on everything anyone has run.
/// </para>
/// <para>
/// <b>It also removes a convention nobody could have followed.</b> Two entries carried a trailing
/// dot — <c>Azure.</c> and <c>Amazon.</c> — because a bare prefix would over-match, and the other
/// fifteen did not, so whether a new entry needed one was a coin flip. Segment matching makes the
/// dot meaningless and it is gone from both.
/// </para>
/// </remarks>
public static class FrameworkNamespaces
{
    /// <summary>Receives calls from outside the solution.</summary>
    public static IReadOnlyList<string> ApiBoundary { get; } = ["Microsoft.AspNetCore"];

    /// <summary>Reaches a database or persistence framework.</summary>
    /// <remarks>
    /// <c>LinqToDB</c> and <c>FluentMigrator</c> were added on measurement. Removing either
    /// fails <c>FrameworkNamespacesTests</c>, which is the whole point of this type existing.
    /// </remarks>
    public static IReadOnlyList<string> DataAccess { get; } =
    [
        "Microsoft.EntityFrameworkCore",
        "System.Data",
        "Dapper",
        "NHibernate",
        "LinqToDB",
        "FluentMigrator",
    ];

    /// <summary>Calls out of the solution.</summary>
    public static IReadOnlyList<string> ExternalCall { get; } =
    [
        "System.Net.Http",
        "Azure",
        "Amazon",
        "RabbitMQ",
        "Confluent.Kafka",
        "MassTransit",
        "Stripe",
        "Twilio",
        "SendGrid",
        "Polly",
    ];

    /// <summary>
    /// The first of <paramref name="externalNamespaces"/> covered by any of
    /// <paramref name="prefixes"/>, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The namespace is returned rather than the prefix because it is the evidence the
    /// classification carries — <c>external-ns:FluentMigrator.Builders</c> tells a reader which of
    /// their own dependencies decided it, where the prefix would only repeat this list back.
    /// </remarks>
    public static string? Match(IEnumerable<string> externalNamespaces, IReadOnlyList<string> prefixes)
    {
        ArgumentNullException.ThrowIfNull(externalNamespaces);
        ArgumentNullException.ThrowIfNull(prefixes);

        return externalNamespaces.FirstOrDefault(ns => prefixes.Any(prefix => Covers(prefix, ns)));
    }

    /// <summary>Whether <paramref name="prefix"/> is <paramref name="ns"/> or a parent of it.</summary>
    public static bool Covers(string prefix, string ns)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(ns);

        return string.Equals(ns, prefix, StringComparison.Ordinal)
               || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
    }
}

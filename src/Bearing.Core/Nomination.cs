namespace IronMarten.Bearing;

/// <summary>
/// The ordering every detector ends with: strongest evidence first, made total by identity.
/// </summary>
/// <remarks>
/// <para>
/// Each detector decides what "strongest" means for its own claim — outlier factor for a
/// concealed decision, fan-in for a blast radius, instability for load-bearing — so the primary
/// keys stay at the call sites where they can be read against the specification. What is shared
/// is the tiebreak, and the reason it has to exist.
/// </para>
/// <para>
/// <b>Ranking alone reproduces on one machine without being a property of the tool.</b> Outlier
/// factors and fan-in counts tie constantly, and a stable sort over a tied group preserves
/// whatever order the walk happened to arrive in — which is file order, which is Roslyn's
/// project order. <c>docs/TESTING.md</c> §5.
/// </para>
/// <para>
/// <b>No <c>Take</c>.</b> <see cref="AnalysisPolicy.Top"/> is a display cap applied by the
/// renderer. A model that truncates leaves every renderer unable to say how much it is not
/// showing, and it silently weakens suppression: in the probe a type
/// nominated below the cap suppresses nothing, because the set membership is tested against was
/// truncated first.
/// </para>
/// </remarks>
internal static class Nomination
{
    /// <summary>
    /// Completes a detector's ordering with the identity tiebreak and drops the ranking keys.
    /// </summary>
    public static List<Finding> Ranked<T>(IOrderedEnumerable<T> ordered, Func<T, Finding> select)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(select);

        return ordered
            .ThenBy(x => select(x).Subject.Canonical, StringComparer.Ordinal)
            .Select(select)
            .ToList();
    }
}

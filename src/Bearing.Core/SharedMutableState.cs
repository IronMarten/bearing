namespace IronMarten.Bearing;

/// <summary>
/// <i>"Every caller on every thread shares these."</i> <c>TECHREQ-job-b.md</c> §3.9.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one case where sharing is certain from the code alone.</b> Every other coupling finding
/// in §3 reasons about what a dependency implies; this one reads a fact. A write to static mutable
/// state is shared by construction, and no amount of call-graph analysis is needed to establish
/// it.
/// </para>
/// <para>
/// <b>What is certain is the sharing, not the contention</b>, and the distinction is invariant 4
/// in miniature: whether two threads ever reach the write together is a runtime question, and a
/// tool that says "race condition" when it can only see "shared" has made the overclaim invariant
/// 4 exists to prevent. The finding carries the counts; the sentence has to carry the limit.
/// </para>
/// <para>
/// <b><c>++</c> counts, and missing it was a real defect</b> (<c>SESSION-NOTES.md</c> #20). Only
/// assignments were checked, and an increment is a non-atomic read-modify-write that shares state
/// exactly as much as an assignment does — rather more dangerously, since it also reads. That is
/// counted in the walk rather than here, so this detector inherits the fix.
/// </para>
/// <para>
/// No threshold, and none is invented. §5 names twenty-three policy values and none of them is
/// this: the gate is <c>&gt; 0</c>, which is not a tuning decision but the definition of the
/// finding. A policy value that could only ever be set to zero or to something arbitrary would
/// misrepresent the finding as calibrated.
/// </para>
/// </remarks>
public static class SharedMutableState
{
    /// <summary>Nominates types that write to static mutable state.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var found = new List<(int Mutations, Finding Finding)>();

        foreach (var type in model.Types)
        {
            if (type.StaticMutations <= 0) continue;

            found.Add((type.StaticMutations, new Finding(
                new FindingKey(FindingKind.SharedMutableState, type.Subject),
                [
                    // Ungated deliberately — see the class remarks. Receipt.Gated would have to
                    // name a policy value, and there is none to name.
                    Receipt.Of("StaticMutations", type.StaticMutations),
                    // The sentence says "and N types call into it", which is what turns a private
                    // static counter into shared state anyone can reach.
                    Receipt.Of("FanIn", type.FanIn),
                ],
                [],
                // Invariant 7: "3 writes to static state" sends the reader looking. The members
                // that write are known here and cost nothing to carry.
                [.. type.Members
                    .Where(member => member.StaticMutations > 0)
                    .OrderBy(member => member.Subject.Canonical, StringComparer.Ordinal)
                    .Select(member => member.Subject)])));
        }

        return Nomination.Ranked(found.OrderByDescending(f => f.Mutations), f => f.Finding);
    }
}

using TestBed.Core.Berths;

namespace TestBed.Core.Yards;

/// <summary>
/// P11, half one — a namespace cycle whose shape is <c>SharedTypes</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fixture had no cycle of this shape, so <c>cycle-is-shared-types</c> could not be observed
/// to withhold anything.</b> TestBed's namespace cycles were one <c>Coupling</c> (P10's
/// <c>Tariffs</c> ↔ <c>Weighing</c>) and one <c>FolderLayout</c> (<c>TestBed.Core</c> and three of
/// its own folders). §4's rule is that a suppression which cannot fail is worse than none, and
/// <c>SuppressionTests.Every_suppression_row_silences_something</c> is that rule as a test — it
/// failed on the new row, which is the test working rather than the row being wrong.
/// </para>
/// <para>
/// <b>Why this pair reads as <c>SharedTypes</c> and not as the other two.</b> The classification
/// asks three questions in order. Are any sibling pairs held both ways — a field typed as the
/// other's abstraction? <b>No</b>: each type names the other only in a method signature, so nothing
/// is <i>held</i> and the <c>Coupling</c> arm cannot fire. Is it one assembly with an anchor — a
/// member that is a prefix of every other? <b>No</b>: <c>Yards</c> and <c>Berths</c> are peers and
/// neither prefixes the other, so the <c>FolderLayout</c> arm cannot fire although both are in
/// <c>Core</c>. Is any pair mutual? <b>No</b>, for the same reason as the first. That is
/// <c>SharedTypes</c>, and it is reached by the arm rather than by a default.
/// </para>
/// <para>
/// <b>The plant constraint holds: no new fan-in on anything that already exists.</b> These two
/// types reference each other and nothing else, and nothing existing references them. The trailing
/// words were checked against the fixture's cohorts before they were chosen — <c>Docket</c> and
/// <c>Placard</c> are new, so no existing type is pulled into a new suffix cohort, which is the
/// mistake <c>*Handler</c> made once. They form cohorts of one and are therefore disclosed by
/// <c>NO PEER GROUP</c> like any other peerless type, which is the ordinary cost of a plant and not
/// a finding about them.
/// </para>
/// </remarks>
public sealed class YardDocket
{
    public int Slots { get; init; }

    /// <summary>
    /// Names the peer namespace's model and does not hold it. A parameter is read at the call and
    /// gone; a field would be state, and state both ways is what <c>Coupling</c> means.
    /// </summary>
    public int SlotsAfter(BerthPlacard placard) => Slots - placard.Priority;
}

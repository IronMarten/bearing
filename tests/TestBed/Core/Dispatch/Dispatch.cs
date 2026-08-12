// PLANT: two gates that fire today without being gated by anything.
//
// Both were found by mutation rather than by reading, and both have the same shape as the
// evaluator plant: the finding produces output, so it looks covered, but the specific condition
// under test is never the one deciding.
//
//   1. HUBS AND GOD OBJECTS splits on `MaxMemberCyclomatic >= highCc OR MemberCount >=
//      godObjectMembers`. Only ShipmentCoordinator reaches the bottleneck branch and it does so
//      on complexity (cc 13), so the member-count half of the disjunction has never decided
//      anything. Deleting `MemberCount >= 20` changed no output. DispatchRegistry reaches it on
//      size alone — 23 members, worst method cc 1 — which is the god object the constant is
//      named for: coupling and bulk with no logic to reason about.
//
//   2. SHARED MUTABLE STATE counts `++` as a static write, and missing it was a real defect
//      (SESSION-NOTES.md #20 — only assignments were checked). The fixture's only nomination,
//      QuoteAssembler, carries BOTH forms: `_totalAssembled++` on line 25 and `= 0` on line 33.
//      So dropping `++` support takes StaticMutations from 2 to 1, which is still > 0, and the
//      finding still fires. The case that was planted for the defect does not protect the fix.
//      DispatchCounter's only static write is `++`, so removing the support empties it.
//
// WHAT THIS PLANT MUST NOT DO — the constraints Bridges.cs records, and they still bind:
//
//   * No new ApiBoundary or ExternalCall types. PrintBoundaries counts them, and row 5's
//     suppression stops being reachable at ten. The fixture sits at nine. Every type here is
//     Internal, and each lane carries one trivial method for that reason: a type with no
//     executable member is classified Contract ("shape:no executable members"), which would
//     have quietly changed what the boundary and change-cost sections see.
//   * No new fan-in on anything that already exists. Every reference below points inside this
//     file, so no existing cohort's median moves. That is also why the registry reaches its
//     fan-out through five new lanes rather than through the evaluators planted alongside it —
//     one more inbound edge on SurchargeEvaluator takes its instability from 0.80 to 0.67 and
//     destroys defect 15's control.
//
// The arithmetic, and every number is load-bearing:
//
//   DispatchRegistry   fan-in 5 (the handlers), fan-out 5 (the lanes)  -> min 5 >= hubMin
//                      23 members, max member cc 1                     -> god object by SIZE
//   DispatchCounter    one static field, written only by ++            -> the #20 case, isolated
//   five *Dispatcher   fan-out 2 each, fan-in 0                       -> reach the two above
//   five *Lane         fan-in 1 each, fan-out 0                        -> give the registry its
//                                                                         fan-out without
//                                                                         touching anything else
//
// The registry is deliberately almost all auto-properties. MemberCount counts declared members
// while Cyclomatic sums executable ones, so bulk without logic is what separates the two halves
// of the disjunction — and a registry of 22 properties is what that looks like in real code.
// Giving it a complex method would satisfy the cc half instead and put the gate straight back
// to sleep.
//
// Cohorts: the dispatchers group on suffix:Dispatcher and the lanes on suffix:Lane, five each and
// both viable. The suffix is Dispatcher rather than Handler on purpose: a *Handler group would
// have pulled the existing SchemaMigrationHandler out of its cohort and shrunk another peer
// population from 33 to 32 — an addition that quietly reshapes. Caught in the golden diff, not
// by reasoning, which is the argument for reading that diff line by line.
//
// The registry and the counter have no suffix peers, so they fall to ns:TestBed.Core.Dispatch —
// a group of two, below --min-cohort, which puts them in NO PEER GROUP. That is deliberate and
// convenient: hubs and shared mutable state are both cohort-free, so neither plant needs peers,
// and having none keeps the registry out of blast radius despite carrying the highest fan-in
// here. Everything in the file is trivial, so nothing is a concealed decision, breaks alone or
// load-bearing.

namespace TestBed.Core.Dispatch;

/// <summary>A routing lane. Inert on purpose — it exists to be depended on.</summary>
public class AirLane { public int Code() => 1; }

/// <inheritdoc cref="AirLane"/>
public class RailLane { public int Code() => 2; }

/// <inheritdoc cref="AirLane"/>
public class RoadLane { public int Code() => 3; }

/// <inheritdoc cref="AirLane"/>
public class SeaLane { public int Code() => 4; }

/// <inheritdoc cref="AirLane"/>
public class BulkLane { public int Code() => 5; }

/// <summary>
/// <b>A god object by size rather than by complexity</b> — the case that makes
/// <c>godObjectMembers</c> the deciding condition instead of a constant nothing reads.
/// </summary>
/// <remarks>
/// Coupled both ways and full of state, with no method worth reasoning about. The standing note
/// on the finding says routers, mediators and composition roots legitimately live here and that
/// this does not make the flag wrong; this is that case, and the flag is right to name it.
/// </remarks>
public class DispatchRegistry
{
    public AirLane Air { get; set; } = new();
    public RailLane Rail { get; set; } = new();
    public RoadLane Road { get; set; } = new();
    public SeaLane Sea { get; set; } = new();
    public BulkLane Bulk { get; set; } = new();

    public int AirCapacity { get; set; }
    public int RailCapacity { get; set; }
    public int RoadCapacity { get; set; }
    public int SeaCapacity { get; set; }
    public int BulkCapacity { get; set; }
    public int AirBacklog { get; set; }
    public int RailBacklog { get; set; }
    public int RoadBacklog { get; set; }
    public int SeaBacklog { get; set; }
    public int BulkBacklog { get; set; }
    public string AirTerminal { get; set; } = "";
    public string RailTerminal { get; set; } = "";
    public string RoadTerminal { get; set; } = "";
    public string SeaTerminal { get; set; } = "";
    public string BulkTerminal { get; set; } = "";
    public bool AirEnabled { get; set; }
    public bool RailEnabled { get; set; }

    /// <summary>
    /// The one executable member, and it is here for a classification reason rather than a
    /// behavioural one: a type with no executable members is classified <c>Contract</c>, and a
    /// twenty-third Contract would change what the change-cost and boundary sections see.
    /// </summary>
    public int Registered() => 5;
}

/// <summary>
/// <b>Static state whose only write is <c>++</c></b> — the isolated form of
/// <c>SESSION-NOTES.md</c> #20.
/// </summary>
/// <remarks>
/// A non-atomic read-modify-write, and the reason the finding counts increment as mutation at
/// all. QuoteAssembler carries an increment too, but it also carries a plain assignment, so it
/// keeps firing whether or not increments are counted. This one does not.
/// </remarks>
public class DispatchCounter
{
    private static int _dispatched;

    public void Record() => _dispatched++;

    public int Seen() => _dispatched;
}

/// <summary>Reaches the registry and the counter. Inert otherwise.</summary>
public class AirDispatcher
{
    private readonly DispatchRegistry _registry = new();
    private readonly DispatchCounter _counter = new();
    public int Handle() => _registry.Registered() + _counter.Seen();
}

/// <inheritdoc cref="AirDispatcher"/>
public class RailDispatcher
{
    private readonly DispatchRegistry _registry = new();
    private readonly DispatchCounter _counter = new();
    public int Handle() => _registry.Registered() + _counter.Seen();
}

/// <inheritdoc cref="AirDispatcher"/>
public class RoadDispatcher
{
    private readonly DispatchRegistry _registry = new();
    private readonly DispatchCounter _counter = new();
    public int Handle() => _registry.Registered() + _counter.Seen();
}

/// <inheritdoc cref="AirDispatcher"/>
public class SeaDispatcher
{
    private readonly DispatchRegistry _registry = new();
    private readonly DispatchCounter _counter = new();
    public int Handle() => _registry.Registered() + _counter.Seen();
}

/// <inheritdoc cref="AirDispatcher"/>
public class BulkDispatcher
{
    private readonly DispatchRegistry _registry = new();
    private readonly DispatchCounter _counter = new();
    public int Handle() => _registry.Registered() + _counter.Seen();
}

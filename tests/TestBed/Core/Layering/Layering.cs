// PLANT (TASKS.md P6): layer span's roll-call collapse, and whether a type's own architectural
// role belongs in the pattern key. Both halves in one plant, because both are the same partition
// read twice — how many members a group has, and what puts a type in one group rather than another.
//
// WHY THERE WAS NOTHING TO OBSERVE. Closing DEFECTS.md §11 took the collapse's only case away.
// The probe groups spanning types on the kind signature alone, and with three significant kinds
// and a floor of three every spanning type carries the identical signature — so all six fixture
// types were one group of six against a threshold of five, the collapsed line was the ONLY branch
// the golden had ever shown, and the per-type detail branch had no case at all. Core groups on the
// named dependencies instead (§11's repair: if the names are the finding, the names are what makes
// two findings the same finding), which split the six into 4 + 1 + 1. The largest group fell to
// four against a threshold of five, and the branches traded places. R1 then rendered the collapse
// from the qualifier — FindingSections.cs, the `collapsed` half — so as of R1 it is written,
// unreachable on this fixture, and held by nothing.
//
// WHAT THE COLLAPSE NEEDS. SpansArchitecturalLayers gates on `groupSize > RollCallThreshold`, and
// RollCallThreshold is Top / RollCallDivisor = 15 / 3 = 5. Six members is the smallest group that
// fires it. The six Internal conduits below are that group.
//
// WHAT THE PATTERN KEY NEEDS. The key is the type's own role plus the components it reaches. No
// two spanning subjects on this fixture shared a dependency set while differing in their own role,
// so grouping on dependencies alone gave the identical partition and the role half of the key
// could be deleted with the suite green. The two ApiBoundary conduits below reach the SAME three
// components as the six, and differ from them in nothing else — so the role is the whole of what
// separates 6 + 2 from one group of 8. Drop it from the key and all eight collapse together,
// including the two that are not part of the pattern.
//
// The two halves interlock rather than merely coexisting: with the role in the key the group of
// six collapses and the pair keeps its detail; without it a group of eight collapses and the pair
// loses detail it is entitled to. That is DEFECTS.md §11's failure in miniature — the collapse
// absorbing something that is not an instance of the pattern — and it is why one plant is enough.
//
// WHY THIS PLANT BRINGS ITS OWN DEPENDENCIES. The one constraint binding every plant is NO NEW
// FAN-IN ON ANYTHING THAT ALREADY EXISTS. Eight types sharing a dependency set means eight new
// inbound edges on each member of that set, so the set has to be new. Three targets, one per
// significant kind, is the minimum: the six conduits are Internal, so their own role contributes
// nothing and they must reach ApiBoundary, DataAccess and ExternalCall through dependencies alone.
// A smaller set would force the two role groups onto different dependency sets, which is the one
// thing this plant has to hold constant.
//
// WHY THE BOUNDARY SURFACES ARE THE SIZES THEY ARE — read this before changing a signature.
// Three of the four new types that are boundaries (ApiBoundary or ExternalCall) push the boundary
// population from 15 to 19, and WIDEST CONTRACT SURFACE gates on median × SurfaceOutlierMultiple.
// The existing surfaces are 1,1,1,1,1,2,3,4,6,7,8,8,8,8,12 — median 4, threshold 6, seven
// qualifiers. Adding four boundaries all ABOVE the median pushes the median to 6 or 7 and empties
// the finding down to one; adding four all below drags it to 2 and admits two more. Both would
// disarm P4, which exists to make that suppression reachable from both sides. Surfaces of 4, 4, 5
// and 5 leave the median at 4 (the 10th of 19), the threshold at 6, and the qualifying set exactly
// the same seven types. DataShape counts declared PUBLIC members only — each `int` parameter and
// each return type is 1 — so `Handle(int, int, int)` is 4 and `Receive(int, int, int, int)` is 5.
// The private dependency fields cost nothing here, which is why they can be shared by all eight.
//
// WHY A NEW NAMESPACE AND A NEW NAME SUFFIX. Cohort assignment takes the most specific candidate
// with enough members, and precedence runs interface < base type < name suffix < architectural
// kind < namespace. `*Conduit` gives all eight a cohort of their own at precedence 2, so none of
// them lands in an existing peer group, and TestBed.Core.Layering keeps the three targets out of
// ns:TestBed.Core — which is a cohort of 28 and would have had its medians moved by eleven new
// members. This is the lesson from Bridges.cs recorded as a construction: naming plants `*Handler`
// once pulled SchemaMigrationHandler into the new suffix cohort and shrank an unrelated peer
// population from 33 to 32, and it was caught in the golden diff rather than by reasoning.
//
// AND WHY THE PAIR IS ApiBoundary RATHER THAN DataAccess. DataAccess would have added no boundary
// contact points at all, which is cheaper on the surface arithmetic above — but the fixture holds
// only two DataAccess types, so a pair plus the target would take the population to five, which is
// MinCohort, which forms a kind:DataAccess cohort that did not exist. TenantStore's only viable
// candidate today is ns:TestBed.Core at precedence 4; kind:DataAccess at precedence 3 would beat
// it, and TenantStore would silently move peer groups. Cohorts are the substrate every finding is
// measured against, so moving one is worse than moving a median that can be held still by
// construction. ApiBoundary is already 13 strong and every one of them has a more specific cohort,
// so nothing there moves.

namespace TestBed.Core.Layering;

/// <summary>
/// Marks a type as an API boundary by attribute. The classifier reads the attribute's name before
/// it looks at base types or the name suffix, which is what keeps the pair below out of both
/// base:ControllerBase (8 members, and inheriting would give ControllerBase two new inbound edges)
/// and suffix:Controller (5 members, which is P4's plant).
/// </summary>
public sealed class RouteAttribute : Attribute
{
}

// ---------------------------------------------------------- the dependency set ----
//
// One type per significant kind, reached by all eight conduits and by nothing else. These three
// are the participants the finding names, and the thing the pattern key is built from.

/// <summary>ApiBoundary, by attribute. Surface 5.</summary>
[Route]
public class LayeringEndpoint
{
    public int Receive(int a, int b, int c, int d) => a + b + c + d;
}

/// <summary>DataAccess, by external namespace — not a boundary, so its surface does not matter.</summary>
public class LayeringArchive
{
    private readonly System.Data.IDbConnection _connection = null;

    public int Read(int a, int b) => _connection == null ? a : a + b;
}

/// <summary>ExternalCall, by external namespace. Surface 5.</summary>
public class LayeringBeacon
{
    private readonly System.Net.Http.HttpClient _http = new();

    public int Send(int a, int b, int c, int d) => _http == null ? a : a + b + c + d;
}

// ------------------------------------------------------- the pattern, six strong ----
//
// Internal, so the whole of each one's span comes through its dependencies. Six against a
// threshold of five: this is the group that collapses, and removing any one of them stops it.
// They are deliberately identical — a layering pattern is boilerplate repeated, and six blocks of
// per-type detail about types that differ in nothing is what invariant 2 exists to prevent.

public class IntakeConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

public class RelayConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

public class MirrorConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

public class SyncConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

public class ReplayConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

public class EgressConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

// ------------------------------------------------------- the control, two strong ----
//
// The same three dependencies, and one difference: these two are boundaries themselves. Under the
// key as written they are a pattern of two and keep their detail. Drop the type's own role from
// the key and they join the six, and a component that receives calls from outside the solution is
// reported as one more instance of an internal relay pattern — which is the collapse absorbing
// something it should not, the same shape as DEFECTS.md §11 one level down.
//
// Surface 4 apiece, which is the median of the boundary population rather than an accident.

[Route]
public class PublicIntakeConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

[Route]
public class PublicRelayConduit
{
    private readonly LayeringEndpoint _endpoint = new();
    private readonly LayeringArchive _archive = new();
    private readonly LayeringBeacon _beacon = new();

    public int Handle(int a, int b, int c) => a + b + c;
}

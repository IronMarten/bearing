// PLANT: P8, part three — D1's retro-protection, declaration 1 of 2.
//
// The other declaration is tests/TestBed/Data/CarrierTwin.cs: same namespace, same name, same
// `partial` keyword, different assembly. It pairs with PayloadTag and does the job PayloadTag
// cannot.
//
// WHY PAYLOADTAG WAS NOT ENOUGH, WHICH IS THE WHOLE REASON THIS EXISTS. D1 is that merging two
// same-named declarations from different assemblies into one row fabricates dependencies — on
// nopCommerce it invented a five-project circular reference. PayloadTag proves the ROW is kept
// apart: two declarations, two rows, numbers that do not sum. What it cannot prove is the
// CONSEQUENCE, because PayloadTag has fan-in 0 in both declarations and no outbound edges either.
// A type nothing points at and which points at nothing is in no cycle whichever way it is keyed,
// so merged and split give identical components and the defect's actual damage is unobservable.
//
// Giving PayloadTag edges was rejected: it would disarm the unreferenced-type traps that name it
// in FixtureCoverageTests and in both fan-in-0 lists in the goldens. So this is a new pair, wired
// the way nopCommerce's was:
//
//   TwinRelay (Core)  ->  CarrierTwin @ Core          an inbound edge, inside Core
//   CarrierTwin @ Data  ->  TagArchive (Data)         an outbound edge, inside Data
//
// SPLIT — which is what Core does, keying identity on (assembly, FQN):
//   two rows. Core's has an inbound edge from Core; Data's has an outbound edge to Data. Neither
//   edge crosses a project boundary, so the project graph is unchanged and there is NO cycle.
//
// MERGED — which is what the probe does, keying on name alone:
//   one row, carrying both edges. Whichever project the merged row is attributed to, one of those
//   two edges now crosses between Core and Data — and Data already depends on Core, so the
//   aggregate closes. A project cycle appears that no code in this fixture contains.
//
// That is the nopCommerce shape at fixture scale, and it is asserted both ways: Core reports no
// project cycle, and the same edges under merged identity report one.
//
// WHY TestBed.Interop AND NOT TestBed.Shared, WHICH IS WHERE PayloadTag LIVES. The first draft put
// this pair beside PayloadTag, and that took TestBed.Shared from two types to six — over
// --min-cohort. PayloadTag stopped being peerless for the first time in the fixture's life, which
// disarmed the coverage assertions that name it and quietly deleted a plant while adding one. Its
// own namespace keeps both collisions peerless and keeps them independent.

namespace TestBed.Interop;

/// <summary>
/// Declaration 1 of 2. Deliberately small: the point is which edges it carries, not its size.
/// </summary>
public partial class CarrierTwin
{
    public string Scac { get; set; }

    public string Normalize() => Scac ?? "UNKNOWN";
}

/// <summary>
/// The inbound half. It exists only to point at the Core declaration, and it is new rather than
/// borrowed because the plant rule forbids adding fan-in to anything already in the fixture.
/// </summary>
public class TwinRelay
{
    private readonly CarrierTwin _twin = new();

    public string Route(string scac)
    {
        _twin.Scac = scac;
        return _twin.Normalize();
    }
}

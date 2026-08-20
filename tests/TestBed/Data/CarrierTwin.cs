// PLANT: P8, part three — D1's retro-protection, declaration 2 of 2.
//
// Pairs with tests/TestBed/Core/Shared/CarrierTwin.cs, which carries the reasoning. Same
// namespace, same name, different assembly.
//
// This is the OUTBOUND half. It references TagArchive, which lives in this assembly, so under
// split identity the edge stays inside Data and the project graph never sees it. Merge this row
// with the Core declaration and that edge leaves Data — which is the fabrication.
//
// Data references Core, so the compiler sees this name twice and prefers the local declaration.
// That is CS0436 and it is exactly the situation the defect is about; the fixture builds with
// warnings off, and PayloadTag avoided it only because Data and Tools do not reference each other.
// A pair that DOES span a reference edge is the case D1 needs and PayloadTag could not provide.

namespace TestBed.Interop;

/// <summary>
/// Declaration 2 of 2, and the one that carries the outbound edge.
/// </summary>
public partial class CarrierTwin
{
    private readonly TagArchive _archive = new();

    public int Retain(string scac, int days)
    {
        _archive.Record(scac);
        return days > 0 ? days : 1;
    }
}

/// <summary>
/// The outbound target, inside Data. New rather than borrowed, for the reason the other
/// declaration gives.
/// </summary>
public class TagArchive
{
    private string _last;

    public void Record(string scac) => _last = scac;

    public string Last => _last ?? "none";
}

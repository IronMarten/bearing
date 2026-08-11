using TestBed.Core.Depots;
using TestBed.Core.Vaults;

namespace TestBed.Core;

/// <summary>
/// Gives every Vault and every Depot exactly one inbound reference, which holds both cohorts'
/// fan-in medians at 1 — the condition <c>FanInXMedian &lt;= 2.0</c> that PricingVault and
/// RoutingDepot each need in order to satisfy the concealed-decision filter on every count
/// except the one under test.
/// </summary>
/// <remarks>
/// <para>
/// Fan-in 0 itself, in ns:TestBed.Core, exactly as LedgerCatalog and ReconciliationCatalog are.
/// A composition root that nothing composes is the ordinary shape for this fixture, and keeping
/// the three identical means none of them reads as the odd one out.
/// </para>
/// <para>
/// SIDE EFFECT, RECORDED DELIBERATELY. The Vaults and Depots depend on TestBed.Core for their
/// *Step dependencies, and this type depends back on both — so the namespace cycle the probe
/// already reported over TestBed.Core and TestBed.Core.Pricing now spans four namespaces rather
/// than two, and the golden line for it changed. That is a parent namespace's composition root
/// referencing its children while the children use shared types from the parent, which is
/// ordinary C# and the same shape Pricing was already in. It is noted because a fixture addition
/// altering what a shipping finding reports is exactly the kind of thing that should never be
/// discovered later from a diff.
/// </para>
/// </remarks>
public class StorageCatalog
{
    private readonly PricingVault _pricing = new();
    private readonly TokenVault _token = new();
    private readonly SessionVault _session = new();
    private readonly RoutingDepot _routing = new();
    private readonly LabelDepot _label = new();
    private readonly ManifestDepot _manifest = new();

    public int Total(int amount)
    {
        return _pricing.Price(amount, "gold", false)
             + _token.Read(amount)
             + _session.Read(amount)
             + _routing.Route(amount, "air", false, false)
             + _label.Label(amount)
             + _manifest.Manifest(amount);
    }
}

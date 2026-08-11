// PLANT: three types with a static fan-in of zero, each for a different legitimate reason.
//
// TECHREQ-job-a.md §5.6. Static fan-in of zero also describes DI-registered services never
// named, types resolved by reflection, and — because test projects are skipped by default —
// anything used only by tests. The acceptance criterion is that NONE of the three is reported
// as unreferenced without its category named.
//
// The trap is worth stating plainly: all three look identical to a pass that counts inbound
// edges, and that is precisely why counting inbound edges is not enough. Invariant 4 — a tool
// that says "safe to remove" about something six customers depend on has caused the burn it
// claimed to prevent.

namespace TestBed.Core;

/// <summary>
/// CONTRAST, not a trap. Reached only through
/// <c>ServiceRegistry.AddSingleton&lt;TenantPolicySink&gt;()</c> — but a generic type argument is
/// a compile-time reference, so its fan-in is 1 and no dead-code pass was ever going to miss it.
///
/// Kept deliberately: TECHREQ-job-a.md §5.6 gives <c>services.AddX&lt;T&gt;()</c> as the DI case
/// to handle, and it is the case that needs no handling. The one that does is
/// <see cref="AuditPolicySink"/> below.
/// </summary>
public class TenantPolicySink
{
    private readonly Dictionary<string, string> _policies = new();

    public void Apply(string tenantId, string policy)
    {
        if (string.IsNullOrEmpty(tenantId)) return;
        _policies[tenantId] = policy;
    }

    public string Lookup(string tenantId)
    {
        return _policies.TryGetValue(tenantId, out var found) ? found : "default";
    }
}

/// <summary>Marker for convention-based registration. Nothing names its implementations.</summary>
public interface IPolicySink
{
    void Apply(string tenantId, string policy);
}

/// <summary>
/// The real DI trap. Registered by <c>ServiceRegistry.AddAllImplementing</c>, which scans and
/// names no type at all — so unlike <see cref="TenantPolicySink"/> there is no generic argument
/// for the compiler to record, and fan-in is genuinely zero.
///
/// Implementing an interface produces an OUTBOUND edge, not an inbound one, so being
/// polymorphic does not rescue it either. TECHREQ-job-a.md §5.6 lists polymorphic-only
/// implementations as their own false-positive category for this reason.
/// </summary>
public class AuditPolicySink : IPolicySink
{
    public void Apply(string tenantId, string policy)
    {
        if (policy == "strict") return;
        if (policy == "audit") return;
    }
}

/// <summary>
/// Named only by the string literal in <c>Composition.ResolveMigrationHandler</c>. There is no
/// compile-time reference to this type anywhere in the solution.
/// </summary>
public class SchemaMigrationHandler
{
    public int Version { get; set; }

    public string Describe()
    {
        if (Version <= 0) return "unversioned";
        if (Version < 10) return "early";
        return "current";
    }
}

/// <summary>
/// Used only from the Core.Tests project, which is skipped by default — so its fan-in is zero
/// in every default run. Deleting it breaks a build that this analysis never looked at.
/// </summary>
public class FixtureBuilder
{
    public RawResponse WithStatus(int status)
    {
        return new RawResponse { StatusCode = status };
    }
}

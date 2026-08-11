// PLANT: the composition root. Supports the DI and reflection traps in DeadCodeTraps.cs.
//
// Deliberately hand-rolled rather than Microsoft.Extensions.DependencyInjection: the fixture
// projects carry no PackageReferences, and the detection requirement is about the SHAPE of the
// call — services.AddX<T>() — not about which container library produced it.

namespace TestBed.Core;

public class ServiceRegistry
{
    private readonly List<string> _registered = new();

    public ServiceRegistry AddSingleton<TService>() where TService : class
    {
        _registered.Add(typeof(TService).FullName);
        return this;
    }

    // Convention registration. No type argument, no type name — the registration that a
    // dead-code pass has nothing at all to find.
    public ServiceRegistry AddAllImplementing(string contractName)
    {
        _registered.Add(contractName);
        return this;
    }

    public int Count => _registered.Count;
}

public static class Composition
{
    // TenantPolicySink is named here and nowhere else. A generic type argument in a
    // registration call is the reference a dead-code pass has to see.
    public static ServiceRegistry Build()
    {
        var services = new ServiceRegistry();
        services.AddSingleton<TenantPolicySink>();
        services.AddAllImplementing("IPolicySink");
        return services;
    }

    // SchemaMigrationHandler is named ONLY by this string. There is no compile-time reference
    // to find, which is exactly why a naive implementation reports it as unreferenced.
    //
    // A typeof(T) would not be a trap at all — the compiler records it and fan-in is non-zero.
    // The string literal is the case that actually bites.
    public static object ResolveMigrationHandler()
    {
        var resolved = System.Type.GetType("TestBed.Core.SchemaMigrationHandler");
        return resolved == null ? null : System.Activator.CreateInstance(resolved);
    }
}

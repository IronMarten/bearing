using System.Net.Http;

namespace TestBed.Core;

// Named for one narrow concern. Actually a gateway policy engine: key validation,
// customer lookup, tenant routing and audit. Reaches across ApiBoundary, DataAccess
// and ExternalCall — which is visible structurally without knowing any of those words.
public class AuthenticationMiddleware : ControllerBase
{
    private readonly TenantStore _tenants = new();
    private readonly AuditClient _audit = new();

    public bool Authenticate(RawResponse raw, NormalizationContext ctx, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;

        var tenant = _tenants.LookupByApiKey(apiKey);
        if (tenant == null) return false;

        ctx.TenantId = tenant;
        ctx.StrictMode = _tenants.IsStrict(tenant);
        _audit.Record(tenant, raw?.StatusCode ?? 0);
        return true;
    }
}

// A plain, honest data-access component for contrast: one concern, one kind.
public class TenantStore
{
    private readonly System.Data.IDbConnection _connection;
    public TenantStore() { _connection = null; }

    public string LookupByApiKey(string apiKey)
    {
        if (_connection == null) return apiKey == "bad" ? null : "tenant-" + apiKey;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT TenantId FROM Keys WHERE ApiKey = @k";
        return (string)cmd.ExecuteScalar();
    }

    public bool IsStrict(string tenant) => tenant.EndsWith("-strict");
}

public class AuditClient
{
    private readonly HttpClient _http = new();
    public void Record(string tenant, int status) { _ = _http.BaseAddress; }
}

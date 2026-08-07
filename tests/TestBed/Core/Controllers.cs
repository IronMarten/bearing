namespace TestBed.Core;

// A pile of near-identical controllers: enumerating these was the false-positive report.
// Only ShipmentController should stand out — it carries real logic at the edge.

public class ControllerBase { }

public class QuoteController : ControllerBase
{
    private readonly Router _router = new();
    private readonly TenantStore _tenants = new();
    public NormalizedResponse Get(RawResponse raw) =>
        _router.Route(raw, new NormalizationContext { TenantId = _tenants.LookupByApiKey("k") });
}

public class RateController : ControllerBase
{
    private readonly Router _router = new();
    private readonly TenantStore _tenants = new();
    public NormalizedResponse Get(RawResponse raw) =>
        _router.Route(raw, new NormalizationContext { TenantId = _tenants.LookupByApiKey("k") });
}

public class TrackingController : ControllerBase
{
    private readonly Router _router = new();
    private readonly TenantStore _tenants = new();
    public NormalizedResponse Get(RawResponse raw) =>
        _router.Route(raw, new NormalizationContext { TenantId = _tenants.LookupByApiKey("k") });
}

public class DocumentController : ControllerBase
{
    private readonly Router _router = new();
    private readonly TenantStore _tenants = new();
    public NormalizedResponse Get(RawResponse raw) =>
        _router.Route(raw, new NormalizationContext { TenantId = _tenants.LookupByApiKey("k") });
}

public class ShipmentController : ControllerBase
{
    private readonly ShipmentCoordinator _coordinator = new();

    public NormalizedResponse Post(RawResponse raw, NormalizationContext ctx, string carrier, int mode)
    {
        if (raw == null) return null;
        if (ctx == null) ctx = new NormalizationContext();
        if (string.IsNullOrEmpty(carrier)) carrier = "UNKNOWN";

        switch (mode)
        {
            case 0: ctx.StrictMode = false; break;
            case 1: ctx.StrictMode = true; break;
            case 2: ctx.StrictMode = raw.StatusCode == 200; break;
            default: return null;
        }

        var result = _coordinator.Coordinate(raw, ctx, carrier);
        if (result == null) return null;
        if (result.Rate <= 0 && ctx.StrictMode) return null;
        if (result.IsGuaranteed && mode == 2) result.ServiceLevel = "NEXT_DAY";
        return result;
    }
}

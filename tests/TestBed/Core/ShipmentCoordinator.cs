namespace TestBed.Core;

// The god object: high fan-in AND high fan-out, shared mutable state, cross-domain
// orchestration. Instability lands near 0.5 and cannot see it.
public class ShipmentCoordinator
{
    private readonly TariffCalculator _tariff = new();
    private readonly Router _router = new();
    private readonly CarrierGateway _gateway = new();

    private NormalizationContext _context;
    private RawResponse _lastRaw;
    private NormalizedResponse _lastResult;
    private ServiceLevelPolicy _policy;
    private int _attempts;
    private string _lastCarrier;

    public NormalizedResponse Coordinate(RawResponse raw, NormalizationContext ctx, string carrier)
    {
        _context = ctx;
        _lastRaw = raw;
        _lastCarrier = carrier;
        _attempts++;

        if (raw == null) return null;
        if (ctx == null) ctx = new NormalizationContext();

        _policy = new ServiceLevelPolicy { Tier = ctx.StrictMode ? "STRICT" : "OPEN" };

        var result = _router.Route(raw, ctx);
        if (result == null) return null;

        if (result.Rate <= 0) result.Rate = _tariff.Apply(10m, "C", 25, false, false);
        else if (_attempts > 3) result.Rate = _tariff.Apply(result.Rate, "D", 25, true, false);

        if (string.IsNullOrEmpty(result.CarrierCode)) result.CarrierCode = carrier ?? "UNKNOWN";
        if (result.IsGuaranteed && _policy.Tier == "STRICT") result.ServiceLevel = "NEXT_DAY";
        if (!result.IsGuaranteed && ctx.StrictMode) result.ServiceLevel = "STANDARD";

        _lastResult = result;
        return result;
    }

    public static bool IsTerminal(NormalizedResponse r) => r != null && r.IsGuaranteed;

    public void Reset() { _attempts = 0; _lastRaw = null; _lastResult = null; _policy = null; }
    public int Attempts => _attempts;
    public string LastCarrier => _lastCarrier;
    public NormalizedResponse LastResult => _lastResult;
    public NormalizationContext Context => _context;

    // P7's near miss on --god-object-members, and the three members exist for no other reason.
    // This type is already a hub on fan-in and fan-out; the size arm of HUB OR GOD OBJECT needs 20
    // members and it now has 19. At the default that changes nothing, which is the point: at
    // --god-object-members 19 the TooLargeToHold qualifier starts holding, and the sweep can see a
    // constant that reported `-` in both directions before.
    //
    // They are AUTO-properties on purpose, and the first draft was not. Three expression-bodied
    // properties carry a cyclomatic point each, which took this type's total from 20 to 23 and
    // overtook TariffCalculator at 22 — so the two swapped places in the cohort's complexity
    // distribution and four percentiles moved in the golden for a plant that is supposed to be
    // about member COUNT. Auto-properties are members with no decision point, so the count moves
    // and nothing else does. They are also not method-like, so they never enter a method-level
    // cohort. docs/TESTING.md §6.
    public bool Ready { get; set; }
    public bool Draining { get; set; }
    public bool Quiesced { get; set; }
}

public class ServiceLevelPolicy
{
    public string Tier { get; set; }
}

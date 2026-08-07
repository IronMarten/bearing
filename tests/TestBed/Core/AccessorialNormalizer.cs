namespace TestBed.Core;

public class AccessorialNormalizer : IResponseNormalizer
{
    private readonly TariffCalculator _tariff = new();
    private readonly AuditClient _audit = new();

    public NormalizedResponse Normalize(RawResponse raw, NormalizationContext ctx)
    {
        var result = new NormalizedResponse
        {
            CarrierCode = raw.Headers.TryGetValue("carrier", out var c) ? c : "UNKNOWN",
            Rate = _tariff.Apply(raw.StatusCode == 200 ? 10.0m : 0m, "B", 10, false, false)
        };
        if (ShipmentCoordinator.IsTerminal(result)) return result;
        _audit.Record(ctx?.TenantId ?? "none", raw.StatusCode);
        return result;
    }
}

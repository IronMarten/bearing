namespace TestBed.Core;

// The stable kernel: everything depends on it, it depends on nothing, and the code
// inside is intricate. Instability near 0 + high cc = the dangerous quadrant.
public class TariffCalculator
{
    private readonly TenantStore _tenants = new();

    public decimal Apply(decimal baseRate, string zone, int weight, bool residential, bool hazmat)
    {
        var rate = baseRate;

        if (weight <= 0) return 0m;
        if (weight > 150) rate *= 1.75m;
        else if (weight > 70) rate *= 1.4m;
        else if (weight > 20) rate *= 1.15m;

        switch (zone)
        {
            case "A": break;
            case "B": rate *= 1.1m; break;
            case "C": rate *= 1.25m; break;
            case "D": rate *= 1.5m; break;
            default: rate *= 2.0m; break;
        }

        if (residential) rate += 4.50m;
        if (hazmat && weight > 50) rate += 95m;
        else if (hazmat) rate += 45m;
        if (residential && hazmat) rate += 15m;

        if (_tenants.IsStrict(zone)) rate *= 1.05m;
        if (rate > 500m && weight > 100) rate *= 0.95m;
        if (rate > 1000m) rate = 1000m;
        else if (rate > 750m && residential) rate *= 0.98m;

        return rate < 0 ? 0m : rate;
    }
}

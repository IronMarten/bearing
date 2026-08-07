using TestBed.Core;

namespace TestBed.Core.Pricing;

// Half of a deliberate namespace cycle: Pricing depends on Core...
public class SurchargeTable
{
    public decimal Lookup(NormalizationContext ctx, string code)
    {
        if (ctx == null || code == null) return 0m;
        return ctx.StrictMode ? 12.5m : 7.5m;
    }
}

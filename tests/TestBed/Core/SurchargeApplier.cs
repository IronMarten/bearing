using TestBed.Core.Pricing;

namespace TestBed.Core;

// ...and Core depends back on Pricing. Neither namespace can be extracted alone.
public class SurchargeApplier
{
    private readonly SurchargeTable _table = new();

    public decimal Apply(NormalizationContext ctx, decimal baseRate, string code)
    {
        var surcharge = _table.Lookup(ctx, code);
        return baseRate + surcharge;
    }
}

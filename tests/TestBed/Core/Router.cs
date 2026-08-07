namespace TestBed.Core;

public class Router
{
    private readonly List<IResponseNormalizer> _normalizers = new()
    {
        new RateNormalizer(), new TransitNormalizer(), new AddressNormalizer(),
        new AccessorialNormalizer(), new ReferenceNormalizer(), new TrackingNormalizer(),
        new GuaranteedServiceNormalizer()
    };

    public NormalizedResponse Route(RawResponse raw, NormalizationContext ctx)
    {
        NormalizedResponse last = null;
        foreach (var n in _normalizers) last = n.Normalize(raw, ctx);
        return last;
    }
}

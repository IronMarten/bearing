namespace TestBed.Core;

public interface IResponseNormalizer
{
    NormalizedResponse Normalize(RawResponse raw, NormalizationContext ctx);
}

// Contract-shaped: properties only, no behaviour. Should classify as Contract.
public class NormalizedResponse
{
    public string CarrierCode { get; set; }
    public decimal Rate { get; set; }
    public string ServiceLevel { get; set; }
    public DateTime? GuaranteedBy { get; set; }
    public bool IsGuaranteed { get; set; }
}

public class RawResponse
{
    public string Payload { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
}

public class NormalizationContext
{
    public string TenantId { get; set; }
    public bool StrictMode { get; set; }
}

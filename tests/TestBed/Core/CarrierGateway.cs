using System.Net.Http;

namespace TestBed.Core;

// External boundary: should classify as ExternalCall.
public class CarrierGateway
{
    private readonly HttpClient _http = new();

    public async Task<RawResponse> FetchAsync(NormalizationContext ctx, string url)
    {
        var response = await _http.GetAsync(url);
        return new RawResponse
        {
            Payload = await response.Content.ReadAsStringAsync(),
            StatusCode = (int)response.StatusCode,
            Headers = new Dictionary<string, string>()
        };
    }
}

namespace TestBed.Core;

// The insidious one: same shape as its 7 peers, but it DECIDES.
public class GuaranteedServiceNormalizer : IResponseNormalizer
{
    public NormalizedResponse Normalize(RawResponse raw, NormalizationContext ctx)
    {
        var result = new NormalizedResponse();
        var options = ParseOptions(raw.Payload);

        if (options.Count == 0)
        {
            result.IsGuaranteed = false;
            result.ServiceLevel = "STANDARD";
            return result;
        }

        foreach (var opt in options)
        {
            if (opt.Deadline == null) continue;
            if (ctx.StrictMode && opt.Rate <= 0) continue;

            switch (opt.Code)
            {
                case "NDA":
                    if (result.GuaranteedBy == null || opt.Deadline < result.GuaranteedBy)
                    {
                        result.GuaranteedBy = opt.Deadline;
                        result.ServiceLevel = "NEXT_DAY";
                        result.Rate = opt.Rate;
                    }
                    break;
                case "PM5":
                    if (result.ServiceLevel != "NEXT_DAY" && opt.Rate < result.Rate)
                    {
                        result.GuaranteedBy = opt.Deadline;
                        result.ServiceLevel = "BY_5PM";
                        result.Rate = opt.Rate;
                    }
                    break;
                case "NOON":
                    if (result.ServiceLevel is null or "STANDARD" || opt.Rate < result.Rate * 0.9m)
                    {
                        result.GuaranteedBy = opt.Deadline;
                        result.ServiceLevel = "BY_NOON";
                        result.Rate = opt.Rate;
                    }
                    break;
                default:
                    if (!ctx.StrictMode && opt.Rate > 0 && result.ServiceLevel == null)
                    {
                        result.ServiceLevel = "STANDARD";
                        result.Rate = opt.Rate;
                    }
                    break;
            }
        }

        result.IsGuaranteed = result.GuaranteedBy != null && result.ServiceLevel != "STANDARD";
        result.CarrierCode = raw.Headers.TryGetValue("carrier", out var c) ? c : "UNKNOWN";
        return result;
    }

    private List<ServiceOption> ParseOptions(string payload)
    {
        var list = new List<ServiceOption>();
        if (string.IsNullOrWhiteSpace(payload)) return list;
        foreach (var line in payload.Split(';'))
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!decimal.TryParse(parts[1], out var rate)) continue;
            DateTime? deadline = DateTime.TryParse(parts[2], out var d) ? d : null;
            list.Add(new ServiceOption { Code = parts[0], Rate = rate, Deadline = deadline });
        }
        return list;
    }

    private class ServiceOption
    {
        public string Code { get; set; }
        public decimal Rate { get; set; }
        public DateTime? Deadline { get; set; }
    }
}

using TestBed.Core;

namespace TestBed.Tools;

// Deliberately the only type in its namespace: exercises the no-peer-group path.
public class PayloadAuditor
{
    public bool IsSuspicious(RawResponse raw)
    {
        if (raw == null) return true;
        if (raw.StatusCode >= 500) return true;
        if (string.IsNullOrEmpty(raw.Payload)) return true;
        return raw.Payload.Length > 100000;
    }
}

// PLANT: the BUG BLAST RADIUS case. Shape copied from Jellyfin's BaseItem — high fan-in, high
// internal complexity, top of its cohort.
//
// WHY TWELVE TYPES. The finding requires FanInPctl >= 95, and Percentile is midrank:
// 100 * (below + 0.5 * equal) / n. A unique maximum therefore scores (n - 0.5)/n * 100, which
// is 94.44 at n = 9 and 95.0 at n = 10. Blast radius CANNOT fire in a cohort smaller than ten,
// whatever the metrics are — while --min-cohort admits cohorts of five. That is the real reason
// it never fired on this fixture, and it is pinned in KnownDefectTests.
//
// So the cohort is twelve, which puts ShipmentLedger at 95.83 rather than exactly on the
// boundary. A fixture that sits on a threshold breaks for reasons that are not about behaviour.
//
// The four conditions, all required (TECHREQ-job-b.md §3.4):
//
//   CohortSize >= 5        12
//   FanIn >= --min-fan-in  11, from the eleven siblings
//   FanInXMedian >= 2.0    11, against a cohort median of 1
//   FanInPctl >= 95        95.83
//   CyclomaticPctl >= 70   95.83, from Record below
//
// LedgerCatalog gives each sibling a fan-in of 1, which is what holds the cohort median at 1.
// Without it every sibling would sit at zero, the median would be zero, and FanInXMedian would
// be rendered as `inf` — a statistic where none exists, which is its own defect and not the one
// this plant is for.
//
// Deliberately acyclic: catalog -> siblings -> ShipmentLedger. A ring would give the siblings
// their fan-in more cheaply and would also fabricate a second type tangle, changing an unrelated
// known answer.

namespace TestBed.Core;

/// <summary>
/// The blast-radius target. Eleven callers and a branchy core: a bug in Record reaches all of
/// them.
/// </summary>
public class ShipmentLedger
{
    private readonly Dictionary<string, decimal> _posted = new();

    public decimal Record(string channel, decimal amount)
    {
        if (string.IsNullOrEmpty(channel)) return 0m;
        if (amount < 0m) return 0m;

        var adjusted = amount;

        if (channel == "rate") adjusted += 1m;
        else if (channel == "transit") adjusted += 2m;
        else if (channel == "tariff") adjusted += 3m;
        else if (channel == "audit") adjusted += 4m;
        else if (channel == "reference") adjusted += 5m;
        else if (channel == "accessorial") adjusted += 6m;
        else if (channel == "surcharge") adjusted += 7m;
        else if (channel == "document") adjusted += 8m;
        else if (channel == "tracking") adjusted += 9m;
        else if (channel == "quote") adjusted += 10m;
        else if (channel == "carrier") adjusted += 11m;
        else adjusted += 12m;

        if (adjusted > 1000m) adjusted = 1000m;
        if (_posted.ContainsKey(channel)) adjusted += _posted[channel];

        _posted[channel] = adjusted;
        return adjusted;
    }

    public decimal Balance(string channel)
    {
        return _posted.TryGetValue(channel, out var found) ? found : 0m;
    }
}

// The eleven siblings. Each references ShipmentLedger and nothing else, so the cohort's fan-in
// distribution is one tall member and eleven at 1.

public class RateLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("rate", amount);
}

public class TransitLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("transit", amount);
}

public class TariffLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("tariff", amount);
}

public class AuditLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("audit", amount);
}

public class ReferenceLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("reference", amount);
}

public class AccessorialLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("accessorial", amount);
}

public class SurchargeLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("surcharge", amount);
}

public class DocumentLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("document", amount);
}

public class TrackingLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("tracking", amount);
}

public class QuoteLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("quote", amount);
}

public class CarrierLedger
{
    private readonly ShipmentLedger _ledger = new();
    public decimal Post(decimal amount) => _ledger.Record("carrier", amount);
}

/// <summary>
/// Holds the cohort median at 1 by giving every sibling exactly one inbound reference. Named
/// Catalog rather than Ledger so it does not join the cohort it exists to shape.
/// </summary>
public class LedgerCatalog
{
    private readonly RateLedger _rate = new();
    private readonly TransitLedger _transit = new();
    private readonly TariffLedger _tariff = new();
    private readonly AuditLedger _audit = new();
    private readonly ReferenceLedger _reference = new();
    private readonly AccessorialLedger _accessorial = new();
    private readonly SurchargeLedger _surcharge = new();
    private readonly DocumentLedger _document = new();
    private readonly TrackingLedger _tracking = new();
    private readonly QuoteLedger _quote = new();
    private readonly CarrierLedger _carrier = new();

    public decimal PostAll(decimal amount)
    {
        return _rate.Post(amount)
             + _transit.Post(amount)
             + _tariff.Post(amount)
             + _audit.Post(amount)
             + _reference.Post(amount)
             + _accessorial.Post(amount)
             + _surcharge.Post(amount)
             + _document.Post(amount)
             + _tracking.Post(amount)
             + _quote.Post(amount)
             + _carrier.Post(amount);
    }
}

namespace TestBed.Core;

public record QuoteLine(string Code, decimal Amount, string Currency);

// Exercises the three DSM categories separately:
//   - construction        (should count as nothing)
//   - `with` expressions   (Transform, not Dsm)
//   - destructive mutation (Dsm), including static state
public class QuoteAssembler
{
    private static int _totalAssembled;          // static mutable state
    private readonly List<QuoteLine> _lines = new();

    public QuoteLine Build(decimal amount)
    {
        // Construction: excluded entirely.
        var line = new QuoteLine("BASE", amount, "USD");

        // Transform: new objects, no mutation, thread-safe.
        var discounted = line with { Amount = amount * 0.9m };
        var converted = discounted with { Currency = "EUR", Amount = discounted.Amount * 0.92m };

        // Destructive: collection mutation and a static write.
        _lines.Add(converted);
        _totalAssembled++;

        return converted;
    }

    public void Reset()
    {
        _lines.Clear();
        _totalAssembled = 0;                      // another static write
    }
}

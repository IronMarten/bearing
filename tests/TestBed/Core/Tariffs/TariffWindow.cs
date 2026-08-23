namespace TestBed.Core.Tariffs;

using TestBed.Core.Weighing;

/// <summary>
/// The other half of plant P10. See <see cref="TestBed.Core.Weighing.IScaleHead"/> for what the
/// pair is for and why it classifies as <c>CycleShape.Coupling</c>.
/// </summary>
public interface ITariffWindow
{
    /// <summary>What the current window charges for a settled weight.</summary>
    decimal RateFor(int kilograms);
}

/// <summary>
/// Prices a settled weight, and reads the head back to decide whether the window still applies.
/// </summary>
/// <remarks>
/// The back-reference is the point: a window that only priced a number would be a one-way
/// dependency, and one-way is not what the section is looking for. Neither of these two can be
/// extracted, understood or tested without the other, which is the sentence the finding makes.
/// </remarks>
public sealed class TariffWindow : ITariffWindow
{
    // The held reference the other way. Tariffs -> Weighing, and together with ScaleHead's field
    // this is the mutual sibling pair that makes the cycle Coupling rather than SharedTypes.
    private readonly IScaleHead _head;

    private readonly decimal _perKilogram;

    /// <summary>Creates the window over the head it reads back.</summary>
    public TariffWindow(IScaleHead head, decimal perKilogram)
    {
        _head = head;
        _perKilogram = perKilogram;
    }

    /// <inheritdoc/>
    public decimal RateFor(int kilograms)
    {
        // Reading the head back is what closes the loop at run time as well as in the graph.
        var settled = _head.SettledKilograms;
        var billable = kilograms > settled ? kilograms : settled;

        return billable * _perKilogram;
    }
}

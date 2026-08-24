using TestBed.Core.Yards;

namespace TestBed.Core.Berths;

/// <summary>
/// P11, half two. <see cref="YardDocket"/> carries the reasoning for the pair.
/// </summary>
public sealed class BerthPlacard
{
    public int Priority { get; init; }

    /// <summary>
    /// The return edge that closes the namespace cycle — again a signature, not a field.
    /// </summary>
    public int PriorityFor(YardDocket docket) => Priority + docket.Slots;
}

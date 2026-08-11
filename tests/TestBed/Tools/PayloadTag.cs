// PLANT: cross-project fully-qualified name collision, declaration 2 of 2.
//
// Pairs with tests/TestBed/Data/PayloadTag.cs. Same namespace, same name, different assembly.
//
// This one is deliberately branchy and carries more members, so that a tool merging the two
// declarations produces a row whose numbers match neither declaration — visible on inspection
// rather than a subtle shift. Until type identity is keyed on (assembly, FQN), the goldens
// record the merged row, which is what makes the defect observable at all.

namespace TestBed.Shared;

public partial class PayloadTag
{
    private readonly Dictionary<string, int> _weights = new();

    public int Priority { get; set; }

    public int Score(string channel, int volume, bool expedited, bool international)
    {
        var score = 0;

        if (channel == "air") score += 40;
        else if (channel == "ocean") score += 10;
        else if (channel == "ground") score += 20;
        else score += 5;

        if (volume > 1000) score += 25;
        else if (volume > 100) score += 15;
        else if (volume > 10) score += 5;

        if (expedited) score += 30;
        if (international) score += 20;
        if (expedited && international) score += 10;

        if (_weights.TryGetValue(channel, out var weight)) score += weight;

        return score > 100 ? 100 : score;
    }

    public void Weight(string channel, int weight)
    {
        _weights[channel] = weight;
    }
}

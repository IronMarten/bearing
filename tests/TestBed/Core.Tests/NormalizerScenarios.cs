// PLANT: the only consumer of TestBed.Core.FixtureBuilder.
//
// This project is skipped by default, so from the analysis's point of view FixtureBuilder has
// no inbound references at all. It is not dead — deleting it breaks this build.

namespace TestBed.Core.Tests;

public class NormalizerScenarios
{
    public string RunSuccessCase()
    {
        var builder = new FixtureBuilder();
        var raw = builder.WithStatus(200);
        return raw.StatusCode == 200 ? "ok" : "unexpected";
    }
}

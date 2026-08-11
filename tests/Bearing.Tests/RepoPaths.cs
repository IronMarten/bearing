namespace Bearing.Tests;

/// <summary>
/// Locates repository files from the test binaries.
/// </summary>
/// <remarks>
/// Tests run out of <c>bin/Debug/net8.0</c>, so anything on disk has to be found by walking
/// up. Doing that in one place matters more than it looks: the alternative is each test
/// deciding for itself what "the repo" means, and the golden baseline was already broken
/// once by a path that was correct on one machine and meaningless everywhere else.
/// </remarks>
internal static class RepoPaths
{
    /// <summary>The repository root — the directory containing <c>Bearing.sln</c>.</summary>
    internal static string Root { get; } = FindRoot();

    /// <summary>The fixture solution the whole suite asserts against.</summary>
    internal static string TestBedSolution { get; } =
        Path.Combine(Root, "tests", "TestBed", "TestBed.sln");

    /// <summary>Compiled output of the test project, where sibling assemblies land.</summary>
    internal static string BinDirectory => AppContext.BaseDirectory;

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;

        while (dir is not null && !File.Exists(Path.Combine(dir, "Bearing.sln")))
            dir = Path.GetDirectoryName(dir);

        return dir ?? throw new InvalidOperationException(
            $"Could not find Bearing.sln above {AppContext.BaseDirectory}.");
    }
}

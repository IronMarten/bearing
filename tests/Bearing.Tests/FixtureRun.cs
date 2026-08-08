using System.Runtime.CompilerServices;
using ArchProbe;
using Microsoft.Build.Locator;

namespace Bearing.Tests;

/// <summary>
/// MSBuildLocator has to register before any MSBuild or Roslyn-workspace type is loaded.
/// A module initializer is the earliest hook available inside a test host, and it must not
/// touch <see cref="SolutionAnalyzer"/> or anything that transitively loads MSBuild.
/// </summary>
internal static class MSBuildBootstrap
{
    [ModuleInitializer]
    internal static void Register()
    {
        if (MSBuildLocator.IsRegistered) return;

        var instance = MSBuildLocator.QueryVisualStudioInstances()
            .OrderByDescending(i => i.Version)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No MSBuild instance found. The .NET SDK must be discoverable.");

        MSBuildLocator.RegisterInstance(instance);
    }
}

/// <summary>
/// Runs the analyzer against TestBed exactly once for the whole suite. The workspace load
/// is the cost centre — seconds, not milliseconds — so every assertion shares one result.
/// </summary>
public sealed class FixtureRun
{
    internal AnalysisResult Result { get; }
    internal IReadOnlyDictionary<string, TypeMetrics> ByName { get; }

    public FixtureRun()
    {
        var options = new Options { SolutionPath = SolutionPath() };

        Result = new SolutionAnalyzer(options)
            .RunAsync(CancellationToken.None)
            .GetAwaiter().GetResult();

        // Cohort sizes and percentiles are computed here, not by the analyzer. Note where
        // this lives: ComputeCohortStats is computation sitting inside the report renderer,
        // which is exactly the entanglement Phase 1 has to undo. Until it moves, the fixture
        // has to mirror the real pipeline or every cohort assertion reads zero.
        Report.ComputeCohortStats(Result);

        // Simple names are unique within TestBed, which keeps assertions readable.
        ByName = Result.Types.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    internal TypeMetrics Type(string name) =>
        ByName.TryGetValue(name, out var t)
            ? t
            : throw new InvalidOperationException(
                $"TestBed has no type '{name}'. Present: {string.Join(", ", ByName.Keys.Order())}");

    static string SolutionPath()
    {
        // Walk up from the test binaries to the repo root.
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "tests", "TestBed")))
            dir = Path.GetDirectoryName(dir);

        if (dir is null)
            throw new InvalidOperationException("Could not locate tests/TestBed from " + AppContext.BaseDirectory);

        return Path.Combine(dir, "tests", "TestBed", "TestBed.sln");
    }
}

[CollectionDefinition(Name)]
public sealed class FixtureCollection : ICollectionFixture<FixtureRun>
{
    public const string Name = "TestBed fixture";
}

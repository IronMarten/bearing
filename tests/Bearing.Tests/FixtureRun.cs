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

    /// <summary>
    /// The options the fixture was analysed with — defaults throughout.
    /// </summary>
    /// <remarks>
    /// Exposed because the golden baselines were recorded with defaults, and the renderers
    /// take <see cref="Options"/> as an argument. A test that reproduced them by hand would
    /// be asserting against its own copy of the thresholds rather than the tool's.
    /// </remarks>
    internal Options Options { get; }

    public FixtureRun()
    {
        var options = Options = new Options { SolutionPath = RepoPaths.TestBedSolution };

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
}

/// <summary>
/// Both analyses of TestBed — the probe's and Core's — share one collection.
/// </summary>
/// <remarks>
/// One collection rather than two because each analysis opens an <c>MSBuildWorkspace</c>, and
/// two collections would run them in parallel against the same solution. That doubles the
/// slowest part of the suite and makes the workspace emit load diagnostics that the
/// single-analysis case does not.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class FixtureCollection : ICollectionFixture<FixtureRun>, ICollectionFixture<CoreWalkFixture>
{
    public const string Name = "TestBed fixture";
}

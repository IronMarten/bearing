using System.Runtime.CompilerServices;
using IronMarten.Bearing;
using Microsoft.Build.Locator;

namespace Bearing.Tests;

/// <summary>
/// MSBuildLocator has to register before any MSBuild or Roslyn-workspace type is loaded.
/// A module initializer is the earliest hook available inside a test host, and it must not
/// touch <see cref="SolutionWalker"/> or anything that transitively loads MSBuild.
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
/// Walks TestBed once for the whole suite.
/// </summary>
/// <remarks>
/// <b>The workspace load is this suite's entire cost centre</b> — seconds, not milliseconds — so
/// every assertion that can share one model does. Until R2 there were two of these running side
/// by side, Core's and the probe's, in one xunit collection rather than two so that they could not
/// open a workspace over the same solution in parallel. One is left.
/// </remarks>
public sealed class CoreWalkFixture
{
    private readonly Dictionary<string, SolutionModel> _byPolicy = new(StringComparer.Ordinal);

    public CoreWalkFixture()
    {
        Model = Walk(AnalysisPolicy.Default);
    }

    /// <summary>TestBed at the default policy, which is what nearly everything reads.</summary>
    public SolutionModel Model { get; }

    /// <summary>
    /// The same fixture under a different policy, walked once per distinct policy.
    /// </summary>
    /// <remarks>
    /// Some questions cannot be asked of the shared model: the policy is fixed at construction
    /// because a finding has to be able to name the policy that produced it, so a test about what
    /// happens at a different threshold needs a real second walk. Memoised so that asking twice is
    /// free — which is what makes <c>PolicySweepTests</c>, moving all twenty-eight values a notch
    /// each way, affordable at all.
    /// </remarks>
    public SolutionModel WalkWith(AnalysisPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var key = string.Join(";", policy.Values.Select(v => $"{v.Name}={v.Value}"));
        if (_byPolicy.TryGetValue(key, out var cached)) return cached;

        return _byPolicy[key] = Walk(policy);
    }

    private static SolutionModel Walk(AnalysisPolicy policy) =>
        new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
}

/// <summary>
/// The one collection every fixture-reading test belongs to.
/// </summary>
/// <remarks>
/// One collection rather than one per fixture, because xunit runs collections in parallel and two
/// of them would open an <c>MSBuildWorkspace</c> over the same solution at the same time — which
/// doubles the slowest part of the suite and makes the workspace emit load diagnostics that the
/// single-analysis case does not. That reason held when there were two analyses to keep apart and
/// it still holds for <c>OrderingTests</c>, which walks a second solution file of its own.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class FixtureCollection : ICollectionFixture<CoreWalkFixture>
{
    public const string Name = "TestBed fixture";
}

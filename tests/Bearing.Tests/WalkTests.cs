using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Runs Core's walker against TestBed once for the whole suite, alongside the probe's.
/// </summary>
public sealed class CoreWalkFixture
{
    private readonly Dictionary<string, SolutionModel> _byPolicy = new(StringComparer.Ordinal);

    public CoreWalkFixture()
    {
        Model = Walk(AnalysisPolicy.Default);
    }

    public SolutionModel Model { get; }

    /// <summary>
    /// The same fixture under a different policy, walked once per distinct policy.
    /// </summary>
    /// <remarks>
    /// The workspace load is the suite's cost centre, which is why everything shares one model.
    /// Some questions cannot be asked of that model: the policy is fixed at construction because
    /// a finding has to be able to name the policy that produced it, so a test about what happens
    /// at a different threshold needs a real second walk. Memoised so that asking twice is free,
    /// and used sparingly — at the time of writing, only by the truncation tests, which need a
    /// --top low enough to bite.
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
/// Core's walk against the probe's, type for type and edge for edge.
/// </summary>
/// <remarks>
/// <para>
/// The probe is still the oracle and stays verbatim; this is the reimplementation existing and
/// agreeing with it. Core is a rewrite — different model, different identity, different edge
/// collection — so every assertion here is a place the two could differ and do not.
/// </para>
/// <para>
/// <b>One divergence is intended and is the point.</b> Core keys a type by
/// <c>(assembly, fully-qualified name)</c>; the probe keys on the name alone. Where a solution
/// declares one FQN in two assemblies, the probe merges the rows and sums their metrics, and
/// Core does not. That is <c>docs/DEFECTS.md</c> §1, the one behaviour extraction is permitted
/// to change, and TestBed plants the case deliberately so the fix is observable rather than
/// asserted.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class WalkTests(CoreWalkFixture core)
{
    /// <summary>
    /// The planted cross-project collisions: one fully-qualified name each, two assemblies each.
    /// </summary>
    /// <remarks>
    /// <b>Two since P8, and the plural is the point.</b> <c>PayloadTag</c> proves the ROW is kept
    /// apart and cannot prove more, because it has no edges in either declaration.
    /// <c>CarrierTwin</c> carries an inbound edge in one assembly and an outbound edge in the
    /// other, which is what makes the merge fabricate a dependency — <c>ProjectCycleTests</c>.
    /// Everything here excludes both, because everything here is about what the two walkers agree
    /// on.
    /// </remarks>
    private static readonly HashSet<string> CollidingNames = new(StringComparer.Ordinal)
    {
        "global::TestBed.Shared.PayloadTag",
        "global::TestBed.Interop.CarrierTwin",
    };

    public static TheoryData<string> Measures =>
    [
        "FanIn", "FanOut", "EffectiveFanOut", "InboundReferenceCount",
        "Cyclomatic", "MaxMemberCyclomatic", "Dsm", "Transform", "StaticMutations",
        "MemberCount", "PublicMemberCount", "ExecutableMemberCount",
        "ParameterCount", "DataShape", "LinesOfCode",
    ];

    [Fact]
    public void Classification_now_carries_the_evidence_that_decided_it()
    {
        // The probe stores the verdict and discards the reason, so a developer cannot check it.
        Assert.All(core.Model.Types, t => Assert.False(string.IsNullOrWhiteSpace(t.Classification.Evidence)));

        var controller = core.Model.Types.First(t => t.Name.EndsWith("Controller", StringComparison.Ordinal));
        Assert.Equal("ApiBoundary", controller.Classification.Kind);
        Assert.Contains(":", controller.Classification.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Member_identity_distinguishes_what_the_probes_did_not()
    {
        // docs/DEFECTS.md §13: the probe's member id is the bare method name, so every Apply in
        // the solution shares one. Core qualifies by declaring type and parameters.
        var applies = core.Model.Types
            .SelectMany(t => t.Members)
            .Where(m => m.Name == "Apply")
            .ToList();

        Assert.True(applies.Count > 1, "the fixture should declare Apply on several types");
        Assert.Equal(applies.Count, applies.Select(m => m.Subject.Canonical).Distinct(StringComparer.Ordinal).Count());
    }

    // ------------------------------------------------------- the new fields ----

    [Fact]
    public void Every_edge_carries_a_kind_and_a_site()
    {
        var references = core.Model.Edges.SelectMany(e => e.References).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, r => Assert.True(r.Site.IsKnown, $"{r.From.Canonical} -> {r.To.Canonical} has no site"));

        // Not everything can be attributed, but if nothing can then the classifier is broken.
        var attributed = references.Count(r => r.Kind != EdgeKind.Other);
        Assert.True(attributed > references.Count / 4,
            $"only {attributed} of {references.Count} references were attributed to a kind");
    }

    [Fact]
    public void The_kinds_that_make_a_graph_readable_are_all_present()
    {
        // The filter that makes a DIP-heavy codebase legible needs these to exist and be
        // distinguishable. If a kind never appears on the fixture, nothing observes it.
        var kinds = core.Model.Edges.SelectMany(e => e.Kinds).ToHashSet();

        Assert.Contains(EdgeKind.InterfaceImplementation, kinds);
        Assert.Contains(EdgeKind.Inheritance, kinds);
        Assert.Contains(EdgeKind.Parameter, kinds);
        Assert.Contains(EdgeKind.Field, kinds);
        Assert.Contains(EdgeKind.Construction, kinds);
    }

    [Fact]
    public void A_primary_site_does_not_depend_on_walk_order()
    {
        // Same discipline as every other emitted ordering: the representative site is the
        // first by file then line, not the first the walk happened to reach.
        foreach (var edge in core.Model.Edges.Where(e => e.Weight > 1))
        {
            var expected = edge.References
                .Where(r => r.Site.IsKnown)
                .OrderBy(r => r.Site.File, StringComparer.Ordinal)
                .ThenBy(r => r.Site.Line)
                .First().Site;

            Assert.Equal(expected, edge.PrimarySite);
        }
    }

    [Fact]
    public void The_model_is_ordered_by_identity_not_by_discovery()
    {
        Assert.Equal(
            core.Model.Types.Select(t => t.Subject.Canonical).Order(StringComparer.Ordinal),
            core.Model.Types.Select(t => t.Subject.Canonical));

        Assert.Equal(
            core.Model.Edges.Select(e => (e.From.Canonical, e.To.Canonical)).Order(),
            core.Model.Edges.Select(e => (e.From.Canonical, e.To.Canonical)));
    }

    private static double CoreMeasure(TypeNode t, string measure) => measure switch
    {
        "FanIn" => t.FanIn,
        "FanOut" => t.FanOut,
        "EffectiveFanOut" => t.EffectiveFanOut,
        "InboundReferenceCount" => t.InboundReferenceCount,
        "Cyclomatic" => t.Cyclomatic,
        "MaxMemberCyclomatic" => t.MaxMemberCyclomatic,
        "Dsm" => t.Dsm,
        "Transform" => t.Transform,
        "StaticMutations" => t.StaticMutations,
        "MemberCount" => t.MemberCount,
        "PublicMemberCount" => t.PublicMemberCount,
        "ExecutableMemberCount" => t.ExecutableMemberCount,
        "ParameterCount" => t.ParameterCount,
        "DataShape" => t.DataShape,
        "LinesOfCode" => t.LinesOfCode,
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null),
    };
}

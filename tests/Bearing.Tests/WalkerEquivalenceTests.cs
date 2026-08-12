using ArchProbe;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Runs Core's walker against TestBed once for the whole suite, alongside the probe's.
/// </summary>
public sealed class CoreWalkFixture
{
    public CoreWalkFixture()
    {
        Model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution })
            .WalkAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    public SolutionModel Model { get; }
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
public sealed class WalkerEquivalenceTests(CoreWalkFixture core, FixtureRun probe)
{
    /// <summary>
    /// The planted cross-project collision: one fully-qualified name, two assemblies.
    /// </summary>
    private const string CollidingName = "global::TestBed.Shared.PayloadTag";

    [Fact]
    public void Core_finds_the_same_types_plus_the_one_the_probe_merges()
    {
        var probeNames = probe.Result.Types.Select(t => t.Id).ToList();
        var coreNames = core.Model.Types.Select(t => t.FullyQualifiedName).ToList();

        // The probe collapses the collision into one row; Core keeps both declarations.
        Assert.Equal(1, probeNames.Count(n => n == CollidingName));
        Assert.Equal(2, coreNames.Count(n => n == CollidingName));

        Assert.Equal(probeNames.Count + 1, coreNames.Count);
        Assert.Equal(probeNames.Order(StringComparer.Ordinal), coreNames.Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_collision_is_two_types_in_two_assemblies_rather_than_one_with_summed_metrics()
    {
        var split = core.Model.Types.Where(t => t.FullyQualifiedName == CollidingName).ToList();
        var merged = probe.Result.Types.Single(t => t.Id == CollidingName);

        Assert.Equal(2, split.Count);
        Assert.Equal(["Data", "Tools"], split.Select(t => t.Assembly).Order(StringComparer.Ordinal));

        // The probe's row is the two declarations added together. Core's are the parts.
        Assert.Equal(merged.MemberCount, split.Sum(t => t.MemberCount));
        Assert.Equal(merged.Loc, split.Sum(t => t.LinesOfCode));
        Assert.Equal(merged.Cyclomatic, split.Sum(t => t.Cyclomatic));

        // And each part is attributed to the project that actually declares it, rather than
        // both being credited to whichever one loaded first.
        Assert.All(split, t => Assert.Equal(t.Assembly, t.Project));
    }

    [Theory]
    [MemberData(nameof(Measures))]
    public void Every_uncollided_type_measures_the_same(string measure)
    {
        var byName = core.Model.Types
            .Where(t => t.FullyQualifiedName != CollidingName)
            .ToDictionary(t => t.FullyQualifiedName, StringComparer.Ordinal);

        var compared = 0;
        foreach (var p in probe.Result.Types.Where(t => t.Id != CollidingName))
        {
            var c = byName[p.Id];
            Assert.Equal(ProbeMeasure(p, measure), CoreMeasure(c, measure));
            compared++;
        }

        Assert.Equal(probe.Result.Types.Count - 1, compared);
    }

    public static TheoryData<string> Measures =>
    [
        "FanIn", "FanOut", "EffectiveFanOut", "InboundReferenceCount",
        "Cyclomatic", "MaxMemberCyclomatic", "Dsm", "Transform", "StaticMutations",
        "MemberCount", "PublicMemberCount", "ExecutableMemberCount",
        "ParameterCount", "DataShape", "LinesOfCode",
    ];

    [Fact]
    public void Every_type_carries_the_same_architectural_kind()
    {
        var byName = core.Model.Types.ToLookup(t => t.FullyQualifiedName, StringComparer.Ordinal);

        foreach (var p in probe.Result.Types.Where(t => t.Id != CollidingName))
            Assert.Equal(p.Kind, byName[p.Id].Single().Classification.Kind);
    }

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
    public void The_same_edges_are_found_with_the_same_weights()
    {
        var probeEdges = probe.Result.Edges
            .Where(e => e.From != CollidingName && e.To != CollidingName)
            .ToDictionary(e => (e.From, e.To), e => e.Weight);

        var coreEdges = new Dictionary<(string, string), int>();
        foreach (var e in core.Model.Edges)
        {
            var from = core.Model.Find(e.From)!.FullyQualifiedName;
            var to = core.Model.Find(e.To)!.FullyQualifiedName;
            if (from == CollidingName || to == CollidingName) continue;

            coreEdges[(from, to)] = coreEdges.GetValueOrDefault((from, to)) + e.Weight;
        }

        Assert.Equal(probeEdges.Keys.Order(), coreEdges.Keys.Order());
        foreach (var (pair, weight) in probeEdges)
            Assert.Equal(weight, coreEdges[pair]);
    }

    [Fact]
    public void External_namespaces_match()
    {
        var byName = core.Model.Types.ToLookup(t => t.FullyQualifiedName, StringComparer.Ordinal);

        foreach (var p in probe.Result.Types.Where(t => t.Id != CollidingName))
            Assert.Equal(
                p.ExternalNamespaces.Order(StringComparer.Ordinal),
                byName[p.Id].Single().ExternalNamespaces.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Members_match_one_for_one()
    {
        var byName = core.Model.Types.ToLookup(t => t.FullyQualifiedName, StringComparer.Ordinal);

        // The probe records only methods and constructors; Core records every member, so
        // compare the intersection on the probe's terms.
        foreach (var group in probe.Result.Methods.GroupBy(m => m.DeclaringTypeId, StringComparer.Ordinal))
        {
            if (group.Key == CollidingName) continue;

            var coreMembers = byName[group.Key].Single().Members;
            foreach (var m in group)
            {
                var match = coreMembers.Single(c =>
                    c.Name == m.Name && c.Location.Line == m.Line);

                Assert.Equal(m.Cyclomatic, match.Cyclomatic);
                Assert.Equal(m.Dsm, match.Dsm);
                Assert.Equal(m.MaxNestingDepth, match.MaxNestingDepth);
                Assert.Equal(m.ParamCount, match.ParameterCount);
                Assert.Equal(m.Loc, match.LinesOfCode);
            }
        }
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

    [Fact]
    public void Coverage_matches()
    {
        Assert.Equal(probe.Result.ExcludedTypes, core.Model.Coverage.ExcludedTypes);
        Assert.Equal(
            probe.Result.SkippedProjects.Order(StringComparer.Ordinal),
            core.Model.Coverage.SkippedProjects.Order(StringComparer.Ordinal));
        Assert.Equal(
            probe.Result.Projects.Select(p => p.Name).Order(StringComparer.Ordinal),
            core.Model.Projects.Select(p => p.Name).Order(StringComparer.Ordinal));
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

    // ------------------------------------------------------------- adapters ----

    private static double ProbeMeasure(TypeMetrics t, string measure) => measure switch
    {
        "FanIn" => t.FanIn,
        "FanOut" => t.FanOut,
        "EffectiveFanOut" => t.FanOutEffective,
        "InboundReferenceCount" => t.InboundRefCount,
        "Cyclomatic" => t.Cyclomatic,
        "MaxMemberCyclomatic" => t.MaxMemberCyclomatic,
        "Dsm" => t.Dsm,
        "Transform" => t.Transform,
        "StaticMutations" => t.StaticMutations,
        "MemberCount" => t.MemberCount,
        "PublicMemberCount" => t.PublicMemberCount,
        "ExecutableMemberCount" => t.ExecutableMembers,
        "ParameterCount" => t.ParamCount,
        "DataShape" => t.DataShape,
        "LinesOfCode" => t.Loc,
        _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null),
    };

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

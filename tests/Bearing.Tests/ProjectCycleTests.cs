using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Project-level circular references — <c>PRD-free-tier.md</c> §7.1, shipped at A3.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fixture cannot produce one, and that is the expected state of the world rather than a
/// gap in the plant.</b> Every ordinary cross-project edge follows a project reference, MSBuild
/// forbids those from cycling, so aggregating the type graph over a solution that compiles
/// normally reproduces the reference DAG. TestBed is one of those solutions and no plant would
/// change it — the shape needs an analysed assembly reached some way other than a project
/// reference, which is a property of a build and not of a source file.
/// </para>
/// <para>
/// So the cycle is constructed here, from the primitives
/// <see cref="Cycles.AmongProjects(IEnumerable{ValueTuple{string, string}}, IEnumerable{ValueTuple{string, string}})"/>
/// takes. That is <c>ProjectReachability</c>'s precedent and it is taken for the same reason: a
/// test that could only run against a graph with no cycle in it would pass by having no case.
/// <c>tests/TestBed</c> proves judgements; whether this fires on real input is
/// <b>not</b> established by anything here.
/// </para>
/// <para>
/// The other half of this file is the confusion <c>docs/DEFECTS.md</c> §1 caused: it fabricated a
/// five-project cycle on nopCommerce by merging same-named types across assemblies. The last case
/// pins that a merged identity is what invents one, so the difference is asserted rather than
/// remembered.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class ProjectCycleTests(CoreWalkFixture core)
{
    private static IReadOnlyList<Cycle> Among(
        (string TypeId, string Project)[] types,
        params (string From, string To)[] edges) =>
        Cycles.AmongProjects(types, edges);

    private static string[] Names(Cycle cycle) =>
        [.. cycle.Members.Select(m => m.Canonical.Replace("project|", "", StringComparison.Ordinal))];

    /// <summary>The ordinary case: every edge runs one way, so there is nothing to report.</summary>
    [Fact]
    public void A_layered_solution_has_no_project_cycle()
    {
        var found = Among(
            [("Api.Controller", "Api"), ("Core.Service", "Core"), ("Data.Repo", "Data")],
            ("Api.Controller", "Core.Service"),
            ("Core.Service", "Data.Repo"));

        Assert.Empty(found);
    }

    /// <summary>Two projects each naming a type in the other.</summary>
    [Fact]
    public void Two_projects_naming_each_other_are_a_cycle()
    {
        var cycle = Assert.Single(Among(
            [("Web.Page", "Web"), ("Core.Service", "Core")],
            ("Web.Page", "Core.Service"),
            ("Core.Service", "Web.Page")));

        Assert.Equal(["Core", "Web"], Names(cycle));
        Assert.Equal(["Core", "Web"], cycle.Path.Select(p => p.Canonical.Replace("project|", "", StringComparison.Ordinal)));
        Assert.True(cycle.PathCoversEveryMember);
    }

    /// <summary>
    /// Two types in the same project pointing at each other is not a project cycle.
    /// </summary>
    /// <remarks>
    /// The distinguishing case, and the one an aggregation gets wrong by omission: a project
    /// depends on itself in every solution ever written, and a graph that kept those self-edges
    /// would report every project with a mutual pair inside it as circular.
    /// </remarks>
    [Fact]
    public void A_cycle_inside_one_project_is_not_a_project_cycle()
    {
        var found = Among(
            [("Core.A", "Core"), ("Core.B", "Core")],
            ("Core.A", "Core.B"),
            ("Core.B", "Core.A"));

        Assert.Empty(found);
    }

    /// <summary>A longer ring is one cycle, and its loop names every project in it.</summary>
    [Fact]
    public void A_three_project_ring_is_one_cycle_with_a_traversable_loop()
    {
        var cycle = Assert.Single(Among(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C")],
            ("A.T", "B.T"), ("B.T", "C.T"), ("C.T", "A.T")));

        Assert.Equal(["A", "B", "C"], Names(cycle));
        Assert.True(cycle.PathCoversEveryMember);
    }

    /// <summary>
    /// A loop shorter than the component says so rather than implying it is the whole of it.
    /// </summary>
    [Fact]
    public void A_loop_smaller_than_the_component_is_marked_as_partial()
    {
        // A and B are mutual; C and D hang off them, so all four are one component while the
        // shortest loop through A is two long.
        var cycle = Assert.Single(Among(
            [("A.T", "A"), ("B.T", "B"), ("C.T", "C"), ("D.T", "D")],
            ("A.T", "B.T"), ("B.T", "A.T"),
            ("B.T", "C.T"), ("C.T", "D.T"), ("D.T", "A.T")));

        Assert.Equal(4, cycle.Size);
        Assert.Equal(2, cycle.Path.Count);
        Assert.False(cycle.PathCoversEveryMember);
    }

    /// <summary>
    /// An edge whose endpoint was never analysed contributes no project.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §7 — 123 of Jellyfin's edges and 57 of nopCommerce's point at types
    /// no node was built for. Attributing one to a project by guessing from its name is how §1
    /// invented a cycle, so an edge missing either endpoint is dropped and not repaired.
    /// </remarks>
    [Fact]
    public void An_edge_to_an_unanalysed_type_cannot_close_a_cycle()
    {
        var found = Among(
            [("Web.Page", "Web"), ("Core.Service", "Core")],
            ("Web.Page", "Core.Service"),
            ("Core.Service", "Gone.Missing"),
            ("Gone.Missing", "Web.Page"));

        Assert.Empty(found);
    }

    /// <summary>
    /// The defect and the feature, side by side: merging two same-named types across assemblies
    /// is what fabricates a project cycle, and keying on assembly is what stops it.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §1 and the caution in <c>PRD-free-tier.md</c> §7.1, which says the
    /// finding is not trustworthy until <c>SubjectRef.ForType(assembly, fqn)</c> is what the
    /// walkers key on. It is — <c>WalkerEquivalenceTests</c> asserts that from three sides — and
    /// this shows what the alternative would have reported here, so the two cannot be confused in
    /// a bug report.
    /// </remarks>
    [Fact]
    public void Merging_a_shared_name_across_assemblies_is_what_invents_a_cycle()
    {
        // One FQN, declared in two assemblies, in two projects. Keyed by assembly these are two
        // types and the edges run Web -> Core and Core -> Shared: a chain.
        (string TypeId, string Project)[] keyedByAssembly =
        [
            ("type|Web|Tag", "Web"),
            ("type|Core|Service", "Core"),
            ("type|Shared|Tag", "Shared"),
        ];

        Assert.Empty(Among(
            keyedByAssembly,
            ("type|Web|Tag", "type|Core|Service"),
            ("type|Core|Service", "type|Shared|Tag")));

        // Keyed by name alone the two declarations collapse into one node, which inherits the
        // Web project and Shared's inbound edge — and the chain closes into a cycle that is not
        // in the code.
        (string TypeId, string Project)[] keyedByName =
        [
            ("Tag", "Web"),
            ("Service", "Core"),
        ];

        var fabricated = Assert.Single(Among(
            keyedByName,
            ("Tag", "Service"),
            ("Service", "Tag")));

        Assert.Equal(["Core", "Web"], Names(fabricated));
    }
    // ------------------------------------------------------- D1, over the real fixture ----

    /// <summary>
    /// <b>D1's retro-protection, and the half <c>PayloadTag</c> could not carry.</b> The fixture
    /// contains a name declared in two assemblies whose <i>merge</i> closes a project cycle that
    /// its split does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test above proves the mechanism over primitives. This one proves the fixture contains
    /// it — which matters because <c>PayloadTag</c>, the collision that was already there, has
    /// fan-in 0 in both declarations and no outbound edges either. A type nothing points at and
    /// which points at nothing sits in no component whichever way it is keyed, so merged and split
    /// give identical answers and the defect's damage was unobservable on this fixture.
    /// </para>
    /// <para>
    /// <c>CarrierTwin</c> is wired the way nopCommerce's collision was: an inbound edge inside
    /// Core and an outbound edge inside Data. Split, neither crosses a project boundary. Merged,
    /// one of them must — and Data already depends on Core, so the aggregate closes into a cycle
    /// that no code in the fixture contains. <c>P8</c>, and <c>tests/TestBed/Core/Shared/CarrierTwin.cs</c>
    /// carries the arithmetic.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fixtures_collision_fabricates_a_project_cycle_only_when_merged()
    {
        var model = core.Model;

        // The collision is real and Core keeps it apart: two rows, one per assembly, and the
        // edges sit on different declarations.
        var twins = model.Types.Where(t => t.Name == "CarrierTwin").OrderBy(t => t.Assembly, StringComparer.Ordinal).ToList();

        Assert.Equal(2, twins.Count);
        Assert.Equal((1, 0), (twins[0].FanIn, twins[0].FanOut));   // Core: pointed at
        Assert.Equal((0, 1), (twins[1].FanIn, twins[1].FanOut));   // Data: pointing out

        // Split — what Core does. No project cycle, which is the right answer.
        Assert.Empty(model.ProjectCycles);

        // Merged — what keying on the fully-qualified name alone does. The assembly segment of the
        // identity is dropped, which is precisely the defect, and the same edges then close.
        static string Merge(string canonical)
        {
            var parts = canonical.Split('|');
            return parts.Length == 3 ? $"{parts[0]}|{parts[2]}" : canonical;
        }

        var merged = Cycles.AmongProjects(
            model.Types
                .GroupBy(t => Merge(t.Subject.Canonical), StringComparer.Ordinal)
                .Select(g => (g.Key, g.OrderBy(t => t.Assembly, StringComparer.Ordinal).First().Project)),
            model.Edges.Select(e => (Merge(e.From.Canonical), Merge(e.To.Canonical))));

        var fabricated = Assert.Single(merged);

        Assert.Equal(
            ["Core", "Data"],
            fabricated.Members.Select(m => m.Canonical.Split('|')[^1]).Order(StringComparer.Ordinal));
    }
}

using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// What the walk records that a metric alone does not: evidence, identity, edge kind and site.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is left of the equivalence suite, and it is the half that was never a comparison.</b>
/// This file ran Core's walk against the probe's, type for type and edge for edge, for as long as
/// Core was a reimplementation whose agreement was a result rather than a tautology. Those
/// assertions went at R2 with the thing they compared against. These six did not, because each
/// asserts something the probe had no surface for — a classification that carries the reason it
/// was reached, a member identity that survives an overload, an edge that knows what kind of
/// reference it is and where, a canonical order that does not depend on discovery.
/// </para>
/// <para>
/// <b>Identity is the one place Core was allowed to differ, and TestBed still plants the case.</b>
/// Core keys a type by <c>(assembly, fully-qualified name)</c>; the probe keyed on the name alone,
/// so where a solution declared one FQN in two assemblies it merged the rows and summed their
/// metrics. That is name-only identity. The fix outlives its witness: the two declarations
/// are two rows in <c>StructureTests.Fixture_shape_is_stable</c>, and the dependency the merge
/// used to fabricate is <c>ProjectCycleTests</c>.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class WalkTests(CoreWalkFixture core)
{

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
        // The probe's member id is the bare method name, so every Apply in
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
}

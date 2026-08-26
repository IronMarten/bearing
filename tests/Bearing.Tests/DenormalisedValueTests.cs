using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Every number the model caches, asserted against the thing it is a cache of.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class exists because of D63, and because of what D63 was not.</b> It was not an
/// untested seam: <c>CsvOutputTests.The_three_files_join_on_the_type_id</c> already asserted that
/// the three exports join, and its own note says <i>"three files are only three files if they
/// join"</i>. That test checks <b>referential integrity</b> — every edge endpoint is a row in
/// <c>types.csv</c> — and every endpoint was. What was wrong was <b>arithmetic agreement</b>:
/// <c>FanOut</c> is a summary of the edges, stored as a scalar on the type row, and nothing
/// asserted the scalar against what it summarises.
/// </para>
/// <para>
/// <b>So the class of defect is a denormalised value</b>, and the model holds several: a count
/// cached on one row that restates data held in a collection somewhere else. Each is written at a
/// different moment of the walk than the collection it describes, which is exactly how D63
/// happened — outbound was counted during the walk and inbound in <c>ModelBuilder.Build</c>, and
/// only one of them could know whether the target had a node.
/// </para>
/// <para>
/// <b>A property here is worth more than a reading.</b> D63 was invisible to 538 tests and to four
/// golden snapshots of the very files that disagreed, because a snapshot pins what a number
/// <i>was</i> and never what it <i>means</i>. These assertions hold over every type in the fixture
/// and fail on the first one that drifts.
/// </para>
/// <para>
/// <b>What is deliberately not here.</b> <c>LinesOfCode</c>, <c>ParameterCount</c>,
/// <c>DataShape</c>, <c>PublicMemberCount</c> and <c>ExecutableMemberCount</c> are walk-derived
/// and the model keeps no collection to recompute them from — asserting them would mean restating
/// the walk, which is a second implementation and not a check.
/// <c>Coverage.EdgesToUnanalysedTypes</c> is the same shape by design: it counts what was dropped,
/// so what it counts is not in the model to be counted again.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class DenormalisedValueTests(CoreWalkFixture core)
{
    /// <summary>
    /// <c>FanOut</c> is the count of a type's out-edges in the model's own edge list — D63.
    /// </summary>
    /// <remarks>
    /// The defect exactly: <c>FanOut</c> counted references the walk had seen and the edge list
    /// carried only those whose target became a node, so the column ran 1 to 10 higher on 1.0% of
    /// nopCommerce's types, 1.5% of Umbraco's and 6.7% of Jellyfin's. Held here at model level as
    /// well as through <c>NeighbourhoodTests</c>, because the export reads the column and the
    /// drill-down reads the edges, and they are two consumers of one fact.
    /// </remarks>
    [Fact]
    public void Fan_out_is_the_out_edges_the_model_carries()
    {
        var outgoing = core.Model.Edges
            .GroupBy(e => e.From.Canonical, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.All(core.Model.Types, type =>
            Assert.Equal(
                outgoing.TryGetValue(type.Subject.Canonical, out var n) ? n : 0,
                type.FanOut));
    }

    /// <summary>The same in the other direction, which is the half that was always right.</summary>
    [Fact]
    public void Fan_in_is_the_in_edges_the_model_carries()
    {
        var incoming = core.Model.Edges
            .GroupBy(e => e.To.Canonical, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.All(core.Model.Types, type =>
            Assert.Equal(
                incoming.TryGetValue(type.Subject.Canonical, out var n) ? n : 0,
                type.FanIn));
    }

    /// <summary>
    /// <c>InboundReferenceCount</c> sums the references on the in-edges, not the edges.
    /// </summary>
    /// <remarks>
    /// An <c>Edge</c> is one (from, to) pair however many references it carries, so this and
    /// <see cref="TypeNode.FanIn"/> are different numbers on purpose and both are printed. The one
    /// that would go unnoticed is this drifting to equal the other.
    /// </remarks>
    [Fact]
    public void Inbound_reference_count_sums_the_references_not_the_edges()
    {
        var weight = core.Model.Edges
            .GroupBy(e => e.To.Canonical, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Weight), StringComparer.Ordinal);

        Assert.All(core.Model.Types, type =>
            Assert.Equal(
                weight.TryGetValue(type.Subject.Canonical, out var n) ? n : 0,
                type.InboundReferenceCount));
    }

    /// <summary>
    /// <c>EffectiveFanOut</c> is fan-out with the abstractions removed, and the rule is in one
    /// place.
    /// </summary>
    /// <remarks>
    /// It is set in <c>ModelBuilder.Build</c> from a set built there, so nothing outside that
    /// method can see which types were treated as insulating. Recomputing it from the published
    /// model is what makes the rule checkable: an interface, an abstract type, or a
    /// <c>Contract</c> by classification.
    /// </remarks>
    [Fact]
    public void Effective_fan_out_removes_exactly_the_insulating_targets()
    {
        var insulating = core.Model.Types
            .Where(t => t.IsAbstractOrInterface
                        || string.Equals(t.Classification.Kind, TypeKinds.Contract, StringComparison.Ordinal))
            .Select(t => t.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(core.Model.Types, type =>
        {
            Assert.Equal(
                type.Outbound.Count(o => !insulating.Contains(o.Canonical)),
                type.EffectiveFanOut);

            Assert.True(type.EffectiveFanOut <= type.FanOut);
        });
    }

    /// <summary><c>MemberCount</c> is the number of members the type actually holds.</summary>
    [Fact]
    public void Member_count_is_the_members()
    {
        Assert.All(core.Model.Types, type => Assert.Equal(type.Members.Count, type.MemberCount));
    }

    /// <summary>
    /// <c>CohortSize</c> is how many types share the cohort, counted over the model.
    /// </summary>
    /// <remarks>
    /// It is the denominator of every cohort-relative claim the tool makes — <i>"37x the median
    /// internal complexity of the 96 types deriving from BaseNopValidator"</i> — so a size that
    /// disagreed with the population would be wrong in the sentence rather than in a column.
    /// </remarks>
    [Fact]
    public void Cohort_size_is_the_cohort_population()
    {
        var sizes = core.Model.Types
            .GroupBy(t => t.Cohort, EqualityComparer<Cohort>.Default)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(core.Model.Types, type => Assert.Equal(sizes[type.Cohort], type.CohortSize));
    }

    /// <summary>
    /// A type's own summaries agree with its members — the ones that cannot drift, asserted so
    /// they cannot start.
    /// </summary>
    /// <remarks>
    /// <c>Cyclomatic</c>, <c>Dsm</c>, <c>Transform</c> and <c>StaticMutations</c> are computed
    /// properties today and a computed property cannot disagree with its source. **That is exactly
    /// what was true of <c>FanIn</c> and false of <c>FanOut</c>**, and the difference between them
    /// was one refactor. These assertions cost nothing and fail the day one of them is cached.
    /// </remarks>
    [Fact]
    public void Member_sums_agree_with_the_members()
    {
        Assert.All(core.Model.Types, type =>
        {
            Assert.Equal(type.Members.Sum(m => m.Cyclomatic), type.Cyclomatic);
            Assert.Equal(type.Members.Sum(m => m.Dsm), type.Dsm);
            Assert.Equal(type.Members.Sum(m => m.Transform), type.Transform);
            Assert.Equal(type.Members.Sum(m => m.StaticMutations), type.StaticMutations);
            Assert.Equal(
                type.Members.Count == 0 ? 0 : type.Members.Max(m => m.Cyclomatic),
                type.MaxMemberCyclomatic);
        });
    }
}

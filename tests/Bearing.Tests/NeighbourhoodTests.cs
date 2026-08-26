using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The one-hop neighbourhood A8's drill-down renders — <see cref="Neighbourhoods"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are properties over every type, not readings off a handful.</b> The defect this
/// projection exists downstream of — <b>D63</b>, a <c>FanOut</c> column that disagreed with the
/// edge list on 1.0% of nopCommerce's types and 6.7% of Jellyfin's — was invisible to 538 tests
/// because the fixture has no edge to an unanalysed type, and it was found only when something
/// outside the suite joined the two files for the first time. A test that pinned three known
/// answers would have missed it the same way.
/// </para>
/// <para>
/// <b>So what is asserted is the reconciliation itself</b>, over the whole fixture: the
/// neighbourhood must account for exactly <see cref="TypeNode.FanIn"/> and
/// <see cref="TypeNode.FanOut"/>, every time. That holds whether or not the fixture can produce
/// the shape that broke it, and it fails if either side drifts again.
/// </para>
/// <para>
/// <b>The no-truncation property is the feature's whole point.</b> A11 round 2's T5 measured
/// completeness — knowing when an answer is finished — so a count a reader cannot check against
/// the list beneath it defeats it. Asserting group sums against the totals is that bar as a test.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class NeighbourhoodTests(CoreWalkFixture core)
{
    /// <summary>
    /// Every type's neighbourhood accounts for exactly its fan-in and fan-out — D63's property.
    /// </summary>
    [Fact]
    public void Reconciles_with_fan_in_and_fan_out()
    {
        Assert.All(core.Model.Types, type =>
        {
            var hood = Neighbourhoods.Of(core.Model, type.Subject);

            Assert.NotNull(hood);
            Assert.Equal(type.FanOut, hood.DependsOnCount);
            Assert.Equal(type.FanIn, hood.DependedOnByCount);
        });
    }

    /// <summary>Nothing is dropped: the groups sum to the totals they are printed beside.</summary>
    [Fact]
    public void Groups_account_for_every_neighbour()
    {
        Assert.All(core.Model.Types, type =>
        {
            var hood = Neighbourhoods.Of(core.Model, type.Subject)!;

            Assert.Equal(hood.DependsOnCount, hood.DependsOn.Sum(g => g.Types.Count));
            Assert.Equal(hood.DependedOnByCount, hood.DependedOnBy.Sum(g => g.Types.Count));
        });
    }

    /// <summary>
    /// A project appears once per direction, so a count is never split across two rows.
    /// </summary>
    /// <remarks>
    /// Two rows for one project would each be true and the pair would read as two projects — D50's
    /// shape, where a mark is accurate and the reader counts something else.
    /// </remarks>
    [Fact]
    public void Each_project_is_one_group()
    {
        Assert.All(core.Model.Types, type =>
        {
            var hood = Neighbourhoods.Of(core.Model, type.Subject)!;

            Assert.Equal(hood.DependsOn.Count, hood.DependsOn.Select(g => g.Project).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(hood.DependedOnBy.Count, hood.DependedOnBy.Select(g => g.Project).Distinct(StringComparer.Ordinal).Count());
        });
    }

    /// <summary>Groups are ordered largest first, which is what the renderers rely on.</summary>
    [Fact]
    public void Groups_are_ordered_largest_first()
    {
        Assert.All(core.Model.Types, type =>
        {
            var hood = Neighbourhoods.Of(core.Model, type.Subject)!;

            foreach (var groups in new[] { hood.DependsOn, hood.DependedOnBy })
                Assert.Equal(
                    groups.Select(g => g.Types.Count),
                    groups.Select(g => g.Types.Count).OrderByDescending(n => n));
        });
    }

    /// <summary>
    /// A mutual dependency is two facts and is counted once as a node.
    /// </summary>
    /// <remarks>
    /// <see cref="Neighbourhood.Distinct"/> exists so a renderer that wants the node count does not
    /// add the two directions and double a type that is in both.
    /// </remarks>
    [Fact]
    public void Distinct_counts_a_mutual_dependency_once()
    {
        var mutual = core.Model.Types
            .Select(t => Neighbourhoods.Of(core.Model, t.Subject)!)
            .First(h => h.DependsOn.SelectMany(g => g.Types)
                .Select(t => t.Subject.Canonical)
                .Intersect(h.DependedOnBy.SelectMany(g => g.Types).Select(t => t.Subject.Canonical),
                    StringComparer.Ordinal)
                .Any());

        Assert.True(mutual.Distinct < mutual.DependsOnCount + mutual.DependedOnByCount);
    }

    /// <summary>
    /// A member-level subject has no type neighbourhood, and that is null rather than empty.
    /// </summary>
    /// <remarks>
    /// The two answers differ and a renderer acts on the difference: an isolated type is a fact
    /// worth printing — <i>"nothing in this solution depends on it"</i> is what makes a
    /// load-bearing claim checkable — and a member has nothing to say at this level at all.
    /// </remarks>
    [Fact]
    public void A_member_subject_has_no_neighbourhood()
    {
        var member = core.Model.Types
            .SelectMany(t => t.Members)
            .Select(m => m.Subject)
            .First();

        Assert.Null(Neighbourhoods.Of(core.Model, member));
    }
}

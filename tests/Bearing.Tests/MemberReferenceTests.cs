using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The member reference graph — A9's first layer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Before this, every reference endpoint in the model was a type</b> — measured, 385 of 385 on
/// the fixture — because <c>ReferenceCollector</c> walked from the type declaration and never knew
/// which member it was inside. Members are first-class everywhere else in the model: subjects,
/// metrics, method-level findings. The reference graph was the last place they were not, and dead
/// code at member level cannot be computed without them.
/// </para>
/// <para>
/// <b>Nothing renders this yet, which is why the assertions are about the graph's shape.</b> A9's
/// remaining layers are the list of ways a fan-in of zero lies; what layer 1 owes is a graph whose
/// endpoints join, whose sources are members rather than types, and which contains the references
/// the <i>type</i> graph is obliged to throw away.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class MemberReferenceTests(CoreWalkFixture core)
{
    private const string Turnstile = "IdentityTurnstile";

    /// <summary>Both endpoints of every reference name a member that exists.</summary>
    /// <remarks>
    /// <b>The graph's one structural invariant.</b> An endpoint that joins nothing is the failure
    /// mode <c>docs/DEFECTS.md</c> §7 was at type level — a reference resolving to a symbol no node
    /// was built for — and there it was a crash rather than an inaccuracy. Here a <c>To</c> that
    /// joined nothing would silently subtract from a member's inbound count, which is the number
    /// A9 makes a deletion claim from.
    /// </remarks>
    [Fact]
    public void Every_endpoint_joins_a_member_that_exists()
    {
        var members = core.Model.Types.SelectMany(t => t.Members).ToList();
        var known = members.Select(m => m.Subject.Canonical).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(members.SelectMany(m => m.Inbound));

        foreach (var member in members)
            foreach (var reference in member.Inbound)
            {
                Assert.Equal(member.Subject.Canonical, reference.To.Canonical);
                if (reference.From is { } from) Assert.Contains(from.Canonical, known);
            }
    }

    /// <summary>
    /// A reference between two members of one type is recorded, and the type graph still has no
    /// self-edge.
    /// </summary>
    /// <remarks>
    /// <b>The reason the member graph is parallel rather than a column on <c>TypeReference</c>.</b>
    /// A self-edge is not a dependency: admitting one would move fan-in, fan-out, instability and
    /// every cycle in the report. But at member level that same reference is the entire question —
    /// a private helper's only caller is nearly always a sibling on its own type, and a dead-code
    /// claim built on the type graph would report every one of them as unreferenced.
    /// </remarks>
    [Fact]
    public void An_intra_type_reference_is_in_the_member_graph_and_not_in_the_type_graph()
    {
        var turnstile = core.Model.Types.Single(t => t.Name == Turnstile);

        Assert.DoesNotContain(
            core.Model.Edges,
            e => e.From.Canonical == turnstile.Subject.Canonical
                 && e.To.Canonical == turnstile.Subject.Canonical);

        // Admit calls the out-overload of TryAdmit, and both are members of this same type.
        var target = Assert.Single(
            turnstile.Members,
            m => m.Subject.Canonical.EndsWith("System.String@)", StringComparison.Ordinal));

        Assert.Contains(
            target.Inbound,
            r => r.From?.Canonical.EndsWith("Admit(System.String)", StringComparison.Ordinal) == true
                 && r.Kind == EdgeKind.Invocation);
    }

    /// <summary>
    /// The source is the member the reference is written in, at every kind of member.
    /// </summary>
    /// <remarks>
    /// <b>Accessors and constructors are the cases worth naming.</b> A reference in a property's
    /// getter belongs to the property, one in an event's <c>add</c> belongs to the event, and one
    /// in a static constructor belongs to that rather than to the instance one beside it. Each is a
    /// place the enclosing member is not the nearest method declaration, and each is a category
    /// <c>TECHREQ-job-a.md</c> §5.6 will have to reason about.
    /// </remarks>
    [Fact]
    public void The_source_is_the_enclosing_member_including_accessors_and_constructors()
    {
        var turnstile = core.Model.Types.Single(t => t.Name == Turnstile);

        Member Named(string name) => turnstile.Members.First(m => m.Name == name);

        // The event's accessors write the backing field.
        Assert.Contains(Named("_turned").Inbound, r => r.From?.Canonical.Contains("E:", StringComparison.Ordinal) == true);

        // The property getter reads the backing field.
        Assert.Contains(Named("_refused").Inbound, r => r.From?.Canonical.Contains("P:", StringComparison.Ordinal) == true);

        // The static constructor writes the static property; the instance one writes the field.
        Assert.Contains(Named("Opened").Inbound, r => r.From?.Canonical.EndsWith("#cctor", StringComparison.Ordinal) == true);
        Assert.Contains(Named("Lane").Inbound, r => r.From?.Canonical.Contains("#ctor(", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Two overloads that differ only by a parameter modifier collect their own callers.
    /// </summary>
    /// <remarks>
    /// X14 and this layer together, and neither is worth much without the other: an exact identity
    /// nothing references is bookkeeping, and a reference graph over merged identities is a
    /// deletion claim about the wrong member. The by-value overload here is called by nothing and
    /// the <c>out</c> one by two members; under the display string that preceded X14 they were one
    /// member with two callers.
    /// </remarks>
    [Fact]
    public void Overloads_collect_their_own_callers()
    {
        var overloads = core.Model.Types.Single(t => t.Name == Turnstile)
            .Members.Where(m => m.Name == "TryAdmit")
            .ToList();

        Assert.Equal(2, overloads.Count);
        Assert.Equal([0, 2], overloads.Select(m => m.InboundReferenceCount).Order());
    }

    /// <summary>References that name a member the walk recorded no row for are counted, not dropped.</summary>
    /// <remarks>
    /// <para>
    /// Invariant 8 at member level. The fixture's seven are compiler-generated constructors: the
    /// three <c>[Route]</c> usages resolve to <c>RouteAttribute</c>'s implicit parameterless
    /// constructor, which no syntax declares and no member row exists for.
    /// </para>
    /// <para>
    /// <b>Asserted as a positive number rather than as zero</b>, because zero here would mean the
    /// walk had stopped finding references it cannot place — which is the state the fixture was in
    /// before, when there was no member graph at all.
    /// </para>
    /// </remarks>
    [Fact]
    public void References_to_members_with_no_row_are_disclosed()
    {
        Assert.True(core.Model.Coverage.MemberReferencesToUnanalysedMembers > 0,
            "no unplaceable member references at all, which the fixture is known to contain");
    }

    /// <summary>
    /// A reference written on the type rather than inside a member carries no source, and the
    /// fixture cannot currently produce one.
    /// </summary>
    /// <remarks>
    /// <b>A known gap, recorded rather than half-planted.</b> A type-level attribute, a base list
    /// and a generic constraint are all written on the type, so a reference from one has no
    /// enclosing member and <c>MemberReference.From</c> is null. The fixture's only attribute type
    /// is <c>RouteAttribute</c>, which declares no constructor, so its three usages resolve to a
    /// compiler-generated member and land in the count above instead. **Giving it one is not a
    /// free change**: the classifier reads <c>RouteAttribute</c> as evidence of an API boundary, so
    /// the plant has to be an attribute of its own, and attribute-driven invocation is already one
    /// of the member-level traps A9 owes. It belongs with those rather than on its own.
    /// </remarks>
    [Fact]
    public void A_type_level_reference_has_no_source_member_and_the_fixture_has_none()
    {
        var sourceless = core.Model.Types
            .SelectMany(t => t.Members)
            .SelectMany(m => m.Inbound)
            .Count(r => r.From is null);

        Assert.Equal(0, sourceless);
    }
}

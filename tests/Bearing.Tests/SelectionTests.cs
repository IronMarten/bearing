using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// X10's selection — one exemplar per kind that fired, rarest first.
/// </summary>
/// <remarks>
/// <para>
/// <b>X10 asks for this in its own words:</b> <i>"the order must be derived at render time and
/// tested as derived, or it is a constant wearing a sort's clothing."</i> So the assertions here
/// are about the <i>rule</i> and not about the fixture's answer — a test pinning ten names would
/// pass just as happily against a hard-coded list, which is the failure mode being guarded against.
/// The fixture's actual selection is pinned once, by the mosaic's snapshot.
/// </para>
/// <para>
/// Constructed finding sets rather than the fixture wherever a property needs a shape the fixture
/// does not have. <see cref="Finding"/> is public and <see cref="FindingSet.Of"/> takes any
/// sequence, so the cases that matter — one kind, a tie on count, a kind that fired once — can be
/// built directly instead of hoped for.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class SelectionTests(CoreWalkFixture core)
{
    /// <summary>Every kind that fired is represented, and no kind twice.</summary>
    /// <remarks>
    /// The self-scaling property, which is what X10 uses <i>instead of</i> a cap: the number of
    /// items is the number of kinds the run produced, so nothing needs tuning as a codebase grows
    /// and there is no constant to drift. A cap would also silently drop a kind that fired once,
    /// which is precisely the kind A11 round 1 found people most interested in.
    /// </remarks>
    [Fact]
    public void One_exemplar_for_every_kind_that_fired_and_none_for_a_kind_that_did_not()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var exemplars = Selection.Exemplars(findings);

        Assert.Equal(
            findings.All.Select(f => f.Kind).Where(Claims.CompetesForLead).Distinct()
                .OrderBy(k => k.ToString(), StringComparer.Ordinal),
            exemplars.Select(f => f.Kind).OrderBy(k => k.ToString(), StringComparer.Ordinal));

        // And the exclusion is real on this fixture rather than vacuous: the cycle kinds fired,
        // and none of them is here. Without this line the assertion above would still pass if
        // CompetesForLead were `=> true` and the cycle detectors were never wired in.
        Assert.Contains(findings.All, f => !Claims.CompetesForLead(f.Kind));
        Assert.DoesNotContain(exemplars, f => !Claims.CompetesForLead(f.Kind));
    }

    /// <summary>Rarest kind first — ascending count on this run.</summary>
    /// <remarks>
    /// <b>An ordering and never a category.</b> Nothing is labelled <i>rare</i>, so there is no
    /// threshold anywhere in this: what the report says is <i>"ordered by how uncommon each kind is
    /// in this codebase"</i>, and the sentence is true because this sort is what produced it.
    /// </remarks>
    [Fact]
    public void The_order_is_ascending_count_on_this_run()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var counts = Selection.Exemplars(findings)
            .Select(f => findings.OfKind(f.Kind).Count)
            .ToList();

        Assert.Equal(counts.OrderBy(c => c), counts);
    }

    /// <summary>
    /// Each exemplar is its own kind's top row, which is the detector's judgement and not this
    /// selection's.
    /// </summary>
    /// <remarks>
    /// <see cref="FindingSet"/>'s contract is that a detector emits strongest-first and nothing
    /// re-sorts. That contract is the only reason taking the head is meaningful, so it is asserted
    /// here rather than assumed — if a detector ever re-sorted, this selection would start leading
    /// with an arbitrary row and nothing else would notice.
    /// </remarks>
    [Fact]
    public void Each_exemplar_is_its_kinds_first_emitted_finding()
    {
        var findings = Analysis.FindingsFor(core.Model);

        foreach (var exemplar in Selection.Exemplars(findings))
            Assert.Same(findings.OfKind(exemplar.Kind)[0], exemplar);
    }

    /// <summary>
    /// Two kinds firing the same number of times settle by name, every time.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/ARCHITECTURE.md</c> §10's total-key rule, and it bites hardest here.</b> A tie
    /// broken by enum order or by hash would move the <i>lead item of the whole report</i> between
    /// runs on an unchanged codebase — the reader-facing equivalent of A3's representative cycle
    /// moving, and worse, because an acknowledged finding coming back as new is at least visible.
    /// Ties are ordinary: any two kinds that fired once are one.
    /// </remarks>
    [Fact]
    public void A_tie_on_count_is_settled_by_the_kinds_name()
    {
        var set = FindingSet.Of(
        [
            Nomination(FindingKind.SharedMutableState, "z"),
            Nomination(FindingKind.BreaksAlone, "y"),
            Nomination(FindingKind.HubOrGodObject, "x"),
        ]);

        Assert.Equal(
            [FindingKind.BreaksAlone, FindingKind.HubOrGodObject, FindingKind.SharedMutableState],
            Selection.Exemplars(set).Select(f => f.Kind));
    }

    /// <summary>
    /// A kind that fired a thousand times comes last, behind one that fired once.
    /// </summary>
    /// <remarks>
    /// The whole point, stated as a case. On nopCommerce this is method-level concealed decision at
    /// 1,091 against layer span at 1 — <i>"a wall of text"</i> against the section every A11
    /// participant said sounded like the biggest problem to go and look at. Volume is not evidence
    /// of importance and the order says so.
    /// </remarks>
    [Fact]
    public void Volume_does_not_lead()
    {
        var many = Enumerable.Range(0, 50)
            .Select(i => Nomination(FindingKind.ConcealedDecisionType, $"many{i}"));

        var set = FindingSet.Of([.. many, Nomination(FindingKind.SpansArchitecturalLayers, "one")]);

        Assert.Equal(FindingKind.SpansArchitecturalLayers, Selection.Exemplars(set)[0].Kind);
    }

    /// <summary>A run that nominated nothing leads with nothing, rather than with an empty row.</summary>
    [Fact]
    public void An_empty_run_selects_nothing() => Assert.Empty(Selection.Exemplars(FindingSet.Empty));

    private static Finding Nomination(FindingKind kind, string name) =>
        new(new FindingKey(kind, SubjectRef.ForType("Fixture", $"global::{name}")), [], [], []);
}

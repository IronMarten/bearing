using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The finding set, without Roslyn: identity, and the queries suppression will be written
/// against.
/// </summary>
/// <remarks>
/// <para>
/// Suppression is the part of Job B most likely to be lost in extraction and least likely to
/// fail loudly when it is — a suppression that stops working produces <b>more</b> output, and
/// more output reads as a working tool. It stops being ordering only if the query it rests on is
/// correct, so the query is tested here on synthetic input rather than only through a solution
/// that happens to exhibit the case.
/// </para>
/// </remarks>
public sealed class FindingSetTests
{
    private static readonly SubjectRef Normalizer =
        SubjectRef.ForType("App", "global::App.RateNormalizer");

    private static readonly SubjectRef Reconcile =
        SubjectRef.ForMember("App", "global::App.RateNormalizer", "App.RateNormalizer.Reconcile(int)");

    private static readonly SubjectRef Other =
        SubjectRef.ForType("App", "global::App.OrderNormalizer");

    [Fact]
    public void A_set_rejects_two_findings_that_share_one_key()
    {
        // Not merged: two claims an acknowledgment could not tell apart is a defect, and
        // silently keeping one of them is how it would ship.
        var ex = Assert.Throws<ArgumentException>(() => FindingSet.Of(
        [
            Claim(FindingKind.ConcealedDecisionType, Normalizer),
            Claim(FindingKind.ConcealedDecisionType, Normalizer),
        ]));

        Assert.Contains("ConcealedDecisionType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void One_subject_may_carry_different_claims()
    {
        var set = FindingSet.Of(
        [
            Claim(FindingKind.ConcealedDecisionType, Normalizer),
            Claim(FindingKind.BugBlastRadius, Normalizer),
        ]);

        Assert.Equal(2, set.About(Normalizer).Count);
    }

    /// <summary>
    /// <c>docs/DEFECTS.md</c> §15: the primary of the two concealed-decision nominations is at
    /// method level, and it is the one the probe's suppression cannot see.
    /// </summary>
    [Fact]
    public void A_claim_about_a_method_is_a_claim_about_the_type_that_declares_it()
    {
        var set = FindingSet.Of([Claim(FindingKind.ConcealedDecisionMethod, Reconcile)]);

        Assert.True(set.ContainsAbout(FindingKind.ConcealedDecisionMethod, Normalizer));

        // And the walk does not reach sideways: a claim about one type's method says nothing
        // about a different type.
        Assert.False(set.ContainsAbout(FindingKind.ConcealedDecisionMethod, Other));
    }

    /// <summary>
    /// The contrast that makes the test above mean something — an exact query is the probe's
    /// behaviour, and it is the defect.
    /// </summary>
    [Fact]
    public void An_exact_query_does_not_see_the_method_level_claim()
    {
        var set = FindingSet.Of([Claim(FindingKind.ConcealedDecisionMethod, Reconcile)]);

        Assert.False(set.Contains(FindingKind.ConcealedDecisionMethod, Normalizer));
        Assert.True(set.Contains(FindingKind.ConcealedDecisionMethod, Reconcile));
    }

    [Fact]
    public void The_walk_does_not_cross_kinds()
    {
        // A concealed decision on a method must not answer a question about blast radius.
        var set = FindingSet.Of([Claim(FindingKind.ConcealedDecisionMethod, Reconcile)]);

        Assert.False(set.ContainsAbout(FindingKind.BugBlastRadius, Normalizer));
    }

    [Fact]
    public void A_qualifier_the_finding_does_not_carry_does_not_hold()
    {
        // Asking about a qualifier is asking whether a stronger claim may be made. Nothing
        // established it, so the answer is no — not "unknown", and not an exception a renderer
        // would have to remember to handle.
        Assert.False(Claim(FindingKind.ConcealedDecisionType, Normalizer)
            .Holds(Qualifiers.LowAbsoluteConnectivity));
    }

    [Fact]
    public void Receipts_are_readable_by_name()
    {
        var finding = new Finding(
            new FindingKey(FindingKind.ConcealedDecisionType, Normalizer),
            [Receipt.Gated("FanIn", 5, nameof(AnalysisPolicy.MinFanIn))],
            [],
            []);

        Assert.Equal(5, finding.ValueOf("FanIn"));
        Assert.Null(finding.ValueOf("FanOut"));
    }

    [Fact]
    public void An_empty_set_answers_every_query()
    {
        Assert.Empty(FindingSet.Empty.All);
        Assert.Empty(FindingSet.Empty.OfKind(FindingKind.ConcealedDecisionType));
        Assert.Empty(FindingSet.Empty.About(Normalizer));
        Assert.False(FindingSet.Empty.ContainsAbout(FindingKind.ConcealedDecisionType, Normalizer));
    }

    private static Finding Claim(FindingKind kind, SubjectRef subject) =>
        new(new FindingKey(kind, subject), [], [], []);
}

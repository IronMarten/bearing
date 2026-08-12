using ArchProbe;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Core's concealed-decision nominations against the probe's, on the fixture.
/// </summary>
/// <remarks>
/// <para>
/// The first findings to move, and the extraction gate applied to them: <c>OracleGoldenTests</c>
/// asks whether the probe's bytes moved, and this asks whether the reimplementation agrees with
/// it. Core is a rewrite — it reads a structure model rather than flat accumulators, and it
/// decides the plumbing wording on the model rather than inside a <c>WriteLine</c> — so
/// agreement is a result and not a tautology.
/// </para>
/// <para>
/// The probe is rendered with its display cap lifted. Core does not truncate: a model that drops
/// findings leaves every renderer unable to say how much it is not showing
/// (<c>docs/DEFECTS.md</c> §3), and in the probe the cap silently weakens suppression too,
/// because the set breaks-alone tests membership against is the truncated one. Comparing against
/// an untruncated render is comparing the two populations rather than one against a prefix of
/// the other.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class FindingEquivalenceTests(CoreWalkFixture core, FixtureRun probe)
{
    /// <summary>The probe's cap, lifted past anything the fixture can produce.</summary>
    private static Options Uncapped => new() { Top = 500 };

    [Fact]
    public void Method_level_concealed_decisions_are_the_probes()
    {
        var expected = ProbeNominations("-- CONCEALED DECISION, METHOD LEVEL");
        var actual = CoreNominations(FindingKind.ConcealedDecisionMethod);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
    }

    [Fact]
    public void Type_level_concealed_decisions_are_the_probes()
    {
        var expected = ProbeNominations("-- CONCEALED DECISION -");
        var actual = CoreNominations(FindingKind.ConcealedDecisionType);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
    }

    /// <summary>
    /// Method level is not a drill-down of type level: it reaches components type level does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CARRY-FORWARD.md</c> §6, on the fixture. Seven of the twelve types nominated at method
    /// level are not nominated at type level — <c>TariffCalculator</c>, <c>ShipmentLedger</c> and
    /// the reconcilers among them — because a type whose total complexity is ordinary can still
    /// hide one branching method, and rolling up to the type averages it away.
    /// </para>
    /// <para>
    /// <c>TECHREQ-job-b.md</c> §3.3 requires extraction not to demote this to a drill-down of
    /// §3.2. Doing so would empty seven of these twelve, and nothing else in this file would
    /// notice: the remaining five would still agree with the probe.
    /// </para>
    /// </remarks>
    [Fact]
    public void Method_level_reaches_components_type_level_misses()
    {
        var findings = Analysis.FindingsFor(core.Model);

        var byMethod = findings.OfKind(FindingKind.ConcealedDecisionMethod)
            .Select(f => f.Subject.DeclaringType!.Canonical)
            .ToHashSet(StringComparer.Ordinal);
        var byType = findings.OfKind(FindingKind.ConcealedDecisionType)
            .Select(f => f.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(7, byMethod.Except(byType, StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Blast radius agrees with the probe, <b>including where the gate was replaced</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core does not implement the probe's <c>FanInPctl &gt;= 95</c>. It gates on rank within the
    /// cohort instead, because the percentile form is unsatisfiable below a cohort of ten
    /// (<c>docs/DEFECTS.md</c> §14). Agreement here is therefore a result about the replacement:
    /// <c>rank ≤ 0.05n + 0.5</c> and <c>pctl ≥ 95</c> are the same condition wherever the latter
    /// could be met, so the only cohorts where the two can disagree are the ones the probe could
    /// never nominate from.
    /// </para>
    /// <para>
    /// <b>And on this fixture they do not disagree at all</b> — which is worth stating plainly,
    /// because it means the repair is not observed here. The stranded cohorts contain types that
    /// now clear the rank gate (<c>NormalizationContext</c> at rank 1 of eight, <c>RawResponse</c>
    /// at rank 2) and every one of them fails blast radius on complexity instead. See
    /// <c>FixtureCoverageTests</c>: the plant that would observe it is still owed.
    /// </para>
    /// </remarks>
    [Fact]
    public void Blast_radius_nominations_are_the_probes()
    {
        var expected = ProbeNominations("-- BUG BLAST RADIUS");
        var actual = CoreTypeNominations(FindingKind.BugBlastRadius);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
    }

    [Fact]
    public void Load_bearing_nominations_are_the_probes()
    {
        var expected = ProbeNominations("-- LOAD-BEARING AND INTRICATE");
        var actual = CoreTypeNominations(FindingKind.LoadBearingAndIntricate);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
    }

    /// <summary>
    /// One type is nominated as both, and that is the design rather than a double-count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PRD-free-tier.md</c> §7.2 settles that blast radius and load-bearing-and-intricate are
    /// two findings rather than one, against the alternative reading that the probe had split a
    /// single message in two. They overlap on <i>"widely depended on and complex"</i> and diverge
    /// on what they claim: how far a defect propagates, judged against peers, versus how
    /// insulated a type is, judged in absolute terms with no cohort at all.
    /// </para>
    /// <para>
    /// <c>ShipmentLedger</c> is where the decision becomes observable. Unlike breaks-alone and
    /// concealed decision — which contradict each other, and where saying both discredits both —
    /// nothing suppresses this pair, so a suppression pass that ever removed one of these two
    /// would be enforcing a merge nobody agreed to. The assertion is what makes that loud.
    /// </para>
    /// <para>
    /// The two receipts below are the divergence itself: the cohort-relative reading exists only
    /// on one of them, and the instability that carries the whole of the other's claim exists
    /// only on the other.
    /// </para>
    /// </remarks>
    [Fact]
    public void Both_findings_may_be_made_about_one_type()
    {
        var ledger = core.Model.Types.Single(t => t.Name == "ShipmentLedger");
        var about = Analysis.FindingsFor(core.Model).About(ledger.Subject).ToList();

        var blast = Assert.Single(about, f => f.Kind == FindingKind.BugBlastRadius);
        var bearing = Assert.Single(about, f => f.Kind == FindingKind.LoadBearingAndIntricate);

        Assert.NotNull(blast.ValueOf("FanInRank"));
        Assert.Null(blast.ValueOf("Instability"));

        Assert.NotNull(bearing.ValueOf("Instability"));
        Assert.Null(bearing.ValueOf("FanInRank"));
        Assert.Null(bearing.ValueOf("CohortSize"));
    }

    // ------------------------------------------------------- the rules, on the model ----

    /// <summary>
    /// Row 6 of the suppression matrix, asserted against the model rather than against wording.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the row that suppresses a <i>sentence</i> rather than a finding, and until now the
    /// only thing that could be tested was the probe's prose — <c>SuppressionTests</c> says so in
    /// as many words. That absence was itself part of what extraction had to fix: a rule enforced
    /// in a renderer is a rule that does not exist, and the JSON and HTML renderers would each
    /// have had to remember it.
    /// </para>
    /// <para>
    /// ThroughputGauge is the case: fan-in 5, which is also its cohort median. Relative says
    /// unremarkable, absolute says five callers depend on it, and only one of those two readings
    /// can be put in front of a developer without being laughed at.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_plumbing_claim_is_decided_on_the_model()
    {
        var gauge = TypeLevel("ThroughputGauge");
        var reconciler = TypeLevel("RateReconciler");

        // Both are nominated — the qualifier decides the sentence, not the finding.
        Assert.False(gauge.Holds(Qualifiers.LowAbsoluteConnectivity));
        Assert.True(reconciler.Holds(Qualifiers.LowAbsoluteConnectivity));

        // And it is the absolute floor that separates them, not the relative one they share.
        Assert.True(gauge.ValueOf("FanIn") >= core.Model.Policy.MinFanIn);
        Assert.True(reconciler.ValueOf("FanIn") < core.Model.Policy.MinFanIn);
        Assert.True(gauge.ValueOf("FanInXMedian") <= core.Model.Policy.ConcealedFanInCeiling);
        Assert.True(reconciler.ValueOf("FanInXMedian") <= core.Model.Policy.ConcealedFanInCeiling);
    }

    /// <summary>
    /// Row 7: no peer group, no relative claim. Invariants 6 and 8.
    /// </summary>
    /// <remarks>
    /// PricingVault is planted so the cohort floor is the only thing between it and a nomination.
    /// Every other condition is asserted here from Core's own numbers, so absence can only be the
    /// gate — without that half, a type that quietly stopped qualifying for an unrelated reason
    /// would keep this test passing.
    /// <para>
    /// The probe's companion test moves the floor and watches the finding come back. That control
    /// cannot be run against Core here, because cohort <i>assignment</i> reads
    /// <c>MinCohort</c> during the walk: a different floor is a different set of peer groups, so
    /// it needs a second walk rather than a second render. <c>SuppressionTests</c> holds the
    /// moving-threshold half on the oracle until that walk is cheap enough to run twice.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_type_below_the_cohort_floor_makes_no_relative_claim()
    {
        var vault = core.Model.Types.Single(t => t.Name == "PricingVault");
        var policy = core.Model.Policy;

        Assert.True(vault.CohortSize < policy.MinCohort);
        Assert.True(vault.MaxMemberCyclomatic >= policy.MinDecisionCc);

        Assert.DoesNotContain(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.ConcealedDecisionType),
            f => f.Subject == vault.Subject);
    }

    // ------------------------------------------------------------------- discipline ----

    /// <summary>
    /// Every threshold a finding cites has to be a value on the policy object.
    /// </summary>
    /// <remarks>
    /// The receipt names the gate rather than copying its number, so that a finding and the
    /// policy cannot disagree about what it was tested against. A gate name that resolves to
    /// nothing would make "which policy produced this finding" unanswerable — the exact failure
    /// <c>AnalysisPolicy</c> exists to prevent.
    /// </remarks>
    [Fact]
    public void Every_gate_a_finding_cites_is_a_named_policy_value()
    {
        var named = core.Model.Policy.Values.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        var cited = Analysis.FindingsFor(core.Model).All
            .SelectMany(f => f.Receipts.Select(r => r.Gate).Concat(f.Qualifiers.Select(q => q.Gate)))
            .Where(gate => gate is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(cited);
        Assert.Empty(cited.Except(named, StringComparer.Ordinal));
    }

    /// <summary>
    /// The emitted order is a total one.
    /// </summary>
    /// <remarks>
    /// Ranking alone reproduces on one machine without being a property of the tool: outlier
    /// factors tie constantly, and a stable sort over a tied group preserves whatever order the
    /// walk arrived in — which is project load order times Roslyn's symbol order.
    /// <c>docs/TESTING.md</c> §5 and <c>OrderingTests</c> record what that cost the probe.
    /// <para>
    /// <b>What this proves and what it does not.</b> It proves the emitted sequence satisfies
    /// rank-descending-then-identity, and it fails if the rank sort is dropped or inverted. It
    /// does not prove the identity tiebreak is doing the work: the model arrives identity-ordered
    /// and the sort is stable, so a tie group would be in identity order with or without it. See
    /// <c>FixtureCoverageTests</c>, which records that and why it will not stay harmless.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(FindingKind.ConcealedDecisionMethod, "CyclomaticXMedian")]
    [InlineData(FindingKind.ConcealedDecisionType, "MaxMemberCyclomaticXMedian")]
    public void Findings_are_emitted_in_a_total_order(FindingKind kind, string rank)
    {
        var findings = Analysis.FindingsFor(core.Model).OfKind(kind);
        Assert.True(findings.Count > 1, "a single finding cannot exercise an ordering");

        for (var i = 1; i < findings.Count; i++)
        {
            var (previous, current) = (findings[i - 1], findings[i]);
            var (before, after) = (previous.ValueOf(rank)!.Value, current.ValueOf(rank)!.Value);

            Assert.True(before >= after, $"{previous} ranks below {current}");
            if (before != after) continue;

            Assert.True(
                string.CompareOrdinal(previous.Subject.Canonical, current.Subject.Canonical) < 0,
                $"{previous} and {current} tie on {rank} and are not in identity order");
        }
    }

    /// <summary>
    /// The fixture ties, so the tiebreak above is exercised rather than merely present.
    /// </summary>
    /// <remarks>
    /// Two pairs of reconcilers tie at method level — 4.333 and 3.667 times their peer median —
    /// and a tie group is where a non-total sort key hides. Without one, deleting the
    /// <c>ThenBy</c> on identity would change nothing and the ordering test would still pass.
    /// Only method level ties: the type-level gap is recorded in <c>FixtureCoverageTests</c>.
    /// </remarks>
    [Fact]
    public void The_fixture_ties_method_level_findings()
    {
        var ranks = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.ConcealedDecisionMethod)
            .Select(f => f.ValueOf("CyclomaticXMedian"))
            .ToList();

        Assert.Equal(2, ranks.Count - ranks.Distinct().Count());
    }

    // --------------------------------------------------------------------- adapters ----

    /// <summary>
    /// The subjects the probe nominates under a section header, as <c>Type.Member</c>.
    /// </summary>
    /// <remarks>
    /// Only the subject names are read, never the sentence around them. Sorted rather than
    /// compared in sequence: the probe orders by outlier factor with its own tiebreak, Core
    /// breaks ties on identity, and which of the two orderings a tied pair lands in is not what
    /// equivalence is about. The order Core emits is asserted separately.
    /// </remarks>
    private List<string> ProbeNominations(string header) =>
        NominationText.SubjectsUnder(NominationText.Render(probe.Result, Uncapped), header)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Type-subject findings, named the way the probe names them — the type alone.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Label"/>, which appends the member a concealed-decision sentence
    /// is about. Blast radius and load-bearing claim something about the type itself, so the
    /// member is a participant rather than part of the subject.
    /// </remarks>
    private List<string> CoreTypeNominations(FindingKind kind) =>
        Analysis.FindingsFor(core.Model)
            .OfKind(kind)
            .Select(f => core.Model.Find(f.Subject)!.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

    private List<string> CoreNominations(FindingKind kind) =>
        Analysis.FindingsFor(core.Model)
            .OfKind(kind)
            .Select(Label)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A finding as the probe would name its subject: the type, then the member the claim is
    /// about.
    /// </summary>
    private string Label(Finding finding)
    {
        if (finding.Subject.Kind == SubjectKind.Member)
        {
            var owner = core.Model.Find(finding.Subject.DeclaringType!)!;
            var member = owner.Members.Single(m => m.Subject == finding.Subject);
            return $"{owner.Name}.{member.Name}";
        }

        var type = core.Model.Find(finding.Subject)!;
        return $"{type.Name}.{type.MostComplexMember!.Name}";
    }

    private Finding TypeLevel(string typeName)
    {
        var type = core.Model.Types.Single(t => t.Name == typeName);
        return Assert.Single(
            Analysis.FindingsFor(core.Model).About(type.Subject),
            f => f.Kind == FindingKind.ConcealedDecisionType);
    }
}

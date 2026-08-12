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

    // ----------------------------------------------------- breaks alone, and §4's rows ----

    /// <summary>
    /// Breaks alone disagrees with the probe, by exactly the two types <c>DEFECTS.md</c> §15 named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first deliberate behaviour change in the findings layer.</b> Every other finding
    /// that has moved agrees with the probe byte-for-byte on the fixture; this one must not. §15:
    /// the probe's concealed-decision exclusion reads a set of <b>type-level</b> nominations, and
    /// §3.3 — the primary of the two, the one that found the right thing on real code — is
    /// invisible to it. So the report says <i>"this method is making business judgements"</i> and
    /// <i>"if it breaks, it breaks alone"</i> about one component.
    /// </para>
    /// <para>
    /// <c>MethodReconciler</c> and <c>TariffReconciler</c> are both nominated at method level and
    /// neither at type level, so both are told they break alone by the probe and neither is by
    /// Core. <c>RoutingDepot</c> is the control: nominated at neither level, and it survives in
    /// both. Asserting the difference as a set rather than asserting Core's output alone is what
    /// makes this a statement about the fix — an unrelated regression that emptied the finding
    /// would satisfy "Core says only RoutingDepot" just as well.
    /// </para>
    /// </remarks>
    [Fact]
    public void Breaks_alone_diverges_from_the_probe_by_exactly_the_defect_fifteen_fix()
    {
        var probeSaid = ProbeNominations("-- BREAKS ALONE");
        var coreSays = CoreTypeNominations(FindingKind.BreaksAlone);

        Assert.Equal(
            ["MethodReconciler", "RoutingDepot", "SurchargeEvaluator", "TariffReconciler"],
            probeSaid);
        Assert.Equal(["RoutingDepot", "SurchargeEvaluator"], coreSays);

        // The difference is entirely the fix, in one direction only: Core removes two claims and
        // adds none. A suppression is allowed to silence, never to nominate.
        Assert.Equal(
            ["MethodReconciler", "TariffReconciler"],
            probeSaid.Except(coreSays, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(coreSays.Except(probeSaid, StringComparer.Ordinal));

        // And it is the method-level nomination doing it, which is the whole of §15. Neither is
        // nominated at type level, so the probe's query could not have found them however it was
        // ordered.
        var detected = Analysis.Detected(core.Model);
        foreach (var name in (string[])["MethodReconciler", "TariffReconciler"])
        {
            var subject = core.Model.Types.Single(t => t.Name == name).Subject;

            Assert.True(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, subject));
            Assert.False(detected.Contains(FindingKind.ConcealedDecisionType, subject));
        }
    }

    /// <summary>
    /// Every implemented suppression row silences something, and each one has a case of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TECHREQ-job-b.md</c> §4's second structural requirement: a suppression that stops
    /// working produces <i>more</i> output, which reads as a working tool, so a row that silences
    /// nothing is a row nothing can fail on.
    /// </para>
    /// <para>
    /// <b>Row 3 was exactly that until the Evaluator cohort was planted.</b> Both types that
    /// reached breaks alone with no callers were taken first — <c>ShipmentController</c> by the
    /// boundary row, <c>AuditReconciler</c> by the concealed-decision row — so
    /// <c>breaks-alone-is-unreferenced</c> could be deleted outright with the suite green.
    /// <c>DetentionEvaluator</c> is unreferenced, is not a boundary, and is not a concealed
    /// decision, so it is the first type only row 3 can reach.
    /// </para>
    /// <para>
    /// The matrix order decides which reason is reported when rows overlap, and it is §4's order
    /// rather than a convenience. Reordering to change an attribution would be choosing the
    /// answer to suit the suite.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_suppression_row_silences_something()
    {
        var detected = Analysis.Detected(core.Model);

        var silenced = detected.All
            .Select(f => Suppression.Silencing(f, detected, core.Model))
            .Where(rule => rule is not null)
            .Select(rule => rule!.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            Suppression.Rules.Select(r => r.Name).ToHashSet(StringComparer.Ordinal),
            silenced);

        // And row 3 has a case no earlier row would have taken, which is what makes it a gate
        // rather than a comment. Without this the set above is satisfied by overlap alone.
        Assert.Equal("breaks-alone-is-unreferenced", SilencingRuleFor("DetentionEvaluator"));

        // Detection and suppression are separate passes, so every silenced finding was really
        // made and then withdrawn. If a detector ever absorbs one of these rules the two sets
        // become equal and this fails.
        Assert.True(detected.Count > Analysis.FindingsFor(core.Model).Count);
    }

    /// <summary>
    /// Row 1 does not depend on fan-in, and row 3 does not depend on the architectural role.
    /// </summary>
    /// <remarks>
    /// The two rows overlap on <c>ShipmentController</c> — a boundary that is also unreferenced —
    /// and an overlap is where a redundant rule hides. Each therefore needs a case only it
    /// catches: <c>ReconciliationController</c> is a boundary with a caller, and
    /// <c>AuditReconciler</c> is unreferenced and not a boundary. Without these two, either row
    /// could be deleted and the surviving set would not move.
    /// </remarks>
    [Fact]
    public void The_boundary_and_unreferenced_rows_are_not_the_same_rule()
    {
        var boundaryWithCallers = core.Model.Types.Single(t => t.Name == "ReconciliationController");
        Assert.Equal("ApiBoundary", boundaryWithCallers.Classification.Kind);
        Assert.True(boundaryWithCallers.FanIn >= core.Model.Policy.BreaksAloneMinFanIn);
        Assert.Equal("breaks-alone-at-a-boundary", SilencingRuleFor(boundaryWithCallers.Name));

        // And the case row 3 would catch alone does not exist: AuditReconciler is unreferenced
        // and not a boundary, but it is a concealed decision, so row 2 reaches it first.
        var unreferencedNonBoundary = core.Model.Types.Single(t => t.Name == "AuditReconciler");
        Assert.NotEqual("ApiBoundary", unreferencedNonBoundary.Classification.Kind);
        Assert.Equal(0, unreferencedNonBoundary.FanIn);
        Assert.Equal("breaks-alone-decides-something", SilencingRuleFor(unreferencedNonBoundary.Name));
    }

    /// <summary>
    /// The control for §15 exists only because <c>DEFECTS.md</c> §10 is still live.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>RoutingDepot</c> is the type that survives breaks alone, and it survives because its
    /// cohort is three. §10: breaks alone runs over all types while its concealed-decision
    /// exclusion reads a cohort-gated population, so a small peer group drops a type out of
    /// concealed decision and straight into breaks alone. Core inherits that — the exclusion is a
    /// suppression row now, but the nomination it searches for is still never made.
    /// </para>
    /// <para>
    /// <b>This is worth pinning because fixing §10 costs the §15 control.</b> The day a
    /// below-floor type can be nominated as a concealed decision, <c>RoutingDepot</c> leaves
    /// breaks alone, the finding empties on this fixture, and the divergence test above starts
    /// asserting an absence rather than a difference. A replacement control has to be planted in
    /// the same change, not discovered afterwards.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_surviving_control_survives_because_of_a_different_live_defect()
    {
        var depot = core.Model.Types.Single(t => t.Name == "RoutingDepot");
        var policy = core.Model.Policy;

        Assert.True(depot.CohortSize < policy.MinCohort);
        Assert.True(depot.MaxMemberCyclomatic >= policy.MinDecisionCc);

        // Every condition for a concealed decision except a viable peer group, so the suppression
        // finds nothing to suppress with.
        var detected = Analysis.Detected(core.Model);
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionType, depot.Subject));
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, depot.Subject));
        Assert.Null(SilencingRuleFor("RoutingDepot"));
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

    /// <summary>
    /// The row that silenced this type's breaks-alone claim, or null if the claim stands.
    /// </summary>
    private string? SilencingRuleFor(string typeName)
    {
        var detected = Analysis.Detected(core.Model);
        var subject = core.Model.Types.Single(t => t.Name == typeName).Subject;
        var finding = Assert.Single(detected.About(subject), f => f.Kind == FindingKind.BreaksAlone);

        return Suppression.Silencing(finding, detected, core.Model)?.Name;
    }

    private Finding TypeLevel(string typeName)
    {
        var type = core.Model.Types.Single(t => t.Name == typeName);
        return Assert.Single(
            Analysis.FindingsFor(core.Model).About(type.Subject),
            f => f.Kind == FindingKind.ConcealedDecisionType);
    }
}

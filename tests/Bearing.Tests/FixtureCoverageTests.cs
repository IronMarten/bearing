using ArchProbe;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// What the fixture does <b>not</b> cover, asserted rather than assumed.
/// </summary>
/// <remarks>
/// Invariant 8 says silence must never read as a clean bill of health. That applies to this
/// suite as much as to the tool: a green run says every assertion held, not that everything
/// is checked. These tests name the holes so they stay visible, and fail the day a hole is
/// filled — at which point the known answer gets recorded in <c>docs/TESTING.md</c> §6 and
/// the assertion here is narrowed.
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class FixtureCoverageTests(FixtureRun run, CoreWalkFixture core)
{
    /// <summary>
    /// Every type nominated as a concealed decision at type level is also nominated at method
    /// level, so type level adds no subject of its own on this fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interesting direction is covered — <c>FindingEquivalenceTests</c> asserts that seven
    /// types are found at method level and nowhere else, which is the reason §3.3 is the primary
    /// of the two. This is the direction that is not: nothing here would notice if the type-level
    /// nomination were reduced to a filter over the method-level one, because on this fixture
    /// that is what it looks like.
    /// </para>
    /// <para>
    /// The gap is in the fixture, not in the finding. A type whose complexity is spread evenly
    /// across several ordinary-looking methods is exactly the case type level exists for, and
    /// TestBed has none: its complex types all concentrate it in one method. Planting one closes
    /// this and fails this test, which is the event worth seeing.
    /// </para>
    /// </remarks>
    [Fact]
    public void Type_level_concealed_decision_adds_no_subject_of_its_own()
    {
        var findings = Analysis.FindingsFor(core.Model);

        var byMethod = findings.OfKind(FindingKind.ConcealedDecisionMethod)
            .Select(f => f.Subject.DeclaringType!.Canonical)
            .ToHashSet(StringComparer.Ordinal);
        var byType = findings.OfKind(FindingKind.ConcealedDecisionType)
            .Select(f => f.Subject.Canonical);

        Assert.Empty(byType.Except(byMethod, StringComparer.Ordinal));
    }

    /// <summary>
    /// No two type-level nominations tie, so their ordering tiebreak is never exercised.
    /// </summary>
    /// <remarks>
    /// The five nominations sit at 12, 8, 5, 4.333 and 3.5 times their peer median, so the sort
    /// is decided entirely by rank and the <c>ThenBy</c> on identity could be deleted without
    /// failing anything. Method level does tie, twice, which is what currently covers the
    /// tiebreak at all — but a tiebreak covered on one finding and not the other is one
    /// reimplementation away from being covered on neither.
    /// </remarks>
    [Fact]
    public void Type_level_concealed_decisions_never_tie()
    {
        var ranks = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.ConcealedDecisionType)
            .Select(f => f.ValueOf("MaxMemberCyclomaticXMedian"))
            .ToList();

        Assert.Equal(ranks.Count, ranks.Distinct().Count());
    }

    /// <summary>
    /// The identity tiebreak on a finding's order is correct and <b>not independently
    /// observable</b>. Deleting it changes nothing that any test can see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things conspire. <c>SolutionModel.Types</c> arrives ordered by identity, and LINQ's
    /// sort is stable — so a tie group is already in identity order before the tiebreak runs.
    /// Removing the <c>ThenBy</c> from <c>ConcealedDecision</c> leaves the emitted order
    /// byte-identical, which was confirmed by removing it.
    /// </para>
    /// <para>
    /// This is the failure <c>OrderingTests</c> exists for, one level up: the probe's writers
    /// also reproduced perfectly while sorting on non-total keys, and it took shuffling the input
    /// to see it. That shuffle is not available here — a <c>SolutionModel</c> can only be
    /// produced by a walk, so there is no permuted one to render from — and until it is, the
    /// tiebreak is a correctness argument rather than a tested property.
    /// </para>
    /// <para>
    /// It matters because it will not stay true. A detector that reads
    /// <c>TypeNode.Members</c> — declaration order, not identity order — or one that groups
    /// before it ranks, inherits no such guarantee, and it would emit a walk-order tie group
    /// with nothing failing. The assertion below pins the property this currently rests on, so
    /// the day the model stops arriving sorted, that shows up here rather than as an unstable
    /// artifact.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_finding_order_currently_rests_on_the_model_arriving_sorted()
    {
        var identityOrder = core.Model.Types
            .Select(t => t.Subject.Canonical)
            .Order(StringComparer.Ordinal);

        Assert.Equal(identityOrder, core.Model.Types.Select(t => t.Subject.Canonical));
    }

    /// <summary>
    /// BUG BLAST RADIUS and BREAKS ALONE both nominate something now. Neither did before, and
    /// the goldens carried no record of how either behaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was the important consequence, and it was not obvious: a section that emits no
    /// output produces the same bytes whatever its thresholds are. Breaks alone's <c>0.8</c>
    /// instability floor and <c>1</c> fan-in floor could have been changed to any other value,
    /// or the finding deleted outright, and <c>OracleGoldenTests</c> would still have passed
    /// byte-for-byte.
    /// </para>
    /// <para>
    /// Breaks alone was the worse of the two: it carries three of Job B's seven suppression
    /// rules (<c>TECHREQ-job-b.md</c> §4), including the invariant-4 boundary exclusion and the
    /// invariant-3 concealed-decision exclusion, and nothing failed if one was removed because
    /// removing it changed empty output into empty output. <c>SuppressionTests</c> covers those
    /// three rows now.
    /// </para>
    /// <para>
    /// All seven suppression rows are now addressed. Six have behavioural tests in
    /// <c>SuppressionTests</c>, each with a control that moves the threshold rather than merely
    /// asserting an absence. Row 5 has none and cannot: the widest-contract-surface suppression
    /// is unreachable at every boundary count, proved in <c>KnownDefectTests</c>.
    ///
    /// Three defects came out of writing them, all pinned: the cohort floor stripping row 2's
    /// suppression off breaks alone, the layer-span collapse hiding the anomaly it shares a
    /// signature with, and row 5 itself.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_two_silent_findings_now_have_fixture_cases()
    {
        var text = NominationText.Render(run.Result, run.Options);

        // Both were empty, and an empty section produces the same bytes whatever its thresholds
        // are. Asserted positively so that filling these gaps cannot quietly un-fill itself.
        Assert.Equal(["ShipmentLedger"], NominationText.SubjectsUnder(text, "-- BUG BLAST RADIUS"));
        Assert.Contains("TariffReconciler", NominationText.SubjectsUnder(text, "-- BREAKS ALONE"));

        // Asserted alongside so this reads as a gap in two findings rather than a fact about
        // two arbitrary strings. If these ever empty out, the parser broke, not the tool.
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CONCEALED DECISION -"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CONCEALED DECISION, METHOD LEVEL"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- CHANGE COST"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- LOAD-BEARING AND INTRICATE"));
        Assert.NotEmpty(NominationText.SubjectsUnder(text, "-- SHARED MUTABLE STATE"));
    }

    /// <summary>
    /// Firing is not the same as being gated: five of Core's new conditions are unobserved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The plant made blast radius produce output; it did not make its gates testable.</b>
    /// Twenty-one mutations were run over §3.4 and §3.6 as they moved into Core. Sixteen failed a
    /// test. These five did not, and each one can be deleted today with all 220 tests green:
    /// </para>
    /// <list type="number">
    ///   <item><description>blast radius' <c>FanIn &gt;= MinFanIn</c> — <b>invariant 1's canonical
    ///     gate</b>, the one whose absence produced the original cry-wolf failure;</description></item>
    ///   <item><description>blast radius' <c>FanInXMedian &gt;= 2.0</c>;</description></item>
    ///   <item><description>blast radius' cohort floor — row 7 of the suppression matrix;</description></item>
    ///   <item><description>load-bearing reading <b>effective</b> rather than raw fan-out — the
    ///     dependency-inversion exclusion of <c>SESSION-NOTES.md</c> #22;</description></item>
    ///   <item><description>the identity tiebreak in <c>Nomination</c>, already known unobservable
    ///     and re-confirmed above.</description></item>
    /// </list>
    /// <para>
    /// <b>Why the golden does not cover these the way it covers the probe's.</b>
    /// <c>The_blast_radius_plant_observes_the_fan_in_floor</c> reasons that the <c>2.0</c> and the
    /// percentile floors are pinned because changing them changes
    /// <c>golden/nominations.verified.txt</c>. That is true of the literals <i>in the probe</i>,
    /// which renders the golden. Core renders nothing yet, so its re-implementation of the same
    /// gate is held only by <c>FindingEquivalenceTests</c> — and a gate that is redundant on this
    /// fixture can be dropped from Core without moving Core's nomination set. The two
    /// implementations are protected by different things, and only one of them is protected here.
    /// </para>
    /// <para>
    /// What each needs is a plant, and the assertions below are the facts that make them
    /// redundant, so filling any gap fails this test rather than silently closing it. Add, do not
    /// reshape, and record the known answer in <c>docs/TESTING.md</c> §6.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_new_findings_have_gates_the_fixture_cannot_observe()
    {
        var policy = core.Model.Policy;

        // 1-3. One nomination, clearing every gate at once, so no gate is the deciding one. Its
        // cohort is twelve, which is also why the rank repair is invisible: the percentile form
        // it replaced was satisfiable here.
        var ledger = core.Model.Types.Single(t => t.Name == "ShipmentLedger");
        Assert.Equal(11, ledger.FanIn);
        Assert.True(ledger.FanIn >= policy.MinFanIn);
        Assert.True(ledger.CohortSize >= 10);

        // 4. Neither load-bearing nominee depends on an abstraction, so excluding abstractions
        // subtracts nothing. The controlled pair that proves the exclusion discriminates lives in
        // SESSION-NOTES.md #22 and has never been in this suite.
        foreach (var name in (string[])["ShipmentLedger", "TariffCalculator"])
        {
            var type = core.Model.Types.Single(t => t.Name == name);
            Assert.Equal(type.FanOut, type.EffectiveFanOut);
            Assert.Equal(type.InstabilityRaw, type.Instability);
        }

        // And the null-instability guard is a different case from the four above: it is not
        // merely unobserved, it is unreachable. Instability is null only when FanIn and
        // EffectiveFanOut are both zero, which fails the fan-in floor two lines later. It stays
        // because MinFanIn is a policy value and could be set to zero.
        Assert.All(
            core.Model.Types.Where(t => t.Instability is null),
            t => Assert.True(t.FanIn < policy.MinFanIn));
    }

    /// <summary>
    /// ~~Breaks alone's own gates are masked by its suppressions.~~ <b>Filled — the instability
    /// gate is observable now.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Instability &gt;= 0.8</c> is the <i>isolated</i> in "complex inside but isolated";
    /// without it the finding claims nothing more than "complex". It could be deleted with the
    /// whole suite green, because every type it held back was <i>also</i> a concealed decision
    /// and suppression row 2 removed each one before the difference could show. A suppression was
    /// masking the detector beneath it.
    /// </para>
    /// <para>
    /// <c>LaneEvaluator</c> ends that: complex, referenced, not a boundary, and not a concealed
    /// decision, so the instability gate is the only thing between it and a nomination. Remove
    /// the gate and the tool says a type with two callers breaks alone. The assertions below are
    /// each of the other conditions, so an unrelated change that stopped it qualifying cannot
    /// leave this passing.
    /// </para>
    /// <para>
    /// <b>The failure it now prevents is concrete.</b> <c>ShipmentLedger</c> has fan-in 11, the
    /// most depended-on type in the fixture, and is nominated as both a bug blast radius and
    /// load-bearing-and-intricate. Without the gate the same run also tells the reader that if it
    /// breaks, it breaks alone — invariant 3's exact failure. That is asserted here too, because
    /// it is the reason the gate is worth a plant rather than a comment.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_instability_gate_is_the_only_thing_holding_back_a_connected_type()
    {
        var policy = core.Model.Policy;
        var detected = Analysis.Detected(core.Model);
        var lane = core.Model.Types.Single(t => t.Name == "LaneEvaluator");

        // Every condition for breaks alone except the one being protected.
        Assert.True(lane.MaxMemberCyclomatic >= policy.HighCc);
        Assert.True(lane.FanIn >= policy.BreaksAloneMinFanIn);
        Assert.Equal("Internal", lane.Classification.Kind);
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionType, lane.Subject));
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, lane.Subject));

        // And the gate itself, which is therefore the whole of why it is silent.
        Assert.True(lane.Instability < policy.IsolatedThreshold);
        Assert.False(detected.Contains(FindingKind.BreaksAlone, lane.Subject));

        // Its peer group is what makes it unremarkable: six evaluators of comparable complexity,
        // so no member is an outlier against the others. That is the property the plant supplies
        // and the reason no such case existed before.
        Assert.True(lane.CohortSize >= policy.MinCohort);

        // The contradiction the gate prevents, stated on the type it would be worst about.
        var ledger = core.Model.Types.Single(t => t.Name == "ShipmentLedger");
        var about = Analysis.FindingsFor(core.Model).About(ledger.Subject).Select(f => f.Kind).ToList();
        Assert.Contains(FindingKind.BugBlastRadius, about);
        Assert.Contains(FindingKind.LoadBearingAndIntricate, about);
        Assert.DoesNotContain(FindingKind.BreaksAlone, about);
    }

    /// <summary>
    /// ~~Breaks alone's own gates are masked by its suppressions.~~ Superseded — kept for the
    /// four types the instability gate holds back, which is still worth pinning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine more mutations when §3.7 and three of §4's rows moved into Core. Seven failed. Two
    /// did not, and the second is the one that matters.
    /// </para>
    /// <para>
    /// <b>1. Row 3, <c>breaks-alone-is-unreferenced</c>, silences nothing.</b> Two types reach
    /// the finding with no callers: <c>ShipmentController</c>, which row 1 takes as a boundary,
    /// and <c>AuditReconciler</c>, which row 2 takes as a concealed decision. The row needs a
    /// plant of its own — unreferenced, not a boundary, not a concealed decision.
    /// </para>
    /// <para>
    /// <b>2. The instability gate can be deleted and no output moves.</b> This is not a spare
    /// threshold: <c>Instability &gt;= 0.8</c> is the <i>isolated</i> in "complex inside but
    /// isolated", and without it the finding claims nothing more than "complex". Four types are
    /// complex enough to qualify and are held back only by it — and <b>all four are also
    /// concealed decisions</b>, so row 2 removes every one of them before the difference can
    /// show. The gate is masked, not redundant.
    /// </para>
    /// <para>
    /// <b>What that would say if the mask ever lifted.</b> <c>ShipmentLedger</c> has fan-in 11,
    /// the most depended-on type in the fixture, and is already nominated as both a bug blast
    /// radius and load-bearing-and-intricate. Delete the instability gate and the same run also
    /// tells the reader that if it breaks, it breaks alone. That is invariant 3's exact failure,
    /// and on this fixture the only thing preventing it is an unrelated suppression row.
    /// </para>
    /// <para>
    /// The plant both gaps need is the same shape: <b>a complex, well-connected type that is not
    /// a concealed decision</b>. Every complex type on TestBed is one, which is why neither gate
    /// has anything to bite on. Add, do not reshape.
    /// </para>
    /// </remarks>
    [Fact]
    public void Breaks_alone_has_gates_its_own_suppressions_hide()
    {
        var policy = core.Model.Policy;
        var detected = Analysis.Detected(core.Model);

        bool IsConcealed(TypeNode t) =>
            detected.ContainsAbout(FindingKind.ConcealedDecisionType, t.Subject) ||
            detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, t.Subject);

        // Everything the instability gate holds back, and the fact that makes it invisible.
        var heldBack = core.Model.Types
            .Where(t => t.Instability is { } i && i < policy.IsolatedThreshold)
            .Where(t => t.MaxMemberCyclomatic >= policy.HighCc)
            .ToList();

        Assert.Equal(
            [
                "FuelEvaluator", "GuaranteedServiceNormalizer", "LaneEvaluator", "PolicyEvaluator",
                "ShipmentCoordinator", "ShipmentLedger", "TariffCalculator", "TransitEvaluator",
            ],
            heldBack.Select(t => t.Name).Order(StringComparer.Ordinal));

        // Four of the eight are the planted evaluators, and none of those is a concealed
        // decision — which is what un-masked the gate. Before the plant every type on this list
        // was one, so row 2 removed each of them and the gate could be deleted with no output
        // moving. The count is asserted so that losing the plant is loud.
        Assert.Equal(4, heldBack.Count(t => !IsConcealed(t)));

        // Row 3's candidates were both taken by an earlier row. Read off the detected set rather
        // than re-derived from the model: OrderRepository and PayloadTag are unreferenced and
        // complex but depend on nothing either, so their instability is undefined and they never
        // reach the finding at all. A gap record that re-states the detector's conditions gets
        // that wrong, and did.
        var unreferenced = detected.OfKind(FindingKind.BreaksAlone)
            .Select(f => core.Model.Find(f.Subject)!)
            .Where(t => t.FanIn < policy.BreaksAloneMinFanIn)
            .ToList();

        Assert.Equal(
            ["AuditReconciler", "DetentionEvaluator", "ShipmentController"],
            unreferenced.Select(t => t.Name).Order(StringComparer.Ordinal));

        // DetentionEvaluator is the one that is neither, so row 3 is the only rule that reaches
        // it. The other two are still taken first, which is why the row was dead before.
        Assert.Equal(
            ["DetentionEvaluator"],
            unreferenced
                .Where(t => !IsConcealed(t) && t.Classification.Kind is not ("ApiBoundary" or "ExternalCall" or "Contract"))
                .Select(t => t.Name));
    }

    /// <summary>
    /// The god-object half of the hub disjunction decides something now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §3.8 splits a hub into a bottleneck or a wiring hub on
    /// <c>MaxMemberCyclomatic &gt;= highCc</c> <b>or</b> <c>MemberCount &gt;= godObjectMembers</c>.
    /// Only <c>ShipmentCoordinator</c> reached the bottleneck branch and it did so on complexity,
    /// so the member-count half had never decided anything: deleting it changed no output.
    /// </para>
    /// <para>
    /// <c>DispatchRegistry</c> reaches it on size alone — 23 members, worst method cc 1. The
    /// control moves the threshold past it and watches the verdict change rather than merely
    /// asserting the finding exists, because a hub that is a bottleneck and a hub that is wiring
    /// are both output and only one of them is right.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_god_object_plant_observes_the_member_count()
    {
        var registry = run.Type("DispatchRegistry");

        // Reaches the branch on size, and could not reach it on complexity.
        Assert.True(registry.MemberCount >= run.Options.GodObjectMembers);
        Assert.True(registry.MaxMemberCyclomatic < run.Options.HighCc);
        Assert.True(Math.Min(registry.FanIn, registry.FanOut) >= run.Options.HubMin);

        var atDefaults = NominationText.Render(run.Result, run.Options);
        Assert.Contains("DispatchRegistry", atDefaults, StringComparison.Ordinal);
        Assert.Contains("Architectural bottleneck", atDefaults, StringComparison.Ordinal);

        // Raise the floor past 23 and the same type reads as wiring instead. It is still a hub,
        // so this is the disjunction moving rather than the finding disappearing.
        var raised = NominationText.Render(
            run.Result, new Options { GodObjectMembers = registry.MemberCount + 1 });

        Assert.Contains("DispatchRegistry", raised, StringComparison.Ordinal);
        Assert.Contains("Wiring hub", raised, StringComparison.Ordinal);
    }

    /// <summary>
    /// Change cost's <c>or ApiBoundary</c> arm is gated <b>in the probe</b> and dead in Core, and
    /// the difference is the saturation conversion rather than a regression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §3.5 gates on <c>Kind is Contract or ApiBoundary</c>. Every nomination on this fixture was
    /// <c>Contract</c>, so the second half of the disjunction could be deleted with no output
    /// moving — and that was not an accident of the fixture. Almost nothing in real code
    /// references a controller, which is why every boundary here sat at fan-in 0 or 1, and why the
    /// arm needed a case built rather than found. <c>DispatchCallbackController</c> is that case:
    /// a return address five dispatchers name because they hand it to a carrier, so the
    /// dependency runs inward, from internal components to the edge.
    /// </para>
    /// <para>
    /// <b>It closes the gap in the probe and not in Core, which is the "extraction halves the
    /// protection" lesson arriving in a new form.</b> The probe gates on the kind filter plus an
    /// absolute floor, and the plant clears both — dropping <c>or "ApiBoundary"</c> from
    /// <c>PrintNominations</c> moves the golden and fails three tests. Core adds a
    /// share-of-the-solution gate, and five callers is rank 20.5 of 128: nowhere near the
    /// most-depended-on part of the application, which is the whole of what the conversion is
    /// for. So the same deletion in <c>ChangeCost</c> passes the suite.
    /// </para>
    /// <para>
    /// <b>Recorded rather than forced, deliberately.</b> Making Core's arm observable needs a
    /// boundary in the solution's top 5% by fan-in. The realistic shape is a base controller —
    /// real codebases' carry enormous fan-in and are unambiguously boundaries — and this fixture
    /// has one, <c>ControllerBase</c> at fan-in 8, which the classifier misses only because the
    /// name lacks the suffix. Reaching the limit of 6.9 needs fan-in 11, so three more
    /// controllers. Choosing 0.10 instead, which <c>ControllerBase</c> would clear at rank 7.5, is
    /// picking the constant to admit our own plant. Neither is worth doing to close a gap that
    /// only exists at this fixture's size. <c>TASKS.md</c> P7.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_change_cost_plant_observes_the_ApiBoundary_arm_in_the_probe_only()
    {
        var callback = run.Type("DispatchCallbackController");

        Assert.Equal("ApiBoundary", callback.Kind);
        Assert.True(callback.FanIn >= run.Options.MinCohort);

        // Core does not nominate it, and the reason is the share gate rather than the kind or the
        // floor — both of which it still clears. Asserted so that the day a plant reaches the
        // solution's top slice, this fails and the arm is recorded as observed on both sides.
        var subject = core.Model.Types.Single(t => t.Name == "DispatchCallbackController");
        Assert.True(subject.FanIn >= core.Model.Policy.MinFanIn);
        Assert.DoesNotContain(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.ChangeCost),
            f => f.Subject == subject.Subject);

        // And every subject Core does nominate is a Contract, which is what makes the arm
        // deletable there.
        Assert.All(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.ChangeCost),
            f => Assert.Equal("Contract", core.Model.Find(f.Subject)!.Classification.Kind));

        // The only one, so the arm is deletable exactly when this type is absent.
        Assert.Equal(
            ["DispatchCallbackController"],
            run.Result.Types
                .Where(t => t.Kind == "ApiBoundary" && t.FanIn >= run.Options.MinCohort)
                .Select(t => t.Name));

        // And it is inert everywhere else, so the plant adds one claim rather than a cluster of
        // them. Each of these is a gate it deliberately fails.
        Assert.True(callback.MaxMemberCyclomatic < run.Options.HighCc);        // not load-bearing,
                                                                              // not a boundary
                                                                              // carrying logic
        Assert.True(Math.Min(callback.FanIn, callback.FanOut) < run.Options.HubMin);  // not a hub
        Assert.Equal("ApiBoundary", callback.KindSpan);                        // one kind, not three

        var text = NominationText.Render(run.Result, run.Options);
        Assert.Contains(
            "DispatchCallbackController — 5 internal callers", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchCallbackController —", text.Replace(
            "DispatchCallbackController — 5 internal callers", "", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>++</c> is the only static write on one type, so dropping support for it empties a
    /// finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SESSION-NOTES.md</c> #20: counting only assignments and missing increment was a real
    /// defect, and <c>++</c> is a non-atomic read-modify-write that shares state exactly as much
    /// as an assignment does. The case planted for it did not protect the fix —
    /// <c>QuoteAssembler</c> carries an increment <i>and</i> a plain assignment, so its
    /// <c>StaticMutations</c> falls from 2 to 1 without the support and the finding still fires.
    /// </para>
    /// <para>
    /// <c>DispatchCounter</c> has one static field and one write, an increment. Stop counting
    /// increments and its count is zero, which is the whole gate — so the exact counts are
    /// asserted rather than "greater than zero", because that is the assertion that moves.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_increment_plant_is_the_only_static_write_that_is_one()
    {
        Assert.Equal(1, run.Type("DispatchCounter").StaticMutations);

        // The pre-existing case, and why it could not do this job.
        Assert.Equal(2, run.Type("QuoteAssembler").StaticMutations);

        Assert.Contains(
            "DispatchCounter",
            NominationText.Render(run.Result, run.Options),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Layer span's own floor is <b>vacuous</b>, not merely unobserved — no fixture can fix it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twelve mutations over §3.1, §3.8 and §3.9 as they moved into Core. Eight failed a test.
    /// This is the first of the three that did not, and it is the only one in the whole register
    /// that a plant cannot close.
    /// </para>
    /// <para>
    /// <b>Three significant kinds and a <c>MinKindSpan</c> of three make "spans the minimum" and
    /// "spans everything" one condition.</b> The gate cannot discriminate at any solution size,
    /// because there is no fourth kind to fall short of. Setting it to 2 changes nothing here and
    /// setting it to 4 empties the finding everywhere, forever. That is <c>TASKS.md</c> X4, and it
    /// is a design question rather than a missing case — recorded here so it is visible from the
    /// suite rather than only from the board.
    /// </para>
    /// <para>
    /// The fixture half is asserted too: nothing sits at span 2, so even a fourth kind would need
    /// a plant to make the floor decide. That plant collides with both constraints binding every
    /// plant — reaching a second significant kind means a new <c>ApiBoundary</c>/<c>ExternalCall</c>
    /// type or new fan-in on an existing one — which is why it is recorded rather than owed.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_layer_span_floor_cannot_discriminate_at_three_significant_kinds()
    {
        var policy = core.Model.Policy;
        var spans = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.SpansArchitecturalLayers)
            .Select(f => f.ValueOf("KindSpan")!.Value)
            .ToList();

        // Every nomination sits exactly on the floor, and the floor is also the ceiling. No
        // finding can be further above it than any other, so the gate admits all or none.
        Assert.NotEmpty(spans);
        Assert.All(spans, span => Assert.Equal(policy.MinKindSpan, span));

        // And nothing sits one below it, so lowering the floor admits nobody either. Read off the
        // model rather than re-derived from the detector's conditions: a gap record that restates
        // them gets them subtly wrong, and did.
        var significant = new[] { "ApiBoundary", "DataAccess", "ExternalCall" };
        var reach = core.Model.Types.Select(type =>
        {
            var kinds = new SortedSet<string>(StringComparer.Ordinal);
            if (significant.Contains(type.Classification.Kind, StringComparer.Ordinal))
                kinds.Add(type.Classification.Kind);
            foreach (var outbound in type.Outbound)
                if (core.Model.Find(outbound) is { } dependency &&
                    significant.Contains(dependency.Classification.Kind, StringComparer.Ordinal))
                    kinds.Add(dependency.Classification.Kind);
            return kinds.Count;
        }).ToList();

        Assert.DoesNotContain(reach, count => count == policy.MinKindSpan - 1);
    }

    /// <summary>
    /// The roll-call collapse has no case, and closing <c>DEFECTS.md</c> §11 is what took it away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A trade, made deliberately and recorded rather than discovered.</b> Under the probe's
    /// kind-signature grouping all six spanning types were one pattern, so the collapse branch was
    /// the only one the fixture exercised and the per-type detail branch had none — the opposite
    /// of what <c>TECHREQ-job-b.md</c> §3.1 and §5 both claim, and the golden settles it. Core
    /// groups on the named dependencies instead, which is §11's repair, and the largest group
    /// falls to four against a threshold of five. Now the detail branch is covered and the
    /// collapse is not.
    /// </para>
    /// <para>
    /// <b>The collapse is the better branch to owe.</b> It removes detail from a finding whose
    /// detail §3.1 calls the finding, so an uncovered collapse risks output that says too much,
    /// while an uncovered detail branch risked the headline finding rendering as one sentence
    /// about controllers — which is what it did.
    /// </para>
    /// <para>
    /// The plant is two types with identical dependency sets and different architectural roles for
    /// the key, and six sharing one dependency set for the threshold. <c>TASKS.md</c> P6 owes
    /// both. Until then <c>Qualifiers.PartOfALayeringPattern</c> can be pinned to
    /// <see langword="false"/>, and the pattern key can ignore a type's own role, with the whole
    /// suite green.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_roll_call_collapse_has_no_case_under_the_named_dependency_grouping()
    {
        var policy = core.Model.Policy;
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);

        // Nothing collapses, so the qualifier is a constant on this fixture.
        Assert.All(findings, f => Assert.False(f.Holds(Qualifiers.PartOfALayeringPattern)));

        // And it is not close: the largest pattern is one short of the threshold, so the plant
        // needs two more members rather than a tuning change.
        var largest = findings.Max(f => f.ValueOf("PatternGroupSize")!.Value);
        Assert.Equal(4, largest);
        Assert.True(largest <= policy.RollCallThreshold);

        // The other half the fixture cannot see: no two subjects share a dependency set while
        // differing in their own role, so whether the role belongs in the pattern key is
        // undecidable here. Grouping on dependencies alone gives the identical partition.
        var withRole = findings
            .GroupBy(f => string.Join(
                "|",
                new[] { core.Model.Find(f.Subject)!.Classification.Kind }
                    .Concat(f.Participants.Select(p => p.Canonical))),
                StringComparer.Ordinal)
            .Count();
        var withoutRole = findings
            .GroupBy(f => string.Join("|", f.Participants.Select(p => p.Canonical)), StringComparer.Ordinal)
            .Count();

        Assert.Equal(withRole, withoutRole);
    }

    /// <summary>
    /// The three dead-code traps are planted, and nothing can currently tell them apart from
    /// code that really is unreferenced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TenantPolicySink</c> is reached only through a DI registration, <c>SchemaMigrationHandler</c>
    /// only through a string literal, and <c>FixtureBuilder</c> only from <c>Core.Tests</c>, which is
    /// skipped by default. All three therefore have a static fan-in of zero — the same reading a
    /// genuinely dead type gives.
    /// </para>
    /// <para>
    /// Type-level dead code is not implemented: the probe reports unreferenced <b>projects</b>, and
    /// <c>TECHREQ-job-a.md</c> §5.6 ships type-level detection last precisely because this is where
    /// the false positives live. So the plants sit in the fixture ahead of the feature, which is the
    /// right order — the acceptance criterion is that none of the three is reported as unreferenced
    /// without its category named, and that criterion is now testable the day the feature lands.
    /// </para>
    /// <para>
    /// This test fails then, and narrowing it is the event worth seeing. Invariant 4: a tool that
    /// says "safe to remove" about something six customers depend on has caused the burn it claimed
    /// to prevent.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("AuditPolicySink")]         // registered by convention — no type name anywhere
    [InlineData("SchemaMigrationHandler")]  // resolved by string literal
    [InlineData("FixtureBuilder")]          // used only from a skipped test project
    public void A_dead_code_trap_reads_exactly_like_dead_code(string trap)
    {
        var planted = run.Result.Types.Single(t => t.Name == trap);

        // Indistinguishable on the only evidence currently collected.
        Assert.Equal(0, planted.FanIn);

        // And nothing in the report mentions it — there is no section for this yet, so silence
        // here is the absence of a feature rather than a clean bill of health.
        Assert.DoesNotContain(trap, NominationText.Render(run.Result, run.Options), StringComparison.Ordinal);
    }

    /// <summary>
    /// The blast-radius plant moves when its thresholds move, which is the whole reason for
    /// planting it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Output existing is not the same as a gate being observed. Before this plant, the section
    /// was empty and would have stayed byte-identical if <c>--min-fan-in</c> had been set to any
    /// value at all — the finding could have been deleted outright and nothing would have failed.
    /// </para>
    /// <para>
    /// Only <c>MinFanIn</c> is reachable as an option; the <c>2.0</c> multiple and the <c>95</c>
    /// and <c>70</c> percentile floors are literals inside <c>PrintNominations</c>. Those are now
    /// covered differently but no less firmly: the nomination is in
    /// <c>golden/nominations.verified.txt</c>, so changing any of them changes the frozen
    /// baseline and <c>OracleGoldenTests</c> fails.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_blast_radius_plant_observes_the_fan_in_floor()
    {
        var atDefaults = NominationText.SubjectsUnder(
            NominationText.Render(run.Result, run.Options), "-- BUG BLAST RADIUS");

        Assert.Equal(["ShipmentLedger"], atDefaults);

        // ShipmentLedger has 11 callers. Raise the floor past it and the finding goes quiet,
        // which it could not have done before there was anything to silence.
        var aboveThePlant = NominationText.SubjectsUnder(
            NominationText.Render(run.Result, new Options { MinFanIn = 12 }), "-- BUG BLAST RADIUS");

        Assert.Empty(aboveThePlant);
    }

    /// <summary>
    /// The DI case named in the requirement is the one that needs no work.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-a.md</c> §5.6 asks for <c>services.AddX&lt;T&gt;()</c> to be detected as an
    /// inbound reference. It already is — a generic type argument is a compile-time reference, so
    /// <c>TenantPolicySink</c> has fan-in 1 and never looked dead. Asserted so the requirement is
    /// not implemented twice, and so the distinction from <c>AuditPolicySink</c> — same container,
    /// same lifetime, registered by convention instead, fan-in 0 — stays visible.
    /// </remarks>
    [Fact]
    public void A_generic_DI_registration_is_already_a_visible_reference()
    {
        Assert.Equal(1, run.Result.Types.Single(t => t.Name == "TenantPolicySink").FanIn);
        Assert.Equal(0, run.Result.Types.Single(t => t.Name == "AuditPolicySink").FanIn);
    }
}

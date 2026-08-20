using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

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
public sealed class FixtureCoverageTests(CoreWalkFixture core)
{
    /// <summary>The one type with this simple name.</summary>
    private TypeNode Type(string name) => core.Model.Types.Single(t => t.Name == name);

    /// <summary>
    /// The types nominated under <paramref name="kind"/>, by name, after suppression.
    /// </summary>
    /// <remarks>
    /// <b>Replaces reading the probe's rendered sections</b>, which is how this file used to ask
    /// "does the fixture reach this finding". Section headers were the only surface the probe had,
    /// so a question about the fixture had to be asked of a renderer. Core has the finding set, so
    /// it is asked of the model, and rewording a section can no longer break a test about a plant.
    /// <para>
    /// A method-level finding is <i>about</i> a member, so its subject is resolved through the
    /// declaring type — the same walk <c>SubjectRef</c> carries for suppression row 2. Nothing
    /// worth naming is dropped by the null filter: a subject that resolves to no type is the
    /// solution itself, which only <c>Coverage</c> findings take and which has no name to list.
    /// </para>
    /// </remarks>
    private List<string> Nominated(FindingKind kind, SolutionModel? model = null)
    {
        var m = model ?? core.Model;

        return [.. Analysis.FindingsFor(m)
            .OfKind(kind)
            .Select(f => m.Find(f.Subject.Kind == SubjectKind.Member ? f.Subject.DeclaringType! : f.Subject))
            .Where(t => t is not null)
            .Select(t => t!.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The whole terminal report, for the two tests that assert an absence from it.</summary>
    private string ReportText() =>
        string.Join("\n", Report.For(core.Model, Analysis.FindingsFor(core.Model)));

    /// <summary>
    /// Every type nominated as a concealed decision at type level is also nominated at method
    /// level, so type level adds no subject of its own on this fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interesting direction is covered — <c>FindingTests</c> asserts that seven
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

        // **P9 closed this gap.** Until 2026-08-20 every type nominated at type level was also
        // nominated at method level, so nothing here would have noticed the type-level detector
        // being reduced to a filter over the method-level one. CustomsTrait is the first subject
        // type level finds alone: its cohort has six types but only one method between them, so
        // the method-level population is below MinCohort and never runs.
        var only = byType.Except(byMethod, StringComparer.Ordinal).ToList();

        Assert.Single(only);
        Assert.EndsWith("CustomsTrait", only[0], StringComparison.Ordinal);
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
        // Both were empty, and an empty finding produces the same output whatever its thresholds
        // are. Asserted positively so that filling these gaps cannot quietly un-fill itself.
        // SpanCaliper joined ShipmentLedger with P7: it is the near miss that sits exactly on the
        // fan-in multiple and on the complexity percentile, which is what makes those two
        // constants observable at all. The finding is no longer a single-row list and the point of
        // this assertion is that it is not empty rather than that it holds one name.
        Assert.Equal(["ShipmentLedger", "SpanCaliper"], Nominated(FindingKind.BugBlastRadius));
        Assert.Contains("TariffReconciler", Nominated(FindingKind.BreaksAlone));

        // Asserted alongside so this reads as a gap in two findings rather than a fact about two
        // arbitrary strings. If these ever empty out, the fixture broke, not the tool.
        Assert.NotEmpty(Nominated(FindingKind.ConcealedDecisionType));
        Assert.NotEmpty(Nominated(FindingKind.ConcealedDecisionMethod));
        Assert.NotEmpty(Nominated(FindingKind.ChangeCost));
        Assert.NotEmpty(Nominated(FindingKind.LoadBearingAndIntricate));
        Assert.NotEmpty(Nominated(FindingKind.SharedMutableState));
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
    /// gate is held only by <c>FindingTests</c> — and a gate that is redundant on this
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

        // DetentionEvaluator and AuditReconciler are the ones that are neither, so row 3 is the
        // only rule that reaches them. ShipmentController is still taken first, by row 1.
        //
        // AuditReconciler joined them when ConcealedTopRank landed: it ranks fourth in its cohort,
        // so it is no longer a concealed decision and row 2 no longer takes it. The row is further
        // from dead than it was, not closer.
        Assert.Equal(
            ["AuditReconciler", "DetentionEvaluator"],
            unreferenced
                .Where(t => !IsConcealed(t) && t.Classification.Kind is not ("ApiBoundary" or "ExternalCall" or "Contract"))
                .Select(t => t.Name).Order(StringComparer.Ordinal));
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
        var registry = Type("DispatchRegistry");
        var policy = core.Model.Policy;

        // Reaches the branch on size, and could not reach it on complexity.
        Assert.True(registry.MemberCount >= policy.GodObjectMembers);
        Assert.True(registry.MaxMemberCyclomatic < policy.HighCc);
        Assert.True(Math.Min(registry.FanIn, registry.FanOut) >= policy.HubMin);

        // The probe said this in prose — "Architectural bottleneck" against "Wiring hub" — and
        // Core carries it as a qualifier on the finding, which is what makes the arm assertable
        // rather than greppable.
        Assert.True(Hub(core.Model, registry).Holds(Qualifiers.TooLargeToHold));

        // Raise the floor past 23 and the same type reads as wiring instead. It is still a hub,
        // so this is the disjunction moving rather than the finding disappearing — and it needs a
        // real second walk, because a finding has to be able to name the policy that produced it.
        var raised = core.WalkWith(policy with { GodObjectMembers = registry.MemberCount + 1 });
        var thereToo = raised.Types.Single(t => t.Name == "DispatchRegistry");

        Assert.False(Hub(raised, thereToo).Holds(Qualifiers.TooLargeToHold));
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
    /// <b>And P8 closed it without aiming at it, which is what the note above predicted.</b> That
    /// note said the gap <i>"only exists at this fixture's size"</i> and refused to force it —
    /// three more controllers to reach a limit of 6.9, or picking 0.10 to admit our own plant.
    /// P8's tangles and collision took the solution from 128 analysed types to 179, the top-5%
    /// rank limit moved with it, and <c>LayeringEndpoint</c> at fan-in 8 came inside the slice on
    /// its own. <b>The arm is observed on both sides now</b>, and the reason is the population
    /// rather than a plant built to admit it — which is the only way it was ever going to be
    /// worth having.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_change_cost_ApiBoundary_arm_is_observed_on_both_sides_now()
    {
        var callback = Type("DispatchCallbackController");

        Assert.Equal("ApiBoundary", callback.Classification.Kind);
        Assert.True(callback.FanIn >= core.Model.Policy.MinCohort);

        // DispatchCallbackController is outside Core's slice, and the reason is the share gate
        // rather than the kind or the floor — both of which it clears.
        Assert.True(callback.FanIn >= core.Model.Policy.MinFanIn);
        Assert.DoesNotContain(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.ChangeCost),
            f => f.Subject == callback.Subject);

        // But the arm itself is no longer deletable in Core: LayeringEndpoint is an ApiBoundary and
        // Core nominates it, so dropping the kind from ChangeCost now moves the finding set. That
        // is the half this test was written to say was missing.
        var kinds = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.ChangeCost)
            .Select(f => core.Model.Find(f.Subject)!.Classification.Kind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ApiBoundary", "Contract"], kinds);

        // Two boundaries clear the floor since P6, whose LayeringEndpoint is reached by eight
        // conduits and got there without having been built to. That is what stops the arm resting
        // on a single plant — and it is also why the arm survives while this type does not:
        // LayeringEndpoint is inside the slice and DispatchCallbackController, at solution midrank
        // 9 against a limit of 7.2, is not.
        Assert.Equal(
            ["DispatchCallbackController", "LayeringEndpoint"],
            core.Model.Types
                .Where(t => t.Classification.Kind == "ApiBoundary"
                            && t.FanIn >= core.Model.Policy.MinCohort)
                .Select(t => t.Name)
                .Order(StringComparer.Ordinal));

        // And it is inert everywhere else, so the plant adds one claim rather than a cluster of
        // them. Each of these is a gate it deliberately fails.
        Assert.True(callback.MaxMemberCyclomatic < core.Model.Policy.HighCc);   // not load-bearing,
                                                                               // not a boundary
                                                                               // carrying logic
        Assert.True(Math.Min(callback.FanIn, callback.FanOut) < core.Model.Policy.HubMin);  // not a hub

        // One kind, not three, so it does not span layers either. The probe carried this as a
        // KindSpan string on the type; Core carries no such column and answers the question the
        // way a reader would — is there a finding.
        Assert.DoesNotContain(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers),
            f => f.Subject == callback.Subject);

        // The whole of it: Core makes no claim about this type at all. The probe named it once,
        // under change cost, and that single mention was what the old assertion pinned by string
        // surgery on the rendered text.
        Assert.Empty(Analysis.FindingsFor(core.Model).About(callback.Subject));
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
        Assert.Equal(1, Type("DispatchCounter").StaticMutations);

        // The pre-existing case, and why it could not do this job.
        Assert.Equal(2, Type("QuoteAssembler").StaticMutations);

        Assert.Contains("DispatchCounter", Nominated(FindingKind.SharedMutableState));
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
    /// The roll-call collapse fires, and it fires for the pattern rather than for the pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This branch was owed from the moment <c>DEFECTS.md</c> §11 closed.</b> Under the probe's
    /// kind-signature grouping all six spanning types were one pattern, so the collapsed line was
    /// the only branch the fixture exercised and the per-type detail branch had none — the opposite
    /// of what <c>TECHREQ-job-b.md</c> §3.1 and §5 both claimed, and the golden settled it. Core
    /// groups on the named dependencies instead, the largest group fell to four against a
    /// threshold of five, and the branches traded places. R1 then rendered the collapse from the
    /// qualifier, so it was written, unreachable and held by nothing until <c>TASKS.md</c> P6.
    /// </para>
    /// <para>
    /// <b>The plant is <c>TestBed.Core.Layering</c>, and it is one construction answering two
    /// questions.</b> Eight types reach the identical three components — one per significant kind,
    /// all three new, because eight shared dependents would otherwise be eight new inbound edges
    /// on types that already exist. Six of them are <c>Internal</c> and are the group of six the
    /// threshold needs. The other two are <c>ApiBoundary</c> and differ from the six in nothing
    /// else, which is what makes the type's own role the whole of the difference between a
    /// partition of 6 + 2 and one group of 8.
    /// </para>
    /// <para>
    /// The two halves interlock: with the role in the key the six collapse and the pair keeps its
    /// detail; without it all eight collapse and the pair loses detail it is entitled to. That is
    /// §11's failure one level down — a collapse absorbing something that is not an instance of
    /// the pattern — which is why the pair is the control rather than a second plant.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_roll_call_collapse_fires_for_the_pattern_and_spares_the_pair()
    {
        var policy = core.Model.Policy;
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);

        string Name(Finding finding) => core.Model.Find(finding.Subject)!.Name;

        // The six that are a pattern, named rather than counted: a count would still pass if the
        // collapse started taking the wrong six.
        Assert.Equal(
            ["EgressConduit", "IntakeConduit", "MirrorConduit", "RelayConduit", "ReplayConduit", "SyncConduit"],
            findings
                .Where(f => f.Holds(Qualifiers.PartOfALayeringPattern))
                .Select(Name)
                .Order(StringComparer.Ordinal));

        // The pair reaches the same three components and keeps its detail, because its group has
        // two members. Both halves asserted — that they are in a group of two, and that the group
        // being small is what spares them.
        var pair = findings.Where(f => Name(f).StartsWith("Public", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, pair.Count);
        Assert.All(pair, f => Assert.False(f.Holds(Qualifiers.PartOfALayeringPattern)));
        Assert.All(pair, f => Assert.Equal(2, f.ValueOf("PatternGroupSize")));

        // Six against a threshold of five, which is the smallest group that fires it.
        Assert.Equal(6, findings.Max(f => f.ValueOf("PatternGroupSize")!.Value));
        Assert.Equal(5, policy.RollCallThreshold);
    }

    /// <summary>
    /// The collapse threshold now decides in both directions, which is what a nudge asks.
    /// </summary>
    /// <remarks>
    /// The leave-one-out question — does the condition discriminate — was already answered, since
    /// deleting the qualifier changes every spanning type's rendering. The nudge is the other
    /// question, and before P6 it had no answer at all: with nothing collapsing, the threshold
    /// could be moved anywhere without moving output. It is asserted here from both sides,
    /// because a gate observable in one direction only is half a gate — <c>docs/TESTING.md</c> §6
    /// carries two of those and they are the reason this suite distinguishes them.
    /// </remarks>
    [Fact]
    public void The_roll_call_threshold_decides_in_both_directions()
    {
        // Loosening it: a divisor of 2 puts the threshold at 7, above the group of six, and the
        // pattern stops collapsing.
        Assert.Empty(CollapsedUnder(core.Model.Policy with { RollCallDivisor = 2 }));

        // Tightening it: a divisor of 4 puts the threshold at 3, and the four boilerplate
        // controllers — a pattern of four that keeps its detail today — collapse as well.
        Assert.Equal(
            ["DocumentController", "EgressConduit", "IntakeConduit", "MirrorConduit", "QuoteController",
             "RateController", "RelayConduit", "ReplayConduit", "SyncConduit", "TrackingController"],
            CollapsedUnder(core.Model.Policy with { RollCallDivisor = 4 }));
    }

    /// <summary>
    /// A type's own architectural role is part of what makes two spanning types one finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recorded as undecidable until P6: no two spanning subjects shared a dependency set while
    /// differing in their role, so grouping on dependencies alone gave the identical partition and
    /// the role could be dropped from the key with the suite green. It cannot now.
    /// </para>
    /// <para>
    /// The consequence is named rather than counted, because a partition count moving is not the
    /// point. Dropping the role merges the <c>ApiBoundary</c> pair into the group of six, taking
    /// it to eight — so two components that receive calls from outside the solution would be
    /// reported as two more instances of an internal relay pattern, and lose the per-kind detail
    /// §3.1 calls the finding.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_types_own_role_is_part_of_the_pattern_key()
    {
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);

        IEnumerable<IGrouping<string, Finding>> GroupBy(Func<Finding, IEnumerable<string>> key) =>
            findings.GroupBy(f => string.Join("|", key(f)), StringComparer.Ordinal);

        var dependencies = GroupBy(f => f.Participants.Select(p => p.Canonical)).ToList();
        var withRole = GroupBy(f => new[] { core.Model.Find(f.Subject)!.Classification.Kind }
            .Concat(f.Participants.Select(p => p.Canonical))).ToList();

        // The partitions differ, which is the whole of what was undecidable before.
        Assert.NotEqual(dependencies.Count, withRole.Count);

        // And this is the group that differs: on dependencies alone the pair joins the six.
        var merged = dependencies.Single(g => g.Count() == 8);
        Assert.Equal(2, merged.Count(f => core.Model.Find(f.Subject)!.Classification.Kind == TypeKinds.ApiBoundary));

        // Eight is past the threshold, so the pair would not merely be regrouped — it would be
        // collapsed, which is the detail loss the key exists to prevent.
        Assert.True(merged.Count() > core.Model.Policy.RollCallThreshold);
    }

    /// <summary>The subjects whose layer-span detail collapses under a given policy.</summary>
    private static List<string> CollapsedUnder(AnalysisPolicy policy)
    {
        var model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None).GetAwaiter().GetResult();

        return Analysis.FindingsFor(model)
            .OfKind(FindingKind.SpansArchitecturalLayers)
            .Where(f => f.Holds(Qualifiers.PartOfALayeringPattern))
            .Select(f => model.Find(f.Subject)!.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Coverage's two global gates: one condition is live since P6, and both constants are dead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>GlobalComplexityFloor</c> decides nothing.</b> It exists so that a max-member
    /// complexity of 1 cannot be reported as <i>"top 86% by complexity"</i> in a codebase with
    /// little branching (<c>SESSION-NOTES.md</c> #8), and on this fixture no below-floor type
    /// clears the percentile while failing the floor — so removing it changes no output. The plant
    /// it needs is a peerless type with cc 1 sitting high on the solution-wide complexity
    /// percentile, which takes a codebase flatter than this one. Still owed.
    /// </para>
    /// <para>
    /// <b><c>GlobalFanInPercentile</c>'s condition had never fired, and P6 gave it a case.</b>
    /// The record said the plant would have to be deliberate — <i>a lone component that much of
    /// the system depends on</i>, close to structural because a type with no peer group usually
    /// has few callers — and predicted it would never arrive by accident. It arrived by accident.
    /// P6's three shared dependency targets each carry fan-in 8 and each land in a cohort too
    /// small to compare against, which is that description exactly.
    /// </para>
    /// <para>
    /// <b>The constant is still dead, and conflating the two is what the sweep caught.</b> The
    /// three sit at <c>GlobalFanInPctl</c> 94.1 against a bar of 90, so a one-notch move in either
    /// direction changes nothing and it takes 95 to empty the claim —
    /// <c>PolicySweepTests</c> reports it unmoved both ways. What P6 closed is the condition: the
    /// gate now has something to admit, so deleting it would be noticed. Before, it admitted
    /// nothing and its removal was free.
    /// </para>
    /// <para>
    /// <b>Worth being uneasy about, and recorded rather than celebrated.</b> The case is a
    /// by-product of a plant built for layer span, so nothing about it is load-bearing for P6 and
    /// a future reshape of the conduits would retire it without anything saying so. That is why
    /// <c>FindingTests</c> names the three types rather than counting them.
    /// </para>
    /// </remarks>
    [Fact]
    public void Coverages_global_gates_are_one_observed_and_one_dead()
    {
        var policy = core.Model.Policy;
        var coverage = Analysis.FindingsFor(core.Model).OfKind(FindingKind.Coverage);

        // P6 closed the fan-in half. Three of its types clear the percentile, so raising the bar
        // now moves output as well as lowering it — see the remarks.
        Assert.Equal(
            3,
            coverage.Count(f => f.ValueOf("GlobalFanInPctl") >= policy.GlobalFanInPercentile));

        // And nothing clears the complexity percentile while failing the floor, which is the only
        // configuration in which the floor decides anything.
        Assert.DoesNotContain(
            coverage,
            f => f.ValueOf("GlobalMaxCcPctl") >= policy.GlobalComplexityPercentile &&
                 f.ValueOf("MaxMemberCyclomatic") <= policy.GlobalComplexityFloor);
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
    /// <b>It will not be this test that announces the feature, though.</b> The assertion below
    /// renders the <i>probe</i>, which is frozen, so type-level dead code landing in Core leaves
    /// it green — the same trap <c>docs/DEFECTS.md</c> §1 fell into, where a pin on the oracle was
    /// mistaken for a guard on the reimplementation. When the feature is built, the assertion that
    /// matters is over Core's finding set and belongs beside it. Invariant 4 is what it has to
    /// satisfy: a tool that says "safe to remove" about something six customers depend on has
    /// caused the burn it claimed to prevent.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("AuditPolicySink")]         // registered by convention — no type name anywhere
    [InlineData("SchemaMigrationHandler")]  // resolved by string literal
    [InlineData("FixtureBuilder")]          // used only from a skipped test project
    public void A_dead_code_trap_reads_exactly_like_dead_code(string trap)
    {
        var planted = Type(trap);

        // Indistinguishable on the only evidence currently collected.
        Assert.Equal(0, planted.FanIn);

        // No claim is made about it, and nothing in the report names it — there is no section for
        // this yet, so silence here is the absence of a feature rather than a clean bill of
        // health. Both halves, because a finding that existed and went unrendered would be a
        // different failure from a finding that was never made.
        Assert.Empty(Analysis.FindingsFor(core.Model).About(planted.Subject));
        Assert.DoesNotContain(trap, ReportText(), StringComparison.Ordinal);
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
        Assert.Equal(["ShipmentLedger", "SpanCaliper"], Nominated(FindingKind.BugBlastRadius));

        // ShipmentLedger has 11 callers and SpanCaliper 5. Raise the floor past both and the
        // finding goes quiet, which it could not have done before there was anything to silence.
        var aboveThePlant = core.WalkWith(core.Model.Policy with { MinFanIn = 12 });

        Assert.Empty(Nominated(FindingKind.BugBlastRadius, aboveThePlant));
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
        Assert.Equal(1, Type("TenantPolicySink").FanIn);
        Assert.Equal(0, Type("AuditPolicySink").FanIn);
    }

    /// <summary>
    /// No constructor is nominated here, so the report never has to name one — and the guard
    /// against naming it badly is vacuous until that changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §24.</b> A constructor's member name <i>is</i> <c>.ctor</c>, so
    /// joining it to its type with a dot yields <c>CustomerInfoValidator..ctor</c>. The fixture
    /// declares no constructor complex enough to be nominated, which is why the defect was found
    /// by reading nopCommerce and not by this suite — and why it sat filed as cosmetic while being
    /// the first row of that solution's concealed-decision section.
    /// </para>
    /// <para>
    /// Both halves matter. The first asserts the hole so it stays visible; the second is a guard
    /// that costs nothing now and starts doing real work the moment a plant fills the hole, which
    /// is the only point at which this could regress.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_constructor_is_nominated_so_the_member_naming_guard_is_vacuous()
    {
        var findings = Analysis.FindingsFor(core.Model);

        var constructors = findings.All
            .Select(f => f.Subject.DeclaringType is null ? null : f.Subject)
            .Count(s => s?.Canonical.Contains(".ctor", StringComparison.Ordinal) == true);

        Assert.Equal(0, constructors);

        var text = string.Join(Environment.NewLine, Report.For(core.Model, findings));
        Assert.DoesNotContain("..ctor", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// No cohort here has a median complexity of zero, so the ranking rule for an undefined
    /// ratio never runs on this fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ConcealedDecision</c> ranks a nomination whose peer median is 0 <b>after</b> every one
    /// whose extremity was measured, and orders that group by absolute complexity, because a
    /// ratio against zero is undefined rather than infinite. Ordering on the ratio alone put ten
    /// tied rows at the top of nopCommerce's section — cc 6 leading — ahead of a constructor at
    /// 37x its peer median. <c>docs/DEFECTS.md</c> §28.
    /// </para>
    /// <para>
    /// <b>The fixture cannot show any of it.</b> Every cohort here has a non-zero median, so that
    /// branch is exercised only by real solutions and the accepted snapshot is no evidence about
    /// it either way. This fails the day a plant adds a cohort of property bags with one complex
    /// member among them — which is the point at which the ordering needs a test of its own and
    /// this assertion should be deleted rather than widened.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_cohort_with_a_median_of_zero_exercises_the_undefined_ratio_ranking()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var typeLevel = findings.OfKind(FindingKind.ConcealedDecisionType);

        var undefined = typeLevel
            .Where(f => double.IsInfinity(f.ValueOf("MaxMemberCyclomaticXMedian") ?? 0))
            .ToList();

        // P9's plant. It was zero until 2026-08-20, and the gap this test recorded was that the
        // ranking rule shipped exercised only by real solutions — 10 of nopCommerce's 79
        // type-level nominations, and none of TestBed's.
        var subject = Assert.Single(undefined);
        Assert.Equal("CustomsTrait", core.Model.Find(subject.Subject)!.Name);

        // And it ranks LAST, which is the rule the plant exists for. Asserted here as well as in
        // FindingTests' ordering theory because that theory encoded the wrong rule for months and
        // nothing could catch it: with no undefined case in the fixture, plain rank-descending and
        // undefined-last are the same sequence.
        Assert.Equal(typeLevel[^1].Subject, subject.Subject);
        Assert.True(
            typeLevel.Count > 1 && double.IsFinite(typeLevel[0].ValueOf("MaxMemberCyclomaticXMedian") ?? 0),
            "the section needs a measured ratio above it or last place proves nothing");
    }

    /// <summary>The hub-or-god-object finding about <paramref name="type"/>.</summary>
    /// <remarks>
    /// Takes the model explicitly because the god-object test asks the same question of a second
    /// walk, and a finding belongs to the model whose policy produced it.
    /// </remarks>
    private static Finding Hub(SolutionModel model, TypeNode type) =>
        Assert.Single(
            Analysis.FindingsFor(model).About(type.Subject),
            f => f.Kind == FindingKind.HubOrGodObject);
}

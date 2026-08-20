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
public sealed class FindingTests(CoreWalkFixture core)
{

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

        // Seven since P7: the near-miss families reach method level with methods their types do
        // not reach type level with, which widens the margin this test measures rather than
        // changing what it claims.
        Assert.Equal(7, byMethod.Except(byType, StringComparer.Ordinal).Count());
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

    // -------------------------------------------------------------------- coverage ----

    /// <summary>
    /// The weaker global claim is made about six types — three by complexity, three by fan-in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §3.11's second half: a type with no peers can still be extreme against the whole solution,
    /// and going quiet about it is not an option. The claim is labelled weaker because it compares
    /// unlike things, and the qualifier is what lets a renderer say so instead of borrowing a
    /// peer-relative sentence.
    /// </para>
    /// <para>
    /// <b>The fan-in half had never fired, and P6 gave it its first case as a side effect.</b> It
    /// was recorded as close to structural — a type with no peers usually has few callers — and as
    /// needing a deliberate plant: <i>a lone component much of the system depends on</i>. P6's
    /// shared dependency set is exactly that shape without having set out to be. Eight conduits
    /// reach three targets, so each target has fan-in 8 against a solution where most types have
    /// none, and none of the three has a peer group. The claim is now made in both flavours.
    /// </para>
    /// <para>
    /// <b>That is the condition, not the constant, and the two are not the same win.</b> The three
    /// sit at <c>GlobalFanInPctl</c> 94.1 against a bar of 90, so 89 and 91 both leave the finding
    /// alone and it takes 95 to move — <c>GlobalFanInPercentile</c> is still in the
    /// constants-the-fixture-cannot-see table. <c>docs/TESTING.md</c> §6 opens by insisting those
    /// are different questions, and the first version of this remark ran them together.
    /// </para>
    /// <para>
    /// It is asserted by name rather than by count, because the case is incidental to the plant
    /// that produced it: reshape P6 and the gate goes quiet again, and this is where that has to
    /// be noticed.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_weaker_global_claim_is_made_in_both_flavours()
    {
        var coverage = Analysis.FindingsFor(core.Model).OfKind(FindingKind.Coverage);

        Assert.Equal(
            ["OrderRepository", "PayloadTag", "RoutingDepot"],
            coverage
                .Where(f => f.Holds(Qualifiers.GloballyExtremeComplexity))
                .Select(f => core.Model.Find(f.Subject)!.Name)
                .Order(StringComparer.Ordinal));

        Assert.Equal(
            ["LayeringArchive", "LayeringBeacon", "LayeringEndpoint"],
            coverage
                .Where(f => f.Holds(Qualifiers.GloballyExtremeFanIn))
                .Select(f => core.Model.Find(f.Subject)!.Name)
                .Order(StringComparer.Ordinal));

        // And it is the floor, not the percentile, that would let a cc-1 type in — so the floor is
        // asserted as applied even though nothing on this fixture depends on it.
        Assert.All(
            coverage.Where(f => f.Holds(Qualifiers.GloballyExtremeComplexity)),
            f => Assert.True(f.ValueOf("MaxMemberCyclomatic") > f.ValueOf("GlobalComplexityFloor")));
    }

    // ------------------------------------------------------------- boundary marking ----

    /// <summary>
    /// The boundary rank is a gate in both directions.
    /// </summary>
    /// <remarks>
    /// The control the absolute form never had: widening the share brings the probe's second
    /// boundary back, and narrowing it does not empty the finding, because the floor decides that.
    /// A gate whose other branch cannot be reached is <c>docs/ARCHITECTURE.md</c> §9's, and this
    /// is the assertion that says which of the two conditions is doing the work on this fixture.
    /// </remarks>
    [Fact]
    public void The_boundary_rank_is_reachable_from_both_sides()
    {
        Assert.Equal(
            ["ReconciliationController", "ShipmentController"],
            BoundariesUnder(core.Model.Policy with { BoundaryTopFraction = 0.5 }));

        // And the floor is what makes an empty answer possible at all: no boundary on this fixture
        // reaches cc 40, so the rank gate has nothing to admit however wide it is opened.
        Assert.Empty(BoundariesUnder(core.Model.Policy with { BoundaryTopFraction = 1, HighCc = 40 }));
    }

    // ------------------------------------------------------------------ change cost ----

    /// <summary>
    /// The floor is the fan-in floor, and it is no longer the cohort floor. <c>DEFECTS.md</c> §9.
    /// </summary>
    /// <remarks>
    /// Both default to 5, which is what hid this for the whole probe build, so the two are pinned
    /// apart here: moving <c>MinCohort</c> must not move this finding, and moving <c>MinFanIn</c>
    /// must. <c>KnownDefectTests</c> holds the probe's half — the wrong knob works and the right
    /// one does nothing — and this is the same experiment against the fix.
    /// <para>
    /// Both walks are needed because cohort assignment reads <c>MinCohort</c> during the walk, so
    /// a different floor is a different set of peer groups rather than a different render.
    /// </para>
    /// </remarks>
    [Fact]
    public void Change_cost_reads_the_fan_in_floor_and_not_the_cohort_floor()
    {
        var atDefaults = CoreTypeNominations(FindingKind.ChangeCost);

        // The cohort floor is nothing to this finding now: it has no cohort in it.
        Assert.Equal(atDefaults, ChangeCostUnder(core.Model.Policy with { MinCohort = 16 }));

        // The fan-in floor is. Raising it past the smallest survivor drops it — two of them since
        // P8, because LayeringEndpoint came inside the solution-wide slice when the fixture grew.
        Assert.Equal(
            ["NormalizationContext", "RawResponse"],
            ChangeCostUnder(core.Model.Policy with { MinFanIn = 16 }));
    }

    /// <summary>
    /// The share decides, in both directions — and P6 is what made that true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This test used to assert the opposite, and the change is worth reading rather than
    /// re-accepting.</b> Its claim was that the constant is defensible because it is
    /// <i>insensitive</i>: the nominated set was identical across 0.05, 0.10 and 0.15, because the
    /// fixture's fan-in population had a gap between 15 and 5 and the gate fell inside it. That
    /// reassurance was an artifact of the gap. P6's three shared dependency targets sit at fan-in
    /// 8 and fill it, so the same threefold range now moves the finding.
    /// </para>
    /// <para>
    /// <b>Losing it is a gain by the inventory's own standard and a loss by X2's.</b>
    /// <c>docs/TESTING.md</c> §6 counts a constant as unobserved when a one-notch move changes
    /// nothing, and by that measure <c>ChangeCostTopFraction</c> has just stopped being one of
    /// them — this is the first fixture state in which the number decides anything. What is gone
    /// is the separate argument that the exact value does not matter, which is what X2 leaned on
    /// when it chose a share of the whole solution over a percentile. The value is now a real
    /// choice, and 0.05 is the one the default makes.
    /// </para>
    /// <para>
    /// Nothing was tuned to produce this. The default's answer is the same three types it was
    /// before the plant; what moved is the answer one notch out.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_change_cost_share_decides_in_both_directions()
    {
        // P8 moved this: change cost is gated SOLUTION-WIDE by X2, so the rank limit is a share of
        // a population that grew by twenty-one types, and LayeringEndpoint came inside the slice it
        // used to sit just outside. The finding did not change its mind — the solution got bigger.
        Assert.Equal(
            ["LayeringEndpoint", "NormalizationContext", "NormalizedResponse", "RawResponse"],
            CoreTypeNominations(FindingKind.ChangeCost));

        // Loosening it admits the boundary the probe's absolute floor always accepted, which is
        // the arm §3.5 has and Core has never exercised at its default.
        Assert.Equal(
            ["LayeringEndpoint", "NormalizationContext", "NormalizedResponse", "RawResponse"],
            ChangeCostUnder(core.Model.Policy with { ChangeCostTopFraction = 0.10 }));

        // And a wider slice admits more, which is the half of X2's decision this pins: change cost
        // is gated SOLUTION-WIDE, so the set it admits is a function of how many types the solution
        // has. P7 added fifteen and 15% went from six ranks to seven — the two extra names are that
        // arithmetic and not a change in the finding.
        Assert.Equal(
            [
                "DispatchCallbackController", "LayeringEndpoint", "ModelDescription",
                "NormalizationContext", "NormalizedResponse", "RawResponse",
            ],
            ChangeCostUnder(core.Model.Policy with { ChangeCostTopFraction = 0.15 }));

        // And tightening it past the third survivor narrows the finding.
        Assert.Equal(
            ["NormalizationContext", "NormalizedResponse", "RawResponse"],
            ChangeCostUnder(core.Model.Policy with { ChangeCostTopFraction = 0.02 }));
    }

    // ------------------------------------------------ hubs, static state, layer span ----

    /// <summary>
    /// The two arms of §3.8's disjunction say different things, which is <c>DEFECTS.md</c> §16.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The probe prints one sentence for both arms — <i>"it both depends on and is depended on by
    /// much of the system, AND carries real logic"</i> — and on the size arm that is false by
    /// construction, because the arm exists precisely for types with bulk and no logic.
    /// <c>DispatchRegistry</c> is told it carries real logic in a sentence whose own receipts say
    /// twenty-three members and a worst method of cc 1. Invariant 5 puts interpretation first and
    /// math as receipts, and there the interpretation refutes its own receipts.
    /// </para>
    /// <para>
    /// Core carries the arms as two independent qualifiers, so a renderer cannot make the claim by
    /// accident. The fixture supplies one type per combination, which is what makes this a gate
    /// rather than a comment: complexity alone, size alone, and neither.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_hub_disjunction_has_two_arms_that_say_different_things()
    {
        var registry = Hub("DispatchRegistry");
        var coordinator = Hub("ShipmentCoordinator");
        var router = Hub("Router");

        // Size alone. The receipts that refute the probe's sentence, on the finding that makes it.
        Assert.True(registry.Holds(Qualifiers.TooLargeToHold));
        Assert.False(registry.Holds(Qualifiers.CarriesRealLogic));
        Assert.Equal(1, registry.ValueOf("MaxMemberCyclomatic"));

        // Complexity alone — the arm where the probe's wording was true all along.
        Assert.True(coordinator.Holds(Qualifiers.CarriesRealLogic));
        Assert.False(coordinator.Holds(Qualifiers.TooLargeToHold));

        // And neither, which is the wiring hub. All three are still hubs: the split decides what
        // is said about them, never whether they are nominated.
        Assert.False(router.Holds(Qualifiers.CarriesRealLogic));
        Assert.False(router.Holds(Qualifiers.TooLargeToHold));

        // The gate is the minimum of the two magnitudes, not the maximum. Router is the case that
        // separates them from below and IResponseNormalizer the one that separates them from
        // above: fan-in 8 and fan-out 3, so a max-based gate would nominate it and this one must
        // not. SESSION-NOTES.md #14 — a ratio cannot see this finding at all.
        var normalizer = core.Model.Types.Single(t => t.Name == "IResponseNormalizer");
        Assert.True(Math.Max(normalizer.FanIn, normalizer.FanOut) >= core.Model.Policy.HubMin);
        Assert.True(Math.Min(normalizer.FanIn, normalizer.FanOut) < core.Model.Policy.HubMin);
        Assert.DoesNotContain(
            Analysis.FindingsFor(core.Model).About(normalizer.Subject),
            f => f.Kind == FindingKind.HubOrGodObject);
    }

    /// <summary>
    /// The finding names the members that write, rather than leaving the reader a count.
    /// </summary>
    /// <remarks>
    /// Invariant 7, and the increment case that <c>SESSION-NOTES.md</c> #20 records as a real
    /// defect: <c>DispatchCounter.Record</c> is a single <c>++</c>, a non-atomic read-modify-write
    /// that shares state exactly as much as an assignment. Stop counting increments and this
    /// finding empties, which is the whole of its gate — so the count is asserted exactly rather
    /// than as "more than none".
    /// </remarks>
    [Fact]
    public void Shared_mutable_state_names_the_members_that_write()
    {
        var counter = Assert.Single(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.SharedMutableState),
            f => core.Model.Find(f.Subject)!.Name == "DispatchCounter");

        Assert.Equal(1, counter.ValueOf("StaticMutations"));
        Assert.Equal(["Record"], MemberNames(counter));

        // Two writes, two members named. The pre-existing case, which could not protect the
        // increment fix because it also carries a plain assignment.
        var assembler = Assert.Single(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.SharedMutableState),
            f => core.Model.Find(f.Subject)!.Name == "QuoteAssembler");

        Assert.Equal(2, assembler.ValueOf("StaticMutations"));
        Assert.Equal(["Build", "Reset"], MemberNames(assembler));
    }

    /// <summary>
    /// A type's own architectural role counts toward its span, and five of six nominations need it.
    /// </summary>
    /// <remarks>
    /// §3.1: a boundary component that also does data access spans layers even if that is its only
    /// significant dependency. Drop the rule and the finding loses every controller and the
    /// middleware, keeping only <c>PolicyBridge</c> — which reaches all three kinds through
    /// dependencies alone and is the control that makes the deletion visible as a change in the
    /// population rather than as an empty section.
    /// </remarks>
    [Fact]
    public void A_types_own_role_counts_toward_its_span()
    {
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);

        var needTheirOwnRole = findings
            .Where(f => f.ValueOf("KindsThroughDependencies") < f.ValueOf("KindSpan"))
            .Select(f => core.Model.Find(f.Subject)!.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "AuthenticationMiddleware", "DocumentController", "QuoteController", "RateController",
                "TrackingController",
            ],
            needTheirOwnRole);

        var bridge = Assert.Single(findings, f => core.Model.Find(f.Subject)!.Name == "PolicyBridge");
        Assert.Equal(bridge.ValueOf("KindSpan"), bridge.ValueOf("KindsThroughDependencies"));
    }

    // ----------------------------------------------------- breaks alone, and §4's rows ----

    // ------------------------------------------------------- the rules, on the model ----

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
    /// Layer span emits rarest pattern first, which is the opposite direction from every other
    /// finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §3.1: <i>"rarer signatures sort first and get full detail."</i> Every other detector ranks
    /// by strength of evidence descending; here the rank is how few others share the pattern, so
    /// ascending is correct and the shared <c>Findings_are_emitted_in_a_total_order</c> theory
    /// cannot cover it — it asserts the descending shape.
    /// </para>
    /// <para>
    /// Without this the ordering could be deleted outright with the suite green, which was
    /// confirmed by deleting it. The discipline it encodes is the one <c>DEFECTS.md</c> §11 is
    /// about: an ordering that puts the boilerplate first is the inverse of interestingness.
    /// </para>
    /// </remarks>
    [Fact]
    public void Layer_span_emits_the_rarest_pattern_first()
    {
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);
        Assert.True(findings.Count > 1, "a single finding cannot exercise an ordering");

        // Ranks differ, so the sort is doing work rather than being satisfied by a constant.
        Assert.True(findings.Select(f => f.ValueOf("PatternGroupSize")).Distinct().Count() > 1);

        for (var i = 1; i < findings.Count; i++)
        {
            var (previous, current) = (findings[i - 1], findings[i]);
            var (before, after) = (previous.ValueOf("PatternGroupSize")!.Value, current.ValueOf("PatternGroupSize")!.Value);

            Assert.True(before <= after, $"{previous} sits in a commoner pattern than {current} and ranks above it");
            if (before != after) continue;

            // Within one pattern the members are equivalent by construction, so the order is a
            // tiebreak between things the finding does not distinguish — but it still has to be
            // total, or it is walk order wearing a sort.
            var (inbound, outbound) = (previous.ValueOf("FanIn")!.Value, current.ValueOf("FanIn")!.Value);
            Assert.True(inbound >= outbound, $"{previous} ranks below {current} on fan-in");
            if (inbound != outbound) continue;

            Assert.True(
                string.CompareOrdinal(previous.Subject.Canonical, current.Subject.Canonical) < 0,
                $"{previous} and {current} tie completely and are not in identity order");
        }
    }

    // --------------------------------------------------------------------- adapters ----

    /// <summary>
    /// Change cost's subjects under a different policy, from a second walk.
    /// </summary>
    /// <remarks>
    /// A walk rather than a re-render because cohort assignment reads <c>MinCohort</c>, so a
    /// policy that moves it is a different set of peer groups. Change cost has no cohort in it,
    /// which is the point being tested — but the comparison has to be honest about the walk.
    /// </remarks>
    private static List<string> ChangeCostUnder(AnalysisPolicy policy)
    {
        var model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None).GetAwaiter().GetResult();

        return Analysis.FindingsFor(model)
            .OfKind(FindingKind.ChangeCost)
            .Select(f => model.Find(f.Subject)!.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The surfaces that survive suppression under a different ceiling.</summary>
    /// <remarks>
    /// A re-walk rather than a re-render, for the same reason as <see cref="ChangeCostUnder"/>:
    /// cohort assignment reads the policy during the walk, so a policy is a model rather than a
    /// view.
    /// </remarks>
    private static List<string> BoundariesUnder(AnalysisPolicy policy)
    {
        var model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None).GetAwaiter().GetResult();

        return Analysis.FindingsFor(model)
            .OfKind(FindingKind.BoundaryCarriesLogic)
            .Select(f => model.Find(f.Subject)!.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>How many subjects share this one's layering pattern, itself included.</summary>
    private double? PatternSize(string typeName) => Spanning(typeName).ValueOf("PatternGroupSize");

    /// <summary>The named dependencies a spanning finding rests on.</summary>
    private List<string> ParticipantNames(string typeName) =>
        Spanning(typeName).Participants
            .Select(p => core.Model.Find(p)!.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

    private Finding Spanning(string typeName) =>
        Assert.Single(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers),
            f => core.Model.Find(f.Subject)!.Name == typeName);

    private Finding Hub(string typeName) =>
        Assert.Single(
            Analysis.FindingsFor(core.Model).OfKind(FindingKind.HubOrGodObject),
            f => core.Model.Find(f.Subject)!.Name == typeName);

    /// <summary>The member names a finding names as participants.</summary>
    private List<string> MemberNames(Finding finding) =>
        finding.Participants
            .Select(p => core.Model.Find(p.DeclaringType!)!.Members.Single(m => m.Subject == p).Name)
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
    /// The fixture ties, so the tiebreak above is exercised rather than merely present.
    /// </summary>
    /// <remarks>
    /// One pair of reconcilers ties at method level — 4.333 times their peer median — and a tie
    /// group is where a non-total sort key hides. Without one, deleting the <c>ThenBy</c> on
    /// identity would change nothing and the ordering test would still pass.
    /// Only method level ties: the type-level gap is recorded in <c>FixtureCoverageTests</c>.
    /// <para>
    /// There were two pairs until <see cref="AnalysisPolicy.ConcealedTopRank"/> landed. The
    /// 3.667x pair ranked fourth and fifth in its cohort and the rank gate refuses both, which
    /// takes the tie with it. One pair is still enough to exercise the tiebreak, but the margin
    /// is now one pair rather than two — if a later gate change takes this one too, the tiebreak
    /// stops being exercised and this test is what says so.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fixture_ties_method_level_findings()
    {
        var ranks = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.ConcealedDecisionMethod)
            .Select(f => f.ValueOf("CyclomaticXMedian"))
            .ToList();

        // Two pairs since P7, which is the margin going back up. The remark above records that it
        // had fallen to one and that losing the last one would leave the tiebreak unexercised; the
        // near-miss families put a second pair back without being built for it.
        Assert.Equal(2, ranks.Count - ranks.Distinct().Count());
    }
}

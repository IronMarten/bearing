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

    /// <summary>
    /// Method-level concealed decisions are the probe's, <b>less the ones a rank gate refuses</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The probe gates on the multiple of the peer median alone, and that is the defect measured
    /// in <c>MEASURE-concealed-decision.md</c>: a cohort median of 1 makes <c>3x median</c>
    /// evaluate to 3, so <c>MinDecisionCc</c> decides the nomination and the count grows with the
    /// size of the codebase. On nopCommerce it produced 1,091 nominations of which the report can
    /// show 15. Core adds <see cref="AnalysisPolicy.ConcealedTopRank"/>: a method must also be one
    /// of the three most complex in its cohort.
    /// </para>
    /// <para>
    /// The difference is asserted as a set, in one direction, for the same reason
    /// <see cref="Breaks_alone_diverges_from_the_probe_by_exactly_the_defect_fifteen_fix"/> does:
    /// a regression that emptied the finding would satisfy "Core says fewer" just as well.
    /// <c>AuditReconciler.Reconcile</c> and <c>TariffReconciler.Reconcile</c> both sit at 3.667x
    /// their peer median and both rank fourth or worse within it — the two the ratio admits and
    /// the rank refuses. Nothing is added, because a narrower gate cannot nominate.
    /// </para>
    /// </remarks>
    [Fact]
    public void Method_level_concealed_decisions_are_the_probes_less_what_rank_refuses()
    {
        var probeSaid = ProbeNominations("-- CONCEALED DECISION, METHOD LEVEL");
        var coreSays = CoreNominations(FindingKind.ConcealedDecisionMethod);

        Assert.NotEmpty(coreSays);
        Assert.Empty(coreSays.Except(probeSaid, StringComparer.Ordinal));
        Assert.Equal(
            ["AuditReconciler.Reconcile", "TariffReconciler.Reconcile"],
            probeSaid.Except(coreSays, StringComparer.Ordinal).Order(StringComparer.Ordinal));
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

        Assert.Equal(5, byMethod.Except(byType, StringComparer.Ordinal).Count());
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

    // -------------------------------------------------------------------- coverage ----

    /// <summary>
    /// Coverage reports exactly the population the probe reports: every type below the floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only finding whose population is defined by what could <i>not</i> be computed, so
    /// agreement here is agreement about the cohort assignment underneath rather than about a
    /// threshold. Invariant 8: silence must never read as a clean bill of health, and this is the
    /// section that makes the rest of the report readable.
    /// </para>
    /// <para>
    /// <b>And it is where <c>docs/DEFECTS.md</c> §1 finally shows up in a finding.</b> Core reports
    /// fourteen where the probe reports thirteen, because <c>TestBed.Shared.PayloadTag</c> is
    /// declared <c>partial</c> in two assemblies that do not reference each other. The probe keys
    /// types on the fully-qualified name alone and merges the two declarations into one row,
    /// summing their metrics; Core keys on <c>(assembly, FQN)</c> and keeps them apart. That is the
    /// one behaviour extraction is permitted to change, the fixture plants the collision
    /// deliberately, and until now it was visible only in the walk. A section whose entire job is
    /// completeness is the right place for it to surface.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_coverage_population_is_the_probes_plus_the_identity_collision()
    {
        var expected = ProbeNominations("   All types with no usable peer group, by fan-in:", stopAt: "NOTE:");
        var actual = CoreTypeNominations(FindingKind.Coverage);

        // Seventeen and eighteen since P6, which added four peerless types: its three shared
        // dependency targets, each the only member of the cohort it lands in, and RouteAttribute.
        // The eight *Conduit types are not among them — they are a name-suffix cohort of eight,
        // which is what keeps the plant out of every existing peer group.
        Assert.Equal(17, expected.Count);
        Assert.Equal(18, actual.Count);

        // Same components, and the extra entry is the second declaration rather than a new subject.
        Assert.Equal(expected, actual.Distinct(StringComparer.Ordinal).ToList());

        var collided = Analysis.FindingsFor(core.Model)
            .OfKind(FindingKind.Coverage)
            .Select(f => core.Model.Find(f.Subject)!)
            .Where(t => t.Name == "PayloadTag")
            .ToList();

        Assert.Equal(2, collided.Count);
        Assert.Equal(["Data", "Tools"], collided.Select(t => t.Assembly).Order(StringComparer.Ordinal));
        Assert.Single(collided.Select(t => t.FullyQualifiedName).Distinct(StringComparer.Ordinal));
    }

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
    /// <b>Defect 33, fixed.</b> The probe names two boundaries; Core names one, because the second
    /// is not unusual among boundaries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fifth deliberate divergence, and it applies this file's other half's rule to its
    /// first half.</b> §3.10 says a section prints only when it discriminates — the principle
    /// <see cref="BoundaryMarking.WidestSurfaces"/> was built on, twelve lines below a gate that
    /// never received it. On its own, <c>maxMemberCyclomatic &gt;= HighCc</c> fires on
    /// <b>19.5% of nopCommerce's 672 boundaries and 33.3% of jellyfin's 174</b>, and a claim made
    /// about a third of the population it filters is describing that population.
    /// </para>
    /// <para>
    /// <b>The floor stays, so this can still find nothing</b>, and the rank is what makes the
    /// selectivity explicit and equal across codebases. On the fixture the boundary population is
    /// small enough that the top share rounds to one, which is the same floor
    /// <see cref="Distribution.TopRankLimit"/> gives blast radius and change cost.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_boundary_set_is_narrower_than_the_probes_where_it_did_not_discriminate()
    {
        var expected = ProbeNominations("   BOUNDARIES CARRYING REAL LOGIC", stopAt: "WIDEST");
        var actual = CoreTypeNominations(FindingKind.BoundaryCarriesLogic);

        Assert.Equal(["ReconciliationController", "ShipmentController"], expected);
        Assert.Equal(["ShipmentController"], actual);
    }

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

    /// <summary>
    /// <b>Defect 12, fixed.</b> The probe names five surfaces; Core names none, because seven
    /// qualify.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fourth deliberate divergence, and the first where the probe's output is visibly the
    /// failure.</b> §3.10 says the section prints only when it discriminates, and the probe
    /// expresses that as a proportion — suppress when the qualifying set exceeds half the
    /// boundaries. It cannot fire at any boundary count, because the qualifying filter is
    /// <c>DataShape &gt;= 1.5 × median</c> and is therefore already bounded by the share the
    /// ceiling tests for. So the probe prints an arbitrary five of the seven qualifiers, silently
    /// truncated: a roll-call of controllers, which is the exact thing this section replaced.
    /// </para>
    /// <para>
    /// Core detects all seven and suppression removes the set as a set. The count is what bounds a
    /// list, and five is the number the probe's own <c>Take</c> already imposed — so the change is
    /// from a silent truncation to a stated silence.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_widest_surface_set_is_suppressed_where_the_probe_names_five()
    {
        var detected = Analysis.Detected(core.Model);
        var surfaces = detected.OfKind(FindingKind.WidestContractSurface);

        // Seven qualify, which is what P4 was planted to produce.
        Assert.Equal(7, surfaces.Count);
        Assert.True(surfaces.Count > core.Model.Policy.MaxNamedSurfaces);

        // And every one of them is withdrawn, by the row that has never been able to fire.
        Assert.All(
            surfaces,
            f => Assert.Equal(
                "widest-surface-is-not-discriminating",
                Suppression.Silencing(f, detected, core.Model)!.Name));

        Assert.Empty(Analysis.FindingsFor(core.Model).OfKind(FindingKind.WidestContractSurface));

        // The probe, meanwhile, names five of the seven and says nothing about the two it dropped.
        Assert.Equal(
            ["DocumentController", "QuoteController", "RateController", "ShipmentController", "TrackingController"],
            ProbeNominations("   WIDEST CONTRACT SURFACE"));
    }

    /// <summary>
    /// The ceiling is a gate in both directions, which the proportional form could never be.
    /// </summary>
    /// <remarks>
    /// Raising it past the qualifying set brings all seven back. That control is the whole
    /// difference between this and what it replaced: <c>KnownDefectTests</c> proves the old gate
    /// cannot fire at any boundary count or distribution, so it had no reachable other branch to
    /// test against.
    /// </remarks>
    [Fact]
    public void The_named_surface_ceiling_is_reachable_from_both_sides()
    {
        var raised = SurfacesUnder(core.Model.Policy with { MaxNamedSurfaces = 7 });
        Assert.Equal(7, raised.Count);
        Assert.Contains("ShipmentController", raised);

        // And one below the set is still suppression, so the boundary of the gate is the count
        // rather than anything about the types.
        Assert.Empty(SurfacesUnder(core.Model.Policy with { MaxNamedSurfaces = 6 }));
    }

    // ------------------------------------------------------------------ change cost ----

    /// <summary>
    /// Change cost diverges from the probe by removing the two the saturation evidence says do
    /// not belong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The third deliberate divergence, and the largest.</b> The probe gates on a kind filter
    /// plus an absolute fan-in floor, which on two real solutions ran to 4.5% and 7.9% of every
    /// type — 252 candidates on the larger, the worst saturation measured, and moving the wrong
    /// way as the codebase grew. Core adds a share-of-the-solution gate, so the finding is bounded
    /// by construction at any size rather than by a constant somebody hopes still holds.
    /// </para>
    /// <para>
    /// <c>ModelDescription</c> and <c>DispatchCallbackController</c> are the two that go, both at
    /// fan-in 5, both at solution rank 20.5. Five callers clears an absolute floor and is nowhere
    /// near the most-depended-on part of the application, which is the whole of what the
    /// conversion is for. The divergence is asserted as a set rather than as Core's output alone:
    /// a regression that emptied the finding would satisfy "Core says three" just as well.
    /// </para>
    /// <para>
    /// <b><c>LayeringEndpoint</c> is P6's, and it is a third divergence of the same kind.</b> Eight
    /// conduits reach it, so it clears the probe's absolute floor of five comfortably — and at
    /// solution midrank 9 of 144, in a tie of five types at fan-in 8, it is still outside Core's
    /// top slice of 7.2 at the default fraction. So the plant widened the probe's set and left
    /// Core's alone, which is the conversion doing exactly what it was converted for, on a type
    /// built for an unrelated reason.
    /// </para>
    /// </remarks>
    [Fact]
    public void Change_cost_diverges_from_the_probe_by_the_saturation_conversion()
    {
        var probeSaid = ProbeNominations("-- CHANGE COST");
        var coreSays = CoreTypeNominations(FindingKind.ChangeCost);

        Assert.Equal(
            [
                "DispatchCallbackController", "LayeringEndpoint", "ModelDescription",
                "NormalizationContext", "NormalizedResponse", "RawResponse",
            ],
            probeSaid);
        Assert.Equal(["NormalizationContext", "NormalizedResponse", "RawResponse"], coreSays);

        // In one direction only. A gate may narrow a finding, never widen it.
        Assert.Equal(
            ["DispatchCallbackController", "LayeringEndpoint", "ModelDescription"],
            probeSaid.Except(coreSays, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(coreSays.Except(probeSaid, StringComparer.Ordinal));
    }

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

        // The fan-in floor is. Raising it past the smallest survivor drops it.
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
        // Unchanged by P6: the default still nominates the three contracts, and LayeringEndpoint
        // is outside the slice at solution midrank 9 against a limit of 7.2.
        Assert.Equal(
            ["NormalizationContext", "NormalizedResponse", "RawResponse"],
            CoreTypeNominations(FindingKind.ChangeCost));

        // Loosening it admits the boundary the probe's absolute floor always accepted, which is
        // the arm §3.5 has and Core has never exercised at its default.
        foreach (var fraction in (double[])[0.10, 0.15])
            Assert.Equal(
                ["LayeringEndpoint", "NormalizationContext", "NormalizedResponse", "RawResponse"],
                ChangeCostUnder(core.Model.Policy with { ChangeCostTopFraction = fraction }));

        // And tightening it past the third survivor narrows the finding.
        Assert.Equal(
            ["NormalizationContext", "RawResponse"],
            ChangeCostUnder(core.Model.Policy with { ChangeCostTopFraction = 0.02 }));
    }

    // ------------------------------------------------ hubs, static state, layer span ----

    /// <summary>
    /// Hubs agree with the probe. The <b>disjunction</b> is what changes, not the population.
    /// </summary>
    /// <remarks>
    /// The subjects are read out of the rendered section rather than re-derived, with the standing
    /// note about routers and mediators dropped — <c>NominationText</c> warns that this section
    /// prints one and that its lines would otherwise read as nominations. The note itself is
    /// required by §3.8 and is a rendering concern, so nothing here asserts on it.
    /// </remarks>
    [Fact]
    public void Hub_nominations_are_the_probes()
    {
        var expected = ProbeNominations("-- HUBS AND GOD OBJECTS", stopAt: "NOTE:");
        var actual = CoreTypeNominations(FindingKind.HubOrGodObject);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
    }

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

    [Fact]
    public void Shared_mutable_state_nominations_are_the_probes()
    {
        var expected = ProbeNominations("-- SHARED MUTABLE STATE");
        var actual = CoreTypeNominations(FindingKind.SharedMutableState);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
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
    /// Layer span nominates exactly what the probe nominates, read off the probe's own model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one finding whose subjects cannot be read from the report.</b> Its section prints a
    /// collapsed pattern line rather than a list — <c>6 types span … Examples: …</c> — so there is
    /// no per-subject line to parse, and only four of the six are named at all. The probe's
    /// <c>KindSpan</c> column is the model surface that survives that: computed by
    /// <c>ComputeKindSpans</c>, frozen in <c>golden/types.verified.csv</c>, and therefore held by
    /// a golden rather than by this test.
    /// </para>
    /// <para>
    /// It is rendered first because the column is populated by <c>PrintNominations</c>. Reading it
    /// without that is an ordering dependence between tests, which is the shape of the defect this
    /// suite exists to catch.
    /// </para>
    /// </remarks>
    [Fact]
    public void Layer_span_nominations_are_the_probes()
    {
        NominationText.Render(probe.Result, Uncapped);

        var expected = probe.Result.Types
            .Where(t => t.KindSpan.Length > 0)
            .Where(t => t.KindSpan.Split('+').Length >= Uncapped.MinKindSpan)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var actual = CoreTypeNominations(FindingKind.SpansArchitecturalLayers);

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
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

    /// <summary>
    /// <b>Defect 11, fixed.</b> A layering pattern is a shared set of dependencies, not a shared
    /// count of kinds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The second deliberate divergence from the oracle</b>, and unlike the first it removes no
    /// claim: the nomination set is identical to the probe's — asserted above — and what changes is
    /// which subjects may have their detail collapsed. That is why the collapse is a qualifier
    /// rather than a suppression row.
    /// </para>
    /// <para>
    /// The probe groups on the kind signature, which assumes a shared signature means a shared
    /// phenomenon. All six fixture types carry <c>ApiBoundary+DataAccess+ExternalCall</c>, so all
    /// six are one group and the whole section collapses to a single line — including
    /// <c>AuthenticationMiddleware</c>, which is §3.1's own worked example of a component whose
    /// name has stopped describing it. The finding's entire output is a sentence about controllers
    /// being wired to a store.
    /// </para>
    /// <para>
    /// The repair comes from §3.1 rather than from taste: <i>the named dependencies per kind are
    /// the finding, not the count</i>. If the names are the finding, the names are what makes two
    /// findings the same finding. Four controllers reaching <c>TenantStore</c> and
    /// <c>CarrierGateway</c> genuinely are one fact; a middleware reaching <c>TenantStore</c> and
    /// <c>AuditClient</c> is a different one, and it keeps its detail.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_layering_pattern_is_a_shared_dependency_set_not_a_shared_kind_count()
    {
        NominationText.Render(probe.Result, Uncapped);
        var findings = Analysis.FindingsFor(core.Model).OfKind(FindingKind.SpansArchitecturalLayers);

        // What the probe groups on: one signature, every member, and X4 is open on the fact that
        // three significant kinds and a span floor of three leave no other signature possible.
        // P6 took this from six to fourteen and the probe still sees one group, which is the
        // clearest statement of §11 the fixture has — the plant deliberately contains two
        // different phenomena and the kind signature cannot tell them apart.
        Assert.Equal(
            14,
            probe.Result.Types.Count(t => t.KindSpan == "ApiBoundary+DataAccess+ExternalCall"));

        // What Core groups on. The anomaly and the bridge stand alone; the boilerplate is one fact.
        Assert.Equal(4, PatternSize("QuoteController"));
        Assert.Equal(4, PatternSize("DocumentController"));
        Assert.Equal(4, PatternSize("RateController"));
        Assert.Equal(4, PatternSize("TrackingController"));
        Assert.Equal(1, PatternSize("AuthenticationMiddleware"));
        Assert.Equal(1, PatternSize("PolicyBridge"));

        // And P6's two: six conduits that are one fact, and the ApiBoundary pair that is not part
        // of it. Same three dependencies, different role, different group.
        Assert.Equal(6, PatternSize("IntakeConduit"));
        Assert.Equal(2, PatternSize("PublicIntakeConduit"));

        // And the dependencies are why, which is the half a count cannot see: the middleware
        // shares the store with the controllers and reaches out somewhere else entirely.
        Assert.Equal(["CarrierGateway", "TenantStore"], ParticipantNames("QuoteController"));
        Assert.Equal(["AuditClient", "TenantStore"], ParticipantNames("AuthenticationMiddleware"));

        // The claim survives for every one of them. A collapse withdraws detail, never a finding —
        // the probe keeps collapsed types named in its examples line, which is the evidence for
        // reading row 4 as a sentence suppression rather than a finding suppression.
        Assert.Equal(14, findings.Count);
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
        Assert.Equal(["RoutingDepot", "SurchargeEvaluator", "TariffReconciler"], coreSays);

        // The difference is entirely the fix, in one direction only: Core removes one claim and
        // adds none. A suppression is allowed to silence, never to nominate.
        //
        // It removed two until ConcealedTopRank landed. TariffReconciler.Reconcile is no longer a
        // concealed decision — it ranks fourth in its cohort — so the suppression that hid this
        // row no longer reaches it, and Core now agrees with the probe about it. The suppression
        // did not change; the finding it keys on did.
        Assert.Equal(
            ["MethodReconciler"],
            probeSaid.Except(coreSays, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(coreSays.Except(probeSaid, StringComparer.Ordinal));

        // And it is the method-level nomination doing it, which is the whole of §15. It is not
        // nominated at type level either, so the probe's query could not have found it however it
        // was ordered.
        var detected = Analysis.Detected(core.Model);
        var methodReconciler = core.Model.Types.Single(t => t.Name == "MethodReconciler").Subject;

        Assert.True(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, methodReconciler));
        Assert.False(detected.Contains(FindingKind.ConcealedDecisionType, methodReconciler));

        // TariffReconciler is the control for the other direction, and it is why this test moved:
        // it has no method-level nomination for the suppression to key on, so nothing withdraws
        // its breaks-alone row and Core keeps it. If a future change re-nominates it, the row
        // disappears again and the set assertion above is what catches it.
        var tariffReconciler = core.Model.Types.Single(t => t.Name == "TariffReconciler").Subject;

        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, tariffReconciler));
        Assert.False(detected.Contains(FindingKind.ConcealedDecisionType, tariffReconciler));
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
    /// <para>
    /// <c>AuditReconciler</c> only became row 3's case when
    /// <see cref="AnalysisPolicy.ConcealedTopRank"/> landed. It ranks fourth in its cohort, so it
    /// is no longer a concealed decision, so row 2 no longer reaches it first — and the row-3-only
    /// case this test previously recorded as *missing* now exists. The rows did not change; the
    /// finding one of them keys on did.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_boundary_and_unreferenced_rows_are_not_the_same_rule()
    {
        var boundaryWithCallers = core.Model.Types.Single(t => t.Name == "ReconciliationController");
        Assert.Equal("ApiBoundary", boundaryWithCallers.Classification.Kind);
        Assert.True(boundaryWithCallers.FanIn >= core.Model.Policy.BreaksAloneMinFanIn);
        Assert.Equal("breaks-alone-at-a-boundary", SilencingRuleFor(boundaryWithCallers.Name));

        // And row 3 has a case it catches alone: AuditReconciler is unreferenced, not a boundary,
        // and no longer a concealed decision, so no earlier row reaches it.
        var unreferencedNonBoundary = core.Model.Types.Single(t => t.Name == "AuditReconciler");
        Assert.NotEqual("ApiBoundary", unreferencedNonBoundary.Classification.Kind);
        Assert.Equal(0, unreferencedNonBoundary.FanIn);
        Assert.Equal("breaks-alone-is-unreferenced", SilencingRuleFor(unreferencedNonBoundary.Name));
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

        Assert.Equal(1, ranks.Count - ranks.Distinct().Count());
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
    /// <param name="stopAt">
    /// The first word of a trailing standing note, where the section prints one. HUBS AND GOD
    /// OBJECTS closes with the note that routers and mediators legitimately live there, and its
    /// lines would otherwise be read as nominated subjects.
    /// </param>
    private List<string> ProbeNominations(string header, string? stopAt = null)
    {
        var subjects = NominationText.SubjectsUnder(
            NominationText.Render(probe.Result, Uncapped), header);

        return (stopAt is null
                ? subjects
                : subjects.TakeWhile(s => !string.Equals(s, stopAt, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

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

    private static List<string> SurfacesUnder(AnalysisPolicy policy)
    {
        var model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None).GetAwaiter().GetResult();

        return Analysis.FindingsFor(model)
            .OfKind(FindingKind.WidestContractSurface)
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

using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The suppression matrix — <c>TECHREQ-job-b.md</c> §4 — tested as behaviour rather than trusted
/// as ordering.
/// </summary>
/// <remarks>
/// <para>
/// Suppression is the part of Job B most likely to be lost in extraction and least likely to fail
/// loudly when it is. <b>A suppression that stops working produces more output, and more output
/// reads as a working tool.</b> Until the fixture had cases that fire, removing any of these rules
/// turned empty output into empty output and nothing failed.
/// </para>
/// <para>
/// <b>This file was rewritten at R2 rather than ported, and the reason is the whole point of the
/// exercise.</b> Every one of its twelve tests read the probe, and eleven of them read its
/// <i>rendered text</i> — the section a subject appeared under — because the probe's matrix
/// existed only as ordering and inline <c>Where</c> clauses inside a 997-line renderer, so there
/// was nothing else to ask. The old file said so in as many words: §4 requires suppression to
/// become a declared relationship between findings, evaluated before rendering, and
/// <c>FindingKey</c> is what it will be expressed against.
/// </para>
/// <para>
/// That happened. <c>Suppression.Rules</c> is the matrix as data, <c>Suppression.Silencing</c>
/// names the row that removed a finding, and rows 4 and 6 are qualifiers on the finding because
/// each silences a <i>sentence</i> rather than a claim. The tests written against that model grew
/// up inside <c>FindingTests</c>, under a heading that read "the rules, on the model",
/// while this file was still the one named after them. R2 moved them here, which is where a reader
/// looking for the suppression matrix will look.
/// </para>
/// <para>
/// <b>What is asserted has changed with them.</b> The old tests named a companion satisfying every
/// other condition of a finding and asserted its absence from a section; these assert which row
/// silenced it, by name. A finding removed for the wrong reason and a finding removed for the
/// right one are indistinguishable from the surviving set, and only one of them is a working
/// suppression.
/// </para>
/// <para>
/// Row 4's collapse and its threshold control live in <c>FixtureCoverageTests</c> beside the plant
/// that makes them reachable — <c>The_roll_call_collapse_fires_for_the_pattern_and_spares_the_pair</c>
/// and <c>The_roll_call_threshold_decides_in_both_directions</c>.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class SuppressionTests(CoreWalkFixture core)
{
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
    /// <summary>
    /// <c>Judge</c> keeps the row that silenced each finding, and agrees with <c>FindingsFor</c>
    /// about which survived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The attribution used to be computed and dropped on the floor.</b> <c>FindingsFor</c>
    /// called <c>Silencing</c> for its truth value and discarded the row, so nothing downstream
    /// could tell a finding removed for the right reason from one removed for the wrong reason —
    /// which is the failure <c>Silencing</c>'s own remark exists to warn about, and it was live.
    /// </para>
    /// <para>
    /// Asserted in three parts, because each fails differently: the reported halves agree, so
    /// adding <c>Judge</c> changed no output; something is actually suppressed, so the second half
    /// is not vacuous; and every silenced entry names a row that is really in the matrix, so an
    /// attribution cannot be invented.
    /// </para>
    /// </remarks>
    [Fact]
    public void Judge_reports_the_same_set_and_says_why_the_rest_went()
    {
        var judged = Analysis.Judge(core.Model);

        Assert.Equal(
            Analysis.FindingsFor(core.Model).All.Select(f => f.Key.Canonical).Order(StringComparer.Ordinal),
            judged.Where(j => j.IsReported).Select(j => j.Finding.Key.Canonical).Order(StringComparer.Ordinal));

        Assert.Equal(Analysis.Detected(core.Model).Count, judged.Count);

        var silenced = judged.Where(j => !j.IsReported).ToList();

        Assert.NotEmpty(silenced);
        Assert.All(silenced, j => Assert.Contains(j.SilencedBy!, Suppression.Rules));

        // A silenced finding names the row that took it, and the row is the one Silencing gives
        // when asked directly. Row 3's case is the one no earlier row would have claimed.
        var detention = silenced.Single(j => j.Finding.Subject.Canonical.EndsWith("DetentionEvaluator", StringComparison.Ordinal));

        Assert.Equal("breaks-alone-is-unreferenced", detention.SilencedBy!.Name);
    }

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
    /// The breaks-alone control survives on its own merits, and no longer on a live defect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaces a test that pinned the opposite.</b> <c>RoutingDepot</c> used to be the
    /// survivor, and it survived <i>because</i> <c>DEFECTS.md</c> §10 was live: its cohort of three
    /// stripped its concealed-decision nomination, so suppression row 2 had nothing to suppress
    /// with. The old test's own remark said the day §10 was fixed the survivor would leave and a
    /// replacement had to be planted <b>in the same change, not discovered afterwards</b>. This is
    /// that change; <c>SurchargeEvaluator</c> is that replacement, and it was planted long before
    /// and never pinned — the register recorded it as pinned and no test named it.
    /// </para>
    /// <para>
    /// The difference that matters: <c>SurchargeEvaluator</c> has a peer group of six, so its
    /// survival does not depend on any gate being wrong. It is simply not a concealed decision.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_breaks_alone_control_does_not_depend_on_a_defect()
    {
        var policy = core.Model.Policy;
        var detected = Analysis.Detected(core.Model);

        // The old survivor has left, which is §10 being fixed rather than a regression.
        var depot = core.Model.Types.Single(t => t.Name == "RoutingDepot");
        Assert.Equal(3, depot.CohortSize);
        Assert.True(
            detected.ContainsAbout(FindingKind.ConcealedDecisionType, depot.Subject)
            || detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, depot.Subject));
        Assert.NotNull(SilencingRuleFor("RoutingDepot"));

        // And the replacement survives with a peer group large enough that no floor is involved.
        var control = core.Model.Types.Single(t => t.Name == "SurchargeEvaluator");
        Assert.True(control.CohortSize >= policy.MinCohort);
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionType, control.Subject));
        Assert.False(detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, control.Subject));
        Assert.Null(SilencingRuleFor("SurchargeEvaluator"));
    }


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
    /// Row 7: no usable peer group, no relative claim. Invariants 6 and 8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Row 7 used to be implemented as <c>MinCohort</c> and it was doing more than row 7 asks.</b>
    /// <c>DEFECTS.md</c> §10: a floor of five silenced peer groups of three, which are small and
    /// real, and the silence sent those types into breaks alone instead — two findings
    /// contradicting each other about one component. What row 7 actually says is that a type with
    /// <i>no</i> peer group makes no relative claim, and that survives the fix.
    /// </para>
    /// <para>
    /// <b>Both halves are now enforced by arithmetic rather than by a constant</b>, which is why
    /// there is no replacement threshold to tune: <c>Distribution.Read</c> answers null below two
    /// values, and the dispersion gate cannot fire at two — with values <c>a &lt; b</c> the median
    /// is their midpoint and the MAD half their gap, so <c>median + k·MAD</c> exceeds <c>b</c> for
    /// every <c>k &gt; 1</c>. The three cases below are the three sizes that matters at.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_type_with_no_usable_peer_group_makes_no_relative_claim()
    {
        var policy = core.Model.Policy;
        var nominated = Analysis.FindingsFor(core.Model).OfKind(FindingKind.ConcealedDecisionType)
            .Select(f => f.Subject)
            .ToHashSet();

        // No peers at all. cc 18 and alone in kind:DataAccess, so nothing but the absence of a
        // group is stopping it — this is row 7 itself.
        var peerless = core.Model.Types.Single(t => t.Name == "OrderRepository");
        Assert.Equal(1, peerless.CohortSize);
        Assert.True(peerless.MaxMemberCyclomatic >= policy.MinDecisionCc);
        Assert.DoesNotContain(peerless.Subject, nominated);

        // One peer, so no TYPE-level claim: refused by the MAD arithmetic above rather than by a
        // floor, and so it holds whatever MinCohort is set to. It does earn a method-level one —
        // "the most complex of the 3 methods in the 2 types whose name ends in Tag" — and that is
        // not an inconsistency, it is the two arms reading two populations. The type arm compares
        // two types; the method arm compares three methods, which is a group large enough to have
        // spread. Selected by assembly because PayloadTag is one of the planted name collisions
        // and Data declares a second one at cc 2.
        var pair = core.Model.Types.Single(t => t.Name == "PayloadTag" && t.Assembly == "Tools");
        Assert.Equal(2, pair.CohortSize);
        Assert.True(pair.MaxMemberCyclomatic >= policy.MinDecisionCc);
        Assert.DoesNotContain(pair.Subject, nominated);

        // Two peers, and it does claim. This is the case §10 was about: the floor used to strip
        // this nomination and hand the type to breaks alone. §62 is what makes the claim honest at
        // this size — one of three reads as a third of its group, not as a rarity.
        var thin = core.Model.Types.Single(t => t.Name == "PricingVault");
        Assert.Equal(3, thin.CohortSize);
        Assert.Contains(thin.Subject, nominated);
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

    /// <summary>
    /// Row 7's control: move the floor under the cohort and the finding comes back.
    /// </summary>
    /// <remarks>
    /// <b>Carried over from the probe's suite, which is the one assertion in it that Core had no
    /// equivalent of.</b> The test above states that PricingVault is not nominated and that its
    /// cohort is below the floor; on its own that is an absence, and an absence is satisfied just
    /// as well by a detector that has stopped working. Lowering the floor and watching the same
    /// type come back is what makes it a statement about the gate.
    /// </remarks>
    [Fact]
    public void Lowering_the_cohort_floor_restores_the_suppressed_finding()
    {
        var lowered = core.WalkWith(core.Model.Policy with { MinCohort = 3 });
        var vault = lowered.Types.Single(t => t.Name == "PricingVault");

        Assert.Contains(
            Analysis.FindingsFor(lowered).OfKind(FindingKind.ConcealedDecisionType),
            f => f.Subject == vault.Subject);
    }
}

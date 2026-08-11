using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// The suppression matrix — <c>TECHREQ-job-b.md</c> §4 — tested as behaviour rather than trusted
/// as ordering.
/// </summary>
/// <remarks>
/// <para>
/// Suppression is the part of Job B most likely to be lost in extraction and least likely to
/// fail loudly when it is. A suppression that stops working produces <b>more</b> output, and
/// more output reads as a working tool. Until the fixture had cases that fire, removing any of
/// these rules turned empty output into empty output and nothing failed.
/// </para>
/// <para>
/// Each test below names a companion that satisfies <b>every other condition</b> of the finding,
/// and asserts both that it is absent and that the conditions it does meet are met. Without the
/// second half, a companion that quietly stopped qualifying for an unrelated reason would still
/// pass, and the suppression would be untested again without anyone noticing.
/// </para>
/// <para>
/// Ordering is currently load-bearing in the implementation: breaks alone captures the
/// concealed-decision nominations from earlier in the same method and tests membership. These
/// tests are what makes that safe to change — §4 requires suppression to become a declared
/// relationship between findings, evaluated before rendering, and <c>FindingKey</c> is what it
/// will be expressed against.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class SuppressionTests(FixtureRun run)
{
    /// <summary>Row 1: never imply safety at a boundary. Invariant 4.</summary>
    /// <remarks>
    /// The probe cannot see external consumers, so "if it breaks, it breaks alone" is the one
    /// claim it must not make about a type on the outside edge. A tool that says "safe to
    /// remove" about something six customers depend on has caused the burn it claimed to
    /// prevent.
    /// </remarks>
    [Fact]
    public void Breaks_alone_is_suppressed_at_a_boundary()
    {
        var boundary = Type("ReconciliationController");

        // Everything except Kind says it qualifies.
        Assert.Equal("ApiBoundary", boundary.Kind);
        Assert.True(boundary.FanIn >= 1);
        Assert.True(boundary.Instability >= 0.8);
        Assert.True(boundary.MaxMemberCyclomatic >= run.Options.HighCc);

        Assert.DoesNotContain("ReconciliationController", BreaksAlone());
    }

    /// <summary>Row 2: never contradict yourself about one component. Invariant 3.</summary>
    /// <remarks>
    /// Structural isolation is not safety when a component <i>decides</i> something — a
    /// normalizer that picks the wrong option propagates into the data going out the door, not
    /// through the call graph. Saying "breaks alone" and "this is making business judgements"
    /// about one type discredits both.
    /// </remarks>
    [Fact]
    public void Breaks_alone_is_suppressed_for_a_concealed_decision()
    {
        var concealed = Type("RateReconciler");

        Assert.Equal("Internal", concealed.Kind);
        Assert.True(concealed.FanIn >= 1);
        Assert.True(concealed.Instability >= 0.8);
        Assert.True(concealed.MaxMemberCyclomatic >= run.Options.HighCc);

        // It is genuinely nominated as a concealed decision, which is the reason for the
        // suppression rather than a coincidence.
        Assert.Contains("RateReconciler", ConcealedDecisions());
        Assert.DoesNotContain("RateReconciler", BreaksAlone());
    }

    /// <summary>Row 3: fan-in of zero is unreferenced code, not reassurance.</summary>
    [Fact]
    public void Breaks_alone_is_suppressed_when_nothing_references_it()
    {
        var orphan = Type("AuditReconciler");

        Assert.Equal("Internal", orphan.Kind);
        Assert.Equal(0, orphan.FanIn);
        Assert.True(orphan.Instability >= 0.8);
        Assert.True(orphan.MaxMemberCyclomatic >= run.Options.HighCc);

        // And it is not suppressed by row 2 instead — that would make this test pass for the
        // wrong reason.
        Assert.DoesNotContain("AuditReconciler", ConcealedDecisions());
        Assert.DoesNotContain("AuditReconciler", BreaksAlone());
    }

    /// <summary>Row 4: a signature shared by many types is a layering pattern. Invariant 2.</summary>
    /// <remarks>
    /// The roll-call discipline, applied to layer spans: six near-identical blocks teach nothing
    /// and cost the section its readers, so past <c>--top / 3</c> the group collapses to one line.
    /// Before this plant the fixture had a single spanning type and the branch had never run —
    /// it could have been deleted with the goldens staying byte-identical.
    /// </remarks>
    [Fact]
    public void Spans_layers_collapses_a_signature_shared_by_too_many_types()
    {
        var text = Render();

        Assert.Contains(
            "6 types span ApiBoundary+DataAccess+ExternalCall — a layering pattern",
            text, StringComparison.Ordinal);

        // And the per-type detail is genuinely gone, rather than the summary being printed
        // alongside it.
        Assert.DoesNotContain(
            "AuthenticationMiddleware [ApiBoundary] — reaches across",
            text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Row 4's control. The floor is <c>--top / 3</c>, so lifting <c>--top</c> to 18 puts it at
    /// exactly 6 and the six-member group stops exceeding it.
    /// </summary>
    /// <remarks>
    /// The control has to move a threshold rather than build a second group: there are exactly
    /// three <c>SignificantKinds</c> and <c>--min-kind-span</c> is 3, so every spanning type
    /// necessarily carries the same signature and a second group cannot exist at defaults.
    /// </remarks>
    [Fact]
    public void Raising_top_restores_the_per_type_layer_span_detail()
    {
        var text = Render(new Options { Top = 18 });

        Assert.Contains(
            "AuthenticationMiddleware [ApiBoundary] — reaches across 3 kinds",
            text, StringComparison.Ordinal);
        Assert.DoesNotContain("a layering pattern rather than an", text, StringComparison.Ordinal);
    }

    /// <summary>Row 6: "plumbing" is an absolute claim, so an absolute floor decides it.</summary>
    /// <remarks>
    /// <para>
    /// The only row of the seven that suppresses a <i>claim inside</i> a finding rather than the
    /// finding itself. The nomination fires either way; what the floor decides is whether the
    /// sentence is allowed to say "looks like plumbing".
    /// </para>
    /// <para>
    /// It has to, because the selection filter is relative — <c>FanInXMedian &lt;= 2.0</c> — and in
    /// a cohort where everything is heavily used, ordinary for its peers still means widely
    /// depended on. ThroughputGauge is exactly that: fan-in 5, and fan-in 5 is also the cohort
    /// median. Relative says unremarkable, absolute says five callers, and only one of those two
    /// readings can be put in front of a developer without being laughed at.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_plumbing_wording_is_suppressed_above_the_fan_in_floor()
    {
        var gauge = Type("ThroughputGauge");

        Assert.True(gauge.FanIn >= run.Options.MinFanIn);
        Assert.True(gauge.FanInXMedian <= 2.0);

        // The finding itself is not suppressed — asserting only the absence below would pass
        // just as well if the nomination had stopped firing altogether.
        Assert.Contains("ThroughputGauge", ConcealedDecisions());

        Assert.Contains(
            "ThroughputGauge.Sample — connectivity is unremarkable for its peers",
            Render(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ThroughputGauge.Sample — looks like plumbing",
            Render(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Row 6's control. Raise the floor past the same type and the plumbing wording comes back,
    /// which is what proves the branch is live rather than merely unvisited.
    /// </summary>
    [Fact]
    public void Raising_the_fan_in_floor_restores_the_plumbing_wording()
    {
        Assert.Contains(
            "ThroughputGauge.Sample — looks like plumbing",
            Render(new Options { MinFanIn = 6 }), StringComparison.Ordinal);
    }

    /// <summary>
    /// And the contrast: below the floor the plumbing wording is accurate, and still used. Both
    /// branches are reachable on one fixture at one setting.
    /// </summary>
    [Fact]
    public void Below_the_fan_in_floor_the_plumbing_wording_still_applies()
    {
        Assert.True(Type("RateReconciler").FanIn < run.Options.MinFanIn);

        Assert.Contains(
            "RateReconciler.Reconcile — looks like plumbing",
            Render(), StringComparison.Ordinal);
    }

    /// <summary>Row 7: no peer group means no relative claim. Invariants 6 and 8.</summary>
    /// <remarks>
    /// <para>
    /// PricingVault is planted so the cohort floor is the only thing between it and a
    /// concealed-decision nomination — every other condition is asserted below, so absence here
    /// can only be the gate. Nothing already in the fixture could do this job: OrderRepository
    /// sits in a cohort of two, but its outlier factor is 1.8 against a floor of 3.0, so it is
    /// excluded twice over and a test written on it would have passed for the wrong reason.
    /// </para>
    /// <para>
    /// The claim being suppressed is <i>comparative</i> — "far above its peers" — and three peers
    /// is not a distribution. The type is not silently dropped: it surfaces under NO PEER GROUP
    /// with a solution-wide reading and that weaker basis stated, which is invariant 8.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_cohort_relative_finding_is_suppressed_below_the_cohort_floor()
    {
        var vault = Type("PricingVault");

        // Every other condition of CONCEALED DECISION is met.
        Assert.True(vault.MaxMemberCyclomatic >= run.Options.MinDecisionCc);
        Assert.True(vault.MaxMemberCyclomaticXMedian >= run.Options.OutlierFactor);
        Assert.True(vault.FanInXMedian <= 2.0);
        Assert.True(vault.FanOutXMedian <= 2.0);

        // And this one is not.
        Assert.True(vault.CohortSize < run.Options.MinCohort);

        Assert.DoesNotContain("PricingVault", ConcealedDecisions());
    }

    /// <summary>
    /// Row 7's control, and the reason the test above is about the gate rather than about
    /// absence: move the floor under the cohort and the finding comes back.
    /// </summary>
    /// <remarks>
    /// Nothing else changes — the metrics were computed once, at defaults, and only the
    /// eligibility filter differs. A suppression that stopped working would show up here as the
    /// finding appearing at both settings; one that started over-firing would show up as neither.
    /// </remarks>
    [Fact]
    public void Lowering_the_cohort_floor_restores_the_suppressed_finding()
    {
        Assert.Contains("PricingVault", ConcealedDecisions(new Options { MinCohort = 3 }));
    }

    /// <summary>
    /// The control: with the three suppressions accounted for, the finding still fires on the
    /// type it should.
    /// </summary>
    /// <remarks>
    /// A suppression suite that only asserts absence would pass just as happily if the finding
    /// were deleted outright.
    /// </remarks>
    [Fact]
    public void Breaks_alone_still_fires_on_the_type_that_earns_it()
    {
        Assert.Contains("TariffReconciler", BreaksAlone());
    }

    private TypeMetrics Type(string name) =>
        run.Result.Types.Single(t => t.Name == name);

    /// <summary>
    /// The rendered nominations. Row 6 is the one row that has to be asserted against wording,
    /// because wording is what it suppresses — there is no model surface carrying the distinction
    /// between "plumbing" and "unremarkable for its peers", and that absence is itself part of
    /// what extraction has to fix.
    /// </summary>
    private string Render() => Render(run.Options);

    private string Render(Options policy) => NominationText.Render(run.Result, policy);

    private string[] BreaksAlone() =>
        NominationText.SubjectsUnder(
            NominationText.Render(run.Result, run.Options), "-- BREAKS ALONE");

    /// <summary>
    /// Type-level concealed-decision subjects. The section renders <c>Type.Member</c>, so the
    /// subject is trimmed back to the type.
    /// </summary>
    private string[] ConcealedDecisions() => ConcealedDecisions(run.Options);

    /// <summary>
    /// The same, under a different threshold policy. The metrics were computed once at defaults;
    /// only the eligibility filter inside <c>PrintNominations</c> differs, which is what lets a
    /// gate be tested by moving it rather than by asserting an absence.
    /// </summary>
    private string[] ConcealedDecisions(Options policy) =>
        NominationText.SubjectsUnder(
                NominationText.Render(run.Result, policy), "-- CONCEALED DECISION -")
            .Select(s => s.Split('.')[0])
            .ToArray();
}

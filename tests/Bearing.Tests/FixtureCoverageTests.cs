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

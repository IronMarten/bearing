using ArchProbe;

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
public sealed class FixtureCoverageTests(FixtureRun run)
{
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
    /// Still uncovered: suppression row 5. Rows 4, 6 and 7 are covered by
    /// <c>SuppressionTests</c> — including the SPANS roll-call collapse — and the two defects
    /// they exposed are pinned in <c>KnownDefectTests</c>: the cohort floor stripping row 2's
    /// suppression off breaks alone, and the layer-span collapse hiding the anomaly it shares a
    /// signature with.
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

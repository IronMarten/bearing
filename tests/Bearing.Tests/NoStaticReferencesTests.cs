using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// Dead code — <c>TECHREQ-job-a.md</c> §5.6, at the member level X5 chose, and A9 layer 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>§5.6's acceptance criterion is the first test here, and the second one records that it is
/// weaker than it reads.</b> The criterion is that a DI-registered type, a reflection-resolved type
/// and a test-only type are none of them reported as unreferenced without their category named.
/// All three pass — and every one of them passes by being <i>externally visible</i>, which is the
/// exclusion X15 settled, not by any of the handling §5.6 was describing. It was written for a
/// type-level pass, where the three plants are the whole story; at member level the visibility rule
/// reaches them first. So the criterion no longer exercises the DI, reflection or test-only
/// handling at all, and layer 3's traps have to be non-public to mean anything.
/// </para>
/// <para>
/// <b>Invariant 4 gets its test here too</b>, and it is owed rather than new:
/// <c>TECHREQ-job-a.md</c> §7's table names it as a Job A acceptance criterion — <i>no output
/// contains "safe to delete/remove"</i> — and nothing asserted it until there was a finding that
/// could violate it.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class NoStaticReferencesTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    private string Text =>
        string.Join("\n", IronMarten.Bearing.Cli.Report.For(core.Model, Findings));

    /// <summary>§5.6's acceptance criterion, literally: none of the three plants is nominated.</summary>
    /// <remarks>
    /// <c>AuditPolicySink</c> is registered by a convention scan that names no type,
    /// <c>SchemaMigrationHandler</c> is named only by a string literal, and <c>FixtureBuilder</c> is
    /// used only from the skipped test project. All three have a type-level fan-in of zero and all
    /// three are legitimate.
    /// </remarks>
    [Theory]
    [InlineData("AuditPolicySink")]
    [InlineData("SchemaMigrationHandler")]
    [InlineData("FixtureBuilder")]
    public void The_three_dead_code_plants_are_not_nominated(string type)
    {
        var subjects = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => f.Subject.Canonical)
            .ToList();

        Assert.DoesNotContain(subjects, s => s.Contains($".{type}|", StringComparison.Ordinal));
    }

    /// <summary>
    /// And every one of them is excluded by visibility, not by its own category.
    /// </summary>
    /// <remarks>
    /// <b>Asserted so that the weakness is on the record rather than in a comment.</b> If a future
    /// change made any of these members non-public, this test fails and says why — at which point
    /// the DI, reflection and test-only handling has to actually exist, which is layer 3. Without
    /// this, §5.6's criterion would read as evidence that those categories work.
    /// </remarks>
    [Theory]
    [InlineData("AuditPolicySink")]
    [InlineData("SchemaMigrationHandler")]
    [InlineData("FixtureBuilder")]
    public void The_three_plants_are_excluded_by_visibility_rather_than_by_category(string type)
    {
        var members = core.Model.Types.Single(t => t.Name == type).Members
            .Where(m => m.InboundReferenceCount == 0)
            .ToList();

        Assert.NotEmpty(members);
        Assert.All(members, m => Assert.True(
            m.IsExternallyVisible || m.ImplementsInterface,
            $"{m.Signature} is neither externally visible nor an interface implementation, "
            + "so §5.6's criterion is now testing something it was not testing before"));
    }

    /// <summary>The disclosure adds up, and it is the reason a short list is trustworthy.</summary>
    /// <remarks>
    /// The four categories overlap and are counted independently, so they are deliberately not
    /// summed. What must reconcile is considered = excluded + nominated, and the nominated half has
    /// to be the number of findings actually emitted — a disclosure that disagreed with the list
    /// beside it would be worse than none.
    /// </remarks>
    [Fact]
    public void The_exclusion_counts_reconcile_with_the_findings()
    {
        var excluded = NoStaticReferences.Excluded(core.Model);
        var nominated = Findings.OfKind(FindingKind.NoStaticReferences).Count;

        Assert.Equal(excluded.Considered, excluded.Excluded + excluded.Nominated);
        Assert.Equal(nominated, excluded.Nominated);
        Assert.True(excluded.Excluded > 0, "nothing was excluded, so the section is not filtering");
    }

    /// <summary>The section states the counts, so the reader can calibrate the list.</summary>
    [Fact]
    public void The_section_discloses_what_it_set_aside()
    {
        var excluded = NoStaticReferences.Excluded(core.Model);

        Assert.Contains($"{excluded.Considered} members had no inbound reference", Text, StringComparison.Ordinal);
        Assert.Contains("visible outside this assembly", Text, StringComparison.Ordinal);
        Assert.Contains("implements an interface", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Invariant 4: no output implies that removing anything is safe.
    /// </summary>
    /// <remarks>
    /// <b><c>TECHREQ-job-a.md</c> §7's table, asserted at last.</b> §5.6 forbids the word "dead"
    /// outright and forbids implying safety, and this is the finding that could break both. The
    /// list is the vocabulary a reader would take as permission — checked over the whole report
    /// rather than over the section, because the highlights and the coverage line quote it too.
    /// </remarks>
    [Theory]
    [InlineData("safe to delete")]
    [InlineData("safe to remove")]
    [InlineData("dead code")]
    [InlineData("unused")]
    [InlineData("unreachable")]
    public void The_report_never_implies_that_removing_something_is_safe(string forbidden)
    {
        Assert.DoesNotContain(forbidden, Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The prescribed label is used, and it is used on the claim itself.</summary>
    [Fact]
    public void The_label_is_the_one_the_spec_prescribes()
    {
        Assert.Contains("no static references found — verify before deleting", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A private field that is initialised and never read is nominated.
    /// </summary>
    /// <remarks>
    /// <b>The positive case, and the fixture has it by accident rather than by design.</b> P6 and
    /// P7 gave their plants dependency fields to manufacture edges, and never read them — so
    /// <c>LabelDepot._normalize</c> is genuine unused state, of exactly the kind a compiler warning
    /// would flag. That the detector finds it is the check that the exclusions have not eaten
    /// everything.
    /// <para>
    /// <b>A known under-report sits beside it.</b> The same field written in a constructor rather
    /// than at its declaration <i>would</i> carry an inbound reference, because a write is a
    /// reference like any other and <c>EdgeKind</c> does not separate reads from writes. Both
    /// fields are equally unread; only one is nominated. That is layer 3's problem and it is a
    /// false negative rather than a false positive, which is the right way round for this feature.
    /// </para>
    /// </remarks>
    [Fact]
    public void An_initialised_but_never_read_private_field_is_nominated()
    {
        var subjects = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => f.Subject.Canonical)
            .ToList();

        Assert.Contains(subjects, s => s.EndsWith("F:TestBed.Core.Depots.LabelDepot._normalize", StringComparison.Ordinal));
    }

    /// <summary>
    /// A type's only accessible constructor is set aside, and a sibling overload is not.
    /// </summary>
    /// <remarks>
    /// <b>Chris's call, on the measurement: a counted exclusion rather than a caveat.</b> A type
    /// with one accessible constructor and no caller for it is what registration looks like from
    /// inside the solution, and on nopCommerce that was 24 of 29 nominations — a section that is
    /// five-sixths one systematic pattern is invariant 1's failure however carefully each row is
    /// worded. The precision is in "only": a container picks one constructor, so a type offering
    /// several has siblings nothing was ever going to call, and those stay.
    /// </remarks>
    [Fact]
    public void A_sole_accessible_constructor_is_set_aside()
    {
        var nominated = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => f.Subject.Canonical)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var type in core.Model.Types)
        {
            var accessible = type.Members
                .Where(m => m.Kind == MemberKind.Constructor && !m.IsStatic
                            && m.Accessibility != "Private")
                .ToList();

            if (accessible.Count != 1) continue;

            Assert.DoesNotContain(accessible[0].Subject.Canonical, nominated);
        }
    }

    /// <summary>The exclusion is counted, and the section says so.</summary>
    [Fact]
    public void The_sole_constructor_exclusion_is_counted()
    {
        var excluded = NoStaticReferences.Excluded(core.Model);

        Assert.True(excluded.SoleConstructors > 0, "the fixture declares no sole-constructor types");
        Assert.Contains("the type's only constructor", Text, StringComparison.Ordinal);
    }
}

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
        string.Join("\n", IronMarten.Bearing.Cli.Report.For(core.Model, Analysis.Judge(core.Model)));

    /// <summary>The default page — one finding per kind.</summary>
    private string Page =>
        HtmlReport.Render(core.Model, Analysis.Judge(core.Model), Instant);

    /// <summary>The page with every section enumerated — <c>--full</c>.</summary>
    private string FullPage =>
        HtmlReport.Render(core.Model, Analysis.Judge(core.Model), Instant, full: true);

    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
    /// Invariant 4: no rendered output implies that removing anything is safe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>TECHREQ-job-a.md</c> §7's table, asserted at last.</b> §5.6 forbids the word "dead"
    /// outright and forbids implying safety, and this is the finding that could break both. The
    /// list is the vocabulary a reader would take as permission — checked over the whole report
    /// rather than over the section, because the highlights and the coverage line quote it too.
    /// </para>
    /// <para>
    /// <b>Over every prose surface, not just the terminal.</b> It ran on <c>Report.For</c> alone
    /// until 2026-08-26, which left the invariant with the highest stated stakes — §8, *the one
    /// whose violation would do real damage* — guarded on one of the two renderers that can
    /// violate it. <c>TESTING.md</c> names renderer divergence as **the** thing to watch, and
    /// records two defects a week apart that were both of that shape.
    /// </para>
    /// <para>
    /// <b>Both HTML shapes, and the <c>--full</c> one is the one that earns its place.</b> The
    /// default page renders one finding per kind, so a violating sentence on any finding that is
    /// not a lead would not appear in it at all. The terminal needs no such pair — it has no
    /// <c>full</c> parameter and always enumerates.
    /// </para>
    /// <para>
    /// <b>This is a fixture-scoped guard and cannot be anything else.</b> It reads the rendered
    /// string, so a real solution declaring a type named <c>UnusedEntryPruner</c> would trip it
    /// while the tool implied nothing. What it holds is that Bearing's own vocabulary stays clear
    /// of these five words, which is the half that is Bearing's to control.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("safe to delete")]
    [InlineData("safe to remove")]
    [InlineData("dead code")]
    [InlineData("unused")]
    [InlineData("unreachable")]
    public void No_render_ever_implies_that_removing_something_is_safe(string forbidden)
    {
        Assert.DoesNotContain(forbidden, Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, Page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, FullPage, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// A type whose data members are mostly unread is named once, not once per member.
    /// </summary>
    /// <remarks>
    /// <b>Grouped, never dropped.</b> Core still emits one finding per member — so <c>--json</c>,
    /// <c>--csv</c> and <c>--full</c> keep every one — and the qualifier decides only how a
    /// renderer groups them. Asserted on both halves, because a collapse that quietly stopped
    /// emitting the members would look identical in the terminal.
    /// </remarks>
    [Fact]
    public void An_unread_carrier_is_named_once_and_its_members_are_still_emitted()
    {
        var grouped = Findings.OfKind(FindingKind.NoStaticReferences)
            .Where(f => f.Holds(Qualifiers.PartOfAnUnreadGroup))
            .ToList();

        // AuditReconciler declares four dependency fields and reads none of them.
        var audit = grouped
            .Where(f => f.Subject.Canonical.Contains(".AuditReconciler|", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(4, audit.Count);
        Assert.Contains("AuditReconciler — 4 of its 5 data members have no reader", Text, StringComparison.Ordinal);

        // ... and the claim itself names the group, asserted on the wording rather than through
        // the report. START HERE can lead with any of these — it takes one exemplar per kind — and
        // a lone-member sentence there would contradict the grouped row below it. Which finding
        // gets picked is Selection's business and changes with the fixture; that the sentence is
        // right is this test's.
        var worded = Claims.For(core.Model, audit[0]);

        Assert.Contains(
            "4 of AuditReconciler's 5 data members have no reader, so read them as a group",
            worded.Sentence,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A lone unread data member stays a member-level claim.
    /// </summary>
    /// <remarks>
    /// The half that makes the rule mean something. <c>MatroskaConstants</c> on Jellyfin has one
    /// unread field of seventeen and is a genuine candidate; <c>SearchResult</c> has seventeen of
    /// twenty-three and is a carrier. <b>Stated as "more than one" with no threshold</b>, because
    /// 74%, 42% and 30% are a continuum and any cut through it would be a number nobody could
    /// defend.
    /// </remarks>
    [Fact]
    public void A_lone_unread_data_member_is_not_collapsed()
    {
        var lonely = Findings.OfKind(FindingKind.NoStaticReferences)
            .Where(f => !f.Holds(Qualifiers.PartOfAnUnreadGroup))
            .Select(f => f.Subject.Canonical)
            .ToList();

        // DepthGauge reads none of its one dependency field, and has only the one.
        Assert.Contains(lonely, s => s.EndsWith("DepthGauge._normalize", StringComparison.Ordinal));
    }

    /// <summary>
    /// The member-level categories, planted where they can be reached and asserted one by one.
    /// </summary>
    /// <remarks>
    /// <b>Non-public on purpose</b> — <c>DeadCodeTraps.cs</c>'s three type-level plants all pass by
    /// being externally visible, so a public member-level trap would test nothing. These are
    /// <c>tests/TestBed/Core/DeadCodeMemberTraps.cs</c>, and they do not all end the same way:
    /// two must not be nominated, one must be nominated with its category named, and one is
    /// nominated with nothing but the standing caveat because that is this tool's honest limit.
    /// </remarks>
    [Fact]
    public void An_override_is_excluded_and_a_wired_handler_is_simply_referenced()
    {
        var nominated = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => f.Subject.Canonical)
            .ToList();

        // The override: callers reach it through the base declaration, so it is excluded.
        Assert.DoesNotContain(nominated, s => s.EndsWith("SettlementProbe.Sample", StringComparison.Ordinal));

        // The += handler: a method group is an ordinary reference and needs no handling at all.
        // If this ever fires, method-group references have stopped resolving.
        Assert.DoesNotContain(nominated, s => s.EndsWith("SettlementProbe.OnSettled", StringComparison.Ordinal));

        // And the base virtual IS nominated, correctly: nothing calls it, so neither it nor the
        // override ever runs. The exclusion is for overrides, not for everything near one.
        Assert.Contains(nominated, s => s.EndsWith("TallyProbe.Sample", StringComparison.Ordinal));
    }

    /// <summary>
    /// A serialisation callback is nominated with its category named, never bare.
    /// </summary>
    /// <remarks>
    /// <b>The gap the plant found, and §5.6's bar stated exactly.</b> A private
    /// <c>[OnDeserialized]</c> method is non-public, so no other exclusion reaches it, and before
    /// the attribute qualifier existed it appeared with nothing said about why it might be
    /// reachable — which is the criterion's own words: not reported as unreferenced <i>without its
    /// category named</i>.
    /// </remarks>
    [Fact]
    public void A_serialisation_callback_is_nominated_with_its_category_named()
    {
        var callback = Assert.Single(
            Findings.OfKind(FindingKind.NoStaticReferences),
            f => f.Subject.Canonical.EndsWith("SettlementProbe.AfterLoad(System.Runtime.Serialization.StreamingContext)",
                StringComparison.Ordinal));

        Assert.True(callback.Holds(Qualifiers.AnAttributeMayDirectIt));
        Assert.Contains("whatever an attribute on it directs there", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A method named only by a string literal is nominated, and that is the recorded limit.
    /// </summary>
    /// <remarks>
    /// <b>Asserted so the limit is a decision rather than an oversight.</b> §5.6 specifies string
    /// literals matching <i>type</i> names, which are long and distinctive. Member names are
    /// neither — matching <c>"Name"</c> or <c>"Add"</c> against every member called that would
    /// rescue half a codebase on a coincidence — so the handling is deliberately not extended to
    /// them. What protects the reader is the label: verify before deleting.
    /// </remarks>
    [Fact]
    public void A_string_dispatched_method_is_nominated_and_that_is_the_recorded_limit()
    {
        var nominated = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => f.Subject.Canonical)
            .ToList();

        Assert.Contains(nominated, s => s.EndsWith("SettlementProbe.OnReplayed", StringComparison.Ordinal));
    }

    /// <summary>
    /// The findings arrive strongest first, which is <c>FindingSet</c>'s contract.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not this detector's preference — the set's stated contract</b>, which every other
    /// detector already honoured: <i>"each detector emits in a total order of its own — strongest
    /// evidence first, broken by identity — and nothing here re-sorts them"</i>. A9 shipped
    /// emitting in model order, and because <c>Selection.Exemplars</c> takes the first of each
    /// kind, the second-most-prominent line of the nopCommerce report became whichever type sorted
    /// first alphabetically: a one-line property on a plugin's DTO, ahead of a fifteen-line private
    /// method nothing calls.
    /// </para>
    /// <para>
    /// <b>Size is the measure because this kind is cohort-free</b> — hubs sorts on
    /// <c>min(fan-in, fan-out)</c> for the same reason — and every nomination has an inbound count
    /// of zero, so the number that fired cannot discriminate. It is also the number the row prints:
    /// X10's follow-up found two sections sorting on one number and showing another, and concluded
    /// that an order the reader cannot see is not an order that helps them.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_findings_arrive_biggest_first()
    {
        var members = core.Model.Types
            .SelectMany(t => t.Members)
            .ToDictionary(m => m.Subject.Canonical, m => m, StringComparer.Ordinal);

        var sizes = Findings.OfKind(FindingKind.NoStaticReferences)
            .Select(f => members[f.Subject.Canonical].LinesOfCode)
            .ToList();

        Assert.NotEmpty(sizes);
        Assert.Equal(sizes.OrderByDescending(n => n), sizes);

        // And the order discriminates on this fixture rather than being vacuously sorted, which a
        // population of equal-sized members would be.
        Assert.True(sizes.Distinct().Count() > 1, "every nomination is the same size, so the order proves nothing");
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

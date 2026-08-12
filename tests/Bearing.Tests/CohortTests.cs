using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Peer-group assignment: the substrate every comparative finding rests on.
/// </summary>
/// <remarks>
/// Extraction can break this without breaking a single finding's condition — nothing throws,
/// no gate changes, every finding still fires, and they are all now comparing against the wrong
/// group. Which is why it is tested directly and against the cases that motivated it, rather
/// than through a solution that happens to exhibit them.
/// </remarks>
public sealed class CohortTests
{
    private const int MinCohort = 5;

    [Fact]
    public void The_most_specific_viable_group_wins_not_the_largest()
    {
        // The rule the whole design turns on. Largest-wins always picks the namespace, since it
        // is the most inclusive candidate available — which collapses every cohort into one and
        // makes every percentile meaningless.
        var subjects = Enumerable.Range(0, 5)
            .Select(i => Subject($"T{i}", Impl("INormalizer"), Ns("App")))
            .Concat(Enumerable.Range(5, 5).Select(i => Subject($"T{i}", Ns("App"))))
            .ToList();

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        Assert.Equal("impl:INormalizer", cohorts["T0"].Key);
        Assert.Equal("interface", cohorts["T0"].Basis);
        Assert.Equal(5, cohorts.SizeOf("T0"));

        // ns:App had ten candidates and still lost, because it is less specific.
        Assert.Equal("ns:App", cohorts["T9"].Key);
    }

    [Fact]
    public void A_group_below_the_floor_is_not_viable_however_specific()
    {
        var subjects = Enumerable.Range(0, 4)
            .Select(i => Subject($"T{i}", Impl("IRare"), Ns("App")))
            .Concat(Enumerable.Range(4, 6).Select(i => Subject($"T{i}", Ns("App"))))
            .ToList();

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        // Four is below the floor, so the interface cannot support a reading and everyone
        // falls back to the namespace.
        Assert.Equal("ns:App", cohorts["T0"].Key);
        Assert.Equal(10, cohorts.SizeOf("T0"));
    }

    [Fact]
    public void A_type_stranded_by_candidate_counts_is_rehomed()
    {
        // The motivating defect. Nine types share the Normalizer suffix, so the suffix looks
        // viable to all nine; eight of them also share an interface and leave for it; the ninth
        // is left in a cohort of one, where every relative statistic compares it against
        // itself and it reports as exactly median no matter how extreme it is.
        var subjects = new List<CohortSubject>();
        for (var i = 0; i < 8; i++)
            subjects.Add(Subject($"Impl{i}", Impl("INormalizer"), Suffix("Normalizer"), Ns("App")));

        subjects.Add(Subject("Lonely", Suffix("Normalizer"), Ns("App")));

        for (var i = 0; i < 5; i++)
            subjects.Add(Subject($"Other{i}", Ns("App")));

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        Assert.Equal("impl:INormalizer", cohorts["Impl0"].Key);
        Assert.Equal("ns:App", cohorts["Lonely"].Key);
        Assert.True(cohorts.SizeOf("Lonely") >= MinCohort);
    }

    [Fact]
    public void Strandees_that_can_move_together_are_counted_together()
    {
        // Potential size, not current size. A more specific cohort forming can starve a coarser
        // one below the floor, and then every stranded type is stuck because none of them can
        // move into a group that no longer qualifies. Counting fellow strandees is what breaks
        // the deadlock — and several arriving at once is usually exactly what happens.
        var subjects = new List<CohortSubject>();

        // Three types each alone in their own suffix group, all sharing a namespace.
        subjects.Add(Subject("A", Suffix("Alpha"), Ns("Shared")));
        subjects.Add(Subject("B", Suffix("Bravo"), Ns("Shared")));
        subjects.Add(Subject("C", Suffix("Charlie"), Ns("Shared")));

        // Two more sit in the namespace already — short of the floor of five on their own.
        subjects.Add(Subject("D", Ns("Shared")));
        subjects.Add(Subject("E", Ns("Shared")));

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        // ns:Shared held only two when the strandees were looking. Counting the three of them
        // arriving takes it to five, which is what makes the move legal for any of them.
        foreach (var id in new[] { "A", "B", "C", "D", "E" })
            Assert.Equal("ns:Shared", cohorts[id].Key);

        Assert.Equal(5, cohorts.SizeOf("A"));
    }

    [Fact]
    public void A_type_with_genuinely_no_peers_is_left_alone_and_reported_as_such()
    {
        // Not every stranding is fixable, and inventing a group would be worse. The tool says
        // "no peer group" and compares against the whole solution instead.
        var subjects = Enumerable.Range(0, 6)
            .Select(i => Subject($"T{i}", Ns("App")))
            .Append(Subject("Alone", Ns("Isolated")))
            .ToList();

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        Assert.Equal("ns:Isolated", cohorts["Alone"].Key);
        Assert.Equal(1, cohorts.SizeOf("Alone"));
    }

    [Fact]
    public void Assignment_is_total_and_the_sizes_add_up()
    {
        var subjects = Enumerable.Range(0, 20)
            .Select(i => Subject($"T{i}", Impl($"I{i % 3}"), Suffix("Handler"), Ns($"N{i % 4}")))
            .ToList();

        var cohorts = CohortSet.Assign(subjects, MinCohort);

        Assert.All(subjects, s => Assert.False(string.IsNullOrEmpty(cohorts[s.Id].Key)));
        Assert.Equal(subjects.Count, cohorts.Sizes.Values.Sum());
    }

    [Fact]
    public void A_subject_with_no_candidates_is_a_programming_error()
    {
        // Derivation always yields the namespace, which is what makes assignment total. A
        // subject without one is a bug upstream, and failing loudly beats inventing a group.
        var ex = Assert.Throws<ArgumentException>(() =>
            CohortSet.Assign([new CohortSubject("T", [])], MinCohort));

        Assert.Contains("no cohort candidates", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- derivation ----

    [Theory]
    [InlineData("OrderNormalizer", false, "Normalizer")]
    [InlineData("IOrderNormalizer", true, "Normalizer")] // leading I stripped, so it groups with the above
    [InlineData("Normalizer", false, null)]              // single word is not a useful cohort
    [InlineData("INormalizer", true, null)]              // ...and stripping the I leaves one
    [InlineData("OrderDb", false, null)]                 // trailing word too short to mean anything
    [InlineData("Order", true, null)]
    public void Trailing_word_is_the_last_pascal_case_word(string name, bool isInterface, string? expected) =>
        Assert.Equal(expected, CohortCandidates.TrailingWord(name, isInterface));

    [Fact]
    public void An_unbroken_acronym_run_yields_no_suffix()
    {
        // A word boundary is an upper-case letter following a lower-case one, so HTTPClient
        // reads as a single word and gets no suffix candidate at all. That is the conservative
        // failure — it declines to group rather than grouping wrongly — and it means such a
        // type falls back to its namespace. Recorded because it is surprising, not because it
        // is wrong.
        Assert.Null(CohortCandidates.TrailingWord("HTTPClient", false));
        Assert.Equal("Client", CohortCandidates.TrailingWord("HttpClient", false));
    }

    [Fact]
    public void Marker_interfaces_do_not_group_anything()
    {
        // Every disposable type implementing IDisposable is evidence that they hold resources,
        // not that they are peers. A cohort built on one is a cross-section of the codebase.
        var candidates = CohortCandidates.For(new TypeShape(
            "OrderNormalizer", "App", false,
            ["System.IDisposable", "App.INormalizer"], null));

        Assert.DoesNotContain(candidates, c => c.Key.Contains("IDisposable", StringComparison.Ordinal));
        Assert.Contains(candidates, c => c.Key == "impl:App.INormalizer");
    }

    [Fact]
    public void Derivation_always_yields_the_namespace_last()
    {
        var candidates = CohortCandidates.For(new TypeShape("X", "", false, [], null));

        var only = Assert.Single(candidates);
        Assert.Equal("ns:<global>", only.Key);
        Assert.Equal(CohortBasis.Namespace, only.Precedence);
    }

    [Fact]
    public void Candidates_come_back_most_specific_first()
    {
        var candidates = CohortCandidates.For(new TypeShape(
            "OrderNormalizer", "App", false, ["App.INormalizer"], "App.NormalizerBase"));

        Assert.Equal(
            [CohortBasis.Interface, CohortBasis.BaseType, CohortBasis.NameSuffix, CohortBasis.Namespace],
            candidates.Select(c => c.Precedence));
    }

    [Fact]
    public void The_catch_all_kind_is_not_a_peer_group()
    {
        // Internal is no more meaningful than the namespace it would displace.
        Assert.Null(CohortCandidates.ForArchitecturalKind("Internal"));
        Assert.Null(CohortCandidates.ForArchitecturalKind(""));

        var kind = CohortCandidates.ForArchitecturalKind("DataAccess");
        Assert.Equal("kind:DataAccess", kind!.Value.Key);
        Assert.Equal(CohortBasis.ArchitecturalKind, kind.Value.Precedence);
    }

    // ------------------------------------------------------------------ helpers ----

    private static CohortSubject Subject(string id, params CohortCandidate[] candidates) =>
        new(id, candidates);

    private static CohortCandidate Impl(string name) => new("impl:" + name, "interface", CohortBasis.Interface);

    private static CohortCandidate Suffix(string name) => new("suffix:" + name, "name suffix", CohortBasis.NameSuffix);

    private static CohortCandidate Ns(string name) => new("ns:" + name, "namespace", CohortBasis.Namespace);
}

/// <summary>
/// Core's cohort assignment against the probe's, over the whole fixture.
/// </summary>
/// <remarks>
/// <para>
/// This was partial until Core had a walk of its own — the probe keeps its derived candidates
/// in a local, so there was no way to feed both halves the same input. Now each side derives
/// its own candidates from the same solution and the outcome is compared type for type, which
/// covers the derivation and the assignment together.
/// </para>
/// <para>
/// Cohort assignment is population-sensitive: every type's group depends on how many other
/// types could join it. So this is a stronger check than it looks — Core assigning one type
/// differently would usually move several.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class CohortEquivalenceTests(CoreWalkFixture core, FixtureRun probe)
{
    /// <summary>The planted collision, which Core keeps as two types and the probe merges.</summary>
    private const string CollidingName = "global::TestBed.Shared.PayloadTag";

    [Fact]
    public void Every_type_lands_in_the_same_cohort_as_the_probe_put_it_in()
    {
        var byName = core.Model.Types
            .Where(t => t.FullyQualifiedName != CollidingName)
            .ToDictionary(t => t.FullyQualifiedName, StringComparer.Ordinal);

        var compared = 0;
        foreach (var p in probe.Result.Types.Where(t => t.Id != CollidingName))
        {
            Assert.Equal(p.Cohort, byName[p.Id].Cohort.Key);
            Assert.Equal(p.CohortBasis, byName[p.Id].Cohort.Basis);
            compared++;
        }

        Assert.Equal(probe.Result.Types.Count - 1, compared);
    }

    [Fact]
    public void Cohort_sizes_match_except_where_the_collision_changes_the_population()
    {
        var probeSizes = probe.Result.Types
            .GroupBy(t => t.Cohort, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var collidedCohorts = core.Model.Types
            .Where(t => t.FullyQualifiedName == CollidingName)
            .Select(t => t.Cohort.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var type in core.Model.Types.Where(t => !collidedCohorts.Contains(t.Cohort.Key)))
            Assert.Equal(probeSizes[type.Cohort.Key], type.CohortSize);

        // Where the collision lands, Core has one more member than the probe — it is counting
        // two declarations the probe folded into one. That is DEFECTS.md §1 reaching the
        // population every percentile in that cohort is taken against.
        Assert.NotEmpty(collidedCohorts);
        foreach (var cohort in collidedCohorts)
            Assert.Equal(probeSizes[cohort] + 1, core.Model.Types.Count(t => t.Cohort.Key == cohort));
    }

    [Fact]
    public void Every_type_has_a_cohort()
    {
        Assert.All(core.Model.Types, t =>
        {
            Assert.False(string.IsNullOrEmpty(t.Cohort.Key));
            Assert.False(string.IsNullOrEmpty(t.Cohort.Basis));
            Assert.True(t.CohortSize >= 1);
        });

        Assert.Equal(core.Model.Types.Count, core.Model.Types.Sum(t => 1));
    }

    [Fact]
    public void The_bases_the_fixture_exercises_are_all_reached()
    {
        // A basis nothing exercises is a branch no test covers. The fixture was built to reach
        // all of them; this asserts it still does rather than assuming.
        var bases = core.Model.Types.Select(t => t.Cohort.Basis).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("interface", bases);
        Assert.Contains("name suffix", bases);
        Assert.Contains("namespace", bases);
        Assert.Contains("architectural kind", bases);
    }
}

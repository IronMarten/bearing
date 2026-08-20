using ArchProbe;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Core computes the same numbers as the probe, on the real fixture.
/// </summary>
/// <remarks>
/// <para>
/// This is the extraction gate that matters. <c>OracleGoldenTests</c> asks whether the probe's
/// bytes moved; this asks whether the reimplementation agrees with it, which is the question
/// phase 1 actually poses. Core is a rewrite rather than a port, so agreement is a result and
/// not a tautology — every assertion here is a place the two could differ and do not.
/// </para>
/// <para>
/// It also documents the two places they differ <b>on purpose</b>. Both are Core refusing to
/// state a number that has no basis, per <c>docs/ARCHITECTURE.md</c> §3: a peer group of one
/// has no reading, and a project with no cross-project coupling has no instability. The probe
/// computes 50 and 1.0 in the first case and relies on the CSV writer to blank them, which is
/// the rule living in a renderer — where every other renderer misses it.
/// </para>
/// <para>
/// The adapters at the bottom are how the probe's flat accumulators are read on Core's terms.
/// They shrink as more of the report moves, and the prose parser that used to sit beside them
/// is already gone — see the note on project coupling.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class CoreEquivalenceTests(FixtureRun run, CoreWalkFixture core)
{
    // ------------------------------------------------------------ type cohorts ----

    public static TheoryData<string> TypeDimensions =>
        ["FanIn", "FanOut", "Cyclomatic", "MaxMemberCyclomatic", "Dsm", "DataShape"];

    [Theory]
    [MemberData(nameof(TypeDimensions))]
    public void Cohort_percentiles_match_the_probe(string dimension)
    {
        var compared = 0;

        foreach (var cohort in run.Result.Types.GroupBy(t => t.Cohort, StringComparer.Ordinal))
        {
            var members = cohort.ToList();
            if (members.Count < 2) continue;   // no reading at all — asserted separately

            var distribution = Distribution.Of(members.Select(m => ValueOf(m, dimension)));

            foreach (var m in members)
            {
                var reading = distribution.Read(ValueOf(m, dimension));

                Assert.NotNull(reading);
                Assert.Equal(ProbePercentile(m, dimension), reading.Value.Percentile);
                compared++;
            }
        }

        Assert.True(compared > 0, $"no comparable cohort exercised {dimension}");
    }

    [Theory]
    [InlineData("FanIn")]
    [InlineData("FanOut")]
    [InlineData("Cyclomatic")]
    [InlineData("MaxMemberCyclomatic")]
    [InlineData("Dsm")]
    public void Cohort_multiples_of_the_median_match_the_probe(string dimension)
    {
        // DataShape is absent on purpose: the probe computes a percentile for it and no
        // multiple, so there is nothing to compare against.
        foreach (var cohort in run.Result.Types.GroupBy(t => t.Cohort, StringComparer.Ordinal))
        {
            var members = cohort.ToList();
            if (members.Count < 2) continue;

            var distribution = Distribution.Of(members.Select(m => ValueOf(m, dimension)));

            foreach (var m in members)
            {
                var reading = distribution.Read(ValueOf(m, dimension));

                Assert.NotNull(reading);
                Assert.Equal(ProbeTimesMedian(m, dimension), reading.Value.TimesMedian);
            }
        }
    }

    [Fact]
    public void Method_cohort_readings_match_the_probe()
    {
        var compared = 0;

        foreach (var cohort in run.Result.Methods.GroupBy(m => m.Cohort, StringComparer.Ordinal))
        {
            var members = cohort.ToList();
            if (members.Count < 2) continue;

            var cc = Distribution.Of(members.Select(m => (double)m.Cyclomatic));
            var dsm = Distribution.Of(members.Select(m => (double)m.Dsm));

            foreach (var m in members)
            {
                Assert.Equal(m.CyclomaticPctl, cc.PercentileOf(m.Cyclomatic));
                Assert.Equal(m.CyclomaticXMedian, cc.TimesMedianOf(m.Cyclomatic));
                Assert.Equal(m.DsmPctl, dsm.PercentileOf(m.Dsm));
                Assert.Equal(m.DsmXMedian, dsm.TimesMedianOf(m.Dsm));
                compared++;
            }
        }

        Assert.True(compared > 0, "no comparable method cohort in the fixture");
    }

    [Fact]
    public void Solution_wide_percentiles_match_the_probe()
    {
        // The "no peer group" fallback: a type with no cohort still gets compared against the
        // whole solution, and says so. 108 types, so the distribution is always comparable.
        var fanIn = Distribution.Of(run.Result.Types.Select(t => (double)t.FanIn));
        var maxCc = Distribution.Of(run.Result.Types.Select(t => (double)t.MaxMemberCyclomatic));

        foreach (var t in run.Result.Types)
        {
            Assert.Equal(t.GlobalFanInPctl, fanIn.PercentileOf(t.FanIn));
            Assert.Equal(t.GlobalMaxCcPctl, maxCc.PercentileOf(t.MaxMemberCyclomatic));
        }
    }

    // ------------------------------------------------- the deliberate divergence ----

    [Fact]
    public void A_cohort_of_one_has_no_reading_in_Core_where_the_probe_computes_fifty()
    {
        var singletons = run.Result.Types.Where(t => t.CohortSize == 1).ToList();
        Assert.NotEmpty(singletons);

        foreach (var t in singletons)
        {
            var distribution = Distribution.Of(new[] { (double)t.Cyclomatic });

            // Core: no basis, so no number.
            Assert.Null(distribution.Read(t.Cyclomatic));

            // The probe: a number, and a meaningless one — the single member ties with itself
            // at midrank 50 and divides by its own median for a ratio of 1.0.
            Assert.Equal(50.0, t.CyclomaticPctl);
            Assert.Equal(1.0, t.CyclomaticXMedian);
        }
    }

    [Fact]
    public void The_divergence_is_invisible_in_rendered_output()
    {
        // Which is why it is safe to land before the renderers move: every relative statistic
        // the probe computes for a cohort of one is already blanked by the CSV writer, so Core
        // refusing to compute it changes no byte of any current artifact. It changes what the
        // JSON and HTML renderers will be able to get wrong.
        var path = Path.Combine(Path.GetTempPath(), $"bearing-equiv-{Guid.NewGuid():N}.csv");
        string csv;
        try
        {
            Report.WriteTypesCsv(path, run.Result.Types);
            csv = File.ReadAllText(path);
        }
        finally
        {
            File.Delete(path);
        }

        var lines = csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        var pctl = Array.IndexOf(lines[0].Split(','), "CyclomaticPctl");
        Assert.True(pctl >= 0, "types.csv has no CyclomaticPctl column");

        var singleton = run.Result.Types.First(t => t.CohortSize == 1);

        // Id is the last column, and Esc may have quoted it.
        var row = lines.Single(l =>
            l.EndsWith("," + singleton.Id, StringComparison.Ordinal)
            || l.EndsWith(",\"" + singleton.Id + "\"", StringComparison.Ordinal));

        Assert.Equal(string.Empty, row.Split(',')[pctl]);
    }

    // --------------------------------------------------------- project coupling ----

    /// <summary>
    /// Project coupling over the fixture, with the answers written down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to read the probe's own sentence and parse the numbers back out of it, because
    /// there was no model surface to compare against and that absence was the defect. There is
    /// one now, on both sides: Core walks the solution itself, and <c>WalkerEquivalenceTests</c>
    /// already establishes that its types and edges are the probe's. Coupling is a pure function
    /// of those two and the function has its own tests, so re-deriving the probe's numbers from
    /// prose proves nothing composition does not — while keeping a regex over report text alive
    /// in the suite.
    /// </para>
    /// <para>
    /// What replaces it is the fixture's known answers, stated rather than parsed. These are
    /// checked figures rather than a snapshot: <c>Core</c> is stable and concrete because
    /// everything depends on it and it depends on nothing, and the two leaf projects each reach
    /// into it while nothing reaches into them.
    /// </para>
    /// </remarks>
    [Fact]
    public void Project_coupling_over_the_fixture_is_what_it_should_be()
    {
        var coupling = CoreProjectCoupling().ToDictionary(c => c.Project, StringComparer.Ordinal);

        Assert.Equal(["Core", "Data", "Tools"], coupling.Keys.Order(StringComparer.Ordinal));

        var analysed = coupling["Core"];
        Assert.Equal(2, analysed.TypesElsewhereReachingIn);      // Data and Tools each reach in
        Assert.Equal(0, analysed.TypesHereReachingOut);          // and it reaches out to neither
        Assert.Equal(6, analysed.AbstractTypes);
        Assert.Equal(155, analysed.TotalTypes);          // 128 before P6's twelve, 140 before P7's fifteen
        Assert.Equal(0, analysed.Instability);                   // maximally stable
        Assert.Equal(MainSequenceZone.Pain, analysed.Zone);      // stable and concrete

        foreach (var leaf in new[] { coupling["Data"], coupling["Tools"] })
        {
            Assert.Equal(0, leaf.TypesElsewhereReachingIn);
            Assert.Equal(1, leaf.TypesHereReachingOut);
            Assert.Equal(0, leaf.AbstractTypes);
            Assert.Equal(1, leaf.Instability);                   // maximally unstable
            Assert.Equal(MainSequenceZone.NearMainSequence, leaf.Zone);
        }
    }

    [Fact]
    public void The_collision_fix_reaches_the_project_totals()
    {
        // The probe credits both halves of the planted cross-project collision to whichever
        // project loaded first, so it sees two types in Data and two in Tools. Core attributes
        // each declaration to the project that declares it, which is one more type in Data —
        // and abstractness is a share of that total, so the fix does not stop at the type row.
        // docs/DEFECTS.md §1.
        var coupling = CoreProjectCoupling().ToDictionary(c => c.Project, StringComparer.Ordinal);

        Assert.Equal(3, coupling["Data"].TotalTypes);
        Assert.Equal(2, coupling["Tools"].TotalTypes);
        Assert.Equal(run.Result.Types.Count(t => t.Project == "Data") + 1, coupling["Data"].TotalTypes);
    }

    // Read from the model rather than assembled here. When the test built the tuples itself it
    // was asserting that ProjectCoupling computes correctly from ids the test chose, which is a
    // weaker claim than the section needs: the renderer will read SolutionModel, so that is the
    // path the fixture's known answers have to hold on.
    private IReadOnlyList<ProjectCoupling> CoreProjectCoupling() => core.Model.ProjectCouplings;

    [Fact]
    public void A_project_with_no_cross_project_coupling_has_no_instability()
    {
        // The probe writes "no cross-project coupling" and moves on, so the absence is stated
        // in prose and lost to every other renderer. Zero would be a lie that places every
        // isolated project in the zone of pain.
        var isolated = Assert.Single(ProjectCoupling.ForSolution(
            [("A.T1", "A", false), ("A.T2", "A", true)],
            [("A.T1", "A.T2")]));

        Assert.Null(isolated.Instability);
        Assert.Null(isolated.DistanceFromMainSequence);
        Assert.Equal(MainSequenceZone.None, isolated.Zone);
        Assert.Equal(0.5, isolated.Abstractness);   // still measurable, and still reported
    }

    /// <summary>
    /// The section is a function of the analysis and not of the order it arrived in.
    /// </summary>
    /// <remarks>
    /// <c>OrderingTests</c> makes this argument for the probe's artifacts and it applies here for
    /// the same reason: the counts accumulate into dictionaries keyed by project, so nothing but
    /// the final <c>OrderBy</c> stops the rows being positioned by insertion order. Asserted on a
    /// synthetic solution rather than the fixture, because three projects in name order would
    /// still pass if the sort were removed.
    /// </remarks>
    [Fact]
    public void Project_coupling_does_not_depend_on_enumeration_order()
    {
        (string, string, bool)[] types =
            [("Z.T1", "Z", false), ("A.T1", "A", true), ("Z.T2", "Z", true), ("M.T1", "M", false)];
        (string, string)[] edges = [("Z.T1", "A.T1"), ("A.T1", "M.T1"), ("Z.T2", "M.T1")];

        static IEnumerable<(string, int, int, int, int)> Shape(IEnumerable<ProjectCoupling> couplings) =>
            couplings.Select(c =>
                (c.Project, c.TypesElsewhereReachingIn, c.TypesHereReachingOut, c.AbstractTypes, c.TotalTypes));

        Assert.Equal(
            Shape(ProjectCoupling.ForSolution(types, edges)),
            Shape(ProjectCoupling.ForSolution(types.Reverse(), edges.Reverse())));
    }

    // ------------------------------------------------------- circular references ----

    /// <summary>
    /// The fixture's one namespace cycle, stated rather than parsed.
    /// </summary>
    /// <remarks>
    /// The golden pins the probe's rendering of the same four namespaces, so agreement here is
    /// agreement with the probe. <c>GraphTests</c> is where the algorithms are compared directly.
    /// </remarks>
    [Fact]
    public void Namespace_cycles_over_the_fixture_are_what_they_should_be()
    {
        var cycle = Assert.Single(core.Model.NamespaceCycles);

        Assert.Equal(
            [
                SubjectRef.ForNamespace("TestBed.Core"),
                SubjectRef.ForNamespace("TestBed.Core.Depots"),
                SubjectRef.ForNamespace("TestBed.Core.Pricing"),
                SubjectRef.ForNamespace("TestBed.Core.Vaults"),
            ],
            cycle.Members);
    }

    [Fact]
    public void Type_tangles_over_the_fixture_are_what_they_should_be()
    {
        var tangle = Assert.Single(core.Model.TypeTangles);

        var names = tangle.Members
            .Select(m => core.Model.Find(m)!.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "AccessorialNormalizer", "AddressNormalizer", "RateNormalizer", "ReferenceNormalizer",
                "Router", "ShipmentCoordinator", "TrackingNormalizer", "TransitNormalizer",
            ],
            names);
    }

    /// <summary>
    /// The type tangle's loop is a walk somebody can follow: every step is an edge that exists,
    /// and the last one closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A3's half of circular references, and the assertion that matters most about it. A path is
    /// the one thing here a reader will act on directly — they will open the first file and look
    /// for the reference to the second — so a path with a step that is not really there sends
    /// them looking for something that does not exist. Membership can be checked against the
    /// probe; a path cannot, because the probe has none.
    /// </para>
    /// <para>
    /// Asserted over the type graph rather than the namespace graph because the type graph is the
    /// one the model carries edges for: a namespace edge is derived, and re-deriving it here to
    /// check it would be asserting this computation against a second copy of itself.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_step_of_a_tangles_loop_is_a_real_dependency()
    {
        var tangle = Assert.Single(core.Model.TypeTangles);

        Assert.NotEmpty(tangle.Path);
        Assert.Equal(tangle.Path.Count, tangle.Path.Distinct().Count());
        Assert.All(tangle.Path, step => Assert.Contains(step, tangle.Members));

        for (var i = 0; i < tangle.Path.Count; i++)
        {
            var from = core.Model.Find(tangle.Path[i])!;
            var to = tangle.Path[(i + 1) % tangle.Path.Count];

            Assert.True(
                from.Outbound.Contains(to),
                $"{from.Name} does not depend on {core.Model.Find(to)!.Name}, so the loop is not walkable.");
        }
    }

    /// <summary>
    /// The fixture's cycles are both larger than their loops, so the disclosure arm is what it
    /// exercises — and the covering arm is not exercised here at all.
    /// </summary>
    /// <remarks>
    /// Recorded as an assertion rather than left to the snapshot because it is a fixture gap and
    /// not a property of the tool: <c>docs/TESTING.md</c> §6. A component whose shortest loop
    /// visits every member renders a line with no qualifier on it, and nothing in TestBed
    /// produces one — <c>GraphTests</c> and <c>ProjectCycleTests</c> are where that arm is
    /// covered. If a plant ever makes this fail, the fixture got better and this is the note to
    /// delete.
    /// </remarks>
    [Fact]
    public void Neither_of_the_fixtures_loops_covers_its_whole_component()
    {
        Assert.All(core.Model.NamespaceCycles, c => Assert.False(c.PathCoversEveryMember));
        Assert.All(core.Model.TypeTangles, c => Assert.False(c.PathCoversEveryMember));
    }

    /// <summary>
    /// The fixture has no project cycle, and that is the correct answer rather than an empty one.
    /// </summary>
    /// <remarks>
    /// Every cross-project edge in a solution that builds normally follows a project reference,
    /// and MSBuild forbids those from cycling — so the aggregate is the reference DAG.
    /// <c>ProjectCycleTests</c> constructs the shape that does cycle, because no plant in TestBed
    /// can: it needs an analysed assembly reached some way other than a project reference, which
    /// is a property of a build rather than of a source file.
    /// </remarks>
    [Fact]
    public void The_fixture_has_no_project_cycle()
    {
        Assert.Empty(core.Model.ProjectCycles);
    }

    /// <summary>
    /// The collision fix does not reach the cycles, and cannot on this fixture.
    /// </summary>
    /// <remarks>
    /// Asserted so that the gap is a statement rather than an omission. Core keeps two
    /// <c>PayloadTag</c> rows where the probe merges them, and both have fan-in 0 — a node with
    /// no inbound edges is in no strongly-connected component, so merged and split give the same
    /// partition. S2 therefore agrees with the probe for a reason that has nothing to do with S2
    /// being right. <c>docs/DEFECTS.md</c> §1, <c>TASKS.md</c> P8.
    /// </remarks>
    [Fact]
    public void The_collision_is_invisible_to_the_cycles_and_that_is_why_P8_exists()
    {
        var collided = core.Model.Types
            .Where(t => t.FullyQualifiedName == "global::TestBed.Shared.PayloadTag")
            .ToList();

        Assert.Equal(2, collided.Count);
        Assert.All(collided, t => Assert.Equal(0, t.FanIn));

        var entangled = core.Model.TypeTangles.SelectMany(c => c.Members).ToHashSet();
        Assert.All(collided, t => Assert.DoesNotContain(t.Subject, entangled));
    }

    // ------------------------------------------------------- unreferenced projects ----

    [Fact]
    public void Unreferenced_projects_over_the_fixture_are_what_they_should_be()
    {
        // Data and Tools each reach into Core and nothing reaches into either. Core itself is
        // depended on by both, so it is not a candidate whatever else is true of it.
        Assert.Equal(
            ["Data", "Tools"],
            core.Model.UnreferencedProjects.Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A root is not dead, and each exclusion carries its own case.
    /// </summary>
    /// <remarks>
    /// The fixture cannot show this. Its two unreferenced projects are plain libraries with no
    /// entry point and no boundary, so all three exclusions are dead against it and deleting any
    /// of them leaves the section unchanged — TESTING.md §9. The control is the last row: the same
    /// project with none of the three applying, which has to appear or the theory above would pass
    /// on a function that returned nothing.
    /// </remarks>
    [Theory]
    [InlineData("has a Main", true, true, false, false)]
    [InlineData("is an executable", false, false, false, false)]
    [InlineData("hosts an API", false, true, true, false)]
    [InlineData("is none of those", false, true, false, true)]
    public void A_root_is_not_dead(string _, bool hasEntryPoint, bool isLibrary, bool hostsAnApi, bool expected)
    {
        var coupling = ProjectCoupling.ForSolution(
            [("Leaf.T", "Leaf", false), ("App.T", "App", false)],
            [("Leaf.T", "App.T")]);   // Leaf reaches out; nothing reaches into Leaf

        var unreferenced = ProjectReachability.Unreferenced(
            [("Leaf", hasEntryPoint, isLibrary), ("App", false, true)],
            coupling,
            hostsAnApi ? ["Leaf"] : []);

        Assert.Equal(expected, unreferenced.Contains("Leaf"));
    }

    [Fact]
    public void A_project_with_no_analysed_types_is_not_reported_as_unreferenced()
    {
        // Ca is counted over types, so a project with none has no Ca to be zero. It is unmeasured
        // rather than unreferenced, and the two are different claims — the same distinction
        // Instability draws by returning null.
        var unreferenced = ProjectReachability.Unreferenced(
            [("Empty", false, true)],
            ProjectCoupling.ForSolution([("A.T", "A", false)], []),
            []);

        Assert.Empty(unreferenced);
    }

    // ------------------------------------------------------------ external surface ----

    [Fact]
    public void Contact_points_over_the_fixture_are_what_they_should_be()
    {
        var contact = core.Model.ContactPoints;

        // P6 took these from 13 / 2 / 15. Three of its types are boundaries: LayeringEndpoint,
        // which the eight conduits reach, and the ApiBoundary pair that is the plant's control.
        // LayeringBeacon is the outbound one. The surfaces were chosen to leave the boundary
        // median where it was — see KnownDefectTests and the plant's own header.
        Assert.Equal(16, contact.Inbound.Count);
        Assert.Equal(3, contact.Outbound.Count);
        Assert.Equal(19, contact.Count);

        // Same population BoundaryMarking judges, counted a different way. The two halves of the
        // section have to be talking about the same set or the renderer joins two answers to
        // different questions.
        Assert.Equal(
            contact.Count,
            core.Model.Types.Count(t => t.Classification.Kind is "ApiBoundary" or "ExternalCall"));
    }

    [Fact]
    public void The_integration_map_over_the_fixture_is_what_it_should_be()
    {
        var map = core.Model.Integrations;

        // One more type touches each: P6's LayeringArchive is DataAccess by System.Data and its
        // LayeringBeacon is ExternalCall by System.Net.Http, which is how the plant reaches two of
        // the three significant kinds without giving an existing type new fan-in.
        Assert.Equal(
            [("System.Data", 3), ("System.Net.Http", 3)],
            map.Systems.Select(d => (d.Namespace, d.TypesTouching)));

        Assert.Equal(22, map.PlumbingReferences);
    }

    /// <summary>
    /// Nothing is dropped: every external reference is either an integration or counted as
    /// plumbing.
    /// </summary>
    /// <remarks>
    /// The filter is the whole of this section's risk. A namespace that fell out of both halves
    /// would be invisible without being disclosed, which is the failure the omitted-count exists
    /// to prevent — and it would be invisible in exactly the way that looks like a short list
    /// rather than a bug.
    /// </remarks>
    [Fact]
    public void The_integration_map_omits_nothing_silently()
    {
        var map = core.Model.Integrations;

        Assert.Equal(
            core.Model.ExternalDependencies.Sum(d => d.TypesTouching),
            map.Systems.Sum(d => d.TypesTouching) + map.PlumbingReferences);
    }

    [Theory]
    [InlineData("System", true)]
    [InlineData("System.Linq", true)]
    [InlineData("System.Text.Json", true)]          // prefix match, no separator boundary
    [InlineData("Microsoft.Extensions.Logging", true)]
    [InlineData("System.Data", false)]              // a database is an integration
    [InlineData("System.Net.Http", false)]          // so is an outbound call
    [InlineData("Azure.Messaging.ServiceBus", false)]
    [InlineData("Systematic.Reporting", false)]     // not System, despite the prefix
    public void Plumbing_is_what_the_map_is_not_about(string @namespace, bool expected) =>
        Assert.Equal(expected, ExternalSurface.IsPlumbing(@namespace));

    // ------------------------------------------------------------------ the model ----

    /// <summary>
    /// The model's projections are memoised, which is only sound because the model is frozen.
    /// </summary>
    /// <remarks>
    /// Reference equality on the second read. <b>Deleting a cache is already a build error</b> —
    /// the backing field goes unused and warnings are errors here — so what this catches is the
    /// case the compiler cannot see: a <c>??=</c> weakened to <c>=</c>, which recomputes on every
    /// read while still looking memoised. Verified by making that change; this is the only test
    /// that fails.
    /// <para>
    /// The claim underneath is the one worth pinning. <see cref="TypeNode"/> has
    /// <c>internal set</c> accessors, and caching a projection over a type that can still change
    /// would serve a stale answer silently. They are all written inside <c>ModelBuilder.Build</c>,
    /// which finishes before the model is constructed. If that ever stops being true, this is the
    /// assertion that should have been read first.
    /// </para>
    /// </remarks>
    [Fact]
    public void Projections_are_computed_once_because_the_model_cannot_change()
    {
        var model = core.Model;

        Assert.Same(model.ProjectCouplings, model.ProjectCouplings);
        Assert.Same(model.NamespaceCycles, model.NamespaceCycles);
        Assert.Same(model.ProjectCycles, model.ProjectCycles);
        Assert.Same(model.TypeTangles, model.TypeTangles);
        Assert.Same(model.UnreferencedProjects, model.UnreferencedProjects);
        Assert.Same(model.ExternalDependencies, model.ExternalDependencies);
        Assert.Same(model.Namespaces, model.Namespaces);

        // Value types, so identity is not the question — that the answer is stable is.
        Assert.Equal(model.ContactPoints.Count, model.ContactPoints.Count);
        Assert.Equal(model.Integrations.PlumbingReferences, model.Integrations.PlumbingReferences);
    }

    // ------------------------------------------------------------------ adapters ----

    private static double ValueOf(TypeMetrics t, string dimension) => dimension switch
    {
        "FanIn" => t.FanIn,
        "FanOut" => t.FanOut,
        "Cyclomatic" => t.Cyclomatic,
        "MaxMemberCyclomatic" => t.MaxMemberCyclomatic,
        "Dsm" => t.Dsm,
        "DataShape" => t.DataShape,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null),
    };

    private static double ProbePercentile(TypeMetrics t, string dimension) => dimension switch
    {
        "FanIn" => t.FanInPctl,
        "FanOut" => t.FanOutPctl,
        "Cyclomatic" => t.CyclomaticPctl,
        "MaxMemberCyclomatic" => t.MaxMemberCyclomaticPctl,
        "Dsm" => t.DsmPctl,
        "DataShape" => t.DataShapePctl,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null),
    };

    private static double ProbeTimesMedian(TypeMetrics t, string dimension) => dimension switch
    {
        "FanIn" => t.FanInXMedian,
        "FanOut" => t.FanOutXMedian,
        "Cyclomatic" => t.CyclomaticXMedian,
        "MaxMemberCyclomatic" => t.MaxMemberCyclomaticXMedian,
        "Dsm" => t.DsmXMedian,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null),
    };

}

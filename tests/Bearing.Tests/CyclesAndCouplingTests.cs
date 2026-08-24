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
public sealed class CyclesAndCouplingTests(CoreWalkFixture core)
{
    // ------------------------------------------------------------ type cohorts ----

    // ------------------------------------------------- the deliberate divergence ----

    // --------------------------------------------------------- project coupling ----

    /// <summary>
    /// Project coupling over the fixture, with the answers written down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to read the probe's own sentence and parse the numbers back out of it, because
    /// there was no model surface to compare against and that absence was the defect. There is
    /// one now, on both sides: Core walks the solution itself, and <c>WalkTests</c>
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
        // P10 adds two interfaces, IScaleHead and ITariffWindow — the abstractions its two
        // namespaces hold each other by, which is what makes the cycle Coupling.
        Assert.Equal(14, analysed.AbstractTypes);   // P3's four *Facet interfaces, X14's IIdentityWicket, A9's TallyProbe, P10's two
        Assert.Equal(199, analysed.TotalTypes);   // …P3 188 → member identity 191 → A9 layer 3 193 → P10 197 → P11 199
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
    /// The fixture's two namespace cycles, stated rather than parsed.
    /// </summary>
    /// <remarks>
    /// <b>One of each shape, which is the arrangement P10 exists to create.</b> The four-namespace
    /// component is the folder layout the fixture always had; the two-namespace one is the plant,
    /// and it is the only <c>Coupling</c> cycle here. Asserting both together is what makes the
    /// section's split observable — a classifier that collapsed the two shapes would keep every
    /// count in this file correct and be caught here.
    /// </remarks>
    [Fact]
    public void Namespace_cycles_over_the_fixture_are_what_they_should_be()
    {
        // Three, and the third is P11: one FolderLayout, one Coupling, one SharedTypes. All three
        // shapes now have a specimen, which is what makes the classifier's split observable in all
        // of its arms rather than in two of them — and it is why cycle-is-shared-types can be
        // observed to withhold something at all.
        Assert.Equal(3, core.Model.NamespaceCycles.Count);
        Assert.Equal(
            [CycleShape.Coupling, CycleShape.FolderLayout, CycleShape.SharedTypes],
            core.Model.ShapedNamespaceCycles.Select(c => c.Shape).Order());

        var folders = core.Model.NamespaceCycles.Single(c => c.Size == 4);

        Assert.Equal(
            [
                SubjectRef.ForNamespace("TestBed.Core"),
                SubjectRef.ForNamespace("TestBed.Core.Depots"),
                SubjectRef.ForNamespace("TestBed.Core.Pricing"),
                SubjectRef.ForNamespace("TestBed.Core.Vaults"),
            ],
            folders.Members);

        // Both pairs, by shape, because P11 made size ambiguous. Naming the members of each is
        // what stops a classifier that swapped the two readings from passing every count above.
        var byShape = core.Model.ShapedNamespaceCycles.ToDictionary(c => c.Shape, c => c.Cycle);

        Assert.Equal(
            [
                SubjectRef.ForNamespace("TestBed.Core.Tariffs"),
                SubjectRef.ForNamespace("TestBed.Core.Weighing"),
            ],
            byShape[CycleShape.Coupling].Members);

        Assert.Equal(
            [
                SubjectRef.ForNamespace("TestBed.Core.Berths"),
                SubjectRef.ForNamespace("TestBed.Core.Yards"),
            ],
            byShape[CycleShape.SharedTypes].Members);
    }

    /// <summary>
    /// The four-namespace component is a folder layout, and the section is right to silence it.
    /// </summary>
    /// <remarks>
    /// <c>TestBed.Core</c> contains the other three, they are all one assembly, and nothing in
    /// them holds anything in another. That is the shape a plugin has — a root beside its own
    /// folders. <b>This used to assert <c>Single</c></b>, which quietly also asserted that nothing
    /// here was ever reportable; P10 is the plant that made the second half false, and
    /// <see cref="The_planted_cycle_is_coupling_and_carries_its_pair"/> is the half that was
    /// missing.
    /// </remarks>
    [Fact]
    public void The_folder_layout_cycle_is_not_coupling()
    {
        var shaped = core.Model.ShapedNamespaceCycles.Single(c => c.Cycle.Size == 4);

        Assert.Equal(CycleShape.FolderLayout, shaped.Shape);
        Assert.False(shaped.IsReportable);
        Assert.Equal("TestBed.Core", shaped.Anchor);
        Assert.Empty(shaped.Pairs);
    }

    /// <summary>
    /// P10's cycle is <c>Coupling</c>, is reportable, and carries the held pair as its evidence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the gate the fixture never had, and its absence is how <c>docs/DEFECTS.md</c>
    /// §46 shipped.</b> Every namespace cycle here was a folder layout, so <c>IsReportable</c> was
    /// false for all of them, the reportable branch was unreachable from the fixture, and both
    /// renderers' cycle output was ungated — the HTML dropped the pair evidence entirely and the
    /// whole suite stayed green.
    /// </para>
    /// <para>
    /// <c>Peers_that_hold_each_other_are_coupling</c> exercises the same judgement through
    /// <c>CycleShapes.Read</c> over hand-written members, which is the unit. This is the end of the
    /// walk: real types, real fields, real edges, a real classification. Both are wanted — the unit
    /// says the rule is right, and this says the rule is reachable.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_planted_cycle_is_coupling_and_carries_its_pair()
    {
        // Named rather than sized: P11 planted a second two-namespace cycle, so size no longer
        // identifies this one. Selecting by the members keeps the shape assertion below meaningful
        // — selecting by Shape == Coupling would have made it assert itself.
        var shaped = core.Model.ShapedNamespaceCycles.Single(
            c => c.Cycle.Members.Any(m => m.Canonical.EndsWith("TestBed.Core.Tariffs", StringComparison.Ordinal)));

        Assert.Equal(CycleShape.Coupling, shaped.Shape);
        Assert.True(shaped.IsReportable);

        // Siblings, so neither contains the other and there is no anchor to report.
        Assert.Null(shaped.Anchor);

        var pair = Assert.Single(shaped.Pairs);

        Assert.Equal("TestBed.Core.Tariffs", pair.First);
        Assert.Equal("TestBed.Core.Weighing", pair.Second);

        // One held field each way. Held means a field whose type is abstract or an interface, so
        // the constructor parameters and the calls alongside them do not count -- which is the
        // distinction that separates this shape from SharedTypes.
        Assert.Equal(1, pair.FirstHolds);
        Assert.Equal(1, pair.SecondHolds);
        Assert.Equal(2, pair.Weight);
    }

    /// <summary>
    /// Two peers holding each other is the finding, and the pair is the evidence for it.
    /// </summary>
    [Fact]
    public void Peers_that_hold_each_other_are_coupling()
    {
        var reading = CycleShapes.Read(
            ["Shop.Orders", "Shop.Customers"],
            OneAssembly("Shop.Orders", "Shop.Customers"),
            Held(("Shop.Orders", "Shop.Customers", 6), ("Shop.Customers", "Shop.Orders", 3)));

        Assert.Equal(CycleShape.Coupling, reading.Shape);

        var pair = Assert.Single(reading.Pairs);
        Assert.Equal(9, pair.Weight);
        Assert.Equal("Shop.Customers", pair.First);
    }

    /// <summary>
    /// A root holding its own folder is not two components, however mutual the references are.
    /// </summary>
    /// <remarks>
    /// The case that made the rule: a plugin whose root declares the settings class and injects
    /// the service in <c>/Services</c>, while that service reads the settings back. Both
    /// directions hold, and there is still nothing to extract — MSBuild ships the one assembly.
    /// </remarks>
    [Fact]
    public void A_root_holding_its_own_subfolder_is_layout_not_coupling()
    {
        var reading = CycleShapes.Read(
            ["Plugin.Tax", "Plugin.Tax.Services"],
            OneAssembly("Plugin.Tax", "Plugin.Tax.Services"),
            Held(("Plugin.Tax", "Plugin.Tax.Services", 4), ("Plugin.Tax.Services", "Plugin.Tax", 2)));

        Assert.Equal(CycleShape.FolderLayout, reading.Shape);
        Assert.Equal("Plugin.Tax", reading.Anchor);
        Assert.Empty(reading.Pairs);
    }

    /// <summary>
    /// Peers that only name each other's types hold nothing, so nothing is entangled.
    /// </summary>
    [Fact]
    public void Peers_that_name_but_do_not_hold_are_shared_types()
    {
        var reading = CycleShapes.Read(
            ["Web.Models.Catalog", "Web.Models.Orders"],
            OneAssembly("Web.Models.Catalog", "Web.Models.Orders"),
            Held());

        Assert.Equal(CycleShape.SharedTypes, reading.Shape);
        Assert.Null(reading.Anchor);
        Assert.Empty(reading.Pairs);
    }

    /// <summary>
    /// The folder-layout reading needs the single assembly, not just the containing namespace.
    /// </summary>
    /// <remarks>
    /// Two projects that happen to share a namespace prefix are two things anyone can extract, so
    /// the argument that makes layout safe to set aside does not apply and the cycle falls back to
    /// being reported. Invariant: silence is only ever bought by the assembly boundary.
    /// </remarks>
    [Fact]
    public void Containment_across_two_assemblies_is_not_folder_layout()
    {
        var projects = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Shared"] = new HashSet<string>(StringComparer.Ordinal) { "Shared" },
            ["Shared.Contracts"] = new HashSet<string>(StringComparer.Ordinal) { "Contracts" },
        };

        var reading = CycleShapes.Read(
            ["Shared", "Shared.Contracts"],
            projects,
            Held(("Shared", "Shared.Contracts", 2), ("Shared.Contracts", "Shared", 1)));

        Assert.Equal(CycleShape.Coupling, reading.Shape);
        Assert.Equal(["Contracts", "Shared"], reading.Projects);
    }

    /// <summary>
    /// A base and its own implementations is not a tangle, however many of them there are.
    /// </summary>
    /// <remarks>
    /// nopCommerce's only tangle in the small: three providers deriving from one base, a manager
    /// that constructs them and a startup type. Every derived-to-base edge reads
    /// <c>Inheritance;Invocation</c> — the invocations are <c>base.X()</c> — so the whole edge is
    /// the hierarchy, and with those gone nothing mutually dependent is left.
    /// </remarks>
    [Fact]
    public void A_base_and_its_implementations_is_a_hierarchy_not_a_tangle()
    {
        var shape = TangleShapes.Read(
            Members("Base", "Manager", "MsSql", "MySql", "Startup"),
            Uses(
                ("Manager", "MsSql"), ("Manager", "MySql"),      // constructs them
                ("Base", "Startup"), ("Startup", "Manager")));   // and the derived-to-base edges are gone

        Assert.Equal(TangleShape.Hierarchy, shape);
    }

    /// <summary>
    /// Mostly-inheritance is not the same as explained-by-inheritance.
    /// </summary>
    /// <remarks>
    /// Jellyfin's group states: four subclasses of one abstract base, four inheritance edges and
    /// sixteen constructions, because each state builds the next. Counting hierarchy edges would
    /// have called it a hierarchy; removing them and asking what survives does not, and the
    /// surviving ring is the finding.
    /// </remarks>
    [Fact]
    public void Subclasses_that_construct_each_other_stay_entangled()
    {
        var shape = TangleShapes.Read(
            Members("AbstractState", "Idle", "Playing", "Waiting"),
            Uses(("Idle", "Playing"), ("Playing", "Waiting"), ("Waiting", "Idle")));

        Assert.Equal(TangleShape.Entangled, shape);
    }

    /// <summary>
    /// The types that close a project cycle, which is the part the section never said.
    /// </summary>
    /// <remarks>
    /// Constructed rather than measured, and it has to be: neither cloned solution has a project
    /// cycle at all — an ordinary cross-project edge follows a project reference and MSBuild
    /// forbids those from cycling. This is the same reason
    /// <see cref="Cycles.AmongProjects(IEnumerable{ValueTuple{string, string}},
    /// IEnumerable{ValueTuple{string, string}})"/> takes primitives.
    /// </remarks>
    [Fact]
    public void A_project_cycle_names_the_links_that_close_it()
    {
        var types = new[] { ("core|A", "Core"), ("web|B", "Web"), ("web|C", "Web") };

        var cycle = Assert.Single(Cycles.AmongProjects(
            types, [("core|A", "web|B"), ("web|C", "core|A")]));

        var links = ProjectLinks.Closing(cycle, types, core.Model.Edges);

        // The fixture's edges know nothing of these projects, so the cycle is real and the
        // evidence for it is empty. That is the honest pairing: Closing reports what the edge
        // list contains, and inventing a link for a cycle found over different inputs is the
        // failure docs/DEFECTS.md §1 is about.
        Assert.Empty(links);
        Assert.Equal(["Core", "Web"], cycle.Members.Select(m => m.Canonical.Replace("project|", "", StringComparison.Ordinal)));
    }

    private static HashSet<string> Members(params string[] types) =>
        new(types, StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<string>> Uses(params (string From, string To)[] edges) =>
        edges
            .GroupBy(e => e.From, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(e => e.To).ToList(), StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlySet<string>> OneAssembly(params string[] namespaces) =>
        namespaces.ToDictionary(
            ns => ns,
            _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal) { "TheAssembly" },
            StringComparer.Ordinal);

    private static Dictionary<(string From, string To), int> Held(
        params (string From, string To, int Weight)[] references) =>
        references.ToDictionary(r => (r.From, r.To), r => r.Weight);

    [Fact]
    public void Type_tangles_over_the_fixture_are_what_they_should_be()
    {
        // The Normalizer tangle, which is the one this test was written for. P8 added two rings
        // beside it and they are asserted in their own tests; this one still owns the eight.
        var tangle = core.Model.TypeTangles.Single(c => c.Size == 8);

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
        // Every tangle since P8, not just the one: a ring's loop is its whole component, so this
        // walks nine steps and four as well as the original three.
        Assert.All(core.Model.TypeTangles, Walkable);

        void Walkable(Cycle tangle)
        {
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
    }

    /// <summary>
    /// The fixture exercises both arms of the loop sentence now, and the note that used to say it
    /// could not is the thing P8 deleted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This assertion used to read <c>Assert.False</c> for every cycle</b>, with a remark saying
    /// the covering arm was not exercised here at all and that a plant making it fail meant the
    /// fixture had got better. P8 is that plant: two rings, of nine types and of four, whose
    /// representative loop visits every member because a ring has exactly one way through it.
    /// </para>
    /// <para>
    /// <b>Both arms are asserted rather than just the new one.</b> The Normalizer tangle is still
    /// the partial case and the namespace cycle still is, so the two sentences — the bare loop and
    /// the <i>"3 of the 8; all 8 reach each other"</i> disclosure — now render in one report, which
    /// is the only arrangement in which a renderer confusing them is visible.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fixture_exercises_both_arms_of_the_loop_sentence()
    {
        // The partial arm, which is what the fixture always had. Read off the four-namespace
        // component specifically rather than All: P10's two-namespace cycle covers both its
        // members, because a pair has exactly one way round it, so the namespace side now
        // exercises BOTH arms too and an All here would be asserting the fixture had not improved.
        Assert.False(core.Model.NamespaceCycles.Single(c => c.Size == 4).PathCoversEveryMember);

        // Both pairs now, P10's and P11's: a pair has exactly one way round it whatever its shape,
        // so All is the honest assertion here and Single stopped being available when P11 landed.
        Assert.All(
            core.Model.NamespaceCycles.Where(c => c.Size == 2),
            c => Assert.True(c.PathCoversEveryMember));
        Assert.Contains(core.Model.TypeTangles, c => !c.PathCoversEveryMember);

        // And the covering arm, which is P8's. Two of them, at different sizes, because the
        // sentence is the same whether the component is four types or nine.
        var covering = core.Model.TypeTangles.Where(c => c.PathCoversEveryMember).ToList();

        Assert.Equal(2, covering.Count);
        Assert.All(covering, c => Assert.Equal(c.Size, c.Path.Count));
        Assert.Equal([4, 9], covering.Select(c => c.Size).Order());
    }

    /// <summary>
    /// Tangles are ordered largest first, which one tangle could never have shown.
    /// </summary>
    /// <remarks>
    /// The fixture had a single tangle until P8, so every comparator produced the same output and
    /// the ordering was decoration. Nine, eight and four is an order a wrong comparator changes.
    /// </remarks>
    [Fact]
    public void Tangles_are_ordered_largest_first()
    {
        var sizes = core.Model.TypeTangles.Select(c => c.Size).ToList();

        Assert.Equal([9, 8, 4], sizes);
    }

    /// <summary>
    /// The tangle floor decides in both directions.
    /// </summary>
    /// <remarks>
    /// <b>The sweep cannot see this one and that is a property of the sweep.</b>
    /// <c>PolicySweepTests</c> fingerprints the finding set, and a type tangle is not a finding —
    /// it is structure, on the model. So <c>MinTangle</c> reports <c>-</c> in that table however
    /// well the fixture covers it, which is the third kind of dash: not a dead constant and not a
    /// distribution that cannot reach it, but an instrument that does not measure it.
    /// <c>docs/TESTING.md</c> §6.
    /// </remarks>
    [Fact]
    public void The_tangle_floor_decides_in_both_directions()
    {
        // Raised past the four-ring, the four-ring goes and the other two stay.
        Assert.Equal([9, 8], TangleSizesUnder(core.Model.Policy with { MinTangle = 5 }));

        // Lowered, the mutual pair arrives — which is the judgement the floor exists to make, and
        // the reason it is not 2. Two types that reference each other are not a tangle.
        // Two pairs at the floor, not one: P11's SharedTypes plant is also two types referencing
        // each other, and at MinTangle 2 it is a tangle like P8's mutual pair. That it is not one
        // at the shipped floor of 4 is the judgement this test exists for.
        Assert.Equal([9, 8, 4, 2, 2], TangleSizesUnder(core.Model.Policy with { MinTangle = 2 }));
    }

    private static List<int> TangleSizesUnder(AnalysisPolicy policy)
    {
        var model = new SolutionWalker(new WalkOptions { SolutionPath = RepoPaths.TestBedSolution, Policy = policy })
            .WalkAsync(CancellationToken.None).GetAwaiter().GetResult();

        return model.TypeTangles.Select(c => c.Size).ToList();
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

        // 23 rather than 22 since the X14 plant: an event needs a delegate type and the fixture
        // declares none, so IdentityTurnstile reaches System.Action. That is plumbing, not an
        // integration — map.Systems above is unchanged, which is the half that would have meant
        // the plant disturbed something.
        // 26 since A9 layer 3: SettlementProbe reaches System.Collections.Generic, System and
        // System.Runtime.Serialization — the last for [OnDeserialized], which is the plant's
        // whole point. All three are plumbing; map.Systems above is unchanged, which is the half
        // that would have meant the plant disturbed an integration.
        Assert.Equal(26, map.PlumbingReferences);
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

}

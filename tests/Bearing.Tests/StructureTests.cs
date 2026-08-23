using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The TestBed fixture has known answers. Until the probe existed every one of them was verified
/// by reading console output, and several defects during that build were reintroductions of a
/// failure already fixed elsewhere. These assert the structural half — the data Job A renders
/// from — so a change cannot move it silently.
///
/// Deliberately asserts against the model, never against report wording; the report is the layer
/// most likely to move, and anything coupled to its sentences would be deleted exactly when it is
/// needed.
/// </summary>
/// <remarks>
/// <b>Ported off the probe at R2, and the numbers moved under it.</b> These read the probe's
/// model until the probe was retired. Most of what they assert is a fact about TestBed and is
/// unchanged — a fan-in of 20 is 20 whoever counted it — but three are not, and each is called
/// out where it sits: the type count is two higher because Core does not merge the planted
/// identity collisions, <c>Kind</c> is now <c>Classification.Kind</c> and carries its evidence,
/// and a cohort of one has no percentile at all rather than a 50 that a renderer had to hide.
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class StructureTests(CoreWalkFixture core)
{
    /// <summary>
    /// The one type with this simple name.
    /// </summary>
    /// <remarks>
    /// <c>Single</c> rather than a name-keyed dictionary, and deliberately: TestBed plants two
    /// fully-qualified names that each exist in two assemblies, so a dictionary over names would
    /// throw while being built and take every test in the file with it. This throws only if a
    /// test actually asks for a collided name, which is a question this file never asks and
    /// <c>ProjectCycleTests</c> asks properly.
    /// </remarks>
    private TypeNode Type(string name) => core.Model.Types.Single(t => t.Name == name);

    // ---- Load health -------------------------------------------------------------
    // First, because everything below is meaningless if it fails. A project that does not
    // load understates fan-in everywhere it is referenced, and nothing fails — it is quieter
    // than being wrong.

    [Fact]
    public void Solution_loads_with_no_warnings()
    {
        Assert.Empty(core.Model.Coverage.LoadDiagnostics);

        // Core.Tests is skipped, and that is the point of it. Test projects are excluded by
        // default, which is what makes FixtureBuilder — used only from there — look like dead
        // code to anything counting inbound edges. Asserted positively so the exclusion cannot
        // quietly stop happening and take the trap with it.
        Assert.Equal(["Core.Tests"], core.Model.Coverage.SkippedProjects.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Fixture_shape_is_stable()
    {
        // 179 rows, and the last two of them are the point. TestBed declares two fully-qualified
        // names in two assemblies each — TestBed.Shared.PayloadTag and CarrierTwin — and Core
        // keys type identity on (assembly, FQN), so each is two rows. The probe keyed on the name
        // alone and reported 177; that was docs/DEFECTS.md §1, the one behaviour the extraction
        // was permitted to change, and this count is where the change is visible. Retiring the
        // probe did not fix it and did not need to: the fix shipped with Core's walk, and what R2
        // removed was the second opinion, not the behaviour.
        //
        // NormalizerScenarios is absent, and correctly so — it lives in Core.Tests, which is
        // skipped.
        //
        // P6 moved all three counts: twelve types (the eight *Conduit types, their three shared
        // dependency targets and RouteAttribute) and twenty-seven edges — 8 × 3 dependency
        // fields, plus the three [Route] usages, which are references like any other.
        //
        // P7 moved them again: fifteen types in two near-miss families — five *Sonde and ten
        // *Caliper — and twenty-eight edges, which are the references that hold each family's
        // fan-in median where its gate needs it. Sixteen methods, not fifteen: SpanCaliper carries
        // a second one so its cyclomatic total lands on the tie that fixes its percentile.
        //
        // And P8 again: nine *Node, four *Link, a mutual pair, and the four types of the second
        // identity collision.
        //
        // P9 adds six: the *Trait chain, five property bags and one type with a method, planted so
        // that a cohort's median complexity is zero. Five edges, because the chain has five links
        // and closes over its own members — no existing type gains fan-in.
        //
        // P3 adds ten: four *Facet interfaces, SettlementProjection which depends only on them,
        // and five *Roster property bags which depend only on it. Nine edges — four out of the
        // projection and five into it — which is what makes its effective fan-out zero against a
        // raw four, and the dependency-inversion exclusion the thing that decides its nomination.
        //
        // And the member-identity plant adds three: IIdentityWicket, IdentityTurnstile and
        // TurnstileExtensions, with three edges, all inside the file. It carries the eight member
        // shapes the model got wrong and the fixture did not contain; MemberIdentityTraps.cs says
        // why each is there. The last two arrived with A9's member graph and were found by
        // measuring a real solution, not by reading: an extension method called as one, and a
        // partial method. Nothing that already existed gains fan-in, a cohort or a contact point,
        // and the extension goes through the interface so that the two types do not name each
        // other — that made a two-type tangle, which is a finding the plant did not aim at.
        //
        // And A9 layer 3's plant adds two more: TallyProbe and SettlementProbe, one edge. They
        // carry the member-level dead-code categories, and every member on them is NON-PUBLIC on
        // purpose — the type-level plants in DeadCodeTraps.cs all pass by being externally
        // visible, so a public member-level trap would test nothing.
        //
        // P10 adds four: IScaleHead and ScaleHead in TestBed.Core.Weighing, ITariffWindow and
        // TariffWindow in TestBed.Core.Tariffs. They are the fixture's first namespace cycle that
        // is NOT a folder layout — two sibling namespaces each holding the other's interface in a
        // field, which is CycleShape.Coupling and therefore the first cycle IsReportable has ever
        // returned true for here. Every cycle before it was TestBed.Core over its own subfolders,
        // so the reportable branch was unreachable and both renderers' cycle output was ungated:
        // that is how docs/DEFECTS.md §46 shipped and stayed green. Nothing that already existed
        // gains fan-in — the four reference only each other — and Head, Window, Scale and Tariff
        // were each checked against the fixture's trailing words before being chosen, so no suffix
        // cohort changes size.
        //
        // One cohort does, and it is worth stating rather than leaving to be found: IScaleHead
        // lands in kind:Contract, 8 -> 9, because an interface is cohorted by architectural kind
        // before name and only ScaleHead ends up under suffix:Head. The naming constraint in
        // docs/TESTING.md is about suffixes and does not reach this. The other three are peerless
        // — suffix:Head at 1 and suffix:Window at 2, both under MinCohort — which is the whole of
        // NO PEER GROUP's move from 22 to 25.
        Assert.Equal(204, core.Model.Types.Count);

        // Unchanged at 362 across the retirement, and not by luck: an Edge is a (from, to) pair
        // however many references it carries, and separating the collided declarations moved
        // which node an edge lands on without changing how many pairs there are.
        // P10's four: each concrete type to the interface it implements, and each to the interface
        // it holds in a field. An Edge is a (from, to) pair however many references it carries, so
        // ScaleHead -> ITariffWindow is one edge although the constructor parameter, the field and
        // the RateFor call are three references — and the field is the only one of the three that
        // CycleShape.IsHeld counts.
        Assert.Equal(384, core.Model.Edges.Count);

        // Methods are counted per declaration, so unlike Types this was never distorted by the
        // planted collisions: Describe, Score and Weight are all three present. Core holds
        // members on the type rather than in a flat list, and counts constructors as method-like
        // because they carry cyclomatic complexity — three of the 186 are constructors.
        //
        // Nine of these are the member-identity plant, and the count is the point of it: two
        // constructors where a display string saw one, and five methods where it saw three — the
        // two TryAdmit overloads differ only by `out`, and the explicit IIdentityWicket.Admit sat
        // on top of the ordinary Admit. Plus IsWired and OnRefused, and OnRefused is ONE member
        // from two declarations — recording both parts put two rows under one subject, which is
        // six colliding subjects on nopCommerce.
        // Seven more with layer 3's plant: the virtual and its override, a constructor, a wired
        // handler, a serialisation callback, a string-dispatched method and the one that names it.
        // P10 adds five, and which five is worth stating because the plant declares eleven members:
        // two constructors, and three methods — ITariffWindow.RateFor, its implementation, and
        // ScaleHead.Settle. The four fields and the two SettledKilograms properties are members but
        // not method-like, and IScaleHead contributes nothing here because its only member is that
        // property. The two held fields are the whole point of the plant and neither is counted by
        // this line, which is why the count is not the thing that proves the plant landed —
        // CyclesAndCouplingTests is.
        Assert.Equal(214, core.Model.Types.Sum(t => t.Members.Count(m => m.IsMethodLike)));
    }

    // ---- Generated code exclusion -------------------------------------------------

    [Fact]
    public void Scaffolded_code_is_excluded_by_default()
    {
        Assert.Equal(2, core.Model.Coverage.ExcludedTypes);

        // Areas/HelpPage is scaffolding: real C#, nobody's design, and it pollutes cohorts.
        Assert.DoesNotContain(core.Model.Types, t => t.Name == "HelpPageSampleGenerator");
    }

    // ---- Architectural Kind -------------------------------------------------------
    // Kind is heuristic and load-bearing: cohort assignment, layer-span, effective fan-out
    // and the boundary section all depend on it, and a misclassification produces a
    // confident wrong finding rather than an error.

    [Theory]
    [InlineData("CarrierGateway", "ExternalCall")]
    [InlineData("OrderRepository", "DataAccess")]
    [InlineData("NormalizationContext", "Contract")]
    [InlineData("AuthenticationMiddleware", "ApiBoundary")]
    public void Kind_is_classified_as_expected(string type, string kind)
    {
        var classification = Type(type).Classification;

        Assert.Equal(kind, classification.Kind);

        // Core carries the evidence beside the verdict, which the probe did not. Asserted as
        // non-empty rather than by wording: a kind that arrived with nothing to show for itself
        // would be a heuristic that had stopped explaining, and the sentence is free to move.
        Assert.NotEmpty(classification.Evidence);
    }

    [Fact]
    public void Kind_keys_off_types_used_not_using_directives()
    {
        // RateRepository is named like data access and imports nothing that makes it so.
        // Classifying on `using` directives rather than types actually used would call this
        // DataAccess and be confidently wrong. This is a planted case, not an accident.
        Assert.Equal("Internal", Type("RateRepository").Classification.Kind);
        Assert.Equal("DataAccess", Type("OrderRepository").Classification.Kind);
    }

    // ---- External namespaces — the integration-map seed ---------------------------

    [Fact]
    public void External_namespace_is_not_truncated_to_a_fixed_depth()
    {
        // Regression: System.Net.Http was once truncated to System.Net, so an HttpClient
        // gateway was never flagged as a boundary at all. Namespace matching must be by
        // segment, never by a fixed number of parts.
        var external = Type("CarrierGateway").ExternalNamespaces;

        Assert.Contains("System.Net.Http", external);
        Assert.DoesNotContain("System.Net", external);
    }

    [Fact]
    public void Data_access_reports_its_external_namespace()
    {
        Assert.Contains("System.Data", Type("OrderRepository").ExternalNamespaces);
    }

    // ---- Cohort assignment --------------------------------------------------------
    // README: check this before trusting anything else. If the normalizers do not land in
    // one cohort, every percentile below them is meaningless.

    [Fact]
    public void The_seven_normalizers_share_one_interface_cohort()
    {
        var normalizers = core.Model.Types
            .Where(t => t.Name.EndsWith("Normalizer", StringComparison.Ordinal)
                        && t.TypeKeyword != "Interface")   // Roslyn TypeKind, capitalised
            .ToList();

        Assert.Equal(7, normalizers.Count);

        // One cohort, and discovered from the shared interface rather than the name suffix.
        // A suffix-based grouping would be a weaker peer group that happens to look right.
        Assert.Single(normalizers.Select(n => n.Cohort.Key).Distinct(StringComparer.Ordinal));
        Assert.All(normalizers, n =>
        {
            Assert.Equal("interface", n.Cohort.Basis);
            Assert.Equal(7, n.CohortSize);
        });
    }

    [Fact]
    public void A_singleton_has_a_cohort_of_one()
    {
        Assert.Equal(1, Type("PayloadAuditor").CohortSize);
    }

    /// <summary>
    /// A peerless type's percentile column is empty in <c>types.csv</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Invariant 6, "blank, never fake". A cohort of one makes every type its own median, so
    /// midrank puts it at exactly 50 — the most extreme outlier in the codebase would sort into
    /// the middle of the file and read as perfectly average.
    /// </para>
    /// <para>
    /// <b>This is the half of the old test that Core did not already answer.</b> It used to open
    /// by asserting that the model held 50 and that only the CSV writer blanked it, and it said
    /// in as many words that every other renderer would emit the 50 unless the rule moved onto
    /// the model. It did move — X9 put the readings on the model as nullable, and
    /// <c>CohortStatisticsTests.A_type_with_no_usable_peer_group_gets_no_relative_reading</c>
    /// asserts all eleven are absent for every peerless type. So the model half is covered and is
    /// not repeated at length here; what is left is the writer, which still has to turn "no
    /// reading" into an empty field rather than into <c>0</c> or the word <c>null</c>, and which
    /// nothing else checks for a percentile.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_cohort_of_one_is_written_blank_never_as_a_percentile()
    {
        var auditor = Type("PayloadAuditor");
        Assert.Null(core.Model.Statistics[auditor.Subject.Canonical].CyclomaticPercentile);

        var rows = CsvOutput.Types(core.Model)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(','))
            .ToList();

        var column = Array.IndexOf(rows[0], "CyclomaticPctl");
        Assert.True(column >= 0, "types.csv has no CyclomaticPctl column");

        // Found by the Name column rather than by a line prefix: Core's first column is the
        // subject reference, so PayloadAuditor no longer starts its own row.
        var name = Array.IndexOf(rows[0], "Name");
        var row = rows.Skip(1).Single(r => r[name] == "PayloadAuditor");

        Assert.Equal("", row[column]);
    }

    // ---- Fan-in: the change-cost and hub evidence ---------------------------------

    [Theory]
    [InlineData("NormalizationContext", 20)]
    [InlineData("RawResponse", 19)]   // +1: FixtureBuilder, the test-only dead-code trap
    [InlineData("NormalizedResponse", 15)]
    [InlineData("ModelDescription", 5)]
    public void Contract_fan_in_is_stable(string type, int fanIn) =>
        Assert.Equal(fanIn, Type(type).FanIn);

    [Fact]
    public void Hub_magnitudes_are_stable()
    {
        // Instability cannot see hubs: high fan-in AND high fan-out lands at I ~ 0.5,
        // identical to a trivial leaf. Both magnitudes are the detector, so both are
        // asserted.
        var coordinator = Type("ShipmentCoordinator");
        Assert.Equal(7, coordinator.FanIn);
        Assert.Equal(7, coordinator.FanOut);

        var router = Type("Router");
        Assert.Equal(5, router.FanIn);
        Assert.Equal(11, router.FanOut);
    }

    [Fact]
    public void Load_bearing_type_keeps_its_low_instability()
    {
        var tariff = Type("TariffCalculator");

        Assert.Equal(7, tariff.FanIn);
        Assert.Equal(1, tariff.FanOut);

        // Nullable on Core, where the probe computed 1.0 for an unconnected type and left the
        // CSV to hide it. This one is connected, so it has a reading — and asserting straight
        // through .Value would throw rather than fail if it ever stopped having one.
        Assert.NotNull(tariff.Instability);
        Assert.Equal(0.125, tariff.Instability!.Value, precision: 3);

        Assert.Equal(22, tariff.MaxMemberCyclomatic);
    }

    // ---- Projects ------------------------------------------------------------------

    [Fact]
    public void Projects_are_discovered_with_their_entry_point_status()
    {
        var projects = core.Model.Projects.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

        Assert.Equal(["Core", "Data", "Tools"], projects.Select(p => p.Name));

        // The status the name promises, which the probe's model did not expose and this test
        // therefore never asserted. All three are libraries and none is an entry point, so a
        // Program.cs appearing in TestBed — or the detection quietly stopping — fails here.
        Assert.All(projects, p =>
        {
            Assert.False(p.HasEntryPoint);
            Assert.True(p.IsLibrary);
        });
    }
}

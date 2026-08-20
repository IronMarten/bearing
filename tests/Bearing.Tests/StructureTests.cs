using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// The TestBed fixture has known answers. Until now every one of them was verified by
/// reading console output, and several defects during the probe build were reintroductions
/// of a failure already fixed elsewhere. These assert the structural half — the data Job A
/// renders from — so extraction cannot change it silently.
///
/// Deliberately asserts against the model, never against report wording. Report.cs is being
/// replaced; anything coupled to its sentences would be deleted exactly when it is needed.
/// </summary>
[Collection(FixtureCollection.Name)]
public sealed class StructureTests(FixtureRun run)
{
    // ---- Load health -------------------------------------------------------------
    // First, because everything below is meaningless if it fails. A project that does not
    // load understates fan-in everywhere it is referenced, and the probe only warns.

    [Fact]
    public void Solution_loads_with_no_warnings()
    {
        Assert.Empty(run.Result.LoadWarnings);

        // Core.Tests is skipped, and that is the point of it. Test projects are excluded by
        // default, which is what makes FixtureBuilder — used only from there — look like dead
        // code to anything counting inbound edges. Asserted positively so the exclusion cannot
        // quietly stop happening and take the trap with it.
        Assert.Equal(["Core.Tests"], run.Result.SkippedProjects.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Fixture_shape_is_stable()
    {
        // 144 rows, not 145: the two TestBed.Shared.PayloadTag declarations are one row, because
        // the probe keys type identity on name alone. That is the planted defect, pinned in
        // KnownDefectTests. This count is the probe's and stays one behind Core — Core already
        // keys on (assembly, FQN) and reports 145; WalkerEquivalenceTests asserts the difference.
        //
        // NormalizerScenarios is absent, and correctly so — it lives in Core.Tests, which is
        // skipped.
        //
        // P6 moved all three: twelve types (the eight *Conduit types, their three shared
        // dependency targets and RouteAttribute) and twenty-seven edges — 8 × 3 dependency
        // fields, plus the three [Route] usages, which are references like any other.
        //
        // P7 moved them again: fifteen types in two near-miss families — five *Sonde and ten
        // *Caliper — and twenty-eight edges, which are the references that hold each family's
        // fan-in median where its gate needs it. Sixteen methods, not fifteen: SpanCaliper carries
        // a second one so its cyclomatic total lands on the tie that fixes its percentile.
        //
        // And P8 again: nine *Node, four *Link, a mutual pair, and the four types of the second
        // identity collision. The probe's count stays two behind Core's rather than one, because
        // there are two collisions for it to merge now.
        Assert.Equal(177, run.Result.Types.Count);
        Assert.Equal(362, run.Result.Edges.Count);
        // Methods are counted per declaration, so unlike Types this is not distorted by the
        // planted collision: Describe, Score and Weight are all three present.
        Assert.Equal(186, run.Result.Methods.Count);
    }

    // ---- Generated code exclusion -------------------------------------------------

    [Fact]
    public void Scaffolded_code_is_excluded_by_default()
    {
        Assert.Equal(2, run.Result.ExcludedTypes);

        // Areas/HelpPage is scaffolding: real C#, nobody's design, and it pollutes cohorts.
        Assert.DoesNotContain(run.Result.Types, t => t.Name == "HelpPageSampleGenerator");
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
    public void Kind_is_classified_as_expected(string type, string kind) =>
        Assert.Equal(kind, run.Type(type).Kind);

    [Fact]
    public void Kind_keys_off_types_used_not_using_directives()
    {
        // RateRepository is named like data access and imports nothing that makes it so.
        // Classifying on `using` directives rather than types actually used would call this
        // DataAccess and be confidently wrong. This is a planted case, not an accident.
        Assert.Equal("Internal", run.Type("RateRepository").Kind);
        Assert.Equal("DataAccess", run.Type("OrderRepository").Kind);
    }

    // ---- External namespaces — the integration-map seed ---------------------------

    [Fact]
    public void External_namespace_is_not_truncated_to_a_fixed_depth()
    {
        // Regression: System.Net.Http was once truncated to System.Net, so an HttpClient
        // gateway was never flagged as a boundary at all. Namespace matching must be by
        // segment, never by a fixed number of parts.
        var external = run.Type("CarrierGateway").ExternalNamespaces;

        Assert.Contains("System.Net.Http", external);
        Assert.DoesNotContain("System.Net", external);
    }

    [Fact]
    public void Data_access_reports_its_external_namespace()
    {
        Assert.Contains("System.Data", run.Type("OrderRepository").ExternalNamespaces);
    }

    // ---- Cohort assignment --------------------------------------------------------
    // README: check this before trusting anything else. If the normalizers do not land in
    // one cohort, every percentile below them is meaningless.

    [Fact]
    public void The_seven_normalizers_share_one_interface_cohort()
    {
        var normalizers = run.Result.Types
            .Where(t => t.Name.EndsWith("Normalizer", StringComparison.Ordinal)
                        && t.TypeKeyword != "Interface")   // Roslyn TypeKind, capitalised
            .ToList();

        Assert.Equal(7, normalizers.Count);

        // One cohort, and discovered from the shared interface rather than the name suffix.
        // A suffix-based grouping would be a weaker peer group that happens to look right.
        Assert.Single(normalizers.Select(n => n.Cohort).Distinct());
        Assert.All(normalizers, n =>
        {
            Assert.Equal("interface", n.CohortBasis);
            Assert.Equal(7, n.CohortSize);
        });
    }

    [Fact]
    public void A_singleton_has_a_cohort_of_one()
    {
        Assert.Equal(1, run.Type("PayloadAuditor").CohortSize);
    }

    [Fact]
    public void A_cohort_of_one_is_rendered_blank_never_as_a_percentile()
    {
        // Invariant 6, "blank, never fake". A cohort of one makes every type its own median,
        // so midrank puts it at exactly 50 — the most extreme outlier in the codebase would
        // sort into the middle of the file and read as perfectly average.
        //
        // Note WHERE this is enforced: the model holds 50, and only the CSV writer blanks
        // it. Every Job A renderer — JSON, HTML, the graph tooltips — will emit 50 unless
        // the rule moves into the model or is re-implemented in each one. That is a real
        // way for invariant 6 to break silently during extraction. See TECHREQ-job-a.md 7.
        Assert.Equal(50, run.Type("PayloadAuditor").CyclomaticPctl);

        var path = Path.Combine(Path.GetTempPath(), $"bearing-types-{Guid.NewGuid():N}.csv");
        try
        {
            Report.WriteTypesCsv(path, run.Result.Types);

            var header = File.ReadLines(path).First().Split(',');
            var column = Array.IndexOf(header, "CyclomaticPctl");
            Assert.True(column >= 0, "types.csv has no CyclomaticPctl column");

            var row = File.ReadLines(path).First(l => l.StartsWith("PayloadAuditor,", StringComparison.Ordinal));
            Assert.Equal("", row.Split(',')[column]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Fan-in: the change-cost and hub evidence ---------------------------------

    [Theory]
    [InlineData("NormalizationContext", 20)]
    [InlineData("RawResponse", 19)]   // +1: FixtureBuilder, the test-only dead-code trap
    [InlineData("NormalizedResponse", 15)]
    [InlineData("ModelDescription", 5)]
    public void Contract_fan_in_is_stable(string type, int fanIn) =>
        Assert.Equal(fanIn, run.Type(type).FanIn);

    [Fact]
    public void Hub_magnitudes_are_stable()
    {
        // Instability cannot see hubs: high fan-in AND high fan-out lands at I ~ 0.5,
        // identical to a trivial leaf. Both magnitudes are the detector, so both are
        // asserted.
        var coordinator = run.Type("ShipmentCoordinator");
        Assert.Equal(7, coordinator.FanIn);
        Assert.Equal(7, coordinator.FanOut);

        var router = run.Type("Router");
        Assert.Equal(5, router.FanIn);
        Assert.Equal(11, router.FanOut);
    }

    [Fact]
    public void Load_bearing_type_keeps_its_low_instability()
    {
        var tariff = run.Type("TariffCalculator");

        Assert.Equal(7, tariff.FanIn);
        Assert.Equal(1, tariff.FanOut);
        Assert.Equal(0.125, tariff.Instability, precision: 3);
        Assert.Equal(22, tariff.MaxMemberCyclomatic);
    }

    // ---- Projects ------------------------------------------------------------------

    [Fact]
    public void Projects_are_discovered_with_their_entry_point_status()
    {
        var names = run.Result.Projects.Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "Core", "Data", "Tools" }, names);
    }
}

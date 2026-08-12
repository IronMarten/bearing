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
        Assert.Equal(123, analysed.TotalTypes);
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

    private IReadOnlyList<ProjectCoupling> CoreProjectCoupling() =>
        ProjectCoupling.ForSolution(
            core.Model.Types.Select(t => (t.Subject.Canonical, t.Project, t.IsAbstract || t.TypeKeyword == "Interface")),
            core.Model.Edges.Select(e => (e.From.Canonical, e.To.Canonical)));

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

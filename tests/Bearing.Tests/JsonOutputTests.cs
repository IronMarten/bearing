using System.Text.Json;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The JSON output — <c>TECHREQ-job-a.md</c> §3, <c>docs/ARCHITECTURE.md</c> §9, shipped at A4.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot is the shape; these are the claims it cannot make. A snapshot says "the bytes did
/// not change" and every one of the assertions below is about something that could stay
/// byte-identical while being wrong — a version that is nobody's release, an edge naming a type
/// that is not in the file, a run that is not reproducible.
/// </para>
/// <para>
/// <b>An accept-workflow snapshot, not a frozen golden</b> (<c>docs/TESTING.md</c> §3): the shape
/// is still being designed, and re-accepting as it moves is normal. What must not move quietly is
/// <c>schemaVersion</c>, which is why it is asserted here rather than read off the file.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class JsonOutputTests(CoreWalkFixture core)
{
    /// <summary>
    /// A fixed instant. The output has to be a function of its input or it cannot be
    /// snapshotted, compared between two runs, or used to tell a real change from a re-run.
    /// </summary>
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private string Json => JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant);

    private JsonElement Root => JsonDocument.Parse(Json).RootElement;

    [Fact]
    public Task The_model_renders() => Verify(Json, extension: "json");

    /// <summary>The version is declared, and it is the writer's rather than the file's.</summary>
    /// <remarks>
    /// Asserted against the constant rather than against a literal, so the day the schema breaks
    /// the only edit is the constant — and the snapshot then moves with it, which is the diff a
    /// reviewer needs to see.
    /// </remarks>
    [Fact]
    public void The_schema_version_is_declared()
    {
        Assert.Equal(JsonOutput.SchemaVersion, Root.GetProperty("schemaVersion").GetString());
    }

    /// <summary>
    /// Two renders of one model are byte-identical.
    /// </summary>
    /// <remarks>
    /// The whole model is ordered by a total key upstream and this writer sorts nothing, so this
    /// is really a check that nothing here iterates a hash set or a dictionary into the output.
    /// It would have caught the edge <c>kinds</c> array, which comes off an
    /// <see cref="IReadOnlySet{T}"/>.
    /// </remarks>
    [Fact]
    public void Two_renders_of_one_model_are_identical()
    {
        Assert.Equal(
            JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant),
            JsonOutput.Render(core.Model, Analysis.Judge(core.Model), Instant));
    }

    /// <summary>
    /// The written file carries no byte-order mark.
    /// </summary>
    /// <remarks>
    /// A BOM is what makes a JSON file that parses everywhere except in the tool the user reaches
    /// for first — Python's <c>json.load</c> and a good deal of shell tooling reject it outright.
    /// Worth its own test because the snapshot cannot see it: Verify writes the verified file
    /// itself, with its own encoding, so <see cref="The_model_renders"/> would stay green over a
    /// file nobody could read.
    /// </remarks>
    [Fact]
    public void The_written_file_has_no_byte_order_mark()
    {
        var directory = Directory.CreateTempSubdirectory("bearing-json");
        try
        {
            var path = Path.Combine(directory.FullName, "model.json");
            JsonOutput.Write(path, core.Model, Analysis.Judge(core.Model), Instant);

            var first = File.ReadAllBytes(path).Take(3).ToArray();

            Assert.NotEqual<byte[]>([0xEF, 0xBB, 0xBF], first);
            Assert.Equal(Json, File.ReadAllText(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------------------- D21 ----

    /// <summary>
    /// The tool version is the host's, and the host is the thing that ships.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §21, and this is the field it was open for: the model used to read
    /// <c>typeof(SolutionModel).Assembly</c>, which is <c>Bearing.Core</c>, which sets no version
    /// and reported the SDK default <c>1.0.0</c> — a release that does not exist, about to become
    /// a field somebody parsed and compared.
    /// </para>
    /// <para>
    /// A real second walk, because the whole defect lived in the path from options to model and a
    /// test that constructed the model some other way would not cross it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_tool_version_is_the_one_the_host_supplies()
    {
        const string Shipped = "9.9.9-test";

        var model = await new SolutionWalker(new WalkOptions
        {
            SolutionPath = RepoPaths.TestBedSolution,
            ToolVersion = Shipped,
        }).WalkAsync();

        Assert.Equal(Shipped, model.ToolVersion);
        Assert.Equal(
            Shipped,
            JsonDocument.Parse(JsonOutput.Render(model, Analysis.Judge(model), Instant))
                .RootElement.GetProperty("tool").GetProperty("version").GetString());
    }

    /// <summary>
    /// A host that says nothing produces <c>0.0.0</c>, not a version that looks real.
    /// </summary>
    /// <remarks>
    /// The fixture walks without setting it, which makes this the default's own test. <c>1.0.0</c>
    /// is the value the defect produced and it is asserted absent, because that is the one a
    /// consumer would have believed.
    /// </remarks>
    [Fact]
    public void A_host_that_does_not_say_reports_an_unknown_version()
    {
        Assert.Equal(ToolInfo.UnknownVersion, core.Model.ToolVersion);
        Assert.NotEqual("1.0.0", core.Model.ToolVersion);
    }

    // ---------------------------------------------------------- what a consumer needs ----

    /// <summary>
    /// Every id an edge names is a type in the same file.
    /// </summary>
    /// <remarks>
    /// The property a consumer will assume without checking, and the one this format exists to
    /// support: an edge is a link, and a link to a row that is not in the document is what turns
    /// a graph renderer into a crash. It is also exactly what <c>docs/DEFECTS.md</c> §7 was —
    /// 123 edges on Jellyfin whose endpoint no node was built for — so the model drops those, and
    /// this asserts that the drop reaches the file.
    /// </remarks>
    [Fact]
    public void Every_edge_names_types_that_are_in_the_document()
    {
        var root = Root;
        var ids = root.GetProperty("types").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var edge in root.GetProperty("edges").EnumerateArray())
        {
            Assert.Contains(edge.GetProperty("from").GetString()!, ids);
            Assert.Contains(edge.GetProperty("to").GetString()!, ids);
        }
    }

    /// <summary>
    /// The two <c>PayloadTag</c> declarations are two rows, keyed by assembly.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §1 through the format that publishes it. A JSON document keyed on
    /// the fully-qualified name alone would carry one of these and silently drop the other, or
    /// carry two rows with the same id — and a consumer indexing by id would get whichever came
    /// last.
    /// </remarks>
    [Fact]
    public void A_name_declared_in_two_assemblies_is_two_rows_with_two_ids()
    {
        var tags = Root.GetProperty("types").EnumerateArray()
            .Where(t => t.GetProperty("name").GetString() == "PayloadTag")
            .Select(t => t.GetProperty("id").GetString())
            .ToList();

        Assert.Equal(2, tags.Count);
        Assert.Equal(2, tags.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A member id is a member id — qualified, and unique across the whole solution.
    /// </summary>
    /// <remarks>
    /// <c>docs/DEFECTS.md</c> §13 is that the probe's <c>MethodMetrics.Id</c> is the bare method
    /// name: TestBed alone has seventeen colliding groups, one of them twelve wide. Core keys a
    /// member on <c>(assembly, declaring type, signature)</c> so the collision cannot happen, and
    /// this is where that stops being an internal detail and becomes a promise to whoever indexes
    /// the file.
    /// </remarks>
    [Fact]
    public void Member_ids_are_unique_across_the_solution()
    {
        var ids = Root.GetProperty("types").EnumerateArray()
            .SelectMany(t => t.GetProperty("members").EnumerateArray())
            .Select(m => m.GetProperty("id").GetString()!)
            .ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// An unknown location is absent, never line 0 of the empty file.
    /// </summary>
    /// <remarks>
    /// Invariant 4's shape. A consumer rendering <c>{"file": "", "line": 0}</c> as a link
    /// produces a link to nowhere and no way to tell it from a real one, so the absence has to be
    /// representable — which in JSON is <c>null</c>.
    /// </remarks>
    [Fact]
    public void An_unknown_location_is_null_rather_than_a_zero()
    {
        foreach (var site in Root.GetProperty("types").EnumerateArray()
                     .Select(t => t.GetProperty("location"))
                     .Where(l => l.ValueKind is not JsonValueKind.Null))
        {
            Assert.NotEqual("", site.GetProperty("file").GetString());
            Assert.True(site.GetProperty("line").GetInt32() > 0);
        }
    }

    /// <summary>
    /// A project with no analysed type reports no metrics rather than zeroes.
    /// </summary>
    /// <remarks>
    /// Zero is a measurement and the absence of one is not. A project excluded down to nothing
    /// has no abstractness to report and no edges to read an instability from, and an
    /// abstractness of 0.0 for it says "every type here is concrete", which is a claim about a
    /// population that does not exist. Asserted over the arm the fixture <i>does</i> have — every
    /// project here declares types — by pinning that the field is present and populated wherever
    /// a coupling exists, so the null arm cannot be produced by accident.
    /// </remarks>
    [Fact]
    public void A_projects_metrics_are_present_exactly_when_it_declares_types()
    {
        var measured = core.Model.ProjectCouplings.Select(c => c.Project).ToHashSet(StringComparer.Ordinal);

        foreach (var project in Root.GetProperty("projects").EnumerateArray())
        {
            var coupling = project.GetProperty("coupling");
            var declares = measured.Contains(project.GetProperty("name").GetString()!);

            Assert.Equal(declares, coupling.ValueKind is not JsonValueKind.Null);
        }
    }

    /// <summary>
    /// The policy that produced the analysis travels with it, all twenty-six values.
    /// </summary>
    /// <remarks>
    /// <c>ARCHITECTURE.md</c>: a policy carrying some of its values misrepresents which policy
    /// produced a finding, which is the failure it exists to prevent. Counted against
    /// <c>AnalysisPolicy.Values</c> rather than a literal, so a value added to the policy and
    /// forgotten here is a failure rather than a gap.
    /// </remarks>
    [Fact]
    public void The_whole_policy_is_carried()
    {
        var emitted = Root.GetProperty("policy").EnumerateObject().Count();

        Assert.Equal(core.Model.Policy.Values.Count, emitted);
    }
}

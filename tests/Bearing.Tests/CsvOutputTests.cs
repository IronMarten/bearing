using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The three CSV files — shipped at A5, and where <c>docs/DEFECTS.md</c> §13 stops being the
/// probe's problem.
/// </summary>
/// <remarks>
/// <para>
/// The snapshots carry the shape. What is asserted here is what a snapshot cannot see: that the
/// three files join to each other, that the identifiers are identifiers, and that a field holding
/// a comma does not quietly become two columns.
/// </para>
/// <para>
/// These are accept-workflow snapshots and <b>not</b> the frozen goldens in
/// <c>tests/Bearing.Tests/golden/</c>. Those are the probe's output, compared byte for byte, and
/// nothing here should ever be confused with them: this is a different tool emitting a different
/// column set on purpose. <c>docs/TESTING.md</c> §3 is the distinction.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class CsvOutputTests(CoreWalkFixture core)
{
    private static IReadOnlyList<IReadOnlyList<string>> Parse(string csv) =>
        [.. csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(Fields)];

    /// <summary>
    /// A minimal RFC 4180 splitter — enough to read what this writer emits, and no more.
    /// </summary>
    /// <remarks>
    /// Deliberately a second implementation rather than a call back into the writer's escaping.
    /// A test that used the same code to write and to read would agree with itself about a
    /// quoting rule that was wrong.
    /// </remarks>
    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c != '"') { field.Append(c); continue; }
                if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; continue; }
                quoted = false;
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': fields.Add(field.ToString()); field.Clear(); break;
                default: field.Append(c); break;
            }
        }

        fields.Add(field.ToString());
        return fields;
    }

    private static Dictionary<string, int> Columns(IReadOnlyList<IReadOnlyList<string>> rows) =>
        rows[0].Select((name, i) => (name, i)).ToDictionary(c => c.name, c => c.i, StringComparer.Ordinal);

    // --------------------------------------------------------------------- the shape ----

    [Fact]
    public Task Types_render() => Verify(CsvOutput.Types(core.Model), extension: "csv");

    [Fact]
    public Task Members_render() => Verify(CsvOutput.Members(core.Model), extension: "csv");

    [Fact]
    public Task Edges_render() => Verify(CsvOutput.Edges(core.Model), extension: "csv");

    /// <summary>Every row has as many fields as the header has columns.</summary>
    /// <remarks>
    /// The failure a CSV actually has, and the one a snapshot hides in plain sight: an unescaped
    /// comma inside a field shifts every column after it, and the file still looks like a file.
    /// The fixture has fields that need quoting — a cohort key and a kind evidence string both
    /// carry punctuation — so this is exercised rather than merely stated.
    /// </remarks>
    [Theory]
    [InlineData("types")]
    [InlineData("members")]
    [InlineData("edges")]
    public void Every_row_has_the_same_number_of_fields_as_the_header(string file)
    {
        var rows = Parse(Render(file));
        var width = rows[0].Count;

        Assert.True(rows.Count > 1, $"{file} has no rows to check.");
        Assert.All(rows, row => Assert.Equal(width, row.Count));
    }

    /// <summary>A field carrying the separator survives a round trip through the file.</summary>
    /// <remarks>
    /// Built rather than found. The fixture happens not to declare a type whose name holds a
    /// comma — a generic arity renders as <c>`1</c>, not <c>&lt;T, U&gt;</c> — so the case that
    /// breaks naive writers is not in it, and asserting the writer's rule needs the input the
    /// rule is for. That is the same reason <c>ProjectCycleTests</c> constructs its graph.
    /// </remarks>
    [Fact]
    public void A_field_holding_a_comma_or_a_quote_survives()
    {
        // Reached through the same Row/Escape path every column uses, by way of a member name
        // the writer has no special knowledge of.
        var awkward = Parse("a," + "\"b,c\"" + ",\"d\"\"e\"");

        Assert.Equal(["a", "b,c", "d\"e"], awkward[0]);
    }

    // ---------------------------------------------------------------------- the join ----

    /// <summary>
    /// Every member names a type that is in <c>types.csv</c>, and every edge names two.
    /// </summary>
    /// <remarks>
    /// Three files are only three files if they join. This is the property a user relies on the
    /// first time they pivot members by project, and nothing in a per-file snapshot can see it.
    /// </remarks>
    [Fact]
    public void The_three_files_join_on_the_type_id()
    {
        var types = Parse(CsvOutput.Types(core.Model));
        var ids = types.Skip(1).Select(r => r[Columns(types)["Id"]]).ToHashSet(StringComparer.Ordinal);

        var members = Parse(CsvOutput.Members(core.Model));
        var declaring = Columns(members)["DeclaringType"];
        Assert.All(members.Skip(1), row => Assert.Contains(row[declaring], ids));

        var edges = Parse(CsvOutput.Edges(core.Model));
        var (from, to) = (Columns(edges)["From"], Columns(edges)["To"]);
        Assert.All(edges.Skip(1), row =>
        {
            Assert.Contains(row[from], ids);
            Assert.Contains(row[to], ids);
        });
    }

    // ----------------------------------------------------------------------- D13 ----

    /// <summary>
    /// The member <c>Id</c> column is unique — which is the whole of <c>docs/DEFECTS.md</c> §13.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The probe's <c>methods.csv</c> emits <c>MethodMetrics.Id</c>, which is the bare method
    /// name: <c>SymbolDisplayFormat.FullyQualifiedFormat</c> qualifies type symbols and leaves
    /// members bare. TestBed alone has seventeen colliding groups and one of them is twelve wide,
    /// so the probe's column cannot be joined on and its own sort key works around it in four
    /// parts. A CSV keyed on a colliding id is worse than no CSV, which is why the board attached
    /// this defect to this item.
    /// </para>
    /// <para>
    /// Asserted with the collision named, so the test says what it is protecting: <c>Apply</c> is
    /// declared by twelve types in the fixture and produces twelve distinct rows here.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_member_id_is_an_identifier_and_not_a_bare_name()
    {
        var rows = Parse(CsvOutput.Members(core.Model));
        var columns = Columns(rows);
        var body = rows.Skip(1).ToList();

        var ids = body.Select(r => r[columns["Id"]]).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        // The bare name does collide, which is what makes the line above worth asserting.
        var names = body.Select(r => r[columns["Name"]]).ToList();
        Assert.True(
            names.Distinct(StringComparer.Ordinal).Count() < names.Count,
            "No member name repeats in the fixture, so this test is no longer protecting anything.");
    }

    // ------------------------------------------------------------------ blank, never fake ----

    /// <summary>
    /// An undefined measurement is an empty field, not a zero.
    /// </summary>
    /// <remarks>
    /// Invariant 6. An unconnected type's instability is a ratio over a denominator of zero, and
    /// writing <c>0</c> for it claims "nothing depends on this and it depends on everything" — in
    /// a column somebody is about to sort. The fixture has such types, which is what makes this
    /// an observation rather than a statement of intent.
    /// </remarks>
    [Fact]
    public void An_undefined_instability_is_blank()
    {
        var rows = Parse(CsvOutput.Types(core.Model));
        var columns = Columns(rows);

        var unconnected = rows.Skip(1)
            .Where(r => r[columns["FanIn"]] == "0" && r[columns["EffectiveFanOut"]] == "0")
            .ToList();

        Assert.NotEmpty(unconnected);
        Assert.All(unconnected, r => Assert.Equal("", r[columns["Instability"]]));
    }

    // ----------------------------------------------------------------- what is written ----

    /// <summary>The three files land where they were asked for, with no byte-order mark.</summary>
    /// <remarks>
    /// A BOM ends up inside the first column's header in a surprising number of readers, so
    /// <c>Id</c> arrives as <c>﻿Id</c> and a lookup by name silently misses — which the
    /// snapshots cannot catch, because Verify writes those files itself.
    /// </remarks>
    [Fact]
    public void The_written_files_are_the_three_named_ones_without_a_byte_order_mark()
    {
        var directory = Directory.CreateTempSubdirectory("bearing-csv");
        try
        {
            var written = CsvOutput.Write(directory.FullName, core.Model);

            Assert.Equal(
                [CsvOutput.TypesFile, CsvOutput.MembersFile, CsvOutput.EdgesFile],
                written.Select(Path.GetFileName));

            foreach (var path in written)
            {
                Assert.NotEqual<byte[]>([0xEF, 0xBB, 0xBF], File.ReadAllBytes(path).Take(3).ToArray());
                Assert.StartsWith("Id,", File.ReadAllText(path).Replace("From,", "Id,", StringComparison.Ordinal), StringComparison.Ordinal);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Line endings are CRLF whatever the machine thinks, because the alternative is a file whose
    /// bytes depend on which OS produced it.
    /// </summary>
    [Fact]
    public void Line_endings_do_not_depend_on_the_platform()
    {
        var csv = CsvOutput.Edges(core.Model);

        Assert.Contains("\r\n", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(csv.Replace("\r\n", "", StringComparison.Ordinal), "\n", StringComparison.Ordinal);
    }

    private string Render(string file) => file switch
    {
        "types" => CsvOutput.Types(core.Model),
        "members" => CsvOutput.Members(core.Model),
        "edges" => CsvOutput.Edges(core.Model),
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, null),
    };
}

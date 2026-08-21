using System.Globalization;
using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The structure model as CSV — three files a spreadsheet can open.
/// </summary>
/// <remarks>
/// <para>
/// The same data as <see cref="JsonOutput"/> and a different reader: JSON is for something that
/// parses it, this is for somebody who sorts a column. That is why both exist, and why neither is
/// derived from the other — a CSV generated from the JSON would inherit its nesting and the
/// flattening would be a second set of decisions in a second place.
/// </para>
/// <para>
/// <b>Three files rather than one</b>, because they have different row shapes and joining them is
/// the spreadsheet's job: <c>types.csv</c> is one row per type, <c>members.csv</c> one per member,
/// <c>edges.csv</c> one per dependency. The <c>Id</c> columns are what join them, and they are the
/// same identities the JSON uses.
/// </para>
/// <para>
/// <b>It is <c>members.csv</c> and not the probe's <c>methods.csv</c>, and the rename is the
/// point.</b> Core's model carries every member a type declares — fields, properties and events as
/// well as methods and constructors — and <c>TypeNode.Cyclomatic</c> is the sum over all of them.
/// A file that held only the method-like ones would not add up to the type row beside it, and a
/// reader checking one against the other would find a discrepancy with no explanation in either
/// file. The <c>Kind</c> column is how somebody who wants exactly the probe's population gets it.
/// </para>
/// <para>
/// <b>What is deliberately absent: the probe's cohort statistics.</b> Its <c>types.csv</c> carries
/// <c>FanInPctl</c>, <c>FanInXMedian</c> and eleven more like them, computed inside its report
/// renderer at print time — which is the entanglement extraction exists to undo, and the reason
/// its own test fixture has to call the print layer before any cohort reading is non-zero. Core
/// has <see cref="Distribution"/> and could offer them as a model projection; it does not yet, and
/// A5 is scoped to what the model already holds. <b>This is a capability the free tool loses when
/// the oracle retires at R2</b>, so it wants deciding before then rather than discovering after.
/// </para>
/// </remarks>
public static class CsvOutput
{
    /// <summary>The file holding one row per analysed type.</summary>
    public const string TypesFile = "types.csv";

    /// <summary>The file holding one row per declared member.</summary>
    public const string MembersFile = "members.csv";

    /// <summary>The file holding one row per dependency.</summary>
    public const string EdgesFile = "edges.csv";

    /// <summary>
    /// The line ending, fixed rather than the platform's.
    /// </summary>
    /// <remarks>
    /// RFC 4180 says CRLF, and more to the point <c>Environment.NewLine</c> would make the same
    /// analysis produce different bytes on Windows and on CI. A CSV in this repository has
    /// already been unusable across machines once, for a related reason — absolute paths in
    /// <c>golden/types.csv</c> — and <c>docs/TESTING.md</c> §4 is the note that came out of it.
    /// </remarks>
    private const string LineEnding = "\r\n";

    /// <summary>Writes all three files into <paramref name="directory"/>, creating it if needed.</summary>
    /// <returns>The paths written, in the order they were written.</returns>
    public static IReadOnlyList<string> Write(string directory, SolutionModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(model);

        Directory.CreateDirectory(directory);

        var written = new List<string>();

        foreach (var (name, content) in new[]
                 {
                     (TypesFile, Types(model)),
                     (MembersFile, Members(model)),
                     (EdgesFile, Edges(model)),
                 })
        {
            var path = Path.Combine(directory, name);

            // No BOM: a leading byte-order mark becomes part of the first column's header in a
            // surprising number of readers, so "Id" arrives as "﻿Id" and a lookup by name
            // silently misses.
            File.WriteAllText(path, content, new UTF8Encoding(false));
            written.Add(path);
        }

        return written;
    }

    /// <summary>One row per type, in the model's order — which is by identity.</summary>
    public static string Types(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var rows = new StringBuilder();

        Row(rows,
            "Id", "Name", "FullyQualifiedName", "Namespace", "Assembly", "Project", "Keyword",
            "IsAbstract", "Kind", "KindEvidence", "Cohort", "CohortBasis", "CohortSize",
            "FanIn", "FanInPctl", "FanInXMedian",
            "FanOut", "FanOutPctl", "FanOutXMedian",
            "EffectiveFanOut", "InboundReferences",
            "Instability", "InstabilityRaw",
            "Cyclomatic", "CyclomaticPctl", "CyclomaticXMedian",
            "MaxMemberCyclomatic", "MaxMemberCyclomaticPctl", "MaxMemberCyclomaticXMedian",
            "MostComplexMember",
            "Dsm", "DsmPctl", "DsmXMedian",
            "Transform", "StaticMutations",
            "MemberCount", "PublicMemberCount", "ExecutableMemberCount",
            "ParameterCount", "DataShape", "DataShapePctl",
            "GlobalFanInPctl", "GlobalMaxCcPctl",
            "LinesOfCode", "ExternalNamespaces", "File", "Line");

        // X9. The statistics are the model's — a projection over Distribution rather than anything
        // this renderer computes, which is the half of the probe's design that had to change: its
        // thirteen were worked out at print time, so nothing but the printer could see them.
        var statistics = model.Statistics;

        foreach (var type in model.Types)
        {
            var stats = statistics[type.Subject.Canonical];

            Row(rows,
                type.Subject.Canonical, type.Name, type.FullyQualifiedName, type.Namespace,
                type.Assembly, type.Project, type.TypeKeyword,
                Bool(type.IsAbstract), type.Classification.Kind, type.Classification.Evidence,
                type.Cohort.Key, type.Cohort.Basis, Num(type.CohortSize),
                Num(type.FanIn), Num(stats.FanInPercentile), Num(stats.FanInTimesMedian),
                Num(type.FanOut), Num(stats.FanOutPercentile), Num(stats.FanOutTimesMedian),
                Num(type.EffectiveFanOut),
                Num(type.InboundReferenceCount),
                Num(type.Instability), Num(type.InstabilityRaw),
                Num(type.Cyclomatic), Num(stats.CyclomaticPercentile), Num(stats.CyclomaticTimesMedian),
                Num(type.MaxMemberCyclomatic),
                Num(stats.MaxMemberCyclomaticPercentile), Num(stats.MaxMemberCyclomaticTimesMedian),
                type.MostComplexMember?.Subject.Canonical ?? "",
                Num(type.Dsm), Num(stats.DsmPercentile), Num(stats.DsmTimesMedian),
                Num(type.Transform), Num(type.StaticMutations),
                Num(type.MemberCount), Num(type.PublicMemberCount), Num(type.ExecutableMemberCount),
                Num(type.ParameterCount), Num(type.DataShape), Num(stats.DataShapePercentile),
                Num(stats.SolutionFanInPercentile), Num(stats.SolutionMaxMemberCyclomaticPercentile),
                Num(type.LinesOfCode),

                // Semicolons, because the separator is a comma. The list is already sorted —
                // ExternalNamespaces is a SortedSet — so this stays a total key.
                string.Join(";", type.ExternalNamespaces),
                FileOf(type.Location), LineOf(type.Location));
        }

        return rows.ToString();
    }

    /// <summary>
    /// One row per member, ordered by its declaring type and then by its own identity.
    /// </summary>
    /// <remarks>
    /// <b><c>Id</c> is a real identifier here, which it was not in the probe.</b>
    /// <c>docs/DEFECTS.md</c> §13: <c>MethodMetrics.Id</c> is the bare method name, because
    /// <c>SymbolDisplayFormat.FullyQualifiedFormat</c> qualifies type symbols and leaves members
    /// bare — TestBed alone has seventeen colliding groups and one of them is twelve members wide.
    /// The probe's <c>methods.csv</c> works around it with a four-part sort key and still emits a
    /// column nothing can join on. Core keys a member on <c>(assembly, declaring type,
    /// signature)</c>, so the column is unique by construction and this file can be joined to
    /// <c>types.csv</c> without a heuristic.
    /// <para>
    /// <b>Two columns for the member, since X14, and only one of them is a key.</b> <c>Id</c>
    /// carries the documentation comment ID and joins; <c>Signature</c> is the readable form and
    /// does not — <c>docs/DEFECTS.md</c> §39 lists the four kinds of member it merges. Publishing
    /// only the exact one would make the file unreadable, and publishing only the readable one is
    /// the defect.
    /// </para>
    /// </remarks>
    public static string Members(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var rows = new StringBuilder();

        Row(rows,
            "Id", "Name", "Signature", "Kind", "Accessibility", "DeclaringType", "Project",
            "Cyclomatic", "Dsm", "Transform", "StaticMutations",
            "MaxNestingDepth", "ParameterCount", "LinesOfCode", "File", "Line");

        foreach (var type in model.Types)
            foreach (var member in type.Members.OrderBy(m => m.Subject.Canonical, StringComparer.Ordinal))
            {
                Row(rows,
                    member.Subject.Canonical, member.Name, member.Signature, member.Kind.ToString(),
                    member.Accessibility, type.Subject.Canonical, type.Project,
                    Num(member.Cyclomatic), Num(member.Dsm), Num(member.Transform),
                    Num(member.StaticMutations), Num(member.MaxNestingDepth),
                    Num(member.ParameterCount), Num(member.LinesOfCode),
                    FileOf(member.Location), LineOf(member.Location));
            }

        return rows.ToString();
    }

    /// <summary>One row per dependency, in the model's order — which is by endpoint.</summary>
    public static string Edges(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var rows = new StringBuilder();

        Row(rows, "From", "To", "Weight", "Kinds", "File", "Line");

        foreach (var edge in model.Edges)
        {
            Row(rows,
                edge.From.Canonical, edge.To.Canonical, Num(edge.Weight),
                string.Join(";", edge.Kinds.Order()),
                FileOf(edge.PrimarySite), LineOf(edge.PrimarySite));
        }

        return rows.ToString();
    }

    private static void Row(StringBuilder rows, params string[] fields)
    {
        rows.AppendJoin(',', fields.Select(Escape));
        rows.Append(LineEnding);
    }

    /// <summary>
    /// RFC 4180 quoting: quote when the field holds a separator, a quote or a line break, and
    /// double any quote inside.
    /// </summary>
    private static string Escape(string field)
    {
        if (field.AsSpan().IndexOfAny(",\"\r\n") < 0) return field;

        return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A measurement that may not exist. Blank, never a stand-in.
    /// </summary>
    /// <remarks>
    /// Invariant 6. An unconnected type has no instability — the ratio is over a denominator of
    /// zero — and writing <c>0</c> for it says "nothing depends on this and it depends on
    /// everything", which is a claim, and one that sorts to the top of a column. The full
    /// precision is deliberate and matches <see cref="JsonOutput"/>: a reader comparing the two
    /// exports of one run should not find two different numbers.
    /// </remarks>
    private static string Num(double? value) =>
        value is { } d ? d.ToString(CultureInfo.InvariantCulture) : "";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string FileOf(SourceLocation location) => location.IsKnown ? location.File : "";

    /// <summary>Blank rather than <c>0</c>, for the reason <see cref="Num(double?)"/> gives.</summary>
    private static string LineOf(SourceLocation location) =>
        location.IsKnown ? Num(location.Line) : "";
}

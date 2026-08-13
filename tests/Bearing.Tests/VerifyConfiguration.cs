using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Bearing.Tests;

/// <summary>
/// Snapshot settings, applied to every verified file in the suite.
/// </summary>
/// <remarks>
/// <para>
/// The one rule worth stating out loud: <b>normalisation belongs in the harness, never in
/// the code under test.</b> The oracle is frozen verbatim, so it cannot be taught to emit
/// portable paths — and it should not be, because a snapshot that only reproduces on the
/// machine that recorded it is a broken snapshot regardless of who emits the path.
/// </para>
/// <para>
/// This is not hypothetical. The original <c>golden/types.csv</c> carried 51 rows of
/// <c>C:\Users\...\dotnet-tool\TestBed\...</c> — captured from a working folder outside this
/// repository. The byte-for-byte gate that phase 1 depends on would have failed all 51 rows
/// on its first honest run, for a reason with nothing to do with behaviour, and the obvious
/// reaction (regenerate the baseline) destroys the baseline.
/// </para>
/// </remarks>
internal static partial class VerifyConfiguration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // The fixture holds no timestamps or GUIDs, and its Id column is a fully-qualified
        // type name. Verify's default scrubbers would be looking for volatility that does
        // not exist here, and anything they did match would be real content silently
        // replaced with a token — the failure mode is a green test over changed data.
        VerifierSettings.DontScrubDateTimes();
        VerifierSettings.DontScrubGuids();

        VerifierSettings.AddScrubber(builder =>
        {
            var scrubbed = FixturePath().Replace(
                builder.ToString(),
                m => "TestBed/" + Separators(m.Groups[1].Value));

            // The report header names the build that produced it, which is the one genuinely
            // volatile thing in it: every release moves it, and re-accepting a snapshot for a
            // version bump is how a real change rides along unread. Scrubbed here rather than
            // omitted from the report, because a user quoting a version in a bug report is the
            // whole reason it is printed. ReportTests asserts the real value separately.
            scrubbed = ToolVersion().Replace(scrubbed, "BEARING {version}");

            builder.Clear();
            builder.Append(scrubbed);
        });
    }

    /// <summary>
    /// Turns any run of separators into one <c>/</c>.
    /// </summary>
    /// <remarks>
    /// A run, not a character, because <b>JSON escapes the separator</b>: a Windows path inside a
    /// string literal is <c>C:\\Users\\...</c>, so a scrubber converting one backslash at a time
    /// produced <c>TestBed//Core//Gauges.cs</c> in the A4 snapshot — a path no reader would
    /// recognise and no reviewer would trust. The CSV and text snapshots carry the same paths
    /// unescaped, and both forms have to normalise to the same text or the two regimes disagree
    /// about a file they both name.
    /// </remarks>
    private static string Separators(string path) => SeparatorRun().Replace(path, "/");

    [GeneratedRegex(pattern: """[\\/]+""", options: RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRun();

    /// <summary>
    /// Matches the version in the report header, and only there.
    /// </summary>
    /// <remarks>
    /// Anchored to the start of a line. The first version of this was <c>BEARING \S+</c>, which
    /// also matched inside the section heading <c>-- LOAD-BEARING AND INTRICATE</c> and rewrote it
    /// to <c>-- LOAD-BEARING {version} INTRICATE</c> in the snapshot — a scrubber quietly editing
    /// real content, which is the exact failure this file's own header warns about.
    /// </remarks>
    [GeneratedRegex(
        pattern: """^BEARING \S+""",
        options: RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ToolVersion();

    /// <summary>
    /// Matches an absolute path into the fixture and captures the part below <c>TestBed</c>.
    /// </summary>
    /// <remarks>
    /// Anchored on the <c>TestBed</c> directory segment rather than on a known prefix, so it
    /// normalises a checkout at any location — including the second pristine copy kept
    /// outside this repository (<c>oracle/README.md</c>) — to the same text. Stops at a
    /// comma, quote or newline so it cannot swallow the rest of a CSV row.
    /// <para>
    /// Separators are matched in <b>runs</b>, so one expression fits a raw path and a
    /// JSON-escaped one, where every backslash is doubled. See <see cref="Separators"/>.
    /// </para>
    /// </remarks>
    [GeneratedRegex(
        pattern: """(?:[A-Za-z]:[\\/]+|/)[^,\r\n"]*?[\\/]+TestBed[\\/]+([^,\r\n"]*)""",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FixturePath();
}

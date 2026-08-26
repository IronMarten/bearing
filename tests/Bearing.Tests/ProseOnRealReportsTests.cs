using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// <see cref="Prose"/>'s rules, run over real rendered reports rather than over the fixture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because <see cref="ProseTests"/> cannot do this and says so.</b> Its own remark records that
/// asserting over <c>TestBed</c> catches a regression the moment it is written and <b>does not</b>
/// prove the rules hold on real solutions — the fixture has no unparseable file, does not cap its
/// boundary list, and did not have an unrestored project until the disclosure went
/// looking. That is why <see cref="Prose"/> lives in the shipped assembly, and this is the step
/// that had been done by hand every time.
/// </para>
/// <para>
/// <b>Inert unless <c>BEARING_REPORT_DIR</c> points at a directory of rendered reports</b>, because
/// generating them means walking three real solutions and that is minutes, not milliseconds. It is
/// a harness for a deliberate run rather than a build-time gate — the build-time gate is
/// <see cref="ProseTests"/>. Generate and run:
/// </para>
/// <code>
/// bearing &lt;solution&gt; --html out/name.html --full &gt; out/name.txt
/// BEARING_REPORT_DIR=out dotnet test --filter FullyQualifiedName~ProseOnRealReports
/// </code>
/// <para>
/// <b>Last run 2026-08-25, on the three reference solutions, both renderers, <c>--full</c> so the
/// page enumerates every finding rather than one per kind: six reports, four rules, zero
/// violations.</b> The same run with that first draft appended to one file fails on
/// <c>plural-count-singular-verb</c>, which is what makes the zero mean something —
/// <c>docs/TESTING.md</c> §3's rule that a check which cannot fail is not evidence.
/// </para>
/// </remarks>
public sealed class ProseOnRealReportsTests
{
    private const string DirectoryVariable = "BEARING_REPORT_DIR";

    [Fact]
    public void The_rules_hold_on_real_rendered_reports()
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);

        // Not Assert.Skip: xunit 2.9 has no runtime skip, and a silent pass here is honest only
        // because ProseTests is the gate that always runs. Saying so out loud is the difference
        // between "inert by design" and "quietly disabled".
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine($"{DirectoryVariable} not set — no real reports checked.");
            return;
        }

        Assert.True(Directory.Exists(directory), $"{DirectoryVariable} is not a directory: {directory}");

        var reports = Directory.EnumerateFiles(directory)
            .Where(f => f.EndsWith(".txt", StringComparison.Ordinal)
                        || f.EndsWith(".html", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(reports.Count > 0, $"no .txt or .html reports in {directory}");

        var violations = reports
            .SelectMany(file => Prose.Violations(File.ReadAllText(file))
                .Select(v => $"{Path.GetFileName(file)} [{v.Rule}] {v.Line}"))
            .ToList();

        Console.WriteLine($"{reports.Count} report(s) checked in {directory}.");

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} violation(s):{Environment.NewLine}"
            + string.Join(Environment.NewLine, violations));
    }
}

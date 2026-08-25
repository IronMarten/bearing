using System.Text.Json;
using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// The findings export's contract — <c>SCHEMA-findings-export.md</c> §1 and §8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Firing is not being gated, and this is a contract.</b> §8 opens with that line and it is the
/// reason this file is separate from <c>JsonOutputTests</c>: those assert what the document looks
/// like, and these assert what it is not allowed to leave out. A snapshot cannot make that claim —
/// an export missing a whole kind stays byte-identical with itself forever.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class FindingsExportTests(CoreWalkFixture core)
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReadOnlyList<Judged> Judged => Analysis.Judge(core.Model);

    private JsonElement Root =>
        JsonDocument.Parse(JsonOutput.Render(core.Model, Judged, Instant)).RootElement;

    private IReadOnlyList<JsonElement> Findings =>
        [.. Root.GetProperty("findings").EnumerateArray()];

    private static string KeyOf(JsonElement finding) => finding.GetProperty("key").GetString()!;

    private static string KindOf(JsonElement finding) => finding.GetProperty("kind").GetString()!;

    /// <summary>
    /// §8.1 — the superset rule. Every judgement the tool made is in the file, and nothing else is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the test that would have caught <c>docs/DEFECTS.md</c> §47</b> — the report
    /// telling a reader that the exports carry every finding while they carried none. §47 was
    /// closed by rewording the page, because the sentence was the only thing that could be fixed
    /// at the time; this is the version that closes it by making the sentence true, and it is the
    /// invariant that stops the four-surface version of the same defect.
    /// </para>
    /// <para>
    /// <b>Asserted as set equality against <c>Analysis.Judge</c>, not as a count and not against a
    /// list.</b> §1's three clauses — every kind the report can print, every section it renders,
    /// nothing capped or filtered — are one claim from the model's side: <i>the judgements are the
    /// population, and the file is all of them</i>. A count would pass while two entries swapped
    /// identity, and a hand-maintained list of kinds is the thing that goes stale the first time a
    /// kind is added, which is exactly what §3.12 just did.
    /// </para>
    /// <para>
    /// <b>Suppressed findings are in the population deliberately.</b> They are judgements the tool
    /// made and declined to print, and §4's <c>status</c> is what distinguishes them — a consumer
    /// that cannot see them cannot tell <i>muted</i> from <i>fixed</i>, which is §7's ageing
    /// problem and the reason acknowledgment memory needs this file rather than the report.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_export_carries_every_judgement_and_nothing_else()
    {
        var judged = Judged;

        // The fixture judges something, and suppresses something, or the equality below is a
        // statement about an empty set agreeing with an empty set.
        Assert.NotEmpty(judged);
        Assert.Contains(judged, j => !j.IsReported);

        Assert.Equal(
            judged.Select(j => j.Finding.Key.Canonical).OrderBy(k => k, StringComparer.Ordinal),
            Findings.Select(KeyOf).OrderBy(k => k, StringComparer.Ordinal));

        // Set equality is not enough on its own: two entries could carry each other's keys and
        // still satisfy it. The status has to travel with the key it belongs to.
        var statusOf = Findings.ToDictionary(KeyOf, f => f.GetProperty("status").GetString()!, StringComparer.Ordinal);

        foreach (var judgement in judged)
        {
            Assert.Equal(
                judgement.IsReported ? "reported" : "suppressed",
                statusOf[judgement.Finding.Key.Canonical]);
        }
    }

    /// <summary>
    /// §8.1, the renderers' half — a kind the report names has entries in the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clause above catches the model leaking and this one catches a renderer inventing.</b>
    /// They are not the same failure. A section that prints a claim which never became a
    /// <see cref="Finding"/> is invisible to the equality test — the judgements and the file would
    /// agree perfectly and the page would still say something the export cannot. **That is not
    /// hypothetical: the circular-reference sections did exactly this until `TECHREQ-job-b.md`
    /// §3.12**, rendering three kinds of claim from a parallel model with no finding behind any of
    /// them.
    /// </para>
    /// <para>
    /// <b>Derived from <see cref="Claims.KindName"/> rather than from a list of section headings.</b>
    /// The name a kind renders under is the tool's own, so a kind added without wording is caught by
    /// the existing sentence tests, and a kind worded without exporting is caught here. Scanned
    /// case-insensitively across both renderers because the terminal shouts its headings and the
    /// HTML does not, and singular-in-plural is why it is <c>Contains</c> rather than equality —
    /// <i>Namespace cycle</i> inside <i>NAMESPACE CYCLES</i>.
    /// </para>
    /// <para>
    /// <b>What this cannot catch, said here rather than discovered later:</b> a section that renders
    /// a claim without ever naming its kind. Nothing derives that, and a registry of what each
    /// renderer prints would be the stale list this file is written to avoid.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_kind_the_report_names_has_entries_in_the_export()
    {
        var findings = Analysis.FindingsFor(core.Model);
        var rendered = string.Join(
            "\n",
            string.Join("\n", Report.For(core.Model, findings)),
            HtmlReport.Render(core.Model, findings, Instant, full: true));

        var exported = Findings.Select(KindOf).ToHashSet(StringComparer.Ordinal);

        var judged = Judged;

        // Two populations, and getting them the wrong way round is what the first two attempts at
        // this test did.
        //
        // EXPORTED is keyed on what FIRED, suppressed rows included -- that is §1's superset.
        // NAMED is keyed on what SURVIVED, because a suppressed claim is one the tool decided not
        // to print and a renderer naming it anyway would be the defect, not the fix.
        //
        // The two mistakes, kept because each is a real distinction this file has to hold:
        //   - `PROJECT CYCLES` renders its heading over an explicit empty state on this fixture.
        //     **A heading is not a claim**, so scanning "every kind the report names must be
        //     exported" reports the report being honest about having nothing to say as a gap.
        //   - `WidestContractSurface` FIRES here and is suppressed as a set by
        //     widest-surface-is-not-discriminating, so nothing prints it. **Fired is not
        //     reported**, and requiring every fired kind to be named asks the renderers to
        //     undo the suppression matrix.
        var fired = judged.Select(j => j.Finding.Kind).Distinct().ToList();
        var reported = judged.Where(j => j.IsReported).Select(j => j.Finding.Kind).Distinct().ToList();

        Assert.NotEmpty(fired);
        Assert.NotEmpty(reported);

        // The two differ on this fixture, or the distinction above is asserted by a test that
        // could not tell whether it held.
        Assert.NotEqual(fired.Count, reported.Count);

        foreach (var kind in fired)
            Assert.True(
                exported.Contains(kind.ToString()),
                $"{Claims.KindName(kind)} fired and has no entry in the export.");

        foreach (var kind in reported)
            Assert.True(
                rendered.Contains(Claims.KindName(kind), StringComparison.OrdinalIgnoreCase),
                $"{Claims.KindName(kind)} survived suppression and neither renderer names it.");
    }

    /// <summary>
    /// A subject carries members when it is a set and a declaring type when it is a member, and
    /// never the other way round.
    /// </summary>
    /// <remarks>
    /// <b>The join a consumer makes is on these two.</b> An empty <c>members</c> array on a type
    /// would read as <i>a set of nothing</i> rather than <i>not a set</i>, and a missing one on a
    /// cycle would lose the only place the component's parts are written down — the canonical
    /// escapes its separators and is not something to parse. Asserted as a correspondence in both
    /// directions so neither can drift into the other.
    /// </remarks>
    [Fact]
    public void A_subject_carries_members_exactly_when_it_is_a_set()
    {
        var sets = 0;
        var members = 0;

        foreach (var finding in Findings)
        {
            var subject = finding.GetProperty("subject");
            var kind = subject.GetProperty("kind").GetString();

            var hasMembers = subject.GetProperty("members").ValueKind is not JsonValueKind.Null;
            var hasDeclaring = subject.GetProperty("declaringType").ValueKind is not JsonValueKind.Null;

            Assert.Equal(kind == "Set", hasMembers);
            Assert.Equal(kind == "Member", hasDeclaring);

            if (hasMembers)
            {
                sets++;
                Assert.NotEmpty(subject.GetProperty("members").EnumerateArray());
            }

            if (hasDeclaring) members++;
        }

        // Both shapes are present, or the correspondence above is asserted over one kind of row.
        Assert.NotEqual(0, sets);
        Assert.NotEqual(0, members);
    }
}

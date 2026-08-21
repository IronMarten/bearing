using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// An absolute gate says what share it took and that the share does not travel —
/// <c>docs/DEFECTS.md</c> §2's last outstanding half, and decision X13's "say why".
/// </summary>
/// <remarks>
/// <para>
/// <b>The measurement is real and reproduces</b>: <c>HubMin = 5</c> selects 3.6% of nopCommerce
/// and 6.9% of Jellyfin. One threshold, two codebases, nearly double the share. X13 kept both
/// gates absolute rather than converting them, because a rank gate cannot report that one codebase
/// is more coupled than another — every codebase has a top 5% — and required them to disclose
/// instead.
/// </para>
/// <para>
/// <b>Held on the set of kinds as well as on the text.</b> A kind added to the absolute list
/// without an absolute gate, or a gate converted without leaving the list, would leave the report
/// saying something false about how to read it.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class AbsoluteGateTests(CoreWalkFixture core)
{
    private FindingSet Findings => Analysis.FindingsFor(core.Model);

    private string Text =>
        string.Join("\n", IronMarten.Bearing.Cli.Report.For(core.Model, Findings));

    /// <summary>
    /// The report with its line breaks and indentation collapsed to single spaces.
    /// </summary>
    /// <remarks>
    /// The caveat is wrapped to the section's width, so a phrase in it straddles a line break —
    /// which is a fact about layout, not about what the report says. Asserting on the wrapped form
    /// would pin the column count as well as the sentence.
    /// </remarks>
    private string Flowed =>
        string.Join(' ', Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Exactly the two kinds X13 named, transcribed rather than derived.</summary>
    /// <remarks>
    /// Transcribed on purpose, which is <c>FrameworkNamespacesTests</c>' argument: the list decides
    /// what the report says about itself, so it fails if anyone trims or extends it, and there is
    /// no other way to notice.
    /// </remarks>
    [Fact]
    public void The_absolute_gates_are_the_two_X13_named()
    {
        var absolute = Enum.GetValues<FindingKind>().Where(Claims.GateIsAbsolute).ToList();

        Assert.Equal([FindingKind.BreaksAlone, FindingKind.HubOrGodObject], absolute.Order());
    }

    /// <summary>Both renderers carry the disclosure, and it names this run's share.</summary>
    [Fact]
    public void The_terminal_discloses_the_share_an_absolute_gate_took()
    {
        var hubs = Findings.OfKind(FindingKind.HubOrGodObject);
        Assert.NotEmpty(hubs);

        Assert.Contains($"{hubs.Count} types of {core.Model.Types.Count}", Flowed, StringComparison.Ordinal);
        Assert.Contains("fixed count rather than a share", Flowed, StringComparison.Ordinal);
    }

    /// <summary>And a comparative gate does not, because there the share is the gate.</summary>
    /// <remarks>
    /// The half that stops the disclosure becoming decoration. Change cost and blast radius are
    /// top-fraction gates, so their share is fixed by construction and saying it varies would be
    /// false.
    /// </remarks>
    [Fact]
    public void A_comparative_gate_says_nothing_of_the_kind()
    {
        Assert.False(Claims.GateIsAbsolute(FindingKind.ChangeCost));
        Assert.False(Claims.GateIsAbsolute(FindingKind.BugBlastRadius));

        var changeCost = Findings.OfKind(FindingKind.ChangeCost);
        Assert.NotEmpty(changeCost);
        Assert.DoesNotContain($"{changeCost.Count} types of {core.Model.Types.Count}", Flowed, StringComparison.Ordinal);
    }

    /// <summary>The share is the one the gate actually took, not a rounded impression of it.</summary>
    [Fact]
    public void The_share_is_computed_from_the_run()
    {
        Assert.Equal("2 types of 8 — 25%. This threshold is a fixed count rather than a share, "
                     + "so the percentage differs between codebases: compare what is named, not how many.",
            Claims.ShareCaveat(2, 8));
    }
}

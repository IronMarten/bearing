namespace TestBed.Core.Weighing;

using TestBed.Core.Tariffs;

/// <summary>
/// Half of plant P10 — the fixture's first <c>CycleShape.Coupling</c> namespace cycle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Every namespace cycle in the fixture was a <c>FolderLayout</c>
/// — <c>TestBed.Core</c> and its own subfolders — so <c>ShapedCycle.IsReportable</c> was false for
/// all of them and the reportable branch was unreachable. That left both renderers' cycle output
/// ungated, which is how <c>docs/DEFECTS.md</c> §46 shipped: the HTML dropped the held-pair
/// evidence and the suite stayed green, because nothing here could produce a cycle to render.
/// </para>
/// <para>
/// <b>What makes it Coupling rather than the other two shapes.</b> The discriminator is a
/// <i>sibling</i> pair of namespaces that <i>hold each other as state</i>, where held means a field
/// whose type is abstract or an interface. <c>Weighing</c> and <c>Tariffs</c> are siblings under
/// <c>TestBed.Core</c> — neither contains the other — and each holds the other's abstraction. Take
/// away either field and the pair still cycles by naming, which reads as <c>SharedTypes</c>; that
/// is the mutation this plant exists to make available.
/// </para>
/// <para>
/// <b>It references nothing that already existed</b>, per the plant constraint in
/// <c>docs/TESTING.md</c> — no new fan-in on any existing type — and the trailing words
/// <c>Head</c>, <c>Window</c>, <c>Scale</c> and <c>Tariff</c> were each checked against the fixture
/// and appear nowhere, so no suffix cohort changes size.
/// </para>
/// </remarks>
public interface IScaleHead
{
    /// <summary>The weight this head last settled on, in whole kilograms.</summary>
    int SettledKilograms { get; }
}

/// <summary>
/// Reads a weight and prices it through the tariff side, which holds this one back.
/// </summary>
public sealed class ScaleHead : IScaleHead
{
    // The held reference. A field, and its type is an interface, which is what
    // CycleShape.IsHeld tests for. Weighing -> Tariffs.
    private readonly ITariffWindow _window;

    private int _settled;

    /// <summary>Creates the head over the tariff window it prices against.</summary>
    public ScaleHead(ITariffWindow window) => _window = window;

    /// <inheritdoc/>
    public int SettledKilograms => _settled;

    /// <summary>Settles on a weight and returns what it costs under the current window.</summary>
    public decimal Settle(int kilograms)
    {
        _settled = kilograms < 0 ? 0 : kilograms;
        return _window.RateFor(_settled);
    }
}

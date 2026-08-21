using System;

namespace TestBed.Core;

/// <summary>
/// The eight member shapes whose identity the model got wrong, and which this fixture did not
/// contain.
/// </summary>
/// <remarks>
/// <para>
/// <b>X14 was unobservable before this file existed.</b> The suite was byte-identical with the
/// documentation-comment-ID identity and with the display string it replaced, because TestBed
/// declared no event, no static constructor, no <c>out</c> or <c>ref</c> parameter, no explicit
/// interface implementation, no multi-declarator field and no public field. Every one of those is
/// ordinary C# and four of them were measured collapsing on a real solution — all 81 of Jellyfin's
/// events into 15 subjects, two real <c>NormalizePath</c> overloads into one, both of
/// <c>WebAppTypeFinder</c>'s constructors into one. A gate that cannot fail is worse than no gate,
/// which is <c>docs/TESTING.md</c> §9, and this is the plant that lets it fail.
/// </para>
/// <para>
/// <b>Two more arrived with A9's member graph, and both were found by measuring rather than by
/// reading.</b> An extension method called as one resolves to the <i>reduced</i> symbol, whose
/// signature has had the receiver removed — so on Jellyfin <c>AddClientFields</c>, called from a
/// dozen controllers, read as having no callers at all, and every extension method in both
/// reference solutions was a dead-code candidate. And a partial method is two declarations of one
/// member, so recording both put two rows under one subject: six of those on nopCommerce, all
/// generated. Neither shape existed here.
/// </para>
/// <para>
/// <b>It takes nothing from the rest of the fixture and gives nothing to it.</b> The one
/// constraint on every plant is no new fan-in on anything that already exists: this closes over
/// its own interface and <c>System.Action</c>, which four fixture types already carry, so no
/// existing cohort, contact point or external namespace moves. <c>Turnstile</c> and <c>Wicket</c>
/// are new trailing words, checked against the fixture's sixty-one, so no suffix cohort gains a
/// member either.
/// </para>
/// <para>
/// <b>Deliberately not a dead-code trap.</b> A9's member-level traps are a different list and are
/// owed separately; every member here is reachable, so nothing in this file will be nominated for
/// deletion and nothing here has to be reasoned about twice.
/// </para>
/// </remarks>
internal interface IIdentityWicket
{
    /// <summary>Implemented twice by <see cref="IdentityTurnstile"/>, on purpose.</summary>
    void Admit(string token);
}

/// <summary>
/// One type carrying all six shapes, so the plant costs the fixture two types rather than six.
/// </summary>
internal sealed partial class IdentityTurnstile : IIdentityWicket
{
    /// <summary>
    /// Two fields on one line. Before X14 this was <b>one</b> member, named <c>_opened</c>.
    /// </summary>
    private int _opened, _refused;

    /// <summary>A public field: contract surface the declaring type used not to be charged for.</summary>
    public readonly string Lane;

    /// <summary>And a public constant, which is the same question with no storage behind it.</summary>
    public const int MaxAdmissions = 32;

    private Action<string>? _turned;

    /// <summary>Three event fields in one type. All three used to be named <c>EventFieldDeclaration</c>.</summary>
    public event Action<string>? Admitted;

    /// <summary>The second of the three.</summary>
    public event Action<string>? Refused;

    /// <summary>The third, and the one that proves the first two were not merged into it.</summary>
    public event Action? Closed;

    /// <summary>
    /// The other event syntax — accessors rather than a field — which is a different syntax node
    /// for the same kind of member.
    /// </summary>
    public event Action<string>? Turned
    {
        add => _turned += value;
        remove => _turned -= value;
    }

    /// <summary>
    /// A static constructor beside an instance one. Both used to render as
    /// <c>IdentityTurnstile.IdentityTurnstile()</c>.
    /// </summary>
    static IdentityTurnstile() => Opened = 0;

    /// <summary>The instance constructor, which is the half that was being merged into.</summary>
    public IdentityTurnstile(string lane) => Lane = lane;

    /// <summary>
    /// Seeded by the static constructor and never written again.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately not incremented, and that is a constraint on the plant rather than an
    /// oversight.</b> A write to static mutable state outside a static constructor is a finding,
    /// and a plant that produces one changes which claim the report leads with — it displaced
    /// <c>QuoteAssembler</c> from the top of `START HERE` when this line read <c>Opened++</c>.
    /// A plant must disturb nothing it did not aim at, and this file aims at member identity.
    /// </remarks>
    public static int Opened { get; private set; }

    /// <summary>Refusals so far, on this instance.</summary>
    public int Refusals => _refused;

    /// <summary>Admissions so far, on this instance.</summary>
    public int Admissions => _opened;

    /// <summary>
    /// The <c>out</c> half of an overload pair that differs only by a parameter modifier.
    /// </summary>
    public bool TryAdmit(string token, out string reason)
    {
        if (string.IsNullOrEmpty(token))
        {
            reason = "no token";
            _refused++;
            Refused?.Invoke(token);
            return false;
        }

        reason = "";
        _opened++;
        Admitted?.Invoke(token);
        return true;
    }

    /// <summary>
    /// The by-value half. Identical to the pair above under a display format that drops
    /// <c>out</c>, and a different member to the compiler and to every caller.
    /// </summary>
    public bool TryAdmit(string token, string reason)
    {
        _turned?.Invoke(reason);
        return TryAdmit(token, out _);
    }

    /// <summary>The ordinary member, which also implements the interface implicitly.</summary>
    public void Admit(string token)
    {
        TryAdmit(token, out _);

        // Called in REDUCED form — the receiver is implicit — which is the form whose symbol does
        // not carry the declaration's signature. Written as `this.IsWired()` rather than
        // `TurnstileExtensions.IsWired(this)` on purpose: the unreduced call joins by itself and
        // proves nothing.
        if (this.IsWired()) OnRefused(token);
    }

    /// <summary>
    /// The definition half of a partial method. Its implementation is the second part of this
    /// class, below, and the two are one member with one identity.
    /// </summary>
    partial void OnRefused(string token);

    /// <summary>
    /// And the explicit implementation beside it, which used to render as
    /// <c>IdentityTurnstile.Admit(string)</c> — the ordinary member's own signature.
    /// </summary>
    void IIdentityWicket.Admit(string token)
    {
        Admit(token);
        Closed?.Invoke();
    }
}

/// <summary>The implementation half of the partial method, in a second part of the same class.</summary>
internal sealed partial class IdentityTurnstile
{
    partial void OnRefused(string token)
    {
        if (token.Length > 0) _refused++;
    }
}

/// <summary>
/// One extension method, so that the reduced-symbol case has somewhere to live.
/// </summary>
/// <remarks>
/// <para>
/// A static class of its own is what an extension method costs; there is no way to write one
/// without it.
/// </para>
/// <para>
/// <b>It extends the interface rather than the class, and that is the plant constraint rather than
/// a preference.</b> Extending <see cref="IdentityTurnstile"/> directly made the two types name
/// each other — the extension takes one as a parameter, the class calls the extension — and a
/// two-type tangle is a <i>finding</i>. The fixture gained one, which is a plant disturbing
/// something it did not aim at. Through <see cref="IIdentityWicket"/> the edges run one way.
/// </para>
/// </remarks>
internal static class TurnstileExtensions
{
    /// <summary>Called from <see cref="IdentityTurnstile.Admit"/>, in reduced form.</summary>
    internal static bool IsWired(this IIdentityWicket wicket) => wicket.ToString() is { Length: > 0 };
}

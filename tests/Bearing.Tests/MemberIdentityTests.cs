using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// A member's identity — decision X14, and <c>docs/DEFECTS.md</c> §39 is what it replaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every assertion here fails against the code as it shipped before X14</b>, which is the only
/// property that makes them worth having. They read <c>TestBed.Core.IdentityTurnstile</c>, planted
/// for this and carrying all six shapes the fixture previously lacked — see
/// <c>tests/TestBed/Core/MemberIdentityTraps.cs</c> for why none of them existed by accident.
/// </para>
/// <para>
/// <b>Uniqueness is asserted over the whole solution and the six cases are asserted one by one.</b>
/// The blanket assertion is what catches the seventh shape nobody has thought of; the named ones
/// are what say which claim broke when it does. <c>CsvOutputTests</c> already asserts a member id
/// is not a bare name — it passed over every one of these, because the fixture could not reach
/// them.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class MemberIdentityTests(CoreWalkFixture core)
{
    private const string Turnstile = "IdentityTurnstile";

    /// <summary>
    /// No two members in the solution share a subject.
    /// </summary>
    /// <remarks>
    /// The general form, and the reason it is worth stating even though the six below are
    /// specific: A9 attaches a claim about deletion to a member subject, and a subject shared by
    /// two members turns "no static references found" into a statement about a member that has
    /// callers. That is invariant 4, and it does not care which of the six shapes caused it.
    /// </remarks>
    [Fact]
    public void No_two_members_share_a_subject()
    {
        var duplicates = core.Model.Types
            .SelectMany(t => t.Members)
            .GroupBy(m => m.Subject.Canonical, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>Every member knows its own accessibility, fields and events included.</summary>
    /// <remarks>
    /// 279 of the fixture's members reported none before X14 — every field it declares — because
    /// <c>GetDeclaredSymbol</c> answers <see langword="null"/> for a field declaration and the
    /// walk took that answer. On nopCommerce the same silence was 18.4% of members.
    /// </remarks>
    [Fact]
    public void Every_member_has_an_accessibility()
    {
        var silent = core.Model.Types
            .SelectMany(t => t.Members)
            .Where(m => string.IsNullOrEmpty(m.Accessibility))
            .Select(m => m.Subject.Canonical)
            .ToList();

        Assert.Empty(silent);
    }

    /// <summary>Two fields declared on one line are two members.</summary>
    [Fact]
    public void A_field_declaration_yields_one_member_per_variable()
    {
        var names = MembersOf(Turnstile).Where(m => m.Kind == MemberKind.Field).Select(m => m.Name).ToList();

        Assert.Contains("_opened", names);
        Assert.Contains("_refused", names);
    }

    /// <summary>Three events in one type are three members with three names.</summary>
    /// <remarks>
    /// The shape that collapsed hardest on a real solution: <c>MemberName</c> had no arm for
    /// <c>EventFieldDeclarationSyntax</c>, so every event fell through to the syntax kind's own
    /// name and all of a type's events became one subject. Jellyfin: 81 events, 15 subjects.
    /// </remarks>
    [Fact]
    public void Each_event_is_its_own_member()
    {
        var events = MembersOf(Turnstile).Where(m => m.Kind == MemberKind.Event).ToList();

        Assert.Equal(
            ["Admitted", "Closed", "Refused", "Turned"],
            events.Select(m => m.Name).Order(StringComparer.Ordinal));

        // The name alone would pass over a walk that gave all four the same subject.
        Assert.Equal(4, events.Select(m => m.Subject.Canonical).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(events, m => m.Name.Contains("Declaration", StringComparison.Ordinal));
    }

    /// <summary>Overloads differing only by a parameter modifier are different members.</summary>
    /// <remarks>
    /// Real: <c>Emby.Server.Implementations.Library.PathExtensions</c> declares
    /// <c>NormalizePath(string, out char)</c> and <c>NormalizePath(string, char)</c>, and they
    /// shared one subject. <see cref="Member.Signature"/> still cannot tell them apart, which is
    /// why it is published as readable rather than as a key — asserted here so that is on the
    /// record rather than assumed.
    /// </remarks>
    [Fact]
    public void An_out_parameter_is_part_of_the_signature()
    {
        var overloads = MembersOf(Turnstile).Where(m => m.Name == "TryAdmit").ToList();

        Assert.Equal(2, overloads.Count);
        Assert.Equal(2, overloads.Select(m => m.Subject.Canonical).Distinct(StringComparer.Ordinal).Count());
        Assert.Single(overloads.Select(m => m.Signature).Distinct(StringComparer.Ordinal));
    }

    /// <summary>A static constructor is not the instance one.</summary>
    /// <remarks>
    /// Real: <c>Nop.Core.Infrastructure.WebAppTypeFinder</c> declares both, and both rendered as
    /// <c>WebAppTypeFinder.WebAppTypeFinder()</c>.
    /// </remarks>
    [Fact]
    public void A_static_constructor_is_a_different_member_from_the_instance_one()
    {
        var constructors = MembersOf(Turnstile).Where(m => m.Kind == MemberKind.Constructor).ToList();

        Assert.Equal(2, constructors.Count);
        Assert.Equal(2, constructors.Select(m => m.Subject.Canonical).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([".cctor", ".ctor"], constructors.Select(m => m.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>An explicit interface implementation is not the ordinary member of the same name.</summary>
    /// <remarks>
    /// Real: <c>MediaBrowser.Common.Plugins.BasePlugin&lt;TConfigurationType&gt;.Configuration</c>.
    /// The display format renders an explicit implementation under its <i>containing</i> type, so
    /// it came out identical to the ordinary member it sits beside.
    /// </remarks>
    [Fact]
    public void An_explicit_implementation_is_a_different_member()
    {
        var admits = MembersOf(Turnstile)
            .Where(m => m.Name == "Admit" || m.Name.EndsWith(".Admit", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, admits.Count);
        Assert.Equal(2, admits.Select(m => m.Subject.Canonical).Distinct(StringComparer.Ordinal).Count());

        // The interface qualifier survives into the name as well as into the identity, which is
        // what lets a report say which of the two it is talking about.
        Assert.Contains(admits, m => m.Name.Contains("IIdentityWicket", StringComparison.Ordinal));
    }

    /// <summary>A public field widens the declaring type's contract surface, not only its readers'.</summary>
    /// <remarks>
    /// <c>ShapeBreadth</c> has always counted a public field when measuring somebody else's type,
    /// so the model held both answers at once: a public field was contract surface when depended
    /// on and not when declared. No fixture type had one, so nothing said so.
    /// </remarks>
    [Fact]
    public void A_public_field_is_contract_surface()
    {
        var turnstile = core.Model.Types.Single(t => t.Name == Turnstile);
        var publicFields = turnstile.Members
            .Count(m => m.Kind == MemberKind.Field && m.Accessibility == "Public");

        Assert.Equal(2, publicFields);                       // Lane, and the const MaxAdmissions
        Assert.True(turnstile.PublicMemberCount >= publicFields,
            $"{turnstile.PublicMemberCount} public members for {publicFields} public fields");
        Assert.True(turnstile.ParameterCount > 0, "no contract surface counted at all");
    }

    private List<Member> MembersOf(string type) =>
        core.Model.Types.Single(t => t.Name == type).Members;
}

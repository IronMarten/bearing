namespace IronMarten.Bearing;

/// <summary>
/// What the dead-code pass declined to consider, and why.
/// </summary>
/// <param name="RuntimeInvoked">
/// Members the runtime calls and no syntax can: entry points, and static constructors.
/// </param>
/// <param name="InterfaceImplementations">Members that exist because an interface asked for them.</param>
/// <param name="Overrides">Members that override a base member.</param>
/// <param name="ExternallyVisible">Members reachable from outside the assembly that declares them.</param>
/// <param name="SoleConstructors">
/// Constructors that are the only accessible one their type declares — whatever creates the type
/// calls this, and a container never names it.
/// </param>
/// <param name="Considered">Members with no inbound reference, before any exclusion.</param>
/// <remarks>
/// <para>
/// <b>Counted rather than silent, and that is invariant 8 rather than a nicety.</b> The exclusions
/// remove 98–99% of what a naive pass would report — 3,837 members down to 48 on Jellyfin and
/// 7,902 down to 37 on nopCommerce — and a reader who is shown 48 findings without being told that
/// 3,789 were set aside has been shown a number they cannot judge. The counts are what makes the
/// short list honest rather than merely short.
/// </para>
/// <para>
/// <b>The categories overlap and are counted independently.</b> A public override that implements
/// an interface member is all three, so these do not sum to <see cref="Excluded"/> — which is why
/// that is measured rather than added up.
/// </para>
/// </remarks>
public sealed record DeadCodeExclusions(
    int RuntimeInvoked,
    int InterfaceImplementations,
    int Overrides,
    int ExternallyVisible,
    int SoleConstructors,
    int Considered)
{
    /// <summary>How many of the considered members an exclusion removed.</summary>
    public int Excluded { get; init; }

    /// <summary>How many survived every exclusion and were nominated.</summary>
    public int Nominated => Considered - Excluded;
}

/// <summary>
/// Members with no inbound reference — <c>TECHREQ-job-a.md</c> §5.6, at the member level X5 chose.
/// </summary>
/// <remarks>
/// <para>
/// <b>The word "dead" appears nowhere, by construction rather than by review.</b> §5.6 is explicit:
/// the label is <i>"no static references found — verify before deleting"</i>, and invariant 4 is
/// why — a tool that says "safe to remove" about something six customers depend on has caused the
/// burn it claimed to prevent. This kind is named for what was measured and not for what it might
/// mean.
/// </para>
/// <para>
/// <b>The four exclusions, and why they are exclusions rather than caveats.</b> An entry point and
/// a static constructor are both invoked by the runtime, and neither has any syntax that could
/// call it — a static constructor's inbound count is zero in every codebase that compiles, which
/// makes reporting one a false positive by construction. A member that implements an interface or
/// overrides a base member exists because a contract demands it, and its callers reach it through
/// the contract. And an externally visible member cannot be judged from this solution at all —
/// whoever might be calling it is not in the workspace. None of those is a member somebody should
/// be asked to look at.
/// </para>
/// <para>
/// <b>Visibility alone decides the fourth, with no test for whether the project is a library</b> —
/// decision X15, taken on the measurement. §5.6 says <i>"the public surface of library projects"</i>,
/// and the qualifier turns out to be the only thing making the rule non-portable: it leaves 0.5% of
/// Jellyfin's members and 10.5% of nopCommerce's, a factor of twenty, decided by nopCommerce
/// hosting application code in a project that compiles to an exe. Packaging is not a property of
/// the code, and the reason for the exclusion — somebody outside this solution may be calling it —
/// holds for an exe as much as for a library, because model binding, plugin loading and reflection
/// all reach into one.
/// </para>
/// <para>
/// <b>What is left carries qualifiers rather than certainty.</b> §5.6's remaining categories are
/// things this analysis cannot see, not things it has ruled out, and a finding says so on its own
/// face — see <see cref="Qualifiers.TestUsageUnobservable"/> and
/// <see cref="Qualifiers.ContainerMayResolve"/>.
/// </para>
/// </remarks>
public static class NoStaticReferences
{
    /// <summary>Every member with no inbound reference that no exclusion removed.</summary>
    public static IEnumerable<Finding> Detect(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Certain rather than suspected: a test project was skipped, so usage from it was never
        // visible to this walk. Solution-level, and it qualifies every nomination equally.
        var testsUnobservable = model.Coverage.SkippedProjects.Count > 0;

        var nominated = Considered(model).Where(x => Excluding(x.Type, x.Member) is null).ToList();

        // How many data members of each type survived. A member cannot know this about itself, so
        // the pass is over the whole candidate set — which is why Detect materialises rather than
        // streaming. See Qualifiers.PartOfAnUnreadGroup for what the count decides.
        var unreadPerType = nominated
            .Where(x => IsData(x.Member))
            .GroupBy(x => x.Type.Subject.Canonical, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var (type, member) in nominated)
        {
            var group = IsData(member)
                        && unreadPerType.TryGetValue(type.Subject.Canonical, out var siblings)
                        && siblings > 1;

            yield return new Finding(
                new FindingKey(FindingKind.NoStaticReferences, member.Subject),
                [
                    Receipt.Of("InboundReferences", 0),
                    // Whether anything names the declaring type is the difference between "this
                    // type is used and this member of it is not" and "nothing here is reached at
                    // all", and those are different claims with different remedies.
                    Receipt.Of("DeclaringTypeInboundReferences", type.InboundReferenceCount),
                    Receipt.Of("DeclaringTypeMembers", type.Members.Count),
                    // The denominator of the collapsed row: "17 of its 23 data members". Carried
                    // rather than recomputed by the renderer, so the sentence and the grouping
                    // read the same number.
                    Receipt.Of("DeclaringTypeDataMembers", type.Members.Count(IsData)),
                    Receipt.Of("UnreadDataMembersInType", unreadPerType.GetValueOrDefault(type.Subject.Canonical)),
                ],
                [
                    new Qualifier(Qualifiers.TestUsageUnobservable, testsUnobservable),
                    new Qualifier(Qualifiers.PartOfAnUnreadGroup, group),
                    new Qualifier(Qualifiers.AnAttributeMayDirectIt, member.HasAttributes),
                ],
                []);
        }
    }

    /// <summary>Whether a member carries data rather than behaviour.</summary>
    /// <remarks>
    /// Properties and fields, which is <c>ShapeBreadth</c>'s definition of breadth and the same
    /// population the contract surface counts. A method nothing calls is one claim however many
    /// of its neighbours share the problem; a carrier nothing reads is one claim about the carrier.
    /// </remarks>
    private static bool IsData(Member member) => member.Kind is MemberKind.Property or MemberKind.Field;

    /// <summary>What the exclusions removed, for the disclosure that ships beside the findings.</summary>
    public static DeadCodeExclusions Excluded(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var considered = Considered(model).ToList();

        return new DeadCodeExclusions(
            considered.Count(x => RuntimeInvoked(x.Member)),
            considered.Count(x => x.Member.ImplementsInterface),
            considered.Count(x => x.Member.IsOverride),
            considered.Count(x => x.Member.IsExternallyVisible),
            considered.Count(x => SoleConstructor(x.Type, x.Member)),
            considered.Count)
        {
            Excluded = considered.Count(x => Excluding(x.Type, x.Member) is not null),
        };
    }

    /// <summary>The population the exclusions are applied to: everything nothing references.</summary>
    private static IEnumerable<(TypeNode Type, Member Member)> Considered(SolutionModel model) =>
        model.Types.SelectMany(type => type.Members
            .Where(member => member.InboundReferenceCount == 0)
            .Select(member => (Type: type, Member: member)));

    /// <summary>
    /// Which exclusion removes this member, or <see langword="null"/> if none does.
    /// </summary>
    /// <remarks>
    /// One function, read by both the detector and the disclosure, so the count and the list cannot
    /// disagree about what was excluded — which is the failure that makes a disclosure worse than
    /// none.
    /// </remarks>
    private static string? Excluding(TypeNode type, Member member) => member switch
    {
        _ when RuntimeInvoked(member) => "invoked by the runtime",
        { ImplementsInterface: true } => "implements an interface",
        { IsOverride: true } => "overrides a base member",
        { IsExternallyVisible: true } => "externally visible",
        _ when SoleConstructor(type, member) => "the type's only accessible constructor",
        _ => null,
    };

    /// <summary>
    /// Members the runtime calls, which no source can.
    /// </summary>
    /// <remarks>
    /// <b>The static-constructor half was measured rather than reasoned to.</b> Eight of
    /// nopCommerce's first fifteen nominations were static constructors before this existed — and
    /// they are not a hard case or a heuristic, they are a category that cannot ever be referred
    /// to, because C# has no syntax for calling one. A finding that fires on every instance of a
    /// language feature is invariant 1's cry-wolf failure in its purest form.
    /// </remarks>
    private static bool RuntimeInvoked(Member member) =>
        member.IsEntryPoint || (member.Kind == MemberKind.Constructor && member.IsStatic);

    /// <summary>
    /// Whether this is the only constructor anything outside the type could call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The DI signature, and it is an exclusion rather than a caveat because the pattern is
    /// systematic.</b> A type with one accessible constructor and no caller for it is what
    /// registration looks like from inside the solution: <c>AddSingleton&lt;T&gt;()</c> names
    /// <c>T</c> and calls none of its constructors, and convention scanning names nothing at all.
    /// On nopCommerce this is <b>24 of 29 nominations</b> — a section that is five-sixths one
    /// systematic pattern is invariant 1's cry-wolf failure however carefully each row is worded.
    /// </para>
    /// <para>
    /// <b>Only when it is the sole accessible constructor, and that is the whole precision of the
    /// rule.</b> A container picks one constructor, so a type offering several has siblings that
    /// nothing calls and nothing was ever going to call — those are real dead overloads and they
    /// stay nominated. nopCommerce has one, Jellyfin none, which is what makes the narrower rule
    /// worth having rather than "exclude constructors".
    /// </para>
    /// <para>
    /// <b>It does not ask whether the type is referenced, and requiring that was the earlier
    /// mistake.</b> A container reaches a type by naming it or by scanning for it; the second
    /// leaves no reference at all, which is exactly the <c>AuditPolicySink</c> case §5.6 calls
    /// out. Requiring an inbound reference made the rule miss the shape it exists for, and left
    /// nopCommerce's <c>EventConsumer</c> nominated with no category named at all.
    /// </para>
    /// <para>
    /// <b>A private constructor is not one of these</b>, and treating it as one was a real misfire:
    /// a container can only call a constructor it can reach, so it shipped on nopCommerce's
    /// <c>RoxyFilemanException</c>, which is the factory-method pattern and not injection. A type
    /// whose only constructor is private has no accessible one, so nothing here is excluded and
    /// that constructor stays nominated.
    /// </para>
    /// <para>
    /// It is deliberately not a test for whether the solution uses a container, because that would
    /// be a curated list of registration APIs, and <c>docs/DEFECTS.md</c> §5 is the standing
    /// example of what one of those costs.
    /// </para>
    /// </remarks>
    private static bool SoleConstructor(TypeNode type, Member member) =>
        Constructible(member) && type.Members.Count(Constructible) == 1;

    /// <summary>A constructor something outside the type could call.</summary>
    private static bool Constructible(Member member) =>
        member is { Kind: MemberKind.Constructor, IsStatic: false }
        && !string.Equals(member.Accessibility, "Private", StringComparison.Ordinal);
}

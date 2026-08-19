namespace IronMarten.Bearing.Cli;

/// <summary>
/// One finding, worded — the subject, the claim it makes, and the numbers behind it.
/// </summary>
/// <param name="Subject">
/// What the claim is about, named the way a reader would name it. Carries the bracketed
/// architectural kind where the section prints one, because that is part of naming the subject
/// rather than part of the claim.
/// </param>
/// <param name="Sentence">
/// <b>The claim, and this is the thing tier 2 exists to show.</b> Punctuated as it will be read:
/// most end in a full stop, a couple deliberately do not, and where the original text ran a
/// receipt into the middle of the sentence it stays there — the number that made the claim is
/// often the claim.
/// </param>
/// <param name="Evidence">
/// The measurements behind it, unbracketed. The terminal parenthesises this; the page gives it a
/// line of its own.
/// </param>
/// <param name="Trailer">A location, where the section prints one, or empty.</param>
public readonly record struct Claim(string Subject, string Sentence, string Evidence, string Trailer)
{
    /// <summary>No claim could be worded — the subject is not a type this model holds.</summary>
    public static Claim None { get; } = new("", "", "", "");

    /// <summary>Whether there is anything to render.</summary>
    public bool Exists => Subject.Length > 0;
}

/// <summary>
/// What each finding says, in one place, for every renderer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public for the reason <see cref="Html"/> is public, and no further.</b> This is the layer
/// whose failure is silent — a sentence that drifts between two renderers is not a crash and not a
/// wrong number — so it is worth asserting directly rather than only through a rendered page.
/// <c>Bearing.Cli</c> packs as a tool and not as a library, so nothing about its surface is a
/// contract; see its csproj.
/// </para>
/// <para>
/// <b>This is the work <see cref="HtmlReport"/> recorded as owed and named the next renderer to do
/// it.</b> A finding carries its kind as an enum and nothing a reader can be shown, so every
/// renderer needs this vocabulary — and until now there were two copies of it, the terminal's
/// per-section sentences and the page's per-kind blurbs, saying different things about the same
/// claim. A13 tier 2 needs a third reading of it, which is the point at which two copies stops
/// being a tolerable duplication and starts being the thing that guarantees drift.
/// </para>
/// <para>
/// <b>The sentences are the terminal's, unchanged except where they were wrong.</b> They are what
/// X10 means by <i>"the findings are risk claims and tier 2 says so"</i> — <i>"looks like plumbing
/// but is 37x the median internal complexity of the 96 types deriving from
/// BaseNopValidator&lt;TModel&gt;"</i> was already shipping, and tier 2's job was to put it
/// somewhere a reader meets it before 1,642 other rows, not to write it again.
/// </para>
/// <para>
/// <b>Still presentation, and still deciding nothing.</b> Every number comes off the finding or off
/// the model it names; nothing here re-derives one, because a renderer that recomputes can disagree
/// with the claim it is printing. Which sentence a disjunction gets is read from the qualifier that
/// holds — <c>docs/DEFECTS.md</c> §16 — rather than chosen here.
/// </para>
/// </remarks>
public static class Claims
{
    /// <summary>Words one finding, or <see cref="Claim.None"/> if its subject is not an analysed type.</summary>
    public static Claim For(SolutionModel model, Finding finding)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(finding);

        return finding.Kind switch
        {
            FindingKind.ConcealedDecisionType => ConcealedType(model, finding),
            FindingKind.ConcealedDecisionMethod => ConcealedMethod(model, finding),
            FindingKind.BugBlastRadius => BlastRadius(model, finding),
            FindingKind.ChangeCost => ChangeCost(model, finding),
            FindingKind.LoadBearingAndIntricate => LoadBearing(model, finding),
            FindingKind.BreaksAlone => BreaksAlone(model, finding),
            FindingKind.HubOrGodObject => Hub(model, finding),
            FindingKind.SharedMutableState => SharedMutableState(model, finding),
            FindingKind.SpansArchitecturalLayers => LayerSpan(model, finding),
            FindingKind.BoundaryCarriesLogic => BoundaryLogic(model, finding),
            FindingKind.WidestContractSurface => ContractSurface(model, finding),
            FindingKind.Coverage => NoPeerGroup(model, finding),
            _ => Claim.None,
        };
    }

    /// <summary>
    /// Whether a kind makes a claim about risk, or discloses what could not be judged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Coverage is not a risk finding and must not be led with as one.</b> It is invariant 8 —
    /// a record that a population got no comparative reading, so that silence is not mistaken for a
    /// clean bill. Putting <i>"no peer group"</i> in a list headed <i>risk</i> asserts something
    /// about a type whose whole entry says nothing could be asserted about it.
    /// </para>
    /// <para>
    /// <b>This does not narrow X10's rule and must not be read as doing so.</b>
    /// <see cref="Selection.Exemplars"/> still returns one exemplar for every kind that fired,
    /// coverage included; what this decides is *where a renderer puts it*, which is the same
    /// distinction the terminal has always drawn by giving coverage its own section. The disclosure
    /// still ships, still carries its count, and is still reachable when the enumeration is not.
    /// </para>
    /// </remarks>
    public static bool IsRiskClaim(FindingKind kind) => kind is not FindingKind.Coverage;

    /// <summary>
    /// The claim a kind makes, in the reader's words.
    /// </summary>
    /// <remarks>
    /// The second copy <see cref="HtmlReport"/> recorded and asked the next renderer to merge. The
    /// terminal's fixed-width banners stay literals — they are a shape rather than a vocabulary,
    /// and nothing else can use them — but every place that needs to *name* a kind in prose reads
    /// this.
    /// </remarks>
    public static string KindName(FindingKind kind) => kind switch
    {
        FindingKind.SpansArchitecturalLayers => "Spans architectural layers",
        FindingKind.ConcealedDecisionType => "Concealed decision",
        FindingKind.ConcealedDecisionMethod => "Concealed decision, method level",
        FindingKind.BugBlastRadius => "Bug blast radius",
        FindingKind.ChangeCost => "Change cost",
        FindingKind.LoadBearingAndIntricate => "Load-bearing and intricate",
        FindingKind.BreaksAlone => "Breaks alone",
        FindingKind.HubOrGodObject => "Hub or god object",
        FindingKind.SharedMutableState => "Shared mutable state",
        FindingKind.BoundaryCarriesLogic => "Boundary carries logic",
        FindingKind.WidestContractSurface => "Widest contract surface",
        FindingKind.Coverage => "No peer group",
        _ => kind.ToString(),
    };

    /// <summary>What a kind is about, for a heading that has to stand without an example under it.</summary>
    public static string KindBlurb(FindingKind kind) => kind switch
    {
        FindingKind.SpansArchitecturalLayers =>
            "Named for one concern, reaching across several — it is doing cross-cutting work whatever it is called.",
        FindingKind.ConcealedDecisionType =>
            "Looks like plumbing, but is far more complex than its peers — and is probably tested like plumbing.",
        FindingKind.ConcealedDecisionMethod =>
            "The same claim about one method. This is the primary level, not a drill-down of the one above.",
        FindingKind.BugBlastRadius =>
            "Widely depended on relative to its peers, and internally complex. A defect here propagates.",
        FindingKind.ChangeCost =>
            "Changing this means changing a lot of callers, judged against the whole solution.",
        FindingKind.LoadBearingAndIntricate =>
            "Much depends on it, it depends on little, and it is intricate enough to hide a bug.",
        FindingKind.BreaksAlone =>
            "Structurally isolated and unstable — it can break on its own, without anything else changing.",
        FindingKind.HubOrGodObject =>
            "Depends on, and is depended on by, much of the system.",
        FindingKind.SharedMutableState =>
            "Writes to static mutable state. Every caller on every thread shares it.",
        FindingKind.BoundaryCarriesLogic =>
            "A boundary type carrying real logic — the place where an outside caller reaches decision-making directly.",
        FindingKind.WidestContractSurface =>
            "The widest contracts this solution exposes, and the most expensive ones to change.",
        FindingKind.Coverage =>
            "Nothing comparable enough to judge these against. Recorded so silence is not mistaken for a clean bill.",
        _ => "",
    };

    // ------------------------------------------------------------------- the sentences ----

    private static Claim ConcealedType(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        var times = finding.ValueOf("MaxMemberCyclomaticXMedian") ?? 0;
        var percentile = finding.ValueOf("MaxMemberCyclomaticPctl") ?? 0;

        // "Looks like plumbing" only holds when connectivity is low in ABSOLUTE terms. The gate is
        // relative, so in a cohort where every member is heavily used "ordinary for its peers"
        // still means widely depended on. Core decides which is true; this picks the words.
        var opening = finding.Holds(Qualifiers.LowAbsoluteConnectivity)
            ? "looks like plumbing but is "
            : "connectivity is unremarkable for its peers, but it is ";

        // The number that ranked the row leads the sentence. This section is ordered on the
        // multiple of the peer median — the quantity `OutlierFactor` gated — and it used to open on
        // the percentile, so nopCommerce printed "top 2%" above two rows reading "top 1%" and the
        // section read as unsorted. An order the reader cannot see is not an order that helps them.
        var basis = double.IsInfinity(times)
            ? "the only complexity among "
            : $"{Sentences.Number(times)}x the median internal complexity of ";

        return new Claim(
            Sentences.Member(type.Name, type.MostComplexMember?.Name ?? ""),
            $"{opening}{basis}{Sentences.PeerGroup(type.Cohort, type.CohortSize)}.",
            $"top {Sentences.TopPercent(percentile)}; cc {type.MaxMemberCyclomatic}, dsm {type.Dsm}, "
            + $"fan-in {type.FanIn}, fan-out {type.FanOut}",
            "");
    }

    private static Claim ConcealedMethod(SolutionModel model, Finding finding)
    {
        var (type, member) = Member(model, finding.Subject);
        if (type is null || member is null) return Claim.None;

        var times = finding.ValueOf("CyclomaticXMedian") ?? 0;
        var peers = finding.ValueOf("CohortSize") ?? 0;

        var basis = double.IsInfinity(times)
            ? "the only complexity among its "
            : $"{Sentences.Number(times)}x the median complexity of its ";

        return new Claim(
            Sentences.Member(type.Name, member.Name),
            $"{basis}{Sentences.Whole(peers)} peers",
            $"cc {member.Cyclomatic}, dsm {member.Dsm}, nesting {member.MaxNestingDepth}, "
            + $"{member.LinesOfCode} lines",
            $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}");
    }

    private static Claim BlastRadius(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"{Sentences.Plural(type.FanIn, "distinct caller")} "
            + $"({Sentences.Number(finding.ValueOf("FanInXMedian") ?? 0)}x its peer median) and "
            + "internally complex. A bug here propagates widely.",
            $"cc {type.Cyclomatic}, fan-out {type.FanOut}, "
            + $"{Sentences.Plural(type.InboundReferenceCount, "call site")}",
            "");
    }

    private static Claim ChangeCost(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"{Sentences.Plural(type.FanIn, "internal caller")} "
            + $"{Sentences.Do(type.FanIn, "depends", "depend")} on this contract "
            + $"({Sentences.Surface(type.DataShape)} of surface). "
            + "Changing it is a distributed edit, not a local one. "
            + "EXTERNAL consumers are not visible to this analysis.",
            type.Classification.Kind,
            "");
    }

    private static Claim LoadBearing(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        // Effective fan-out excludes abstractions, so "depends on nothing" and "depends on nothing
        // concrete" are different claims and the second one names the difference.
        var dependsOn = type.EffectiveFanOut == 0
            ? type.FanOut == 0 ? "nothing" : $"nothing concrete ({type.FanOut} abstractions/contracts)"
            : type.EffectiveFanOut == type.FanOut
                ? $"{type.EffectiveFanOut}"
                : $"{type.EffectiveFanOut} concrete types ({type.FanOut} total)";

        return new Claim(
            type.Name,
            $"{Sentences.Plural(type.FanIn, "type")} "
            + $"{Sentences.Do(type.FanIn, "depends", "depend")} on it, it depends on {dependsOn}. "
            + $"And {type.MostComplexMember?.Name} is cc {type.MaxMemberCyclomatic}. "
            + "Hard to change safely, and intricate enough to hide a bug.",
            "",
            "");
    }

    private static Claim BreaksAlone(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"only {Sentences.Plural(type.FanIn, "type")} "
            + $"{Sentences.Do(type.FanIn, "depends", "depend")} on it. "
            + $"Complex inside (cc {type.MaxMemberCyclomatic}) but isolated — "
            + "if it breaks, it breaks alone.",
            "",
            "");
    }

    private static Claim Hub(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            $"{type.Name} [{type.Classification.Kind}]",
            $"fan-in {type.FanIn}, fan-out {type.FanOut}. "
            + Verdict(finding, type),
            "",
            "");
    }

    /// <summary>
    /// Which danger this hub actually presents.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/DEFECTS.md</c> §16.</b> The probe treats the two arms as one disjunction and
    /// prints "AND carries real logic" whenever either fires — which is false by construction on
    /// the size arm, since a type reaches it precisely by having bulk and no logic. The receipts in
    /// the same sentence then refute the sentence: <i>"carries real logic (23 members, worst method
    /// at cc 1)"</i>. Core carries the two as independent qualifiers, so the sentence is chosen from
    /// what actually holds and cannot contradict its own evidence.
    /// </remarks>
    private static string Verdict(Finding finding, TypeNode type)
    {
        var logic = finding.Holds(Qualifiers.CarriesRealLogic);
        var size = finding.Holds(Qualifiers.TooLargeToHold);
        var worst = type.MostComplexMember;

        if (!logic && !size)
        {
            return "Wiring hub: high coupling both ways but little logic inside "
                   + $"(worst method cc {type.MaxMemberCyclomatic}). Risky to re-route, not to reason about.";
        }

        var what = (logic, size) switch
        {
            (true, true) => "AND carries real logic in something too large to hold at once "
                            + $"({type.MemberCount} members, worst method {worst?.Name} at cc "
                            + $"{type.MaxMemberCyclomatic}, dsm {type.Dsm})",
            (true, false) => $"AND carries real logic ({type.MemberCount} members, worst method "
                             + $"{worst?.Name} at cc {type.MaxMemberCyclomatic}, dsm {type.Dsm})",
            // The size arm alone. No claim about logic, because the receipts would refute it.
            // docs/DEFECTS.md §29: "too large for anyone to hold at once" was read as the report
            // giving up rather than as a claim about the type. Naming the shape says the same thing
            // and cannot be read as an apology.
            _ => $"AND is broad rather than deep ({type.MemberCount} members, no method above "
                 + $"cc {type.MaxMemberCyclomatic}) — a lot to hold at once, but nothing "
                 + "intricate inside it",
        };

        return "Architectural bottleneck: it both depends on and is depended on by much of "
               + $"the system, {what}. Cross-domain orchestration and shared state tend to collect here.";
    }

    private static Claim SharedMutableState(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"{Sentences.Plural(type.StaticMutations, "write")} to static state, "
            + $"and {Sentences.Plural(type.FanIn, "type")} "
            + $"{Sentences.Do(type.FanIn, "calls", "call")} into it. Whether these are "
            + "genuinely contended is a runtime question this analysis cannot answer — "
            + "but the sharing is certain from the code.",
            "",
            "");
    }

    /// <summary>
    /// The layer-span headline. The per-kind breakdown under it is the terminal's and stays there.
    /// </summary>
    /// <remarks>
    /// <c>TECHREQ-job-b.md</c> §3.1 says the per-kind breakdown <i>is</i> the finding, so this is a
    /// headline rather than the whole claim — which is the one place a tier 2 item is deliberately
    /// less than the section it points at, and the reason it points at it.
    /// </remarks>
    private static Claim LayerSpan(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            $"{type.Name} [{type.Classification.Kind}]",
            $"reaches across {Sentences.Whole(finding.ValueOf("KindSpan") ?? 0)} kinds",
            "",
            "");
    }

    private static Claim BoundaryLogic(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"{type.MostComplexMember?.Name} is cc {type.MaxMemberCyclomatic}. "
            + "Business decisions at an external edge are the hardest kind to change later.",
            "",
            "");
    }

    private static Claim ContractSurface(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            type.Name,
            $"{Sentences.Surface(type.DataShape)} across "
            + $"{Sentences.Plural(type.PublicMemberCount, "public member")}.",
            "",
            "");
    }

    /// <summary>
    /// The disclosure, worded — and it is the one claim that is stronger when it says less.
    /// </summary>
    /// <remarks>
    /// Where the type is extreme against the whole solution there is a real thing to say, with the
    /// comparison named so it cannot borrow a cohort's confidence. Where it is not, the entry is
    /// that nothing could be judged, which is the whole of invariant 8's point.
    /// </remarks>
    private static Claim NoPeerGroup(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        var dimensions = Dimensions(finding, type).ToList();

        return dimensions.Count > 0
            ? new Claim(
                $"{type.Name} [{type.Classification.Kind}]",
                $"{string.Join(" and ", dimensions)}, solution-wide.",
                "no cohort to compare against",
                "")
            : new Claim(
                type.Name,
                $"no peer group — {Sentences.Plural(type.CohortSize - 1, "peer")} "
                + $"among {Sentences.PeerGroupNoun(type.Cohort)}. Nothing comparable enough to judge it against.",
                $"fan-in {type.FanIn}, cc {type.Cyclomatic}",
                "");
    }

    /// <summary>
    /// Only the dimension that actually qualifies is stated.
    /// </summary>
    /// <remarks>
    /// In a codebase where most types have no callers, a fan-in of zero lands at a high midrank
    /// percentile — <i>"top 86% by fan-in, 0 callers"</i> is both absurd and corrosive. Core decides
    /// which dimension survives that check and carries it as a qualifier; this only picks the words
    /// for the ones that did.
    /// </remarks>
    internal static IEnumerable<string> Dimensions(Finding finding, TypeNode type)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(type);

        if (finding.Holds(Qualifiers.GloballyExtremeFanIn))
        {
            yield return $"top {Sentences.TopPercent(finding.ValueOf("GlobalFanInPctl") ?? 0)} by fan-in "
                         + $"({Sentences.Plural(type.FanIn, "caller")})";
        }

        if (finding.Holds(Qualifiers.GloballyExtremeComplexity))
        {
            yield return $"top {Sentences.TopPercent(finding.ValueOf("GlobalMaxCcPctl") ?? 0)} by complexity "
                         + $"(cc {type.MaxMemberCyclomatic} in {type.MostComplexMember?.Name})";
        }
    }

    /// <summary>Resolves a member subject back to its declaring type and the member itself.</summary>
    internal static (TypeNode? Type, Member? Member) Member(SolutionModel model, SubjectRef subject)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.DeclaringType is not { } declaring) return (null, null);
        if (model.Find(declaring) is not { } type) return (null, null);

        return (type, type.Members.FirstOrDefault(m => m.Subject == subject));
    }
}

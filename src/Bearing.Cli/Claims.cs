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
            FindingKind.NoStaticReferences => NoStaticReferences(model, finding),
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
    /// Whether this kind competes for the leading rail and enters the per-kind census.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two questions, two predicates, and conflating them would put a lie in a file to achieve a
    /// layout.</b> <see cref="IsRiskClaim"/> answers <i>claim or disclosure</i>, and it feeds the
    /// named population in <see cref="Subjects"/> and the export's class. This answers <i>does it
    /// compete for the lead and the census</i>. A cycle is unambiguously a <b>claim</b>, so
    /// <c>IsRiskClaim</c> is true for it; marking it a disclosure to keep it off the rail would
    /// have been the cheap way here and would have made the export say something false.
    /// <c>SCHEMA-findings-export.md</c> §6.
    /// </para>
    /// <para>
    /// <b>Why cycles do not compete.</b> They have rendered in their own <c>Circular references</c>
    /// section since Job A, and selection is rarest-first: on nopCommerce <c>NamespaceCycle</c> and
    /// <c>TypeTangle</c> fire <b>once each</b> against load-bearing's four, so competing would hand
    /// a cycle the <c>Top finding</c> card on the strength of being rare rather than being worse.
    /// The decision is recorded, not derived here.
    /// </para>
    /// <para>
    /// <b>The two must be able to disagree or the split is decorative</b>, and
    /// <c>ClaimsTests</c> asserts that they do. The one selector answering two questions is
    /// <c>docs/DEFECTS.md</c> §40 and §41's family, which is why this is a second predicate rather
    /// than another arm on the first.
    /// </para>
    /// </remarks>
    public static bool CompetesForLead(FindingKind kind) =>
        kind is not (FindingKind.NamespaceCycle or FindingKind.ProjectCycle or FindingKind.TypeTangle);

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
        FindingKind.NoStaticReferences => "No static references found",
        FindingKind.Coverage => "No peer group",
        FindingKind.NamespaceCycle => "Namespace cycle",
        FindingKind.ProjectCycle => "Project cycle",
        FindingKind.TypeTangle => "Type tangle",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Whether a kind's gate is a fixed count rather than a share of the codebase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Decision X13 named exactly these two, and <c>docs/DEFECTS.md</c> §2 is the measurement
    /// behind it.</b> A comparative gate ranks and therefore always selects the same share; an
    /// absolute gate asserts a property of the type — <c>min(fan-in, fan-out) &gt;= 5</c>, or
    /// instability and complexity both past a bar — and the share it happens to select is a fact
    /// about the codebase rather than about the gate. <c>HubMin = 5</c> takes <b>3.6% of
    /// nopCommerce and 6.9% of Jellyfin</b>: one threshold, two codebases, nearly double.
    /// </para>
    /// <para>
    /// <b>X13 kept both absolute and required them to say why</b>, because converting them is what
    /// would erase the finding: a rank gate cannot report that one codebase is more coupled than
    /// another, since every codebase has a top 5%. This is the "say why", and it is the last
    /// outstanding half of §2.
    /// </para>
    /// </remarks>
    public static bool GateIsAbsolute(FindingKind kind) =>
        kind is FindingKind.HubOrGodObject or FindingKind.BreaksAlone;

    /// <summary>
    /// What an absolute gate selected here, and why that share does not travel.
    /// </summary>
    /// <remarks>
    /// Written once and read by both renderers. The share is stated for <i>this</i> run rather
    /// than quoting the other reference solution: a report should say what it found, and the
    /// caveat is what makes the number safe to carry to a different codebase.
    /// </remarks>
    public static string ShareCaveat(int found, int types)
    {
        var share = types > 0 ? 100.0 * found / types : 0;

        return $"{Sentences.Plural(found, "type")} of {Sentences.Whole(types)} — "
               + $"{share.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%. "
               + "This threshold is a fixed count rather than a share, so the percentage differs "
               + "between codebases: compare what is named, not how many.";
    }

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
        FindingKind.NoStaticReferences =>
            "Nothing in this solution refers to these. Verify before deleting — the categories this analysis cannot see are named beside each one.",
        FindingKind.Coverage =>
            "Nothing comparable enough to judge these against. Recorded so silence is not mistaken for a clean bill.",
        FindingKind.NamespaceCycle =>
            "Sibling namespaces holding each other as state. Namespaces are how .NET expresses layering, so this is the architectural one.",
        FindingKind.ProjectCycle =>
            "Two projects each naming a type in the other. Legal MSBuild, and still the unit anyone extracts.",
        FindingKind.TypeTangle =>
            "Types that all reach each other, so none of them can be understood or moved on its own.",
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

    /// <summary>
    /// The claim leads with the rank, because the rank is what the gate decided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>docs/DEFECTS.md</c> §34, and the measurement moved the diagnosis.</b> The register
    /// recorded this as <i>cohorts are too big to be peers</i> — <i>"93x the median of its 2,909
    /// peers"</i> about a group holding 53% of the solution's methods. Measured, size is not what
    /// breaks the claim: <b>58 of nopCommerce's 70 usable cohorts have a method median of 1 or
    /// 0</b>, so the ratio is the subject's own complexity divided by one. The two largest ratios
    /// on that solution come from cohorts with a median of 1, and the 1,022-method cohort with a
    /// median of 3 produces the <i>smallest</i>.
    /// </para>
    /// <para>
    /// <b>So the sentence says what was measured.</b> <c>b5cc69a</c> made this a rank gate and the
    /// wording kept leading with the ratio — the same mistake as <c>docs/DEFECTS.md</c> §28, where
    /// a section sorted on the multiple and opened on the percentile. Rank is a true statement
    /// about a peer group of any size: <i>the most complex of 2,909 methods</i> is checkable, and
    /// does not collapse when the median sits on the floor.
    /// </para>
    /// <para>
    /// <b>The two numbers are stated side by side rather than as their ratio</b>, so a median of 1
    /// is visible instead of hidden inside a multiplication. The ratio is still a gate and still a
    /// receipt; what it is not any more is the claim.
    /// </para>
    /// <para>
    /// <b>And the population is counted in the right units.</b> The old sentence read
    /// <i>"its 2,909 peers"</i> off <c>CohortSize</c>, which counts <b>methods</b> — the group is
    /// 193 types. Both numbers are said now, because a reader who checks either one against the
    /// cohort would otherwise find the tool wrong.
    /// </para>
    /// <para>
    /// <b>Type-level concealed decision keeps the ratio wording deliberately</b>: its gate is still
    /// the multiple, so leading on it is honest there. The two sentences differ because the two
    /// gates do — <c>TASKS.md</c>, D2.
    /// </para>
    /// </remarks>
    private static Claim ConcealedMethod(SolutionModel model, Finding finding)
    {
        var (type, member) = Member(model, finding.Subject);
        if (type is null || member is null) return Claim.None;

        var rank = finding.ValueOf("CyclomaticRank") ?? 1;
        var methods = finding.ValueOf("CohortSize") ?? 0;
        var median = finding.ValueOf("MedianCohortCyclomatic") ?? 0;

        var standing = rank <= 1
            ? "the most complex"
            : $"among the {Sentences.Whole(Math.Ceiling(rank))} most complex";

        return new Claim(
            Sentences.Member(type.Name, member.Name),
            $"{standing} of the {Sentences.Plural(methods, "method")} in "
            + $"{Sentences.PeerGroup(type.Cohort, type.CohortSize)}.",
            $"cc {member.Cyclomatic} against a peer median of {Sentences.Number(median)}; "
            + $"dsm {member.Dsm}, nesting {member.MaxNestingDepth}, {member.LinesOfCode} lines",
            $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}");
    }

    /// <summary>
    /// A member nothing in this solution refers to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sentence never says "dead", "unused" or "safe to delete", and that is
    /// <c>TECHREQ-job-a.md</c> §5.6 written down rather than remembered.</b> The label it
    /// prescribes is <i>"no static references found — verify before deleting"</i>, and invariant 4
    /// is the reason: a tool that implies safety about something six customers depend on has caused
    /// the burn it claimed to prevent. What this can honestly say is what it looked for and did not
    /// find.
    /// </para>
    /// <para>
    /// <b>The qualifiers are the second half of the requirement</b> — §5.6 asks the report to name
    /// each category it could not rule out, so they are appended to the sentence rather than shown
    /// somewhere a reader might not reach. A nomination with no qualifier is the strongest form
    /// this finding takes, and it is still only "nothing here refers to it".
    /// </para>
    /// <para>
    /// <b>Whether the declaring type is referenced is part of the claim, not decoration.</b> "This
    /// type is used and this member of it is not" and "nothing reaches any of this" are different
    /// findings with different remedies, and the reader can act on the first without opening
    /// anything.
    /// </para>
    /// </remarks>
    private static Claim NoStaticReferences(SolutionModel model, Finding finding)
    {
        var (type, member) = Member(model, finding.Subject);
        if (type is null || member is null) return Claim.None;

        var typeInbound = finding.ValueOf("DeclaringTypeInboundReferences") ?? 0;
        // A member of an unread carrier says so on its own face, because the terminal section is
        // not the only place a finding is read: Selection.Exemplars can lead START HERE with one
        // of these, and a lone member sentence there would contradict the grouped row below it.
        // A13's rule is that every finding is worded in one place and both renderers read it.
        if (finding.Holds(Qualifiers.PartOfAnUnreadGroup))
        {
            var unread = finding.ValueOf("UnreadDataMembersInType") ?? 0;
            var data = finding.ValueOf("DeclaringTypeDataMembers") ?? 0;

            return new Claim(
                Sentences.Member(type.Name, member.Name),
                $"no static references found — verify before deleting. {Sentences.Whole(unread)} of "
                + $"{type.Name}'s {Sentences.Plural(data, "data member")} "
                + $"{Sentences.Do(unread, "has", "have")} no reader, so read them as a group.",
                $"{member.Accessibility.ToLowerInvariant()} {member.Kind.ToString().ToLowerInvariant()}, "
                + Sentences.Plural(member.LinesOfCode, "line"),
                $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}");
        }

        // Two different findings and two different remedies: a live type with a member nothing
        // calls is a member to remove; a type nothing reaches at all is a type to remove, and
        // saying so about each of its members individually would be saying it many times.
        // Sentences.Do rather than an inline ternary, which is docs/DEFECTS.md §32's remedy — and
        // §32 said the next such number was "a defect waiting on the right input". It was: this
        // read "1 reference reach UserAgentHelper itself" on nopCommerce, and the fixture makes no
        // singular here either.
        var standing = typeInbound > 0
            ? $"{Sentences.Plural(typeInbound, "reference")} "
              + $"{Sentences.Do(typeInbound, "reaches", "reach")} {type.Name} itself"
            : $"Nothing refers to {type.Name} either";

        // The categories go in the evidence rather than the sentence, and the reason is what the
        // section looked like when they did not: one of them is solution-level and therefore true
        // of every row, so it printed the same clause seventy-eight times. A parenthetical is
        // where every other section puts the qualifying detail, and the claim stays one sentence.
        var unseen = Unseen(finding).ToList();
        var evidence = $"{member.Accessibility.ToLowerInvariant()} {member.Kind.ToString().ToLowerInvariant()}, "
                       + Sentences.Plural(member.LinesOfCode, "line");

        if (unseen.Count > 0) evidence += "; not visible here: " + Sentences.List(unseen);

        return new Claim(
            Sentences.Member(type.Name, member.Name),
            $"no static references found — verify before deleting. {standing}.",
            evidence,
            $"{Path.GetFileName(member.Location.File)}:{member.Location.Line}");
    }

    /// <summary>
    /// The categories this analysis could not rule out, in the reader's words.
    /// </summary>
    /// <remarks>
    /// Read off the qualifiers the finding carries rather than re-derived, which is
    /// <c>docs/ARCHITECTURE.md</c> §3: Core decides whether the qualifying fact holds and this
    /// decides what words to put it in.
    /// </remarks>
    private static IEnumerable<string> Unseen(Finding finding)
    {
        if (finding.Holds(Qualifiers.AnAttributeMayDirectIt))
            yield return "whatever an attribute on it directs there";

        if (finding.Holds(Qualifiers.TestUsageUnobservable)) yield return "usage from test projects";
    }

    private static Claim BlastRadius(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        // A ratio against a zero median is undefined, and `Sentences.Number` says so — but
        // "undefined" followed by an "x" is not a word. docs/DEFECTS.md §38. The concealed-decision
        // sentence above already branches for this; this one never did, and shipped
        // "89 distinct callers (undefinedx its peer median)" on nopCommerce's BaseController.
        //
        // The replacement states what is actually true and no more: the median is zero, so the
        // typical peer has no callers at all and no multiple of it exists. That is a weaker claim
        // than a ratio rather than a stronger one, which is the same choice §3.2 makes.
        var times = finding.ValueOf("FanInXMedian") ?? 0;
        var against = double.IsInfinity(times)
            ? "(its peer median is zero)"
            : $"({Sentences.Number(times)}x its peer median)";

        return new Claim(
            type.Name,
            $"{Sentences.Plural(type.FanIn, "distinct caller")} "
            + $"{against} and "
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

    /// <summary>
    /// The hub claim, which opens the rail — and opens it on two terms it has to define.
    /// </summary>
    /// <remarks>
    /// <b><c>docs/DEFECTS.md</c> §51.</b> The sentence led with <c>fan-in 28, fan-out 24</c> and
    /// defined neither. The two words appear nowhere else on the page as anything but threshold
    /// <i>names</i> inside the collapsed <c>Show all thresholds</c> table — <c>MinFanIn</c>,
    /// <c>ConcealedFanInCeiling</c> — which is §27's surface and not a glossary.
    /// <para>
    /// <b>A11 round 2's participants explained the two to each other to get through T3</b>, and it
    /// worked because five people were in one room. <c>PRD-free-tier.md</c> §2 defines the target
    /// reader as explicitly not the architect who thinks in coupling metrics, and that reader is
    /// alone. The claim already spent a clause explaining <i>bottleneck</i>; the two measures it
    /// leads with got none.
    /// </para>
    /// <para>
    /// <b>In place, and not as a second sentence restating the numbers.</b> <i>"fan-in 7, fan-out
    /// 7 — 7 types use it, and it uses 7 types"</i> was the first attempt and it prints every digit
    /// twice, which reads as arithmetic rather than as a definition. A parenthesis after each
    /// number defines the term where the reader meets it and costs four words.
    /// </para>
    /// </remarks>
    private static Claim Hub(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        return new Claim(
            $"{type.Name} [{type.Classification.Kind}]",
            $"fan-in {type.FanIn} (types that use it), "
            + $"fan-out {type.FanOut} (types it uses). "
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
    /// <summary>
    /// The one claim whose evidence is a breakdown rather than a row of measurements.
    /// </summary>
    /// <remarks>
    /// <b><c>TECHREQ-job-b.md</c> §3.1 says the per-kind breakdown <i>is</i> the finding</b>, and
    /// until A13 tier 3 the record carried none of it: the claim was <i>"reaches across 3
    /// kinds"</i> and the kinds themselves existed only in the terminal section's own loop. That
    /// was invisible while the page rendered ten equal cards and became the whole of the lead card
    /// the day one of them was enlarged — on nopCommerce this is the finding the rarest-first rule
    /// selects, so the page's screenshot frame carried a claim with no numbers under it.
    /// <b>Counts here, names in the section</b>: the two are the same breakdown at two
    /// granularities, ordered the same way, and neither re-derives the other's ordering.
    /// </remarks>
    private static Claim LayerSpan(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        var byKind = finding.Participants
            .Select(model.Find)
            .Where(t => t is not null)
            .GroupBy(t => t!.Classification.Kind, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t!.Name).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);

        var kinds = new SortedSet<string>(byKind.Keys, StringComparer.Ordinal);

        // The type's own role counts toward the span where the span exceeds what its dependencies
        // reach — the component is cross-cutting partly by being where it is. The section words
        // that as "itself"; so does this, for the same reason a count of one would be a lie.
        if ((finding.ValueOf("KindSpan") ?? 0) > byKind.Count) kinds.Add(type.Classification.Kind);

        return new Claim(
            $"{type.Name} [{type.Classification.Kind}]",
            $"reaches across {Sentences.Whole(finding.ValueOf("KindSpan") ?? 0)} kinds",
            string.Join(", ", kinds.Select(k =>
                byKind.TryGetValue(k, out var count) ? $"{count} {k}" : $"{k} itself")),
            "");
    }

    /// <summary>
    /// A boundary carrying decisions, with the population it was judged against.
    /// </summary>
    /// <remarks>
    /// <b>The evidence line is new with <c>docs/DEFECTS.md</c> §33 and the gap was old.</b> This
    /// claim carried no numbers at all, which was invisible while every card was the same size and
    /// became the whole of the lead card the moment the rank gate made this the rarest kind on the
    /// fixture — the same hole layer span had, found the same way. What it states is the pair the
    /// gate reads: where the type sits among the boundaries, and what the median boundary looks
    /// like, so <i>"cc 14"</i> is a comparison rather than a number.
    /// </remarks>
    private static Claim BoundaryLogic(SolutionModel model, Finding finding)
    {
        if (model.Find(finding.Subject) is not { } type) return Claim.None;

        var boundaries = finding.ValueOf("BoundaryCount") ?? 0;
        var median = finding.ValueOf("MedianBoundaryCyclomatic") ?? 0;

        return new Claim(
            type.Name,
            $"{type.MostComplexMember?.Name} is cc {type.MaxMemberCyclomatic}. "
            + "Business decisions at an external edge are the hardest kind to change later.",
            boundaries > 0
                ? $"among the most complex of this solution's {Sentences.Whole(boundaries)} "
                  + $"{Sentences.Do(boundaries, "boundary", "boundaries")}, where the median is "
                  + $"cc {Sentences.Number(median)}"
                : "",
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

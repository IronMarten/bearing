namespace IronMarten.Bearing;

/// <summary>
/// One row of <c>TECHREQ-job-b.md</c> §4: a claim that must not be made, and why.
/// </summary>
/// <param name="Name">A stable identifier for the row, so a suppressed finding can say what silenced it.</param>
/// <param name="Kind">The finding this row suppresses.</param>
/// <param name="Invariant">The <c>PRD-free-tier.md</c> §6 invariant it enforces.</param>
/// <param name="Reason">Why the claim would be wrong, in the terms a developer would challenge it in.</param>
/// <remarks>
/// <para>
/// <b>The matrix is a table in the specification and a table here, deliberately.</b> §4 calls
/// suppression the most valuable and least documented part of Job B, and names the risk: in the
/// probe it exists as ordering and inline <c>Where</c> clauses inside a 997-line renderer, which
/// makes it the part most likely to be lost in extraction and the least likely to fail loudly
/// when it is. A rule that is a row in one place and a row in the other can be checked against
/// its source by reading; a rule inlined into a detector cannot.
/// </para>
/// <para>
/// <b>Two of breaks-alone's three rows are conditions on the subject rather than relationships
/// between findings</b>, and they still live here rather than in the detector. §4 lists them as
/// suppressions, and the one-to-one correspondence is worth more than the purity: a reader
/// checking the seven rows against the code should find seven rules, not four rules and three
/// conditions distributed into two files.
/// </para>
/// </remarks>
public sealed record SuppressionRule(
    string Name,
    FindingKind Kind,
    string Invariant,
    string Reason)
{
    /// <summary>Whether this row silences a finding of <see cref="Kind"/>.</summary>
    public required Func<Finding, FindingSet, SolutionModel, bool> Applies { get; init; }

    /// <summary>
    /// Evaluates the row, including the check that it is even the right kind of finding.
    /// </summary>
    /// <remarks>
    /// The kind test is here rather than in each <see cref="Applies"/> so that no row can be
    /// written that silences a finding it was not specified for. Row 2 asks only "is this type
    /// already nominated as a concealed decision", which is true of plenty of findings that must
    /// not be removed.
    /// </remarks>
    public bool AppliesTo(Finding finding, FindingSet detected, SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(model);

        return finding.Kind == Kind && Applies(finding, detected, model);
    }
}

/// <summary>
/// The suppression matrix — <c>TECHREQ-job-b.md</c> §4, as data.
/// </summary>
/// <remarks>
/// <para>
/// Evaluated as a pass over the whole detected set, never inside a detector. That is
/// <c>docs/ARCHITECTURE.md</c> §5's requirement and it is not a stylistic one: in the probe,
/// breaks-alone's concealed-decision exclusion works by capturing the nominations made earlier in
/// the same method and testing membership later, so any reordering of the renderer breaks
/// invariant 3 — and breaking it produces <i>more</i> output, which reads as a working tool.
/// </para>
/// <para>
/// <b>Only breaks alone's three rows are here, and two more will never be.</b> Rows 4 and 6 are
/// not finding suppressions at all: each silences a <i>sentence</i>, and both are carried as a
/// <see cref="Qualifier"/> on the finding instead. Row 4's evidence for that is the probe's own
/// output — a collapsed type keeps its place in the examples line, so the claim survives and only
/// its detail is dropped. Rows 5 and 7 belong to findings that have not moved.
/// </para>
/// </remarks>
public static class Suppression
{
    /// <summary>
    /// Architectural roles at which the tool cannot see who else is depending on a type.
    /// </summary>
    private static readonly string[] Unseeable = ["ApiBoundary", "ExternalCall", "Contract"];

    /// <summary>The rows implemented so far, in matrix order.</summary>
    public static IReadOnlyList<SuppressionRule> Rules { get; } =
    [
        new SuppressionRule(
            "breaks-alone-at-a-boundary",
            FindingKind.BreaksAlone,
            Invariant: "4",
            "the tool cannot see external consumers, and \"isolated\" is the one claim it must " +
            "not get wrong at an edge")
        {
            Applies = (finding, _, model) =>
                model.Find(finding.Subject) is { } type &&
                Unseeable.Contains(type.Classification.Kind, StringComparer.Ordinal),
        },

        new SuppressionRule(
            "breaks-alone-decides-something",
            FindingKind.BreaksAlone,
            Invariant: "3",
            "structural isolation is not safety when a component decides something — a " +
            "normalizer that picks the wrong option propagates into the data going out the " +
            "door, not through the call graph")
        {
            // At type level or on any of its methods, which is the whole of docs/DEFECTS.md §15.
            // The reason the rule exists is behavioural and behaviour lives in methods, so the
            // level that happened to nominate it does not change whether the decision is there.
            // ContainsAbout is the query SubjectRef's member -> declaring type walk was built for.
            Applies = (finding, detected, _) =>
                detected.ContainsAbout(FindingKind.ConcealedDecisionType, finding.Subject) ||
                detected.ContainsAbout(FindingKind.ConcealedDecisionMethod, finding.Subject),
        },

        new SuppressionRule(
            "breaks-alone-is-unreferenced",
            FindingKind.BreaksAlone,
            Invariant: "4",
            "that is unreferenced code, a different finding, not reassurance")
        {
            Applies = (finding, _, model) =>
                model.Find(finding.Subject) is { } type &&
                type.FanIn < model.Policy.BreaksAloneMinFanIn,
        },

        new SuppressionRule(
            "widest-surface-is-not-discriminating",
            FindingKind.WidestContractSurface,
            Invariant: "2",
            "it is not discriminating, so it is noise — the section promises to name what stands " +
            "out and a list of seven is not that")
        {
            // The first row that suppresses a SET rather than a subject: every member goes or
            // none does, because what is wrong is the size of the set and not anything about the
            // type. docs/DEFECTS.md §12.
            //
            // An absolute count, and that is the whole repair. The probe asks whether the
            // qualifying set exceeds half the boundaries, and the qualifying filter is already
            // proportional to the same distribution — so the set lands on the threshold and never
            // crosses it, at any boundary count. A gate cannot sit on a filter that has already
            // bounded the thing it measures.
            Applies = (_, detected, model) =>
                detected.OfKind(FindingKind.WidestContractSurface).Count > model.Policy.MaxNamedSurfaces,
        },
    ];

    /// <summary>
    /// The row that silences <paramref name="finding"/>, or <see langword="null"/> when the claim
    /// stands.
    /// </summary>
    /// <remarks>
    /// The first matching row wins, and which one it is is reported rather than discarded — a
    /// finding removed for the wrong reason and a finding removed for the right one are
    /// indistinguishable from the surviving set alone, and only one of them is a working
    /// suppression.
    /// </remarks>
    public static SuppressionRule? Silencing(Finding finding, FindingSet detected, SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(model);

        foreach (var rule in Rules)
            if (rule.AppliesTo(finding, detected, model))
                return rule;

        return null;
    }
}

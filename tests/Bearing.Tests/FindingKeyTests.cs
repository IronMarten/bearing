using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The identity key, tested against the two things that depend on it: suppression needs
/// equality within a run, acknowledgment memory needs equality across runs.
///
/// Each test below corresponds to a way the key could be wrong that would not fail loudly.
/// A key that collides silences a finding that should have fired; a key that is unstable
/// discards an acknowledgment the user made. Neither produces an error.
/// </summary>
public sealed class FindingKeyTests
{
    private const string AsmA = "Nop.Core";
    private const string AsmB = "Nop.Plugin.Shipping";
    private const string Fqn = "global::Nop.Core.Domain.Shipping.ShipmentItem";

    [Fact]
    public void Same_type_name_in_two_assemblies_is_two_subjects()
    {
        // The defect this key exists to fix. Keyed on name alone these merge into one row with
        // FanIn, FanOut, Cyclomatic, Dsm, Loc and MemberCount summed across both — which on
        // nopCommerce fabricated a five-project circular reference. TECHREQ-job-b.md §8.8.
        var inCore = SubjectRef.ForType(AsmA, Fqn);
        var inPlugin = SubjectRef.ForType(AsmB, Fqn);

        Assert.NotEqual(inCore, inPlugin);
        Assert.NotEqual(inCore.Canonical, inPlugin.Canonical);
    }

    [Fact]
    public void Same_type_in_the_same_assembly_is_one_subject()
    {
        // Partial classes within one compilation are one type. The fix must not overshoot into
        // splitting those apart.
        Assert.Equal(SubjectRef.ForType(AsmA, Fqn), SubjectRef.ForType(AsmA, Fqn));
    }

    [Fact]
    public void Overloads_are_different_members()
    {
        // A finding about one overload must not silence a finding about another.
        var one = SubjectRef.ForMember(AsmA, Fqn, "Calculate(decimal)");
        var two = SubjectRef.ForMember(AsmA, Fqn, "Calculate(decimal, string)");

        Assert.NotEqual(one, two);
    }

    [Fact]
    public void A_member_knows_the_type_that_declares_it()
    {
        // Suppression walks this edge: breaks-alone is suppressed for a type already nominated
        // as a concealed decision, and concealed decision can be nominated at method level.
        var member = SubjectRef.ForMember(AsmA, Fqn, "Calculate(decimal)");

        Assert.Equal(SubjectRef.ForType(AsmA, Fqn), member.DeclaringType);
        Assert.Null(SubjectRef.ForType(AsmA, Fqn).DeclaringType);
    }

    [Fact]
    public void A_set_does_not_depend_on_discovery_order()
    {
        // Tarjan returns component membership, which has no inherent order. If traversal order
        // reached the identity, a cycle would look "new" because the walk started elsewhere.
        var a = SubjectRef.ForProject("Nop.Core");
        var b = SubjectRef.ForProject("Nop.Data");
        var c = SubjectRef.ForProject("Nop.Services");

        Assert.Equal(SubjectRef.ForSet([a, b, c]), SubjectRef.ForSet([c, a, b]));
    }

    [Fact]
    public void A_set_ignores_a_repeated_member()
    {
        var a = SubjectRef.ForProject("Nop.Core");
        var b = SubjectRef.ForProject("Nop.Data");

        Assert.Equal(SubjectRef.ForSet([a, b]), SubjectRef.ForSet([a, b, a]));
        Assert.Equal(2, SubjectRef.ForSet([a, b, a]).Members.Count);
    }

    [Fact]
    public void A_set_of_different_members_is_a_different_set()
    {
        var a = SubjectRef.ForProject("Nop.Core");
        var b = SubjectRef.ForProject("Nop.Data");
        var c = SubjectRef.ForProject("Nop.Services");

        Assert.NotEqual(SubjectRef.ForSet([a, b]), SubjectRef.ForSet([a, c]));
    }

    [Fact]
    public void Components_cannot_be_forged_across_the_separator()
    {
        // Method signatures and generic names contain punctuation freely, and a set nests other
        // canonical forms inside itself. Without escaping, two different subjects could produce
        // one string — silently merging two findings, the same failure as keying on name alone.
        var left = SubjectRef.ForType("A|B", "C");
        var right = SubjectRef.ForType("A", "B|C");

        Assert.NotEqual(left, right);
        Assert.NotEqual(left.Canonical, right.Canonical);
    }

    [Fact]
    public void A_backslash_in_a_component_does_not_forge_a_separator_either()
    {
        Assert.NotEqual(
            SubjectRef.ForType("A\\", "B"),
            SubjectRef.ForType("A", "B"));
    }

    [Fact]
    public void Kind_and_subject_together_make_the_identity()
    {
        var subject = SubjectRef.ForType(AsmA, Fqn);

        Assert.Equal(
            new FindingKey(FindingKind.BreaksAlone, subject),
            new FindingKey(FindingKind.BreaksAlone, subject));

        Assert.NotEqual(
            new FindingKey(FindingKind.BreaksAlone, subject),
            new FindingKey(FindingKind.ConcealedDecisionType, subject));
    }

    [Fact]
    public void The_canonical_form_names_the_kind_rather_than_numbering_it()
    {
        // Enum values renumber when a member is inserted. A stored acknowledgment that silently
        // changes meaning across a tool upgrade is worse than one that fails to match.
        var key = new FindingKey(FindingKind.BreaksAlone, SubjectRef.ForType(AsmA, Fqn));

        Assert.StartsWith("BreaksAlone|", key.Canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Membership_is_testable_before_anything_renders()
    {
        // The suppression requirement, in the smallest form that expresses it. Today this works
        // by capturing nominations earlier in the same method, which makes renderer ordering
        // load-bearing — reorder it and invariant 3 breaks silently. TECHREQ-job-b.md §4.
        var suspect = SubjectRef.ForType(AsmA, Fqn);
        var bystander = SubjectRef.ForType(AsmA, "global::Nop.Core.Domain.Orders.Order");

        // A separately constructed key must match, or membership testing is reference equality
        // wearing a disguise.
        var nominated = new HashSet<FindingKey>
        {
            new(FindingKind.ConcealedDecisionType, suspect),
        };

        Assert.Contains(new FindingKey(FindingKind.ConcealedDecisionType, suspect), nominated);
        Assert.DoesNotContain(new FindingKey(FindingKind.ConcealedDecisionType, bystander), nominated);
        Assert.DoesNotContain(new FindingKey(FindingKind.BreaksAlone, suspect), nominated);
    }

    [Fact]
    public void The_canonical_form_is_a_pure_function_of_kind_and_subject()
    {
        // Acknowledgment memory persists this string and has to recognise it next run, in a
        // different process, with the type discovered by a different traversal. Nothing about
        // how the key was reached may reach the string.
        //
        // This is not yet a round trip: there is no parser, because nothing reads an
        // acknowledgment file yet. When one exists, that test belongs beside this one.
        var written = new FindingKey(
            FindingKind.HubOrGodObject,
            SubjectRef.ForMember(AsmA, Fqn, "Calculate(decimal)")).Canonical;

        var reconstructed = new FindingKey(
            FindingKind.HubOrGodObject,
            SubjectRef.ForMember(AsmA, Fqn, "Calculate(decimal)")).Canonical;

        Assert.Equal(written, reconstructed, StringComparer.Ordinal);
    }

    [Fact]
    public void Coverage_is_about_the_solution_and_needs_no_narrower_subject()
    {
        Assert.Equal(SubjectKind.Solution, SubjectRef.Solution.Kind);
        Assert.NotEqual(SubjectRef.Solution, SubjectRef.ForProject("Nop.Core"));
    }

    [Fact]
    public void An_empty_set_is_rejected_rather_than_silently_identified()
    {
        Assert.Throws<ArgumentException>(() => SubjectRef.ForSet([]));
    }

    [Fact]
    public void A_blank_component_is_rejected()
    {
        // An empty assembly name would make every unassigned type the same subject.
        Assert.Throws<ArgumentException>(() => SubjectRef.ForType("", Fqn));
        Assert.Throws<ArgumentException>(() => SubjectRef.ForType(AsmA, "   "));
    }
}

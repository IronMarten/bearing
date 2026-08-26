using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The namespace lists that decide an architectural role, and the rule that matches them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file is <c>TASKS.md</c> P10, and it exists because the fixture cannot do this job.</b>
/// A measured fix added <c>LinqToDB</c> and <c>FluentMigrator</c> to the data-access
/// list on the evidence of two real-solution runs — 134 reclassifications on nopCommerce, 0 on
/// jellyfin — and TestBed references neither library. It cannot, without taking a NuGet dependency
/// for the sake of a classification rule. So the suite was byte-identical with the list and
/// without it, and the fix could have been reverted by anyone with everything green.
/// </para>
/// <para>
/// <b>A plant was the other option and this is the cheaper one.</b> P10 was recorded as a fixture
/// plant with a note that making the list assertable would probably beat it; that is what this is.
/// It cannot prove the classifier reaches these lists — <c>StructureTests.Kind_is_classified_as_expected</c>
/// does that on real types — only that the lists say what they are meant to say and that the
/// matching rule means what it claims. That is exactly the half that had nothing watching it.
/// </para>
/// </remarks>
public sealed class FrameworkNamespacesTests
{
    /// <summary>
    /// The data-access list still carries what D5 measured it needed.
    /// </summary>
    /// <remarks>
    /// Transcribed rather than derived, which is the point: this fails if anyone trims the list,
    /// and there is no other way to notice. The two names D5 added are called out because they are
    /// the ones the fixture cannot reach and therefore the ones at risk.
    /// </remarks>
    [Fact]
    public void The_data_access_list_is_what_it_was_measured_to_need()
    {
        Assert.Equal(
            [
                "Microsoft.EntityFrameworkCore",
                "System.Data",
                "Dapper",
                "NHibernate",
                "LinqToDB",
                "FluentMigrator",
            ],
            FrameworkNamespaces.DataAccess);
    }

    /// <summary>The other two lists, for the same reason.</summary>
    [Fact]
    public void The_boundary_and_external_lists_are_transcribed()
    {
        Assert.Equal(["Microsoft.AspNetCore"], FrameworkNamespaces.ApiBoundary);

        Assert.Equal(
            [
                "System.Net.Http",
                "Azure",
                "Amazon",
                "RabbitMQ",
                "Confluent.Kafka",
                "MassTransit",
                "Stripe",
                "Twilio",
                "SendGrid",
                "Polly",
            ],
            FrameworkNamespaces.ExternalCall);
    }

    /// <summary>
    /// The namespaces nopCommerce and jellyfin actually reach, classified the way those runs
    /// classified them.
    /// </summary>
    /// <remarks>
    /// <b>The real-solution evidence, held in the suite.</b> Each of these is a namespace observed
    /// on a reference solution and the list it has to fall into; without them, "the list is what it
    /// was measured to need" is a claim about six strings rather than about two codebases.
    /// </remarks>
    [Theory]
    [InlineData("FluentMigrator.Builders")]     // 129 nopCommerce types — the mapping layer
    [InlineData("FluentMigrator")]              // 15 more
    [InlineData("FluentMigrator.Runner")]
    [InlineData("LinqToDB")]
    [InlineData("LinqToDB.Mapping")]
    [InlineData("LinqToDB.DataProvider")]
    [InlineData("System.Data")]                 // the 23 that classified before D5, incidentally
    [InlineData("System.Data.Common")]
    [InlineData("Microsoft.EntityFrameworkCore")]        // jellyfin's, and why it did not move
    [InlineData("Microsoft.EntityFrameworkCore.Storage")]
    public void A_namespace_a_reference_solution_reaches_is_data_access(string @namespace) =>
        Assert.Equal(@namespace, FrameworkNamespaces.Match([@namespace], FrameworkNamespaces.DataAccess));

    /// <summary>
    /// Matching is by namespace segment, so a prefix cannot claim a namespace that merely starts
    /// with its letters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule was <c>StartsWith</c> until P10. <c>StructureTests</c> pins the same principle one
    /// level up for namespace <i>collection</i> — <c>System.Net.Http</c> was once truncated to
    /// <c>System.Net</c>, and an HttpClient gateway stopped being a boundary at all — and matching
    /// had the mirror-image hole.
    /// </para>
    /// <para>
    /// Measured before the change: nothing on nopCommerce, jellyfin or TestBed matched any prefix
    /// except at a segment boundary, so this preserves behaviour on every solution anyone has run.
    /// It is here so that stays true of the next one.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("System.Database")]
    [InlineData("System.DataAnnotations")]
    [InlineData("DapperExtensions")]
    [InlineData("StripeCompatibility")]
    [InlineData("PollyExtras")]
    public void A_namespace_that_only_shares_a_prefix_is_not_a_match(string @namespace)
    {
        Assert.Null(FrameworkNamespaces.Match([@namespace], FrameworkNamespaces.DataAccess));
        Assert.Null(FrameworkNamespaces.Match([@namespace], FrameworkNamespaces.ExternalCall));
    }

    /// <summary>
    /// A trailing dot is no longer needed, and no entry carries one.
    /// </summary>
    /// <remarks>
    /// <c>Azure.</c> and <c>Amazon.</c> carried one because under <c>StartsWith</c> a bare prefix
    /// over-matched; the other fifteen did not, so whether a new entry needed a dot was a coin
    /// flip. Segment matching makes it meaningless — asserted, so nobody adds one back and quietly
    /// breaks their own entry, since <c>Azure.</c> would now match neither <c>Azure</c> nor
    /// <c>Azure.Storage</c>.
    /// </remarks>
    [Fact]
    public void No_entry_carries_a_trailing_dot()
    {
        string[][] lists =
        [
            [.. FrameworkNamespaces.ApiBoundary],
            [.. FrameworkNamespaces.DataAccess],
            [.. FrameworkNamespaces.ExternalCall],
        ];

        Assert.All(lists, list => Assert.All(list, entry =>
            Assert.False(entry.EndsWith('.'), $"'{entry}' carries a trailing dot")));

        // And the two that used to still match what they were there for.
        Assert.Equal("Azure.Storage.Blobs",
            FrameworkNamespaces.Match(["Azure.Storage.Blobs"], FrameworkNamespaces.ExternalCall));
        Assert.Equal("Amazon.S3",
            FrameworkNamespaces.Match(["Amazon.S3"], FrameworkNamespaces.ExternalCall));
    }

    /// <summary>The lists do not overlap, so a namespace cannot decide two roles.</summary>
    /// <remarks>
    /// Classification returns on the first arm that matches, so an overlap would make the answer a
    /// property of the order the arms are written in rather than of the namespace.
    /// </remarks>
    [Fact]
    public void No_namespace_can_satisfy_two_roles()
    {
        var all = FrameworkNamespaces.ApiBoundary
            .Concat(FrameworkNamespaces.DataAccess)
            .Concat(FrameworkNamespaces.ExternalCall)
            .ToList();

        foreach (var entry in all)
        {
            var roles = 0;
            if (FrameworkNamespaces.Match([entry], FrameworkNamespaces.ApiBoundary) is not null) roles++;
            if (FrameworkNamespaces.Match([entry], FrameworkNamespaces.DataAccess) is not null) roles++;
            if (FrameworkNamespaces.Match([entry], FrameworkNamespaces.ExternalCall) is not null) roles++;

            Assert.True(roles == 1, $"'{entry}' satisfies {roles} roles, so the arm order decides it");
        }
    }
}

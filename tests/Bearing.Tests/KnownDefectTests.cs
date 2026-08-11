using ArchProbe;

namespace Bearing.Tests;

/// <summary>
/// Defects found in the frozen oracle after the freeze, pinned rather than fixed.
/// </summary>
/// <remarks>
/// <para>
/// <c>oracle/ArchProbe</c> cannot be edited: the golden baselines are the record of its exact
/// output, and changing the implementation they describe is the one thing the oracle exists
/// not to do (<c>CONTRIBUTING.md</c>). A defect found after the freeze therefore gets a test
/// that states the <b>wrong</b> behaviour as the current behaviour.
/// </para>
/// <para>
/// That cuts both ways on purpose. Extraction cannot carry the defect forward silently,
/// because the requirement is written down beside it — and it cannot fix it silently either,
/// because the day <c>Bearing.Core</c> does the right thing this test fails and somebody has
/// to delete it deliberately. Deleting it is the event worth seeing.
/// </para>
/// <para>
/// Every entry names the requirement that supersedes it.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class KnownDefectTests(FixtureRun run)
{
    /// <summary>
    /// CHANGE COST gates on <c>--min-cohort</c>, a cohort-size floor, where it means
    /// <c>--min-fan-in</c>, the "widely depended on" floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both default to 5, so the two are indistinguishable at defaults and the defect is
    /// invisible in <c>golden/nominations.verified.txt</c>. It only appears when either is
    /// tuned — which is exactly when someone is relying on the threshold to mean what it says.
    /// </para>
    /// <para>
    /// The reading that settles it: CHANGE COST is not cohort-relative at all. It runs over
    /// every type rather than the eligible set, so a cohort threshold has nothing to be a
    /// threshold on.
    /// </para>
    /// <para>
    /// Superseded by <c>TECHREQ-job-b.md</c> §3.5 and §9. Fix in Core during extraction.
    /// </para>
    /// </remarks>
    [Fact]
    public void Change_cost_is_gated_by_min_cohort_where_it_means_min_fan_in()
    {
        // The wrong knob has an effect: raising the COHORT floor above a contract's fan-in
        // drops it from a finding that has no cohort in it.
        var byCohort = ChangeCostSubjects(new Options { MinCohort = 16 });

        Assert.Contains("NormalizationContext", byCohort);       // fan-in 20
        Assert.Contains("RawResponse", byCohort);                // fan-in 18
        Assert.DoesNotContain("NormalizedResponse", byCohort);   // fan-in 15, dropped by a cohort threshold
        Assert.DoesNotContain("ModelDescription", byCohort);     // fan-in 5

        // And the right knob has none: a fan-in floor of 18 should leave two contracts
        // standing. All four survive.
        var byFanIn = ChangeCostSubjects(new Options { MinFanIn = 18 });

        Assert.Equal(
            new[] { "ModelDescription", "NormalizationContext", "NormalizedResponse", "RawResponse" },
            byFanIn.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// BUG BLAST RADIUS cannot fire in a cohort smaller than ten, whatever the metrics are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Percentile</c> is midrank: <c>100 * (below + 0.5 * equal) / n</c>. A unique maximum
    /// therefore scores <c>(n - 0.5)/n * 100</c> — 90.0 at n=5, 94.44 at n=9, 95.0 at n=10. The
    /// finding requires <c>FanInPctl &gt;= 95</c>, so every cohort of five to nine members is
    /// structurally incapable of producing it. Ties at the top score lower still.
    /// </para>
    /// <para>
    /// <c>--min-cohort</c> admits cohorts of five. Four of the fixture's cohorts sit in the dead
    /// band, and no value of fan-in, complexity or the threshold constants rescues them: the
    /// ceiling is arithmetic, not tuning. This is why the finding nominated nothing here for so
    /// long, and why the plant needed a twelve-member cohort rather than a more extreme type.
    /// </para>
    /// <para>
    /// It is also the inverse of the review question that caught the original cry-wolf failure.
    /// "Can this fire on 100% of a category?" has a twin — "can this fire at all?" — and nothing
    /// was asking it.
    /// </para>
    /// <para>
    /// Not superseded by any requirement yet. <c>TECHREQ-job-b.md</c> §5 converts absolute gates
    /// to percentiles; this is the opposite direction and needs its own answer, because a
    /// percentile floor above <c>(n-0.5)/n</c> is unsatisfiable rather than merely strict.
    /// </para>
    /// </remarks>
    [Fact]
    public void Blast_radius_is_unreachable_in_a_cohort_below_ten()
    {
        // The ceiling, computed the way the probe computes it.
        static double MaxAchievablePctl(int cohortSize) => 100.0 * (cohortSize - 0.5) / cohortSize;

        Assert.True(MaxAchievablePctl(5) < 95);    // 90.00
        Assert.True(MaxAchievablePctl(9) < 95);    // 94.44
        Assert.True(MaxAchievablePctl(10) >= 95);  // 95.00 — the first cohort size that can

        // And the fixture agrees: no member of any cohort below ten reaches 95, including the
        // ones that are the clear maximum of their group.
        var stranded = run.Result.Types
            .Where(t => t.CohortSize >= 5 && t.CohortSize < 10)
            .ToList();

        Assert.NotEmpty(stranded);
        Assert.All(stranded, t => Assert.True(t.FanInPctl < 95));
    }

    /// <summary>
    /// Two types with the same fully-qualified name in two assemblies merge into one row, and
    /// their metrics are summed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Model.cs</c> states the reasoning in its own comment — "types are keyed by fully-
    /// qualified name so partials across multiple files aggregate into a single row" — and it
    /// is correct for partials within one compilation. Across compilations it is wrong: .NET
    /// permits the same FQN in two assemblies and plugin architectures use it deliberately.
    /// </para>
    /// <para>
    /// The fixture plants the case in <c>Data</c> and <c>Tools</c>, which do not reference each
    /// other. Confirmed on nopCommerce, where the merge also fabricated a five-project circular
    /// reference — a shipping Job A finding computed on conflated numbers
    /// (<c>SPIKE-job-a-prior-art.md</c> §7.5).
    /// </para>
    /// <para>
    /// Superseded by <c>TECHREQ-job-b.md</c> §8 criterion 8, the one carve-out from the
    /// byte-identical rule. When Core keys on <c>(assembly, FQN)</c> this test fails and is
    /// deleted deliberately — and the goldens move, which is the point of planting it.
    /// <c>SubjectRef.ForType</c> already implements the key that supersedes this.
    /// </para>
    /// </remarks>
    [Fact]
    public void Two_types_sharing_a_name_across_assemblies_merge_into_one_row()
    {
        var tags = run.Result.Types.Where(t => t.Name == "PayloadTag").ToList();

        // Two declarations, two assemblies. One row.
        Assert.Single(tags);

        var merged = tags[0];

        // Data's declaration carries 2 members (Label, Describe); Tools' carries 4 (_weights,
        // Priority, Score, Weight). Neither type has six of anything.
        Assert.Equal(6, merged.MemberCount);

        // And one declaration's identity is simply lost — the surviving row attributes the
        // whole thing to Tools, so Data's copy is invisible and its project is under-counted.
        Assert.Equal("Tools", merged.Project);
        Assert.EndsWith("Tools/PayloadTag.cs", merged.File.Replace('\\', '/'), StringComparison.Ordinal);
    }

    /// <summary>
    /// The contracts nominated under CHANGE COST when the fixture is rendered with
    /// <paramref name="policy"/>. Reads report text because the threshold is a literal inside
    /// <c>PrintNominations</c> and there is no model surface to assert against — see
    /// <see cref="NominationText"/>. That absence is the defect.
    /// </summary>
    private string[] ChangeCostSubjects(Options policy) =>
        NominationText.SubjectsUnder(
            NominationText.Render(run.Result, policy), "-- CHANGE COST");
}

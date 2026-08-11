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
    /// A method-level concealed decision does not suppress breaks alone on its declaring type,
    /// so the report contradicts itself about one component.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The suppression captures type-level nominations only — <c>concealed.Select(t =&gt; t.Id)</c>
    /// in <c>PrintNominations</c>, where <c>concealed</c> is the §3.2 list. §3.3 nominates the
    /// same signal on methods, and §3.3 is the <b>primary</b> of the two: type-level came back
    /// empty on real code while method-level found the right thing, because a type whose total
    /// complexity is ordinary can still hide one 47-branch method.
    /// </para>
    /// <para>
    /// So the case that matters most is the case the suppression misses. On the fixture,
    /// <c>MethodReconciler</c> is nominated at method level and then told it breaks alone —
    /// "this method is making business judgements" and "if it breaks, it breaks alone", about
    /// one type, in one report. That is exactly the contradiction invariant 3 exists to prevent.
    /// </para>
    /// <para>
    /// <c>RateReconciler</c> is the contrast: nominated at BOTH levels, so the type-level
    /// suppression catches it and breaks alone stays quiet. The two differ only in whether the
    /// type-level nomination happened to fire, which is not a difference a user would accept as
    /// meaningful.
    /// </para>
    /// <para>
    /// Superseded by <c>TECHREQ-job-b.md</c> §4 row 2, amended to read "at type level (§3.2) or
    /// on any of its methods (§3.3)". <c>SubjectRef</c> walks member → declaring type for this.
    /// Fix in Core during extraction; deleting this test is the event worth seeing.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_method_level_concealed_decision_does_not_suppress_breaks_alone()
    {
        var text = NominationText.Render(run.Result, run.Options);

        var breaksAlone = NominationText.SubjectsUnder(text, "-- BREAKS ALONE");
        var atMethodLevel = NominationText
            .SubjectsUnder(text, "-- CONCEALED DECISION, METHOD LEVEL")
            .Select(s => s.Split('.')[0])
            .ToArray();

        // Nominated as concealing a decision, in one of its methods.
        Assert.Contains("MethodReconciler", atMethodLevel);

        // And told it breaks alone anyway. Both sentences, one component, one report.
        Assert.Contains("MethodReconciler", breaksAlone);

        // The contrast: RateReconciler is nominated at method level too, but ALSO at type
        // level, and the type-level nomination is the only one the suppression can see.
        Assert.Contains("RateReconciler", atMethodLevel);
        Assert.DoesNotContain("RateReconciler", breaksAlone);
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
    /// WIDEST CONTRACT SURFACE can never be suppressed, at any number of boundaries. Its filter
    /// and its suppression threshold are the same number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 5 says the section is suppressed when the qualifying set exceeds half the boundaries,
    /// because a list that long is not discriminating and is therefore noise. The implementation:
    /// </para>
    /// <code>
    /// var bigSurface = boundaries
    ///     .Where(t =&gt; t.DataShape &gt;= Math.Max(surfaceMedian * 1.5, 1))
    ///     .OrderByDescending(t =&gt; t.DataShape).Take(5).ToList();
    /// if (bigSurface.Count &gt; 0 &amp;&amp; bigSurface.Count &lt;= Math.Max(1, boundaries.Count / 2))
    /// </code>
    /// <para>
    /// A value at or above 1.5x a positive median is strictly above that median, so every
    /// qualifying boundary comes from the upper half of the distribution and the qualifying set
    /// can never hold more than <c>floor(n / 2)</c> members. When the median is zero the
    /// threshold falls back to 1, and the members at or below the median are all zero, so the
    /// bound is the same or tighter. Either way the count tops out at exactly the value it is
    /// required to EXCEED. It lands on the boundary at every n and never crosses it.
    /// </para>
    /// <para>
    /// The <c>Take(5)</c> is a second, independent ceiling that bites from ten boundaries up,
    /// but it is not what makes this unreachable — removing it changes nothing, because the
    /// median-relative filter has already capped the set at half.
    /// </para>
    /// <para>
    /// So this is the third finding in the fixture whose real question is "can this fire at
    /// all?", after BUG BLAST RADIUS below a cohort of ten and the layer-span examples list. It
    /// is also the one that most deserved the question: the rule exists to protect readers of
    /// LARGE codebases from an undiscriminating list, and it has never run anywhere.
    /// </para>
    /// <para>
    /// Not superseded by any requirement yet, and unlike the other rows this one cannot be fixed
    /// by moving a constant — a proportional suppression cannot sit on top of a filter that is
    /// itself proportional to the same distribution. The gate has to be expressed against
    /// something the filter does not already bound: an absolute surface floor, or a dispersion
    /// test that asks whether the top of the distribution is actually separated from the middle.
    /// </para>
    /// </remarks>
    [Fact]
    public void Widest_contract_surface_can_never_be_suppressed()
    {
        // The probe's own arithmetic, reproduced so the claim is about the rule and not about
        // one fixture. Median matches Report.Median; the rest is PrintBoundaries.
        static double Median(double[] v)
        {
            var s = (double[])v.Clone();
            Array.Sort(s);
            var mid = s.Length / 2;
            return s.Length % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2.0;
        }

        static bool Suppressed(double[] shapes)
        {
            var qualifying = Math.Min(shapes.Count(s => s >= Math.Max(Median(shapes) * 1.5, 1)), 5);
            return qualifying > 0 && qualifying > Math.Max(1, shapes.Length / 2);
        }

        // The fixture's own nine boundaries: one qualifies against a ceiling of four.
        var fixtureShapes = run.Result.Types
            .Where(t => t.Kind is "ApiBoundary" or "ExternalCall")
            .Select(t => (double)t.DataShape)
            .ToArray();

        Assert.Equal(9, fixtureShapes.Length);
        Assert.False(Suppressed(fixtureShapes));

        // And the distributions that MAXIMISE the qualifying set — half the boundaries at zero,
        // half as wide as you like. Every one lands exactly on the threshold and none crosses
        // it, which is the signature of a gate measured against its own filter.
        Assert.False(Suppressed([0, 100]));
        Assert.False(Suppressed([0, 0, 100, 100]));
        Assert.False(Suppressed([0, 0, 0, 0, 0, 100, 100, 100, 100]));
        Assert.False(Suppressed([0, 0, 0, 0, 0, 100, 100, 100, 100, 100]));
        Assert.False(Suppressed([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 100, 100]));

        // Including when the spread is extreme rather than binary.
        Assert.False(Suppressed([1, 2, 3, 4, 5, 600, 700, 800, 900]));
    }

    /// <summary>
    /// The layer-span roll-call collapse groups by signature, so boilerplate arriving in a group
    /// silences the one anomaly in it — and the examples it keeps are chosen by fan-in, which
    /// selects for boilerplate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 4 is right that six near-identical blocks are a layering pattern rather than six
    /// discoveries. What it assumes is that a shared signature means a shared phenomenon, and
    /// that does not hold. Four Get controllers span ApiBoundary+DataAccess+ExternalCall because
    /// they are wired to a store and a gateway, which is boilerplate. AuthenticationMiddleware
    /// carries the identical signature because it reaches into customer lookup and an audit
    /// service, which is the section's own worked example of a component whose name has stopped
    /// describing it — "a gateway policy engine wearing an auth name", per PrintLayerSpan's
    /// summary.
    /// </para>
    /// <para>
    /// Same signature, opposite meanings, and the collapse cannot tell them apart. The anomaly
    /// keeps its name in the examples list and loses the detail block that made it actionable —
    /// the kinds it reaches, the types it reaches them through, and the instruction to check
    /// whether the name still fits.
    /// </para>
    /// <para>
    /// Two things make it worse. The examples are ordered
    /// <c>OrderByDescending(x =&gt; x.Type.FanIn)</c> and cut at four, and on this fixture five of
    /// the six members tie at fan-in 0 — so which four names survive is settled by enumeration
    /// order and nothing else. The anomaly is named here by position, not because it earned a
    /// place, and one more boilerplate controller would displace it on a coin toss. That is the
    /// same hazard as defect 6, arriving in report content rather than in layout.
    /// </para>
    /// <para>
    /// And the collapse is triggered by population, so the more boilerplate a codebase contains
    /// the more reliably its real finding is hidden. On either solution in the spike this branch
    /// would fire every time.
    /// </para>
    /// <para>
    /// Not superseded by any requirement yet. §4 row 4 needs to say what makes members of a
    /// signature group equivalent, or the collapse needs to keep anomalies out of the count —
    /// and the examples need an ordering that is not the inverse of interestingness.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_layer_span_collapse_hides_the_anomaly_it_shares_a_signature_with()
    {
        var text = NominationText.Render(run.Result, run.Options);

        // The four boilerplate controllers and the middleware are one group, and the group
        // collapsed.
        Assert.Contains("a layering pattern rather than an", text, StringComparison.Ordinal);

        // The middleware's own detail — the reason the section exists — is not in the report.
        Assert.DoesNotContain("Check that the name still describes what it does", text, StringComparison.Ordinal);

        // It is a genuine anomaly and not more boilerplate: it is the only member of the group
        // reaching all three kinds through a mix of its own role and its dependencies, and the
        // only one whose name describes a single narrow concern.
        var middleware = run.Result.Types.Single(t => t.Name == "AuthenticationMiddleware");
        Assert.Equal("ApiBoundary+DataAccess+ExternalCall", middleware.KindSpan);

        // The ordering that picks which four to name is by fan-in, and five of the six tie at
        // zero — so membership of the examples list is decided by enumeration order. The anomaly
        // appears in it by position rather than on merit, and one more boilerplate controller
        // would evict it without any threshold changing.
        var group = run.Result.Types
            .Where(t => t.KindSpan == "ApiBoundary+DataAccess+ExternalCall")
            .ToList();

        Assert.Equal(6, group.Count);
        Assert.Equal(0, middleware.FanIn);
        Assert.Equal(5, group.Count(t => t.FanIn == 0));
    }

    /// <summary>
    /// The cohort floor strips a suppression it was never meant to touch, so lowering a
    /// threshold <b>removes</b> a contradictory claim instead of adding claims.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 7 suppresses cohort-relative findings below <c>--min-cohort</c>, which is right: three
    /// peers is not a distribution. But BREAKS ALONE is not cohort-relative — its heading says
    /// "no cohort required" — and it reads its row 2 suppression out of the concealed-decision
    /// list, which <i>is</i> cohort-gated:
    /// </para>
    /// <code>
    /// var concealedIds = new HashSet&lt;string&gt;(concealed.Select(t =&gt; t.Id), ...);  // from `eligible`
    /// var breaksAlone = result.Types                                             // NOT from `eligible`
    ///     .Where(t =&gt; !concealedIds.Contains(t.Id))
    /// </code>
    /// <para>
    /// So a small peer group drops a type out of concealed decision, out of <c>concealedIds</c>,
    /// and straight into breaks alone. Suppressing one finding switched another one on, and the
    /// report tells the reader that a type it cannot characterise is safe to change.
    /// </para>
    /// <para>
    /// This is invariant 3 again, but not the path <c>TECHREQ-job-b.md</c> §4 row 2 was amended
    /// to cover: that amendment was about type level versus method level, and this is about
    /// eligibility. Both share one cause — suppression is ordering inside a renderer rather than
    /// a declared relationship between findings, so it can only see what has already been
    /// computed by the time it runs.
    /// </para>
    /// <para>
    /// Not superseded by any requirement yet. §4 needs a row saying a suppression may not be
    /// weakened by the gate on the finding that supplies it; the fix in Core is that suppression
    /// is evaluated over findings, not over a filtered list a renderer happened to build first.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_cohort_floor_strips_the_concealed_decision_suppression_from_breaks_alone()
    {
        // At defaults its cohort of three is below the floor, so it is not a concealed decision
        // and nothing suppresses breaks alone.
        Assert.DoesNotContain("RoutingDepot", ConcealedSubjects(run.Options));
        Assert.Contains("RoutingDepot", BreaksAlone(run.Options));

        // Drop the floor under that same cohort and the type is nominated as a concealed
        // decision — at which point the suppression it should always have had starts working
        // and breaks alone goes quiet. Same type, same metrics, one threshold.
        var floorAtThree = new Options { MinCohort = 3 };

        Assert.Contains("RoutingDepot", ConcealedSubjects(floorAtThree));
        Assert.DoesNotContain("RoutingDepot", BreaksAlone(floorAtThree));
    }

    private string[] BreaksAlone(Options policy) =>
        NominationText.SubjectsUnder(
            NominationText.Render(run.Result, policy), "-- BREAKS ALONE");

    private string[] ConcealedSubjects(Options policy) =>
        NominationText.SubjectsUnder(
                NominationText.Render(run.Result, policy), "-- CONCEALED DECISION -")
            .Select(s => s.Split('.')[0])
            .ToArray();

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

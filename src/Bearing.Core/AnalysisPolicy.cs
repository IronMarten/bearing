namespace IronMarten.Bearing;

/// <summary>
/// Every threshold that decides whether a finding fires, named and in one place.
/// </summary>
/// <remarks>
/// <para>
/// These numbers encode judgement. Several were arrived at by watching one specific false
/// positive, and a team has to be able to see which policy produced a finding — so changing one
/// must be a reviewable event rather than a silent behaviour change between releases.
/// </para>
/// <para>
/// <b>Ten of these were already flags. Thirteen were literals inside conditions</b> — not a
/// flag, not a constant, not reviewable, and in several cases not covered by any test. A policy
/// object carrying ten of twenty-three values misrepresents which policy produced a finding,
/// which is the exact failure it exists to prevent. All of them are here, or this type is
/// decorative.
/// </para>
/// <para>
/// Two are relations rather than constants — the roll-call threshold scales with
/// <see cref="Top"/>, and the surface-discrimination ceiling scales with the number of
/// boundaries in the solution. They are exposed as the divisor that can be tuned plus a method
/// that applies it, so the knob is nameable without pretending the gate is a fixed number.
/// </para>
/// <para>
/// The drift section carries two further literals — a global-percentile arrival floor and a
/// movement delta. They are deliberately absent: drift is paid-tier, and it gets its own policy
/// when it ships rather than being half-represented here.
/// </para>
/// </remarks>
public sealed record AnalysisPolicy
{
    /// <summary>The policy schema version. Bumped when a value is added, removed or renamed.</summary>
    public const string Version = "1";

    /// <summary>The defaults every measurement in the repository was taken against.</summary>
    public static AnalysisPolicy Default { get; } = new();

    // ------------------------------------------------------------ peer groups ----

    /// <summary>Below this many peers, a cohort's percentiles are not meaningful.</summary>
    /// <remarks>
    /// <b>Gating only, since X3.</b> This decides whether a cohort supports a comparative claim.
    /// It used to decide <see cref="CohortBasisFloor"/> as well, which is why
    /// <c>DEFECTS.md</c> §10 had no local repair: one number was answering two questions that pull
    /// in opposite directions.
    /// </remarks>
    public int MinCohort { get; init; } = 5;

    /// <summary>
    /// The fewest candidates a basis may have and still be chosen to compare a type against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Specificity, not sufficiency</b> — <see cref="Cohorts.Assign"/> takes the most specific
    /// basis whose candidate count clears this, so <b>lowering it makes cohorts finer</b>, which is
    /// the opposite direction from <see cref="MinCohort"/>. Measured on Jellyfin: moving the single
    /// combined value from 5 to 3 re-based <b>155 of 1,502 types</b>, so any change to the gate was
    /// silently a change to every percentile in the report.
    /// </para>
    /// <para>
    /// <b>Split from <see cref="MinCohort"/> at the same default</b>, so the report is
    /// byte-identical the day this lands and the two can then move independently.
    /// <c>ARCHITECTURE.md</c> §11 carries the decision and the measurements.
    /// </para>
    /// </remarks>
    public int CohortBasisFloor { get; init; } = 5;

    /// <summary>The "x times the peer median" bar an outlier has to clear.</summary>
    public double OutlierFactor { get; init; } = 3.0;

    /// <summary>
    /// The absolute floor beside every normalized measure: how many callers "widely depended
    /// on" has to mean before a percentile is allowed to say it.
    /// </summary>
    public int MinFanIn { get; init; } = 5;

    /// <summary>Display cap per message.</summary>
    public int Top { get; init; } = 15;

    // ------------------------------------------------------------- complexity ----

    /// <summary>McCabe's conventional "worth a look" threshold.</summary>
    public int HighCc { get; init; } = 10;

    /// <summary>Below this, "concealed decision" contradicts itself — there is no decision.</summary>
    public int MinDecisionCc { get; init; } = 5;

    /// <summary>
    /// How far down its peer group a method may sit and still be a concealed decision: rank 1 is
    /// the cohort's most complex method, and this admits that one and the next two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fixed rank, deliberately not a fraction.</b> <see cref="BlastTopFraction"/> and
    /// <see cref="ChangeCostTopFraction"/> read a proportion of their population through
    /// <see cref="Distribution.TopRankLimit"/>, and that is right for them because their
    /// population is the whole solution and a proportion of it is the claim. Here the population
    /// is one cohort, cohorts differ in size by three orders of magnitude, and a proportion of
    /// the largest is not a short list: on nopCommerce the <c>suffix:Service</c> cohort holds
    /// 2,909 methods, so a 1% limit admits 30 of them and a 5% limit admits 142.
    /// </para>
    /// <para>
    /// A fixed rank also bounds the output by the <i>taxonomy</i> rather than by the codebase —
    /// nominations land at roughly this many per qualifying cohort however large the solution is,
    /// where a proportion grows with it. That is the whole reason this replaced a ratio: see
    /// <c>MEASURE-concealed-decision.md</c>.
    /// </para>
    /// </remarks>
    public int ConcealedTopRank { get; init; } = 3;

    /// <summary>
    /// The share of a cohort that may be nominated as a concealed decision, capped by
    /// <see cref="ConcealedTopRank"/>. Binds only below that cap, so it tightens small cohorts
    /// and leaves large ones exactly as they were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The paragraph above is right that a proportion cannot do this job alone, and this does
    /// not ask it to.</b> Measured 2026-08-25 on three solutions: a bare 5% limit gives
    /// nopCommerce 423 method nominations against 103 today, 10% gives 814 and 20% gives 1,243 —
    /// exactly the failure <see cref="ConcealedTopRank"/> was introduced to avoid. The effective
    /// limit is <c>min(ConcealedTopRank, TopRankLimit(ConcealedTopShare))</c>, so the fixed rank
    /// still bounds the output by the taxonomy and the share only ever makes it smaller.
    /// </para>
    /// <para>
    /// <b>What it fixes is the other end, which the fixed rank alone gets wrong.</b> A top-3 of a
    /// cohort of five is 60% of that cohort and a top-3 of five hundred is 0.6% — one constant
    /// meaning two different things, which is <c>docs/DEFECTS.md</c> §10's complaint. At this
    /// value a cohort of five admits one and a cohort of twenty-five or more admits three.
    /// </para>
    /// <para>
    /// <b>It is not here because the thin end was dangerous, and that is worth recording because
    /// it is the obvious reason to assume.</b> Below-floor cohorts hold 0.6%, 1.4% and 0.6% of
    /// the gated population on the three solutions, so letting them nominate unguarded adds 12,
    /// 11 and 53 findings. §10's pattern: a real inconsistency with almost no field incidence.
    /// The share is taken because selectivity should not depend on cohort size, not because the
    /// output was wrong without it. <c>ARCHITECTURE.md</c> §11.
    /// </para>
    /// </remarks>
    public double ConcealedTopShare { get; init; } = 0.20;

    /// <summary>
    /// How many median absolute deviations above its peer median a value must sit to be an
    /// outlier among them. Where the peers have no spread at all, above the median is enough.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the gate <see cref="OutlierFactor"/> used to be, asking a question a multiple of
    /// the median cannot.</b> A ratio asks <i>how many times its peers is it</i>, and on a cohort
    /// whose median is 1 that evaluates to the value — <c>3x median</c> becomes <c>cc &gt;= 3</c>,
    /// which is <c>docs/DEFECTS.md</c> §2. It also fails in the other direction, and that is the
    /// half this was built for: a method at <c>cc</c> 12 in a cohort whose median is 10.5 is the
    /// top of its group and is not remarkable, and <b>rank alone cannot tell the two apart because
    /// rank is ordinal</b>. <c>TestBed</c>'s planted evaluators are exactly that case — three of
    /// them, at 1.14x their peers — and P0 planted them as <i>complex code that is not
    /// anomalous</i>.
    /// </para>
    /// <para>
    /// <b>The <c>MAD = 0</c> branch is not a special case, it is the other half of the claim.</b>
    /// A group with no spread supports no statement about gaps, so the sentence stops being a
    /// multiple and becomes a count — <i>"the only complexity among the 6 types whose name ends in
    /// Trait"</i>, which <c>Claims.ConcealedType</c> already renders. Letting everything above the
    /// median through there is <c>ARCHITECTURE.md</c> §11's trap; what stops it is
    /// <see cref="ConcealedTopShare"/> bounding the group to its top few, not a constant here.
    /// </para>
    /// <para>
    /// <b>Measured 2026-08-25 on three solutions, member level, against 103 / 167 / 366 today:</b>
    /// this gate with the rank limit beside it gives <b>103 / 160 / 366</b>. The value is 3 for
    /// the ordinary reason — it is the same k the ratio used — and it was not tuned to reproduce
    /// that: k of 1 and 2 give 107 / 168 / 396 and 104 / 166 / 385.
    /// </para>
    /// </remarks>
    public double ConcealedDispersionFactor { get; init; } = 3.0;

    /// <summary>
    /// How far down the boundary population a type may sit and still be said to carry real logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gate this joined was absolute and did not discriminate.</b> <c>MaxMemberCyclomatic
    /// &gt;= HighCc</c> on its own fires on <b>19.5% of nopCommerce's 672 boundaries and 33.3% of
    /// jellyfin's 174</b> — and a claim made about a third of the population it filters is
    /// describing that population rather than finding an anomaly in it. The same constant also
    /// means different things per solution: <c>HighCc = 10</c> is 5x the boundary median on
    /// nopCommerce and 2x on jellyfin, and nothing in the output said so.
    /// <c>MEASURE-concealed-decision.md</c> §10.
    /// </para>
    /// <para>
    /// <b>The rule it broke is written twelve lines below it in its own file.</b>
    /// <see cref="BoundaryMarking.WidestSurfaces"/> carries the sibling principle — a section
    /// prints only when it discriminates — and it was articulated for that finding and never
    /// applied to this one.
    /// </para>
    /// <para>
    /// <b>A fraction rather than a fixed rank, because the population is the boundaries and a
    /// proportion of them is the claim</b> — the same reading <see cref="BlastTopFraction"/> and
    /// <see cref="ChangeCostTopFraction"/> take, and the opposite of
    /// <see cref="ConcealedTopRank"/>, whose population is one cohort of wildly varying size.
    /// Measured at this value: <b>34 of nopCommerce's boundaries and 9 of jellyfin's</b>, against
    /// 131 and 58.
    /// </para>
    /// <para>
    /// <b>The absolute floor stays and is not redundant.</b> Rank alone would nominate the top 5%
    /// of boundaries however tame they are — <c>docs/ARCHITECTURE.md</c> §9's gate that cannot
    /// fail. A solution whose boundaries are all thin still reports nothing here, which is the
    /// answer rather than an empty section.
    /// </para>
    /// </remarks>
    public double BoundaryTopFraction { get; init; } = 0.05;

    // ------------------------------------------------------------------ shape ----

    /// <summary>Fan-in and fan-out both at or above this is a hub.</summary>
    public int HubMin { get; init; } = 5;

    /// <summary>Where a hub reads as a god object rather than as wiring.</summary>
    public int GodObjectMembers { get; init; } = 20;

    /// <summary>Architectural kinds a component must reach across to be spanning.</summary>
    public int MinKindSpan { get; init; } = 3;

    // ------------------------------------------------------------- instability ----

    /// <summary>
    /// At or below this instability, a type is load-bearing: much depends on it, it depends on
    /// little.
    /// </summary>
    public double StableThreshold { get; init; } = 0.2;

    /// <summary>
    /// At or above this instability, a type is isolated — if it breaks, it breaks alone.
    /// </summary>
    /// <remarks>
    /// <b>Independent of <see cref="StableThreshold"/>, deliberately.</b> The defaults are 0.2
    /// and 0.8, and the symmetry is a coincidence that is not maintained: these gate different
    /// findings about different populations, and someone loosening what counts as *stable* has
    /// no reason to also loosen what counts as *isolated*. Deriving one from the other would
    /// make a single knob move two findings, which is the shape of the change-cost defect —
    /// two values that agree at defaults, only one of which is reachable.
    /// </remarks>
    public double IsolatedThreshold { get; init; } = 0.8;

    /// <summary>
    /// Minimum callers for "breaks alone" to apply. A fan-in of zero is not reassurance, it is
    /// unreferenced code — a different finding.
    /// </summary>
    public int BreaksAloneMinFanIn { get; init; } = 1;

    // ----------------------------------------------- concealed decision, shape ----

    /// <summary>
    /// Upper bound on fan-in relative to the peer median for a type to read as "mapper-shaped".
    /// </summary>
    /// <remarks>
    /// A ratio rather than a percentile on purpose: in a tied cohort a fan-out of 5 against
    /// peers of 4 lands at the 93rd percentile while being, in substance, identical.
    /// </remarks>
    public double ConcealedFanInCeiling { get; init; } = 2.0;

    /// <summary>Upper bound on fan-out relative to the peer median. See
    /// <see cref="ConcealedFanInCeiling"/>.</summary>
    public double ConcealedFanOutCeiling { get; init; } = 2.0;

    // ------------------------------------------------------------ blast radius ----

    /// <summary>Fan-in relative to the peer median for a bug's blast radius to be wide.</summary>
    public double BlastFanInMultiple { get; init; } = 2.0;

    /// <summary>
    /// The share of a cohort, by fan-in, that counts as "the top" for blast radius.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the probe's <c>FanInPctl &gt;= 95</c>, which was <b>unsatisfiable in any cohort
    /// smaller than ten</b> while <see cref="MinCohort"/> admitted five — a gate unreachable by
    /// arithmetic rather than by tuning. <c>docs/DEFECTS.md</c> §14.
    /// </para>
    /// <para>
    /// <b>It stays a fraction rather than becoming a count</b>, and that is the part worth
    /// defending. A percentile-within-cohort gate self-limits by construction: blast radius
    /// nominated 1.0% and 0.9% of types on two unrelated real solutions, holding steady where
    /// the absolute-gated findings ran to 4–7% of everything. That stability is the finding's
    /// best evidence. A fixed "top N per cohort" would be reachable at every size and would
    /// throw it away — starving on large cohorts and saturating on small ones.
    /// </para>
    /// <para>
    /// Read through <see cref="Distribution.TopRankLimit"/>, which is where the floor of 1 that
    /// actually repairs the defect lives.
    /// </para>
    /// </remarks>
    public double BlastTopFraction { get; init; } = 0.05;

    /// <summary>Complexity percentile within the cohort for blast radius.</summary>
    public double BlastComplexityPercentile { get; init; } = 70;

    // ------------------------------------------------------------- change cost ----

    /// <summary>
    /// The share of the <b>whole solution</b>, by fan-in, that counts as "the top" for change
    /// cost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Solution-wide, where <see cref="BlastTopFraction"/> is per-cohort</b>, and the
    /// difference is the question each finding asks. Blast radius asks how far a defect
    /// propagates compared with a type's peers. This asks which part of the <i>application</i> is
    /// most expensive to change, which is not a question about peers — and §3.5 is explicit that
    /// the finding runs over all types with no cohort gate, so a lone contract with thirty callers
    /// is not silenced for having none.
    /// </para>
    /// <para>
    /// <b>Where 0.05 comes from, since it should not be taken on faith.</b> It is not tuned to the
    /// fixture. The two measured solutions put the eligible population — <c>Contract</c> plus
    /// <c>ApiBoundary</c> — at 13–30% of all types, and blast radius demonstrates that a
    /// proportional gate at 0.05 lands a finding near 1% of a codebase, which is the rate that
    /// held still across both. A share of the solution rather than of the eligible set is what
    /// keeps that true when one codebase is 20.3% controllers and another 8.5%.
    /// </para>
    /// <para>
    /// <b>And it is deliberately not load-bearing, which is checkable rather than hoped.</b> On
    /// the fixture the nominated set is <i>identical</i> at 0.05, 0.10 and 0.15 — a threefold
    /// range — because the population has a gap between fan-in 15 and fan-in 5 and the gate falls
    /// in it. Only below 0.02 does it move. A constant whose output is stable across the range it
    /// would plausibly be tuned within is a constant that is not deciding much, and
    /// <c>FindingEquivalenceTests</c> asserts that rather than leaving it as a claim.
    /// </para>
    /// <para>
    /// <b>Unvalidated on real code, and recorded as such.</b> The two-solution run measured the
    /// old absolute gate, not this one, and re-running solutions is out of scope
    /// (<c>NEXT-SESSION.md</c>). What this value is owed is the backtest — <c>TASKS.md</c> Z3.
    /// </para>
    /// </remarks>
    public double ChangeCostTopFraction { get; init; } = 0.05;

    // ---------------------------------------------------------- roll-call cap ----

    /// <summary>
    /// Divides <see cref="Top"/> to give the group size above which per-item detail collapses
    /// into a single pattern line. See <see cref="RollCallThreshold"/>.
    /// </summary>
    public int RollCallDivisor { get; init; } = 3;

    /// <summary>
    /// Group size above which detail collapses to one line — repeated across many types it is a
    /// pattern, not a list of findings.
    /// </summary>
    public int RollCallThreshold => Top / RollCallDivisor;

    // ------------------------------------------------------- contract surface ----

    /// <summary>Multiple of the median boundary surface above which a contract is unusually wide.</summary>
    public double SurfaceOutlierMultiple { get; init; } = 1.5;

    /// <summary>
    /// Absolute floor beneath <see cref="SurfaceOutlierMultiple"/>, so a solution whose
    /// boundaries all measure zero does not make every boundary an outlier.
    /// </summary>
    public double SurfaceOutlierFloor { get; init; } = 1;

    /// <summary>
    /// The largest set of contract surfaces that can be named before the section stops
    /// discriminating and becomes a list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An absolute count, replacing the proportional ceiling, and that is
    /// <c>docs/DEFECTS.md</c> §12's whole point.</b> The probe suppresses when the qualifying set
    /// exceeds half the boundaries, and the qualifying filter is
    /// <see cref="SurfaceOutlierMultiple"/> against the median of the same distribution — so the
    /// set is already bounded by the share the ceiling tests for. It lands on the threshold at
    /// every boundary count and never crosses. A gate phrased as "too large a share" cannot sit on
    /// a filter proportional to the same statistic, and no value of a divisor repairs that.
    /// </para>
    /// <para>
    /// What actually goes wrong is not a proportion. The section promises to name what stands out
    /// and instead reads a list, and a count is what bounds a list. Five is the number the probe's
    /// <c>Take(5)</c> already imposed — so this changes a silent truncation into the gate itself:
    /// past the ceiling the section says nothing rather than naming an arbitrary five of the
    /// qualifiers.
    /// </para>
    /// </remarks>
    public int MaxNamedSurfaces { get; init; } = 5;

    // ------------------------------------------------------------- no cohort ----

    /// <summary>Solution-wide fan-in percentile for a type with no peer group.</summary>
    public double GlobalFanInPercentile { get; init; } = 90;

    /// <summary>Solution-wide complexity percentile for a type with no peer group.</summary>
    public double GlobalComplexityPercentile { get; init; } = 90;

    /// <summary>
    /// Absolute floor beside <see cref="GlobalComplexityPercentile"/>, applied
    /// <b>exclusively</b>: complexity must be strictly greater than this.
    /// </summary>
    /// <remarks>
    /// In a codebase where most types have no branching at all, a max-member complexity of 1
    /// lands at a high midrank percentile — "top 86% by complexity, cc 1" is both absurd and
    /// corrosive. The floor is what stops the percentile speaking on its own.
    /// </remarks>
    public int GlobalComplexityFloor { get; init; } = 1;

    // ----------------------------------------------------------------- graphs ----

    /// <summary>Smallest type tangle worth reporting; mutual pairs and triples are ordinary C#.</summary>
    public int MinTangle { get; init; } = 4;

    /// <summary>The minimum surface a boundary needs to count as unusually wide.</summary>
    public double SurfaceOutlierThreshold(double medianSurface) =>
        Math.Max(medianSurface * SurfaceOutlierMultiple, SurfaceOutlierFloor);

    /// <summary>
    /// Every value, in a stable order, for reporting which policy produced a finding.
    /// </summary>
    public IReadOnlyList<(string Name, double Value)> Values =>
    [
        (nameof(MinCohort), MinCohort),
        (nameof(CohortBasisFloor), CohortBasisFloor),
        (nameof(OutlierFactor), OutlierFactor),
        (nameof(MinFanIn), MinFanIn),
        (nameof(Top), Top),
        (nameof(HighCc), HighCc),
        (nameof(MinDecisionCc), MinDecisionCc),
        (nameof(ConcealedTopRank), ConcealedTopRank),
        (nameof(ConcealedTopShare), ConcealedTopShare),
        (nameof(ConcealedDispersionFactor), ConcealedDispersionFactor),
        (nameof(HubMin), HubMin),
        (nameof(GodObjectMembers), GodObjectMembers),
        (nameof(MinKindSpan), MinKindSpan),
        (nameof(StableThreshold), StableThreshold),
        (nameof(IsolatedThreshold), IsolatedThreshold),
        (nameof(BreaksAloneMinFanIn), BreaksAloneMinFanIn),
        (nameof(ConcealedFanInCeiling), ConcealedFanInCeiling),
        (nameof(ConcealedFanOutCeiling), ConcealedFanOutCeiling),
        (nameof(BlastFanInMultiple), BlastFanInMultiple),
        (nameof(BoundaryTopFraction), BoundaryTopFraction),
        (nameof(BlastTopFraction), BlastTopFraction),
        (nameof(BlastComplexityPercentile), BlastComplexityPercentile),
        (nameof(ChangeCostTopFraction), ChangeCostTopFraction),
        (nameof(RollCallDivisor), RollCallDivisor),
        (nameof(SurfaceOutlierMultiple), SurfaceOutlierMultiple),
        (nameof(SurfaceOutlierFloor), SurfaceOutlierFloor),
        (nameof(MaxNamedSurfaces), MaxNamedSurfaces),
        (nameof(GlobalFanInPercentile), GlobalFanInPercentile),
        (nameof(GlobalComplexityPercentile), GlobalComplexityPercentile),
        (nameof(GlobalComplexityFloor), GlobalComplexityFloor),
        (nameof(MinTangle), MinTangle),
    ];

    /// <summary>Whether this policy is unmodified from <see cref="Default"/>.</summary>
    public bool IsDefault => this == Default;

    /// <summary>
    /// The values that differ from <see cref="Default"/>, so a report can state what was tuned
    /// rather than reprinting twenty-five numbers nobody changed.
    /// </summary>
    public IReadOnlyList<(string Name, double Value, double DefaultValue)> Overrides
    {
        get
        {
            var defaults = Default.Values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
            return Values
                .Where(v => defaults[v.Name] != v.Value)
                .Select(v => (v.Name, v.Value, defaults[v.Name]))
                .ToList();
        }
    }

    /// <summary>
    /// Throws when a value could not have produced a meaningful finding.
    /// </summary>
    /// <remarks>
    /// Guards the arithmetic rather than the judgement: a percentile outside 0..100 or a
    /// divisor of zero cannot be what anyone meant, whereas an unusual-but-valid threshold is
    /// the user's call to make.
    /// </remarks>
    public void Validate()
    {
        foreach (var (name, value) in Values)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be a finite number.");
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, value, $"{name} cannot be negative.");
        }

        foreach (var (name, value) in new (string, double)[]
                 {
                     (nameof(BlastComplexityPercentile), BlastComplexityPercentile),
                     (nameof(GlobalFanInPercentile), GlobalFanInPercentile),
                     (nameof(GlobalComplexityPercentile), GlobalComplexityPercentile),
                 })
        {
            if (value > 100)
                throw new ArgumentOutOfRangeException(name, value, $"{name} is a percentile and must be within 0..100.");
        }

        // A share of a cohort, so 1.0 means "all of it" and anything above is not a share. The
        // upper bound matters more than it looks: this gate is the only thing keeping blast
        // radius self-limiting, and a fraction of 2 would silently turn it into a roll-call of
        // every type clearing the other three conditions.
        if (BoundaryTopFraction is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(BoundaryTopFraction), BoundaryTopFraction, "BoundaryTopFraction is a share of the boundary population and must be within 0..1.");

        if (BlastTopFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(BlastTopFraction), BlastTopFraction, "BlastTopFraction is a share of a cohort and must be within 0..1.");

        // Same reasoning one level up: a share of the solution above 1 turns the only
        // self-limiting gate change cost has back into the roll-call it was converted away from.
        if (ChangeCostTopFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(ChangeCostTopFraction), ChangeCostTopFraction, "ChangeCostTopFraction is a share of the solution and must be within 0..1.");

        if (RollCallDivisor < 1)
            throw new ArgumentOutOfRangeException(nameof(RollCallDivisor), RollCallDivisor, "RollCallDivisor must be at least 1.");
        // Zero would suppress the section wherever a single wide surface qualifies, which is the
        // finding never speaking rather than speaking too often.
        if (MaxNamedSurfaces < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxNamedSurfaces), MaxNamedSurfaces, "MaxNamedSurfaces must be at least 1.");
        if (MinCohort < 2)
            throw new ArgumentOutOfRangeException(nameof(MinCohort), MinCohort, "A cohort of fewer than two has no comparative reading at all.");

        if (CohortBasisFloor < 2)
            throw new ArgumentOutOfRangeException(nameof(CohortBasisFloor), CohortBasisFloor, "A basis yielding fewer than two candidates is not a peer group.");
    }
}

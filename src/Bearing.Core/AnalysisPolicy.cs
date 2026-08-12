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
    public int MinCohort { get; init; } = 5;

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
    /// Divides the boundary count to give the largest qualifying set that still discriminates.
    /// See <see cref="MaxDiscriminatingSurfaces"/>.
    /// </summary>
    public int SurfaceDiscriminationDivisor { get; init; } = 2;

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

    /// <summary>
    /// The largest qualifying set that still discriminates, for a solution with
    /// <paramref name="boundaryCount"/> boundaries. A set larger than this is a roll-call of the
    /// whole population rather than a finding about part of it.
    /// </summary>
    /// <remarks>
    /// Worth knowing before tuning: because the qualifying filter is itself proportional to the
    /// same distribution, the qualifying set can never <i>exceed</i> this number — it lands on
    /// it and never crosses. <c>docs/DEFECTS.md</c> §12.
    /// </remarks>
    public int MaxDiscriminatingSurfaces(int boundaryCount) =>
        Math.Max(1, boundaryCount / SurfaceDiscriminationDivisor);

    /// <summary>The minimum surface a boundary needs to count as unusually wide.</summary>
    public double SurfaceOutlierThreshold(double medianSurface) =>
        Math.Max(medianSurface * SurfaceOutlierMultiple, SurfaceOutlierFloor);

    /// <summary>
    /// Every value, in a stable order, for reporting which policy produced a finding.
    /// </summary>
    public IReadOnlyList<(string Name, double Value)> Values =>
    [
        (nameof(MinCohort), MinCohort),
        (nameof(OutlierFactor), OutlierFactor),
        (nameof(MinFanIn), MinFanIn),
        (nameof(Top), Top),
        (nameof(HighCc), HighCc),
        (nameof(MinDecisionCc), MinDecisionCc),
        (nameof(HubMin), HubMin),
        (nameof(GodObjectMembers), GodObjectMembers),
        (nameof(MinKindSpan), MinKindSpan),
        (nameof(StableThreshold), StableThreshold),
        (nameof(IsolatedThreshold), IsolatedThreshold),
        (nameof(BreaksAloneMinFanIn), BreaksAloneMinFanIn),
        (nameof(ConcealedFanInCeiling), ConcealedFanInCeiling),
        (nameof(ConcealedFanOutCeiling), ConcealedFanOutCeiling),
        (nameof(BlastFanInMultiple), BlastFanInMultiple),
        (nameof(BlastTopFraction), BlastTopFraction),
        (nameof(BlastComplexityPercentile), BlastComplexityPercentile),
        (nameof(RollCallDivisor), RollCallDivisor),
        (nameof(SurfaceOutlierMultiple), SurfaceOutlierMultiple),
        (nameof(SurfaceOutlierFloor), SurfaceOutlierFloor),
        (nameof(SurfaceDiscriminationDivisor), SurfaceDiscriminationDivisor),
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
        if (BlastTopFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(BlastTopFraction), BlastTopFraction, "BlastTopFraction is a share of a cohort and must be within 0..1.");

        if (RollCallDivisor < 1)
            throw new ArgumentOutOfRangeException(nameof(RollCallDivisor), RollCallDivisor, "RollCallDivisor must be at least 1.");
        if (SurfaceDiscriminationDivisor < 1)
            throw new ArgumentOutOfRangeException(nameof(SurfaceDiscriminationDivisor), SurfaceDiscriminationDivisor, "SurfaceDiscriminationDivisor must be at least 1.");
        if (MinCohort < 2)
            throw new ArgumentOutOfRangeException(nameof(MinCohort), MinCohort, "A cohort of fewer than two has no comparative reading at all.");
    }
}

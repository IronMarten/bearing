using System.Diagnostics;

namespace IronMarten.Bearing;

/// <summary>
/// A named span of the walk, timed separately from the rest.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stages cut at the seams the cost actually has, not at every method boundary.</b>
/// <see cref="Open"/> is MSBuild's, <see cref="Compile"/> is Roslyn's, and <see cref="Walk"/> is
/// this tool's — and until A12 nobody knew which of the three the number on the board belonged to.
/// A profile that cannot separate at least those is a stopwatch with extra steps.
/// </para>
/// <para>
/// <b><see cref="Walk"/> contains the four stages after it and is not a peer of them.</b> Adding
/// every member of this enum together double-counts the walk, which is why
/// <see cref="WalkProfile.TopLevel"/> and <see cref="WalkProfile.WithinWalk"/> exist and why
/// nothing should be summing the enum itself.
/// </para>
/// </remarks>
public enum WalkStage
{
    /// <summary>Opening the solution: MSBuild evaluates and design-time-builds every project.</summary>
    Open,

    /// <summary>Producing a compilation per project — Roslyn parses and binds.</summary>
    Compile,

    /// <summary>
    /// Reading the reference paths once, up front: which assemblies are ours, and where each
    /// external one was resolved from.
    /// </summary>
    Index,

    /// <summary>
    /// The traversal itself — every analysable type in every compilation.
    /// <b>Contains the four stages below it.</b>
    /// </summary>
    Walk,

    /// <summary>Within the walk: fetching each declaration's syntax.</summary>
    Syntax,

    /// <summary>Within the walk: obtaining a semantic model for each declaration's tree.</summary>
    Semantics,

    /// <summary>
    /// Within the walk: collecting the references out of one declaration.
    /// <b>This is the stage A9 changes</b> — member attribution happens in
    /// <c>ReferenceCollector</c>, and this is the reading it has to be judged against.
    /// </summary>
    References,

    /// <summary>Within the walk: the per-member metrics — complexity, surface, accessibility.</summary>
    Members,

    /// <summary>Assembling the model out of what the walk accumulated.</summary>
    Build,
}

/// <summary>
/// How long each stage of one walk took, and how much work it had to do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Always measured, never behind a flag.</b> The instrument is a handful of
/// <see cref="Stopwatch.GetTimestamp"/> calls per declaration, and a profile that has to be
/// switched on describes a run the user did not have. <c>--profile</c> decides whether the numbers
/// are printed, not whether they are taken.
/// </para>
/// <para>
/// <b>It reconciles, and that is the property worth keeping.</b> <see cref="Unaccounted"/> is
/// <see cref="Total"/> minus the top-level stages, and <see cref="WalkUnaccounted"/> is
/// <see cref="Walk"/> minus its own four; work that grows without being measured shows up there
/// rather than nowhere. A profile whose parts do not sum to the whole invites exactly the reading
/// A2's first figures got — a number quoted with confidence that nobody could re-derive.
/// </para>
/// <para>
/// <b>Not part of the structure model, and not in the JSON.</b> Two runs over one commit produce
/// the same model and different profiles, and a wall-clock reading inside the artifact that is
/// asserted for determinism is how a snapshot suite learns to be re-accepted blind
/// (<c>docs/TESTING.md</c> §3).
/// </para>
/// </remarks>
public sealed class WalkProfile
{
    private readonly TimeSpan[] _stages;

    internal WalkProfile(TimeSpan[] stages, TimeSpan total, int projects, int types, int declarations)
    {
        _stages = stages;
        Total = total;
        Projects = projects;
        Types = types;
        Declarations = declarations;
    }

    /// <summary>A profile of nothing — what a walk that has not run reports.</summary>
    public static WalkProfile None { get; } =
        new(new TimeSpan[Enum.GetValues<WalkStage>().Length], TimeSpan.Zero, 0, 0, 0);

    /// <summary>The stages that partition the whole walk, in the order they happen.</summary>
    public static IReadOnlyList<WalkStage> TopLevel { get; } =
        [WalkStage.Open, WalkStage.Compile, WalkStage.Index, WalkStage.Walk, WalkStage.Build];

    /// <summary>The stages that partition <see cref="WalkStage.Walk"/>, in the order they happen.</summary>
    public static IReadOnlyList<WalkStage> WithinWalk { get; } =
        [WalkStage.Syntax, WalkStage.Semantics, WalkStage.References, WalkStage.Members];

    /// <summary>How long one stage took.</summary>
    public TimeSpan this[WalkStage stage] => _stages[(int)stage];

    /// <summary>The whole of the walk, measured once around everything.</summary>
    /// <remarks>
    /// Measured rather than summed, so that <see cref="Unaccounted"/> is a real reading and not
    /// zero by construction.
    /// </remarks>
    public TimeSpan Total { get; }

    /// <summary>Projects that produced a compilation.</summary>
    public int Projects { get; }

    /// <summary>Types walked, after exclusions.</summary>
    public int Types { get; }

    /// <summary>
    /// Declarations walked, which exceeds <see cref="Types"/> by however many extra parts the
    /// solution's partial types have.
    /// </summary>
    /// <remarks>
    /// The per-declaration count is the denominator that matters for the four stages inside the
    /// walk: they run once per declaration rather than once per type, and on a codebase leaning on
    /// partials the two differ by enough to change what a per-unit cost means.
    /// </remarks>
    public int Declarations { get; }

    /// <summary>What <see cref="Total"/> holds that the top-level stages do not account for.</summary>
    public TimeSpan Unaccounted => Total - Sum(TopLevel);

    /// <summary>What <see cref="WalkStage.Walk"/> holds that its four inner stages do not account for.</summary>
    /// <remarks>
    /// Enumerating the types, the exclusion test, interning each node and classifying it — the
    /// part of the traversal that is this tool's own bookkeeping rather than Roslyn answering a
    /// question.
    /// </remarks>
    public TimeSpan WalkUnaccounted => this[WalkStage.Walk] - Sum(WithinWalk);

    /// <summary>The total of the given stages.</summary>
    public TimeSpan Sum(IEnumerable<WalkStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        return stages.Aggregate(TimeSpan.Zero, (running, stage) => running + this[stage]);
    }
}

/// <summary>
/// Accumulates stage times while a walk runs.
/// </summary>
/// <remarks>
/// <para>
/// Timestamps are taken by hand rather than through a <c>using</c> scope, because two of these
/// stages sit inside the per-declaration loop and one of them inside an <c>async</c> method: a
/// disposable scope there is either an allocation per declaration or a struct hoisted into a state
/// machine, and neither reads better than the two lines it would replace.
/// </para>
/// <para>
/// Not thread-safe, and does not need to be — the walk is sequential, which is itself one of the
/// things this profile exists to let somebody argue about.
/// </para>
/// </remarks>
internal sealed class WalkClock
{
    private readonly long[] _ticks = new long[Enum.GetValues<WalkStage>().Length];
    private readonly long _started = Stopwatch.GetTimestamp();

    /// <summary>Projects that produced a compilation.</summary>
    internal int Projects { get; set; }

    /// <summary>Types walked, after exclusions.</summary>
    internal int Types { get; set; }

    /// <summary>Declarations walked.</summary>
    internal int Declarations { get; set; }

    /// <summary>A timestamp to hand back to <see cref="Add"/> when the stage ends.</summary>
    internal static long Now() => Stopwatch.GetTimestamp();

    /// <summary>Charges the time since <paramref name="since"/> to a stage.</summary>
    internal void Add(WalkStage stage, long since) =>
        _ticks[(int)stage] += Stopwatch.GetTimestamp() - since;

    /// <summary>Freezes what has accumulated, and stops the total.</summary>
    internal WalkProfile Freeze()
    {
        var stages = new TimeSpan[_ticks.Length];
        for (var i = 0; i < _ticks.Length; i++) stages[i] = Stopwatch.GetElapsedTime(0, _ticks[i]);

        return new WalkProfile(stages, Stopwatch.GetElapsedTime(_started), Projects, Types, Declarations);
    }
}

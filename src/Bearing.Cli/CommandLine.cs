using System.Globalization;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// What the user asked for.
/// </summary>
/// <param name="Options">What to analyse, with the policy the flags produced.</param>
/// <param name="ShowHelp">Whether to print usage instead of analysing.</param>
/// <param name="ShowVersion">Whether to print the version instead of analysing.</param>
/// <param name="JsonPath">Where to write the model as JSON, or null for not at all.</param>
/// <param name="CsvDirectory">Where to write the CSV files, or null for not at all.</param>
/// <param name="HtmlPath">Where to write the HTML report, or null for not at all.</param>
/// <param name="DiagramPath">Where to write the architecture diagram as SVG, or null.</param>
/// <param name="MosaicPath">Where to write the mosaic as SVG, or null.</param>
/// <param name="PlotPath">Where to write the plot as SVG, or null.</param>
/// <param name="Full">Whether the report enumerates every finding rather than leading with one per kind.</param>
/// <param name="Profile">Whether to print, to stderr, where the run's time went.</param>
/// <remarks>
/// The file outputs are <b>additions</b> to the terminal report rather than alternatives to it.
/// A run that writes JSON still prints, because the two answer different people: a report is read
/// once by a person and a file is read repeatedly by something else, and making one suppress the
/// other means a user who wanted both learns they have to run the analysis twice.
/// </remarks>
public sealed record Invocation(
    WalkOptions? Options,
    bool ShowHelp,
    bool ShowVersion,
    string? JsonPath = null,
    string? CsvDirectory = null,
    string? HtmlPath = null,
    string? DiagramPath = null,
    string? MosaicPath = null,
    string? PlotPath = null,
    bool Full = false,
    bool Profile = false);

/// <summary>
/// Raised when the command line cannot be understood. Carries a message a user can act on.
/// </summary>
public sealed class CommandLineException : Exception
{
    /// <summary>Creates the exception.</summary>
    public CommandLineException(string message) : base(message) { }

    /// <summary>Creates the exception.</summary>
    public CommandLineException() { }

    /// <summary>Creates the exception.</summary>
    public CommandLineException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Turns arguments into a <see cref="WalkOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every one of <see cref="AnalysisPolicy"/>'s named values gets a flag. That is a deliberate
/// commitment rather than convenience: the policy exists so a reader can see which thresholds
/// produced a finding, and a threshold that is named in the output but cannot be moved from the
/// command line is only half-exposed. <c>AnalysisPolicyTests</c> holds the table complete, so
/// adding a value to the policy and forgetting the flag is a test failure rather than a gap
/// nobody notices.
/// </para>
/// <para>
/// <b>Flag names are derived, not written twice.</b> <c>MinFanIn</c> becomes <c>--min-fan-in</c>
/// by one rule applied to the property name, so a rename cannot leave the flag behind saying
/// something else. The probe's spellings all fall out of that rule unchanged, which is why the
/// flags a user already knows keep working.
/// </para>
/// </remarks>
public static class CommandLine
{
    /// <summary>
    /// Every policy value, with how to apply it. The name is the property's, so the flag and the
    /// policy cannot drift apart.
    /// </summary>
    private static readonly (string Property, Func<AnalysisPolicy, double, AnalysisPolicy> Apply)[] PolicyFlags =
    [
        (nameof(AnalysisPolicy.MinCohort), (p, v) => p with { MinCohort = (int)v }),
        (nameof(AnalysisPolicy.OutlierFactor), (p, v) => p with { OutlierFactor = v }),
        (nameof(AnalysisPolicy.MinFanIn), (p, v) => p with { MinFanIn = (int)v }),
        (nameof(AnalysisPolicy.Top), (p, v) => p with { Top = (int)v }),
        (nameof(AnalysisPolicy.HighCc), (p, v) => p with { HighCc = (int)v }),
        (nameof(AnalysisPolicy.MinDecisionCc), (p, v) => p with { MinDecisionCc = (int)v }),
        (nameof(AnalysisPolicy.ConcealedTopRank), (p, v) => p with { ConcealedTopRank = (int)v }),
        (nameof(AnalysisPolicy.HubMin), (p, v) => p with { HubMin = (int)v }),
        (nameof(AnalysisPolicy.GodObjectMembers), (p, v) => p with { GodObjectMembers = (int)v }),
        (nameof(AnalysisPolicy.MinKindSpan), (p, v) => p with { MinKindSpan = (int)v }),
        (nameof(AnalysisPolicy.StableThreshold), (p, v) => p with { StableThreshold = v }),
        (nameof(AnalysisPolicy.IsolatedThreshold), (p, v) => p with { IsolatedThreshold = v }),
        (nameof(AnalysisPolicy.BreaksAloneMinFanIn), (p, v) => p with { BreaksAloneMinFanIn = (int)v }),
        (nameof(AnalysisPolicy.ConcealedFanInCeiling), (p, v) => p with { ConcealedFanInCeiling = v }),
        (nameof(AnalysisPolicy.ConcealedFanOutCeiling), (p, v) => p with { ConcealedFanOutCeiling = v }),
        (nameof(AnalysisPolicy.BlastFanInMultiple), (p, v) => p with { BlastFanInMultiple = v }),
        (nameof(AnalysisPolicy.BlastTopFraction), (p, v) => p with { BlastTopFraction = v }),
        (nameof(AnalysisPolicy.BoundaryTopFraction), (p, v) => p with { BoundaryTopFraction = v }),
        (nameof(AnalysisPolicy.BlastComplexityPercentile), (p, v) => p with { BlastComplexityPercentile = v }),
        (nameof(AnalysisPolicy.ChangeCostTopFraction), (p, v) => p with { ChangeCostTopFraction = v }),
        (nameof(AnalysisPolicy.RollCallDivisor), (p, v) => p with { RollCallDivisor = (int)v }),
        (nameof(AnalysisPolicy.SurfaceOutlierMultiple), (p, v) => p with { SurfaceOutlierMultiple = v }),
        (nameof(AnalysisPolicy.SurfaceOutlierFloor), (p, v) => p with { SurfaceOutlierFloor = v }),
        (nameof(AnalysisPolicy.MaxNamedSurfaces), (p, v) => p with { MaxNamedSurfaces = (int)v }),
        (nameof(AnalysisPolicy.GlobalFanInPercentile), (p, v) => p with { GlobalFanInPercentile = v }),
        (nameof(AnalysisPolicy.GlobalComplexityPercentile), (p, v) => p with { GlobalComplexityPercentile = v }),
        (nameof(AnalysisPolicy.GlobalComplexityFloor), (p, v) => p with { GlobalComplexityFloor = (int)v }),
        (nameof(AnalysisPolicy.MinTangle), (p, v) => p with { MinTangle = (int)v }),
    ];

    /// <summary>Every policy flag, as it appears on the command line.</summary>
    public static IReadOnlyList<string> PolicyFlagNames =>
        PolicyFlags.Select(f => FlagFor(f.Property)).ToList();

    /// <summary>The policy property a flag sets, or <see langword="null"/> if it sets none.</summary>
    public static string? PropertyBehind(string flag) =>
        PolicyFlags
            .Where(f => string.Equals(FlagFor(f.Property), flag, StringComparison.Ordinal))
            .Select(f => f.Property)
            .FirstOrDefault();

    /// <summary>
    /// <c>MinFanIn</c> becomes <c>--min-fan-in</c>: lower-case, hyphen before each capital.
    /// </summary>
    public static string FlagFor(string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        var flag = new System.Text.StringBuilder("--");
        for (var i = 0; i < property.Length; i++)
        {
            if (i > 0 && char.IsUpper(property[i])) flag.Append('-');
            flag.Append(char.ToLowerInvariant(property[i]));
        }

        return flag.ToString();
    }

    /// <summary>Parses arguments, or throws <see cref="CommandLineException"/>.</summary>
    public static Invocation Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var policy = AnalysisPolicy.Default;
        string? solutionPath = null;
        var includeTests = false;
        var excluded = new List<string>();
        var clearDefaultExcludes = false;
        string? jsonPath = null;
        string? csvDirectory = null;
        string? htmlPath = null;
        string? diagramPath = null;
        string? mosaicPath = null;
        string? plotPath = null;
        var full = false;
        var profile = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    return new Invocation(null, ShowHelp: true, ShowVersion: false);

                case "--version":
                    return new Invocation(null, ShowHelp: false, ShowVersion: true);

                case "--full":
                    full = true;
                    continue;

                case "--profile":
                    profile = true;
                    continue;

                case "--include-tests":
                    includeTests = true;
                    continue;

                case "--no-default-excludes":
                    clearDefaultExcludes = true;
                    continue;

                case "--exclude-path":
                    excluded.Add(Next(args, ref i, arg));
                    continue;

                case "--json":
                    jsonPath = Path.GetFullPath(Next(args, ref i, arg));
                    continue;

                case "--csv":
                    csvDirectory = Path.GetFullPath(Next(args, ref i, arg));
                    continue;

                case "--html":
                    htmlPath = Path.GetFullPath(Next(args, ref i, arg));
                    continue;

                case "--diagram":
                    diagramPath = Path.GetFullPath(Next(args, ref i, arg));
                    continue;

                case "--mosaic":
                    mosaicPath = Path.GetFullPath(Next(args, ref i, arg));
                    continue;

                case "--plot":
                    plotPath = Path.GetFullPath(Next(args, ref i, arg));
                    continue;
            }

            if (PropertyBehind(arg) is not null)
            {
                var raw = Next(args, ref i, arg);
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw new CommandLineException($"{arg} expects a number, not '{raw}'.");

                var apply = PolicyFlags.First(f => string.Equals(FlagFor(f.Property), arg, StringComparison.Ordinal)).Apply;
                policy = apply(policy, value);
                continue;
            }

            if (arg.StartsWith('-')) throw new CommandLineException($"Unknown option: {arg}");

            if (solutionPath is not null)
                throw new CommandLineException($"More than one solution given: '{solutionPath}' and '{arg}'.");

            solutionPath = Path.GetFullPath(arg);
        }

        if (solutionPath is null) return new Invocation(null, ShowHelp: true, ShowVersion: false);

        // Defaults are replaced rather than added to when the user opts out, and appended to
        // otherwise. The list is what the model reports as ExclusionsApplied, so it has to be the
        // set actually used and not the set requested.
        var fragments = clearDefaultExcludes
            ? (IReadOnlyList<string>)excluded
            : [.. new WalkOptions { SolutionPath = solutionPath }.ExcludedPathFragments, .. excluded];

        return new Invocation(
            new WalkOptions
            {
                SolutionPath = solutionPath,
                Policy = policy,
                IncludeTests = includeTests,
                ExcludedPathFragments = fragments,
            },
            ShowHelp: false,
            ShowVersion: false,
            JsonPath: jsonPath,
            CsvDirectory: csvDirectory,
            HtmlPath: htmlPath,
            DiagramPath: diagramPath,
            MosaicPath: mosaicPath,
            PlotPath: plotPath,
            Full: full,
            Profile: profile);
    }

    private static string Next(IReadOnlyList<string> args, ref int i, string flag)
    {
        if (i + 1 >= args.Count) throw new CommandLineException($"{flag} expects a value.");
        return args[++i];
    }

    /// <summary>Usage text, including every policy flag and its default.</summary>
    public static IEnumerable<string> Usage(string version)
    {
        yield return $"bearing {version} - Iron Marten";
        yield return "";
        yield return "  bearing <solution.sln> [options]";
        yield return "";
        yield return "  --include-tests            analyse projects that look like test projects";
        yield return "  --exclude-path <fragment>  skip files whose path contains this";
        yield return "  --no-default-excludes      drop the built-in exclusions instead of adding to them";
        yield return "  --json <file>              also write the whole model as JSON";
        yield return "  --csv <dir>                also write types.csv, members.csv, edges.csv";
        yield return "  --html <file>              also write the shareable single-file report";
        yield return "  --diagram <file.svg>       also write the project map, for pasting into chat";
        yield return "  --mosaic <file.svg>        also write the mosaic: every type as one cell";
        yield return "  --plot <file.svg>          also write the plot: projects by reach and density";
        yield return "  --full                     enumerate every finding instead of one per kind";
        yield return "  --profile                  also print, to stderr, where the run's time went";
        yield return "  --version                  print the version and exit";
        yield return "";
        yield return "  Thresholds — every value the report cites can be moved:";

        var defaults = AnalysisPolicy.Default.Values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

        foreach (var (property, _) in PolicyFlags)
        {
            var flag = FlagFor(property);
            var value = defaults.TryGetValue(property, out var d)
                ? d.ToString("0.####", CultureInfo.InvariantCulture)
                : "";
            yield return $"  {flag,-34} default {value}";
        }
    }
}

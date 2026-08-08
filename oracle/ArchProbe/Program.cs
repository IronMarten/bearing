using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;

namespace ArchProbe;

sealed class Options
{
    public string SolutionPath;
    public string OutDir = "archprobe-out";
    public bool IncludeTests;
    public int MinCohort = 5;
    public double OutlierFactor = 3.0;
    public int MinFanIn = 5;
    public double StableThreshold = 0.2;
    public int HighCc = 10;          // McCabe's conventional "worth a look" threshold
    public int MinDecisionCc = 5;    // below this, 'concealed decision' is self-contradicting
    public int HubMin = 5;           // fan-in AND fan-out both at or above this
    public int GodObjectMembers = 20;
    public int MinKindSpan = 3;      // architectural kinds a component reaches across
    public int MinDriftDelta = 3;    // absolute change below which drift is noise
    public int MinTangle = 4;        // mutual pairs/triples are ordinary C#
    public string BaselinePath;
    public int Top = 15;
    public bool NoDefaultExcludes;
    public readonly List<string> ExcludePaths = new();

    // Scaffolded or tool-generated code. It is real C# and it compiles, but it is nobody's
    // design, so it pollutes cohorts and produces nominations no one can act on. EF
    // Migrations alone can be hundreds of files with enormous methods.
    //
    // Directory patterns are written with forward slashes and matched against a normalized
    // path, so they work regardless of which separator the workspace hands back — and so
    // that a stray backslash in a literal cannot silently disable the pattern.
    static readonly string[] DefaultExcludes =
    {
        "/migrations/", "/helppage/", "/obj/", "/bin/",
        "/connected services/", "/service references/", "/web references/",
        "/.nuget/", "/packages/",
        ".designer.cs", ".generated.cs", ".g.cs", ".g.i.cs",
        "reference.cs", "assemblyinfo.cs", "globalusings.cs",
    };

    public IEnumerable<string> ActiveExcludes =>
        NoDefaultExcludes ? ExcludePaths : DefaultExcludes.Concat(ExcludePaths);

    static string Normalize(string s) => s.Replace('\\', '/');

    public bool IsExcludedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var normalized = Normalize(path);
        foreach (var pattern in ActiveExcludes)
            if (normalized.Contains(Normalize(pattern), StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static readonly Regex TestProject =
        new(@"(^|\.)(tests?|specs?|unittests?|integrationtests?)($|\.)|tests?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool LooksLikeTestProject(string name) => TestProject.IsMatch(name ?? "");

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": o.OutDir = args[++i]; break;
                case "--include-tests": o.IncludeTests = true; break;
                case "--min-cohort": o.MinCohort = int.Parse(args[++i]); break;
                case "--outlier-factor": o.OutlierFactor = double.Parse(args[++i]); break;
                case "--min-fan-in": o.MinFanIn = int.Parse(args[++i]); break;
                case "--stable-threshold": o.StableThreshold = double.Parse(args[++i]); break;
                case "--high-cc": o.HighCc = int.Parse(args[++i]); break;
                case "--min-decision-cc": o.MinDecisionCc = int.Parse(args[++i]); break;
                case "--hub-min": o.HubMin = int.Parse(args[++i]); break;
                case "--god-object-members": o.GodObjectMembers = int.Parse(args[++i]); break;
                case "--min-kind-span": o.MinKindSpan = int.Parse(args[++i]); break;
                case "--min-drift-delta": o.MinDriftDelta = int.Parse(args[++i]); break;
                case "--min-tangle": o.MinTangle = int.Parse(args[++i]); break;
                case "--baseline": o.BaselinePath = Path.GetFullPath(args[++i]); break;
                case "--top": o.Top = int.Parse(args[++i]); break;
                case "--exclude-path": o.ExcludePaths.Add(args[++i]); break;
                case "--no-default-excludes": o.NoDefaultExcludes = true; break;
                case "-h":
                case "--help": return null;
                default:
                    if (args[i].StartsWith("-"))
                        throw new ArgumentException($"Unknown option: {args[i]}");
                    o.SolutionPath = Path.GetFullPath(args[i]);
                    break;
            }
        }
        return string.IsNullOrEmpty(o.SolutionPath) ? null : o;
    }
}

static class Program
{
    static async Task<int> Main(string[] args)
    {
        Options opt;
        try
        {
            opt = Options.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        if (opt == null)
        {
            Usage();
            return 1;
        }

        if (!File.Exists(opt.SolutionPath))
        {
            Console.Error.WriteLine($"Solution not found: {opt.SolutionPath}");
            return 2;
        }

        // MSBuildLocator must register before any MSBuild/Roslyn-workspace type loads,
        // which is why the real work lives in a separate non-inlined method.
        try
        {
            var instance = MSBuildLocator.QueryVisualStudioInstances()
                .OrderByDescending(i => i.Version)
                .FirstOrDefault();

            if (instance == null)
            {
                Console.Error.WriteLine("No MSBuild instance found. Install the .NET SDK (dotnet --version should work).");
                return 3;
            }

            Console.Error.WriteLine($"MSBuild: {instance.Name} {instance.Version} ({instance.MSBuildPath})");
            MSBuildLocator.RegisterInstance(instance);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MSBuildLocator failed: {ex.Message}");
            return 3;
        }

        return await RunAsync(opt);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<int> RunAsync(Options opt)
    {
        try
        {
            var result = await new SolutionAnalyzer(opt).RunAsync(CancellationToken.None);

            if (opt.BaselinePath != null)
            {
                if (!File.Exists(opt.BaselinePath))
                {
                    Console.Error.WriteLine($"Baseline not found: {opt.BaselinePath}");
                    return 2;
                }
                result.BaselineRows = Baseline.Load(opt.BaselinePath);
                Console.Error.WriteLine($"Baseline: {result.BaselineRows.Count} type(s) from {opt.BaselinePath}");
            }

            Report.ComputeCohortStats(result);

            Directory.CreateDirectory(opt.OutDir);
            var types = Path.Combine(opt.OutDir, "types.csv");
            var methods = Path.Combine(opt.OutDir, "methods.csv");
            var edges = Path.Combine(opt.OutDir, "edges.csv");
            var nominations = Path.Combine(opt.OutDir, "nominations.txt");
            var prediction = Path.Combine(opt.OutDir, "prediction-sheet.csv");

            Report.WriteTypesCsv(types, result.Types);
            Report.WriteMethodsCsv(methods, result.Methods);
            Report.WriteEdgesCsv(edges, result.Edges);
            Report.WritePredictionSheet(prediction, result.Types, opt);

            using (var fw = new StreamWriter(nominations, false))
                Report.PrintNominations(result, opt, fw);
            Report.PrintNominations(result, opt, Console.Out);

            Console.Error.WriteLine();
            Console.Error.WriteLine($"Types:   {result.Types.Count}  -> {types}");
            Console.Error.WriteLine($"Methods: {result.Methods.Count}  -> {methods}");
            Console.Error.WriteLine($"Edges:   {result.Edges.Count}  -> {edges}");
            Console.Error.WriteLine($"Prediction sheet (no metrics) -> {prediction}");
            Console.Error.WriteLine($"Cohorts: {result.Types.Select(t => t.Cohort).Distinct().Count()} " +
                                    $"({result.Types.Count(t => t.CohortSize >= opt.MinCohort)} types in cohorts of >= {opt.MinCohort})");

            if (result.ExcludedTypes > 0)
                Console.Error.WriteLine($"Excluded {result.ExcludedTypes} type(s) in generated/scaffolded paths " +
                                        $"({string.Join(" ", opt.ActiveExcludes)}) — --no-default-excludes to keep them");

            if (result.SkippedProjects.Count > 0)
                Console.Error.WriteLine($"Skipped test projects ({result.SkippedProjects.Count}): " +
                                        string.Join(", ", result.SkippedProjects.Take(10)) +
                                        (result.SkippedProjects.Count > 10 ? ", ..." : ""));

            if (result.LoadWarnings.Count > 0)
            {
                Console.Error.WriteLine($"\n{result.LoadWarnings.Count} load warning(s) — fan-in is understated for anything that failed to load:");
                foreach (var warning in result.LoadWarnings.Take(10))
                    Console.Error.WriteLine("  " + warning.Split('\n')[0]);
                if (result.LoadWarnings.Count > 10) Console.Error.WriteLine("  ...");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAILED: {ex}");
            return 4;
        }
    }

    static void Usage()
    {
        Console.Error.WriteLine(@"
archprobe <path-to.sln> [options]

  Throwaway probe: computes design-metric dimensions per type and per method,
  ranks each one WITHIN ITS STRUCTURAL PEER COHORT, and nominates outliers.
  Not a product. One codebase, one evening.

Options:
  --out <dir>            output directory (default: archprobe-out)
  --include-tests        include test projects (default: excluded — test refs
                         inflate fan-in and would corrupt the experiment)
  --min-cohort <n>       ignore cohorts smaller than n for nominations (default 5)
  --outlier-factor <x>   'x times the peer median' threshold (default 3.0)
  --min-fan-in <n>       absolute floor before anything counts as widely
                         depended on, regardless of percentile (default 5)
  --stable-threshold <x> instability at or below which a type counts as
                         load-bearing, Ce/(Ce+Ca) (default 0.2)
  --high-cc <n>          cyclomatic complexity that counts as intricate on its
                         own, no cohort needed (default 10)
  --min-decision-cc <n>  minimum cyclomatic complexity before a method can be
                         called a concealed decision (default 5)
  --hub-min <n>          fan-in AND fan-out both at or above this makes a hub
                         (default 5)
  --god-object-members <n>
                         member count at which a hub reads as a god object
                         rather than wiring (default 20)
  --min-kind-span <n>    architectural kinds a component must reach across
                         before it counts as cross-cutting (default 3)
  --baseline <types.csv> diff against an earlier run and report criticality
                         drift. Any archived types.csv works, including one
                         produced by running against an older commit.
  --min-drift-delta <n>  absolute change below which drift is noise (default 3)
  --min-tangle <n>       smallest mutually-dependent type cluster worth
                         reporting (default 4; pairs are ordinary C#)
  --top <n>              max instances per message type (default 15)
  --exclude-path <s>     skip files whose path contains s (repeatable)
  --no-default-excludes  keep generated/scaffolded code (Migrations, HelpPage,
                         *.designer.cs, Connected Services, ...) which is
                         excluded by default

Outputs types.csv, methods.csv, edges.csv, nominations.txt.
");
    }
}

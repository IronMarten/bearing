using System.Reflection;
using System.Text;
using Microsoft.Build.Locator;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The host: reads arguments, registers MSBuild, runs the walk, and prints what came back.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here decides what anything means. Argument text is <see cref="CommandLine"/>'s,
/// the analysis is Core's, and the words are <see cref="Report"/>'s — this method's whole job is
/// to sequence them and to turn a failure into an exit code. That is the split
/// <c>docs/ARCHITECTURE.md</c> is about, and it is worth holding here more than anywhere, because
/// the probe's equivalent grew into 997 lines of formatting with the interpretation baked in.
/// </para>
/// <para>
/// <b>MSBuild is registered before any Roslyn type is touched.</b> <c>MSBuildLocator</c> rewrites
/// how MSBuild assemblies resolve for the life of the process, so it has to run before the
/// workspace types load — which is why the walk is behind a separate non-inlined method rather
/// than in <c>Main</c>. A library that registered a process-wide singleton on load could not be
/// composed, so Core does not do it and says so in its csproj.
/// </para>
/// <para>
/// <b>Exit codes.</b> <c>0</c> analysed, <c>1</c> the analysis failed, <c>2</c> the invocation was
/// wrong — bad arguments, or a solution this tool cannot read — and <c>3</c> no MSBuild. The
/// second and third are separated because they are different people's problems: <c>2</c> is
/// something the user typed and can retype, <c>3</c> is the machine. Every failure the load can
/// produce now has an arm here; a raw MSBuild stack trace is what the missing one looked like.
/// </para>
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Before anything else, because these two together are the only reading that includes the
        // host starting. See StartupCost: the profile's total has to be the number a stopwatch
        // outside the process would show, or the first row of the table is an apology for the rest.
        var startup = StartupCost();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        UseUtf8();

        var version = ToolInfo.ReadVersion(Assembly.GetExecutingAssembly());

        Invocation invocation;
        try
        {
            invocation = CommandLine.Parse(args);
        }
        catch (CommandLineException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Run 'bearing --help' for usage.");
            return 2;
        }

        if (invocation.ShowVersion)
        {
            Console.WriteLine(version);
            return 0;
        }

        if (invocation.ShowHelp || invocation.Options is null)
        {
            foreach (var line in CommandLine.Usage(version)) Console.WriteLine(line);
            return invocation.ShowHelp ? 0 : 2;
        }

        // The version the tool actually ships, told to Core rather than guessed by it.
        // The model used to read Bearing.Core's assembly, which sets none.
        var options = invocation.Options with { ToolVersion = version };

        if (!File.Exists(options.SolutionPath))
        {
            Console.Error.WriteLine($"Solution not found: {options.SolutionPath}");
            return 2;
        }

        // Read before the walk, so a typo in --acknowledge costs nothing. A default file that is
        // not there is the ordinary first run and means an empty set; a named one that is not there
        // is a typo, and silently analysing as though the user had acknowledged nothing is the
        // worst of the three ways to handle it.
        Acknowledgments acknowledged;
        try
        {
            acknowledged = Acknowledgments.Read(invocation.AcknowledgePath!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read {invocation.AcknowledgePath}: {ex.Message}");
            return 2;
        }

        if (invocation.AcknowledgeExplicit && acknowledged.Path is null)
        {
            Console.Error.WriteLine($"Acknowledgment file not found: {invocation.AcknowledgePath}");
            return 2;
        }

        if (!RegisterMSBuild()) return 3;

        try
        {
            return await AnalyseAsync(options, invocation, acknowledged, startup, clock)
                .ConfigureAwait(false);
        }
        catch (SolutionLoadException ex)
        {
            foreach (var line in Failure.CouldNotRead(ex)) Console.Error.WriteLine(line);
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Analysis failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Runs the walk and prints the report.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Main"/> and never inlined, so that no Roslyn or MSBuild type is
    /// resolved before <see cref="RegisterMSBuild"/> has run. Inlining this is not a style
    /// question — it reintroduces the load-order bug.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<int> AnalyseAsync(
        WalkOptions options,
        Invocation invocation,
        Acknowledgments acknowledged,
        TimeSpan startup,
        System.Diagnostics.Stopwatch clock)
    {
        var mark = TimeSpan.Zero;
        var stages = new List<ProfileStage> { new("startup", startup, "process start to Main") };

        // Each stage is charged the interval since the last one ended, so the rows cannot overlap
        // and cannot leave a gap between them that is charged to nothing.
        TimeSpan Lap()
        {
            var elapsed = clock.Elapsed;
            var lap = elapsed - mark;
            mark = elapsed;
            return lap;
        }

        // Everything Main did before handing over: parsing the arguments, and locating and
        // registering MSBuild. The second of those is not free and belongs to nobody else's row.
        stages.Add(new ProfileStage("register", Lap(), "MSBuild located"));

        var walker = new SolutionWalker(options);
        var model = await walker.WalkAsync().ConfigureAwait(false);
        Lap();
        stages.AddRange(ProfileReport.StagesOf(walker.Profile));

        // Judged once, and the report reads the surviving half of it. The export needs the
        // suppressed rows too -- SCHEMA-findings-export.md §1 -- and asking Analysis twice would
        // re-run every detector to reach a subset of what the first answer already carried.
        var judgement = Analysis.Judge(model, acknowledged);
        stages.Add(new ProfileStage(
            "analysis", Lap(), Sentences.Plural(judgement.Reported.Count, "finding")));

        // Rendering and writing are one stage because they are one act: Report.For is lazy, so
        // the lines are produced by the loop that prints them and there is no seam to time.
        foreach (var line in Report.For(model, judgement)) Console.WriteLine(line);
        stages.Add(new ProfileStage("report", Lap(), "terminal"));

        // After the report, and to stderr, so that neither the file nor the note about it can
        // land in the middle of output somebody is piping.
        if (invocation.JsonPath is { } json)
        {
            JsonOutput.Write(json, model, judgement, DateTimeOffset.UtcNow, options);
            Console.Error.WriteLine($"Wrote {json}");
            stages.Add(new ProfileStage("json", Lap()));
        }

        if (invocation.CsvDirectory is { } csv)
        {
            foreach (var path in CsvOutput.Write(csv, model))
                Console.Error.WriteLine($"Wrote {path}");
            stages.Add(new ProfileStage("csv", Lap()));
        }

        if (invocation.HtmlPath is { } html)
        {
            HtmlReport.Write(html, model, judgement, DateTimeOffset.UtcNow, invocation.Full);
            Console.Error.WriteLine($"Wrote {html}");
            stages.Add(new ProfileStage("html", Lap()));
        }

        if (invocation.DiagramPath is { } diagram)
        {
            ArchitectureDiagram.Write(diagram, model);
            Console.Error.WriteLine($"Wrote {diagram}");
            stages.Add(new ProfileStage("diagram", Lap()));
        }

        if (invocation.MosaicPath is { } mosaic)
        {
            Mosaic.Write(mosaic, model, judgement.Reported);
            Console.Error.WriteLine($"Wrote {mosaic}");
            stages.Add(new ProfileStage("mosaic", Lap()));
        }

        if (invocation.PlotPath is { } plot)
        {
            ReachPlot.Write(plot, model, judgement.Reported);
            Console.Error.WriteLine($"Wrote {plot}");
            stages.Add(new ProfileStage("plot", Lap()));
        }

        if (invocation.Profile)
            foreach (var line in ProfileReport.For(stages, startup + clock.Elapsed))
                Console.Error.WriteLine(line);

        return 0;
    }

    /// <summary>
    /// How long the process took to reach <see cref="Main"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the OS rather than from a stopwatch, because no stopwatch this program starts can
    /// see the part before its first line runs — the host resolving the runtime, loading the
    /// assemblies and jitting the entry point. On a self-contained tool that is not always small,
    /// and a profile whose total is smaller than the wall clock a caller measured is a profile
    /// that will be argued with rather than used.
    /// </para>
    /// <para>
    /// Best effort. Process start time is unreadable on some hosts and, in principle, can come
    /// back later than now if the clock moved; both give zero rather than a negative first row.
    /// </para>
    /// </remarks>
    private static TimeSpan StartupCost()
    {
        try
        {
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            var since = DateTime.Now - self.StartTime;
            return since > TimeSpan.Zero ? since : TimeSpan.Zero;
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException
                                      or System.ComponentModel.Win32Exception)
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Writes UTF-8, so the report says what it was written to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Redirected to a file, <c>Console.Out</c> encodes through the
    /// process code page, and on a Windows machine that is not UTF-8 every em dash in the report
    /// best-fit-maps to an ASCII hyphen — 247 of them in one nopCommerce run, none surviving.
    /// </para>
    /// <para>
    /// <b>The mangling is not the em dash.</b> Best-fit mapping is silent and lossy for anything
    /// the code page cannot represent, and a character with no mapping becomes <c>?</c> — so a
    /// nominated type named with a non-ASCII identifier is reported under a name the reader cannot
    /// search for. Naming the component is the whole job of a finding.
    /// </para>
    /// <para>
    /// Best effort: setting this fails on a host with no console attached, and a tool that refused
    /// to run because it could not choose an encoding would be worse than one whose dashes are
    /// hyphens. The file writers do not depend on it — they pass their own <see cref="UTF8Encoding"/>.
    /// </para>
    /// </remarks>
    private static void UseUtf8()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
            // No console to configure. The report still renders; its dashes may not survive.
        }
    }

    private static bool RegisterMSBuild()
    {
        if (MSBuildLocator.IsRegistered) return true;

        try
        {
            var instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(i => i.Version).FirstOrDefault();
            if (instance is null)
            {
                Console.Error.WriteLine(
                    "No MSBuild instance found. Install the .NET SDK — 'dotnet --version' should work.");
                return false;
            }

            MSBuildLocator.RegisterInstance(instance);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"MSBuild could not be registered: {ex.Message}");
            return false;
        }
    }
}

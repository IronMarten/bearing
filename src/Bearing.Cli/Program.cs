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
/// produce now has an arm here; <c>docs/DEFECTS.md</c> §23 is what the missing one looked like.
/// </para>
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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
        // docs/DEFECTS.md §21: the model used to read Bearing.Core's assembly, which sets none.
        var options = invocation.Options with { ToolVersion = version };

        if (!File.Exists(options.SolutionPath))
        {
            Console.Error.WriteLine($"Solution not found: {options.SolutionPath}");
            return 2;
        }

        if (!RegisterMSBuild()) return 3;

        try
        {
            return await AnalyseAsync(options, invocation).ConfigureAwait(false);
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
    private static async Task<int> AnalyseAsync(WalkOptions options, Invocation invocation)
    {
        var model = await new SolutionWalker(options).WalkAsync().ConfigureAwait(false);
        var findings = Analysis.FindingsFor(model);

        foreach (var line in Report.For(model, findings)) Console.WriteLine(line);

        // After the report, and to stderr, so that neither the file nor the note about it can
        // land in the middle of output somebody is piping.
        if (invocation.JsonPath is { } json)
        {
            JsonOutput.Write(json, model, DateTimeOffset.UtcNow);
            Console.Error.WriteLine($"Wrote {json}");
        }

        if (invocation.CsvDirectory is { } csv)
            foreach (var path in CsvOutput.Write(csv, model))
                Console.Error.WriteLine($"Wrote {path}");

        return 0;
    }

    /// <summary>
    /// Writes UTF-8, so the report says what it was written to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>docs/DEFECTS.md</c> §25. Redirected to a file, <c>Console.Out</c> encodes through the
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

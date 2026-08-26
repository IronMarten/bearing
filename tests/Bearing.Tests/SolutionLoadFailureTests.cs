using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// What the tool does with a solution it cannot read.
/// </summary>
/// <remarks>
/// <para>
/// In the fixture collection because each case opens an <c>MSBuildWorkspace</c>, and the reason
/// <see cref="FixtureCollection"/> exists is that two workspaces over the same solution at once
/// change what the load reports. These open a different file, but the constraint is on the
/// workspace and not on the file.
/// </para>
/// <para>
/// <b>The failing inputs are written here rather than committed.</b> A <c>.slnx</c> checked into
/// this repo is a file that looks like a solution to every tool that scans the tree, and one
/// deliberately malformed <c>.sln</c> is a trap for the next person regenerating a golden. They
/// cost a temporary directory and they are worth it.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class SolutionLoadFailureTests
{
    /// <summary>
    /// The defect itself: the walk raises something the host can catch, not MSBuild's own
    /// exception from an assembly the host does not reference.
    /// </summary>
    [Fact]
    public async Task An_unreadable_solution_raises_a_load_failure_rather_than_msbuilds_own()
    {
        using var scratch = new Scratch();
        // Genuinely malformed, not merely empty: "<Solution />" parses now
        // and an empty solution is a walk over nothing rather than a failure.
        var path = scratch.Write("Broken.slnx", "<Solution><Project Path=");

        var failure = await Assert.ThrowsAsync<SolutionLoadException>(
            () => new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync());

        Assert.Equal(path, failure.SolutionPath);

        // The cause is kept, not classified. Losing it would leave a permission error and an
        // unparseable file indistinguishable, and only one of those is worth retrying.
        Assert.NotNull(failure.InnerException);
    }

    /// <summary>
    /// The three inputs a first-time user is most likely to give all fail the same way, and all
    /// of them are the walk's to raise rather than the process's to print.
    /// </summary>
    /// <remarks>
    /// They arrive from MSBuild as one message — <c>No file format header found</c> — which is
    /// why <see cref="Failure"/> reads the path rather than the message to tell them apart.
    /// </remarks>
    [Theory]
    [InlineData("Solution.slnx", "<Solution><Project Path=")]
    [InlineData("NotASolution.sln", "this is not a solution")]
    [InlineData("Project.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />")]
    public async Task Every_shape_of_unreadable_input_arrives_as_a_load_failure(string name, string content)
    {
        using var scratch = new Scratch();
        var path = scratch.Write(name, content);

        await Assert.ThrowsAsync<SolutionLoadException>(
            () => new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync());
    }

    /// <summary>
    /// A well-formed <c>.slnx</c> loads, and loads the same solution its <c>.sln</c> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The container is the only new thing — the projects a
    /// <c>.slnx</c> names are the same <c>.csproj</c> files, so the claim worth pinning is not
    /// "it opened" but "it opened the same thing". Comparing type counts against the fixture's
    /// own <c>.sln</c> is what makes a silently half-loaded solution fail here.
    /// </para>
    /// <para>
    /// Written at test time rather than committed, for the reason the malformed inputs are: a
    /// second solution file beside <c>TestBed.sln</c> is a file every tool that scans the tree
    /// would try to build.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_well_formed_slnx_loads_the_same_solution_its_sln_does()
    {
        using var scratch = new Scratch();

        var projects = Directory
            .EnumerateFiles(Path.GetDirectoryName(RepoPaths.TestBedSolution)!, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Path.GetDirectoryName(RepoPaths.TestBedSolution)!, p))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(projects);

        var slnx = "<Solution>"
                   + string.Join("", projects.Select(p => $"<Project Path=\"{p}\" />"))
                   + "</Solution>";

        // Beside TestBed.sln, because the Path attributes are relative to the .slnx.
        var path = Path.Combine(Path.GetDirectoryName(RepoPaths.TestBedSolution)!, "FromSlnx.slnx");
        await File.WriteAllTextAsync(path, slnx);

        try
        {
            var fromSlnx = await new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync();
            var fromSln = await new SolutionWalker(
                new WalkOptions { SolutionPath = RepoPaths.TestBedSolution }).WalkAsync();

            Assert.Equal(
                fromSln.Types.Select(t => t.Subject.Canonical).Order(StringComparer.Ordinal),
                fromSlnx.Types.Select(t => t.Subject.Canonical).Order(StringComparer.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------ what it says ----

    /// <summary>The path is the first thing said, because it is the thing most often mistyped.</summary>
    [Fact]
    public void The_message_leads_with_the_path()
    {
        var lines = Failure.CouldNotRead(Raised(@"C:\work\App.sln")).ToList();

        Assert.Equal(@"Could not read the solution: C:\work\App.sln", lines[0]);
    }

    /// <summary>
    /// No stack frame reaches the user, asserted as an absence.
    /// </summary>
    /// <remarks>
    /// The absence is the defect. Asserting the new text alone would stay green if a later change
    /// printed the message <i>and</i> let the exception escape, which is the same eleven frames
    /// with a sentence above them.
    /// </remarks>
    [Fact]
    public void No_stack_frame_reaches_the_user()
    {
        var lines = Failure.CouldNotRead(Raised(@"C:\work\App.slnx")).ToList();

        Assert.DoesNotContain(lines, l => l.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("Microsoft.Build", StringComparison.Ordinal));
    }

    /// <summary>MSBuild's own words survive, because sometimes they are the specific thing wrong.</summary>
    [Fact]
    public void The_underlying_reason_is_repeated_rather_than_replaced()
    {
        var lines = Failure.CouldNotRead(Raised(@"C:\work\App.sln", "Access to the path is denied.")).ToList();

        Assert.Contains(lines, l => l.Contains("Access to the path is denied.", StringComparison.Ordinal));
    }

    /// <summary>
    /// The path is given once. MSBuild appends the file it was reading to its own message, so
    /// the unedited text puts a second full path on the screen directly under the first.
    /// </summary>
    [Fact]
    public void The_path_is_not_printed_twice()
    {
        const string Path = @"C:\work\App.slnx";
        var lines = Failure.CouldNotRead(Raised(Path, $"No file format header found.  {Path}")).ToList();

        Assert.Single(lines, l => l.Contains(Path, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Trim() == "No file format header found.");
    }

    /// <summary>
    /// Each of the three causes is named, and none of them is named for the others.
    /// </summary>
    /// <remarks>
    /// The point of the section is that "No file format header found" is true of all three and
    /// useful for none, so the assertions are on the discriminating half.
    /// </remarks>
    [Theory]
    [InlineData(@"C:\work\App.slnx", ".slnx")]
    [InlineData(@"C:\work\App.csproj", "project file")]
    [InlineData(@"C:\work\App.fsproj", "project file")]
    [InlineData(@"C:\work\notes.txt", "needs a .sln")]
    public void The_advice_names_what_was_actually_given(string path, string expected)
    {
        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(Raised(path)));

        Assert.Contains(expected, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A project file is diagnosed as a project file whatever the message underneath says.
    /// </summary>
    /// <remarks>
    /// Pinned separately because it is the arm most likely to be quietly lost: the natural way to
    /// write this section is a switch on MSBuild's message, and that reads identically for a
    /// <c>.csproj</c> and for a truncated <c>.sln</c>.
    /// </remarks>
    [Fact]
    public void A_project_file_is_not_diagnosed_from_the_message()
    {
        var text = string.Join(
            Environment.NewLine,
            Failure.CouldNotRead(Raised(@"C:\work\App.csproj", "Something else went wrong.")));

        Assert.Contains("project file, not a solution", text, StringComparison.Ordinal);
    }

    // ------------------------------------------------- a file the parser did not accept ----

    /// <summary>
    /// A file that does not parse is refused, disclosed, and its neighbours survive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The damage was never a missing type — it was a wrong
    /// one.</b> A type in a broken file was collected as <c>global::NeighbourInSameFile</c> rather
    /// than under its own namespace, because the namespace declaration was part of what failed to
    /// parse: a wrong <c>SubjectRef</c>, which <c>--baseline</c> reads as a delete plus an add,
    /// and an edge that vanished while the type count stayed right and the report went on saying
    /// every project compiled.
    /// </para>
    /// <para>
    /// <b>Walked rather than asserted against a hand-built <c>Coverage</c>.</b> The claim is about
    /// what Roslyn does with a tree it could not parse, so a test that supplies the answer proves
    /// nothing. The good file in the same project is what makes this a refusal rather than a
    /// project-level failure.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_file_that_does_not_parse_is_refused_rather_than_walked()
    {
        using var scratch = new Scratch();
        scratch.Write("App.csproj", MinimalProject);
        scratch.Write("Good.cs", "namespace Lib; public class Sound { public int Keep() => 1; }");
        // Unterminated: the namespace and the class header parse, the body does not.
        scratch.Write("Broken.cs", "namespace Lib; public class Torn { public int Slip() => ");
        var path = scratch.Write("App.sln", MinimalSolution);

        var model = await new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync();

        var names = model.Types.Select(t => t.Subject.Canonical).ToList();

        Assert.Contains(names, n => n.Contains("Sound", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Torn", StringComparison.Ordinal));

        // Absent is the honest outcome; absent and unmentioned is the defect.
        Assert.Contains(
            model.Coverage.UnreadableFiles,
            f => f.EndsWith("Broken.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(
            model.Coverage.UnreadableFiles,
            f => f.EndsWith("Good.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// A project that merely does not compile is still walked.
    /// </summary>
    /// <remarks>
    /// <b>The line this must not cross.</b> Semantic errors are the ordinary condition of a project
    /// whose packages did not restore — an unresolved type is <c>CS0246</c> and parses perfectly —
    /// and refusing those would refuse most of the real world for a problem none of them has. Only
    /// a syntax error puts a type under the wrong namespace, so only a syntax error refuses a
    /// tree.
    /// </remarks>
    [Fact]
    public async Task A_semantic_error_does_not_refuse_the_file()
    {
        using var scratch = new Scratch();
        scratch.Write("App.csproj", MinimalProject);
        // Parses cleanly; NoSuchType does not exist, so this is CS0246 and nothing more.
        scratch.Write("Semantic.cs", "namespace Lib; public class Held { public NoSuchType? Gap; }");
        var path = scratch.Write("App.sln", MinimalSolution);

        var model = await new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync();

        Assert.Contains(model.Types, t => t.Subject.Canonical.Contains("Held", StringComparison.Ordinal));
        Assert.Empty(model.Coverage.UnreadableFiles);
    }

    /// <summary>Healthy code says nothing about parsing, because the tripwire is not a feature.</summary>
    /// <remarks>
    /// The section states the clean case for a project that did not load — invariant 8, silence is
    /// not a clean bill of health. This one deliberately does not, and the difference is that a
    /// project failing to load is a thing a reader might reasonably suspect, where "the parser
    /// accepted your C#" is a sentence nobody needs on a report already called a wall of text.
    /// Recorded so the next reader knows it was a choice.
    /// </remarks>
    [Fact]
    public void A_run_with_nothing_unreadable_says_nothing_about_parsing()
    {
        var text = string.Join(Environment.NewLine, Report.NotAnalysed(new Coverage
        {
            ExclusionsApplied = [],
            SkippedProjects = [],
            LoadDiagnostics = [],
            ProjectsNotLoaded = [],
            ExcludedTypes = 0,
            UnreadableFiles = [],
            ProjectsWithUnresolvedReferences = [],
        }));

        Assert.DoesNotContain("could not be parsed", text, StringComparison.Ordinal);
    }

    // -------------------------------------------------- an SDK the machine does not have ----

    /// <summary>
    /// The defect, end to end and on a real load: a perfectly good solution pinning an SDK that is
    /// not installed is not called an unreadable file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Driven through the walk rather than through a synthesized
    /// exception</b>, because the whole fix turns on matching words MSBuild chose, and a test that
    /// supplies those words itself proves only that the constant matches the constant. This one
    /// fails if MSBuild ever rewords the sentence — which is the point, since the failure mode is
    /// this arm silently falling back to <i>"check that the file is complete and readable"</i>.
    /// </para>
    /// <para>
    /// <b>Deterministic on any machine.</b> <c>99.0.100</c> with <c>rollForward: disable</c>
    /// resolves nowhere, so the test does not depend on which SDKs the runner happens to carry —
    /// unlike the report that found the defect, which needed a machine without .NET 10.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_solution_pinning_an_uninstalled_sdk_is_not_reported_as_an_unreadable_file()
    {
        using var scratch = new Scratch();
        scratch.Write("global.json", """{ "sdk": { "version": "99.0.100", "rollForward": "disable" } }""");
        scratch.Write("App.csproj", MinimalProject);
        var path = scratch.Write("App.sln", MinimalSolution);

        var failure = await Assert.ThrowsAsync<SolutionLoadException>(
            () => new SolutionWalker(new WalkOptions { SolutionPath = path }).WalkAsync());

        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(failure));

        Assert.Contains("does not have the .NET SDK", text, StringComparison.Ordinal);
        Assert.Contains("99.0.100", text, StringComparison.Ordinal);

        // The sentence the defect is named for. The file is well-formed and naming 38 projects on
        // the run that found this, so sending the user to inspect it is sending them at the one
        // thing that is not wrong.
        Assert.DoesNotContain("complete and readable", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>global.json</c> is named, wherever above the solution it sits.
    /// </summary>
    /// <remarks>
    /// The walk up is the half worth pinning: nopCommerce keeps its pin beside the solution, and
    /// plenty of repositories keep it at the root with the solution a directory or two down. A
    /// message that only found the adjacent one would be silent in the second case, which is most
    /// of what it exists to say.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("src")]
    [InlineData("src/Solution")]
    public void The_global_json_that_pins_the_sdk_is_named_with_its_version(string below)
    {
        using var scratch = new Scratch();
        var pin = scratch.Write("global.json", """{ "sdk": { "version": "10.0.100" } }""");
        var path = scratch.WriteUnder(below, "App.sln", MinimalSolution);

        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(Raised(path, SdkResolutionFailure)));

        Assert.Contains($"{pin} pins SDK 10.0.100.", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both halves of MSBuild's sentence match on their own.
    /// </summary>
    /// <remarks>
    /// <c>hostfxr_resolve_sdk2</c> is the host call and is the stable half; the prose around it is
    /// MSBuild's and has been reworded before. Either alone is enough, so a rewording of one drops
    /// the arm loudly here rather than quietly in front of a user.
    /// </remarks>
    [Theory]
    [InlineData("Call to hostfxr_resolve_sdk2. There may be more details in stderr.")]
    [InlineData("Failed to find all versions of .NET Core MSBuild.")]
    public void Either_marker_alone_names_the_cause(string cause)
    {
        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(Raised(@"C:\work\App.sln", cause)));

        Assert.Contains("does not have the .NET SDK", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no <c>global.json</c> anywhere above it, the cause is still named and no file is.
    /// </summary>
    /// <remarks>
    /// The demand can come from somewhere else — an SDK-style project, a pinned MSBuild — and
    /// inventing a path for the user to open would be worse than the sentence this replaced.
    /// </remarks>
    [Fact]
    public void With_no_global_json_the_advice_names_the_cause_and_no_file()
    {
        using var scratch = new Scratch();
        var path = scratch.Write("App.sln", MinimalSolution);

        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(Raised(path, SdkResolutionFailure)));

        Assert.Contains("does not have the .NET SDK", text, StringComparison.Ordinal);
        Assert.DoesNotContain("global.json pins", text, StringComparison.Ordinal);
        Assert.Contains("dotnet --list-sdks", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>global.json</c> that pins nothing readable still gets named, without a version.
    /// </summary>
    /// <remarks>
    /// This runs on the worst run a user has, after something has already gone wrong, so a
    /// malformed pin is a plausible thing to meet here. Naming the file is most of the value;
    /// throwing out of an error message is none of it.
    /// </remarks>
    [Theory]
    [InlineData("{ not json at all")]
    [InlineData("""{ "sdk": { } }""")]
    [InlineData("""{ "msbuild-sdks": { "X": "1.0.0" } }""")]
    public void A_global_json_that_pins_no_version_is_named_without_one(string content)
    {
        using var scratch = new Scratch();
        var pin = scratch.Write("global.json", content);
        var path = scratch.Write("App.sln", MinimalSolution);

        var text = string.Join(Environment.NewLine, Failure.CouldNotRead(Raised(path, SdkResolutionFailure)));

        Assert.Contains($"{pin} pins an SDK this machine does not carry.", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A project file stays diagnosed from the path even when the SDK is the thing that failed.
    /// </summary>
    /// <remarks>
    /// <b>The ordering decision, pinned.</b> Pointing at a <c>.csproj</c> is wrong independently of
    /// what the machine carries, and it will still be wrong after the install — so the arm that is
    /// certain from the path runs before the arm that reads a message. The <c>.slnx</c> arm is not
    /// in that position, because its advice is an inference from a failure that may not be about
    /// the file at all, and a newer-SDK solution is the case where it is not.
    /// </remarks>
    [Fact]
    public void A_project_file_outranks_the_sdk_and_a_slnx_does_not()
    {
        var project = string.Join(
            Environment.NewLine, Failure.CouldNotRead(Raised(@"C:\work\App.csproj", SdkResolutionFailure)));
        var container = string.Join(
            Environment.NewLine, Failure.CouldNotRead(Raised(@"C:\work\App.slnx", SdkResolutionFailure)));

        Assert.Contains("project file, not a solution", project, StringComparison.Ordinal);
        Assert.Contains("does not have the .NET SDK", container, StringComparison.Ordinal);
        Assert.DoesNotContain("well-formed", container, StringComparison.Ordinal);
    }

    /// <summary>MSBuild's own words for a solution asking for an SDK that is not there.</summary>
    private const string SdkResolutionFailure =
        "An exception of type System.InvalidOperationException was thrown: Failed to find all "
        + "versions of .NET Core MSBuild. Call to hostfxr_resolve_sdk2. There may be more details "
        + "in stderr.";

    private const string MinimalProject =
        """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>""";

    private const string MinimalSolution =
        """
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "App.csproj", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Global
        EndGlobal
        """;

    private static SolutionLoadException Raised(string path, string cause = "No file format header found.") =>
        new($"'{path}' could not be read as a solution.", new InvalidOperationException(cause))
        {
            SolutionPath = path,
        };

    /// <summary>A temporary directory that removes itself.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string _directory =
            Directory.CreateTempSubdirectory("bearing-load-failure").FullName;

        internal string Write(string name, string content) => WriteUnder("", name, content);

        /// <summary>Writes into a folder below the scratch root, creating it.</summary>
        internal string WriteUnder(string below, string name, string content)
        {
            var folder = Path.Combine(_directory, below.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}

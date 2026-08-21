using IronMarten.Bearing;
using IronMarten.Bearing.Cli;

namespace Bearing.Tests;

/// <summary>
/// What the tool does with a solution it cannot read — <c>docs/DEFECTS.md</c> §23.
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
        // Genuinely malformed, not merely empty: "<Solution />" parses now (docs/DEFECTS.md
        // §8) and an empty solution is a walk over nothing rather than a failure.
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
    /// <c>docs/DEFECTS.md</c> §8. The container is the only new thing — the projects a
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

        internal string Write(string name, string content)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}

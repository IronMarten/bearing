namespace IronMarten.Bearing.Cli;

/// <summary>
/// What the tool says when it cannot start.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/DEFECTS.md</c> §23. A first-time user's most likely mistakes — pointing at a project
/// file, at a <c>.slnx</c>, at something that is not a solution at all — reached them as eleven
/// frames of MSBuild stack trace, because <see cref="Program"/> caught the argument parser's
/// exception and nothing the walk could throw. All three arrive as the same MSBuild message,
/// <c>No file format header found</c>, so the message alone cannot tell them apart and the tool
/// has to look at what it was given.
/// </para>
/// <para>
/// <b>Lines rather than a writer</b>, for the reason <see cref="Report"/> is: the thing a user
/// sees on their worst run should be assertable without a console.
/// </para>
/// <para>
/// <b>The extension is read after the failure, never before it.</b> There is no pre-flight check
/// rejecting <c>.slnx</c>, because the day MSBuild learns to parse one the load will simply
/// succeed and this text will stop being reached — whereas a guard in front of the load would go
/// on refusing a file that had started working, and nothing would fail to say so.
/// </para>
/// </remarks>
public static class Failure
{
    /// <summary>
    /// Why the solution could not be read, and what to do about it.
    /// </summary>
    public static IEnumerable<string> CouldNotRead(SolutionLoadException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var path = failure.SolutionPath;

        yield return $"Could not read the solution: {path}";

        // MSBuild's own words. Kept because it is occasionally the specific thing that is wrong
        // — a truncated file, a permission error — and a tool that replaces every cause with its
        // own guess is one a user cannot get past when the guess is wrong.
        if (Reason(failure, path) is { Length: > 0 } reason) yield return $"  {reason}";

        yield return "";

        foreach (var line in Advice(path)) yield return line;
    }

    /// <summary>
    /// The cause, with the solution path taken back out of it.
    /// </summary>
    /// <remarks>
    /// MSBuild appends the file it was reading to its own message, so the raw text repeats a full
    /// path the line above has already given — twice on one screen, and the second copy is the
    /// one wrapped across the terminal. Removing it is presentation, which is why it happens here
    /// and not in the exception.
    /// </remarks>
    private static string Reason(SolutionLoadException failure, string path)
    {
        if (failure.InnerException is not { } cause) return "";

        return cause.Message.Replace(path, "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static IEnumerable<string> Advice(string path)
    {
        var extension = Path.GetExtension(path);

        if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            yield return "That is an XML solution file. Bearing reads the .sln format, and MSBuild's";
            yield return "solution parser does not accept .slnx yet — so this is a limitation and not";
            yield return "a problem with your file. If a .sln for the same projects still exists, use it.";
            yield break;
        }

        if (ProjectExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return "That is a project file, not a solution. Bearing measures each type against";
            yield return "its peers across the whole solution, so it needs the .sln that names them all.";
            yield break;
        }

        yield return "Bearing needs a .sln file. Check the path names the solution rather than a";
        yield return "project or a directory, and that the file is complete and readable.";
    }

    /// <summary>
    /// The project files a user is most likely to point at by mistake.
    /// </summary>
    /// <remarks>
    /// Not just C#: a user with a mixed solution reaches for whichever project is in front of
    /// them, and being told "that is a project file" is right in every case. Whether Bearing
    /// would then have analysed it is a different question, and one it does not get to.
    /// </remarks>
    private static readonly string[] ProjectExtensions = [".csproj", ".vbproj", ".fsproj", ".proj"];
}

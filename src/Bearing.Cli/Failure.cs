using System.Text.Json;

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
/// rejecting <c>.slnx</c>, and the design note that predicted why is worth keeping now that it has
/// paid: the text said it would stop being reached the day the load learned to succeed, and
/// <c>docs/DEFECTS.md</c> §8 is that day. A guard in front of the load would have gone on refusing
/// a file that had started working, and nothing would have failed to say so.
/// </para>
/// <para>
/// <b>What a <c>.slnx</c> failure means now is different, so the sentence is too.</b> It is no
/// longer "this tool does not read your format" — it does — but a file that did not parse or names
/// a project that is not there. Leaving the old text would be the tool blaming its own limitation
/// for the user's typo.
/// </para>
/// <para>
/// <b>A fourth cause was added later and it is not one of §23's</b> — <c>docs/DEFECTS.md</c> §43.
/// A solution pinning an SDK newer than the machine's is the likeliest first-run failure there is,
/// it is a missing prerequisite rather than a mistake, and unlike the other three it arrives in
/// words that name it. So it is the one arm read from the message, and <see cref="Advice"/> carries
/// the order that keeps that from reopening what §23 settled.
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

        foreach (var line in Advice(path, Reason(failure, path))) yield return line;
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

    /// <summary>
    /// What to do about it: certain from the path first, then the one cause that names itself,
    /// then the guesses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order is the argument.</b> A <c>.csproj</c> is not a solution whatever the machine is
    /// missing, and it will still not be one after an SDK install, so the path settles that arm
    /// before anything reads a message. The other two path-based arms are inferences drawn from a
    /// failure that may not be about the file at all, so a cause that states its own name outranks
    /// them.
    /// </para>
    /// <para>
    /// <b>This sits inside §23 rather than against it</b> — <c>docs/DEFECTS.md</c> §43. What §23
    /// established is that its three causes all arrive as <c>No file format header found</c>, so
    /// the message cannot tell them apart and the path has to. A missing SDK does not share that
    /// message; it is separable by exactly the thing §23 could not use.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Advice(string path, string reason)
    {
        var extension = Path.GetExtension(path);

        if (ProjectExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return "That is a project file, not a solution. Bearing measures each type against";
            yield return "its peers across the whole solution, so it needs the .sln that names them all.";
            yield break;
        }

        if (NeedsAnSdkThisMachineLacks(reason))
        {
            foreach (var line in MissingSdk(path)) yield return line;
            yield break;
        }

        if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Bearing reads .slnx, so the format is not the problem. The file itself could";
            yield return "not be parsed as an XML solution — check that it is well-formed, and that every";
            yield return "Path it names resolves from the folder the .slnx is in.";
            yield break;
        }

        yield return "Bearing needs a .sln file. Check the path names the solution rather than a";
        yield return "project or a directory, and that the file is complete and readable.";
    }

    /// <summary>
    /// Whether the load failed because the host could not resolve the SDK the solution asks for.
    /// </summary>
    /// <remarks>
    /// <b>Two markers rather than one.</b> <c>hostfxr_resolve_sdk2</c> is the host call that
    /// actually fails and is the stable half; the sentence wrapped around it is MSBuild's prose and
    /// has been reworded before. Matching either means a rewording drops this arm back into the
    /// fallback — which is the shape the defect had — instead of silently.
    /// </remarks>
    private static bool NeedsAnSdkThisMachineLacks(string reason) =>
        reason.Contains("hostfxr_resolve_sdk2", StringComparison.OrdinalIgnoreCase)
        || reason.Contains("find all versions of .NET Core MSBuild", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The solution is fine and the machine is short a prerequisite — say which one, and where the
    /// demand for it is written.
    /// </summary>
    /// <remarks>
    /// <b>It names the file rather than describing it.</b> A <c>global.json</c> can sit any number
    /// of directories above the solution, and sending a user to go and find one is most of the work
    /// this message exists to save. Where there is no such file the demand came from somewhere else,
    /// and the advice says that rather than sending them after a file that is not there.
    /// </remarks>
    private static IEnumerable<string> MissingSdk(string path)
    {
        yield return "This machine does not have the .NET SDK the solution asks for. That is a missing";
        yield return "prerequisite rather than a problem with the file, which is fine as it stands.";
        yield return "";

        if (SdkDemand(path) is var (pin, version))
        {
            yield return version.Length > 0
                ? $"{pin} pins SDK {version}."
                : $"{pin} pins an SDK this machine does not carry.";
            yield return "";
        }

        yield return "Run 'dotnet --list-sdks' to see what is installed and install the version that";
        yield return "is pinned; 'dotnet --info' names the one Bearing would otherwise have used.";
    }

    /// <summary>
    /// The nearest <c>global.json</c> at or above the solution, and the SDK version it pins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nearest wins, walking up</b>, because that is how the SDK host resolves it. A message
    /// naming a different file from the one that was obeyed would send the user to edit something
    /// with no effect on the outcome.
    /// </para>
    /// <para>
    /// <b>Every failure to read one is swallowed.</b> This runs on the worst run a user has, after
    /// something has already gone wrong, and an unreadable or malformed <c>global.json</c> is a
    /// plausible thing to meet there. Naming the file without its version is still most of the
    /// value; throwing out of an error message is none of it.
    /// </para>
    /// </remarks>
    private static (string Pin, string Version)? SdkDemand(string path)
    {
        try
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

            for (; directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "global.json");
                if (!File.Exists(candidate)) continue;

                return (candidate, PinnedVersion(candidate));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException or System.Security.SecurityException)
        {
            // Fall through to the wording that carries no file.
        }

        return null;
    }

    /// <summary>The <c>sdk.version</c> a <c>global.json</c> pins, or empty when it pins none.</summary>
    private static string PinnedVersion(string globalJson)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(globalJson));

            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("sdk", out var sdk)
                   && sdk.ValueKind == JsonValueKind.Object
                   && sdk.TryGetProperty("version", out var version)
                   && version.ValueKind == JsonValueKind.String
                ? version.GetString() ?? ""
                : "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                      or System.Security.SecurityException)
        {
            return "";
        }
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

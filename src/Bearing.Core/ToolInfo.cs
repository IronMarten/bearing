using System.Reflection;

namespace IronMarten.Bearing;

/// <summary>
/// Version reporting for the tool.
///
/// This is deliberately the first thing in <c>Bearing.Core</c>, and it is here rather than
/// in the CLI to make the seam concrete before there is any analysis to argue about: the
/// rule that decides what a version string looks like is logic, and logic lives in Core.
/// Printing it is presentation, and that stays in <c>Bearing.Cli</c>. See
/// <c>docs/ARCHITECTURE.md</c>.
/// </summary>
public static class ToolInfo
{
    /// <summary>Fallback when an assembly carries no informational version.</summary>
    public const string UnknownVersion = "0.0.0";

    /// <summary>
    /// Reads the display version of <paramref name="assembly"/>.
    /// </summary>
    /// <remarks>
    /// Takes the assembly rather than calling <see cref="Assembly.GetEntryAssembly"/> so the
    /// result is a function of its input. The entry assembly under a test host is the test
    /// runner, which would make this untestable in exactly the place it needs a test.
    /// </remarks>
    public static string ReadVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informational)
            ? UnknownVersion
            : StripSourceRevision(informational);
    }

    /// <summary>
    /// Drops the commit hash SourceLink appends, turning <c>0.0.1-preview.1+abc1234</c> into
    /// <c>0.0.1-preview.1</c>.
    /// </summary>
    /// <remarks>
    /// SemVer build metadata is everything after the first <c>+</c>, so the first one is the
    /// right split point — a later <c>+</c> would be inside the metadata we are discarding.
    /// </remarks>
    public static string StripSourceRevision(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}

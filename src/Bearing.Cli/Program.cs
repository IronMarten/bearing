using System.Reflection;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// Placeholder entry point for the 0.0.1-preview package.
///
/// This build performs no analysis. It exists so that <c>IronMarten.Bearing</c> is
/// published with complete, consistent metadata ahead of the NuGet ID-prefix
/// reservation request for <c>IronMarten.*</c>. Analysis lands in 0.1.
///
/// Note what this class does and does not do: it reads arguments and writes to the
/// console, and nothing else. Deciding what the version string should say is
/// <see cref="ToolInfo"/>'s job, in Bearing.Core. That split is the whole architecture
/// (<c>docs/ARCHITECTURE.md</c>), and it is worth holding even here, where the logic is
/// four lines long.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var version = ToolInfo.ReadVersion(Assembly.GetExecutingAssembly());

        if (Array.Exists(args, a => string.Equals(a, "--version", StringComparison.Ordinal)))
        {
            Console.WriteLine(version);
            return 0;
        }

        Console.WriteLine($"bearing {version} - Iron Marten");
        Console.WriteLine();
        Console.WriteLine("This is a placeholder release. It reserves the package identity and");
        Console.WriteLine("performs no analysis yet.");
        Console.WriteLine();
        Console.WriteLine("  https://github.com/ironmarten/bearing");

        return 0;
    }
}

using System.Reflection;

namespace IronMarten.Bearing;

/// <summary>
/// Placeholder entry point for the 0.0.1-preview package.
///
/// This build performs no analysis. It exists so that <c>IronMarten.Bearing</c> is
/// published with complete, consistent metadata ahead of the NuGet ID-prefix
/// reservation request for <c>IronMarten.*</c>. Analysis lands in 0.1.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        // Strip the source-link commit hash that SourceLink appends (e.g. "0.0.1-preview.1+abc1234").
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        if (args.Contains("--version"))
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

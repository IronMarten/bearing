using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// Framework or package, decided on a real load against a NuGet cache this test builds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the fixture cannot pin this on its own.</b> A targeting pack reaches the NuGet cache only
/// when the SDK doing the build does not carry it — SDK 10 building a <c>net8.0</c> project restores
/// <c>microsoft.netcore.app.ref/8.0.x</c> as a package. So whether the fixture's
/// <c>System.Data</c> resolves from the cache or from <c>packs/</c> depends on which SDKs the
/// machine has, and a machine with the exact pack installed passes with the defect present. The
/// day Bearing moved off .NET 8 is the day it showed: every framework reference in TestBed read
/// as NuGet.
/// </para>
/// <para>
/// <b>Two borrowed assemblies, one placed each way.</b> The cache here holds a package laid out as
/// a targeting pack — <c>data/FrameworkList.xml</c> at its root — and one laid out as an ordinary
/// package. Neither is restored; a <c>HintPath</c> is enough for the SDK to resolve a reference
/// from a path, and the path is the whole of what the rule reads. The names are deliberately not
/// framework names, so a rule that went back to matching names would fail here.
/// </para>
/// </remarks>
public sealed class ExternalOriginTests
{
    [Fact]
    public async Task A_targeting_pack_in_the_nuget_cache_is_the_framework_and_a_package_beside_it_is_not()
    {
        using var scratch = new Scratch();

        var pack = scratch.Directory("cache/some.targeting.pack/1.0.0");
        scratch.Write("cache/some.targeting.pack/1.0.0/data/FrameworkList.xml", "<FileList></FileList>");
        var humanizer = Scratch.Copy("Humanizer.dll", Path.Combine(pack, "ref", "net8.0"));

        var package = scratch.Directory("cache/some.package/1.0.0");
        var json = Scratch.Copy("Newtonsoft.Json.dll", Path.Combine(package, "lib", "net8.0"));

        scratch.Write("App/App.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <Reference Include="Humanizer"><HintPath>{humanizer}</HintPath></Reference>
                <Reference Include="Newtonsoft.Json"><HintPath>{json}</HintPath></Reference>
              </ItemGroup>
            </Project>
            """);
        scratch.Write("App/Uses.cs", """
            namespace App;
            public class UsesPack { public string F() => Humanizer.StringHumanizeExtensions.Humanize("x"); }
            public class UsesPackage { public string G() => Newtonsoft.Json.JsonConvert.SerializeObject(1); }
            """);
        var solution = scratch.Write("App/App.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        var model = await new SolutionWalker(new WalkOptions
        {
            SolutionPath = solution,
            NuGetCachePath = scratch.Directory("cache"),
        }).WalkAsync();

        Assert.Equal(ExternalOrigin.Framework, model.OriginOf("Humanizer"));
        Assert.Equal(ExternalOrigin.Package, model.OriginOf("Newtonsoft.Json"));
    }

    /// <summary>A temporary directory that removes itself.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string _root =
            System.IO.Directory.CreateTempSubdirectory("bearing-origin").FullName;

        internal string Directory(string below)
        {
            var path = Path.Combine(_root, below.Replace('/', Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        internal string Write(string below, string content)
        {
            var path = Path.Combine(_root, below.Replace('/', Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Copies an assembly from the test's own output into <paramref name="folder"/>.</summary>
        internal static string Copy(string assembly, string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, assembly);
            File.Copy(Path.Combine(AppContext.BaseDirectory, assembly), target);
            return target;
        }

        public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);
    }
}

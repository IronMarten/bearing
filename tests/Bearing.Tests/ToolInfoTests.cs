using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The first logic in <c>Bearing.Core</c>, and small enough to show what a Core function is
/// meant to look like: takes its input as an argument, returns a value, touches nothing.
/// </summary>
public sealed class ToolInfoTests
{
    [Theory]
    [InlineData("0.0.1-preview.1+abc1234", "0.0.1-preview.1")]  // what SourceLink emits
    [InlineData("0.1.0", "0.1.0")]                              // no metadata to strip
    [InlineData("1.0.0-rc.1", "1.0.0-rc.1")]                    // prerelease is not metadata
    [InlineData("1.0.0+a+b", "1.0.0")]                          // split on the first '+'
    [InlineData("+abc", "")]                                    // degenerate, but defined
    public void Source_revision_is_stripped_at_the_first_plus(string version, string expected) =>
        Assert.Equal(expected, ToolInfo.StripSourceRevision(version));

    [Fact]
    public void The_running_assembly_reports_its_package_version()
    {
        // Reads Bearing.Core's own metadata rather than the CLI's, because the entry
        // assembly under a test host is the test runner.
        var version = ToolInfo.ReadVersion(typeof(ToolInfo).Assembly);

        Assert.NotEqual(ToolInfo.UnknownVersion, version);
        Assert.DoesNotContain("+", version, StringComparison.Ordinal);
    }

    [Fact]
    public void An_assembly_with_no_informational_version_reports_unknown()
    {
        // Blank, never fake — invariant 6, applied to the smallest thing in the codebase.
        // The alternative is inventing a version number, and a wrong version in a bug report
        // costs more than an obviously absent one.
        var bare = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName("NoVersion"),
            System.Reflection.Emit.AssemblyBuilderAccess.Run);

        Assert.Equal(ToolInfo.UnknownVersion, ToolInfo.ReadVersion(bare));
    }
}

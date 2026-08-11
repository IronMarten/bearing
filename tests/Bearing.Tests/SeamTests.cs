using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Bearing.Tests;

/// <summary>
/// The architecture, asserted rather than described.
/// </summary>
/// <remarks>
/// <para>
/// <c>Bearing.Core</c> computes and <c>Bearing.Cli</c> renders. That is the load-bearing
/// decision in <c>docs/ARCHITECTURE.md</c>, and every deliverable — terminal output, JSON,
/// the HTML report, the dependency graph — assumes it holds.
/// </para>
/// <para>
/// It is a test and not a review convention because the review convention already lost once.
/// <c>Report.cs</c> is 997 of the probe's 2,515 lines, and the computation that ended up in
/// there did not arrive in one deliberate commit — <c>ComputeCohortStats</c> and the
/// per-project I/A/D numbers are still, today, computed while printing. Nobody decided that.
/// It accumulated one reasonable-looking line at a time, which is what this catches.
/// </para>
/// <para>
/// Reads compiled metadata rather than source text. The compiler only emits a type reference
/// for a type that is actually used, so a mention in a comment or a string cannot trip it and
/// a real call cannot hide from it.
/// </para>
/// </remarks>
public sealed class SeamTests
{
    /// <summary>
    /// Types Bearing.Core may not touch, and what each one would mean.
    /// </summary>
    private static readonly (string TypeName, string Why)[] ForbiddenInCore =
    [
        ("System.Console",
            "Core would be deciding how something looks. Return the data and let a renderer "
            + "in Bearing.Cli present it — that is the whole seam."),
    ];

    [Fact]
    public void Core_does_not_reference_forbidden_types()
    {
        var referenced = TypeReferencesOf(CoreAssemblyPath);

        var violations = ForbiddenInCore
            .Where(f => referenced.Contains(f.TypeName))
            .Select(f => $"  {f.TypeName}{Environment.NewLine}    {f.Why}")
            .ToList();

        Assert.True(violations.Count == 0,
            "Bearing.Core references types it is not allowed to use:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Core_does_not_depend_on_the_cli()
    {
        // The dependency runs one way. Bearing.Cli's assembly name is `bearing`, which is
        // also the tool command — matched case-insensitively so a future rename to
        // Bearing.Cli.dll is caught too.
        var assemblies = AssemblyReferencesOf(CoreAssemblyPath);

        Assert.DoesNotContain("bearing", assemblies, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearing.Cli", assemblies, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Core_project_declares_no_reference_to_a_cli_project()
    {
        // Belt and braces, and it fails earlier and more legibly than the metadata check:
        // an unused project reference leaves no trace in the IL, so the assembly-level test
        // above would stay green while the csproj said otherwise.
        //
        // Reads the ProjectReference items rather than grepping the file. The first version
        // of this test did grep, and it failed on the comment in Bearing.Core.csproj that
        // explains the rule — a naive substring match firing on a mention of the thing
        // rather than a use of it. That is precisely the false positive TECHREQ-job-a.md 5.6
        // forbids in dead-code detection, so it should not survive in our own suite either.
        var csproj = XDocument.Load(
            Path.Combine(RepoPaths.Root, "src", "Bearing.Core", "Bearing.Core.csproj"));

        var referenced = csproj.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenced,
            path => path.Contains(".Cli", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_seam_test_is_actually_looking_at_something()
    {
        // A guard on the guards. Every assertion above passes trivially against an assembly
        // that does not exist or holds no code, so failure would look exactly like success
        // at the moment Core starts to matter.
        Assert.True(File.Exists(CoreAssemblyPath),
            $"Bearing.Core.dll was not found at {CoreAssemblyPath}. The seam tests above "
            + "cannot fail without it, so they prove nothing until this is fixed.");

        Assert.NotEmpty(TypeReferencesOf(CoreAssemblyPath));
    }

    private static string CoreAssemblyPath =>
        Path.Combine(RepoPaths.BinDirectory, "Bearing.Core.dll");

    /// <summary>Every type the assembly's IL actually refers to, as <c>Namespace.Name</c>.</summary>
    private static HashSet<string> TypeReferencesOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);
            var ns = reader.GetString(reference.Namespace);
            var name = reader.GetString(reference.Name);

            names.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        return names;
    }

    private static HashSet<string> AssemblyReferencesOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        return reader.AssemblyReferences
            .Select(h => reader.GetString(reader.GetAssemblyReference(h).Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

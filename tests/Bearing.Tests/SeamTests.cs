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
        // System.Text.StringBuilder was the fourth candidate and it is not expressible. Core
        // references it in every record's compiler-generated ToString and PrintMembers —
        // Acknowledgment, Judged, AnalysisPolicy, TypeShape, CohortCandidate, CohortSubject and
        // the rest — so the reference is not Core's to remove, and a member-level entry fails
        // the same way because the generated code calls Append. Same shape as System.Environment
        // in the call list below, and the general rule is in CONTRIBUTING.md: a type the compiler
        // emits for a language feature cannot be banned by either list, however much you would
        // like to ban it.
        ("System.Drawing",
            "Geometry is presentation. The map, the mosaic and the plot decide their own layout "
            + "in Bearing.Cli from a model that knows nothing about pixels — ARCHITECTURE.md §3 — "
            + "and a size or a colour computed in Core would be judgement wearing a renderer's "
            + "clothes. Matched as a namespace prefix, because it is the whole namespace that is "
            + "wrong here rather than any one type in it."),
    ];

    /// <summary>
    /// Calls Bearing.Core may not make, where the type itself is legitimate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a second list rather than another entry in the first.</b> The first attempt at the
    /// entry below forbade <c>System.Environment</c> outright and failed immediately — on
    /// <c>get_CurrentManagedThreadId</c>, which the compiler emits into every async state machine.
    /// Core is full of async, so the type reference is not Core's to remove and a type-level ban
    /// there can only ever be a gate nobody can satisfy. The call is what §5 forbids, so the call
    /// is what this matches.
    /// </para>
    /// <para>
    /// Same evidence as above — compiled metadata, so a mention in a comment cannot trip it — one
    /// level finer. <see cref="MemberReference"/> carries the declaring type and the member name,
    /// which is exactly the granularity "not from the environment" is stated at.
    /// </para>
    /// </remarks>
    private static readonly (string TypeName, string Member, string Why)[] ForbiddenCallsInCore =
    [
        ("System.Environment", "GetEnvironmentVariable",
            "Core would be reading the machine instead of its arguments, and ARCHITECTURE.md §5 "
            + "requires the same inputs to give the same output every time. This entry exists "
            + "because Core did read one: OriginOfPath took NUGET_PACKAGES out of the "
            + "environment, so two machines classified the same external reference differently "
            + "and the classification reached the integration map. It is "
            + "WalkOptions.NuGetCachePath now, filled by the host — which is where any other "
            + "environment read belongs too."),
        ("System.Environment", "GetEnvironmentVariables",
            "The bulk form of the above, and the way round it if only the single read were "
            + "listed. Same rule, same remedy: take it as an argument."),
    ];

    [Fact]
    public void Core_does_not_reference_forbidden_types()
    {
        var referenced = TypeReferencesOf(CoreAssemblyPath);

        // Exact match or namespace prefix. `System.Console` is a type and matches itself;
        // `System.Drawing` is a namespace and has to match everything under it, because the
        // entry is about the whole namespace rather than about any one type in it. A prefix
        // cannot over-match here: `System.Console` has no types nested beneath it.
        var violations = ForbiddenInCore
            .Where(f => referenced.Any(r =>
                string.Equals(r, f.TypeName, StringComparison.Ordinal)
                || r.StartsWith(f.TypeName + ".", StringComparison.Ordinal)))
            .Select(f => $"  {f.TypeName}{Environment.NewLine}    {f.Why}")
            .ToList();

        Assert.True(violations.Count == 0,
            "Bearing.Core references types it is not allowed to use:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Core_does_not_make_forbidden_calls()
    {
        var called = MemberReferencesOf(CoreAssemblyPath);

        var violations = ForbiddenCallsInCore
            .Where(f => called.Contains((f.TypeName, f.Member)))
            .Select(f => $"  {f.TypeName}.{f.Member}{Environment.NewLine}    {f.Why}")
            .ToList();

        Assert.True(violations.Count == 0,
            "Bearing.Core makes calls it is not allowed to make:"
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
        Assert.NotEmpty(MemberReferencesOf(CoreAssemblyPath));
    }

    [Fact]
    public void The_forbidden_call_check_finds_the_call_where_it_is_allowed_to_live()
    {
        // The mutation test for Core_does_not_make_forbidden_calls, and the positive half of
        // the rule. "Not from the environment" is only half a decision; the other half is that
        // the host does it, so NUGET_PACKAGES is read exactly once, in CommandLine.cs, and
        // handed to Core as WalkOptions.NuGetCachePath.
        //
        // Asserting it here means the detector is shown to detect against a real assembly
        // rather than passing because nothing anywhere makes the call — which is the shape
        // docs/TESTING.md §9 rejects. Delete the read in CommandLine.cs and this fails; move it
        // back into Core and the test above fails. One of the two always fires.
        var called = MemberReferencesOf(CliAssemblyPath);

        Assert.Contains(("System.Environment", "GetEnvironmentVariable"), called);
    }

    private static string CoreAssemblyPath =>
        Path.Combine(RepoPaths.BinDirectory, "Bearing.Core.dll");

    /// <summary>The Cli assembly. <c>bearing</c> is the assembly name and the tool command.</summary>
    private static string CliAssemblyPath =>
        Path.Combine(RepoPaths.BinDirectory, "bearing.dll");

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

    /// <summary>
    /// Every member the assembly's IL calls on a type it does not declare, as
    /// <c>(Namespace.Type, Member)</c>.
    /// </summary>
    /// <remarks>
    /// Only <see cref="HandleKind.TypeReference"/> parents are read. A member reference can also
    /// hang off a type specification (a constructed generic) or a module, and neither can name
    /// the plain BCL static this list is about — including them would mean resolving signatures
    /// to get back to the same answer.
    /// </remarks>
    private static HashSet<(string Type, string Member)> MemberReferencesOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var calls = new HashSet<(string, string)>();

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;

            var declaring = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            var ns = reader.GetString(declaring.Namespace);
            var name = reader.GetString(declaring.Name);

            calls.Add((
                string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}",
                reader.GetString(member.Name)));
        }

        return calls;
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

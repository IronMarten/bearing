using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using IronMarten.Bearing;

namespace Bearing.Tests;

/// <summary>
/// The claim <see cref="SolutionModel.WithPolicy"/> rests on: one policy value is baked into a
/// built model, and it is <see cref="AnalysisPolicy.CohortBasisFloor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this needs a test at all.</b> <c>WithPolicy</c> re-judges an existing model under a new
/// policy instead of re-reading the solution, which is sound only while every other policy value
/// is read <i>after</i> the model exists — lazily on the model, or by a detector. The day someone
/// adds a policy read inside <c>ModelBuilder</c>, that stops being true, and the failure is silent:
/// a sweep comparing two models that differ in a value neither of them applied. No output looks
/// wrong; the measurement is simply of nothing.
/// </para>
/// <para>
/// <b>Asserted over the IL rather than over behaviour, deliberately.</b> The behavioural form —
/// walk with each of the 29 values moved and check the model is unchanged — costs 29 workspace
/// loads, which is the cost this seam exists to remove, so the guard would eat the win. Reading
/// the compiled metadata answers the same question in milliseconds and answers it for every value
/// at once, including the ones no fixture could exercise. The technique is <c>SeamTests</c>'.
/// </para>
/// <para>
/// <b>Scope: the two types that build a model.</b> <c>SolutionWalker</c> opens the workspace and
/// <c>ModelBuilder</c> turns it into a <see cref="SolutionModel"/>. Nothing else runs before a
/// model exists. <c>Cohorts.Assign</c> is deliberately outside that scope and cannot read a policy
/// at all — it takes the floor as an <c>int</c> parameter, which is the shape that makes this
/// checkable.
/// </para>
/// </remarks>
[Collection(FixtureCollection.Name)]
public sealed class PolicySeamTests(CoreWalkFixture core)
{
    /// <summary>
    /// The only <see cref="AnalysisPolicy"/> member the model-building path may read.
    /// </summary>
    /// <remarks>
    /// <c>Validate</c> is a method rather than a threshold — it asserts the policy is coherent and
    /// reads every value to do it, which is why it is named here rather than excluded by kind.
    /// </remarks>
    private static readonly HashSet<string> AllowedDuringBuild = new(StringComparer.Ordinal)
    {
        "get_" + nameof(AnalysisPolicy.CohortBasisFloor),
        nameof(AnalysisPolicy.Validate),
    };

    [Fact]
    public void The_model_building_path_reads_one_policy_value()
    {
        var read = PolicyMembersReadBy("SolutionWalker", "ModelBuilder");

        Assert.NotEmpty(read);

        var unexpected = read.Except(AllowedDuringBuild, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unexpected.Count == 0,
            "The model-building path reads policy values SolutionModel.WithPolicy assumes it does "
            + "not, so re-judging a built model under a new policy would silently ignore them: "
            + string.Join(", ", unexpected)
            + ". Either move the read after the model exists, or add the value to WithPolicy's "
            + "refusal beside CohortBasisFloor.");
    }

    /// <summary>The refusal is real, and it names the value rather than failing later.</summary>
    [Fact]
    public void Re_judging_refuses_to_move_the_one_value_that_is_baked_in()
    {
        var model = core.Model;
        var moved = model.Policy with { CohortBasisFloor = model.Policy.CohortBasisFloor + 1 };

        var thrown = Assert.Throws<ArgumentException>(() => model.WithPolicy(moved));
        Assert.Contains("CohortBasisFloor", thrown.Message, StringComparison.Ordinal);

        // And the ordinary case is not refused.
        var fine = model.Policy with { HighCc = model.Policy.HighCc + 1 };
        Assert.Equal(fine.HighCc, model.WithPolicy(fine).Policy.HighCc);
    }

    /// <summary>
    /// Every <see cref="AnalysisPolicy"/> member called from the named types' method bodies.
    /// </summary>
    /// <remarks>
    /// Same-assembly calls carry a <c>MethodDefinition</c> token rather than a
    /// <c>MemberReference</c>, so this maps <c>AnalysisPolicy</c>'s own method definitions to their
    /// tokens and scans each caller's IL for them. Crude — it looks for the four token bytes
    /// anywhere in the body rather than decoding opcodes — and crude in the safe direction: a false
    /// positive fails the test and gets read, where a missed call would not.
    /// </remarks>
    private static HashSet<string> PolicyMembersReadBy(params string[] typeNames)
    {
        var path = Path.Combine(RepoPaths.BinDirectory, "Bearing.Core.dll");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var policyMembers = new Dictionary<int, string>();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (reader.GetString(type.Name) != nameof(AnalysisPolicy)) continue;

            foreach (var m in type.GetMethods())
                policyMembers[MetadataTokens.GetToken(m)] = reader.GetString(reader.GetMethodDefinition(m).Name);
        }

        Assert.NotEmpty(policyMembers);

        var wanted = typeNames.ToHashSet(StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0) continue;

            var declaring = reader.GetTypeDefinition(method.GetDeclaringType());
            if (!wanted.Contains(reader.GetString(declaring.Name))) continue;

            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
            if (il is null) continue;

            for (var i = 0; i + 4 <= il.Length; i++)
                if (policyMembers.TryGetValue(BitConverter.ToInt32(il, i), out var name))
                    found.Add(name);
        }

        return found;
    }
}

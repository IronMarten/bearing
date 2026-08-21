using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TestBed.Core;

/// <summary>
/// The member-level dead-code categories, planted where they can actually be reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here is non-public, and that is the whole point of the file.</b>
/// <c>DeadCodeTraps.cs</c> plants §5.6's three type-level cases and they all pass — by being
/// <i>externally visible</i>, which is the X15 exclusion, rather than by any of the handling the
/// acceptance criterion was written to exercise. At member level the visibility rule reaches a
/// public trap first, so a public trap tests nothing. These are private.
/// </para>
/// <para>
/// <b>Four categories, and they do not all end the same way</b> — an override and a <c>+=</c>
/// handler must not be nominated, a serialisation callback must be nominated <i>with its category
/// named</i>, and a string-dispatched method is nominated with nothing but the standing caveat,
/// which is this tool's honest limit rather than a bug.
/// </para>
/// <para>
/// <b>What is not here is not missing.</b> Operators and conversions must be <c>public static</c>
/// in C#, so X15's exclusion covers them by construction and a plant could not reach the case.
/// Compiler-generated record members have no declaration, so no member row exists for them.
/// Interface and explicit implementations are planted in <c>MemberIdentityTraps.cs</c>.
/// </para>
/// </remarks>
internal abstract class TallyProbe
{
    /// <summary>Overridden below. Callers reach the override through this.</summary>
    internal virtual int Sample() => 0;
}

/// <summary>The four cases, on one type so the plant costs the fixture two.</summary>
internal sealed class SettlementProbe : TallyProbe
{
    private readonly List<string> _seen = [];

    private event Action? Settled;

    internal SettlementProbe() => Settled += OnSettled;

    /// <summary>
    /// EXCLUDED — overrides a base member. Nothing calls it directly and nothing was going to:
    /// callers reach it through <see cref="TallyProbe.Sample"/>.
    /// </summary>
    internal override int Sample() => _seen.Count;

    /// <summary>
    /// NOT NOMINATED — wired with <c>+=</c> in the constructor, which is an ordinary reference and
    /// needs no handling at all. The control for the category: if this ever appears in the
    /// section, method-group references have stopped resolving.
    /// </summary>
    private void OnSettled() => _seen.Add("settled");

    /// <summary>
    /// NOMINATED, with its category named — a serialisation callback. Nothing in this solution
    /// calls it; the attribute is the only sign that anything ever will.
    /// </summary>
    [OnDeserialized]
    private void AfterLoad(StreamingContext context) => _seen.Add("loaded");

    /// <summary>
    /// NOMINATED, and this one is the tool's limit rather than a defect. It is named only by the
    /// string literal in <see cref="Replay"/>, and §5.6's string-literal handling is specified for
    /// <i>type</i> names — which are long and distinctive. Member names are neither: matching
    /// <c>"Name"</c> or <c>"Add"</c> against every member called that would rescue half a codebase
    /// on a coincidence. Left nominated, with the standing "verify before deleting".
    /// </summary>
    private void OnReplayed() => _seen.Add("replayed");

    internal void Replay(string hook)
    {
        if (hook == "OnReplayed") Settled?.Invoke();
    }
}

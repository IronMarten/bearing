namespace ArchProbe;

/// <summary>
/// Per-type accumulator. Types are keyed by fully-qualified name so partials across
/// multiple files aggregate into a single row.
/// </summary>
sealed class TypeMetrics
{
    public string Id = "";
    public string Name = "";
    public string Namespace = "";
    public string Project = "";
    public string File = "";
    public int Line;
    public string TypeKeyword = "";      // class / struct / interface / record
    public bool IsAbstract;

    public string Kind = "Internal";     // ApiBoundary / DataAccess / ExternalCall / Contract / Internal
    public string Cohort = "";
    public string CohortBasis = "";
    public string KindSpan = "";       // significant architectural kinds reached      // impl: / base: / suffix: / ns:

    // Structural role
    public readonly HashSet<string> InboundTypes = new(StringComparer.Ordinal);
    public readonly HashSet<string> OutboundTypes = new(StringComparer.Ordinal);
    public readonly HashSet<string> ExternalNamespaces = new(StringComparer.Ordinal);
    public int InboundRefCount;          // weight, not just distinct callers

    // Internal complexity
    public int Cyclomatic;               // summed over members
    public int MaxMemberCyclomatic;
    public string MaxMemberName = "";
    public int Dsm;                      // destructive mutation of existing state
    public int Transform;                // non-destructive shaping (initializers, `with`)
    public int StaticMutations;          // writes to static mutable state
    public int Loc;

    // Surface / data parameters
    public int MemberCount;
    public int PublicMemberCount;
    public int ExecutableMembers;        // members with a real body — behaviour, not shape
    public int ParamCount;               // summed over public members
    public int DataShape;                // depth-1 expansion of param/return shapes

    public int FanIn => InboundTypes.Count;
    public int FanOut => OutboundTypes.Count;

    /// <summary>
    /// Fan-out excluding abstractions (interfaces, abstract classes) and data contracts.
    /// Depending on an abstraction is the mechanism dependency inversion uses to REDUCE
    /// exposure to change, so counting it as coupling risk penalises the practice that
    /// exists to avoid the risk. Set after all types are known.
    /// </summary>
    public int FanOutEffective;

    /// <summary>
    /// Martin's instability, Ce / (Ce + Ca). 0 = maximally stable (much depends on it, it
    /// depends on little); 1 = maximally unstable. Self-normalizing, so unlike everything
    /// else here it needs no peer cohort — which is what makes it usable on singletons.
    /// NaN when the type is entirely unconnected and the ratio is undefined.
    ///
    /// Computed from effective fan-out. Low instability here means "insulated from change
    /// in what it depends on" — pair it with fan-in before reading it as load-bearing.
    /// </summary>
    public double Instability => FanIn + FanOutEffective == 0
        ? double.NaN
        : (double)FanOutEffective / (FanIn + FanOutEffective);

    /// <summary>The same ratio over raw fan-out, kept for audit.</summary>
    public double InstabilityRaw => FanIn + FanOut == 0 ? double.NaN : (double)FanOut / (FanIn + FanOut);

    // Cohort-relative results, filled in by Report.
    public int CohortSize;
    public double FanInPctl, FanOutPctl, CyclomaticPctl, MaxMemberCyclomaticPctl, DsmPctl, DataShapePctl;
    public double FanInXMedian, FanOutXMedian, CyclomaticXMedian, MaxMemberCyclomaticXMedian, DsmXMedian;

    // Whole-solution position. Weaker than a peer comparison (it compares unlike things),
    // but it is the only relative signal available to a type that has no peers.
    public double GlobalFanInPctl, GlobalMaxCcPctl;
}

/// <summary>
/// Per-method accumulator. The concealed-decision signal often lives at method level
/// (one nasty ResolveX inside an otherwise ordinary normalizer), so it gets its own
/// cohort ranking rather than being averaged into the type.
/// </summary>
sealed class MethodMetrics
{
    public string Id = "";
    public string Name = "";
    public string DeclaringType = "";
    public string DeclaringTypeId = "";
    public string Project = "";
    public string File = "";
    public int Line;
    public string Cohort = "";           // inherited from declaring type
    public string Accessibility = "";

    public int Cyclomatic;
    public int Dsm;
    public int Transform;
    public int StaticMutations;
    public int ParamCount;
    public int Loc;
    public int MaxNestingDepth;

    public int CohortSize;
    public double CyclomaticPctl, DsmPctl;
    public double CyclomaticXMedian, DsmXMedian;
}

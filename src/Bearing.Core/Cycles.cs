namespace IronMarten.Bearing;

/// <summary>
/// A set of subjects that all reach each other — a strongly-connected component of some graph
/// over the solution.
/// </summary>
/// <remarks>
/// <para>
/// The members and nothing else. Job A wants a traversable path — <c>A → B → C → A</c> is what a
/// reader can act on, and "these six namespaces are entangled" is not — but a path is a different
/// computation from a component and inventing one here would mean picking an arbitrary walk
/// through the cycle and presenting it as the cycle. <c>TECHREQ-job-a.md</c> §5.1; the probe has
/// the same gap and the same reason.
/// </para>
/// <para>
/// <b>No truncation.</b> The probe writes six namespace names or eight type names and appends
/// <c>", ..."</c>, so how many members a cycle really has is recoverable but what they are is
/// not. That is <c>docs/DEFECTS.md</c> §3 in the small, and a display cap is the renderer's to
/// apply and disclose.
/// </para>
/// </remarks>
public sealed class Cycle
{
    internal Cycle(IReadOnlyList<SubjectRef> members)
    {
        Members = members;
        Subject = SubjectRef.ForSet(members);
    }

    /// <summary>
    /// The cycle's identity: its members taken jointly, in canonical order.
    /// </summary>
    /// <remarks>
    /// Discovering the same cycle from a different entry point has to produce the same identity,
    /// or a finding about it would be "new" every time the walk started somewhere else. See
    /// <see cref="SubjectRef.ForSet"/>.
    /// </remarks>
    public SubjectRef Subject { get; }

    /// <summary>Everything in the cycle, ordered by identity.</summary>
    public IReadOnlyList<SubjectRef> Members { get; }

    /// <summary>How many subjects are entangled.</summary>
    public int Size => Members.Count;
}

/// <summary>
/// Circular references: the same computation over two different graphs.
/// </summary>
/// <remarks>
/// Namespaces and types are separate questions and the answers do not imply each other. Two
/// namespaces are mutually dependent when any type in one reaches any type in the other, so a
/// namespace cycle can exist with no type cycle anywhere inside it — and a type tangle inside one
/// namespace produces no namespace cycle at all.
/// </remarks>
public static class Cycles
{
    /// <summary>
    /// The smallest component that is a cycle rather than a node.
    /// </summary>
    /// <remarks>
    /// Two by construction and not by tuning: a component of one is a single node, which for the
    /// type graph means a self-reference and for the namespace graph cannot arise at all, since
    /// intra-namespace edges are not part of that graph. Contrast
    /// <see cref="AnalysisPolicy.MinTangle"/>, which is a judgement — mutual pairs and triples of
    /// types are ordinary C# and reporting them buries the signal.
    /// </remarks>
    private const int SmallestRealCycle = 2;

    /// <summary>Mutually dependent namespaces, largest first.</summary>
    /// <remarks>
    /// Namespaces rather than folders or projects: this is the layering question, and a cycle
    /// here means the namespaces cannot be understood, extracted or built independently
    /// whatever directory they live in.
    /// </remarks>
    public static IReadOnlyList<Cycle> AmongNamespaces(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var type in model.Types)
        {
            var from = NamespaceOf(type);
            if (!adjacency.TryGetValue(from, out var reaches))
            {
                adjacency[from] = reaches = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (var target in type.Outbound)
            {
                // A dangling edge names a type that was excluded or did not load. It cannot
                // contribute a namespace, and guessing one from the name would invent an edge.
                if (model.Find(target) is not { } dependency) continue;

                var to = NamespaceOf(dependency);
                if (!string.Equals(from, to, StringComparison.Ordinal)) reaches.Add(to);
            }
        }

        return Componentise(
            adjacency,
            SmallestRealCycle,
            SubjectRef.ForNamespace);
    }

    /// <summary>
    /// Type tangles: groups of at least <see cref="AnalysisPolicy.MinTangle"/> types that all
    /// reach each other, largest first.
    /// </summary>
    public static IReadOnlyList<Cycle> AmongTypes(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var byId = model.Types.ToDictionary(t => t.Subject.Canonical, StringComparer.Ordinal);

        var adjacency = model.Types.ToDictionary(
            t => t.Subject.Canonical,
            t => (IReadOnlyList<string>)t.Outbound
                .Select(target => target.Canonical)
                .Where(byId.ContainsKey)
                .ToList(),
            StringComparer.Ordinal);

        var components = Graphs.StronglyConnected(adjacency, model.Policy.MinTangle);

        return Order(components.Select(members => new Cycle(
            members.Select(id => byId[id].Subject).ToList())));
    }

    private static string NamespaceOf(TypeNode type) =>
        string.IsNullOrEmpty(type.Namespace) ? GlobalNamespace : type.Namespace;

    /// <summary>
    /// What the global namespace is called when it has to be a graph node.
    /// </summary>
    /// <remarks>
    /// The empty string would be a node whose name is indistinguishable from an absent one, and
    /// types in the global namespace do participate in cycles like any others.
    /// </remarks>
    private const string GlobalNamespace = "<global>";

    private static List<Cycle> Componentise(
        Dictionary<string, HashSet<string>> adjacency,
        int minSize,
        Func<string, SubjectRef> subjectOf)
    {
        var components = Graphs.StronglyConnected(
            adjacency.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.ToList(),
                StringComparer.Ordinal),
            minSize);

        return Order(components.Select(members => new Cycle(members.Select(subjectOf).ToList())));
    }

    /// <summary>
    /// Largest first, then by identity.
    /// </summary>
    /// <remarks>
    /// A total order, and it is the model's rather than a renderer's because every renderer wants
    /// the same answer to "which is the worst one" and three of them deriving it separately is
    /// how the layout that renders three ways from one dataset happens. The probe applies exactly
    /// this order while printing; moving it here changes nothing except who owns it.
    /// </remarks>
    private static List<Cycle> Order(IEnumerable<Cycle> cycles) =>
        cycles
            .OrderByDescending(c => c.Size)
            .ThenBy(c => c.Subject.Canonical, StringComparer.Ordinal)
            .ToList();
}

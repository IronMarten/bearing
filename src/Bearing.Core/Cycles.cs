namespace IronMarten.Bearing;

/// <summary>
/// A set of subjects that all reach each other — a strongly-connected component of some graph
/// over the solution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership and one walk through it.</b> Tarjan answers "these six namespaces are mutually
/// entangled", which is true and which a reader cannot act on; <c>TECHREQ-job-a.md</c> §5.1 asks
/// for <c>A → B → C → A</c>, which names an edge they can go and delete. Both are carried
/// because they are different claims: <see cref="Members"/> is the extent of the problem and
/// <see cref="Path"/> is one instance of it.
/// </para>
/// <para>
/// <b>The objection that deferred the path is answered rather than withdrawn.</b> It was that a
/// component holds many cycles, so any one of them is an arbitrary walk presented as the cycle.
/// It is still true. What makes it safe is that the choice is now stated — shortest, through the
/// component's first member by identity — and that <see cref="PathCoversEveryMember"/> lets a
/// renderer say when the walk is smaller than the entanglement instead of letting a reader
/// assume it is the whole of it.
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
    internal Cycle(IReadOnlyList<SubjectRef> members, IReadOnlyList<SubjectRef> path)
    {
        Members = members;
        Path = path;
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

    /// <summary>
    /// One traversable loop through this component, as a walk: <c>[A, B, C]</c> means
    /// <c>A → B → C → A</c>. The last member depends on the first; the renderer closes it.
    /// </summary>
    /// <remarks>
    /// Shortest, through <c>Members[0]</c>. Never empty for a component of two or more, since
    /// every member of a strongly-connected component lies on a cycle — but it may be far
    /// shorter than <see cref="Members"/>, and <see cref="PathCoversEveryMember"/> is how a
    /// caller knows.
    /// </remarks>
    public IReadOnlyList<SubjectRef> Path { get; }

    /// <summary>
    /// Whether the loop visits everything that is entangled, or only part of it.
    /// </summary>
    /// <remarks>
    /// Invariant 4 in the small: a two-name loop printed under a six-namespace component, with
    /// nothing saying the other four are also in it, tells a reader that breaking one edge fixes
    /// the problem. The size and the walk are both true and they are not the same number, so
    /// whichever is shown has to be the one it is labelled as.
    /// </remarks>
    public bool PathCoversEveryMember => Path.Count == Members.Count;

    /// <summary>How many subjects are entangled.</summary>
    public int Size => Members.Count;
}

/// <summary>
/// Circular references: the same computation over two different graphs.
/// </summary>
/// <remarks>
/// <para>
/// Namespaces, types and projects are separate questions and the answers do not imply each other.
/// Two namespaces are mutually dependent when any type in one reaches any type in the other, so a
/// namespace cycle can exist with no type cycle anywhere inside it — and a type tangle inside one
/// namespace produces no namespace cycle at all.
/// </para>
/// <para>
/// <b>Project cycles are the third, and the reason they exist is worth stating.</b> "MSBuild
/// forbids them" is true of project <i>references</i> and only of those. This tool does not build
/// the reference graph; it builds a type-reference graph and aggregates it, and that graph cycles
/// whenever two projects each contain a type naming one in the other — which is legal, buildable,
/// and usually a layering violation the reference graph is too coarse to see. It reaches an
/// analysed type without a project reference when the assembly is resolved some other way: a
/// binary or package reference to something a project in this solution also builds.
/// <c>PRD-free-tier.md</c> §7.1.
/// </para>
/// <para>
/// <b>Not to be confused with what <c>docs/DEFECTS.md</c> §1 fabricated.</b> Keying type identity
/// on the fully-qualified name alone merged two same-named types across assemblies and attributed
/// one's edges to the other's project, which invented a five-project cycle on nopCommerce. That
/// was a defect and this is a feature, and the only thing keeping them apart is that
/// <see cref="SubjectRef.ForType"/> is what the walkers key on — which is the state of the code
/// and not a promise about it.
/// </para>
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

        return Componentise(adjacency, SmallestRealCycle, SubjectRef.ForNamespace);
    }

    /// <summary>
    /// Mutually dependent projects, largest first — the type graph aggregated to the projects
    /// that declare its endpoints.
    /// </summary>
    /// <remarks>
    /// Not gated by a policy value. Two projects each naming a type in the other is the whole of
    /// the finding: unlike a type tangle, there is no size at which it becomes ordinary.
    /// </remarks>
    public static IReadOnlyList<Cycle> AmongProjects(SolutionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return AmongProjects(
            model.Types.Select(t => (t.Subject.Canonical, t.Project)),
            model.Edges.Select(e => (e.From.Canonical, e.To.Canonical)));
    }

    /// <summary>
    /// The same computation over primitives.
    /// </summary>
    /// <param name="types">Each analysed type's identity and the project that declares it.</param>
    /// <param name="edges">Each dependency, by type identity at both ends.</param>
    /// <remarks>
    /// <b>Primitives rather than a model, and for the reason <see cref="ProjectReachability"/> is
    /// the same shape.</b> A solution that compiles usually has no project cycle at all — every
    /// ordinary cross-project edge follows a project reference, so the aggregate is the reference
    /// DAG — and the fixture is one of those. Taking a model would leave this testable only
    /// against a graph with nothing in it, which is a test that passes by having no case rather
    /// than by being right. <c>docs/TESTING.md</c> §6 carries the gap; this signature is what
    /// lets the cycle itself be constructed and asserted.
    /// </remarks>
    public static IReadOnlyList<Cycle> AmongProjects(
        IEnumerable<(string TypeId, string Project)> types,
        IEnumerable<(string From, string To)> edges)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(edges);

        var projectOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (typeId, project) in types)
        {
            projectOf[typeId] = project;
            if (!adjacency.ContainsKey(project))
                adjacency[project] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, to) in edges)
        {
            // Both endpoints have to be analysed types. An edge to something the walk never
            // declared has no project — DEFECTS.md §7 — and inventing one from the name is how
            // §1 fabricated a cycle in the first place.
            if (!projectOf.TryGetValue(from, out var fromProject)) continue;
            if (!projectOf.TryGetValue(to, out var toProject)) continue;

            if (!string.Equals(fromProject, toProject, StringComparison.Ordinal))
                adjacency[fromProject].Add(toProject);
        }

        return Componentise(adjacency, SmallestRealCycle, SubjectRef.ForProject);
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

        return Build(adjacency, model.Policy.MinTangle, id => byId[id].Subject);
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
        Func<string, SubjectRef> subjectOf) =>
        Build(
            adjacency.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.ToList(),
                StringComparer.Ordinal),
            minSize,
            subjectOf);

    /// <summary>
    /// Components into cycles: membership from Tarjan, and one walk through each.
    /// </summary>
    /// <remarks>
    /// The single place a <see cref="Cycle"/> is made, so that no graph can acquire a
    /// membership set without a path or a path computed against a different adjacency than the
    /// one the component came from.
    /// </remarks>
    private static List<Cycle> Build(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency,
        int minSize,
        Func<string, SubjectRef> subjectOf)
    {
        var components = Graphs.StronglyConnected(adjacency, minSize);

        return Order(components.Select(members => new Cycle(
            members.Select(subjectOf).ToList(),
            PathThrough(adjacency, members).Select(subjectOf).ToList())));
    }

    /// <summary>
    /// One loop through a component, found in the subgraph induced on it.
    /// </summary>
    /// <remarks>
    /// <b>Induced, not the whole graph.</b> A component's members reach nodes outside it, and a
    /// walk that left the component and came back would still be a real cycle — but it would be a
    /// different, larger one, and reporting it as this component's is exactly the "arbitrary walk
    /// presented as the cycle" the deferral was about. The seed is <c>members[0]</c>, which
    /// <see cref="Graphs.StronglyConnected"/> guarantees is the ordinal minimum.
    /// </remarks>
    private static IReadOnlyList<string> PathThrough(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency,
        IReadOnlyList<string> members)
    {
        var inside = members.ToHashSet(StringComparer.Ordinal);

        var induced = members.ToDictionary(
            m => m,
            m => (IReadOnlyList<string>)(adjacency.TryGetValue(m, out var reaches)
                ? reaches.Where(inside.Contains).ToList()
                : []),
            StringComparer.Ordinal);

        return Graphs.ShortestCycleThrough(induced, members[0]);
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

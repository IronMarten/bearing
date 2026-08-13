namespace IronMarten.Bearing;

/// <summary>
/// Graph algorithms over the structure model.
/// </summary>
public static class Graphs
{
    /// <summary>
    /// Tarjan's strongly-connected components, iterative, returning only components at or above
    /// <paramref name="minSize"/>.
    /// </summary>
    /// <param name="adjacency">
    /// Every node, with the nodes it reaches. A node with no outbound edges still belongs here as
    /// a key with an empty list, or it is not part of the graph at all. Edges pointing at keys
    /// that are absent are ignored rather than rejected — real input has dangling targets, since
    /// an edge can name a type that was excluded or failed to load.
    /// </param>
    /// <param name="minSize">
    /// Smallest component worth returning. A component of one is a node rather than a cycle, so
    /// two is the smallest value that means anything; the callers' choices above that are
    /// judgements about noise and live in <see cref="AnalysisPolicy"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Iterative rather than recursive on purpose: recursion depth is bounded by the longest
    /// dependency path, and a large solution can exceed the stack long before it exceeds anything
    /// else. The fixture pins this at 50,000 deep.
    /// </para>
    /// <para>
    /// <b>The result is canonical, and that is not cosmetic.</b> A strongly-connected component
    /// is a set, and Tarjan hands it back as a stack-pop sequence — both the order within a
    /// component and the order components are discovered in are artefacts of which root the outer
    /// loop reached first, which is dictionary enumeration order, which is insertion order, which
    /// is project load order. The partition is a property of the graph; none of the ordering
    /// around it is. Sorting here means no caller can inherit a visit-order dependence, and since
    /// components are disjoint, ordering on the first element is total.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<string>> StronglyConnected(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency, int minSize)
    {
        ArgumentNullException.ThrowIfNull(adjacency);

        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowlink = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var component = new Stack<string>();
        var result = new List<List<string>>();

        foreach (var root in adjacency.Keys)
        {
            if (indices.ContainsKey(root)) continue;

            var work = new List<Frame> { new(root) };

            while (work.Count > 0)
            {
                var frame = work[^1];
                var v = frame.Node;

                if (frame.Child == 0)
                {
                    indices[v] = lowlink[v] = index++;
                    component.Push(v);
                    onStack.Add(v);
                }

                var children = adjacency.TryGetValue(v, out var list) ? list : [];
                var descended = false;

                while (frame.Child < children.Count)
                {
                    var w = children[frame.Child];
                    frame.Child++;

                    if (!indices.ContainsKey(w))
                    {
                        work.Add(new Frame(w));
                        descended = true;
                        break;
                    }

                    if (onStack.Contains(w) && indices[w] < lowlink[v]) lowlink[v] = indices[w];
                }

                if (descended) continue;

                work.RemoveAt(work.Count - 1);

                if (work.Count > 0)
                {
                    var parent = work[^1].Node;
                    if (lowlink[v] < lowlink[parent]) lowlink[parent] = lowlink[v];
                }

                if (lowlink[v] != indices[v]) continue;

                var scc = new List<string>();
                string popped;
                do
                {
                    popped = component.Pop();
                    onStack.Remove(popped);
                    scc.Add(popped);
                } while (!string.Equals(popped, v, StringComparison.Ordinal));

                if (scc.Count >= minSize) result.Add(scc);
            }
        }

        foreach (var scc in result) scc.Sort(StringComparer.Ordinal);
        result.Sort((a, b) => StringComparer.Ordinal.Compare(a[0], b[0]));

        return result;
    }

    /// <summary>
    /// The shortest cycle through <paramref name="seed"/>, as a walk: <c>A, B, C</c> for
    /// <c>A → B → C → A</c>. Empty when the seed is on no cycle at all.
    /// </summary>
    /// <param name="adjacency">
    /// The graph to walk. Callers pass the subgraph induced on one component, so every node
    /// reaches every other and the answer is guaranteed to exist.
    /// </param>
    /// <param name="seed">Where the walk starts and ends.</param>
    /// <remarks>
    /// <para>
    /// <b>Why a path at all.</b> Tarjan answers "these six namespaces are mutually entangled",
    /// and a reader cannot act on that — <c>TECHREQ-job-a.md</c> §5.1 asks for
    /// <c>A → B → C → A</c>, which names an edge they can go and delete. The reason this was
    /// deferred is real and is not dissolved by implementing it: a component holds many cycles,
    /// and any one of them is a choice. So the choice is made where it can be stated — shortest,
    /// through the component's first member by identity — and the caller is told when the walk is
    /// shorter than the component so it can say so rather than imply the path *is* the cycle.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> Breadth-first gives the shortest, the seed is the ordinal minimum of
    /// a set rather than whatever Tarjan popped first, and ties between equal-length walks are
    /// broken by visiting neighbours in ordinal order. None of the three is cosmetic: a
    /// representative that moved between runs would make an acknowledged finding come back.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> ShortestCycleThrough(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency, string seed)
    {
        ArgumentNullException.ThrowIfNull(adjacency);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);

        if (!adjacency.ContainsKey(seed)) return [];

        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            foreach (var next in Neighbours(adjacency, node))
            {
                if (string.Equals(next, seed, StringComparison.Ordinal))
                {
                    // A self-reference is a component of one, which is not a cycle this tool
                    // reports; every caller filters those out before getting here.
                    if (string.Equals(node, seed, StringComparison.Ordinal)) continue;

                    return Walk(previous, node, seed);
                }

                if (previous.ContainsKey(next)) continue;

                previous[next] = node;
                queue.Enqueue(next);
            }
        }

        return [];
    }

    private static IEnumerable<string> Neighbours(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency, string node) =>
        adjacency.TryGetValue(node, out var next)
            ? next.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal)
            : [];

    private static List<string> Walk(IReadOnlyDictionary<string, string> previous, string last, string seed)
    {
        var reversed = new List<string> { last };

        while (!string.Equals(reversed[^1], seed, StringComparison.Ordinal))
            reversed.Add(previous[reversed[^1]]);

        reversed.Reverse();
        return reversed;
    }

    private sealed class Frame(string node)
    {
        public string Node { get; } = node;

        public int Child { get; set; }
    }
}

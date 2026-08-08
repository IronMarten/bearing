namespace ArchProbe;

static class Graphs
{
    /// <summary>
    /// Tarjan's strongly-connected components, iterative.
    ///
    /// Iterative rather than recursive on purpose: recursion depth here is bounded by the
    /// longest dependency path, and a large solution can exceed the stack long before it
    /// exceeds anything else. Returns only components at or above minSize — a
    /// single-element component is just a node, and on a real codebase mutual pairs are
    /// so common they would drown the finding.
    /// </summary>
    public static List<List<string>> StronglyConnected(
        IReadOnlyDictionary<string, List<string>> adjacency, int minSize)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowlink = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var component = new Stack<string>();
        var result = new List<List<string>>();

        foreach (var root in adjacency.Keys)
        {
            if (indices.ContainsKey(root)) continue;

            var work = new List<Frame> { new(root, 0) };

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

                var children = adjacency.TryGetValue(v, out var list) ? list : EmptyList;
                var descended = false;

                while (frame.Child < children.Count)
                {
                    var w = children[frame.Child];
                    frame.Child++;

                    if (!indices.ContainsKey(w))
                    {
                        work.Add(new Frame(w, 0));
                        descended = true;
                        break;
                    }

                    if (onStack.Contains(w) && indices[w] < lowlink[v])
                        lowlink[v] = indices[w];
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

        return result;
    }

    static readonly List<string> EmptyList = new();

    sealed class Frame
    {
        public readonly string Node;
        public int Child;
        public Frame(string node, int child) { Node = node; Child = child; }
    }
}

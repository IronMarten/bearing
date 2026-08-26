"""Does the project map's geometry still say what the model says?

    python tools/map-geometry.py <diagram.svg> [<model.json>]

`<diagram.svg>` is a file `bearing --diagram` wrote and `<model.json>` a file
`bearing --json` wrote, from the same run. The SVG half needs no model and is the
half that matters: it measures the shipped drawing rather than a reimplementation
of the code that produced it.

Three defects lived in this drawing and all three were invisible to the suite,
because the fixture is three projects in a chain and cannot produce the shapes:

  * **an edge drawn through the box in the way.** Boxes are opaque. Painted last
    they cut a layer-skipping line in half, and two stubs either side of a box read
    as a dependency in and another out -- a direct dependency reading as a chain
    through a project it never names. It was 18 of 29 lines on nopCommerce, 81 of
    98 on Jellyfin, 27 of 44 on Umbraco.
  * **a layer too wide for one row, drawn as two.** A row means *depends on the row
    below* everywhere else on the drawing, so a wrapped layer states a dependency
    the code does not have. Jellyfin's layer 4 holds eleven boxes.
  * **a drawing that quietly shows a third of the edges.** The map draws the
    transitive reduction, which preserves reachability exactly -- but only while it
    really is the reduction, and only while the caption still discloses the rest.

The figures those fixes are recorded with sit in doc comments with no test beside
them, and `ARCHITECTURE.md` says to assume any such figure is stale. This exists so
the next reading is a re-run rather than an argument.

Exit code is 1 if any invariant below fails, so it can be run in anger.
"""
import json, re, sys, collections

# Geometry constants mirror ArchitectureDiagram. They are read back out of the SVG
# where possible; MAX_PER_ROW cannot be, and a change to it changes what wraps.
MAX_PER_ROW = 5
SAMPLES = 400

RECT = re.compile(r'<rect class="(?P<cls>[^"]*)" x="(?P<x>\d+)" y="(?P<y>\d+)" '
                  r'width="(?P<w>\d+)" height="(?P<h>\d+)"')
EDGE = re.compile(r'<path class="ed" d="M(\d+) (\d+) C\d+ \d+ \d+ \d+ (\d+) (\d+)"')
RULE = re.compile(r'<path class="lr" d="M0 (\d+)')
NAME = re.compile(r'<text class="nm" x="\d+" y="\d+" text-anchor="middle">([^<]*)</text>')


def curve(p0, p3, n=SAMPLES):
    """The cubic the renderer draws: control points directly below and above the ends."""
    p1, p2 = (p0[0], p0[1] + 20), (p3[0], p3[1] - 20)
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        yield (u*u*u*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t*t*t*p3[0],
               u*u*u*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t*t*t*p3[1])


def read_svg(path):
    text = open(path, encoding='utf-8').read()
    boxes = [{'x': int(m['x']), 'y': int(m['y']), 'w': int(m['w']), 'h': int(m['h'])}
             for m in RECT.finditer(text)]
    for box, name in zip(boxes, NAME.findall(text)):
        box['name'] = name
    edges = [tuple(int(v) for v in m) for m in EDGE.findall(text)]
    rules = sorted(int(y) for y in RULE.findall(text))
    return boxes, edges, rules


def crossings(boxes, edges):
    """Edges whose curve passes through a box that is neither of its endpoints."""
    hit = []
    for (x1, y1, x2, y2) in edges:
        through = set()
        for (px, py) in curve((x1, y1), (x2, y2)):
            # The endpoints sit exactly on their own box borders; those are not crossings.
            if abs(py - y1) < 1 or abs(py - y2) < 1:
                continue
            for box in boxes:
                if (box['x'] <= px <= box['x'] + box['w']
                        and box['y'] <= py <= box['y'] + box['h']):
                    through.add(box.get('name', f"{box['x']},{box['y']}"))
        if through:
            hit.append(((x1, y1, x2, y2), sorted(through)))
    return hit


def rows_of(boxes):
    """Boxes grouped by their top edge, top of the drawing first."""
    by_y = collections.defaultdict(list)
    for box in boxes:
        by_y[box['y']].append(box)
    return [by_y[y] for y in sorted(by_y)]


# ---------------------------------------------------------------- the model half

def project_graph(model):
    """The dependency graph between projects, aggregated from type edges."""
    project_of = {t['id']: t['project'] for t in model['types']}
    deps = {p: set() for p in project_of.values()}
    for edge in model['edges']:
        source, target = project_of.get(edge['from']), project_of.get(edge['to'])
        if source is None or target is None or source == target:
            continue
        deps[source].add(target)
    return {k: sorted(v) for k, v in deps.items()}


def components(adjacency):
    """Tarjan, iterative, canonically ordered -- ProjectGraph condenses before layering."""
    index, idx, low, on, stack, out = [0], {}, {}, set(), [], []
    for root in adjacency:
        if root in idx:
            continue
        work = [[root, 0]]
        while work:
            v = work[-1][0]
            if work[-1][1] == 0:
                idx[v] = low[v] = index[0]
                index[0] += 1
                stack.append(v)
                on.add(v)
            children, descended = adjacency.get(v, []), False
            while work[-1][1] < len(children):
                w = children[work[-1][1]]
                work[-1][1] += 1
                if w not in idx:
                    work.append([w, 0])
                    descended = True
                    break
                if w in on and idx[w] < low[v]:
                    low[v] = idx[w]
            if descended:
                continue
            work.pop()
            if work and low[v] < low[work[-1][0]]:
                low[work[-1][0]] = low[v]
            if low[v] != idx[v]:
                continue
            group = []
            while True:
                popped = stack.pop()
                on.discard(popped)
                group.append(popped)
                if popped == v:
                    break
            out.append(sorted(group))
    return sorted(out, key=lambda c: c[0])


def layers(deps):
    """Longest-path depth over the condensation: 0 depends on nothing."""
    comps = components(deps)
    comp_of = {p: i for i, c in enumerate(comps) for p in c}
    above = {i: set() for i in range(len(comps))}
    for project, targets in deps.items():
        for target in targets:
            if comp_of[project] != comp_of[target]:
                above[comp_of[project]].add(comp_of[target])
    depth = {}

    def deepest(component):
        if component in depth:
            return depth[component]
        depth[component] = 0
        best = max((deepest(n) + 1 for n in above[component]), default=0)
        depth[component] = best
        return best

    return ({p: deepest(comp_of[p]) for p in comp_of},
            comp_of,
            {i: len(c) for i, c in enumerate(comps)})


def fold(deps, layer, comp_of, sizes):
    """Projects of the same shape become one box; a cycle is one box whatever its shape."""
    dependents = collections.defaultdict(set)
    for project, targets in deps.items():
        for target in targets:
            if target in deps:
                dependents[target].add(project)

    groups = collections.defaultdict(list)
    for project in deps:
        if sizes[comp_of[project]] > 1:
            key = (layer[project], str(comp_of[project]), '', '')
        else:
            key = (layer[project], '', ' '.join(deps[project]),
                   ' '.join(sorted(dependents[project])))
        groups[key].append(project)

    boxes = []
    for key, projects in groups.items():
        projects = sorted(projects)
        boxes.append({'projects': projects, 'layer': key[0],
                      'dependsOn': sorted({t for p in projects for t in deps[p]}
                                          - set(projects))})
    return sorted(boxes, key=lambda b: (b['layer'], b['projects'][0]))


def reduction(boxes):
    """Every dependency whose reachability no other path already carries."""
    box_of = {p: i for i, b in enumerate(boxes) for p in b['projects']}
    out = [set() for _ in boxes]
    for i, box in enumerate(boxes):
        for target in box['dependsOn']:
            j = box_of.get(target)
            if j is not None and j != i:
                out[i].add(j)
    reach = {}

    def reaches(i):
        if i in reach:
            return reach[i]
        reach[i] = set()
        seen = set()
        for j in out[i]:
            seen |= {j} | reaches(j)
        reach[i] = seen
        return seen

    kept, implied = [], 0
    for i in range(len(boxes)):
        for j in sorted(out[i]):
            if any(j in reaches(k) for k in out[i] if k != j):
                implied += 1
            else:
                kept.append((i, j))
    return kept, implied


def width_bound(depth, width):
    """Push each node to the shallowest layer at or below its depth that has room.

    This is the remedy that was proposed for the wrapped layer and rejected on
    measurement: it removes the wrap by drawing boxes deeper than they are, which
    trades a misstatement a reader can check against the edges for one that leaves
    no trace on the page.
    """
    # Shallowest first, ties by name: the order decides which of several boxes at one
    # depth takes the push, so it has to be a property of the graph and not of a dict.
    used, out = collections.Counter(), {}
    for node in sorted(depth, key=lambda n: (depth[n], n)):
        level = depth[node]
        while used[level] >= width:
            level += 1
        used[level] += 1
        out[node] = level
    return out


# ---------------------------------------------------------------------- report

def main(argv):
    if not argv:
        print(__doc__)
        return 2

    boxes, edges, rules = read_svg(argv[0])
    rows = rows_of(boxes)
    failures = []

    print(f"{argv[0]}: {len(boxes)} boxes, {len(rows)} rows, {len(edges)} edges, "
          f"{len(rules)} layer rules")

    through = crossings(boxes, edges)
    print(f"  edges crossing a box that is neither endpoint: {len(through)} of {len(edges)}")
    for (edge, names) in through[:8]:
        print(f"      through {', '.join(names)}")
    if len(through) > 8:
        print(f"      ({len(through) - 8} more)")

    # A rule marks every boundary an edge proves. Rows drawn without one above them
    # are a layer continued, and the drawing must not be claiming a dependency there.
    if rules:
        wrapped = [i for i, row in enumerate(rows[1:], start=1)
                   if not any(row[0]['y'] - 40 < y < row[0]['y'] for y in rules)]
        print(f"  rows continuing the layer above (no rule): {len(wrapped)} -> "
              f"{len(rows) - len(wrapped)} layers in {len(rows)} rows")
        if len(rules) != len(rows) - len(wrapped) - 1:
            failures.append(f"{len(rules)} rules for "
                            f"{len(rows) - len(wrapped)} layers: one boundary is unmarked")
    elif len(rows) > 1:
        print("  no layer rules: every gap is a layer boundary, so none needs marking")

    if len(argv) < 2:
        return 1 if failures else 0

    model = json.load(open(argv[1], encoding='utf-8'))
    deps = project_graph(model)
    layer, comp_of, sizes = layers(deps)
    drawn = fold(deps, layer, comp_of, sizes)
    kept, implied = reduction(drawn)

    print(f"  model: {len(deps)} projects folded to {len(drawn)} boxes, "
          f"{max(layer.values()) + 1} layers")
    print(f"  dependencies: {len(kept)} carry reachability, {implied} implied by a path")

    if len(drawn) != len(boxes):
        failures.append(f"the drawing has {len(boxes)} boxes and the model folds to {len(drawn)}")
    if len(kept) != len(edges):
        failures.append(f"the drawing has {len(edges)} edges and the reduction is {len(kept)}: "
                        "it is not drawing the reduction")

    per_layer = collections.Counter(b['layer'] for b in drawn)
    widest = max(per_layer.values())
    print(f"  widest layer: {widest} boxes"
          + (f" -- wraps, since the cap is {MAX_PER_ROW}" if widest > MAX_PER_ROW else ""))

    box_depth = {b['projects'][0]: b['layer'] for b in drawn}
    bounded = width_bound(box_depth, MAX_PER_ROW)
    moved = [n for n in box_depth if bounded[n] != box_depth[n]]
    print(f"  a width bound at {MAX_PER_ROW} would draw {len(moved)} of {len(drawn)} boxes "
          "deeper than they are")
    for n in sorted(moved, key=lambda n: (-(bounded[n] - box_depth[n]), n))[:5]:
        print(f"      {n}: depth {box_depth[n]} -> {bounded[n]}")

    for f in failures:
        print(f"  FAIL {f}")
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

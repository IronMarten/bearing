"""How big is one hop, on the types the report actually shows?

    python tools/ego-reach.py <csv-dir> <model.json>

`<csv-dir>` is a directory `bearing --csv` wrote and `<model.json>` a file
`bearing --json` wrote, **from the same run**. It measures the shipped model
rather than a reimplementation of the code that built it.

A8's whole risk was carried by one line in `TECHREQ-job-a.md` 5.5 -- *one hop
reads, two hops fails* -- and the measurement under it was three seeds on
Jellyfin, taken during the spike before Umbraco existed. That reading is right
about the general population and it answers the wrong question, because a
drill-down is never opened on an arbitrary type. It is opened from a finding,
and a finding is a claim about an extreme.

So this measures three populations, and the gap between them is the point:

  * **every type** -- the spike's population. One hop is small: a median of 5-7
    and a p90 near 25 on all three solutions.
  * **every nominated subject** -- every type some reported finding names.
    Roughly triple the median.
  * **the rendered subjects** -- the first `policy.top` of each kind, which is
    what a reader can actually click. Each detector emits strongest-first, so
    these are the most extreme members of the most extreme population, and
    their neighbourhoods are the largest in the codebase by construction.

The cost figure is what settles whether the view can be pre-rendered into the
single-file report rather than scripted, so it is printed beside each cap.

Exit code is 1 if `edges.csv` and the model's own edge list disagree at all --
if that fires, this is not measuring the model's graph and every figure below is
void.

It also prints, without failing on it, how far a type's `FanOut` column sits
from the edges the same run emits. They are not the same number: `FanOut` counts
distinct outbound references and the edge list carries only the edges whose
target is an analysed type, so the column runs 1-7 higher on 1.0% of
nopCommerce's types, 1.5% of Umbraco's and 6.7% of Jellyfin's, never lower.
**A drill-down must be drawn from the edge list and captioned from it too** --
`fan-out 139` printed beside 136 drawn boxes is D50's defect in a new place.
"""
import csv, json, statistics, sys
from collections import defaultdict

# Bytes per node and per edge in the drawn SVG, taken from the shipped project
# map's own density rather than guessed: a box with its label, and a path with
# its coordinates. Only used for the cost estimate, and only to an order.
BOX_BYTES, EDGE_BYTES = 130, 70

# Caps worth reporting. 25 and 50 are legibility guesses; 100 and 200 exist to
# show how fast the tail falls off, which is what keeps a cap from being a
# magic number -- the sensitivity is printed, not asserted.
CAPS = (25, 50, 100, 200)

# The two edge lists one run emits must be the same list. There is no tolerance
# on this one: --csv and --json render one model, and a difference is D46's
# family rather than a rounding.


def percentile(xs, p):
    xs = sorted(xs)
    k = (len(xs) - 1) * p / 100
    lo, hi = int(k), min(int(k) + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)


def load(csv_dir, model_path):
    rows = list(csv.DictReader(open(f"{csv_dir}/types.csv", encoding="utf-8-sig", newline="")))
    out, inn = defaultdict(set), defaultdict(set)
    for e in csv.DictReader(open(f"{csv_dir}/edges.csv", encoding="utf-8-sig", newline="")):
        a, b = e["From"], e["To"]
        if a != b:                      # the type graph carries no self-edge
            out[a].add(b)
            inn[b].add(a)
    return rows, out, inn, json.load(open(model_path, encoding="utf-8"))


def report(label, sizes, total_types):
    print(f"  {label:22s} n={len(sizes):5d}  median {statistics.median(sizes):5.0f}"
          f"  p90 {percentile(sizes, 90):5.0f}  p95 {percentile(sizes, 95):5.0f}"
          f"  max {max(sizes):5d} ({max(sizes) / total_types * 100:.0f}% of the codebase)")


def main(csv_dir, model_path):
    rows, out, inn, model = load(csv_dir, model_path)
    ids = {r["Id"] for r in rows}
    hop1 = {i: out[i] | inn[i] for i in ids}

    from_json = defaultdict(set)
    for e in model["edges"]:
        if e["from"] != e["to"]:
            from_json[e["from"]].add(e["to"])
    csv_pairs = {(a, b) for a, bs in out.items() for b in bs}
    json_pairs = {(a, b) for a, bs in from_json.items() for b in bs}
    print(f"{model['solutionPath']}")
    print(f"  {len(rows)} types, {len(csv_pairs)} distinct edges")
    if csv_pairs != json_pairs:
        print(f"  FAIL: --csv and --json disagree on "
              f"{len(csv_pairs ^ json_pairs)} edges; not one model")
        return 1

    skew = [(int(r["FanOut"]) - len(out[r["Id"]]), r["Name"]) for r in rows]
    off = [s for s in skew if s[0] != 0]
    print(f"  FanOut column vs drawable edges: {len(off)} types differ "
          f"({len(off) / len(rows) * 100:.1f}%), "
          f"by {min(d for d, _ in off) if off else 0} to {max(d for d, _ in off) if off else 0}; "
          f"the column counts references the edge list cannot draw")

    findings = [f for f in model["findings"]
                if f.get("status") == "reported"
                and (f.get("subject") or {}).get("kind") == "TypeDeclaration"]
    nominated = [s for s in dict.fromkeys(f["subject"]["canonical"] for f in findings) if s in ids]

    top = model["policy"]["top"]
    per_kind = defaultdict(list)
    for f in findings:
        per_kind[f["kind"]].append(f["subject"]["canonical"])
    shown = [s for s in dict.fromkeys(c for v in per_kind.values() for c in v[:top]) if s in ids]

    print(f"\n  one-hop neighbourhood, in + out, excluding self:")
    report("every type", [len(hop1[i]) for i in ids], len(rows))
    report("nominated", [len(hop1[s]) for s in nominated], len(rows))
    report(f"rendered (top {top})", [len(hop1[s]) for s in shown], len(rows))

    print(f"\n  what a cap costs, on the {len(shown)} rendered subjects:")
    for cap in CAPS:
        keep = [s for s in shown if len(hop1[s]) <= cap]
        nodes = sum(len(hop1[s]) + 1 for s in keep)
        edges = sum(len(out[s]) + len(inn[s]) for s in keep)
        kb = (nodes * BOX_BYTES + edges * EDGE_BYTES) / 1024
        print(f"    cap {cap:>3}: draws {len(keep):3d}/{len(shown)} "
              f"({len(keep) / len(shown) * 100:4.0f}%), withholds {len(shown) - len(keep):3d}"
              f"   ~{kb:6.0f} KB of inline SVG")
    nodes = sum(len(hop1[s]) + 1 for s in shown)
    edges = sum(len(out[s]) + len(inn[s]) for s in shown)
    print(f"    no cap : draws {len(shown):3d}/{len(shown)} ( 100%), withholds   0"
          f"   ~{(nodes * BOX_BYTES + edges * EDGE_BYTES) / 1024:6.0f} KB of inline SVG")

    print(f"\n  rendered subjects by kind (median / p90 / max one-hop):")
    by_kind = defaultdict(list)
    for kind, subjects in per_kind.items():
        for s in dict.fromkeys(subjects[:top]):
            if s in ids:
                by_kind[kind].append(len(hop1[s]))
    for kind, v in sorted(by_kind.items(), key=lambda kv: -statistics.median(kv[1])):
        print(f"    {kind:28s} {statistics.median(v):6.0f} {percentile(v, 90):6.0f} "
              f"{max(v):6d}   n={len(v)}")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))

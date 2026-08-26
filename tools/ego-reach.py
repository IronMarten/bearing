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
  * **the leads** -- the single top row of each kind, which is X10's selection
    and what A8 ships against. Being the most extreme row of each kind, these
    are the WORST cases and not typical ones: a median of 66-100, and 19 of
    the 28 distinct leads across the three solutions are over 50 nodes. That
    is what took the drill-down from a drawing to a grouped list, so it is
    measured here rather than remembered. Leads are DEDUPED -- two kinds can
    lead with the same type, as BaseItem does for hubs and blast radius on
    Jellyfin, and counting it twice moves the median.

The cost figure is what settles whether the view can be pre-rendered into the
single-file report rather than scripted, so it is printed beside each cap.

Exit code is 1 if `edges.csv` and the model's own edge list disagree at all --
if that fires, this is not measuring the model's graph and every figure below is
void.

It also reconciles a type's `FanOut` column against the edges the same run emits.
Those disagreed until 2026-08-26 -- the column ran 1-10 higher on 1.0% of
nopCommerce's types, 1.5% of Umbraco's and 6.7% of Jellyfin's, because outbound
was collected during the walk and inbound in `Build`, which is the first place
that can know whether the target got a node. That was **D63**, it is fixed, and
a non-zero count on this line is a regression of it. The suite cannot see it:
the fixture has no edge to an unanalysed type.
"""
import csv, json, statistics, sys
from collections import defaultdict

# Bytes per node and per edge in the drawn SVG, taken from the shipped project
# map's own density rather than guessed: a box with its label, and a path with
# its coordinates. Only used for the cost estimate, and only to an order.
BOX_BYTES, EDGE_BYTES = 130, 70

# One row of the list form: a name, its project, the direction, and a link.
ROW_BYTES = 70

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
    if off:
        print(f"  FanOut column vs drawable edges: {len(off)} types differ "
              f"({len(off) / len(rows) * 100:.1f}%), by {min(d for d, _ in off)} to "
              f"{max(d for d, _ in off)} -- D63, and it was fixed on 2026-08-26; a non-zero "
              f"count here is a regression of it")
    else:
        print("  FanOut column reconciles with the edge list on every type -- D63")

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

    leads = list(dict.fromkeys(v[0] for v in per_kind.values() if v and v[0] in ids))
    lead_sizes = [len(hop1[s]) for s in leads]
    project = {r["Id"]: r["Project"] for r in rows}
    groups, biggest = [], []
    for s in leads:
        by = defaultdict(int)
        for t in hop1[s]:
            by[project[t]] += 1
        groups.append(len(by))
        biggest.append(max(by.values()) if by else 0)
    drawn_kb = ((sum(lead_sizes) + len(leads)) * BOX_BYTES
                + sum(len(out[s]) + len(inn[s]) for s in leads) * EDGE_BYTES) / 1024
    print()
    print("  the leads -- X10's selection, one per kind, and what A8 ships against:")
    print(f"    {len(leads)} distinct leads, one-hop median {statistics.median(lead_sizes):.0f}, "
          f"max {max(lead_sizes)}, {sum(1 for x in lead_sizes if x > 50)} over 50 nodes")
    print(f"    as drawings:      ~{drawn_kb:5.0f} KB, largest {max(lead_sizes)} nodes "
          f"-- the hairball 5.5 refuses")
    print(f"    as grouped lists: ~{sum(lead_sizes) * ROW_BYTES / 1024:5.0f} KB over "
          f"{sum(lead_sizes)} rows, nothing withheld")
    print(f"    grouped by project: {statistics.median(groups):.0f} groups per lead "
          f"(max {max(groups)}); largest single group median {statistics.median(biggest):.0f}, "
          f"max {max(biggest)} -- a group needs a count and a fold, never a truncation")
    print()
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

"""Do the model's cached numbers still agree with what they are caches of?

    python tools/denormalised.py <csv-dir>

`<csv-dir>` is a directory `bearing --csv` wrote. This is `DenormalisedValueTests`
run against a real solution instead of the fixture, and it exists because the
fixture is not evidence: **D63 was invisible to 538 tests and to four golden
snapshots of the very files that disagreed**, and it was only ever going to show
on a codebase big enough to contain the shape.

A denormalised value is a count cached on one row that restates data held in a
collection somewhere else. Each is written at a different moment of the walk than
the collection it describes, which is precisely how D63 happened: outbound was
counted during the walk and inbound in `ModelBuilder.Build`, and only one of them
could know whether the target had a node.

Exit code is 1 if any property fails.
"""
import csv, sys
from collections import defaultdict


def load(path, key=None):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def check(name, failures, total):
    mark = "ok  " if not failures else "FAIL"
    print(f"  [{mark}] {name}: {len(failures)} of {total} rows disagree")
    for row in failures[:3]:
        print(f"           {row}")
    return not failures


def main(csv_dir):
    types = load(f"{csv_dir}/types.csv")
    edges = load(f"{csv_dir}/edges.csv")
    members = load(f"{csv_dir}/members.csv")

    out_deg, in_deg, in_weight = defaultdict(int), defaultdict(int), defaultdict(int)
    for e in edges:
        out_deg[e["From"]] += 1
        in_deg[e["To"]] += 1
        in_weight[e["To"]] += int(e["Weight"])

    by_type = defaultdict(list)
    for m in members:
        by_type[m["DeclaringType"]].append(m)

    cohort_size = defaultdict(int)
    for t in types:
        cohort_size[(t["Cohort"], t["CohortBasis"])] += 1

    insulating = {
        t["Id"] for t in types
        if t["IsAbstract"].lower() == "true"
        or t["Keyword"] == "interface"
        or t["Kind"] == "Contract"
    }
    out_targets = defaultdict(list)
    for e in edges:
        out_targets[e["From"]].append(e["To"])

    print(f"{csv_dir}  ({len(types)} types, {len(edges)} edges, {len(members)} members)")
    ok = True

    ok &= check("FanOut is the out-edges", [
        f"{t['Name']}: column {t['FanOut']} vs edges {out_deg[t['Id']]}"
        for t in types if int(t["FanOut"]) != out_deg[t["Id"]]], len(types))

    ok &= check("FanIn is the in-edges", [
        f"{t['Name']}: column {t['FanIn']} vs edges {in_deg[t['Id']]}"
        for t in types if int(t["FanIn"]) != in_deg[t["Id"]]], len(types))

    ok &= check("InboundReferences sums the references", [
        f"{t['Name']}: column {t['InboundReferences']} vs weight {in_weight[t['Id']]}"
        for t in types if int(t["InboundReferences"]) != in_weight[t["Id"]]], len(types))

    ok &= check("EffectiveFanOut removes the insulating targets", [
        f"{t['Name']}: column {t['EffectiveFanOut']} vs recomputed "
        f"{sum(1 for x in out_targets[t['Id']] if x not in insulating)}"
        for t in types
        if int(t["EffectiveFanOut"]) != sum(1 for x in out_targets[t["Id"]] if x not in insulating)
    ], len(types))

    ok &= check("MemberCount is the members", [
        f"{t['Name']}: column {t['MemberCount']} vs rows {len(by_type[t['Id']])}"
        for t in types if int(t["MemberCount"]) != len(by_type[t["Id"]])], len(types))

    ok &= check("CohortSize is the cohort population", [
        f"{t['Name']}: column {t['CohortSize']} vs counted "
        f"{cohort_size[(t['Cohort'], t['CohortBasis'])]}"
        for t in types
        if int(t["CohortSize"]) != cohort_size[(t["Cohort"], t["CohortBasis"])]], len(types))

    for column, field in (("Cyclomatic", "Cyclomatic"), ("Dsm", "Dsm"),
                          ("Transform", "Transform"), ("StaticMutations", "StaticMutations")):
        ok &= check(f"{column} sums its members", [
            f"{t['Name']}: column {t[column]} vs sum "
            f"{sum(int(m[field]) for m in by_type[t['Id']])}"
            for t in types
            if int(t[column]) != sum(int(m[field]) for m in by_type[t["Id"]])], len(types))

    ok &= check("MaxMemberCyclomatic is the largest member", [
        f"{t['Name']}: column {t['MaxMemberCyclomatic']} vs max "
        f"{max((int(m['Cyclomatic']) for m in by_type[t['Id']]), default=0)}"
        for t in types
        if int(t["MaxMemberCyclomatic"])
        != max((int(m["Cyclomatic"]) for m in by_type[t["Id"]]), default=0)], len(types))

    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))

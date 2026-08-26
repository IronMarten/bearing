"""X16's measurement: is cohort *size* or cohort *dispersion* the variable a
cohort-relative claim should be gated on?

    python tools/cohort-dispersion.py <csv-dir> [<csv-dir> ...]

Each <csv-dir> is a directory `bearing --csv` wrote, so this measures the shipped
binary's own output rather than a reimplementation of it.

It reconstructs the two populations the gates actually read, and nothing else:

  * **member level** -- ConcealedDecision.AtMethodLevel groups method-like members
    (Method | Constructor, StructureModel.IsMethodLike) by their declaring type's
    cohort key, drops groups below MinCohort, and reads Cyclomatic off the survivors.
  * **type level** -- ConcealedDecision at type level groups types by cohort key,
    drops groups below MinCohort, and reads MaxMemberCyclomatic, FanIn and FanOut.

`ARCHITECTURE.md` §11's X16 table was measured on 2026-08-21 with no script beside
it, and it cannot be reproduced from the CSVs: its largest nopCommerce cohort reads
`n=2,927` where the shipped binary emits 2,909 -- which is the number §34's entry
carries for the same cohort, in the same file. This exists so the next reading is a
re-run rather than an argument.

Defaults mirror AnalysisPolicy: MinCohort 5, OutlierFactor 3.0.
"""
import csv, sys, collections, statistics, os

MIN_COHORT = 5
OUTLIER_FACTOR = 3.0
METHOD_LIKE = {"Method", "Constructor"}


def median(v):
    return statistics.median(v)


def mad(v):
    m = median(v)
    return statistics.median([abs(x - m) for x in v])


def times_median(value, med):
    # Distribution.TimesMedianOf: a multiple of a median of zero is undefined, not
    # infinite -- docs/DEFECTS.md §28.
    return None if med <= 0 else value / med


def load(d):
    def rows(name):
        with open(os.path.join(d, name), encoding="utf-8-sig", newline="") as f:
            return list(csv.DictReader(f))
    return rows("types.csv"), rows("members.csv")


def report(d):
    types, members = load(d)
    cohort_of = {t["Id"]: t["Cohort"] for t in types}

    member_pool = collections.defaultdict(list)
    for m in members:
        if m["Kind"] not in METHOD_LIKE:
            continue
        c = cohort_of.get(m["DeclaringType"])
        if c is not None:
            member_pool[c].append(int(m["Cyclomatic"]))

    type_pool = collections.defaultdict(list)
    for t in types:
        type_pool[t["Cohort"]].append(int(t["MaxMemberCyclomatic"]))

    print("=" * 78)
    print(os.path.basename(os.path.normpath(d)),
          f"-- {len(types)} types, {len(member_pool)} cohorts with method-like members")

    for label, pool in (("member level (cc of method-like members)", member_pool),
                        ("type level   (MaxMemberCyclomatic of types)", type_pool)):
        gated = {k: v for k, v in pool.items() if len(v) >= MIN_COHORT}
        n = len(gated)
        if not n:
            continue
        med01 = sum(1 for v in gated.values() if median(v) <= 1)
        mad0 = sum(1 for v in gated.values() if mad(v) == 0)
        # the trap: at MAD 0 a naive median + k*MAD gate admits everything above the median
        naive = sum(len([x for x in v if x > median(v)]) for v in gated.values() if mad(v) == 0)
        # what ships: TimesMedian >= OutlierFactor, undefined where the median is 0
        fires = undefined = 0
        for v in gated.values():
            md = median(v)
            for x in v:
                tm = times_median(x, md)
                if tm is None:
                    undefined += 1
                elif tm >= OUTLIER_FACTOR:
                    fires += 1
        pop = sum(len(v) for v in gated.values())
        print(f"\n  {label}")
        print(f"    cohorts at or above MinCohort {MIN_COHORT}   {n}")
        print(f"    median 0 or 1 -- so 3x median IS cc>=3   {med01} ({med01/n:.0%})")
        print(f"    zero dispersion, MAD = 0                 {mad0} ({mad0/n:.0%})")
        print(f"    population in those cohorts              {pop}")
        print(f"    ships: TimesMedian >= {OUTLIER_FACTOR} fires on      {fires} ({fires/pop:.1%})")
        print(f"    ...of which median is 0, so undefined    {undefined} ({undefined/pop:.1%})")
        print(f"    naive median+k*MAD would fire on         {naive} ({naive/pop:.1%}) -- the trap")
        print("    largest:")
        for k, v in sorted(gated.items(), key=lambda kv: -len(kv[1]))[:3]:
            print(f"      {k[:52]:52s} n={len(v):5d} median={median(v):g} MAD={mad(v):g} max={max(v)}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    for d in sys.argv[1:]:
        report(d)

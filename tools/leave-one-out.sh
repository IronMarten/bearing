#!/usr/bin/env bash
#
# The leave-one-out half of the gate inventory — docs/TESTING.md §6.
#
# The nudge half is a test (PolicySweepTests) because moving a policy value needs
# no source edit. This half does: it deletes each gate in turn and asks whether
# anything notices. That cannot live in the suite, so it lives here instead of in
# somebody's memory of having run it once.
#
# Two stages, because "did anything notice" and "did output change" are different
# questions and the interesting gates are the ones that answer them differently:
#
#   output-moves  the report at defaults changes.  The gate discriminates plainly.
#   suite-only    the report at defaults is byte-identical, but a test fails — so
#                 the gate only decides off the default path, and is held solely by
#                 a test that deliberately moves a threshold to look at it.
#   DEAD          no change at defaults and the whole suite stays green.  Deletable
#                 today.  This is the list that matters.
#
# Usage:  bash tools/leave-one-out.sh [output-dir]
#
# It mutates files under src/Bearing.Core and restores each with `git checkout HEAD --`
# before moving on, so it needs a clean tree and leaves one. Roughly 30 builds and
# a handful of full test runs; budget half an hour.
#
# DO NOT TOUCH THE WORKING TREE WHILE IT RUNS, AND THAT INCLUDES `git add`. It
# refuses to start dirty and cannot defend against a tree that becomes dirty
# underneath it. Restores are from HEAD rather than the index for that reason —
# staging a mutation used to make the restore reinstate it.
#
# GUARDS is the inventory, and it is hand-maintained on purpose: which `if` is a
# gate and which is a null-extraction is a judgement, and a regex that guessed
# would quietly drop a gate and report a smaller, healthier-looking number.
#
# IT IS KEYED ON THE LINE'S TEXT, NOT ITS NUMBER, AND THAT IS THE WHOLE REPAIR.
# It was keyed on file:line until 2026-08-20, and by the time it was next run the
# detectors had been rewritten under it — b5cc69a's rank conversion, D33's boundary
# fix. Eight of twenty-nine entries had drifted onto doc comments and blank lines,
# and commenting out `/// </para>` changes nothing, so they came back **DEAD**.
# Three BoundaryMarking gates and five ConcealedDecision ones were reported dead
# without ever having been tested. Keyed on text, drift is an abort with the line
# it could not find, which is the failure this file is supposed to be immune to.
#
# Format:  file @@ which-occurrence @@ exact line text @@ what it gates
set -u

cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$LOCALAPPDATA/Microsoft/dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DiffEngine_Disabled=true

OUT="${1:-$(mktemp -d)}"
SLN=tests/TestBed/TestBed.sln

if [ -n "$(git status --short)" ]; then
  echo "working tree is not clean — refusing to mutate source" >&2
  exit 1
fi

# ConcealedDecision moved under this inventory twice and the entries were corrected
# on 2026-08-26, once the staleness check above was repaired enough to say so:
#
#   D10  deleted BOTH `peers.Count < policy.MinCohort` gates. MinCohort is context
#        here now, not a gate, and the entries are gone rather than re-pointed.
#   X16  turned the two `TimesMedian < OutlierFactor` ratio gates into dispersion
#        gates — `!Outlies(...)`, which is median + factor x MAD — and rebased the
#        method rank gate onto `limit`, the smaller of ConcealedTopRank and a capped
#        share. Same three decisions, different expressions, so the entries are
#        re-pointed rather than dropped.
#
# The `is not { } x` null-extractions are still deliberately absent, for the same
# reason the note below gives: commenting one out fails the build.
#
# Two entries are deliberately absent. BoundaryMarking's kind filter is
# `model.Types.Where(IsBoundary)` in both detectors, and commenting an assignment
# out does not relax a gate, it fails the build — the script's own note for
# build-fails says such a line is a null-extraction and the inventory should drop
# it. Whether that filter discriminates wants a different instrument.
GUARDS="
BlastRadius.cs@@1@@if (peers.Count < policy.MinCohort) continue;@@MinCohort cohort floor
BlastRadius.cs@@1@@if (type.FanIn < policy.MinFanIn) continue;@@MinFanIn absolute floor
BlastRadius.cs@@1@@if (inbound.TimesMedian < policy.BlastFanInMultiple) continue;@@BlastFanInMultiple x-median
BlastRadius.cs@@1@@if (inbound.Rank > topRank) continue;@@BlastTopFraction rank gate
BlastRadius.cs@@1@@if (cc.Percentile < policy.BlastComplexityPercentile) continue;@@BlastComplexityPercentile
BoundaryMarking.cs@@1@@if (type.MaxMemberCyclomatic < policy.HighCc) continue;@@HighCc boundary-carries-logic
BoundaryMarking.cs@@1@@if (rank > topRank) continue;@@boundary rank gate
BoundaryMarking.cs@@1@@if (type.DataShape < threshold) continue;@@SurfaceOutlier threshold
BreaksAlone.cs@@1@@if (instability < policy.IsolatedThreshold) continue;@@IsolatedThreshold
BreaksAlone.cs@@1@@if (type.MaxMemberCyclomatic < policy.HighCc) continue;@@HighCc
ChangeCost.cs@@1@@if (!Eligible.Contains(type.Classification.Kind, StringComparer.Ordinal)) continue;@@Contract-or-ApiBoundary kind filter
ChangeCost.cs@@1@@if (type.FanIn < policy.MinFanIn) continue;@@MinFanIn floor
ChangeCost.cs@@1@@if (reading.Rank > limit) continue;@@ChangeCostTopFraction rank gate
ConcealedDecision.cs@@1@@if (member.Cyclomatic < policy.MinDecisionCc) continue;@@MinDecisionCc (method level)
ConcealedDecision.cs@@1@@if (reading.Rank > limit) continue;@@ConcealedTopRank/ConcealedTopShare rank gate (method level)
ConcealedDecision.cs@@1@@if (!Outlies(complexity, member.Cyclomatic, policy)) continue;@@ConcealedDispersionFactor (method level)
ConcealedDecision.cs@@1@@if (type.MaxMemberCyclomatic < policy.MinDecisionCc) continue;@@MinDecisionCc (type level)
ConcealedDecision.cs@@1@@if (!Outlies(complexity, type.MaxMemberCyclomatic, policy)) continue;@@ConcealedDispersionFactor (type level)
ConcealedDecision.cs@@1@@if (inbound.TimesMedian > policy.ConcealedFanInCeiling) continue;@@ConcealedFanInCeiling
ConcealedDecision.cs@@1@@if (outbound.TimesMedian > policy.ConcealedFanOutCeiling) continue;@@ConcealedFanOutCeiling
HubOrGodObject.cs@@1@@if (coupling < policy.HubMin) continue;@@HubMin coupling floor
LoadBearing.cs@@1@@if (instability > policy.StableThreshold) continue;@@StableThreshold
LoadBearing.cs@@1@@if (type.FanIn < policy.MinFanIn) continue;@@MinFanIn floor
LoadBearing.cs@@1@@if (type.MaxMemberCyclomatic < policy.HighCc) continue;@@HighCc
NoPeerGroup.cs@@1@@if (type.CohortSize >= policy.MinCohort) continue;@@MinCohort (inverted)
SharedMutableState.cs@@1@@if (type.StaticMutations <= 0) continue;@@StaticMutations > 0
SpansArchitecturalLayers.cs@@1@@if (!IsSignificant(dependency)) continue;@@IsSignificant dependency filter
SpansArchitecturalLayers.cs@@1@@if (kinds.Count < policy.MinKindSpan) continue;@@MinKindSpan floor
"

# Locate every guard BEFORE mutating anything. A stale inventory should abort with
# the line it cannot find rather than spend half an hour reporting comments as dead.
stale=0
while IFS= read -r entry; do
  [ -z "$entry" ] && continue
  file="${entry%%@@*}"; rest="${entry#*@@}"
  nth="${rest%%@@*}"; rest="${rest#*@@}"
  snippet="${rest%%@@*}"; label="${rest##*@@}"
  path="src/Bearing.Core/$file"

  # `|| found=0` and NOT `|| echo 0`, and that one character was the whole bug.
  # `grep -c` prints 0 AND exits 1 when it matches nothing, so the `||` also fired
  # and $found became the two-line string "0\n0". `[ "0\n0" -lt 1 ]` is not false,
  # it is an ERROR — "integer expression expected" — so the if-body never ran, the
  # entry was not counted stale, and the abort this block exists to perform did not
  # happen. Found 2026-08-26 with five stale ConcealedDecision entries in the
  # inventory: D10 removed both MinCohort gates and X16 replaced the two ratio gates
  # and the rank gate, and this reported none of it. A staleness check that cannot
  # report staleness is the failure this file's header claims immunity from.
  found=$(grep -c -F -- "$snippet" "$path" 2>/dev/null) || found=0
  if [ "$found" -lt "$nth" ]; then
    echo "INVENTORY STALE: $file has $found occurrence(s) of, and needs $nth:" >&2
    echo "    $snippet" >&2
    echo "    ($label)" >&2
    stale=$((stale + 1))
  fi
done <<<"$GUARDS"

if [ "$stale" -gt 0 ]; then
  echo >&2
  echo "$stale inventory entr(y|ies) no longer match the source. Fix GUARDS and re-run." >&2
  exit 1
fi

echo "baseline..."
dotnet build -v q --nologo >/dev/null 2>&1 || { echo "baseline build failed" >&2; exit 1; }
dotnet run --project src/Bearing.Cli --no-build -- "$SLN" >"$OUT/baseline.txt" 2>/dev/null

: >"$OUT/results.tsv"

while IFS= read -r entry; do
  [ -z "$entry" ] && continue
  file="${entry%%@@*}"; rest="${entry#*@@}"
  nth="${rest%%@@*}"; rest="${rest#*@@}"
  snippet="${rest%%@@*}"; label="${rest##*@@}"
  path="src/Bearing.Core/$file"

  line=$(grep -n -F -- "$snippet" "$path" | sed -n "${nth}p" | cut -d: -f1)

  sed -i "${line}s|^|// LEAVE-ONE-OUT |" "$path"

  if ! dotnet build -v q --nologo >/dev/null 2>&1; then
    # Not a verdict: the line is a null-extraction rather than a gate, and the
    # inventory above should drop it.
    verdict="build-fails"
  else
    dotnet run --project src/Bearing.Cli --no-build -- "$SLN" >"$OUT/mutant.txt" 2>/dev/null
    if ! diff -q "$OUT/baseline.txt" "$OUT/mutant.txt" >/dev/null 2>&1; then
      verdict="output-moves"
    elif dotnet test --nologo 2>&1 | grep -q "^Passed!"; then
      verdict="DEAD"
    else
      verdict="suite-only"
    fi
  fi

  printf '%s\t%s\t%s\t%s\n' "$file" "$line" "$label" "$verdict" >>"$OUT/results.tsv"
  echo "$file:$line  $label  -> $verdict"

  git checkout HEAD -- "$path"
done <<<"$GUARDS"

echo
echo "dead gates:"
# awk rather than `grep -P`, which needs a unibyte or UTF-8 locale and silently
# printed "(none)" on a run with six DEAD rows in it.
awk -F'\t' '$4 == "DEAD" { print "  " $1 ":" $2 "  " $3; n++ } END { if (!n) print "  (none)" }' \
  "$OUT/results.tsv"

echo
awk -F'\t' '{ n[$4]++ } END { for (v in n) printf "%-14s %d\n", v, n[v] }' "$OUT/results.tsv"
echo
echo "results: $OUT/results.tsv"

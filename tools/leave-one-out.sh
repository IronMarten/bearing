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
# It mutates files under src/Bearing.Core and restores each with `git checkout --`
# before moving on, so it needs a clean tree and leaves one. Roughly 30 builds and
# a handful of full test runs; budget half an hour.
#
# GUARDS is the inventory, and it is hand-maintained on purpose: which `if` is a
# gate and which is a null-extraction is a judgement, and a regex that guessed
# would quietly drop a gate and report a smaller, healthier-looking number.
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

# file:line:what the condition gates
GUARDS="
BlastRadius.cs:41:MinCohort cohort floor
BlastRadius.cs:54:MinFanIn absolute floor
BlastRadius.cs:59:BlastFanInMultiple x-median
BlastRadius.cs:64:BlastTopFraction rank gate
BlastRadius.cs:66:BlastComplexityPercentile
BoundaryMarking.cs:49:IsBoundary kind filter
BoundaryMarking.cs:50:HighCc boundary-carries-logic
BoundaryMarking.cs:109:SurfaceOutlier threshold
BreaksAlone.cs:41:IsolatedThreshold
BreaksAlone.cs:42:HighCc
ChangeCost.cs:69:Contract-or-ApiBoundary kind filter
ChangeCost.cs:73:MinFanIn floor
ChangeCost.cs:76:ChangeCostTopFraction rank gate
ConcealedDecision.cs:56:MinCohort (method level)
ConcealedDecision.cs:66:MinDecisionCc (method level)
ConcealedDecision.cs:69:OutlierFactor (method level)
ConcealedDecision.cs:108:MinCohort (type level)
ConcealedDecision.cs:116:MinDecisionCc (type level)
ConcealedDecision.cs:119:OutlierFactor (type level)
ConcealedDecision.cs:123:ConcealedFanInCeiling
ConcealedDecision.cs:124:ConcealedFanOutCeiling
HubOrGodObject.cs:53:HubMin coupling floor
LoadBearing.cs:46:StableThreshold
LoadBearing.cs:50:MinFanIn floor
LoadBearing.cs:52:HighCc
NoPeerGroup.cs:50:MinCohort (inverted)
SharedMutableState.cs:43:StaticMutations > 0
SpansArchitecturalLayers.cs:86:IsSignificant dependency filter
SpansArchitecturalLayers.cs:92:MinKindSpan floor
"

echo "baseline..."
dotnet build -v q --nologo >/dev/null 2>&1 || { echo "baseline build failed" >&2; exit 1; }
dotnet run --project src/Bearing.Cli --no-build -- "$SLN" >"$OUT/baseline.txt" 2>/dev/null

: >"$OUT/results.tsv"

while IFS= read -r entry; do
  [ -z "$entry" ] && continue
  file="${entry%%:*}"; rest="${entry#*:}"
  line="${rest%%:*}"; label="${rest#*:}"
  path="src/Bearing.Core/$file"

  sed -i "${line}s|^|// LEAVE-ONE-OUT |" "$path"

  if ! dotnet build -v q --nologo >/dev/null 2>&1; then
    # Not a verdict: it means the line was a null-extraction rather than a gate,
    # and the inventory above should drop it.
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

  git checkout -- "$path"
done <<<"$GUARDS"

echo
echo "dead gates:"
grep -P '\tDEAD$' "$OUT/results.tsv" || echo "  (none)"
echo
echo "results: $OUT/results.tsv"

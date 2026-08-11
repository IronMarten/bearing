# oracle/ — do not clean this up

`ArchProbe` is the throwaway probe that proved Bearing's analysis logic. It is kept here
verbatim, and it is not shipped (`IsPackable=false`).

It exists for one job: **when analysis moves into `Bearing.Core`, run both against the same
solution and diff the output.** Any divergence is either a bug or an improvement that has to
be justified out loud. That turns a scaffold into a regression harness for the extraction.

Roughly 2,500 lines encoding about 32 behaviours that were expensive to get right, several
of which were fixed, reintroduced elsewhere, and fixed again. `Report.cs` is 997 of those
lines — console formatting with the interpretation baked into it, which is the layer being
replaced.

## The rule while extracting

`tests/Bearing.Tests/golden/` holds `nominations.verified.txt`, `types.verified.csv` and
`edges.verified.csv` from a clean run of the pristine probe against `tests/TestBed`.

Byte-identical, or the extraction changed behaviour. **This is a test now** —
`OracleGoldenTests` regenerates all three from the probe on every run, so:

```
dotnet test Bearing.sln
```

It used to be the manual `diff` that lived here, which meant it only ran when somebody
remembered. Several of the probe's defects were reintroductions caught the second and third
time by exactly that kind of vigilance, and vigilance is what a restructure destroys.

Regenerate the baselines only with a deliberate reason stated in the commit message — they
are evidence, not output. Paths are normalised by the test harness, never by the probe; see
`docs/TESTING.md` §4 for why that distinction cost 51 rows once already.

A second pristine copy lives outside this repo, so the comparison never depends on git
archaeology.

## Why it carries 32 analyzer warnings

`oracle/Directory.Build.props` turns off warnings-as-errors, analyzers and code style for
this directory. That is deliberate and the file explains it: fixing a CA1305 here can change
the decimals rendered into `nominations.txt`, which is a behaviour change wearing the
costume of a lint fix. The probe is evidence, not source we maintain.

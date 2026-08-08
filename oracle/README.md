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

`tests/Bearing.Tests/golden/` holds `nominations.txt`, `types.csv` and `edges.csv` from a
clean run of the pristine probe against `tests/TestBed`. After moving any computation out of
`Report.cs`:

```
dotnet run --project oracle/ArchProbe -- tests/TestBed/TestBed.sln --out ../after
diff ../after/nominations.txt tests/Bearing.Tests/golden/nominations.txt
```

Byte-identical, or the extraction changed behaviour. Regenerate the golden files only with a
deliberate, explained reason — they are the baseline, not an output.

A second pristine copy lives outside this repo, so the comparison never depends on git
archaeology.

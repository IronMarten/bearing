# Testing

```
dotnet test Bearing.sln
```

43 assertions, about 3 seconds. The workspace load is the cost centre, not the analysis, so
the whole suite shares one analyzed fixture.

---

## 1. Why this suite is shaped the way it is

Roughly **32 defects** were found while building the probe. Every one of them was caught by
a human reading console output, because nothing asserted. Several were *reintroductions* —
ties ranking at the 100th percentile, zero medians, roll-call findings — the same failure
reappearing in a new message after being fixed elsewhere, caught the second and third time
only by manual vigilance.

Manual vigilance does not survive a restructure, and phase 1 is a restructure. That is what
this suite is for. It is not a coverage target.

## 2. The stack

| Choice | Why |
|---|---|
| xUnit v2, bare `Assert` | already in use, no assertion-library dependency to carry |
| Verify.Xunit | snapshot workflow: `.received.` vs `.verified.`, diff tooling, explicit accept |
| One test project | the oracle comparison runs both implementations; a project boundary through the middle of that would help nobody |
| `System.Reflection.Metadata` | reads compiled IL for the seam tests — no extra dependency |

Deliberately **not** FluentAssertions: v8 moved to a commercial licence, and this is an
Apache-2.0 repository with a paid tier planned downstream. Not a fight worth having later.

## 3. Two snapshot regimes, and they are not the same

This is the distinction most likely to be got wrong, because Verify treats both identically
and the difference is entirely one of intent.

### Frozen — `tests/Bearing.Tests/golden/`

`nominations.verified.txt`, `types.verified.csv`, `edges.verified.csv`: the output of the
pristine probe against `tests/TestBed`.

These are **evidence, not output**. They record what the tool did before the extraction
started. `OracleGoldenTests` regenerates them from the probe on every run and compares.

> Accepting a change here is a claim that the tool's behaviour changed on purpose. It goes
> in the commit message, in those words, with the reason. If you cannot write that sentence,
> you have found a bug rather than an improvement.

This was a shell command in `oracle/README.md` that somebody had to remember to run. It is
a test now for the reason in §1.

### Accept-workflow — everywhere else

Snapshots of surfaces still being designed: JSON output, the HTML report, terminal
rendering. There is no prior truth to preserve, so re-accepting as the design moves is
normal maintenance and needs no ceremony.

### Accepting a snapshot

A failing snapshot writes `<name>.received.<ext>` beside the verified file. Read the diff,
then:

```
mv golden/nominations.received.txt golden/nominations.verified.txt
```

`*.received.*` is git-ignored. In CI and headless runs set `DiffEngine_Disabled=true` so no
diff tool is launched.

## 4. Snapshots must be machine-independent, and the harness does that work

**Normalisation belongs in the harness, never in the code under test.**

`VerifyConfiguration.cs` holds one scrubber: it rewrites any absolute path into the fixture
as `TestBed/…` with forward slashes. Verify's default date and GUID scrubbers are turned
**off**, because the fixture contains neither and anything they matched would be real
content silently replaced by a token — a green test over changed data.

This is not hypothetical. The original `golden/types.csv` carried 51 rows of
`C:\Users\…\dotnet-tool\TestBed\…` — captured from a working folder outside this repository
entirely. The byte-for-byte gate that phase 1 depends on would have failed all 51 rows on
its first honest run, for a reason with nothing to do with behaviour. The natural reaction
to that failure is to regenerate the baseline, which destroys the baseline.

The current baselines were re-recorded and verified identical to the originals once paths
were normalised — same bytes, same findings, nothing behavioural changed.

## 5. What is asserted

**Against the model, never against report wording.** `Report.cs` is the layer being
replaced; tests coupled to its sentences would die exactly when they are needed. The
snapshots in §3 are the deliberate exception, and they exist to catch the wording moving.

| File | Covers |
|---|---|
| `GraphTests` | Tarjan over synthetic input, no Roslyn. Includes a 50,000-deep chain, which pins the iterative implementation against a well-meaning recursive rewrite. |
| `StructureTests` | load health, fixture shape, generated-code exclusion, `Kind` classification, namespace truncation, cohort discovery, contract fan-in, hub magnitudes |
| `OracleGoldenTests` | the three frozen baselines |
| `OrderingTests` | that every artifact is a function of the analysis and not of its enumeration order — see below |
| `DistributionTests` | the comparative substrate — midrank, medians, and when a reading is refused — without Roslyn |
| `CoreEquivalenceTests` | that Core computes the same numbers as the probe on the fixture. **The extraction gate** — see below |
| `AnalysisPolicyTests` | that every threshold is named and reviewable, including the thirteen that were literals |
| `CohortTests` | peer-group assignment and candidate derivation — stranding, starvation, reconciliation — plus full cohort equivalence with the probe |
| `WalkerEquivalenceTests` | Core's walk against the probe's: every type, measure, edge, member and external namespace |
| `SeamTests` | Core references no console; Core does not depend on Cli |
| `ToolInfoTests` | the first logic in Core |
| `KnownDefectTests` | defects found after the freeze, pinned as current behaviour — see below |
| `FixtureCoverageTests` | what the fixture does *not* cover, asserted so it stays visible |

### Pinning a defect you are not allowed to fix

The oracle is frozen, so a defect found after the freeze cannot be fixed where it lives. It
gets a test asserting the **wrong** behaviour instead, naming the requirement that supersedes
it. Extraction then cannot carry it forward silently, and cannot fix it silently either — the
day Core does the right thing the test fails, and deleting it is a deliberate act rather than
a diff nobody reads.

Each pinned test has an entry in [`DEFECTS.md`](DEFECTS.md), which carries the evidence and the
remedy; the test names the requirement and asserts the behaviour. Add to both or neither — a
pinned test with no entry is a defect nobody can act on, and an entry with no test is one that
can be carried forward silently.

`Change_cost_is_gated_by_min_cohort_where_it_means_min_fan_in` is the first. It is also the
one place the suite reads report text rather than the model, because the threshold is a
literal inside `PrintNominations` and there is no model surface to assert against. That
absence is the defect; only the subject names are read, never the sentence.

### The extraction gate is agreement, not byte-identity

`OracleGoldenTests` asks whether the probe's output moved. That catches a regression in the
probe and says nothing about the rewrite, because Core is not in the picture at all.

`CoreEquivalenceTests` asks the question extraction actually poses: **does the reimplementation
agree with the oracle?** Every cohort reading, every method reading, every solution-wide
percentile and every project's coupling, computed twice and compared on the real fixture. Core
is a rewrite rather than a port, so agreement is a result rather than a tautology — each
assertion is a place the two could differ and do not.

As each piece of `Report.cs` moves, its equivalence check lands here first and the probe's
version becomes the expectation.

**An equivalence check has a shelf life, and knowing when it expires matters.** The project
metrics had no model surface on either side, so the test read the probe's sentence and parsed
the numbers back out — the same licence `KnownDefectTests` takes, because the absence of a model
surface *was* the defect. Once Core walked the solution itself, that check became redundant
rather than merely ugly: the walk equivalence establishes that Core's types and edges are the
probe's, coupling is a pure function of those two, and the function has its own tests. Composing
those three proves what the parser proved. It was deleted and replaced with the fixture's known
answers, stated rather than parsed.

The general shape: an end-to-end comparison earns its place while the pieces underneath are
unverified, and becomes a liability once they are — at which point it is a regex over prose that
can break for reasons that have nothing to do with correctness.

**Deliberate divergences are asserted, not described.** Core refuses to state a number with no
basis: a peer group of one has no reading, a project with no cross-project coupling has no
instability. The test pins both halves — what Core declines to say, and what the probe says
instead — plus the proof that the difference is invisible in current output, because the CSV
already blanks exactly those values. That proof is what makes it safe to land a behaviour
difference before the renderers move.

### A snapshot that reproduces is not the same as a snapshot that is determined

`OrderingTests` exists because the goldens passed for weeks while resting on nothing. Every
writer sorted on a non-total key — 257 of 261 edges tie on `Weight` alone — so the position of
most rows was decided by `Dictionary` enumeration order, which is insertion order, which is
project load order. Reversing the project declaration order in `TestBed.sln`, an edit with no
semantic content, moved all of it. Nothing was ever measured differently; the rows just landed
somewhere else.

That distinction is the whole point during extraction. `Bearing.Core` is a reimplementation
rather than a port, so it will not reproduce the probe's incidental insertion order however
correct its numbers are. A frozen snapshot that encodes enumeration order would have gone red on
day one for no reason, and the real regression would have been invisible in the noise.

So the test does not ask *does it reproduce*. It renders each artifact twice — once from the
analysis, once from a shuffled view of the same objects — and requires the bytes to match. That
is the question extraction actually poses. It is also its own control: remove any `ThenBy` in
`Report.cs` and it fails while every golden stays green, which is the failure mode it was
written to catch.

When you add a writer or a nomination list, give it a total key. `Id` for types,
`(From, To)` for edges, `(DeclaringTypeId, Id, File, Line)` for methods — **not `Id` alone for
methods**, which is the bare method name and ties twelve ways on `Apply`.

### Order matters in `StructureTests`

Load health is asserted first because everything below it is meaningless if it fails. A
project that does not load understates fan-in everywhere it is referenced.

## 6. The fixture is the specification

`tests/TestBed/` is a synthetic solution with **known answers**, and its defects are
deliberate: a god object, a concealed decision hidden in plumbing, seven near-identical
normalizers with one planted outlier, a DIP contrast pair, a layer-spanning auth
middleware, a namespace cycle, two unreferenced projects, a type named like data access
that is not, and scaffolded code that must be excluded.

It opts out of analyzers and warnings-as-errors (`tests/TestBed/Directory.Build.props`).
Tidying it up changes the expected answers.

**Add to it; do not reshape it.** When you add a case, record its known answer below.

### Current known answers

- **89 type rows** from **90 declarations**, 88 methods, 202 edges, 13 cohorts, 2 excluded,
  **zero load warnings**, **1 skipped project** (`Core.Tests`). The row/declaration gap is the
  planted identity collision below, and it is the expected answer until Core keys types on
  `(assembly, FQN)`.
- namespace cycle: `TestBed.Core` ↔ `TestBed.Core.Pricing`
- type tangle: 8 types — the six plain normalizers plus `Router` and `ShipmentCoordinator`
- breaks alone: `TariffReconciler` fires; `MethodReconciler` also fires and **should not** — see
  the note below. Cohort `suffix:Reconciler` (9 members), medians fan-out 2, fan-in 1,
  max-member-cc 3. The three suppression companions each satisfy every *other* condition:
  `ReconciliationController` (ApiBoundary), `RateReconciler` (nominated as a concealed
  decision), `AuditReconciler` (fan-in 0)
- blast radius: `ShipmentLedger` alone, in cohort `suffix:Ledger` (12 members) — fan-in 11,
  `FanInPctl` 95.83, `FanInXMedian` 11, cc 18, `CyclomaticPctl` 95.83. All four conditions with
  margin; the cohort is twelve rather than ten so the case does not sit on the boundary
- unreferenced projects: `Data`, `Tools`. **Not** `Core.Tests` — it is skipped, not dead
- dead-code traps, all fan-in 0 and currently indistinguishable from genuinely dead code:
  `AuditPolicySink` (registered by convention, named nowhere), `SchemaMigrationHandler` (named
  only by a string literal), `FixtureBuilder` (used only from the skipped `Core.Tests`)
- `TenantPolicySink` is the **contrast**: registered by `AddSingleton<T>()`, fan-in **1**. A
  generic type argument is a compile-time reference, so the DI case §5.6 names as needing
  detection already works. The case that does not is convention registration
- boundary: 8 contact points, 6 inbound, 2 outbound
- `AuthenticationMiddleware [ApiBoundary]` spans 3 kinds via `TenantStore` and `AuditClient`
- project Martin metrics: `Core` I 0 A 0.1 D 0.9 (zone of pain); `Data` and `Tools` I 1 A 0 D 0
- **identity collision:** `TestBed.Shared.PayloadTag` is declared `partial` in both `Data` and
  `Tools`, which do not reference each other. The probe reports **one** row: `Project=Tools`,
  `MemberCount 6` (Data's 2 plus Tools' 4), `Loc 42` across both files, `cc 16` while its
  largest member is `cc 13`. Data's declaration is invisible and its project under-counted.
  Pinned by `KnownDefectTests`; `partial` is deliberate, so a fix that stops merging partials
  *within* one compilation is also wrong

### The fixture's known gaps

**~~For dead code (Job A).~~ Filled — the plants are in ahead of the feature.** `Core.Tests`
exists, and `AuditPolicySink`, `SchemaMigrationHandler` and `FixtureBuilder` each read as
unreferenced for a different legitimate reason. `FixtureCoverageTests` asserts all three have
fan-in 0 *and* that nothing in the report mentions them — type-level dead code is not
implemented, so that silence is a missing feature rather than a clean bill of health. The test
fails the day detection lands, which is when each category has to be named.

> **Planting these corrected the requirement.** `TECHREQ-job-a.md` §5.6 asks for
> `services.AddX<T>()` to be detected as an inbound reference. It already is — a generic type
> argument is a compile-time reference, so `TenantPolicySink` has fan-in 1 and was never at
> risk. The DI false positive that actually bites is **convention registration**, where no type
> is named anywhere. Both are in the fixture now, one as trap and one as contrast, so the
> distinction cannot be lost again.

**~~For two existing findings (Job B).~~ Both filled.** The SPANS roll-call collapse branch is
still uncovered, as are suppression rows 4–7. A section that emits no output produces the same
bytes whatever its thresholds are — so the goldens carried no record of how either finding
behaved, and their thresholds could have been changed to any value, or the findings deleted,
with the suite still green.

Breaks alone was the one that mattered: it carries three suppression rules, including *never
imply safety at a boundary* and *never contradict yourself about one component*, and removing
any of them turned empty output into empty output. `SuppressionTests` covers those three rows
now, each with a companion that satisfies **every other condition** of the finding — without
that second half, a companion that quietly stopped qualifying would still pass and the
suppression would be untested again with nobody noticing.

> **Filling breaks alone found a suppression that is missing.** §4 row 2 suppresses the finding
> for a type "already nominated as a concealed decision", and the implementation captures
> **type-level** nominations only. §3.3 nominates the same signal on *methods*, and §3.3 is the
> primary of the two — type-level came back empty on real code while method-level found the
> right thing.
>
> So the case that matters most is the case the suppression misses. `MethodReconciler` is
> nominated at method level and then told it breaks alone: *"this method is making business
> judgements"* and *"if it breaks, it breaks alone"*, about one component, in one report — the
> contradiction invariant 3 exists to prevent. `RateReconciler` is the contrast, nominated at
> both levels and correctly suppressed. The two differ only in whether the type-level nomination
> happened to fire, which is not a difference a user would accept as meaningful.
>
> Pinned by `KnownDefectTests.A_method_level_concealed_decision_does_not_suppress_breaks_alone`.
> §4 row 2 is amended at source to read "at type level (§3.2) or on any of its methods (§3.3)".

> **Filling blast radius found the reason it was empty, and it was not the fixture.**
> `Percentile` is midrank — `100 * (below + 0.5 * equal) / n` — so a unique maximum scores
> `(n - 0.5)/n * 100`: 90.0 at n=5, **94.44 at n=9**, 95.0 at n=10. The finding requires
> `FanInPctl >= 95`, so **no cohort of five to nine members can ever produce it**, whatever its
> members look like, while `--min-cohort` admits cohorts of five.
>
> The ceiling is arithmetic, not tuning, and it is why the plant needed a twelve-member cohort
> rather than a more extreme type. Pinned by
> `KnownDefectTests.Blast_radius_is_unreachable_in_a_cohort_below_ten`, and it needs an answer
> in its own right: `TECHREQ-job-b.md` §5 converts absolute gates to percentiles, and this is
> the hazard in that direction — a percentile floor above `(n-0.5)/n` is unsatisfiable rather
> than merely strict.
>
> It is also the inverse of the question that caught the original cry-wolf failure. §8 asks
> "can this fire on 100% of a category?" Its twin — **"can this fire at all?"** — was not being
> asked, and is now.

**~~For type identity.~~ Filled.** `TestBed.Shared.PayloadTag` is now declared in both `Data`
and `Tools`. The goldens record the merged row, so the defect *and* its fix are both visible:
when Core keys on `(assembly, FQN)` the row count goes 52 → 53 and `KnownDefectTests` fails,
which is the event worth seeing (`TECHREQ-job-b.md` §8, criterion 8).

> **Adding to the fixture moves the frozen goldens, and §3's acceptance sentence does not
> apply.** "The tool's behaviour changed on purpose" is not true of a fixture addition — the
> *input* changed. Two rules keep the oracle intact:
>
> 1. **Regenerate in a commit that touches the fixture and nothing else.** A golden change that
>    rides along with a Core change destroys the baseline it exists to be.
> 2. **Expect existing rows to move.** Percentiles are population-relative, so planting one
>    type shifted `GlobalFanInPctl` and `GlobalMaxCcPctl` on all 51 existing rows. That is
>    arithmetic, not behaviour — but it means the diff is never purely additive, and skimming
>    it for "only new rows" will mislead you.

## 7. The invariants are acceptance criteria

From the PRD. Not style preferences — every one was learned by shipping the opposite and
watching it produce confident, plausible, wrong output. These four are the ones the current
work can violate:

| Invariant | Test |
|---|---|
| 2 — anomaly, not roll-call | the integration map lists no plumbing namespace individually |
| 4 — never imply safety at a boundary | no output contains "safe to delete/remove" |
| 6 — blank, never fake | a project with no dependents emits blank I, not 0 |
| 8 — state the coverage | every view reports exclusions, load failures, and what was skipped |

Invariant 6 is currently enforced in the wrong place — see
[`ARCHITECTURE.md`](ARCHITECTURE.md) §3.

## 8. Review questions for any new finding

Each is a recurring defect class from the probe build, turned into a question:

- Does this normalized measure have an absolute floor beside it? *(failed 5 ways)*
- Can this fire on 100% of a category? *(failed 4 times — and now measured at 6.9%)*
- **Can this fire at all?** *(the highest-yield question here — three findings failed it: blast
  radius below a cohort of ten, widest contract surface at any size, and the layer-span examples
  list. Two of the three were unreachable by arithmetic rather than by tuning, which no amount of
  running the tool would have revealed)*
- **Is this gate measured against something its own filter already bounds?** *(a proportional
  suppression on top of a proportional filter can only ever land on its own threshold —
  `DEFECTS.md` §12)*
- Can two findings contradict each other about one component?
- Does this claim something the tool cannot see?
- Is a statistic being printed where none exists? *(`999x`, median-of-one)*
- **Does this number travel to a second codebase?** *(four of seven did not: percentile gates
  travel, absolute ones do not)*
- **Is this output ordered by something total, or is the tail of the sort decided by whatever
  order the data arrived in?** *(a stable sort on a non-total key looks deterministic on one
  machine and is not a property of the tool — §5, `DEFECTS.md` §6)*

## 9. A gate that cannot fail is worse than no gate

Both new gates were mutation-tested when they were written: a `Console.WriteLine` added to
`Bearing.Core` fails `SeamTests`, and moving one threshold fails `OracleGoldenTests`. Do the
same for the next one — a snapshot suite that silently stopped covering anything looks
exactly like a passing suite.

`SeamTests.The_seam_test_is_actually_looking_at_something` exists for this reason: every
other assertion in that file passes trivially against an assembly that is missing or empty.

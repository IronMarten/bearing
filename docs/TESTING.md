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
| `FindingSetTests` | finding identity and the member → declaring type query suppression is written against, on synthetic input |
| `FindingEquivalenceTests` | Core's concealed-decision nominations against the probe's, both levels, plus the rules that are now model facts rather than wording |
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

- **127 type rows** from **128 declarations**, 133 methods, 290 edges, 21 cohorts, 2 excluded,
  **zero load warnings**, **1 skipped project** (`Core.Tests`). The row/declaration gap is the
  planted identity collision below, and it is the expected answer until Core keys types on
  `(assembly, FQN)`.

  > These four numbers were **89 / 90 / 88 / 202** in this file until they were checked against
  > the goldens, having drifted through every plant since they were written. Nothing asserted
  > them, so nothing failed. `StructureTests.Fixture_shape_is_stable` pins the counts and this
  > line now quotes it — a known answer that no test holds is a comment, and it rots at exactly
  > the rate the fixture grows.
- namespace cycle: `TestBed.Core` ↔ `TestBed.Core.Pricing`
- type tangle: 8 types — the six plain normalizers plus `Router` and `ShipmentCoordinator`
- breaks alone: `TariffReconciler` fires; `MethodReconciler` also fires and **should not** — see
  the note below. Cohort `suffix:Reconciler` (9 members), medians fan-out 2, fan-in 1,
  max-member-cc 3. The three suppression companions each satisfy every *other* condition:
  `ReconciliationController` (ApiBoundary), `RateReconciler` (nominated as a concealed
  decision), `AuditReconciler` (fan-in 0)
- **concealed decision, method level: 12 nominations**, led by `TariffCalculator.Apply` at 22x
  its peer median. Two tie groups — `MethodReconciler`/`RateReconciler` at 4.333 and
  `AuditReconciler`/`TariffReconciler` at 3.667 — which is what exercises the ordering tiebreak
- **concealed decision, type level: 5 nominations** — `ShipmentController` 12x,
  `ThroughputGauge` 8x, `AuthenticationMiddleware` 5x, `RateReconciler` 4.333x,
  `GuaranteedServiceNormalizer` 3.5x. No ties, and every one of the five is also nominated at
  method level: on this fixture type level adds no subject of its own, which is a gap rather
  than a property — see below
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
- boundary: **10** contact points, 8 inbound, 2 outbound. Shapes
  `2,3,4,6,7,8,8,8,8,12`, median 7.5, so `WIDEST CONTRACT SURFACE` threshold 11.25 and
  `ShipmentController` at 12 is the sole qualifier — one against a ceiling of five, which is
  `docs/DEFECTS.md` §12 and why row 5 still cannot fire
- change cost: **the probe says 5, Core says 3**, and the difference is the saturation conversion.
  The probe's five are four contracts — `NormalizationContext` 20, `RawResponse` 19,
  `NormalizedResponse` 15, `ModelDescription` 5 — plus `DispatchCallbackController` at 5, the only
  `ApiBoundary` ever to clear the absolute floor. Core drops the two at fan-in 5: both sit at
  solution rank 20.5 of 128, which is not the most-depended-on part of the application. Core's
  three are identical at `ChangeCostTopFraction` 0.05, 0.10 and 0.15 — the population has a gap
  between fan-in 15 and 5 and the gate falls in it — and narrow to two at 0.02
- `AuthenticationMiddleware [ApiBoundary]` spans 3 kinds via `TenantStore` and `AuditClient`
- **layer span: 6 nominations**, and every one of them sits exactly on `minKindSpan`. Three
  significant kinds and a floor of three make "spans the minimum" and "spans everything" the same
  condition, so the floor cannot discriminate at any solution size — `TASKS.md` X4. Nothing on the
  fixture reaches exactly two, either, so lowering it admits nobody
- **layering patterns: three of them**, grouped on the type's own role plus its named dependencies
  rather than on the kind signature (`docs/DEFECTS.md` §11). `QuoteController`,
  `DocumentController`, `RateController` and `TrackingController` are one pattern of four —
  `ApiBoundary`, reaching `TenantStore` and `CarrierGateway`. `AuthenticationMiddleware`
  (`TenantStore`, `AuditClient`) and `PolicyBridge` (`Internal`, reaching `QuoteController`,
  `TenantStore` and `CarrierGateway`) are patterns of one. Four is below the roll-call threshold of
  five, so **nothing collapses** and every nomination keeps its detail. Under the probe's
  kind-signature grouping all six were one pattern and the whole section collapsed to a line
- five of the six need their own architectural role to reach the span — the four controllers and
  the middleware, all at two kinds through dependencies. `PolicyBridge` is the control: three
  through dependencies alone
- **hubs: 3 nominations**, one per combination of §3.8's disjunction. `ShipmentCoordinator` (7/7,
  16 members, cc 13) on complexity alone; `DispatchRegistry` (5/5, 23 members, cc 1) on size
  alone; `Router` (5/11, 2 members, cc 2) on neither, which is the wiring hub. `IResponseNormalizer`
  (8/3) and `CarrierGateway` (6/2) are the contrasts that make the gate a minimum rather than a
  maximum
- **shared mutable state: 2 nominations.** `DispatchCounter` — one static field, one write, an
  increment, fan-in 5 — is the case that protects `++` counting at all: stop counting increments
  and its count is zero. `QuoteAssembler` has two writes across `Build` and `Reset`, one of them a
  plain assignment, which is why it could not do that job
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

**For the two concealed-decision nominations, once they moved into Core.** Both agree with the
probe on the fixture, and two gaps came out of getting them there. Both are in
`FixtureCoverageTests`, so they fail the day they are filled.

- **Type level adds no subject of its own.** All five of its nominations are also nominated at
  method level, so an extraction that quietly reduced §3.2 to a filter over §3.3 would still
  agree with the probe. The missing case is a type whose complexity is spread across several
  ordinary-looking methods; every complex type in TestBed concentrates it in one. The
  interesting direction *is* covered — seven types are found at method level and nowhere else,
  which is why §3.3 is primary.
- **The identity tiebreak on a finding's order is not observable.** `SolutionModel.Types`
  arrives ordered by identity and LINQ's sort is stable, so a tie group is already in identity
  order before the tiebreak runs — removing it leaves the output byte-identical, confirmed by
  removing it. This is `OrderingTests`'s lesson one level up, and the shuffle that caught it
  there is not available: a `SolutionModel` can only be produced by a walk, so there is no
  permuted one to render from. It will not stay harmless — a detector reading
  `TypeNode.Members`, which is declaration order, inherits no such guarantee.

**For blast radius and load-bearing, once they moved into Core.** Both agree with the probe
exactly, and the interesting result came from mutating rather than from running. Twenty-one
mutations over §3.4 and §3.6: sixteen failed a test, **five did not**, and each can be deleted
today with the suite green. In `FixtureCoverageTests.The_new_findings_have_gates_the_fixture_cannot_observe`.

- **Blast radius' `FanIn >= MinFanIn`, its `FanInXMedian >= 2.0`, and its cohort floor.** The
  first is invariant 1's canonical gate — the one whose absence ranked eight one-caller
  normalizers at the 100th percentile and fired on all of them. One nomination clears all four
  conditions at once, so no single condition is the deciding one.
- **Load-bearing reading *effective* rather than raw fan-out.** `SESSION-NOTES.md` #22 says this
  exclusion does 100% of the discriminating, on a controlled pair that has never been in this
  suite. Both nominees here have `FanOut == FanOutEffective`, so it subtracts nothing and
  swapping in `InstabilityRaw` changes no output.
- **The defect 14 repair itself.** The stranded cohorts contain types that now clear the rank
  gate — `NormalizationContext` at rank 1 of eight, `RawResponse` at rank 2 — and every one
  fails on complexity instead. Only `DistributionTests` catches the floor being removed.

> **The two implementations are not protected by the same thing, and that is the lesson.**
> `The_blast_radius_plant_observes_the_fan_in_floor` reasons that the probe's literals are pinned
> because changing them changes the frozen golden. True — of the probe, which renders it. Core
> renders nothing yet, so its re-implementation of the same gate is held only by the equivalence
> check, and a gate that is redundant on this fixture can be dropped from Core without moving
> Core's nomination set. **Extraction silently halves the protection on every gate it copies**,
> and it will keep doing so until Cli renders from the model.

**~~For breaks alone, once it moved into Core.~~ Filled — one plant closed both.** Nine mutations
found two dead gates; seven types in `Core/Rating/Evaluators.cs` now make both fail.

- ~~**Row 3, `breaks-alone-is-unreferenced`, silences nothing.**~~ Both types that reached the
  finding with no callers were taken by an earlier row — `ShipmentController` as a boundary,
  `AuditReconciler` as a concealed decision. `DetentionEvaluator` is neither.
- ~~**The instability gate can be deleted and no output moves.**~~ `Instability >= 0.8` is the
  *isolated* in "complex inside but isolated"; without it the finding claims nothing more than
  "complex". Every type it held back was **also a concealed decision**, so row 2 removed each one
  before the difference could show. `LaneEvaluator` is complex, referenced, not a boundary and
  not a concealed decision, so the gate is now the only thing keeping it silent.

> **What the plant had to supply, and why nothing in the fixture already did.** Concealed
> decision fires on `CyclomaticXMedian >= 3.0` against the peer group's *method* population, so
> any type whose complexity stands out against its peers is one — and every complex type in
> TestBed concentrates its complexity in a single method. The plant is a cohort of six evaluators
> of **comparable** complexity, which puts twelve similar values in that population and leaves no
> member three times the median. It is not a dodge: a uniform family of rule evaluators is what
> genuinely-complex-but-not-anomalous code looks like, and the tool staying quiet about it is
> correct.
>
> **A suppression can mask the detector beneath it, and that is a failure mode §4 does not
> warn about.** §4's concern is a suppression that stops working and produces more output. This
> is the reverse: a suppression working so broadly that the gate underneath stops being tested,
> and the finding keeps passing every test with half its meaning removed. Look for it wherever
> two rules remove the same population.

> **The same plant replaced a control that rested on a defect.** `SurchargeEvaluator` survives
> breaks alone with a peer group of six. `RoutingDepot` survives only because defect 10 strips
> its concealed-decision nomination, so before this the §15 divergence test would have emptied
> the moment defect 10 was fixed. Both are asserted now; the fix can proceed without taking the
> control with it.

> **What the second one would say if the mask lifted.** `ShipmentLedger` has fan-in 11 — the most
> depended-on type in the fixture — and is already nominated as both a bug blast radius and
> load-bearing-and-intricate. Delete the instability gate and the same run also tells the reader
> that if it breaks, it breaks alone. That is invariant 3's exact failure, and the only thing
> preventing it here is an unrelated suppression row.
>
> **A suppression masking a detector's gate is a new failure mode**, and it is not the one §4
> warns about. §4's concern is a suppression that stops working and produces more output. This is
> the reverse: a suppression working so broadly that the detector beneath it stops being tested,
> and the finding would keep passing every test with half its meaning removed. Both gaps need one
> plant — a complex, well-connected type that is *not* a concealed decision. Every complex type
> on TestBed is one.

> **Re-deriving a detector's population in a gap record gets it wrong.** The first version of this
> test looked for unreferenced complex types in the model rather than in the detected set, and
> caught `OrderRepository` and `PayloadTag` — both unreferenced, both complex, and both depending
> on nothing either, so their instability is undefined and they never reach the finding. Read the
> finding set; do not restate the conditions that built it.

**For the unported findings, measured before porting them.** A gate inventory over §3.1, §3.5,
§3.8 and §3.9 — for each condition, is there a case where that condition is the one deciding?
Two were dead and are now planted in `Core/Dispatch/Dispatch.cs`:

- **`MemberCount >= godObjectMembers`.** `ShipmentCoordinator` is the only other bottleneck and
  reaches the branch on complexity, so the size arm of the disjunction never decided anything.
  `DispatchRegistry` reaches it on size alone — 23 members, worst method cc 1. The control moves
  the threshold past it and watches the verdict change from bottleneck to wiring hub, because
  both are output and only one is right.
- **`++` as a static write.** `SESSION-NOTES.md` #20 records missing increment as a real defect,
  and the case planted for it does not protect the fix: `QuoteAssembler` carries an increment
  *and* an assignment, so its count falls 2 → 1 without the support and the finding still fires.
  `DispatchCounter`'s only write is an increment, so its count falls to 0.

> **The inventory corrected itself once, which is the reason to run it rather than reason it.**
> It reported the spans roll-call collapse as one type short of firing. It was not: the
> measurement grouped types by their *dependency* kinds while the finding groups by the whole
> spanning signature, so all six land in one group and the golden has shown the collapsed line
> since `Bridges.cs` was planted. A gate inventory is itself a model of the finding, and a wrong
> model reports a gap that is not there.

> **And it found a wording defect the moment the gate had a case.** The size arm of the hub
> disjunction prints *"AND carries real logic"* about a type whose worst method is cc 1 —
> `DEFECTS.md` §16. The receipts in the same sentence refute it. Nothing could have seen that
> while the arm was unreachable.

> **Two constraints on any further plant, both still binding.** `Bridges.cs` records them: no new
> `ApiBoundary` or `ExternalCall` type, because the fixture sits at nine boundaries and row 5's
> suppression stops being reachable at ten; and no new fan-in on anything that already exists.
> The second is easy to violate by accident — naming the new types `*Handler` pulled
> `SchemaMigrationHandler` into the new suffix cohort and shrank an unrelated peer population
> from 33 to 32. Caught in the golden diff, not by reasoning. Renamed to `*Dispatcher`.

**Still open, and not closed by either plant.** Three gates from §3.4 and §3.6 remain unobserved —
blast radius' absolute fan-in floor and its multiple-of-median, and load-bearing's use of
effective rather than raw fan-out. The evaluator cohort does not reach them: blast radius needs a
type that clears the rank and complexity gates while failing one of those two, and the fan-out
exclusion needs a type depending on *abstractions* rather than concrete ones. Confirmed by
re-running those three mutations after the plant — all three still pass.

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

**For layer span, hubs and shared mutable state, once they moved into Core.** All three agree with
the probe on the fixture. Twelve mutations: **nine failed a test, three did not.** Both of §3.8's
arms and §3.9's single gate are observed — that is what the `Dispatch.cs` plant bought — and all
three survivors belong to §3.1.

- **`MinKindSpan` is vacuous, and no plant can fix it.** Three significant kinds and a floor of
  three make *spans the minimum* and *spans everything* one condition: setting it to 2 changes
  nothing on the fixture and setting it to 4 empties the finding on every solution, forever. This
  is the only entry in this section that is not a missing case — it is `TASKS.md` X4, and it needs
  a decision rather than a type. Recorded in
  `FixtureCoverageTests.The_layer_span_floor_cannot_discriminate_at_three_significant_kinds`, which
  also asserts the fixture half: nothing reaches exactly two, so even a fourth kind would need a
  plant before the floor decided anything.
- **The roll-call collapse has no case**, and closing `DEFECTS.md` §11 is what took it away. The
  largest pattern fell from six to four against a threshold of five. Before the fix the collapse
  was the *only* branch the fixture exercised and the per-type detail had none, so this is a trade
  rather than a loss — and the better half to owe, because the collapse removes detail that §3.1
  calls the finding.
- **Whether a type's own role belongs in the pattern key is undecidable here.** No two spanning
  subjects share a dependency set while differing in their role, so grouping on dependencies alone
  gives the identical partition and dropping the role from the key moves nothing.

  Both need one plant: six types sharing a dependency set for the threshold, two of them differing
  in their own role for the key. `TASKS.md` P6.

> **A stale claim in the requirement, settled by the golden.** `TECHREQ-job-b.md` §3.1 and §5 both
> say the fixture *"exercises only the detail branch"* of the roll-call collapse. It was the exact
> opposite — one group of six against a threshold of five, so only the collapsed line ever
> rendered, and `golden/nominations.verified.txt` has carried nothing else. The requirement is
> corrected at source. Worth noting how it survived: the claim cited `SESSION-NOTES.md` #30, which
> recorded both branches as verified *at the time*, and nobody re-read the golden when the fixture
> moved underneath it.

> **The inventory is now the cheapest step in a port, and it keeps paying.** Two of the three
> survivors above are §3.1's, and the port took an afternoon because §3.8's and §3.9's cases were
> planted before anyone wrote a detector. `TECHREQ-job-b.md` §10 puts B0 before B2 and it means it.

### The complete inventory, measured in one pass

Everything above was found one port at a time, which made a fixed backlog read as fresh decay
every session. This is the whole of it, measured rather than reasoned, by two sweeps over the
**23 named policy values** and every guard in every Core detector. Re-run both when a plant lands.

**Method.** *Leave-one-out*: delete each `if (…) continue;` in turn and run the suite — this asks
whether the **condition** discriminates. *Nudge*: move each policy value one notch each way and
compare the finding set including qualifiers — this asks whether the **constant** does. They are
different questions and a gate can pass one and fail the other, which is why both are here.
`MinKindSpan` is the case that shows it: deleting the condition admits every type in the solution,
so it looks observed, while moving the floor from 3 to 2 changes nothing at all.

**Conditions that discriminate: 20 of 22.** The two that do not are blast radius' absolute fan-in
floor and its multiple-of-median, both already recorded above.

**Constants the fixture cannot see — the real list.** Of 23 values, a one-notch move changes
nothing for these:

| Value | Read by | Why nothing moves |
|---|---|---|
| `OutlierFactor` 3.0 | §3.2, §3.3 | nominations sit at 3.5×–22×; nothing is near the bar |
| `HighCc` 10 | §3.6, §3.7, §3.8, §3.10 | complexity is bimodal — cc 1 or cc 11+ |
| `GodObjectMembers` 20 | §3.8 | `DispatchRegistry` at 23 is the only case; observable at ±4, not ±1 |
| `StableThreshold` 0.2 | §3.6 | nominees at instability 0 and 0.125 |
| `ConcealedFanInCeiling` 2.0 | §3.2 | every nominee is at 0 or infinity, never between |
| `BlastFanInMultiple` 2.0 | §3.4 | the one nominee is at 11× |
| `BlastComplexityPercentile` 70 | §3.4 | the one nominee is at 95.8 |
| `Top` 15 | §3.1 via `RollCallThreshold` | Core does not truncate, and 14/3 and 16/3 both floor to the same threshold |
| `SurfaceOutlierMultiple`, `SurfaceOutlierFloor`, `SurfaceDiscriminationDivisor` | §3.10 | **not ported yet** — Core reads none of them |
| `GlobalFanInPercentile`, `GlobalComplexityPercentile`, `GlobalComplexityFloor` | §3.11 | **not ported yet**, and two are dead on the fixture besides — see below |
| `MinTangle` 4 | graphs | not ported yet |

**The three findings that were still in the probe, inventoried ahead of their ports.** This is the
part that was never done before, and it paid: every prediction below held when the ports landed,
including both of coverage's dead gates. It is also the cheap half — it needs no code, only the
model.

- **§3.5 change cost — ported, and the arm went dead again for a better reason.** `or ApiBoundary`
  was dead under the absolute gate and the `DispatchCallbackController` plant closed it *in the
  probe*, where dropping the arm now fails three tests. Core's converted gate is a share of the
  whole solution, and five callers is rank 20.5 of 128, so the arm is deletable there with the
  suite green. **This is "extraction halves the protection" in a new form**: previously Core's
  copy of a gate was unprotected because nothing rendered it; here it is unprotected because Core
  and the probe no longer gate the same way, and the plant was built for the probe's. Closing it
  needs a boundary in the solution's top slice — realistically a base controller, which this
  fixture has at `ControllerBase` fan-in 8 but classifies `Internal` for want of the name suffix.
  Reaching a limit of 6.9 takes fan-in 11. Recorded rather than forced, because the alternative is
  picking `ChangeCostTopFraction` to admit our own plant.
- **§3.10 boundary marking — ported.** *Boundaries carrying real logic* discriminates, two of
  fifteen. *Widest contract surface* discriminates, seven of fifteen after P4 — and its suppression
  is reachable from both sides for the first time. All four mutations over the detector and the new
  row fail a test.
- **§3.11 coverage — ported, and the inventory called both gates correctly in advance.**
  `GlobalComplexityPercentile` discriminates: three of the thirteen clear it (`OrderRepository` 98,
  `PayloadTag` 95.2, `RoutingDepot` 91.7). **`GlobalComplexityFloor` is dead** — no below-floor type
  clears the percentile while failing the floor, so the absolute floor never decides.
  **`GlobalFanInPercentile` is observable downward only**: not one of the thirteen reaches the 90th
  percentile by fan-in solution-wide, so raising it changes nothing and only the negative assertion
  stops it being lowered. That is close to structural — a type with no peers usually has few
  callers — so its plant has to be deliberate: a lone component much of the system depends on,
  which is the case §3.11 exists for and the fixture has never had.

> **The one structural finding, and it explains the rest.** Loosening a threshold by one notch
> moves output for **3 of 23** values. Nearly every gate has slack on both sides: types clear a
> gate comfortably or fail it comfortably, and almost nothing sits just outside one. That is what
> a fixture built by planting *positive* cases looks like — every plant so far has answered "does
> this finding fire?" and none has answered "is this the number at which it stops firing?"
>
> It is also why gates keep reading as dead, and why the remedy is one plant rather than eleven. A
> deliberate **near-miss band** — cc 9 against a floor of 10, fan-in 4 against 5, instability 0.78
> against 0.8, member count 19 against 20 — would make most of the table above observable at once.
> `TASKS.md` P7.

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

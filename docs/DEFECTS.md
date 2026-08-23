# Known defects

Behaviour that is wrong today, recorded rather than fixed. Every entry names what supersedes it.

This was also the work order for extraction. Most are fixed in `Bearing.Core` — **18, 19 and 20
are fixed in `Bearing.Cli`**, and they are a different class: not defects carried through the
port, but ones the port made visible by producing a report a user could read. All three were
found by running the shipped binary on a real solution, and none of them can be caught by the
suite. See *How these were found*.

## Why a defect gets recorded instead of fixed

**Historically, because the probe was frozen.** `oracle/ArchProbe` was the fixed point of an
in-flight refactor: extraction moved ~997 lines of computation out of its `Report.cs`, and the
probe was what separated *"I broke it"* from *"I changed it on purpose."* Stillness was the
property it provided, not correctness — an oracle wrong in a known way discharges that job
perfectly — so a correctness defect never justified editing it, and only defect 6, which stopped
it functioning as an oracle at all, was ever fixed in place.

**That regime ended at `TASKS.md` R2, and with it the two things that enforced it.**
`KnownDefectTests` pinned each defect by asserting the probe's wrong behaviour as current; it is
gone, because every assertion in it was a statement about an implementation that no longer
exists. Defect 1 is why that is no loss rather than a gap: Core had keyed type identity on
`(assembly, FQN)` since `ModelBuilder` adopted `SubjectRef`, and the pin stayed green throughout,
so it could not tell you whether the defect was live. **A pin against a frozen implementation
cannot fail when the live one starts doing the right thing.** The equivalence suite — which ran
both implementations and compared them — is gone with it.

**So an entry here is now a claim about the shipped tool, and nothing else pins it.** What
replaced the pins is that each fixed defect has an ordinary test naming the behaviour it
requires, in the suite that owns that behaviour. A defect that is still live and still wants
watching needs one of those, not a pin; see D37, whose fix is asserted by `OrderingTests` and
whose absence would fail there.

## The register

Roughly severity-ordered. Read each entry's own status line: **"fixed in Core" and "fixed" meant
different things while the probe was still rendering**, and every entry that says the former was
written when the shipped tool still carried the defect. R1 moved rendering to `Bearing.Cli` and
R2 removed the other implementation, so both now mean the same thing.

### 1. Type identity is keyed on fully-qualified name alone

.NET permits the same FQN in two assemblies, and plugin architectures use it deliberately. The
two rows merge, their fan-in, fan-out, complexity and LOC are **summed**, and one type's edges
are attributed to the other's project. On nopCommerce this fabricated a five-project circular
reference — a shipping finding, wrong.

Worse than a merge: **which project is blamed is decided by load order.** Reversing the project
declaration order in `TestBed.sln` moves the row's `Project`, `File` and `Line` wholesale while
every measure stays byte-identical, and it takes the project abstractness ratio with it. So the
finding's *identity*, not just its severity, can differ between two machines analysing the same
commit.

**Fixed in Core.** `SubjectRef.ForType(assembly, fqn)` is the correct key — `ARCHITECTURE.md` §4 —
and Core computes with it: `ModelBuilder.GetOrAdd` keys the type table on `subject.Canonical`, and
`CollectReferences` keys the edge map on the canonical pair, so nodes *and* edges are
assembly-qualified. This is the one behaviour extraction is permitted to change
(`TECHREQ-job-b.md` §8 criterion 8), and `WalkTests` asserts the divergence from
three sides: Core reports exactly one type more than the probe, it keeps both `PayloadTag`
declarations, and each is attributed to the project that declares it rather than to whichever
loaded first.

**Live in the probe, and it stays there.** The oracle is frozen, so the merge remains in the
goldens until the renderer moves off them at R1.

**Still owed: a case where the difference decides something.** Both `PayloadTag` declarations have
fan-in 0, and the type appears in neither the namespace cycle nor the eight-type tangle. A type
with no inbound edges cannot be in a strongly-connected component, so merging or splitting it
yields identical components — Core's cycle output will match the probe's exactly, and the
nopCommerce fabrication has no fixture analogue. The obvious repair is the wrong one: giving
`PayloadTag` fan-in adds inbound edges to a type that already exists and disarms the
unreferenced-type traps that name it (`FixtureCoverageTests`, and both fan-in-0 lists in the
goldens). The plant is a **new** colliding pair, declared in two assemblies and sitting inside a
cycle. Until it exists, this fix is asserted and not observed — `docs/TESTING.md` §6.

Pinned: `Two_types_sharing_a_name_across_assemblies_merge_into_one_row` — a pin on the **probe's**
behaviour, not a guard on Core's. It asserts against the probe's run, and the probe cannot change,
so it can never fire. It retires with the oracle at R2.

> **The case where the difference decides something has now been observed — on nopCommerce, not
> in the fixture.** `SPIKE-job-a-prior-art.md` §7 recorded the exact mechanism:
> `Nop.Data.Mapping.BaseNameCompatibility` is a partial class in `Nop.Data` and is declared again
> in the Avalara tax plugin, a deliberate nopCommerce extension pattern. Keyed by name the two
> merge, the plugin's outbound references are attributed to `Nop.Data`, and the phantom edge closes
> a **five-project cycle**: `Nop.Data → Nop.Plugin.Tax.Avalara → Nop.Services → Nop.Web →
> Nop.Web.Framework`.
>
> Run at A3, Core reports **two rows and no project cycle**. Both declarations are present, each
> attributed to the project that declares it — `type|Nop.Data|…` with fan-out 28 and
> `type|Nop.Plugin.Tax.Avalara|…` with fan-out 2 — and a scan of all 3,209 types finds **exactly
> one** FQN declared in two assemblies, which is the count the spike found by reading source.
>
> **This is an observation and not a regression test.** It does not run in CI and it will not fail
> if the fix regresses. **P8 is still wanted**, and its justification is unchanged. What has moved
> is that the fix is no longer only *asserted*: the thing it was supposed to prevent is measured as
> absent, on the codebase where it was measured as present.

### 2. Absolute gates do not travel between codebases — **closed 2026-08-21**

Change cost fires on 7.9% of nopCommerce, hubs on 6.9% of Jellyfin, both truncated to 15 by
`--top`. Blast radius — the only percentile-gated finding — held at 1.0% and 0.9% across both.

Convert, do not retune. But see defect 14: converting runs *toward* a different hazard.

**Change cost is converted**, being the worst of them: `ChangeCostTopFraction`, a share of the
**whole solution** by fan-in, beside the absolute floor rather than instead of it — §3.4 keeps its
floor for invariant 1's reason, and a share alone crowns the top of a population where nothing is
tall.

**Solution-wide, not per-cohort, and that is a choice of reader rather than of arithmetic.**
Within-cohort answers *"which controller is riskiest to change"* — a maintainer's question, for
someone who already knows the codebase. Solution-wide answers *"which part of this application is
riskiest to change"*, which is what someone arriving at an unfamiliar codebase is asking, and it
is what §3.5 was written for: not cohort-gated, running over all types, so a lone contract with
thirty callers is not silenced for having no peers. Both are real findings and the maintainer's
view is a second nomination set, not a wording change — `TASKS.md` X8.

It is also the more defensible of the two against a codebase nobody has seen. The spike found
cohort **basis** to be the measure that swung hardest between the two solutions — name suffix
55.4% → 33.9%, base type 14.3% → 44.8% — so a within-cohort gate inherits that instability, while
a share of the solution is that share at any size.

**Hubs and breaks alone are not converted.** The reason recorded here was *"not until there is a
fixture case that can observe the difference: `TASKS.md` P7"*. **P7 landed and they still did not
move**, which is what prompted rereading this entry rather than working it.

---

**Re-measured 2026-08-20, on both reference solutions, and rewritten.** The diagnosis in the title
holds and is the durable part:

| `HubMin = 5` | findings | share | equivalent rank cut |
|---|---|---|---|
| nopCommerce (3,209 types) | 117 | **3.6%** | ≈ top 3% |
| jellyfin (1,545 types) | 106 | **6.9%** | ≈ top 5% |

One threshold, two codebases, nearly double the share. That is the whole claim and it reproduces.

**What does not hold is the remedy this entry prescribes, and that is why it has never finished.**
*"Convert to percentiles"* is not what either conversion actually did:

- **Change cost** uses `ChangeCostTopFraction`, a share of the whole solution **beside the absolute
  floor rather than instead of it** — this entry says so itself, four paragraphs up.
- **Blast radius**, under defect 14, replaced a percentile threshold with a **midrank rank
  position**, `rank <= max(1, fraction × n + 0.5)`, and kept its absolute floor. Defect 14 states
  outright that it *"needs its own answer rather than falling out of defect 2's
  absolute-to-percentile conversion, because that conversion runs toward this hazard."*

**So the proven form is a top-fraction midrank position kept beside the absolute floor**, and this
entry has been asking for something else the whole time.

**And the remaining conversion is under-determined, which the numbers show.** The deciding value is
`min(fan-in, fan-out)`, a small integer with enormous tie groups: 117 types sit at ≥ 5 on
nopCommerce, and relaxing by one integer to ≥ 4 admits 186 — **59% more for one step**. Where a
rank gate lands is therefore decided by tie handling and by which population it ranks over, neither
of which "convert to percentiles" specifies. That is defect 14's territory, and it is a choice
rather than a repair.

**What is left of this entry is one job and one decision, and they are now filed apart.**

- **The decision is `TASKS.md` X13** — thresholds global or calibrated per codebase, which
  `ARCHITECTURE.md` §11 has carried as an open question all along while citing this entry to narrow
  it. A defect register is for behaviour that is wrong with a remedy already understood; this half
  never met that condition, and filing it here is what let it be deferred five times instead of
  decided once.
- **The job**, once X13 is answered, is applying the proven form to hubs and breaks alone. It is an
  hour against a known pattern rather than an open question.

**Both are done, and the job turned out not to be the conversion.** X13 answered the decision by
keeping hubs and breaks alone **absolute** — converting them is what would erase the finding, since
a rank gate cannot report that one codebase is more coupled than another when every codebase has a
top 5%. What X13 required instead was that they **say why**, which is what this entry's diagnosis
was always for: the share is a fact about the codebase and a reader comparing two reports has to be
told.

**Both renderers now disclose it**, worded once in `Claims.ShareCaveat` and read by each:

```
   117 types of 3209 — 3.6%. This threshold is a fixed count rather
   than a share, so the percentage differs between codebases: compare
   what is named, not how many.
```

3.6% on nopCommerce and 6.9% on jellyfin — the table above, printed by the tool itself.
`AbsoluteGateTests` holds the set of absolute kinds transcribed rather than derived, and holds the
other half too: a comparative gate says nothing of the kind, because there the share *is* the gate
and claiming it varies would be false.

**Do not quote the old percentages.** Hubs 6.9% and breaks alone 2.8% predate the
concealed-decision fix; breaks alone was `(none)` on nopCommerce and is now 27. The table above is
current as of 2026-08-20 and was taken after defect 5, which does not touch fan-in or fan-out.

### 3. Truncation is never disclosed

15 of 106 shown, and nothing says so. `ARCHITECTURE.md` invariant 8.

### 4. Load success is judged by diagnostic, not outcome — **fixed**

All six nopCommerce "load failures" were NuGet vulnerability advisories; every project loaded. A
hard-fail rule would refuse a major .NET codebase over unrelated CVEs.

**MSBuild raises a package advisory as a `Failure`-kind workspace diagnostic**, so the list the
walk collected could never support the claim hung on it. The wording had already retreated to
*"these are not necessarily failures"* while still telling a reader to treat every number on the
page as a lower bound until they had ruled six CVEs out — a hedge in front of a claim, which is
the shape of a judgement made from the wrong evidence.

**Fixed 2026-08-20 by recording the outcome.** `Coverage.ProjectsNotLoaded` names the projects
that were selected and produced no compilation; the walk already knew this and threw it away. The
lower-bound warning now hangs on that list, which is the only fact that supports it, and the
diagnostics are shown as diagnostics. Both renderers state the outcome either way — invariant 8:
*"every project loaded"* is the reassurance a reader needs before trusting a fan-in, and the
absence of a warning is not that.

**On nopCommerce the section now lists the six advisories and then says every project compiled.**
The HTML used to justify the hedge with an anecdote about a different solution — *"on one
reference solution every one of them was a NuGet vulnerability advisory, and 3,209 types loaded
anyway"* — and now states the outcome of the run in front of the reader.

Two tests, because the defect is a pair: `A_load_diagnostic_is_shown_without_being_called_a_failure`
asserts the lower-bound wording is *absent* when nothing failed, and
`A_project_that_did_not_load_is_what_bounds_the_numbers` asserts it is present, and names the
project, when something did.

### 5. `DataAccess` classification is a hardcoded list of four ORMs — **fixed**

The list is `Microsoft.EntityFrameworkCore`, `System.Data`, `Dapper`, `NHibernate`, plus a
`base:DbContext` rule. It misses **LinqToDB** and **FluentMigrator**, which is what nopCommerce
uses.

**Re-measured 2026-08-20 against `nop-v5`, and the consequence this entry used to claim was
wrong.** It said the data layer *"reads as `Internal`"* and *"layer-span goes silent"*. Neither
holds:

- `DataAccess` fires **23 times**, 20 of them inside `Nop.Data` — but **every one of the 23 is
  matched by `System.Data*`**, not by an ORM rule and not by `base:DbContext`. The classification
  is right by coincidence, which is the same shape as defect 2's `HubMin = 5`.
- Layer span is **not** silent: `NopStartup` reaches across three kinds and is nominated.
- What is actually missed is the largest coherent group in the project: **114 of `Nop.Data`'s 129
  `Internal` types are the `*Builder` mapping layer** under `Nop.Data/Mapping/Builders/`, one per
  entity, all touching `FluentMigrator.Builders`. They are data access by any reading and they
  are classified `Internal`.

**So the failure is a misclassification of 114 types, not a silence.** That is worse than the
entry claimed, because kind is not cosmetic — it is a cohort basis, it gates layer span, and it
feeds effective fan-out and the boundary section.

**It does not move the plot.** A project's density is findings over *all* its types regardless of
kind, so `Nop.Data`'s 12-of-153 reading is unaffected — worth stating because `A11` round 2
pre-registers that project as *"surprisingly clean"* and a fix here does not disturb it.

**Fixed 2026-08-20** by adding `LinqToDB` and `FluentMigrator` to the prefix list. Measured on both
reference solutions before and after:

| | nopCommerce | jellyfin |
|---|---|---|
| types reclassified | **134**, all `Internal` → `DataAccess` | **0** |
| `DataAccess` | 23 → 157 | 24 → 24 |
| evidence | 129 `FluentMigrator.Builders`, 15 `FluentMigrator`, 7 `LinqToDB`, 1 `FluentMigrator.Runner` | unchanged — it uses EF Core, already on the list |

**jellyfin is the control and it does not move**, which is what says this is a gap in the list
rather than a rule tuned to one codebase.

**What it changed downstream is larger than the classification, and is the point.** `Kind` is a
cohort basis and it gates layer span, so on nopCommerce:

- **Layer span went from 1 finding to 5.** It fired only on `NopStartup` before, because the
  `DataAccess` arm of "reaches across three kinds" could not fire on a solution whose data layer
  read as `Internal`. The four it now finds are the ones the finding exists for —
  `AvalaraTaxManager`, `OmnisendService`, `FacebookPixelService` and `InstallationService`, each
  reaching an API boundary, a data provider and an outbound HTTP client.
- **The lead claim changed.** Selection is rarest-kind-first (X10), and layer span was rarest at
  one. At five it is no longer, so the report now opens on `FormValueRequiredAttribute`
  (load-bearing and intricate, 1 of 4). **`TASKS.md`'s note that "layer span leads on both real
  solutions" was true and is now false for nopCommerce.**
- Concealed decision 79 → 78 and no-peer-group 107 → 106, both from cohorts recomposing around the
  new kind.

**The fixture cannot see any of this and no test asserts it.** TestBed references neither library —
it cannot, without taking a package dependency for a classification rule — so the suite is green
and unchanged either way. That is the same shape as the gaps `TASKS.md` Track P exists for, and it
is recorded there rather than left implied. The evidence for this fix is the two real-solution
runs above, and re-running them is how it stays true.

### 6. Visit-order dependence — **resolved**

The one defect that could have disqualified the oracle, so it was checked before phase 1 rather
than recorded.

Determinism held: six separate processes produced byte-identical output. Separate processes
matter, because .NET randomises string hashing per process and a single-process repeat proves
nothing.

But the stability was accidental. Every writer sorted on a non-total key — 257 of 261 edges tie
on `Weight` alone, 89 of 108 types on `(Cohort, MaxMemberCyclomaticXMedian)` — so most row
positions were settled by dictionary enumeration order, which is insertion order, which is
project load order. Reversing the project order in `TestBed.sln` moved all of it while leaving
the row multiset identical.

That had to be fixed in place, because Core will not reproduce the probe's incidental insertion
order however correct its numbers are, and the oracle diff would have gone red on ordering the
day Core computed anything. Total keys everywhere, `StronglyConnected` returns a canonical form,
and `OrderingTests` holds the property by re-rendering from a shuffled analysis. See
`TESTING.md` §5.

Layout — the original form of this defect, three different layouts from one dataset — remains
unfixed, but layout is in no golden and blocks nothing.

### 7. 1.4–2.0% of edges point at absent types — **fixed in Core at A2, and the rate was the least of it**

Including Roslyn anonymous types, which should never be graph nodes.

**The recorded consequence was wrong.** This sat on the register as a small inaccuracy — a low
single-digit percentage of edges going nowhere — and it was in fact a hard crash.
`ModelBuilder.Build` resolved both endpoints of every edge with an unguarded dictionary lookup, so
the first absent endpoint threw `KeyNotFoundException` and the run produced no report at all.
Bearing could not analyse **either** reference solution: Jellyfin died at 73s on
`Jellyfin.Server.Implementations.Users.UserManager`, nopCommerce at 142s on an anonymous type
(§22).

The mistake underneath is a question swapped for a similar one. `_isInSolution` answers *"does this
symbol belong to a project in this solution"*. The lookup needed *"did the walk build a node for
it"*, and those differ for every type skipped by a path exclusion, living in a skipped project, or
generated by the compiler — all of which still resolve to symbols a reference can point at.

Such edges are now dropped rather than invented, and **counted rather than dropped silently**:
`Coverage.EdgesToUnanalysedTypes`, disclosed in `-- WHAT WAS NOT ANALYSED` on every run, including
when it is zero. Measured rates: **123 of Jellyfin's edges, 57 of nopCommerce's**.

> **It was invisible on TestBed and fatal on real code**, and the fixture could not have caught it:
> every type TestBed declares is analysed, so no edge has ever had an absent endpoint. The only
> movement in the snapshot after the fix is one new line reading `none`. `docs/TESTING.md` §6 owes
> this gap a row.

### 8. `.slnx` solutions do not load at all — **fixed**

Orchard Core could not be analysed. Cannot block extraction — TestBed is a `.sln`.

Reproduced deliberately at A2 against a hand-written `.slnx` over TestBed's three projects:
`Microsoft.Build.Construction.SolutionFile.Parse` throws `InvalidProjectFileException: No file
format header found`. **And it takes the tool down with it** — eleven frames of MSBuild stack
trace, straight at the user, which is §23 and a separate defect from this one.

**Fixed, and not by moving off the toolchain the goldens were measured on.** The obvious repair —
upgrade Roslyn until `MSBuildWorkspace` reads `.slnx` — was available and was the wrong one four
days before a review: it is a jump from 4.12 to 5.9, it changes symbol resolution, and every
golden in the suite is measured against the current one. Nothing about that risk is repaid by a
solution format.

What was actually missing is smaller than it looked. **Only the container is new.** A `.slnx`
names the same `.csproj` files a `.sln` does, and MSBuild evaluates each of those exactly as it
always has — so `SolutionWalker.OpenProjectsAsync` reads the file with
`Microsoft.VisualStudio.SolutionPersistence`, the serializer Visual Studio and the SDK use, and
hands each path to `OpenProjectAsync`. Confirmed on nopCommerce: a `.slnx` listing its 28 projects
produces a report byte-identical to the one from `NopCommerce.sln`, across 3,209 types, differing
only in the file name in the banner.

Two things the repair had to get right, and one of them was found by the test rather than by
reading:

- **`OpenProjectAsync` follows project references**, so a project pulled in by an earlier one is
  already present when its own turn comes — and the second open throws `'Core' is already part of
  the workspace` rather than doing nothing. Any solution whose projects reference each other hits
  this, which is most of them. The workspace is re-read on each iteration rather than a set being
  accumulated, because opening one project can add several.
- **An empty `<Solution />` is valid and now parses**, so it is a walk over nothing rather than a
  failure — which is what an empty `.sln` already produced. Two tests in
  `SolutionLoadFailureTests` had been using it as their unreadable input and now use a genuinely
  malformed file; a test asserting a failure that has stopped being one is a test that passes for
  the wrong reason.

**§23's design note paid.** It predicted that the `.slnx` advice would stop being reached the day
the load learned to succeed, which is why the extension is read after the failure and never
before. That is this day. The sentence itself changed, because what a `.slnx` failure means is now
a malformed file or a project path that does not resolve — not a format the tool cannot read.

### 9. Change cost gates on `minCohort` where it means a fan-in floor — **fixed in Core**

Both default to 5, so the two are indistinguishable at defaults and the defect is invisible in
the goldens. It appears only when either is tuned — which is exactly when someone is relying on
the threshold to mean what it says. Superseded by defect 2: the answer is a percentile.

`ChangeCost` reads `MinFanIn`, and the two are pinned apart by
`Change_cost_reads_the_fan_in_floor_and_not_the_cohort_floor` — moving `MinCohort` to 16 must not
move the finding, and moving `MinFanIn` to 16 must. Reverting the floor to `MinCohort` fails a
test, which it could not do while both read 5.

**Defect 2 is only half-closed by the same change, and the half that is closed is this finding.**
The share gate below makes change cost self-limiting; hubs at 6.9% and breaks alone at 2.8% are
untouched and still absolute.

Still pinned against the probe: `Change_cost_is_gated_by_min_cohort_where_it_means_min_fan_in`.
The probe is unchanged and retires at `TASKS.md` R2.

### 10. The cohort floor strips a suppression it was never meant to touch

`breaksAlone` runs over all types — "no cohort required" — but reads its concealed-decision
exclusion from a list built out of the cohort-gated `eligible`. A small peer group therefore
drops a type out of concealed decision and straight into breaks alone. `RoutingDepot` is told it
breaks alone at cc 12 purely because it has three peers instead of five, and *lowering*
`--min-cohort` to 3 removes the contradiction.

Violates invariant 3 — two findings contradicting each other about one component.

**Core inherits it, and that is not an oversight.** Making suppression a declared rule (defect 15)
does not help: the rule searches for a concealed-decision nomination that the cohort floor stops
anyone from making. Fixing it means deciding what a below-floor type may be nominated *as*, which
is `ARCHITECTURE.md` §11's thresholds-global-vs-calibrated question, not a local repair.

~~**Fixing it costs defect 15's control.**~~ **It did, and no longer does.** `RoutingDepot`
survives breaks alone precisely because its cohort of three strips its concealed-decision
nomination, so the day a below-floor type can be nominated it leaves the finding — and until
`Core/Rating/Evaluators.cs` was planted it was the *only* survivor, which would have left §15's
divergence test asserting an absence rather than a difference. `SurchargeEvaluator` is the
replacement: it survives on a peer group of six, so its survival does not depend on this defect.
Both are pinned, and fixing this one can now proceed on its own.
Pinned: `The_surviving_control_survives_because_of_a_different_live_defect`.

Pinned: `The_cohort_floor_strips_the_concealed_decision_suppression_from_breaks_alone`.

**Measured 2026-08-21: incidence on both reference solutions is zero.** Cross-referencing every
breaks-alone finding, uncapped, against its cohort size: nopCommerce 0 of 27, Jellyfin 0 of 14. Not
one sits in a below-floor cohort. `RoutingDepot` is a TestBed plant; the invariant-3 violation is
real and pinned and it does not occur in the wild.

**And the obvious repair is not available.** Lowering the floor looks like it fixes this — it does
on the fixture — but `MinCohort` also selects *which basis* a type is compared against, so moving
it 5 → 3 re-bases 155 of Jellyfin's 1,502 types. `ConditionProcessor` leaves breaks alone at 3 with
nothing gated at all: it moves from `ns:MediaBrowser.Model.Dlna` (8 peers) to `suffix:Processor`
(3), where three peers make cc 80 an outlier. A fix aimed here would have changed every percentile
in the report and been credited with the wrong mechanism.

**The parameter split is done** — `CohortBasisFloor` owns selection, `MinCohort` keeps sufficiency
— and it does not close this. Letting thin cohorts nominate runs into `ConcealedTopRank`, which is
3: `Reading.Rank` is midrank from the top, so in a cohort of three the ranks are 1, 2 and 3 and the
gate admits every member. The gate that exists to stop the finding growing with the size of the
codebase goes vacuous at exactly the cohort sizes this entry is about.

**That is the third repair in a row to open a new one, and the recurrence is the finding.** This is
one of ten entries — §2, §9, §10, §14, §17, §19, §28, §33, §34, §38 — that are the same design
decision failing, each repaired locally by adding or moving a threshold on *cohort size*. Measured
on both solutions, size is the wrong variable: 88% of nopCommerce's method cohorts have a median of
0 or 1, so `3x median` **is** `cc >= 3`; 74% have zero dispersion; and `base:BaseNopModel` has
**1,656 members with MAD 0**. A cohort of three and a cohort of 1,656 fail identically.

**So this is a symptom and it should not be repaired again.** It carries zero field incidence and it
now waits on **X16** — how a cohort-relative claim is gated at all — in `ARCHITECTURE.md` §11.

### 11. The layer-span collapse hides the anomaly it shares a signature with — **fixed in Core**

The collapse assumes a shared kind signature means a shared phenomenon. Four boilerplate
controllers and one genuine anomaly carry the identical signature, and the collapse absorbs the
anomaly — losing exactly the detail block that made it actionable.

The examples kept are ordered by fan-in and cut at four, and five of six tie at zero, so which
names survive was settled by enumeration order. Defect 6's fix made that *reproducible* — and
changed which name survives, once, as a direct result. It did not make it *right*: a tiebreak is
not a requirement.

**The requirement it was missing was in §3.1 the whole time.** *"The named dependencies per kind
are the finding, not the count."* If the names are the finding, the names are what makes two
findings the same finding — so grouping on the count discards the thing the section calls the
point, and that is exactly what lets a middleware reaching `TenantStore` and `AuditClient` be
counted as another controller wired to `TenantStore` and `CarrierGateway`. `SpansArchitecturalLayers`
groups on the type's own architectural role plus its named dependencies. On the fixture the four
controllers stay one pattern and both anomalies keep their detail.

**The second deliberate divergence from the oracle, and unlike the first it withdraws nothing.**
The nomination set is identical to the probe's — `Layer_span_nominations_are_the_probes` — and what
moves is which subjects may have their detail collapsed. That is also why the collapse is a
qualifier rather than a suppression row: the probe keeps every collapsed type named in its examples
line, so the claim survives and only the detail is dropped, which is row 6's shape rather than row
1's.

The ordering half is fixed with it. Rarest pattern first, then fan-in, then identity — a total
order, held by `Layer_span_emits_the_rarest_pattern_first`, which fails if the rank is dropped.

Still pinned against the probe: `The_layer_span_collapse_hides_the_anomaly_it_shares_a_signature_with`.
The probe is unchanged and the golden still carries the collapsed line; the pin retires with the
oracle at `TASKS.md` R2.

**What the fix cost.** The collapse branch now has no fixture case — the largest pattern falls from
six to four against a threshold of five — where before the fix it was the *only* branch the fixture
exercised and the per-type detail had none. Recorded in
`FixtureCoverageTests.The_roll_call_collapse_has_no_case_under_the_named_dependency_grouping`, with
the plant it needs.

### 12. `WIDEST CONTRACT SURFACE` can never be suppressed, at any boundary count — **fixed in Core**

The filter is `DataShape >= max(1.5 × median, 1)`, so qualifying boundaries always come from the
upper half and the set can never exceed `floor(n/2)` — precisely the number the suppression
requires it to *exceed*. It lands on the threshold at every n and never crosses. The `Take(5)`
cap is a second ceiling above ten boundaries but is not the cause; removing it changes nothing.

The only entry here that cannot be fixed by moving a constant: **a proportional gate cannot sit
on a filter proportional to the same distribution.**

**Decided: an absolute count ceiling.** `bigSurface.Count <= MaxNamedSurfaces`, replacing
`Math.Max(1, boundaries.Count / 2)`, with the default at the five the `Take` already imposes.

The trap ruled out two of the three candidates. Any gate phrased as "the qualifying set is too
large a *share*" tests a set the filter has already bounded by that share, so a dispersion test
on the same statistic inherits the defect — "is the top separated from the middle" is what
`1.5 × median` already asks, and it is non-empty exactly when the filter is. What goes wrong in
#28 is not a proportion at all: the section promises to name what stands out and instead reads a
list, and a count is what bounds a list.

It also stops the `Take(5)` being a silent truncation. Past the ceiling the section prints
nothing rather than an arbitrary five of the qualifying boundaries, which is `DEFECTS.md` §3 for
this section.

**~~It is reachable but not yet observable.~~ Superseded — read the "built, and observable from
both sides" paragraph below instead.** The reasoning is kept because it is what the decision was
taken against: the qualifying set is bounded by `floor(n/2)`, so firing would need at least twelve
boundaries with six qualifying, and TestBed had **ten** with a maximum of five able to qualify.
P4 has since taken the fixture to **fifteen**, and the ceiling is an absolute count rather than
`floor(n/2)`, so both halves of that arithmetic are now historical.

**This entry was read backwards for several sessions, and it cost three plants.** "Can never be
suppressed at any boundary count" was turned into a *constraint on the fixture* — no new
`ApiBoundary` or `ExternalCall` type — recorded in `Bridges.cs`, `Dispatch.cs`, `TASKS.md` X1 and
`docs/TESTING.md`, with two mutually contradictory justifications: that a tenth boundary makes the
suppression reachable, and that a tenth boundary stops it being reachable. Both are wrong and this
section says so. The pin is a synthetic proof over the distributions that *maximise* the
qualifying set; the fixture appears in it only as a count. Going to ten moved that literal and
nothing else. Withdrawn as decision X1, which is what unblocked the change-cost plant, P4 and F9.

**A correction to the reasoning above, found while checking it.** *"Qualifying boundaries always
come from the upper half"* holds only while the median is above `2/3`. Below that the
`max(…, 1)` floor takes over, the gate stops being proportional, and more than half can qualify —
`[0,0,0,0,0,1,1,1,1,1,1,1]` puts seven of twelve over the line. The conclusion survives because
the `Take(5)` cap catches it, so the claim *"removing it changes nothing"* is the part that is
wrong rather than the verdict. Worth keeping: the decided replacement is an absolute count, and
this is the second way the proportional form misbehaves.

**Built, and observable from both sides for the first time.** `AnalysisPolicy.MaxNamedSurfaces`
replaces the proportional ceiling, and P4 planted the population that reaches it: five ops
endpoints of surface 1 drag the median from 7.5 to 4, which takes the qualifying set from one to
seven. Suppression row 5 withdraws the set as a set — the first row that suppresses a *set* rather
than a subject, because what is wrong is its size and not anything about the types in it. Raising
the ceiling to seven brings all of them back, which is the control the proportional form could
never offer.

**The plant works from below, and that is the defect restated as a construction.** It adds no wide
surfaces; it adds narrow ones. A filter proportional to the median can be driven from below by
boilerplate, so a codebase full of thin endpoints makes its own broad surfaces "stand out" without
one of them changing.

**The probe's output is now visibly the failure**, which is worth more than the argument was: it
names five of the seven qualifiers, silently truncated by `Take(5)` — a roll-call of controllers,
the exact thing §3.10 says this section replaced. Core names none and says so. Fourth deliberate
divergence.

Pinned: `Widest_contract_surface_can_never_be_suppressed`, and the pin is sharper than it was.
With seven qualifiers the proportional gate *still* cannot fire, because the `Take(5)` caps the
count at five while the ceiling rose with the population to seven. **Adding qualifiers made it
less able to fire.**

### 13. `MethodMetrics.Id` is not an identifier — it is the bare method name

`SymbolDisplayFormat.FullyQualifiedFormat` qualifies *type* symbols and leaves *member* symbols
bare, so `Fq(decl)` yields `Reconcile`, `Apply`, `Post`. TestBed alone has **17 colliding groups,
one of them 12 members wide** — every `Apply` in the solution shares one Id.

Invisible until now because **nothing ever read the field**. It is written on every method and
used nowhere, so no output is wrong today and there is nothing to pin.

**It is load-bearing for the finding key.** `SubjectRef` admits a member as a subject, and
method-level concealed decision is precisely the nomination that needs one. A member subject
built on this would collide across every type sharing a method name — silently merging findings
about different components, in exactly the way defect 1 merges types. The remedy is the same
shape: `SymbolDisplayMemberOptions.IncludeContainingType`, and key a member as
`(declaring type, signature)`.

Worked around in `Report.cs` with `DeclaringTypeId` as a sort tiebreak; the field itself is left
alone for Core.

**Closed at A5, and it was closed by construction rather than repaired.** Core never had the
defect: `SubjectRef.ForMember(assembly, declaring type, signature)` is what `ModelBuilder` keys a
member on, which is the remedy above, arrived at when member subjects were built rather than when
a CSV needed them. What A5 changes is that the identity is now **published** — `members.csv` has an
`Id` column and `types.csv` has a matching one, so the two files join without a heuristic where the
probe's `methods.csv` emits a column nothing can join on.

The board attached this defect to A5 because *"a CSV keyed on a colliding id is worse than no
CSV"*, and that is the right instinct: a file whose key column repeats invites a join that silently
merges rows, which is defect 1's failure mode one level down.
`CsvOutputTests.A_member_id_is_an_identifier_and_not_a_bare_name` asserts uniqueness **and** that
the bare `Name` column still collides — without the second half the test would keep passing over a
fixture that had stopped containing the case.

### 14. A percentile floor can be unsatisfiable, and `FanInPctl >= 95` is

`Percentile` is midrank — `100·(below + 0.5·equal)/n` — so a unique maximum tops out at
`(n-0.5)/n·100`: 90.0 at n=5, 94.44 at n=9. `BUG BLAST RADIUS` therefore **cannot fire in any
cohort of 5–9**, whatever its members look like, while `--min-cohort` admits 5.

Arithmetic, not tuning. Needs its own answer rather than falling out of defect 2's
absolute-to-percentile conversion, because that conversion runs *toward* this hazard.

**Fixed in Core (Aug 2026): a midrank *position* replaces the percentile threshold.** Blast
radius gates on `rank <= max(1, blastTopFraction × n + 0.5)`, `blastTopFraction` defaulting to
0.05 — `Distribution.RankOf` and `Distribution.TopRankLimit`. The probe is unchanged and still
carries the defect; it is the oracle, not the product.

**This is the same gate, not a retune.** Midrank position and midrank percentile are one
statistic — `rank = n(100 − pctl)/100 + 0.5` identically, for every value and every tie
configuration — so substituting it into `pctl >= 95` gives `rank <= 0.05n + 0.5` exactly. The
`+ 0.5` is the midrank offset, not a fudge factor. **Core therefore admits precisely what the
probe admits in every cohort of ten or more**, which is why no golden moved and why
`FindingTests` agrees on the fixture.

`Math.Max(1, …)` is the entire repair. Below n = 10 the percentile form yields a limit under 1,
which no rank can satisfy; flooring it at 1 admits the cohort maximum and nothing else. A
two-way tie for that maximum ranks 1.5 and is still refused — the top of a small group is one
type or it is nobody.

**Why not a fixed rank.** `rank <= 1` is reachable everywhere and would have been simpler, and
it discards the property worth keeping: a percentile-within-cohort gate self-limits by
construction, which is why blast radius held at 1.0% and 0.9% of types across two unrelated real
solutions while the absolute-gated findings ran to 4–7%. Midrank also matters for the same
reason — under competition ranking (`1 + strictly-greater`) forty types tied at the cohort
maximum all rank 1 and clear any top fraction, which is defect 3's eight normalizers arriving by
a different door. Pinned: `A_mass_tie_at_the_maximum_is_not_the_top_of_anything`.

The absolute floor `FanIn >= minFanIn` is unchanged and is what stops a rank test crowning the
tallest member of a cohort where nothing is tall — invariant 1's canonical case.

Pinned: `Blast_radius_is_unreachable_in_a_cohort_below_ten` **stays as written** — it is a
statement about the oracle, which still has the defect. Core's side is
`The_top_rank_limit_never_drops_below_one`,
`A_cohort_of_nine_can_reach_the_top_rank_but_a_tie_for_it_cannot` and
`Rank_is_the_percentile_from_the_other_end`.

**The fix is not observed on the fixture, and that is recorded rather than assumed.** TestBed's
stranded cohorts contain types that now clear the rank gate — `NormalizationContext` at rank 1
of eight, `RawResponse` at rank 2 — and every one of them fails blast radius on complexity
instead. So `Math.Max` can be deleted with the fixture green; only the `Distribution` tests catch
it. A plant is owed, alongside four other blast-radius and load-bearing gates in the same
position: `FixtureCoverageTests.The_new_findings_have_gates_the_fixture_cannot_observe`.

### 15. Breaks-alone's concealed-decision suppression is type-level only

The primary of the two concealed-decision nominations is at method level, and it is the one the
suppression cannot see — so the report says "this method is making business judgements" and "if
it breaks, it breaks alone" about one component. Invariant 3 again, by a different route than
defect 10.

The decision is closed — a method-level concealed decision *does* suppress breaks-alone on its
declaring type, and `SubjectRef` walks member → declaring type to express it. What remains is
the fix in Core.

**Fixed in Core (Aug 2026).** Breaks-alone moved, and row 2 of the suppression matrix is now a
declared rule asking `ContainsAbout` at *both* levels — `Suppression.Rules`,
`breaks-alone-decides-something`. The fixture shows the fix exactly as predicted:
`MethodReconciler` and `TariffReconciler` are nominated at method level, neither at type level,
both told they break alone by the probe and neither by Core. `RoutingDepot` survives in both as
the control.

**This is the findings layer's first deliberate divergence from the oracle.** Every other
finding that has moved agrees with the probe byte-for-byte on the fixture; this one must not, and
the difference is asserted as a set in both directions — Core removes two claims and adds none.
A suppression may silence, never nominate. `Breaks_alone_diverges_from_the_probe_by_exactly_the_defect_fifteen_fix`.

The probe keeps the defect and its pin, `A_method_level_concealed_decision_does_not_suppress_breaks_alone`.
It is the oracle, not the product.

### 16. A god object by size is told it carries real logic — **fixed in Core**

`HUBS AND GOD OBJECTS` splits on `MaxMemberCyclomatic >= highCc` **or** `MemberCount >=
godObjectMembers`, and both branches print the same sentence: *"Architectural bottleneck: it
both depends on and is depended on by much of the system, AND carries real logic."* The
disjunction has two arms and the wording only describes one of them. On the size arm the claim
is false by construction — that arm exists precisely for types with bulk and no logic.

Visible on the fixture the moment a case for the size arm existed:

> `DispatchRegistry` — … Architectural bottleneck: … **AND carries real logic** (23 members,
> worst method `Registered` at **cc 1**, dsm 0).

Twenty-three members and a worst method of cc 1. The receipts in the same sentence refute the
claim the sentence makes, which is the failure invariant 5 exists to prevent — interpretation
first, math as receipts, and here the interpretation contradicts its own receipts.

Not caught earlier because the size arm had never decided anything: `ShipmentCoordinator` is the
only other bottleneck and reaches the branch on complexity, where the sentence is true. This is
the *second* thing the same plant found — the first was that the arm was untested at all.

~~**Fix it in Core when §3.8 ports**~~ — done. `HubOrGodObject` carries the two arms as
independent qualifiers, `carries-real-logic` and `too-large-to-hold`, so a renderer says what each
arm actually means and cannot say the wrong one by accident. The size arm means coupled both ways
and large enough that no one holds it in their head, with nothing complex inside — a different
danger from a bottleneck carrying logic, which is §3.8's whole design and what one sentence
collapsed back together.

The disjunction is still derivable — a bottleneck is either arm holding — so nothing is lost by
splitting it. `The_hub_disjunction_has_two_arms_that_say_different_things` holds one fixture type
per combination: complexity alone, size alone, and neither.

Not pinned, and it stays that way: the sentence is wording, and `docs/TESTING.md` §5 is explicit
that this suite asserts against the model rather than against report prose. The golden holds the
probe's exact string and the probe is unchanged, so the wording moves at R1 and is visible there.

### 17. `NO PEER GROUP` claims an absence that is not true

The section states, in fixed prose on every run: *"No PEER comparison was possible for these. They
are absent from the nominations above."* The first sentence is true and the second is false — by
design rather than by accident. **The cohort-free findings do not consult a cohort**, so a type
with no viable peer group is fully eligible for every one of them; §3.6, §3.7, §3.8 and §3.9 all
carry *"no cohort required"* in their own headings.

Three types appear in both places at once: `RoutingDepot` is told it breaks alone,
`DispatchRegistry` is a hub and a god object, `DispatchCounter` holds shared mutable state. All
three are then listed as having been left out.

Found by porting §3.11, and it is a **wording** defect rather than a gate defect — no nomination
is wrong and the coverage population is right. What is wrong is a sentence telling the reader not
to look for these names above, when the most important thing the tool says about `RoutingDepot` is
above. Invariant 8 is about silence not reading as safety; this is its inverse, a disclosure that
overstates what it disclosed.

**Fix at R1**, when Cli renders from the model: the sentence has to say "absent from the
*cohort-relative* nominations", and the section should name which of its types were nominated
anyway. Core already has what that needs — coverage and the other findings share one `FindingSet`,
so the renderer can ask rather than assert.

Pinned: `The_coverage_section_claims_an_absence_that_is_not_true`.

### 18. The report header is working notes, addressed to the people who built it — **fixed at A0**

Every run, first four lines, in `Report.Header`:

```
================================================================
NOMINATED INSTANCES
Draft sentences. Receipts in parentheses. Rewrite before the session.
================================================================
```

*"Rewrite before the session"* is an instruction to the authors about a validation session that
happened months ago. It shipped through extraction untouched — `golden/nominations.verified.txt`
carries the same three lines, so this is the probe's text inherited rather than R1's mistake, and
the only thing R1 changed was dropping the `(cohorts of >= 5 members)` parenthetical.

It survived because **nothing asserts that output is addressed to a user.** The snapshot suite
holds the header exactly, which pins it as *current* and reads as *intended*; the equivalence
suite compares Core to the probe, and both say it. A line that both implementations agree on is
invisible to every check the repo has.

Fix in `Bearing.Cli`. Not pinned — a pin asserts the probe's behaviour, and the probe is not what
ships. `ReportTests.The_report_renders` moves on the accept workflow; `golden/` does not, because
it is the probe's frozen baseline and the probe is unchanged.

**Fixed.** The header names the build and the solution, the counts, and how to read what follows.
`ReportTests.The_header_names_the_build_and_the_solution` asserts the old strings are **absent** as
well as the new ones present, because text like this returns by being copied out of an old
snapshot. Printing the version immediately turned up §21.

### 19. The cohort sentence discards the field that would make it true — **fixed at A0**

`PRD-free-tier.md` §4 gives the canonical output as *"top 2% of your 56 normalizers"*, and §5
rests the whole anomaly claim on it: the reader cannot say *"normalizers are just complex"* — the
other 55 are not. **The tool has never produced that sentence.** What it renders, from the frozen
golden:

```
... top 6% of internal complexity among your 8 ControllerBase.
... top 10% of internal complexity among your 5 Gauge.
... top 7% of internal complexity among your 7 IResponseNormalizer.
```

A base type, a singular noun, and **an interface name with the `I` prefix intact** — that last one
in the very case the PRD's example was written from. Against a real solution the namespace
fallback is worse, because there is no noun in it at all: running Bearing on its own solution
gives *"among your 63 Bearing."* and *"among your 17 ArchProbe"*.

The mechanism is one method. `FindingSections.ShortCohort` takes the cohort key, strips everything
up to the first `:` — which is `impl:`, `base:`, `suffix:`, `kind:` or `ns:`, **the token that says
what kind of group this is** — and then takes the last dotted segment. `Cohort` carries `Basis`
beside `Key` for exactly this, documented as *"how the group was arrived at, for the report to
explain itself"*, and **no file in `Bearing.Cli` reads it.** The renderer throws away the word that
would make the sentence grammatical, then hides the loss by stripping the prefix that encoded it.

The fix is not pluralisation. Each basis needs its own phrasing — *"the 7 types implementing
`IResponseNormalizer`"*, *"the 8 types deriving from `ControllerBase`"*, *"the 5 types whose names
end in `Gauge`"*, *"the 63 types in `IronMarten.Bearing`"* — because a reader who cannot tell
*which* population the claim is against cannot check it, and checkability is the entire argument
for cohort-relative findings over scores.

Invisible to the equivalence suite by construction: `TESTING.md` §5 asserts against the model
rather than report prose, and both implementations render the same wrong sentence from the same
right model. Fix in `Bearing.Cli`; `ReportTests` re-accepts, `golden/` stays as the probe's record.

**Fixed.** `Sentences.PeerGroup` switches on `Basis` and gives each its own phrase; `ShortCohort`
is gone from both files that had a private copy of it. Verified against real solutions rather than
only the fixture, which is where all five bases actually occur — *"the 249 types deriving from
`BaseNopModel`"*, *"the 31 implementations of `ILocalizedEntity`"*, *"the 86 types whose name ends
in Manager"*, *"the 6 types in `MediaBrowser.Model.MediaInfo`"*.

A second form, `PeerGroupNoun`, carries the same descriptor without a count, because the coverage
list reports **peers** — one fewer than the cohort — and *"the 1 type classified as ApiBoundary"*
reads as though a type with no peers had one.

### 20. `0 external contact point(s)` prints directly above six external systems — **fixed at A0**

The boundary section opens with a count of types classified `ApiBoundary` or `ExternalCall`, then
warns that *"changes at ANY of these is outside what static analysis can see"*, and then the
integration map lists every external system by how many types touch it. On Bearing's own solution
that reads:

```
   0 external contact point(s): 0 inbound API, 0 outbound. Consumer impact of
   changes at ANY of these is outside what static analysis can see.

   INTEGRATION MAP — external systems, by how many types touch them:
     System.IO                                  9 types
     Microsoft.CodeAnalysis                     8 types
     ...
```

Both numbers are right and they measure different things — a contact point is a *type* at the
edge, an integration is a *namespace* reached across it. The defect is that the section states an
absence and then enumerates a presence, four lines apart, with nothing saying they are different
questions. Invariant 4 is about never implying safety at a boundary; a zero that a reader takes
for "no external surface here" is that failure arriving through arithmetic that is individually
correct.

**The fixture cannot show this.** TestBed has 19 contact points, so the disagreeing case is the
zero case, and the zero case is unexercised — which is why it took a run against a solution that
is a library and a CLI and nothing else to surface it. `TESTING.md` §6 should carry the gap.

Related but not the same: whether `Microsoft.CodeAnalysis` should be called an *external system*
alongside Stripe and Azure at all is defect 5's classification question, and fixing the wording
here does not answer it.

**Fixed.** The zero case no longer prints a bare `0`: it names both explanations — a codebase with
no edge of its own, or frameworks this classifier does not recognise — and points at the
integration map as the way to tell which. The map's heading now says it counts what the solution
*calls into*, which is the other half of why the two read as a contradiction. **The fixture still
cannot show it**, so the wording was verified against Bearing analysing itself; `TESTING.md` §6
still owes the row.

### 21. `SolutionModel.ToolVersion` reports the wrong assembly

`<Version>0.0.1-preview.1</Version>` is set on `Bearing.Cli`. The property reads
`ToolInfo.ReadVersion(typeof(SolutionModel).Assembly)` — which is `Bearing.Core`, which sets no
version and therefore reports the SDK default `1.0.0`.

`ToolInfo.ReadVersion` is not at fault and its own remarks explain why it takes an assembly rather
than calling `GetEntryAssembly`: *"so the result is a function of its input."* The caller passes
the wrong input.

**Found by rendering it.** Nothing had ever printed the value, so nothing had ever compared it to
the version on the package. The report header now reads the Cli's assembly and
`ReportTests.The_models_tool_version_reports_the_wrong_assembly` pins the divergence.

Still open, because the *model's* copy is the one that reaches the JSON writer at **A4**, where it
stops being a printed string and becomes a field somebody parses and compares against a release.

**Fixed at A4, by making the version an input rather than a lookup.** `WalkOptions.ToolVersion` is
what the model carries, and the host supplies it — `Program` passes the version it already reads
off its own assembly for the header, so there is now one read where there were two disagreeing
ones. Core cannot find this out for itself: the version lives on whatever packs, `Bearing.Core`
does not, and `Assembly.GetEntryAssembly` is the test runner under a test host, which is the reason
`ToolInfo.ReadVersion` takes an assembly in the first place.

**The default is `ToolInfo.UnknownVersion` — `0.0.0` — and not Core's own.** A host that says
nothing now reports "nobody told me". The old value reported a release that does not exist, in a
field a consumer was about to parse; of the two ways to be wrong, only one of them is visible to
the person reading it. The fixture walks without supplying a version, so that default is what
`ReportTests` pins, and `JsonOutputTests.The_tool_version_is_the_one_the_host_supplies` does a real
second walk with one set — the defect lived in the path from options to model, and a test that
built the model some other way would not cross it.

### 22. Anonymous types are collected as components — **fixed in Core at A2**

`global::<anonymous type: int id>` reached the reference graph as a target and killed nopCommerce
outright (§7). An anonymous type belongs to the compilation, so `_isInSolution` accepts it, and
`ResolveToNamedType` filtered special types and error types but not this.

It is also not a component in any sense the report means: a reader cannot navigate to it, name it
or change it, and the type that projected it is already the subject of every claim worth making.
`ResolveToNamedType` now drops them.

> **Recorded rather than closed, because the class of problem is open.** `_isInSolution` accepts
> anything the compilation owns, and nothing enumerates what else that admits. Lambdas and their
> display classes, iterator and async state machines, and a record's `<Clone>$` are the obvious
> next candidates, and each would arrive exactly the way this one did — as a crash on somebody
> else's codebase, months after the fixture said everything was fine.

### 23. An unreadable solution crashes with a raw MSBuild stack trace

Reproduced against a `.slnx` (§8): `InvalidProjectFileException: No file format header found`,
followed by eleven frames of `Microsoft.Build` and `Microsoft.CodeAnalysis.MSBuild` internals,
printed straight at the user with no message of the tool's own.

**This is not specific to `.slnx`.** `Program.cs` catches `CommandLineException` and nothing that
the walk can throw, so a mistyped path, a permission error, a corrupt solution and an unsupported
format all take the same route. A first-time user's most likely mistake is a wrong path, and the
tool's answer to it is a stack trace.

Cheap to fix and user-visible; not fixed at A2, whose job was to measure.

**Fixed, and it was three causes rather than one.** Pointing at a `.csproj` crashes identically —
the likeliest first-run mistake of the three, and the one the register had not named. All three
arrive from MSBuild as the same sentence, `No file format header found`, so the message cannot
discriminate between them and anything switching on it would be wrong two times in three.

`SolutionWalker.OpenAsync` turns any failure to open the workspace into `SolutionLoadException`,
which `Program` catches and `Failure.CouldNotRead` renders. Two decisions are worth keeping:

- **The catch is narrow in scope, not in type.** It covers one call — open this file as a
  solution — and the whole walk is outside it, so it cannot swallow an analysis bug. Within that
  call the failure types are MSBuild's, from assemblies Core deliberately does not reference;
  listing the ones we happen to have seen is how the next unlisted one reaches a user as a stack
  trace, which is this defect exactly.
- **The `.slnx` advice is chosen after the load fails, never before it.** There is no pre-flight
  extension check, so the day MSBuild parses `.slnx` (§8) the load simply succeeds and the text
  stops being reached. A guard in front of the load would go on refusing a file that had started
  working, and nothing would fail to say so.

Pinned by `SolutionLoadFailureTests`, which asserts the absence of a stack frame as well as the
presence of the new text — the new sentence alone stays green if a later change prints it *and*
lets the exception escape, which is the same eleven frames with a heading.

### 24. A constructor renders as `Type..ctor` — **fixed**

`CustomerInfoValidator..ctor` — from nopCommerce. The member's name *is* `.ctor`, and the sentence
joins type and member with a dot.

Cosmetic, one line, and **only visible on real code**: TestBed declares no constructor complex
enough to be nominated, so no fixture case and no snapshot shows it. Related to §13, which is the
same identity question one level deeper.


**Fixed.** `Sentences.Member` is one rule for both renderers and spells the word out — *constructor*, not *ctor*, because the name is there so a reader can find the thing and `.ctor` is what the runtime calls it. The HTML had its own half-fix producing `CustomerInfoValidator ctor`, which was less wrong and still addressed to the runtime.

**It stopped being cosmetic before it was fixed.** Ranking undefined ratios last moved a pair of constructors to the top of nopCommerce's concealed-decision section, so this was the report's opening line. *Cosmetic* was a judgement about where the sentence sat, not about the sentence.

No fixture case — TestBed declares no constructor complex enough to be nominated, which is why nopCommerce found this and the suite did not. Named as a hole in `FixtureCoverageTests` with a guard beside it that starts working the day a plant fills it.
### 25. A redirected report is transcoded through the process code page

`bearing App.sln > report.txt` on a Windows machine whose code page is not UTF-8 encodes
`Console.Out` through that code page. Every em dash in the report best-fit-maps to an ASCII
hyphen: **247 of them in one nopCommerce run, and not one U+2014 survived**.

**The em dash is not the problem.** Best-fit mapping is silent and lossy for anything the code
page cannot represent, and a character with no mapping at all becomes `?`. A type named with a
non-ASCII identifier — legal C#, and ordinary in a codebase that is not written in English — is
then reported under a name the reader cannot search for. Naming the component is the whole job of
a finding.

**Invisible to the suite by construction, and not because the fixture is synthetic.** Every
snapshot calls `Report.For` and asserts on the strings it returns; nothing in the suite goes
through `Console.Out` at all, so the encoding boundary is not merely untested, it is not on the
path under test. That is a different gap from the ones in `TESTING.md` §6 — those are inputs the
fixture cannot contain, and this is a stage the harness does not execute.

**Fixed at the same time it was found.** `Program.UseUtf8` sets `Console.OutputEncoding` before
anything is written, best-effort: it throws where no console is attached, and a tool that refused
to run because it could not choose an encoding would be worse than one whose dashes are hyphens.
The file writers never depended on it — `JsonOutput` and `CsvOutput` pass their own
`UTF8Encoding(false)`.

Found by reading the *bytes* of a redirected run rather than the run, which is the only way this
shows up: on screen the terminal renders whatever it was given and nothing looks wrong.

---

**26–31 are a third class: defects a reader outside the build made visible.** 1–17 were carried
through the port. 18–25 were made visible by the port, when it produced a report a user could read
— but the reader was still the author. These came from `TASKS.md` A11 round 1, run to the protocol
in the private spec set: two developers unfamiliar with nopCommerce, given the shipped HTML report
and nothing else. **None could have come from the suite, and none came from the author running the
binary either** — every one is about whether a label, a sentence or a table means anything to
someone who does not already know what it is for. They are open, none is pinned, and all six are
`Bearing.Cli`.

### 26. The three facts under a finding's name are unlabelled, and are not the same kind of thing — **fixed**

`<p class="where">Nop.Core · BaseAttribute.cs:9 · the 31 implementations of ILocalizedEntity</p>`

The first two are identity and location. The third is **the population the claim is measured
against** — the cohort, which is the entire basis on which the finding was made and the one thing
on the card that is not an address. Joined by middots, unlabelled, it reads as a third address.

Participants understood the phrase perfectly well and could not tell what job it was doing,
guessing *"project membership, definition location, caller set"* and wanting to open the code to
check. **The phrase is not the defect.** This is not `PRD-free-tier.md` §5 failing to transmit; it
is §5's mechanism arriving with nothing to say it is a mechanism.

`ARCHITECTURE.md` §4 already states the general form, written at A6 about `Participants`:
heterogeneous data rendered by a generic renderer, where one relationship per kind means a renderer
label suffices. **The `where` line has the same disease and was never covered by that rule.** The
remedy is the same, and it is a label rather than a redesign.


**Fixed, and half of it was a correctness fix rather than a labelling one.** The addresses stay joined; the comparison gets its own line and says it is one.

**The half that was not reported**: the peer group printed on *every* card, including the cohort-free findings. §3.6–§3.9 carry *"no cohort required"* in their own headings, so those cards claimed a relative reading their finding never made — §17's mistake in a different element, and on the fixture it is **74 of 107 cards**. The gate is the detector's own gated `CohortSize` receipt rather than a list of kinds kept in the renderer, and the split it produces is right on inspection: change cost names no peer group because X2 made it solution-wide, and coverage does because the peer group is its whole subject. `HtmlReportTests.A_card_names_a_peer_group_only_where_the_finding_used_one`.
### 27. `Why this fired` publishes 65 internal identifiers — **settled**

The receipts table renders `Measured / Value / Had to clear` as raw policy and metric field names.
Across one nopCommerce run that is **65 distinct identifiers** shown to the reader —
`MaxMemberCyclomaticXMedian`, `FanInSolutionRankLimit`, `RollCallDivisor`, `Dsm`, `OutlierFactor`,
sixty more.

`PRD-free-tier.md` §4 forbids exactly this: *"every metric exists to support an interpreted claim a
non-expert can act on. If a number does not end in a sentence someone changes their behaviour over,
it does not ship."* The rule was taken because a validation session found developers did not hold
*stability* as a vocabulary concept; the report duly never prints `instability 0.296`, and then
prints `MaxMemberCyclomaticPctl` in a table.

**It is the designed explanation, which is what makes it worse than an ordinary leak.** Asked what
the pane told them, participants reported understanding **less** after expanding it than before —
*"I have no idea what that is, but it seems like a big number. Is that a problem?"* A disclosure
that reduces comprehension is not a disclosure.


**Settled at A13 tier 3, and the answer is what the pane is *for* rather than what it is called.**
Held deliberately until then, because deciding it twice is how the two decisions disagree.

**The table is a receipt, not an explanation, and the page is now built that way.** Two changes,
neither of which is a translation of the sixty-five:

- **Tier 4 took the enumeration off the default page**, so the pane exists only under `--full`.
  Nobody meets it without having asked for every finding, which is the point at which a reader is
  auditing rather than orienting. That is most of the fix, and it shipped before this entry was
  answered.
- **The summary says what the table is** — *"the receipts behind this claim"*, over a line saying
  the names are the tool's own, kept unchanged so a number can be matched to the threshold table at
  the foot of the page, and that the sentence above is the claim. *"Why this fired"* promised the
  explanation and delivered field names; that promise is the defect, and it is the half a rename
  can fix.

**What was considered and rejected**, all three for the reason `TASKS.md` recorded when it held
this entry: translating 65 identifiers is a vocabulary to maintain and to drift, dropping the table
loses the receipts that let a reader check a claim against the code, and a curated subset is a
standing choice about which gates are explainable. The unchanged names are the join to
`AnalysisPolicy`; renaming them here would break the one thing the pane is good for.

**The reader who wanted an explanation is now served above it** — the annotated card names what
each line of a finding is for, and the per-kind census says what every kind means. Whether that
lands is A11 round 2's question, not this entry's.

### 28. A ratio against a zero median renders as `∞` — **fixed**

`MaxMemberCyclomaticXMedian` is displayed as `∞` when the cohort's median is 0 — a real value in
the receipts table of a real card. The quantity is undefined, not infinite, and the gate it feeds
cannot discriminate in that cohort at all.

**Rare, and it found its own worst case.** 62 of nopCommerce's 163 cohorts have a median
`maxMemberCyclomatic` of 0, holding 46% of types — but only **10 types of 3,209** both clear
`MinDecisionCc` and sit in one, so `∞` reaches a card about three times in a thousand.
`BaseAttribute` is one of the ten and was the first card a participant opened.

**Worth being clear about what this is not.** It was investigated as a possible cause of the 1,091
method-level nominations on that run and **it is not one** — the volume is granularity times scale,
which §3.2's method-level primacy chose on purpose. This is a display defect of narrow scope, and
recording it as anything larger would be wrong.


**Fixed in both formatters: it reads `undefined`.** An infinity glyph in a column of measurements invites a reader to treat it as the largest value there, and it is the absence of one.

**The ranking was wrong for the same reason and that mattered more.** `ConcealedDecision` ordered on the ratio alone, so all ten of nopCommerce's undefined rows sorted above every type whose extremity was measured, and `Nomination`'s tiebreak settled them alphabetically. The section opened on `cc 6` where it now opens on a constructor at 37x its peer median. Undefined ratios rank last and among themselves by absolute complexity. `docs/TESTING.md` §6 records that the fixture cannot reach any of it.
### 29. *"too large for anyone to hold at once"* reads as the tool giving up — **fixed**

Asked directly, on the god-object-by-size arm: *"is that a sign of a problem, or is the report
giving up?"*

The sentence is D16's, and D16's fix was correct — the two arms are independent qualifiers, so a
renderer cannot tell a god object by size that it carries real logic. What survived is a phrase
whose subject is ambiguous: *anyone* can be read as the reader, the author, or the tool. One line.


**Fixed.** It names the shape instead — *broad rather than deep, a lot to hold at once but nothing intricate inside it*. The claim is unchanged and the same receipts back it. §16's test asserts the old phrasing **absent** as well as the new one present, because that is the kind of sentence that returns by being copied from an old snapshot.
### 30. External dependencies do not separate framework from third-party from first-party — **fixed**

`Microsoft.AspNetCore.Mvc`, `System.IO` and a payment SDK are listed alike. Requested unprompted,
and with the reason attached: *"it would help to indicate what the project built versus what is
language or framework provided — I'm not going to change any of those, so I'm not worried about
them."*

That is a reader dividing the list by **what they could act on**, which is the axis the section
does not carry. `ExternalSurface` already makes a judgement here — the plumbing filter — so the
classifier exists and is one distinction short. Related to §5, which is the same classifier being
too narrow in a different direction, and the two want fixing together.


**Fixed — the row says who provides it, read off how the SDK resolved the assembly.** Framework references resolve out of the targeting packs and the shared framework, packages out of the NuGet cache; both are facts about how restore works rather than a list of names to maintain. That matters because names cannot answer it — `System.Text.Json` is in the shared framework on one target and a package on another — and because a curated list is §5's defect, which is the one this entry sits next to.

**Origin does not decide the integration map, and the fixture is why.** The first attempt treated framework-resolved as plumbing and emptied the map: `System.Data` and `System.Net.Http` both resolve from the framework, and both are exactly how TestBed reaches a database and the network. `CyclesAndCouplingTests.The_integration_map_over_the_fixture_is_what_it_should_be` caught it in one run. **The two questions are different** — origin answers *"could somebody change this dependency"*, the filter answers *"does this reach outside the process"* — and the reader asked the first, whose answer is a label on the row rather than a reason to drop it.

Unknown says nothing rather than guessing a third answer, and the name-based filter still applies to those. On nopCommerce: `Newtonsoft.Json` and `FluentValidation` are packages, `System.IO` is framework, and `Microsoft.AspNetCore.Mvc` resolves as a package while `Microsoft.AspNetCore.Http` resolves as framework — a distinction no name list makes.
### 31. A folded diagram box does not read as containing the projects that are missing — **reopened 2026-08-22, fixed again**

*"Why isn't `Nop.Plugin` in the graph above?"* — asked while looking at the Projects list directly
below the diagram, where the plugin names are.

The boxes say *"Omnisend +5"* and *"6 projects, same shape"*. **The fold is the artifact's best
compression** — 27 projects to 10 boxes on nopCommerce, against 1444px unfolded — and to a first
reader it reads as an omission rather than as a container. The caption explains that folding
happened; nothing connects a folded box to the names that are inside it.

The same session found the flat Projects list **outperforming the diagram** at the actual
navigation task: asked where they would change tax calculation, nobody reached it through the map,
and everybody found `Nop.Plugin.Tax` by scrolling the list.


**Fixed — the names go beside the diagram, not into it.** A picture cannot be searched, and a box that grows to fit its members is the thing the fold exists to prevent. `ArchitectureDiagram.Folded` shares `Title` with the boxes, so a legend cannot disagree with the label it explains. On nopCommerce it answers the question that was actually asked: *"CustomerRoles +6 holds Nop.Plugin.DiscountRules.CustomerRoles, …"*, and `Nop.Plugin.Tax` is findable in it.

**Reopened 2026-08-22. The first fix put the names beside the picture instead of in it, and the
evidence it leaned on was confounded.** Its argument was that a picture cannot be searched and that
*"a box that grows to fit its members is the thing the fold exists to prevent"*. The second half is
measurably false of this drawing: `Labels` already caps a name at twenty characters, so a member
line is about 123px inside a 144px box interior. **Naming all 27 of nopCommerce's projects took the
diagram from 580 x 642 to 580 x 841 — the width did not move at all.**

**The confound.** §31 cited round 1's T2 — *"the searchable inventory beat the structural diagram at
the actual navigation task"*, participants finding tax by scrolling the Projects list — as evidence
that readers navigate by list. **Both tax projects were inside folded boxes that named neither**:
`Nop.Plugin.Tax.FixedOrByCountryStateZip` sits in the box labelled `Brevo +5` and
`Nop.Plugin.Tax.Avalara` in `Omnisend +5`. The map was graded on a task whose answer it had hidden,
so T2 does not show a list beating a map; it shows a map that had removed the thing being looked
for. The participants' own question — *"why isn't `Nop.Plugin` in the graph above?"* — was literally
correct, and the first fix answered it by moving the names further out of the picture.

**What a reader is doing with this map.** Orientation, not navigation — but orientation requires
locating yourself in it, and a reader arrives holding a name. **The fold's finding is also
illegible without the members**: *these seven projects are architecturally interchangeable* is a
statement about how the solution is organised, and `+6` delivers it as an omission instead of as
the claim. Naming them turns an apology into a finding.

**Fixed again — the names are in the boxes, and the legend stays as the searchable copy.** Height
grows and width does not, so `MaxPerRow` and the width acceptance criterion are untouched.
`Every_project_in_a_folded_box_is_named_in_the_drawing` asserts against the **drawing** rather than
the legend, because `ArchitectureDiagram.Folded` is `HtmlReport`'s and the standalone `--diagram`
export has no legend at all — which is where hiding a name costs most, §5.4 asking that file to
survive being pasted into Slack.

> **Still open, and small.** `Labels` shortens to the last unique segment, so
> `Nop.Plugin.Tax.Avalara` renders as `Avalara` and does not say *tax*. The reader who knows their
> provider finds it; the reader searching for the category does not, in the SVG. The report's
> legend carries full names, so this is an export-only gap and it is recorded rather than fixed —
> changing the label rule reopens §1's identity argument, which is a bigger question than this.

### 45. A layer wider than the cap is drawn as two rows, and nothing says so

`ArchitectureDiagram.MaxPerRow` is 5; a layer holding more wraps onto a second row. Its own remark
says wrapping *"does misrepresent the layout"*, that *"nothing on the drawing currently says so"*,
and that it *"ships unexercised on real input"*. **The first two are right and the third is
wrong** — it fires on Jellyfin, which is a reference solution and the artifact §5.4 calls the
screenshot.

**Measured from the shipped SVG, not from a model.** Jellyfin's drawing has ten rows for twenty-one
projects, and two adjacent rows of exactly five sit at y = 344 and y = 452 with **zero edges between
them** and identical downstream fan-out — 5, 5, 5 and 8 edges into the four rows below. Every other
adjacent pair of rows on that drawing carries at least one edge.

**That zero is conclusive rather than suggestive.** `ProjectGraph.DepthOf` is a longest path —
`deepest = max(DepthOf(next) + 1)` — so a node at depth *k* always has a neighbour at *k−1*, and two
genuinely adjacent layers must therefore share at least one edge. Two adjacent rows with none are
one layer drawn as two.

**Why it matters more than the ink.** A row means *depends on the row below* everywhere else on the
drawing, so a wrapped layer reads as a dependency that is not in the code — the same class of
misrepresentation as §44's axis, and the opposite of what a layered map is for.

**The remedy is not raising the cap**, which moves the threshold rather than removing the class.
Width-bounded layering does remove it: Coffman–Graham takes a width bound W and guarantees no layer
exceeds it, so overflow moves *down a layer* rather than across a row. Rendered at W = 5 on
Jellyfin's used project graph it gives 952 x 892 against the shipped 952 x 1074 — same width, one
extra layer, no wrap.

> **But it must be conditional, and that is the part with no rule yet.** On nopCommerce the same
> bound places **22 of 27 projects deeper than their dependency depth**, because 22 of them
> genuinely sit at depth 0 under a plugin host — the true profile is `[22, 1, 1, 1, 1, 1]`. There
> width *is* the finding and the fold is the honest compression. **A layer wider than the cap is
> either a layering the algorithm can improve or a fact about the codebase**, and shipping one
> treatment for both is how this drawing came to hide a layer in the first place. Specimens:
> `SPIKE-job-a-prior-art.md` §9.

### 32. A verb agrees with a number a real solution made singular — **fixed**

*"AzurePictureService — 7 writes to static state, and 1 type call into it."* Three sentences, one
mistake, and the register already warned about it.

**A3 met this once and reworded around it**: *"the other 1 are entangled too"* became a phrase with
no verb in it, recorded with the note that *"the next such number is a defect waiting on the right
input"*. It was three of them, all reachable on nopCommerce and none on TestBed:

| Section | Shipped | Should read |
|---|---|---|
| Shared mutable state | `1 type call into it` — **6 of 19 rows** | `1 type calls into it` |
| Change cost | `1 fields/params of surface` — `BaseEntity`, the section's opening row | `1 field/param of surface` |
| Load-bearing | `1 type depend on it` | `1 type depends on it` |

**Breaks alone got it right**, inline: `{(type.FanIn == 1 ? "depends" : "depend")}`. That is what
makes this a missing helper rather than missing care — the same author wrote both, and the one that
was correct is the one where the singular case had already been seen.

**The remedy A3 chose does not scale, and that is the more useful half of this entry.** *Do not
write a verb after a computed number* is a rule nobody can follow while writing the sentences this
report is made of; `PRD-free-tier.md` §4 asks for sentences, not for phrases, and a report that
avoids verbs to stay safe is a report that has stopped making claims.


**Fixed — `Sentences.Do` and `Sentences.Surface`**, beside `Plural`, which is where a reader
writing the next sentence will find them. Found while extracting the wording into `Claims` for A13
tier 2: reading eleven sentences side by side is what made three of them visibly the same mistake,
where each had looked fine in its own section. **The fixture cannot show any of it** — TestBed has
no shared-mutable-state type with one caller and no contract with a one-field surface — so this
ships asserted against a real run and named here as a hole, the way §24 was.

### 33. A boundary finding fires on a third of the boundaries it filters — **fixed**

`BoundaryMarking.CarryingRealLogic` gates on `maxMemberCyclomatic >= HighCc (10)`, absolute, with
no cohort and no distribution behind it. Measured 2026-08-18: it fires on **19.5% of nopCommerce's
672 boundaries and 33.3% of jellyfin's 174**.

**A gate that names a third of the population it filters is describing that population rather than
finding an anomaly in it.** The counts themselves are printable — 131 and 58 — so this is not the
volume defect that 1,091 concealed decisions were. It is a selectivity defect, and it is worse for
being invisible: a reader has no way to know that *"boundaries carrying real logic"* means *"a
third of the boundaries"* here and a fifth somewhere else.

The same constant also means different things per solution. The median `maxMemberCyclomatic` among
boundaries is **2 on nopCommerce and 5 on jellyfin**, so `HighCc = 10` is five times the median in
one and twice it in the other, and nothing in the output says so.

**The rule this breaks is written twelve lines above it in its own file.** The remarks on
`ExternalSurface`'s sibling finding, at `BoundaryMarking.cs:73-76`, state that the section prints
only when it discriminates and suppresses when the qualifying set exceeds half the boundaries. That
principle was articulated for one finding and not applied to `CarryingRealLogic` immediately above
it; at 33.3% this gate is two thirds of the way to its sibling's stated absurdity threshold.

The fix is the shape defect 2 wants everywhere: gate on rank within the boundary population rather
than on an absolute constant. Rank forms measured at the same time — top 5% admits 34 on
nopCommerce and 9 on jellyfin; top 10% admits 68 and 17. The private `MEASURE-concealed-decision.md`
§10 carries the sensitivity tables.


**Fixed: it also requires rank within the boundary population.** `BoundaryTopFraction = 0.05` — the
same reading `BlastTopFraction` and `ChangeCostTopFraction` take, because the population here is the
boundaries and a proportion of them is the claim. Measured: **131 → 34 on nopCommerce (5.1% of its
672 boundaries), 58 → 9 on jellyfin**, both what §10's sensitivity table predicted.

**The floor stays, and it is what lets this find nothing.** Rank on its own nominates the top 5% of
boundaries however tame they all are, which is `ARCHITECTURE.md` §9's gate that cannot fail. Both
conditions are gated receipts, so a reader can see which one a boundary cleared. The claim also
gained an evidence line — it had none, which was invisible until the rank gate made this the
fixture's rarest kind and the lead card rendered with an empty numbers row.

**The sweep reports no movement at one notch and that is the fixture, not the gate.** Fifteen
boundaries, where 1% cannot move a rank; `FindingTests.The_boundary_rank_is_reachable_from_both_sides`
is the control that reaches both branches.

### 34. A cohort of 2,909 is not a peer group — **fixed, and the diagnosis moved**

Cohorts are assigned by name suffix, base type, namespace or kind, and at the top end they swallow
the solution. `suffix:Service` holds **2,909 of nopCommerce's 9,219 method-like members — 53% of
everything analysed**; `suffix:Factory` holds 1,307; jellyfin's `suffix:Manager` holds 1,284. Three
cohorts of 500 or more produced 58% of all concealed-decision nominations before the rank gate
landed.

**The claim is arithmetically true and rhetorically false.** *"93× the median complexity of its
2,909 peers"* presents a global ranking as a peer comparison. The whole argument for
percentile-within-cohort is that it makes heterogeneous components comparable by asking a local
question; a cohort that is half the codebase has stopped asking one.

**The rank gate limited the damage without fixing this.** `ConcealedTopRank` admits 2 methods from
`suffix:Service` where a proportional 5% limit would admit 142, so the *count* is now survivable
while the *claim* is unchanged. That is worth stating plainly because the volume symptom is gone
and the correctness defect is not.

This is `ARCHITECTURE.md` §11's thresholds-global-versus-calibrated question arriving at the cohort
end rather than the threshold end, so it belongs with the decision recorded as X3 in the private
board rather than being repaired locally. Candidate remedies, none measured: a cohort-size ceiling
with fallback to a finer basis, or refusing a bare name-suffix basis when the suffix covers more
than some share of the solution.

**Measured 2026-08-21, and the second remedy is contradicted.** nopCommerce's two largest cohorts
are base-type, not name-suffix: `base:BaseNopModel` at 249 and `base:BaseNopEntityModel` at 178,
with `suffix:Service` (193) only third. Refusing a bare suffix basis would leave the worst one
standing, so a ceiling belongs on candidate count whatever the basis is. The scale of the problem
also differs by solution — 42% of nopCommerce's types sit in cohorts of 100+ against 6.6% of
Jellyfin's — which is why one solution would have made this look like a nopCommerce quirk.

### 40. The mosaic outlines twelve cells and calls them the eleven claims above — **fixed**

`Mosaic`'s caption:

```
Some finding is about 473 of them, which is the tint; the 12 outlined in red are the
claims above, in the same order.
```

`START HERE` carries **eleven** claims. The twelfth outline is the `Coverage` exemplar — a type the
report deliberately makes no claim about.

**Two selectors disagree, and each is right on its own terms.** `Mosaic.Marks` fills the leading set
from `Selection.Exemplars`, which groups `findings.All` by kind and takes one per group; `Coverage`
is a kind, so it contributes an exemplar. `Highlights` excludes `Coverage` from the claim list on
purpose, and says why: *"It is a disclosure rather than a claim."* Neither is wrong. **The caption
is**, because it asserts a correspondence between two sets built by different rules.

**It has been wrong in every cut that has this mosaic**: v4, v5, v6 and v7 each say `11 outlined`
against 10 claims, and v8 says 12 against 11. The constant off-by-one is why the ratio never looked
suspicious — the caption's number always exceeded the claim count by exactly one, and nothing on the
page puts the two figures side by side.

**Live in the A11 round 2 artifact.** `a13-materials/nop-v8.html` carries it, T9 asks a participant
what the mosaic is for, and the caption is what answers. A participant who counts is being told the
report claims something it does not.

**The fix was a choice rather than a correction, and both halves of it were taken.** The leading
set drops `Coverage` — `Mosaic.Marks` filters `Selection.Exemplars` through
`Claims.IsRiskClaim`, the same predicate `Highlights` uses, so the picture outlines the list the
page prints. And the caption stops asserting a correspondence it cannot guarantee: it now reads
*"the types the claims above are about"*, which stays true the day two claims land on one type and
the cell count drops below the claim count. Keeping the disclosure on the picture was the rejected
option — a mark whose caption has to explain that one of them is not a claim is a worse picture
than one that does not draw it.

**Gated by `MosaicTests.The_outlined_cells_are_exactly_the_claims_the_page_leads_with`**, which
asserts the outline count against the claims and first requires the fixture to produce a disclosure
exemplar the claims do not also name — without that line it would pass on a fixture where the
question cannot arise. Control: restoring the unfiltered `Exemplars` call fails it, and three
others.

### 41. The `Clean` tile counts a disclosure as a finding — **fixed**

*"85% — of 3,209 types, no finding names them."* The 15% is `Subjects.Named`, which walks
`findings.All`, and `findings.All` includes the 107 `Coverage` entries. **The tile is treating
"nothing comparable to compare this against" as a finding that names the type.**

**The same page contradicts it.** The census says of exactly those types: *"That is not a finding
about those types — it is a record that the tool stayed quiet about them."*

**On nopCommerce it is worth three points.** 3,209 − 473 named = 2,736 clean, 85.3%. With the
disclosure out, **100 types leave the named population** and the tile reads **2,836 clean of 3,209,
88.4%** — rendered as 88%. The mosaic's tint moves with it, those types being in the `n` path, and
so does the plot caption's *"85% of this codebase has nothing said about it"*, which is the same
figure rendered a second time.

> **This entry first said 104 types and 88.5%, both derived rather than measured, and the re-cut
> corrected them.** The arithmetic used the `NO PEER GROUP` section's own line — *"3 of them do
> still appear in the nominations above"* — as the overlap between the disclosed types and the
> claimed ones. That line is computed from `findings.About(f.Subject)`, an exact-subject question,
> so it counts a type whose *own* row carries another claim and not one whose **member** does. Four
> more of the 107 are named by a member-level finding and stay tinted, which makes the overlap 7
> rather than 3. **The error was small and in the direction that flatters the fix**, which is the
> direction to be most suspicious of; it survived one review and did not survive the run.

**Same root as 40 and a different consequence**, so they are two entries rather than one: 40 is a
caption that miscounts a mark set, this is a metric that includes a population the tool declined to
judge. A fix that excludes `Coverage` from `Subjects.Named` addresses this one and leaves 40
standing.

**Which way it should go was not obvious, and the census decided it.** A type with no peer group
*is* a type the report has a row about, so an argument existed for changing the tile's wording
instead. What settled it is that the page already says which reading is right, in the section that
owns the disclosure: *"that is not a finding about those types."* Two places disagreeing is the
defect; the one that states its reasoning is the one that survives.

**Fixed at the single derivation rather than in four renderers.** `Subjects.Named` now counts
claims — `Claims.IsRiskClaim(finding.Kind)` — which is what its own docstring already argued for:
one derivation, because two of them disagree silently. Repairing the tile alone would have left the
mosaic's tint, the plot's y-axis and `Foundations`' share carrying the old population, and the fifth
consumer to be written wrong later.

**What moved on the fixture**, which is the behaviour change the golden records: clean 64% → 71%,
and the concentration tile from `Data` 2.78x to `Core` 1.04x — the excess a project carries is now
an excess of claims, and `Data` was winning it partly on disclosures.

**Gated by `TilesTests.Clean_does_not_count_a_type_the_run_declined_to_judge`**, which recomputes
the share from the claims rather than reading it back off the mosaic. The test that existed held the
tile against `Mosaic.Marked` and both against `Subjects.Named`, which is the right shape and cannot
catch this: when the shared derivation is wrong the renderers agree on the wrong number and nothing
fails. Control: reverting the filter fails the new test and leaves the old one passing, which was
run and is the point of the entry.

## How these were found

Worth recording, because the methods generalise and the defects do not.

**Planting a case corrects the requirement more often than it confirms it** — three times out of
four, twice over, across the fixture plants and the suppression matrix. Making a finding fire is
how you learn whether its threshold is real. Defects 10, 11 and 12 were all found by trying to
make a suppression row fire, not by reading the code.

**Two of those were unreachable by arithmetic rather than by tuning**, which no amount of running
the tool would have revealed. That is the origin of the review question *"can this fire at all?"*
in `TESTING.md` §8.

**Using a value is how you learn whether it means what it is named.** Defect 13 was found by
reaching for `MethodMetrics.Id` as a sort key — not by the audit that was looking for exactly
this class of problem, which had passed over it because nothing read it.

**Suppressions interact through the population, not just through the code.** One suppression's
test plant had to be built from existing boundary types, because adding a new one would have
moved `boundaries.Count` and disarmed defect 12's row before it could be examined.

**Running the tool on a solution that is not the fixture found three defects in one pass.**
Defects 18, 19 and 20 all came from `bearing ./Bearing.sln` — 89 types, 4.5s — and none of them
could have come from the suite. Two were agreed on by both implementations, which makes them
invisible to the equivalence check, and pinned by a snapshot, which records them as intended. The
third needs a population the fixture does not have: TestBed has 19 contact points, so the case
where the count is zero and the integration map is not has never rendered. **A fixture answers
the questions it was built to answer.** The complement — *is this output addressed to a user, and
does it read as one thing* — has no test and probably cannot have one, which is an argument for
running the shipped binary on real solutions as a scheduled activity rather than as a
demonstration.

**That complement does have a test. It is just not an automated one, and it found six defects in
one session.** Defects 26–31 came from handing the report to two developers who had never seen the
codebase and had not built the tool, and asking them to answer questions about it — `TASKS.md` A11,
protocol in the private spec set. Three things about the method are worth keeping:

**The author is the wrong reader, and knows too much to stop being one.** Defects 18–20 were the
best the author's own eyes could do, and they were real. 26–31 sat in the same output afterwards
and were invisible from inside, because every one of them is a question about what a label means to
someone who does not know the model behind it. There is no amount of careful re-reading that
recovers not knowing something.

**Ask for a task, never for an opinion.** Every one of these came out of someone trying to answer a
question and failing, or asking what a thing was *for* while trying to use it. None came from
asking whether the report was good; that question was never put, on purpose, and would have
produced politeness instead.

**A pre-registered bar is what makes a bad result usable.** The session's headline outcome — nobody
could answer the acceptance question in `TECHREQ-job-a.md` §5.5 — was graded against a rule written
down before anyone was booked, including what each outcome would mean for the work. Without that
the temptation to re-read the result as *"they needed more time"* is close to irresistible, and the
same session yields nothing.


**Fixed — and measuring it moved the diagnosis, which is the part worth keeping.** This entry said
the cohort was too big to be a peer group. It is not what breaks the claim:

| cohort | methods | median | max | ratio |
|---|---:|---:|---:|---:|
| `suffix:Service` | 2,909 | **1** | 93 | 93x |
| `suffix:Factory` | 1,307 | **1** | 46 | 46x |
| `base:…BaseAdminController` | 1,022 | 3 | 60 | **20x** |

**58 of nopCommerce's 70 usable cohorts have a method median of 1 or 0**, so the ratio is the
subject's own complexity divided by one — and the largest cohort, the one with a real median,
produces the *smallest* ratio. Size was never the mechanism; a median on the floor was.

**So the sentence says what the gate measured.** `b5cc69a` made this a rank gate and the wording
kept leading with the ratio, which is §28's mistake one level up. It reads *"the most complex of the
2,909 methods in the 193 types whose name ends in Service"* now, and the evidence line states
*"cc 93 against a peer median of 1"* rather than their ratio — so a median of 1 is visible instead
of hidden inside a multiplication.

**A units error went with it.** The old sentence read *"its 2,909 peers"* off `CohortSize`, which
counts **methods**; the group is 193 types. Both numbers are stated now.

**Type-level concealed decision keeps the ratio wording deliberately**: its gate is still the
multiple, so leading on it is honest there. The two sentences differ because the two gates do.

**What this does not close is `X3`.** Whether a 193-type cohort is the right population at all is
`ARCHITECTURE.md` §11's calibration question, and it is untouched: this entry was about the claim,
and the claim is now true.

### 35. Three inline SVGs share one stylesheet — **fixed**

The report inlines three drawings — `ReachPlot`, `Mosaic` and `ArchitectureDiagram` —
and an SVG `<style>` block inside an HTML document is **not scoped to that SVG**. Every rule is
page-wide, and the last block wins.

**Found while building the plot**: its label class was `nm`, which is also the project map's, so the
two silently restyled each other's text — 13px against 14px, which is exactly the kind of difference
nobody reports as a bug. The plot's rules are all scoped to `.rp` now.

**~~The other two still collide.~~ They never did, and this entry was wrong about it.** It claimed
`Mosaic` and `ArchitectureDiagram` *"both define `.bg`, `.ti` and `.lg`"*. Checked against the
source and against the history before the fix: the diagram defines `.bx`, `.nm`, `.sm` and `.ed`
and nothing else, and has never defined any of the three. The mosaic defines `.bg`, `.bl`, `.c`,
`.n`, `.f`, `.pn`, `.ti`, `.lg`. **Once the plot was scoped there was no overlap left between any
two drawings**, and the only near-miss against the page's own stylesheet is `.n`, which is
harmless: the page rule is `td.n,th.n` and a `fill` does nothing to a table cell.

**Closed 2026-08-20 as a latent hazard rather than a live fault**, and worth closing as one: every
class here is one or two characters, the page and the drawings are written in different files by
different code, and the next drawing would have had to rediscover the rule. `Mosaic` is `.mo`,
`ArchitectureDiagram` is `.ad`, and every rule carries its prefix. No drawn element moved — the
four snapshots that changed differ only in the root attribute and the selectors.

**The test is what makes the next one safe.** The failure mode is a *rendering* one and the suite
could not see it: each drawing stays well-formed, keeps every element, and looks right standalone
with `--mosaic` or `--diagram`. The only symptom is on the composed page, which is where
`HtmlReportTests.Each_inlined_drawing_scopes_its_own_stylesheet` asserts it — every inlined `<svg>`
carrying a stylesheet must carry a class on its root, and every selector in it must begin with
that class. Verified by unscoping one mosaic rule, which fails it by name.

### 36. The plot's y-axis title overlaps its own subtitle — **fixed**

`ReachPlot` writes two texts into the strip above the plot area and places them independently:

- the subtitle, at `x = Left (96), y = 46` — *"One dot per project, sized by how many types it
  declares. Bearing 0.0.1-preview.1"*
- the y-axis title, at `x = Left - 78 (18), y = Top - 14 (50)` — *"↑ how much of it a finding
  names"*

Four pixels of baseline separation and an axis title about 200px wide starting 78px to the left of
a subtitle that starts at 96. **They collide from x ≈ 96 to x ≈ 216 on every run**, whatever the
solution — it is fixed geometry, not data-dependent.

**The lesson is the part worth keeping.** This file's labels *are* collision-checked: `Labels`
tries four offsets per project and drops a name that fits nowhere, and `Unlabelled` discloses it.
That discipline was applied to the data and not to the furniture, which was placed by hand at
constants that looked right in isolation. **The suite cannot see it** — the SVG is well-formed,
every element is present, and no assertion in `ReachPlotTests` is about where two pieces of chrome
sit relative to each other.

**Fixed 2026-08-20 by rotating it up its own axis**, which is the second of the two remedies
recorded here and the better one: it removes the collision by construction rather than by re-tuning
the constant that caused it, and a y-axis title running up its axis is where a reader looks for it.
The arrow rotates with the text — a right-pointing arrow turned -90° points up, and it sits at the
end of the string, which after rotation is the top of the axis — so both axis titles still state
their own direction.

**The measurement in this entry was slightly optimistic.** The collision runs from x = 96 to
**x = 233**, not 216: the title is 32 characters at 12px, and the estimate here used a narrower
one.

**It has a test now, and that is the part that outlives the fix.**
`ReachPlotTests.No_two_pieces_of_header_furniture_overlap` estimates a box for every unrotated
`<text>` in the strip above the plot area, using the same width constant the layout itself uses,
and fails if any two intersect. Verified by putting the old geometry back: it fails naming both
texts and their exact spans. **The lesson this entry recorded — that the collision discipline was
applied to the data and not to the furniture — is now enforced rather than written down.**


### 37. The JSON export's `projects` array is positioned by solution declaration order — **fixed at R2**

`ModelBuilder.Build` canonicalised two of the three collections it returns. Types were sorted by
`Subject.Canonical`, edges by `From` then `To`, and **projects were passed through in the order
the workspace handed them back** — which is the order `.sln` declares them in. `JsonOutput.Projects`
enumerates `model.Projects` directly, so reversing four lines of a solution file, an edit with no
semantic content, reordered the `projects` array in `bearing.json`.

**Nothing was wrong with the data and that is why it survived.** Every project carried its own
metrics with it; only the rows moved. A consumer diffing two runs of the same solution would see
the array change and have no way to tell an edit from a reorder.

**One renderer out of three was affected, which is the part worth keeping.** The terminal report
and the HTML report both sort projects for themselves, and `types.csv`, `edges.csv` and
`members.csv` inherit the model's own canonical order. So the tool looked stable from every angle
anyone had checked from: the goldens reproduced, the snapshots reproduced, and the one export that
read the order the model never promised was the newest one. **A guarantee that two of three
consumers re-implement is not a guarantee**, and `SolutionModel.Projects` said "every project
analysed" where its neighbours said "ordered by identity" and "ordered by endpoint".

**Found by porting `OrderingTests` off the probe**, and it is the reason that port is worth more
than the test it replaces. The old test shuffled the probe's model in memory and re-rendered,
which can only perturb what the renderer is handed. The new one reverses the project declarations
in `TestBed.sln` and walks it a second time, which perturbs the load — and that is the exact
perturbation the old test's own remarks described as the thing that had moved 98.5% of `edges.csv`
during phase 0, written down and never automated.

**Fixed in `ModelBuilder.Build`** rather than in `JsonOutput`, beside the sorts it sits between:
one guarantee on the model is worth three correct renderers, and the next export would have had
the same coin-flip. `SolutionModel.Projects` now says "ordered by name". The JSON snapshot moved
`Data` above `Tools` and nothing else changed.

### 38. `undefinedx its peer median` — **fixed**

Blast radius' sentence interpolated the fan-in multiple and followed it with a literal `x`:

```
BaseController — 89 distinct callers (undefinedx its peer median) and internally complex.
```

**`Sentences.Number` is not the bug and was already right.** Defect 28 made it render an infinite
ratio as the word *undefined* rather than as `∞` or a collapsed large number, because a ratio
against a zero median is undefined and a renderer that prints `999x` turns that into what reads as
a measurement. The bug is a **call site that appends an `x` to whatever comes back**.

**The same file already had the branch, eighty lines up.** `Claims.ConcealedDecision` reads:

```csharp
var basis = double.IsInfinity(times)
    ? "the only complexity among "
    : $"{Sentences.Number(times)}x the median internal complexity of ";
```

Blast radius never got one. So this is defect 28 finished rather than a new discovery: the
formatter was fixed, one of its two consumers was fixed, and the other was not.

**It was live on a real solution and in frozen study materials.** `BaseController` is one of
nopCommerce's most depended-on types, and the line above is in `a13-materials/nop-v6.txt` — the
A11 round 2 artifact, before it was regenerated as v7. It is exactly the class of presentation
defect round 2 exists to judge, so a participant meeting it would have produced a finding already
known.

**Fixed by branching, and the replacement states what is true and no more**: `(its peer median is
zero)`. The typical peer has no callers at all, so no multiple of it exists — a weaker claim than a
ratio rather than a stronger one, which is the choice §3.2 makes for the same reason.

**Found by building a fixture plant that was then discarded.** P5's case gave a cohort a zero
fan-in median, which surfaced this immediately; the plant itself turned out to be unnecessary and
was thrown away, and the defect it exposed is the thing that was worth the trip.

**Tested on constructed findings**, `ClaimsTests.An_undefined_ratio_never_renders_as_a_multiple`,
over both sentences that take a multiple. The fixture cannot reach the case and building a plant
for it would be a large fixture change to protect a small branch. A synthetic finding cannot prove
a detector produces such a value; it proves the renderer survives one, which is the half that was
broken. Verified by control: removing the branch fails the blast-radius row and leaves the
concealed-decision row passing.

### 39. A member subject is a display string, and four kinds of member are not identified by it — **fixed by X14**

**Found while planning A9, which is the point at which it starts to matter.** X5 put dead code at
member level, so a member subject stops being an internal key and becomes the thing a claim about
deletion is attached to. Four separate failures land on `ModelBuilder.MemberSubject`, and they have
one root: **the subject is `ISymbol.ToDisplayString(MemberFormat)`, and a display string is not an
identity.** This is defect 13 one level up — that one was closed by construction because Core keyed
a member as `(assembly, declaring type, signature)`, and this is the discovery that Core's
*signature* is not sufficiently qualified either.

**Measured on both reference solutions**, from `members.csv` as it ships today.

| | nopCommerce | Jellyfin |
|---|---|---|
| members | 25,165 | 12,086 |
| **blank `Accessibility`** | **4,638 (18.4%)** | **1,764 (14.6%)** |
| colliding member ids | 7 | 17 |
| members lost to a collision | 7 | 41 |

**a. A field or an event field has no symbol at all.** `SemanticModel.GetDeclaredSymbol` returns
`null` for `FieldDeclarationSyntax` and `EventFieldDeclarationSyntax` — the declaration is the
`VariableDeclaratorSyntax` under it. `MemberSubject` falls back to `MemberName(member)`, and three
things go wrong at once. **Accessibility is blank on every field**, which is the 18.4% above.
**`AccumulateSurface` and `PublicMemberCount` skip them**, so a public field contributes nothing to
the contract surface `WIDEST CONTRACT SURFACE` reads. And the id is a bare name where a method's is
a qualified signature — `…CacheKeyManager|_keys` beside
`…CacheKeyManager|global::Nop.Core.Caching.CacheKeyManager.AddKey(string)`.

**b. An event field's id is the literal string `EventFieldDeclaration`.** `MemberName` has no arm
for it, so it falls through to `member.Kind().ToString()`. Every event in a type therefore shares
one subject: **15 colliding subjects on Jellyfin covering all 81 of its events**, three of them
merging three events into one. nopCommerce declares no events and shows none of this, which is why
two solutions are the standard.

**c. `ref`, `out` and `in` are not in the signature.** `MemberFormat` sets
`SymbolDisplayParameterOptions.IncludeType` and nothing else, so
`NormalizePath(this string?, out char)` and `NormalizePath(this string?, char)` are both
`NormalizePath(string, char)` — real, in `Emby.Server.Implementations.Library.PathExtensions`, and
they are different members with different callers.

**d. A static constructor is indistinguishable from an instance one**, and an explicit interface
implementation from the ordinary member of the same name. `Nop.Core.Infrastructure.WebAppTypeFinder`
declares both constructors and they render identically as `WebAppTypeFinder.WebAppTypeFinder()`;
`MediaBrowser.Common.Plugins.BasePlugin<TConfigurationType>.Configuration` is the second shape.

**Why it is a defect now and was not before.** Nothing user-facing joins on a member subject yet —
`members.csv` publishes the id, and `CsvOutputTests.A_member_id_is_an_identifier_and_not_a_bare_name`
asserts uniqueness over a fixture that does not contain any of these four cases. **A9 is the
consumer that makes it dangerous**: a claim that a member has no static references, keyed on a
subject that merges three events or two overloads, is a "safe to delete" about something with
callers. That is invariant 4, and it is the specific burn `TECHREQ-job-a.md` §5.6 exists to prevent.

**The remedy was a decision rather than a patch**, because every member id in every export moves and
`JsonOutput.SchemaVersion` has to move with it. **X14 was taken on this evidence: a member is
identified by its documentation comment ID**, which separates all four cases by construction because
it is the form the compiler emits for a cross-assembly reference. Repairing the display format —
add `IncludeParamsRefOut`, resolve fields through their declarator, special-case `.cctor`, qualify
explicit implementations — was rejected for the reason §5 and §30 both give: it is a curated list of
the four cases somebody happened to measure, and the fifth is sorted wrong silently.

**Two things travelled with the fix.** A field declaration now yields one member per variable, so
`int a, b;` is two members rather than one named `a`. And a public field is charged to the declaring
type's contract surface — `ShapeBreadth` had always counted one when measuring somebody *else's*
type, so the model held both answers at once, and no fixture type had a public field to say so.

**None of it was observable until the fixture could reach it.** The suite was byte-identical with
the fix and without it, because TestBed declared none of the six shapes.
`tests/TestBed/Core/MemberIdentityTraps.cs` is the plant and `MemberIdentityTests` is the eight
assertions; the control is reverting X14 and watching all eight fail, which is what was done. The
plant's contribution and what it did not disturb are in `docs/TESTING.md` §6.

`members.csv` and the JSON now publish a readable `Signature` beside the id, and `SchemaVersion` is
`2.0` — a key changing value is a major, where a field being added is not.

### 42. A file Roslyn cannot parse is walked anyway, and the report says it read everything — **fixed**

The walk reacts only when `GetCompilationAsync` returns null. A file whose *syntax* fails to parse
still produces a compilation — with errors — so `CompileAsync` records nothing, the walkers run
over Roslyn's error-recovery tree, and `WHAT WAS NOT ANALYSED` prints **"Every project selected for
analysis produced a compilation."** That sentence is a false assurance, and it is the defect.

**What the recovery tree actually costs, reproduced.** A two-file project, one file using C# 14
extension members against the pinned Roslyn 4.12, `LangVersion=preview`, the other ordinary:

- `NeighbourInSameFile` — declared under `namespace Lib;` in the broken file — was collected as
  `global::NeighbourInSameFile`. The parse error ended the file-scoped namespace early, so the type
  is attributed to the global namespace. **Its `SubjectRef` is therefore wrong**, which means every
  namespace-level claim misplaces it and a `--baseline` comparison reads it as one type deleted and
  a different one added.
- Its `public IPlainService? Service { get; set; }` produced **no edge at all**. Fan-in on
  `IPlainService` is understated by one, silently.
- The type count was 4, which is correct. Nothing about the output looks wrong.

**The blast radius is the file, not the project.** A third, well-formed file in the same project
was walked correctly and both its `Field` edges came through. So the realistic shape of this is a
handful of files in a large solution, not a failed run — which is what makes it survivable, and
also what makes it invisible.

**Current exposure is zero, and that is why this is recorded rather than rushed.** Instrumenting
`CompileAsync` to count syntax-error files across both cloned solutions: nopCommerce **0 of 3,683**
in 27 projects, Jellyfin **0 of 1,653** in 21 projects, with `Load diagnostics: none` on both, so
the denominators are honest. Both are `net8.0`-era, which is the C# the pinned Roslyn handles — the
measurement shows we are not broken now, not that we cannot be.

**Two fixes, and only the second is the point.** Reporting the count belongs in `WHAT WAS NOT
ANALYSED`, but it is a tripwire rather than a feature: on healthy code it prints nothing, and its
job is that *we* find out rather than a customer does. The fix that matters is **not walking a tree
that has syntax errors at all** — a missing type is an honest gap, a type under the wrong namespace
is wrong data, and drift comparison cannot tell the second from a real change.

**Not an argument for chasing Roslyn versions.** Any version has a language ceiling and C# 15 will
exist; upgrading moves the cliff rather than removing it. What removes the class is the tool
knowing when it could not read something. See `TASKS.md` Track D.


**Fixed 2026-08-22, in `CompileAsync`.** A syntax tree carrying an error diagnostic is removed from
the compilation before anything walks it, and the file is named in `Coverage.UnreadableFiles`. The
refusal is the fix and the disclosure is the tripwire: **a missing type is an honest gap, a type
under the wrong namespace is wrong data.**

**Syntax only, and that is the line the fix must not cross.** Semantic errors are the ordinary
condition of a project whose packages did not restore — an unresolved type is `CS0246` and parses
perfectly — so refusing on those would refuse most of the real world for a problem none of them
has. Only a syntax error can put a type under the wrong namespace, because only a syntax error can
eat the namespace declaration. `A_semantic_error_does_not_refuse_the_file` pins that half.

**Driven through a real walk rather than a hand-built `Coverage`.** The claim is about what Roslyn
does with a tree it could not parse, so a test supplying the answer proves nothing. The fixture
writes a good file and a torn one into one project: the good type survives, the torn one is absent,
and only the torn file is named.

**Re-measured on three solutions after the change, and the exposure is still zero** — nopCommerce
at the 2024 commit (3,209 types), Jellyfin (1,545) and **current nopCommerce (3,802)**, which is the
`net10` era one and the likeliest to use C# the pinned Roslyn cannot read. So this ships as
insurance, and the report on healthy code is unchanged.

> **It says nothing when nothing failed, which is a deliberate departure from this section's own
> habit.** Invariant 8 — silence is not a clean bill of health — is why the section states *"every
> project selected for analysis produced a compilation"* even when it did. The difference is that a
> project failing to load is something a reader might reasonably suspect, where *"the parser
> accepted your C#"* is a sentence nobody needs on a report round 1 already called a wall of text.
> `A_run_with_nothing_unreadable_says_nothing_about_parsing` pins the choice so the next reader
> knows it was one.

### 43. A solution needing a newer SDK is reported as an unreadable file — **fixed**

Running against current nopCommerce, whose `global.json` pins `10.0.100`, on a machine carrying
only 8.0.423:

```
Could not read the solution: .../nopCurrent/src/NopCommerce.sln
  An exception of type System.InvalidOperationException was thrown: Failed to find all versions
  of .NET Core MSBuild. Call to hostfxr_resolve_sdk2.

Bearing needs a .sln file. Check the path names the solution rather than a
project or a directory, and that the file is complete and readable.
```

**The file is perfect.** It is a well-formed `.sln` naming 38 projects that build for anyone with
the right toolchain. The advice sends the user to inspect the one thing that is not wrong, and the
sentence it lands on is the fallback arm — the one written for a mistyped path.

**This is the likeliest first-run failure there is**, and more common than the three §23 named. A
`.csproj` or a `.slnx` is a mistake a user makes once; a `global.json` pinning an SDK newer than the
machine's is the normal condition of any shop that is not on the newest toolchain, and of every CI
image that has not been updated. It is also not a user error at all — it is a missing prerequisite,
and the remedy is an install rather than an edit.

**§23's design is right and this is inside it, not against it.** The advice is chosen after the load
fails, never before, and that stays. What §23 established is that the *three* causes it handled all
arrive as one MSBuild sentence — `No file format header found` — so the message cannot discriminate
and the path must. **This cause does not share that message.** `Failed to find all versions of .NET
Core MSBuild` and `hostfxr_resolve_sdk2` name it exactly, so it is separable by the thing §23 could
not use, and reading it here does not reopen what §23 closed.

**What the sentence should say** is that the solution asks for an SDK this machine does not have,
which `global.json` is asking for it, and that installing it is the fix — not that the file might be
incomplete.

**Adjacent to §42 and not the same, worth keeping apart.** This is the *load* stage failing loudly
with the wrong explanation. §42 is the *parse* stage failing silently with no explanation. They sit
in sequence on one road: without the newer SDK the run stops here, and with it the run proceeds to
hand C# the pinned Roslyn cannot read to a walker that will not say so.

**Fixed 2026-08-21.** `Failure` grew a fourth arm, and it says:

```
This machine does not have the .NET SDK the solution asks for. That is a missing
prerequisite rather than a problem with the file, which is fine as it stands.

C:\...\src\global.json pins SDK 99.0.100.

Run 'dotnet --list-sdks' to see what is installed and install the version that
is pinned; 'dotnet --info' names the one Bearing would otherwise have used.
```

**It names the file rather than describing it.** A `global.json` can sit any number of directories
above the solution — nopCommerce keeps its pin beside the solution, plenty of repositories keep it
at the root — and sending a user to go and find one is most of the work the message exists to save.
The lookup walks up from the solution and takes the nearest, because that is what the SDK host
obeys; a message naming a different file would send the user to edit something with no effect.
Every failure to read one is swallowed and the file is still named without its version: this runs
on the worst run a user has, a malformed pin is plausible there, and throwing out of an error
message is worth nothing.

**Ordering, which is the part that could quietly reopen §23.** Certain-from-the-path first, then
the cause that names itself, then the guesses. A `.csproj` is not a solution whatever the machine
carries and will still not be one after an install, so it is settled before anything reads a
message. The `.slnx` arm is *not* in that position — its advice is an inference drawn from a
failure that may not be about the file at all, and this defect is the case where it is not, so a
`.slnx` on a machine short an SDK now gets told about the SDK instead of being told to check that
it is well-formed.

**Pinned by a real load, not a synthesized exception.** The whole fix turns on matching words
MSBuild chose, and a test supplying those words itself proves only that a constant matches a
constant. `A_solution_pinning_an_uninstalled_sdk_is_not_reported_as_an_unreadable_file` writes a
`global.json` pinning `99.0.100` with `rollForward: disable`, walks it, and asserts the advice names
the SDK and never says *"complete and readable"*. It is deterministic on any machine — unlike the
report that found this, which needed one without .NET 10 — and it fails if MSBuild rewords the
sentence, which is the failure mode: this arm silently falling back to the fallback. Both markers
are matched independently, `hostfxr_resolve_sdk2` being the stable half.

### 44. Every channel of the reach plot is normalised to its own run, so two reports cannot be compared by eye

Found by the use the picture is most likely to get: two runs of the same solution, side by side.
nopCommerce at 2024-08-27 and at 2026-08-21, same tool build, `Nop.Web.Framework`:

| | reach (x) | density (y) | rendered at |
|---|---|---|---|
| 2024 | **43.1%** | 15.7% | (840, 264) |
| 2026 | **33.7%** | 26.0% | (823, 121) |

**Its reach fell nine points and it did not visibly move.** The x-axis is 17.3 px per 1% in the
first report and 21.6 in the second, so a real improvement — proportionally fewer of the solution's
types reaching into it — renders as the same far-right position, and a reader concludes nothing
changed. A first reader of these two reports read the point as having moved *up and to the right*.
Up was right; right was the axis.

**All three channels are normalised, not just the one that misled.**

```
var xmax = Bound(points.Max(p => p.Reach));
var ymax = Bound(points.Max(p => p.Density));
var biggest = Math.Max(1, points.Max(p => p.Types));
double R(int types) => Math.Max(5, 34 * Math.Sqrt(types / (double)biggest));
```

Position on both axes and bubble area are all relative to the extremes of the run being drawn. **The
y-axis matching across these two reports is luck** — both maxima happened to round to the same
bound — so the one reading that came out right came out right by accident. A project whose type
count never changed also draws smaller whenever some other project grows.

**This is not a nitpick, because the metric was chosen to avoid exactly this.** `PlotPoint.Reach` is
a share rather than a count, and the reason is written on it: *"A count would make the axis a
property of the solution's size rather than of its shape, and two runs could not be read the same
way."* The measure was made run-comparable on purpose and the rendering discards it. Same class as
§34 — the claim is sound and the presentation of it is not.

**It is load-bearing for the paid tier and not only for the free one.** `PRD-paid-tier.md` is
drift: the product is two runs compared. A picture at the top of the report that cannot survive
that comparison is a defect in the thing the paid tier sells, and it would have been found by a
customer rather than by us — this time it was found by a five-minute manual read, which is the
cheapest possible way to have learned it.

**Candidate remedies, none measured.** A fixed 0–100 domain on both axes, which is honest and wastes
most of the canvas. A domain shared with the baseline when one is supplied, which fixes the paid
case and leaves the single-run case unchanged. Or stating the axis range and the area basis on the
plot, which does not make the pictures comparable but stops a reader believing they are.

---

**Measured 2026-08-21, and two of those three are gone.** The third shipped. **The entry stays
open**, because what shipped is a mitigation and the headline is still true: two reports cannot be
compared by eye.

**The fixed 0–100 domain is dead, and it is the one that looked right.** Rendered against
nopCommerce it puts all five labelled projects inside x 110–467 and y 393–465 — a quarter of the
canvas, with the two densest dots merging. That was the expected cost. The unexpected one is that
it moves `Nop.Web.Framework` (43% reach, 29% density) out of the top-right corner, and
`PROTOCOL-a11-newcomer.md` §12 pre-registers *"a position — top right"* as the reading round 2
exists to detect. **The obvious fix would have broken the instrument built to grade the picture.**

**The baseline-shared domain is ruled out for this tier, not merely unbuilt.** **X7** put drift in
the paid service — *"not the free tool with a flag on it"* — and `R1` records the free report being
a section shorter than the probe's as asserted deliberately. So the free tier's own picture cannot
be fixed by a mechanism that needs a second run, whatever gets built later. It also would not have
caught this: **this was found by a manual read of two reports with no baseline between them**,
which is the free-tier case and the one that stays broken.

**What shipped is the disclosure, and the drawing now states all three scales.** Two lines below the
x-axis title:

> Every scale here fits this run: 0–50% across, 0–30% up, dot area against the largest project's
> 1,218 types.
> Another report is scaled to its own run, so the two do not line up — compare the numbers, not the
> positions.

Across the pair that found the defect those read **0–50% / 1,218 types** and **0–40% / 1,281
types**, so the disagreement a reader was previously not given is now on both pages. The area basis
is stated beside the axes because it misleads the same way and is worse: a tick label at least
discloses an axis to a reader who looks for it, and nothing said a dot shrank because some other
project grew.

**It cost nothing that was already drawn.** `Height` and `Bottom` grew by the same 36, so
`Height - Bottom` and `Height - Top - Bottom` — the two numbers both axis maps are built from — are
unchanged at 484 and 420. The diff against the frozen artifact is four lines: the viewBox, the
background rect, and the two new sentences. Every dot, tick, gridline and label sits where it sat.
`The_plotting_area_is_where_it_was_before_the_disclosure` holds that, and the footer gets §36's
collision discipline rather than a second lesson about fixed geometry.

**A second face of the same defect, found on Jellyfin and folded in here rather than filed apart.**
Its y-axis is bound at **100%, set entirely by `Emby.Photos` — 1 of 1 named**. Every other project
on that drawing is under 36%, so **nine-tenths of the vertical canvas is spent on one type**, and
the axis of the whole picture is set by the least informative point on it. It is the family of §34
and §41 — a ratio on a denominator too small to mean anything — and it is the same defect wearing a
second face: the axis is a function of the run, and here the run's extreme is noise. Note what it
does to the remedies above: a fixed 0–100 y-axis costs Jellyfin *nothing*, because that is already
what it draws. **It is not repaired with a floor on the denominator**, which would be the sixth
local threshold of exactly the kind **X16** exists to stop being added one at a time.

**What is left, and it has to work from one run.** A domain that does not depend on the run's own
extremes, that survives the top-right reading, and that is not set by a project with one type in
it. X16 is the road that remains — the baseline road is closed by X7, not merely unpaved — and it
is not this entry's to build alone.

### 46. A suppression emptied the HTML cycles section, because the evidence replacing it went to one renderer — **fixed**

Found by re-deriving `PROTOCOL-a11-newcomer.md` §§11–12 against a fresh cut, which is the fifth
re-derivation to turn something up and the fifth time it was not the thing being looked for.

The cycles rework set the folder-layout components aside as not-findings. On nopCommerce that took
the HTML's namespace group **from 22 cycles to one**, and what remained of the finding was a
thirty-name membership, one example loop, and a list of twenty-one components that are fine:

```
30: Nop.Services, Nop.Services.Affiliates, Nop.Services.Attributes, … (30 names)
loop: Nop.Services → Nop.Services.Localization → Nop.Services.ExportImport → Nop.Services
Mutually dependent, not reported — …
```

**A membership is not somewhere to start.** The pairs are, and they existed: `ffb4415` computed
`ShapedCycle.Pairs` for exactly this, and `83e4675` rendered them — in the text report only.

| | text report | HTML |
|---|---|---|
| cycles in the findings group, v9 → v12 | 22 → 1 | 22 → 1 |
| held-pair lines added back | 6, plus the remainder | **0** |

**The commit that removed the population is the commit that added the evidence, and it touched both
renderers**: `83e4675` is `HtmlReport.cs` +49, `StructureSections.cs` +98. The HTML got the
subtraction and not the addition. `git log -S Pairs -- src/Bearing.Cli/HtmlReport.cs` returns
nothing, in any commit — the pairs were never there to lose.

**This is a general shape, not a one-off, and it is the reason this entry is worth its length.** A
suppression is allowed to make a section *shorter*; it is not allowed to make it **emptier in one
renderer than in the other**. Look for it wherever a change removes a population and adds evidence
in its place — the removal is easy to apply twice and the addition is easy to apply once.

**Live in the A11 round 2 artifact until 2026-08-23.** `a13-materials/nop-v11.html` carries it. T3
asks a participant to pick something the report flags and say what they would do about it on Monday
morning; the largest circular-reference finding on the page had no answer to that, and §6 withholds
the `.txt` unless they ask for it.

**The fix.** `Cycles` builds the same `Cycle`-keyed lookup the type tangles already use and passes
`CycleGroup`'s existing `annotate` seam a `Holding` lambda — six pairs, heaviest first, then the
count of what was not shown. Six because the text report shows six, and the remainder is stated
rather than dropped. Cut as `nop-v12.html`: the terminal report and the plot are byte-identical to
v11, and the HTML differs by the timestamp and seven lines.

**Was not gated, and the green suite was the evidence for it.** 467 passed **both before and after**
the fix, because nothing asserted the section either way: `TestBed`'s only namespace cycle was
`TestBed.Core`, a `FolderLayout`, so `IsReportable` was false, the HTML golden rendered `None.` for
the namespace group, and `grep -c "held reference"` was **0** in both HTML goldens. `CycleShape.Coupling`
was exercised in `CyclesAndCouplingTests` through `ShapeReading`, which takes hand-written members and
weights precisely because `Cycle` cannot be constructed outside Core — so the **judgement** was gated
and **neither renderer's rendering of it** was.

**Closed 2026-08-23 by plant P10** (`95ed129`), two sibling namespaces holding each other's interface
in a field — the fixture's first `Coupling` cycle and the first time `IsReportable` has returned true
here. The HTML golden now carries the line this defect was about:
*"TestBed.Core.Tariffs ↔ TestBed.Core.Weighing — 2 held reference(s)"*, and
`The_planted_cycle_is_coupling_and_carries_its_pair` asserts the shape, the pair and both hold
counts. `docs/TESTING.md` carries the plant's cost: four types, one existing cohort moved
(`kind:Contract`, 8 → 9), no nomination changed, `PolicySweepTests` green and the leave-one-out
verdicts unchanged at 25 / 4 / 1.

### 47. The report tells the reader the exports carry the findings, and no export carries any — **reworded; the export itself is `SCHEMA-findings-export.md` step 5**

Found while writing `BEARING-OUTPUT-CONTRACT.md` — reading the output surface for a consumer rather
than for a reader, which is a third pair of eyes on the same page and turned this up on the first
pass.

`Everything else` closes with this, on every HTML report (`HtmlReport.cs:441`):

```
None of them is hidden and none is summarised away — every one is in the exports,
which carry every finding, every type, every member and every dependency:
  --json  — the whole model, with a schema version, for anything that reads it back.
  --csv   — types.csv, members.csv and edges.csv, which join on identity rather than on a name.
  --full  — this page with every section enumerated, capped at --top (15) per kind.
```

**It is false in each of the three things it lists.**

| listed | carries findings |
|---|---|
| `--json` | **no** — the document's twelve top-level keys are `schemaVersion, tool, generatedAt, solutionPath, policy, coverage, projects, types, edges, cycles, externalDependencies, boundary`, and `grep -i finding src/Bearing.Cli/JsonOutput.cs` returns nothing |
| `--csv` | **no** — same grep over `CsvOutput.cs` returns nothing; the three files are model rows |
| `--full` | prose, and **capped at `--top`** — 15 of this run's 117 hubs, so it is the summarising-away the sentence denies |

**The exports carry the model. The findings are rendered and never serialised.** That is a defensible
scope decision — `TECHREQ-job-a.md` §1 defers the finding record on purpose — but the page states
the opposite of it as reassurance, in the one paragraph whose whole job is to promise nothing was
hidden. The comment above the `--full` branch (`HtmlReport.cs:62`) tells the next maintainer the same
untruth: *"every row of it is still reachable — in `--json`, in `--csv`, and here behind a flag."*

**Same family as §40, §41 and the D2 sentence in the A11 protocol §11**: the page asserting a
property of itself that is not true, in each case a property nothing on the page lets a reader check.
Four now, which makes it a class rather than a run of bad luck — and the class is *claims the report
makes about the report*, which no fixture can hold, because the fixture renders the same claim.

**Live in the A11 round 2 artifact.** `a13-materials/nop-v12.html` carries it, immediately under the
per-kind census a participant reads for T3, and it is the sentence that would answer a participant
asking where the other 102 hubs are.

**Not fixed.** Two ways to close it and they are not the same size: reword to what is true — the
exports carry the model, `--full` enumerates to `--top` — or **make the sentence true** by serialising
findings, which is `PRD-paid-tier.md`'s seam and not a wording change. Wording now does not foreclose
the export later. Either way it costs a recut of the round 2 artifact.

**Both renderers carried it, and that is worth its own line.** §46 is the two renderers *drifting*;
this is the two renderers **agreeing and both being wrong** — the HTML in `Everything else`, the
terminal in `Highlights.cs:77` (*"every finding this run made is in --json and --csv"*), different
words for the same untruth. A parity check between the renderers would have caught §46 and would have
passed this. What catches this is `SCHEMA-findings-export.md` §1's rule — *the export is a superset of
every judgement the free tool renders* — asserted against the export rather than between renderers.

**Reworded 2026-08-23, and the wording says only what is true now.** What is complete is the
*counting*, so that is what is claimed: *"Nothing above is a quiet subset — the count beside each kind
is every finding of it this run made. What is capped is the enumeration, not the counting."* `--json`
is described as *"the model every claim was computed from … It carries the model rather than the
claims"*, which is both honest and the thing a user reaching for `--json` needs to know before they
open it. The terminal line matches. The comment at the `--full` branch now records what it used to
claim rather than repeating it.

**Gated, unlike §46.** Both report snapshots failed on the change — `ReportTests.The_report_renders`
and `HtmlReportTests.The_report_renders` — and the diff was exactly the two sentences and nothing
else. Goldens accepted under `CONTRIBUTING.md`'s rule. `The_full_report_renders` did **not** move,
correctly: the paragraph lives in the `else` branch and `--full` does not render it.

**Still open, and deliberately: the sentence is true rather than strong.** Making it strong again —
*every finding is in the exports* — is the findings export, `SCHEMA-findings-export.md` step 5. This
entry closes when the export ships and the wording can go back.

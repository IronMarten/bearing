# Known defects

Behaviour that is wrong today, recorded rather than fixed. Every entry names what supersedes
it, and most are pinned as tests in `KnownDefectTests` so that neither carrying one forward nor
fixing one can happen quietly.

This is also the work order for extraction. None is a patch to `oracle/ArchProbe`. Most are fixed
in `Bearing.Core` — **18, 19 and 20 are fixed in `Bearing.Cli`**, and they are a different class:
not defects carried through the port, but ones the port made visible by producing a report a user
could read. All three were found by running the shipped binary on a real solution, and none of
them can be caught by the suite. See *How these were found*.

## Why a defect gets recorded instead of fixed

`oracle/ArchProbe` is frozen. The freeze is not a compatibility promise — nothing has shipped
that could depend on it — it is the fixed point of an in-flight refactor. Extraction moves ~997
lines of computation out of `Report.cs`, and the oracle is what separates *"I broke it"* from
*"I changed it on purpose."*

Stillness is the property it provides, not correctness. An oracle that is wrong in a known,
pinned way discharges that job perfectly. Editing it costs every fix twice, because Core is a
reimplementation rather than a port, and spends the safety net on code that is being deleted.

**The rule.** A correctness defect never justifies editing the oracle, because the oracle does
not claim to be correct. Only a defect that stops it *functioning as an oracle* does —
nondeterminism, or an inability to produce a baseline at all. Defect 6 was the one that had to
be tested against that bar; it is the only entry here that was fixed in place.

**The freeze is not absolute.** `KnownDefectTests` is the register of golden rows *expected* to
move, each naming the requirement that supersedes it. The regime is "frozen except for a
registered list of intended changes" — not "frozen".

**The probe is still the only implementation that *renders*.** Core computes every finding and
every structure section, and several of these defects are fixed there — but nothing a user runs
reads Core yet, so the shipped tool carries the register until `TASKS.md` R1. That is an argument
for R1 being the only thing on the agenda, not for unfreezing.

**Read each entry's own status line, not this one.** "Fixed in Core" and "fixed" are different
claims here: the first means the defect is gone from the model and still present in anything the
probe renders.

## The register

Roughly severity-ordered. **"Pinned" means `KnownDefectTests` asserts the wrong behaviour as
current — the *probe's* behaviour, and nothing more.** Every assertion in that class runs against
the probe's run, and the probe is frozen, so a pin cannot fail when Core starts doing the right
thing and cannot be relied on to announce it. Defect 1 is the proof: Core has keyed type identity
on `(assembly, FQN)` since `ModelBuilder` adopted `SubjectRef`, and the pin is still green. Pins
retire with the oracle at `TASKS.md` R2.

What catches a fix — or a fix that quietly does the wrong thing — is the equivalence suite, which
runs both implementations and compares them. A defect Core is expected to fix needs an entry
there, stated as an intended divergence, as well as a pin here.

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
(`TECHREQ-job-b.md` §8 criterion 8), and `WalkerEquivalenceTests` asserts the divergence from
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

### 2. Absolute gates saturate; percentile gates do not — **one of three converted**

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

**Hubs (6.9%) and breaks alone (2.8%) are not converted**, and neither should be until there is a
fixture case that can observe the difference: `TASKS.md` P7.

### 3. Truncation is never disclosed

15 of 106 shown, and nothing says so. `ARCHITECTURE.md` invariant 8.

### 4. Load success is judged by diagnostic, not outcome

All six nopCommerce "load failures" were NuGet vulnerability advisories; every project loaded. A
hard-fail rule would refuse a major .NET codebase over unrelated CVEs.

### 5. `DataAccess` classification is a hardcoded list of four ORMs

Misses LinqToDB and FluentMigrator, so nopCommerce's data layer reads as `Internal` and
layer-span goes silent rather than misfiring. Silence is the better failure of the two, which is
why it survived this long.

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

### 8. `.slnx` solutions do not load at all — **confirmed at A2**

Orchard Core could not be analysed. Cannot block extraction — TestBed is a `.sln`.

Reproduced deliberately at A2 against a hand-written `.slnx` over TestBed's three projects:
`Microsoft.Build.Construction.SolutionFile.Parse` throws `InvalidProjectFileException: No file
format header found`. **And it takes the tool down with it** — eleven frames of MSBuild stack
trace, straight at the user, which is §23 and a separate defect from this one.

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
`FindingEquivalenceTests` agrees on the fixture.

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

**Origin does not decide the integration map, and the fixture is why.** The first attempt treated framework-resolved as plumbing and emptied the map: `System.Data` and `System.Net.Http` both resolve from the framework, and both are exactly how TestBed reaches a database and the network. `CoreEquivalenceTests.The_integration_map_over_the_fixture_is_what_it_should_be` caught it in one run. **The two questions are different** — origin answers *"could somebody change this dependency"*, the filter answers *"does this reach outside the process"* — and the reader asked the first, whose answer is a label on the row rather than a reason to drop it.

Unknown says nothing rather than guessing a third answer, and the name-based filter still applies to those. On nopCommerce: `Newtonsoft.Json` and `FluentValidation` are packages, `System.IO` is framework, and `Microsoft.AspNetCore.Mvc` resolves as a package while `Microsoft.AspNetCore.Http` resolves as framework — a distinction no name list makes.
### 31. A folded diagram box does not read as containing the projects that are missing — **fixed**

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
boundaries, where 1% cannot move a rank; `FindingEquivalenceTests.The_boundary_rank_is_reachable_from_both_sides`
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

### 35. Three inline SVGs share one stylesheet, and two of them share class names

The report inlines three drawings — `ReachPlot`, `Mosaic` and `ArchitectureDiagram` —
and an SVG `<style>` block inside an HTML document is **not scoped to that SVG**. Every rule is
page-wide, and the last block wins.

**Found while building the plot**: its label class was `nm`, which is also the project map's, so the
two silently restyled each other's text — 13px against 14px, which is exactly the kind of difference
nobody reports as a bug. The plot's rules are all scoped to `.rp` now.

**The other two still collide and are getting away with it.** `Mosaic` and `ArchitectureDiagram` both
define `.bg`, `.ti` and `.lg`, and it is harmless *only because the definitions happen to agree*.
Changing either one changes both drawings, and nothing fails: the mosaic's title would silently take
the diagram's size, or the reverse, depending on which section renders last.

**Not urgent and not invisible either.** Scoping both is mechanical — a class on each root and a
prefix on each rule — and it moves two snapshots. What makes it worth an entry is that the failure
mode is a *rendering* one, so the suite cannot see it: both artifacts stay well-formed, both keep
every element, and the picture is simply wrong on the page and right when written standalone with
`--mosaic` or `--diagram`.

### 36. The plot's y-axis title overlaps its own subtitle

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

**Cheap to fix and deliberately not fixed yet** (2026-08-20): move the axis title into its own row
above the plot area, or rotate it up the axis where it belongs, and give the strip enough height for
both. The reason it is recorded rather than repaired is that it arrived at the end of a long
presentation pass, and the next change to this file should carry it rather than a session of its
own.


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

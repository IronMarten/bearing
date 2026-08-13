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

### 24. A constructor renders as `Type..ctor`

`CustomerInfoValidator..ctor` — from nopCommerce. The member's name *is* `.ctor`, and the sentence
joins type and member with a dot.

Cosmetic, one line, and **only visible on real code**: TestBed declares no constructor complex
enough to be nominated, so no fixture case and no snapshot shows it. Related to §13, which is the
same identity question one level deeper.

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

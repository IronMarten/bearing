# Known defects

Behaviour that is wrong today, recorded rather than fixed. Every entry names what supersedes
it, and most are pinned as tests in `KnownDefectTests` so that neither carrying one forward nor
fixing one can happen quietly.

This is also the work order for extraction. Each of these has to be fixed in `Bearing.Core`;
none is a patch to `oracle/ArchProbe`.

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

**The probe is still the only implementation**, so the tool carries these defects until
extraction lands. That is an argument for extraction being the only thing on the agenda, not for
unfreezing.

## The register

Roughly severity-ordered. "Pinned" means `KnownDefectTests` asserts the wrong behaviour as
current, and will fail the day Core does the right thing.

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

**Half remedied.** `SubjectRef.ForType(assembly, fqn)` is the correct key and is tested — see
`ARCHITECTURE.md` §4. Nothing computes with it yet; the walkers still key on name, so the defect
is live until extraction adopts it.

Pinned: `Two_types_sharing_a_name_across_assemblies_merge_into_one_row`.

### 2. Absolute gates saturate; percentile gates do not

Change cost fires on 7.9% of nopCommerce, hubs on 6.9% of Jellyfin, both truncated to 15 by
`--top`. Blast radius — the only percentile-gated finding — held at 1.0% and 0.9% across both.

Convert, do not retune. But see defect 14: converting runs *toward* a different hazard.

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

### 7. 1.4–2.0% of edges point at absent types

Including Roslyn anonymous types, which should never be graph nodes.

### 8. `.slnx` solutions do not load at all

Orchard Core could not be analysed. Cannot block extraction — TestBed is a `.sln`.

### 9. Change cost gates on `minCohort` where it means a fan-in floor

Both default to 5, so the two are indistinguishable at defaults and the defect is invisible in
the goldens. It appears only when either is tuned — which is exactly when someone is relying on
the threshold to mean what it says. Superseded by defect 2: the answer is a percentile.

Pinned: `Change_cost_is_gated_by_min_cohort_where_it_means_min_fan_in`.

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

### 12. `WIDEST CONTRACT SURFACE` can never be suppressed, at any boundary count

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

**It is reachable but not yet observable.** The qualifying set is bounded by `floor(n/2)`, so
firing needs at least twelve boundaries with six of them qualifying; TestBed has **ten**, and a
maximum of five can qualify. The gate is reachable in principle at every solution large enough,
which the current one is not at any size — but the fixture needs boundaries planted before the
suppression has a behavioural test, and until it does this stays a suppression that cannot fail.

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

Pinned: `Widest_contract_surface_can_never_be_suppressed`.

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

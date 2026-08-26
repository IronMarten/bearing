# Known defects

**Open entries only.** Behaviour that is wrong in the shipped tool today, recorded rather than
fixed. Every entry names what supersedes it.

**Closed entries are not kept here any more (2026-08-24).** The register had reached forty-seven
entries and forty-four of them were closed, so the file was ninety-four percent archive and a
reader had to filter it before it could be used as a work list. **The prose is in git**, and
`## Closed` below is the index that says which number was what — a title and a status line each, so
every `D<n>` reference in the specs still resolves without archaeology. Recover one with
`git log -p -- docs/DEFECTS.md`, or `git show <rev>:docs/DEFECTS.md` for the whole file as it was.

> **Two things the purge turned up, recorded because they say what this file is for.**
>
> **D3 and D17 carried no status line, and the reason is mundane: their closure was written down
> in the other file.** Both were closed at **R1**, with a test each — D3's renderer states what it
> dropped in every capped list, verified at `--top 2` where six lists disclose; D17's coverage
> section asks `FindingSet.About` and reports the answer, and its test derives the number from the
> finding set rather than reading the sentence. The private `DONE.md` records both, and R1's own
> entry there names all four defects it closed. **Two of those four — D11 and D16 — were already
> marked here from earlier Core work, and the two R1 closed in the renderer were never mirrored
> back.** So it is one missed update in one batch, not a general unreliability.
>
> **The lesson is about having two places that carry status, and it is the one `TASKS.md` already
> warns about, arriving from the other direction.** A closure written to the archive and not to the
> register leaves the register saying nothing, and nothing on either side notices. **The register
> is the authority for whether a defect is live**; if a closure is recorded anywhere else first, it
> is not recorded until it is here.
>
> **And D34's entry had been split in half** — its heading and opening in one place, its resolution
> stranded below the `## How these were found` break with D40 and D41 in between, so a reader
> following the heading got D40's content. Nothing caught it. **The file stopped being readable
> before it stopped being written to**, which is the cost this cleanup was paying.

## Why a defect gets recorded instead of fixed

**An entry here is a claim about the shipped tool, and nothing pins it.** `KnownDefectTests` and
the equivalence suite both went at `TASKS.md` R2, with the frozen probe they asserted against, and
neither is a loss: **a pin against a frozen implementation cannot fail when the live one starts
doing the right thing.** D1 is the worked example and it is in git — Core had been keying type
identity correctly since `ModelBuilder` adopted `SubjectRef`, and the pin stayed green throughout,
so it could not tell you whether the defect was live.

**What replaces a pin is an ordinary test in the suite that owns the behaviour**, naming what that
behaviour must be. A defect that is still live and still wants watching needs one of those.

**The divergence to watch is between renderers.** The terminal report and the HTML are two
renderers over one model and they do not carry the same disclosures. D46 and D47 were both of that
shape, and so is the rule they produced: **a suppression is allowed to make a section shorter; it
is not allowed to make it emptier in one renderer than in the other.** Look for it wherever a
change removes a population and adds evidence in its place.

## The register

*Six open. Roughly severity-ordered, and the numbers are identity — they are never reused and
never renumbered.*

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

### 53. The project map draws an edge through whatever box is in the way, so a direct dependency reads as an indirect one

The map places projects in depth layers and routes each edge as a cubic from the source box's
bottom edge to the target box's top edge. **Nothing avoids the boxes in between.** Where a layer is
narrow the columns line up, the spline degenerates to a straight line, and it crosses an unrelated
node.

On nopCommerce `Nop.Services`, `Nop.Data` and `Nop.Core` are one column — all three at x 206–374,
at y 543, 651 and 759 — and the `Nop.Services → Nop.Core` edge is `M290 605 C290 625 290 739 290
759`: **a vertical line at x = 290 straight through the interior of the `Nop.Data` box.**
`Nop.Core` *is* a direct dependency of `Nop.Services`, in the declared graph and the used graph
alike. Nothing distinguishes that edge from one that terminates at `Nop.Data` and resumes below it.

**Measured, and it is not a nopCommerce accident:**

| run | edges crossing an unrelated box |
|---|---|
| `nop-v13.html` | **18 of 29** |
| `nop-2024-08-27.html` | 18 of 29 — so it predates the backtest cut |
| `jellyfin-v3.html` | **81 of 98 — 83%** |

**It worsens as the map grows**, which is the opposite of what a map is for.

**Found by A11 round 2, and the participants are the evidence that it misleads**: two of five
described `Nop.Core` as an **indirect** dependency of `Nop.Services` — the reading the drawing
supports and the code contradicts — in a task all five otherwise answered correctly, inside two
minutes. The private record is `FINDINGS-a11-round2.md` §5.

**This is D45's inverse, and the two are one piece of work.** D45 is geometry asserting a
dependency the code does not have, a wrapped layer read as a row; this is geometry concealing one
it does. Both are the layered map's *drawing* rather than its data, and both are worse on Jellyfin.
**Do not fix this alone and call the map done.**

**The cheap half needs no new rule**: route around box interiors — a waypointed or orthogonal edge
where a straight one would cross — which is independent of D45's open question about when a wide
layer is a fact about the codebase rather than a layering to fix.

**What would hold it**: a geometric assertion over the rendered SVG — no edge path enters the
interior of a box rect that is not one of its own endpoints. `TestBed` already has a
three-project chain, so this is reachable without a plant, which is rare for a picture defect.

### 54. `--top`, a display cap, decides a judgement

`AnalysisPolicy.RollCallThreshold` is `Top / RollCallDivisor` — 15 / 3, so 5 by default — and
`SpansArchitecturalLayers` tests a nomination's group size against it to decide whether the finding
carries the `part-of-a-layering-pattern` qualifier. **`Top` is the display cap**, applied everywhere
else through `Sentences.Cap` to decide how many rows a section prints.

So the same code at the same commit says different things about itself depending on how many rows
the reader asked to see. Measured on `TestBed`, through the shipped CLI:

| run | findings | keys | layer-span findings claiming a layering pattern |
|---|---|---|---|
| `--top 1` | 180 | identical | **all of them** — the threshold is `1 / 3` = 0, and every group size exceeds 0 |
| `--top 15` | 180 | identical | 8 fewer |

**The population does not move — only the claim about it does.** That is what makes this worse than
a cap and not better: nothing appears or vanishes, so a consumer diffing two runs sees a qualifier
flip with no cause anywhere in the code, which is the *"real architectural event"* reading
`SCHEMA-findings-export.md` §3 worries about in a different setting.

**Invariant 2 is the thing being decided.** *Anomaly, not roll-call* is a judgement about the
codebase — four controllers reaching into data access is one fact about your layering rather than
four findings. Deriving its threshold from the display budget makes it a judgement about the
terminal instead, and the qualifier is model-level: it reaches both renderers and now the export.

**Found by `SCHEMA-findings-export.md` §8.3's test**, which asserts the export is uncapped because
*"a persistence format must not depend on a presentation flag."* It fails, and the reason it fails
is not the export.

**§4 of that document is wrong where it explains the omission** — *"`--top` is not represented. It
is applied by the renderers through `Sentences.Cap`; **Core has no notion of it**"*. Core has one.
Corrected there, with a pointer here.

**`Top` is two settings wearing one name**, and that is the shape of the fix rather than the fix
itself: the number of rows to print and the group size above which detail collapses are unrelated
quantities that happen to be one ratio apart today. **Not repaired here, and deliberately** — the
remedies are an absolute group-size floor or a share of the nominated set, neither is measured, and
this register's own discipline is that a threshold proposed without measuring both solutions is the
next entry rather than the fix for this one. It is X16's family in a different layer: a constant
that looks calibrated and is coupled to something it has no relationship with.

### 58. A namespace cycle's shape is decided by one arm and applied to the whole component

Umbraco's namespace graph has a single strongly-connected component of **363 namespaces**, rendered
as:

```
NAMESPACE CYCLES - sibling namespaces that hold each other as state,
so neither can be layered, understood or extracted without the other:
  363 namespaces: Microsoft.Extensions.Hosting <-> ... - 6 of 363 shown
```

**The component is real and the sentence is not.** 363 namespaces are not *siblings*, and the
section itself lists only **14 mutually-holding pairs** inside it -- `CycleShapes.Read` labels the
whole component `Coupling` because *at least one* sibling pair holds, and the label then describes
all 363.

**Verified at source rather than argued from the graph.** Exactly one Umbraco file declares
`namespace Microsoft.Extensions.Hosting` -- an extension class following the ordinary .NET
convention of putting extensions in the namespace of the thing they extend -- and
`Umbraco.Extensions` spans 175 files across projects. Those catch-all namespaces reach everything
and are reached by everything, which collapses the projection into one component. **The finding is
true and unactionable**, which is invariant 2: a flag that fires on 363 of a codebase's namespaces
conveys nothing.

**A second face, folded in here rather than numbered separately**: `Microsoft.Extensions.Hosting` is
presented as a component of Umbraco's architecture. It is, by the model's definition -- Umbraco
declares a type in it. It reads as the framework being part of the cycle.

**Defect 45's missing rule is the same rule**, in the namespace layer instead of the map layer: a
component this large is either a layering problem or a fact about the codebase, and there is nothing
that tells them apart.

## Closed

**Index only — the prose is in git.** Fifty-five entries: forty-four removed 2026-08-24, plus D55 and D59, then A11 round 2's presentation list — D48, D49, D50, D51, D52 and D60 — and then D56 and D57, all closed 2026-08-25 and indexed the same way. Status is as the
entry last recorded it, except where this table says otherwise. The last revision carrying all
forty-seven in full is the commit before this one.

| # | entry | status |
|---|---|---|
| 1 | Type identity is keyed on fully-qualified name alone | fixed |
| 2 | Absolute gates do not travel between codebases | closed 2026-08-21 |
| 3 | Truncation is never disclosed | closed at R1 |
| 4 | Load success is judged by diagnostic, not outcome | fixed |
| 5 | `DataAccess` classification is a hardcoded list of four ORMs | fixed |
| 6 | Visit-order dependence | resolved |
| 7 | 1.4–2.0% of edges point at absent types | fixed in Core at A2, and the rate was the least of it |
| 8 | `.slnx` solutions do not load at all | fixed |
| 9 | Change cost gates on `minCohort` where it means a fan-in floor | fixed in Core |
| 11 | The layer-span collapse hides the anomaly it shares a signature with | fixed in Core |
| 12 | `WIDEST CONTRACT SURFACE` can never be suppressed, at any boundary count | fixed in Core |
| 13 | `MethodMetrics.Id` is not an identifier — it is the bare method name | fixed |
| 14 | A percentile floor can be unsatisfiable, and `FanInPctl >= 95` is | fixed |
| 15 | Breaks-alone's concealed-decision suppression is type-level only | fixed |
| 16 | A god object by size is told it carries real logic | fixed in Core |
| 17 | `NO PEER GROUP` claims an absence that is not true | closed at R1 |
| 18 | The report header is working notes, addressed to the people who built it | fixed at A0 |
| 19 | The cohort sentence discards the field that would make it true | fixed at A0 |
| 20 | `0 external contact point(s)` prints directly above six external systems | fixed at A0 |
| 21 | `SolutionModel.ToolVersion` reports the wrong assembly | fixed |
| 22 | Anonymous types are collected as components | fixed in Core at A2 |
| 23 | An unreadable solution crashes with a raw MSBuild stack trace | fixed |
| 24 | A constructor renders as `Type..ctor` | fixed |
| 25 | A redirected report is transcoded through the process code page | fixed |
| 26 | The three facts under a finding's name are unlabelled, and are not the same kind of thing | fixed |
| 27 | `Why this fired` publishes 65 internal identifiers | settled |
| 28 | A ratio against a zero median renders as `∞` | fixed |
| 29 | *"too large for anyone to hold at once"* reads as the tool giving up | fixed |
| 30 | External dependencies do not separate framework from third-party from first-party | fixed |
| 31 | A folded diagram box does not read as containing the projects that are missing | reopened 2026-08-22, fixed again |
| 32 | A verb agrees with a number a real solution made singular | fixed |
| 33 | A boundary finding fires on a third of the boundaries it filters | fixed |
| 34 | A cohort of 2,909 is not a peer group | fixed, and the diagnosis moved |
| 35 | Three inline SVGs share one stylesheet | fixed |
| 36 | The plot's y-axis title overlaps its own subtitle | fixed |
| 37 | The JSON export's `projects` array is positioned by solution declaration order | fixed at R2 |
| 38 | `undefinedx its peer median` | fixed |
| 39 | A member subject is a display string, and four kinds of member are not identified by it | fixed by X14 |
| 40 | The mosaic outlines twelve cells and calls them the eleven claims above | fixed |
| 41 | The `Clean` tile counts a disclosure as a finding | fixed |
| 42 | A file Roslyn cannot parse is walked anyway, and the report says it read everything | fixed |
| 43 | A solution needing a newer SDK is reported as an unreadable file | fixed |
| 46 | A suppression emptied the HTML cycles section, because the evidence replacing it went to one renderer | fixed |
| 47 | The report tells the reader the exports carry the findings, and no export carries any | reworded; the export itself is `SCHEMA-findings-export.md` step 5 |
| 55 | A count-bearing sentence disagrees with its own number | fixed 2026-08-25 |
| 59 | An MSBuild diagnostic is pasted into the report verbatim | fixed 2026-08-25 |
| 48 | The project map tints two boxes and nothing on the map says what the tint means | fixed 2026-08-25 |
| 49 | *main sequence* is used twenty-five times and is never defined | fixed 2026-08-25, with §48 |
| 50 | Both of the mosaic's marks are per *type*, and readers count both as *findings* | fixed 2026-08-25 |
| 51 | `fan-in 28, fan-out 24` opens a first-screen claim with no gloss | fixed 2026-08-25 |
| 52 | The `Most intricate` tile names a member without its project | fixed 2026-08-25 |
| 60 | `framework` and `package` read as a category and mean a provenance | reworded 2026-08-25 to state the resolution |
| 56 | Nothing tells the reader that a project's references did not resolve | fixed 2026-08-25 |
| 57 | Two types with one fully-qualified name render as one type contradicting itself | fixed 2026-08-25; the entry's reason for surviving was wrong, see below |
| 61 | `TimesMedian >= OutlierFactor` is satisfied by definition at a zero median | closed 2026-08-25 by X16 — the ratio no longer gates, so there is no tautological gate to fire |

> **D56 shipped with a wider meaning than it was filed with, and the widening was measured.** The
> entry proposed counting `CS0246`/`CS0234`/`CS0012` as *"this project's references did not
> resolve"*, which is restore failure. It is restore failure **and one other thing**:
> `Umbraco.JsonSchema` has no `project.assets.json` and cannot find `CommandLine`, `Namotion` or
> `NJsonSchema`, which restore fixes — and `Umbraco.Core` is fully restored, emits one, and the one
> is `UmbracoBuilder.cs:325` registering `AddUnique<IElementContainerService,
> ElementContainerService>()` **against a type no file in the solution declares.** Both are missing
> edges and the consequence is identical, so both are counted; the sentence reports the consequence
> and says the cause is usually restore rather than asserting it. The first draft said *"restore the
> solution and re-run to close the gap"*, which is false on the second case.

> **The cost was measured on both reference solutions before it shipped, and it is not what it
> looks like.** `GetDiagnostics` binds every method body, which reads as a second semantic pass and
> is mostly a rescheduled one — the walk was paying for that binding lazily, one question at a time.
> With `--profile`, before and after: nopCommerce `compile 10.5s → 16.4s, walk 15.5s → 12.1s, total
> 33.7s → 35.2s`; Umbraco `compile 3.5s → 8.2s, walk 21.1s → 12.4s, total 30.4s → 26.5s`. **+1.5s on
> one and −3.9s on the other**, against a 60s cold budget.

> **D57's entry was wrong about why it survived, and the property is what found that out.** It
> recorded the fixture's planted collisions — `PayloadTag`, `CarrierTwin` — as *"never nominated, so
> they never reach a claim"*. `TestBed.Interop.CarrierTwin` is declared in both `Core` and `Data`,
> **both declarations are nominated**, and the two rows separate cleanly because `Subjects.Where`
> leads with the project. That is §57's scenario handled rather than §57 occurring, and it is why
> nothing ever looked wrong. The first draft of the property asserted *one name, one address* and
> failed on exactly that pair — correct behaviour, flagged as a defect. It is keyed on the declaring
> type instead.

> **What was actually broken was a second face, found on Umbraco on 2026-08-25 and not in the
> entry.** The concealed-decision claim titles itself with the type's most complex *member* and
> passed no trailer, so `Subjects.Where` fell back to the declaring *type*'s line: the page printed
> `Utf8ToAsciiConverter.ToAscii` at `:12`, the class declaration, beside a tile printing the same
> name at `:131`, the method. **And the type declares two `ToAscii` overloads**, at 76 and 131, cc 3
> and cc 1312 — so a reader could not tell whether they were looking at two methods or one method
> described twice. §39 made a member subject an identity *precisely* so a member could be located;
> this was that work stopping one element short, which is the shape §52 had on the tile.

> **The six of 2026-08-25 were one session and three pieces of work**, and two of them changed
> shape when the words were rendered rather than reasoned about. **§50's legend wording came from
> the register and did not survive contact**: *"N types the claims above are about"* says *above*,
> which is false in the one place the mosaic is designed to end up — pasted where the report is
> not — and at a count of one it reads *1 types*, which `Prose` then flags. It ships as *N types
> the report leads with*. **§51's first attempt printed every digit twice** — *"fan-in 7, fan-out 7
> — 7 types use it, and it uses 7 types"* — and became a parenthesis after each number instead.
> Neither was visible until the received diff was read.

> **§48 left one thing unobserved and it is now asserted instead.** The `useless` tint fires on
> none of nopCommerce, Jellyfin, Umbraco or TestBed, so the key's two-zone branch has never been
> rendered. `ArchitectureDiagramTests.Every_tint_the_map_can_draw_is_keyed` states the property
> over the enum rather than over a run: a zone a box can be tinted for that carries no name or no
> gloss is a keyless colour shipping again, and no golden would say so.

> **D3 and D17 are listed as *closed at R1*, which is what `DONE.md` says and what this file never
> did.** Their behaviour was also verified against the shipped run before removal. They are why the
> table states a status for every row rather than copying what each heading happened to carry.

> **Nine of these rows are X16's specimen set, and X16 is still open.** §2, §9, §14, §17, §19, §28,
> §33, §34 and §38 — with live **D10** as the tenth — are one design decision failing ten times,
> each closed by adding or moving a threshold on cohort *size*. D10's entry carries the argument;
> what these nine carry is the evidence for each instance. **Recover their prose before X16's
> measurement**, which is X16's first work and is a reading exercise before it is a code change.
> They are the one part of this index whose full text is load-bearing for open work.

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

**The same method ran again as A11 round 2, and found D48–D53.** Five .NET developers who had never
seen nopCommerce, one room, own laptops, **answers written privately to the facilitator rather than
spoken**. Four things it added to the three lessons above:

**Written answers are worth the awkwardness of collecting them.** Round 1 was two people answering
out loud, where the second answer can be an echo. Round 2's best reading of the mosaic — one that
tied it straight back to the plot's own *"118 of 547 named"* — came from someone writing alone, and
the sentence the room converged on afterwards was **cruder than what one participant had already
written**. Without the private answers that reading would not be in the record at all, and the
mosaic would have looked worse than it is.

**A defect can be sitting in the geometry rather than in the words.** D48 through D52 are wording,
the class this method has always been good for. **D53 is not**: it took a participant saying
*indirect* about a direct dependency, and then opening the SVG and measuring, to find that 18 of 29
edges cross a box they have nothing to do with. **The report was read by five people and the
drawing was read by none of them** — the question *what does this line pass through* is one nobody
thinks to ask, which is why it survived every freeze and both solutions.

**A short answer is not a shallow one, and scoring it as one inverts a result.** *"The ones with the
biggest dots"* was first written up as the pre-registered misread of dot area as severity. It was
not: another task in the same session recorded that every participant knew area was the type count,
and the question being answered — *which part would you least want to change* — rewards exactly that
compression. **Check whether another task in the same session already disconfirms an error before
recording it.**

**Verification cuts both ways, and that is what makes it useful.** In the same write-up, one
reported reaction turned out to be a real defect the moment the artifact was opened (D53), and two
turned out to be nothing (the misread above, and a loose paraphrase of an axis whose caption states
the share correctly three times). **Neither outcome is available without opening the file.**

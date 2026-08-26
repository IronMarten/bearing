# Architecture

What Bearing is built out of, and the rules that hold it together. Each rule here was
learned by shipping the opposite. Where a rule is enforced by a test rather than by review,
that is said explicitly — and it is said because review already failed to hold that line
once.

Read alongside [`TESTING.md`](TESTING.md) (how these rules are verified) and
[`../CONTRIBUTING.md`](../CONTRIBUTING.md) (how the code is written).

---

## 1. The shape

```
src/Bearing.Core/     analysis      workspace load, walkers, the structure model,
                                    graph algorithms, classification, policy
src/Bearing.Cli/      presentation  argument parsing, terminal output, HTML report,
                                    CSV and JSON writers.  The only project that packs.
tests/Bearing.Tests/  verification  one project, over Core and Cli
tests/TestBed/        fixture       a synthetic solution with known answers
```

Dependencies run one way: `Cli → Core`.

**There was a fourth entry here until `TASKS.md` R2.** `oracle/ArchProbe/` held the original
probe, frozen verbatim, and the test project referenced it so that every number Core computed
could be diffed against the implementation it replaced. It is gone, and §9 records what it was
for and what replaced each job it did.

**The product is two jobs, and they pull in different directions** (`PRD-free-tier.md` §3).
Job A — dependency graph, architecture diagram, integration map, dead code, dead projects,
circular references — is the distribution mechanism: what gets found, run and screenshotted.
Job B — the risk findings — is why anyone runs it twice. Architecturally they are not peers.
**Job A is renderers over the structure model (§4). Job B is claims derived from it.** A
finding model sits above the structure model and is deferred (`TECHREQ-job-a.md` §1) until
the HTML report's findings pane specifies what a finding has to carry — by then the renderer
will have said what that is, which beats guessing now. That deferral is **contested**:
`TECHREQ-job-b.md` §7 argues Job B's suppression rules need finding identity before any
renderer exists, since a suppression is a relationship between findings about one component
and cannot be expressed against output that has already been written.

**What exists today, stated plainly.** Extraction is done. `Bearing.Core` holds the walk, the
structure model, the finding layer and the suppression matrix; `Bearing.Cli` holds every
renderer. This document specified what Core must be before Core was anything, and it is now
mostly a description rather than a specification — where the two disagree, read `src/` and fix
this file.

**The probe is retired and nothing should be read back out of it.** It was a throwaway whose
accumulators were one evening's convenience, and §4 was a different and larger shape on
purpose.

## 2. The invariants

Eight, from `PRD-free-tier.md` §6. They are reproduced here because the rest of this document
cites them by number, and a constraint a reader cannot resolve is not a constraint. They are
acceptance criteria rather than preferences — each was learned by shipping the opposite and
watching it produce confident, plausible, wrong output. `TESTING.md` §7 holds the tests.

| # | Invariant | What it constrains here |
|---|---|---|
| 1 | Every normalized measure needs an absolute floor beside it | the model carries raw magnitude next to every ratio and percentile; a renderer cannot restore what the model discarded |
| 2 | Anomaly, not roll-call | collapsing N findings into one summary line is a decision about meaning, so it belongs in Core and not in a writer |
| 3 | Never contradict yourself about one component | findings suppress each other, so no renderer may emit one in isolation |
| 4 | Never imply safety at a boundary | §8 — this one constrains the code, not just the wording |
| 5 | Interpretation first, math as receipts | ordering is presentation; that a receipt exists at all is the model's job |
| 6 | Blank, never fake | §3's corollary — the rule belongs in the model, and today it lives in the CSV writer |
| 7 | Name the specifics | the model retains the named participants of a claim, not only its magnitude |
| 8 | State the coverage | §7 — the model has to carry coverage data or no renderer can report it |

Invariant 1 failed **five separate ways in a single build session**: ties ranking at the
100th percentile; a cohort of one reporting as exactly median; "top 86% by fan-in" on a type
with zero callers; instability 0 on a component with one dependent; a peer median of zero
making every non-zero value infinite. That is why it is first, and why `TESTING.md` §8 turns
it into a standing review question rather than trusting anyone to remember it.

Four — 2, 4, 6 and 8 — are the ones the current phase can violate, and each has a test. The
rest bind the finding layer, which is not built.

## 3. Core computes. Cli renders.

**The single most important rule in this repository.**

`Bearing.Core` produces data. `Bearing.Cli` turns data into something a human reads.
Nothing in Core decides what anything looks like; nothing in Cli decides what anything
means.

This is not an aesthetic preference. The probe's `Report.cs` is 997 of its 2,515 lines, and
the reason Bearing had to be started rather than continued is that in those 997 lines the
interpretation and the formatting are the same statement. `ComputeCohortStats` — cohort
sizes and percentiles, the substrate of every finding the tool makes — lives inside the
renderer to this day. So do the per-project Martin metrics. The test fixture has to call
`Report.ComputeCohortStats` by hand or every cohort reading comes back zero.

Nobody decided that. It accumulated one reasonable-looking line at a time.

So the rule is enforced mechanically, in `tests/Bearing.Tests/SeamTests.cs`:

| Enforced | How |
|---|---|
| Core references no `System.Console` | type references in the compiled assembly |
| Core does not depend on Cli | assembly references, plus the `ProjectReference` items |
| The check is not vacuous | the assembly exists and has type references at all |

The forbidden-type list is a table in that file with a sentence per entry explaining what
including it would mean. Add to it when a new way to leak presentation into Core turns up.

**What this buys.** Every Job A deliverable — terminal output, JSON, the HTML report, the
architecture diagram, the dependency graph — is a renderer over one model rather than five
parallel re-implementations of the same sentences. And it makes the model testable without
asserting on prose, which matters because the prose is the part most likely to change.

### The corollary that is easy to miss

A rule enforced in a renderer is a rule that does not exist.

Invariant 6 says *blank, never fake* — a statistic with no meaningful basis is emitted
empty. A cohort of one holds `CyclomaticPctl = 50` in the model, because midrank puts a
single element tying with itself at exactly the median. Only the CSV writer blanks it.
Every other renderer — JSON, HTML, graph tooltips — will emit `50`, and the most extreme
outlier in a codebase will read as perfectly average.

That is invariant 6 breaking silently in the new output surfaces, and the fix is not to
repeat the rule in each writer. **Rules about what a number means belong in the model.**
`StructureTests.A_cohort_of_one_is_rendered_blank_never_as_a_percentile` currently pins
both halves — the model's `50` and the CSV's blank — so the day the rule moves, the test
says so.

## 4. The structure model

The one object every Job A deliverable renders from, and the thing §9 commits to serializing.
Specified in `TECHREQ-job-a.md` §3 and restated here because it is an architectural
commitment rather than an implementation detail: five deliverables, the JSON output and the
paid-tier seam all read it.

It is deliberately **not** the finding model. This is the substrate — smaller, far less
contentious, and buildable now.

```
Solution
  ├─ analysis metadata    tool version, policy version, timestamp, target path
  ├─ coverage             exclusions applied, projects that failed to load, types dropped
  ├─ Project[]            name, path, output kind (exe/lib/test/apihost), refs,
  │                       Ca, Ce, A, I, D
  ├─ Namespace[]          name, member types, inter-namespace edges
  ├─ Type[]               id (assembly + FQN), name, ns, project, file, line, keyword,
  │                       accessibility, kind + why, members, external namespaces, metrics
  ├─ Member[]             signature, file, line, cc, nesting, dsm, params
  ├─ Edge[]               from, to, weight, kind, site
  └─ ExternalDependency[] namespace, category, types touching it
```

### Three fields to collect during the walk, not after — **all three collected**

Each is nearly free inside the existing pass and costs a second full traversal of the
solution to reconstruct. Deciding late is the expensive option.

`SolutionWalker` collects all three. `Edge` carries every individual `TypeReference` with its
kind and site rather than only a weight, so the weight is now a count of things that each know
where they came from.

- **`Edge.kind`** — inheritance, interface implementation, field, constructor parameter,
  method call, generic argument, attribute. Without it the only filter a dependency-graph UI
  can offer is edge weight, which is the least interesting one available. Hiding abstraction
  and data-contract edges is what makes a DIP-heavy codebase readable at all.
- **`Edge.site`** — file and line for at least one representative reference per edge. This
  is what makes *"who actually calls this"* clickable rather than a claim.
- **`Type.kind` + why** — §6. Store `attribute:ApiController`, `base:DbContext`,
  `external-ns:Azure.Messaging` beside the value, never the value alone.

The taxonomy for `Edge.kind` is still open — §11.

### Project metrics are model data — **moved**

Ca, Ce, A, I and D were computed inside the oracle's print routine and never modelled. So were
the cohort statistics — sizes, percentiles and multiples of the peer median, the substrate of
every Job B claim. Both were §3's failure mode in its purest form, and moving them is what phase
1 actually is (`TECHREQ-job-a.md` §4).

Both now live in Core, as `Distribution` and `ProjectCoupling`, with `CyclesAndCouplingTests`
holding them to the probe's numbers on the fixture. The probe still computes its own copies —
it is the oracle, and it stays verbatim — so this is the reimplementation existing and agreeing,
not the probe delegating.

Cohort assignment followed, as `CohortSet` and `CohortCandidates`. It is the substrate beneath
the substrate: `Distribution` is computed *over* a peer group, so an error here changes what
every finding compares against without breaking a single condition. Split so that deriving
candidates — which needs to know what a type is — is separate from choosing between them, which
does not. The choosing half is pure, which is what makes stranding and starvation testable
directly rather than through a solution that happens to exhibit them.

`SolutionWalker` then closed the loop: Core loads the solution, builds the model, and assigns
cohorts as part of the walk, so a `TypeNode` carries its peer group and its size. Every type in
the fixture lands in the same group the probe put it in.

The corollary in §3 is discharged along with it. `Distribution.Read` returns nothing for a group
of fewer than two rather than the arithmetically-correct-and-meaningless 50, and
`ProjectCoupling.Instability` is null where there is no cross-project coupling. Invariant 6 now
holds in the model, so a renderer cannot miss it by omission.

### Two properties extraction must not lose

From `TECHREQ-job-a.md` §2, called out because they are cheap to keep and expensive to add
back:

- **Source location per type and per member.** Every clickable artifact depends on it.
- **Single-pass edge collection** — one walk yielding fan-in and fan-out together, rather
  than N× `FindReferencesAsync`. This reads like an implementation detail and is not one: it
  sets what a run costs on a large solution, and success metric #4 is time-to-first-finding
  under 60 seconds cold (`PRD-free-tier.md` §9).

### No composite, anywhere

`PRD-free-tier.md` §5 forbids ever displaying a composite score — not on a dashboard, not in
a tooltip, not as a CSV column, permanently. The model therefore does not carry one either.
This is a derived constraint rather than a quoted one, and the reason is mechanical: a field
that exists gets rendered eventually, and the argument for adding it will always be made
about the model rather than about the display.

### The finding identity key — settled; the finding record is deferred but no longer empty

The model above is not the finding model, and that deferral still holds for the full record:
the HTML findings pane will say what a finding must carry, and that beats guessing. But the
**identity** half could not wait, because three things need it before any renderer exists.

- **Suppression is a relationship between findings.** "Breaks alone is suppressed for anything
  already nominated as a concealed decision" cannot be expressed against output that has
  already been written. Today it works by capturing nominations earlier in the same method and
  testing membership later, which makes renderer ordering load-bearing — reorder the renderer
  and invariant 3 breaks silently, producing *more* output, which reads as a working tool.
- **Acknowledgment memory** needs "known and fine" to attach to something that is still the
  same thing next run.
- **A re-run is only informative if a finding can be new**, and new is a comparison.

The key is **`(finding kind, subject)`** and nothing else. `FindingKey` and `SubjectRef` in
`Bearing.Core`.

The subject is not always a type — concealed decision is nominated at type level *and* method
level, coverage is about the solution, and a cycle is about its members jointly — so a subject
is one of: type, member, project, namespace, set, or solution. **A type is keyed by
`(assembly, fully-qualified name)`, never by name alone**; see §10.

**What the key excludes is the point.** File, line, metric values, threshold values, rank and
position under `--top` are all out, because each moves when nothing meaningful changed, and any
of them would discard an acknowledgment for a reason the user would not recognise as one.

Two consequences, recorded rather than hidden:

- **Magnitude is excluded.** Acknowledge a god object and it stays acknowledged if it doubles
  in size. Banding severity into the key would make identity depend on a threshold, so a
  retune would invalidate every stored acknowledgment and a subject on a band edge would
  re-alert every run. Escalation is better served by storing the metrics *beside* the
  acknowledgment and deciding the rule later — which this key does not foreclose.
- **A rename produces a new key**, so the acknowledgment is lost and the finding returns as
  new. Right for drift, which should surface renames as events; slightly wrong for
  acknowledgment memory. It is the price of not building rename detection now.

**A method-level concealed decision suppresses breaks-alone on its declaring type.** The
suppression matrix says "already nominated as a concealed decision" without saying whether a
nomination on one of the type's methods counts. It does. The reason the suppression exists is
that structural isolation is not safety when a component *decides* something — a normalizer
that picks the wrong option propagates into the data going out the door rather than through the
call graph — and that argument is about behaviour, which lives in methods. Which level happened
to nominate it does not change whether the decision is there. `SubjectRef` walks member →
declaring type for exactly this, and `FindingSet.ContainsAbout` is the query that expresses it.

### What a finding carries, and what is still deferred

The record stayed deferred; three parts of it could not, because the first two findings to move
needed them and none is a guess about what the HTML pane will want.

| On `Finding` | Why it exists now |
|---|---|
| `Receipts` | §6 — a claim whose basis is not available is worthless even when it is correct. Each names the `AnalysisPolicy` value it was tested against rather than copying the number, so a finding and the policy cannot disagree about what gated it, and the mapping is checkable |
| `Qualifiers` | §4 row 6 suppresses a *sentence* rather than a finding, and there was no model surface carrying the distinction — so the only thing testable was the probe's prose. Core decides whether the qualifying fact holds; Cli decides the words |
| `Participants` | invariant 7 — the model retains the named participants of a claim, not only its magnitude |

Severity, rank and position stay out, for the reasons the key excludes them.

### What the HTML pane said about the record — A6

`TECHREQ-job-a.md` §6 defers the full record until the findings pane can say what it needs. It has
now been built, and the answer is **three observations and one constraint**, not a list of missing
fields.

**The record needs nothing added today.** Location, cohort and metrics are all reachable by looking
the subject up in the model, which the renderer has anyway — so keeping them off the finding was
right, and the key's exclusions cost the pane nothing.

**Excluding severity was right, and it is visible.** With no rank there is no honest global order,
so the pane groups by kind and *says* the findings are not ranked. A list rendered top-to-bottom
is read as ranked whatever the model believes, so the alternative was not "no order" but "an
invented one" — a renderer manufacturing the judgement Core refused to make.

**`Participants` is untyped, and one generic renderer is what exposes it.** Across the eleven kinds
the list holds at least four unrelated relationships: the *dependencies that make the span*, the
*members that write to static state*, the *callers* a defect or a change reaches, and the *most
complex member*. The terminal never noticed because it writes a bespoke sentence per section; a
pane that renders every kind through one path cannot avoid it, and rendering them all as
*"Names: …"* is wrong in a specific way — for a god object nominated on the **size** arm the named
member exists to show there is *no* method carrying the weight, and listing it the way a dependency
set is listed says the opposite.

> **The constraint, written down because breaking it is what forces the change:** *every finding
> kind carries exactly one participant relationship.* While that holds, the relationship is a
> function of the kind and a label in the renderer is complete — which is what `HtmlReport`
> does, and it keeps words in the Cli where §3 wants them. **The day one kind carries two** — a hub
> naming both its callers and its worst method — a label cannot express it, and
> `Participant(Subject, Role)` becomes necessary in Core. That is a model change touching every
> detector, so it should be made deliberately and not discovered.

**One thing to clean up rather than a finding:** kind → title and kind → explanation now exist
twice in the Cli, once in `FindingSections` as section headers and once in `HtmlReport`. Both are
words and both belong in the Cli, so §3 is not violated — but they will drift, and the next
renderer should read from one copy.

**Detection and suppression are separate passes** (`Analysis`). Every detector sees the model
and nothing else, so no detector can depend on having run after another one; relationships
between findings are resolved afterwards against the whole `FindingSet`. In the probe they are
resolved by where the code sits in a 1,066-line method, which is what makes reordering it break
invariant 3 without failing anything.

**Core does not truncate.** `Top` is a display cap and applying it in the model leaves every
renderer unable to say how much it is not showing. It also silently weakens
suppression: in the probe the set breaks-alone tests membership against is the truncated one, so
a type nominated below the cap suppresses nothing.

## 5. Analysis is a function, not a process

Core's entry point is conceptually `(solution, policy) → model`. Same inputs, same output,
every time.

- **Take inputs as arguments.** Not from ambient state, not from the environment, not from
  the entry assembly. `ToolInfo.ReadVersion(Assembly)` takes the assembly rather than
  calling `Assembly.GetEntryAssembly()` — under a test host the entry assembly is the test
  runner, which would make it untestable in exactly the place it needs a test. It is four
  lines long and it still follows the rule, because the rule is only worth anything if it
  is not negotiated case by case.
- **No culture-dependent formatting or comparison.** Enforced as build errors
  (CA1304/CA1305/CA1307/CA1309/CA1310, see `.editorconfig`). Output is compared
  byte-for-byte against a stored baseline; a machine that renders `3.5` as `3,5` produces a
  diff that looks like a behaviour change and is not.
- **String matching on namespaces is by segment, never by depth.** `System.Net.Http` was
  once truncated to `System.Net`, and an `HttpClient` gateway was therefore never flagged
  as a boundary at all. Pinned by
  `StructureTests.External_namespace_is_not_truncated_to_a_fixed_depth`.

## 6. Everything a finding rests on must be inspectable

The tool's product is claims about someone's codebase. The first thing a user does with a
claim that looks wrong is ask why it was made, and if the answer is not available the claim
is worthless even when it is correct.

- **`Kind` carries its reason.** `ApiBoundary` / `DataAccess` / `ExternalCall` / `Contract`
  / `Internal` is a heuristic over attributes, base types and referenced namespaces, and it
  is load-bearing: cohort assignment, layer-span, effective fan-out, the boundary section
  and the integration map all depend on it. A misclassification does not produce an error,
  it produces a confident wrong finding. Store `attribute:ApiController`,
  `base:DbContext`, `external-ns:Azure.Messaging` next to the value, not just the value.
- **Classification data is declarative, not a switch statement.** Which external namespaces
  are plumbing and which are integrations is a long tail that otherwise accretes forever in
  one method. It belongs in a data file that can be read, diffed and extended.
- **Thresholds are a named, versioned policy object.** `--min-fan-in 5`, `--high-cc 10`,
  `--outlier-factor 3.0`, `--min-decision-cc 5`, `--min-tangle 4` are judgment calls;
  several were arrived at by watching one specific false positive, and the reasoning
  currently survives only in prose. A team must be able to see which policy produced a
  finding, and changing a threshold must be a reviewable event rather than a silent
  behaviour change between releases.

## 7. Coverage is part of the output, not a footnote

Silence must never read as a clean bill of health.

Every view states what it stayed silent about: excluded generated code, peerless
components, projects that failed to load. This is invariant 8, and it is an architectural
constraint rather than a reporting one — the model has to carry coverage data or no
renderer can report it.

**Partial loads are a hard failure for Job A.** A project that fails to restore or load
understates fan-in *everywhere it is referenced*. The probe prints a warning count and
continues. That is survivable for a one-shot report where a human reads the warning; it is
not survivable for a dependency graph or dead-code detection, where the result is
confidently wrong in a way nothing downstream can detect. `StructureTests` asserts the
fixture loads with zero warnings and zero skipped projects, so the distinction stays
visible.

## 8. Never imply safety at a boundary

The tool cannot see external consumers. It marks the boundary as unseeable and says so.

This is invariant 4, it constrains the code and not just the wording, and it is the one
whose violation would do real damage: a tool that says *"safe to remove, two internal
references"* about a field six customers depend on has caused the burn it claimed to
prevent.

Concretely, for dead-code detection: static fan-in of zero also describes DI-registered
services, types resolved by reflection or serialization, the public API surface of a
library, entry points, ORM materialization, convention-wired handlers, polymorphic-only
implementations, and — because test projects are excluded from metrics — anything used only
by tests. The output never says "dead". It says *"no static references found — verify
before deleting"*, and it names each category it could not rule out.

Note the same false positive appears in our own build: `.editorconfig` disables CA1812
(uninstantiated internal class), because it misfires on types constructed only by the test
host. Shipping a rule we tell users is unreliable, while relying on it ourselves, is not a
position worth defending.

## 9. Serialization is a contract from the first release

The structure model (§4) serializes to JSON. It carries a `schemaVersion` from v0.1 — cheap
now, a breaking change later.

This is the free tool's own output format **first** and the paid-tier seam second. Do not
let the seam design drive it. It may ship documented-as-unstable; that is a separate
decision from whether it is versioned.

## 10. Decisions taken

| Decision | Where |
|---|---|
| Licence is Apache-2.0 | patent grant, plus the trademark clause over the Bearing / Iron Marten names |
| Core and Cli are separate projects | this document, §3 |
| Core is not published as a library | `Bearing.Core.csproj` — its API has no stable shape yet |
| The probe was kept verbatim as a diff oracle, then retired at R2 | this document, §1 — it had verified P7 and P8 on the day it went, and what it proved is recorded in `docs/TESTING.md` rather than left to be re-derived |
| Conventions are build errors, not warnings | `Directory.Build.props` |
| The package ships Roslyn and is ~13MB, and that is accepted | Z2, measured 2026-08-22. **Bearing's own code is 0.29MB of a 13.4MB package — 2.2%.** Roslyn is 7.6MB, MSBuild 1.4MB, `System.*` 1.0MB, and the two MSBuild build hosts 2.3MB between them. There is no lever worth pulling: the only separable 1.3MB is `BuildHost-net472`, which is what loads a non-SDK-style project on Windows, and trading legacy-solution support for a tenth of a one-time download is a bad trade for a tool whose first act is opening a stranger's solution. `dotnet tool install` pays it once |
| Publishing is trusted publishing, from `release.yml` alone | Z1. The API key was deleted when the prefix was granted, and NuGet now caps new keys at 30 days and expires pre-2026-08-17 keys on 2026-11-01, so OIDC is the only durable route. `ci.yml` packs and has no `id-token` permission; `release.yml` is manual-dispatch only, because every automatic trigger is a way to publish something nobody meant to |
| A finding is identified by `(kind, subject)` and nothing else | §4, `FindingKey` |
| The layer-span floor stays at 3 | X4, taken 2026-08-20 — §11 carries the distribution. At 2 the finding is 2.9% of nopCommerce against 0.2%, and the 57 types it would admit are mostly controller-to-repository. Rarest-first selection needs it rare |
| Comparative gates rank; absolute gates stay absolute and say why | X13, taken 2026-08-20. A rank gate always fires — every codebase has a top 5% — so it can never report a healthy codebase. jellyfin has 1.5x fewer types than nopCommerce and nearly as many hubs; the absolute gate says so and a percentile gate would erase it |
| A projection takes what it reads, never the whole model | the structural review after R2. `SolutionModel` memoises seven projections and calls each one; four of them took the model back, which made `SolutionModel` <-> projection a two-cycle each and the four of them one five-type tangle through the hub. `ProjectCoupling` and `ProjectReachability` had already chosen the other way, for testability — a projection that takes a model cannot be tested without a workspace load, because the model's constructor is internal |
| A type is identified by `(assembly, FQN)`, never by name alone | §4, `SubjectRef` — .NET permits one FQN in two assemblies and plugin architectures use it deliberately; keying on the name merges the rows and sums their metrics |
| The SDK is pinned | `global.json` — an unpinned toolchain picks the newest SDK on the machine, which made CI build a net8.0 project with .NET 10 and fail on rules no developer machine had |
| Both graph artifacts are static | neither view that proved legible on a real solution needs a layout engine, so elkjs / cytoscape / d3-force come off the critical path. The only view that did need one should not ship: a two-hop ego view pulls in 24–41% of the codebase from an ordinary seed |
| `EdgeKind` is a fixed enum, not an open set | decided when the walkers moved. The set is closed by the language — there is a finite number of syntactic ways one type can name another. An open taxonomy pushes the cost onto every renderer, which then has to decide what to do with a kind it has never seen, and in practice shows everything: the failure the filter exists to prevent. Adding a member later is a compatible change |
| A type is identified in the model by `SubjectRef`, so the FQN collision no longer merges | The remedy for name-only identity, and the one behaviour extraction is permitted to change. TestBed plants the collision so the fix is observable rather than asserted |
| Every threshold is a named value on `AnalysisPolicy`, including the thirteen that were literals | a policy carrying ten of twenty-three misrepresents which policy produced a finding, which is the failure it exists to prevent |
| `StableThreshold` and `IsolatedThreshold` are independent | the defaults are 0.2 and 0.8 and the symmetry is coincidence, not maintained. They gate different findings over different populations; deriving one from the other would make one flag move two findings |
| A method-level concealed decision suppresses breaks-alone on its declaring type | the reason the suppression exists is behavioural, and behaviour lives in methods — so the level that *nominated* it is not what decides. `SubjectRef` walks member → declaring type to express it. Implemented: `Suppression.Rules`, `breaks-alone-decides-something`, asking `ContainsAbout` at both levels |
| A renderer is handed the judgement, and takes its population from it | Answered by A10, 2026-08-26 — the entry that had to touch the seam. A renderer's population and its reported-or-withheld decision come from `Judgement`; the model is read only for display detail, looked up by subject. Both circular-reference sections took their population from `model.ShapedNamespaceCycles` and split it on `ShapedCycle.IsReportable`, which is a renderer re-deciding from the shape what the matrix had decided over the whole finding set. They agreed because both cycle rows happen to test that shape, and nothing held them to it. Acknowledgment memory is what turned that from renderer drift waiting to happen into a defect: a population re-derived from the model cannot see the user's file, so those sections would have kept printing a finding the user had dismissed, with nothing failing. `JudgementSeamTests` withholds a claim for a reason no shape carries |
| The *mutually dependent, not reported* list is driven by suppression, not by everything withheld | A10, and the two sets were identical until it shipped. That list is the tool explaining its own withholding, so it names each component and the row that set it aside. A finding the **user** marked known and fine has no place on it — listing that component by name under a heading is re-flagging it, which is what acknowledgment memory exists to stop. It is still disclosed, by count, in its own section |
| Every emitted artifact is ordered by a total key | a stable sort on a non-total key reproduces on one machine without being a property of the tool, and Core is a reimplementation that will not inherit the probe's enumeration order. `TESTING.md` §5 |
| A finding carries receipts, qualifiers and participants; the rest of the record stays deferred | §4 — each of the three is load-bearing for a finding that has already moved, and none anticipates the findings pane |
| Detection and suppression are separate passes over the whole finding set | §4 — a detector that can only be correct if it runs after another one is the probe's ordering dependence with different syntax |
| Core emits every finding; `Top` is applied by the renderer | §4 — a truncating model cannot disclose what it dropped, and in the probe truncation silently weakens suppression |
| A member's kind is model data | §4 — the findings do not all read the same population, and "has an executable body" is a different set from "is a method or constructor" |
| `BUG BLAST RADIUS` gates on midrank **position** within the cohort, not on a percentile | `FanInPctl >= 95` is unsatisfiable below a cohort of ten while `MinCohort` admits five. Rank and percentile are one statistic (`rank = n(100−pctl)/100 + 0.5`), so `rank <= max(1, 0.05n + 0.5)` admits exactly what the percentile admitted wherever it was satisfiable — the repair is the floor of 1, not a retune, and no golden moved. It stays a *fraction* because a percentile-within-cohort gate self-limits, which is why this finding held at ~1% on two unrelated real solutions where absolute gates ran to 4–7% |
| `BUG BLAST RADIUS` and `LOAD-BEARING AND INTRICATE` are two findings, and neither suppresses the other | `PRD-free-tier.md` §7.2 — they overlap on "widely depended on and complex" and diverge on the claim: how far a defect propagates, judged against peers, versus how insulated a type is, judged in absolute terms with no cohort. `ShipmentLedger` is nominated as both on the fixture, and unlike breaks-alone and concealed decision that is not a contradiction |
| The suppression matrix is a table in code, and rows that are subject conditions live there too | §4, `Suppression.Rules` — §4 names suppression as the part most likely to be lost in extraction and least likely to fail loudly when it is. Two of breaks-alone's three rows are conditions on the subject rather than relationships between findings, and they are still rows: a reader checking seven rows against the code should find seven rules, not four rules and three conditions inlined into detectors. Which row silenced a finding is reported, not discarded — a finding removed for the wrong reason is indistinguishable from one removed for the right one, from the surviving set alone |
| A layering pattern is a shared **dependency set**, not a shared kind signature | §3.1 says the named dependencies per kind are the finding rather than the count, so the names are also what makes two findings the same finding. Grouping on the count let four boilerplate controllers absorb the middleware that is the section's own worked example. The nomination set does not move, only which subjects may have their detail collapsed, which is why the collapse is a qualifier and not a suppression row |
| §3.8's disjunction is carried as two independent qualifying facts, not one boolean | The arms name two different dangers, and one sentence for both is false by construction on the size arm, which exists precisely for types with bulk and no logic. A renderer picks the sentence; Core does not let it pick the wrong one |
| **`CHANGE COST` gates on a share of the whole solution, beside its absolute floor — and the floor is `MinFanIn`, never `MinCohort`** | Decision X2, and the absolute gate that did not travel. It was the worst-saturating finding measured (7.9% of nopCommerce, 252 candidates, moving the wrong way as codebases grow) and the only fix for that is a proportional gate; retuning an absolute one cannot hold across codebases. **Solution-wide rather than per-cohort is a choice of reader**: within-cohort answers "which controller is riskiest to change", which serves a maintainer who knows the code, and solution-wide answers "which part of this application is riskiest", which is what someone new to it is asking — and it is what §3.5 was written for, running over all types so a lone contract with no peers is not silenced. The constant is derived from the measured eligible share and blast radius' demonstrated rate, not tuned to the fixture, and its *insensitivity* is asserted rather than hoped: the nominated set is identical at 0.05, 0.10 and 0.15 |
| **The fixture may gain boundaries. `WIDEST CONTRACT SURFACE`'s suppression is not at risk from any boundary count** | Decision X1, and it withdraws a constraint rather than adding one. Three plants recorded "no new `ApiBoundary` or `ExternalCall` type" with two contradictory reasons — that ten boundaries makes row 5 reachable, and that ten stops it being reachable. The measurement proves it unreachable at *every* count, and its pin is a synthetic proof over the distributions that maximise the qualifying set; the fixture enters it only as a literal count. The constraint was protecting against something that cannot happen, and it was blocking the change-cost plant, P4 and F9 |
| **The CSV export is `members.csv`, not the probe's `methods.csv`** | A5. Core's model carries every member a type declares, and `TypeNode.Cyclomatic` is the sum over all of them — so a file holding only the method-like ones would not add up to the type row beside it, and a reader checking one against the other would find a discrepancy with no explanation in either file. The `Kind` column is how somebody who wants exactly the probe's population gets it. **What the file deliberately does not carry is the probe's cohort statistics** — `FanInPctl`, `FanInXMedian` and eleven more, computed inside its report renderer at print time, which is the entanglement extraction exists to undo. Core has `Distribution` and could offer them as a model projection; it does not, and A5 is scoped to what the model already holds. That is a capability the free tool **loses when the oracle retires at R2**, so it wants deciding before then rather than noticing after |
| **The JSON schema is written out by hand, not reflected off the model** | A4, §9. Serialising `SolutionModel` directly would publish every property of every Core type the moment it was added, in whatever order the compiler emitted — so an internal rename becomes a breaking change nobody noticed making. `JsonOutput`'s private records **are** the schema, and changing one is a visible edit to a file whose only purpose is to be that. `schemaVersion` is carried from the first release and is deliberately **not** the tool's version: the tool ships far more often than this shape moves, and a consumer pinning on the tool version would re-pin every release for nothing. Whether the schema is a *public contract* was §11's question; the row below answers it |
| **The JSON schema is versioned, and not a public contract before 1.0** | X6. `schemaVersion` ships at `1.0` and moves when a consumer would have to change, independent of the tool's version — that machinery is what makes instability **safe**, not what makes the shape stable, and the two get confused. **The tool is `0.0.1-preview.1` and has no known consumer**, so a contract now would be a promise about needs nobody has stated. **A9 has not shipped**: the one remaining §7.1 deliverable adds fields to this file, so freezing the shape now would freeze it immediately before the thing most likely to move it. `PRD-free-tier.md` §7.3 gives the paid tier's persistence role to **CSV**, not JSON, so nothing downstream is owed a promise yet. **The asymmetry decides it**: unstable → stable is always available and costs nothing, stable → unstable is a breach. Documented as unstable in `README.md`, and promoted at 1.0 if a consumer exists to want it |
| **The thirteen cohort statistics are a projection on the model, and the exports read it** | X9. The probe computed them inside its *renderer* at print time, so nothing but the printer could see them; Bearing's model holds them now and `--csv` and `--json` read the same projection — `SolutionModel.Statistics`, lazily, the way `ProjectCouplings` already worked. **A projection rather than new analysis**: every value is a reading off `Distribution`, which the detectors already use, and `CohortStatisticsTests` asserts a blast-radius finding's receipts and the export cannot disagree about the same number. **The deadline was R2**, because the probe is what made them checkable — this is the one capability the free tool would have given up without noticing. **Two deliberate narrowings against the probe, both invariant 6**: readings are blank below `MinCohort` rather than only at a cohort of one, because a percentile over two peers is arithmetic rather than a comparison and the report already says so in words; and an undefined multiple — a median of zero — is blank where the probe writes `inf`, because the point is that infinity is a missing measurement and every tool that opens a CSV sorts it to the top of the column as though it were the largest. The percentile survives there and carries the reading. The two solution-wide readings are never blank, because the solution is always a population: they are what a peerless type still gets |
| **Change cost keeps one reading, and it is the newcomer's** | X8, closing the half X2 left. Within-cohort answers *"which controller is riskiest to change"* — a maintainer's question, for someone who already knows the codebase. Solution-wide answers *"which part of this application is riskiest"*, which is `PRD-free-tier.md` §2's reader. **The maintainer's view is a second nomination set over the same measurement**, so shipping both puts one type on the page twice under two readings — §9's anti-metric, and the roll-call failure every collapse in this build has been undoing. Not a renderer flag, and not free tier. **The split does not generalise, and that is the part worth keeping**: change cost is the only finding whose claim does not name peers, which is the whole reason its population was a free choice. Blast radius (*"widely depended on **relative to its peers**"*) and concealed decision (*"top N% **among its peers**"*) carry the cohort inside the claim — remove it and there is no finding left. Hubs is cohort-free: absolute counts that mean the same with or without peers |
| **The tool version is an input to the analysis, not something Core looks up** | `SolutionModel.ToolVersion` used to read `typeof(SolutionModel).Assembly` — `Bearing.Core`, which sets no `<Version>` and so reported `1.0.0` against a tool shipping `0.0.1-preview.1`. The version belongs to whatever packs and Core is not it; `GetEntryAssembly` is not the escape, because under a test host it is the runner, which is why `ToolInfo.ReadVersion` takes an assembly at all. `WalkOptions.ToolVersion` defaults to `0.0.0` rather than to Core's own, so a host that does not say reads as "nobody told me" instead of as a release that does not exist — of the two ways to be wrong, only one is visible to the person reading the field |
| **A cycle carries one representative loop beside its membership, and the choice of loop is stated rather than hidden** | A3, `TECHREQ-job-a.md` §5.1. "These six namespaces are entangled" is true and cannot be acted on; `A → B → C → A` names an edge somebody can go and delete. The objection that deferred this — a component holds many cycles, so any one of them is an arbitrary walk presented as *the* cycle — is not withdrawn, it is answered: the walk is the **shortest through the component's ordinal-first member**, computed in the subgraph induced on that component, and `Cycle.PathCoversEveryMember` makes the renderer say when the loop is smaller than the entanglement. A two-name loop under a six-namespace component with nothing said is invariant 4's failure — it reads as "delete this edge and it is gone". All three properties are load-bearing rather than tidy: breadth-first fixes the length, the ordinal seed fixes the start, ordinal neighbour order breaks ties, and a representative that moved between runs would make an acknowledged finding come back as new |
| **Project cycles are a third graph, aggregated from type edges rather than read from project references** | A3, `PRD-free-tier.md` §7.1. "MSBuild forbids them" is true of project *references* and only of those. Bearing builds a type-reference graph and aggregates it, and that cycles whenever two projects each name a type in the other — reachable when an analysed assembly is resolved some way other than a project reference, and usually a layering violation the reference graph is too coarse to see. **Ungated**: unlike a type tangle there is no size at which it becomes ordinary. `Cycles.AmongProjects` takes primitives as well as a model, `ProjectReachability`'s precedent, because no solution that builds normally has one — including the fixture — and a test that could only run against an empty graph would pass by having no case. Note what name-only identity fabricated at exactly this spot: `ProjectCycleTests` pins the defect's answer beside the feature's so a bug report cannot confuse them |
| **Findings are selected, never ranked, and the selection carries no constant** | X10, `PRD-free-tier.md` §4, §5, §9, and A11 round 1. **The findings are risk claims and say so** — §7.2 is `[proven]`, and the sentences already read *"looks like plumbing but is in the top 1% of internal complexity among the 249 types deriving from BaseNopModel"*. What does not exist is an order **across** kinds. The only candidate common unit is extremity within one's own cohort, and §3.6–§3.9 are cohort-free by design — load-bearing, breaks alone, hubs and god objects and shared mutable state all carry *"no cohort required"* in their own headings — so for half the kinds there is no percentile to compare, and a cross-kind order would mean giving those findings a population they were deliberately built not to need. **So: sections order by ascending count on the run; within a kind, by the cohort-relative measure that fired; and the report leads with one exemplar per kind that fired, rarest first, each exemplar being that kind's top row.** Rarity is an *ordering*, never a category — nothing says "this fired rarely", so there is no threshold, no constant, and nothing to drift, and the item count self-scales with how many kinds fired. **The ordering is stated in the text** — *"ordered by how uncommon each kind is in this codebase"* — because a top-down list reads as ranked whatever the model believes, and rarity is not severity. **Rejected: convergence.** Measured on nopCommerce it selects 10 components named by three or more kinds, and they are base classes and DI registration helpers — scaffolding rather than orientation. **Deferred: a solution-wide share cap per kind**, X2's precedent, until **P7** can observe the difference; the same reason the remaining two absolute-gate conversions are parked. The order must be derived at render time **and tested as derived**, or it is a constant wearing a sort's clothing |
| **The report's tiers answer to different metrics, and the picture is judged by the third** | A13. `PRD-free-tier.md` §4 — *"if a number does not end in a sentence someone changes their behaviour over, it does not ship"* — is an **orientation** rule, and applying it to the artifact whose job is to get the tool found would strangle it; §9's third metric is installs and referral share, and it is not measured in behaviour change. So tier 1 makes no claim and is allowed not to, while tiers 2 and 3 answer to §4 exactly as before. **Stated up front rather than discovered**, because the failure of not saying it is `D = 0.42` coming back: a tier with no stated bar gets the loosest one anybody assumes. What tier 1 is *not* exempt from is §8 — no composite, no grade, nothing that reads as one, and `MosaicTests.The_mosaic_carries_no_scores` is the same assertion `ArchitectureDiagramTests` makes for the same reason |
| **The mosaic carries two marks, and the second one exists because the first was measured** | A13 tier 1. Marking every finding-named cell is true cell by cell and false as a picture: findings select large complex components and cell area is lines, so the two correlate hard — **651 of nopCommerce's 3,209 cells, 20% by count, came out 72% of the ink**, and 492 of Jellyfin's 1,545 came out 70%. A picture three-quarters in one alarm colour asserts a verdict over a whole codebase that no finding in it makes, and it is §9's anti-metric — *number of findings; more is worse* — rendered as a wash. **The repair is a third state, not a threshold**: the tint separates by *hue* at equal lightness so the volume stays legible without becoming the foreground, and the strong mark is X10's exemplars, which is 3.0% of the ink on nopCommerce and 8.6% on Jellyfin. Neither mark is a magnitude and neither carries a constant — one is *some finding names this*, the other is a selection whose size is the number of kinds that fired |
| **X10's selection is one function, and both tiers read it rather than a copy** | A13. `Selection.Exemplars` is in `Bearing.Cli` because it is a render-time reading of a finished finding set and Core makes no such choice — but it is the one thing in that assembly which is not *words*, so it sits beside the renderers instead of inside one. The mosaic marks what the findings pane will lead with, from the same call, so the picture and the prose cannot disagree about where a reader should start. X10 requires it be **tested as derived** — an order that is not computed from the run is a constant wearing a sort's clothing — which is what `SelectionTests` asserts, about the rule and not about the fixture's answer |
| **Every finding is worded in one place, and both renderers read it** | A13 tier 2, `Claims`. There were two copies — the terminal's per-section sentences and the page's per-kind blurbs — and tier 2 needed a third reading of the same claims, which is the point at which duplication stops being tolerable and starts guaranteeing drift. **The sentences are the terminal's, unchanged except where they were wrong**: X10's *"the findings are risk claims and tier 2 says so"* is satisfied by moving text a reader already gets, not by writing new text. What stayed in the renderers is layout — the fixed-width row, the card, the caps — because how many lines fit on a screen is a property of a medium. `ClaimsTests` asserts the two artifacts word a lead claim identically, through the rendered output rather than by calling one function twice |
| **Coverage is selected but is not led with as a risk** | A13 tier 2, `Claims.IsRiskClaim`. It is invariant 8's record that a population got no comparative reading, and putting *"no peer group"* in a list headed **risk** asserts something about a type whose entire entry says nothing could be asserted about it. **This does not narrow X10** — `Selection.Exemplars` still returns one exemplar for every kind that fired, coverage included — it decides where a renderer puts the result, which is the distinction the terminal has always drawn by giving coverage its own section. The count is disclosed on the default page in prose, so the shorter report does not buy its brevity with invariant 8 |
| **The report leads with one claim per kind and the enumeration ships behind `--full`** | A13 tiers 2 and 4. A11 round 1 measured the enumeration as *"a wall of text"* — 1,642 findings, 66% of them one kind — and tier 4 already specified that the full report stays behind a flag. Doing it at tier 2 rather than at tier 3 is what makes the page shorter now rather than later: **350KB to 104KB on nopCommerce**, of which 59KB is the mosaic. Nothing is summarised away — `--json` and `--csv` carry every row, `--full` renders the sections, and the default page states all three plus the count it is not showing. A page quietly showing nine findings of 1,642 would be undisclosed truncation at the scale of a whole artifact |
| **The tile row states four claims about the codebase and no census counts** | A13 tier 3, `Tiles`. What shipped was *types / projects / dependencies / findings* — three numbers a reader already knows and one that measures the tool — and the brief's B3 asks which four numbers deserve the biggest glyphs on the page. `PRD-free-tier.md` §4 answers it: a number that does not end in a sentence somebody changes their behaviour over does not earn one. **The rejected fifth is the one worth remembering** — *"findings worth attention"* is a count of outstanding work, which is a lint mental model, and §7.2 holds that an anomaly is an observation rather than a backlog item. So the findings total stays, in prose, in `Everything else`. **No constant anywhere**: widest reach and sharpest outlier are maxima, clean is a share of the whole, and concentration picks the project with the largest **excess** of named types over its proportional share rather than the largest ratio — excesses sum to zero across projects by construction, so a two-type project with two findings cannot outrank a large one carrying thirty more than it should. **A tile the run cannot support is absent, never zero or a dash** (invariant 6). The sharpest-outlier tile is provisional against the peer-group defect and names the quantity it multiplied, because the maximum is taken across quantities and *"sharpest"* alone would be the cross-kind order X10 refuses |
| **The lead finding is one annotated card and the rest are a rail against the same gutter** | A13 tier 3, candidate E. A11 round 1 failed on **comprehension** — participants placed the components correctly and did not know why any of it mattered — so the card labels what each of its own lines is for, once, and the teaching column then carries kind and rank for every other claim. The brief's B1 makes a single finding card the screenshot frame, so it has to stand alone with no page around it; C1 is why a rail row drops the evidence line and the kind's definition. **Every kind's definition survives once, in the per-kind census**, which is where tier 3's within-solution baseline becomes visible: a count of a kind used to exist only beside the row that led it, so a reader could see 103 and never see 3,209. The census is ordered by `Selection.Exemplars` rather than by a second rule, so the page holds one ordering |
| **The report's picture is a plot of projects, and the mosaic sits below the project map** | X11, `ReachPlot`, and it replaced a picture that was measured rather than disliked. Cell area on the mosaic is lines of code while every claim on the page is a count of types, and on nopCommerce **17% of the types are named and they hold 58% of the ink** — so a reader assembling *"which project is dense with findings **and** holds everything else up"* got it wrong three times, each time by reading area exactly as drawn: `Nop.Web` looked worst and is the least dense of the five with 31 dependents, *"almost all of it is red"* is 26%, and `Nop.Web.Framework` — densest at 29%, most depended on at 1,280 — went unmentioned because 235 types is a small tile. **The two quantities are one position now**: reach across, density up, dot area the type count. **The specific thing it must not become is a score** — no zones, no shading, no ramp, no quadrant labels, one `<rect>` on the drawing and it is the ground — because a two-axis picture is how §8's composite arrives as a graphic. **What the mosaic keeps** is the claim no number makes as well, that every analysed type is on the page and most of it is pale; it moves under the project map, which is the picture people ask for, and still ships standalone as `--mosaic` where §9's third metric lives. Labels are placed deterministically and a name that fits nowhere is disclosed beside the picture — nopCommerce places all five, jellyfin places eleven of twenty and lists nine |
| **The profile is always taken and `--profile` only prints it** | A12, `WalkProfile`. A measurement behind a switch describes a run the user did not have, and the instrument is a handful of timestamps per declaration. The stages are the five steps `WalkAsync` is already split into, so a step that stops being its own method stops being its own reading |
| **The profile hangs off the walker, never off the model** | A12. A profile is a fact about one run on one machine; the model is what two runs over one commit are asserted to produce identically, and `docs/TESTING.md` §3 is about what happens to a snapshot suite that learns to re-accept a field that legitimately changes |
| **The profile reconciles, and the residual is a difference rather than a row** | A12, `ProfileReport.For`. Each stage is charged the interval since the last one ended, and whatever the caller did not hand over is printed as `unmeasured`. A caller cannot hide an unmeasured stage by forgetting to add it, which is the only kind of residual worth reading |
| **A member is identified by its documentation comment ID** | X14. The display string it replaced dropped `ref`/`out`/`in`, rendered a static constructor exactly like an instance one, gave an explicit interface implementation the containing type's name, and — for every field, which has no symbol of its own — was not a signature at all. Repairing the format case by case is the curated list §5 and §30 are both about; the doc ID separates all four by construction because the compiler has to |
| **A field declaration is one member per variable** | X14. `int a, b;` is two fields with two names and two sets of callers, and a dead-code claim cannot be made about a member the model never separated from its neighbour |
| **A public field is contract surface for the type that declares it, not only for the types that read it** | X14, and it was one model holding two answers: `ShapeBreadth` counted a public field when measuring somebody else's type while `AccumulateSurface` could not see one at all. Events stay out for the same reason rather than in spite of it — breadth is data crossing a boundary and an event is a callback |
| **An artifact asserts only over the population its own evidence covers — X17** | Taken 2026-08-26, closing three defects together — the wrapped layer, the edge drawn through a box, and the cycle shape read from one arm. All three were held open on *when is a large component a fact about the codebase rather than a finding about it?* — a judgement with no measurement behind it — and none of them needed it. The mistake in each was a drawing or a label describing a set larger than the one it measured, and the evidence's extent is arithmetic: a row boundary is claimed where an edge proves it, an edge is drawn where no other path carries its reachability, and a coupling reading covers the namespaces in its held pairs. **Bound the claim, do not judge the population.** The rejected alternative is on record because it was measured: a width-bounded layering removes §45's wrap by drawing 7 of Jellyfin's 21 boxes deeper than they are, which trades a misstatement a reader can check for one that leaves no trace |
| **The dead-code exclusion keys on visibility alone, never on the project's output kind** | X15, `TECHREQ-job-a.md` §5.6, taken on the measurement. The spec's "library projects" qualifier leaves 0.5% of Jellyfin's members and 10.5% of nopCommerce's — a factor of twenty decided by which projects compile to an exe. Packaging is not a property of the code, and model binding, plugin loading and reflection all reach into an exe |
| **Every exclusion the dead-code pass makes is counted and printed** | A9 layer 2, `NoStaticReferences`. The exclusions remove 98–99% of the members with no inbound reference, so a list shown without them is a number nobody can calibrate. Invariant 8 at the scale of one finding |
| **A type whose data members are mostly unread is one claim, not one per member** | A9 layer 3, and the measurement rejected the obvious rule first: **none** of Jellyfin's forty nominations carried an attribute, because `System.Text.Json` matches on names. The signal is concentration — 17 of `SearchResult`'s 23 against 1 of `MatroskaConstants`' 17 — stated as "more than one" with no threshold, since 74/42/30% is a continuum. Grouped by the renderer, never dropped by Core |
| **A type's only accessible constructor is never a dead-code candidate** | A9 layer 2, taken on the measurement. It is what registration looks like from inside the solution, and it was 24 of nopCommerce's 29 nominations. The precision is in "only" — a container picks one constructor, so a sibling overload nothing calls is a real dead overload and stays. It does not test whether the type is referenced, because convention scanning leaves no reference at all |
| **A static constructor is never a dead-code candidate** | A9 layer 2. C# has no syntax that calls one, so its inbound count is zero in every codebase that compiles — a finding that fires on every instance of a language feature is invariant 1's cry-wolf failure. Measured: eight of nopCommerce's first fifteen nominations |
| **The member reference graph is parallel to the type graph, not a column on it** | A9 layer 1, `MemberReference`. The type graph must not contain a self-edge — it would move fan-in, fan-out, instability and every cycle — and at member level that same reference is the whole question, because a private helper's only caller is usually a sibling on its own type |
| `WIDEST CONTRACT SURFACE` is gated by an absolute count ceiling, not a proportion | A gate phrased as "too large a share" tests a set the median-relative filter has already bounded by that share, so a dispersion test on the same statistic inherits the defect. The section promises to name what stands out, and a count is what bounds a list. It also turns the `Take(5)` from a silent truncation into the gate itself |

## 11. Decisions still open

**None, as of 2026-08-26.** The last one — what a renderer is handed when a finding has been
withheld — was answered by A10 and is recorded in §10, where decisions taken live. Full context for
anything that reopens is in the private `TECHREQ-job-a.md` §10.

A decision here is distinct from a known defect, which is behaviour already understood to be wrong
with a remedy in hand. These are questions with no answer yet, and there are currently none.

**Everything below is answered and kept for its measurement**, which is the reusable part and the
reason these are not simply deleted. Each says what settled it. **A struck-through heading means the
question is closed**; if the section grows an unstruck one, that is a live decision again.


- **~~Whether `SignificantKinds` stays at three.~~ Answered — X4, and the floor stays at 3.** Kept
  here because the measurement is the reusable part. There are exactly three and `--min-kind-span`
  is 3, so every spanning type necessarily carries the same signature: the `GroupBy` in
  `PrintLayerSpan` is written for a generality that cannot occur. Ties into the edge-kind
  taxonomy, and into the collapse — a richer taxonomy would separate the anomaly from the
  boilerplate that currently hides it.

  **Measured 2026-08-20, on both reference solutions, and it separates two claims that had been
  travelling as one.** Kinds reached counting the type's own role, after the classifier fix:

  | kinds reached | nopCommerce (3,209) | jellyfin (1,545) |
  |---|---|---|
  | 3 — the current floor | **5** (0.2%) | **2** (0.1%) |
  | 2 | 88 | 12 |
  | signatures at 2+ | `ApiBoundary+DataAccess` 57, `ApiBoundary+ExternalCall` 31, all three 5 | `ApiBoundary+ExternalCall` 7, `ApiBoundary+DataAccess` 5, all three 2 |

  Reproduces the tool's own counts at the current floor exactly, which is what makes the other
  rows credible — one parameter moved, same method.

  **The signature claim above holds**: every firing type spans all three, on both solutions, so
  nothing can distinguish them by signature. **The claim that the floor cannot discriminate does
  not.** Dropping it to 2 takes nopCommerce from 5 findings to 93 and jellyfin from 2 to 14. It is
  the most consequential filter in the finding, not decoration.

  **So the choice is not "a fourth kind or admit the floor is decoration".** It is whether the
  finding means *"reaches all three layers"* — rare, 0.1–0.2%, and what ships — or *"reaches across
  layers at all"*, which at 2 admits `ApiBoundary+DataAccess`: a boundary reaching straight into
  the database, which is the layering violation most developers would name first. At 2 the
  `GroupBy` also has three real signatures rather than one.

  **A plant cannot settle this and a measurement did**, which is `TASKS.md`'s X4 entry being wrong
  about its own blocker rather than about its subject.
- ~~**Dead code at member level or types only.**~~ **Answered — X5, and it is member level.**
  Kept here for the measurement that decided the shape of the work: every edge endpoint the walk
  records is a type, so this is a change to reference *collection* and not a new detector. Far more
  useful, far more false-positive prone, and both remain true — `TASKS.md` A9 carries the cost.

- **~~What the cohort floor is for, when one number is doing two jobs.~~ Answered — X3, and the parameter is split.** `CohortBasisFloor` owns selection, `MinCohort` keeps sufficiency, both at 5, landed byte-identical. **The second half of the answer — what a thin cohort may be nominated as — is superseded by X16 below**, which found that the question was posed on the wrong variable. `MinCohort` is read in
  two places that pull in opposite directions, and neither can move without the other:

  - **Selection**, `Cohorts.Assign`: `.Where(candidateCounts[c.Key] >= minCohort).OrderBy(c =>
    c.Precedence)` — *the most specific basis that still yields a usable group*. **Lowering it makes
    cohorts finer.**
  - **Gating**, `ConcealedDecision` and `BlastRadius`: `if (peers.Count < policy.MinCohort)
    continue`. **Raising it silences nominations.**

  **Measured 2026-08-21, both reference solutions, defaults unless stated.**

  | | nopCommerce (3,209 types) | Jellyfin (1,545 types) |
  |---|---|---|
  | distinct cohorts | 160 | 164 |
  | types below the floor of 5 | 107 (3.3%) | 132 (8.5%) |
  | …of those, in a cohort of **one** | 38 | 38 |
  | types in cohorts of 100+ | 1,349 (42%) | 102 (6.6%) |
  | breaks-alone findings, uncapped | 27 | 14 |
  | **…in a below-floor cohort — the D10 population** | **0** | **0** |
  | types re-based when the floor moves 5 → 3 | — | **155 of 1,502 (10.3%)** |

  **The cohort floor's defect does not occur on either real solution.** Not one breaks-alone finding sits in
  a below-floor cohort, at any cap. `RoutingDepot` is a TestBed plant, the invariant-3 violation is
  real and pinned, and its incidence in the wild is zero. **So X3 as the board stated it — "what a
  below-floor type may be nominated as" — is asking about the wrong population**: 3–8% of types, a
  third of them cohorts of one, producing no contradictions.

  **The measurement that does matter is the last row.** Moving the floor one notch re-bases a tenth
  of Jellyfin, because the floor decides *specificity*, not just sufficiency. It is also what makes
  the obvious repair unsafe: `ConditionProcessor` leaves breaks alone at a floor of 3 with nothing
  gated — it moves from `ns:MediaBrowser.Model.Dlna` (8 peers) to `suffix:Processor` (3), and three
  peers make cc 80 an outlier. A fix aimed at D10 would have silently changed every percentile in
  the report and been credited with the wrong mechanism.

  **The proposal is to split the parameter and then remove the threshold from the claim.**
  `CohortBasisFloor` for selection and `MinComparableCohort` for gating, both defaulting to 5, so
  the report is byte-identical the day it lands. Then answer the thin-cohort question *by
  construction* rather than by tuning: **let thin cohorts nominate, and degrade the sentence with
  the evidence** — a cohort of three earns *"cc 80 against 3 peers, median 4"* and never *"top
  5%"*. Percentile language needs enough peers for a percentile to mean something; count language
  is honest at any size. That is the move `CohortStatistics` already makes when it blanks below the
  floor, carried into the sentence instead of stopping at silence.

  **The ceiling is the half with real incidence, and the recorded remedy does not
  reach it.** §28 proposes refusing a bare name-suffix basis. nopCommerce's two largest cohorts are
  base-type — `base:BaseNopModel` at 249 and `base:BaseNopEntityModel` at 178 — with
  `suffix:Service` (193) third. A suffix-only rule leaves the worst one standing, so a ceiling
  belongs on candidate count whatever the basis is. 42% of nopCommerce sits in cohorts of 100+
  against 6.6% of Jellyfin, which is also why this looks like a nopCommerce problem from one
  solution and a general one from two.

- **How a cohort-relative claim is gated — X16.** Ten of the forty-two entries in
  the closed defects are one design decision failing repeatedly: absolute gates do not
  travel, §9 a floor doing two jobs, §10 the same, §14 an unsatisfiable percentile floor, §17 a peer
  group that is not one, §19 the sentence discarding what would make it true, §28 a ratio against a
  zero median, §33 a gate that fires on a third of what it filters, §34 a cohort of 2,909, §38
  `undefinedx its peer median`. Each was repaired locally and each repair added or moved a
  threshold. **That recurrence is the finding.**

  ~~**Measured 2026-08-21, method cohorts on both reference solutions.**~~ **Re-derived 2026-08-25 on
  three, and the 2026-08-21 table was taken on a population the gate does not read.** It is kept
  below struck through, because the correction is the reusable part.

  | ~~2026-08-21~~ | ~~nopCommerce (139 cohorts)~~ | ~~Jellyfin (156)~~ |
  |---|---|---|
  | ~~peer median is 0 or 1~~ | ~~123 (88%)~~ | ~~127 (81%)~~ |
  | ~~zero dispersion, MAD = 0~~ | ~~104 (74%)~~ | ~~100 (64%)~~ |
  | ~~largest cohort~~ | ~~`suffix:Service` n=2,927~~ | ~~`suffix:Manager` n=1,339~~ |
  | ~~second~~ | ~~`base:BaseNopModel` n=1,656 median 0 MAD 0~~ | ~~`suffix:Info` n=927~~ |

  **The population was wrong.** `ConcealedDecision.AtMethodLevel` reads `IsMethodLike` — `Method` and
  `Constructor` — and drops groups below `MinCohort`. The old table pooled **every** member kind,
  fields and properties included, and applied no floor: its *139 cohorts* for nopCommerce reproduces
  exactly on that pooling and on no other. Fields and properties carry `cc` 0, so the pooling is what
  drove the median to 0 and the MAD to 0. **The gate's own population is 105 cohorts, 70 of them
  above the floor.**

  **Its marquee counterexample does not exist in that population.** `base:BaseNopModel` is quoted as
  *1,656 members with MAD 0*; method-like, it is **n=124, median 1, MAD 0, max 9**. And the largest
  cohort reads `n=2,927` where the shipped binary emits **2,909** — which is the number the register
  §34 carries for the same cohort in the same file.

  **Re-derived 2026-08-25, `tools/cohort-dispersion.py` over `--csv` output, defaults, at or above
  `MinCohort` 5.** nopCommerce is `cloned-realworld/nop` at 2024-08-27, which is what every figure in
  this file has always meant by the name; `nopCurrent` is a different and larger solution.

  | member level — cc of method-like members | nopCommerce (3,209 types) | Jellyfin (1,545) | Umbraco (6,230) |
  |---|---|---|---|
  | cohorts at or above the floor | 70 | 104 | 277 |
  | peer median is 0 or 1 | 58 (83%) | 62 (60%) | 204 (74%) |
  | zero dispersion, MAD = 0 | 43 (61%) | 52 (50%) | 154 (56%) |
  | population living in those cohorts | 1,917 of 9,144 (21%) | 1,665 of 5,454 (31%) | 8,376 of 19,137 (44%) |

  | type level — MaxMemberCyclomatic of types | nopCommerce | Jellyfin | Umbraco |
  |---|---|---|---|
  | cohorts at or above the floor | 95 | 90 | 278 |
  | zero dispersion, MAD = 0 | 62 (65%) | 42 (47%) | 147 (53%) |
  | **population living in those cohorts** | **2,159 of 3,102 (70%)** | 648 of 1,413 (46%) | 2,840 of 6,069 (47%) |
  | what ships produces there | **23 findings** | **17** | **72** |
  | …because the median is 0, so the ratio is undefined | 1,427 (46% of the gated population) | 388 (28%) | 1,636 (27%) |

  **Size does not predict dispersion, and *uncorrelated* is the right word for it.** Share of
  cohorts with MAD 0 by size, against a null that holds the cohort sizes fixed and reshuffles every
  `cc` value across them — `tools/cohort-dispersion.py`, 300 draws:

  | cohort size | nopCommerce | *null* | Jellyfin | *null* | Umbraco | *null* |
  |---|---|---|---|---|---|---|
  | 5–9 | 90% (n=10) | *26%* | 60% (n=25) | *29%* | 66% (n=44) | *47%* |
  | 10–24 | 53% (n=19) | *10%* | 52% (n=31) | *19%* | 57% (n=103) | *44%* |
  | 25–99 | **70%** (n=27) | *2%* | 49% (n=37) | *9%* | 56% (n=90) | *48%* |
  | 100–499 | 45% (n=11) | *0%* | 33% (n=9) | *1%* | 41% (n=32) | *49%* |
  | 500+ | 0% (**n=3**) | *0%* | 0% (**n=2**) | *0%* | 38% (n=8) | *52%* |

  **A first reading of the left-hand columns called this a real but weak correlation. It is not one,
  and the null column is why.** MAD is a median of absolute deviations over a small, discrete,
  heavily zero-inflated variable — at n=5 it is 0 whenever three of five values equal the median,
  which is likely when the median is 0 or 1 and `cc` is a small integer. **So MAD-0 share falls with
  size by arithmetic**: with the relationship destroyed, nopCommerce's null still falls 26% → 0%
  across the buckets. Any observed downward trend is confounded by the estimator and cannot be read
  as a property of peer groups.

  Two other reasons the trend is not there to be read. **It is not monotone** — nopCommerce goes 90%,
  53%, **70%**, 45%. And **the tails are empty**: *no cohort above 500 members has zero dispersion*
  rests on **three cohorts** and **two**. Umbraco is the only solution with a usable 500+ bucket, at
  eight, and it reads 38% rather than 0%.

  **So the original wording stands and its evidence does not.** *A cohort of 3 and a cohort of 1,656
  fail identically* is the right conclusion, reached the wrong way: the counterexample it names is
  from the pooled-all-members population, and what actually supports it is that dispersion is not
  recoverable from size in any bucket with enough cohorts to look at. **Every gate in this family is
  a threshold on a proxy that carries no signal**, which is why each fix holds at the size it was
  measured against and breaks at the next one.

  > **What the null model did establish, and it is not about size.** On nopCommerce and Jellyfin the
  > observed share exceeds the null at every bucket, hugely — **70% against 2%** at size 25–99. Real
  > cohorts are far more homogeneous than random groups of the same size, so **`Cohorts.Assign` is
  > doing real work** and this is the first independent evidence of it.
  >
  > **On Umbraco it is nearly not.** Its null sits at 44–52% at every size, because **50% of its
  > method-like members have `cc` exactly 1 and 64% are ≤ 1**, so a random group of any size is
  > about as likely to have MAD 0 as a real cohort. **That is a live constraint on the gate below**:
  > if MAD = 0 is the trigger for the no-spread branch, on Umbraco it fires about as often on
  > arbitrary groupings as on peer groups, and the branch is not discriminating there. It is the
  > most useful thing this measurement found and it needs answering before the branch is built.

  **The variable is dispersion.** With spread, an outlier is an outlier at any n; without it, nobody
  is, at any n. Stated that way the family collapses into one rule, and the rule *removes*
  thresholds — `ConcealedTopRank` exists to control volume that median-1 cohorts create, and
  `MinCohort`'s gating role exists to control the thin end of the same problem.

  **The trap, found while working it through and recorded so it is not rediscovered. Measured
  2026-08-25, and it is real and sharper than this said.** At MAD = 0 the scale estimate collapses,
  so `median + k·MAD` is the median and everything above it fires. Counted inside the MAD-0 cohorts
  only, which is the comparison the claim is about — findings from a naive gate against what ships:

  | | nopCommerce | Jellyfin | Umbraco |
  |---|---|---|---|
  | member level | 429 vs 286 — **1.5x** | 529 vs 337 — **1.6x** | 2,398 vs 1,300 — **1.8x** |
  | type level | 426 vs 23 — **18.5x** | 102 vs 17 — **6.0x** | 401 vs 72 — **5.6x** |

  **Swapping the statistic is not the fix**, and the type-level row is why: what ships produces
  almost nothing in those cohorts, so a naive replacement is not a tightening that went too far, it
  is a flood where there was silence.

  **But in the cohorts that do have spread, dispersion is the tighter gate, and that is new.**
  `median + k·MAD` against `3x median`, MAD > 0 only: nopCommerce 996 vs 1,491 at member level and
  197 vs 231 at type level; Jellyfin 618 vs 885 and 135 vs 159; Umbraco 1,312 vs 2,308 and 439 vs
  615. **15% to 43% fewer on every solution and both levels.** So the two branches are not one
  statistic applied twice — the spread branch is a straight improvement and the no-spread branch
  needs the different *sentence*, not a different threshold.

  ~~**The no-spread branch is the majority of the type-level population**~~ — **the population claim
  holds and the mechanism was wrong, corrected 2026-08-25.** 70% of nopCommerce's gated type-level
  population does live in zero-dispersion cohorts, 46% of Jellyfin's and 47% of Umbraco's. **What is
  not true is that a median of 0 silences the claim.** `Distribution.TimesMedianOf` returns
  `PositiveInfinity` there and `Read` only returns null for a group below `IsComparable`, so
  `TimesMedian >= OutlierFactor` is satisfied **by definition** — a zero-median cohort is
  auto-*admitted*, not blocked. The first version of this paragraph had it exactly backwards.

  **That error was a defect of its own**, because modelling it the wrong way produced 102 findings
  where the tool emits 103, and the missing one was a finding gated on an infinity that the export
  publishes as `null`. 7 such on nopCommerce, 5 on Jellyfin, 23 on Umbraco, across three finding
  kinds and two gates. **§28 fixed the rendering half of this and never reached the detector.**

  **What the trap points at is the actual answer: let the claim follow the distribution rather than
  making a threshold chase the size.** A type at cc 12 among 1,656 peers all at 0 is a finding, and
  it is not a relative one. With spread the sentence is *"unusual among its peers — cc 93 against a
  median of 1"*; without spread it is *"the only one of its kind — cc 12 where all 1,656 peers are
  0"*. Two claims, each true where it is used, neither needing a size threshold. Same shape as the
  namespace-cycle repair of the same day: classify by what is actually there instead of
  thresholding an aggregate.

  **Which gate actually decides — measured 2026-08-25, and it redirects this entry.**
  `ConcealedDecision.AtMethodLevel` is a conjunction of three gates, not one: an **absolute floor**
  (`MinDecisionCc`, `cc >= 5`), the **cohort-relative ratio** (`TimesMedian >= OutlierFactor`), and
  a **rank gate** (`Rank <= ConcealedTopRank`). X16 is written as a question about the ratio. Drop
  one at a time, member level, `tools/cohort-dispersion.py`:

  | | nopCommerce | Jellyfin | Umbraco |
  |---|---|---|---|
  | population in gated cohorts | 9,144 | 5,454 | 19,137 |
  | **all three — what ships** | **102** | **165** | **363** |
  | drop the **ratio** | 108 — **+5%** | 177 — **+6%** | 405 — **+11%** |
  | drop the **floor** | 132 — +28% | 191 — +14% | 493 — +35% |
  | drop the **rank** | 1,091 — +959% | 824 — +393% | 1,770 — +384% |

  (Reconstruction from `--csv`. With `TimesMedianOf`'s infinity modelled correctly it reproduces the
  export exactly — 103 and 366. The first pass modelled a zero median as *blocked* rather than
  *auto-admitted*, landed one short on nopCommerce, and that one finding is now §61.)

  **The cohort-relative ratio is the least load-bearing of the three.** Removing it entirely moves
  the shipped output by 6–12%; removing the absolute floor moves it two to three times as much, and
  removing the rank gate multiplies it by four to ten. The comment beside `MinDecisionCc` already
  says as much — *"`cc >= 5` decides 82% of nominations by itself"* — and this is that claim
  measured on three solutions rather than one.

  **And what ships is not noisy, which is the other half of the correction.** The shipped
  `ConcealedDecisionMethod` findings have a median `cc` of **14** on nopCommerce and **11** on
  Umbraco, a minimum of exactly 5, and **none at all below `cc` 5**. The `cc 3` flood the ratio
  generates — its single most common candidate on all three solutions — never reaches the export.

  **So the premise of this entry needs restating.** It is not *the relative claim is noisy and badly
  gated*: the output is clean. It is that **the relative claim is barely gating anything, and the
  correctness that exists comes from an absolute floor that §2 says does not travel between
  codebases, plus a rank gate that §10 shows goes vacuous at small cohorts.** The finding is very
  nearly `cc >= 5 AND top-3 in its cohort`, and the cohort-relative sentence it is reported in
  describes the gate that contributes least.

  **That moves the decision.** Swapping the ratio's statistic for a dispersion-based one — the
  direction below — is aimed at the gate with 6–12% of the leverage. The measurement points at two
  other questions instead: whether **rank** should be the cohort-relative gate outright, since it is
  ordinal, needs no median, is defined where `TimesMedian` is undefined, and is already doing the
  work; and whether the **absolute floor** should be calibrated rather than constant, which is §2's
  question and has never been asked of `MinDecisionCc`. **Neither is decided here, and the entry's
  original direction is not withdrawn — it is now the third-ranked option rather than the only one.**

  **The replacement, measured 2026-08-25 on three solutions.** The ratio is demoted from gate to
  evidence — it contributes 5–12% and it is what §61 rides on — so the question is what carries the
  cohort-relative judgement instead. Member level, `tools/cohort-dispersion.py`:

  | rank gate | nopCommerce | Jellyfin | Umbraco | cohorts 5–9 | cohorts 100+ |
  |---|---|---|---|---|---|
  | **ships** — `cc>=5 AND ratio>=3 AND rank<=3` | 103 | 167 | 366 | 1 / 17 / 14 | 36 / 31 / 106 |
  | `rank<=3`, ratio demoted | 108 | 177 | 405 | 1 / 22 / 26 | 36 / 31 / 106 |
  | `rank<=1` — no constant at all | 40 | 66 | 155 | 1 / 12 / 18 | 11 / 10 / 35 |
  | **`min(3, ceil(0.10n))`, floor 1** | **105** | **157** | **382** | 1 / 12 / 18 | 36 / 31 / 106 |

  **A pure share is not the answer** — 5% of a 2,909-member cohort is 146 findings from one cohort,
  and 5/10/20% shares give nopCommerce 423, 814 and 1,243 against 103. **Capped, it is.**
  `min(ConcealedTopRank, ceil(share·n))` reproduces the shipped volume within ±6% on all three,
  leaves the large end **byte-identical** (36/31/106 either way), and tightens only cohorts of 5–24
  — the one place the ratio was doing any work, because a top-3 of five is 60% of the cohort and a
  top-3 of five hundred is 0.6%.

  **`rank<=1` is the only formulation with no constant in it at all** — *the most complex member of
  its peer group* is a definition rather than a threshold — and it cuts 58–61% of the findings. That
  is a report-moving change rather than a refactor, and it is recorded as the option that exists,
  not the one recommended.

  **Deleting `MinCohort`'s gating role is cheap under every formulation, and that is a correction to
  the reason given for wanting the share.** Below-floor cohorts hold **57, 79 and 119 members** —
  0.6%, 1.4% and 0.6% of the gated population — so letting them nominate adds 12, 11 and 53 findings
  even under the unguarded `rank<=3`. **§10's pattern exactly: a real inconsistency with almost no
  field incidence.** The share is worth taking because selectivity should not depend on cohort size,
  not because the thin end is dangerous. It is not.

  **Honest cost.** `min(3, ceil(0.10n))` is *two* constants where there was one. They do different
  jobs — 3 is a volume cap, 10% is selectivity — which is X3's split-the-parameter shape rather than
  a sixth threshold, and neither has to move when a codebase changes size. **X16 was expected to
  delete two policy values; on this design it deletes `OutlierFactor`'s gating use and keeps
  `ConcealedTopRank` as a cap.** Say so rather than counting it as a deletion.

  **Shipped 2026-08-25, `581a61f`, and the shape is not what this entry proposed.** Dispersion did
  not *replace* the ratio as a single gate; it took the ratio's job while a rank limit took a job
  the entry had not separated out.

  | | gate | job |
  |---|---|---|
  | `MinDecisionCc` | `cc >= 5` | absolute floor, unchanged |
  | `ConcealedTopShare` + `ConcealedTopRank` | `rank <= min(3, TopRankLimit(0.2))` | **how many** may say it — method arm only |
  | `ConcealedDispersionFactor` | `cc > (MAD > 0 ? median + 3·MAD : median)` | **whether the gap is meaningful** |
  | ~~`OutlierFactor`~~ | — | evidence now; gates nothing |

  Method arm 103/167/366 → **103/160/366**; type arm 78/64/146 → **69/61/144**; nopCommerce's
  `suffix:Service` roll-call halves, 21 → 10.

  > **The trap above is real and does not bite, and the reason is the part this entry was missing.**
  > It considered dispersion *replacing* the ratio with nothing else beside it, which is why a naive
  > gate read as worse than what ships. With a rank limit bounding a no-spread group to its top few,
  > `x > median` at `MAD = 0` is safe — and the claim there is a count rather than a multiple,
  > which `Claims.ConcealedType` already renders as *"the only complexity among its N peers"*.
  > **Dispersion decides whether a gap is meaningful; rank decides how many may say so. Neither
  > does the other's job, and that is the sentence this entry took four measurements to reach.**

  > **Demoting the ratio with only rank behind it is wrong, and three real solutions did not catch
  > it.** Rank is ordinal, so the top of a high-median cohort clears it while being barely above its
  > peers: the run nominated `DetentionEvaluator`, `FuelEvaluator` and `LaneEvaluator` at **cc 12
  > against a peer median of 10.5**, and P0 planted those as *complex code that is not anomalous*.
  > The comment beside the gate had predicted it and named the count — *"the fixture has three of
  > those"*. On the reference solutions the same class was 6, 12 and 42 findings that read as
  > marginal. **The fixture is where a gate is observable; the solutions are where it is measured.
  > This entry needed both and would have shipped a false-positive class on either alone.**

  > **A rank gate on the type arm over-cuts and was reverted.** 78 → 38 against 69 for the
  > dispersion swap alone: two volume-ish conditions stack where the ratio was one. The type arm
  > has no rank gate, as it never did.

  **Scope, corrected 2026-08-25.** This read *expected to close §10, §14, §28, §34, §38*, and
  **nine of the ten entries above are already closed** — each by the local repair this decision
  exists to remove. Only **§10** is live. So the deliverable is: **§10 closes; §14, §28, §34 and §38
  lose the threshold their repair installed; X3's remainder is answered; and `ConcealedTopRank` and
  `MinCohort`'s gating use are deleted.** Stated as a count of closures it is a success criterion
  nothing can meet.

  **Measured on two solutions, to be re-derived on three.** The table above is 2026-08-21,
  nopCommerce and Jellyfin. Umbraco — 6,230 types — became a reference solution on 2026-08-25 and
  has never been in this measurement. **Unmeasured** — the replacement has not been run end to end,
  and that measurement is the first piece of work, not the last.

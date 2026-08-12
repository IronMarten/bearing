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
oracle/ArchProbe/     evidence      the original probe, frozen verbatim.  Not shipped.
tests/Bearing.Tests/  verification  one project, referencing both Core and the oracle
tests/TestBed/        fixture       a synthetic solution with known answers
```

Dependencies run one way: `Cli → Core`. Nothing depends on the oracle except the tests.

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

**What exists today, stated plainly.** `Bearing.Core` holds one file, `ToolInfo.cs`. The
analysis is built and validated, but it is built in `oracle/ArchProbe/`, which is frozen and
cannot be edited — the baselines in `tests/Bearing.Tests/golden/` are the record of its exact
output. Extraction is the next work (`TECHREQ-job-a.md` §4, phase 1).

So this document specifies **what Core must be, not what Core is.** Where the oracle diverges
from it, the oracle is the thing that changes. Do not read requirements back out of
`ArchProbe` — it is a throwaway probe whose accumulators are one evening's convenience, and
§4 is a different and larger shape on purpose.

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

### Three fields to collect during the walk, not after

Each is nearly free inside the existing pass and costs a second full traversal of the
solution to reconstruct. Deciding late is the expensive option.

- **`Edge.kind`** — inheritance, interface implementation, field, constructor parameter,
  method call, generic argument, attribute. Without it the only filter a dependency-graph UI
  can offer is edge weight, which is the least interesting one available. Hiding abstraction
  and data-contract edges is what makes a DIP-heavy codebase readable at all.
- **`Edge.site`** — file and line for at least one representative reference per edge. This
  is what makes *"who actually calls this"* clickable rather than a claim.
- **`Type.kind` + why** — §6. Store `attribute:ApiController`, `base:DbContext`,
  `external-ns:Azure.Messaging` beside the value, never the value alone.

The taxonomy for `Edge.kind` is still open — §11.

### Project metrics are model data

Ca, Ce, A, I and D are computed inside the oracle's print routine and were never modelled. So
are the cohort statistics — sizes, percentiles and multiples of the peer median, the substrate
of every Job B claim. Both are §3's failure mode in its purest form, and moving them is what
phase 1 actually is (`TECHREQ-job-a.md` §4).

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

### The finding identity key — settled; the finding record is not

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
declaring type for exactly this.

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
| The probe is kept verbatim as a diff oracle | `oracle/README.md` |
| Conventions are build errors, not warnings | `Directory.Build.props` |
| A finding is identified by `(kind, subject)` and nothing else | §4, `FindingKey` |
| A type is identified by `(assembly, FQN)`, never by name alone | §4, `SubjectRef` — .NET permits one FQN in two assemblies and plugin architectures use it deliberately; keying on the name merges the rows and sums their metrics |
| The SDK is pinned | `global.json` — an unpinned toolchain picks the newest SDK on the machine, which made CI build a net8.0 project with .NET 10 and fail on rules no developer machine had |
| Both graph artifacts are static | neither view that proved legible on a real solution needs a layout engine, so elkjs / cytoscape / d3-force come off the critical path. The only view that did need one should not ship: a two-hop ego view pulls in 24–41% of the codebase from an ordinary seed |
| A method-level concealed decision suppresses breaks-alone on its declaring type | the reason the suppression exists is behavioural, and behaviour lives in methods — so the level that *nominated* it is not what decides. `SubjectRef` walks member → declaring type to express it. Not yet implemented: `DEFECTS.md` §15 |
| Every emitted artifact is ordered by a total key | a stable sort on a non-total key reproduces on one machine without being a property of the tool, and Core is a reimplementation that will not inherit the probe's enumeration order. `TESTING.md` §5, `DEFECTS.md` §6 |

## 11. Decisions still open

These are live, and each one changes code that has not been written yet. Full context in
the private `TECHREQ-job-a.md` §10.

Distinct from [`DEFECTS.md`](DEFECTS.md): that is behaviour known to be wrong, with a remedy
already understood. These are questions with no answer yet.

- **Edge kind taxonomy.** §4 commits to collecting one. *Which* set — inheritance,
  implementation, field, parameter, call, generic argument, attribute — and whether it is
  fixed or extensible, is undecided. Decide before the walkers move; §4 says why deciding
  after them is expensive. The filter is worth building either way: abstraction and contract
  edges are 39–50% of all out-edges.
- **How `WIDEST CONTRACT SURFACE` should be gated at all.** `DEFECTS.md` §12 is the one row
  that cannot be fixed by moving a constant, so extraction cannot simply port it. An absolute
  surface floor and a dispersion test — is the top of the distribution actually separated from
  the middle — are the two candidates. Decide before the suppression matrix is implemented, or
  it gets reimplemented unreachable.
- **Whether `SignificantKinds` stays at three.** There are exactly three and `--min-kind-span`
  is 3, so every spanning type necessarily carries the same signature: the `GroupBy` in
  `PrintLayerSpan` is written for a generality that cannot occur. Ties into the edge-kind
  taxonomy, and into `DEFECTS.md` §11 — a richer taxonomy would separate the anomaly from the
  boilerplate that currently hides it.
- **Thresholds global, or calibrated per codebase.** `DEFECTS.md` §2 narrows it: percentile
  gates travel between codebases, absolute ones do not.
- **Dead code at member level or types only.** Far more useful, far more false-positive
  prone.
- **Is the JSON schema a public contract from v0.1**, or versioned-but-unstable?

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

## The register

*One open. The numbers are identity — they are never reused and never renumbered.*

> **D45, D53 and D58 closed together on 2026-08-26, and the rule they were waiting for is not the
> rule they asked for.** All three had been held open on one question, stated in D45 and repeated
> in D58: *when is a large component a fact about the codebase rather than a finding about it?*
> Answering it needs a judgement nobody has a measurement for. **None of the three needed it**,
> because in each case the artifact's mistake was not misjudging a population — it was **asserting
> over a population wider than its own evidence**, and the extent of the evidence is arithmetic:
>
> - **D45** — a row boundary means *depends on*. Between two genuinely adjacent layers there is
>   always an edge, because `DepthOf` is a longest path; between two rows of one wrapped layer
>   there never is, because equal depth forbids an edge and a mutual pair is one box already. Both
>   are theorems, so the drawing marks the boundaries it can prove and stops implying the rest.
> - **D53** — a line means *depends on*. The transitive reduction of a DAG is unique and preserves
>   reachability exactly, so drawing it tells a reader everything drawing every edge told them.
> - **D58** — a shape reading means *these hold each other as state*. It is now claimed over the
>   namespaces named in the held pairs, which is what the evidence is.
>
> **What made the question look unavoidable was a measurement taken on the wrong graph.** D45's
> counter-example — a width bound puts *22 of 27 nopCommerce projects deeper than their real
> depth* — is true of the twenty-seven **projects** and irrelevant to the drawing, which lays out
> ten **boxes** whose widest layer is three. Re-measured on what the renderer actually lays out,
> the bound displaces **0 of 10** boxes on nopCommerce and **0 of 18** on Umbraco because it never
> engages at all, and **7 of 21** on Jellyfin, where it was supposed to be the remedy. So it is
> not conditional; it is worse everywhere it does anything, trading a misstatement a reader can
> check against the edges for one that leaves no trace on the page.
>
> **One half of D58's second face is not fixed, and it is a prominence problem rather than a false
> claim.** `Microsoft.Extensions.Hosting` is still the first member named on Umbraco's cycle line
> and still seeds the example loop, because members are ordinal and `Cycles.PathThrough` seeds at
> the ordinal minimum. Everything said about it is now true — it reaches the others, and the
> holding sentence no longer covers it. Seeding the loop from a namespace the evidence covers would
> read better and was deliberately not done: `CycleShapes` is a pass *over* the detected set and
> does not have the adjacency, so wiring the reading back into detection would invert the
> separation that keeps the suppressed set disclosable. It is worth its own entry if anyone reads
> the loop line and is misled by it; nobody has yet.

> **It is a decision now, and it has code in it, so it has a number**: `docs/ARCHITECTURE.md` §10,
> X17. The general form is *bound the claim to the evidence rather than judge the population*, and
> it is worth reaching for wherever an artifact describes a set larger than the one it measured.

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


## Closed

> **Most `docs/DEFECTS.md §N` references in `src/` now point at index rows, and that is fine** —
> they are prose in doc comments, and this index is what keeps them resolvable. **If a closed entry
> is ever restored to full prose, restore it in place** rather than renumbering: the numbers are
> identity, and `ArchitectureDiagram.cs` alone cites nine of them.

**Index only — the prose is in git.** Sixty-one entries: forty-four removed 2026-08-24, plus D55 and D59, then A11 round 2's presentation list — D48, D49, D50, D51, D52 and D60 — then D56 and D57, all closed 2026-08-25 — and D45, D53 and D58, closed together on 2026-08-26 by the rule recorded above and indexed the same way. Status is as the
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
| 10 | The cohort floor strips a suppression it was never meant to touch | fixed 2026-08-26 — `MinCohort` no longer gates nomination in either `ConcealedDecision` arm, so no type is dropped out of concealed decision and into breaks alone. Nothing replaces it: `Distribution.Read` refuses below two values and the dispersion gate cannot fire at two |
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
| 44 | Every channel of the reach plot is normalised to its own run, so two reports cannot be compared by eye | fixed 2026-08-26 — both axes are a fixed 0–100% square root and the dot radius is `sqrt(types)`, so all three channels are functions of the project rather than the run. `Bound`, `Step` and the radius basis deleted; the 2026-08-22 disclosure is inverted rather than removed |
| 46 | A suppression emptied the HTML cycles section, because the evidence replacing it went to one renderer | fixed |
| 45 | A layer wider than the cap is drawn as two rows, and nothing says so | fixed 2026-08-26 — rows of one layer sit at a tighter gap and a dashed rule marks each boundary that an edge proves, drawn only where a layer wrapped. The width bound was measured on the folded graph and rejected: it displaces 7 of Jellyfin's 21 boxes and never engages on the other two |
| 53 | The project map draws an edge through whatever box is in the way, so a direct dependency reads as an indirect one | fixed 2026-08-26 — the map draws `ProjectGraph.Reduction` and paints edges over the boxes. Crossings went 18→0 on nopCommerce, 27→2 on Umbraco and 81→21 on Jellyfin; the implied count is disclosed in the caption |
| 58 | A namespace cycle's shape is decided by one arm and applied to the whole component | fixed 2026-08-26 — the holding claim is made over `ShapedCycle.Coupled` and the component keeps its own, larger sentence. Umbraco reads 15 of 363, nopCommerce 10 of 30, Jellyfin 2 of 18. See the residue in the register's note |
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
| 62 | A "top N%" claim is made from a midrank, so it is twice as extreme as the group allows | fixed 2026-08-26 — `Distribution.TopShareOf` is the claim statistic, `PercentileOf` stays the ordering one; the reasoning is beside the code and the rule is `ClaimsTests.No_claim_is_more_extreme_than_one_member_of_its_peer_group` |

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

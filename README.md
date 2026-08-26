# Bearing

**Get your bearings in a .NET codebase.**

Bearing reads a solution and gives you two things: a map of the system, and a short list
of the components that are unusual *for what they are*.

```
dotnet tool install -g IronMarten.Bearing
bearing ./MySolution.sln
```

Zero configuration. No account. No network call.

---

> ### Status: early preview
>
> `0.0.1-preview` is a **placeholder release and performs no analysis.** It is published
> so the package identity is established while the first real version is built. The
> analysis engine is validated and being productised; watch the repo for `0.1`.

---

## What it answers

*"Do I understand my system?"*

Not *"is my code healthy?"* — that is a different question, it is well served by other
tools, and its audience is architecture specialists. The first question gets asked
constantly by people who are not specialists, usually right before they change something.

## How it reports

**Findings are sentences, not scores.** Every measurement exists to support a claim you
can act on. If a number does not end in something that changes what someone does, it is
not shown.

**Nothing absolute is ever the headline.** A finding is relative to the peer cohort a
component *should* resemble — discovered structurally from shared interfaces, base types,
name suffixes, architectural role, or namespace. "Top 2% of your 56 normalizers" is a
claim you can check. "Risk score 103,680" is a claim you can only argue with.

**There is no composite score.** Not on a dashboard, not in a tooltip, not as a CSV
column. This is a deliberate and permanent constraint.

**Silence is never a clean bill of health.** Every report states what it stayed quiet
about — components with no peer group, excluded generated code, projects that failed to
load. A tool that quietly says nothing about the riskiest thing in your codebase is worse
than no tool.

**A finding you have decided about stays decided.** Put its key in `.bearing-acknowledged`
beside your solution, commit the file, and the claim stops being made — so a second run tells
you what is *new* rather than repeating what you already looked at. Keys come from `--json`,
where every finding carries the one it is identified by; a note after a tab records why, which
is the half that is still worth reading a year later. The report always says how many findings
the file kept out of it, because a tool that can be told to go quiet has to say when it has.

**Acknowledging is not deleting.** The claim stays in `--json` with its evidence, marked
`acknowledged`, and an entry that stops matching anything — which is what a rename does — is
reported rather than dropped.

**The JSON and CSV are unstable while the tool is 0.x.** `--json` carries a `schemaVersion`,
independent of the tool's version and moved only when a consumer would have to change to keep
reading. Pin against it if you build on the output. The shape is not a public contract before
1.0 — one deliverable is still unbuilt and it will add fields — and it becomes one at 1.0 if
anyone is depending on it by then.

## Design constraints

These are not preferences. Each one was learned by building the opposite and watching it
produce confident, plausible, wrong output.

- Every normalized measure carries an absolute floor beside it. Ratios and percentiles
  discard magnitude, and magnitude is frequently the point.
- Anomaly, not roll-call. A flag that fires on every member of a category conveys nothing;
  four controllers reaching into data access is one fact about your layering, not four
  findings.
- Never two findings that contradict each other about one component.
- Never imply safety at a boundary. Bearing cannot see external consumers, so it marks the
  boundary as unseeable rather than guessing.
- Blank, never fake. A statistic with no meaningful basis is emitted empty.
- Name the specifics. *"Spans 3 architectural kinds"* is arguable; *"why is authentication
  calling `TenantStore`?"* is not.

## Non-goals

- **No runtime or traffic observation, ever.** Not telemetry, not API keys, not sampling.
- **No composite score, letter grade, or "architecture score 92".**
- **No AI-generated explanations.** Findings are deterministic and auditable.

## Requirements

.NET SDK 8.0 or later. The target solution must restore before analysis, or projects load
with missing references and the results are silently understated.

## Licence

[Apache-2.0](https://github.com/ironmarten/bearing/blob/main/LICENSE) © Iron Marten

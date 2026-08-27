# Contributing

```
dotnet build Bearing.sln      # warnings are errors
dotnet test  Bearing.sln      # ~550 tests, ~2 minutes (docs/TESTING.md has the breakdown)
```

> **This repository is developed against private specifications, and outside contributions
> are not what it is for.** The source is public so it can be *inspected* — so anyone can
> read what the tool measures, check a claim against the code that made it, and see the
> reasoning behind a threshold. That is the goal, and it is a different goal from being
> contributable to.
>
> The practical consequence is in the comments. Source under `src/` and `tests/` cites six
> documents by name — `PRD-free-tier.md`, `TECHREQ-job-a.md`, `TECHREQ-job-b.md`,
> `SCHEMA-findings-export.md`, `TASKS.md` and `SESSION-NOTES.md` — and none of them is in
> this repository, because they are the commercial documents rather than the design ones.
> **Those citations are load-bearing, not decorative**: `ArchitectureDiagram.cs` cites
> §5.4 for an acceptance criterion — *legible at screenshot size on a 30-project solution
> with no interaction* — that the comment does not restate, so an outside reader cannot
> resolve the bar the code is built to.
>
> `ARCHITECTURE.md` §2 states the principle this breaks: *a constraint a reader cannot
> resolve is not a constraint*. It reproduces the eight invariants for exactly that reason.
> The rest is not reproduced, and saying so plainly is better than leaving it to read as an
> oversight. **If you are reading a comment whose citation you cannot follow, the decision
> it refers to is real and the reasoning is not published — take the code at face value.**

Read [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) first — it is short, and it explains
why the code is split the way it is. [`docs/TESTING.md`](docs/TESTING.md) covers the suite
and the two snapshot regimes. Behaviour known to be wrong and deliberately left that way is
filed as an issue — **check the open ones before reporting a defect**, and before assuming
a surprising output is a bug you have just found.

On a machine where the SDK is user-local and not on `PATH`:

```
export DOTNET_ROOT="$LOCALAPPDATA/Microsoft/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

---

## Conventions are build errors

`TreatWarningsAsErrors`, .NET analyzers at `latest-Recommended`, and code style enforced in
build. A convention that only warns is a convention nobody follows by the third month.

One directory opts out, and it has a `Directory.Build.props` saying why in full:

- **`tests/TestBed/`** — the fixture's defects are the specification.

There were two. `oracle/` held the probe frozen verbatim as the diff oracle, and it was
retired at R2 along with the golden baselines it existed to produce.

If you need a suppression elsewhere, put it in `.editorconfig` with a comment explaining
what the rule is right about and why it does not apply here. Every existing suppression has
one. Do not use `#pragma warning disable` for anything that recurs.

### Rules that are errors on purpose

`CA1304`, `CA1305`, `CA1307`, `CA1309`, `CA1310` — culture and comparison. These are not
style. Output is compared byte-for-byte against a stored baseline, and a machine that
renders `3.5` as `3,5` produces a diff that looks like a behaviour change and is not.
Ordinal comparison matters for the same reason it mattered in the probe: namespace matching
is by segment, and `System.Net.Http` must never collapse to `System.Net`.

## Style

Set in `.editorconfig` and enforced; the notable ones:

- File-scoped namespaces, `using` outside the namespace, System directives first.
- 4 spaces, CRLF, final newline, no trailing whitespace. 2 spaces for XML, JSON and YAML.
- `_camelCase` private fields, `PascalCase` members, `I`-prefixed interfaces.
- `var` when the type is apparent, the explicit type otherwise.
- Braces optional on a genuine one-liner, required once a statement spans lines.

Test method names are sentences — `Solution_loads_with_no_warnings`,
`Kind_keys_off_types_used_not_using_directives`. The naming rules are switched off under
`tests/` so they do not fight this.

## Comments

The bar is: **explain the decision, not the mechanism.** Anyone can read what the code does.
What is expensive to recover is why it does that and not the obvious alternative.

Worth writing:

- Why a threshold is the number it is, and which false positive produced it.
- What a naive implementation would do instead, and what breaks when it does — the fixture
  is full of planted traps, and a comment is what stops someone "fixing" the code into one.
- Which defect a piece of code is the fix for. Several of the probe's defects were
  reintroductions; a comment is cheaper than finding out a third time.

Not worth writing: restating the statement below it.

## Commits

Subject in the imperative, under ~72 characters, describing the change in the codebase's own
terms — `Phase 0: regression suite over the TestBed fixture`, not `update tests`. The body
carries the reasoning where there is any.

**Changing a frozen golden baseline requires the reason in the commit message**, stated as a
deliberate behaviour change. See [`docs/TESTING.md`](docs/TESTING.md) §3. A baseline that
gets quietly regenerated is not a baseline.

On Windows, note the shell you are in: `@'…'@` here-strings are PowerShell-only and bash
parses `@'…'` as a stray `@` plus a quoted string, which leaks a `@` as the commit subject.
`git commit -F <file>` works in both.

## Adding a test

Assert against the model, never against report wording — `Report.cs` is the layer being
replaced, and tests coupled to its sentences would die exactly when they are needed. The
accepted snapshots are the deliberate exception.

If you add a case to `tests/TestBed/`, **add, do not reshape**, and record the known answer
in [`docs/TESTING.md`](docs/TESTING.md) §6. Every assertion in the suite is a claim about
the fixture's exact shape, and `StructureTests.Fixture_shape_is_stable` is where that shape
is written down — read it there rather than from a copy. This sentence carried one until
2026-08-26 and it was four times too small.

Then mutation-test the assertion: break the thing on purpose and confirm it fails. A gate
that cannot fail is worse than no gate, because it looks like coverage.

## Adding an architectural rule

If the rule matters, add it to `SeamTests` with a sentence explaining what violating it
would mean — not just to `ARCHITECTURE.md`. Prose did not stop `Report.cs` becoming 997
lines of computation-inside-a-renderer, and it will not stop the next one.

There are two lists, and the choice between them is whether the type or the call is the
thing being forbidden:

- **`ForbiddenInCore`** — a type Core may not touch at all. `System.Console` is the case:
  there is no legitimate use of it in a layer that returns data.
- **`ForbiddenCallsInCore`** — a call Core may not make on a type that is otherwise fine.
  `Environment.GetEnvironmentVariable` is the case, and it is why the second list exists:
  banning `System.Environment` outright fails on `get_CurrentManagedThreadId`, which the
  compiler emits into every async state machine. A rule nobody can satisfy is not a rule.

**A type the compiler emits for a language feature cannot go in either list**, and finding
that out three times is what the two lists cost:

| wanted to ban | why it is not bannable |
|---|---|
| `System.Environment` | `get_CurrentManagedThreadId`, emitted into every async state machine |
| `System.Text.StringBuilder` | `ToString` and `PrintMembers`, emitted for every `record` |
| `System.Globalization` | not a distinction metadata carries — Core legitimately calls `CultureInfo.InvariantCulture`, and CA1304/CA1305 already make the misuse a build error |

The test reads IL, which is the right call — a mention in a comment cannot trip it, and a real
call cannot hide from it — and the price is that it sees what the compiler wrote as well as what
you wrote. Check the entry passes on a clean tree before writing its reason, not after.

Both read compiled metadata rather than source, so a mention in a comment cannot trip them.
Mutation-test the entry either way: `The_forbidden_call_check_finds_the_call_where_it_is_allowed_to_live`
is the shape to copy — it asserts the same detector *does* fire against the assembly where
the call belongs, so the gate cannot pass because nothing anywhere makes it.

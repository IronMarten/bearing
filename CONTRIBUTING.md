# Contributing

```
dotnet build Bearing.sln      # warnings are errors
dotnet test  Bearing.sln      # 202 assertions, ~3s
```

Read [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) first — it is short, and it explains
why the code is split the way it is. [`docs/TESTING.md`](docs/TESTING.md) covers the suite
and the two snapshot regimes. [`docs/DEFECTS.md`](docs/DEFECTS.md) lists behaviour that is
known to be wrong and deliberately left that way — **check it before reporting one**, and
before assuming a surprising output is a bug you have just found.

On a machine where the SDK is user-local and not on `PATH`:

```
export DOTNET_ROOT="$LOCALAPPDATA/Microsoft/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

---

## Conventions are build errors

`TreatWarningsAsErrors`, .NET analyzers at `latest-Recommended`, and code style enforced in
build. A convention that only warns is a convention nobody follows by the third month.

Two directories opt out, and each has a `Directory.Build.props` saying why in full:

- **`oracle/`** — the probe is frozen verbatim as the diff oracle. It carries 32 analyzer
  warnings and every one is a fair comment. Acting on any of them edits the implementation
  whose output the golden baselines are the record of. CA1305 in particular is not
  cosmetic: adding an `IFormatProvider` can change rendered decimals in `nominations.txt`,
  which is a behaviour change disguised as a lint fix.
- **`tests/TestBed/`** — the fixture's defects are the specification.

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
golden snapshots are the deliberate exception.

If you add a case to `tests/TestBed/`, **add, do not reshape**, and record the known answer
in [`docs/TESTING.md`](docs/TESTING.md) §6. Every assertion in the suite is a claim about
the fixture's exact shape — 51 types, 131 edges, 8 cohorts, 2 excluded.

Then mutation-test the assertion: break the thing on purpose and confirm it fails. A gate
that cannot fail is worse than no gate, because it looks like coverage.

## Adding an architectural rule

If the rule matters, add it to `SeamTests.ForbiddenInCore` with a sentence explaining what
violating it would mean — not just to `ARCHITECTURE.md`. Prose did not stop `Report.cs`
becoming 997 lines of computation-inside-a-renderer, and it will not stop the next one.

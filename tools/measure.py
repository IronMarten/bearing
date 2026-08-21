"""Time the shipped binary against a real solution.

    python tools/measure.py <exe> <solution.sln> [--repeat 3] [-- <tool args>]

Prints each run, the median, and — since A12 — the median of every stage in the tool's
own profile. Exists because A2's figures — 57s on Jellyfin, 96s on nopCommerce, "slower
than the probe on both" — turned out to be artifacts of the build configuration, and a
number nobody can re-derive is a number that quietly becomes a planning constraint.
`tools/leave-one-out.sh` is the same idea for the gate inventory.

Four things it does that a stopwatch does not:

  * **Records the first byte of output separately from the last.** Time-to-first-finding
    is `PRD-free-tier.md` metric 4 and it is not the same measurement as time-to-report —
    unless the architecture makes it the same, which is worth knowing and is currently
    the case.
  * **Repeats.** A single run put Jellyfin anywhere between 19s and 36s depending on what
    else was on the machine. The spread is wider than most differences worth arguing about.
  * **Drains stderr.** A child writing more than a pipe buffer of diagnostics blocks
    forever while the parent waits on stdout. ArchProbe on nopCommerce does exactly that,
    and the first version of this script deadlocked on it.
  * **Takes the median of each stage, not only of the total.** A change that moves 2s from
    one stage to another leaves the total alone, and a change that costs 2s in the walk is
    invisible next to MSBuild's spread. A9 changes one stage — `references` — and this is
    how that is judged.

`--profile` is passed to the tool automatically unless you passed it yourself. It costs
one short table on stderr, which is drained either way; the measurement is always taken
inside the tool, so asking for it printed does not change the run.

Run nothing else on the machine while measuring. That is not a formality: the contended
runs above were this repository's own test suite.
"""
import subprocess, sys, time, os, statistics, tempfile

args = sys.argv[1:]
extra = []
if "--" in args:
    i = args.index("--")
    args, extra = args[:i], args[i + 1:]

repeat = 3
if "--repeat" in args:
    i = args.index("--repeat")
    repeat = int(args[i + 1])
    args = args[:i] + args[i + 2:]

if len(args) < 2:
    sys.exit(__doc__)

if "--profile" not in extra:
    extra = extra + ["--profile"]

exe, solution = args[0], args[1]
runs = []
stages = {}   # display name -> [seconds per run], insertion-ordered as the table prints it


def read_profile(text):
    """The stage rows of one `-- PROFILE` table, as {name: seconds}.

    Keyed on the name with its indentation kept, because `unmeasured` appears twice — once
    inside the walk and once for the run — and collapsing them would silently add them up.
    """
    found, inside = {}, False

    for line in text.splitlines():
        if line.strip() == "-- PROFILE":
            inside = True
            continue
        if not inside or not line.strip():
            continue
        if set(line.strip()) == {"-"}:          # the rule under the last stage
            break

        fields = line.split()
        if len(fields) < 2 or not fields[1].endswith("s"):
            continue                            # the prose above the table

        try:
            seconds = float(fields[1][:-1])
        except ValueError:
            continue

        nesting = "  " if line.startswith("     ") else ""
        found[nesting + fields[0]] = seconds

    return found


for n in range(1, repeat + 1):
    start = time.monotonic()
    first = None

    with tempfile.TemporaryFile(mode="w+", encoding="utf-8", errors="replace") as errors:
        proc = subprocess.Popen(
            [exe, solution] + extra,
            stdout=subprocess.PIPE, stderr=errors,
            text=True, encoding="utf-8", errors="replace", bufsize=1)

        for _ in proc.stdout:
            if first is None:
                first = time.monotonic() - start

        code = proc.wait()
        errors.seek(0)
        profile = read_profile(errors.read())

    total = time.monotonic() - start
    runs.append(total)
    for name, seconds in profile.items():
        stages.setdefault(name, []).append(seconds)

    print(f"  run {n}: {total:6.1f}s   first output {first or total:6.1f}s   exit {code}")

print(f"\n  median of {repeat}: {statistics.median(runs):.1f}s"
      f"   (spread {min(runs):.1f}-{max(runs):.1f}s)")

if stages:
    width = max(len(name) for name in stages)
    print("\n  medians by stage. The tool's own reading, which ends before the process does:\n")
    for name, seconds in stages.items():
        print(f"    {name.ljust(width)}  {statistics.median(seconds):6.2f}s"
              f"   (spread {min(seconds):5.2f}-{max(seconds):5.2f}s)")

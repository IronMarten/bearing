"""Time the shipped binary against a real solution.

    python tools/measure.py <exe> <solution.sln> [--repeat 3] [-- <tool args>]

Prints each run and the median. Exists because A2's figures — 57s on Jellyfin, 96s on
nopCommerce, "slower than the probe on both" — turned out to be artifacts of the build
configuration, and a number nobody can re-derive is a number that quietly becomes a
planning constraint. `tools/leave-one-out.sh` is the same idea for the gate inventory.

Three things it does that a stopwatch does not:

  * **Records the first byte of output separately from the last.** Time-to-first-finding
    is `PRD-free-tier.md` metric 4 and it is not the same measurement as time-to-report —
    unless the architecture makes it the same, which is worth knowing and is currently
    the case.
  * **Repeats.** A single run put Jellyfin anywhere between 19s and 36s depending on what
    else was on the machine. The spread is wider than most differences worth arguing about.
  * **Drains stderr.** A child writing more than a pipe buffer of diagnostics blocks
    forever while the parent waits on stdout. ArchProbe on nopCommerce does exactly that,
    and the first version of this script deadlocked on it.

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

exe, solution = args[0], args[1]
runs = []

for n in range(1, repeat + 1):
    start = time.monotonic()
    first = None

    with tempfile.TemporaryFile() as errors:
        proc = subprocess.Popen(
            [exe, solution] + extra,
            stdout=subprocess.PIPE, stderr=errors,
            text=True, encoding="utf-8", errors="replace", bufsize=1)

        for _ in proc.stdout:
            if first is None:
                first = time.monotonic() - start

        code = proc.wait()

    total = time.monotonic() - start
    runs.append(total)
    print(f"  run {n}: {total:6.1f}s   first output {first or total:6.1f}s   exit {code}")

print(f"\n  median of {repeat}: {statistics.median(runs):.1f}s"
      f"   (spread {min(runs):.1f}-{max(runs):.1f}s)")

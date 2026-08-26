namespace IronMarten.Bearing;

/// <summary>
/// One finding a user has marked <i>known and fine</i>.
/// </summary>
/// <param name="Key">
/// The <see cref="FindingKey.Canonical"/> form of the claim being dismissed. Ordinal equality
/// against a finding's key and nothing looser — see <see cref="Acknowledgments"/> for why a name
/// is not offered as an alternative.
/// </param>
/// <param name="Note">
/// Why it is fine, in the user's words, or <see langword="null"/> if they did not say. Carried
/// rather than discarded because the accumulated file is the artifact — a list of keys with no
/// reasons beside them is unreviewable by the next person, and a year later by the same one.
/// </param>
/// <param name="Line">
/// Which line of the file it came from, 1-based. What a diagnostic has to name: a key is not
/// something a user can find in their file by eye.
/// </param>
public sealed record Acknowledgment(string Key, string? Note, int Line);

/// <summary>
/// The acknowledgment file — <c>PRD-free-tier.md</c> §10.3, and success metric 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mark a finding known and fine, and it stays quiet next run.</b> Without it the known-gnarly
/// component is re-flagged forever, which is invariant 2's alert fatigue arriving by the back door
/// — a re-run is only informative if a finding can be <i>new</i>.
/// </para>
/// <para>
/// <b>Keyed on <see cref="FindingKey.Canonical"/>, and deliberately not on a type name.</b> The key
/// is <c>(kind, subject)</c> and nothing else, which is what makes it survive the things that move
/// when nothing meaningful changed — the file, the line, the metric, the threshold, the rank, the
/// position under <c>--top</c>. A name would be shorter to type and would silence every claim about
/// that component, including the one the user has not seen yet.
/// </para>
/// <para>
/// <b>An acknowledgment is not a suppression row, and conflating them would cost the export its
/// point.</b> A suppression is the tool deciding a claim would be wrong; an acknowledgment is the
/// tool standing by a claim the user has dismissed. <c>SCHEMA-findings-export.md</c> §1 makes the
/// difference observable rather than academic: the export carries every judgement, so a consumer
/// has to be able to tell a claim Bearing withheld from one it made and the user waved through.
/// That is why <see cref="Judged"/> carries them on two fields and the export has three statuses.
/// </para>
/// <para>
/// <b>Escalation is deliberately not here.</b> Acknowledge a god object and it stays acknowledged
/// if it doubles in size — the trade <see cref="FindingKey"/> records, taken because banding
/// severity into the key would make a retune invalidate every stored acknowledgment. The receipts
/// are in the export beside the key, so re-raising on material change is a decision that can be
/// made later against data already being written. This format does not foreclose it.
/// </para>
/// <para>
/// <b>Read in Core rather than in the CLI.</b> This is an input to the judgement, not a
/// presentation choice: which claims stand is Core's question, and a renderer that filtered its
/// own output would be the second undeclared route <c>docs/ARCHITECTURE.md</c> §11 is about.
/// </para>
/// </remarks>
public sealed class Acknowledgments
{
    /// <summary>
    /// The directory Bearing's committed files live in, beside the solution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A directory rather than a bare dotfile, because it will not be the only file in it.</b>
    /// The paid tier keeps its policy beside this one and collects the directory in CI, and two
    /// files a customer maintains together should not be in two places for no reason.
    /// </para>
    /// <para>
    /// <b>Beside the solution, not at a repository root, and the multi-solution case decides it.</b>
    /// The cardinality is one per analysis unit — one per solution — and this makes that structural
    /// instead of conventional. A single directory at a repository root serving several solutions
    /// has to tell their files apart by name, and a naming convention is the same class of thing as
    /// a canonicalisation rule: a second place two implementations can disagree about what a file
    /// is. It also costs nothing to find. Bearing analyses a solution and has no notion of a
    /// repository; discovering a root means depending on git, which would be a real coupling bought
    /// for a cosmetic gain, and would fail outside a checkout.
    /// </para>
    /// <para>
    /// <b>The two are not the same directory as often as they look.</b> nopCommerce's solution is at
    /// <c>src/NopCommerce.sln</c>, with a second under <c>src/Build/</c> — so a repository root and
    /// a solution directory differ in the ordinary case, not an exotic one. Jellyfin and Umbraco
    /// both keep theirs at the root, where the question does not arise.
    /// </para>
    /// <para>
    /// <b>And it keeps <see cref="Judgement.Unmatched"/> meaning what it says.</b> One file serving
    /// several solutions reports every key the current run's solution does not contain as unmatched,
    /// which turns a rename signal into noise proportional to how much of the repository this run
    /// is not looking at.
    /// </para>
    /// </remarks>
    public const string DefaultDirectoryName = ".bearing";

    /// <summary>
    /// What the file is called when the user does not say. Meant to be committed — the accumulation
    /// is the point.
    /// </summary>
    public const string DefaultFileName = "acknowledged";

    /// <summary>How the file is written when a report or a message names it.</summary>
    public const string DisplayName = DefaultDirectoryName + "/" + DefaultFileName;

    private const char NoteSeparator = '\t';
    private const char Comment = '#';

    private readonly Dictionary<string, Acknowledgment> _byKey;

    private Acknowledgments(
        IReadOnlyList<Acknowledgment> all,
        Dictionary<string, Acknowledgment> byKey,
        string? path)
    {
        All = all;
        _byKey = byKey;
        Path = path;
    }

    /// <summary>Nothing acknowledged — a run with no file, which is every first run.</summary>
    public static Acknowledgments None { get; } =
        new([], new Dictionary<string, Acknowledgment>(StringComparer.Ordinal), null);

    /// <summary>Every entry, in file order.</summary>
    public IReadOnlyList<Acknowledgment> All { get; }

    /// <summary>Where they were read from, or <see langword="null"/> when nothing was read.</summary>
    public string? Path { get; }

    /// <summary>How many findings this file can silence.</summary>
    public int Count => All.Count;

    /// <summary>
    /// Reads the file at <paramref name="path"/>, or returns <see cref="None"/> if there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A missing file is not an error, and a malformed line cannot exist.</b> Every line that is
    /// neither blank nor a comment is a key, because nothing here can know which keys a run will
    /// produce — validating them would need either a table of every possible subject or a rule that
    /// rejects the next finding kind. A key that matches nothing is reported after the run instead,
    /// where it can be counted against what was actually judged.
    /// </para>
    /// <para>
    /// <b>The first of two entries with one key wins, rather than the file being rejected.</b> This
    /// is a committed file, so the way it acquires a duplicate is a merge, and refusing to analyse
    /// a solution because two branches acknowledged the same finding would be the tool punishing
    /// the behaviour it exists to produce. The two say the same thing; only the note can differ.
    /// </para>
    /// </remarks>
    /// <exception cref="IOException">The file exists and could not be read.</exception>
    public static Acknowledgments Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return File.Exists(path) ? Of(File.ReadAllLines(path), path) : None;
    }

    /// <summary>Parses lines already in hand — the file format, without the file.</summary>
    public static Acknowledgments Of(IEnumerable<string> lines, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var all = new List<Acknowledgment>();
        var byKey = new Dictionary<string, Acknowledgment>(StringComparer.Ordinal);
        var number = 0;

        foreach (var raw in lines)
        {
            number++;

            var line = raw.Trim();
            if (line.Length == 0 || line[0] == Comment) continue;

            // The note is whatever follows the first tab. A tab rather than a marker character
            // because a key is built from assembly names, fully-qualified names and member
            // signatures, and every printable character this file might reserve occurs in at
            // least one of them. A tab occurs in none.
            var split = line.IndexOf(NoteSeparator);
            var key = (split < 0 ? line : line[..split]).TrimEnd();
            var note = split < 0 ? null : Blank(line[(split + 1)..].Trim());

            if (key.Length == 0) continue;

            var entry = new Acknowledgment(key, note, number);
            if (byKey.TryAdd(key, entry)) all.Add(entry);
        }

        return all.Count == 0 && path is null ? None : new Acknowledgments(all, byKey, path);
    }

    /// <summary>
    /// Where the file sits for a given solution when the user does not say.
    /// </summary>
    public static string DefaultPathFor(string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        return System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(solutionPath) ?? ".", DefaultDirectoryName, DefaultFileName);
    }

    /// <summary>
    /// How to name <paramref name="path"/> in a report, given the solution it was read for.
    /// </summary>
    /// <remarks>
    /// <b>Relative to the solution when it is under it, and the whole path otherwise.</b> The file
    /// name alone stopped being enough the moment it became <c>acknowledged</c> inside a directory:
    /// a report telling a reader that <i>acknowledged</i> withheld three findings has named nothing
    /// they can open. A user who pointed <c>--acknowledge</c> somewhere else gets the path they
    /// gave, because for them the location is the fact.
    /// </remarks>
    public static string Naming(string? path, string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        if (path is null) return DisplayName;

        var root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(solutionPath));
        if (root is null) return path;

        var full = System.IO.Path.GetFullPath(path);

        return full.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? full[(root.Length + 1)..].Replace(System.IO.Path.DirectorySeparatorChar, '/')
            : full;
    }

    /// <summary>The entry dismissing <paramref name="key"/>, or <see langword="null"/> if none does.</summary>
    public Acknowledgment? For(FindingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _byKey.GetValueOrDefault(key.Canonical);
    }

    private static string? Blank(string value) => value.Length == 0 ? null : value;
}

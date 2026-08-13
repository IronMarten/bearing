using System.Globalization;
using System.Text;

namespace IronMarten.Bearing.Cli;

/// <summary>
/// The mechanics of writing HTML safely. No opinions about the report.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="HtmlReport"/> so that escaping is one decision made once rather than a
/// habit applied unevenly. Every value that reaches the page goes through <see cref="Text"/>, and
/// the report file contains no other way to emit a string — which is checkable by reading it, and
/// is the only reason a renderer interpolating type names from real source can be trusted.
/// </para>
/// <para>
/// <b>Type names are hostile input in exactly one direction.</b> Generic arity renders as
/// <c>List&lt;T&gt;</c>, and an unescaped angle bracket does not corrupt a page so much as silently
/// eat the rest of a line — which would look like a rendering bug and hide a real component.
/// </para>
/// <para>
/// Public rather than internal so <see cref="Text"/> can be asserted directly. It is the one piece
/// here whose failure is silent, and testing it only through a rendered page means testing it
/// against whatever characters the fixture happens to contain. <c>Bearing.Cli</c> packs as a tool
/// and not as a library, so nothing about its surface is a contract — see its csproj.
/// </para>
/// </remarks>
public static class Html
{
    /// <summary>Escapes text for element content and for a quoted attribute value.</summary>
    /// <remarks>
    /// Both contexts, one method. Splitting them is how a value ends up escaped for the wrong one:
    /// the difference is the quote characters, and including them costs nothing in element content.
    /// </remarks>
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var escaped = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            switch (c)
            {
                case '&': escaped.Append("&amp;"); break;
                case '<': escaped.Append("&lt;"); break;
                case '>': escaped.Append("&gt;"); break;
                case '"': escaped.Append("&quot;"); break;
                case '\'': escaped.Append("&#39;"); break;
                default: escaped.Append(c); break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>A number, in the invariant culture, with trailing zeros trimmed.</summary>
    public static string Number(double value) =>
        double.IsPositiveInfinity(value) ? "∞"
        : double.IsNaN(value) ? ""
        : value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A whole number with thousands separators, for counts a reader scans.</summary>
    public static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// An identifier safe to use as an anchor target, derived from a subject's canonical form.
    /// </summary>
    /// <remarks>
    /// Derived rather than invented, so a link and its target cannot disagree, and hashed down to
    /// a short token because a canonical member id is long enough to make the document noticeably
    /// larger when it appears twice per finding. Ordinal-stable: the same subject produces the same
    /// anchor on every run, which matters for a file somebody bookmarks a section of.
    /// </remarks>
    public static string Anchor(string canonical)
    {
        // FNV-1a. Not cryptographic and does not need to be: a collision produces two findings
        // sharing an anchor, which is a navigation annoyance, not a wrong claim. Chosen over
        // slugifying the canonical form because that is neither short nor reliably unique.
        var hash = 2166136261;
        foreach (var c in canonical)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return "c" + hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}

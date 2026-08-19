namespace IronMarten.Bearing.Cli;

/// <summary>
/// The report's stylesheet, inlined into the page.
/// </summary>
/// <remarks>
/// <para>
/// <b>No external request, ever</b> — <c>TECHREQ-job-a.md</c> §6. Corporate networks block them and
/// the artifact has to work offline, so there is no CDN, no webfont and no remote image here. That
/// is also why the font stack is the system one: a report that waits on a font is a report that
/// renders twice, and the second render is the one the screenshot misses.
/// </para>
/// <para>
/// <b>Kept small on purpose.</b> Bundle size is a real budget when the page also has to carry the
/// data, and every rule here is one a section actually uses. The layout is CSS grid and flexbox
/// with no framework; collapsing is <c>&lt;details&gt;</c> rather than script, which means the
/// page has no JavaScript at all — it prints, it works with script disabled, and there is nothing
/// in it a corporate proxy can object to.
/// </para>
/// <para>
/// <b>Both colour schemes.</b> The palette is defined as custom properties on <c>:root</c> and
/// redefined under <c>prefers-color-scheme: dark</c>, so the artifact matches whoever opens it
/// rather than whoever generated it.
/// </para>
/// </remarks>
internal static class HtmlStyle
{
    /// <summary>The whole stylesheet.</summary>
    internal const string Css = """
:root{--bg:#fbfbfa;--panel:#fff;--ink:#1a1a1a;--muted:#6b6b6b;--line:#e3e3e0;--accent:#8a5a2b;
--accent-soft:#f5ede3;--ok:#3d6b47;--warn:#8a6d1f;--mono:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace}
@media(prefers-color-scheme:dark){:root{--bg:#16171a;--panel:#1d1e22;--ink:#e9e8e6;--muted:#9a9a97;
--line:#2e3036;--accent:#d8a76a;--accent-soft:#2a2320;--ok:#8fc79c;--warn:#d9bd6a}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.55 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
.wrap{max-width:60rem;margin:0 auto;padding:2.5rem 1.25rem 5rem}
h1{font-size:1.6rem;margin:0 0 .25rem;letter-spacing:-.01em}
h2{font-size:1.15rem;margin:2.75rem 0 .5rem;padding-bottom:.35rem;border-bottom:1px solid var(--line)}
h3{font-size:.95rem;margin:1.5rem 0 .5rem;color:var(--muted);text-transform:uppercase;letter-spacing:.06em}
p{margin:.5rem 0}
a{color:var(--accent)}
code,.mono{font-family:var(--mono);font-size:.87em}
.sub{color:var(--muted);font-size:.9rem}
.lede{color:var(--muted);max-width:44rem}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(10rem,1fr));gap:.5rem;margin:1.25rem 0}
.tile{background:var(--panel);border:1px solid var(--line);border-radius:.5rem;padding:.6rem .85rem}
.tile b{display:block;font-size:1.35rem;font-weight:600;letter-spacing:-.02em}
.tile .tl{display:block;color:var(--muted);font-size:.72rem;text-transform:uppercase;letter-spacing:.05em}
.tile .tn{display:block;color:var(--muted);font-size:.78rem;line-height:1.35;margin-top:.35rem}
table{width:100%;border-collapse:collapse;font-size:.9rem;margin:.5rem 0}
th,td{text-align:left;padding:.4rem .5rem;border-bottom:1px solid var(--line);vertical-align:top}
th{color:var(--muted);font-weight:600;font-size:.78rem;text-transform:uppercase;letter-spacing:.05em}
td.n,th.n{text-align:right;font-variant-numeric:tabular-nums}
.scroll{overflow-x:auto}
.picture{margin:1.25rem 0 .5rem;border:1px solid var(--line);border-radius:.5rem;overflow:hidden}
.picture svg{display:block;width:100%;height:auto}
.card{background:var(--panel);border:1px solid var(--line);border-radius:.6rem;padding:.9rem 1rem;margin:.6rem 0}
.card>h4{margin:0 0 .15rem;font-size:1rem}
.card.lead{border-left:3px solid var(--accent)}
.anat,.row{display:grid;grid-template-columns:9.5rem minmax(0,1fr);gap:.35rem 1rem;align-items:baseline}
.anat{margin:1.5rem 0 .5rem}
.lbl{text-align:right;color:var(--accent);font-size:.72rem;line-height:1.3}
.fld{min-width:0}
.anat .name{font-size:1.3rem;margin-right:.5rem}
.name{font-weight:600}
.rail{margin:1.5rem 0 0;border-top:1px solid var(--line)}
.row{padding:.9rem 0;border-bottom:1px solid var(--line)}
.kind{display:block;color:var(--accent);font-size:.76rem;line-height:1.25}
.rank{display:block;color:var(--muted);font-size:.72rem}
.big{font-size:1.05rem;line-height:1.45;max-width:52ch}
@media(max-width:38rem){.anat,.row{grid-template-columns:1fr;gap:.2rem}.lbl{text-align:left}}
.claim{margin:.15rem 0 .5rem}
.where{color:var(--muted);font-size:.82rem;font-family:var(--mono)}
.tags{display:flex;flex-wrap:wrap;gap:.3rem;margin:.5rem 0 0}
.tag{background:var(--accent-soft);color:var(--accent);border-radius:.3rem;padding:.1rem .45rem;font-size:.75rem}
details{margin:.5rem 0 0}
summary{cursor:pointer;color:var(--muted);font-size:.82rem}
summary:hover{color:var(--accent)}
.receipts{margin:.4rem 0 0;font-size:.85rem}
.receipts td:first-child{font-family:var(--mono)}
.empty{color:var(--muted);font-style:italic}
.note{border-left:3px solid var(--accent);background:var(--accent-soft);padding:.6rem .85rem;border-radius:0 .4rem .4rem 0;margin:.75rem 0;font-size:.9rem}
.loop{font-family:var(--mono);font-size:.82rem;color:var(--muted);margin:.15rem 0 .6rem}
footer{margin-top:4rem;padding-top:1rem;border-top:1px solid var(--line);color:var(--muted);font-size:.82rem}
@media print{body{background:#fff}.card,.tile,.anat,.row{break-inside:avoid}details{display:none}}
""";
}

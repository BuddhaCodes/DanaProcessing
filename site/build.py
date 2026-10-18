#!/usr/bin/env python3
"""
Builds the documentation website from site/content.

    python site/build.py

Sources (edit these):
  site/content/<id>.html   one reference topic each; first line is
                           <!--meta {"title", "nav", "summary", "tags"} -->
  site/site.json           the groups and the order of the topics
  site/keywords.json       the A-Z keyword index (name, kind, target id)
  site/home.html           the home page body
  assets/site.css, assets/site.js   the look and the behavior (hand-written)

Output (commit it, GitHub Pages serves it as is):
  index.html, reference/<id>.html, reference/a-z.html,
  assets/search-index.js, assets/anchors.js, sitemap.xml

Every link to "#some-id" in a topic is pointed at the page that now holds
that id, and old links to the single-page site (index.html#fill) are sent
to their new page by assets/anchors.js.
"""
import datetime
import html
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
SITE_URL = "https://buddhacodes.github.io/DanaProcessing/"
PAYPAL = "https://paypal.me/CarlosFernandez934"
GITHUB = "https://github.com/BuddhaCodes/DanaProcessing"
RELEASES = GITHUB + "/releases/latest"


# ----------------------------------------------------------------------------
# Loading
# ----------------------------------------------------------------------------

def load_pages():
    groups = json.loads((HERE / "site.json").read_text(encoding="utf-8"))["groups"]
    pages, order = {}, []
    for g in groups:
        for pid in g["pages"]:
            text = (HERE / "content" / f"{pid}.html").read_text(encoding="utf-8")
            m = re.match(r"<!--meta (\{.*?\}) -->\n", text)
            if not m:
                raise SystemExit(f"site/content/{pid}.html: first line must be <!--meta {{...}} -->")
            meta = json.loads(m.group(1))
            pages[pid] = {"id": pid, "group": g["title"], "body": text[m.end():], **meta}
            order.append(pid)
    return groups, pages, order


def strip_tags(s):
    return html.unescape(re.sub(r"<[^>]+>", "", s))


def esc(s):
    return html.escape(s, quote=True)


# ----------------------------------------------------------------------------
# Turning a topic's old markup into the new page's
# ----------------------------------------------------------------------------

SIG_RE = re.compile(r'<span class="sig-return">.*?</span>', re.S)


def split_top(h):
    """Splits a title on ' · ' but never inside a <span class="sig-return">."""
    keep = []
    h = SIG_RE.sub(lambda m: keep.append(m.group(0)) or f"\x00{len(keep) - 1}\x00", h)
    parts = [p.strip() for p in re.split(r"\s+·\s+", h) if p.strip()]
    return [re.sub(r"\x00(\d+)\x00", lambda m: keep[int(m.group(1))], p) for p in parts]


def api_part(t):
    t0 = t.split("→")[0].strip()
    return bool(re.match(r"^[\w.<>&;]+\s*(\(|$)", t0)) or t0.startswith(("[", "//")) \
        or bool(re.match(r"^(struct|class|enum|record)\s+\w+$", t0))


def part_name(t):
    t0 = t.split("→")[0].strip()
    if t0.startswith("["):
        return t0[: t0.index("]") + 1]
    if t0.startswith("//") or re.match(r"^(struct|class|enum|record)\s", t0):
        return t0
    return re.split(r"[\s(]", t0)[0]


def entry_heading(m):
    """<h3 class="entry-title" id="x">Fill(a, b) · NoFill()</h3> -> a short h2 plus the signatures as a list."""
    eid, inner = m.group(1), m.group(2).strip()
    parts = split_top(inner)
    modifiers = [p for p in parts if re.fullmatch(r"[a-z]+", strip_tags(p).strip())]
    sigs = [p for p in parts if p not in modifiers]
    texts = [strip_tags(SIG_RE.sub("", p)).strip() for p in sigs]
    badges = "".join(f'<span class="badge">{esc(strip_tags(b))}</span>' for b in modifiers)
    if sigs and all(api_part(t) for t in texts):
        names = []
        for t in texts:
            n = part_name(t)
            if n not in names:
                names.append(n)
        title = " · ".join(names)
        lone = len(sigs) == 1 and texts[0] == title and "sig-return" not in sigs[0]
        sig_list = "" if lone else (
            '<ul class="sigs" aria-label="Signatures">'
            + "".join(f"<li><code>{s}</code></li>" for s in sigs) + "</ul>")
        return (f'<h2 class="entry-title api" id="{eid}"><code>{esc(title)}</code>{badges}</h2>'
                + sig_list)
    # A prose heading ("Runnable example", "Hot Reload"): a sig-return becomes a badge.
    prose = SIG_RE.sub(lambda s: f'<span class="badge">{esc(strip_tags(s.group(0)))}</span>', inner)
    return f'<h2 class="entry-title" id="{eid}">{prose}{badges}</h2>'


PLAY_SVG = ('<svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true" fill="currentColor">'
            '<path d="M7 4l13 8-13 8z"></path></svg>')


def code_blocks(body, pid):
    counter = [0]

    def block(m):
        attrs, code, button = m.group(1) or "", m.group(2), m.group(3)
        idm = re.search(r'id="([^"]+)"', attrs)
        if idm:
            cid = idm.group(1)
        else:
            counter[0] += 1
            cid = f"{pid}-code-{counter[0]}"
        run = ""
        if button:
            run = (f'<button class="dana-test-btn" data-target="{cid}" type="button">'
                   f'{PLAY_SVG}<span class="dana-test-label">Run in Dana</span></button>')
        return (f'<div class="code-block"><div class="code-bar"><span class="code-label">C#</span>'
                f'<div class="code-actions"><button type="button" class="copy-btn" data-target="{cid}">Copy code</button>{run}</div></div>'
                f'<pre id="{cid}" tabindex="0" aria-label="Code, scrollable">{code}</pre></div>')

    return re.sub(
        r'<div class="code-block">\s*<pre([^>]*)>(.*?)</pre>\s*(<button class="dana-test-btn".*?</button>)?\s*</div>',
        block, body, flags=re.S)


def label_tables(body):
    """Each wide table scrolls in a focusable region, named after its caption or its header row."""
    used = {}

    def wrap(m):
        table = m.group(1)
        cap = re.search(r"<caption>(.*?)</caption>", table, re.S)
        if cap:
            name = strip_tags(cap.group(1)).strip()
        else:
            heads = [strip_tags(h).strip() for h in re.findall(r"<th[^>]*>(.*?)</th>", table, re.S)[:3]]
            name = "Table: " + ", ".join(h for h in heads if h) if any(heads) else "Table"
        used[name] = used.get(name, 0) + 1
        if used[name] > 1:
            name += f" ({used[name]})"
        return f'<div class="table-wrap" role="region" tabindex="0" aria-label="{esc(name)}">{table}'

    return re.sub(r'<div class="table-wrap">(\s*<table.*?</table>)', wrap, body, flags=re.S)


def transform(body, pid, anchor_page):
    body = re.sub(r'<h3 class="entry-title" id="([^"]+)">(.*?)</h3>', entry_heading, body, flags=re.S)
    body = code_blocks(body, pid)
    # any other <pre>: reachable and scrollable from the keyboard
    body = re.sub(r"<pre(?![^>]*tabindex)([^>]*)>", r'<pre\1 tabindex="0" aria-label="Code, scrollable">', body)
    body = label_tables(body)
    body = re.sub(r'<i class="ph[^"]*"></i>', "", body)

    def href(m):
        target = m.group(1)
        if target == "index":
            return 'href="a-z.html"'
        page = anchor_page.get(target)
        if page is None:
            raise SystemExit(f"{pid}: link to #{target}, which no page has")
        if page == pid:
            return f'href="#{target}"'
        return f'href="{page}.html"' if target == page else f'href="{page}.html#{target}"'

    return re.sub(r'href="#([^"]+)"', href, body)


def first_intro(body):
    """The topic's first intro paragraph moves up into the page header."""
    m = re.search(r'<p class="section-intro">(.*?)</p>\s*', body, re.S)
    if not m or body[: m.start()].strip():
        return "", body
    return m.group(1), body[: m.start()] + body[m.end():]


# ----------------------------------------------------------------------------
# Shared pieces
# ----------------------------------------------------------------------------

HEART = ('<svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true" fill="currentColor">'
         '<path d="M12 21s-7.5-4.6-9.6-9.4C1 8.2 3.1 4.5 6.8 4.5c2.2 0 3.7 1.3 5.2 3 1.5-1.7 3-3 5.2-3 3.7 0 5.8 3.7 4.4 7.1C19.5 16.4 12 21 12 21z"></path></svg>')
LOGO = ('<svg width="30" height="30" viewBox="0 0 30 30" aria-hidden="true"><circle cx="15" cy="15" r="13" fill="none" stroke="currentColor" stroke-width="2"></circle>'
        '<circle cx="15" cy="15" r="6" fill="#E7B66B"></circle><circle cx="23" cy="8" r="2.5" fill="#FF8A6B"></circle></svg>')
SEARCH_ICON = ('<svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">'
               '<circle cx="11" cy="11" r="7"></circle><path d="M20 20l-3.5-3.5"></path></svg>')
GROUP_COLORS = {"Start here": "#F4EBDD", "Basics": "#E7B66B", "Drawing": "#FF8A6B", "Media": "#9DB8F2",
                "Data & math": "#8FD3B6", "3D": "#9DB8F2", "Sound": "#8FD3B6",
                "Live & generative": "#FF8A6B", "Tooling": "#E7B66B"}


# Google Analytics (GA4). The tag is NOT in the page: site.js loads it only
# after the visitor accepts the banner (and never on a "Global Privacy
# Control"/"Do Not Track" browser unless they opt in from the footer).
ANALYTICS_ID = "G-N4ZWC7GN1B"


def head(title, description, canonical, root, extra=""):
    return f"""<!DOCTYPE html>
<html lang="en" data-root="{root}">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<!-- Google Analytics only loads after the visitor says yes (assets/site.js, "Analytics: only with permission"). -->
<meta name="dana-analytics" content="{ANALYTICS_ID}">
<title>{esc(title)}</title>
<meta name="description" content="{esc(description)}">
<link rel="canonical" href="{canonical}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="DanaProcessing">
<meta property="og:title" content="{esc(title)}">
<meta property="og:description" content="{esc(description)}">
<meta property="og:url" content="{canonical}">
<meta property="og:image" content="{SITE_URL}og-image.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta property="og:locale" content="en_US">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:image" content="{SITE_URL}og-image.png">
<meta name="theme-color" content="#13202A">
<link rel="icon" type="image/png" href="{root}DanaProcessing.Ide/Theme/dana.png">
<link rel="apple-touch-icon" href="{root}DanaProcessing.Ide/Theme/dana.png">
{extra}<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Atkinson+Hyperlegible:ital,wght@0,400;0,700;1,400&amp;family=Bricolage+Grotesque:opsz,wght@12..96,600;12..96,800&amp;family=JetBrains+Mono:wght@400;500&amp;display=swap" rel="stylesheet">
<link rel="stylesheet" href="{root}assets/site.css">
<script src="{root}assets/search-index.js" defer></script>
<script src="{root}assets/site.js" defer></script>
</head>
"""


def header(root, current):
    def nav_link(label, href, key):
        cur = ' aria-current="page"' if key == current else ""
        return f'<a href="{href}"{cur}>{label}</a>'
    return f"""<a class="skip" href="#main">Skip to content</a>
<header class="site-header">
  <div class="bar">
    <a class="brand" href="{root}index.html">{LOGO}<span>DanaProcessing</span></a>
    <button type="button" class="menu-toggle" aria-expanded="false" aria-controls="site-menu">Menu</button>
    <div class="site-menu" id="site-menu">
      <nav class="main-nav" aria-label="Main">
        {nav_link("Learn", root + "reference/tutorial.html", "learn")}
        {nav_link("Reference", root + "reference/a-z.html", "reference")}
        {nav_link("Examples", root + "index.html#gallery", "examples")}
        {nav_link("The IDE", root + "reference/ide.html", "ide")}
      </nav>
      <form class="search" role="search" action="{root}reference/a-z.html">
        <label class="visually-hidden" for="site-search">Search the documentation</label>
        <div class="search-box">
          {SEARCH_ICON}
          <input id="site-search" name="q" type="search" autocomplete="off" placeholder="Search — press /" aria-describedby="search-status" aria-controls="search-results">
        </div>
        <p id="search-status" class="visually-hidden" aria-live="polite"></p>
        <ul id="search-results" class="search-results" hidden></ul>
      </form>
      <a class="support-link" href="{root}index.html#support">{HEART}<span>Support Dana</span></a>
    </div>
  </div>
</header>
"""


def footer(root):
    return f"""<footer class="site-footer">
  <div class="wrap">
    <p>DanaProcessing · v1.0 RC · MIT · stands on the shoulders of <a href="https://processing.org">Processing</a>, <a href="https://p5js.org">p5.js</a> and <a href="https://sonic-pi.net">Sonic Pi</a>.</p>
    <nav aria-label="Footer">
      <a href="{root}reference/a-z.html">All topics &amp; A–Z</a>
      <a href="{RELEASES}">Releases</a>
      <a href="{GITHUB}">GitHub</a>
      <a href="{root}index.html#support">Support</a>
      <button type="button" class="footer-link" data-privacy-choices hidden>Privacy choices</button>
    </nav>
  </div>
</footer>
</body>
</html>
"""


def support_aside(root):
    return f"""<aside class="support-aside" aria-label="Support the project">
  <p><strong>Found this useful?</strong> Dana is free and built in spare time — a donation buys more time for docs like this one.</p>
  <a class="btn btn-ink" href="{PAYPAL}">{HEART}<span>Donate via PayPal</span></a>
</aside>
"""


# ----------------------------------------------------------------------------
# Pages
# ----------------------------------------------------------------------------

def topic_nav(groups, pages, current, entries):
    out = ['<nav class="topics" id="topics" aria-label="Reference topics">']
    for g in groups:
        out.append(f'<div class="topic-group"><h2>{esc(g["title"])}</h2><ul>')
        for pid in g["pages"]:
            p = pages[pid]
            if pid == current:
                out.append(f'<li><a href="{pid}.html" aria-current="page">{esc(p["nav"])}</a>')
                if entries:
                    out.append('<ul class="on-this-page" aria-label="On this page">')
                    out += [f'<li><a href="#{eid}">{label}</a></li>' for eid, label in entries]
                    out.append("</ul>")
                out.append("</li>")
            else:
                out.append(f'<li><a href="{pid}.html">{esc(p["nav"])}</a></li>')
        out.append("</ul></div>")
    out.append('<p class="topics-all"><a href="a-z.html">All topics &amp; A–Z index</a></p></nav>')
    return "\n".join(out)


def entries_of(body):
    found = []
    for m in re.finditer(r'<h2 class="entry-title[^"]*" id="([^"]+)">(.*?)</h2>', body, re.S):
        inner = m.group(2)
        code = re.match(r"<code>(.*?)</code>", inner)
        label = code.group(1) if code else re.sub(r'<span class="badge">.*?</span>', "", inner)
        label = strip_tags(label).strip()
        found.append((m.group(1), f"<code>{esc(label)}</code>" if code else esc(label)))
    return found


def render_topic(groups, pages, order, pid, anchor_page):
    p = pages[pid]
    intro, body = first_intro(p["body"])
    body = transform(body, pid, anchor_page)
    if intro:
        intro = transform(intro, pid, anchor_page)
    entries = entries_of(body)
    i = order.index(pid)
    prev_id = order[i - 1] if i > 0 else None
    next_id = order[i + 1] if i + 1 < len(order) else None
    color = GROUP_COLORS.get(p["group"], "#E7B66B")
    tags = "".join(f'<span class="tag">{esc(t)}</span>' for t in p.get("tags", []))
    pager = ['<nav class="pager" aria-label="Previous and next topic">']
    if prev_id:
        pager.append(f'<a class="prev" href="{prev_id}.html"><span>← Previous</span><strong>{esc(pages[prev_id]["title"])}</strong></a>')
    if next_id:
        pager.append(f'<a class="next" href="{next_id}.html"><span>Next →</span><strong>{esc(pages[next_id]["title"])}</strong></a>')
    pager.append("</nav>")
    on_page = ""
    if entries:
        on_page = ('<details class="on-this-page-mobile"><summary>On this page · '
                   f'{len(entries)} {"entry" if len(entries) == 1 else "entries"}</summary><ul>'
                   + "".join(f'<li><a href="#{eid}">{label}</a></li>' for eid, label in entries)
                   + "</ul></details>")
    title = f'{p["title"]} — DanaProcessing reference'
    out = head(title, p["summary"], f'{SITE_URL}reference/{pid}.html', "../")
    out += '<body class="page-topic">\n' + header("../", "learn" if p["group"] == "Start here" else "ide" if pid == "ide" else "reference")
    out += f"""<main id="main">
<div class="topic-hero">
  <div class="wrap">
    <div class="topic-hero-text">
      <nav class="breadcrumb" aria-label="Breadcrumb"><ol><li><a href="a-z.html">Reference</a></li><li>{esc(p["group"])}</li><li aria-current="page">{esc(p["title"])}</li></ol></nav>
      <h1>{esc(p["title"])}</h1>
      {f'<p class="lede">{intro}</p>' if intro else ''}
      {f'<p class="tags">{tags}</p>' if tags else ''}
    </div>
    <div class="topic-art" aria-hidden="true"><span style="background:{color}"></span><span></span><span></span><span></span><span></span></div>
  </div>
</div>
<div class="wrap topic-layout">
  <button type="button" class="topics-toggle" aria-expanded="false" aria-controls="topics">All topics</button>
{topic_nav(groups, pages, pid, entries)}
  <div class="topic-main">
{on_page}
<div class="doc">
{body}
</div>
{"".join(pager)}
{support_aside("../")}
  </div>
</div>
</main>
"""
    out += footer("../")
    return out


def render_az(groups, pages, keywords, anchor_page):
    topics = []
    for g in groups:
        links = "".join(f'<li><a href="{pid}.html">{esc(pages[pid]["title"])}</a></li>' for pid in g["pages"])
        topics.append(f'<section class="az-group"><h3>{esc(g["title"])}</h3><ul>{links}</ul></section>')
    by_letter = {}
    for k in sorted(keywords, key=lambda k: k["name"].lower()):
        letter = k["name"].lstrip("[/").upper()[:1]
        letter = letter if letter.isalpha() else "#"
        by_letter.setdefault(letter, []).append(k)
    letters = []
    for letter, ks in sorted(by_letter.items()):
        items = []
        for k in ks:
            t = k["target"]
            page = anchor_page[t]
            url = f"{page}.html" if t == page else f"{page}.html#{t}"
            items.append(f'<li data-key="{esc(k["name"].lower())}"><a href="{url}"><code>{esc(k["name"])}</code></a> <span class="kind">{esc(k["kind"])}</span></li>')
        letters.append(f'<section class="az-letter" aria-labelledby="az-{letter}"><h3 id="az-{letter}">{letter}</h3><ul>{"".join(items)}</ul></section>')
    out = head("All topics and the A–Z index — DanaProcessing", "Every topic of the DanaProcessing reference and every function, class and enum, A to Z.",
               f"{SITE_URL}reference/a-z.html", "../")
    out += '<body class="page-topic">\n' + header("../", "reference")
    out += f"""<main id="main">
<div class="topic-hero">
  <div class="wrap">
    <div class="topic-hero-text">
      <h1>The whole reference</h1>
      <p class="lede">{len(pages)} topics and {len(keywords)} names. Filter the list, or press <kbd>/</kbd> anywhere to search.</p>
    </div>
  </div>
</div>
<div class="wrap az">
  <h2>Topics</h2>
  <div class="az-topics">{"".join(topics)}</div>
  <h2 id="a-z">A–Z</h2>
  <div class="az-filter">
    <label for="az-q">Filter names</label>
    <input id="az-q" type="search" autocomplete="off" aria-describedby="az-count">
    <p id="az-count" aria-live="polite">{len(keywords)} names</p>
  </div>
  <div class="az-letters">{"".join(letters)}</div>
{support_aside("../")}
</div>
</main>
"""
    out += footer("../")
    return out


def render_home(pages, anchor_page):
    body = (HERE / "home.html").read_text(encoding="utf-8")

    def ref(m):
        t = m.group(1)
        page = anchor_page[t]
        return f'href="reference/{page}.html"' if t == page else f'href="reference/{page}.html#{t}"'

    body = re.sub(r'href="ref:([^"]+)"', ref, body)
    body = body.replace("{{PAYPAL}}", PAYPAL).replace("{{GITHUB}}", GITHUB).replace("{{RELEASES}}", RELEASES)
    out = head("DanaProcessing — Creative coding in C# (2D, 3D & live-coded sound)",
               "Creative coding in C#, the Processing way: 2D, real 3D and a live-coding audio engine, MIDI and microphone input, seeds and plotter export, with its own desktop IDE. Free, MIT.",
               SITE_URL, "./",
               extra='<script>if(location.hash.length>1){var s=document.createElement("script");s.src="assets/anchors.js";document.head.appendChild(s);}</script>\n')
    out += '<body class="page-home">\n' + header("./", "") + body + footer("./")
    return out


# ----------------------------------------------------------------------------

def main():
    groups, pages, order = load_pages()
    keywords = json.loads((HERE / "keywords.json").read_text(encoding="utf-8"))

    anchor_page = {}
    for pid, p in pages.items():
        for aid in [pid] + re.findall(r'\bid="([^"]+)"', p["body"]):
            if aid in anchor_page and anchor_page[aid] != pid:
                raise SystemExit(f'id "{aid}" is on both {anchor_page[aid]} and {pid}')
            anchor_page[aid] = pid
    anchor_page["index"] = "a-z"
    for k in keywords:
        if k["target"] not in anchor_page:
            raise SystemExit(f'keyword {k["name"]}: #{k["target"]} is on no page')

    (ROOT / "reference").mkdir(exist_ok=True)
    search = []
    for pid in order:
        html_text = render_topic(groups, pages, order, pid, anchor_page)
        (ROOT / "reference" / f"{pid}.html").write_text(html_text, encoding="utf-8")
        search.append([pages[pid]["title"], f"reference/{pid}.html", "topic", pages[pid]["group"]])
        for eid, label in entries_of(transform(pages[pid]["body"], pid, anchor_page)):
            search.append([strip_tags(label), f"reference/{pid}.html#{eid}", "", pages[pid]["title"]])
    seen = {(s[0], s[1]) for s in search}
    for k in keywords:
        page = anchor_page[k["target"]]
        url = f"reference/{page}.html" + ("" if k["target"] == page else f'#{k["target"]}')
        if (k["name"], url) not in seen:
            search.append([k["name"], url, k["kind"], pages[page]["title"]])
    (ROOT / "reference" / "a-z.html").write_text(render_az(groups, pages, keywords, anchor_page), encoding="utf-8")
    (ROOT / "index.html").write_text(render_home(pages, anchor_page), encoding="utf-8")

    (ROOT / "assets").mkdir(exist_ok=True)
    (ROOT / "assets" / "search-index.js").write_text(
        "// Generated by site/build.py: [label, url, kind, topic]\nwindow.DANA_INDEX = "
        + json.dumps(search, ensure_ascii=False, separators=(",", ":")) + ";\n", encoding="utf-8")
    old = {aid: (f"reference/{page}.html" if aid == page else f"reference/{page}.html#{aid}")
           for aid, page in anchor_page.items()}
    old["index"] = "reference/a-z.html"
    (ROOT / "assets" / "anchors.js").write_text(
        "// Generated by site/build.py: the old one-page site's #anchors -> their page now.\n"
        "(function(){var m=" + json.dumps(old, separators=(",", ":")) +
        ";var t=m[decodeURIComponent(location.hash.slice(1))];if(t)location.replace(t);})();\n", encoding="utf-8")

    today = datetime.date.today().isoformat()
    urls = [SITE_URL] + [f"{SITE_URL}reference/{pid}.html" for pid in order] + [f"{SITE_URL}reference/a-z.html"]
    (ROOT / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
        + "".join(f"  <url>\n    <loc>{u}</loc>\n    <lastmod>{today}</lastmod>\n  </url>\n" for u in urls)
        + "</urlset>\n", encoding="utf-8")
    print(f"built {len(order)} topics, a-z, home; {len(search)} search entries")


if __name__ == "__main__":
    main()

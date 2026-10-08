#!/usr/bin/env python3
"""Pre-release checker for the Sholto marketing site (stdlib only).

Usage:  python3 site/check.py [site_dir]      (default: the directory holding this file)
Exit code 0 = all checks pass, 1 = any failure.

Checks
------
1. Links: every local src, srcset candidate, poster, href and <source src> in
   index.html (plus url(...) in linked local CSS) must resolve to an existing
   file under the site dir. Ignored: http(s)://, //host, mailto:, tel:, data:,
   javascript:. Query/fragment are stripped; a leading "/" means site root.
2. Anchors: every href="#x" must match an id="x" (or name="x") in the page.
   Bare "#" is ignored.
3. First-view weight <= 600 KB. Definition: index.html + every local CSS and
   JS file it references (anywhere) + every other local asset a browser
   fetches for the hero, i.e. referenced before the hero ends. The hero is the
   first element whose id/class contains "hero"; the cutoff is that element's
   end tag (whatever its tag). If no
   hero marker exists, the first <section> is taken to be the hero and the
   cutoff is the next <section> start tag. Markup after the cutoff is below
   the fold even when it is not a <section> (e.g. <article> blocks). Hrefs to
   other pages/downloads do not count as first-view assets.
   Media model (what a browser really fetches):
     <video>    first <source src> + poster. Later <source>s and the <img>
                fallback inside <video> are never fetched by a browser that
                plays the first source, so they are not first-view.
     <picture> / srcset   only the single largest candidate on disk counts
                (all <source srcset>s, the <img src> and <img srcset> of one
                <picture>, or all candidates of one standalone <img srcset>).
   Files excluded from first view this way still count toward the total and
   their own per-asset budget.
4. Total weight <= 3 MB: index.html + every unique local file referenced.
5. Per-asset budgets, classified by extension:
     video  (.mp4 .webm .mov)        <= 300 KB
     animated image: .gif, or .webp containing an ANIM chunk, or an
       animated .png (acTL chunk)    <= 400 KB
     still  (.webp without ANIM, .png, .jpg/.jpeg, .avif, .svg, .ico)
                                      <= 120 KB
   Other types (css, js, fonts, pdf...) have no per-asset budget.

Sizes are raw bytes on disk (not gzipped); KB = 1024 bytes.
"""
import re
import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

KB = 1024
FIRST_VIEW_BUDGET = 600 * KB
TOTAL_BUDGET = 3 * 1024 * KB
VIDEO_BUDGET, ANIM_BUDGET, STILL_BUDGET = 300 * KB, 400 * KB, 120 * KB
VIDEO_EXT = {".mp4", ".webm", ".mov"}
STILL_EXT = {".png", ".jpg", ".jpeg", ".avif", ".svg", ".ico", ".webp"}
IGNORED = re.compile(r"^(?:[a-z][a-z0-9+.-]*:|//)", re.I)  # any scheme or //host
LOCAL_SCHEMES_OK = ()  # no scheme counts as local


class Page(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.refs = []          # (url, tag, attr, in_first_view)
        self.ids = set()
        self.anchors = []       # href="#x"
        self.n_sections = 0
        self.hero_seen = False
        self.hero_is_section = False
        self.cutoff_hit = False
        self._sections_after_hero = 0
        self.stack = []         # open non-void tags
        self.hero_depth = None  # len(stack) once hero is pushed
        self.video = None       # dict(first_source=bool) while inside <video>
        self.picture = None     # group id while inside <picture>
        self._gid = 0

    VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link",
            "meta", "source", "track", "wbr", "param"}

    def handle_endtag(self, tag):
        if tag in self.VOID or tag not in self.stack:
            return
        while self.stack:
            t = self.stack.pop()
            if t == tag:
                break
        if tag == "video":
            self.video = None
        if tag == "picture":
            self.picture = None
        if (self.hero_depth is not None and len(self.stack) < self.hero_depth
                and not self.cutoff_hit):
            self.cutoff_hit = True              # hero element closed

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag not in self.VOID:
            self.stack.append(tag)
        for k in ("id", "name"):
            if a.get(k):
                self.ids.add(a[k])
        marker = f"{a.get('id') or ''} {a.get('class') or ''}".lower()
        if not self.hero_seen and "hero" in marker:
            self.hero_seen = True
            self.hero_is_section = tag == "section"
            if tag not in self.VOID:
                self.hero_depth = len(self.stack)
        if tag == "section":
            self.n_sections += 1
            if not self.cutoff_hit and not self.hero_seen and self.n_sections == 2:
                self.cutoff_hit = True          # no marker: 1st section is hero
        first = not self.cutoff_hit
        # the cutoff section's own attributes are below the fold
        if tag == "section" and self.cutoff_hit:
            first = False
        if tag == "video":
            self.video = {"source_seen": False}
        if tag == "picture":
            self._gid += 1
            self.picture = self._gid
        group = None
        if self.picture is not None and tag in ("source", "img"):
            group = self.picture
        elif tag == "img" and a.get("srcset"):
            self._gid += 1
            group = self._gid
        for attr in ("src", "href", "poster"):
            v = a.get(attr)
            if v is None:
                continue
            v = v.strip()
            if attr == "href" and v.startswith("#"):
                if len(v) > 1:
                    self.anchors.append(v[1:])
                continue
            f = first
            if self.video is not None and tag in ("source", "img") and attr == "src":
                if tag == "img" or self.video["source_seen"]:
                    f = False                    # fallback / later source: not fetched
                else:
                    self.video["source_seen"] = True
            self.refs.append((v, tag, attr, f, group))
        for attr in ("srcset", "imagesrcset"):
            if a.get(attr):
                for cand in a[attr].split(","):
                    toks = cand.strip().split()
                    if toks:
                        self.refs.append((toks[0], tag, "srcset", first, group))


def local_path(root, url):
    """Return Path for a local reference, or None if it is external/ignorable."""
    if not url or IGNORED.match(url):
        return None
    p = unquote(urlsplit(url).path)
    if not p:
        return None
    return (root / p.lstrip("/")) if p.startswith("/") else (root / p)


def classify(path):
    ext = path.suffix.lower()
    try:
        head = path.read_bytes()[:65536] if path.exists() else b""
    except OSError:
        head = b""
    if ext in VIDEO_EXT:
        return "video", VIDEO_BUDGET
    if ext == ".gif":
        return "anim", ANIM_BUDGET
    if ext == ".webp":
        return ("anim", ANIM_BUDGET) if b"ANIM" in head else ("still", STILL_BUDGET)
    if ext in (".png", ".apng") and b"acTL" in head:
        return "anim", ANIM_BUDGET
    if ext in STILL_EXT:
        return "still", STILL_BUDGET
    return "other", None


def fmt(n):
    return f"{n / KB:8.1f} KB"


def main(argv):
    root = Path(argv[1] if len(argv) > 1 else Path(__file__).parent).resolve()
    index = root / "index.html"
    if not index.is_file():
        print(f"FAIL: {index} not found")
        return 1
    page = Page()
    page.feed(index.read_text(encoding="utf-8", errors="replace"))
    failures = []

    # collect refs; follow url(...) in local CSS
    refs = list(page.refs)
    for url, tag, attr, first, grp in list(refs):
        p = local_path(root, url)
        if p and p.suffix.lower() == ".css" and p.is_file():
            css = p.read_text(encoding="utf-8", errors="replace")
            for m in re.finditer(r"url\(\s*['\"]?([^'\")]+?)['\"]?\s*\)", css):
                u = m.group(1).strip()
                q = local_path(p.parent, u)
                if q:
                    refs.append((str(q.relative_to(root)) if q.is_relative_to(root) else u,
                                 "css-url", "url()", True, None))

    # <picture>/srcset: a browser fetches one candidate; count the largest.
    best = {}    # group -> (size, index of ref)
    for i, (url, tag, attr, first, grp) in enumerate(refs):
        p = local_path(root, url)
        if grp is not None and first and p is not None and p.is_file():
            if grp not in best or p.stat().st_size > best[grp][0]:
                best[grp] = (p.stat().st_size, i)
    refs = [(u, t, at, f and (g is None or best.get(g, (0, i))[1] == i), g)
            for i, (u, t, at, f, g) in enumerate(refs)]

    files = {}   # Path -> first_view flag
    missing = []
    for url, tag, attr, first, grp in refs:
        p = local_path(root, url)
        if p is None:
            continue
        if not p.exists():
            missing.append((url, tag, attr))
            continue
        if p.is_dir():
            continue
        p = p.resolve()
        files[p] = files.get(p, False) or first

    for url, tag, attr in missing:
        failures.append(f"broken link: <{tag} {attr}> -> {url}")
    for a in page.anchors:
        if a not in page.ids:
            failures.append(f"bad anchor: href=\"#{a}\" has no matching id")

    index_r = index.resolve()
    files.pop(index_r, None)
    rows = []   # (kind, relpath, size, budget, first)
    for p, first in files.items():
        ext = p.suffix.lower()
        if ext == ".css":
            kind, budget = "css", None
        elif ext in (".js", ".mjs"):
            kind, budget = "js", None
        else:
            kind, budget = classify(p)
        try:
            rel = str(p.relative_to(root))
        except ValueError:
            rel = str(p)
        rows.append((kind, rel, p.stat().st_size, budget, first))
    rows.sort(key=lambda r: (not r[4], r[1]))

    html_size = index.stat().st_size
    first_total = html_size + sum(r[2] for r in rows if r[0] in ("css", "js") or r[4])
    total = html_size + sum(r[2] for r in rows)

    print(f"Site: {root}")
    print(f"{'file':44} {'kind':6} {'size':>11} {'budget':>10}  fv  status")
    print("-" * 86)
    print(f"{'index.html':44} {'html':6} {fmt(html_size)} {'':>10}  yes")
    for kind, rel, size, budget, first in rows:
        status = "ok"
        if budget is not None and size > budget:
            status = "OVER"
            failures.append(f"over budget: {rel} is {size / KB:.1f} KB ({kind} limit {budget // KB} KB)")
        fv = "yes" if (first or kind in ("css", "js")) else ""
        print(f"{rel[:44]:44} {kind:6} {fmt(size)} {(str(budget // KB) + ' KB') if budget else '-':>10}  {fv:3} {status}")
    for url, tag, attr in missing:
        print(f"{url[:44]:44} {'-':6} {'MISSING':>11} {'':>10}      MISSING")
    print("-" * 86)
    fv_ok = first_total <= FIRST_VIEW_BUDGET
    tot_ok = total <= TOTAL_BUDGET
    print(f"First view: {fmt(first_total)} / {FIRST_VIEW_BUDGET // KB} KB  {'ok' if fv_ok else 'OVER'}")
    print(f"Total page: {fmt(total)} / {TOTAL_BUDGET // KB} KB  {'ok' if tot_ok else 'OVER'}")
    if not fv_ok:
        failures.append(f"first-view weight {first_total / KB:.1f} KB > {FIRST_VIEW_BUDGET // KB} KB")
    if not tot_ok:
        failures.append(f"total weight {total / KB:.1f} KB > {TOTAL_BUDGET // KB} KB")

    if failures:
        print(f"\nFAIL ({len(failures)}):")
        for f in failures:
            print("  - " + f)
        return 1
    print("\nPASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

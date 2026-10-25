#!/usr/bin/env python3
"""Generate the calendar-week pages of the website (ISO 8601 weeks):
  docs/weeks/index.html            "What week is it?" (filled in by weeks.js)
  docs/weeks/<year>/index.html     year at a glance: 12 months with week numbers
  docs/weeks/<year>/cw<n>/         one page per week
  docs/weeks.ics                   subscribable calendar with a "CW n" event every Monday
  docs/sitemap.xml
Covers FIRST_YEAR to three years ahead; .github/workflows/week-pages.yml re-runs it every January.
Output depends only on the year range, so re-running it is a no-op until the year changes.
Usage: week-pages.py"""
import calendar, html, os, shutil
from datetime import date, timedelta

SITE = "https://rockykusuma.github.io/weekmark/"
FIRST_YEAR = 2025
LAST_YEAR = date.today().year + 3
YEARS = range(FIRST_YEAR, LAST_YEAR + 1)
DOCS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs")
OUT = os.path.join(DOCS, "weeks")

MONTHS = ["January", "February", "March", "April", "May", "June", "July",
          "August", "September", "October", "November", "December"]
DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
DOWNLOAD = "https://github.com/rockykusuma/weekmark/releases/latest"
BREW = "brew install --cask rockykusuma/weekmark/weekmark"

def monday(y, w): return date.fromisocalendar(y, w, 1)
def weeks_in(y): return date(y, 12, 28).isocalendar()[1]
def has_page(y, w): return y in YEARS and 1 <= w <= weeks_in(y)
def esc(s): return html.escape(s, quote=True)
def short(d): return f"{d.day} {MONTHS[d.month - 1][:3]}"
def long(d): return f"{DAYS[d.weekday()]} {d.day} {MONTHS[d.month - 1]}"

def short_range(mon):
    """'9–15 Nov', '27 Apr – 3 May': the same text the app copies after 'CW46 '."""
    sun = mon + timedelta(6)
    return f"{mon.day}–{short(sun)}" if mon.month == sun.month else f"{short(mon)} – {short(sun)}"

def long_range(mon):
    sun = mon + timedelta(6)
    first = long(mon) + (f" {mon.year}" if mon.year != sun.year else "")
    return f"{first} – {long(sun)} {sun.year}"

def us_week(mon):
    """US numbering (Sunday start, the week with 1 January is week 1) for Monday–Saturday of this ISO week.
    Saturday is always in the US week's own year, so number it from there."""
    sat = mon + timedelta(5)
    jan1_offset = (date(sat.year, 1, 1).weekday() + 1) % 7  # Sunday = 0
    return sat.year, (sat.timetuple().tm_yday - 1 + jan1_offset) // 7 + 1

def page(path, title, description, body, depth):
    """Write a page `depth` folders below docs/weeks/ (0 = the hub)."""
    up = "../" * depth
    rel = os.path.relpath(os.path.dirname(path), OUT).replace(os.sep, "/")
    url = SITE + "weeks/" + ("" if rel == "." else rel + "/")
    doc = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{esc(title)}</title>
<meta name="description" content="{esc(description)}">
<link rel="canonical" href="{url}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="Weekmark">
<meta property="og:url" content="{url}">
<meta property="og:title" content="{esc(title)}">
<meta property="og:description" content="{esc(description)}">
<meta property="og:image" content="{SITE}assets/og.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="627">
<meta name="twitter:card" content="summary_large_image">
<link rel="icon" href="{up}../assets/icon.png">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Big+Shoulders+Display:wght@800;900&family=Figtree:wght@400;500;600;700&family=JetBrains+Mono:wght@400&display=swap">
<link rel="stylesheet" href="{up}style.css">
</head>
<body>
<div class="wrap">
<header class="top">
  <a class="brand" href="{up}../"><img src="{up}../assets/icon.png" alt="">Weekmark</a>
  <nav><a href="{up}">What week is it?</a><a href="{up}{date.today().year}/">{date.today().year} at a glance</a></nav>
</header>
<main>
{body}
</main>
<aside class="cta">
  <div>
    <h2>The week number, always on your Mac</h2>
    <p>Weekmark keeps the calendar week on your desktop and answers “when is CW46?” from any app, with working days, holidays, sprints and milestones. Free, notarized, no tracking.</p>
  </div>
  <div class="cta-actions">
    <a class="btn" href="{DOWNLOAD}">Download for macOS</a>
    <code class="cmd">{BREW}</code>
  </div>
</aside>
<footer>
  <span>Weekmark · by Rakesh Kusuma</span>
  <span><a href="{up}#subscribe">Week numbers in your calendar</a> · <a href="https://github.com/rockykusuma/weekmark">Source on GitHub</a></span>
</footer>
</div>
<script src="{up}weeks.js" defer></script>
</body>
</html>
"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(doc)
    return url

def week_link(y, w, frm_depth, label=None):
    """Link to week y-w from a page frm_depth folders below docs/weeks/, or plain text if it has no page."""
    label = label or str(w)
    if not has_page(y, w): return f"<span>{label}</span>"
    return f'<a href="{"../" * frm_depth}{y}/cw{w}/">{label}</a>'

def week_page(y, w):
    mon = monday(y, w)
    sun = mon + timedelta(6)
    thu = mon + timedelta(3)
    uy, uw = us_week(mon)
    us = f"{uw}" + (f" of {uy}" if uy != y else "")
    us_note = "the same number" if (uy, uw) == (y, w) else "a different number"
    prev, nxt = (mon - timedelta(7)).isocalendar(), (mon + timedelta(7)).isocalendar()
    days = "\n".join(
        f'    <li data-date="{d.isoformat()}"><span>{DAYS[d.weekday()][:3]}</span><b>{d.day}</b><span>{MONTHS[d.month - 1][:3]}</span></li>'
        for d in (mon + timedelta(i) for i in range(7)))
    body = f"""<section class="week">
  <div class="cw" aria-hidden="true"><span class="cw-label">CW</span><span class="cw-big">{w}</span></div>
  <div>
    <p class="eyebrow">Calendar week · ISO 8601</p>
    <h1>Week {w}, {y}</h1>
    <p class="dates">{long_range(mon)}</p>
    <p class="rel" data-week-start="{mon.isoformat()}"></p>
  </div>
</section>
<ol class="days">
{days}
</ol>
<dl class="facts">
  <div><dt>ISO week</dt><dd><code>{y}-W{w:02d}</code></dd></div>
  <div><dt>Copy as</dt><dd><code>CW{w} ({short_range(mon)})</code></dd></div>
  <div><dt>Quarter</dt><dd>Q{(thu.month - 1) // 3 + 1} {thu.year}</dd></div>
  <div><dt>Weeks in {y}</dt><dd>{weeks_in(y)}</dd></div>
  <div><dt>US week number</dt><dd>{us}</dd></div>
</dl>
<p class="note">Week {w} is also written KW {w} (Kalenderwoche) or CW {w}. ISO weeks start on Monday, and week 1 is the week with the year’s first Thursday.
US calendars start weeks on Sunday and count the week with 1 January as week 1, so they give Monday {short(mon)} to Saturday {short(sun - timedelta(1))} {us_note}: week {us}.</p>
<nav class="pager">
  {week_link(prev[0], prev[1], 2, f"← Week {prev[1]}")}
  <a href="../">{y} at a glance</a>
  {week_link(nxt[0], nxt[1], 2, f"Week {nxt[1]} →")}
</nav>"""
    return page(os.path.join(OUT, str(y), f"cw{w}", "index.html"),
                f"Week {w}, {y}: dates {short_range(mon)} {sun.year} (KW {w}) | Weekmark",
                f"Calendar week {w} of {y} (KW {w}) runs from {long(mon)} to {long(sun)} {sun.year}. "
                f"US week number, quarter, and how far away it is.", body, 2)

def month_grid(y, m):
    rows = []
    for wk in calendar.Calendar(firstweekday=0).monthdatescalendar(y, m):
        iy, iw, _ = wk[0].isocalendar()
        cells = "".join(f'<td{"" if d.month == m else " class=out"} data-date="{d.isoformat()}">{d.day}</td>' for d in wk)
        rows.append(f'<tr data-week-start="{wk[0].isoformat()}"><th scope="row">{week_link(iy, iw, 1)}</th>{cells}</tr>')
    return f"""<div class="month">
  <h2>{MONTHS[m - 1]}</h2>
  <table>
    <thead><tr><th scope="col" title="Calendar week">CW</th><th>M</th><th>T</th><th>W</th><th>T</th><th>F</th><th class="we">S</th><th class="we">S</th></tr></thead>
    <tbody>{"".join(rows)}</tbody>
  </table>
</div>"""

def year_page(y):
    first, last = monday(y, 1), monday(y, weeks_in(y)) + timedelta(6)
    pager = [f'<a href="../{y - 1}/">← {y - 1}</a>' if y - 1 in YEARS else "<span></span>",
             f'<a href="cw1/">Week 1</a>',
             f'<a href="../{y + 1}/">{y + 1} →</a>' if y + 1 in YEARS else "<span></span>"]
    body = f"""<section class="year-head">
  <p class="eyebrow">Calendar weeks · ISO 8601</p>
  <h1>{y} at a glance</h1>
  <p class="dates">{y} has {weeks_in(y)} calendar weeks. Week 1 starts on {long(first)} {first.year}; week {weeks_in(y)} ends on {long(last)} {last.year}.</p>
</section>
<div class="year">
{chr(10).join(month_grid(y, m) for m in range(1, 13))}
</div>
<nav class="pager">{"".join(pager)}</nav>"""
    return page(os.path.join(OUT, str(y), "index.html"),
                f"Calendar weeks {y}: all {weeks_in(y)} week numbers at a glance | Weekmark",
                f"A {y} calendar with week numbers: every month of {y} with its ISO calendar weeks (KW), "
                f"from week 1 starting {short(first)} {first.year} to week {weeks_in(y)}.", body, 1)

def hub():
    years = "".join(f'<a href="{y}/">{y}<small>{weeks_in(y)} weeks</small></a>' for y in YEARS)
    body = f"""<section class="week now" data-first="{FIRST_YEAR}" data-last="{LAST_YEAR}">
  <div class="cw" aria-hidden="true"><span class="cw-label">CW</span><span class="cw-big" id="now-cw">–</span></div>
  <div>
    <p class="eyebrow">What week is it?</p>
    <h1 id="now-title">This week’s number</h1>
    <p class="dates" id="now-range">Today’s ISO 8601 calendar week, with its dates.</p>
    <p><a class="btn" id="now-link" href="{date.today().year}/">See this week</a></p>
  </div>
</section>
<section>
  <h2 class="h2">Calendar weeks by year</h2>
  <div class="years">{years}</div>
</section>
<section id="subscribe" class="subscribe">
  <h2 class="h2">Week numbers in your calendar</h2>
  <p>Subscribe once and every Monday shows “CW 46” in Google Calendar, Outlook or Apple Calendar, on any device. It updates itself.</p>
  <div class="cta-actions">
    <a class="btn" href="webcal://rockykusuma.github.io/weekmark/weeks.ics">Apple Calendar / Outlook</a>
    <a class="btn btn-quiet" href="https://calendar.google.com/calendar/r?cid=webcal://rockykusuma.github.io/weekmark/weeks.ics">Google Calendar</a>
  </div>
  <p class="fine">Or add this URL as a calendar subscription: <code>{SITE}weeks.ics</code></p>
</section>"""
    return page(os.path.join(OUT, "index.html"), "What week is it? Today’s calendar week number | Weekmark",
                "The current calendar week number (ISO 8601, KW) with its dates, plus every week of "
                f"{FIRST_YEAR}–{LAST_YEAR} at a glance and a week-number calendar to subscribe to.", body, 0)

def ics():
    def fold(line):  # RFC 5545: lines of at most 75 octets, continuations start with a space
        out, cur = [], b""
        for ch in line:
            b = ch.encode()
            if len(cur) + len(b) > (75 if not out else 74):
                out.append(cur.decode()); cur = b""
            cur += b
        return "\r\n ".join(out + [cur.decode()])
    lines = ["BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Weekmark//Week numbers//EN", "CALSCALE:GREGORIAN",
             "METHOD:PUBLISH", "X-WR-CALNAME:Week numbers", "X-WR-CALDESC:ISO 8601 calendar week numbers, by Weekmark",
             "REFRESH-INTERVAL;VALUE=DURATION:P1W", "X-PUBLISHED-TTL:P1W"]
    for y in YEARS:
        for w in range(1, weeks_in(y) + 1):
            mon = monday(y, w)
            lines += ["BEGIN:VEVENT", f"UID:cw-{y}-{w:02d}@rockykusuma.github.io",
                      "DTSTAMP:20250101T000000Z",  # fixed, so the file only changes when the year range does
                      f"DTSTART;VALUE=DATE:{mon:%Y%m%d}", f"DTEND;VALUE=DATE:{mon + timedelta(1):%Y%m%d}",
                      f"SUMMARY:CW {w}",
                      f"DESCRIPTION:Calendar week {w}\\, {y}: {short_range(mon)}\\n{SITE}weeks/{y}/cw{w}/",
                      f"URL:{SITE}weeks/{y}/cw{w}/", "TRANSP:TRANSPARENT", "END:VEVENT"]
    lines.append("END:VCALENDAR")
    with open(os.path.join(DOCS, "weeks.ics"), "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(fold(l) for l in lines) + "\r\n")

shutil.rmtree(OUT, ignore_errors=True)
os.makedirs(OUT)
for asset in ("style.css", "weeks.js"):
    shutil.copy(os.path.join(os.path.dirname(os.path.abspath(__file__)), "weeks", asset), OUT)
urls = [SITE, hub()]
for y in YEARS:
    urls.append(year_page(y))
    urls += [week_page(y, w) for w in range(1, weeks_in(y) + 1)]
ics()
with open(os.path.join(DOCS, "sitemap.xml"), "w", encoding="utf-8") as f:
    f.write('<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
            + "".join(f"  <url><loc>{u}</loc></url>\n" for u in urls) + "</urlset>\n")
print(f"✓ {len(urls) - 2} pages in docs/weeks/ ({FIRST_YEAR}–{LAST_YEAR}), docs/weeks.ics, docs/sitemap.xml")

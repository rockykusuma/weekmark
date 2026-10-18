#!/usr/bin/env python3
"""Convert release-notes/<version>.md (headings, bullets, **bold**, `code`, links) to a small HTML page
for Sparkle's update window. Usage: notes-to-html.py in.md out.html"""
import html, re, sys

src, dst = sys.argv[1], sys.argv[2]

def inline(t):
    t = html.escape(t)
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"`(.+?)`", r"<code>\1</code>", t)
    t = re.sub(r"\[(.+?)\]\((https?://[^)]+)\)", r'<a href="\2">\1</a>', t)
    return t

out, in_list = [], False
for line in open(src, encoding="utf-8").read().splitlines():
    s = line.strip()
    if s.startswith("- ") or s.startswith("* "):
        if not in_list: out.append("<ul>"); in_list = True
        out.append(f"<li>{inline(s[2:])}</li>"); continue
    if in_list: out.append("</ul>"); in_list = False
    if not s: continue
    if s.startswith("#"):
        out.append(f"<h2>{inline(s.lstrip('#').strip())}</h2>")
    else:
        out.append(f"<p>{inline(s)}</p>")
if in_list: out.append("</ul>")

style = ("<style>:root{color-scheme:light dark}body{font:13px -apple-system,system-ui,sans-serif;margin:12px 16px;"
         "line-height:1.45}h2{font-size:15px;margin:0 0 8px}ul{padding-left:18px;margin:0}li{margin:0 0 6px}"
         "code{font:12px ui-monospace,Menlo,monospace}</style>")
open(dst, "w", encoding="utf-8").write(f"<!doctype html><meta charset=\"utf-8\">{style}\n" + "\n".join(out) + "\n")

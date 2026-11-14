#!/usr/bin/env python3
"""Kopiert eine (OFL-)Schrift unter neuem Familiennamen: rename-font.py <src.ttf> <dst.ttf> <Familie> <Stil>
Nötig, weil WPF Schriften über den internen Familiennamen findet (nicht über Dateiname/Registry)."""
import sys
from fontTools.ttLib import TTFont
src, dst, fam, style = sys.argv[1:5]
f = TTFont(src)
full = fam if style == "Regular" else f"{fam} {style}"
ps = (fam + "-" + style).replace(" ", "")
for rec in f["name"].names:
    v = {1: fam, 2: style, 3: f"{full};ets-arm64", 4: full, 6: ps, 16: fam, 17: style}.get(rec.nameID)
    if v is not None:
        rec.string = v
f.save(dst)
print("->", dst, "=", full)

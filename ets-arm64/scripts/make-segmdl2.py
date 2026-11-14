#!/usr/bin/env python3
"""Erzeugt eine minimale Ersatzschrift „Segoe MDL2 Assets“ (segmdl2.ttf) für ETS6 unter Wine.

Die Originalschrift ist nicht frei verteilbar. ETS nutzt daraus u. a. Fensterknöpfe
(Schließen/Minimieren/Maximieren/Wiederherstellen). Zusätzlich wird der gesamte
Private-Use-Bereich U+E000–U+F8FF mit einem leeren Platzhalter-Glyph belegt, damit
WPF für keine Symbol-Codepoints in „MapUnresolvedCharacters“ scheitert.
Benötigt python3-fonttools.   Aufruf: python3 make-segmdl2.py <ziel.ttf>
"""
import sys
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

EM = 2048
NAMED = {0xE106: "close", 0xE711: "close", 0xE8BB: "close", 0xEB90: "close",
         0xE921: "minimize", 0xE922: "maximize", 0xE923: "restore", 0xE73C: "dash"}


def rect(p, x0, y0, x1, y1):
    p.moveTo((x0, y0)); p.lineTo((x0, y1)); p.lineTo((x1, y1)); p.lineTo((x1, y0)); p.closePath()


def frame(p, x0, y0, x1, y1, t=90):
    rect(p, x0, y0, x1, y0 + t); rect(p, x0, y1 - t, x1, y1)
    rect(p, x0, y0, x0 + t, y1); rect(p, x1 - t, y0, x1, y1)


def draw(name, p):
    if name == "minimize":
        rect(p, 400, 260, 1400, 350)
    elif name == "maximize":
        frame(p, 400, 300, 1400, 1300)
    elif name == "restore":
        frame(p, 300, 200, 1250, 1150); frame(p, 550, 450, 1500, 1400)
    elif name == "close":
        p.moveTo((400, 350)); p.lineTo((1380, 1330)); p.lineTo((1500, 1210)); p.lineTo((520, 230)); p.closePath()
        p.moveTo((1380, 230)); p.lineTo((400, 1210)); p.lineTo((520, 1330)); p.lineTo((1500, 350)); p.closePath()
    elif name == "dash":
        rect(p, 500, 760, 1300, 850)
    # ".notdef" und "blank": leer


order = [".notdef", "blank", "close", "minimize", "maximize", "restore", "dash"]
cmap = {cp: "blank" for cp in range(0xE000, 0xF900)}
cmap.update(NAMED)
cmap[0x20] = "blank"

fb = FontBuilder(EM, isTTF=True)
fb.setupGlyphOrder(order)
fb.setupCharacterMap(cmap)
glyphs, metrics = {}, {}
for n in order:
    pen = TTGlyphPen(None)
    draw(n, pen)
    glyphs[n] = pen.glyph()
    metrics[n] = (1800, 0)
fb.setupGlyf(glyphs)
fb.setupHorizontalMetrics(metrics)
fb.setupHorizontalHeader(ascent=1638, descent=-410)
fb.setupNameTable({"familyName": "Segoe MDL2 Assets", "styleName": "Regular",
                   "fullName": "Segoe MDL2 Assets", "psName": "SegoeMDL2Assets"})
fb.setupOS2(sTypoAscender=1638, sTypoDescender=-410, usWinAscent=1638, usWinDescent=410,
            ulUnicodeRange1=0, ulUnicodeRange2=0, ulUnicodeRange3=0x10000000)  # Bit 60: Private Use Area
fb.setupPost()
fb.save(sys.argv[1] if len(sys.argv) > 1 else "segmdl2.ttf")
print("geschrieben:", sys.argv[1] if len(sys.argv) > 1 else "segmdl2.ttf")

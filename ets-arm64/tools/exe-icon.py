#!/usr/bin/env python3
"""Extrahiert die erste Symbolgruppe (RT_GROUP_ICON) einer Windows-EXE als .ico.
Aufruf: exe-icon.py <datei.exe> <ausgabe.ico>"""
import struct, sys

data = open(sys.argv[1], "rb").read()
pe = struct.unpack_from("<I", data, 0x3C)[0]
nsec = struct.unpack_from("<H", data, pe + 6)[0]
optsz = struct.unpack_from("<H", data, pe + 20)[0]
opt = pe + 24
magic = struct.unpack_from("<H", data, opt)[0]
ddir = opt + (96 if magic == 0x10B else 112)
rsrc_rva = struct.unpack_from("<I", data, ddir + 2 * 8)[0]
secs = [struct.unpack_from("<8sIIII", data, pe + 24 + optsz + i * 40) for i in range(nsec)]


def off(rva):
    for _, vsz, va, rsz, ptr in secs:
        if va <= rva < va + max(vsz, rsz):
            return rva - va + ptr
    raise ValueError(hex(rva))


base = off(rsrc_rva)


def entries(d):
    n = sum(struct.unpack_from("<HH", data, base + d + 12))
    return [struct.unpack_from("<II", data, base + d + 16 + i * 8) for i in range(n)]


def leaf(o):                       # bis zum Datenblatt absteigen (jeweils erster Eintrag)
    while o & 0x80000000:
        o = entries(o & 0x7FFFFFFF)[0][1]
    rva, size = struct.unpack_from("<II", data, base + o)
    return data[off(rva):off(rva) + size]


types = dict(entries(0))
icons = {name: o for name, o in entries(types[3] & 0x7FFFFFFF)}     # RT_ICON
grp = leaf(entries(types[14] & 0x7FFFFFFF)[0][1])                  # RT_GROUP_ICON
count = struct.unpack_from("<H", grp, 4)[0]
imgs, hdr = [], bytearray(grp[:6])
for i in range(count):
    w, h, cc, r, planes, bpp, size, iid = struct.unpack_from("<BBBBHHIH", grp, 6 + i * 14)
    imgs.append((w, h, cc, r, planes, bpp, leaf(icons[iid])))
pos = 6 + 16 * len(imgs)
dirs, blob = bytearray(), bytearray()
for w, h, cc, r, planes, bpp, img in imgs:
    dirs += struct.pack("<BBBBHHII", w, h, cc, r, planes, bpp, len(img), pos + len(blob))
    blob += img
open(sys.argv[2], "wb").write(bytes(hdr) + dirs + blob)
print(f"{count} Bilder -> {sys.argv[2]}")

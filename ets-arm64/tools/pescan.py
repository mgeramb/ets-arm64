#!/usr/bin/env python3
"""Classify Windows PE files: machine type, managed/native, CorFlags, native imports."""
import os
import struct
import sys

MACHINES = {0x14c: "x86", 0x8664: "x64", 0xaa64: "ARM64", 0x1c4: "ARMNT", 0xa641: "ARM64EC"}


def rva_to_off(rva, sections):
    for va, vsize, raw, rawsize in sections:
        if va <= rva < va + max(vsize, rawsize):
            return rva - va + raw
    return None


def cstr(data, off):
    end = data.find(b"\0", off)
    return data[off:end].decode("latin1", "replace")


def scan(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:2] != b"MZ":
        return None
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        return None
    machine, nsec = struct.unpack_from("<HH", data, pe + 4)
    optsize = struct.unpack_from("<H", data, pe + 20)[0]
    chars = struct.unpack_from("<H", data, pe + 22)[0]
    opt = pe + 24
    magic = struct.unpack_from("<H", data, opt)[0]
    dd = opt + (96 if magic == 0x10B else 112)
    sec_off = opt + optsize
    sections = []
    for i in range(nsec):
        s = sec_off + i * 40
        vsize, va, rawsize, raw = struct.unpack_from("<IIII", data, s + 8)
        sections.append((va, vsize, raw, rawsize))

    def ddir(i):
        return struct.unpack_from("<II", data, dd + i * 8)

    imports = []
    irva, isz = ddir(1)
    if irva:
        o = rva_to_off(irva, sections)
        while o is not None and o + 20 <= len(data):
            name_rva = struct.unpack_from("<I", data, o + 12)[0]
            if name_rva == 0:
                break
            no = rva_to_off(name_rva, sections)
            if no is not None:
                imports.append(cstr(data, no).lower())
            o += 20

    clr_rva, _ = ddir(14)
    info = {
        "machine": MACHINES.get(machine, hex(machine)),
        "pe32plus": magic == 0x20B,
        "dll": bool(chars & 0x2000),
        "managed": False,
        "kind": "native",
        "imports": imports,
    }
    if clr_rva:
        o = rva_to_off(clr_rva, sections)
        flags = struct.unpack_from("<I", data, o + 16)[0]
        info["managed"] = True
        ilonly = bool(flags & 1)
        req32 = bool(flags & 2)
        pref32 = bool(flags & 0x20000)
        if not ilonly:
            kind = "mixed C++/CLI"
        elif machine == 0x8664:
            kind = ".NET x64-only"
        elif machine == 0xAA64:
            kind = ".NET ARM64-only"
        elif req32 and not pref32:
            kind = ".NET x86-only (32BITREQUIRED)"
        elif pref32:
            kind = ".NET AnyCPU 32-bit preferred"
        else:
            kind = ".NET AnyCPU"
        info["kind"] = kind
    else:
        info["kind"] = "native " + info["machine"]
    return info


def main(roots):
    rows = []
    for root in roots:
        for dp, _, fns in os.walk(root):
            for fn in fns:
                p = os.path.join(dp, fn)
                try:
                    with open(p, "rb") as f:
                        if f.read(2) != b"MZ":
                            continue
                    r = scan(p)
                except Exception as e:  # noqa: BLE001
                    r = {"kind": f"error {e}", "imports": []}
                if r:
                    rows.append((os.path.relpath(p, root), r))
    rows.sort(key=lambda x: (x[1]["kind"], x[0].lower()))
    for p, r in rows:
        imp = ""
        if not r.get("managed"):
            imp = "  imports: " + ", ".join(r["imports"])
        print(f"{r['kind']:32} {os.path.getsize(os.path.join(roots[0], p)) if False else '':0}{p}{imp}")
    from collections import Counter
    print("\nSummary:", dict(Counter(r["kind"] for _, r in rows)))


if __name__ == "__main__":
    main(sys.argv[1:])

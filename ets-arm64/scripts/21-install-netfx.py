#!/usr/bin/env python3
"""Mini-CBS-Installer: installiert .NET Framework 4.8.1 ARM64 aus den entpackten
Komponenten-Manifesten (KB5011048-arm64.cab) in einen Wine-Prefix.

  python3 21-install-netfx.py <cab-dir> <WINEPREFIX> <out.reg>

- kopiert <file>-Einträge nach destinationPath/destinationName
- legt <link>-Ziele (FrameworkArm64, GAC_MSIL, ...) als Hardlinks an
- schreibt alle <registryKey>/<registryValue> in eine .reg-Datei (für `wine regedit`)
Nur Komponenten mit Präfix arm64_ werden verarbeitet (x86_/wow64_ = 32-Bit, nicht benötigt).
"""
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET

src, prefix, out_reg = sys.argv[1:4]
drive_c = os.path.join(prefix, "drive_c")

UNIX_VARS = {
    "runtime.windows": "windows",
    "runtime.system32": "windows/system32",
    "runtime.inf": "windows/inf",
    "runtime.wbem": "windows/system32/wbem",
    "runtime.drivers": "windows/system32/drivers",
    "runtime.programfiles": "Program Files",
}
WIN_VARS = {k: "C:\\" + v.replace("/", "\\") for k, v in UNIX_VARS.items()}
VAR_RE = re.compile(r"\$\(([a-zA-Z0-9.]+)\)")
unknown_vars = set()


def expand(s, table):
    def rep(m):
        k = m.group(1)
        if k in table:
            return table[k]
        unknown_vars.add(k)
        return m.group(0)
    return VAR_RE.sub(rep, s)


def to_unix(winpath):
    p = expand(winpath, UNIX_VARS).replace("\\", "/").strip("/")
    return os.path.join(drive_c, p)


def local(tag):
    return tag.rsplit("}", 1)[-1]


stats = dict(manifests=0, files=0, links=0, missing=0, keys=0, values=0, parse_errors=0, gac=0)
gac_list = []  # Windows-Pfade prozessorspezifischer Assemblies (clrarch=arm64) für Fusion-Installation
reg = {}  # key -> list of (name, type, value)

for fn in sorted(os.listdir(src)):
    if not fn.endswith(".manifest") or not fn.startswith("arm64_"):
        continue
    stats["manifests"] += 1
    path = os.path.join(src, fn)
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as e:
        stats["parse_errors"] += 1
        print("PARSE", fn, e, file=sys.stderr)
        continue
    compdir = path[: -len(".manifest")]
    present = {}
    if os.path.isdir(compdir):
        present = {f.lower(): os.path.join(compdir, f) for f in os.listdir(compdir)}

    for el in root.iter():
        t = local(el.tag)
        if t == "file":
            name = el.get("name")
            dpath = el.get("destinationPath")
            if not name or dpath is None:
                continue
            srcfile = present.get(name.lower()) or present.get((el.get("sourceName") or "").lower())
            if not srcfile:
                stats["missing"] += 1
                print("MISSING", fn, name, file=sys.stderr)
                continue
            dest = os.path.join(to_unix(dpath), el.get("destinationName") or name)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            shutil.copy2(srcfile, dest)
            stats["files"] += 1
            win_dest = expand(dpath, WIN_VARS).rstrip("\\") + "\\" + (el.get("destinationName") or name)
            for g in el:
                # <gac clrarch="arm64"> ohne GAC-Link -> per Fusion (IAssemblyCache) installieren
                if local(g.tag) == "gac" and g.get("clrarch") == "arm64":
                    gac_list.append(win_dest)
                    stats["gac"] += 1
            for ln in el:
                if local(ln.tag) == "link" and ln.get("destination"):
                    ldest = to_unix(ln.get("destination"))
                    os.makedirs(os.path.dirname(ldest), exist_ok=True)
                    if os.path.exists(ldest):
                        os.remove(ldest)
                    try:
                        os.link(dest, ldest)
                    except OSError:
                        shutil.copy2(dest, ldest)
                    stats["links"] += 1
        elif t == "registryKey":
            key = expand(el.get("keyName"), WIN_VARS)
            vals = reg.setdefault(key, [])
            stats["keys"] += 1
            for v in el:
                if local(v.tag) != "registryValue":
                    continue
                vals.append((v.get("name") or "", v.get("valueType"),
                             expand(v.get("value") or "", WIN_VARS)))
                stats["values"] += 1

# --- Zusatz-Werte, die unter Windows vom OS / Setup-Bootstrapper kommen
fw = "C:\\windows\\Microsoft.NET\\FrameworkArm64\\"
extra = {
    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\.NETFramework": [
        ("InstallRoot", "REG_SZ", fw), ("InstallRootArm64", "REG_SZ", fw)],
    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full": [
        ("Release", "REG_DWORD", "0x00082348"),  # 533320 = 4.8.1
        ("Version", "REG_SZ", "4.8.09032"), ("Install", "REG_DWORD", "0x1"),
        ("InstallPath", "REG_SZ", fw + "v4.0.30319\\"), ("TargetVersion", "REG_SZ", "4.0.0")],
    "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Client": [
        ("Release", "REG_DWORD", "0x00082348"), ("Version", "REG_SZ", "4.8.09032"),
        ("Install", "REG_DWORD", "0x1"), ("InstallPath", "REG_SZ", fw + "v4.0.30319\\")],
}
for k, vs in extra.items():
    reg.setdefault(k, []).extend(vs)


def regstr(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def hexbytes(b):
    return ",".join(f"{x:02x}" for x in b)


lines = ["Windows Registry Editor Version 5.00", ""]
for key in reg:
    lines.append(f"[{key}]")
    for name, typ, val in reg[key]:
        n = "@" if name == "" else regstr(name)
        if typ == "REG_SZ":
            lines.append(f"{n}={regstr(val)}")
        elif typ == "REG_DWORD":
            lines.append(f"{n}=dword:{int(val, 0) & 0xFFFFFFFF:08x}")
        elif typ == "REG_QWORD":
            lines.append(f"{n}=hex(b):{hexbytes(int(val, 0).to_bytes(8, 'little'))}")
        elif typ == "REG_EXPAND_SZ":
            lines.append(f"{n}=hex(2):{hexbytes((val + chr(0)).encode('utf-16-le'))}")
        elif typ == "REG_MULTI_SZ":
            items = re.findall(r'"((?:[^"]|"")*)"', val) if val else []
            data = "".join(i + "\0" for i in items) + "\0"
            lines.append(f"{n}=hex(7):{hexbytes(data.encode('utf-16-le'))}")
        elif typ == "REG_BINARY":
            lines.append(f"{n}=hex:{hexbytes(bytes.fromhex(val)) if val else ''}")
        else:
            print("REGTYPE?", key, name, typ, file=sys.stderr)
    lines.append("")

with open(out_reg, "w", encoding="utf-16") as f:
    f.write("\r\n".join(lines))
with open(os.path.splitext(out_reg)[0] + "-gac.txt", "w", encoding="utf-8") as f:
    f.write("\r\n".join(gac_list) + "\r\n")

print("stats:", stats)
print("unbekannte Variablen:", sorted(unknown_vars) or "-")

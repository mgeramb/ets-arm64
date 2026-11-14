#!/usr/bin/env bash
# Baut eine ARM64-mscoree.dll für .NET 4.8.1 ARM64 unter Wine:
#  - _CorExeMain / _CorDllMain als echte Funktionen (mscoree_shim.c), die mscoreei.dll aus
#    %windir%\Microsoft.NET\FrameworkArm64\v4.0.30319 laden (Wine-Loader folgt bei IL-only
#    Images keinen Forwardern: RtlFindExportedRoutineByName)
#  - alle übrigen Exporte von mscoreei.dll als Forwarder
# Unter Windows ist mscoree.dll Teil des OS; im .NET-Paket ist nur mscoreei.dll enthalten.
set -euo pipefail
ORD="${SHIM_ORDINALS-1}"; [ "$ORD" = 0 ] && ORD=""
ENTRY="/entry:ShimDllMain"; [ "${SHIM_DLLMAIN-1}" = 0 ] && ENTRY="/noentry"
MSCOREEI="$1"            # Pfad zur ARM64 mscoreei.dll (für die Exportliste)
OUT="$2"                 # Ziel mscoree.dll
HERE="$(dirname "$(readlink -f "$0")")"
WORK=$(mktemp -d)

python3 - "$MSCOREEI" > "$WORK/exports.txt" <<'EOF'
import struct, sys
d = open(sys.argv[1], 'rb').read()
pe = struct.unpack_from('<I', d, 0x3c)[0]
nsec = struct.unpack_from('<H', d, pe + 6)[0]
osz = struct.unpack_from('<H', d, pe + 20)[0]
opt = pe + 24
dd = opt + (96 if struct.unpack_from('<H', d, opt)[0] == 0x10b else 112)
secs = []
for i in range(nsec):
    s = opt + osz + i * 40
    vs, va, rs, ro = struct.unpack_from('<IIII', d, s + 8)
    secs.append((va, max(vs, rs), ro))
def off(r):
    for va, sz, ro in secs:
        if va <= r < va + sz:
            return r - va + ro
def cs(o):
    return d[o:d.index(b'\0', o)].decode()
e = off(struct.unpack_from('<I', d, dd)[0])
base, nfunc, nname = struct.unpack_from('<III', d, e + 16)
names, ords = off(struct.unpack_from('<I', d, e + 32)[0]), off(struct.unpack_from('<I', d, e + 36)[0])
funcs = off(struct.unpack_from('<I', d, e + 28)[0])
byidx = {struct.unpack_from('<H', d, ords + 2 * i)[0]: cs(off(struct.unpack_from('<I', d, names + 4 * i)[0]))
         for i in range(nname)}
for i in range(nfunc):
    if struct.unpack_from('<I', d, funcs + 4 * i)[0]:
        print(f"{base + i}\t{byidx.get(i, '')}")
EOF

{ echo "LIBRARY mscoree.dll"; echo "EXPORTS"
  # gleiche Ordinalzahlen wie mscoreei.dll (clr.dll importiert z. T. per Ordinal, z. B. #24, #142)
  while IFS=$'\t' read -r ord f; do
    case "$f" in
      "")          echo "  __ord$ord=mscoreei.#$ord @$ord NONAME" ;;  # #24: von clr.dll per Ordinal benötigt
      _CorExeMain2) ;;  # NICHT exportieren: mscoreei!_CorExeMain prüft mscoree#142 und springt sonst rekursiv in sich selbst
      _CorExeMain) echo "  _CorExeMain=shim_CorExeMain${ORD:+ @$ord}" ;;
      _CorDllMain) echo "  _CorDllMain=shim_CorDllMain${ORD:+ @$ord}" ;;
      *)           echo "  $f=mscoreei.$f${ORD:+ @$ord}" ;;
    esac
  done < "$WORK/exports.txt"; } > "$WORK/mscoree.def"

printf 'LIBRARY kernel32.dll\nEXPORTS\n  LoadLibraryW\n  GetProcAddress\n  GetWindowsDirectoryW\n' > "$WORK/kernel32.def"
llvm-dlltool -m arm64 -d "$WORK/kernel32.def" -l "$WORK/kernel32.lib"

clang --target=aarch64-windows-msvc -O2 -fno-builtin -fno-stack-protector -c \
  -o "$WORK/shim.obj" "$HERE/mscoree_shim.c"
lld-link /nologo /dll $ENTRY /nodefaultlib /machine:arm64 /def:"$WORK/mscoree.def" \
  /out:"$OUT" "$WORK/shim.obj" "$WORK/kernel32.lib"
echo "mscoree.dll gebaut: $(wc -l < "$WORK/exports.txt") Exporte -> $OUT"

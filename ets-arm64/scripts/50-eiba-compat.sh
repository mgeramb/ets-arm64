#!/usr/bin/env bash
# Installiert die EIBA-sc-Kompatibilitätsschicht (für den Import von Geräten mit Plugins, WineHQ-Bug 60377)
# aus dem originalen ETS-Installer – so wie es der Windows-Installer tut:
# EIBA.*.dll nach "C:\Program Files (x86)\Common Files\EIBA sc" und in den GAC.
# Läuft in der VM. Aufruf: 50-eiba-compat.sh <Ets6Setup.exe> [gacinstall.exe]
#   gacinstall.exe = gebautes scripts/gacinstall.cs (Standard: C:\ets-arm64\gacinstall.exe)
# Benötigt: 7z, msiextract (sudo dnf -y install 7zip msitools)
set -euo pipefail
SETUP="$1"
GACINSTALL="${2:-C:\\ets-arm64\\gacinstall.exe}"
export PATH=/opt/wine-arm64/bin:$PATH WINEPREFIX="${WINEPREFIX:-$HOME/.wine-ets}" \
       WINEDLLOVERRIDES="mscoree=n;mshtml=" WINEDEBUG=-all
W=$(mktemp -d)
trap 'rm -rf "$W"' EXIT

# 1. Ets6Setup.exe ist ein WiX-Burn-Bundle: die Pakete stecken im angehängten Container (zweites, großes
#    CAB „MSCF“ hinter dem UX-Container). Kompatibilitäts-MSI = Payload ETSCompatibilityComponents.msi.
python3 -I - "$SETUP" "$W/attached.cab" <<'EOF'
import struct, sys
d = open(sys.argv[1], "rb").read()
cabs, i = [], 0
while (i := d.find(b"MSCF\0\0\0\0", i)) >= 0:
    size = struct.unpack_from("<I", d, i + 8)[0]
    if 0 < size <= len(d) - i:
        cabs.append((size, i))
    i += 4
size, off = max(cabs)
open(sys.argv[2], "wb").write(d[off:off + size])
EOF
# Payload-Namen im Container sind a0..aN; das Kompatibilitäts-MSI über seinen Inhalt finden
7z x -y -o"$W/payload" "$W/attached.cab" >/dev/null
COMPAT=""
for f in "$W"/payload/a*; do
  files=$(msiinfo export "$f" File 2>/dev/null || true)      # kein grep -q in der Pipe (pipefail/SIGPIPE)
  if [[ "$files" == *EIBA.Interop.EtecDOM.dll* ]]; then COMPAT="$f"; break; fi
done
[ -n "$COMPAT" ] || { echo "ETSCompatibilityComponents.msi nicht gefunden" >&2; exit 1; }
msiextract --directory "$W/compat" "$COMPAT" >/dev/null

# 2. Kopieren + GAC (die 4 IL-Assemblies laden nativ ARM64; EIBA.Hawk.Utils ist x86-C++/CLI und wird nur
#    vom 32-Bit-Kompatibilitätsmodus ETS6C.exe genutzt – Installation trotzdem wie unter Windows)
DEST="$WINEPREFIX/drive_c/Program Files (x86)/Common Files/EIBA sc"
mkdir -p "$DEST"
cp "$W/compat/Common Files/EIBA sc/"EIBA.*.dll "$DEST/"
LIST="$W/eiba.lst"; : > "$LIST"
for f in "$DEST"/EIBA.*.dll; do
  printf 'C:\\Program Files (x86)\\Common Files\\EIBA sc\\%s\r\n' "$(basename "$f")" >> "$LIST"
done
wine "$GACINSTALL" "$(winepath -w "$LIST")"
echo "EIBA sc installiert: $(ls "$DEST" | grep -c '^EIBA')" Assemblies

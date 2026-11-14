#!/usr/bin/env bash
# Schriften für WPF/ETS: freie Fedora-Schriften nach C:\windows\Fonts + Registry,
# „Segoe UI“ = Liberation Sans, „Segoe MDL2 Assets“ = selbst erzeugter Ersatz.
set -euo pipefail
: "${WINEPREFIX:?WINEPREFIX setzen}"
HERE="$(dirname "$(readlink -f "$0")")"
sudo dnf -y -q install liberation-sans-fonts liberation-serif-fonts liberation-mono-fonts \
  dejavu-sans-fonts dejavu-serif-fonts dejavu-sans-mono-fonts python3-fonttools >/dev/null
F="$WINEPREFIX/drive_c/windows/Fonts"; mkdir -p "$F"
REG=$(mktemp --suffix=.reg)
echo 'Windows Registry Editor Version 5.00' > "$REG"
echo '[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Fonts]' >> "$REG"
add() {  # add <quelle.ttf> <zielname.ttf> <Registry-Name>
  cp -f "$1" "$F/$2"; printf '"%s (TrueType)"="%s"\n' "$3" "$2" >> "$REG"; }
for f in /usr/share/fonts/liberation-*/*.ttf /usr/share/fonts/dejavu-*/*.ttf; do
  n=$(basename "$f"); add "$f" "$n" "$(python3 -c "
import sys; from fontTools.ttLib import TTFont
t=TTFont(sys.argv[1]); print(t['name'].getDebugName(4))" "$f")"
done
# Segoe UI = Liberation Sans mit internem Familiennamen „Segoe UI“ (WPF sucht über den Namen in der Datei)
L=/usr/share/fonts/liberation-sans-fonts
for spec in "Regular:segoeui" "Bold:segoeuib" "Italic:segoeuii" "BoldItalic:segoeuiz"; do
  st=${spec%%:*}; fn=${spec##*:}; stn=${st/BoldItalic/Bold Italic}
  python3 "$HERE/rename-font.py" "$L/LiberationSans-$st.ttf" "$F/$fn.ttf" "Segoe UI" "$stn" >/dev/null
  n="Segoe UI"; [ "$st" != Regular ] && n="Segoe UI $stn"
  printf '"%s (TrueType)"="%s"\n' "$n" "$fn.ttf" >> "$REG"
done
# Wines eigene Ersatzschriften (Tahoma, Marlett, …) ebenfalls nach C:\windows\Fonts
WF=$(dirname "$(dirname "$(command -v wine)")")/share/wine/fonts
for f in "$WF"/*.ttf "$WF"/*.ttc; do [ -e "$f" ] && cp -f "$f" "$F/"; done
python3 "$HERE/make-segmdl2.py" "$F/segmdl2.ttf"
printf '"%s (TrueType)"="%s"\n' "Segoe MDL2 Assets" "segmdl2.ttf" >> "$REG"
cat >> "$REG" <<'EOF'

[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\FontSubstitutes]
"Arial"="Liberation Sans"
"Times New Roman"="Liberation Serif"
"Courier New"="Liberation Mono"
"Consolas"="Liberation Mono"
"Tahoma"="DejaVu Sans"
EOF
wine regedit /S "$(winepath -w "$REG")"
echo "Schriften installiert: $(ls "$F" | wc -l) Dateien in $F"

#!/usr/bin/env bash
# Erzeugt eine im Finder/Dock anklickbare App, die ets-start.sh im Hintergrund startet.
# Aufruf: make-app.sh [Ziel-Ordner]   (Standard: ~/Applications)  -> "ETS6 ARM64.app"
# Log der Starts: ~/Library/Logs/ets-start.log
set -euo pipefail
DEST="${1:-$HOME/Applications}"
APP="$DEST/ETS6 ARM64.app"
START="$(cd "$(dirname "$0")" && pwd)/ets-start.sh"
[ -x "$START" ] || { echo "ets-start.sh nicht gefunden/ausführbar: $START" >&2; exit 1; }
mkdir -p "$DEST"
rm -rf "$APP"

# AppleScript-Applet: startet das Skript losgelöst (nohup) und beendet sich sofort.
# Finder-Apps haben nur /usr/bin:/bin im PATH -> Homebrew (limactl) ergänzen.
SCPT="$(mktemp -t ets-app).applescript"
cat > "$SCPT" <<EOF
do shell script "export PATH=/opt/homebrew/bin:/usr/local/bin:/opt/X11/bin:\$PATH; mkdir -p ~/Library/Logs; nohup " & quoted form of "$START" & " >> ~/Library/Logs/ets-start.log 2>&1 < /dev/null &"
EOF
osacompile -o "$APP" "$SCPT"
rm -f "$SCPT"

# Kennung + kein Dock-Symbol während des kurzen Applet-Laufs
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier org.ets-arm64.launcher" "$APP/Contents/Info.plist" 2>/dev/null \
  || /usr/libexec/PlistBuddy -c "Add :CFBundleIdentifier string org.ets-arm64.launcher" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Add :LSUIElement bool true" "$APP/Contents/Info.plist" 2>/dev/null || true

# Symbol: ETS_ICON=/pfad/icon.icns, sonst aus der lokalen ETS-Installation (ETS_DIR, Standard ~/ETS6)
# extrahieren (KNX-Symbol, daher nicht im Repo).
ICON="${ETS_ICON:-}"
if [ -z "$ICON" ] && [ -f "${ETS_DIR:-$HOME/ETS6}/ETS6.exe" ]; then
  T="$(mktemp -d)"
  if python3 -I "$(dirname "$START")/../tools/exe-icon.py" "${ETS_DIR:-$HOME/ETS6}/ETS6.exe" "$T/ets.ico" >/dev/null &&
     sips -s format png "$T/ets.ico" --out "$T/big.png" >/dev/null; then
    mkdir "$T/ets.iconset"
    for s in 16 32 128 256 512; do
      sips -z $s $s "$T/big.png" --out "$T/ets.iconset/icon_${s}x${s}.png" >/dev/null
      sips -z $((s*2)) $((s*2)) "$T/big.png" --out "$T/ets.iconset/icon_${s}x${s}@2x.png" >/dev/null
    done
    iconutil -c icns "$T/ets.iconset" -o "$T/ets.icns" && ICON="$T/ets.icns"
  fi
fi
[ -n "$ICON" ] && [ -f "$ICON" ] && cp "$ICON" "$APP/Contents/Resources/applet.icns"
codesign --force --sign - "$APP"              # Ad-hoc-Signatur nach Plist-/Symboländerung erneuern
touch "$APP"
echo "Erstellt: $APP  (startet $START)"

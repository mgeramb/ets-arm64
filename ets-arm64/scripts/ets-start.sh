#!/usr/bin/env bash
# Startet ETS6 (nativ ARM64 in der Lima-VM) und zeigt das Fenster über XQuartz auf dem Mac an.
# Voraussetzungen: XQuartz (brew install --cask xquartz), Lima-VM „wine-arm64“ eingerichtet.
set -euo pipefail
VM="${ETS_VM:-wine-arm64}"
WINEPREFIX_VM='$HOME/.wine-ets'

# 1. XQuartz starten (falls noch nicht läuft) und auf das X11-Socket warten
if ! pgrep -xq Xquartz; then
  open -a XQuartz
  for _ in $(seq 1 30); do pgrep -xq Xquartz && break; sleep 1; done
fi
# DISPLAY setzt XQuartz erst nach erneutem Anmelden; :0 ist das lokale XQuartz-Socket
export DISPLAY="${DISPLAY:-:0}"

# 2. VM starten, falls nötig
[ "$(limactl list --format '{{.Status}}' "$VM")" = Running ] || limactl start "$VM"

# Läuft ETS schon? Dann nicht neu starten (wineserver -k würde ein offenes Projekt ohne Speichern beenden).
# Erzwingen: ETS_FORCE_RESTART=1
if [ -z "${ETS_FORCE_RESTART:-}" ] && limactl shell "$VM" -- pgrep -f 'ETS6N.exe' >/dev/null 2>&1; then
  echo "ETS läuft bereits – kein Neustart."
  osascript -e 'display notification "ETS läuft bereits." with title "ETS6 ARM64"' 2>/dev/null || true
  exit 0
fi

# 3. HID-Brücke für den KNX-Lizenz-Dongle (Mac IOHIDManager -> VM /dev/uhid) im Hintergrund.
#    Läuft als Schleife, solange ETS läuft: Stick später eingesteckt/abgezogen -> erneuter Versuch.
#    Abschalten: ETS_NO_HIDBRIDGE=1. Binary: KNX_HIDBRIDGE oder ../hidbridge/knx-hidbridge.
BRIDGE="${KNX_HIDBRIDGE:-$(cd "$(dirname "$0")/.." && pwd)/hidbridge/knx-hidbridge}"
BRIDGE_LOG="$HOME/Library/Logs/knx-hidbridge.log"
BRIDGE_PID=
if [ -z "${ETS_NO_HIDBRIDGE:-}" ] && [ -x "$BRIDGE" ]; then
  pkill -x knx-hidbridge 2>/dev/null || true          # Reste einer früheren Sitzung
  mkdir -p "$(dirname "$BRIDGE_LOG")"; : > "$BRIDGE_LOG"
  ( while :; do "$BRIDGE" >>"$BRIDGE_LOG" 2>&1; sleep 3; done ) &
  BRIDGE_PID=$!
  trap 'kill $BRIDGE_PID 2>/dev/null; pkill -x knx-hidbridge 2>/dev/null; true' EXIT INT TERM
  # ETS sucht den Dongle beim Start -> kurz auf die Verbindung warten (max. 5 s, ohne Stick sofort weiter)
  for _ in $(seq 1 10); do
    grep -q -E "verbunden|nicht gefunden" "$BRIDGE_LOG" 2>/dev/null && break; sleep 0.5
  done
  grep -q verbunden "$BRIDGE_LOG" && echo "Lizenz-Dongle: über HID-Brücke verbunden" \
                                  || echo "Lizenz-Dongle: nicht gefunden (Parallels? siehe $BRIDGE_LOG)"
fi

# 4. ETS per SSH mit X11-Forwarding starten (-Y: vertrauenswürdig, nötig für Wine/WPF).
#    Kein exec, damit die Brücke danach beendet wird; wineserver -w wartet auf alle Wine-Prozesse.
SSHCFG="$HOME/.lima/$VM/ssh.config"
ssh -F "$SSHCFG" -Y -o XAuthLocation=/opt/X11/bin/xauth -o ControlMaster=no -o ControlPath=none "lima-$VM" \
  "export PATH=/opt/wine-arm64/bin:\$PATH WINEPREFIX=$WINEPREFIX_VM WINEDLLOVERRIDES='mscoree=n;mshtml=' WINEDEBUG=-all ${ETS_ARM64_ENV:-};
   wineserver -k 2>/dev/null; wineserver -w;
   wine 'C:\\ets-arm64\\ETS6.exe' $*; wineserver -w"

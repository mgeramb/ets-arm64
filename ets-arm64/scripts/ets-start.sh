#!/usr/bin/env bash
# Startet ETS6 (nativ ARM64 in der Lima-VM) und zeigt die Fenster auf dem Mac an.
#   ETS_DISPLAY=xpra     xpra im seamless-Modus (Standard, wenn Xpra.app gefunden wird):
#                        Server in der VM, nativer macOS-Client, Transport über Limas SSH (kein offener Port).
#                        Retina-scharf, Sitzung überlebt das Schließen des Clients (erneuter Start = neu verbinden).
#   ETS_DISPLAY=xquartz  X11-Forwarding per ssh -Y zu XQuartz (bisheriger Weg)
# Weitere Schalter: ETS_FORCE_RESTART=1, ETS_NO_HIDBRIDGE=1, ETS_ARM64_ENV="VAR=wert …" (Diagnose),
#   XPRA_APP=/pfad/Xpra.app, ETS_XPRA_SCALE=2 (Retina; 1 = ohne HiDPI), ETS_XPRA_DISPLAY=100.
# Voraussetzungen: Lima-VM „wine-arm64“ eingerichtet; xpra: Xpra.app (xpra.org) + vm/install-system.sh;
#   xquartz: XQuartz (brew install --cask xquartz).
set -euo pipefail
VM="${ETS_VM:-wine-arm64}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SSHCFG="$HOME/.lima/$VM/ssh.config"
SSH=(ssh -F "$SSHCFG" -o ControlMaster=no -o ControlPath=none)
XDISP="${ETS_XPRA_DISPLAY:-100}"

# 0. Anzeige wählen
XPRA_APP="${XPRA_APP:-}"
if [ -z "$XPRA_APP" ]; then
  for a in "$HOME/Applications/Xpra.app" /Applications/Xpra.app; do [ -x "$a/Contents/MacOS/Xpra" ] && XPRA_APP="$a" && break; done
fi
MODE="${ETS_DISPLAY:-}"
[ -n "$MODE" ] || { [ -n "$XPRA_APP" ] && MODE=xpra || MODE=xquartz; }
if [ "$MODE" = xpra ] && [ -z "$XPRA_APP" ]; then
  echo "Xpra.app nicht gefunden (XPRA_APP setzen) – nehme XQuartz."; MODE=xquartz
fi
echo "Anzeige: $MODE"

# 1. XQuartz starten (falls nötig) und auf das X11-Socket warten
if [ "$MODE" = xquartz ]; then
  if ! pgrep -xq Xquartz; then
    open -a XQuartz
    for _ in $(seq 1 30); do pgrep -xq Xquartz && break; sleep 1; done
  fi
  # DISPLAY setzt XQuartz erst nach erneutem Anmelden; :0 ist das lokale XQuartz-Socket
  export DISPLAY="${DISPLAY:-:0}"
fi

# 2. VM starten, falls nötig
[ "$(limactl list --format '{{.Status}}' "$VM")" = Running ] || limactl start "$VM"

# HID-Brücke für den KNX-Lizenz-Dongle (Mac IOHIDManager -> VM /dev/uhid) im Hintergrund.
# Läuft als Schleife, solange dieses Skript läuft: Stick später eingesteckt/abgezogen -> erneuter Versuch.
# Abschalten: ETS_NO_HIDBRIDGE=1. Binary: KNX_HIDBRIDGE oder ../hidbridge/knx-hidbridge.
BRIDGE="${KNX_HIDBRIDGE:-$(cd "$HERE/.." && pwd)/hidbridge/knx-hidbridge}"
BRIDGE_LOG="$HOME/Library/Logs/knx-hidbridge.log"
start_bridge() {
  [ -z "${ETS_NO_HIDBRIDGE:-}" ] && [ -x "$BRIDGE" ] || return 0
  # Läuft schon eine Brücke (z. B. Schleife einer früheren/anderen Sitzung), nicht beenden: sie verbindet
  # sich selbst neu. Zwei Brücken gleichzeitig würden sich um Stick und Port 7531 streiten.
  if pgrep -xq knx-hidbridge; then echo "Lizenz-Dongle: HID-Brücke läuft bereits"; return 0; fi
  mkdir -p "$(dirname "$BRIDGE_LOG")"; : > "$BRIDGE_LOG"
  ( while :; do "$BRIDGE" >>"$BRIDGE_LOG" 2>&1; sleep 3; done ) &
  local pid=$!
  trap "kill $pid 2>/dev/null; pkill -x knx-hidbridge 2>/dev/null; true" EXIT INT TERM
  # ETS sucht den Dongle beim Start -> kurz auf die Verbindung warten (max. 5 s, ohne Stick sofort weiter)
  for _ in $(seq 1 10); do
    grep -q -E "verbunden|nicht gefunden" "$BRIDGE_LOG" 2>/dev/null && break; sleep 0.5
  done
  grep -q verbunden "$BRIDGE_LOG" && echo "Lizenz-Dongle: über HID-Brücke verbunden" \
                                  || echo "Lizenz-Dongle: nicht gefunden (Parallels? siehe $BRIDGE_LOG)"
}

# xpra-Client verbinden (blockiert, bis der Client endet: ETS beendet oder Client geschlossen)
SCALE="${ETS_XPRA_SCALE:-2}"
xpra_attach() {
  "$XPRA_APP/Contents/MacOS/Xpra" attach "ssh://lima-$VM/$XDISP" \
    --ssh="ssh -F $SSHCFG -o ControlMaster=no -o ControlPath=none" \
    --desktop-scaling="$(awk "BEGIN{print 1/$SCALE}")" --title='@title@' \
    --speaker=no --microphone=no --webcam=no --printing=no --notifications=no \
    --splash=no --tray=no || true
}
xpra_live() { "${SSH[@]}" "lima-$VM" "xpra list 2>/dev/null | grep -q 'LIVE session at :$XDISP'"; }

# Läuft ETS schon? Dann nicht neu starten (wineserver -k würde ein offenes Projekt ohne Speichern beenden).
# Erzwingen: ETS_FORCE_RESTART=1. Im xpra-Modus wird stattdessen erneut verbunden.
if [ -z "${ETS_FORCE_RESTART:-}" ] && limactl shell "$VM" -- pgrep -f 'ETS6N.exe' >/dev/null 2>&1; then
  if [ "$MODE" = xpra ] && xpra_live; then
    echo "ETS läuft bereits in der xpra-Sitzung :$XDISP – verbinde erneut."
    start_bridge
    xpra_attach
    while xpra_live; do sleep 10; done                  # Brücke halten, solange die Sitzung lebt
    exit 0
  fi
  echo "ETS läuft bereits – kein Neustart."
  osascript -e 'display notification "ETS läuft bereits." with title "ETS6 ARM64"' 2>/dev/null || true
  exit 0
fi

# 3. Dongle-Brücke
start_bridge

# 4. Startskript in die VM legen (aktuelle Fassung aus dem Repo)
"${SSH[@]}" "lima-$VM" 'mkdir -p ~/.local/share/ets-arm64 && cat > ~/.local/share/ets-arm64/ets-vm-run.sh && chmod +x ~/.local/share/ets-arm64/ets-vm-run.sh' \
  < "$HERE/ets-vm-run.sh"
RUN='$HOME/.local/share/ets-arm64/ets-vm-run.sh'
ARGS="$*"

if [ "$MODE" = xpra ]; then
  # 5a. xpra-Server mit ETS als Kindprozess; Sitzung endet mit ETS (--exit-with-children).
  #     DPI = 96 × Skalierung -> WPF rendert in Retina-Auflösung, der Client zeigt 1:1 an.
  "${SSH[@]}" "lima-$VM" "xpra stop :$XDISP >/dev/null 2>&1;
    env ETS_DPI=$((96 * SCALE)) ${ETS_ARM64_ENV:-} xpra start :$XDISP --daemon=yes \
      --start-child=\"$RUN $ARGS\" --exit-with-children=yes \
      --pulseaudio=no --speaker=off --microphone=off --webcam=no --printing=no --file-transfer=no \
      --mdns=no --notifications=no --systemd-run=no >/dev/null"
  xpra_attach
  while xpra_live; do sleep 10; done                    # Client geschlossen, ETS läuft weiter -> Brücke halten
else
  # 5b. ETS per SSH mit X11-Forwarding starten (-Y: vertrauenswürdig, nötig für Wine/WPF).
  #     Kein exec, damit die Brücke danach beendet wird; das VM-Skript wartet auf alle Wine-Prozesse.
  "${SSH[@]}" -Y -o XAuthLocation=/opt/X11/bin/xauth "lima-$VM" \
    "env ETS_DPI=96 ${ETS_ARM64_ENV:-} $RUN $ARGS"
fi

#!/usr/bin/env bash
# Richtet die Systemteile der VM ein (als normaler Benutzer in der VM ausführen, nutzt sudo).
# Aufruf vom Mac:  limactl shell wine-arm64 -- bash /pfad/zum/repo/vm/install-system.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/.." && pwd)"
sudo dnf -y install xorg-x11-xauth xdpyinfo mesa-dri-drivers python3
# xpra (seamless-Anzeige, ets-start.sh ETS_DISPLAY=xpra). Fedoras xpra-Paket zieht die GObject-Bindings
# nicht mit (Fehler „module gi has no attribute require_version“) -> explizit installieren.
sudo dnf -y install xpra python3-gobject python3-cairo gtk3 gobject-introspection
sudo install -m 0644 "$HERE/system/50-x11.conf"          /etc/ssh/sshd_config.d/50-x11.conf
sudo systemctl reload sshd
sudo install -m 0644 "$HERE/system/uhid.conf"            /etc/modules-load.d/uhid.conf
sudo modprobe uhid
sudo install -m 0644 "$HERE/system/90-knx-dongle.rules"  /etc/udev/rules.d/90-knx-dongle.rules
sudo udevadm control --reload-rules
sudo install -D -m 0755 "$REPO/ets-arm64/hidbridge/uhid-bridge.py" /opt/ets-arm64/uhid-bridge.py
sudo install -m 0644 "$HERE/system/uhid-bridge.service"  /etc/systemd/system/uhid-bridge.service
sudo systemctl daemon-reload
sudo systemctl enable --now uhid-bridge
echo "VM-Systemteile eingerichtet."

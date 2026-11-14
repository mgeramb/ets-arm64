#!/usr/bin/env bash
# Lädt den offiziellen .NET Framework 4.8.1 Offline-Installer und entpackt das ARM64-CBS-Paket.
# Ergebnis: $NETFX_DIR/arm64/  (CBS-Komponenten + .manifest-Dateien)
set -euo pipefail
NETFX_DIR="${NETFX_DIR:-$HOME/netfx481}"
URL="https://download.microsoft.com/download/4/b/2/cd00d4ed-ebdd-49ee-8a33-eabc3d1030e3/NDP481-x86-x64-AllOS-ENU.exe"
SHA256="c0ca2e0c9cd18a24a0a77369a13fae2c2c4e8bc83355dd24e5ddc00f9d791fe3"

sudo dnf -y -q install 7zip
SZ=$(command -v 7zz || command -v 7z)   # Fedora: 7z, Homebrew: 7zz
mkdir -p "$NETFX_DIR" && cd "$NETFX_DIR"
[ -f NDP481-x86-x64-AllOS-ENU.exe ] || curl -fsSLO "$URL"
echo "$SHA256  NDP481-x86-x64-AllOS-ENU.exe" | sha256sum -c -

rm -rf x arm64
"$SZ" x -y -ox NDP481-x86-x64-AllOS-ENU.exe >/dev/null
"$SZ" x -y -oarm64 x/xarm64-Windows10.0-KB5011048-arm64.cab >/dev/null
echo "Manifeste: $(ls arm64/*.manifest | wc -l), Dateien: $(find arm64 -type f | wc -l)"

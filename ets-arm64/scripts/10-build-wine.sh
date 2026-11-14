#!/usr/bin/env bash
# Baut Wine (aarch64, reines ARM64 ohne x86-Emulation) in der Fedora-VM.
# Aufruf: bash /mnt/share/10-build-wine.sh   (als normaler User, nutzt sudo für dnf/install)
set -euo pipefail
WINE_VER="${WINE_VER:-11.21}"
PREFIX="${PREFIX:-/opt/wine-arm64}"
SRC="$HOME/src"

echo "== [1/4] Build-Abhängigkeiten"
sudo dnf -y install --setopt=install_weak_deps=False \
  gcc gcc-c++ make flex bison perl gettext pkgconf-pkg-config \
  clang lld llvm \
  libX11-devel libXext-devel libXrender-devel libXrandr-devel libXi-devel \
  libXcursor-devel libXcomposite-devel libXfixes-devel libXinerama-devel \
  libXxf86vm-devel libxkbcommon-devel mesa-libGL-devel mesa-libEGL-devel \
  vulkan-loader-devel wayland-devel \
  freetype-devel fontconfig-devel gnutls-devel libunwind-devel \
  dbus-devel cups-devel libusb1-devel systemd-devel \
  krb5-devel pulseaudio-libs-devel alsa-lib-devel \
  libpcap-devel sane-backends-devel gstreamer1-devel gstreamer1-plugins-base-devel \
  SDL2-devel ocl-icd-devel opencl-headers \
  xz curl

echo "== [2/4] Quelltext wine-$WINE_VER"
mkdir -p "$SRC" && cd "$SRC"
if [ ! -d "wine-$WINE_VER" ]; then
  curl -fLO "https://dl.winehq.org/wine/source/11.x/wine-$WINE_VER.tar.xz"
  tar xf "wine-$WINE_VER.tar.xz"
fi

echo "== [2b] Patches aus $(dirname "$0")/patches"
cd "$SRC/wine-$WINE_VER"
for p in "$(dirname "$(readlink -f "$0")")"/patches/*.patch; do
  [ -e "$p" ] || continue
  if patch -p1 -R --dry-run -s -f < "$p" >/dev/null 2>&1; then echo "  bereits drin: $(basename "$p")"
  else patch -p1 -s < "$p" && echo "  angewendet: $(basename "$p")"; fi
done
cd "$SRC"

echo "== [3/4] configure + make"
mkdir -p "$SRC/build-$WINE_VER" && cd "$SRC/build-$WINE_VER"
# PE-Teile werden mit clang (-target aarch64-windows) gebaut; kein mingw nötig.
"../wine-$WINE_VER/configure" --prefix="$PREFIX" --enable-archs=aarch64 --with-mingw=clang \
  2>&1 | tee configure.log | tail -40
make -j"$(nproc)" 2>&1 | tee make.log | grep -E "error|Error" | head -20 || true
test -f loader/wine || { echo "BUILD FEHLGESCHLAGEN"; exit 1; }

echo "== [4/4] Installation nach $PREFIX"
sudo make install >/dev/null
"$PREFIX/bin/wine" --version

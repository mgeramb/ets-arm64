#!/usr/bin/env bash
# Läuft IN DER VM: setzt die Wine-Umgebung, beendet einen alten wineserver, startet ETS und wartet,
# bis alle Wine-Prozesse beendet sind. Wird von ets-start.sh benutzt (xpra-Modus: als --start-child).
#   ETS_DPI=<n>       Wine-DPI (LogPixels) vor dem Start setzen, z. B. 192 für Retina über xpra, 96 sonst
#   ETS_ARM64_*       Diagnose-Schalter des Hosts werden einfach durchgereicht
export PATH=/opt/wine-arm64/bin:$PATH WINEPREFIX="$HOME/.wine-ets" \
       WINEDLLOVERRIDES="mscoree=n;mshtml=" WINEDEBUG=-all
wineserver -k 2>/dev/null; wineserver -w
if [ -n "${ETS_DPI:-}" ]; then
  hex=$(printf '0x%x' "$ETS_DPI")
  if ! wine reg query 'HKCU\Control Panel\Desktop' /v LogPixels 2>/dev/null | grep -qi "$hex"; then
    for k in 'HKCU\Control Panel\Desktop' 'HKCU\Software\Wine\Fonts' 'HKCC\Software\Fonts'; do
      wine reg add "$k" /v LogPixels /t REG_DWORD /d "$hex" /f >/dev/null 2>&1
    done
    # Die System-DPI liest win32u beim Start der Wine-Sitzung -> wineserver neu starten
    wineserver -k 2>/dev/null; wineserver -w
  fi
fi
wine 'C:\ets-arm64\ETS6.exe' "$@"
wineserver -w

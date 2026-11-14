#!/usr/bin/env python3
"""Erzeugt die Config des ARM64-Hosts (C:\\ets-arm64\\ETS6N.exe.config) aus der ETS-Config.

Der Host braucht eine eigene Config (u. a. useLegacyV2RuntimeActivationPolicy für die ETS-Abhängigkeiten).
Zusätzlich werden prozessweite Laufzeitschalter ergänzt, die ETS unter Wine braucht – ohne die
ETS-Datei selbst zu verändern:
  - legacyCorruptedStateExceptionsPolicy: native Zugriffsfehler (AV/SEH) bleiben in .NET abfangbar,
    statt den Prozess sofort zu beenden (prozessweit, wird nur aus der Config der EXE gelesen)
  - AppContextSwitchOverrides Switch.System.Windows.Controls.Grid.StarDefinitionsCanExceedAvailableSpace
    (WPF-Layoutverhalten wie unter .NET 4.7.x; setzt der Host zusätzlich per AppContext.SetSwitch
    in der ETS-AppDomain)
Aufruf: make-host-config.py <ETS6N.exe.config aus dem ETS-Ordner> <Ziel>
"""
import sys

SWITCH = "Switch.System.Windows.Controls.Grid.StarDefinitionsCanExceedAvailableSpace=true"

src, dst = sys.argv[1], sys.argv[2]
text = open(src, encoding="utf-8-sig").read()
if "<runtime>" not in text:
    sys.exit("keine <runtime>-Sektion in " + src)
inject = "<runtime>"
if "legacyCorruptedStateExceptionsPolicy" not in text:
    inject += '\n    <legacyCorruptedStateExceptionsPolicy enabled="true" />'
if "AppContextSwitchOverrides" not in text:
    inject += '\n    <AppContextSwitchOverrides value="' + SWITCH + '" />'
elif SWITCH.split("=")[0] not in text:
    sys.exit("AppContextSwitchOverrides schon vorhanden – Schalter bitte von Hand ergänzen")
open(dst, "w", encoding="utf-8").write(text.replace("<runtime>", inject, 1))
print("Host-Config geschrieben:", dst)

# ETS6 auf Apple Silicon – nativ ARM64, ohne Rosetta

Experimentelles Projekt: Die KNX-Software **ETS 6** läuft auf einem Mac mit Apple Silicon als **ARM64-Code**,
ohne Rosetta und ohne x86/x64-Emulation – inklusive **USB-Lizenz-Dongle**.

> ⚠️ Inoffiziell, nicht von KNX unterstützt. Es werden **keine** ETS-, KNX- oder Microsoft-Dateien
> mitgeliefert und keine ETS-Dateien verändert. Eine eigene, gültige ETS-Lizenz ist nötig.

## Aufbau

| Baustein | Lösung |
|---|---|
| Laufzeitumgebung | Mini-VM (Lima, Apple Virtualization.framework) mit Fedora aarch64 – macOS selbst kann keinen Windows-ARM64-Code ausführen (x18-Register, 16-KB-Seiten, RWX nur mit `MAP_JIT`) |
| Wine | Wine 11.21, als ARM64 gebaut, mit 5 Patches (`ets-arm64/patches/`) |
| .NET | ARM64-Teile von .NET Framework 4.8.1 aus dem offiziellen Offline-Installer + eigener `mscoree`-Shim |
| ETS-Start | ARM64-Launcher `ETS6.exe` + Host `ETS6N.exe` (startet das unveränderte ETS in eigener AppDomain) |
| Lizenz-Dongle | Host lädt die von KNX mitgelieferte **Linux-ARM64**-DevKit-Bibliothek über eine eigene `libdl.so`-Brücke |
| USB | `knx-hidbridge` (macOS, IOHIDManager) → TCP → `uhid-bridge.py` (VM, `/dev/uhid`) → virtuelles `/dev/hidrawN` |
| Anzeige | XQuartz (X11-Forwarding über SSH) |
| Mac-Dateien | `~/ETS-Daten` = Laufwerk `H:` |
| Start | `ets-arm64/scripts/ets-start.sh` bzw. „ETS6 ARM64.app“ (`make-app.sh`) |

## Stand

✅ ETS 6.4.1 startet nativ ARM64, Projekte, Internetzugriff, Dongle-Lizenz („User is licensed“)
❌ Cloud-Lizenz (KNX liefert die Cloud-Bibliothek nur als x86/x64-Windows-Code)
⚠️ Neustart aus ETS heraus funktioniert nicht · Installationsskript in Arbeit

## Dokumentation

Alle Schritte mit exakten Befehlen, Fehlerbildern und Ursachen: **[INSTALL-LOG.md](INSTALL-LOG.md)**

## Verzeichnisse

- `vm/` – Lima-Vorlage (`wine-arm64.yaml`), Systemdateien der VM und `install-system.sh`
- `wine/ets-settings.reg` – Registry-Einstellungen für den Wine-Prefix
- `CLAUDE.md` – Projektkontext und Arbeitsregeln für Claude Code
- `ets-arm64/patches/` – Wine-Patches
- `ets-arm64/scripts/` – Build-/Installationsskripte, Launcher, Host, libdl-Brücke, Startskript
- `ets-arm64/hidbridge/` – Dongle-Brücke (Mac + VM)
- `ets-arm64/tools/` – Analysewerkzeuge (PE/IL, macOS-Probe)
- `ets-arm64/tests/` – Testprogramme für Wine/.NET
- `ets-arm64/logs/`, `ets-arm64/screenshots/` – Protokolle und Bildschirmfotos

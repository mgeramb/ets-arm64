# CLAUDE.md – ETS6 nativ ARM64 auf Apple Silicon

Ziel: KNX **ETS 6** auf einem Apple-Silicon-Mac **ohne Rosetta und ohne x86/x64-Emulation** betreiben,
inklusive USB-Lizenz-Dongle. Am Ende soll ein Installationsskript stehen, das alles auf einem anderen
Mac reproduziert. Sprache im Projekt (Doku, Kommentare, Commits): **Deutsch**.

## Arbeitsregeln
- **Alles protokollieren** in `INSTALL-LOG.md` (exakte Befehle, Fehlerbild, Ursache, Lösung; Legende
  ✅ ❌ ⚠️ 🔁). Neue Abschnitte vor „Offene Punkte / Ideen“ einfügen, fortlaufend nummerieren.
- **Repo ist öffentlich** (github.com/mgeramb/ets-arm64): niemals ETS-/KNX-/Microsoft-Dateien, gebaute
  Binärdateien, `DevKit.so`, KNX-Symbole, disassemblierten KNX-Code, Projektnamen oder Dongle-Seriennummern
  einchecken. Solche Dinge werden bei der Installation lokal erzeugt/extrahiert.
- **ETS-Dateien nie verändern** – alle Anpassungen zur Laufzeit im Host (`etshost.cs`).
- **Laufendes ETS nicht ungefragt beenden** (`wineserver -k` verwirft offene Projekte). Vorher prüfen:
  `limactl shell wine-arm64 -- pgrep -f ETS6N.exe`.
- Nach Änderungen an Host/Brücke mit dem echten Dongle testen; Erfolg = ETS-Log „pre-check: User is licensed“.

## Architektur (Details: README.md, INSTALL-LOG.md)
- **VM**: Lima `wine-arm64` (vz, Fedora 45 aarch64, Vorlage `vm/wine-arm64.yaml`). Grund: macOS nullt x18,
  hat 16-KB-Seiten und RWX nur mit `MAP_JIT` (gemessen mit `tools/macprobe.c`).
- **Wine** 11.21 aarch64, `/opt/wine-arm64`, Quelltext `~/src/wine-11.21` (+ `ets-arm64/patches/0001–0006`),
  Build `~/src/build-11.21`. Prefix `~/.wine-ets`.
- **.NET 4.8.1 ARM64** aus `NDP481-x86-x64-AllOS-ENU.exe` (CAB `…arm64.cab`, CBS-Manifeste) →
  `scripts/21-install-netfx.py`; GAC_64 → GAC_ARM64 kopieren; `mscoree`-Shim `scripts/mscoree_shim.c`
  (gleiche Ordinals wie mscoreei, #24 NONAME, **kein** `_CorExeMain2` #142) nach system32 **und** FrameworkArm64.
- **ETS-Start**: `C:\ets-arm64\ETS6.exe` (ARM64-Launcher, ETS6N prüft Elternprozessnamen „ETS6“, hält Mutex)
  → `C:\ets-arm64\ETS6N.exe` = Host (`scripts/etshost.cs`, eigene AppDomain, braucht eigene `.config` =
  per `scripts/make-host-config.py` aus ETS6N.exe.config erzeugt, + Laufzeitschalter)
  → `C:\Program Files (x86)\ETS6\ETS6N.exe`.
- **Dongle**: CloudKit wird deaktiviert (nur x86/x64-Windows). DevKit: Host ruft den **Unix**-Lader auf
  (lädt `DevKit.so` über `C:\windows\system32\libdl.so` = `scripts/libdl_bridge.c`, CWD dabei `Z:\tmp`),
  bildet die 53 Funktionen per Exportname auf die **Windows**-Delegattypen ab (eigene Typen je Pfad!).
- **USB**: Mac `ets-arm64/hidbridge/knx-hidbridge` (IOHIDManager) → TCP 127.0.0.1:7531 → VM-Dienst
  `uhid-bridge` → `/dev/uhid` → `/dev/hidrawN` (udev 0666). Stick darf nicht an Parallels hängen.
- **Anzeige** XQuartz via `ssh -Y`; **Mac-Dateien** `~/ETS-Daten` = `H:`; **Start** `scripts/ets-start.sh`
  bzw. `~/Applications/ETS6 ARM64.app` (`scripts/make-app.sh`).

## Häufige Befehle
```bash
limactl shell wine-arm64 -- bash -c '…'          # in der VM ausführen
# Wine-Umgebung in der VM (Programme):
export PATH=/opt/wine-arm64/bin:$PATH WINEPREFIX=$HOME/.wine-ets WINEDLLOVERRIDES="mscoree=n;mshtml=" WINEDEBUG=-all
# wineboot -u nur mit WINEDLLOVERRIDES="mscoree,mshtml=" (sonst unsichtbarer Mono-Dialog)
# C# bauen (AnyCPU läuft dank Patch 0001 als ARM64):
wine 'C:\windows\Microsoft.NET\FrameworkArm64\v4.0.30319\csc.exe' /nologo /platform:anycpu x.cs
ETS_ARM64_ENV="ETS_ARM64_FC_ALL=1" ets-arm64/scripts/ets-start.sh   # Diagnose-Start
```
Logs: Host `~/.wine-ets/drive_c/users/$USER/AppData/Local/Temp/ets-arm64-host.log`,
ETS `…/AppData/Local/KNX/ETS6/Log/ETS6.log` (VM); Mac `~/Library/Logs/ets-start.log`, `knx-hidbridge.log`.
Diagnose-Variablen des Hosts: `ETS_ARM64_FC_ALL=1`, `ETS_ARM64_DEVKIT_EVENTS=1`, `ETS_ARM64_WATCHDOG=<s>`.
Analysewerkzeuge (`ets-arm64/tools/`): `pinv.cs` (P/Invokes), `typeinfo.cs`, `ildump.cs`, `ilscan.cs`, `pescan.py`.

## Offen
- Gesamt-Installationsskript (Lima-VM → Wine-Build → .NET → mscoree/libdl/Host → Schriften → Registry
  `wine/ets-settings.reg` → `vm/install-system.sh` → App). Bausteine liegen in `scripts/` und `vm/`.
- EIBA-sc-Dateien für Plugin-Geräte-Import (aus Ets6Setup.exe/Windows, INSTALL-LOG §24c).
- Cloud-Lizenz (nicht nativ möglich), Neustart aus ETS heraus, Patch 0003 noch nötig?, Wine-Upstream-MRs.

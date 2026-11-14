# ETS6 auf Apple Silicon ohne Rosetta – Installationsprotokoll

Ziel: ETS6 (ETS6N.exe, .NET AnyCPU) mit .NET Framework 4.8.1 **ARM64** in einem
**ARM64-Wine** ausführen, ohne Rosetta und ohne x86-Emulation. Wine läuft in einer
Mini-VM (Lima, Apple Virtualization.framework `vz`) mit Fedora aarch64 (4K-Seiten).

Jeder Schritt ist mit exakten Befehlen und Ergebnis protokolliert, damit daraus
später `install-ets-arm64.sh` erzeugt werden kann.
Legende: ✅ erfolgreich · ❌ fehlgeschlagen · ⚠️ Workaround · 🔁 im Skript übernehmen

---

## 0. Ausgangslage (2026-11-13)

| Punkt | Wert |
|---|---|
| Host | Apple M3 Max, 36 GB RAM, macOS 27.0.1 (26A434) |
| Freier Platz | ~94 GB |
| Homebrew | /opt/homebrew |
| Vorhanden | Parallels Desktop 19.4.3 (Standard, kein `prlctl exec`), VM „Windows 11“ (ARM) |
| ETS | ETS6-Programmverzeichnis nach `/Users/mgeramb/ETS6` kopiert |

### 0.1 Analyse ETS6-Binärdateien (pescan.py)
556 PE-Dateien: 544 .NET AnyCPU; nativ/x86 nur:
`ETS6.exe` (x86-Launcher, umgehbar → `ETS6N.exe` direkt starten),
`ETS6C.exe` (32-Bit-Kompatibilitätsmodus), `EtecDOM/EtecFwk/EtecCtrl/Falcon/Hawk.dll`
(x86 C++/CLI, nur Kompatibilitätsmodus), `EtecRedirect.dll`, `PluginInstaller.exe`,
`runtimes/win-{x86,x64}/native/WebView2Loader.dll` (**kein arm64** → nachrüsten).

### 0.2 Warum kein natives macOS-Wine (macprobe.c, auf dem Host gemessen)
- Seitengröße 16384; mmap/mprotect mit 4K-Granularität → EINVAL
- x18 wird bei jedem Syscall/Kontextwechsel/Signal auf 0 gesetzt (Windows-ARM64 TEB-Register)
- RWX-Mapping ohne MAP_JIT → EPERM
→ Windows-ARM64-Code kann unter macOS nicht nativ laufen. Lösung: Linux-Mini-VM.

---

## 1. Host: Lima installieren 🔁

```bash
brew install lima
limactl --version        # -> limactl version 2.3.0
```
✅

## 2. Host: Fedora-aarch64-VM anlegen 🔁

Gemeinsame Ordner:
- `/Users/mgeramb/ETS6` → `/mnt/ets6` (nur lesen)
- Arbeitsordner (`share/`) → `/mnt/share` (schreibbar)

```bash
limactl create --name=wine-arm64 --vm-type=vz --cpus=8 --memory=12 --disk=40 \
  --mount-none \
  --set '.mounts=[{"location":"$HOME/ETS6","mountPoint":"/mnt/ets6","writable":false},
                  {"location":"<SHARE>","mountPoint":"/mnt/share","writable":true}]' \
  --tty=false template:fedora
limactl start wine-arm64
```
✅ Dauer ca. 3–5 min (Download Fedora-Cloud-Image).

Ergebnis (`limactl shell wine-arm64`):
| Prüfung | Ergebnis |
|---|---|
| Distribution | Fedora release 45, Kernel 7.2.11-300.fc45.aarch64 |
| `getconf PAGESIZE` | **4096** ✅ |
| CPUs / RAM | 8 / 11 GiB |
| `/mnt/ets6` | ETS-Programmdateien sichtbar ✅ |

## 3. VM: Eignung für Windows-ARM64-Code prüfen (macprobe.c)

```bash
sudo dnf -y install gcc
gcc -O1 -ffixed-x18 -o /tmp/macprobe /mnt/share/macprobe.c -lpthread && /tmp/macprobe
```
| Test | macOS-Host | Fedora-VM |
|---|---|---|
| Seitengröße | 16384 ❌ | 4096 ✅ |
| x18 im Rechenloop (16 Threads konkurrierend) | nach ~14k Iter. 0 ❌ | 200M Iter. erhalten ✅ |
| x18 über Syscalls/yield | 100000/100000 gelöscht ❌ | 0/100000 ✅ |
| x18 im Signal-Handler | 0 ❌ | erhalten ✅ |
| RWX-mmap ohne MAP_JIT | EPERM ❌ | ok ✅ |
| 4K mprotect / MAP_FIXED 4K | EINVAL ❌ | ok ✅ |
| Adresse 0x10000 | belegt (PAGEZERO) | ok ✅ |

→ Die Lima/vz-VM ist für natives Windows-ARM64 geeignet.

## 4. VM: Wine für aarch64 bauen 🔁

Fedora 45 liefert für aarch64 **kein** Wine-Paket (`dnf repoquery wine` leer) → Build aus Quelltext.
Aktuelle Version laut https://dl.winehq.org/wine/source/11.x/: **wine-11.21**.

Skript: `share/10-build-wine.sh` (Abhängigkeiten via dnf, `configure --enable-archs=aarch64
--with-mingw=clang --prefix=/opt/wine-arm64`, `make -j8`, `make install`).
Log: `share/10-build-wine.log`.

Status: Build läuft …

## 5. .NET Framework 4.8.1 ARM64 direkt von Microsoft 🔁

Statt Export aus einer Windows-VM: offizieller Offline-Installer.

```bash
# Link-ID 2203305 = Offline-Installer (2203304 = Web-Installer, 2203306 = DevPack)
curl -fLO https://download.microsoft.com/download/4/b/2/cd00d4ed-ebdd-49ee-8a33-eabc3d1030e3/NDP481-x86-x64-AllOS-ENU.exe
# SHA256 c0ca2e0c9cd18a24a0a77369a13fae2c2c4e8bc83355dd24e5ddc00f9d791fe3  (77.688.504 Bytes)
7zz x -ox NDP481-x86-x64-AllOS-ENU.exe
7zz x -oarm64 x/xarm64-Windows10.0-KB5011048-arm64.cab     # 376 MB CAB -> 3100 Dateien, 366 MB
```
✅ Ergebnis:
- Enthält `Windows10.0-KB5011048-x86.cab`, `x64-…-x64.cab`, **`xarm64-…-arm64.cab`**
- CAB-Inhalt = CBS-Komponenten (`<arch>_<name>_<token>_<ver>_none_<hash>/` + `.manifest`)
- **Alle 554 DLL/EXE sind vollständige PE-Dateien** (keine PA30-Deltas) ✅
- Architekturen im ARM64-CAB: 74 nativ ARM64, 14 .NET ARM64-only, 255 .NET AnyCPU,
  27 C++/CLI, plus x64- und x86-Kopien (für emulierte Prozesse; nicht benötigt)
- ARM64-Dateien heißen `ARM64_<name>` und werden laut Manifest umbenannt (`destinationName`)
- Ziele laut Manifest (`destinationPath`):
  - `%windir%\Microsoft.NET\FrameworkArm64\v4.0.30319\` (148 Dateien, + `WPF\`, `Config\`)
  - `%windir%\Microsoft.NET\Framework64\v4.0.30319\` (x64), `…\Framework\v4.0.30319\` (x86)
  - `%windir%\System32\`: `ARM64_{vcruntime140,vcruntime140_1,msvcp140,ucrtbase}_clr0400.dll`, `aspnet_counters.dll`
  - GAC-Assemblies: Manifeste ohne `destinationPath` → GAC (Layout noch zu klären)
- Registry: 179 Manifeste mit `<registryKey>`, ~11.000 `<registryValue>` → per Skript in .reg umwandeln
- Kernkomponenten ARM64 vorhanden: `clr.dll`, `clrjit.dll`, `mscoreei.dll`, `wpfgfx_v0400.dll`,
  `PresentationNative_v0400.dll`, `ngen.exe`, `csc.exe`

⚠️ **`mscoree.dll` (System32-Shim) fehlt** – gehört zu Windows selbst, nicht zum .NET-Paket.
`mscoreei.dll` (ARM64) exportiert alle relevanten Funktionen (125 Exporte, u. a. `_CorExeMain`,
`_CorDllMain`, `CLRCreateInstance`, `CorBindToRuntimeEx`, `GetCORVersion`, `DllGetClassObject`, `ND_*`).
→ Plan: eigene ARM64-`mscoree.dll` als reine Export-Weiterleitung auf `mscoreei.dll` mit
clang/lld-link bauen, Wine-Override `mscoree=n`.

Hilfsskripte: `tools/pescan.py` (Architektur/CorFlags), `tools/peexports.py` (Exportliste).

Manifest-Struktur (für eigenen „CBS-Mini-Installer“):
- `<file name sourceName destinationPath destinationName>` → Zieldatei
- `<link destination=…>` → zusätzliche Kopien, u. a. **GAC**: `%windir%\Microsoft.NET\Assembly\GAC_MSIL\<Name>\v4.0_<Ver>__<Token>\` (249 Links, nur GAC_MSIL)
- `<registryKey keyName><registryValue name valueType value>` (7457 Keys / 11182 Values; Roots HKLM\SOFTWARE, HKLM\SYSTEM, HKCR)
- Variablen: `$(runtime.windows)`, `$(runtime.system32)`, `$(runtime.inf)`, `$(runtime.wbem)`, `$(string.*)`
- `<ngen>`-Hinweise ignorieren (JIT reicht), `<securityDescriptor>` ignorieren

## 4b. Wine-Build Ergebnis
✅ `wine-11.21` installiert unter `/opt/wine-arm64` (Build ~20 min, 8 vCPU).
configure-Hinweise (unkritisch): kein libpcsclite, libva-drm, libgphoto2, OSSv4, libcapi20, libnetapi.

## 6. VM: Anzeige + Wine-Prefix (ARM64) 🔁

```bash
sudo dnf -y install xorg-x11-server-Xvfb xorg-x11-xauth mesa-dri-drivers
Xvfb :99 -screen 0 1920x1080x24 &          # vorerst headless; GUI-Lösung folgt
export PATH=/opt/wine-arm64/bin:$PATH WINEPREFIX=$HOME/.wine-ets DISPLAY=:99
export WINEDLLOVERRIDES="mscoree,mshtml="  # WICHTIG: sonst hängt wineboot am Mono/Gecko-Dialog
wineboot -i && wineserver -w               # ~3 s
wine cmd /c "ver & echo %PROCESSOR_ARCHITECTURE%"
```
Ergebnis: `Microsoft Windows 10.0.19045`, `ARCH=ARM64`, `kernel32.dll: PE32+ … ARM64` ✅
⚠️ Erster Versuch ohne Override hing endlos in `rundll32 setupapi,InstallHinfSection … wine.inf`
(Mono-Installationsdialog unsichtbar auf Xvfb).
ℹ️ `failed to start …\syswow64\rundll32.exe: c0000135` – erwartet, kein x86/WoW64 gebaut.

## 7. .NET 4.8.1 ARM64 in den Prefix installieren 🔁

```bash
bash /mnt/share/20-get-netfx.sh            # Download (SHA256-geprüft) + Entpacken nach ~/netfx481/arm64
python3 /mnt/share/21-install-netfx.py ~/netfx481/arm64 $WINEPREFIX ~/netfx481/netfx-arm64.reg
wine regedit /S 'Z:\home\<user>\netfx481\netfx-arm64.reg'
```
✅ stats: 1389 arm64-Manifeste, 1029 Dateien, 754 Links (u. a. 248 GAC_MSIL-Assemblies), 3778 Keys,
5712 Values, 0 fehlend, 0 Parse-Fehler. Zusätzlich gesetzt: `.NETFramework\InstallRoot(+Arm64)`,
`NDP\v4\Full|Client` Release=533320 (4.8.1).
⚠️ Fedora: `7zip`-Paket heißt Binary `7z` (nicht `7zz`) – Skript erkennt beides.

## 8. Wine-Patch: AnyCPU-.NET-Programme nativ als ARM64 starten 🔁

Problem: `wine hello.exe` (AnyCPU, PE32-Header) → `failed to load …ntdll.dll error c00000bb`.
Ursache: Wine bildet `IMAGE_FLAGS_ComPlusNativeReady` auf 64-Bit-Hosts fest auf **AMD64** ab
(`is_machine_64bit(native_machine) ? IMAGE_FILE_MACHINE_AMD64 : native_machine`) – 3 Stellen in
`dlls/ntdll/unix/loader.c` (2×) und `server/mapping.c` (1×).
Fix: `share/patches/0001-anycpu-il-native-arm64.patch` → `native_machine`. Build-Skript wendet
`patches/*.patch` automatisch an (`patch -p1`, idempotent). Danach Neubau (inkrementell ~3 min).
⚠️ Nach jedem Wine-Neubau aktualisiert sich der Prefix (wine.inf) → immer mit
`WINEDLLOVERRIDES="mscoree=n;mshtml="` starten, sonst hängt der unsichtbare Mono/Gecko-Dialog.

## 9. Eigene ARM64-`mscoree.dll` (Shim) 🔁

`share/22-build-mscoree.sh` + `share/mscoree_shim.c` (clang `--target=aarch64-windows-msvc`, lld-link,
Importlib via `llvm-dlltool`). Iterationen:
| Version | Ergebnis |
|---|---|
| nur Forwarder (`/noentry`) | Wine-Loader: `c000007a` – `fixup_imports_ilonly` nutzt `RtlFindExportedRoutineByName`, folgt keinen Forwardern |
| + echte `_CorExeMain`/`_CorDllMain` (laden `FrameworkArm64\…\mscoreei.dll`) | CLR startet, AV in `clr.dll+0xFD27C`: `GetProcAddress(mscoree, #24)` liefert falsche Funktion (Ordinals fortlaufend statt original) |
| + Ordinals 1:1 wie mscoreei (17…142, #24 NONAME) | AV in `mscoreei+0x122A8`: `mscoreei!_CorExeMain` holt `mscoree#142` (`_CorExeMain2`) → Forwarder springt rekursiv in mscoreei zurück |
| + `DllMain` → `mscoreei!OnShimDllMainCalled` | (beibehalten, wie Original) |
| **+ `_CorExeMain2` (#142) NICHT exportieren** | ✅ **läuft** |

Installation: Shim nach `system32\mscoree.dll` **und** `FrameworkArm64\v4.0.30319\mscoree.dll`
(clr.dll lädt mscoree auch aus dem eigenen Verzeichnis). Override `HKCU\Software\Wine\DllOverrides
mscoree=native`, Windows-Version `win11`.

Test `share/test/hello.cs` (csc.exe aus FrameworkArm64, `/platform:anycpu`):
```
Hallo aus .NET 4.0.30319.42000 | .NET Framework 4.8.9195.0
ProcessArchitecture: Arm64, 64bit=True
Runtime dir: C:\windows\Microsoft.NET\FrameworkArm64\v4.0.30319\
Dictionary ok: 100000
```
✅ **.NET Framework 4.8.1 läuft nativ als ARM64 – ohne Rosetta, ohne x86-Emulation.**

Debug-Hilfen (nicht im Installer): temporärer TRACE `GPA …` in `LdrGetProcedureAddress`
(`dlls/ntdll/loader.c`, Original in `/tmp/loader.c.orig`), `winedbg` per stdin (`printf "c\nbt\n" | winedbg x.exe`),
`llvm-objdump -d`, `llvm-readobj --coff-exports/--coff-imports`.

## 10. GAC für prozessorspezifische Assemblies 🔁

Problem: `FileNotFoundException: PresentationCore` – 12 Manifeste haben `<gac clrarch="arm64">` ohne
`<link>` (u. a. mscorlib, PresentationCore, System.Data, System.Web, System.Transactions, System.Printing).
Lösung: `21-install-netfx.py` schreibt deren Pfade nach `netfx-arm64-gac.txt`; `scripts/gacinstall.cs`
ruft `fusion.dll!CreateAssemblyCache` → `IAssemblyCache::InstallAssembly` (alle 12 OK).
⚠️ Fusion legt sie unter `GAC_64` ab, der ARM64-Loader sucht aber in **`GAC_ARM64`**
(Strings in clr.dll: `GAC_32 GAC_64 GAC_ARM64 GAC_MSIL`) → danach `GAC_64` nach `GAC_ARM64` kopieren.
Fusion-Log zur Diagnose: `HKLM\Software\Microsoft\Fusion` EnableLog/ForceLog/LogFailures=1, LogPath=C:\fusionlog\.
Ergebnis: WPF-Fenstertest (`tests/wpf2.cs`) ✅.

## 11. Wine-Patch 0002: `combase!RoGetParameterizedTypeInstanceIID` 🔁

Stub lieferte `E_NOTIMPL` → CLR wirft intern `HRException` (65× beim ETS-Start). Implementiert nach
WinRT-Spezifikation (Signatur `pinterface({piid};…)`, `struct(…)`, `enum(…)`, `rc(…)`, SHA-1/UUIDv5 mit
Namespace `11f47ad5-7b73-42c0-abae-878b1e16adee`), inkl. `RoFreeParameterizedTypeExtra` /
`RoParameterizedTypeExtraGetTypeSignature`. Verifiziert: IIterable<String> = `e2fcc7c1-3bfc-5a0b-b2b0-72e769d1cb7e`.

## 12. Schriften für WPF 🔁 (`scripts/40-fonts.sh`)

Problem: WPF `FailFast("Unrecoverable system error")` in `TypefaceMap.MapUnresolvedCharacters`.
Ursache: WPF findet Schriften über den **internen Familiennamen**; eine nur umbenannte Datei genügt nicht.
Lösung: Liberation/DejaVu (Fedora) nach `C:\windows\Fonts`, „Segoe UI“ = Liberation Sans mit per fonttools
umbenannter Familie (`rename-font.py`, OFL-konform), Wine-Schriften kopieren, eigene „Segoe MDL2 Assets“
(`make-segmdl2.py`: Fensterknöpfe + ganze Private-Use-Area). Plus `HKCU\Software\Microsoft\Avalon.Graphics
DisableHWAcceleration=1`.

## 13. ETS-Start ohne x86-Launcher 🔁

ETS6N.exe prüft, ob der **Elternprozess „ETS6“** heißt (IL von `ETS.App.CrKuL5Ogv`), sonst Neustart über
den x86-Launcher. Lösung: eigener ARM64-Launcher `C:\ets-arm64\ETS6.exe` (`scripts/ets6-arm64-launcher.cs`,
hält auch den Launcher-Mutex `Global\KNXETS6_54E11E05-…`). ETS-Dateien bleiben unverändert.

## 14. Wine-Patches 0003/0004: ARM64-Ausnahmebehandlung 🔁

Symptome: Endlosschleife im Unwinder, AV mit „ompilerS“ (ASCII) in x20, Absturz in `~HENUMInternalHolder`.
Analyse mit öffentlichen Symbolen (`clr.pdb` von msdl.microsoft.com, GUID C5E24E8B…F3) + `llvm-pdbutil`.
- **0004 (Hauptursache):** ARM64-`.pdata`-Einträge mit **Flag = 3** enthalten keine Unwind-Daten, sondern
  die RVA eines anderen `.pdata`-Eintrags (geteilte Daten, z. B. für Funktionsfragmente) und haben keine
  eigene Länge (reichen bis zum nächsten Eintrag). clr.dll: 20 220 von 96 628 Einträgen! Wine las sie als
  gepackte Daten. Fix in `find_function_info_arm64`/`RtlVirtualUnwind2`.
- **0003:** Consolidate-Callback (C++-catch) mit x19–x28/d8–d15 aus dem Ziel-Kontext aufrufen und
  zurückschreiben (MSVC-ARM64-Funclets nutzen die Register des Elternframes). Ob nach 0004 noch nötig: offen.
Tests: `tests/asmfail.cs`, `tests/cbexc.cs` ✅.

## 15. Shim-Konfiguration 🔁

Meldung „This application could not be started“ (Shim will .NET 2.0 / `fondue.exe NetFx3` starten).
Diagnose: `COMPlus_CLRLoadLogDir=C:\clrlog`. Ursache: Host-EXE hatte keine `.config` →
`useLegacyV2RuntimeActivationPolicy` fehlte. Lösung: `ETS6N.exe.config` neben den Host kopieren.
⚠️ Wine-AeDebug abschalten (`AeDebug\Auto=0`, kein `Debugger`), sonst „hängt“ ein Crash in `winedbg --auto`.

## 16. KNX-native Bibliotheken (Dongle/Cloud-Lizenz) ⚠️ BLOCKER für lizenzierten Betrieb

`Knx.DevKit.dll` und `Knx.CloudKit.dll` enthalten verschlüsselte native Bibliotheken
(`Knx.DevKit32/64.Win`, `…32/64.Unix`, `…32/64.Arm`; `Knx.CloudKit32/64.Win`), die per eigenem Loader in
den Speicher gemappt werden. Unter Windows wird **immer** die x86/x64-Variante gewählt
(`IsWindows → Win`, „Arm“ nur für Linux) → `EXCEPTION_ILLEGAL_INSTRUCTION` im ARM64-Prozess.
Workaround (ohne ETS-Dateien zu ändern): `scripts/etshost.cs` = ARM64-Host `C:\ets-arm64\ETS6N.exe`,
startet ETS in eigener AppDomain (Basis = ETS-Ordner, Original-Config) und setzt vorher per Reflection
das „Laden fehlgeschlagen“-Flag beider `*.Secure.NativeManager` (aus dem IL von `Load()` ermittelt).
**Ergebnis: ETS 6.4.1 startet nativ ARM64 bis ins Hauptfenster – aber nur als „ETS6 Demo“.**
Screenshots: `ets-arm64/screenshots/01-splash-arm64.png`, `02-mainwindow-arm64.png`.

## 17. Anzeige auf dem Mac über XQuartz 🔁 (`scripts/ets-start.sh`)

```bash
brew install --cask xquartz          # 2.8.6, Universal-Binary (arm64 nativ); braucht Admin-Passwort
# VM: X11-Forwarding + Werkzeuge
echo "X11Forwarding yes" | sudo tee /etc/ssh/sshd_config.d/50-x11.conf && sudo systemctl reload sshd
sudo dnf -y install xorg-x11-xauth xdpyinfo
# Mac: Start
DISPLAY=:0 ssh -F ~/.lima/wine-arm64/ssh.config -Y -o XAuthLocation=/opt/X11/bin/xauth \
    -o ControlMaster=no -o ControlPath=none lima-wine-arm64 'wine "C:\ets-arm64\ETS6.exe"'
```
- `DISPLAY` setzt XQuartz erst nach erneutem Anmelden → Skript nutzt `:0` (Socket `/tmp/.X11-unix/X0`).
- `XAuthLocation=/opt/X11/bin/xauth` nötig, sonst kein Forwarding (VM: `DISPLAY` leer).
- Test: `xdpyinfo` über Forwarding → 1728×1084, RENDER/RANDR/XInput/MIT-SHM ✅.
- Vor dem Start laufende Xvfb-/wineserver-Sitzung beenden (ein wineserver pro Prefix).
✅ ETS startet und erscheint als XQuartz-Fenster auf dem Mac.
⚠️ News-Download: „Could not create SSL/TLS secure channel“ (Wine schannel/TLS, offen).

## 18. Mac-Dateien in ETS: Laufwerk H: 🔁

Bewusst nur ein eigener Ordner (nicht das ganze Home, Schreibzugriff begrenzt):
```bash
mkdir -p ~/ETS-Daten
limactl stop wine-arm64
limactl edit wine-arm64 --tty=false --set \
  '.mounts += [{"location":"'$HOME'/ETS-Daten","mountPoint":"/mnt/ets-daten","writable":true}]'
limactl start wine-arm64
# in der VM:
ln -sfn /mnt/ets-daten ~/.wine-ets/dosdevices/h:
```
✅ Lesen/Schreiben in beide Richtungen getestet (Datei vom Mac in Wine lesbar, von Wine angelegte Datei
und Ordner sofort im Finder sichtbar).
⚠️ Der Mount `/mnt/share` zeigt noch auf einen temporären Arbeitsordner → im Installer durch einen festen
Ordner ersetzen oder entfernen.

### Bekannt
- „ETS neu starten“ aus ETS heraus funktioniert nicht (vermutlich startet ETS dabei den x86-Launcher).

## 19. Internet / HTTPS aus ETS 🔁

Symptom: „Could not create SSL/TLS secure channel“; Test (`tests/net.cs`): HTTPS mit `SystemDefault`
→ „The specified value is not valid in the 'SslProtocolType' enumeration“, mit expliziter Tls12/Tls13 OK.
VM selbst (curl, ping) und .NET-Ping funktionieren.
- **Patch 0005** (`secur32`): `get_protocol()` kannte `GNUTLS_TLS1_3` nicht und lieferte 0 → jetzt
  `SP_PROT_TLS1_3_CLIENT` (korrekt, aber allein nicht ausreichend).
- **Registry (Lösung)**, Standard-Schalter von .NET 4.x:
  ```bash
  for k in 'HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319' 'HKLM\SOFTWARE\WOW6432Node\Microsoft\.NETFramework\v4.0.30319'; do
    wine reg add "$k" /v SystemDefaultTlsVersions /t REG_DWORD /d 0 /f
    wine reg add "$k" /v SchUseStrongCrypto      /t REG_DWORD /d 1 /f
  done
  ```
  → `SecurityProtocol = Tls, Tls11, Tls12, Tls13` statt SystemDefault; `tests/net48.cs` (Ziel .NET 4.8 wie ETS) ✅.
✅ ETS: Update-Prüfung („UpdatesAvailable“) und Dashboard-Downloads von media.knx.org erfolgreich.
ℹ️ ETS meldet weiterhin „ICMP Ping reported: TimedOut“ (eigene Erreichbarkeitsprüfung), ohne Auswirkung.

## 20. Lizenz unter Windows on ARM (Parallels) – geklärt
Im Task-Manager der Windows-11-ARM-VM läuft `ETS6N.exe` als **x64** (Prism-Emulation). KNX liefert die
Lizenz-/Dongle-Bibliotheken nur für x86/x64 – auch unter Windows on ARM wird ETS also emuliert.

## 21. Lizenz-Dongle nativ ARM64 ✅ (libdl-Brücke + DevKit-Linux-Bibliothek + HID-Brücke)

**Ergebnis: ETS-Log „pre-check: User is licensed“ – License Stick wird nativ (ohne Rosetta/x64) erkannt.**

### 21a. libdl.so-Brücke (`scripts/libdl_bridge.c`, Bau `scripts/22b-build-libdl.sh` → `C:\windows\system32\libdl.so`)
ARM64-PE-DLL, die dem Unix-Lader von Knx.DevKit (`[DllImport("libdl.so")] dlopen/dlsym`) echte glibc-Funktionen liefert:
- eigene Linux-PID per Syscall (`svc #0`, x8=172) – `/proc/self` liefert im Wine-Kontext den wineserver;
- `/proc/<pid>/maps` → libc.so.6 → `.dynsym` → `dlopen`/`dlsym`/`dlclose`/`dlerror`;
- jeder Aufruf über Thunk (sichert x18 = Windows-TEB), `dlsym` liefert ebenfalls einen Thunk.

### 21b. Host (`scripts/etshost.cs` → `C:\ets-arm64\ETS6N.exe`, Bau: csc FrameworkArm64 `/platform:anycpu`)
`EnableDevKitArm64`:
1. Unix-Lader (P/Invoke `libdl.so!dlopen`) und Windows-Lader (kernel32-P/Invokes + `Dictionary<Type,Delegate>`) per Reflection finden.
2. Unix-Ladefunktion `bool X(bool)` aufrufen. ⚠️ Sie schreibt `/tmp/DevKit.so` – .NET legt den Pfad auf das
   *aktuelle Laufwerk* → Arbeitsverzeichnis währenddessen auf `Z:\tmp`.
3. ⚠️ **Windows- und Unix-Pfad benutzen je eigene Delegattypen** pro Funktion (Fehlerbild: Lookup
   `I64o2erXj8U<T>()` wirft `System.Exception` tausendfach aus `DeviceManager`-Timer, ETS: „User is not licensed“).
   Lösung: Zuordnung über den Exportnamen (`Dictionary<string,Type>` beider Lader); Windows-Delegat =
   `Marshal.GetDelegateForFunctionPointer(GetFunctionPointerForDelegate(unixDelegat), winTyp)`.
   Host-Log: „53 Funktionen auf Windows-Delegattypen abgebildet, 0 fehlen“.
4. „geladen“-Flag des Windows-Laders setzen. CloudKit bleibt deaktiviert (nur x86/x64-Windows-Code).
Diagnose-Umgebungsvariablen (über `ETS_ARM64_ENV="…" ets-start.sh`): `ETS_ARM64_FC_ALL=1` (alle
First-Chance-Exceptions aus DevKit/Licensing mit Stack), `ETS_ARM64_DEVKIT_EVENTS=1`, `ETS_ARM64_WATCHDOG=<s>`.

### 21c. HID-Brücke Mac → VM (`hidbridge/`)
- Mac: `hidbridge/knx-hidbridge` (C, IOHIDManager; `clang -O2 -o knx-hidbridge knx-hidbridge.c -framework IOKit -framework CoreFoundation`),
  verbindet zu 127.0.0.1:7531 (Lima-Portweiterleitung) und überträgt Deskriptor, Input-/Output-/Feature-Reports.
- VM: `/opt/ets-arm64/uhid-bridge.py` als systemd-Dienst `uhid-bridge` (root) → `/dev/uhid` → `/dev/hidrawN`
  mit `HID_ID=0003:00002A07:00000102`, `HID_UNIQ=<Seriennr.>`.
- `/etc/modules-load.d/uhid.conf` (`uhid`), udev `/etc/udev/rules.d/90-knx-dongle.rules`:
  `KERNEL=="hidraw*", SUBSYSTEMS=="hid", KERNELS=="*:2A07:0102.*", MODE="0666"`.
- ⚠️ Stick darf nicht an Parallels hängen (Parallels → Geräte → USB & Bluetooth → Häkchen entfernen / „Mit Mac verbinden“).
- DevKit.so liest direkt `/sys/class/hidraw` + `/dev/hidrawN` (kein Wine-HID nötig); Wine-winebus öffnet hidraw parallel, stört nicht.

### 21d. ets-start.sh-Fix
`wineserver -k` lief vor dem Setzen von `WINEPREFIX` (traf `~/.wine`) → alte ETS-Instanz blieb, neue beendete sich
wegen Einzelinstanz-Mutex (Exitcode -1). Jetzt: erst exportieren, dann `wineserver -k; wineserver -w`.

## 22. Start per Finder/Dock: „ETS6 ARM64.app“ 🔁 (`scripts/make-app.sh`)

```bash
ets-arm64/scripts/make-app.sh            # -> ~/Applications/ETS6 ARM64.app (Ziel-Ordner als 1. Argument)
```
- AppleScript-Applet (`osacompile`), startet `ets-start.sh` per `nohup` losgelöst und beendet sich sofort;
  `LSUIElement` (kein Dock-Symbol für das Applet), PATH um `/opt/homebrew/bin` ergänzt (Finder-Apps haben nur `/usr/bin:/bin`).
- Log: `~/Library/Logs/ets-start.log` (Start) und `~/Library/Logs/knx-hidbridge.log` (Dongle-Brücke).
- Symbol: aus der lokalen ETS-Installation extrahiert (`tools/exe-icon.py` → `.ico` → `sips`/`iconutil` → `.icns`;
  `ETS_DIR`, Standard `~/ETS6`, oder `ETS_ICON=…icns`). KNX-Symbol wird nicht ins Repo übernommen.
- ⚠️ Plist-Änderung bricht die Ad-hoc-Signatur → am Ende `codesign --force --sign -`.
- Die App verweist auf den absoluten Pfad von `ets-start.sh` → nach Verschieben des Repos neu erzeugen.

`ets-start.sh`: **Schutz gegen Doppelstart** – läuft `ETS6N.exe` bereits in der VM, kein Neustart (sonst würde
`wineserver -k` ein offenes Projekt ohne Speichern beenden), nur Mitteilung „ETS läuft bereits“. Erzwingen: `ETS_FORCE_RESTART=1`.
✅ Getestet bei laufendem ETS (Mitteilung, ETS bleibt offen) und als Kaltstart über die App: Brücke „verbunden“, ETS „User is licensed“.

---

## Offene Punkte / Ideen

### Wine-Upstream (Merge Request auf gitlab.winehq.org, durch den Nutzer)
- Patch 0001 nur bedingt annehmbar: AMD64-Mapping existiert vermutlich wegen wine-mono (nur x86/x64).
  Für Upstream: ARM64 nur, wenn eine ARM64-fähige CLR installiert ist (wie Windows 11) + Test in `dlls/ntdll/tests`.

### Lizenz-Dongle – Befund (Host, `ioreg`)
| Feld | Wert |
|---|---|
| Name | „License Stick KNX-XXXXXXXX“, Hersteller „KNX“ |
| VID/PID | 0x2a07 / 0x0102 (WineEts-udev-Regel: `SUBSYSTEM=="usb", ATTRS{idVendor}=="2a07", ATTRS{idProduct}=="0102"`) |
| Speed | Full-Speed, bcdDevice 0x0201, bMaxPacketSize0 64 |
| Interface 0 | **bInterfaceClass 3 (HID)**, Subclass 0, Protocol 0, 2 Endpoints (Interrupt IN/OUT) |
| macOS-Treiber | **kein IOHIDDevice gebunden** – `prl_vm_app` (Parallels) hält einen AppleUSBHostDeviceUserClient |
⚠️ Parallels beansprucht den Stick → für eine eigene Bridge muss er in Parallels auf „Mac“ gelassen werden. ✅ gelöst, siehe §21.
Laut WineEts brauchte Wine für den Dongle MR !10604 (ntoskrnl/cfgmgr32: Device-Tree, `CM_Get_Parent`) und
MR !10301 (hidclass.sys) → ETS läuft den Geräte-Baum HID → USB-Elterngerät hoch.

### USB-Passthrough (Lima/vz hat keins)
- **Plan A – HID-Bridge:** macOS-Daemon (IOHIDManager, Swift/C) liest/schreibt HID-Reports des
  Geräts → Socket → Gast-Daemon erzeugt per `/dev/uhid` ein virtuelles HID-Gerät mit identischer
  VID/PID und Report-Descriptor. Deckt KNX-USB-Schnittstelle **und** Lizenz-Dongle ab (beide HID).
- **Plan B – nur KNX-Schnittstelle:** `knxd` auf dem Mac (USB) → KNXnet/IP-Tunnel → ETS per IP.
- Nicht über ETS-.NET-Layer (signierte Assemblies, bricht bei Updates).

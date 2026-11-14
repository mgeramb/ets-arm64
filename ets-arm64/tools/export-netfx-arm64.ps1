# Exportiert .NET Framework 4.8.1 (ARM64) aus Windows 11 ARM in den Mac-Home-Ordner.
# In der Parallels-VM in einer PowerShell *als Administrator* ausfuehren.
# Ziel: ~/netfx481-arm64 auf dem Mac (Parallels-Freigabe \\Mac\Home).

$ErrorActionPreference = 'Continue'
$dst = '\\Mac\Home\netfx481-arm64'
New-Item -ItemType Directory -Force $dst | Out-Null

# 1. Framework-Verzeichnisse und GAC (ohne vorkompilierte NativeImages)
robocopy C:\Windows\Microsoft.NET "$dst\Windows\Microsoft.NET" /E /XD NativeImages* /R:0 /W:0 /NFL /NDL /NP

# 2. Shim-DLLs aus System32 / SysWOW64
$shim = 'mscoree.dll','mscorier.dll','mscories.dll','netfxperf.dll','d3dcompiler_47.dll','PresentationHost.exe','PresentationHostProxy.dll'
robocopy C:\Windows\System32 "$dst\Windows\System32" $shim /R:0 /W:0 /NFL /NDL /NP
robocopy C:\Windows\SysWOW64 "$dst\Windows\SysWOW64" $shim /R:0 /W:0 /NFL /NDL /NP

# 3. Registry
reg export 'HKLM\SOFTWARE\Microsoft\.NETFramework'          "$dst\reg-netframework.reg" /y
reg export 'HKLM\SOFTWARE\Microsoft\NET Framework Setup'    "$dst\reg-ndp.reg" /y
reg export 'HKLM\SOFTWARE\WOW6432Node\Microsoft\.NETFramework' "$dst\reg-netframework-wow.reg" /y
reg export 'HKLM\SOFTWARE\Microsoft\Fusion'                 "$dst\reg-fusion.reg" /y

# 4. Infos
"OS:      $([Environment]::OSVersion.VersionString)"                          | Out-File "$dst\info.txt"
"Arch:    $env:PROCESSOR_ARCHITECTURE"                                         | Out-File "$dst\info.txt" -Append
"Release: $((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release)" | Out-File "$dst\info.txt" -Append
Get-ChildItem C:\Windows\Microsoft.NET -Directory | Select-Object -Expand Name   | Out-File "$dst\info.txt" -Append

Write-Host "Fertig: $dst"

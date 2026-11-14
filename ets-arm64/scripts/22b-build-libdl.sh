#!/usr/bin/env bash
# Baut die ARM64-Windows-DLL "libdl.so" (Brücke zu glibc dlopen/dlsym) für Knx.DevKit.
set -euo pipefail
HERE="$(dirname "$(readlink -f "$0")")"; OUT="$1"; WORK=$(mktemp -d)
printf 'LIBRARY kernel32.dll\nEXPORTS\n  CreateFileA\n  ReadFile\n  CloseHandle\n  VirtualAlloc\n  VirtualProtect\n  FlushInstructionCache\n  GetCurrentProcess\n  OutputDebugStringA\n' > "$WORK/k32.def"
llvm-dlltool -m arm64 -d "$WORK/k32.def" -l "$WORK/k32.lib"
clang --target=aarch64-windows-msvc -O2 -fno-builtin -fno-stack-protector -ffixed-x18 -c -o "$WORK/libdl.obj" "$HERE/libdl_bridge.c"
lld-link /nologo /dll /entry:DllMain /nodefaultlib /machine:arm64 /out:"$OUT" "$WORK/libdl.obj" "$WORK/k32.lib"
llvm-readobj --coff-exports "$OUT" | grep -E "Name: dl" | tr -d " "

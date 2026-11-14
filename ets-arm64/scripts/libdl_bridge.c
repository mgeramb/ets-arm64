/* libdl.so (ARM64-Windows-DLL für Wine/aarch64)
 *
 * Erlaubt .NET-Code im Wine-Prozess, Linux-aarch64-Shared-Objects zu laden und aufzurufen:
 *   [DllImport("libdl.so")] dlopen / dlsym / dlclose / dlerror
 * Die echten glibc-Funktionen werden über /proc/self/maps + .dynsym von libc.so.6 gefunden
 * (keine Änderung an Wine nötig). Aufrufe in Linux-Code laufen über Sprungstellen, die das
 * Windows-Register x18 (TEB) sichern, da Linux-Code x18 als normales Register benutzen darf.
 *
 * Genutzt für Knx.DevKit (ETS6): dessen Unix-Pfad lädt die native ARM64-Linux-Bibliothek
 * per dlopen/dlsym und erzeugt daraus Delegaten (nur Integer/Zeiger-Argumente, <= 8).
 */
typedef unsigned char BYTE;
typedef unsigned short WORD;
typedef unsigned int DWORD;
typedef unsigned long long QWORD;
typedef void *HANDLE;
typedef int BOOL;

#define WINAPI
__declspec(dllimport) HANDLE WINAPI CreateFileA(const char *, DWORD, DWORD, void *, DWORD, DWORD, HANDLE);
__declspec(dllimport) BOOL WINAPI ReadFile(HANDLE, void *, DWORD, DWORD *, void *);
__declspec(dllimport) BOOL WINAPI CloseHandle(HANDLE);
__declspec(dllimport) void *WINAPI VirtualAlloc(void *, QWORD, DWORD, DWORD);
__declspec(dllimport) BOOL WINAPI VirtualProtect(void *, QWORD, DWORD, DWORD *);
__declspec(dllimport) BOOL WINAPI FlushInstructionCache(HANDLE, const void *, QWORD);
__declspec(dllimport) HANDLE WINAPI GetCurrentProcess(void);
__declspec(dllimport) void WINAPI OutputDebugStringA(const char *);

#define INVALID_HANDLE_VALUE ((HANDLE)(long long)-1)

/* ---- kleine Hilfsfunktionen ohne CRT ---- */
static QWORD slen(const char *s) { QWORD n = 0; while (s[n]) n++; return n; }
static int scmp(const char *a, const char *b) { while (*a && *a == *b) { a++; b++; } return (BYTE)*a - (BYTE)*b; }
static const char *sstr(const char *h, const char *n)
{
    QWORD ln = slen(n), i;
    for (; *h; h++) { for (i = 0; i < ln && h[i] == n[i]; i++); if (i == ln) return h; }
    return 0;
}
static QWORD hexval(const char **p)
{
    QWORD v = 0; char c;
    while ((c = **p)) {
        if (c >= '0' && c <= '9') v = v * 16 + (c - '0');
        else if (c >= 'a' && c <= 'f') v = v * 16 + (c - 'a' + 10);
        else break;
        (*p)++;
    }
    return v;
}

static BYTE heap[1 << 20];   /* statischer Puffer für /proc/self/maps und libc-ELF-Header */
static QWORD heap_used;
static void *halloc(QWORD n) { void *p; n = (n + 15) & ~15ull; if (heap_used + n > sizeof(heap)) return 0; p = heap + heap_used; heap_used += n; return p; }

/* liest eine Linux-Datei über Wines Laufwerk Z: komplett ein */
static BYTE *read_unix_file(const char *path, QWORD max, QWORD *size)
{
    char win[512] = "Z:"; QWORD i, n = 2; DWORD got; BYTE *buf; HANDLE h;
    for (i = 0; path[i] && n < sizeof(win) - 1; i++) win[n++] = path[i] == '/' ? '\\' : path[i];
    win[n] = 0;
    h = CreateFileA(win, 0x80000000 /* GENERIC_READ */, 7, 0, 3 /* OPEN_EXISTING */, 0, 0);
    if (h == INVALID_HANDLE_VALUE) return 0;
    if (!(buf = VirtualAlloc(0, max + 1, 0x3000, 0x04))) { CloseHandle(h); return 0; }  /* bleibt bestehen (einmalig) */
    *size = 0;
    while (*size < max && ReadFile(h, buf + *size, (DWORD)(max - *size), &got, 0) && got) *size += got;
    buf[*size] = 0;
    CloseHandle(h);
    return buf;
}

/* ---- Aufruf von Linux-Code mit gesichertem x18 ---- */
/* QWORD call_linux(void *fn, QWORD a0..a5): x0=fn, x1..x6=Argumente */
QWORD call_linux(void *fn, QWORD a, QWORD b, QWORD c, QWORD d, QWORD e, QWORD f);
__asm__(".globl call_linux\n"
        "call_linux:\n\t"
        "stp x29, x30, [sp, #-32]!\n\t"
        "mov x29, sp\n\t"
        "str x18, [sp, #16]\n\t"
        "mov x16, x0\n\t"
        "mov x0, x1\n\tmov x1, x2\n\tmov x2, x3\n\tmov x3, x4\n\tmov x4, x5\n\tmov x5, x6\n\t"
        "blr x16\n\t"
        "ldr x18, [sp, #16]\n\t"
        "ldp x29, x30, [sp], #32\n\t"
        "ret\n");

/* Sprungstelle pro Linux-Funktion (Argumente x0..x7 bleiben unverändert):
 *   stp x29,x30,[sp,#-32]! ; mov x29,sp ; str x18,[sp,#16] ; ldr x16,=target ; blr x16
 *   ldr x18,[sp,#16] ; ldp x29,x30,[sp],#32 ; ret ; .quad target            */
static const DWORD thunk_code[] = { 0xa9be7bfd, 0x910003fd, 0xf9000bf2, 0x580000b0, 0xd63f0200,
                                    0xf9400bf2, 0xa8c27bfd, 0xd65f03c0 };
#define THUNK_SIZE 48
static BYTE *thunk_page; static QWORD thunk_used;

static void *make_thunk(void *target)
{
    BYTE *t; QWORD i; DWORD old;
    if (!target) return 0;
    if (!thunk_page || thunk_used + THUNK_SIZE > 65536) {
        thunk_page = VirtualAlloc(0, 65536, 0x3000 /* COMMIT|RESERVE */, 0x04 /* RW */);
        thunk_used = 0;
        if (!thunk_page) return 0;
    }
    t = thunk_page + thunk_used; thunk_used += THUNK_SIZE;
    VirtualProtect(thunk_page, 65536, 0x04, &old);
    for (i = 0; i < sizeof(thunk_code); i++) t[i] = ((const BYTE *)thunk_code)[i];
    *(QWORD *)(t + 32) = (QWORD)target;  /* Literal für "ldr x16" (Offset 12, +20 Byte) */
    VirtualProtect(thunk_page, 65536, 0x20 /* EXECUTE_READ */, &old);
    FlushInstructionCache(GetCurrentProcess(), t, THUNK_SIZE);
    return t;
}

/* Linux-Syscall getpid (aarch64: Nr. 172) direkt, ohne Wine */
static QWORD linux_getpid(void)
{
    register QWORD x8 __asm__("x8") = 172;
    register QWORD x0 __asm__("x0");
    __asm__ volatile("svc #0" : "=r"(x0) : "r"(x8) : "memory");
    return x0;
}

/* ---- glibc-Symbole finden ---- */
static void *p_dlopen, *p_dlsym, *p_dlclose, *p_dlerror, *p_getpid;
static int resolved;
static QWORD dbg_maps_size, dbg_base, dbg_elf_size; static char dbg_path[400];

static void resolve_glibc(void)
{
    QWORD msize, fsize, base = 0, i;
    const char *maps, *line, *p;
    char path[400];
    BYTE *elf;
    if (resolved) return;
    resolved = 1;
    /* /proc/self würde (über wineserver geöffnet) den falschen Prozess zeigen -> eigene Linux-PID per Syscall */
    {
        char mp[64] = "/proc/"; char digits[24]; int nd = 0; QWORD pid = linux_getpid(), n = 6;
        do { digits[nd++] = (char)('0' + pid % 10); pid /= 10; } while (pid);
        while (nd) mp[n++] = digits[--nd];
        for (i = 0; "/maps"[i]; i++) mp[n++] = "/maps"[i];
        mp[n] = 0;
        if (!(maps = (const char *)read_unix_file(mp, 512 * 1024, &msize))) return;
    }
    dbg_maps_size = msize;
    for (line = maps; *line; ) {
        const char *nl = line; while (*nl && *nl != '\n') nl++;
        if ((p = sstr(line, "/libc.so.6")) && p < nl) {
            const char *q = line; QWORD start = hexval(&q), off;
            q++; hexval(&q); while (*q == ' ') q++; q += 5; off = hexval(&q);
            if (off == 0) {
                const char *s = line; while (s < nl && *s != '/') s++;
                for (i = 0; s < nl && i < sizeof(path) - 1; s++) path[i++] = *s;
                path[i] = 0; base = start; break;
            }
        }
        line = *nl ? nl + 1 : nl;
    }
    if (!base) { OutputDebugStringA("libdl.so-Bridge: libc.so.6 nicht in /proc/self/maps\n"); return; }
    dbg_base = base; for (i = 0; path[i]; i++) dbg_path[i] = path[i];
    if (!(elf = read_unix_file(path, 4 * 1024 * 1024, &fsize))) return;
    dbg_elf_size = fsize;
    {
        QWORD shoff = *(QWORD *)(elf + 0x28); WORD shnum = *(WORD *)(elf + 0x3c);
        for (i = 0; i < shnum; i++) {
            BYTE *sh = elf + shoff + i * 64;
            if (*(DWORD *)(sh + 4) == 11 /* SHT_DYNSYM */) {
                BYTE *str_sh = elf + shoff + *(DWORD *)(sh + 0x28) * 64;
                const char *strtab = (const char *)elf + *(QWORD *)(str_sh + 0x18);
                BYTE *sym = elf + *(QWORD *)(sh + 0x18);
                QWORD n = *(QWORD *)(sh + 0x20) / 24, k;
                for (k = 0; k < n; k++, sym += 24) {
                    const char *name = strtab + *(DWORD *)sym; QWORD val = *(QWORD *)(sym + 8);
                    if (!val || !*(WORD *)(sym + 6)) continue;
                    if (!scmp(name, "dlopen")) p_dlopen = (void *)(base + val);
                    else if (!scmp(name, "dlsym")) p_dlsym = (void *)(base + val);
                    else if (!scmp(name, "dlclose")) p_dlclose = (void *)(base + val);
                    else if (!scmp(name, "dlerror")) p_dlerror = (void *)(base + val);
                    else if (!scmp(name, "getpid")) p_getpid = (void *)(base + val);
                }
            }
        }
    }
}

/* ---- Exporte (Windows-ARM64-ABI, für P/Invoke) ---- */
__declspec(dllexport) void *dlopen(const char *file, int mode)
{
    resolve_glibc();
    if (!p_dlopen) return 0;
    return (void *)call_linux(p_dlopen, (QWORD)file, (QWORD)(unsigned)mode, 0, 0, 0, 0);
}

__declspec(dllexport) void *dlsym(void *handle, const char *name)
{
    void *fn;
    resolve_glibc();
    if (!p_dlsym || !handle) return 0;
    fn = (void *)call_linux(p_dlsym, (QWORD)handle, (QWORD)name, 0, 0, 0, 0);
    return make_thunk(fn);    /* Aufrufer erhält eine x18-sichere Sprungstelle */
}

__declspec(dllexport) int dlclose(void *handle)
{
    resolve_glibc();
    if (!p_dlclose || !handle) return -1;
    return (int)call_linux(p_dlclose, (QWORD)handle, 0, 0, 0, 0, 0);
}

__declspec(dllexport) char *dlerror(void)
{
    resolve_glibc();
    return p_dlerror ? (char *)call_linux(p_dlerror, 0, 0, 0, 0, 0, 0) : 0;
}

/* Diagnose: liefert Zustand der glibc-Suche */
__declspec(dllexport) void dlbridge_info(QWORD *out, char *path)
{
    QWORD i;
    resolve_glibc();
    out[0] = dbg_maps_size; out[1] = dbg_base; out[2] = dbg_elf_size;
    out[3] = (QWORD)p_dlopen; out[4] = (QWORD)p_dlsym; out[5] = (QWORD)p_dlerror;
    for (i = 0; dbg_path[i]; i++) path[i] = dbg_path[i]; path[i] = 0;
}

__declspec(dllexport) long long dlbridge_getpid(void)
{
    resolve_glibc();
    return p_getpid ? (long long)call_linux(p_getpid, 0, 0, 0, 0, 0, 0) : -1;
}

BOOL DllMain(HANDLE inst, DWORD reason, void *reserved) { return 1; }

/* Minimaler ARM64-mscoree.dll-Shim für .NET Framework 4.8.1 unter Wine.
 * _CorExeMain/_CorDllMain müssen echte Funktionen sein (Wine's Loader löst für IL-only
 * Images keine Export-Forwarder auf). Alle übrigen Exporte werden per .def an
 * mscoreei.dll weitergeleitet. Keine CRT, nur kernel32-Importe. */
typedef unsigned short WCHAR;
typedef void *HMODULE;
typedef int BOOL;
typedef unsigned int DWORD;
typedef void *(*FARPROC)(void);

__declspec(dllimport) HMODULE LoadLibraryW(const WCHAR *);
__declspec(dllimport) FARPROC GetProcAddress(HMODULE, const char *);
__declspec(dllimport) unsigned int GetWindowsDirectoryW(WCHAR *, unsigned int);

static HMODULE mscoreei;

static HMODULE get_mscoreei(void)
{
    static const WCHAR tail[] = L"\\Microsoft.NET\\FrameworkArm64\\v4.0.30319\\mscoreei.dll";
    WCHAR path[520];
    unsigned int n, i;

    if (mscoreei) return mscoreei;
    n = GetWindowsDirectoryW(path, 260);
    if (n && n < 200)
    {
        for (i = 0; tail[i]; i++) path[n + i] = tail[i];
        path[n + i] = 0;
        mscoreei = LoadLibraryW(path);
    }
    if (!mscoreei) mscoreei = LoadLibraryW(L"mscoreei.dll");
    return mscoreei;
}

int shim_CorExeMain(void)
{
    int (*f)(void) = (int (*)(void))GetProcAddress(get_mscoreei(), "_CorExeMain");
    return f ? f() : -1;
}

BOOL shim_CorDllMain(HMODULE inst, DWORD reason, void *reserved)
{
    BOOL (*f)(HMODULE, DWORD, void *) =
        (BOOL (*)(HMODULE, DWORD, void *))GetProcAddress(get_mscoreei(), "_CorDllMain");
    return f ? f(inst, reason, reserved) : 0;
}

/* DllMain: wie das Original reicht mscoree.dll alle DllMain-Aufrufe an
 * mscoreei!OnShimDllMainCalled weiter (initialisiert u. a. die globale Shim-Instanz). */
BOOL ShimDllMain(HMODULE inst, DWORD reason, void *reserved)
{
    BOOL (*f)(HMODULE, DWORD, void *);
    HMODULE ei = (reason == 1 /* DLL_PROCESS_ATTACH */) ? get_mscoreei() : mscoreei;
    if (!ei) return reason != 1;
    f = (BOOL (*)(HMODULE, DWORD, void *))GetProcAddress(ei, "OnShimDllMainCalled");
    return f ? f(inst, reason, reserved) : 1;
}

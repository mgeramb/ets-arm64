// Installiert Assemblies über die Fusion-API (IAssemblyCache) von .NET 4.8.1 ARM64 in den GAC.
// Aufruf: gacinstall.exe <listfile>   (eine Windows-Pfadangabe pro Zeile)
using System; using System.IO; using System.Runtime.InteropServices;
class GacInstall {
  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("e707dcde-d1cd-11d2-bab9-00c04f8eceae")]
  interface IAssemblyCache {
    [PreserveSig] int UninstallAssembly();
    [PreserveSig] int QueryAssemblyInfo();
    [PreserveSig] int CreateAssemblyCacheItem();
    [PreserveSig] int CreateAssemblyScavenger();
    [PreserveSig] int InstallAssembly(uint flags, [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr refData);
  }
  delegate int CreateAssemblyCacheFn(out IAssemblyCache cache, int reserved);
  [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryW(string p);
  [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr m, string n);
  static int Main(string[] a) {
    string fusion = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "fusion.dll");
    IntPtr h = LoadLibraryW(fusion);
    if (h == IntPtr.Zero) { Console.WriteLine("fusion.dll nicht ladbar: " + fusion); return 2; }
    var create = (CreateAssemblyCacheFn)Marshal.GetDelegateForFunctionPointer(GetProcAddress(h, "CreateAssemblyCache"), typeof(CreateAssemblyCacheFn));
    IAssemblyCache cache; int hr = create(out cache, 0);
    if (hr != 0) { Console.WriteLine("CreateAssemblyCache hr=0x" + hr.ToString("x8")); return 3; }
    int fail = 0;
    foreach (var line in File.ReadAllLines(a[0])) {
      var p = line.Trim(); if (p.Length == 0) continue;
      hr = cache.InstallAssembly(2 /* FORCE_REFRESH */, p, IntPtr.Zero);
      Console.WriteLine((hr == 0 ? "OK   " : "FAIL ") + "0x" + hr.ToString("x8") + "  " + p);
      if (hr != 0) fail++;
    }
    return fail == 0 ? 0 : 1;
  }
}

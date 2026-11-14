// Test für Wine-Bug 60062 / Patch 0006: Installer.OpenDatabase mit numerischem OpenMode (wie ETS bei
// KNX-Secure-Passwörtern). Ohne Patch wird z. B. 0 zum Ausgabepfad "0" (Kopie, read/write statt read-only).
// Prüfung über den Trace: WINEDEBUG=trace+msi wine msiopen.exe 2>&1 | grep 'MSI_OpenDatabaseW'
//   erwartet: (null) bzw. 0x1/0x2  –  Fehler: L"0", L"1", L"2"
using System; using System.IO; using System.Reflection; using System.Runtime.InteropServices;
class T {
  [DllImport("msi.dll", CharSet = CharSet.Unicode)] static extern uint MsiOpenDatabase(string path, IntPtr persist, out IntPtr h);
  [DllImport("msi.dll")] static extern uint MsiDatabaseCommit(IntPtr h);
  [DllImport("msi.dll")] static extern uint MsiCloseHandle(IntPtr h);
  static object Call(object o, string m, params object[] a) { return o.GetType().InvokeMember(m, BindingFlags.InvokeMethod, null, o, a); }
  static int Main() {
    string p = Path.Combine(Path.GetTempPath(), "msiopen-test.msi"); if (File.Exists(p)) File.Delete(p);
    IntPtr h; uint r = MsiOpenDatabase(p, (IntPtr)3 /*MSIDBOPEN_CREATE*/, out h);
    if (r != 0) { Console.WriteLine("Testdatenbank anlegen fehlgeschlagen: " + r); return 99; }
    MsiDatabaseCommit(h); MsiCloseHandle(h);
    var inst = Activator.CreateInstance(Type.GetTypeFromProgID("WindowsInstaller.Installer"));
    int fails = 0;
    foreach (int mode in new[] { 0 /*ReadOnly*/, 1 /*Transact*/, 2 /*Direct*/ }) {
      try { var db = Call(inst, "OpenDatabase", p, mode); Marshal.FinalReleaseComObject(db); GC.Collect(); GC.WaitForPendingFinalizers();
            Console.WriteLine("OpenDatabase(mode " + mode + "): OK"); }
      catch (Exception ex) { fails++; Console.WriteLine("OpenDatabase(mode " + mode + "): FEHLER " + (ex.InnerException ?? ex).Message); }
    }
    return fails;
  } }

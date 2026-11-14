// ARM64-Ersatz für den x86-Launcher ETS6.exe (ETS-Dateien bleiben unverändert).
// Hält den Launcher-Mutex und startet ETS6N.exe (normaler Modus, AnyCPU -> nativ ARM64).
using System; using System.Diagnostics; using System.IO; using System.Linq; using System.Threading;
class Launcher {
  const string MutexName = @"Global\KNXETS6_54E11E05-D8E6-45F2-9B09-DE462C4DBA3F";
  static int Main(string[] args) {
    string dir = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : @"C:\Program Files (x86)\ETS6";
    var rest = args.Length > 0 && Directory.Exists(args[0]) ? args.Skip(1) : args;
    bool created;
    using (var m = new Mutex(true, MutexName, out created)) {
      Console.WriteLine("Mutex " + MutexName + " created=" + created);
      string exe = Path.Combine(dir, "ETS6N.exe");
      string argl = string.Join(" ", rest.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a));
      // ARM64-Host (ETS6N.exe im Launcher-Ordner) startet ETS in-process, siehe etshost.cs
      string host = Environment.GetEnvironmentVariable("ETS_DEBUG_HOST");
      if (string.IsNullOrEmpty(host)) host = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ETS6N.exe");
      if (File.Exists(host)) { argl = "\"" + exe + "\" " + argl; exe = host; }
      var psi = new ProcessStartInfo(exe, argl) {
        WorkingDirectory = dir, UseShellExecute = false };
      var p = Process.Start(psi);
      Console.WriteLine("ETS6N.exe gestartet, PID " + p.Id);
      p.WaitForExit();
      Console.WriteLine("ETS6N.exe beendet, Exitcode " + p.ExitCode);
      return p.ExitCode;
    }
  }
}

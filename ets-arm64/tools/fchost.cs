// Debug-Host: führt ETS6N.exe im selben Prozess aus und protokolliert alle First-Chance-Exceptions.
using System; using System.IO; using System.Linq;
class H {
  [STAThread] static int Main(string[] a) {
    var log = new StreamWriter(@"C:\ets-arm64\firstchance.log", false) { AutoFlush = true };
    int n = 0;
    AppDomain.CurrentDomain.FirstChanceException += (s, e) => {
      if (n++ > 400) return;
      try { log.WriteLine("=== FC#" + n + " " + e.Exception.GetType().FullName + ": " + e.Exception.Message);
            log.WriteLine(Environment.StackTrace.Split('\n').Skip(3).Take(14).Aggregate("", (x, y) => x + y + "\n")); } catch { }
    };
    AppDomain.CurrentDomain.UnhandledException += (s, e) => { log.WriteLine("UNHANDLED " + e.ExceptionObject); };
    return AppDomain.CurrentDomain.ExecuteAssembly(a[0], a.Skip(1).ToArray());
  }
}

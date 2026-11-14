// Testet den DevKit-Unix-Lader in verschiedenen Konstellationen
using System; using System.IO; using System.Linq; using System.Reflection;
public class Runner : MarshalByRefObject {
  public string Pre;
  public void SetPre(string p) { Pre = p; }
  public string Run(string dll, bool arg) {
    var asm = Assembly.LoadFrom(dll); var bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    if (Pre == "gettypes") { try { asm.GetTypes(); } catch (ReflectionTypeLoadException) { } }
    if (Pre == "cloudkit") { var ck = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(dll), "Knx.CloudKit.dll")); ck.GetType("Knx.CloudKit.Secure.NativeManager", true).GetFields(bf); }
    if (Pre == "cloudkitflag") { var ck = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(dll), "Knx.CloudKit.dll")); var nm = ck.GetType("Knx.CloudKit.Secure.NativeManager", true); nm.GetField("M3NUPZSbc8f", bf).SetValue(null, true); }
    var t = asm.GetType("PcBX1noIdVoFytDjHc5M.YKcQgdoItcEBB6V75EE4+p1bOM5o9iEsDrObTuqfu", true);
    var load = t.GetMethod("lIKo9mu7CQi", bf);
    try { return "Load(" + arg + ") -> " + load.Invoke(null, new object[] { arg }) + " in Domain '" + AppDomain.CurrentDomain.FriendlyName + "' base=" + AppDomain.CurrentDomain.BaseDirectory + " cwd=" + Environment.CurrentDirectory; }
    catch (Exception ex) { return "Exception " + (ex.InnerException ?? ex).Message; }
  } }
class M { [STAThread] static void Main(string[] a) {
  string dll = a[0], dir = Path.GetDirectoryName(dll); string mode = a[1];
  if (mode == "default") Console.WriteLine(new Runner().Run(dll, false));
  else {
    var setup = new AppDomainSetup { ApplicationBase = dir, ConfigurationFile = Path.Combine(dir, "ETS6N.exe.config") }; if (Environment.GetEnvironmentVariable("APPNAME") != null) setup.ApplicationName = Environment.GetEnvironmentVariable("APPNAME");
    var d = AppDomain.CreateDomain("X", null, setup);
    var r = (Runner)d.CreateInstanceFromAndUnwrap(typeof(M).Assembly.Location, typeof(Runner).FullName);
    if (mode == "domain-cwd") Directory.SetCurrentDirectory(dir);
    if (mode == "domain-ztmp") Directory.SetCurrentDirectory(@"Z:\tmp");
    if (a.Length > 2) r.SetPre(a[2]);
    if (mode == "cwd-ets-then-ztmp") { Directory.SetCurrentDirectory(dir); Directory.SetCurrentDirectory(@"Z:\tmp"); }
    Console.WriteLine(r.Run(dll, false));
  } } }

// Listet Typen mit statischen Methoden Load/IsLoaded etc.: findtypes.exe <asm>
using System; using System.IO; using System.Linq; using System.Reflection;
class F { static void Main(string[] a) {
  string dir = Path.GetDirectoryName(Path.GetFullPath(a[0]));
  AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
  Type[] ts; try { ts = Assembly.LoadFrom(a[0]).GetTypes(); } catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(t => t != null).ToArray(); }
  var bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  foreach (var t in ts) {
    var names = t.GetMethods(bf).Select(m => m.Name).Concat(t.GetProperties(bf).Select(p => p.Name)).ToArray();
    if (names.Any(n => n == "Load" || n == "IsLoaded" || n.Contains("Native") || n == "get_IsLoaded"))
      Console.WriteLine(t.FullName + " : " + string.Join(",", names.Distinct().Where(n => !n.StartsWith("add_") && !n.StartsWith("remove_")).Take(20)));
  } } }

// Listet alle P/Invokes (DLL!Einsprung) aller Typen einer Assembly: pinv.exe <asm>
using System; using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices;
class P { static void Main(string[] a) {
  string dir = Path.GetDirectoryName(Path.GetFullPath(a[0]));
  AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
  Type[] ts; try { ts = Assembly.LoadFrom(a[0]).GetTypes(); } catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(t => t != null).ToArray(); }
  var bf = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  foreach (var t in ts) foreach (var m in t.GetMethods(bf)) {
    if ((m.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
    var di = (DllImportAttribute)Attribute.GetCustomAttribute(m, typeof(DllImportAttribute));
    Console.WriteLine((di != null ? di.Value + "!" + (di.EntryPoint ?? m.Name) : "?") + "  <- " + t.FullName + "::" + m.Name);
  }
} }

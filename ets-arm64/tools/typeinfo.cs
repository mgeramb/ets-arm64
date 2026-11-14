// Zeigt Felder/Methoden eines Typs: typeinfo.exe <asm> <Typ>
using System; using System.IO; using System.Linq; using System.Reflection;
class T { static void Main(string[] a) {
  string dir = Path.GetDirectoryName(Path.GetFullPath(a[0]));
  AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
  var t = Assembly.LoadFrom(a[0]).GetType(a[1], true); var bf = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  foreach (var f in t.GetFields(bf)) Console.WriteLine("F " + (f.IsStatic ? "static " : "") + f.FieldType.FullName + " " + f.Name);
  foreach (var p in t.GetProperties(bf)) Console.WriteLine("P " + p.PropertyType.FullName + " " + p.Name);
  foreach (var m in t.GetMethods(bf)) Console.WriteLine("M " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(",", m.GetParameters().Select(x => x.ParameterType.Name)) + ")" + (m.GetMethodBody() != null ? " try/catch=" + m.GetMethodBody().ExceptionHandlingClauses.Count : ""));
} }

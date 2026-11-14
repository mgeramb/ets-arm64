// Ruft den Unix-Lader von Knx.DevKit auf (entschlüsselt + schreibt die native Lib) und zeigt P/Invoke-Ziele.
using System; using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices;
class U { static void Main(string[] a) {
  string dir = Path.GetDirectoryName(a[0]);
  AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
  var asm = Assembly.LoadFrom(a[0]); var t = asm.GetType(a[1], true);
  var bf = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
  foreach (var m in t.GetMethods(bf).Where(x => (x.Attributes & MethodAttributes.PinvokeImpl) != 0)) {
    var d = m.GetCustomAttributesData().FirstOrDefault(c => c.AttributeType == typeof(DllImportAttribute));
    var di = (DllImportAttribute)Attribute.GetCustomAttribute(m, typeof(DllImportAttribute));
    Console.WriteLine("PInvoke " + m.Name + " -> " + (di != null ? di.Value + "!" + di.EntryPoint : "?") + " (" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + ")");
  }
  var f = t.GetFields(bf).FirstOrDefault(x => x.FieldType.IsGenericType && x.FieldType.GetGenericArguments()[0] == typeof(string));
  if (a.Length > 3) {
    var load = t.GetMethod(a[2], bf);
    try { Console.WriteLine("Load -> " + load.Invoke(null, new object[] { false })); } catch (Exception ex) { Console.WriteLine("Load: " + (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message); }
  }
  foreach (var fld in t.GetFields(bf)) {
    var v = fld.GetValue(null);
    var dict = v as System.Collections.IDictionary;
    if (dict != null && fld.FieldType.GetGenericArguments()[0] == typeof(string)) foreach (System.Collections.DictionaryEntry de in dict) {
      var ty = (Type)de.Value; var inv = ty.GetMethod("Invoke");
      Console.WriteLine("EXPORT " + de.Key + " : " + inv.ReturnType.Name + "(" + string.Join(", ", inv.GetParameters().Select(p => p.ParameterType.Name)) + ")");
    }
  }
} }

// Prüft, ob Assemblies per vollem Namen aus dem GAC geladen werden: gacload.exe <dll>...
using System; using System.Reflection;
class T { static int Main(string[] a) { int f = 0;
  foreach (var p in a) { var n = AssemblyName.GetAssemblyName(p);
    try { var asm = Assembly.Load(n.FullName); Console.WriteLine("OK  " + n.Name + "  GAC=" + asm.GlobalAssemblyCache + "  " + asm.Location); }
    catch (Exception ex) { f++; Console.WriteLine("ERR " + n.Name + ": " + ex.GetType().Name + " " + ex.Message); } }
  return f; } }

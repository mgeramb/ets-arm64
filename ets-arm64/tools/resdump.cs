// Listet eingebettete Ressourcen einer Assembly und prüft PE-Architektur: resdump.exe <asm> [outdir]
using System; using System.IO; using System.Reflection;
class R { static void Main(string[] a) {
  var asm = Assembly.LoadFrom(a[0]);
  foreach (var n in asm.GetManifestResourceNames()) {
    using (var s = asm.GetManifestResourceStream(n)) {
      var b = new byte[s.Length]; s.Read(b, 0, b.Length); string kind = "";
      if (b.Length > 0x40 && b[0] == 'M' && b[1] == 'Z') { int pe = BitConverter.ToInt32(b, 0x3c); if (pe + 6 < b.Length) kind = "PE machine 0x" + BitConverter.ToUInt16(b, pe + 4).ToString("x4"); }
      Console.WriteLine(n + "  " + b.Length + " bytes  " + kind);
      if (a.Length > 1) File.WriteAllBytes(Path.Combine(a[1], n), b);
    } } } }

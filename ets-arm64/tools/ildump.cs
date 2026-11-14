// Dumpt IL einer Methode mit aufgelösten Tokens: ildump.exe <assembly> <Typ> <Methode>
using System; using System.IO; using System.Linq; using System.Reflection; using System.Reflection.Emit; using System.Collections.Generic;
class D {
  static Dictionary<short, OpCode> ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
  static void Main(string[] a) {
    string dir = Path.GetDirectoryName(Path.GetFullPath(a[0]));
    AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
    var asm = Assembly.LoadFrom(a[0]); var t = asm.GetType(a[1], true);
    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(x => x.Name == a[2])) {
      Console.WriteLine("== " + m); var il = m.GetMethodBody().GetILAsByteArray(); var mod = m.Module; int i = 0;
      while (i < il.Length) {
        int at = i; OpCode op; short v = il[i++]; if (v == 0xFE) v = (short)(0xFE00 | il[i++]); op = ops[v]; string arg = "";
        switch (op.OperandType) {
          case OperandType.InlineNone: break;
          case OperandType.ShortInlineBrTarget: arg = "IL_" + (i + 1 + (sbyte)il[i]).ToString("x4"); i += 1; break;
          case OperandType.ShortInlineI: case OperandType.ShortInlineVar: arg = il[i].ToString(); i += 1; break;
          case OperandType.InlineVar: arg = BitConverter.ToInt16(il, i).ToString(); i += 2; break;
          case OperandType.InlineBrTarget: arg = "IL_" + (i + 4 + BitConverter.ToInt32(il, i)).ToString("x4"); i += 4; break;
          case OperandType.InlineI: arg = BitConverter.ToInt32(il, i).ToString(); i += 4; break;
          case OperandType.InlineI8: arg = BitConverter.ToInt64(il, i).ToString(); i += 8; break;
          case OperandType.ShortInlineR: i += 4; break; case OperandType.InlineR: i += 8; break;
          case OperandType.InlineString: arg = "\"" + mod.ResolveString(BitConverter.ToInt32(il, i)) + "\""; i += 4; break;
          case OperandType.InlineSwitch: int n = BitConverter.ToInt32(il, i); i += 4 + 4 * n; arg = "switch(" + n + ")"; break;
          default: int tok = BitConverter.ToInt32(il, i); i += 4;
            try { var mm = mod.ResolveMember(tok); arg = (mm.DeclaringType != null ? mm.DeclaringType.FullName + "::" : "") + mm.Name; } catch (Exception ex) { arg = "tok 0x" + tok.ToString("x8") + " (" + ex.GetType().Name + ")"; } break;
        }
        Console.WriteLine("  IL_" + at.ToString("x4") + ": " + op.Name + " " + arg);
      }
    }
  }
}

// Sucht in einer Assembly alle Methoden, die ein bestimmtes Member aufrufen: ilscan.exe <asm> <Typ::Name>
using System; using System.IO; using System.Linq; using System.Reflection; using System.Reflection.Emit; using System.Collections.Generic;
class S {
  static Dictionary<short, OpCode> ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
  static void Main(string[] a) {
    string dir = Path.GetDirectoryName(Path.GetFullPath(a[0]));
    AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
    var asm = Assembly.LoadFrom(a[0]); Type[] types;
    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
    foreach (var t in types) foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))) {
      MethodBody b; try { b = m.GetMethodBody(); } catch { continue; } if (b == null) continue;
      var il = b.GetILAsByteArray(); int i = 0;
      while (i < il.Length) {
        short v = il[i++]; if (v == 0xFE) v = (short)(0xFE00 | il[i++]); OpCode op; if (!ops.TryGetValue(v, out op)) break;
        int sz = op.OperandType == OperandType.InlineNone ? 0 : op.OperandType == OperandType.ShortInlineBrTarget || op.OperandType == OperandType.ShortInlineI || op.OperandType == OperandType.ShortInlineVar ? 1 : op.OperandType == OperandType.InlineVar ? 2 : op.OperandType == OperandType.InlineI8 || op.OperandType == OperandType.InlineR ? 8 : op.OperandType == OperandType.InlineSwitch ? 4 + 4 * BitConverter.ToInt32(il, i) : 4;
        if (op.OperandType == OperandType.InlineField && a[1].StartsWith("F::")) { try { var ff = m.Module.ResolveField(BitConverter.ToInt32(il, i)); if (ff.Name == a[1].Substring(3)) Console.WriteLine(op.Name + " " + ff.Name + " <- " + t.FullName + "::" + m.Name + "  @IL_" + (i - 1).ToString("x4")); } catch { } }
        if (op.OperandType == OperandType.InlineMethod) { try { var mm = m.Module.ResolveMethod(BitConverter.ToInt32(il, i)); var key = mm.DeclaringType.Name + "::" + mm.Name; if (key == a[1] || (a[1].StartsWith("*::") && mm.Name == a[1].Substring(3))) Console.WriteLine(key + " <- " + t.FullName + "::" + m.Name + "  @IL_" + (i - 1).ToString("x4")); } catch { } }
        i += sz;
      }
    }
  }
}

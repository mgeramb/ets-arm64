using System; using System.Linq; using System.Runtime.InteropServices; using System.Text;
class I { [DllImport("libdl.so")] static extern void dlbridge_info(long[] o, StringBuilder p);
  static void Main() { var o = new long[6]; var sb = new StringBuilder(512); dlbridge_info(o, sb);
    Console.WriteLine("maps=" + o[0] + " base=0x" + o[1].ToString("x") + " elf=" + o[2] + " dlopen=0x" + o[3].ToString("x") + " path=" + sb);
    foreach (var l in System.IO.File.ReadAllText(@"Z:\proc\self\maps").Split('\n').Where(l => l.Contains("libc.so") || l.Contains("ld-linux"))) Console.WriteLine("  " + l); } }

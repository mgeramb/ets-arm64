using System; using System.Runtime.InteropServices;
class D {
  [DllImport("libdl.so")] static extern IntPtr dlopen(string f, int m);
  [DllImport("libdl.so")] static extern IntPtr dlsym(IntPtr h, string n);
  [DllImport("libdl.so")] static extern IntPtr dlerror();
  delegate int IntFn();
  static void Main(string[] a) {
    var h = dlopen(a.Length > 0 ? a[0] : "/tmp/DevKit.so", 2);
    Console.WriteLine("dlopen -> 0x" + h.ToString("x") + "  dlerror=" + Marshal.PtrToStringAnsi(dlerror()));
    if (h == IntPtr.Zero) return;
    foreach (var n in new[] { "pPwMEKnRQskM", "ebSWwVfoaRHq", "suzrCvfSVnLw", "KUZHkOwgodRm" }) {
      var p = dlsym(h, n); Console.Write(n + " @0x" + p.ToString("x"));
      if (p != IntPtr.Zero) Console.Write(" -> " + ((IntFn)Marshal.GetDelegateForFunctionPointer(p, typeof(IntFn)))());
      Console.WriteLine();
    }
  } }

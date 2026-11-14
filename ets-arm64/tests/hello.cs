using System;
using System.Runtime.InteropServices;
class P {
  static int Main() {
    Console.WriteLine("Hallo aus .NET " + Environment.Version + " | " + RuntimeInformation.FrameworkDescription);
    Console.WriteLine("ProcessArchitecture: " + RuntimeInformation.ProcessArchitecture + ", 64bit=" + Environment.Is64BitProcess);
    Console.WriteLine("Runtime dir: " + RuntimeEnvironment.GetRuntimeDirectory());
    var x = new System.Collections.Generic.Dictionary<string,int>(); for (int i=0;i<100000;i++) x["k"+i]=i;
    Console.WriteLine("Dictionary ok: " + x.Count);
    return 0;
  }
}

using System; using System.Runtime.InteropServices;
class P { [DllImport("libdl.so")] static extern long dlbridge_getpid(); static void Main() { Console.WriteLine("getpid via Bruecke = " + dlbridge_getpid()); } }

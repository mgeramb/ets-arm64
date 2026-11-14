using System; using System.Threading; using System.Collections.Generic; using System.Diagnostics;
class G { static volatile bool stop;
  static void Main() {
    var sw = Stopwatch.StartNew(); var ts = new List<Thread>();
    for (int i = 0; i < 4; i++) { var t = new Thread(() => { var l = new List<byte[]>(); while (!stop) { l.Add(new byte[1000]); if (l.Count > 5000) l.Clear(); } }); t.Start(); ts.Add(t); }
    var w = new Thread(() => { var e = new ManualResetEvent(false); e.WaitOne(); }) { IsBackground = true }; w.Start();   // Thread in nativem Wait
    var sl = new Thread(() => { while (!stop) Thread.Sleep(1); }) { IsBackground = true }; sl.Start();
    for (int i = 0; i < 20; i++) { GC.Collect(); Console.WriteLine("GC " + i + " ok nach " + sw.ElapsedMilliseconds + " ms, Gen2=" + GC.CollectionCount(2)); Console.Out.Flush(); Thread.Sleep(100); }
    stop = true; foreach (var t in ts) t.Join(); Console.WriteLine("fertig");
  } }

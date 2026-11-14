using System; using System.Windows; using System.Windows.Controls; using System.Diagnostics;
class C {
  static void Deep(int n) { if (n == 0) throw new InvalidOperationException("im Callback"); Deep(n - 1); }
  [STAThread] static void Main() {
    var app = new Application(); var w = new Window { Width = 300, Height = 100, Content = new TextBlock { Text = "x" } };
    w.Loaded += (s, e) => {
      var sw = Stopwatch.StartNew();
      for (int i = 0; i < 5; i++) { try { Deep(5); } catch (InvalidOperationException) { } Console.WriteLine("Ausnahme " + i + " gefangen nach " + sw.ElapsedMilliseconds + " ms"); Console.Out.Flush(); }
      try { Type.GetType("Gibt.Es.Nicht, NichtDa", true); } catch (Exception ex) { Console.WriteLine("TypeLoad/FileNotFound gefangen: " + ex.GetType().Name); }
      app.Shutdown(); };
    app.Run(w); Console.WriteLine("ende ok"); } }

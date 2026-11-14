using System; using System.Windows; using System.Windows.Controls; using System.Windows.Media;
class W {
  static void Step(string s) { Console.WriteLine("STEP " + s); Console.Out.Flush(); }
  [STAThread] static int Main() {
    AppDomain.CurrentDomain.UnhandledException += (s, e) => { Console.WriteLine("UNHANDLED: " + e.ExceptionObject); Console.Out.Flush(); };
    try {
      Step("1 load PresentationCore"); var t1 = typeof(System.Windows.Media.Visual);
      Step("2 Dispatcher"); var d = System.Windows.Threading.Dispatcher.CurrentDispatcher;
      Step("3 RenderCapability tier=" + (RenderCapability.Tier >> 16));
      Step("4 new Application"); var app = new Application();
      Step("5 new Window"); var win = new Window { Title = "WPF ARM64 Test", Width = 400, Height = 200 };
      Step("6 content"); win.Content = new TextBlock { Text = "WPF laeuft auf ARM64-Wine", FontSize = 20 };
      win.Loaded += (s, e) => { Step("8 loaded"); var tm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; tm.Tick += (a, b) => app.Shutdown(); tm.Start(); };
      Step("7 app.Run"); app.Run(win);
      Step("9 exit ok"); return 0;
    } catch (Exception ex) { Console.WriteLine("CAUGHT: " + ex); return 1; }
  }
}

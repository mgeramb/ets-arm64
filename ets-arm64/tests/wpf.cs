using System; using System.Windows; using System.Windows.Controls;
class W { [STAThread] static void Main() {
  var app = new Application();
  var win = new Window { Title = "WPF ARM64 Test", Width = 400, Height = 200,
    Content = new TextBlock { Text = "WPF laeuft auf ARM64-Wine", FontSize = 20, Margin = new Thickness(20) } };
  win.Loaded += (s, e) => { Console.WriteLine("WPF window loaded, RenderCapability tier=" + (System.Windows.Media.RenderCapability.Tier >> 16));
                            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                            t.Tick += (a, b) => app.Shutdown(); t.Start(); };
  app.Run(win); Console.WriteLine("WPF exit ok"); } }

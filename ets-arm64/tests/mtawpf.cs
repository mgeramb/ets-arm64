using System; class P { [MTAThread] static void Main() { Console.WriteLine("MTA start"); Console.Out.Flush();
  var t = typeof(System.Windows.Media.Visual); Console.WriteLine("PresentationCore geladen: " + t.Assembly.FullName);
  var a = typeof(System.Windows.Window); Console.WriteLine("PresentationFramework geladen"); } }

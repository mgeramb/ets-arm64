using System; using System.Net; using System.Net.NetworkInformation;
class N { static void Main(string[] a) {
  Console.WriteLine("SecurityProtocol=" + ServicePointManager.SecurityProtocol);
  foreach (var p in new[] { (SecurityProtocolType)0, SecurityProtocolType.Tls12, (SecurityProtocolType)12288 /*Tls13*/ }) {
    try { ServicePointManager.SecurityProtocol = p; var s = new WebClient().DownloadString("https://update.knx.org/data/XML/project-23/knx_master.xml");
          Console.WriteLine("HTTPS " + p + ": OK " + s.Length + " Zeichen"); }
    catch (Exception ex) { Console.WriteLine("HTTPS " + p + ": " + ex.GetType().Name + " " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : "")); } }
  try { var r = new Ping().Send("update.knx.org", 3000); Console.WriteLine("Ping: " + r.Status + " " + r.RoundtripTime + " ms"); } catch (Exception ex) { Console.WriteLine("Ping: " + ex.Message); }
} }

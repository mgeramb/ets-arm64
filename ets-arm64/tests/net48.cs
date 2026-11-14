using System; using System.Net;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class N { static void Main() { Console.WriteLine("SecurityProtocol=" + ServicePointManager.SecurityProtocol);
  try { var s = new WebClient().DownloadString("https://update.knx.org/data/XML/project-23/knx_master.xml"); Console.WriteLine("HTTPS default (wie ETS): OK " + s.Length); }
  catch (Exception ex) { Console.WriteLine("HTTPS default: " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : "")); } } }

using System; class P { [STAThread] static void Main() { Console.WriteLine("STA ok, apt=" + System.Threading.Thread.CurrentThread.GetApartmentState()); } }

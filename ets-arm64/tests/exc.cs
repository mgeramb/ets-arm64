using System;
class P {
  static void Thrower(int d) { if (d == 0) throw new InvalidOperationException("tief"); Thrower(d - 1); }
  static int Main() {
    try { throw new Exception("einfach"); } catch (Exception e) { Console.WriteLine("1 gefangen: " + e.Message); }
    try { Thrower(10); } catch (InvalidOperationException e) { Console.WriteLine("2 gefangen: " + e.Message); }
    try { object o = null; o.ToString(); } catch (NullReferenceException) { Console.WriteLine("3 NullReference gefangen (HW-Exception)"); }
    try { int z = 0; Console.WriteLine(1 / z); } catch (DivideByZeroException) { Console.WriteLine("4 DivideByZero gefangen"); }
    try { Type.GetType("Gibt.Es.Nicht", true); } catch (TypeLoadException) { Console.WriteLine("5 TypeLoad gefangen"); }
    Console.WriteLine("EH ok"); return 0;
  }
}

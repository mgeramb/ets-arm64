using System; using System.Diagnostics;
class A { static void Main() {
  var sw = Stopwatch.StartNew();
  try { System.Reflection.Assembly.Load("NichtDa"); } catch (Exception ex) { Console.WriteLine("1 Assembly.Load: " + ex.GetType().Name + " nach " + sw.ElapsedMilliseconds + " ms"); }
  Console.Out.Flush();
  try { Type.GetType("Gibt.Es.Nicht, NichtDa", true); } catch (Exception ex) { Console.WriteLine("2 GetType mit Assembly: " + ex.GetType().Name + " nach " + sw.ElapsedMilliseconds + " ms"); }
  Console.Out.Flush();
  try { Type.GetType("System.GibtEsNicht, mscorlib", true); } catch (Exception ex) { Console.WriteLine("3 TypeLoad in mscorlib: " + ex.GetType().Name + " nach " + sw.ElapsedMilliseconds + " ms"); }
  Console.WriteLine("ende ok"); } }

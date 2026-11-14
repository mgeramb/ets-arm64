using System; using System.Linq; using System.Windows; using System.Windows.Controls; using System.Windows.Media;
class F {
  static void Try(string font, string text) {
    Console.Write("[" + font + "] " + string.Join(" ", text.Select(c => ((int)c).ToString("X4"))) + " -> "); Console.Out.Flush();
    var tb = new TextBlock { Text = text, FontFamily = new FontFamily(font) }; tb.Measure(new Size(500, 100));
    Console.WriteLine("ok " + tb.DesiredSize); }
  [STAThread] static void Main() {
    Console.WriteLine("System fonts: " + Fonts.SystemFontFamilies.Count + " -> " + string.Join(", ", Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(x => x).Take(40)));
    var gtf = new Typeface("Segoe UI"); GlyphTypeface g; Console.WriteLine("Segoe UI GlyphTypeface: " + (gtf.TryGetGlyphTypeface(out g) ? g.FontUri.ToString() : "NICHT gefunden"));
    Try("Segoe UI", "Hallo Welt ÄÖÜß");
    Try("Arial", "Test");
    Try("Segoe MDL2 Assets", "\uE106\uE921");
    Try("Segoe UI", "\uE106");
    Try("Segoe UI", "\u2026\u2192\u25B6");
    Try("Segoe UI", "\u4E2D\u6587");
    Console.WriteLine("fertig");
  }
}

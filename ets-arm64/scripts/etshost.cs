// ETS6-ARM64-Host: führt ETS6N.exe im eigenen (nativen ARM64-)Prozess aus.
// Die native Sicherheitsbibliothek von Knx.DevKit (USB-Lizenz-Dongle) existiert für Windows nur
// als x86/x64-Code und kann in einem ARM64-Prozess nicht laufen. Der Host markiert sie deshalb vor
// dem Start als „Laden fehlgeschlagen“, sodass ETS ohne Dongle-Unterstützung weiterläuft
// (Cloud-Lizenz funktioniert). ETS-Dateien werden nicht verändert.
// Aufruf (durch den ARM64-Launcher ETS6.exe): etshost.exe <Pfad zu ETS6N.exe> [Argumente...]
using System; using System.IO; using System.Linq; using System.Reflection; using System.Reflection.Emit;

class EtsHost {
  static void Log(string s) {
    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "ets-arm64-host.log"), DateTime.Now.ToString("s") + " " + s + Environment.NewLine); } catch { }
  }

  // Findet in Load() das erste „ldc.i4.1; stsfld <static bool>“ = Flag „Laden fehlgeschlagen“.
  static FieldInfo FindFailedFlag(Type nm) {
    var load = nm.GetMethod("Load", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
    var il = load.GetMethodBody().GetILAsByteArray();
    for (int i = 0; i + 5 < il.Length; i++) {
      if (il[i] == 0x17 /* ldc.i4.1 */ && il[i + 1] == 0x80 /* stsfld */) {
        var f = nm.Module.ResolveField(BitConverter.ToInt32(il, i + 2));
        if (f.IsStatic && f.FieldType == typeof(bool) && f.DeclaringType == nm) return f;
      }
    }
    return null;
  }

  // KNX-Bibliotheken mit eingebetteter nativer x86/x64-Bibliothek (kein Windows-ARM64-Build vorhanden)
  static readonly string[] NativeLibAssemblies = { "Knx.CloudKit" };   // Knx.DevKit: siehe EnableDevKitArm64

  // Rückfall: DevKit ebenfalls deaktivieren (ETS läuft dann ohne Dongle-Unterstützung)
  static void NativeLibAssembliesFallback(string etsDir) { DisableNativeLibs(etsDir, new[] { "Knx.DevKit" }); }

  static void DisableNativeLibs(string etsDir) { DisableNativeLibs(etsDir, NativeLibAssemblies); }

  static void DisableNativeLibs(string etsDir, string[] names) {
    foreach (var name in names) {
      try {
        var asm = Assembly.LoadFrom(Path.Combine(etsDir, name + ".dll"));
        var nm = asm.GetType(name + ".Secure.NativeManager", true);
        var flag = FindFailedFlag(nm);
        if (flag == null) { Log(name + ": Flag nicht gefunden – keine Änderung"); continue; }
        flag.SetValue(null, true);
        Log(name + ": native Bibliothek deaktiviert (" + flag.Name + "=true)");
      } catch (Exception ex) { Log(name + ": " + ex.Message); }
    }
  }

  // Knx.DevKit (Lizenz-Dongle): statt der x64-Windows-Bibliothek die mitgelieferte ARM64-Linux-Bibliothek
  // verwenden. Der Unix-Lader von DevKit lädt sie über libdl.so (ARM64-Brücke zu glibc); seine Delegaten
  // werden in den Windows-Lader übernommen, den ETS unter Windows benutzt.
  static bool EnableDevKitArm64(string etsDir) {
    try {
      var asm = Assembly.LoadFrom(Path.Combine(etsDir, "Knx.DevKit.dll"));
      var bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
      Type unixLoader = null, winLoader = null;
      foreach (var t in asm.GetTypes()) {
        foreach (var m in t.GetMethods(bf)) {
          if ((m.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
          var di = (System.Runtime.InteropServices.DllImportAttribute)Attribute.GetCustomAttribute(m, typeof(System.Runtime.InteropServices.DllImportAttribute));
          if (di == null) continue;
          if (di.Value == "libdl.so" && di.EntryPoint == "dlopen") unixLoader = t;
          if (di.Value.StartsWith("kernel32", StringComparison.OrdinalIgnoreCase) && t.DeclaringType != null && t.GetFields(bf).Any(f => f.FieldType == typeof(System.Collections.Generic.Dictionary<Type, Delegate>))) winLoader = t;
        }
      }
      if (unixLoader == null || winLoader == null) { Log("DevKit-ARM64: Lader nicht gefunden (unix=" + unixLoader + ", win=" + winLoader + ")"); return false; }
      var load = unixLoader.GetMethods(bf).First(m => m.ReturnType == typeof(bool) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool) && m.GetMethodBody() != null && m.GetMethodBody().ExceptionHandlingClauses.Count > 0);
      // Der Unix-Lader schreibt nach "/tmp/DevKit.so" und lädt diesen Pfad per dlopen. .NET legt den Pfad auf
      // dem aktuellen Laufwerk ab -> Arbeitsverzeichnis kurz auf Laufwerk Z: (= Linux-Wurzel) stellen.
      string oldCwd = Environment.CurrentDirectory; bool ok;
      EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> fc =
        (o, e) => Log("DevKit-ARM64 (intern): " + e.Exception.GetType().Name + ": " + e.Exception.Message);
      AppDomain.CurrentDomain.FirstChanceException += fc;
      try { Environment.CurrentDirectory = @"Z:\tmp"; ok = (bool)load.Invoke(null, new object[] { false }); }
      finally { Environment.CurrentDirectory = oldCwd; AppDomain.CurrentDomain.FirstChanceException -= fc; }
      if (!ok) { Log("DevKit-ARM64: Unix-Lader meldet Fehler (" + unixLoader.FullName + "::" + load.Name + ", win=" + winLoader.FullName + ", cwd=" + Environment.CurrentDirectory + ")"); return false; }
      var dictType = typeof(System.Collections.Generic.Dictionary<Type, Delegate>);
      var src = (System.Collections.Generic.Dictionary<Type, Delegate>)unixLoader.GetFields(bf).First(f => f.FieldType == dictType).GetValue(null);
      var dst = (System.Collections.Generic.Dictionary<Type, Delegate>)winLoader.GetFields(bf).First(f => f.FieldType == dictType).GetValue(null);
      lock (dst) foreach (var kv in src) dst[kv.Key] = kv.Value;
      // Windows- und Unix-Pfad benutzen je eigene Delegattypen für dieselbe Funktion. Zuordnung über den
      // Exportnamen (Dictionary<string,Type> beider Lader bzw. deren Methode Type X(string)); der Windows-
      // Delegat wird auf denselben Funktionszeiger (libdl-Thunk) gelegt.
      var nameDictType = typeof(System.Collections.Generic.Dictionary<string, Type>);
      var unixNames = (System.Collections.Generic.Dictionary<string, Type>)unixLoader.GetFields(bf).First(f => f.FieldType == nameDictType).GetValue(null);
      var winNames = (System.Collections.Generic.Dictionary<string, Type>)winLoader.GetFields(bf).First(f => f.FieldType == nameDictType).GetValue(null);
      var winTypeOf = winLoader.GetMethods(bf).FirstOrDefault(m => m.ReturnType == typeof(Type) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
      int mapped = 0, missing = 0;
      foreach (var kv in unixNames) {
        Delegate ud; if (kv.Value == null || !src.TryGetValue(kv.Value, out ud)) continue;
        Type wt = null;
        if (winNames != null) winNames.TryGetValue(kv.Key, out wt);
        if (wt == null && winTypeOf != null) try { wt = (Type)winTypeOf.Invoke(null, new object[] { kv.Key }); } catch { }
        if (wt == null) { missing++; Log("DevKit-ARM64: kein Windows-Delegat für " + kv.Key); continue; }
        try {
          var fp = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(ud);
          lock (dst) dst[wt] = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(fp, wt);
          if (winNames != null) winNames[kv.Key] = wt;
          mapped++;
        } catch (Exception ex) { missing++; Log("DevKit-ARM64: " + kv.Key + ": " + ex.Message); }
      }
      Log("DevKit-ARM64: " + mapped + " Funktionen auf Windows-Delegattypen abgebildet, " + missing + " fehlen (Namen unix=" + unixNames.Count + ", win=" + (winNames == null ? -1 : winNames.Count) + ")");
      winLoader.GetFields(bf).First(f => f.FieldType == typeof(bool)).SetValue(null, true);   // „geladen“
      // Diagnose: DevKit-Ereignisse protokollieren (erst NACH dem Laden, sonst stört die NativeManager-Initialisierung)
      if (Environment.GetEnvironmentVariable("ETS_ARM64_DEVKIT_EVENTS") == "1") try {
        var nmType = asm.GetType("Knx.DevKit.Secure.NativeManager");
        foreach (var ev in nmType.GetEvents(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
          var h = Delegate.CreateDelegate(ev.EventHandlerType, typeof(EtsHost).GetMethod("OnDevKitEvent", BindingFlags.Static | BindingFlags.NonPublic));
          ev.AddEventHandler(null, h);
        }
      } catch (Exception ex) { Log("DevKit-Events: " + ex.Message); }
      Log("DevKit-ARM64: native ARM64-Bibliothek aktiv (" + src.Count + " Funktionen, " + unixLoader.Name + " -> " + winLoader.Name + ")");
      return true;
    } catch (Exception ex) { Log("DevKit-ARM64: " + ex); return false; }
  }

  static void OnDevKitEvent(object sender, EventArgs e) {
    var props = e.GetType().GetProperties().Select(pp => { try { return pp.Name + "=" + pp.GetValue(e); } catch { return pp.Name + "=?"; } });
    Log("DevKit-Ereignis " + e.GetType().Name + ": " + string.Join("; ", props));
  }

  // läuft in der ETS-AppDomain
  public class Prep : MarshalByRefObject {
    public void Run(string dir) {
      DisableNativeLibs(dir);
      if (!EnableDevKitArm64(dir)) { NativeLibAssembliesFallback(dir); }
      // Diagnose: Stack des Hauptthreads periodisch protokollieren (ETS_ARM64_WATCHDOG=Sekunden)
      int wd; if (int.TryParse(Environment.GetEnvironmentVariable("ETS_ARM64_WATCHDOG"), out wd) && wd > 0) {
        var main = System.Threading.Thread.CurrentThread;
        var t = new System.Threading.Thread(() => {
          for (;;) {
            System.Threading.Thread.Sleep(wd * 1000);
            try {
#pragma warning disable 618
              Log("Watchdog: suspend"); main.Suspend(); Log("Watchdog: suspended"); System.Diagnostics.StackTrace st;
              try { st = new System.Diagnostics.StackTrace(main, false); } finally { main.Resume(); }
#pragma warning restore 618
              Log("Watchdog Hauptthread:" + Environment.NewLine + st);
            } catch (Exception ex) { Log("Watchdog: " + ex.Message); }
          } }) { IsBackground = true };
        t.Start();
      }
      // Diagnose: native Fehler (SEH) mit Stack ins Host-Log schreiben
      bool fcAll = Environment.GetEnvironmentVariable("ETS_ARM64_FC_ALL") == "1";
      AppDomain.CurrentDomain.FirstChanceException += (s, e) => {
        if (fcAll) { var st = Environment.StackTrace; if (st.Contains("Knx.DevKit") || st.Contains("Licensing")) Log("FC " + e.Exception.GetType().FullName + ": " + e.Exception.Message + Environment.NewLine + st); return; }
        if (e.Exception is System.Runtime.InteropServices.SEHException || e.Exception is AccessViolationException || e.Exception is BadImageFormatException || e.Exception is DllNotFoundException)
          Log("FirstChance " + e.Exception.GetType().Name + ": " + e.Exception.Message + Environment.NewLine + Environment.StackTrace);
      };
    }
  }

  [STAThread]
  static int Main(string[] args) {
    string ets = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\ETS6\ETS6N.exe";
    string dir = Path.GetDirectoryName(ets);
    Log("Start " + ets + " (" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ")");
    var setup = new AppDomainSetup { ApplicationBase = dir, ConfigurationFile = ets + ".config", ApplicationName = "ETS6N" };
    var domain = AppDomain.CreateDomain("ETS6N", null, setup);
    var prep = (Prep)domain.CreateInstanceFromAndUnwrap(typeof(EtsHost).Assembly.Location, typeof(Prep).FullName);
    prep.Run(dir);
    return domain.ExecuteAssembly(ets, args.Skip(1).ToArray());
  }
}

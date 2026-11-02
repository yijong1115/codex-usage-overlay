using System;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace CodexUsageOverlay.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CaseAttribute : Attribute
    {
        public string Suite;
        public CaseAttribute(string suite) { Suite = suite; }
    }
    public static class Assert
    {
        public static void True(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Equal<T>(T expected, T actual) { if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
        public static void Throws(Action action) { bool threw = false; try { action(); } catch { threw = true; } True(threw, "Expected rejection"); }
        public static IDictionary<string, object> Json(string json) { return (IDictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json); }
    }
    public static class TestRunner
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string suite = args.Length > 0 ? args[0] : "";
            if (suite == "--probe") { NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)); Console.WriteLine(new JavaScriptSerializer().Serialize(WindowTracker.Probe())); return 0; }
            if (suite == "--render-card") return VisualQA.RenderCard(args.Length > 1 ? args[1] : "Light");
            if (suite == "--render-settings") return VisualQA.RenderSettings(args.Length > 1 ? args[1] : "Image", args.Length > 2 ? args[2] : "Light");
            if (suite == "--render-details") return VisualQA.RenderDetails(args.Length > 1 ? args[1] : "Light");
            if (suite == "--probe-overlay") return OverlayNativeProbe.Run();
            if (suite == "--test-hide") return OverlayNativeProbe.TestForegroundHide();
            if (suite == "--fake-server") return FakeServerEntry(args);
            int total = 0, failed = 0;
            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                var a = (CaseAttribute)m.GetCustomAttributes(typeof(CaseAttribute), false).FirstOrDefault();
                if (a == null || (suite.Length > 0 && a.Suite != suite)) continue;
                total++;
                try { m.Invoke(null, null); Console.WriteLine("PASS " + m.Name); }
                catch (Exception e) { failed++; Console.WriteLine("FAIL " + m.Name + ": " + (e.InnerException ?? e).Message); }
            }
            Console.WriteLine(total + " tests, " + failed + " failures");
            return total == 0 || failed > 0 ? 1 : 0;
        }
        static int FakeServerEntry(string[] args)
        {
            Type t = Assembly.GetExecutingAssembly().GetType("CodexUsageOverlay.Tests.FakeServer");
            return t == null ? 2 : (int)t.GetMethod("Run").Invoke(null, new object[] { args });
        }
    }
}

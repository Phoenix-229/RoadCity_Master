#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> One entry point that runs every gate in the tool and reports a single verdict, so a topic-boundary
    /// verification is ONE batchmode launch instead of one per gate (each gate's own RunBatch calls
    /// EditorApplication.Exit, so only one of them can run per launch). Gates are DISCOVERED by reflection rather than
    /// listed, because a hand-maintained list of 40-odd names is exactly the kind of duplication that silently stops
    /// including new gates. Discovery is sorted by full type name, so the run order and the report are deterministic
    /// (C-06). A gate that throws is counted as a failure and named, never swallowed. Report lines are prefixed
    /// RCMSUITE. </summary>
    public static class RcmGateSuite
    {
        private const string GateMethodName = "Run";

        [MenuItem("Window/Road City Master/Testing/RUN ALL GATES (topic-boundary verification)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.RcmGateSuite.RunBatch. Exits 1 if any gate failed or threw. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSUITE BEGIN\n");

            List<MethodInfo> gates = Discover();
            int failedGates = 0;
            int threwGates = 0;

            // A headless run names each gate in the console BEFORE invoking it. The report below is written in one
            // block at the very end, so a gate that never RETURNS produces no report at all -- and a suite of ninety
            // gates that stalls behind an empty log cannot be diagnosed except by running it again and waiting again.
            // The breadcrumb makes the log's last line name the gate that did not come back, which turns a lost cycle
            // into a fixable one. It is written to the console and deliberately NOT into the report, so the report
            // stays byte-identical between two runs (C-06), and it is suppressed outside batchmode because ninety
            // extra console lines would bury the verdict an author opened the menu item to read.
            bool headless = Application.isBatchMode;
            if (headless)
            {
                Debug.Log("RCMSUITE discovered " + gates.Count + " gates");
            }

            for (int i = 0; i < gates.Count; i++)
            {
                MethodInfo m = gates[i];
                string typeName = m.DeclaringType == null ? "<unknown>" : m.DeclaringType.FullName;
                object[] args = new object[] { null };
                bool ok;
                if (headless)
                {
                    Debug.Log("RCMSUITE enter  " + (i + 1) + "/" + gates.Count + "  " + typeName);
                }
                try
                {
                    ok = (bool)m.Invoke(null, args);
                }
                catch (Exception e)
                {
                    threwGates++;
                    Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    builder.Append("RCMSUITE THREW  ").Append(typeName).Append(": ")
                           .Append(inner.GetType().Name).Append(": ").Append(inner.Message).Append("\n");
                    continue;
                }

                string gateReport = args[0] as string;
                if (!ok) { failedGates++; }
                builder.Append("RCMSUITE ").Append(ok ? "PASS " : "FAIL ").Append(typeName).Append("\n");
                if (!ok && !string.IsNullOrEmpty(gateReport))
                {
                    builder.Append(gateReport).Append("\n");
                }
            }

            bool passed = failedGates == 0 && threwGates == 0 && gates.Count > 0;
            if (gates.Count == 0)
            {
                builder.Append("RCMSUITE FAIL   no gates were discovered -- the suite found nothing to run\n");
            }
            builder.Append("RCMSUITE result ").Append(passed ? "PASS" : "FAIL")
                   .Append("  gates ").Append(gates.Count)
                   .Append("  failed ").Append(failedGates)
                   .Append("  threw ").Append(threwGates).Append("\n");
            builder.Append("RCMSUITE END");

            report = builder.ToString();
            return passed;
        }

        /// <summary> Every public static bool Run(out string) in the tool's own assemblies, except this one. Sorted by
        /// full type name so the order never depends on assembly load order. </summary>
        private static List<MethodInfo> Discover()
        {
            List<MethodInfo> found = new List<MethodInfo>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                string name = assemblies[a].GetName().Name;
                if (name != "RoadCityMaster" && name != "RoadCityMaster.Editor") { continue; }

                Type[] types;
                try { types = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null || type == typeof(RcmGateSuite)) { continue; }
                    MethodInfo m = type.GetMethod(GateMethodName, BindingFlags.Public | BindingFlags.Static,
                        null, new Type[] { typeof(string).MakeByRefType() }, null);
                    if (m == null || m.ReturnType != typeof(bool)) { continue; }
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 1 || !ps[0].IsOut) { continue; }
                    found.Add(m);
                }
            }

            found.Sort(delegate (MethodInfo x, MethodInfo y)
            {
                string xn = x.DeclaringType == null ? "" : x.DeclaringType.FullName;
                string yn = y.DeclaringType == null ? "" : y.DeclaringType.FullName;
                return string.CompareOrdinal(xn, yn);
            });
            return found;
        }
    }
}
#endif

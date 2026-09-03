#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-52's gate: proves the status model without a scene. It asserts that with no provider the mode line
    /// says so and names what is unavailable; that with a provider it names the provider; that the panel is never
    /// empty; that a stale result is never presented as current (no build => "No build recorded yet", a build =>
    /// stamped with its time and seed); that every refusal is listed with its object and station and carries the id
    /// needed to select the object; and that the render is deterministic for the same inputs (C-06). The GUI panel and
    /// the jump-to-object click are the deferred editor half. Report lines are prefixed RCMSTATUS. </summary>
    public static class StatusPanelGate
    {
        [MenuItem("Window/Road City Master/Testing/Build status panel (mode, provider, refusals)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.StatusPanelGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMSTATUS BEGIN\n");
            List<string> failures = new List<string>();

            CaseStandaloneNamesUnavailable(builder, failures);
            CaseConnectedNamesProvider(builder, failures);
            CaseNeverEmpty(builder, failures);
            CaseNeverStale(builder, failures);
            CaseRefusalsCarryObjectAndStation(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSTATUS FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSTATUS result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSTATUS END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void CaseStandaloneNamesUnavailable(StringBuilder builder, List<string> failures)
        {
            BuildStatus s = new BuildStatus();  // defaults to Standalone
            string line = s.ModeLine();
            Expect(line.Contains("Standalone"), "standalone: the mode line did not say Standalone", failures);
            Expect(line.Contains("unavailable"), "standalone: the mode line did not name anything as unavailable", failures);
            Expect(line.Contains("tunnel"), "standalone: the mode line did not name the tunnel cavity as unavailable", failures);
            builder.Append("RCMSTATUS ").Append("standalone-line".PadRight(22))
                   .Append("no provider -> says so and names what is unavailable\n");
        }

        private static void CaseConnectedNamesProvider(StringBuilder builder, List<string> failures)
        {
            BuildStatus s = new BuildStatus { mode = TerrainProviderMode.Connected, providerName = "TerrainSculptor" };
            string line = s.ModeLine();
            Expect(line.Contains("TerrainSculptor"), "connected: the mode line did not name the provider", failures);
            Expect(line.Contains("available"), "connected: the mode line did not say the cuts are available", failures);
            builder.Append("RCMSTATUS ").Append("connected-line".PadRight(22))
                   .Append("provider connected -> names it\n");
        }

        private static void CaseNeverEmpty(StringBuilder builder, List<string> failures)
        {
            BuildStatus s = new BuildStatus();
            Expect(!s.IsEverEmpty(), "empty: the panel reported itself empty", failures);
            Expect(!string.IsNullOrEmpty(s.RenderText()), "empty: the rendered panel text was empty", failures);
            builder.Append("RCMSTATUS ").Append("never-empty".PadRight(22))
                   .Append("the panel always has at least the mode line\n");
        }

        private static void CaseNeverStale(StringBuilder builder, List<string> failures)
        {
            BuildStatus s = new BuildStatus();
            Expect(!s.HasStamp(), "stale: a fresh status claimed to carry a build stamp", failures);
            Expect(s.LastBuildLine().Contains("No build recorded"), "stale: no-build state was not stated plainly", failures);

            DateTime when = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            s.RecordBuild(when, 20260829UL, BuildOutcome.Success, null);
            Expect(s.HasStamp(), "stale: a recorded build did not carry a stamp", failures);
            string line = s.LastBuildLine();
            Expect(line.Contains("2026-08-29"), "stale: the last-build line did not carry its timestamp", failures);
            Expect(line.Contains("20260829"), "stale: the last-build line did not carry its seed", failures);
            builder.Append("RCMSTATUS ").Append("never-stale".PadRight(22))
                   .Append("result is stamped with time and seed, never shown stale\n");
        }

        private static void CaseRefusalsCarryObjectAndStation(StringBuilder builder, List<string> failures)
        {
            BuildStatus s = new BuildStatus();
            List<RefusalEntry> refusals = new List<RefusalEntry>
            {
                new RefusalEntry("road.7", "Ring Road", 142.5, "roundabout radius below the 50 km/h minimum"),
                new RefusalEntry("road.7", "Ring Road", 310.0, "superelevation above the authored maximum")
            };
            s.RecordBuild(new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc), 5UL, BuildOutcome.Success, refusals);
            Expect(s.lastOutcome == BuildOutcome.RefusedSome,
                "refusal: success with refusals was not promoted to RefusedSome", failures);
            Expect(s.refusals.Count == 2, "refusal: not all refusals were recorded", failures);
            Expect(s.refusals[0].objectId == "road.7", "refusal: the object id needed to select the object was lost", failures);
            string text = s.RenderText();
            Expect(text.Contains("Ring Road"), "refusal: the object name was not listed", failures);
            Expect(text.Contains("142.5") && text.Contains("310.0"), "refusal: a refusal's station was not listed", failures);
            Expect(text.Contains("roundabout radius"), "refusal: the reason was not listed", failures);
            builder.Append("RCMSTATUS ").Append("refusals-listed".PadRight(22))
                   .Append("each refusal names object, station and reason\n");
        }

        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            DateTime when = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            BuildStatus a = new BuildStatus(); a.RecordBuild(when, 9UL, BuildOutcome.Success, null);
            BuildStatus b = new BuildStatus(); b.RecordBuild(when, 9UL, BuildOutcome.Success, null);
            Expect(a.RenderText() == b.RenderText(), "determinism: two identical builds rendered differently", failures);
            builder.Append("RCMSTATUS ").Append("determinism".PadRight(22))
                   .Append("same inputs -> identical panel text\n");
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

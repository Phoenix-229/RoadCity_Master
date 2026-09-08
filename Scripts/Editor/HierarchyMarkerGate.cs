// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-53's gate: proves the hierarchy marker without a scene. It asserts that the three states resolve to
    /// three DISTINCT glyphs (a marker that reads the same in two states tells the author nothing); that the connected
    /// tooltip names the provider; that a linker entered whose provider is missing is its own state and says why cuts
    /// will not run instead of degrading silently into standalone; that a row DRAWS from the cache without resolving
    /// (the probe count must not move when a row is read, which is what keeps the hierarchy from stalling); that the
    /// marker never contradicts RM-52's panel; and that resolution is deterministic (C-06). The drawn glyph's placement
    /// is the deferred editor half. Report lines are prefixed RCMHIER. </summary>
    public static class HierarchyMarkerGate
    {
        [MenuItem("Window/Road City Master/Testing/Hierarchy connection marker (connected, standalone, broken linker)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.HierarchyMarkerGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMHIER BEGIN\n");
            List<string> failures = new List<string>();

            CaseThreeStatesAreDistinct(builder, failures);
            CaseConnectedNamesProvider(builder, failures);
            CaseBrokenLinkerIsItsOwnState(builder, failures);
            CaseRowReadsCacheWithoutProbing(builder, failures);
            CaseNeverContradictsPanel(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMHIER FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMHIER result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMHIER END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void CaseThreeStatesAreDistinct(StringBuilder builder, List<string> failures)
        {
            MarkerView standalone = HierarchyConnectionMarker.Resolve(false, false, null);
            MarkerView connected = HierarchyConnectionMarker.Resolve(true, true, "TerrainSculptor");
            MarkerView broken = HierarchyConnectionMarker.Resolve(true, false, "TerrainSculptor");

            Expect(standalone.state == ConnectionMarkerState.Standalone,
                "states: no linker did not resolve to Standalone", failures);
            Expect(connected.state == ConnectionMarkerState.Connected,
                "states: a resolved linker did not resolve to Connected", failures);
            Expect(broken.state == ConnectionMarkerState.LinkerBroken,
                "states: an unresolved linker did not resolve to LinkerBroken", failures);

            Expect(standalone.glyph != connected.glyph && connected.glyph != broken.glyph
                && standalone.glyph != broken.glyph, "states: two states share a glyph and cannot be told apart", failures);
            Expect(!string.IsNullOrEmpty(standalone.tooltip) && !string.IsNullOrEmpty(connected.tooltip)
                && !string.IsNullOrEmpty(broken.tooltip), "states: a state carried no tooltip", failures);

            builder.Append("RCMHIER ").Append("three-states".PadRight(22))
                   .Append("connected / standalone / broken linker -> three distinct glyphs\n");
        }

        private static void CaseConnectedNamesProvider(StringBuilder builder, List<string> failures)
        {
            MarkerView v = HierarchyConnectionMarker.Resolve(true, true, "TerrainSculptor");
            Expect(v.tooltip.Contains("TerrainSculptor"), "connected: the tooltip did not name the provider", failures);
            Expect(v.tooltip.Contains("available"), "connected: the tooltip did not say the cuts are available", failures);

            MarkerView unnamed = HierarchyConnectionMarker.Resolve(true, true, null);
            Expect(!string.IsNullOrEmpty(unnamed.tooltip) && unnamed.tooltip.Contains("provider"),
                "connected: a nameless provider produced a tooltip that names nothing", failures);

            builder.Append("RCMHIER ").Append("connected-names".PadRight(22))
                   .Append("connected tooltip names the provider it is connected to\n");
        }

        private static void CaseBrokenLinkerIsItsOwnState(StringBuilder builder, List<string> failures)
        {
            MarkerView broken = HierarchyConnectionMarker.Resolve(true, false, "TerrainSculptor");
            MarkerView standalone = HierarchyConnectionMarker.Resolve(false, false, null);

            Expect(broken.state != ConnectionMarkerState.Standalone,
                "broken: a missing provider degraded silently into standalone", failures);
            Expect(broken.state != ConnectionMarkerState.Connected,
                "broken: a missing provider was reported as connected", failures);
            Expect(broken.tooltip != standalone.tooltip,
                "broken: the broken tooltip is the standalone tooltip", failures);
            Expect(broken.tooltip.Contains("TerrainSculptor"),
                "broken: the tooltip did not name the provider that is missing", failures);
            Expect(broken.tooltip.Contains("missing"),
                "broken: the tooltip did not say the provider is missing", failures);
            Expect(broken.tooltip.Contains("will not run"),
                "broken: the tooltip did not say what stops working", failures);

            builder.Append("RCMHIER ").Append("broken-linker".PadRight(22))
                   .Append("linker entered, provider gone -> named, not silently standalone\n");
        }

        private static void CaseRowReadsCacheWithoutProbing(StringBuilder builder, List<string> failures)
        {
            EntityId row = EntityId.FromULong(4242);
            EntityId unknown = EntityId.FromULong(99999);
            HierarchyConnectionMarker.Clear();
            MarkerView pushed = HierarchyConnectionMarker.Resolve(true, true, "TerrainSculptor");
            int afterResolve = HierarchyConnectionMarker.ProbeCount;
            HierarchyConnectionMarker.Push(row, pushed);

            MarkerView got;
            bool hit = HierarchyConnectionMarker.TryGet(row, out got);
            Expect(hit, "cache: a pushed row could not be read back", failures);
            Expect(got.state == pushed.state && got.glyph == pushed.glyph,
                "cache: the row read back a different marker than was pushed", failures);

            for (int i = 0; i < 50; i++) { HierarchyConnectionMarker.TryGet(row, out got); }
            Expect(HierarchyConnectionMarker.ProbeCount == afterResolve,
                "cache: reading a row 50 times resolved again -- the hierarchy would probe per repaint", failures);

            MarkerView miss;
            Expect(!HierarchyConnectionMarker.TryGet(unknown, out miss),
                "cache: an unknown row reported a cached marker", failures);

            HierarchyConnectionMarker.Forget(row);
            Expect(!HierarchyConnectionMarker.TryGet(row, out miss),
                "cache: a forgotten row was still cached", failures);

            builder.Append("RCMHIER ").Append("cache-not-probe".PadRight(22))
                   .Append("50 row reads resolve zero times; push on change is the only resolve\n");
        }

        private static void CaseNeverContradictsPanel(StringBuilder builder, List<string> failures)
        {
            BuildStatus connectedPanel = new BuildStatus
            {
                mode = TerrainProviderMode.Connected,
                providerName = "TerrainSculptor"
            };
            BuildStatus standalonePanel = new BuildStatus();   // defaults to Standalone

            Expect(HierarchyConnectionMarker.AgreesWith(ConnectionMarkerState.Connected, connectedPanel.mode),
                "agreement: a connected marker disagreed with a connected panel", failures);
            Expect(!HierarchyConnectionMarker.AgreesWith(ConnectionMarkerState.Connected, standalonePanel.mode),
                "agreement: a connected marker was allowed beside a standalone panel", failures);
            Expect(HierarchyConnectionMarker.AgreesWith(ConnectionMarkerState.Standalone, standalonePanel.mode),
                "agreement: a standalone marker disagreed with a standalone panel", failures);
            Expect(HierarchyConnectionMarker.AgreesWith(ConnectionMarkerState.LinkerBroken, standalonePanel.mode),
                "agreement: a broken linker did not agree with the standalone panel it produces", failures);
            Expect(!HierarchyConnectionMarker.AgreesWith(ConnectionMarkerState.LinkerBroken, connectedPanel.mode),
                "agreement: a broken linker was allowed beside a connected panel", failures);

            builder.Append("RCMHIER ").Append("agrees-with-panel".PadRight(22))
                   .Append("marker and RM-52's status panel can never say opposite things\n");
        }

        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            MarkerView a = HierarchyConnectionMarker.Resolve(true, true, "TerrainSculptor");
            MarkerView b = HierarchyConnectionMarker.Resolve(true, true, "TerrainSculptor");
            Expect(a.glyph == b.glyph && a.tooltip == b.tooltip && a.state == b.state,
                "determinism: the same three facts resolved to two different markers", failures);
            builder.Append("RCMHIER ").Append("determinism".PadRight(22))
                   .Append("same inputs -> identical glyph and tooltip\n");
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

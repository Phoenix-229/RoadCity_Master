// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-46's gate: proves the footprint conform stage without a scene. It asserts that a cell under water
    /// produces no blocks; that a cell within a zone's shoreline setback of a water edge is removed and one beyond it
    /// survives; that a PORT keeps its water edge (zero setback -- the exception as DATA, not a code branch); that
    /// ground steeper than a zone's slope limit is removed, and that informal settlement genuinely keeps steeper
    /// ground a planned suburb loses (per-zone limit is data); that the removed area is reported as an absolute
    /// figure, a fraction, and a per-reason breakdown; and that the conform is bit-identical on repeat (C-06). The
    /// EXACT polygon clip against the coast (Q-11) and the highlighted-region overlay are the deferred half. Report
    /// lines are prefixed RCMCONFORM. </summary>
    public static class CityFootprintConformGate
    {
        [MenuItem("Window/Road City Master/Testing/City footprint conform (water and slope)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityFootprintConformGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMCONFORM report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCONFORM BEGIN\n");
            List<string> failures = new List<string>();

            CaseNoBlocksInWater(builder, failures);
            CaseShorelineSetbackRemoves(builder, failures);
            CasePortKeepsWaterEdge(builder, failures);
            CaseSteepSlopePerZone(builder, failures);
            CaseRemovedAreaReported(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCONFORM FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCONFORM result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCONFORM END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A cell under water produces no blocks. </summary>
        private static void CaseNoBlocksInWater(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass res = ZoneCatalog.Authored().Resolve("zone.residential.low");
            ConformCell water = Cell(res, true, 0.0, 0.0);
            Expect(FootprintConform.ClassifyCell(water) == ConformRemoval.Water,
                "water: a cell under water was not removed", failures);
            builder.Append("RCMCONFORM ").Append("no-water-blocks".PadRight(22))
                   .Append("a cell under water produces no blocks\n");
        }

        /// <summary> A cell within the shoreline setback is removed; one beyond it survives. </summary>
        private static void CaseShorelineSetbackRemoves(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass res = ZoneCatalog.Authored().Resolve("zone.residential.low"); // setback 15 m, slope 15
            ConformCell near = Cell(res, false, 8.0, 2.0);  // 8 m from water < 15 m setback
            ConformCell far = Cell(res, false, 40.0, 2.0);  // well back, gentle
            Expect(FootprintConform.ClassifyCell(near) == ConformRemoval.ShorelineSetback,
                "setback: a cell inside the shoreline setback was not removed", failures);
            Expect(FootprintConform.ClassifyCell(far) == ConformRemoval.Survives,
                "setback: a cell beyond the setback on gentle ground did not survive", failures);
            builder.Append("RCMCONFORM ").Append("shoreline-setback".PadRight(22))
                   .Append("a cell inside the setback is removed, beyond it survives\n");
        }

        /// <summary> A port keeps its water edge: a zero setback means a cell at the waterline survives. </summary>
        private static void CasePortKeepsWaterEdge(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass port = ZoneCatalog.Authored().Resolve("zone.port.logistics"); // setback 0
            ZoneTypeClass res = ZoneCatalog.Authored().Resolve("zone.residential.low");  // setback 15
            ConformCell portEdge = Cell(port, false, 1.0, 2.0); // 1 m from water, gentle
            ConformCell resEdge = Cell(res, false, 1.0, 2.0);
            Expect(FootprintConform.ClassifyCell(portEdge) == ConformRemoval.Survives,
                "port: the port did not keep its water edge (zero setback)", failures);
            Expect(FootprintConform.ClassifyCell(resEdge) == ConformRemoval.ShorelineSetback,
                "port: the residential edge cell was not removed (setback should apply)", failures);
            builder.Append("RCMCONFORM ").Append("port-water-edge".PadRight(22))
                   .Append("port keeps its water edge; setback is per-zone data\n");
        }

        /// <summary> Ground steeper than a zone's limit is removed, and the limit is per-zone: informal settlement
        /// keeps ground a planned suburb loses. </summary>
        private static void CaseSteepSlopePerZone(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass res = c.Resolve("zone.residential.low");        // slope limit 15
            ZoneTypeClass informal = c.Resolve("zone.informal.settlement"); // slope limit 30
            ConformCell resSteep = Cell(res, false, 100.0, 25.0);       // 25 deg > 15
            ConformCell informalSteep = Cell(informal, false, 100.0, 25.0); // 25 deg < 30
            Expect(FootprintConform.ClassifyCell(resSteep) == ConformRemoval.SteepSlope,
                "slope: a 25 deg cell was not removed for the planned suburb (limit 15)", failures);
            Expect(FootprintConform.ClassifyCell(informalSteep) == ConformRemoval.Survives,
                "slope: informal settlement did not keep steeper ground (limit 30, data not exception)", failures);
            builder.Append("RCMCONFORM ").Append("slope-per-zone".PadRight(22))
                   .Append("steep ground removed; informal keeps steeper ground\n");
        }

        /// <summary> The removed area is reported as an absolute figure, a fraction, and a per-reason breakdown. </summary>
        private static void CaseRemovedAreaReported(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass res = ZoneCatalog.Authored().Resolve("zone.residential.low"); // setback 15, slope 15
            double cellArea = 16.0;
            List<ConformCell> cells = new List<ConformCell>();
            // 4 survive (far, gentle)
            for (int i = 0; i < 4; i++) { cells.Add(Cell(res, false, 40.0, 2.0)); }
            // 3 water
            for (int i = 0; i < 3; i++) { cells.Add(Cell(res, true, 0.0, 0.0)); }
            // 2 setback
            for (int i = 0; i < 2; i++) { cells.Add(Cell(res, false, 5.0, 2.0)); }
            // 1 slope
            cells.Add(Cell(res, false, 40.0, 30.0));

            IList<ConformRemoval> perCell;
            ConformReport r = FootprintConform.Conform(cells, cellArea, out perCell);
            Expect(Math.Abs(r.paintedAreaM2 - 160.0) < 1e-9, "report: painted area was wrong", failures);
            Expect(Math.Abs(r.removedToWaterM2 - 48.0) < 1e-9, "report: water removal area was wrong", failures);
            Expect(Math.Abs(r.removedToSetbackM2 - 32.0) < 1e-9, "report: setback removal area was wrong", failures);
            Expect(Math.Abs(r.removedToSlopeM2 - 16.0) < 1e-9, "report: slope removal area was wrong", failures);
            Expect(Math.Abs(r.removedAreaM2 - 96.0) < 1e-9, "report: total removed area was wrong", failures);
            Expect(Math.Abs(r.removedFraction - 0.6) < 1e-9, "report: removed fraction was wrong", failures);
            Expect(Math.Abs(r.survivingAreaM2 - 64.0) < 1e-9, "report: surviving area was wrong", failures);
            Expect(perCell.Count == cells.Count, "report: the per-cell overlay count did not match the input", failures);
            builder.Append("RCMCONFORM ").Append("removed-area".PadRight(22))
                   .Append("removed area reported absolute, fraction, per-reason\n");
        }

        /// <summary> The conform is bit-identical on repeat -- pure logic (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass res = ZoneCatalog.Authored().Resolve("zone.residential.low");
            List<ConformCell> cells = new List<ConformCell>
            {
                Cell(res, false, 40.0, 2.0), Cell(res, true, 0.0, 0.0), Cell(res, false, 5.0, 2.0)
            };
            IList<ConformRemoval> p1, p2;
            ConformReport a = FootprintConform.Conform(cells, 16.0, out p1);
            ConformReport b = FootprintConform.Conform(cells, 16.0, out p2);
            bool same = a.removedAreaM2 == b.removedAreaM2 && a.removedFraction == b.removedFraction
                        && p1.Count == p2.Count;
            if (same) { for (int i = 0; i < p1.Count; i++) { if (p1[i] != p2[i]) { same = false; } } }
            Expect(same, "determinism: two identical conforms disagreed", failures);
            builder.Append("RCMCONFORM ").Append("determinism".PadRight(22))
                   .Append("identical footprint -> identical conform report\n");
        }

        /// <summary> Build a conform cell. </summary>
        private static ConformCell Cell(ZoneTypeClass zone, bool isWater, double distanceToWater, double slopeDegrees)
        {
            ConformCell c = new ConformCell();
            c.zone = zone;
            c.isWater = isWater;
            c.distanceToWaterMetres = distanceToWater;
            c.slopeDegrees = slopeDegrees;
            return c;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

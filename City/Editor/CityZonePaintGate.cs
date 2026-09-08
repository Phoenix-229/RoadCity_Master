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
    /// <summary> RM-45's gate: proves what the paint MEANS without a scene. It asserts that two zones over one cell
    /// resolve by DECLARED precedence and NOT by paint order (reversing the list keeps the same winner); that a
    /// precedence tie is REFUSED and reported rather than silently picked; that an unpainted cell resolves to null
    /// (not refused); that a lot takes its zone by MAJORITY over its cells; that a lot straddling a boundary within
    /// the stated fraction is refused and named; that intensity has a stated range and meaning (0 = sparsest,
    /// 1 = densest) for storeys and lot size; and that resolution is bit-identical on repeat (C-06). The scene sampler
    /// over real lot polygons and the painted-channel storage are the deferred half. Report lines are prefixed
    /// RCMPAINT. </summary>
    public static class CityZonePaintGate
    {
        [MenuItem("Window/Road City Master/Testing/City zone paint (precedence and majority)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityZonePaintGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMPAINT report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPAINT BEGIN\n");
            List<string> failures = new List<string>();

            CaseOverlapResolvesByPrecedence(builder, failures);
            CasePrecedenceTieRefused(builder, failures);
            CaseEmptyCellIsNull(builder, failures);
            CaseLotMajority(builder, failures);
            CaseLotStraddleRefused(builder, failures);
            CaseIntensityMeaning(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPAINT FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPAINT result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPAINT END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Two zones over one cell resolve by declared precedence, and reversing the list keeps the same
        /// winner -- so the result does not depend on paint order. </summary>
        private static void CaseOverlapResolvesByPrecedence(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass commercial = c.Resolve("zone.commercial.centre"); // precedence 40
            ZoneTypeClass residential = c.Resolve("zone.residential.low");  // precedence 10

            ZoneResolution a = ZonePaint.ResolveCell(new List<ZoneTypeClass> { residential, commercial });
            ZoneResolution b = ZonePaint.ResolveCell(new List<ZoneTypeClass> { commercial, residential });
            Expect(!a.refused && a.zoneId == "zone.commercial.centre",
                "precedence: the higher-precedence zone did not win", failures);
            Expect(!b.refused && b.zoneId == a.zoneId,
                "precedence: reversing the paint order changed the winner (order-dependent)", failures);
            builder.Append("RCMPAINT ").Append("precedence".PadRight(22))
                   .Append("higher precedence wins, independent of paint order\n");
        }

        /// <summary> Two zones sharing the top precedence refuse and name the tie rather than picking. </summary>
        private static void CasePrecedenceTieRefused(StringBuilder builder, List<string> failures)
        {
            ZoneTypeClass p = new ZoneTypeClass(); p.zoneId = "zone.a"; p.precedence = 30;
            ZoneTypeClass q = new ZoneTypeClass(); q.zoneId = "zone.b"; q.precedence = 30;
            ZoneResolution r = ZonePaint.ResolveCell(new List<ZoneTypeClass> { p, q });
            Expect(r.refused && r.zoneId == null, "tie: a precedence tie was not refused", failures);
            Expect(!string.IsNullOrEmpty(r.reason) && r.reason.Contains("precedence tie"),
                "tie: the refusal did not name the precedence tie", failures);
            builder.Append("RCMPAINT ").Append("tie-refused".PadRight(22))
                   .Append("a precedence tie is refused and named, not guessed\n");
        }

        /// <summary> An unpainted cell resolves to null (no settlement here), not a refusal. </summary>
        private static void CaseEmptyCellIsNull(StringBuilder builder, List<string> failures)
        {
            ZoneResolution r = ZonePaint.ResolveCell(new List<ZoneTypeClass>());
            Expect(!r.refused && r.zoneId == null, "empty: an unpainted cell was not resolved to null", failures);
            builder.Append("RCMPAINT ").Append("empty-cell".PadRight(22))
                   .Append("an unpainted cell is null, not a refusal\n");
        }

        /// <summary> A lot takes the zone covering the majority of its cells (not its centroid's zone). </summary>
        private static void CaseLotMajority(StringBuilder builder, List<string> failures)
        {
            // 7 commercial cells, 2 residential: majority commercial, well clear of the straddle fraction.
            List<string> cells = new List<string>
            {
                "zone.commercial.centre", "zone.commercial.centre", "zone.residential.low",
                "zone.commercial.centre", "zone.commercial.centre", "zone.commercial.centre",
                "zone.residential.low", "zone.commercial.centre", "zone.commercial.centre"
            };
            ZoneResolution r = ZonePaint.ResolveLotByMajority(cells, 0.15);
            Expect(!r.refused && r.zoneId == "zone.commercial.centre",
                "majority: the majority zone did not win the lot", failures);
            builder.Append("RCMPAINT ").Append("lot-majority".PadRight(22))
                   .Append("a lot takes its majority-area zone, not its centroid\n");
        }

        /// <summary> A lot straddling a boundary within the stated fraction is refused and named. </summary>
        private static void CaseLotStraddleRefused(StringBuilder builder, List<string> failures)
        {
            // 5 vs 4 -> lead share 0.56, second 0.44, difference 0.11 <= 0.15 straddle fraction: refuse.
            List<string> cells = new List<string>
            {
                "zone.commercial.centre", "zone.commercial.centre", "zone.residential.low",
                "zone.commercial.centre", "zone.residential.low", "zone.commercial.centre",
                "zone.residential.low", "zone.commercial.centre", "zone.residential.low"
            };
            ZoneResolution r = ZonePaint.ResolveLotByMajority(cells, 0.15);
            Expect(r.refused && r.zoneId == null, "straddle: a boundary-straddling lot was not refused", failures);
            Expect(!string.IsNullOrEmpty(r.reason) && r.reason.Contains("straddle"),
                "straddle: the refusal did not name the straddle", failures);
            builder.Append("RCMPAINT ").Append("lot-straddle".PadRight(22))
                   .Append("a boundary-straddling lot is refused and named\n");
        }

        /// <summary> Intensity has a stated range and meaning: 0 = sparsest, 1 = densest, for storeys and lot size. </summary>
        private static void CaseIntensityMeaning(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass commercial = c.Resolve("zone.commercial.centre"); // storeys 2..12, lots 150..2000

            Expect(ZonePaint.StoreyAtIntensity(commercial, 0.0) == 2, "intensity: 0 was not the min storey", failures);
            Expect(ZonePaint.StoreyAtIntensity(commercial, 1.0) == 12, "intensity: 1 was not the max storey", failures);
            Expect(ZonePaint.StoreyAtIntensity(commercial, 0.5) == 7, "intensity: 0.5 was not the mid storey", failures);
            // Clamp beyond the range.
            Expect(ZonePaint.StoreyAtIntensity(commercial, 2.0) == 12 && ZonePaint.StoreyAtIntensity(commercial, -1.0) == 2,
                "intensity: out-of-range intensity did not clamp", failures);
            // Lot size maps inversely: denser (1) -> smallest lot, sparsest (0) -> largest lot.
            Expect(Math.Abs(ZonePaint.LotSizeAtIntensityM2(commercial, 1.0) - 150.0) < 1e-6
                   && Math.Abs(ZonePaint.LotSizeAtIntensityM2(commercial, 0.0) - 2000.0) < 1e-6,
                "intensity: lot size did not map inversely to intensity", failures);
            builder.Append("RCMPAINT ").Append("intensity".PadRight(22))
                   .Append("0 = sparsest, 1 = densest, one stated meaning\n");
        }

        /// <summary> Resolution is bit-identical on repeat -- pure logic, stable tallies (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            List<ZoneTypeClass> set = new List<ZoneTypeClass>
            {
                c.Resolve("zone.residential.low"), c.Resolve("zone.commercial.centre"), c.Resolve("zone.industrial.general")
            };
            ZoneResolution a = ZonePaint.ResolveCell(set);
            ZoneResolution b = ZonePaint.ResolveCell(set);
            Expect(a.zoneId == b.zoneId && a.refused == b.refused,
                "determinism: two identical resolutions disagreed", failures);
            builder.Append("RCMPAINT ").Append("determinism".PadRight(22))
                   .Append("identical paint -> identical resolution\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

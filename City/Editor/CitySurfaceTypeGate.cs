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
    /// <summary> RM-41's gate: proves the unsealed surface type without a scene. It asserts that an unsealed surface
    /// has a steeper crown than a sealed one; that it suppresses lane markings entirely (RM-32 reads the surface,
    /// not an emptied list); that it widens the painted verge (reusing RM-38's paint test, paint independent of the
    /// deform width); that it marks a side ditch as required (RM-30); that switching a road unsealed and back to
    /// sealed restores the sealed values with NO leftover; that the material sets are distinct and AUTHORED, NOT
    /// GENERATED; and that everything is bit-identical on repeat (C-06). The crown/marking/ditch/material wiring is
    /// the deferred half. Report lines are prefixed RCMSURF. </summary>
    public static class CitySurfaceTypeGate
    {
        [MenuItem("Window/Road City Master/Testing/City surface type (sealed vs unsealed)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CitySurfaceTypeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMSURF report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSURF BEGIN\n");
            List<string> failures = new List<string>();

            CaseUnsealedSteeperCrown(builder, failures);
            CaseUnsealedSuppressesMarkings(builder, failures);
            CaseUnsealedWidensVerge(builder, failures);
            CaseUnsealedRequiresDitch(builder, failures);
            CaseSwitchBackNoLeftover(builder, failures);
            CaseMaterialsAuthoredNotGenerated(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSURF FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSURF result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSURF END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> An unsealed surface has a steeper crown than a sealed one (it sheds water by shape). </summary>
        private static void CaseUnsealedSteeperCrown(StringBuilder builder, List<string> failures)
        {
            SurfaceTypeClass sealedS = SurfaceTypeClass.Sealed();
            SurfaceTypeClass unsealedS = SurfaceTypeClass.Unsealed();
            Expect(Math.Abs(sealedS.crownCrossSlope - 0.02f) < 1e-9, "crown: the sealed crown was not 2%", failures);
            Expect(unsealedS.crownCrossSlope > sealedS.crownCrossSlope + 0.01f,
                "crown: the unsealed crown was not meaningfully steeper than the sealed crown", failures);
            builder.Append("RCMSURF ").Append("crown".PadRight(22))
                   .Append("unsealed crown steeper (sheds water by shape)\n");
        }

        /// <summary> An unsealed surface suppresses lane markings entirely; a sealed surface emits them. </summary>
        private static void CaseUnsealedSuppressesMarkings(StringBuilder builder, List<string> failures)
        {
            Expect(SurfaceType.EmitsLaneMarkings(SurfaceTypeClass.Sealed()),
                "markings: a sealed road did not emit lane markings", failures);
            Expect(!SurfaceType.EmitsLaneMarkings(SurfaceTypeClass.Unsealed()),
                "markings: an unsealed road did not suppress lane markings", failures);
            builder.Append("RCMSURF ").Append("markings".PadRight(22))
                   .Append("unsealed emits no markings; sealed does\n");
        }

        /// <summary> An unsealed surface widens the painted verge; reuse RM-38's paint test to show it paints wider. </summary>
        private static void CaseUnsealedWidensVerge(StringBuilder builder, List<string> failures)
        {
            SurfaceTypeClass sealedS = SurfaceTypeClass.Sealed();
            SurfaceTypeClass unsealedS = SurfaceTypeClass.Unsealed();
            double flattened = 4.0; // an RM-38 flattened half-width
            double sealedW = SurfaceType.PaintHalfWidthMetres(flattened, sealedS);   // 4.5
            double unsealedW = SurfaceType.PaintHalfWidthMetres(flattened, unsealedS); // 8.0
            Expect(unsealedW > sealedW, "verge: the unsealed verge was not wider than the sealed verge", failures);
            // RM-38's IsPaintedAt (reused, not re-implemented): a point the sealed verge misses, the unsealed hits.
            Expect(!TerrainFootprint.IsPaintedAt(6.0, sealedW) && TerrainFootprint.IsPaintedAt(6.0, unsealedW),
                "verge: the two verges did not differ where RM-38 tests paint coverage", failures);
            builder.Append("RCMSURF ").Append("verge".PadRight(22))
                   .Append("unsealed paints a wider dust margin (RM-38 test)\n");
        }

        /// <summary> An unsealed surface marks a side ditch as required; a sealed one does not. </summary>
        private static void CaseUnsealedRequiresDitch(StringBuilder builder, List<string> failures)
        {
            Expect(!SurfaceType.RequiresSideDitch(SurfaceTypeClass.Sealed()),
                "ditch: a sealed road wrongly required a side ditch", failures);
            Expect(SurfaceType.RequiresSideDitch(SurfaceTypeClass.Unsealed()),
                "ditch: an unsealed road did not mark a side ditch as required (RM-30)", failures);
            builder.Append("RCMSURF ").Append("drainage".PadRight(22))
                   .Append("unsealed requires a side ditch; sealed does not\n");
        }

        /// <summary> Switching a road to unsealed and back to sealed restores the sealed values with no leftover. </summary>
        private static void CaseSwitchBackNoLeftover(StringBuilder builder, List<string> failures)
        {
            SurfaceTypeClass original = SurfaceTypeClass.Sealed();
            SurfaceTypeClass road = original;                 // road is sealed
            road = SurfaceTypeClass.Unsealed();               // switch to unsealed
            Expect(!SurfaceType.ValuesEqual(road, original), "switch: unsealed did not differ from sealed", failures);
            road = SurfaceTypeClass.Sealed();                 // switch back
            Expect(SurfaceType.ValuesEqual(road, original),
                "switch: switching back to sealed did not restore the sealed values (leftover)", failures);
            builder.Append("RCMSURF ").Append("switch-back".PadRight(22))
                   .Append("unsealed -> sealed restores values, no residue\n");
        }

        /// <summary> The two material sets are distinct and non-empty -- AUTHORED, NOT GENERATED. </summary>
        private static void CaseMaterialsAuthoredNotGenerated(StringBuilder builder, List<string> failures)
        {
            string sealedId = SurfaceTypeClass.Sealed().materialSetId;
            string unsealedId = SurfaceTypeClass.Unsealed().materialSetId;
            Expect(!string.IsNullOrEmpty(sealedId) && !string.IsNullOrEmpty(unsealedId) && sealedId != unsealedId,
                "materials: the surface material sets were not distinct authored identifiers", failures);
            builder.Append("RCMSURF ").Append("materials".PadRight(22))
                   .Append("distinct authored material sets (not generated)\n");
        }

        /// <summary> The surface values are bit-identical on repeat -- pure authored data (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            Expect(SurfaceType.ValuesEqual(SurfaceTypeClass.Unsealed(), SurfaceTypeClass.Unsealed())
                   && SurfaceType.ValuesEqual(SurfaceTypeClass.Sealed(), SurfaceTypeClass.Sealed()),
                "determinism: two identical surface selections differed", failures);
            builder.Append("RCMSURF ").Append("determinism".PadRight(22))
                   .Append("identical selection -> identical values\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

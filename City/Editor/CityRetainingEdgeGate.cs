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
    /// <summary> RM-34's gate: proves the elevated-road-edge trigger, the hysteresis that stops the band
    /// flickering, the independent two-side evaluation and the end cap without a scene. It asserts that a road at
    /// grade shows no face while a road on fill shows a wall and a taller one a viaduct; that the height above
    /// terrain is the road elevation minus the ground (floored at zero); that a height hovering at a band boundary
    /// HOLDS its band under the stable selector while the bare comparison flickers; that the two sides of the same
    /// station can land in different bands; that the face height tapers to zero over the cap transition at the end
    /// (daylighting, shared with RM-39); and that everything is bit-identical on repeat (C-06). The swept face
    /// mesh and the per-band wall prefabs are the deferred half. Report lines are prefixed RCMWALL. </summary>
    public static class CityRetainingEdgeGate
    {
        [MenuItem("Window/Road City Master/Testing/City retaining edge (height bands)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityRetainingEdgeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMWALL report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMWALL BEGIN\n");
            List<string> failures = new List<string>();

            CaseHeightAboveTerrain(builder, failures);
            CaseTriggerByBand(builder, failures);
            CaseHysteresisHoldsBand(builder, failures);
            CaseTwoSidesIndependent(builder, failures);
            CaseCapDaylights(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMWALL FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMWALL result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMWALL END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Height above terrain is the road elevation minus the ground, floored at zero. </summary>
        private static void CaseHeightAboveTerrain(StringBuilder builder, List<string> failures)
        {
            Expect(Math.Abs(RetainingEdge.HeightAboveTerrainMetres(5.0, 2.0) - 3.0) < 1e-9,
                "height: road 5 over ground 2 was not 3 m of fill", failures);
            Expect(Math.Abs(RetainingEdge.HeightAboveTerrainMetres(1.0, 4.0)) < 1e-12,
                "height: a road below the ground (a cut) did not floor to zero fill", failures);
            builder.Append("RCMWALL ").Append("height".PadRight(20))
                   .Append("fill height = road elevation - ground, floored at 0\n");
        }

        /// <summary> At grade there is no face; a few metres of fill triggers a wall; a lot triggers a viaduct. </summary>
        private static void CaseTriggerByBand(StringBuilder builder, List<string> failures)
        {
            RetainingEdgeClass cls = RetainingEdgeClass.Default(); // wall 1.5, viaduct 6
            Expect(RetainingEdge.SelectBand(0.0, cls) == EdgeStructure.EarthSlope
                   && !RetainingEdge.HasRetainingFace(EdgeStructure.EarthSlope),
                "trigger: a road at grade produced a retaining face", failures);
            Expect(RetainingEdge.SelectBand(3.0, cls) == EdgeStructure.RetainingWall
                   && RetainingEdge.HasRetainingFace(EdgeStructure.RetainingWall),
                "trigger: 3 m of fill did not produce a retaining wall", failures);
            Expect(RetainingEdge.SelectBand(8.0, cls) == EdgeStructure.Viaduct,
                "trigger: 8 m of fill did not produce a viaduct edge", failures);
            builder.Append("RCMWALL ").Append("trigger".PadRight(20))
                   .Append("grade -> none, fill -> wall, tall -> viaduct\n");
        }

        /// <summary> A height hovering at the wall boundary HOLDS its band under the stable selector, while the
        /// bare comparison flickers on every step. </summary>
        private static void CaseHysteresisHoldsBand(StringBuilder builder, List<string> failures)
        {
            RetainingEdgeClass cls = RetainingEdgeClass.Default(); // wall 1.5, hysteresis 0.25
            double[] hover = { 1.6, 1.4, 1.6, 1.4, 1.6 }; // straddles 1.5 within the hysteresis band

            int bareChanges = 0;
            EdgeStructure barePrev = RetainingEdge.SelectBand(hover[0], cls);
            for (int i = 1; i < hover.Length; i++)
            {
                EdgeStructure b = RetainingEdge.SelectBand(hover[i], cls);
                if (b != barePrev) { bareChanges++; }
                barePrev = b;
            }

            int stableChanges = 0;
            EdgeStructure stablePrev = EdgeStructure.RetainingWall; // arrived on a wall
            for (int i = 0; i < hover.Length; i++)
            {
                EdgeStructure b = RetainingEdge.SelectBandStable(hover[i], stablePrev, cls);
                if (b != stablePrev) { stableChanges++; }
                stablePrev = b;
            }

            Expect(bareChanges >= 3, "hysteresis: the bare comparison did not flicker as expected", failures);
            Expect(stableChanges == 0,
                "hysteresis: the stable selector flickered at the band boundary instead of holding", failures);
            builder.Append("RCMWALL ").Append("hysteresis".PadRight(20))
                   .Append("band holds at a boundary, no flicker\n");
        }

        /// <summary> The two sides of one station are evaluated independently: an unlevel ground can put one side
        /// on a wall and the other on an earth slope. </summary>
        private static void CaseTwoSidesIndependent(StringBuilder builder, List<string> failures)
        {
            RetainingEdgeClass cls = RetainingEdgeClass.Default();
            double leftFill = RetainingEdge.HeightAboveTerrainMetres(5.0, 2.0);  // 3 m -> wall
            double rightFill = RetainingEdge.HeightAboveTerrainMetres(5.0, 4.5); // 0.5 m -> earth slope
            EdgeStructure left = RetainingEdge.SelectBand(leftFill, cls);
            EdgeStructure right = RetainingEdge.SelectBand(rightFill, cls);
            Expect(left == EdgeStructure.RetainingWall && right == EdgeStructure.EarthSlope,
                "two-sides: the sides did not resolve independently on unlevel ground", failures);
            builder.Append("RCMWALL ").Append("two-sides".PadRight(20))
                   .Append("each side of a station banded independently\n");
        }

        /// <summary> The face tapers to zero at the end over the cap transition and is full height in the interior. </summary>
        private static void CaseCapDaylights(StringBuilder builder, List<string> failures)
        {
            RetainingEdgeClass cls = RetainingEdgeClass.Default(); // cap transition 5 m
            const double fill = 4.0;
            Expect(Math.Abs(RetainingEdge.CappedFaceHeightMetres(fill, 0.0, cls)) < 1e-12,
                "cap: the face did not reach zero at the very end (would leave a slab)", failures);
            Expect(Math.Abs(RetainingEdge.CappedFaceHeightMetres(fill, 2.5, cls) - 2.0) < 1e-9,
                "cap: the face did not taper linearly over the transition", failures);
            Expect(Math.Abs(RetainingEdge.CappedFaceHeightMetres(fill, 5.0, cls) - fill) < 1e-9,
                "cap: the face was not full height at the end of the transition", failures);
            Expect(Math.Abs(RetainingEdge.CappedFaceHeightMetres(fill, 20.0, cls) - fill) < 1e-9,
                "cap: the face was not full height in the interior", failures);
            builder.Append("RCMWALL ").Append("cap-daylight".PadRight(20))
                   .Append("face tapers to zero at ends, full in the interior\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            RetainingEdgeClass cls = RetainingEdgeClass.Default();
            double a = RetainingEdge.CappedFaceHeightMetres(3.3, 1.7, cls);
            double b = RetainingEdge.CappedFaceHeightMetres(3.3, 1.7, cls);
            EdgeStructure e1 = RetainingEdge.SelectBandStable(2.0, EdgeStructure.EarthSlope, cls);
            EdgeStructure e2 = RetainingEdge.SelectBandStable(2.0, EdgeStructure.EarthSlope, cls);
            Expect(a == b && e1 == e2,
                "determinism: two identical retaining-edge computations produced different values", failures);
            builder.Append("RCMWALL ").Append("determinism".PadRight(20))
                   .Append("identical inputs -> bit-identical band/height\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

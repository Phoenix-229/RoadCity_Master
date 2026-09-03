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
    /// <summary> RM-30's gate: proves the culvert sizing/skew/invert maths and the ditch Manning capacity + outfall
    /// rule without a scene. It asserts that a shallow crossing needs a longer barrel than a square one; that the
    /// barrel is sized from FLOW through the authored HDS-5 table (not clearance) and flags a flow past the table;
    /// that the outlet invert carries the channel slope through and an inlet invert above the upstream bed is
    /// flagged as damming the stream; that Manning capacity rises with depth and a V-ditch capacity is the Manning
    /// flow of its own geometry; that a ditch with no outfall (or one that cannot fall to it) is refused rather
    /// than emitted; and that the maths is bit-identical on repeat (C-06). The barrel/opening mesh, the collider
    /// and the crossing detection against the hydrology polylines are the deferred half. Report lines are prefixed
    /// RCMCULV. </summary>
    public static class CityCulvertGate
    {
        [MenuItem("Window/Road City Master/Testing/City culvert + ditch (HDS-5 / Manning)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityCulvertGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMCULV report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCULV BEGIN\n");
            List<string> failures = new List<string>();

            CaseSkewLengthensBarrel(builder, failures);
            CaseSizedFromFlowNotClearance(builder, failures);
            CaseInvertContinuityAndDamFlag(builder, failures);
            CaseManningAndVDitch(builder, failures);
            CaseDitchNeedsOutfall(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCULV FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCULV result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCULV END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A square crossing gives the shortest barrel (= embankment width); a shallow crossing needs a
        /// longer one. </summary>
        private static void CaseSkewLengthensBarrel(StringBuilder builder, List<string> failures)
        {
            const double width = 12.0;
            double square = Culvert.BarrelLengthMetres(width, 90.0);
            double skew = Culvert.BarrelLengthMetres(width, 20.0);
            Expect(Math.Abs(square - width) < 1e-9, "skew: a square crossing was not the embankment width",
                failures);
            Expect(skew > square + 1.0, "skew: a shallow crossing did not need a longer barrel", failures);
            Expect(Math.Abs(skew - width / Math.Sin(20.0 * Math.PI / 180.0)) < 1e-9,
                "skew: the skew barrel length did not follow width / sin(angle)", failures);
            builder.Append("RCMCULV ").Append("skew-length".PadRight(22))
                   .Append("barrel length grows as the crossing leaves square\n");
        }

        /// <summary> The barrel is picked from the flow-class table, larger flow -> larger barrel, and a flow past
        /// the table is flagged. </summary>
        private static void CaseSizedFromFlowNotClearance(StringBuilder builder, List<string> failures)
        {
            CulvertClass cls = CulvertClass.Default();
            Culvert.BarrelSizing small = Culvert.SizeBarrel(0.3, cls);   // <= 0.5 class
            Culvert.BarrelSizing mid = Culvert.SizeBarrel(3.0, cls);     // <= 5.0 class
            Culvert.BarrelSizing huge = Culvert.SizeBarrel(50.0, cls);   // past the table
            Expect(Math.Abs(small.barrelDiameterMetres - 0.6) < 1e-9,
                "sizing: a low flow did not take the 0.6 m barrel", failures);
            Expect(mid.barrelDiameterMetres > small.barrelDiameterMetres,
                "sizing: a larger flow did not take a larger barrel", failures);
            Expect(huge.exceedsTable && Math.Abs(huge.barrelDiameterMetres - 2.4) < 1e-9,
                "sizing: a flow past the table was not flagged at the top barrel size", failures);
            builder.Append("RCMCULV ").Append("flow-sizing".PadRight(22))
                   .Append("barrel sized from flow via the HDS-5 table, not clearance\n");
        }

        /// <summary> The outlet invert drops by the channel slope over the barrel; an inlet invert above the
        /// upstream bed is flagged as damming while one at the bed is not. </summary>
        private static void CaseInvertContinuityAndDamFlag(StringBuilder builder, List<string> failures)
        {
            double outlet = Culvert.OutletInvertElevationMetres(10.0, 0.01, 12.0); // 1% over 12 m -> 0.12 drop
            Expect(Math.Abs(outlet - (10.0 - 0.12)) < 1e-9,
                "invert: the outlet invert did not carry the channel slope through the barrel", failures);
            Expect(!Culvert.InvertDamsStream(10.0, 10.0),
                "invert: a barrel seated at the upstream bed was wrongly flagged as damming", failures);
            Expect(Culvert.InvertDamsStream(10.5, 10.0),
                "invert: a barrel invert above the upstream bed was not flagged as damming the stream", failures);
            builder.Append("RCMCULV ").Append("invert".PadRight(22))
                   .Append("invert carries the bed slope; a raised invert dams and is flagged\n");
        }

        /// <summary> Manning capacity rises with depth, and the V-ditch capacity equals the Manning flow of its own
        /// area/radius. </summary>
        private static void CaseManningAndVDitch(StringBuilder builder, List<string> failures)
        {
            RoadsideDitchClass cls = RoadsideDitchClass.Default(); // n 0.03, z 3
            double shallow = Culvert.VDitchCapacityM3PerSecond(0.2, 0.01, cls);
            double deep = Culvert.VDitchCapacityM3PerSecond(0.4, 0.01, cls);
            Expect(shallow > 0.0 && deep > shallow,
                "manning: a deeper V-ditch did not carry more flow", failures);

            // Cross-check the deep case against Manning computed from the same geometry by hand.
            double z = cls.sideSlopeHorizontalPerVertical;
            double area = z * 0.4 * 0.4;
            double perimeter = 2.0 * 0.4 * Math.Sqrt(1.0 + z * z);
            double radius = area / perimeter;
            double expected = Culvert.ManningFlowM3PerSecond(area, radius, 0.01, cls.manningRoughness);
            Expect(Math.Abs(deep - expected) < 1e-9,
                "manning: the V-ditch capacity did not match Manning of its own geometry", failures);

            Expect(Math.Abs(Culvert.ManningFlowM3PerSecond(1.0, 0.5, 0.0, 0.03)) < 1e-12,
                "manning: a zero slope did not give zero flow", failures);
            builder.Append("RCMCULV ").Append("manning".PadRight(22))
                   .Append("Q = (1/n)·A·R^(2/3)·S^(1/2); deeper V-ditch carries more\n");
        }

        /// <summary> A ditch that can fall to an existing outfall drains; one with no outfall, or one whose
        /// downstream end is not lower, is refused (a closed basin). </summary>
        private static void CaseDitchNeedsOutfall(StringBuilder builder, List<string> failures)
        {
            Expect(Culvert.DitchDrainsToOutfall(5.0, 4.5, true),
                "outfall: a ditch that could fall to an outfall was refused", failures);
            Expect(!Culvert.DitchDrainsToOutfall(5.0, 4.5, false),
                "outfall: a ditch with no outfall was not refused", failures);
            Expect(!Culvert.DitchDrainsToOutfall(5.0, 5.2, true),
                "outfall: a ditch that could not fall to its outfall was not refused as a closed basin", failures);
            builder.Append("RCMCULV ").Append("ditch-outfall".PadRight(22))
                   .Append("a ditch drains to an outfall or is refused, never a basin\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            RoadsideDitchClass cls = RoadsideDitchClass.Default();
            double a = Culvert.VDitchCapacityM3PerSecond(0.3, 0.02, cls);
            double b = Culvert.VDitchCapacityM3PerSecond(0.3, 0.02, cls);
            double l1 = Culvert.BarrelLengthMetres(10.0, 35.0);
            double l2 = Culvert.BarrelLengthMetres(10.0, 35.0);
            Expect(a == b && l1 == l2,
                "determinism: two identical culvert/ditch computations produced different values", failures);
            builder.Append("RCMCULV ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical sizing/capacity\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

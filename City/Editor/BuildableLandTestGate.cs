#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-11's gate: proves the buildable-land test is a deterministic pure function of
    /// (lot, terrain, criteria) and that each of its verdicts -- buildable, reshaped, rejected -- is
    /// reachable, on terrain hand-built to trigger exactly one reason at a time.
    ///
    /// The gentle shared <see cref="FakePcgTerrain"/> stands in for flat, buildable ground; a small
    /// analytic ramp fixture (defined below, NOT a second copy of the shared terrain -- it is a
    /// different surface for a different assertion) supplies the steep, high-relief and waterside cases
    /// the gentle surface cannot. It does NOT judge that the reshaped remnant looks right in the Scene
    /// view -- that is the author's visual check on a real generated world. What it locks down is that
    /// the verdicts are reproducible and that the reasons carry the numbers a rejection must. </summary>
    public static class BuildableLandTestGate
    {
        private const float Spacing = 5f;

        [MenuItem("Window/Road City Master/Testing/Buildable-land test")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.BuildableLandTestGate.RunBatch. Exits 1 on failure so a build
        /// script can gate on it. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Builds each case, evaluates, and checks. Report lines are prefixed RCMBUILD so a
        /// log grep can read the verdict without wading through Unity's surrounding output. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMBUILD BEGIN\n");
            List<string> failures = new List<string>();
            BuildabilityCriteria criteria = BuildabilityCriteria.Default();

            // 1. BUILDABLE: gentle shared terrain, a comfortable lot, no water, nothing occupied.
            IPCGDataProvider gentle = new FakePcgTerrain();
            List<Vector3> flatLot = Square(0f, 0f, 40f);
            LotVerdict buildable = BuildableLandTest.Evaluate(
                flatLot, gentle, Spacing, float.NegativeInfinity, null, criteria);
            Log(builder, "buildable", buildable);
            Expect(buildable.result == Buildability.Buildable, "flat lot was not Buildable", failures);
            Expect(buildable.reshapedPolygon == null, "buildable lot carried a reshaped polygon", failures);

            // Determinism (C-06): the same lot on the same terrain earns a field-identical verdict.
            LotVerdict buildableAgain = BuildableLandTest.Evaluate(
                flatLot, gentle, Spacing, float.NegativeInfinity, null, criteria);
            Expect(VerdictsEqual(buildable, buildableAgain),
                "re-evaluating the same lot produced a different verdict", failures);

            // 2. WATER: flat ground at Y=0 with the water surface at Y=0 -- inside the freeboard.
            IPCGDataProvider flatAtZero = new RampTerrain(0f, 0f, 0f, 0f);
            LotVerdict wet = BuildableLandTest.Evaluate(
                Square(0f, 0f, 40f), flatAtZero, Spacing, 0f, null, criteria);
            Log(builder, "water", wet);
            Expect(wet.result == Buildability.Rejected, "waterside lot was not Rejected", failures);
            Expect(wet.reason.Contains("water surface"), "water rejection did not name the water surface", failures);

            // 3. OCCUPANCY: a buildable lot that overlaps an already-claimed footprint.
            List<List<Vector3>> occupied = new List<List<Vector3>> { Square(20f, 20f, 40f) };
            LotVerdict taken = BuildableLandTest.Evaluate(
                Square(0f, 0f, 40f), gentle, Spacing, float.NegativeInfinity, occupied, criteria);
            Log(builder, "occupancy", taken);
            Expect(taken.result == Buildability.Rejected, "overlapping lot was not Rejected", failures);
            Expect(taken.reason.Contains("footprint"), "occupancy rejection did not name a footprint", failures);

            // 4. RESHAPED: flat on the left, steep on the right -- the buildable half should survive.
            IPCGDataProvider splitRamp = new RampTerrain(0f, 2f, 50f, 50f);
            LotVerdict reshaped = BuildableLandTest.Evaluate(
                Rect(0f, 0f, 100f, 60f), splitRamp, Spacing, float.NegativeInfinity, null, criteria);
            Log(builder, "reshaped", reshaped);
            Expect(reshaped.result == Buildability.Reshaped, "half-steep lot was not Reshaped", failures);
            Expect(reshaped.reshapedPolygon != null && reshaped.reshapedPolygon.Count >= 3,
                "reshape produced no remnant polygon", failures);
            if (reshaped.reshapedPolygon != null)
            {
                Expect(Geometry2D.Area(reshaped.reshapedPolygon) >= criteria.minLotArea,
                    "reshaped remnant is under the zone minimum", failures);
                Expect(reshaped.maxSlope <= criteria.maxSlopePercent,
                    "reshaped remnant is still over the grade limit", failures);
            }

            // 5. REJECT (slope): a uniformly steep lot with no buildable part to keep.
            IPCGDataProvider steep = new RampTerrain(0f, 50f, 50f, 0f);
            LotVerdict tooSteep = BuildableLandTest.Evaluate(
                Square(0f, 0f, 40f), steep, Spacing, float.NegativeInfinity, null, criteria);
            Log(builder, "reject-slope", tooSteep);
            Expect(tooSteep.result == Buildability.Rejected, "uniformly steep lot was not Rejected", failures);
            Expect(tooSteep.reason.Contains("max slope"), "slope rejection did not name the slope", failures);

            // 6. REJECT (relief): a gentle grade, but a large lot so the height range beats the pad limit.
            IPCGDataProvider gentleRamp = new RampTerrain(0f, 10f, 10f, 0f);
            LotVerdict tooDeep = BuildableLandTest.Evaluate(
                Rect(0f, 0f, 80f, 80f), gentleRamp, Spacing, float.NegativeInfinity, null, criteria);
            Log(builder, "reject-relief", tooDeep);
            Expect(tooDeep.result == Buildability.Rejected, "high-relief lot was not Rejected", failures);
            Expect(tooDeep.reason.Contains("relief"), "relief rejection did not name the relief", failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMBUILD FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMBUILD result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMBUILD END");

            report = builder.ToString();
            return failures.Count == 0;
        }
        /// <summary> Appends one verdict as a single RCMBUILD line with the numbers a reader would want
        /// to sanity-check the reason against: the verdict, the grade, and the relief. </summary>
        private static void Log(StringBuilder builder, string caseName, LotVerdict v)
        {
            builder.Append("RCMBUILD ").Append(caseName.PadRight(13))
                   .Append(v.result.ToString().PadRight(10))
                   .Append("maxSlope ").Append(v.maxSlope.ToString("F1")).Append("%  ")
                   .Append("relief ").Append(v.reliefMetres.ToString("F1")).Append("m  ")
                   .Append(v.reason).Append("\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        /// <summary> Field-by-field verdict equality, the determinism check's yardstick: same result,
        /// same reason text, byte-identical numbers, and a reshaped polygon that is either null on both
        /// or the same vertices in the same order. Anything less means the test is not a pure function
        /// of its inputs (C-06). </summary>
        private static bool VerdictsEqual(LotVerdict a, LotVerdict b)
        {
            if (a.result != b.result || a.reason != b.reason ||
                a.maxSlope != b.maxSlope || a.meanSlope != b.meanSlope ||
                a.reliefMetres != b.reliefMetres)
            {
                return false;
            }
            if (a.reshapedPolygon == null || b.reshapedPolygon == null)
            {
                return a.reshapedPolygon == null && b.reshapedPolygon == null;
            }
            if (a.reshapedPolygon.Count != b.reshapedPolygon.Count)
            {
                return false;
            }
            for (int i = 0; i < a.reshapedPolygon.Count; i++)
            {
                if (a.reshapedPolygon[i] != b.reshapedPolygon[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> A CCW square lot in the XZ plane with its near corner at
        /// (<paramref name="x0"/>, <paramref name="z0"/>) and the given side. Y is left at 0: the test
        /// re-samples real heights from the terrain provider, so the polygon's own Y never enters a
        /// verdict. </summary>
        private static List<Vector3> Square(float x0, float z0, float side)
        {
            return Rect(x0, z0, side, side);
        }

        /// <summary> A CCW rectangle lot in the XZ plane: near corner at
        /// (<paramref name="x0"/>, <paramref name="z0"/>), extent <paramref name="width"/> along X and
        /// <paramref name="depth"/> along Z. </summary>
        private static List<Vector3> Rect(float x0, float z0, float width, float depth)
        {
            return new List<Vector3>
            {
                new Vector3(x0, 0f, z0),
                new Vector3(x0 + width, 0f, z0),
                new Vector3(x0 + width, 0f, z0 + depth),
                new Vector3(x0, 0f, z0 + depth)
            };
        }
    }
}
#endif

#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-26's gate: proves the pure half of the road terminus -- detection and placement quality --
    /// without a scene. It asserts that an end node NOT in an intersection is a terminus while one that IS shows
    /// nothing (two roads meeting end-to-end); that nothing is placed until the author assigns a prefab (default
    /// none); that the terminator is SEATED ON THE ROAD's own surface height and not the sampled ground, which is
    /// the sink-into-the-shoulder defect the task exists to prevent; that it is centred on the carriageway and
    /// squared to the final node's heading facing OUTWARD (opposite yaw at the two ends); and that the placement is
    /// bit-identical on re-bake (C-06). The author-facing prefab-category selection and the actual scene placement
    /// are the deferred half. Report lines are prefixed RCMTERM. </summary>
    public static class CityRoadTerminusGate
    {
        [MenuItem("Window/Road City Master/Testing/City road terminus")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityRoadTerminusGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMTERM report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMTERM BEGIN\n");
            List<string> failures = new List<string>();

            CaseDetection(builder, failures);
            CaseDefaultNonePlacesNothing(builder, failures);
            CaseSeatedOnRoadSurface(builder, failures);
            CaseCentredAndSquaredOutward(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMTERM FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMTERM result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMTERM END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A free end (not in an intersection) is a terminus; an end that belongs to an intersection is
        /// not, so two roads meeting end-to-end show nothing. </summary>
        private static void CaseDetection(StringBuilder builder, List<string> failures)
        {
            Expect(RoadTerminus.IsTerminus(false), "detect: a free road end was not a terminus", failures);
            Expect(!RoadTerminus.IsTerminus(true),
                "detect: an end that belongs to an intersection was treated as a terminus", failures);
            builder.Append("RCMTERM ").Append("detection".PadRight(22))
                   .Append("free end is a terminus, intersection end is not\n");
        }

        /// <summary> The terminator is author-selected and defaults to none: nothing is placed at a terminus until a
        /// prefab is assigned, and never at a non-terminus even when one is. </summary>
        private static void CaseDefaultNonePlacesNothing(StringBuilder builder, List<string> failures)
        {
            Expect(!RoadTerminus.ShouldPlace(false, false),
                "default: a terminus with no prefab assigned still placed something", failures);
            Expect(RoadTerminus.ShouldPlace(false, true),
                "default: a terminus with a prefab assigned placed nothing", failures);
            Expect(!RoadTerminus.ShouldPlace(true, true),
                "default: an intersection end placed a terminator despite not being a terminus", failures);
            builder.Append("RCMTERM ").Append("default-none".PadRight(22))
                   .Append("places only when a terminus has an author-chosen prefab\n");
        }

        /// <summary> The terminator sits at the ROAD's surface height, not the sampled ground: a road raised 5 m
        /// over terrain at 0 gets a terminator at y = 5, never y = 0. </summary>
        private static void CaseSeatedOnRoadSurface(StringBuilder builder, List<string> failures)
        {
            // End node centre reported at natural-ground height 0, but the road surface there is raised to 5 m.
            Vector3 nodeCentre = new Vector3(120f, 0f, 40f);
            const float roadSurfaceHeight = 5f;
            TerminusPlacement p = RoadTerminus.ComputePlacement(
                nodeCentre, roadSurfaceHeight, new Vector2(0f, 1f), RoadEnd.End);
            Expect(Mathf.Abs(p.position.y - roadSurfaceHeight) < 1e-4f,
                "seat: the terminator did not sit on the road surface height", failures);
            Expect(Mathf.Abs(p.position.y - nodeCentre.y) > 1e-3f,
                "seat: the terminator sank to the natural ground height instead of the road surface", failures);
            builder.Append("RCMTERM ").Append("seat-on-road".PadRight(22))
                   .Append("terminator seats on the road surface, not the ground\n");
        }

        /// <summary> Centred on the carriageway (XZ = node centre) and squared to the heading facing OUTWARD: at the
        /// END node the yaw faces along the tangent, at the START node it faces the opposite way (180 deg apart). </summary>
        private static void CaseCentredAndSquaredOutward(StringBuilder builder, List<string> failures)
        {
            Vector3 nodeCentre = new Vector3(10f, 3f, -7f);
            Vector2 tangent = new Vector2(1f, 0f); // road runs along +X, so +X yaw is 90 deg
            TerminusPlacement atEnd = RoadTerminus.ComputePlacement(nodeCentre, 3f, tangent, RoadEnd.End);
            TerminusPlacement atStart = RoadTerminus.ComputePlacement(nodeCentre, 3f, tangent, RoadEnd.Start);

            Expect(Mathf.Abs(atEnd.position.x - nodeCentre.x) < 1e-4f &&
                   Mathf.Abs(atEnd.position.z - nodeCentre.z) < 1e-4f,
                "centred: the terminator drifted off the carriageway centreline", failures);
            Expect(Mathf.Abs(Mathf.DeltaAngle(atEnd.yawDegrees, 90f)) < 1e-3f,
                "squared: the END terminator was not squared to the road heading (+X -> 90 deg)", failures);
            Expect(Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(atEnd.yawDegrees, atStart.yawDegrees)) - 180f) < 1e-3f,
                "outward: the START and END terminators did not face opposite ways", failures);
            builder.Append("RCMTERM ").Append("centred-squared".PadRight(22))
                   .Append("centred on carriageway, squared outward at both ends\n");
        }

        /// <summary> The same end re-bakes to the identical placement -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            Vector3 nodeCentre = new Vector3(55f, 12f, 88f);
            Vector2 tangent = new Vector2(0.6f, 0.8f);
            TerminusPlacement a = RoadTerminus.ComputePlacement(nodeCentre, 12f, tangent, RoadEnd.End);
            TerminusPlacement b = RoadTerminus.ComputePlacement(nodeCentre, 12f, tangent, RoadEnd.End);
            Expect(a.position == b.position && a.yawDegrees == b.yawDegrees,
                "determinism: two placements of the same end differed", failures);
            builder.Append("RCMTERM ").Append("determinism".PadRight(22))
                   .Append("identical end -> identical placement on re-bake\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

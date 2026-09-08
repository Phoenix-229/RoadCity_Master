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
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-22's gate: proves <see cref="BuildingPadConform"/> picks the right pad elevation by
    /// each rule, grows an apron ring outward from the footprint, blends monotonically, and -- the load-
    /// bearing part -- produces IDENTICAL pads whatever order the lots arrive in and merges overlapping
    /// aprons into one shared-elevation group (C-06). It runs on analytic terrains built in memory
    /// (<see cref="FakePcgTerrain"/>, <see cref="RampTerrain"/>) so it needs no scene, and it judges the
    /// pad RECORD only -- RM-22 never edits terrain, so "terrain 5 m away is untouched" is the solver's
    /// (RM-14's) property, not measurable here. Report lines are prefixed RCMPAD. </summary>
    public static class PadConformGate
    {
        [MenuItem("Window/Road City Master/Testing/Pad conform")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.PadConformGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMPAD report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPAD BEGIN\n");
            List<string> failures = new List<string>();

            CaseElevationRules(builder, failures);
            CaseBlendMonotonic(builder, failures);
            CaseOrderIndependent(builder, failures);
            CaseGrouping(builder, failures);
            CaseAreaWeighted(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPAD FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPAD result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPAD END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Centroid rule = one sample at the area centroid; min-corner rule = min corner
        /// height + freeboard. On a 10 % ramp a 10 m square at the origin has corners at 0 m and 0.5 m,
        /// so the two rules must give distinctly different, checkable elevations. </summary>
        private static void CaseElevationRules(StringBuilder builder, List<string> failures)
        {
            PadConformParams p = PadConformParams.Default();

            // Centroid rule on the gentle surface: elevation is the height at the footprint centroid.
            FakePcgTerrain gentle = new FakePcgTerrain();
            List<Vector3> fp = Square(100f, 100f, 5f);
            PadRecord centroid = Single(gentle, p, 1, fp, PadElevationRule.CentroidSample);
            float expected = gentle.SampleHeight(100f, 100f);
            Log(builder, "centroid-elev", centroid);
            Expect(Approx(centroid.padElevation, expected, 1e-3f),
                "centroid-elev: elevation " + centroid.padElevation + " != centroid sample " + expected, failures);
            Expect(FootprintAtY(centroid, centroid.padElevation),
                "centroid-elev: footprint Y not seated at the pad elevation", failures);
            Expect(Geometry2D.Area(centroid.apronRing) > Geometry2D.Area(centroid.footprint),
                "centroid-elev: apron ring is not larger than the footprint", failures);

            // Min-corner rule on a uniform 10 % ramp: min corner (0 m) + 0.3 m freeboard = 0.3 m.
            RampTerrain ramp = new RampTerrain(0f, 0f, 10f, 0f);
            PadRecord minCorner = Single(ramp, p, 1, Square(0f, 0f, 5f), PadElevationRule.MinCornerPlusFreeboard);
            Log(builder, "mincorner-elev", minCorner);
            Expect(Approx(minCorner.padElevation, 0f + p.freeboardMetres, 1e-3f),
                "mincorner-elev: elevation " + minCorner.padElevation + " != min(0) + freeboard", failures);

            // Same footprint, centroid rule: centroid at x=0 sits at 0 m -- distinct from the min-corner result.
            PadRecord centroidRamp = Single(ramp, p, 1, Square(0f, 0f, 5f), PadElevationRule.CentroidSample);
            Log(builder, "centroid-ramp", centroidRamp);
            Expect(Approx(centroidRamp.padElevation, 0f, 1e-3f),
                "centroid-ramp: elevation " + centroidRamp.padElevation + " != 0 at the ramp origin", failures);
        }

        /// <summary> The apron weight rises monotonically from 0 (all pad) at the footprint edge to 1
        /// (all terrain) at the ring, for both curves, and the height lerp hits both endpoints. </summary>
        private static void CaseBlendMonotonic(StringBuilder builder, List<string> failures)
        {
            float[] ts = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
            CheckCurve(builder, failures, PadBlendCurve.Smoothstep, ts);
            CheckCurve(builder, failures, PadBlendCurve.Linear, ts);
        }

        /// <summary> One curve: endpoints 0 and 1, non-decreasing, and the height lerp hits pad at t=0
        /// and natural at t=1. </summary>
        private static void CheckCurve(StringBuilder builder, List<string> failures, PadBlendCurve curve, float[] ts)
        {
            builder.Append("RCMPAD ").Append(("blend-" + curve).PadRight(19)).Append("weights ");
            float prev = float.NegativeInfinity;
            bool monotone = true;
            for (int i = 0; i < ts.Length; i++)
            {
                float w = BuildingPadConform.NaturalWeight01(ts[i], curve);
                builder.Append(w.ToString("0.00")).Append(i == ts.Length - 1 ? "" : ", ");
                if (w < prev - 1e-4f) { monotone = false; }
                prev = w;
            }
            builder.Append("\n");
            Expect(Approx(BuildingPadConform.NaturalWeight01(0f, curve), 0f, 1e-4f),
                "blend-" + curve + ": weight at the footprint edge is not 0", failures);
            Expect(Approx(BuildingPadConform.NaturalWeight01(1f, curve), 1f, 1e-4f),
                "blend-" + curve + ": weight at the apron ring is not 1", failures);
            Expect(monotone, "blend-" + curve + ": weight is not monotonic non-decreasing", failures);
            Expect(Approx(BuildingPadConform.ApronHeight(10f, 2f, 0f, curve), 10f, 1e-4f),
                "blend-" + curve + ": height at t=0 is not the pad elevation", failures);
            Expect(Approx(BuildingPadConform.ApronHeight(10f, 2f, 1f, curve), 2f, 1e-4f),
                "blend-" + curve + ": height at t=1 is not the natural terrain height", failures);
        }

        /// <summary> Three overlapping lots on a ramp, computed in one order and its reverse, must give
        /// byte-for-byte identical pads per lot id -- the determinism the whole task exists to guarantee
        /// (C-06). </summary>
        private static void CaseOrderIndependent(StringBuilder builder, List<string> failures)
        {
            RampTerrain ramp = new RampTerrain(0f, 0f, 10f, 0f);
            PadConformParams p = PadConformParams.Default();
            List<PadRequest> forward = new List<PadRequest>
            {
                Req(10, Square(0f, 0f, 5f)),
                Req(20, Square(12f, 0f, 5f)),
                Req(30, Square(24f, 0f, 5f)),
            };
            List<PadRequest> reversed = new List<PadRequest> { forward[2], forward[1], forward[0] };

            List<PadRecord> a = BuildingPadConform.ComputePads(forward, ramp, p);
            List<PadRecord> b = BuildingPadConform.ComputePads(reversed, ramp, p);
            bool same = SamePads(a, b);
            builder.Append("RCMPAD ").Append("order-independent".PadRight(19))
                   .Append(same ? "PASS  " : "FAIL  ").Append("pads ").Append(a.Count).Append("\n");
            Expect(same, "order-independent: pads differ between forward and reversed input", failures);
        }

        /// <summary> Overlapping aprons merge into one group sharing the smallest lot id; lots whose
        /// aprons do not reach each other stay in their own groups. </summary>
        private static void CaseGrouping(StringBuilder builder, List<string> failures)
        {
            FakePcgTerrain gentle = new FakePcgTerrain();
            PadConformParams p = PadConformParams.Default();

            // Far apart: two lots 100 m apart, 4 m aprons -- no overlap, so two groups.
            List<PadRecord> apart = BuildingPadConform.ComputePads(new List<PadRequest>
            {
                Req(1, Square(0f, 0f, 5f)),
                Req(2, Square(100f, 0f, 5f)),
            }, gentle, p);
            builder.Append("RCMPAD ").Append("group-split".PadRight(19))
                   .Append("ids ").Append(apart[0].padGroupId).Append(", ").Append(apart[1].padGroupId).Append("\n");
            Expect(apart[0].padGroupId == 1 && apart[1].padGroupId == 2,
                "group-split: distant lots were not left in their own groups", failures);

            // Close: two lots 12 m apart -- aprons overlap, so one group labelled by the smaller id.
            List<PadRecord> close = BuildingPadConform.ComputePads(new List<PadRequest>
            {
                Req(1, Square(0f, 0f, 5f)),
                Req(2, Square(12f, 0f, 5f)),
            }, gentle, p);
            builder.Append("RCMPAD ").Append("group-merge".PadRight(19))
                   .Append("ids ").Append(close[0].padGroupId).Append(", ").Append(close[1].padGroupId).Append("\n");
            Expect(close[0].padGroupId == 1 && close[1].padGroupId == 1,
                "group-merge: overlapping lots were not merged under the smallest id", failures);
            Expect(Approx(close[0].padElevation, close[1].padElevation, 1e-4f),
                "group-merge: merged lots do not share one elevation", failures);
        }

        /// <summary> The default area-weighted group elevation must land between the members' own
        /// elevations and nearer the larger footprint's. Big lot at 0 m (elevation 0), small lot at
        /// 10 m on a 10 % ramp (elevation 1 m); the shared level should sit well below the midpoint.
        /// The footprints do not overlap (half-sizes 6 and 3, centres 10 m apart) but their 4 m aprons
        /// do, which is what pulls them into one group. </summary>
        private static void CaseAreaWeighted(StringBuilder builder, List<string> failures)
        {
            RampTerrain ramp = new RampTerrain(0f, 0f, 10f, 0f);
            PadConformParams p = PadConformParams.Default();
            List<PadRecord> pads = BuildingPadConform.ComputePads(new List<PadRequest>
            {
                Req(1, Square(0f, 0f, 6f)),
                Req(2, Square(10f, 0f, 3f)),
            }, ramp, p);
            float shared = pads[0].padElevation;
            builder.Append("RCMPAD ").Append("area-weighted".PadRight(19))
                   .Append("shared ").Append(shared.ToString("0.000")).Append("\n");
            Expect(pads[0].padGroupId == 1 && pads[1].padGroupId == 1,
                "area-weighted: the two lots did not merge", failures);
            Expect(shared > 0f && shared < 1f,
                "area-weighted: shared elevation " + shared + " is not between the members' 0 and 1", failures);
            Expect(Mathf.Abs(shared - 0f) < Mathf.Abs(shared - 1f),
                "area-weighted: shared elevation is not biased toward the larger footprint", failures);
        }

        /// <summary> Computes a single pad through the public batch entry point (one request). </summary>
        private static PadRecord Single(IPCGDataProvider terrain, PadConformParams p, int lotId,
            List<Vector3> footprint, PadElevationRule rule)
        {
            List<PadRequest> one = new List<PadRequest> { Req(lotId, footprint, rule) };
            return BuildingPadConform.ComputePads(one, terrain, p)[0];
        }

        private static PadRequest Req(int lotId, List<Vector3> footprint)
        {
            return new PadRequest(lotId, footprint, PadElevationRule.CentroidSample, GroundContactMode.OnPad);
        }

        private static PadRequest Req(int lotId, List<Vector3> footprint, PadElevationRule rule)
        {
            return new PadRequest(lotId, footprint, rule, GroundContactMode.OnPad);
        }

        /// <summary> A counter-clockwise square footprint centred at (cx, cz) with the given half-size,
        /// flat at Y=0 (the conform samples its own heights, so the input Y is irrelevant). </summary>
        private static List<Vector3> Square(float cx, float cz, float half)
        {
            return new List<Vector3>
            {
                new Vector3(cx - half, 0f, cz - half),
                new Vector3(cx + half, 0f, cz - half),
                new Vector3(cx + half, 0f, cz + half),
                new Vector3(cx - half, 0f, cz + half),
            };
        }

        private static bool Approx(float a, float b, float eps)
        {
            return Mathf.Abs(a - b) <= eps;
        }

        /// <summary> True when every footprint vertex of the pad sits at the given Y. </summary>
        private static bool FootprintAtY(PadRecord pad, float y)
        {
            for (int i = 0; i < pad.footprint.Count; i++)
            {
                if (!Approx(pad.footprint[i].y, y, 1e-4f)) { return false; }
            }
            return true;
        }

        /// <summary> True when two pad lists agree on every lot's group id, elevation, and footprint and
        /// apron vertices -- matched by lot id, since the batch returns lots in id order regardless of
        /// input order. </summary>
        private static bool SamePads(List<PadRecord> a, List<PadRecord> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                PadRecord x = a[i];
                PadRecord y = Match(b, x.lotId);
                if (y == null || x.padGroupId != y.padGroupId || !Approx(x.padElevation, y.padElevation, 1e-5f))
                {
                    return false;
                }
                if (!SameRing(x.footprint, y.footprint) || !SameRing(x.apronRing, y.apronRing))
                {
                    return false;
                }
            }
            return true;
        }

        private static PadRecord Match(List<PadRecord> pads, int lotId)
        {
            for (int i = 0; i < pads.Count; i++)
            {
                if (pads[i].lotId == lotId) { return pads[i]; }
            }
            return null;
        }

        private static bool SameRing(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                if ((a[i] - b[i]).sqrMagnitude > 1e-8f) { return false; }
            }
            return true;
        }

        /// <summary> Appends one pad as a single RCMPAD line: the case, group, elevation and vertex counts. </summary>
        private static void Log(StringBuilder builder, string name, PadRecord pad)
        {
            builder.Append("RCMPAD ").Append(name.PadRight(19))
                   .Append("group ").Append(pad.padGroupId)
                   .Append("  elev ").Append(pad.padElevation.ToString("0.000"))
                   .Append("  fp ").Append(pad.footprint.Count)
                   .Append("  ring ").Append(pad.apronRing.Count).Append("\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

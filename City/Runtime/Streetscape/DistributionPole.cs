#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
using RoadCityMaster.CrossSection;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which side of a road frontage a pole run sits on. A real distribution run stays on ONE side
    /// for its whole length rather than alternating, so this is a per-run flag the planner holds stable. </summary>
    public enum FrontageSide
    {
        Left = 0,
        Right = 1
    }


    /// <summary> RM-24's authored parameter block for a distribution-pole run. AUTHORED against real ranges
    /// (C-15): distribution spans are TENS of metres (default 40 m) -- an order of magnitude shorter than
    /// RM-23's transmission spans, which is why this run does NOT use the catenary solver. The remaining
    /// numbers are the conflict-resolution handles the frontage needs. </summary>
    [Serializable]
    public sealed class DistributionPoleClass
    {
        public float spanLengthMetres = 40f;
        public FrontageSide side = FrontageSide.Right;
        public float minSeparationFromFurnitureMetres = 2f;
        public float maxNudgeMetres = 5f;
        public float nudgeStepMetres = 0.5f;

        public static DistributionPoleClass Default()
        {
            return new DistributionPoleClass();
        }
    }


    /// <summary> RM-24's verifiable core: where distribution poles land along a road frontage, once the
    /// frontage is already carrying RM-13's streetlights, signs, bins and bus stops from the SAME
    /// <c>EdgeObjectMaker</c> mechanism. A pole is just a prefab repeated at a spacing -- that part is the
    /// existing maker -- so the real task, and the part that can be proven headlessly, is everything AROUND the
    /// spacing: keeping the run on one consistent side, and resolving the conflict where a pole would land on a
    /// furniture item already on that strip. The spacing/exclusion maths is RM-13's
    /// <see cref="FurnitureRunPlanner"/> reused, not re-implemented (no-duplication).
    ///
    /// Conflict resolution is (a) skip stations inside an intersection corner (delegated to the planner's
    /// exclusion spans), (b) skip or NUDGE a station that collides with an already-placed furniture item, and
    /// (c) do both in a FIXED order so the outcome is reproducible (C-06). The cable between poles is treated as
    /// a STRAIGHT segment -- an honest simplification, stated as one: at tens-of-metres spans and street viewing
    /// distance the sag is under the width of the wire mesh, so RM-23's bisection solver would buy nothing a
    /// player can see. </summary>
    public static class DistributionPoleRun
    {
        /// <summary> Resolve the pole stations for one run. Starts from the quantised, intersection-excluded
        /// candidate stations (RM-13's planner), then walks them in ascending order and resolves each against
        /// the furniture already on the strip: a colliding pole is nudged by whole steps -- the search order is
        /// fixed (0, +step, -step, +2*step, -2*step, ...) so it is reproducible -- and dropped only if no offset
        /// within <see cref="DistributionPoleClass.maxNudgeMetres"/> clears both the furniture and the exclusion
        /// spans while staying on the run. </summary>
        public static List<float> PlanPoleStations(float runLength, DistributionPoleClass cls,
            IList<Vector2> intersectionExclusions, IList<float> existingFurniture)
        {
            List<float> result = new List<float>();
            if (cls == null || runLength <= 0f) { return result; }

            List<float> candidates = FurnitureRunPlanner.PlanStations(
                runLength, cls.spanLengthMetres, intersectionExclusions);

            for (int i = 0; i < candidates.Count; i++)
            {
                float resolved;
                if (TryResolve(candidates[i], runLength, cls, intersectionExclusions,
                        existingFurniture, out resolved))
                {
                    result.Add(resolved);
                }
            }
            return result;
        }

        /// <summary> The fixed-order nudge search for one candidate station. Returns the first offset within the
        /// maximum nudge (tried as 0, +step, -step, +2*step, -2*step, ...) that clears every furniture item by
        /// the minimum separation, avoids the intersection exclusions, and stays within [0, runLength]. </summary>
        public static bool TryResolve(float station, float runLength, DistributionPoleClass cls,
            IList<Vector2> intersectionExclusions, IList<float> existingFurniture, out float resolved)
        {
            resolved = station;
            float step = cls.nudgeStepMetres > 0f ? cls.nudgeStepMetres : 0.5f;
            int maxSteps = Mathf.Max(0, Mathf.FloorToInt(cls.maxNudgeMetres / step));

            for (int k = 0; k <= maxSteps; k++)
            {
                // Try +k then -k so the search order is deterministic; k == 0 tries the original once.
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    float d = station + sign * k * step;
                    if (d < 0f || d > runLength) { continue; }
                    if (FurnitureRunPlanner.IsExcluded(d, intersectionExclusions)) { continue; }
                    if (ClearsFurniture(d, existingFurniture, cls.minSeparationFromFurnitureMetres))
                    {
                        resolved = d;
                        return true;
                    }
                    if (k == 0) { break; } // avoid testing the original station twice
                }
            }
            return false;
        }

        /// <summary> Whether a station is at least the minimum separation from every already-placed furniture
        /// item. </summary>
        public static bool ClearsFurniture(float station, IList<float> existingFurniture, float minSeparation)
        {
            if (existingFurniture == null) { return true; }
            for (int i = 0; i < existingFurniture.Count; i++)
            {
                if (Mathf.Abs(station - existingFurniture[i]) < minSeparation) { return false; }
            }
            return true;
        }

        /// <summary> The honest simplification: the cable between two poles is a straight segment, so its height
        /// at a fraction t along the span is a linear interpolation of the two attachment heights. Documented as
        /// a simplification, NOT the catenary -- see the class summary for why the solver is deliberately not
        /// used here. </summary>
        public static float StraightCableHeightAt(float height1, float height2, float t)
        {
            float clamped = Mathf.Clamp01(t);
            return Mathf.Lerp(height1, height2, clamped);
        }
    }
}

#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> RM-13's two genuinely NEW furniture requirements, kept pure so they can be proven without a
    /// scene: EXCLUSION and QUANTISATION. Street furniture -- lamp columns, bins, benches, signs -- is placed
    /// by the existing <c>EdgeObjectMaker</c>, which already spaces objects along a spline at a fixed
    /// interval, so this is NOT a second scatter system. What the existing maker does not do is (1) skip
    /// driveways, crossings, bus stops and junction corners rather than dropping a lamp column into a vehicle
    /// crossover, and (2) land spacing on whole intervals so a run does not end with a half-gap.
    ///
    /// This planner computes the placement stations (arc-distances along the run) that satisfy both; the
    /// existing maker then places a prefab at each. A run is an interval [0, runLength] in metres. Exclusion
    /// spans are [start, end] arc-distance ranges the furniture must avoid. </summary>
    public static class FurnitureRunPlanner
    {
        /// <summary> The placement stations for one furniture run. QUANTISATION: the desired spacing is
        /// rounded to a whole number of equal intervals across the run, so posts sit at both ends and no run
        /// ends with a half-gap; the actual spacing is <c>runLength / intervals</c>, as close to the desired
        /// spacing as a whole count allows. EXCLUSION: any station falling inside an exclusion span is
        /// dropped, so nothing is placed in a driveway, crossing or junction corner. Deterministic: a pure
        /// function of its arguments (C-06). </summary>
        /// <param name="runLength">The run length in metres (&gt;= 0).</param>
        /// <param name="desiredSpacing">The target spacing in metres (&gt; 0).</param>
        /// <param name="exclusionSpans">Arc-distance ranges [start,end] to avoid; may be null.</param>
        public static List<float> PlanStations(float runLength, float desiredSpacing,
            IList<Vector2> exclusionSpans)
        {
            List<float> stations = new List<float>();
            if (runLength <= 0f || desiredSpacing <= 0f) { return stations; }

            // Quantise to a whole number of equal intervals so the run starts and ends on a post.
            int intervals = Mathf.Max(1, Mathf.RoundToInt(runLength / desiredSpacing));
            float actualSpacing = runLength / intervals;

            for (int i = 0; i <= intervals; i++)
            {
                float d = i * actualSpacing;
                if (i == intervals) { d = runLength; } // pin the last post exactly on the end
                if (!IsExcluded(d, exclusionSpans))
                {
                    stations.Add(d);
                }
            }
            return stations;
        }

        /// <summary> The actual spacing a run of this length and desired spacing quantises to -- exposed so a
        /// gate can check it stays close to the desired value. </summary>
        public static float QuantisedSpacing(float runLength, float desiredSpacing)
        {
            if (runLength <= 0f || desiredSpacing <= 0f) { return 0f; }
            int intervals = Mathf.Max(1, Mathf.RoundToInt(runLength / desiredSpacing));
            return runLength / intervals;
        }

        /// <summary> Whether a station falls inside any exclusion span (inclusive of the endpoints, so a post
        /// exactly at a crossing edge is treated as inside and dropped). </summary>
        public static bool IsExcluded(float distance, IList<Vector2> exclusionSpans)
        {
            if (exclusionSpans == null) { return false; }
            for (int i = 0; i < exclusionSpans.Count; i++)
            {
                float a = exclusionSpans[i].x;
                float b = exclusionSpans[i].y;
                if (a > b) { float t = a; a = b; b = t; }
                if (distance >= a - 1e-4f && distance <= b + 1e-4f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

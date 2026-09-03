#region "Imports"
using System;
using RoadCityMaster.CrossSection;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which side of the carriageway a run rides. A roadside barrier is on one side; a median barrier is
    /// RM-60's, not this. </summary>
    public enum RunSide
    {
        Left = 0,
        Right = 1
    }


    /// <summary> The two placement rules every run obeys, in one place so no feature gets them wrong. Lateral offset
    /// is read from the EMITTED cross-section edge at each station -- so a barrier follows a widening shoulder
    /// instead of drifting into it -- and the offset comes from RM-31's <see cref="DividedRoad"/> section, the same
    /// geometry the road actually built. Vertical reference is the ROAD surface, never sampled terrain: this class
    /// takes a road-surface height and adds to it, and holds no terrain reader at all, which is the anti-sink rule
    /// stated as an absence rather than a promise. </summary>
    public static class EdgeRunGeometry
    {
        /// <summary> The signed lateral offset, in metres, of a run at a station: the outboard carriageway edge
        /// from RM-31's emitted section, moved a further <paramref name="extraOffsetMetres"/> away from the road.
        /// Negative is left of the centreline. Because the section is sampled per station, a run at a fixed extra
        /// offset still tracks a shoulder that widens or narrows with station. </summary>
        public static float LateralOffsetAt(DividedRoadProfile profile, float station, RunSide side,
            float extraOffsetMetres)
        {
            DividedSection section = DividedRoad.Section(profile, station);
            if (section.points == null || section.points.Length == 0) { return 0f; }

            int last = section.points.Length - 1;
            float edge = side == RunSide.Left ? section.points[0].lateral : section.points[last].lateral;
            float outward = side == RunSide.Left ? -1f : 1f;
            float extra = extraOffsetMetres > 0f ? extraOffsetMetres : 0f;
            return edge + outward * extra;
        }

        /// <summary> How far the outboard edge itself moves between two stations: positive means the road widened on
        /// that side, so a run keeping the same extra offset moves outboard by this much. Used to prove the run
        /// tracks the emitted edge rather than a constant. </summary>
        public static float EdgeShiftMetres(DividedRoadProfile profile, float fromStation, float toStation,
            RunSide side)
        {
            float a = LateralOffsetAt(profile, fromStation, side, 0f);
            float b = LateralOffsetAt(profile, toStation, side, 0f);
            return Mathf.Abs(b) - Mathf.Abs(a);
        }

        /// <summary> A run point's height: the ROAD surface height at that station plus the run's own vertical
        /// offset (a kerb sits above the surface, a buried rail below it). The terrain height is never consulted --
        /// a run on fill rides the carriageway it protects, not the ground under the embankment. </summary>
        public static float RunPointHeightMetres(float roadSurfaceHeightMetres, float verticalOffsetMetres)
        {
            return roadSurfaceHeightMetres + verticalOffsetMetres;
        }

        /// <summary> A full run point at a station: the emitted-edge lateral offset carried on the road surface. The
        /// caller supplies the centreline world point and the road-surface height; this assembles the run point in
        /// the section's lateral frame (X = right of travel is handled by the caller's basis). </summary>
        public static Vector3 RunPoint(Vector3 centrelineWorld, float lateralOffsetMetres,
            Vector3 rightOfTravelUnit, float roadSurfaceHeightMetres, float verticalOffsetMetres)
        {
            Vector3 lateral = rightOfTravelUnit * lateralOffsetMetres;
            float y = RunPointHeightMetres(roadSurfaceHeightMetres, verticalOffsetMetres);
            return new Vector3(centrelineWorld.x + lateral.x, y, centrelineWorld.z + lateral.z);
        }
    }
}

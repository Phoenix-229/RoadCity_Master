#region "Imports"
using System.Globalization;
#endregion


namespace RoadCityMaster
{
    /// <summary> The acceptance bands the Road component enforces at run time, written ONCE so the inspector and the
    /// runtime cannot disagree about them.
    ///
    /// RM-94 found the two of them contradicting each other. <c>InspectorFieldRegistry</c> declared lane width as
    /// 2.5-5.0 m and cited AASHTO for it, and then <c>Road.UpdateRoad</c> clamped the same field to 0.2-500 m: the
    /// inspector warned about a 6 m lane it went on to build anyway, and a value assigned from script bypassed the
    /// warning entirely. Two limits disagreeing is worse than either being wrong on its own, because neither one is
    /// the answer to "what will this tool accept". Segment resolution had the same split -- 1-20 m declared, 1-50 m
    /// clamped.
    ///
    /// This type is RUNTIME, and the editor registry reads its numbers rather than the other way round, because the
    /// clamp has to run in the runtime assembly and the registry lives in the editor one. Pure arithmetic, no
    /// UnityEngine, so a headless gate can check it. </summary>
    public static class RoadFieldLimits
    {
        // ---- Lane width ----

        /// <summary> Narrowest lane the road model accepts, in metres. </summary>
        public const double LaneWidthMinMetres = 2.5;

        /// <summary> Widest lane the road model accepts, in metres. </summary>
        public const double LaneWidthMaxMetres = 5.0;

        /// <summary> Why the band is where it is. Read by the inspector registry as the field's declared source, so the
        /// warning an author sees and the clamp that runs cite the same sentence. The wording is reproduced in the
        /// generated field tables, so it is kept byte-for-byte as it was. </summary>
        public const string LaneWidthSource =
            "AUTHORED: highway lane 2.7–3.75 m (AASHTO); bound widened for shared/rural lanes";

        // ---- Segment resolution (metres of road per generated mesh segment) ----

        /// <summary> Finest segment length accepted, in metres. Below this the mesh cost climbs with no visible gain. </summary>
        public const double SegmentResolutionMinMetres = 1.0;

        /// <summary> Coarsest segment length accepted, in metres. </summary>
        public const double SegmentResolutionMaxMetres = 20.0;

        public const string SegmentResolutionSource =
            "AUTHORED: metres per generated segment; lower is finer mesh at higher cost";

        // ---- The clamps themselves ----

        /// <summary> The lane width the model will work with, in metres. </summary>
        public static float ClampLaneWidth(float metres)
        {
            return Clamp(metres, LaneWidthMinMetres, LaneWidthMaxMetres);
        }

        /// <summary> The segment length the model will work with, in metres. </summary>
        public static float ClampSegmentResolution(float metres)
        {
            return Clamp(metres, SegmentResolutionMinMetres, SegmentResolutionMaxMetres);
        }

        /// <summary> NaN is clamped to the minimum rather than passed through: every comparison against NaN is false,
        /// so an unguarded clamp returns it unchanged and a single bad assignment then propagates through the whole
        /// cross-section as NaN vertices. </summary>
        private static float Clamp(float value, double min, double max)
        {
            if (float.IsNaN(value)) { return (float)min; }
            if (value < min) { return (float)min; }
            if (value > max) { return (float)max; }
            return value;
        }

        /// <summary> One band as text, for a report that has to name the limit it enforced. </summary>
        public static string Describe(double min, double max)
        {
            return min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture)
                + " m";
        }
    }
}

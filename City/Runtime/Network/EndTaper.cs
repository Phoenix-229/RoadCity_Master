// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The one end-taper operation RM-39 mandates be "written once and used by both": a retaining face
    /// (RM-34), a cut slope (RM-39) and a fill slope all need the identical rule at their extremity -- over a
    /// transition length the structure's DRIVING DIMENSION (face height, or slope height, which walks the catch
    /// point in toward the road) falls linearly to zero, so the structure daylights out of the ground instead of
    /// ending in a free-standing wall of earth. Keeping it in one place is what stops three slightly different
    /// tapers drifting apart. Pure arithmetic, so it is deterministic (C-06). </summary>
    public static class EndTaper
    {
        /// <summary> The taper fraction at a distance from the nearest end: 0 exactly at the end, 1 once past the
        /// transition length, and a straight ramp between. A non-positive transition means no taper (fraction 1
        /// everywhere, i.e. full height right up to the end). A negative distance clamps to the end. </summary>
        public static double FractionAt(double distanceFromEndMetres, double transitionLengthMetres)
        {
            if (transitionLengthMetres <= 0.0) { return 1.0; }
            double t = distanceFromEndMetres / transitionLengthMetres;
            if (t <= 0.0) { return 0.0; }
            if (t >= 1.0) { return 1.0; }
            return t;
        }

        /// <summary> A driving dimension tapered to zero at the end: the full value scaled by the taper fraction.
        /// A non-positive value is already nothing to taper. </summary>
        public static double TaperedDimensionMetres(double fullDimensionMetres, double distanceFromEndMetres,
            double transitionLengthMetres)
        {
            if (fullDimensionMetres <= 0.0) { return 0.0; }
            return fullDimensionMetres * FractionAt(distanceFromEndMetres, transitionLengthMetres);
        }
    }
}

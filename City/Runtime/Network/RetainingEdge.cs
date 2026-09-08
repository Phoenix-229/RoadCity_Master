// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The edge structure a station of elevated road needs, chosen by how high the road sits above the
    /// natural ground. Small fill is held by an earth slope (no structure); past a threshold it needs a retaining
    /// WALL; higher again the road is a VIADUCT whose edge is a parapet and edge beam rather than a wall. Which one
    /// is a HEIGHT-BAND decision, and the wall sub-type (gravity / cantilever / MSE / gabion) is a material+prefab
    /// choice per band, not geometry, so it lives in the Prefab Category Set, not here. </summary>
    public enum EdgeStructure
    {
        EarthSlope = 0,   // no retaining face
        RetainingWall = 1,
        Viaduct = 2
    }


    /// <summary> RM-34's AUTHORED height bands and cap for the elevated-road edge. Values AUTHORED from ordinary
    /// earthworks practice: fill under ~1.5 m is normally an earth slope, a wall holds fill above that, and above
    /// ~6 m the road is treated as a viaduct. The hysteresis keeps the band from flickering where the height sits
    /// near a boundary, and the cap transition is the length over which the face daylights to zero at its ends
    /// (shared with RM-39). </summary>
    [Serializable]
    public sealed class RetainingEdgeClass
    {
        /// <summary> Fill height above which an earth slope is replaced by a retaining wall. </summary>
        public float wallThresholdMetres = 1.5f;

        /// <summary> Fill height above which the wall is replaced by a viaduct edge. </summary>
        public float viaductThresholdMetres = 6f;

        /// <summary> Band-edge hysteresis: the height must move this far past a threshold to switch bands, so a
        /// height hovering at a boundary does not flicker between types. </summary>
        public float hysteresisMetres = 0.25f;

        /// <summary> Length over which the face height tapers to zero at each end, so it daylights into the ground
        /// instead of ending in a free-standing slab (the RM-39 cap). </summary>
        public float capTransitionLengthMetres = 5f;

        public static RetainingEdgeClass Default()
        {
            return new RetainingEdgeClass();
        }
    }

    /// <summary> RM-34's verifiable core: the pure trigger and cap maths for the elevated-road edge, without a
    /// scene. The road's height above natural terrain drives a HEIGHT-BAND selection (earth slope / wall /
    /// viaduct) that is stabilised with hysteresis so it does not flicker at a boundary; each side is evaluated
    /// independently because the ground is rarely level across the road; and the face height is CAPPED to zero
    /// over a transition at each end so it daylights into the ground rather than ending in a slab. The swept face
    /// mesh, the per-band wall prefabs and the terrain sampling itself are the deferred scene half. </summary>
    public static class RetainingEdge
    {
        /// <summary> The road's height above the natural ground at a station: the road (spline) elevation minus the
        /// sampled terrain elevation, floored at zero (a road below grade is a cut, not an elevated edge). </summary>
        public static double HeightAboveTerrainMetres(double roadElevationMetres, double terrainElevationMetres)
        {
            double diff = roadElevationMetres - terrainElevationMetres;
            return diff > 0.0 ? diff : 0.0;
        }

        /// <summary> The band from a bare threshold comparison (no hysteresis) -- flickers near a boundary, kept
        /// only so the stable selector can be compared against it. </summary>
        public static EdgeStructure SelectBand(double heightAboveTerrainMetres, RetainingEdgeClass cls)
        {
            if (heightAboveTerrainMetres >= cls.viaductThresholdMetres) { return EdgeStructure.Viaduct; }
            if (heightAboveTerrainMetres >= cls.wallThresholdMetres) { return EdgeStructure.RetainingWall; }
            return EdgeStructure.EarthSlope;
        }

        /// <summary> The band chosen with hysteresis around the previous band: the height must rise a full
        /// hysteresis past a threshold to move up a band, and fall a full hysteresis below it to move down, so a
        /// height hovering at a boundary holds its band instead of flickering. </summary>
        public static EdgeStructure SelectBandStable(double heightAboveTerrainMetres, EdgeStructure previous,
            RetainingEdgeClass cls)
        {
            double low = cls.wallThresholdMetres;
            double high = cls.viaductThresholdMetres;
            double h = cls.hysteresisMetres;

            bool aboveHigh = previous == EdgeStructure.Viaduct
                ? heightAboveTerrainMetres >= high - h   // stay a viaduct until it falls a hysteresis below
                : heightAboveTerrainMetres >= high + h;  // become a viaduct only a hysteresis above
            bool aboveLow = previous == EdgeStructure.EarthSlope
                ? heightAboveTerrainMetres >= low + h    // become a wall only a hysteresis above
                : heightAboveTerrainMetres >= low - h;   // stay a wall/viaduct until a hysteresis below

            if (aboveHigh) { return EdgeStructure.Viaduct; }
            if (aboveLow) { return EdgeStructure.RetainingWall; }
            return EdgeStructure.EarthSlope;
        }

        /// <summary> Whether a band produces a retaining face at all (a wall or a viaduct edge do; an earth slope
        /// does not, and a road at grade is an earth slope of zero height). </summary>
        public static bool HasRetainingFace(EdgeStructure band)
        {
            return band != EdgeStructure.EarthSlope;
        }

        /// <summary> The face height at a station, capped near the ends: full fill height in the interior, tapering
        /// linearly to zero over the cap transition as the end is approached, so the face daylights into the ground
        /// rather than stopping in mid-air. A non-positive transition means no cap. Delegates to the one shared
        /// end-taper (RM-39) so the wall, the cut slope and the fill slope all daylight the identical way. </summary>
        public static double CappedFaceHeightMetres(double fillHeightMetres, double distanceFromEndMetres,
            RetainingEdgeClass cls)
        {
            return EndTaper.TaperedDimensionMetres(fillHeightMetres, distanceFromEndMetres,
                cls.capTransitionLengthMetres);
        }
    }
}

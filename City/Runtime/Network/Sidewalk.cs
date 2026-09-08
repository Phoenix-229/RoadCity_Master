// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-29's authored sidewalk (footway) dimensions. The straight run is a cross-section swept along an
    /// OFFSET of the road spline (RM-31's variable-width sweep / `SplinatedMeshMaker` already do the sweeping); the
    /// numbers here decide which offset and at what height. Every value is taken from PROWAG (the US Public
    /// Right-of-Way Accessibility Guidelines) and the AASHTO Green Book rather than chosen to look right at editor
    /// zoom. A footway is NOT "a strip beside the road"; these are the tabulated quantities that make it walkable
    /// and let it turn a corner. </summary>
    [Serializable]
    public sealed class SidewalkClass
    {
        /// <summary> Footway (pedestrian access route) width. PROWAG sets a 1.2 m absolute minimum clear width and
        /// recommends 1.5 m; 1.8 m lets two wheelchairs pass and is the common design width. AUTHORED at 1.8 m.
        /// Authored dimensions here are held as double (exact decimals) so the derived offset and heights are the
        /// values the standard states -- float 1.8f = 1.79999995... breached the gate, and once one term is exact
        /// the verge + half-width cancellation the offset relied on breaks unless the whole set is double. </summary>
        public double footwayWidthMetres = 1.8;

        /// <summary> PROWAG minimum clear width the footway may never fall below. </summary>
        public double minFootwayWidthMetres = 1.2;

        /// <summary> Verge/buffer between the kerb face and the footway (planting or furniture strip). NACTO/AASHTO
        /// urban buffers run ~0.5-2 m; AUTHORED at 0.6 m as a narrow but real separation. </summary>
        public double vergeWidthMetres = 0.6;

        /// <summary> Kerb (curb) face height: the footway top sits this far above the carriageway edge. 100-150 mm
        /// is the standard range; AUTHORED at 0.15 m. </summary>
        public double kerbHeightMetres = 0.15;

        /// <summary> Maximum vertical lip at a crossing. PROWAG caps a change in level at a pedestrian crossing at
        /// 13 mm (½ inch); the footway drops to this near-flush kerb where it meets the side road. </summary>
        public double maxCrossingLipMetres = 0.013;

        /// <summary> Carriageway corner (kerb-return) radius the junction is authored with, used when the junction
        /// does not supply its own. AASHTO minimum kerb-return radii for urban corners run ~4.5-7.5 m; AUTHORED at
        /// 5 m. The footway's own return radius is DERIVED from the junction's, never authored independently. </summary>
        public double cornerReturnRadiusMetres = 5;

        public static SidewalkClass Default()
        {
            return new SidewalkClass();
        }
    }


    /// <summary> RM-29's verifiable core: the pure maths of a footway that (1) sits at a fixed offset from the
    /// carriageway edge, (2) inherits the ROAD's vertical profile rather than the terrain's -- the same anti-sink
    /// rule RM-26's terminator follows, from the same cause -- (3) carries around an intersection corner on a
    /// kerb-return arc whose radius is DERIVED from the junction's corner radius (coupled to RM-27/RM-28), (4)
    /// drops to a near-flush kerb at a crossing, and (5) refuses to fold: offsetting a curve inward by more than
    /// its local radius of curvature turns the offset into a self-intersecting loop (a spike / black knot of
    /// triangles), so that degeneracy is DETECTED and reported -- exactly the polygon-offset problem Q-11 names.
    ///
    /// The straight-run sweep, the corner-arc mesh, the crossing geometry and the actual clipping of a detected
    /// self-intersection are the deferred scene half; here we own the offset distances, the heights, the derived
    /// return radius and the fold test. </summary>
    public static class Sidewalk
    {
        /// <summary> Effective footway width, never below the PROWAG minimum clear width. </summary>
        public static double EffectiveFootwayWidthMetres(SidewalkClass cls)
        {
            return Math.Max(cls.footwayWidthMetres, cls.minFootwayWidthMetres);
        }

        /// <summary> Lateral distance from the carriageway edge to the footway CENTRELINE: across the verge and out
        /// to the middle of the footway. This is the offset the cross-section sweep follows. </summary>
        public static double OffsetFromCarriagewayEdgeMetres(SidewalkClass cls)
        {
            return cls.vergeWidthMetres + 0.5 * EffectiveFootwayWidthMetres(cls);
        }

        /// <summary> Footway top height along a straight run: the ROAD's profile height raised by the kerb, NOT the
        /// terrain height. A footway that sampled natural terrain would submerge where the road is raised and float
        /// where it is cut. </summary>
        public static double SidewalkTopHeightMetres(double roadProfileHeightMetres, SidewalkClass cls)
        {
            return roadProfileHeightMetres + cls.kerbHeightMetres;
        }

        /// <summary> Footway height at a crossing: it drops from the kerb to the near-flush lip PROWAG allows, still
        /// referenced to the road profile (not the terrain) so it meets the carriageway cleanly. </summary>
        public static double CrossingDropHeightMetres(double roadProfileHeightMetres, SidewalkClass cls)
        {
            return roadProfileHeightMetres + cls.maxCrossingLipMetres;
        }

        /// <summary> The footway's kerb-return arc radius at an intersection corner, DERIVED from the carriageway's
        /// own corner radius plus the footway offset -- the outer footway is concentric outside the kerb line, so
        /// change the corner and the footway follows. A non-positive carriageway radius falls back to the class
        /// authored radius plus the offset. </summary>
        public static double KerbReturnRadiusMetres(double carriagewayCornerRadiusMetres, SidewalkClass cls)
        {
            double baseRadius = carriagewayCornerRadiusMetres > 0.0
                ? carriagewayCornerRadiusMetres : cls.cornerReturnRadiusMetres;
            return baseRadius + OffsetFromCarriagewayEdgeMetres(cls);
        }

        /// <summary> The outcome of testing an inward offset for the fold that makes a hand-rolled implementation
        /// emit a spike: it is degenerate when the offset distance meets or exceeds the local radius of curvature,
        /// because at that point the offset curve crosses itself. </summary>
        public struct OffsetFoldCheck
        {
            public bool degenerate;
            public double localRadiusMetres;
            public double offsetMetres;
        }

        /// <summary> Test whether offsetting the concave side of a curve inward by <paramref name="offsetMetres"/>
        /// folds the offset over itself. A straight run or an outward offset (non-positive / infinite local radius)
        /// never folds; on a curve it folds once the offset reaches the local radius of curvature. The scene half
        /// clips the self-intersection where this reports degenerate; clamping is not a fix because the clamp shows
        /// as a pinch. </summary>
        public static OffsetFoldCheck CheckInwardOffset(double localRadiusOfCurvatureMetres, double offsetMetres)
        {
            OffsetFoldCheck r = new OffsetFoldCheck();
            r.localRadiusMetres = localRadiusOfCurvatureMetres;
            r.offsetMetres = offsetMetres;
            if (offsetMetres <= 0.0 || localRadiusOfCurvatureMetres <= 0.0
                || double.IsInfinity(localRadiusOfCurvatureMetres))
            {
                r.degenerate = false;
                return r;
            }
            r.degenerate = offsetMetres >= localRadiusOfCurvatureMetres - 1e-9;
            return r;
        }
    }
}

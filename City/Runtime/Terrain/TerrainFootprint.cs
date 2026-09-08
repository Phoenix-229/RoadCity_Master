// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-38's AUTHORED footprint widths. The reported defect is that ONE width drives all three stages
    /// of the terraform, so a narrow road carves a wide flat terrace. The fix separates them: the FLATTENED band
    /// follows the emitted cross-section (RM-31, per station) plus an authored shoulder and nothing more; the
    /// BLEND reaches natural height over its own authored distance with a smoothstep (not a linear ramp, which
    /// leaves a visible crease); and the PAINT width is independent, narrow for a sealed road and wide for an
    /// unsealed one, never wired to the deform width. Values AUTHORED from ordinary road-earthworks practice
    /// (C-15), retunable per road. </summary>
    [Serializable]
    public sealed class TerrainFootprintClass
    {
        /// <summary> Authored shoulder added beyond the emitted cross-section before the ground stops being flat. </summary>
        public float shoulderMetres = 0.5f;

        /// <summary> Distance beyond the flattened edge over which the terrain blends back to natural height. </summary>
        public float blendDistanceMetres = 8f;

        /// <summary> Paint margin beyond the flattened edge for a SEALED road (a narrow verge only). </summary>
        public float sealedPaintMarginMetres = 0.5f;

        /// <summary> Paint margin beyond the flattened edge for an UNSEALED road (a wide dust margin, RM-41). </summary>
        public float unsealedPaintMarginMetres = 4f;

        public static TerrainFootprintClass Default()
        {
            return new TerrainFootprintClass();
        }
    }

    /// <summary> RM-38's verifiable core: the corrected footprint maths, separated into three independent widths
    /// and a creaseless blend, without a scene. The flattened half-width is a FUNCTION of the emitted
    /// cross-section (so RM-31's per-station width flows straight through instead of a per-road constant); the
    /// blend is a smoothstep with zero derivative at both ends evaluated against NATURAL terrain sampled once (the
    /// same build-order determinism trap RM-22's pads have); and the paint width is its own quantity driven by
    /// the surface type. Wiring these into the live threaded writer (ProcessLineHeights / DoRects) and
    /// RoadUtility.MakeSplatMap, and the before/after screenshots, are the deferred scene half. </summary>
    public static class TerrainFootprint
    {
        /// <summary> The flattened half-width at a station: the emitted cross-section half-width (RM-31, varying)
        /// plus the authored shoulder -- and nothing else. This is the single relationship whose being a constant
        /// caused the terrace artefact. </summary>
        public static double FlattenedHalfWidthMetres(double crossSectionHalfWidthMetres, TerrainFootprintClass cls)
        {
            double w = crossSectionHalfWidthMetres + cls.shoulderMetres;
            return w > 0.0 ? w : 0.0;
        }

        /// <summary> The outer edge of the blend: the flattened half-width plus the authored blend distance. </summary>
        public static double BlendOuterHalfWidthMetres(double flattenedHalfWidthMetres, TerrainFootprintClass cls)
        {
            return flattenedHalfWidthMetres + cls.blendDistanceMetres;
        }

        /// <summary> The paint half-width, independent of the deform width: the flattened edge plus the surface
        /// type's own margin (narrow when sealed, wide when unsealed). </summary>
        public static double PaintHalfWidthMetres(double flattenedHalfWidthMetres, bool surfaceSealed,
            TerrainFootprintClass cls)
        {
            double margin = surfaceSealed ? cls.sealedPaintMarginMetres : cls.unsealedPaintMarginMetres;
            return flattenedHalfWidthMetres + margin;
        }

        /// <summary> Smoothstep between two edges: 0 at or below edge0, 1 at or above edge1, and the S-curve
        /// 3t^2 - 2t^3 in between, whose derivative is ZERO at both ends -- so the blend meets both the flat band
        /// and the natural ground tangentially, with no crease. A degenerate (edge1 &lt;= edge0) snaps to 1. </summary>
        public static double Smoothstep(double edge0, double edge1, double x)
        {
            if (edge1 <= edge0) { return x < edge0 ? 0.0 : 1.0; }
            double t = (x - edge0) / (edge1 - edge0);
            if (t <= 0.0) { return 0.0; }
            if (t >= 1.0) { return 1.0; }
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary> The deformed terrain height at a lateral distance from the road centre: the road height flat
        /// out to the flattened edge, then a smoothstep blend to the NATURAL height by the outer blend edge, then
        /// natural height beyond. The natural height must be sampled ONCE from unmodified terrain, not from ground
        /// a previous road already moved, or the result depends on build order. </summary>
        public static double DeformedHeightMetres(double roadHeightMetres, double naturalHeightMetres,
            double lateralDistanceMetres, double flattenedHalfWidthMetres, double blendOuterHalfWidthMetres)
        {
            double d = Math.Abs(lateralDistanceMetres);
            double frac = Smoothstep(flattenedHalfWidthMetres, blendOuterHalfWidthMetres, d);
            return roadHeightMetres + (naturalHeightMetres - roadHeightMetres) * frac;
        }

        /// <summary> Whether the splat is painted at a lateral distance: painted out to the paint half-width and
        /// no farther -- so a sealed road's dirt stops at its narrow verge rather than at the deform width. </summary>
        public static bool IsPaintedAt(double lateralDistanceMetres, double paintHalfWidthMetres)
        {
            return Math.Abs(lateralDistanceMetres) <= paintHalfWidthMetres + 1e-9;
        }
    }
}

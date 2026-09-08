// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How a footprint mask is sampled between pixel centres. Chosen ONCE and RECORDED on the mask, because
    /// nearest and bilinear disagree at the edge and the edge is where lots get accepted or rejected -- two operators
    /// reading the same mask two ways is the non-determinism this field removes. </summary>
    public enum MaskSampling
    {
        Nearest = 0,
        Bilinear = 1
    }

    /// <summary> RM-44's settlement footprint mask: an authored raster over the world's XZ extent saying WHERE a
    /// settlement is. Fixed dimensions and a fixed origin (a serialised asset, NOT a texture reconstructed from brush
    /// strokes at load), a stated metres-per-pixel, and a recorded sampling mode -- so saving, reopening and
    /// rebuilding produce the identical city (C-06). Feathering is a SEPARATE authored width, not a soft brush edge,
    /// because a feathered boundary carries meaning downstream (RM-45 density, RM-47 scatter) and must be reproducible
    /// rather than a consequence of how many times the author dragged. This is an EDIT-TIME asset (§ 10.3); the runtime
    /// loader must not reference it. The Handles brush and the serialised storage format are the deferred editor
    /// half. </summary>
    [Serializable]
    public sealed class FootprintMaskClass
    {
        /// <summary> World X of the mask's min corner (metres). </summary>
        public float originXMetres = 0f;

        /// <summary> World Z of the mask's min corner (metres). </summary>
        public float originZMetres = 0f;

        /// <summary> Ground size of one pixel (metres per pixel), shown in the inspector in metres, not pixels alone,
        /// because a mask smooth at map zoom can be four metres per pixel on the ground. </summary>
        public float metresPerPixel = 4f;

        public int widthPixels = 0;
        public int heightPixels = 0;

        /// <summary> Coverage per pixel (0..1), row-major (index = z * width + x). </summary>
        public float[] coverage = new float[0];

        /// <summary> The recorded sampling mode (see <see cref="MaskSampling"/>). </summary>
        public MaskSampling sampling = MaskSampling.Bilinear;

        /// <summary> Authored feather width in metres: the distance over which coverage ramps from 1 to 0 across a
        /// boundary. A reproducible quantity, independent of brush drag count. </summary>
        public float featherWidthMetres = 8f;

        public FootprintMaskClass() { }

        public FootprintMaskClass(int widthPixels, int heightPixels, float metresPerPixel)
        {
            this.widthPixels = widthPixels < 0 ? 0 : widthPixels;
            this.heightPixels = heightPixels < 0 ? 0 : heightPixels;
            this.metresPerPixel = metresPerPixel > 0f ? metresPerPixel : 1f;
            this.coverage = new float[this.widthPixels * this.heightPixels];
        }
    }

    /// <summary> RM-44's verifiable core: the mask's coordinate, sampling and feather rules, without a scene. The
    /// world extent is reported in metres; painting outside the bounds is CLAMPED, not wrapped; sampling follows the
    /// mask's recorded mode; and the feather is a reproducible ramp over an authored width. The editor brush (paint /
    /// erase / feather in world units via Handles) and the serialised asset format are the deferred half. </summary>
    public static class FootprintMask
    {
        /// <summary> The mask's ground width in metres. </summary>
        public static double WorldWidthMetres(FootprintMaskClass m)
        {
            return m == null ? 0.0 : (double)m.widthPixels * m.metresPerPixel;
        }

        /// <summary> The mask's ground depth in metres. </summary>
        public static double WorldDepthMetres(FootprintMaskClass m)
        {
            return m == null ? 0.0 : (double)m.heightPixels * m.metresPerPixel;
        }

        /// <summary> Whether a world position lies within the mask's ground extent. </summary>
        public static bool IsInsideWorldBounds(FootprintMaskClass m, double worldXMetres, double worldZMetres)
        {
            if (m == null || m.widthPixels <= 0 || m.heightPixels <= 0) { return false; }
            double lx = worldXMetres - m.originXMetres;
            double lz = worldZMetres - m.originZMetres;
            return lx >= 0.0 && lz >= 0.0 && lx <= WorldWidthMetres(m) && lz <= WorldDepthMetres(m);
        }

        /// <summary> Read one pixel with the index clamped to the grid (never wraps). </summary>
        public static float PixelClamped(FootprintMaskClass m, int px, int pz)
        {
            if (m == null || m.widthPixels <= 0 || m.heightPixels <= 0 || m.coverage == null) { return 0f; }
            if (px < 0) { px = 0; } else if (px >= m.widthPixels) { px = m.widthPixels - 1; }
            if (pz < 0) { pz = 0; } else if (pz >= m.heightPixels) { pz = m.heightPixels - 1; }
            int i = pz * m.widthPixels + px;
            return (i >= 0 && i < m.coverage.Length) ? m.coverage[i] : 0f;
        }

        /// <summary> Sample the mask at a world position using its RECORDED sampling mode, clamping outside the bounds
        /// rather than wrapping. </summary>
        public static double Sample(FootprintMaskClass m, double worldXMetres, double worldZMetres)
        {
            if (m == null || m.widthPixels <= 0 || m.heightPixels <= 0) { return 0.0; }
            // Pixel-centre coordinates: pixel [0,0]'s centre sits half a pixel in from the origin corner.
            double fx = (worldXMetres - m.originXMetres) / m.metresPerPixel - 0.5;
            double fz = (worldZMetres - m.originZMetres) / m.metresPerPixel - 0.5;

            if (m.sampling == MaskSampling.Nearest)
            {
                int px = (int)Math.Floor(fx + 0.5);
                int pz = (int)Math.Floor(fz + 0.5);
                return PixelClamped(m, px, pz);
            }

            int x0 = (int)Math.Floor(fx);
            int z0 = (int)Math.Floor(fz);
            double tx = fx - x0;
            double tz = fz - z0;
            double c00 = PixelClamped(m, x0, z0);
            double c10 = PixelClamped(m, x0 + 1, z0);
            double c01 = PixelClamped(m, x0, z0 + 1);
            double c11 = PixelClamped(m, x0 + 1, z0 + 1);
            double top = c00 + (c10 - c00) * tx;
            double bottom = c01 + (c11 - c01) * tx;
            return top + (bottom - top) * tz;
        }

        /// <summary> The feather ramp: coverage as a function of signed distance from the painted boundary (positive
        /// inside, negative outside) over an authored feather width. Reproducible regardless of how the boundary was
        /// drawn -- a zero width is a hard edge (1 inside, 0 outside). </summary>
        public static double FeatherCoverage(double signedDistanceInsideMetres, double featherWidthMetres)
        {
            if (featherWidthMetres <= 0.0)
            {
                return signedDistanceInsideMetres >= 0.0 ? 1.0 : 0.0;
            }
            double half = featherWidthMetres * 0.5;
            double t = (signedDistanceInsideMetres + half) / featherWidthMetres; // -half -> 0, +half -> 1
            if (t <= 0.0) { return 0.0; }
            if (t >= 1.0) { return 1.0; }
            return t;
        }
    }
}

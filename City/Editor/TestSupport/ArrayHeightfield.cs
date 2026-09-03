#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> An <see cref="IHeightfieldTarget"/> made of nothing but arrays, so the watertight rules can be
    /// asserted with no scene, no terrain, no collider and no frame.
    ///
    /// This is not a stand-in for a terrain that would be better tested for real. It is the only way to test the thing
    /// that actually matters: whether the write is ALL-OR-NOTHING and whether the surface it leaves has a step in it.
    /// Against a real terrain both questions come back through the engine's own quantisation, filtering and collider
    /// rebuild, so a failure could always be blamed on one of those; against arrays a failure is the rule being wrong.
    ///
    /// It records what it was told, in order, because the order is half the contract: a transaction must settle exactly
    /// once, whichever way it closes, and "settled twice" and "never settled" are both holes waiting to happen.
    ///
    /// RM-99 also makes it an <see cref="ITerrainSurfaceReader"/>, which is what lets the SAME ground be read down the
    /// tile path and down the provider path so the two can be required to agree. Reading its own arrays back through
    /// <see cref="WatertightTerrainWrite.HeightAtNormalised"/> is deliberate: that is the engine's own bilinear rule, so
    /// the standalone side of the comparison is the surface a collider would present rather than a nearest sample that
    /// would agree with nothing. </summary>
    public sealed class ArrayHeightfield : IHeightfieldTarget, ITerrainSurfaceReader
    {
        private float[,] heights;
        private bool[,] surfacePresent;

        public int applyHeightsCount;
        public int applySurfaceCount;
        public int settleCount;
        public int settleWithHolesCount;

        /// <summary> How many writes the tile turned away because the array's shape did not match its resolution, which
        /// is the one thing a real tile refuses too. Counted rather than thrown: an exception from a test double tells
        /// you where the double is, not where the caller went wrong. </summary>
        public int refusedWrites;

        /// <summary> Where the tile's first sample sits in the world. Present so a world-coordinate probe has something
        /// to convert against; zero unless a test puts the tile somewhere on purpose. </summary>
        public double originX;

        public double originZ;

        /// <summary> Samples taken through <see cref="SampleHeight"/>, so a comparison can prove the surface was
        /// actually asked rather than answered from something the test already held. </summary>
        public int sampleCalls;

        public ArrayHeightfield(int resolution, double heightRangeMetres, double spanMetres)
        {
            HeightmapResolution = resolution;
            HeightRangeMetres = heightRangeMetres;
            SpanMetresX = spanMetres;
            SpanMetresZ = spanMetres;
            heights = new float[resolution, resolution];
            surfacePresent = new bool[resolution - 1, resolution - 1];
            for (int f = 0; f < resolution - 1; f++)
            {
                for (int s = 0; s < resolution - 1; s++) { surfacePresent[f, s] = true; }
            }
        }

        public string TargetName { get { return "array heightfield"; } }

        public int HeightmapResolution { get; private set; }

        public double HeightRangeMetres { get; private set; }

        public double SpanMetresX { get; private set; }

        public double SpanMetresZ { get; private set; }

        public double BaseY { get { return 0.0; } }

        /// <summary> The live surface, for an assertion that wants to read what is actually on the tile. Handed out
        /// directly: a copy would let a test pass while the tile held something else. </summary>
        public float[,] Live { get { return heights; } }

        public bool[,] LiveSurfacePresent { get { return surfacePresent; } }

        /// <summary> Fill the tile with a flat plane at a world elevation, as a starting ground. </summary>
        public void FillFlat(double elevationMetres)
        {
            float v = (float)(elevationMetres / HeightRangeMetres);
            for (int f = 0; f < HeightmapResolution; f++)
            {
                for (int s = 0; s < HeightmapResolution; s++) { heights[f, s] = v; }
            }
        }

        /// <summary> Fill the tile with a hill: a cosine dome centred on the tile, which is the shape a bored tunnel is
        /// supposed to pass under and therefore the shape whose survival is worth asserting. </summary>
        public void FillDome(double baseElevationMetres, double peakRiseMetres)
        {
            int last = HeightmapResolution - 1;
            for (int f = 0; f <= last; f++)
            {
                for (int s = 0; s <= last; s++)
                {
                    double u = (double)f / last * 2.0 - 1.0;
                    double v = (double)s / last * 2.0 - 1.0;
                    double r = Math.Sqrt(u * u + v * v);
                    if (r > 1.0) { r = 1.0; }
                    double rise = peakRiseMetres * 0.5 * (1.0 + Math.Cos(r * Math.PI));
                    heights[f, s] = (float)((baseElevationMetres + rise) / HeightRangeMetres);
                }
            }
        }

        /// <summary> A copy of the current surface, for a test that needs the "before" to compare against after a
        /// roll-back. </summary>
        public float[,] Snapshot()
        {
            float[,] copy = new float[HeightmapResolution, HeightmapResolution];
            Array.Copy(heights, copy, heights.Length);
            return copy;
        }

        public float[,] ReadHeights()
        {
            return Snapshot();
        }

        public bool[,] ReadSurfacePresent()
        {
            int side = HeightmapResolution - 1;
            bool[,] copy = new bool[side, side];
            Array.Copy(surfacePresent, copy, surfacePresent.Length);
            return copy;
        }

        public void ApplyHeights(float[,] incoming)
        {
            if (incoming == null || incoming.GetLength(0) != HeightmapResolution
                || incoming.GetLength(1) != HeightmapResolution)
            {
                refusedWrites++;
                return;
            }
            heights = new float[HeightmapResolution, HeightmapResolution];
            Array.Copy(incoming, heights, incoming.Length);
            applyHeightsCount++;
        }

        public void ApplySurfacePresent(bool[,] incoming)
        {
            if (incoming == null) { refusedWrites++; return; }
            int side = HeightmapResolution - 1;
            if (incoming.GetLength(0) != side || incoming.GetLength(1) != side) { refusedWrites++; return; }
            surfacePresent = new bool[side, side];
            Array.Copy(incoming, surfacePresent, incoming.Length);
            applySurfaceCount++;
        }

        public void Settle(bool holesChanged)
        {
            settleCount++;
            if (holesChanged) { settleWithHolesCount++; }
        }

        /// <summary> The world elevation at a sample, in metres, for readable assertions. </summary>
        public double ElevationAt(int first, int second)
        {
            return heights[first, second] * HeightRangeMetres;
        }

        #region "Reading it as a surface (RM-99)"
        /// <summary> Fill the tile with a plane, which is the one ground whose height at an arbitrary world point can be
        /// stated in closed form. That matters for the dual-mode comparison: a provider computing the plane directly and
        /// a tile interpolating it from samples must agree, and any disagreement is therefore the PATH rather than the
        /// shape. Bilinear interpolation reproduces a plane exactly, so what is left is the tile's 16-bit quantisation,
        /// which is exactly the size the probe tolerance is set against. </summary>
        public void FillPlane(double perMetreX, double perMetreZ, double atOriginMetres)
        {
            int last = HeightmapResolution - 1;
            for (int f = 0; f <= last; f++)
            {
                double worldZ = originZ + SpanMetresZ * ((double)f / last);
                for (int s = 0; s <= last; s++)
                {
                    double worldX = originX + SpanMetresX * ((double)s / last);
                    double y = atOriginMetres + perMetreX * worldX + perMetreZ * worldZ;
                    heights[f, s] = (float)(y / HeightRangeMetres);
                }
            }
        }

        /// <summary> The surface at a world point, through the engine's own bilinear rule.
        ///
        /// Outside the tile the answer is not-a-number rather than the base of the world. A tile that answered zero off
        /// its own edge would make every probe past the boundary read as a several-hundred-metre cliff, and a continuity
        /// check would then fail for a reason that has nothing to do with the write being tested. Not-a-number is the
        /// same "no answer here" the rest of the tool uses, and the probe counts it instead of measuring it. </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            sampleCalls++;
            double u = (worldX - originX) / SpanMetresX;
            double v = (worldZ - originZ) / SpanMetresZ;
            if (u < 0.0 || u > 1.0 || v < 0.0 || v > 1.0) { return float.NaN; }
            return (float)WatertightTerrainWrite.HeightAtNormalised(heights, u, v, HeightRangeMetres, BaseY);
        }

        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            // One sample spacing either side, which is the finest slope the tile actually holds; a smaller step would
            // only be measuring the interpolation.
            double step = SpanMetresX / (HeightmapResolution - 1);
            float left = SampleHeight((float)(worldX - step), worldZ);
            float right = SampleHeight((float)(worldX + step), worldZ);
            float back = SampleHeight(worldX, (float)(worldZ - step));
            float forward = SampleHeight(worldX, (float)(worldZ + step));
            if (float.IsNaN(left) || float.IsNaN(right) || float.IsNaN(back) || float.IsNaN(forward))
            {
                return Vector3.up;
            }
            Vector3 n = new Vector3((float)-((right - left) / (2.0 * step)), 1f,
                (float)-((forward - back) / (2.0 * step)));
            return n.normalized;
        }

        /// <summary> The one chunk this tile is, so a region check has a declared extent to compare against. </summary>
        public List<Bounds> GetActiveChunkBounds()
        {
            List<Bounds> bounds = new List<Bounds>();
            Vector3 centre = new Vector3((float)(originX + SpanMetresX * 0.5), (float)(HeightRangeMetres * 0.5),
                (float)(originZ + SpanMetresZ * 0.5));
            bounds.Add(new Bounds(centre, new Vector3((float)SpanMetresX, (float)HeightRangeMetres,
                (float)SpanMetresZ)));
            return bounds;
        }
        #endregion
    }
}
#endif

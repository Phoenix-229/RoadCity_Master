// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-98. The rules that make a terrain modification watertight, written as functions over arrays so
    /// that every one of them can be asserted headless -- no scene, no collider, no frame.
    ///
    /// The author's report is "the terrain gets a hole", and that single symptom has four distinct causes, which is
    /// why they are all handled here rather than patched separately:
    ///
    /// 1. The drawn surface and the collided surface disagree, because heights were written and nothing re-established
    ///    the collider. Cured by <c>Settle</c> on the sink -- see <see cref="ITerrainWriteSink"/> -- which every
    ///    transaction calls exactly once when it closes, whichever way it closes.
    /// 2. A hole flag left set by an operation that was cancelled or superseded. The flags are per-quad and INVERTED
    ///    (true means surface present), so a stale <c>false</c> is a genuine hole in collision with nothing drawn to
    ///    explain it. Cured by sealing the flags on roll-back, and by <see cref="AnyHoleOpen"/> being asserted after
    ///    every interrupted build.
    /// 3. A region written without the neighbourhood the engine needs to close the seam. The boundary sample is shared
    ///    by the written region and the ground outside it; if the write stops AT that sample, the sample belongs to
    ///    whichever pass ran last, and a hairline crack that behaves like a narrow chasm appears along it. Cured by
    ///    <see cref="NeighbourhoodOf"/>: the write always extends one sample past what it changed, so the shared
    ///    sample is interior to the write rather than on its edge.
    /// 4. A cut or fill face that never reaches existing ground, leaving a vertical wall whose foot has no ground at
    ///    all. Cured by <see cref="BlendPerimeter"/> plus <see cref="RequiredBlendSamples"/>: the band that carries
    ///    the written surface back to untouched ground is not a fixed cosmetic three samples, it is however many
    ///    samples the drop actually needs to stay under the step limit.
    ///
    /// Nothing here reads or writes <c>TerrainData</c>. The one adapter that does is <see cref="TerrainTileTarget"/>
    /// at the bottom of this file, and its writes go through the gateway's sink like every other write in the tool, so
    /// RM-57's search still finds exactly one owner. </summary>
    public static class WatertightTerrainWrite
    {
        #region "Sample geometry"
        /// <summary> Metres between adjacent heightmap samples. The span is <c>resolution - 1</c> intervals, never
        /// <c>resolution</c> -- dividing by the sample count is the classic off-by-one that puts every computed
        /// region one sample short of where it should end. </summary>
        public static double SampleSpacingMetres(double spanMetres, int resolution)
        {
            if (resolution < 2) { return 0.0; }
            return spanMetres / (resolution - 1);
        }

        /// <summary> The steepest one-sample step a write may introduce, in metres.
        ///
        /// This is a SLOPE limit expressed at the terrain's own sample spacing, not a fixed height, because a fixed
        /// height cannot be right for two terrains: half a metre across a 1 m sample is a wall, and across a 20 m
        /// sample it is a gentle bank. The gradient comes from the AASHTO Roadside Design Guide's slope
        /// classification -- slopes steeper than 1:3 are CRITICAL, meaning a vehicle leaving the road cannot traverse
        /// them, whereas 1:3 and flatter are traversable. A seam at or below 1:3 is therefore a slope; anything
        /// steeper is the invisible wall this task exists to eliminate. </summary>
        public static double MaximumSeamStepMetres(double spacingMetres, WatertightWriteClass cls)
        {
            double gradient = cls != null ? cls.maximumSeamGradient : 1.0 / 3.0;
            if (gradient <= 0.0) { return 0.0; }
            return spacingMetres * gradient;
        }

        /// <summary> How many blend rings a given drop needs so that no single step exceeds the limit.
        ///
        /// A linear ramp over <c>b</c> rings moves the surface by <c>delta / (b + 1)</c> per sample, so the condition
        /// is <c>delta / (b + 1) &lt;= step</c>, giving <c>b &gt;= delta / step - 1</c>. The configured band is a
        /// floor, not the answer: a 4 m cut on a 1 m-spacing terrain needs eleven rings and gets them. </summary>
        public static int RequiredBlendSamples(double worstDeltaMetres, double spacingMetres,
            WatertightWriteClass cls)
        {
            int floor = cls != null ? cls.perimeterBlendSamples : 3;
            if (floor < 0) { floor = 0; }
            double step = MaximumSeamStepMetres(spacingMetres, cls);
            if (step <= 0.0) { return floor; }
            double delta = Math.Abs(worstDeltaMetres);
            int needed = (int)Math.Ceiling(delta / step) - 1;
            if (needed < 0) { needed = 0; }
            return needed > floor ? needed : floor;
        }
        #endregion


        #region "Windows"
        /// <summary> The write window grown by the skirt and the blend band, clamped to the heightmap.
        ///
        /// <paramref name="blendSamples"/> is what <see cref="RequiredBlendSamples"/> returned, and the skirt is the
        /// extra sample that puts the shared boundary sample inside the write. Clamping is not a failure: at a tile
        /// edge the neighbouring tile continues the surface, and that seam is the linker's business (RM-99), not this
        /// function's -- <see cref="HeightSampleWindow.clampedAtEdge"/> records that it happened so a caller can say
        /// so instead of guessing. </summary>
        public static HeightSampleWindow NeighbourhoodOf(HeightSampleWindow written, int blendSamples,
            int resolution, WatertightWriteClass cls)
        {
            int skirt = cls != null ? cls.neighbourhoodSkirtSamples : 1;
            if (skirt < 0) { skirt = 0; }
            if (blendSamples < 0) { blendSamples = 0; }
            return written.ExpandedBy(skirt + blendSamples, resolution);
        }

        /// <summary> The largest height difference, in metres, between two arrays over one window. This is the drop
        /// the blend band has to absorb, and it is measured rather than assumed. </summary>
        public static double WorstDeltaMetres(float[,] written, float[,] original, HeightSampleWindow window,
            double heightRangeMetres)
        {
            if (written == null || original == null) { return 0.0; }
            double worst = 0.0;
            for (int f = window.firstMin; f <= window.firstMax; f++)
            {
                for (int s = window.secondMin; s <= window.secondMax; s++)
                {
                    double d = Math.Abs((double)written[f, s] - (double)original[f, s]) * heightRangeMetres;
                    if (d > worst) { worst = d; }
                }
            }
            return worst;
        }
        #endregion


        #region "The perimeter blend"
        /// <summary> Carry the written surface OUTWARD into the blend band, in place, so there is something for
        /// <see cref="BlendPerimeter"/> to ramp back down.
        ///
        /// This step is not decoration and leaving it out is silent: a conform worker writes the road's surface into the
        /// corridor cells and leaves every other sample holding untouched ground, so in the band the written and the
        /// original arrays are IDENTICAL. Blending identical values produces identical values -- the band comes out as
        /// ground, the corridor comes out as road, and the full drop still lands on the one sample between them. The face
        /// that is supposed to reach existing ground never gets built, and the write looks blended while being exactly as
        /// sharp as before.
        ///
        /// So the road's own edge value is first carried out across the band. Each sample outside
        /// <paramref name="changed"/> takes the value of the nearest sample ON its boundary, found by clamping the
        /// coordinates into it -- the closest point of a rectangle to an outside point is its clamp, so this is nearest
        /// neighbour rather than an approximation of it. That leaves a plateau at road level across the band, which
        /// <see cref="BlendPerimeter"/> then tilts down to the untouched ground at the outer ring. Nothing inside
        /// <paramref name="changed"/> is read after it is written and nothing inside it is written at all, so the road's
        /// conformed surface is carried outward without being disturbed. </summary>
        public static void ExtendOutward(float[,] written, HeightSampleWindow changed,
            HeightSampleWindow neighbourhood)
        {
            if (written == null || !changed.HasSamples) { return; }
            for (int f = neighbourhood.firstMin; f <= neighbourhood.firstMax; f++)
            {
                for (int s = neighbourhood.secondMin; s <= neighbourhood.secondMax; s++)
                {
                    if (changed.Contains(f, s)) { continue; }
                    int cf = f < changed.firstMin ? changed.firstMin : (f > changed.firstMax ? changed.firstMax : f);
                    int cs = s < changed.secondMin ? changed.secondMin
                        : (s > changed.secondMax ? changed.secondMax : s);
                    written[f, s] = written[cf, cs];
                }
            }
        }

        /// <summary> Carry the written surface back to untouched ground across the outer rings of
        /// <paramref name="neighbourhood"/>, in place.
        ///
        /// The outermost ring ends up EXACTLY equal to the original ground -- weight zero, not "nearly zero" -- which
        /// is what makes the seam disappear rather than shrink: there is no discontinuity at the boundary of the
        /// write because the boundary sample was not changed at all. Inward from there the weight rises linearly, one
        /// ring at a time, until the surface is fully the written one. Samples nearer the middle than the band are
        /// untouched by this call, so a road's own conformed surface is never softened.
        ///
        /// Both arrays are indexed [first = z, second = x], the engine's own convention. </summary>
        public static void BlendPerimeter(float[,] written, float[,] original, HeightSampleWindow neighbourhood,
            int blendSamples)
        {
            if (written == null || original == null) { return; }
            if (blendSamples < 0) { blendSamples = 0; }
            int rings = blendSamples + 1;

            for (int f = neighbourhood.firstMin; f <= neighbourhood.firstMax; f++)
            {
                for (int s = neighbourhood.secondMin; s <= neighbourhood.secondMax; s++)
                {
                    int depth = neighbourhood.DepthFromEdge(f, s);
                    if (depth > blendSamples) { continue; }
                    double weight = (double)depth / rings;
                    double o = original[f, s];
                    written[f, s] = (float)(o + (written[f, s] - o) * weight);
                }
            }
        }

        /// <summary> The worst one-sample step in the DIFFERENCE between the written and the original surface, over a
        /// window and one sample outside it, in metres.
        ///
        /// The difference field is the honest thing to measure: the ground's own slope is not the write's fault, and
        /// subtracting it leaves exactly what the write introduced. A result at or below
        /// <see cref="MaximumSeamStepMetres"/> means no wall was created anywhere on the boundary, whatever the
        /// terrain underneath was doing. </summary>
        public static double WorstIntroducedStepMetres(float[,] written, float[,] original,
            HeightSampleWindow window, double heightRangeMetres, out int atFirst, out int atSecond)
        {
            atFirst = -1;
            atSecond = -1;
            if (written == null || original == null) { return 0.0; }

            int firstCount = written.GetLength(0);
            int secondCount = written.GetLength(1);
            double worst = 0.0;

            for (int f = window.firstMin; f <= window.firstMax; f++)
            {
                for (int s = window.secondMin; s <= window.secondMax; s++)
                {
                    double here = (written[f, s] - original[f, s]) * heightRangeMetres;
                    if (f + 1 < firstCount)
                    {
                        double there = (written[f + 1, s] - original[f + 1, s]) * heightRangeMetres;
                        double step = Math.Abs(there - here);
                        if (step > worst) { worst = step; atFirst = f; atSecond = s; }
                    }
                    if (s + 1 < secondCount)
                    {
                        double there = (written[f, s + 1] - original[f, s + 1]) * heightRangeMetres;
                        double step = Math.Abs(there - here);
                        if (step > worst) { worst = step; atFirst = f; atSecond = s; }
                    }
                }
            }
            return worst;
        }

        /// <summary> The worst one-sample step in the finished surface itself, for the report. Not gated: a cliff that
        /// was already there is the world's, not the road's. </summary>
        public static double WorstAbsoluteStepMetres(float[,] heights, HeightSampleWindow window,
            double heightRangeMetres)
        {
            if (heights == null) { return 0.0; }
            int firstCount = heights.GetLength(0);
            int secondCount = heights.GetLength(1);
            double worst = 0.0;
            for (int f = window.firstMin; f <= window.firstMax; f++)
            {
                for (int s = window.secondMin; s <= window.secondMax; s++)
                {
                    if (f + 1 < firstCount)
                    {
                        double step = Math.Abs(heights[f + 1, s] - heights[f, s]) * heightRangeMetres;
                        if (step > worst) { worst = step; }
                    }
                    if (s + 1 < secondCount)
                    {
                        double step = Math.Abs(heights[f, s + 1] - heights[f, s]) * heightRangeMetres;
                        if (step > worst) { worst = step; }
                    }
                }
            }
            return worst;
        }
        #endregion


        #region "Hole flags"
        /// <summary> True when any quad in the map is a hole. The engine's convention is inverted -- <c>true</c> means
        /// the surface is PRESENT -- so the thing being looked for is a <c>false</c>. </summary>
        public static bool AnyHoleOpen(bool[,] surfacePresent)
        {
            if (surfacePresent == null) { return false; }
            for (int f = 0; f < surfacePresent.GetLength(0); f++)
            {
                for (int s = 0; s < surfacePresent.GetLength(1); s++)
                {
                    if (!surfacePresent[f, s]) { return true; }
                }
            }
            return false;
        }

        /// <summary> How many quads are holes, for a report that says how bad rather than only whether. </summary>
        public static int CountHolesOpen(bool[,] surfacePresent)
        {
            if (surfacePresent == null) { return 0; }
            int count = 0;
            for (int f = 0; f < surfacePresent.GetLength(0); f++)
            {
                for (int s = 0; s < surfacePresent.GetLength(1); s++)
                {
                    if (!surfacePresent[f, s]) { count++; }
                }
            }
            return count;
        }

        /// <summary> Close every hole in the map, in place.
        ///
        /// Deliberately NOT what roll-back does: roll-back restores the snapshot, which closes what the cancelled build
        /// opened while leaving an older tunnel mouth alone. This is the primitive for a caller that has established
        /// there should be no opening at all -- a fresh tile, or a region whose tunnel has been deleted. </summary>
        public static void SealAll(bool[,] surfacePresent)
        {
            if (surfacePresent == null) { return; }
            for (int f = 0; f < surfacePresent.GetLength(0); f++)
            {
                for (int s = 0; s < surfacePresent.GetLength(1); s++)
                {
                    surfacePresent[f, s] = true;
                }
            }
        }

        /// <summary> The quad a normalised position falls in. The holes map is one smaller than the heightmap per
        /// axis, so a probe at the far edge belongs to the last quad rather than to one past it. </summary>
        public static bool SurfacePresentAtNormalised(bool[,] surfacePresent, double normalisedX,
            double normalisedZ)
        {
            if (surfacePresent == null) { return true; }
            int firstCount = surfacePresent.GetLength(0);
            int secondCount = surfacePresent.GetLength(1);
            int f = (int)(normalisedZ * firstCount);
            int s = (int)(normalisedX * secondCount);
            if (f < 0) { f = 0; }
            if (s < 0) { s = 0; }
            if (f >= firstCount) { f = firstCount - 1; }
            if (s >= secondCount) { s = secondCount - 1; }
            return surfacePresent[f, s];
        }
        #endregion


        #region "Probing the surface without a scene"
        /// <summary> The height the heightmap reports at a normalised position, in world metres, by the same bilinear
        /// interpolation the engine uses between samples.
        ///
        /// This exists so the probe sweep can run headless. In a real scene the sweep drops a physics probe and
        /// compares where it lands with this number; with no scene it compares the array against itself through the
        /// same path a probe would take, which still catches a torn region, a stale perimeter and a hole. </summary>
        public static double HeightAtNormalised(float[,] heights, double normalisedX, double normalisedZ,
            double heightRangeMetres, double baseY)
        {
            if (heights == null) { return baseY; }
            int firstCount = heights.GetLength(0);
            int secondCount = heights.GetLength(1);
            if (firstCount < 2 || secondCount < 2) { return baseY; }

            double fz = Clamp01(normalisedZ) * (firstCount - 1);
            double fx = Clamp01(normalisedX) * (secondCount - 1);
            int f0 = (int)Math.Floor(fz);
            int s0 = (int)Math.Floor(fx);
            if (f0 > firstCount - 2) { f0 = firstCount - 2; }
            if (s0 > secondCount - 2) { s0 = secondCount - 2; }
            double tz = fz - f0;
            double tx = fx - s0;

            double h00 = heights[f0, s0];
            double h01 = heights[f0, s0 + 1];
            double h10 = heights[f0 + 1, s0];
            double h11 = heights[f0 + 1, s0 + 1];
            double lower = h00 + (h01 - h00) * tx;
            double upper = h10 + (h11 - h10) * tx;
            return baseY + (lower + (upper - lower) * tz) * heightRangeMetres;
        }

        /// <summary> The height quantisation the engine imposes, in metres. Heights are stored as 16-bit normalised
        /// values, so no comparison against a heightmap can be tighter than one quantum however carefully it is
        /// computed -- which is where <see cref="WatertightWriteClass.probeToleranceMetres"/> comes from rather than
        /// from taste. </summary>
        public static double HeightQuantumMetres(double heightRangeMetres)
        {
            return heightRangeMetres / 65535.0;
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) { return 0.0; }
            if (v > 1.0) { return 1.0; }
            return v;
        }
        #endregion
    }


    /// <summary> An inclusive window of heightmap samples, indexed the engine's way: <c>first</c> runs along world Z
    /// and <c>second</c> along world X. A window rather than a rectangle of world metres, because everything watertight
    /// is decided at sample granularity -- the seam, the skirt, the blend band and the step limit all count samples.
    /// </summary>
    public struct HeightSampleWindow
    {
        public int firstMin;
        public int firstMax;
        public int secondMin;
        public int secondMax;

        /// <summary> True when growing this window ran into the edge of the heightmap, so a caller can say "the seam
        /// at the tile edge is the neighbour's" instead of pretending it blended. </summary>
        public bool clampedAtEdge;

        public static HeightSampleWindow FromBounds(int firstMin, int firstMax, int secondMin, int secondMax)
        {
            HeightSampleWindow w;
            w.firstMin = Math.Min(firstMin, firstMax);
            w.firstMax = Math.Max(firstMin, firstMax);
            w.secondMin = Math.Min(secondMin, secondMax);
            w.secondMax = Math.Max(secondMin, secondMax);
            w.clampedAtEdge = false;
            return w;
        }

        /// <summary> No samples at all, and detectably so.
        ///
        /// <see cref="FromBounds"/> cannot express this: it sorts its arguments, so even a deliberately backwards pair
        /// comes back as a one-sample window and <see cref="HasSamples"/> is true. A caller that found nothing to write
        /// therefore needs a value it can hand on and have recognised -- otherwise "the worker changed no cells" arrives
        /// as "the worker changed the sample at the tile origin", which is a write nobody asked for. The fields are set
        /// directly, past the sorting, which is the only reason this is not simply another FromBounds call. </summary>
        public static HeightSampleWindow None
        {
            get
            {
                HeightSampleWindow w;
                w.firstMin = 0;
                w.firstMax = -1;
                w.secondMin = 0;
                w.secondMax = -1;
                w.clampedAtEdge = false;
                return w;
            }
        }

        /// <summary> The whole heightmap of a square array. </summary>
        public static HeightSampleWindow Whole(int resolution)
        {
            return FromBounds(0, resolution - 1, 0, resolution - 1);
        }

        public int FirstCount { get { return firstMax - firstMin + 1; } }

        public int SecondCount { get { return secondMax - secondMin + 1; } }

        public int SampleCount { get { return FirstCount * SecondCount; } }

        public bool HasSamples { get { return FirstCount > 0 && SecondCount > 0; } }

        public bool Contains(int first, int second)
        {
            return first >= firstMin && first <= firstMax && second >= secondMin && second <= secondMax;
        }

        /// <summary> How many samples in from this window's own edge a sample sits: 0 on the outer ring, 1 on the next
        /// ring in, and so on. This is what the blend weight is a function of. </summary>
        public int DepthFromEdge(int first, int second)
        {
            int a = first - firstMin;
            int b = firstMax - first;
            int c = second - secondMin;
            int d = secondMax - second;
            int min = a < b ? a : b;
            if (c < min) { min = c; }
            if (d < min) { min = d; }
            return min;
        }

        /// <summary> This window grown by <paramref name="samples"/> on every side and clamped to
        /// <c>[0, resolution - 1]</c>, remembering whether the clamp bit. </summary>
        public HeightSampleWindow ExpandedBy(int samples, int resolution)
        {
            if (samples < 0) { samples = 0; }
            int lastIndex = resolution - 1;
            HeightSampleWindow w;
            w.firstMin = firstMin - samples;
            w.firstMax = firstMax + samples;
            w.secondMin = secondMin - samples;
            w.secondMax = secondMax + samples;
            w.clampedAtEdge = clampedAtEdge
                || w.firstMin < 0 || w.secondMin < 0 || w.firstMax > lastIndex || w.secondMax > lastIndex;
            if (w.firstMin < 0) { w.firstMin = 0; }
            if (w.secondMin < 0) { w.secondMin = 0; }
            if (w.firstMax > lastIndex) { w.firstMax = lastIndex; }
            if (w.secondMax > lastIndex) { w.secondMax = lastIndex; }
            return w;
        }

        public string Describe()
        {
            return string.Format("samples z[{0}..{1}] x[{2}..{3}]{4}", firstMin, firstMax, secondMin, secondMax,
                clampedAtEdge ? " (clamped at the tile edge)" : string.Empty);
        }
    }


    /// <summary> The dials the watertight path reads. Every one has a reason stated beside it, and every default is
    /// derived from something outside this file rather than chosen because it looked about right. </summary>
    [Serializable]
    public class WatertightWriteClass
    {
        /// <summary> Extra samples written past the region that actually changed, so the sample SHARED by the write
        /// and the ground outside it is interior to the write. One is enough and one is necessary: the seam is exactly
        /// one sample wide. </summary>
        public int neighbourhoodSkirtSamples = 1;

        /// <summary> The minimum blend band. A larger drop gets a wider band automatically
        /// (<see cref="WatertightTerrainWrite.RequiredBlendSamples"/>); this is only the floor, so that even a
        /// millimetre of conform still lands softly instead of as a visible ring. </summary>
        public int perimeterBlendSamples = 3;

        /// <summary> The steepest seam a write may introduce, as a slope. 1:3 is where the AASHTO Roadside Design
        /// Guide's classification turns from traversable to critical, so it is the ceiling for a face the tool creates
        /// on purpose. </summary>
        public double maximumSeamGradient = 1.0 / 3.0;

        /// <summary> How far a probe may land from the height the heightmap reports before it counts as a
        /// disagreement. Two 16-bit quanta on a 600 m height range is about 18 mm, and the collider is built from the
        /// same quantised samples, so anything tighter would be testing the storage format rather than the write.
        /// </summary>
        public double probeToleranceMetres = 0.02;

        /// <summary> Probe spacing for the verification sweep, in metres. Half a metre puts several probes across the
        /// narrowest thing the tool builds -- a footway -- so a tear cannot hide between two probes. </summary>
        public double probeSpacingMetres = 0.5;

        /// <summary> Whether a roll-back re-applies the hole snapshot even when this transaction never touched holes.
        /// It does, as a guard against another path having opened one while the build was in flight -- a cancelled
        /// build that leaves an opening behind is precisely the reported defect.
        ///
        /// Note what this does NOT do: it does not seal every hole. It restores the map that was there when the
        /// transaction opened, so a tunnel mouth cut by an EARLIER build survives a later build's cancellation. Sealing
        /// unconditionally would fix the reported hole by destroying a feature, which is the wrong trade. </summary>
        public bool restoreHolesOnRollBack = true;

        public static WatertightWriteClass Default()
        {
            return new WatertightWriteClass();
        }
    }


    /// <summary> One terrain tile, seen only as a heightfield that can be read, written and settled. The transaction
    /// talks to this and never to the engine, which is what lets the whole all-or-nothing story be asserted over plain
    /// arrays in a headless gate. </summary>
    public interface IHeightfieldTarget
    {
        /// <summary> A name for the report. </summary>
        string TargetName { get; }

        /// <summary> Samples per side of the heightmap. </summary>
        int HeightmapResolution { get; }

        /// <summary> The tile's vertical range in metres -- what a normalised height of 1.0 means. </summary>
        double HeightRangeMetres { get; }

        /// <summary> The tile's world span in metres along X and Z. </summary>
        double SpanMetresX { get; }

        double SpanMetresZ { get; }

        /// <summary> World Y of a normalised height of zero. </summary>
        double BaseY { get; }

        /// <summary> The whole heightfield, as a fresh array the caller may keep. </summary>
        float[,] ReadHeights();

        /// <summary> The whole hole map (one smaller per axis), as a fresh array. </summary>
        bool[,] ReadSurfacePresent();

        void ApplyHeights(float[,] heights);

        void ApplySurfacePresent(bool[,] surfacePresent);

        /// <summary> Make the drawn surface, the collided surface and what was just written agree. Called exactly once
        /// per transaction, on commit AND on roll-back. </summary>
        void Settle(bool holesChanged);
    }


    /// <summary> All-or-nothing terrain writing: snapshot, apply, then either commit or put it back exactly as it was.
    ///
    /// This is stated as a prerequisite for RM-99's asynchronous work rather than as a nicety, because once a build can
    /// be cancelled mid-flight, interruption stops being exceptional. An author who cancels a road halfway, or who
    /// edits a node while a refresh is in flight, must not be able to leave a permanent hole -- and "half-written" is
    /// the only state from which a hole survives. </summary>
    public sealed class TerrainWriteTransaction
    {
        public enum TransactionState
        {
            Open = 0,
            Committed = 1,
            RolledBack = 2
        }

        private readonly IHeightfieldTarget target;
        private readonly float[,] heightsBefore;
        private readonly bool[,] surfaceBefore;
        private readonly string requesterId;
        private bool appliedHeights;
        private bool appliedHoles;
        private TransactionState state = TransactionState.Open;

        private TerrainWriteTransaction(IHeightfieldTarget target, string requesterId, float[,] heightsBefore,
            bool[,] surfaceBefore)
        {
            this.target = target;
            this.requesterId = requesterId != null ? requesterId : string.Empty;
            this.heightsBefore = heightsBefore;
            this.surfaceBefore = surfaceBefore;
        }

        /// <summary> Snapshot a tile and open a transaction over it. The snapshot is a copy, not a reference: the
        /// caller is about to modify the array it was handed. </summary>
        public static TerrainWriteTransaction Open(IHeightfieldTarget target, string requesterId)
        {
            if (target == null) { return null; }
            return new TerrainWriteTransaction(target, requesterId, target.ReadHeights(),
                target.ReadSurfacePresent());
        }

        public TransactionState State { get { return state; } }

        public string RequesterId { get { return requesterId; } }

        public bool IsOpen { get { return state == TransactionState.Open; } }

        /// <summary> True when anything at all reached the tile, so an abort knows whether there is anything to undo.
        /// </summary>
        public bool Applied { get { return appliedHeights || appliedHoles; } }

        /// <summary> The snapshot, for a caller that needs the original ground to blend against. Handed out directly
        /// rather than copied, because the blend reads it thousands of times per road; it must not be modified.
        /// </summary>
        public float[,] HeightsBefore { get { return heightsBefore; } }

        public bool[,] SurfacePresentBefore { get { return surfaceBefore; } }

        public void ApplyHeights(float[,] heights)
        {
            if (state != TransactionState.Open || heights == null) { return; }
            target.ApplyHeights(heights);
            appliedHeights = true;
        }

        public void ApplySurfacePresent(bool[,] surfacePresent)
        {
            if (state != TransactionState.Open || surfacePresent == null) { return; }
            target.ApplySurfacePresent(surfacePresent);
            appliedHoles = true;
        }

        /// <summary> Keep what was written, and settle the tile so physics matches it. </summary>
        public void Commit()
        {
            if (state != TransactionState.Open) { return; }
            state = TransactionState.Committed;
            target.Settle(appliedHoles);
        }

        /// <summary> Put the tile back exactly as it was found and settle, so physics matches the restored ground.
        ///
        /// The snapshot is re-applied whole rather than by region on purpose: a partial write that threw halfway may
        /// have touched samples nobody recorded, and the only region that is certainly correct is all of it. The hole
        /// map is restored the same way, which closes anything this build opened while leaving an older tunnel mouth
        /// exactly where it was -- see <see cref="WatertightWriteClass.restoreHolesOnRollBack"/>. </summary>
        public void RollBack(WatertightWriteClass cls)
        {
            if (state != TransactionState.Open) { return; }
            state = TransactionState.RolledBack;

            bool holesTouched = false;
            if (appliedHeights) { target.ApplyHeights(heightsBefore); }
            bool restoreHoles = appliedHoles || (cls == null || cls.restoreHolesOnRollBack);
            if (restoreHoles && surfaceBefore != null && surfaceBefore.Length > 0)
            {
                target.ApplySurfacePresent(surfaceBefore);
                holesTouched = true;
            }
            target.Settle(holesTouched);
        }

        public string Describe()
        {
            return string.Format("{0} on '{1}' for '{2}' heights={3} holes={4}", state, target.TargetName,
                requesterId, appliedHeights, appliedHoles);
        }
    }


    /// <summary> The production <see cref="IHeightfieldTarget"/>: one scene terrain, read directly and written through
    /// the gateway's sink so that RM-57's single-owner search still holds. Reading is not writing, which is why the
    /// reads below are allowed to live here -- the sink stays the only place that modifies the data. </summary>
    public sealed class TerrainTileTarget : IHeightfieldTarget
    {
        private readonly Terrain terrain;
        private readonly ITerrainWriteSink sink;

        public TerrainTileTarget(Terrain terrain, ITerrainWriteSink sink)
        {
            this.terrain = terrain;
            this.sink = sink;
        }

        private TerrainData Data
        {
            get { return terrain != null ? terrain.terrainData : null; }
        }

        public string TargetName
        {
            get { return terrain != null ? terrain.name : "(no terrain)"; }
        }

        public int HeightmapResolution
        {
            get { TerrainData d = Data; return d != null ? d.heightmapResolution : 0; }
        }

        public double HeightRangeMetres
        {
            get { TerrainData d = Data; return d != null ? d.size.y : 0.0; }
        }

        public double SpanMetresX
        {
            get { TerrainData d = Data; return d != null ? d.size.x : 0.0; }
        }

        public double SpanMetresZ
        {
            get { TerrainData d = Data; return d != null ? d.size.z : 0.0; }
        }

        public double BaseY
        {
            get { return terrain != null ? terrain.transform.position.y : 0.0; }
        }

        public float[,] ReadHeights()
        {
            TerrainData d = Data;
            if (d == null) { return new float[0, 0]; }
            int res = d.heightmapResolution;
            return d.GetHeights(0, 0, res, res);
        }

        public bool[,] ReadSurfacePresent()
        {
            TerrainData d = Data;
            if (d == null) { return new bool[0, 0]; }
            int res = d.holesResolution;
            if (res <= 0) { return new bool[0, 0]; }
            return d.GetHoles(0, 0, res, res);
        }

        public void ApplyHeights(float[,] heights)
        {
            if (sink == null) { return; }
            sink.WriteHeights(terrain, heights);
        }

        public void ApplySurfacePresent(bool[,] surfacePresent)
        {
            if (sink == null) { return; }
            sink.WriteHoles(terrain, 0, 0, surfacePresent);
        }

        public void Settle(bool holesChanged)
        {
            if (sink == null) { return; }
            sink.Settle(terrain, holesChanged);
        }
    }
}

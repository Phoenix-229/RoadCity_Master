// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-98's watertight gate: a terrain write is all-or-nothing, and the surface it leaves has no wall in
    /// it.
    ///
    /// The author's report was "the terrain gets a hole". <see cref="WatertightTerrainWrite"/> lists the four causes
    /// behind that one symptom; <see cref="CityTunnelGroundGate"/> covers the tunnel sentence, and this gate covers the
    /// other three, over plain arrays through <see cref="ArrayHeightfield"/> -- no scene, no terrain, no collider, no
    /// frame. That is not a compromise. Against a real terrain a failed comparison can always be blamed on the engine's
    /// quantisation, its filtering or a collider that had not rebuilt yet; against arrays a failure is the rule being
    /// wrong, which is the only thing worth gating.
    ///
    /// Four sweeps:
    ///
    ///   1. PROBE. Conform a corridor through a hill and then walk the finished surface at the configured probe spacing,
    ///      the way a physics probe would walk it. Every probe must find the surface that was written, none may fall
    ///      into a hole, and no two neighbouring probes may be separated by more than the seam limit. The tolerance is
    ///      checked against the tile's own 16-bit quantum as well, so the dial cannot quietly become looser than the
    ///      storage format needs.
    ///   2. BOUNDARY STEP. Cut twenty metres out of flat ground and require that the write introduced no step steeper
    ///      than 1:3 anywhere -- and, first, that the SAME cut without the outward extension still fails, because a
    ///      sweep that passes on the broken code gates nothing. The ring count is read back from
    ///      <see cref="WatertightTerrainWrite.RequiredBlendSamples"/> and asserted to have exceeded the configured
    ///      floor, which is what proves the band was derived from the drop rather than taken from the dial.
    ///   3. INTERRUPTED BUILD. A build that tore a cut halfway and opened a hole, then was cancelled, must leave the
    ///      heightfield bit-for-bit as it was found, no hole open, and the tile settled exactly once.
    ///   4. THE OLDER MOUTH. A tunnel opening cut by an earlier build must SURVIVE a later build's cancellation.
    ///      Sealing everything on roll-back would make sweep 3 pass by destroying a feature, so the trade is gated in
    ///      both directions rather than only the convenient one.
    ///
    /// Every figure is derived from the classes and the tile. A gate that types in 2.6 m keeps passing after someone
    /// changes the seam gradient, and the tool is then wrong in the scene. </summary>
    public static class CityWatertightTerrainGate
    {
        [MenuItem("Window/Road City Master/Testing/Watertight terrain writes")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            UnityEngine.Debug.Log(report);
            if (!passed) { UnityEngine.Debug.LogError("RCMWATERTIGHT FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            Run(out report);
            UnityEngine.Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            List<string> measured = new List<string>();

            SweepOneProbe(failures, measured);
            SweepTwoBoundaryStep(failures, measured);
            SweepThreeInterruptedBuild(failures, measured);
            SweepFourOlderMouthSurvives(failures, measured);

            StringBuilder sb = new StringBuilder();
            sb.Append("RM-98 watertight terrain write gate: ");
            sb.Append(failures.Count == 0 ? "PASS" : "FAIL");
            for (int i = 0; i < failures.Count; i++) { sb.Append("\n  - ").Append(failures[i]); }
            for (int i = 0; i < measured.Count; i++) { sb.Append("\n    ").Append(measured[i]); }
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string what, List<string> failures)
        {
            if (!condition) { failures.Add(what); }
        }


        #region "The tile these sweeps share"
        // A tile the size of a real one. The resolution is 2^n + 1 as the engine requires, the height range is a
        // landscape's rather than a test's so the 16-bit quantum is realistic, and the span makes the sample spacing
        // 7.8125 m -- coarse enough that a twenty metre cut genuinely needs several rings, which is the case worth
        // gating.
        private const int Res = 129;
        private const double Range = 600.0;
        private const double Span = 1000.0;

        private static WatertightWriteClass Cls() { return WatertightWriteClass.Default(); }

        private static double Spacing()
        {
            return WatertightTerrainWrite.SampleSpacingMetres(Span, Res);
        }

        private static float Normalised(double elevationMetres)
        {
            return (float)(elevationMetres / Range);
        }

        /// <summary> Set every sample in a window to one elevation, which is what a conform to a flat carriageway
        /// amounts to over a short run. </summary>
        private static void SetWindow(float[,] heights, HeightSampleWindow window, double elevationMetres)
        {
            float v = Normalised(elevationMetres);
            for (int f = window.firstMin; f <= window.firstMax; f++)
            {
                for (int s = window.secondMin; s <= window.secondMax; s++) { heights[f, s] = v; }
            }
        }

        /// <summary> The production sequence, in one place so the sweeps cannot each drift into testing a different
        /// order of operations: measure the drop, derive the band, grow the window, carry the surface outward, tilt it
        /// back to the ground. </summary>
        private static HeightSampleWindow Finish(float[,] written, float[,] before, HeightSampleWindow changed,
            WatertightWriteClass cls, out double worstDelta, out int blend)
        {
            worstDelta = WatertightTerrainWrite.WorstDeltaMetres(written, before, changed, Range);
            blend = WatertightTerrainWrite.RequiredBlendSamples(worstDelta, Spacing(), cls);
            HeightSampleWindow neighbourhood = WatertightTerrainWrite.NeighbourhoodOf(changed, blend, Res, cls);
            WatertightTerrainWrite.ExtendOutward(written, changed, neighbourhood);
            WatertightTerrainWrite.BlendPerimeter(written, before, neighbourhood, blend);
            return neighbourhood;
        }
        #endregion


        #region "1. Probe sweep"
        private static void SweepOneProbe(List<string> failures, List<string> measured)
        {
            WatertightWriteClass cls = Cls();
            ArrayHeightfield tile = new ArrayHeightfield(Res, Range, Span);
            tile.FillDome(100.0, 40.0);

            float[,] before = tile.Snapshot();
            float[,] written = tile.ReadHeights();

            // A corridor across the hill, conformed to one carriageway level. Interior by more rings than a forty metre
            // drop can ask for, so this sweep is about the finished surface rather than about a tile edge.
            HeightSampleWindow corridor = HeightSampleWindow.FromBounds(50, 78, 20, 108);
            SetWindow(written, corridor, 130.0);

            double worstDelta;
            int blend;
            HeightSampleWindow neighbourhood = Finish(written, before, corridor, cls, out worstDelta, out blend);
            Expect(!neighbourhood.clampedAtEdge,
                "the probe sweep's corridor was meant to sit clear of the tile edge, but the neighbourhood clamped -- "
                + "the sweep would then be measuring RM-99's inter-tile seam instead of this one", failures);

            float[,] intended = new float[Res, Res];
            Array.Copy(written, intended, written.Length);

            TerrainWriteTransaction t = TerrainWriteTransaction.Open(tile, "probe sweep");
            t.ApplyHeights(written);
            t.Commit();

            Expect(t.State == TerrainWriteTransaction.TransactionState.Committed,
                "a committed transaction must report itself committed", failures);
            Expect(tile.settleCount == 1,
                "a committed transaction must settle the tile exactly once, so the drawn and the collided surface "
                + "cannot disagree; settled " + tile.settleCount.ToString(CultureInfo.InvariantCulture) + " time(s)",
                failures);
            Expect(tile.refusedWrites == 0, "the tile refused a write during the probe sweep", failures);

            // The dial has to be defensible for THIS tile, not in the abstract: heights are stored as 16-bit normalised
            // values, so nothing can be compared more tightly than a quantum however carefully it is computed.
            double quantum = WatertightTerrainWrite.HeightQuantumMetres(Range);
            Expect(cls.probeToleranceMetres >= quantum * 2.0,
                "the probe tolerance is tighter than two 16-bit quanta on this tile's height range, so the sweep would "
                + "be testing the storage format rather than the write", failures);

            double maxStep = WatertightTerrainWrite.MaximumSeamStepMetres(Spacing(), cls);
            double worstDisagreement = 0.0;
            double worstProbeStep = 0.0;
            int holesUnderProbe = 0;
            int probes = 0;

            // Two crossing lines rather than a full grid: at half a metre a grid over a kilometre is four million
            // probes, and the seam this sweep is looking for runs along the corridor, so a line across it and a line
            // along it meet every ring of the blend band once.
            double step = cls.probeSpacingMetres > 0.0 ? cls.probeSpacingMetres : 0.5;
            double alongZ = 0.5;
            double previous = double.NaN;
            for (double x = 0.0; x <= Span; x += step)
            {
                double nx = x / Span;
                double reported = WatertightTerrainWrite.HeightAtNormalised(tile.Live, nx, alongZ, Range, tile.BaseY);
                double asked = WatertightTerrainWrite.HeightAtNormalised(intended, nx, alongZ, Range, tile.BaseY);
                double d = Math.Abs(reported - asked);
                if (d > worstDisagreement) { worstDisagreement = d; }
                if (!WatertightTerrainWrite.SurfacePresentAtNormalised(tile.LiveSurfacePresent, nx, alongZ))
                {
                    holesUnderProbe++;
                }
                if (!double.IsNaN(previous))
                {
                    double s = Math.Abs(reported - previous);
                    if (s > worstProbeStep) { worstProbeStep = s; }
                }
                previous = reported;
                probes++;
            }

            double alongX = 0.5;
            previous = double.NaN;
            for (double z = 0.0; z <= Span; z += step)
            {
                double nz = z / Span;
                double reported = WatertightTerrainWrite.HeightAtNormalised(tile.Live, alongX, nz, Range, tile.BaseY);
                double asked = WatertightTerrainWrite.HeightAtNormalised(intended, alongX, nz, Range, tile.BaseY);
                double d = Math.Abs(reported - asked);
                if (d > worstDisagreement) { worstDisagreement = d; }
                if (!WatertightTerrainWrite.SurfacePresentAtNormalised(tile.LiveSurfacePresent, alongX, nz))
                {
                    holesUnderProbe++;
                }
                if (!double.IsNaN(previous))
                {
                    double s = Math.Abs(reported - previous);
                    if (s > worstProbeStep) { worstProbeStep = s; }
                }
                previous = reported;
                probes++;
            }

            Expect(probes > 0, "the probe sweep took no probes at all", failures);
            Expect(worstDisagreement <= cls.probeToleranceMetres, string.Format(CultureInfo.InvariantCulture,
                "a probe found the surface {0:0.####} m from where the write put it, past the {1:0.####} m tolerance -- "
                + "the transaction did not deliver the whole array", worstDisagreement, cls.probeToleranceMetres),
                failures);
            Expect(holesUnderProbe == 0, string.Format(CultureInfo.InvariantCulture,
                "{0} probe(s) fell through a hole after a write that opened none", holesUnderProbe), failures);
            Expect(worstProbeStep <= maxStep, string.Format(CultureInfo.InvariantCulture,
                "two probes {0:0.##} m apart are separated by {1:0.####} m of height, past the {2:0.####} m seam limit "
                + "-- that is the invisible wall", step, worstProbeStep, maxStep), failures);

            measured.Add(string.Format(CultureInfo.InvariantCulture,
                "probe sweep: {0} probes at {1:0.##} m, worst disagreement {2:0.#####} m (tolerance {3:0.####}, quantum "
                + "{4:0.#####}), worst probe-to-probe step {5:0.####} m (limit {6:0.####}), drop {7:0.##} m over {8} "
                + "ring(s)", probes, step, worstDisagreement, cls.probeToleranceMetres, quantum, worstProbeStep,
                maxStep, worstDelta, blend));
        }
        #endregion


        #region "2. Boundary step sweep"
        private static void SweepTwoBoundaryStep(List<string> failures, List<string> measured)
        {
            WatertightWriteClass cls = Cls();
            double spacing = Spacing();
            double maxStep = WatertightTerrainWrite.MaximumSeamStepMetres(spacing, cls);
            double quantum = WatertightTerrainWrite.HeightQuantumMetres(Range);
            const double GroundLevel = 120.0;
            const double CutLevel = 100.0;

            ArrayHeightfield tile = new ArrayHeightfield(Res, Range, Span);
            tile.FillFlat(GroundLevel);
            float[,] before = tile.Snapshot();

            HeightSampleWindow cut = HeightSampleWindow.FromBounds(40, 80, 40, 80);

            // First, the defect. A blend with no outward extension leaves the band holding untouched ground, so the
            // whole drop stays on the one sample at the cut's edge. If this does NOT fail, the sweep below proves
            // nothing, because it would pass on broken code just as happily.
            float[,] broken = tile.ReadHeights();
            SetWindow(broken, cut, CutLevel);
            double brokenDelta = WatertightTerrainWrite.WorstDeltaMetres(broken, before, cut, Range);
            int brokenBlend = WatertightTerrainWrite.RequiredBlendSamples(brokenDelta, spacing, cls);
            HeightSampleWindow brokenNeighbourhood = WatertightTerrainWrite.NeighbourhoodOf(cut, brokenBlend, Res, cls);
            WatertightTerrainWrite.BlendPerimeter(broken, before, brokenNeighbourhood, brokenBlend);
            int bf, bs;
            double brokenStep = WatertightTerrainWrite.WorstIntroducedStepMetres(broken, before,
                brokenNeighbourhood.ExpandedBy(1, Res), Range, out bf, out bs);
            Expect(brokenStep > maxStep, string.Format(CultureInfo.InvariantCulture,
                "blending WITHOUT the outward extension was supposed to leave a wall for this sweep to catch, but the "
                + "worst introduced step was only {0:0.####} m against a {1:0.####} m limit -- this sweep can no longer "
                + "tell the fixed path from the broken one", brokenStep, maxStep), failures);

            // Now the production sequence.
            float[,] written = tile.ReadHeights();
            SetWindow(written, cut, CutLevel);
            double worstDelta;
            int blend;
            HeightSampleWindow neighbourhood = Finish(written, before, cut, cls, out worstDelta, out blend);

            Expect(Math.Abs(worstDelta - (GroundLevel - CutLevel)) <= quantum, string.Format(
                CultureInfo.InvariantCulture, "the measured drop should be the {0:0.#} m that was cut, but read as "
                + "{1:0.####} m", GroundLevel - CutLevel, worstDelta), failures);
            Expect(blend > cls.perimeterBlendSamples, string.Format(CultureInfo.InvariantCulture,
                "a {0:0.#} m drop at {1:0.####} m spacing needs more than the configured floor of {2} ring(s), so the "
                + "band must be DERIVED; it came back as {3}", worstDelta, spacing, cls.perimeterBlendSamples, blend),
                failures);
            Expect(!neighbourhood.clampedAtEdge,
                "the boundary-step sweep's cut was meant to sit clear of the tile edge", failures);

            int atFirst, atSecond;
            double introduced = WatertightTerrainWrite.WorstIntroducedStepMetres(written, before,
                neighbourhood.ExpandedBy(1, Res), Range, out atFirst, out atSecond);
            Expect(introduced <= maxStep + quantum, string.Format(CultureInfo.InvariantCulture,
                "the write introduced a {0:0.####} m step at sample [{1},{2}], past the {3:0.####} m limit that 1:3 "
                + "traversable slope gives at this tile's {4:0.####} m spacing", introduced, atFirst, atSecond, maxStep,
                spacing), failures);

            // The outer ring must be EXACTLY the ground it was, not nearly. "Nearly" is how a surface creeps by a
            // centimetre per rebuild until the seam is back.
            bool outerRingExact = true;
            for (int f = neighbourhood.firstMin; f <= neighbourhood.firstMax; f++)
            {
                for (int s = neighbourhood.secondMin; s <= neighbourhood.secondMax; s++)
                {
                    if (neighbourhood.DepthFromEdge(f, s) != 0) { continue; }
                    if (written[f, s] != before[f, s]) { outerRingExact = false; }
                }
            }
            Expect(outerRingExact,
                "the outermost ring of the write must be bit-for-bit the ground that was there, so there is no "
                + "discontinuity at the boundary of the write at all", failures);

            // And the road's own surface must come through untouched: a blend that reaches into the corridor is a road
            // that sags at its edges.
            int centreFirst = (cut.firstMin + cut.firstMax) / 2;
            int centreSecond = (cut.secondMin + cut.secondMax) / 2;
            Expect(written[centreFirst, centreSecond] == Normalised(CutLevel),
                "the middle of the cut must still be exactly the level that was asked for -- the blend band may not "
                + "reach into the conformed surface", failures);

            // The face has to actually descend, monotonically, across the band. A band that wobbles would pass a step
            // test while looking like corrugations in the scene.
            bool monotonic = true;
            int probeSecond = centreSecond;
            for (int f = neighbourhood.firstMin; f < cut.firstMin; f++)
            {
                if (written[f + 1, probeSecond] > written[f, probeSecond]) { monotonic = false; }
            }
            Expect(monotonic,
                "the cut face must fall steadily from existing ground to the carriageway across the band, not wobble",
                failures);

            measured.Add(string.Format(CultureInfo.InvariantCulture,
                "boundary step sweep: {0:0.#} m cut at {1:0.####} m spacing -> {2} ring(s) (floor {3}), worst "
                + "introduced step {4:0.####} m against a {5:0.####} m limit; unextended blend left {6:0.####} m",
                worstDelta, spacing, blend, cls.perimeterBlendSamples, introduced, maxStep, brokenStep));
        }
        #endregion


        #region "3. Interrupted build sweep"
        private static void SweepThreeInterruptedBuild(List<string> failures, List<string> measured)
        {
            WatertightWriteClass cls = Cls();
            ArrayHeightfield tile = new ArrayHeightfield(Res, Range, Span);
            tile.FillDome(100.0, 40.0);

            float[,] original = tile.Snapshot();
            bool[,] originalHoles = tile.ReadSurfacePresent();
            Expect(!WatertightTerrainWrite.AnyHoleOpen(originalHoles),
                "a fresh tile must start with no hole open, or this sweep proves nothing", failures);

            TerrainWriteTransaction t = TerrainWriteTransaction.Open(tile, "cancelled build");
            Expect(t != null && t.IsOpen, "a transaction over a tile must open", failures);
            Expect(t != null && !t.Applied, "a transaction that has written nothing must not report itself applied",
                failures);

            // Half a cut -- the state a build is in when it is cancelled between two splines.
            float[,] torn = tile.ReadHeights();
            SetWindow(torn, HeightSampleWindow.FromBounds(40, 60, 40, 80), 90.0);
            t.ApplyHeights(torn);

            // And a tunnel mouth, opened by the same build.
            bool[,] holes = tile.ReadSurfacePresent();
            holes[50, 50] = false;
            t.ApplySurfacePresent(holes);

            Expect(t.Applied, "a transaction that wrote heights and holes must report itself applied", failures);
            Expect(WatertightTerrainWrite.AnyHoleOpen(tile.LiveSurfacePresent),
                "the cancelled build was supposed to have opened a hole before it was cancelled, or the roll-back "
                + "below is not being asked to close anything", failures);
            Expect(tile.settleCount == 0, "nothing may settle the tile while a transaction is still open", failures);

            t.RollBack(cls);

            Expect(t.State == TerrainWriteTransaction.TransactionState.RolledBack,
                "a rolled-back transaction must report itself rolled back", failures);

            int differing = 0;
            for (int f = 0; f < Res; f++)
            {
                for (int s = 0; s < Res; s++)
                {
                    if (tile.Live[f, s] != original[f, s]) { differing++; }
                }
            }
            Expect(differing == 0, string.Format(CultureInfo.InvariantCulture,
                "a cancelled build must leave the heightfield bit-for-bit as it found it, but {0} sample(s) differ",
                differing), failures);

            int stillOpen = WatertightTerrainWrite.CountHolesOpen(tile.LiveSurfacePresent);
            Expect(stillOpen == 0, string.Format(CultureInfo.InvariantCulture,
                "a cancelled build left {0} hole(s) open -- exactly the reported defect", stillOpen), failures);
            Expect(tile.settleCount == 1, string.Format(CultureInfo.InvariantCulture,
                "a roll-back must settle the tile exactly once so physics matches the restored ground; settled {0} "
                + "time(s)", tile.settleCount), failures);
            Expect(tile.settleWithHolesCount == 1,
                "a roll-back that restored the hole map must say so when it settles, or the holes texture is never "
                + "re-synced and the collider keeps the opening", failures);

            // Closing a closed transaction must be inert. Two settles is two collider rebuilds and, worse, a window in
            // which the tile has been written by a transaction that already gave its ground back.
            t.Commit();
            t.RollBack(cls);
            Expect(tile.settleCount == 1, string.Format(CultureInfo.InvariantCulture,
                "closing an already-closed transaction must do nothing, but the tile has now settled {0} time(s)",
                tile.settleCount), failures);

            // A transaction that wrote nothing and is then cancelled must not write heights at all.
            ArrayHeightfield untouched = new ArrayHeightfield(Res, Range, Span);
            untouched.FillFlat(100.0);
            TerrainWriteTransaction empty = TerrainWriteTransaction.Open(untouched, "cancelled before it wrote");
            empty.RollBack(cls);
            Expect(untouched.applyHeightsCount == 0,
                "rolling back a build that never wrote a height must not write one either", failures);
            Expect(untouched.settleCount == 1,
                "even a build that wrote nothing must settle once when it closes, because another path may have "
                + "opened a hole while it was in flight", failures);

            measured.Add(string.Format(CultureInfo.InvariantCulture,
                "interrupted build sweep: {0} sample(s) differ after roll-back, {1} hole(s) left open, {2} settle(s)",
                differing, stillOpen, tile.settleCount));
        }
        #endregion


        #region "4. The older mouth survives"
        private static void SweepFourOlderMouthSurvives(List<string> failures, List<string> measured)
        {
            WatertightWriteClass cls = Cls();
            ArrayHeightfield tile = new ArrayHeightfield(Res, Range, Span);
            tile.FillFlat(100.0);

            // An earlier build's tunnel mouth, already in the tile before today's build starts.
            bool[,] mouth = tile.ReadSurfacePresent();
            mouth[64, 64] = false;
            mouth[64, 65] = false;
            tile.ApplySurfacePresent(mouth);
            int mouthQuads = WatertightTerrainWrite.CountHolesOpen(tile.LiveSurfacePresent);
            Expect(mouthQuads == 2, "the earlier tunnel mouth was not established, so this sweep proves nothing",
                failures);

            TerrainWriteTransaction t = TerrainWriteTransaction.Open(tile, "a later build, cancelled");
            float[,] torn = tile.ReadHeights();
            SetWindow(torn, HeightSampleWindow.FromBounds(20, 30, 20, 30), 80.0);
            t.ApplyHeights(torn);
            t.RollBack(cls);

            int afterwards = WatertightTerrainWrite.CountHolesOpen(tile.LiveSurfacePresent);
            Expect(afterwards == mouthQuads, string.Format(CultureInfo.InvariantCulture,
                "a later build's cancellation must restore the hole map it FOUND, leaving an earlier tunnel mouth "
                + "exactly where it was; {0} quad(s) were open before and {1} after -- sealing everything would fix a "
                + "hole by destroying a feature", mouthQuads, afterwards), failures);
            Expect(tile.LiveSurfacePresent[64, 64] == false && tile.LiveSurfacePresent[64, 65] == false,
                "the earlier mouth must be open at the same quads it was open at, not merely the same number of them",
                failures);

            measured.Add(string.Format(CultureInfo.InvariantCulture,
                "older mouth sweep: {0} quad(s) open before a cancelled build, {1} after", mouthQuads, afterwards));
        }
        #endregion
    }
}
#endif

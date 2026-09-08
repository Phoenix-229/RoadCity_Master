// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The four steps of regenerating host terrain under a road that is already built, in the only order that
    /// does not leave something broken. RM-99 asks for this to be STATED rather than implied by whatever order the calls
    /// happen to be made in, so it is an ordered list a check can be run against. </summary>
    public enum HostRegenerationStep
    {
        /// <summary> Every tunnel opening and claimed region goes back to the provider FIRST. </summary>
        ReleaseHolds = 0,

        /// <summary> The host replaces the ground. The tool writes nothing during this step and in connected mode
        /// cannot: <see cref="ProviderTerrainSink"/> suppresses and counts every write, which is what makes this step
        /// enforceable rather than a rule in a comment. </summary>
        RegenerateGround = 1,

        /// <summary> Each road's <see cref="GroundConformRecord"/> is compared with the new ground. Reads only.
        /// </summary>
        EvaluateDrift = 2,

        /// <summary> Roads that need it are rebuilt -- which re-acquires their holds -- or named in a report. Never
        /// neither. </summary>
        RebuildOrReport = 3,
    }


    /// <summary> What went wrong with the order, named rather than numbered. </summary>
    public enum HostRegenerationViolation
    {
        None = 0,

        /// <summary> The ground was replaced while the tool still held openings on it. A held opening describes a hole
        /// in a heightfield that is about to stop existing, so afterwards the host is either honouring a hole in the
        /// middle of new ground or has dropped it and sealed a tunnel mouth. Both are the hole nobody can find.
        /// </summary>
        RegeneratedWhileHolding = 1,

        /// <summary> Drift was measured before the ground was replaced, so the verdict describes ground that is already
        /// gone. This is the failure that reports "everything agrees" immediately after a regeneration. </summary>
        EvaluatedBeforeRegenerating = 2,

        /// <summary> The sequence ended with neither a rebuild nor a report. Silence here is precisely the outcome
        /// RM-99 exists to forbid: the roads are floating and nobody has been told. </summary>
        FinishedWithoutRebuildOrReport = 3,

        /// <summary> A step is missing or the sequence is empty. </summary>
        Incomplete = 4,
    }


    /// <summary> RM-99's ordering contract, and the check that a sequence obeys it.
    ///
    /// The order is not a preference. Each step depends on the previous one having finished: holds must be back before
    /// the ground they describe is replaced, the ground must be replaced before a comparison against it means anything,
    /// and the comparison must have happened before anybody can say which roads to rebuild. Written out, the dependency
    /// chain IS the order, which is why the contract can be one array and a walk over it rather than a document.
    ///
    /// The check takes the sequence that was actually performed, so a gate can prove the wrong orders are caught instead
    /// of proving the right one runs. A test that only ever feeds the correct sequence in is a test of nothing.
    /// </summary>
    public static class HostRegenerationContract
    {
        private static readonly HostRegenerationStep[] steps = new HostRegenerationStep[]
        {
            HostRegenerationStep.ReleaseHolds,
            HostRegenerationStep.RegenerateGround,
            HostRegenerationStep.EvaluateDrift,
            HostRegenerationStep.RebuildOrReport,
        };

        public static IList<HostRegenerationStep> Steps { get { return steps; } }

        /// <summary> The contract in one sentence, for a panel and for a refusal. </summary>
        public static string Sentence
        {
            get
            {
                return "When the world regenerates terrain under a built road: release every held opening and claimed "
                    + "region first, let the host replace the ground without the tool writing, then compare each road "
                    + "against the new ground, then rebuild the roads that need it or name them. A road is never left "
                    + "floating, buried or holed, and never left unreported.";
            }
        }

        /// <summary> Judge a performed sequence. Steps may repeat -- a host that regenerates two chunks does the middle
        /// two steps twice -- so this checks the ORDER of first occurrences and the dependencies, not an exact match
        /// against the array. An exact match would fail every real caller and would therefore be turned off. </summary>
        public static HostRegenerationViolation Check(IList<HostRegenerationStep> performed)
        {
            if (performed == null || performed.Count == 0) { return HostRegenerationViolation.Incomplete; }

            bool released = false;
            bool regenerated = false;
            bool evaluated = false;
            bool finished = false;

            for (int i = 0; i < performed.Count; i++)
            {
                switch (performed[i])
                {
                    case HostRegenerationStep.ReleaseHolds:
                        released = true;
                        break;
                    case HostRegenerationStep.RegenerateGround:
                        if (!released) { return HostRegenerationViolation.RegeneratedWhileHolding; }
                        regenerated = true;
                        break;
                    case HostRegenerationStep.EvaluateDrift:
                        if (!regenerated) { return HostRegenerationViolation.EvaluatedBeforeRegenerating; }
                        evaluated = true;
                        break;
                    case HostRegenerationStep.RebuildOrReport:
                        if (!evaluated) { return HostRegenerationViolation.Incomplete; }
                        finished = true;
                        break;
                }
            }

            if (!released || !regenerated || !evaluated) { return HostRegenerationViolation.Incomplete; }
            if (!finished) { return HostRegenerationViolation.FinishedWithoutRebuildOrReport; }
            return HostRegenerationViolation.None;
        }
    }


    /// <summary> One road's verdict, ready to be printed. </summary>
    public struct StaleRoadEntry
    {
        public string roadName;
        public GroundDrift drift;
    }


    /// <summary> RM-99's stale-state report: every road that was measured, what happened to the ground under it, and
    /// which of them have to be rebuilt.
    ///
    /// Its one rule is that it cannot be empty when something is wrong. A report that lists only the failures looks
    /// identical to a report that was never run, so the roads that agreed are listed too and the count of roads
    /// CONSIDERED is part of the output. That is the difference between "nothing needs refreshing" and "nothing was
    /// checked", and those two have been mistaken for each other in every asset that has ever shipped a stale-cache
    /// warning. </summary>
    public sealed class StaleRoadReport
    {
        public readonly List<StaleRoadEntry> entries = new List<StaleRoadEntry>();

        public int Considered { get { return entries.Count; } }

        public void Add(string roadName, GroundDrift drift)
        {
            StaleRoadEntry entry = new StaleRoadEntry();
            entry.roadName = string.IsNullOrEmpty(roadName) ? "unnamed road" : roadName;
            entry.drift = drift;
            entries.Add(entry);
        }

        public int CountWith(GroundVerdict verdict)
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++) { if (entries[i].drift.verdict == verdict) { n++; } }
            return n;
        }

        /// <summary> The roads an author has to act on: out of tolerance, or unmeasurable. An unmeasurable road belongs
        /// here rather than in the quiet list -- "I could not check this one" is an action item, and letting it pass
        /// silently is how a disconnected provider turns into a scene full of floating roads. </summary>
        public IList<string> RoadsNeedingRefresh()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                GroundVerdict v = entries[i].drift.verdict;
                if (v == GroundVerdict.Agrees) { continue; }
                names.Add(entries[i].roadName);
            }
            return names;
        }

        public bool AllAgree { get { return RoadsNeedingRefresh().Count == 0; } }

        /// <summary> The whole report, one line per road plus a totals line. The totals line is printed even when every
        /// road agreed, for the reason in the class summary. </summary>
        public string Describe()
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                b.Append("  ").Append(entries[i].drift.Describe(entries[i].roadName)).Append('\n');
            }
            b.Append("  considered ").Append(Considered.ToString(CultureInfo.InvariantCulture))
             .Append(" roads: agrees ").Append(CountWith(GroundVerdict.Agrees).ToString(CultureInfo.InvariantCulture))
             .Append(", advised ")
             .Append(CountWith(GroundVerdict.RefreshAdvised).ToString(CultureInfo.InvariantCulture))
             .Append(", required ")
             .Append(CountWith(GroundVerdict.RefreshRequired).ToString(CultureInfo.InvariantCulture))
             .Append(", never recorded ")
             .Append(CountWith(GroundVerdict.NeverRecorded).ToString(CultureInfo.InvariantCulture))
             .Append(", unreadable ")
             .Append(CountWith(GroundVerdict.GroundUnavailable).ToString(CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }


    /// <summary> One straight line of surface heights, read through whichever reader the mode installed. </summary>
    public struct SurfaceLine
    {
        public float[] heights;
        public int unreadable;
        public double worstStepMetres;
        public int worstStepAt;

        public int Count { get { return heights != null ? heights.Length : 0; } }
    }


    /// <summary> RM-99: the connected case held to RM-98's standard, using RM-98's own numbers.
    ///
    /// The point of this file is that it knows nothing about modes. Both sinks implement
    /// <see cref="ITerrainSurfaceReader"/> and the gateway hands one over, so the identical probe walks the tile in
    /// standalone and the provider in connected mode. Any other arrangement -- a provider-shaped double, a second probe
    /// written for the linked path -- would be testing the copy, and the copy is what stays correct while the real path
    /// rots.
    ///
    /// The tolerance the two modes are compared within is not chosen here either. It is
    /// <see cref="WatertightWriteClass.probeToleranceMetres"/>, the same tolerance RM-98's single-mode probe sweep holds
    /// one surface to; holding the AGREEMENT between two surfaces to that same figure is what makes "the probe sweep
    /// passes in both modes" a statement about the same standard rather than two different ones. </summary>
    public static class LinkedGroundCheck
    {
        /// <summary> Walk a straight line through a surface reader at the probe spacing and report the surface it found,
        /// including the worst step between neighbouring probes -- which is the continuity question RM-98 asks, asked of
        /// the provider's ground instead of the tile's. </summary>
        public static SurfaceLine ProbeLine(ITerrainSurfaceReader reader, Vector2 from, Vector2 to,
            WatertightWriteClass cls)
        {
            SurfaceLine line = new SurfaceLine();
            line.worstStepAt = -1;
            double spacing = cls.probeSpacingMetres > 0.0 ? cls.probeSpacingMetres : 0.5;
            double length = Vector2.Distance(from, to);
            int steps = (int)System.Math.Ceiling(length / spacing);
            if (steps < 1) { steps = 1; }
            line.heights = new float[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)((double)i / steps);
                Vector2 at = Vector2.Lerp(from, to, t);
                float h = reader != null ? reader.SampleHeight(at.x, at.y) : float.NaN;
                if (float.IsNaN(h) || float.IsInfinity(h)) { line.unreadable++; }
                line.heights[i] = h;
                if (i == 0) { continue; }
                float previous = line.heights[i - 1];
                if (float.IsNaN(previous) || float.IsNaN(h)) { continue; }
                double step = h - previous;
                if (step < 0.0) { step = -step; }
                if (step > line.worstStepMetres) { line.worstStepMetres = step; line.worstStepAt = i; }
            }
            return line;
        }

        /// <summary> The worst disagreement between two readings of the same line, and where. Lines of different length
        /// are a failure in themselves -- the same request produced a different number of probes -- and are reported as
        /// an infinite disagreement rather than compared over the shorter one, because quietly comparing the overlap is
        /// how a mode that returns half a road passes. </summary>
        public static double WorstModeDisagreementMetres(SurfaceLine a, SurfaceLine b, out int atProbe)
        {
            atProbe = -1;
            if (a.heights == null || b.heights == null) { return double.PositiveInfinity; }
            if (a.heights.Length != b.heights.Length) { return double.PositiveInfinity; }
            double worst = 0.0;
            for (int i = 0; i < a.heights.Length; i++)
            {
                float x = a.heights[i];
                float y = b.heights[i];
                if (float.IsNaN(x) != float.IsNaN(y)) { atProbe = i; return double.PositiveInfinity; }
                if (float.IsNaN(x)) { continue; }
                double d = x - y;
                if (d < 0.0) { d = -d; }
                if (d > worst) { worst = d; atProbe = i; }
            }
            return worst;
        }

        /// <summary> Whether a region the tool is about to hand over lies inside ground the provider has actually
        /// declared. A region that pokes past every declared chunk is not an error the provider will raise -- it will
        /// take the request and modify the part it owns -- so the half outside comes back as untouched ground under a
        /// finished road. Checking before the hand-over is the only place this is cheap. </summary>
        public static bool RegionWithinDeclared(TerrainRegion region, IList<Bounds> declared, out string why)
        {
            why = string.Empty;
            if (!region.hasArea) { why = "the region has no area"; return false; }
            if (declared == null || declared.Count == 0)
            {
                why = "the provider declared no active chunks, so there is nowhere to put this region";
                return false;
            }

            Bounds want = region.ToBounds();
            Bounds union = declared[0];
            for (int i = 1; i < declared.Count; i++) { union.Encapsulate(declared[i]); }

            bool insideX = want.min.x >= union.min.x && want.max.x <= union.max.x;
            bool insideZ = want.min.z >= union.min.z && want.max.z <= union.max.z;
            if (insideX && insideZ) { return true; }

            why = "the region spans x " + Span(want.min.x, want.max.x) + " z " + Span(want.min.z, want.max.z)
                + " but the provider's declared ground spans x " + Span(union.min.x, union.max.x)
                + " z " + Span(union.min.z, union.max.z);
            return false;
        }

        private static string Span(float min, float max)
        {
            return min.ToString("F1", CultureInfo.InvariantCulture) + ".."
                + max.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}

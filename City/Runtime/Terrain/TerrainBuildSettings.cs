// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-98. The one place the terrain side of a build reads its engineering classes from.
    ///
    /// This exists because of a specific, ordinary bug: the terraforming worker runs off the main thread, and off the
    /// main thread it may not touch a <c>Road</c>, a <c>Terrain</c>, a serialized asset or anything else Unity owns.
    /// A worker that needs to know how much cover a tunnel gets therefore cannot ask -- it either receives the figure
    /// before it starts, or it invents one. The vendored code invented one (three centimetres, a rendering nudge that
    /// had become a tunnel's roof), and this holder is what replaces that with the project's real numbers.
    ///
    /// It is a static holder rather than an asset on purpose, and the reason is the same thread rule: the value has to
    /// be readable from a worker, which rules out a <c>ScriptableObject</c> reference being dereferenced there. An
    /// author-facing asset can perfectly well drive this -- it pushes its values in on the main thread, once, at the top
    /// of a build (see <c>Terraforming.TerrainsCalc</c>) -- but the worker only ever reads plain managed objects that
    /// were finished being written before it woke up.
    ///
    /// Every field is a class from the layer that owns that subject, not a copy of its numbers:
    /// <see cref="TunnelClass"/> owns the bore, <see cref="TunnelCoverClass"/> the cover practice,
    /// <see cref="GradeSeparationClass"/> the clearance, <see cref="WatertightWriteClass"/> the seam limits. Nothing
    /// here restates a figure that lives somewhere else, so there is nothing here to drift. </summary>
    public static class TerrainBuildSettings
    {
        #region "The classes"
        private static TunnelClass tunnel = new TunnelClass();
        private static TunnelCoverClass cover = TunnelCoverClass.Default();
        private static GradeSeparationClass separation = GradeSeparationClass.Default();
        private static WatertightWriteClass watertight = WatertightWriteClass.Default();
        private static GroundDriftClass drift = GroundDriftClass.Default();

        /// <summary> The bore: radius, lining oversize, and the minimum water cover a tunnel under a river bed must
        /// keep. Never null -- a null assignment restores the default rather than leaving a hole for a worker to
        /// dereference on a thread where it cannot be caught usefully. </summary>
        public static TunnelClass Tunnel
        {
            get { return tunnel; }
            set { tunnel = value != null ? value : new TunnelClass(); }
        }

        /// <summary> Cover practice: how many excavated diameters over a bore, how much soil over a cut-and-cover
        /// roof, how thick that roof is, and how long a portal notch may be. </summary>
        public static TunnelCoverClass Cover
        {
            get { return cover; }
            set { cover = value != null ? value : TunnelCoverClass.Default(); }
        }

        /// <summary> Vertical clearance for a grade separation, which is also what sets a box tunnel's crown above the
        /// carriageway. </summary>
        public static GradeSeparationClass Separation
        {
            get { return separation; }
            set { separation = value != null ? value : GradeSeparationClass.Default(); }
        }

        /// <summary> Seam and blend limits for terrain writing. A gateway takes its own copy of the reference when it
        /// is created and may be pointed at something else afterwards -- a test needs that -- so this is the project
        /// DEFAULT rather than the value in force, which is <see cref="TerrainWriteGateway.Watertight"/>. </summary>
        public static WatertightWriteClass Watertight
        {
            get { return watertight; }
            set { watertight = value != null ? value : WatertightWriteClass.Default(); }
        }

        /// <summary> How far the ground may move under a finished road before somebody is told, and how often a road is
        /// measured. Lives here rather than on the gateway because it is asked AFTER a build -- often after the gateway
        /// that did the build has been replaced by a reconnect -- and a verdict that changed depending on which gateway
        /// happened to be installed would be worse than no verdict. </summary>
        public static GroundDriftClass Drift
        {
            get { return drift; }
            set { drift = value != null ? value : GroundDriftClass.Default(); }
        }
        #endregion


        #region "Handing them to the worker"
        /// <summary> Push the current classes into the terraforming worker's static cache, on the main thread, before
        /// the worker is started.
        ///
        /// Called from the build entry rather than from the worker, which is the whole point: by the time
        /// <c>DoRects</c> is running there is no main thread to ask. The worker caches a single derived figure -- how
        /// far the finished ground sits above the carriageway through a tunnel -- because that is all it needs, and a
        /// cached scalar cannot be mistaken for permission to read the classes from the wrong thread later.
        ///
        /// Kept here rather than in the worker so that the dependency points one way: this assembly's terrain layer
        /// knows about the vendored worker, and the worker knows only about numbers. </summary>
        public static void ApplyToWorker()
        {
            RoadCityMaster.Threading.TerraformingThreaded.UseTunnelCover(tunnel, cover, separation);
        }

        /// <summary> Put every class back to its published default and re-push. For a test that changed one of them,
        /// and for an author who wants out of an experiment. </summary>
        public static void Reset()
        {
            tunnel = new TunnelClass();
            cover = TunnelCoverClass.Default();
            separation = GradeSeparationClass.Default();
            watertight = WatertightWriteClass.Default();
            ApplyToWorker();
        }

        public static string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "bore r={0:0.###} m +{1:0.###} m lining, water cover >={2:0.###} m; cover {3:0.###} diameters bored / "
                + "{4:0.###} m soil over a {5:0.###} m roof; clearance {6:0.###} m; seam <=1:{7:0.###} with >={8} "
                + "blend samples",
                tunnel.boreRadiusMetres, tunnel.liningOversizeMetres, tunnel.minWaterCoverMetres,
                cover.boredCoverDiameters, cover.minimumSoilCoverMetres, cover.roofThicknessMetres,
                separation.verticalClearanceMetres,
                watertight.maximumSeamGradient > 0.0 ? 1.0 / watertight.maximumSeamGradient : 0.0,
                watertight.perimeterBlendSamples);
        }
        #endregion
    }
}

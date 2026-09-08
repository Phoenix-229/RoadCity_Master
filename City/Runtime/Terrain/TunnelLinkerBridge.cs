// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-98/RM-99. What actually crosses the linker when a segment is made a tunnel.
    ///
    /// The author's report is that choosing a tunnel cuts the hill the tunnel was supposed to pass under, and the cause
    /// is not the tunnel code -- it is the DESCRIPTION. A build announces the ground it changed as boxes; the host's
    /// linker turns each box into "this ground is a cut", pre-loads it, heals it, and keeps re-flattening it after a
    /// regeneration. Announce a bored span that way and the hill is levelled by a host that is doing exactly what it was
    /// told. Nothing in either codebase is wrong; the sentence between them was.
    ///
    /// So this file says the right sentence, and it says it in three separate boxes rather than one:
    ///
    ///   * The OPENING -- the bore itself, from the structural floor to the crown, one excavated diameter wide. This is
    ///     the only box the host is asked to remove surface from, and it is requested as a tunnel opening, which the
    ///     host already implements properly: surface off, a wall skirt hung from the cut edge down to the floor, a
    ///     collider floor across the footprint, and the whole thing re-stamped after a regeneration instead of being
    ///     filled back in. That is why the road's own prefabs meet ground rather than a void -- the floor the host lays
    ///     is what the carriageway and everything on it rests on.
    ///   * The PORTALS -- one bounded notch at each end, <see cref="TunnelTerrainPolicy.PortalNotchLengthMetres"/> long.
    ///     Real cut, because a headwall needs a face to be built against, and bounded, because a tunnel entrance must
    ///     not quietly become a long open cutting through the hill.
    ///   * The SPAN BETWEEN THEM -- announced as <see cref="GroundTreatment.LeaveIntact"/>, or not announced at all when
    ///     the cover is already there. This is the box that used to be a cut, and it is the whole fix.
    ///
    /// Neither side reaches into the other. The host's contract is untouched: this bridge speaks only through
    /// <see cref="TerrainWriteGateway"/>'s existing hold and announce calls, which reach the host through the interface
    /// it published. The treatment rides on <see cref="TerrainRegion"/>, which was already the only thing that crossed.
    /// </summary>
    public static class TunnelLinkerBridge
    {
        #region "What a tunnel run is"
        /// <summary> One continuous tunnel between two portals, as the terrain side needs to see it. Stated in metres
        /// about a centre point rather than as spline nodes, so the planning maths is a function of its arguments and a
        /// gate can drive it with a table (C-06). </summary>
        public struct TunnelRun
        {
            public double centreX;
            public double centreZ;

            /// <summary> Portal to portal along the road, in metres. </summary>
            public double lengthMetres;

            /// <summary> True when the road runs nearer to world X than world Z through this run. Decides which two
            /// faces of the opening the host leaves open. </summary>
            public bool alongX;

            /// <summary> Finished carriageway elevation at the run's centre. </summary>
            public double roadSurfaceElevationMetres;

            /// <summary> Pavement build-up under the carriageway: the opening's floor sits this far below the surface.
            /// </summary>
            public double pavementThicknessMetres;

            /// <summary> Natural ground (or water bed) elevation over the crown at the tightest point of the run. The
            /// tightest point, not the average, because cover is a minimum and an average would let one shallow station
            /// pass. </summary>
            public double tightestGroundElevationMetres;

            /// <summary> Half-width of the corridor the road occupies, for the portal notch boxes -- the notch is as
            /// wide as the works, not as wide as the bore. </summary>
            public double corridorHalfWidthMetres;

            public bool underWater;
            public bool immersedTube;

            /// <summary> How deep an immersed tube is buried below the bed, for the trench footprint. Ignored unless
            /// <see cref="immersedTube"/>. </summary>
            public double burialDepthMetres;
        }
        #endregion


        #region "The plan"
        /// <summary> What the bridge decided, before any of it is executed. Separating the two is what lets a gate
        /// assert the decision without a scene, a provider or a terrain. </summary>
        public struct TunnelLinkPlan
        {
            public bool viable;

            /// <summary> Why not, in words, when <see cref="viable"/> is false. Never a code. </summary>
            public string refusal;

            public TunnelBuildOutcome outcome;

            /// <summary> The bore, labelled <see cref="GroundTreatment.LeaveIntact"/>. Requested as a tunnel opening.
            /// </summary>
            public TerrainRegion opening;

            /// <summary> The two bounded notches, labelled <see cref="GroundTreatment.PortalNotch"/>. </summary>
            public TerrainRegion firstPortal;
            public TerrainRegion secondPortal;

            /// <summary> The span over the bore. Labelled <see cref="GroundTreatment.LeaveIntact"/> when the hill is
            /// deep enough, <see cref="GroundTreatment.CoverFill"/> when an embankment has to be put over the roof, or
            /// <see cref="GroundTreatment.RestoreAfterBackfill"/> for an immersed tube's trench. </summary>
            public TerrainRegion span;

            /// <summary> True when the span must not be announced as changed ground at all, because nothing about it
            /// changes: a bore with adequate cover. The host is still told about the OPENING, which is what it needs to
            /// carve; telling it about the span as well is what levelled the hill. </summary>
            public bool spanIsUntouched;

            public double requiredCoverMetres;
            public double presentCoverMetres;
            public TunnelOpeningGeometry geometry;

            public string Describe()
            {
                if (!viable)
                {
                    return "tunnel link refused: " + (refusal != null ? refusal : "no reason given");
                }
                return string.Format(CultureInfo.InvariantCulture,
                    "{0}; cover {1:0.###} m of {2:0.###} m required; span {3}; {4}",
                    geometry.Describe(), presentCoverMetres, requiredCoverMetres,
                    spanIsUntouched ? "NOT announced (hill untouched)" : span.treatment.ToString(),
                    outcome.ToString());
            }
        }

        /// <summary> The opening box carried alongside the plan, so a gate can check the geometry as numbers rather
        /// than re-deriving it from the region. </summary>
        public struct TunnelOpeningGeometry
        {
            public double floorElevationMetres;
            public double crownElevationMetres;
            public double widthMetres;
            public double lengthMetres;
            public bool openAlongX;

            public string Describe()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "bore {0:0.##} m wide x {1:0.##} m long, y[{2:0.###}..{3:0.###}], open along {4}",
                    widthMetres, lengthMetres, floorElevationMetres, crownElevationMetres, openAlongX ? "X" : "Z");
            }
        }
        #endregion


        #region "Planning"
        /// <summary> Decide what crosses the linker for one tunnel run. Touches nothing: no gateway, no provider, no
        /// terrain. Pure so that "a tunnel does not cut the hill" can be asserted as arithmetic.
        ///
        /// A run whose cover is short is NOT silently deepened and NOT silently allowed. Under water it is refused,
        /// because the failure there is the river coming through the roof and no amount of fill on a river bed fixes a
        /// bore driven too close to it -- that refusal is
        /// <see cref="TunnelBuildOutcome.RefusedInsufficientCover"/>, and it is the host's own rule read from
        /// <see cref="TunnelClass"/> rather than a second copy of it. On land it becomes a cut-and-cover box with an
        /// embankment over the roof, which is what is actually built when an alignment runs shallow. </summary>
        public static TunnelLinkPlan Plan(TunnelRun run, TunnelMode mode, TunnelClass tunnel, TunnelCoverClass cover,
            GradeSeparationClass separation)
        {
            TunnelCoverClass coverClass = cover != null ? cover : TunnelCoverClass.Default();
            TunnelClass tunnelClass = tunnel != null ? tunnel : new TunnelClass();

            TunnelLinkPlan plan = new TunnelLinkPlan();
            plan.opening = TerrainRegion.Empty;
            plan.firstPortal = TerrainRegion.Empty;
            plan.secondPortal = TerrainRegion.Empty;
            plan.span = TerrainRegion.Empty;
            plan.refusal = string.Empty;

            if (run.lengthMetres <= 0.0)
            {
                plan.refusal = "the tunnel has no length, so there is nothing to open";
                plan.outcome = TunnelBuildOutcome.RefusedInsufficientCover;
                return plan;
            }

            TunnelTerrainPolicy.TunnelOpeningBox box = TunnelTerrainPolicy.OpeningFor(run.centreX, run.centreZ,
                run.lengthMetres, run.roadSurfaceElevationMetres, run.pavementThicknessMetres, run.alongX,
                tunnelClass, coverClass, separation);

            plan.geometry.floorElevationMetres = box.floorElevationMetres;
            plan.geometry.crownElevationMetres = box.crownElevationMetres;
            plan.geometry.widthMetres = box.halfAcrossRoadMetres * 2.0;
            plan.geometry.lengthMetres = box.SpanAlongRoadMetres;
            plan.geometry.openAlongX = box.openAlongX;

            plan.requiredCoverMetres = TunnelTerrainPolicy.RequiredCoverMetres(run.underWater, tunnelClass,
                coverClass);

            // Cover is measured down to the top of the STRUCTURE, which for a bore is the crown of the excavation and
            // not the roof of a box that is not being built. Measuring from the box crown reads about a metre and a half
            // too generous on the default bore, and that leniency is exactly the amount that would let a bore under a
            // river bed pass the minimum-cover test with its excavation inside the minimum.
            double structureCrown = TunnelTerrainPolicy.StructureCrownElevationMetres(
                run.roadSurfaceElevationMetres, run.pavementThicknessMetres, run.immersedTube, tunnelClass, coverClass,
                separation);
            plan.presentCoverMetres = run.tightestGroundElevationMetres - structureCrown;

            // Under water the host's own minimum decides, and it is asked in the host's own words: one station, the
            // tightest, scanned with the tunnel class that owns the figure.
            double[] bed = new double[] { run.tightestGroundElevationMetres };
            double[] crown = new double[] { structureCrown };
            TunnelCavity.CoverScan scan = TunnelCavity.ScanWaterCover(bed, crown, tunnelClass);
            plan.outcome = TunnelCavity.ResolveBuild(mode, run.underWater, scan);

            if (plan.outcome == TunnelBuildOutcome.RefusedInsufficientCover)
            {
                plan.refusal = string.Format(CultureInfo.InvariantCulture,
                    "only {0:0.###} m of cover over the crown where {1:0.###} m is required under water -- the bore "
                    + "would breach the bed", plan.presentCoverMetres, plan.requiredCoverMetres);
                return plan;
            }

            plan.viable = true;
            plan.opening = TunnelTerrainPolicy.OpeningRegion(box);

            double notch = TunnelTerrainPolicy.PortalNotchLengthMetres(tunnelClass, coverClass);
            double halfNotch = notch * 0.5;
            double halfRun = run.lengthMetres * 0.5;
            double portalHalfWidth = run.corridorHalfWidthMetres > box.halfAcrossRoadMetres
                ? run.corridorHalfWidthMetres : box.halfAcrossRoadMetres;

            plan.firstPortal = PortalRegion(run, box, -halfRun - halfNotch, halfNotch, portalHalfWidth);
            plan.secondPortal = PortalRegion(run, box, halfRun + halfNotch, halfNotch, portalHalfWidth);

            // The span. Three cases, and only one of them is announced as ground the host may work on.
            double spanHalfWidth = portalHalfWidth;
            if (run.underWater && run.immersedTube)
            {
                spanHalfWidth = TunnelTerrainPolicy.DredgedTrenchHalfWidthMetres(box.halfAcrossRoadMetres * 2.0,
                    run.burialDepthMetres, coverClass);
                plan.span = SpanRegion(run, box, halfRun, spanHalfWidth, run.tightestGroundElevationMetres)
                    .WithTreatment(GroundTreatment.RestoreAfterBackfill);
                plan.spanIsUntouched = false;
            }
            else if (plan.presentCoverMetres >= plan.requiredCoverMetres)
            {
                plan.span = SpanRegion(run, box, halfRun, spanHalfWidth, run.tightestGroundElevationMetres)
                    .WithTreatment(GroundTreatment.LeaveIntact);
                plan.spanIsUntouched = true;
            }
            else
            {
                double target = TunnelTerrainPolicy.CoverFillTargetElevationMetres(run.roadSurfaceElevationMetres,
                    run.underWater, tunnelClass, coverClass, separation);
                plan.span = SpanRegion(run, box, halfRun, spanHalfWidth, target)
                    .WithTreatment(GroundTreatment.CoverFill);
                plan.spanIsUntouched = false;
            }

            return plan;
        }

        /// <summary> One portal notch: a box of bounded length at the given offset along the road, from the opening
        /// floor up to the natural ground, wide enough for the works. </summary>
        private static TerrainRegion PortalRegion(TunnelRun run, TunnelTerrainPolicy.TunnelOpeningBox box,
            double offsetAlongRoad, double halfLength, double halfWidth)
        {
            double centreX = run.centreX + (run.alongX ? offsetAlongRoad : 0.0);
            double centreZ = run.centreZ + (run.alongX ? 0.0 : offsetAlongRoad);
            double halfX = run.alongX ? halfLength : halfWidth;
            double halfZ = run.alongX ? halfWidth : halfLength;
            double top = run.tightestGroundElevationMetres > box.crownElevationMetres
                ? run.tightestGroundElevationMetres : box.crownElevationMetres;
            TerrainRegion r = TerrainRegion.FromExtents(centreX - halfX, box.floorElevationMetres, centreZ - halfZ,
                centreX + halfX, top, centreZ + halfZ);
            return r.WithTreatment(GroundTreatment.PortalNotch);
        }

        /// <summary> The ground over the bore, between the portals. </summary>
        private static TerrainRegion SpanRegion(TunnelRun run, TunnelTerrainPolicy.TunnelOpeningBox box,
            double halfLength, double halfWidth, double topElevationMetres)
        {
            double halfX = run.alongX ? halfLength : halfWidth;
            double halfZ = run.alongX ? halfWidth : halfLength;
            double top = topElevationMetres > box.crownElevationMetres ? topElevationMetres : box.crownElevationMetres;
            return TerrainRegion.FromExtents(run.centreX - halfX, box.crownElevationMetres, run.centreZ - halfZ,
                run.centreX + halfX, top, run.centreZ + halfZ);
        }
        #endregion


        #region "Executing the plan against the link"
        /// <summary> What executing the plan produced, so the caller can release the opening later and a gate can read
        /// what was asked of the host. </summary>
        public struct TunnelLinkResult
        {
            public bool executed;
            public string refusal;
            public TerrainRequestHandle openingHandle;

            /// <summary> How many regions the host was told about. A bore with adequate cover announces its two portal
            /// notches and NOT the span, so this is 2 -- and a gate reading 3 has caught the defect coming back.
            /// </summary>
            public int announcedRegionCount;

            public string Describe()
            {
                if (!executed) { return "not executed: " + (refusal != null ? refusal : "no reason given"); }
                return string.Format(CultureInfo.InvariantCulture, "opening {0}, {1} region(s) announced",
                    openingHandle.IsValid ? "granted" : "refused", announcedRegionCount);
            }
        }

        /// <summary> Carry the plan across the link.
        ///
        /// The order matters. The opening is requested FIRST, because it is the only request that must survive a
        /// regeneration and the host keys its re-stamping off the hold; announcing changed ground first would let a
        /// regeneration land between the two and fill the bore back in. The portal notches are announced next, as
        /// ordinary claimed ground, because they genuinely are. The span is announced only when something happens to it.
        /// </summary>
        public static TunnelLinkResult Execute(TerrainWriteGateway gateway, TunnelLinkPlan plan, string requesterId)
        {
            TunnelLinkResult result = new TunnelLinkResult();
            result.refusal = string.Empty;

            if (gateway == null)
            {
                result.refusal = "no terrain gateway, so nothing can be asked of the host";
                return result;
            }
            if (!plan.viable)
            {
                result.refusal = plan.refusal;
                return result;
            }

            string id = string.IsNullOrEmpty(requesterId) ? "rcm.tunnel" : requesterId;
            string refusal;
            result.openingHandle = gateway.RequestTunnelOpening(plan.opening, id + ".opening", out refusal);
            if (!result.openingHandle.IsValid)
            {
                result.refusal = refusal;
                return result;
            }

            List<TerrainRegion> announce = new List<TerrainRegion>();
            if (plan.firstPortal.hasArea) { announce.Add(plan.firstPortal); }
            if (plan.secondPortal.hasArea) { announce.Add(plan.secondPortal); }
            if (!plan.spanIsUntouched && plan.span.hasArea) { announce.Add(plan.span); }

            for (int i = 0; i < announce.Count; i++)
            {
                string cutRefusal;
                TerrainRequestHandle handle = gateway.RequestCutRegion(announce[i], id + ".portal", out cutRefusal);
                if (handle.IsValid) { gateway.ReleaseCutRegion(handle); }
            }

            result.announcedRegionCount = announce.Count;
            result.executed = true;
            return result;
        }
        #endregion
    }
}

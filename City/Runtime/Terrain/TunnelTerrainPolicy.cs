#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What the ground along one station of a road is supposed to have done to it. This is the field that
    /// travels with a <see cref="TerrainRegion"/> across the linker, and it is the whole answer to "why did my hill
    /// disappear when I made that segment a tunnel": a region described without it is a region the host is entitled to
    /// flatten. </summary>
    public enum GroundTreatment
    {
        /// <summary> Cut and fill to the road surface: the ordinary at-grade case. </summary>
        Conform = 0,

        /// <summary> Do not touch this ground at all. A bored tunnel's span: the road is inside the hill, so the hill
        /// stays exactly as the world generator made it. Cutting here is the reported defect. </summary>
        LeaveIntact = 1,

        /// <summary> Raise the ground to the minimum cover over the tunnel roof, and NEVER lower it. A cut-and-cover
        /// box under an embankment: where the natural surface is already deeper than the cover requires, nothing is
        /// done. </summary>
        CoverFill = 2,

        /// <summary> The short length of real excavation at a portal, where the headwall meets daylight. The only place
        /// a tunnel is permitted to cut into a hillside. </summary>
        PortalNotch = 3,

        /// <summary> Dredge, lay, backfill: an immersed tube's trench, whose FINAL bed elevation is the one that was
        /// there before. The host is being told the bed will end up unchanged, so it must not treat the trench as the
        /// new surface. </summary>
        RestoreAfterBackfill = 4,

        /// <summary> Hold flat against regeneration: a building pad (RM-22), stated here so the treatment enum is the
        /// single vocabulary the linker speaks rather than two overlapping ones. </summary>
        HoldFlatPad = 5
    }


    /// <summary> RM-98/RM-99. The cover figures that decide whether a tunnel can exist where it was drawn, and
    /// therefore whether the ground above it is cut, filled or left alone.
    ///
    /// Every number here is a real tunnelling figure with its reasoning beside it, and the two that already live
    /// elsewhere are READ from there rather than restated: the clear-bore radius and the minimum water cover belong to
    /// <see cref="TunnelClass"/> (RM-37), and the vertical clearance belongs to <see cref="GradeSeparationClass"/>
    /// (RM-35). Restating either would let a project retune one and silently disagree with the other. </summary>
    [Serializable]
    public sealed class TunnelCoverClass
    {
        /// <summary> Ground over the crown of a BORED tunnel, expressed in excavated diameters.
        ///
        /// One diameter is the ordinary shallow-bore limit: with less ground than that above the crown the arch cannot
        /// mobilise, the face is at risk of breaking upward, and the job stops being a bore and becomes a
        /// cut-and-cover box. It is a ratio rather than a length because it scales with the tunnel -- a 4 m bore under
        /// 4 m of rock is shallow; a 12 m bore under 4 m of rock is a trench with a lid. </summary>
        public double boredCoverDiameters = 1.0;

        /// <summary> Fill over the roof of a CUT-AND-COVER box, in metres. This is the depth that carries what goes on
        /// top -- pavement build-up, drainage, buried services, and enough soil that the structure is not exposed by
        /// ordinary landscaping. Road cut-and-cover boxes are routinely built with a metre and a half of it, and less
        /// than that is a roof pretending to be ground. </summary>
        public double minimumSoilCoverMetres = 1.5;

        /// <summary> The structural roof between the traffic envelope and the ground above it, in metres. A reinforced
        /// concrete roof slab spanning a two-lane road box is about this thick; it is stated separately from the cover
        /// because it is structure, not ground, and the crown elevation the cover is measured from sits on top of it.
        /// </summary>
        public double roofThicknessMetres = 0.9;

        /// <summary> How far a portal is allowed to cut into the hillside, in outer diameters. A portal needs a face to
        /// build the headwall against and a short approach cut to reach it; two diameters is that face and no more, so
        /// a tunnel entrance cannot quietly become a long open cutting through the hill it was supposed to pass under.
        /// </summary>
        public double portalNotchDiameters = 2.0;

        /// <summary> Slope of the sides of an immersed tube's dredged trench, run over rise. Dredged slopes in soft
        /// marine soils stand at about one in three, and the trench footprint the host is told about has to include
        /// them or the region is narrower than the work. </summary>
        public double dredgedTrenchSideSlope = 3.0;

        /// <summary> Depth from the finished carriageway down to the top of the structural invert, in metres -- the
        /// tunnel's internal build-up: wearing course, binder, road base, and the drainage and cable ducts that run
        /// under a road tunnel's slab rather than beside it.
        ///
        /// It lives here beside <see cref="roofThicknessMetres"/> because the two are the same kind of fact seen from
        /// opposite ends: the roof is the structure above the traffic envelope, this is the structure below it, and the
        /// bore has to be big enough to contain both. A road tunnel is built with something in the region of three
        /// quarters of a metre of it -- appreciably more than an open road's surfacing, because the services that sit in
        /// a verge outside go under the slab inside.
        ///
        /// This is what <see cref="InvertElevationMetres"/> subtracts, so it is also what decides how deep the
        /// excavation has to go for a given carriageway level. </summary>
        public double pavementBuildUpMetres = 0.8;

        public static TunnelCoverClass Default()
        {
            return new TunnelCoverClass();
        }
    }


    /// <summary> RM-98/RM-99's answer to the author's report that a tunnel cuts the hill it was supposed to pass
    /// through.
    ///
    /// The legacy terraforming already refuses to LOWER ground over a tunnel -- it raises ground that sits below the
    /// road and leaves higher ground alone -- but it does so against a 0.03 m constant, which is a rendering nudge to
    /// hide the road mesh, not a cover requirement. Three centimetres of soil over a road tunnel is not a tunnel; the
    /// hill above it is still whatever the world generator drew, and the moment the corridor is described to the host
    /// as changed ground, the host is entitled to conform it. So the fix has two halves and both are here:
    ///
    /// 1. The cover the ground must actually have (<see cref="RequiredCoverMetres"/>), measured from the CROWN of the
    ///    structure -- road surface, plus the traffic clearance, plus the roof -- not from the road surface.
    /// 2. The treatment the region is labelled with (<see cref="ClassifyStation"/>), so that a bore's span crosses the
    ///    linker as <see cref="GroundTreatment.LeaveIntact"/> and neither side of the link can conform it away.
    ///
    /// Everything is a function of its arguments, in <c>double</c>, so both the standalone and the connected path can
    /// be asserted against the same numbers headless (C-06). </summary>
    public static class TunnelTerrainPolicy
    {
        #region "Elevations through the structure"
        /// <summary> The traffic envelope's height above the road surface: what the clearance sign at the portal says.
        /// Read from RM-35's grade-separation class so a project that retunes clearance for rail retunes both the
        /// flyover and the tunnel at once. </summary>
        public static double ClearanceMetres(GradeSeparationClass separation)
        {
            return separation != null ? separation.verticalClearanceMetres : 5.1;
        }

        /// <summary> World elevation of the top of a CUT-AND-COVER BOX over a road surface: clearance, then roof. This
        /// -- not the road surface -- is what cover is measured from, which is the single most consequential difference
        /// between this file and the constant it replaces. </summary>
        public static double CrownElevationMetres(double roadSurfaceElevationMetres, GradeSeparationClass separation,
            TunnelCoverClass cover)
        {
            double roof = cover != null ? cover.roofThicknessMetres : 0.9;
            return roadSurfaceElevationMetres + ClearanceMetres(separation) + roof;
        }

        /// <summary> World elevation of the invert -- the bottom of the excavation -- under a road surface. The road
        /// does not sit on bare rock: the pavement build-up is between them, and the same figure puts the floor of the
        /// opening the host carves in the right place. </summary>
        public static double InvertElevationMetres(double roadSurfaceElevationMetres, double pavementThicknessMetres)
        {
            double pavement = pavementThicknessMetres > 0.0 ? pavementThicknessMetres : 0.0;
            return roadSurfaceElevationMetres - pavement;
        }

        /// <summary> World elevation of the top of a BORE over a road surface: the invert plus one excavated diameter,
        /// because that is what a circular bore is. A bore large enough to carry a road is taller than the box that
        /// would carry the same road, so its crown is higher and the cover over it is measured from higher up -- using
        /// the box's crown for a bore would report cover that is not there. </summary>
        public static double BoreCrownElevationMetres(double roadSurfaceElevationMetres,
            double pavementThicknessMetres, TunnelClass tunnel)
        {
            return InvertElevationMetres(roadSurfaceElevationMetres, pavementThicknessMetres)
                + ExcavatedDiameterMetres(tunnel);
        }

        /// <summary> The excavated outer diameter of a bore: the clear bore plus the lining, doubled. Uses RM-37's own
        /// lining maths rather than a second copy of it. </summary>
        public static double ExcavatedDiameterMetres(TunnelClass tunnel)
        {
            TunnelClass cls = tunnel != null ? tunnel : new TunnelClass();
            double outer = TunnelCavity.LiningOuterRadiusMetres(cls.boreRadiusMetres, cls);
            return outer * 2.0;
        }

        /// <summary> The top of the STRUCTURE -- the surface cover is actually measured down to.
        ///
        /// Which of the two crowns that is depends on what is built. An immersed tube or a cut-and-cover box tops out at
        /// its roof slab, <see cref="CrownElevationMetres"/>. A bore tops out at the crown of the excavation,
        /// <see cref="BoreCrownElevationMetres"/>, which for the default 4 m bore sits about a metre and a half HIGHER
        /// than the box roof would. Measure a bore's cover from the box crown and the answer is a metre and a half too
        /// generous -- enough to let a bore under a river bed read as legal while its excavation is actually inside the
        /// minimum cover. So the greater of the two is the honest one, and it is taken here rather than at each call
        /// site. </summary>
        public static double StructureCrownElevationMetres(double roadSurfaceElevationMetres,
            double pavementThicknessMetres, bool immersedTube, TunnelClass tunnel, TunnelCoverClass cover,
            GradeSeparationClass separation)
        {
            double box = CrownElevationMetres(roadSurfaceElevationMetres, separation, cover);
            if (immersedTube) { return box; }
            double bore = BoreCrownElevationMetres(roadSurfaceElevationMetres, pavementThicknessMetres, tunnel);
            return bore > box ? bore : box;
        }
        #endregion


        #region "How much cover the ground must have"
        /// <summary> The ground a BORE needs over its crown before it can be driven rather than dug, in metres: one
        /// excavated diameter. This is a TEST, not a target -- ground short of it does not get filled up to it, it
        /// stops being a bore and becomes a box. That distinction is what keeps a tunnel toggled on flat ground from
        /// raising a nine-metre mound over itself. </summary>
        public static double RequiredBoredCoverMetres(TunnelClass tunnel, TunnelCoverClass cover)
        {
            double diameters = cover != null ? cover.boredCoverDiameters : 1.0;
            return ExcavatedDiameterMetres(tunnel) * diameters;
        }

        /// <summary> The cover that must END UP over a tunnel's crown, in metres -- the figure ground is actually filled
        /// to.
        ///
        /// Under water it is RM-37's minimum water cover, because the failure there is the river coming through the
        /// roof, and that minimum belongs to <see cref="TunnelClass"/>. On land it is the cut-and-cover fill depth:
        /// pavement build-up, drainage, buried services and enough soil that ordinary landscaping does not expose the
        /// structure. It is deliberately NOT the bored figure -- see <see cref="RequiredBoredCoverMetres"/> for why
        /// those are two different questions. </summary>
        public static double RequiredCoverMetres(bool underWater, TunnelClass tunnel, TunnelCoverClass cover)
        {
            if (underWater)
            {
                return tunnel != null ? tunnel.minWaterCoverMetres : 3.0;
            }
            return cover != null ? cover.minimumSoilCoverMetres : 1.5;
        }

        /// <summary> Cover actually present at a station: ground surface less crown. Negative means the crown is out in
        /// the open air, which is a portal or a mistake. </summary>
        public static double CoverAtStationMetres(double groundElevationMetres, double roadSurfaceElevationMetres,
            GradeSeparationClass separation, TunnelCoverClass cover)
        {
            return groundElevationMetres - CrownElevationMetres(roadSurfaceElevationMetres, separation, cover);
        }

        /// <summary> The ground elevation a <see cref="GroundTreatment.CoverFill"/> station is filled UP to. Never a
        /// target to cut down to: see <see cref="ClassifyStation"/>. </summary>
        public static double CoverFillTargetElevationMetres(double roadSurfaceElevationMetres, bool underWater,
            TunnelClass tunnel, TunnelCoverClass cover, GradeSeparationClass separation)
        {
            return CrownElevationMetres(roadSurfaceElevationMetres, separation, cover)
                + RequiredCoverMetres(underWater, tunnel, cover);
        }

        /// <summary> How far above the road surface the ground must end up over a tunnel, in metres: clearance, roof
        /// and cover.
        ///
        /// This is the figure the vendored threaded terraforming reads. It replaced a literal 0.03 -- three centimetres,
        /// which was a rendering nudge to keep the road mesh from poking through the terrain, not a cover depth. Three
        /// centimetres of soil over a road tunnel is why a hill looked sliced open where the tunnel was supposed to pass
        /// under it. Being a function of the same two classes the rest of the policy reads, a project that retunes
        /// clearance retunes what the ground does at the same time. </summary>
        public static double TunnelGroundTopAboveRoadMetres(bool underWater, TunnelClass tunnel,
            TunnelCoverClass cover, GradeSeparationClass separation)
        {
            return CoverFillTargetElevationMetres(0.0, underWater, tunnel, cover, separation);
        }

        /// <summary> How far into the hillside a portal may cut, in metres. </summary>
        public static double PortalNotchLengthMetres(TunnelClass tunnel, TunnelCoverClass cover)
        {
            double diameters = cover != null ? cover.portalNotchDiameters : 2.0;
            return ExcavatedDiameterMetres(tunnel) * diameters;
        }

        /// <summary> Half-width of the ground a dredged trench disturbs at bed level, for an immersed tube of a given
        /// outer width buried to a given depth below the bed. The side slopes are part of the footprint: a 12 m tube
        /// 6 m down in one-in-three material disturbs 24 m of bed, not 12. </summary>
        public static double DredgedTrenchHalfWidthMetres(double tubeOuterWidthMetres, double burialDepthMetres,
            TunnelCoverClass cover)
        {
            double slope = cover != null ? cover.dredgedTrenchSideSlope : 3.0;
            if (slope < 0.0) { slope = 0.0; }
            double depth = burialDepthMetres > 0.0 ? burialDepthMetres : 0.0;
            return tubeOuterWidthMetres * 0.5 + depth * slope;
        }
        #endregion


        #region "Which treatment a station gets"
        /// <summary> Where along the tunnel a station sits. Supplied by the caller from the spline's own tunnel
        /// start/end nodes, so this file does not need a spline and can be driven from a table in a gate. </summary>
        public struct StationContext
        {
            /// <summary> True when this station lies between a tunnel start node and its matched end node. </summary>
            public bool insideTunnel;

            /// <summary> Distance along the road from this station to the nearer portal, in metres. Only meaningful
            /// when <see cref="insideTunnel"/> is true. </summary>
            public double distanceToNearerPortalMetres;

            /// <summary> True when the tunnel passes under a water body here -- a river, lake or seabed above the
            /// crown rather than soil or rock. </summary>
            public bool underWater;

            /// <summary> True when the tunnel under water is an immersed tube laid in a dredged trench and backfilled,
            /// rather than a bore driven through the ground beneath the bed. </summary>
            public bool immersedTube;

            /// <summary> Natural ground (or bed) elevation at the station, before anything is built. </summary>
            public double groundElevationMetres;

            /// <summary> Finished road surface elevation at the station. </summary>
            public double roadSurfaceElevationMetres;

            /// <summary> Pavement build-up under the carriageway, so the invert -- and therefore a bore's crown -- lands
            /// in the right place. Zero is treated as no build-up rather than as an error. </summary>
            public double pavementThicknessMetres;
        }

        /// <summary> The treatment one station's ground gets. This is the rule the author's complaint reduces to, and
        /// it is short on purpose:
        ///
        /// * Not in a tunnel -> <see cref="GroundTreatment.Conform"/>. Nothing about tunnels applies.
        /// * Within a portal notch of daylight -> <see cref="GroundTreatment.PortalNotch"/>. This is the one place a
        ///   tunnel cuts ground, and it is bounded by <see cref="PortalNotchLengthMetres"/> rather than running as far
        ///   as the corridor happens to reach.
        /// * An immersed tube under water -> <see cref="GroundTreatment.RestoreAfterBackfill"/>. The trench is real
        ///   work but it is filled in again; the bed the host must plan around is the one that was already there.
        /// * Enough cover already -> <see cref="GroundTreatment.LeaveIntact"/>. THE HILL IS NOT TOUCHED. Not conformed,
        ///   not smoothed, not "conformed with a tolerance" -- untouched, so that the region crossing the linker
        ///   carries a label the host cannot honour by flattening it.
        /// * Not enough cover -> <see cref="GroundTreatment.CoverFill"/>. The box is built and an embankment is put
        ///   over it, which is what is actually done on site when the alignment runs shallow. </summary>
        public static GroundTreatment ClassifyStation(StationContext station, TunnelClass tunnel,
            TunnelCoverClass cover, GradeSeparationClass separation)
        {
            if (!station.insideTunnel) { return GroundTreatment.Conform; }

            if (station.distanceToNearerPortalMetres <= PortalNotchLengthMetres(tunnel, cover))
            {
                return GroundTreatment.PortalNotch;
            }
            if (station.underWater && station.immersedTube)
            {
                return GroundTreatment.RestoreAfterBackfill;
            }

            // Is this a bore? One excavated diameter of ground over the BORE crown says yes, and a bore's ground is
            // never touched at all.
            double boreCover = station.groundElevationMetres - BoreCrownElevationMetres(
                station.roadSurfaceElevationMetres, station.pavementThicknessMetres, tunnel);
            if (boreCover >= RequiredBoredCoverMetres(tunnel, cover)) { return GroundTreatment.LeaveIntact; }

            // Not a bore, so it is a box. Ground already above the finished cover is still left alone -- being short of
            // the BORED requirement is not a reason to move ground that already covers the roof properly.
            double target = CoverFillTargetElevationMetres(station.roadSurfaceElevationMetres, station.underWater,
                tunnel, cover, separation);
            return station.groundElevationMetres >= target ? GroundTreatment.LeaveIntact : GroundTreatment.CoverFill;
        }

        /// <summary> True when a treatment permits LOWERING the natural surface. Exactly one of them does, and a
        /// single call answering this question is what stops the next terrain path from re-deciding it. </summary>
        public static bool MayLowerGround(GroundTreatment treatment)
        {
            return treatment == GroundTreatment.Conform || treatment == GroundTreatment.PortalNotch;
        }

        /// <summary> True when a treatment permits RAISING the natural surface. </summary>
        public static bool MayRaiseGround(GroundTreatment treatment)
        {
            return treatment == GroundTreatment.Conform || treatment == GroundTreatment.CoverFill
                || treatment == GroundTreatment.PortalNotch || treatment == GroundTreatment.HoldFlatPad;
        }

        /// <summary> True when the ground under this treatment ENDS UP exactly where it started, so there is nothing for
        /// the host to be told about.
        ///
        /// This is the predicate <see cref="TerrainWriteGateway.EndBuild"/> reads to decide what NOT to announce, and it
        /// is deliberately the same fact <see cref="FinishedGroundElevationMetres"/> implements: those two treatments are
        /// exactly the ones that method returns the incoming ground elevation for, bit for bit. Keeping one owner of the
        /// fact means the announcement and the arithmetic can never drift apart and start disagreeing about whether a
        /// hill moved.
        ///
        /// <see cref="GroundTreatment.LeaveIntact"/> is the hill a bore passes under: untouched, so unannounced.
        /// <see cref="GroundTreatment.RestoreAfterBackfill"/> is an immersed tube's trench, which is dredged, the tube
        /// floated in, and then backfilled to the original bed line -- the works are real but the finished bed is the bed
        /// that was there, so announcing it as changed ground would invite the host to conform a river bed to the tube.
        /// </summary>
        public static bool GroundEndsUnchanged(GroundTreatment treatment)
        {
            return treatment == GroundTreatment.LeaveIntact || treatment == GroundTreatment.RestoreAfterBackfill;
        }

        /// <summary> How strongly a treatment protects the ground it labels: higher protects more.
        ///
        /// The order is not a preference, it is what each treatment permits.
        /// <see cref="GroundTreatment.LeaveIntact"/> permits nothing, so it protects most.
        /// <see cref="GroundTreatment.RestoreAfterBackfill"/> permits work that ends where it started, so the FINAL
        /// surface is equally untouched -- but it is one step less protective because the ground is genuinely disturbed
        /// in between and a host that streams may see the trench. <see cref="GroundTreatment.CoverFill"/> and
        /// <see cref="GroundTreatment.HoldFlatPad"/> may raise but never lower. <see cref="GroundTreatment.PortalNotch"/>
        /// may cut, but only over a bounded length. <see cref="GroundTreatment.Conform"/> may do anything, so it
        /// protects least. </summary>
        public static int ProtectionRank(GroundTreatment treatment)
        {
            switch (treatment)
            {
                case GroundTreatment.LeaveIntact: return 5;
                case GroundTreatment.RestoreAfterBackfill: return 4;
                case GroundTreatment.CoverFill: return 3;
                case GroundTreatment.HoldFlatPad: return 2;
                case GroundTreatment.PortalNotch: return 1;
                default: return 0;
            }
        }

        /// <summary> The one of two treatments that protects the ground more. Used when a box has to stand for a span
        /// carrying both -- see <see cref="TerrainRegion.Union"/> for why the answer must lean this way. </summary>
        public static GroundTreatment MoreProtective(GroundTreatment first, GroundTreatment second)
        {
            return ProtectionRank(first) >= ProtectionRank(second) ? first : second;
        }
        #endregion


        #region "The box the host is asked to open"
        /// <summary> The world box a tunnel opening is requested over, in the shape the host's carver expects.
        ///
        /// The host removes surface inside this box, hangs a wall skirt from the cut edge down to
        /// <see cref="floorElevationMetres"/>, and lays a collider floor across the footprint -- so the box is not a
        /// bounding hint, it IS the opening, and getting its bottom wrong is what leaves a road hanging over a void or a
        /// skirt buried in the ground.
        ///
        /// It is stated in metres about a centre point with half-extents rather than as an engine box, so the maths is
        /// testable headless and the engine type appears only where the region is handed over (C-06). </summary>
        public struct TunnelOpeningBox
        {
            public double centreX;
            public double centreZ;
            public double halfAlongRoadMetres;
            public double halfAcrossRoadMetres;

            /// <summary> Underside of the opening: the tunnel's structural floor, which is BELOW the road surface by the
            /// pavement build-up. The host's skirt hangs to here. </summary>
            public double floorElevationMetres;

            /// <summary> Top of the opening: the crown. The host removes surface above the road only as far as this, so
            /// a hill that is taller than the crown keeps its summit. </summary>
            public double crownElevationMetres;

            /// <summary> True when the road runs along world X, so the host leaves the X faces open and walls the
            /// other two. The host derives this from the box proportions on its own, and an elongated box gives it the
            /// right answer -- this field records what we intended so a gate can check the two agree. </summary>
            public bool openAlongX;

            public double SpanAlongRoadMetres { get { return halfAlongRoadMetres * 2.0; } }
            public double HeightMetres { get { return crownElevationMetres - floorElevationMetres; } }

            public string Describe()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "opening centre({0:0.##},{1:0.##}) {2:0.##} x {3:0.##} m, y[{4:0.###}..{5:0.###}], open along {6}",
                    centreX, centreZ, SpanAlongRoadMetres, halfAcrossRoadMetres * 2.0, floorElevationMetres,
                    crownElevationMetres, openAlongX ? "X" : "Z");
            }
        }

        /// <summary> Build the opening box for one tunnel run.
        ///
        /// Width across the road is the excavated diameter, not the carriageway width: the opening has to clear the
        /// lining, or the skirt lands inside the tunnel shell. Height runs from the structural floor -- road surface
        /// less the pavement build-up -- to the crown, which is the clearance and roof above the road surface. Both come
        /// from the same two files the rest of this policy reads, so a project that retunes clearance retunes the hole
        /// it digs at the same time. </summary>
        public static TunnelOpeningBox OpeningFor(double centreX, double centreZ, double runLengthMetres,
            double roadSurfaceElevationMetres, double pavementThicknessMetres, bool roadRunsAlongX,
            TunnelClass tunnel, TunnelCoverClass cover, GradeSeparationClass separation)
        {
            TunnelOpeningBox box;
            box.centreX = centreX;
            box.centreZ = centreZ;
            box.halfAlongRoadMetres = (runLengthMetres > 0.0 ? runLengthMetres : 0.0) * 0.5;
            box.halfAcrossRoadMetres = ExcavatedDiameterMetres(tunnel) * 0.5;
            double pavement = pavementThicknessMetres > 0.0 ? pavementThicknessMetres : 0.0;
            box.floorElevationMetres = roadSurfaceElevationMetres - pavement;
            box.crownElevationMetres = CrownElevationMetres(roadSurfaceElevationMetres, separation, cover);
            box.openAlongX = roadRunsAlongX;
            return box;
        }

        /// <summary> The opening as a region, labelled so the host knows it is a tunnel and not a cutting. The label
        /// travels with it: this is the region the bridge hands to <c>RequestHold</c>, and the treatment on it is what
        /// stops the same ground being announced later as ordinary changed ground. </summary>
        public static TerrainRegion OpeningRegion(TunnelOpeningBox box)
        {
            double halfX = box.openAlongX ? box.halfAlongRoadMetres : box.halfAcrossRoadMetres;
            double halfZ = box.openAlongX ? box.halfAcrossRoadMetres : box.halfAlongRoadMetres;
            TerrainRegion r = TerrainRegion.FromExtents(
                box.centreX - halfX, box.floorElevationMetres, box.centreZ - halfZ,
                box.centreX + halfX, box.crownElevationMetres, box.centreZ + halfZ);
            return r.WithTreatment(GroundTreatment.LeaveIntact);
        }
        #endregion


        #region "The elevation a station ends up at"

        /// <summary> The elevation the ground ends up at, given the treatment. The one function the terraforming path
        /// calls per sample, so that "a tunnel does not cut the hill" is enforced in arithmetic rather than in a
        /// comment: for <see cref="GroundTreatment.LeaveIntact"/> and
        /// <see cref="GroundTreatment.RestoreAfterBackfill"/> the answer is the natural elevation, bit for bit.
        /// </summary>
        public static double FinishedGroundElevationMetres(GroundTreatment treatment, StationContext station,
            double conformTargetElevationMetres, TunnelClass tunnel, TunnelCoverClass cover,
            GradeSeparationClass separation)
        {
            if (treatment == GroundTreatment.LeaveIntact
                || treatment == GroundTreatment.RestoreAfterBackfill)
            {
                return station.groundElevationMetres;
            }
            if (treatment == GroundTreatment.CoverFill)
            {
                double target = CoverFillTargetElevationMetres(station.roadSurfaceElevationMetres, station.underWater,
                    tunnel, cover, separation);
                return station.groundElevationMetres > target ? station.groundElevationMetres : target;
            }
            return conformTargetElevationMetres;
        }
        #endregion


        #region "Scanning a whole tunnel"
        /// <summary> What a scan of a whole tunnel found. </summary>
        public struct TunnelGroundScan
        {
            /// <summary> True when no station inside the tunnel would have its ground lowered. </summary>
            public bool hillIntact;

            /// <summary> The first station index where a cut would have happened, or -1. </summary>
            public int firstCutStationIndex;

            /// <summary> How many stations are left alone, filled over, notched at a portal, or backfilled. </summary>
            public int leftIntactCount;
            public int coverFillCount;
            public int portalNotchCount;
            public int backfillCount;

            /// <summary> The least cover found anywhere inside the tunnel, and where. </summary>
            public double worstCoverMetres;
            public int worstCoverStationIndex;

            public string Describe()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "intact {0} fill {1} portal {2} backfill {3}, worst cover {4:0.###} m at station {5}, "
                    + "hill {6}", leftIntactCount, coverFillCount, portalNotchCount, backfillCount, worstCoverMetres,
                    worstCoverStationIndex, hillIntact ? "intact" : "CUT at station " + firstCutStationIndex);
            }
        }

        /// <summary> Classify every station of a tunnel and report what the ground does. The gate's headline assertion
        /// is <see cref="TunnelGroundScan.hillIntact"/>: over a bore with adequate cover, not one station may be
        /// lowered.
        ///
        /// <paramref name="conformTargets"/> is what the ordinary at-grade path WOULD have written at each station --
        /// the trench through the hill -- so the scan can prove the tunnel path departed from it rather than merely
        /// claiming a different intent. </summary>
        public static TunnelGroundScan ScanTunnelGround(IList<StationContext> stations,
            IList<double> conformTargets, TunnelClass tunnel, TunnelCoverClass cover,
            GradeSeparationClass separation)
        {
            TunnelGroundScan scan;
            scan.hillIntact = true;
            scan.firstCutStationIndex = -1;
            scan.leftIntactCount = 0;
            scan.coverFillCount = 0;
            scan.portalNotchCount = 0;
            scan.backfillCount = 0;
            scan.worstCoverMetres = double.MaxValue;
            scan.worstCoverStationIndex = -1;

            if (stations == null || stations.Count == 0)
            {
                scan.worstCoverMetres = 0.0;
                return scan;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                StationContext s = stations[i];
                if (!s.insideTunnel) { continue; }

                GroundTreatment treatment = ClassifyStation(s, tunnel, cover, separation);
                if (treatment == GroundTreatment.LeaveIntact) { scan.leftIntactCount++; }
                else if (treatment == GroundTreatment.CoverFill) { scan.coverFillCount++; }
                else if (treatment == GroundTreatment.PortalNotch) { scan.portalNotchCount++; }
                else if (treatment == GroundTreatment.RestoreAfterBackfill) { scan.backfillCount++; }

                double conform = conformTargets != null && i < conformTargets.Count
                    ? conformTargets[i] : s.groundElevationMetres;
                double finished = FinishedGroundElevationMetres(treatment, s, conform, tunnel, cover, separation);
                if (finished < s.groundElevationMetres && treatment != GroundTreatment.PortalNotch)
                {
                    if (scan.hillIntact) { scan.firstCutStationIndex = i; }
                    scan.hillIntact = false;
                }

                double present = CoverAtStationMetres(s.groundElevationMetres, s.roadSurfaceElevationMetres,
                    separation, cover);
                if (present < scan.worstCoverMetres)
                {
                    scan.worstCoverMetres = present;
                    scan.worstCoverStationIndex = i;
                }
            }

            if (scan.worstCoverStationIndex < 0) { scan.worstCoverMetres = 0.0; }
            return scan;
        }
        #endregion
    }
}

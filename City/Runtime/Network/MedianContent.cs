#region "Imports"
using System;
using System.Globalization;
using RoadCityMaster.CrossSection;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What a median of a given width can physically hold. The order is the order widths grow in, so
    /// widening a median only ever moves it up this list -- but the kinds are not cumulative: at the width where a
    /// depressed trough becomes possible the barrier is no longer warranted, so the barrier does not survive into
    /// the wider bands. "What is in this median" is therefore one lookup in a table of width bands rather than a
    /// pile of per-feature conditionals that can disagree with each other. </summary>
    public enum MedianContentKind
    {
        /// <summary> Too narrow for anything physical: paint on the carriageway, no kerb, no island. </summary>
        PaintedFlush = 0,

        /// <summary> A raised kerbed island. Markings and the kerb, nothing else -- there is no room. </summary>
        KerbedIslandMarkings = 1,

        /// <summary> Wide enough to stand a barrier in, with the offsets a barrier needs either side. </summary>
        Barrier = 2,

        /// <summary> Wide enough to depress into a drainage trough with traversable side slopes. </summary>
        Trough = 3,

        /// <summary> Wide enough for the trough AND for trees set back from both carriageway edges. </summary>
        TroughAndPlanting = 4
    }


    /// <summary> One row of the median width-band table: the narrowest width at which a kind becomes possible, and
    /// where that number comes from. <see cref="authoredThreshold"/> is true where no adopted standard states the
    /// figure and it is this project's own, recorded rather than presented as derived (C-15). </summary>
    public struct MedianWidthBand
    {
        public float minimumWidthMetres;
        public MedianContentKind kind;
        public bool authoredThreshold;
        public string basis;
    }


    /// <summary> How a barrier run ends. A run that simply stops leaves a blunt rail end facing traffic, which is
    /// the single most dangerous part of a real guardrail and reads instantly as unfinished geometry, so
    /// <see cref="None"/> is never a treatment a placed run is allowed to keep -- it is the value that means "not
    /// resolved", and a plan carrying it is refused. </summary>
    public enum TerminalTreatment
    {
        None = 0,

        /// <summary> The rail is flared away from the carriageway over a length set by the design speed. </summary>
        FlaredEnd = 1,

        /// <summary> The rail is buried into a backslope, the preferred real treatment where the ground offers
        /// one, because it removes the end from the traffic path entirely and needs no flare length. </summary>
        BuriedInBackslope = 2,

        /// <summary> A redirective crash cushion, used where there is no backslope and no room to flare. </summary>
        CrashCushion = 3
    }


    /// <summary> One resolved end of a barrier run: the treatment, the length it consumes along the road, and WHERE
    /// the end object sits -- and the placement is RM-26's <see cref="TerminusPlacement"/> computed by RM-26's own
    /// code, not a second implementation, so a terminating road, a bridge parapet and a median barrier all end
    /// through one mechanism. A terminal that could not be resolved carries <see cref="refused"/> and a sentence
    /// naming every length involved, instead of a blunt end. </summary>
    public struct BarrierTerminal
    {
        public TerminalTreatment treatment;
        public float lengthMetres;
        public TerminusPlacement placement;
        public bool refused;
        public string refusal;
    }


    /// <summary> The median trough's own geometry. Its side slopes are FLATTER than a roadside ditch's: a median
    /// slope is inside the clear zone and has to be traversable by a vehicle that leaves the carriageway, which is
    /// 1:6 in AASHTO practice, where a mowable roadside ditch is 1:3. The flat bottom keeps the invert
    /// maintainable, and the depth cap is what stops a wide median from becoming a chasm. Hydraulics are NOT
    /// duplicated here -- <see cref="MedianContent.TroughCapacityM3PerSecond"/> hands this geometry to RM-30's
    /// Manning implementation. </summary>
    [Serializable]
    public sealed class MedianTroughClass
    {
        /// <summary> Flat bottom width in metres. AASHTO asks for at least 1.2 m for maintenance; 3 m is normal in
        /// a wide depressed median and is the figure the planting band's arithmetic uses. AUTHORED. </summary>
        public float bottomWidthMetres = 3f;

        /// <summary> Side slope, horizontal per 1 vertical. 1:6 is the AASHTO traversable median slope. </summary>
        public float sideSlopeHorizontalPerVertical = 6f;

        /// <summary> Deepest the trough may go below the carriageway edges, in metres. AUTHORED from depressed
        /// median practice (0.9-1.2 m); past it the side slopes flatten instead of the trough deepening. </summary>
        public float maximumDepthMetres = 1.2f;

        /// <summary> Minimum longitudinal grade as a fraction. 0.5 % is standard minimum drainage grade: below it
        /// the trough is a flat trench that holds water along its whole length. </summary>
        public float minimumLongitudinalGrade = 0.005f;

        public static MedianTroughClass Default()
        {
            return new MedianTroughClass();
        }
    }


    /// <summary> Everything about one median run that the resolver needs and cannot invent: its width, the speed the
    /// terminals are sized for, the room and ground available at the ends, the two invert levels and whether an
    /// outfall exists, and the tree row's dimensions for the sight-line report. Grouped in one struct so adding a
    /// condition does not change every call site. </summary>
    public struct MedianSite
    {
        public float widthMetres;
        public float designSpeedKmh;

        /// <summary> Room along the road, at each end, before the median closes -- what a flare has to fit in. </summary>
        public float approachLengthMetres;

        /// <summary> Whether the ground at the ends offers a backslope to bury the rail into. </summary>
        public bool backslopeAvailable;

        /// <summary> How far the end has to move laterally away from the barrier line, in metres. </summary>
        public float lateralOffsetMetres;

        public Vector3 startNodeCentre;
        public Vector3 endNodeCentre;

        /// <summary> The ROAD's surface height at the ends. RM-26's anti-sink rule: never sampled ground. </summary>
        public float roadSurfaceHeightMetres;

        /// <summary> Road tangent in XZ at the run's last node; the start end faces the reverse of it. </summary>
        public Vector2 tangentAtEndXZ;

        public float longitudinalGradeFraction;
        public float upstreamInvertMetres;
        public float downstreamInvertMetres;
        public bool outfallPresent;

        /// <summary> Where the low point is, so a trough with nowhere to drain is refused by station. </summary>
        public float lowPointStation;

        public float treeSpacingMetres;
        public float trunkDiameterMetres;
    }


    /// <summary> The whole answer for one median run: which band it fell in, what is in it, how the barrier ends,
    /// how deep the trough is and whether it drains, and whether planting fits. Produced whole by
    /// <see cref="MedianContent.Resolve"/> and never patched afterwards, which is how "changing the median width
    /// re-solves all three" is guaranteed rather than hoped for: a narrower median cannot leave a wider median's
    /// trough behind, because nothing carries over between two calls. </summary>
    public struct MedianPlan
    {
        public float widthMetres;
        public MedianContentKind kind;

        public bool hasBarrier;
        public BarrierTerminal startTerminal;
        public BarrierTerminal endTerminal;

        public bool hasTrough;
        public float troughDepthMetres;
        public float troughBottomWidthMetres;
        public string troughRefusal;

        public bool hasPlanting;
        public float plantingSetbackMetres;

        /// <summary> Why planting was left out, with the width quoted. Not a refusal of the median: a median that
        /// is too narrow for trees is still a working median, it just has no trees. </summary>
        public string plantingRefusal;

        /// <summary> Set when the median cannot be built as authored -- an unterminated barrier run, or a trough
        /// with nowhere to drain. Both are defects that are invisible in a screenshot. </summary>
        public bool refused;
        public string refusal;
    }


    /// <summary> RM-60's verifiable core: what goes in the median of an RM-59 divided road, and how it ends.
    ///
    /// Three rules shape this file. First, what a median can hold is a WIDTH BAND TABLE, in the spirit of RM-34's
    /// retaining bands -- one ordered table with a stated basis per row, so a width change re-solves everything by
    /// re-reading the table instead of by re-evaluating scattered conditionals. Second, a barrier run's ends are the
    /// deliverable, not a decoration, and they are RM-26's terminals: the placement comes from
    /// <see cref="RoadTerminus.ComputePlacement"/>, so the anti-sink height rule and the outward facing are the
    /// same code a road terminus uses. Third, the trough is a drainage feature and inherits RM-30 wholesale: its
    /// capacity is <see cref="Culvert.VDitchCapacityM3PerSecond"/> and its outfall test is
    /// <see cref="Culvert.DitchDrainsToOutfall"/>, so a median trough cannot disagree with a roadside ditch about
    /// what draining means.
    ///
    /// Planting is reported, not policed. Sight distance across a median is a real design quantity, but whether a
    /// tree row blocks a view the author cares about is a judgement the tool cannot make, so the clear opening
    /// between trunks along a line of sight is measured and handed over, and only the unambiguous case -- a median
    /// too narrow to set trees back from both carriageways -- is refused, with the width quoted. </summary>
    public static class MedianContent
    {
        /// <summary> Setback in metres from each carriageway edge to the nearest trunk: the AASHTO Roadside Design
        /// Guide clear zone for 100 km/h on a flat, moderate-volume road. A tree is a fixed object, so it belongs
        /// outside the clear zone; inside it, planting a tree is planting a collision. </summary>
        public const float PlantingSetbackMetres = 6f;

        private static readonly MedianWidthBand[] Bands = BuildBands();

        private static MedianWidthBand[] BuildBands()
        {
            return new MedianWidthBand[]
            {
                Band(0f, MedianContentKind.PaintedFlush, false,
                    "below the minimum raised-median width there is nothing to raise: paint only"),
                Band(1.2f, MedianContentKind.KerbedIslandMarkings, false,
                    "AASHTO minimum raised median width, 1.2 m (4 ft)"),
                Band(2.4f, MedianContentKind.Barrier, true,
                    "AUTHORED assembly of sourced parts: a 0.61 m F-shape barrier base (AASHTO Roadside Design "
                    + "Guide) plus a 0.9 m offset from each carriageway edge"),
                Band(9f, MedianContentKind.Trough, false,
                    "AASHTO: a median barrier is warranted BELOW 9 m (30 ft) on a high-speed divided highway, and a "
                    + "depressed median with traversable slopes becomes practical at that width"),
                Band(15f, MedianContentKind.TroughAndPlanting, true,
                    "AUTHORED: 2 x 6 m clear-zone setback plus a 3 m trough bottom. Planting setbacks are usually "
                    + "not stated by the standard, so this threshold is recorded as this project's own")
            };
        }

        private static MedianWidthBand Band(float minimumWidthMetres, MedianContentKind kind, bool authored,
            string basis)
        {
            MedianWidthBand band = new MedianWidthBand();
            band.minimumWidthMetres = minimumWidthMetres;
            band.kind = kind;
            band.authoredThreshold = authored;
            band.basis = basis;
            return band;
        }

        /// <summary> How many width bands the table has. </summary>
        public static int BandCount { get { return Bands.Length; } }

        /// <summary> One row of the table, for an inspector or a report to show the author WHY a median holds what
        /// it holds. The table is not handed out as an array so a caller cannot quietly rewrite a threshold. </summary>
        public static MedianWidthBand BandAt(int index)
        {
            return Bands[index];
        }

        /// <summary> The band a median of this width falls in: the widest row whose minimum it reaches. The scan is
        /// ordered and inclusive at the threshold, so a median exactly 2.4 m wide holds a barrier. </summary>
        public static MedianWidthBand BandFor(float widthMetres)
        {
            MedianWidthBand chosen = Bands[0];
            for (int i = 0; i < Bands.Length; i++)
            {
                if (widthMetres + 1e-4f >= Bands[i].minimumWidthMetres) { chosen = Bands[i]; }
            }
            return chosen;
        }

        /// <summary> A barrier belongs to its own band only. At the width where a depressed median becomes practical
        /// the standard stops warranting a barrier, and standing one in a trough would be inventing a treatment no
        /// standard asks for. </summary>
        public static bool HoldsBarrier(MedianContentKind kind)
        {
            return kind == MedianContentKind.Barrier;
        }

        public static bool HoldsTrough(MedianContentKind kind)
        {
            return kind >= MedianContentKind.Trough;
        }

        public static bool HoldsPlanting(MedianContentKind kind)
        {
            return kind >= MedianContentKind.TroughAndPlanting;
        }

        /// <summary> The narrowest median that can hold planting, READ FROM THE TABLE rather than recomputed, so
        /// there is exactly one such number in the tool. </summary>
        public static float MinimumPlantingWidthMetres()
        {
            for (int i = 0; i < Bands.Length; i++)
            {
                if (Bands[i].kind == MedianContentKind.TroughAndPlanting) { return Bands[i].minimumWidthMetres; }
            }
            return float.MaxValue;
        }

        /// <summary> The median width of an RM-59 divided road at a station: twice the half-gap, and zero where the
        /// median has closed. The one place the two tasks join, so the content is sized by the geometry that was
        /// actually emitted rather than by a number typed twice. </summary>
        public static float MedianWidthAt(DividedRoadProfile profile, float station)
        {
            float half = profile.medianHalfGap.EmitsAt(station) ? profile.medianHalfGap.HalfWidthAt(station) : 0f;
            return half * 2f;
        }

        // AASHTO Roadside Design Guide flare rates for a barrier beyond the shy line, expressed as longitudinal run
        // per 1 m of lateral offset, against design speed: 110 km/h 20:1, 100 km/h 18:1, 80 km/h 14:1, 60 km/h 11:1,
        // 50 km/h 8:1. Faster roads need flatter flares, which is why the length is a table and not a constant.
        private static readonly float[] FlareSpeedsKmh = { 50f, 60f, 80f, 100f, 110f };
        private static readonly float[] FlareRuns = { 8f, 11f, 14f, 18f, 20f };

        // Redirective crash-cushion bay length against speed. AUTHORED: cushion lengths are product-specific, and
        // these are typical installed lengths (about 2.8 m at 50 km/h, about 6.5 m at 110 km/h).
        private static readonly float[] CushionSpeedsKmh = { 50f, 110f };
        private static readonly float[] CushionLengths = { 2.8f, 6.5f };

        /// <summary> Length of rail a buried-in-backslope terminal needs to reach the slope, in metres. AUTHORED
        /// from standard practice (about 7.6 m / 25 ft of rail run into the backslope). </summary>
        public const float BuriedEndLengthMetres = 7.6f;

        /// <summary> The flare rate for a design speed: metres of longitudinal run per metre of lateral offset.
        /// Exact at a table row, linearly interpolated between rows, clamped past both ends -- the same table idiom
        /// RM-40 uses for side friction, so a speed off the end of the table cannot produce a nonsense rate. </summary>
        public static float FlareRunPerMetreOffset(float designSpeedKmh)
        {
            return LookupClamped(FlareSpeedsKmh, FlareRuns, designSpeedKmh);
        }

        /// <summary> How much road a flared end consumes: the lateral offset times the flare rate for the speed. A
        /// 4.5 m offset at 110 km/h needs 90 m, which is exactly why a flare often does not fit and the ladder below
        /// has to fall through to a cushion. </summary>
        public static float FlareLengthMetres(float lateralOffsetMetres, float designSpeedKmh)
        {
            float offset = lateralOffsetMetres > 0f ? lateralOffsetMetres : 0f;
            return offset * FlareRunPerMetreOffset(designSpeedKmh);
        }

        /// <summary> Installed length of a redirective crash cushion for a design speed. </summary>
        public static float CrashCushionLengthMetres(float designSpeedKmh)
        {
            return LookupClamped(CushionSpeedsKmh, CushionLengths, designSpeedKmh);
        }

        private static float LookupClamped(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) { return ys[0]; }
            int last = xs.Length - 1;
            if (x >= xs[last]) { return ys[last]; }
            for (int i = 0; i < last; i++)
            {
                if (x >= xs[i] && x <= xs[i + 1])
                {
                    float span = xs[i + 1] - xs[i];
                    float t = span > 0f ? (x - xs[i]) / span : 0f;
                    return ys[i] + (ys[i + 1] - ys[i]) * t;
                }
            }
            return ys[last];
        }

        /// <summary> Choose the end treatment from the room and the ground, in order of what a real installation
        /// prefers: bury the rail in a backslope where there is one, otherwise flare it, otherwise fit a crash
        /// cushion -- and if not even a cushion fits, REFUSE, naming the room available and all three lengths that
        /// were tried. Nothing in this ladder can hand back <see cref="TerminalTreatment.None"/> without refusing,
        /// which is the property that stops a blunt end ever being emitted. </summary>
        public static BarrierTerminal ChooseTerminal(MedianSite site, RoadEnd whichEnd)
        {
            BarrierTerminal terminal = new BarrierTerminal();
            terminal.treatment = TerminalTreatment.None;
            float room = site.approachLengthMetres > 0f ? site.approachLengthMetres : 0f;
            float flare = FlareLengthMetres(site.lateralOffsetMetres, site.designSpeedKmh);
            float cushion = CrashCushionLengthMetres(site.designSpeedKmh);

            if (site.backslopeAvailable && room + 1e-4f >= BuriedEndLengthMetres)
            {
                terminal.treatment = TerminalTreatment.BuriedInBackslope;
                terminal.lengthMetres = BuriedEndLengthMetres;
                return terminal;
            }
            if (room + 1e-4f >= flare)
            {
                terminal.treatment = TerminalTreatment.FlaredEnd;
                terminal.lengthMetres = flare;
                return terminal;
            }
            if (room + 1e-4f >= cushion)
            {
                terminal.treatment = TerminalTreatment.CrashCushion;
                terminal.lengthMetres = cushion;
                return terminal;
            }

            terminal.refused = true;
            terminal.refusal = "the " + (whichEnd == RoadEnd.End ? "far" : "near") + " end of the median barrier has "
                + Metres(room) + " of road to end in, which is not enough for any terminal: a flare needs "
                + Metres(flare) + " at " + Number(site.designSpeedKmh) + " km/h, a buried-in-backslope end needs "
                + Metres(BuriedEndLengthMetres) + " and a backslope, and a crash cushion needs " + Metres(cushion)
                + ". Start the barrier further from the end, or leave the median unbarriered -- an unterminated run "
                + "leaves a blunt rail end facing traffic.";
            return terminal;
        }

        /// <summary> The chosen treatment plus WHERE the end object sits, and the placement is RM-26's: same anti-sink
        /// height rule (the road's own surface, never sampled ground), same outward facing, same yaw convention. The
        /// position is resolved even when the treatment is refused, because that is still where the run ends -- what
        /// the refusal stops is the run being built at all. </summary>
        public static BarrierTerminal PlanTerminal(MedianSite site, RoadEnd whichEnd)
        {
            BarrierTerminal terminal = ChooseTerminal(site, whichEnd);
            Vector3 centre = whichEnd == RoadEnd.End ? site.endNodeCentre : site.startNodeCentre;
            terminal.placement = RoadTerminus.ComputePlacement(centre, site.roadSurfaceHeightMetres,
                site.tangentAtEndXZ, whichEnd);
            return terminal;
        }

        /// <summary> How far the trough bottom sits below the carriageway edges: the side room either side of the flat
        /// bottom divided by the side slope, capped. </summary>
        public static float TroughDepthMetres(float widthMetres, MedianTroughClass trough)
        {
            MedianTroughClass t = trough != null ? trough : MedianTroughClass.Default();
            float bottom = t.bottomWidthMetres > 0f ? t.bottomWidthMetres : 0f;
            float slope = t.sideSlopeHorizontalPerVertical > 0f ? t.sideSlopeHorizontalPerVertical : 1f;
            float sideRoom = (widthMetres - bottom) * 0.5f;
            if (sideRoom <= 0f) { return 0f; }
            float depth = sideRoom / slope;
            float cap = t.maximumDepthMetres > 0f ? t.maximumDepthMetres : depth;
            return depth < cap ? depth : cap;
        }

        /// <summary> The side slope the trough is ACTUALLY built at, horizontal per 1 vertical. It equals the class
        /// value until the depth cap bites, after which the slopes flatten -- a wider median gets a gentler trough,
        /// not a deeper one, because depth is the dangerous dimension. </summary>
        public static float EffectiveSideSlopeHorizontalPerVertical(float widthMetres, MedianTroughClass trough)
        {
            MedianTroughClass t = trough != null ? trough : MedianTroughClass.Default();
            float depth = TroughDepthMetres(widthMetres, t);
            if (depth <= 0f) { return t.sideSlopeHorizontalPerVertical; }
            float bottom = t.bottomWidthMetres > 0f ? t.bottomWidthMetres : 0f;
            return ((widthMetres - bottom) * 0.5f) / depth;
        }

        /// <summary> The trough as RM-30 sees it, so its hydraulics come from RM-30's implementation and not from a
        /// second one written here. The side slope handed over is the EFFECTIVE one, so the capacity describes the
        /// shape that was built. </summary>
        public static RoadsideDitchClass AsDitchClass(float widthMetres, MedianTroughClass trough)
        {
            MedianTroughClass t = trough != null ? trough : MedianTroughClass.Default();
            RoadsideDitchClass ditch = RoadsideDitchClass.Default();
            ditch.sideSlopeHorizontalPerVertical = EffectiveSideSlopeHorizontalPerVertical(widthMetres, t);
            return ditch;
        }

        /// <summary> Capacity of the median trough, in m3/s, from RM-30's Manning code. Treated as the V of its side
        /// slopes: the flat bottom's own area is not added, so the figure is conservative -- stated rather than
        /// quietly claimed as exact. </summary>
        public static double TroughCapacityM3PerSecond(float widthMetres, MedianTroughClass trough,
            double flowDepthMetres, double longitudinalSlope)
        {
            return Culvert.VDitchCapacityM3PerSecond(flowDepthMetres, longitudinalSlope,
                AsDitchClass(widthMetres, trough));
        }

        /// <summary> Whether the trough can actually get rid of its water, using RM-30's own outfall test: an outfall
        /// must exist AND the downstream invert must be below the upstream one. </summary>
        public static bool TroughDrains(MedianSite site)
        {
            return Culvert.DitchDrainsToOutfall(site.upstreamInvertMetres, site.downstreamInvertMetres,
                site.outfallPresent);
        }

        /// <summary> Why the trough cannot be built, or "" when it can. Two failures, both invisible in a screenshot
        /// and both named by station: a grade too flat to move water, and no outfall to move it to. The second is
        /// RM-30's chain-of-basins failure sitting in the middle of a highway. </summary>
        public static string TroughRefusal(MedianSite site, MedianTroughClass trough)
        {
            MedianTroughClass t = trough != null ? trough : MedianTroughClass.Default();
            float grade = Mathf.Abs(site.longitudinalGradeFraction);
            float minimum = t.minimumLongitudinalGrade > 0f ? t.minimumLongitudinalGrade : 0f;
            if (grade + 1e-6f < minimum)
            {
                return "the median trough at station " + Metres(site.lowPointStation) + " is graded at "
                    + Percent(grade) + ", below the " + Percent(minimum) + " minimum drainage grade, so it holds "
                    + "water along its whole length instead of moving it. Grade the trough, or leave the median "
                    + "undrained.";
            }
            if (!TroughDrains(site))
            {
                return "the median trough at station " + Metres(site.lowPointStation) + " has nowhere to drain "
                    + "(upstream invert " + Metres(site.upstreamInvertMetres) + ", downstream invert "
                    + Metres(site.downstreamInvertMetres) + ", outfall "
                    + (site.outfallPresent ? "present" : "absent") + "): it is a closed basin in the middle of a "
                    + "highway. Carry it to a cross-drain, a culvert under a carriageway, or a watercourse.";
            }
            return "";
        }

        /// <summary> Whether trees can be set back from BOTH carriageway edges in a median this wide. </summary>
        public static bool PlantingFits(float widthMetres)
        {
            return widthMetres + 1e-4f >= MinimumPlantingWidthMetres();
        }

        /// <summary> Why planting was left out, with the width quoted, or "" where it fits. </summary>
        public static string PlantingRefusal(float widthMetres)
        {
            if (PlantingFits(widthMetres)) { return ""; }
            return "planting was left out of a " + Metres(widthMetres) + " median: trees need "
                + Metres(PlantingSetbackMetres) + " of clear zone from each carriageway edge, so the median has to "
                + "be at least " + Metres(MinimumPlantingWidthMetres()) + " wide. Widen the median, or accept a "
                + "median without trees.";
        }

        /// <summary> The clear opening, in metres, between two trunks along a line of sight crossing the median at
        /// this angle to the road. Real geometry: the spacing foreshortens to spacing x sin(angle) across the line of
        /// sight, and the trunk's own width closes that gap. Zero means the row is a wall at that angle. This is
        /// REPORTED, never refused: whether the view being blocked matters is the author's call. </summary>
        public static float ClearSightOpeningMetres(float spacingMetres, float trunkDiameterMetres,
            float viewAngleDegrees)
        {
            float spacing = spacingMetres > 0f ? spacingMetres : 0f;
            float trunk = trunkDiameterMetres > 0f ? trunkDiameterMetres : 0f;
            float across = spacing * Mathf.Abs(Mathf.Sin(viewAngleDegrees * Mathf.Deg2Rad));
            float opening = across - trunk;
            return opening > 0f ? opening : 0f;
        }

        /// <summary> The angle to the road below which the tree row is opaque -- asin(trunk / spacing). Trunks that
        /// touch return 90 degrees: opaque from every direction. </summary>
        public static float OpaqueBelowAngleDegrees(float spacingMetres, float trunkDiameterMetres)
        {
            float spacing = spacingMetres > 0f ? spacingMetres : 0f;
            float trunk = trunkDiameterMetres > 0f ? trunkDiameterMetres : 0f;
            if (spacing <= 0f || trunk >= spacing) { return 90f; }
            return Mathf.Asin(trunk / spacing) * Mathf.Rad2Deg;
        }

        /// <summary> Resolve the whole median from its width and its site, in one pass, returning a fresh plan. The
        /// band decides what is possible; the site decides whether it can be built. Because the plan is built whole
        /// and nothing is carried between calls, narrowing a median cannot leave the wider median's trough or the
        /// barrier's terminals behind -- which is the "changing the width re-solves all three" requirement. </summary>
        public static MedianPlan Resolve(MedianSite site, MedianTroughClass trough)
        {
            MedianTroughClass t = trough != null ? trough : MedianTroughClass.Default();
            MedianWidthBand band = BandFor(site.widthMetres);

            MedianPlan plan = new MedianPlan();
            plan.widthMetres = site.widthMetres;
            plan.kind = band.kind;
            plan.troughRefusal = "";
            plan.plantingRefusal = "";
            plan.refusal = "";

            if (HoldsBarrier(plan.kind))
            {
                plan.startTerminal = PlanTerminal(site, RoadEnd.Start);
                plan.endTerminal = PlanTerminal(site, RoadEnd.End);
                plan.hasBarrier = !plan.startTerminal.refused && !plan.endTerminal.refused;
                if (!plan.hasBarrier)
                {
                    plan.refused = true;
                    plan.refusal = plan.startTerminal.refused ? plan.startTerminal.refusal
                                                             : plan.endTerminal.refusal;
                }
            }

            if (HoldsTrough(plan.kind))
            {
                plan.troughRefusal = TroughRefusal(site, t);
                plan.hasTrough = plan.troughRefusal == "";
                if (plan.hasTrough)
                {
                    plan.troughBottomWidthMetres = t.bottomWidthMetres;
                    plan.troughDepthMetres = TroughDepthMetres(site.widthMetres, t);
                }
                else
                {
                    plan.refused = true;
                    if (plan.refusal == "") { plan.refusal = plan.troughRefusal; }
                }
            }

            if (HoldsPlanting(plan.kind))
            {
                plan.hasPlanting = true;
                plan.plantingSetbackMetres = PlantingSetbackMetres;
            }
            else if (site.treeSpacingMetres > 0f)
            {
                plan.plantingRefusal = PlantingRefusal(site.widthMetres);
            }

            return plan;
        }

        /// <summary> An AUTHORED site to resolve against: 120 m of room at each end, no backslope, a 1.2 m lateral
        /// offset for the end treatment, a 1 % trough grade falling 1 m to a present outfall, and no tree row.
        /// Callers override what their road actually has. </summary>
        public static MedianSite Site(float widthMetres, float designSpeedKmh)
        {
            MedianSite site = new MedianSite();
            site.widthMetres = widthMetres;
            site.designSpeedKmh = designSpeedKmh;
            site.approachLengthMetres = 120f;
            site.backslopeAvailable = false;
            site.lateralOffsetMetres = 1.2f;
            site.startNodeCentre = new Vector3(0f, 0f, 0f);
            site.endNodeCentre = new Vector3(0f, 0f, 400f);
            site.roadSurfaceHeightMetres = 0f;
            site.tangentAtEndXZ = new Vector2(0f, 1f);
            site.longitudinalGradeFraction = 0.01f;
            site.upstreamInvertMetres = 1f;
            site.downstreamInvertMetres = 0f;
            site.outfallPresent = true;
            site.lowPointStation = 0f;
            site.treeSpacingMetres = 0f;
            site.trunkDiameterMetres = 0f;
            return site;
        }

        private static string Metres(float value)
        {
            return Number(value) + " m";
        }

        private static string Percent(float fraction)
        {
            return Number(fraction * 100f) + " %";
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

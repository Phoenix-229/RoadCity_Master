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
    /// <summary> D-13. The spillway forms this register sizes, and the discharge coefficient each carries in the weir
    /// equation Q = C · L · H^(3/2). These are the standard SI figures for the three overflow forms a river dam of this
    /// scale actually uses: an ogee crest sheds the most for its width, a broad crest the least, a chute sits between.
    /// The coefficient is not a taste knob -- it is the shape's measured performance, so the discharge a graph reads
    /// back is the discharge the geometry gives, not a guess. </summary>
    public enum SpillwayType
    {
        /// <summary> Ogee (overflow) crest, the shaped weir most concrete gravity dams carry. C ~ 2.2. </summary>
        OgeeOverflow = 0,

        /// <summary> Chute spillway, a channel carrying flow down the face. C ~ 2.1. </summary>
        Chute = 1,

        /// <summary> Broad-crested weir, a flat sill. Lowest discharge for its width. C ~ 1.7. </summary>
        BroadCrested = 2
    }


    /// <summary> The dam body itself, as figures a builder can raise and a report can state. A dam is not a prefab
    /// because its height is the valley's depth and its footprint follows that height through the batter of its two
    /// faces; only the crest section (width, road, parapet) is content an author swaps. Every level here is a world
    /// height in metres, the same datum the terrain reader answers in. </summary>
    [Serializable]
    public struct DamProfile
    {
        public bool resolved;
        public string refusal;

        /// <summary> Top of the structure. The reservoir never reaches it -- the freeboard is the gap it leaves.
        /// </summary>
        public double crestLevelMetres;

        /// <summary> The safety gap between full reservoir and crest, kept at or above the 1.5 m floor a dam of this
        /// class carries against wave run-up and flood surcharge. </summary>
        public double freeboardMetres;

        /// <summary> Crest minus freeboard: the level the water is held at, and the one Universe Creator raises the
        /// upstream body to. </summary>
        public double reservoirSurfaceLevelMetres;

        public double crestLengthMetres;
        public double crestWidthMetres;

        /// <summary> Crest level minus the valley floor: how tall the structure actually stands. </summary>
        public double structuralHeightMetres;

        public double upstreamRunPerRise;
        public double downstreamRunPerRise;

        /// <summary> Footprint front-to-back at the base: the crest width plus each face battering out by the height
        /// times its run-per-rise. This is the ground the structure occupies, which the abutment keying must clear.
        /// </summary>
        public double baseWidthMetres;

        public string Describe()
        {
            if (!resolved) { return "dam body refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal); }
            string crest = M(crestLevelMetres);
            string surface = M(reservoirSurfaceLevelMetres);
            string height = M(structuralHeightMetres);
            string baseW = M(baseWidthMetres);
            return "dam crest " + crest + " m, holds water to " + surface + " m (freeboard " + M(freeboardMetres)
                + " m), stands " + height + " m, base " + baseW + " m front-to-back";
        }

        private static string M(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }


    /// <summary> The valley read across the dam line, which is what the abutments must key into. Sampled off the real
    /// terrain, never assumed: the two ends of the crest are where the structure meets the ground, and if the ground
    /// there is below the crest the dam does not reach the valley walls and is reported as too short rather than left to
    /// float past them. </summary>
    [Serializable]
    public struct ValleyCrossSection
    {
        public bool resolved;
        public string refusal;

        public double leftAbutmentLevelMetres;
        public double rightAbutmentLevelMetres;

        /// <summary> The lowest ground under the dam line: the valley floor the structure founds on. </summary>
        public double valleyFloorLevelMetres;

        /// <summary> The width of the section that lies below the crest -- the opening the crest must close. </summary>
        public double openingWidthAtCrestMetres;

        public int sampleCount;

        /// <summary> True only when both abutments stand at or above the crest, so the dam keys into rock at each end
        /// rather than leaving a gap the water runs around. </summary>
        public bool crestSpansValley;

        public string Describe()
        {
            if (!resolved)
            {
                return "valley section refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal);
            }
            string floor = M(valleyFloorLevelMetres);
            string opening = M(openingWidthAtCrestMetres);
            string spans = crestSpansValley ? "crest reaches both abutments"
                : "crest does NOT reach one or both abutments -- the dam is too short for this line";
            string abutments = M(leftAbutmentLevelMetres) + " m / " + M(rightAbutmentLevelMetres) + " m";
            return "valley floor " + floor + " m, opening " + opening + " m at crest, abutments " + abutments
                + "; " + spans;
        }

        private static string M(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }


    /// <summary> The reservoir the dam impounds, found by flooding the real terrain upstream of the dam line to the
    /// held level -- not a painted lake and not a bounding box, but the cells whose ground actually lies below the
    /// water, reached from the dam line without crossing it. The volume is the water that would stand above that ground.
    /// If the fill runs out its search budget the extent is reported as incomplete rather than as final, because a
    /// valley open at this level would otherwise read as a closed reservoir. </summary>
    [Serializable]
    public struct ImpoundedReservoir
    {
        public bool resolved;
        public string refusal;

        public double surfaceLevelMetres;
        public int floodedCellCount;
        public double surfaceAreaSquareMetres;
        public double storageVolumeCubicMetres;

        public double minX;
        public double maxX;
        public double minZ;
        public double maxZ;

        /// <summary> True when the flood fill hit its cell budget or window edge before the water ran out. The reservoir
        /// is at least this big and may be larger; the valley is likely open at this level. </summary>
        public bool reachedSearchLimit;

        public double cellSizeMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "reservoir refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal);
            }
            if (floodedCellCount == 0)
            {
                return "impounds nothing at " + M(surfaceLevelMetres) + " m: the ground upstream of the dam line is "
                    + "already above the held level, so there is no reservoir to raise";
            }
            string area = M(surfaceAreaSquareMetres);
            string vol = M(storageVolumeCubicMetres);
            string tail = reachedSearchLimit
                ? " (search limit reached -- at least this large, valley likely open at this level)" : string.Empty;
            return "reservoir " + area + " m2, " + vol + " m3 held to " + M(surfaceLevelMetres) + " m across "
                + floodedCellCount.ToString(CultureInfo.InvariantCulture) + " cells" + tail;
        }

        private static string M(double v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }
    }


    /// <summary> The spillway sized by the weir equation, so the discharge is the crest's, not a wish. The design head
    /// is the reservoir surface standing above the spillway crest; a spillway crest at or above the reservoir passes
    /// nothing and is reported as such rather than returning a made-up number. </summary>
    [Serializable]
    public struct SpillwaySizing
    {
        public bool resolved;
        public string refusal;

        public SpillwayType type;
        public double crestLevelMetres;
        public double widthMetres;
        public double designHeadMetres;
        public double dischargeCumecs;

        public string Describe()
        {
            if (!resolved)
            {
                return "spillway refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal);
            }
            return type.ToString() + " spillway, " + M(widthMetres) + " m wide, head " + M(designHeadMetres)
                + " m, passes " + M(dischargeCumecs) + " m3/s at full reservoir";
        }

        private static string M(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }


    /// <summary> The low-level outlet, sized by the orifice equation Q = Cd · A · sqrt(2 g H). Present only when a bore
    /// is given; the static head is the reservoir standing above the outlet invert. This is the controlled release the
    /// spillway does not carry -- the flow the author sees leaving the foot of the dam. </summary>
    [Serializable]
    public struct OutletSizing
    {
        public bool present;
        public string refusal;

        public double invertLevelMetres;
        public double boreDiameterMetres;
        public double staticHeadMetres;
        public double dischargeCumecs;

        public string Describe()
        {
            if (!present)
            {
                return string.IsNullOrEmpty(refusal) ? "no outlet works" : "outlet refused: " + refusal;
            }
            return "outlet bore " + M(boreDiameterMetres) + " m at invert " + M(invertLevelMetres) + " m, head "
                + M(staticHeadMetres) + " m, passes " + M(dischargeCumecs) + " m3/s";
        }

        private static string M(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }


    /// <summary> Whether the body the author drew would actually stand, by the two checks a gravity dam is designed
    /// against: sliding along the foundation contact and overturning about the downstream toe. Both are reported as a
    /// factor of safety -- resisting effect over driving effect -- so a figure below 1 is a dam that fails, and the
    /// codes ask for margin above that (USBR: 1.5 sliding with no cohesion counted; ICOLD asks 2.0 for overturning on
    /// the normal load case).
    ///
    /// Three things here are easy to get wrong and are done properly, because a wrong factor of safety reads exactly
    /// like a right one:
    ///
    /// 1. The water load is the RESERVOIR head, not the structural height. Water stands at crest minus freeboard --
    ///    that is what freeboard means -- so driving the hydrostatic force off the full height overstates it by the
    ///    freeboard on a squared term. Tailwater on the downstream face pushes BACK, so it is subtracted, not ignored.
    ///
    /// 2. Uplift is included. Water enters the foundation contact and presses the dam UP, and on a real gravity
    ///    section it removes a large fraction of the weight that friction depends on -- it is the reason the classical
    ///    "weight over water push" sum is optimistic, and the reason drainage galleries exist at all. The distribution
    ///    taken here is the undrained trapezoid: full reservoir pressure at the heel falling linearly to tailwater
    ///    pressure at the toe. That is the conservative case and the one a section without a working drain line
    ///    actually sees.
    ///
    /// 3. The weight acts through the real centroid of the trapezoidal section, found by decomposing it into the
    ///    upstream batter triangle, the middle rectangle and the downstream batter triangle and taking the
    ///    area-weighted mean. The B/3 shortcut is only correct for a triangular section with a vertical upstream face,
    ///    and every metre of crest width and upstream batter moves the true centroid away from it.
    ///
    /// Everything is computed PER METRE of crest length: both the resisting and the driving side scale with length, so
    /// it cancels, and carrying it would only invite a unit error. </summary>
    [Serializable]
    public struct DamStability
    {
        public bool resolved;
        public string refusal;

        /// <summary> Self-weight of one metre run of the section, in newtons. </summary>
        public double weightPerMetreNewtons;

        /// <summary> Uplift force on one metre run of the foundation contact, in newtons, acting upward. </summary>
        public double upliftPerMetreNewtons;

        /// <summary> Net horizontal water push on one metre run: upstream hydrostatic less tailwater, in newtons.
        /// </summary>
        public double netHorizontalPerMetreNewtons;

        /// <summary> Distance from the upstream heel to the section's centroid, in metres. </summary>
        public double centroidFromHeelMetres;

        /// <summary> Resisting over driving along the foundation contact. </summary>
        public double slidingFactorOfSafety;

        /// <summary> Stabilising over overturning moment about the downstream toe. </summary>
        public double overturningFactorOfSafety;

        /// <summary> The reservoir head the section is loaded by: surface level less the foundation. </summary>
        public double headMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "stability refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal);
            }
            return "stands against sliding " + M(slidingFactorOfSafety) + " and overturning "
                + M(overturningFactorOfSafety) + " under " + M(headMetres) + " m of head, with uplift "
                + M(upliftPerMetreNewtons / 1000.0) + " kN per metre run removing "
                + M(100.0 * upliftPerMetreNewtons / weightPerMetreNewtons) + "% of the section's weight";
        }

        private static string M(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }


    /// <summary> The whole request the dam states for Universe Creator to answer, gathered as data. Road & City Master
    /// computes every figure on its own side and never touches the water body itself; this struct is what travels
    /// through the linker so UC can raise the reservoir, lower the tailwater and key the terrain into the abutments on
    /// its next update. Until then the dam is a structure standing in an unchanged river, and the report says so.
    /// </summary>
    [Serializable]
    public struct DamRequest
    {
        public bool resolved;
        public string refusal;

        public DamProfile profile;
        public ValleyCrossSection valley;
        public ImpoundedReservoir reservoir;
        public SpillwaySizing spillway;
        public OutletSizing outlet;

        /// <summary> The water level below the dam: the downstream toe ground plus the river's natural depth there. The
        /// head the structure holds is the reservoir surface standing above this. </summary>
        public double tailwaterLevelMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "dam request refused: " + (string.IsNullOrEmpty(refusal) ? "unstated" : refusal);
            }
            double head = profile.reservoirSurfaceLevelMetres - tailwaterLevelMetres;
            string headText = head.ToString("0.##", CultureInfo.InvariantCulture);
            return profile.Describe() + "; " + reservoir.Describe() + "; tailwater "
                + tailwaterLevelMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m, gross head " + headText
                + " m. Until Universe Creator's update runs this is a structure in an unchanged river.";
        }
    }


    /// <summary> D-13's Road & City Master half: the dam figured entirely from the author's geometry and the real
    /// terrain, with nothing written to the terrain or the water here. The one feature in this register that cannot be
    /// finished on one side of the linker, so this side computes and states -- reservoir level, impounded extent, valley
    /// section, tailwater, spillway and outlet discharge -- and hands them across as a <see cref="DamRequest"/>.
    ///
    /// Nothing draws a seed: the flood fill visits cells in a fixed order and the weir and orifice equations are
    /// closed-form, so the same dam on the same terrain gives the same request every bake (C-06). The terrain is read
    /// only through <see cref="ITerrainSurfaceReader"/> -- the six-member contract that already exists -- so this adds
    /// no provider member (R-5 stands; UnderstoodMemberCount stays 6). </summary>
    public static class DamWorks
    {
        /// <summary> The freeboard floor for a dam of this class: 1.5 m against wave run-up and flood surcharge. Below
        /// it the body is refused rather than built with too little margin. </summary>
        public const double MinFreeboardMetres = 1.5;

        /// <summary> Standard gravity, for the orifice head term. </summary>
        public const double Gravity = 9.80665;

        /// <summary> Discharge coefficient of a sharp-edged submerged orifice -- the low-level outlet's bore. </summary>
        public const double OrificeCoefficient = 0.62;

        /// <summary> Default flood-fill resolution. Five metres resolves a reservoir's shape without a million samples.
        /// </summary>
        public const double DefaultCellSizeMetres = 5.0;

        /// <summary> The flood fill's cell budget. Bounds the work and the extent window; hitting it is reported, not
        /// hidden. </summary>
        public const int DefaultMaxFloodCells = 120000;

        /// <summary> How many points the valley section is read at across the crest line. </summary>
        public const int DefaultValleySamples = 33;

        /// <summary> The weir discharge coefficient for each spillway form, in SI (Q = C · L · H^1.5). </summary>
        public static double DischargeCoefficient(SpillwayType type)
        {
            switch (type)
            {
                case SpillwayType.OgeeOverflow: return 2.2;
                case SpillwayType.Chute: return 2.1;
                default: return 1.7;
            }
        }

        /// <summary> The dam body from the author's geometry and the valley floor the section found. Refuses a
        /// freeboard below the floor, a crest at or below the valley floor (there is nothing to dam), and any
        /// non-positive dimension or face slope, because each of those is a body that cannot stand. </summary>
        public static bool TryProfile(double crestLevelMetres, double valleyFloorLevelMetres, double freeboardMetres,
            double crestLengthMetres, double crestWidthMetres, double upstreamRunPerRise, double downstreamRunPerRise,
            out DamProfile profile)
        {
            profile = new DamProfile();
            profile.crestLevelMetres = crestLevelMetres;
            profile.freeboardMetres = freeboardMetres;
            profile.crestLengthMetres = crestLengthMetres;
            profile.crestWidthMetres = crestWidthMetres;
            profile.upstreamRunPerRise = upstreamRunPerRise;
            profile.downstreamRunPerRise = downstreamRunPerRise;

            if (IsBad(crestLevelMetres) || IsBad(valleyFloorLevelMetres) || IsBad(freeboardMetres)
                || IsBad(crestLengthMetres) || IsBad(crestWidthMetres) || IsBad(upstreamRunPerRise)
                || IsBad(downstreamRunPerRise))
            {
                profile.refusal = "a dam figure is not a finite number";
                return false;
            }
            if (freeboardMetres < MinFreeboardMetres)
            {
                profile.refusal = "freeboard " + Fmt(freeboardMetres) + " m is below the " + Fmt(MinFreeboardMetres)
                    + " m floor for a dam of this class";
                return false;
            }
            if (crestLevelMetres <= valleyFloorLevelMetres)
            {
                profile.refusal = "the crest at " + Fmt(crestLevelMetres) + " m is not above the valley floor at "
                    + Fmt(valleyFloorLevelMetres) + " m, so there is nothing to dam";
                return false;
            }
            if (crestLengthMetres <= 0.0 || crestWidthMetres <= 0.0 || upstreamRunPerRise < 0.0
                || downstreamRunPerRise < 0.0)
            {
                profile.refusal = "crest length and width must be positive and the face slopes non-negative";
                return false;
            }

            profile.structuralHeightMetres = crestLevelMetres - valleyFloorLevelMetres;
            profile.reservoirSurfaceLevelMetres = crestLevelMetres - freeboardMetres;
            profile.baseWidthMetres = crestWidthMetres
                + profile.structuralHeightMetres * (upstreamRunPerRise + downstreamRunPerRise);
            profile.resolved = true;
            profile.refusal = string.Empty;
            return true;
        }

        /// <summary> Read the valley across the dam line off the real terrain. The line runs along the axis heading,
        /// centred on the dam, one sample per step from one end to the other. The two ends are the abutments; the lowest
        /// sample is the valley floor; the run of samples below the crest is the opening the crest must close. </summary>
        public static ValleyCrossSection SampleValley(ITerrainSurfaceReader ground, double centreX, double centreZ,
            double axisHeadingDeg, double crestLengthMetres, double crestLevelMetres, int samples)
        {
            ValleyCrossSection v = new ValleyCrossSection();
            if (ground == null) { v.refusal = "no terrain reader was supplied"; return v; }
            if (IsBad(crestLengthMetres) || crestLengthMetres <= 0.0)
            {
                v.refusal = "crest length must be positive";
                return v;
            }
            if (samples < 3) { samples = 3; }

            double rad = axisHeadingDeg * Math.PI / 180.0;
            double ax = Math.Sin(rad);
            double az = Math.Cos(rad);
            double half = crestLengthMetres * 0.5;
            double step = crestLengthMetres / (samples - 1);

            double floor = double.PositiveInfinity;
            double firstBelow = double.NaN;
            double lastBelow = double.NaN;
            double left = 0.0;
            double right = 0.0;
            for (int i = 0; i < samples; i++)
            {
                double d = -half + step * i;
                float h = ground.SampleHeight((float)(centreX + ax * d), (float)(centreZ + az * d));
                if (float.IsNaN(h) || float.IsInfinity(h))
                {
                    v.refusal = "the terrain reader answered a non-finite height across the dam line";
                    return v;
                }
                double hd = h;
                if (i == 0) { left = hd; }
                if (i == samples - 1) { right = hd; }
                if (hd < floor) { floor = hd; }
                if (hd < crestLevelMetres)
                {
                    if (double.IsNaN(firstBelow)) { firstBelow = d; }
                    lastBelow = d;
                }
            }

            v.leftAbutmentLevelMetres = left;
            v.rightAbutmentLevelMetres = right;
            v.valleyFloorLevelMetres = floor;
            v.openingWidthAtCrestMetres = double.IsNaN(firstBelow) ? 0.0 : (lastBelow - firstBelow);
            v.sampleCount = samples;
            v.crestSpansValley = left >= crestLevelMetres && right >= crestLevelMetres;
            v.resolved = true;
            v.refusal = string.Empty;
            return v;
        }

        /// <summary> Flood the terrain upstream of the dam line to the held level. A world-aligned grid of cells is
        /// walked breadth-first from the cell just upstream of the dam centre; a cell joins the reservoir when its
        /// ground is below the surface level AND it lies on the upstream side of the dam line, so the water never leaks
        /// past the barrier. The walk is capped by a cell budget and a window derived from it; reaching either sets
        /// <see cref="ImpoundedReservoir.reachedSearchLimit"/> rather than pretending the extent is final. </summary>
        public static ImpoundedReservoir FloodFill(ITerrainSurfaceReader ground, double centreX, double centreZ,
            double axisHeadingDeg, double upstreamHeadingDeg, double surfaceLevelMetres, double cellSizeMetres,
            int maxCells)
        {
            ImpoundedReservoir r = new ImpoundedReservoir();
            r.surfaceLevelMetres = surfaceLevelMetres;
            r.cellSizeMetres = cellSizeMetres;
            if (ground == null) { r.refusal = "no terrain reader was supplied"; return r; }
            if (IsBad(surfaceLevelMetres)) { r.refusal = "the held level is not a finite number"; return r; }
            if (cellSizeMetres <= 0.0 || IsBad(cellSizeMetres)) { r.refusal = "cell size must be positive"; return r; }
            if (maxCells <= 0) { r.refusal = "the flood-fill budget must be positive"; return r; }

            // Upstream unit normal (heading 0 = +Z, 90 = +X), and the dam line's own direction for the half-plane test.
            double uRad = upstreamHeadingDeg * Math.PI / 180.0;
            double ux = Math.Sin(uRad);
            double uz = Math.Cos(uRad);
            double uLen = Math.Sqrt(ux * ux + uz * uz);
            if (uLen < 1e-9) { r.refusal = "the upstream heading has no direction"; return r; }
            ux /= uLen; uz /= uLen;

            int maxSpan = (int)Math.Ceiling(Math.Sqrt(maxCells)) + 2;
            HashSet<long> visited = new HashSet<long>();
            Queue<int> qi = new Queue<int>();
            Queue<int> qj = new Queue<int>();

            // Seed one cell upstream of the centre, so the fill starts on the water side of the line. The upstream
            // normal is a unit vector, so the nearest whole-cell offset along it is just its rounded components.
            int si = (int)Math.Round(ux);
            int sj = (int)Math.Round(uz);
            if (si == 0 && sj == 0) { si = ux >= 0.0 ? 1 : -1; }
            Enqueue(visited, qi, qj, si, sj);

            int count = 0;
            double volume = 0.0;
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
            bool limit = false;

            int[] di = { 1, -1, 0, 0 };
            int[] dj = { 0, 0, 1, -1 };

            while (qi.Count > 0)
            {
                if (count >= maxCells) { limit = true; break; }
                int i = qi.Dequeue();
                int j = qj.Dequeue();

                double wx = centreX + i * cellSizeMetres;
                double wz = centreZ + j * cellSizeMetres;

                // Upstream-side test: the cell centre must be on the water side of the dam line through the centre.
                double side = (wx - centreX) * ux + (wz - centreZ) * uz;
                if (side < 0.0) { continue; }

                float h = ground.SampleHeight((float)wx, (float)wz);
                if (float.IsNaN(h) || float.IsInfinity(h)) { continue; }
                if (h >= surfaceLevelMetres) { continue; }

                count++;
                volume += (surfaceLevelMetres - h) * cellSizeMetres * cellSizeMetres;
                if (wx < minX) { minX = wx; }
                if (wx > maxX) { maxX = wx; }
                if (wz < minZ) { minZ = wz; }
                if (wz > maxZ) { maxZ = wz; }

                for (int k = 0; k < 4; k++)
                {
                    int ni = i + di[k];
                    int nj = j + dj[k];
                    if (Math.Abs(ni) > maxSpan || Math.Abs(nj) > maxSpan) { limit = true; continue; }
                    Enqueue(visited, qi, qj, ni, nj);
                }
            }

            r.floodedCellCount = count;
            r.surfaceAreaSquareMetres = count * cellSizeMetres * cellSizeMetres;
            r.storageVolumeCubicMetres = volume;
            if (count > 0)
            {
                r.minX = minX; r.maxX = maxX; r.minZ = minZ; r.maxZ = maxZ;
            }
            r.reachedSearchLimit = limit;
            r.resolved = true;
            r.refusal = string.Empty;
            return r;
        }

        /// <summary> Size the spillway by the weir equation. The design head is the reservoir surface above the
        /// spillway crest; a crest at or above the reservoir passes nothing, which is a refusal, not a zero dressed up
        /// as an answer. </summary>
        public static SpillwaySizing SizeSpillway(SpillwayType type, double reservoirSurfaceLevelMetres,
            double spillwayCrestLevelMetres, double widthMetres)
        {
            SpillwaySizing s = new SpillwaySizing();
            s.type = type;
            s.crestLevelMetres = spillwayCrestLevelMetres;
            s.widthMetres = widthMetres;
            if (IsBad(reservoirSurfaceLevelMetres) || IsBad(spillwayCrestLevelMetres) || IsBad(widthMetres))
            {
                s.refusal = "a spillway figure is not a finite number";
                return s;
            }
            if (widthMetres <= 0.0) { s.refusal = "spillway width must be positive"; return s; }
            double head = reservoirSurfaceLevelMetres - spillwayCrestLevelMetres;
            if (head <= 0.0)
            {
                s.refusal = "the spillway crest at " + Fmt(spillwayCrestLevelMetres) + " m is at or above the reservoir "
                    + "surface at " + Fmt(reservoirSurfaceLevelMetres) + " m, so it passes no flow";
                return s;
            }
            s.designHeadMetres = head;
            s.dischargeCumecs = DischargeCoefficient(type) * widthMetres * Math.Pow(head, 1.5);
            s.resolved = true;
            s.refusal = string.Empty;
            return s;
        }

        /// <summary> Size the low-level outlet by the orifice equation. Present only when a bore is given; a bore at or
        /// above the reservoir has no head and is reported as such. </summary>
        public static OutletSizing SizeOutlet(double reservoirSurfaceLevelMetres, double invertLevelMetres,
            double boreDiameterMetres)
        {
            OutletSizing o = new OutletSizing();
            o.invertLevelMetres = invertLevelMetres;
            o.boreDiameterMetres = boreDiameterMetres;
            if (boreDiameterMetres <= 0.0) { o.present = false; o.refusal = string.Empty; return o; }
            if (IsBad(reservoirSurfaceLevelMetres) || IsBad(invertLevelMetres) || IsBad(boreDiameterMetres))
            {
                o.refusal = "an outlet figure is not a finite number";
                return o;
            }
            double head = reservoirSurfaceLevelMetres - invertLevelMetres;
            if (head <= 0.0)
            {
                o.refusal = "the outlet invert at " + Fmt(invertLevelMetres) + " m is at or above the reservoir "
                    + "surface, so there is no head to drive it";
                return o;
            }
            double area = Math.PI * (boreDiameterMetres * 0.5) * (boreDiameterMetres * 0.5);
            o.staticHeadMetres = head;
            o.dischargeCumecs = OrificeCoefficient * area * Math.Sqrt(2.0 * Gravity * head);
            o.present = true;
            o.refusal = string.Empty;
            return o;
        }

        /// <summary> Unit weight of mass concrete, in newtons per cubic metre: 2400 kg/m3 under standard gravity. The
        /// figure USBR's Design of Small Dams carries for unreinforced mass concrete, which is what a gravity section
        /// is -- it is placed for weight, not for tension, so it holds very little steel. </summary>
        public const double ConcreteUnitWeightNewtonsPerCubicMetre = 2400.0 * Gravity;

        /// <summary> Unit weight of fresh water, in newtons per cubic metre, at the same standard gravity. </summary>
        public const double WaterUnitWeightNewtonsPerCubicMetre = 1000.0 * Gravity;

        /// <summary> The sliding factor of safety a gravity dam is designed to clear on the normal load case with no
        /// cohesion counted (USBR). Reported against, never silently enforced. </summary>
        public const double MinSlidingFactorOfSafety = 1.5;

        /// <summary> The overturning factor of safety asked for about the downstream toe on the normal load case.
        /// </summary>
        public const double MinOverturningFactorOfSafety = 2.0;

        /// <summary> Whether the section would stand, by the sliding and overturning checks. See
        /// <see cref="DamStability"/> for why the water load is the reservoir head rather than the structural height,
        /// why uplift is in the sum, and why the centroid is decomposed rather than taken as B/3.
        ///
        /// <paramref name="frictionAngleDeg"/> is the friction angle of the concrete-on-rock contact; 45 degrees is
        /// sound rock. <paramref name="cohesionPascals"/> is the contact's cohesion -- pass zero for the check the
        /// codes actually ask for, because cohesion is the part of the shear strength a fissure destroys and it is
        /// conventionally not counted on unless drilling has proven it. </summary>
        public static DamStability Stability(DamProfile profile, double tailwaterLevelMetres, double frictionAngleDeg,
            double cohesionPascals)
        {
            DamStability s = new DamStability();
            if (!profile.resolved)
            {
                s.refusal = "the dam body was refused, so there is no section to check: " + profile.refusal;
                return s;
            }
            if (IsBad(tailwaterLevelMetres) || IsBad(frictionAngleDeg) || IsBad(cohesionPascals))
            {
                s.refusal = "a stability figure is not a finite number";
                return s;
            }
            if (frictionAngleDeg <= 0.0 || frictionAngleDeg >= 90.0)
            {
                s.refusal = "the foundation friction angle must lie between 0 and 90 degrees";
                return s;
            }
            if (cohesionPascals < 0.0) { s.refusal = "cohesion cannot be negative"; return s; }

            double height = profile.structuralHeightMetres;
            double foundationLevel = profile.crestLevelMetres - height;
            double baseWidth = profile.baseWidthMetres;

            // The section, front to back at the base: the upstream batter runs out by height * its run-per-rise, the
            // crest width sits in the middle, the downstream batter runs out by height * its own run-per-rise.
            double upstreamRun = height * profile.upstreamRunPerRise;
            double downstreamRun = height * profile.downstreamRunPerRise;

            // Area-weighted centroid, measured from the upstream heel. The two batters are triangles whose centroids
            // sit one third of the run from their own vertical side; the crest block is a rectangle centred on itself.
            double areaUpstream = 0.5 * upstreamRun * height;
            double areaMiddle = profile.crestWidthMetres * height;
            double areaDownstream = 0.5 * downstreamRun * height;
            double area = areaUpstream + areaMiddle + areaDownstream;
            if (area <= 0.0) { s.refusal = "the section encloses no area, so it has no weight"; return s; }

            double centroidUpstream = upstreamRun * 2.0 / 3.0;
            double centroidMiddle = upstreamRun + profile.crestWidthMetres * 0.5;
            double centroidDownstream = upstreamRun + profile.crestWidthMetres + downstreamRun / 3.0;
            double centroid = (areaUpstream * centroidUpstream + areaMiddle * centroidMiddle
                + areaDownstream * centroidDownstream) / area;

            // Water loads. The reservoir stands at crest minus freeboard, NOT at the crest.
            double head = profile.reservoirSurfaceLevelMetres - foundationLevel;
            if (head < 0.0) { head = 0.0; }
            double tailDepth = tailwaterLevelMetres - foundationLevel;
            if (tailDepth < 0.0) { tailDepth = 0.0; }
            if (tailDepth > height) { tailDepth = height; }

            double gammaW = WaterUnitWeightNewtonsPerCubicMetre;
            double weight = area * ConcreteUnitWeightNewtonsPerCubicMetre;

            // Horizontal hydrostatic thrust per metre run is the triangular pressure prism, gamma * h^2 / 2, and it
            // acts at h/3 above the base. Tailwater pushes the other way with the same law.
            double pushUpstream = gammaW * head * head * 0.5;
            double pushDownstream = gammaW * tailDepth * tailDepth * 0.5;
            double netPush = pushUpstream - pushDownstream;

            // Uplift on the contact: full reservoir pressure at the heel falling linearly to tailwater at the toe.
            double pressureHeel = gammaW * head;
            double pressureToe = gammaW * tailDepth;
            double uplift = 0.5 * (pressureHeel + pressureToe) * baseWidth;

            // Where that trapezoid of pressure acts, measured from the heel. For a linear distribution the centroid
            // sits at B/3 * (p_heel + 2 * p_toe) / (p_heel + p_toe); equal pressures give B/2, as they must.
            double pressureSum = pressureHeel + pressureToe;
            double upliftArmFromHeel = pressureSum <= 0.0 ? baseWidth * 0.5
                : baseWidth * (pressureHeel + 2.0 * pressureToe) / (3.0 * pressureSum);

            double effectiveWeight = weight - uplift;
            double frictionRad = frictionAngleDeg * Math.PI / 180.0;
            double resistingShear = (effectiveWeight > 0.0 ? effectiveWeight : 0.0) * Math.Tan(frictionRad)
                + cohesionPascals * baseWidth;

            s.weightPerMetreNewtons = weight;
            s.upliftPerMetreNewtons = uplift;
            s.netHorizontalPerMetreNewtons = netPush;
            s.centroidFromHeelMetres = centroid;
            s.headMetres = head;

            // Moments about the downstream toe. Weight and uplift both act on lever arms measured from the toe, so a
            // distance from the heel becomes baseWidth minus itself. Uplift is destabilising: it lifts the heel.
            double weightArm = baseWidth - centroid;
            double upliftArm = baseWidth - upliftArmFromHeel;
            double stabilising = weight * weightArm;
            double overturning = pushUpstream * (head / 3.0) + uplift * upliftArm
                - pushDownstream * (tailDepth / 3.0);

            s.slidingFactorOfSafety = netPush <= 0.0 ? double.PositiveInfinity : resistingShear / netPush;
            s.overturningFactorOfSafety = overturning <= 0.0 ? double.PositiveInfinity : stabilising / overturning;
            s.resolved = true;
            s.refusal = string.Empty;
            return s;
        }

        /// <summary> The whole dam, from geometry and terrain to the request UC answers. Reads the valley first (its
        /// floor sets the structural height), then the body, then floods to the held level, sizes the two outlets, and
        /// reads the downstream toe for the tailwater. A reservoir that impounds nothing is not a refusal -- it is an
        /// honest zero the report states -- but a valley that cannot be read or a body that cannot stand is. </summary>
        public static DamRequest Resolve(ITerrainSurfaceReader ground, double damCentreX, double damCentreZ,
            double axisHeadingDeg, double upstreamHeadingDeg, double crestLevelMetres, double freeboardMetres,
            double crestLengthMetres, double crestWidthMetres, double upstreamRunPerRise, double downstreamRunPerRise,
            SpillwayType spillwayType, double spillwayCrestLevelMetres, double spillwayWidthMetres,
            double outletInvertLevelMetres, double outletBoreDiameterMetres, double downstreamWaterDepthMetres,
            double cellSizeMetres, int maxFloodCells, int valleySamples)
        {
            DamRequest req = new DamRequest();
            if (ground == null)
            {
                req.refusal = "no terrain reader was supplied, so nothing can be read or stated";
                return req;
            }

            req.valley = SampleValley(ground, damCentreX, damCentreZ, axisHeadingDeg, crestLengthMetres,
                crestLevelMetres, valleySamples);
            if (!req.valley.resolved)
            {
                req.refusal = "the valley section could not be read: " + req.valley.refusal;
                return req;
            }

            DamProfile profile;
            if (!TryProfile(crestLevelMetres, req.valley.valleyFloorLevelMetres, freeboardMetres, crestLengthMetres,
                crestWidthMetres, upstreamRunPerRise, downstreamRunPerRise, out profile))
            {
                req.profile = profile;
                req.refusal = "the dam body is not valid: " + profile.refusal;
                return req;
            }
            req.profile = profile;

            req.reservoir = FloodFill(ground, damCentreX, damCentreZ, axisHeadingDeg, upstreamHeadingDeg,
                profile.reservoirSurfaceLevelMetres, cellSizeMetres, maxFloodCells);
            req.spillway = SizeSpillway(spillwayType, profile.reservoirSurfaceLevelMetres, spillwayCrestLevelMetres,
                spillwayWidthMetres);
            req.outlet = SizeOutlet(profile.reservoirSurfaceLevelMetres, outletInvertLevelMetres,
                outletBoreDiameterMetres);

            // Tailwater: the downstream toe ground plus the river's natural depth there. The toe sits a face-run beyond
            // the crest on the downstream side.
            double downRad = (upstreamHeadingDeg + 180.0) * Math.PI / 180.0;
            double dx = Math.Sin(downRad);
            double dz = Math.Cos(downRad);
            double toeDist = crestWidthMetres * 0.5 + profile.structuralHeightMetres * downstreamRunPerRise;
            float toe = ground.SampleHeight((float)(damCentreX + dx * toeDist), (float)(damCentreZ + dz * toeDist));
            double toeGround = (float.IsNaN(toe) || float.IsInfinity(toe)) ? req.valley.valleyFloorLevelMetres : toe;
            double depth = (IsBad(downstreamWaterDepthMetres) || downstreamWaterDepthMetres < 0.0)
                ? 0.0 : downstreamWaterDepthMetres;
            req.tailwaterLevelMetres = toeGround + depth;

            req.resolved = true;
            req.refusal = string.Empty;
            return req;
        }

        private static void Enqueue(HashSet<long> visited, Queue<int> qi, Queue<int> qj, int i, int j)
        {
            long key = ((long)i << 32) | (uint)j;
            if (visited.Contains(key)) { return; }
            visited.Add(key);
            qi.Enqueue(i);
            qj.Enqueue(j);
        }

        private static bool IsBad(double v) { return double.IsNaN(v) || double.IsInfinity(v); }

        private static string Fmt(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}

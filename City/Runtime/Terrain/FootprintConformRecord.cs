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
    /// <summary> How a hand-placed object's footprint is turned into stations, and the tolerances the answer is then
    /// read against.
    ///
    /// The spacing is DERIVED from the footprint rather than inherited from <see cref="GroundDriftClass"/>. That class's
    /// 20 m is the metric highway station, which is the right interval along a road and proves nothing about an 8 m
    /// house: two stations on a house cannot tell a level slab from one resting on a single high corner. So the spacing
    /// comes from the object, bounded at both ends.
    ///
    /// <see cref="minimumStationSpacingMetres"/> is the floor, and it is a measured limit rather than a taste: a
    /// 1025-resolution heightmap over a 500 m tile is 500 / 1024 = 0.49 m per post, and between two posts the surface
    /// IS a plane, so a station in the middle returns a height its neighbours already implied.
    ///
    /// <see cref="maximumStationSpacingMetres"/> and <see cref="perimeterStationTarget"/> are AUTHORED in the sense
    /// <see cref="PadConformParams"/> uses the word -- deliberate figures with their reasoning beside them, not
    /// measurements. 3 m is the widest gap that can still hide a hollow of
    /// <see cref="GroundDriftClass.visibleMismatchMetres"/> depth while that hollow still reads as a dish under the base
    /// rather than as a stone under one corner. 24 is a COUNT and not a spacing, so the cost of scanning one object does
    /// not grow with the object: a 200 m warehouse and an 8 m shed both get about two dozen edge stations, and the
    /// bounds are what keep that sensible at either extreme.
    ///
    /// <see cref="shortestSideDivisions"/> is the narrow-dimension ceiling, and it is the one rule the perimeter count
    /// cannot express: a 60 m by 2 m wall has a 124 m perimeter, so a count over the perimeter samples it every few
    /// metres along a length nobody doubts and says nothing about the 2 m width where it actually rocks. Four stations
    /// across the narrow dimension is the fewest that distinguishes a bow from a tilt -- two show a tilt, three suggest a
    /// bow, four confirm the third was not noise.
    ///
    /// The order the four bounds are applied in is part of the rule and is set out on <see cref="SpacingFor"/>.
    ///
    /// <see cref="drift"/> is held here rather than passed separately because the tolerances and the station ceiling are
    /// one policy from an author's side, and because a second copy of 512, or of 25 mm, would be free to disagree with
    /// the first. </summary>
    public sealed class FootprintScanClass
    {
        public double maximumStationSpacingMetres = 3.0;
        public double minimumStationSpacingMetres = 0.5;
        public int shortestSideDivisions = 4;
        public int perimeterStationTarget = 24;

        /// <summary> Whether the inside of the footprint is sampled as well as its edge. On, because a bump under the
        /// middle of a slab lifts no corner: all four read low together, which an edge-only scan reports as ground that
        /// fell away when the truth is ground that rose. </summary>
        public bool includeInterior = true;

        /// <summary> How the objects being swept meet the ground, which decides whether a gap under one is a fault at
        /// all. <see cref="GroundContactMode.Piled"/> objects are recorded as ungrounded and pass by the same route a
        /// bridge deck does, so a raised walkway is not reported as a floating shed. </summary>
        public GroundContactMode contactMode = GroundContactMode.SkirtToTerrain;

        /// <summary> The tolerances the verdict is read against, and the station ceiling. </summary>
        public GroundDriftClass drift = GroundDriftClass.Default();

        /// <summary> How many objects one sweep will look at. 4096 is not a performance figure but a mistake guard: an
        /// author who names a scene root instead of a container gets a bounded wait and a sentence saying the sweep stopped
        /// early, rather than an editor that appears to have hung. Reaching it is REPORTED, the same way reaching the
        /// station ceiling is. </summary>
        public int maximumObjects = 4096;

        public static FootprintScanClass Default()
        {
            return new FootprintScanClass();
        }

        /// <summary> The station spacing for one footprint, in metres. <paramref name="shortestSideMetres"/> is the
        /// smaller side of the footprint's XZ extent -- its narrow dimension, not its shortest edge.
        ///
        /// The order is the whole of the rule. The perimeter count sets the figure; the eye's ceiling may only narrow it;
        /// the narrow dimension may only narrow it further, because a thin thing must be sampled across its thinness
        /// whatever its perimeter says; and the heightmap floor is applied LAST, because a station closer than one
        /// heightmap post returns a height its neighbours already implied and no earlier rule can make that useful.
        /// </summary>
        public double SpacingFor(double perimeterMetres, double shortestSideMetres)
        {
            int target = perimeterStationTarget > 0 ? perimeterStationTarget : 1;
            double spacing = perimeterMetres > 0.0 ? perimeterMetres / target : maximumStationSpacingMetres;
            if (maximumStationSpacingMetres > 0.0 && spacing > maximumStationSpacingMetres)
            {
                spacing = maximumStationSpacingMetres;
            }
            int divisions = shortestSideDivisions > 0 ? shortestSideDivisions : 1;
            if (shortestSideMetres > 0.0)
            {
                double across = shortestSideMetres / divisions;
                if (across < spacing) { spacing = across; }
            }
            if (spacing < minimumStationSpacingMetres) { spacing = minimumStationSpacingMetres; }
            return spacing > 0.0 ? spacing : 1.0;
        }
    }


    /// <summary> One hand-placed object's answer: what was measured under it, and in the author's own terms what that
    /// means. Carries the <see cref="GroundDrift"/> the road side already computes rather than restating any part of it.
    ///
    /// It has its own <see cref="Describe"/> instead of calling <see cref="GroundDrift.Describe"/> because that one's
    /// nouns are "road floating" and "road buried". A garden shed reported as a floating road is a worse outcome than two
    /// short sentence builders, and rewording the road's report to suit a shed is not this task's business. </summary>
    public struct FootprintScanFinding
    {
        public string objectName;

        /// <summary> The scene path from the scan root down, so two objects called "Shed" can be told apart in a list of
        /// findings that has no scene to click in. </summary>
        public string objectPath;

        public GroundDrift drift;

        /// <summary> The world height of the lowest point of the object's bounds -- the plane the ground under it is
        /// measured against. </summary>
        public float baseElevationMetres;

        public GroundContactMode contactMode;
        public double footprintAreaM2;

        /// <summary> The lot id this object's pad request was given, or -1 when no request was produced. The id is the
        /// request's index in the list it was added to, handed out in the order objects were found, which is stable for an
        /// unchanged hierarchy because the walk is depth-first over roots taken in order. </summary>
        public int lotId;

        public bool padRequested;

        /// <summary> True when an author needs to look at this one: either the ground disagrees with the base by more
        /// than the tolerance, or nothing could be concluded at all. A verdict nobody could reach is not a pass.
        /// </summary>
        public bool NeedsAttention
        {
            get
            {
                return drift.NeedsRefresh
                    || drift.verdict == GroundVerdict.NeverRecorded
                    || drift.verdict == GroundVerdict.GroundUnavailable;
            }
        }

        /// <summary> One line an author can act on. Shaped like <see cref="GroundDrift.Describe"/> -- name, verdict, then
        /// the detail -- so a findings list and a road report read the same way, but worded for a thing that stands on
        /// the ground rather than for a thing that runs along it. </summary>
        public string Describe()
        {
            string name = string.IsNullOrEmpty(objectName) ? "unnamed object" : objectName;
            StringBuilder b = new StringBuilder();
            b.Append(name).Append(": ").Append(drift.verdict.ToString()).Append(" -- ");
            if (drift.verdict == GroundVerdict.NeverRecorded)
            {
                b.Append("no footprint could be measured, so nothing was judged");
                return b.ToString();
            }
            if (drift.verdict == GroundVerdict.GroundUnavailable)
            {
                b.Append("the ground under it could not be read");
                if (drift.stationsUnreadable > 0)
                {
                    b.Append(" (").Append(drift.stationsUnreadable.ToString(CultureInfo.InvariantCulture))
                     .Append(" stations unreadable)");
                }
                return b.ToString();
            }
            if (contactMode == GroundContactMode.Piled)
            {
                b.Append("stands on piles, so the gap under it is by design and was not judged");
                return b.ToString();
            }
            if (drift.motion == GroundMotion.Reshaped)
            {
                b.Append("rocking: daylight up to ")
                 .Append(drift.worstFallMetres.ToString("F3", CultureInfo.InvariantCulture))
                 .Append(" m under one part and ")
                 .Append(drift.worstRiseMetres.ToString("F3", CultureInfo.InvariantCulture))
                 .Append(" m of ground over the base at another");
            }
            else if (drift.motion == GroundMotion.Fell)
            {
                b.Append("hanging clear of the ground by up to ")
                 .Append(drift.worstFallMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m");
            }
            else if (drift.motion == GroundMotion.Rose)
            {
                b.Append("its base buried by up to ")
                 .Append(drift.worstRiseMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m of ground");
            }
            else
            {
                b.Append("resting on the ground, worst gap ")
                 .Append(drift.WorstAbsoluteMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m");
            }
            b.Append(", worst at (").Append(drift.worstX.ToString("F1", CultureInfo.InvariantCulture)).Append(", ")
             .Append(drift.worstZ.ToString("F1", CultureInfo.InvariantCulture)).Append("); ")
             .Append(drift.stationsMeasured.ToString(CultureInfo.InvariantCulture)).Append(" stations at ")
             .Append(drift.effectiveStationSpacingMetres.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");
            if (drift.stationsUnreadable > 0)
            {
                b.Append(", ").Append(drift.stationsUnreadable.ToString(CultureInfo.InvariantCulture))
                 .Append(" unreadable");
            }
            if (padRequested)
            {
                b.Append("; levelling requested as lot ").Append(lotId.ToString(CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }
    }


    /// <summary> The ground under one hand-placed object's footprint, measured at stations derived from that footprint --
    /// the check the road side has had since RM-99 and the author's own buildings have never had.
    ///
    /// This is a RECORD SHAPE and not a second verdict. <see cref="GroundDriftClass"/>, <see cref="GroundMotion"/>,
    /// <see cref="GroundVerdict"/> and <see cref="GroundConformRecord.Evaluate"/> are reused exactly as the road uses
    /// them: the <see cref="conform"/> field IS a road record, its station list is filled from a footprint instead of
    /// from a spline, and <see cref="Evaluate"/> hands straight to it. A fork of the verdict vocabulary would let a shed
    /// and a road disagree about what 25 mm means.
    ///
    /// THE SUBSTITUTION THAT MAKES THE REUSE EXACT. <see cref="GroundStation.groundYAtBuild"/> means "the height the
    /// ground was when this was built", and the road's Evaluate reports how far the ground has since moved from it. For a
    /// hand-placed object the height the ground OUGHT to be is the object's own base, so the base goes into every
    /// station's groundYAtBuild and the same arithmetic answers a different question: not how far the ground moved, but
    /// how far it stands from the thing sitting on it. The three motions then carry the author's three complaints without
    /// being renamed -- ground below the base is <see cref="GroundMotion.Fell"/>, which is the daylight under a hanging
    /// object; ground above it is <see cref="GroundMotion.Rose"/>, which is a base sunk into a bump; one of each across
    /// the station set is <see cref="GroundMotion.Reshaped"/>, which is exactly "touching it in some places and not in
    /// others".
    ///
    /// NO READER IS NEEDED TO RECORD. The road's Record samples the terrain to learn what the ground was; here the
    /// reference height is the object's own base, so <see cref="Record"/> is pure geometry and only
    /// <see cref="Evaluate"/> touches the ground. A gate can therefore build a record and check the station layout with
    /// no terrain in the scene at all.
    ///
    /// NOT TO BE CONFUSED WITH <see cref="FootprintConform"/>, which is a different pass on a different thing: that one
    /// removes cells of a SETTLEMENT's footprint that fall in water or on slope, before anything is built. This one
    /// measures the ground under ONE already-placed object and never removes anything. </summary>
    public sealed class FootprintConformRecord
    {
        public string objectName = string.Empty;
        public string objectPath = string.Empty;

        /// <summary> The footprint ring in world space, XZ, counter-clockwise. Its y values carry the base elevation, so
        /// it can be handed to <see cref="PadRequest"/> unchanged. </summary>
        public readonly List<Vector3> footprint = new List<Vector3>();

        public float baseElevationMetres;
        public GroundContactMode contactMode = GroundContactMode.SkirtToTerrain;
        public double perimeterMetres;
        public double shortestSideMetres;
        public double areaM2;
        public int perimeterStations;
        public int interiorStations;

        /// <summary> The road's own record, holding this footprint's stations. Composed rather than inherited from
        /// because a footprint is not a spline: nothing here wants <see cref="GroundConformRecord.Record"/>. </summary>
        public readonly GroundConformRecord conform = new GroundConformRecord();

        public bool HasStations { get { return conform.HasStations; } }
        public double EffectiveStationSpacingMetres { get { return conform.effectiveStationSpacingMetres; } }
        public int StationCount { get { return conform.stations.Count; } }

        /// <summary> Lay out the stations for one footprint. Pure geometry: the ground is not touched here, only in
        /// <see cref="Evaluate"/>.
        ///
        /// <paramref name="footprintWorld"/> is a world-space ring; fewer than three vertices, or a ring with no extent,
        /// produces a record with no stations, which <see cref="Evaluate"/> answers with
        /// <see cref="GroundVerdict.NeverRecorded"/> rather than with a pass. </summary>
        public static FootprintConformRecord Record(string objectName, string objectPath,
            IList<Vector3> footprintWorld, float baseElevationMetres, FootprintScanClass cls)
        {
            FootprintScanClass c = cls != null ? cls : FootprintScanClass.Default();
            FootprintConformRecord record = new FootprintConformRecord();
            record.objectName = objectName != null ? objectName : string.Empty;
            record.objectPath = objectPath != null ? objectPath : string.Empty;
            record.baseElevationMetres = baseElevationMetres;
            record.contactMode = c.contactMode;
            record.conform.roadName = record.objectName;
            if (footprintWorld == null || footprintWorld.Count < 3) { return record; }

            for (int i = 0; i < footprintWorld.Count; i++)
            {
                Vector3 v = footprintWorld[i];
                record.footprint.Add(new Vector3(v.x, baseElevationMetres, v.z));
            }
            // Counter-clockwise, the winding this package's face polygons carry, so a footprint from anywhere reads the
            // same way to anything downstream that cares.
            if (Geometry2D.SignedDoubleArea(record.footprint) < 0f) { record.footprint.Reverse(); }

            float minX, maxX, minZ, maxZ;
            Extent(record.footprint, out minX, out maxX, out minZ, out maxZ);
            record.perimeterMetres = PerimeterOf(record.footprint);
            double width = maxX - minX;
            double depth = maxZ - minZ;
            record.shortestSideMetres = width < depth ? width : depth;
            record.areaM2 = Geometry2D.Area(record.footprint);
            if (record.perimeterMetres <= 0.0 || record.shortestSideMetres <= 0.0) { return record; }

            Layout(record, c, minX, maxX, minZ, maxZ);
            return record;
        }

        /// <summary> Fill the station list, widening the spacing until it fits under
        /// <see cref="GroundDriftClass.maximumStations"/>. Widening rather than truncating is the rule that class already
        /// states for a long road: reaching the ceiling LOSES detail, so the spacing actually used is what gets reported.
        /// Truncating would instead leave one end of the footprint unmeasured while still reporting a pass over it.
        ///
        /// The correction factor is a square root because the interior is a grid, so halving the station count means
        /// widening by about 1.41; the 1.05 minimum guarantees the loop advances even when the overshoot is tiny.
        /// </summary>
        private static void Layout(FootprintConformRecord record, FootprintScanClass cls,
            float minX, float maxX, float minZ, float maxZ)
        {
            double spacing = cls.SpacingFor(record.perimeterMetres, record.shortestSideMetres);
            GroundDriftClass drift = cls.drift != null ? cls.drift : GroundDriftClass.Default();
            int cap = drift.maximumStations > 8 ? drift.maximumStations : 8;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                record.conform.stations.Clear();
                record.perimeterStations = AddPerimeterStations(record, spacing);
                record.interiorStations = cls.includeInterior
                    ? AddInteriorStations(record, spacing, minX, maxX, minZ, maxZ)
                    : 0;
                int total = record.conform.stations.Count;
                if (total <= cap) { break; }
                double factor = System.Math.Sqrt((double)total / cap);
                spacing *= factor > 1.05 ? factor : 1.05;
            }
            record.conform.effectiveStationSpacingMetres = spacing;
        }

        /// <summary> Every corner, plus stations stepped along each edge. The corner is always taken because a base shows
        /// daylight at a corner first, and because a footprint edge shorter than one spacing would otherwise contribute
        /// no station at all. </summary>
        private static int AddPerimeterStations(FootprintConformRecord record, double spacing)
        {
            List<Vector3> ring = record.footprint;
            int added = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % ring.Count];
                AddStation(record, a.x, a.z);
                added++;
                double dx = b.x - a.x;
                double dz = b.z - a.z;
                double length = System.Math.Sqrt(dx * dx + dz * dz);
                if (length <= spacing) { continue; }
                int steps = (int)(length / spacing);
                for (int s = 1; s <= steps; s++)
                {
                    double t = (s * spacing) / length;
                    if (t >= 1.0) { break; }
                    AddStation(record, (float)(a.x + dx * t), (float)(a.z + dz * t));
                    added++;
                }
            }
            return added;
        }

        /// <summary> A lattice over the footprint's extent, kept to the points that fall inside the ring. The lattice is
        /// offset by half a spacing so no row or column lands exactly on an edge, which is the one case
        /// <see cref="Geometry2D.Contains"/> answers inconsistently by its own account.
        ///
        /// A footprint smaller than one spacing gets its area centroid instead, because a footprint with no interior
        /// station is exactly the case an edge-only scan gets wrong: a small shed on a dome reads low at all four corners,
        /// which is a fall, when the truth is a rise under its middle. </summary>
        private static int AddInteriorStations(FootprintConformRecord record, double spacing,
            float minX, float maxX, float minZ, float maxZ)
        {
            List<Vector3> ring = record.footprint;
            int added = 0;
            for (double z = minZ + spacing * 0.5; z < maxZ; z += spacing)
            {
                for (double x = minX + spacing * 0.5; x < maxX; x += spacing)
                {
                    if (!Geometry2D.Contains(ring, new Vector2((float)x, (float)z))) { continue; }
                    AddStation(record, (float)x, (float)z);
                    added++;
                }
            }
            if (added == 0)
            {
                Vector2 centre = Geometry2D.Centroid(ring);
                if (Geometry2D.Contains(ring, centre))
                {
                    AddStation(record, centre.x, centre.y);
                    added++;
                }
            }
            return added;
        }

        /// <summary> One station. Both heights are the object's base: <see cref="GroundStation.groundYAtBuild"/> is what
        /// the road's Evaluate measures the ground against, and <see cref="GroundStation.roadYAtBuild"/> carries the same
        /// figure rather than being left at zero, because a station holding a zero reads as a surface at sea level to
        /// anything that inspects the list later.
        ///
        /// <see cref="GroundContactMode.Piled"/> objects are recorded ungrounded, which routes them through the road's own
        /// "no grounded stations, so nothing to answer for" branch -- the same mechanism that stops every bridge span
        /// reporting as a floating road. No new flag and no new branch. </summary>
        private static void AddStation(FootprintConformRecord record, float worldX, float worldZ)
        {
            GroundStation station = new GroundStation();
            station.worldX = worldX;
            station.worldZ = worldZ;
            station.groundYAtBuild = record.baseElevationMetres;
            station.roadYAtBuild = record.baseElevationMetres;
            station.grounded = record.contactMode != GroundContactMode.Piled;
            record.conform.stations.Add(station);
        }

        private static double PerimeterOf(List<Vector3> ring)
        {
            double sum = 0.0;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % ring.Count];
                double dx = b.x - a.x;
                double dz = b.z - a.z;
                sum += System.Math.Sqrt(dx * dx + dz * dz);
            }
            return sum;
        }

        private static void Extent(List<Vector3> ring, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = ring[0].x; maxX = ring[0].x; minZ = ring[0].z; maxZ = ring[0].z;
            for (int i = 1; i < ring.Count; i++)
            {
                Vector3 v = ring[i];
                if (v.x < minX) { minX = v.x; }
                if (v.x > maxX) { maxX = v.x; }
                if (v.z < minZ) { minZ = v.z; }
                if (v.z > maxZ) { maxZ = v.z; }
            }
        }

        /// <summary> The verdict, computed by the ROAD's own <see cref="GroundConformRecord.Evaluate"/> over the stations
        /// laid out here. This wrapper exists so a caller holding a footprint record does not have to know that a road
        /// record is inside it; it adds no arithmetic of its own, which is the point. </summary>
        public GroundDrift Evaluate(ITerrainSurfaceReader ground, GroundDriftClass cls)
        {
            return conform.Evaluate(ground, cls);
        }

        /// <summary> The pad request that would level the ground under this object, for
        /// <see cref="BuildingPadConform.ComputePads"/> to turn into a pad and an apron.
        ///
        /// The footprint is COPIED because <see cref="PadRecord"/> keeps the list it is given, and a later re-scan
        /// rebuilding this record would otherwise reach into a request already handed on.
        ///
        /// No elevation is passed, and there is nowhere to pass one: <see cref="PadRequest"/> carries a
        /// <see cref="PadElevationRule"/> and the pad conform decides the level from terrain samples. So this asks for the
        /// ROCKING to be taken out, and the residual offset between the object's base and the pad it ends up standing on is
        /// reported as a number in the finding rather than silently closed. Moving the object to meet the pad is
        /// <see cref="GroundSeating"/>'s job and is only ever done to objects this tool placed. </summary>
        public PadRequest ToPadRequest(int lotId, PadElevationRule rule)
        {
            return new PadRequest(lotId, new List<Vector3>(footprint), rule, contactMode);
        }
    }


    /// <summary> The scene sweep: walk the roots an author named, measure the ground under everything standing under
    /// them, and hand back one finding per object -- plus, only when asked for, the pad requests that would fix them.
    ///
    /// WHAT COUNTS AS ONE OBJECT. The first Transform at or below a named root that has a renderer or a collider OF ITS
    /// OWN. Empty containers are walked through at any depth, so naming a "Buildings" object scans the buildings under it
    /// and naming a district container scans the buildings inside each district. The author's own objects are plain
    /// prefabs and carry no component of this tool, which is why the test is geometry rather than an RCM type; objects
    /// this tool placed are scanned on the same terms, and a bad verdict on one of those is a bug report about the tool
    /// rather than about the scene.
    ///
    /// ONE LIMITATION, STATED PLAINLY. A prefab whose own root carries neither renderer nor collider is scanned as its
    /// PARTS, so a roof part reads as hanging clear of the ground by its own height. Name that prefab's own Transform as
    /// a scan root, or give its root a collider, and it is scanned as one thing. The alternative rule -- take the whole
    /// subtree of every direct child of a root -- gets that case right and a container-of-containers wrong, which is the
    /// worse failure because it would ask for ground to be levelled under a whole district.
    ///
    /// THE FOOTPRINT IS AN AXIS-ALIGNED RECTANGLE. A plain prefab exposes no footprint polygon, so the object's world
    /// bounds are used: exact for something standing square to the world, and generous for something turned on the spot.
    /// Generous in the safe direction -- it measures ground the object does not quite stand on, rather than missing
    /// ground it does -- and the excess is bounded by the same corner geometry that the pad conform's 4 m apron already
    /// spans. That is the right direction for a REPORT and the wrong one for a levelling, which is the reason producing
    /// pad requests is off until an author turns it on.
    ///
    /// NOTHING HERE WRITES A HEIGHTMAP VALUE, and nothing here moves an author's object. The sweep measures, reports, and
    /// at most REQUESTS; the request goes to <see cref="BuildingPadConform"/> and from there to the one channel that
    /// edits ground. </summary>
    public static class HandPlacedGroundScan
    {
        /// <summary> Measure everything under <paramref name="roots"/>.
        ///
        /// <paramref name="padRequests"/> is the report-only switch: pass null and nothing is requested, pass a list and
        /// the objects outside tolerance are added to it. The list must be the scan's own and start empty, because a
        /// request's lot id is its index in that list -- <see cref="BuildingPadConform.ComputePads"/> orders by lot id,
        /// so ids shared with somebody else's lots would interleave two unrelated sets of pads. </summary>
        public static List<FootprintScanFinding> Sweep(IList<Transform> roots, ITerrainSurfaceReader ground,
            FootprintScanClass cls, PadElevationRule padRule, List<PadRequest> padRequests, out string summary)
        {
            FootprintScanClass c = cls != null ? cls : FootprintScanClass.Default();
            List<FootprintScanFinding> findings = new List<FootprintScanFinding>();
            bool truncated;
            List<Transform> objects = CollectObjects(roots, c.maximumObjects, out truncated);
            for (int i = 0; i < objects.Count; i++)
            {
                Transform t = objects[i];
                FootprintScanFinding finding = new FootprintScanFinding();
                finding.objectName = t.name;
                finding.objectPath = PathOf(t);
                finding.contactMode = c.contactMode;
                finding.lotId = -1;
                List<Vector3> footprint;
                float baseY;
                if (!TryFootprintOf(t, out footprint, out baseY))
                {
                    finding.drift.verdict = GroundVerdict.NeverRecorded;
                    findings.Add(finding);
                    continue;
                }
                FootprintConformRecord record = FootprintConformRecord.Record(t.name, finding.objectPath, footprint,
                    baseY, c);
                finding.baseElevationMetres = baseY;
                finding.footprintAreaM2 = record.areaM2;
                finding.drift = record.Evaluate(ground, c.drift);
                if (padRequests != null && finding.drift.NeedsRefresh)
                {
                    finding.lotId = padRequests.Count;
                    finding.padRequested = true;
                    padRequests.Add(record.ToPadRequest(finding.lotId, padRule));
                }
                findings.Add(finding);
            }
            summary = Summarise(findings, truncated, c.maximumObjects);
            return findings;
        }

        /// <summary> Depth-first over each root, in the order the roots were given, which is what makes a re-scan of an
        /// unchanged hierarchy produce the same list in the same order -- and therefore the same lot ids. Nested roots are
        /// deduped, because naming both a district and a building inside it must not scan that building twice. </summary>
        public static List<Transform> CollectObjects(IList<Transform> roots, int maximumObjects, out bool truncated)
        {
            List<Transform> found = new List<Transform>();
            HashSet<Transform> seen = new HashSet<Transform>();
            truncated = false;
            if (roots == null) { return found; }
            int cap = maximumObjects > 0 ? maximumObjects : int.MaxValue;
            for (int i = 0; i < roots.Count; i++)
            {
                CollectUnder(roots[i], found, seen, cap, ref truncated);
            }
            return found;
        }

        private static void CollectUnder(Transform t, List<Transform> found, HashSet<Transform> seen, int cap,
            ref bool truncated)
        {
            if (t == null || truncated) { return; }
            if (HasOwnGeometry(t))
            {
                if (seen.Add(t))
                {
                    if (found.Count >= cap) { truncated = true; return; }
                    found.Add(t);
                }
                return;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                CollectUnder(t.GetChild(i), found, seen, cap, ref truncated);
                if (truncated) { return; }
            }
        }

        /// <summary> The scene path, so two objects called "Shed" can be told apart in a list of findings that has no
        /// scene to click in. </summary>
        public static string PathOf(Transform t)
        {
            if (t == null) { return string.Empty; }
            StringBuilder b = new StringBuilder(t.name);
            Transform parent = t.parent;
            while (parent != null)
            {
                b.Insert(0, "/").Insert(0, parent.name);
                parent = parent.parent;
            }
            return b.ToString();
        }

        private static bool HasOwnGeometry(Transform t)
        {
            Renderer renderer = t.GetComponent<Renderer>();
            if (renderer != null && CountsAsGeometry(renderer)) { return true; }
            return t.GetComponent<Collider>() != null;
        }

        /// <summary> Whether a renderer describes a thing that stands on the ground. A mesh does; a particle system, a
        /// trail and a line do not -- their bounds move with an effect, so a puff of smoke would otherwise be reported as
        /// a building hanging in the air and could ask for the ground under it to be levelled. </summary>
        private static bool CountsAsGeometry(Renderer renderer)
        {
            if (!renderer.enabled) { return false; }
            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
        }

        /// <summary> The footprint of one object: the XZ rectangle of its world bounds, at the height of the lowest point
        /// of those bounds. False when the object turned out to have no measurable extent, which the sweep reports as
        /// <see cref="GroundVerdict.NeverRecorded"/> rather than as a pass. </summary>
        public static bool TryFootprintOf(Transform t, out List<Vector3> footprint, out float baseElevationMetres)
        {
            footprint = null;
            baseElevationMetres = 0f;
            Bounds bounds;
            if (t == null || !TryWorldBounds(t, out bounds)) { return false; }
            if (bounds.size.x <= 0f || bounds.size.z <= 0f) { return false; }
            baseElevationMetres = bounds.min.y;
            float x0 = bounds.min.x, x1 = bounds.max.x, z0 = bounds.min.z, z1 = bounds.max.z;
            footprint = new List<Vector3>(4);
            footprint.Add(new Vector3(x0, baseElevationMetres, z0));
            footprint.Add(new Vector3(x1, baseElevationMetres, z0));
            footprint.Add(new Vector3(x1, baseElevationMetres, z1));
            footprint.Add(new Vector3(x0, baseElevationMetres, z1));
            return true;
        }

        /// <summary> The world bounds of an object and its parts. Trigger colliders are left out on purpose: a trigger is
        /// a volume somebody wants told about, not a thing resting on the ground, and a trigger box thrown loosely around a
        /// building would otherwise decide that building's footprint. Disabled renderers and colliders are left out for
        /// the same reason -- nothing stands there. </summary>
        private static bool TryWorldBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!CountsAsGeometry(renderers[i])) { continue; }
                if (!any) { bounds = renderers[i].bounds; any = true; }
                else { bounds.Encapsulate(renderers[i].bounds); }
            }
            Collider[] colliders = t.GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (!col.enabled || col.isTrigger) { continue; }
                if (!any) { bounds = col.bounds; any = true; }
                else { bounds.Encapsulate(col.bounds); }
            }
            return any;
        }

        /// <summary> One line over a whole sweep, for the place an author is actually looking: the counts, the single worst
        /// object by name and number, and whether the sweep stopped early. The per-object detail is in each finding's own
        /// <see cref="FootprintScanFinding.Describe"/>. </summary>
        public static string Summarise(IList<FootprintScanFinding> findings, bool truncated, int maximumObjects)
        {
            if (findings == null || findings.Count == 0)
            {
                return "Scanned nothing: no object under the scan roots had renderer or collider bounds.";
            }
            int sitting = 0, hanging = 0, buried = 0, rocking = 0, unjudged = 0, requested = 0;
            double worst = -1.0;
            string worstName = string.Empty;
            for (int i = 0; i < findings.Count; i++)
            {
                FootprintScanFinding f = findings[i];
                if (f.padRequested) { requested++; }
                if (f.drift.verdict == GroundVerdict.NeverRecorded
                    || f.drift.verdict == GroundVerdict.GroundUnavailable)
                {
                    unjudged++;
                    continue;
                }
                if (f.drift.motion == GroundMotion.Fell) { hanging++; }
                else if (f.drift.motion == GroundMotion.Rose) { buried++; }
                else if (f.drift.motion == GroundMotion.Reshaped) { rocking++; }
                else { sitting++; }
                if (f.drift.WorstAbsoluteMetres > worst)
                {
                    worst = f.drift.WorstAbsoluteMetres;
                    worstName = string.IsNullOrEmpty(f.objectName) ? "unnamed object" : f.objectName;
                }
            }
            StringBuilder b = new StringBuilder();
            b.Append("Scanned ").Append(findings.Count.ToString(CultureInfo.InvariantCulture)).Append(": ")
             .Append(sitting.ToString(CultureInfo.InvariantCulture)).Append(" sitting right, ")
             .Append(hanging.ToString(CultureInfo.InvariantCulture)).Append(" hanging clear, ")
             .Append(buried.ToString(CultureInfo.InvariantCulture)).Append(" with a buried base, ")
             .Append(rocking.ToString(CultureInfo.InvariantCulture)).Append(" rocking, ")
             .Append(unjudged.ToString(CultureInfo.InvariantCulture)).Append(" not judged.");
            if (worst >= 0.0 && !string.IsNullOrEmpty(worstName))
            {
                b.Append(" Worst is ").Append(worstName).Append(" at ")
                 .Append(worst.ToString("F3", CultureInfo.InvariantCulture)).Append(" m.");
            }
            b.Append(requested > 0
                ? " " + requested.ToString(CultureInfo.InvariantCulture) + " levelling requests produced."
                : " No levelling requested.");
            if (truncated)
            {
                b.Append(" Stopped at the ").Append(maximumObjects.ToString(CultureInfo.InvariantCulture))
                 .Append("-object limit, so the rest of the scan roots were not looked at: name a narrower root.");
            }
            return b.ToString();
        }
    }
}

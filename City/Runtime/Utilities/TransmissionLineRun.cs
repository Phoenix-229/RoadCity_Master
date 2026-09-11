// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> D-14. A cross-country overhead transmission line as a HAND-PLACED scene child, the same family as
    /// <see cref="RoadCityMaster.City.RoadPowerLine"/> and the rail alignment: an author draws where the towers march,
    /// and this computes the real steel and the real cables on it. It is deliberately NOT a graph node and NOT
    /// procedurally sited, because the spec says so in as many words -- a transmission line is a route somebody
    /// surveyed, not something a settlement generator sprinkles, and it must "never be inside a settlement". A node
    /// that placed it would be free to route it through a town; a hand-placed child cannot, and reports the crossing
    /// when its own corners stray into one.
    ///
    /// The route is a POLYLINE of authored corner points, not a road spline and not a clothoid alignment. A
    /// transmission line runs in straight legs and turns at angle towers; the corner IS the primitive, which is why
    /// <see cref="TransmissionLine.ChooseTowerType"/> takes a turn angle. Each leg is divided into even spans by
    /// <see cref="TransmissionLine.SpanCount"/> -- a pole/tower line chooses a span LENGTH and fits a whole number of
    /// equal spans, the mirror of the distribution line, and unlike the overhead-conductor walk whose span is a
    /// varying constraint.
    ///
    /// Everything real is borrowed, never re-derived: the catenary and its clearance come from
    /// <see cref="TransmissionLine"/>; the tower shape and its attachment heights from
    /// <see cref="TransmissionTowerClass"/> and <see cref="TransmissionTowerAttachments"/>; the four-foot seating and
    /// its verdict from B-15's <see cref="FootprintConformRecord"/>, NOT a second seating rule; the settlement
    /// rectangles from each scene <see cref="CityAuthoring"/>'s own region resolver. This file computes and refuses;
    /// it never mutates a scene, never touches an asset, and never draws (C-06). The editor-side builder turns a
    /// buildable plan into lattice-tower prefabs and conductor meshes, and the inspector edits it. </summary>


    /// <summary> The authored answers for one transmission run: the route, the electrical class, the steel, and the
    /// materials the builder is ASKED to use (never invented). A run with fewer than two corners is not a line and the
    /// planner refuses it by name rather than drawing a single tower in the void. </summary>
    [Serializable]
    public sealed class TransmissionLineRunSettings
    {
        public bool enabled = true;

        /// <summary> The route, in WORLD space, corner by corner. Y is authored only as a hint; the built towers are
        /// seated on the sampled ground, never on this Y, because ground height is measured and never authored -- the
        /// tool's convention everywhere. Two corners are the minimum; three or more introduce angle towers. </summary>
        public List<Vector3> routeCorners = new List<Vector3>();

        /// <summary> The electrical figures -- span length, sag, statutory clearance, the angle-tower threshold. The
        /// conductor sag and the clearance are engineering tables; they live on their own class for the same reason
        /// the pole line splits electrical from geometry. </summary>
        public TransmissionLineClass electrical = new TransmissionLineClass();

        /// <summary> The steelwork -- base spread, cross-arm height and length, phase spacing, earth-wire peak,
        /// insulator strings. The shape a buyer picks, apart from the sag a line engineer strings to. </summary>
        public TransmissionTowerClass tower = new TransmissionTowerClass();

        /// <summary> How many phases the circuit carries. Three is the ordinary single-circuit case; the phase count
        /// drives how many conductors each span strings and where they sit across the arm. </summary>
        public int phaseCount = 3;

        /// <summary> The materials the builder is ASKED to use -- one for the galvanised lattice steel, one for the
        /// insulator strings (ceramic or glass, never the steel's grey), one for the conductors, one for the earth wire.
        /// The insulator gets its own slot for the same reason the pole line splits hardware from timber: an
        /// insulator string is the tower's one non-steel part and a galvanised insulator reads as wrong at any distance.
        /// Null is honoured as "author has not chosen one yet" and reported by the inspector; the planner and the
        /// builder never invent a material, the same contract the pole line keeps. </summary>
        public Material steelMaterial;
        public Material insulatorMaterial;
        public Material conductorMaterial;
        public Material shieldWireMaterial;

        public static TransmissionLineRunSettings Default()
        {
            return new TransmissionLineRunSettings();
        }
    }


    /// <summary> One placed tower: where its base sits after four-foot seating, which way it faces, its type, and the
    /// attachment arithmetic the spans string to. A tower is FLAGGED, never dropped, when a foot hangs -- an author
    /// moves the corner or accepts the cut, and hiding it would strand a span on a tower that is not really down.
    /// </summary>
    [Serializable]
    public struct TransmissionRunTower
    {
        /// <summary> The corner index this tower belongs to on the route, or -1 for an intermediate suspension tower
        /// placed along a leg between two corners. </summary>
        public int cornerIndex;

        /// <summary> The tower base in world space AFTER seating to the lowest of the four feet. The tower's own base
        /// plane sits here; the steel rises from it and the phases hang at their attachment heights above it. </summary>
        public Vector3 basePoint;

        /// <summary> The yaw the cross-arm faces, degrees about world Y: perpendicular to the line, and at a corner the
        /// bisector of the two legs, so the arm splits the angle rather than favouring one leg. </summary>
        public float yawDegrees;

        public TowerType type;

        /// <summary> Whether this is a dead-end (the first or last tower of the whole run), which takes a strain string
        /// like an angle tower even on a straight pull, because a terminal carries the full one-sided tension. </summary>
        public bool isTerminal;

        /// <summary> The world Y the lowest foot sampled and the world Y the highest foot sampled, so the inspector can
        /// show how much the base was dropped and by how much the highest foot now stands proud of grade. </summary>
        public float lowestFootGroundY;
        public float highestFootGroundY;

        /// <summary> B-15's verdict on this tower's four-foot footprint against the seated base: Rose == the base is
        /// keyed into the hill (the good case), Fell == a foot hangs in air. Carried so the run's summary can name the
        /// towers that need attention without re-running the scan. </summary>
        public GroundVerdict footingVerdict;
        public GroundMotion footingMotion;

        /// <summary> The world Y the phases attach at (arm level, or an insulator-length below it on a suspension
        /// tower), and the world Y the earth wire rides. Absolute, so a span can be solved from two towers' attachment
        /// heights without re-reading either tower's shape. </summary>
        public float conductorAttachWorldY;
        public float shieldWireWorldY;

        /// <summary> The insulator string length this tower's type draws, reported so the builder hangs the right one.
        /// </summary>
        public float insulatorLengthMetres;
    }


    /// <summary> One span between two consecutive towers: the solved cable and its clearance verdict. The phases share
    /// this one solved catenary and reuse it offset sideways across the arm, exactly as a pole-line span does, rather
    /// than solving a curve per phase. </summary>
    [Serializable]
    public struct TransmissionRunSpan
    {
        public int fromTowerIndex;
        public int toTowerIndex;
        public float spanMetres;
        public Catenary catenary;
        public TransmissionLine.ClearanceResult clearance;
    }


    /// <summary> The full result of planning a run: the towers, the spans, and every refusal that stopped it being
    /// buildable. A plan with any refusal is NOT buildable -- the builder reads <see cref="Buildable"/> and does
    /// nothing on a false, so a settlement crossing or a hanging foot cannot be silently built over. </summary>
    public sealed class TransmissionLineRunPlan
    {
        public readonly List<TransmissionRunTower> towers = new List<TransmissionRunTower>();
        public readonly List<TransmissionRunSpan> spans = new List<TransmissionRunSpan>();
        public readonly List<string> refusals = new List<string>();

        public double routeLengthMetres;
        public double requiredClearanceMetres;
        public double worstClearanceMetres = double.PositiveInfinity;
        public int worstSpanIndex = -1;
        public bool clears = true;

        /// <summary> How many towers came back with a foot hanging (footing verdict not Rose/Agrees). A count rather
        /// than a bool so the summary can say "2 of 14 towers have a foot in air" and send the author to them. </summary>
        public int towersWithHangingFoot;

        /// <summary> Buildable exactly when there is something to build and nothing refused it. The builder must gate
        /// on this; a run that crosses a settlement, has too few corners, or cannot clear the ground is not built.
        /// </summary>
        public bool Buildable
        {
            get { return towers.Count > 0 && spans.Count > 0 && refusals.Count == 0; }
        }

        public string Summary()
        {
            StringBuilder b = new StringBuilder();
            if (refusals.Count > 0)
            {
                b.Append("Transmission run NOT buildable: ");
                for (int i = 0; i < refusals.Count; i++)
                {
                    if (i > 0) { b.Append("; "); }
                    b.Append(refusals[i]);
                }
                return b.ToString();
            }
            b.Append("Transmission run: ")
             .Append(towers.Count.ToString(CultureInfo.InvariantCulture)).Append(" towers, ")
             .Append(spans.Count.ToString(CultureInfo.InvariantCulture)).Append(" spans over ")
             .Append(routeLengthMetres.ToString("F0", CultureInfo.InvariantCulture)).Append(" m; ");
            if (clears)
            {
                b.Append("clears ground by ")
                 .Append(worstClearanceMetres.ToString("F2", CultureInfo.InvariantCulture))
                 .Append(" m at worst (needs ")
                 .Append(requiredClearanceMetres.ToString("F2", CultureInfo.InvariantCulture)).Append(" m)");
            }
            else
            {
                b.Append("FOULS ground: ")
                 .Append(worstClearanceMetres.ToString("F2", CultureInfo.InvariantCulture))
                 .Append(" m clearance at span ")
                 .Append(worstSpanIndex.ToString(CultureInfo.InvariantCulture)).Append(" needs ")
                 .Append(requiredClearanceMetres.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");
            }
            if (towersWithHangingFoot > 0)
            {
                b.Append("; ").Append(towersWithHangingFoot.ToString(CultureInfo.InvariantCulture))
                 .Append(" tower(s) with a foot in air");
            }
            return b.ToString();
        }
    }


    /// <summary> D-14's verifiable core: turn an authored route and a steel/electrical class into placed towers and
    /// solved spans, or into named refusals. PURE -- no scene, no asset, no clock, no random (C-06). The ground is
    /// reached only through <see cref="ITerrainSurfaceReader"/> and the settlement rectangles arrive as world
    /// <see cref="Rect"/> DATA, both passed in, which is what lets D-16 assert a whole run -- tower heights, span
    /// clearances, a settlement refusal -- with no scene present. </summary>
    public static class TransmissionLineRunPlanner
    {
        /// <summary> Ground samples taken across each span for the clearance test, ends included. Nine matches the
        /// pole line, dense enough to catch a hump mid-span without making the test a survey. </summary>
        public const int GroundSamplesPerSpan = 9;

        /// <summary> The one entry point. <paramref name="ground"/> is the same surface reader the build uses (tile in
        /// standalone, provider in connected mode); <paramref name="settlementBoundsWorldXZ"/> is every settlement's
        /// world XZ rectangle, gathered by the component from the scene and handed in as data so this stays pure.
        /// </summary>
        public static TransmissionLineRunPlan Plan(TransmissionLineRunSettings settings, ITerrainSurfaceReader ground,
            IList<Rect> settlementBoundsWorldXZ)
        {
            TransmissionLineRunPlan plan = new TransmissionLineRunPlan();
            TransmissionLineRunSettings s = settings != null ? settings : TransmissionLineRunSettings.Default();
            TransmissionLineClass elec = s.electrical != null ? s.electrical : TransmissionLineClass.Default();
            TransmissionTowerClass steel = s.tower != null ? s.tower : TransmissionTowerClass.Default();
            plan.requiredClearanceMetres = elec.minGroundClearanceMetres;

            if (!s.enabled)
            {
                return Refuse(plan, "the run is disabled");
            }
            List<Vector3> route = s.routeCorners;
            if (route == null || route.Count < 2)
            {
                return Refuse(plan, "a transmission run needs at least two corner points; it has "
                    + (route == null ? 0 : route.Count));
            }
            if (ground == null)
            {
                return Refuse(plan, "no terrain surface reader was supplied, so no tower can be seated on the ground");
            }
            if (s.phaseCount < 1)
            {
                return Refuse(plan, "the circuit carries " + s.phaseCount + " phases; a line needs at least one");
            }

            // A settlement crossing is reported before any steel is placed: the spec forbids a run being inside a
            // settlement, and a leg that enters one is the crossing to name. This does NOT depend on the ground, so it
            // is testable with no terrain at all.
            ReportSettlementCrossings(plan, route, settlementBoundsWorldXZ);

            BuildTowers(plan, route, s, elec, steel, ground);
            if (plan.towers.Count < 2)
            {
                return Refuse(plan, "the route resolved to fewer than two towers, so there is no span to string");
            }

            BuildSpans(plan, s, elec, steel, ground);

            // D-15. The run has produced its towers and spans; before anything calls it buildable, count how many of
            // each land in ONE streaming chunk and refuse a pathological run by its own number, rather than shipping a
            // chunk that stutters on the phone D-15 answers for. The count runs once here at plan time over a finite
            // run -- never per frame -- so it adds no play-time cost of its own.
            CheckPerChunkCeilings(plan);
            return plan;
        }

        // ------------------------------------------------------------------ per-chunk instance ceiling (D-15)

        /// <summary> D-15. A transmission run is INSTANCED content -- one cached tower mesh drawn per tower, one span
        /// group hung per span -- so the cost that can hurt a phone is not the total but how many of each fall in one
        /// streaming chunk. Count the busiest chunk for towers and for spans and refuse the run, naming its own count,
        /// when either is past the family's per-chunk ceiling (<see cref="CityInstanceBudget"/>); the author widens the
        /// spacing or splits the run rather than being handed a chunk that hitches. The chunk size is the streamer's own
        /// authored figure, so the "per chunk" the ceiling measures is the "per chunk" the game loads. </summary>
        private static void CheckPerChunkCeilings(TransmissionLineRunPlan plan)
        {
            float chunk = CityStreamingConfig.DefaultChunkSizeMetres;

            List<Vector3> towerPoints = new List<Vector3>(plan.towers.Count);
            for (int i = 0; i < plan.towers.Count; i++) { towerPoints.Add(plan.towers[i].basePoint); }
            int busiestTowers = CityInstanceBudget.BusiestChunkCount(towerPoints, chunk);
            CityInstanceCeilingVerdict towerVerdict =
                CityInstanceBudget.Check(CityInstanceFamily.TransmissionTower, busiestTowers);
            if (!towerVerdict.withinCeiling) { Refuse(plan, towerVerdict.message); }

            // A span is placed as one conductor group hung at the midpoint of its two towers; that midpoint is the
            // chunk it belongs to, the same cell the streamer would bucket the placed set into.
            List<Vector3> spanPoints = new List<Vector3>(plan.spans.Count);
            for (int i = 0; i < plan.spans.Count; i++)
            {
                TransmissionRunSpan span = plan.spans[i];
                if (span.fromTowerIndex < 0 || span.fromTowerIndex >= plan.towers.Count
                    || span.toTowerIndex < 0 || span.toTowerIndex >= plan.towers.Count) { continue; }
                Vector3 a = plan.towers[span.fromTowerIndex].basePoint;
                Vector3 b = plan.towers[span.toTowerIndex].basePoint;
                spanPoints.Add((a + b) * 0.5f);
            }
            int busiestSpans = CityInstanceBudget.BusiestChunkCount(spanPoints, chunk);
            CityInstanceCeilingVerdict spanVerdict =
                CityInstanceBudget.Check(CityInstanceFamily.TransmissionSpan, busiestSpans);
            if (!spanVerdict.withinCeiling) { Refuse(plan, spanVerdict.message); }
        }

        // ------------------------------------------------------------------ towers

        private static void BuildTowers(TransmissionLineRunPlan plan, List<Vector3> route,
            TransmissionLineRunSettings s, TransmissionLineClass elec, TransmissionTowerClass steel,
            ITerrainSurfaceReader ground)
        {
            double totalLength = 0.0;
            int lastLeg = route.Count - 2;

            for (int leg = 0; leg <= lastLeg; leg++)
            {
                Vector3 from = route[leg];
                Vector3 to = route[leg + 1];
                Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
                Vector3 flatTo = new Vector3(to.x, 0f, to.z);
                float legLength = Vector3.Distance(flatFrom, flatTo);
                totalLength += legLength;

                int spanCount = TransmissionLine.SpanCount(legLength, elec.spanLengthMetres);
                if (spanCount < 1) { spanCount = 1; }

                // Place the towers that BEGIN on this leg: the start corner (k = 0), then the intermediate suspension
                // towers (k = 1 .. spanCount - 1). The end corner is NOT placed here -- a non-final leg's end corner is
                // the next leg's start corner, and the final leg's end corner is the run's far terminal, placed once
                // after the loop. So no corner is ever seated twice.
                for (int k = 0; k < spanCount; k++)
                {
                    float t = spanCount == 0 ? 0f : (float)k / spanCount;
                    Vector3 at = Vector3.Lerp(flatFrom, flatTo, t);
                    bool isCorner = k == 0;
                    int cornerIndex = isCorner ? leg : -1;
                    float yaw = YawAt(route, leg, k, spanCount);
                    bool isTerminal = leg == 0 && k == 0;
                    TowerType type = TowerTypeAt(route, leg, k, isTerminal, elec.angleTowerThresholdDeg);
                    plan.towers.Add(SeatTower(cornerIndex, at, yaw, type, isTerminal, s, steel, ground));
                }
            }

            // The final corner: the run's far terminal, placed once, here.
            Vector3 endFlat = new Vector3(route[route.Count - 1].x, 0f, route[route.Count - 1].z);
            float endYaw = YawAt(route, lastLeg, -1, 0);
            plan.towers.Add(SeatTower(route.Count - 1, endFlat, endYaw, TowerType.Angle, true, s, steel, ground));

            plan.routeLengthMetres = totalLength;

            for (int i = 0; i < plan.towers.Count; i++)
            {
                if (plan.towers[i].footingVerdict != GroundVerdict.Agrees
                    && plan.towers[i].footingMotion != GroundMotion.Rose)
                {
                    plan.towersWithHangingFoot++;
                }
            }
        }

        /// <summary> Seat one tower on its four feet: sample the ground under each foot, drop the base to the LOWEST
        /// foot, key it in by the tower's setting depth, and take B-15's verdict on the footprint. This is the same
        /// scanner the hand-placed sweep uses, not a second seating rule -- a tower is a hand-placed object with four
        /// contact points, and the footprint IS its four feet. </summary>
        private static TransmissionRunTower SeatTower(int cornerIndex, Vector3 flatAt, float yawDegrees,
            TowerType type, bool isTerminal, TransmissionLineRunSettings s, TransmissionTowerClass steel,
            ITerrainSurfaceReader ground)
        {
            TransmissionRunTower tower = new TransmissionRunTower();
            tower.cornerIndex = cornerIndex;
            tower.yawDegrees = yawDegrees;
            tower.type = type;
            tower.isTerminal = isTerminal;

            List<Vector2> feet = steel.FootOffsets();
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);
            float lowest = float.PositiveInfinity;
            float highest = float.NegativeInfinity;
            List<Vector3> footprintWorld = new List<Vector3>(feet.Count);
            for (int i = 0; i < feet.Count; i++)
            {
                Vector3 local = new Vector3(feet[i].x, 0f, feet[i].y);
                Vector3 world = flatAt + rot * local;
                float g = ground.SampleHeight(world.x, world.z);
                if (!float.IsNaN(g) && !float.IsInfinity(g))
                {
                    if (g < lowest) { lowest = g; }
                    if (g > highest) { highest = g; }
                }
                footprintWorld.Add(new Vector3(world.x, g, world.z));
            }
            if (float.IsInfinity(lowest)) { lowest = flatAt.y; highest = flatAt.y; }

            tower.lowestFootGroundY = lowest;
            tower.highestFootGroundY = highest;
            // The base plane sits at the lowest foot; the steel is keyed in below it by the setting depth. Seating to
            // the lowest foot is what stops the far foot hanging on a slope: a base at the mean would leave the down-
            // hill foot in air, and a base at the highest would bury half the tower.
            tower.basePoint = new Vector3(flatAt.x, lowest, flatAt.z);

            // B-15's four-foot verdict: the footprint's four contact points against the seated base. Ground that Rose
            // relative to the base means the base is dug into the hill (good); Fell means a foot hangs.
            FootprintScanClass scan = FootprintScanClass.Default();
            FootprintConformRecord record = FootprintConformRecord.Record(
                "transmission tower", "transmission run", footprintWorld, lowest, scan);
            GroundDrift drift = record.Evaluate(ground, scan.drift);
            tower.footingVerdict = drift.verdict;
            tower.footingMotion = drift.motion;

            TransmissionTowerAttachments a = TransmissionTowerAttachments.For(steel, s.phaseCount, type, isTerminal);
            tower.conductorAttachWorldY = lowest + a.conductorAttachY;
            tower.shieldWireWorldY = lowest + a.shieldWireY;
            tower.insulatorLengthMetres = a.insulatorLengthMetres;
            return tower;
        }

        /// <summary> The tower type at station k on a leg: an intermediate station is always a suspension tower; the
        /// start corner takes the run terminal as a strain tower, or an angle tower when the route turns there beyond
        /// the threshold, otherwise a suspension tower carrying a straight-through corner. </summary>
        private static TowerType TowerTypeAt(List<Vector3> route, int leg, int k, bool isTerminal,
            float angleThresholdDeg)
        {
            if (k != 0) { return TowerType.Suspension; }
            if (isTerminal) { return TowerType.Angle; }
            double turn = TurnAngleAt(route, leg);
            return TransmissionLine.ChooseTowerType(turn, angleThresholdDeg);
        }

        /// <summary> The turn angle at corner <paramref name="corner"/>, degrees, between the incoming and outgoing
        /// legs. Zero at the run's two ends (no turn to measure). Positive magnitude only; the sign of a turn does not
        /// change which tower type it wants. </summary>
        private static double TurnAngleAt(List<Vector3> route, int corner)
        {
            if (corner <= 0 || corner >= route.Count - 1) { return 0.0; }
            Vector3 a = route[corner] - route[corner - 1];
            Vector3 b = route[corner + 1] - route[corner];
            Vector2 inDir = new Vector2(a.x, a.z);
            Vector2 outDir = new Vector2(b.x, b.z);
            if (inDir.sqrMagnitude < 1e-6f || outDir.sqrMagnitude < 1e-6f) { return 0.0; }
            float dot = Vector2.Dot(inDir.normalized, outDir.normalized);
            if (dot > 1f) { dot = 1f; }
            if (dot < -1f) { dot = -1f; }
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        /// <summary> The yaw the cross-arm faces at a tower: perpendicular to the line direction, and at a corner the
        /// bisector of the two legs so the arm splits the angle. k &lt; 0 asks for the run's final corner (incoming leg
        /// only). </summary>
        private static float YawAt(List<Vector3> route, int leg, int k, int spanCount)
        {
            Vector2 dir;
            if (k < 0)
            {
                Vector3 a = route[route.Count - 1] - route[route.Count - 2];
                dir = new Vector2(a.x, a.z);
            }
            else if (k == 0 && leg > 0)
            {
                // A corner tower: bisect the incoming and outgoing legs.
                Vector3 inV = route[leg] - route[leg - 1];
                Vector3 outV = route[leg + 1] - route[leg];
                Vector2 inDir = new Vector2(inV.x, inV.z);
                Vector2 outDir = new Vector2(outV.x, outV.z);
                if (inDir.sqrMagnitude > 1e-6f) { inDir = inDir.normalized; }
                if (outDir.sqrMagnitude > 1e-6f) { outDir = outDir.normalized; }
                dir = inDir + outDir;
                if (dir.sqrMagnitude < 1e-6f) { dir = outDir; }
            }
            else
            {
                Vector3 a = route[leg + 1] - route[leg];
                dir = new Vector2(a.x, a.z);
            }
            if (dir.sqrMagnitude < 1e-6f) { return 0f; }
            // Yaw of the line direction about world Y; the arm is drawn perpendicular to it by the builder.
            return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ spans

        private static void BuildSpans(TransmissionLineRunPlan plan, TransmissionLineRunSettings s,
            TransmissionLineClass elec, TransmissionTowerClass steel, ITerrainSurfaceReader ground)
        {
            plan.worstClearanceMetres = double.PositiveInfinity;
            plan.worstSpanIndex = -1;
            plan.clears = true;

            for (int i = 0; i < plan.towers.Count - 1; i++)
            {
                TransmissionRunTower from = plan.towers[i];
                TransmissionRunTower to = plan.towers[i + 1];
                Vector3 flatFrom = new Vector3(from.basePoint.x, 0f, from.basePoint.z);
                Vector3 flatTo = new Vector3(to.basePoint.x, 0f, to.basePoint.z);
                float spanMetres = Vector3.Distance(flatFrom, flatTo);

                TransmissionRunSpan span = new TransmissionRunSpan();
                span.fromTowerIndex = i;
                span.toTowerIndex = i + 1;
                span.spanMetres = spanMetres;

                // The cable hangs between the two towers' PHASE attachment heights -- the lowest conductor, the one the
                // statutory clearance is measured from. Solved once and reused across phases offset along the arm.
                span.catenary = TransmissionLine.Solve(spanMetres, from.conductorAttachWorldY,
                    to.conductorAttachWorldY, elec.sagRatio);

                double[] groundHeights = SampleGround(flatFrom, flatTo, ground);
                span.clearance = TransmissionLine.SpanClearance(span.catenary, groundHeights,
                    elec.minGroundClearanceMetres);

                if (span.clearance.worstClearanceMetres < plan.worstClearanceMetres)
                {
                    plan.worstClearanceMetres = span.clearance.worstClearanceMetres;
                    plan.worstSpanIndex = i;
                }
                if (!span.clearance.clears) { plan.clears = false; }
                plan.spans.Add(span);
            }

            if (!plan.clears)
            {
                Refuse(plan, "a span fouls the statutory ground clearance ("
                    + plan.requiredClearanceMetres.ToString("F2", CultureInfo.InvariantCulture)
                    + " m) at span " + plan.worstSpanIndex.ToString(CultureInfo.InvariantCulture)
                    + "; raise the towers, shorten the span, or reroute -- the run is not flattened for you");
            }
        }

        /// <summary> Ground heights across a span at evenly spaced stations, ends included, for the clearance test.
        /// An unreadable station (NaN/Inf from an unloaded chunk) is reported as a very LOW ground so it never masks a
        /// real foul as a false clearance -- a station that cannot be read is not a station that is known clear.
        /// </summary>
        private static double[] SampleGround(Vector3 flatFrom, Vector3 flatTo, ITerrainSurfaceReader ground)
        {
            int n = GroundSamplesPerSpan;
            double[] heights = new double[n];
            int last = n - 1;
            for (int i = 0; i < n; i++)
            {
                float t = last == 0 ? 0f : (float)i / last;
                Vector3 at = Vector3.Lerp(flatFrom, flatTo, t);
                float g = ground.SampleHeight(at.x, at.z);
                heights[i] = (float.IsNaN(g) || float.IsInfinity(g)) ? double.NegativeInfinity : g;
            }
            return heights;
        }

        // ------------------------------------------------------------------ settlements

        /// <summary> Report every leg that enters a settlement rectangle. The spec forbids a transmission run being
        /// inside a settlement, so an author who drew a corner through a town has to be told which leg and which
        /// settlement, before any steel is placed. Segment-vs-rectangle in world XZ; a leg whose either end is inside
        /// or which crosses an edge counts. </summary>
        private static void ReportSettlementCrossings(TransmissionLineRunPlan plan, List<Vector3> route,
            IList<Rect> settlementBoundsWorldXZ)
        {
            if (settlementBoundsWorldXZ == null || settlementBoundsWorldXZ.Count == 0) { return; }
            for (int leg = 0; leg < route.Count - 1; leg++)
            {
                Vector2 p0 = new Vector2(route[leg].x, route[leg].z);
                Vector2 p1 = new Vector2(route[leg + 1].x, route[leg + 1].z);
                for (int r = 0; r < settlementBoundsWorldXZ.Count; r++)
                {
                    Rect rect = settlementBoundsWorldXZ[r];
                    if (rect.width <= 0f || rect.height <= 0f) { continue; }
                    if (SegmentCrossesRect(p0, p1, rect))
                    {
                        Refuse(plan, "leg " + leg.ToString(CultureInfo.InvariantCulture)
                            + " crosses a settlement footprint (" + rect.ToString()
                            + "); a transmission run may not pass through a settlement -- reroute the corner");
                    }
                }
            }
        }

        /// <summary> Whether segment p0->p1 touches axis-aligned rect, in XZ. Liang-Barsky slab clipping: cheap, no
        /// allocation, and true when either endpoint is inside as well as when the segment merely crosses an edge.
        /// </summary>
        private static bool SegmentCrossesRect(Vector2 p0, Vector2 p1, Rect rect)
        {
            float dx = p1.x - p0.x;
            float dy = p1.y - p0.y;
            float t0 = 0f;
            float t1 = 1f;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { p0.x - rect.xMin, rect.xMax - p0.x, p0.y - rect.yMin, rect.yMax - p0.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-9f)
                {
                    if (q[i] < 0f) { return false; } // parallel and outside this slab
                    continue;
                }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) { return false; } if (t > t0) { t0 = t; } }
                else { if (t < t0) { return false; } if (t < t1) { t1 = t; } }
            }
            return t0 <= t1;
        }

        private static TransmissionLineRunPlan Refuse(TransmissionLineRunPlan plan, string why)
        {
            plan.refusals.Add(why);
            plan.clears = false;
            return plan;
        }
    }


    /// <summary> D-14's scene presence: a hand-placed child that holds the authored route and steel, gathers the
    /// scene answers only a scene can give (the terrain reader and the settlement rectangles), and asks the pure
    /// planner. It computes and refuses. It NEVER mutates the scene, never touches an asset, and never draws -- the
    /// editor builder places the steel and the conductors, the inspector edits this, and C-06 keeps the run baked
    /// rather than regenerated at play. </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TransmissionLineRun : MonoBehaviour
    {
        public TransmissionLineRunSettings settings = new TransmissionLineRunSettings();

        /// <summary> Plan this run against a supplied ground reader, collecting the scene's settlement rectangles
        /// itself. The reader is passed in rather than resolved here so the identical plan can be made in standalone
        /// (tile) and connected (provider) mode, the same contract the pole line keeps. </summary>
        public TransmissionLineRunPlan Plan(ITerrainSurfaceReader ground, out string note)
        {
            List<Rect> bounds = CollectSettlementBounds();
            TransmissionLineRunPlan plan = TransmissionLineRunPlanner.Plan(settings, ground, bounds);
            note = plan.Summary();
            return plan;
        }

        /// <summary> The world XZ rectangle of every settlement in the scene, from each <see cref="CityAuthoring"/>'s
        /// own region resolver, so the planner can refuse a run that crosses one. A settlement whose region is cleared
        /// (zero/negative) is skipped rather than treated as a point, because its own resolver already refuses it and a
        /// zero rect would never be crossed anyway. </summary>
        private List<Rect> CollectSettlementBounds()
        {
            List<Rect> bounds = new List<Rect>();
#if UNITY_2023_1_OR_NEWER
            CityAuthoring[] cities = UnityEngine.Object.FindObjectsByType<CityAuthoring>();
#else
            CityAuthoring[] cities = UnityEngine.Object.FindObjectsOfType<CityAuthoring>();
#endif
            if (cities == null) { return bounds; }
            for (int i = 0; i < cities.Length; i++)
            {
                CityAuthoring city = cities[i];
                if (city == null) { continue; }
                Rect world;
                string reason;
                if (city.TryRegionWorldXZ(out world, out reason)) { bounds.Add(world); }
            }
            return bounds;
        }
    }
}

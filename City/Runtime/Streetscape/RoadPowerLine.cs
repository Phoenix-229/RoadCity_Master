#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using RoadCityMaster;
using RoadCityMaster.EdgeObjects;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which of NESC Table 232-1's two columns a run answers to. This is the "class popup" B-27 asks the
    /// road pole-line child to carry, and it is deliberately NOT a decorative label: the choice picks the required
    /// vertical clearance the spans are judged against, 5.6 m for an open supply conductor of 750 V to 22 kV and
    /// 4.7 m for 0 to 750 V. A run mis-set to Secondary is a run that will accept spans a primary must refuse, so
    /// the popup is the one field on the component that can change a pass into a fail without moving a pole.
    /// </summary>
    public enum DistributionCircuitClass
    {
        Secondary = 0,
        Primary = 1
    }


    /// <summary> The author's answers for one hand-placed pole line along one road. Everything here is a scene
    /// answer -- where the run starts, which side it takes, how often a transformer appears -- except the two
    /// nested classes, which are the parameter blocks the maths already lives in: the electrical figures stay on
    /// <see cref="DistributionPoleClass"/> (span length, separations, NESC clearances, conductor and tension) and
    /// the pole's dimensions stay on <see cref="PoleGeometryClass"/>. Span length is NOT repeated here for that
    /// reason; it is the electrical block's field, drawn in this component's inspector, with one home.
    ///
    /// <see cref="offsetBeyondEdgeMetres"/> is the distance from the edge of the carriageway to the pole's centre.
    /// 1.0 m is the ordinary urban figure -- a pole set just behind the kerb line. It is a field rather than a
    /// constant because a rural frontage is not the same case: AASHTO's Roadside Design Guide clear zone for a
    /// two-lane road at 80 km/h and low volume is around 3 to 4.5 m, and an author placing a run along a highway
    /// should be able to say so. The tool does not choose for them; it plants the pole where they say and refuses
    /// the span if the wire ends up too low. </summary>
    [Serializable]
    public sealed class RoadPowerLineSettings
    {
        public bool enabled = true;
        public DistributionCircuitClass circuit = DistributionCircuitClass.Primary;
        public FrontageSide side = FrontageSide.Right;
        public float offsetBeyondEdgeMetres = 1.0f;
        public float startStationMetres = 0f;
        public float endStationMetres = -1f;
        public int transformerEveryNPoles = 8;
        public float guyTurnDegrees = 15f;
        public int phaseCount = 3;
        public bool secondaryRack = true;

        /// <summary> Whether a communication strand shares these poles. True on a street, where joint use with a
        /// telephone or cable plant is the norm, and false on a rural primary run. How far below the supply it hangs
        /// is NESC Rule 238's separation, carried by
        /// <see cref="DistributionPoleClass.communicationSeparationMetres"/>. </summary>
        public bool communicationStrung = true;

        public DistributionPoleClass electrical = new DistributionPoleClass();
        public PoleGeometryClass shape = new PoleGeometryClass();

        /// <summary> The materials the built run wears. These are the first <c>Material</c> fields in the city half of
        /// the tool, and they are here because a procedural pole has no prefab to carry its own. An
        /// <c>EdgeObjectMaker</c> run takes its look from the prefab the author picked; a run the tool draws itself has
        /// to take it from somewhere, so it is ASKED FOR, in the same shape the vendored makers ask in
        /// (<c>EdgeObjectMaker.edgeMaterial1</c>, <c>Road.RoadMaterial1</c>), and never invented. Nothing in this tool
        /// constructs a <c>Material</c>, and <c>CitySurfaceTypeGate.CaseMaterialsAuthoredNotGenerated</c> is the
        /// assertion that keeps it that way.
        ///
        /// <c>SurfaceTypeClass.materialSetId</c> is NOT the mechanism here. That string names a surface type so the
        /// tool can tell one surface from another -- no code resolves it to a material -- and adding a resolver to
        /// hang three poles' worth of timber on would be a second answer to a question the prefab already answers.
        ///
        /// Two slots per renderer, always, because every mesh the pole line builds has two submeshes: the timber and
        /// the hardware. A slot left empty is not a refusal. The run is still geometrically correct and still legal;
        /// the author is simply told which slot is bare in the sentence the build returns, because a pole line that
        /// renders magenta is a missing reference and not a mistake in the plan. </summary>
        public Material timberMaterial = null;
        public Material hardwareMaterial = null;
        public Material conductorMaterial = null;

        public static RoadPowerLineSettings Default() { return new RoadPowerLineSettings(); }
    }


    /// <summary> One resolved pole. The butt point is where the pole's bottom end sits, which is BELOW the ground
    /// it is seated on by the ANSI O5.1 setting depth -- that embedment is the reason a pole does not look like a
    /// stick balanced on a hillside. The three world heights are the attachment levels the conductors are strung
    /// from, taken from the same <see cref="PoleAttachments"/> the mesh was drawn with, so a wire can
    /// never land at a height no fitting exists at. </summary>
    [Serializable]
    public struct RoadPowerLinePole
    {
        public float stationMetres;
        public Vector3 buttPoint;
        public float groundY;
        public float yawDegrees;
        public PoleFittings fittings;
        public int guyCount;
        public float armWorldY;
        public float neutralWorldY;
        public float secondaryWorldY;
        public float communicationWorldY;
        public bool nudged;
        public float nudgedFromStationMetres;
    }


    /// <summary> One span between two consecutive poles, with the catenary that hangs across it and the clearance
    /// verdict over the ground beneath. All phases of a span share one curve: they are strung at the same tension
    /// over the same distance and differ only in their lateral offset on the cross-arm, so solving once and
    /// offsetting is the same answer as solving three times, and the plan carries the curve rather than the
    /// wires. </summary>
    [Serializable]
    public struct RoadPowerLineSpan
    {
        public int fromPoleIndex;
        public int toPoleIndex;
        public float spanMetres;
        public Catenary catenary;
        public TransmissionLine.ClearanceResult clearance;
    }


    /// <summary> What the planner worked out, including what it would not do. A plan that refuses is still a plan:
    /// it reports every pole it could place and names the spans whose clearance failed with the shortfall, because
    /// "the line is too low here by 40 cm" is a sentence an author can act on and "build failed" is not. </summary>
    public sealed class RoadPowerLinePlan
    {
        public List<RoadPowerLinePole> poles = new List<RoadPowerLinePole>();
        public List<RoadPowerLineSpan> spans = new List<RoadPowerLineSpan>();
        public List<string> refusals = new List<string>();
        public float runLengthMetres;
        public double requiredClearanceMetres;
        public double worstClearanceMetres = double.PositiveInfinity;
        public int worstSpanIndex = -1;
        public int droppedPoleCount;
        public bool clears = true;

        /// <summary> True when the plan can be built as it stands: at least one pole, and no span refused.
        /// </summary>
        public bool Buildable { get { return poles.Count > 0 && refusals.Count == 0; } }

        /// <summary> One line an inspector or a gate report can print without formatting anything itself.
        /// </summary>
        public string Summary()
        {
            string worst = worstSpanIndex >= 0
                ? worstClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m at span "
                  + worstSpanIndex.ToString(CultureInfo.InvariantCulture)
                : "no span";
            return poles.Count.ToString(CultureInfo.InvariantCulture) + " poles, "
                   + spans.Count.ToString(CultureInfo.InvariantCulture) + " spans over "
                   + runLengthMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m; tightest clearance "
                   + worst + " against " + requiredClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture)
                   + " m required; " + droppedPoleCount.ToString(CultureInfo.InvariantCulture)
                   + " dropped for furniture; " + (clears ? "clears" : "REFUSED");
        }
    }


    /// <summary> B-27's D-1 in its provable form: given a road's centreline as world points, the author's answers,
    /// the furniture already on the frontage and a reader for the ground, work out where every pole stands, what
    /// fittings it carries, how the wire hangs between them, and which spans must be refused.
    ///
    /// It is a pure static function of its arguments -- no component, no scene, no clock, no random (C-06) -- for
    /// two reasons. The first is that a gate can then test a pole line without building a road: hand it a straight
    /// or curved polyline and an <see cref="ITerrainSurfaceReader"/> stub and the whole chain is exercised. The
    /// second is the doctrine the settlement components already follow: the component computes and refuses, and
    /// something else does the scene work.
    ///
    /// The order of operations is the real one a line crew's drawing follows, and each step is delegated rather
    /// than re-derived: stations come from <see cref="DistributionPoleRun.PlanPoleStations"/> (RM-13's quantiser
    /// plus RM-24's furniture nudge), attachment heights come from <see cref="PoleAttachments"/> so a
    /// conductor lands on a fitting that exists in the mesh, and the span itself is solved and judged by
    /// <see cref="DistributionPoleRun.SolveSpan"/> and <see cref="DistributionPoleRun.SpanClearance"/>, which are
    /// the one catenary solver at distribution numbers. </summary>
    public static class RoadPowerLinePlanner
    {
        /// <summary> How many ground samples a span's clearance is measured at. Nine over a nominal 40 m span is a
        /// reading every 5 m, finer than any terrain the tool writes, and it always includes both attachments and
        /// the mid-span point where a level span's sag is deepest. </summary>
        public const int GroundSamplesPerSpan = 9;

        /// <summary> Resolve one run. <paramref name="centreline"/> is the road's own curve as world points in
        /// order, <paramref name="roadHalfWidthMetres"/> the distance from the centreline to the edge of the
        /// carriageway, and stations are measured along the polyline from its first point. </summary>
        public static RoadPowerLinePlan Plan(RoadPowerLineSettings settings, IList<Vector3> centreline,
            float roadHalfWidthMetres, IList<Vector2> intersectionExclusions, IList<float> existingFurniture,
            ITerrainSurfaceReader ground)
        {
            RoadPowerLinePlan plan = new RoadPowerLinePlan();
            RoadPowerLineSettings s = settings != null ? settings : RoadPowerLineSettings.Default();
            DistributionPoleClass e = s.electrical != null ? s.electrical : DistributionPoleClass.Default();
            PoleGeometryClass shape = s.shape != null ? s.shape : PoleGeometryClass.Default();

            bool overRoadway = s.circuit == DistributionCircuitClass.Primary;
            plan.requiredClearanceMetres = overRoadway
                ? e.roadCrossingClearanceMetres : e.secondaryCrossingClearanceMetres;

            if (centreline == null || centreline.Count < 2)
            {
                return Refuse(plan, "this run has no centreline to follow: a pole line needs at least two points "
                                    + "of road");
            }
            if (ground == null)
            {
                return Refuse(plan, "the ground could not be read, so no pole can be seated and no clearance "
                                    + "measured; run this with a terrain the tool can see");
            }
            if (s.phaseCount < 1)
            {
                return Refuse(plan, "a pole line with no phase carries nothing: set the phase count to 1, 2 or 3");
            }

            float[] cumulative = Cumulative(centreline);
            plan.runLengthMetres = cumulative[cumulative.Length - 1];
            if (plan.runLengthMetres <= 0f)
            {
                return Refuse(plan, "the centreline given has no length, so there is nowhere to put a pole");
            }

            List<float> candidates = RoadCityMaster.CrossSection.FurnitureRunPlanner.PlanStations(
                plan.runLengthMetres, e.spanLengthMetres, intersectionExclusions);
            List<float> stations = DistributionPoleRun.PlanPoleStations(
                plan.runLengthMetres, e, intersectionExclusions, existingFurniture);
            plan.droppedPoleCount = candidates.Count - stations.Count;
            if (plan.droppedPoleCount < 0) { plan.droppedPoleCount = 0; }

            BuildPoles(plan, stations, candidates, centreline, cumulative, roadHalfWidthMetres, s, e, shape, ground);
            BuildSpans(plan, s, e, ground, overRoadway);
            return plan;
        }

        /// <summary> Plant every surviving station: offset it to the frontage, seat it on the ground with its ANSI
        /// setting depth buried, and read its attachment heights off the same key the mesh will be built from.
        /// </summary>
        private static void BuildPoles(RoadPowerLinePlan plan, IList<float> stations, IList<float> candidates,
            IList<Vector3> centreline, float[] cumulative, float halfWidth, RoadPowerLineSettings s,
            DistributionPoleClass e, PoleGeometryClass shape, ITerrainSurfaceReader ground)
        {
            float lateral = halfWidth + s.offsetBeyondEdgeMetres;
            float sign = s.side == FrontageSide.Right ? 1f : -1f;

            for (int i = 0; i < stations.Count; i++)
            {
                Vector3 onRoad;
                Vector3 tangent;
                PointAt(centreline, cumulative, stations[i], out onRoad, out tangent);

                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude < 1e-8f) { right = Vector3.right; }
                else { right.Normalize(); }

                Vector3 at = onRoad + right * (sign * lateral);
                float groundY = ground.SampleHeight(at.x, at.z);

                RoadPowerLinePole pole = new RoadPowerLinePole();
                pole.stationMetres = stations[i];
                pole.groundY = groundY;
                pole.yawDegrees = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
                pole.fittings = FittingsFor(i, s);
                pole.guyCount = GuyCountFor(i, stations, centreline, cumulative, s.guyTurnDegrees);
                pole.nudged = WasNudged(stations[i], candidates, out pole.nudgedFromStationMetres);

                PoleMeshKey key = PoleMeshKey.For(shape, e, pole.fittings, s.phaseCount, pole.guyCount);
                PoleAttachments a = PoleAttachments.For(key, shape);

                // The factory works in butt-local space with the origin at the pole's bottom end, so the lift that
                // maps local to world is the ground height minus the depth the pole is set to. That is what buries
                // the embedment instead of standing the pole on the surface.
                float lift = groundY - a.groundY;
                pole.buttPoint = new Vector3(at.x, lift, at.z);
                pole.armWorldY = lift + a.armY;
                pole.neutralWorldY = lift + a.neutralY;
                pole.secondaryWorldY = lift + a.secondaryY;
                pole.communicationWorldY = lift + a.communicationY;
                plan.poles.Add(pole);
            }
        }

        /// <summary> Hang the wire between each consecutive pair and judge it. The span length is the HORIZONTAL run
        /// between the two butt points, which is the length a conductor is strung and sagged over; the stations
        /// that spaced the poles are arc length along the road, and the two differ by a fraction of a percent at
        /// any grade a road is built to. Every span uses the arm attachment, because that is where the phases are,
        /// and a run whose tightest span fails is refused with the shortfall named rather than silently built.
        /// The ground beneath a span is sampled once and judged twice: the phases against their own row of the table,
        /// and any communication strand against the lower one (<see cref="CheckCommunication"/>).
        /// </summary>
        private static void BuildSpans(RoadPowerLinePlan plan, RoadPowerLineSettings s, DistributionPoleClass e,
            ITerrainSurfaceReader ground, bool overRoadway)
        {
            string column = overRoadway ? "750 V to 22 kV over a road" : "0 to 750 V";

            for (int i = 0; i + 1 < plan.poles.Count; i++)
            {
                RoadPowerLinePole a = plan.poles[i];
                RoadPowerLinePole b = plan.poles[i + 1];
                float dx = b.buttPoint.x - a.buttPoint.x;
                float dz = b.buttPoint.z - a.buttPoint.z;
                double span = Mathf.Sqrt(dx * dx + dz * dz);
                if (span <= 0.0) { continue; }

                RoadPowerLineSpan record = new RoadPowerLineSpan();
                record.fromPoleIndex = i;
                record.toPoleIndex = i + 1;
                record.spanMetres = (float)span;
                record.catenary = DistributionPoleRun.SolveSpan(span, a.armWorldY, b.armWorldY, e);
                List<double> groundLine = SampleGroundAlong(ground, a.buttPoint, b.buttPoint);
                record.clearance = DistributionPoleRun.SpanClearance(span, a.armWorldY, b.armWorldY, e,
                    groundLine, overRoadway);
                plan.spans.Add(record);

                if (record.clearance.worstClearanceMetres < plan.worstClearanceMetres)
                {
                    plan.worstClearanceMetres = record.clearance.worstClearanceMetres;
                    plan.worstSpanIndex = plan.spans.Count - 1;
                }
                if (!record.clearance.clears)
                {
                    plan.clears = false;
                    plan.refusals.Add("the span from station "
                        + a.stationMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m to "
                        + b.stationMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m clears the ground by "
                        + record.clearance.worstClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture)
                        + " m against " + plan.requiredClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture)
                        + " m required (NESC Table 232-1, " + column + "): shorten the span, use a longer pole, or "
                        + "raise the ground under it");
                }

                CheckCommunication(plan, a, b, span, e, groundLine);
            }
        }

        /// <summary> The communication strand's own clearance, refused in its own sentence.
        ///
        /// A shared pole hangs two different things over the same ground, and NESC Table 232-1 does not judge them by
        /// one figure: the supply conductors above answer to the row for their voltage, the strand below answers to
        /// the 4.7 m row. That is the same 4.7 m a 0 to 750 V supply conductor takes, which is why
        /// <see cref="DistributionPoleClass.secondaryCrossingClearanceMetres"/> serves both and there is no third
        /// number in the class for it. It is also why the roadway flag the supply check takes is not forwarded here:
        /// the strand's column is the low one wherever the run goes.
        ///
        /// The strand does NOT feed <see cref="RoadPowerLinePlan.worstClearanceMetres"/>. That figure is shown beside
        /// <see cref="RoadPowerLinePlan.requiredClearanceMetres"/>, which is the SUPPLY requirement, so a strand
        /// measurement placed there would read as a failure on a run whose phases are legal. It does set
        /// <see cref="RoadPowerLinePlan.clears"/>, because a strand hung too low is unbuildable whatever the phases
        /// above it do. </summary>
        private static void CheckCommunication(RoadPowerLinePlan plan, RoadPowerLinePole a, RoadPowerLinePole b,
            double spanMetres, DistributionPoleClass e, IList<double> groundHeights)
        {
            if ((a.fittings & PoleFittings.Communication) == 0) { return; }
            if ((b.fittings & PoleFittings.Communication) == 0) { return; }

            TransmissionLine.ClearanceResult strand = DistributionPoleRun.SpanClearance(spanMetres,
                a.communicationWorldY, b.communicationWorldY, e, groundHeights, false);
            if (strand.clears) { return; }

            plan.clears = false;
            plan.refusals.Add("the communication strand from station "
                + a.stationMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m to "
                + b.stationMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m clears the ground by "
                + strand.worstClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m against "
                + e.secondaryCrossingClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture)
                + " m required (NESC Table 232-1, 0 to 750 V, which is the row a communication conductor takes): "
                + "shorten the span, use a longer pole, or turn the strand off for this frontage");
        }

        /// <summary> How many conductors a secondary rack carries. A 120/240 V single-phase three-wire secondary is two
        /// hots and a neutral, and the neutral is the one already strung on its own rack above, so the rack below
        /// carries two. </summary>
        public const int SecondaryConductorsPerRack = 2;

        /// <summary> Every wire the run hangs, as world attachments and the curve between them. Pure, so D-16 asserts
        /// the count and the sag with no scene: a wire is the part of a pole line an author sees first and the part a
        /// gate could not see at all while it lived inside a mesh builder.
        ///
        /// A conductor needs TWO ends, so a level is strung across a span only when BOTH of its poles carry that
        /// fitting. That is what stops a secondary from being left hanging at a pole where the rack was dropped.
        ///
        /// The phases share the span's own catenary -- they are one conductor at one tension over one length, differing
        /// only in where along the arm they sit -- while the neutral and the secondary are solved again at their own
        /// attachment heights. Solving them again is not a second answer: it is the SAME solver at the same sag ratio,
        /// and a neutral 0.61 m below the arm hangs 0.61 m lower, not on the phases' curve.
        ///
        /// The communication strand is strung at the SAME sag ratio as the supply above it, which is not what a
        /// telephone plant does -- a messenger is pulled slacker than a phase -- and is deliberate. Rule 238's
        /// separation is measured at the attachment, and equal sag holds that separation constant along the whole
        /// span instead of letting it close mid-span; a slacker strand only opens the gap. This is the tight case,
        /// and inventing a messenger tension to model the loose one would be a second answer to a solved question.
        ///
        /// The order is fixed for C-06: spans in ascending order, and within a span the phases across the arm, then the
        /// neutral, then the secondary rack, then the communication strand. </summary>
        public static List<ConductorSpan> StringConductors(RoadPowerLinePlan plan, RoadPowerLineSettings settings)
        {
            List<ConductorSpan> wires = new List<ConductorSpan>();
            if (plan == null || settings == null) { return wires; }
            DistributionPoleClass e = settings.electrical != null
                ? settings.electrical : DistributionPoleClass.Default();
            PoleGeometryClass shape = settings.shape != null ? settings.shape : PoleGeometryClass.Default();

            for (int i = 0; i < plan.spans.Count; i++)
            {
                RoadPowerLineSpan span = plan.spans[i];
                if (!Indexed(plan, span.fromPoleIndex) || !Indexed(plan, span.toPoleIndex)) { continue; }
                RoadPowerLinePole a = plan.poles[span.fromPoleIndex];
                RoadPowerLinePole b = plan.poles[span.toPoleIndex];
                Vector3 rightA = Quaternion.Euler(0f, a.yawDegrees, 0f) * Vector3.right;
                Vector3 rightB = Quaternion.Euler(0f, b.yawDegrees, 0f) * Vector3.right;

                float[] offsetsA = PoleAttachments.For(
                    PoleMeshKey.For(shape, e, a.fittings, settings.phaseCount, a.guyCount), shape).phaseOffsets;
                float[] offsetsB = PoleAttachments.For(
                    PoleMeshKey.For(shape, e, b.fittings, settings.phaseCount, b.guyCount), shape).phaseOffsets;
                int phases = Mathf.Min(offsetsA.Length, offsetsB.Length);
                for (int p = 0; p < phases; p++)
                {
                    wires.Add(new ConductorSpan(
                        At(a.buttPoint, rightA, offsetsA[p], a.armWorldY),
                        At(b.buttPoint, rightB, offsetsB[p], b.armWorldY), span.catenary));
                }

                if (Both(a, b, PoleFittings.NeutralRack))
                {
                    wires.Add(new ConductorSpan(
                        At(a.buttPoint, rightA, 0f, a.neutralWorldY),
                        At(b.buttPoint, rightB, 0f, b.neutralWorldY),
                        DistributionPoleRun.SolveSpan(span.spanMetres, a.neutralWorldY, b.neutralWorldY, e)));
                }

                if (Both(a, b, PoleFittings.SecondaryRack))
                {
                    Catenary curve = DistributionPoleRun.SolveSpan(
                        span.spanMetres, a.secondaryWorldY, b.secondaryWorldY, e);
                    for (int r = 0; r < SecondaryConductorsPerRack; r++)
                    {
                        float offset = RackOffset(r, SecondaryConductorsPerRack, shape.rackWidthMetres);
                        wires.Add(new ConductorSpan(
                            At(a.buttPoint, rightA, offset, a.secondaryWorldY),
                            At(b.buttPoint, rightB, offset, b.secondaryWorldY), curve));
                    }
                }

                if (Both(a, b, PoleFittings.Communication))
                {
                    wires.Add(new ConductorSpan(
                        At(a.buttPoint, rightA, 0f, a.communicationWorldY),
                        At(b.buttPoint, rightB, 0f, b.communicationWorldY),
                        DistributionPoleRun.SolveSpan(span.spanMetres, a.communicationWorldY,
                            b.communicationWorldY, e)));
                }
            }
            return wires;
        }

        /// <summary> An attachment point: the pole's own butt position carried sideways along its arm and lifted to the
        /// level the wire is held at. The butt point's own height is not used -- it is underground. </summary>
        private static Vector3 At(Vector3 buttPoint, Vector3 right, float offset, float worldY)
        {
            return new Vector3(buttPoint.x + right.x * offset, worldY, buttPoint.z + right.z * offset);
        }

        /// <summary> Whether both ends of a span carry a fitting, which is what it takes to string one wire. </summary>
        private static bool Both(RoadPowerLinePole a, RoadPowerLinePole b, PoleFittings fitting)
        {
            return (a.fittings & fitting) != 0 && (b.fittings & fitting) != 0;
        }

        /// <summary> Whether a span's pole index points at a pole that is actually in the plan. A plan is built here
        /// and its spans always index its own poles, so this never fires on the tool's own output; it is here because
        /// <see cref="StringConductors"/> is public and a gate hands it plans it made itself, and a wire read off the
        /// end of the pole list would be an exception instead of an assertion failure. </summary>
        private static bool Indexed(RoadPowerLinePlan plan, int index)
        {
            return index >= 0 && index < plan.poles.Count;
        }

        /// <summary> Where one of the rack's conductors sits across it: evenly spread and centred on the pole, so two
        /// on a 0.30 m rack straddle it at a quarter of its width either side rather than touching its ends.
        /// </summary>
        private static float RackOffset(int index, int count, float rackWidthMetres)
        {
            if (count <= 1) { return 0f; }
            float width = rackWidthMetres > 0f ? rackWidthMetres : 0.30f;
            return (index - (count - 1) * 0.5f) * (width / count);
        }

        /// <summary> Which fittings the pole at this index carries. Every pole on a primary run takes a cross-arm,
        /// a neutral and a ground wire. A transformer never arrives alone: its primary is fused through a cut-out
        /// and a surge arrester sits beside it, which is why those three flags are set together rather than being
        /// three independent author choices.
        ///
        /// Communication is not a distribution fitting at all -- it is another utility's plant on the same pole -- so
        /// it is set only where the author says this frontage carries one. </summary>
        private static PoleFittings FittingsFor(int index, RoadPowerLineSettings s)
        {
            PoleFittings f = PoleFittings.CrossArm | PoleFittings.NeutralRack | PoleFittings.GroundWire;
            if (s.secondaryRack) { f |= PoleFittings.SecondaryRack; }
            if (s.communicationStrung) { f |= PoleFittings.Communication; }
            int every = s.transformerEveryNPoles;
            if (every > 0 && index % every == 0)
            {
                f |= PoleFittings.Transformer | PoleFittings.Cutout | PoleFittings.Arrester;
            }
            return f;
        }

        /// <summary> How many down-guys this pole needs. A terminal pole at either end of the run takes one, set in
        /// line with the conductors to oppose their unbalanced pull -- a dead-end with no guy is the one arrangement
        /// that actually pulls a pole over. An intermediate pole is in balance and needs none, unless the line turns
        /// through more than the author's threshold there, in which case the resultant pull needs a guy on the
        /// bisector. </summary>
        private static int GuyCountFor(int index, IList<float> stations, IList<Vector3> centreline,
            float[] cumulative, float turnDegrees)
        {
            if (index == 0 || index == stations.Count - 1) { return 1; }
            float threshold = turnDegrees > 0f ? turnDegrees : 15f;
            return DeflectionDegrees(centreline, cumulative, stations, index) > threshold ? 1 : 0;
        }

        /// <summary> The horizontal angle the line turns through at one pole, measured between the chord coming in
        /// and the chord going out. Taken on the centreline rather than on the offset pole positions because the
        /// offset is constant along the run and so does not change the angle. </summary>
        private static float DeflectionDegrees(IList<Vector3> centreline, float[] cumulative, IList<float> stations,
            int index)
        {
            Vector3 before;
            Vector3 at;
            Vector3 after;
            Vector3 ignored;
            PointAt(centreline, cumulative, stations[index - 1], out before, out ignored);
            PointAt(centreline, cumulative, stations[index], out at, out ignored);
            PointAt(centreline, cumulative, stations[index + 1], out after, out ignored);

            Vector3 incoming = at - before;
            Vector3 outgoing = after - at;
            incoming.y = 0f;
            outgoing.y = 0f;
            if (incoming.sqrMagnitude < 1e-8f || outgoing.sqrMagnitude < 1e-8f) { return 0f; }
            return Vector3.Angle(incoming, outgoing);
        }

        /// <summary> Whether this resolved station was moved off its quantised candidate by the furniture nudge, and
        /// which candidate it came from. Candidates are exact multiples of the span length, so the nearest one is
        /// unambiguous for any nudge smaller than half a span -- which every nudge is, the maximum being 5 m against
        /// a 40 m span. </summary>
        private static bool WasNudged(float station, IList<float> candidates, out float from)
        {
            from = station;
            if (candidates == null || candidates.Count == 0) { return false; }
            float best = candidates[0];
            float bestGap = Mathf.Abs(station - best);
            for (int i = 1; i < candidates.Count; i++)
            {
                float gap = Mathf.Abs(station - candidates[i]);
                if (gap < bestGap) { bestGap = gap; best = candidates[i]; }
            }
            from = best;
            return bestGap > 1e-3f;
        }

        /// <summary> The ground beneath one span, at <see cref="GroundSamplesPerSpan"/> evenly spaced readings that
        /// include both ends. </summary>
        private static List<double> SampleGroundAlong(ITerrainSurfaceReader ground, Vector3 from, Vector3 to)
        {
            List<double> heights = new List<double>(GroundSamplesPerSpan);
            int last = GroundSamplesPerSpan - 1;
            for (int i = 0; i <= last; i++)
            {
                float t = (float)i / last;
                heights.Add(ground.SampleHeight(Mathf.Lerp(from.x, to.x, t), Mathf.Lerp(from.z, to.z, t)));
            }
            return heights;
        }

        /// <summary> Arc length to each point of the centreline, so a station can be found without re-measuring the
        /// whole polyline per pole. Public because the caller that feeds this planner its furniture stations measures
        /// the same polyline, and two arc-length tables for one centreline is how a pole clears a bin by two metres in
        /// one file and by one in another. </summary>
        public static float[] Cumulative(IList<Vector3> points)
        {
            float[] cumulative = new float[points.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            return cumulative;
        }

        /// <summary> The station of a world point on this centreline: the arc length to the closest point of the
        /// polyline, not to the closest sample.
        ///
        /// The difference is the whole reason this exists. Furniture arrives as world positions -- an
        /// <c>EdgeObjectMaker</c> records where it put each bin and sign, not how far along the road it is -- and the
        /// planner's conflict test is a two-metre separation. Snapping to the nearest sample would quantise every
        /// furniture station to the five-metre sample spacing, so a bin could be reported two and a half metres from
        /// where it stands and a pole would be planted on it. Projecting onto the segment is exact to the polyline,
        /// which is the same curve the poles themselves are placed on, so both sides of the comparison have the same
        /// error and it cancels.
        ///
        /// Ties go to the earlier segment, which keeps the answer reproducible where a point sits equidistant from two
        /// segments at an inside corner (C-06). </summary>
        public static float StationOf(IList<Vector3> points, float[] cumulative, Vector3 worldPoint)
        {
            if (points == null || points.Count < 2 || cumulative == null) { return 0f; }

            float bestStation = 0f;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 along = b - a;
                float lengthSqr = along.sqrMagnitude;
                float t = lengthSqr > 1e-8f ? Vector3.Dot(worldPoint - a, along) / lengthSqr : 0f;
                if (t < 0f) { t = 0f; }
                else if (t > 1f) { t = 1f; }

                Vector3 onSegment = a + along * t;
                float sqr = (worldPoint - onSegment).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestStation = Mathf.Lerp(cumulative[i], cumulative[i + 1], t);
                }
            }
            return bestStation;
        }

        /// <summary> Where a station falls on the centreline, and the horizontal direction of travel there. The
        /// tangent is flattened because a pole stands plumb and its offset to the frontage is a horizontal distance:
        /// leaving the grade in would shorten the offset on a hill and lean the cross-arm with the slope. </summary>
        private static void PointAt(IList<Vector3> points, float[] cumulative, float station,
            out Vector3 position, out Vector3 tangent)
        {
            int last = points.Count - 1;
            float total = cumulative[last];
            float d = station < 0f ? 0f : (station > total ? total : station);

            int i = 0;
            while (i < last && cumulative[i + 1] < d) { i++; }
            if (i >= last) { i = last - 1; }

            float segment = cumulative[i + 1] - cumulative[i];
            float t = segment > 1e-6f ? (d - cumulative[i]) / segment : 0f;
            position = Vector3.Lerp(points[i], points[i + 1], t);

            tangent = points[i + 1] - points[i];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-8f) { tangent = Vector3.forward; }
            else { tangent.Normalize(); }
        }

        /// <summary> Record why nothing was planned and hand the empty plan back. A refusal is an answer. </summary>
        private static RoadPowerLinePlan Refuse(RoadPowerLinePlan plan, string why)
        {
            plan.refusals.Add(why);
            plan.clears = false;
            return plan;
        }
    }


    /// <summary> B-27's hand-placed pole-line child. Added under a road the author drew themselves, it says "this
    /// road carries a distribution line" and nothing more: it holds the answers, it can work out the whole run on
    /// demand, and it refuses when the run will not clear the ground. It does not build anything. Following the
    /// doctrine the settlement components already set -- this file computes and refuses; it never mutates a scene,
    /// never touches an asset, and never draws -- the meshes and objects are placed by
    /// <c>RoadPowerLineBuilder</c> on the editor side, which is also what keeps the run BAKED rather than a
    /// runtime cost.
    ///
    /// Stations in the plan are measured from the run's own start, not the road's, so moving
    /// <see cref="RoadPowerLineSettings.startStationMetres"/> slides the whole run rather than renumbering it.
    /// Bridge and tunnel spans of the parent road become exclusions: a distribution line crossing a bridge is
    /// carried on the structure and one crossing a tunnel is not there at all, so neither is a place to plant a
    /// pole. </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RoadPowerLine : MonoBehaviour
    {
        public RoadPowerLineSettings settings = new RoadPowerLineSettings();

        /// <summary> How finely the parent road's curve is sampled before the run is planned. 5 m is fine enough
        /// that a 40 m span's ends land within a tenth of a metre of the curve on any radius a road is built to,
        /// and coarse enough that a kilometre of road is two hundred points rather than ten thousand. </summary>
        public float centrelineSampleSpacingMetres = 5f;

        /// <summary> The road this run belongs to: the component's own object first, then its parents, so the child
        /// can sit under the road or under a holder beneath it. </summary>
        public Road ResolveRoad()
        {
            Road road = GetComponent<Road>();
            if (road != null) { return road; }
            return GetComponentInParent<Road>();
        }

        /// <summary> Work the whole run out. Returns null and a note when there is nothing to plan against -- no
        /// road, no built spline, or a run shorter than a single span.
        ///
        /// <paramref name="existingFurniture"/> distinguishes null from empty on purpose. NULL means "read the road's
        /// own frontage", and the stations are recovered from the road's edge objects
        /// (<see cref="FrontageStations"/>) against the centreline sampled here, so the curve is sampled once for both
        /// answers. An EMPTY list means "there is no furniture", which is what a gate hands over when it is testing the
        /// spacing alone. Without that distinction a caller who wanted the road's real furniture and a caller who
        /// wanted none would have to say the same thing. </summary>
        public RoadPowerLinePlan Plan(ITerrainSurfaceReader ground, IList<float> existingFurniture, out string note)
        {
            note = string.Empty;
            RoadPowerLineSettings s = settings != null ? settings : RoadPowerLineSettings.Default();
            if (!s.enabled)
            {
                note = "this pole line is switched off";
                return null;
            }

            Road road = ResolveRoad();
            if (road == null || road.spline == null)
            {
                note = "a pole line has to hang on a road: put this component on a road, or under one";
                return null;
            }

            List<Vector2> exclusions;
            float halfWidth;
            List<Vector3> centreline = SampleCentreline(road, out exclusions, out halfWidth, out note);
            if (centreline == null) { return null; }

            IList<float> furniture = existingFurniture != null
                ? existingFurniture : (IList<float>)FrontageStations(road, centreline);

            RoadPowerLinePlan plan = RoadPowerLinePlanner.Plan(s, centreline, halfWidth, exclusions,
                furniture, ground);
            note = plan.Summary();
            return plan;
        }

        /// <summary> The parent road's curve as world points over the run's own extent, together with the station
        /// spans the run must not plant a pole in and the road's half width.
        ///
        /// The bridge and tunnel exclusions are found by MARKING samples rather than by inverting the spline's
        /// arc-length map: each sample already knows both its parameter and its station, so a sample whose parameter
        /// falls inside one of the spline's bridge or tunnel spans marks its station, and consecutive marked samples
        /// merge into one exclusion. That is exact to the sample spacing and needs no inverse the spline does not
        /// offer. </summary>
        public List<Vector3> SampleCentreline(Road road, out List<Vector2> exclusions, out float halfWidth,
            out string note)
        {
            exclusions = new List<Vector2>();
            halfWidth = 0f;
            note = string.Empty;

            SplineC spline = road.spline;
            float length = spline != null ? spline.distance : -1f;
            if (length <= 0f)
            {
                note = "this road's spline has not been built yet, so it has no length to run a pole line along";
                return null;
            }

            RoadPowerLineSettings s = settings != null ? settings : RoadPowerLineSettings.Default();
            DistributionPoleClass e = s.electrical != null ? s.electrical : DistributionPoleClass.Default();
            halfWidth = road.RoadWidth() * 0.5f;

            float from = Mathf.Clamp(s.startStationMetres, 0f, length);
            float to = s.endStationMetres < 0f ? length : Mathf.Clamp(s.endStationMetres, 0f, length);
            if (to - from < e.spanLengthMetres)
            {
                note = "the run from " + from.ToString("0.#", CultureInfo.InvariantCulture) + " m to "
                       + to.ToString("0.#", CultureInfo.InvariantCulture) + " m is shorter than one "
                       + e.spanLengthMetres.ToString("0.#", CultureInfo.InvariantCulture)
                       + " m span, so it would carry poles and no wire";
                return null;
            }

            float spacing = centrelineSampleSpacingMetres > 0.5f ? centrelineSampleSpacingMetres : 5f;
            int intervals = Mathf.Max(1, Mathf.CeilToInt((to - from) / spacing));
            List<Vector3> points = new List<Vector3>(intervals + 1);
            int excludedFrom = -1;

            for (int i = 0; i <= intervals; i++)
            {
                float distance = Mathf.Lerp(from, to, (float)i / intervals);
                float t = spline.TranslateDistBasedToParam(distance);
                points.Add(spline.GetSplineValue(t, false));

                float station = distance - from;
                bool blocked = InSpan(spline.BridgeParams, t) || InSpan(spline.TunnelParams, t);
                if (blocked && excludedFrom < 0) { excludedFrom = i; }
                else if (!blocked && excludedFrom >= 0)
                {
                    exclusions.Add(new Vector2(Mathf.Lerp(from, to, (float)excludedFrom / intervals) - from,
                        station));
                    excludedFrom = -1;
                }
            }
            if (excludedFrom >= 0)
            {
                exclusions.Add(new Vector2(Mathf.Lerp(from, to, (float)excludedFrom / intervals) - from, to - from));
            }
            return points;
        }

        /// <summary> The stations of the frontage furniture already on this road, so the planner can nudge a pole off a
        /// bin instead of into it.
        ///
        /// RM-13's furniture is placed by <c>EdgeObjectMaker</c>, one per kind per node, and each maker records the
        /// WORLD position of every object it placed rather than a station. So the stations are recovered by projecting
        /// those positions onto the same centreline the poles will stand along
        /// (<see cref="RoadPowerLinePlanner.StationOf"/>) -- the road's own curve, sampled once, measured once.
        ///
        /// Every maker on every node is read, including the ones whose objects sit on the far side of the road. That is
        /// deliberate and it is not a bug to be tightened later: a pole run occupies one frontage, but which frontage a
        /// maker's objects are on is a question about their lateral offset and their rotation, and answering it here
        /// would put a second copy of that reasoning beside the one place that owns it. Reading them all means the run
        /// keeps clear of a bin on the other kerb it did not need to avoid, which costs a nudge of half a metre and
        /// never costs a pole -- the maximum nudge is five metres and a span is forty.
        ///
        /// A maker with no placed objects contributes nothing, which is what makes this honest: it reports the
        /// furniture that is THERE, not the furniture the author configured and has not built yet. </summary>
        public List<float> FrontageStations(Road road, IList<Vector3> centreline)
        {
            List<float> stations = new List<float>();
            if (road == null || road.spline == null || centreline == null || centreline.Count < 2)
            {
                return stations;
            }

            float[] cumulative = RoadPowerLinePlanner.Cumulative(centreline);
            List<SplineN> nodes = road.spline.nodes;
            if (nodes == null) { return stations; }

            for (int n = 0; n < nodes.Count; n++)
            {
                SplineN node = nodes[n];
                if (node == null || node.EdgeObjects == null) { continue; }
                for (int m = 0; m < node.EdgeObjects.Count; m++)
                {
                    EdgeObjectMaker maker = node.EdgeObjects[m];
                    if (maker == null || maker.edgeObjectLocations == null) { continue; }
                    for (int o = 0; o < maker.edgeObjectLocations.Count; o++)
                    {
                        stations.Add(RoadPowerLinePlanner.StationOf(
                            centreline, cumulative, maker.edgeObjectLocations[o]));
                    }
                }
            }
            return stations;
        }

        /// <summary> Whether a spline parameter falls inside any of a list of spans.
        ///
        /// PUBLIC because D-5's <c>RailAlignment</c> needs the same question answered about the same lists: a road is
        /// grade separated from a railway exactly where its spline says bridge or tunnel, so the crossing search offers
        /// only the runs where this returns false. Two spellings of "is this station on a bridge" is how one of them
        /// ends up inclusive at the end and the other exclusive. </summary>
        public static bool InSpan(List<SplineSpan> spans, float t)
        {
            if (spans == null) { return false; }
            for (int i = 0; i < spans.Count; i++)
            {
                if (t >= spans[i].start && t <= spans[i].end) { return true; }
            }
            return false;
        }
    }
}

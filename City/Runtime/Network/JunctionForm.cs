#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-16's junction FORM: what shape a junction takes, as distinct from how it is controlled. The four
    /// forms are the ones a highway authority actually distinguishes, and they are not a re-spelling of the stop
    /// control already on the component: a priority junction and a signalised junction share one geometry and differ
    /// only in control, whereas a roundabout and a grade-separated interchange are different geometry that then
    /// IMPLIES its control. So the form is authored and the control is a consequence of it -- see
    /// <see cref="JunctionForm.ControlFor"/> -- which is why no stop-type field is duplicated here. </summary>
    public enum JunctionTypeEnum
    {
        /// <summary> At-grade, minor road gives way. The default, and the geometry the tool already builds. </summary>
        Priority = 0,

        /// <summary> At-grade, the same geometry, but the right of way is assigned by signals in turn. </summary>
        Signalised = 1,

        /// <summary> The legs trim to an inscribed circle and traffic circulates one way around a raised island;
        /// entering traffic yields to circulating traffic, so there is no stop line and no signal. </summary>
        Roundabout = 2,

        /// <summary> One road is carried over the other on a deck and the two are joined by ramps instead of by a
        /// shared surface. The at-grade junction is NOT removed by this -- the flyover and its ramps are added
        /// alongside it, which is what makes an interchange an upgrade rather than a replacement. </summary>
        GradeSeparatedInterchange = 3
    }


    /// <summary> The right of way a form implies. Read off the form rather than authored, so a signalised
    /// roundabout or a stop-signed flyover cannot be described. </summary>
    public enum JunctionControl
    {
        /// <summary> Give way or stop on the minor arm; the major arm is unimpeded. </summary>
        StopOrYield = 0,

        /// <summary> Signal heads assign the right of way in a timed or detected sequence. </summary>
        Signals = 1,

        /// <summary> Yield at the entry line to traffic already circulating. No stop, no signal. </summary>
        EntryYield = 2,

        /// <summary> No control at the crossing point at all, because the two roads never meet on one surface. </summary>
        Uncontrolled = 3
    }


    /// <summary> Everything a junction needs to be resolved into a plan. One request type for all four forms, with
    /// only the fields the chosen form reads being consulted: the alternative is four near-identical entry points
    /// and a caller that has to know which to call. Every field is a parameter one of the shipped operators already
    /// takes, so nothing here re-states geometry that lives elsewhere. </summary>
    public sealed class JunctionRequest
    {
        public JunctionTypeEnum form = JunctionTypeEnum.Priority;

        // --- Roundabout (RoundaboutGeometry) ---
        /// <summary> Null falls back to the NCHRP-672 defaults. </summary>
        public RoundaboutClass roundabout;
        /// <summary> One bearing per leg, degrees. Fewer than three legs is not a roundabout. </summary>
        public IList<double> legBearingsDeg;
        /// <summary> One elevation per leg, metres, resolved to a single circulatory plane. </summary>
        public IList<double> legElevationsMetres;
        /// <summary> One approach length per leg, metres; how much road each leg has to reach the plane. </summary>
        public IList<double> legApproachLengthsMetres;
        /// <summary> Fastest-path radius R1 through the entry, metres. Non-positive means fully deflected. </summary>
        public double entryFastestPathRadiusMetres;
        /// <summary> Splitter-island length on the shortest leg, metres -- the one that has to clear the minimum. </summary>
        public double splitterIslandLengthMetres;

        // --- Grade-separated interchange (GradeSeparation, InterchangeRamp) ---
        /// <summary> Null falls back to the 5.1 m clearance default. </summary>
        public GradeSeparationClass separation;
        /// <summary> Null falls back to the authored ramp defaults. </summary>
        public InterchangeRampClass ramp;
        /// <summary> Null falls back to the superelevation defaults the minimum ramp radius is solved from. </summary>
        public SuperelevationClass superelevation;
        public int classRankA;
        public int identifierA;
        public int classRankB;
        public int identifierB;
        /// <summary> Each road's class maximum gradient as a fraction; the RAISED road's is the one used. </summary>
        public double maxGradientFractionA;
        public double maxGradientFractionB;
        /// <summary> How much approach the raised road actually has on each side of the crossing, metres. </summary>
        public double availableApproachLengthMetres;
        /// <summary> Station along the raised road at which the crossing falls, metres. </summary>
        public double crossingStationMetres;
        /// <summary> Deck length to mark as bridge, metres. </summary>
        public double spanLengthMetres;
        /// <summary> Total length of the raised road, metres; the span is clamped to it. </summary>
        public double raisedRoadLengthMetres;
        /// <summary> Straight-line plan distance the connecting ramp must cover, metres. </summary>
        public double rampPlanDistanceMetres;
        /// <summary> Radius the author asked the ramp curve to use, metres. </summary>
        public double rampRequestedRadiusMetres;
        public RampAlignmentKind rampAlignment = RampAlignmentKind.Direct;
        /// <summary> Angle between the two roads at the crossing, degrees; sets how far the ramp has to turn. </summary>
        public double roadAngleBetweenDegrees = 90.0;
    }


    /// <summary> What resolving a junction produced. <c>resolved</c> and <c>flagged</c> are the same pair the shipped
    /// resolvers use, and <c>message</c> carries the refusal in the author's words. The form-specific numbers are the
    /// quantities the builder needs and the inspector quotes back; a field a form does not produce stays zero. </summary>
    public struct JunctionPlan
    {
        public bool resolved;
        public bool flagged;
        public string message;
        public JunctionTypeEnum form;
        public JunctionControl control;

        // --- Roundabout ---
        public double circulatoryWidthMetres;
        public double centralIslandDiameterMetres;
        public double circulatoryPlaneElevationMetres;
        public double worstLegSeparationDeg;
        public double worstApproachGradientChangePercent;

        // --- Grade-separated interchange ---
        /// <summary> True when road A is the one carried over; false when road B is. </summary>
        public bool raisedRoadIsA;
        public double raiseHeightMetres;
        public double requiredApproachLengthMetres;
        public double bridgeSpanStartMetres;
        public double bridgeSpanEndMetres;
        public double rampLengthMetres;
        public double rampRadiusMetres;
        public double rampGradientFraction;
    }


    /// <summary> B-16's dispatcher. It owns no geometry of its own: each form is routed to the operator that already
    /// implements it -- <see cref="RoundaboutGeometry"/>, <see cref="GradeSeparation"/>, <see cref="InterchangeRamp"/>
    /// -- and their own resolve structs are read rather than re-declared. What is new here is only the two things
    /// none of them could know: which operator a given form means, and which right of way that form implies. </summary>
    public static class JunctionForm
    {
        /// <summary> The right of way the form implies. This is the whole reason the form does not duplicate the stop
        /// control already on the component: the control is DERIVED, so the two can never disagree. </summary>
        public static JunctionControl ControlFor(JunctionTypeEnum form)
        {
            switch (form)
            {
                case JunctionTypeEnum.Signalised: return JunctionControl.Signals;
                case JunctionTypeEnum.Roundabout: return JunctionControl.EntryYield;
                case JunctionTypeEnum.GradeSeparatedInterchange: return JunctionControl.Uncontrolled;
                default: return JunctionControl.StopOrYield;
            }
        }

        /// <summary> Whether the form builds its junction on one shared road surface. The two at-grade forms do, and
        /// their geometry is the geometry the tool already builds -- which is why they are accepted unchanged. </summary>
        public static bool IsAtGrade(JunctionTypeEnum form)
        {
            return form == JunctionTypeEnum.Priority || form == JunctionTypeEnum.Signalised;
        }

        /// <summary> The smallest inscribed circle diameter this class can be built at, metres. TWO bounds have to
        /// hold and the larger governs: the circulatory ring must leave a positive central island (ICD > 2 x
        /// circulatory width), and the circulating CENTRELINE radius, which sits at ICD/2 - width/2, must not fall
        /// below the class's tightest circulatory radius (ICD >= 2 x minimum radius + width). At the default class
        /// the second bound governs at 28.95 m, and it reproduces the 13.5 m centreline the class documents at an
        /// ICD of 32 m. Nothing is authored here; both bounds are algebra on figures already in the class. </summary>
        public static double MinimumInscribedDiameterMetres(RoundaboutClass cls)
        {
            double width = RoundaboutGeometry.CirculatoryWidthMetres(cls);
            double islandBound = 2.0 * width;
            double radiusBound = 2.0 * cls.minCirculatoryRadiusMetres + width;
            return radiusBound > islandBound ? radiusBound : islandBound;
        }

        /// <summary> Resolve a junction into a plan. The at-grade forms are accepted as they stand -- their geometry is
        /// what the tool already builds, and B-16 leaves it untouched -- while the other two are handed to their
        /// operator and its refusals are returned verbatim. A null request is refused rather than defaulted, because a
        /// junction with nothing authored on it is a caller mistake, not a priority junction. </summary>
        public static JunctionPlan Resolve(JunctionRequest request)
        {
            if (request == null)
            {
                JunctionPlan none = new JunctionPlan();
                none.message = "no junction was described, so there is nothing to resolve";
                none.flagged = true;
                return none;
            }

            switch (request.form)
            {
                case JunctionTypeEnum.Roundabout: return ResolveRoundabout(request);
                case JunctionTypeEnum.GradeSeparatedInterchange: return ResolveInterchange(request);
                default: return ResolveAtGrade(request);
            }
        }

        /// <summary> The two at-grade forms. Nothing is computed and nothing is refused: the form only records which
        /// control the existing junction machinery should assign. </summary>
        private static JunctionPlan ResolveAtGrade(JunctionRequest request)
        {
            JunctionPlan plan = Start(request.form);
            plan.resolved = true;
            return plan;
        }

        private static JunctionPlan Start(JunctionTypeEnum form)
        {
            JunctionPlan plan = new JunctionPlan();
            plan.form = form;
            plan.control = ControlFor(form);
            plan.message = string.Empty;
            return plan;
        }

        private static JunctionPlan Refuse(JunctionPlan plan, string message)
        {
            plan.resolved = false;
            plan.flagged = true;
            plan.message = message;
            return plan;
        }

        private static string F(double v)
        {
            return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary> A roundabout, sized and checked by <see cref="RoundaboutGeometry"/>. The refusals are taken in the
        /// order an engineer would: is it big enough to be one at all, are the legs far enough apart, does each entry
        /// deflect, is the island long enough to shelter a crossing, and can every leg reach the one circulatory plane
        /// without kinking at the yield line. The size test is the class's own minimum diameter, so the separate
        /// positive-island test is not repeated -- it is one of the two bounds that minimum is built from. </summary>
        private static JunctionPlan ResolveRoundabout(JunctionRequest request)
        {
            JunctionPlan plan = Start(JunctionTypeEnum.Roundabout);
            RoundaboutClass cls = request.roundabout ?? RoundaboutClass.Default();
            plan.circulatoryWidthMetres = RoundaboutGeometry.CirculatoryWidthMetres(cls);
            plan.centralIslandDiameterMetres = RoundaboutGeometry.CentralIslandDiameterMetres(cls);

            int legCount = request.legBearingsDeg != null ? request.legBearingsDeg.Count : 0;
            if (legCount < 3)
            {
                return Refuse(plan, "a roundabout needs at least three legs to circulate between; " + legCount
                    + " were given, which is a road passing through rather than a junction");
            }

            double minimumDiameter = MinimumInscribedDiameterMetres(cls);
            if (cls.inscribedCircleDiameterMetres + 1e-9 < minimumDiameter)
            {
                return Refuse(plan, "an inscribed circle diameter of " + F(cls.inscribedCircleDiameterMetres)
                    + " m is below the " + F(minimumDiameter) + " m this class needs: a "
                    + F(plan.circulatoryWidthMetres) + " m circulatory way around it would leave a circulating"
                    + " centreline tighter than the " + F(cls.minCirculatoryRadiusMetres)
                    + " m minimum radius, so a design vehicle could not track around");
            }

            RoundaboutGeometry.LegSeparationCheck legs =
                RoundaboutGeometry.CheckLegSeparations(request.legBearingsDeg, cls);
            plan.worstLegSeparationDeg = legs.worstSeparationDeg;
            if (!legs.accepted)
            {
                return Refuse(plan, "two legs are only " + F(legs.worstSeparationDeg)
                    + " degrees apart, below the " + F(legs.minRequiredDeg)
                    + " degree minimum, so their entry geometry would cross");
            }

            if (!RoundaboutGeometry.EntryDeflectionAdequate(request.entryFastestPathRadiusMetres, cls))
            {
                return Refuse(plan, "an entry whose fastest path curves at " + F(request.entryFastestPathRadiusMetres)
                    + " m radius exceeds the " + F(cls.maxFastestPathRadiusMetres)
                    + " m cap, so a driver is never made to slow and the roundabout decorates rather than functions");
            }
            if (!RoundaboutGeometry.SplitterIslandAdequate(request.splitterIslandLengthMetres, cls))
            {
                return Refuse(plan, "the shortest leg's splitter island is " + F(request.splitterIslandLengthMetres)
                    + " m, under the " + F(cls.minSplitterIslandLengthMetres)
                    + " m minimum, so it cannot channel an entry or shelter someone crossing it");
            }

            RoundaboutGeometry.PlaneResolve plane = RoundaboutGeometry.ResolveCirculatoryPlane(
                request.legElevationsMetres, request.legApproachLengthsMetres, cls);
            plan.circulatoryPlaneElevationMetres = plane.planeElevationMetres;
            plan.worstApproachGradientChangePercent = plane.worstGradientChangePercent;
            if (!plane.allLegsWithinGradient)
            {
                return Refuse(plan, "leg " + plane.worstLegIndex + " would need a "
                    + F(plane.worstGradientChangePercent) + " % gradient change to reach the circulatory plane at "
                    + F(plane.planeElevationMetres) + " m, over the " + F(cls.maxApproachGradientChangePercent)
                    + " % limit, so it would kink at the yield line");
            }

            plan.resolved = true;
            return plan;
        }

        /// <summary> A grade-separated interchange: which road rises, whether it can rise far enough within the approach
        /// it has, where its deck is marked, and whether one ramp connecting the two can be driven. Each answer comes
        /// from the operator that owns it -- <see cref="GradeSeparation"/> for the first three,
        /// <see cref="InterchangeRamp"/> for the last -- and the ramp's own refusal text is passed through unedited
        /// rather than re-worded here. The DECK ITSELF is not generated: it is an author prefab loaded onto the raised
        /// road's bridge nodes, so what this returns is the span to mark, not a mesh. </summary>
        private static JunctionPlan ResolveInterchange(JunctionRequest request)
        {
            JunctionPlan plan = Start(JunctionTypeEnum.GradeSeparatedInterchange);
            GradeSeparationClass sep = request.separation ?? GradeSeparationClass.Default();
            InterchangeRampClass rampCls = request.ramp ?? InterchangeRampClass.Default();
            SuperelevationClass superCls = request.superelevation ?? SuperelevationClass.Default();

            int raised = GradeSeparation.ChooseRaisedRoad(request.classRankA, request.identifierA,
                request.classRankB, request.identifierB);
            plan.raisedRoadIsA = raised == 0;
            double raisedGradient = plan.raisedRoadIsA ? request.maxGradientFractionA : request.maxGradientFractionB;
            string raisedName = plan.raisedRoadIsA ? "A" : "B";

            if (raisedGradient <= 0.0)
            {
                return Refuse(plan, "road " + raisedName + " is the one that has to rise, but its class allows no"
                    + " gradient at all, so no length of approach could lift it the "
                    + F(sep.verticalClearanceMetres) + " m of clearance");
            }

            GradeSeparation.SeparationResolve separation = GradeSeparation.ResolveSeparation(
                sep.verticalClearanceMetres, raisedGradient, request.availableApproachLengthMetres);
            plan.raiseHeightMetres = separation.raiseHeightMetres;
            plan.requiredApproachLengthMetres = separation.requiredApproachLengthMetres;
            if (!separation.resolved)
            {
                return Refuse(plan, "road " + raisedName + " has " + F(request.availableApproachLengthMetres)
                    + " m of approach to the crossing but needs " + F(separation.requiredApproachLengthMetres)
                    + " m on each side to gain " + F(sep.verticalClearanceMetres) + " m of clearance at its "
                    + F(raisedGradient * 100.0) + " % gradient limit");
            }

            if (request.spanLengthMetres <= 0.0)
            {
                return Refuse(plan, "the flyover was resolved but no deck length was given, so there is no span to"
                    + " mark for the bridge prefab to sit on");
            }
            double spanStart;
            double spanEnd;
            GradeSeparation.BridgeSpanMetres(request.crossingStationMetres, request.spanLengthMetres,
                request.raisedRoadLengthMetres, out spanStart, out spanEnd);
            plan.bridgeSpanStartMetres = spanStart;
            plan.bridgeSpanEndMetres = spanEnd;

            InterchangeRamp.RampResolve ramp = InterchangeRamp.ResolveRamp(request.rampPlanDistanceMetres,
                separation.raiseHeightMetres, request.rampRequestedRadiusMetres, request.rampAlignment,
                request.roadAngleBetweenDegrees, raisedGradient, rampCls, superCls);
            plan.rampLengthMetres = ramp.rampLengthMetres;
            plan.rampRadiusMetres = ramp.radiusMetres;
            plan.rampGradientFraction = ramp.gradientFraction;
            if (!ramp.resolved)
            {
                return Refuse(plan, ramp.message);
            }

            plan.resolved = true;
            return plan;
        }
    }
}

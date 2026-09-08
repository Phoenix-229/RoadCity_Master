#region "Imports"
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RoadCityMaster.CrossSection;
#endregion


namespace RoadCityMaster
{
    public class RoadIntersection : MonoBehaviour
    {
        #region "Vars"
        public SplineN node1;
        public SplineN node2;

        public string node1uID;
        public string node2uID;

        //Unique ID
        protected string uID;
        public string intersectionName = "";

        public bool isSameSpline = false;
        public bool isDrawingGizmo = true;
        public bool isFlipped = false;
        public bool isUsingDefaultMaterials = true;
        public bool isAutoUpdatingIntersection = true;
        public bool isNode2BLeftTurnLane = true;
        public bool isNode2BRightTurnLane = true;
        public bool isNode2FLeftTurnLane = true;
        public bool isNode2FRightTurnLane = true;
        public bool isFirstSpecialFirst = false;
        public bool isFirstSpecialLast = false;
        public bool isSecondSpecialFirst = false;
        public bool isSecondSpecialLast = false;
        public bool isCornerRR1Enabled = false;
        public bool isCornerRR2Enabled = false;
        public bool isCornerRL1Enabled = false;
        public bool isCornerRL2Enabled = false;
        public bool isCornerLR1Enabled = false;
        public bool isCornerLR2Enabled = false;
        public bool isCornerLL1Enabled = false;
        public bool isCornerLL2Enabled = false;

        #region "Marker materials"
        public Material markerCenter1 = null;
        public Material markerCenter2 = null;
        public Material markerCenter3 = null;
        public Material markerExtStretch1 = null;
        public Material markerExtStretch2 = null;
        public Material markerExtStretch3 = null;
        public Material markerExtTiled1 = null;
        public Material markerExtTiled2 = null;
        public Material markerExtTiled3 = null;
        #endregion

        #region "Lane materials"
        public Material lane0Mat1 = null;
        public Material lane0Mat2 = null;
        public Material lane1Mat1 = null;
        public Material lane1Mat2 = null;
        public Material lane2Mat1 = null;
        public Material lane2Mat2 = null;
        public Material lane3Mat1 = null;
        public Material lane3Mat2 = null;

        public Material lane1Mat1Disabled = null;
        public Material lane1Mat2Disabled = null;
        public Material lane1Mat1DisabledActive = null;
        public Material lane1Mat2DisabledActive = null;
        public Material lane2Mat1Disabled = null;
        public Material lane2Mat2Disabled = null;
        public Material lane2Mat1DisabledActive = null;
        public Material lane2Mat2DisabledActive = null;
        public Material lane2Mat1DisabledActiveR = null;
        public Material lane2Mat2DisabledActiveR = null;
        public Material lane3Mat1Disabled = null;
        public Material lane3Mat2Disabled = null;
        #endregion

        //Width of the largest of road connected
        /// <summary> 10 * 1.25f = intersectionWidth; Never written, only read </summary>
        public int intersectionWidth = 10;
        /// <summary> Amount of lanes from road of node1 </summary>
        public int lanesAmount;
        public int ignoreSide = -1;
        public int ignoreCorner = -1;

        #region "LaneMatching"
        /// <summary> RM-90: what this junction does about two approaches carrying different numbers of lanes.
        ///
        /// <see cref="lanesAmount"/> above is read from node1's road only, which is fine for choosing a marker
        /// texture (2L / 4L / 6L) but says nothing about the OTHER approach. When a four-lane arterial meets a
        /// two-lane street, two of the arterial's lanes have nowhere to go, and the corner build -- which sees
        /// approaches as widths -- has no way to say so. This reads both roads' per-direction counts and returns
        /// the real matching: the through count, the surplus lanes that must end, and the length of the taper
        /// they end over.
        ///
        /// The arithmetic lives in <see cref="ApproachMatch"/> and the taper length in
        /// <see cref="LaneModel.TaperLengthMetres"/>; nothing is recomputed here. Returns a refused verdict --
        /// never a guess -- when either node is missing its road. </summary>
        public ApproachMatchVerdict MatchApproaches()
        {
            Road roadA = node1 != null && node1.spline != null ? node1.spline.road : null;
            Road roadB = node2 != null && node2.spline != null ? node2.spline.road : null;
            if (roadA == null || roadB == null)
            {
                ApproachMatchVerdict missing = new ApproachMatchVerdict();
                missing.citation = "";
                missing.refusal = "intersection '" + IntersectionName()
                    + "' cannot be reconciled: " + (roadA == null ? "the first" : "the second")
                    + " approach has no road behind its node.";
                return missing;
            }
            return ApproachMatch.Match(ApproachSection.FromRoad(roadA), ApproachSection.FromRoad(roadB));
        }

        /// <summary> The last reconciliation of the two approaches, recomputed at the start of every build by
        /// <see cref="ReconcileApproaches"/>. Deliberately NOT serialised: it is derived entirely from the two
        /// roads, so persisting it would let a stale verdict outlive the road edit that invalidated it -- the same
        /// reason RM-52's panel stamps its own results. Callers that need it outside a build read it through
        /// <see cref="MatchApproaches"/>, which computes rather than caches. </summary>
        [System.NonSerialized] public ApproachMatchVerdict approachMatch;

        /// <summary> Computes the reconciliation once per build and leaves it in <see cref="approachMatch"/>, so the
        /// corner build, the marker choice and RM-52's panel all read ONE answer instead of each calling the
        /// matcher and risking three. Returns the verdict for the caller that wants it immediately. </summary>
        public ApproachMatchVerdict ReconcileApproaches()
        {
            approachMatch = MatchApproaches();
            return approachMatch;
        }

        /// <summary> The refusal this junction should contribute to RM-52's panel, or false when it reconciles.
        /// The station is the junction's own position along the first approach, so selecting the entry in the
        /// panel takes the author to the place the problem is rather than to the road's start.
        ///
        /// The verdict-to-entry mapping itself is <see cref="ApproachMatch.TryRefusal"/>, not repeated here: it is
        /// asserted by the gate, and a second copy of it in a component no gate can instantiate is exactly how the
        /// panel and the arithmetic would drift apart. This supplies only what the scene knows -- which object,
        /// and where along it. </summary>
        public bool TryApproachRefusal(out RefusalEntry entry)
        {
            double station = node1 != null ? node1.time * ApproachLengthMetres(node1) : 0.0;
            return ApproachMatch.TryRefusal(MatchApproaches(), node1uID, IntersectionName(), station, out entry);
        }

        /// <summary> A label for a refusal or a gizmo: the authored name when there is one, otherwise the
        /// object's name, so an entry in the panel is never blank. </summary>
        public string IntersectionName()
        {
            if (!string.IsNullOrEmpty(intersectionName)) { return intersectionName; }
            return name;
        }

        /// <summary> The length of the road an approach node sits on, used only to turn the node's normalised
        /// spline time into a station in metres for a refusal entry. The spline reports -1 before it has been
        /// measured, which would put the refusal at a negative station, so an unmeasured spline reads as 0. </summary>
        private static float ApproachLengthMetres(SplineN node)
        {
            if (node == null || node.spline == null || node.spline.distance < 0f) { return 0f; }
            return node.spline.distance;
        }
        #endregion

        public IntersectionTypeEnum intersectionType = IntersectionTypeEnum.FourWay;
        public iStopTypeEnum intersectionStopType = iStopTypeEnum.StopSign_AllWay;
        public RoadTypeEnum roadType = RoadTypeEnum.NoTurnLane;
        public LightTypeEnum lightType = LightTypeEnum.Timed;

        #region "B-16 junction form"
        /// <summary> What SHAPE this junction takes. `intersectionType` says how many legs meet and
        /// `intersectionStopType` says who waits; this says whether they meet on one surface at all. The two at-grade
        /// forms leave the existing geometry exactly as it is -- only the stop control they permit differs -- while a
        /// roundabout replaces the crossing surface with a circulatory way and an interchange keeps the crossing and
        /// ADDS a flyover and ramps beside it. </summary>
        public City.JunctionTypeEnum junctionType = City.JunctionTypeEnum.Priority;

        /// <summary> Inscribed circle diameter for the roundabout form, metres: the outer circle the legs trim to.
        /// Refused below the diameter the class's circulatory width and minimum circulating radius imply. </summary>
        public float roundaboutInscribedDiameterMetres = 40f;

        /// <summary> Entry width of one approach lane at the yield line, metres; the circulatory carriageway width is
        /// derived from it, so it sizes the ring and therefore the island. </summary>
        public float roundaboutEntryWidthMetres = 4.5f;

        /// <summary> Splitter-island length on the shortest leg, metres -- the leg that has to clear the minimum, since
        /// the others are longer by definition. </summary>
        public float roundaboutSplitterIslandLengthMetres = 15f;

        /// <summary> Headroom the raised road must leave over the road beneath it, metres. This is what sets how far it
        /// climbs, and therefore how much approach length it needs on each side. </summary>
        public float interchangeVerticalClearanceMetres = 5.1f;

        /// <summary> How the connecting ramp is laid out: straight off the mainline, part-way around, or a full loop.
        /// The choice sets how far the ramp has to turn and so how long its curve must be. </summary>
        public City.RampAlignmentKind rampAlignment = City.RampAlignmentKind.Direct;

        /// <summary> Plan radius asked of the ramp curve, metres. Refused below the radius its design speed and
        /// superelevation allow, because a curve that cannot be driven at the speed it is signed for is not a ramp.
        ///
        /// The default is the DERIVED floor, not a round number: at the ramp class's 50 km/h design speed the published
        /// relation R = V^2 / (127 (e + f)) with e = 0.06 and the tabulated f = 0.16 gives 89.5 m, which is well above
        /// the class's own 50 m house minimum, so 90 m is the smallest radius a fresh interchange can actually be built
        /// at. Defaulting to the house minimum instead would have opened every new junction already refusing itself.
        /// </summary>
        public float rampRadiusMetres = 90f;

        /// <summary> Deck length to mark as bridge on the raised road, metres. The deck itself is an author prefab
        /// loaded onto the span's nodes; this only says how long the span is. </summary>
        public float flyoverSpanLengthMetres = 30f;

        /// <summary> Whether the last resolve refused, and why, for the inspector to quote back. Construction scratch:
        /// it is re-derived on every resolve and must not persist in the scene file. </summary>
        [System.NonSerialized]
        public City.JunctionPlan junctionPlan;

        /// <summary> The right of way this form implies, derived rather than stored. </summary>
        public City.JunctionControl ResolveControl()
        {
            return City.JunctionForm.ControlFor(junctionType);
        }

        /// <summary> Whether the authored stop control is one this FORM can actually carry. The form does not replace
        /// `intersectionStopType` -- every traffic-light path still reads it -- it constrains which values of it mean
        /// anything: a roundabout has no stop line to sign and a flyover has no shared surface to signal. Returns true
        /// with an empty refusal when the pair is consistent. </summary>
        public bool StopTypeIsCompatible(out string refusal)
        {
            refusal = string.Empty;
            switch (ResolveControl())
            {
                case City.JunctionControl.Signals:
                    if (intersectionStopType == iStopTypeEnum.TrafficLight1
                        || intersectionStopType == iStopTypeEnum.TrafficLight2) { return true; }
                    refusal = "a signalised junction is controlled by signal heads, so its stop control has to be"
                        + " TrafficLight1 or TrafficLight2, not " + intersectionStopType;
                    return false;

                case City.JunctionControl.EntryYield:
                    if (intersectionStopType == iStopTypeEnum.None) { return true; }
                    refusal = "a roundabout is entered by yielding to traffic already circulating, so it has no stop"
                        + " line and no signal to set; its stop control has to be None, not " + intersectionStopType;
                    return false;

                case City.JunctionControl.Uncontrolled:
                    if (intersectionStopType == iStopTypeEnum.None) { return true; }
                    refusal = "at an interchange the two roads never share a surface, so there is nothing at the"
                        + " crossing to control; its stop control has to be None, not " + intersectionStopType;
                    return false;

                default:
                    if (intersectionStopType == iStopTypeEnum.StopSign_AllWay
                        || intersectionStopType == iStopTypeEnum.None) { return true; }
                    refusal = "a priority junction assigns the right of way by sign, so its stop control has to be"
                        + " StopSign_AllWay or None; " + intersectionStopType + " needs the Signalised form";
                    return false;
            }
        }

        /// <summary> Assemble everything the form needs and hand it to <see cref="City.JunctionForm.Resolve"/>, leaving
        /// the answer in <see cref="junctionPlan"/> for the inspector. Nothing is computed here that an operator owns:
        /// this reads the authored fields, samples the two splines for the leg geometry a roundabout needs, and ranks
        /// the two roads for the flyover. Returns a refused plan -- never a guess -- when either node has no road.
        ///
        /// Leg geometry, for the roundabout form: a junction of two roads has four arms, and
        /// <see cref="intersectionType"/> already says whether the fourth exists. Which of the terminating road's two
        /// arms is the absent one does not change the separation verdict: the three-leg set is either
        /// {b1, b1+180, b2} or {b1, b1+180, b2+180}, and both have the same pair of adjacent gaps by symmetry. Each
        /// leg is sampled at the inscribed circle edge rather than at the junction centre, because that is where it
        /// meets the circulatory way -- sampling the centre would give all four arms one elevation and turn the
        /// approach-gradient check into a formality. </summary>
        public City.JunctionPlan ResolveJunction()
        {
            Road roadA = node1 != null && node1.spline != null ? node1.spline.road : null;
            Road roadB = node2 != null && node2.spline != null ? node2.spline.road : null;
            junctionPlan = new City.JunctionPlan();
            junctionPlan.form = junctionType;
            junctionPlan.control = ResolveControl();
            if (roadA == null || roadB == null)
            {
                junctionPlan.flagged = true;
                junctionPlan.message = "junction '" + IntersectionName() + "' cannot be resolved: "
                    + (roadA == null ? "the first" : "the second") + " approach has no road behind its node.";
                return junctionPlan;
            }

            City.JunctionRequest request = new City.JunctionRequest();
            request.form = junctionType;
            request.roundabout = RoundaboutClassFromFields();
            request.separation = new City.GradeSeparationClass();
            request.separation.verticalClearanceMetres = interchangeVerticalClearanceMetres;
            request.splitterIslandLengthMetres = roundaboutSplitterIslandLengthMetres;
            request.rampAlignment = rampAlignment;
            request.rampRequestedRadiusMetres = rampRadiusMetres;
            request.spanLengthMetres = flyoverSpanLengthMetres;

            List<double> bearings = new List<double>();
            List<double> elevations = new List<double>();
            List<double> approaches = new List<double>();
            double halfDiameter = roundaboutInscribedDiameterMetres * 0.5f;
            AddArms(node1, halfDiameter, true, bearings, elevations, approaches);
            AddArms(node2, halfDiameter, intersectionType == IntersectionTypeEnum.FourWay,
                bearings, elevations, approaches);
            request.legBearingsDeg = bearings;
            request.legElevationsMetres = elevations;
            request.legApproachLengthsMetres = approaches;
            request.roadAngleBetweenDegrees = ArmAngleBetweenDegrees();

            FillInterchange(request, roadA, roadB);
            junctionPlan = City.JunctionForm.Resolve(request);
            return junctionPlan;
        }

        /// <summary> The roundabout class this junction authored: the NCHRP defaults with the three fields the author
        /// is given control of overridden, so every figure the author did not touch stays the cited one. </summary>
        private City.RoundaboutClass RoundaboutClassFromFields()
        {
            City.RoundaboutClass cls = City.RoundaboutClass.Default();
            cls.inscribedCircleDiameterMetres = roundaboutInscribedDiameterMetres;
            cls.entryWidthMetres = roundaboutEntryWidthMetres;
            cls.minSplitterIslandLengthMetres = roundaboutSplitterIslandLengthMetres;
            return cls;
        }

        /// <summary> Add one road's arms as legs: its bearing, its elevation where it meets the inscribed circle, and
        /// how much road that arm has left to reach the circulatory plane over. The forward arm is always added; the
        /// arm behind the node is added only when the junction has a fourth leg. </summary>
        private static void AddArms(SplineN node, double halfDiameterMetres, bool includeArmBehind,
            List<double> bearings, List<double> elevations, List<double> approaches)
        {
            if (node == null || node.spline == null) { return; }
            float length = node.spline.distance;
            float forwardBearing = Mathf.Atan2(node.tangent.x, node.tangent.z) * Mathf.Rad2Deg;
            if (length <= 0f)
            {
                bearings.Add(forwardBearing);
                elevations.Add(node.pos.y);
                approaches.Add(0.0);
                if (includeArmBehind)
                {
                    bearings.Add(forwardBearing + 180f);
                    elevations.Add(node.pos.y);
                    approaches.Add(0.0);
                }
                return;
            }

            float edgeStep = (float)(halfDiameterMetres / length);
            float ahead = Mathf.Clamp01(node.time + edgeStep);
            bearings.Add(forwardBearing);
            elevations.Add(node.spline.GetSplineValue(ahead).y);
            approaches.Add((1f - ahead) * length);
            if (!includeArmBehind) { return; }

            float behind = Mathf.Clamp01(node.time - edgeStep);
            bearings.Add(forwardBearing + 180f);
            elevations.Add(node.spline.GetSplineValue(behind).y);
            approaches.Add(behind * length);
        }

        /// <summary> The plan angle between the two roads at this junction, degrees, 0-180. </summary>
        private double ArmAngleBetweenDegrees()
        {
            if (node1 == null || node2 == null) { return 90.0; }
            Vector3 a = new Vector3(node1.tangent.x, 0f, node1.tangent.z);
            Vector3 b = new Vector3(node2.tangent.x, 0f, node2.tangent.z);
            if (a.sqrMagnitude < 0.000001f || b.sqrMagnitude < 0.000001f) { return 90.0; }
            return Vector3.Angle(a.normalized, b.normalized);
        }

        /// <summary> Fill the flyover half of the request. The class RANK is each road's design speed in km/h -- a
        /// cited figure that already orders the classes by how major they are, unlike the enum's own ordering, which
        /// puts a service lane and a footpath after a motorway. The identifier tie-break, used only when the two ranks
        /// are equal, is an ordinal comparison of the two node uIDs, so it is stable across sessions where an instance
        /// id would not be. Which road rises is asked of <see cref="City.GradeSeparation.ChooseRaisedRoad"/> here as
        /// well, purely to know whose approach length to measure; it is the same deterministic call the resolver makes,
        /// not a second copy of the rule. The ramp's plan distance is the approach the raised road needs, because a
        /// ramp runs from the deck back down to the road below and that is how far that is. </summary>
        private void FillInterchange(City.JunctionRequest request, Road roadA, Road roadB)
        {
            RoadClassProfile profileA = RoadClassDefaults.For(roadA.roadClass);
            RoadClassProfile profileB = RoadClassDefaults.For(roadB.roadClass);
            request.classRankA = (int)profileA.DesignSpeedKPH;
            request.classRankB = (int)profileB.DesignSpeedKPH;
            request.maxGradientFractionA = profileA.MaxGrade;
            request.maxGradientFractionB = profileB.MaxGrade;

            bool aWinsTieBreak = string.CompareOrdinal(node1uID ?? string.Empty, node2uID ?? string.Empty) >= 0;
            request.identifierA = aWinsTieBreak ? 1 : 0;
            request.identifierB = aWinsTieBreak ? 0 : 1;

            bool raiseA = City.GradeSeparation.ChooseRaisedRoad(request.classRankA, request.identifierA,
                request.classRankB, request.identifierB) == 0;
            SplineN raisedNode = raiseA ? node1 : node2;
            double raisedGradient = raiseA ? request.maxGradientFractionA : request.maxGradientFractionB;
            float raisedLength = raisedNode != null && raisedNode.spline != null ? raisedNode.spline.distance : 0f;
            float raisedTime = raisedNode != null ? raisedNode.time : 0f;
            request.raisedRoadLengthMetres = raisedLength;
            request.crossingStationMetres = raisedTime * raisedLength;
            double ahead = (1f - raisedTime) * raisedLength;
            double behind = raisedTime * raisedLength;
            request.availableApproachLengthMetres = ahead < behind ? ahead : behind;
            request.rampPlanDistanceMetres = City.GradeSeparation.RequiredApproachLengthMetres(
                interchangeVerticalClearanceMetres, raisedGradient);
        }
        #endregion

        #region "CalculationData"
        // Allocated by Intersections.InitializeIntersectionObjectsInternal on every construction pass, so it is
        // construction scratch and is marked as such: serialization skipped it in any case, and the attribute says so
        // instead of leaving a warning to say it.
        [System.NonSerialized]
        public CornerPositionMaker[] cornerPoints;

        public Vector3 cornerRR;
        public Vector3 cornerRROuter;
        public Vector3 cornerRRRampOuter;
        public Vector3 cornerRL;
        public Vector3 cornerRLOuter;
        public Vector3 cornerRLRampOuter;
        public Vector3 cornerLR;
        public Vector3 cornerLROuter;
        public Vector3 cornerLRRampOuter;
        public Vector3 cornerLL;
        public Vector3 cornerLLOuter;
        public Vector3 cornerLLRampOuter;

        public Vector2 cornerRR2D;
        public Vector2 cornerRL2D;
        public Vector2 cornerLR2D;
        public Vector2 cornerLL2D;

        public Vector3[] cornerLRCornerRR;
        public Vector3[] cornerLLCornerRL;
        public Vector3[] cornerLLCornerLR;
        public Vector3[] cornerRLCornerRR;
        #endregion

        public float gradeMod = 0.375f;
        public float gradeModNegative = 0.75f;
        public float scalingSense = 3f;
        public float oddAngle;
        public float evenAngle;
        public float maxInterDistance = 0f;
        public float maxInterDistanceSQ = 0f;
        public float height = 50000f;
        public float signHeight = -2000f;

        #region "Traffic Light Vars"
        public bool isLightsEnabled = true;
        public bool isRegularPoleAlignment = true;
        public bool isTrafficPoleStreetLight = true;
        public bool isTrafficLightGray = false;
        public bool isLeftTurnYieldOnGreen = true;
        public float streetLightRange = 30f;
        public float streetLightIntensity = 1f;
        public Color streetLightColor = new Color(1f, 0.7451f, 0.27451f, 1f);
        private int fixedTimeIndex = 0;
        public TrafficLightController lightsRR;
        public TrafficLightController lightsRL;
        public TrafficLightController lightsLL;
        public TrafficLightController lightsLR;
        public float fixedTimeRegularLightLength = 10f;
        public float fixedTimeLeftTurnLightLength = 5f;
        public float fixedTimeAllRedLightLength = 1f;
        public float fixedTimeYellowLightLength = 2f;
        // Rebuilt by CreateFixedSequence() from the four fixedTime* lengths and roadType at the top of Start(), so the
        // serialized floats above are the saved state and this list is derived from them. TrafficLightSequence also has
        // no parameterless constructor, which is a second reason not to hand it to the serializer.
        [System.NonSerialized]
        public List<TrafficLightSequence> fixedTimeSequenceList;
        #endregion
        #endregion


        public enum IntersectionTypeEnum { ThreeWay, FourWay };
        public enum iStopTypeEnum { StopSign_AllWay, TrafficLight1, None, TrafficLight2 };
        public enum RoadTypeEnum { NoTurnLane, TurnLane, BothTurnLanes };
        public enum LightTypeEnum { Timed, Sensors };


        // A struct may be better and faster
        public class CornerPositionMaker
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 directionFromCenter;
        }


        private Construction2DRect boundsRect;


        #region "Setup"
        /// <summary> Links nodes and intersection </summary>
        public void Setup(SplineN _node1, SplineN _node2)
        {
            if (_node1.spline == _node2.spline)
            {
                isSameSpline = true;
            }

			if (_node1.idOnSpline < _node2.idOnSpline || _node1.isEndPoint || (!isSameSpline && !_node2.isEndPoint))
			{
				node1 = _node1;
				node2 = _node2;
			}
			else
			{
				node1 = _node2;
				node2 = _node1;
			}

			if (node1.isEndPoint || node2.isEndPoint)
			{
				intersectionType = IntersectionTypeEnum.ThreeWay;
			}
			else
			{
				intersectionType = IntersectionTypeEnum.FourWay;
			}

            node1.intersectionOtherNode = node2;
            node2.intersectionOtherNode = node1;

            node1.ToggleHideFlags(true);
            node2.ToggleHideFlags(true);

            node1uID = node1.uID;
            node2uID = node2.uID;
            node1.isIntersection = true;
            node2.isIntersection = true;
            node1.intersection = this;
            node2.intersection = this;
        }


        /// <summary> Deletes Meshes based on road name and the centermarker if _node is intersections node1 </summary>
        public void DeleteRelevantChildren(SplineN _node, string _string)
        {
            Transform transformChild;
            int childCount = transform.childCount;
            for (int index = childCount - 1; index >= 0; index--)
            {
                transformChild = transform.GetChild(index);
                if (transformChild.name.ToLower().Contains(_string.ToLower()))
                {
                    Object.DestroyImmediate(transformChild.gameObject);
                }
                else if (_node == node1)
                {
                    if (transformChild.name.ToLower().Contains("centermarkers"))
                    {
                        Object.DestroyImmediate(transformChild.gameObject);
                    }
                }
            }
        }
        #endregion


        #region "Utility"
        /// <summary> Attach other spline to PiggyBacks if not same spline and setup </summary>
        public void UpdateRoads()
        {
            if (!isSameSpline)
            {
                SplineC[] piggys = new SplineC[1];
                piggys[0] = node2.spline;
                node1.spline.road.PiggyBacks = piggys;
                node1.spline.TriggerSetup();
            }
            else
            {
                node1.spline.TriggerSetup();
            }
        }


        public void ConstructBoundsRect()
        {
            boundsRect = null;
            boundsRect = new Construction2DRect(new Vector2(cornerRR.x, cornerRR.z), new Vector2(cornerRL.x, cornerRL.z), new Vector2(cornerLR.x, cornerLR.z), new Vector2(cornerLL.x, cornerLL.z));
        }


        /// <summary> Creates boundsRect if null and returns true if _vector is inside the rect </summary>
        public bool Contains(ref Vector3 _vector)
        {
            Vector2 vector2D = new Vector2(_vector.x, _vector.z);
            if (boundsRect == null)
            {
                ConstructBoundsRect();
            }
            return boundsRect.Contains(ref vector2D);
        }


        private bool ContainsLineOld(Vector3 _vector1, Vector3 _vector2, int _lineDef = 30)
        {
            int MaxDef = _lineDef;
            float MaxDefF = (float)MaxDef;

            Vector3[] tVects = new Vector3[MaxDef];

            tVects[0] = _vector1;
            float mMod = 0f;
            float fcounter = 1f;
            for (int index = 1; index < (MaxDef - 1); index++)
            {
                mMod = fcounter / MaxDefF;
                tVects[index] = ((_vector2 - _vector1) * mMod) + _vector1;
                fcounter += 1f;
            }
            tVects[MaxDef - 1] = _vector2;

            Vector2 xVect = default(Vector2);
            for (int index = 0; index < MaxDef; index++)
            {
                xVect = new Vector2(tVects[index].x, tVects[index].z);
                if (boundsRect.Contains(ref xVect))
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary> Returns true when the Vectors or the line between them are inside the intersection </summary>
        public bool ContainsLine(Vector3 _vector1, Vector3 _vector2)
        {
            Vector2 tVectStart = new Vector2(_vector1.x, _vector1.z);
            Vector2 tVectEnd = new Vector2(_vector2.x, _vector2.z);
            bool bIntersects = Intersects2D(ref tVectStart, ref tVectEnd, ref cornerRR2D, ref cornerRL2D);
            if (bIntersects)
            {
                return true;
            }
            bIntersects = Intersects2D(ref tVectStart, ref tVectEnd, ref cornerRL2D, ref cornerLL2D);
            if (bIntersects)
            {
                return true;
            }
            bIntersects = Intersects2D(ref tVectStart, ref tVectEnd, ref cornerLL2D, ref cornerLR2D);
            if (bIntersects)
            {
                return true;
            }
            bIntersects = Intersects2D(ref tVectStart, ref tVectEnd, ref cornerLR2D, ref cornerRR2D);
            return bIntersects;
        }


        // Returns true if the lines intersect, otherwise false. If the lines
        // intersect, intersectionPoint holds the intersection point.
        private static bool Intersects2D(ref Vector2 _line1S, ref Vector2 _line1E, ref Vector2 _line2S, ref Vector2 _line2E)
        {
            float firstLineSlopeX, firstLineSlopeY, secondLineSlopeX, secondLineSlopeY;

            firstLineSlopeX = _line1E.x - _line1S.x;
            firstLineSlopeY = _line1E.y - _line1S.y;

            secondLineSlopeX = _line2E.x - _line2S.x;
            secondLineSlopeY = _line2E.y - _line2S.y;

            float s, t;
            s = (-firstLineSlopeY * (_line1S.x - _line2S.x) + firstLineSlopeX * (_line1S.y - _line2S.y)) / (-secondLineSlopeX * firstLineSlopeY + firstLineSlopeX * secondLineSlopeY);
            t = (secondLineSlopeX * (_line1S.y - _line2S.y) - secondLineSlopeY * (_line1S.x - _line2S.x)) / (-secondLineSlopeX * firstLineSlopeY + firstLineSlopeX * secondLineSlopeY);

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                return true;
            }
            // No collision
            return false;
        }
        #endregion


        #region "Gizmos"
        private void OnDrawGizmos()
        {
            if (!isDrawingGizmo)
            {
                return;
            }
            Gizmos.color = Color.blue;
            Gizmos.DrawCube(transform.position + new Vector3(0f, 5f, 0f), new Vector3(2f, 11f, 2f));
        }


        private void OnDrawGizmosSelected()
        {
            if (!isDrawingGizmo)
            {
                return;
            }
            Gizmos.color = new Color(0f, 0f, 0.7f);
            Gizmos.DrawCube(transform.position + new Vector3(0f, 6f, 0f), new Vector3(2.5f, 12f, 2.5f));
        }
        #endregion


        #region "Traffic light controlling"
        private void Start()
        {
            lightsRR.Setup(isLeftTurnYieldOnGreen);
            lightsRL.Setup(isLeftTurnYieldOnGreen);
            lightsLL.Setup(isLeftTurnYieldOnGreen);
            lightsLR.Setup(isLeftTurnYieldOnGreen);
            if (lightType == LightTypeEnum.Timed)
            {
                CreateFixedSequence();
                FixedTimeIncrement();
            }
            else
            {
                //Do your custom stuff
                //A Traffic addon could include sensor mode.
            }
        }


        private void CreateFixedSequence()
        {
            TrafficLightSequence SequenceMaker = null;
            fixedTimeSequenceList = new List<TrafficLightSequence>();
            if (roadType != RoadTypeEnum.NoTurnLane)
            {
                SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.LeftTurn, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeLeftTurnLightLength);
                fixedTimeSequenceList.Add(SequenceMaker);
            }
            if (roadType != RoadTypeEnum.NoTurnLane)
            {
                SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.LeftTurn, TrafficLightController.iLightSubStatusEnum.Yellow, fixedTimeYellowLightLength);
                fixedTimeSequenceList.Add(SequenceMaker);
            }
            SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.Red, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeAllRedLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.Regular, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeRegularLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.Regular, TrafficLightController.iLightSubStatusEnum.Yellow, fixedTimeYellowLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.Red, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeAllRedLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);

            if (roadType != RoadTypeEnum.NoTurnLane)
            {
                SequenceMaker = new TrafficLightSequence(false, TrafficLightController.iLightControllerEnum.LeftTurn, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeLeftTurnLightLength);
                fixedTimeSequenceList.Add(SequenceMaker);
            }
            if (roadType != RoadTypeEnum.NoTurnLane)
            {
                SequenceMaker = new TrafficLightSequence(false, TrafficLightController.iLightControllerEnum.LeftTurn, TrafficLightController.iLightSubStatusEnum.Yellow, fixedTimeYellowLightLength);
                fixedTimeSequenceList.Add(SequenceMaker);
            }
            SequenceMaker = new TrafficLightSequence(true, TrafficLightController.iLightControllerEnum.Red, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeAllRedLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(false, TrafficLightController.iLightControllerEnum.Regular, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeRegularLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(false, TrafficLightController.iLightControllerEnum.Regular, TrafficLightController.iLightSubStatusEnum.Yellow, fixedTimeYellowLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
            SequenceMaker = new TrafficLightSequence(false, TrafficLightController.iLightControllerEnum.Red, TrafficLightController.iLightSubStatusEnum.Green, fixedTimeAllRedLightLength);
            fixedTimeSequenceList.Add(SequenceMaker);
        }


        private IEnumerator TrafficLightFixedUpdate(float _time)
        {
            yield return new WaitForSeconds(_time);
            FixedTimeIncrement();
        }


        /// <summary> Executes the next traffic light sequence and shedules a new increment </summary>
        private void FixedTimeIncrement()
        {
            TrafficLightSequence SequenceMaker = fixedTimeSequenceList[fixedTimeIndex];
            fixedTimeIndex += 1;
            if (fixedTimeIndex > (fixedTimeSequenceList.Count - 1))
            {
                fixedTimeIndex = 0;
            }

            TrafficLightController lights1 = null;
            TrafficLightController lights2 = null;

            TrafficLightController lightsOuter1 = null;
            TrafficLightController lightsOuter2 = null;

            if (SequenceMaker.isLightMasterPath1)
            {
                lights1 = lightsRL;
                lights2 = lightsLR;

                if (isFlipped)
                {
                    lightsOuter1 = lightsRR;
                    lightsOuter2 = lightsLL;
                }
                else
                {
                    lightsOuter1 = lightsRR;
                    lightsOuter2 = lightsLL;
                }
            }
            else
            {
                if (isFlipped)
                {
                    lights1 = lightsRR;
                    lights2 = lightsLL;
                }
                else
                {
                    lights1 = lightsRR;
                    lights2 = lightsLL;
                }

                lightsOuter1 = lightsRL;
                lightsOuter2 = lightsLR;
            }

            TrafficLightController.iLightControllerEnum LCE = SequenceMaker.lightController;
            TrafficLightController.iLightSubStatusEnum LCESub = SequenceMaker.lightSubcontroller;

            if (LCE == TrafficLightController.iLightControllerEnum.Regular)
            {
                lights1.UpdateLights(TrafficLightController.iLightStatusEnum.Regular, LCESub, isLightsEnabled);
                lights2.UpdateLights(TrafficLightController.iLightStatusEnum.Regular, LCESub, isLightsEnabled);
                lightsOuter1.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
                lightsOuter2.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
            }
            else if (LCE == TrafficLightController.iLightControllerEnum.LeftTurn)
            {
                lights1.UpdateLights(TrafficLightController.iLightStatusEnum.LeftTurn, LCESub, isLightsEnabled);
                lights2.UpdateLights(TrafficLightController.iLightStatusEnum.LeftTurn, LCESub, isLightsEnabled);
                lightsOuter1.UpdateLights(TrafficLightController.iLightStatusEnum.RightTurn, LCESub, isLightsEnabled);
                lightsOuter2.UpdateLights(TrafficLightController.iLightStatusEnum.RightTurn, LCESub, isLightsEnabled);
            }
            else if (LCE == TrafficLightController.iLightControllerEnum.Red)
            {
                lights1.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
                lights2.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
                lightsOuter1.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
                lightsOuter2.UpdateLights(TrafficLightController.iLightStatusEnum.Red, LCESub, isLightsEnabled);
            }

            //Debug.Log ("Starting: " + SMaker.ToString());
            StartCoroutine(TrafficLightFixedUpdate(SequenceMaker.time));
        }
        #endregion


        #region "Materials"
        public void ResetMaterialsAll()
        {
            ResetCenterMaterials(false);
            ResetExtStrechtedMaterials(false);
            ResetExtTiledMaterials(false);
            ResetLanesMaterials(false);
            UpdateMaterials();
        }


        public void ResetCenterMaterials(bool _isUpdate = true)
        {
            string lanesNumber = "-2L";
            lanesAmount = node1.spline.road.LaneTotal();
            if (lanesAmount == 4)
            {
                lanesNumber = "-4L";
            }
            else if (lanesAmount == 6)
            {
                lanesNumber = "-6L";
            }
            if (intersectionType == IntersectionTypeEnum.ThreeWay)
            {
                lanesNumber += "-3";
                if (node1.idOnSpline < 2 || node2.idOnSpline < 2)
                {
                    //if(isFirstSpecialFirst || isFirstSpecialLast)
                    //{
                    //Reverse if from node 0
                    //stands for "Center Reversed"
                    lanesNumber += "-crev";
                    //}
                }
            }

            string basePath = RoadEditorUtility.GetBasePath();

            if (roadType == RoadTypeEnum.BothTurnLanes)
            {
                markerCenter1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterCenter-Both" + lanesNumber + ".mat");
                markerCenter2 = null;
                markerCenter3 = null;
            }
            else if (roadType == RoadTypeEnum.TurnLane)
            {
                markerCenter1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterCenter-Left" + lanesNumber + ".mat");
                markerCenter2 = null;
                markerCenter3 = null;
            }
            else if (roadType == RoadTypeEnum.NoTurnLane)
            {
                markerCenter1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterCenter-None" + lanesNumber + ".mat");
                markerCenter2 = null;
                markerCenter3 = null;
            }
            if (_isUpdate)
            {
                UpdateMaterials();
            }
        }


        public void ResetExtStrechtedMaterials(bool _isUpdate = true)
        {
            string lanesNumber = "-2L";
            lanesAmount = node1.spline.road.LaneTotal();
            if (lanesAmount == 4)
            {
                lanesNumber = "-4L";
            }
            else if (lanesAmount == 6)
            {
                lanesNumber = "-6L";
            }

            string basePath = RoadEditorUtility.GetBasePath();

            if (roadType == RoadTypeEnum.BothTurnLanes)
            {
                markerExtStretch1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterStretch-Both" + lanesNumber + ".mat");
                markerExtStretch2 = null;
                markerExtStretch3 = null;
            }
            else if (roadType == RoadTypeEnum.TurnLane)
            {
                markerExtStretch1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterStretch-Left" + lanesNumber + ".mat");
                markerExtStretch2 = null;
                markerExtStretch3 = null;
            }
            else if (roadType == RoadTypeEnum.NoTurnLane)
            {
                markerExtStretch1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterStretch-None" + lanesNumber + ".mat");
                markerExtStretch2 = null;
                markerExtStretch3 = null;
            }
            if (_isUpdate)
            {
                UpdateMaterials();
            }
        }


        public void ResetExtTiledMaterials(bool _isUpdate = true)
        {
            string basePath = RoadEditorUtility.GetBasePath();

            if (roadType == RoadTypeEnum.BothTurnLanes)
            {
                markerExtTiled1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Road1.mat");
                markerExtTiled2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");
                markerExtTiled3 = null;
            }
            else if (roadType == RoadTypeEnum.TurnLane)
            {
                markerExtTiled1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Road1.mat");
                markerExtTiled2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");
                markerExtTiled3 = null;
            }
            else if (roadType == RoadTypeEnum.NoTurnLane)
            {
                markerExtTiled1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Road1.mat");
                markerExtTiled2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");
                markerExtTiled3 = null;
            }
            if (_isUpdate)
            {
                UpdateMaterials();
            }
        }


        public void ResetLanesMaterials(bool _isUpdate = true)
        {
            string lanesNumber = "";
            lanesAmount = node1.spline.road.LaneTotal();
            if (lanesAmount == 4)
            {
                lanesNumber = "-4L";
            }
            else if (lanesAmount == 6)
            {
                lanesNumber = "-6L";
            }

            string basePath = RoadEditorUtility.GetBasePath();

            if (intersectionType == IntersectionTypeEnum.ThreeWay)
            {
                lane1Mat1Disabled = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabled.mat");
                lane1Mat2Disabled = null;
                if (roadType == RoadTypeEnum.BothTurnLanes)
                {
                    lane1Mat1DisabledActive = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabledOuterRR.mat");
                    lane1Mat2DisabledActive = null;
                    lane2Mat1Disabled = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabledR.mat");
                    lane2Mat2Disabled = null;
                }
                else
                {
                    lane2Mat1Disabled = null;
                    lane2Mat2Disabled = null;
                    lane2Mat1DisabledActive = null;
                    lane2Mat2DisabledActive = null;
                }
                lane2Mat1DisabledActive = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabledOuter" + lanesNumber + ".mat");
                lane2Mat2DisabledActive = null;
                if (roadType == RoadTypeEnum.BothTurnLanes)
                {
                    lane2Mat1DisabledActiveR = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabledOuterR.mat");
                    lane2Mat2DisabledActiveR = null;
                    lane3Mat1Disabled = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterLaneDisabledR.mat");
                    lane3Mat2Disabled = null;
                }
                else
                {
                    lane2Mat1DisabledActiveR = null;
                    lane2Mat2DisabledActiveR = null;
                    lane3Mat1Disabled = null;
                    lane3Mat2Disabled = null;
                }
            }
            else
            {
                lane1Mat1Disabled = null;
                lane1Mat2Disabled = null;
                lane2Mat1Disabled = null;
                lane2Mat2Disabled = null;
                lane2Mat1DisabledActive = null;
                lane2Mat2DisabledActive = null;
                lane2Mat1DisabledActiveR = null;
                lane2Mat2DisabledActiveR = null;
                lane3Mat1Disabled = null;
                lane3Mat2Disabled = null;
            }

            if (roadType == RoadTypeEnum.BothTurnLanes)
            {
                lane0Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteLYellowR" + lanesNumber + ".mat");
                lane0Mat2 = null;
                lane1Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterYellowLWhiteR.mat");
                lane1Mat2 = null;
                lane2Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteR" + lanesNumber + ".mat");
                lane2Mat2 = null;
                lane3Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteR.mat");
                lane3Mat2 = null;
            }
            else if (roadType == RoadTypeEnum.TurnLane)
            {
                lane0Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteLYellowR" + lanesNumber + ".mat");
                lane0Mat2 = null;
                lane1Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterYellowLWhiteR.mat");
                lane1Mat2 = null;
                lane2Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteR" + lanesNumber + ".mat");
                lane2Mat2 = null;
                lane3Mat1 = null;
                lane3Mat2 = null;
            }
            else if (roadType == RoadTypeEnum.NoTurnLane)
            {
                lane0Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterWhiteLYellowR" + lanesNumber + ".mat");
                lane0Mat2 = null;
                lane1Mat1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/InterYellowLWhiteR" + lanesNumber + ".mat");
                lane1Mat2 = null;
                lane2Mat1 = null;
                lane2Mat2 = null;
                lane3Mat1 = null;
                lane3Mat2 = null;
            }

            if (_isUpdate)
            {
                UpdateMaterials();
            }
        }


        private void ApplyMaterials(List<MeshRenderer> _meshRenderers, Material _material1 = null, Material _material2 = null, Material _material3 = null)
        {
            if (_meshRenderers != null && _meshRenderers.Count > 0)
            {
                List<Material> MarkerExtStretchMats = new List<Material>();

                if (_material1 != null)
                {
                    MarkerExtStretchMats.Add(_material1);
                    if (_material2 != null)
                    {
                        MarkerExtStretchMats.Add(_material2);
                        if (_material3 != null)
                        {
                            MarkerExtStretchMats.Add(_material3);
                        }
                    }
                }

                Material[] meshMaterials = MarkerExtStretchMats.ToArray();
                for (int i = 0; i < _meshRenderers.Count; i++)
                {
                    //By reference: the list was filled from the intersection's own serialized material fields, so it
                    //holds the author's ASSETS. Assigning .materials would instantiate a copy of each per renderer,
                    //leaving the junction answering to copies that are not assets, do not survive a domain reload,
                    //and are minted again on every rebuild.
                    _meshRenderers[i].sharedMaterials = meshMaterials;
                }
            }
        }


        public void UpdateMaterials()
        {
            int childCount = transform.childCount;
            List<MeshRenderer> extStretchMR = new List<MeshRenderer>();
            List<MeshRenderer> extTiledMR = new List<MeshRenderer>();
            MeshRenderer centerMR = null;
            List<MeshRenderer> lane0MR = new List<MeshRenderer>();
            List<MeshRenderer> lane1MR = new List<MeshRenderer>();
            List<MeshRenderer> lane2MR = new List<MeshRenderer>();
            List<MeshRenderer> lane3MR = new List<MeshRenderer>();
            List<MeshRenderer> laneD1MR = new List<MeshRenderer>();
            List<MeshRenderer> laneD3MR = new List<MeshRenderer>();
            List<MeshRenderer> laneDA2MR = new List<MeshRenderer>();
            List<MeshRenderer> laneDAR2MR = new List<MeshRenderer>();
            List<MeshRenderer> laneD2MR = new List<MeshRenderer>();
            List<MeshRenderer> laneDA1MR = new List<MeshRenderer>();

            MeshRenderer childMesh;
            string transformName = "";
            for (int i = 0; i < childCount; i++)
            {
                childMesh = transform.GetChild(i).GetComponent<MeshRenderer>();
                if(childMesh == null)
                {
                    continue;
                }
                
                transformName = childMesh.transform.name.ToLower();
                if (transformName.Contains("-stretchext"))
                {
                    extStretchMR.Add(childMesh);
                    continue;
                }
                if (transformName.Contains("-tiledext"))
                {
                    extTiledMR.Add(childMesh);
                    continue;
                }
                if (transformName.Contains("centermarkers"))
                {
                    centerMR = childMesh;
                    continue;
                }
                if (transformName.Contains("lane0"))
                {
                    lane0MR.Add(childMesh);
                    continue;
                }
                if (transformName.Contains("lane1"))
                {
                    lane1MR.Add(childMesh);
                    continue;
                }
                if (transformName.Contains("lane2"))
                {
                    lane2MR.Add(childMesh);
                    continue;
                }
                if (transformName.Contains("lane3"))
                {
                    lane3MR.Add(childMesh);
                    continue;
                }
                if (intersectionType == IntersectionTypeEnum.ThreeWay)
                {
                    if (transformName.Contains("laned1"))
                    {
                        laneD1MR.Add(childMesh);
                        continue;
                    }
                    if (transformName.Contains("laned3"))
                    {
                        laneD3MR.Add(childMesh);
                        continue;
                    }
                    if (transformName.Contains("laneda2"))
                    {
                        laneDA2MR.Add(childMesh);
                        continue;
                    }
                    if (transformName.Contains("lanedar2"))
                    {
                        laneDAR2MR.Add(childMesh);
                        continue;
                    }
                    if (transformName.Contains("laned2"))
                    {
                        laneD2MR.Add(childMesh);
                        continue;
                    }
                    if (transformName.Contains("laneda1"))
                    {
                        laneDA1MR.Add(childMesh);
                        continue;
                    }
                }
            }

            ApplyMaterials(extStretchMR, markerExtStretch1, markerExtStretch2, markerExtStretch3);
            ApplyMaterials(extTiledMR, markerExtTiled1, markerExtTiled2, markerExtTiled3);

            // Center only uses 1 mesh renderer
            if (centerMR != null)
            {
                List<Material> centerMats = new List<Material>();

                if (markerCenter1 != null)
                {
                    centerMats.Add(markerCenter1);
                    if (markerCenter2 != null)
                    {
                        centerMats.Add(markerCenter2);
                        if (markerCenter3 != null)
                        {
                            centerMats.Add(markerCenter3);
                        }
                    }
                }

                centerMR.sharedMaterials = centerMats.ToArray();
            }


            ApplyMaterials(lane0MR, lane0Mat1, lane0Mat2);
            ApplyMaterials(lane1MR, lane1Mat1, lane1Mat2);
            ApplyMaterials(lane2MR, lane2Mat1, lane2Mat2);
            ApplyMaterials(lane3MR, lane3Mat1, lane3Mat2);
            ApplyMaterials(laneD1MR, lane1Mat1Disabled, lane1Mat2Disabled);
            ApplyMaterials(laneD3MR, lane3Mat1Disabled, lane3Mat2Disabled);
            ApplyMaterials(laneDA2MR, lane2Mat1DisabledActive, lane2Mat2DisabledActive);
            ApplyMaterials(laneDAR2MR, lane2Mat1DisabledActiveR, lane2Mat2DisabledActiveR);
            ApplyMaterials(laneD2MR, lane2Mat1Disabled, lane2Mat2Disabled);
            ApplyMaterials(laneDA1MR, lane1Mat1DisabledActive, lane1Mat2DisabledActive);
        }
        #endregion


        #region "TrafficLights"
        public void ToggleTrafficLightPoleColor()
        {
            string basePath = RoadEditorUtility.GetBasePath();

            Material trafficLightMaterial = null;
            if (isTrafficLightGray)
            {
                trafficLightMaterial = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Signs/InterTLB2.mat");
            }
            else
            {
                trafficLightMaterial = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Signs/InterTLB1.mat");
            }
            int childCount = transform.childCount;
            string transformName = "";
            MeshRenderer MR = null;
            Material[] materials = new Material[1];
            materials[0] = trafficLightMaterial;
            for (int index = 0; index < childCount; index++)
            {
                transformName = transform.GetChild(index).name.ToLower();
                if (transformName.Contains("trafficlight"))
                {
                    MR = transform.GetChild(index).GetComponent<MeshRenderer>();
                    MR.sharedMaterials = materials;
                }
            }
        }


        public void TogglePointLights(bool _isLightsEnabled)
        {
            isLightsEnabled = _isLightsEnabled;
            int cCount = transform.childCount;
            Light[] fLights = null;
            Transform tTrans = null;
            for (int index = 0; index < cCount; index++)
            {
                if (transform.GetChild(index).name.ToLower().Contains("trafficlight"))
                {
                    tTrans = transform.GetChild(index);
                    int childCount = tTrans.childCount;
                    for (int k = 0; k < childCount; k++)
                    {
                        if (tTrans.GetChild(k).name.ToLower().Contains("streetlight"))
                        {
                            fLights = tTrans.GetChild(k).GetComponentsInChildren<Light>();
                            if (fLights != null)
                            {
                                for (int j = 0; j < fLights.Length; j++)
                                {
                                    fLights[j].enabled = isLightsEnabled;
                                    fLights[j].range = streetLightRange;
                                    fLights[j].intensity = streetLightIntensity;
                                    fLights[j].color = streetLightColor;
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }


        public void ResetStreetLightSettings()
        {
            streetLightRange = 30f;
            streetLightIntensity = 1f;
            streetLightColor = new Color(1f, 0.7451f, 0.27451f, 1f);
            TogglePointLights(isLightsEnabled);
        }
        #endregion
    }
}

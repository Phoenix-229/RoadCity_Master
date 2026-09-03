#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using RoadCityMaster.CrossSection;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The design vehicles this tool knows how to build a corner for. RM-97 owns this catalogue: nothing
    /// anywhere else in the tool held a vehicle dimension or a turning radius before it. </summary>
    public enum DesignVehicleKind
    {
        PassengerCar = 0,
        SingleUnitTruck = 1,
        CityTransitBus = 2,
        IntermediateSemitrailer = 3,
        InterstateSemitrailer = 4,
    }


    /// <summary> One design vehicle, with the dimensions and the two turning radii the standard publishes for it.
    ///
    /// Both radii are held because they answer different questions and because holding both makes the pair CHECKABLE for
    /// a SINGLE UNIT: the minimum turning radius is measured to the outer front wheel path, the minimum inside radius to
    /// the inner rear wheel path, and the swept-path relation R_i = sqrt(R_o^2 - L^2) - track relates them to within a
    /// third of a metre for the car, the truck and the bus. The gate asserts that agreement, which is how a mistyped
    /// digit in either column is caught -- neither number can be verified on its own, but the pair can.
    ///
    /// It is NOT asserted for a combination, and the reason is not tolerance: the tabulated turning radius of a
    /// semitrailer is the tractor's, while the sum of the wheelbases exceeds it, so the relation goes imaginary. That is
    /// the physical fact behind a semitrailer's published inside radius of 2.4 m -- the trailer cuts almost to the centre
    /// of the turn. <see cref="IsCombination"/> marks the vehicles the pair check must skip rather than fudge. </summary>
    public readonly struct DesignVehicle
    {
        public readonly DesignVehicleKind Kind;
        public readonly string Name;
        public readonly double LengthMetres;
        public readonly double WidthMetres;
        /// <summary> Effective wheelbase, metres: for a single unit the axle-to-axle distance, for a combination the sum
        /// of the tractor and trailer wheelbases, which is what the swept-path relation uses. </summary>
        public readonly double EffectiveWheelbaseMetres;
        public readonly double FrontOverhangMetres;
        /// <summary> Minimum design turning radius, metres, to the OUTER FRONT WHEEL path. </summary>
        public readonly double MinimumTurningRadiusMetres;
        /// <summary> Minimum inside radius, metres, to the INNER REAR WHEEL path. This is the number a kerb has to stay
        /// outside of, and it is why a semitrailer's corner is broad even though its turning radius is not much larger
        /// than a bus's -- the inside radius is what collapses. </summary>
        public readonly double MinimumInsideRadiusMetres;
        /// <summary> Minimum edge-of-pavement radius for a 90-degree turn built as a simple curve, metres, as the
        /// standard tabulates it for this vehicle. This is the corner radius at the reference deflection; other
        /// deflections are not read from a table but checked against the swept path directly -- see
        /// <see cref="IntersectionGeometry.SolveCorner"/>. </summary>
        public readonly double SimpleCurveCornerRadiusMetres;

        public DesignVehicle(DesignVehicleKind kind, string name, double lengthMetres, double widthMetres,
            double effectiveWheelbaseMetres, double frontOverhangMetres, double minimumTurningRadiusMetres,
            double minimumInsideRadiusMetres, double simpleCurveCornerRadiusMetres)
        {
            Kind = kind;
            Name = name;
            LengthMetres = lengthMetres;
            WidthMetres = widthMetres;
            EffectiveWheelbaseMetres = effectiveWheelbaseMetres;
            FrontOverhangMetres = frontOverhangMetres;
            MinimumTurningRadiusMetres = minimumTurningRadiusMetres;
            MinimumInsideRadiusMetres = minimumInsideRadiusMetres;
            SimpleCurveCornerRadiusMetres = simpleCurveCornerRadiusMetres;
        }

        /// <summary> True for a tractor-semitrailer, where the swept-path relation between the two published radii does
        /// not hold because the summed wheelbase exceeds the tractor's turning radius. </summary>
        public bool IsCombination
        {
            get
            {
                return Kind == DesignVehicleKind.IntermediateSemitrailer
                       || Kind == DesignVehicleKind.InterstateSemitrailer;
            }
        }

        /// <summary> The same vehicle as RM-96's curve-widening input. The bridge is here rather than a second vehicle
        /// table living in <see cref="CarriagewaySection"/>: RM-96 carries three dimensions because that is all the
        /// swept-path widening relation needs, and this is the one place they come from. </summary>
        public SweptPathVehicle ForSweptPath()
        {
            return new SweptPathVehicle(Name, WidthMetres, EffectiveWheelbaseMetres, FrontOverhangMetres);
        }
    }


    /// <summary> The catalogue. Metric figures as the standard publishes them; a vehicle is chosen, not authored, so
    /// there is exactly one decision behind every corner in a junction. </summary>
    public static class DesignVehicles
    {
        public static DesignVehicle For(DesignVehicleKind kind)
        {
            switch (kind)
            {
                case DesignVehicleKind.SingleUnitTruck:
                    return new DesignVehicle(kind, "SU-9 single-unit truck", 9.2, 2.4, 6.1, 1.2, 12.8, 8.6, 15.0);
                case DesignVehicleKind.CityTransitBus:
                    return new DesignVehicle(kind, "BUS-12 city transit bus", 12.2, 2.6, 7.6, 2.1, 12.8, 7.4, 15.0);
                case DesignVehicleKind.IntermediateSemitrailer:
                    return new DesignVehicle(kind, "WB-12 intermediate semitrailer", 13.9, 2.4, 12.2, 0.9, 12.2, 5.9,
                        18.0);
                case DesignVehicleKind.InterstateSemitrailer:
                    return new DesignVehicle(kind, "WB-20 interstate semitrailer", 22.4, 2.6, 18.9, 1.3, 13.7, 2.4,
                        30.0);
                default:
                    return new DesignVehicle(DesignVehicleKind.PassengerCar, "P passenger car", 5.8, 2.1, 3.4, 0.9,
                        7.3, 4.2, 7.5);
            }
        }

        public static DesignVehicleKind[] All()
        {
            return new DesignVehicleKind[]
            {
                DesignVehicleKind.PassengerCar,
                DesignVehicleKind.SingleUnitTruck,
                DesignVehicleKind.CityTransitBus,
                DesignVehicleKind.IntermediateSemitrailer,
                DesignVehicleKind.InterstateSemitrailer,
            };
        }

        /// <summary> The design vehicle a road class is expected to accommodate. This is the single authored decision
        /// RM-97 reduces corner geometry to: a residential lane is built for a car and a freight route for a
        /// semitrailer, which is exactly why one corner is tight and the other broad. </summary>
        public static DesignVehicleKind ForRoadClass(Road.RoadClassEnum roadClass)
        {
            switch (roadClass)
            {
                case Road.RoadClassEnum.UrbanSingleLane:
                case Road.RoadClassEnum.OneWayStreet:
                case Road.RoadClassEnum.ServiceLane:
                case Road.RoadClassEnum.PedestrianPath:
                    return DesignVehicleKind.PassengerCar;
                case Road.RoadClassEnum.UrbanTwoLane:
                case Road.RoadClassEnum.UrbanFourLane:
                case Road.RoadClassEnum.Collector:
                    return DesignVehicleKind.SingleUnitTruck;
                case Road.RoadClassEnum.Arterial:
                    return DesignVehicleKind.CityTransitBus;
                case Road.RoadClassEnum.HighwayDual:
                case Road.RoadClassEnum.Motorway:
                    return DesignVehicleKind.InterstateSemitrailer;
                default:
                    return DesignVehicleKind.PassengerCar;
            }
        }
    }


    /// <summary> The figures the standard publishes for intersection layout, held as data so a project can hold its own
    /// adopted values without a second copy of the arithmetic. Everything here is a QUOTED figure; every derived length
    /// in <see cref="IntersectionGeometry"/> is computed from these and never tabulated. </summary>
    [Serializable]
    public sealed class IntersectionGeometryClass
    {
        // ---- Skew ------------------------------------------------------------------------------------------------
        /// <summary> The smallest crossing angle the standard accepts, degrees, measured between the two centrelines.
        /// Below this a junction is refused rather than built folded -- RM-55's fail-closed principle. </summary>
        public double minimumCrossingAngleDegrees = 60.0;
        public double preferredCrossingAngleDegrees = 90.0;
        public string crossingAngleSource =
            "AASHTO Green Book, intersection alignment: intersections should be as near right angles as practical, " +
            "and an angle of less than 60 degrees should be avoided.";

        // ---- Intersection sight distance -------------------------------------------------------------------------
        /// <summary> Where the stopped driver's eye sits, metres back from the edge of the major travelled way. This is
        /// the vertex of both sight triangles. </summary>
        public double decisionPointSetbackMetres = 4.4;
        public double eyeHeightMetres = 1.08;
        public double objectHeightMetres = 1.08;
        /// <summary> Gap accepted by a passenger car turning LEFT from a stop onto a two-lane major road, seconds. </summary>
        public double leftTurnGapSeconds = 7.5;
        /// <summary> Gap accepted turning RIGHT from a stop, seconds. </summary>
        public double rightTurnGapSeconds = 6.5;
        /// <summary> Gap accepted crossing the major road from a stop, seconds. </summary>
        public double crossingGapSeconds = 6.5;
        /// <summary> Added per lane crossed beyond the tabulated two, seconds. </summary>
        public double additionalLaneGapSeconds = 0.5;
        public double referenceLanesCrossed = 2.0;
        public double singleUnitTruckGapAdditionSeconds = 1.0;
        public double combinationTruckGapAdditionSeconds = 2.0;
        /// <summary> Travel time each leg of an APPROACH sight triangle covers at an uncontrolled junction, seconds. </summary>
        public double approachSightTriangleSeconds = 3.0;
        /// <summary> Metres travelled per second per km/h -- the 1/3.6 conversion the standard writes as 0.278. </summary>
        public double speedToMetresPerSecond = 0.278;

        // ---- Turn lanes and flares -------------------------------------------------------------------------------
        public double storedCarLengthMetres = 7.6;
        public double storedTruckLengthMetres = 13.7;
        /// <summary> Storage is built for twice the average arrivals per cycle so a random peak does not spill. </summary>
        public double storageCycleFactor = 2.0;
        public double minimumStorageLengthMetres = 15.0;

        public static IntersectionGeometryClass Default()
        {
            return new IntersectionGeometryClass();
        }
    }


    /// <summary> One kerb corner, solved. Every length here is derived from the radius and the deflection by plane
    /// trigonometry, so there is no corner table to fall out of step with the vehicle catalogue. </summary>
    public readonly struct CornerSolution
    {
        public readonly DesignVehicleKind Vehicle;
        public readonly string VehicleName;
        /// <summary> The angle between the two centrelines, degrees, as the author built it. </summary>
        public readonly double CrossingAngleDegrees;
        /// <summary> True for the corner on the ACUTE side of a skewed crossing -- the one that turns through more than
        /// a right angle and whose tangent length runs away as the crossing sharpens. </summary>
        public readonly bool AcuteSide;
        /// <summary> How far the kerb turns through at this corner, degrees: 180 - crossing on the acute side, the
        /// crossing angle itself on the obtuse side. At a square crossing both are 90 and the four corners agree. </summary>
        public readonly double DeflectionDegrees;
        public readonly double RadiusMetres;
        /// <summary> Distance from the kerb-line intersection to each tangent point, metres: R*tan(deflection/2). </summary>
        public readonly double TangentLengthMetres;
        public readonly double MiddleOrdinateMetres;
        public readonly double ExternalDistanceMetres;
        public readonly double ArcLengthMetres;
        /// <summary> What the design vehicle needs here, metres. </summary>
        public readonly double RequiredRadiusMetres;
        /// <summary> The inner rear wheel path radius implied by the vehicle's own two published radii through the
        /// swept-path relation, metres. Carried so a report can show WHY the requirement is what it is. </summary>
        public readonly double SweptInnerRadiusMetres;
        public readonly bool Adequate;
        /// <summary> How far short the built radius falls, metres; zero when adequate. </summary>
        public readonly double ShortfallMetres;

        public CornerSolution(DesignVehicleKind vehicle, string vehicleName, double crossingAngleDegrees,
            bool acuteSide, double deflectionDegrees, double radiusMetres, double tangentLengthMetres,
            double middleOrdinateMetres, double externalDistanceMetres, double arcLengthMetres,
            double requiredRadiusMetres, double sweptInnerRadiusMetres)
        {
            Vehicle = vehicle;
            VehicleName = vehicleName;
            CrossingAngleDegrees = crossingAngleDegrees;
            AcuteSide = acuteSide;
            DeflectionDegrees = deflectionDegrees;
            RadiusMetres = radiusMetres;
            TangentLengthMetres = tangentLengthMetres;
            MiddleOrdinateMetres = middleOrdinateMetres;
            ExternalDistanceMetres = externalDistanceMetres;
            ArcLengthMetres = arcLengthMetres;
            RequiredRadiusMetres = requiredRadiusMetres;
            SweptInnerRadiusMetres = sweptInnerRadiusMetres;
            Adequate = radiusMetres >= requiredRadiusMetres - 1e-9;
            ShortfallMetres = Adequate ? 0.0 : requiredRadiusMetres - radiusMetres;
        }
    }


    /// <summary> How the corner-cut plate meets the two legs.
    ///
    /// The two corners that sit on one kerb line of the main road are separated along that kerb by
    /// 2*w_cross/sin(theta), because the crossing road's two kerb lines cut it at that spacing. The two corner arcs
    /// consume R*tan(deflection/2) each, and since the deflections are theta and 180-theta those tangent lengths are
    /// R*tan(theta/2) and R/tan(theta/2), which sum to exactly 2R/sin(theta). The sin(theta) cancels: the plate closes
    /// with no fold if and only if R &lt;= w_cross, WHATEVER the skew. That is a derived result and it is worth stating,
    /// because it is not what the legacy proximity search assumed.
    ///
    /// Skew still breaks a plate -- just not by folding it. The two tangent lengths on one kerb are equal only at a
    /// square crossing; at any skew they differ by 2R*cos(theta)/sin(theta), and a plate built with ONE setback for all
    /// four corners (which is what a nearest-point search with a fixed threshold produces) leaves exactly that gap on
    /// the obtuse side. <see cref="FixedSetbackGapMetres"/> is that number. </summary>
    public readonly struct CornerPlateFit
    {
        public readonly double CrossingAngleDegrees;
        public readonly double MainHalfWidthMetres;
        public readonly double CrossHalfWidthMetres;
        public readonly double RadiusMetres;
        /// <summary> Kerb length available between the two corners on one side of the main road, metres. </summary>
        public readonly double AvailableKerbLengthMetres;
        /// <summary> Kerb length the two corner arcs consume, metres: 2R/sin(theta). </summary>
        public readonly double RequiredKerbLengthMetres;
        public readonly double AcuteTangentLengthMetres;
        public readonly double ObtuseTangentLengthMetres;
        /// <summary> What a single-setback plate would gap by on the obtuse corner, metres. Zero only at 90 degrees. </summary>
        public readonly double FixedSetbackGapMetres;
        public readonly bool Meets;
        public readonly double OverlapMetres;

        public CornerPlateFit(double crossingAngleDegrees, double mainHalfWidthMetres, double crossHalfWidthMetres,
            double radiusMetres, double availableKerbLengthMetres, double requiredKerbLengthMetres,
            double acuteTangentLengthMetres, double obtuseTangentLengthMetres)
        {
            CrossingAngleDegrees = crossingAngleDegrees;
            MainHalfWidthMetres = mainHalfWidthMetres;
            CrossHalfWidthMetres = crossHalfWidthMetres;
            RadiusMetres = radiusMetres;
            AvailableKerbLengthMetres = availableKerbLengthMetres;
            RequiredKerbLengthMetres = requiredKerbLengthMetres;
            AcuteTangentLengthMetres = acuteTangentLengthMetres;
            ObtuseTangentLengthMetres = obtuseTangentLengthMetres;
            FixedSetbackGapMetres = Math.Abs(acuteTangentLengthMetres - obtuseTangentLengthMetres);
            OverlapMetres = requiredKerbLengthMetres - availableKerbLengthMetres;
            Meets = OverlapMetres <= 1e-9;
        }
    }


    /// <summary> Whether a crossing angle is inside the range the adopted standard accepts, with the limit and its
    /// source carried so a refusal can quote them rather than assert a bare number. </summary>
    public readonly struct SkewVerdict
    {
        public readonly bool Accepted;
        /// <summary> The crossing angle folded into (0, 90]: theta and 180-theta are the same crossing. </summary>
        public readonly double CrossingAngleDegrees;
        public readonly double SkewFromPerpendicularDegrees;
        public readonly double MinimumCrossingAngleDegrees;
        public readonly string Source;
        public readonly string Reason;

        public SkewVerdict(bool accepted, double crossingAngleDegrees, double minimumCrossingAngleDegrees,
            string source, string reason)
        {
            Accepted = accepted;
            CrossingAngleDegrees = crossingAngleDegrees;
            SkewFromPerpendicularDegrees = 90.0 - crossingAngleDegrees;
            MinimumCrossingAngleDegrees = minimumCrossingAngleDegrees;
            Source = source;
            Reason = reason;
        }
    }


    /// <summary> Something standing near a corner that a driver has to see past, in the approach's own frame: the major
    /// road runs along X with the junction at the origin, and the stopped approach comes in from negative Y.
    ///
    /// Held BY NAME. The tool reports the object it found and the distance that has to be clear; it never moves an
    /// author's object -- see the INTERSECTION-SIGHT-TRIANGLE-BLOCKED entry in the manual references. </summary>
    public readonly struct SightObstruction
    {
        public readonly string Name;
        /// <summary> Metres along the major road from the junction; negative to the driver's left. </summary>
        public readonly double AlongMajorMetres;
        /// <summary> Metres from the major road centreline toward the stopped approach; 0 on the centreline, negative
        /// on the approach side. </summary>
        public readonly double TowardApproachMetres;
        public readonly double HeightMetres;

        public SightObstruction(string name, double alongMajorMetres, double towardApproachMetres, double heightMetres)
        {
            Name = string.IsNullOrEmpty(name) ? "(unnamed object)" : name;
            AlongMajorMetres = alongMajorMetres;
            TowardApproachMetres = towardApproachMetres;
            HeightMetres = heightMetres;
        }
    }


    /// <summary> The two departure sight triangles at one stop-controlled approach, and what stands in them.
    ///
    /// The legs are computed from the major road's design speed and the gap the standard says a driver of this vehicle
    /// accepts, so LOWERING THE DESIGN SPEED SHORTENS THE LEGS -- which is what the manual already offers as a remedy
    /// for a blocked triangle, and it is true here because nothing about the legs is authored. </summary>
    public readonly struct SightTriangleVerdict
    {
        public readonly double MajorDesignSpeedKmh;
        public readonly double DecisionSetbackMetres;
        /// <summary> Seconds of gap the turning movement needs, after the lane and vehicle adjustments. </summary>
        public readonly double LeftGapSeconds;
        public readonly double RightGapSeconds;
        /// <summary> Metres of major road that must be visible to the driver's left, metres. </summary>
        public readonly double LeftLegMetres;
        public readonly double RightLegMetres;
        public readonly bool Clear;
        /// <summary> Every object found inside a triangle, by name, in the order given. </summary>
        public readonly string[] BlockingNames;
        /// <summary> For each blocking object, the leg length that has to be clear, metres. </summary>
        public readonly double[] BlockingRequiredClearMetres;
        /// <summary> Which side each blocking object stands on: -1 left, +1 right. </summary>
        public readonly int[] BlockingSides;
        public readonly string Description;

        public SightTriangleVerdict(double majorDesignSpeedKmh, double decisionSetbackMetres, double leftGapSeconds,
            double rightGapSeconds, double leftLegMetres, double rightLegMetres, string[] blockingNames,
            double[] blockingRequiredClearMetres, int[] blockingSides, string description)
        {
            MajorDesignSpeedKmh = majorDesignSpeedKmh;
            DecisionSetbackMetres = decisionSetbackMetres;
            LeftGapSeconds = leftGapSeconds;
            RightGapSeconds = rightGapSeconds;
            LeftLegMetres = leftLegMetres;
            RightLegMetres = rightLegMetres;
            BlockingNames = blockingNames ?? new string[0];
            BlockingRequiredClearMetres = blockingRequiredClearMetres ?? new double[0];
            BlockingSides = blockingSides ?? new int[0];
            Clear = BlockingNames.Length == 0;
            Description = description;
        }
    }


    /// <summary> RM-97: intersection geometry derived from a design vehicle instead of dialled.
    ///
    /// What this owns: the corner radius and every length that follows from it, the corner-cut plate's fit against both
    /// legs, the intersection sight triangles, the turn-bay storage length, and the skew limit with its source.
    ///
    /// What it deliberately does NOT own, and calls instead:
    ///   - the taper into a turn bay or a flare -- <see cref="LaneModel.TaperLengthMetres"/> is the one home of the
    ///     MUTCD taper relation, so a bay here is assembled through <see cref="AuxiliaryLane.TurnBay"/>;
    ///   - the kerb return the footway is built on -- <see cref="Sidewalk.KerbReturnRadiusMetres"/> already CONSUMES a
    ///     carriageway corner radius and was waiting for this to supply one;
    ///   - curve widening and the swept path across a section -- RM-96's <see cref="CarriagewaySection"/>, which takes
    ///     its vehicle from <see cref="DesignVehicle.ForSweptPath"/> so there is one vehicle table, not two.
    ///
    /// The legacy corner code in RoadCreationT found corners by a nearest-point proximity search between the two nodes'
    /// construction edges, with fudge thresholds and a 1.25 distance scale. It never held a radius at all, which is why
    /// nothing here is a rewrite of it: there was no relation to preserve. </summary>
    public static class IntersectionGeometry
    {
        private const double DegreesToRadians = Math.PI / 180.0;

        private static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        /// <summary> Folds a crossing angle into (0, 90]. A crossing at 120 degrees IS a crossing at 60 degrees: the two
        /// centrelines make both angles at once, and the standard's limit is on the acute one. </summary>
        public static double NormalisedCrossingAngleDegrees(double crossingAngleDegrees)
        {
            double a = crossingAngleDegrees % 180.0;
            if (a < 0.0) { a += 180.0; }
            if (a > 90.0) { a = 180.0 - a; }
            return a;
        }

        /// <summary> The skew gate. Refuses rather than building folded geometry, and quotes the limit's source so the
        /// refusal is checkable by a reader who has the standard open. </summary>
        public static SkewVerdict CheckSkew(double crossingAngleDegrees, IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            double a = NormalisedCrossingAngleDegrees(crossingAngleDegrees);
            bool accepted = a >= cls.minimumCrossingAngleDegrees - 1e-9;
            string reason = accepted
                ? Fmt("Crossing angle {0:0.##} deg is within the accepted range (>= {1:0.##} deg); skew from " +
                      "perpendicular is {2:0.##} deg.", a, cls.minimumCrossingAngleDegrees, 90.0 - a)
                : Fmt("Crossing angle {0:0.##} deg is below the accepted minimum of {1:0.##} deg. The junction was " +
                      "refused rather than built folded. Source: {2}", a, cls.minimumCrossingAngleDegrees,
                      cls.crossingAngleSource);
            return new SkewVerdict(accepted, a, cls.minimumCrossingAngleDegrees, cls.crossingAngleSource, reason);
        }

        /// <summary> How far the kerb turns through at one corner. The acute-side corner turns through more than a right
        /// angle, and this is the number the legacy fixed-setback plate had no way to know. </summary>
        public static double CornerDeflectionDegrees(double crossingAngleDegrees, bool acuteSide)
        {
            double a = NormalisedCrossingAngleDegrees(crossingAngleDegrees);
            return acuteSide ? 180.0 - a : a;
        }

        /// <summary> The inner rear wheel path radius implied by a vehicle's own dimensions, metres.
        ///
        /// R_i = sqrt(R_o^2 - L^2) - track, where R_o is the minimum turning radius measured to the outer front wheel
        /// and L is the effective wheelbase. This is the same swept-path geometry RM-96 uses for curve widening, applied
        /// at the vehicle's tightest turn instead of at a highway radius.
        ///
        /// It is computed here rather than read from the catalogue so the catalogue's published inside radius becomes a
        /// CHECK on the transcription: the two agree to about a third of a metre for every SINGLE-UNIT design vehicle,
        /// and a mistyped digit in either column breaks that agreement. For a combination the relation goes imaginary --
        /// the summed wheelbase exceeds the tractor's turning radius -- and zero is returned rather than a NaN, so a
        /// caller that forgets <see cref="DesignVehicle.IsCombination"/> gets a visibly wrong number instead of a value
        /// that poisons a vertex buffer. </summary>
        public static double SweptInnerRadiusMetres(DesignVehicle vehicle)
        {
            double outer = vehicle.MinimumTurningRadiusMetres;
            double wheelbase = vehicle.EffectiveWheelbaseMetres;
            double inner = outer * outer - wheelbase * wheelbase;
            if (inner <= 0.0) { return 0.0; }
            double r = Math.Sqrt(inner) - vehicle.WidthMetres;
            return r < 0.0 ? 0.0 : r;
        }

        /// <summary> The corner radius the design vehicle requires at this deflection, metres.
        ///
        /// At the reference 90-degree deflection this is the vehicle's tabulated simple-curve edge-of-pavement radius.
        /// Away from 90 degrees nothing is read from a table: the requirement is scaled by the ratio of the chord the
        /// kerb has to carry, sin(deflection/2) / sin(45 deg), which is 1 at the reference, smaller for a gentler
        /// deflection and larger for a sharper one -- the direction the published angle-of-turn columns move in. The
        /// requirement is never allowed below the vehicle's own inside radius, because a kerb inside the inner rear wheel
        /// path is overrun no matter how gentle the deflection is. </summary>
        public static double RequiredCornerRadiusMetres(DesignVehicle vehicle, double deflectionDegrees)
        {
            double half = 0.5 * deflectionDegrees * DegreesToRadians;
            double reference = Math.Sin(45.0 * DegreesToRadians);
            double scale = Math.Sin(half) / reference;
            if (scale < 0.0) { scale = 0.0; }
            double required = vehicle.SimpleCurveCornerRadiusMetres * scale;
            double floorRadius = vehicle.MinimumInsideRadiusMetres;
            return required < floorRadius ? floorRadius : required;
        }

        /// <summary> Solve one corner, letting the design vehicle choose the radius. </summary>
        public static CornerSolution SolveCorner(DesignVehicleKind kind, double crossingAngleDegrees, bool acuteSide)
        {
            return SolveCorner(kind, crossingAngleDegrees, acuteSide, 0.0);
        }

        /// <summary> Solve one corner. Pass a positive <paramref name="authoredRadiusMetres"/> to CHECK a radius that is
        /// already built: the solution then carries both the built radius and the vehicle's requirement, which is what
        /// lets a report name both numbers instead of just failing. Pass zero to derive it. </summary>
        public static CornerSolution SolveCorner(DesignVehicleKind kind, double crossingAngleDegrees, bool acuteSide,
            double authoredRadiusMetres)
        {
            DesignVehicle vehicle = DesignVehicles.For(kind);
            double deflection = CornerDeflectionDegrees(crossingAngleDegrees, acuteSide);
            double required = RequiredCornerRadiusMetres(vehicle, deflection);
            double radius = authoredRadiusMetres > 0.0 ? authoredRadiusMetres : required;

            double half = 0.5 * deflection * DegreesToRadians;
            double cos = Math.Cos(half);
            double tangent;
            double external;
            if (cos <= 1e-9)
            {
                // A 180-degree deflection is a doubled-back leg, not a corner. The skew gate refuses long before this,
                // but a finite pair of numbers here keeps a report readable instead of printing infinity.
                tangent = double.PositiveInfinity;
                external = double.PositiveInfinity;
            }
            else
            {
                tangent = radius * Math.Tan(half);
                external = radius * (1.0 / cos - 1.0);
            }
            double middleOrdinate = radius * (1.0 - cos);
            double arc = radius * deflection * DegreesToRadians;

            return new CornerSolution(kind, vehicle.Name, NormalisedCrossingAngleDegrees(crossingAngleDegrees),
                acuteSide, deflection, radius, tangent, middleOrdinate, external, arc, required,
                SweptInnerRadiusMetres(vehicle));
        }

        /// <summary> All four corners of a two-road crossing, in the order acute, obtuse, acute, obtuse. At a square
        /// crossing the four agree; at any skew two of them turn through more than a right angle. Naming a different
        /// design vehicle moves all four, because every radius here comes from the catalogue. </summary>
        public static CornerSolution[] SolveCorners(DesignVehicleKind kind, double crossingAngleDegrees)
        {
            return new CornerSolution[]
            {
                SolveCorner(kind, crossingAngleDegrees, true),
                SolveCorner(kind, crossingAngleDegrees, false),
                SolveCorner(kind, crossingAngleDegrees, true),
                SolveCorner(kind, crossingAngleDegrees, false),
            };
        }

        /// <summary> Fit the corner-cut plate against the two corners that sit on ONE kerb line of the main road.
        ///
        /// Call it twice with the two half-widths swapped to check both roads' kerbs. The two tangent lengths returned
        /// are the setbacks the plate must use at each end -- they are equal only at a square crossing, and building one
        /// setback for all four corners is exactly the error <see cref="CornerPlateFit.FixedSetbackGapMetres"/>
        /// measures. </summary>
        public static CornerPlateFit FitPlate(double crossingAngleDegrees, double mainHalfWidthMetres,
            double crossHalfWidthMetres, double radiusMetres)
        {
            double theta = NormalisedCrossingAngleDegrees(crossingAngleDegrees);
            double sin = Math.Sin(theta * DegreesToRadians);
            double available = sin <= 1e-9 ? double.PositiveInfinity : 2.0 * crossHalfWidthMetres / sin;
            double required = sin <= 1e-9 ? double.PositiveInfinity : 2.0 * radiusMetres / sin;

            double halfObtuse = 0.5 * theta * DegreesToRadians;
            double halfAcute = 0.5 * (180.0 - theta) * DegreesToRadians;
            double tanObtuse = Math.Tan(halfObtuse);
            double obtuseTangent = radiusMetres * tanObtuse;
            double acuteTangent = tanObtuse <= 1e-12 ? double.PositiveInfinity : radiusMetres / tanObtuse;
            // Guard the identity rather than trusting it: tan(90 - theta/2) == 1/tan(theta/2).
            double acuteDirect = radiusMetres * Math.Tan(halfAcute);
            if (!double.IsInfinity(acuteTangent) && Math.Abs(acuteDirect - acuteTangent) < 1e-6)
            {
                acuteTangent = acuteDirect;
            }

            return new CornerPlateFit(theta, mainHalfWidthMetres, crossHalfWidthMetres, radiusMetres, available,
                required, acuteTangent, obtuseTangent);
        }

        /// <summary> The largest corner radius that still closes the plate on both kerbs, metres.
        ///
        /// Because the two tangent lengths on one kerb sum to 2R/sin(theta) and the two corners are 2w/sin(theta) apart,
        /// the skew cancels and the limit is simply the narrower road's half-width. This is returned as a function rather
        /// than left as a comment so a caller can clamp before building instead of discovering the fold in a mesh. </summary>
        public static double LargestClosingRadiusMetres(double mainHalfWidthMetres, double crossHalfWidthMetres)
        {
            return Math.Min(mainHalfWidthMetres, crossHalfWidthMetres);
        }

        /// <summary> Seconds of gap the standard says this vehicle's driver accepts, after the two published
        /// adjustments: half a second per lane crossed beyond the tabulated two, and a fixed addition for a truck --
        /// larger for a combination than for a single unit, because the whole unit has to clear. </summary>
        public static double GapSeconds(DesignVehicleKind kind, double lanesCrossed, bool turningLeft,
            IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            double gap = turningLeft ? cls.leftTurnGapSeconds : cls.rightTurnGapSeconds;
            double extraLanes = lanesCrossed - cls.referenceLanesCrossed;
            if (extraLanes > 0.0) { gap += cls.additionalLaneGapSeconds * extraLanes; }
            switch (kind)
            {
                case DesignVehicleKind.SingleUnitTruck:
                case DesignVehicleKind.CityTransitBus:
                    gap += cls.singleUnitTruckGapAdditionSeconds;
                    break;
                case DesignVehicleKind.IntermediateSemitrailer:
                case DesignVehicleKind.InterstateSemitrailer:
                    gap += cls.combinationTruckGapAdditionSeconds;
                    break;
            }
            return gap;
        }

        /// <summary> Metres of major road that has to be visible for a gap of this length at this speed:
        /// 0.278 * V * t. Halving the design speed halves the leg, which is why the manual's remedy of lowering the
        /// approach speed is a real remedy and not a platitude. </summary>
        public static double SightDistanceMetres(double majorDesignSpeedKmh, double gapSeconds,
            IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            double d = cls.speedToMetresPerSecond * majorDesignSpeedKmh * gapSeconds;
            return d < 0.0 ? 0.0 : d;
        }

        /// <summary> The leg of an APPROACH sight triangle at an uncontrolled junction, metres: what each driver covers
        /// in the standard's travel time at that leg's own design speed. </summary>
        public static double ApproachSightTriangleLegMetres(double designSpeedKmh, IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            return SightDistanceMetres(designSpeedKmh, cls.approachSightTriangleSeconds, cls);
        }

        /// <summary> Whether a point lies inside the triangle D-O-A, with points on an edge counted as inside because a
        /// sign flip must not be the difference between reporting an obstruction and hiding it. </summary>
        private static bool InsideTriangle(double px, double py, double ax, double ay, double bx, double by,
            double cx, double cy)
        {
            double d1 = (px - ax) * (by - ay) - (py - ay) * (bx - ax);
            double d2 = (px - bx) * (cy - by) - (py - by) * (cx - bx);
            double d3 = (px - cx) * (ay - cy) - (py - cy) * (ax - cx);
            bool anyNegative = d1 < -1e-9 || d2 < -1e-9 || d3 < -1e-9;
            bool anyPositive = d1 > 1e-9 || d2 > 1e-9 || d3 > 1e-9;
            return !(anyNegative && anyPositive);
        }

        /// <summary> Solve both departure sight triangles at one stop-controlled approach and list, BY NAME, everything
        /// standing in them.
        ///
        /// The frame: the major road runs along X with the junction at the origin, the stopped approach comes in from
        /// negative Y, and the driver's eye sits at (0, -setback). The left triangle is that eye, the origin, and the
        /// point one left-turn leg away in the negative X direction; the right triangle mirrors it.
        ///
        /// An object shorter than the standard's object height cannot break the sight line and is not reported -- a kerb
        /// is not an obstruction. Nothing is moved: the offending object's name and the distance that must be clear are
        /// returned for the caller to report. </summary>
        public static SightTriangleVerdict SolveSightTriangle(double majorDesignSpeedKmh, double lanesCrossed,
            DesignVehicleKind kind, IList<SightObstruction> obstructions, IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            double leftGap = GapSeconds(kind, lanesCrossed, true, cls);
            double rightGap = GapSeconds(kind, lanesCrossed, false, cls);
            double leftLeg = SightDistanceMetres(majorDesignSpeedKmh, leftGap, cls);
            double rightLeg = SightDistanceMetres(majorDesignSpeedKmh, rightGap, cls);
            double setback = cls.decisionPointSetbackMetres;

            List<string> names = new List<string>();
            List<double> required = new List<double>();
            List<int> sides = new List<int>();
            if (obstructions != null)
            {
                for (int i = 0; i < obstructions.Count; i++)
                {
                    SightObstruction o = obstructions[i];
                    if (o.HeightMetres < cls.objectHeightMetres) { continue; }
                    bool blocksLeft = InsideTriangle(o.AlongMajorMetres, o.TowardApproachMetres,
                        0.0, -setback, 0.0, 0.0, -leftLeg, 0.0);
                    bool blocksRight = InsideTriangle(o.AlongMajorMetres, o.TowardApproachMetres,
                        0.0, -setback, 0.0, 0.0, rightLeg, 0.0);
                    if (blocksLeft)
                    {
                        names.Add(o.Name);
                        required.Add(leftLeg);
                        sides.Add(-1);
                    }
                    else if (blocksRight)
                    {
                        names.Add(o.Name);
                        required.Add(rightLeg);
                        sides.Add(1);
                    }
                }
            }

            string description = Fmt(
                "Approach sight triangles at {0:0.##} km/h for the {1}: left leg {2:0.##} m ({3:0.##} s gap), " +
                "right leg {4:0.##} m ({5:0.##} s gap), driver eye {6:0.##} m back from the travelled way.",
                majorDesignSpeedKmh, DesignVehicles.For(kind).Name, leftLeg, leftGap, rightLeg, rightGap, setback);
            for (int i = 0; i < names.Count; i++)
            {
                description += Fmt("\n  BLOCKED by '{0}' in the {1} triangle; {2:0.##} m must be clear.",
                    names[i], sides[i] < 0 ? "left" : "right", required[i]);
            }
            if (names.Count == 0) { description += "\n  Both triangles are clear."; }

            return new SightTriangleVerdict(majorDesignSpeedKmh, setback, leftGap, rightGap, leftLeg, rightLeg,
                names.ToArray(), required.ToArray(), sides.ToArray(), description);
        }

        /// <summary> Queue storage a turn bay needs, metres: twice the average arrivals per cycle, each occupying the
        /// stored length of the mix of vehicles using it. Built for twice the average because arrivals are random and a
        /// bay that spills puts a stopped vehicle in a through lane. </summary>
        public static double TurnBayStorageLengthMetres(double vehiclesPerCycle, double truckFraction,
            IntersectionGeometryClass cls)
        {
            if (cls == null) { cls = IntersectionGeometryClass.Default(); }
            if (vehiclesPerCycle < 0.0) { vehiclesPerCycle = 0.0; }
            if (truckFraction < 0.0) { truckFraction = 0.0; }
            if (truckFraction > 1.0) { truckFraction = 1.0; }
            double perVehicle = (1.0 - truckFraction) * cls.storedCarLengthMetres
                                + truckFraction * cls.storedTruckLengthMetres;
            double storage = cls.storageCycleFactor * vehiclesPerCycle * perVehicle;
            return storage < cls.minimumStorageLengthMetres ? cls.minimumStorageLengthMetres : storage;
        }

        /// <summary> Assemble the turn bay. The storage length is RM-97's; the TAPER that opens it is not touched here --
        /// <see cref="AuxiliaryLane.TurnBay"/> takes the design speed and asks <see cref="LaneModel"/> for it, so the
        /// MUTCD taper relation stays in the one place that owns it. </summary>
        public static AuxiliaryLane TurnBay(int direction, double stopLineStation, double vehiclesPerCycle,
            double truckFraction, double laneWidthMetres, double designSpeedKmh, IntersectionGeometryClass cls)
        {
            double storage = TurnBayStorageLengthMetres(vehiclesPerCycle, truckFraction, cls);
            return AuxiliaryLane.TurnBay(direction, stopLineStation, storage, laneWidthMetres, designSpeedKmh);
        }

        /// <summary> Total length a flare occupies upstream of the stop line, metres: the storage plus the taper that
        /// opens it. The taper term is a CALL, never a second copy of the relation. </summary>
        public static double FlareLengthMetres(double vehiclesPerCycle, double truckFraction, double laneWidthMetres,
            double designSpeedKmh, IntersectionGeometryClass cls)
        {
            return TurnBayStorageLengthMetres(vehiclesPerCycle, truckFraction, cls)
                   + LaneModel.TaperLengthMetres(laneWidthMetres, designSpeedKmh);
        }

        /// <summary> The corner radius to hand the footway, metres. <see cref="Sidewalk.KerbReturnRadiusMetres"/> has
        /// always taken a carriageway corner radius as its argument and fallen back to an authored default because
        /// nothing in the tool computed one; this is the supply it was written for. </summary>
        public static double CarriagewayCornerRadiusForFootway(DesignVehicleKind kind, double crossingAngleDegrees,
            bool acuteSide)
        {
            return SolveCorner(kind, crossingAngleDegrees, acuteSide).RadiusMetres;
        }
    }
}

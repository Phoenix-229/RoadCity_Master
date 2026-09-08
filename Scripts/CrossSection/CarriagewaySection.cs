// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using RoadCityMaster.City;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> The three dimensions of a design vehicle that the swept-path relation needs. Turning radii and the
    /// vehicle CATALOGUE belong to RM-97, which owns design vehicles; this struct carries only what curve widening
    /// consumes, so the two tasks meet at an argument list rather than at two copies of a vehicle table. </summary>
    public readonly struct SweptPathVehicle
    {
        public readonly string Name;
        /// <summary> Track width on a tangent, metres: the out-to-out width of the tyres. </summary>
        public readonly double TrackWidthMetres;
        /// <summary> Wheelbase, metres, between the extreme axles. </summary>
        public readonly double WheelbaseMetres;
        /// <summary> Front overhang, metres, from the front axle to the front bumper. </summary>
        public readonly double FrontOverhangMetres;

        public SweptPathVehicle(string name, double trackWidthMetres, double wheelbaseMetres,
            double frontOverhangMetres)
        {
            Name = name;
            TrackWidthMetres = trackWidthMetres;
            WheelbaseMetres = wheelbaseMetres;
            FrontOverhangMetres = frontOverhangMetres;
        }
    }


    /// <summary> Whether a section drains, and to where. Returned rather than a bool because "this road does not
    /// drain" is not actionable and "the inside half is falling toward the median at 0.4%" is. </summary>
    public readonly struct DrainageVerdict
    {
        public readonly bool DrainsToBothEdges;
        public readonly double OuterRate;
        public readonly double InnerRate;

        public DrainageVerdict(bool drainsToBothEdges, double outerRate, double innerRate)
        {
            DrainsToBothEdges = drainsToBothEdges; OuterRate = outerRate; InnerRate = innerRate;
        }
    }


    /// <summary> The two cross-slopes that define a carriageway section at one station, each expressed as the rise per
    /// metre travelled OUTWARD from the axis of rotation toward that edge. A normal crown is therefore two NEGATIVE
    /// rates (both edges below the axis); full banking is a positive outer rate and a negative inner one of the same
    /// magnitude, which is a plane.
    ///
    /// Stated as rise-outward rather than as a signed world slope on purpose: it makes "does this drain" a question
    /// about two signs rather than about which side of the road you are standing on, and it is the same convention for
    /// a left-hand and a right-hand curve. </summary>
    public readonly struct SectionSlopes
    {
        public readonly double OuterRate;
        public readonly double InnerRate;

        public SectionSlopes(double outerRate, double innerRate)
        {
            OuterRate = outerRate; InnerRate = innerRate;
        }
    }


    /// <summary> Where the transition happens, in stations. Computed once from the relative-gradient relation and then
    /// consulted; a schedule rather than a set of lengths because every question the gate asks -- what is the slope
    /// here, is it continuous, does it reach full banking before the curve -- is a question about stations. </summary>
    public readonly struct TransitionSchedule
    {
        /// <summary> The superelevation runoff: crown-removed to full banking. </summary>
        public readonly double RunoffLengthMetres;
        /// <summary> The tangent runout: normal crown to crown-removed on the outer lane. </summary>
        public readonly double RunoutLengthMetres;
        public readonly double EntryRunoutStartStation;
        public readonly double EntryRunoffStartStation;
        public readonly double CurveStartStation;
        public readonly double CurveEndStation;
        public readonly double ExitRunoffEndStation;
        public readonly double ExitRunoutEndStation;

        public TransitionSchedule(double runoffLengthMetres, double runoutLengthMetres,
            double curveStartStation, double curveEndStation)
        {
            RunoffLengthMetres = runoffLengthMetres;
            RunoutLengthMetres = runoutLengthMetres;
            CurveStartStation = curveStartStation;
            CurveEndStation = curveEndStation;
            EntryRunoffStartStation = curveStartStation - runoffLengthMetres;
            EntryRunoutStartStation = curveStartStation - runoffLengthMetres - runoutLengthMetres;
            ExitRunoffEndStation = curveEndStation + runoffLengthMetres;
            ExitRunoutEndStation = curveEndStation + runoffLengthMetres + runoutLengthMetres;
        }

        /// <summary> The whole distance over which the section is changing, both transitions and the curve. </summary>
        public double TotalTransitionLengthMetres
        {
            get { return 2.0 * (RunoffLengthMetres + RunoutLengthMetres); }
        }
    }


    /// <summary> RM-96's design constants for the carriageway section. Everything derivable is derived, and the gate
    /// proves the derivation reproduces the standard's published table; the two genuinely tabulated figures say so in
    /// their own doc comments and are checked for the shape the published table has, which is what catches a
    /// transcription slip.
    ///
    /// double throughout, for RM-40's reason: a tabulated lookup must return the published figure exactly, and
    /// float 0.44f is 0.43999999. </summary>
    [Serializable]
    public sealed class CarriagewaySectionClass
    {
        /// <summary> Design speeds the relative-gradient limit is tabulated at, km/h. This table starts at 20 and runs
        /// to 130 -- a wider span than the side-friction table in RM-40, because the published relative-gradient table
        /// covers those speeds and truncating it here would silently clamp a 130 km/h motorway to the 120 figure. </summary>
        public double[] tabulatedSpeedsKmh = { 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130 };

        /// <summary> The maximum relative gradient between the pavement edge and the axis of rotation, PERCENT, one per
        /// tabulated speed. TRANSCRIBED from the published table -- it is a judgement about what a driver tolerates and
        /// is not derivable from anything. It is checked instead for the shape the published table has: strictly
        /// decreasing with speed, in two straight segments (0.05 per 10 km/h up to 80 km/h, 0.03 per 10 km/h after),
        /// which is how a mistyped digit is caught. This is the number that makes runoff length depend on lane width
        /// and lane count, and it is the reason real roads begin banking well before the curve. </summary>
        public double[] maximumRelativeGradientPercent =
            { 0.80, 0.75, 0.70, 0.65, 0.60, 0.55, 0.50, 0.47, 0.44, 0.41, 0.38, 0.35 };

        /// <summary> The lane-count values the published runoff adjustment factor is tabulated at (half-lane steps,
        /// because a two-lane road rotated about its centreline rotates 1.5 lanes on each side of an offset axis). </summary>
        public double[] publishedLanesRotated = { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5 };

        /// <summary> The published runoff adjustment factor b_w at each of those lane counts. NOT a transcribed table in
        /// use -- <see cref="CarriagewaySection.LaneRotationFactor"/> derives it from
        /// b_w = (1 + 0.5(n - 1)) / n, and the gate asserts the derivation reproduces every one of these six values.
        /// They are here so that assertion can exist at all. </summary>
        public double[] publishedLaneRotationFactors = { 1.00, 0.83, 0.75, 0.70, 0.67, 0.64 };

        /// <summary> Lateral clearance per lane on a curve, metres: the gap between the vehicle's swept track and the
        /// lane edge that a driver needs on a curve. The published value scales with lane width (0.6 m for 3.0 m lanes,
        /// 0.75 m for 3.3 m, 0.9 m for 3.6 m); <see cref="CarriagewaySection.LateralClearanceMetres"/> interpolates it
        /// from the lane width rather than fixing one, so a narrow street and a motorway do not share a clearance. </summary>
        public double lateralClearanceAtThreeMetreLane = 0.6;

        /// <summary> Lane width, metres, at which the lateral clearance above is stated. </summary>
        public double lateralClearanceReferenceLaneWidth = 3.0;

        /// <summary> Metres of extra clearance per metre of lane width above the reference: from the published pairs
        /// (3.0, 0.6), (3.3, 0.75), (3.6, 0.9) the rate is 0.15 / 0.3 = 0.5, exactly. </summary>
        public double lateralClearancePerMetreOfLane = 0.5;

        /// <summary> The coefficient in the extra-width allowance for the difficulty of driving on a curve,
        /// Z = k V / sqrt(R), with V in km/h and R in metres. The published relation uses k = 0.1. </summary>
        public double drivingDifficultyCoefficient = 0.1;

        /// <summary> Widening below this is disregarded, metres. The published rule: a widening of less than 0.6 m has
        /// little value and is not worth the construction difficulty, so it is dropped rather than built. </summary>
        public double minimumWideningMetres = 0.6;

        /// <summary> The design vehicle curve widening is computed for. RM-97 owns the catalogue, so these four figures
        /// are ADOPTED from it rather than transcribed here -- see <see cref="AdoptDesignVehicle"/>. They stay as
        /// serialized fields so an author can still override one on a single road, but the shipped default is whatever
        /// the catalogue says the vehicle is, and it cannot drift away from it.
        ///
        /// This was a real drift, not a hypothetical one: these fields were first authored with a 2.6 m track width under
        /// the name "Single-unit truck", and the SU-9 in the catalogue is 2.4 m wide. Two copies of a vehicle agreed
        /// until one of them was looked up. </summary>
        public DesignVehicleKind designVehicleKind = DesignVehicleKind.SingleUnitTruck;
        public string designVehicleName = "SU-9 single-unit truck";
        public double vehicleTrackWidthMetres = 2.4;
        public double vehicleWheelbaseMetres = 6.1;
        public double vehicleFrontOverhangMetres = 1.2;

        /// <summary> Take the vehicle's name and its three swept-path figures from RM-97's catalogue. The track width
        /// used here is the vehicle's overall WIDTH, which is what the swept-path relation needs -- a design vehicle's
        /// published track and its overall width are the same figure for every vehicle in the catalogue. </summary>
        public void AdoptDesignVehicle(DesignVehicleKind kind)
        {
            DesignVehicle v = DesignVehicles.For(kind);
            designVehicleKind = kind;
            designVehicleName = v.Name;
            vehicleTrackWidthMetres = v.WidthMetres;
            vehicleWheelbaseMetres = v.EffectiveWheelbaseMetres;
            vehicleFrontOverhangMetres = v.FrontOverhangMetres;
        }

        public SweptPathVehicle Vehicle()
        {
            return new SweptPathVehicle(designVehicleName, vehicleTrackWidthMetres, vehicleWheelbaseMetres,
                vehicleFrontOverhangMetres);
        }

        public static CarriagewaySectionClass Default()
        {
            CarriagewaySectionClass cls = new CarriagewaySectionClass();
            cls.AdoptDesignVehicle(cls.designVehicleKind);
            return cls;
        }
    }


    /// <summary> RM-96's verifiable core: the carriageway's cross-section as a FUNCTION OF STATION rather than a
    /// constant. Three things the legacy code does not have, and one rule.
    ///
    ///   * A CROWN, taken from the surface type rather than restated here, so a gravel road drains at its own rate and
    ///     a sealed one at its own. A carriageway with no crown does not drain, and a viewer reads a flat,
    ///     mirror-uniform surface as painted ground even when they cannot say why.
    ///   * A RUNOFF AND RUNOUT LENGTH computed from the maximum relative gradient, so banking is developed over a real
    ///     distance instead of appearing at a node. Banking cannot be developed faster than a limiting relative
    ///     gradient between the pavement edge and the axis of rotation; faster than that the outer edge visibly climbs
    ///     and the author feels a lurch driving the result. That limit makes the length depend on lane width and lane
    ///     count, which is why a six-lane motorway begins banking much earlier than a two-lane road.
    ///   * CURVE WIDENING from the vehicle's swept-path relation, which is why a tight bend is wider than the straight
    ///     either side of it.
    ///
    /// And the rule, which is the part that keeps working after everyone has forgotten it:
    /// <see cref="SurfaceHeightAtOffsetMetres"/> is the ONLY place a height across the section is computed. Every kerb,
    /// marking, barrier and swept element reads it at its own lateral offset. The most common way a road tool decays is
    /// a kerb that computes its own tilt from an older copy of the same formula, so that after any fix the kerb and the
    /// surface disagree by a fraction of a degree and a hairline of background shows between them at grazing angles.
    /// One owner (RM-62) is what prevents that class of bug permanently rather than fixing its instances.
    ///
    /// What this does NOT own: the banking VALUE e, which is RM-40's `e + f = V^2/(127 R)` and is consumed here as an
    /// argument; the divided section's point layout, which is RM-59's <see cref="DividedRoad"/>; and the design-vehicle
    /// catalogue, which is RM-97's. </summary>
    public static class CarriagewaySection
    {
        // ---- the crown ----

        /// <summary> The normal cross-fall on a straight, as a decimal, read from the surface type. Nothing is restated
        /// here: a new surface asset changes the crown of every road built on it, which is the whole point of RM-41
        /// making surfaces assets instead of enum branches. </summary>
        public static double NormalCrossSlope(SurfaceTypeClass surface)
        {
            if (surface == null) { return 0.0; }
            return surface.crownCrossSlope;
        }

        // ---- the shoulder ----

        /// <summary> The gentlest cross-slope a paved shoulder may be given, as a decimal. AASHTO puts a paved
        /// shoulder between 2% and 6% and requires it be no flatter than the travelled way beside it: a shoulder
        /// flatter than the carriageway ponds the water the crown has just shed onto it. </summary>
        public const double ShoulderCrossSlopeMinimum = 0.02;

        /// <summary> The steepest cross-slope a paved shoulder may be given: 6%. Past that a vehicle pulling onto the
        /// shoulder leans hard enough for the driver to feel it snatch. </summary>
        public const double ShoulderCrossSlopeMaximum = 0.06;

        /// <summary> The cross-slope a shoulder is built at when nothing else is asked for: 4%, the usual figure for a
        /// bituminous shoulder and the middle of the permitted band. </summary>
        public const double ShoulderCrossSlopeNormal = 0.04;

        public const string ShoulderCrossSlopeSource =
            "AASHTO: a paved shoulder is cross-sloped 2% to 6% and never flatter than the travelled way it adjoins; "
            + "4% is the usual bituminous figure";

        /// <summary> How far the OUTER EDGE of a shoulder sits below the carriageway edge it starts from, in metres:
        /// the shoulder's own width taken down its own cross-slope. It is a drop, so it comes back positive and the
        /// caller subtracts it.
        ///
        /// This exists to replace hand-found constants. A drop is a slope over a width and nothing else, which is
        /// exactly why one fixed figure cannot serve two different shoulders: at the 4% normal slope a 2.5 m shoulder
        /// falls 0.10 m and a 1 m shoulder falls 0.04 m, so a constant fitted to the first is wrong for the second by
        /// more than the whole drop. A road with no shoulder has nothing to fall across and gets zero. </summary>
        public static double ShoulderFallMetres(double shoulderWidthMetres)
        {
            return ShoulderFallMetres(shoulderWidthMetres, ShoulderCrossSlopeNormal);
        }

        /// <summary> The same drop at an explicit cross-slope, clamped into the permitted band so a mistyped figure
        /// cannot flatten a shoulder into a pond or tip it past what a stopping vehicle will tolerate. The height
        /// itself still comes from <see cref="SurfaceHeightAtOffsetMetres"/>, so a shoulder edge cannot disagree with
        /// the surface it is attached to. </summary>
        public static double ShoulderFallMetres(double shoulderWidthMetres, double crossSlope)
        {
            if (!(shoulderWidthMetres > 0.0)) { return 0.0; }
            double slope = crossSlope;
            if (!(slope > 0.0)) { slope = ShoulderCrossSlopeNormal; }
            if (slope < ShoulderCrossSlopeMinimum) { slope = ShoulderCrossSlopeMinimum; }
            if (slope > ShoulderCrossSlopeMaximum) { slope = ShoulderCrossSlopeMaximum; }
            return SurfaceHeightAtOffsetMetres(shoulderWidthMetres, new SectionSlopes(slope, slope));
        }

        // ---- the runoff and runout lengths ----

        /// <summary> The maximum relative gradient at a design speed, percent, interpolated between tabulated speeds and
        /// held flat outside the table's range. Interpolated rather than snapped because a design speed of 95 km/h is a
        /// legitimate thing to author and snapping it to 90 would hand back a longer runoff than the standard
        /// permits. </summary>
        public static double MaximumRelativeGradientPercent(double designSpeedKmh, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            double[] speeds = cls.tabulatedSpeedsKmh;
            double[] gradients = cls.maximumRelativeGradientPercent;
            int n = Math.Min(speeds.Length, gradients.Length);
            if (n == 0) { return 0.0; }
            if (designSpeedKmh <= speeds[0]) { return gradients[0]; }
            if (designSpeedKmh >= speeds[n - 1]) { return gradients[n - 1]; }
            for (int i = 1; i < n; i++)
            {
                if (designSpeedKmh > speeds[i]) { continue; }
                double span = speeds[i] - speeds[i - 1];
                if (span <= 0.0) { return gradients[i]; }
                double t = (designSpeedKmh - speeds[i - 1]) / span;
                return gradients[i - 1] + (t * (gradients[i] - gradients[i - 1]));
            }
            return gradients[n - 1];
        }

        /// <summary> The runoff adjustment factor b_w for the number of lanes rotated, DERIVED from
        /// b_w = (1 + 0.5(n - 1)) / n. It is below 1 for more than one lane because the outermost lane of a wide
        /// rotation does not have to obey the relative-gradient limit on its own: the limit applies to the average, so a
        /// six-lane rotation is permitted to develop faster per lane than a single lane would be. </summary>
        public static double LaneRotationFactor(double lanesRotated)
        {
            if (lanesRotated <= 0.0) { return 1.0; }
            return (1.0 + (0.5 * (lanesRotated - 1.0))) / lanesRotated;
        }

        /// <summary> The superelevation runoff length, metres: L_r = (w n1 e_d / delta) b_w, where w is the lane width,
        /// n1 the number of lanes rotated, e_d the design superelevation as a decimal, delta the maximum relative
        /// gradient as a decimal, and b_w the lane adjustment. This is the length over which the outer edge climbs from
        /// crown-removed to full banking, and it is the single number that separates a road from a road-shaped
        /// ribbon. </summary>
        public static double SuperelevationRunoffLengthMetres(double laneWidthMetres, double lanesRotated,
            double designSuperelevation, double designSpeedKmh, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            if (designSuperelevation <= 0.0 || laneWidthMetres <= 0.0 || lanesRotated <= 0.0) { return 0.0; }
            double deltaPercent = MaximumRelativeGradientPercent(designSpeedKmh, cls);
            if (deltaPercent <= 0.0) { return 0.0; }
            double delta = deltaPercent / 100.0;
            double rotatedWidth = laneWidthMetres * lanesRotated;
            return (rotatedWidth * designSuperelevation / delta) * LaneRotationFactor(lanesRotated);
        }

        /// <summary> The tangent runout length, metres: L_t = (e_NC / e_d) L_r. It follows from the runoff rather than
        /// being a second authored length, because both develop at the same relative gradient -- the runout is simply
        /// the part of that same straight edge-profile line that lies between normal crown and zero. That identity is
        /// what makes the outer edge's whole transition ONE straight line, which the gate checks. </summary>
        public static double TangentRunoutLengthMetres(double normalCrown, double designSuperelevation,
            double runoffLengthMetres)
        {
            if (designSuperelevation <= 0.0 || runoffLengthMetres <= 0.0) { return 0.0; }
            return (normalCrown / designSuperelevation) * runoffLengthMetres;
        }

        /// <summary> The whole transition schedule around one curve. </summary>
        public static TransitionSchedule Schedule(double curveStartStation, double curveEndStation,
            double laneWidthMetres, double lanesRotated, double designSuperelevation, double designSpeedKmh,
            double normalCrown, CarriagewaySectionClass cls)
        {
            double runoff = SuperelevationRunoffLengthMetres(laneWidthMetres, lanesRotated, designSuperelevation,
                designSpeedKmh, cls);
            double runout = TangentRunoutLengthMetres(normalCrown, designSuperelevation, runoff);
            return new TransitionSchedule(runoff, runout, curveStartStation, curveEndStation);
        }

        // ---- the section at a station ----

        /// <summary> The two cross-slopes at one station.
        ///
        /// The OUTER rate is delegated to RM-40's <see cref="Superelevation.OuterEdgeCrossSlopeThroughRunoff"/> over the
        /// combined runout-plus-runoff fraction, and that delegation is exact rather than convenient: because
        /// L_t / L_r = e_NC / e_d, the outer edge's piecewise path (crown to zero over the runout, zero to full banking
        /// over the runoff) is a SINGLE straight line in the combined fraction, which is precisely what that function
        /// computes. RM-40 owns the outer edge's rotation; this task owns how long it gets to take.
        ///
        /// The INNER rate is this task's own: it holds at normal crown through the runout, then steepens linearly from
        /// crown to full banking over the runoff, reaching a plane with the outer edge exactly at the curve. </summary>
        public static SectionSlopes SlopesAtStation(double station, TransitionSchedule schedule,
            double appliedSuperelevation, double normalCrown)
        {
            if (appliedSuperelevation <= 0.0)
            {
                return new SectionSlopes(-normalCrown, -normalCrown);
            }

            // Outside the whole transition: a plain crown.
            if (station <= schedule.EntryRunoutStartStation || station >= schedule.ExitRunoutEndStation)
            {
                return new SectionSlopes(-normalCrown, -normalCrown);
            }
            // Through the curve: a plane at full banking.
            if (station >= schedule.CurveStartStation && station <= schedule.CurveEndStation)
            {
                return new SectionSlopes(appliedSuperelevation, -appliedSuperelevation);
            }

            bool entering = station < schedule.CurveStartStation;
            double combined = schedule.RunoffLengthMetres + schedule.RunoutLengthMetres;
            double travelled = entering
                ? station - schedule.EntryRunoutStartStation
                : schedule.ExitRunoutEndStation - station;
            double fraction = combined > 0.0 ? travelled / combined : 1.0;
            if (fraction < 0.0) { fraction = 0.0; }
            if (fraction > 1.0) { fraction = 1.0; }

            double outer = Superelevation.OuterEdgeCrossSlopeThroughRunoff(fraction, normalCrown,
                appliedSuperelevation);

            // The inner edge: normal crown until the runoff begins, then linear to full banking.
            double inner;
            double runoffTravelled = travelled - schedule.RunoutLengthMetres;
            if (runoffTravelled <= 0.0 || schedule.RunoffLengthMetres <= 0.0)
            {
                inner = -normalCrown;
            }
            else
            {
                double u = runoffTravelled / schedule.RunoffLengthMetres;
                if (u > 1.0) { u = 1.0; }
                inner = -normalCrown - (u * (appliedSuperelevation - normalCrown));
            }
            return new SectionSlopes(outer, inner);
        }

        /// <summary> THE ONE PLACE a height across the section is computed. A positive offset is toward the
        /// outside-of-curve edge, a negative one toward the inside; the answer is relative to the axis of rotation.
        /// Every kerb, marking, barrier and swept element calls this at its own offset. </summary>
        public static double SurfaceHeightAtOffsetMetres(double lateralOffsetMetres, SectionSlopes slopes)
        {
            return lateralOffsetMetres >= 0.0
                ? lateralOffsetMetres * slopes.OuterRate
                : -lateralOffsetMetres * slopes.InnerRate;
        }

        /// <summary> An attached element's height at its own lateral offset. Deliberately nothing but a call to
        /// <see cref="SurfaceHeightAtOffsetMetres"/>: it exists so that an author reaching for "how high does my kerb
        /// sit" finds a function that CANNOT disagree with the surface, rather than writing the multiplication out
        /// again. </summary>
        public static double AttachedElementHeightMetres(double lateralOffsetMetres, SectionSlopes slopes)
        {
            return SurfaceHeightAtOffsetMetres(lateralOffsetMetres, slopes);
        }

        /// <summary> The section as evenly spaced points, in RM-59's own vertex type so the divided builder and this
        /// share one representation. Offsets run from the inside edge to the outside edge. </summary>
        public static SectionPoint[] SurfaceSection(double halfWidthMetres, SectionSlopes slopes, int intervals)
        {
            int n = intervals > 0 ? intervals : 1;
            SectionPoint[] points = new SectionPoint[n + 1];
            double step = (halfWidthMetres * 2.0) / n;
            for (int i = 0; i <= n; i++)
            {
                double lateral = -halfWidthMetres + (i * step);
                points[i] = new SectionPoint((float)lateral,
                    (float)SurfaceHeightAtOffsetMetres(lateral, slopes));
            }
            return points;
        }

        /// <summary> Whether the section sheds water to both edges, and at what rates. On a straight both rates are
        /// negative and it does; inside a banked curve the outer rate is positive and the water crosses to the inside
        /// edge, which is correct and not a defect. </summary>
        public static DrainageVerdict Drainage(SectionSlopes slopes)
        {
            return new DrainageVerdict(slopes.OuterRate < 0.0 && slopes.InnerRate < 0.0,
                slopes.OuterRate, slopes.InnerRate);
        }

        // ---- curve widening ----

        /// <summary> The lateral clearance per lane on a curve, metres, interpolated from the lane width off the
        /// published pairs (3.0 m, 0.6 m) and (3.6 m, 0.9 m). </summary>
        public static double LateralClearanceMetres(double laneWidthMetres, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            double extra = (laneWidthMetres - cls.lateralClearanceReferenceLaneWidth)
                * cls.lateralClearancePerMetreOfLane;
            double clearance = cls.lateralClearanceAtThreeMetreLane + extra;
            return clearance < 0.0 ? 0.0 : clearance;
        }

        /// <summary> The vehicle's track width once it is ON the curve, metres: U = u + R - sqrt(R^2 - L^2). The rear
        /// wheels cut inside the front ones, so a truck occupies more width on a bend than its own track. Below the
        /// radius where R equals the wheelbase the relation has no solution -- the vehicle physically cannot make the
        /// turn -- and the track is returned as the full width, which the widening check then reports as an
        /// impossibility rather than as a NaN. </summary>
        public static double TrackWidthOnCurveMetres(double radiusMetres, SweptPathVehicle vehicle)
        {
            if (radiusMetres <= 0.0) { return vehicle.TrackWidthMetres; }
            double inside = (radiusMetres * radiusMetres) - (vehicle.WheelbaseMetres * vehicle.WheelbaseMetres);
            if (inside <= 0.0) { return vehicle.TrackWidthMetres + radiusMetres; }
            return vehicle.TrackWidthMetres + radiusMetres - Math.Sqrt(inside);
        }

        /// <summary> The width the front overhang sweeps beyond the front wheels, metres:
        /// F_A = sqrt(R^2 + A(2L + A)) - R. This is why a long-nosed vehicle needs a wider lane than its track
        /// suggests, and why the allowance grows as the radius tightens. </summary>
        public static double FrontOverhangWidthMetres(double radiusMetres, SweptPathVehicle vehicle)
        {
            if (radiusMetres <= 0.0) { return vehicle.FrontOverhangMetres; }
            double a = vehicle.FrontOverhangMetres;
            double l = vehicle.WheelbaseMetres;
            return Math.Sqrt((radiusMetres * radiusMetres) + (a * ((2.0 * l) + a))) - radiusMetres;
        }

        /// <summary> The extra width allowed for the difficulty of driving on a curve, metres: Z = k V / sqrt(R). It is
        /// the one term in the widening relation that is about the DRIVER rather than the vehicle, and it is why the
        /// allowance depends on speed at all. </summary>
        public static double DrivingDifficultyAllowanceMetres(double designSpeedKmh, double radiusMetres,
            CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            if (radiusMetres <= 0.0) { return 0.0; }
            return cls.drivingDifficultyCoefficient * designSpeedKmh / Math.Sqrt(radiusMetres);
        }

        /// <summary> The travelled-way width required on a curve, metres:
        /// W_c = N (U + C) + (N - 1) F_A + Z, with N lanes. </summary>
        public static double TravelledWayWidthOnCurveMetres(int laneCount, double laneWidthMetres,
            double radiusMetres, double designSpeedKmh, SweptPathVehicle vehicle, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            int n = laneCount < 1 ? 1 : laneCount;
            double u = TrackWidthOnCurveMetres(radiusMetres, vehicle);
            double c = LateralClearanceMetres(laneWidthMetres, cls);
            double fa = FrontOverhangWidthMetres(radiusMetres, vehicle);
            double z = DrivingDifficultyAllowanceMetres(designSpeedKmh, radiusMetres, cls);
            return (n * (u + c)) + ((n - 1) * fa) + z;
        }

        /// <summary> The widening to add on a curve, metres: the required travelled-way width less the width the
        /// straight already has, floored at zero and DISREGARDED below the published minimum -- a widening of a few
        /// centimetres is not worth building and would show up as a wobble in the edge line rather than as a wider
        /// road. </summary>
        public static double CurveWideningMetres(int laneCount, double laneWidthMetres, double radiusMetres,
            double designSpeedKmh, SweptPathVehicle vehicle, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            int n = laneCount < 1 ? 1 : laneCount;
            double required = TravelledWayWidthOnCurveMetres(n, laneWidthMetres, radiusMetres, designSpeedKmh,
                vehicle, cls);
            double onTangent = n * laneWidthMetres;
            double widening = required - onTangent;
            if (widening < cls.minimumWideningMetres) { return 0.0; }
            return widening;
        }

        /// <summary> The radius above which widening is disregarded for this road and vehicle, metres -- the answer to
        /// "where does the wider pavement stop". Found by bisection on the radius rather than by a closed form, because
        /// the widening relation mixes a square root, a reciprocal square root and a difference of radii and does not
        /// invert cleanly; the bracket is generous and the tolerance is a millimetre of radius, so the answer is
        /// deterministic to well below anything a road can express. </summary>
        public static double WideningThresholdRadiusMetres(int laneCount, double laneWidthMetres,
            double designSpeedKmh, SweptPathVehicle vehicle, CarriagewaySectionClass cls)
        {
            if (cls == null) { cls = CarriagewaySectionClass.Default(); }
            double low = 10.0;
            double high = 20000.0;
            if (CurveWideningMetres(laneCount, laneWidthMetres, low, designSpeedKmh, vehicle, cls) <= 0.0)
            {
                return low;   // even the tightest bend in the bracket needs none.
            }
            if (CurveWideningMetres(laneCount, laneWidthMetres, high, designSpeedKmh, vehicle, cls) > 0.0)
            {
                return high;  // still widening at 20 km of radius: the road is wider than the vehicle fits in.
            }
            for (int i = 0; i < 60; i++)
            {
                double mid = (low + high) * 0.5;
                if (CurveWideningMetres(laneCount, laneWidthMetres, mid, designSpeedKmh, vehicle, cls) > 0.0)
                { low = mid; }
                else { high = mid; }
                if (high - low < 0.001) { break; }
            }
            return high;
        }
    }
}

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
    /// <summary> Which movement a ramp performs. A DIVERGE leaves the mainline (an exit); a MERGE joins it (an
    /// entry). The two are not mirror images in the standards: a driver leaving decelerates on a lane that is
    /// already there, while a driver joining must reach mainline speed before the lane runs out, so the auxiliary
    /// lane lengths and the taper ends differ. </summary>
    public enum RampMovement
    {
        Diverge = 0,
        Merge = 1
    }

    /// <summary> How the ramp's plan curve is laid out between the two far-apart nodes it connects. LOOP turns
    /// through more than 90 degrees (the inner quadrant of a cloverleaf); DIRECT keeps the driver turning the way
    /// they are already going; SEMI_DIRECT crosses under or over and comes back. Only the plan geometry differs --
    /// the climb, the clearance and the auxiliary lane are the same machinery for all three. </summary>
    public enum RampAlignmentKind
    {
        Direct = 0,
        SemiDirect = 1,
        Loop = 2
    }

    /// <summary> RM-35's ramp constants, continuing <see cref="GradeSeparationClass"/>. Every value here is either a
    /// PUBLISHED quantity with its source named or an AUTHORED one that says so.
    ///
    /// The one this project was asked for by name is the auxiliary lane that opens BEFORE the diverge. AASHTO's
    /// Green Book gives deceleration-lane lengths in the 30-100 m range depending on the speed differential; the
    /// figure carried here is the length of full-width lane held AHEAD of the gore, 30 m, which is the low end of
    /// that band and the length an author asked for. It is deliberately separate from the TAPER that opens the lane
    /// -- the taper is not authored at all, it comes from <see cref="LaneModel.TaperLengthMetres"/>, so a lane
    /// opening at 100 km/h takes its published 223 m and one at 50 km/h takes 58 m.
    ///
    /// Held as exact decimals (double) for the same reason the clearance is: 30.0 divided by a class gradient must
    /// give the published length, and a float 30f fed through two divisions drifts off it. </summary>
    public sealed class InterchangeRampClass
    {
        /// <summary> Full-width auxiliary lane held ahead of the gore on a DIVERGE, in metres. AASHTO deceleration
        /// practice, low end of the band; this is the "opens ~30 m before the diverge" quantity. </summary>
        public double auxiliaryLaneBeforeDivergeMetres = 30.0;

        /// <summary> Full-width auxiliary lane held after the merge nose on a MERGE, in metres. Longer than the
        /// diverge case because a joining driver must reach mainline speed inside it (AASHTO acceleration lengths
        /// exceed deceleration lengths at every speed differential). AUTHORED at twice the diverge length, which
        /// sits inside AASHTO's acceleration band for an ordinary ramp-to-arterial merge. </summary>
        public double auxiliaryLaneAfterMergeMetres = 60.0;

        /// <summary> Maximum ramp gradient as a fraction. AASHTO allows ramps to climb steeper than the mainline
        /// they leave -- ramp grades of 6-8 % are ordinary where a mainline is held to 4 %. AUTHORED at 0.08 (8 %)
        /// as the ceiling; a project's own road class gradient is used instead when it is stricter. </summary>
        public double maxRampGradientFraction = 0.08;

        /// <summary> Design speed of the ramp proper, km/h. AASHTO ties ramp design speed to the mainline's; a ramp
        /// off a 100 km/h road is designed near 50 km/h. AUTHORED default for an ordinary ramp. </summary>
        public double rampDesignSpeedKmh = 50.0;

        /// <summary> Smallest plan radius the ramp curve may use, metres. Left AUTHORED rather than derived so a
        /// project can hold its ramps to a house standard; the derived minimum from
        /// <see cref="Superelevation.MinRadiusForDesignSpeedMetres"/> is used when it is larger. </summary>
        public double minRampRadiusMetres = 50.0;

        public static InterchangeRampClass Default()
        {
            return new InterchangeRampClass();
        }
    }


    /// <summary> The ramp a resolved flyover needs, as numbers: where the auxiliary lane opens on the mainline, how
    /// long the curved climbing ramp must be to gain the clearance without exceeding the ramp gradient, whether the
    /// plan curve is legal at the ramp's design speed, and the refusal when it is not.
    ///
    /// WHY THIS IS SEPARATE FROM GradeSeparation. <see cref="GradeSeparation"/> answers whether two crossing roads
    /// can be separated at all -- it raises one road over the other and refuses when the approach is too short. That
    /// is a FLYOVER, and a flyover on its own carries no traffic between the two roads: the crossing roads still do
    /// not connect. The ramp is what connects them, and it is a different problem with different standards (ramp
    /// gradients exceed mainline gradients, ramp design speeds are lower, and the auxiliary lane is a mainline
    /// feature rather than a ramp one). Keeping them apart is also what lets an interchange be built WITHOUT
    /// deleting the at-grade intersection that was there: the intersection is untouched machinery, the flyover is
    /// one road raised, and the ramp is a third thing added alongside both.
    ///
    /// THE FAR-APART NODE CASE. A ramp almost never connects two nodes that happen to sit near each other. It
    /// connects a node on one road to a node on another that may be hundreds of metres away, at a different
    /// elevation, pointing a different way. So the ramp length is resolved from THREE independent minimums -- the
    /// plan distance between the two nodes, the climb the clearance demands, and the curve the turn demands -- and
    /// the longest of them wins. Sizing a ramp from the straight-line node distance alone is the mistake that
    /// produces a ramp that either cannot climb or cannot turn.
    ///
    /// The verifiable core is here. The swept ramp mesh, its terrain fitting, the gore-area pavement markings and
    /// the physical barrier at the nose are the deferred scene half. </summary>
    public static class InterchangeRamp
    {
        /// <summary> Where a DIVERGE ramp's auxiliary lane sits on the mainline, in metres along the mainline. The
        /// gore is where the ramp physically leaves; the full-width lane is held for the authored length ahead of
        /// it, and the TAPER that opens that lane sits upstream of the full-width part at its published length.
        /// Returns the three stations: where the taper begins, where full width is reached, and the gore. </summary>
        public static void DivergeAuxiliaryLaneStations(double goreStationMetres, double laneWidthMetres,
            double mainlineDesignSpeedKmh, InterchangeRampClass cls,
            out double taperStartMetres, out double fullWidthMetres, out double goreMetres)
        {
            goreMetres = goreStationMetres;
            fullWidthMetres = goreStationMetres - cls.auxiliaryLaneBeforeDivergeMetres;
            taperStartMetres = fullWidthMetres - LaneModel.TaperLengthMetres(laneWidthMetres, mainlineDesignSpeedKmh);
        }

        /// <summary> Where a MERGE ramp's auxiliary lane sits on the mainline. The nose is where the ramp joins; the
        /// lane runs at full width for the authored acceleration length after it and then tapers closed over the
        /// published taper length. Returns the three stations in travel order. </summary>
        public static void MergeAuxiliaryLaneStations(double noseStationMetres, double laneWidthMetres,
            double mainlineDesignSpeedKmh, InterchangeRampClass cls,
            out double noseMetres, out double endFullWidthMetres, out double taperEndMetres)
        {
            noseMetres = noseStationMetres;
            endFullWidthMetres = noseStationMetres + cls.auxiliaryLaneAfterMergeMetres;
            taperEndMetres = endFullWidthMetres + LaneModel.TaperLengthMetres(laneWidthMetres, mainlineDesignSpeedKmh);
        }

        /// <summary> The mainline auxiliary lane a ramp needs, as the same <see cref="AuxiliaryLane"/> record the
        /// cross-section already consumes -- so a ramp's auxiliary lane is not a second mechanism. A diverge asks for
        /// a Deceleration bay ending at the gore; a merge asks for an Acceleration lane opening at the nose. </summary>
        public static AuxiliaryLane MainlineAuxiliaryLane(RampMovement movement, int direction,
            double stationMetres, double laneWidthMetres, double mainlineDesignSpeedKmh, InterchangeRampClass cls)
        {
            if (movement == RampMovement.Diverge)
            {
                return AuxiliaryLane.Deceleration(direction, stationMetres,
                    cls.auxiliaryLaneBeforeDivergeMetres, laneWidthMetres, mainlineDesignSpeedKmh);
            }
            return AuxiliaryLane.Acceleration(direction, stationMetres,
                cls.auxiliaryLaneAfterMergeMetres, laneWidthMetres, mainlineDesignSpeedKmh);
        }

        /// <summary> The ramp length the CLIMB demands: the height to be gained divided by the gradient the ramp may
        /// use. The ramp gradient is the lesser of the ramp class ceiling and the road class maximum when the road
        /// class is stricter -- a project holding its roads to 4 % does not get 8 % ramps by accident. A non-positive
        /// gradient can never climb, so it needs an infinite ramp and is always refused. </summary>
        public static double ClimbLengthMetres(double heightGainMetres, double roadClassMaxGradientFraction,
            InterchangeRampClass cls)
        {
            double gradient = cls.maxRampGradientFraction;
            if (roadClassMaxGradientFraction > 0.0 && roadClassMaxGradientFraction < gradient)
            {
                gradient = roadClassMaxGradientFraction;
            }
            if (gradient <= 0.0) { return double.PositiveInfinity; }
            if (heightGainMetres <= 0.0) { return 0.0; }
            return heightGainMetres / gradient;
        }

        /// <summary> The arc length of the ramp's plan curve: radius times the turn in radians. A ramp that turns
        /// 270 degrees on a 50 m radius is 236 m of pavement no matter how close its two end nodes are, which is why
        /// the curve is one of the three minimums. </summary>
        public static double CurveArcLengthMetres(double radiusMetres, double turnAngleDegrees)
        {
            if (radiusMetres <= 0.0 || turnAngleDegrees == 0.0) { return 0.0; }
            double turn = Math.Abs(turnAngleDegrees);
            return radiusMetres * turn * Math.PI / 180.0;
        }

        /// <summary> The turn a ramp alignment performs, in degrees, as the published families are drawn: a direct
        /// connection turns through the angle between the two roads; a semi-direct one crosses and returns, adding a
        /// half turn; a loop takes the long way round the quadrant. Sign is dropped -- handedness is the plan
        /// geometry's business, not the length's. </summary>
        public static double AlignmentTurnDegrees(RampAlignmentKind kind, double roadAngleBetweenDegrees)
        {
            double between = Math.Abs(roadAngleBetweenDegrees) % 360.0;
            if (between > 180.0) { between = 360.0 - between; }
            switch (kind)
            {
                case RampAlignmentKind.Direct:
                    return between;
                case RampAlignmentKind.SemiDirect:
                    return between + 180.0;
                case RampAlignmentKind.Loop:
                    return 360.0 - between;
            }
            return between;
        }

        /// <summary> The smallest plan radius this ramp may use: the authored house minimum, or the radius the
        /// published superelevation relation demands at the ramp's design speed, whichever is LARGER. Taking the
        /// larger is the only safe reading -- a house standard looser than the physics is not a standard. </summary>
        public static double MinimumRadiusMetres(InterchangeRampClass cls, SuperelevationClass superCls)
        {
            double physics = Superelevation.MinRadiusForDesignSpeedMetres(cls.rampDesignSpeedKmh, superCls,
                Superelevation.SideFrictionFactor(cls.rampDesignSpeedKmh, superCls));
            return physics > cls.minRampRadiusMetres ? physics : cls.minRampRadiusMetres;
        }

        /// <summary> The outcome of resolving one ramp between two far-apart nodes. </summary>
        public struct RampResolve
        {
            /// <summary> A buildable ramp was produced. </summary>
            public bool resolved;
            /// <summary> Could not be produced as asked; every quoted length is still filled in so the author can
            /// see what to change. </summary>
            public bool flagged;
            /// <summary> Why, in one sentence, when flagged. Empty when resolved. </summary>
            public string message;
            /// <summary> The length the ramp will be built at: the longest of the three minimums. </summary>
            public double rampLengthMetres;
            /// <summary> Straight-line plan distance between the two nodes the ramp connects. </summary>
            public double planDistanceMetres;
            /// <summary> Length the climb alone demands. </summary>
            public double climbLengthMetres;
            /// <summary> Arc length the plan curve alone demands. </summary>
            public double curveLengthMetres;
            /// <summary> The radius actually used, after the minimum was applied. </summary>
            public double radiusMetres;
            /// <summary> The gradient the ramp ends up climbing at, once the length is settled. Always at or below
            /// the ceiling when resolved. </summary>
            public double gradientFraction;
            /// <summary> Which of the three minimums decided the length: 0 plan, 1 climb, 2 curve. </summary>
            public int governingConstraint;
        }

        /// <summary> Resolve one ramp connecting a node on one road to a node on another, which may be far apart in
        /// plan and at different elevations.
        ///
        /// The three minimums are computed independently and the LONGEST governs; the ramp then climbs its height
        /// over that length, so the gradient that comes out is at or below the ceiling by construction. Two things
        /// are refused rather than built: a radius below the minimum the ramp's design speed allows (the curve
        /// cannot be driven at the speed it is signed for), and a climb the gradient ceiling cannot achieve at all
        /// (a non-positive gradient, i.e. infinite length). Everything else resolves, and the quoted lengths tell
        /// the author which constraint they are fighting. </summary>
        public static RampResolve ResolveRamp(double planDistanceMetres, double heightGainMetres,
            double requestedRadiusMetres, RampAlignmentKind kind, double roadAngleBetweenDegrees,
            double roadClassMaxGradientFraction, InterchangeRampClass cls, SuperelevationClass superCls)
        {
            RampResolve r = new RampResolve();
            r.message = string.Empty;
            r.planDistanceMetres = planDistanceMetres < 0.0 ? 0.0 : planDistanceMetres;

            double minRadius = MinimumRadiusMetres(cls, superCls);
            r.radiusMetres = requestedRadiusMetres;

            double turn = AlignmentTurnDegrees(kind, roadAngleBetweenDegrees);
            r.climbLengthMetres = ClimbLengthMetres(heightGainMetres, roadClassMaxGradientFraction, cls);
            r.curveLengthMetres = CurveArcLengthMetres(r.radiusMetres, turn);

            if (requestedRadiusMetres < minRadius - 1e-9)
            {
                r.flagged = true;
                r.message = "ramp radius " + F(requestedRadiusMetres)
                          + " m is below the " + F(minRadius)
                          + " m minimum for a ramp design speed of " + F(cls.rampDesignSpeedKmh)
                          + " km/h";
                return r;
            }

            if (double.IsPositiveInfinity(r.climbLengthMetres))
            {
                r.flagged = true;
                r.message = "ramp cannot climb: the gradient ceiling is not positive, so no finite ramp gains "
                          + F(heightGainMetres) + " m";
                return r;
            }

            r.rampLengthMetres = r.planDistanceMetres;
            r.governingConstraint = 0;
            if (r.climbLengthMetres > r.rampLengthMetres)
            {
                r.rampLengthMetres = r.climbLengthMetres;
                r.governingConstraint = 1;
            }
            if (r.curveLengthMetres > r.rampLengthMetres)
            {
                r.rampLengthMetres = r.curveLengthMetres;
                r.governingConstraint = 2;
            }

            r.gradientFraction = r.rampLengthMetres > 0.0 ? heightGainMetres / r.rampLengthMetres : 0.0;
            r.resolved = true;
            return r;
        }

        /// <summary> The stations along a resolved ramp at which its centreline is sampled, from 0 to the ramp
        /// length inclusive, at no more than <paramref name="maxSpacingMetres"/> apart and made even -- the same
        /// ceiling-then-re-divide rule the bridge supports use, so a ramp's vertices are evenly spaced rather than
        /// ending on a stub. Deterministic: same inputs, same list (C-06). </summary>
        public static List<double> CentrelineStationsMetres(double rampLengthMetres, double maxSpacingMetres)
        {
            List<double> stations = new List<double>();
            if (rampLengthMetres <= 0.0) { return stations; }
            stations.Add(0.0);
            if (maxSpacingMetres > 0.0)
            {
                int bays = (int)Math.Ceiling(rampLengthMetres / maxSpacingMetres);
                if (bays < 1) { bays = 1; }
                double step = rampLengthMetres / bays;
                for (int i = 1; i < bays; i++) { stations.Add(i * step); }
            }
            stations.Add(rampLengthMetres);
            return stations;
        }

        /// <summary> The ramp centreline's elevation at a station: a straight grade from the low end to the high end
        /// over the resolved length. Clamped outside the ramp so a station past either end returns that end's
        /// elevation rather than extrapolating off the structure. A vertical curve at each end is the deferred scene
        /// half's business; the grade line is what the length was resolved against. </summary>
        public static double ElevationAtStationMetres(double stationMetres, double startElevationMetres,
            double heightGainMetres, double rampLengthMetres)
        {
            if (rampLengthMetres <= 0.0) { return startElevationMetres; }
            double s = stationMetres;
            if (s <= 0.0) { return startElevationMetres; }
            if (s >= rampLengthMetres) { return startElevationMetres + heightGainMetres; }
            return startElevationMetres + heightGainMetres * (s / rampLengthMetres);
        }

        /// <summary> Culture-invariant number text, so a refusal message reads the same on every machine and two runs
        /// of the same scene produce byte-identical reports (C-06). </summary>
        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

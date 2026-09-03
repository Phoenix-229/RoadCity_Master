#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which way a pair of grades bends. The distinction is not cosmetic: a crest is limited by what the
    /// driver can SEE over it, a sag by how far the headlights reach into it, and the two produce different curve
    /// lengths from the same grade change -- at 100 km/h a crest needs K = 52 and a sag K = 45. </summary>
    public enum VerticalCurveKind
    {
        Crest = 0,
        Sag = 1,
        /// <summary> The grades are equal: no curve is needed. </summary>
        None = 2
    }


    /// <summary> A vertical point of intersection -- where two grades meet, before the parabola rounds the corner off.
    /// Station is measured along the alignment, not in world space, so it survives the road being moved. </summary>
    public readonly struct VerticalPoint
    {
        public readonly double StationMetres;
        public readonly double ElevationMetres;

        public VerticalPoint(double stationMetres, double elevationMetres)
        {
            StationMetres = stationMetres;
            ElevationMetres = elevationMetres;
        }
    }


    /// <summary> The outcome of one check along the profile, with the numbers that produced it. Every check reports the
    /// value, the limit and where -- never a bare pass or fail -- because "this road is too steep" is not actionable
    /// and "9.4% at station 214 m exceeds the 8% limit for Motorway" is. </summary>
    public readonly struct ProfileFinding
    {
        public readonly bool Ok;
        public readonly double StationMetres;
        public readonly double Value;
        public readonly double Limit;
        public readonly string Message;

        public ProfileFinding(bool ok, double stationMetres, double value, double limit, string message)
        {
            Ok = ok; StationMetres = stationMetres; Value = value; Limit = limit; Message = message;
        }

        public static ProfileFinding Pass(double station, double value, double limit)
        {
            return new ProfileFinding(true, station, value, limit, null);
        }
    }


    /// <summary> RM-95's vertical design constants. Unlike a table typed from memory, everything here except the four
    /// PUBLISHED PHYSICAL constants is DERIVED from its defining relation, and the gate proves the derivation
    /// reproduces the standard's own published design table at every tabulated speed. That is the difference between
    /// citing a standard and remembering one: if a figure below were mistyped, the derived table would stop matching
    /// and the gate would say so.
    ///
    /// Held as double, not float, for the reason RM-40 found the hard way: a tabulated lookup must return the published
    /// figure exactly, and float 0.35f is 0.34999999. </summary>
    [Serializable]
    public sealed class VerticalDesignClass
    {
        /// <summary> Brake-reaction (perception-reaction) time in seconds. AASHTO uses 2.5 s for design -- long enough
        /// to cover the great majority of drivers, and the single most influential figure in the whole model. </summary>
        public double perceptionReactionSeconds = 2.5;

        /// <summary> The deceleration used for stopping-sight distance, as a friction-equivalent coefficient. AASHTO
        /// specifies a comfortable deceleration of 3.4 m/s-squared, which is 3.4 / 9.81 = 0.3466 of gravity; 0.35 is
        /// that figure to two places and is what the metric SSD relation's 254 denominator already assumes
        /// (254 = 2 x 9.81 x 3.6-squared / 10, the metric unit-conversion constant). </summary>
        public double decelerationCoefficient = 0.35;

        /// <summary> Driver eye height above the pavement, metres. AASHTO passenger-car design value 1.08 m. </summary>
        public double driverEyeHeightMetres = 1.08;

        /// <summary> Height of the object a driver must see to stop for it, metres. AASHTO design value 0.60 m --
        /// chosen as the height that guarantees a stopped vehicle's tail-lights are visible. </summary>
        public double objectHeightMetres = 0.60;

        /// <summary> Headlight mounting height above the pavement, metres. AASHTO design value 0.60 m; this is what
        /// governs a SAG curve, since nothing but the headlights lights the road at night. </summary>
        public double headlightHeightMetres = 0.60;

        /// <summary> Upward divergence of the headlight beam from the vehicle's longitudinal axis, degrees. AASHTO
        /// design value 1 degree. </summary>
        public double headlightBeamDivergenceDegrees = 1.0;

        /// <summary> Design speeds the profile checks are tabulated at, km/h. Steps of 10 km/h, which is how every
        /// design-speed table in the standard is laid out. </summary>
        public double[] tabulatedSpeedsKmh = { 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };

        /// <summary> AASHTO's PUBLISHED design stopping-sight distances, metres, one per tabulated speed. These are not
        /// an independent source of truth -- <see cref="VerticalAlignment.StoppingSightDistanceMetres"/> computes them
        /// from the relation and the gate asserts the two agree at all ten speeds. They are here so that agreement can
        /// be checked at all. </summary>
        public double[] publishedStoppingSightMetres = { 35, 50, 65, 85, 105, 130, 155, 185, 215, 250 };

        /// <summary> The minimum vertical curve length even when the grade change is tiny, metres. AASHTO gives
        /// L_min = 0.6V with V in km/h -- three seconds of travel, so a curve never reads as a kink however gentle the
        /// grade change is. Below this the parabola is shorter than the eye can resolve as a curve at all. </summary>
        public double minimumCurveLengthPerKph = 0.6;

        public static VerticalDesignClass Default()
        {
            return new VerticalDesignClass();
        }
    }


    /// <summary> RM-95's vertical alignment: grades as percentages between vertical points of intersection, joined by
    /// parabolic crest and sag curves whose length comes from L = K x A, plus a stopping-sight-distance check along the
    /// finished profile. It mirrors RM-75's horizontal model deliberately -- the profile is AUTHORED as grades and
    /// curves, and the existing spline's elevation is FITTED to it, so arc-length mapping, the buffer maker, the
    /// sweeper and every edge-object run keep working untouched. No new mesh path appears here, only better numbers
    /// flowing into the existing one (RM-62).
    ///
    /// The legacy code has no vertical alignment in the engineering sense at all: node elevations are interpolated by
    /// the same Hermite evaluation as the plan, so a road's grade is whatever the tangents happen to produce and a hill
    /// is a shape the author nudges until it looks acceptable. Without a grade VALUE there is no way to REFUSE an
    /// impossible road, so the tool silently builds a 22% motorway. Everything physical downstream -- superelevation
    /// runoff length (RM-96), sight distance, drainage direction, the clearance a grade separation actually achieves
    /// (RM-35), the earthwork a cut produces (RM-38) -- is a function of grade, and each is currently approximated or
    /// simply asserted.
    ///
    /// Every relation here is stated in its metric form, and every K value is DERIVED from the sight-distance geometry
    /// rather than transcribed. RM-94's census is why: transcribed tables are exactly the class of number that turns
    /// out to be three magic constants encoding one real one. </summary>
    public static class VerticalAlignment
    {
        /// <summary> The metric SSD denominator constant. 254 = 2 x 9.81 x (3.6 squared) / 10 -- the conversion that
        /// lets V be entered in km/h and the answer come out in metres. Not a tunable. </summary>
        public const double MetricBrakingConstant = 254.0;

        /// <summary> The metric constant converting km/h to m/s for the reaction-distance term: 1 / 3.6 = 0.2778.
        /// Written as the standard writes it. </summary>
        public const double MetricReactionConstant = 0.278;

        // ---- grades ----

        /// <summary> The grade between two vertical points, as a PERCENTAGE (rising positive). Percent rather than a
        /// fraction because that is the unit every published grade limit is stated in, and converting at the boundary
        /// once is safer than converting at each use. </summary>
        public static double GradePercent(VerticalPoint from, VerticalPoint to)
        {
            double run = to.StationMetres - from.StationMetres;
            if (Math.Abs(run) < 1e-9) { return 0.0; }
            return ((to.ElevationMetres - from.ElevationMetres) / run) * 100.0;
        }

        /// <summary> The algebraic grade difference A, in percent, always positive. This is the A in L = K x A. </summary>
        public static double AlgebraicDifferencePercent(double gradeInPercent, double gradeOutPercent)
        {
            return Math.Abs(gradeOutPercent - gradeInPercent);
        }

        /// <summary> Which way the profile bends. A crest is where the grade DECREASES through the point (up then less
        /// steeply up, or up then down); a sag is where it increases. </summary>
        public static VerticalCurveKind KindOf(double gradeInPercent, double gradeOutPercent)
        {
            double delta = gradeOutPercent - gradeInPercent;
            if (Math.Abs(delta) < 1e-9) { return VerticalCurveKind.None; }
            return delta < 0.0 ? VerticalCurveKind.Crest : VerticalCurveKind.Sag;
        }

        // ---- sight distance ----

        /// <summary> Stopping sight distance in metres: SSD = 0.278 x V x t + V-squared / (254 x (f +/- G)), the metric
        /// AASHTO relation. The grade term is what makes this a VERTICAL question rather than a table lookup -- braking
        /// downhill takes longer, so a falling grade lengthens the requirement and a rising one shortens it.
        /// Grade is passed as a percentage and divided here, so callers never have to remember which unit this wants.
        /// The friction term is floored so a grade steeper than the available friction cannot produce a negative or
        /// infinite distance; at that point the vehicle cannot stop on that grade at all, which is a design refusal
        /// rather than a number. </summary>
        public static double StoppingSightDistanceMetres(double speedKph, double gradePercent,
            VerticalDesignClass design)
        {
            if (design == null) { design = VerticalDesignClass.Default(); }
            double reaction = MetricReactionConstant * speedKph * design.perceptionReactionSeconds;
            double effective = design.decelerationCoefficient + (gradePercent / 100.0);
            if (effective < 0.01) { effective = 0.01; }
            double braking = (speedKph * speedKph) / (MetricBrakingConstant * effective);
            return reaction + braking;
        }

        /// <summary> The DESIGN stopping sight distance: the computed value rounded up to the next 5 m, which is how the
        /// standard's published table is derived from the same relation. Rounding up rather than to nearest matters --
        /// a design value must never be shorter than the computed requirement. </summary>
        public static double DesignStoppingSightMetres(double speedKph, VerticalDesignClass design)
        {
            double raw = StoppingSightDistanceMetres(speedKph, 0.0, design);
            return Math.Ceiling(raw / 5.0) * 5.0;
        }

        // ---- vertical curves ----

        /// <summary> The crest rate of vertical curvature K -- metres of curve per percent of grade change -- derived
        /// from the sight line grazing the crest: K = S-squared / (200 x (sqrt(h1) + sqrt(h2)) squared), where h1 is the
        /// driver's eye height and h2 the object height. With AASHTO's 1.08 m and 0.60 m the denominator is 658, and
        /// this reproduces the published crest K column exactly. </summary>
        public static double CrestK(double sightDistanceMetres, VerticalDesignClass design)
        {
            if (design == null) { design = VerticalDesignClass.Default(); }
            double roots = Math.Sqrt(design.driverEyeHeightMetres) + Math.Sqrt(design.objectHeightMetres);
            double denominator = 200.0 * roots * roots;
            if (denominator <= 0.0) { return 0.0; }
            return (sightDistanceMetres * sightDistanceMetres) / denominator;
        }

        /// <summary> The sag rate of vertical curvature K, derived from the headlight beam reaching the pavement:
        /// K = S-squared / (200 x (h + S x tan(beta))), where h is the headlight height and beta the beam's upward
        /// divergence. With AASHTO's 0.60 m and 1 degree the denominator is 120 + 3.5S, and this reproduces the
        /// published sag K column. A sag is governed by NIGHT, which is why it needs no object height: there is nothing
        /// to see beyond where the light stops. </summary>
        public static double SagK(double sightDistanceMetres, VerticalDesignClass design)
        {
            if (design == null) { design = VerticalDesignClass.Default(); }
            double tanBeta = Math.Tan(design.headlightBeamDivergenceDegrees * Math.PI / 180.0);
            double denominator = 200.0 * (design.headlightHeightMetres + (sightDistanceMetres * tanBeta));
            if (denominator <= 0.0) { return 0.0; }
            return (sightDistanceMetres * sightDistanceMetres) / denominator;
        }

        /// <summary> The K required at a design speed for the given curve kind, using the DESIGN sight distance. </summary>
        public static double RequiredK(double speedKph, VerticalCurveKind kind, VerticalDesignClass design)
        {
            if (kind == VerticalCurveKind.None) { return 0.0; }
            double sight = DesignStoppingSightMetres(speedKph, design);
            return kind == VerticalCurveKind.Crest ? CrestK(sight, design) : SagK(sight, design);
        }

        /// <summary> The vertical curve length a grade change requires, metres: L = K x A, floored at the standard's
        /// minimum length of 0.6V so a gentle grade change still reads as a curve rather than a kink. Returns the
        /// larger of the two, which is what the standard means by both being requirements. </summary>
        public static double RequiredCurveLengthMetres(double gradeInPercent, double gradeOutPercent,
            double speedKph, VerticalDesignClass design)
        {
            if (design == null) { design = VerticalDesignClass.Default(); }
            VerticalCurveKind kind = KindOf(gradeInPercent, gradeOutPercent);
            if (kind == VerticalCurveKind.None) { return 0.0; }
            double a = AlgebraicDifferencePercent(gradeInPercent, gradeOutPercent);
            double byK = RequiredK(speedKph, kind, design) * a;
            double floor = design.minimumCurveLengthPerKph * speedKph;
            return Math.Max(byK, floor);
        }

        /// <summary> The elevation on a symmetrical parabolic vertical curve, at a distance x past the curve's start.
        /// y = y_start + g_in x + (A / (200 L)) x-squared, with A in percent and g_in in percent -- the standard's own
        /// parabola, which is what makes the grade CONTINUOUS through the curve: the derivative at x = 0 is exactly the
        /// incoming grade and at x = L exactly the outgoing one, so no node can produce a kink. That continuity is the
        /// gate's central claim about dragging a node. </summary>
        public static double ElevationOnCurveMetres(double startElevationMetres, double gradeInPercent,
            double gradeOutPercent, double curveLengthMetres, double distanceIntoCurveMetres)
        {
            if (curveLengthMetres <= 0.0)
            {
                return startElevationMetres + ((gradeInPercent / 100.0) * distanceIntoCurveMetres);
            }
            double x = distanceIntoCurveMetres;
            double gIn = gradeInPercent / 100.0;
            double rate = (gradeOutPercent - gradeInPercent) / (100.0 * 2.0 * curveLengthMetres);
            return startElevationMetres + (gIn * x) + (rate * x * x);
        }

        /// <summary> The grade at a point ON the curve, as a percentage: the parabola's derivative, linear in x. Used
        /// by the sight-distance check, which needs the grade at each station rather than between nodes, and by the
        /// continuity assertion -- this returns gradeIn at x = 0 and gradeOut at x = L by construction. </summary>
        public static double GradeOnCurvePercent(double gradeInPercent, double gradeOutPercent,
            double curveLengthMetres, double distanceIntoCurveMetres)
        {
            if (curveLengthMetres <= 0.0) { return gradeInPercent; }
            double t = distanceIntoCurveMetres / curveLengthMetres;
            if (t < 0.0) { t = 0.0; }
            if (t > 1.0) { t = 1.0; }
            return gradeInPercent + ((gradeOutPercent - gradeInPercent) * t);
        }

        // ---- checks against a road class ----

        /// <summary> Whether a grade is within its class's maximum, with the offending station, the grade, the limit and
        /// the class named in the message. The limit and its citation both come from RoadClassDefaults, so no grade
        /// figure is restated here -- RM-88 owns them. </summary>
        public static ProfileFinding CheckGrade(double stationMetres, double gradePercent,
            Road.RoadClassEnum roadClass)
        {
            RoadClassProfile profile = RoadClassDefaults.For(roadClass);
            double limitPercent = profile.MaxGrade * 100.0;
            double magnitude = Math.Abs(gradePercent);
            if (magnitude <= limitPercent + 1e-9)
            {
                return ProfileFinding.Pass(stationMetres, magnitude, limitPercent);
            }
            return new ProfileFinding(false, stationMetres, magnitude, limitPercent, string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.0}% grade at station {1:0.0} m exceeds the {2:0.0}% maximum for road class {3} ({4})",
                magnitude, stationMetres, limitPercent, roadClass, profile.MaxGradeCite.Reference));
        }

        /// <summary> Whether a vertical curve is long enough for its grade change at the design speed, naming the
        /// required length and which of the two requirements it failed. </summary>
        public static ProfileFinding CheckCurveLength(double stationMetres, double actualLengthMetres,
            double gradeInPercent, double gradeOutPercent, double speedKph, VerticalDesignClass design)
        {
            double required = RequiredCurveLengthMetres(gradeInPercent, gradeOutPercent, speedKph, design);
            if (actualLengthMetres >= required - 1e-9)
            {
                return ProfileFinding.Pass(stationMetres, actualLengthMetres, required);
            }
            VerticalCurveKind kind = KindOf(gradeInPercent, gradeOutPercent);
            double a = AlgebraicDifferencePercent(gradeInPercent, gradeOutPercent);
            return new ProfileFinding(false, stationMetres, actualLengthMetres, required, string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "the {0} curve at station {1:0.0} m is {2:0.0} m long but needs {3:0.0} m "
                + "(L = K x A, with A = {4:0.0}% and K = {5:0.0} at {6:0} km/h)",
                kind.ToString().ToLowerInvariant(), stationMetres, actualLengthMetres, required, a,
                RequiredK(speedKph, kind, design), speedKph));
        }

        /// <summary> Whether the sight distance available over a crest is enough to stop, given the curve as built.
        /// Inverts L = K x A: the K a curve actually delivers is L / A, and the sight distance that K provides is
        /// sqrt(K x 658). Comparing that against the requirement AT THE LOCAL GRADE is the point -- a crest on a
        /// falling approach needs more distance than the flat table says. </summary>
        public static ProfileFinding CheckStoppingSight(double stationMetres, double actualLengthMetres,
            double gradeInPercent, double gradeOutPercent, double speedKph, VerticalDesignClass design)
        {
            if (design == null) { design = VerticalDesignClass.Default(); }
            double a = AlgebraicDifferencePercent(gradeInPercent, gradeOutPercent);
            double required = StoppingSightDistanceMetres(speedKph, gradeOutPercent, design);

            if (a < 1e-9)
            {
                // A tangent grade: the sight line is unobstructed by the profile, so the check cannot fail here.
                return ProfileFinding.Pass(stationMetres, double.PositiveInfinity, required);
            }

            double deliveredK = actualLengthMetres / a;
            VerticalCurveKind kind = KindOf(gradeInPercent, gradeOutPercent);
            double available;
            if (kind == VerticalCurveKind.Crest)
            {
                double roots = Math.Sqrt(design.driverEyeHeightMetres) + Math.Sqrt(design.objectHeightMetres);
                available = Math.Sqrt(deliveredK * 200.0 * roots * roots);
            }
            else
            {
                // A sag inverts to a quadratic rather than a square root: K = S^2 / (200 (h + S tan b)) rearranges to
                // S^2 - (200 K tan b) S - 200 K h = 0, so S is the positive root. The linear term is what the headlight
                // beam's divergence contributes -- the further ahead you look, the higher the beam has climbed.
                double tanBeta = Math.Tan(design.headlightBeamDivergenceDegrees * Math.PI / 180.0);
                double b = 200.0 * deliveredK * tanBeta;
                double c = 200.0 * deliveredK * design.headlightHeightMetres;
                available = (b + Math.Sqrt((b * b) + (4.0 * c))) / 2.0;
            }

            if (available >= required - 1e-9)
            {
                return ProfileFinding.Pass(stationMetres, available, required);
            }
            return new ProfileFinding(false, stationMetres, available, required, string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "the {0} at station {1:0.0} m offers {2:0.0} m of stopping sight distance but {3:0.0} m is required "
                + "at {4:0} km/h on a {5:0.0}% grade",
                kind.ToString().ToLowerInvariant(), stationMetres, available, required, speedKph, gradeOutPercent));
        }

        // ---- the fitted profile ----

        /// <summary> Samples the finished profile at a fixed station interval, rounding every vertical point of
        /// intersection with its required parabola. This is what gets handed to the existing spline as elevations: the
        /// legacy mesh path is untouched and simply receives heights that came from a real profile instead of from
        /// whatever the Hermite tangents produced. Returns one sample per station step, inclusive of both ends.
        ///
        /// The curve at each interior point is centred on the point and is as long as its grade change requires, but is
        /// clipped to half the distance to each neighbour so two curves cannot overlap -- which is the one way a
        /// parabola-per-point scheme can produce the discontinuity it exists to prevent. </summary>
        public static List<VerticalPoint> Sample(IList<VerticalPoint> points, double stationStepMetres,
            double speedKph, VerticalDesignClass design)
        {
            List<VerticalPoint> outPoints = new List<VerticalPoint>();
            if (points == null || points.Count == 0) { return outPoints; }
            if (points.Count == 1) { outPoints.Add(points[0]); return outPoints; }
            if (stationStepMetres <= 0.0) { stationStepMetres = 1.0; }
            if (design == null) { design = VerticalDesignClass.Default(); }

            double start = points[0].StationMetres;
            double end = points[points.Count - 1].StationMetres;
            double span = end - start;
            if (span <= 0.0) { outPoints.Add(points[0]); return outPoints; }

            // Stepped by INDEX, not by accumulating the station in a double. Accumulation drifts, and the drift lands
            // exactly where it does most harm: the final station arrives at 799.9999 instead of 800, the guard below
            // then appends a second sample a ten-thousandth of a metre away, and anything that differentiates the
            // profile divides an elevation difference by that near-zero run and reports a grade of nonsense.
            int steps = (int)Math.Floor((span / stationStepMetres) + 1e-9);
            for (int i = 0; i <= steps; i++)
            {
                double s = start + (i * stationStepMetres);
                outPoints.Add(new VerticalPoint(s, ElevationAt(points, s, speedKph, design)));
            }
            // Include the final station when the step does not divide the length, but only if it is a real distance
            // from the last sample -- never as a duplicate.
            double lastStation = outPoints[outPoints.Count - 1].StationMetres;
            if (end - lastStation > stationStepMetres * 0.001)
            {
                outPoints.Add(new VerticalPoint(end, ElevationAt(points, end, speedKph, design)));
            }
            return outPoints;
        }

        /// <summary> The elevation at one station on the finished profile. </summary>
        public static double ElevationAt(IList<VerticalPoint> points, double stationMetres, double speedKph,
            VerticalDesignClass design)
        {
            if (points == null || points.Count == 0) { return 0.0; }
            if (points.Count == 1) { return points[0].ElevationMetres; }
            if (design == null) { design = VerticalDesignClass.Default(); }

            if (stationMetres <= points[0].StationMetres) { return points[0].ElevationMetres; }
            int last = points.Count - 1;
            if (stationMetres >= points[last].StationMetres) { return points[last].ElevationMetres; }

            for (int i = 0; i < last; i++)
            {
                VerticalPoint a = points[i];
                VerticalPoint b = points[i + 1];
                if (stationMetres < a.StationMetres || stationMetres > b.StationMetres) { continue; }

                double gradeIn = GradePercent(a, b);

                // Is this station inside the parabola centred on b?
                if (i + 2 <= last)
                {
                    double gradeOut = GradePercent(b, points[i + 2]);
                    double length = ClippedCurveLength(points, i + 1, gradeIn, gradeOut, speedKph, design);
                    double curveStart = b.StationMetres - (length / 2.0);
                    if (length > 0.0 && stationMetres >= curveStart)
                    {
                        double elevationAtCurveStart = b.ElevationMetres
                            - ((gradeIn / 100.0) * (length / 2.0));
                        return ElevationOnCurveMetres(elevationAtCurveStart, gradeIn, gradeOut, length,
                            stationMetres - curveStart);
                    }
                }
                // Is it inside the parabola centred on a (its back half)?
                if (i - 1 >= 0)
                {
                    double gradeBefore = GradePercent(points[i - 1], a);
                    double length = ClippedCurveLength(points, i, gradeBefore, gradeIn, speedKph, design);
                    double curveEnd = a.StationMetres + (length / 2.0);
                    if (length > 0.0 && stationMetres <= curveEnd)
                    {
                        double curveStart = a.StationMetres - (length / 2.0);
                        double elevationAtCurveStart = a.ElevationMetres
                            - ((gradeBefore / 100.0) * (length / 2.0));
                        return ElevationOnCurveMetres(elevationAtCurveStart, gradeBefore, gradeIn, length,
                            stationMetres - curveStart);
                    }
                }
                // On the tangent between the two curves.
                return a.ElevationMetres + ((gradeIn / 100.0) * (stationMetres - a.StationMetres));
            }
            return points[last].ElevationMetres;
        }

        /// <summary> The curve length actually used at an interior point: what L = K x A requires, clipped to half the
        /// shorter neighbouring tangent so adjacent parabolas cannot overlap. The clip is reported separately by the
        /// gate rather than silently applied -- a curve that had to be shortened is a road that does not meet the
        /// standard, and hiding that would defeat the whole model. </summary>
        public static double ClippedCurveLength(IList<VerticalPoint> points, int index,
            double gradeInPercent, double gradeOutPercent, double speedKph, VerticalDesignClass design)
        {
            double required = RequiredCurveLengthMetres(gradeInPercent, gradeOutPercent, speedKph, design);
            if (required <= 0.0) { return 0.0; }
            if (points == null || index <= 0 || index >= points.Count - 1) { return 0.0; }

            double back = points[index].StationMetres - points[index - 1].StationMetres;
            double forward = points[index + 1].StationMetres - points[index].StationMetres;

            // A curve of length L centred on the point reaches L/2 into each neighbouring tangent, and the neighbour's
            // own curve reaches L/2 back into the same tangent from the other end. So each curve may claim at most half
            // of the shorter tangent, which caps L at the whole of min(back, forward).
            double room = Math.Min(back, forward);
            return Math.Min(required, room);
        }

        /// <summary> Whether the curve at an interior point had to be shortened to fit between its neighbours. </summary>
        public static bool CurveWasClipped(IList<VerticalPoint> points, int index, double speedKph,
            VerticalDesignClass design)
        {
            if (points == null || index <= 0 || index >= points.Count - 1) { return false; }
            double gradeIn = GradePercent(points[index - 1], points[index]);
            double gradeOut = GradePercent(points[index], points[index + 1]);
            double required = RequiredCurveLengthMetres(gradeIn, gradeOut, speedKph, design);
            double used = ClippedCurveLength(points, index, gradeIn, gradeOut, speedKph, design);
            return used < required - 1e-9;
        }
    }
}

#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.City;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-97's gate: proves a junction's corners are DERIVED from a design vehicle rather than dialled in, and
    /// that the two things a corner can silently get wrong -- the plate's fit and the sight triangle -- are checked
    /// rather than assumed.
    ///
    /// The legacy code this replaces found corners by a nearest-point search between two nodes' construction edges with
    /// a 0.07 m match threshold and a 1.25 distance scale. It held no radius, no vehicle and no angle, so there is no
    /// old behaviour to preserve and nothing here is a second copy of anything.
    ///
    /// Four claims.
    ///
    /// The CATALOGUE is transcribed and cannot be derived, so it is checked the only way a transcription can be: each
    /// vehicle publishes TWO turning radii, and for a single unit the swept-path relation
    /// R_i = sqrt(R_o^2 - L^2) - track ties them together. A mistyped digit in either column breaks the tie. The gate
    /// asserts the tie for the car, the truck and the bus, and asserts that it is DELIBERATELY skipped for the two
    /// semitrailers, where the summed wheelbase exceeds the tractor's turning radius and the relation goes imaginary --
    /// which is itself the reason a semitrailer's published inside radius is 2.4 m.
    ///
    /// The CORNER is required to move when the vehicle changes. Every length in a solved corner is plane trigonometry on
    /// the radius and the deflection, and the gate recomputes all four independently. A corner built below the vehicle's
    /// requirement is required to report BOTH numbers, because a bare failure tells an author nothing about how much
    /// room the corner is short of.
    ///
    /// The PLATE is where skew bites. The two corners on one kerb are 2w/sin(theta) apart and consume 2R/sin(theta)
    /// between them, so the skew cancels and the plate closes exactly when the radius fits the narrower road's
    /// half-width -- the gate asserts that boundary to the millimetre from both sides. What skew does break is a plate
    /// built with ONE setback for all four corners: the acute and obtuse tangent lengths differ by 2R*cos/sin, and the
    /// gate asserts that number is zero at a square crossing and positive at every skew, which is exactly the gap a
    /// fixed-threshold search leaves.
    ///
    /// The SIGHT TRIANGLE is required to be speed-driven, because the manual already offers "lower the design speed on
    /// the approach, which shortens the legs" as a remedy for a blocked triangle, and that sentence has to be true.
    /// Halving the speed must halve the leg. A blocked triangle must name the object and the distance that has to be
    /// clear, and must leave the object alone -- the gate compares the obstruction list before and after.
    ///
    /// Report lines are prefixed RCMJUNCTION. </summary>
    public static class IntersectionGeometryGate
    {
        [MenuItem("Window/Road City Master/Testing/Intersection geometry (design vehicle corners, sight triangle)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.IntersectionGeometryGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMJUNCTION BEGIN\n");
            List<string> failures = new List<string>();

            CaseTheCatalogueIsOrderedAndPhysical(builder, failures);
            CaseThePublishedRadiiAgreeWithTheSweptPathRelation(builder, failures);
            CaseNamingADesignVehicleMovesEveryCorner(builder, failures);
            CaseACornerBelowRequirementReportsBothNumbers(builder, failures);
            CaseTheCornerLengthsAreThePlaneTrigonometry(builder, failures);
            CaseSkewSplitsTheCornersAndSquareCrossingsAgree(builder, failures);
            CaseThePlateClosesExactlyWhenTheRadiusFitsTheNarrowerRoad(builder, failures);
            CaseAFixedSetbackPlateGapsUnderSkew(builder, failures);
            CaseTheSightTriangleLegsFollowTheSpeedAndTheGap(builder, failures);
            CaseABlockedTriangleNamesTheObjectAndLeavesItAlone(builder, failures);
            CaseTheSkewLimitIsQuotedWithItsSource(builder, failures);
            CaseTheTurnBayTakesItsTaperFromTheOneOwner(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMJUNCTION FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMJUNCTION result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMJUNCTION END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        // ---- the catalogue ----

        /// <summary> The catalogue is transcribed, so this case checks the ORDER and the physical sense of it: a longer
        /// vehicle never needs a tighter corner, no vehicle's wheelbase exceeds its own length, and the inside radius
        /// COLLAPSES from the truck onward. That collapse is the fact that makes a freight junction broad -- a
        /// semitrailer's turning radius is barely larger than a bus's, but its inner rear wheel cuts to 2.4 m. </summary>
        private static void CaseTheCatalogueIsOrderedAndPhysical(StringBuilder builder, List<string> failures)
        {
            DesignVehicleKind[] kinds = DesignVehicles.All();
            Expect(kinds.Length == 5, Fmt("catalogue: expected 5 design vehicles, found {0}", kinds.Length), failures);

            double previousCorner = -1.0;
            for (int i = 0; i < kinds.Length; i++)
            {
                DesignVehicle v = DesignVehicles.For(kinds[i]);
                Expect(!string.IsNullOrEmpty(v.Name), Fmt("catalogue: {0} has no name", kinds[i]), failures);
                Expect(v.Kind == kinds[i],
                    Fmt("catalogue: {0} reports itself as {1}", kinds[i], v.Kind), failures);
                Expect(v.LengthMetres > v.EffectiveWheelbaseMetres,
                    Fmt("catalogue: {0} has a wheelbase {1:F2} m that is not shorter than its length {2:F2} m",
                        v.Name, v.EffectiveWheelbaseMetres, v.LengthMetres), failures);
                Expect(v.WidthMetres > 1.5 && v.WidthMetres < 3.0,
                    Fmt("catalogue: {0} width {1:F2} m is outside the range a road vehicle can be", v.Name,
                        v.WidthMetres), failures);
                Expect(v.FrontOverhangMetres > 0.0 && v.FrontOverhangMetres < 3.0,
                    Fmt("catalogue: {0} front overhang {1:F2} m is implausible", v.Name, v.FrontOverhangMetres),
                    failures);
                Expect(v.MinimumTurningRadiusMetres > v.MinimumInsideRadiusMetres,
                    Fmt("catalogue: {0} inside radius {1:F2} m is not inside its turning radius {2:F2} m", v.Name,
                        v.MinimumInsideRadiusMetres, v.MinimumTurningRadiusMetres), failures);
                Expect(v.SimpleCurveCornerRadiusMetres >= previousCorner - 1e-9,
                    Fmt("catalogue: {0} needs a tighter corner ({1:F2} m) than the vehicle before it ({2:F2} m)",
                        v.Name, v.SimpleCurveCornerRadiusMetres, previousCorner), failures);
                previousCorner = v.SimpleCurveCornerRadiusMetres;
            }

            double su = DesignVehicles.For(DesignVehicleKind.SingleUnitTruck).MinimumInsideRadiusMetres;
            double bus = DesignVehicles.For(DesignVehicleKind.CityTransitBus).MinimumInsideRadiusMetres;
            double wb12 = DesignVehicles.For(DesignVehicleKind.IntermediateSemitrailer).MinimumInsideRadiusMetres;
            double wb20 = DesignVehicles.For(DesignVehicleKind.InterstateSemitrailer).MinimumInsideRadiusMetres;
            Expect(su > bus && bus > wb12 && wb12 > wb20,
                Fmt("catalogue: the inside radius does not collapse with length ({0:F2}, {1:F2}, {2:F2}, {3:F2} m)",
                    su, bus, wb12, wb20), failures);

            Line(builder, "catalogue", Fmt(
                "5 vehicles, corner radii {0:F1} to {1:F1} m, inside radius collapses {2:F1} -> {3:F1} m",
                DesignVehicles.For(DesignVehicleKind.PassengerCar).SimpleCurveCornerRadiusMetres, previousCorner,
                su, wb20));
        }

        /// <summary> The transcription check that makes the catalogue verifiable. For a single unit the two published
        /// radii must satisfy R_i = sqrt(R_o^2 - L^2) - track; the gate allows a third of a metre, which is the spread
        /// between the published pairs and far tighter than any plausible typo. For a combination the relation is
        /// required to be UNUSABLE -- if it ever started returning a positive number for a semitrailer, either the
        /// wheelbase or the turning radius has been edited into something the vehicle is not. </summary>
        private static void CaseThePublishedRadiiAgreeWithTheSweptPathRelation(StringBuilder builder,
            List<string> failures)
        {
            const double tolerance = 0.35;
            DesignVehicleKind[] kinds = DesignVehicles.All();
            int checkedUnits = 0;
            double worst = 0.0;
            string worstName = "(none)";

            for (int i = 0; i < kinds.Length; i++)
            {
                DesignVehicle v = DesignVehicles.For(kinds[i]);
                double swept = IntersectionGeometry.SweptInnerRadiusMetres(v);

                if (v.IsCombination)
                {
                    Expect(v.EffectiveWheelbaseMetres >= v.MinimumTurningRadiusMetres,
                        Fmt("swept pair: {0} is marked a combination but its summed wheelbase {1:F2} m is inside its " +
                            "turning radius {2:F2} m, so the single-unit relation would apply and is not being checked",
                            v.Name, v.EffectiveWheelbaseMetres, v.MinimumTurningRadiusMetres), failures);
                    Expect(swept == 0.0,
                        Fmt("swept pair: {0} returned {1:R} m instead of zero from an imaginary relation", v.Name,
                            swept), failures);
                    continue;
                }

                double difference = Math.Abs(swept - v.MinimumInsideRadiusMetres);
                Expect(difference <= tolerance,
                    Fmt("swept pair: {0} published inside radius {1:F2} m against {2:F2} m from the relation " +
                        "(sqrt({3:F2}^2 - {4:F2}^2) - {5:F2}), difference {6:F2} m exceeds {7:F2} m", v.Name,
                        v.MinimumInsideRadiusMetres, swept, v.MinimumTurningRadiusMetres, v.EffectiveWheelbaseMetres,
                        v.WidthMetres, difference, tolerance), failures);
                if (difference > worst) { worst = difference; worstName = v.Name; }
                checkedUnits++;
            }

            Expect(checkedUnits == 3,
                Fmt("swept pair: expected 3 single units to check, checked {0}", checkedUnits), failures);
            Line(builder, "swept pair", Fmt(
                "3 single units tied to the relation within {0:F2} m (worst {1}), 2 combinations correctly skipped",
                worst, worstName));
        }

        // ---- the corner ----

        /// <summary> Naming a design vehicle has to move the corners, and it has to move ALL of them: this is the single
        /// authored decision the whole of RM-97 reduces corner geometry to. The chain car -> truck -> intermediate
        /// semitrailer -> interstate semitrailer must widen strictly at every step, and at the reference 90-degree
        /// deflection each corner must come out at exactly the vehicle's published simple-curve radius, which is what
        /// proves the radius is read from the catalogue rather than a constant that happens to look plausible. The bus
        /// and the single-unit truck are published with the SAME corner radius; the equality is asserted deliberately so
        /// that if either figure is ever edited the tie breaking is noticed here. </summary>
        private static void CaseNamingADesignVehicleMovesEveryCorner(StringBuilder builder, List<string> failures)
        {
            DesignVehicleKind[] chain =
            {
                DesignVehicleKind.PassengerCar,
                DesignVehicleKind.SingleUnitTruck,
                DesignVehicleKind.IntermediateSemitrailer,
                DesignVehicleKind.InterstateSemitrailer,
            };

            double previous = -1.0;
            for (int i = 0; i < chain.Length; i++)
            {
                DesignVehicle v = DesignVehicles.For(chain[i]);
                CornerSolution[] corners = IntersectionGeometry.SolveCorners(chain[i], 90.0);
                Expect(corners.Length == 4,
                    Fmt("vehicle drives corner: {0} produced {1} corners", v.Name, corners.Length), failures);

                for (int c = 0; c < corners.Length; c++)
                {
                    Expect(Math.Abs(corners[c].RadiusMetres - v.SimpleCurveCornerRadiusMetres) < 1e-9,
                        Fmt("vehicle drives corner: {0} corner {1} came out at {2:F3} m, not the published {3:F3} m",
                            v.Name, c, corners[c].RadiusMetres, v.SimpleCurveCornerRadiusMetres), failures);
                    Expect(corners[c].VehicleName == v.Name,
                        Fmt("vehicle drives corner: corner {0} reports '{1}' instead of '{2}'", c,
                            corners[c].VehicleName, v.Name), failures);
                    Expect(corners[c].RadiusMetres > previous,
                        Fmt("vehicle drives corner: {0} corner {1} at {2:F3} m did not widen on the previous vehicle's " +
                            "{3:F3} m", v.Name, c, corners[c].RadiusMetres, previous), failures);
                }
                previous = v.SimpleCurveCornerRadiusMetres;
            }

            double su = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, 90.0, true).RadiusMetres;
            double bus = IntersectionGeometry.SolveCorner(DesignVehicleKind.CityTransitBus, 90.0, true).RadiusMetres;
            Expect(Math.Abs(su - bus) < 1e-9,
                Fmt("vehicle drives corner: the truck and the bus are published with the same corner radius but came " +
                    "out at {0:F3} m and {1:F3} m", su, bus), failures);

            Line(builder, "vehicle drives corner", Fmt(
                "car {0:F1} m -> truck {1:F1} m -> WB-12 {2:F1} m -> WB-20 {3:F1} m, all four corners each",
                IntersectionGeometry.SolveCorner(DesignVehicleKind.PassengerCar, 90.0, true).RadiusMetres, su,
                IntersectionGeometry.SolveCorner(DesignVehicleKind.IntermediateSemitrailer, 90.0, true).RadiusMetres,
                IntersectionGeometry.SolveCorner(DesignVehicleKind.InterstateSemitrailer, 90.0, true).RadiusMetres));
        }

        /// <summary> A corner that is too tight for its design vehicle must report BOTH numbers and the shortfall between
        /// them. A gate that only checked for a failure flag would pass an implementation that said "corner inadequate"
        /// and left the author to guess whether it was short by 100 mm or by 9 m. </summary>
        private static void CaseACornerBelowRequirementReportsBothNumbers(StringBuilder builder, List<string> failures)
        {
            DesignVehicle bus = DesignVehicles.For(DesignVehicleKind.CityTransitBus);
            CornerSolution tight = IntersectionGeometry.SolveCorner(DesignVehicleKind.CityTransitBus, 90.0, true, 6.0);

            Expect(!tight.Adequate,
                Fmt("shortfall: a 6.00 m corner was reported adequate for the {0}, which needs {1:F2} m", bus.Name,
                    tight.RequiredRadiusMetres), failures);
            Expect(Math.Abs(tight.RadiusMetres - 6.0) < 1e-9,
                Fmt("shortfall: the built radius was reported as {0:F3} m instead of the 6.00 m passed in",
                    tight.RadiusMetres), failures);
            Expect(Math.Abs(tight.RequiredRadiusMetres - bus.SimpleCurveCornerRadiusMetres) < 1e-9,
                Fmt("shortfall: the requirement was reported as {0:F3} m instead of the published {1:F3} m",
                    tight.RequiredRadiusMetres, bus.SimpleCurveCornerRadiusMetres), failures);
            Expect(Math.Abs(tight.ShortfallMetres - (bus.SimpleCurveCornerRadiusMetres - 6.0)) < 1e-9,
                Fmt("shortfall: reported {0:F3} m short instead of {1:F3} m", tight.ShortfallMetres,
                    bus.SimpleCurveCornerRadiusMetres - 6.0), failures);
            Expect(tight.SweptInnerRadiusMetres > 0.0,
                "shortfall: the corner carries no swept inner radius, so a report cannot say why the requirement is " +
                "what it is", failures);

            CornerSolution ample = IntersectionGeometry.SolveCorner(DesignVehicleKind.CityTransitBus, 90.0, true, 25.0);
            Expect(ample.Adequate && ample.ShortfallMetres == 0.0,
                Fmt("shortfall: a 25.00 m corner was reported {0} short of {1:F2} m", ample.ShortfallMetres,
                    ample.RequiredRadiusMetres), failures);

            CornerSolution derived = IntersectionGeometry.SolveCorner(DesignVehicleKind.CityTransitBus, 90.0, true, 0.0);
            Expect(derived.Adequate && Math.Abs(derived.RadiusMetres - derived.RequiredRadiusMetres) < 1e-9,
                Fmt("shortfall: a derived corner came out at {0:F3} m against a requirement of {1:F3} m",
                    derived.RadiusMetres, derived.RequiredRadiusMetres), failures);

            Line(builder, "shortfall", Fmt(
                "6.00 m corner for the {0} reported short by {1:F2} m against a requirement of {2:F2} m", bus.Name,
                tight.ShortfallMetres, tight.RequiredRadiusMetres));
        }

        /// <summary> Every length a corner reports is recomputed here from the radius and the deflection alone, so the
        /// solver cannot quietly carry a fudge factor: tangent R*tan(d/2), middle ordinate R*(1-cos(d/2)), external
        /// R*(sec(d/2)-1), arc R*d. Swept across seven crossing angles, both sides, three radii. </summary>
        private static void CaseTheCornerLengthsAreThePlaneTrigonometry(StringBuilder builder, List<string> failures)
        {
            double[] angles = { 90.0, 85.0, 80.0, 75.0, 70.0, 65.0, 60.0 };
            double[] radii = { 7.5, 15.0, 30.0 };
            const double degreesToRadians = Math.PI / 180.0;
            int compared = 0;
            double worst = 0.0;

            for (int a = 0; a < angles.Length; a++)
            {
                for (int r = 0; r < radii.Length; r++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        bool acute = side == 0;
                        CornerSolution s = IntersectionGeometry.SolveCorner(DesignVehicleKind.PassengerCar, angles[a],
                            acute, radii[r]);
                        double half = 0.5 * s.DeflectionDegrees * degreesToRadians;
                        double expectedTangent = radii[r] * Math.Tan(half);
                        double expectedOrdinate = radii[r] * (1.0 - Math.Cos(half));
                        double expectedExternal = radii[r] * (1.0 / Math.Cos(half) - 1.0);
                        double expectedArc = radii[r] * s.DeflectionDegrees * degreesToRadians;

                        double worstHere = Math.Max(Math.Abs(s.TangentLengthMetres - expectedTangent),
                            Math.Max(Math.Abs(s.MiddleOrdinateMetres - expectedOrdinate),
                                Math.Max(Math.Abs(s.ExternalDistanceMetres - expectedExternal),
                                    Math.Abs(s.ArcLengthMetres - expectedArc))));
                        Expect(worstHere < 1e-9,
                            Fmt("corner trigonometry: crossing {0:F0} deg, R {1:F1} m, {2} side is off by {3:R} m",
                                angles[a], radii[r], acute ? "acute" : "obtuse", worstHere), failures);
                        Expect(s.MiddleOrdinateMetres <= radii[r] + 1e-9
                               && s.TangentLengthMetres >= s.MiddleOrdinateMetres - 1e-9
                               && s.ExternalDistanceMetres >= s.MiddleOrdinateMetres - 1e-9,
                            Fmt("corner trigonometry: crossing {0:F0} deg, R {1:F1} m gives an impossible set " +
                                "(T {2:F3}, M {3:F3}, E {4:F3})", angles[a], radii[r], s.TangentLengthMetres,
                                s.MiddleOrdinateMetres, s.ExternalDistanceMetres), failures);
                        if (worstHere > worst) { worst = worstHere; }
                        compared++;
                    }
                }
            }

            Line(builder, "corner trigonometry", Fmt(
                "{0} corners recomputed from R and deflection alone, worst disagreement {1:R} m", compared, worst));
        }

        /// <summary> At a square crossing all four corners are the same corner; at any skew two of them turn through more
        /// than a right angle and their tangent lengths run away by exactly 1/tan^2(theta/2). At 60 degrees that is a
        /// factor of three, which is the size of the error a single-setback plate makes. The case also proves the folding
        /// of the crossing angle: a junction described as 120 degrees IS the 60-degree junction, and describing it the
        /// other way must not produce a different set of corners. </summary>
        private static void CaseSkewSplitsTheCornersAndSquareCrossingsAgree(StringBuilder builder,
            List<string> failures)
        {
            const double degreesToRadians = Math.PI / 180.0;

            CornerSolution squareAcute = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, 90.0, true);
            CornerSolution squareObtuse = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, 90.0,
                false);
            Expect(Math.Abs(squareAcute.DeflectionDegrees - 90.0) < 1e-9
                   && Math.Abs(squareObtuse.DeflectionDegrees - 90.0) < 1e-9,
                Fmt("skew split: a square crossing gave deflections {0:F4} and {1:F4} deg",
                    squareAcute.DeflectionDegrees, squareObtuse.DeflectionDegrees), failures);
            Expect(Math.Abs(squareAcute.TangentLengthMetres - squareObtuse.TangentLengthMetres) < 1e-9,
                Fmt("skew split: a square crossing gave different setbacks ({0:F4} against {1:F4} m)",
                    squareAcute.TangentLengthMetres, squareObtuse.TangentLengthMetres), failures);

            double[] angles = { 85.0, 75.0, 65.0, 60.0 };
            for (int i = 0; i < angles.Length; i++)
            {
                // A fixed radius is passed on both sides on purpose: the setback ratio is a claim about the PLATE at a
                // given radius. Letting each corner derive its own radius would fold the requirement scaling into the
                // ratio and test two things at once.
                CornerSolution acute = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, angles[i],
                    true, 15.0);
                CornerSolution obtuse = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, angles[i],
                    false, 15.0);
                Expect(Math.Abs(acute.DeflectionDegrees + obtuse.DeflectionDegrees - 180.0) < 1e-9,
                    Fmt("skew split: at {0:F0} deg the two deflections sum to {1:F6} instead of 180", angles[i],
                        acute.DeflectionDegrees + obtuse.DeflectionDegrees), failures);
                Expect(acute.DeflectionDegrees > 90.0 && obtuse.DeflectionDegrees < 90.0,
                    Fmt("skew split: at {0:F0} deg the acute corner turns {1:F3} deg and the obtuse {2:F3} deg",
                        angles[i], acute.DeflectionDegrees, obtuse.DeflectionDegrees), failures);

                double tanHalf = Math.Tan(0.5 * angles[i] * degreesToRadians);
                double expectedRatio = 1.0 / (tanHalf * tanHalf);
                double ratio = acute.TangentLengthMetres / obtuse.TangentLengthMetres;
                Expect(Math.Abs(ratio - expectedRatio) < 1e-9,
                    Fmt("skew split: at {0:F0} deg the setback ratio is {1:F6}, not 1/tan^2(theta/2) = {2:F6}",
                        angles[i], ratio, expectedRatio), failures);

                CornerSolution folded = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck,
                    180.0 - angles[i], true, 15.0);
                Expect(Math.Abs(folded.TangentLengthMetres - acute.TangentLengthMetres) < 1e-9,
                    Fmt("skew split: describing the junction as {0:F0} deg instead of {1:F0} deg changed the setback " +
                        "({2:F4} against {3:F4} m)", 180.0 - angles[i], angles[i], folded.TangentLengthMetres,
                        acute.TangentLengthMetres), failures);
            }

            CornerSolution sixty = IntersectionGeometry.SolveCorner(DesignVehicleKind.SingleUnitTruck, 60.0, true,
                15.0);
            Line(builder, "skew split", Fmt(
                "square corners identical; at 60 deg the acute corner turns {0:F0} deg and sets back {1:F2} m on a " +
                "15.00 m radius, three times the obtuse", sixty.DeflectionDegrees, sixty.TangentLengthMetres));
        }

        // ---- the corner-cut plate ----

        /// <summary> The plate's closure condition, asserted from both sides to the millimetre.
        ///
        /// The two corners on one kerb sit 2w/sin(theta) apart and consume T_acute + T_obtuse = 2R/sin(theta) between
        /// them, so the plate closes exactly when R &lt;= w -- independent of skew. The case asserts the sum identity
        /// first (which is what makes the skew cancel), then walks the boundary a millimetre either side, then repeats it
        /// with the two roads swapped so the narrower road governs. </summary>
        private static void CaseThePlateClosesExactlyWhenTheRadiusFitsTheNarrowerRoad(StringBuilder builder,
            List<string> failures)
        {
            const double degreesToRadians = Math.PI / 180.0;
            double[] angles = { 90.0, 80.0, 70.0, 60.0 };
            const double mainHalf = 9.0;
            const double crossHalf = 7.0;

            for (int i = 0; i < angles.Length; i++)
            {
                double sin = Math.Sin(angles[i] * degreesToRadians);
                CornerPlateFit fit = IntersectionGeometry.FitPlate(angles[i], mainHalf, crossHalf, 6.0);

                Expect(Math.Abs(fit.AvailableKerbLengthMetres - 2.0 * crossHalf / sin) < 1e-9,
                    Fmt("plate closure: at {0:F0} deg the available kerb is {1:F6} m, not 2w/sin = {2:F6} m", angles[i],
                        fit.AvailableKerbLengthMetres, 2.0 * crossHalf / sin), failures);
                Expect(Math.Abs(fit.RequiredKerbLengthMetres - 2.0 * 6.0 / sin) < 1e-9,
                    Fmt("plate closure: at {0:F0} deg the required kerb is {1:F6} m, not 2R/sin = {2:F6} m", angles[i],
                        fit.RequiredKerbLengthMetres, 2.0 * 6.0 / sin), failures);
                Expect(Math.Abs(fit.AcuteTangentLengthMetres + fit.ObtuseTangentLengthMetres
                                - fit.RequiredKerbLengthMetres) < 1e-9,
                    Fmt("plate closure: at {0:F0} deg the two setbacks sum to {1:F6} m instead of the required " +
                        "{2:F6} m -- the identity the closure condition rests on has broken", angles[i],
                        fit.AcuteTangentLengthMetres + fit.ObtuseTangentLengthMetres, fit.RequiredKerbLengthMetres),
                    failures);

                CornerPlateFit exact = IntersectionGeometry.FitPlate(angles[i], mainHalf, crossHalf, crossHalf);
                CornerPlateFit over = IntersectionGeometry.FitPlate(angles[i], mainHalf, crossHalf, crossHalf + 0.001);
                CornerPlateFit under = IntersectionGeometry.FitPlate(angles[i], mainHalf, crossHalf, crossHalf - 0.001);
                Expect(exact.Meets && under.Meets && !over.Meets,
                    Fmt("plate closure: at {0:F0} deg the boundary at R = w = {1:F3} m is wrong (exact {2}, under {3}, " +
                        "over {4})", angles[i], crossHalf, exact.Meets, under.Meets, over.Meets), failures);

                CornerPlateFit swapped = IntersectionGeometry.FitPlate(angles[i], crossHalf, mainHalf, mainHalf + 0.001);
                Expect(!swapped.Meets,
                    Fmt("plate closure: at {0:F0} deg the wider road's kerb accepted R = {1:F3} m", angles[i],
                        mainHalf + 0.001), failures);
            }

            Expect(Math.Abs(IntersectionGeometry.LargestClosingRadiusMetres(mainHalf, crossHalf) - crossHalf) < 1e-9,
                "plate closure: the largest closing radius is not the narrower road's half-width", failures);

            Line(builder, "plate closure", Fmt(
                "4 crossing angles: setbacks sum to 2R/sin, closure boundary exactly at R = {0:F2} m either way round",
                crossHalf));
        }

        /// <summary> What skew actually breaks. The plate closes at any skew, so the failure is not a fold -- it is a plate
        /// built with ONE setback for all four corners, which is what a nearest-point search with a fixed match threshold
        /// produces. The two setbacks on one kerb differ by exactly 2R*cos(theta)/sin(theta): zero at a square crossing,
        /// 8.66 m on a 7.5 m radius at 60 degrees. That gap is wider than a traffic lane, and it is entirely invisible to
        /// a proximity search that never knew the crossing angle. </summary>
        private static void CaseAFixedSetbackPlateGapsUnderSkew(StringBuilder builder, List<string> failures)
        {
            const double degreesToRadians = Math.PI / 180.0;
            double[] angles = { 90.0, 85.0, 75.0, 65.0, 60.0 };
            const double radius = 7.5;

            CornerPlateFit square = IntersectionGeometry.FitPlate(90.0, 9.0, 9.0, radius);
            Expect(square.FixedSetbackGapMetres < 1e-9,
                Fmt("fixed setback: a square crossing reports a {0:R} m gap", square.FixedSetbackGapMetres), failures);

            double previous = -1.0;
            for (int i = 0; i < angles.Length; i++)
            {
                CornerPlateFit fit = IntersectionGeometry.FitPlate(angles[i], 9.0, 9.0, radius);
                double theta = angles[i] * degreesToRadians;
                double expected = 2.0 * radius * Math.Cos(theta) / Math.Sin(theta);
                Expect(Math.Abs(fit.FixedSetbackGapMetres - expected) < 1e-9,
                    Fmt("fixed setback: at {0:F0} deg the gap is {1:F6} m, not 2R*cos/sin = {2:F6} m", angles[i],
                        fit.FixedSetbackGapMetres, expected), failures);
                Expect(fit.FixedSetbackGapMetres >= previous - 1e-9,
                    Fmt("fixed setback: the gap shrank as the crossing sharpened ({0:F4} m at {1:F0} deg after " +
                        "{2:F4} m)", fit.FixedSetbackGapMetres, angles[i], previous), failures);
                previous = fit.FixedSetbackGapMetres;
                Expect(fit.Meets,
                    Fmt("fixed setback: at {0:F0} deg a {1:F1} m radius on a {2:F1} m half-width failed to close",
                        angles[i], radius, 9.0), failures);
            }

            CornerPlateFit sixty = IntersectionGeometry.FitPlate(60.0, 9.0, 9.0, radius);
            Expect(sixty.FixedSetbackGapMetres > 8.0,
                Fmt("fixed setback: at 60 deg the gap is only {0:F3} m, so the case is not exercising the failure it " +
                    "claims to", sixty.FixedSetbackGapMetres), failures);

            Line(builder, "fixed setback", Fmt(
                "gap 0.00 m at 90 deg rising to {0:F2} m at 60 deg on a {1:F1} m radius, always 2R*cos/sin",
                sixty.FixedSetbackGapMetres, radius));
        }

        /// <summary> Case 9. Every sight-triangle leg is 0.278 * V * t and nothing else. That matters beyond arithmetic:
        /// the refusal text for a blocked triangle offers "lower the design speed on the approach, which shortens the
        /// legs", and this case is what makes that sentence true rather than hopeful. Halve the speed, halve the leg.
        /// The gap itself moves with the vehicle and with the number of lanes crossed, both in the published direction:
        /// a bus needs longer than a car, a semitrailer longer than a bus, and a wider road longer than a narrow one.
        /// </summary>
        private static void CaseTheSightTriangleLegsFollowTheSpeedAndTheGap(StringBuilder builder, List<string> failures)
        {
            IntersectionGeometryClass cls = IntersectionGeometryClass.Default();

            double carLeft = IntersectionGeometry.GapSeconds(DesignVehicleKind.PassengerCar, 2.0, true, cls);
            double carRight = IntersectionGeometry.GapSeconds(DesignVehicleKind.PassengerCar, 2.0, false, cls);
            Expect(Math.Abs(carLeft - 7.5) < 1e-9,
                Fmt("sight gap: a car turning left across two lanes wants {0:R} s, not 7.5 s", carLeft), failures);
            Expect(Math.Abs(carRight - 6.5) < 1e-9,
                Fmt("sight gap: a car turning right across two lanes wants {0:R} s, not 6.5 s", carRight), failures);

            double leg = IntersectionGeometry.SightDistanceMetres(60.0, carLeft, cls);
            Expect(Math.Abs(leg - 125.1) < 1e-9,
                Fmt("sight leg: 60 km/h on a 7.5 s gap gives {0:F6} m, not the worked 125.1 m", leg), failures);
            double half = IntersectionGeometry.SightDistanceMetres(30.0, carLeft, cls);
            Expect(Math.Abs(half - leg * 0.5) < 1e-9,
                Fmt("sight leg: halving the design speed gave {0:F6} m, not half of {1:F6} m", half, leg), failures);

            double bus = IntersectionGeometry.GapSeconds(DesignVehicleKind.CityTransitBus, 2.0, true, cls);
            double semi = IntersectionGeometry.GapSeconds(DesignVehicleKind.InterstateSemitrailer, 2.0, true, cls);
            Expect(bus > carLeft + 1e-9,
                Fmt("sight gap: a bus ({0:F2} s) does not want longer than a car ({1:F2} s)", bus, carLeft), failures);
            Expect(semi > bus + 1e-9,
                Fmt("sight gap: a semitrailer ({0:F2} s) does not want longer than a bus ({1:F2} s)", semi, bus),
                failures);

            double four = IntersectionGeometry.GapSeconds(DesignVehicleKind.PassengerCar, 4.0, true, cls);
            Expect(Math.Abs(four - (carLeft + 1.0)) < 1e-9,
                Fmt("sight gap: crossing four lanes instead of two added {0:F3} s, not two half-second steps",
                    four - carLeft), failures);
            double one = IntersectionGeometry.GapSeconds(DesignVehicleKind.PassengerCar, 1.0, true, cls);
            Expect(Math.Abs(one - carLeft) < 1e-9,
                Fmt("sight gap: crossing fewer lanes than the tabulated two SHORTENED the gap to {0:F3} s; the " +
                    "adjustment is an addition only", one), failures);

            double approach = IntersectionGeometry.ApproachSightTriangleLegMetres(60.0, cls);
            Expect(Math.Abs(approach - 0.278 * 60.0 * cls.approachSightTriangleSeconds) < 1e-9,
                Fmt("approach triangle: leg {0:F4} m does not use the {1:F1} s uncontrolled travel time", approach,
                    cls.approachSightTriangleSeconds), failures);

            Line(builder, "sight legs", Fmt(
                "60 km/h: left {0:F2} m, right {1:F2} m, uncontrolled approach {2:F2} m; gaps car {3:F1} s, bus " +
                "{4:F1} s, WB-20 {5:F1} s",
                leg, IntersectionGeometry.SightDistanceMetres(60.0, carRight, cls), approach, carLeft, bus, semi));
        }

        /// <summary> Case 10. A blocked triangle is REPORTED, not repaired. The solver names the object, says which
        /// triangle it stands in and how many metres have to be clear, and touches nothing -- an author who placed a
        /// kiosk gets told the kiosk is the problem and decides what to do about it. Two boundaries are pinned here as
        /// well: an object shorter than the standard's object height is not an obstruction (a kerb never blocks a sight
        /// line), and an object beyond the leg is outside the triangle no matter how tall it is. </summary>
        private static void CaseABlockedTriangleNamesTheObjectAndLeavesItAlone(StringBuilder builder,
            List<string> failures)
        {
            IntersectionGeometryClass cls = IntersectionGeometryClass.Default();
            List<SightObstruction> objects = new List<SightObstruction>
            {
                new SightObstruction("corner kiosk", -40.0, -2.0, 3.0),
                new SightObstruction("kerb upstand", -40.0, -2.0, 0.15),
                new SightObstruction("tree beyond the leg", -400.0, -2.0, 6.0),
                new SightObstruction("advertising hoarding", 30.0, -2.0, 2.5)
            };
            double[] beforeAlong = new double[objects.Count];
            double[] beforeToward = new double[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                beforeAlong[i] = objects[i].AlongMajorMetres;
                beforeToward[i] = objects[i].TowardApproachMetres;
            }

            SightTriangleVerdict v = IntersectionGeometry.SolveSightTriangle(60.0, 2.0,
                DesignVehicleKind.PassengerCar, objects, cls);

            Expect(!v.Clear, "blocked triangle: two objects stand in the triangles and the verdict came back clear",
                failures);
            Expect(v.BlockingNames.Length == 2,
                Fmt("blocked triangle: {0} objects were reported, not the kiosk and the hoarding",
                    v.BlockingNames.Length), failures);
            if (v.BlockingNames.Length == 2)
            {
                Expect(v.BlockingNames[0] == "corner kiosk" && v.BlockingSides[0] == -1,
                    Fmt("blocked triangle: the first report is '{0}' on side {1}, not the kiosk on the left",
                        v.BlockingNames[0], v.BlockingSides[0]), failures);
                Expect(Math.Abs(v.BlockingRequiredClearMetres[0] - v.LeftLegMetres) < 1e-9,
                    Fmt("blocked triangle: the kiosk asks for {0:F3} m clear, not the left leg {1:F3} m",
                        v.BlockingRequiredClearMetres[0], v.LeftLegMetres), failures);
                Expect(v.BlockingNames[1] == "advertising hoarding" && v.BlockingSides[1] == 1,
                    Fmt("blocked triangle: the second report is '{0}' on side {1}, not the hoarding on the right",
                        v.BlockingNames[1], v.BlockingSides[1]), failures);
                Expect(Math.Abs(v.BlockingRequiredClearMetres[1] - v.RightLegMetres) < 1e-9,
                    Fmt("blocked triangle: the hoarding asks for {0:F3} m clear, not the right leg {1:F3} m",
                        v.BlockingRequiredClearMetres[1], v.RightLegMetres), failures);
            }

            CaseNothingInTheTriangleWasMoved(builder, failures, objects, beforeAlong, beforeToward, v, cls);
        }

        /// <summary> The second half of case 10, split only to keep one method readable: the author's objects are still
        /// exactly where the author put them, the low kerb and the far tree were passed over, and the description a
        /// human reads carries both offending names. </summary>
        private static void CaseNothingInTheTriangleWasMoved(StringBuilder builder, List<string> failures,
            List<SightObstruction> objects, double[] beforeAlong, double[] beforeToward, SightTriangleVerdict v,
            IntersectionGeometryClass cls)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                bool still = Math.Abs(objects[i].AlongMajorMetres - beforeAlong[i]) < 1e-12
                             && Math.Abs(objects[i].TowardApproachMetres - beforeToward[i]) < 1e-12;
                Expect(still, Fmt("blocked triangle: '{0}' moved from ({1:F3}, {2:F3}) to ({3:F3}, {4:F3}); the solver " +
                    "reports, it does not relocate an author's object", objects[i].Name, beforeAlong[i],
                    beforeToward[i], objects[i].AlongMajorMetres, objects[i].TowardApproachMetres), failures);
            }

            for (int i = 0; i < v.BlockingNames.Length; i++)
            {
                Expect(v.BlockingNames[i] != "kerb upstand",
                    "blocked triangle: a 0.15 m kerb was reported as an obstruction", failures);
                Expect(v.BlockingNames[i] != "tree beyond the leg",
                    "blocked triangle: an object 400 m away was reported as standing inside a 125 m triangle",
                    failures);
            }

            Expect(!string.IsNullOrEmpty(v.Description) && v.Description.Contains("corner kiosk")
                   && v.Description.Contains("advertising hoarding"),
                "blocked triangle: the human-readable description does not name both offending objects", failures);

            List<SightObstruction> harmless = new List<SightObstruction>
            {
                new SightObstruction("kerb upstand", -40.0, -2.0, 0.15),
                new SightObstruction("tree beyond the leg", -400.0, -2.0, 6.0)
            };
            SightTriangleVerdict clear = IntersectionGeometry.SolveSightTriangle(60.0, 2.0,
                DesignVehicleKind.PassengerCar, harmless, cls);
            Expect(clear.Clear && clear.BlockingNames.Length == 0,
                Fmt("blocked triangle: a low kerb and a distant tree produced {0} reports", clear.BlockingNames.Length),
                failures);
            SightTriangleVerdict empty = IntersectionGeometry.SolveSightTriangle(60.0, 2.0,
                DesignVehicleKind.PassengerCar, null, cls);
            Expect(empty.Clear, "blocked triangle: a null obstruction list did not come back clear", failures);

            Line(builder, "blocked triangle", Fmt(
                "'{0}' left ({1:F2} m must be clear) and '{2}' right ({3:F2} m); {4} object(s) skipped, none moved",
                v.BlockingNames.Length > 0 ? v.BlockingNames[0] : "-",
                v.BlockingNames.Length > 0 ? v.BlockingRequiredClearMetres[0] : 0.0,
                v.BlockingNames.Length > 1 ? v.BlockingNames[1] : "-",
                v.BlockingNames.Length > 1 ? v.BlockingRequiredClearMetres[1] : 0.0,
                objects.Count - v.BlockingNames.Length));
        }

        /// <summary> Case 11. The skew limit refuses, and the refusal is checkable. A reader who disagrees with the 60
        /// degree floor can go and read the sentence it came from, because the verdict carries that sentence -- so the
        /// number is a quotation rather than a preference. The fold is pinned too: a junction described as 120 degrees is
        /// the same junction as one described as 60, and describing it the other way round must not change the answer.
        /// </summary>
        private static void CaseTheSkewLimitIsQuotedWithItsSource(StringBuilder builder, List<string> failures)
        {
            IntersectionGeometryClass cls = IntersectionGeometryClass.Default();

            SkewVerdict square = IntersectionGeometry.CheckSkew(90.0, cls);
            Expect(square.Accepted, "skew: a square crossing was refused", failures);
            Expect(Math.Abs(square.SkewFromPerpendicularDegrees) < 1e-9,
                Fmt("skew: a square crossing reports {0:R} deg of skew", square.SkewFromPerpendicularDegrees), failures);

            SkewVerdict atLimit = IntersectionGeometry.CheckSkew(60.0, cls);
            Expect(atLimit.Accepted, "skew: exactly 60 deg was refused; the limit is a floor, not an exclusive bound",
                failures);
            Expect(Math.Abs(atLimit.SkewFromPerpendicularDegrees - 30.0) < 1e-9,
                Fmt("skew: 60 deg reports {0:F4} deg from perpendicular, not 30", atLimit.SkewFromPerpendicularDegrees),
                failures);

            double[] refused = { 59.0, 45.0, 30.0, 5.0 };
            for (int i = 0; i < refused.Length; i++)
            {
                SkewVerdict bad = IntersectionGeometry.CheckSkew(refused[i], cls);
                Expect(!bad.Accepted, Fmt("skew: {0:F0} deg was accepted", refused[i]), failures);
                Expect(!string.IsNullOrEmpty(bad.Reason) && bad.Reason.Contains(cls.crossingAngleSource),
                    Fmt("skew: the refusal at {0:F0} deg does not quote its source", refused[i]), failures);
            }

            double[] folded = { 120.0, 240.0, -60.0, 300.0 };
            for (int i = 0; i < folded.Length; i++)
            {
                SkewVerdict f = IntersectionGeometry.CheckSkew(folded[i], cls);
                Expect(f.Accepted && Math.Abs(f.CrossingAngleDegrees - 60.0) < 1e-9,
                    Fmt("skew: {0:F0} deg folded to {1:F4} deg instead of 60, accepted {2}", folded[i],
                        f.CrossingAngleDegrees, f.Accepted), failures);
            }

            Expect(!string.IsNullOrEmpty(square.Source) && square.Source.Contains("60"),
                "skew: the verdict's source string does not carry the limit it justifies", failures);
            Expect(Math.Abs(cls.minimumCrossingAngleDegrees - 60.0) < 1e-9
                   && Math.Abs(cls.preferredCrossingAngleDegrees - 90.0) < 1e-9,
                Fmt("skew: the class holds minimum {0:F1} deg and preferred {1:F1} deg",
                    cls.minimumCrossingAngleDegrees, cls.preferredCrossingAngleDegrees), failures);

            Line(builder, "skew limit", Fmt(
                "minimum {0:F0} deg (preferred {1:F0}); 59 deg refused, 120 deg folds to 60 and is accepted; source " +
                "{2} characters", cls.minimumCrossingAngleDegrees, cls.preferredCrossingAngleDegrees,
                cls.crossingAngleSource.Length));
        }

        /// <summary> Case 12. RM-97 owns how much queue a turn bay has to store; it owns nothing about the taper that
        /// opens the bay. That belongs to LaneModel, and the only honest way to prove RM-97 did not quietly grow a second
        /// copy of the MUTCD relation is to subtract: flare length minus storage must equal LaneModel's own taper to the
        /// last bit. The assembled lane is checked in the same breath, because a bay closes AT the stop line -- it has an
        /// opening taper and no closing one.
        ///
        /// The stop line is put a long way downstream on purpose. AuxiliaryLane.Bay clamps a negative start at zero, so a
        /// bay near station zero would have its taper silently truncated and this case would be measuring the clamp
        /// instead of the relation. </summary>
        private static void CaseTheTurnBayTakesItsTaperFromTheOneOwner(StringBuilder builder, List<string> failures)
        {
            IntersectionGeometryClass cls = IntersectionGeometryClass.Default();
            const double laneWidth = 3.3;
            const double speed = 60.0;
            const double stopLine = 500.0;

            double storage = IntersectionGeometry.TurnBayStorageLengthMetres(4.0, 0.10, cls);
            double flare = IntersectionGeometry.FlareLengthMetres(4.0, 0.10, laneWidth, speed, cls);
            double taper = LaneModel.TaperLengthMetres(laneWidth, speed);
            Expect(Math.Abs((flare - storage) - taper) < 1e-9,
                Fmt("turn bay: flare {0:F6} m less storage {1:F6} m is {2:F6} m, but LaneModel's taper is {3:F6} m -- a " +
                    "second copy of the taper relation has appeared", flare, storage, flare - storage, taper), failures);

            double expectedStorage = 2.0 * 4.0 * (0.90 * 7.6 + 0.10 * 13.7);
            Expect(Math.Abs(storage - expectedStorage) < 1e-9,
                Fmt("turn bay: storage {0:F6} m is not twice four arrivals at the mixed stored length {1:F6} m",
                    storage, expectedStorage), failures);

            AuxiliaryLane bay = IntersectionGeometry.TurnBay(1, stopLine, 4.0, 0.10, laneWidth, speed, cls);
            Expect(Math.Abs(bay.End - stopLine) < 1e-9 && Math.Abs(bay.EndFull - stopLine) < 1e-9,
                Fmt("turn bay: the bay ends at {0:F3} m and holds full width to {1:F3} m; a bay closes at the stop " +
                    "line {2:F3} m with no closing taper", bay.End, bay.EndFull, stopLine), failures);
            Expect(Math.Abs((bay.EndFull - bay.Full) - storage) < 1e-9,
                Fmt("turn bay: the full-width run is {0:F6} m, not the {1:F6} m of storage", bay.EndFull - bay.Full,
                    storage), failures);
            Expect(Math.Abs((bay.Full - bay.Start) - taper) < 1e-9,
                Fmt("turn bay: the opening taper measures {0:F6} m on the assembled lane, not LaneModel's {1:F6} m",
                    bay.Full - bay.Start, taper), failures);

            double floor = IntersectionGeometry.TurnBayStorageLengthMetres(0.0, 0.0, cls);
            Expect(Math.Abs(floor - cls.minimumStorageLengthMetres) < 1e-9,
                Fmt("turn bay: an empty bay stores {0:F3} m instead of the {1:F3} m floor", floor,
                    cls.minimumStorageLengthMetres), failures);
            double trucky = IntersectionGeometry.TurnBayStorageLengthMetres(4.0, 0.60, cls);
            Expect(trucky > storage + 1e-9,
                Fmt("turn bay: a truck-heavy mix stores {0:F3} m, no more than a car-heavy {1:F3} m", trucky, storage),
                failures);

            Line(builder, "turn bay", Fmt(
                "storage {0:F2} m + taper {1:F2} m = flare {2:F2} m at {3:F0} km/h; floor {4:F1} m; truck-heavy " +
                "{5:F2} m", storage, taper, flare, speed, floor, trucky));
        }

        /// <summary> Case 13. Constraint C-06: the same inputs give the same geometry every time. Every entry point that
        /// returns a shape is called twice on identical inputs and compared bit for bit -- corners, the plate fit, the
        /// sight triangles and the assembled bay. A junction that solved differently on the second call would rebuild
        /// itself on every reimport, and no author could tell a real design change from noise. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            IntersectionGeometryClass cls = IntersectionGeometryClass.Default();

            CornerSolution[] first = IntersectionGeometry.SolveCorners(DesignVehicleKind.CityTransitBus, 72.0);
            CornerSolution[] second = IntersectionGeometry.SolveCorners(DesignVehicleKind.CityTransitBus, 72.0);
            Expect(first.Length == second.Length && first.Length == 4,
                Fmt("determinism: the corner count changed between calls ({0} then {1})", first.Length, second.Length),
                failures);
            for (int i = 0; i < first.Length && i < second.Length; i++)
            {
                bool same = first[i].DeflectionDegrees == second[i].DeflectionDegrees
                            && first[i].RadiusMetres == second[i].RadiusMetres
                            && first[i].TangentLengthMetres == second[i].TangentLengthMetres
                            && first[i].ArcLengthMetres == second[i].ArcLengthMetres
                            && first[i].RequiredRadiusMetres == second[i].RequiredRadiusMetres
                            && first[i].Adequate == second[i].Adequate;
                Expect(same, Fmt("determinism: corner {0} changed between calls (radius {1:R} then {2:R})", i,
                    first[i].RadiusMetres, second[i].RadiusMetres), failures);
            }

            CornerPlateFit plateA = IntersectionGeometry.FitPlate(72.0, 7.0, 9.5, 6.0);
            CornerPlateFit plateB = IntersectionGeometry.FitPlate(72.0, 7.0, 9.5, 6.0);
            Expect(plateA.RequiredKerbLengthMetres == plateB.RequiredKerbLengthMetres
                   && plateA.AvailableKerbLengthMetres == plateB.AvailableKerbLengthMetres
                   && plateA.FixedSetbackGapMetres == plateB.FixedSetbackGapMetres
                   && plateA.Meets == plateB.Meets,
                Fmt("determinism: the plate fit changed between calls (required {0:R} then {1:R})",
                    plateA.RequiredKerbLengthMetres, plateB.RequiredKerbLengthMetres), failures);

            List<SightObstruction> objects = new List<SightObstruction>
            {
                new SightObstruction("kiosk", -40.0, -2.0, 3.0)
            };
            SightTriangleVerdict sightA = IntersectionGeometry.SolveSightTriangle(80.0, 4.0,
                DesignVehicleKind.SingleUnitTruck, objects, cls);
            SightTriangleVerdict sightB = IntersectionGeometry.SolveSightTriangle(80.0, 4.0,
                DesignVehicleKind.SingleUnitTruck, objects, cls);
            Expect(sightA.LeftLegMetres == sightB.LeftLegMetres && sightA.RightLegMetres == sightB.RightLegMetres
                   && sightA.BlockingNames.Length == sightB.BlockingNames.Length
                   && sightA.Description == sightB.Description,
                Fmt("determinism: the sight triangle changed between calls (left leg {0:R} then {1:R})",
                    sightA.LeftLegMetres, sightB.LeftLegMetres), failures);

            AuxiliaryLane bayA = IntersectionGeometry.TurnBay(1, 500.0, 3.0, 0.2, 3.3, 60.0, cls);
            AuxiliaryLane bayB = IntersectionGeometry.TurnBay(1, 500.0, 3.0, 0.2, 3.3, 60.0, cls);
            Expect(bayA.Start == bayB.Start && bayA.Full == bayB.Full && bayA.EndFull == bayB.EndFull
                   && bayA.End == bayB.End,
                Fmt("determinism: the turn bay changed between calls (start {0:R} then {1:R})", bayA.Start, bayB.Start),
                failures);

            Line(builder, "determinism", "corners, plate fit, sight triangles and turn bay identical on repeat calls");
        }

        // ---- helpers ----

        private static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }

        private static void Line(StringBuilder builder, string label, string text)
        {
            builder.Append("RCMJUNCTION ").Append(label.PadRight(26)).Append(text).Append("\n");
        }
    }
}
#endif

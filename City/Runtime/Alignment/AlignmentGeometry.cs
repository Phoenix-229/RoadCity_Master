#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What one piece of a horizontal alignment IS.
    ///
    /// A road's plan is not a curve fitted to a set of points; it is a SEQUENCE of pieces, each of which has a name and
    /// a defining property that a surveyor could stake out with a tape and a theodolite. A tangent is dead straight.
    /// An arc holds one constant radius. A transition has curvature that changes at a constant rate along its length --
    /// that single property is the definition of the clothoid, and it is the reason a driver can turn the wheel at a
    /// steady rate instead of yanking it at the instant the arc begins.
    ///
    /// The distinction matters here because RM-75's whole claim is that the alignment is AUTHORED in these terms and the
    /// spline is fitted to the result. A Hermite spline can pass through the same points, but it cannot tell you which
    /// of them lie on a 300 m radius, and the author cannot argue with a number the tool does not hold. </summary>
    public enum AlignmentElementKind
    {
        /// <summary> Dead straight: zero curvature from end to end. </summary>
        Tangent = 0,

        /// <summary> The transition INTO an arc: curvature rises linearly from zero to 1/R. </summary>
        EntryTransition = 1,

        /// <summary> Constant radius. </summary>
        Arc = 2,

        /// <summary> The transition OUT of an arc: curvature falls linearly from 1/R back to zero. </summary>
        ExitTransition = 3
    }


    /// <summary> One resolved piece of the plan, in metres and radians, anchored in the world.
    ///
    /// Every piece carries its own anchor -- a point and a heading -- rather than being evaluated by walking the chain
    /// from the first station. Walking would accumulate rounding along a long road and would make the point at station
    /// 8 000 m depend on how many pieces preceded it; anchoring makes each piece answer for itself, exactly, and makes
    /// a gate able to interrogate one piece in isolation.
    ///
    /// The anchor is the piece's START for a tangent, an entry transition and an arc. For an EXIT transition it is the
    /// piece's END -- the point where curvature has fallen back to zero and the outgoing tangent begins -- with the
    /// heading being the outgoing tangent's direction. That is not an inconsistency but the geometry itself: a
    /// clothoid's series expansion is written from its straight end, and an exit transition's straight end is its last
    /// station, so measuring it backwards from there is the only way to use the closed form rather than an
    /// approximation. <see cref="AlignmentGeometry.PointAt"/> hides the difference. </summary>
    [Serializable]
    public struct AlignmentElement
    {
        public AlignmentElementKind kind;

        /// <summary> Station of the piece's first point, in metres along the whole alignment. </summary>
        public double startStationMetres;

        /// <summary> The piece's own length, in metres, measured ALONG the curve rather than across the chord. </summary>
        public double lengthMetres;

        /// <summary> Radius in metres for an arc or a transition; 0 for a tangent. </summary>
        public double radiusMetres;

        /// <summary> The transition's full length in metres, which sets the RATE at which curvature changes. Held on
        /// the arc and both transitions of the same curve because a transition's shape depends on the pair (R, Ls) and
        /// not on its own length alone. </summary>
        public double transitionLengthMetres;

        /// <summary> +1 where the alignment turns left, -1 where it turns right, 0 on a tangent. Left is
        /// counter-clockwise in the world's XZ plane, which is the engine's own left for a forward heading. </summary>
        public int turnSign;

        public double anchorXMetres;
        public double anchorZMetres;

        /// <summary> Unit heading at the anchor, XZ only. Two components rather than a vector so the whole record stays
        /// in double: a 12 km alignment resolved in float loses centimetres, and the fitter's chord tolerance is quoted
        /// in centimetres. </summary>
        public double headingX;
        public double headingZ;
    }


    /// <summary> The published plane geometry of a road curve, and nothing else.
    ///
    /// Every formula here is the one in the surveying texts, written out rather than fitted: the tangent distance of a
    /// circular curve, the arc's length, its mid-ordinate and external distance, the clothoid's series expansion, the
    /// shift and tangent advance a transition adds, and the comfort length a transition needs at a design speed. No
    /// engine types, no scene, no state -- which is why a headless gate can assert each of them against a figure worked
    /// by hand, and why the same arithmetic serves the fitter, the handles and the readout without being written three
    /// times. </summary>
    public static class AlignmentGeometry
    {
        /// <summary> The rate at which centripetal acceleration is allowed to build along a transition, in m/s^3.
        ///
        /// This is the C in Shortt's relation, the criterion transition curves have been designed to since 1909: a
        /// passenger feels the ONSET of side force, not the force itself, so the transition is made long enough that
        /// the force arrives gradually. Published practice puts C between 0.3 (very comfortable, long spirals) and
        /// 0.8 m/s^3 (accepted on constrained low-speed work). AUTHORED at 0.5 -- the middle of that band, and the
        /// figure most commonly tabulated for general road work. Retunable per project. </summary>
        public const double TransitionComfortRateMetresPerSecondCubed = 0.5;

        /// <summary> Below this deflection a curve is not worth staking: a 0.2 degree change of direction over a 400 m
        /// tangent is inside the width of the carriageway, and inserting an arc for it would put two curve points
        /// within a metre of each other and produce a radius the size of the world. AUTHORED, in radians (about
        /// 0.057 degrees). </summary>
        public const double NegligibleDeflectionRadians = 0.001;

        /// <summary> The tangent distance of a plain circular curve: T = R tan(delta/2), the distance back from the
        /// intersection of the two tangents to the point where the arc leaves the straight. </summary>
        public static double TangentLengthMetres(double radiusMetres, double deflectionRadians)
        {
            if (radiusMetres <= 0.0 || deflectionRadians <= 0.0) { return 0.0; }
            return radiusMetres * Math.Tan(deflectionRadians * 0.5);
        }

        /// <summary> The arc's own length: L = R delta, with delta in radians. </summary>
        public static double ArcLengthMetres(double radiusMetres, double deflectionRadians)
        {
            if (radiusMetres <= 0.0 || deflectionRadians <= 0.0) { return 0.0; }
            return radiusMetres * deflectionRadians;
        }

        /// <summary> The mid-ordinate: M = R (1 - cos(delta/2)), how far the arc sits inside the long chord. This is the
        /// number that decides how densely the fitter must sample, because it is the same quantity measured over a
        /// single chord. </summary>
        public static double MidOrdinateMetres(double radiusMetres, double deflectionRadians)
        {
            if (radiusMetres <= 0.0 || deflectionRadians <= 0.0) { return 0.0; }
            return radiusMetres * (1.0 - Math.Cos(deflectionRadians * 0.5));
        }

        /// <summary> The external distance: E = R (sec(delta/2) - 1), how far the tangent intersection stands outside
        /// the arc. Quoted at the node because it is what tells an author whether the curve will fit the gap. </summary>
        public static double ExternalMetres(double radiusMetres, double deflectionRadians)
        {
            if (radiusMetres <= 0.0 || deflectionRadians <= 0.0) { return 0.0; }
            double cos = Math.Cos(deflectionRadians * 0.5);
            if (cos <= 1e-12) { return double.PositiveInfinity; }
            return radiusMetres * (1.0 / cos - 1.0);
        }

        /// <summary> The angle a transition itself turns through: theta_s = Ls / (2 R).
        ///
        /// It follows directly from the definition. Curvature rises linearly from 0 to 1/R over length Ls, so its
        /// average over the transition is 1/(2R), and the heading change is the average curvature times the length.
        /// This angle is subtracted TWICE from the deflection before the arc is measured, which is why a curve with
        /// transitions needs a deflection larger than Ls/R to exist at all. </summary>
        public static double TransitionAngleRadians(double transitionLengthMetres, double radiusMetres)
        {
            if (transitionLengthMetres <= 0.0 || radiusMetres <= 0.0) { return 0.0; }
            return transitionLengthMetres / (2.0 * radiusMetres);
        }

        /// <summary> The SHIFT: p = Ls^2 / (24 R), how far the circular arc must be moved inward so that the transitions
        /// can join the straights tangentially. Without the shift the transition and the arc would not meet, and this
        /// term is the reason a transitioned curve of radius R does not occupy the same ground as a plain one. </summary>
        public static double TransitionShiftMetres(double transitionLengthMetres, double radiusMetres)
        {
            if (transitionLengthMetres <= 0.0 || radiusMetres <= 0.0) { return 0.0; }
            return (transitionLengthMetres * transitionLengthMetres) / (24.0 * radiusMetres);
        }

        /// <summary> The TANGENT ADVANCE: k = Ls/2 - Ls^3/(240 R^2), the distance from the shifted arc's tangent point
        /// to the transition's straight end, measured along the tangent. Half the transition length to first order --
        /// the transition spends half its length turning -- with the published third-order correction. </summary>
        public static double TransitionTangentAdvanceMetres(double transitionLengthMetres, double radiusMetres)
        {
            if (transitionLengthMetres <= 0.0 || radiusMetres <= 0.0) { return 0.0; }
            double ls = transitionLengthMetres;
            return ls * 0.5 - (ls * ls * ls) / (240.0 * radiusMetres * radiusMetres);
        }

        /// <summary> The whole transitioned curve's tangent distance: Ts = (R + p) tan(delta/2) + k. With no transition
        /// (Ls = 0) both p and k vanish and this collapses to R tan(delta/2), the plain circular case -- one formula,
        /// not two code paths that can disagree. </summary>
        public static double TotalTangentLengthMetres(double radiusMetres, double deflectionRadians,
            double transitionLengthMetres)
        {
            if (radiusMetres <= 0.0 || deflectionRadians <= 0.0) { return 0.0; }
            double p = TransitionShiftMetres(transitionLengthMetres, radiusMetres);
            double k = TransitionTangentAdvanceMetres(transitionLengthMetres, radiusMetres);
            return (radiusMetres + p) * Math.Tan(deflectionRadians * 0.5) + k;
        }

        /// <summary> The transition length comfort demands at a design speed, from Shortt's relation Ls = v^3 / (C R).
        ///
        /// Written with the speed conversion done in the open rather than as the tabulated constant 0.0214: v = V/3.6,
        /// so Ls = V^3 / (46.656 C R). Quoting the derivation instead of the constant is what makes this a formula an
        /// author can check rather than a magic number. A non-positive radius is a straight and needs no transition.
        /// </summary>
        public static double ComfortTransitionLengthMetres(double designSpeedKmh, double radiusMetres,
            double rateMetresPerSecondCubed)
        {
            if (radiusMetres <= 0.0 || designSpeedKmh <= 0.0 || rateMetresPerSecondCubed <= 0.0) { return 0.0; }
            double v = designSpeedKmh / 3.6;
            return (v * v * v) / (rateMetresPerSecondCubed * radiusMetres);
        }

        /// <summary> A point on a transition, in the transition's OWN frame: x along the straight it left, y away from it.
        ///
        /// The clothoid has no closed form in these coordinates -- its integrals are the Fresnel integrals -- so the
        /// published series is used, with a = R Ls:
        ///   x = l - l^5/(40 a^2) + l^9/(3456 a^4)
        ///   y = l^3/(6 a)  - l^7/(336 a^3)
        /// Truncated where it is because the terms shrink by roughly theta_s^4 each time and road transitions turn
        /// through well under a radian: at the 0.5 rad transition angle that a tight urban curve reaches, the first
        /// dropped term is under a tenth of a millimetre over a 100 m transition, which is four orders below the
        /// centimetre tolerance the fitter works to. </summary>
        public static void TransitionLocal(double distanceAlongMetres, double radiusMetres,
            double transitionLengthMetres, out double alongMetres, out double offsetMetres)
        {
            alongMetres = distanceAlongMetres;
            offsetMetres = 0.0;
            if (radiusMetres <= 0.0 || transitionLengthMetres <= 0.0 || distanceAlongMetres <= 0.0) { return; }

            double l = distanceAlongMetres;
            double a = radiusMetres * transitionLengthMetres;
            double l2 = l * l;
            double l3 = l2 * l;
            double l5 = l3 * l2;
            double l7 = l5 * l2;
            double l9 = l7 * l2;
            double a2 = a * a;
            double a3 = a2 * a;
            double a4 = a2 * a2;

            alongMetres = l - l5 / (40.0 * a2) + l9 / (3456.0 * a4);
            offsetMetres = l3 / (6.0 * a) - l7 / (336.0 * a3);
        }

        /// <summary> The heading a transition has turned through by distance l: theta = l^2 / (2 R Ls).
        ///
        /// The integral of a curvature that rises linearly, and the reason the transition angle at l = Ls is Ls/(2R).
        /// </summary>
        public static double TransitionHeadingChangeRadians(double distanceAlongMetres, double radiusMetres,
            double transitionLengthMetres)
        {
            if (radiusMetres <= 0.0 || transitionLengthMetres <= 0.0 || distanceAlongMetres <= 0.0) { return 0.0; }
            return (distanceAlongMetres * distanceAlongMetres) / (2.0 * radiusMetres * transitionLengthMetres);
        }

        /// <summary> Left of a heading, in the XZ plane: the engine's own left for an object facing that way. </summary>
        public static void LeftOf(double headingX, double headingZ, out double leftX, out double leftZ)
        {
            leftX = -headingZ;
            leftZ = headingX;
        }

        /// <summary> A heading turned counter-clockwise in XZ, which is a LEFT turn for a forward heading. </summary>
        public static void Turn(double headingX, double headingZ, double radians, out double outX, out double outZ)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            outX = headingX * c - headingZ * s;
            outZ = headingX * s + headingZ * c;
        }

        private static double IntoElement(AlignmentElement e, double distanceIntoElementMetres)
        {
            if (distanceIntoElementMetres <= 0.0) { return 0.0; }
            return distanceIntoElementMetres >= e.lengthMetres ? e.lengthMetres : distanceIntoElementMetres;
        }

        /// <summary> The world point at a distance into one piece. Clamped to the piece rather than extrapolated: a
        /// station past the end of an arc is not on a longer arc, it is on the next piece, and answering as though the
        /// arc continued is how a fitter ends up drawing a spiral off the end of the road. </summary>
        public static void PointAt(AlignmentElement e, double distanceIntoElementMetres,
            out double xMetres, out double zMetres)
        {
            double d = IntoElement(e, distanceIntoElementMetres);
            double lx;
            double lz;
            LeftOf(e.headingX, e.headingZ, out lx, out lz);
            double sign = e.turnSign;

            if (e.kind == AlignmentElementKind.Tangent || e.radiusMetres <= 0.0)
            {
                xMetres = e.anchorXMetres + e.headingX * d;
                zMetres = e.anchorZMetres + e.headingZ * d;
                return;
            }

            if (e.kind == AlignmentElementKind.Arc)
            {
                double phi = d / e.radiusMetres;
                double along = e.radiusMetres * Math.Sin(phi);
                double offset = e.radiusMetres * (1.0 - Math.Cos(phi));
                xMetres = e.anchorXMetres + e.headingX * along + lx * sign * offset;
                zMetres = e.anchorZMetres + e.headingZ * along + lz * sign * offset;
                return;
            }

            // Both transitions use the same series; the exit one is measured BACKWARDS from its straight end, which is
            // where its anchor sits, so the along-component is subtracted rather than added.
            double l = e.kind == AlignmentElementKind.ExitTransition ? e.lengthMetres - d : d;
            double a;
            double o;
            TransitionLocal(l, e.radiusMetres, e.transitionLengthMetres, out a, out o);
            double direction = e.kind == AlignmentElementKind.ExitTransition ? -1.0 : 1.0;
            xMetres = e.anchorXMetres + e.headingX * a * direction + lx * sign * o;
            zMetres = e.anchorZMetres + e.headingZ * a * direction + lz * sign * o;
        }

        /// <summary> The heading at a distance into one piece, as a unit XZ direction. </summary>
        public static void HeadingAt(AlignmentElement e, double distanceIntoElementMetres,
            out double headingX, out double headingZ)
        {
            double d = IntoElement(e, distanceIntoElementMetres);
            headingX = e.headingX;
            headingZ = e.headingZ;
            if (e.kind == AlignmentElementKind.Tangent || e.radiusMetres <= 0.0) { return; }

            if (e.kind == AlignmentElementKind.Arc)
            {
                Turn(e.headingX, e.headingZ, e.turnSign * (d / e.radiusMetres), out headingX, out headingZ);
                return;
            }

            // An exit transition's anchor heading is the OUTGOING tangent, reached only at its last station, so every
            // earlier station is short of it by the angle the remaining transition still has to turn.
            double l = e.kind == AlignmentElementKind.ExitTransition ? e.lengthMetres - d : d;
            double turned = TransitionHeadingChangeRadians(l, e.radiusMetres, e.transitionLengthMetres);
            double sense = e.kind == AlignmentElementKind.ExitTransition ? -1.0 : 1.0;
            Turn(e.headingX, e.headingZ, e.turnSign * turned * sense, out headingX, out headingZ);
        }

        /// <summary> Curvature at a distance into one piece, in 1/m, SIGNED: positive turning left, negative turning
        /// right. Signed rather than absolute because the sign is exactly what the ribbon needs to colour a reverse
        /// curve differently from a compound one, and what a superelevation pass needs to know which edge to lift.
        /// </summary>
        public static double CurvatureAt(AlignmentElement e, double distanceIntoElementMetres)
        {
            double d = IntoElement(e, distanceIntoElementMetres);
            if (e.kind == AlignmentElementKind.Tangent || e.radiusMetres <= 0.0) { return 0.0; }
            if (e.kind == AlignmentElementKind.Arc) { return e.turnSign / e.radiusMetres; }

            if (e.transitionLengthMetres <= 0.0) { return e.turnSign / e.radiusMetres; }
            double l = e.kind == AlignmentElementKind.ExitTransition ? e.lengthMetres - d : d;
            return e.turnSign * (l / (e.radiusMetres * e.transitionLengthMetres));
        }

        /// <summary> The station step whose chord stays within a stated tolerance of the true curve.
        ///
        /// Inverting the mid-ordinate: a chord subtending angle theta on radius R departs from the arc by
        /// R(1 - cos(theta/2)), so holding that at the tolerance gives theta = 2 acos(1 - tol/R) and a step of R theta.
        /// This is what makes "fitted densely enough" a MEASURED claim rather than a hopeful one -- the fitter is not
        /// sampling every few metres because a few metres sounded right, it is sampling at the spacing that keeps the
        /// fitted line within the tolerance the author asked for. </summary>
        public static double ChordStepMetres(double radiusMetres, double toleranceMetres)
        {
            if (radiusMetres <= 0.0 || toleranceMetres <= 0.0) { return 0.0; }
            if (toleranceMetres >= radiusMetres) { return Math.PI * radiusMetres; }
            double theta = 2.0 * Math.Acos(1.0 - toleranceMetres / radiusMetres);
            return radiusMetres * theta;
        }
    }
}

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which alignment a cross-slope request belongs to. Highway superelevation (this file) and railway
    /// CANT (RM-25) are different features with different units and different transition rules; RM-40 must REFUSE a
    /// rail-class alignment rather than reuse the highway formula with different numbers. </summary>
    public enum AlignmentClass
    {
        Highway = 0,
        Rail = 1
    }

    /// <summary> About which axis the cross-section is rotated to develop superelevation. v1 rotates ABOUT THE
    /// CENTRELINE (outer edge up, inner edge down); rotating about the inside edge (everything up) is a real
    /// alternative but is DISABLED for v1 and recorded in § 10.9 so it is not mistaken for an oversight. </summary>
    public enum SuperelevationAxis
    {
        AboutCentreline = 0,
        AboutInsideEdge = 1   // labelled + disabled for v1
    }

    /// <summary> An authored design-speed tier standing in for Q-13 until it is answered. AASHTO ties design speed
    /// to road class; these AUTHORED km/h defaults (C-15) let RM-40 and RM-27 run now and are replaced wholesale
    /// when Q-13 lands. </summary>
    public enum RoadSpeedTier
    {
        Local = 0,
        Collector = 1,
        Arterial = 2,
        Highway = 3
    }

    /// <summary> RM-40's AUTHORED superelevation constants, quoted from the AASHTO metric geometric-design relation
    /// e + f = V^2 / (127 * R). The side-friction factors below are the published high-speed AASHTO values
    /// (dimensionless, falling with speed); the maximum superelevation e_max is a CLIMATE choice (0.04 iced urban up
    /// to 0.12 rural without ice). Values AUTHORED here so the tool runs; the exact figures MUST be checked against
    /// the adopted edition before shipping, per the task's "not from memory" rule. Retunable per project. </summary>
    [Serializable]
    public sealed class SuperelevationClass
    {
        /// <summary> Maximum superelevation as a decimal (e_max). AUTHORED climate default 0.06 (a temperate mix).
        /// Exact decimal (double) so the clamp value returned is 0.06, not float 0.06f = 0.059999999... </summary>
        public double maxSuperelevation = 0.06;

        /// <summary> Normal pavement crown cross-slope on a straight, as a decimal (AUTHORED 0.02 = 2%). Exact
        /// decimal (double) for consistency with the rest of the tabulated design set. </summary>
        public double normalCrown = 0.02;

        /// <summary> Superelevation runoff length: the station distance over which the cross-slope rotates from
        /// normal crown to full banking. Long enough that the lifted outer edge does not kink (AUTHORED 40 m). </summary>
        public float runoffLengthMetres = 40f;

        /// <summary> The rotation axis in use for v1. </summary>
        public SuperelevationAxis axis = SuperelevationAxis.AboutCentreline;

        /// <summary> Design speeds (km/h) at which the side-friction factors below are tabulated. Held as double
        /// so the tabulated value at an exact speed is returned exactly, not float-widened. </summary>
        public double[] tabulatedSpeedsKmh = { 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };

        /// <summary> AASHTO high-speed maximum side-friction factors f, one per tabulated speed (dimensionless,
        /// AUTHORED from the published table; verify against the adopted edition). Held as double so a tabulated
        /// lookup returns the published figure exactly -- float 0.12f = 0.119999997... breached the gate. </summary>
        public double[] sideFrictionFactors = { 0.17, 0.17, 0.16, 0.15, 0.14, 0.14, 0.13, 0.12, 0.11, 0.09 };

        public static SuperelevationClass Default()
        {
            return new SuperelevationClass();
        }
    }

    /// <summary> RM-40's verifiable core: the published superelevation relation and its transition, without a scene.
    /// The required cross-slope is e = V^2/(127 R) - f from the design radius, the design speed and the tabulated
    /// side friction; it is clamped to normal crown on a straight and refused (with the radius that would be needed)
    /// where it exceeds e_max; it develops over a runoff from crown to full banking before the curve, holds through
    /// the curve, and returns after, with no discontinuity; the zero-cross-slope station inside the runoff is found
    /// so a coincident near-zero longitudinal grade (no drainage) can be flagged; and a rail alignment is refused
    /// outright. Feeding this rotation term into RM-31's cross-section builder and the drainage/grade sampling are
    /// the deferred scene half. </summary>
    public static class Superelevation
    {
        /// <summary> The AUTHORED design speed for a road tier, standing in for Q-13. </summary>
        public static double AuthoredDesignSpeedKmh(RoadSpeedTier tier)
        {
            switch (tier)
            {
                case RoadSpeedTier.Local: return 50.0;
                case RoadSpeedTier.Collector: return 70.0;
                case RoadSpeedTier.Arterial: return 90.0;
                default: return 110.0; // Highway
            }
        }

        /// <summary> The side-friction factor at a design speed: a linear interpolation of the tabulated AASHTO
        /// values, clamped to the ends of the table. Deterministic (C-06). </summary>
        public static double SideFrictionFactor(double designSpeedKmh, SuperelevationClass cls)
        {
            double[] v = cls.tabulatedSpeedsKmh;
            double[] f = cls.sideFrictionFactors;
            if (v == null || f == null || v.Length == 0 || v.Length != f.Length) { return 0.0; }
            if (designSpeedKmh <= v[0]) { return f[0]; }
            if (designSpeedKmh >= v[v.Length - 1]) { return f[f.Length - 1]; }
            for (int i = 1; i < v.Length; i++)
            {
                if (designSpeedKmh <= v[i])
                {
                    double t = (designSpeedKmh - v[i - 1]) / (v[i] - v[i - 1]);
                    return f[i - 1] + t * (f[i] - f[i - 1]);
                }
            }
            return f[f.Length - 1];
        }

        /// <summary> The required superelevation from the published relation e = V^2/(127 R) - f, floored at zero
        /// (a curve gentle enough that side friction alone suffices needs no banking, only normal crown). A
        /// non-positive radius (a straight) needs none. </summary>
        public static double RequiredSuperelevation(double designSpeedKmh, double radiusMetres, double sideFriction)
        {
            if (radiusMetres <= 0.0) { return 0.0; }
            double e = (designSpeedKmh * designSpeedKmh) / (127.0 * radiusMetres) - sideFriction;
            return e > 0.0 ? e : 0.0;
        }

        /// <summary> The minimum curve radius at a design speed for which the banking stays within e_max:
        /// R_min = V^2 / (127 (e_max + f)). This is the radius the tool quotes when a curve is too tight. </summary>
        public static double MinRadiusForDesignSpeedMetres(double designSpeedKmh, SuperelevationClass cls,
            double sideFriction)
        {
            double denom = 127.0 * (cls.maxSuperelevation + sideFriction);
            if (denom <= 0.0) { return double.PositiveInfinity; }
            return (designSpeedKmh * designSpeedKmh) / denom;
        }

        /// <summary> The same published relation solved the other way: the speed a curve of this radius implies,
        /// V = sqrt(127 R (e + f)). It lives here, beside the forward relation, so the tool holds exactly ONE
        /// speed-radius law -- a roundabout quoting its implied circulatory speed calls this rather than repeating
        /// the arithmetic. Because f is itself a function of speed the relation is closed by fixed-point iteration
        /// over the class's own tabulated f: a fixed twelve passes from a fixed start, so the answer is repeatable
        /// bit-for-bit (C-06) and never depends on a convergence race. Roundabout circulating speeds sit below the
        /// table's first row (30 km/h), where SideFrictionFactor clamps to f[0]; the iteration then settles in two
        /// passes and the quoted speed carries that clamp honestly rather than extrapolating a low-speed f the
        /// table does not contain. A non-positive radius, or a radius so tight that (e + f) cannot hold it at any
        /// tabulated speed, returns 0 -- the caller refuses on the radius, not on this number. </summary>
        public static double SpeedFromRadiusKmh(double radiusMetres, double superelevation, SuperelevationClass cls)
        {
            if (radiusMetres <= 0.0 || cls == null) { return 0.0; }
            double speed = 30.0;
            for (int pass = 0; pass < 12; pass++)
            {
                double inside = 127.0 * radiusMetres * (superelevation + SideFrictionFactor(speed, cls));
                if (inside <= 0.0) { return 0.0; }
                speed = System.Math.Sqrt(inside);
            }
            return speed;
        }

        /// <summary> The outcome of resolving the banking at a curve. </summary>
        public struct BankingResult
        {
            public bool refusedRailAlignment;      // a rail alignment -> RM-25's cant owns this, refuse
            public bool feasible;                  // banking within e_max was applied (or none needed)
            public bool exceedsMax;                // required banking exceeded e_max -> report the radius needed
            public double requiredSuperelevation;
            public double appliedSuperelevation;   // == required when feasible, clamped to e_max when exceeded
            public double minRadiusForDesignSpeedMetres;
        }

        /// <summary> Resolve the banking for one curve. A rail alignment is refused outright. Otherwise the required
        /// banking is computed; if it fits within e_max it is applied, and if not the curve is flagged with the
        /// radius that would be needed at this design speed. </summary>
        public static BankingResult ResolveBanking(AlignmentClass alignment, double designSpeedKmh,
            double radiusMetres, SuperelevationClass cls)
        {
            BankingResult r = new BankingResult();
            if (alignment == AlignmentClass.Rail)
            {
                r.refusedRailAlignment = true;
                r.feasible = false;
                return r;
            }
            double f = SideFrictionFactor(designSpeedKmh, cls);
            double required = RequiredSuperelevation(designSpeedKmh, radiusMetres, f);
            r.requiredSuperelevation = required;
            r.minRadiusForDesignSpeedMetres = MinRadiusForDesignSpeedMetres(designSpeedKmh, cls, f);
            if (required > cls.maxSuperelevation + 1e-9)
            {
                r.exceedsMax = true;
                r.feasible = false;
                r.appliedSuperelevation = cls.maxSuperelevation;
            }
            else
            {
                r.feasible = true;
                r.appliedSuperelevation = required;
            }
            return r;
        }

        /// <summary> The applied superelevation at a station along the alignment. Zero (normal crown) before the
        /// runoff begins; a linear development from zero to full banking over the runoff ending at the curve start;
        /// full banking held through the curve; and a mirror-image return to zero over a runoff after the curve.
        /// Continuous everywhere (the ramps meet the flats at equal values), and full value is reached at the curve
        /// start -- before the midpoint -- as the gate requires. </summary>
        public static double SuperelevationAtStation(double stationMetres, double curveStartMetres,
            double curveEndMetres, double appliedSuperelevation, SuperelevationClass cls)
        {
            double runoff = cls.runoffLengthMetres;
            if (appliedSuperelevation <= 0.0) { return 0.0; }
            double developStart = curveStartMetres - runoff;
            if (stationMetres <= developStart) { return 0.0; }
            if (stationMetres < curveStartMetres)
            {
                if (runoff <= 0.0) { return appliedSuperelevation; }
                double t = (stationMetres - developStart) / runoff;
                return appliedSuperelevation * t;
            }
            if (stationMetres <= curveEndMetres) { return appliedSuperelevation; }
            double developEnd = curveEndMetres + runoff;
            if (stationMetres >= developEnd) { return 0.0; }
            if (runoff <= 0.0) { return appliedSuperelevation; }
            double u = (developEnd - stationMetres) / runoff;
            return appliedSuperelevation * u;
        }

        /// <summary> The outer edge's cross-slope as it rotates through the runoff, as a decimal: it starts at the
        /// normal crown (outer edge sloping down, taken negative), passes through zero, and ends at full banking
        /// (outer edge up, positive). The fraction is 0 at the runoff start and 1 at full banking. </summary>
        public static double OuterEdgeCrossSlopeThroughRunoff(double runoffFraction, double normalCrown,
            double appliedSuperelevation)
        {
            double t = runoffFraction < 0.0 ? 0.0 : (runoffFraction > 1.0 ? 1.0 : runoffFraction);
            return -normalCrown + t * (normalCrown + appliedSuperelevation);
        }

        /// <summary> The runoff fraction at which the outer edge cross-slope passes through zero -- the station with
        /// no lateral drainage, which is only a defect if the longitudinal grade is also near zero. </summary>
        public static double ZeroCrossSlopeFraction(double normalCrown, double appliedSuperelevation)
        {
            double span = normalCrown + appliedSuperelevation;
            if (span <= 0.0) { return 0.0; }
            return normalCrown / span;
        }

        /// <summary> Whether a station is the real drainage defect: cross-slope near zero AND longitudinal grade
        /// near zero, so water has nowhere to run. Both thresholds authored small. </summary>
        public static bool HasFlatDrainageDefect(double crossSlope, double longitudinalGrade, double epsilon)
        {
            return Math.Abs(crossSlope) < epsilon && Math.Abs(longitudinalGrade) < epsilon;
        }

        /// <summary> The outer-edge rise for a given banking, by rotation axis: about the centreline the outer edge
        /// rises by super * half-width (the inner edge drops the same); about the inside edge the outer edge rises
        /// by super * full-width and nothing drops. </summary>
        public static double OuterEdgeRiseMetres(double appliedSuperelevation, double halfWidthMetres,
            SuperelevationAxis axis)
        {
            return axis == SuperelevationAxis.AboutInsideEdge
                ? appliedSuperelevation * (2.0 * halfWidthMetres)
                : appliedSuperelevation * halfWidthMetres;
        }
    }
}

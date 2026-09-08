// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A plan-view point or vector in metres, held in double precision. `Vector2` is float, and a city-scale
    /// roundabout sitting a few kilometres from the world origin loses roughly a tenth of a millimetre per component
    /// there -- enough that a curvature computed from differenced float points is visibly noisy, and curvature is the
    /// quantity RM-61 refuses on. The x/z naming is deliberate: this is the ground plane, not screen space. </summary>
    public struct PlanXZ
    {
        public double x;
        public double z;

        public PlanXZ(double x, double z) { this.x = x; this.z = z; }

        public static PlanXZ Zero { get { return new PlanXZ(0.0, 0.0); } }

        public static PlanXZ operator +(PlanXZ a, PlanXZ b) { return new PlanXZ(a.x + b.x, a.z + b.z); }
        public static PlanXZ operator -(PlanXZ a, PlanXZ b) { return new PlanXZ(a.x - b.x, a.z - b.z); }
        public static PlanXZ operator *(PlanXZ a, double s) { return new PlanXZ(a.x * s, a.z * s); }

        public double Length { get { return Math.Sqrt(x * x + z * z); } }

        /// <summary> Unit vector, or zero for a zero-length input (never a division by zero and never a NaN that
        /// would poison a measured report downstream). </summary>
        public PlanXZ Normalised
        {
            get
            {
                double len = Length;
                if (len <= 0.0) { return Zero; }
                return new PlanXZ(x / len, z / len);
            }
        }

        /// <summary> Left-hand perpendicular in the plan (rotate +90 deg about +Y). </summary>
        public PlanXZ Perpendicular { get { return new PlanXZ(-z, x); } }

        public static double Dot(PlanXZ a, PlanXZ b) { return a.x * b.x + a.z * b.z; }

        /// <summary> The scalar cross product a.x*b.z - a.z*b.x: signed area, and the sign that tells one turn
        /// direction from the other. </summary>
        public static double Cross(PlanXZ a, PlanXZ b) { return a.x * b.z - a.z * b.x; }

        public static double Distance(PlanXZ a, PlanXZ b) { return (a - b).Length; }

        public Vector3 ToWorld(float heightMetres) { return new Vector3((float)x, heightMetres, (float)z); }

        public Vector2 ToVector2() { return new Vector2((float)x, (float)z); }

        public static PlanXZ FromVector2(Vector2 v) { return new PlanXZ(v.x, v.y); }
    }

    /// <summary> The three shapes a circulatory way may take. A circle is the special case, not the definition. </summary>
    public enum RoundaboutShapeKind
    {
        /// <summary> One radius everywhere; every per-station quantity comes out constant. </summary>
        Circle = 0,

        /// <summary> An oval: two axes and a rotation. Radius of curvature varies continuously between b^2/a at the
        /// ends of the major axis (the tight end) and a^2/b at the ends of the minor axis. </summary>
        Ellipse = 1,

        /// <summary> A closed control polygon smoothed into a curve. Used for a circulatory way fitted to an existing
        /// site where neither a circle nor an oval fits the available land. </summary>
        ControlPolygon = 2
    }


    /// <summary> RM-61's shape-flexible circulatory way. The outer kerb line of the roundabout is a CLOSED PLANAR
    /// CURVE, parameterised t in [0,1), and every quantity RM-28 held as a single number is re-derived PER STATION:
    /// there is no `radius` field to read, because an ellipse or a fitted polygon does not have one. The local radius
    /// of curvature is measured from the curve's own derivatives, the inscribed dimensions are MEASURED with calipers
    /// rather than assumed from the parameters, and a shape whose tight end falls below the class minimum radius is
    /// REFUSED with the offending station and radius named.
    ///
    /// The polygon case is smoothed with a CLOSED UNIFORM CUBIC B-SPLINE, chosen over an interpolating spline for one
    /// reason that matters here: it is C2, so its curvature is continuous. A C1 curve (a Catmull-Rom, or arcs joined
    /// at tangents) has a curvature that JUMPS at every control point, and a curvature walk over it would refuse or
    /// accept depending on which side of a joint a sample landed. The trade is that the curve does not pass through
    /// its control points -- it is pulled toward them -- which is the correct behaviour for a kerb line being fitted
    /// to a site boundary and is documented here so no caller expects interpolation.
    ///
    /// Parameter direction is the direction of circulation: `t` increasing is onward around the loop, and a caller
    /// orders control points to match the hand of traffic it is building for. Nothing in this file assumes a hand. </summary>
    [Serializable]
    public sealed class RoundaboutShape
    {
        /// <summary> Which of the three shapes this is. </summary>
        public RoundaboutShapeKind kind = RoundaboutShapeKind.Circle;

        /// <summary> Shape centre in plan metres, for the circle and ellipse cases. The polygon case has no authored
        /// centre; use `MeasuredCentroid` for every case, which reads the curve rather than the field. </summary>
        public Vector2 centreXZ = Vector2.zero;

        /// <summary> Circle: the radius. Ellipse: the semi-major axis. Metres. </summary>
        public double semiMajorMetres = 20.0;

        /// <summary> Ellipse: the semi-minor axis, in metres. Ignored by the circle case (which uses semiMajor for
        /// both), and by the polygon case. </summary>
        public double semiMinorMetres = 20.0;

        /// <summary> Ellipse: rotation of the major axis from +X toward +Z, in degrees. </summary>
        public double rotationDeg = 0.0;

        /// <summary> Polygon: the closed control polygon, in plan metres, in circulation order. The curve is closed
        /// implicitly -- do NOT repeat the first point at the end. </summary>
        public List<Vector2> controlPointsXZ = new List<Vector2>();

        /// <summary> A closed uniform cubic B-spline needs at least three control points to close on itself. Three is
        /// legal and rounds off hard; four or more is the useful range. </summary>
        public const int MinimumControlPoints = 3;

        public static RoundaboutShape Circle(Vector2 centreXZ, double radiusMetres)
        {
            RoundaboutShape s = new RoundaboutShape();
            s.kind = RoundaboutShapeKind.Circle;
            s.centreXZ = centreXZ;
            s.semiMajorMetres = radiusMetres;
            s.semiMinorMetres = radiusMetres;
            s.rotationDeg = 0.0;
            return s;
        }

        public static RoundaboutShape Ellipse(Vector2 centreXZ, double semiMajorMetres, double semiMinorMetres,
            double rotationDeg)
        {
            RoundaboutShape s = new RoundaboutShape();
            s.kind = RoundaboutShapeKind.Ellipse;
            s.centreXZ = centreXZ;
            s.semiMajorMetres = semiMajorMetres;
            s.semiMinorMetres = semiMinorMetres;
            s.rotationDeg = rotationDeg;
            return s;
        }

        public static RoundaboutShape Polygon(IList<Vector2> controlPointsXZ)
        {
            RoundaboutShape s = new RoundaboutShape();
            s.kind = RoundaboutShapeKind.ControlPolygon;
            s.controlPointsXZ = new List<Vector2>();
            if (controlPointsXZ != null)
            {
                for (int i = 0; i < controlPointsXZ.Count; i++) { s.controlPointsXZ.Add(controlPointsXZ[i]); }
            }
            return s;
        }

        /// <summary> Whether this shape can be walked at all, with the reason spelled out when it cannot. A shape that
        /// fails here is refused before any measurement is reported, so no caller ever reads a radius derived from a
        /// degenerate curve. </summary>
        public bool IsUsable(out string reason)
        {
            if (kind == RoundaboutShapeKind.ControlPolygon)
            {
                int count = controlPointsXZ == null ? 0 : controlPointsXZ.Count;
                if (count < MinimumControlPoints)
                {
                    reason = "Control polygon has " + count.ToString(CultureInfo.InvariantCulture)
                        + " points; a closed cubic needs at least "
                        + MinimumControlPoints.ToString(CultureInfo.InvariantCulture) + ".";
                    return false;
                }
                reason = string.Empty;
                return true;
            }

            if (semiMajorMetres <= 0.0)
            {
                reason = "Semi-major axis is " + Number(semiMajorMetres) + " m; it must be positive.";
                return false;
            }
            if (kind == RoundaboutShapeKind.Ellipse && semiMinorMetres <= 0.0)
            {
                reason = "Semi-minor axis is " + Number(semiMinorMetres) + " m; it must be positive.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> Wrap a parameter into [0,1). The curve is closed, so t = 1.3 and t = -0.7 are the same station as
        /// t = 0.3 and every walk may run off either end without a caller guarding it. </summary>
        public static double Wrap01(double t)
        {
            double w = t - Math.Floor(t);
            if (w >= 1.0) { w -= 1.0; }
            if (w < 0.0) { w = 0.0; }
            return w;
        }

        private PlanXZ Rotated(double localX, double localZ)
        {
            double phi = rotationDeg * Math.PI / 180.0;
            double c = Math.Cos(phi);
            double s = Math.Sin(phi);
            return new PlanXZ(localX * c - localZ * s, localX * s + localZ * c);
        }

        /// <summary> The point on the kerb line at parameter t. </summary>
        public PlanXZ PointAt(double t)
        {
            double u = Wrap01(t);
            if (kind == RoundaboutShapeKind.ControlPolygon) { return SplinePointAt(u); }

            double theta = 2.0 * Math.PI * u;
            double a = semiMajorMetres;
            double b = kind == RoundaboutShapeKind.Circle ? semiMajorMetres : semiMinorMetres;
            PlanXZ offset = Rotated(a * Math.Cos(theta), b * Math.Sin(theta));
            return PlanXZ.FromVector2(centreXZ) + offset;
        }

        /// <summary> dP/dt. Its magnitude is the metres of kerb per unit parameter, which is what the arc-length
        /// integral runs on; its direction is the circulatory tangent. </summary>
        public PlanXZ FirstDerivativeAt(double t)
        {
            double u = Wrap01(t);
            if (kind == RoundaboutShapeKind.ControlPolygon) { return SplineFirstDerivativeAt(u); }

            double theta = 2.0 * Math.PI * u;
            double a = semiMajorMetres;
            double b = kind == RoundaboutShapeKind.Circle ? semiMajorMetres : semiMinorMetres;
            double k = 2.0 * Math.PI;
            return Rotated(-a * Math.Sin(theta) * k, b * Math.Cos(theta) * k);
        }

        /// <summary> d2P/dt2, needed for curvature. </summary>
        public PlanXZ SecondDerivativeAt(double t)
        {
            double u = Wrap01(t);
            if (kind == RoundaboutShapeKind.ControlPolygon) { return SplineSecondDerivativeAt(u); }

            double theta = 2.0 * Math.PI * u;
            double a = semiMajorMetres;
            double b = kind == RoundaboutShapeKind.Circle ? semiMajorMetres : semiMinorMetres;
            double k = -4.0 * Math.PI * Math.PI;
            return Rotated(a * Math.Cos(theta) * k, b * Math.Sin(theta) * k);
        }

        private PlanXZ ControlPoint(int index)
        {
            int n = controlPointsXZ.Count;
            int i = index % n;
            if (i < 0) { i += n; }
            return PlanXZ.FromVector2(controlPointsXZ[i]);
        }

        /// <summary> Split a global parameter into a segment index and a local 0..1 within that segment. There is one
        /// segment per control point because the curve is closed. </summary>
        private void SplineSegment(double u, out int segment, out double local)
        {
            int n = controlPointsXZ.Count;
            double scaled = u * n;
            segment = (int)Math.Floor(scaled);
            if (segment >= n) { segment = n - 1; }
            if (segment < 0) { segment = 0; }
            local = scaled - segment;
            if (local < 0.0) { local = 0.0; }
            if (local > 1.0) { local = 1.0; }
        }

        private PlanXZ SplinePointAt(double u)
        {
            int i; double s;
            SplineSegment(u, out i, out s);
            double one = 1.0 - s;
            double b0 = one * one * one / 6.0;
            double b1 = (3.0 * s * s * s - 6.0 * s * s + 4.0) / 6.0;
            double b2 = (-3.0 * s * s * s + 3.0 * s * s + 3.0 * s + 1.0) / 6.0;
            double b3 = s * s * s / 6.0;
            return ControlPoint(i) * b0 + ControlPoint(i + 1) * b1 + ControlPoint(i + 2) * b2
                + ControlPoint(i + 3) * b3;
        }

        private PlanXZ SplineFirstDerivativeAt(double u)
        {
            int i; double s;
            SplineSegment(u, out i, out s);
            int n = controlPointsXZ.Count;
            double one = 1.0 - s;
            double d0 = -one * one / 2.0;
            double d1 = (9.0 * s * s - 12.0 * s) / 6.0;
            double d2 = (-9.0 * s * s + 6.0 * s + 3.0) / 6.0;
            double d3 = s * s / 2.0;
            PlanXZ perSegment = ControlPoint(i) * d0 + ControlPoint(i + 1) * d1 + ControlPoint(i + 2) * d2
                + ControlPoint(i + 3) * d3;
            return perSegment * n;
        }

        private PlanXZ SplineSecondDerivativeAt(double u)
        {
            int i; double s;
            SplineSegment(u, out i, out s);
            int n = controlPointsXZ.Count;
            double e0 = 1.0 - s;
            double e1 = 3.0 * s - 2.0;
            double e2 = -3.0 * s + 1.0;
            double e3 = s;
            PlanXZ perSegment = ControlPoint(i) * e0 + ControlPoint(i + 1) * e1 + ControlPoint(i + 2) * e2
                + ControlPoint(i + 3) * e3;
            return perSegment * (n * n);
        }

        /// <summary> Unit circulatory tangent at t, pointing onward around the loop. </summary>
        public PlanXZ TangentAt(double t)
        {
            return FirstDerivativeAt(t).Normalised;
        }

        /// <summary> Local radius of curvature at t, from the plane-curve identity R = |P'|^3 / |P' x P''|. This is
        /// THE per-station quantity RM-61 exists for: on a circle it returns the radius at every t, on an ellipse it
        /// sweeps between b^2/a and a^2/b, and on a fitted polygon it follows the curve. A straight stretch has no
        /// curvature and returns positive infinity rather than dividing by zero. </summary>
        public double CurvatureRadiusAt(double t)
        {
            PlanXZ d1 = FirstDerivativeAt(t);
            PlanXZ d2 = SecondDerivativeAt(t);
            double speed = d1.Length;
            double cross = Math.Abs(PlanXZ.Cross(d1, d2));
            if (cross <= 1e-12 || speed <= 0.0) { return double.PositiveInfinity; }
            return (speed * speed * speed) / cross;
        }

        /// <summary> Sample count for every arc-length measurement in this file. Fixed, so two runs of the same shape
        /// measure the same perimeter to the last bit (C-06). At this count the chord sum sits within about a
        /// millionth of the true length for any roundabout-scale loop. </summary>
        public const int ArcSamples = 2048;

        private double[] BuildArcTable()
        {
            double[] cumulative = new double[ArcSamples + 1];
            PlanXZ previous = PointAt(0.0);
            cumulative[0] = 0.0;
            for (int i = 1; i <= ArcSamples; i++)
            {
                PlanXZ current = PointAt((double)i / ArcSamples);
                cumulative[i] = cumulative[i - 1] + PlanXZ.Distance(current, previous);
                previous = current;
            }
            return cumulative;
        }

        /// <summary> Measured length of the closed kerb line, in metres. Measured, not formula: there is no closed form
        /// for an ellipse's perimeter and none at all for a fitted polygon. </summary>
        public double PerimeterMetres()
        {
            double[] table = BuildArcTable();
            return table[ArcSamples];
        }

        /// <summary> Arc length in metres from t = 0 to t, following the curve. </summary>
        public double ArcLengthTo(double t)
        {
            double u = Wrap01(t);
            double[] table = BuildArcTable();
            double scaled = u * ArcSamples;
            int i = (int)Math.Floor(scaled);
            if (i >= ArcSamples) { return table[ArcSamples]; }
            double frac = scaled - i;
            return table[i] + frac * (table[i + 1] - table[i]);
        }

        /// <summary> The parameter at a given station measured along the kerb line from t = 0, wrapping around the loop
        /// so a walk may run past the end. This is what lets a refusal name a STATION IN METRES -- the number a person
        /// can find on the ground -- instead of a parameter nobody can pace out. </summary>
        public double ParameterAtArcLength(double stationMetres)
        {
            return ParameterInTable(BuildArcTable(), stationMetres);
        }

        /// <summary> Arc-length-weighted centroid of the kerb line, in plan metres. Measured from the curve, so the
        /// polygon case has a centre without one being authored, and an ellipse's centre is confirmed rather than
        /// trusted. Weighting by arc length (not by sample count) keeps the answer from drifting toward whichever end
        /// of an oval the parameterisation crowds samples into. </summary>
        public PlanXZ MeasuredCentroid()
        {
            PlanXZ sum = PlanXZ.Zero;
            double weight = 0.0;
            PlanXZ previous = PointAt(0.0);
            for (int i = 1; i <= ArcSamples; i++)
            {
                PlanXZ current = PointAt((double)i / ArcSamples);
                double segment = PlanXZ.Distance(current, previous);
                PlanXZ midpoint = (current + previous) * 0.5;
                sum = sum + midpoint * segment;
                weight += segment;
                previous = current;
            }
            if (weight <= 0.0) { return PlanXZ.FromVector2(centreXZ); }
            return sum * (1.0 / weight);
        }

        /// <summary> Outward unit normal at t -- perpendicular to the tangent, pointing AWAY from the loop's centre.
        /// Which of the two perpendiculars is outward depends on the hand the control points were wound in, so it is
        /// decided by measurement against the centroid rather than assumed from a winding convention. </summary>
        public PlanXZ OutwardNormalAt(double t, PlanXZ centroid)
        {
            PlanXZ candidate = TangentAt(t).Perpendicular;
            PlanXZ radial = PointAt(t) - centroid;
            if (PlanXZ.Dot(candidate, radial) < 0.0) { return candidate * -1.0; }
            return candidate;
        }

        public PlanXZ OutwardNormalAt(double t) { return OutwardNormalAt(t, MeasuredCentroid()); }

        /// <summary> Directions tried when measuring the loop with calipers: 720 of them, one every half degree, fixed
        /// so the measurement repeats exactly. </summary>
        public const int CaliperDirections = 720;

        /// <summary> What a pair of calipers reads on this loop: the widest and narrowest distances across it, and the
        /// axis each was read along. An ellipse returns 2a and 2b; a fitted polygon returns whatever it actually is. </summary>
        public struct CaliperReading
        {
            public double widestMetres;
            public double widestAxisBearingDeg;
            public double narrowestMetres;
            public double narrowestAxisBearingDeg;
        }

        /// <summary> MEASURE the loop across every caliper direction. For each direction the loop is projected onto
        /// that axis and the extent between the extreme projections is the distance across; the largest such extent is
        /// the widest axis and the smallest is the narrowest. This is the surveyor's measurement, and it is the reason
        /// RM-61 does not need the shape to have a radius: the inscribed dimensions come out of the curve. </summary>
        public CaliperReading MeasureCalipers()
        {
            PlanXZ[] samples = new PlanXZ[ArcSamples];
            for (int i = 0; i < ArcSamples; i++) { samples[i] = PointAt((double)i / ArcSamples); }

            CaliperReading r = new CaliperReading();
            r.widestMetres = 0.0;
            r.narrowestMetres = double.PositiveInfinity;
            for (int d = 0; d < CaliperDirections; d++)
            {
                double bearing = 180.0 * d / CaliperDirections;
                double radians = bearing * Math.PI / 180.0;
                PlanXZ axis = new PlanXZ(Math.Cos(radians), Math.Sin(radians));
                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;
                for (int i = 0; i < samples.Length; i++)
                {
                    double projection = PlanXZ.Dot(samples[i], axis);
                    if (projection < low) { low = projection; }
                    if (projection > high) { high = projection; }
                }
                double extent = high - low;
                if (extent > r.widestMetres) { r.widestMetres = extent; r.widestAxisBearingDeg = bearing; }
                if (extent < r.narrowestMetres) { r.narrowestMetres = extent; r.narrowestAxisBearingDeg = bearing; }
            }
            if (double.IsPositiveInfinity(r.narrowestMetres)) { r.narrowestMetres = 0.0; }
            return r;
        }

        /// <summary> The inscribed circle diameter the legs must trim to, MEASURED: the largest circle that fits inside
        /// the loop can be no wider than the narrowest distance across it, so the narrowest caliper reading is the ICD.
        /// On a circle this returns the diameter; on an oval it returns the SHORT axis, which is the honest answer --
        /// quoting the long axis would let a design claim room it does not have on the tight side. </summary>
        public double MeasuredInscribedCircleDiameterMetres()
        {
            return MeasureCalipers().narrowestMetres;
        }

        /// <summary> Bisection passes used to land a leg's bearing exactly on the kerb line. Twenty halvings of a
        /// 1/512 parameter bracket resolve the crossing to about a nanometre of arc -- far finer than the geometry
        /// needs, and fixed so the answer never depends on a tolerance race. </summary>
        public const int BearingBisectionPasses = 20;

        /// <summary> Where a leg arriving on a given bearing meets the kerb line: scan for the ray from the centroid to
        /// cross the curve, then bisect. Bearings are measured from +X toward +Z, the same convention as `rotationDeg`.
        /// Returns false only if the ray never crosses, which a closed loop containing its own centroid cannot do --
        /// so a false here means the shape is degenerate, and the caller refuses rather than guessing a station. </summary>
        public bool TryParameterAtBearing(double bearingDeg, PlanXZ centroid, out double t)
        {
            double radians = bearingDeg * Math.PI / 180.0;
            PlanXZ ray = new PlanXZ(Math.Cos(radians), Math.Sin(radians));
            const int scan = 512;
            double previousCross = 0.0;
            double previousDot = 0.0;
            bool havePrevious = false;
            for (int i = 0; i <= scan; i++)
            {
                double sample = (double)i / scan;
                PlanXZ radial = PointAt(sample) - centroid;
                double cross = PlanXZ.Cross(ray, radial);
                double dot = PlanXZ.Dot(ray, radial);
                if (dot > 0.0 && cross == 0.0)
                {
                    t = Wrap01(sample);
                    return true;
                }
                if (havePrevious && previousDot > 0.0 && dot > 0.0
                    && ((previousCross < 0.0 && cross > 0.0) || (previousCross > 0.0 && cross < 0.0)))
                {
                    double low = (double)(i - 1) / scan;
                    double high = sample;
                    bool lowIsNegative = previousCross < 0.0;
                    for (int pass = 0; pass < BearingBisectionPasses; pass++)
                    {
                        double mid = 0.5 * (low + high);
                        double midCross = PlanXZ.Cross(ray, PointAt(mid) - centroid);
                        if (midCross == 0.0) { low = mid; high = mid; break; }
                        if ((midCross < 0.0) == lowIsNegative) { low = mid; } else { high = mid; }
                    }
                    t = Wrap01(0.5 * (low + high));
                    return true;
                }
                previousCross = cross;
                previousDot = dot;
                havePrevious = true;
            }
            t = 0.0;
            return false;
        }

        public bool TryParameterAtBearing(double bearingDeg, out double t)
        {
            return TryParameterAtBearing(bearingDeg, MeasuredCentroid(), out t);
        }

        /// <summary> Walk the whole loop at a uniform station step and return the parameter at each station, with the
        /// measured perimeter. The arc-length table is built ONCE for the entire walk: calling
        /// `ParameterAtArcLength` in a loop would rebuild it at every station and turn a curvature walk into thousands
        /// of redundant curve evaluations. Station 0 is t = 0 and the last station falls short of the perimeter, so no
        /// station is visited twice. </summary>
        public int UniformStations(double stepMetres, out double[] stationMetres, out double[] parameters,
            out double perimeterMetres)
        {
            double[] table = BuildArcTable();
            perimeterMetres = table[ArcSamples];
            double step = stepMetres > 0.0 ? stepMetres : 0.5;
            int count = (int)Math.Ceiling(perimeterMetres / step - 1e-9);
            if (count < 1) { count = 1; }
            stationMetres = new double[count];
            parameters = new double[count];
            for (int i = 0; i < count; i++)
            {
                double s = i * step;
                stationMetres[i] = s;
                parameters[i] = ParameterInTable(table, s);
            }
            return count;
        }

        private static double ParameterInTable(double[] table, double stationMetres)
        {
            double total = table[table.Length - 1];
            if (total <= 0.0) { return 0.0; }
            double s = stationMetres - Math.Floor(stationMetres / total) * total;
            int low = 0;
            int high = table.Length - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (table[mid] <= s) { low = mid; } else { high = mid; }
            }
            double span = table[high] - table[low];
            double frac = span > 0.0 ? (s - table[low]) / span : 0.0;
            return ((double)low + frac) / (table.Length - 1);
        }

        private static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    /// <summary> RM-61's solver: the shape-flexible roundabout resolved against RM-28's authored class. Nothing here
    /// re-checks what RM-28 already owns -- leg separation, entry deflection and splitter-island adequacy are called
    /// on `RoundaboutGeometry`, and the speed-radius relation is called on `Superelevation` -- so a change to a
    /// published rule lands in one file and this one follows. What RM-61 adds is that every derived quantity is taken
    /// PER STATION from the curve: the local radius of curvature, the local circulatory tangent an entry must meet
    /// squarely, the re-solved splitter axis and length, and the measured inscribed dimensions. </summary>
    public static class ShapedRoundabout
    {
        /// <summary> Station step for the curvature walk, in metres. Half a metre is finer than the kerb is built to
        /// and fine enough that the tightest point of any roundabout-scale oval is caught within a few millimetres of
        /// radius; fixed, so the walk is repeatable. </summary>
        public const double CurvatureStationStepMetres = 0.5;

        /// <summary> The result of walking the whole loop and reading the radius of curvature at every station. </summary>
        public struct CurvatureWalk
        {
            public bool accepted;
            public bool shapeUsable;
            public int stations;
            public double stationStepMetres;
            public double perimeterMetres;
            public double tightestRadiusMetres;
            public double tightestStationMetres;
            public double widestRadiusMetres;
            public double widestStationMetres;
            public double minRequiredRadiusMetres;
            public double impliedSpeedAtTightestKmh;
            public string refusal;
        }

        /// <summary> Walk the kerb line at a uniform station step and read the LOCAL radius of curvature at each
        /// station. The tightest reading is the one that decides the design: if it falls below the class minimum the
        /// walk REFUSES, and the refusal names the station in metres and the radius it read there, because "the shape
        /// is too tight somewhere" is not something a person can act on. The implied circulatory speed at that
        /// tightest station is reported from the tool's one speed-radius law, with the circulatory cross-slope entered
        /// NEGATED: the loop drains outward, which is adverse banking for a circulating vehicle. </summary>
        public static CurvatureWalk WalkCurvature(RoundaboutShape shape, RoundaboutClass cls,
            SuperelevationClass banking)
        {
            CurvatureWalk w = new CurvatureWalk();
            w.stationStepMetres = CurvatureStationStepMetres;
            w.minRequiredRadiusMetres = cls.minCirculatoryRadiusMetres;
            w.refusal = string.Empty;

            string reason = string.Empty;
            if (shape == null || !shape.IsUsable(out reason))
            {
                w.shapeUsable = false;
                w.accepted = false;
                w.refusal = "Shape cannot be walked: " + (shape == null ? "no shape supplied." : reason);
                return w;
            }
            w.shapeUsable = true;

            double[] stations;
            double[] parameters;
            double perimeter;
            int count = shape.UniformStations(CurvatureStationStepMetres, out stations, out parameters, out perimeter);
            w.stations = count;
            w.perimeterMetres = perimeter;
            w.tightestRadiusMetres = double.PositiveInfinity;
            w.widestRadiusMetres = 0.0;

            for (int i = 0; i < count; i++)
            {
                double radius = shape.CurvatureRadiusAt(parameters[i]);
                if (radius < w.tightestRadiusMetres)
                {
                    w.tightestRadiusMetres = radius;
                    w.tightestStationMetres = stations[i];
                }
                if (radius > w.widestRadiusMetres)
                {
                    w.widestRadiusMetres = radius;
                    w.widestStationMetres = stations[i];
                }
            }

            w.impliedSpeedAtTightestKmh = Superelevation.SpeedFromRadiusKmh(w.tightestRadiusMetres,
                -cls.circulatoryCrossSlopeOutward, banking);

            w.accepted = w.tightestRadiusMetres >= cls.minCirculatoryRadiusMetres - 1e-9;
            if (!w.accepted)
            {
                w.refusal = "Circulatory radius " + Number(w.tightestRadiusMetres) + " m at station "
                    + Number(w.tightestStationMetres) + " m is below the class minimum "
                    + Number(cls.minCirculatoryRadiusMetres)
                    + " m; a design vehicle cannot track the tight end of this shape. Widen the narrow axis or move"
                    + " the control points out at that station.";
            }
            return w;
        }

        /// <summary> Path offset used by the fastest-path construction, in metres: NCHRP 672 draws the vehicle path
        /// 1.5 m clear of a kerb line or island edge, because a driver does not drive on the kerb. </summary>
        public const double FastestPathOffsetMetres = 1.5;

        /// <summary> One entry resolved against the local shape. Every angle and length here was read at THIS leg's
        /// station, so two legs on an oval get different answers from the same class. </summary>
        public struct EntrySolution
        {
            public bool solved;
            public int legIndex;
            public double bearingDeg;
            public double parameter;
            public double stationMetres;
            public PlanXZ pointXZ;
            public PlanXZ localTangentXZ;
            public PlanXZ approachDirectionXZ;
            public double localRadiusMetres;
            public double angleToCirculatoryTangentDeg;
            public double skewFromPerpendicularDeg;
            public bool perpendicularEnough;
            public double fastestPathRadiusMetres;
            public bool deflectionAdequate;
            public double outerDistanceFromCentreMetres;
            public double splitterLengthMetres;
            public bool splitterAdequate;
            public PlanXZ splitterAxisXZ;
            public bool accepted;
            public string refusal;
        }

        /// <summary> The circumradius of the circle through three points: R = abc / 4A, the standard identity, with a
        /// collinear triple (zero area) returning positive infinity because three points on a line lie on a straight,
        /// not a circle. This is the arithmetic behind every fastest-path radius in the file. </summary>
        public static double CircumradiusMetres(PlanXZ a, PlanXZ b, PlanXZ c)
        {
            double sideA = PlanXZ.Distance(a, b);
            double sideB = PlanXZ.Distance(b, c);
            double sideC = PlanXZ.Distance(c, a);
            double twiceArea = Math.Abs(PlanXZ.Cross(b - a, c - a));
            if (twiceArea <= 1e-12) { return double.PositiveInfinity; }
            return (sideA * sideB * sideC) / (2.0 * twiceArea);
        }

        /// <summary> Resolve one leg against the shape it meets. Every quantity is read at the leg's own station:
        ///
        /// - the entry point, found by intersecting the leg's bearing with the kerb line, and its station in metres;
        /// - the LOCAL circulatory tangent and the LOCAL radius of curvature there;
        /// - the angle the approach makes with that local tangent, and how far that is from square. This is the
        ///   check that a circle cannot fail and an oval can: on a circle the radial is always perpendicular to the
        ///   tangent, so the skew is zero everywhere, while on an oval a radial leg meets a slanted tangent;
        /// - the fastest-path radius R1 from a real three-point circle, the three points being the approach path
        ///   1.5 m off the right-hand kerb, the yield-line path 1.5 m off the splitter nose, and the circulatory path
        ///   1.5 m off the central island a quarter of the loop onward. The lengths of that construction are taken
        ///   FROM THE SHAPE (half the measured inscribed diameter for the approach run, a quarter of the measured
        ///   perimeter for the lead) rather than fixed, which is what makes a large flat loop come out with a large R1
        ///   and fail deflection while a compact one passes;
        /// - the splitter island, re-solved: its axis is this leg's approach direction, and its available length is the
        ///   leg's origin distance minus how far out the kerb line actually sits at this bearing. On an oval that
        ///   subtraction differs leg by leg, so a leg landing on the long axis can be short of island while its
        ///   neighbour on the short axis has room to spare.
        ///
        /// The three verdicts are RM-28's own: `EntryDeflectionAdequate`, `SplitterIslandAdequate`, and the class skew
        /// limit. Nothing is re-derived here that RM-28 already publishes. </summary>
        public static EntrySolution SolveEntry(RoundaboutShape shape, RoundaboutClass cls, int legIndex,
            double bearingDeg, double legOriginDistanceFromCentreMetres, PlanXZ centroid)
        {
            EntrySolution e = new EntrySolution();
            e.legIndex = legIndex;
            e.bearingDeg = bearingDeg;
            e.refusal = string.Empty;

            string reason;
            double t;
            if (shape == null || !shape.IsUsable(out reason) || !shape.TryParameterAtBearing(bearingDeg, centroid, out t))
            {
                e.solved = false;
                e.accepted = false;
                e.refusal = "Leg " + legIndex.ToString(CultureInfo.InvariantCulture) + " on bearing "
                    + Number(bearingDeg) + " deg does not meet the kerb line; the shape is degenerate.";
                return e;
            }

            e.solved = true;
            e.parameter = t;
            e.stationMetres = shape.ArcLengthTo(t);
            e.pointXZ = shape.PointAt(t);
            e.localTangentXZ = shape.TangentAt(t);
            e.localRadiusMetres = shape.CurvatureRadiusAt(t);

            PlanXZ radial = (e.pointXZ - centroid).Normalised;
            e.outerDistanceFromCentreMetres = PlanXZ.Distance(e.pointXZ, centroid);
            e.approachDirectionXZ = radial * -1.0;

            double cosine = PlanXZ.Dot(e.approachDirectionXZ, e.localTangentXZ);
            if (cosine > 1.0) { cosine = 1.0; }
            if (cosine < -1.0) { cosine = -1.0; }
            e.angleToCirculatoryTangentDeg = Math.Acos(cosine) * 180.0 / Math.PI;
            e.skewFromPerpendicularDeg = Math.Abs(e.angleToCirculatoryTangentDeg - 90.0);
            e.perpendicularEnough = e.skewFromPerpendicularDeg <= cls.maxEntrySkewFromPerpendicularDeg + 1e-9;

            double lateralSign = PlanXZ.Dot(radial.Perpendicular, e.localTangentXZ) >= 0.0 ? 1.0 : -1.0;
            PlanXZ onwardSide = radial.Perpendicular * lateralSign;
            double halfEntry = 0.5 * cls.entryWidthMetres;
            double lateral = halfEntry - FastestPathOffsetMetres;
            double perimeter = shape.PerimeterMetres();
            double approachRun = 0.5 * shape.MeasuredInscribedCircleDiameterMetres();
            double leadMetres = 0.25 * perimeter;
            double circulatoryWidth = RoundaboutGeometry.CirculatoryWidthMetres(cls);
            double islandOffset = circulatoryWidth - FastestPathOffsetMetres;
            if (islandOffset < 0.0) { islandOffset = 0.0; }

            PlanXZ approachPoint = e.pointXZ + radial * approachRun + onwardSide * lateral;
            PlanXZ yieldPoint = e.pointXZ - onwardSide * lateral;
            double leadParameter = shape.ParameterAtArcLength(e.stationMetres + leadMetres);
            PlanXZ circulatoryPoint = shape.PointAt(leadParameter)
                - shape.OutwardNormalAt(leadParameter, centroid) * islandOffset;
            e.fastestPathRadiusMetres = CircumradiusMetres(approachPoint, yieldPoint, circulatoryPoint);
            e.deflectionAdequate = RoundaboutGeometry.EntryDeflectionAdequate(e.fastestPathRadiusMetres, cls);

            e.splitterAxisXZ = radial;
            double available = legOriginDistanceFromCentreMetres - e.outerDistanceFromCentreMetres;
            e.splitterLengthMetres = available > 0.0 ? available : 0.0;
            e.splitterAdequate = RoundaboutGeometry.SplitterIslandAdequate(e.splitterLengthMetres, cls);

            StringBuilder refusal = new StringBuilder();
            if (!e.perpendicularEnough)
            {
                refusal.Append("Leg ").Append(legIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(" meets the circulatory way at ").Append(Number(e.angleToCirculatoryTangentDeg))
                    .Append(" deg at station ").Append(Number(e.stationMetres)).Append(" m, ")
                    .Append(Number(e.skewFromPerpendicularDeg)).Append(" deg off square against a limit of ")
                    .Append(Number(cls.maxEntrySkewFromPerpendicularDeg)).Append(" deg. ");
            }
            if (!e.deflectionAdequate)
            {
                refusal.Append("Leg ").Append(legIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(" fastest-path radius ").Append(Number(e.fastestPathRadiusMetres))
                    .Append(" m exceeds the cap ").Append(Number(cls.maxFastestPathRadiusMetres))
                    .Append(" m; the entry does not deflect. ");
            }
            if (!e.splitterAdequate)
            {
                refusal.Append("Leg ").Append(legIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(" splitter island is ").Append(Number(e.splitterLengthMetres))
                    .Append(" m against a minimum ").Append(Number(cls.minSplitterIslandLengthMetres))
                    .Append(" m: the kerb line sits ").Append(Number(e.outerDistanceFromCentreMetres))
                    .Append(" m out at this bearing, leaving that much of the leg's ")
                    .Append(Number(legOriginDistanceFromCentreMetres)).Append(" m. ");
            }
            e.refusal = refusal.ToString().TrimEnd();
            e.accepted = e.perpendicularEnough && e.deflectionAdequate && e.splitterAdequate;
            return e;
        }

        /// <summary> Everything RM-61 can say about one shaped roundabout, measured. </summary>
        public struct ShapedResolve
        {
            public bool accepted;
            public PlanXZ centroid;
            public double perimeterMetres;
            public double widestAxisMetres;
            public double narrowestAxisMetres;
            public double measuredInscribedCircleDiameterMetres;
            public double circulatoryWidthMetres;
            public double impliedCirculatorySpeedKmh;
            public CurvatureWalk curvature;
            public RoundaboutGeometry.LegSeparationCheck separation;
            public EntrySolution[] entries;
            public string refusal;
        }

        /// <summary> When a caller gives no leg origin distance, the leg is assumed to start this far back from where
        /// the kerb line sits at its bearing. NCHRP 672's desirable splitter-island length is about 30 m, so a leg
        /// described without an origin is treated as having exactly the desirable run and nothing more -- generous
        /// enough not to invent a refusal, tight enough that it cannot hide one. </summary>
        public const double DefaultLegRunBeyondKerbMetres = 30.0;

        /// <summary> Resolve the whole junction: measure the loop, walk its curvature, check the legs are far enough
        /// apart (RM-28's check, unchanged), and solve every entry against the LOCAL shape it meets. The plan is built
        /// whole on every call -- a shape edited from an oval back to a circle leaves nothing behind from the oval. </summary>
        public static ShapedResolve Resolve(RoundaboutShape shape, RoundaboutClass cls, SuperelevationClass banking,
            IList<double> legBearingsDeg, IList<double> legOriginDistancesMetres)
        {
            ShapedResolve r = new ShapedResolve();
            r.entries = new EntrySolution[0];
            r.refusal = string.Empty;
            r.curvature = WalkCurvature(shape, cls, banking);
            r.circulatoryWidthMetres = RoundaboutGeometry.CirculatoryWidthMetres(cls);

            if (!r.curvature.shapeUsable)
            {
                r.accepted = false;
                r.refusal = r.curvature.refusal;
                return r;
            }

            r.centroid = shape.MeasuredCentroid();
            r.perimeterMetres = r.curvature.perimeterMetres;
            RoundaboutShape.CaliperReading calipers = shape.MeasureCalipers();
            r.widestAxisMetres = calipers.widestMetres;
            r.narrowestAxisMetres = calipers.narrowestMetres;
            r.measuredInscribedCircleDiameterMetres = calipers.narrowestMetres;
            r.impliedCirculatorySpeedKmh = r.curvature.impliedSpeedAtTightestKmh;
            r.separation = RoundaboutGeometry.CheckLegSeparations(legBearingsDeg, cls);

            int legs = legBearingsDeg == null ? 0 : legBearingsDeg.Count;
            r.entries = new EntrySolution[legs];
            for (int i = 0; i < legs; i++)
            {
                double bearing = legBearingsDeg[i];
                double origin;
                if (legOriginDistancesMetres != null && i < legOriginDistancesMetres.Count)
                {
                    origin = legOriginDistancesMetres[i];
                }
                else
                {
                    double outer;
                    double t;
                    outer = shape.TryParameterAtBearing(bearing, r.centroid, out t)
                        ? PlanXZ.Distance(shape.PointAt(t), r.centroid)
                        : 0.0;
                    origin = outer + DefaultLegRunBeyondKerbMetres;
                }
                r.entries[i] = SolveEntry(shape, cls, i, bearing, origin, r.centroid);
            }

            StringBuilder refusal = new StringBuilder();
            if (!r.curvature.accepted) { refusal.Append(r.curvature.refusal).Append(' '); }
            if (!r.separation.accepted)
            {
                refusal.Append("Closest legs are ").Append(Number(r.separation.worstSeparationDeg))
                    .Append(" deg apart against a minimum ").Append(Number(r.separation.minRequiredDeg))
                    .Append(" deg. ");
            }
            for (int i = 0; i < r.entries.Length; i++)
            {
                if (!r.entries[i].accepted && r.entries[i].refusal.Length > 0)
                {
                    refusal.Append(r.entries[i].refusal).Append(' ');
                }
            }
            r.refusal = refusal.ToString().TrimEnd();
            r.accepted = r.refusal.Length == 0;
            return r;
        }

        /// <summary> The measured junction written out for a person: the numbers first, then whatever was refused. </summary>
        public static string Describe(ShapedResolve r)
        {
            StringBuilder s = new StringBuilder();
            s.Append("Perimeter ").Append(Number(r.perimeterMetres)).Append(" m, widest axis ")
                .Append(Number(r.widestAxisMetres)).Append(" m, narrowest axis ")
                .Append(Number(r.narrowestAxisMetres)).Append(" m (measured ICD ")
                .Append(Number(r.measuredInscribedCircleDiameterMetres)).Append(" m), circulatory width ")
                .Append(Number(r.circulatoryWidthMetres)).Append(" m.\n");
            s.Append("Tightest radius ").Append(Number(r.curvature.tightestRadiusMetres)).Append(" m at station ")
                .Append(Number(r.curvature.tightestStationMetres)).Append(" m; widest radius ")
                .Append(Number(r.curvature.widestRadiusMetres)).Append(" m at station ")
                .Append(Number(r.curvature.widestStationMetres)).Append(" m; implied circulatory speed ")
                .Append(Number(r.impliedCirculatorySpeedKmh)).Append(" km/h over ")
                .Append(r.curvature.stations.ToString(CultureInfo.InvariantCulture)).Append(" stations.\n");
            for (int i = 0; i < r.entries.Length; i++)
            {
                EntrySolution e = r.entries[i];
                s.Append("Leg ").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" at ")
                    .Append(Number(e.bearingDeg)).Append(" deg: station ").Append(Number(e.stationMetres))
                    .Append(" m, local radius ").Append(Number(e.localRadiusMetres)).Append(" m, meets tangent at ")
                    .Append(Number(e.angleToCirculatoryTangentDeg)).Append(" deg, R1 ")
                    .Append(Number(e.fastestPathRadiusMetres)).Append(" m, splitter ")
                    .Append(Number(e.splitterLengthMetres)).Append(" m.\n");
            }
            s.Append(r.accepted ? "Accepted." : "REFUSED: " + r.refusal);
            return s.ToString();
        }

        private static string Number(double value)
        {
            if (double.IsPositiveInfinity(value)) { return "straight"; }
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

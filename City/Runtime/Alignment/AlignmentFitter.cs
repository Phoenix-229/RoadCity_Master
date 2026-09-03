#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What a fit produced: the points, the stations they came from, and how far the polyline through them
    /// departs from the true curve at its worst.
    ///
    /// The last number is the only one that makes "fitted densely enough" a claim rather than a hope. It is MEASURED --
    /// the true curve is evaluated at the midpoint of every chord and compared with the chord's own midpoint -- so a
    /// tolerance that was not met is reported as a number, not discovered later as a visible kink in the carriageway.
    /// </summary>
    public sealed class AlignmentFit
    {
        /// <summary> The fitted positions, in world metres, with Y left at zero.
        ///
        /// Zero rather than a sampled height because this is the HORIZONTAL alignment and it has no opinion about
        /// elevation: whoever installs these points -- the editor operator that writes them onto nodes -- samples the
        /// ground through the terrain reader and lifts them. Baking a height in here would give the alignment a second
        /// job and a hidden dependency on terrain that a headless gate could not run without. </summary>
        public List<Vector3> points = new List<Vector3>();

        /// <summary> The station each point was taken at, in metres. Kept because it is what lets a caller relate a node
        /// back to the curve that produced it -- which curve, which radius, what curvature -- instead of guessing from
        /// the position. </summary>
        public List<double> stationsMetres = new List<double>();

        /// <summary> The worst measured departure of the fitted polyline from the true curve, in metres. </summary>
        public double worstDepartureMetres;

        /// <summary> The tolerance that was asked for, in metres. </summary>
        public double toleranceMetres;

        /// <summary> Whether the measured departure came in within the tolerance. </summary>
        public bool withinTolerance;

        public string report = "";

        public int PointCount
        {
            get { return points == null ? 0 : points.Count; }
        }
    }


    /// <summary> Turns resolved geometry into the points a spline is built from.
    ///
    /// This is the hinge of RM-75. The alignment is authored as tangents, arcs and transitions, and everything already in
    /// the tool -- the construction chain, the arc-length mapping, the edge objects, the intersections -- keeps working
    /// because none of it is told: it still receives a spline through nodes. What changes is WHERE those nodes are, and
    /// how many. A straight gets two, because a straight needs two. A curve gets as many as its radius and the stated
    /// tolerance require, which is the same arithmetic a surveyor uses to decide chainage intervals.
    ///
    /// The fitter creates nothing and touches nothing: it returns a list. Whoever installs it owns the undo step. </summary>
    public static class AlignmentFitter
    {
        /// <summary> No step shorter than this, in metres. A 15 m radius junction curve at 5 cm tolerance would otherwise
        /// ask for a point every 2.4 m, and a hundred nodes across a corner is a plan nobody can select in the scene.
        /// AUTHORED at 2 m: half a lane width, below which two nodes are indistinguishable to the eye. </summary>
        public const double MinimumStepMetres = 2.0;

        /// <summary> No step longer than this, in metres. A dead straight kilometre needs only its two ends
        /// geometrically, but a node every 200 m is what lets an author grab the road in the middle of it. AUTHORED at
        /// 200 m. </summary>
        public const double MaximumStepMetres = 200.0;

        /// <summary> Fit a resolved plan. </summary>
        public static AlignmentFit Fit(AlignmentSolution solution, double toleranceMetres)
        {
            AlignmentFit fit = new AlignmentFit();
            fit.toleranceMetres = toleranceMetres > 0.0 ? toleranceMetres : 0.05;
            if (solution == null || solution.elements == null || solution.elements.Count == 0)
            {
                fit.report = "nothing to fit: the plan resolved to no geometry";
                return fit;
            }

            for (int k = 0; k < solution.elements.Count; k++)
            {
                AlignmentElement e = solution.elements[k];

                // A zero-length piece is real -- two curves meeting with no straight between them produce one -- and it
                // must be stepped over rather than sampled, because its one and only point is the previous piece's last
                // point and a spline with two nodes in the same place has no tangent there.
                if (e.lengthMetres <= 1e-9) { continue; }

                int steps = StepCountFor(e, fit.toleranceMetres);
                int first = fit.points.Count == 0 ? 0 : 1;
                for (int s = first; s <= steps; s++)
                {
                    double into = e.lengthMetres * ((double)s / steps);
                    double x;
                    double z;
                    AlignmentGeometry.PointAt(e, into, out x, out z);
                    fit.points.Add(new Vector3((float)x, 0f, (float)z));
                    fit.stationsMetres.Add(e.startStationMetres + into);
                }
            }

            fit.worstDepartureMetres = WorstDepartureMetres(solution, fit);
            fit.withinTolerance = fit.worstDepartureMetres <= fit.toleranceMetres + 1e-9;
            fit.report = ReportFor(solution, fit);
            return fit;
        }

        /// <summary> How many equal steps one piece is cut into.
        ///
        /// A straight takes the ceiling step: geometrically it needs two points, and the only reason to add more is so an
        /// author can grab the road somewhere other than its ends. A curved piece takes the step whose chord stays inside
        /// the tolerance, floored and capped so a hairpin does not produce a hundred nodes and a motorway sweep does not
        /// produce two. A TRANSITION is stepped at its tightest radius rather than its average -- the end where it meets
        /// the arc is the part that would show a kink, and sampling the whole piece for the worst of it costs a handful of
        /// points and removes the question. </summary>
        private static int StepCountFor(AlignmentElement e, double toleranceMetres)
        {
            double step;
            if (e.kind == AlignmentElementKind.Tangent || e.radiusMetres <= 0.0)
            {
                step = MaximumStepMetres;
            }
            else
            {
                step = AlignmentGeometry.ChordStepMetres(e.radiusMetres, toleranceMetres);
                if (step < MinimumStepMetres) { step = MinimumStepMetres; }
                if (step > MaximumStepMetres) { step = MaximumStepMetres; }
            }
            if (step <= 0.0) { return 1; }
            int steps = (int)Math.Ceiling(e.lengthMetres / step - 1e-9);
            return steps < 1 ? 1 : steps;
        }

        /// <summary> The worst departure of the fitted polyline from the true curve, measured rather than predicted.
        ///
        /// Each chord is compared with the curve at the STATION halfway along it, which is where a chord is furthest from
        /// its arc. Where a plan's curves overlap, the resolved geometry has a genuine step in it, and this measurement
        /// reports that step honestly instead of hiding it -- the fitted line really does leave the plan there, and the
        /// legend has already named the corners responsible. </summary>
        public static double WorstDepartureMetres(AlignmentSolution solution, AlignmentFit fit)
        {
            if (solution == null || fit == null || fit.points == null || fit.points.Count < 2) { return 0.0; }
            double worst = 0.0;
            for (int i = 1; i < fit.points.Count; i++)
            {
                double middleStation = (fit.stationsMetres[i - 1] + fit.stationsMetres[i]) * 0.5;
                double tx;
                double tz;
                if (!AlignmentResolver.PointAtStation(solution, middleStation, out tx, out tz)) { continue; }
                double cx = ((double)fit.points[i - 1].x + fit.points[i].x) * 0.5;
                double cz = ((double)fit.points[i - 1].z + fit.points[i].z) * 0.5;
                double dx = tx - cx;
                double dz = tz - cz;
                double departure = Math.Sqrt(dx * dx + dz * dz);
                if (departure > worst) { worst = departure; }
            }
            return worst;
        }

        /// <summary> The fit in words: how many points, how far apart, and whether the tolerance was actually met.
        /// The last part is stated as a MEASUREMENT against the asked-for figure, because "fitted densely" with no number
        /// beside it is the kind of claim § 10.24 forbids. </summary>
        private static string ReportFor(AlignmentSolution solution, AlignmentFit fit)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(fit.PointCount.ToString(CultureInfo.InvariantCulture)).Append(" points over ");
            sb.Append(Number(solution.lengthMetres)).Append(" m");

            double shortest = double.PositiveInfinity;
            double longest = 0.0;
            for (int i = 1; i < fit.stationsMetres.Count; i++)
            {
                double gap = fit.stationsMetres[i] - fit.stationsMetres[i - 1];
                if (gap < shortest) { shortest = gap; }
                if (gap > longest) { longest = gap; }
            }
            if (fit.stationsMetres.Count > 1)
            {
                sb.Append(", spaced ").Append(Number(shortest)).Append(" m to ").Append(Number(longest)).Append(" m");
            }

            sb.Append("\n  worst departure from the true curve ").Append(Number(fit.worstDepartureMetres));
            sb.Append(" m against a tolerance of ").Append(Number(fit.toleranceMetres)).Append(" m -- ");
            sb.Append(fit.withinTolerance
                ? "the fitted line holds the plan"
                : "the fitted line leaves the plan, which happens where two curves overlap");
            return sb.ToString();
        }

        private static string Number(double value)
        {
            if (double.IsInfinity(value)) { return "no limit"; }
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

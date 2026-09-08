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
    /// <summary> One point of intersection in an authored plan: where two straights would meet if neither were curved.
    ///
    /// A road is authored at its CORNERS, not at every metre of its curve. That is how a road is designed on paper and
    /// how it is staked on the ground -- the surveyor is given the intersection, the radius and the transition length,
    /// and everything else follows from geometry. Holding the author's intent as three numbers per corner rather than as
    /// a hundred fitted positions is the difference between a plan that can be re-cut at a different radius and a bag of
    /// points that can only be dragged. </summary>
    [Serializable]
    public sealed class AlignmentPoint
    {
        /// <summary> World XZ of the tangent intersection, in metres. Height is deliberately absent: this is the
        /// HORIZONTAL alignment, and the vertical profile is a separate design with its own grades and its own
        /// curves -- conflating them is what makes a road that satisfies neither. </summary>
        public float xMetres;
        public float zMetres;

        /// <summary> The arc radius the author wants here, in metres. Zero means "the tightest this class and speed
        /// allow", which is resolved rather than guessed -- and is reported, so an author who left it at zero can see
        /// what they were given. </summary>
        public float radiusMetres;

        /// <summary> The transition length in metres, or a NEGATIVE number for "work it out". Negative rather than zero
        /// as the sentinel because zero is a real, meaningful author choice: a plain circular curve with no transition
        /// at all, which is correct on a low-speed junction radius and wrong on a rural bend. </summary>
        public float transitionLengthMetres = -1f;

        public AlignmentPoint() { }

        public AlignmentPoint(float xMetres, float zMetres)
        {
            this.xMetres = xMetres;
            this.zMetres = zMetres;
        }

        public AlignmentPoint(float xMetres, float zMetres, float radiusMetres, float transitionLengthMetres)
        {
            this.xMetres = xMetres;
            this.zMetres = zMetres;
            this.radiusMetres = radiusMetres;
            this.transitionLengthMetres = transitionLengthMetres;
        }
    }


    /// <summary> The authored plan: the corners, the class it is built for, the speed it is designed to, and how closely
    /// the spline must follow it.
    ///
    /// This record is the INPUT. It is never derived from the spline; the spline is derived from it. RM-75's rule is
    /// that the alignment is authored as tangents and arcs and the spline is fitted densely to the result, and that
    /// direction is what keeps every downstream consumer -- the construction chain, the arc-length mapping, the edge
    /// objects -- working untouched: they still see a spline through nodes, and they never learn that the nodes came
    /// from a clothoid. </summary>
    [Serializable]
    public sealed class HorizontalAlignment
    {
        public List<AlignmentPoint> points = new List<AlignmentPoint>();

        /// <summary> The road class this plan is designed for, by id, so the plan survives a catalogue reorder. </summary>
        public int roadClassId;

        /// <summary> Design speed in km/h. This is the number that decides the minimum radius and the transition length,
        /// so it is authored ON the alignment rather than inferred from the geometry: a 40 m radius is a mistake at
        /// 90 km/h and a perfectly ordinary junction at 30, and only the author knows which road this is. </summary>
        public float designSpeedKmh = 50f;

        /// <summary> How far the fitted spline is allowed to depart from the true curve, in metres. AUTHORED at 0.05 --
        /// five centimetres, an order below the width of a lane marking, so the difference is invisible in the world and
        /// still cheap: a 200 m radius at this tolerance samples about every 6.3 m. </summary>
        public float chordToleranceMetres = 0.05f;

        /// <summary> Whether a dragged node is attracted to a suitability corridor. OFF, and it must stay off until an
        /// author asks: guided placement that arrives switched on takes the author's hand off the road, and RM-75's own
        /// wording is that it "must stay opt-in". </summary>
        public bool guidedPlacementEnabled;

        /// <summary> How close a dragged node must come to the corridor before it is pulled in, in metres. AUTHORED at
        /// 24 -- about a cell and a half of RM-74's 16 m raster, close enough that the snap feels deliberate and far
        /// enough that the author does not have to hit a cell edge. </summary>
        public float guidedSnapToleranceMetres = 24f;

        public int PointCount
        {
            get { return points == null ? 0 : points.Count; }
        }

        /// <summary> The alignment's own summary line, for an inspector that must say what this plan IS before it says
        /// anything about whether it is any good. </summary>
        public string SummaryLine()
        {
            return PointCount.ToString(CultureInfo.InvariantCulture) + " points at "
                + designSpeedKmh.ToString("0.#", CultureInfo.InvariantCulture) + " km/h, fitted to within "
                + chordToleranceMetres.ToString("0.###", CultureInfo.InvariantCulture) + " m";
        }
    }


    /// <summary> One resolved corner: what the geometry came out as, and what is WRONG with it.
    ///
    /// The second half is the point. A curve that is too tight for its design speed is still drawn, still fitted and
    /// still drivable in a game -- RM-74 established that this tool warns and does not block, and a plan is no
    /// different. What it must never do is stay silent, because a 40 m bend on a 90 km/h road looks exactly like a
    /// 400 m one from above. So every curve carries the radius it was given, the radius the speed demanded, and a
    /// sentence saying which of the two it is. </summary>
    [Serializable]
    public struct AlignmentCurve
    {
        /// <summary> Index into <see cref="HorizontalAlignment.points"/> of the corner this curve rounds. </summary>
        public int pointIndex;

        public double radiusMetres;
        public double transitionLengthMetres;

        /// <summary> The angle the road turns through at this corner, in radians, always positive. The DIRECTION is in
        /// <see cref="turnSign"/> -- a signed deflection would make every comparison against a published formula carry
        /// an absolute value. </summary>
        public double deflectionRadians;

        /// <summary> +1 left, -1 right. </summary>
        public int turnSign;

        /// <summary> Tangent distance: how far back from the corner the curve begins. </summary>
        public double tangentLengthMetres;

        /// <summary> The circular arc that survives after both transitions have taken their share of the deflection.
        /// Zero is a legitimate answer -- two transitions meeting nose to nose, which is a real curve form. </summary>
        public double arcLengthMetres;

        /// <summary> Transition + arc + transition. </summary>
        public double totalLengthMetres;

        /// <summary> The tightest radius this design speed tolerates, from the published relation. </summary>
        public double minimumRadiusMetres;

        /// <summary> The radius is below what the speed demands: drivable here, signposted in reality. </summary>
        public bool belowMinimumRadius;

        /// <summary> The transition had to be SHORTENED because the corner does not turn through enough angle to hold
        /// two full ones at this radius. Reported rather than hidden, because the shortened transition is no longer the
        /// one comfort asked for. </summary>
        public bool transitionShortened;

        /// <summary> The curve reaches back further than the straight leading to it is long, so it collides with its
        /// neighbour. Still drawn; the plan is the author's. </summary>
        public bool overlapsNeighbour;

        /// <summary> The corner barely turns, so no curve was cut at all. </summary>
        public bool negligible;
    }


    /// <summary> A resolved plan: the pieces in order, the corners they came from, and the words to explain them.
    ///
    /// Held as a plain list of pieces rather than a tree because a road's plan IS a sequence -- station 0 to station L,
    /// each piece handing over to the next at a known point -- and because a sequence is what both consumers want: the
    /// fitter walks it forwards emitting points, and the readout binary-searches it for the piece a station falls in.
    /// </summary>
    [Serializable]
    public sealed class AlignmentSolution
    {
        public List<AlignmentElement> elements = new List<AlignmentElement>();
        public List<AlignmentCurve> curves = new List<AlignmentCurve>();

        /// <summary> Total length along the alignment, in metres. The sum of the pieces, not the sum of the straight
        /// legs: the curves cut corners, so a plan is always shorter than its own outline. </summary>
        public double lengthMetres;

        /// <summary> Why this plan is what it is, in sentences an author can act on. Never empty. </summary>
        public string legend = "";

        /// <summary> The piece a station falls in, or -1 when the station is off the plan. Off the plan is a question,
        /// not a clamp: a station past the end is not the last point, and returning the last point would let a caller
        /// draw a road that carries on past where it stops. </summary>
        public int ElementAt(double stationMetres, out double distanceIntoElementMetres)
        {
            distanceIntoElementMetres = 0.0;
            if (elements == null || elements.Count == 0) { return -1; }
            if (stationMetres < 0.0 || stationMetres > lengthMetres) { return -1; }

            int low = 0;
            int high = elements.Count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (elements[mid].startStationMetres <= stationMetres) { low = mid; }
                else { high = mid - 1; }
            }
            distanceIntoElementMetres = stationMetres - elements[low].startStationMetres;
            return low;
        }
    }
}

using System.Globalization;


namespace RoadCityMaster
{
    /// <summary> Where a sign or a signal support is allowed to stand relative to the road it serves, and how far back
    /// from the intersection a stop line sits. Both are published rules in metres, and both used to be fractions of
    /// something else.
    ///
    /// The lateral offset is the one that matters most, because the old rule -- a fraction of the shoulder width --
    /// failed hardest exactly where the consequence is worst: a road with no shoulder got an offset of zero, which puts
    /// a steel pole on the edge line of a live traffic lane. A fraction of a shoulder is not a clearance; the shoulder
    /// is there for a stopped vehicle and the furniture belongs BEYOND it, not part-way across it.
    ///
    /// MUTCD 2A.19 (Lateral Offset) gives two figures and this class holds both: a sign erected beyond the shoulder
    /// stands at least 0.6 m (2 ft) clear of the shoulder edge, and where there is no shoulder it stands at least
    /// 1.8 m (6 ft) from the edge of the travelled way. The same section allows a reduced 0.3 m (1 ft) offset from the
    /// face of a kerb where the right of way is restricted; that clause is deliberately NOT applied automatically,
    /// because it is a constrained-urban exception an author chooses, not a default the generator should reach for.
    ///
    /// Nothing here is a clear zone. MedianContent holds the AASHTO Roadside Design Guide clear-zone setback for
    /// planting, which is a much larger number answering a different question -- how far a run-off-road vehicle travels
    /// at speed. An intersection is a low-speed conflict area where signs and signals have to be close enough to read,
    /// and the MUTCD offsets are the rule that governs them. </summary>
    public static class RoadsideFurniture
    {
        /// <summary> Minimum clearance in metres from the OUTER EDGE OF THE SHOULDER to a sign or signal support.
        /// MUTCD 2A.19: 2 ft. </summary>
        public const double LateralOffsetBeyondShoulderMetres = 0.6;

        /// <summary> Minimum offset in metres from the EDGE OF THE TRAVELLED WAY where there is no shoulder to measure
        /// from. MUTCD 2A.19: 6 ft. This is the floor that stops a shoulderless road from putting the pole in the
        /// lane. </summary>
        public const double LateralOffsetNoShoulderMetres = 1.8;

        /// <summary> Distance in metres from the edge of the intersection back to the stop line. MUTCD 3B.16 gives
        /// 4 ft as the minimum, measured from the nearest crosswalk line where one is marked. It is where a stopped
        /// driver's eye is, so it is also the station a stop sign should face along. </summary>
        public const double StopLineSetbackMetres = 1.2;

        public const string LateralOffsetSource =
            "MUTCD 2A.19: 0.6 m (2 ft) beyond the shoulder edge, or 1.8 m (6 ft) from the edge of the travelled way "
            + "where there is no shoulder";

        /// <summary> The offset in metres, measured outward from the EDGE OF THE TRAVELLED WAY, at which a sign or a
        /// signal support may stand on a road with this shoulder. Both MUTCD clauses are satisfied by taking whichever
        /// is larger, which is also why a narrow shoulder does not produce an offset narrower than the no-shoulder
        /// floor: half a metre of shoulder plus 0.6 m is still less than 1.8 m, and the pole would be standing in the
        /// place a vehicle stops. </summary>
        public static float LateralOffsetFromTravelledWay(float shoulderWidthMetres)
        {
            double shoulder = shoulderWidthMetres > 0f ? shoulderWidthMetres : 0.0;
            if (float.IsNaN(shoulderWidthMetres)) { shoulder = 0.0; }
            double beyondShoulder = shoulder + LateralOffsetBeyondShoulderMetres;
            return (float)(beyondShoulder > LateralOffsetNoShoulderMetres
                ? beyondShoulder
                : LateralOffsetNoShoulderMetres);
        }

        /// <summary> One line naming the offset this shoulder produces and the rule behind it, for a report or a
        /// tooltip that has to say why the pole is where it is. </summary>
        public static string DescribeLateralOffset(float shoulderWidthMetres)
        {
            return LateralOffsetFromTravelledWay(shoulderWidthMetres).ToString("0.##", CultureInfo.InvariantCulture)
                + " m from the edge of the travelled way (" + LateralOffsetSource + ")";
        }
    }
}

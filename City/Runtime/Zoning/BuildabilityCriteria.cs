#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The authored thresholds the buildable-land test (RM-11) checks a candidate lot
    /// against: the four limits that decide whether a lot is buildable, must be reshaped, or is
    /// rejected outright. Every value here is AUTHORED, never generated -- section 10's standing rule
    /// is that the tool refuses with a number the author set, not with a silent heuristic.
    ///
    /// These are RM-11's own inputs, kept deliberately small. They are NOT the full zoning taxonomy:
    /// RM-42 owns zones (plot ratio, site coverage, use class) and will supply one of these per zone,
    /// which is why the water freeboard is a per-criteria field rather than a global -- a waterfront
    /// commercial frontage legitimately sits closer to the water than a suburban house does. Until
    /// RM-42 exists, a single default instance stands in for every lot. </summary>
    [Serializable]
    public struct BuildabilityCriteria
    {
        /// <summary> The steepest ground grade a lot may carry, as a percentage (rise/run * 100 --
        /// the units an author reads on a road sign, and the units RM-11's reason strings quote).
        /// A lot whose maximum sampled grade exceeds this is reshaped to its buildable part, or
        /// rejected if too little remains. </summary>
        public float maxSlopePercent;

        /// <summary> The smallest area, in square metres, a lot (or the buildable remnant of a
        /// reshaped one) may have and still be worth building on. A reshape that leaves less than this
        /// becomes a rejection. </summary>
        public float minLotArea;

        /// <summary> Metres of dry margin a lot's ground must clear above the water surface. A bare
        /// comparison against the water level would pass a lot sitting two centimetres above a lake,
        /// which is visually absurd, and water surfaces move -- hydrology can raise a level after the
        /// city is baked -- so the test demands headroom, not mere dryness. </summary>
        public float waterFreeboard;

        /// <summary> The greatest height range, in metres, a lot may span across its polygon -- the
        /// relief a building pad would have to absorb in cut and fill. This is a PLACEHOLDER for
        /// RM-22's real pad cut/fill limit; once the pad exists, RM-22 owns this number and RM-11 reads
        /// it from there. Slope and relief are different tests: a uniform hillside is high slope and
        /// low relief, a small gully is moderate slope and high relief, and only relief predicts what
        /// a pad must move. </summary>
        public float maxReliefMetres;

        /// <summary> Sensible standing defaults for a residential lot, used until RM-42 zones supply
        /// per-zone values: 25 % max grade, 200 m^2 minimum, 1 m of freeboard, 6 m of relief. </summary>
        public static BuildabilityCriteria Default()
        {
            BuildabilityCriteria c;
            c.maxSlopePercent = 25f;
            c.minLotArea = 200f;
            c.waterFreeboard = 1f;
            c.maxReliefMetres = 6f;
            return c;
        }
    }
}

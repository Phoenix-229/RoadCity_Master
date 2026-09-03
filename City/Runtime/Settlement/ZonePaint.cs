#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How a lot-sized region takes its zone from the per-cell zone paint. A block or a lot is a POLYGON,
    /// not a point, so "which zone is this lot?" needs a rule. Majority (the zone over the most of the lot's area) is
    /// chosen over centroid because centroid produces buildings of the wrong type on boundary lots; where the top two
    /// zones are within a stated straddle fraction the resolver REFUSES and reports rather than picking. </summary>
    public struct ZoneResolution
    {
        /// <summary> The resolved zone id, or null if refused. </summary>
        public string zoneId;
        /// <summary> True when the resolver refused (a precedence tie between overlapping zones, or a lot straddling a
        /// boundary by more than the stated fraction) -- the cell/lot is reported, not silently assigned. </summary>
        public bool refused;
        /// <summary> One line naming why it refused (empty when resolved). </summary>
        public string reason;
    }

    /// <summary> RM-45's verifiable core: what the paint MEANS, without a scene. The footprint (RM-44) says WHETHER
    /// there is a settlement; this says WHAT KIND (zone assignment) and HOW MUCH (intensity), over the same grid.
    /// Overlap resolves by each zone's DECLARED precedence, never by paint order; a precedence tie is refused and
    /// reported. A lot takes its zone by MAJORITY over its area with a stated straddle tie rule. Intensity has a
    /// stated range and meaning (0 = the sparsest form this zone permits, 1 = the densest) so three operators cannot
    /// read the same number three ways. The masks are EDIT-TIME assets (§ 10.3). The scene sampler that walks real
    /// lot polygons and the second painted channel's storage are the deferred half. </summary>
    public static class ZonePaint
    {
        /// <summary> Resolve overlapping zones at one cell by declared precedence: the highest precedence wins. Where
        /// the top precedence is shared by two or more present zones the resolver REFUSES (paint order is not a
        /// tiebreak) and names the tie. An empty set resolves to null, not refused (no settlement painted here). </summary>
        public static ZoneResolution ResolveCell(IList<ZoneTypeClass> presentZones)
        {
            ZoneResolution r = new ZoneResolution();
            if (presentZones == null || presentZones.Count == 0)
            {
                r.zoneId = null; r.refused = false; r.reason = "";
                return r;
            }

            int topPrecedence = int.MinValue;
            for (int i = 0; i < presentZones.Count; i++)
            {
                if (presentZones[i] == null) { continue; }
                if (presentZones[i].precedence > topPrecedence) { topPrecedence = presentZones[i].precedence; }
            }

            // Count and remember the zones sharing the top precedence.
            int topCount = 0;
            string winner = null;
            for (int i = 0; i < presentZones.Count; i++)
            {
                if (presentZones[i] == null) { continue; }
                if (presentZones[i].precedence == topPrecedence)
                {
                    topCount++;
                    if (winner == null) { winner = presentZones[i].zoneId; }
                }
            }

            if (topCount > 1)
            {
                r.zoneId = null; r.refused = true;
                r.reason = "precedence tie at " + topPrecedence + " between " + topCount + " zones";
                return r;
            }
            r.zoneId = winner; r.refused = false; r.reason = "";
            return r;
        }

        /// <summary> Resolve a lot's zone by MAJORITY over its cells' resolved zone ids. If the leading zone's share
        /// minus the runner-up's share is within the straddle fraction, the lot straddles a boundary and is REFUSED
        /// and reported rather than assigned. Cells that themselves refused do not vote. </summary>
        public static ZoneResolution ResolveLotByMajority(IList<string> perCellZoneIds, double straddleTieFraction)
        {
            ZoneResolution r = new ZoneResolution();
            if (perCellZoneIds == null || perCellZoneIds.Count == 0)
            {
                r.zoneId = null; r.refused = false; r.reason = "";
                return r;
            }

            // Tally votes in first-seen order (stable, C-06).
            List<string> ids = new List<string>();
            List<int> counts = new List<int>();
            int total = 0;
            for (int i = 0; i < perCellZoneIds.Count; i++)
            {
                string id = perCellZoneIds[i];
                if (string.IsNullOrEmpty(id)) { continue; }
                total++;
                int idx = ids.IndexOf(id);
                if (idx < 0) { ids.Add(id); counts.Add(1); }
                else { counts[idx] = counts[idx] + 1; }
            }

            if (total == 0)
            {
                r.zoneId = null; r.refused = false; r.reason = "";
                return r;
            }

            // The leading zone by count, then the best of the rest (stable on ties: first-seen keeps the lead).
            int leadIdx = 0;
            for (int i = 1; i < counts.Count; i++)
            {
                if (counts[i] > counts[leadIdx]) { leadIdx = i; }
            }
            int secondCount = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                if (i == leadIdx) { continue; }
                if (counts[i] > secondCount) { secondCount = counts[i]; }
            }

            double leadShare = (double)counts[leadIdx] / total;
            double secondShare = (double)secondCount / total;
            if (leadShare - secondShare <= straddleTieFraction)
            {
                r.zoneId = null; r.refused = true;
                r.reason = "lot straddles a zone boundary (lead " + leadShare.ToString("0.##")
                           + " vs " + secondShare.ToString("0.##") + ")";
                return r;
            }
            r.zoneId = ids[leadIdx]; r.refused = false; r.reason = "";
            return r;
        }

        /// <summary> Intensity's stated meaning as a storey count: 0 = the sparsest form this zone permits
        /// (storeyMin), 1 = the densest (storeyMax). Clamped to [0,1]; rounds to a whole storey. </summary>
        public static int StoreyAtIntensity(ZoneTypeClass zone, double intensity01)
        {
            if (zone == null) { return 0; }
            double t = intensity01 < 0.0 ? 0.0 : (intensity01 > 1.0 ? 1.0 : intensity01);
            double storeys = zone.storeyMin + (zone.storeyMax - zone.storeyMin) * t;
            return (int)Math.Round(storeys, MidpointRounding.AwayFromZero);
        }

        /// <summary> Intensity's stated meaning as a lot size: 1 = the densest (smallest lot, plotSizeMinM2), 0 = the
        /// sparsest (largest lot, plotSizeMaxM2). Denser means smaller lots, so intensity maps inversely. </summary>
        public static double LotSizeAtIntensityM2(ZoneTypeClass zone, double intensity01)
        {
            if (zone == null) { return 0.0; }
            double t = intensity01 < 0.0 ? 0.0 : (intensity01 > 1.0 ? 1.0 : intensity01);
            return zone.plotSizeMaxM2 + (zone.plotSizeMinM2 - zone.plotSizeMaxM2) * t;
        }
    }
}

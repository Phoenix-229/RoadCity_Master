#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The zoning LIMITS one lot is governed by: the planning envelope a placed building
    /// must fit inside. RM-12 reads these and REFUSES-plus-reports a building that breaks any of them
    /// (§ 10.9's rule: the tool never silently drops a building, it says which limit broke and by how
    /// much). These are the real fields a zoning bylaw actually carries -- front/side/rear setback,
    /// site coverage, plot ratio (FAR), and a height cap -- so the numbers mean what a planner means
    /// by them, not an invented proxy.
    ///
    /// OWNERSHIP: § 10.14 / RM-42 owns the FULL zoning taxonomy (use classes, storey ranges, street
    /// pattern, streetscape sets, scatter density). This struct is the SUBSET RM-12 needs to place a
    /// building correctly, defined here because placement lands before RM-42 and cannot invent it at
    /// the call site. When RM-42 (and, upstream, the Settlement Master spec -- Q-16) land, they extend
    /// THIS type in place rather than declaring a second, so the taxonomy stays single-source -- the
    /// same arrangement <see cref="BuildabilityCriteria"/> documents. Every envelope in
    /// <see cref="ForZone"/> is AUTHORED, NOT GENERATED: real-world-shaped ballpark planning
    /// figures, swappable wholesale when the sourced Settlement Master values arrive (C-15). </summary>
    [Serializable]
    public struct ZoneParams
    {
        /// <summary> The zoning class id this envelope belongs to, matched against
        /// <see cref="Lot.zoneId"/>. 0 means unzoned. </summary>
        public int zoneId;

        /// <summary> Human-readable zone name for report lines ("Low-density residential"). </summary>
        public string displayName;

        /// <summary> Minimum distance, in metres, the building must be set back from its FRONT (street)
        /// lot line. RM-12 anchors the building this far behind the frontage edge. </summary>
        public float frontSetbackMetres;

        /// <summary> Minimum distance, in metres, the building must keep from each SIDE and rear lot
        /// line -- the clearance RM-12 measures from every footprint corner to the nearest lot edge. </summary>
        public float sideSetbackMetres;

        /// <summary> The largest fraction (0..1) of the lot area the building footprint may cover.
        /// RM-12 refuses a footprint whose plan area exceeds this share of the lot. </summary>
        public float maxSiteCoverage;

        /// <summary> The plot ratio / floor-area ratio (FAR): the greatest ratio of total gross floor
        /// area (footprint x storeys) to lot area the zone permits. </summary>
        public float maxPlotRatio;

        /// <summary> The height cap, in metres, from pad to roof. RM-12 refuses a taller building. </summary>
        public float maxHeightMetres;

        /// <summary> The nominal storey height, in metres, used to convert a building's height into a
        /// storey count for the plot-ratio check. AUTHORED ~3 m (a typical floor-to-floor). </summary>
        public float storeyHeightMetres;

        /// <summary> A conservative catch-all envelope used only when a lot carries no matching zone.
        /// AUTHORED: 3 m front / 1.5 m side setback, 50 % coverage, FAR 1.0, 9 m cap, 3 m storeys.
        /// These are deliberately middling so an unzoned lot still places something plausible rather
        /// than nothing; a real zone from <see cref="ForZone"/> overrides all of it. </summary>
        public static ZoneParams Default()
        {
            ZoneParams z;
            z.zoneId = UnzonedId;
            z.displayName = "Unzoned (default envelope)";
            z.frontSetbackMetres = 3f;
            z.sideSetbackMetres = 1.5f;
            z.maxSiteCoverage = 0.5f;
            z.maxPlotRatio = 1.0f;
            z.maxHeightMetres = 9f;
            z.storeyHeightMetres = 3f;
            return z;
        }

        // The int zone ids RM-12's placement path keys on (Lot.zoneId is an int). They mirror the string
        // ids in § 10.14 / RM-42's ZoneCatalog one-to-one; the two never drift because ForZone below is the
        // single source of the int-keyed envelope, just as ZoneCatalog is the single source of the full
        // string-keyed taxonomy. 0 is unzoned.
        public const int UnzonedId = 0;
        public const int LowDensityResidentialId = 1;
        public const int MediumDensityResidentialId = 2;
        public const int CommercialCentreId = 3;
        public const int GeneralIndustrialId = 4;

        /// <summary> The AUTHORED planning envelope for a zone id. Every number is a real-world-shaped
        /// planning figure and is kept consistent with the matching zone in <see cref="ZoneCatalog"/>
        /// (front/side setback, site coverage, plot ratio); the height cap is derived from that zone's
        /// storey range at an AUTHORED 3 m floor-to-floor plus a one-storey allowance for roof and
        /// parapet, so a building that fits the storey count is not refused on height. An unknown id
        /// returns <see cref="Default"/> (fail-safe, never a wrong zone). Swappable wholesale when the
        /// sourced Settlement Master values arrive (C-15). </summary>
        public static ZoneParams ForZone(int zoneId)
        {
            ZoneParams z = Default();
            switch (zoneId)
            {
                case LowDensityResidentialId:
                    z.zoneId = LowDensityResidentialId;
                    z.displayName = "Low-density residential";
                    z.frontSetbackMetres = 6f; z.sideSetbackMetres = 3f;
                    z.maxSiteCoverage = 0.35f; z.maxPlotRatio = 0.6f;
                    z.maxHeightMetres = 10f;   // 2 storeys x 3 m + roof/parapet
                    z.storeyHeightMetres = 3f;
                    return z;
                case MediumDensityResidentialId:
                    z.zoneId = MediumDensityResidentialId;
                    z.displayName = "Medium-density residential";
                    z.frontSetbackMetres = 3f; z.sideSetbackMetres = 1.5f;
                    z.maxSiteCoverage = 0.5f; z.maxPlotRatio = 1.4f;
                    z.maxHeightMetres = 15f;   // 4 storeys x 3 m + roof/parapet
                    z.storeyHeightMetres = 3f;
                    return z;
                case CommercialCentreId:
                    z.zoneId = CommercialCentreId;
                    z.displayName = "Commercial centre";
                    z.frontSetbackMetres = 0f; z.sideSetbackMetres = 0f;
                    z.maxSiteCoverage = 0.9f; z.maxPlotRatio = 5f;
                    z.maxHeightMetres = 40f;   // 12 storeys x 3.2 m + plant
                    z.storeyHeightMetres = 3.2f;
                    return z;
                case GeneralIndustrialId:
                    z.zoneId = GeneralIndustrialId;
                    z.displayName = "General industrial";
                    z.frontSetbackMetres = 8f; z.sideSetbackMetres = 5f;
                    z.maxSiteCoverage = 0.6f; z.maxPlotRatio = 1f;
                    z.maxHeightMetres = 14f;   // 3 tall sheds x ~4 m + roof
                    z.storeyHeightMetres = 4f;
                    return z;
            }
            return z;
        }
    }
}

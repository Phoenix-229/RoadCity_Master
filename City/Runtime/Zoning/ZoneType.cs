#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-42's ZoneType asset: the bundle of planning parameters that make one part of a city read
    /// differently from another. It is an ASSET, never an enum, because the list of zones is a content decision that
    /// changes per project and per region -- so adding a zone is authoring one asset, not a code branch in five
    /// files. The fields use the real names planning instruments use (setback, site coverage, plot ratio / FAR) so a
    /// reviewer can check them against a zoning ordinance. This asset set is the SINGLE definition of zoning for both
    /// the city layer and the settlement system; neither may define its own list. The specific taxonomy must
    /// reconcile with the Settlement Master spec when it exists -- the values below are AUTHORED candidates
    /// (C-15) held for that review, including the two zones generic taxonomies omit (informal settlement, port /
    /// logistics). </summary>
    [Serializable]
    public sealed class ZoneTypeClass
    {
        /// <summary> Stable key both systems reference this zone by (e.g. "zone.residential.low"). </summary>
        public string zoneId = "zone.unset";

        /// <summary> The real-world planning term for this zone. </summary>
        public string planningTerm = "";

        /// <summary> One line on what distinguishes this zone from its neighbours. </summary>
        public string distinguishes = "";

        /// <summary> Building categories permitted in this zone (prefab category keys, not hard-coded types). </summary>
        public string[] permittedBuildingCategories = new string[0];

        public int storeyMin = 1;
        public int storeyMax = 2;

        public float plotSizeMinM2 = 200f;
        public float plotSizeMaxM2 = 800f;

        /// <summary> Front and side setback (metres): how far a building must sit back from the plot edges. </summary>
        public float frontSetbackMetres = 4f;
        public float sideSetbackMetres = 2f;

        /// <summary> Site coverage: the fraction of a lot that may be built on (0..1). </summary>
        public float siteCoverage = 0.4f;

        /// <summary> Plot ratio / floor-area ratio: total floor area over lot area. </summary>
        public float plotRatio = 0.8f;

        /// <summary> The street pattern this zone prefers (e.g. "grid", "organic", "cul-de-sac", "quay"). </summary>
        public string preferredStreetPattern = "grid";

        /// <summary> The streetscape furniture set this zone uses (asset id). </summary>
        public string streetscapeFurnitureSetId = "furniture.default";

        /// <summary> Scatter density band RM-47 consumes (low..high, per unit area). </summary>
        public float scatterDensityLow = 0f;
        public float scatterDensityHigh = 1f;

        /// <summary> Informal / unplanned settlement: organic topology, no setbacks, high coverage, irregular lots --
        /// NOT a low-density residential zone with different prefabs. </summary>
        public bool organicTopology = false;

        /// <summary> Port / logistics: bound to a water edge, lots sized around handling equipment -- NOT industrial. </summary>
        public bool boundToWaterEdge = false;

        /// <summary> Market / open trading ground: this zone is an OPEN SURFACE, not a building volume -- a large paved
        /// or covered ground with stalls, distinguished from general commercial the way a real market is (a surface on
        /// the busiest street, not a taller building). Storeys stay at one; the coverage is the trading ground itself.
        /// A boolean because a market is a KIND of place, exactly like informal settlement and port -- DATA, not a code
        /// special-case in the placer. </summary>
        public bool openSurface = false;

        /// <summary> Market frontage rule: this zone seeks the BUSIEST street frontage rather than the tallest envelope,
        /// so the placer sites it against the highest-traffic edge available. Data, paired with <see cref="openSurface"/>
        /// so "a market, not an office block" is two authored facts and not a hard-coded exception. </summary>
        public bool frontsOnBusiestStreet = false;

        /// <summary> RM-45 overlap precedence: when two zones are painted over the same cell, the higher precedence
        /// wins BY DECLARATION (a civic zone painted over a residential one wins), never by paint order (paint order
        /// is not serialised and not reproducible). Where precedence TIES the resolver refuses and reports the cell. </summary>
        public int precedence = 0;

        /// <summary> RM-46 shoreline setback (metres): how far back from a water edge this zone must stay -- a real
        /// town's edge is a strip of bank/path/beach, not the waterline. A port's setback is 0 (or negative) because
        /// reaching the water is the point; that is how the port exception is DATA, not a code special-case. </summary>
        public float shorelineSetbackMetres = 12f;

        /// <summary> RM-46 slope limit (degrees): ground steeper than this is removed before anything is built,
        /// because a lot on it would need a pad so deep it becomes a terrace. Per zone because informal settlement
        /// genuinely occupies steeper ground than a planned suburb -- that difference is DATA, not an exception. </summary>
        public float maxSlopeDegrees = 15f;
    }
}

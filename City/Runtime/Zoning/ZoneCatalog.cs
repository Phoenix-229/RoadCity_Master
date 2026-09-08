#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-42's single-source zone registry: an ordered set of ZoneType assets referenced by BOTH the city
    /// layer and the settlement system, so the two cannot drift into two lists that need a silent switch statement to
    /// convert between. Zones are resolved by their stable id; an unknown id returns null (fail-closed) rather than
    /// mapping to a default, which is the whole class of bug the single source removes. Adding a zone is
    /// <see cref="Add"/>-ing one authored asset -- no code change, no enum branch. Ordered list + string keys, so two
    /// runs agree (C-06). </summary>
    public sealed class ZoneCatalog
    {
        private readonly List<ZoneTypeClass> zones = new List<ZoneTypeClass>();

        /// <summary> Add one authored zone asset. Ignored if an id is already present (the set stays a set). </summary>
        public void Add(ZoneTypeClass zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.zoneId)) { return; }
            if (Resolve(zone.zoneId) != null) { return; }
            zones.Add(zone);
        }

        /// <summary> Resolve a zone by id, or null if the set does not define it (fail-closed, never a default). </summary>
        public ZoneTypeClass Resolve(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) { return null; }
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].zoneId == zoneId) { return zones[i]; }
            }
            return null;
        }

        public bool Contains(string zoneId) { return Resolve(zoneId) != null; }

        public int Count { get { return zones.Count; } }

        /// <summary> The ids in authoring order (stable). </summary>
        public IList<string> ZoneIds()
        {
            List<string> ids = new List<string>(zones.Count);
            for (int i = 0; i < zones.Count; i++) { ids.Add(zones[i].zoneId); }
            return ids;
        }

        /// <summary> The AUTHORED candidate taxonomy held for the Settlement Master review -- a representative set
        /// including the two zones generic taxonomies omit (informal settlement, port / logistics). Replaced
        /// wholesale when the spec lands; the MECHANISM (asset set + registry) does not change. </summary>
        public static ZoneCatalog Authored()
        {
            ZoneCatalog c = new ZoneCatalog();
            c.Add(ResidentialLow());
            c.Add(ResidentialMedium());
            c.Add(CommercialCentre());
            c.Add(Market());
            c.Add(Industrial());
            c.Add(InformalSettlement());
            c.Add(PortLogistics());
            return c;
        }

        private static ZoneTypeClass ResidentialLow()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.residential.low";
            z.planningTerm = "Low-density residential";
            z.distinguishes = "Detached houses, large setbacks, low coverage.";
            z.permittedBuildingCategories = new[] { "house.detached" };
            z.storeyMin = 1; z.storeyMax = 2;
            z.plotSizeMinM2 = 400f; z.plotSizeMaxM2 = 1200f;
            z.frontSetbackMetres = 6f; z.sideSetbackMetres = 3f;
            z.siteCoverage = 0.35f; z.plotRatio = 0.6f;
            z.preferredStreetPattern = "cul-de-sac";
            z.streetscapeFurnitureSetId = "furniture.suburban";
            z.scatterDensityLow = 0.6f; z.scatterDensityHigh = 1f;
            z.precedence = 10;
            z.shorelineSetbackMetres = 15f; z.maxSlopeDegrees = 15f;
            return z;
        }

        private static ZoneTypeClass ResidentialMedium()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.residential.medium";
            z.planningTerm = "Medium-density residential";
            z.distinguishes = "Row houses and low apartments, moderate coverage.";
            z.permittedBuildingCategories = new[] { "house.row", "apartment.low" };
            z.storeyMin = 2; z.storeyMax = 4;
            z.plotSizeMinM2 = 200f; z.plotSizeMaxM2 = 600f;
            z.frontSetbackMetres = 3f; z.sideSetbackMetres = 1.5f;
            z.siteCoverage = 0.5f; z.plotRatio = 1.4f;
            z.preferredStreetPattern = "grid";
            z.streetscapeFurnitureSetId = "furniture.urban";
            z.scatterDensityLow = 0.3f; z.scatterDensityHigh = 0.6f;
            z.precedence = 20;
            z.shorelineSetbackMetres = 12f; z.maxSlopeDegrees = 18f;
            return z;
        }

        private static ZoneTypeClass CommercialCentre()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.commercial.centre";
            z.planningTerm = "Commercial centre";
            z.distinguishes = "Continuous street frontage, high coverage, minimal setback.";
            z.permittedBuildingCategories = new[] { "retail.frontage", "office.midrise" };
            z.storeyMin = 2; z.storeyMax = 12;
            z.plotSizeMinM2 = 150f; z.plotSizeMaxM2 = 2000f;
            z.frontSetbackMetres = 0f; z.sideSetbackMetres = 0f;
            z.siteCoverage = 0.9f; z.plotRatio = 5f;
            z.preferredStreetPattern = "grid";
            z.streetscapeFurnitureSetId = "furniture.downtown";
            z.scatterDensityLow = 0f; z.scatterDensityHigh = 0.2f;
            z.precedence = 40;
            z.shorelineSetbackMetres = 10f; z.maxSlopeDegrees = 12f;
            return z;
        }

        private static ZoneTypeClass Market()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.commercial.market";
            z.planningTerm = "Market / open trading ground";
            z.distinguishes = "A large open trading surface on the busiest street, not a taller building.";
            z.permittedBuildingCategories = new[] { "market.ground", "market.hall", "market.stall" };
            z.storeyMin = 1; z.storeyMax = 1;
            z.plotSizeMinM2 = 1000f; z.plotSizeMaxM2 = 20000f;
            z.frontSetbackMetres = 0f; z.sideSetbackMetres = 0f;
            z.siteCoverage = 0.6f; z.plotRatio = 0.5f;
            z.preferredStreetPattern = "grid";
            z.streetscapeFurnitureSetId = "furniture.market";
            z.scatterDensityLow = 0f; z.scatterDensityHigh = 0.2f;
            z.openSurface = true; z.frontsOnBusiestStreet = true;
            z.precedence = 42;
            z.shorelineSetbackMetres = 10f; z.maxSlopeDegrees = 6f;
            return z;
        }

        private static ZoneTypeClass Industrial()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.industrial.general";
            z.planningTerm = "General industrial";
            z.distinguishes = "Large sheds, wide access, deep plots.";
            z.permittedBuildingCategories = new[] { "shed.industrial", "warehouse" };
            z.storeyMin = 1; z.storeyMax = 3;
            z.plotSizeMinM2 = 1000f; z.plotSizeMaxM2 = 10000f;
            z.frontSetbackMetres = 8f; z.sideSetbackMetres = 5f;
            z.siteCoverage = 0.6f; z.plotRatio = 1f;
            z.preferredStreetPattern = "grid";
            z.streetscapeFurnitureSetId = "furniture.industrial";
            z.scatterDensityLow = 0f; z.scatterDensityHigh = 0.3f;
            z.precedence = 30;
            z.shorelineSetbackMetres = 15f; z.maxSlopeDegrees = 10f;
            return z;
        }

        private static ZoneTypeClass InformalSettlement()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.informal.settlement";
            z.planningTerm = "Informal / unplanned settlement";
            z.distinguishes = "Organic topology, no setbacks, high coverage, irregular lots.";
            z.permittedBuildingCategories = new[] { "dwelling.informal" };
            z.storeyMin = 1; z.storeyMax = 3;
            z.plotSizeMinM2 = 20f; z.plotSizeMaxM2 = 150f;
            z.frontSetbackMetres = 0f; z.sideSetbackMetres = 0f;
            z.siteCoverage = 0.95f; z.plotRatio = 1.5f;
            z.preferredStreetPattern = "organic";
            z.streetscapeFurnitureSetId = "furniture.minimal";
            z.scatterDensityLow = 0.1f; z.scatterDensityHigh = 0.4f;
            z.organicTopology = true;
            z.precedence = 5;
            z.shorelineSetbackMetres = 5f; z.maxSlopeDegrees = 30f;
            return z;
        }

        private static ZoneTypeClass PortLogistics()
        {
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.port.logistics";
            z.planningTerm = "Port / logistics";
            z.distinguishes = "Bound to a water edge, lots sized around handling equipment.";
            z.permittedBuildingCategories = new[] { "terminal.container", "warehouse.transit" };
            z.storeyMin = 1; z.storeyMax = 2;
            z.plotSizeMinM2 = 5000f; z.plotSizeMaxM2 = 50000f;
            z.frontSetbackMetres = 10f; z.sideSetbackMetres = 6f;
            z.siteCoverage = 0.7f; z.plotRatio = 0.8f;
            z.preferredStreetPattern = "quay";
            z.streetscapeFurnitureSetId = "furniture.industrial";
            z.scatterDensityLow = 0f; z.scatterDensityHigh = 0.1f;
            z.boundToWaterEdge = true;
            z.precedence = 50;
            z.shorelineSetbackMetres = 0f; z.maxSlopeDegrees = 8f;
            return z;
        }
    }

    /// <summary> RM-42's verifiable core helpers: validation of a zone's planning parameters, without a scene. The
    /// catalog itself (ZoneCatalog) is the single source; these functions confirm a zone's numbers are self-
    /// consistent so a bad asset is caught at author time rather than producing a district that cannot be built. </summary>
    public static class ZoneTaxonomy
    {
        /// <summary> Whether a zone's parameters are self-consistent: non-empty id, ordered storey and plot ranges,
        /// non-negative setbacks, and site coverage a genuine fraction. </summary>
        public static bool IsValid(ZoneTypeClass zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.zoneId)) { return false; }
            if (zone.storeyMin < 1 || zone.storeyMax < zone.storeyMin) { return false; }
            if (zone.plotSizeMinM2 < 0f || zone.plotSizeMaxM2 < zone.plotSizeMinM2) { return false; }
            if (zone.frontSetbackMetres < 0f || zone.sideSetbackMetres < 0f) { return false; }
            if (zone.siteCoverage < 0f || zone.siteCoverage > 1f) { return false; }
            if (zone.plotRatio < 0f) { return false; }
            if (zone.scatterDensityHigh < zone.scatterDensityLow) { return false; }
            return true;
        }
    }
}

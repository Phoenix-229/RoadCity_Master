#region "Imports"
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Whether RM-12 placed a building on a lot or refused it. </summary>
    public enum PlacementOutcome
    {
        /// <summary> The building was placed; <see cref="PlacementVerdict.instance"/> is its record. </summary>
        Placed,

        /// <summary> No building was placed because it broke a zoning limit or would not fit inside the
        /// lot. <see cref="PlacementVerdict.reason"/> names the quantity, its value and the limit. </summary>
        Refused
    }


    /// <summary> What RM-12 decided for one <see cref="PlacementRequest"/>. Mirrors the RM-11
    /// <see cref="LotVerdict"/> discipline: the tool never silently omits a building -- a refusal
    /// ALWAYS names the quantity, its measured value, and the limit it broke, so an author can see why
    /// a lot is empty and either change the zone or accept it. A placement anyone cannot interrogate
    /// turns into "the generator just doesn't build there", the report that gets the check disabled. </summary>
    public struct PlacementVerdict
    {
        /// <summary> The lot this verdict is for (<see cref="Lot.id"/>). </summary>
        public int lotId;

        /// <summary> Placed or refused. </summary>
        public PlacementOutcome outcome;

        /// <summary> A quotable explanation, always set: on success it states the building was placed;
        /// on refusal it names the quantity, its value and the limit -- e.g.
        /// "footprint 640 m^2 exceeds coverage limit 400 m^2 (0.40 of 1000 m^2 lot)". </summary>
        public string reason;

        /// <summary> The placed building's record. Valid only when <see cref="outcome"/> is
        /// <see cref="PlacementOutcome.Placed"/>; a defaulted struct otherwise. </summary>
        public BuildingInstance instance;
    }
}

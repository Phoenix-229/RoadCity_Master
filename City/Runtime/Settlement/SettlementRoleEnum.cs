#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-15 / C-13. What a hand-drawn road IS in the settlement network: which order of place it connects,
    /// and therefore which order of place may be seeded where it ends. The author draws and names the network first;
    /// this field is how that intent reaches the siting code, because a road called "road7" tells it nothing.
    ///
    /// This is the THIRD road-class axis and it is deliberately a separate field with a separate name, on the author's
    /// own instruction ("it is easier to determine the different or use different name if the classes will collide").
    /// The three never merge:
    ///
    ///   1. <see cref="Road.RoadClassEnum"/>   -- the CROSS-SECTION. How wide, how many lanes, what design speed.
    ///   2. RoadCityMaster.City.RoadClass      -- the FUNCTIONAL class INSIDE a settlement (arterial / collector /
    ///                                            local), assigned by the generator to streets it creates itself.
    ///   3. This enum                          -- the SETTLEMENT-NETWORK role of a road the AUTHOR drew between
    ///                                            places.
    ///
    /// A motorway-class cross-section can be a village connector in a small country and an intercity trunk in a large
    /// one, so knowing (1) can never tell you (3). Nothing here changes a lane count, a width or a speed: this field is
    /// read by the siting pass and by nothing in the mesh or terraform path.
    ///
    /// The default is <see cref="None"/> so that every scene authored before this field existed deserialises to "not
    /// part of the settlement network" and seeds nothing. Opting a road in is an explicit act.
    ///
    /// Which order of place each role seeds -- and the population, spacing and area bands that go with it -- is NOT
    /// decided here. That table, with its sources, belongs to SettlementOrderCatalog (C-15); duplicating any of it in
    /// this file would create the second copy that would later disagree with the first. </summary>
    public enum SettlementRoleEnum
    {
        /// <summary> Not part of the settlement network. The road is built exactly as before and seeds nothing.
        /// This is the default for both new and pre-existing roads. </summary>
        [InspectorName("None (not a settlement road)")]
        None = 0,

        /// <summary> "Main highway roads" -- the top of the network, carrying the largest places. Its free ends are
        /// where the capital and the big cities are proposed. </summary>
        [InspectorName("Main highway")]
        MainHighway = 1,

        /// <summary> "Intercities roads" -- between cities of the ordinary urban order, below the main highway. </summary>
        [InspectorName("Intercities")]
        Intercity = 2,

        /// <summary> "Intercities connecting towns roads" -- a road that leaves the intercity network to reach a
        /// town. </summary>
        [InspectorName("Intercities connecting towns")]
        TownConnector = 3,

        /// <summary> "Intercities connecting villages roads" -- the lowest order in the network, reaching a
        /// village. </summary>
        [InspectorName("Intercities connecting villages")]
        VillageConnector = 4
    }
}

#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One placed building: which prefab, which variant, and where it sits.
    ///
    /// SAVED, in full -- never derived, even though a generator produced it. A building is the
    /// thing an author most often nudges by hand, and re-deriving it would mean a placement change
    /// anywhere upstream silently relocates every building in the city. So the resolved variant and
    /// the final transform are frozen into the saved data at bake time and a re-bake never
    /// overwrites them.
    ///
    /// The prefab is referenced by manifest category and variant index (RM-77, RM-55), not by a
    /// direct asset reference, so the data model carries no hard dependency on any particular
    /// content and a city asset stays loadable when a prefab set is swapped. </summary>
    [Serializable]
    public struct BuildingInstance
    {
        /// <summary> SAVED. <see cref="Lot.id"/> of the lot this building stands on. </summary>
        public int lotId;

        /// <summary> SAVED. The prefab manifest category to place (RM-77). A category is one lookup
        /// the tool performs; it may hold several assets. </summary>
        public string prefabCategory;

        /// <summary> SAVED. Which asset within the category RM-55's seeded draw chose, frozen here
        /// so the choice is reproducible without re-running the draw and stable if the author edits
        /// it by hand. </summary>
        public int variantIndex;

        /// <summary> SAVED. World-space position of the placed building. </summary>
        public Vector3 position;

        /// <summary> SAVED. World-space rotation of the placed building. </summary>
        public Quaternion rotation;

        /// <summary> SAVED. World-space scale of the placed building. </summary>
        public Vector3 scale;
    }
}

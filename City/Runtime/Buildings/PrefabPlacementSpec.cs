#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The facts about the CHOSEN prefab that RM-12 needs to place it correctly, resolved
    /// upstream and handed in -- never invented here. The prefab is picked by RM-77's manifest lookup
    /// and RM-55's seeded variant draw; those are unbuilt, so this struct is the SEAM between "which
    /// prefab" (their job) and "where and how it sits" (RM-12's job). RM-48's
    /// <see cref="PrefabContract"/> already fixes the geometry conventions these fields assume: the
    /// pivot is at the base, plan-centred, and local +Z is FORWARD -- so a footprint width is measured
    /// across the +X axis and depth along +Z, and RM-12 aims that +Z at the street.
    ///
    /// The footprint is given as a plan RECTANGLE (width x depth in metres) rather than a polygon: it
    /// is the prefab's real-world plan bounding box, which is all the setback and site-coverage checks
    /// need, and it keeps this seam trivial for RM-77 to fill from a prefab's measured bounds. </summary>
    public struct PrefabPlacementSpec
    {
        /// <summary> The manifest category this prefab belongs to (RM-77), copied onto the placed
        /// <see cref="BuildingInstance.prefabCategory"/>. </summary>
        public string prefabCategory;

        /// <summary> Which asset within the category RM-55's seeded draw chose, copied onto
        /// <see cref="BuildingInstance.variantIndex"/>. A negative value means "unresolved" -- the
        /// placement still runs (the geometry does not depend on the choice), it is just recorded as-is. </summary>
        public int variantIndex;

        /// <summary> The prefab's plan footprint in local metres, packed as (width, depth):
        /// <c>footprintSize.x</c> is the width across the frontage (the prefab's local +X) and
        /// <c>footprintSize.y</c> is the depth into the lot (the prefab's local +Z, the FORWARD axis).
        /// Real-world metre scale, per the contract. </summary>
        public Vector2 footprintSize;

        /// <summary> The prefab's height in metres, pad to roof -- the quantity the zone height cap and
        /// the plot-ratio storey count are measured against. </summary>
        public float heightMetres;

        /// <summary> How this prefab meets the ground (RM-22 recorded the same choice on the pad). RM-12
        /// sets the placed pivot's base to the pad elevation for every mode; the mode is carried onto
        /// nothing here but drives the pad/terrain treatment, so it is threaded through for the record. </summary>
        public GroundContactMode groundContactMode;

        public PrefabPlacementSpec(string prefabCategory, int variantIndex, Vector2 footprintSize,
            float heightMetres, GroundContactMode groundContactMode)
        {
            this.prefabCategory = prefabCategory;
            this.variantIndex = variantIndex;
            this.footprintSize = footprintSize;
            this.heightMetres = heightMetres;
            this.groundContactMode = groundContactMode;
        }
    }
}

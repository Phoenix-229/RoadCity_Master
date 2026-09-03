#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-41's surface-type asset: ONE object that holds everything an unsealed road differs from a sealed
    /// one in -- crown cross-slope, lane-marking policy, painted-verge width, and side-ditch requirement -- plus its
    /// authored material set. Sealed and unsealed are BOTH instances of this asset (symmetry), so a third type (a
    /// cobbled street, a concrete slab road) is a new asset, never an enum branch in five files. The gravel crown is
    /// steeper because an unsealed surface sheds water by shape alone (a flat gravel road potholes); the exact value
    /// is AUTHORED here from ordinary gravel-road practice (~4% crown; C-15) and belongs to the adopted low-volume /
    /// gravel-road manual. Surface wear (wheel tracks, corrugation, centre ridge) is AUTHORED CONTENT in the material
    /// set, NOT GENERATED -- no implementer may substitute a noise function and call it wear. </summary>
    [Serializable]
    public sealed class SurfaceTypeClass
    {
        /// <summary> Human-readable name of the surface type (also the key the road references). </summary>
        public string surfaceName = "Sealed";

        /// <summary> Normal crown cross-slope as a decimal. AUTHORED: sealed ~0.02, unsealed ~0.04 (steeper to shed
        /// water by shape). </summary>
        public float crownCrossSlope = 0.02f;

        /// <summary> Whether this surface carries lane markings at all. Unsealed roads suppress them ENTIRELY (RM-32
        /// reads this rather than the author blanking the marking list by hand). </summary>
        public bool emitsLaneMarkings = true;

        /// <summary> The painted verge margin beyond the flattened edge, this surface's OWN width (RM-38: paint is
        /// independent of the deform width). AUTHORED: sealed a narrow verge, unsealed a wide dust margin. </summary>
        public float paintMarginMetres = 0.5f;

        /// <summary> Whether a side ditch is required. An unsealed road without a ditch loses its surface in the
        /// first wet season, so it marks the requirement (links RM-30) rather than shipping a road that could not
        /// survive its world. </summary>
        public bool requiresSideDitch = false;

        /// <summary> Identifier of the AUTHORED material set for this surface (tiling material + optional prefab
        /// detail, arc-length-correct UVs). AUTHORED, NOT GENERATED. </summary>
        public string materialSetId = "surface.sealed.default";

        /// <summary> The AUTHORED sealed surface: gentle crown, markings on, narrow verge, no ditch required. </summary>
        public static SurfaceTypeClass Sealed()
        {
            SurfaceTypeClass s = new SurfaceTypeClass();
            s.surfaceName = "Sealed";
            s.crownCrossSlope = 0.02f;
            s.emitsLaneMarkings = true;
            s.paintMarginMetres = 0.5f;
            s.requiresSideDitch = false;
            s.materialSetId = "surface.sealed.default";
            return s;
        }

        /// <summary> The AUTHORED unsealed (gravel / murram / laterite) surface: steeper crown, NO markings, a wide
        /// dust verge, and a required side ditch. </summary>
        public static SurfaceTypeClass Unsealed()
        {
            SurfaceTypeClass s = new SurfaceTypeClass();
            s.surfaceName = "Unsealed";
            s.crownCrossSlope = 0.04f;
            s.emitsLaneMarkings = false;
            s.paintMarginMetres = 4f;
            s.requiresSideDitch = true;
            s.materialSetId = "surface.unsealed.gravel";
            return s;
        }

        public static SurfaceTypeClass Default()
        {
            return Sealed();
        }
    }

    /// <summary> RM-41's verifiable core: the surface-type decisions, without a scene. Selecting a surface type sets
    /// the crown, the marking policy, the painted-verge width and the drainage requirement together, and switching
    /// back restores the other surface's values with nothing left over -- because the surface is one asset, not four
    /// scattered fields. The painted width feeds RM-38's paint test unchanged (paint independent of deform width).
    /// The cross-section crown wiring, the marking suppression in the emitter, the ditch hand-off to RM-30 and the
    /// authored material application are the deferred scene half. </summary>
    public static class SurfaceType
    {
        /// <summary> The painted half-width for a surface: the flattened edge plus this surface's own verge margin
        /// (RM-38's rule that paint is its own quantity). </summary>
        public static double PaintHalfWidthMetres(double flattenedHalfWidthMetres, SurfaceTypeClass surface)
        {
            double w = flattenedHalfWidthMetres + surface.paintMarginMetres;
            return w > 0.0 ? w : 0.0;
        }

        /// <summary> Whether lane markings are emitted for a surface -- unsealed suppresses them entirely. </summary>
        public static bool EmitsLaneMarkings(SurfaceTypeClass surface)
        {
            return surface != null && surface.emitsLaneMarkings;
        }

        /// <summary> Whether a side ditch must be marked as required for a surface (RM-30). </summary>
        public static bool RequiresSideDitch(SurfaceTypeClass surface)
        {
            return surface != null && surface.requiresSideDitch;
        }

        /// <summary> Whether two surface selections carry identical values -- used to prove that switching to another
        /// surface and back leaves no residue (the "restores the sealed values with no leftover" gate). </summary>
        public static bool ValuesEqual(SurfaceTypeClass a, SurfaceTypeClass b)
        {
            if (a == null || b == null) { return a == b; }
            return a.surfaceName == b.surfaceName
                   && a.crownCrossSlope == b.crownCrossSlope
                   && a.emitsLaneMarkings == b.emitsLaneMarkings
                   && a.paintMarginMetres == b.paintMarginMetres
                   && a.requiresSideDitch == b.requiresSideDitch
                   && a.materialSetId == b.materialSetId;
        }
    }
}

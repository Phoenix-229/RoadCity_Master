#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which kind of city element a <see cref="CitySelection"/> points at, ordered from the
    /// BROADEST (a whole block) to the NARROWEST (a single lot corner), with the network node treated as
    /// the thing "behind" the geometry -- RM-16's hierarchical selection descends this order on repeated
    /// clicks at the same spot. <see cref="None"/> is the empty selection. </summary>
    public enum CityElementKind
    {
        /// <summary> Nothing is selected. </summary>
        None = 0,

        /// <summary> A whole block -- the broadest pick. </summary>
        Block = 1,

        /// <summary> One lot within a block. </summary>
        Lot = 2,

        /// <summary> A single corner of a lot polygon -- the narrowest geometry pick. </summary>
        LotCorner = 3,

        /// <summary> A street-network node -- the graph vertex "behind" the derived geometry. </summary>
        NetworkNode = 4
    }


    /// <summary> An immutable pointer to one selected city element, deliberately identifying elements by
    /// their STABLE id (or, for a node, its index) rather than by object reference -- so a selection
    /// survives a re-bake that reorders <see cref="CityData.blocks"/> / <see cref="CityData.lots"/>, the
    /// same "indices/ids, not references" discipline <see cref="CityData"/> itself keeps. RM-16's scene
    /// handles and its inspector both read a selection of this shape; the resolver
    /// (<see cref="CitySelectionResolver"/>) produces it and the edit ops (<see cref="CityEditOps"/>)
    /// consume it, so there is one selection type, not one per caller. </summary>
    [Serializable]
    public struct CitySelection : IEquatable<CitySelection>
    {
        /// <summary> What kind of element this points at. </summary>
        public CityElementKind kind;

        /// <summary> The element's identity: a <see cref="NetworkNode"/> INDEX into
        /// <see cref="CityData.nodes"/> when <see cref="kind"/> is <see cref="CityElementKind.NetworkNode"/>;
        /// a <see cref="Block.id"/> for <see cref="CityElementKind.Block"/>; a <see cref="Lot.id"/> for
        /// <see cref="CityElementKind.Lot"/> and <see cref="CityElementKind.LotCorner"/>. Meaningless
        /// (and left 0) for <see cref="CityElementKind.None"/>. </summary>
        public int primaryId;

        /// <summary> For <see cref="CityElementKind.LotCorner"/>, the index of the corner in the lot's
        /// <see cref="Lot.polygon"/>. -1 for every other kind. </summary>
        public int cornerIndex;

        /// <summary> The empty selection. </summary>
        public static CitySelection None
        {
            get { return new CitySelection { kind = CityElementKind.None, primaryId = 0, cornerIndex = -1 }; }
        }

        /// <summary> A block selection by <see cref="Block.id"/>. </summary>
        public static CitySelection ForBlock(int blockId)
        {
            return new CitySelection { kind = CityElementKind.Block, primaryId = blockId, cornerIndex = -1 };
        }

        /// <summary> A lot selection by <see cref="Lot.id"/>. </summary>
        public static CitySelection ForLot(int lotId)
        {
            return new CitySelection { kind = CityElementKind.Lot, primaryId = lotId, cornerIndex = -1 };
        }

        /// <summary> A single-corner selection: a lot id plus the corner's index in its polygon. </summary>
        public static CitySelection ForLotCorner(int lotId, int corner)
        {
            return new CitySelection { kind = CityElementKind.LotCorner, primaryId = lotId, cornerIndex = corner };
        }

        /// <summary> A network-node selection by its index in <see cref="CityData.nodes"/>. </summary>
        public static CitySelection ForNode(int nodeIndex)
        {
            return new CitySelection { kind = CityElementKind.NetworkNode, primaryId = nodeIndex, cornerIndex = -1 };
        }

        public bool Equals(CitySelection other)
        {
            return kind == other.kind && primaryId == other.primaryId && cornerIndex == other.cornerIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is CitySelection other && Equals(other);
        }

        /// <summary> A deterministic hash of the three fields (no reference identity, no
        /// <c>object.GetHashCode</c> on a reference) so a selection can key a dictionary reproducibly.
        /// This is a pure integer-field override for in-editor dictionary keying, not a string
        /// GetHashCode on a generation path.
        /// determinism-exempt </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)kind;
                h = h * 397 + primaryId;
                h = h * 397 + cornerIndex;
                return h;
            }
        }
    }
}

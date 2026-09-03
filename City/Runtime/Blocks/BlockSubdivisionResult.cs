#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A tally of what the block-subdivision operator did, carried alongside its result so a
    /// caller (and the RM-10 gate) can see how many faces were found, how many survived the inset, and
    /// how many lots came out. Pure data, produced by the operator and never read back into it. </summary>
    public sealed class BlockSubdivisionReport
    {
        /// <summary> Closed bounded faces the planar traversal found in the network. </summary>
        public int faces;

        /// <summary> Faces that produced a valid inset block. </summary>
        public int blocks;

        /// <summary> Faces dropped because the inset collapsed -- the block was too thin for the
        /// street reservation to leave any buildable land, or the mitred offset self-intersected. </summary>
        public int collapsed;

        /// <summary> Lots produced across all blocks. </summary>
        public int lots;

        /// <summary> A single-line human summary for the log. </summary>
        public override string ToString()
        {
            return "faces " + faces + " blocks " + blocks +
                   " collapsed " + collapsed + " lots " + lots;
        }
    }


    /// <summary> The transient result of the block-subdivision stage: the blocks the network's faces
    /// became and the lots those blocks were cut into, plus the report. It is the currency RM-10 hands
    /// on -- its <see cref="blocks"/> and <see cref="lots"/> are written into
    /// <see cref="CityData.blocks"/> / <see cref="CityData.lots"/> at bake time. A plain class, not an
    /// asset: the persisted form is <see cref="CityData"/>. The <see cref="Block"/> and <see cref="Lot"/>
    /// records here are the same serialisable records that end up on disk, already carrying their stable
    /// ids so a later re-bake can reattach overrides. </summary>
    public sealed class BlockSubdivisionResult
    {
        /// <summary> The inset, buildable blocks, one per surviving face, in the deterministic order
        /// the faces were discovered. </summary>
        public readonly List<Block> blocks = new List<Block>();

        /// <summary> The lots, each carrying the <see cref="Block.id"/> of its parent block. </summary>
        public readonly List<Lot> lots = new List<Lot>();

        /// <summary> What the operator did to build this result. </summary>
        public readonly BlockSubdivisionReport report = new BlockSubdivisionReport();
    }
}

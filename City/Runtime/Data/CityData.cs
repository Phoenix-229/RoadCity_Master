// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The serialisable shape of one city: its street network, the blocks and lots that
    /// network encloses, and the buildings placed on those lots. One asset per city.
    ///
    /// This type is the whole content of RM-08, and the single decision it encodes is what is
    /// SAVED versus what is DERIVED:
    ///
    ///   SAVED    the author's decisions and the generator's inputs -- things a re-bake must never
    ///            overwrite: <see cref="seed"/>, <see cref="generatorVersion"/>, <see cref="nodes"/>,
    ///            <see cref="edges"/>, <see cref="buildings"/>, and any block or lot whose
    ///            isOverridden flag is set.
    ///   DERIVED  everything a deterministic function of the saved data can reproduce: the geometry
    ///            of every non-overridden <see cref="blocks"/> and <see cref="lots"/> entry. RM-20's
    ///            re-bake recomputes these from the network and the seed.
    ///
    /// Note that "derived" does NOT mean "absent from disk". Section 10.3 requires a baked city to
    /// open and enter play mode with no generator running, so the derived geometry is written into
    /// the saved asset at bake time. The saved-versus-derived line is therefore about what a
    /// re-bake will recompute, not about what is persisted. <see cref="ClearDerived"/> is the exact
    /// boundary: it drops precisely what a re-bake would recompute and keeps precisely what a
    /// re-bake would preserve.
    ///
    /// Three conventions here are load-bearing, not stylistic:
    ///   * INDICES, NOT REFERENCES. <see cref="NetworkEdge"/> refers to nodes by their index in
    ///     <see cref="nodes"/>, and blocks/lots/buildings refer to each other by <c>id</c>, never by
    ///     object reference. Unity's serialiser cannot round-trip a shared reference inside a plain
    ///     [Serializable] graph and silently duplicates the referenced object instead, turning one
    ///     node shared by four edges into four separate nodes on reload.
    ///   * <see cref="schemaVersion"/> EXISTS FROM THE FIRST COMMIT, because the first schema change
    ///     happens long before anyone thinks to add a version field, and retrofitting one means
    ///     guessing what old assets contain.
    ///   * <see cref="generatorVersion"/> IS A STRING recorded in the asset, so RM-20's bake can tell
    ///     an author that this city was produced by an older generator and re-baking will change it
    ///     -- information the author needs before the change happens, not after. </summary>
    [CreateAssetMenu(fileName = "City", menuName = "Road City Master/City Data", order = 0)]
    public sealed class CityData : ScriptableObject
    {
        /// <summary> The schema version this file's shape was written under. Bumped by any change to
        /// the shape of this type or the records it holds, so a loader can migrate or refuse an
        /// asset written by a different version. SAVED. See <see cref="CurrentSchemaVersion"/>. </summary>
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary> The generator input, not a result: the same seed and the same network reproduce
        /// the same derived city. SAVED. </summary>
        public int seed;

        /// <summary> Semantic version of the generation code that produced this city, recorded so a
        /// re-bake can warn on a mismatch rather than silently changing the result. SAVED. </summary>
        public string generatorVersion = "";

        /// <summary> The street-network vertices. SAVED -- graph input, never recomputed. </summary>
        public List<NetworkNode> nodes = new List<NetworkNode>();

        /// <summary> The street-network segments, referring to <see cref="nodes"/> by index. SAVED --
        /// graph input, never recomputed. </summary>
        public List<NetworkEdge> edges = new List<NetworkEdge>();

        /// <summary> The blocks the network encloses. DERIVED (recomputed by a re-bake) except for
        /// entries whose <see cref="Block.isOverridden"/> is set, which are preserved. Serialised in
        /// full so play mode needs no generator. </summary>
        public List<Block> blocks = new List<Block>();

        /// <summary> The lots the blocks are subdivided into. DERIVED except for overridden entries,
        /// exactly as <see cref="blocks"/>. </summary>
        public List<Lot> lots = new List<Lot>();

        /// <summary> The placed buildings. SAVED in full -- never recomputed by a re-bake. </summary>
        public List<BuildingInstance> buildings = new List<BuildingInstance>();


        /// <summary> The schema version the current code writes. This is the value RM-08 committed;
        /// any later change to the saved shape increments it and adds a migration step. </summary>
        public const int CurrentSchemaVersion = 1;


        /// <summary> Drops exactly what a re-bake would recompute -- every non-overridden block and
        /// lot -- and keeps exactly what a re-bake would preserve: the seed, the network, the
        /// buildings, and every overridden block and lot. This is the operational definition of the
        /// saved-versus-derived boundary; RM-20's bake calls it before re-deriving so an author's
        /// overrides survive while stale derived geometry does not. </summary>
        public void ClearDerived()
        {
            blocks.RemoveAll(block => block != null && !block.isOverridden);
            lots.RemoveAll(lot => lot != null && !lot.isOverridden);
        }


        /// <summary> True when this asset was written under the schema the current code understands.
        /// A loader that reads false must migrate or refuse the asset rather than trust its
        /// fields. </summary>
        public bool IsCurrentSchema
        {
            get { return schemaVersion == CurrentSchemaVersion; }
        }
    }
}

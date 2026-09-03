#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What kind of terrain modification a call represents. Kept separate from HOW it is carried out: the
    /// same kind is a direct write in standalone mode and a request in connected mode, and that is the whole point of
    /// RM-57's gateway. </summary>
    public enum TerrainWriteKind
    {
        /// <summary> The heightfield: road cuts and fills, building pads, rail formation. </summary>
        Heights = 0,

        /// <summary> A detail (grass) layer cleared where the road now is. </summary>
        DetailLayer = 1,

        /// <summary> The tree instance set, with trees standing in the corridor removed. </summary>
        Trees = 2,

        /// <summary> Splat / alphamap painting, where a surface material is laid down. </summary>
        Splat = 3,

        /// <summary> Terrain holes, for a tunnel mouth or a cavity. </summary>
        Holes = 4,

        /// <summary> Putting the terrain back as it was, when a road or structure is deleted. </summary>
        Restore = 5
    }


    /// <summary> How a modification was actually carried out. This is the answer to RM-57's "the tool states which
    /// path it used", and it is recorded per call rather than inferred, so a mixed build cannot hide. </summary>
    public enum TerrainWritePath
    {
        /// <summary> Standalone: the tool wrote the terrain itself, exactly as the vendored tool always has. </summary>
        DirectWrite = 0,

        /// <summary> Connected: the provider was told what changed and owns the terrain; the tool wrote nothing.
        /// </summary>
        RequestedThroughProvider = 1,

        /// <summary> Neither: the feature cannot exist in this mode and was refused in words, not faked. </summary>
        Refused = 2
    }


    /// <summary> The three things the tool can ASK a provider to hold open on its behalf. Each one is paired with a
    /// release on the gateway, because a request that is never released leaves an orphaned modification behind.
    /// </summary>
    public enum TerrainRequestKind
    {
        /// <summary> A region the tool is about to change, so the provider can pre-load and heal it. </summary>
        CutRegion = 0,

        /// <summary> A hole through the surface for a tunnel mouth, kept cut across regenerations. </summary>
        TunnelOpening = 1,

        /// <summary> A flat building pad held against terrain regeneration (RM-22). </summary>
        BuildingPad = 2
    }


    /// <summary> A ticket for something the tool asked to have held open. It carries the provider's own handle when
    /// there is one, so a release goes back to the same object that granted it, and it is a struct with an explicit
    /// <see cref="IsValid"/> so a refusal returns an obviously-dead ticket rather than a plausible-looking zero.
    /// </summary>
    public struct TerrainRequestHandle
    {
        /// <summary> The gateway's own id, unique within one gateway and assigned by a counter (never a hash or a
        /// GUID, so a build is reproducible -- C-06). Zero means "no request". </summary>
        public int id;

        public TerrainRequestKind kind;

        /// <summary> The handle the provider gave back, for the kinds the provider grants. </summary>
        public int providerHandle;

        /// <summary> Who asked, so an unreleased request names its owner instead of just a number. </summary>
        public string requesterId;

        public TerrainRegion region;

        /// <summary> True when this ticket refers to a live request that MUST still be released. </summary>
        public bool IsValid { get { return id != 0; } }

        /// <summary> A dead ticket, for a refused or unsupported request. </summary>
        public static TerrainRequestHandle None
        {
            get
            {
                TerrainRequestHandle h;
                h.id = 0;
                h.kind = TerrainRequestKind.CutRegion;
                h.providerHandle = 0;
                h.requesterId = string.Empty;
                h.region = TerrainRegion.Empty;
                return h;
            }
        }

        /// <summary> One line naming the outstanding request, for the review the spec asks for. </summary>
        public string Describe()
        {
            return string.Format("#{0} {1} for '{2}' {3}", id, kind,
                string.IsNullOrEmpty(requesterId) ? "(unnamed)" : requesterId, region.Describe());
        }
    }


    /// <summary> What one call through the gateway did: the kind of change, the path it took, the region it touched
    /// and, when it was refused, the words that say why and what to connect. Returned from every write so a caller
    /// cannot ignore a refusal by accident. </summary>
    public struct TerrainWriteReceipt
    {
        public TerrainWriteKind kind;
        public TerrainWritePath path;
        public TerrainRegion region;

        /// <summary> Who or what asked for the change (a road name, a pad id), for the ledger. </summary>
        public string requesterId;

        /// <summary> Empty unless <see cref="path"/> is <see cref="TerrainWritePath.Refused"/>. </summary>
        public string refusalReason;

        public bool Accepted { get { return path != TerrainWritePath.Refused; } }

        public string Describe()
        {
            string head = string.Format("{0} via {1} {2}", kind, path, region.Describe());
            return string.IsNullOrEmpty(refusalReason) ? head : head + " -- " + refusalReason;
        }
    }


    /// <summary> The engine-facing half of a terrain modification, behind one interface so that the gateway holds the
    /// mode decision and the rules, and the sink holds only the mechanics. There are exactly two production
    /// implementations -- the direct one, which is the vendored tool's own path preserved unchanged, and the provider
    /// one -- plus a recording double in the gate. This is the seam that lets RM-57's search be meaningful: the ONLY
    /// place in the tool allowed to touch <c>TerrainData</c> is the direct sink. </summary>
    public interface ITerrainWriteSink
    {
        /// <summary> A name for the report: "direct", or the provider's own name. </summary>
        string SinkName { get; }

        /// <summary> Which path calls through this sink take. </summary>
        TerrainWritePath Path { get; }

        /// <summary> Apply a full heightfield to one terrain. <paramref name="heights"/> is normalised 0..1 in the
        /// engine's own convention, indexed [z, x]. </summary>
        void WriteHeights(Terrain terrain, float[,] heights);

        /// <summary> Replace one detail layer's density map on one terrain. </summary>
        void WriteDetailLayer(Terrain terrain, int layerIndex, int[,] density);

        /// <summary> Replace the tree instance set on one terrain. </summary>
        void WriteTrees(Terrain terrain, TreeInstance[] trees);

        /// <summary> Replace the alphamap block starting at (x, z) on one terrain. </summary>
        void WriteSplat(Terrain terrain, int x, int z, float[,,] alphamaps);

        /// <summary> Replace the hole map block starting at (x, z) on one terrain. </summary>
        void WriteHoles(Terrain terrain, int x, int z, bool[,] holes);

        /// <summary> Tell whoever owns the terrain that this region changed. Called ONCE per build with the union.
        /// </summary>
        void NotifyRegionChanged(TerrainRegion region);

        /// <summary> RM-98. Make the drawn surface, the collided surface and what was just written agree, and do it
        /// whichever way the operation ended -- a roll-back settles too, or the collider keeps the shape that was
        /// abandoned.
        ///
        /// This is a member of the interface rather than a call the gateway makes for itself because only the sink
        /// knows what "settled" means on its side: in standalone mode it is the engine's own flush plus re-establishing
        /// the collider's data, and in connected mode the provider owns both and there is nothing to do but count the
        /// call so the two modes can be compared. Leaving it out is the first of the four ways the ground gets a hole:
        /// heights written, collider stale, character walks through visibly solid ground. </summary>
        void Settle(Terrain terrain, bool holesChanged);

        /// <summary> Ask for something to be held open. Returns the granting side's handle, or a negative number
        /// when this sink cannot grant that kind at all. </summary>
        int RequestHold(TerrainRequestKind kind, TerrainRegion region);

        /// <summary> Give back a hold previously granted by <see cref="RequestHold"/>. </summary>
        void ReleaseHold(TerrainRequestKind kind, int providerHandle);
    }


    /// <summary> Reading the surface, which goes the same two ways as writing it: in connected mode the provider is
    /// the authority on height and normal and on which chunks are loaded, and in standalone mode the engine's own
    /// <see cref="Terrain"/> is. Kept separate from <see cref="ITerrainWriteSink"/> so a caller that only samples the
    /// surface cannot accidentally hold the thing that modifies it. </summary>
    public interface ITerrainSurfaceReader
    {
        /// <summary> World-space height at (x, z), for snapping a spline point to the surface. </summary>
        float SampleHeight(float worldX, float worldZ);

        /// <summary> Surface normal at (x, z), for banking and slope analysis. </summary>
        Vector3 SampleNormal(float worldX, float worldZ);

        /// <summary> The regions currently loaded, so work over unloaded ground can be deferred rather than done
        /// against absent data. An empty list means "everything is here" only in standalone mode, where there is no
        /// streaming; a connected reader that returns an empty list means nothing is loaded. </summary>
        List<Bounds> GetActiveChunkBounds();
    }


    /// <summary> One entry of the gateway's ledger: what was asked for, in order, WITHOUT the dispatch path. Two
    /// builds of the same road -- one standalone, one connected -- must produce identical ledgers, and that is how
    /// RM-57's "switching modes with the same road produces the same road" becomes a check a machine can run rather
    /// than a claim a human makes. </summary>
    public struct TerrainLedgerEntry
    {
        public TerrainWriteKind kind;
        public TerrainRegion region;
        public string requesterId;

        /// <summary> The comparable form: kind, requester and region, but never the path or the sink name. </summary>
        public string Signature()
        {
            return string.Format("{0}|{1}|{2}", kind, requesterId, region.Describe());
        }
    }


    /// <summary> The ledger of what was asked for, in call order.
    ///
    /// The gateway that owns this lives for the whole session, so an unbounded list here would be a slow leak: every
    /// road, every rebuild, every delete would add entries that nothing ever removes. It is therefore capped. The cap
    /// is high enough that a whole city's build is recorded in full, and <see cref="TotalAdded"/> keeps counting past
    /// it so nothing is silently lost -- <see cref="Dropped"/> being non-zero is information, not a failure.
    /// </summary>
    public sealed class TerrainLedger
    {
        /// <summary> How many entries are kept. Past this, entries are counted but not stored. </summary>
        public const int Capacity = 8192;

        private readonly List<TerrainLedgerEntry> entries = new List<TerrainLedgerEntry>();
        private int totalAdded;

        public void Add(TerrainWriteKind kind, TerrainRegion region, string requesterId)
        {
            totalAdded++;
            if (entries.Count >= Capacity) { return; }
            TerrainLedgerEntry e;
            e.kind = kind;
            e.region = region;
            e.requesterId = requesterId != null ? requesterId : string.Empty;
            entries.Add(e);
        }

        public int Count { get { return entries.Count; } }

        /// <summary> Everything ever recorded, including what the cap turned away. </summary>
        public int TotalAdded { get { return totalAdded; } }

        /// <summary> How many entries the cap turned away. </summary>
        public int Dropped { get { return totalAdded - entries.Count; } }

        public TerrainLedgerEntry this[int index] { get { return entries[index]; } }

        public void Clear() { entries.Clear(); totalAdded = 0; }

        /// <summary> The whole ledger as one comparable string. </summary>
        public string Signature()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) { sb.Append('\n'); }
                sb.Append(entries[i].Signature());
            }
            return sb.ToString();
        }
    }
}

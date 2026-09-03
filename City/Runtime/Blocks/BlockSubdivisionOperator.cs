#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-10: the block-subdivision operator. It is the one pipeline stage that turns the
    /// street network into buildable parcels, composing the three published algorithms that live in
    /// this folder -- planar face traversal (<see cref="PlanarFaceFinder"/>), mitred inward inset
    /// (<see cref="PolygonInset"/>) and Parish and Müller recursive OBB subdivision
    /// (<see cref="LotSubdivider"/>) -- behind the pure <see cref="CityOperator{TIn,TOut}"/> contract.
    ///
    /// Each network face becomes a block: the face polygon (running down the street centrelines) is
    /// pulled in, per edge, by that street's half-width plus footway plus setback, and the surviving
    /// inset is cut into lots that each front a street. A face whose inset collapses is not emitted --
    /// it is counted in the report as a block too thin for its reservation.
    ///
    /// Determinism (C-06) is threaded through a STABLE FACE KEY rather than visitation order: the key
    /// is an order-independent hash of the face's sorted node indices, the block id is derived from it,
    /// and the subdivision seed is <see cref="CitySeed.ForItem"/> of the operator seed and that key. So
    /// the same network and the same operator seed yield the same blocks and lots, field for field,
    /// regardless of the order the traversal happened to discover the faces in. </summary>
    [CreateAssetMenu(fileName = "BlockSubdivisionOperator",
        menuName = "Road City Master/Operators/Block Subdivision", order = 11)]
    public sealed class BlockSubdivisionOperator : CityOperator<BlockSubdivisionInput, BlockSubdivisionResult>
    {
        /// <summary> The authored road-class catalogue whose per-class widths set how far each block
        /// edge is inset for its bordering street. Read, never written; may be null, in which case an
        /// edge insets by footway plus setback alone. </summary>
        public RoadClassCatalog catalog;

        /// <summary> Footway (pavement) width in metres added to every street's half-width when
        /// insetting a block edge -- the walkable strip between kerb and lot line. </summary>
        public float footwayWidth = 2f;

        /// <summary> Zoning setback in metres from the footway to the buildable lot line. Until the
        /// zoning taxonomy exists (RM-11 / RM-42) it is one authored value; later it becomes per-zone. </summary>
        public float setback = 3f;

        /// <summary> Target lot area in square metres: a block is recursively split until each part is
        /// under this, where the frontage rule allows. One authored value now, per-zone later (RM-11). </summary>
        public float targetLotArea = 800f;

        /// <summary> How far the split ratio may wander from the midline, 0 (always halve) to 1 (may
        /// cut anywhere in the legal 0.2-0.8 band). Drawn per split from the per-face seed. </summary>
        [Range(0f, 1f)]
        public float splitJitter = 0.35f;

        /// <summary> Finds the network's faces, insets each into a block, and cuts each surviving
        /// block into lots -- filling the <see cref="BlockSubdivisionResult"/> and its report. Pure:
        /// depends only on <paramref name="input"/>, this asset's fields and <paramref name="operatorSeed"/>. </summary>
        public override BlockSubdivisionResult Execute(BlockSubdivisionInput input, int operatorSeed)
        {
            BlockSubdivisionResult result = new BlockSubdivisionResult();
            if (input == null || input.network == null)
            {
                return result;
            }

            StreetNetwork net = input.network;
            Dictionary<long, int> edgeByPair = BuildEdgeLookup(net);

            List<List<int>> faces = PlanarFaceFinder.FindFaces(net);
            result.report.faces = faces.Count;

            for (int f = 0; f < faces.Count; f++)
            {
                List<int> faceNodes = faces[f];

                // The face polygon runs down the street centrelines; inset each edge by its own street.
                List<Vector3> facePolygon = new List<Vector3>(faceNodes.Count);
                List<float> insetPerEdge = new List<float>(faceNodes.Count);
                for (int i = 0; i < faceNodes.Count; i++)
                {
                    facePolygon.Add(net.nodes[faceNodes[i]].position);
                    int na = faceNodes[i];
                    int nb = faceNodes[(i + 1) % faceNodes.Count];
                    insetPerEdge.Add(InsetForEdge(net, edgeByPair, na, nb));
                }

                List<Vector3> insetPolygon;
                if (!PolygonInset.TryInset(facePolygon, insetPerEdge, out insetPolygon))
                {
                    result.report.collapsed++;
                    continue;
                }

                int faceKey = FaceKey(faceNodes);
                Block block = new Block
                {
                    id = NonZero(faceKey),
                    boundaryNodes = new List<int>(faceNodes),
                    polygon = insetPolygon,
                    isOverridden = false
                };
                result.blocks.Add(block);
                result.report.blocks++;

                int faceSeed = CitySeed.ForItem(operatorSeed, faceKey);
                List<List<Vector3>> lotPolygons =
                    LotSubdivider.Subdivide(block.polygon, targetLotArea, splitJitter, faceSeed);

                for (int li = 0; li < lotPolygons.Count; li++)
                {
                    result.lots.Add(new Lot
                    {
                        id = NonZero(CitySeed.ForItem(block.id, li)),
                        blockId = block.id,
                        polygon = lotPolygons[li],
                        zoneId = 0,
                        isOverridden = false
                    });
                }
            }

            result.report.lots = result.lots.Count;
            return result;
        }

        /// <summary> Maps each unordered node pair to its edge index, so a face edge can find the
        /// street it runs along and read that street's width for the inset. </summary>
        private static Dictionary<long, int> BuildEdgeLookup(StreetNetwork net)
        {
            Dictionary<long, int> map = new Dictionary<long, int>(net.edges.Count);
            for (int i = 0; i < net.edges.Count; i++)
            {
                long key = PairKey(net.edges[i].a, net.edges[i].b);
                if (!map.ContainsKey(key))
                {
                    map[key] = i;
                }
            }
            return map;
        }

        /// <summary> The inset distance for the face edge between two nodes: the bordering street's
        /// half-width (from its road class) plus footway plus setback. When the pair has no edge or no
        /// resolvable class -- neither should happen for a real face -- the half-width is zero, so the
        /// edge still insets by footway plus setback rather than not at all. </summary>
        private float InsetForEdge(StreetNetwork net, Dictionary<long, int> edgeByPair, int na, int nb)
        {
            float halfWidth = 0f;
            int edgeIndex;
            if (edgeByPair.TryGetValue(PairKey(na, nb), out edgeIndex))
            {
                NetworkEdge edge = net.edges[edgeIndex];
                RoadClass rc = catalog != null ? catalog.Get(edge.roadClassId) : null;
                if (rc != null)
                {
                    halfWidth = rc.TotalWidth * 0.5f;
                }
            }
            return halfWidth + footwayWidth + setback;
        }

        /// <summary> Packs an unordered node-index pair into one long key (lower index first), so the
        /// lookup is direction-independent -- a face may traverse an edge either way. </summary>
        private static long PairKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        /// <summary> An order-independent identity for a face: a hash of its node indices sorted
        /// ascending, so the id a block gets does not depend on which node the traversal happened to
        /// start the loop on. This is the block's stable identity across a re-bake (RM-20). </summary>
        private static int FaceKey(List<int> faceNodes)
        {
            List<int> sorted = new List<int>(faceNodes);
            sorted.Sort();
            int key = sorted.Count;
            for (int i = 0; i < sorted.Count; i++)
            {
                key = CitySeed.ForItem(key, sorted[i]);
            }
            return key;
        }

        /// <summary> Maps a hash to a non-zero id, since 0 is the reserved "unassigned" value on
        /// <see cref="Block"/> and <see cref="Lot"/>. Deterministic: the same hash always maps the
        /// same way. </summary>
        private static int NonZero(int value)
        {
            return value == 0 ? 1 : value;
        }
    }
}

#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> D-15. The families of INSTANCED content this register (D-11 .. D-14, and D-17's sweep) places along a
    /// run -- each one a single cached mesh drawn many times, which is D-15's whole cost model: N of a family are
    /// <b>one mesh and one instanced draw call</b>, so the count that can hurt a phone is not the total but how many
    /// land in <i>one streaming chunk</i>.
    ///
    /// A family exists here for exactly one reason: to carry a per-chunk instance CEILING (<see cref="CityInstanceBudget"/>).
    /// An author who draws a pathological run -- a transmission line staked with a tower every metre, a river bank
    /// railed at ten-centimetre centres -- is told the count and refused, rather than shipped a chunk that stutters on
    /// the flagship-and-mid-flagship phone D-15 is answering for. The ceiling is a BAKE-TIME gate on what the run
    /// produces; it is never consulted per frame, so it adds no play-time cost of its own (the no-per-frame-alloc rule
    /// D-16 asserts).
    ///
    /// These are the GENERATED families only -- the ones the tool makes from a number and instances. The author's own
    /// heavy prefabs (buildings, their bridge decks, the tower models they bring) are NOT here: those stream on RM-17's
    /// building path, budgeted by <see cref="CitySpawnTable"/> and the per-frame instantiation budget, which is a
    /// different mechanism for a different cost. </summary>
    public enum CityInstanceFamily
    {
        /// <summary> A lattice transmission tower (D-14). One cached mesh per <c>TowerMeshKey</c>, one instanced draw
        /// per tower along the run. </summary>
        TransmissionTower = 0,

        /// <summary> One transmission span's conductors and shield wire between two towers (D-14) -- a handful of
        /// catenary segments, placed as one group per span. </summary>
        TransmissionSpan = 1,

        /// <summary> A distribution / overhead-line pole (D-1's pole line). One cached pole mesh, one draw per pole. </summary>
        DistributionPole = 2,

        /// <summary> One overhead-line span between two distribution poles -- the conductor run a pole line hangs. </summary>
        OverheadLineSpan = 3,

        /// <summary> A rail sleeper (D-5). Instanced at the class sleeper spacing along the alignment. </summary>
        RailSleeper = 4,

        /// <summary> A railing post along a river bank, bridge edge or park edge (D-12). Instanced at its spacing on
        /// the pavement side of a top. </summary>
        RailingPost = 5,

        /// <summary> A small instanced streetscape prop -- a bench, a bin, a bollard (D-17's street families). Sparse
        /// by nature; the ceiling is a guard against a spacing authored to zero. </summary>
        StreetFurniture = 6,

        /// <summary> A railway overhead line mast (D-7). One cached mesh per specification, one draw per mast. </summary>
        OHLEMast = 7
    }


    /// <summary> The answer <see cref="CityInstanceBudget.Check"/> gives for one family in one run: the ceiling, the
    /// count found in the busiest chunk, whether it is within, and -- when it is not -- the sentence a planner adds to
    /// its refusals so the author reads the count rather than feeling the stutter. </summary>
    public struct CityInstanceCeilingVerdict
    {
        public CityInstanceFamily family;
        public int ceiling;
        public int countInBusiestChunk;
        public bool withinCeiling;

        /// <summary> Empty when within the ceiling; otherwise a one-line refusal naming the family, the count and the
        /// ceiling. </summary>
        public string message;
    }


    /// <summary> D-15's per-chunk instance ceilings, stated as NUMBERS rather than hoped for. Each ceiling is the
    /// realistic maximum a 100 m chunk of that family holds -- grounded on a real spacing where one exists -- times an
    /// AUTHORED headroom, so only a genuinely pathological run trips it. A run's planner counts its own instances into
    /// chunks (<see cref="BusiestChunkCount"/>), checks the busiest against the ceiling, and refuses with the count if
    /// it is over. Pure and static: no per-frame allocation, no update loop, nothing that touches determinism. </summary>
    public static class CityInstanceBudget
    {
        /// <summary> The per-chunk instance ceiling for a family. The realistic figure each headroom is set above is
        /// named in the comment; the headroom itself is AUTHORED, because "how far past realistic is still not
        /// pathological" is a judgement, not a physical constant (C-15). </summary>
        public static int CeilingFor(CityInstanceFamily family)
        {
            switch (family)
            {
                // A 100 m chunk holds ~2 towers on a straight run at the 80 m minimum span, up to ~4 where an angle
                // cluster meets a terminal. AUTHORED headroom to 8.
                case CityInstanceFamily.TransmissionTower: return 8;
                // Spans track towers: ~1-3 per chunk. AUTHORED headroom to 8.
                case CityInstanceFamily.TransmissionSpan: return 8;
                // Distribution spans run 30-50 m; a dense grid puts lines on several streets, ~20 poles a chunk.
                // AUTHORED headroom to 48.
                case CityInstanceFamily.DistributionPole: return 48;
                case CityInstanceFamily.OverheadLineSpan: return 48;
                // Sleepers sit at the real 0.6 m spacing: 100 m of single track is ~167, a double track ~334.
                // AUTHORED headroom to 600 covers a double track through a chunk.
                case CityInstanceFamily.RailSleeper: return 600;
                // Railing posts at ~2 m are ~50 a run; two banks plus a bridge and a park edge ~200. AUTHORED to 400.
                case CityInstanceFamily.RailingPost: return 400;
                // Street furniture is sparse -- a handful per street. AUTHORED to 120, a guard against a zero spacing.
                case CityInstanceFamily.StreetFurniture: return 120;
                // OHLE masts sit at ~50m intervals. A 100m chunk holds ~2. AUTHORED headroom to 200 covers a yard.
                case CityInstanceFamily.OHLEMast: return 200;
                default: return 0;
            }
        }

        /// <summary> Check a family's busiest-chunk instance count against its ceiling. A count at or below the ceiling
        /// is within; above it is refused, and the message names the family, the count and the ceiling so the planner
        /// can report it verbatim. A negative count is clamped to zero. </summary>
        public static CityInstanceCeilingVerdict Check(CityInstanceFamily family, int countInBusiestChunk)
        {
            CityInstanceCeilingVerdict v = new CityInstanceCeilingVerdict();
            v.family = family;
            v.ceiling = CeilingFor(family);
            v.countInBusiestChunk = countInBusiestChunk < 0 ? 0 : countInBusiestChunk;
            v.withinCeiling = v.countInBusiestChunk <= v.ceiling;
            v.message = v.withinCeiling
                ? string.Empty
                : Name(family) + ": " + v.countInBusiestChunk + " in one streaming chunk is past the per-chunk ceiling"
                    + " of " + v.ceiling + " -- the run is refused rather than shipped as a stutter; widen the spacing"
                    + " or split the run";
            return v;
        }

        /// <summary> How many instances land in the busiest chunk, given their world positions and the streaming chunk
        /// size. Buckets each position by <see cref="CityChunkGrid.ChunkOf"/> and returns the largest bucket -- the one
        /// number the ceiling is tested against. Order-independent and closed-form, so the same positions give the same
        /// answer on every bake (C-06). Runs at plan/bake time over a finite run, never per frame. </summary>
        public static int BusiestChunkCount(IReadOnlyList<Vector3> positions, float chunkSizeMetres)
        {
            if (positions == null || positions.Count == 0) { return 0; }
            Dictionary<ChunkCoord, int> tally = new Dictionary<ChunkCoord, int>();
            int worst = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                ChunkCoord cell = CityChunkGrid.ChunkOf(positions[i], chunkSizeMetres);
                int n;
                tally.TryGetValue(cell, out n);
                n++;
                tally[cell] = n;
                if (n > worst) { worst = n; }
            }
            return worst;
        }

        /// <summary> The human name of a family, for a refusal sentence and the streaming node's ceiling report. </summary>
        public static string Name(CityInstanceFamily family)
        {
            switch (family)
            {
                case CityInstanceFamily.TransmissionTower: return "Transmission towers";
                case CityInstanceFamily.TransmissionSpan: return "Transmission spans";
                case CityInstanceFamily.DistributionPole: return "Distribution poles";
                case CityInstanceFamily.OverheadLineSpan: return "Overhead line spans";
                case CityInstanceFamily.RailSleeper: return "Rail sleepers";
                case CityInstanceFamily.RailingPost: return "Railing posts";
                case CityInstanceFamily.StreetFurniture: return "Street furniture";
                case CityInstanceFamily.OHLEMast: return "Overhead line masts";
                default: return "Unknown family";
            }
        }

        /// <summary> Every family paired with its ceiling, in enum order -- the whole declaration as data, so the
        /// streaming node can publish it and D-16's gate can assert each number is present and positive. </summary>
        public static List<CityInstanceCeilingVerdict> AllCeilings()
        {
            List<CityInstanceCeilingVerdict> list = new List<CityInstanceCeilingVerdict>();
            CityInstanceFamily[] families =
            {
                CityInstanceFamily.TransmissionTower, CityInstanceFamily.TransmissionSpan,
                CityInstanceFamily.DistributionPole, CityInstanceFamily.OverheadLineSpan,
                CityInstanceFamily.RailSleeper, CityInstanceFamily.RailingPost,
                CityInstanceFamily.StreetFurniture
            };
            for (int i = 0; i < families.Length; i++)
            {
                CityInstanceCeilingVerdict v = new CityInstanceCeilingVerdict();
                v.family = families[i];
                v.ceiling = CeilingFor(families[i]);
                v.withinCeiling = true;
                v.message = string.Empty;
                list.Add(v);
            }
            return list;
        }
    }
}

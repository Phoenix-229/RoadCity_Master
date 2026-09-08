// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Deterministic derivation of a per-operator seed from the one city seed.
    ///
    /// Section 10.4 requires every pipeline operator to be a pure function of its input, its own
    /// parameters, and "a seed derived from the city seed" -- and C-06 requires the whole pipeline
    /// to be deterministic. Those two together forbid two things a naive implementation does by
    /// accident: seeding every operator from the same city seed (so unrelated stages produce
    /// correlated randomness), and seeding an operator from a running counter (so inserting a stage
    /// silently reshuffles every stage after it). Deriving the seed from the stable coordinates of
    /// the operator in the pipeline -- its stage index and its position within the stage -- avoids
    /// both: each operator gets an independent stream, and the streams do not move when an
    /// unrelated operator is added elsewhere.
    ///
    /// This is a seed-mixing primitive, not domain logic. It is not shared with
    /// UniverseCreator.MatrixOperations.HashPair, which mixes two floats into a 0-1 float for a
    /// terrain blend mode -- a different signature and a different output domain -- so reusing it
    /// here would mean adapting a float pipeline to an integer one, not calling the right function. </summary>
    public static class CitySeed
    {
        /// <summary> Derives the seed for one operator from the city seed and the operator's fixed
        /// coordinates in the pipeline. Same inputs always give the same result, and the results for
        /// distinct coordinates are uncorrelated. </summary>
        /// <param name="citySeed"> The one seed saved on <see cref="CityData.seed"/>. </param>
        /// <param name="stageIndex"> Index of the fixed pipeline stage the operator runs in. </param>
        /// <param name="operatorIndex"> The operator's position within that stage's operator list. </param>
        public static int ForOperator(int citySeed, int stageIndex, int operatorIndex)
        {
            return Mix(Mix(citySeed, stageIndex), operatorIndex);
        }

        /// <summary> Derives a sub-seed for one item within an operator -- a block, a lot, a
        /// building slot -- from the operator's seed and the item's stable key, so per-item
        /// randomness is reproducible and independent of the order items are visited in. </summary>
        public static int ForItem(int operatorSeed, int itemKey)
        {
            return Mix(operatorSeed, itemKey);
        }

        /// <summary> Integer avalanche (Wang / MurmurHash finaliser style): folds two ints into one
        /// well-distributed int. Allocation-free and stateless, so it is safe to call from worker
        /// threads. The constants are the standard MurmurHash3 32-bit finaliser constants. </summary>
        private static int Mix(int a, int b)
        {
            uint h = (uint)a * 2654435761u + (uint)b * 2246822519u + 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (int)h;
        }
    }
}

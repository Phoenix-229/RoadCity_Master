#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A stable 64-bit FNV-1a string hash and a per-element seed derivation. STABLE across processes
    /// (unlike string GetHashCode, which C-06's smell test bans), so a city keyed on an element's id reproduces
    /// tomorrow as it did today. This is the seed side of RM-47's rule: an instance's randomness is derived from the
    /// city seed AND the identity of the thing being scattered along, never from a shared global stream that advances
    /// as the pass walks the city. </summary>
    public static class DeterministicHash
    {
        /// <summary> Stable 64-bit FNV-1a of a string (the same algorithm the determinism audit prints). </summary>
        public static ulong Fnv1a64(string s)
        {
            ulong hash = 14695981039346656037UL;
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        /// <summary> Derive a per-element seed from the city seed and the element's stable id. Mixing the two means a
        /// street's trees are identical whether that street is built first or last -- no dependence on visitation
        /// order. </summary>
        public static ulong SeedFor(ulong citySeed, string elementId)
        {
            ulong h = Fnv1a64(elementId);
            // Mix the two independently-varying inputs so neither dominates (SplitMix64 finaliser style).
            ulong z = citySeed ^ (h + 0x9E3779B97F4A7C15UL + (citySeed << 6) + (citySeed >> 2));
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary> A tiny deterministic PRNG (SplitMix64), managed C# only (C-01), seeded per element by
    /// <see cref="DeterministicHash.SeedFor"/>. Not for cryptography -- for reproducible placement. Two runs from the
    /// same seed produce the identical stream (C-06). </summary>
    public struct DeterministicRandom
    {
        private ulong state;

        public DeterministicRandom(ulong seed) { state = seed; }

        public static DeterministicRandom ForElement(ulong citySeed, string elementId)
        {
            return new DeterministicRandom(DeterministicHash.SeedFor(citySeed, elementId));
        }

        /// <summary> The next 64-bit value in the stream. </summary>
        public ulong NextUInt64()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary> A double in [0, 1). </summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary> A double in [min, max). </summary>
        public double NextRange(double min, double max)
        {
            return min + (max - min) * NextDouble();
        }
    }
}

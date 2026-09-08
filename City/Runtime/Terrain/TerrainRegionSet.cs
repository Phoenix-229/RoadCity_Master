// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The ground a build changed, kept as a few tight boxes instead of one big one.
    ///
    /// This exists because of a mistake that is easy to make and expensive to live with. A build touches ground all
    /// along a road, and the obvious way to report it is to accumulate one box around everything. For a road that
    /// crosses the map diagonally that box IS the map, so the provider is asked to heal terrain that never changed,
    /// and a single road rebuilds the world. Announcing every segment separately is the opposite failure: a hundred
    /// round trips for one road.
    ///
    /// So: merge segments while merging is cheap, and stop when it stops being cheap. Each box remembers how much
    /// ground was actually added into it, and a merge is only accepted when the merged box is not much bigger than
    /// the ground it covers. Along a straight run neighbours collapse into one box; where a road turns or jumps to
    /// another part of the map the boxes stay separate. A hard limit then caps how many announcements a build can
    /// ever make, by merging the cheapest remaining pairs.
    ///
    /// Everything here is arithmetic on doubles in a fixed order, so the same road always produces the same set
    /// (C-06). </summary>
    public sealed class TerrainRegionSet
    {
        /// <summary> How much bigger a merged box may be than the ground it covers before the merge is refused.
        /// 1.35 keeps a straight corridor as one box while refusing to swallow a diagonal. </summary>
        public const double MergeSlack = 1.35;

        /// <summary> The most boxes a build may announce. A hundred-node road is capped at this many round trips,
        /// which is the whole point of batching. </summary>
        public const int DefaultLimit = 32;

        /// <summary> Where merging starts being forced rather than opportunistic. Kept above the limit so an ordinary
        /// build never pays for the cheapest-pair search. </summary>
        private const int HardCap = 128;

        private readonly List<TerrainRegion> regions = new List<TerrainRegion>();
        private readonly List<double> covered = new List<double>();
        private TerrainRegion union = TerrainRegion.Empty;

        public int Count { get { return regions.Count; } }
        public TerrainRegion this[int index] { get { return regions[index]; } }

        /// <summary> One box around everything, for callers that genuinely want the extent -- a status line, a gate
        /// checking nothing was missed. Never the thing that gets announced. </summary>
        public TerrainRegion Union { get { return union; } }

        public bool HasArea { get { return union.hasArea; } }

        /// <summary> The ground actually announced: the boxes' own areas added up. Compared against
        /// <see cref="Union"/>'s area this is the number that says whether batching helped. </summary>
        public double AnnouncedArea
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < regions.Count; i++) { total += regions[i].FootprintArea; }
                return total;
            }
        }

        /// <summary> The ground that was handed in, before any merging. Less than <see cref="AnnouncedArea"/> by
        /// whatever the merges rounded up. </summary>
        public double CoveredArea
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < covered.Count; i++) { total += covered[i]; }
                return total;
            }
        }

        public void Clear()
        {
            regions.Clear();
            covered.Clear();
            union = TerrainRegion.Empty;
        }

        /// <summary> Add one changed patch. Empty patches are ignored rather than becoming a box at the origin.
        ///
        /// A patch only joins a box that carries the SAME treatment (RM-98). Merging a tunnel span into the open
        /// approach beside it would produce one box that has to stand for both, and there is no honest label for that:
        /// call it a cut and the host flattens the hill, call it protected and the host stops re-flattening the
        /// approach after a regeneration. Keeping them apart costs one extra box per tunnel and is the whole reason the
        /// treatment can be trusted on the other side of the link. </summary>
        public void Add(TerrainRegion region)
        {
            if (!region.hasArea) { return; }
            union = union.Union(region);

            double area = region.FootprintArea;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].treatment != region.treatment) { continue; }
                TerrainRegion merged = regions[i].Union(region);
                if (merged.FootprintArea <= (covered[i] + area) * MergeSlack)
                {
                    regions[i] = merged;
                    covered[i] = covered[i] + area;
                    return;
                }
            }

            regions.Add(region);
            covered.Add(area);
            if (regions.Count > HardCap) { Coalesce(DefaultLimit); }
        }

        /// <summary> Force the set down to at most <paramref name="limit"/> boxes by repeatedly merging whichever
        /// pair wastes the least ground. Called once at the end of a build, and by <see cref="Add"/> if a build
        /// somehow produces more scattered patches than the hard cap.
        ///
        /// Pairs sharing a treatment are merged first, for the reason given on <see cref="Add"/>. Only when no such
        /// pair is left -- a build with more distinct treatments than the limit allows boxes -- does it merge across
        /// treatments, and then <see cref="TerrainRegion.Union"/> keeps the more protective of the two. That fallback
        /// exists so the loop always makes progress: refusing every merge here would spin forever. </summary>
        public void Coalesce(int limit)
        {
            if (limit < 1) { limit = 1; }
            while (regions.Count > limit)
            {
                int bestI = -1, bestJ = -1;
                double bestWaste = double.MaxValue;
                for (int i = 0; i < regions.Count - 1; i++)
                {
                    for (int j = i + 1; j < regions.Count; j++)
                    {
                        if (regions[i].treatment != regions[j].treatment) { continue; }
                        double waste = regions[i].Union(regions[j]).FootprintArea - covered[i] - covered[j];
                        if (waste < bestWaste)
                        {
                            bestWaste = waste;
                            bestI = i;
                            bestJ = j;
                        }
                    }
                }

                if (bestI < 0)
                {
                    bestI = 0;
                    bestJ = 1;
                    bestWaste = double.MaxValue;
                    for (int i = 0; i < regions.Count - 1; i++)
                    {
                        for (int j = i + 1; j < regions.Count; j++)
                        {
                            double waste = regions[i].Union(regions[j]).FootprintArea - covered[i] - covered[j];
                            if (waste < bestWaste)
                            {
                                bestWaste = waste;
                                bestI = i;
                                bestJ = j;
                            }
                        }
                    }
                }

                regions[bestI] = regions[bestI].Union(regions[bestJ]);
                covered[bestI] = covered[bestI] + covered[bestJ];
                regions.RemoveAt(bestJ);
                covered.RemoveAt(bestJ);
            }
        }

        /// <summary> What was announced, for a status line or a failing gate. </summary>
        public string Describe()
        {
            if (regions.Count < 1) { return "nothing changed"; }
            StringBuilder sb = new StringBuilder();
            sb.Append(regions.Count).Append(regions.Count == 1 ? " region" : " regions");
            for (int i = 0; i < regions.Count; i++)
            {
                sb.Append("\n  ").Append(regions[i].Describe());
            }
            return sb.ToString();
        }
    }
}

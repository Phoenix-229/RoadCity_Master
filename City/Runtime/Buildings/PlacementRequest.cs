// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One lot's request to place a building, matching the § 10.9 / RM-12 shape: the lot, the
    /// street-facing edge to orient against, the zoning envelope to obey, the pad elevation the base
    /// sits on, the per-lot seed, and the resolved prefab facts. It carries everything the placement
    /// solver reads and nothing it does not, so the solver stays a pure function of its request list
    /// (C-06) -- no scene access, no terrain sampling (the pad already resolved height into
    /// <see cref="padLevel"/>).
    ///
    /// FRONTAGE: <see cref="frontageEdge"/> is the two world-space endpoints of the lot's street edge.
    /// It is passed IN, not read off the <see cref="Lot"/>, because a lot does not store which of its
    /// edges fronts the street -- the caller derives it (the edge nearest the fronting road) and hands
    /// it here, exactly as the RM-12 spec's PlacementRequest does. </summary>
    public struct PlacementRequest
    {
        /// <summary> The lot the building stands on -- its polygon bounds the placement and its
        /// <see cref="Lot.id"/> is the stable key for deterministic ordering and the output record. </summary>
        public Lot lot;

        /// <summary> The lot's street-facing edge, as two world-space points [start, end]. RM-12 aims
        /// the building's forward (+Z) across this edge toward the street and sets it back from it. </summary>
        public List<Vector3> frontageEdge;

        /// <summary> The zoning envelope this lot is governed by (setbacks, coverage, FAR, height cap). </summary>
        public ZoneParams zone;

        /// <summary> The pad elevation, in metres, the building's base sits on -- from RM-22's
        /// <see cref="PadRecord.padElevation"/> for this lot. This is why the solver never touches
        /// terrain: the height question was already answered by the pad stage. </summary>
        public float padLevel;

        /// <summary> The stable per-lot seed (<see cref="CitySeed.ForItem"/> of the operator seed and
        /// the lot id). Carried for RM-55's seeded variant draw upstream; the placement GEOMETRY is
        /// deterministic without it, so the record stays reproducible either way. </summary>
        public int seedForLot;

        /// <summary> The chosen prefab's facts (category, variant, footprint, height, ground contact) --
        /// the seam to RM-77 / RM-55. </summary>
        public PrefabPlacementSpec prefab;

        public PlacementRequest(Lot lot, List<Vector3> frontageEdge, ZoneParams zone, float padLevel,
            int seedForLot, PrefabPlacementSpec prefab)
        {
            this.lot = lot;
            this.frontageEdge = frontageEdge;
            this.zone = zone;
            this.padLevel = padLevel;
            this.seedForLot = seedForLot;
            this.prefab = prefab;
        }
    }
}

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-12: places a building on each lot -- on the ground, facing the street, inside its
    /// lot -- and refuses, with a numbered reason, any that will not fit its zoning envelope. A PURE,
    /// deterministic solver in the mould of RM-22's <see cref="BuildingPadConform"/>: it reads only its
    /// request list, samples no terrain (the pad already resolved height into
    /// <see cref="PlacementRequest.padLevel"/>), touches no scene, and processes lots in id order so the
    /// output never depends on the order requests arrive (C-06).
    ///
    /// The three gate properties, made concrete:
    ///   ON THE GROUND -- the placed pivot's base Y is set to the pad elevation for EVERY ground-contact
    ///     mode, so no building floats above its pad and none is sunk below it. (Skirt and piled modes
    ///     differ in how RM-22/RM-14 treat the terrain AROUND the base, not in where the base sits.)
    ///   FACING THE STREET -- the building's local +Z (the contract's FORWARD) is aimed along the
    ///     OUTWARD normal of the frontage edge, so it faces its own street rather than a world axis.
    ///   INSIDE ITS LOT -- the footprint rectangle, oriented and set back, must lie wholly within the lot
    ///     polygon with at least the zone's side setback of clearance to the boundary, or the building is
    ///     refused rather than placed poking through a neighbour.
    ///
    /// A refusal ALWAYS carries the quantity, its value and the limit (the <see cref="LotVerdict"/>
    /// discipline), so an empty lot is explained, never silent. </summary>
    public static class BuildingPlacement
    {
        /// <summary> Places a building for each request and returns the placed instances, with the full
        /// per-lot verdict list (placed AND refused, each with its reason) written to
        /// <paramref name="verdicts"/>. Requests are processed in ascending lot id, so the result is
        /// independent of input order. </summary>
        public static List<BuildingInstance> ComputePlacements(
            IList<PlacementRequest> requests, out List<PlacementVerdict> verdicts)
        {
            List<PlacementRequest> ordered = new List<PlacementRequest>(requests);
            ordered.Sort((p, q) => p.lot.id.CompareTo(q.lot.id));

            verdicts = new List<PlacementVerdict>(ordered.Count);
            List<BuildingInstance> placed = new List<BuildingInstance>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                PlacementVerdict v = PlaceOne(ordered[i]);
                verdicts.Add(v);
                if (v.outcome == PlacementOutcome.Placed)
                {
                    placed.Add(v.instance);
                }
            }
            return placed;
        }

        /// <summary> Places (or refuses) one lot's building. Returns on the FIRST limit broken, naming
        /// it -- the same one-reason discipline RM-11 uses -- checking the cheap numeric zoning limits
        /// before the geometric fit. All geometry is worked in XZ through <see cref="Geometry2D"/>; no
        /// primitive is re-implemented here. </summary>
        private static PlacementVerdict PlaceOne(PlacementRequest req)
        {
            PlacementVerdict v = new PlacementVerdict();
            v.lotId = req.lot.id;
            v.outcome = PlacementOutcome.Refused;

            // ----- frontage must be a real edge -----
            if (req.frontageEdge == null || req.frontageEdge.Count < 2)
            {
                v.reason = "no frontage edge supplied";
                return v;
            }
            Vector2 p0 = Geometry2D.XZ(req.frontageEdge[0]);
            Vector2 p1 = Geometry2D.XZ(req.frontageEdge[1]);
            Vector2 edge = p1 - p0;
            if (edge.sqrMagnitude < 1e-6f)
            {
                v.reason = "frontage edge is degenerate (zero length)";
                return v;
            }

            // ----- orientation: +Z aims along the OUTWARD normal (away from the lot interior) -----
            Vector2 mid = (p0 + p1) * 0.5f;
            Vector2 centroid = Geometry2D.Centroid(req.lot.polygon);
            Vector2 nA = new Vector2(-edge.y, edge.x).normalized;
            Vector2 outward = Vector2.Dot(nA, centroid - mid) < 0f ? nA : -nA;
            Vector2 inward = -outward;
            Vector2 across = new Vector2(outward.y, -outward.x); // width axis, along the frontage

            // ----- envelope numbers -----
            ZoneParams z = req.zone;
            float lotArea = Geometry2D.Area(req.lot.polygon);
            float footArea = req.prefab.footprintSize.x * req.prefab.footprintSize.y;

            // Cheap numeric zoning limits first, each with its measured value beside the limit.
            float coverageLimit = z.maxSiteCoverage * lotArea;
            if (footArea > coverageLimit + 1e-3f)
            {
                v.reason = Fmt("footprint {0} m^2 exceeds coverage limit {1} m^2 ({2} of {3} m^2 lot)",
                    footArea, coverageLimit, z.maxSiteCoverage, lotArea);
                return v;
            }
            if (req.prefab.heightMetres > z.maxHeightMetres + 1e-3f)
            {
                v.reason = Fmt("height {0} m exceeds zone cap {1} m", req.prefab.heightMetres, z.maxHeightMetres);
                return v;
            }
            float storeyH = z.storeyHeightMetres > 0.01f ? z.storeyHeightMetres : 3f;
            int storeys = Mathf.Max(1, Mathf.FloorToInt(req.prefab.heightMetres / storeyH));
            float grossFloor = footArea * storeys;
            float farLimit = z.maxPlotRatio * lotArea;
            if (grossFloor > farLimit + 1e-3f)
            {
                v.reason = Fmt("floor area {0} m^2 ({1} storeys) exceeds plot-ratio limit {2} m^2 (FAR {3})",
                    grossFloor, (float)storeys, farLimit, z.maxPlotRatio);
                return v;
            }

            // ----- position: plan-centre set back from the frontage by front setback + half depth -----
            float halfW = req.prefab.footprintSize.x * 0.5f;
            float halfD = req.prefab.footprintSize.y * 0.5f;
            Vector2 anchor = mid + inward * (z.frontSetbackMetres + halfD);

            // ----- inside its lot, with side-setback clearance to the boundary -----
            Vector2[] corners =
            {
                anchor + across * halfW + outward * halfD,
                anchor - across * halfW + outward * halfD,
                anchor + across * halfW - outward * halfD,
                anchor - across * halfW - outward * halfD,
            };
            for (int i = 0; i < corners.Length; i++)
            {
                if (!Geometry2D.Contains(req.lot.polygon, corners[i]))
                {
                    v.reason = "footprint extends beyond the lot boundary (a corner falls outside the lot)";
                    return v;
                }
            }
            // The front setback governs the frontage line; this covers the remaining sides. It is exact
            // whenever front setback >= side setback (every ZoneCatalog zone), and conservatively refuses
            // otherwise -- a safe refuse-and-report, never a wrong placement.
            float minClear = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                float d = Geometry2D.DistancePointToPolygonBoundary(req.lot.polygon, corners[i]);
                if (d < minClear) { minClear = d; }
            }
            if (minClear + 1e-3f < z.sideSetbackMetres)
            {
                v.reason = Fmt("side setback {0} m short of the {1} m required (nearest lot line)",
                    minClear, z.sideSetbackMetres);
                return v;
            }

            // ----- it fits: freeze the transform. Base Y = pad for every ground-contact mode. -----
            Vector3 forward = new Vector3(outward.x, 0f, outward.y);
            BuildingInstance b;
            b.lotId = req.lot.id;
            b.prefabCategory = req.prefab.prefabCategory;
            b.variantIndex = req.prefab.variantIndex;
            b.position = new Vector3(anchor.x, req.padLevel, anchor.y);
            b.rotation = Quaternion.LookRotation(forward, Vector3.up);
            b.scale = Vector3.one;

            v.instance = b;
            v.outcome = PlacementOutcome.Placed;
            v.reason = Fmt("placed: footprint {0} m^2 on {1} m^2 lot, {2} storeys, base at {3} m",
                footArea, lotArea, (float)storeys, req.padLevel);
            return v;
        }

        /// <summary> Report-line formatter: invariant culture so a comma-decimal machine writes the same
        /// reason as a dot-decimal one, and two decimals so a number reads cleanly in the log. </summary>
        private static string Fmt(string format, params float[] values)
        {
            object[] boxed = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                boxed[i] = values[i].ToString("0.##", CultureInfo.InvariantCulture);
            }
            return string.Format(CultureInfo.InvariantCulture, format, boxed);
        }
    }
}

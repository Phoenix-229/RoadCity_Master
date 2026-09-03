#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How a prefab meets the ground once its pad exists. Chosen per prefab (RM-12 reads it
    /// from the § 10.14 category set), NOT by RM-22 -- the pad conform only RECORDS which mode was
    /// chosen so the solver and the placer agree. Defined here because RM-22 is the first task to name
    /// it; RM-12 and the category sets reference this same enum rather than declaring a second. </summary>
    public enum GroundContactMode
    {
        /// <summary> The prefab sits flat on the pad elevation -- the ordinary building case. </summary>
        OnPad,

        /// <summary> The prefab skirts down to natural terrain around its base -- a building seated by
        /// the apron alone on near-flat ground, with no pad cut. </summary>
        SkirtToTerrain,

        /// <summary> The prefab stands on piles/stilts above terrain; the pad marks the reference plane
        /// but ground is not brought up to it. </summary>
        Piled
    }


    /// <summary> Which rule sets a pad's elevation. AUTHORED per prefab (RM-12 passes it in); the pad
    /// conform never guesses which one applies. </summary>
    public enum PadElevationRule
    {
        /// <summary> Height sampled once at the footprint's AREA centroid -- the default, because an
        /// average of the corners biases an L-shaped or stepped footprint toward the wing that carries
        /// more vertices. </summary>
        CentroidSample,

        /// <summary> min(corner heights) + freeboard -- what a real graded site does so water sheds away
        /// from the structure rather than into it. </summary>
        MinCornerPlusFreeboard
    }


    /// <summary> The apron's blend shape from pad elevation out to natural terrain. AUTHORED, exactly
    /// like RM-31's median width. Both curves are MONOTONIC on [0,1]: a curve that overshot would put a
    /// visible lip or a trench ring around every building in the world. </summary>
    public enum PadBlendCurve
    {
        /// <summary> Smoothstep 3t^2 - 2t^3 -- the default; zero slope at both ends, so the pad meets the
        /// apron and the apron meets terrain without a crease. </summary>
        Smoothstep,

        /// <summary> A straight ramp t -- honest and cheap where a crease at the ends is acceptable. </summary>
        Linear
    }


    /// <summary> How a pad GROUP's single shared elevation is derived from its members. The RM-22 spec
    /// requires that overlapping pads "share a single elevation" but does NOT state which statistic
    /// picks it, and Q-12 does not cover it -- so, per the AUTHORED-not-invented rule (the
    /// BuildabilityCriteria precedent), it is an exposed policy with a documented default rather than a
    /// hardcoded formula. </summary>
    public enum PadGroupElevationPolicy
    {
        /// <summary> Footprint-area-weighted mean of member elevations -- the default; a larger building
        /// dominates the shared level, which reads naturally on a terraced street. </summary>
        AreaWeightedMean,

        /// <summary> The highest member elevation -- every member is cut down to it, nothing is buried. </summary>
        Highest,

        /// <summary> The lowest member elevation -- every member is filled up to it, nothing floats. </summary>
        Lowest,

        /// <summary> The plain mean of member elevations. </summary>
        Mean
    }


    /// <summary> RM-22's AUTHORED tuning: the numbers the pad conform needs that the geometric-design
    /// standard (Q-12) still owes. Marked AUTHORED, NOT GENERATED and exposed with a sensible
    /// <see cref="Default"/> -- the <c>BuildabilityCriteria</c> precedent -- rather than hardcoded as if
    /// they were real, so when Q-12 lands the true figures replace these in one place. </summary>
    [Serializable]
    public struct PadConformParams
    {
        /// <summary> How far, in metres, the apron extends outward from the footprint edge before height
        /// has fully returned to natural terrain. AUTHORED -- Q-12 owes the real figure. </summary>
        public float apronWidthMetres;

        /// <summary> The freeboard added above min(corner heights) for the
        /// <see cref="PadElevationRule.MinCornerPlusFreeboard"/> rule, in metres. AUTHORED -- Q-12 owes
        /// the real figure. </summary>
        public float freeboardMetres;

        /// <summary> The apron blend shape. </summary>
        public PadBlendCurve blendCurve;

        /// <summary> How a merged pad group's single shared elevation is chosen. </summary>
        public PadGroupElevationPolicy groupElevationPolicy;

        /// <summary> Standing defaults, ALL AUTHORED placeholders pending Q-12: a 4 m apron, 0.3 m of
        /// freeboard, a smoothstep blend, and an area-weighted group elevation. </summary>
        public static PadConformParams Default()
        {
            PadConformParams p;
            p.apronWidthMetres = 4f;
            p.freeboardMetres = 0.3f;
            p.blendCurve = PadBlendCurve.Smoothstep;
            p.groupElevationPolicy = PadGroupElevationPolicy.AreaWeightedMean;
            return p;
        }
    }


    /// <summary> One lot's request for a pad: the footprint polygon (world-space; only X/Z are read --
    /// the conform samples its own heights), the per-prefab elevation rule and ground-contact mode, and
    /// the stable lot id that fixes deterministic processing order (C-06). </summary>
    public struct PadRequest
    {
        public int lotId;
        public List<Vector3> footprint;
        public PadElevationRule elevationRule;
        public GroundContactMode groundContactMode;

        public PadRequest(int lotId, List<Vector3> footprint,
            PadElevationRule elevationRule, GroundContactMode groundContactMode)
        {
            this.lotId = lotId;
            this.footprint = footprint;
            this.elevationRule = elevationRule;
            this.groundContactMode = groundContactMode;
        }
    }


    /// <summary> RM-22's output for one lot: a REQUEST to § 4's conform solver (via RM-14), never a
    /// terrain edit. It carries the flat pad elevation, the footprint it flattens, the apron ring the
    /// blend runs out to, the blend curve, and the ground-contact mode the prefab chose. When several
    /// lots' aprons overlap they form a pad GROUP: every member then carries the SAME
    /// <see cref="padGroupId"/> and the SAME <see cref="padElevation"/>, which is what makes the result
    /// independent of bake order (C-06). </summary>
    public sealed class PadRecord
    {
        /// <summary> The lot this pad belongs to (the request's stable id). </summary>
        public readonly int lotId;

        /// <summary> The group this pad was merged into: the smallest lot id among the members whose
        /// aprons overlap, so it is stable across bakes. A lone pad's group is its own lot id. </summary>
        public int padGroupId;

        /// <summary> The single flat elevation, in metres, the footprint is brought to. Shared across a
        /// group. </summary>
        public float padElevation;

        /// <summary> The footprint polygon the pad flattens (world X/Z; Y carries the pad elevation). </summary>
        public readonly List<Vector3> footprint;

        /// <summary> The outer apron boundary: the footprint offset outward by the apron width, each
        /// vertex carrying NATURAL terrain height. Height blends from <see cref="padElevation"/> at the
        /// footprint edge to natural terrain at this ring. A group's members keep their own rings -- the
        /// single merged outer ring is a boolean union, deferred to the Q-11 polygon-union decision --
        /// because the shared elevation alone is what secures C-06. </summary>
        public readonly List<Vector3> apronRing;

        /// <summary> The elevation rule that produced this pad's elevation before any grouping. </summary>
        public readonly PadElevationRule elevationRule;

        /// <summary> The blend shape the solver applies across the apron. </summary>
        public readonly PadBlendCurve blendCurve;

        /// <summary> The ground-contact mode the prefab chose (recorded here, not decided here). </summary>
        public readonly GroundContactMode groundContactMode;

        public PadRecord(int lotId, float padElevation, List<Vector3> footprint, List<Vector3> apronRing,
            PadElevationRule elevationRule, PadBlendCurve blendCurve, GroundContactMode groundContactMode)
        {
            this.lotId = lotId;
            this.padGroupId = lotId;
            this.padElevation = padElevation;
            this.footprint = footprint;
            this.apronRing = apronRing;
            this.elevationRule = elevationRule;
            this.blendCurve = blendCurve;
            this.groundContactMode = groundContactMode;
        }
    }
}

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.

namespace RoadCityMaster.City
{
    using System;
    using UnityEngine;

    /// <summary> B-27 / D-13. The author-facing component for a dam, placed as a child of a road
    /// or directly in the scene hierarchy on a river. The inspector fields are the dam's authored
    /// dimensions; the computed DamRequest is what the linker carries to Universe Creator.
    ///
    /// The body is either a prefab the author provides or a generated gravity-dam shape keyed
    /// into the sampled abutments.
    ///
    /// REAL-WORLD GRAVITY DAM ENGINEERING:
    /// ------------------------------------
    /// A gravity dam resists the horizontal hydrostatic force of the reservoir entirely through
    /// its own self-weight. The trapezoidal cross-section provides stability against both
    /// overturning (moment about the toe) and sliding (shear along the foundation contact).
    ///
    /// The upstream face is near-vertical (slope 0.05) to maximise the reservoir volume.
    /// The downstream face is battered at 0.75:1 (run:rise), producing a base width that
    /// satisfies the classical stability inequality:
    ///     W·(B/3) ≥ γw·H²/6   (overturning about the downstream toe)
    /// where W = weight of the dam, B = base width, γw = unit weight of water, H = head.
    ///
    /// The crest must be wide enough for a two-lane road (minimum 6 m for inspection vehicles)
    /// and long enough to span the full valley cross-section between abutments.
    ///
    /// POWER GENERATION INFRASTRUCTURE:
    /// ---------------------------------
    /// Real hydroelectric dams include a powerhouse building at the downstream toe, housing
    /// Francis or Kaplan turbines connected to generators. Water reaches the turbines through
    /// penstocks (large-diameter steel pressure pipes) embedded in or running down the
    /// downstream face. The powerhouse connects to a substation switchyard with step-up
    /// transformers and lattice transmission towers carrying high-voltage lines to the grid.
    ///
    /// INSPECTION ACCESS:
    /// -------------------
    /// Gravity dams contain internal drainage and inspection galleries — horizontal tunnels
    /// running the length of the dam at one or more elevations. Engineers access these through
    /// steel doors in the downstream face or from the powerhouse. The galleries allow
    /// monitoring of seepage, uplift pressures, and structural movement via installed instruments.
    ///
    /// MONOLITHIC CONSTRUCTION:
    /// -------------------------
    /// Real concrete gravity dams are poured in discrete monolith blocks (typically 15 m wide)
    /// separated by contraction joints. These joints accommodate thermal contraction during
    /// concrete curing and are later grouted to create a watertight barrier. Each monolith
    /// is structurally independent until grouting, which is why the generated mesh partitions
    /// the dam body into visible block segments.
    /// </summary>
    [ExecuteAlways]
    public sealed class DamAuthoring : MonoBehaviour
    {
        // ====================================================================
        // DAM GEOMETRY — matches DamProfile field naming from DamWorks.cs
        // ====================================================================

        [Header("Dam Geometry")]

        /// <summary> Crest level above sea level, in metres. </summary>
        [Tooltip("Crest level above sea level (m). The top of the structure.")]
        public float crestLevelMetres = 100f;

        /// <summary> Crest length (along the dam axis), in metres. </summary>
        [Tooltip("Crest length along the dam axis (m). Must span the full valley.")]
        public float crestLengthMetres = 200f;

        /// <summary> Crest width (the top surface), in metres. Real gravity dams: 3–10 m. </summary>
        [Tooltip("Crest width (m). Must be wide enough for a two-lane road (≥6 m).")]
        public float crestWidthMetres = 6f;

        /// <summary> Upstream face slope as run-per-rise. Near-vertical for a gravity dam: 0.05. </summary>
        [Tooltip("Upstream face slope (H:V). Near-vertical: 0.05. Vertical: 0.")]
        public float upstreamFaceSlope = 0.05f;

        /// <summary> Downstream face slope as run-per-rise. 0.75 is standard gravity dam. </summary>
        [Tooltip("Downstream face slope (H:V). Standard gravity dam: 0.75.")]
        public float downstreamFaceSlope = 0.75f;

        /// <summary> Freeboard: how far the crest stands above the reservoir surface, in metres.
        /// Floor: 1.5 m (small-dam design practice). </summary>
        [Tooltip("Freeboard above reservoir surface (m). Minimum 1.5 m per dam safety codes.")]
        public float freeboardMetres = 1.5f;

        /// <summary> Width of each concrete pour monolith between contraction joints (m).
        /// Typical value: 15 m. Controls the number of visible block segments. </summary>
        [Tooltip("Monolith block width (m). Typical: 15 m. Controls contraction joint spacing.")]
        public float monolithWidthMetres = 15f;

        // ====================================================================
        // SPILLWAY — uses SpillwayType enum from DamWorks.cs
        // ====================================================================

        [Header("Spillway")]

        /// <summary> Spillway type. Enum values defined in DamWorks.cs. </summary>
        public SpillwayType spillwayType = SpillwayType.OgeeOverflow;

        /// <summary> Spillway crest level (must be below dam crest minus freeboard). </summary>
        [Tooltip("Spillway crest level (m). Must be below (crestLevel - freeboard).")]
        public float spillwayCrestMetres = 98f;

        /// <summary> Spillway width, in metres. </summary>
        [Tooltip("Spillway width (m). Governs flood discharge capacity.")]
        public float spillwayWidthMetres = 30f;

        // ====================================================================
        // OUTLET WORKS
        // ====================================================================

        [Header("Outlet Works")]

        /// <summary> Low-level outlet invert level, in metres. </summary>
        [Tooltip("Low-level outlet invert elevation (m). For controlled drawdown.")]
        public float outletInvertMetres = 70f;

        /// <summary> Outlet cross-section area, in m². </summary>
        [Tooltip("Outlet cross-section area (m²). Controls drawdown flow rate.")]
        public float outletAreaSqMetres = 4f;

        // ====================================================================
        // POWERHOUSE & ELECTRICAL INFRASTRUCTURE
        // ====================================================================

        [Header("Powerhouse")]

        /// <summary> Whether this dam includes a hydroelectric powerstation. </summary>
        [Tooltip("Enable to generate a powerhouse building at the downstream toe.")]
        public bool hasPowerhouse = true;

        /// <summary> Powerhouse building width along the dam axis (m). </summary>
        [Tooltip("Powerhouse width along the dam axis (m). Must fit all turbine bays.")]
        public float powerhouseWidthMetres = 80f;

        /// <summary> Powerhouse building height from foundation to roof (m). </summary>
        [Tooltip("Powerhouse height (m). Must clear the generator housings.")]
        public float powerhouseHeightMetres = 25f;

        /// <summary> Powerhouse building depth perpendicular to the dam axis (m). </summary>
        [Tooltip("Powerhouse depth perpendicular to dam (m). Houses turbines + draft tubes.")]
        public float powerhouseDepthMetres = 30f;

        /// <summary> Number of Francis turbine units. Each occupies one bay in the powerhouse. </summary>
        [Tooltip("Number of Francis turbine units. Each needs one penstock and one generator.")]
        public int turbineCount = 4;

        /// <summary> Penstock inner radius (m). Real-world: 1.5–5 m depending on head and flow. </summary>
        [Tooltip("Penstock inner radius (m). Typical: 1.5–5 m. Sized for design flow velocity ≤5 m/s.")]
        public float penstockRadiusMetres = 2.5f;

        [Header("Electrical Grid")]

        /// <summary> Whether to generate the outdoor substation switchyard. </summary>
        [Tooltip("Enable to generate transformer pads and transmission towers.")]
        public bool includeSwitchyard = true;

        /// <summary> Number of step-up transformer units in the switchyard. </summary>
        [Tooltip("Number of step-up transformers. Typically one per turbine unit.")]
        public int transformerCount = 4;

        /// <summary> Number of lattice steel transmission towers carrying HV lines from the substation. </summary>
        [Tooltip("Number of outgoing transmission towers (typically 2–6).")]
        public int transmissionTowerCount = 3;

        // ====================================================================
        // INTERNAL INSPECTION ACCESS
        // ====================================================================

        [Header("Inspection Galleries")]

        /// <summary> Whether to generate internal walk-in inspection galleries with doors. </summary>
        [Tooltip("Enable to carve hollow inspection tunnels inside the dam body.")]
        public bool includeInspectionGalleries = true;

        /// <summary> Gallery passage width (m). Real-world: 2.5–4 m. </summary>
        [Tooltip("Gallery passage width (m). Must allow equipment trolleys. Typical: 3 m.")]
        public float galleryWidthMetres = 3f;

        /// <summary> Gallery passage height (m). Real-world: 3–5 m. </summary>
        [Tooltip("Gallery passage height (m). Must clear standing height + instruments. Typical: 4 m.")]
        public float galleryHeightMetres = 4f;

        // ====================================================================
        // APPEARANCE — Material slots (author fills, never invented in code)
        // ====================================================================

        [Header("Appearance")]

        /// <summary> Optional author-provided dam body prefab. When non-null, replaces the
        /// generated gravity-dam mesh. </summary>
        public GameObject bodyPrefab;

        /// <summary> Material for the generated dam body (concrete faces). </summary>
        public Material bodyMaterial;

        /// <summary> Material for the crest road surface. </summary>
        public Material crestMaterial;

        /// <summary> Material for the powerhouse building exterior. </summary>
        public Material powerhouseMaterial;

        /// <summary> Material for turbines, penstocks, and mechanical equipment. </summary>
        public Material turbineMaterial;

        /// <summary> Material for inspection gallery interiors (inverted normals). </summary>
        public Material galleryMaterial;

        /// <summary> Material for metal structures (towers, transformers, catwalks). </summary>
        public Material metalGridMaterial;

        /// <summary> Material for the spillway chute surface. </summary>
        public Material spillwayMaterial;

        /// <summary> Downstream water depth (river depth below the dam). </summary>
        [Tooltip("Downstream water depth below the dam (m). Used for tailwater level.")]
        public float downstreamWaterDepthMetres = 5f;

        // ====================================================================
        // COMPUTED STATE (read-only, set by inspector/builder)
        // ====================================================================

        [HideInInspector] public DamRequest lastRequest;
        [HideInInspector] public bool hasBuiltMeshes;

        // ====================================================================
        // ENGINEERING CALCULATIONS — REAL-WORLD GRAVITY DAM PHYSICS
        // ====================================================================

        /// <summary>
        /// Computes the structural height of the dam from the crest level to the
        /// assumed valley floor. Uses a simplified estimate; the real height comes
        /// from DamWorks.SampleValley which reads the actual terrain.
        /// </summary>
        /// <returns>Estimated structural height in metres.</returns>
        public float EstimateStructuralHeight()
        {
            return crestLevelMetres - outletInvertMetres + 10f;
        }

        /// <summary>
        /// Computes the base width of the dam at foundation level using the
        /// classical gravity dam stability formula.
        ///
        /// Base width = crest width + H × (upstream slope + downstream slope)
        ///
        /// For a 100 m dam with slopes 0.05 and 0.75:
        ///   B = 6 + 100 × (0.05 + 0.75) = 6 + 80 = 86 m
        /// </summary>
        /// <param name="structuralHeight">Height from foundation to crest (m).</param>
        /// <returns>Base width in metres.</returns>
        public float CalculateBaseWidth(float structuralHeight)
        {
            return crestWidthMetres + structuralHeight * (upstreamFaceSlope + downstreamFaceSlope);
        }

        /// <summary>
        /// Calculates the concrete volume of the dam body for mass/weight estimation.
        /// Uses the trapezoidal cross-section area × crest length.
        ///
        /// Volume = ((crestWidth + baseWidth) / 2) × height × length
        /// </summary>
        /// <param name="structuralHeight">Height from foundation to crest (m).</param>
        /// <returns>Concrete volume in cubic metres.</returns>
        public float CalculateConcreteVolume(float structuralHeight)
        {
            float baseWidth = CalculateBaseWidth(structuralHeight);
            float crossSection = ((crestWidthMetres + baseWidth) / 2f) * structuralHeight;
            return crossSection * crestLengthMetres;
        }

        /// <summary>
        /// Estimates the dam's self-weight using standard reinforced concrete density
        /// of 2400 kg/m³ (per USBR Design of Small Dams, Table 8-1).
        /// </summary>
        /// <param name="structuralHeight">Height from foundation to crest (m).</param>
        /// <returns>Mass in kilograms.</returns>
        public float CalculateConcreteMass(float structuralHeight)
        {
            return CalculateConcreteVolume(structuralHeight) * 2400f;
        }

        /// <summary>
        /// Computes the factor of safety against sliding using the Mohr-Coulomb criterion:
        ///
        ///   FoS = (W·tan(φ) + c·B·L) / Fh
        ///
        /// where:
        ///   W   = self-weight of the dam (N)
        ///   φ   = friction angle of the foundation rock–concrete interface (degrees)
        ///   c   = cohesion at the foundation contact (Pa)
        ///   B   = base width (m)
        ///   L   = crest length (m)
        ///   Fh  = resultant horizontal hydrostatic force = γw·H²/2 per unit length × L
        ///   γw  = unit weight of water = 9810 N/m³
        ///
        /// Minimum acceptable FoS for gravity dams: 1.5 (USBR) to 2.0 (ICOLD).
        /// </summary>
        /// <param name="structuralHeight">Height from foundation to crest (m).</param>
        /// <param name="frictionAngleDeg">Foundation friction angle (degrees). Typical rock: 45°.</param>
        /// <param name="cohesionPa">Foundation cohesion (Pa). Typical: 500 kPa = 500000 Pa.</param>
        /// <returns>Factor of safety against sliding (dimensionless).</returns>
        public float CalculateSlidingSafetyFactor(float structuralHeight, float frictionAngleDeg, float cohesionPa)
        {
            float weight = CalculateConcreteMass(structuralHeight) * 9.81f;
            float baseWidth = CalculateBaseWidth(structuralHeight);
            float frictionRad = frictionAngleDeg * Mathf.Deg2Rad;
            float resistingForce = (weight * Mathf.Tan(frictionRad)) + (cohesionPa * baseWidth * crestLengthMetres);

            // Hydrostatic force on the upstream face (triangular pressure distribution)
            float gammaW = 9810f; // N/m³
            float hydrostaticForce = gammaW * structuralHeight * structuralHeight / 2f * crestLengthMetres;

            if (hydrostaticForce <= 0f) return float.MaxValue;
            return resistingForce / hydrostaticForce;
        }

        /// <summary>
        /// Computes the factor of safety against overturning about the downstream toe.
        ///
        ///   FoS = Stabilising moment / Overturning moment
        ///
        /// Stabilising moment = W × (distance from toe to centroid of trapezoid)
        /// Overturning moment = Fh × H/3  (hydrostatic resultant acts at H/3 above base)
        ///
        /// Minimum acceptable: 1.5 (USBR).
        /// </summary>
        /// <param name="structuralHeight">Height from foundation to crest (m).</param>
        /// <returns>Factor of safety against overturning (dimensionless).</returns>
        public float CalculateOverturningSafetyFactor(float structuralHeight)
        {
            float baseWidth = CalculateBaseWidth(structuralHeight);
            float volume = CalculateConcreteVolume(structuralHeight);
            float weight = volume * 2400f * 9.81f; // Total weight in Newtons

            // Centroid of the trapezoidal cross-section from the downstream toe
            // For a trapezoid with parallel sides a (crest) and b (base), centroid from the wider side:
            //   x_centroid = b/3 × (2a + b) / (a + b)  — but measured from downstream toe
            float a = crestWidthMetres;
            float b = baseWidth;
            float downstreamRun = downstreamFaceSlope * structuralHeight;
            float centroidFromToe = (b - downstreamRun) + (b * (2f * a + b)) / (3f * (a + b));
            // Simplified: use b/3 as approximation for massive gravity dam
            float stabilising = weight * (baseWidth / 3f);

            // Overturning moment from hydrostatic pressure
            float gammaW = 9810f;
            float Fh = gammaW * structuralHeight * structuralHeight / 2f * crestLengthMetres;
            float overturning = Fh * (structuralHeight / 3f);

            if (overturning <= 0f) return float.MaxValue;
            return stabilising / overturning;
        }

        /// <summary>
        /// Calculates spillway discharge using the standard weir equation:
        ///   Q = Cd × L × H^1.5
        /// where Cd depends on SpillwayType (Ogee: 2.2, Chute: 2.1, BroadCrested: 1.7).
        /// </summary>
        /// <param name="headOverSpillway">Head of water above the spillway crest (m).</param>
        /// <returns>Discharge in m³/s.</returns>
        public float CalculateSpillwayDischarge(float headOverSpillway)
        {
            float cd;
            switch (spillwayType)
            {
                case SpillwayType.OgeeOverflow: cd = 2.2f; break;
                case SpillwayType.Chute:        cd = 2.1f; break;
                case SpillwayType.BroadCrested:  cd = 1.7f; break;
                default:                         cd = 2.0f; break;
            }
            if (headOverSpillway <= 0f) return 0f;
            return cd * spillwayWidthMetres * Mathf.Pow(headOverSpillway, 1.5f);
        }

        /// <summary>
        /// Calculates outlet discharge using the orifice equation:
        ///   Q = Cd × A × √(2gH)
        /// where Cd = 0.6 (standard sharp-edged orifice coefficient).
        /// </summary>
        /// <param name="headAboveOutlet">Head of water above the outlet centreline (m).</param>
        /// <returns>Discharge in m³/s.</returns>
        public float CalculateOutletDischarge(float headAboveOutlet)
        {
            const float cd = 0.6f;
            if (headAboveOutlet <= 0f) return 0f;
            return cd * outletAreaSqMetres * Mathf.Sqrt(2f * 9.81f * headAboveOutlet);
        }
    }
}
namespace RoadCityMaster.City.Runtime.Network
{
    using System;
    using UnityEngine;

    /// <summary>
    /// B-27 / D-13: Advanced Real-World Dam Authoring Component.
    /// 
    /// THIS SCRIPT IS PART OF THE ADVANCED SIMULATION TIER.
    /// 
    /// It provides high-fidelity, real-world parameterized structural authoring 
    /// for monolithic gravity dams, power generating infrastructure, internal 
    /// hollow engineering galleries, and complex high-grid connections.
    /// 
    /// This component completely foregoes prefabs. Instead, it computes and passes
    /// precise geometrical limits to the DamBodyMeshes generator which sculpts
    /// colliders and meshes to the exact specifications required by Universe Creator.
    /// </summary>
    [ExecuteAlways]
    public class DamAuthoring : MonoBehaviour
    {
        [Header("Global Topography & Siting")]
        [Tooltip("The total length of the dam crest across the valley (m). Must intersect canyon abutments.")]
        public float crestLength = 150f;

        [Tooltip("The width of the dam crest in meters. Real-world dams feature crests wide enough for two-lane roads (8m - 12m).")]
        public float crestWidth = 12f;

        [Tooltip("Total vertical height of the dam from foundation bedrock to crest (m).")]
        public float damHeight = 80f;

        [Header("Advanced Gravity Dam Profiles")]
        [Tooltip("Upstream face slope (H:V). Typically near vertical (0.0 - 0.1) to use water weight for stability.")]
        [Range(0f, 0.3f)]
        public float upstreamSlope = 0.05f;

        [Tooltip("Downstream face slope (H:V). Typically 0.7 to 0.85 to provide sufficient base width against overturning moments.")]
        [Range(0.5f, 1.5f)]
        public float downstreamSlope = 0.8f;

        [Tooltip("The width of each concrete monolith block (m). Dams are built in sections separated by contraction joints.")]
        [Range(10f, 30f)]
        public float monolithWidth = 15f;

        [Header("Power Generation Complex")]
        [Tooltip("If true, a comprehensive powerhouse is generated at the downstream toe, complete with high grid substations.")]
        public bool hasPowerhouse = true;

        [Tooltip("Width of the powerhouse structure (m).")]
        public float powerhouseWidth = 60f;

        [Tooltip("Height of the powerhouse structure (m).")]
        public float powerhouseHeight = 25f;

        [Tooltip("Depth of the powerhouse structure (m).")]
        public float powerhouseDepth = 30f;

        [Tooltip("Number of hydroelectric turbine units housed inside the plant.")]
        [Range(1, 12)]
        public int turbineCount = 6;

        [Tooltip("Radius of the penstocks carrying water to the turbines.")]
        [Range(1f, 5f)]
        public float penstockRadius = 3f;

        [Header("Engineering Galleries & Interior Space")]
        [Tooltip("If true, the mesh generator will carve out physical hollow galleries inside the dam block with walk-in colliders.")]
        public bool includeWalkableGalleries = true;

        [Tooltip("Width of internal galleries and access doors (m).")]
        public float galleryWidth = 3f;

        [Tooltip("Height of internal galleries and access doors (m).")]
        public float galleryHeight = 4f;

        [Header("Electrical Grid Infrastructure")]
        [Tooltip("If true, generates the high-voltage substation switchyard next to the powerhouse.")]
        public bool includeSwitchyard = true;

        [Tooltip("Number of transformer units to generate outside the powerhouse.")]
        [Range(1, 10)]
        public int transformerCount = 4;

        [Tooltip("Number of high-voltage transmission towers mounted on the powerhouse roof.")]
        [Range(1, 6)]
        public int transmissionTowerCount = 2;

        [Header("Hydraulics & Spillway")]
        [Tooltip("The percentage of the dam crest length dedicated to the spillway chute.")]
        [Range(0.1f, 0.5f)]
        public float spillwayWidthPercentage = 0.2f;

        [Tooltip("Depth of the spillway radial gates below the crest (m).")]
        public float spillwayGateDepth = 5f;

        [Header("Procedural Materials")]
        public Material bodyMaterial;
        public Material powerhouseMaterial;
        public Material roofMaterial;
        public Material spillwayMaterial;
        public Material turbineMaterial;
        public Material galleryMaterial;
        public Material metalGridMaterial;

        [HideInInspector]
        public bool hasBuiltMeshes = false;

        /// <summary>
        /// Calculates the required base width of the dam to maintain structural stability.
        /// </summary>
        public float CalculateBaseWidth()
        {
            return crestWidth + (upstreamSlope * damHeight) + (downstreamSlope * damHeight);
        }

        /// <summary>
        /// Evaluates the factor of safety against overturning.
        /// Uses advanced volumetric physics to simulate the real-world stability of the monoliths.
        /// </summary>
        public float CalculateFactorOfSafetyOverturning()
        {
            float concreteDensity = 2400f; // kg/m^3
            float waterDensity = 1000f; // kg/m^3
            float g = 9.81f;

            float waterForce = 0.5f * waterDensity * g * damHeight * damHeight;
            float overturningMoment = waterForce * (damHeight / 3f);

            float baseW = CalculateBaseWidth();
            float crossArea = ((crestWidth + baseW) / 2f) * damHeight;
            float weight = crossArea * concreteDensity * g;
            
            float restoringMoment = weight * (baseW * 0.5f);

            if (overturningMoment == 0) return float.MaxValue;
            return restoringMoment / overturningMoment;
        }

        /// <summary>
        /// Evaluates the factor of safety against sliding.
        /// </summary>
        public float CalculateFactorOfSafetySliding()
        {
            float concreteDensity = 2400f; // kg/m^3
            float waterDensity = 1000f; // kg/m^3
            float g = 9.81f;
            float frictionAngle = 0.65f; // Coefficient of friction for rock

            float waterForce = 0.5f * waterDensity * g * damHeight * damHeight;
            float baseW = CalculateBaseWidth();
            float crossArea = ((crestWidth + baseW) / 2f) * damHeight;
            float weight = crossArea * concreteDensity * g;

            float slidingResistance = weight * frictionAngle;

            if (waterForce == 0) return float.MaxValue;
            return slidingResistance / waterForce;
        }
    }
}

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.

namespace RoadCityMaster.City
{
    using UnityEngine;

    /// <summary> B-27 / D-13. The author-facing component for a dam standing on a river: the dimensions an author
    /// draws, and the <see cref="DamRequest"/> those dimensions plus the real terrain resolve to.
    ///
    /// ONCE THE DAM HAS RESOLVED, this type reports nothing of its own: every figure comes from the resolved
    /// <see cref="DamRequest"/>/<see cref="DamStability"/> that <see cref="DamWorks"/> produced off the real terrain,
    /// which is D-13's resolver and was already written, already wired into <c>CityNodeDispatch</c>'s Dam node, and
    /// already gated. The "Engineering estimates" region below is read by the inspector and the mesh builder BEFORE
    /// that resolve has run -- when there is no terrain answer yet to show -- and it is bound by one rule: it may not
    /// hold a second copy of a physical constant. So it borrows <see cref="DamWorks"/>'s own coefficients and
    /// constants (<see cref="DamWorks.DischargeCoefficient"/>, <see cref="DamWorks.OrificeCoefficient"/>,
    /// <see cref="DamWorks.Gravity"/>) and builds the section centroid with the identical decomposition, so the
    /// estimate cannot drift away from the resolved figure the way an earlier revision did -- that revision had a
    /// different orifice coefficient (0.6 against the house 0.62), a different gravity (9.81 against 9.80665) and a
    /// structural height of <c>crestLevel - outletInvert + 10</c>, a guess with a magic ten metres in it standing in
    /// for the valley floor that <see cref="DamWorks.SampleValley"/> reads off the actual ground. Two values for one
    /// physical constant in one tool is a bug whichever one is right.
    ///
    /// So the rule for this file: it holds AUTHORED figures and material slots, it forwards the resolved answer once
    /// there is one, and the pre-resolution estimate it draws in the meantime reads its constants from
    /// <see cref="DamWorks"/> rather than restating them.
    ///
    /// WHERE THE DAM STANDS comes from the transform, not from fields. A placement duplicated into
    /// centre/heading fields is a placement that disagrees with the handle the author actually drags.
    /// <see cref="DamWorks"/> takes headings in the compass convention Unity's Y rotation already uses (0 = +Z,
    /// 90 = +X), so the two map onto each other directly:
    ///   * the crest runs along the object's local X, which is heading <c>eulerAngles.y + 90</c>;
    ///   * the reservoir stands on local -Z, which is heading <c>eulerAngles.y + 180</c>.
    /// Local +Z is therefore downstream, and that is the side the powerhouse and the spillway chute are built on.
    ///
    /// REAL-WORLD GRAVITY DAM ENGINEERING. A gravity dam resists the reservoir's horizontal push through its own
    /// self-weight alone. The upstream face is near-vertical (0.05 run per rise) to hold back the most water for the
    /// least excavation; the downstream face is battered around 0.75 run per rise, which is what makes the base wide
    /// enough for the weight to win the moment about the toe. Those two slopes and the crest width are the whole
    /// section, which is why they are the only shape figures authored here.
    ///
    /// POWER GENERATION. A hydroelectric dam carries a powerhouse at the downstream toe holding Francis or Kaplan
    /// turbines, fed through penstocks down the downstream face, discharging to the tailrace. The powerhouse feeds a
    /// switchyard of step-up transformers, and lattice towers carry the line away. All of that is a BUILDING the
    /// author can walk into, not a decorative block -- see <c>DamBodyMeshes</c>, which raises it.
    ///
    /// INSPECTION ACCESS. Gravity dams are hollow in one specific way: a drainage and inspection gallery runs the
    /// length of the section, reached through steel doors, so engineers can read seepage and uplift instruments. The
    /// gallery is the reason <see cref="DamWorks.Stability"/> can be run with drained assumptions at all, and it is
    /// carved as real void with real walls, not implied by a box with its normals flipped.
    ///
    /// MONOLITHIC CONSTRUCTION. Concrete gravity dams are poured in discrete monoliths, typically 15 m wide, split by
    /// contraction joints that take up thermal shrinkage during curing and are grouted afterwards. That is why the
    /// generated body reads as blocks rather than one extrusion. </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DamAuthoring : MonoBehaviour
    {
        #region "Dam geometry"

        [Header("Dam Geometry")]

        /// <summary> Top of the structure, as a world height in metres -- the same datum the terrain reader answers
        /// in. </summary>
        [Tooltip("Crest level above the world datum (m). The top of the structure.")]
        public float crestLevelMetres = 100f;

        /// <summary> Crest length along the dam axis (local X), in metres. Must reach both abutments; the valley
        /// section reports it when it does not. </summary>
        [Tooltip("Crest length along the dam axis (m). Must span the full valley between abutments.")]
        public float crestLengthMetres = 200f;

        /// <summary> Crest width front to back, in metres. Real gravity dams carry 3-10 m; the floor here is what a
        /// two-lane inspection road plus its footway needs. </summary>
        [Tooltip("Crest width (m). Must be wide enough for the road and footway it carries.")]
        public float crestWidthMetres = 9f;

        /// <summary> Upstream face slope as run per rise. Near-vertical on a gravity section: 0.05. </summary>
        [Tooltip("Upstream face slope (run per rise). Near-vertical: 0.05. Vertical: 0.")]
        public float upstreamFaceSlope = 0.05f;

        /// <summary> Downstream face slope as run per rise. 0.75 is the standard gravity batter. </summary>
        [Tooltip("Downstream face slope (run per rise). Standard gravity dam: 0.75.")]
        public float downstreamFaceSlope = 0.75f;

        /// <summary> The gap the crest keeps above the held reservoir, in metres. <see cref="DamWorks"/> refuses a
        /// body below its 1.5 m floor rather than building one with too little margin against wave run-up. </summary>
        [Tooltip("Freeboard above the reservoir surface (m). Refused below the 1.5 m floor.")]
        public float freeboardMetres = 1.5f;

        /// <summary> Width of each concrete pour between contraction joints, in metres. Typically 15 m. </summary>
        [Tooltip("Monolith block width (m). Typical: 15 m. Sets the contraction joint spacing.")]
        public float monolithWidthMetres = 15f;

        #endregion


        #region "Spillway and outlet"

        [Header("Spillway")]

        /// <summary> Which overflow form the crest carries. The discharge coefficient is the shape's measured
        /// performance, held once in <see cref="DamWorks.DischargeCoefficient"/>. </summary>
        public SpillwayType spillwayType = SpillwayType.OgeeOverflow;

        /// <summary> Spillway crest level in metres. Must sit below the held reservoir or it passes nothing, which
        /// the sizing reports as a refusal rather than a zero dressed up as an answer. </summary>
        [Tooltip("Spillway crest level (m). Must be below (crest level - freeboard) to pass any flow.")]
        public float spillwayCrestMetres = 98f;

        /// <summary> Spillway width along the crest, in metres. Governs flood discharge capacity. </summary>
        [Tooltip("Spillway width (m). Governs flood discharge capacity.")]
        public float spillwayWidthMetres = 30f;

        [Header("Outlet Works")]

        /// <summary> Low-level outlet invert level, in metres, for controlled drawdown. </summary>
        [Tooltip("Low-level outlet invert level (m). For controlled drawdown.")]
        public float outletInvertMetres = 70f;

        /// <summary> Outlet bore diameter in metres. Zero means the dam carries no low-level outlet.
        ///
        /// A DIAMETER, not an area: <see cref="DamWorks.SizeOutlet"/> squares it into an area itself, and a component
        /// that stored the area instead would be the only place in the tool where the same pipe is described two
        /// ways. </summary>
        [Tooltip("Outlet bore diameter (m). Zero for no low-level outlet.")]
        public float outletBoreDiameterMetres = 2.25f;

        #endregion


        #region "Powerhouse and electrical"

        [Header("Powerhouse")]

        /// <summary> Whether this dam carries a hydroelectric station at the downstream toe. </summary>
        [Tooltip("Enable to build a powerhouse building at the downstream toe.")]
        public bool hasPowerhouse = true;

        /// <summary> Powerhouse width along the dam axis, in metres. Must fit every turbine bay. </summary>
        [Tooltip("Powerhouse width along the dam axis (m). Must fit all turbine bays.")]
        public float powerhouseWidthMetres = 80f;

        /// <summary> Powerhouse height from its floor to the roof, in metres. Must clear the generator housings and
        /// the overhead travelling crane that lifts them. </summary>
        [Tooltip("Powerhouse height (m). Must clear the generator housings and the machine-hall crane.")]
        public float powerhouseHeightMetres = 25f;

        /// <summary> Powerhouse depth away from the dam face, in metres. </summary>
        [Tooltip("Powerhouse depth away from the dam (m). Houses the turbines and draft tubes.")]
        public float powerhouseDepthMetres = 30f;

        /// <summary> Number of turbine units. Each takes one bay and one penstock. </summary>
        [Tooltip("Number of turbine units. Each needs one penstock and one generator.")]
        public int turbineCount = 4;

        /// <summary> Penstock inner radius in metres. Real plant sizes the bore so the design flow travels at about
        /// 5 m/s; 1.5-5 m covers most heads. </summary>
        [Tooltip("Penstock inner radius (m). Typical 1.5-5 m, sized for a flow velocity near 5 m/s.")]
        public float penstockRadiusMetres = 2.5f;

        [Header("Electrical Grid")]

        /// <summary> Whether to build the outdoor substation switchyard. </summary>
        [Tooltip("Enable to build transformer pads and outgoing transmission towers.")]
        public bool includeSwitchyard = true;

        /// <summary> Step-up transformer units in the switchyard, typically one per turbine. </summary>
        [Tooltip("Number of step-up transformers. Typically one per turbine unit.")]
        public int transformerCount = 4;

        /// <summary> Lattice steel towers carrying the outgoing high-voltage lines. </summary>
        [Tooltip("Number of outgoing transmission towers (typically 2-6).")]
        public int transmissionTowerCount = 3;

        #endregion


        #region "Inspection access"

        [Header("Inspection Galleries")]

        /// <summary> Whether to carve the internal walk-in inspection gallery and its access doors. </summary>
        [Tooltip("Enable to carve a walk-in inspection gallery through the dam body.")]
        public bool includeInspectionGalleries = true;

        /// <summary> Gallery passage width in metres. Real galleries run 2.5-4 m so an equipment trolley fits.
        /// </summary>
        [Tooltip("Gallery passage width (m). Must pass an equipment trolley. Typical: 3 m.")]
        public float galleryWidthMetres = 3f;

        /// <summary> Gallery passage height in metres, to the crown of the arch. Real galleries run 3-5 m to clear
        /// standing height plus the instruments hung from the soffit. </summary>
        [Tooltip("Gallery passage height (m). Standing height plus instruments. Typical: 4 m.")]
        public float galleryHeightMetres = 4f;

        /// <summary> How far the gallery floor stands above the foundation, in metres. A gallery is set above the
        /// contact so the drains that feed it can fall into it. </summary>
        [Tooltip("Gallery floor height above the foundation (m). Drains must fall into it.")]
        public float galleryFloorAboveFoundationMetres = 8f;

        /// <summary> Clear width of the steel access door in the downstream face, in metres. </summary>
        [Tooltip("Clear width of the gallery access door (m).")]
        public float galleryDoorWidthMetres = 1.4f;

        /// <summary> Clear height of the steel access door in the downstream face, in metres. </summary>
        [Tooltip("Clear height of the gallery access door (m).")]
        public float galleryDoorHeightMetres = 2.1f;

        /// <summary> How far the door leaf stands open, in degrees, about its hinge. Zero reads as shut. The leaf is
        /// a real child object on a real pivot, so this is the angle it is actually turned to. </summary>
        [Range(0f, 120f)]
        [Tooltip("How far the door leaf stands open about its hinge (degrees). 0 is shut.")]
        public float galleryDoorOpenDegrees = 75f;

        #endregion


        #region "Crest deck"

        [Header("Crest Deck")]

        /// <summary> Carriageway width on the crest, in metres. A two-lane inspection road is 6 m; the crest must be
        /// wide enough for this plus both footways. </summary>
        [Tooltip("Crest carriageway width (m). Two lanes for inspection vehicles: 6 m.")]
        public float crestRoadWidthMetres = 6f;

        /// <summary> Width of the raised footway on each side of the carriageway, in metres. This is the pavement
        /// walked on to reach the gallery door and to stand over the spillway. </summary>
        [Tooltip("Raised footway width on each side of the carriageway (m).")]
        public float crestFootwayWidthMetres = 1.5f;

        /// <summary> How far the footway stands above the carriageway, in metres -- a real kerb upstand. </summary>
        [Tooltip("Kerb upstand: how far the footway stands above the carriageway (m).")]
        public float crestKerbHeightMetres = 0.15f;

        /// <summary> Parapet height above the footway, in metres. Guards the drop on both sides. </summary>
        [Tooltip("Parapet height above the footway (m). Guards the drop on both edges.")]
        public float crestParapetHeightMetres = 1.1f;

        #endregion


        #region "Terrain reading"

        [Header("Terrain Reading")]

        /// <summary> Downstream river depth at the toe, in metres. The tailwater level is the toe ground plus this,
        /// and the tailwater is what pushes back on the downstream face in the stability sum. </summary>
        [Tooltip("Downstream river depth at the toe (m). Sets the tailwater level.")]
        public float downstreamWaterDepthMetres = 5f;

        /// <summary> Flood-fill resolution in metres for the impounded reservoir. </summary>
        [Tooltip("Flood-fill cell size (m). Five metres resolves a reservoir without a million samples.")]
        public float floodCellSizeMetres = (float)DamWorks.DefaultCellSizeMetres;

        /// <summary> Cell budget for the flood fill. Hitting it is reported, never hidden. </summary>
        [Tooltip("Flood-fill cell budget. Reaching it is reported as an incomplete extent.")]
        public int maxFloodCells = DamWorks.DefaultMaxFloodCells;

        /// <summary> How many points the valley is read at across the crest line. </summary>
        [Tooltip("How many points the valley section is sampled at across the crest line.")]
        public int valleySamples = DamWorks.DefaultValleySamples;

        [Header("Foundation")]

        /// <summary> Friction angle of the concrete-on-rock foundation contact, in degrees. Sound rock: 45. </summary>
        [Tooltip("Foundation contact friction angle (degrees). Sound rock: 45.")]
        public float foundationFrictionAngleDeg = 45f;

        /// <summary> Cohesion at the foundation contact, in pascals. Left at zero by default because cohesion is the
        /// part of the shear strength one fissure destroys, and the codes ask for the check that does not count on
        /// it unless drilling has proven it. </summary>
        [Tooltip("Foundation contact cohesion (Pa). Zero is the check the codes actually ask for.")]
        public float foundationCohesionPascals;

        #endregion


        #region "Appearance"

        [Header("Appearance")]

        /// <summary> Optional author-provided body prefab. When set, it replaces the generated gravity section.
        /// </summary>
        public GameObject bodyPrefab;

        /// <summary> Mass concrete of the dam body. </summary>
        public Material bodyMaterial;

        /// <summary> Carriageway surface on the crest. </summary>
        public Material crestMaterial;

        /// <summary> Footway, kerb and parapet on the crest. </summary>
        public Material pavementMaterial;

        /// <summary> Powerhouse building exterior. </summary>
        public Material powerhouseMaterial;

        /// <summary> Turbines, penstocks and mechanical plant. </summary>
        public Material turbineMaterial;

        /// <summary> Gallery lining: the walls, floor and soffit of the carved passage. </summary>
        public Material galleryMaterial;

        /// <summary> The steel door leaf and its frame. </summary>
        public Material doorMaterial;

        /// <summary> Towers, transformers, handrails and catwalks. </summary>
        public Material metalGridMaterial;

        /// <summary> The spillway chute and its training walls. </summary>
        public Material spillwayMaterial;

        #endregion


        #region "Computed state"

        /// <summary> The last request resolved off the real terrain. Written by <see cref="Resolve"/>, never
        /// authored. </summary>
        [HideInInspector] public DamRequest lastRequest;

        /// <summary> The last stability check for that request. Written by <see cref="Resolve"/>. </summary>
        [HideInInspector] public DamStability lastStability;

        /// <summary> Whether generated child meshes are currently in the scene under this object. </summary>
        [HideInInspector] public bool hasBuiltMeshes;

        #endregion


        #region "Placement, in the resolver's own convention"

        /// <summary> Heading of the crest axis in degrees, in the compass convention <see cref="DamWorks"/> reads
        /// (0 = +Z, 90 = +X). The crest runs along the object's local X, which is a quarter turn from its forward.
        /// </summary>
        public double AxisHeadingDeg { get { return transform.eulerAngles.y + 90.0; } }

        /// <summary> Heading of the upstream side, where the reservoir stands: the object's local -Z. </summary>
        public double UpstreamHeadingDeg { get { return transform.eulerAngles.y + 180.0; } }

        #endregion


        #region "Resolution"

        /// <summary> Resolve the whole dam off the real terrain: the valley section, the body, the impounded
        /// reservoir, both outlets, the tailwater, and whether the section stands. Stores the answer in
        /// <see cref="lastRequest"/> and <see cref="lastStability"/> and returns whether it resolved.
        ///
        /// The ground arrives as an argument rather than being reached for, because this type is runtime and the
        /// gateway that owns the reader is a scene service the editor holds: a component that went looking for it
        /// would compute a different answer depending on what else happened to be loaded.
        ///
        /// Nothing here writes to the terrain or the water. Until Universe Creator answers the request, the dam is a
        /// structure standing in an unchanged river, and <see cref="DamRequest.Describe"/> says exactly that.
        /// </summary>
        public bool Resolve(ITerrainSurfaceReader ground, out string refusal)
        {
            if (ground == null)
            {
                refusal = "no terrain reader was supplied, so the valley under the dam cannot be read. Connect the "
                    + "terrain provider, or open the scene that carries it, and resolve again.";
                lastRequest = new DamRequest();
                lastStability = new DamStability();
                return false;
            }

            Vector3 at = transform.position;
            lastRequest = DamWorks.Resolve(ground, at.x, at.z, AxisHeadingDeg, UpstreamHeadingDeg,
                crestLevelMetres, freeboardMetres, crestLengthMetres, crestWidthMetres, upstreamFaceSlope,
                downstreamFaceSlope, spillwayType, spillwayCrestMetres, spillwayWidthMetres, outletInvertMetres,
                outletBoreDiameterMetres, downstreamWaterDepthMetres, floodCellSizeMetres, maxFloodCells,
                valleySamples);

            lastStability = DamWorks.Stability(lastRequest.profile, lastRequest.tailwaterLevelMetres,
                foundationFrictionAngleDeg, foundationCohesionPascals);

            refusal = lastRequest.resolved ? string.Empty : lastRequest.refusal;
            return lastRequest.resolved;
        }

        /// <summary> Whether the crest section the author drew is wide enough for what it is asked to carry: a
        /// carriageway, a footway on each side, and the kerb between them. Returned as a refusal string rather than a
        /// bool so the inspector can state the figure that does not fit instead of merely going red. </summary>
        public bool IsCrestSectionConsistent(out string reason)
        {
            float needed = crestRoadWidthMetres + 2f * crestFootwayWidthMetres;
            if (crestRoadWidthMetres <= 0f)
            {
                reason = "the crest carriageway has no width, so the road across the dam is not a road";
                return false;
            }
            if (crestFootwayWidthMetres < 0f)
            {
                reason = "the crest footway width is negative";
                return false;
            }
            if (needed > crestWidthMetres)
            {
                reason = "the crest deck needs " + needed.ToString("0.##") + " m for a "
                    + crestRoadWidthMetres.ToString("0.##") + " m carriageway between two "
                    + crestFootwayWidthMetres.ToString("0.##") + " m footways, but the crest is only "
                    + crestWidthMetres.ToString("0.##") + " m wide front to back";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> Whether the gallery the author asked for actually fits inside the section, and whether its door
        /// fits in the gallery. A passage taller than the wall it is cut through would carve the dam in half, and a
        /// door wider than the passage would open onto its own jamb. </summary>
        public bool IsGalleryConsistent(out string reason)
        {
            if (!includeInspectionGalleries) { reason = string.Empty; return true; }
            if (galleryWidthMetres <= 0f || galleryHeightMetres <= 0f)
            {
                reason = "the inspection gallery has no width or no height, so there is no passage to walk";
                return false;
            }
            if (galleryDoorWidthMetres <= 0f || galleryDoorHeightMetres <= 0f)
            {
                reason = "the gallery door has no clear width or height, so there is no way in";
                return false;
            }
            if (galleryDoorWidthMetres > galleryWidthMetres)
            {
                reason = "the gallery door is " + galleryDoorWidthMetres.ToString("0.##") + " m wide but the passage "
                    + "behind it is only " + galleryWidthMetres.ToString("0.##") + " m, so the leaf would open onto "
                    + "its own jamb";
                return false;
            }
            if (galleryDoorHeightMetres > galleryHeightMetres)
            {
                reason = "the gallery door is " + galleryDoorHeightMetres.ToString("0.##") + " m tall but the passage "
                    + "behind it is only " + galleryHeightMetres.ToString("0.##") + " m";
                return false;
            }
            if (galleryFloorAboveFoundationMetres < 0f)
            {
                reason = "the gallery floor is below the foundation contact";
                return false;
            }
            return IsCrestSectionConsistent(out reason);
        }

        #endregion


        #region "Engineering estimates (read by the inspector and the mesh builder)"

        // Mass concrete unit weight. 2400 kg/m^3 is the figure the gravity-section codes size to.
        private const float ConcreteDensityKgPerCubicMetre = 2400f;
        private const float WaterDensityKgPerCubicMetre = 1000f;

        /// <summary> Structural height crest-to-foundation, in metres. Once the dam has resolved off the terrain the
        /// height the section actually found is returned; before that it is estimated from the authored levels as the
        /// crest above the low-level outlet invert, the deepest control the author has placed. </summary>
        public float EstimateStructuralHeight()
        {
            if (lastRequest.resolved && lastRequest.profile.structuralHeightMetres > 0.0)
                return (float)lastRequest.profile.structuralHeightMetres;
            return Mathf.Max(0f, crestLevelMetres - outletInvertMetres);
        }

        /// <summary> Base width front-to-back at the foundation, in metres, for a structural height of
        /// <paramref name="H"/> -- the crest width plus the spread of the two faces down the height. This is the
        /// identical sum <see cref="DamWorks"/> builds the body's section to. </summary>
        public float CalculateBaseWidth(float H)
        {
            return crestWidthMetres + Mathf.Max(0f, H) * (upstreamFaceSlope + downstreamFaceSlope);
        }

        /// <summary> Concrete volume of the gravity section, in cubic metres, for a structural height of
        /// <paramref name="H"/>: the trapezoid between the crest width and the base width, run along the crest
        /// length. </summary>
        public float CalculateConcreteVolume(float H)
        {
            H = Mathf.Max(0f, H);
            float sectionArea = 0.5f * (crestWidthMetres + CalculateBaseWidth(H)) * H;
            return sectionArea * Mathf.Max(0f, crestLengthMetres);
        }

        /// <summary> Concrete mass, in kilograms, for a structural height of <paramref name="H"/>. </summary>
        public float CalculateConcreteMass(float H)
        {
            return CalculateConcreteVolume(H) * ConcreteDensityKgPerCubicMetre;
        }

        /// <summary> Spillway discharge, in cubic metres per second, for a head of <paramref name="head"/> over the
        /// spillway crest. The weir law Q = C.L.H^1.5 with the coefficient <see cref="DamWorks.DischargeCoefficient"/>
        /// gives for the authored spillway form -- the same law the built chute is sized to. </summary>
        public float CalculateSpillwayDischarge(float head)
        {
            if (head <= 0f) return 0f;
            return (float)DamWorks.DischargeCoefficient(spillwayType) * spillwayWidthMetres * Mathf.Pow(head, 1.5f);
        }

        /// <summary> Low-level outlet discharge, in cubic metres per second, for a head of <paramref name="head"/>
        /// above the outlet invert. The orifice law Q = Cd.A.sqrt(2.g.H) on the circular bore, with the sharp-edged
        /// coefficient <see cref="DamWorks.OrificeCoefficient"/>. </summary>
        public float CalculateOutletDischarge(float head)
        {
            if (head <= 0f) return 0f;
            float radius = outletBoreDiameterMetres * 0.5f;
            float area = Mathf.PI * radius * radius;
            return (float)DamWorks.OrificeCoefficient * area * Mathf.Sqrt(2f * (float)DamWorks.Gravity * head);
        }

        /// <summary> Factor of safety against sliding along the foundation contact, for a structural height of
        /// <paramref name="H"/>, contact friction angle <paramref name="frictionAngleDeg"/> and cohesion
        /// <paramref name="cohesionPa"/>. When the dam has resolved, the figure the full section check produced is
        /// returned; before that it is the textbook 2D sum on a metre of section, (mu.W + c.B) / P, where W is the
        /// section weight, P the horizontal water thrust 0.5.rho.g.H^2, and mu = tan(phi). </summary>
        public float CalculateSlidingSafetyFactor(float H, float frictionAngleDeg, float cohesionPa)
        {
            if (lastRequest.resolved && lastStability.slidingFactorOfSafety > 0.0)
                return (float)lastStability.slidingFactorOfSafety;
            H = Mathf.Max(0f, H);
            if (H <= 0f) return 0f;
            float g = (float)DamWorks.Gravity;
            float baseW = CalculateBaseWidth(H);
            float weightPerMetre = 0.5f * (crestWidthMetres + baseW) * H * ConcreteDensityKgPerCubicMetre * g;
            float thrustPerMetre = 0.5f * WaterDensityKgPerCubicMetre * g * H * H;
            if (thrustPerMetre <= 0f) return 0f;
            float mu = Mathf.Tan(frictionAngleDeg * Mathf.Deg2Rad);
            return (mu * weightPerMetre + cohesionPa * baseW) / thrustPerMetre;
        }

        /// <summary> Distance from the upstream heel to the section's area centroid, in metres, for a structural
        /// height of <paramref name="H"/> and base width <paramref name="baseW"/>. The gravity section is a heel-side
        /// batter triangle, a crest rectangle and a toe-side batter triangle; each part's own centroid is area-weighted
        /// into one. This is the same construction <see cref="DamWorks"/> uses for the resolved figure, kept here so
        /// the pre-resolution estimate does not disagree with it. </summary>
        private float SectionCentroidFromHeelMetres(float H, float baseW)
        {
            float upstreamRun = H * upstreamFaceSlope;
            float downstreamRun = H * downstreamFaceSlope;
            float areaUpstream = 0.5f * upstreamRun * H;
            float areaMiddle = crestWidthMetres * H;
            float areaDownstream = 0.5f * downstreamRun * H;
            float area = areaUpstream + areaMiddle + areaDownstream;
            if (area <= 0f) { return baseW * 0.5f; }
            float centroidUpstream = upstreamRun * 2f / 3f;
            float centroidMiddle = upstreamRun + crestWidthMetres * 0.5f;
            float centroidDownstream = upstreamRun + crestWidthMetres + downstreamRun / 3f;
            return (areaUpstream * centroidUpstream + areaMiddle * centroidMiddle
                + areaDownstream * centroidDownstream) / area;
        }


        /// <summary> Factor of safety against overturning about the downstream toe, for a structural height of
        /// <paramref name="H"/>. Resolved figure when the dam has resolved; otherwise the ratio of the section
        /// weight's stabilising moment about the toe to the water thrust's overturning moment (the thrust acts a
        /// third of the height above the base). </summary>
        public float CalculateOverturningSafetyFactor(float H)
        {
            if (lastRequest.resolved && lastStability.overturningFactorOfSafety > 0.0)
                return (float)lastStability.overturningFactorOfSafety;
            H = Mathf.Max(0f, H);
            if (H <= 0f) return 0f;
            float g = (float)DamWorks.Gravity;
            float baseW = CalculateBaseWidth(H);
            float weightPerMetre = 0.5f * (crestWidthMetres + baseW) * H * ConcreteDensityKgPerCubicMetre * g;
            // The section weight acts through the trapezoid's centroid, and the lever arm is that centroid's distance
            // from the DOWNSTREAM TOE. The centroid sits toward the upstream heel for a battered section, so measured
            // from the toe the arm is baseWidth minus the distance from the heel -- roughly two thirds of the base,
            // not one third. This is the identical decomposition DamWorks.Stability builds (heel-side batter triangle,
            // crest rectangle, toe-side batter triangle), so the pre-resolution estimate agrees with the resolved
            // figure instead of reading half of it.
            float centroidFromHeel = SectionCentroidFromHeelMetres(H, baseW);
            float resisting = weightPerMetre * (baseW - centroidFromHeel);
            float thrustPerMetre = 0.5f * WaterDensityKgPerCubicMetre * g * H * H;
            float overturning = thrustPerMetre * (H / 3f);
            if (overturning <= 0f) return 0f;
            return resisting / overturning;
        }

        #endregion
    }
}

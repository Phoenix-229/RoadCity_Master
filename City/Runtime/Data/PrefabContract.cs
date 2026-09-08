// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Per-category collider expectation for a user-supplied prefab. Left to whatever an
    /// artist happened to save, a collider costs frames invisibly (a distant transmission tower with a
    /// mesh collider) or a placement is un-clickable (a scattered tree with none) -- so the policy is
    /// stated per category and enforced, not inferred. </summary>
    public enum PrefabColliderPolicy
    {
        /// <summary> Either is accepted; the validator does not check colliders for this category. </summary>
        Optional,

        /// <summary> The prefab MUST carry at least one collider (e.g. a scattered tree the player walks into). </summary>
        Required,

        /// <summary> The prefab must carry NO collider (e.g. distant scenery placed in the thousands). </summary>
        Forbidden
    }


    /// <summary> The per-category half of the RM-48 contract: the two terms that legitimately differ
    /// between one kind of content and another. A tree, a pole and a building do not share a collider
    /// need or an instance count, so these cannot be global constants -- but everything else in
    /// <see cref="PrefabContract"/> (pivot, scale, materials) is universal and lives there. AUTHORED,
    /// NOT GENERATED: a human states these per category set; the validator only enforces them. </summary>
    [Serializable]
    public struct PrefabCategoryRule
    {
        /// <summary> The category this rule governs -- the same string key a <see cref="BuildingInstance"/>
        /// (and RM-77's manifest / RM-55's resolver) references content by. </summary>
        public string category;

        /// <summary> True when this category is placed in enough quantity that an instance without an
        /// <see cref="UnityEngine.LODGroup"/> will not survive the frame budget. The validator reports a
        /// missing LOD group as a violation only for such categories. </summary>
        public bool requiresLod;

        /// <summary> Whether an instance of this category must, must not, or may carry a collider. </summary>
        public PrefabColliderPolicy colliderPolicy;

        /// <summary> A conservative default rule for an unfamiliar category: no LOD demanded and colliders
        /// left to the author. A real category set overrides this per category. </summary>
        public static PrefabCategoryRule Default(string category)
        {
            PrefabCategoryRule r;
            r.category = category;
            r.requiresLod = false;
            r.colliderPolicy = PrefabColliderPolicy.Optional;
            return r;
        }
    }


    /// <summary> RM-48's written prefab contract, as data: the universal terms every user-supplied
    /// prefab or model root must satisfy to be placed by this tool, whatever its category. It is the
    /// most load-bearing interface in the module -- the boundary between the tool (which places) and the
    /// project's content (which is supplied) -- so it is written down here AND enforced by
    /// <c>PrefabContractValidator</c>, because a contract described only in prose gets violated on the
    /// first busy afternoon and shows up later as a floating, sunk, backwards or magenta object.
    ///
    /// The terms, and why each is a term rather than a preference:
    /// - PIVOT at the base of the object and centred in plan. Every placement in this module seats an
    ///   object on a surface (a pole on a verge, a tree in a scatter, a building on a pad), so a pivot
    ///   anywhere else offsets every instance identically -- which reads as a systemic bug, not content.
    /// - FORWARD axis is local +Z. The placement machinery orients a prefab with
    ///   <c>Quaternion.LookRotation(tangent)</c>, which aligns local +Z to the direction of travel, so a
    ///   prefab modelled facing any other axis looks correct on one road and backwards on the next. This
    ///   is a modelling convention the validator cannot measure from geometry (there is no "front" in a
    ///   mesh), so it is stated here and carried per prefab for the placer; the validator enforces the
    ///   MEASURABLE terms below.
    /// - REAL-WORLD SCALE in metres at unit root scale. A 0.01 root scale from an FBX import multiplies
    ///   through every derived transform, and the module's spacing figures are all metric.
    /// - URP-COMPATIBLE MATERIALS. The project is URP-only; a Built-in-pipeline shader renders magenta
    ///   rather than failing loudly.
    /// - AN LOD GROUP where the category is placed in quantity (see <see cref="PrefabCategoryRule"/>).
    /// - A STATED COLLIDER POLICY per category (see <see cref="PrefabCategoryRule"/>).
    ///
    /// This struct holds the universal, measurable tolerances; the per-category terms are in
    /// <see cref="PrefabCategoryRule"/>. Every value is AUTHORED. </summary>
    [Serializable]
    public struct PrefabContract
    {
        /// <summary> How far, in metres, the lowest point of the geometry may sit from the pivot's Y=0
        /// plane before the pivot is judged not to be at the base. A small margin absorbs modelling
        /// noise; a real offset (an FBX pivot left at the model's centre) blows past it. </summary>
        public float pivotBaseToleranceMetres;

        /// <summary> How far, in metres, the geometry's plan centre (its X/Z midpoint) may sit from the
        /// pivot before the pivot is judged off-centre in plan. </summary>
        public float pivotPlanToleranceMetres;

        /// <summary> How far each component of the root's local scale may differ from 1 before the root
        /// scale is judged non-unit. Tight, because a wrong root scale is never a rounding artefact. </summary>
        public float scaleTolerance;

        /// <summary> Standing defaults: 5 cm of base slack, 10 cm of plan slack, and a root scale that
        /// must be 1 to within a thousandth. </summary>
        public static PrefabContract Default()
        {
            PrefabContract c;
            c.pivotBaseToleranceMetres = 0.05f;
            c.pivotPlanToleranceMetres = 0.10f;
            c.scaleTolerance = 0.001f;
            return c;
        }
    }
}

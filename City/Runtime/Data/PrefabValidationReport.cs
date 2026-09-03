#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The kinds of contract violation the validator can name. Each maps to exactly one term
    /// of <see cref="PrefabContract"/> / <see cref="PrefabCategoryRule"/>, so a report reads back against
    /// the written contract one line at a time and a fix is never ambiguous about which term it satisfies.
    /// The forward axis is deliberately absent: it is a modelling convention the placer carries, not a
    /// property the validator can measure from geometry. </summary>
    public enum PrefabViolationKind
    {
        /// <summary> The root has no renderable mesh at all -- there is nothing to place or measure. </summary>
        NoRenderer,

        /// <summary> The lowest point of the geometry sits above the pivot's Y=0 plane by more than the
        /// tolerance: the object would float, or (if below) sink, by that amount at every placement. </summary>
        PivotNotAtBase,

        /// <summary> The geometry's plan centre is offset from the pivot in X or Z: every instance is
        /// shifted identically off its intended spot. </summary>
        PivotOffCentre,

        /// <summary> The root's local scale is not 1: real-world spacing figures multiply through wrong. </summary>
        RootScaleNonUnit,

        /// <summary> A material uses a shader outside the URP-compatible set -- it will render magenta. </summary>
        NonUrpMaterial,

        /// <summary> A renderer has an empty material slot: that submesh renders with the error shader. </summary>
        UnassignedMaterial,

        /// <summary> The category is placed in quantity but the prefab carries no <see cref="UnityEngine.LODGroup"/>. </summary>
        MissingLodGroup,

        /// <summary> The category's policy requires a collider and the prefab carries none. </summary>
        ColliderMissing,

        /// <summary> The category's policy forbids a collider and the prefab carries one. </summary>
        ColliderForbidden
    }


    /// <summary> One named failure against the contract: which term was broken and a message that quotes
    /// the MEASURED value beside the limit, so the report tells an author what to change, not merely that
    /// something is wrong. </summary>
    public struct PrefabViolation
    {
        /// <summary> Which contract term this violation belongs to. </summary>
        public PrefabViolationKind kind;

        /// <summary> Human-readable, with the measured number and the limit -- e.g. "pivot is 1.80 m
        /// above the mesh base (tolerance 0.05 m)". </summary>
        public string message;

        public PrefabViolation(PrefabViolationKind kind, string message)
        {
            this.kind = kind;
            this.message = message;
        }
    }


    /// <summary> The verdict on one prefab or model root against the contract: the asset it concerns, the
    /// category rule it was judged under, and every violation found -- ALL of them, not the first, because
    /// an author fixing one at a time and re-importing wastes the afternoon the written contract exists to
    /// save. A placement step (RM-12) treats a non-empty report as a refusal: a prefab that breaks the
    /// contract is not placed, rather than placed wrong. </summary>
    public sealed class PrefabValidationReport
    {
        /// <summary> The project-relative asset path judged, for the report line and the author's click-through. </summary>
        public readonly string assetPath;

        /// <summary> The category key the asset was validated as (its per-category rule governed the LOD
        /// and collider checks). </summary>
        public readonly string category;

        /// <summary> Every violation found, in the order the validator checks the terms. </summary>
        public readonly List<PrefabViolation> violations;

        public PrefabValidationReport(string assetPath, string category)
        {
            this.assetPath = assetPath;
            this.category = category;
            this.violations = new List<PrefabViolation>();
        }

        /// <summary> True when the asset satisfies every term -- the only state in which a placer may use it. </summary>
        public bool Passed
        {
            get { return violations.Count == 0; }
        }

        /// <summary> Records a violation. </summary>
        public void Add(PrefabViolationKind kind, string message)
        {
            violations.Add(new PrefabViolation(kind, message));
        }
    }
}

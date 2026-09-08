// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-48's enforcement half: reads a prefab OR model (FBX) root and reports every way it
    /// breaks <see cref="PrefabContract"/> / <see cref="PrefabCategoryRule"/>, each with the measured
    /// number beside the limit. It is a pure editor utility -- no scene state survives a call -- consumed
    /// by the prefab-import gate and (later) by RM-12's placement, which refuses a prefab whose report is
    /// non-empty rather than placing it wrong.
    ///
    /// Two project facts shape it. FIRST, content here is MODELS, not just prefabs (one authored `.prefab`
    /// beside dozens of FBX roots), so <see cref="ValidateFolder"/> sweeps `t:GameObject`, not `t:Prefab`,
    /// or it would judge one asset in fifty. SECOND, FBX materials resolve by NAME, not GUID: the validator
    /// reads the already-resolved <see cref="Renderer.sharedMaterials"/> and never traces a GUID, so a
    /// name-wired material is seen as present -- it is NOT reported as an orphan on GUID evidence. </summary>
    public static class PrefabContractValidator
    {
        /// <summary> A material passes the URP check when its shader's name begins with one of these. The
        /// project is URP-only; anything else (Standard, Legacy, Mobile, Autodesk Interactive from a raw
        /// FBX import) renders magenta. Detected by name so no URP assembly reference is needed. </summary>
        private static readonly string[] UrpShaderPrefixes =
        {
            "Universal Render Pipeline/",
            "Shader Graphs/",
            "UniverseCreator/"
        };

        /// <summary> Judges one root against the contract and returns every violation found (never just the
        /// first). <paramref name="asset"/> is the loaded prefab/model root; <paramref name="assetPath"/>
        /// is its project-relative path for the report line. </summary>
        public static PrefabValidationReport Validate(
            GameObject asset, string assetPath, PrefabContract contract, PrefabCategoryRule rule)
        {
            PrefabValidationReport report = new PrefabValidationReport(assetPath, rule.category);
            if (asset == null)
            {
                report.Add(PrefabViolationKind.NoRenderer, "asset could not be loaded as a GameObject");
                return report;
            }

            CheckRootScale(asset, contract, report);
            CheckPivotAndBounds(asset, contract, report);
            CheckMaterials(asset, report);
            CheckLodGroup(asset, rule, report);
            CheckColliders(asset, rule, report);
            return report;
        }

        /// <summary> Validates every GameObject asset under <paramref name="folderPath"/> against one
        /// category rule. The search key is `t:GameObject` on purpose: it matches BOTH `.prefab` assets
        /// and imported model (FBX) roots, so a folder of models is judged in full. </summary>
        public static List<PrefabValidationReport> ValidateFolder(
            string folderPath, PrefabContract contract, PrefabCategoryRule rule)
        {
            List<PrefabValidationReport> reports = new List<PrefabValidationReport>();
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                reports.Add(Validate(asset, path, contract, rule));
            }
            return reports;
        }

        /// <summary> The root's local scale must be 1 to within the tolerance; a 0.01 from an FBX import
        /// multiplies through every derived transform and every metric spacing figure. </summary>
        private static void CheckRootScale(GameObject asset, PrefabContract contract, PrefabValidationReport report)
        {
            Vector3 s = asset.transform.localScale;
            float d = Mathf.Max(Mathf.Abs(s.x - 1f), Mathf.Abs(s.y - 1f), Mathf.Abs(s.z - 1f));
            if (d > contract.scaleTolerance)
            {
                report.Add(PrefabViolationKind.RootScaleNonUnit,
                    string.Format("root local scale is ({0:0.###}, {1:0.###}, {2:0.###}), not unit (tolerance {3})",
                        s.x, s.y, s.z, contract.scaleTolerance));
            }
        }

        /// <summary> Measures the geometry relative to the pivot -- lowest point off the Y=0 base plane, and
        /// plan centre off the pivot in X/Z -- from a throwaway instance placed at the origin with identity
        /// rotation, so the world AABB reads directly as offsets in metres. The instance is always
        /// destroyed. A root with no renderable mesh is reported as such and skips the geometry checks. </summary>
        private static void CheckPivotAndBounds(GameObject asset, PrefabContract contract, PrefabValidationReport report)
        {
            GameObject instance = Object.Instantiate(asset);
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                Bounds b;
                if (!TryCombinedBounds(instance, out b))
                {
                    report.Add(PrefabViolationKind.NoRenderer, "no MeshRenderer or SkinnedMeshRenderer found");
                    return;
                }
                if (Mathf.Abs(b.min.y) > contract.pivotBaseToleranceMetres)
                {
                    report.Add(PrefabViolationKind.PivotNotAtBase,
                        string.Format("lowest geometry is {0:0.00} m from the pivot base plane (tolerance {1} m)",
                            b.min.y, contract.pivotBaseToleranceMetres));
                }
                float plan = Mathf.Max(Mathf.Abs(b.center.x), Mathf.Abs(b.center.z));
                if (plan > contract.pivotPlanToleranceMetres)
                {
                    report.Add(PrefabViolationKind.PivotOffCentre,
                        string.Format("plan centre is offset ({0:0.00}, {1:0.00}) m from the pivot (tolerance {2} m)",
                            b.center.x, b.center.z, contract.pivotPlanToleranceMetres));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary> The combined world AABB of every renderer that has geometry. Returns false when the
        /// root renders nothing. </summary>
        private static bool TryCombinedBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (!any) { bounds = r.bounds; any = true; }
                else { bounds.Encapsulate(r.bounds); }
            }
            return any;
        }

        /// <summary> Every material on every renderer must be URP-compatible. Reads the RESOLVED
        /// <see cref="Renderer.sharedMaterials"/> -- so a name-wired FBX material counts as present and is
        /// never called an orphan -- and reports each distinct offending material once. A genuinely empty
        /// slot is reported as unassigned (it renders with the error shader), which is a real fault, not the
        /// GUID-orphan false positive the name-resolution trap warns against. </summary>
        private static void CheckMaterials(GameObject asset, PrefabValidationReport report)
        {
            HashSet<Material> seen = new HashSet<Material>();
            foreach (Renderer r in asset.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null)
                    {
                        report.Add(PrefabViolationKind.UnassignedMaterial,
                            string.Format("renderer '{0}' has an empty material slot {1}", r.name, i));
                        continue;
                    }
                    if (!seen.Add(m))
                    {
                        continue;
                    }
                    if (m.shader == null || !IsUrpShader(m.shader))
                    {
                        report.Add(PrefabViolationKind.NonUrpMaterial,
                            string.Format("material '{0}' uses shader '{1}', not a URP-compatible shader",
                                m.name, m.shader == null ? "(none)" : m.shader.name));
                    }
                }
            }
        }

        /// <summary> A category placed in quantity must carry an LOD group, or a distant instance without
        /// one will not survive the frame budget. </summary>
        private static void CheckLodGroup(GameObject asset, PrefabCategoryRule rule, PrefabValidationReport report)
        {
            if (rule.requiresLod && asset.GetComponentInChildren<LODGroup>(true) == null)
            {
                report.Add(PrefabViolationKind.MissingLodGroup,
                    string.Format("category '{0}' is placed in quantity but the prefab has no LODGroup", rule.category));
            }
        }

        /// <summary> Enforces the category's stated collider policy: Required demands at least one collider,
        /// Forbidden demands none, Optional checks nothing. </summary>
        private static void CheckColliders(GameObject asset, PrefabCategoryRule rule, PrefabValidationReport report)
        {
            bool hasCollider = asset.GetComponentsInChildren<Collider>(true).Length > 0;
            if (rule.colliderPolicy == PrefabColliderPolicy.Required && !hasCollider)
            {
                report.Add(PrefabViolationKind.ColliderMissing,
                    string.Format("category '{0}' requires a collider but the prefab has none", rule.category));
            }
            else if (rule.colliderPolicy == PrefabColliderPolicy.Forbidden && hasCollider)
            {
                report.Add(PrefabViolationKind.ColliderForbidden,
                    string.Format("category '{0}' forbids a collider but the prefab carries one", rule.category));
            }
        }

        /// <summary> True when the shader name begins with a URP-compatible family prefix. </summary>
        private static bool IsUrpShader(Shader shader)
        {
            string name = shader.name;
            for (int i = 0; i < UrpShaderPrefixes.Length; i++)
            {
                if (name.StartsWith(UrpShaderPrefixes[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
#endif

#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-48's gate: proves <see cref="PrefabContractValidator"/> names every contract term it is
    /// meant to catch, and passes a clean root, on subjects built in memory -- because `City/Content` ships
    /// empty, so the gate cannot lean on real imported content. Each subject trips EXACTLY ONE term (a raised
    /// child for the pivot, a 0.01 root scale, a non-URP shader, and so on) so a failure points at one check.
    /// It does NOT judge that a real artist prefab looks right in the Scene view -- that is the author's
    /// visual check. Report lines are prefixed RCMPREFAB so a log grep reads the verdict cleanly. </summary>
    public static class PrefabContractGate
    {
        [MenuItem("Window/Road City Master/Testing/Prefab contract")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.PrefabContractGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Builds each subject, validates it, and checks the report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPREFAB BEGIN\n");
            List<string> failures = new List<string>();

            PrefabCategoryRule lodAndCollider = Rule("tree", true, PrefabColliderPolicy.Required);
            PrefabCategoryRule forbidCollider = Rule("scenery", false, PrefabColliderPolicy.Forbidden);
            PrefabCategoryRule plain = Rule("prop", false, PrefabColliderPolicy.Optional);

            // 1. CLEAN: unit scale, base at Y=0, centred, URP material, an LOD group, a collider.
            Case(builder, failures, "clean",
                Build(0f, 0f, 1f, true, true, true), lodAndCollider, null, true);

            // 2. PIVOT NOT AT BASE: geometry raised 1.8 m above the pivot.
            Case(builder, failures, "pivot-base",
                Build(1.8f, 0f, 1f, true, true, true), lodAndCollider, PrefabViolationKind.PivotNotAtBase, false);

            // 3. PIVOT OFF CENTRE: geometry shifted 1 m in X from the pivot.
            Case(builder, failures, "pivot-plan",
                Build(0f, 1f, 1f, true, true, true), lodAndCollider, PrefabViolationKind.PivotOffCentre, false);

            // 4. ROOT SCALE: a 0.01 FBX-import scale on the root.
            Case(builder, failures, "root-scale",
                Build(0f, 0f, 0.01f, true, true, true), lodAndCollider, PrefabViolationKind.RootScaleNonUnit, false);

            // 5. NON-URP MATERIAL: a Built-in shader that renders magenta under URP.
            Case(builder, failures, "non-urp",
                Build(0f, 0f, 1f, false, true, true), lodAndCollider, PrefabViolationKind.NonUrpMaterial, false);

            // 6. MISSING LOD: a category placed in quantity, no LOD group.
            Case(builder, failures, "missing-lod",
                Build(0f, 0f, 1f, true, true, false), lodAndCollider, PrefabViolationKind.MissingLodGroup, false);

            // 7. COLLIDER FORBIDDEN: distant scenery that must carry none, but does.
            Case(builder, failures, "collider-forbidden",
                Build(0f, 0f, 1f, true, true, false), forbidCollider, PrefabViolationKind.ColliderForbidden, false);

            // 8. COLLIDER REQUIRED: a walk-into prop that must carry one, but does not.
            Case(builder, failures, "collider-required",
                Build(0f, 0f, 1f, true, false, false), lodAndCollider, PrefabViolationKind.ColliderMissing, false);

            // 9. NO RENDERER: an empty root with nothing to place.
            Case(builder, failures, "no-renderer",
                new GameObject("empty"), plain, PrefabViolationKind.NoRenderer, false);

            foreach (string failure in failures)
            {
                builder.Append("RCMPREFAB FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPREFAB result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPREFAB END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Shorthand for a category rule. </summary>
        private static PrefabCategoryRule Rule(string category, bool requiresLod, PrefabColliderPolicy policy)
        {
            PrefabCategoryRule r;
            r.category = category;
            r.requiresLod = requiresLod;
            r.colliderPolicy = policy;
            return r;
        }

        /// <summary> Builds one in-memory subject: a root at <paramref name="rootScale"/> with a unit cube
        /// child whose base sits <paramref name="baseOffset"/> m above the pivot and <paramref name="planX"/>
        /// m off-centre in X. The cube carries a URP or a Built-in material, keeps or drops its collider, and
        /// the root gains a configured LOD group when asked. Everything else is contract-clean, so exactly
        /// the intended term is broken. </summary>
        private static GameObject Build(float baseOffset, float planX, float rootScale, bool urp, bool collider, bool lod)
        {
            GameObject root = new GameObject("subject");
            root.transform.localScale = new Vector3(rootScale, rootScale, rootScale);

            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(planX, 0.5f + baseOffset, 0f);

            BoxCollider box = child.GetComponent<BoxCollider>();
            if (!collider && box != null) { Object.DestroyImmediate(box); }

            Renderer renderer = child.GetComponent<Renderer>();
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Sprites/Default");
            if (shader != null) { renderer.sharedMaterial = new Material(shader); }

            if (lod)
            {
                LODGroup group = root.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(0.01f, new[] { renderer }) });
                group.RecalculateBounds();
            }
            return root;
        }

        /// <summary> Validates one subject, logs the report, asserts the expected outcome, and destroys the
        /// subject. <paramref name="expected"/> is the one violation kind the case is built to trip, or null
        /// for a clean pass. </summary>
        private static void Case(StringBuilder builder, List<string> failures, string name,
            GameObject subject, PrefabCategoryRule rule, PrefabViolationKind? expected, bool expectPass)
        {
            PrefabValidationReport report = PrefabContractValidator.Validate(
                subject, "(memory)/" + name, PrefabContract.Default(), rule);
            LogReport(builder, name, report);

            if (expectPass)
            {
                Expect(report.Passed,
                    name + ": expected a clean pass but got " + report.violations.Count + " violation(s)", failures);
            }
            else
            {
                Expect(!report.Passed, name + ": expected a violation but the report passed", failures);
                if (expected.HasValue)
                {
                    Expect(Contains(report, expected.Value),
                        name + ": expected " + expected.Value + " but it was not reported", failures);
                }
            }
            Object.DestroyImmediate(subject);
        }

        /// <summary> Appends one report as a single RCMPREFAB line: the case, pass/fail, count, and kinds. </summary>
        private static void LogReport(StringBuilder builder, string name, PrefabValidationReport report)
        {
            builder.Append("RCMPREFAB ").Append(name.PadRight(19))
                   .Append(report.Passed ? "PASS  " : "FAIL  ")
                   .Append("violations ").Append(report.violations.Count);
            for (int i = 0; i < report.violations.Count; i++)
            {
                builder.Append(i == 0 ? "  [" : ", ").Append(report.violations[i].kind);
            }
            if (report.violations.Count > 0) { builder.Append("]"); }
            builder.Append("\n");
        }

        /// <summary> True when the report carries a violation of the given kind. </summary>
        private static bool Contains(PrefabValidationReport report, PrefabViolationKind kind)
        {
            foreach (PrefabViolation v in report.violations)
            {
                if (v.kind == kind) { return true; }
            }
            return false;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

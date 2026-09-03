#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-54's gate: proves the sub-hierarchy contract on a throwaway root, then removes it. It asserts the
    /// five branches and their exact names; that every generated kind maps to exactly one branch so no call site can
    /// invent its own parenting; that generated names carry the source element's id and round-trip, while an author's
    /// own name does not parse as generated; that a second build REUSES the branches instead of creating a set beside
    /// them, and that an author's own child of a branch survives that; and that a generated object under a branch is
    /// recognised while one left at the scene root is not. Report lines are prefixed RCMCITYTREE. </summary>
    public static class CityHierarchyGate
    {
        [MenuItem("Window/Road City Master/Testing/City sub-hierarchy (five branches, reuse, deterministic names)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityHierarchyGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCITYTREE BEGIN\n");
            List<string> failures = new List<string>();

            CaseFiveNamedBranches(builder, failures);
            CaseBranchPerKind(builder, failures);
            CaseNamesCarryTheElementId(builder, failures);
            CaseRebuildReusesBranches(builder, failures);
            CaseNothingEscapesToRoot(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCITYTREE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCITYTREE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCITYTREE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void CaseFiveNamedBranches(StringBuilder builder, List<string> failures)
        {
            IList<CityBranch> all = CityHierarchy.All();
            Expect(all.Count == 5, "branches: there are not exactly five branches", failures);

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string name = CityHierarchy.BranchName(all[i]);
                Expect(!string.IsNullOrEmpty(name), "branches: a branch has no name", failures);
                Expect(seen.Add(name), "branches: two branches share the name " + name, failures);
                Expect(!string.IsNullOrEmpty(CityHierarchy.BranchPurpose(all[i])),
                    "branches: " + name + " does not state what belongs in it", failures);
            }
            Expect(seen.Contains("Network") && seen.Contains("Blocks") && seen.Contains("Buildings")
                && seen.Contains("Streetscape") && seen.Contains("Utilities"),
                "branches: the five documented names are not the five branch names", failures);

            builder.Append("RCMCITYTREE ").Append("five-branches".PadRight(22))
                   .Append("Network / Blocks / Buildings / Streetscape / Utilities, each stating its contents\n");
        }

        private static void CaseBranchPerKind(StringBuilder builder, List<string> failures)
        {
            Expect(CityHierarchy.BranchFor(GeneratedKind.Road) == CityBranch.Network,
                "kinds: a road was not parented into Network", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Junction) == CityBranch.Network,
                "kinds: a junction was not parented into Network", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Block) == CityBranch.Blocks,
                "kinds: a block was not parented into Blocks", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Lot) == CityBranch.Blocks,
                "kinds: a lot was not parented into Blocks", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Building) == CityBranch.Buildings,
                "kinds: a building was not parented into Buildings", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Furniture) == CityBranch.Streetscape,
                "kinds: RM-47's baked furniture was not parented into Streetscape", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Planting) == CityBranch.Streetscape,
                "kinds: planting was not parented into Streetscape", failures);
            Expect(CityHierarchy.BranchFor(GeneratedKind.Power) == CityBranch.Utilities
                && CityHierarchy.BranchFor(GeneratedKind.Water) == CityBranch.Utilities
                && CityHierarchy.BranchFor(GeneratedKind.Rail) == CityBranch.Utilities,
                "kinds: a utility carrier was not parented into Utilities", failures);

            builder.Append("RCMCITYTREE ").Append("branch-per-kind".PadRight(22))
                   .Append("each generated kind resolves to exactly one branch\n");
        }

        private static void CaseNamesCarryTheElementId(StringBuilder builder, List<string> failures)
        {
            string name = CityHierarchy.NameFor(GeneratedKind.Furniture, "street.14", 7);
            Expect(name.Contains("street.14"), "names: the source element id is not in the generated name", failures);
            Expect(name.Contains("#0007"), "names: the ordinal is not zero-padded and sortable", failures);
            Expect(!name.Contains("Prefab ("), "names: a generated name fell back to Unity's clone naming", failures);

            string id;
            Expect(CityHierarchy.TryParseName(name, out id) && id == "street.14",
                "names: a generated name did not round-trip its element id", failures);

            string blank = CityHierarchy.NameFor(GeneratedKind.Lot, null, 0);
            Expect(blank.Contains("unassigned"), "names: a missing id produced an ambiguous name", failures);

            string authorId;
            Expect(!CityHierarchy.TryParseName("My Lamp Post", out authorId),
                "names: an author's own object parsed as tool output", failures);

            builder.Append("RCMCITYTREE ").Append("names-carry-id".PadRight(22))
                   .Append("deterministic, searchable, diffable; author names stay distinguishable\n");
        }

        private static void CaseRebuildReusesBranches(StringBuilder builder, List<string> failures)
        {
            GameObject root = new GameObject("RCMCITYTREE_TempRoot");
            try
            {
                Transform t = root.transform;
                IList<CityBranch> all = CityHierarchy.All();
                for (int i = 0; i < all.Count; i++) { CityHierarchy.EnsureBranch(t, all[i]); }
                Expect(t.childCount == 5, "reuse: the first build did not create exactly five branches", failures);

                Transform streetscapeFirst = CityHierarchy.EnsureBranch(t, CityBranch.Streetscape);
                GameObject authorOwn = new GameObject("Author's own lamp");
                authorOwn.transform.SetParent(streetscapeFirst, false);

                for (int i = 0; i < all.Count; i++) { CityHierarchy.EnsureBranch(t, all[i]); }
                Expect(t.childCount == 5, "reuse: a rebuild created a second set of branches beside the first", failures);

                Transform streetscapeSecond = CityHierarchy.EnsureBranch(t, CityBranch.Streetscape);
                Expect(streetscapeFirst == streetscapeSecond,
                    "reuse: the rebuild returned a different Streetscape transform", failures);
                Expect(streetscapeSecond.childCount == 1 && streetscapeSecond.GetChild(0).name == "Author's own lamp",
                    "reuse: the author's own child of a branch did not survive the rebuild", failures);

                Expect(CityHierarchy.EnsureBranch(null, CityBranch.Network) == null,
                    "reuse: a null city root did not refuse quietly", failures);
            }
            finally { Object.DestroyImmediate(root); }

            builder.Append("RCMCITYTREE ").Append("reuse-not-duplicate".PadRight(22))
                   .Append("branches created on demand, reused on rebuild, never deleted by the tool\n");
        }

        private static void CaseNothingEscapesToRoot(StringBuilder builder, List<string> failures)
        {
            GameObject root = new GameObject("RCMCITYTREE_TempRoot2");
            try
            {
                Transform streetscape = CityHierarchy.EnsureBranch(root.transform, CityBranch.Streetscape);
                GameObject parented = new GameObject(CityHierarchy.NameFor(GeneratedKind.Furniture, "street.2", 1));
                parented.transform.SetParent(streetscape, false);

                CityBranch found;
                Expect(CityHierarchy.TryBranchOf(parented.transform, out found) && found == CityBranch.Streetscape,
                    "root: a parented object did not report the branch it sits in", failures);
                Expect(!CityHierarchy.TryBranchOf(root.transform, out found),
                    "root: the city root itself reported sitting in a branch", failures);

                GameObject escaped = new GameObject("RCMCITYTREE_Escaped");
                try
                {
                    Expect(!CityHierarchy.TryBranchOf(escaped.transform, out found),
                        "root: an object at the scene root was accepted as branch-parented", failures);
                }
                finally { Object.DestroyImmediate(escaped); }
            }
            finally { Object.DestroyImmediate(root); }

            builder.Append("RCMCITYTREE ").Append("nothing-at-root".PadRight(22))
                   .Append("branch membership is checkable, so an escaped object is detectable\n");
        }

        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            string a = CityHierarchy.NameFor(GeneratedKind.Building, "lot.88", 3);
            string b = CityHierarchy.NameFor(GeneratedKind.Building, "lot.88", 3);
            Expect(a == b, "determinism: the same element produced two different names", failures);
            Expect(CityHierarchy.BranchName(CityBranch.Utilities) == CityHierarchy.BranchName(CityBranch.Utilities),
                "determinism: a branch name is not stable", failures);
            builder.Append("RCMCITYTREE ").Append("determinism".PadRight(22))
                   .Append("same inputs -> identical names and branches\n");
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

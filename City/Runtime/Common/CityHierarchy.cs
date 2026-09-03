#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-54's core. Five named branches under the city root, because each is a different LIFETIME, not an
    /// aesthetic grouping: Network is authored and long-lived, Blocks is derived and regenerated whenever the network
    /// changes, Buildings may be hand-edited and kept, Streetscape is the most numerous and the most disposable, and
    /// Utilities crosses block boundaries and belongs to none of them. Regenerating one category without disturbing the
    /// others is only possible because the branches separate them. Two rules are enforced here rather than assumed:
    /// a branch is created on demand and NEVER deleted by the tool (so an author's own children of a branch survive a
    /// rebuild), and every generated name is deterministic and carries the id of the element it came from -- a hierarchy
    /// of "Prefab (1) ... Prefab (4173)" cannot be diffed, cannot be searched, and makes RM-70's assertions
    /// unwriteable. Nothing here looks anything up by path: callers hold the Transform this returns. </summary>

    /// <summary> The five branches. The order is the authoring order and is stable; do not renumber. </summary>
    public enum CityBranch { Network = 0, Blocks = 1, Buildings = 2, Streetscape = 3, Utilities = 4 }

    /// <summary> What a generated object is, so its branch is decided in one place instead of at every call site. </summary>
    public enum GeneratedKind
    {
        Road, Junction,
        Block, Lot,
        Building,
        Furniture, Planting,
        Power, Water, Rail
    }

    public static class CityHierarchy
    {
        /// <summary> The city root's own name. The root is found through a serialised reference, never by this string;
        /// it exists so a newly created root is named predictably. </summary>
        public const string RootName = "City";

        private static readonly CityBranch[] _all =
        {
            CityBranch.Network, CityBranch.Blocks, CityBranch.Buildings, CityBranch.Streetscape, CityBranch.Utilities
        };

        /// <summary> Every branch, in authoring order. </summary>
        public static IList<CityBranch> All() { return _all; }

        /// <summary> The exact child name of a branch. Fixed strings: RM-70 asserts against them and the documentation
        /// lists them, so they are not derived from the enum's ToString by accident. </summary>
        public static string BranchName(CityBranch branch)
        {
            switch (branch)
            {
                case CityBranch.Network: return "Network";
                case CityBranch.Blocks: return "Blocks";
                case CityBranch.Buildings: return "Buildings";
                case CityBranch.Streetscape: return "Streetscape";
                case CityBranch.Utilities: return "Utilities";
            }
            return "Network";
        }

        /// <summary> One sentence per branch, for the inspector and the documentation, so "what belongs in each" has a
        /// single source instead of being restated in three places. </summary>
        public static string BranchPurpose(CityBranch branch)
        {
            switch (branch)
            {
                case CityBranch.Network: return "Roads and junctions: authored, long-lived.";
                case CityBranch.Blocks: return "Block and lot geometry: derived, regenerated when the network changes.";
                case CityBranch.Buildings: return "Placed building instances: may be hand-edited and kept.";
                case CityBranch.Streetscape: return "Furniture and planting: the most numerous and the most disposable.";
                case CityBranch.Utilities: return "Power, water and rail carriers: they cross block boundaries.";
            }
            return "";
        }

        /// <summary> The branch a generated object belongs in. Decided from what the object IS, so no call site invents
        /// its own parenting rule. </summary>
        public static CityBranch BranchFor(GeneratedKind kind)
        {
            switch (kind)
            {
                case GeneratedKind.Road:
                case GeneratedKind.Junction: return CityBranch.Network;
                case GeneratedKind.Block:
                case GeneratedKind.Lot: return CityBranch.Blocks;
                case GeneratedKind.Building: return CityBranch.Buildings;
                case GeneratedKind.Furniture:
                case GeneratedKind.Planting: return CityBranch.Streetscape;
                case GeneratedKind.Power:
                case GeneratedKind.Water:
                case GeneratedKind.Rail: return CityBranch.Utilities;
            }
            return CityBranch.Streetscape;
        }

        /// <summary> A deterministic name that carries the source element's id and the ordinal within it, e.g.
        /// "Furniture[street.14]#0007". Same inputs, same name, every rebuild (C-06). An empty id is written as
        /// "unassigned" rather than left blank, so a name is never ambiguous. </summary>
        public static string NameFor(GeneratedKind kind, string sourceElementId, int ordinal)
        {
            string id = string.IsNullOrEmpty(sourceElementId) ? "unassigned" : sourceElementId;
            return kind.ToString() + "[" + id + "]#" + ordinal.ToString("D4", CultureInfo.InvariantCulture);
        }

        /// <summary> Read the source element id back out of a generated name. Returns false for anything the tool did
        /// not name, which is how an author's own child is told apart from generated output. </summary>
        public static bool TryParseName(string name, out string sourceElementId)
        {
            sourceElementId = null;
            if (string.IsNullOrEmpty(name)) { return false; }
            int open = name.IndexOf('[');
            int close = name.IndexOf(']');
            if (open <= 0 || close <= open + 1) { return false; }
            if (name.IndexOf('#') != close + 1) { return false; }
            sourceElementId = name.Substring(open + 1, close - open - 1);
            return true;
        }

        /// <summary> Find a branch under the city root, creating it only if it is absent. Reuses the existing child, so
        /// a rebuild never puts a second "Streetscape" beside the first. Never deletes anything. </summary>
        public static Transform EnsureBranch(Transform cityRoot, CityBranch branch)
        {
            if (cityRoot == null) { return null; }
            string wanted = BranchName(branch);
            for (int i = 0; i < cityRoot.childCount; i++)
            {
                Transform child = cityRoot.GetChild(i);
                if (child != null && child.name == wanted) { return child; }
            }
            GameObject created = new GameObject(wanted);
            created.transform.SetParent(cityRoot, false);
            return created.transform;
        }

        /// <summary> The branch a transform already sits in, or false if it is not under one. Used to assert that no
        /// generated object escaped to the scene root. </summary>
        public static bool TryBranchOf(Transform t, out CityBranch branch)
        {
            branch = CityBranch.Network;
            if (t == null || t.parent == null) { return false; }
            string parentName = t.parent.name;
            for (int i = 0; i < _all.Length; i++)
            {
                if (BranchName(_all[i]) == parentName) { branch = _all[i]; return true; }
            }
            return false;
        }
    }
}

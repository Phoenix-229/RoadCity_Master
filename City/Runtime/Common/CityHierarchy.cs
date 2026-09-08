// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
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

        /// <summary> Which kind a generated name declares. <see cref="NameFor"/> writes the kind's own name in front of
        /// the bracket, so the kind whose name prefixes it IS the kind -- asked of the enum rather than by cutting the
        /// string up a second way, which is the version that would still compile after a member was renamed and would
        /// silently stop matching. False for any name this class did not write. </summary>
        public static bool TryParseKind(string name, out GeneratedKind kind)
        {
            kind = GeneratedKind.Road;
            if (string.IsNullOrEmpty(name)) { return false; }
            System.Array kinds = System.Enum.GetValues(typeof(GeneratedKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                GeneratedKind candidate = (GeneratedKind)kinds.GetValue(i);
                if (!name.StartsWith(candidate.ToString() + "[", System.StringComparison.Ordinal)) { continue; }
                kind = candidate;
                return true;
            }
            return false;
        }

        /// <summary> How a block's or a lot's id is spelled inside a generated name, and read back out of one.
        ///
        /// The id in "Building[lot.88]#0003" is a string because <see cref="NameFor"/> takes a string: a furniture item's
        /// source element is a CONTENT CATEGORY, not a numbered record, so the argument cannot be an int. That leaves the
        /// two numbered cases needing a spelling, and a spelling that is written in one file and read in another is the
        /// kind that drifts -- so both directions live here, beside the name they go into. C-11's inspector is the reader:
        /// it turns "lot.88" back into the <see cref="CitySelection"/> that <see cref="CityEditOps.MarkOverridden"/>
        /// takes, which is the whole bridge between an object an author clicked on and the record the bake honours. </summary>
        public const string BlockElementPrefix = "block.";

        /// <summary> The lot spelling. See <see cref="BlockElementPrefix"/>. </summary>
        public const string LotElementPrefix = "lot.";

        /// <summary> The source element id for a block, for <see cref="NameFor"/>. </summary>
        public static string ElementIdForBlock(int blockId)
        {
            return BlockElementPrefix + blockId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary> The source element id for a lot, for <see cref="NameFor"/>. </summary>
        public static string ElementIdForLot(int lotId)
        {
            return LotElementPrefix + lotId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary> Read a numbered source element id back into the element it names. False for an id that names no
        /// numbered record -- a content category, or an id whose digits are not digits -- which is not a fault: it is how
        /// a furniture item is told apart from a building, and the caller says so rather than offering an edit that has
        /// nothing to edit. </summary>
        public static bool TryParseElementId(string sourceElementId, out CityElementKind kind, out int id)
        {
            kind = CityElementKind.None;
            id = 0;
            if (string.IsNullOrEmpty(sourceElementId)) { return false; }

            string digits;
            if (sourceElementId.StartsWith(BlockElementPrefix, System.StringComparison.Ordinal))
            {
                kind = CityElementKind.Block;
                digits = sourceElementId.Substring(BlockElementPrefix.Length);
            }
            else if (sourceElementId.StartsWith(LotElementPrefix, System.StringComparison.Ordinal))
            {
                kind = CityElementKind.Lot;
                digits = sourceElementId.Substring(LotElementPrefix.Length);
            }
            else
            {
                return false;
            }

            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                kind = CityElementKind.None;
                id = 0;
                return false;
            }
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

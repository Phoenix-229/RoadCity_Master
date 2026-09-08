// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-15 / C-11. The child-of-child coverage: what an author gets when they click on a city BRANCH, or on
    /// one of the objects a bake put inside it.
    ///
    /// WHY THIS IS NOT A CustomEditor. A branch is a bare GameObject with a Transform and nothing else, and so is every
    /// object a bake writes under it -- deliberately, because a marker component on the generated output would be a
    /// component shipped on thousands of scene objects for the editor's benefit. There is nothing for
    /// <c>[CustomEditor]</c> to attach to. B-11 already solved this shape for the road tool with
    /// <see cref="GeneratedObjectPanel"/>, which draws from <c>Editor.finishedDefaultHeaderGUI</c> under Unity's own
    /// GameObject header; this file is the city half of that panel and is called by it, so an author sees ONE box rather
    /// than two, and there is one header hook in the tool rather than one per subsystem.
    ///
    /// WHAT IT DOES NOT REPEAT. The identity sentences -- which branch, what it is for, which element produced this
    /// object -- are already written by <see cref="GeneratedObjectProvenance"/> and drawn above this. Restating them
    /// would be the duplication that lets two answers disagree. What is added here is what only the scene and the city
    /// data can answer:
    ///
    ///   * WHERE THE OBJECT ACTUALLY SITS, from <see cref="CityHierarchy.TryBranchOf"/>, against where its kind belongs.
    ///     The bake finds its previous output by name directly under one branch, so an object dragged into another one is
    ///     not found: the next bake makes a second copy in the right place. That is a fault an author cannot see and the
    ///     hierarchy cannot report.
    ///   * WHETHER A HAND EDIT SURVIVES THE NEXT BAKE, and the one control that changes the answer:
    ///     <see cref="CityEditOps.MarkOverridden"/>. It and <see cref="CitySelectionResolver"/> were written with no UI
    ///     at all -- this is the wiring, not new logic. The bridge is
    ///     <see cref="CityHierarchy.TryParseElementId"/>: the object's name carries "lot.88", and the ops take a
    ///     <see cref="CitySelection"/>.
    ///
    /// The sentences are pure functions of their inputs so a gate can assert the wording without a skin, which is the
    /// same split <see cref="GeneratedObjectPanel"/> keeps. <see cref="Draw"/> itself assumes the caller has already
    /// probed for a usable editor skin, because the one caller does. </summary>
    public static class CityBranchEditor
    {
        #region "Pure -- what is true, without a GUI"
        /// <summary> Whether this object's own name is one of the five branch names. A branch is identified by its NAME
        /// because that is what <see cref="CityHierarchy.EnsureBranch"/> finds it by; <see cref="CityHierarchy.TryBranchOf"/>
        /// answers the different question of what a CHILD sits under, and returns false on a branch itself. </summary>
        public static bool IsBranchName(string objectName, out CityBranch branch)
        {
            branch = CityBranch.Network;
            if (string.IsNullOrEmpty(objectName)) { return false; }
            System.Collections.Generic.IList<CityBranch> all = CityHierarchy.All();
            for (int i = 0; i < all.Count; i++)
            {
                if (CityHierarchy.BranchName(all[i]) != objectName) { continue; }
                branch = all[i];
                return true;
            }
            return false;
        }

        /// <summary> The selection the edit ops take, from the source element id the object's name carries. False when
        /// the id names no numbered record -- a furniture item's element is a content category -- which is a state to
        /// explain, not a failure. </summary>
        public static bool TryElementSelection(string sourceElementId, out CitySelection selection, out string elementWord)
        {
            selection = CitySelection.None;
            elementWord = string.Empty;

            CityElementKind kind;
            int id;
            if (!CityHierarchy.TryParseElementId(sourceElementId, out kind, out id)) { return false; }

            if (kind == CityElementKind.Block)
            {
                selection = CitySelection.ForBlock(id);
                elementWord = "block";
                return true;
            }
            if (kind == CityElementKind.Lot)
            {
                selection = CitySelection.ForLot(id);
                elementWord = "lot";
                return true;
            }
            return false;
        }

        /// <summary> What the next bake does to a BRANCH. Precise about the one case that is not a bake:
        /// <see cref="CityHierarchy.EnsureBranch"/> finds-or-creates and never destroys, so a branch and any child an
        /// author parented into it survive every bake -- but *Clear city*, on the city's own inspector, deletes the whole
        /// branch, and an author who reads "never deleted" and then presses it has been told a half-truth. </summary>
        public static string BranchBakeSentence(CityBranch branch)
        {
            return "A bake never deletes this branch: it finds it, or makes it once, and writes its output inside. A "
                + "child you parent in here yourself survives too. What does remove it is *Clear city* on the city's own "
                + "inspector, which deletes all five branches and everything in them.";
        }

        /// <summary> What the next bake does to a generated object whose source element IS a numbered block or lot, and
        /// the exact reach of the button below it.
        ///
        /// The caveat is the load-bearing half. <see cref="CityEditOps.MarkOverridden"/> sets a flag on the RECORD in the
        /// city data, and <see cref="CityData.ClearDerived"/> then keeps that record through the next bake. It says
        /// nothing about this scene object: the bake writes this object's transform from the record again, so a position
        /// dragged here is overwritten either way. Telling an author their drag is protected when it is not would be a
        /// worse defect than the silence this panel replaces. </summary>
        public static string ElementBakeSentence(bool overridden, string elementWord, int id)
        {
            string what = elementWord + " " + id.ToString(CultureInfo.InvariantCulture);
            if (overridden)
            {
                return "The " + what + " this was built for is marked hand-owned, so the next bake keeps it instead of "
                    + "recomputing it from the network and the seed. The object you are looking at is still remade from "
                    + "that record: a transform you drag here does not survive, the " + elementWord + " itself does.";
            }
            return "The next bake recomputes the " + what + " this was built for from the network and the seed, then "
                + "writes this object again from the result. Marking it hand-owned keeps the " + elementWord
                + " itself through the next bake -- not this object's transform, which is written from the "
                + elementWord + " either way.";
        }

        /// <summary> Why there is no control on an object whose source element is not a numbered record. The furniture
        /// operator names its output by CONTENT CATEGORY, so there is no block and no lot to mark; what decides such an
        /// object is the node that placed it. </summary>
        public static string NoElementSentence(string sourceElementId)
        {
            string id = string.IsNullOrEmpty(sourceElementId) ? "unassigned" : sourceElementId;
            return "This was built for \"" + id + "\", which is a content category rather than a numbered block or lot, "
                + "so there is nothing here to mark as hand-owned. What places it is the node that produced it: change "
                + "that node's parameters on the city graph and bake again.";
        }

        /// <summary> The mismatch sentence: this object sits under one branch and its kind belongs under another. The
        /// consequence is specific and is the reason this is worth a message at all -- the bake looks for its previous
        /// output by name directly under the branch the KIND maps to, so it will not find this one and will make a second
        /// copy over there. </summary>
        public static string WrongBranchSentence(GeneratedKind kind, CityBranch actual, CityBranch expected)
        {
            return "This sits under " + CityHierarchy.BranchName(actual) + " and a " + kind.ToString().ToLower()
                + " belongs under " + CityHierarchy.BranchName(expected) + ". The next bake looks for it by name "
                + "directly under " + CityHierarchy.BranchName(expected) + ", does not find it here, and makes a second "
                + "one there. Move it back, or delete it and bake again.";
        }

        /// <summary> The same consequence for an object that is not directly under any branch -- nested one level deeper,
        /// usually by being dragged onto another generated object. </summary>
        public static string NotUnderBranchSentence(GeneratedKind kind, CityBranch expected)
        {
            return "This is not directly under a city branch. The next bake looks for it by name directly under "
                + CityHierarchy.BranchName(expected) + ", does not find it, and makes a second one there. Move it back "
                + "under " + CityHierarchy.BranchName(expected) + ", or delete it and bake again.";
        }
        #endregion


        #region "Scene -- what only the hierarchy can answer"
        /// <summary> The city an object belongs to, found by its COMPONENT and not by the root's name.
        ///
        /// <see cref="CitySettlementFactory.CreateCity"/> names the root from an author-supplied name, so a city called
        /// "Riverport" is still a city and a test against <see cref="CityHierarchy.RootName"/> would miss it. The
        /// component is the identity. The walk starts at <paramref name="t"/> itself, so a city root answers about
        /// itself. </summary>
        public static bool TryFindCity(Transform t, out CityAuthoring city)
        {
            city = null;
            Transform walk = t;
            while (walk != null)
            {
                CityAuthoring found = walk.GetComponent<CityAuthoring>();
                if (found != null) { city = found; return true; }
                walk = walk.parent;
            }
            return false;
        }

        /// <summary> Whether this panel has anything to add about the object. Both conditions are required: it is under a
        /// city, so a road object whose name happens to parse is not claimed; and its own name is either one of the five
        /// branch names or a name <see cref="CityHierarchy.NameFor"/> wrote. Everything else under a city -- the root,
        /// which has its own inspector, and a child an author parented in -- is left to the general panel, which already
        /// says the right thing about it. </summary>
        public static bool Owns(GameObject go)
        {
            if (go == null) { return false; }
            CityAuthoring city;
            if (!TryFindCity(go.transform, out city)) { return false; }
            CityBranch branch;
            if (IsBranchName(go.name, out branch)) { return true; }
            GeneratedKind kind;
            return CityHierarchy.TryParseKind(go.name, out kind);
        }
        #endregion


        #region "Draw -- the city half of one box"
        /// <summary> Draw the city half. The caller has already probed for a usable editor skin, has already opened the
        /// foldout, and has already written the identity sentences; this adds only the two answers those cannot give.
        /// Styles are READ and never written, so <c>InspectorStyleOwnershipGate</c> holds. </summary>
        public static void Draw(GameObject go)
        {
            if (go == null) { return; }

            CityAuthoring city;
            if (!TryFindCity(go.transform, out city)) { return; }

            CityBranch branch;
            if (IsBranchName(go.name, out branch)) { DrawBranch(go, branch); return; }

            GeneratedKind kind;
            string elementId;
            if (!CityHierarchy.TryParseKind(go.name, out kind)) { return; }
            if (!CityHierarchy.TryParseName(go.name, out elementId)) { return; }
            DrawGenerated(go, city, kind, elementId);
        }

        /// <summary> A branch. The count is the one thing the panel can say that the hierarchy makes an author expand a
        /// triangle to learn, and it is the number that answers "did the bake do anything". </summary>
        private static void DrawBranch(GameObject go, CityBranch branch)
        {
            int children = go.transform.childCount;
            string count = children == 1
                ? "1 object inside."
                : children.ToString(CultureInfo.InvariantCulture) + " objects inside.";
            EditorGUILayout.LabelField(count, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("A branch is a container, not a city element: there is nothing here to mark as "
                + "hand-owned.", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(BranchBakeSentence(branch), MessageType.Info);
        }
        /// <summary> A generated object. Two answers, in the order an author needs them: is it in the right place, and
        /// does anything they do to it survive. </summary>
        private static void DrawGenerated(GameObject go, CityAuthoring city, GeneratedKind kind, string elementId)
        {
            CityBranch expected = CityHierarchy.BranchFor(kind);

            // WHERE IT ACTUALLY SITS. The identity line above names the branch this KIND maps to; only the scene knows
            // whether the object is really there, and CityBakeRunner.EnsureObject searches ONE branch, non-recursively.
            CityBranch actual;
            if (!CityHierarchy.TryBranchOf(go.transform, out actual))
            {
                EditorGUILayout.HelpBox(NotUnderBranchSentence(kind, expected), MessageType.Warning);
            }
            else if (actual != expected)
            {
                EditorGUILayout.HelpBox(WrongBranchSentence(kind, actual, expected), MessageType.Warning);
            }
            else
            {
                // In the right branch: say what that branch is FOR, which the identity line names but does not explain.
                EditorGUILayout.LabelField(CityHierarchy.BranchPurpose(actual), EditorStyles.miniLabel);
            }

            // WHETHER A HAND EDIT SURVIVES. Three states have no control to offer, and each is a different sentence
            // rather than a disabled button, because a disabled button does not say why.
            CitySelection selection;
            string elementWord;
            if (!TryElementSelection(elementId, out selection, out elementWord))
            {
                EditorGUILayout.HelpBox(NoElementSentence(elementId), MessageType.Info);
                return;
            }

            if (city.cityData == null)
            {
                EditorGUILayout.HelpBox("This city has no city data asset yet, and the hand-owned flag is stored on that "
                    + "asset rather than on this object. Bake the city once and the control appears here.",
                    MessageType.Info);
                return;
            }
            string number = selection.primaryId.ToString(CultureInfo.InvariantCulture);
            bool recordExists = selection.kind == CityElementKind.Block
                ? CityEditOps.FindBlock(city.cityData, selection.primaryId) != null
                : CityEditOps.FindLot(city.cityData, selection.primaryId) != null;
            if (!recordExists)
            {
                // MarkOverridden returns false when the record is gone, so a button here would be one that does nothing.
                EditorGUILayout.HelpBox("The " + elementWord + " " + number + " this was built for is no longer in the "
                    + "city data, so this object is left over from an earlier bake. There is nothing to mark: bake again "
                    + "and it goes.", MessageType.Warning);
                return;
            }

            bool overridden = CityEditOps.IsOverridden(city.cityData, selection);
            EditorGUILayout.HelpBox(ElementBakeSentence(overridden, elementWord, selection.primaryId),
                overridden ? MessageType.Info : MessageType.Warning);

            string label = overridden ? "Let the next bake recompute it" : "Mark as overridden";
            string tip = overridden
                ? "Clear the hand-owned flag on " + elementWord + " " + number + " so the next bake recomputes it from "
                    + "the network and the seed."
                : "Set the hand-owned flag on " + elementWord + " " + number + " so the next bake keeps it instead of "
                    + "recomputing it from the network and the seed.";
            if (GUILayout.Button(new GUIContent(label, tip),
                GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                Apply(city, selection, !overridden);
            }
        }
        /// <summary> The mutation, and the only one this file performs. <see cref="CityEditOps"/> deliberately touches
        /// nothing but the data -- its own doc says the surrounding Undo and dirty-marking belong to the Editor assembly,
        /// which is here. <see cref="CityData"/> is a ScriptableObject, so without the
        /// <see cref="EditorUtility.SetDirty"/> the flag is set in memory and lost when Unity next reloads the asset.
        /// The record is checked before the flag is written, so an op that reports false leaves no dirty asset behind. </summary>
        private static void Apply(CityAuthoring city, CitySelection selection, bool overridden)
        {
            if (city == null || city.cityData == null) { return; }

            Undo.RecordObject(city.cityData,
                overridden ? "Mark city element overridden" : "Revert city element to derived");

            bool changed = overridden
                ? CityEditOps.MarkOverridden(city.cityData, selection)
                : CityEditOps.Revert(city.cityData, selection);
            if (!changed) { return; }

            EditorUtility.SetDirty(city.cityData);
        }
        #endregion
    }
}
#endif

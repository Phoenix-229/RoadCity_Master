// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-11. The panel that answers, on the tool's own generated children, the four questions the hierarchy
    /// could not: what is this, what produced it, which road or intersection owns it, and will an edit I make here
    /// survive the next build.
    ///
    /// The road tool has eight custom inspectors and every one of them is attached to a component an author placed. The
    /// objects an author actually clicks on while looking for something -- MainMeshes and the eleven objects under it,
    /// the twelve combined plates under an intersection, the signs and signal heads, the staging containers -- carry no
    /// component of ours at all, so no `CustomEditor` can ever reach them. Their inspector is Unity's: a Transform, a
    /// MeshFilter, a MeshRenderer, and nothing that says the mesh in front of you is rewritten from scratch the next
    /// time the road is built.
    ///
    /// WHY A HEADER HOOK. `Editor.finishedDefaultHeaderGUI` is raised by `Editor.DrawHeader` immediately after
    /// `OnHeaderGUI`, and Unity itself subscribes to it in `AssetImporterEditor` to hang a notification under an
    /// importer's header. It is the only mechanism that can add a panel to an object carrying none of our components
    /// without either adding a runtime component to generated output -- which would change what gets BUILT, and B-11 is
    /// explicitly editor-side only -- or registering a `CustomEditor` for Transform or MeshFilter, which would hijack
    /// those inspectors for every object in the project including objects that have nothing to do with this tool.
    ///
    /// The hook fires for every selection in the project, so silence is the default and is a requirement rather than a
    /// nicety: <see cref="GeneratedObjectProvenance.ForTransform"/> returns `isKnown` false for anything the tool did not
    /// name, and this draws nothing at all in that case. The test is a string comparison against a table, so the cost on
    /// someone else's GameObject is a walk of one list.
    ///
    /// C-11 ADDED THE CITY HALF TO THIS SAME HOOK rather than a second one. A city branch, and every object a city bake
    /// writes inside it, is the identical shape of problem -- a bare GameObject carrying no component of ours -- and a
    /// second header subscription would put a second box under the same header with its own version of the same answer.
    /// <see cref="CityBranchEditor.Owns"/> decides, and <see cref="CityBranchEditor.Draw"/> writes the two sentences only
    /// the scene and the city data can give. It also REPLACES the disposition sentence below, which is written in road
    /// vocabulary: see the comment at that call. </summary>
    public static class GeneratedObjectPanel
    {
        /// <summary> The foldout identity. Not a component type, but the same store and the same convention: an author
        /// who collapses this panel means it for generated objects, not for the one they happened to be looking at.
        /// </summary>
        public const string FoldoutType = "GeneratedObject";

        /// <summary> The heading over the panel. A pure function so a gate can assert the wording without a GUI: the
        /// heading has to distinguish output the build owns from scaffolding the AUTHOR owns, because telling someone
        /// their road is about to be overwritten when it is not is the same defect as the silence it replaces.
        /// </summary>
        public static string HeaderLabel(GeneratedObjectView view)
        {
            if (!view.isKnown) { return string.Empty; }
            if (view.disposition == RebuildDisposition.AuthorOwned) { return "Yours to edit — " + view.title; }
            return "Generated — " + view.title;
        }

        /// <summary> The label on the select button, which names what it will select rather than always saying "road":
        /// the junction plates and the centre markers are produced by the road's mesh builder but parented to the
        /// INTERSECTION, and a button that said road while selecting an intersection would be its own small lie.
        /// </summary>
        public static string SelectButtonLabel(string ownerLabel)
        {
            string what = string.IsNullOrEmpty(ownerLabel) ? "owner" : ownerLabel;
            return "Select the " + what + " that made this";
        }

        [InitializeOnLoadMethod]
        private static void Install()
        {
            // Removed first, then added: a domain reload runs this again and a plain += would draw the panel twice.
            Editor.finishedDefaultHeaderGUI -= OnHeader;
            Editor.finishedDefaultHeaderGUI += OnHeader;
        }

        private static void OnHeader(Editor editor)
        {
            if (editor == null) { return; }

            // One object only. On a multi-selection the objects can have different provenance, and four sentences about
            // the first of them is worse than nothing.
            Object[] targets = editor.targets;
            if (targets == null || targets.Length != 1) { return; }

            // The GameObject header, not a component's. The hook is raised for component editors too, and their target
            // is the component -- this cast is what keeps the panel to one draw per inspector.
            GameObject go = editor.target as GameObject;
            if (go == null) { return; }

            // A scene object. A prefab asset selected in the Project window has no scene and no build behind it, so
            // provenance is not a question that can be answered about it.
            if (!go.scene.IsValid()) { return; }

            // Batchmode: the skin getters throw rather than return null, so this is the project's own probe.
            if (!InspectorTextLayout.SkinIsAvailable()) { return; }

            GeneratedObjectView view = GeneratedObjectProvenance.ForTransform(go.transform);
            if (!view.isKnown) { return; }

            Draw(go, view);
        }


        /// <summary> Which shade of box the disposition gets. A rebuild that DESTROYS the object an author is looking at
        /// is a warning; one that keeps it, and scaffolding they are meant to edit, are information. </summary>
        public static MessageType Severity(RebuildDisposition disposition)
        {
            if (disposition == RebuildDisposition.Regenerated) { return MessageType.Warning; }
            if (disposition == RebuildDisposition.Transient) { return MessageType.Warning; }
            return MessageType.Info;
        }

        /// <summary> The panel. Four facts and at most one button, drawn with the tool's own paragraph and command-button
        /// spellings so it reads as part of the same inspector rather than a bolted-on box, and reading styles without
        /// writing any -- InspectorStyleOwnershipGate holds that line and this is inside it. </summary>
        public static void Draw(GameObject go, GeneratedObjectView view)
        {
            if (go == null || !view.isKnown) { return; }

            InspectorTextLayout.Apply();

            bool wasOpen = InspectorFoldoutState.IsOpen(FoldoutType, FieldGroup.WhatItIs);
            bool isOpen = EditorGUILayout.Foldout(wasOpen, HeaderLabel(view), true);
            if (isOpen != wasOpen) { InspectorFoldoutState.SetOpen(FoldoutType, FieldGroup.WhatItIs, isOpen); }
            if (!isOpen) { return; }

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(view.whatItIs, EditorStyles.wordWrappedLabel);
            GUILayout.Space(4f);

            if (view.insideGenerated.Length > 0)
            {
                EditorGUILayout.LabelField("Inside " + view.insideGenerated + ", which the build owns.",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.LabelField("Produced by " + view.producedBy + ".", EditorStyles.miniLabel);

            Transform owner;
            string ownerLabel;
            bool haveOwner = GeneratedObjectProvenance.TryFindOwner(go.transform, out owner, out ownerLabel);
            if (haveOwner && owner != null && owner != go.transform)
            {
                EditorGUILayout.LabelField("Owned by " + owner.name + ", the " + ownerLabel + ".",
                    EditorStyles.miniLabel);
            }
            GUILayout.Space(4f);

            // The disposition, in ONE box. When a city owns the object the generic sentence is not drawn at all: its
            // Regenerated wording ends "Change the road or the intersection that owns it", and for a city lot the thing
            // to change is a node on the city graph or the lot's own hand-owned flag. Two boxes disagreeing about what to
            // do next is the defect this panel exists to remove, so the city half replaces it rather than joining it.
            if (CityBranchEditor.Owns(go))
            {
                CityBranchEditor.Draw(go);
            }
            else
            {
                EditorGUILayout.HelpBox(GeneratedObjectProvenance.DispositionSentence(view.disposition),
                    Severity(view.disposition));
            }

            // The way out of the generated tree. Drawn only when there is something ELSE to select: on the road or the
            // intersection itself the owner is the selection, and a button that reselects what is already selected is a
            // control that does nothing.
            if (haveOwner && owner != null && owner != go.transform)
            {
                if (GUILayout.Button(new GUIContent(SelectButtonLabel(ownerLabel),
                        "Select " + owner.name + ", which is where this object is actually changed from. Editing it "
                        + "here does not survive a rebuild; editing the " + ownerLabel + " is what rebuilds it."),
                    GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
                {
                    RequestSelect(owner.gameObject);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static GameObject _pendingSelection;

        /// <summary> What the select button has asked for and not yet been given. A gate reads this to prove the press
        /// does not change the selection on the spot. </summary>
        public static GameObject PendingSelection { get { return _pendingSelection; } }

        /// <summary> Ask for a selection change on the next editor tick rather than during this one.
        ///
        /// Setting `Selection.activeGameObject` from inside a header pass replaces the inspector whose header is halfway
        /// through being drawn, and the layout groups opened before the press are then closed against a different
        /// editor. Deferring by one tick is how the rest of this tool defers work out of a drawing pass -- the same
        /// `delayCall` and the same named-method rule, because a lambda subscribed with += cannot be removed with -=.
        /// Removed before it is added, so two presses in one frame cannot queue two selections. </summary>
        public static void RequestSelect(GameObject target)
        {
            _pendingSelection = target;
            EditorApplication.delayCall -= ApplySelectionNow;
            EditorApplication.delayCall += ApplySelectionNow;
        }

        /// <summary> Hand the selection over, and ping it so the hierarchy scrolls to it: an author who presses this on a
        /// plate buried under an intersection needs to SEE where they landed. </summary>
        public static void ApplySelectionNow()
        {
            GameObject target = _pendingSelection;
            _pendingSelection = null;
            if (target == null) { return; }
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
#endif

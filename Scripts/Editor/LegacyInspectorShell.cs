#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-102. The one place the legacy inspectors get their modern behaviour from: the product header, the
    /// "this object is building" line, the RM-51 foldouts drawn through <see cref="InspectorFieldDrawer"/>, and a
    /// rebuild request that coalesces.
    ///
    /// A shell rather than a rewrite, because the instruction is overhaul-do-not-break. The legacy editors hold
    /// accumulated knowledge nobody wrote down -- which buttons are destructive, which operations are meaningless on a
    /// road with no nodes, the order the wizard walks an author through -- and deleting them to write new ones is how a
    /// road tool loses features silently. So they keep their own structure and their own buttons, and hand their
    /// registry-known VALUES to this: one drawer that knows each field's unit, its range, and where the range comes
    /// from, instead of eleven editors each inventing a layout and a validation rule.
    ///
    /// Async-awareness lives here for the same reason. Once a build is queued rather than run inline, an inspector that
    /// does not know one is in flight will start a second, or write to data a worker is reading, or repaint a value the
    /// finishing build is about to overwrite. Editing stays ENABLED while a build runs -- disabling it would make the
    /// tool feel broken for the seconds a build takes -- and the edit is coalesced instead. </summary>
    public static class LegacyInspectorShell
    {
        #region "Header"
        /// <summary> The product header every inspector opens with (C-05: one product string, from the model). </summary>
        public static void Header(string subject)
        {
            string title = InspectorFieldRegistry.ProductDisplayName;
            if (!string.IsNullOrEmpty(subject)) { title = title + " — " + subject; }
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }


        /// <summary> B-5, fault 1. One section boundary inside a legacy inspector: a bold title with space above it, no
        /// trailing colon, and its explanation on the hover.
        ///
        /// The fault was that a section heading, a bare label ending in a colon and an RM-51 foldout all drew at the same
        /// weight, so nothing in the inspector read as a group boundary. The fix is not a heavier heading, it is ONE
        /// weight shared by both kinds of boundary: this draws bold, and <see cref="Group"/> draws its foldout bold
        /// through <see cref="BoldFoldout"/>, so a heading and a foldout are the same rank wherever they meet.
        ///
        /// No colon, because a heading is not a prefix label. The colon on a prefix label binds it to the control on its
        /// right; a heading has nothing on its right to bind to. </summary>
        public static void SectionHeader(string title)
        {
            SectionHeader(title, null);
        }


        /// <summary> The same heading with the sentence an author gets on hover. Null or empty draws no tooltip, and
        /// that is not the same as a heading nobody bothered to describe: a section whose every field carries its own
        /// tooltip does not always need a second one on the heading above them. </summary>
        public static void SectionHeader(string title, string tooltip)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.boldLabel);
        }


        /// <summary> The one bold-foldout style, built once and shared by every group boundary this shell draws.
        ///
        /// A COPY of <c>EditorStyles.foldout</c>, never that instance itself: it belongs to the editor, so switching its
        /// font style to bold would bold every foldout in every window Unity has open -- the same fault B-3 fixed for
        /// word wrap. RM-99's ownership gate allows the copy for exactly that reason and skips any line that makes one.
        ///
        /// Null when there is no editor skin. A gate running in batchmode has none, and the getter THROWS rather than
        /// returning null, so the probe is <see cref="InspectorTextLayout.SkinIsAvailable"/> and callers fall back to the
        /// unstyled overload rather than to a null style. </summary>
        private static GUIStyle boldFoldout;


        private static GUIStyle BoldFoldout()
        {
            if (boldFoldout != null) { return boldFoldout; }
            if (!InspectorTextLayout.SkinIsAvailable()) { return null; }
            boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            return boldFoldout;
        }
        #endregion


        #region "The build line"
        /// <summary> Reused every repaint. An inspector redraws many times a second and this is read on each one, so a
        /// fresh list per repaint would be garbage generated by the act of looking at a road. </summary>
        private static readonly List<City.BuildTicket> tickets = new List<City.BuildTicket>();

        /// <summary> Where this owner's build has got to. <paramref name="progress"/> is 0..1 while it is running and
        /// meaningless while it is queued, which <paramref name="queued"/> distinguishes -- a queued build drawn as 0%
        /// reads as a build that is running and getting nowhere. </summary>
        public static bool Building(string owner, out bool queued, out float progress)
        {
            int ahead;
            int ticketId;
            return Building(owner, out queued, out progress, out ahead, out ticketId);
        }


        /// <summary> RM-109. B-8. The same, plus how many builds are IN FRONT of this one --
        /// <paramref name="ahead"/> is the ticket's position in the queue, so 0 means nothing is in front of it.
        ///
        /// The distinction is not cosmetic. "Waiting for the build in front of it" is a true and useful sentence for a
        /// ticket with something in front of it, and a FALSE one for the ticket at the head: if the head of the queue is
        /// still not running, there is no build in front of it to wait for and the real fault is that nothing is pumping
        /// the queue at all (B-1). Printing the same sentence for both taught an author to read a stalled pipeline as
        /// normal queueing. <see cref="City.BuildDispatch.CopyTickets"/> hands the tickets over in queue order, so the
        /// loop index IS the position and no second query is needed.
        ///
        /// An overload rather than a changed signature: the three-argument form is what the RM-102 inspector gate
        /// asserts against, and callers that only want "is it building" should not have to declare a variable for a
        /// number they will not read.
        ///
        /// RM-110 (B-9) adds <paramref name="ticketId"/>, because a Cancel button has to name the ticket it cancels and
        /// this is the one place that already has it in hand -- <see cref="City.BuildDispatch.Cancel(int)"/> takes an
        /// id, and a second walk of the queue to find the same ticket again could find a different one, the queue having
        /// moved between the two walks. It is 0 when the owner has no ticket, which is the value Cancel refuses, so a
        /// caller that ignores the return value still cannot cancel something that was never found. </summary>
        public static bool Building(string owner, out bool queued, out float progress, out int ahead, out int ticketId)
        {
            queued = false;
            progress = 0f;
            ahead = 0;
            ticketId = 0;
            if (string.IsNullOrEmpty(owner)) { return false; }

            City.BuildDispatch.Shared.CopyTickets(tickets);
            for (int i = 0; i < tickets.Count; i++)
            {
                if (tickets[i].owner != owner) { continue; }
                queued = !tickets[i].isRunning;
                progress = tickets[i].progress;
                ahead = i;
                ticketId = tickets[i].id;
                return true;
            }
            return false;
        }


        /// <summary> Draw the line, and return whether anything was drawn. Nothing is drawn when the object is not
        /// building: a permanent "not building" row is noise in an inspector an author reads all day. </summary>
        public static bool BuildLine(string owner)
        {
            bool queued;
            float progress;
            int ahead;
            int ticketId;
            if (!Building(owner, out queued, out progress, out ahead, out ticketId)) { return false; }

            // RM-109. B-8. Three states, not two, and the third is the one that mattered: a queued ticket with nothing
            // ahead of it. The old line named a build in front of it in every queued case, which is a lie at the head of
            // the queue and hid a pipeline that was not being driven behind a sentence that read as normal.
            string text;
            if (!queued)
            {
                text = "Building — " + Mathf.RoundToInt(progress * 100f)
                    + "%. Editing stays enabled; changes are coalesced.";
            }
            else if (ahead > 0)
            {
                text = "Queued — " + ahead.ToString() + (ahead == 1 ? " build" : " builds")
                    + " ahead of it. Editing stays enabled; changes are coalesced.";
            }
            else
            {
                text = "Queued — next to build. Editing stays enabled; changes are coalesced.";
            }
            EditorGUILayout.HelpBox(text, MessageType.Info);

            // RM-110, B-9. The way out. A build an author no longer wants had no stop of any kind on it: the only
            // recourse was to wait for a road they had already decided against, and on a long road with terrain
            // deformation that is minutes of an editor they cannot use. BuildDispatch has carried a Cancel since RM-88
            // and nothing in the tool had ever called it.
            //
            // Drawn only while there IS a ticket, because this whole line is: a Cancel button on an object that is not
            // building is a control that does nothing, and an author who presses one and sees no change learns to
            // distrust the ones that do work.
            //
            // The id comes from the same walk of the queue that produced the text above, not from a fresh one. Cancel
            // means two different things depending on which ticket it finds -- a cooperative stop for the one that is
            // running, removal for one still waiting -- and BuildDispatch decides which; asking again here would be a
            // second copy of that decision, and one that could be answered about a different ticket if the queue moved
            // between the two questions.
            //
            // Drawn the way every other command in this shell is drawn: a bare button at CommandButtonWidth, no layout
            // group around it. One spelling for a command button is what makes a column of them line up.
            if (GUILayout.Button(new GUIContent("Cancel build",
                    "Stop this build. A build that has already started is asked to stop at its next safe point, so the "
                    + "road is left as it was rather than half-written; one still waiting is taken out of the queue. "
                    + "Whatever the road looked like before the build is what it looks like after."),
                GUILayout.Width(CommandButtonWidth)))
            {
                City.BuildDispatch.Shared.Cancel(ticketId);
            }
            return true;
        }
        #endregion


        #region "Controls more than one inspector draws"
        /// <summary> The width every command button in a legacy inspector is drawn at. B-6, fault 3 found four buttons
        /// in one inspector at three different widths in two different styles, which reads as four unrelated things
        /// rather than four commands. One figure, wide enough for a short sentence-case label at the editor's default
        /// font, is the whole rule. </summary>
        public const float CommandButtonWidth = 150f;


        /// <summary> The branded command-button style, built once for every inspector that draws a build command.
        ///
        /// B-11 finishes what B-6, fault 3 started. B-6 deleted `RoadSystemEditor`'s private copy of this style; four
        /// editors still had their own, and they were not four copies of one style -- they were three. `RoadEditor` and
        /// `RoadIntersectionEditor` branded `Update road` and `Update intersection` with `otherbg`/`otherbg2`,
        /// `SplineNEditor` branded `Open Wizard` with `loadbg`/`loadbgglow` and shifted its label 35 px off centre, and
        /// `TerrainEditor` built a fourth from `FlexBG` twice over -- the same texture for rest and for hover, so it had
        /// no hover at all -- and then never drew a single button with it. Four hand-built styles, three appearances, one
        /// of them dead. The command that rebuilds a road and the command that rebuilds an intersection are the same kind
        /// of act and now look like it.
        ///
        /// A COPY of `GUI.skin.button`, never that instance: RM-99's ownership gate allows the copy and forbids the
        /// mutation, which is B-3's fault written down as a rule. The width is <see cref="CommandButtonWidth"/> rather
        /// than the 128 px two of the four carried, so a branded command and a plain one line up.
        ///
        /// Null when there is no editor skin -- a gate in batchmode has none, and the getter THROWS rather than returning
        /// null, so the probe is <see cref="InspectorTextLayout.SkinIsAvailable"/>. The four call sites cannot see that
        /// null: they are inside `OnInspectorGUI`, which Unity does not call without a skin. It is returned for the case
        /// that CAN happen -- a gate asking what the style is, headlessly, where the answer is "there isn't one yet"
        /// rather than an exception. </summary>
        private static GUIStyle commandButton;

        public static GUIStyle CommandButtonStyle()
        {
            if (commandButton != null) { return commandButton; }
            if (!InspectorTextLayout.SkinIsAvailable()) { return null; }

            string basePath = RoadEditorUtility.GetBasePath();
            Texture2D rest = null;
            Texture2D lit = null;
            EditorUtilities.LoadTexture(ref rest, basePath + "/Editor/Icons/otherbg.png");
            EditorUtilities.LoadTexture(ref lit, basePath + "/Editor/Icons/otherbg2.png");

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.contentOffset = new Vector2(0f, 1f);
            style.normal.textColor = new Color(1f, 1f, 1f, 1f);
            style.normal.background = rest;
            style.active.background = lit;
            style.focused.background = lit;
            style.hover.background = lit;
            style.fixedHeight = 16f;
            style.fixedWidth = CommandButtonWidth;

            // Only cached once the textures actually arrived. LoadAssetFromPath can return null while the asset database
            // is still importing on a fresh open, and caching that would leave every branded button unbranded until the
            // next domain reload -- with nothing on screen to say why.
            if (rest == null || lit == null) { return style; }
            commandButton = style;
            return commandButton;
        }


        /// <summary> What "save mesh assets" costs, drawn wherever that option is offered.
        ///
        /// B-6, fault 3. The option exists twice -- once per road and once for every road in a system -- and so did the
        /// warning: the same sentence, character for character, in `RoadEditor` and in `RoadSystemEditor`, each drawn
        /// through a hand-built `GUIStyle` copy of `GUI.skin.textArea` with a red tint and a background texture loaded
        /// off disk. A sentence written twice is a sentence corrected once, and two red boxes built from two texture
        /// loads are two more things to keep looking alike than the tool can. `HelpBox` already draws a warning the way
        /// the rest of the editor draws one, and its icon is why the sentence no longer starts with the word WARNING.
        /// </summary>
        public static void SavingMeshesWarning()
        {
            EditorGUILayout.HelpBox("Saving meshes as assets is slow: it writes every generated mesh into the project, "
                + "which can add minutes to a build of a whole road system. Leave it off unless the meshes have to be "
                + "shared between scenes or kept under version control.", MessageType.Warning);
        }


        /// <summary> The "which camera gets flown" field, its fill-in-the-obvious-one button, and the note for when
        /// there is no obvious one. A road flies a camera along itself; a road system flies one through a bridge. Two
        /// features, one control, so it is drawn here rather than twice.
        ///
        /// B-6, fault 1. Both inspectors used to fill the field WHILE DRAWING -- `if (field == null) { SetSingle(); }`
        /// at the top of a repaint. Selecting the object was therefore an edit: it wrote a scene reference with no undo
        /// step behind it, dirtied the scene, and in a two-camera scene did it silently forever, because a search that
        /// declines to guess leaves the field null and the next repaint tries again. The pick is a button now, so the
        /// author asks for it and can undo it, and the search that declines to guess gets to SAY so.
        ///
        /// The value is written through the serialized property, never onto the component: that is what puts the pick in
        /// the undo stack, and it is why this takes no owner object and calls no <c>Undo.RecordObject</c> of its own --
        /// the caller's existing <c>ApplyModifiedProperties</c> is the commit. Writing the field directly and then
        /// calling <c>serializedObject.Update()</c> to catch up would also throw away every other edit the caller had
        /// pending in the same pass. </summary>
        /// <param name="_camera"> The component's own camera property -- `Road` and `RoadSystem` each have one, for
        /// their own feature. </param>
        /// <param name="_pickFailed"> What this returned last repaint: whether the author has already asked for the
        /// scene's camera and been told there isn't a single one. </param>
        /// <returns> Whether the note should still be shown next repaint. </returns>
        public static bool CameraField(SerializedProperty _camera, string _tooltip, bool _pickFailed)
        {
            if (_camera == null) { return false; }

            EditorGUILayout.PropertyField(_camera, new GUIContent("Editor play camera", _tooltip));
            if (_camera.objectReferenceValue != null) { return false; }

            if (GUILayout.Button(new GUIContent("Use scene camera",
                    "Point the field at the scene's camera. A scene with more than one camera has no obvious one to "
                    + "fly, so nothing is picked and this says so."),
                GUILayout.Width(CommandButtonWidth)))
            {
                _camera.objectReferenceValue = EngineIntegration.OnlySceneCamera();
                _pickFailed = _camera.objectReferenceValue == null;
            }

            if (_pickFailed)
            {
                EditorGUILayout.HelpBox("This scene does not have exactly one camera, so there is no obvious one to "
                    + "fly. Drag the camera to use into the field above.", MessageType.Info);
            }
            return _pickFailed;
        }
        #endregion



        #region "Foldouts drawn through the drawer"
        /// <summary> The registry's row for one field of one component, or null when the registry does not own it.
        /// One lookup, used by <see cref="Field"/>, by <see cref="Group"/> and by <see cref="Handles"/>, so "the
        /// registry knows about this field" is decided in one place. </summary>
        public static FieldDescriptor Descriptor(string componentType, string field)
        {
            if (string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(field)) { return null; }
            IList<FieldDescriptor> all = InspectorFieldRegistry.ForComponent(componentType);
            for (int i = 0; i < all.Count; i++) { if (all[i].field == field) { return all[i]; } }
            return null;
        }


        /// <summary> One registry field, drawn where the legacy inspector drew it raw, through the drawer that knows its
        /// unit and its limits. Returns false -- and draws nothing -- when the registry does not own the field, which is
        /// the signal that the inspector must keep drawing it itself.
        ///
        /// This exists beside <see cref="Group"/> because a legacy inspector holds two kinds of field. Most belong in
        /// the RM-51 foldouts and are drawn by group. A few are gated on a neighbouring toggle -- a shoulder width
        /// only means something once shoulders are switched on -- and keeping them next to the toggle that governs them
        /// is knowledge worth keeping, so those stay at their old position and come through here instead.
        ///
        /// B-5, fault 7, changed what "gated" DRAWS as. It used to mean hidden while the toggle was off, which taught an
        /// author that the road has no shoulder width at all rather than that the one it has is not in use, and took the
        /// row out of the layout so everything below it jumped. It now means DISABLED, in an
        /// <c>EditorGUI.DisabledScope</c> conditioned on the toggle: the figure stays readable, its position is stable,
        /// and the inspector stops accepting input it would then ignore. The scope belongs to the CALLER, because only the
        /// caller knows which toggle governs the field -- the registry describes a field's unit and range, not the switch
        /// that makes it relevant.
        ///
        /// <paramref name="fields"/> is the inspector's own commit buffer (B-19); it is passed straight down to the
        /// drawer, which is where the rule lives. </summary>
        public static bool Field(SerializedObject so, string componentType, string field, FocusOutFields fields)
        {
            return Field(so, componentType, field, fields, null);
        }


        /// <summary> The same field with a control of the inspector's own on the value's row -- in practice the legacy
        /// reset-to-default icon button, which the inspector still owns because the default it writes is the road's,
        /// not the registry's. </summary>
        public static bool Field(SerializedObject so, string componentType, string field, FocusOutFields fields,
            System.Action trailing)
        {
            FieldDescriptor d = Descriptor(componentType, field);
            if (d == null) { return false; }
            return InspectorFieldDrawer.Draw(so, d, fields, trailing);
        }


        /// <summary> One RM-51 foldout: the group's title, then every registry field of that group for this component,
        /// drawn through <see cref="InspectorFieldDrawer.Draw"/> so each carries its unit and its inline out-of-range
        /// message. Returns how many fields were drawn -- zero for a group this component has no fields in, in which
        /// case no foldout is drawn either. The open/closed state is <see cref="InspectorFoldoutState"/>'s, so it is
        /// remembered between selections and shared with the modern inspectors. </summary>
        public static int Group(SerializedObject so, string componentType, FieldGroup group, FocusOutFields fields)
        {
            return Group(so, componentType, group, fields, null);
        }


        /// <summary> The same foldout, minus the fields named in <paramref name="skip"/> -- the ones this inspector
        /// draws itself, at their own position, because a neighbouring toggle governs whether they appear at all. They
        /// still go through the drawer (see <see cref="Field"/>); what <paramref name="skip"/> prevents is drawing them
        /// a SECOND time here, which would leave one value under two controls in one inspector. </summary>
        public static int Group(SerializedObject so, string componentType, FieldGroup group, FocusOutFields fields,
            string[] skip)
        {
            if (so == null || string.IsNullOrEmpty(componentType)) { return 0; }

            IList<FieldDescriptor> all = InspectorFieldRegistry.ForComponent(componentType);
            ofGroup.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].group != group) { continue; }
                if (Skipped(skip, all[i].field)) { continue; }
                ofGroup.Add(all[i]);
            }
            if (ofGroup.Count == 0) { return 0; }

            bool open = InspectorFoldoutState.IsOpen(componentType, group);
            GUIStyle style = BoldFoldout();
            bool nowOpen = style == null
                ? EditorGUILayout.Foldout(open, ManualReferences.GroupText(group), true)
                : EditorGUILayout.Foldout(open, ManualReferences.GroupText(group), true, style);
            if (nowOpen != open) { InspectorFoldoutState.SetOpen(componentType, group, nowOpen); }
            if (!nowOpen) { return 0; }

            int drawn = 0;
            EditorGUI.indentLevel++;
            for (int i = 0; i < ofGroup.Count; i++)
            {
                if (InspectorFieldDrawer.Draw(so, ofGroup[i], fields)) { drawn++; }
            }
            EditorGUI.indentLevel--;
            return drawn;
        }


        /// <summary> The group's fields for the repaint in progress. Reused rather than allocated per group per
        /// repaint: an inspector repaints on mouse movement, and four short-lived lists a frame is garbage an author
        /// pays for in stutter while dragging a handle. Safe as shared state because GUI code is single-threaded and
        /// a foldout never contains another foldout. </summary>
        private static readonly List<FieldDescriptor> ofGroup = new List<FieldDescriptor>();


        /// <summary> Whether this field is one the caller draws itself. A null or empty list skips nothing. </summary>
        private static bool Skipped(string[] skip, string field)
        {
            if (skip == null) { return false; }
            for (int i = 0; i < skip.Length; i++) { if (skip[i] == field) { return true; } }
            return false;
        }


        /// <summary> Every group this component has fields in, in the order the groups are declared: what it is, what it
        /// looks like, what it does to the world, what is generated from it. </summary>
        public static int AllGroups(SerializedObject so, string componentType, FocusOutFields fields)
        {
            return AllGroups(so, componentType, fields, null);
        }


        /// <summary> Every group, minus the fields the inspector draws at their own position.
        ///
        /// B-19. <paramref name="fields"/> is threaded through here rather than left out, and leaving it out was a real
        /// fault rather than a tidiness point. The four foldouts draw the MAJORITY of a component's registry fields --
        /// only the handful gated on a neighbouring toggle come through <see cref="Field"/> -- so an AllGroups that
        /// could not reach the caller's commit buffer would have handed <see cref="InspectorFieldDrawer.Draw"/> a null
        /// one, and every field in every foldout would still have committed per keystroke while the four in-place
        /// fields did not. B-19 would have been true of the exceptions and false of the rule. </summary>
        public static int AllGroups(SerializedObject so, string componentType, FocusOutFields fields, string[] skip)
        {
            int drawn = 0;
            drawn += Group(so, componentType, FieldGroup.WhatItIs, fields, skip);
            drawn += Group(so, componentType, FieldGroup.WhatItLooksLike, fields, skip);
            drawn += Group(so, componentType, FieldGroup.WhatItDoesToTheWorld, fields, skip);
            drawn += Group(so, componentType, FieldGroup.WhatIsGeneratedFromIt, fields, skip);
            return drawn;
        }


        /// <summary> Whether the registry owns this field, and therefore whether an inspector is allowed to draw it
        /// itself. The gate asks the same question of the same table, so "the registry knows about it" means one thing
        /// for the drawer and for the scan. </summary>
        public static bool Handles(string componentType, string field)
        {
            IList<FieldDescriptor> all = InspectorFieldRegistry.ForComponent(componentType);
            for (int i = 0; i < all.Count; i++) { if (all[i].field == field) { return true; } }
            return false;
        }
        #endregion


        #region "Measured read-outs"
        /// <summary> B-5, fault 4. Open the one group a COMPUTED value is allowed to appear in, and return whether it is
        /// open.
        ///
        /// The policy is not restated here. What counts as a measured value, the single heading they appear under, and
        /// that none of them is editable are <see cref="MeasuredValuePresentation"/>'s, settled by RM-64. What was
        /// missing was anything that DREW through it: the legacy inspectors printed their computed figures as bare labels
        /// in among the editable fields, which is why "Road width: 10.0 meters" read as a field that had stopped working.
        /// This is that drawing surface, and it reads the policy's own constants, so a change there moves every inspector
        /// with it rather than leaving one of them drawing measurements its own way.
        ///
        /// Closed by default, from <see cref="InspectorFoldoutState.IsMeasuredOpen"/>: a measurement is an answer, and an
        /// author who wants one goes looking for it, whereas the four RM-51 groups are where they work. </summary>
        public static bool BeginMeasured(string componentType)
        {
            bool open = InspectorFoldoutState.IsMeasuredOpen(componentType);
            GUIStyle style = BoldFoldout();
            bool nowOpen = style == null
                ? EditorGUILayout.Foldout(open, MeasuredValuePresentation.GroupLabel, true)
                : EditorGUILayout.Foldout(open, MeasuredValuePresentation.GroupLabel, true, style);
            if (nowOpen != open) { InspectorFoldoutState.SetMeasuredOpen(componentType, nowOpen); }
            if (nowOpen) { EditorGUI.indentLevel++; }
            return nowOpen;
        }


        /// <summary> Close it. Takes what <see cref="BeginMeasured"/> returned rather than reading the store a second
        /// time: the author may have clicked the foldout during this repaint, and unwinding the indent on the NEW state
        /// instead of the drawn one leaves the whole rest of the inspector one level deep. </summary>
        public static void EndMeasured(bool wasOpen)
        {
            if (wasOpen) { EditorGUI.indentLevel--; }
        }


        /// <summary> One computed figure: its name, the value, and the unit it is in.
        ///
        /// Drawn disabled, from <see cref="MeasuredValuePresentation.IsEditable"/> -- a constant precisely so a drawer
        /// cannot pass a different answer -- and with the value in the field column rather than run on into the label, so
        /// it lines up with the editable rows above it while reading as something the tool worked out.
        ///
        /// The unit is a separate argument for the same reason <see cref="InspectorFieldDrawer"/> keeps one: a caller that
        /// concatenates "10.0" and its unit itself is how one inspector ends up spelling metres three ways. Pass null or
        /// empty when the figure carries no unit of its own to append -- a count, or a value the model has already
        /// formatted with its own unit inside it, as `Road.TerrainHistoryByteSize` does when it hands over "12 kb". In
        /// the second case the unit is still spelled in exactly one place; that place is simply the model rather than
        /// this call. </summary>
        public static void Measured(string label, string value, string unit)
        {
            using (new EditorGUI.DisabledScope(!MeasuredValuePresentation.IsEditable))
            {
                EditorGUILayout.LabelField(label, string.IsNullOrEmpty(unit) ? value : value + " " + unit);
            }
        }
        #endregion


        #region "Coalescing"
        /// <summary> Roads asked to rebuild during this editor frame. A set, so forty keystrokes on one road are one
        /// entry -- and a list of ROADS rather than of requests, because the request is made once, later. </summary>
        private static readonly List<Road> dirty = new List<Road>();

        private static bool flushArmed;

        /// <summary> Ask for a rebuild after the current GUI event, not during it.
        ///
        /// Two problems are solved by the delay and neither is cosmetic. Dragging a slider raises a change per mouse
        /// move, and each one would otherwise queue a build whose result the next one throws away -- forty builds, all
        /// but one wasted, and the author waiting for all forty. And a build started from inside <c>OnInspectorGUI</c>
        /// runs while the layout is half drawn, which is how an inspector ends up throwing about mismatched GUI groups.
        ///
        /// The queue's own supersede (RM-101) is the second half of this: a road already building when the flush arrives
        /// is asked to stop and replaced, so the panel keeps one entry per road rather than one per edit. </summary>
        public static void Rebuild(Road road)
        {
            if (road == null) { return; }
            if (!dirty.Contains(road)) { dirty.Add(road); }

            if (flushArmed) { return; }
            flushArmed = true;

            // A named method, not an anonymous delegate: -= removes a different instance than += added when the
            // delegate is anonymous, and therefore removes nothing (RM-101 removed one of those leaks already).
            EditorApplication.update -= Flush;
            EditorApplication.update += Flush;
        }


        /// <summary> Fires once, on the first editor frame after the edits stopped arriving in the current one. </summary>
        private static void Flush()
        {
            EditorApplication.update -= Flush;
            flushArmed = false;

            for (int i = 0; i < dirty.Count; i++)
            {
                Road road = dirty[i];
                if (road == null) { continue; }   // destroyed between the edit and the flush
                road.RequestUpdate();
            }
            dirty.Clear();
        }


        /// <summary> Apply what the inspector changed as ONE undo step, and rebuild only if something actually changed.
        ///
        /// One gesture, one undo step, is the contract RM-102's gate states, and it is not decoration: an edit applied to
        /// a serialised object while a worker holds a snapshot of it, then undone, can leave geometry built from values
        /// that no longer exist anywhere. <c>ApplyModifiedProperties</c> registers the whole set of changes made this
        /// frame as a single step, so the undo that follows returns the data AND -- because it triggers a rebuild the
        /// same way an edit does -- the geometry. </summary>
        public static bool ApplyAndRebuild(SerializedObject so, Road road)
        {
            if (so == null) { return false; }
            if (!so.ApplyModifiedProperties()) { return false; }
            Rebuild(road);
            return true;
        }
        #endregion
    }
}
#endif

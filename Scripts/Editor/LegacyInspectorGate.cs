#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-102's gate: proves the legacy inspector family really came onto the modern drawer, and stays
    /// there. RM-51 built the field model and the drawer; RM-66 recorded that they were wired rather than dead; but
    /// until RM-102 nothing in an author's inspector called them, so a road's lane width was still drawn by hand in
    /// one file with its own layout and its own idea of what a legal value is. The cases below are the checks that
    /// stop that from coming back.
    ///
    /// Most of this is a source scan rather than a repaint, and deliberately so. What went wrong before was never a
    /// runtime fault -- every one of those hand-drawn fields worked -- it was a file quietly growing a second control
    /// for a value that already had one. That is visible in the text and invisible at runtime, so the text is what is
    /// gated. The scan reads CODE lines only: a comment naming the old manual file, or explaining why a slider was
    /// removed, is documentation and must stay legible, not something to be worded around to keep a gate quiet.
    ///
    /// Report lines are prefixed RCMLEGACYUI. </summary>
    public static class LegacyInspectorGate
    {
        #region "The two inspectors this gate is about"
        /// <summary> The Road inspector, and the component it edits. These two files are the whole scope: the field
        /// registry covers exactly two components, so "no legacy inspector draws a raw field for a registry-known
        /// value" is a claim about these files and nowhere else. The other twelve author-facing editors draw values
        /// the registry does not know -- a spline node's magnitude, an intersection's corner radius -- and a gate that
        /// demanded they stop drawing those would be demanding they draw nothing. </summary>
        private const string RoadFile = "RoadEditor.cs";
        private const string RoadComponent = "Road";
        private const string TerrainFile = "TerrainEditor.cs";
        private const string TerrainComponent = "RoadTerrain";
        #endregion


        [MenuItem("Window/Road City Master/Testing/Legacy inspectors on the modern drawer")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.LegacyInspectorGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMLEGACYUI BEGIN\n");
            List<string> failures = new List<string>();

            CaseNoRawDrawForARegistryField(builder, failures);
            CaseEveryRegistryFieldIsDrawnExactlyOnce(builder, failures);
            CaseTheDrawerLabelsTheUnitAndNeverClamps(builder, failures);
            CaseTheHeaderComesFromTheModel(builder, failures);
            CaseNoVendorNameOrUrlInAuthorFacingCode(builder, failures);
            CaseBuildAwarenessAndCoalescing(builder, failures);
            CaseOneGestureIsOneUndoStep(builder, failures);
            CaseTheCensusCoversEveryDrawnField(builder, failures);
            CaseEveryWindowHasAWayIn(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMLEGACYUI FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMLEGACYUI result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMLEGACYUI END");

            report = builder.ToString();
            return failures.Count == 0;
        }


        #region "Case 1 -- no hand-drawn control for a value the registry owns"
        /// <summary> The central claim of RM-102. For each of the two inspectors whose component the registry covers,
        /// every remaining hand-rolled numeric control is checked against that component's registered fields: if one
        /// of them is drawn by hand, the value now has two controls in one tool -- the foldout's, which knows the
        /// field's unit and its cited limits, and the old one, which knows neither -- and whichever the author happens
        /// to use decides what the road is built from.
        ///
        /// The check resolves the ALIAS as well as the field name, because the legacy files rarely named the two
        /// together: the splat resolution was drawn through a local called <c>splatImageWidth</c> while the serialised
        /// field it wrote is <c>splatResoWidth</c>, so a scan for the field name alone read as clean while a raw
        /// IntField sat on it. The alias comes from the file's own FindProperty lines, so it cannot drift.
        ///
        /// Popups and toggles are checked alongside the numeric controls even though only the numeric ones carry a
        /// band, and the reason is the fault this task actually removed. The road's lane count was drawn by a hand-made
        /// "2 / 4 / 6 lanes" popup that wrote both directions from one choice -- so a road with three lanes forward and
        /// two back, which is what an auxiliary lane leaves behind, could not be expressed at all, and selecting from
        /// the popup silently destroyed one that had been set correctly elsewhere. A duplicate control does that
        /// whatever type it draws. </summary>
        private static void CaseNoRawDrawForARegistryField(StringBuilder builder, List<string> failures)
        {
            int raw = 0;
            raw += RawDrawScan(RoadFile, RoadComponent, failures);
            raw += RawDrawScan(TerrainFile, TerrainComponent, failures);
            builder.Append("RCMLEGACYUI ").Append("no-raw-draw".PadRight(24))
                   .Append("no registry field is drawn by hand (")
                   .Append(raw).Append(" hand-rolled numeric controls remain, all for unregistered values)\n");
        }


        /// <summary> Returns how many hand-rolled numeric controls the file still has, for the report line: the count
        /// is not itself a failure -- an inspector legitimately draws values the registry does not know -- it is there
        /// so a reviewer can see the number falling as later tasks register more of them. </summary>
        private static int RawDrawScan(string file, string component, List<string> failures)
        {
            string[] lines = CodeLines(file, failures);
            Dictionary<string, string> alias = AliasesOf(lines);
            IList<FieldDescriptor> known = InspectorFieldRegistry.ForComponent(component);
            Expect(known.Count > 0, "raw-draw: the registry knows no " + component + " fields (setup)", failures);

            int raw = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                bool numeric = IsRawNumericDraw(lines[i]);
                if (!numeric && !IsRawChoiceDraw(lines[i])) { continue; }
                if (numeric) { raw++; }
                for (int f = 0; f < known.Count; f++)
                {
                    string field = known[f].field;
                    Expect(!NamesField(lines[i], field, alias),
                        "raw-draw: " + file + ":" + (i + 1) + " draws " + component + "." + field
                        + " by hand; it is a registry field and must go through LegacyInspectorShell",
                        failures);
                }
            }
            return raw;
        }


        /// <summary> Whether this line refers to the serialised field, either by its own name or through a local the
        /// file bound to it with FindProperty. </summary>
        private static bool NamesField(string line, string field, Dictionary<string, string> alias)
        {
            if (MentionsIdentifier(line, field)) { return true; }
            foreach (KeyValuePair<string, string> pair in alias)
            {
                if (pair.Value == field && MentionsIdentifier(line, pair.Key)) { return true; }
            }
            return false;
        }
        #endregion


        #region "Case 2 -- every registry field still reaches the author"
        /// <summary> The other half of case 1, and the half that catches a deletion. Removing a hand-drawn control is
        /// only correct if something else now draws that value; remove it and forget the replacement and the field is
        /// still serialised, still built from, and no longer editable by anyone -- a road setting that exists but
        /// cannot be reached, which no compiler and no repaint will report.
        ///
        /// A field is reachable one of two ways. Either the inspector names it in a <c>LegacyInspectorShell.Field</c>
        /// call, drawing it at its own position because a neighbouring control governs whether it means anything; or
        /// one of the four foldouts draws it, which requires the file to call <c>AllGroups</c> for that component.
        /// Those two are mutually exclusive by construction: a field drawn in place is listed in the inspector's skip
        /// array so the foldout leaves it alone, and the skip array is read HERE BY REFLECTION rather than by reading
        /// the source, because the array is the thing the foldouts actually obey.
        ///
        /// That makes the two failures this case exists for exact. A field in the skip array with no Field call is
        /// drawn by nobody -- the mistake of adding a name to the array and forgetting the control. A name in the skip
        /// array that is not a registry field of that component skips nothing, so the foldout draws the field AND the
        /// in-place call draws it: one value, two controls, which is the duplication RM-102 is meant to end. </summary>
        private static void CaseEveryRegistryFieldIsDrawnExactlyOnce(StringBuilder builder, List<string> failures)
        {
            int reached = 0;
            reached += ReachabilityScan(RoadFile, RoadComponent, typeof(RoadEditor), failures);
            reached += ReachabilityScan(TerrainFile, TerrainComponent, typeof(TerrainEditor), failures);
            Expect(reached == InspectorFieldRegistry.All().Count,
                "reachable: " + reached + " of " + InspectorFieldRegistry.All().Count
                + " registry fields are drawn; the rest are serialised but unreachable", failures);
            builder.Append("RCMLEGACYUI ").Append("every-field-reachable".PadRight(24))
                   .Append("all ").Append(reached).Append(" registry fields are drawn, none twice\n");
        }


        /// <summary> The name of the skip array every converted legacy inspector declares. Named once: a second
        /// inspector added to this gate must use the same name, which is the point -- the convention is what makes the
        /// reflection above possible without the gate knowing anything about each file. </summary>
        private const string SkipArrayField = "drawnInPlace";


        private static int ReachabilityScan(string file, string component, System.Type editor, List<string> failures)
        {
            string[] lines = CodeLines(file, failures);
            string body = string.Join("\n", lines);
            bool groupsDrawn = body.Contains("AllGroups(serializedObject, \"" + component + "\"");

            Dictionary<string, bool> skipped = new Dictionary<string, bool>();
            System.Reflection.FieldInfo info = editor.GetField(SkipArrayField,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (info != null)
            {
                string[] skip = info.GetValue(null) as string[];
                if (skip != null)
                {
                    for (int i = 0; i < skip.Length; i++)
                    {
                        skipped[skip[i]] = true;
                        Expect(LegacyInspectorShell.Handles(component, skip[i]),
                            "reachable: " + file + " skips '" + skip[i] + "', which is not a registry field of "
                            + component + ", so it skips nothing and the field ends up under two controls", failures);
                    }
                }
            }
            Expect(!groupsDrawn || info != null,
                "reachable: " + file + " calls AllGroups with a skip list this gate cannot read; the array must be a "
                + "private static string[] named " + SkipArrayField, failures);

            IList<FieldDescriptor> known = InspectorFieldRegistry.ForComponent(component);
            int reached = 0;
            for (int f = 0; f < known.Count; f++)
            {
                string field = known[f].field;
                bool inPlace = body.Contains("LegacyInspectorShell.Field(serializedObject, \"" + component
                    + "\", \"" + field + "\"");
                if (inPlace) { reached++; }

                if (skipped.ContainsKey(field))
                {
                    Expect(inPlace, "reachable: " + component + "." + field + " is in " + file + "'s skip array but "
                        + "no LegacyInspectorShell.Field call draws it, so nothing does", failures);
                    continue;
                }
                if (inPlace) { continue; }

                Expect(groupsDrawn, "reachable: " + component + "." + field + " is drawn by no Field call in " + file
                    + " and the file never calls AllGroups, so nothing draws it", failures);
                if (groupsDrawn) { reached++; }
            }
            return reached;
        }
        #endregion


        #region "Case 3 -- the drawer labels the unit and never clamps"
        /// <summary> What an author gets in exchange for the hand-rolled controls going away: the unit is on the label,
        /// and a value outside the field's real-world band produces a sentence naming the limit and where it comes
        /// from, with the value left exactly as typed.
        ///
        /// Not clamping is the part worth gating. A clamp is the tempting fix -- it keeps the data always valid -- and
        /// it is wrong here for a reason the removed splat-width slider shows plainly: an author who types 400 into a
        /// slider bounded at 256 gets 256, is told nothing, and now believes the tool accepted 400. Worse, the number
        /// they meant is gone, so nobody can later ask whether 400 was reasonable. A warning leaves the author's
        /// intent on the record and leaves the decision with them, which also matters because several of these bands
        /// are design guidance rather than physical law: a 16% grade is outside AASHTO's table and is still what a
        /// mountain service road is. </summary>
        private static void CaseTheDrawerLabelsTheUnitAndNeverClamps(StringBuilder builder, List<string> failures)
        {
            string[] drawer = CodeLines("InspectorFieldDrawer.cs", failures);
            string body = string.Join("\n", drawer);

            Expect(body.Contains("d.label + \"  (\" + unit + \")\""),
                "unit-label: the drawer no longer appends the unit to the label, so numeric fields ship bare",
                failures);
            Expect(body.Contains("MessageType.Warning"),
                "no-clamp: the drawer raises no warning, so an out-of-range value is passed silently", failures);
            Expect(!body.Contains("Mathf.Clamp"),
                "no-clamp: the drawer clamps a value; RM-102 requires the author's number to survive with a warning",
                failures);
            Expect(!body.Contains("EditorGUILayout.Slider"),
                "no-clamp: the drawer uses a Slider, which clamps silently at its own bounds", failures);
            Expect(!body.Contains("EditorGUILayout.IntSlider"),
                "no-clamp: the drawer uses an IntSlider, which clamps silently at its own bounds", failures);

            // And behaviourally, on the field whose band the tool ships a default in the middle of: validation reports
            // and returns, it does not hand back a corrected number. FieldDescriptor.Validate returns a verdict and a
            // sentence and has no way to write to the property, which is the design -- this asserts the verdict is
            // actually produced for a value outside the band rather than swallowed.
            FieldDescriptor splat = Find(TerrainComponent, "splatWidth");
            Expect(splat != null, "no-clamp: RoadTerrain.splatWidth missing from the registry (setup)", failures);
            if (splat != null)
            {
                Expect(splat.range.IsBounded,
                    "no-clamp: RoadTerrain.splatWidth has no band, so removing its slider removed the only limit",
                    failures);
                ValidationResult over = splat.Validate(400.0);
                Expect(!over.ok, "no-clamp: a 400 m splat stripe was accepted without comment", failures);
                Expect(over.message != null && over.message.Contains(splat.range.source),
                    "no-clamp: the splat width message did not name the source of its limit", failures);
            }

            builder.Append("RCMLEGACYUI ").Append("unit-and-no-clamp".PadRight(24))
                   .Append("units are on the labels and out-of-range warns instead of clamping\n");
        }
        #endregion


        #region "Case 4 -- the header names this product, from the model"
        /// <summary> Every converted inspector opens with the product name, and gets it from
        /// <see cref="InspectorFieldRegistry.ProductDisplayName"/> rather than from a literal of its own. The
        /// distinction is the whole point of C-05: a literal per inspector is how a rename reaches nine files and
        /// misses two, and the two that were missed are the ones an author happens to open. </summary>
        private static void CaseTheHeaderComesFromTheModel(StringBuilder builder, List<string> failures)
        {
            string shell = string.Join("\n", CodeLines("LegacyInspectorShell.cs", failures));
            Expect(shell.Contains("InspectorFieldRegistry.ProductDisplayName"),
                "header: the shell's Header does not read the product name from the model", failures);

            string[] converted = { RoadFile, TerrainFile };
            for (int i = 0; i < converted.Length; i++)
            {
                string body = string.Join("\n", CodeLines(converted[i], failures));
                Expect(body.Contains("LegacyInspectorShell.Header("),
                    "header: " + converted[i] + " draws no product header", failures);
                Expect(!body.Contains("\"Road & City Master"),
                    "header: " + converted[i] + " carries the product name as its own literal instead of reading it "
                    + "from the model", failures);
            }
            builder.Append("RCMLEGACYUI ").Append("header-from-model".PadRight(24))
                   .Append("every converted inspector heads with the model's product string\n");
        }
        #endregion


        #region "Case 5 -- nothing author-facing names or links to the old vendor"
        /// <summary> No author-facing editor file may name the tool this one grew out of, and no code line may carry a
        /// github.com address other than the single declaration of this project's own repository.
        ///
        /// The links mattered more than the names. Before RM-102 the Help window offered a wiki, a repository and an
        /// issue tracker, all belonging to a third party -- so an author reading the manual read one describing
        /// different behaviour, and an author filing a bug filed it where nobody who could act on it would ever see
        /// it. The one address that remains is declared in EditorUtilities and read from there, so moving the
        /// repository is one edit rather than a hunt.
        ///
        /// The scan is over code lines only. A comment recording that the manual used to be called something else is
        /// the reason a reader can understand the change, and gating prose would only teach the next person to word
        /// around the gate. Documentation carries its own separate check in ToolManualGate. </summary>
        private static void CaseNoVendorNameOrUrlInAuthorFacingCode(StringBuilder builder, List<string> failures)
        {
            // Assembled from fragments rather than written whole, for the same reason ShipAuditSweeps assembles its
            // own needles that way: sweep 34 reads this file too, and a needle table spelled out in full is a scanner
            // reporting its own definitions as the breach they exist to find.
            string[] vendor = { "Road" + "Architect", "Road " + "Architect", "Micro" + "GSD", "Fritzs" + "Hero" };
            IList<InspectorRecord> rows = InspectorConsistencyChecklist.Rows();
            Expect(rows.Count > 0, "vendor: the author-facing register is empty (setup)", failures);

            int scanned = 0;
            foreach (InspectorRecord row in rows)
            {
                string[] lines = CodeLines(row.file, failures);
                if (lines.Length == 0) { continue; }
                scanned++;
                for (int i = 0; i < lines.Length; i++)
                {
                    for (int v = 0; v < vendor.Length; v++)
                    {
                        Expect(lines[i].IndexOf(vendor[v], System.StringComparison.Ordinal) < 0,
                            "vendor: " + row.file + ":" + (i + 1) + " names '" + vendor[v] + "' in code", failures);
                    }
                    if (lines[i].IndexOf("github.com", System.StringComparison.Ordinal) < 0) { continue; }
                    Expect(lines[i].Contains("ProjectRepositoryURL ="),
                        "vendor: " + row.file + ":" + (i + 1) + " carries a github address of its own; every link "
                        + "must come from EditorUtilities.ProjectRepositoryURL", failures);
                }
            }
            Expect(EditorUtilities.ProjectRepositoryURL.Contains("github.com/Phoenix-229/RoadCity_Master"),
                "vendor: the declared repository is not this project's", failures);
            builder.Append("RCMLEGACYUI ").Append("no-vendor-in-code".PadRight(24))
                   .Append(scanned).Append(" author-facing files carry no vendor name and one repository address\n");
        }
        #endregion


        #region "Case 6 -- build awareness, and one rebuild for a burst of edits"
        /// <summary> The async half. An inspector open on a road that is building must say so, must stay editable while
        /// it builds, and must turn a burst of edits into one rebuild.
        ///
        /// The source half of this case is the one that found a real fault. Every rebuild the Road inspector asked for
        /// went through <c>isUpdateRequired</c>, which the editor loop turned into <c>spline.TriggerSetup()</c> ->
        /// <c>Road.UpdateRoad()</c> -- the synchronous build, written before the queue existed and knowing nothing
        /// about it. So RM-100's dispatch and RM-101's supersede were both bypassed by the one caller that needed them
        /// most: dragging a slider ran a complete main-thread build per mouse move, each discarding the last one's
        /// result. Both spellings are gated here so neither can come back by habit.
        ///
        /// <c>Rebuild</c> must also defer through <c>EditorApplication.update</c> and unsubscribe a NAMED method.
        /// <c>-=</c> against an anonymous delegate removes a different instance than <c>+=</c> added and therefore
        /// removes nothing, which is a leak RM-101 already had to find once. </summary>
        private static void CaseBuildAwarenessAndCoalescing(StringBuilder builder, List<string> failures)
        {
            string shell = string.Join("\n", CodeLines("LegacyInspectorShell.cs", failures));
            Expect(shell.Contains("EditorApplication.update -= Flush"),
                "coalesce: the shell does not unsubscribe its flush by name", failures);
            Expect(shell.Contains("EditorApplication.update += Flush"),
                "coalesce: the shell does not defer the rebuild to the next editor frame", failures);
            Expect(!shell.Contains("update += delegate") && !shell.Contains("update += ()"),
                "coalesce: the shell subscribes an anonymous delegate, which -= can never remove", failures);
            Expect(shell.Contains("RequestUpdate()"),
                "coalesce: the shell's rebuild does not go through Road.RequestUpdate, so it misses the queue",
                failures);

            string road = string.Join("\n", CodeLines(RoadFile, failures));
            Expect(!road.Contains("isUpdateRequired = true"),
                "coalesce: " + RoadFile + " still sets isUpdateRequired, which reaches the synchronous build",
                failures);
            Expect(!road.Contains("TriggerSetup()"),
                "coalesce: " + RoadFile + " still calls TriggerSetup, which is Road.UpdateRoad run inline", failures);
            Expect(road.Contains("LegacyInspectorShell.Rebuild(road)"),
                "coalesce: " + RoadFile + " asks for no rebuild through the shell", failures);
            Expect(road.Contains("LegacyInspectorShell.BuildLine("),
                "building: " + RoadFile + " never says that the road it is showing is building", failures);
            Expect(!road.Contains("EditorGUI.BeginDisabledGroup"),
                "building: " + RoadFile + " disables its controls; RM-102 requires editing to stay enabled during a "
                + "build, with the edit coalesced instead", failures);

            // Behaviourally: the shell answers about an owner that is not building without inventing progress, and
            // tolerates the arguments an inspector really passes it -- an empty owner id before a road has one, and a
            // road destroyed between the edit and the flush.
            bool queued;
            float progress;
            Expect(!LegacyInspectorShell.Building("rcm:gate:not-a-real-owner", out queued, out progress),
                "building: an owner with no ticket was reported as building", failures);
            Expect(!queued && Mathf.Approximately(progress, 0f),
                "building: a road that is not building came back with a queue state or progress", failures);
            Expect(!LegacyInspectorShell.Building(null, out queued, out progress),
                "building: a null owner id was reported as building", failures);
            LegacyInspectorShell.Rebuild(null);
            Expect(!LegacyInspectorShell.ApplyAndRebuild(null, null),
                "coalesce: ApplyAndRebuild reported a change with no serialised object", failures);

            builder.Append("RCMLEGACYUI ").Append("build-aware-coalesced".PadRight(24))
                   .Append("the inspector shows the build, stays editable, and coalesces its edits\n");
        }
        #endregion


        #region "Case 7 -- one gesture, one undo step"
        /// <summary> Each converted inspector applies what changed exactly once per repaint, inside a
        /// <c>GUI.changed</c> guard. Two apply calls in one pass is two undo steps for one gesture, and the reason that
        /// matters is not tidiness: a build takes a snapshot of the road, so an edit that undoes in halves can leave
        /// geometry standing that was built from a combination of values the road never actually held.
        ///
        /// "Per repaint" is the whole of the claim, and counting every <c>ApplyModifiedProperties</c> in the file does
        /// not measure it. An apply inside a button handler runs when the author presses the button and at no other
        /// time, so it is a gesture in its own right and an undo step of its own is the correct outcome rather than the
        /// fault -- <c>RoadEditor.ApplyClassToRoad</c> is exactly that, and it has to flush there, because this editor
        /// reads its properties at the top of <c>OnInspectorGUI</c> and writes them back at the end, so a direct field
        /// write in between would be overwritten by that write-back. Counting it as a second per-repaint apply asks for
        /// that flush to be deleted, which would reintroduce the bug the flush is there to prevent.
        ///
        /// So the applies are classified rather than totalled: one reached from a <c>GUI.changed</c> guard, which is the
        /// repaint path and must be single, and any others, which must each name their own undo step within a line or
        /// two of the flush. That second requirement is what keeps this from being a hole -- a stray apply cannot be
        /// dropped into a helper to escape the count, because an apply that records no undo entry fails the case
        /// wherever it sits. </summary>
        private static void CaseOneGestureIsOneUndoStep(StringBuilder builder, List<string> failures)
        {
            string[] converted = { RoadFile, TerrainFile };
            int commands = 0;
            for (int i = 0; i < converted.Length; i++)
            {
                string[] lines = CodeLines(converted[i], failures);
                int onRepaintPath = 0;
                for (int l = 0; l < lines.Length; l++)
                {
                    if (!lines[l].Contains("ApplyModifiedProperties()")) { continue; }

                    bool guarded = false;
                    for (int back = l; back >= 0 && back > l - 60; back--)
                    {
                        if (lines[back].Contains("if (GUI.changed)")) { guarded = true; break; }
                    }
                    if (guarded) { onRepaintPath++; continue; }

                    // Not on the repaint path: it has to be a command, and a command is a thing with an undo entry.
                    bool recordsUndo = false;
                    for (int on = l; on < lines.Length && on < l + 6; on++)
                    {
                        if (lines[on].Contains("Undo.Record")) { recordsUndo = true; break; }
                    }
                    Expect(recordsUndo, "undo: " + converted[i] + " applies its changes on line "
                        + (l + 1) + ", outside any GUI.changed guard and with no"
                        + " Undo.Record beside it, so that write belongs to whatever undo step happens to be open",
                        failures);
                    if (recordsUndo) { commands++; }
                }
                Expect(onRepaintPath == 1, "undo: " + converted[i] + " applies its changes " + onRepaintPath
                    + " times per repaint; one gesture must be one undo step", failures);
            }
            builder.Append("RCMLEGACYUI ").Append("one-gesture-one-undo".PadRight(24))
                   .Append("each converted inspector applies its edits once when something changed, plus ")
                   .Append(commands)
                   .Append(" command flushes that record their own undo step\n");
        }
        #endregion


        #region "Case 8 -- the census still describes what is drawn"
        /// <summary> RM-50's census is the record of the serialised authoring surface, and RM-102 moved controls
        /// around on top of it. Every field the tool now draws must still appear there, so the document an author or a
        /// reviewer reads describes the inspector they actually see rather than the one before the conversion. </summary>
        private static void CaseTheCensusCoversEveryDrawnField(StringBuilder builder, List<string> failures)
        {
            string path = Path.Combine(
                Path.GetDirectoryName(InspectorConsistencyDocWriter.ChecklistPath()), "FieldCensus.md");
            if (!File.Exists(path))
            {
                failures.Add("census: '" + path + "' does not exist, so nothing records what these fields are");
                builder.Append("RCMLEGACYUI ").Append("census-covers-fields".PadRight(24))
                       .Append("the census is missing\n");
                return;
            }

            string census = File.ReadAllText(path);
            int covered = 0;
            foreach (FieldDescriptor d in InspectorFieldRegistry.All())
            {
                if (census.IndexOf(d.field, System.StringComparison.Ordinal) >= 0) { covered++; continue; }
                failures.Add("census: " + d.component + "." + d.field
                    + " is drawn by an inspector but appears nowhere in FieldCensus.md");
            }
            builder.Append("RCMLEGACYUI ").Append("census-covers-fields".PadRight(24))
                   .Append(covered).Append(" of ").Append(InspectorFieldRegistry.All().Count)
                   .Append(" drawn fields are described in the census\n");
        }
        #endregion


        #region "Case 9 -- every window this tool ships has a way in (B-14)"
        /// <summary> An EditorWindow nobody can open is a stub with extra steps, and this is the check that says so.
        ///
        /// B-14's test, in the author's words, is whether they *can tell this code exists at all*. HelpWindow failed
        /// it. RM-108 deleted that window's menu item on the recorded grounds that the RoadSystem inspector already
        /// offered a "Quick help" button beside "Manual" -- it did not; nothing in the tool called
        /// <c>EditorMenu.ShowHelpWindow</c>, and no other file opened the type. So a whole window shipped with no way
        /// in, and nothing complained, because a window that is never opened cannot throw.
        ///
        /// The second hop is what this case is really for. HelpWindow's type WAS named outside its own file, in its
        /// opener, so "is it referenced anywhere" read as clean the whole time it was unreachable. What was missing was
        /// a caller for the opener, one hop further out, which is why the rows below record both.
        ///
        /// The list is hard-coded and its completeness is asserted, which is the same bargain <see
        /// cref="InspectorStyleOwnershipGate"/> makes with its caller list: a fourth window fails this case until
        /// somebody writes down how an author reaches it, and writing that down is the point. </summary>
        private static void CaseEveryWindowHasAWayIn(StringBuilder builder, List<string> failures)
        {
            // window, the file whose code opens it, and -- where the opener is a plain static that Unity does not draw
            // by itself -- the call that has to reach that opener from somewhere else.
            string[][] ways = new string[][]
            {
                new string[] { "HelpWindow", "EditorMenu.cs", "RoadSystemEditor.cs", "EditorMenu.ShowHelpWindow(" },
                new string[] { "SaveWindow", "SplineNEditor.cs", null, null },
                new string[] { "Wizard", "SplineNEditor.cs", null, null }
            };

            List<string> declared = WindowFilesInEditorFolder(failures);
            Expect(declared.Count == ways.Length,
                "way-in: the editor folder declares " + declared.Count.ToString() + " EditorWindow types ("
                + string.Join(", ", declared.ToArray()) + ") and this case records " + ways.Length.ToString()
                + ". Add the new one with the way an author opens it, or it ships unreachable as HelpWindow did",
                failures);

            int reachable = 0;
            for (int w = 0; w < ways.Length; w++)
            {
                if (WayInto(ways[w], failures)) { reachable++; }
            }

            builder.Append("RCMLEGACYUI ").Append("window-has-a-way-in".PadRight(24))
                   .Append(reachable).Append(" of ").Append(ways.Length)
                   .Append(" shipped windows can be opened from the tool's own UI\n");
        }


        /// <summary> The EditorWindow types this tool declares, read from the editor folder rather than listed, so the
        /// completeness check above is an assertion about the folder and not about this file agreeing with itself.
        /// Comments are blanked first: the prose in these gates names window types constantly. </summary>
        private static List<string> WindowFilesInEditorFolder(List<string> failures)
        {
            List<string> found = new List<string>();
            string folder = InspectorConsistencyDocWriter.EditorScriptFolder();
            if (!Directory.Exists(folder)) { return found; }

            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            for (int f = 0; f < files.Length; f++)
            {
                string[] lines = CodeLines(Path.GetFileName(files[f]), failures);
                for (int i = 0; i < lines.Length; i++)
                {
                    int at = lines[i].IndexOf(": EditorWindow", System.StringComparison.Ordinal);
                    if (at < 0) { continue; }
                    int classAt = lines[i].IndexOf("class ", System.StringComparison.Ordinal);
                    if (classAt < 0 || classAt > at) { continue; }

                    string name = lines[i].Substring(classAt + 6, at - classAt - 6).Trim();
                    if (name.Length > 0 && name.IndexOf(' ') < 0) { found.Add(name); }
                }
            }
            found.Sort();
            return found;
        }


        /// <summary> Whether one recorded way in still exists. Two source reads, because the fault being caught is a
        /// file that stopped CALLING something -- which compiles, runs, and cannot be seen at runtime by anything short
        /// of an author looking for a button that is not there. </summary>
        private static bool WayInto(string[] way, List<string> failures)
        {
            string window = way[0];
            string openerFile = way[1];
            string callerFile = way[2];
            string call = way[3];

            string[] opener = CodeLines(openerFile, failures);
            bool opens = false;
            for (int i = 0; i < opener.Length; i++)
            {
                if (opener[i].IndexOf("GetWindow<" + window + ">", System.StringComparison.Ordinal) >= 0)
                {
                    opens = true;
                    break;
                }
            }
            Expect(opens, "way-in: " + openerFile + " no longer opens " + window
                + ", so nothing in the tool opens it and the window ships unreachable", failures);

            // A window opened from inside another Editor or EditorWindow is reached because Unity draws that class.
            // Only an opener Unity does NOT draw by itself needs a caller, and that is the row that records one.
            if (callerFile == null) { return opens; }

            string[] caller = CodeLines(callerFile, failures);
            bool called = false;
            for (int i = 0; i < caller.Length; i++)
            {
                if (caller[i].IndexOf(call, System.StringComparison.Ordinal) >= 0) { called = true; break; }
            }
            Expect(called, "way-in: " + callerFile + " no longer calls " + call + "), so " + window
                + " has an opener that nothing reaches -- exactly the state RM-108 left it in", failures);

            return opens && called;
        }
        #endregion


        #region "Reading a source file the way a compiler would, not the way grep does"
        /// <summary> One source file of the editor folder, with its comments blanked.
        ///
        /// Blanked, not removed: a case that reports "RoadEditor.cs:412 draws laneWidth by hand" is only useful if 412
        /// is the line an author will find it on, and deleting lines renumbers everything after the first comment.
        ///
        /// Comments have to go because the cases below search for spellings that a comment is allowed to contain, and
        /// gating prose would teach the next person to word around the gate rather than fix the code. Case 6 forbids
        /// <c>TriggerSetup()</c> in the Road inspector, and the doc comment that explains WHY it is forbidden names it
        /// four times. Case 5 forbids the old vendor name, and the one honest use of it left in the tool is a comment
        /// recording which manual filename the shipped one replaced.
        ///
        /// The trailing-comment cut is the part with a trap in it, and it is Case 5's trap specifically. Cutting a line
        /// at the first <c>//</c> would cut <c>"https://github.com/..."</c> in half, blinding the one case whose whole
        /// job is to read repository addresses. So the cut only happens at a <c>//</c> found outside a string or char
        /// literal, which means tracking quotes and their backslash escapes. A block comment opened after code on the
        /// same line is not tracked; its continuation lines begin with <c>*</c> and are blanked by the rule above,
        /// which leaves the opening line's code visible -- under-stripping, which can only cost a false positive that
        /// names its own file and line, never a missed fault. </summary>
        private static string[] CodeLines(string file, List<string> failures)
        {
            string path = Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), file);
            if (!File.Exists(path))
            {
                failures.Add("source: '" + path + "' does not exist, so nothing about it could be checked");
                return new string[0];
            }

            string[] lines = File.ReadAllLines(path);
            bool inBlock = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (inBlock)
                {
                    if (line.IndexOf("*/", System.StringComparison.Ordinal) >= 0) { inBlock = false; }
                    lines[i] = "";
                    continue;
                }
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*"))
                {
                    lines[i] = "";
                    continue;
                }
                if (trimmed.StartsWith("/*"))
                {
                    if (line.IndexOf("*/", System.StringComparison.Ordinal) < 0) { inBlock = true; }
                    lines[i] = "";
                    continue;
                }
                lines[i] = WithoutTrailingComment(line);
            }
            return lines;
        }


        /// <summary> The line up to its trailing <c>//</c>, where that <c>//</c> is really a comment and not the middle
        /// of a URL or a path inside a string. </summary>
        private static string WithoutTrailingComment(string line)
        {
            bool inString = false;
            bool inChar = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') { inString = false; }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') { inChar = false; }
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') { return line.Substring(0, i); }
            }
            return line;
        }


        /// <summary> Which local variable in this file stands for which serialised field, read out of the
        /// <c>FindProperty</c> calls that create them.
        ///
        /// This map is why Case 1 can see a fault that a search for field names cannot. The splat resolution was drawn
        /// through a local called <c>splatImageWidth</c>, while the serialised field it writes is
        /// <c>splatResoWidth</c>: a scan for the registry's field names read that file as clean while a raw IntField
        /// sat squarely on a registry value. The alias is the link between the two names.
        ///
        /// A local with a very generic name -- <c>prop</c> in a one-line reset helper -- becomes an alias too, and in
        /// principle could match an unrelated line. It has not here, and the cost of it happening is one failure that
        /// names its own file and line, against the cost of not resolving aliases at all, which is a whole class of
        /// fault going unseen. </summary>
        private static Dictionary<string, string> AliasesOf(string[] lines)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            for (int i = 0; i < lines.Length; i++)
            {
                int call = lines[i].IndexOf("FindProperty(\"", System.StringComparison.Ordinal);
                if (call < 0) { continue; }
                int assign = lines[i].IndexOf('=');
                if (assign < 0 || assign > call) { continue; }

                int open = call + "FindProperty(\"".Length;
                int close = lines[i].IndexOf('"', open);
                if (close < 0) { continue; }
                string field = lines[i].Substring(open, close - open);
                if (field.Length == 0) { continue; }

                string left = lines[i].Substring(0, assign).Trim();
                int split = left.LastIndexOfAny(new char[] { ' ', '\t', '.', '[' });
                string local = split >= 0 ? left.Substring(split + 1) : left;
                if (local.Length == 0) { continue; }
                map[local] = field;
            }
            return map;
        }


        /// <summary> The spellings by which a number gets drawn by hand. Sliders are in the list because a slider is
        /// the worst of them: it clamps in silence, so an author who types 400 into a 256-limited field gets 256 and no
        /// explanation of where their number went. </summary>
        private static readonly string[] numericDraws =
        {
            "IntField(", "FloatField(", "LongField(", "DoubleField(",
            "Slider(", "IntSlider(", "DelayedFloatField(", "DelayedIntField("
        };


        /// <summary> Whether this line draws a number itself instead of handing it to the drawer. </summary>
        private static bool IsRawNumericDraw(string line)
        {
            return IsRawDraw(line, numericDraws);
        }


        /// <summary> The spellings by which a choice -- an enum, or a bool -- gets drawn by hand. Deliberately does NOT
        /// include a bare <c>Field(</c>: <c>LegacyInspectorShell.Field(...)</c> is the correct call and naming a
        /// registry field beside it is exactly what the fixed code does. </summary>
        private static readonly string[] choiceDraws =
        {
            "Popup(", "EnumPopup(", "EnumFlagsField(", "Toggle(", "ToggleLeft("
        };


        /// <summary> Whether this line draws an enum or a bool itself instead of handing it to the drawer. </summary>
        private static bool IsRawChoiceDraw(string line)
        {
            return IsRawDraw(line, choiceDraws);
        }


        /// <summary> A control drawn by this file rather than by the drawer: one of the given spellings, on a line that
        /// really is calling Unity's immediate-mode GUI. The prefix test is what keeps
        /// <c>LegacyInspectorShell.Field(serializedObject, "Road", "laneWidth", ...)</c> -- the fixed form, which names
        /// a registry field on purpose -- from reading as the fault it replaced. </summary>
        private static bool IsRawDraw(string line, string[] spellings)
        {
            if (line.IndexOf("EditorGUILayout.", System.StringComparison.Ordinal) < 0
                && line.IndexOf("EditorGUI.", System.StringComparison.Ordinal) < 0) { return false; }
            for (int i = 0; i < spellings.Length; i++)
            {
                if (line.IndexOf(spellings[i], System.StringComparison.Ordinal) >= 0) { return true; }
            }
            return false;
        }


        /// <summary> Whether <paramref name="name"/> appears on this line as a whole identifier. Substring matching
        /// would make every check on <c>medianWidth</c> also fire on <c>medianWidthOverride</c>, and a gate that
        /// reports faults in fields that do not exist gets switched off. </summary>
        private static bool MentionsIdentifier(string line, string name)
        {
            if (string.IsNullOrEmpty(name)) { return false; }
            int at = 0;
            while (at <= line.Length - name.Length)
            {
                at = line.IndexOf(name, at, System.StringComparison.Ordinal);
                if (at < 0) { return false; }
                int after = at + name.Length;
                bool leftFree = at == 0 || !IsIdentifierChar(line[at - 1]);
                bool rightFree = after >= line.Length || !IsIdentifierChar(line[after]);
                if (leftFree && rightFree) { return true; }
                at = after;
            }
            return false;
        }


        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }


        /// <summary> The registry's row for one field, through the shell's own lookup rather than a second copy of it,
        /// so the gate cannot pass because it asks a different table than the drawer reads. </summary>
        private static FieldDescriptor Find(string component, string field)
        {
            return LegacyInspectorShell.Descriptor(component, field);
        }


        /// <summary> Record a failure without stopping: one run should report everything that is wrong, not the first
        /// thing. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
        #endregion
    }
}
#endif

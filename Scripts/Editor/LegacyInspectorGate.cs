// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary> The RoadSystem inspector. It is not in the registry's scope -- the values it draws are the system's
        /// own build options, which no field descriptor covers -- so it is absent from the cases above and named here
        /// instead, for the two cases that are about how an inspector behaves rather than which values it owns: the
        /// colon rule, and B-6's "an inspector does not edit the scene while it draws". </summary>
        private const string SystemFile = "RoadSystemEditor.cs";

        /// <summary> The opening of the saving-meshes warning, assembled from two halves so that THIS file is not itself
        /// one of the copies it counts. Comments are blanked before a file is read, but a string literal is not, and a
        /// gate that fails because of its own search term would be its own worst false positive. </summary>
        private static readonly string SavingMeshesSentence = "Saving meshes as assets is slow"
            + ": it writes every generated mesh into the project";

        /// <summary> The name of the deleted camera setter, in halves for the same reason: a scan for it that matched its
        /// own failure message would report this gate file as the fault. </summary>
        private static readonly string DeletedCameraSetter = "EditorCameraSet" + "Single";
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
            CaseInputsCommitOnFocusOut(builder, failures);
            CaseNoControlLabelEndsInAColon(builder, failures);
            CaseTheSystemInspectorDoesNotEditWhileItDraws(builder, failures);

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


        #region "Case 3 -- the drawer puts the unit in its own column, describes the field, and never clamps"
        /// <summary> What an author gets in exchange for the hand-rolled controls going away: the unit stands in a column
        /// of its own beside the value, the field's own sentence is on the hover, and a value outside the field's
        /// real-world band produces a sentence naming the limit and where it comes from, with the value left exactly as
        /// typed.
        ///
        /// B-5 changed the first of those three and this case with it. The unit used to be appended to the prefix label
        /// (`"Lane width  (m)"`), which is what this gate asserted; fault 5 is that a unit inside the label competes with
        /// the field's name for the label column and pushes the longest labels onto a second line. So the assertion is
        /// inverted rather than deleted: the composition must be GONE from the drawer, and the column must be there
        /// instead. Deleting it would have left the tool free to drift back to a bare number with nothing to catch it.
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

            Expect(!body.Contains("d.label + \"  (\" + unit + \")\""),
                "unit-column: the drawer has put the unit back inside the prefix label, which is the overflow B-5 "
                + "fault 5 removed", failures);
            Expect(body.Contains("DrawUnit(d)"),
                "unit-column: the drawer draws no unit cell, so numeric fields ship bare", failures);
            Expect(body.Contains("InspectorTextLayout.UnitColumnWidth()"),
                "unit-column: the unit cell is not sized from the one shared column width, so the tokens will not line "
                + "up down the inspector", failures);
            Expect(body.Contains("new GUIContent(d.label, d.tooltip)"),
                "tooltip: the drawer does not put the descriptor's sentence on the row's content, so fault 8's hover "
                + "text never reaches an author", failures);
            Expect(body.Contains("MessageType.Warning"),
                "no-clamp: the drawer raises no warning, so an out-of-range value is passed silently", failures);
            Expect(!body.Contains("Mathf.Clamp"),
                "no-clamp: the drawer clamps a value; RM-102 requires the author's number to survive with a warning",
                failures);
            Expect(!body.Contains("EditorGUILayout.Slider"),
                "no-clamp: the drawer uses a Slider, which clamps silently at its own bounds", failures);
            Expect(!body.Contains("EditorGUILayout.IntSlider"),
                "no-clamp: the drawer uses an IntSlider, which clamps silently at its own bounds", failures);

            // B-5, fault 8. Every registered field carries a hover sentence. Asserted here, in the case that owns what a
            // field row gives an author, because the tooltip arrives on the same GUIContent as the label: a row whose
            // descriptor shipped without one looks identical to a row whose drawer dropped it.
            //
            // The length floor is what stops a stub. `40` is AUTHORED (C-15): a tooltip has to be a SENTENCE about what
            // the value does, and no useful one is shorter than that -- the shortest row in the registry runs to 120
            // characters. A word, a repeated label, or a `TODO` fails, which is the only failure mode a machine can
            // actually tell apart from prose.
            IList<FieldDescriptor> registered = InspectorFieldRegistry.All();
            for (int i = 0; i < registered.Count; i++)
            {
                FieldDescriptor d = registered[i];
                Expect(d.tooltip != null && d.tooltip.Length >= 40,
                    "tooltip: " + d.component + "." + d.field + " has no hover sentence, so its row tells an author "
                    + "what the field is called and nothing about what it does", failures);
            }

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
                   .Append("units are in their own column, every field is described, out-of-range warns\n");
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
            // B-5, fault 7, narrowed this. The rule was a blanket ban on disabling anything, written when the only
            // reason a control had ever been disabled here was that the road was building -- and that reason is still
            // wrong: an author who spots a mistake mid-build must be able to correct it, with the edit coalesced.
            // B-5 needed the OTHER half, which the blanket ban forbade: a field governed by a toggle -- a shoulder
            // width, a grade ceiling -- was HIDDEN when its toggle was off, so switching the toggle made rows appear
            // and shift everything under them, and an author could not read what the road already had without turning
            // the feature on. Greyed, the value stays legible with its unit and its band, and the toggle above it
            // reads as what governs it. So the ban is now on the CONDITION, not on the act: a scope may be opened on
            // this road's own state and never on the state of a build.
            Expect(!road.Contains("EditorGUI.BeginDisabledGroup"),
                "building: " + RoadFile + " opens a disabled group by hand; use `using (new EditorGUI.DisabledScope"
                + "(...))`, which cannot leak if the body returns or throws", failures);

            string[] roadLines = CodeLines(RoadFile, failures);
            int scopes = 0;
            for (int i = 0; i < roadLines.Length; i++)
            {
                Match m = Regex.Match(roadLines[i], @"new EditorGUI\.DisabledScope\(([^)]*)\)");
                if (!m.Success) { continue; }
                scopes++;
                string condition = m.Groups[1].Value;
                Expect(condition.Contains("road.is"),
                    "building: " + RoadFile + " line " + (i + 1) + " disables a control on '" + condition
                    + "', which is not a state of the road this inspector is editing", failures);
                for (int b = 0; b < buildStateTokens.Length; b++)
                {
                    Expect(!condition.Contains(buildStateTokens[b]),
                        "building: " + RoadFile + " line " + (i + 1) + " disables a control while the road is building"
                        + " ('" + buildStateTokens[b] + "'); RM-102 requires editing to stay enabled during a build,"
                        + " with the edit coalesced instead", failures);
                }
            }
            Expect(scopes > 0,
                "building: " + RoadFile + " disables nothing at all, so B-5's toggle-governed fields are hidden again",
                failures);

            // The two fields B-5 named, asserted positively: each is inside a scope governed by its own toggle, so
            // neither can be quietly put back behind an `if` that hides the row. The field call is matched by the same
            // fixed form case 2 counts, which is why that call is left unchanged to the byte at both sites.
            Expect(DisabledScopeGuards(roadLines, "!road.isShouldersEnabled", "\"shoulderWidth\""),
                "disabled: the shoulder width is not drawn greyed under its own toggle; a field an author cannot read "
                + "is B-5 fault 7 again", failures);
            Expect(DisabledScopeGuards(roadLines, "!road.isMaxGradeEnabled", "\"maxGrade\""),
                "disabled: the grade ceiling is not drawn greyed under its own toggle; a field an author cannot read "
                + "is B-5 fault 7 again", failures);

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

                    // B-19. Matched on the guard's OPENING, `if (GUI.changed`, and deliberately not on the whole of
                    // the old `if (GUI.changed)`. Once a value commits on focus-out instead of per keystroke, no Unity
                    // control reports a change on the frame the commit lands, so GUI.changed is false there and every
                    // repaint guard in the tool had to become `if (GUI.changed || fields.Changed)`. Matching the
                    // closing bracket made this case read those guards as ABSENT, classify the repaint flush as a
                    // stray command, and then demand an Undo.Record beside a flush that already takes its undo step
                    // from the guard -- so the case failed on the fix rather than on a fault.
                    //
                    // Whether the `fields.Changed` half is actually present is case 10's assertion
                    // (`CommitBufferThreading`), not this one's, so that requirement lives in exactly one place.
                    bool guarded = false;
                    for (int back = l; back >= 0 && back > l - 60; back--)
                    {
                        if (lines[back].Contains("if (GUI.changed")) { guarded = true; break; }
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


        #region "Case 10 -- a typed or dragged value commits when the author is done, not per keystroke (B-19)"
        /// <summary> B-19's gate. An author typing 3500 into a road's length used to build the road at 3, then at 35,
        /// then at 350, then at 3500 -- four rebuilds, four undo steps, and three of them from a number nobody meant.
        /// Worse, each rebuild ran <c>serializedObject.Update()</c> on the way out, which re-read the property and
        /// replaced the half-typed text under the cursor, so a slow typist could not finish the number at all.
        ///
        /// The fix is <see cref="FocusOutFields"/>: a typed value reaches the property on Enter or focus-out, a dragged
        /// handle on mouse-up, and the control shows what the author is typing in the meantime. This case is what keeps
        /// it that way, and it gates four separate things, because there are four separate ways to lose it.
        ///
        /// The first is the obvious one: a new raw <c>FloatField</c> or <c>Slider</c> appearing in an inspector, which
        /// is a per-keystroke control again no matter what the rest of the file does. That is a per-file budget with an
        /// allowance table, and the table is the record of every deliberate exception -- see <see cref="allowedRawInput"/>.
        ///
        /// The second is the subtle one, and it is the one that cost a session to find. On the frame a deferred control
        /// commits, NO Unity control reported a change, so <c>GUI.changed</c> is false. An inspector whose apply gate
        /// reads <c>if (GUI.changed)</c> therefore skips <c>ApplyModifiedProperties()</c> on precisely the frame that
        /// had something to apply, and the next <c>Update()</c> reads the old value back out of the object -- the
        /// author's number is accepted, discarded, and the control shows the discard as though they had mistyped. The
        /// gate demands that any <c>GUI.changed</c> guard wrapping an apply also test <c>fields.Changed</c>.
        ///
        /// The third is a buffer that never gets a frame boundary: <see cref="FocusOutFields.BeginFrame"/> is what
        /// clears the previous pass's verdict, and without it <c>Changed</c> latches true and every repaint applies.
        ///
        /// The fourth is reaching <see cref="InspectorFieldDrawer"/> without a buffer at all. The drawer falls back to
        /// Unity's own <c>PropertyField</c> when handed a null one -- correct as a fallback, and a silent reversal of
        /// this whole task if a real call site ever takes it. </summary>
        private static void CaseInputsCommitOnFocusOut(StringBuilder builder, List<string> failures)
        {
            int exceptions = RawInputBudget(failures);
            int threaded = CommitBufferThreading(failures);
            DrawerAlwaysGetsACommitBuffer(failures);

            // Changed is the verdict every apply gate above is now guarded by, so nothing outside the buffer may set
            // it. A public setter would let a caller "fix" a gate failure by asserting the commit happened.
            System.Reflection.PropertyInfo changed = typeof(FocusOutFields).GetProperty("Changed");
            Expect(changed != null, "commit: FocusOutFields has no public Changed property (setup)", failures);
            if (changed != null)
            {
                System.Reflection.MethodInfo setter = changed.GetSetMethod(true);
                Expect(setter == null || !setter.IsPublic,
                    "commit: FocusOutFields.Changed has a public setter, so a caller can claim a commit that no "
                    + "control made and every apply gate in the tool would believe it", failures);
            }

            // And behaviourally, on a fresh buffer: nothing has committed, so nothing may be applied. BeginFrame is
            // the frame boundary, and a boundary that leaves the verdict standing is the latch described above.
            FocusOutFields probe = new FocusOutFields();
            Expect(!probe.Changed, "commit: a FocusOutFields reports a commit before any control has drawn", failures);
            probe.BeginFrame();
            Expect(!probe.Changed, "commit: BeginFrame does not clear the previous pass's verdict, so Changed latches "
                + "true and every later repaint applies whether or not anything committed", failures);

            builder.Append("RCMLEGACYUI ").Append("commit-on-focus-out".PadRight(24))
                   .Append(threaded).Append(" inspectors buffer their edits to focus-out, with ")
                   .Append(exceptions).Append(" recorded immediate controls in ")
                   .Append(allowedRawInput.Length).Append(" files\n");
        }
        /// <summary> Every immediate control this tool ships on purpose, with the file it lives in and how many of them
        /// that file is allowed. Both directions are asserted: a file with more raw inputs than its row fails, and so
        /// does a file with fewer, because the row IS the decision and a decision nobody recorded reversing should not
        /// evaporate quietly. A file absent from the table must have none at all.
        ///
        /// Three rows, and each is a judgement rather than an oversight:
        ///
        /// FocusOutFields.cs is the implementation. Its two raw text fields ARE the deferred control -- the box the
        /// author types into while the property still holds the old value. Gating them would be gating the fix.
        ///
        /// RoadSystemEditor.cs frames the editor's play camera on a bridge: zoom, height, and two rotation axes. They
        /// are fields on the Editor instance, not on the RoadSystem -- nothing serialises them, no undo step is written,
        /// no road is rebuilt, and the only thing they move is where the camera stands. Deferring them would mean
        /// dragging a zoom slider blind and seeing the frame jump once when the button came up, which is worse at the
        /// only job those four controls have.
        ///
        /// SaveWindow.cs is the save dialog: a display name and a description. The name feeds a live readout of the
        /// exact path about to be written and a warning when a file is already there, both of which exist to answer
        /// "what am I about to save, and over what" BEFORE the button is pressed. Deferred, they would answer about the
        /// previous name. Nothing here is serialised, applied, or rebuilt either. </summary>
        private static readonly string[][] allowedRawInput = new string[][]
        {
            new string[] { "FocusOutFields.cs", "2" },
            new string[] { "RoadSystemEditor.cs", "4" },
            new string[] { "SaveWindow.cs", "2" }
        };


        /// <summary> Counts the raw typed-or-dragged controls in every editor source file and checks each against the
        /// table. Returns how many recorded exceptions were found, for the report line.
        ///
        /// The whole folder is scanned rather than the author-facing register, and that is deliberate: the register is
        /// a hand-kept list, and the fault this case exists to catch is a control appearing in a file nobody thought to
        /// add to a list. </summary>
        private static int RawInputBudget(List<string> failures)
        {
            string folder = InspectorConsistencyDocWriter.EditorScriptFolder();
            if (!Directory.Exists(folder))
            {
                failures.Add("commit: '" + folder + "' does not exist, so no inspector could be scanned");
                return 0;
            }

            int recorded = 0;
            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            for (int f = 0; f < files.Length; f++)
            {
                string name = Path.GetFileName(files[f]);
                string[] lines = CodeLines(name, failures);

                int count = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!IsRawInputDraw(lines[i])) { continue; }
                    count++;
                    if (Allowance(name) >= 0) { continue; }
                    failures.Add("commit: " + name + ":" + (i + 1) + " draws a value the author types or drags with "
                        + "Unity's own control, which commits per keystroke. Draw it through the file's FocusOutFields, "
                        + "or record the exception in LegacyInspectorGate.allowedRawInput with the reason");
                }

                int allowed = Allowance(name);
                if (allowed < 0) { continue; }
                recorded += count;
                Expect(count == allowed, "commit: " + name + " is recorded as having " + allowed
                    + " immediate controls on purpose and now has " + count
                    + "; update the row in allowedRawInput, with the reason, or put the value back on focus-out",
                    failures);
            }

            for (int a = 0; a < allowedRawInput.Length; a++)
            {
                Expect(File.Exists(Path.Combine(folder, allowedRawInput[a][0])),
                    "commit: allowedRawInput records an exception in '" + allowedRawInput[a][0]
                    + "', which no longer exists", failures);
            }
            return recorded;
        }


        /// <summary> How many immediate controls this file is allowed, or -1 when it is allowed none. </summary>
        private static int Allowance(string file)
        {
            for (int a = 0; a < allowedRawInput.Length; a++)
            {
                if (allowedRawInput[a][0] != file) { continue; }
                int allowed;
                return int.TryParse(allowedRawInput[a][1], out allowed) ? allowed : -1;
            }
            return -1;
        }
        /// <summary> For every inspector that owns a commit buffer: it must give that buffer a frame boundary, and any
        /// <c>GUI.changed</c> guard it wraps an apply in must also test the buffer's verdict. Returns how many
        /// inspectors were checked.
        ///
        /// The guard check is written as "every GUI.changed guard that wraps an apply" rather than "the apply gate",
        /// because there is no way to tell from the source which one an author would call the gate -- and no need to.
        /// If a guard wraps an apply, a commit frame must be able to get through it; that is true of the first one and
        /// of any later one somebody adds. A guard that wraps only <c>EditorUtility.SetDirty</c> -- which is what the
        /// scene-view handlers in these files have -- wraps no apply and is left alone, because missing a SetDirty on a
        /// commit frame loses nothing: the apply on the same frame dirties the object itself. </summary>
        private static int CommitBufferThreading(List<string> failures)
        {
            string folder = InspectorConsistencyDocWriter.EditorScriptFolder();
            if (!Directory.Exists(folder)) { return 0; }

            int scanned = 0;
            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            for (int f = 0; f < files.Length; f++)
            {
                string name = Path.GetFileName(files[f]);
                // This file constructs a buffer of its own, a few lines up, to prove the mechanism starts clean and
                // that BeginFrame clears it. That probe is a test fixture, not an inspector: it has no repaint, no
                // apply, and no author looking at it.
                if (name == "LegacyInspectorGate.cs") { continue; }

                string[] lines = CodeLines(name, failures);
                string body = string.Join("\n", lines);
                if (!body.Contains("new FocusOutFields()")) { continue; }
                scanned++;

                Expect(body.Contains("fields.BeginFrame()"),
                    "commit: " + name + " owns a FocusOutFields and never calls BeginFrame, so its verdict is never "
                    + "cleared: Changed latches true at the first commit and every repaint after it applies", failures);

                for (int l = 0; l < lines.Length; l++)
                {
                    if (lines[l].IndexOf("if (GUI.changed", System.StringComparison.Ordinal) < 0) { continue; }
                    if (lines[l].Contains("fields.Changed")) { continue; }

                    bool wrapsAnApply = false;
                    for (int on = l + 1; on < lines.Length && on < l + 60; on++)
                    {
                        if (lines[on].Contains("ApplyModifiedProperties()")) { wrapsAnApply = true; break; }
                    }
                    Expect(!wrapsAnApply, "commit: " + name + ":" + (l + 1) + " guards an apply on GUI.changed alone. "
                        + "On the frame a deferred control commits nothing else reported a change, so GUI.changed is "
                        + "false and the author's value is dropped at the next Update(). Test fields.Changed too",
                        failures);
                }
            }
            return scanned;
        }


        /// <summary> Nothing reaches the drawer without a commit buffer. The drawer's null fallback is Unity's own
        /// PropertyField, which is correct as a fallback and is per-keystroke commits again if a shipped call site ever
        /// takes it -- and it would take it silently, drawing a control that looks right and behaves the way it did
        /// before B-19. </summary>
        private static void DrawerAlwaysGetsACommitBuffer(List<string> failures)
        {
            string folder = InspectorConsistencyDocWriter.EditorScriptFolder();
            if (!Directory.Exists(folder)) { return; }

            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            string[] calls = { "InspectorFieldDrawer.Draw(", "LegacyInspectorShell.Field(",
                "LegacyInspectorShell.Group(", "LegacyInspectorShell.AllGroups(" };
            for (int f = 0; f < files.Length; f++)
            {
                string name = Path.GetFileName(files[f]);
                if (name == "LegacyInspectorGate.cs") { continue; }

                string[] lines = CodeLines(name, failures);
                for (int l = 0; l < lines.Length; l++)
                {
                    for (int c = 0; c < calls.Length; c++)
                    {
                        if (lines[l].IndexOf(calls[c], System.StringComparison.Ordinal) < 0) { continue; }
                        Expect(MentionsIdentifier(lines[l], "fields"),
                            "commit: " + name + ":" + (l + 1) + " reaches the drawer without naming a commit buffer, "
                            + "so that field falls back to Unity's per-keystroke PropertyField", failures);
                    }
                }
            }
        }
        #endregion


        #region "Case 6's disabled-control helpers -- B-5, fault 7"
        /// <summary> The spellings by which a disabled scope would be reading the state of a BUILD rather than the
        /// state of the road. A control greyed because the road is generating is the fault RM-102 removed: the author
        /// who notices the mistake is exactly the one who cannot then correct it, and the correction is what the
        /// coalescing queue exists to absorb. A control greyed because the feature that owns it is switched off is the
        /// opposite -- it is B-5's fix, and it is why case 6 can no longer ban disabling outright. </summary>
        private static readonly string[] buildStateTokens =
        {
            "Build", "queued", "progress", "isUpdateRequired"
        };


        /// <summary> Whether <paramref name="field"/> is drawn inside a disabled scope opened on
        /// <paramref name="condition"/>. Six lines of look-ahead, which is the brace, an explaining comment or two,
        /// and the call: enough for the two sites B-5 names and not enough for the field to have wandered off into
        /// some other block that merely follows the scope.
        ///
        /// <paramref name="field"/> arrives QUOTED -- "\"shoulderWidth\"" -- so that the match is the string literal in
        /// the Field call and not a mention of the same word in an identifier. </summary>
        private static bool DisabledScopeGuards(string[] lines, string condition, string field)
        {
            string open = "new EditorGUI.DisabledScope(" + condition + ")";
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(open, System.StringComparison.Ordinal) < 0) { continue; }
                int last = System.Math.Min(lines.Length - 1, i + 6);
                for (int j = i + 1; j <= last; j++)
                {
                    if (lines[j].IndexOf(field, System.StringComparison.Ordinal) >= 0) { return true; }
                }
            }
            return false;
        }
        #endregion


        #region "Case 11 -- no control label ends in a colon"
        /// <summary> B-5, fault 2. No label on a control in either legacy inspector ends in a colon.
        ///
        /// Unity's prefix label already separates itself from the control it names, so <c>"Gizmos: "</c> reached the
        /// author as "Gizmos:  " -- a colon and two trailing spaces -- while the label directly under it, written by
        /// the same file on the same day, had neither. The colon is not wrong in itself; a column of labels where some
        /// carry one and some do not is, because the eye reads the difference as meaning something.
        ///
        /// The scan is on CODE lines, so the comments that quote the old spellings -- and this fix leaves a lot of them,
        /// since a label's history is the only record of why it reads as it does -- stay legible instead of being
        /// worded around. A literal is judged only on a line that draws something labelled, and only when the literal
        /// is a WHOLE argument: <c>LabelField("Nodes: " + count)</c> is a sentence being built and its colon is
        /// punctuation, where <c>Toggle("Save mesh assets: ", …)</c> is a label and its colon is the fault.
        ///
        /// Scoped to this one file on purpose. The other legacy inspectors still spell their labels their own way; each
        /// is rebuilt under its own register entry, and a gate that failed for a file nobody has reached yet would be
        /// noise that teaches the suite to be ignored. </summary>
        private static void CaseNoControlLabelEndsInAColon(StringBuilder builder, List<string> failures)
        {
            // B-6 brought the second file in. `RoadSystemEditor` had `"Save mesh assets: "`, `"Custom camera rot:"`,
            // `"Rotation X:"` and five more, so a rule proved on one inspector and not applied to the next one fixed
            // would have been a rule about a file rather than about the tool.
            string[] files = new string[] { RoadFile, SystemFile };
            int scanned = 0;
            for (int f = 0; f < files.Length; f++)
            {
                string[] lines = CodeLines(files[f], failures);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!DrawsALabelledControl(lines[i])) { continue; }
                    scanned++;
                    foreach (Match m in Regex.Matches(lines[i], "\"[A-Za-z][^\"\n]*:[ ]*\"[ ]*[,)]"))
                    {
                        failures.Add("label: " + files[f] + ":" + (i + 1) + " ends a control label in a colon "
                            + m.Value.Trim() + "; Unity draws the separator itself, so the colon is a second one, and "
                            + "the labels beside this one carry none");
                    }
                }
            }
            Expect(scanned > 0,
                "label: no labelled control was found in the legacy inspectors at all, so nothing was checked (setup)",
                failures);
            builder.Append("RCMLEGACYUI ").Append("no-colon-labels".PadRight(24))
                   .Append("no control label ends in a colon (").Append(scanned).Append(" lines)\n");
        }


        /// <summary> Whether this line puts a label on a control -- the only lines the colon rule applies to. A help
        /// paragraph is deliberately outside it: those go through <c>RenderHelpParagraph</c>, they are prose rather
        /// than labels, and prose is allowed to introduce a list with a colon. </summary>
        private static bool DrawsALabelledControl(string line)
        {
            return line.Contains("EditorGUILayout.") || line.Contains("GUILayout.Label")
                || line.Contains("new GUIContent(") || line.Contains("SectionHeader(")
                || line.Contains("RenderHelpHeader(");
        }
        #endregion


        #region "Case 12 -- the system inspector does not edit the scene while it draws (B-6)"
        /// <summary> B-6. Six faults in one inspector, five of which can be stated as a rule about the source and are.
        ///
        /// The one worth the most: `OnInspectorGUI` used to CALL `roadSystem.EditorCameraSetSingle()`, which searched
        /// the scene for its only camera and wrote the result into a serialised field of a scene component. Selecting
        /// the object was therefore an edit -- no undo step, no dirty flag the author had asked for, and a value that
        /// reappeared every repaint after being cleared on purpose. The pick is a button now, in
        /// `LegacyInspectorShell.CameraField`, and both component-side setters were deleted rather than left
        /// zero-caller, so this case proves the spelling is gone from the runtime scripts as well as from the editor
        /// ones. It is checked on code lines, which is what lets the two tombstone comments explaining the removal keep
        /// naming the method they removed.
        ///
        /// The rest are the faults that made the file read as several unrelated tools: a widget whose current value
        /// came off the component while its new value went through the serialised property (so an undo restored the
        /// property and the widget went on showing the field); command buttons at three hard-coded widths in two
        /// styles; and a warning sentence that existed byte for byte in a second inspector. Each is now a single line
        /// of policy -- no field read on a drawing line, no numeric width, one shared warning -- and each is stated
        /// here so that the next person to add a control to this file finds the rule before the reviewer does.
        ///
        /// The last check is the general form of the fault nobody would have found by reading a diff: `Flip camera Y`
        /// wrote a field that NOTHING read, so the control had no effect whatever and looked exactly like one that
        /// did. Every field this inspector declares must be read somewhere and not merely assigned -- where "read"
        /// deliberately excludes the left-hand side of its own assignment, because that is precisely how a dead control
        /// disguises itself. </summary>
        private static void CaseTheSystemInspectorDoesNotEditWhileItDraws(StringBuilder builder, List<string> failures)
        {
            string[] lines = CodeLines(SystemFile, failures);
            if (lines.Length < 1) { return; }

            ToolWideSpellings(failures);
            SystemInspectorDrawingLines(lines, failures);
            EverySystemInspectorFieldIsRead(lines, failures);

            Expect(!Contains(lines, "SceneView.lastActiveSceneView"),
                "system: " + SystemFile + " reaches SceneView.lastActiveSceneView directly; it is null in a scene with "
                + "no scene view -- a maximised game view, or batch mode -- so use EngineIntegration, which guards it",
                failures);
            Expect(Contains(lines, "Undo.RecordObjects("),
                "system: " + SystemFile + " no longer records an undo step for the roads it writes a build option "
                + "into; ticking one option on a forty-road system has to be one Ctrl+Z, not none",
                failures);
            Expect(Contains(lines, "LegacyInspectorShell.SavingMeshesWarning("),
                "system: " + SystemFile + " draws its own copy of the saving-meshes warning again", failures);

            builder.Append("RCMLEGACYUI ").Append("system-draws-only".PadRight(24))
                   .Append("the system inspector draws without editing (").Append(lines.Length).Append(" lines)\n");
        }


        /// <summary> The two spellings whose whole point is that they exist in ONE place, checked across every script of
        /// the tool rather than across the editor folder: the camera setter this task deleted lived on the runtime
        /// components, and the warning sentence has to be absent from the two inspectors that used to hold a copy each.
        /// </summary>
        private static void ToolWideSpellings(List<string> failures)
        {
            string editorFolder = InspectorConsistencyDocWriter.EditorScriptFolder();
            DirectoryInfo scripts = Directory.GetParent(editorFolder);
            if (scripts == null || !scripts.Exists)
            {
                failures.Add("system: '" + editorFolder + "' has no parent script folder, so the tool-wide spellings "
                    + "could not be checked (setup)");
                return;
            }

            string[] sources = Directory.GetFiles(scripts.FullName, "*.cs", SearchOption.AllDirectories);
            int warningCopies = 0;
            for (int s = 0; s < sources.Length; s++)
            {
                string[] code = CodeLinesAt(sources[s], failures);
                string name = Path.GetFileName(sources[s]);
                for (int i = 0; i < code.Length; i++)
                {
                    if (MentionsIdentifier(code[i], DeletedCameraSetter))
                    {
                        failures.Add("system: " + name + ":" + (i + 1) + " names " + DeletedCameraSetter
                            + "; that setter filled a scene component's camera field from a repaint, and its "
                            + "replacement is the author-pressed button in LegacyInspectorShell.CameraField");
                    }
                }
                if (Contains(code, SavingMeshesSentence)) { warningCopies++; }
            }

            Expect(warningCopies == 1,
                "system: the saving-meshes warning is written out in " + warningCopies + " files; it belongs to "
                + "LegacyInspectorShell.SavingMeshesWarning alone, so the two inspectors that offer the option cannot "
                + "drift into warning about it differently",
                failures);
        }
        /// <summary> The lines of the system inspector that draw something, judged on what they read and how wide they
        /// are. A drawing line may not take its value off the component -- <c>roadSystem.something</c> with no bracket
        /// after it is a field or property read, where <c>roadSystem.AddRoad(true)</c> is a command and is fine -- and
        /// no line in the file may hard-code a control width. </summary>
        private static void SystemInspectorDrawingLines(string[] lines, List<string> failures)
        {
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"GUILayout\.Width\(\s*[0-9]"))
                {
                    failures.Add("system: " + SystemFile + ":" + (i + 1) + " hard-codes a control width; every command "
                        + "button in this file is LegacyInspectorShell.CommandButtonWidth wide, which is what stops one "
                        + "row of buttons reading as four unrelated things");
                }
                if (!DrawsALabelledControl(lines[i])) { continue; }
                drawn++;

                foreach (Match m in Regex.Matches(lines[i], @"roadSystem\.([A-Za-z_][A-Za-z0-9_]*)\s*(\()?"))
                {
                    if (m.Groups[2].Success) { continue; }
                    failures.Add("system: " + SystemFile + ":" + (i + 1) + " draws a control whose value is read off "
                        + "the component (roadSystem." + m.Groups[1].Value + "); read the serialized property instead, "
                        + "or an undo restores the property while the control goes on showing the field");
                }
            }
            Expect(drawn > 0,
                "system: no drawing line was found in " + SystemFile + " at all, so nothing was checked (setup)",
                failures);
        }


        /// <summary> Every field this inspector declares is read somewhere, and not merely written.
        ///
        /// The left-hand side of the field's own assignment does not count, which is the whole point: a control written
        /// as <c>myField = EditorGUILayout.Toggle(…)</c> and read by nothing is a control with no effect, and that is
        /// exactly what `Flip camera Y` was for as long as this file has existed. A compound assignment
        /// (<c>index += 1</c>) does read the field and is counted, and so is an assignment that mentions the field on
        /// its right-hand side. </summary>
        private static void EverySystemInspectorFieldIsRead(string[] lines, List<string> failures)
        {
            int checkedFields = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                Match declaration = Regex.Match(lines[i],
                    @"^\s*private\s+(?:static\s+)?[A-Za-z_][A-Za-z0-9_<>,\.\[\]\s]*?\s([A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)");
                if (!declaration.Success) { continue; }

                string field = declaration.Groups[1].Value;
                checkedFields++;
                Expect(IsReadSomewhere(lines, field, i),
                    "system: " + SystemFile + ":" + (i + 1) + " declares '" + field + "', which nothing in the file "
                    + "ever reads; a control that writes a field no code asks about does nothing at all, and looks "
                    + "exactly like one that works",
                    failures);
            }
            Expect(checkedFields > 0,
                "system: no field declaration was found in " + SystemFile + ", so nothing was checked (setup)",
                failures);
        }
        /// <summary> Whether <paramref name="field"/> is read anywhere but its own declaration. </summary>
        private static bool IsReadSomewhere(string[] lines, string field, int declaredAt)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == declaredAt) { continue; }
                if (!MentionsIdentifier(lines[i], field)) { continue; }

                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith(field, System.StringComparison.Ordinal))
                {
                    string rest = trimmed.Substring(field.Length).TrimStart();
                    bool isPlainAssignment = rest.StartsWith("=", System.StringComparison.Ordinal)
                        && !rest.StartsWith("==", System.StringComparison.Ordinal);
                    // `f = g;` writes and does not read. `f = h(f);` does both, and so does `f += 1`.
                    if (isPlainAssignment && !MentionsIdentifier(rest.Substring(1), field)) { continue; }
                }
                return true;
            }
            return false;
        }


        /// <summary> Whether any of these code lines contains this text. </summary>
        private static bool Contains(string[] lines, string text)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(text, System.StringComparison.Ordinal) >= 0) { return true; }
            }
            return false;
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
            return CodeLinesAt(Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), file), failures);
        }


        /// <summary> The same reader, addressed by full path. B-6's case has to look at the RUNTIME scripts as well --
        /// the method it proves is gone lived on `Road` and `RoadSystem` -- and the alternative to this two-line split
        /// was a second copy of the comment stripper that could disagree with the first about what a comment is.
        ///
        /// <c>internal</c> for that same reason, not for this gate's convenience: <see cref="DispositionGate"/> case 6
        /// has to decide whether a member a performed removal deleted is really gone, and a mention of its name in the
        /// comment explaining the removal is not a use of it. That is the rule <c>Docs/Disposition.md</c> already
        /// states -- "comments read as comments and not as references" -- so it reads code through this stripper rather
        /// than growing a second one that could disagree with it (B-21). </summary>
        internal static string[] CodeLinesAt(string path, List<string> failures)
        {
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


        /// <summary> The spellings by which a value the author TYPES gets drawn by hand, beyond the numeric ones above:
        /// text, and the vector fields, which are three or four text boxes in a row.
        ///
        /// Toggles, popups and object fields are deliberately absent, and their absence is the shape of B-19 rather
        /// than an omission. A toggle commits on one click, a popup on one selection, an object field on one drop --
        /// each is already a single completed gesture, so there is no half-finished state for a deferred commit to
        /// protect. What B-19 is about is a value that passes through nonsense on the way to what the author meant: 3
        /// and 35 and 350 en route to 3500, or a slider handle sweeping every width between where it started and where
        /// it is going. Only typing and dragging do that. </summary>
        private static readonly string[] keystrokeDraws =
        {
            "TextField(", "TextArea(", "DelayedTextField(",
            "Vector2Field(", "Vector3Field(", "Vector4Field("
        };


        /// <summary> Whether this line draws a value the author types or drags with Unity's own immediate control --
        /// the B-19 question, which is wider than case 1's: case 1 asks whether a REGISTRY field is hand-drawn in one
        /// of two files, this asks whether ANY typed or dragged value anywhere in the folder still commits per
        /// keystroke. </summary>
        private static bool IsRawInputDraw(string line)
        {
            return IsRawNumericDraw(line) || IsRawDraw(line, keystrokeDraws);
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

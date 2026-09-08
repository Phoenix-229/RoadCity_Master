// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-64's gate: proves the consistency pass is recorded honestly, without opening a single inspector.
    ///
    /// Thirteen cases. The register covers exactly the author-facing files that are actually on disk, so a sixteenth
    /// inspector cannot escape the pass; every row's state is the DERIVED one, and none is RM-64's unexplained gap; a
    /// file whose only change was its header is never reported as converted; a row that will not be converted carries a
    /// real reason and one that is scheduled carries an owner; the refusal routing is total and single-valued, which is
    /// "never both, never neither" as a checkable property; measured values are non-editable and every one names the task
    /// that computes it; foldout keys are per component TYPE, stable, and free of GetHashCode; reading a foldout falls
    /// back to the default policy WITHOUT writing a preference entry; the generated table is well formed; and the
    /// document on disk still matches the register. B-11 added the last two: the generated-object name table answers for
    /// every name the build emits and is silent on everything else, and the header panel it feeds reports without
    /// editing -- foldout per type, and its one button defers the selection instead of changing it mid-draw.
    /// Report lines are prefixed RCMCONSIST.
    ///
    /// What it does NOT prove: that the converted layout looks right. Repaint, foldout animation and field ordering on
    /// screen are the deferred visual half, and RM-102 is where the legacy family's drawing actually changes. </summary>
    public static class InspectorConsistencyGate
    {
        [MenuItem("Window/Road City Master/Testing/Inspector consistency checklist")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.InspectorConsistencyGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMCONSIST BEGIN\n");
            List<string> failures = new List<string>();

            CaseRegisterCoversTheDirectory(builder, failures);
            CaseStateIsDerived(builder, failures);
            CaseNoUnexplainedGap(builder, failures);
            CaseHeaderRenameIsNeverConverted(builder, failures);
            CaseReasonsAndOwnersAreReal(builder, failures);
            CaseRefusalRoutingIsTotal(builder, failures);
            CaseMeasuredValuesAreNotEditable(builder, failures);
            CaseFoldoutKeysArePerType(builder, failures);
            CaseFoldoutReadDoesNotWrite(builder, failures);
            CaseTableIsWellFormed(builder, failures);
            CaseDocumentMatchesRegister(builder, failures);
            CaseGeneratedNamesAreAnswered(builder, failures);
            CaseGeneratedPanelIsReadOnly(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCONSIST FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCONSIST result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCONSIST END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }

        /// <summary> Case 1. The register's file set equals the directory's, minus § 10's own tooling. This is the case
        /// that makes RM-64 hold in a year: a new inspector added without a checklist row fails the build, rather than
        /// quietly being a sixteenth file nobody passed over. </summary>
        private static void CaseRegisterCoversTheDirectory(StringBuilder builder, List<string> failures)
        {
            string folder = InspectorConsistencyDocWriter.EditorScriptFolder();
            if (!Directory.Exists(folder))
            {
                failures.Add("case1: the editor script folder '" + folder + "' does not exist");
                return;
            }

            Dictionary<string, bool> excluded = new Dictionary<string, bool>();
            foreach (string name in InspectorConsistencyChecklist.NotAuthorFacing()) { excluded[name] = true; }

            Dictionary<string, bool> onDisk = new Dictionary<string, bool>();
            foreach (string path in Directory.GetFiles(folder, "*.cs"))
            {
                string name = Path.GetFileName(path);
                if (!excluded.ContainsKey(name)) { onDisk[name] = true; }
            }

            Dictionary<string, bool> inRegister = new Dictionary<string, bool>();
            foreach (InspectorRecord row in InspectorConsistencyChecklist.Rows()) { inRegister[row.file] = true; }

            foreach (string name in onDisk.Keys)
            {
                Expect(inRegister.ContainsKey(name),
                    "case1: '" + name + "' is an author-facing editor file with no checklist row", failures);
            }
            foreach (string name in inRegister.Keys)
            {
                Expect(onDisk.ContainsKey(name),
                    "case1: the checklist names '" + name + "', which is not on disk (or is on the exclusion list)",
                    failures);
            }
            Expect(InspectorConsistencyChecklist.Count == 15,
                "case1: RM-64's fifteen files became fourteen when RM-101 deleted EditorProgressWindow.cs, the modal-bar"
                + " demo window, and are fifteen again since B-11 added GeneratedObjectPanel.cs, the header panel on"
                + " generated objects; the register carries " + InspectorConsistencyChecklist.Count,
                failures);

            builder.Append("RCMCONSIST case1 ").Append(onDisk.Count).Append(" author-facing files on disk, ")
                   .Append(InspectorConsistencyChecklist.Count).Append(" rows, ")
                   .Append(excluded.Count).Append(" excluded by name\n");
        }

        /// <summary> Case 2. The state is derived, in the stated order, for every row -- and a constructed row with all
        /// five criteria really does come back converted, so the derivation is not merely permissive. </summary>
        private static void CaseStateIsDerived(StringBuilder builder, List<string> failures)
        {
            foreach (InspectorRecord row in InspectorConsistencyChecklist.Rows())
            {
                ConversionState expected;
                if (row.applied == ConversionCriteria.All) { expected = ConversionState.Converted; }
                else if (row.owner.Length > 0) { expected = ConversionState.Pending; }
                else if (row.reason.Length > 0) { expected = ConversionState.DeliberatelyUnchanged; }
                else { expected = ConversionState.NotConverted; }

                Expect(row.State == expected,
                    "case2: '" + row.file + "' derives " + row.State + " where the rule gives " + expected, failures);
            }

            InspectorRecord full = new InspectorRecord("Probe.cs", "Probe", InspectorKind.ComponentInspector,
                ConversionCriteria.All, "", "", "");
            Expect(full.State == ConversionState.Converted,
                "case2: a row with all five criteria is not reported as converted", failures);
            Expect(full.Missing == ConversionCriteria.None,
                "case2: a fully converted row still reports missing criteria", failures);

            InspectorRecord bare = new InspectorRecord("Probe.cs", "Probe", InspectorKind.ComponentInspector,
                ConversionCriteria.None, "", "", "");
            Expect(bare.State == ConversionState.NotConverted,
                "case2: a row with nothing applied and nothing explained is not reported as a gap", failures);

            builder.Append("RCMCONSIST case2 derivation agrees on all ")
                   .Append(InspectorConsistencyChecklist.Count).Append(" rows plus two probes\n");
        }

        /// <summary> Case 3. RM-64's forbidden outcome: not one row is an unexplained gap. </summary>
        private static void CaseNoUnexplainedGap(StringBuilder builder, List<string> failures)
        {
            IList<InspectorRecord> gaps = InspectorConsistencyChecklist.InState(ConversionState.NotConverted);
            foreach (InspectorRecord row in gaps)
            {
                failures.Add("case3: '" + row.file + "' is neither converted, nor owned, nor explained");
            }
            builder.Append("RCMCONSIST case3 converted ")
                   .Append(InspectorConsistencyChecklist.InState(ConversionState.Converted).Count)
                   .Append(", pending ")
                   .Append(InspectorConsistencyChecklist.InState(ConversionState.Pending).Count)
                   .Append(", deliberately unchanged ")
                   .Append(InspectorConsistencyChecklist.InState(ConversionState.DeliberatelyUnchanged).Count)
                   .Append(", gaps ").Append(gaps.Count).Append("\n");
        }

        /// <summary> Case 4. RM-64 names this trap by hand: a file that had its header renamed is not converted. Because
        /// the state is derived, the property holds for any row, present or future. </summary>
        private static void CaseHeaderRenameIsNeverConverted(StringBuilder builder, List<string> failures)
        {
            int renames = 0;
            foreach (InspectorRecord row in InspectorConsistencyChecklist.Rows())
            {
                if (!row.IsHeaderRenameOnly) { continue; }
                renames++;
                Expect(row.State != ConversionState.Converted,
                    "case4: '" + row.file + "' changed only its product string and is reported as converted", failures);
                Expect((row.Missing & ConversionCriteria.GroupedVocabulary) != 0,
                    "case4: '" + row.file + "' is a header rename yet claims the grouping vocabulary", failures);
            }
            builder.Append("RCMCONSIST case4 ").Append(renames)
                   .Append(" header-rename-only rows, none reported converted\n");
        }

        /// <summary> Case 5. An excuse has to say something. A deliberately-unchanged row carries a reason long enough to
        /// be a reason rather than a shrug, and a pending row carries an owner that looks like a task id. </summary>
        private static void CaseReasonsAndOwnersAreReal(StringBuilder builder, List<string> failures)
        {
            foreach (InspectorRecord row in InspectorConsistencyChecklist.Rows())
            {
                if (row.State == ConversionState.DeliberatelyUnchanged)
                {
                    Expect(row.reason.Length >= 40,
                        "case5: '" + row.file + "' is deliberately unchanged on a " + row.reason.Length
                        + "-character reason", failures);
                    Expect(row.owner.Length == 0,
                        "case5: '" + row.file + "' is both unchanged-forever and owned by " + row.owner, failures);
                }
                if (row.State == ConversionState.Pending)
                {
                    Expect(LooksLikeATaskId(row.owner),
                        "case5: '" + row.file + "' is pending on owner '" + row.owner + "', which is not a task id",
                        failures);
                }
            }
            builder.Append("RCMCONSIST case5 reasons and owners well formed\n");
        }


        /// <summary> Whether an owner reads as one of this tool's task ids: an <c>RM-</c> id from the conversion plan, or
        /// a <c>B-</c> id from the bug register. The second family was added by B-6, which is the first entry to finish
        /// layout work on a file the plan had left to RM-102: writing RM-102 next to work RM-102 did not do would make
        /// the column useless for the only thing it is for, which is knowing who to ask. The shape checked is a prefix
        /// and then a number, so a bare "B-" or an "RM-" with nothing after it is still refused. </summary>
        private static bool LooksLikeATaskId(string owner)
        {
            if (string.IsNullOrEmpty(owner)) { return false; }
            int dash = owner.IndexOf('-');
            if (dash < 1 || dash + 1 >= owner.Length) { return false; }

            string prefix = owner.Substring(0, dash);
            if (prefix != "RM" && prefix != "B") { return false; }

            for (int i = dash + 1; i < owner.Length; i++)
            {
                if (!char.IsDigit(owner[i])) { return false; }
            }
            return true;
        }

        /// <summary> Case 6. "Never both, never neither" as a property: the routing is total, single-valued, and the two
        /// destinations are distinct. Three lines here replace a convention nobody remembers on the fifteenth file. </summary>
        private static void CaseRefusalRoutingIsTotal(StringBuilder builder, List<string> failures)
        {
            ReportPlacement perObject = RefusalRouting.Decide(RefusalScope.PerObject);
            ReportPlacement aggregate = RefusalRouting.Decide(RefusalScope.Aggregate);

            Expect(perObject == ReportPlacement.OnTheObjectInspector,
                "case6: a per-object refusal is routed to " + perObject, failures);
            Expect(aggregate == ReportPlacement.InTheStatusPanel,
                "case6: an aggregate report is routed to " + aggregate, failures);
            Expect(perObject != aggregate,
                "case6: both scopes route to the same place, so a refusal appears twice or not at all", failures);
            Expect(RefusalRouting.Decide(RefusalScope.PerObject) == perObject,
                "case6: the routing is not stable across calls", failures);

            builder.Append("RCMCONSIST case6 per-object -> ").Append(perObject)
                   .Append(", aggregate -> ").Append(aggregate).Append("\n");
        }

        /// <summary> Case 7. A computed value must not look typeable. The presentation is constants, so this asserts the
        /// constants and that every listed measured value names the task that computes it -- an unattributed measurement
        /// is a number nobody can check (C-15). </summary>
        private static void CaseMeasuredValuesAreNotEditable(StringBuilder builder, List<string> failures)
        {
            Expect(MeasuredValuePresentation.IsEditable == false,
                "case7: measured values are drawn editable", failures);
            Expect(MeasuredValuePresentation.GroupLabel == "Measured",
                "case7: the measured group label is '" + MeasuredValuePresentation.GroupLabel + "'", failures);

            IList<string> known = MeasuredValuePresentation.Known();
            Expect(known.Count >= 8,
                "case7: only " + known.Count + " measured values are registered", failures);
            foreach (string entry in known)
            {
                Expect(entry.IndexOf("(RM-", System.StringComparison.Ordinal) > 0,
                    "case7: measured value '" + entry + "' does not name the task that computes it", failures);
            }

            known.Add("mutation probe");
            Expect(MeasuredValuePresentation.Known().Count != known.Count,
                "case7: Known() hands out its own list, so a caller can edit the register", failures);

            builder.Append("RCMCONSIST case7 ").Append(MeasuredValuePresentation.Known().Count)
                   .Append(" measured values, none editable\n");
        }

        /// <summary> Case 8. Foldout keys are per component TYPE and per group, stable across calls, prefixed, and contain
        /// no hash -- C-06 bans GetHashCode here because a key that changes between Unity versions is a foldout that
        /// forgets. The default policy is asserted too: the identity group open, the rest closed. </summary>
        private static void CaseFoldoutKeysArePerType(StringBuilder builder, List<string> failures)
        {
            string roadIs = InspectorFoldoutState.Key("Road", FieldGroup.WhatItIs);
            string roadWorld = InspectorFoldoutState.Key("Road", FieldGroup.WhatItDoesToTheWorld);
            string nodeIs = InspectorFoldoutState.Key("SplineN", FieldGroup.WhatItIs);

            Expect(roadIs == InspectorFoldoutState.Key("Road", FieldGroup.WhatItIs),
                "case8: the key is not stable across calls", failures);
            Expect(roadIs != roadWorld, "case8: two groups of one component share a key", failures);
            Expect(roadIs != nodeIs, "case8: two component types share a key", failures);
            Expect(roadIs.StartsWith(InspectorFoldoutState.KeyPrefix, System.StringComparison.Ordinal),
                "case8: the key is not prefixed, so it can collide with another tool's preference", failures);
            // What the key carries AFTER the prefix, because the prefix opens with the tool's own name: a plain
            // `roadIs.IndexOf("Road")` finds "Road" at 0 inside "RoadCityMaster.Foldout." and so answers a question
            // nobody asked. The question is whether the COMPONENT type reached the key, and only the body can say.
            string roadIsBody = roadIs.Length > InspectorFoldoutState.KeyPrefix.Length
                ? roadIs.Substring(InspectorFoldoutState.KeyPrefix.Length)
                : roadIs;
            Expect(roadIsBody.IndexOf("Road", System.StringComparison.Ordinal) >= 0
                && roadIsBody.IndexOf("WhatItIs", System.StringComparison.Ordinal) >= 0,
                "case8: the key does not name both the component type and the group", failures);
            Expect(InspectorFoldoutState.MeasuredKey("Road") != roadIs,
                "case8: the measured group shares a key with an author group", failures);

            Expect(InspectorFoldoutState.DefaultOpen(FieldGroup.WhatItIs),
                "case8: the identity group defaults closed, so the inspector opens looking empty", failures);
            Expect(!InspectorFoldoutState.DefaultOpen(FieldGroup.WhatItLooksLike)
                && !InspectorFoldoutState.DefaultOpen(FieldGroup.WhatItDoesToTheWorld)
                && !InspectorFoldoutState.DefaultOpen(FieldGroup.WhatIsGeneratedFromIt),
                "case8: more than one group defaults open, so the inspector opens as a wall", failures);

            builder.Append("RCMCONSIST case8 key '").Append(roadIs).Append("'\n");
        }

        /// <summary> Case 9. Reading a foldout an author has never touched returns the default and writes NOTHING -- a
        /// tool that manufactures a preference entry per group per component per repaint is a tool that pollutes the
        /// editor's preferences file. Then a recorded choice comes back, and only for the type it was made on. The store
        /// is swapped for an in-memory one so this proves the behaviour without touching real preferences, and the real
        /// store is restored whatever happens. </summary>
        private static void CaseFoldoutReadDoesNotWrite(StringBuilder builder, List<string> failures)
        {
            InMemoryFoldoutStore store = new InMemoryFoldoutStore();
            try
            {
                InspectorFoldoutState.UseStore(store);

                bool openByDefault = InspectorFoldoutState.IsOpen("Road", FieldGroup.WhatItIs);
                bool closedByDefault = InspectorFoldoutState.IsOpen("Road", FieldGroup.WhatItLooksLike);
                Expect(openByDefault && !closedByDefault,
                    "case9: the default policy is not what DefaultOpen says", failures);
                Expect(store.Count == 0,
                    "case9: reading foldouts wrote " + store.Count + " preference entries", failures);
                Expect(!InspectorFoldoutState.IsMeasuredOpen("Road"),
                    "case9: the measured group defaults open", failures);
                Expect(store.Count == 0,
                    "case9: reading the measured foldout wrote a preference entry", failures);

                InspectorFoldoutState.SetOpen("Road", FieldGroup.WhatItLooksLike, true);
                Expect(store.Count == 1,
                    "case9: recording one choice wrote " + store.Count + " entries", failures);
                Expect(InspectorFoldoutState.IsOpen("Road", FieldGroup.WhatItLooksLike),
                    "case9: a recorded choice does not come back", failures);
                Expect(!InspectorFoldoutState.IsOpen("SplineN", FieldGroup.WhatItLooksLike),
                    "case9: a choice made on one component type leaked onto another", failures);
                Expect(InspectorFoldoutState.IsOpen("Road", FieldGroup.WhatItIs),
                    "case9: recording one group's choice disturbed another group", failures);
            }
            finally
            {
                InspectorFoldoutState.UseStore(new EditorPrefsFoldoutStore());
            }

            builder.Append("RCMCONSIST case9 defaults read clean, one write per recorded choice\n");
        }

        /// <summary> Case 10. The generated table is well formed and deterministic: seven columns on every row, no stray
        /// pipe inside a cell (which would silently split a column), no duplicate or unsorted file name, and the same
        /// text on a second call. </summary>
        private static void CaseTableIsWellFormed(StringBuilder builder, List<string> failures)
        {
            string table = InspectorConsistencyChecklist.ToManualTable();
            Expect(table == InspectorConsistencyChecklist.ToManualTable(),
                "case10: the table differs between two calls", failures);

            string[] lines = table.Split('\n');
            Expect(lines.Length == InspectorConsistencyChecklist.Count + 2,
                "case10: the table has " + lines.Length + " lines for "
                + InspectorConsistencyChecklist.Count + " rows plus two headers", failures);

            string previous = string.Empty;
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            for (int i = 0; i < lines.Length; i++)
            {
                int pipes = 0;
                for (int c = 0; c < lines[i].Length; c++) { if (lines[i][c] == '|') { pipes++; } }
                Expect(pipes == 8,
                    "case10: line " + (i + 1) + " has " + pipes + " pipes, so a cell contains one", failures);
            }
            foreach (InspectorRecord row in InspectorConsistencyChecklist.Rows())
            {
                Expect(!seen.ContainsKey(row.file), "case10: '" + row.file + "' appears twice", failures);
                seen[row.file] = true;
                Expect(string.CompareOrdinal(previous, row.file) < 0,
                    "case10: '" + row.file + "' is out of file-name order after '" + previous + "'", failures);
                previous = row.file;
                Expect(row.target.Length > 0 && row.note.IndexOf('|') < 0 && row.reason.IndexOf('|') < 0,
                    "case10: '" + row.file + "' has an empty target or a pipe in a cell", failures);
            }

            builder.Append("RCMCONSIST case10 ").Append(lines.Length).Append(" lines, ")
                   .Append(table.Length).Append(" characters, sorted and pipe-free\n");
        }

        /// <summary> Case 11. The document on disk still says what the register says. This is the case that stops RM-64's
        /// checklist becoming the thing it exists to prevent -- a written claim about the tool that quietly stopped being
        /// true. Comparison is on content, not on whitespace, through the same Normalise the availability and provenance
        /// tables use. </summary>
        private static void CaseDocumentMatchesRegister(StringBuilder builder, List<string> failures)
        {
            string path = InspectorConsistencyDocWriter.ChecklistPath();
            string onDisk, message;
            if (!InspectorConsistencyDocWriter.ReadTableFrom(path, out onDisk, out message))
            {
                failures.Add("case11: " + message);
                return;
            }

            string wanted = "\n\n" + InspectorConsistencyChecklist.ToManualTable() + "\n";
            string normalisedDisk = TerrainFeatureTableWriter.Normalise(onDisk);
            string normalisedWanted = TerrainFeatureTableWriter.Normalise(wanted);

            if (normalisedDisk != normalisedWanted)
            {
                failures.Add("case11: the checklist in '" + path + "' has drifted from the register ("
                    + normalisedDisk.Length + " characters on disk, " + normalisedWanted.Length
                    + " generated). Run Window > Road City Master > Documentation > "
                    + "Write the inspector consistency checklist.");
            }

            builder.Append("RCMCONSIST case11 ").Append(normalisedDisk.Length)
                   .Append(" normalised characters on disk, ")
                   .Append(normalisedDisk == normalisedWanted ? "matching" : "DRIFTED").Append("\n");
        }

        /// <summary> Case 12. B-11's name table has to be a TOTAL answer over the names the build emits, and silent
        /// everywhere else. Both halves matter: a generated object whose name is missing from the table gets no panel and
        /// the author is back to guessing whether their edit survives, and a table that claims someone else's GameObject
        /// puts our sentence on an object we know nothing about. The order the rows are declared in is load-bearing --
        /// For() takes the FIRST match -- so a suffix rule declared above an exact name would shadow it and the exact
        /// row would be dead code that reads as live. </summary>
        private static void CaseGeneratedNamesAreAnswered(StringBuilder builder, List<string> failures)
        {
            IList<GeneratedObjectRow> rows = GeneratedObjectProvenance.Rows();
            Expect(rows.Count > 0 && rows.Count == GeneratedObjectProvenance.RowCount,
                "case12: the name table is empty or RowCount disagrees with it", failures);

            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.Ordinal);
            List<string> duplicates = new List<string>();
            List<string> shadowed = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                GeneratedObjectRow row = rows[i];
                string key = ((int)row.rule).ToString() + "|" + row.pattern + "|" + row.tail;
                if (seen.ContainsKey(key)) { duplicates.Add(row.pattern); } else { seen[key] = true; }

                Expect(!string.IsNullOrEmpty(row.title) && !string.IsNullOrEmpty(row.producedBy),
                    "case12: '" + row.pattern + "' carries no title or no producer", failures);
                Expect(row.whatItIs != null && row.whatItIs.Length >= 40 && row.whatItIs.EndsWith(".",
                    StringComparison.Ordinal),
                    "case12: '" + row.pattern + "' does not explain itself in a finished sentence", failures);

                // The one name this row exists to answer. For() returns the FIRST match, so if any row above this one
                // also claims that name, this row is dead code that reads as live -- and the author gets the broader
                // row's sentence about a specific object. The table is grouped by where the objects live rather than by
                // rule, which is the readable order to maintain but not a safe one, so the shadowing is checked instead
                // of being assumed away.
                string probe = row.pattern;
                if (row.rule == GeneratedNameRule.PrefixThenIndex) { probe = row.pattern + "0" + row.tail; }
                else if (row.rule == GeneratedNameRule.Suffix) { probe = "RcmProbe" + row.pattern; }
                int first = -1;
                for (int j = 0; j < rows.Count && first < 0; j++)
                {
                    if (rows[j].Matches(probe)) { first = j; }
                }
                if (first != i)
                {
                    shadowed.Add(row.pattern + " (answered by "
                        + (first < 0 ? "nothing" : "'" + rows[first].pattern + "'") + ")");
                }
            }
            Expect(duplicates.Count == 0,
                "case12: these patterns are declared twice, so the second row can never be reached: "
                + string.Join(", ", duplicates.ToArray()), failures);
            Expect(shadowed.Count == 0,
                "case12: these rows do not answer for their own name, so they are unreachable: "
                + string.Join(", ", shadowed.ToArray()), failures);

            Expect(!GeneratedObjectProvenance.For(string.Empty).isKnown,
                "case12: the table answers for an empty name", failures);
            Expect(!GeneratedObjectProvenance.For("Directional Light").isKnown,
                "case12: the table claims an object the tool never made", failures);
            Expect(!GeneratedObjectProvenance.ForTransform(null).isKnown,
                "case12: a null transform is reported as known", failures);

            GeneratedObjectView mesh = GeneratedObjectProvenance.For("MainMeshes");
            Expect(mesh.isKnown && mesh.disposition == RebuildDisposition.Regenerated,
                "case12: the road's own mesh container is not answered as regenerated output", failures);

            string[] sentences =
            {
                GeneratedObjectProvenance.DispositionSentence(RebuildDisposition.Regenerated),
                GeneratedObjectProvenance.DispositionSentence(RebuildDisposition.MeshRewritten),
                GeneratedObjectProvenance.DispositionSentence(RebuildDisposition.Transient),
                GeneratedObjectProvenance.DispositionSentence(RebuildDisposition.AuthorOwned)
            };
            for (int i = 0; i < sentences.Length; i++)
            {
                Expect(!string.IsNullOrEmpty(sentences[i]) && sentences[i].Length >= 60,
                    "case12: one rebuild disposition has no sentence an author could act on", failures);
                for (int j = i + 1; j < sentences.Length; j++)
                {
                    Expect(sentences[i] != sentences[j],
                        "case12: two rebuild dispositions read identically, so the panel cannot tell them apart",
                        failures);
                }
            }

            builder.Append("RCMCONSIST case12 ").Append(rows.Count)
                   .Append(" generated names answered, silent off-table, four distinct dispositions\n");
        }

        /// <summary> Case 13. The header panel is the one author-facing file in the register that owns no fields, so the
        /// two criteria its row DOES claim are the two proved here: the foldout is remembered per type in
        /// InspectorFoldoutState rather than on an editor Unity throws away at every selection change, and reading it
        /// writes nothing. The rest of the case is the panel's read-only promise. Its one button changes the selection,
        /// which is the one thing a header pass must not do while it is mid-draw -- Unity is walking the editors of the
        /// object being replaced -- so the press only RECORDS the target and a delayed call applies it. That deferral is
        /// invisible in the running editor and would be silently lost in a refactor, which is why it is asserted rather
        /// than commented. </summary>
        private static void CaseGeneratedPanelIsReadOnly(StringBuilder builder, List<string> failures)
        {
            Expect(GeneratedObjectPanel.HeaderLabel(GeneratedObjectView.Unknown).Length == 0,
                "case13: the panel offers a header for an object it does not recognise", failures);

            GeneratedObjectView built = new GeneratedObjectView(true, "Carriageway surface", "x", "y",
                RebuildDisposition.Regenerated, string.Empty);
            GeneratedObjectView mine = new GeneratedObjectView(true, "City", "x", "y",
                RebuildDisposition.AuthorOwned, string.Empty);
            Expect(GeneratedObjectPanel.HeaderLabel(built).StartsWith("Generated", StringComparison.Ordinal),
                "case13: regenerated output is not announced as generated", failures);
            Expect(GeneratedObjectPanel.HeaderLabel(mine).StartsWith("Yours", StringComparison.Ordinal),
                "case13: an author-owned object is announced as generated, which invites an author to leave it alone",
                failures);

            Expect(GeneratedObjectPanel.Severity(RebuildDisposition.Regenerated) == MessageType.Warning
                && GeneratedObjectPanel.Severity(RebuildDisposition.Transient) == MessageType.Warning,
                "case13: an object the next build destroys is reported as information, not a warning", failures);
            Expect(GeneratedObjectPanel.Severity(RebuildDisposition.AuthorOwned) == MessageType.Info
                && GeneratedObjectPanel.Severity(RebuildDisposition.MeshRewritten) == MessageType.Info,
                "case13: an object that survives the build is reported as a warning", failures);

            Expect(GeneratedObjectPanel.SelectButtonLabel("road").IndexOf("road", StringComparison.Ordinal) >= 0,
                "case13: the button does not name what it will select", failures);
            Expect(GeneratedObjectPanel.SelectButtonLabel(string.Empty).Length > 0,
                "case13: the button has no label when the owner could not be named", failures);

            InMemoryFoldoutStore store = new InMemoryFoldoutStore();
            try
            {
                InspectorFoldoutState.UseStore(store);
                InspectorFoldoutState.IsOpen(GeneratedObjectPanel.FoldoutType, FieldGroup.WhatItIs);
                Expect(store.Count == 0,
                    "case13: drawing the panel's foldout wrote " + store.Count + " preference entries", failures);
                InspectorFoldoutState.SetOpen(GeneratedObjectPanel.FoldoutType, FieldGroup.WhatItIs, false);
                Expect(!InspectorFoldoutState.IsOpen(GeneratedObjectPanel.FoldoutType, FieldGroup.WhatItIs),
                    "case13: closing the panel is not remembered, so it reopens at every selection change", failures);
                Expect(InspectorFoldoutState.IsOpen("Road", FieldGroup.WhatItIs),
                    "case13: the panel's foldout key is not its own -- closing it closed a component's group", failures);
            }
            finally
            {
                InspectorFoldoutState.UseStore(new EditorPrefsFoldoutStore());
            }

            GameObject saved = Selection.activeGameObject;
            GameObject probe = new GameObject("RCMCONSIST case13 probe");
            try
            {
                GeneratedObjectPanel.RequestSelect(probe);
                Expect(GeneratedObjectPanel.PendingSelection == probe,
                    "case13: the button did not record what it was asked to select", failures);
                Expect(Selection.activeGameObject != probe,
                    "case13: the button changed the selection during the header pass instead of deferring it", failures);

                GeneratedObjectPanel.ApplySelectionNow();
                Expect(GeneratedObjectPanel.PendingSelection == null,
                    "case13: the deferred selection was not consumed, so it would fire again later", failures);
            }
            finally
            {
                Selection.activeGameObject = saved;
                UnityEngine.Object.DestroyImmediate(probe);
            }

            builder.Append("RCMCONSIST case13 panel reports only; foldout per type, selection deferred\n");
        }
    }
}
#endif

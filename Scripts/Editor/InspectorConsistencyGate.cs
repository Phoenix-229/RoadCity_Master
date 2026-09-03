#if UNITY_EDITOR
#region "Imports"
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
    /// Eleven cases. The register covers exactly the author-facing files that are actually on disk, so a sixteenth
    /// inspector cannot escape the pass; every row's state is the DERIVED one, and none is RM-64's unexplained gap; a
    /// file whose only change was its header is never reported as converted; a row that will not be converted carries a
    /// real reason and one that is scheduled carries an owner; the refusal routing is total and single-valued, which is
    /// "never both, never neither" as a checkable property; measured values are non-editable and every one names the task
    /// that computes it; foldout keys are per component TYPE, stable, and free of GetHashCode; reading a foldout falls
    /// back to the default policy WITHOUT writing a preference entry; the generated table is well formed; and the
    /// document on disk still matches the register. Report lines are prefixed RCMCONSIST.
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
            Expect(InspectorConsistencyChecklist.Count == 14,
                "case1: RM-64's fifteen files became fourteen when RM-101 deleted EditorProgressWindow.cs, the modal-bar"
                + " demo window; the register carries " + InspectorConsistencyChecklist.Count,
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
                    Expect(row.owner.StartsWith("RM-", System.StringComparison.Ordinal) && row.owner.Length >= 5,
                        "case5: '" + row.file + "' is pending on owner '" + row.owner + "', which is not a task id",
                        failures);
                }
            }
            builder.Append("RCMCONSIST case5 reasons and owners well formed\n");
        }

        /// <summary> Case 6. "Never both, never neither" as a property: the routing is total, single-valued, and the two
        /// destinations are distinct. Three lines here replace a convention nobody remembers on the fourteenth file. </summary>
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
    }
}
#endif

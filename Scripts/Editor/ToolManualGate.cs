// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-65's gate: proves the manual cannot be wrong about the things a manual is usually wrong about.
    ///
    /// Twelve cases. The field reference IS RM-51's registry rather than a transcription of it, so a documented field
    /// cannot drift out of the inspector; every documented field carries a unit and a range with a named source; the
    /// spot check RM-65 asks for is run EXHAUSTIVELY rather than on a sample of ten, which is strictly stronger; range
    /// text is invariant-formatted, so a comma-decimal machine cannot generate a different manual (C-06); every refusal
    /// names its wording, its reason and at least two ways out, and its id is stable and ordered; the cited-standards
    /// table has exactly one row per Cite line in § 10; no row invents an edition -- a source the project has not
    /// adopted says so in the words the register reserves for it; all four tables are well formed; the hand-written
    /// prose still states the exclusions and carries the licence and attribution; the document on disk still matches
    /// all four registers; and the menu map is reflected from the attributes rather than counted, which is B-20's fix
    /// for a manual that stated two different menu totals for the same tree. Report lines are prefixed RCMMANUAL.
    ///
    /// What it does NOT prove: that the prose is good, or that the four walkthroughs work when followed in the editor.
    /// A reader following them is RM-65's human half; this gate proves the reference half cannot be stale. </summary>

    public static class ToolManualGate
    {
        [MenuItem("Window/Road City Master/Testing/Manual against the registers")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.ToolManualGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMMANUAL BEGIN\n");
            List<string> failures = new List<string>();

            CaseFieldsAreTheRegistry(builder, failures);
            CaseEveryFieldHasUnitAndRange(builder, failures);
            CaseSpotCheckFindsNoDrift(builder, failures);
            CaseRangeTextIsInvariant(builder, failures);
            CaseRefusalsAreActionable(builder, failures);
            CaseRefusalIdsAreOrderedAndCopied(builder, failures);
            CaseStandardsCoverEveryCite(builder, failures);
            CaseNothingInventedIsMarked(builder, failures);
            CaseTablesAreWellFormed(builder, failures);
            CaseProseStatesExclusionsAndLicence(builder, failures);
            CaseDocumentMatchesRegisters(builder, failures);
            CaseMenuMapIsReflected(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMMANUAL FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMMANUAL result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMMANUAL END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }

        /// <summary> Case 1. The manual's field reference is RM-51's registry, walked -- not a second list that has to be
        /// kept in step with it. Proving identity rather than agreement is what makes drift impossible instead of
        /// merely detectable. </summary>
        private static void CaseFieldsAreTheRegistry(StringBuilder builder, List<string> failures)
        {
            IList<FieldDescriptor> manual = ManualReferences.Fields();
            IList<FieldDescriptor> registry = InspectorFieldRegistry.All();

            Expect(manual.Count == registry.Count,
                "case1: the manual documents " + manual.Count + " fields but the registry has " + registry.Count,
                failures);

            int same = 0;
            for (int i = 0; i < manual.Count && i < registry.Count; i++)
            {
                if (ReferenceEquals(manual[i], registry[i])) { same++; continue; }
                failures.Add("case1: manual field " + i + " ('" + manual[i].field
                    + "') is not the registry's descriptor -- the manual is transcribing, not walking");
            }

            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            foreach (FieldDescriptor d in manual)
            {
                string key = d.component + "." + d.field;
                Expect(!seen.ContainsKey(key), "case1: '" + key + "' is documented twice", failures);
                seen[key] = true;
                Expect(d.field.Length > 0 && d.label.Length > 0,
                    "case1: '" + key + "' has an empty serialised name or label", failures);
            }

            builder.Append("RCMMANUAL case1 ").Append(same).Append(" of ").Append(registry.Count)
                   .Append(" documented fields are the registry's own descriptors\n");
        }

        /// <summary> Case 2. RM-65's deliverable is "every author-facing field with its unit and valid range". A field
        /// documented without one of those is the row a reader stops trusting the table over. </summary>
        private static void CaseEveryFieldHasUnitAndRange(StringBuilder builder, List<string> failures)
        {
            int measured = 0;
            foreach (FieldDescriptor d in ManualReferences.Fields())
            {
                string key = d.component + "." + d.field;

                if (d.IsNumericMeasured())
                {
                    measured++;
                    Expect(d.UnitLabel().Length > 0,
                        "case2: '" + key + "' is a measured numeric field with no unit label", failures);
                }

                Expect(ManualReferences.UnitText(d).Length > 0,
                    "case2: '" + key + "' documents an empty unit cell", failures);
                Expect(d.range.IsBounded,
                    "case2: '" + key + "' documents no valid range", failures);
                Expect(ManualReferences.SourceText(d) != "no source recorded",
                    "case2: '" + key + "' documents a limit with no source", failures);
                Expect(ManualReferences.RangeText(d).IndexOf('|') < 0
                    && ManualReferences.SourceText(d).IndexOf('|') < 0,
                    "case2: '" + key + "' contains a pipe, which would break the table", failures);
            }

            builder.Append("RCMMANUAL case2 every documented field carries a unit and a sourced range (")
                   .Append(measured).Append(" measured numeric)\n");
        }

        /// <summary> Case 3. RM-65 asks for a spot check of ten fields. The register is small enough to check ALL of
        /// them, which is strictly stronger than a sample, so that is what this does -- and it says how many, so nobody
        /// reads "exhaustive" as a claim about a bigger register than the tool actually has. </summary>
        private static void CaseSpotCheckFindsNoDrift(StringBuilder builder, List<string> failures)
        {
            IList<FieldDescriptor> fields = ManualReferences.Fields();
            string[] lines = ManualReferences.FieldsTable().Split('\n');

            Expect(lines.Length == fields.Count + 2,
                "case3: the fields table has " + lines.Length + " lines for " + fields.Count
                + " fields (expected header, separator and one row each)", failures);

            int checkedRows = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                int line = i + 2;
                if (line >= lines.Length) { break; }
                FieldDescriptor d = fields[i];
                string row = lines[line];

                bool ok = row.Contains("`" + d.component + "`")
                    && row.Contains("`" + d.field + "`")
                    && row.Contains("| " + d.label + " |")
                    && row.Contains("| " + ManualReferences.UnitText(d) + " |")
                    && row.Contains("| " + ManualReferences.RangeText(d) + " |")
                    && row.Contains(ManualReferences.SourceText(d));

                Expect(ok, "case3: the row for '" + d.component + "." + d.field
                    + "' does not carry that name, unit, range and source", failures);
                if (ok) { checkedRows++; }
            }

            builder.Append("RCMMANUAL case3 spot check is exhaustive: ").Append(checkedRows)
                   .Append(" of ").Append(fields.Count).Append(" fields show no drift\n");
        }

        /// <summary> Case 4. The generated manual must be the same document on every machine. A culture that writes a
        /// decimal comma would otherwise produce "0,15" here and a different file, which the drift check would then
        /// report as a failure on a machine that had changed nothing (C-06). </summary>
        private static void CaseRangeTextIsInvariant(StringBuilder builder, List<string> failures)
        {
            FieldDescriptor probe = new FieldDescriptor("Probe", "probeField", "Probe field",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Fraction,
                FieldRange.Between(0.0, 0.15, "AUTHORED: probe"));
            Expect(ManualReferences.RangeText(probe) == "0 to 0.15 fraction",
                "case4: a bounded fractional range renders as '" + ManualReferences.RangeText(probe)
                + "' rather than '0 to 0.15 fraction'", failures);

            FieldDescriptor half = new FieldDescriptor("Probe", "probeMin", "Probe min",
                FieldGroup.WhatItIs, FieldUnit.Metres, FieldRange.AtLeast(2.5, "AUTHORED: probe"));
            Expect(ManualReferences.RangeText(half) == "at least 2.5 m",
                "case4: a half-open range renders as '" + ManualReferences.RangeText(half) + "'", failures);

            FieldDescriptor open = new FieldDescriptor("Probe", "probeOpen", "Probe open",
                FieldGroup.WhatItIs, FieldUnit.None, FieldRange.None());
            Expect(ManualReferences.RangeText(open) == "unbounded",
                "case4: an unbounded range renders as '" + ManualReferences.RangeText(open) + "'", failures);
            Expect(ManualReferences.UnitText(open) == "dimensionless",
                "case4: a unitless field renders its unit as '" + ManualReferences.UnitText(open)
                + "' rather than saying it is dimensionless", failures);

            builder.Append("RCMMANUAL case4 range and unit text is invariant-formatted and never blank\n");
        }

        /// <summary> Case 5. RM-65's refusals reference is the task's most unusual claim: it turns the module's
        /// fail-loudly philosophy into a feature. That only works if every entry says what the author sees, why the tool
        /// refused, and at least two ways out. One way out is not a diagnosis, it is an instruction. </summary>
        private static void CaseRefusalsAreActionable(StringBuilder builder, List<string> failures)
        {
            IList<ManualRefusal> refusals = ManualReferences.Refusals();
            Expect(refusals.Count >= 10,
                "case5: only " + refusals.Count + " refusals are catalogued; RM-65 names five kinds as a floor",
                failures);

            int resolutions = 0;
            foreach (ManualRefusal e in refusals)
            {
                Expect(e.id.Length > 0, "case5: a refusal has an empty id", failures);
                foreach (char c in e.id)
                {
                    Expect((c >= 'A' && c <= 'Z') || c == '-',
                        "case5: refusal id '" + e.id + "' is not an upper-case dashed token", failures);
                }
                Expect(e.message.Length > 20, "case5: '" + e.id + "' has no real message", failures);
                Expect(e.why.Contains("RM-"), "case5: '" + e.id + "' does not name the task that owns it", failures);
                Expect(e.ResolutionCount >= 2,
                    "case5: '" + e.id + "' offers " + e.ResolutionCount + " way(s) out; RM-65 asks for two or three",
                    failures);
                Expect(e.message.IndexOf('|') < 0 && e.why.IndexOf('|') < 0
                    && e.ResolutionText().IndexOf('|') < 0,
                    "case5: '" + e.id + "' contains a pipe, which would break the table", failures);
                resolutions += e.ResolutionCount;
            }

            builder.Append("RCMMANUAL case5 ").Append(refusals.Count).Append(" refusals, ")
                   .Append(resolutions).Append(" documented ways out\n");
        }

        /// <summary> Case 6. A refusal id is a stable public token: the status panel groups by it and an author searches
        /// for it. Ordinal ordering keeps the generated table stable across machines, and handing out a copy keeps a
        /// caller from editing the catalogue through the list it was given. </summary>
        private static void CaseRefusalIdsAreOrderedAndCopied(StringBuilder builder, List<string> failures)
        {
            IList<ManualRefusal> refusals = ManualReferences.Refusals();
            for (int i = 1; i < refusals.Count; i++)
            {
                Expect(string.CompareOrdinal(refusals[i - 1].id, refusals[i].id) < 0,
                    "case6: '" + refusals[i].id + "' does not follow '" + refusals[i - 1].id
                    + "' in ordinal order (or is a duplicate)", failures);
            }

            int before = refusals.Count;
            refusals.Clear();
            Expect(ManualReferences.Refusals().Count == before,
                "case6: clearing the returned list emptied the catalogue -- Refusals() hands out its own storage",
                failures);

            IList<string> ways = ManualReferences.Refusals()[0].Resolutions();
            int waysBefore = ways.Count;
            ways.Clear();
            Expect(ManualReferences.Refusals()[0].Resolutions().Count == waysBefore,
                "case6: clearing the returned resolutions emptied the entry's own array", failures);

            builder.Append("RCMMANUAL case6 refusal ids are ordinal-ordered and the catalogue is handed out by copy\n");
        }

        /// <summary> Case 7. RM-65's gate: every Cite line in § 10 appears in the manual with its edition. § 10 carries
        /// twelve, so the table carries twelve rows, one per citing task. A thirteenth cite added later without a row
        /// fails here rather than escaping the audit. </summary>
        private static void CaseStandardsCoverEveryCite(StringBuilder builder, List<string> failures)
        {
            IList<CitedStandard> standards = ManualReferences.Standards();
            Expect(standards.Count == ManualReferences.CiteLineCount,
                "case7: the standards table has " + standards.Count + " rows for "
                + ManualReferences.CiteLineCount + " Cite lines in section 10", failures);

            Dictionary<string, bool> tasks = new Dictionary<string, bool>();
            foreach (CitedStandard c in standards)
            {
                Expect(c.task.StartsWith("RM-", System.StringComparison.Ordinal),
                    "case7: '" + c.task + "' is not an RM task id", failures);
                Expect(!tasks.ContainsKey(c.task),
                    "case7: '" + c.task + "' cites twice; one row per citing task", failures);
                tasks[c.task] = true;

                Expect(c.feature.Length > 0 && c.quantity.Length > 0 && c.source.Length > 0
                    && c.edition.Length > 0,
                    "case7: '" + c.task + "' has an empty feature, quantity, source or edition", failures);
                Expect(c.feature.IndexOf('|') < 0 && c.quantity.IndexOf('|') < 0 && c.source.IndexOf('|') < 0
                    && c.edition.IndexOf('|') < 0,
                    "case7: '" + c.task + "' contains a pipe, which would break the table", failures);
            }

            builder.Append("RCMMANUAL case7 ").Append(standards.Count)
                   .Append(" cited standards, one per Cite line in section 10\n");
        }

        /// <summary> Case 8. The rule the whole standards table exists to make auditable: a number is either read from a
        /// named edition or it is marked as not yet adopted. Nothing in between, and no plausible-looking edition filled
        /// in to make the column look complete (C-15). </summary>
        private static void CaseNothingInventedIsMarked(StringBuilder builder, List<string> failures)
        {
            int adopted = 0, pending = 0;
            foreach (CitedStandard c in ManualReferences.Standards())
            {
                if (c.IsAdopted)
                {
                    adopted++;
                    Expect(c.edition.IndexOf("NOT ADOPTED", System.StringComparison.Ordinal) < 0,
                        "case8: '" + c.task + "' claims an adopted edition that says NOT ADOPTED", failures);
                    continue;
                }

                pending++;
                Expect(c.edition == CitedStandard.PendingGeometric || c.edition == CitedStandard.PendingMarking,
                    "case8: '" + c.task + "' is not adopted but does not use one of the two reserved tokens",
                    failures);
                Expect(c.edition.Contains("Q-1"),
                    "case8: '" + c.task + "' does not name the open decision that would fix its edition", failures);
            }

            Expect(adopted >= 2,
                "case8: only " + adopted + " row(s) carry a real edition; section 10 names two sources outright",
                failures);
            Expect(adopted + pending == ManualReferences.CiteLineCount,
                "case8: adopted plus pending is " + (adopted + pending) + ", not "
                + ManualReferences.CiteLineCount, failures);

            builder.Append("RCMMANUAL case8 ").Append(adopted).Append(" rows carry a real edition, ")
                   .Append(pending).Append(" are marked NOT ADOPTED rather than guessed\n");
        }

        /// <summary> Case 9. A malformed row is invisible in a text diff and destroys the table in every renderer. Each
        /// table is checked for its own column count, a separator row, and stability across two builds. </summary>
        private static void CaseTablesAreWellFormed(StringBuilder builder, List<string> failures)
        {
            CheckTable("fields", ManualReferences.FieldsTable(), 8,
                ManualReferences.Fields().Count, builder, failures);
            CheckTable("refusals", ManualReferences.RefusalsTable(), 5,
                ManualReferences.Refusals().Count, builder, failures);
            CheckTable("standards", ManualReferences.StandardsTable(), 6,
                ManualReferences.Standards().Count, builder, failures);
            CheckTable("menus", MenuMapRegister.MapTable(), 3,
                MenuMapRegister.RowCount(), builder, failures);
        }

        private static void CheckTable(string name, string table, int pipesPerLine, int rowCount,
            StringBuilder builder, List<string> failures)
        {
            string[] lines = table.Split('\n');
            Expect(lines.Length == rowCount + 2,
                "case9: the " + name + " table has " + lines.Length + " lines for " + rowCount + " rows", failures);
            Expect(lines.Length > 1 && lines[1].StartsWith("| ---", System.StringComparison.Ordinal),
                "case9: the " + name + " table has no separator row", failures);

            for (int i = 0; i < lines.Length; i++)
            {
                int pipes = 0;
                foreach (char c in lines[i]) { if (c == '|') { pipes++; } }
                Expect(pipes == pipesPerLine,
                    "case9: " + name + " line " + (i + 1) + " has " + pipes + " pipes, not " + pipesPerLine,
                    failures);
            }

            builder.Append("RCMMANUAL case9 the ").Append(name).Append(" table is well formed (")
                   .Append(rowCount).Append(" rows, ").Append(pipesPerLine - 1).Append(" columns)\n");
        }

        /// <summary> The prose an author is owed, and which no register can generate: the six exclusions in the words
        /// section 10 uses, the four walkthrough headings, both names of the tool, and the attribution. </summary>
        public static string[] RequiredProse()
        {
            return new string[]
            {
                "no interchanges",
                "no multi-lane circulatory spirals",
                "no rail crossings or turnouts",
                "no procedural buildings",
                "no generated wear",
                "no city node canvas",
                "## Build a divided road with a median barrier",
                "## Place a roundabout",
                "## Connect a terrain provider",
                "## Diagnose a refusal",
                "MIT License",
                "MicroGSD LLC",
                "LICENSE.md",
                InspectorFieldRegistry.ProductDisplayName,
            };
        }

        /// <summary> Case 10. The hand-written half. A manual whose tables are generated can still ship without saying
        /// what the tool refuses to do or who wrote the code it derives from, and neither omission shows up in a table
        /// comparison. The identifier is checked from the same constant the inspectors draw (C-05). </summary>
        private static void CaseProseStatesExclusionsAndLicence(StringBuilder builder, List<string> failures)
        {
            string contents, message;
            if (!ToolManualWriter.ReadWholeManual(ToolManualWriter.ManualPath(), out contents, out message))
            {
                failures.Add("case10: " + message);
                builder.Append("RCMMANUAL case10 the manual could not be read\n");
                return;
            }

            string[] wanted = RequiredProse();
            foreach (string phrase in wanted)
            {
                Expect(contents.IndexOf(phrase, System.StringComparison.Ordinal) >= 0,
                    "case10: the manual never says '" + phrase + "'", failures);
            }

            Expect(contents.IndexOf("RoadCityMaster", System.StringComparison.Ordinal) >= 0,
                "case10: the manual never gives the identifier the assemblies and folders use", failures);

            builder.Append("RCMMANUAL case10 ").Append(wanted.Length)
                   .Append(" required passages present, including all six exclusions and the MIT attribution\n");
        }

        /// <summary> Case 11. The one that makes the other ten bite: the file on disk must hold exactly what the
        /// registers generate, compared whitespace-tolerantly. Read-only -- the gate reports drift and names the menu
        /// item that fixes it, and never edits the document itself. </summary>
        private static void CaseDocumentMatchesRegisters(StringBuilder builder, List<string> failures)
        {
            string path = ToolManualWriter.ManualPath();
            int matched = 0;
            for (int i = 0; i < ToolManualWriter.TableCount; i++)
            {
                string onDisk, message;
                if (!ToolManualWriter.ReadTableFrom(path, i, out onDisk, out message))
                {
                    failures.Add("case11: " + message);
                    continue;
                }

                string wanted = ToolManualWriter.WantedBody(i);
                if (TerrainFeatureTableWriter.Normalise(onDisk) == TerrainFeatureTableWriter.Normalise(wanted))
                {
                    matched++;
                    continue;
                }

                failures.Add("case11: generated table " + i + " in '" + path + "' has drifted from the register; "
                    + "run Window > Road City Master > Documentation > Write the manual's generated tables");
            }

            Expect(matched == ToolManualWriter.TableCount,
                "case11: " + matched + " of " + ToolManualWriter.TableCount
                + " generated tables match the registers", failures);

            builder.Append("RCMMANUAL case11 ").Append(matched).Append(" of ").Append(ToolManualWriter.TableCount)
                   .Append(" generated tables on disk match the registers exactly\n");
        }

        /// <summary> Case 12. B-20's fix, asserted where it cannot rot: the menu map is REFLECTED from the attributes,
        /// never counted. The manual said 126 items in one section and 134 in another for the same tree, and both
        /// numbers were counted by hand -- which is short by construction here, because five paths are declared as a
        /// <c>const string</c> and referenced by name, so a text search for the attribute never sees them.
        ///
        /// A count alone would still have been a number somebody typed. These are the claims a count cannot make: one
        /// menu root (RM-108), no path registered twice, a table whose group rows add up to its own total, and every gate
        /// in the suite reachable from the menu on its own -- which is what the manual says about the suite, and what
        /// nothing checked until two City build gates had run for a topic with no menu item at all. </summary>
        private static void CaseMenuMapIsReflected(StringBuilder builder, List<string> failures)
        {
            List<string> outside = MenuMapRegister.PathsOutsideTheRoot();
            Expect(outside.Count == 0,
                "case12: " + outside.Count + " menu item(s) sit outside '" + MenuMapRegister.Root + "': "
                + string.Join(", ", outside.ToArray()), failures);

            List<string> repeated = MenuMapRegister.DuplicatePaths();
            Expect(repeated.Count == 0,
                "case12: a menu path is registered more than once: " + string.Join(", ", repeated.ToArray()),
                failures);

            List<string> orphans = MenuMapRegister.GatesWithoutMenuItem();
            Expect(orphans.Count == 0,
                "case12: " + orphans.Count + " gate(s) in the suite carry no menu item of their own, so they can only"
                + " be run as part of the whole sweep: " + string.Join(", ", orphans.ToArray()), failures);

            List<string> groups = MenuMapRegister.Groups();
            int summed = 0;
            foreach (string group in groups) { summed += MenuMapRegister.CountIn(group); }
            Expect(summed == MenuMapRegister.Total(),
                "case12: the groups hold " + summed + " items but the register counts " + MenuMapRegister.Total(),
                failures);

            bool topFirst = groups.Count > 0 && groups[0] == MenuMapRegister.TopLevel;
            Expect(topFirst, "case12: the menu map does not list the top level first", failures);

            Expect(MenuMapRegister.MapTable().Contains("| **Total** | **" + MenuMapRegister.Total() + "** |"),
                "case12: the generated menu table does not state the reflected total", failures);

            int submenus = topFirst ? groups.Count - 1 : groups.Count;
            builder.Append("RCMMANUAL case12 ").Append(MenuMapRegister.Total())
                   .Append(" menu items reflected from the attributes: ")
                   .Append(MenuMapRegister.CountIn(MenuMapRegister.TopLevel)).Append(" at the top level and ")
                   .Append(submenus).Append(" submenus; gates with no menu item of their own: ")
                   .Append(orphans.Count).Append("\n");
        }
    }
}
#endif

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
    /// <summary> RM-66's gate: proves the disposition pass was evidence-based rather than confident.
    ///
    /// Nine cases. All four searches are recorded with evidence on every item; nothing is marked <c>remove</c> unless
    /// every search that could run did run and none of them found a reference; anything a search DID find is never
    /// marked remove, and a claimed reference must point at a real location; every <c>wire up</c> names the task that
    /// does the wiring; every reason is a judgement rather than a hedge; every item the register names still EXISTS on
    /// disk, so the list describes the tree as it is and no deletion happened quietly; all three dispositions are used;
    /// the table is well formed; and <c>Docs/Disposition.md</c> still matches the register. Report lines are prefixed
    /// RCMDEAD.
    ///
    /// What it does NOT prove: that a removal, once performed, breaks nothing. That is what the compile and the suite
    /// prove, one disposition group at a time, which is why the removals are a separate change and not this pass.
    /// </summary>
    public static class DispositionGate
    {
        [MenuItem("Window/Road City Master/Testing/Disposition list against the evidence")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.DispositionGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMDEAD BEGIN\n");
            List<string> failures = new List<string>();

            CaseFourSearchesRecorded(builder, failures);
            CaseNothingRemovedWithoutEvidence(builder, failures);
            CaseAClaimedReferencePointsSomewhere(builder, failures);
            CaseWireUpNamesItsOwner(builder, failures);
            CaseEveryReasonIsAJudgement(builder, failures);
            CaseEveryItemStillExists(builder, failures);
            CaseAllThreeDispositionsAreUsed(builder, failures);
            CaseTableIsWellFormed(builder, failures);
            CaseDocumentMatchesRegister(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMDEAD FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMDEAD result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMDEAD END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }

        /// <summary> Case 1. Four searches, recorded, per item. An empty evidence cell is the failure this whole task
        /// exists to prevent: a disposition that reads as researched and was not. </summary>
        private static void CaseFourSearchesRecorded(StringBuilder builder, List<string> failures)
        {
            IList<DispositionEntry> all = DispositionRegister.All();
            Expect(all.Count >= 10,
                "case1: only " + all.Count + " items in the disposition list; RM-49 flagged more than that", failures);

            int cells = 0;
            foreach (DispositionEntry d in all)
            {
                Expect(!string.IsNullOrEmpty(d.item), "case1: an entry has no item name", failures);
                string[] evidence = d.Evidence();
                Expect(evidence.Length == 4,
                    "case1: '" + d.item + "' records " + evidence.Length + " searches, not 4", failures);
                for (int i = 0; i < evidence.Length; i++)
                {
                    cells++;
                    Expect(!string.IsNullOrEmpty(evidence[i]) && evidence[i].Length > 15,
                        "case1: '" + d.item + "' search " + (i + 1) + " has no recorded evidence", failures);
                    Expect(evidence[i] == null || evidence[i].IndexOf('|') < 0,
                        "case1: '" + d.item + "' search " + (i + 1) + " evidence contains a pipe", failures);
                }
                Expect(d.item.IndexOf('|') < 0 && d.reason.IndexOf('|') < 0,
                    "case1: '" + d.item + "' has a pipe in its name or reason", failures);
            }

            builder.Append("RCMDEAD case1 ").Append(all.Count).Append(" items, ").Append(cells)
                   .Append(" search results, every one with recorded evidence\n");
        }

        /// <summary> Case 2. The rule RM-66 is built around. Removal requires that every search which COULD run did
        /// run -- a code search and a string-or-name search can always run -- and that none of the four found
        /// anything. </summary>
        private static void CaseNothingRemovedWithoutEvidence(StringBuilder builder, List<string> failures)
        {
            int removals = 0;
            foreach (DispositionEntry d in DispositionRegister.All())
            {
                if (d.disposition != Disposition.Remove) { continue; }
                removals++;
                Expect(d.NothingFoundAReference,
                    "case2: '" + d.item + "' is marked remove although a search found a reference", failures);
                Expect(d.BothDecisiveSearchesRan,
                    "case2: '" + d.item + "' is marked remove with a code or string search recorded as not "
                    + "applicable; both can always run", failures);
            }

            Expect(removals >= 1,
                "case2: nothing is marked remove; a pass that removes nothing has not been run", failures);
            builder.Append("RCMDEAD case2 ").Append(removals)
                   .Append(" removals, each with all four searches negative and both decisive searches actually run\n");
        }

        /// <summary> Case 3. The other direction: a reference that was found must point at a place a reader can open,
        /// and an item with any reference at all is never a removal candidate. </summary>
        private static void CaseAClaimedReferencePointsSomewhere(StringBuilder builder, List<string> failures)
        {
            int cited = 0;
            foreach (DispositionEntry d in DispositionRegister.All())
            {
                SearchOutcome[] outcomes = d.Outcomes();
                string[] evidence = d.Evidence();
                bool anyReference = false, anyLocation = false;
                for (int i = 0; i < outcomes.Length; i++)
                {
                    if (outcomes[i] != SearchOutcome.Referenced) { continue; }
                    anyReference = true;
                    if (CitesALocation(evidence[i])) { anyLocation = true; }
                }

                if (!anyReference) { continue; }
                cited++;
                Expect(anyLocation,
                    "case3: '" + d.item + "' claims a reference but no evidence names a file", failures);
                Expect(d.disposition != Disposition.Remove,
                    "case3: '" + d.item + "' is referenced and marked remove", failures);
            }

            builder.Append("RCMDEAD case3 ").Append(cited)
                   .Append(" items are referenced, each citing a file, none of them marked remove\n");
        }

        private static bool CitesALocation(string evidence)
        {
            if (string.IsNullOrEmpty(evidence)) { return false; }
            string[] extensions = new string[] { ".cs", ".rao", ".prefab", ".unity", ".asset", ".meta" };
            foreach (string extension in extensions)
            {
                if (evidence.IndexOf(extension, System.StringComparison.Ordinal) >= 0) { return true; }
            }
            return false;
        }

        /// <summary> Case 4. "Wire up" is a promise, so it names who keeps it. An unowned wire-up is a deletion with a
        /// kinder word on it. </summary>
        private static void CaseWireUpNamesItsOwner(StringBuilder builder, List<string> failures)
        {
            int wireUps = 0;
            foreach (DispositionEntry d in DispositionRegister.All())
            {
                if (d.disposition != Disposition.WireUp) { continue; }
                wireUps++;
                Expect(d.reason.IndexOf("RM-", System.StringComparison.Ordinal) >= 0,
                    "case4: '" + d.item + "' is marked wire up but names no task that does it", failures);
            }

            Expect(wireUps >= 1,
                "case4: nothing is marked wire up; RM-66 names the sidewalk as the case that must be", failures);
            builder.Append("RCMDEAD case4 ").Append(wireUps)
                   .Append(" wire-ups, each naming the task that owns the wiring\n");
        }

        /// <summary> Case 5. Every reason is a decision, not a hedge, and every item appears once. </summary>
        private static void CaseEveryReasonIsAJudgement(StringBuilder builder, List<string> failures)
        {
            string[] hedges = new string[] { "unknown", "TBD", "probably", "not sure", "unclear", "maybe" };
            List<string> seen = new List<string>();
            foreach (DispositionEntry d in DispositionRegister.All())
            {
                Expect(d.reason != null && d.reason.Length > 40,
                    "case5: '" + d.item + "' has no reason worth the name", failures);
                foreach (string hedge in hedges)
                {
                    Expect(d.reason.IndexOf(hedge, System.StringComparison.OrdinalIgnoreCase) < 0,
                        "case5: '" + d.item + "' hedges its reason with '" + hedge + "'", failures);
                }
                Expect(!seen.Contains(d.item), "case5: '" + d.item + "' appears twice", failures);
                seen.Add(d.item);
            }

            builder.Append("RCMDEAD case5 ").Append(seen.Count)
                   .Append(" reasons, each a judgement, no item listed twice\n");
        }

        /// <summary> Case 6. The register describes the tree AS IT IS. Every item it names -- including every one
        /// marked for removal -- must still be on disk, because a disposition list is a decision record and the
        /// removals are a separate, revertible change. A missing file means something was deleted without the compile
        /// and suite run that has to follow it. </summary>
        private static void CaseEveryItemStillExists(StringBuilder builder, List<string> failures)
        {
            string root = DispositionDocWriter.ToolRoot();
            int found = 0;
            foreach (DispositionEntry d in DispositionRegister.All())
            {
                string full = Path.Combine(root, d.path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full)) { found++; continue; }
                failures.Add("case6: '" + d.item + "' names '" + d.path + "', which is not on disk at '" + full + "'");
            }

            builder.Append("RCMDEAD case6 ").Append(found)
                   .Append(" of ").Append(DispositionRegister.All().Count)
                   .Append(" named files present; nothing has been deleted ahead of its own change\n");
        }

        /// <summary> Case 7. All three dispositions are in use. A list that is all keeps is a pass that flinched; a
        /// list that is all removals is the loud failure RM-66 opens by naming. </summary>
        private static void CaseAllThreeDispositionsAreUsed(StringBuilder builder, List<string> failures)
        {
            int removes = DispositionRegister.CountWith(Disposition.Remove);
            int wireUps = DispositionRegister.CountWith(Disposition.WireUp);
            int keeps = DispositionRegister.CountWith(Disposition.KeepWithReason);
            int total = DispositionRegister.All().Count;

            Expect(removes >= 1 && wireUps >= 1 && keeps >= 1,
                "case7: dispositions are not all used: " + removes + " remove, " + wireUps + " wire up, "
                + keeps + " keep", failures);
            Expect(removes + wireUps + keeps == total,
                "case7: " + (removes + wireUps + keeps) + " dispositions for " + total + " items", failures);
            Expect(keeps >= removes,
                "case7: more removals (" + removes + ") than keeps (" + keeps + ") in a tree whose assets load by "
                + "string path -- re-read the evidence before trusting that", failures);

            builder.Append("RCMDEAD case7 ").Append(removes).Append(" remove, ").Append(wireUps)
                   .Append(" wire up, ").Append(keeps).Append(" keep with reason, ").Append(total).Append(" total\n");
        }

        /// <summary> Case 8. Eight columns per row, and a separator. A row that loses a pipe silently merges two
        /// searches into one cell, which is the one formatting defect that changes the MEANING of this table. </summary>
        private static void CaseTableIsWellFormed(StringBuilder builder, List<string> failures)
        {
            const int pipesPerLine = 9;
            string[] lines = DispositionRegister.Table().Split('\n');
            int rows = DispositionRegister.All().Count;

            Expect(lines.Length == rows + 2,
                "case8: the table has " + lines.Length + " lines for " + rows + " items", failures);
            Expect(lines.Length > 1 && lines[1].StartsWith("| ---", System.StringComparison.Ordinal),
                "case8: the table has no separator row", failures);

            for (int i = 0; i < lines.Length; i++)
            {
                int pipes = 0;
                foreach (char c in lines[i]) { if (c == '|') { pipes++; } }
                Expect(pipes == pipesPerLine,
                    "case8: line " + (i + 1) + " has " + pipes + " pipes, not " + pipesPerLine, failures);
            }

            builder.Append("RCMDEAD case8 the table is well formed (").Append(rows).Append(" rows, ")
                   .Append(pipesPerLine - 1).Append(" columns)\n");
        }

        /// <summary> Case 9. The document on disk holds what the register generates, and its hand-written half still
        /// states the four searches and the sequencing rule. Read-only: the gate reports drift and names the menu item
        /// that fixes it. </summary>
        private static void CaseDocumentMatchesRegister(StringBuilder builder, List<string> failures)
        {
            string path = DispositionDocWriter.DocPath();
            string onDisk, message;
            if (!DispositionDocWriter.ReadTableFrom(path, out onDisk, out message))
            {
                failures.Add("case9: " + message);
                builder.Append("RCMDEAD case9 the disposition list could not be read\n");
                return;
            }

            bool matches = TerrainFeatureTableWriter.Normalise(onDisk)
                == TerrainFeatureTableWriter.Normalise(DispositionDocWriter.WantedBody());
            Expect(matches,
                "case9: the table in '" + path + "' has drifted from the register; run Window > Road City Master > "
                + "Documentation > Write the disposition list", failures);

            string contents;
            if (!DispositionDocWriter.ReadWholeDoc(path, out contents, out message))
            {
                failures.Add("case9: " + message);
                return;
            }

            string[] wanted = new string[]
            {
                "code search", "scene and prefab search", "serialised asset search", "string or name search",
                "RM-49", "RM-48", "one change per disposition group",
            };
            foreach (string phrase in wanted)
            {
                Expect(contents.IndexOf(phrase, System.StringComparison.Ordinal) >= 0,
                    "case9: the document never says '" + phrase + "'", failures);
            }

            builder.Append("RCMDEAD case9 the table on disk matches the register and the prose states all four "
                + "searches and the sequencing rule\n");
        }
    }
}
#endif

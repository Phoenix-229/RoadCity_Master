// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-22's gate: proves every editor command this tool ships is reachable from the tool's own
    /// author-facing UI, and stays reachable.
    ///
    /// The defect it exists to stop is not a broken command -- every command it names worked perfectly. RM-108 deleted
    /// twenty menu rows that could not say what they would act on until after they had been pressed, which was the
    /// right call, and wrote comments saying the RoadSystem inspector hosted the replacements. It did not. So between
    /// that task and B-22 the corridor overlay, the alignment ribbon and the three OBJ exporters had no way in at all:
    /// code that compiled, passed its own gates, was described in the manual, and could not be reached by an author
    /// from anywhere in the editor.
    ///
    /// That is invisible at runtime and invisible to the compiler -- a public static method with no caller is not an
    /// error -- so it is gated in the source, against the one register that answers the question honestly: the fifteen
    /// author-facing editor files in InspectorConsistencyChecklist.Rows(). A reference from a gate, a writer or another
    /// scene tool does not count and must not, because none of those is a surface an author touches, and counting them
    /// is how "it is referenced somewhere" comes to be read as "it can be used".
    ///
    /// Code lines only, through LegacyInspectorGate's stripper. This tool's comments quote deleted menu rows and dead
    /// member names constantly -- that is how a removal stays legible -- and a scan that read a tombstone as a caller
    /// would have passed on the three files B-22 had to correct.
    ///
    /// Report lines are prefixed RCMENTRY. </summary>
    public static class EditorEntryPointGate
    {
        #region "What this gate reads"
        private const string Prefix = "RCMENTRY";

        /// <summary> This gate's own file. It holds every needle below as a string literal, so a scan that counted it
        /// would report the commands reachable BECAUSE the gate looks for them -- the one false positive that would make
        /// this whole file worthless. Two things stop it and both are needed: the file is on the checklist's
        /// not-author-facing list, which keeps it out of the set of surfaces, and <see cref="AuthorFacingSurfaces"/>
        /// asserts that membership rather than trusting it, so taking it off that list fails this gate out loud instead
        /// of quietly turning it into a check that passes because of itself. </summary>
        private const string ThisFile = "EditorEntryPointGate.cs";

        /// <summary> Where the scene tools live, relative to the tool root. They are not in the editor assembly -- they
        /// sit in the runtime one behind #if UNITY_EDITOR -- which is why the folder is named here rather than taken
        /// from the editor-script folder the rest of this family reads. </summary>
        private static readonly string SceneToolFolder = Path.Combine("City", Path.Combine("Editor", "SceneTools"));

        /// <summary> How a scene tool's switch is recognised: the pair of no-argument statics it subscribes and
        /// unsubscribes its scene GUI with, and the accessor a control over them reads. Case 1 finds its subjects with
        /// these rather than from a list, so a third scene tool written next year is checked without an edit here --
        /// which is the point, given that the fault this gate is about was nobody re-reading a claim gone stale.
        /// </summary>
        private const string ShowDeclaration = "public static void Show()";
        private const string HideDeclaration = "public static void Hide()";
        private const string ShownDeclaration = "public static bool IsShown";
        #endregion


        [MenuItem("Window/Road City Master/Testing/Scene tools and exporters have a way in")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.EditorEntryPointGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append(Prefix).Append(" BEGIN\n");
            List<string> failures = new List<string>();

            Dictionary<string, string> surfaces = AuthorFacingSurfaces(failures);
            CaseEverySceneToolHasASwitch(surfaces, builder, failures);
            CaseEveryNamedCommandIsCalled(surfaces, builder, failures);
            CaseTheSectionsStateWhatTheyNeed(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append(Prefix).Append(" FAIL   ").Append(failure).Append("\n");
            }
            builder.Append(Prefix).Append(" result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append(Prefix).Append(" END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        #region "Case 1 -- every scene tool can be switched on AND off from one author-facing file"
        /// <summary> For each scene tool declaring a Show()/Hide() pair, ONE author-facing file must name all three of
        /// Show(, Hide( and IsShown -- and the same file must name all three.
        ///
        /// "Same file, all three" is the case, not tidiness. A file that calls Show() and nothing else has given the
        /// author a one-way trip: the tool comes on and there is no control to turn it off. A file that calls both but
        /// never reads IsShown has given them two buttons that cannot say which state the tool is in, so the only way
        /// to find out is to look at the scene and guess. One toggle over the accessor answers both, and requiring the
        /// three together is how that shape is held. </summary>
        private static void CaseEverySceneToolHasASwitch(Dictionary<string, string> surfaces, StringBuilder builder,
            List<string> failures)
        {
            string folder = Path.Combine(DispositionDocWriter.ToolRoot(), SceneToolFolder);
            if (!Directory.Exists(folder))
            {
                failures.Add("scene-tool-switch: '" + folder + "' does not exist, so no scene tool could be checked");
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            int tools = 0;
            int switched = 0;
            for (int f = 0; f < files.Length; f++)
            {
                string body = string.Join("\n", LegacyInspectorGate.CodeLinesAt(files[f], failures));
                if (body.IndexOf(ShowDeclaration, System.StringComparison.Ordinal) < 0) { continue; }

                tools++;
                string type = Path.GetFileNameWithoutExtension(files[f]);
                Expect(body.IndexOf(HideDeclaration, System.StringComparison.Ordinal) >= 0,
                    "scene-tool-switch: " + type + " declares '" + ShowDeclaration + "' and no '" + HideDeclaration
                    + "', so whatever turns it on cannot turn it off", failures);
                Expect(body.IndexOf(ShownDeclaration, System.StringComparison.Ordinal) >= 0,
                    "scene-tool-switch: " + type + " declares '" + ShowDeclaration + "' and no '" + ShownDeclaration
                    + "', so a control over it cannot say which state it is in", failures);

                if (SurfaceNamingAll(surfaces, Path.GetFileName(files[f]),
                    new string[] { type + ".Show(", type + ".Hide(", type + ".IsShown" }) != null)
                {
                    switched++;
                    continue;
                }

                failures.Add("scene-tool-switch: no author-facing editor file names all three of " + type + ".Show(, "
                    + type + ".Hide( and " + type + ".IsShown, so the tool is in the build and an author cannot switch "
                    + "it on. That is the state RM-108 left the corridor overlay and the alignment ribbon in, with "
                    + "three comments saying otherwise (B-22)");
            }

            builder.Append(Prefix).Append(' ').Append("scene-tool-switch".PadRight(24))
                   .Append(switched).Append(" of ").Append(tools)
                   .Append(" scene tools can be switched from an author-facing file\n");
        }
        #endregion


        #region "Case 2 -- the named commands are called from an author-facing file"
        /// <summary> The commands that are not a Show()/Hide() pair and so cannot be found by the folder scan above:
        /// the overlay's display choice and its readout, the ribbon's last suggestion, the three OBJ exporters, and the
        /// credit link that is the reason those exporters may be shipped at all.
        ///
        /// Each row is checked twice. First that the declaring file really declares the member -- otherwise a rename
        /// leaves the reference check hunting something that no longer exists and reporting its absence as a missing
        /// caller, which sends the next reader to the wrong file entirely. Then that an author-facing file names it.
        ///
        /// Needles carry the type name, and a bracket where the member is a method, because a bare member name is a
        /// substring of too much: "SuitabilityOverlay.Display" matches every call to DisplayName, so the display
        /// accessor is deliberately NOT a row -- it is read in the same expression as SetDisplay, which is. A row that
        /// can be satisfied by something other than itself is worse than no row.
        ///
        /// Three members are missing from this table on purpose: SetMap, SetPlan and SetCorridor, the calls that would
        /// hand a baked raster and a solved alignment to the two scene tools. Nothing calls them, so a row for each
        /// would fail today -- and it would be the right failure for the wrong task. That feed is B-28; when it lands,
        /// it adds its three rows here. </summary>
        private static readonly string[][] NamedCommands =
        {
            new string[] { "SuitabilityOverlay.cs", "SetDisplay(", "which measurement the overlay colours by" },
            new string[] { "SuitabilityOverlay.cs", "DisplayName(", "the wording of each measurement" },
            new string[] { "SuitabilityOverlay.cs", "CursorLine", "the reading under the cursor" },
            new string[] { "AlignmentHandles.cs", "LastSuggestion", "what guided placement did to the last drag" },
            new string[] { "ObjExporter.cs", "ExportSelectionToSeparate(", "one OBJ file per mesh" },
            new string[] { "ObjExporter.cs", "ExportEachSelectionToSingle(", "one OBJ file per selected object" },
            new string[] { "ObjExporter.cs", "ExportWholeSelectionToSingle(", "one OBJ file for the whole selection" },
            new string[] { "ObjExporter.cs", "OpenLink(", "the source credit those writers are shipped under" }
        };

        private static void CaseEveryNamedCommandIsCalled(Dictionary<string, string> surfaces, StringBuilder builder,
            List<string> failures)
        {
            int called = 0;
            for (int c = 0; c < NamedCommands.Length; c++)
            {
                string file = NamedCommands[c][0];
                string member = NamedCommands[c][1];
                string purpose = NamedCommands[c][2];
                string needle = Path.GetFileNameWithoutExtension(file) + "." + member;

                string body = string.Join("\n", LegacyInspectorGate.CodeLinesAt(DeclaringPath(file), failures));
                if (body.IndexOf(member, System.StringComparison.Ordinal) < 0)
                {
                    failures.Add("named-command: " + file + " no longer declares " + member + ", so this row is looking "
                        + "for something that does not exist. Rename the row with the member, or delete it with the "
                        + "member");
                    continue;
                }

                if (SurfaceNamingAll(surfaces, file, new string[] { needle }) != null)
                {
                    called++;
                    continue;
                }

                failures.Add("named-command: no author-facing editor file names " + needle + " (" + purpose
                    + "), so it ships with no way in");
            }

            builder.Append(Prefix).Append(' ').Append("named-command-called".PadRight(24))
                   .Append(called).Append(" of ").Append(NamedCommands.Length)
                   .Append(" named commands are called from an author-facing file\n");
        }
        #endregion


        #region "Case 3 -- the two conditional sections still state what they need"
        /// <summary> The corridor and alignment sections draw only once there is a raster or a plan to draw. This case
        /// holds those two conditions in place.
        ///
        /// It is not caution being enforced. A section offering to switch on an overlay that will draw nothing is the
        /// same defect as the menu rows RM-108 deleted -- a control that cannot say whether it will do anything until
        /// after it has been pressed -- so dropping the condition would put back in the inspector exactly the fault the
        /// deletion took out of the menu. Both values arrive from a bake and nothing hands them over yet (B-28), which
        /// is also the honest reason both sections are closed today. </summary>
        private static readonly string[][] Preconditions =
        {
            new string[] { "SuitabilityOverlay.Map",
                "the corridor section is offered whether or not a bake has left a raster to draw" },
            new string[] { "AlignmentHandles.Plan",
                "the alignment section is offered whether or not a bake has left a plan to draw" }
        };

        /// <summary> The inspector that draws both sections. Named once, here, rather than in each row. </summary>
        private const string SectionHost = "RoadSystemEditor.cs";

        private static void CaseTheSectionsStateWhatTheyNeed(StringBuilder builder, List<string> failures)
        {
            string body = string.Join("\n", LegacyInspectorGate.CodeLinesAt(
                Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), SectionHost), failures));

            int stated = 0;
            for (int p = 0; p < Preconditions.Length; p++)
            {
                if (body.IndexOf(Preconditions[p][0] + " != null", System.StringComparison.Ordinal) >= 0)
                {
                    stated++;
                    continue;
                }
                failures.Add("precondition-stated: " + SectionHost + " does not test " + Preconditions[p][0]
                    + " != null, so " + Preconditions[p][1]);
            }

            builder.Append(Prefix).Append(' ').Append("precondition-stated".PadRight(24))
                   .Append(stated).Append(" of ").Append(Preconditions.Length)
                   .Append(" conditional sections still state what they need\n");
        }
        #endregion


        #region "Shared readers"
        /// <summary> The tool's author-facing editor files and their code, read once for all three cases.
        ///
        /// The set comes from InspectorConsistencyChecklist.Rows() and nowhere else, because "reachable" has to mean
        /// reachable by an AUTHOR. Every other candidate set answers a weaker question and would have passed happily
        /// through the whole span this gate describes: the tree counts a gate that quotes a member name, and the editor
        /// folder counts the writers and registers. The corridor overlay was named by its own gate throughout, and no
        /// author could reach it.
        ///
        /// That register is also why this gate keeps no list of surfaces to maintain: a sixteenth author-facing editor
        /// is added there as a row because the checklist requires it, and this scan picks it up. </summary>
        private static Dictionary<string, string> AuthorFacingSurfaces(List<string> failures)
        {
            Dictionary<string, string> surfaces = new Dictionary<string, string>();
            Expect(new List<string>(InspectorConsistencyChecklist.NotAuthorFacing()).Contains(ThisFile),
                "surfaces: " + ThisFile + " is not on the checklist's not-author-facing list, so this gate's own file "
                + "can be read as one of the surfaces below -- and it names every command it looks for, so all three "
                + "cases would then pass because the gate exists", failures);

            IList<InspectorRecord> rows = InspectorConsistencyChecklist.Rows();
            for (int r = 0; r < rows.Count; r++)
            {
                string file = rows[r].file;
                if (string.IsNullOrEmpty(file) || file == ThisFile || surfaces.ContainsKey(file)) { continue; }
                surfaces[file] = string.Join("\n", LegacyInspectorGate.CodeLinesAt(
                    Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), file), failures));
            }

            Expect(surfaces.Count > 0,
                "surfaces: the checklist named no author-facing editor file, so every case below would report a missing "
                + "caller for a reason that has nothing to do with the caller", failures);
            return surfaces;
        }


        /// <summary> The first author-facing file naming EVERY needle given, or null when none does. The file that
        /// declares the member is skipped: a method naming itself is not a way in. </summary>
        private static string SurfaceNamingAll(Dictionary<string, string> surfaces, string declaringFile,
            string[] needles)
        {
            foreach (KeyValuePair<string, string> surface in surfaces)
            {
                if (surface.Key == declaringFile) { continue; }
                bool all = true;
                for (int n = 0; n < needles.Length && all; n++)
                {
                    all = surface.Value.IndexOf(needles[n], System.StringComparison.Ordinal) >= 0;
                }
                if (all) { return surface.Key; }
            }
            return null;
        }


        /// <summary> Where a named file is read from. Two folders, because the scene tools are not in the editor
        /// assembly and the exporter is: the file's own name decides, and a name in neither folder falls through to the
        /// editor folder, where the reader reports it missing by full path rather than silently reading nothing.
        /// </summary>
        private static string DeclaringPath(string file)
        {
            string sceneTool = Path.Combine(Path.Combine(DispositionDocWriter.ToolRoot(), SceneToolFolder), file);
            return File.Exists(sceneTool)
                ? sceneTool
                : Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), file);
        }


        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
        #endregion
    }
}
#endif

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Proves that no editor-only file of this tool can reach a player build.
    ///
    /// RM-06 split the tool into RoadCityMaster (all platforms) and RoadCityMaster.Editor
    /// (includePlatforms Editor). That boundary is real but it is invisible: nothing in the
    /// editor complains if a later refactor moves an editor file into the runtime assembly, and
    /// the symptom only appears in a player build, which is the most expensive place to find it.
    /// This audit reads the compiler's own assembly graph instead, so the check costs a second
    /// and can run on demand or headless.
    ///
    /// Note what it does NOT do: it does not build a player. It measures the only stage at which
    /// an editor type could enter one -- which assemblies the player compiles, and which source
    /// files feed them.
    ///
    /// RM-104 added a third question, the mirror of the first two: not "can an editor file reach a
    /// player?" but "does a player file reach for the editor?". A runtime file that names
    /// UnityEditor behind a conditional compiles cleanly today and breaks the day the conditional
    /// is edited, so the census is pinned here rather than left to memory. </summary>
    public static class AssemblyBoundaryAudit
    {
        /// <summary> Substring that marks a path as editor-only by folder convention. Compared
        /// against forward-slash asset paths, which is what CompilationPipeline reports. </summary>
        private const string editorFolderMarker = "/Editor/";


        /// <summary> The one runtime file this tool allows to name <c>UnityEditor</c>: the facade whose whole purpose
        /// is to hold those calls behind <c>#if UNITY_EDITOR</c> so that its callers -- around a hundred sites in
        /// <c>Scripts/</c> -- carry no conditional of their own. RM-104 drove every other runtime file to zero, and
        /// CHECK 3 below pins that figure. Matched as a path suffix against forward-slash asset paths. </summary>
        private const string editorFacadeFile = "/Scripts/EngineIntegration.cs";


        [MenuItem("Window/Road City Master/Testing/Audit assembly boundary")]
        public static void AuditFromMenu()
        {
            string report;
            if (Audit(out report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.AssemblyBoundaryAudit.AuditBatch. Exits 1 on failure so a build script
        /// can gate on it. </summary>
        public static void AuditBatch()
        {
            string report;
            bool passed = Audit(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }


        /// <summary> Suite entry point. <c>RcmGateSuite</c> discovers every <c>public static bool Run(out string)</c>
        /// in this tool's two assemblies, so the name is what puts the boundary and the RM-104 census into the
        /// self-test instead of leaving them behind a menu item nobody clicks. </summary>
        public static bool Run(out string report)
        {
            return Audit(out report);
        }


        /// <summary> Runs all three checks. Returns true when the boundary holds. The report is one
        /// block of text whose lines are prefixed RCMAUDIT, so a log grep can read the result
        /// without parsing Unity's surrounding noise. </summary>
        public static bool Audit(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMAUDIT BEGIN\n");

            // Only this tool's own paths are judged. Unity's Burst package deliberately keeps
            // files in a Runtime/Editor folder inside a runtime assembly, and that is its
            // business, not a defect this audit should report.
            // GetBasePath returns the folder with the OS separator, so normalise; and end it with
            // a slash so a sibling folder whose name merely starts the same cannot match.
            string toolBase = RoadEditorUtility.GetBasePath().Replace('\\', '/');
            if (!toolBase.EndsWith("/"))
            {
                toolBase = toolBase + "/";
            }

            builder.Append("RCMAUDIT base     ").Append(toolBase).Append("\n");

            List<string> failures = new List<string>();

            // Which assemblies does this tool own on the editor side? Anything that compiles a
            // file under one of its Editor folders. Derived rather than hard-coded, so renaming
            // the editor assembly or adding a second one cannot silently narrow the audit.
            HashSet<string> editorSideAssemblies = new HashSet<string>();
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                foreach (string sourceFile in assembly.sourceFiles)
                {
                    string path = sourceFile.Replace('\\', '/');
                    if (path.StartsWith(toolBase) && path.Contains(editorFolderMarker))
                    {
                        editorSideAssemblies.Add(assembly.name);
                        break;
                    }
                }
            }

            foreach (string name in editorSideAssemblies)
            {
                builder.Append("RCMAUDIT editor   ").Append(name).Append("\n");
            }

            if (editorSideAssemblies.Count == 0)
            {
                failures.Add("no assembly compiles any file under " + toolBase +
                             "**/Editor/ -- either the tool moved or the audit is looking in the wrong place");
            }

            // CHECK 1  no assembly that holds this tool's editor code may be part of a player.
            // CHECK 2  no file under one of its Editor folders may be a compilation input to any
            //          player assembly. Check 1 catches a deleted includePlatforms entry; check 2
            //          catches a file that drifted into the runtime assembly, for instance if the
            //          City/Editor asmref were removed.
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (editorSideAssemblies.Contains(assembly.name))
                {
                    failures.Add("assembly " + assembly.name +
                                 " holds editor code and is compiled for the player");
                }

                foreach (string sourceFile in assembly.sourceFiles)
                {
                    string path = sourceFile.Replace('\\', '/');
                    if (path.StartsWith(toolBase) && path.Contains(editorFolderMarker))
                    {
                        failures.Add("player assembly " + assembly.name + " compiles " + path);
                    }
                }
            }

            // CHECK 3  the UnityEditor-from-runtime census (RM-104). A runtime file that reaches for UnityEditor
            //          behind #if UNITY_EDITOR still compiles in the editor, so checks 1 and 2 stay quiet about it;
            //          the bill arrives later, when someone forgets the guard or ports the tool. RM-104 drove that
            //          count to zero in every runtime file but ONE stated remainder, the EngineIntegration facade.
            //          This check pins the figure so it cannot climb again in silence.
            List<string> facadeSeen = new List<string>();
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                foreach (string sourceFile in assembly.sourceFiles)
                {
                    string path = sourceFile.Replace('\\', '/');
                    if (!path.StartsWith(toolBase) || path.Contains(editorFolderMarker)) { continue; }

                    int firstLine;
                    int reaches = CountEditorReaches(path, out firstLine);

                    if (path.EndsWith(editorFacadeFile))
                    {
                        facadeSeen.Add(path);
                        builder.Append("RCMAUDIT facade   ").Append(path)
                               .Append("  reaches ").Append(reaches).Append("\n");
                        if (reaches == 0)
                        {
                            failures.Add(path + " is the stated UnityEditor remainder yet names UnityEditor nowhere" +
                                         " -- the facade moved, so this census is measuring nothing");
                        }
                        continue;
                    }

                    if (reaches > 0)
                    {
                        failures.Add("runtime file " + path + " names UnityEditor " + reaches +
                                     " time(s), first at line " + firstLine +
                                     " -- route it through EngineIntegration instead (RM-104)");
                    }
                }
            }

            if (facadeSeen.Count == 0)
            {
                failures.Add("no player assembly compiles " + editorFacadeFile +
                             " -- the stated UnityEditor remainder has been renamed or removed");
            }

            foreach (string failure in failures)
            {
                builder.Append("RCMAUDIT FAIL     ").Append(failure).Append("\n");
            }

            builder.Append("RCMAUDIT result   ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMAUDIT END");

            report = builder.ToString();
            return failures.Count == 0;
        }


        /// <summary> How many lines of this file name <c>UnityEditor</c> outside a comment, and the first such line,
        /// 1-based (0 when there is none). Comments are stripped because several runtime files discuss this very
        /// boundary in prose -- <c>Road.cs</c>, <c>BuildDispatch.cs</c> and <c>DataAssetAudit.cs</c> all do, and a
        /// sentence explaining that a file does not name the namespace must not be read as naming it. Everything left
        /// counts, string literals included: a runtime file has no other business spelling it. A <c>//</c> inside a
        /// string can hide the rest of its own line, so the count is a floor, not a ceiling -- fine for a check whose
        /// job is to notice a reach appearing, not to measure exactly how many. A file the compiler listed but that
        /// cannot be read counts zero: that is a broken checkout, not a boundary defect. </summary>
        private static int CountEditorReaches(string _projectRelativePath, out int _firstLine)
        {
            _firstLine = 0;
            string[] lines;
            try { lines = File.ReadAllLines(_projectRelativePath); }
            catch (IOException) { return 0; }
            catch (System.UnauthorizedAccessException) { return 0; }

            int count = 0;
            bool isInBlockComment = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (isInBlockComment)
                {
                    int end = line.IndexOf("*/");
                    if (end < 0) { continue; }
                    line = line.Substring(end + 2);
                    isInBlockComment = false;
                }

                int open = line.IndexOf("/*");
                if (open >= 0)
                {
                    int close = line.IndexOf("*/", open + 2);
                    if (close < 0) { line = line.Substring(0, open); isInBlockComment = true; }
                    else { line = line.Substring(0, open) + line.Substring(close + 2); }
                }

                int comment = line.IndexOf("//");
                if (comment >= 0) { line = line.Substring(0, comment); }

                if (line.Contains("UnityEditor"))
                {
                    count++;
                    if (_firstLine == 0) { _firstLine = i + 1; }
                }
            }

            return count;
        }
    }
}
#endif

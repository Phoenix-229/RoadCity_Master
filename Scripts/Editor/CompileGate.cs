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
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-71's gate: the one batch-mode compile checkpoint, graded by reading its log.
    ///
    /// The task's rule is that "an empty exit code with no log is not a pass", and that rule is worth nothing while the
    /// only thing enforcing it is a person's attention at the end of a long session. So the compile is still a command
    /// someone runs -- there is no way to compile from inside a running editor without lying about it -- but the
    /// EVIDENCE is graded here: the log has to exist, be large enough to have got past startup, name this project,
    /// carry a completed domain reload, and contain no `error CS` and no warning belonging to this tool.
    ///
    /// The domain reload is the honest signal and the exit code is not. A reload only happens once the assemblies
    /// compiled, so a run that later died of an out-of-memory fault still proves a clean compile, and a run that never
    /// reached one did not compile at all whatever it returned. Both of those have happened here.
    ///
    /// In batch mode the compile finishes long before `-executeMethod` is called, so this gate normally grades the log
    /// of the very run it is inside, taken from that run's own `-logFile` argument. From the menu, where there is no
    /// such argument, it grades the newest retained log in the project's `Logs` folder instead.
    /// </summary>
    public static class CompileGate
    {
        /// <summary> Warnings in this tool that are accepted with a reason, as `identity` then `why`. Deliberately
        /// EMPTY: every warning found so far was a real defect with a real fix -- an obsolete `FindObjectsByType`
        /// overload, and a type name that collided across the assembly boundary -- and an allowance list that starts
        /// populated is a list that never empties. An entry here has to earn its place. </summary>
        private static readonly string[][] AcceptedToolWarnings = new string[0][];

        [MenuItem("Window/Road City Master/Testing/Batch-mode compile evidence", false, 2270)]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            if (passed) { Debug.Log(report); } else { Debug.LogError(report); }
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCOMPILE BEGIN\n");

            CompileLogReading reading = CompileLog.Read(CompileLog.ResolveLogPath());

            CaseALogExists(builder, failures, reading);
            CaseTheLogIsCredible(builder, failures, reading);
            CaseTheRunWasThisProjectInBatchMode(builder, failures, reading);
            CaseTheLogWasKeptInsideTheProject(builder, failures, reading);
            CaseNoCompileErrors(builder, failures, reading);
            CaseNoToolWarnings(builder, failures, reading);
            CaseTheCompileActuallyCompleted(builder, failures, reading);
            CaseBenignNoiseIsClassified(builder, failures);
            CaseTheReaderCatchesAPlantedFailure(builder, failures);
            CaseTheLaunchRecipeIsIntact(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMCOMPILE FAIL  ").Append(failures[i]).Append('\n');
            }
            builder.Append("RCMCOMPILE result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append('\n');
            builder.Append("RCMCOMPILE END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Case 1. There is a log. This is first because it is the failure the task singles out: a launch whose
        /// project path was split at a space returns in seconds having written nothing, and every later check would
        /// otherwise have nothing to disagree with. </summary>
        private static void CaseALogExists(StringBuilder builder, List<string> failures, CompileLogReading reading)
        {
            Expect(reading.logPath.Length > 0,
                "log: no batch-mode log could be found in " + CompileLog.LogFolder()
                + " and this run has no -logFile of its own; an empty exit code with no log is not a pass", failures);
            Expect(reading.exists || reading.logPath.Length == 0,
                "log: " + reading.logPath + " was named but does not exist", failures);
            if (reading.abortLine.StartsWith("the log could not be read", StringComparison.Ordinal))
            {
                failures.Add("log: " + reading.abortLine);
            }
            builder.Append("RCMCOMPILE case 1 log  ")
                   .Append(reading.exists ? Path.GetFileName(reading.logPath) : "MISSING")
                   .Append("  bytes ").Append(reading.bytes).Append("  lines ").Append(reading.lines).Append('\n');
        }

        /// <summary> Case 2. The log is big enough and structured enough to be a real run. The stub written by a broken
        /// launch is about a kilobyte and has no argument block, so both halves are needed: a size floor alone would pass
        /// any large unrelated file, and a structure check alone would pass a truncated one. </summary>
        private static void CaseTheLogIsCredible(StringBuilder builder, List<string> failures, CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 2 credible  not graded, no log\n"); return; }

            Expect(reading.bytes >= CompileLog.MinimumCredibleBytes,
                "credible: the log is only " + reading.bytes + " bytes, below the " + CompileLog.MinimumCredibleBytes
                + " a run that got past startup writes -- this is the stub a split project path leaves", failures);
            Expect(reading.lines > 0, "credible: the log has no lines", failures);
            Expect(reading.sawArgumentBlock,
                "credible: the log has no COMMAND LINE ARGUMENTS block, so what it graded cannot be established",
                failures);
            builder.Append("RCMCOMPILE case 2 credible  argument block ").Append(reading.sawArgumentBlock)
                   .Append("  distinct diagnostics ").Append(reading.diagnostics.Count).Append('\n');
        }

        /// <summary> Case 3. The run was batch mode against THIS project, and it was not aborted. The project-path check
        /// is what catches the launch bug by its signature rather than by its symptom: the shell appends a truncated path
        /// to the working directory, so the log names a project that does not exist. </summary>
        private static void CaseTheRunWasThisProjectInBatchMode(StringBuilder builder, List<string> failures,
            CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 3 project  not graded, no log\n"); return; }

            Expect(reading.sawBatchMode, "project: the log's arguments do not include -batchmode", failures);
            Expect(!reading.sawAbort, "project: the run aborted -- " + reading.abortLine, failures);
            Expect(reading.projectPathAccepted.Length > 0,
                "project: the log never says it changed project path, so it never opened a project", failures);
            Expect(SamePath(reading.projectPathAccepted, CompileLog.ProjectRoot()),
                "project: the log opened " + reading.projectPathAccepted + ", not this project at "
                + CompileLog.ProjectRoot(), failures);
            builder.Append("RCMCOMPILE case 3 project  batchmode ").Append(reading.sawBatchMode)
                   .Append("  opened ").Append(reading.projectPathAccepted.Length > 0
                       ? Path.GetFileName(reading.projectPathAccepted.TrimEnd('/', '\\')) : "nothing")
                   .Append("  aborted ").Append(reading.sawAbort).Append('\n');
        }

        /// <summary> Case 4. The log was kept where a reader will find it -- inside the project. A log in a temp folder
        /// is not retained evidence, it is a file that happened to be written, and the task asks for the log to be kept.
        /// </summary>
        private static void CaseTheLogWasKeptInsideTheProject(StringBuilder builder, List<string> failures,
            CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 4 retained  not graded, no log\n"); return; }

            string folder = CompileLog.LogFolder();
            bool inside = UnderFolder(reading.logPath, folder);
            Expect(inside, "retained: the log is at " + reading.logPath + ", not in the project's " + folder, failures);
            if (reading.logFileArgument.Length > 0)
            {
                Expect(UnderFolder(reading.logFileArgument, folder),
                    "retained: the run asked for its log at " + reading.logFileArgument + ", outside " + folder,
                    failures);
            }
            builder.Append("RCMCOMPILE case 4 retained  inside the project ").Append(inside)
                   .Append("  requested ").Append(reading.logFileArgument.Length > 0
                       ? Path.GetFileName(reading.logFileArgument) : "not stated").Append('\n');
        }

        /// <summary> Case 5. No `error CS` anywhere in the log, from any tree. Errors elsewhere are reported separately
        /// rather than excused: a compile error in the host package stops this tool's assemblies from loading just as
        /// effectively as one of its own, and a runtime-assembly error hides every editor-assembly error behind it.
        /// </summary>
        private static void CaseNoCompileErrors(StringBuilder builder, List<string> failures, CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 5 errors  not graded, no log\n"); return; }

            int tool = reading.ToolErrorCount;
            int all = reading.ErrorCount;
            Expect(tool == 0, "errors: " + tool + " in this tool -- " + reading.Describe(true, DiagnosticOrigin.RoadTool, 6),
                failures);
            Expect(all == tool, "errors: " + (all - tool) + " outside this tool -- " + reading.Describe(true, null, 6)
                + " (they still stop this tool's assemblies loading, so the compile is not green)", failures);
            builder.Append("RCMCOMPILE case 5 errors  tool ").Append(tool).Append("  total ").Append(all).Append('\n');
        }

        /// <summary> Case 6. No warning belonging to this tool. Only this tool's: R-5 forbids fixing a host warning here,
        /// so asserting on one would make the gate permanently red for a reason it is not allowed to address -- and a gate
        /// that cannot go green stops being read. The accepted-warning list is empty on purpose. </summary>
        private static void CaseNoToolWarnings(StringBuilder builder, List<string> failures, CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 6 warnings  not graded, no log\n"); return; }

            int unaccepted = 0;
            for (int i = 0; i < reading.diagnostics.Count; i++)
            {
                CompileDiagnostic d = reading.diagnostics[i];
                if (d.isError || d.origin != DiagnosticOrigin.RoadTool) { continue; }
                if (IsAccepted(d)) { continue; }
                unaccepted++;
            }
            Expect(unaccepted == 0, "warnings: " + unaccepted + " in this tool with no recorded reason -- "
                + reading.Describe(false, DiagnosticOrigin.RoadTool, 6), failures);
            builder.Append("RCMCOMPILE case 6 warnings  tool ").Append(reading.ToolWarningCount)
                   .Append("  accepted with a reason ").Append(AcceptedToolWarnings.Length)
                   .Append("  unaccounted ").Append(unaccepted)
                   .Append("  elsewhere ").Append(reading.WarningCount - reading.ToolWarningCount).Append('\n');
        }

        /// <summary> Case 7. The compile ran to completion. A domain reload is the proof, because it happens only after
        /// the assemblies built -- which also means an out-of-memory death AFTER the reload leaves the compile verdict
        /// intact, and is reported rather than treated as a compile failure. </summary>
        private static void CaseTheCompileActuallyCompleted(StringBuilder builder, List<string> failures,
            CompileLogReading reading)
        {
            if (!reading.exists) { builder.Append("RCMCOMPILE case 7 completed  not graded, no log\n"); return; }

            Expect(reading.sawDomainReloadBegin,
                "completed: the log has no assembly reload, so the compile never finished", failures);
            Expect(reading.sawDomainReloadFinish,
                "completed: the assembly reload began but never finished resetting the domain", failures);
            builder.Append("RCMCOMPILE case 7 completed  reload ").Append(reading.CompileCompleted)
                   .Append("  out of memory afterwards ").Append(reading.sawOutOfMemory)
                   .Append(reading.sawOutOfMemory ? "  (the compile verdict stands; the fault is memory, not code)" : "")
                   .Append('\n');
        }

        /// <summary> Case 8. The lines this reader dismisses are really the ones it means to dismiss, and it does not
        /// dismiss anything else. Both halves are needed: a table nobody checks drifts into excusing a real fault, and
        /// the four entries here exist because each one has already been mistaken for a failure in this project.
        /// </summary>
        private static void CaseBenignNoiseIsClassified(StringBuilder builder, List<string> failures)
        {
            IList<string[]> table = CompileLog.BenignNoiseTable();
            Expect(table.Count > 0, "noise: the dismissal table is empty", failures);
            for (int i = 0; i < table.Count; i++)
            {
                Expect(table[i][0].Length > 0 && table[i][1].Length > 0,
                    "noise: row " + i + " has an empty marker or an empty reason", failures);
                Expect(CompileLog.BenignReason("prefix " + table[i][0] + " suffix") != null,
                    "noise: the table lists " + table[i][0] + " but the reader does not recognise it", failures);
            }
            Expect(CompileLog.BenignReason(
                       "Assets\\RoadCityMaster\\Scripts\\X.cs(1,1): error CS0103: The name 'y' does not exist") == null,
                "noise: a real compile error was classified as benign", failures);
            builder.Append("RCMCOMPILE case 8 noise  dismissed with a reason ").Append(table.Count).Append('\n');
        }

        /// <summary> Case 9. The reader is shown a synthetic log with failures planted in it, and has to find exactly
        /// those. This is the only case that can distinguish "the compile is clean" from "the reader matches nothing" --
        /// and the second is the likelier of the two, because Unity writes its diagnostics with BACKSLASH separators
        /// while every path in this tool is written with forward slashes. </summary>
        private static void CaseTheReaderCatchesAPlantedFailure(StringBuilder builder, List<string> failures)
        {
            string root = CompileLog.ProjectRoot();
            string synthetic =
                  "COMMAND LINE ARGUMENTS:\n-batchmode\n-projectPath\n" + root + "\n"
                + "-logFile\n" + Path.Combine(CompileLog.LogFolder(), "negative_control.log") + "\n"
                + "-executeMethod\n" + CompileLog.SuiteEntryPoint + "\n"
                + "Successfully changed project path to: " + root + "\n"
                + "Assets\\RoadCityMaster\\Scripts\\Planted.cs(12,7): error CS0103: The name 'x' does not exist\n"
                + "Assets\\RoadCityMaster\\Scripts\\Planted.cs(12,7): error CS0103: The name 'x' does not exist\n"
                + "Assets\\RoadCityMaster\\City\\Runtime\\Planted.cs(3,1): warning CS0219: assigned but never used\n"
                + "Assets\\UniverseCreator\\Core\\Runtime\\Elsewhere.cs(9,2): warning CS0618: obsolete\n"
                + "Packages\\com.unity.render-pipelines.universal\\X.cs(4,4): warning CS0414: unused\n"
                + "this line says error CS but carries no line and column, so it must not parse\n"
                + "AssetImportWorkerClient::OnTransportError - code=2 error=End of file\n"
                + "Begin MonoManager ReloadAssembly\n"
                + "- Finished resetting the current domain, in  0.426 seconds\n";

            CompileLogReading probe = new CompileLogReading();
            CompileLog.Parse(synthetic, probe);

            Expect(probe.ErrorCount == 1,
                "control: the planted error appears twice, as a real log repeats it once per compilation pass, and must "
                + "count as one -- counted " + probe.ErrorCount, failures);
            Expect(probe.ToolErrorCount == 1, "control: the planted error was not attributed to this tool", failures);
            Expect(probe.diagnostics.Count == 4,
                "control: expected four distinct diagnostics, read " + probe.diagnostics.Count
                + " -- a fifth means the lookalike line parsed", failures);
            Expect(probe.ToolWarningCount == 1,
                "control: expected one tool warning, read " + probe.ToolWarningCount, failures);
            Expect(probe.WarningCount == 3,
                "control: expected three warnings across the trees, read " + probe.WarningCount, failures);
            Expect(probe.benignNoise == 1, "control: the shutdown handshake was not recognised as benign", failures);
            Expect(probe.sawBatchMode && probe.sawArgumentBlock,
                "control: the argument block was not read back out of the log", failures);
            Expect(SamePath(probe.projectPathAccepted, root),
                "control: the project path was not read back as " + root, failures);
            Expect(probe.sawDomainReloadBegin && probe.sawDomainReloadFinish,
                "control: the reload lines were not seen", failures);
            Expect(!probe.CompileCompleted,
                "control: a log carrying an error still read as a completed clean compile", failures);

            CompileLogReading noReload = new CompileLogReading();
            CompileLog.Parse("COMMAND LINE ARGUMENTS:\n-batchmode\nnothing else happened\n", noReload);
            Expect(!noReload.CompileCompleted,
                "control: a log with no errors and no reload read as a completed compile, which is the silent-zero "
                + "failure this gate exists to refuse", failures);

            builder.Append("RCMCOMPILE case 9 control  planted errors 1 found ").Append(probe.ErrorCount)
                   .Append("  distinct diagnostics ").Append(probe.diagnostics.Count)
                   .Append("  benign ").Append(probe.benignNoise).Append('\n');
        }

        /// <summary> Case 10. The launch itself is recorded as data and checked, because the two ways this command has
        /// failed here were both in its argument list: an unquoted project path containing a space, and a `-noUpm` that
        /// manufactured three package errors out of nothing. A recipe kept in someone's head is a recipe that regresses.
        /// </summary>
        private static void CaseTheLaunchRecipeIsIntact(StringBuilder builder, List<string> failures)
        {
            string line = CompileLog.LaunchArgumentLine("rcm_compile_checkpoint.log");
            string root = CompileLog.ProjectRoot();

            Expect(line.IndexOf("-batchmode", StringComparison.Ordinal) >= 0
                   && line.IndexOf("-quit", StringComparison.Ordinal) >= 0
                   && line.IndexOf("-nographics", StringComparison.Ordinal) >= 0,
                "launch: the argument line is missing one of -batchmode, -quit, -nographics", failures);
            Expect(line.IndexOf("\"" + root + "\"", StringComparison.Ordinal) >= 0,
                "launch: the project path is not quoted, so a path containing a space arrives split in half", failures);
            Expect(line.IndexOf("\"" + Path.Combine(CompileLog.LogFolder(), "rcm_compile_checkpoint.log") + "\"",
                       StringComparison.Ordinal) >= 0,
                "launch: the log path is not quoted, or does not point inside the project's " + CompileLog.LogFolderName,
                failures);
            Expect(line.IndexOf("-executeMethod " + CompileLog.SuiteEntryPoint, StringComparison.Ordinal) >= 0,
                "launch: the run does not call " + CompileLog.SuiteEntryPoint, failures);
            Expect(line.IndexOf("-noUpm", StringComparison.Ordinal) < 0,
                "launch: -noUpm is present; it invents package-manager errors and changes nothing else", failures);

            IList<string[]> preconditions = CompileLog.LaunchPreconditions();
            Expect(preconditions.Count >= 4,
                "launch: the pre-flight list has only " + preconditions.Count + " conditions", failures);
            for (int i = 0; i < preconditions.Count; i++)
            {
                Expect(preconditions[i][0].Length > 0 && preconditions[i][1].Length > 0,
                    "launch: pre-flight condition " + i + " has no text or no reason", failures);
            }
            builder.Append("RCMCOMPILE case 10 launch  pre-flight conditions ").Append(preconditions.Count)
                   .Append("  entry point ").Append(CompileLog.SuiteEntryPoint).Append('\n');
        }

        /// <summary> A warning this tool accepts, with its reason recorded. </summary>
        private static bool IsAccepted(CompileDiagnostic diagnostic)
        {
            string identity = diagnostic.Identity();
            for (int i = 0; i < AcceptedToolWarnings.Length; i++)
            {
                if (string.Equals(AcceptedToolWarnings[i][0], identity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary> Two paths naming the same place. Separators and a trailing separator differ freely between what a
        /// log prints and what the editor reports, and neither difference means anything. </summary>
        private static bool SamePath(string left, string right)
        {
            return string.Equals(Normalise(left), Normalise(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool UnderFolder(string path, string folder)
        {
            string normalisedFolder = Normalise(folder);
            if (normalisedFolder.Length == 0) { return false; }
            return Normalise(path).StartsWith(normalisedFolder + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalise(string path)
        {
            if (path == null) { return string.Empty; }
            return path.Replace('\\', '/').TrimEnd('/').Trim();
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }
    }
}
#endif

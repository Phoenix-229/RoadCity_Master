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
    /// <summary> RM-92: proves the port targets the URP that is actually installed.
    ///
    /// Why this exists at all. "Ported to URP" is not a checkable statement, because URP is not one thing. Between
    /// majors the `Core.hlsl` include paths move, the lens-flare asset type changes, and the shadow-caster pass has to
    /// be declared differently -- so a shader written against one major and run against another does not warn, it
    /// draws magenta. RM-67 / RM-68 / RM-91 were all written against 17.5, and until now that was recorded as prose
    /// in <see cref="PipelineAudit"/>'s fact table: a sentence claiming `Packages/manifest.json:3` says 17.5.0. This
    /// gate reads that file and turns the claim into a fact, which is the whole difference between "it looked fine on
    /// my machine" and something a build can fail on.
    ///
    /// Major, not exact. A mismatch FAILS on the major only. 17.5 to 17.6 is a patch this tool survives; 17 to 18 is
    /// a port that has to be redone. Reporting the full installed string either way means an author sees the drift
    /// even when it is legal.
    ///
    /// No network, and provably so. The version is read from files the project already carries, never from the Package
    /// Manager -- a gate that reached the network would fail in a batchmode build on a machine with no route out. The
    /// parse is a pure string function, so case 1 establishes that the reader is RIGHT against synthetic text with no
    /// project, no packages and no route out at all; the later cases then apply that proven reader to the real files.
    /// Agreeing with itself would have proved nothing.
    ///
    /// Report lines are prefixed RCMURP. </summary>
    public static class PipelineVersionGate
    {
        private const string Prefix = "RCMURP";

        [MenuItem("Window/Road City Master/Testing/Render pipeline version", false, 1092)]
        private static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder builder = new StringBuilder();
            builder.Append(Prefix).Append(" BEGIN\n");

            CaseParserReadsBothPackageFileShapes(builder, failures);
            CaseMajorIsVersionShaped(builder, failures);
            CaseInstalledVersionIsReadFromTheProject(builder, failures);
            CaseMajorMatchesTheRecordedTarget(builder, failures);
            CaseEngineIsAtOrAboveTheSupportedFloor(builder, failures);
            CaseTheCitationPointsAtTheRightLine(builder, failures);
            CaseDeterministic(builder, failures);
            CaseNoAbsolutePathIsTyped(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append(Prefix).Append(" FAIL ").Append(failures[i]).Append('\n');
            }
            builder.Append(Prefix).Append(" result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(Prefix).Append(" END\n");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The reader, proved against text rather than against the project. Both package files nest the
        /// version differently -- `manifest.json` puts it straight after the package key, `packages-lock.json` opens
        /// an object whose `version` member holds it -- and one reader has to handle both.
        ///
        /// The decoy matters more than the happy path. `com.unity.render-pipelines.universal-config` is a DIFFERENT
        /// package that shares URP's id as a prefix, sits next to it in the lock file, and even appears INSIDE URP's
        /// own dependency block. A prefix match would read the config package's version and report it as URP's, and
        /// on the real files today it would report the right number for the wrong reason -- both are 17.5.0. So the
        /// decoy is given a version that differs, which is the only way the mistake becomes visible. </summary>
        private static void CaseParserReadsBothPackageFileShapes(StringBuilder builder, List<string> failures)
        {
            string manifest = "{\n  \"dependencies\": {\n"
                + "    \"" + PipelineAudit.UrpPackageId + "\": \"17.5.0\",\n"
                + "    \"com.unity.modules.terrain\": \"1.0.0\"\n  }\n}\n";
            Expect(PipelineAudit.ParseUrpVersion(manifest) == "17.5.0",
                "parse: the manifest shape (version straight after the key) read as '"
                + PipelineAudit.ParseUrpVersion(manifest) + "' instead of 17.5.0.", failures);

            string locked = "{\n  \"dependencies\": {\n"
                + "    \"" + PipelineAudit.UrpPackageId + "\": {\n"
                + "      \"version\": \"17.5.0\",\n      \"depth\": 0,\n"
                + "      \"dependencies\": {\n"
                + "        \"" + PipelineAudit.UrpPackageId + "-config\": \"99.9.9\"\n      }\n    },\n"
                + "    \"" + PipelineAudit.UrpPackageId + "-config\": {\n"
                + "      \"version\": \"99.9.9\"\n    }\n  }\n}\n";
            string fromLock = PipelineAudit.ParseUrpVersion(locked);
            Expect(fromLock == "17.5.0",
                "parse: the lock-file shape read as '" + fromLock + "' instead of 17.5.0.", failures);
            Expect(fromLock != "99.9.9",
                "parse: the reader returned the -config package's version. That package shares URP's id as a prefix, "
                + "so the key must be matched WITH its closing quote; on the real files both happen to be 17.5.0 "
                + "today, which would have hidden this.", failures);

            string decoyFirst = "{\n  \"dependencies\": {\n"
                + "    \"" + PipelineAudit.UrpPackageId + "-config\": \"99.9.9\",\n"
                + "    \"" + PipelineAudit.UrpPackageId + "\": \"17.5.0\"\n  }\n}\n";
            Expect(PipelineAudit.ParseUrpVersion(decoyFirst) == "17.5.0",
                "parse: with the -config package listed FIRST the reader returned '"
                + PipelineAudit.ParseUrpVersion(decoyFirst) + "'; key order must not matter.", failures);

            Expect(PipelineAudit.ParseUrpVersion("{}") == "",
                "parse: a file naming no URP package must read as empty, which is a real answer (the next file is "
                + "then consulted) and not an error.", failures);
            Expect(PipelineAudit.ParseUrpVersion(null) == "",
                "parse: null text must read as empty rather than throwing.", failures);

            builder.Append(Prefix).Append(" trace  parse  manifest shape, lock shape, -config decoy both orders\n");
        }

        /// <summary> The major is the number the verdict turns on, so what counts as version-shaped is stated here.
        /// A non-numeric or signed head is -1 rather than 0: 0 is a legal major, and returning it for garbage would
        /// make an unreadable version look like a mismatch against a real one. </summary>
        private static void CaseMajorIsVersionShaped(StringBuilder builder, List<string> failures)
        {
            Expect(PipelineAudit.MajorOf("17.5.0") == 17,
                "major: 17.5.0 gave " + PipelineAudit.MajorOf("17.5.0") + ".", failures);
            Expect(PipelineAudit.MajorOf("18.0.0") == 18,
                "major: 18.0.0 gave " + PipelineAudit.MajorOf("18.0.0") + ".", failures);
            Expect(PipelineAudit.MajorOf("17") == 17,
                "major: a bare '17' with no dot is still major 17; got " + PipelineAudit.MajorOf("17") + ".",
                failures);
            Expect(PipelineAudit.MajorOf("17.5.0-pre.3") == 17,
                "major: a pre-release suffix does not change the major; got "
                + PipelineAudit.MajorOf("17.5.0-pre.3") + ".", failures);
            Expect(PipelineAudit.MajorOf("") == -1, "major: an empty string must be -1, not 0.", failures);
            Expect(PipelineAudit.MajorOf(null) == -1, "major: null must be -1, not 0.", failures);
            Expect(PipelineAudit.MajorOf("universal") == -1,
                "major: a non-numeric head must be -1, not 0 -- 0 is a legal major and would read as a real "
                + "mismatch instead of as unreadable.", failures);
            Expect(PipelineAudit.MajorOf(".5.0") == -1,
                "major: a leading dot leaves no head to parse; got " + PipelineAudit.MajorOf(".5.0") + ".",
                failures);

            builder.Append(Prefix).Append(" trace  major   target ").Append(PipelineAudit.TargetUrpVersion)
                   .Append(" -> ").Append(PipelineAudit.MajorOf(PipelineAudit.TargetUrpVersion)
                       .ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        /// <summary> The read against the real project, and the report line an author actually reads. The file is
        /// named project-relative, because naming it is a claim that has to stay true on another machine. </summary>
        private static void CaseInstalledVersionIsReadFromTheProject(StringBuilder builder, List<string> failures)
        {
            InstalledUrp installed = PipelineAudit.ReadInstalledUrp(CompileLog.ProjectRoot());

            Expect(installed.found,
                "installed: " + (installed.problem.Length > 0 ? installed.problem : "the version was not found."),
                failures);

            if (installed.found)
            {
                Expect(installed.sourceFile == PipelineAudit.ManifestRelativePath
                    || installed.sourceFile == PipelineAudit.LockRelativePath,
                    "installed: the version was reported as coming from '" + installed.sourceFile
                    + "', which is neither of the two files this gate consults.", failures);
                Expect(installed.sourceFile.IndexOf(':') < 0,
                    "installed: the named source file '" + installed.sourceFile + "' is not project-relative; a "
                    + "path carrying a drive letter is meaningless on another machine (C-04).", failures);
                Expect(PipelineAudit.MajorOf(installed.version) >= 0,
                    "installed: '" + installed.version + "' is not version-shaped.", failures);
            }

            builder.Append(Prefix).Append(" trace  read    ")
                   .Append(installed.found ? installed.version : "(unreadable)")
                   .Append("  from ").Append(installed.found ? installed.sourceFile : "-")
                   .Append("  (file read, no network)\n");
        }

        /// <summary> The verdict. Stated as its own case so the report says plainly which of the two possible
        /// failures happened: the version could not be read, or it was read and disagrees. </summary>
        private static void CaseMajorMatchesTheRecordedTarget(StringBuilder builder, List<string> failures)
        {
            InstalledUrp installed = PipelineAudit.ReadInstalledUrp(CompileLog.ProjectRoot());
            if (!installed.found)
            {
                builder.Append(Prefix).Append(" trace  verdict SKIPPED -- nothing was read to compare\n");
                return;   // already failed above; a second sentence about it would not add anything
            }

            int target = PipelineAudit.MajorOf(PipelineAudit.TargetUrpVersion);
            int actual = PipelineAudit.MajorOf(installed.version);

            Expect(target >= 0,
                "verdict: the recorded target '" + PipelineAudit.TargetUrpVersion + "' is not version-shaped, so "
                + "there is nothing to compare against.", failures);
            Expect(target == actual,
                "verdict: this tool's shaders and its lens flare were written against URP "
                + PipelineAudit.TargetUrpVersion + " (major " + target.ToString(CultureInfo.InvariantCulture)
                + "), but " + installed.sourceFile + " says URP " + installed.version + " (major "
                + actual.ToString(CultureInfo.InvariantCulture) + ") is installed. Include paths, the lens-flare "
                + "asset type and the shadow-caster declaration all move between URP majors, and none of those "
                + "produce a warning -- they produce magenta. Re-port against the installed major, or install the "
                + "recorded one; do not ship across a major gap.", failures);

            bool exact = installed.version == PipelineAudit.TargetUrpVersion;
            builder.Append(Prefix).Append(" trace  verdict target ").Append(PipelineAudit.TargetUrpVersion)
                   .Append("  installed ").Append(installed.version).Append("  major ")
                   .Append(target == actual ? "matches" : "DIFFERS")
                   .Append(exact ? "  (exact)" : "  (minor/patch drift, legal)").Append('\n');
        }

        /// <summary> <see cref="PipelineAudit"/>'s fact table cites `Packages/manifest.json:3` by line number, and a
        /// cited line number is the kind of claim that rots silently the first time someone adds a dependency above
        /// it. Checking it costs one file read and keeps the document honest. </summary>
        private static void CaseTheCitationPointsAtTheRightLine(StringBuilder builder, List<string> failures)
        {
            string full = Path.Combine(CompileLog.ProjectRoot(),
                PipelineAudit.ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                builder.Append(Prefix).Append(" trace  cite    SKIPPED -- ")
                       .Append(PipelineAudit.ManifestRelativePath).Append(" is not present\n");
                return;
            }

            string[] lines = File.ReadAllLines(full);
            int cited = PipelineAudit.ManifestUrpLine;
            bool inRange = cited >= 1 && cited <= lines.Length;
            Expect(inRange,
                "cite: the fact table cites " + PipelineAudit.ManifestRelativePath + ":"
                + cited.ToString(CultureInfo.InvariantCulture) + ", but the file has only "
                + lines.Length.ToString(CultureInfo.InvariantCulture) + " lines.", failures);

            if (inRange)
            {
                Expect(lines[cited - 1].IndexOf("\"" + PipelineAudit.UrpPackageId + "\"",
                        System.StringComparison.Ordinal) >= 0,
                    "cite: " + PipelineAudit.ManifestRelativePath + ":"
                    + cited.ToString(CultureInfo.InvariantCulture) + " no longer carries the URP dependency -- it "
                    + "reads '" + lines[cited - 1].Trim() + "'. Update PipelineAudit.ManifestUrpLine and the fact "
                    + "table's citation together.", failures);
            }

            builder.Append(Prefix).Append(" trace  cite    ").Append(PipelineAudit.ManifestRelativePath)
                   .Append(':').Append(cited.ToString(CultureInfo.InvariantCulture)).Append(" carries the URP key\n");
        }

        /// <summary> The ENGINE floor, which is a different pin from the URP one above and fails for a different
        /// reason. RM-104 removed the last pre-Unity-6 branches from the vendored scripts, so the tool no longer has an
        /// older spelling to fall back on: below 6000.4 it does not warn, it fails to compile on
        /// <c>PhysicsMaterial</c>, <c>FindObjectsByType</c>, <c>ContributeGI</c> and <c>detailResolution</c>. Saying so
        /// here turns "requires Unity 6" from a line in the README into something the suite checks. </summary>
        private static void CaseEngineIsAtOrAboveTheSupportedFloor(StringBuilder builder, List<string> failures)
        {
            // The comparison is proved on text first. An editor can only report its own version, so the one case that
            // matters -- an engine below the floor -- is the one this machine can never produce.
            int major, minor;
            Expect(!PipelineAudit.EngineMeetsFloor("2018.1.0f2", out major, out minor),
                "floor: 2018.1.0f2 was accepted as meeting the " + PipelineAudit.MinimumEngineVersion + " floor.",
                failures);
            Expect(!PipelineAudit.EngineMeetsFloor("2022.3.20f1", out major, out minor),
                "floor: 2022.3.20f1 was accepted -- a 2022 major is below 6000 however new its minor looks.",
                failures);
            Expect(!PipelineAudit.EngineMeetsFloor("6000.3.9f1", out major, out minor),
                "floor: 6000.3.9f1 is one minor below the floor and was accepted.", failures);
            Expect(PipelineAudit.EngineMeetsFloor("6000.4.0f1", out major, out minor),
                "floor: 6000.4.0f1 is the floor itself and was rejected.", failures);
            Expect(PipelineAudit.EngineMeetsFloor("6000.5.2f1", out major, out minor),
                "floor: 6000.5.2f1 is above the floor and was rejected.", failures);
            Expect(PipelineAudit.EngineMeetsFloor("6001.0.0f1", out major, out minor),
                "floor: a newer major (6001.0.0f1) was rejected -- the floor is a minimum, not a pin.", failures);
            Expect(!PipelineAudit.EngineMeetsFloor("not-a-version", out major, out minor),
                "floor: unreadable text was treated as a supported version.", failures);
            Expect(!PipelineAudit.EngineMeetsFloor("6000", out major, out minor),
                "floor: '6000' has no minor to compare and was accepted anyway.", failures);

            // Then the editor this is actually running in.
            string running = Application.unityVersion;
            bool isSupported = PipelineAudit.EngineMeetsFloor(running, out major, out minor);
            Expect(isSupported, "floor: this editor reports '" + running + "', below the supported floor "
                + PipelineAudit.MinimumEngineVersion + " -- there is no pre-Unity-6 branch left to fall back on, so "
                + "the tool cannot compile here at all.", failures);

            builder.Append(Prefix).Append(" trace  engine  running ").Append(running)
                   .Append("  floor ").Append(PipelineAudit.MinimumEngineVersion)
                   .Append(isSupported ? "  at or above\n" : "  BELOW\n");
        }


        /// <summary> Two reads of the same unchanged files must give the same answer. A reader that walked a
        /// dictionary or depended on enumeration order would pass once and fail on a colleague's machine. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            string root = CompileLog.ProjectRoot();
            InstalledUrp a = PipelineAudit.ReadInstalledUrp(root);
            InstalledUrp b = PipelineAudit.ReadInstalledUrp(root);

            Expect(a.found == b.found && a.version == b.version && a.sourceFile == b.sourceFile
                    && a.problem == b.problem,
                "determinism: two reads of the same files disagreed -- '" + a.version + "' from '" + a.sourceFile
                + "' then '" + b.version + "' from '" + b.sourceFile + "'.", failures);

            string first, second;
            bool ok1 = Run2(out first);
            bool ok2 = Run2(out second);
            Expect(ok1 == ok2 && first == second,
                "determinism: the two inner reports differ, so this gate's own output is not reproducible.",
                failures);

            builder.Append(Prefix).Append(" trace  determ  two reads identical\n");
        }

        /// <summary> The read-only half of <see cref="Run"/>, used by the determinism case so it can compare two
        /// reports without recursing into itself. </summary>
        private static bool Run2(out string report)
        {
            InstalledUrp installed = PipelineAudit.ReadInstalledUrp(CompileLog.ProjectRoot());
            report = installed.found
                ? installed.version + " from " + installed.sourceFile
                : "unreadable: " + installed.problem;
            return installed.found;
        }

        /// <summary> C-04 at the two files this task touches: no absolute path and no drive letter typed into either,
        /// so the gate works wherever the folder is installed. The needle is BUILT from a character code rather than
        /// written as a literal -- a scan for a drive-letter pattern that spelled the pattern out would find itself in
        /// its own source and fail every time. </summary>
        private static void CaseNoAbsolutePathIsTyped(StringBuilder builder, List<string> failures)
        {
            string root = RoadEditorUtility.GetBasePath();
            string[] files =
            {
                Path.Combine(root, Path.Combine("Scripts", Path.Combine("Editor", "PipelineAudit.cs"))),
                Path.Combine(root, Path.Combine("Scripts", Path.Combine("Editor", "PipelineVersionGate.cs"))),
            };

            string colon = ((char)58).ToString();
            string[] needles = { colon + ((char)92).ToString(), colon + "/" };
            int scanned = 0;

            for (int i = 0; i < files.Length; i++)
            {
                if (!File.Exists(files[i]))
                {
                    failures.Add("paths: expected to scan '" + Path.GetFileName(files[i])
                        + "' for typed absolute paths, but it is not where this gate looked.");
                    continue;
                }

                scanned++;
                string text = File.ReadAllText(files[i]);
                for (int n = 0; n < needles.Length; n++)
                {
                    // "http://" and "https://" carry the same pair and are prose, not paths, so they are excluded
                    // by checking what precedes the colon rather than by widening the needle.
                    int at = text.IndexOf(needles[n], System.StringComparison.Ordinal);
                    while (at > 0)
                    {
                        char before = text[at - 1];
                        bool isScheme = at >= 4 && (text.Substring(at - 4, 4) == "http"
                            || (at >= 5 && text.Substring(at - 5, 5) == "https"));
                        if (!isScheme && ((before >= 'A' && before <= 'Z') || (before >= 'a' && before <= 'z')))
                        {
                            failures.Add("paths: " + Path.GetFileName(files[i]) + " appears to type an absolute "
                                + "path near offset " + at.ToString(CultureInfo.InvariantCulture)
                                + "; C-04 forbids a drive letter in a script, and a typed one would be wrong on "
                                + "every machine but this one.");
                            break;
                        }
                        at = text.IndexOf(needles[n], at + 1, System.StringComparison.Ordinal);
                    }
                }
            }

            Expect(scanned == files.Length,
                "paths: " + scanned.ToString(CultureInfo.InvariantCulture) + " of "
                + files.Length.ToString(CultureInfo.InvariantCulture) + " files were scanned, so this case did not "
                + "prove what it claims.", failures);

            builder.Append(Prefix).Append(" trace  paths   ")
                   .Append(scanned.ToString(CultureInfo.InvariantCulture))
                   .Append(" file(s) scanned, no typed drive letter\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

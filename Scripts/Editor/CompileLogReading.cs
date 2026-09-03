#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Which tree a compiler diagnostic came from. The distinction is the whole point of the gate: a warning
    /// in the host package or in a Unity package is not this tool's to fix and R-5 forbids fixing it here, while a
    /// warning under `Assets/RoadCityMaster/` is. </summary>
    public enum DiagnosticOrigin
    {
        RoadTool,
        HostPackage,
        Elsewhere,
    }


    /// <summary> One `error CS` or `warning CS` line, taken apart. Unity writes these with backslash separators and a
    /// `(line,column)` suffix -- `Assets\RoadCityMaster\Scripts\EngineIntegration.cs(167,52): warning CS0618: ...` --
    /// and writes them once per compilation pass, so the same diagnostic appears several times in one log and has to be
    /// de-duplicated by identity rather than counted by line. </summary>
    public struct CompileDiagnostic
    {
        public readonly bool isError;
        public readonly string code;
        public readonly string file;
        public readonly int line;
        public readonly int column;
        public readonly string text;
        public readonly DiagnosticOrigin origin;

        public CompileDiagnostic(bool isError, string code, string file, int line, int column, string text,
            DiagnosticOrigin origin)
        {
            this.isError = isError;
            this.code = code;
            this.file = file;
            this.line = line;
            this.column = column;
            this.text = text;
            this.origin = origin;
        }

        /// <summary> The identity two copies of the same diagnostic share. </summary>
        public string Identity()
        {
            return file + "(" + line.ToString(CultureInfo.InvariantCulture) + ","
                   + column.ToString(CultureInfo.InvariantCulture) + ") " + code;
        }

        public override string ToString()
        {
            return Identity() + "  " + (isError ? "error" : "warning") + "  " + text;
        }
    }


    /// <summary> Everything a batch-mode log can be asked about, read once. RM-71's gate exists because an exit code is
    /// not evidence: a launch whose project path was split at a space exits in under three seconds with no log at all,
    /// and a stub log of a kilobyte reads as success to anything that only checks whether a file appeared. So the
    /// reading records not just the diagnostics but whether the run got far enough to have compiled anything. </summary>
    public sealed class CompileLogReading
    {
        public string logPath = string.Empty;
        public bool exists;
        public long bytes;
        public int lines;

        /// <summary> Distinct diagnostics, de-duplicated across compilation passes, in first-seen order. </summary>
        public readonly List<CompileDiagnostic> diagnostics = new List<CompileDiagnostic>();

        /// <summary> What the launch actually asked for, read back out of the log's own argument block. </summary>
        public string projectPathArgument = string.Empty;
        public string logFileArgument = string.Empty;
        public string executeMethodArgument = string.Empty;
        public string projectPathAccepted = string.Empty;

        public bool sawArgumentBlock;
        public bool sawBatchMode;
        public bool sawDomainReloadBegin;
        public bool sawDomainReloadFinish;
        public bool sawAbort;
        public bool sawOutOfMemory;
        public string abortLine = string.Empty;

        /// <summary> Lines that read like faults and are not, counted rather than reported, so a reader who greps the
        /// log for "error" and finds them knows they were seen and dismissed on purpose. </summary>
        public int benignNoise;

        public int ErrorCount { get { return CountWhere(true, null); } }
        public int WarningCount { get { return CountWhere(false, null); } }
        public int ToolWarningCount { get { return CountWhere(false, DiagnosticOrigin.RoadTool); } }
        public int ToolErrorCount { get { return CountWhere(true, DiagnosticOrigin.RoadTool); } }

        private int CountWhere(bool isError, DiagnosticOrigin? origin)
        {
            int n = 0;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].isError != isError) { continue; }
                if (origin.HasValue && diagnostics[i].origin != origin.Value) { continue; }
                n++;
            }
            return n;
        }

        /// <summary> True when the log shows the compile ran to completion. A domain reload only happens after the
        /// assemblies compiled, which is why its presence is the honest proof and the exit code is not: a run that
        /// later died of an out-of-memory fault still compiled cleanly, and a run that never reached a reload did not
        /// compile at all no matter what it returned. </summary>
        public bool CompileCompleted
        {
            get { return sawDomainReloadBegin && sawDomainReloadFinish && ErrorCount == 0; }
        }

        /// <summary> The diagnostics of one kind, formatted, capped so a broken build does not print a novel. </summary>
        public string Describe(bool isError, DiagnosticOrigin? origin, int cap)
        {
            StringBuilder builder = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < diagnostics.Count && shown < cap; i++)
            {
                if (diagnostics[i].isError != isError) { continue; }
                if (origin.HasValue && diagnostics[i].origin != origin.Value) { continue; }
                if (shown > 0) { builder.Append("; "); }
                builder.Append(diagnostics[i].ToString());
                shown++;
            }
            if (shown == 0) { builder.Append("none"); }
            return builder.ToString();
        }
    }


    /// <summary> RM-71's reader: it turns a retained batch-mode log into an answer. Nothing here launches Unity -- the
    /// launch is a command a person runs, and the point of this class is that the command's *evidence* is checked by
    /// code rather than by eye, because "an empty exit code with no log is not a pass" is a rule that only holds if
    /// something enforces it. </summary>
    public static class CompileLog
    {
        /// <summary> Where the log must live. Inside the project, never a temp folder: a log a reader cannot find while
        /// looking at the project is a log that does not exist for the purpose it was kept for. </summary>
        public const string LogFolderName = "Logs";

        /// <summary> The entry point the run must call. Not the host's self test -- this tool's own suite. </summary>
        public const string SuiteEntryPoint = "RoadCityMaster.RcmGateSuite.RunBatch";

        /// <summary> A log smaller than this never got past starting up. The stub written when a launch splits its
        /// project path at a space is about a kilobyte, and a real run's log is tens of kilobytes before it has done
        /// anything, so the floor separates the two without needing to know how long the run was. </summary>
        public const long MinimumCredibleBytes = 8 * 1024;

        private const string ToolFolderToken = "Assets/RoadCityMaster/";
        private const string HostFolderToken = "Assets/UniverseCreator/";

        /// <summary> Log lines that read like faults and are not, each with the reason it is dismissed. Kept as a table
        /// rather than as a habit, because every one of these has already cost this project time once. </summary>
        private static readonly string[][] BenignNoise = {
            new[] { "OnTransportError - code=2",
                "the import worker's normal shutdown handshake; it follows \"Editor is closing down\" and returns 0" },
            new[] { "Editor requested this worker to shutdown",
                "the editor asking its own import worker to stop at the end of the run" },
            new[] { "AcceleratorClientConnectionCallback - disconnected",
                "the cache server was never configured, so there is nothing to stay connected to" },
            new[] { "Curl error 3: URL rejected",
                "`m_CacheServerMode: 0` means \"use preferences\", and the empty preference dials `:0`" },
        };

        /// <summary> The project folder: the parent of `Assets`. Derived, never typed -- C-04 forbids an absolute path
        /// in a script, and a typed one would also be wrong on any other machine. </summary>
        public static string ProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent == null ? Application.dataPath : parent.FullName;
        }

        public static string LogFolder()
        {
            return Path.Combine(ProjectRoot(), LogFolderName);
        }

        /// <summary> Which tree a diagnostic's file belongs to. </summary>
        public static DiagnosticOrigin OriginOf(string file)
        {
            string normalised = file.Replace('\\', '/');
            if (normalised.IndexOf(ToolFolderToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DiagnosticOrigin.RoadTool;
            }
            if (normalised.IndexOf(HostFolderToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DiagnosticOrigin.HostPackage;
            }
            return DiagnosticOrigin.Elsewhere;
        }

        /// <summary> A log line that reads like a fault but is not, with the reason, or null. </summary>
        public static string BenignReason(string line)
        {
            for (int i = 0; i < BenignNoise.Length; i++)
            {
                if (line.IndexOf(BenignNoise[i][0], StringComparison.Ordinal) >= 0) { return BenignNoise[i][1]; }
            }
            return null;
        }

        /// <summary> The dismissal table, for the gate to check itself against. </summary>
        public static IList<string[]> BenignNoiseTable()
        {
            string[][] copy = new string[BenignNoise.Length][];
            for (int i = 0; i < BenignNoise.Length; i++)
            {
                copy[i] = new[] { BenignNoise[i][0], BenignNoise[i][1] };
            }
            return copy;
        }

        /// <summary> Take one line apart into a diagnostic, or refuse it. Written against the shape Unity actually
        /// emits, which is `path(line,column): warning CS0618: text` with BACKSLASH separators -- a parser written
        /// against the forward-slash form used everywhere else in this tool would silently match nothing, which is the
        /// exact failure RM-70 made a gate out of. </summary>
        public static bool TryDiagnostic(string line, out CompileDiagnostic diagnostic)
        {
            diagnostic = new CompileDiagnostic(false, string.Empty, string.Empty, 0, 0, string.Empty,
                DiagnosticOrigin.Elsewhere);

            bool isError = true;
            int at = line.IndexOf("): error CS", StringComparison.Ordinal);
            if (at < 0)
            {
                isError = false;
                at = line.IndexOf("): warning CS", StringComparison.Ordinal);
            }
            if (at < 0) { return false; }

            string head = line.Substring(0, at + 1);
            int open = head.LastIndexOf('(');
            if (open <= 0) { return false; }
            string file = head.Substring(0, open).Trim();
            string span = head.Substring(open + 1, head.Length - open - 2);
            int comma = span.IndexOf(',');
            if (comma <= 0) { return false; }

            int lineNumber;
            int column;
            if (!int.TryParse(span.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out lineNumber)) { return false; }
            if (!int.TryParse(span.Substring(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out column)) { return false; }

            string rest = line.Substring(at + 3);
            int colon = rest.IndexOf(": ", StringComparison.Ordinal);
            if (colon <= 0) { return false; }
            string codeWords = rest.Substring(0, colon);
            int space = codeWords.LastIndexOf(' ');
            string code = space >= 0 ? codeWords.Substring(space + 1) : codeWords;
            string text = rest.Substring(colon + 2);

            diagnostic = new CompileDiagnostic(isError, code, file, lineNumber, column, text, OriginOf(file));
            return true;
        }

        /// <summary> Read a log's text into an answer. Separate from <see cref="Read"/> so the gate can feed it a
        /// synthetic log and prove the reader catches a planted failure -- a checker nobody has ever seen fail is a
        /// checker nobody has evidence about. </summary>
        public static void Parse(string text, CompileLogReading reading)
        {
            string[] lines = ShipAuditSweeps.SplitLines(text);
            reading.lines = lines.Length;
            HashSet<string> seen = new HashSet<string>();
            bool inArguments = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed == "COMMAND LINE ARGUMENTS:") { reading.sawArgumentBlock = true; inArguments = true; }
                else if (inArguments)
                {
                    if (trimmed == "-batchmode") { reading.sawBatchMode = true; }
                    else if (trimmed == "-projectPath" && i + 1 < lines.Length)
                    { reading.projectPathArgument = lines[i + 1].Trim(); }
                    else if (trimmed == "-logFile" && i + 1 < lines.Length)
                    { reading.logFileArgument = lines[i + 1].Trim(); }
                    else if (trimmed == "-executeMethod" && i + 1 < lines.Length)
                    { reading.executeMethodArgument = lines[i + 1].Trim(); }
                }

                if (trimmed.StartsWith("Successfully changed project path to:", StringComparison.Ordinal))
                {
                    inArguments = false;
                    reading.projectPathAccepted = trimmed.Substring("Successfully changed project path to:".Length)
                                                         .Trim();
                }
                if (trimmed.IndexOf("Aborting batchmode due to failure", StringComparison.Ordinal) >= 0)
                { reading.sawAbort = true; reading.abortLine = trimmed; }
                if (trimmed.IndexOf("Begin MonoManager ReloadAssembly", StringComparison.Ordinal) >= 0)
                { reading.sawDomainReloadBegin = true; }
                if (trimmed.IndexOf("Finished resetting the current domain", StringComparison.Ordinal) >= 0)
                { reading.sawDomainReloadFinish = true; }
                if (trimmed.IndexOf("Could not allocate memory", StringComparison.Ordinal) >= 0)
                { reading.sawOutOfMemory = true; }
                if (BenignReason(trimmed) != null) { reading.benignNoise++; }

                CompileDiagnostic diagnostic;
                if (!TryDiagnostic(line, out diagnostic)) { continue; }
                if (!seen.Add(diagnostic.Identity())) { continue; }
                reading.diagnostics.Add(diagnostic);
            }
        }

        /// <summary> Read one log file. A missing file is an answer, not an exception: "no log" is the failure mode this
        /// gate was written for. </summary>
        public static CompileLogReading Read(string path)
        {
            CompileLogReading reading = new CompileLogReading();
            reading.logPath = path == null ? string.Empty : path;
            if (reading.logPath.Length == 0 || !File.Exists(reading.logPath)) { return reading; }

            reading.exists = true;
            try
            {
                FileInfo info = new FileInfo(reading.logPath);
                reading.bytes = info.Length;

                // `FileShare.ReadWrite`, because the log being graded is usually the one THIS run is still writing:
                // Unity holds it open for append, and the ordinary read helpers ask for exclusive-enough sharing to be
                // refused. Reading a file whose writer is still attached is the whole point here.
                string text;
                using (FileStream stream = new FileStream(reading.logPath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
                Parse(text, reading);
            }
            catch (IOException error)
            {
                reading.abortLine = "the log could not be read: " + error.Message;
            }
            return reading;
        }

        /// <summary> The log THIS process is writing, taken from its own command line. In batch mode the compile is
        /// finished long before `-executeMethod` runs, so by the time a gate reads this the compile section is already
        /// on disk -- the run can grade its own compile without a second launch. </summary>
        public static string CurrentRunLogPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-logFile", StringComparison.OrdinalIgnoreCase)) { return args[i + 1]; }
            }
            return string.Empty;
        }

        /// <summary> The newest retained log worth grading, when this run has none of its own -- which is the case in
        /// the interactive editor, where the menu item is used. Unity's own worker and package logs are skipped: they
        /// are not compile runs and picking one would grade the wrong thing. </summary>
        public static string NewestRetainedLog()
        {
            string folder = LogFolder();
            if (!Directory.Exists(folder)) { return string.Empty; }

            string[] candidates = Directory.GetFiles(folder, "*.log", SearchOption.TopDirectoryOnly);
            string best = string.Empty;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = Path.GetFileName(candidates[i]);
                if (name.StartsWith("AssetImportWorker", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (name.StartsWith("shadercompiler", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.Equals(name, "upm.log", StringComparison.OrdinalIgnoreCase)) { continue; }

                FileInfo info = new FileInfo(candidates[i]);
                if (info.Length < MinimumCredibleBytes) { continue; }
                if (info.LastWriteTimeUtc <= bestTime) { continue; }
                bestTime = info.LastWriteTimeUtc;
                best = candidates[i];
            }
            return best;
        }

        /// <summary> The log this gate will grade: this run's own if it has one, otherwise the newest retained. </summary>
        public static string ResolveLogPath()
        {
            string mine = CurrentRunLogPath();
            if (mine.Length > 0 && File.Exists(mine)) { return mine; }
            return NewestRetainedLog();
        }

        /// <summary> The launch, written down as one pre-quoted argument string. The quoting is the substance, not
        /// formatting: this project's shell joins an argument ARRAY with spaces and does not quote the elements, so a
        /// project path containing a space arrives split in half, and the run dies in under three seconds having written
        /// no log at all -- which reads exactly like "nothing to report" unless something checks for the log. </summary>
        public static string LaunchArgumentLine(string logFileName)
        {
            string root = ProjectRoot();
            string log = Path.Combine(LogFolder(), logFileName);
            return "-batchmode -quit -nographics"
                 + " -projectPath \"" + root + "\""
                 + " -logFile \"" + log + "\""
                 + " -executeMethod " + SuiteEntryPoint;
        }

        /// <summary> Conditions that must hold before the launch, each because it has already gone wrong here once.
        /// Kept next to the argument line so the two cannot drift apart. </summary>
        public static IList<string[]> LaunchPreconditions()
        {
            return new[] {
                new[] { "free physical memory at least about 4.5 GB",
                    "a run that starts short of memory dies mid-test with \"Could not allocate memory\", and the "
                    + "process that dies is whichever asks next -- not necessarily this one" },
                new[] { "no other editor open on another project",
                    "two editors plus import workers do not fit in this machine's memory, and a batch launch has "
                    + "already killed a session someone was working in" },
                new[] { "the log name is fresh rather than deleted first",
                    "Unity overwrites its own `-logFile` target, and deleting it first is what trips the shell's "
                    + "path guard on the editor's own install folder" },
                new[] { "no `-noUpm`",
                    "it manufactures three package-manager errors in the log and changes nothing else" },
            };
        }
    }
}
#endif

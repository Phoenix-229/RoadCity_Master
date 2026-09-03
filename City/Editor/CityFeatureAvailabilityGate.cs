#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-58's gate: the standalone path keeps working, and a feature that needs a provider says so.
    ///
    /// § 10.24's rule has a shape a machine can check. Every feature the tool offers is either usable with no provider
    /// or it names what to connect -- there is no third case where a control exists, does nothing, and explains
    /// nothing. That is what the cases below measure, against the one table all three consumers read, so the
    /// inspector, the road tool's manual and Universe Creator's documentation cannot drift into disagreeing about
    /// which mode can do what.
    ///
    /// The last case drives the install hook, because a table that is right about the modes is worthless if nothing
    /// ever decides which mode the scene is in. It restores whatever gateway was installed before it ran, so ordering
    /// this gate among the others cannot change another gate's result. </summary>
    public static class CityFeatureAvailabilityGate
    {
        [MenuItem("Window/Road City Master/Testing/Terrain feature availability")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError("RCMAVAIL FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();

            CaseEveryRowIsComplete(failures);
            CaseIdsAreUnique(failures);
            CaseProviderOnlyFeaturesNameWhatToConnect(failures);
            CaseNothingSilentlyDoesNothing(failures);
            CaseUnknownIdStaysUsable(failures);
            CaseModeCountsAgree(failures);
            CaseMarkdownIsOneRowPerFeature(failures);
            CaseSignatureIsStable(failures);
            CaseManualCarriesTheGeneratedTable(failures);
            CaseInstallHookDecidesTheMode(failures);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(failures.Count == 0 ? "RCMAVAIL result PASS" : "RCMAVAIL result FAIL");
            sb.Append("  cases 10  failures ").Append(failures.Count);
            for (int i = 0; i < failures.Count; i++) { sb.Append("\n  - ").Append(failures[i]); }
            report = sb.ToString();
            return failures.Count == 0;
        }

        /// <summary> A row with a blank sentence is worse than a missing row: the inspector would show empty space
        /// where an explanation belongs. Cells also may not contain a pipe, which would silently break the markdown
        /// table both documents render. </summary>
        private static void CaseEveryRowIsComplete(List<string> failures)
        {
            Expect(TerrainFeatureAvailability.Count >= 10,
                "the availability table should cover the whole feature surface, found "
                + TerrainFeatureAvailability.Count + " rows", failures);

            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                TerrainFeatureRow row = TerrainFeatureAvailability.Row(i);
                string where = "row " + i + " ('" + row.id + "')";
                Expect(!string.IsNullOrEmpty(row.id), where + " has no id", failures);
                Expect(!string.IsNullOrEmpty(row.title), where + " has no title", failures);
                Expect(row.standalone != null && row.standalone.Length >= 30,
                    where + " does not say what happens standalone", failures);
                Expect(row.connected != null && row.connected.Length >= 30,
                    where + " does not say what happens connected", failures);
                Expect(row.title.IndexOf('|') < 0 && row.standalone.IndexOf('|') < 0
                    && row.connected.IndexOf('|') < 0, where + " contains a pipe and would break the table", failures);
            }
        }

        private static void CaseIdsAreUnique(List<string> failures)
        {
            List<string> seen = new List<string>();
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                string id = TerrainFeatureAvailability.Row(i).id;
                Expect(!seen.Contains(id), "id '" + id + "' appears twice, so one row can never be found", failures);
                seen.Add(id);
            }
        }

        /// <summary> The rule itself: a provider-only feature's standalone sentence names what to connect, word for
        /// word the same instruction everywhere, and the feature reports itself unusable so a control can grey out.
        /// </summary>
        private static void CaseProviderOnlyFeaturesNameWhatToConnect(List<string> failures)
        {
            int providerOnly = 0;
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                TerrainFeatureRow row = TerrainFeatureAvailability.Row(i);
                if (row.support != TerrainFeatureSupport.ConnectedOnly) { continue; }
                providerOnly++;
                string where = "provider-only feature '" + row.id + "'";
                Expect(row.standalone.Contains(TerrainFeatureAvailability.WhatToConnect),
                    where + " does not tell the user what to connect", failures);
                Expect(!row.AvailableIn(TerrainProviderMode.Standalone),
                    where + " reports itself usable standalone", failures);
                Expect(row.AvailableIn(TerrainProviderMode.Connected),
                    where + " reports itself unusable connected", failures);
            }
            Expect(providerOnly > 0, "no feature is marked as needing a provider, which cannot be right", failures);
        }

        /// <summary> Two ways a feature could go silent: a sentence that explains nothing, or a row unusable in both
        /// modes, which would be a control that can never work. </summary>
        private static void CaseNothingSilentlyDoesNothing(List<string> failures)
        {
            string[] emptyPhrases = { "not supported", "TODO", "n/a", "N/A", "coming soon", "does nothing" };
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                TerrainFeatureRow row = TerrainFeatureAvailability.Row(i);
                for (int p = 0; p < emptyPhrases.Length; p++)
                {
                    Expect(!row.standalone.Contains(emptyPhrases[p]),
                        "'" + row.id + "' explains itself with '" + emptyPhrases[p] + "' rather than with what "
                        + "happens", failures);
                }
                Expect(row.AvailableIn(TerrainProviderMode.Standalone)
                    || row.AvailableIn(TerrainProviderMode.Connected),
                    "'" + row.id + "' is unusable in both modes, so it is a control that can never work", failures);
            }
        }

        /// <summary> A typo in a call site must not disable working functionality. The table is documentation, so an
        /// id it has never heard of stays usable and says so instead of greying a real control out. </summary>
        private static void CaseUnknownIdStaysUsable(List<string> failures)
        {
            const string unknown = "no.such.feature";
            bool found;
            TerrainFeatureRow row = TerrainFeatureAvailability.Find(unknown, out found);
            Expect(!found, "an id that is not in the table was reported as found", failures);
            Expect(row.id == unknown, "the placeholder row lost the id that was asked for", failures);
            Expect(row.title.Contains(unknown), "the placeholder row does not name the unknown id", failures);
            Expect(TerrainFeatureAvailability.AvailableIn(unknown, TerrainProviderMode.Standalone),
                "an unknown id disabled a control standalone", failures);
            Expect(TerrainFeatureAvailability.AvailableIn(unknown, TerrainProviderMode.Connected),
                "an unknown id disabled a control connected", failures);
            Expect(!string.IsNullOrEmpty(
                TerrainFeatureAvailability.SentenceFor(unknown, TerrainProviderMode.Standalone)),
                "an unknown id produced no sentence at all", failures);
        }

        /// <summary> Connected mode can do everything; standalone is short by exactly the provider-only rows. Anything
        /// else means the two answers -- the per-row flag and the summary list -- disagree. </summary>
        private static void CaseModeCountsAgree(List<string> failures)
        {
            int providerOnly = 0;
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                if (TerrainFeatureAvailability.Row(i).support == TerrainFeatureSupport.ConnectedOnly)
                { providerOnly++; }
            }

            IList<TerrainFeatureRow> missingConnected =
                TerrainFeatureAvailability.UnavailableIn(TerrainProviderMode.Connected);
            IList<TerrainFeatureRow> missingStandalone =
                TerrainFeatureAvailability.UnavailableIn(TerrainProviderMode.Standalone);

            Expect(missingConnected.Count == 0,
                "connected mode is missing " + missingConnected.Count + " features, and it should be missing none",
                failures);
            Expect(missingStandalone.Count == providerOnly,
                "standalone is missing " + missingStandalone.Count + " features but " + providerOnly
                + " are marked as needing a provider", failures);
        }

        /// <summary> One table, rendered. The markdown is generated so the manual and Universe Creator's documentation
        /// cannot drift, which only holds if every row reaches it exactly once with four columns. </summary>
        private static void CaseMarkdownIsOneRowPerFeature(List<string> failures)
        {
            string markdown = TerrainFeatureAvailability.ToMarkdown();
            string[] lines = markdown.Replace("\r", string.Empty).Split('\n');
            List<string> real = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length > 0) { real.Add(lines[i]); }
            }

            Expect(real.Count == TerrainFeatureAvailability.Count + 2,
                "the table has " + real.Count + " lines for " + TerrainFeatureAvailability.Count
                + " features plus a header and a separator", failures);

            for (int i = 0; i < real.Count; i++)
            {
                string line = real[i];
                Expect(line.StartsWith("|") && line.EndsWith("|"),
                    "table line " + i + " is not a table row: " + line, failures);
                Expect(CountPipes(line) == 5,
                    "table line " + i + " has " + (CountPipes(line) - 1) + " columns instead of 4", failures);
            }

            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                TerrainFeatureRow row = TerrainFeatureAvailability.Row(i);
                Expect(markdown.Contains(row.title), "'" + row.id + "' never reaches the rendered table", failures);
                Expect(markdown.Contains(row.standalone),
                    "'" + row.id + "' reaches the table without its standalone sentence", failures);
            }
        }

        /// <summary> The fingerprint the three consumers compare. Two calls in one session must agree, or nothing
        /// downstream can claim it read the same table. </summary>
        private static void CaseSignatureIsStable(List<string> failures)
        {
            string first = TerrainFeatureAvailability.Signature();
            string second = TerrainFeatureAvailability.Signature();
            Expect(first == second, "the table's signature changed between two calls in one session", failures);

            string[] lines = first.Split('\n');
            Expect(lines.Length == TerrainFeatureAvailability.Count,
                "the signature covers " + lines.Length + " rows but the table has "
                + TerrainFeatureAvailability.Count, failures);
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                Expect(first.Contains(TerrainFeatureAvailability.Row(i).id + "|"),
                    "'" + TerrainFeatureAvailability.Row(i).id + "' is not in the signature", failures);
            }
        }

        /// <summary> The document consumers, checked rather than trusted. BOTH manuals -- this tool's and the world
        /// side's integration chapter (UC-47) -- carry the table between markers and the writer regenerates it; this
        /// fails the build if what is in either file is not what the code would generate, which is the only way a
        /// copied table stays honest for longer than a month. Two documents is exactly the case where trust fails: a
        /// feature gets added, one manual is updated, and the other keeps promising the old answer to the other
        /// product's users. The fix when it fails is one menu item -- Documentation / Write the terrain availability
        /// table -- not a retype. </summary>
        private static void CaseManualCarriesTheGeneratedTable(List<string> failures)
        {
            string[] paths = TerrainFeatureTableWriter.DocumentPaths();
            string generated = TerrainFeatureTableWriter.Normalise(TerrainFeatureAvailability.ToMarkdown());

            for (int p = 0; p < paths.Length; p++)
            {
                string path = paths[p];
                string inFile;
                string message;
                bool read = TerrainFeatureTableWriter.ReadTableFrom(path, out inFile, out message);
                Expect(read, "the availability table could not be read: " + message, failures);
                if (!read) { continue; }

                string document = TerrainFeatureTableWriter.Normalise(inFile);
                Expect(document == generated,
                    "the availability table in '" + path + "' has drifted from the code -- run Window/Road City "
                    + "Master/Documentation/Write the terrain availability table", failures);

                if (document != generated)
                {
                    string[] wanted = generated.Split('\n');
                    string[] found = document.Split('\n');
                    Expect(false, "table line count: document " + found.Length + ", code " + wanted.Length, failures);
                    for (int i = 0; i < wanted.Length && i < found.Length; i++)
                    {
                        if (wanted[i] == found[i]) { continue; }
                        Expect(false, "first differing table line is " + i, failures);
                        break;
                    }
                }
            }
        }

        /// <summary> The install hook. A correct table is useless if nothing decides which mode the scene is in, so
        /// this drives the decision and checks that it is stated in words and matches the gateway that came out of it.
        /// Whatever gateway was installed before is put back, so this gate cannot change another one's result.
        /// </summary>
        private static void CaseInstallHookDecidesTheMode(List<string> failures)
        {
            TerrainWriteGateway saved = TerrainWriteGateway.Active;
            try
            {
                bool connected = TerrainGatewayBootstrap.Refresh();
                string decision = TerrainGatewayBootstrap.LastDecision;

                Expect(TerrainGatewayBootstrap.Decided, "the bootstrap ran without recording that it decided",
                    failures);
                Expect(!string.IsNullOrEmpty(decision), "the bootstrap decided without saying what it decided",
                    failures);

                TerrainWriteGateway installed = TerrainWriteGateway.Active;
                Expect(installed != null, "the bootstrap installed no gateway at all", failures);
                Expect(installed.Mode == (connected ? TerrainProviderMode.Connected : TerrainProviderMode.Standalone),
                    "the bootstrap reported " + (connected ? "connected" : "standalone")
                    + " but installed a gateway in mode " + installed.Mode, failures);

                if (!connected)
                {
                    Expect(decision.Contains(TerrainFeatureAvailability.WhatToConnect),
                        "the standalone decision does not tell the user what to connect", failures);
                    Expect(installed.Sink is DirectTerrainSink,
                        "standalone installed a sink that does not write the terrain itself", failures);
                }
                else
                {
                    Expect(installed.Sink is ProviderTerrainSink,
                        "connected installed a sink that writes the terrain directly", failures);
                    Expect(!string.IsNullOrEmpty(installed.ProviderName),
                        "connected mode does not name the provider it connected to", failures);
                }

                // Forcing standalone must be honoured whatever is in the scene, and must be stated, not silent.
                TerrainGatewayBootstrap.UseStandalone();
                Expect(TerrainWriteGateway.Active.Mode == TerrainProviderMode.Standalone,
                    "asking for standalone did not produce a standalone gateway", failures);
                Expect(TerrainGatewayBootstrap.LastDecision != decision || !connected,
                    "asking for standalone from connected mode did not change the reported decision", failures);

                // Deciding twice from an unchanged scene must reach the same answer -- the decision is evidence, not
                // a coin toss (C-06).
                bool again = TerrainGatewayBootstrap.Refresh();
                Expect(again == connected, "two scans of the same scene disagreed about whether a provider is present",
                    failures);
                Expect(TerrainGatewayBootstrap.LastDecision == decision,
                    "two scans of the same scene reported different decisions", failures);
            }
            finally
            {
                TerrainWriteGateway.Install(saved);
            }
        }

        private static int CountPipes(string line)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '|') { count++; }
            }
            return count;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }
    }
}
#endif

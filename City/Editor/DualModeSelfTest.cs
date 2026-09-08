// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-72's gate: build ONE fixed fixture through both terrain modes and account for every difference.
    ///
    /// The gate RM-72 asks for, clause by clause, and where each is proved:
    ///
    /// - "reports, per feature, identical / expected-difference / unexpected-difference" -- every RCMDUAL feature line
    ///   carries the feature id, the support level and the verdict.
    /// - "with the expected differences read from the availability table rather than hard-coded in the test" -- the
    ///   comparison walks <see cref="TerrainFeatureAvailability.Rows"/>; this file contains no list of which features
    ///   may differ, and <see cref="CaseExpectationsComeFromTable"/> checks the licensed set is exactly the table's
    ///   non-BothModes rows, so a hard-coded expectation would show up as a mismatch.
    /// - "an unexpected difference fails the test and names the feature" -- proved by fabricating the three defects the
    ///   comparison exists to catch and asserting each is caught AND named. Without that, a comparison that always
    ///   answered "identical" would pass this gate.
    /// - "removing the provider from the connected run and re-running produces exactly the standalone result" -- the
    ///   run through <c>ForProvider(null, ...)</c> is compared, signature for signature, with the standalone run.
    /// - "the test passes twice in a row from the same seed" -- the whole comparison runs twice and both the verdicts
    ///   and the run signatures must match.
    ///
    /// What this gate does NOT claim: that any heightfield was applied. No scene <c>Terrain</c> exists in a headless
    /// fixture, so the standalone sink's writes are dispatched against a null terrain and return without touching the
    /// engine. What is under test is the dispatch, the accounting, the geometry either mode produced, and the words a
    /// mode used when it had to refuse -- exactly the surface RM-57 built and RM-58 documented. </summary>
    public static class DualModeSelfTest
    {
        [MenuItem("Window/Road City Master/Testing/Dual-mode self-test (same fixture, both modes)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); } else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: -executeMethod
        /// RoadCityMaster.City.Editor.DualModeSelfTest.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMDUAL BEGIN\n");
            List<string> failures = new List<string>();

            CaseBothModesCompared(builder, failures);
            CaseExpectationsComeFromTable(builder, failures);
            CaseUnexpectedPlanDifferenceIsCaught(builder, failures);
            CaseConnectedOnlyAgreementIsCaught(builder, failures);
            CaseUnwordedRefusalIsCaught(builder, failures);
            CaseRemovingProviderReproducesStandalone(builder, failures);
            CaseTwiceInARow(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMDUAL FAIL   ").Append(failures[i]).Append('\n');
            }
            builder.Append("RCMDUAL result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("RCMDUAL END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Records a failure when the condition is false. </summary>
        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition) { failures.Add(failureMessage); }
        }


        #region "The shared pair of runs"
        /// <summary> Build the fixture once per mode and compare. Each mode gets its OWN gateway, never the installed
        /// one, so the gate cannot be affected by whatever the project happens to have installed and cannot leave a
        /// gateway behind. Both runs release everything they hold before returning; the comparison asserts that. </summary>
        private static DualModeComparison BuildBothAndCompare(out DualModeRun standalone, out DualModeRun connected)
        {
            TerrainWriteGateway standaloneGateway = TerrainWriteGateway.ForStandalone();
            standalone = DualModeFixture.Build(standaloneGateway);
            standaloneGateway.ReleaseAll();

            TerrainWriteGateway connectedGateway = TerrainWriteGateway.ForProvider(new FakePcgTerrain(),
                "dual mode fixture provider");
            connected = DualModeFixture.Build(connectedGateway);
            connectedGateway.ReleaseAll();

            return DualModeComparison.Compare(standalone, connected);
        }
        #endregion


        #region "Case 1 -- the per-feature report"
        /// <summary> The report RM-72 asks for, and the verdict on it. Every table feature is exercised, the ledgers
        /// match, nothing is left held, and no feature differs without the table licensing it. Each failing finding is
        /// reported as its own failure line so the feature is NAMED rather than folded into a count. </summary>
        private static void CaseBothModesCompared(StringBuilder builder, List<string> failures)
        {
            DualModeRun standalone;
            DualModeRun connected;
            DualModeComparison comparison = BuildBothAndCompare(out standalone, out connected);

            builder.Append("RCMDUAL standalone path ").Append(standalone.path.ToString())
                   .Append(" connected path ").Append(connected.path.ToString()).Append('\n');
            comparison.AppendReport(builder, "RCMDUAL");

            for (int i = 0; i < comparison.findings.Count; i++)
            {
                DualModeFinding f = comparison.findings[i];
                if (!f.IsFailure) { continue; }
                failures.Add(f.verdict.ToString() + " on feature " + f.featureId + ": " + f.why);
            }
            for (int i = 0; i < comparison.gaps.Count; i++)
            {
                failures.Add("the fixture never exercised table feature " + comparison.gaps[i]);
            }

            Expect(comparison.findings.Count == TerrainFeatureAvailability.Count,
                "compared " + comparison.findings.Count.ToString(CultureInfo.InvariantCulture) + " features but the "
                + "availability table has " + TerrainFeatureAvailability.Count.ToString(CultureInfo.InvariantCulture),
                failures);
            Expect(comparison.ledgersMatch,
                "the two modes produced different terrain ledgers, which the path-free signature must not allow:\n"
                + "standalone " + comparison.standaloneLedger + "\nconnected  " + comparison.connectedLedger,
                failures);
            Expect(comparison.buildCountsMatch, "the two modes closed a different number of builds", failures);
            Expect(comparison.holdsAllReleased, "a mode finished the fixture still holding terrain open", failures);
            Expect(standalone.path == TerrainWritePath.DirectWrite,
                "standalone dispatched writes down " + standalone.path.ToString(), failures);
            Expect(connected.path == TerrainWritePath.RequestedThroughProvider,
                "connected dispatched writes down " + connected.path.ToString(), failures);
        }
        #endregion


        #region "Case 2 -- the expectations came from the table"
        /// <summary> The clause that stops this gate from being a list of excuses: every verdict must be explicable from
        /// RM-58's table alone. Read the table here, independently of the comparison, and require that the features it
        /// calls BothModes came out Identical and that every difference the comparison licensed belongs to a row the
        /// table marks BetterConnected or ConnectedOnly. A hard-coded expectation inside the comparison shows up as a
        /// licensed difference on a BothModes row, or a support level that disagrees with the row it claims to be.
        /// </summary>
        private static void CaseExpectationsComeFromTable(StringBuilder builder, List<string> failures)
        {
            DualModeRun standalone;
            DualModeRun connected;
            DualModeComparison comparison = BuildBothAndCompare(out standalone, out connected);

            int licensed = 0;
            int bothModes = 0;
            for (int i = 0; i < comparison.findings.Count; i++)
            {
                DualModeFinding f = comparison.findings[i];
                bool found;
                TerrainFeatureRow row = TerrainFeatureAvailability.Find(f.featureId, out found);
                Expect(found, "the comparison judged feature " + f.featureId + " which is not in the table", failures);
                if (!found) { continue; }

                Expect(f.support == row.support,
                    "feature " + f.featureId + " was judged as " + f.support.ToString() + " but the table says "
                    + row.support.ToString(), failures);

                if (row.support == TerrainFeatureSupport.BothModes)
                {
                    bothModes++;
                    Expect(f.verdict == DualModeVerdict.Identical,
                        "the table says " + f.featureId + " is identical either way, but the comparison returned "
                        + f.verdict.ToString(), failures);
                }
                if (f.verdict != DualModeVerdict.ExpectedDifference) { continue; }
                licensed++;
                Expect(row.support != TerrainFeatureSupport.BothModes,
                    "a difference was licensed on " + f.featureId + ", a BothModes row -- the expectation did not come "
                    + "from the table", failures);
                Expect(row.AvailableIn(TerrainProviderMode.Connected),
                    "a difference was licensed on " + f.featureId + " but the table does not offer it connected either",
                    failures);
            }

            builder.Append("RCMDUAL table bothModes ").Append(bothModes.ToString(CultureInfo.InvariantCulture))
                   .Append(" licensedDifferences ").Append(licensed.ToString(CultureInfo.InvariantCulture))
                   .Append(" tableRows ")
                   .Append(TerrainFeatureAvailability.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            Expect(licensed > 0,
                "no difference at all was licensed, so the fixture is not exercising the modes: a fixture where every "
                + "feature agrees cannot tell a working gateway from a broken one", failures);
        }
        #endregion


        #region "Cases 3-5 -- the detector actually detects, and names what it found"
        /// <summary> A one-feature run, for feeding the comparison a defect on purpose. The aggregate fields are set to
        /// agreeing values so the fabricated feature is the only thing under judgement. </summary>
        private static DualModeRun Fabricate(TerrainProviderMode mode, string featureId, string plan, string request,
            string outcome, string refusal)
        {
            DualModeRun run = new DualModeRun();
            run.mode = mode;
            run.modeName = mode == TerrainProviderMode.Connected ? "connected" : "standalone";
            run.path = mode == TerrainProviderMode.Connected
                ? TerrainWritePath.RequestedThroughProvider : TerrainWritePath.DirectWrite;
            run.ledgerSignature = "fabricated";
            run.buildCount = 1;
            run.notifyCount = 1;
            run.outstandingHolds = 0;

            DualModeFeatureRecord record = new DualModeFeatureRecord();
            record.featureId = featureId;
            record.plan = plan;
            record.request = request;
            record.outcome = outcome;
            record.refusal = refusal;
            run.features.Add(record);
            return run;
        }

        /// <summary> The finding for one fabricated feature, or a default with an empty id when the comparison did not
        /// judge it at all. </summary>
        private static DualModeFinding FindingFor(DualModeComparison comparison, string featureId)
        {
            for (int i = 0; i < comparison.findings.Count; i++)
            {
                if (comparison.findings[i].featureId == featureId) { return comparison.findings[i]; }
            }
            return new DualModeFinding();
        }

        /// <summary> The first defect: geometry that changed when the ground changed hands. No support level licenses
        /// it, and the message must name the feature, because the point of the test is to send the author to one file.
        /// </summary>
        private static void CaseUnexpectedPlanDifferenceIsCaught(StringBuilder builder, List<string> failures)
        {
            const string id = "road.details";
            DualModeComparison comparison = DualModeComparison.Compare(
                Fabricate(TerrainProviderMode.Standalone, id, "culvert barrel 0.9000", "details", "granted", ""),
                Fabricate(TerrainProviderMode.Connected, id, "culvert barrel 1.2000", "details", "granted", ""));

            DualModeFinding f = FindingFor(comparison, id);
            builder.Append("RCMDUAL detector plan-difference ").Append(f.verdict.ToString())
                   .Append(" names ").Append(f.why.Contains(id)).Append('\n');
            Expect(f.featureId == id, "the plan-difference defect was not judged at all", failures);
            Expect(f.verdict == DualModeVerdict.UnexpectedDifference,
                "a plan difference on a BothModes feature returned " + f.verdict.ToString(), failures);
            Expect(f.IsFailure, "a plan difference did not count as a failure", failures);
            Expect(f.why.Contains(id), "the plan-difference message does not name the feature", failures);
            Expect(!comparison.Passed, "a comparison carrying a plan difference still reported Passed", failures);
        }

        /// <summary> The defect a naive "both runs matched" test would applaud: a provider-only feature that standalone
        /// produced identically. That is standalone faking a cut it cannot make, and it must fail. </summary>
        private static void CaseConnectedOnlyAgreementIsCaught(StringBuilder builder, List<string> failures)
        {
            const string id = "tunnel.cavity";
            DualModeComparison comparison = DualModeComparison.Compare(
                Fabricate(TerrainProviderMode.Standalone, id, "bore 4.5000", "opening", "build CutAndLined", ""),
                Fabricate(TerrainProviderMode.Connected, id, "bore 4.5000", "opening", "build CutAndLined", ""));

            DualModeFinding f = FindingFor(comparison, id);
            builder.Append("RCMDUAL detector connected-only-agreement ").Append(f.verdict.ToString()).Append('\n');
            Expect(f.featureId == id, "the faked provider-only feature was not judged at all", failures);
            Expect(f.verdict == DualModeVerdict.UnexpectedDifference,
                "standalone matching connected on a ConnectedOnly feature returned " + f.verdict.ToString()
                + " instead of a failure", failures);
            Expect(!comparison.Passed, "a comparison where standalone faked a provider-only feature Passed", failures);
        }

        /// <summary> The third defect: the refusal is correct but silent. A feature that quietly does nothing is worse
        /// than one that says what to connect, so an unworded refusal is its own verdict rather than an excused
        /// difference. </summary>
        private static void CaseUnwordedRefusalIsCaught(StringBuilder builder, List<string> failures)
        {
            const string id = "building.pad";
            DualModeComparison silent = DualModeComparison.Compare(
                Fabricate(TerrainProviderMode.Standalone, id, "pad 18x12", "pad", "pad not held", ""),
                Fabricate(TerrainProviderMode.Connected, id, "pad 18x12", "pad", "pad held, released True", ""));
            DualModeFinding silentFinding = FindingFor(silent, id);

            DualModeComparison worded = DualModeComparison.Compare(
                Fabricate(TerrainProviderMode.Standalone, id, "pad 18x12", "pad", "pad not held",
                    "a building pad held against terrain regeneration needs a connected terrain provider"),
                Fabricate(TerrainProviderMode.Connected, id, "pad 18x12", "pad", "pad held, released True", ""));
            DualModeFinding wordedFinding = FindingFor(worded, id);

            builder.Append("RCMDUAL detector unworded-refusal ").Append(silentFinding.verdict.ToString())
                   .Append(" worded ").Append(wordedFinding.verdict.ToString()).Append('\n');
            Expect(silentFinding.verdict == DualModeVerdict.RefusalNotWorded,
                "a silent provider-only refusal returned " + silentFinding.verdict.ToString(), failures);
            Expect(silentFinding.IsFailure, "a silent refusal did not count as a failure", failures);
            Expect(wordedFinding.verdict == DualModeVerdict.ExpectedDifference,
                "a worded provider-only refusal returned " + wordedFinding.verdict.ToString()
                + " instead of an expected difference", failures);
        }
        #endregion


        #region "Cases 6-7 -- unplugging the provider, and passing twice"
        /// <summary> RM-72's sharpest clause: take the provider away from the connected run and the result must be the
        /// standalone result EXACTLY -- not merely a passing run. The gateway's own fallback is what is under test here:
        /// <c>ForProvider(null, ...)</c> is documented to fall back to standalone rather than to a gateway that refuses
        /// everything, and this compares the two runs signature for signature, so a fallback that produced a
        /// nearly-standalone run (a different path, a lost write, an extra announcement) fails. </summary>
        private static void CaseRemovingProviderReproducesStandalone(StringBuilder builder, List<string> failures)
        {
            TerrainWriteGateway plain = TerrainWriteGateway.ForStandalone();
            DualModeRun standalone = DualModeFixture.Build(plain);
            plain.ReleaseAll();

            TerrainWriteGateway unplugged = TerrainWriteGateway.ForProvider(null, "provider removed");
            DualModeRun withoutProvider = DualModeFixture.Build(unplugged);
            unplugged.ReleaseAll();

            bool same = standalone.Signature() == withoutProvider.Signature();
            builder.Append("RCMDUAL provider removed -> mode ").Append(withoutProvider.modeName)
                   .Append(" path ").Append(withoutProvider.path.ToString())
                   .Append(" matchesStandalone ").Append(same).Append('\n');

            Expect(unplugged.Mode == TerrainProviderMode.Standalone,
                "removing the provider left the gateway in " + unplugged.Mode.ToString() + " mode", failures);
            Expect(withoutProvider.path == TerrainWritePath.DirectWrite,
                "with no provider, writes went down " + withoutProvider.path.ToString(), failures);
            Expect(same, "removing the provider did not reproduce the standalone result exactly:\nstandalone "
                + standalone.Signature() + "\nunplugged  " + withoutProvider.Signature(), failures);
        }

        /// <summary> Twice in a row from the same seed: the same verdicts and, byte for byte, the same two runs. A test
        /// that passes once and differs the second time is not a determinism gate, it is a coincidence. </summary>
        private static void CaseTwiceInARow(StringBuilder builder, List<string> failures)
        {
            DualModeRun firstStandalone;
            DualModeRun firstConnected;
            DualModeComparison first = BuildBothAndCompare(out firstStandalone, out firstConnected);

            DualModeRun secondStandalone;
            DualModeRun secondConnected;
            DualModeComparison second = BuildBothAndCompare(out secondStandalone, out secondConnected);

            StringBuilder firstReport = new StringBuilder();
            StringBuilder secondReport = new StringBuilder();
            first.AppendReport(firstReport, "RCMDUAL");
            second.AppendReport(secondReport, "RCMDUAL");

            bool sameStandalone = firstStandalone.Signature() == secondStandalone.Signature();
            bool sameConnected = firstConnected.Signature() == secondConnected.Signature();
            bool sameReport = firstReport.ToString() == secondReport.ToString();

            builder.Append("RCMDUAL twice passed ").Append(first.Passed).Append('/').Append(second.Passed)
                   .Append(" sameStandalone ").Append(sameStandalone)
                   .Append(" sameConnected ").Append(sameConnected)
                   .Append(" sameReport ").Append(sameReport).Append('\n');

            Expect(first.Passed && second.Passed,
                "the comparison did not pass twice in a row (" + first.Passed + "/" + second.Passed + ")", failures);
            Expect(sameStandalone, "two standalone runs of the same fixture differ", failures);
            Expect(sameConnected, "two connected runs of the same fixture differ", failures);
            Expect(sameReport, "the per-feature report is not reproducible", failures);
        }
        #endregion
    }
}
#endif

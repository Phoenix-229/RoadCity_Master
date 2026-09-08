// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> What the comparison concluded about ONE feature, and why. </summary>
    public struct DualModeFinding
    {
        public string featureId;

        /// <summary> The support level read from RM-58's availability table -- the only place the expectation comes
        /// from. Nothing in this file decides which features are allowed to differ. </summary>
        public TerrainFeatureSupport support;

        public DualModeVerdict verdict;

        /// <summary> One line naming the axis that decided the verdict, so a failure reads as a sentence rather than a
        /// verdict code. </summary>
        public string why;

        /// <summary> Whether this finding fails the test. An expected difference does not; a difference the table did
        /// not license does, and so does a provider-only feature that was refused without saying what to connect.
        /// </summary>
        public bool IsFailure
        {
            get
            {
                return verdict == DualModeVerdict.UnexpectedDifference
                    || verdict == DualModeVerdict.RefusalNotWorded;
            }
        }
    }


    /// <summary> RM-72's comparison: build the same fixture in both modes and say, per feature, whether the two runs
    /// agreed, differed for a reason the availability table gives, or differed for no reason at all.
    ///
    /// The rules are deliberately asymmetric, because "the outputs should match" is false of everything:
    ///
    /// - The PLAN must be identical for every feature, with no exception the table can grant. None of it asks the
    ///   ground a question, so a difference is not a mode regression at all -- it is an unseeded generator, a
    ///   hash-ordered iteration or a build-order dependency. That is the first defect this test finds, and it names
    ///   the feature so the search starts in one file.
    /// - The REQUEST must be identical too. The same fixture asks the gateway for the same ground either way; RM-57's
    ///   ledger signature leaves the dispatch path out precisely so the two are comparable.
    /// - The OUTCOME is the only axis the table may excuse. A BothModes row that differs is a defect. A ConnectedOnly
    ///   row that AGREES is also a defect -- that is standalone quietly faking a feature it cannot do, which is the
    ///   failure § 10.24 exists to prevent and the one a "both runs matched" test would applaud.
    ///
    /// The ledger is compared whole, and every hold must be back. Announcement counts are REPORTED, not compared:
    /// connected mode accumulates the tunnel opening and the building pad it is able to hold, so it legitimately has
    /// more ground to announce, and coalescing then makes the count a property of the batcher rather than of the
    /// modes. </summary>
    public sealed class DualModeComparison
    {
        public readonly List<DualModeFinding> findings = new List<DualModeFinding>();

        /// <summary> Table features the fixture never exercised. Reported as failures: a feature nobody built cannot
        /// be said to behave the same in both modes. </summary>
        public readonly List<string> gaps = new List<string>();

        public int identicalCount;
        public int expectedDifferenceCount;
        public int unexpectedDifferenceCount;
        public int refusalNotWordedCount;

        public bool ledgersMatch;
        public bool buildCountsMatch;
        public bool holdsAllReleased;

        public string standaloneLedger = string.Empty;
        public string connectedLedger = string.Empty;
        public int standaloneNotify;
        public int connectedNotify;
        public int suppressedWrites;

        /// <summary> The whole verdict: no unlicensed difference, no unworded refusal, no unexercised feature, one
        /// shared ledger, and nothing still held open. </summary>
        public bool Passed
        {
            get
            {
                return unexpectedDifferenceCount == 0 && refusalNotWordedCount == 0 && gaps.Count == 0
                    && ledgersMatch && buildCountsMatch && holdsAllReleased;
            }
        }

        /// <summary> Compare two runs of the fixture, walking the availability table rather than a list typed here, so
        /// a feature added to the table is compared without editing this file (and shows up as a gap until the fixture
        /// exercises it). </summary>
        public static DualModeComparison Compare(DualModeRun standalone, DualModeRun connected)
        {
            DualModeComparison c = new DualModeComparison();
            if (standalone == null || connected == null) { return c; }

            c.standaloneLedger = standalone.ledgerSignature;
            c.connectedLedger = connected.ledgerSignature;
            c.ledgersMatch = standalone.ledgerSignature == connected.ledgerSignature;
            c.buildCountsMatch = standalone.buildCount == connected.buildCount;
            c.holdsAllReleased = standalone.outstandingHolds == 0 && connected.outstandingHolds == 0;
            c.standaloneNotify = standalone.notifyCount;
            c.connectedNotify = connected.notifyCount;
            c.suppressedWrites = connected.suppressedWrites;

            IList<TerrainFeatureRow> rows = TerrainFeatureAvailability.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                TerrainFeatureRow row = rows[i];
                bool inStandalone;
                bool inConnected;
                DualModeFeatureRecord s = standalone.Feature(row.id, out inStandalone);
                DualModeFeatureRecord n = connected.Feature(row.id, out inConnected);
                if (!inStandalone || !inConnected)
                {
                    c.gaps.Add(row.id + " (standalone " + inStandalone + ", connected " + inConnected + ")");
                    continue;
                }
                c.Record(Judge(row, s, n));
            }
            return c;
        }

        /// <summary> The rules, in the order that makes a failure informative: a plan difference first (it is a
        /// determinism defect and swamps everything else), then a request difference, then the outcome the table has
        /// something to say about. </summary>
        private static DualModeFinding Judge(TerrainFeatureRow row, DualModeFeatureRecord s, DualModeFeatureRecord n)
        {
            DualModeFinding f = new DualModeFinding();
            f.featureId = row.id;
            f.support = row.support;

            if (s.plan != n.plan)
            {
                f.verdict = DualModeVerdict.UnexpectedDifference;
                f.why = "the plan differs between modes, which no support level licenses: geometry that never asks "
                    + "the ground a question changed when the ground changed hands, so this is a determinism defect "
                    + "in " + row.id + ", not a mode difference";
                return f;
            }
            if (s.request != n.request)
            {
                f.verdict = DualModeVerdict.UnexpectedDifference;
                f.why = "the same fixture asked the terrain gateway for different ground in the two modes";
                return f;
            }

            bool outcomesAgree = s.outcome == n.outcome;
            if (outcomesAgree)
            {
                if (row.support == TerrainFeatureSupport.ConnectedOnly)
                {
                    f.verdict = DualModeVerdict.UnexpectedDifference;
                    f.why = "the table says this cannot be done without a provider, yet standalone produced the same "
                        + "outcome as connected -- standalone is faking it instead of refusing in words";
                    return f;
                }
                f.verdict = DualModeVerdict.Identical;
                f.why = "same plan, same request, same outcome";
                return f;
            }

            if (row.support == TerrainFeatureSupport.BothModes)
            {
                f.verdict = DualModeVerdict.UnexpectedDifference;
                f.why = "the table says this is identical either way, but the outcome differs: standalone \""
                    + Trim(s.outcome) + "\" versus connected \"" + Trim(n.outcome) + "\"";
                return f;
            }

            if (row.support == TerrainFeatureSupport.ConnectedOnly && !RefusalIsWorded(row, s))
            {
                f.verdict = DualModeVerdict.RefusalNotWorded;
                f.why = "standalone could not do this, correctly, but said nothing that tells the author what to "
                    + "connect";
                return f;
            }

            f.verdict = DualModeVerdict.ExpectedDifference;
            f.why = "the table's " + row.support.ToString() + " row licenses this: standalone \"" + Trim(s.outcome)
                + "\" versus connected \"" + Trim(n.outcome) + "\"";
            return f;
        }

        /// <summary> Whether a provider-only refusal actually told the author something. Two halves, both from the
        /// table: the run must have carried refusal words at all, and the table's own standalone sentence for this
        /// feature must end in the one instruction that resolves it. Nothing here retypes that sentence. </summary>
        private static bool RefusalIsWorded(TerrainFeatureRow row, DualModeFeatureRecord standaloneRecord)
        {
            if (string.IsNullOrEmpty(standaloneRecord.refusal)) { return false; }
            string sentence = TerrainFeatureAvailability.SentenceFor(row.id, TerrainProviderMode.Standalone);
            if (string.IsNullOrEmpty(sentence)) { return false; }
            return sentence.Contains(TerrainFeatureAvailability.WhatToConnect);
        }

        /// <summary> Outcomes are multi-line for some features; a report line wants one. </summary>
        private static string Trim(string outcome)
        {
            if (string.IsNullOrEmpty(outcome)) { return string.Empty; }
            int newline = outcome.IndexOf('\n');
            string first = newline < 0 ? outcome : outcome.Substring(0, newline);
            if (first.Length > 120) { first = first.Substring(0, 120); }
            return first;
        }

        private void Record(DualModeFinding f)
        {
            findings.Add(f);
            if (f.verdict == DualModeVerdict.Identical) { identicalCount++; }
            else if (f.verdict == DualModeVerdict.ExpectedDifference) { expectedDifferenceCount++; }
            else if (f.verdict == DualModeVerdict.RefusalNotWorded) { refusalNotWordedCount++; }
            else { unexpectedDifferenceCount++; }
        }

        /// <summary> The per-feature table the gate prints: one line per feature, its support level, its verdict and
        /// the reason. This is the output RM-72 asks for -- identical / expected-difference / unexpected-difference
        /// stated per feature, not a single pass line. </summary>
        public void AppendReport(StringBuilder builder, string prefix)
        {
            if (builder == null) { return; }
            string tag = prefix != null ? prefix : string.Empty;
            for (int i = 0; i < findings.Count; i++)
            {
                DualModeFinding f = findings[i];
                builder.Append(tag).Append(" feature ").Append(f.featureId)
                       .Append("  ").Append(f.support.ToString())
                       .Append("  ").Append(f.verdict.ToString())
                       .Append("  ").Append(f.why).Append('\n');
            }
            for (int i = 0; i < gaps.Count; i++)
            {
                builder.Append(tag).Append(" gap     ").Append(gaps[i]).Append('\n');
            }
            builder.Append(tag).Append(" totals identical ")
                   .Append(identicalCount.ToString(CultureInfo.InvariantCulture))
                   .Append(" expected ").Append(expectedDifferenceCount.ToString(CultureInfo.InvariantCulture))
                   .Append(" unexpected ").Append(unexpectedDifferenceCount.ToString(CultureInfo.InvariantCulture))
                   .Append(" unworded ").Append(refusalNotWordedCount.ToString(CultureInfo.InvariantCulture))
                   .Append(" gaps ").Append(gaps.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(tag).Append(" ledgersMatch ").Append(ledgersMatch)
                   .Append(" buildCountsMatch ").Append(buildCountsMatch)
                   .Append(" holdsAllReleased ").Append(holdsAllReleased)
                   .Append(" notify ").Append(standaloneNotify.ToString(CultureInfo.InvariantCulture))
                   .Append('/').Append(connectedNotify.ToString(CultureInfo.InvariantCulture))
                   .Append(" suppressedWrites ").Append(suppressedWrites.ToString(CultureInfo.InvariantCulture))
                   .Append('\n');
        }
    }
}
#endif

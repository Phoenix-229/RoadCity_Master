#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-6. THE ANSWER, for one settlement, to "does this place have a railway, and if so how much of
    /// one". One struct, returned by <see cref="SettlementRailPresence.Resolve"/>, holding both the decision and every
    /// figure the decision was made from -- because a settlement that came out unserved and a settlement that came out
    /// with four stations are answered by the same curve, and the only way an author can tell whether the curve is wrong
    /// or the draw was unlucky is to be shown the probability that produced it.
    ///
    /// It carries a REFUSAL rather than a bool, on the doctrine every other computing type in this folder follows: a
    /// resolver that cannot answer says why in words the inspector can print, and never silently returns the
    /// no-railway answer for a table that simply was not there. Those two outcomes look identical to a caller reading a
    /// bool and mean completely different things -- one is a village, the other is a missing asset. </summary>
    [Serializable]
    public struct SettlementRailReport
    {
        /// <summary> Whether the resolver could answer at all. False means <see cref="refusal"/> is set and every figure
        /// below is meaningless -- NOT that the settlement is unserved. </summary>
        public bool resolved;

        /// <summary> Why it could not answer, empty when it could. </summary>
        public string refusal;

        /// <summary> The decision: whether this settlement is served by a railway. </summary>
        public bool served;

        /// <summary> The probability the curve gave for a settlement of this order and population, in [0, 1]. Kept even
        /// when <see cref="served"/> is false, because 0.48 coming out unserved is a coin that fell one way and 0.02
        /// coming out unserved is the curve working, and an author debugging a rail-less region needs to know
        /// which. </summary>
        public float presenceProbability;

        /// <summary> The uniform draw in [0, 1) that was compared against <see cref="presenceProbability"/>. Recorded so
        /// the comparison is reproducible by hand: served is exactly <c>draw &lt; presenceProbability</c>. </summary>
        public float presenceDraw;

        /// <summary> How many stations, when served; zero when not. Never zero when served -- see
        /// <see cref="SettlementRailFitRow.StationCount"/> for why a served settlement with no station is a
        /// contradiction rather than an edge case. </summary>
        public int stationCount;

        /// <summary> The typical distance between this settlement's stations, in metres, drawn from the row's spacing
        /// quantiles. Zero when unserved. </summary>
        public float stationSpacingMetres;

        /// <summary> How much track this settlement implies, in metres. Zero when unserved. DERIVED from the two figures
        /// above rather than fitted, for the reason <see cref="SettlementRailFitRow.TrackLengthMetres"/>
        /// documents. </summary>
        public float trackLengthMetres;

        /// <summary> The order and bucket the answer was read from, and whether the bucket was substituted. When
        /// <see cref="usedWorldFallback"/> is true the answer came from the world curve because the named continent had
        /// no row, which is an honest answer and a different one -- so it is reported rather than hidden. </summary>
        public int orderIndex;
        public string continentKey;
        public bool usedWorldFallback;

        /// <summary> Whether the curve that produced this was fitted from a real dump or is one of the authored
        /// placeholders <see cref="SettlementFitTable.CreateAuthored"/> writes. Carried on the REPORT and not just on
        /// the table, so a figure copied out of an inspector into a note still says what it rests on. </summary>
        public bool fromFittedCurve;

        /// <summary> The population the curve was asked about. Recorded because the caller's population may itself have
        /// been proposed rather than authored, and a wrong railway is often a wrong population. </summary>
        public int population;

        /// <summary> One line, in the register's voice: the decision, the odds behind it, and what it rests on. It
        /// deliberately does NOT restate the station count and spacing when unserved, because zero stations 2,500 m apart
        /// is a sentence that reads like a measurement and is not one. </summary>
        public string Describe()
        {
            if (!resolved)
            {
                return "railway: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            string basis = (fromFittedCurve ? "fitted" : "AUTHORED PLACEHOLDER") + " curve for order " + orderIndex
                           + " in " + continentKey + (usedWorldFallback ? " (world fallback)" : "");
            if (!served)
            {
                return "railway: none. " + Figure(presenceProbability * 100f, "%") + " chance at "
                       + population.ToString("N0", CultureInfo.InvariantCulture) + " people, drew "
                       + Figure(presenceDraw * 100f, "%") + ". " + basis + ".";
            }
            return "railway: " + stationCount + (stationCount == 1 ? " station" : " stations") + " about "
                   + Figure(stationSpacingMetres, " m") + " apart, " + Figure(trackLengthMetres, " m")
                   + " of track. " + Figure(presenceProbability * 100f, "%") + " chance at "
                   + population.ToString("N0", CultureInfo.InvariantCulture) + " people, drew "
                   + Figure(presenceDraw * 100f, "%") + ". " + basis + ".";
        }

        private static string Figure(float value, string unit)
        {
            return value.ToString(Mathf.Abs(value) < 10f ? "0.0" : "0", CultureInfo.InvariantCulture) + unit;
        }
    }


    /// <summary> B-27 / D-6. Turns a fitted rail row and one settlement's figures into a
    /// <see cref="SettlementRailReport"/>. A static resolver, not a component: it computes and refuses, it never mutates
    /// a scene, never touches an asset and never draws.
    ///
    /// WHY THE DRAW IS SEEDED AND NOT RANDOM. Presence is a probability, so answering it needs a number in [0, 1) -- and
    /// C-06 forbids that number coming from anywhere but the city seed and the settlement's own identity. A settlement
    /// that has a railway on one bake and not the next is not a simulation of anything; it is a bug that looks like
    /// variety. So the draw comes from <see cref="DeterministicRandom.ForElement"/> keyed on the city seed mixed with the
    /// settlement's identity string, which makes the answer a pure function of (seed, identity, curve) and reproducible
    /// by hand from the three figures the report carries.
    ///
    /// WHY IDENTITY AND NOT POSITION. Keying on world position would make a settlement lose its railway when a road is
    /// nudged, because the position changes and the hash with it. Keying on the identity string means the settlement keeps
    /// its railway while it keeps its name and order, which is the stability an author editing a road expects.
    ///
    /// WHY THREE DRAWS FROM ONE STREAM AND NOT THREE STREAMS. Presence, spacing and the count's jitter are drawn in a
    /// fixed order from a single per-settlement stream. One stream is enough because the order is fixed in code and
    /// cannot vary with visitation; three streams would be three more identity strings to keep stable and would buy
    /// nothing. The order below must not be reshuffled -- doing so silently changes every existing city's
    /// railways. </summary>
    public static class SettlementRailPresence
    {
        /// <summary> The suffix mixed into the settlement's identity to make the rail stream. A named constant rather
        /// than a literal at the call site, because changing it re-rolls every railway in every city and that should be
        /// a visible edit to a documented constant, not a typo in a string. </summary>
        public const string StreamKey = "rail-presence";

        /// <summary> The smallest population the presence curve is asked about. Below this the answer is no railway
        /// without a draw. NOT a threshold on the curve -- the logistic is perfectly happy to return 0.0003 for a hamlet
        /// of forty -- but a guard on the LOGARITHM: log10 of zero is negative infinity and log10 of a negative number is
        /// NaN, and a curve fed NaN returns NaN, which compares false against everything and would read as "unserved for
        /// no stated reason". Forty is chosen because it is below every shipped band's minimum, so the guard never fires
        /// on a settlement the ladder actually produces. </summary>
        public const int MinimumPopulationForCurve = 40;

        /// <summary> B-27 / D-9. Which railway a presence answer is about: the SURFACE network, fitted from railway
        /// stations, or the UNDERGROUND network -- a metro -- fitted from subway stations. The two share every step of
        /// the draw and differ only in which row set they read, which provenance flag they report and which
        /// per-settlement stream they draw from, so they are one method with a mode rather than two that would drift
        /// apart the first time one was fixed. Surface is the default and the value zero, so every existing caller and
        /// every existing city's railways are unchanged. </summary>
        public enum RailMode { Surface = 0, Underground = 1 }

        /// <summary> The suffix mixed into a settlement's identity to make its UNDERGROUND stream, distinct from
        /// <see cref="StreamKey"/> so a settlement's metro draw and its surface-rail draw are independent -- a place can
        /// have one and not the other -- and neither re-rolls when the other's code changes. </summary>
        public const string UndergroundStreamKey = "underground-presence";

        /// <summary> Answers the SURFACE railway question for one settlement, or refuses. The mode-free overload every
        /// existing caller uses; it forwards to the moded one below as <see cref="RailMode.Surface"/> so there is one
        /// body, not two. </summary>
        public static SettlementRailReport Resolve(SettlementFitTable table, int orderIndex, string continentKey,
                                                  int population, string identity, ulong citySeed)
        {
            return Resolve(table, orderIndex, continentKey, population, identity, citySeed, RailMode.Surface);
        }

        /// <summary> Answers the railway question for one settlement, or refuses.
        ///
        /// <paramref name="identity"/> must be the settlement's stable identity -- <c>SettlementCandidate.Identity</c> --
        /// and not its display name, for the reason the class header gives. <paramref name="citySeed"/> is the city's
        /// seed, not a per-operator seed: railway presence is a property of the settlement and should not change because
        /// an operator was reordered in the stage list. </summary>
        public static SettlementRailReport Resolve(SettlementFitTable table, int orderIndex, string continentKey,
                                                  int population, string identity, ulong citySeed, RailMode mode)
        {
            bool underground = mode == RailMode.Underground;
            string noun = underground ? "an underground railway" : "a railway";
            string rowNoun = underground ? "metro" : "rail";

            SettlementRailReport report = new SettlementRailReport();
            report.orderIndex = orderIndex;
            report.continentKey = string.IsNullOrEmpty(continentKey) ? SettlementFitTable.WorldKey : continentKey;
            report.population = population;

            if (table == null)
            {
                report.refusal = "there is no settlement fit table, so nothing says whether a settlement of this size is"
                                 + " served by " + noun;
                return report;
            }
            if (string.IsNullOrEmpty(identity))
            {
                report.refusal = "this settlement has no identity string, and the " + rowNoun + " draw is keyed on it --"
                                 + " without one the answer would change every bake";
                return report;
            }

            SettlementRailFitRow row;
            bool usedWorldFallback;
            bool found = underground
                ? table.TryGetUnderground(orderIndex, report.continentKey, out row, out usedWorldFallback)
                : table.TryGetRail(orderIndex, report.continentKey, out row, out usedWorldFallback);
            if (!found)
            {
                report.refusal = "the fit table holds no " + rowNoun + " row for order " + orderIndex + " in "
                                 + report.continentKey + " and none for the world either, so the " + rowNoun
                                 + " question is unanswered rather than answered no";
                return report;
            }
            report.usedWorldFallback = usedWorldFallback;
            report.continentKey = usedWorldFallback ? SettlementFitTable.WorldKey : report.continentKey;
            report.fromFittedCurve = underground ? table.undergroundFittedFromRealDump : table.railFittedFromRealDump;

            string shapeReason;
            if (!row.IsShapeConsistent(out shapeReason))
            {
                report.refusal = "the " + rowNoun + " row is not shaped like a railway fit, so its curve is not worth"
                                 + " drawing from: " + shapeReason;
                return report;
            }

            if (population < MinimumPopulationForCurve)
            {
                report.resolved = true;
                report.served = false;
                report.presenceProbability = 0f;
                report.presenceDraw = 0f;
                return report;
            }

            string streamKey = underground ? UndergroundStreamKey : StreamKey;
            DeterministicRandom random = DeterministicRandom.ForElement(citySeed, identity + "|" + streamKey);

            report.presenceProbability = row.PresenceProbability(population);
            report.presenceDraw = (float)random.NextDouble();
            report.resolved = true;
            report.served = report.presenceDraw < report.presenceProbability;
            if (!report.served) { return report; }

            report.stationCount = row.StationCount(population);
            report.stationSpacingMetres = row.DrawSpacing(ref random);
            report.trackLengthMetres = (report.stationCount + 1) * report.stationSpacingMetres;
            return report;
        }

        /// <summary> Resolves every candidate in a list, appending one report per candidate in the same order, and
        /// returns how many came out served. Positionally paired with the input list rather than keyed, because the
        /// caller already holds the candidates and a dictionary keyed on identity would be a second place for the
        /// identity to be got wrong.
        ///
        /// A candidate that REFUSES still gets a report appended, so the two lists stay the same length and a refusal is
        /// visible against the settlement it belongs to instead of shortening the list and shifting every answer after it
        /// onto the wrong place. That includes a candidate the siting pass REJECTED: an unaccepted candidate is asked
        /// nothing and gets a refusal naming its own refusal, rather than being skipped, for the same length
        /// reason. </summary>
        public static int ResolveAll(SettlementFitTable table, IList<SettlementCandidate> candidates,
                                     string continentKey, ulong citySeed, IList<SettlementRailReport> reportsOut)
        {
            if (candidates == null || reportsOut == null) { return 0; }
            int served = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                SettlementCandidate candidate = candidates[i];
                if (!candidate.accepted)
                {
                    SettlementRailReport skipped = new SettlementRailReport();
                    skipped.orderIndex = candidate.orderIndex;
                    skipped.continentKey = string.IsNullOrEmpty(continentKey)
                        ? SettlementFitTable.WorldKey : continentKey;
                    skipped.population = candidate.proposedPopulation;
                    skipped.refusal = "this settlement was not accepted by the siting pass, so it has no railway to"
                                      + " answer for: " + (string.IsNullOrEmpty(candidate.refusal)
                                          ? "no reason given" : candidate.refusal);
                    reportsOut.Add(skipped);
                    continue;
                }
                SettlementRailReport report = Resolve(table, candidate.orderIndex, continentKey,
                                                     candidate.proposedPopulation, candidate.Identity, citySeed);
                reportsOut.Add(report);
                if (report.resolved && report.served) { served++; }
            }
            return served;
        }
    }
}

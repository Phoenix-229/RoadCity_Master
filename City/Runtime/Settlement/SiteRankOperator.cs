// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-4. One candidate's place in the order, with the arithmetic that put it there left in view.
    ///
    /// The ranking is the shipped cost model and nothing else: the five mean resistances the site test measured, weighted
    /// by the author's own <see cref="SuitabilityWeights"/> through <c>SuitabilityFactors.Resistance</c>, turned into a
    /// cost by <c>SuitabilityFactors.CellCost</c>. So a site's rank is expressed in the same unit a route's cost is --
    /// how many metres of easy ground a metre here is worth -- and the factor that bound it is named by
    /// <c>SuitabilityFactors.BindingFactor</c>, weighted, exactly as a corridor cell's is. </summary>
    public struct SiteRanking
    {
        /// <summary> The candidate, named as the site test named it. </summary>
        public string identity;

        /// <summary> The region, in world XZ metres. </summary>
        public Rect regionXZ;

        /// <summary> Its position in the order, 1 for the chosen site. Refused candidates are ranked 0: they are listed
        /// so an author can read why, not placed in a queue they were never in. </summary>
        public int rank;

        /// <summary> Whether this is the site the operator chose. </summary>
        public bool chosen;

        /// <summary> Whether the site test accepted this candidate as buildable at all. </summary>
        public bool buildable;

        /// <summary> The weighted resistance of this site's ground, 0..1, and the cost that follows from it -- 1 on the
        /// easiest ground a class can have, rising to 1 + <c>SuitabilityFactors.CostSpread</c> on the worst it tolerates.
        /// </summary>
        public float resistance;
        public float cost;

        /// <summary> How much buildable ground the site holds, and what share of it that is. The tie-breaker, in that
        /// order, and reported because two sites of equal cost are not equally useful. </summary>
        public float buildableAreaM2;
        public float buildableFraction;

        /// <summary> Which of the five factors weighs most on this site, and the sentence that says so with its own
        /// measurement in it. On a refused candidate the sentence is the site test's refusal instead, because the reason
        /// it is not in the order is more use than the reason it would have been second. </summary>
        public SuitabilityFactor binding;
        public string reason;
        /// <summary> Where this row's verdict sits in the set the site test produced, so the chosen candidate's whole
        /// verdict can be carried forward exactly rather than found again by name -- two road ends may honestly share a
        /// name, and a lookup by name would then place the city on the wrong one. </summary>
        public int sourceIndex;

        /// <summary> The seeded key that breaks a dead-equal tie, from <c>CitySeed.ForItem</c> keyed on the candidate's
        /// own name rather than on its position in the list. Keyed on the name because a tie broken by index would move
        /// the chosen city the moment the author reordered their roads, and the same world must rebuild identically.
        /// </summary>
        public int tieKey;

        /// <summary> One line for the inspector: where this site placed and what decided it. </summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append(rank > 0 ? rank.ToString(CultureInfo.InvariantCulture) + ". " : "-- ");
            text.Append(string.IsNullOrEmpty(identity) ? "site" : identity);
            if (chosen) { text.Append(" (chosen)"); }
            if (!buildable)
            {
                text.Append(": not a site. ").Append(reason);
                return text.ToString();
            }
            text.Append(": cost ").Append(cost.ToString("0.###", CultureInfo.InvariantCulture));
            text.Append(" from a weighted resistance of ").Append(resistance.ToString("0.###",
                CultureInfo.InvariantCulture));
            text.Append(", ").Append(buildableAreaM2.ToString("0", CultureInfo.InvariantCulture));
            text.Append(" m^2 buildable (").Append((buildableFraction * 100f).ToString("0.#",
                CultureInfo.InvariantCulture));
            text.Append(" % of the region). ").Append(reason);
            return text.ToString();
        }
    }
    /// <summary> B-15 / C-4. Which candidate the settlement is built on, and every candidate that was not, in order.
    ///
    /// The runner-ups are the point of returning a set rather than a <see cref="Rect"/>: an author who expected the city
    /// at the north road end and found it at the south one can read, in one line each, that the north end cost more and
    /// which factor made it cost more. A chooser that returned only its choice would leave that unanswerable. </summary>
    public sealed class SiteChoice
    {
        /// <summary> Whether any candidate could hold a settlement. </summary>
        public bool chosen;

        /// <summary> The chosen candidate: its name, its region in world XZ metres, and the site test's own verdict for
        /// it, carried whole so a later stage never has to re-measure ground that has already been measured. </summary>
        public string chosenIdentity = string.Empty;
        public Rect chosenRegionXZ;
        public SiteVerdict chosenVerdict;

        /// <summary> Every candidate, cheapest first, with the refused ones after them. </summary>
        public List<SiteRanking> rankings = new List<SiteRanking>();

        /// <summary> Why nothing was chosen, when nothing was: the site test's own refusal when it did not run, or the
        /// count of candidates that all failed. Empty on a choice. </summary>
        public string refusalReason = string.Empty;

        /// <summary> The weights the order was computed with, in the author's own words
        /// (<c>SuitabilityWeights.Line</c>), so a surprising order can be checked against the numbers that produced it.
        /// </summary>
        public string weightsLine = string.Empty;

        /// <summary> The one line a caller logs. </summary>
        public string Summary()
        {
            if (!chosen)
            {
                return "no candidate site can hold a settlement: " + refusalReason;
            }
            StringBuilder text = new StringBuilder();
            text.Append("settlement sited at ").Append(chosenIdentity).Append(", the cheapest of ");
            text.Append(rankings.Count.ToString(CultureInfo.InvariantCulture)).Append(" candidates on ");
            text.Append(weightsLine);
            return text.ToString();
        }

        /// <summary> The full order: the summary, then one line per candidate. </summary>
        public string Report()
        {
            StringBuilder text = new StringBuilder();
            text.Append(Summary());
            for (int i = 0; i < rankings.Count; i++)
            {
                text.Append('\n').Append("  ").Append(rankings[i].Describe());
            }
            return text.ToString();
        }
    }
    /// <summary> B-15 / C-4. Chooses which candidate the settlement is built on, out of the verdicts the site test
    /// produced.
    ///
    /// It measures nothing itself -- that is the division of labour: <c>SiteTestOperator</c> reads the ground once and
    /// this weighs what it read. Everything it weighs with is authored: the five <see cref="SuitabilityWeights"/> on this
    /// asset, applied through the shipped <c>SuitabilityFactors.Resistance</c> and <c>CellCost</c>, so the order a site
    /// lands in is the same arithmetic that priced the roads leading to it.
    ///
    /// Ties are real and are broken in a stated order: equal cost goes to the site with more buildable land, and a site
    /// equal in both is settled by <c>CitySeed.ForItem</c> keyed on the candidate's NAME. A coin toss keyed on the name
    /// rebuilds the same world every time and does not move the city when the author reorders their roads, which a tie
    /// broken by list position would. </summary>
    [CreateAssetMenu(fileName = "SiteRankOperator", menuName = "Road City Master/Operators/Site Rank", order = 13)]
    public sealed class SiteRankOperator : CityOperator<SiteVerdictSet, SiteChoice>
    {
        /// <summary> The author's weights for the five factors. Serialised here rather than read from the corridor stage
        /// because siting a settlement and routing a road are allowed to value the same ground differently -- a town will
        /// accept earthwork a lane would detour around -- and the defaults are the authored ones (C-15). </summary>
        [SerializeField] private SuitabilityWeights weights = new SuitabilityWeights();

        /// <summary> How close two costs must be to count as equal: one part in a thousand of a cost that spans 1 to
        /// 1 + <c>SuitabilityFactors.CostSpread</c>. Below that the difference is float arithmetic, not ground. </summary>
        private const float TieEpsilon = 0.001f;

        /// <summary> How close two buildable areas must be to count as equal, in square metres. </summary>
        private const float AreaTieEpsilonM2 = 1f;

        /// <summary> Weighs every verdict, orders them, and names the winner. </summary>
        public override SiteChoice Execute(SiteVerdictSet input, int operatorSeed)
        {
            SiteChoice choice = new SiteChoice();
            if (input == null)
            {
                choice.refusalReason = "no site verdicts were given to the site ranking";
                return choice;
            }
            if (!input.ran)
            {
                choice.refusalReason = "the site test did not run, so there is nothing to rank: " + input.refusalReason;
                return choice;
            }
            if (weights == null || !weights.IsUsable)
            {
                choice.refusalReason = "the ranking weights on this operator sum to nothing, so no site can be preferred"
                                       + " to another. Author them, or leave the defaults";
                return choice;
            }
            choice.weightsLine = weights.Line();
            for (int i = 0; i < input.verdicts.Count; i++)
            {
                choice.rankings.Add(Score(input.verdicts[i], i, operatorSeed));
            }
            choice.rankings.Sort(Compare);

            int placed = 0;
            for (int i = 0; i < choice.rankings.Count; i++)
            {
                SiteRanking row = choice.rankings[i];
                if (row.buildable)
                {
                    placed++;
                    row.rank = placed;
                    row.chosen = placed == 1;
                    if (row.chosen)
                    {
                        choice.chosen = true;
                        choice.chosenIdentity = row.identity;
                        choice.chosenRegionXZ = row.regionXZ;
                        choice.chosenVerdict = input.verdicts[row.sourceIndex];
                    }
                }
                choice.rankings[i] = row;
            }
            if (!choice.chosen)
            {
                choice.refusalReason = input.verdicts.Count.ToString(CultureInfo.InvariantCulture)
                                       + " candidate regions were judged and none of them can hold a settlement. Each"
                                       + " one's own reason is in the order below";
            }
            return choice;
        }
        /// <summary> Weighs one verdict into a row. Nothing is measured here: every number comes from the verdict the site
        /// test produced, so the ranking cannot disagree with the test about the ground.
        ///
        /// <c>CellCost</c> is asked for a passable cost (<c>forbidden</c> false) because a candidate whose cells are all
        /// impassable has no buildable cells and was refused by the test before it reached this operator.
        ///
        /// The binding factor can only land on slope, earthwork or network distance, and each of those three sentences is
        /// carrying a real measurement from the verdict. Water and land use cannot win it: both resistances are zero for
        /// the reasons the verdict states, and <c>BindingFactor</c> never returns a factor that contributes nothing.
        /// </summary>
        private SiteRanking Score(SiteVerdict verdict, int sourceIndex, int operatorSeed)
        {
            SiteRanking row = new SiteRanking();
            row.identity = verdict.identity;
            row.regionXZ = verdict.regionXZ;
            row.sourceIndex = sourceIndex;
            row.buildable = verdict.accepted;
            row.buildableAreaM2 = verdict.buildableAreaM2;
            row.buildableFraction = verdict.buildableFraction;
            string name = string.IsNullOrEmpty(verdict.identity) ? "candidate" : verdict.identity;
            row.tieKey = CitySeed.ForItem(operatorSeed,
                (int)(DeterministicHash.Fnv1a64(name) & 0x7FFFFFFFul));

            double resistance = SuitabilityFactors.Resistance(verdict.meanSlopeResistance,
                verdict.meanEarthworkResistance, verdict.meanWaterResistance, verdict.meanLandUseResistance,
                verdict.meanNetworkResistance, weights);
            row.resistance = (float)resistance;
            row.cost = (float)SuitabilityFactors.CellCost(resistance, false);
            row.binding = SuitabilityFactors.BindingFactor(verdict.meanSlopeResistance, verdict.meanEarthworkResistance,
                verdict.meanWaterResistance, verdict.meanLandUseResistance, verdict.meanNetworkResistance, weights);
            if (!row.buildable)
            {
                row.reason = verdict.refusal;
                return row;
            }
            row.reason = SuitabilityFactors.BindingSentence(row.binding, false, verdict.meanSlopeFraction,
                verdict.governingSlopeFraction, verdict.meanEarthworkM3PerMetre, 0.0, 0.0,
                float.IsNaN(verdict.metresToNearestRoad) ? 0.0 : verdict.metresToNearestRoad);
            return row;
        }
        /// <summary> The order, stated once: buildable candidates first, then cheapest, then the one holding more
        /// buildable land, then a seeded coin toss, then the name. It is a TOTAL order on purpose --
        /// <c>List&lt;T&gt;.Sort</c> is not a stable sort, so any pair it leaves undecided is a pair whose order the
        /// runtime is free to swap between two builds of the same world, and C-06 forbids that.
        ///
        /// The two epsilons are applied by quantising rather than by comparing a difference. A comparator that answers
        /// "equal" whenever |a-b| &lt; eps is not transitive -- a can tie b and b tie c while a beats c -- and .NET is
        /// entitled to throw on an inconsistent comparer; bucketing each value once is the same intent expressed
        /// transitively. It also settles a cost that arrived as NaN, which a refused candidate's can (its means divide by
        /// a readable-cell count of zero): every NaN lands in one bucket instead of comparing false against everything.
        /// </summary>
        private static int Compare(SiteRanking a, SiteRanking b)
        {
            if (a.buildable != b.buildable)
            {
                return a.buildable ? -1 : 1;
            }
            long costA = (long)(a.cost / TieEpsilon);
            long costB = (long)(b.cost / TieEpsilon);
            if (costA != costB)
            {
                return costA < costB ? -1 : 1;
            }
            long areaA = (long)(a.buildableAreaM2 / AreaTieEpsilonM2);
            long areaB = (long)(b.buildableAreaM2 / AreaTieEpsilonM2);
            if (areaA != areaB)
            {
                return areaA > areaB ? -1 : 1;   // more buildable land wins a tie on cost
            }
            if (a.tieKey != b.tieKey)
            {
                return a.tieKey < b.tieKey ? -1 : 1;
            }
            return string.CompareOrdinal(a.identity, b.identity);
        }
    }
}

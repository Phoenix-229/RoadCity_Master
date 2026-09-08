// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-11. The land-use COMPOSITION of a settlement of one order: what fraction of its developed
    /// ground is residential, general commercial, market, and industrial. A fitted row exactly as
    /// <see cref="SettlementRailFitRow"/> is, and a SEPARATE list on <see cref="SettlementFitTable"/> for the reason the
    /// rail and metro rows are separate lists -- it is fitted from a different measurement of the same settlements (the
    /// breakdown of their built area by use), so it lives beside them, not as four more fields on the population row.
    ///
    /// WHY A COMPOSITION AND NOT A LOGISTIC. Rail asks a yes/no of each place, so its fit is a logistic and its answer a
    /// seeded draw. Land use asks "of the ground this place develops, how much is each use" -- a set of fractions that
    /// sum to one, whose fit is the MEAN composition of the places of this order. There is no coin to toss: the shares
    /// are read, not drawn, so the same order gives the same shares every bake with no seed at all (C-06 holds without
    /// a stream). The task's *"real land-use shares by settlement order come from the fit, not from a typed table"* is
    /// exactly this row: the shares are DATA fitted per order, not a switch statement returning a constant.
    ///
    /// WHY MARKET IS ITS OWN SHARE. A market is commercial land, but the task distinguishes it from general commercial
    /// (a large open surface, not a taller building), so it is carried as its own fraction rather than folded into
    /// commercial. A caller that wants all commercial ground reads <see cref="SettlementLandUse.LandUseShares"/>'s
    /// combined share; a caller that wants to place the market as an open ground reads the market share alone. </summary>
    [Serializable]
    public sealed class SettlementLandUseFitRow
    {
        /// <summary> Which order this row describes, matching <see cref="SettlementOrderClass.orderIndex"/>: 1 capital
        /// through 4 village. </summary>
        public int orderIndex = 1;

        /// <summary> Which continent bucket, or <see cref="SettlementFitTable.WorldKey"/> for the whole dump. Same keys
        /// as the population and rail rows, so a project filtering one bucket gets its land-use fit or the world's. </summary>
        public string continentKey = SettlementFitTable.WorldKey;

        /// <summary> How many SETTLEMENTS this composition was averaged over -- the sample, not an area. Zero in the
        /// authored placeholder, exactly as the authored rail row carries zero. </summary>
        public int recordCount;

        // ------------------------------------------------------------------ the composition: FITTED mean shares
        // Fractions of DEVELOPED ground. They sum to one across these four uses; informal settlement and port/logistics
        // are NOT in the composition, because where they land is decided by suitability (a water edge, steep ground),
        // not by an order's share -- folding them in would claim a share the fit did not measure.

        /// <summary> Share of developed ground that is residential. The largest share in every real settlement, and
        /// larger the smaller the place. </summary>
        public float residentialShare = 0.65f;

        /// <summary> Share that is GENERAL commercial (shopfronts, offices) -- market ground excluded, carried below. </summary>
        public float commercialShare = 0.14f;

        /// <summary> Share that is market / open trading ground -- the use D-11 distinguishes from general commercial.
        /// Small everywhere, because a market is a focused open ground rather than a spread of buildings. </summary>
        public float marketShare = 0.04f;

        /// <summary> Share that is industrial (sheds, yards, logistics floor). Larger in the higher orders, which is
        /// where industry concentrates. </summary>
        public float industrialShare = 0.17f;

        /// <summary> The least sample a real fit needs before it is more than an anecdote -- the same figure the
        /// population row uses, so "usable" means the same thing across the fit. </summary>
        public const int MinimumRecordCount = 20;

        /// <summary> How far the four shares may sum from one and still count as a composition -- a small tolerance for
        /// rounding in a fitted mean. Outside it the row is refused rather than silently rescaled to hide a bad fit. </summary>
        public const float ShareSumTolerance = 0.02f;

        /// <summary> The four shares added up -- one in a good composition, off by rounding at most. </summary>
        public float ShareSum()
        {
            return residentialShare + commercialShare + marketShare + industrialShare;
        }

        /// <summary> Whether this row is a USABLE real fit: fitted from at least <see cref="MinimumRecordCount"/> places
        /// and shape-consistent. An authored placeholder is shape-consistent but not usable, and says which. </summary>
        public bool IsUsable(out string reason)
        {
            if (!IsShapeConsistent(out reason)) { return false; }
            if (recordCount < MinimumRecordCount)
            {
                reason = "fitted from " + recordCount + " settlements, fewer than the " + MinimumRecordCount
                         + " a composition needs to be more than an anecdote";
                return false;
            }
            reason = "";
            return true;
        }

        /// <summary> Whether the numbers are a composition at all: every share in [0,1] and the four summing to one
        /// within <see cref="ShareSumTolerance"/>. A negative share, a share above one, or a sum that is not near one is
        /// not a land-use breakdown and is refused rather than used. </summary>
        public bool IsShapeConsistent(out string reason)
        {
            if (!InUnit(residentialShare) || !InUnit(commercialShare)
                || !InUnit(marketShare) || !InUnit(industrialShare))
            {
                reason = "a land-use share is outside [0,1], so this is not a composition";
                return false;
            }
            float sum = ShareSum();
            if (Math.Abs(sum - 1f) > ShareSumTolerance)
            {
                reason = "the four shares sum to " + sum.ToString("0.###", CultureInfo.InvariantCulture)
                         + ", not one, so they are not a composition of developed ground";
                return false;
            }
            reason = "";
            return true;
        }

        private static bool InUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        /// <summary> The composition in one line, as percentages, for an author to read the fit back and disagree. </summary>
        public string Describe()
        {
            return "order " + orderIndex + " land use: residential " + Pct(residentialShare) + ", commercial "
                   + Pct(commercialShare) + ", market " + Pct(marketShare) + ", industrial " + Pct(industrialShare);
        }

        private static string Pct(float share)
        {
            return (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }


    /// <summary> B-27 / D-11. Turns the fitted land-use row for a settlement's order into the shares a placer uses. A
    /// static resolver on the folder's doctrine: it reads the fit and refuses, it never mutates a scene, samples the
    /// terrain, or draws a seed -- the shares are a composition, read the same every bake. It does NOT place anything:
    /// the zoning pass B-15 already ships (<see cref="ZonePaint"/>, <see cref="ZoneCatalog"/>) paints the zones; this
    /// answers only "how much of each", which the task says must come from the fit and not a typed table. </summary>
    public static class SettlementLandUse
    {
        /// <summary> THE ANSWER: the four land-use shares for one settlement, normalised to sum to exactly one, beside
        /// whether the fit they came from was real and whether the world bucket stood in for a missing continent one.
        /// A refusal string, not a bool, on the folder's doctrine -- a missing row and an inconsistent row are different
        /// faults with different fixes. </summary>
        public struct LandUseShares
        {
            public bool resolved;
            public string refusal;
            public int orderIndex;
            public float residential;
            public float commercial;
            public float market;
            public float industrial;

            /// <summary> Whether the row came from a real dump. False for the shipped placeholder -- surfaced, never
            /// hidden, so a report reads honestly. </summary>
            public bool fromRealDump;

            /// <summary> Whether the world bucket answered for a continent bucket that had no row. </summary>
            public bool usedWorldFallback;

            /// <summary> All commercial ground: general commercial plus market, since a market IS commercial land. A
            /// caller that does not care about the market distinction reads this; one that places the market as an open
            /// ground reads <see cref="market"/> alone. </summary>
            public float AllCommercial { get { return commercial + market; } }

            public string Describe()
            {
                if (!resolved)
                {
                    return "land use: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
                }
                string origin = fromRealDump ? "fitted" : "authored placeholder";
                string fallback = usedWorldFallback ? ", world bucket" : "";
                return "order " + orderIndex + " land use (" + origin + fallback + "): residential " + Pct(residential)
                       + ", commercial " + Pct(commercial) + ", market " + Pct(market) + ", industrial "
                       + Pct(industrial);
            }

            private static string Pct(float share)
            {
                return (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            }
        }

        /// <summary> Resolve the land-use shares for one order in one continent bucket. Reads the row through the table's
        /// world-fallback lookup, refuses when the table holds no land-use row or the row is not a composition, and
        /// otherwise returns the four shares NORMALISED to sum to exactly one -- a fitted mean can be off by rounding,
        /// and normalising here means every caller gets shares that add up rather than each renormalising its own way. </summary>
        public static LandUseShares Resolve(SettlementFitTable table, int orderIndex, string continentKey)
        {
            LandUseShares shares = new LandUseShares();
            shares.orderIndex = orderIndex;

            if (table == null)
            {
                shares.refusal = "no fit table was supplied, so land-use shares cannot be read";
                return shares;
            }

            SettlementLandUseFitRow row;
            bool usedWorldFallback;
            if (!table.TryGetLandUse(orderIndex, continentKey, out row, out usedWorldFallback) || row == null)
            {
                shares.refusal = "the fit table holds no land-use row for order " + orderIndex
                                 + " in bucket '" + (string.IsNullOrEmpty(continentKey) ? "(none)" : continentKey) + "'";
                return shares;
            }

            string reason;
            if (!row.IsShapeConsistent(out reason))
            {
                shares.refusal = "the land-use row is not a composition: " + reason;
                return shares;
            }

            float sum = row.ShareSum();
            if (sum <= 0f)
            {
                shares.refusal = "the land-use shares sum to zero, so there is no composition to normalise";
                return shares;
            }

            shares.resolved = true;
            shares.residential = row.residentialShare / sum;
            shares.commercial = row.commercialShare / sum;
            shares.market = row.marketShare / sum;
            shares.industrial = row.industrialShare / sum;
            shares.fromRealDump = table.landUseFittedFromRealDump;
            shares.usedWorldFallback = usedWorldFallback;
            return shares;
        }
    }
}

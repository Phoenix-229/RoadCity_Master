// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> D-15. The authored per-chunk instance ceiling for each content family. The streaming scheduler's
    /// per-frame budget controls when work is done; this table controls whether a single chunk contains more instances
    /// than the mobile target can reasonably carry. A count over its family ceiling is refused, never silently trimmed.
    /// The figures are authored budget limits, not physical measurements, and are exposed here so the gate and a loader
    /// read the same table. </summary>
    public struct CityInstanceBudgetRule
    {
        public string family;
        public int maximumInstancesPerChunk;

        public CityInstanceBudgetRule(string family, int maximumInstancesPerChunk)
        {
            this.family = family;
            this.maximumInstancesPerChunk = maximumInstancesPerChunk;
        }
    }


    /// <summary> The one source of D-15's family ceilings. Values are deliberately conservative authored limits for
    /// the Helio G99 / Mali-G57 target in the module brief: repeated small street objects have a larger allowance than
    /// large civic or power assets, and a family with no row cannot pass validation. </summary>
    public static class CityInstanceBudgets
    {
        private static readonly CityInstanceBudgetRule[] Rules =
        {
            new CityInstanceBudgetRule("Street", 512),
            new CityInstanceBudgetRule("Street furniture", 256),
            new CityInstanceBudgetRule("Power", 64),
            new CityInstanceBudgetRule("Water and waste", 64),
            new CityInstanceBudgetRule("Telecoms and gas", 64),
            new CityInstanceBudgetRule("Civic", 48),
            new CityInstanceBudgetRule("Transport", 96),
            new CityInstanceBudgetRule("Freight", 96),
            new CityInstanceBudgetRule("Sport and leisure", 96),
            new CityInstanceBudgetRule("Village-scale agriculture", 96),
            new CityInstanceBudgetRule("Earthworks and crossings", 128)
        };

        public static IReadOnlyList<CityInstanceBudgetRule> All
        {
            get { return Rules; }
        }

        /// <summary> Finds a family ceiling by its stable display name. </summary>
        public static bool TryGet(string family, out CityInstanceBudgetRule rule)
        {
            for (int i = 0; i < Rules.Length; i++)
            {
                if (string.Equals(Rules[i].family, family, StringComparison.Ordinal))
                {
                    rule = Rules[i];
                    return true;
                }
            }
            rule = new CityInstanceBudgetRule();
            return false;
        }

        /// <summary> Refuses an overfull chunk with the family and both figures in the sentence. </summary>
        public static bool TryValidate(string family, int instanceCount, out string refusal)
        {
            refusal = string.Empty;
            CityInstanceBudgetRule rule;
            if (!TryGet(family, out rule))
            {
                refusal = "no per-chunk instance ceiling is declared for the " + family + " family";
                return false;
            }
            if (instanceCount < 0)
            {
                refusal = family + " has a negative instance count ("
                    + instanceCount.ToString(CultureInfo.InvariantCulture) + ")";
                return false;
            }
            if (instanceCount > rule.maximumInstancesPerChunk)
            {
                refusal = family + " has " + instanceCount.ToString(CultureInfo.InvariantCulture)
                    + " instances in one chunk, above its authored ceiling of "
                    + rule.maximumInstancesPerChunk.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            return true;
        }
    }
}

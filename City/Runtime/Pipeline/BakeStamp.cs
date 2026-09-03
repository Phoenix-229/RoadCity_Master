#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What a re-bake staleness check concluded about a baked city -- never an action, only a
    /// verdict, because RM-20 forbids an implicit re-bake: the tool warns and OFFERS, the author decides. </summary>
    public enum BakeStaleness
    {
        /// <summary> The stamp matches the current generator and schema -- nothing to warn about. </summary>
        UpToDate = 0,

        /// <summary> The city was baked by an OLDER generator version. A re-bake would change it; warn,
        /// name both versions, offer a re-bake, and perform none. </summary>
        StaleGenerator = 1,

        /// <summary> The city was baked by a NEWER generator than this code -- opened with an older tool.
        /// Do not touch it silently; the author must update the tool or accept the risk. </summary>
        NewerGenerator = 2,

        /// <summary> The saved data shape predates the current schema; a loader must migrate or refuse
        /// (RM-08's <see cref="CityData.IsCurrentSchema"/>). </summary>
        StaleSchema = 3
    }


    /// <summary> RM-20's audit stamp written INTO the baked output, so a baked city is auditable a year
    /// later: what seed produced it, which generator and schema version, when (informational only), and
    /// which source asset. This is the exact shape the spec fixes.
    ///
    /// The one determinism rule this type must not break (C-06): <see cref="bakedAtUtc"/> is INFORMATIONAL
    /// and must never feed a generation decision or a staleness verdict. <see cref="Evaluate"/> therefore
    /// ignores it, and the gate proves two stamps differing only in their timestamp evaluate identically. </summary>
    [Serializable]
    public struct BakeStamp
    {
        /// <summary> The generator input the city was baked from. </summary>
        public int seed;

        /// <summary> Semantic version of the generation code that produced the bake. </summary>
        public string generatorVersion;

        /// <summary> RM-08's data-shape version at bake time. </summary>
        public int schemaVersion;

        /// <summary> ISO-8601 UTC time of the bake -- INFORMATIONAL ONLY. Never an input to anything,
        /// or determinism is gone. It answers "does this baked content predate a change I remember?". </summary>
        public string bakedAtUtc;

        /// <summary> The GUID of the source <see cref="CityData"/> asset the bake came from. </summary>
        public string sourceCityAssetGuid;

        /// <summary> Build a stamp from the source city and the current code versions. The caller supplies
        /// <paramref name="bakedAtUtc"/> (the editor reads the clock; passing it in keeps this type free of
        /// <c>DateTime.Now</c> -- determinism-exempt: named in prose to explain its absence, never called here --
        /// which the determinism smell-test bans from the generation assembly). </summary>
        public static BakeStamp Create(CityData source, string currentGeneratorVersion, string sourceGuid,
            string bakedAtUtc)
        {
            BakeStamp s;
            s.seed = source != null ? source.seed : 0;
            s.generatorVersion = currentGeneratorVersion ?? "";
            s.schemaVersion = source != null ? source.schemaVersion : CityData.CurrentSchemaVersion;
            s.bakedAtUtc = bakedAtUtc ?? "";
            s.sourceCityAssetGuid = sourceGuid ?? "";
            return s;
        }

        /// <summary> Decide, WITHOUT changing anything, whether this stamp is stale against the current
        /// generator version and schema. The result is a verdict the tool acts on by warning and offering a
        /// re-bake -- never by baking. Deterministic and independent of <see cref="bakedAtUtc"/>. </summary>
        public BakeStaleness Evaluate(string currentGeneratorVersion, int currentSchemaVersion)
        {
            if (schemaVersion != currentSchemaVersion)
            {
                return BakeStaleness.StaleSchema;
            }
            int cmp = CompareSemanticVersions(generatorVersion, currentGeneratorVersion);
            if (cmp < 0) { return BakeStaleness.StaleGenerator; }
            if (cmp > 0) { return BakeStaleness.NewerGenerator; }
            return BakeStaleness.UpToDate;
        }

        /// <summary> A deterministic dotted-numeric semantic-version compare: -1 if <paramref name="a"/> is
        /// older, 1 if newer, 0 if equal. Each dot-separated field is compared numerically when both parse,
        /// else by ordinal string order; a missing field counts as 0 so "1.2" and "1.2.0" are equal. No
        /// culture, no <c>string.GetHashCode</c> -- both would break C-06 across machines/sessions. </summary>
        public static int CompareSemanticVersions(string a, string b)
        {
            string[] pa = (a ?? "").Split('.');
            string[] pb = (b ?? "").Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                string fa = i < pa.Length ? pa[i].Trim() : "0";
                string fb = i < pb.Length ? pb[i].Trim() : "0";
                int ia, ib;
                bool na = int.TryParse(fa, out ia);
                bool nb = int.TryParse(fb, out ib);
                if (na && nb)
                {
                    if (ia != ib) { return ia < ib ? -1 : 1; }
                }
                else
                {
                    int c = string.CompareOrdinal(fa, fb);
                    if (c != 0) { return c < 0 ? -1 : 1; }
                }
            }
            return 0;
        }
    }
}

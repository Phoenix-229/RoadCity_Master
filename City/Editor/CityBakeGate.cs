#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-20's gate: proves the parts of THE BAKE that carry the bugs and can be checked without a
    /// scene. The bake wires scene objects, terrain conform and scatter -- the author confirms those in the
    /// editor -- but three rules decide whether a bake is correct, and all three are pure: the audit
    /// <see cref="BakeStamp"/> is written and its staleness verdict is deterministic and NEVER an implicit
    /// re-bake (§ 10.3), the five stages run in the ONE canonical order (RM-70 asserts the sequence rather
    /// than trusting it), and GENERATION NEVER RUNS AT PLAY TIME -- refused and recorded by
    /// <see cref="CityGenerationGuard"/> so the gate's "no generator ran" half is proven, not assumed. The
    /// runtime output FORM is Q-6, resolved AUTHORED as a prefab-instance spawn table (see
    /// <see cref="CityBakePipeline"/>). Report lines are prefixed RCMBAKE. </summary>
    public static class CityBakeGate
    {
        [MenuItem("Window/Road City Master/Testing/City bake stamp and ordering")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityBakeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMBAKE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMBAKE BEGIN\n");
            List<string> failures = new List<string>();

            CaseStampFields(builder, failures);
            CaseTimestampDoesNotAffectVerdict(builder, failures);
            CaseStaleness(builder, failures);
            CaseSemanticCompare(builder, failures);
            CaseBakeOrder(builder, failures);
            CasePlayModeGuard(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMBAKE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMBAKE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMBAKE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The stamp records the source's seed and schema and the caller's version/guid/time. </summary>
        private static void CaseStampFields(StringBuilder builder, List<string> failures)
        {
            CityData city = ScriptableObject.CreateInstance<CityData>();
            city.seed = 4242;
            BakeStamp s = BakeStamp.Create(city, "1.4.0", "abc123guid", "2026-08-29T12:00:00Z");
            Expect(s.seed == 4242, "stamp: seed not copied from the source city", failures);
            Expect(s.schemaVersion == city.schemaVersion, "stamp: schema version not copied", failures);
            Expect(s.generatorVersion == "1.4.0", "stamp: generator version not recorded", failures);
            Expect(s.sourceCityAssetGuid == "abc123guid", "stamp: source asset guid not recorded", failures);
            Expect(s.bakedAtUtc == "2026-08-29T12:00:00Z", "stamp: bakedAtUtc not recorded", failures);
            builder.Append("RCMBAKE ").Append("stamp-fields".PadRight(22))
                   .Append("seed ").Append(s.seed).Append(", gen ").Append(s.generatorVersion)
                   .Append(", schema ").Append(s.schemaVersion).Append("\n");
        }

        /// <summary> C-06: the informational timestamp must never change a verdict. Two stamps that differ
        /// only in <c>bakedAtUtc</c> evaluate identically. </summary>
        private static void CaseTimestampDoesNotAffectVerdict(StringBuilder builder, List<string> failures)
        {
            CityData city = ScriptableObject.CreateInstance<CityData>();
            BakeStamp early = BakeStamp.Create(city, "1.4.0", "g", "2026-01-01T00:00:00Z");
            BakeStamp late = BakeStamp.Create(city, "1.4.0", "g", "2026-12-31T23:59:59Z");
            BakeStaleness a = early.Evaluate("1.4.0", city.schemaVersion);
            BakeStaleness b = late.Evaluate("1.4.0", city.schemaVersion);
            Expect(a == b, "timestamp: two stamps differing only in bakedAtUtc gave different verdicts ("
                + a + " vs " + b + ")", failures);
            builder.Append("RCMBAKE ").Append("timestamp-inert".PadRight(22))
                   .Append("verdict ").Append(a).Append(" for both times\n");
        }

        /// <summary> The staleness verdict by each rule; schema mismatch takes precedence, and no case
        /// performs a bake -- <see cref="BakeStamp.Evaluate"/> only returns a verdict. </summary>
        private static void CaseStaleness(StringBuilder builder, List<string> failures)
        {
            CityData city = ScriptableObject.CreateInstance<CityData>();
            int schema = city.schemaVersion;
            BakeStamp s = BakeStamp.Create(city, "1.4.0", "g", "t");

            Expect(s.Evaluate("1.4.0", schema) == BakeStaleness.UpToDate,
                "stale: equal version + schema was not UpToDate", failures);
            Expect(s.Evaluate("1.5.0", schema) == BakeStaleness.StaleGenerator,
                "stale: an older baked generator was not StaleGenerator", failures);
            Expect(s.Evaluate("1.3.0", schema) == BakeStaleness.NewerGenerator,
                "stale: a newer baked generator was not NewerGenerator", failures);
            Expect(s.Evaluate("1.4.0", schema + 1) == BakeStaleness.StaleSchema,
                "stale: a schema mismatch did not take precedence", failures);
            builder.Append("RCMBAKE ").Append("staleness".PadRight(22))
                   .Append("UpToDate / StaleGenerator / NewerGenerator / StaleSchema all correct\n");
        }

        /// <summary> The version compare is numeric per field, not lexical: 1.10 is newer than 1.9, and
        /// 1.2 equals 1.2.0. </summary>
        private static void CaseSemanticCompare(StringBuilder builder, List<string> failures)
        {
            Expect(BakeStamp.CompareSemanticVersions("1.2", "1.2.0") == 0,
                "semver: 1.2 and 1.2.0 were not treated as equal", failures);
            Expect(BakeStamp.CompareSemanticVersions("1.10", "1.9") > 0,
                "semver: 1.10 was not treated as newer than 1.9 (lexical compare leaked in)", failures);
            Expect(BakeStamp.CompareSemanticVersions("2.0", "1.9") > 0,
                "semver: 2.0 was not newer than 1.9", failures);
            Expect(BakeStamp.CompareSemanticVersions("1.0", "1.0.1") < 0,
                "semver: 1.0 was not older than 1.0.1", failures);
            builder.Append("RCMBAKE ").Append("semver".PadRight(22))
                   .Append("numeric per-field compare correct (1.10 > 1.9)\n");
        }

        /// <summary> The stage-ordering contract: the canonical run is valid, running scatter before conform
        /// is caught, skipping the (blocked) footprint/conform/scatter stages is allowed, and repeating a
        /// stage is caught. </summary>
        private static void CaseBakeOrder(StringBuilder builder, List<string> failures)
        {
            string reason;
            Expect(CityBakePipeline.IsInCanonicalOrder(new List<BakeStage>(CityBakePipeline.CanonicalOrder),
                out reason), "order: the canonical order was rejected (" + reason + ")", failures);

            List<BakeStage> wrong = new List<BakeStage>
            {
                BakeStage.LoadOrGenerate, BakeStage.BakeScatter, BakeStage.ConformTerrain
            };
            Expect(!CityBakePipeline.IsInCanonicalOrder(wrong, out reason),
                "order: scatter-before-conform was not caught", failures);

            // With RM-14/RM-15 blocked, a bake skips footprints/conform/scatter -- still valid.
            List<BakeStage> skipped = new List<BakeStage>
            {
                BakeStage.LoadOrGenerate, BakeStage.ProduceRuntime
            };
            Expect(CityBakePipeline.IsInCanonicalOrder(skipped, out reason),
                "order: skipping blocked stages was wrongly rejected (" + reason + ")", failures);

            List<BakeStage> repeated = new List<BakeStage>
            {
                BakeStage.LoadOrGenerate, BakeStage.LoadOrGenerate, BakeStage.ProduceRuntime
            };
            Expect(!CityBakePipeline.IsInCanonicalOrder(repeated, out reason),
                "order: a repeated stage was not caught", failures);

            builder.Append("RCMBAKE ").Append("stage-order".PadRight(22))
                   .Append("canonical ok; out-of-order & repeat caught; skip allowed\n");
        }

        /// <summary> § 10.3's runtime half: the generation guard is open in edit mode and CLOSED in play
        /// mode, where any generation attempt is refused and recorded so the self-test can assert none ran. </summary>
        private static void CasePlayModeGuard(StringBuilder builder, List<string> failures)
        {
            CityGenerationGuard.EnterEditMode();
            Expect(CityGenerationGuard.RequireGenerationAllowed("bake"),
                "guard: generation was refused in edit mode", failures);

            CityGenerationGuard.EnterPlayMode();
            bool allowedInPlay = CityGenerationGuard.RequireGenerationAllowed("StreetNetworkGenerator.Generate");
            Expect(!allowedInPlay, "guard: generation was ALLOWED at play time (the § 10.3 failure)", failures);
            Expect(CityGenerationGuard.BlockedAttempts == 1,
                "guard: the blocked attempt was not recorded (count "
                + CityGenerationGuard.BlockedAttempts + ")", failures);
            Expect(CityGenerationGuard.LastBlockedContext == "StreetNetworkGenerator.Generate",
                "guard: the blocked context was not recorded", failures);

            CityGenerationGuard.EnterEditMode(); // leave the gate open for the editor
            builder.Append("RCMBAKE ").Append("playmode-guard".PadRight(22))
                   .Append("open in edit, closed in play; 1 attempt refused + recorded\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }


    }
}
#endif

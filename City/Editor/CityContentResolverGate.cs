#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-55's gate: proves the city layer has ONE fail-closed content-resolution path and no escape
    /// hatch. It checks that an unregistered category REFUSES (never a grey box), that the three refusal reasons
    /// are told apart (empty / no-variant / present-but-failed-validation), that a resolve is EITHER an asset OR a
    /// refusal and never a third thing, that selection within a category is a deterministic seeded draw keyed on
    /// the requester (so it reproduces and ignores registration order), and that refusals AGGREGATE into one entry
    /// per (category, reason) with a count and a first requester. Finally it SCANS the City/Runtime source and
    /// asserts there is no <c>GameObject.CreatePrimitive</c>, no fallback/placeholder prefab field, and no
    /// "nearest category" heuristic anywhere in the city layer -- the thing RM-55 forbids. Report lines prefixed
    /// RCMRESOLVE. </summary>
    public static class CityContentResolverGate
    {
        [MenuItem("Window/Road City Master/Testing/City content resolver (fail-closed)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityContentResolverGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMRESOLVE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMRESOLVE BEGIN\n");
            List<string> failures = new List<string>();

            CaseEmptyCategoryRefuses(builder, failures);
            CaseReasonsDistinguished(builder, failures);
            CaseNoThirdPath(builder, failures);
            CaseResolvesToRegisteredAsset(builder, failures);
            CaseDeterministicAndOrderIndependent(builder, failures);
            CaseAggregation(builder, failures);
            CaseNoFallbackInCityLayer(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMRESOLVE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMRESOLVE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMRESOLVE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Build a candidate quickly. </summary>
        private static ContentCandidate Cand(string id, string category, bool validated, params string[] tags)
        {
            ContentCandidate c = new ContentCandidate();
            c.assetId = id;
            c.category = category;
            c.passedValidation = validated;
            c.variantTags = (tags != null && tags.Length > 0) ? tags : null;
            return c;
        }

        /// <summary> An unregistered category refuses: nothing is placed, and the reason is CategoryEmpty -- no
        /// grey box, no borrowed asset. </summary>
        private static void CaseEmptyCategoryRefuses(StringBuilder builder, List<string> failures)
        {
            ContentCatalog catalog = new ContentCatalog(); // register nothing
            ContentResolution r = ContentResolver.Resolve(catalog, "street_tree", "lot#42", 12345UL);
            Expect(!r.resolved, "empty: an unregistered category did not refuse", failures);
            Expect(r.refusal.reason == ContentRefusalReason.CategoryEmpty,
                "empty: refusal reason was not CategoryEmpty", failures);
            Expect(string.IsNullOrEmpty(r.asset.assetId),
                "empty: a refusal still carried an asset (something would be placed)", failures);
            builder.Append("RCMRESOLVE ").Append("empty-refuses".PadRight(24))
                   .Append("unregistered category places nothing and refuses (CategoryEmpty)\n");
        }

        /// <summary> The three refusal reasons are told apart: empty vs. a validated asset of the WRONG variant
        /// (NoVariantMatch) vs. a right-variant asset that FAILED validation (FailedValidation). </summary>
        private static void CaseReasonsDistinguished(StringBuilder builder, List<string> failures)
        {
            // Present but wrong variant: a validated "oak" when "palm" is required -> NoVariantMatch, not empty.
            ContentCatalog c1 = new ContentCatalog();
            c1.Register(Cand("tree_oak", "street_tree", true, "oak"));
            ContentResolution r1 = ContentResolver.Resolve(c1, "street_tree", "lot#7", 1UL, "palm");
            Expect(!r1.resolved && r1.refusal.reason == ContentRefusalReason.NoVariantMatch,
                "reasons: a required variant with no matching asset was not NoVariantMatch", failures);

            // Right variant but failed validation -> FailedValidation, not empty and not NoVariantMatch.
            ContentCatalog c2 = new ContentCatalog();
            c2.Register(Cand("tree_palm_broken", "street_tree", false, "palm"));
            ContentResolution r2 = ContentResolver.Resolve(c2, "street_tree", "lot#7", 1UL, "palm");
            Expect(!r2.resolved && r2.refusal.reason == ContentRefusalReason.FailedValidation,
                "reasons: a present-but-invalid matching asset was not FailedValidation", failures);

            builder.Append("RCMRESOLVE ").Append("reasons-distinct".PadRight(24))
                   .Append("empty / no-variant / failed-validation are told apart\n");
        }

        /// <summary> No third path: a resolved result carries a real asset and no meaningful refusal reason detail,
        /// while a refused result carries no asset. Every outcome is exactly one of the two. </summary>
        private static void CaseNoThirdPath(StringBuilder builder, List<string> failures)
        {
            ContentCatalog catalog = new ContentCatalog();
            catalog.Register(Cand("bench_a", "street_furniture", true));

            ContentResolution ok = ContentResolver.Resolve(catalog, "street_furniture", "stop#1", 9UL);
            Expect(ok.resolved && !string.IsNullOrEmpty(ok.asset.assetId),
                "third-path: a resolvable request did not return an asset", failures);

            ContentResolution no = ContentResolver.Resolve(catalog, "missing", "stop#1", 9UL);
            Expect(!no.resolved && string.IsNullOrEmpty(no.asset.assetId),
                "third-path: a refusal was not asset-free", failures);
            builder.Append("RCMRESOLVE ").Append("no-third-path".PadRight(24))
                   .Append("every outcome is asset XOR refusal, never both/neither\n");
        }

        /// <summary> A registered, validated category resolves to one of ITS OWN assets -- the exact category, not
        /// a neighbour. </summary>
        private static void CaseResolvesToRegisteredAsset(StringBuilder builder, List<string> failures)
        {
            ContentCatalog catalog = new ContentCatalog();
            catalog.Register(Cand("lamp_std", "street_light", true));
            catalog.Register(Cand("lamp_tall", "street_light", true));
            catalog.Register(Cand("hydrant", "street_furniture", true)); // a different category must never leak in

            ContentResolution r = ContentResolver.Resolve(catalog, "street_light", "pole#3", 77UL);
            Expect(r.resolved, "resolve: a populated validated category refused", failures);
            Expect(r.asset.category == "street_light",
                "resolve: the chosen asset was not from the requested category", failures);
            Expect(r.asset.assetId == "lamp_std" || r.asset.assetId == "lamp_tall",
                "resolve: the chosen asset was not one of the registered assets", failures);
            builder.Append("RCMRESOLVE ").Append("resolves-own".PadRight(24))
                   .Append("populated category resolves to one of its own registered assets\n");
        }

        /// <summary> The seeded draw is deterministic (same requester+seed -> same asset) and independent of
        /// registration order (the same set registered in a different order picks the same asset), and different
        /// requesters spread across more than one asset. </summary>
        private static void CaseDeterministicAndOrderIndependent(StringBuilder builder, List<string> failures)
        {
            ContentCatalog a = new ContentCatalog();
            a.Register(Cand("v_a", "sign", true));
            a.Register(Cand("v_b", "sign", true));
            a.Register(Cand("v_c", "sign", true));

            ContentResolution r1 = ContentResolver.Resolve(a, "sign", "node#88", 500UL);
            ContentResolution r2 = ContentResolver.Resolve(a, "sign", "node#88", 500UL);
            Expect(r1.resolved && r2.resolved && r1.asset.assetId == r2.asset.assetId,
                "determinism: same requester+seed picked different assets", failures);

            // Same three assets, reversed registration order -> same pick (draw is over the sorted set).
            ContentCatalog b = new ContentCatalog();
            b.Register(Cand("v_c", "sign", true));
            b.Register(Cand("v_b", "sign", true));
            b.Register(Cand("v_a", "sign", true));
            ContentResolution r3 = ContentResolver.Resolve(b, "sign", "node#88", 500UL);
            Expect(r3.resolved && r3.asset.assetId == r1.asset.assetId,
                "order: registration order changed the deterministic pick", failures);

            // Different requesters must be able to land on more than one asset (identity drives variety).
            HashSet<string> picked = new HashSet<string>();
            for (int i = 0; i < 60; i++)
            {
                ContentResolution ri = ContentResolver.Resolve(a, "sign", "node#" + i, 500UL);
                if (ri.resolved) { picked.Add(ri.asset.assetId); }
            }
            Expect(picked.Count > 1, "variety: every requester drew the identical asset (not requester-keyed)", failures);
            builder.Append("RCMRESOLVE ").Append("deterministic".PadRight(24))
                   .Append("requester+seed draw is stable, order-independent, and varies by requester\n");
        }

        /// <summary> Refusals aggregate: 4000 empty-category requests become ONE entry with count 4000 and the
        /// first requester, and two different reasons make two entries -- not one line per instance. </summary>
        private static void CaseAggregation(StringBuilder builder, List<string> failures)
        {
            ContentCatalog empty = new ContentCatalog();
            ContentRefusalAggregator agg = new ContentRefusalAggregator();
            for (int i = 0; i < 4000; i++)
            {
                agg.Add(ContentResolver.Resolve(empty, "street_tree", "lot#" + i, 3UL));
            }
            Expect(agg.DistinctCount == 1, "aggregate: 4000 identical refusals did not collapse to one entry", failures);
            Expect(agg.TotalRefusals() == 4000, "aggregate: the folded count was wrong", failures);
            IList<ContentRefusalAggregator.Entry> es = agg.Entries();
            Expect(es.Count == 1 && es[0].count == 4000 && es[0].firstRequester == "lot#0",
                "aggregate: the single entry lost its count or first requester", failures);

            // A second, different reason makes a second entry (still aggregated within each reason).
            ContentCatalog oneInvalid = new ContentCatalog();
            oneInvalid.Register(Cand("broken", "street_light", false));
            agg.Add(ContentResolver.Resolve(oneInvalid, "street_light", "pole#1", 3UL));
            agg.Add(ContentResolver.Resolve(oneInvalid, "street_light", "pole#2", 3UL));
            Expect(agg.DistinctCount == 2, "aggregate: a distinct (category,reason) did not form its own entry", failures);
            builder.Append("RCMRESOLVE ").Append("aggregation".PadRight(24))
                   .Append("refusals fold to one entry per (category,reason) with count + first requester\n");
        }

        /// <summary> Scans every C# file under City/Runtime and asserts the city layer has NO escape hatch: no
        /// <c>GameObject.CreatePrimitive</c> (a grey box), no <c>fallbackPrefab</c>/<c>defaultPrefab</c>/
        /// <c>placeholderPrefab</c> field (a silent substitution), and no "nearest category" heuristic. The path is
        /// derived from <see cref="Application.dataPath"/> at runtime (no absolute literal, C-04). The legacy
        /// vendor Scripts/ folder and editor test fixtures are intentionally out of scope -- RM-55 governs the city
        /// content layer. </summary>
        private static void CaseNoFallbackInCityLayer(StringBuilder builder, List<string> failures)
        {
            string runtimeRoot = Path.Combine(Application.dataPath,
                Path.Combine("RoadCityMaster", Path.Combine("City", "Runtime")));
            if (!Directory.Exists(runtimeRoot))
            {
                Expect(false, "scan: City/Runtime folder not found at " + runtimeRoot, failures);
                return;
            }

            string[] files = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            List<string> hits = new List<string>();
            string[] banned = new string[]
            {
                "CreatePrimitive", "fallbackPrefab", "defaultPrefab", "placeholderPrefab",
                "greyBox", "grayBox", "nearestCategory", "NearestCategory"
            };
            for (int f = 0; f < files.Length; f++)
            {
                string[] lines = File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) { continue; }
                    for (int b = 0; b < banned.Length; b++)
                    {
                        if (line.IndexOf(banned[b], StringComparison.Ordinal) >= 0)
                        {
                            hits.Add(Path.GetFileName(files[f]) + ":" + (i + 1) + " '" + banned[b] + "'");
                        }
                    }
                }
            }
            Expect(hits.Count == 0,
                "scan: city layer contains a forbidden fallback/primitive/heuristic: "
                + string.Join("; ", hits.ToArray()), failures);
            builder.Append("RCMRESOLVE ").Append("no-fallback-scan".PadRight(24))
                   .Append("City/Runtime has no CreatePrimitive, fallback/default prefab, or nearest-category (")
                   .Append(files.Length).Append(" files)\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

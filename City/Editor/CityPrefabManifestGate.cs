#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-77's headless self-test for THE PREFAB MANIFEST. It proves the manifest is a complete, honest
    /// promise: every documented row exists as a category, out-of-scope and rail (Q-15) rows are never build-
    /// required, a category that lists variants carries the VariantTag need, and -- the point of putting the
    /// manifest last -- an empty required category is refused BY NAME through the one RM-55 resolver, never filled
    /// with a fallback, a primitive, or an asset borrowed from another category. Runs headless; asserts data, not
    /// pixels. </summary>
    public static class CityPrefabManifestGate
    {
        private const string Prefix = "RCMMANIFEST";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Prefab manifest")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            _cases = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CaseEverySectionAndRowPresent(failures);
            CaseOutOfScopeNeverBuildRequired(failures);
            CaseRailFlaggedQ15(failures);
            CaseVariantsImplyVariantTag(failures);
            CaseEmptyRequiredRefusesByName(failures);
            CaseNeverBorrowsFromAnotherCategory(failures);
            CaseInstallerReportsHonestly(failures);
            CaseSlugIsStableAndSingleSlash(failures);
            CaseMetadataTableHasSixReasons(failures);
            CaseManifestIsDeterministic(failures);

            for (int i = 0; i < failures.Count; i++) { sb.AppendLine(Prefix + " FAIL   " + failures[i]); }
            sb.AppendLine(Prefix + " result " + (failures.Count == 0 ? "PASS" : "FAIL") + "  cases " + _cases
                + "  failures " + failures.Count);
            sb.Append(Prefix + " END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(message); }
        }

        private static string Num(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary> The manifest transcribes every documented row: the count is the 140 rows of § 10.38 as extended by
        /// B-15 across its 22 sections, and a spread of named rows -- one per far-apart section -- resolves by its
        /// derived id, so a dropped or renamed row is caught, not silently missing.
        ///
        /// B-15 raised the table from 108 rows in 19 sections to 140 in 22. The three new sections are the ones the
        /// author's "everything a city has, even what I did not say" exposed as missing: parks and open space (with the
        /// park path they named), squares and the pedestrian realm, and off-carriageway movement. A named row from each
        /// is asserted below, so the new sections are held to the same standard as the original nineteen. </summary>
        private static void CaseEverySectionAndRowPresent(List<string> failures)
        {
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            Expect(all.Count == 140, "manifest should carry 140 rows; got " + all.Count, failures);

            HashSet<string> sections = new HashSet<string>();
            for (int i = 0; i < all.Count; i++) { sections.Add(all[i].section); }
            Expect(sections.Count == 22, "manifest should span 22 sections; got " + sections.Count, failures);

            ManifestCategory dummy;
            Expect(PrefabManifest.TryGet("parks-and-open-space/park-path", out dummy),
                "the park path row must resolve by its derived id -- it is the author's own example of a detail the "
                + "table was missing, and it is NOT the kerbside footway row.", failures);
            Expect(PrefabManifest.TryGet("squares-promenades-and-pedestrian-realm/resting-area", out dummy),
                "the resting-area row must resolve by its derived id.", failures);
            Expect(PrefabManifest.TryGet("off-carriageway-movement-and-parking/cycle-track", out dummy),
                "the cycle-track row must resolve by its derived id.", failures);
            Expect(PrefabManifest.TryGet("road-furniture-and-safety/guardrail-run", out dummy),
                "Guardrail run row is missing from the manifest.", failures);
            Expect(PrefabManifest.TryGet("telecommunications/cell-tower", out dummy),
                "Cell tower row (the author's named case) is missing.", failures);
            Expect(PrefabManifest.TryGet("electrical-distribution-and-transmission/transmission-tower", out dummy),
                "Transmission tower row is missing.", failures);
            Expect(PrefabManifest.TryGet("buildings-by-zone/residential-low-density", out dummy),
                "Residential low density row is missing.", failures);
            Expect(PrefabManifest.TryGet("site-works-and-pads/driveway", out dummy),
                "Driveway row is missing.", failures);
            Expect(!PrefabManifest.TryGet("nope/not-a-real-row", out dummy),
                "TryGet should fail for an id that was never defined.", failures);
        }

        /// <summary> Nothing marked OUT OF SCOPE or rail-only can ever be a required build category. The rail
        /// turnouts row is the documented out-of-scope case; the build-required set contains only in-scope rows and
        /// so excludes every rail and out-of-scope row. </summary>
        private static void CaseOutOfScopeNeverBuildRequired(List<string> failures)
        {
            ManifestCategory turnouts;
            bool found = PrefabManifest.TryGet("rail/turnouts-and-crossings", out turnouts);
            Expect(found, "the out-of-scope 'Turnouts and crossings' row must still be listed.", failures);
            Expect(found && turnouts.scope == ManifestScope.OutOfScope,
                "'Turnouts and crossings' must be OutOfScope.", failures);
            Expect(found && !turnouts.RequiresBuild,
                "an out-of-scope row must never be build-required.", failures);

            List<ManifestCategory> required = PrefabManifest.BuildRequiredCategories();
            bool anyNonInScope = false;
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i].scope != ManifestScope.InScope) { anyNonInScope = true; }
            }
            Expect(!anyNonInScope, "the build-required set must contain only in-scope rows.", failures);
        }

        /// <summary> Rail ships only if Q-15 says so: every rail row is flagged RailOnlyQ15 (or, for turnouts,
        /// OutOfScope) and none is InScope, so a road-only build never treats rail content as a missing promise.
        /// </summary>
        private static void CaseRailFlaggedQ15(List<string> failures)
        {
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            int railRows = 0;
            int railInScope = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].section != "Rail") { continue; }
                railRows++;
                if (all[i].scope == ManifestScope.InScope) { railInScope++; }
            }
            Expect(railRows == 7, "the Rail section should carry 7 rows; got " + railRows, failures);
            Expect(railInScope == 0, "no rail row may be InScope until Q-15 puts rail in scope.", failures);
        }

        /// <summary> A category that lists required variants carries the VariantTag need automatically -- the tool
        /// picks between variants by rule, so the rule needs a label to match. Conversely a row with no variants
        /// (the turnouts row) carries no VariantTag. </summary>
        private static void CaseVariantsImplyVariantTag(List<string> failures)
        {
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            int checkedWithVariants = 0;
            for (int i = 0; i < all.Count; i++)
            {
                bool hasVariants = all[i].RequiredVariantCount > 0;
                bool hasTag = (all[i].metadata & PrefabMetadataNeed.VariantTag) != 0;
                if (hasVariants)
                {
                    checkedWithVariants++;
                    Expect(hasTag, all[i].categoryId + " lists variants but lacks the VariantTag need.", failures);
                }
                else
                {
                    Expect(!hasTag, all[i].categoryId + " has no variants but carries VariantTag.", failures);
                }
            }
            Expect(checkedWithVariants >= 130,
                "almost every row lists variants; expected 130+, checked " + checkedWithVariants, failures);
        }

        /// <summary> The heart of "the manifest is last": an in-scope category with no content is refused BY NAME.
        /// A fresh content set gets one empty entry per manifest row; its catalog holds nothing; and resolving a
        /// required category returns an RM-55 refusal whose reason is CategoryEmpty and whose text names the
        /// category id -- not a fallback, not a primitive. </summary>
        private static void CaseEmptyRequiredRefusesByName(List<string> failures)
        {
            CategoryContentSet set = ScriptableObject.CreateInstance<CategoryContentSet>();
            int added = ManifestInstaller.EnsureAllCategories(set);
            Expect(added == 140, "EnsureAllCategories should add 140 empty entries; added " + added, failures);
            Expect(set.Count == 140, "the set should hold 140 categories after install; got " + set.Count,
                failures);

            ContentValidationCache cache = new ContentValidationCache();
            ContentCatalog catalog = set.BuildCatalog(cache);
            ManifestCategory guardrail;
            PrefabManifest.TryGet("road-furniture-and-safety/guardrail-run", out guardrail);
            ContentResolution res = ManifestInstaller.ResolveOrRefuse(catalog, guardrail, "RM-76", 4242UL);

            Expect(!res.resolved, "an empty required category must not resolve to any asset.", failures);
            Expect(res.refusal.reason == ContentRefusalReason.CategoryEmpty,
                "the refusal reason for an empty category must be CategoryEmpty.", failures);
            Expect(res.refusal.category == guardrail.categoryId,
                "the refusal must name the requested category id.", failures);
            Expect(res.refusal.Describe().Contains(guardrail.categoryId),
                "the refusal text must name the category so an author knows what to register.", failures);
            Object.DestroyImmediate(set);
        }

        /// <summary> No category ever borrows content from another. With a catalog holding a passing asset in one
        /// category, resolving a DIFFERENT (empty) category still refuses -- the asset from the wrong category is
        /// never substituted -- while resolving the category that does hold it succeeds. </summary>
        private static void CaseNeverBorrowsFromAnotherCategory(List<string> failures)
        {
            ContentCatalog catalog = new ContentCatalog();
            ContentCandidate other = new ContentCandidate();
            other.assetId = "OtherCategoryAsset";
            other.category = "buildings-by-zone/hospitality";
            other.passedValidation = true;
            catalog.Register(other);

            ManifestCategory guardrail;
            PrefabManifest.TryGet("road-furniture-and-safety/guardrail-run", out guardrail);
            ContentResolution wrong = ManifestInstaller.ResolveOrRefuse(catalog, guardrail, "RM-76", 7UL);
            Expect(!wrong.resolved, "a category must never borrow an asset filed under a different category.",
                failures);
            Expect(wrong.refusal.reason == ContentRefusalReason.CategoryEmpty,
                "resolving the empty guardrail category should refuse CategoryEmpty even when other content exists.",
                failures);

            ManifestCategory hospitality;
            PrefabManifest.TryGet("buildings-by-zone/hospitality", out hospitality);
            ContentResolution right = ManifestInstaller.ResolveOrRefuse(catalog, hospitality, "RM-42", 7UL);
            Expect(right.resolved && right.asset.assetId == "OtherCategoryAsset",
                "the category that holds the asset should resolve to it.", failures);
        }
        /// <summary> The installer reports honestly, never optimistically. A fresh set gets one empty entry per
        /// row; the report has one status per row; every in-scope empty row reads Empty and every rail/out-of-scope
        /// empty row reads OutOfScopeEmpty; and -- because nothing was authored -- not one row reads Populated or
        /// Partial. </summary>
        private static void CaseInstallerReportsHonestly(List<string> failures)
        {
            CategoryContentSet set = ScriptableObject.CreateInstance<CategoryContentSet>();
            int added = ManifestInstaller.EnsureAllCategories(set);
            Expect(added == 140, "EnsureAllCategories should add 140 empty entries; added " + added, failures);
            Expect(set.Count == 140, "the set should hold 140 categories after install; got " + set.Count,
                failures);

            ContentValidationCache cache = new ContentValidationCache();
            List<ManifestCategoryStatus> report = ManifestInstaller.Report(set, cache);
            Expect(report.Count == 140, "the report should carry one status per row; got " + report.Count, failures);

            int emptyInScope = 0, outOfScopeEmpty = 0, populatedOrPartial = 0;
            for (int i = 0; i < report.Count; i++)
            {
                switch (report[i].health)
                {
                    case ManifestCategoryHealth.Empty: emptyInScope++; break;
                    case ManifestCategoryHealth.OutOfScopeEmpty: outOfScopeEmpty++; break;
                    default: populatedOrPartial++; break;
                }
            }
            Expect(emptyInScope == 133, "133 in-scope rows should report Empty on a fresh set; got " + emptyInScope,
                failures);
            Expect(outOfScopeEmpty == 7, "the 7 rail rows should report OutOfScopeEmpty; got " + outOfScopeEmpty,
                failures);
            Expect(populatedOrPartial == 0, "no row can be Populated or Partial before any asset is authored.",
                failures);
            Object.DestroyImmediate(set);
        }

        /// <summary> Every category id is a stable, single-slash slug: lowercase, exactly one '/', no double dashes,
        /// no leading or trailing dash or slash. This is what makes the id a safe, deterministic key for RM-56's
        /// entries and RM-55's refusals. </summary>
        private static void CaseSlugIsStableAndSingleSlash(List<string> failures)
        {
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].categoryId;
                int slashes = 0;
                for (int c = 0; c < id.Length; c++) { if (id[c] == '/') { slashes++; } }
                Expect(slashes == 1, id + " must contain exactly one '/'; found " + slashes, failures);
                Expect(id == id.ToLowerInvariant(), id + " must be lowercase.", failures);
                Expect(!id.Contains("--") && !id.Contains("//"),
                    id + " must not contain a doubled separator.", failures);
                Expect(id.Length > 0 && id[0] != '-' && id[0] != '/'
                    && id[id.Length - 1] != '-' && id[id.Length - 1] != '/',
                    id + " must not begin or end with a separator.", failures);
            }
        }
        /// <summary> The placement-metadata table is exactly the six needs the doc lists, no more, no fewer: all
        /// six PrefabMetadataNeed bits are represented once, and every row carries a display name and a non-empty
        /// "why" so an author reading the table learns why each need exists. </summary>
        private static void CaseMetadataTableHasSixReasons(List<string> failures)
        {
            IReadOnlyList<PlacementMetadataNote> notes = PrefabManifest.MetadataNeeds();
            Expect(notes.Count == 6, "the metadata table should list exactly 6 needs; got " + notes.Count, failures);

            PrefabMetadataNeed union = PrefabMetadataNeed.None;
            for (int i = 0; i < notes.Count; i++)
            {
                union |= notes[i].need;
                Expect(!string.IsNullOrEmpty(notes[i].displayName), "a metadata note has no display name.", failures);
                Expect(!string.IsNullOrEmpty(notes[i].why), notes[i].displayName + " has no 'why' text.", failures);
            }
            PrefabMetadataNeed allSix = PrefabMetadataNeed.FootprintSizeAndShape | PrefabMetadataNeed.AttachmentPoints
                | PrefabMetadataNeed.PartLength | PrefabMetadataNeed.VariantTag
                | PrefabMetadataNeed.GroundContactMode | PrefabMetadataNeed.FrontageSideAndOffset;
            Expect(union == allSix, "all six metadata needs must be represented exactly once in the table.", failures);
        }

        /// <summary> C-06 for the manifest: it is pure data, so reading it twice yields byte-identical content in
        /// byte-identical order. A fingerprint of every row (id, section, scope, variant count, metadata bits) built
        /// twice must match, and must carry all 140 rows. </summary>
        private static void CaseManifestIsDeterministic(List<string> failures)
        {
            string first = Fingerprint();
            string second = Fingerprint();
            Expect(first == second, "the manifest fingerprint must be identical across reads (C-06).", failures);

            int rows = 0;
            for (int i = 0; i < first.Length; i++) { if (first[i] == '\n') { rows++; } }
            Expect(rows == 140, "the fingerprint must carry all 140 rows; counted " + rows, failures);
        }

        /// <summary> A stable, order-sensitive digest of the whole manifest, used only by the determinism case. </summary>
        private static string Fingerprint()
        {
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                sb.Append(all[i].categoryId).Append('|').Append(all[i].section).Append('|');
                sb.Append(((int)all[i].scope).ToString(CultureInfo.InvariantCulture)).Append('|');
                sb.Append(all[i].RequiredVariantCount.ToString(CultureInfo.InvariantCulture)).Append('|');
                sb.Append(((int)all[i].metadata).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
#endif

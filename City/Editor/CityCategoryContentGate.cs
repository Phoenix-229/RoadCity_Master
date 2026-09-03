#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-56's gate: proves an author can see WHERE every category's content comes from, and that what they
    /// are shown is what a build will use. It checks that paths are project-relative and that an absolute one is
    /// refused (C-04), that a named source folder alone places NOTHING until an explicit rescan (so the build never
    /// enumerates a folder), that a rescan's result is sorted and identical whatever order the file system offered the
    /// paths in, that each asset carries RM-48's pass/fail mark with its specific reason, that those marks come from a
    /// cache which recomputes on asset CHANGE and not per repaint, that an empty category is visibly empty rather than
    /// looking configured, and that a category shown as failing makes RM-55's resolver refuse. Report lines prefixed
    /// RCMCONTENTSRC. </summary>
    public static class CityCategoryContentGate
    {
        [MenuItem("Window/Road City Master/Testing/Category content sources")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityCategoryContentGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMCONTENTSRC report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCONTENTSRC BEGIN\n");
            List<string> failures = new List<string>();

            CaseProjectRelativePaths(builder, failures);
            CaseFolderAloneResolvesNothing(builder, failures);
            CaseRescanSortedAndDeterministic(builder, failures);
            CaseRescanRefusals(builder, failures);
            CaseValidationMarks(builder, failures);
            CaseCacheNotPerRepaint(builder, failures);
            CaseEmptyLooksEmpty(builder, failures);
            CaseShownIsWhatBuildUses(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCONTENTSRC FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCONTENTSRC result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCONTENTSRC END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Build an entry quickly. </summary>
        private static CategoryContentEntry Entry(string category, string folder, params string[] paths)
        {
            CategoryContentEntry e = CategoryContentEntry.Empty(category);
            e.sourceFolder = folder != null ? folder : string.Empty;
            e.assetPaths = paths != null ? paths : new string[0];
            return e;
        }

        /// <summary> Paths shown and stored are project-relative; an absolute one converts or is refused, and folder
        /// containment respects the separator boundary. The absolute paths used here are DERIVED from the running
        /// project (no absolute literal in this file, C-04). </summary>
        private static void CaseProjectRelativePaths(StringBuilder builder, List<string> failures)
        {
            string assetsAbs = ProjectPaths.Normalise(Application.dataPath);
            string insideAbs = assetsAbs + "/RoadCityMaster/City/Content/Prefabs";
            string outsideAbs = ProjectPaths.FolderOf(assetsAbs) + "/NotInTheProject/thing.fbx";

            Expect(ProjectPaths.ToProjectRelative(insideAbs, assetsAbs)
                   == "Assets/RoadCityMaster/City/Content/Prefabs",
                "paths: an absolute path inside the project did not convert to its project-relative form", failures);
            Expect(ProjectPaths.ToProjectRelative(outsideAbs, assetsAbs) == null,
                "paths: a path outside the project was not refused", failures);
            Expect(ProjectPaths.ToProjectRelative(assetsAbs, assetsAbs) == "Assets",
                "paths: the Assets folder itself did not convert", failures);

            Expect(ProjectPaths.IsProjectRelative("Assets/City/Trees"),
                "paths: a valid project-relative path was rejected", failures);
            Expect(!ProjectPaths.IsProjectRelative(insideAbs),
                "paths: an absolute path passed as project-relative", failures);
            Expect(!ProjectPaths.IsProjectRelative("Assets/../Elsewhere"),
                "paths: a path climbing out of the project was accepted", failures);
            Expect(!ProjectPaths.IsProjectRelative("Library/City/Trees"),
                "paths: a path outside Assets was accepted", failures);

            Expect(ProjectPaths.IsUnder("Assets/Trees", "Assets/Trees/oak.fbx"),
                "paths: a contained asset was not seen as under its folder", failures);
            Expect(!ProjectPaths.IsUnder("Assets/Trees", "Assets/TreesWip/oak.fbx"),
                "paths: a name-prefix folder was wrongly treated as containing the asset", failures);
            Expect(ProjectPaths.FileNameWithoutExtension("Assets/Trees/oak_big.fbx") == "oak_big",
                "paths: the display name was not the bare file name", failures);

            builder.Append("RCMCONTENTSRC ").Append("project-relative".PadRight(26))
                   .Append("paths convert, absolute is refused, containment respects the separator\n");
        }

        /// <summary> A named source folder ALONE resolves nothing: it is an authoring convenience, and only the
        /// explicit list a rescan wrote is what anything reads. </summary>
        private static void CaseFolderAloneResolvesNothing(StringBuilder builder, List<string> failures)
        {
            CategoryContentEntry e = Entry("cell_tower", "Assets/RoadCityMaster/City/Content/Prefabs");
            CategoryFoldoutView v = CategoryContentSet.BuildView(e, new ContentValidationCache());

            Expect(v.count == 0 && v.assets.Length == 0,
                "folder-alone: naming a source folder resolved assets without a rescan", failures);
            Expect(v.sourceFolderNamed && v.emptyKind == CategoryEmptyKind.FolderNamedButNoAssets,
                "folder-alone: a named-but-unscanned folder was not reported as its own kind of empty", failures);
            Expect(e.ResolvedAssetPaths().Length == 0,
                "folder-alone: the serialised list was not empty", failures);

            builder.Append("RCMCONTENTSRC ").Append("folder-alone".PadRight(26))
                   .Append("a source folder places nothing until an explicit rescan (build never enumerates)\n");
        }

        /// <summary> A rescan's serialised result is SORTED, de-duplicated, scoped to the folder, and identical
        /// whatever order the file system offered the paths in -- so nothing downstream depends on enumeration
        /// order. It also reports what it changed. </summary>
        private static void CaseRescanSortedAndDeterministic(StringBuilder builder, List<string> failures)
        {
            string folder = "Assets/City/Trees";
            List<string> scrambled = new List<string>(new string[]
            {
                folder + "/palm.fbx", folder + "/oak.fbx", folder + "/birch.fbx",
                folder + "/oak.fbx",                        // a duplicate must collapse
                "Assets/City/TreesWip/unfinished.fbx"       // outside the folder: ignored, counted
            });
            List<string> reversed = new List<string>(scrambled);
            reversed.Reverse();

            CategoryFolderRescan.Result a = CategoryFolderRescan.Rescan(folder, scrambled, null);
            CategoryFolderRescan.Result b = CategoryFolderRescan.Rescan(folder, reversed, null);

            Expect(a.accepted, "rescan: a valid rescan was refused", failures);
            Expect(a.assetPaths.Length == 3, "rescan: the asset count was wrong (duplicate or scope leak)", failures);
            Expect(a.rejectedOutsideFolder == 1, "rescan: a path outside the folder was not rejected", failures);
            Expect(a.assetPaths.Length == 3 && a.assetPaths[0] == folder + "/birch.fbx"
                   && a.assetPaths[2] == folder + "/palm.fbx",
                "rescan: the result was not path-sorted", failures);

            bool identical = a.assetPaths.Length == b.assetPaths.Length;
            for (int i = 0; identical && i < a.assetPaths.Length; i++)
            {
                if (a.assetPaths[i] != b.assetPaths[i]) { identical = false; }
            }
            Expect(identical, "rescan: enumeration order changed the serialised result", failures);

            // Re-scanning against an existing list reports the difference rather than rewriting silently.
            List<string> existing = new List<string>(new string[] { folder + "/oak.fbx", folder + "/gone.fbx" });
            CategoryFolderRescan.Result c = CategoryFolderRescan.Rescan(folder, scrambled, existing);
            Expect(c.added == 2 && c.removed == 1,
                "rescan: the added/removed report did not match the change", failures);

            builder.Append("RCMCONTENTSRC ").Append("rescan-stable".PadRight(26))
                   .Append("rescan sorts, de-duplicates, stays in scope, and reports what changed\n");
        }

        /// <summary> A rescan refuses with a message rather than storing something unusable: no folder named, or a
        /// folder given as an absolute path (which would be meaningless on another machine -- C-04). </summary>
        private static void CaseRescanRefusals(StringBuilder builder, List<string> failures)
        {
            CategoryFolderRescan.Result none = CategoryFolderRescan.Rescan(string.Empty, null, null);
            Expect(!none.accepted && !string.IsNullOrEmpty(none.message),
                "refusals: rescanning with no source folder was not refused with a message", failures);
            Expect(none.assetPaths != null && none.assetPaths.Length == 0,
                "refusals: a refused rescan still produced assets", failures);

            string absoluteFolder = ProjectPaths.Normalise(Application.dataPath) + "/City/Trees";
            CategoryFolderRescan.Result abs = CategoryFolderRescan.Rescan(absoluteFolder,
                new List<string>(new string[] { "Assets/City/Trees/oak.fbx" }), null);
            Expect(!abs.accepted, "refusals: an absolute source folder was accepted", failures);
            Expect(abs.message != null && abs.message.Contains("project-relative"),
                "refusals: the absolute-folder refusal did not say the path must be project-relative", failures);

            builder.Append("RCMCONTENTSRC ").Append("rescan-refusals".PadRight(26))
                   .Append("no folder and an absolute folder are both refused with a message\n");
        }

        /// <summary> Every asset carries RM-48's mark: a pass, or a fail WITH the validator's specific reason. An
        /// asset nobody has validated reads back as unchecked -- never as a pass. </summary>
        private static void CaseValidationMarks(StringBuilder builder, List<string> failures)
        {
            string folder = "Assets/City/Poles";
            CategoryContentEntry e = Entry("street_light", folder,
                folder + "/lamp_ok.prefab", folder + "/lamp_bad.prefab", folder + "/lamp_new.prefab");

            ContentValidationCache cache = new ContentValidationCache();
            cache.Record(folder + "/lamp_ok.prefab", true, null, 1L);
            cache.Record(folder + "/lamp_bad.prefab", false,
                "pivot is 1.80 m above the mesh base (tolerance 0.05 m)", 1L);
            // lamp_new is deliberately never recorded.

            CategoryFoldoutView v = CategoryContentSet.BuildView(e, cache);
            Expect(v.count == 3 && v.passedCount == 1 && v.failedCount == 1 && v.uncheckedCount == 1,
                "marks: the pass/fail/unchecked counts did not match the recorded verdicts", failures);

            CategoryAssetView bad = new CategoryAssetView();
            CategoryAssetView unchecked_ = new CategoryAssetView();
            for (int i = 0; i < v.assets.Length; i++)
            {
                if (v.assets[i].displayName == "lamp_bad") { bad = v.assets[i]; }
                if (v.assets[i].displayName == "lamp_new") { unchecked_ = v.assets[i]; }
            }
            Expect(bad.state == ContentValidationState.Failed && bad.reason.Contains("pivot"),
                "marks: a failing asset did not carry the validator's own reason", failures);
            Expect(unchecked_.state == ContentValidationState.NotYetChecked,
                "marks: an unvalidated asset was shown as something other than unchecked", failures);
            Expect(bad.Describe().Contains("FAIL"),
                "marks: the row line did not show the failure", failures);

            builder.Append("RCMCONTENTSRC ").Append("validation-marks".PadRight(26))
                   .Append("each asset shows pass / fail-with-reason / unchecked, counted correctly\n");
        }

        /// <summary> The marks come from a cache: repainting the foldout costs nothing, and only an asset CHANGE
        /// (a moved stamp) or an explicit invalidation forces RM-48's checks to run again. </summary>
        private static void CaseCacheNotPerRepaint(StringBuilder builder, List<string> failures)
        {
            string asset = "Assets/City/Poles/lamp_ok.prefab";
            ContentValidationCache cache = new ContentValidationCache();

            Expect(cache.NeedsRecompute(asset, 1L),
                "cache: an asset with no verdict did not need computing", failures);
            cache.Record(asset, true, null, 1L);

            for (int repaint = 0; repaint < 50; repaint++)
            {
                if (cache.NeedsRecompute(asset, 1L))
                {
                    Expect(false, "cache: a repaint with no asset change forced a recompute", failures);
                    break;
                }
            }
            Expect(cache.Hits == 50 && cache.Misses == 1,
                "cache: fifty unchanged repaints did not resolve to fifty cache hits", failures);

            Expect(cache.NeedsRecompute(asset, 2L),
                "cache: a changed asset stamp did not force a recompute", failures);
            cache.Invalidate(asset);
            Expect(cache.NeedsRecompute(asset, 2L),
                "cache: an invalidated verdict was still served from the cache", failures);
            string reason;
            Expect(cache.StateOf(asset, out reason) == ContentValidationState.NotYetChecked,
                "cache: an invalidated asset still read back a verdict", failures);

            builder.Append("RCMCONTENTSRC ").Append("cache-on-change".PadRight(26))
                   .Append("verdicts are cached across repaints and recomputed only on asset change\n");
        }

        /// <summary> An empty category is VISIBLY empty and says which kind of empty it is: never wired at all, or
        /// wired to a folder that has yielded nothing. Neither may read like a configured category. </summary>
        private static void CaseEmptyLooksEmpty(StringBuilder builder, List<string> failures)
        {
            ContentValidationCache cache = new ContentValidationCache();

            CategoryFoldoutView unwired = CategoryContentSet.BuildView(Entry("bus_shelter", null), cache);
            Expect(unwired.IsEmpty && unwired.emptyKind == CategoryEmptyKind.NothingConfigured,
                "empty: an unwired category was not reported as nothing-configured", failures);
            Expect(unwired.Headline().Contains("EMPTY"),
                "empty: an unwired category's header did not read as empty", failures);
            Expect(unwired.Headline().Contains("no source folder"),
                "empty: an unwired category did not say it has no source folder", failures);

            CategoryFoldoutView fruitless = CategoryContentSet.BuildView(
                Entry("bus_shelter", "Assets/City/Shelters"), cache);
            Expect(fruitless.IsEmpty && fruitless.emptyKind == CategoryEmptyKind.FolderNamedButNoAssets,
                "empty: a named folder with no assets was not its own kind of empty", failures);
            Expect(fruitless.Headline().Contains("EMPTY") && fruitless.Headline().Contains("Assets/City/Shelters"),
                "empty: a fruitless category did not read as empty while naming its folder", failures);
            Expect(fruitless.Headline() != unwired.Headline(),
                "empty: the two kinds of empty are indistinguishable to the author", failures);

            CategoryContentEntry filled = Entry("bus_shelter", "Assets/City/Shelters",
                "Assets/City/Shelters/shelter_a.prefab");
            cache.Record("Assets/City/Shelters/shelter_a.prefab", true, null, 1L);
            CategoryFoldoutView populated = CategoryContentSet.BuildView(filled, cache);
            Expect(!populated.IsEmpty && populated.emptyKind == CategoryEmptyKind.NotEmpty
                   && !populated.Headline().Contains("EMPTY"),
                "empty: a populated category was reported as empty", failures);

            builder.Append("RCMCONTENTSRC ").Append("empty-looks-empty".PadRight(26))
                   .Append("both kinds of empty are visible and distinct from a configured category\n");
        }

        /// <summary> What the author is SHOWN is what a build USES: the same wiring becomes RM-55's catalog, so an
        /// asset marked failing in the foldout makes the resolver refuse (and says so with the validation reason),
        /// while a passing one resolves to exactly that asset. The set also keeps one entry per category and lists
        /// them in a stable order whatever order they were added in. </summary>
        private static void CaseShownIsWhatBuildUses(StringBuilder builder, List<string> failures)
        {
            CategoryContentSet set = ScriptableObject.CreateInstance<CategoryContentSet>();
            try
            {
                string towers = "Assets/City/Towers";
                string trees = "Assets/City/Trees";
                set.SetEntry(Entry("cell_tower", towers, towers + "/tower_bad.prefab"));
                set.SetEntry(Entry("street_tree", trees, trees + "/oak.prefab"));
                set.SetEntry(Entry("cell_tower", towers, towers + "/tower_bad.prefab")); // upsert, not a duplicate
                Expect(set.Count == 2, "set: re-adding a category duplicated it instead of replacing it", failures);

                IList<string> keys = set.Categories();
                Expect(keys.Count == 2 && keys[0] == "cell_tower" && keys[1] == "street_tree",
                    "set: categories were not listed in a stable sorted order", failures);

                ContentValidationCache cache = new ContentValidationCache();
                cache.Record(towers + "/tower_bad.prefab", false, "root local scale is 1.4 (must be 1)", 1L);
                cache.Record(trees + "/oak.prefab", true, null, 1L);

                IList<CategoryFoldoutView> views = set.BuildAllViews(cache);
                Expect(views.Count == 2 && views[0].category == "cell_tower" && views[0].failedCount == 1,
                    "set: the foldout views did not report the failing tower", failures);

                ContentCatalog catalog = set.BuildCatalog(cache);
                ContentResolution tower = ContentResolver.Resolve(catalog, "cell_tower", "mast#1", 42UL);
                Expect(!tower.resolved && tower.refusal.reason == ContentRefusalReason.FailedValidation,
                    "bridge: an asset shown as failing did not make the resolver refuse for that reason", failures);

                ContentResolution tree = ContentResolver.Resolve(catalog, "street_tree", "lot#1", 42UL);
                Expect(tree.resolved && tree.asset.assetId == trees + "/oak.prefab",
                    "bridge: an asset shown as passing did not resolve to that asset", failures);

                ContentResolution absent = ContentResolver.Resolve(catalog, "bench", "stop#1", 42UL);
                Expect(!absent.resolved && absent.refusal.reason == ContentRefusalReason.CategoryEmpty,
                    "bridge: a category the set never wired did not refuse as empty", failures);

                builder.Append("RCMCONTENTSRC ").Append("shown-is-used".PadRight(26))
                       .Append("the wiring shown per category is the catalog RM-55 resolves from\n");
            }
            finally
            {
                Object.DestroyImmediate(set);
            }
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

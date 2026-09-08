// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One category's content wiring, as the author sees it. The two halves are deliberately different in
    /// kind: <see cref="assetPaths"/> are EXPLICIT references and are the ONLY thing a build reads -- they survive a
    /// folder rename and Unity's dependency tracking understands them -- while <see cref="sourceFolder"/> is an
    /// authoring CONVENIENCE that fills that list on demand. The build therefore never enumerates a folder, so it
    /// cannot pick up work-in-progress and cannot depend on the file system's enumeration order (C-06). </summary>
    [Serializable]
    public struct CategoryContentEntry
    {
        /// <summary> The category key -- the same string RM-48's rules, RM-55's resolver and RM-77's manifest use. </summary>
        public string category;

        /// <summary> Where the author keeps this category's content, project-relative (C-04). May be empty. </summary>
        public string sourceFolder;

        /// <summary> The explicit, sorted, serialised asset list a build resolves from. </summary>
        public string[] assetPaths;

        /// <summary> What the last rescan did, in one line, so the action leaves a visible trace. </summary>
        public string lastRescanNote;

        /// <summary> A fresh entry for a category with nothing wired yet. </summary>
        public static CategoryContentEntry Empty(string category)
        {
            CategoryContentEntry e;
            e.category = category;
            e.sourceFolder = string.Empty;
            e.assetPaths = new string[0];
            e.lastRescanNote = string.Empty;
            return e;
        }

        /// <summary> The serialised asset list, never null. Reading this NEVER touches the file system. </summary>
        public string[] ResolvedAssetPaths()
        {
            return assetPaths != null ? assetPaths : new string[0];
        }

        /// <summary> True when a source folder is named at all. </summary>
        public bool HasSourceFolder()
        {
            return !string.IsNullOrEmpty(sourceFolder);
        }
    }


    /// <summary> Validation standing per asset, CACHED. RM-56 draws a pass or fail mark beside every asset, and
    /// recomputing RM-48's geometry checks on every repaint would make the inspector cost real frames -- so a result
    /// is recorded once against the asset's change stamp and reused until that stamp moves, the same discipline
    /// RM-53's hierarchy marker follows. The stamp is supplied by the caller (an asset timestamp or import hash in
    /// the editor half), so this stays pure and testable. </summary>
    public sealed class ContentValidationCache
    {
        private struct Cached
        {
            public ContentValidationState state;
            public string reason;
            public long stamp;
        }

        private readonly Dictionary<string, Cached> byPath = new Dictionary<string, Cached>(StringComparer.Ordinal);
        private int hits;
        private int misses;

        /// <summary> Record RM-48's verdict for one asset against the stamp it was computed at. </summary>
        public void Record(string assetPath, bool passed, string reason, long stamp)
        {
            if (string.IsNullOrEmpty(assetPath)) { return; }
            Cached c;
            c.state = passed ? ContentValidationState.Passed : ContentValidationState.Failed;
            c.reason = passed ? string.Empty : (reason != null ? reason : "failed validation");
            c.stamp = stamp;
            byPath[assetPath] = c;
        }

        /// <summary> Whether the cached verdict for this asset must be recomputed -- because there is none, or
        /// because the asset has changed since. A repaint that changes nothing is a HIT and costs no work. </summary>
        public bool NeedsRecompute(string assetPath, long currentStamp)
        {
            Cached c;
            if (string.IsNullOrEmpty(assetPath)) { return false; }
            if (!byPath.TryGetValue(assetPath, out c) || c.stamp != currentStamp)
            {
                misses++;
                return true;
            }
            hits++;
            return false;
        }

        /// <summary> The standing of one asset, with RM-48's reason when it failed. An asset never validated reads
        /// back <see cref="ContentValidationState.NotYetChecked"/> -- never a pass. </summary>
        public ContentValidationState StateOf(string assetPath, out string reason)
        {
            Cached c;
            if (!string.IsNullOrEmpty(assetPath) && byPath.TryGetValue(assetPath, out c))
            {
                reason = c.reason;
                return c.state;
            }
            reason = string.Empty;
            return ContentValidationState.NotYetChecked;
        }

        /// <summary> Drop one asset's verdict, so the next query recomputes it (an asset-changed notification). </summary>
        public void Invalidate(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) { byPath.Remove(assetPath); }
        }

        /// <summary> Drop every verdict. </summary>
        public void Clear() { byPath.Clear(); }

        /// <summary> Reuses of a still-valid verdict. </summary>
        public int Hits { get { return hits; } }

        /// <summary> Times a verdict was absent or stale and had to be recomputed. </summary>
        public int Misses { get { return misses; } }

        /// <summary> How many assets have a recorded verdict. </summary>
        public int RecordedCount { get { return byPath.Count; } }
    }


    /// <summary> The "rescan" action: turns a source folder's contents into the entry's EXPLICIT, sorted asset list.
    /// It is a deliberate, visible author action, never something a build does -- the enumeration itself belongs to
    /// the caller (AssetDatabase in the editor half) and is passed in here, so the ordering rule, the folder-scope
    /// rule and the C-04 refusal are all testable without a project on disk. </summary>
    public static class CategoryFolderRescan
    {
        /// <summary> What a rescan did, or why it refused. </summary>
        public struct Result
        {
            public bool accepted;
            public string message;
            public string[] assetPaths;   // sorted, de-duplicated, all inside the source folder
            public int added;
            public int removed;
            public int rejectedOutsideFolder;
        }

        /// <summary> Fold an enumeration of candidate asset paths into the entry's serialised list. Refuses outright
        /// when no folder is named or when the folder is not project-relative; silently drops candidates that lie
        /// outside the folder (counting them) and sorts what remains, so the result is identical whatever order the
        /// file system handed the paths over in. </summary>
        public static Result Rescan(string sourceFolder, IList<string> enumeratedPaths, IList<string> existingPaths)
        {
            Result r = new Result();
            r.assetPaths = new string[0];

            if (string.IsNullOrEmpty(sourceFolder))
            {
                r.message = "no source folder is named for this category: nothing to rescan";
                return r;
            }
            if (!ProjectPaths.IsProjectRelative(sourceFolder))
            {
                r.message = "source folder must be project-relative (an absolute path is never stored): " + sourceFolder;
                return r;
            }

            string folder = ProjectPaths.Normalise(sourceFolder);
            List<string> kept = new List<string>();
            if (enumeratedPaths != null)
            {
                for (int i = 0; i < enumeratedPaths.Count; i++)
                {
                    string p = ProjectPaths.Normalise(enumeratedPaths[i]);
                    if (p.Length == 0) { continue; }
                    if (!ProjectPaths.IsProjectRelative(p) || !ProjectPaths.IsUnder(folder, p))
                    {
                        r.rejectedOutsideFolder++;
                        continue;
                    }
                    if (!kept.Contains(p)) { kept.Add(p); }
                }
            }
            kept.Sort(StringComparer.Ordinal);

            // What changed, so the action reports itself rather than silently rewriting the list.
            for (int i = 0; i < kept.Count; i++)
            {
                if (existingPaths == null || !existingPaths.Contains(kept[i])) { r.added++; }
            }
            if (existingPaths != null)
            {
                for (int i = 0; i < existingPaths.Count; i++)
                {
                    if (!kept.Contains(ProjectPaths.Normalise(existingPaths[i]))) { r.removed++; }
                }
            }

            r.assetPaths = kept.ToArray();
            r.accepted = true;
            r.message = string.Format("{0} asset(s) from '{1}': +{2} / -{3}{4}", kept.Count, folder, r.added,
                r.removed, r.rejectedOutsideFolder > 0
                    ? string.Format(" ({0} outside the folder ignored)", r.rejectedOutsideFolder) : string.Empty);
            return r;
        }
    }


    /// <summary> The category-set ASSET: the author's one place to say where each category's content comes from.
    /// It answers "where will my cell towers come from?" per category -- the source folder, the assets currently
    /// resolved from it, each one's RM-48 standing and a count -- and it is the same wiring RM-55's resolver reads,
    /// so what the author is shown IS what a build will use. Entries are kept category-sorted, and every view is
    /// built in a stable order, so the inspector list never reshuffles between repaints. </summary>
    [CreateAssetMenu(fileName = "CityCategorySet", menuName = "Road City Master/City Category Set", order = 2)]
    public sealed class CategoryContentSet : ScriptableObject
    {
        [SerializeField]
        private List<CategoryContentEntry> categories = new List<CategoryContentEntry>();

        /// <summary> How many categories are wired (including deliberately empty ones). </summary>
        public int Count { get { return categories != null ? categories.Count : 0; } }

        /// <summary> Every category key, sorted, so the foldouts appear in a stable order. </summary>
        public IList<string> Categories()
        {
            List<string> keys = new List<string>();
            if (categories != null)
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    if (!string.IsNullOrEmpty(categories[i].category)) { keys.Add(categories[i].category); }
                }
            }
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary> The wiring for one category, if it is present. </summary>
        public bool TryGetEntry(string category, out CategoryContentEntry entry)
        {
            if (categories != null && !string.IsNullOrEmpty(category))
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    if (categories[i].category == category) { entry = categories[i]; return true; }
                }
            }
            entry = CategoryContentEntry.Empty(category);
            return false;
        }

        /// <summary> Add or replace one category's wiring (upsert by category key). </summary>
        public void SetEntry(CategoryContentEntry entry)
        {
            if (string.IsNullOrEmpty(entry.category)) { return; }
            if (categories == null) { categories = new List<CategoryContentEntry>(); }
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].category == entry.category) { categories[i] = entry; return; }
            }
            categories.Add(entry);
        }

        /// <summary> Build exactly what one category's foldout displays. Paths are shown project-relative; assets are
        /// path-sorted; the counts are computed from the cached RM-48 verdicts; and an unwired or fruitless category
        /// reports WHICH kind of empty it is, so it cannot be mistaken for a configured one. </summary>
        public CategoryFoldoutView BuildView(string category, ContentValidationCache cache)
        {
            CategoryContentEntry entry;
            TryGetEntry(category, out entry);
            return BuildView(entry, cache);
        }

        /// <summary> Build the foldout view for a given entry, without needing it to be in this set. </summary>
        public static CategoryFoldoutView BuildView(CategoryContentEntry entry, ContentValidationCache cache)
        {
            CategoryFoldoutView v = new CategoryFoldoutView();
            v.category = entry.category;
            v.sourceFolder = entry.HasSourceFolder() ? ProjectPaths.Normalise(entry.sourceFolder) : string.Empty;
            v.sourceFolderNamed = entry.HasSourceFolder();

            string[] paths = entry.ResolvedAssetPaths();
            List<string> sorted = new List<string>(paths);
            sorted.Sort(StringComparer.Ordinal);

            List<CategoryAssetView> rows = new List<CategoryAssetView>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (string.IsNullOrEmpty(sorted[i])) { continue; }
                CategoryAssetView row = new CategoryAssetView();
                row.assetPath = sorted[i];
                row.displayName = ProjectPaths.FileNameWithoutExtension(sorted[i]);
                string reason = string.Empty;
                if (cache != null) { row.state = cache.StateOf(sorted[i], out reason); }
                else { row.state = ContentValidationState.NotYetChecked; }
                row.reason = reason;
                rows.Add(row);

                if (row.state == ContentValidationState.Passed) { v.passedCount++; }
                else if (row.state == ContentValidationState.Failed) { v.failedCount++; }
                else { v.uncheckedCount++; }
            }

            v.assets = rows.ToArray();
            v.count = rows.Count;
            v.emptyKind = v.count > 0
                ? CategoryEmptyKind.NotEmpty
                : (v.sourceFolderNamed ? CategoryEmptyKind.FolderNamedButNoAssets
                                       : CategoryEmptyKind.NothingConfigured);
            return v;
        }

        /// <summary> Every category's foldout view, in category order. </summary>
        public IList<CategoryFoldoutView> BuildAllViews(ContentValidationCache cache)
        {
            List<CategoryFoldoutView> views = new List<CategoryFoldoutView>();
            IList<string> keys = Categories();
            for (int i = 0; i < keys.Count; i++) { views.Add(BuildView(keys[i], cache)); }
            return views;
        }

        /// <summary> Hand this wiring to RM-55's resolver. Only the EXPLICIT asset list is registered -- no folder is
        /// walked -- and an asset's <see cref="ContentCandidate.passedValidation"/> is true only where the cache holds
        /// a PASS: an asset nobody has validated yet is not placeable, so the resolver refuses instead of placing
        /// something unchecked. This is why the foldout and the build can never disagree. </summary>
        public ContentCatalog BuildCatalog(ContentValidationCache cache)
        {
            ContentCatalog catalog = new ContentCatalog();
            RegisterInto(catalog, cache);
            return catalog;
        }

        /// <summary> The same wiring, added to a catalogue that already exists, for the case a settlement names several
        /// sets: a city with a buildings library and a street-furniture library resolves out of one catalogue, not two,
        /// and two sets that both name a category contribute to the same pool rather than one hiding the other.
        ///
        /// Split out of <see cref="BuildCatalog(ContentValidationCache)"/> rather than copied beside it, so the rule that
        /// only a cached PASS is placeable is stated in exactly one place. </summary>
        public void RegisterInto(ContentCatalog catalog, ContentValidationCache cache)
        {
            if (catalog == null) { return; }
            IList<string> keys = Categories();
            for (int k = 0; k < keys.Count; k++)
            {
                CategoryFoldoutView v = BuildView(keys[k], cache);
                for (int i = 0; i < v.assets.Length; i++)
                {
                    ContentCandidate c = new ContentCandidate();
                    c.assetId = v.assets[i].assetPath;
                    c.category = keys[k];
                    c.passedValidation = v.assets[i].state == ContentValidationState.Passed;
                    c.variantTags = null;
                    catalog.Register(c);
                }
            }
        }
    }
}

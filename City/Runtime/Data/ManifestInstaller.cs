#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How populated one manifest category is, reported honestly rather than assumed. POPULATED means it
    /// has content and every asset passed RM-48; PARTIAL means it has content but some of it is unchecked or
    /// failed; EMPTY means the author has supplied nothing. OUT-OF-SCOPE-EMPTY is EMPTY on a row a build never
    /// requires (rail under Q-15, or a documented out-of-scope row), so a completeness check does not treat it as a
    /// gap. There is deliberately no "filled with a placeholder" state -- that state does not exist in this tool.
    /// </summary>
    public enum ManifestCategoryHealth
    {
        Empty = 0,
        Partial = 1,
        Populated = 2,
        OutOfScopeEmpty = 3
    }

    /// <summary> One manifest category paired with what the content set actually holds for it: the count of assets,
    /// how many passed RM-48, and the resulting health. Built from RM-56's own foldout view, so the number the
    /// report shows is the number the inspector shows -- there is no second source of truth. </summary>
    public struct ManifestCategoryStatus
    {
        public ManifestCategory category;
        public ManifestCategoryHealth health;
        public int assetCount;
        public int passedCount;
        public int failedCount;
        public int uncheckedCount;

        /// <summary> A one-line, culture-invariant summary a report or console can print verbatim. </summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(category.categoryId).Append(": ").Append(HealthWord(health));
            sb.Append(" (").Append(assetCount.ToString(CultureInfo.InvariantCulture)).Append(" assets, ");
            sb.Append(passedCount.ToString(CultureInfo.InvariantCulture)).Append(" passed, ");
            sb.Append(failedCount.ToString(CultureInfo.InvariantCulture)).Append(" failed, ");
            sb.Append(uncheckedCount.ToString(CultureInfo.InvariantCulture)).Append(" unchecked)");
            return sb.ToString();
        }

        private static string HealthWord(ManifestCategoryHealth h)
        {
            switch (h)
            {
                case ManifestCategoryHealth.Populated: return "POPULATED";
                case ManifestCategoryHealth.Partial: return "PARTIAL";
                case ManifestCategoryHealth.OutOfScopeEmpty: return "EMPTY (out of scope)";
                default: return "EMPTY";
            }
        }
    }

    /// <summary> Turns the RM-77 manifest into an RM-56 content set and reads its state back honestly. It does NOT
    /// invent content: EnsureAllCategories creates one EMPTY entry per manifest row so every promised category is
    /// visible in RM-56's foldouts, and Report says truthfully whether each is populated, partial or empty. A build
    /// that needs an empty required category is refused BY NAME through the ordinary RM-55 resolver -- ResolveOrRefuse
    /// is a thin pass-through, so there is exactly one refusal mechanism, not a second copy here. </summary>
    public static class ManifestInstaller
    {
        /// <summary> Ensures every manifest category exists as an entry in the set, creating an EMPTY entry for any
        /// that is missing (RM-56's own <see cref="CategoryContentEntry.Empty"/>). Existing entries -- an author's
        /// filled folders -- are left untouched. Returns how many empty entries were added. Deterministic: the
        /// manifest order is fixed. </summary>
        public static int EnsureAllCategories(CategoryContentSet set)
        {
            if (set == null) { return 0; }
            int added = 0;
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            for (int i = 0; i < all.Count; i++)
            {
                CategoryContentEntry existing;
                if (!set.TryGetEntry(all[i].categoryId, out existing))
                {
                    set.SetEntry(CategoryContentEntry.Empty(all[i].categoryId));
                    added++;
                }
            }
            return added;
        }

        /// <summary> The health of every manifest category, read from RM-56's foldout view so the numbers match the
        /// inspector exactly. Out-of-scope and rail (Q-15) rows that are empty report OutOfScopeEmpty, so a
        /// completeness check does not count them as gaps in a road-only build. </summary>
        public static List<ManifestCategoryStatus> Report(CategoryContentSet set, ContentValidationCache cache)
        {
            List<ManifestCategoryStatus> report = new List<ManifestCategoryStatus>();
            IReadOnlyList<ManifestCategory> all = PrefabManifest.Categories;
            for (int i = 0; i < all.Count; i++)
            {
                ManifestCategoryStatus status = new ManifestCategoryStatus();
                status.category = all[i];
                if (set != null && set.TryGetEntry(all[i].categoryId, out _))
                {
                    CategoryFoldoutView view = set.BuildView(all[i].categoryId, cache);
                    status.assetCount = view.count;
                    status.passedCount = view.passedCount;
                    status.failedCount = view.failedCount;
                    status.uncheckedCount = view.uncheckedCount;
                }
                status.health = HealthOf(status, all[i].scope);
                report.Add(status);
            }
            return report;
        }

        /// <summary> Derives health from the counts: POPULATED only when there is content and all of it passed
        /// RM-48; PARTIAL when there is content but some is failed or unchecked; EMPTY when there is none -- and
        /// EMPTY on a non-in-scope row reports as OutOfScopeEmpty so it is never mistaken for a missing promise.
        /// </summary>
        private static ManifestCategoryHealth HealthOf(ManifestCategoryStatus s, ManifestScope scope)
        {
            if (s.assetCount <= 0)
            {
                return scope == ManifestScope.InScope
                    ? ManifestCategoryHealth.Empty : ManifestCategoryHealth.OutOfScopeEmpty;
            }
            if (s.failedCount == 0 && s.uncheckedCount == 0 && s.passedCount == s.assetCount)
            {
                return ManifestCategoryHealth.Populated;
            }
            return ManifestCategoryHealth.Partial;
        }

        /// <summary> Resolves one manifest category against a catalog, or returns RM-55's named refusal. This is a
        /// pass-through to <see cref="ContentResolver.Resolve(ContentCatalog, string, string, ulong)"/> so an empty
        /// required category refuses by its own id (reason CategoryEmpty) through the one resolver the whole tool
        /// uses -- never a fallback, never a primitive, never a substitute from another category. </summary>
        public static ContentResolution ResolveOrRefuse(ContentCatalog catalog, ManifestCategory category,
            string requester, ulong citySeed)
        {
            return ContentResolver.Resolve(catalog, category.categoryId, requester, citySeed);
        }
    }
}

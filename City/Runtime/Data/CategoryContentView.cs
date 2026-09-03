#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What is known about one asset's standing against RM-48's contract. <see cref="NotYetChecked"/> is a
    /// real, distinct state and is never drawn as a pass: an asset nobody has validated must not look approved.
    /// </summary>
    public enum ContentValidationState
    {
        NotYetChecked = 0,
        Passed = 1,
        Failed = 2
    }


    /// <summary> One row of a category's foldout: the asset, the name an author reads, its validation standing and,
    /// when it failed, RM-48's own specific reason (pivot, scale, shader pipeline, missing LOD) so the fix is
    /// visible where the author's attention already is. </summary>
    public struct CategoryAssetView
    {
        public string assetPath;      // project-relative
        public string displayName;
        public ContentValidationState state;
        public string reason;         // empty unless state == Failed

        /// <summary> The row as one line, for the report and for a text-mode listing. </summary>
        public string Describe()
        {
            string mark = state == ContentValidationState.Passed ? "ok"
                : (state == ContentValidationState.Failed ? "FAIL" : "unchecked");
            return string.IsNullOrEmpty(reason)
                ? string.Format("[{0}] {1}", mark, assetPath)
                : string.Format("[{0}] {1} -- {2}", mark, assetPath, reason);
        }
    }


    /// <summary> Why a category has nothing to show. The three cases must LOOK different, because "I never set this
    /// up" and "I set it up and the folder is empty" send an author to different places. RM-56's gate demands an
    /// empty category be visibly empty rather than appear configured. </summary>
    public enum CategoryEmptyKind
    {
        /// <summary> Not empty: the category has at least one resolved asset. </summary>
        NotEmpty = 0,

        /// <summary> Nothing at all: no source folder named, no asset referenced. </summary>
        NothingConfigured = 1,

        /// <summary> A source folder is named but no asset has been taken from it yet -- the author must rescan,
        /// or the folder genuinely holds nothing. </summary>
        FolderNamedButNoAssets = 2
    }


    /// <summary> Everything one category's foldout displays, computed once and drawn from. Assets are in a stable
    /// order (path-sorted) so the list does not reshuffle between repaints. </summary>
    public struct CategoryFoldoutView
    {
        public string category;
        public string sourceFolder;      // project-relative, empty when none is named
        public bool sourceFolderNamed;
        public CategoryAssetView[] assets;
        public int count;
        public int passedCount;
        public int failedCount;
        public int uncheckedCount;
        public CategoryEmptyKind emptyKind;

        /// <summary> True when the category resolves to nothing. </summary>
        public bool IsEmpty { get { return count == 0; } }

        /// <summary> The foldout's own header line: the category, where its content comes from, and the counts --
        /// the answer to "where will my cell towers come from?" without leaving the inspector. </summary>
        public string Headline()
        {
            string where = sourceFolderNamed ? sourceFolder : "no source folder";
            if (count == 0)
            {
                string why = emptyKind == CategoryEmptyKind.FolderNamedButNoAssets
                    ? "EMPTY -- folder named but no assets taken from it (rescan)"
                    : "EMPTY -- nothing configured";
                return string.Format("{0}: {1}  ({2})", category, why, where);
            }
            return string.Format("{0}: {1} asset(s) -- {2} ok, {3} failed, {4} unchecked  ({5})",
                category, count, passedCount, failedCount, uncheckedCount, where);
        }
    }
}

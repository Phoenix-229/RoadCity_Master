#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-109. B-3 and B-4. How this tool's legacy inspectors lay out label text: wrapped paragraphs and a
    /// label column wide enough for the labels actually drawn -- without leaving either setting behind for the rest of
    /// the editor to inherit.
    ///
    /// The two faults were halves of one visible bug. In the Road inspector,
    /// `Gizmo new Node preview Color:` rendered on two lines and the second line painted over
    /// `Gizmo Selected Color:` underneath it. Both halves had to be there for that to happen:
    ///
    /// B-3, the wrap that escaped. Nine assignments across six files wrote `wordWrap = true` into
    /// `EditorStyles.label`, `EditorStyles.miniLabel` and -- in the wizard -- `GUI.skin.label`, the ACTIVE SKIN.
    /// None of those is this tool's object; each is one instance the editor owns and hands to every window that asks.
    /// Nothing set them back, so selecting a road once rewrapped label text in Unity's own inspectors and in other
    /// packages' windows until the next domain reload. Four of the nine sat inside an `Init()` behind an
    /// `isInitialized` flag and therefore ran ONCE -- which is all a permanent leak needs.
    ///
    /// B-4, the column that was never sized. `EditorGUIUtility.labelWidth` was set nowhere in the tool: zero hits
    /// across every `.cs`. So every field row used Unity's default column of roughly 150px while the widest prefix
    /// label the tool draws is `"Align objects to road rotation: "` at 32 characters, with
    /// `"Gizmo new Node preview Color: "` -- the one in the screenshot -- at 30. A `ColorField` reserves ONE
    /// `singleLineHeight` row; its prefix label needed two; the overflow was drawn outside the reserved rect, on top of
    /// the next control. Widening the column is what stops the second line existing, so this is the half that changes
    /// what the screenshot shows.
    ///
    /// Why the wrap is kept rather than removed. The textbook fix is an owned `GUIStyle` passed explicitly at each
    /// label. Measured before choosing: those six files hold 245 `EditorGUILayout.LabelField` calls across bodies of
    /// 1265-2268 lines, and the ones that must keep wrapping are the long explanatory paragraphs an author reads --
    /// several of them 200 to 438 characters. A sweep of 245 sites that missed some would clip help text with no
    /// compiler complaint. The defect was never that labels wrap; it is that the wrap ESCAPED. So the wrap stays and
    /// its lifetime is bounded.
    ///
    /// Where this is called from is part of the fix, not tidying. <see cref="Apply"/> belongs in the per-repaint path
    /// (`OnInspectorGUI` / `OnGUI`), and the four `Init()` calls were MOVED there. From `Init()` a call would run once:
    /// enough to leak, and -- now that the settings are handed back -- not enough to keep this tool's own paragraphs
    /// wrapped past the first repaint.
    ///
    /// The one residue, stated rather than papered over: a NON-tool component drawn below one of ours in the same
    /// inspector pass still sees both settings for that one pass. Bounding it tighter would need to know which editor
    /// draws last in a pass, which Unity does not expose. In practice the components under ours are ours, and one pass
    /// is a repaint, not a session.
    ///
    /// Why the ACTIVE SKIN is no longer touched at all. The first version of this file also set
    /// `GUI.skin.label.wordWrap` and put it back with the rest, and that was wrong twice over. It threw --
    /// `ArgumentException: You can only call GUI functions from inside OnGUI`, from `GUI.get_skin` -- because the
    /// restore runs on `delayCall`, which is not a GUI callback, and reading `GUI.skin` outside one is illegal. Worse
    /// than the exception: `GUI.skin` is per-pass state that Unity sets at the top of each `OnGUI` and can differ
    /// between windows, so a restore on a later tick would have written this tool's captured value onto whichever skin
    /// happened to be current then -- possibly a skin this tool never touched. The mutation also turned out to serve
    /// no caller. `GUILayout.Label` with no style argument is the only control that reads `GUI.skin.label`, and of the
    /// six files that call `Apply` the only unstyled ones are seven short section captions in `RoadEditor` --
    /// `"Materials:"`, `"Physics materials defaults:"` and the like, none of which can wrap. The two long
    /// `GUILayout.Label` texts in the tool, `RoadEditor.cs:440` and `RoadSystemEditor.cs:105`, already pass an
    /// explicit style. So the skin handling was removed rather than repaired: nothing here writes to `GUI.skin`, and
    /// <see cref="InspectorStyleOwnershipGate"/> case 5 asserts that it never starts again. A `GUILayout.Label` added
    /// later that does need wrapping passes `EditorStyles.wordWrappedLabel`, as the City scene tools already do.
    /// Everything this file still sets -- the two `EditorStyles` flags and `EditorGUIUtility.labelWidth` -- is legal to
    /// read and write outside a GUI callback, which is what makes the deferred restore sound. </summary>
    public static class InspectorTextLayout
    {
        #region "The editor's own values"
        /// <summary> Captured once per domain load, from the settings as the editor made them, BEFORE anything here has
        /// written to them. Captured lazily rather than in a static constructor, because `EditorStyles` is only valid
        /// once the editor's skin exists and a static constructor can run before that. </summary>
        private static bool captured;
        private static bool labelWrapWas;
        private static bool miniLabelWrapWas;
        private static float labelWidthWas;

        private static bool restoreArmed;
        #endregion


        #region "Apply"
        /// <summary> Lay this tool's text out for the drawing pass in progress: wrapped paragraphs, and a label column
        /// wide enough for the widest label the tool draws. Call it at the top of an `OnInspectorGUI` or `OnGUI`.
        ///
        /// There is no matching End to forget, and an early `return` further down the body cannot strand the editor in
        /// this tool's settings -- which is the other half of why a scope object was not used on bodies this shape:
        /// wrapping three legacy files of 1265-2268 lines in a `using` block would mean reindenting all of them. </summary>
        public static void Apply()
        {
            if (EditorStyles.label == null) { return; }   // no skin yet: nothing to set, nothing to give back

            if (!captured)
            {
                labelWrapWas = EditorStyles.label.wordWrap;
                miniLabelWrapWas = EditorStyles.miniLabel.wordWrap;
                labelWidthWas = EditorGUIUtility.labelWidth;
                captured = true;
            }

            EditorStyles.label.wordWrap = true;
            EditorStyles.miniLabel.wordWrap = true;

            WidenLabelColumn();

            if (restoreArmed) { return; }
            restoreArmed = true;

            // A named method, not an anonymous delegate. `delayCall` is a plain multicast field, and `+=` of a lambda
            // cannot be removed by a later `-=`; the named form stays removable and stays one subscription.
            EditorApplication.delayCall -= RestoreNow;
            EditorApplication.delayCall += RestoreNow;
        }


        /// <summary> Hand the shared settings back. Armed by <see cref="Apply"/> to run on the tick after the pass that
        /// asked for them, so the next window to draw sees the editor's own values.
        ///
        /// Everything touched here is legal outside a GUI callback, and that is a constraint on this method, not an
        /// observation about it: `delayCall` is not `OnGUI`, so a `GUI.*` member read here throws
        /// `ArgumentException: You can only call GUI functions from inside OnGUI`. The first version of this file read
        /// `GUI.skin` and did exactly that. `EditorStyles.label`, `EditorStyles.miniLabel` and
        /// `EditorGUIUtility.labelWidth` are plain objects and a plain static, safe to read and write off-pass once the
        /// skin exists -- which the `EditorStyles.label == null` guard below is what establishes. **Nothing that this
        /// method cannot legally put back may be set in <see cref="Apply"/>.**
        ///
        /// Public because the restore is the half of this that can actually be wrong, and a private one could only be
        /// checked by reading the source. <see cref="InspectorStyleOwnershipGate"/> calls it to prove that what Apply
        /// sets, this puts back -- exactly, and including the values captured before the tool ever wrote to them. Calling
        /// it early is safe: the next repaint re-applies. </summary>
        public static void RestoreNow()
        {
            EditorApplication.delayCall -= RestoreNow;
            restoreArmed = false;

            if (!captured) { return; }
            if (EditorStyles.label == null) { return; }

            EditorStyles.label.wordWrap = labelWrapWas;
            EditorStyles.miniLabel.wordWrap = miniLabelWrapWas;
            EditorGUIUtility.labelWidth = labelWidthWas;
        }
        #endregion


        #region "The label column (B-4)"
        /// <summary> The widest prefix label this tool actually draws, found by sweeping every labelled field call in
        /// the editor folder rather than guessed: `"Align objects to road rotation: "` at 32 characters. The column is
        /// MEASURED from it instead of being given a pixel number, so it stays right when the editor's font or DPI
        /// scaling differs from this machine's -- a hardcoded 210px is correct on one display and wrong on the next.
        ///
        /// Public so <see cref="InspectorStyleOwnershipGate"/> can re-run that sweep and fail if a longer prefix label
        /// is added later: the yardstick going stale is exactly how a sized column quietly becomes a narrow one again.
        /// </summary>
        public const string WidestLabel = "Align objects to road rotation: ";

        /// <summary> Measured once per domain load. `CalcSize` is cheap but not free, and this runs on every repaint of
        /// every inspector in the tool; the metrics it depends on do not change without a reload. </summary>
        private static float measured;

        /// <summary> Give the label column room for the labels this tool draws, and leave the control on the row usable.
        ///
        /// Only ever WIDER, never narrower: Unity sizes the default column from the inspector's width, and on a narrow
        /// inspector its choice is the better one. The cap is what keeps this honest -- on a docked, narrow inspector
        /// there is no width at which a 32-character label and a colour swatch both fit, so the control's minimum wins
        /// and the label wraps, which is the case wrapping exists for. </summary>
        private static void WidenLabelColumn()
        {
            if (measured <= 0f)
            {
                measured = EditorStyles.label.CalcSize(new GUIContent(WidestLabel)).x + 8f;
            }

            // `currentViewWidth` is the inspector's width during a draw pass. It can be 0 or nonsense outside one, and a
            // column computed from that would be worse than Unity's default, so bail rather than guess.
            float view = EditorGUIUtility.currentViewWidth;
            if (view <= 1f) { return; }

            // 120px is a usable colour swatch, object field or slider; below that the row stops being editable, which
            // is a worse fault than a wrapped label.
            float room = view - 120f;
            if (room < 100f) { return; }

            float wanted = measured < room ? measured : room;
            if (wanted > EditorGUIUtility.labelWidth) { EditorGUIUtility.labelWidth = wanted; }
        }
        #endregion
    }
}
#endif

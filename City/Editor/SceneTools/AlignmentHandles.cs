#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-75's authoring surface: the corners an author drags, the curve the geometry makes of them, and the
    /// words that say what each corner became.
    ///
    /// It is the counterpart of <see cref="SuitabilityOverlay"/> and shares its shape -- it subscribes to the scene GUI
    /// only while shown, it draws a legend panel after the world handles, it caches heights through the terrain reader
    /// rather than sampling per frame -- but it differs in the one way that matters: this tool WRITES. The overlay draws
    /// advice about ground; these handles move the author's own corners. So every difference from the overlay is a
    /// difference in service of that: one undo step per drag, and never a byte touched outside the point being dragged.
    ///
    /// The three things on screen each answer a question the author would otherwise have to guess. The RIBBON, coloured
    /// by curvature, shows where the road is turning hardest -- the eye cannot read a radius off a plan line but it reads
    /// a colour instantly. The HANDLES are the authored corners, and only the corners: a road is designed at its
    /// intersections, so those are what an author grabs, and the hundred fitted nodes between them are consequences, not
    /// controls. The PANEL names, for each corner, the radius it got and whether the speed can hold it -- because a
    /// tight bend looks like a gentle one from above and only a sentence tells them apart.
    ///
    /// GUIDED PLACEMENT is opt-in and lives entirely in <see cref="CorridorGuidance"/>. These handles ask it for a
    /// suggestion on every drag; when the plan's switch is off, that suggestion is the author's own position unchanged,
    /// so the snap is inert by construction and a project that never turns it on cannot feel it. </summary>
    public static class AlignmentHandles
    {
        private static HorizontalAlignment plan;
        private static RoadClass planClass;
        private static SuperelevationClass superelevation;
        private static Object owner;
        private static SuitabilityRasterClass map;
        private static CorridorPlan corridor;
        private static ITerrainSurfaceReader reader;

        private static AlignmentSolution solution;
        private static bool shown;
        private static string lastSuggestion = string.Empty;

        /// <summary> How many stations the ribbon is sampled at per metre of plan is not fixed; instead the ribbon walks
        /// the same fitted points the road is built from, so it can never disagree with the carriageway it sits above.
        /// This is the height it floats at, clear of the road surface. </summary>
        public const float RibbonLiftMetres = 1.0f;

        public static bool IsShown { get { return shown; } }
        public static HorizontalAlignment Plan { get { return plan; } }
        public static string LastSuggestion { get { return lastSuggestion; } }

        /// <summary> Hand the handles a plan to author, the class and speed table that resolve it, and the object that
        /// serializes it. The owner is the Undo target and nothing else: a drag records IT, so one press of undo returns
        /// the whole plan to where it was. A null owner still draws and still drags -- for a plan held in a plain field
        /// with no component behind it -- but without an undo entry, which is stated in the panel rather than hidden.
        /// </summary>
        public static void SetPlan(HorizontalAlignment authored, RoadClass cls, SuperelevationClass super, Object host)
        {
            plan = authored;
            planClass = cls;
            superelevation = super != null ? super : SuperelevationClass.Default();
            owner = host;
            Resolve();
            Repaint();
        }

        /// <summary> Hand the handles the corridor to be guided by. Optional and separate from the plan, exactly as in
        /// the overlay: an author solves a corridor once and drags many nodes against it, and a plan is advice about a
        /// map rather than part of one. Absent, guidance simply has nothing to snap to and says so. </summary>
        public static void SetCorridor(SuitabilityRasterClass surface, CorridorPlan solved, ITerrainSurfaceReader terrain)
        {
            map = surface;
            corridor = solved;
            reader = terrain;
            Repaint();
        }

        private static void Resolve()
        {
            solution = plan != null && plan.PointCount >= 2
                ? AlignmentResolver.Resolve(plan, planClass, superelevation)
                : null;
        }

        private static void Repaint()
        {
            if (shown) { SceneView.RepaintAll(); }
        }

        /// <summary> Start drawing. Idempotent, and subscribed here rather than at load so a project that never authors
        /// an alignment pays nothing for the tool. </summary>
        public static void Show()
        {
            if (shown) { return; }
            SceneView.duringSceneGui += OnSceneGui;
            shown = true;
            SceneView.RepaintAll();
        }

        public static void Hide()
        {
            if (!shown) { return; }
            SceneView.duringSceneGui -= OnSceneGui;
            shown = false;
            SceneView.RepaintAll();
        }

        [MenuItem("Window/Road City Master/Alignment/Show the alignment handles", false, 140)]
        public static void ShowFromMenu() { Show(); }

        [MenuItem("Window/Road City Master/Alignment/Show the alignment handles", true)]
        public static bool ShowFromMenuValidate() { return !shown; }

        [MenuItem("Window/Road City Master/Alignment/Hide the alignment handles", false, 141)]
        public static void HideFromMenu() { Hide(); }

        [MenuItem("Window/Road City Master/Alignment/Hide the alignment handles", true)]
        public static bool HideFromMenuValidate() { return shown; }

        /// <summary> One scene repaint: the curvature ribbon along the fitted line, a draggable handle on each authored
        /// corner, then the legend panel on top. The panel comes last for the same reason it does in the overlay -- it
        /// is a screen-space rectangle, and a world handle drawn after it would paint over it. </summary>
        private static void OnSceneGui(SceneView view)
        {
            if (plan == null) { return; }
            DrawRibbon();
            DrawHandles();
            DrawPanel(view);
        }

        /// <summary> The fitted line, each segment tinted by how hard the road is turning there. It walks the SAME points
        /// the road is built from, so the ribbon can never draw a curve the carriageway does not have. Colour runs green
        /// where the road is straight to red at the class's tightest allowed radius; a bend tighter than that pins at red
        /// rather than running off the scale, because "as red as it gets" is the right reading for "too tight". </summary>
        private static void DrawRibbon()
        {
            if (solution == null) { return; }
            AlignmentFit fit = AlignmentFitter.Fit(solution, plan.chordToleranceMetres);
            if (fit.PointCount < 2) { return; }

            double reference = ReferenceCurvature();
            Color was = Handles.color;
            for (int i = 1; i < fit.points.Count; i++)
            {
                double midStation = (fit.stationsMetres[i - 1] + fit.stationsMetres[i]) * 0.5;
                double curvature = System.Math.Abs(AlignmentResolver.CurvatureAtStation(solution, midStation));
                float t = reference > 0.0 ? (float)Clamp01(curvature / reference) : 0f;

                Vector3 a = Lift(fit.points[i - 1]);
                Vector3 b = Lift(fit.points[i]);
                Handles.color = CurvatureColour(t);
                Handles.DrawAAPolyLine(6f, new Vector3[] { a, b });
            }
            Handles.color = was;
        }

        /// <summary> A handle on each authored corner -- and only the corners. Dragging one records the owner for undo,
        /// asks guided placement for a suggestion, and writes the result back into that ONE point. Nothing else is
        /// touched: the fitted nodes between corners are recomputed from the plan, never stored, so there is nothing
        /// there to corrupt. </summary>
        private static void DrawHandles()
        {
            for (int i = 0; i < plan.points.Count; i++)
            {
                AlignmentPoint point = plan.points[i];
                Vector3 world = new Vector3(point.xMetres, SurfaceHeight(point.xMetres, point.zMetres), point.zMetres);
                float size = HandleUtility.GetHandleSize(world) * 0.15f;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (!EditorGUI.EndChangeCheck()) { continue; }

                GuidanceResult suggestion = CorridorGuidance.Suggest(plan, map, corridor, moved.x, moved.z);
                lastSuggestion = "corner " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + suggestion.sentence;

                if (owner != null) { Undo.RecordObject(owner, "Move alignment node"); }
                point.xMetres = (float)suggestion.xMetres;
                point.zMetres = (float)suggestion.zMetres;
                if (owner != null) { EditorUtility.SetDirty(owner); }
                Resolve();
            }
        }

        /// <summary> The panel: what plan is loaded, whether guided placement is on, the last suggestion a drag drew,
        /// and a line per corner from the resolver's own readout. The guidance line is stated whether or not it is on,
        /// because "off" is the safe default an author should be able to confirm at a glance. </summary>
        private static void DrawPanel(SceneView view)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(8f, 8f, 460f, 300f), GUI.skin.box);

            GUILayout.Label("Alignment handles", EditorStyles.boldLabel);
            GUILayout.Label(plan.SummaryLine());
            GUILayout.Label(plan.guidedPlacementEnabled
                ? "guided placement ON, snapping within " + plan.guidedSnapToleranceMetres.ToString("0.#",
                    CultureInfo.InvariantCulture) + " m of the corridor"
                : "guided placement OFF -- the node stays exactly where you put it");
            if (owner == null)
            {
                GUILayout.Label("no serialized owner set, so a drag has no undo entry");
            }
            if (lastSuggestion.Length > 0) { GUILayout.Label(lastSuggestion); }

            if (solution != null && solution.curves != null)
            {
                for (int i = 0; i < solution.curves.Count && i < 8; i++)
                {
                    GUILayout.Label(AlignmentResolver.CurveReadout(solution.curves[i]), EditorStyles.wordWrappedLabel);
                }
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        /// <summary> The curvature the ribbon treats as fully red: the sharpest the class is meant to be driven at this
        /// design speed. Falls back to a 50 m radius when no class is set, so the ribbon still reads on a bare plan.
        /// </summary>
        private static double ReferenceCurvature()
        {
            double minRadius = AlignmentResolver.MinimumRadiusMetres(plan.designSpeedKmh, planClass, superelevation);
            if (minRadius <= 0.0) { minRadius = 50.0; }
            return 1.0 / minRadius;
        }

        private static Color CurvatureColour(float t)
        {
            Color easy = new Color(0.25f, 0.75f, 0.30f, 0.95f);
            Color middling = new Color(0.95f, 0.85f, 0.25f, 0.95f);
            Color hard = new Color(0.85f, 0.20f, 0.15f, 0.95f);
            return t < 0.5f ? Color.Lerp(easy, middling, t * 2f) : Color.Lerp(middling, hard, (t - 0.5f) * 2f);
        }

        private static Vector3 Lift(Vector3 fitted)
        {
            return new Vector3(fitted.x, SurfaceHeight(fitted.x, fitted.z) + RibbonLiftMetres, fitted.z);
        }

        private static float SurfaceHeight(float x, float z)
        {
            return reader != null ? reader.SampleHeight(x, z) : 0f;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0.0) { return 0.0; }
            return value >= 1.0 ? 1.0 : value;
        }
    }
}
#endif

#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> Which single quantity the overlay is drawing. ONE at a time, never a blend.
    ///
    /// RM-74's requirement that a factor can be shown on its own is not a convenience -- it is what makes the map
    /// arguable. A composite surface can only be agreed or disagreed with as a whole, whereas an author who can put
    /// gradient on the screen by itself, then earthwork by itself, can see which of the two is driving a detour and
    /// change the weight that is actually responsible. <see cref="Binding"/> is the same idea from the other side: it
    /// colours each cell by WHICH factor decided it, which is the fastest way to read a whole map at once. </summary>
    public enum SuitabilityDisplay
    {
        Cost = 0,
        Slope = 1,
        Earthwork = 2,
        Binding = 3,
        Forbidden = 4,
        Corridor = 5
    }


    /// <summary> RM-74's overlay: the cost surface on the ground, with a legend and a cursor readout.
    ///
    /// Three things are on screen and each exists for a reason § 10.24 already established elsewhere in this tool. The
    /// COLOURED CELLS show where the map thinks a road belongs. The LEGEND states the weights, the road class and which
    /// factors had a source at all, because a five-factor overlay drawn from two factors is indistinguishable from a
    /// real one and an author must not have to guess. The CURSOR READOUT names the binding constraint of the cell under
    /// the mouse in words with its measurement in it -- "slope 14 %, above the 8 % maximum for this class" -- because a
    /// colour tells an author that ground is bad and only a sentence tells them why, or which number to change.
    ///
    /// It draws and it reads. It never writes: no node, no edge, no terrain, no asset, and no snapping of anything the
    /// author has drawn. The overlay subscribes to the scene GUI only while it is shown, so a project that never opens
    /// it pays nothing for it.
    ///
    /// Deliberately EDITOR code (like RM-09's road builder next to it): the analysis itself is pure and lives in
    /// <see cref="SuitabilityMapBuilder"/> and <see cref="CorridorSolver"/> under Runtime, where a gate can test it
    /// headlessly. Only the drawing is here. </summary>
    public static class SuitabilityOverlay
    {
        private static SuitabilityRasterClass map;
        private static CorridorPlan plan;
        private static RoadClass drawClass;
        private static float[] drawHeightMetres = new float[0];
        private static string legend = string.Empty;
        private static SuitabilityDisplay display = SuitabilityDisplay.Cost;
        private static bool shown;
        private static string cursorLine = string.Empty;

        /// <summary> The most cells the overlay will draw in one scene view. AUTHORED: a world-sized raster at 16 m
        /// cells runs to millions of quads, and an overlay that stalls the editor is an overlay an author turns off and
        /// never sees again. Past this count the overlay thins its stride and SAYS SO in the legend, rather than
        /// silently drawing a different map from the one that was solved. </summary>
        public const int MaxDrawnCells = 40000;

        public static bool IsShown { get { return shown; } }
        public static SuitabilityDisplay Display { get { return display; } }
        public static string Legend { get { return legend; } }
        public static string CursorLine { get { return cursorLine; } }
        public static SuitabilityRasterClass Map { get { return map; } }

        /// <summary> Hand the overlay a solved surface. The heights it draws at are sampled ONCE here, through the same
        /// reader the map was built from, rather than per frame: a per-frame sample would make the overlay's cost
        /// depend on the terrain's sampling cost, and in connected mode that means asking a provider thousands of
        /// questions per repaint. A null reader is allowed and simply draws flat at zero. </summary>
        public static void SetMap(SuitabilityRasterClass surface, string surfaceLegend, RoadClass cls,
            ITerrainSurfaceReader reader)
        {
            map = surface;
            legend = surfaceLegend != null ? surfaceLegend : string.Empty;
            drawClass = cls;
            plan = null;
            cursorLine = string.Empty;
            CacheDrawHeights(reader);
            Repaint();
        }

        /// <summary> Hand the overlay a corridor to draw over the surface. Separate from the surface because the two
        /// have different lifetimes -- an author re-solves a corridor between new endpoints many times against one
        /// map -- and because a plan is advice about a map, not part of it. </summary>
        public static void SetPlan(CorridorPlan corridorPlan)
        {
            plan = corridorPlan;
            Repaint();
        }

        public static void SetDisplay(SuitabilityDisplay which)
        {
            display = which;
            Repaint();
        }

        private static void CacheDrawHeights(ITerrainSurfaceReader reader)
        {
            int count = SuitabilityRaster.CellCount(map);
            drawHeightMetres = new float[count];
            if (map == null || count == 0) { return; }
            for (int cz = 0; cz < map.depthCells; cz++)
            {
                for (int cx = 0; cx < map.widthCells; cx++)
                {
                    int index = SuitabilityRaster.Index(map, cx, cz);
                    if (index < 0) { continue; }
                    float x = (float)SuitabilityRaster.CellCentreXMetres(map, cx);
                    float z = (float)SuitabilityRaster.CellCentreZMetres(map, cz);
                    drawHeightMetres[index] = reader != null ? reader.SampleHeight(x, z) : 0f;
                }
            }
        }

        private static void Repaint()
        {
            if (shown) { SceneView.RepaintAll(); }
        }

        /// <summary> Start drawing. Subscribing here rather than at load is the difference between a feature that costs
        /// nothing until it is asked for and one that draws on every scene repaint in every project forever. Idempotent:
        /// showing twice does not subscribe twice, which would double every quad and halve the frame rate. </summary>
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
            cursorLine = string.Empty;
            SceneView.RepaintAll();
        }

        // RM-108 removed a "Corridor" submenu of ten rows from Window/Road City Master: show, hide, their two
        // validators, and one row per display mode. Every one of them was a wrapper of the form `Show()` or
        // `SetDisplay(SuitabilityDisplay.X)` with a [MenuItem] on it and no other caller, and the whole submenu was
        // unreachable in the sense that matters: this overlay draws NOTHING until something calls SetMap, and no menu
        // row can call it, so an author who found "Show the suitability overlay" got an empty scene view and no
        // explanation.
        //
        // B-22 is the correction. The sentence that stood here said the controls "live in the RoadSystem inspector
        // now"; they did not, because RM-108 deleted the rows and described the replacement instead of writing it, so
        // for the whole interval between the two tasks Show(), Hide() and SetDisplay() had no caller anywhere in the
        // tool. They have one now: the RoadSystem inspector draws a "Corridor" section with a show/hide toggle over
        // IsShown, one popup over SuitabilityDisplay -- the ten rows really do collapse to one control, because the
        // overlay draws exactly one mode at a time -- and CursorLine as a read-out. Each popup row is worded by
        // DisplayName below, so the six sentences are not spelled a second time in the inspector.
        //
        // The precondition is honest rather than cautious: that section is drawn only while Map is non-null, because
        // the paragraph above is still true and a control that switches on a tool drawing nothing is the same defect
        // as the menu row was. And it is still true that nothing calls SetMap -- the bake computes the raster, no code
        // hands it here. That missing feed is B-28, not this file. The drawing is unchanged by either task.

        /// <summary> The 0..1 quantity a cell contributes to the current display, or a negative number when this cell
        /// has nothing to show in this mode (an off-corridor cell in corridor mode, say) and should not be drawn at all.
        ///
        /// Gradient and earthwork are shown against the SAME references the cost surface used -- the class's own maximum
        /// gradient and the bench volume at that maximum -- rather than against the extremes present in this particular
        /// raster. Auto-scaling to the data is the usual choice and it is the wrong one here: it would make a gentle
        /// valley look as red as a mountain range purely because nothing steeper was in frame, and two maps of
        /// neighbouring areas would then be uncomparable. </summary>
        public static double ValueFor(SuitabilityRasterClass surface, CorridorPlan corridor, int index,
            SuitabilityDisplay which, double maxGradientFraction, double referenceEarthworkM3)
        {
            if (surface == null || index < 0 || index >= surface.cost.Length) { return -1.0; }
            switch (which)
            {
                case SuitabilityDisplay.Cost:
                    return surface.cost[index];
                case SuitabilityDisplay.Slope:
                    if (maxGradientFraction <= 0.0) { return -1.0; }
                    return Clamp01(surface.slopeFraction[index] / maxGradientFraction);
                case SuitabilityDisplay.Earthwork:
                    if (referenceEarthworkM3 <= 0.0) { return -1.0; }
                    return Clamp01(surface.earthworkVolumeM3[index] / referenceEarthworkM3);
                case SuitabilityDisplay.Binding:
                    return surface.binding[index];
                case SuitabilityDisplay.Forbidden:
                    return surface.forbidden[index] ? 1.0 : -1.0;
                case SuitabilityDisplay.Corridor:
                    if (corridor == null || corridor.inCorridor == null || index >= corridor.inCorridor.Length)
                    { return -1.0; }
                    return corridor.inCorridor[index] ? 1.0 : -1.0;
                default:
                    return -1.0;
            }
        }

        /// <summary> Colour for a drawn cell. Green through amber to red for a continuous quantity, because that is the
        /// convention every suitability map an author has ever seen uses and inventing a different one buys nothing; a
        /// fixed palette for the binding-factor mode, since those values are categories and a ramp across categories
        /// would imply an order they do not have. Forbidden ground is drawn in the same red as maximum cost, with a
        /// stronger alpha, so it reads as "the worst case, and different in kind". </summary>
        public static Color ColourFor(SuitabilityDisplay which, double value)
        {
            if (which == SuitabilityDisplay.Binding)
            {
                switch ((SuitabilityFactor)(int)value)
                {
                    case SuitabilityFactor.Slope: return new Color(0.85f, 0.25f, 0.20f, 0.35f);
                    case SuitabilityFactor.Earthwork: return new Color(0.90f, 0.60f, 0.15f, 0.35f);
                    case SuitabilityFactor.Water: return new Color(0.20f, 0.45f, 0.90f, 0.35f);
                    case SuitabilityFactor.LandUse: return new Color(0.65f, 0.30f, 0.75f, 0.35f);
                    case SuitabilityFactor.NetworkDistance: return new Color(0.45f, 0.45f, 0.45f, 0.35f);
                    default: return new Color(0.30f, 0.75f, 0.35f, 0.20f);
                }
            }
            if (which == SuitabilityDisplay.Forbidden) { return new Color(0.85f, 0.15f, 0.15f, 0.45f); }
            if (which == SuitabilityDisplay.Corridor) { return new Color(0.20f, 0.70f, 0.90f, 0.35f); }

            float t = (float)Clamp01(value);
            Color easy = new Color(0.25f, 0.75f, 0.30f, 0.30f);
            Color middling = new Color(0.95f, 0.85f, 0.25f, 0.32f);
            Color hard = new Color(0.85f, 0.20f, 0.15f, 0.36f);
            return t < 0.5f ? Color.Lerp(easy, middling, t * 2f) : Color.Lerp(middling, hard, (t - 0.5f) * 2f);
        }

        private static double Clamp01(double value)
        {
            if (value <= 0.0) { return 0.0; }
            return value >= 1.0 ? 1.0 : value;
        }

        /// <summary> One scene repaint: the cells, the least-cost line, the cursor readout, then the legend on top.
        /// Order matters -- the legend is a screen-space panel and has to be drawn after the world-space handles or the
        /// handles paint over it. </summary>
        private static void OnSceneGui(SceneView view)
        {
            int stride = DrawStride();
            DrawCells(stride);
            DrawPath();
            UpdateCursor();
            DrawPanel(view, stride);
        }

        /// <summary> How many cells to step between drawn quads. One where the raster is small enough to draw whole;
        /// larger where it is not, chosen so the drawn count stays under <see cref="MaxDrawnCells"/>. </summary>
        public static int DrawStride()
        {
            int count = SuitabilityRaster.CellCount(map);
            if (count <= MaxDrawnCells) { return 1; }
            int stride = (int)Mathf.Ceil(Mathf.Sqrt((float)count / MaxDrawnCells));
            return stride < 1 ? 1 : stride;
        }

        private static void DrawCells(int stride)
        {
            if (map == null || SuitabilityRaster.CellCount(map) == 0) { return; }

            double maxGradient = drawClass != null ? drawClass.maxGradient : 0.0;
            double platform = SuitabilityMapBuilder.PlatformWidthMetres(drawClass);
            double referenceEarthwork = SuitabilityFactors.BenchVolumePerMetreM3(maxGradient, platform);

            float half = map.metresPerCell * 0.5f * stride;
            Vector3[] quad = new Vector3[4];
            Color outline = new Color(0f, 0f, 0f, 0f);

            for (int cz = 0; cz < map.depthCells; cz += stride)
            {
                for (int cx = 0; cx < map.widthCells; cx += stride)
                {
                    int index = SuitabilityRaster.Index(map, cx, cz);
                    if (index < 0) { continue; }
                    double value = ValueFor(map, plan, index, display, maxGradient, referenceEarthwork);
                    if (value < 0.0) { continue; }

                    float x = (float)SuitabilityRaster.CellCentreXMetres(map, cx);
                    float z = (float)SuitabilityRaster.CellCentreZMetres(map, cz);
                    float y = index < drawHeightMetres.Length ? drawHeightMetres[index] : 0f;
                    y += 0.25f;   // clear of the ground so the quad is not z-fighting the terrain it describes

                    quad[0] = new Vector3(x - half, y, z - half);
                    quad[1] = new Vector3(x + half, y, z - half);
                    quad[2] = new Vector3(x + half, y, z + half);
                    quad[3] = new Vector3(x - half, y, z + half);
                    Handles.DrawSolidRectangleWithOutline(quad, ColourFor(display, value), outline);
                }
            }
        }

        /// <summary> The least-cost line, drawn over whichever surface is showing. It is drawn as a POLYLINE through
        /// cell centres and nothing more: no handles to drag, no snapping, no control points an author could mistake for
        /// an editable spline. RM-74's output is advice, and a line you can grab looks like a road. </summary>
        private static void DrawPath()
        {
            if (map == null || plan == null || !plan.reached || plan.pathCells.Count < 2) { return; }
            Vector3[] points = new Vector3[plan.pathCells.Count];
            for (int i = 0; i < plan.pathCells.Count; i++)
            {
                int index = plan.pathCells[i];
                int cx = index % map.widthCells;
                int cz = index / map.widthCells;
                float y = index < drawHeightMetres.Length ? drawHeightMetres[index] : 0f;
                points[i] = new Vector3((float)SuitabilityRaster.CellCentreXMetres(map, cx), y + 0.6f,
                    (float)SuitabilityRaster.CellCentreZMetres(map, cz));
            }
            Color was = Handles.color;
            Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            Handles.DrawAAPolyLine(4f, points);
            Handles.color = was;
        }

        /// <summary> The cell under the pointer, and its sentence. The ground point comes from a physics ray against the
        /// scene's own colliders, which is the terrain the map describes; where nothing is hit -- an author looking at
        /// the sky, or a scene with no terrain collider -- the ray is met with the horizontal plane through the mapped
        /// ground instead, so the readout degrades to "outside the mapped area" rather than to nothing at all. </summary>
        private static void UpdateCursor()
        {
            Event current = Event.current;
            if (current == null || map == null) { return; }
            if (current.type != EventType.MouseMove && current.type != EventType.Repaint) { return; }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 point;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100000f)) { point = hit.point; }
            else
            {
                Plane ground = new Plane(Vector3.up, new Vector3(0f, MeanDrawHeight(), 0f));
                float distance;
                if (!ground.Raycast(ray, out distance)) { return; }
                point = ray.GetPoint(distance);
            }

            int cellX, cellZ;
            if (!SuitabilityRaster.CellAt(map, point.x, point.z, out cellX, out cellZ))
            {
                cursorLine = "outside the mapped area";
                return;
            }
            cursorLine = SuitabilityMapBuilder.ReadoutFor(map, cellX, cellZ, drawClass);
            if (current.type == EventType.MouseMove) { SceneView.RepaintAll(); }
        }

        private static float MeanDrawHeight()
        {
            if (drawHeightMetres.Length == 0) { return 0f; }
            double sum = 0.0;
            for (int i = 0; i < drawHeightMetres.Length; i++) { sum += drawHeightMetres[i]; }
            return (float)(sum / drawHeightMetres.Length);
        }

        /// <summary> The panel: what is being shown, the weights behind it, the cell under the pointer, and -- when the
        /// raster was too large to draw whole -- the fact that it was thinned. That last line matters more than it looks:
        /// an author comparing a thinned overlay against a full one would otherwise see two different maps of the same
        /// ground and have no way to know why. </summary>
        private static void DrawPanel(SceneView view, int stride)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(8f, 8f, 420f, 260f), GUI.skin.box);

            GUILayout.Label("Suitability overlay -- " + DisplayName(display), EditorStyles.boldLabel);
            if (map == null || SuitabilityRaster.CellCount(map) == 0)
            {
                GUILayout.Label("No surface is solved yet, so there is nothing to draw.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                GUILayout.Label(legend, EditorStyles.wordWrappedMiniLabel);
                if (stride > 1)
                {
                    GUILayout.Label("Drawing every " + stride + "th cell: the raster is larger than "
                        + MaxDrawnCells + " cells. The map itself is unthinned; only this drawing is.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                if (plan != null) { GUILayout.Label(plan.Summary(), EditorStyles.wordWrappedMiniLabel); }
                GUILayout.Space(4f);
                GUILayout.Label(cursorLine.Length > 0 ? cursorLine : "Point at the ground to read a cell.",
                    EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        /// <summary> The display's name in the author's words, not the enum's. </summary>
        public static string DisplayName(SuitabilityDisplay which)
        {
            switch (which)
            {
                case SuitabilityDisplay.Cost: return "cost, all factors combined";
                case SuitabilityDisplay.Slope: return "gradient only, against this class's maximum";
                case SuitabilityDisplay.Earthwork: return "earthwork only, against the volume at that maximum";
                case SuitabilityDisplay.Binding: return "which factor decided each cell";
                case SuitabilityDisplay.Forbidden: return "ground this class cannot climb";
                case SuitabilityDisplay.Corridor: return "the corridor band";
                default: return which.ToString();
            }
        }
    }
}
#endif

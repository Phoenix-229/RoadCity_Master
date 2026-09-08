#if UNITY_EDITOR
#region "Imports"
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-14. The inspector for a hand-placed transmission run.
    ///
    /// Nothing here lays a numbered FIGURE out. Every editable dimension -- the span, the sag, the clearance, the whole
    /// steel catalogue and the four materials -- is drawn by <see cref="LegacyInspectorShell.AllGroups"/> from the rows
    /// <see cref="CityFieldRows"/> registers for this component, so a run's two dozen controls arrive with the unit
    /// column, the cited range, the inline out-of-range message, focus-out commit and the per-field reset icon every
    /// other inspector in the tool has -- and a field added to the settings later appears here untouched.
    ///
    /// What this file owns that the pole line and the railway do not is the ROUTE. A transmission run's route is a
    /// polyline of world corners the author drags, the exception the registry does not draw (a row for an array of
    /// vectors under a heading that reads "what it looks like" would be a row that helps nobody). So this file draws the
    /// corners two ways that agree: a typed list in the inspector for a coordinate an author knows exactly, and a
    /// dragged handle per corner in the Scene view for one they are placing by eye. Both write the same list and both
    /// invalidate the same plan, which is why a drag and a keystroke can never leave the run half-replanned.
    ///
    /// The other half is what the pole line's inspector owns too: saying what the answers produced. A transmission run
    /// is refused by physics the pole line is not big enough to hit -- a leg drawn through a settlement, a span that
    /// fouls the statutory clearance, a route of one corner -- so the refusals are printed in full, each naming the leg
    /// or the span and the remedy, because "not buildable" without which leg is a message an author cannot act on.
    ///
    /// The sentences are pure static functions of the plan (<see cref="PlanLine"/>, <see cref="GroundLine"/>,
    /// <see cref="RouteLine"/>), which is what lets D-16's gate assert the wording headlessly: a gate cannot open an
    /// inspector, and a sentence built inside <c>OnInspectorGUI</c> is a sentence no gate has ever read.
    ///
    /// The plan is CACHED and recomputed only when the author changes a field, drags a corner, or asks for a recheck.
    /// Planning a cross-country run seats every tower on four sampled feet and sweeps nine ground heights per span, and
    /// doing that on every repaint would make selecting the component cost more than building the run. The ground is why
    /// the recheck button exists: terraforming the hill under a line does not dirty this component, so nothing else
    /// would tell the cache it is stale. </summary>
    [CustomEditor(typeof(TransmissionLineRun))]
    public sealed class TransmissionLineRunEditor : UnityEditor.Editor
    {
        #region "What this inspector owns"
        /// <summary> The registry key for this component's rows. Spelled once, as a string, because the registry is
        /// keyed by type name and a second spelling is a row that silently stops being found. </summary>
        private const string ComponentName = "TransmissionLineRun";

        /// <summary> The serialized path of the route list. Held as a constant for the same reason the component name is:
        /// the handles, the typed list and the add/remove buttons all reach the one property, and a second spelling is a
        /// control that edits a different array than the one drawn. </summary>
        private const string RouteProperty = "settings.routeCorners";

        /// <summary> B-19's commit buffer, so no figure on this inspector commits per keystroke. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();

        /// <summary> The last plan and the note that came with it, kept between repaints. Null means "not planned since
        /// the last change", which is the state a fresh selection starts in. </summary>
        private TransmissionLineRunPlan cached;
        private string cachedNote = string.Empty;

        /// <summary> Whether the plan in the cache was measured against a real surface. Recorded at plan time rather than
        /// asked again at draw time, because the sentence has to describe the figures that are actually on screen: a
        /// gateway installed after the last plan would otherwise have the inspector claim a tower was seated on ground it
        /// was never read from. </summary>
        private bool cachedHadGround;

        /// <summary> What the last press of *Build* said, kept so the sentence outlives the frame the press happened in.
        /// </summary>
        private string lastBuildLine = string.Empty;
        #endregion


        #region "The two sections"
        public override void OnInspectorGUI()
        {
            fields.BeginFrame();
            serializedObject.Update();

            TransmissionLineRun run = target as TransmissionLineRun;
            if (run == null) { return; }

            LegacyInspectorShell.Header("Transmission run");

            EditorGUI.BeginChangeCheck();
            LegacyInspectorShell.SectionHeader("The line",
                "What the line carries, the steel that carries it, and the materials it is dressed in.");
            LegacyInspectorShell.AllGroups(serializedObject, ComponentName, fields);
            if (EditorGUI.EndChangeCheck()) { cached = null; }

            DrawRoute(run);
            DrawWhatCameOut(run);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary> The measured half: what the run works out to, and every reason it was refused.
        ///
        /// The plan is taken from the cache, filled on the first repaint after a change. The *Recheck* button is not a
        /// duplicate of that: a field change or a dragged corner invalidates the cache, but reshaping the ground under an
        /// unchanged line does not, and a clearance that was legal before a hill was raised is the case an author most
        /// needs told. </summary>
        private void DrawWhatCameOut(TransmissionLineRun run)
        {
            LegacyInspectorShell.SectionHeader("What came out",
                "The run the answers above produce, and anything that makes it unbuildable.");

            if (cached == null) { Replan(run); }
            if (cached == null)
            {
                EditorGUILayout.LabelField(PlanLine(null, cachedNote), EditorStyles.wordWrappedLabel);
                return;
            }

            bool open = LegacyInspectorShell.BeginMeasured(ComponentName);
            if (open) { DrawFigures(cached); }
            LegacyInspectorShell.EndMeasured(open);

            EditorGUILayout.LabelField(PlanLine(cached, cachedNote), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(GroundLine(cachedHadGround), EditorStyles.wordWrappedLabel);
            DrawRefusals(cached);
            DrawCommands(run);
        }
        #endregion


        #region "The route"
        /// <summary> The one control the registry does not draw: the polyline of world corners. Drawn as the serialized
        /// list so a coordinate an author knows can be typed exactly, with two buttons for the common edits -- append a
        /// corner a little past the last, and drop the last -- because a scene handle is for placing a corner by eye and
        /// a button is for adding one to place. Every write goes through the serialized object and is recorded for undo
        /// by Unity's own list drawing, so it collapses into the same history as the handle drags.
        ///
        /// Changing the route throws the plan away, the same as any field above: the towers march along these corners,
        /// so a moved corner is a re-seated line. </summary>
        private void DrawRoute(TransmissionLineRun run)
        {
            LegacyInspectorShell.SectionHeader("The route",
                "The corners the towers march along, in world space. Drag them in the Scene view, or type a coordinate "
                + "you know. Two corners is the least a line can be; a third and beyond turn the route and raise an "
                + "angle tower where it bends.");

            SerializedProperty route = serializedObject.FindProperty(RouteProperty);
            if (route == null)
            {
                EditorGUILayout.HelpBox("The route list could not be found on this component.", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(route, new GUIContent("Corner points"), true);
            if (EditorGUI.EndChangeCheck()) { cached = null; }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add a corner", LegacyInspectorShell.CommandButtonStyle()))
            {
                AppendCorner(route);
            }
            using (new EditorGUI.DisabledScope(route.arraySize == 0))
            {
                if (GUILayout.Button("Remove the last corner", LegacyInspectorShell.CommandButtonStyle()))
                {
                    route.arraySize = Mathf.Max(0, route.arraySize - 1);
                    cached = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(RouteLine(route.arraySize), EditorStyles.wordWrappedLabel);
        }

        /// <summary> Append one corner past the current last, offset along the last leg so it lands somewhere visible
        /// rather than on top of the corner it grew from. With fewer than two corners there is no leg to extend, so the
        /// new corner steps out along world +X by a nominal span instead -- a starting point the author then drags.
        /// </summary>
        private void AppendCorner(SerializedProperty route)
        {
            int n = route.arraySize;
            Vector3 next;
            const float step = 300f; // a nominal span, so a fresh corner is a line apart rather than a coincident point
            if (n >= 2)
            {
                Vector3 last = route.GetArrayElementAtIndex(n - 1).vector3Value;
                Vector3 prev = route.GetArrayElementAtIndex(n - 2).vector3Value;
                Vector3 dir = last - prev;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.right;
                next = last + dir * step;
            }
            else if (n == 1)
            {
                next = route.GetArrayElementAtIndex(0).vector3Value + new Vector3(step, 0f, 0f);
            }
            else
            {
                next = Vector3.zero;
            }
            route.arraySize = n + 1;
            route.GetArrayElementAtIndex(n).vector3Value = next;
            cached = null;
        }
        #endregion


        #region "Scene handles"
        /// <summary> The route in the Scene view: a line through the corners and a draggable handle on each.
        ///
        /// It is the counterpart of <see cref="AlignmentHandles"/> and shares its discipline -- one undo step per drag,
        /// never a byte touched outside the corner being dragged -- but it is simpler, because a transmission route is
        /// its corners and nothing is fitted between them. Dragging a handle records the component for undo, writes the
        /// one corner, and drops the plan so the figures recompute against where the corner now is.
        ///
        /// The handle rides on the ground under the corner rather than on the corner's authored Y, because the towers
        /// are seated on the sampled ground and never on the authored Y -- a handle floating at the typed height would
        /// sit where no tower will. </summary>
        private void OnSceneGUI()
        {
            TransmissionLineRun run = target as TransmissionLineRun;
            if (run == null || run.settings == null || run.settings.routeCorners == null) { return; }
            System.Collections.Generic.List<Vector3> route = run.settings.routeCorners;
            if (route.Count == 0) { return; }

            DrawRouteLine(route);

            for (int i = 0; i < route.Count; i++)
            {
                Vector3 authored = route[i];
                Vector3 world = new Vector3(authored.x, SurfaceHeight(authored.x, authored.z), authored.z);
                float size = HandleUtility.GetHandleSize(world) * 0.15f;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (!EditorGUI.EndChangeCheck()) { continue; }

                Undo.RecordObject(run, "Move transmission corner");
                // Only X and Z are authored; Y is a hint the seating ignores, so a drag writes the plane and leaves the
                // hint as it was rather than baking a ground height the tower would only re-sample.
                route[i] = new Vector3(moved.x, authored.y, moved.z);
                EditorUtility.SetDirty(run);
                cached = null;
            }
        }

        /// <summary> The polyline joining the corners, lifted clear of the ground so it reads above the terrain rather
        /// than z-fighting the surface. It is drawn from the same corners the handles sit on, so the line an author sees
        /// is the route the planner walks and never a smoothed or fitted version of it. </summary>
        private static void DrawRouteLine(System.Collections.Generic.List<Vector3> route)
        {
            if (route.Count < 2) { return; }
            Color was = Handles.color;
            Handles.color = new Color(0.35f, 0.7f, 1f, 0.9f);
            for (int i = 1; i < route.Count; i++)
            {
                Vector3 a = Lift(route[i - 1]);
                Vector3 b = Lift(route[i]);
                Handles.DrawAAPolyLine(4f, new Vector3[] { a, b });
            }
            Handles.color = was;
        }

        /// <summary> A corner lifted onto the ground under it, plus a metre of clearance so the line floats above the
        /// surface it follows rather than disappearing into it. </summary>
        private static Vector3 Lift(Vector3 corner)
        {
            return new Vector3(corner.x, SurfaceHeight(corner.x, corner.z) + 1f, corner.z);
        }

        /// <summary> The ground height under a plan-space point, through the installed reader -- the same surface every
        /// height question in the tool goes through. Zero when no reader is installed, which is the flat plane the
        /// ground sentence warns the earthwork was measured against. </summary>
        private static float SurfaceHeight(float x, float z)
        {
            ITerrainSurfaceReader reader = TerrainWriteGateway.Active != null
                ? TerrainWriteGateway.Active.SurfaceReader
                : null;
            return reader != null ? reader.SampleHeight(x, z) : 0f;
        }
        #endregion


        #region "Planning on demand"
        /// <summary> Fill the cache. The ground comes from the installed gateway rather than from the scene's terrains
        /// directly, so a line planned while a provider owns the terrain is measured against the surface that provider
        /// reports. The component collects the settlement rectangles itself, which is why -- unlike the pole line's Plan
        /// -- this one takes only the reader and the out note, and no furniture. </summary>
        private void Replan(TransmissionLineRun run)
        {
            ITerrainSurfaceReader ground = TerrainWriteGateway.Active != null
                ? TerrainWriteGateway.Active.SurfaceReader
                : null;
            cachedHadGround = ground != null;
            string note;
            cached = run.Plan(ground, out note);
            cachedNote = note;
        }
        #endregion


        #region "The measured figures"
        /// <summary> The figures a transmission run is judged by. Every one is drawn through
        /// <see cref="LegacyInspectorShell.Measured"/>, so none reads as a field that has stopped working, and the unit
        /// is passed separately rather than concatenated into the value.
        ///
        /// The order is the order an author asks the questions in: how much line is there, how many towers hold it up,
        /// how close does the lowest cable come to the ground, and how many towers ended with a foot in air. The
        /// clearance sits where an author looks first after "is it buildable", because it is the figure a refusal points
        /// back at. </summary>
        private static void DrawFigures(TransmissionLineRunPlan plan)
        {
            LegacyInspectorShell.Measured("Towers", Count(plan.towers.Count), null);
            LegacyInspectorShell.Measured("Spans", Count(plan.spans.Count), null);
            LegacyInspectorShell.Measured("Route length", Metres(plan.routeLengthMetres), "m");
            LegacyInspectorShell.Measured("Tightest clearance", Clearance(plan), Measurable(plan) ? "m" : null);
            LegacyInspectorShell.Measured("Clearance required", Metres(plan.requiredClearanceMetres), "m");
            LegacyInspectorShell.Measured("Towers with a foot in air", Count(plan.towersWithHangingFoot), null);
        }
        #endregion


        #region "Refusals and commands"
        /// <summary> Every refusal, in full. A settlement crossing names the leg and the footprint; a clearance failure
        /// names the span, the height achieved and the height required. Shortening any of that to fit a line would remove
        /// the part the author acts on. Drawn as help boxes so they read as the tool's answer rather than as a label the
        /// layout happened to produce. </summary>
        private static void DrawRefusals(TransmissionLineRunPlan plan)
        {
            for (int i = 0; i < plan.refusals.Count; i++)
            {
                EditorGUILayout.HelpBox(plan.refusals[i], MessageType.Warning);
            }
        }

        /// <summary> Build, clear, and recheck.
        ///
        /// *Build* is disabled while the plan is unbuildable, which is the point of refusing: a run that crosses a
        /// settlement or fouls its clearance is not offered and then reverted, it is not offered. *Recheck* is always
        /// available because the ground can change without this component changing.
        ///
        /// The scene work itself is <see cref="TransmissionLineRunBuilder"/>'s. This file presses the button and prints
        /// what came back. </summary>
        private void DrawCommands(TransmissionLineRun run)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!cached.Buildable))
            {
                if (GUILayout.Button("Build the transmission run", LegacyInspectorShell.CommandButtonStyle()))
                {
                    lastBuildLine = TransmissionLineRunBuilder.Build(run, cached);
                }
            }
            if (GUILayout.Button("Clear it", LegacyInspectorShell.CommandButtonStyle()))
            {
                lastBuildLine = TransmissionLineRunBuilder.Clear(run);
            }
            if (GUILayout.Button("Recheck the ground", LegacyInspectorShell.CommandButtonStyle()))
            {
                cached = null;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastBuildLine))
            {
                EditorGUILayout.LabelField(lastBuildLine, EditorStyles.wordWrappedLabel);
            }
        }
        #endregion


        #region "The sentences, pure"
        /// <summary> The one sentence that says what the run is. When there is no plan the note is the answer -- a run
        /// switched off, a route of one corner, or a leg through a settlement all say something, and none of them is a
        /// silent failure. When there is a plan, the plan's own summary is used rather than a second wording of it: the
        /// summary is what the gate reads and what a log line would carry, and an inspector that says it differently is a
        /// second answer to the same question. </summary>
        public static string PlanLine(TransmissionLineRunPlan plan, string note)
        {
            if (plan == null)
            {
                return string.IsNullOrEmpty(note) ? "Nothing planned yet." : Capitalise(note) + ".";
            }
            return plan.Summary();
        }

        /// <summary> What the route is, in a sentence, before any plan exists to summarise. Pure on the corner COUNT
        /// alone because that is the whole of what makes a route a line: nothing to string below two, a straight run at
        /// two, a turning route above. D-16 asserts each wording without a scene, which is why the count comes in as a
        /// number and not a serialized property. </summary>
        public static string RouteLine(int cornerCount)
        {
            if (cornerCount <= 0) { return "No route yet. Add a corner, then another, to draw where the line runs."; }
            if (cornerCount == 1)
            {
                return "One corner so far. A line needs at least two; add another and the towers will march between "
                       + "them.";
            }
            if (cornerCount == 2)
            {
                return "A straight run between two corners. Add a corner in the middle to turn the line, and an angle "
                       + "tower rises where it bends.";
            }
            return Count(cornerCount) + " corners, so the route turns "
                   + Countable(cornerCount - 2, "time", "times") + " between its two ends.";
        }

        /// <summary> What ground this run was seated against, and what it means when there is none.
        ///
        /// It is a sentence rather than nothing because the tower seating and the span clearances are the only figures on
        /// this inspector that depend on something outside the component, and a clearance measured against a flat plane
        /// looks exactly like one measured against a hill. The reader normally exists -- the standalone sink is itself an
        /// <c>ITerrainSurfaceReader</c> -- so the second branch is the case where a connected provider has gone away,
        /// which is precisely when an author would otherwise trust a figure they should not.
        ///
        /// Pure, so D-16 can assert both wordings without a scene. </summary>
        public static string GroundLine(bool hasGround)
        {
            if (hasGround)
            {
                return "Towers are seated and cables cleared through the installed terrain surface, so the figures above "
                       + "are what this line costs on the ground as it stands now. Reshape that ground and press "
                       + "Recheck.";
            }
            return "No terrain surface is readable, so every foot came back at zero: the towers are seated on a flat "
                   + "plane and the clearances measure the cables against it, not against your ground.";
        }

        /// <summary> A note is written to sit inside a sentence, so it starts lower case; a note used AS a sentence has
        /// to start upper. Done here rather than by writing every note twice. </summary>
        private static string Capitalise(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            if (char.IsUpper(text[0])) { return text; }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary> The tightest clearance on the run, or a plain statement that there is none to report. A span-less
        /// run would otherwise print positive infinity, which reads as a very good clearance rather than as no
        /// measurement. The unit is dropped in that case by <see cref="Measurable"/>, because "no span to measure m" is
        /// how a figure and its unit come apart. </summary>
        private static string Clearance(TransmissionLineRunPlan plan)
        {
            if (!Measurable(plan)) { return "no span to measure"; }
            return Metres(plan.worstClearanceMetres);
        }

        /// <summary> Whether the tightest clearance is a measurement at all. </summary>
        private static bool Measurable(TransmissionLineRunPlan plan)
        {
            return plan != null && plan.spans.Count > 0 && !double.IsInfinity(plan.worstClearanceMetres);
        }

        /// <summary> A count and the word that agrees with it. One time is not "1 times", and a sentence that gets that
        /// wrong is read as a placeholder. </summary>
        private static string Countable(int value, string singular, string plural)
        {
            return Count(value) + " " + (value == 1 ? singular : plural);
        }

        /// <summary> A count, formatted invariantly so the inspector does not read differently in a comma-decimal
        /// locale. </summary>
        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Metres(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
#endif

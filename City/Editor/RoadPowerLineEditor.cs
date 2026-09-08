// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-1. The inspector for a hand-placed pole line.
    ///
    /// Nothing here lays a field out. Every editable figure is drawn by
    /// <see cref="LegacyInspectorShell.AllGroups"/> from the twelve rows <see cref="CityFieldRows"/> registers for this
    /// component, so the run's controls arrive with the unit column, the cited range, the inline out-of-range message,
    /// focus-out commit and the per-field reset icon that every other inspector in the tool has -- and a field added to
    /// the component later appears here without this file being touched.
    ///
    /// What this file owns is the other half: saying what the answers produced. A pole line is the first authoring
    /// surface in the tool whose result can be REFUSED by physics rather than by a missing reference -- a span strung
    /// too low over a road is a legal failure, not a slip -- so the refusals are printed in full, each naming the two
    /// stations, the clearance achieved, the figure required and the NESC column it came from. An author who is told
    /// only "failed" would have to guess which of three remedies applies.
    ///
    /// The sentences are pure static functions of the plan (<see cref="PlanLine"/>, <see cref="RoadLine"/>), which is
    /// what lets D-16's gate assert the wording headlessly: a gate cannot open an inspector, and a sentence built
    /// inside <c>OnInspectorGUI</c> is a sentence no gate has ever read.
    ///
    /// The plan is CACHED and recomputed only when the author changes a field or asks for a recheck. Planning a
    /// kilometre of line samples two hundred curve points and nine ground heights per span, and doing that on every
    /// repaint would make selecting the component cost more than building the run. The ground is the reason the
    /// recheck button exists: terraforming the hill under a line does not dirty this component, so nothing else would
    /// tell the cache it is stale. </summary>
    [CustomEditor(typeof(RoadPowerLine))]
    public sealed class RoadPowerLineEditor : UnityEditor.Editor
    {
        #region "What this inspector owns"
        /// <summary> The registry key for this component's rows. Spelled once, as a string, because the registry is
        /// keyed by type name and a second spelling is a row that silently stops being found. </summary>
        private const string ComponentName = "RoadPowerLine";

        /// <summary> B-19's commit buffer, so no figure on this inspector commits per keystroke. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();

        /// <summary> The last plan and the note that came with it, kept between repaints. Null means "not planned since
        /// the last change", which is the state a fresh selection starts in. </summary>
        private RoadPowerLinePlan cached;
        private string cachedNote = string.Empty;

        /// <summary> What the last press of *Build* said, kept so the sentence outlives the frame the press happened
        /// in. </summary>
        private string lastBuildLine = string.Empty;

        /// <summary> What the last preset press set, kept so the sentence survives the repaint the press happened in.
        /// The presets are the only control here that writes several fields at once, so saying which fields moved is
        /// the difference between a button and a surprise. </summary>
        private string lastClassLine = string.Empty;
        #endregion


        #region "The two sections"
        public override void OnInspectorGUI()
        {
            fields.BeginFrame();
            serializedObject.Update();

            RoadPowerLine line = target as RoadPowerLine;
            if (line == null) { return; }

            LegacyInspectorShell.Header("Pole line");

            EditorGUI.BeginChangeCheck();
            LegacyInspectorShell.SectionHeader("The run",
                "What the line is, which side of the road it runs on, and how it is fitted out.");
            LegacyInspectorShell.AllGroups(serializedObject, ComponentName, fields);
            if (EditorGUI.EndChangeCheck()) { cached = null; }
            DrawClassPresets(line);

            DrawWhatCameOut(line);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary> The measured half: what the run works out to, and every reason it was refused.
        ///
        /// The plan is taken from the cache, filled on the first repaint after a change. The *Recheck* button is not a
        /// duplicate of that: a field change invalidates the cache, but reshaping the ground under an unchanged line
        /// does not, and a clearance that was legal before a hill was raised is the case an author most needs told.
        /// </summary>
        private void DrawWhatCameOut(RoadPowerLine line)
        {
            Road road = line.ResolveRoad();
            LegacyInspectorShell.SectionHeader("What came out",
                "The run the answers above produce, and anything that makes it unbuildable.");
            EditorGUILayout.LabelField(RoadLine(road), EditorStyles.wordWrappedLabel);
            if (road == null) { return; }

            if (cached == null) { Replan(line); }
            if (cached == null)
            {
                EditorGUILayout.LabelField(PlanLine(null, cachedNote), EditorStyles.wordWrappedLabel);
                return;
            }

            bool open = LegacyInspectorShell.BeginMeasured(ComponentName);
            if (open) { DrawFigures(cached); }
            LegacyInspectorShell.EndMeasured(open);

            EditorGUILayout.LabelField(PlanLine(cached, cachedNote), EditorStyles.wordWrappedLabel);
            DrawRefusals(cached);
            DrawCommands(line);
        }
        #endregion


        #region "The three presets"
        /// <summary> The street/highway difference, as three buttons rather than a stored mode.
        ///
        /// D-4 says a city pole and a highway pole are the same pole, and that what differs between them is AUTHORED.
        /// A popup that REMEMBERED which column was chosen would give the span length a second home: the author nudges
        /// a span to 45 m, the popup still reads "urban street", and the inspector now disagrees with the numbers
        /// underneath it. These buttons write into the run's own fields and remember nothing, which is why the sentence
        /// they leave behind is past tense.
        ///
        /// The write is recorded for undo and is followed by <c>serializedObject.Update()</c>, because the fields above
        /// were drawn from the serialized copy taken at the top of this frame and the <c>ApplyModifiedProperties</c> at
        /// the bottom of it would otherwise write that stale copy straight back over the preset. </summary>
        private void DrawClassPresets(RoadPowerLine line)
        {
            EditorGUILayout.LabelField("Set it from a standard column, then adjust anything:",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < ClassOrder.Length; i++)
            {
                if (GUILayout.Button(PoleRunClasses.Name(ClassOrder[i]), LegacyInspectorShell.CommandButtonStyle()))
                {
                    ApplyClass(line, ClassOrder[i]);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastClassLine))
            {
                EditorGUILayout.LabelField(lastClassLine, EditorStyles.wordWrappedLabel);
            }
        }

        /// <summary> The three columns, in the order they are offered: shortest span first, so the row reads as an axis
        /// rather than as three unrelated buttons. </summary>
        private static readonly PoleRunClass[] ClassOrder =
        {
            PoleRunClass.UrbanStreet, PoleRunClass.SuburbanStreet, PoleRunClass.RuralHighway
        };

        /// <summary> Write one column into the run, then throw the plan away so the figures below recompute against
        /// what the press just set rather than against what stood there before it. </summary>
        private void ApplyClass(RoadPowerLine line, PoleRunClass runClass)
        {
            Undo.RecordObject(line, "Set pole run class");
            lastClassLine = PoleRunClasses.Apply(line.settings, runClass);
            EditorUtility.SetDirty(line);
            serializedObject.Update();
            cached = null;
        }
        #endregion


        #region "Planning on demand"
        /// <summary> Fill the cache. The ground comes from the installed gateway rather than from the scene's terrains
        /// directly, so a line planned while a provider owns the terrain is measured against the surface that provider
        /// reports -- the same reader every other height question in the tool goes through.
        ///
        /// Null furniture is deliberate: it asks the component to read the road's own edge objects, which is the whole
        /// point of the conflict resolution. </summary>
        private void Replan(RoadPowerLine line)
        {
            string note;
            cached = line.Plan(TerrainWriteGateway.Active.SurfaceReader, null, out note);
            cachedNote = note;
        }
        #endregion


        #region "The measured figures"
        /// <summary> The ten figures a pole line is judged by. Every one of them is drawn through
        /// <see cref="LegacyInspectorShell.Measured"/>, so none of them reads as a field that has stopped working, and
        /// the unit is passed separately rather than concatenated into the value. </summary>
        private static void DrawFigures(RoadPowerLinePlan plan)
        {
            LegacyInspectorShell.Measured("Poles", Count(plan.poles.Count), null);
            LegacyInspectorShell.Measured("Spans", Count(plan.spans.Count), null);
            LegacyInspectorShell.Measured("Run length", Metres(plan.runLengthMetres), "m");
            LegacyInspectorShell.Measured("Tightest clearance", Clearance(plan), Measurable(plan) ? "m" : null);
            LegacyInspectorShell.Measured("Clearance required", Metres(plan.requiredClearanceMetres), "m");
            LegacyInspectorShell.Measured("Dropped for furniture", Count(plan.droppedPoleCount), null);
            LegacyInspectorShell.Measured("Nudged clear of furniture", Count(NudgedCount(plan)), null);
            LegacyInspectorShell.Measured("Transformers", Count(FittedCount(plan, PoleFittings.Transformer)), null);
            LegacyInspectorShell.Measured("Guyed poles", Count(GuyedCount(plan)), null);
            LegacyInspectorShell.Measured("Poles carrying a strand",
                Count(FittedCount(plan, PoleFittings.Communication)), null);
        }

        /// <summary> How many poles were moved off a furniture item. Pure and public because it is the figure telling
        /// an author whether the frontage is crowded, and D-16 asserts it without a skin. </summary>
        public static int NudgedCount(RoadPowerLinePlan plan)
        {
            if (plan == null) { return 0; }
            int count = 0;
            for (int i = 0; i < plan.poles.Count; i++)
            {
                if (plan.poles[i].nudged) { count++; }
            }
            return count;
        }

        /// <summary> How many poles carry a given fitting. </summary>
        public static int FittedCount(RoadPowerLinePlan plan, PoleFittings fitting)
        {
            if (plan == null) { return 0; }
            int count = 0;
            for (int i = 0; i < plan.poles.Count; i++)
            {
                if ((plan.poles[i].fittings & fitting) != 0) { count++; }
            }
            return count;
        }

        /// <summary> How many poles are guyed. Counts POLES rather than guys, because two guys on one dead-end pole is
        /// one anchor problem for the author and not two. </summary>
        public static int GuyedCount(RoadPowerLinePlan plan)
        {
            if (plan == null) { return 0; }
            int count = 0;
            for (int i = 0; i < plan.poles.Count; i++)
            {
                if (plan.poles[i].guyCount > 0) { count++; }
            }
            return count;
        }
        #endregion


        #region "Refusals and commands"
        /// <summary> Every refusal, in full. A clearance failure names two stations, the height achieved, the height
        /// required and the table column it came from, and shortening any of that to fit a line would remove the part
        /// the author acts on. Drawn as help boxes so they read as the tool's answer rather than as a label the layout
        /// happened to produce. </summary>
        private static void DrawRefusals(RoadPowerLinePlan plan)
        {
            for (int i = 0; i < plan.refusals.Count; i++)
            {
                EditorGUILayout.HelpBox(plan.refusals[i], MessageType.Warning);
            }
        }

        /// <summary> Build, clear, and recheck.
        ///
        /// *Build* is disabled while the plan is unbuildable, which is the point of refusing: a run that fails its
        /// clearance is not offered and then reverted, it is not offered. *Recheck* is always available because the
        /// ground can change without this component changing.
        ///
        /// The scene work itself is <see cref="RoadPowerLineBuilder"/>'s. This file presses the button and prints what
        /// came back. </summary>
        private void DrawCommands(RoadPowerLine line)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!cached.Buildable))
            {
                if (GUILayout.Button("Build the pole line", LegacyInspectorShell.CommandButtonStyle()))
                {
                    lastBuildLine = RoadPowerLineBuilder.Build(line, cached);
                }
            }
            if (GUILayout.Button("Clear it", LegacyInspectorShell.CommandButtonStyle()))
            {
                lastBuildLine = RoadPowerLineBuilder.Clear(line);
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
        /// <summary> What road this line hangs on, or why it hangs on nothing. Pure, so D-16 can assert it. </summary>
        public static string RoadLine(Road road)
        {
            if (road == null)
            {
                return "This pole line is not under a road. Put the component on a road, or on a child of one, and it "
                       + "will run along that road's own curve.";
            }
            return "Runs along " + road.name + ".";
        }

        /// <summary> The one sentence that says what the run is. When there is no plan the note is the answer -- a line
        /// that is switched off, a spline not built yet, or a run shorter than a span all say something, and none
        /// of them is a failure. When there is a plan, the plan's own summary is used rather than a second wording of
        /// it: the summary is what the gate reads and what a log line would carry, and an inspector that says it
        /// differently is a second answer to the same question. </summary>
        public static string PlanLine(RoadPowerLinePlan plan, string note)
        {
            if (plan == null)
            {
                return string.IsNullOrEmpty(note) ? "Nothing planned yet." : Capitalise(note) + ".";
            }
            return plan.Summary();
        }

        /// <summary> A note is written to sit inside a sentence, so it starts lower case; a note used AS a sentence has
        /// to start upper. Done here rather than by writing every note twice. </summary>
        private static string Capitalise(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            if (char.IsUpper(text[0])) { return text; }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
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

        /// <summary> The tightest clearance on the run, or a plain statement that there is none to report. A span-less
        /// run would otherwise print positive infinity, which reads as a very good clearance rather than as no
        /// measurement. The unit is dropped in that case by <see cref="Measurable"/>, because "no span to measure m" is
        /// how a figure and its unit come apart. </summary>
        private static string Clearance(RoadPowerLinePlan plan)
        {
            if (!Measurable(plan)) { return "no span to measure"; }
            return Metres(plan.worstClearanceMetres);
        }

        /// <summary> Whether the tightest clearance is a measurement at all. </summary>
        private static bool Measurable(RoadPowerLinePlan plan)
        {
            return plan != null && plan.spans.Count > 0 && !double.IsInfinity(plan.worstClearanceMetres);
        }
        #endregion
    }
}
#endif

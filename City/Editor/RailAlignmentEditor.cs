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
    /// <summary> B-27 / D-5. The inspector for a hand-placed railway.
    ///
    /// Nothing here lays a field out. Every editable figure is drawn by
    /// <see cref="LegacyInspectorShell.AllGroups"/> from the rows <see cref="CityFieldRows"/> registers for this
    /// component, so a railway's thirty-odd controls arrive with the unit column, the cited range, the inline
    /// out-of-range message, focus-out commit and the per-field reset icon every other inspector in the tool has --
    /// and a field added to <c>RailAlignmentSettings</c> later appears here without this file being touched.
    ///
    /// What this file owns is the other half: saying what the answers produced. A railway is the second authoring
    /// surface whose result can be REFUSED by geometry rather than by a missing reference -- a curve below the class
    /// minimum radius cannot be built as track at all -- and it is the first whose measured figures include a
    /// SPEED the author never typed. So the refusals are printed in full and the figures are printed beside them,
    /// because "unbuildable" without the radius it needed is a message an author cannot act on.
    ///
    /// The sentences are pure static functions of the plan (<see cref="PlanLine"/>, <see cref="GroundLine"/>,
    /// <see cref="CrossingLine"/>), which is what lets D-16's gate assert the wording headlessly: a gate cannot open
    /// an inspector, and a sentence built inside <c>OnInspectorGUI</c> is a sentence no gate has ever read.
    ///
    /// The plan is CACHED and recomputed only when the author changes a field or asks for a recheck. Staking a
    /// kilometre of mainline resolves the horizontal alignment, sweeps a gradient-limited vertical profile over two
    /// hundred samples, reads a ground height at each and then searches every road in the scene for a crossing;
    /// doing that per repaint would make selecting the component cost more than building the railway. The ground is
    /// why the recheck button exists: terraforming the hill under a staked line does not dirty this component, so
    /// nothing else would tell the cache it is stale. </summary>
    [CustomEditor(typeof(RailAlignment))]
    public sealed class RailAlignmentEditor : UnityEditor.Editor
    {
        #region "What this inspector owns"
        /// <summary> The registry key for this component's rows. Spelled once, as a string, because the registry is
        /// keyed by type name and a second spelling is a row that silently stops being found. </summary>
        private const string ComponentName = "RailAlignment";

        /// <summary> B-19's commit buffer, so no figure on this inspector commits per keystroke. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();

        /// <summary> The last plan and the note that came with it, kept between repaints. Null means "not planned
        /// since the last change", which is the state a fresh selection starts in. </summary>
        private RailAlignmentPlan cached;
        private string cachedNote = string.Empty;

        /// <summary> Whether the plan in the cache was measured against a real surface. Recorded at plan time rather
        /// than asked again at draw time, because the sentence has to describe the figures that are actually on
        /// screen: a gateway installed after the last plan would otherwise have the inspector claim the earthwork
        /// came from ground it was never read from. </summary>
        private bool cachedHadGround;

        /// <summary> The overhead line strung over the cached alignment, and the note that came with it. Planned in the
        /// same pass and off the same samples as <see cref="cached"/>, so the masts hang from the rail head the track
        /// figures were measured to rather than from a second reading of it. Null is the ordinary state, not a failure:
        /// an unelectrified line carries no wire, and the toggle that says so is a field above. The note is why -- kept
        /// so <see cref="OverheadLine"/> can say it, though the inspector only prints it once a wire exists. </summary>
        private RailOverheadPlan cachedOverhead;
        private string cachedOverheadNote = string.Empty;

        /// <summary> What the last press of *Build* said, kept so the sentence outlives the frame the press happened
        /// in. </summary>
        private string lastBuildLine = string.Empty;

        /// <summary> What the last class press set. Kept for the same reason the pole line keeps its own: the columns
        /// are the only control here that writes thirty fields at once, so saying which figures moved is the
        /// difference between a button and a surprise. </summary>
        private string lastClassLine = string.Empty;
        #endregion


        #region "The two sections"
        public override void OnInspectorGUI()
        {
            fields.BeginFrame();
            serializedObject.Update();

            RailAlignment line = target as RailAlignment;
            if (line == null) { return; }

            LegacyInspectorShell.Header("Railway");

            EditorGUI.BeginChangeCheck();
            LegacyInspectorShell.SectionHeader("The line",
                "What kind of railway this is, what it is laid on, and how finely it is staked.");
            LegacyInspectorShell.AllGroups(serializedObject, ComponentName, fields);
            if (EditorGUI.EndChangeCheck()) { cached = null; }
            DrawClassPresets(line);

            DrawWhatCameOut(line);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary> The measured half: what the line works out to, and every reason it was refused.
        ///
        /// The plan is taken from the cache, filled on the first repaint after a change. The *Recheck* button is not
        /// a duplicate of that: a field change invalidates the cache, but reshaping the ground under an unchanged
        /// alignment does not, and an embankment that was within the authored limit before a valley was deepened is
        /// exactly the case an author most needs told. </summary>
        private void DrawWhatCameOut(RailAlignment line)
        {
            LegacyInspectorShell.SectionHeader("What came out",
                "The railway the answers above stake, and anything that makes it unbuildable.");

            if (cached == null) { Replan(line); }
            if (cached == null)
            {
                EditorGUILayout.LabelField(PlanLine(null, cachedNote), EditorStyles.wordWrappedLabel);
                return;
            }

            bool open = LegacyInspectorShell.BeginMeasured(ComponentName);
            if (open) { DrawFigures(cached, cachedOverhead); }
            LegacyInspectorShell.EndMeasured(open);

            EditorGUILayout.LabelField(PlanLine(cached, cachedNote), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(GroundLine(cachedHadGround), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(CrossingLine(cached), EditorStyles.wordWrappedLabel);
            if (cachedOverhead != null)
            {
                EditorGUILayout.LabelField(OverheadLine(cachedOverhead, cachedOverheadNote),
                    EditorStyles.wordWrappedLabel);
            }
            DrawStakingNotes(cached);
            DrawRefusals(cached);
            if (cachedOverhead != null) { DrawOverheadRefusals(cachedOverhead); }
            DrawCommands(line);
        }
        #endregion


        #region "The four columns"
        /// <summary> The kind-of-railway difference, as four buttons rather than a stored class.
        ///
        /// D-5 asks for a popup and this is deliberately not one, for the reason
        /// <c>RoadPowerLineEditor.DrawClassPresets</c> gives about its own three buttons: a popup that REMEMBERED the
        /// chosen column would give every figure a second home. The author widens a radius to 350 m, the popup still
        /// reads "tramway", and the inspector now holds two answers to what the design speed is -- with no way to
        /// tell which one the cant above was computed from. These buttons write into the run's own fields and
        /// remember nothing, which is why the sentence they leave behind is past tense.
        ///
        /// They are worth more here than on a pole line, because the four columns differ by two orders of magnitude:
        /// a street tramway turns on 25 m and a high-speed line needs 4000 m, and an author who had to type thirty
        /// figures to get from one to the other would type most of them wrong. The sentence each press leaves ends
        /// with the speed that column's own tightest legal curve can be run at, which is the figure an author is
        /// really choosing and would otherwise have to solve for.
        ///
        /// The write is recorded for undo and is followed by <c>serializedObject.Update()</c>, because the fields
        /// above were drawn from the serialized copy taken at the top of this frame and the
        /// <c>ApplyModifiedProperties</c> at the bottom of it would otherwise write that stale copy straight back
        /// over the column. </summary>
        private void DrawClassPresets(RailAlignment line)
        {
            EditorGUILayout.LabelField("Set it from a standard column, then adjust anything:",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < ClassOrder.Length; i++)
            {
                if (GUILayout.Button(RailLineClasses.Name(ClassOrder[i]),
                    LegacyInspectorShell.CommandButtonStyle()))
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

        /// <summary> The four columns, in the order they are offered: tightest curve first, so the row reads as a
        /// scale from a street to a high-speed line rather than as four unrelated buttons. </summary>
        private static readonly RailLineClass[] ClassOrder =
        {
            RailLineClass.Tramway, RailLineClass.BranchLine, RailLineClass.Mainline, RailLineClass.HighSpeed
        };

        /// <summary> Write one column into the line, then throw the plan away so the figures below recompute against
        /// what the press just set rather than against what stood there before it. </summary>
        private void ApplyClass(RailAlignment line, RailLineClass lineClass)
        {
            Undo.RecordObject(line, "Set railway line class");
            lastClassLine = RailLineClasses.Apply(line.settings, lineClass);
            EditorUtility.SetDirty(line);
            serializedObject.Update();
            cached = null;
        }
        #endregion


        #region "Planning on demand"
        /// <summary> Fill the cache. The ground comes from the installed gateway rather than from the scene's
        /// terrains directly, so a line staked while a provider owns the terrain is measured against the surface
        /// that provider reports -- the same reader every other height question in the tool goes through.
        ///
        /// Null roads are deliberate: it asks the component to read the scene's own roads, sorted, which is the
        /// whole point of the level-crossing search. A gate hands it two invented ones instead. </summary>
        private void Replan(RailAlignment line)
        {
            string note;
            ITerrainSurfaceReader ground = TerrainWriteGateway.Active != null
                ? TerrainWriteGateway.Active.SurfaceReader
                : null;
            cachedHadGround = ground != null;
            cached = line.Plan(ground, null, out note);
            cachedNote = note;

            // The overhead line is planned off the alignment that just came back rather than the raw component, so the
            // masts hang from the same samples the track figures were measured to. It is only strung when the line is
            // electrified; on every other line PlanOverhead returns null with a note, and the inspector stays quiet
            // about a wire the author did not ask for.
            string overheadNote = string.Empty;
            cachedOverhead = cached != null ? line.PlanOverhead(cached, ground, out overheadNote) : null;
            cachedOverheadNote = cached != null ? overheadNote : note;
        }
        #endregion


        #region "The measured figures"
        /// <summary> The figures a railway is judged by. Every one is drawn through
        /// <see cref="LegacyInspectorShell.Measured"/>, so none reads as a field that has stopped working, and the
        /// unit is passed separately rather than concatenated into the value -- which is what lets the cant figures
        /// carry "mm" beside a length in "m" without either looking like the other.
        ///
        /// The order is the order an author asks the questions in: how much railway is there, how tightly does it
        /// turn, how steeply does it climb, how fast can it be run, and how much earth did it cost. The permissible
        /// speed sits in the middle of that rather than at the end because it is the figure that most often sends an
        /// author back to the radius above. </summary>
        private static void DrawFigures(RailAlignmentPlan plan, RailOverheadPlan overhead)
        {
            LegacyInspectorShell.Measured("Run length", Metres(plan.runLengthMetres), "m");
            LegacyInspectorShell.Measured("Tracks", Count(plan.trackCount), null);
            LegacyInspectorShell.Measured("Formation width", Metres(plan.formationWidthMetres), "m");
            LegacyInspectorShell.Measured("Sleepers", Count(plan.sleeperCount), null);
            LegacyInspectorShell.Measured("Sharpest radius", Radius(plan), Turning(plan) ? "m" : null);
            LegacyInspectorShell.Measured("Steepest gradient", Fixed(plan.steepestGradientPerMille, "0.#"),
                "per mille");
            LegacyInspectorShell.Measured("Runnable at", Speed(plan), Restricted(plan) ? "km/h" : null);
            LegacyInspectorShell.Measured("Cant applied", Fixed(plan.maxAppliedCantMillimetres, "0"), "mm");
            LegacyInspectorShell.Measured("Cant deficiency", Fixed(plan.maxCantDeficiencyMillimetres, "0"), "mm");
            LegacyInspectorShell.Measured("Transition length", Metres(plan.totalTransitionLengthMetres), "m");
            LegacyInspectorShell.Measured("Deepest embankment", Metres(plan.deepestEmbankmentMetres), "m");
            LegacyInspectorShell.Measured("Deepest cutting", Metres(plan.deepestCuttingMetres), "m");
            LegacyInspectorShell.Measured("Structure clearance",
                Metres(plan.requiredStructureClearanceMetres), "m");
            LegacyInspectorShell.Measured("Level crossings", Count(plan.crossings.Count), null);
            LegacyInspectorShell.Measured("Station stops", Count(plan.stationStopCount), null);
            DrawOverheadFigures(overhead);
        }

        /// <summary> The overhead line's figures, folded into the same measured group as the track's rather than given
        /// a second foldout of their own: they are figures this same component worked out, and an author reads the run
        /// as one railway, not a railway and an electrification. Drawn only once a wire actually exists -- an
        /// unelectrified line has no overhead plan, and a line whose class refused the wire has a plan with fewer than
        /// two masts whose figures are the defaults, so the refusal box below carries that message instead of a row of
        /// zeroes here. The shortest-dropper row drops its unit the way the radius and speed rows do, because a run
        /// whose spans were all too short to hang a dropper has no such length to report. </summary>
        private static void DrawOverheadFigures(RailOverheadPlan overhead)
        {
            if (overhead == null || overhead.masts.Count < 2) { return; }
            EditorGUILayout.LabelField("Overhead line", EditorStyles.boldLabel);
            LegacyInspectorShell.Measured("Masts", Count(overhead.masts.Count), null);
            LegacyInspectorShell.Measured("Shortest span", Metres(overhead.shortestSpanMetres), "m");
            LegacyInspectorShell.Measured("Longest span", Metres(overhead.longestSpanMetres), "m");
            LegacyInspectorShell.Measured("Contact wire height",
                Metres(overhead.nominalContactHeightMetres), "m");
            LegacyInspectorShell.Measured("Lowest contact wire",
                Metres(overhead.lowestContactHeightMetres), "m");
            LegacyInspectorShell.Measured("Masts lowered", Count(overhead.loweredMastCount), null);
            LegacyInspectorShell.Measured("Droppers", Count(overhead.droppers.Count), null);
            LegacyInspectorShell.Measured("Shortest dropper",
                overhead.droppers.Count > 0 ? Metres(overhead.shortestDropperMetres) : "none",
                overhead.droppers.Count > 0 ? "m" : null);
            LegacyInspectorShell.Measured("Worst mid-span offset",
                Fixed(overhead.worstMidSpanOffsetMetres, "0.###"), "m");
        }
        #endregion


        #region "Refusals, notes and commands"
        /// <summary> The two things the staking REPORTS rather than refuses, each drawn only when it happened.
        ///
        /// A shortened transition and an overlapping curve are both buildable and both real -- the tangent between
        /// two curves was too short to fit the ramp the class rate wants, so the ramp was steepened instead of the
        /// line being rejected. That is what the real thing does. But an author told nothing would believe the run
        /// met a limit it does not, so each gets a sentence naming what was traded and where to look. They are
        /// <c>Info</c> rather than <c>Warning</c> for the reason the builder's material note is neither a refusal
        /// nor a warning: a channel that mixes "this cannot be built" with "this was tightened" teaches an author to
        /// read past both. </summary>
        private static void DrawStakingNotes(RailAlignmentPlan plan)
        {
            string notes = StakingLine(plan);
            if (string.IsNullOrEmpty(notes)) { return; }
            EditorGUILayout.HelpBox(notes, MessageType.Info);
        }

        /// <summary> Every refusal, in full. A radius failure names the curve, the radius it reached and the class
        /// minimum it needed; an earthwork failure names the station and the depth. Shortening any of that to fit a
        /// line would remove the part the author acts on. Drawn as help boxes so they read as the tool's answer
        /// rather than as a label the layout happened to produce. </summary>
        private static void DrawRefusals(RailAlignmentPlan plan)
        {
            for (int i = 0; i < plan.refusals.Count; i++)
            {
                EditorGUILayout.HelpBox(plan.refusals[i], MessageType.Warning);
            }
        }

        /// <summary> Every overhead-line refusal, in its own right. Kept separate from <see cref="DrawRefusals"/>
        /// rather than merged, because the two answer different questions: the track can be laid and the wire still
        /// refuse -- a curve wireable at no legal span, a structure that leaves no room for the conductor, a class that
        /// is not a catenary system. An author fixes those by moving a mast or raising a bridge, not by easing the
        /// curve the track refusals point at, so mixing the two lists would send them to the wrong tool. Drawn as help
        /// boxes for the reason the track refusals are: they are the tool's answer, not a stray label. </summary>
        private static void DrawOverheadRefusals(RailOverheadPlan plan)
        {
            for (int i = 0; i < plan.refusals.Count; i++)
            {
                EditorGUILayout.HelpBox(plan.refusals[i], MessageType.Warning);
            }
        }

        /// <summary> Build, clear, and recheck.
        ///
        /// *Build* is disabled while the plan is unbuildable, which is the point of refusing: a line whose curve is
        /// below the class minimum is not offered and then reverted, it is not offered. *Recheck* is always
        /// available because the ground can change without this component changing.
        ///
        /// The scene work itself is <see cref="RailAlignmentBuilder"/>'s. This file presses the button and prints
        /// what came back. </summary>
        private void DrawCommands(RailAlignment line)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!cached.Buildable))
            {
                if (GUILayout.Button("Build the railway", LegacyInspectorShell.CommandButtonStyle()))
                {
                    lastBuildLine = RailAlignmentBuilder.Build(line, cached, cachedOverhead);
                }
            }
            if (GUILayout.Button("Clear it", LegacyInspectorShell.CommandButtonStyle()))
            {
                lastBuildLine = RailAlignmentBuilder.Clear(line);
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
        /// <summary> The one sentence that says what the railway is. When there is no plan the note is the answer --
        /// a line switched off, an alignment with one node, or a run shorter than one sample spacing all say
        /// something, and none of them is a failure. When there is a plan, the plan's own summary is used rather
        /// than a second wording of it: the summary is what the gate reads and what a log line would carry, and an
        /// inspector that says it differently is a second answer to the same question. </summary>
        public static string PlanLine(RailAlignmentPlan plan, string note)
        {
            if (plan == null)
            {
                return string.IsNullOrEmpty(note) ? "Nothing staked yet." : Capitalise(note) + ".";
            }
            return plan.Summary();
        }

        /// <summary> The one sentence that says what the overhead line is, or why there is none. Like
        /// <see cref="PlanLine"/> it hands off to the plan's own <see cref="RailOverheadPlan.Summary"/> rather than
        /// re-wording it, so the inspector, a log line and D-16's gate all read one sentence; and like it, a null plan
        /// is not a failure -- an unelectrified line, or one with fewer than two samples, has a note that IS the
        /// answer. The inspector only calls this once a wire exists, but the method still answers for null so the gate
        /// can assert the note wording without a scene. </summary>
        public static string OverheadLine(RailOverheadPlan plan, string note)
        {
            if (plan == null)
            {
                return string.IsNullOrEmpty(note) ? "No overhead line." : Capitalise(note) + ".";
            }
            return plan.Summary();
        }
        ///
        /// The plan's summary already counts the crossings and gives the worst skew, so this does not restate
        /// either. What it adds is the thing a skew COSTS: the panel length, which is the road width divided by the
        /// sine of the angle, and which is the figure that turns "most skewed 30 degrees" into "22 m of crossing
        /// surface". It also names the road, because an author looking at four crossings needs to know which one is
        /// the expensive one.
        ///
        /// A line with no crossings is not silent. Saying so is the difference between "this railway crosses no
        /// road" and "the search has not run", and an author who staked a line expecting a crossing where a road
        /// clearly runs is being told the road is bridged over it or is not at grade. </summary>
        public static string CrossingLine(RailAlignmentPlan plan)
        {
            if (plan == null) { return "No crossings: nothing has been staked yet."; }
            if (plan.crossings.Count == 0)
            {
                return "This line crosses no road at grade. Any road that looks as though it should cross is either "
                       + "carried over the railway on a structure or is not staked yet.";
            }

            int worst = 0;
            for (int i = 1; i < plan.crossings.Count; i++)
            {
                if (plan.crossings[i].panelLengthMetres > plan.crossings[worst].panelLengthMetres) { worst = i; }
            }
            RailLevelCrossing c = plan.crossings[worst];
            string text = "The longest crossing panel is " + Metres(c.panelLengthMetres) + " m, where "
                          + RoadName(c.roadName) + " crosses at " + Fixed(c.crossingAngleDegrees, "0.#")
                          + " degrees, " + Metres(c.railStationMetres) + " m along the line.";
            if (c.appliedCantMillimetres >= 1.0)
            {
                text += " That track is canted " + Fixed(c.appliedCantMillimetres, "0")
                        + " mm there, so the road surface has to twist through the cant to meet the rails.";
            }
            return text;
        }

        /// <summary> What the staking traded to fit, or nothing at all. Empty when neither happened, which is what
        /// keeps the note out of the inspector on a clean line rather than printing a reassurance nobody asked for.
        /// </summary>
        public static string StakingLine(RailAlignmentPlan plan)
        {
            if (plan == null) { return string.Empty; }
            List<string> parts = new List<string>();
            if (plan.shortenedTransitionCount > 0)
            {
                parts.Add(Countable(plan.shortenedTransitionCount, "transition was", "transitions were")
                          + " shortened to fit the tangent before the curve, so the cant ramps steeper there than "
                          + "the class rate allows");
            }
            if (plan.overlappingCurveCount > 0)
            {
                parts.Add(Countable(plan.overlappingCurveCount, "curve runs", "curves run")
                          + " into the next one, leaving no straight between them");
            }
            if (parts.Count == 0) { return string.Empty; }

            return "The line was staked, with what it had to give: " + string.Join("; ", parts.ToArray())
                   + ". Both are buildable and both are ordinary. Moving the nodes further apart is what undoes "
                   + "them.";
        }

        /// <summary> What ground this line was measured against, and what it means when there is none.
        ///
        /// It is a sentence rather than nothing because the earthwork figures are the only ones on this inspector
        /// that depend on something outside the component, and a cutting depth measured against a flat plane looks
        /// exactly like a cutting depth measured against a hill. The reader normally exists -- the standalone sink is
        /// itself an <c>ITerrainSurfaceReader</c> -- so the second branch is the case where a connected provider has
        /// gone away, which is precisely when an author would otherwise trust a figure they should not.
        ///
        /// Pure, so D-16 can assert both wordings without a scene. </summary>
        public static string GroundLine(bool hasGround)
        {
            if (hasGround)
            {
                return "Heights are read through the installed terrain surface, so the earthwork below is what this "
                       + "line costs on the ground as it stands now. Reshape that ground and press Recheck.";
            }
            return "No terrain surface is readable, so every height came back as zero: the earthwork figures below "
                   + "measure this line against a flat plane and not against your ground.";
        }

        /// <summary> A road's name, or the plain words for a road that arrived without one. A crossing sentence
        /// reading "where  crosses" is how an empty string reaches a screenshot. </summary>
        private static string RoadName(string name)
        {
            return string.IsNullOrEmpty(name) ? "an unnamed road" : name;
        }

        /// <summary> A note is written to sit inside a sentence, so it starts lower case; a note used AS a sentence
        /// has to start upper. Done here rather than by writing every note twice. </summary>
        private static string Capitalise(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            if (char.IsUpper(text[0])) { return text; }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary> The sharpest radius, or the plain words for a line that never turns. Infinity printed as a
        /// number would read as a very wide curve rather than as no curve at all, and the unit is dropped with it by
        /// <see cref="Turning"/> so the row does not read "straight throughout m". </summary>
        private static string Radius(RailAlignmentPlan plan)
        {
            if (!Turning(plan)) { return "straight throughout"; }
            return Metres(plan.sharpestRadiusMetres);
        }

        /// <summary> Whether the line turns at all, which is whether the sharpest radius is a measurement.
        /// </summary>
        private static bool Turning(RailAlignmentPlan plan)
        {
            return plan != null && !double.IsInfinity(plan.sharpestRadiusMetres);
        }

        /// <summary> The slowest the run has to be taken, or the plain words for a line nothing restricts. A
        /// straight line's permissible speed is positive infinity, and printing that would read as a very fast
        /// railway rather than as no restriction. </summary>
        private static string Speed(RailAlignmentPlan plan)
        {
            if (!Restricted(plan)) { return "nothing restricts it"; }
            return Fixed(plan.lowestPermissibleSpeedKmh, "0");
        }

        /// <summary> Whether anything on the line imposes a speed restriction. </summary>
        private static bool Restricted(RailAlignmentPlan plan)
        {
            return plan != null && !double.IsInfinity(plan.lowestPermissibleSpeedKmh);
        }

        /// <summary> A count and the word that agrees with it. One transition is not "1 transitions were", and a
        /// sentence that gets that wrong is read as a placeholder. </summary>
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

        private static string Fixed(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
#endif

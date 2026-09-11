// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-5. The scene half of a railway: the one place in the tool that turns a
    /// <see cref="RailAlignmentPlan"/> into objects.
    ///
    /// The division is the same one the pole line states and for the same reason. <see cref="RailAlignment"/> computes
    /// and refuses -- it never mutates a scene, never touches an asset and never draws -- and this is the other side of
    /// that sentence: everything here mutates a scene and nothing here decides anything. Where the track runs, how it
    /// is banked, how steeply it climbs, where a road crosses it and whether the line is legal at all were all answered
    /// before <see cref="Build"/> was called. A builder that recomputed any of it would be a second answer to a settled
    /// question, and the answer an author was shown in the inspector would be the one that lost.
    ///
    /// So the work here is short: clear what was built last time, make one holder, sweep the run into pieces, lay a
    /// panel at every level crossing, and return a sentence saying what happened.
    ///
    /// TWO objects per piece, not one. A railway has three material families -- ballast, sleeper, rail -- against
    /// <c>MeshScratch</c>'s two-submesh contract, so the formation (ballast and sleepers) is one mesh and the rails are
    /// another. Widening the contract to three was the alternative and was refused: every mesh in the pole family is
    /// built on it, <c>Make</c> writes exactly two materials, and a contract that means two things is a contract that
    /// silently mis-skins whichever family was written first.
    ///
    /// BAKED, not runtime. The meshes are built here, in the editor assembly, and saved with the scene, so a shipped
    /// player carries vertices and not a mesh builder. It is also why the geometry lives in
    /// <see cref="RailTrackMeshes"/> under <c>City/Editor</c> and not beside the planner: RM-62's provenance gate
    /// scans <c>City/Runtime</c> for mesh tokens and must keep finding none.
    ///
    /// One undo step for the whole run, collapsed the way <see cref="RoadPowerLineBuilder"/> collapses a pole line: an
    /// author who presses Build and dislikes it presses Ctrl+Z once, not once per piece.
    ///
    /// The names come from <see cref="CityHierarchy"/>, which is what lets <see cref="Clear"/> know what it may delete.
    /// A generated child parses as a <see cref="GeneratedKind.Rail"/> name; a child the author made does not.
    /// </summary>
    public static class RailAlignmentBuilder
    {
        #region "What a built run is called"
        /// <summary> The undo label. One string, because the group name and every registered object share it and two
        /// spellings would read as two operations in the undo history. </summary>
        private const string UndoLabel = "Build railway";

        /// <summary> The undo label for the other direction. Clearing is its own step rather than a second
        /// "Build railway" entry, because an author who has just cleared a run reads the history to find the step to
        /// undo, and two identical names is how the wrong one gets undone. </summary>
        private const string ClearLabel = "Clear railway";

        /// <summary> The four element ids a run uses: the holder, a formation piece, a rails piece and a crossing
        /// panel. They differ so that the four can share an ordinal without sharing a name, and so that
        /// <see cref="OwnsItsMesh"/> can answer for each. </summary>
        private const string RunElement = "run.rail.";
        private const string BedElement = "bed.rail.";
        private const string SteelElement = "steel.rail.";
        private const string PanelElement = "panel.rail.";
        #endregion


        #region "Build"
        /// <summary> Build the line, and return the sentence the inspector prints. A sentence rather than a bool
        /// because every outcome here has something to say: a refused plan, a plan too short to sweep, a built line
        /// with a material slot left empty and a complete line are four different answers, and only one of them is a
        /// failure.
        ///
        /// The plan is taken AS GIVEN. It is not re-planned, not re-checked against the ground and not repaired: the
        /// inspector planned it against the installed surface reader, printed its refusals, and disabled the button
        /// while it was unbuildable. The one thing tested here is <see cref="RailAlignmentPlan.Buildable"/>, because a
        /// caller that is not the inspector -- a gate, a batch, a later bake runner -- can reach this method too.
        ///
        /// The track count comes from the PLAN and not from the settings. The planner clamped it, staked the formation
        /// at that width and measured every clearance against it; reading the author's field again here would let a
        /// two-track line be built on a formation that was checked as one. </summary>
        public static string Build(RailAlignment line, RailAlignmentPlan plan, RailOverheadPlan overheadPlan)
        {
            if (line == null) { return "Nothing was built: the builder was handed no railway."; }
            if (plan == null) { return "Nothing was built: the line has not been planned yet."; }
            if (!plan.Buildable) { return NotBuildableLine(plan); }

            RailAlignmentSettings s = line.settings != null ? line.settings : new RailAlignmentSettings();
            RailTrackClass track = s.track != null ? s.track : RailTrackClass.Default();
            RailFormationClass form = s.formation != null ? s.formation : RailFormationClass.Default();
            int tracks = plan.trackCount > 0 ? plan.trackCount : 1;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Remove(line);
            Transform holder = MakeHolder(line);

            // The rails and the panel write ONE material into BOTH slots. Their meshes have a single submesh against
            // MeshScratch's two-submesh contract, and a renderer whose material array is shorter than its mesh draws
            // the surplus submesh with the last material -- so the array is filled deliberately rather than left to
            // that fallback, which is the kind of behaviour that changes between Unity versions.
            Material[] bedMaterials = new Material[] { s.ballastMaterial, s.sleeperMaterial };
            Material[] steelMaterials = new Material[] { s.railMaterial, s.railMaterial };
            Material[] panelMaterials = new Material[] { s.crossingMaterial, s.crossingMaterial };

            int pieces = BuildPieces(plan, form, track, tracks, holder, line.name, bedMaterials, steelMaterials);
            int panels = BuildPanels(plan, form, track, tracks, holder, line.name, panelMaterials);
            
            string ohleSentence = BuildOverheadLine(line, overheadPlan, holder, undoGroup);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);

            return BuiltLine(plan, pieces, panels, line.name) + MissingMaterialsNote(s, panels)
                   + BatchingNote(pieces * 2 + panels + 1) + ohleSentence;
        }

        /// <summary> Sweep the run into pieces, two objects each.
        ///
        /// A piece is <see cref="RailTrackMeshes.SamplesPerPiece"/> samples of track, about 235 m at the 5 m default
        /// spacing, and it is the unit three separate limits all want. A mesh has a 65 535-vertex ceiling on the 16-bit
        /// index format and a kilometre of double-track sleepers would pass it. A mesh has ONE bounding box, and a box
        /// as long as the line is a box that is always on screen, so a single-mesh railway is a railway that never
        /// culls. And a rebuild that changed one curve would rewrite every vertex of the line rather than a few pieces
        /// of it.
        ///
        /// Both objects of a piece are placed at the SAME origin -- <see cref="RailTrackMeshes.PieceOrigin"/>, the
        /// piece's first sample -- and both meshes are built about it. That is what keeps the rails on their bed: the
        /// two sweeps share their frames, their origin and their vertex positions to the last bit, so there is no
        /// arithmetic left in which one could drift from the other. Local coordinates rather than world ones also keep
        /// the vertices small, which matters on a line staked ten kilometres from the scene origin, where world-space
        /// floats have lost the millimetre a 5 cm flangeway is measured in.
        ///
        /// A null mesh is SKIPPED, not refused. It means the piece had fewer than two usable samples, which the plan
        /// has already reported on; refusing here would turn one degenerate tail into a line that will not build.
        /// </summary>
        private static int BuildPieces(RailAlignmentPlan plan, RailFormationClass form, RailTrackClass track,
            int tracks, Transform holder, string runName, Material[] bedMaterials, Material[] steelMaterials)
        {
            int count = RailTrackMeshes.PieceCount(plan.samples.Count);
            int built = 0;
            for (int piece = 0; piece < count; piece++)
            {
                int from, span;
                if (!RailTrackMeshes.PieceRange(plan.samples.Count, piece, out from, out span)) { continue; }

                string bedName = Named(BedElement, runName, piece);
                string steelName = Named(SteelElement, runName, piece);
                Mesh bed = RailTrackMeshes.BuildFormation(plan.samples, from, span, form, track, tracks, bedName);
                Mesh steel = RailTrackMeshes.BuildRails(plan.samples, from, span, form, track, tracks, steelName);
                if (bed == null || steel == null) { continue; }

                Vector3 origin = RailTrackMeshes.PieceOrigin(plan.samples, from);
                Make(bedName, holder, origin, Quaternion.identity, bed, bedMaterials);
                Make(steelName, holder, origin, Quaternion.identity, steel, steelMaterials);
                built++;
            }
            return built;
        }

        /// <summary> Lay one panel at every level crossing the plan found.
        ///
        /// The panel is its own object rather than part of the piece it falls in, for two reasons that both come from
        /// where a crossing IS. It is a road's material and a road authority's surface, so it takes its own slot;
        /// and it straddles whatever piece boundary happens to be near it, so a panel folded into a piece would be
        /// cut in half by the arbitrary place the sweep was divided.
        ///
        /// It is placed at <see cref="RailLevelCrossing.position"/>, the point the planner found the road crossing at,
        /// and its mesh is built about that same point -- the crossing's own answer, not a second search for it.
        /// </summary>
        private static int BuildPanels(RailAlignmentPlan plan, RailFormationClass form, RailTrackClass track,
            int tracks, Transform holder, string runName, Material[] panelMaterials)
        {
            int built = 0;
            for (int i = 0; i < plan.crossings.Count; i++)
            {
                RailLevelCrossing crossing = plan.crossings[i];
                string name = Named(PanelElement, runName, i);
                Mesh panel = RailTrackMeshes.BuildCrossingPanel(plan.samples, crossing, form, track, tracks, name);
                if (panel == null) { continue; }
                Make(name, holder, crossing.position, Quaternion.identity, panel, panelMaterials);
                built++;
            }
            return built;
        }

        /// <summary> The holder every generated object hangs under. A child of the component rather than a branch of a
        /// city root, because a railway is HAND-PLACED: a line staked across open country has no city root to hang
        /// from, and <see cref="CityHierarchy.EnsureBranch"/> needs one. The name is still a hierarchy name, so what a
        /// build made is told apart from what the author made by the same parse either way. </summary>
        private static Transform MakeHolder(RailAlignment line)
        {
            GameObject holder = new GameObject(Named(RunElement, line.name, 0));
            Undo.RegisterCreatedObjectUndo(holder, UndoLabel);
            holder.transform.SetParent(line.transform, false);
            return holder.transform;
        }

        /// <summary> One object, its mesh and its two material slots. Identity rotation throughout: the bank, the grade
        /// and the curve are already IN the vertices, put there by the swept frames, so a rotation here would turn a
        /// piece that is already oriented. </summary>
        private static GameObject Make(string name, Transform parent, Vector3 position, Quaternion rotation,
            Mesh mesh, Material[] materials)
        {
            GameObject made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, UndoLabel);
            made.transform.SetParent(parent, false);
            made.transform.SetPositionAndRotation(position, rotation);
            MeshFilter filter = made.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(made);
            return made;
        }

        /// <summary> A generated child's name: a hierarchy name whose element id says both which run this belongs
        /// to and which of the four kinds of object it is.
        ///
        /// The run name is cleaned of the three characters <see cref="CityHierarchy.TryParseName"/> reads as structure.
        /// A component called <c>Main line [up]#north</c> is a perfectly ordinary thing for an author to type, and
        /// carrying those brackets into a generated name would produce a name that parses back to the wrong element id
        /// -- which is how Clear would one day fail to recognise its own output. </summary>
        private static string Named(string prefix, string runName, int ordinal)
        {
            string clean = string.IsNullOrEmpty(runName) ? "unnamed" : runName;
            clean = clean.Replace('[', '-').Replace(']', '-').Replace('#', '-');
            return CityHierarchy.NameFor(GeneratedKind.Rail, prefix + clean, ordinal);
        }

        /// <summary> D-7 scene half. Generates the OHLE objects (masts + wires) from the already-computed
        /// <see cref="RailOverheadPlan"/>. Called after the track is built, because the overhead stands ON
        /// the track, not beside it. </summary>
        public static string BuildOverheadLine(
            RailAlignment alignment,
            RailOverheadPlan overheadPlan,
            Transform holder,
            int undoGroup)
        {
            if (alignment == null || alignment.settings == null || !alignment.settings.electrified 
                || overheadPlan == null || !overheadPlan.Buildable)
            {
                return string.Empty;
            }

            int builtMasts = 0;
            int builtSpans = 0;

            RailAlignmentSettings s = alignment.settings;
            Material[] mastMats = new Material[] { s.ohleMastMaterial, s.ohleBracketMaterial };
            Material[] wireMats = new Material[] { s.ohleWireMaterial, s.ohleWireMaterial };

            // 1. Masts
            for (int i = 0; i < overheadPlan.masts.Count; i++)
            {
                RailOverheadMast mast = overheadPlan.masts[i];
                string name = Named("mast.rail.", alignment.name, i);
                GameObject go;

                if (s.ohleMastPrefab != null)
                {
                    go = UnityEngine.Object.Instantiate(s.ohleMastPrefab);
                    go.name = name;
                    
                    // Rotation: +X faces the track
                    // Since mast.basePoint is laterally offset, we need the tangent. We can use MeshScratch.FrameAt
                    Vector3 fwd, right, up, pos;
                    RailTrackMeshes.FrameAt(alignment.settings.plan.samples, 0, alignment.settings.plan.samples.Count, mast.stationMetres, s.track.cantFormulaGaugeMillimetres, out pos, out fwd, out right, out up);
                    
                    // We face track. If staggerSign is -1, track is to our right? No, mast stands at lateral offset.
                    // The track is at -right * (MastClearOfFormationMetres + halfWidth).
                    // So +X should be -right.
                    go.transform.SetPositionAndRotation(mast.basePoint, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(0, -90 * mast.staggerSign, 0));
                }
                else
                {
                    OHLEMastMeshKey key = new OHLEMastMeshKey
                    {
                        baseDiameter = s.overhead.mastBaseDiameterMetres,
                        topDiameter = s.overhead.mastTopDiameterMetres,
                        bracketTubeDiameter = s.overhead.bracketTubeDiameterMetres,
                        foundationDepth = s.overhead.mastFoundationDepthMetres,
                        mastTopAboveMessenger = s.overhead.mastTopAboveMessengerMetres,
                        railHeadAboveGround = mast.railHeadAboveGroundMetres,
                        messengerHeight = mast.messengerHeightMetres,
                        stagger = mast.staggerMetres,
                        staggerSign = mast.staggerSign,
                        halfFormationWidth = (float)s.formation.FormationWidthMetres(s.trackCount) * 0.5f
                    };
                    Mesh mesh = OHLEMastMesh.GetOrCreate(key);
                    go = new GameObject(name);
                    go.transform.position = mast.basePoint;
                    
                    Vector3 fwd, right, up, pos;
                    RailTrackMeshes.FrameAt(alignment.settings.plan.samples, 0, alignment.settings.plan.samples.Count, mast.stationMetres, s.track.cantFormulaGaugeMillimetres, out pos, out fwd, out right, out up);
                    
                    go.transform.rotation = Quaternion.LookRotation(fwd, up);

                    MeshFilter filter = go.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = mastMats;
                }
                
                go.transform.SetParent(holder, true);
                Undo.RegisterCreatedObjectUndo(go, UndoLabel);
                builtMasts++;
            }

            // 2. Wires
            for (int i = 0; i < overheadPlan.spans.Count; i++)
            {
                RailOverheadSpan span = overheadPlan.spans[i];
                RailOverheadMast fromMast = overheadPlan.masts[span.fromMastIndex];
                RailOverheadMast toMast = overheadPlan.masts[span.toMastIndex];
                
                Vector3 fwd1, right1, up1, pos1;
                RailTrackMeshes.FrameAt(alignment.settings.plan.samples, 0, alignment.settings.plan.samples.Count, fromMast.stationMetres, s.track.cantFormulaGaugeMillimetres, out pos1, out fwd1, out right1, out up1);
                
                Vector3 fwd2, right2, up2, pos2;
                RailTrackMeshes.FrameAt(alignment.settings.plan.samples, 0, alignment.settings.plan.samples.Count, toMast.stationMetres, s.track.cantFormulaGaugeMillimetres, out pos2, out fwd2, out right2, out up2);
                
                Vector3 p1CatW = pos1 + up1 * fromMast.messengerHeightMetres + right1 * (fromMast.staggerMetres * fromMast.staggerSign);
                Vector3 p2CatW = pos2 + up2 * toMast.messengerHeightMetres + right2 * (toMast.staggerMetres * toMast.staggerSign);
                
                Vector3 p1ConW = pos1 + up1 * fromMast.contactWireHeightMetres + right1 * (fromMast.staggerMetres * fromMast.staggerSign);
                Vector3 p2ConW = pos2 + up2 * toMast.contactWireHeightMetres + right2 * (toMast.staggerMetres * toMast.staggerSign);

                string name = Named("wire.rail.", alignment.name, i);
                Mesh mesh;
                OHLEWireMesh.BuildSpan(span, fromMast, toMast, p1CatW, p2CatW, p1ConW, p2ConW, fromMast.basePoint, s.overhead, out mesh);
                
                GameObject go = new GameObject(name);
                go.transform.SetPositionAndRotation(fromMast.basePoint, Quaternion.identity);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = wireMats;
                
                go.transform.SetParent(holder, true);
                Undo.RegisterCreatedObjectUndo(go, UndoLabel);
                builtSpans++;
            }

            return " OHLE: " + builtMasts + " masts, " + builtSpans + " spans built.";
        }
        #endregion


        #region "Clearing what was built"
        /// <summary> Take the built line out of the scene and say what went. Its own undo group, so an author who
        /// built, looked and cleared has two steps in the history and not one ambiguous pair. </summary>
        public static string Clear(RailAlignment line)
        {
            if (line == null) { return "Nothing was cleared: the builder was handed no railway."; }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            int removed = Remove(line);
            Undo.SetCurrentGroupName(ClearLabel);
            Undo.CollapseUndoOperations(undoGroup);
            return ClearedLine(removed);
        }

        /// <summary> Clear only the overhead line equipment. </summary>
        public static string ClearOverheadLine(RailAlignment line)
        {
            if (line == null) { return "Nothing was cleared."; }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            int removed = 0;
            Transform holder = FindHolder(line);
            if (holder != null)
            {
                for (int i = holder.childCount - 1; i >= 0; i--)
                {
                    Transform child = holder.GetChild(i);
                    if (child.name.Contains("[mast.rail.") || child.name.Contains("[wire.rail."))
                    {
                        removed += RemoveTree(child.gameObject);
                    }
                }
            }
            Undo.SetCurrentGroupName("Clear OHLE");
            Undo.CollapseUndoOperations(undoGroup);
            return "Cleared " + removed + " OHLE objects.";
        }

        /// <summary> Build only the overhead line equipment. </summary>
        public static string BuildOverheadOnly(RailAlignment line, RailOverheadPlan overheadPlan)
        {
            if (line == null || overheadPlan == null) { return "Nothing built."; }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            ClearOverheadLine(line); // Remove old OHLE
            Transform holder = FindHolder(line);
            if (holder == null) { return "Please build the railway first."; }
            string sentence = BuildOverheadLine(line, overheadPlan, holder, undoGroup);
            Undo.SetCurrentGroupName("Build OHLE");
            Undo.CollapseUndoOperations(undoGroup);
            return sentence;
        }

        private static Transform FindHolder(RailAlignment line)
        {
            for (int i = 0; i < line.transform.childCount; i++)
            {
                Transform child = line.transform.GetChild(i);
                if (child.name.StartsWith("Rail[run.rail.", System.StringComparison.Ordinal)) { return child; }
            }
            return null;
        }

        /// <summary> Destroy the children this builder made, and only those.
        ///
        /// A name is the whole test. A child survives unless <see cref="CityHierarchy.TryParseKind"/> reads it as
        /// <see cref="GeneratedKind.Rail"/>, which is what makes it safe for an author to park their own prefab under
        /// the component -- a signal box, a buffer stop, a station building they modelled -- and still press *Build*
        /// again. There is no marker component to add and no list to keep in sync, because a list that drifts from the
        /// scene is worse than no list.
        ///
        /// Walked in reverse because destroying a child shortens the list it is being read from. </summary>
        private static int Remove(RailAlignment line)
        {
            int removed = 0;
            for (int i = line.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = line.transform.GetChild(i);
                GeneratedKind kind;
                if (child == null || !CityHierarchy.TryParseKind(child.name, out kind)) { continue; }
                if (kind != GeneratedKind.Rail) { continue; }
                removed += RemoveTree(child.gameObject);
            }
            return removed;
        }

        /// <summary> Destroy one generated subtree, meshes included, and return how many objects went.
        ///
        /// The meshes are destroyed BEFORE their objects and through <see cref="Undo.DestroyObjectImmediate"/> rather
        /// than <c>Object.DestroyImmediate</c>. Both halves matter. A mesh that is not an asset and is referenced by
        /// nothing but the renderer being destroyed would otherwise sit in memory for the rest of the session, and here
        /// that is not a rounding error: a kilometre of double track is four dozen pieces of ten thousand vertices, and
        /// an author who rebuilds a line twenty times while tuning a curve would leave twenty copies of it behind.
        /// And a mesh destroyed outside the undo system would not come back when the author undoes the clear, leaving
        /// the restored objects showing nothing. </summary>
        private static int RemoveTree(GameObject root)
        {
            int objects = root.GetComponentsInChildren<Transform>(true).Length;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] == null || filters[i].sharedMesh == null) { continue; }
                if (!OwnsItsMesh(filters[i].gameObject.name)) { continue; }
                Undo.DestroyObjectImmediate(filters[i].sharedMesh);
            }
            Undo.DestroyObjectImmediate(root);
            return objects;
        }

        /// <summary> Whether the object with this name brought its own mesh into the scene, and so has to take it away
        /// again.
        ///
        /// True for all three mesh kinds, and that is the difference from the pole line. A pole shows a mesh from
        /// <c>PoleMeshFactory</c>'s cache, shared by every pole with the same parameter set, so destroying it would
        /// empty the next run's poles too. A piece of railway is a swept curve: no two pieces of a line are the same
        /// shape, there is nothing to cache and nothing is shared, so every mesh here belongs to the one object showing
        /// it. Public and pure because it is the one rule that decides whether a rebuild leaks, and D-16 asserts it
        /// without a scene. </summary>
        public static bool OwnsItsMesh(string name)
        {
            string element;
            if (!CityHierarchy.TryParseName(name, out element)) { return false; }
            return element.StartsWith(BedElement, StringComparison.Ordinal)
                   || element.StartsWith(SteelElement, StringComparison.Ordinal)
                   || element.StartsWith(PanelElement, StringComparison.Ordinal)
                   || element.StartsWith("wire.rail.", StringComparison.Ordinal);
        }
        #endregion


        #region "What the build says, pure"
        /// <summary> How many objects make the batching sentence worth printing. Below this the draw calls are noise;
        /// at eight and up the line is a row of unique meshes and how they are batched decides between a handful of
        /// setup passes and one per piece. </summary>
        private const int BatchingWorthMentioning = 8;

        /// <summary> What a successful build did.
        ///
        /// It says what the BUILD produced -- pieces, panels, objects -- and deliberately not what the LINE is.
        /// <see cref="RailAlignmentPlan.Summary"/> already states the length, the track count, the sharpest radius, the
        /// steepest gradient, the cant, the runnable speed and the earthwork, and it is printed directly above this
        /// sentence. Saying any of it again here would be a second wording of one answer, and the day the two disagree
        /// is the day an author stops trusting both.
        ///
        /// The object count is derived rather than counted: two objects per piece, one per panel, and the holder. A
        /// count taken from the hierarchy would be the same number arrived at less certainly, and it is the number an
        /// author checks the build against.
        ///
        /// Pure, and public with it, because the sentence is the only report an author gets and D-16 asserts the
        /// wording headlessly -- a sentence assembled inside a button handler is a sentence no gate has ever read.
        /// </summary>
        public static string BuiltLine(RailAlignmentPlan plan, int pieces, int panels, string runName)
        {
            if (plan == null) { return "Nothing was built: the line has not been planned yet."; }

            int objects = pieces * 2 + panels + 1;
            string where = string.IsNullOrEmpty(runName) ? string.Empty : " under " + runName;
            string text = "Built " + Countable(pieces, "piece", "pieces") + " of track";
            if (panels > 0)
            {
                text += " and " + Countable(panels, "crossing panel", "crossing panels");
            }
            return text + where + ", " + Countable(objects, "object", "objects") + " in all.";
        }

        /// <summary> Why a build did not happen. The refusals are already printed in full above the button, so this
        /// counts them and points at them rather than repeating one of them badly. An unstaked line is the other case
        /// and it is not a refusal: nothing was refused, there was simply no alignment to sweep. </summary>
        public static string NotBuildableLine(RailAlignmentPlan plan)
        {
            if (plan == null) { return "Nothing was built: the line has not been planned yet."; }
            if (plan.samples.Count < 2)
            {
                return "Nothing was built: no track was staked. The alignment may have fewer than two nodes, or the "
                       + "run may be shorter than one sample spacing.";
            }
            return "Nothing was built: " + Countable(plan.refusals.Count, "refusal stands", "refusals stand")
                   + " against this line, listed above. The button comes back when they are answered.";
        }

        /// <summary> Which material slots were left empty, said plainly and appended to the build sentence.
        ///
        /// An empty slot is NOT a refusal, nor a warning. A refusal means the line cannot legally be built -- a curve
        /// below the minimum radius, a gradient past the ruling one -- and mixing a missing reference in with that
        /// would teach an author to read past both. The geometry a bare slot produces is the geometry a filled one
        /// produces; it is pink. So the line is built, and the sentence names the slot.
        ///
        /// The crossing slot is only named when a panel was actually laid. A line that crosses no road has nothing to
        /// fill that slot FOR, and naming it would send an author looking for a material the build never asked for.
        /// </summary>
        public static string MissingMaterialsNote(RailAlignmentSettings settings, int panels)
        {
            RailAlignmentSettings s = settings != null ? settings : new RailAlignmentSettings();
            List<string> bare = new List<string>();
            if (s.ballastMaterial == null) { bare.Add("ballast"); }
            if (s.sleeperMaterial == null) { bare.Add("sleeper"); }
            if (s.railMaterial == null) { bare.Add("rail"); }
            if (panels > 0 && s.crossingMaterial == null) { bare.Add("crossing"); }
            if (bare.Count == 0) { return string.Empty; }

            return " The " + string.Join(" and the ", bare.ToArray()) + (bare.Count == 1 ? " slot is" : " slots are")
                   + " empty, so that much of the line renders in Unity's missing-material pink. The geometry is "
                   + "right; only the material is absent.";
        }

        /// <summary> The one performance sentence, and the opposite of the pole line's.
        ///
        /// A pole line is one mesh shown at many stations, which is what GPU instancing is for. A railway is the other
        /// case: no two pieces of a curve are the same shape, so there is nothing for instancing to repeat and ticking
        /// it on the rail material would do nothing an author could measure. What DOES batch them is that every piece
        /// wears the same material set, which is what URP's SRP Batcher keys on, and that is a property of how this
        /// builder assigns materials rather than of anything the author sets -- so it is worth one sentence saying the
        /// line is already in the good case, and saying which well-meant tick would take it out again. </summary>
        private static string BatchingNote(int objects)
        {
            if (objects < BatchingWorthMentioning) { return string.Empty; }
            return " These " + Count(objects) + " objects are each a unique mesh, so GPU instancing has nothing to "
                   + "repeat -- but they all wear the same material set, which is what URP's SRP Batcher batches on, "
                   + "so the line draws in a few setup passes rather than one per piece. Leaving them out of Batching "
                   + "Static keeps it that way: static batching would copy every piece's vertices into the scene a "
                   + "second time for a gain the SRP Batcher is already giving.";
        }

        /// <summary> What a clear did. Says what it left as well as what it took, because the author's own children
        /// surviving a clear is the behaviour they would otherwise have to test by losing something. </summary>
        public static string ClearedLine(int removed)
        {
            if (removed <= 0)
            {
                return "There was nothing to clear: no built line was found under this component.";
            }
            return "Cleared " + Countable(removed, "object", "objects")
                   + ". Anything you parented here yourself was left alone.";
        }

        /// <summary> A count and the word that agrees with it. One piece is not "1 pieces", and a sentence that gets
        /// that wrong is read as a placeholder. </summary>
        private static string Countable(int value, string singular, string plural)
        {
            return Count(value) + " " + (value == 1 ? singular : plural);
        }

        /// <summary> A count, formatted invariantly, so the sentence does not change shape in a comma-decimal locale.
        /// </summary>
        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
#endif

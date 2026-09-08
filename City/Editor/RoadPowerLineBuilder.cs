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
    /// <summary> B-27 / D-1. The scene half of a pole line: the one place in the tool that turns a
    /// <see cref="RoadPowerLinePlan"/> into objects.
    ///
    /// The division is stated by the component itself -- it computes and refuses, it never mutates a scene, it never
    /// touches an asset and it never draws. This is the other side of that sentence. Everything here mutates a scene
    /// and nothing here decides anything: where a pole stands, which fittings it carries, how far a wire sags and
    /// whether the run is legal at all were all answered before <see cref="Build"/> was called, and a builder that
    /// recomputed any of it would be a second answer to a settled question.
    ///
    /// So what it does is short. It clears what it built last time, makes one holder, drops one CACHED mesh at every
    /// pole, groups the wires into a few meshes, and returns a sentence saying what happened.
    ///
    /// The grouping is the phone budget (D-15). A pole is the same mesh at every station that shares its parameter set,
    /// so twenty-six poles are one mesh and twenty-six transforms -- and one draw call if the timber material has GPU
    /// instancing on, which is why the sentence says so when it does not. Wires cannot be instanced, every span being a
    /// different curve, so they are batched <see cref="ConductorCurve.SpansPerPiece"/> spans to a mesh: a kilometre of
    /// line is a handful of draw calls instead of a hundred and fifty.
    ///
    /// BAKED, not runtime. The meshes are built here, in the editor assembly, and saved with the scene, so a shipped
    /// player carries the vertices and not the mesh builder. That is what the baked-not-runtime direction means in
    /// practice, and it is why <c>City/Runtime</c> now contains no mesh builder at all.
    ///
    /// One undo step for the whole run, collapsed the way <see cref="CitySettlementFactory"/> collapses a settlement
    /// bake: an author who presses Build and dislikes it presses Ctrl+Z once, not once per pole.
    ///
    /// The names come from <see cref="CityHierarchy"/>, which is what lets <see cref="Clear"/> know what it may delete.
    /// A generated child parses as a <see cref="GeneratedKind.Power"/> name; a child the author made does not. Clear
    /// removes the first kind and never touches the second. </summary>
    public static class RoadPowerLineBuilder
    {
        #region "What a built run is called"
        /// <summary> The undo label. One string, because the group name and every registered object share it and two
        /// spellings would read as two operations in the undo history. </summary>
        private const string UndoLabel = "Build pole line";

        /// <summary> The undo label for the other direction. Clearing is its own step rather than a second
        /// "Build pole line" entry, because an author who has just cleared a run reads the history to find the step to
        /// undo, and two identical names is how the wrong one gets undone. </summary>
        private const string ClearLabel = "Clear pole line";

        /// <summary> The three element ids a run uses. They differ so that the holder, a pole and a wire piece can
        /// share an ordinal without sharing a name, and so that <see cref="OwnsItsMesh"/> can tell which objects bring
        /// their own mesh into the scene and which are showing one from the factory cache. </summary>
        private const string LineElement = "line.road.";
        private const string PoleElement = "pole.road.";
        private const string WireElement = "wire.road.";
        #endregion


        #region "Build"
        /// <summary> Build the run, and return the sentence the inspector prints. A sentence rather than a bool because
        /// every outcome here has something to say: a refused plan, a plan with no poles, a built run with a material
        /// slot left empty and a complete run are four different answers, and only one of them is a failure.
        ///
        /// The plan is taken AS GIVEN. It is not re-planned, not re-checked against the ground and not repaired: the
        /// inspector planned it against the installed surface reader, printed its refusals, and disabled the button
        /// while it was unbuildable. A builder that re-planned could disagree with what the author was shown. The one
        /// thing tested here is <see cref="RoadPowerLinePlan.Buildable"/>, because a caller that is not the
        /// inspector -- a gate, a batch, a later bake runner -- can reach this method too. </summary>
        public static string Build(RoadPowerLine line, RoadPowerLinePlan plan)
        {
            if (line == null) { return "Nothing was built: the builder was handed no pole line."; }
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }
            if (!plan.Buildable) { return NotBuildableLine(plan); }

            RoadPowerLineSettings s = line.settings != null ? line.settings : RoadPowerLineSettings.Default();
            PoleGeometryClass shape = s.shape != null ? s.shape : PoleGeometryClass.Default();
            DistributionPoleClass electrical = s.electrical != null ? s.electrical : DistributionPoleClass.Default();
            Road road = line.ResolveRoad();
            string roadName = road != null ? road.name : string.Empty;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Remove(line);
            Transform holder = MakeHolder(line, roadName);

            Material[] poleMaterials = new Material[] { s.timberMaterial, s.hardwareMaterial };
            Material[] wireMaterials = new Material[] { s.conductorMaterial, s.hardwareMaterial };

            for (int i = 0; i < plan.poles.Count; i++)
            {
                RoadPowerLinePole pole = plan.poles[i];
                PoleMeshKey key = PoleMeshKey.For(shape, electrical, pole.fittings, s.phaseCount, pole.guyCount);
                Make(Named(PoleElement, roadName, i), holder, pole.buttPoint,
                    Quaternion.Euler(0f, pole.yawDegrees, 0f), PoleMeshFactory.Get(key, shape), poleMaterials);
            }

            int pieces = BuildWires(plan, s, shape, electrical, holder, roadName, wireMaterials);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);

            return BuiltLine(plan, pieces, roadName) + MissingMaterialsNote(s) + InstancingNote(s, plan.poles.Count);
        }

        /// <summary> The holder every generated object hangs under. A child of the component rather than a branch of a
        /// city root, because a pole line is HAND-PLACED: a run along a rural road has no city root to hang from, and
        /// <see cref="CityHierarchy.EnsureBranch"/> needs one. The name is still a hierarchy name, so what a build made
        /// is told apart from what the author made by the same parse either way. </summary>
        private static Transform MakeHolder(RoadPowerLine line, string roadName)
        {
            GameObject holder = new GameObject(Named(LineElement, roadName, 0));
            Undo.RegisterCreatedObjectUndo(holder, UndoLabel);
            holder.transform.SetParent(line.transform, false);
            return holder.transform;
        }

        /// <summary> One object, its mesh and its two material slots. Both slots are written even when the author has
        /// filled neither, because every mesh this file places has two submeshes and a renderer whose material array is
        /// shorter than its mesh draws the surplus submesh with the LAST material rather than with none. </summary>
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

        /// <summary> A generated child's name: a hierarchy name whose element id says both which road the run follows
        /// and which of the three kinds of object this is.
        ///
        /// The road name is cleaned of the three characters <see cref="CityHierarchy.TryParseName"/> reads as
        /// structure. A road called <c>Bypass [2]#North</c> is a perfectly ordinary thing for an author to type, and
        /// carrying those brackets into a generated name would produce a name that parses back to the wrong element id
        /// -- which is how Clear would one day fail to recognise its own output. </summary>
        private static string Named(string prefix, string roadName, int ordinal)
        {
            string clean = string.IsNullOrEmpty(roadName) ? "unnamed" : roadName;
            clean = clean.Replace('[', '-').Replace(']', '-').Replace('#', '-');
            return CityHierarchy.NameFor(GeneratedKind.Power, prefix + clean, ordinal);
        }
        #endregion


        #region "The wires"
        /// <summary> String every wire the plan hangs, as a few meshes rather than as one per span.
        ///
        /// The wires themselves are <see cref="RoadPowerLinePlanner.StringConductors"/>'s answer -- the attachments and
        /// the curves are already decided, and this only decides how many of them share a mesh. It groups instead of
        /// merging everything because one mesh for a kilometre of line would be one bounding box as long as the road:
        /// nothing behind the camera could ever be culled, and a single wire edit would rebuild the lot.
        ///
        /// Returns how many pieces were made, which is the figure the sentence reports and the figure a phone budget is
        /// argued in. </summary>
        private static int BuildWires(RoadPowerLinePlan plan, RoadPowerLineSettings s, PoleGeometryClass shape,
            DistributionPoleClass electrical, Transform holder, string roadName, Material[] materials)
        {
            List<ConductorSpan> wires = RoadPowerLinePlanner.StringConductors(plan, s);
            if (wires.Count == 0) { return 0; }

            int perPiece = WiresPerPiece(s);
            List<ConductorSpan> piece = new List<ConductorSpan>(perPiece);
            int pieces = 0;
            for (int i = 0; i < wires.Count; i++)
            {
                piece.Add(wires[i]);
                if (piece.Count < perPiece && i < wires.Count - 1) { continue; }
                MakeWirePiece(piece, shape, electrical, holder, roadName, pieces, materials);
                pieces++;
                piece.Clear();
            }
            return pieces;
        }

        /// <summary> How many conductor spans go into one mesh.
        ///
        /// <see cref="ConductorCurve.SpansPerPiece"/> is stated in SPANS -- eight of them, about 320 m of line -- and
        /// what arrives here is a flat list of wires. The two are reconciled by the order the planner emits, which is
        /// span-major: every wire of one span, then every wire of the next. So a fixed number of ENTRIES is a fixed
        /// number of spans, and the number of wires a span carries is the same figure the planner used -- the phases,
        /// the neutral, the secondary rack when the run has one, and the communication strand when it shares them.
        ///
        /// A span that dropped its rack because a pole lacks the fitting makes a piece cover a span or so more
        /// than eight. That shifts a boundary and nothing else, which is why this is a count and not a search: it is
        /// pure, it is the same answer every time for the same settings (C-06), and D-16 can assert it. </summary>
        public static int WiresPerPiece(RoadPowerLineSettings settings)
        {
            RoadPowerLineSettings s = settings != null ? settings : RoadPowerLineSettings.Default();
            int perSpan = Mathf.Max(1, s.phaseCount) + 1
                          + (s.secondaryRack ? RoadPowerLinePlanner.SecondaryConductorsPerRack : 0)
                          + (s.communicationStrung ? 1 : 0);
            return Mathf.Max(1, ConductorCurve.SpansPerPiece * perSpan);
        }

        /// <summary> One mesh of wires, placed at its own first attachment point.
        ///
        /// The mesh is registered with undo as a created object. It has to be: it is not an asset and nothing
        /// but this one <c>MeshFilter</c> refers to it, so a build that was undone without it would leave the mesh in
        /// memory with no owner -- and the same is true of a rebuild, which is why <see cref="Remove"/> destroys these
        /// meshes and leaves the poles' alone. A pole is showing a mesh the factory owns and shares; a wire piece owns
        /// the one it is showing. </summary>
        private static void MakeWirePiece(List<ConductorSpan> piece, PoleGeometryClass shape,
            DistributionPoleClass electrical, Transform holder, string roadName, int ordinal, Material[] materials)
        {
            Vector3 origin = piece[0].from;
            Mesh mesh = ConductorMesh.Build(piece, origin, electrical.conductorDiameterMetres, shape.wireSides,
                "RCM Conductors " + ordinal.ToString("D4", CultureInfo.InvariantCulture));
            Undo.RegisterCreatedObjectUndo(mesh, UndoLabel);
            Make(Named(WireElement, roadName, ordinal), holder, origin, Quaternion.identity, mesh, materials);
        }
        #endregion


        #region "Clearing what was built"
        /// <summary> Take the built run out of the scene and say what went. Its own undo group, so an author who built,
        /// looked and cleared has two steps in the history and not one ambiguous pair. </summary>
        public static string Clear(RoadPowerLine line)
        {
            if (line == null) { return "Nothing was cleared: the builder was handed no pole line."; }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            int removed = Remove(line);
            Undo.SetCurrentGroupName(ClearLabel);
            Undo.CollapseUndoOperations(undoGroup);
            return ClearedLine(removed);
        }

        /// <summary> Destroy the children this builder made, and only those.
        ///
        /// A name is the whole test. A child survives unless <see cref="CityHierarchy.TryParseKind"/> reads it as
        /// <see cref="GeneratedKind.Power"/>, which is what makes it safe for an author to park their own prefab under
        /// the component -- a transformer they modelled, a bird, a sign -- and still press *Build* again. Nothing else
        /// distinguishes generated from authored here: there is no marker component to add and no list to keep in sync,
        /// because a list that drifts from the scene is worse than no list. <c>CityBranchEditor</c> recognises its
        /// own output the same way.
        ///
        /// Walked in reverse because destroying a child shortens the list it is being read from. </summary>
        private static int Remove(RoadPowerLine line)
        {
            int removed = 0;
            for (int i = line.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = line.transform.GetChild(i);
                GeneratedKind kind;
                if (child == null || !CityHierarchy.TryParseKind(child.name, out kind)) { continue; }
                if (kind != GeneratedKind.Power) { continue; }
                removed += RemoveTree(child.gameObject);
            }
            return removed;
        }

        /// <summary> Destroy one generated subtree, meshes included, and return how many objects went.
        ///
        /// The wire meshes are destroyed BEFORE their objects and through <see cref="Undo.DestroyObjectImmediate"/>
        /// rather than <c>Object.DestroyImmediate</c>. Both halves of that matter. A mesh that is not an asset and is
        /// referenced by nothing but the renderer being destroyed would otherwise sit in memory for the rest of the
        /// session, which over a few dozen rebuilds of a kilometre of line is real memory and not a rounding error; and
        /// a mesh destroyed outside the undo system would not come back when the author undoes the clear, leaving the
        /// restored objects showing nothing. <c>CityGraphWindow</c> pairs a destroy with its owner the same way, so one
        /// undo brings both back.
        ///
        /// Pole meshes are left alone. They belong to <see cref="PoleMeshFactory"/>'s cache and are shared by
        /// every pole with the same parameter set -- destroying one would empty the poles of the next run too. That is
        /// the whole reason <see cref="OwnsItsMesh"/> exists. </summary>
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
        /// again. True for a wire piece, false for a pole and false for anything this builder did not name. Public and
        /// pure because it is the one rule that decides whether a rebuild leaks, and D-16 asserts it without a
        /// scene. </summary>
        public static bool OwnsItsMesh(string name)
        {
            string element;
            if (!CityHierarchy.TryParseName(name, out element)) { return false; }
            return element.StartsWith(WireElement, StringComparison.Ordinal);
        }
        #endregion


        #region "What the build says, pure"
        /// <summary> How many poles make instancing worth a sentence. Below this the draw calls are noise; at eight and
        /// up the run is a row of identical meshes and the material setting decides between a handful of calls
        /// and one per pole. </summary>
        private const int InstancingWorthMentioning = 8;

        /// <summary> What a successful build did. Pure, and public with it, because the sentence is the only report an
        /// author gets and D-16 asserts the wording headlessly -- a sentence assembled inside a button handler is a
        /// sentence no gate has ever read.
        ///
        /// The clearance half is dropped when there is no span to measure. A single-pole run would otherwise print the
        /// plan's initial worst clearance, which is infinity, and infinity reads as a very good clearance rather
        /// than as no measurement. <c>RoadPowerLineEditor</c> drops the unit in the same case for the same reason.
        /// </summary>
        public static string BuiltLine(RoadPowerLinePlan plan, int wirePieces, string roadName)
        {
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }

            string where = string.IsNullOrEmpty(roadName) ? string.Empty : " along " + roadName;
            string text = "Built " + Countable(plan.poles.Count, "pole", "poles") + " and "
                          + Countable(wirePieces, "wire piece", "wire pieces") + where + ", "
                          + Metres(plan.runLengthMetres) + " m of line";
            if (plan.spans.Count > 0 && !double.IsInfinity(plan.worstClearanceMetres))
            {
                text += ", tightest clearance " + Metres(plan.worstClearanceMetres) + " m against "
                        + Metres(plan.requiredClearanceMetres) + " m required";
            }
            return text + ".";
        }

        /// <summary> Why a build did not happen. The refusals are already printed in full above the button, so
        /// this counts them and points at them rather than repeating one of them badly. An empty run is the other case
        /// and it is not a refusal: nothing was refused, there was simply nowhere legal to stand a pole. </summary>
        public static string NotBuildableLine(RoadPowerLinePlan plan)
        {
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }
            if (plan.poles.Count == 0)
            {
                return "Nothing was built: no pole station survived. The run may be shorter than one span, or every "
                       + "station may have fallen inside an intersection corner or on a furniture item it could not be "
                       + "nudged clear of.";
            }
            return "Nothing was built: " + Countable(plan.refusals.Count, "refusal stands", "refusals stand")
                   + " against this run, listed above. The button comes back when they are answered.";
        }

        /// <summary> Which material slots were left empty, said plainly and appended to the build sentence.
        ///
        /// An empty slot is NOT a refusal, nor a warning. A refusal means the run cannot legally be built --
        /// a span too low over a road -- and mixing a missing reference in with that would teach an author to read past
        /// both. The geometry a bare slot produces is the geometry a filled one produces; it is pink. So the run is
        /// built, and the sentence names the slot. </summary>
        public static string MissingMaterialsNote(RoadPowerLineSettings settings)
        {
            RoadPowerLineSettings s = settings != null ? settings : RoadPowerLineSettings.Default();
            List<string> bare = new List<string>();
            if (s.timberMaterial == null) { bare.Add("timber"); }
            if (s.hardwareMaterial == null) { bare.Add("hardware"); }
            if (s.conductorMaterial == null) { bare.Add("conductor"); }
            if (bare.Count == 0) { return string.Empty; }

            return " The " + string.Join(" and the ", bare.ToArray()) + (bare.Count == 1 ? " slot is" : " slots are")
                   + " empty, so that much of the run renders in Unity's missing-material pink. The geometry is right; "
                   + "only the material is absent.";
        }

        /// <summary> The one performance sentence, and only when it applies.
        ///
        /// Every pole with the same parameter set shows the same cached mesh, which is exactly the case GPU instancing
        /// was made for -- but instancing is a material tick, and the material is the author's. So the tool does not
        /// set it: it reports that a run of identical meshes is about to be drawn one call at a time, which is the part
        /// an author cannot see. Static batching is deliberately NOT suggested instead; it would copy every pole's
        /// vertices into the scene and take the instancing away. </summary>
        private static string InstancingNote(RoadPowerLineSettings settings, int poleCount)
        {
            if (settings == null || settings.timberMaterial == null) { return string.Empty; }
            if (poleCount < InstancingWorthMentioning) { return string.Empty; }
            if (settings.timberMaterial.enableInstancing) { return string.Empty; }

            return " These " + Count(poleCount) + " poles share one mesh per parameter set, so GPU instancing"
                   + " would draw them in a few calls -- but it is switched off on the timber material. Tick "
                   + "Enable GPU Instancing on it.";
        }

        /// <summary> What a clear did. Says what it left as well as what it took, because the author's own children
        /// surviving a clear is the behaviour they would otherwise have to test by losing something. </summary>
        public static string ClearedLine(int removed)
        {
            if (removed <= 0)
            {
                return "There was nothing to clear: no built run was found under this component.";
            }
            return "Cleared " + Countable(removed, "object", "objects")
                   + ". Anything you parented here yourself was left alone.";
        }

        /// <summary> A count and the word that agrees with it. One pole is not "1 poles", and a sentence that gets that
        /// wrong is read as a placeholder. </summary>
        private static string Countable(int value, string singular, string plural)
        {
            return Count(value) + " " + (value == 1 ? singular : plural);
        }

        /// <summary> Counts and metres, formatted invariantly, so the sentence does not change shape in a comma-decimal
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

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
    /// <summary> D-14. The scene half of a transmission run: the one place in the tool that turns a
    /// <see cref="TransmissionLineRunPlan"/> into objects, the mirror of <see cref="RoadPowerLineBuilder"/>.
    ///
    /// The division is stated by the component itself -- it computes and refuses, it never mutates a scene, never
    /// touches an asset and never draws. This is the other side of that sentence. Everything here mutates a scene and
    /// nothing here decides anything: where a tower stands, which type it is, how far a cable sags and whether the run
    /// crosses a settlement were all answered before <see cref="Build"/> was called, and a builder that recomputed any
    /// of it would be a second answer to a settled question. The one thing it re-derives is pure GEOMETRY -- the
    /// lateral offset of each phase across the arm -- from <see cref="TransmissionTowerAttachments"/>, the same
    /// arithmetic the steel was drawn with, so a conductor meets the insulator it hangs from rather than a number that
    /// nearly matches.
    ///
    /// So what it does is short. It clears what it built last time, makes one holder, drops one CACHED tower mesh at
    /// every tower, groups the conductors and the earth wire into a few meshes, and returns a sentence saying what
    /// happened.
    ///
    /// The grouping is the phone budget (D-15). A tower is the same mesh at every station that shares its parameter set
    /// -- shape, phase count, type -- so a line's suspension towers are one mesh and many transforms, and one draw call
    /// if the steel material has GPU instancing on, which is why the sentence says so when it does not. Wires cannot be
    /// instanced, every span being a different curve, so they are batched <see cref="ConductorCurve.SpansPerPiece"/>
    /// spans to a mesh.
    ///
    /// BAKED, not runtime. The meshes are built here, in the editor assembly, and saved with the scene, so a shipped
    /// player carries the vertices and not the mesh builder -- the same baked-not-runtime direction the pole line keeps,
    /// and what keeps <c>City/Runtime</c> free of any mesh builder.
    ///
    /// The names come from <see cref="CityHierarchy"/>, which is what lets <see cref="Clear"/> know what it may delete:
    /// a generated child parses as a <see cref="GeneratedKind.Power"/> name; a child the author parked under the run --
    /// a substation model, a survey marker -- does not, and Clear leaves it alone. </summary>
    public static class TransmissionLineRunBuilder
    {
        #region "What a built run is called"
        private const string UndoLabel = "Build transmission run";
        private const string ClearLabel = "Clear transmission run";

        /// <summary> The three element ids a run uses: the holder, a tower and a wire piece. They differ so the three
        /// can share an ordinal without sharing a name, and so <see cref="OwnsItsMesh"/> can tell which objects bring
        /// their own mesh into the scene (the wires) and which show one from the factory cache (the towers). </summary>
        private const string LineElement = "line.xmsn.";
        private const string TowerElement = "tower.xmsn.";
        private const string WireElement = "wire.xmsn.";
        #endregion


        #region "Build"
        /// <summary> Build the run and return the sentence the inspector prints. Taken AS GIVEN: the plan is not
        /// re-planned, not re-checked against the ground and not repaired, because the inspector planned it against the
        /// installed surface reader, printed its refusals, and disabled the button while it was unbuildable. The one
        /// thing tested here is <see cref="TransmissionLineRunPlan.Buildable"/>, because a caller that is not the
        /// inspector -- a gate, a batch -- can reach this method too. </summary>
        public static string Build(TransmissionLineRun run, TransmissionLineRunPlan plan)
        {
            if (run == null) { return "Nothing was built: the builder was handed no transmission run."; }
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }
            if (!plan.Buildable) { return NotBuildableLine(plan); }

            TransmissionLineRunSettings s = run.settings != null ? run.settings : TransmissionLineRunSettings.Default();
            TransmissionTowerClass steel = s.tower != null ? s.tower : TransmissionTowerClass.Default();
            TransmissionLineClass elec = s.electrical != null ? s.electrical : TransmissionLineClass.Default();
            int phaseCount = Mathf.Max(0, s.phaseCount);
            string runName = run.name;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Remove(run);
            Transform holder = MakeHolder(run, runName);

            Material[] towerMaterials = new Material[] { s.steelMaterial, s.insulatorMaterial };
            Material[] conductorMaterials = new Material[] { s.conductorMaterial, s.insulatorMaterial };
            Material[] shieldMaterials = new Material[] { s.shieldWireMaterial, s.insulatorMaterial };

            for (int i = 0; i < plan.towers.Count; i++)
            {
                TransmissionRunTower tower = plan.towers[i];
                TowerMeshKey key = TowerMeshKey.For(steel, phaseCount, tower.type, tower.isTerminal);
                Make(Named(TowerElement, runName, i), holder, tower.basePoint,
                    Quaternion.Euler(0f, tower.yawDegrees, 0f), TowerMeshFactory.Get(key, steel), towerMaterials);
            }

            int pieces = BuildWires(plan, s, steel, elec, holder, runName, conductorMaterials, shieldMaterials);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);

            return BuiltLine(plan, pieces, runName) + MissingMaterialsNote(s)
                   + InstancingNote(s, plan.towers.Count);
        }

        /// <summary> The holder every generated object hangs under: a child of the component, because a transmission
        /// run is HAND-PLACED and has no city root to hang from. The name is still a hierarchy name, so what a build
        /// made is told from what the author made by the same parse either way. </summary>
        private static Transform MakeHolder(TransmissionLineRun run, string runName)
        {
            GameObject holder = new GameObject(Named(LineElement, runName, 0));
            Undo.RegisterCreatedObjectUndo(holder, UndoLabel);
            holder.transform.SetParent(run.transform, false);
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

        /// <summary> A generated child's name: a hierarchy name whose element id says which run it belongs to and which
        /// of the three kinds of object this is. The run name is cleaned of the three characters
        /// <see cref="CityHierarchy.TryParseName"/> reads as structure, the same guard the pole line keeps, so a run an
        /// author called <c>North Feed [2]#</c> cannot produce a name that parses back to the wrong element id and one
        /// day slips past Clear. </summary>
        private static string Named(string prefix, string runName, int ordinal)
        {
            string clean = string.IsNullOrEmpty(runName) ? "unnamed" : runName;
            clean = clean.Replace('[', '-').Replace(']', '-').Replace('#', '-');
            return CityHierarchy.NameFor(GeneratedKind.Power, prefix + clean, ordinal);
        }
        #endregion


        #region "The wires"
        /// <summary> String every conductor and every earth wire the plan hangs, as a few meshes rather than as one per
        /// span. The conductors and the shield wires are built as SEPARATE piece sets: they carry different diameters,
        /// different materials and different sag heights, so batching them together would be a mesh with two materials'
        /// worth of geometry in one submesh. Returns how many pieces were made, the figure the sentence reports and the
        /// figure a phone budget is argued in. </summary>
        private static int BuildWires(TransmissionLineRunPlan plan, TransmissionLineRunSettings s,
            TransmissionTowerClass steel, TransmissionLineClass elec, Transform holder, string runName,
            Material[] conductorMaterials, Material[] shieldMaterials)
        {
            int phaseCount = Mathf.Max(0, s.phaseCount);
            List<ConductorSpan> conductors = new List<ConductorSpan>();
            List<ConductorSpan> shields = new List<ConductorSpan>();
            StringSpans(plan, steel, elec, phaseCount, conductors, shields);

            int ordinal = 0;
            ordinal = EmitPieces(conductors, ConductorCurve.SpansPerPiece * Mathf.Max(1, phaseCount),
                steel.conductorDiameterMetres, steel.wireSides, holder, runName, conductorMaterials, ordinal);
            ordinal = EmitPieces(shields, ConductorCurve.SpansPerPiece * Mathf.Max(1, steel.shieldWireCount),
                steel.shieldWireDiameterMetres, steel.wireSides, holder, runName, shieldMaterials, ordinal);
            return ordinal;
        }

        /// <summary> Turn the plan's towers and spans into hanging conductors and earth wires, span-major (every wire of
        /// one span, then the next) so a fixed number of entries is a fixed number of spans and the piece boundaries
        /// fall predictably. The phases reuse the span's ONE solved catenary offset sideways across the arm; the earth
        /// wire is solved from the two towers' shield heights at its own authored sag ratio because it is a separate
        /// mechanical member, not another phase conductor. </summary>
        private static void StringSpans(TransmissionLineRunPlan plan, TransmissionTowerClass steel,
            TransmissionLineClass elec, int phaseCount, List<ConductorSpan> conductors, List<ConductorSpan> shields)
        {
            for (int i = 0; i < plan.spans.Count; i++)
            {
                TransmissionRunSpan span = plan.spans[i];
                TransmissionRunTower a = plan.towers[span.fromTowerIndex];
                TransmissionRunTower b = plan.towers[span.toTowerIndex];
                TransmissionTowerAttachments at = TransmissionTowerAttachments.For(steel, phaseCount, a.type,
                    a.isTerminal);
                TransmissionTowerAttachments bt = TransmissionTowerAttachments.For(steel, phaseCount, b.type,
                    b.isTerminal);

                int phases = at.phaseOffsets != null ? at.phaseOffsets.Length : 0;
                for (int p = 0; p < phases; p++)
                {
                    Vector3 from = AttachPoint(a, at.phaseOffsets[p], at.conductorAttachY);
                    Vector3 to = AttachPoint(b, PhaseOffsetAt(bt, p), bt.conductorAttachY);
                    conductors.Add(new ConductorSpan(from, to, span.catenary));
                }

                float[] fromApex = TowerMeshFactory.ShieldApexOffsets(at, steel.shieldWireCount);
                float[] toApex = TowerMeshFactory.ShieldApexOffsets(bt, steel.shieldWireCount);
                Catenary shieldCurve = TransmissionLine.Solve(span.spanMetres, a.shieldWireWorldY, b.shieldWireWorldY,
                    elec.shieldWireSagRatio);
                int wires = Mathf.Min(fromApex.Length, toApex.Length);
                for (int w = 0; w < wires; w++)
                {
                    Vector3 from = AttachPoint(a, fromApex[w], at.shieldWireY);
                    Vector3 to = AttachPoint(b, toApex[w], bt.shieldWireY);
                    shields.Add(new ConductorSpan(from, to, shieldCurve));
                }
            }
        }

        /// <summary> A wire attachment in world space: a point on the tower's arm (a lateral offset, a height above the
        /// base) rotated into the tower's facing and placed at its seated base. The tower's yaw points local +Z along
        /// the line, so local X is the arm and a lateral offset lands across it. This is why the height by itself would
        /// not do: the same offset at two towers of a corner lands on two different arm directions, which is the whole
        /// reason a corner needs the bisector yaw the planner gave it. </summary>
        private static Vector3 AttachPoint(TransmissionRunTower tower, float lateralOffset, float localY)
        {
            Vector3 local = new Vector3(lateralOffset, localY, 0f);
            return tower.basePoint + Quaternion.Euler(0f, tower.yawDegrees, 0f) * local;
        }

        /// <summary> Phase p's lateral offset on a tower, tolerant of a tower that resolved to a different phase count
        /// than its neighbour (it never does within one run, but reading past the end of an array to save a check is
        /// how a builder crashes on the one run that does). </summary>
        private static float PhaseOffsetAt(TransmissionTowerAttachments a, int p)
        {
            if (a.phaseOffsets == null || a.phaseOffsets.Length == 0) { return 0f; }
            if (p < 0) { p = 0; }
            if (p >= a.phaseOffsets.Length) { p = a.phaseOffsets.Length - 1; }
            return a.phaseOffsets[p];
        }

        /// <summary> Emit a list of wires as grouped meshes, continuing an ordinal so conductor and shield pieces never
        /// share a name. Returns the ordinal after the last piece, which is the running total of pieces. </summary>
        private static int EmitPieces(List<ConductorSpan> wires, int perPiece, float diameter, int sides,
            Transform holder, string runName, Material[] materials, int startOrdinal)
        {
            if (wires.Count == 0) { return startOrdinal; }
            int perP = Mathf.Max(1, perPiece);
            List<ConductorSpan> piece = new List<ConductorSpan>(perP);
            int ordinal = startOrdinal;
            for (int i = 0; i < wires.Count; i++)
            {
                piece.Add(wires[i]);
                if (piece.Count < perP && i < wires.Count - 1) { continue; }
                MakeWirePiece(piece, diameter, sides, holder, runName, ordinal, materials);
                ordinal++;
                piece.Clear();
            }
            return ordinal;
        }

        /// <summary> One mesh of wires, placed at its own first attachment point. The mesh is registered with undo as a
        /// created object because it is not an asset and nothing but this one <c>MeshFilter</c> refers to it, so a build
        /// that was undone without it -- or a rebuild -- would leave it in memory with no owner. That is why
        /// <see cref="Remove"/> destroys these meshes and leaves the towers' shared meshes alone. </summary>
        private static void MakeWirePiece(List<ConductorSpan> piece, float diameter, int sides, Transform holder,
            string runName, int ordinal, Material[] materials)
        {
            Vector3 origin = piece[0].from;
            Mesh mesh = ConductorMesh.Build(piece, origin, diameter, sides,
                "RCM Transmission Wires " + ordinal.ToString("D4", CultureInfo.InvariantCulture));
            Undo.RegisterCreatedObjectUndo(mesh, UndoLabel);
            Make(Named(WireElement, runName, ordinal), holder, origin, Quaternion.identity, mesh, materials);
        }
        #endregion


        #region "Clearing what was built"
        /// <summary> Take the built run out of the scene and say what went. Its own undo group, so an author who built,
        /// looked and cleared has two steps in the history and not one ambiguous pair. </summary>
        public static string Clear(TransmissionLineRun run)
        {
            if (run == null) { return "Nothing was cleared: the builder was handed no transmission run."; }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            int removed = Remove(run);
            Undo.SetCurrentGroupName(ClearLabel);
            Undo.CollapseUndoOperations(undoGroup);
            return ClearedLine(removed);
        }

        /// <summary> Destroy the children this builder made, and only those. A name is the whole test: a child survives
        /// unless <see cref="CityHierarchy.TryParseKind"/> reads it as <see cref="GeneratedKind.Power"/>, which is what
        /// makes it safe for an author to park their own model under the component and still press Build again. Walked
        /// in reverse because destroying a child shortens the list it is being read from. </summary>
        private static int Remove(TransmissionLineRun run)
        {
            int removed = 0;
            for (int i = run.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = run.transform.GetChild(i);
                GeneratedKind kind;
                if (child == null || !CityHierarchy.TryParseKind(child.name, out kind)) { continue; }
                if (kind != GeneratedKind.Power) { continue; }
                removed += RemoveTree(child.gameObject);
            }
            return removed;
        }

        /// <summary> Destroy one generated subtree, meshes included, and return how many objects went. The wire meshes
        /// are destroyed BEFORE their objects and through <see cref="Undo.DestroyObjectImmediate"/>, so they neither
        /// leak nor fail to return when the clear is undone. The tower meshes are left alone: they belong to
        /// <see cref="TowerMeshFactory"/>'s cache and are shared by every tower of the same parameter set, so destroying
        /// one would empty the towers of the next run too. That is the whole reason <see cref="OwnsItsMesh"/> exists.
        /// </summary>
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
        /// again. True for a wire piece, false for a tower and false for anything this builder did not name. Public and
        /// pure because it is the one rule that decides whether a rebuild leaks, and D-16 asserts it without a scene.
        /// </summary>
        public static bool OwnsItsMesh(string name)
        {
            string element;
            if (!CityHierarchy.TryParseName(name, out element)) { return false; }
            return element.StartsWith(WireElement, StringComparison.Ordinal);
        }
        #endregion


        #region "What the build says, pure"
        /// <summary> How many towers make instancing worth a sentence. Below this the draw calls are noise; above it the
        /// run is a row of identical suspension towers and the material setting decides between a handful of calls and
        /// one per tower. </summary>
        private const int InstancingWorthMentioning = 8;

        /// <summary> What a successful build did. Pure and public, because the sentence is the only report an author
        /// gets and D-16 asserts the wording headlessly. The clearance half is dropped when there is no span to measure,
        /// so a single-tower degenerate run does not print the plan's initial worst clearance of infinity as though it
        /// were a very good clearance. </summary>
        public static string BuiltLine(TransmissionLineRunPlan plan, int wirePieces, string runName)
        {
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }

            string where = string.IsNullOrEmpty(runName) ? string.Empty : " for " + runName;
            string text = "Built " + Countable(plan.towers.Count, "tower", "towers") + " and "
                          + Countable(wirePieces, "wire piece", "wire pieces") + where + ", "
                          + Metres(plan.routeLengthMetres) + " m of line";
            if (plan.spans.Count > 0 && !double.IsInfinity(plan.worstClearanceMetres))
            {
                text += ", tightest clearance " + Metres(plan.worstClearanceMetres) + " m against "
                        + Metres(plan.requiredClearanceMetres) + " m required";
            }
            if (plan.towersWithHangingFoot > 0)
            {
                text += ". " + Countable(plan.towersWithHangingFoot, "tower has", "towers have")
                        + " a foot in air -- built anyway, but move the corner or accept the cut";
            }
            return text + ".";
        }

        /// <summary> Why a build did not happen. The refusals are already printed in full above the button, so this
        /// counts them and points at them rather than repeating one badly. Too few towers is the other case and it is
        /// not a refusal: nothing was refused, the route simply did not resolve to a line. </summary>
        public static string NotBuildableLine(TransmissionLineRunPlan plan)
        {
            if (plan == null) { return "Nothing was built: the run has not been planned yet."; }
            if (plan.towers.Count < 2)
            {
                return "Nothing was built: the route resolved to fewer than two towers, so there is no span to string. "
                       + "A run needs at least two corner points far enough apart to hold a span.";
            }
            return "Nothing was built: " + Countable(plan.refusals.Count, "refusal stands", "refusals stand")
                   + " against this run, listed above. The button comes back when they are answered.";
        }

        /// <summary> Which material slots were left empty, said plainly and appended to the build sentence. An empty
        /// slot is NOT a refusal: the geometry a bare slot produces is the geometry a filled one produces; it is pink.
        /// So the run is built and the sentence names the slot, the same contract the pole line keeps. </summary>
        public static string MissingMaterialsNote(TransmissionLineRunSettings settings)
        {
            TransmissionLineRunSettings s = settings != null ? settings : TransmissionLineRunSettings.Default();
            List<string> bare = new List<string>();
            if (s.steelMaterial == null) { bare.Add("steel"); }
            if (s.insulatorMaterial == null) { bare.Add("insulator"); }
            if (s.conductorMaterial == null) { bare.Add("conductor"); }
            if (s.shieldWireMaterial == null) { bare.Add("earth wire"); }
            if (bare.Count == 0) { return string.Empty; }

            return " The " + string.Join(" and the ", bare.ToArray()) + (bare.Count == 1 ? " slot is" : " slots are")
                   + " empty, so that much of the run renders in Unity's missing-material pink. The geometry is right; "
                   + "only the material is absent.";
        }

        /// <summary> The one performance sentence, and only when it applies. Every tower of the same parameter set shows
        /// the same cached mesh, which is exactly the case GPU instancing was made for -- but instancing is a material
        /// tick, and the material is the author's. So the tool reports that a row of identical towers is about to be
        /// drawn one call at a time rather than setting it. Static batching is deliberately NOT suggested: it would copy
        /// every tower's vertices into the scene and take the instancing away. </summary>
        private static string InstancingNote(TransmissionLineRunSettings settings, int towerCount)
        {
            if (settings == null || settings.steelMaterial == null) { return string.Empty; }
            if (towerCount < InstancingWorthMentioning) { return string.Empty; }
            if (settings.steelMaterial.enableInstancing) { return string.Empty; }

            return " These " + Count(towerCount) + " towers share one mesh per parameter set, so GPU instancing"
                   + " would draw the repeated ones in a few calls -- but it is switched off on the steel material. "
                   + "Tick Enable GPU Instancing on it.";
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

        private static string Countable(int value, string singular, string plural)
        {
            return Count(value) + " " + (value == 1 ? singular : plural);
        }

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

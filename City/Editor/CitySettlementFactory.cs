// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-15 / C-3. The one place a settlement comes into a scene, and the two ways an author asks for it: the
    /// <i>Add city</i> button on the road system's own inspector, and <i>Build settlements from roads</i>, which walks
    /// the network the author drew and puts a settlement at every free road end that declares a settlement role.
    ///
    /// Both paths land here rather than in the inspector file, because they are the same act. An inspector that carried
    /// the creation code and a menu item that carried a second copy is precisely the shape this register keeps finding
    /// broken: two implementations of one outcome, free to drift, and only one of them ever corrected.
    ///
    /// THE SWEEP IS IDEMPOTENT. A terminus that already carries a settlement is left exactly as it is and reported as
    /// already done, so an author can draw three more roads and press the button again without every existing city
    /// being rebuilt underneath them. Identity is <c>CityAuthoring.sourceRoad</c> plus <c>sourceEnd</c> -- the
    /// provenance C-1 records for this reason -- and not the GameObject's name, which an author is free to rename.
    ///
    /// It refuses rather than half-building. A road that declares a role but has no free end -- both ends swallowed by
    /// intersections -- produces a named reason and no GameObject, because a settlement placed at an arbitrary point on
    /// such a road would be the tool inventing a location the author did not draw.
    ///
    /// ONE UNDO STEP per press, for the whole sweep. Unity cannot roll back an asset write, so the default graph asset
    /// is FOUND OR CREATED ONCE and shared: pressing the button twice does not litter the project with graphs, and an
    /// undo leaves no orphan behind. That sharing is also correct rather than merely tidy -- a <see cref="CityGraph"/>
    /// holds nothing scene-specific, which is the whole of C-2's reasoning, so one graph legitimately drives every
    /// settlement in the scene at its own seed and its own region. An author who wants one city to diverge duplicates
    /// the asset and assigns the copy.
    ///
    /// It creates and reports. It computes nothing about WHERE a settlement's region should reach -- that is C-14's
    /// proposal from the terminus and the order's fitted area -- and it maps no role to an order, which is C-15's
    /// table. This file puts the component in the scene, points it at a graph, records which road end it came from,
    /// and says what it did. </summary>
    public static class CitySettlementFactory
    {
        /// <summary> Where the shared default graph lives, under the tool's own root as
        /// <c>RoadEditorUtility.GetBasePath</c> reports it -- the same convention every other asset this tool writes
        /// already follows, rather than a second folder rule invented here. </summary>
        private const string GraphFolderUnderTool = "City/Graphs";

        /// <summary> The shared default graph's file name. </summary>
        private const string DefaultGraphFileName = "DefaultCityGraph.asset";

        /// <summary> Creates one settlement under the road system and returns it, or refuses with the reason and leaves
        /// the scene untouched.
        ///
        /// The graph is resolved FIRST, before anything is created, so a project that cannot be written to produces a
        /// refusal rather than a city pointing at nothing. Then one GameObject, its five branches, and the two
        /// provenance fields C-1 records. The seed is derived from the settlement's name through the tool's own mixer,
        /// so it is distinct per city and the SAME city rebuilt from the same road end comes back with the same seed --
        /// a random default would make a scene change simply by being opened. </summary>
        public static bool CreateCity(RoadSystem system, string proposedName, Vector3 worldCentre, Road sourceRoad,
            RoadEnd sourceEnd, bool select, out CityAuthoring city, out string reason)
        {
            city = null;
            if (system == null)
            {
                reason = "there is no road system to put a settlement under. A city is a sibling of the roads, under"
                         + " the same RoadSystem parent, so one has to exist first -- Window > Road City Master >"
                         + " Create road system makes one";
                return false;
            }
            string graphWhy;
            CityGraph graph = FindOrCreateDefaultGraph(out graphWhy);
            if (graph == null) { reason = graphWhy; return false; }

            string wanted = UniqueChildName(system.transform,
                string.IsNullOrEmpty(proposedName) ? CityHierarchy.RootName : proposedName);
            GameObject created = new GameObject(wanted);
            Undo.RegisterCreatedObjectUndo(created, "Add city");
            created.transform.SetParent(system.transform, false);
            created.transform.position = worldCentre;

            city = created.AddComponent<CityAuthoring>();
            city.graph = graph;
            city.seed = SeedForName(wanted);
            city.sourceRoad = sourceRoad;
            city.sourceEnd = sourceEnd;

            // The five branches, so the Hierarchy shows the structure the moment the city appears rather than after the
            // first bake. They are children of an object whose creation is registered above, so the one undo step takes
            // them with it -- registering each of them again would put five entries in the undo stack for one press.
            IList<CityBranch> branches = CityHierarchy.All();
            for (int i = 0; i < branches.Count; i++)
            {
                CityHierarchy.EnsureBranch(created.transform, branches[i]);
            }

            if (select) { EngineIntegration.SetActiveGameObject(created); }
            reason = string.Empty;
            return true;
        }

        /// <summary> Where a settlement goes when no road end put it there: the point the Scene view is looking at, which
        /// is Unity's own convention for "create it here", and the world origin when there is no Scene view to ask --
        /// batchmode, or a project opened with no scene tab.
        ///
        /// It lives here rather than in each caller because BOTH hand-add paths need the same answer, and two copies of
        /// it would be free to drift into two different meanings of "here". The author drags it from wherever it lands;
        /// C-14 is what proposes a region around it. </summary>
        public static Vector3 DefaultPlacementPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            return view == null ? Vector3.zero : view.pivot;
        }

        /// <summary> Walks every road under the system that declares a settlement role, and puts one settlement at every
        /// FREE end -- the ends <c>RoadTerminus.IsTerminus</c> reports, which is any endpoint node that is not part of an
        /// intersection. That is the author's own rule: the roads determine the settlements, and a road's free end is
        /// where a place begins.
        ///
        /// Both ends are considered, because a village connector legitimately ends in a village at each end. A road
        /// whose both ends are swallowed by intersections is reported as declaring a role it cannot seed, which is the
        /// useful outcome -- the alternative is putting a city somewhere on the road that the author never drew.
        ///
        /// The whole sweep is one undo step, and every settlement that already exists at a terminus is left untouched.
        /// </summary>
        public static bool BuildSettlementsFromRoads(RoadSystem system, out string report)
        {
            if (system == null)
            {
                report = "there is no road system to walk, so there are no roads to read settlement roles from";
                return false;
            }
            Road[] roads = system.GetComponentsInChildren<Road>();
            CityAuthoring[] existing = system.GetComponentsInChildren<CityAuthoring>();
            List<string> refusals = new List<string>();
            int rolesFound = 0, createdCount = 0, alreadyThere = 0, rolelessRoads = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            for (int r = 0; roads != null && r < roads.Length; r++)
            {
                Road road = roads[r];
                if (road == null) { continue; }
                if (road.settlementRole == SettlementRoleEnum.None) { rolelessRoads++; continue; }
                rolesFound++;
                int terminiOnThisRoad = 0;
                bool structurallyUnreadable = false;
                for (int e = 0; e < 2 && !structurallyUnreadable; e++)
                {
                    RoadEnd end = e == 0 ? RoadEnd.Start : RoadEnd.End;
                    SplineN endNode;
                    string why;
                    if (!TryEndNode(road, end, out endNode, out why))
                    {
                        refusals.Add(why);
                        structurallyUnreadable = true;
                        break;
                    }
                    if (!RoadTerminus.IsTerminus(endNode.isIntersection)) { continue; }
                    terminiOnThisRoad++;
                    if (HasSettlementAt(existing, road, end)) { alreadyThere++; continue; }
                    CityAuthoring made;
                    string createWhy;
                    if (!CreateCity(system, SettlementNameFor(road, end), endNode.pos, road, end, false, out made,
                            out createWhy))
                    {
                        refusals.Add(createWhy);
                        continue;
                    }
                    createdCount++;
                }
                if (!structurallyUnreadable && terminiOnThisRoad == 0)
                {
                    refusals.Add("the road " + road.name + " declares the settlement role " + road.settlementRole
                                 + " but has no free end -- both of its endpoints sit inside intersections, so there is"
                                 + " nowhere on it a settlement begins. Either it is a through road that should declare"
                                 + " no role, or the end a place was meant to stand at has since been joined to another"
                                 + " road");
                }
            }

            Undo.SetCurrentGroupName("Build settlements from roads");
            Undo.CollapseUndoOperations(undoGroup);
            report = ComposeSweepReport(rolesFound, rolelessRoads, createdCount, alreadyThere, refusals);
            return refusals.Count == 0;
        }

        /// <summary> The sweep's one report line, or its numbered refusals. It says what happened to every road it
        /// walked -- created, already there, skipped as a through road, refused -- because a button that silently does
        /// nothing when no road declares a role is indistinguishable from a broken one. </summary>
        private static string ComposeSweepReport(int rolesFound, int rolelessRoads, int createdCount, int alreadyThere,
            List<string> refusals)
        {
            StringBuilder builder = new StringBuilder();
            if (rolesFound == 0)
            {
                builder.Append("no road under this system declares a settlement role, so the sweep had nothing to"
                               + " place. Set the role on the roads you drew as the settlement network -- main highway,"
                               + " intercity, intercity connecting towns, intercity connecting villages -- and the free"
                               + " end of each becomes a settlement");
                if (rolelessRoads > 0)
                {
                    builder.Append(". ").Append(rolelessRoads).Append(" road")
                           .Append(rolelessRoads == 1 ? " was" : "s were").Append(" walked and left alone");
                }
                return builder.ToString();
            }
            builder.Append(rolesFound).Append(" road").Append(rolesFound == 1 ? "" : "s")
                   .Append(" declare a settlement role: ").Append(createdCount).Append(" settlement")
                   .Append(createdCount == 1 ? "" : "s").Append(" created, ").Append(alreadyThere)
                   .Append(" already there and left untouched");
            if (rolelessRoads > 0)
            {
                builder.Append(", ").Append(rolelessRoads).Append(" through road")
                       .Append(rolelessRoads == 1 ? "" : "s").Append(" skipped");
            }
            if (refusals.Count == 0)
            {
                builder.Append(", and nothing refused");
                return builder.ToString();
            }
            builder.Append(", and ").Append(refusals.Count)
                   .Append(refusals.Count == 1 ? " refusal:" : " refusals:");
            for (int i = 0; i < refusals.Count; i++)
            {
                builder.Append("\n  ").Append(i + 1).Append(". ").Append(refusals[i]);
            }
            return builder.ToString();
        }

        /// <summary> The spline node at one end of a road, or a refusal naming what is unreadable. It fails ONLY on a
        /// road that cannot be read at all -- no spline, fewer than two nodes, a missing node. Whether that node is a
        /// free end is a separate question, and <c>RoadTerminus.IsTerminus</c> is the one place it is asked; folding the
        /// two together here would report a perfectly ordinary through road as a fault. </summary>
        private static bool TryEndNode(Road road, RoadEnd end, out SplineN node, out string reason)
        {
            node = null;
            List<SplineN> nodes = road.spline == null ? null : road.spline.nodes;
            if (nodes == null || nodes.Count < 2)
            {
                reason = "the road " + road.name + " declares the settlement role " + road.settlementRole
                         + " but holds fewer than two nodes, so it has no ends to read. Draw the road first -- the"
                         + " settlements come from the network the author drew, so there is nothing to site until it"
                         + " exists";
                return false;
            }
            node = end == RoadEnd.Start ? nodes[0] : nodes[nodes.Count - 1];
            if (node == null)
            {
                reason = "the " + (end == RoadEnd.Start ? "first" : "last") + " node of the road " + road.name
                         + " is missing, so where that end of it lies cannot be read. Re-adding the node repairs it";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> Whether a settlement already stands at this road end. Identity is the provenance pair C-1 records
        /// -- the road and which of its two ends -- and not the GameObject's name or its position, either of which an
        /// author is free to change without meaning "build me another one". This is what makes the sweep idempotent.
        /// </summary>
        private static bool HasSettlementAt(CityAuthoring[] existing, Road road, RoadEnd end)
        {
            for (int i = 0; existing != null && i < existing.Length; i++)
            {
                CityAuthoring candidate = existing[i];
                if (candidate == null) { continue; }
                if (candidate.sourceRoad == road && candidate.sourceEnd == end) { return true; }
            }
            return false;
        }

        /// <summary> The name a swept settlement is given: the branch root's name, the road it came from, and which end
        /// -- so the Hierarchy reads as provenance at a glance, and a village at each end of one connector is two
        /// distinguishable entries rather than "City" and "City (1)". </summary>
        private static string SettlementNameFor(Road road, RoadEnd end)
        {
            return CityHierarchy.RootName + " - " + road.name + " - " + (end == RoadEnd.Start ? "start" : "end");
        }

        /// <summary> A name no sibling already holds, so two settlements never share one and Unity is never left to
        /// append its own "(1)". The loop tries as many candidates as there are children plus one, which by the
        /// pigeonhole principle is one more than the number of names that can be taken -- the trailing return exists
        /// because the compiler cannot see that, not because a case reaches it. </summary>
        private static string UniqueChildName(Transform parent, string wanted)
        {
            if (parent == null || parent.Find(wanted) == null) { return wanted; }
            int limit = parent.childCount + 2;
            for (int suffix = 2; suffix <= limit; suffix++)
            {
                string candidate = wanted + " " + suffix;
                if (parent.Find(candidate) == null) { return candidate; }
            }
            return wanted + " " + limit;
        }

        /// <summary> The seed a new settlement is born with: the stable FNV-1a of its name, folded to 32 bits.
        ///
        /// It is <see cref="DeterministicHash.Fnv1a64"/> and not <c>string.GetHashCode</c> deliberately -- C-06's smell
        /// test bans the latter precisely because it is not stable across processes, and a seed that changed when Unity
        /// restarted would rebuild every city differently for no authored reason. Since the swept name carries the road
        /// and the end, the SAME road end always yields the same seed, while two settlements never share one.
        ///
        /// This mints a city seed; it does not derive a stream from one. Splitting a city seed into the per-stage and
        /// per-item streams the operators run on is <c>CitySeed</c>'s job, and this file does not duplicate it.
        /// </summary>
        private static int SeedForName(string settlementName)
        {
            ulong stable = DeterministicHash.Fnv1a64(settlementName);
            return unchecked((int)(uint)((stable >> 32) ^ stable));
        }

        /// <summary> The shared default graph: the one at <c>&lt;tool&gt;/City/Graphs/DefaultCityGraph.asset</c> if it is
        /// already there, otherwise a new one wired to the default chain. Returns null and a named reason rather than
        /// throwing, because both callers are buttons.
        ///
        /// It never overwrites. A different asset already occupying that exact path is reported instead of being
        /// replaced -- <c>AssetDatabase.CreateAsset</c> would silently destroy it, and an author who put something there
        /// deliberately would lose it to a button they pressed to add a city.
        ///
        /// A graph it creates is VALIDATED before it is handed back, and a graph that fails is deleted again so the
        /// project is left exactly as it was. That is not defensive noise: the chain is wired through
        /// <see cref="TryWireEdge"/> precisely so it cannot fail, and this is the assertion that keeps that true when a
        /// later task adds a node to the chain. </summary>
        private static CityGraph FindOrCreateDefaultGraph(out string reason)
        {
            string folder, folderWhy;
            if (!TryGraphFolder(out folder, out folderWhy)) { reason = folderWhy; return null; }
            string assetPath = folder + "/" + DefaultGraphFileName;
            CityGraph found = AssetDatabase.LoadAssetAtPath<CityGraph>(assetPath);
            if (found != null) { reason = string.Empty; return found; }
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                reason = "something that is not a city graph already occupies " + assetPath + ", and writing the default"
                         + " graph over it would destroy it. Move or rename that asset, or assign a graph to the city by"
                         + " hand";
                return null;
            }

            CityGraph made = ScriptableObject.CreateInstance<CityGraph>();
            made.name = Path.GetFileNameWithoutExtension(DefaultGraphFileName);
            AssetDatabase.CreateAsset(made, assetPath);
            WireDefaultChain(made);
            EditorUtility.SetDirty(made);
            AssetDatabase.SaveAssets();

            string validation;
            if (!made.Validate(out validation))
            {
                AssetDatabase.DeleteAsset(assetPath);
                reason = "the default city graph was built and then refused by its own validator, so it was removed"
                         + " again rather than left for the bake to trip over -- " + validation;
                return null;
            }
            reason = string.Empty;
            return made;
        }

        /// <summary> The folder the default graph lives in, created if it is not there yet, as an <c>Assets/</c>-relative
        /// path with forward slashes.
        ///
        /// The root comes from <c>RoadEditorUtility.GetBasePath</c>, which is the same answer every other asset this tool
        /// writes already uses -- and which THROWS when the tool has been moved somewhere it does not recognise. That
        /// throw is caught here and turned into the refusal, because it travels up through a button press otherwise. Its
        /// answer arrives with the platform's separator, hence the replace: the precedent is
        /// <c>CityDataRoundTripTest</c>, which does the same. </summary>
        private static bool TryGraphFolder(out string folder, out string reason)
        {
            folder = null;
            string root;
            try
            {
                root = RoadEditorUtility.GetBasePath().Replace('\\', '/');
            }
            catch (Exception error)
            {
                reason = "the tool's own folder could not be located, so there is nowhere to put the default city graph"
                         + " -- " + error.Message;
                return false;
            }
            if (!AssetDatabase.IsValidFolder(root))
            {
                reason = "the tool reports its own folder as " + root + ", which the project does not hold as a folder,"
                         + " so the default city graph has nowhere to be written";
                return false;
            }
            string[] segments = GraphFolderUnderTool.Split('/');
            string current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current,
                        segments[i])))
                {
                    reason = "the folder " + next + " could not be created, so the default city graph has nowhere to be"
                             + " written. A read-only project folder is the usual cause";
                    return false;
                }
                current = next;
            }
            folder = current;
            reason = string.Empty;
            return true;
        }

        /// <summary> Wires the default chain into a freshly created graph: one node per operator this build actually
        /// ships, in bake order, each with its operator instance stored INSIDE the graph asset as a sub-asset.
        ///
        /// Sub-assets rather than separate files, for the reason the class doc gives about undo: one file appears, one
        /// file is deleted, and an operator cannot be left orphaned in the project by a graph that went away. It also
        /// means duplicating the asset duplicates its operators, so an author who wants one city to diverge gets their
        /// own copies to edit rather than shared ones that change every city at once.
        ///
        /// Two nodes today, both in <c>LoadOrGenerate</c>: the street network, then the block subdivision that reads a
        /// network -- and since C-5 they ARE wired to each other, which is the change this method waited for. The older
        /// form of this summary said an edge between them was the one thing <c>CityGraph.Validate</c> refused, and that
        /// was true while a wire could only be checked against the operators' CLR types: <c>StreetNetworkOperator</c>
        /// emits a <c>StreetNetwork</c> and <c>BlockSubdivisionOperator</c> reads a <c>BlockSubdivisionInput</c>, so
        /// assignability failed. The catalogue's port sheet describes what actually travels -- <c>network</c> out,
        /// <c>network</c> in, a <c>StreetNetwork</c> either side -- so <see cref="TryWireEdge"/> now finds that pair and
        /// the default graph arrives wired, exactly as this method's own doc predicted it would. </summary>
        private static void WireDefaultChain(CityGraph graph)
        {
            CityGraphNode network = AddOperatorNode(graph, CityNodeKind.StreetNetwork, typeof(StreetNetworkOperator),
                new Vector2(0f, 0f));
            CityGraphNode blocks = AddOperatorNode(graph, CityNodeKind.BlockSubdivision,
                typeof(BlockSubdivisionOperator), new Vector2(280f, 0f));
            TryWireEdge(graph, network, blocks);
        }

        /// <summary> Adds one node running a fresh instance of an operator type, stored inside the graph asset. Returns
        /// the node, or null when the kind or the type could not be resolved -- which leaves the graph with one node fewer
        /// rather than with a node wiring nothing, the state <see cref="CityGraph.Validate"/> exists to refuse.
        ///
        /// The node itself is built by <c>CityNodeCatalog.CreateNode</c> and not here. That is the rule C-5 introduced and
        /// this method obeys rather than restates: one place creates nodes, so the title, the stage, the kind and every
        /// parameter row come from the catalogue, and a node this factory writes cannot differ from one C-8's search
        /// window drops on the canvas. A second creation path is how the two would drift. </summary>
        private static CityGraphNode AddOperatorNode(CityGraph graph, CityNodeKind kind, Type operatorType,
            Vector2 canvasPosition)
        {
            if (graph == null || operatorType == null) { return null; }
            CityNodeDescriptor descriptor;
            if (!CityNodeCatalog.TryGet(kind, out descriptor)) { return null; }
            ScriptableObject op = ScriptableObject.CreateInstance(operatorType);
            if (op == null) { return null; }
            op.name = descriptor.displayName;
            AssetDatabase.AddObjectToAsset(op, graph);
            CityGraphNode node = CityNodeCatalog.CreateNode(descriptor);
            node.operatorAsset = op;
            node.canvasPosition = canvasPosition;
            graph.nodes.Add(node);
            return node;
        }

        /// <summary> Wires one node's output into another's input IF the two carry the same payload, and reports whether
        /// it did.
        ///
        /// The check is the same one <see cref="CityGraph.Validate"/> applies -- so the factory can never write a graph
        /// its own validator would then refuse -- and since C-5 that check is the catalogue's PORT SHEET rather than the
        /// operators' CLR types. It asks the two entries for the first output/input pair whose declared payload matches
        /// and names those two ports on the edge. Names rather than indices, because <see cref="CityGraphEdge"/> stores
        /// names and reordering ports must never silently move a wire.
        ///
        /// Declining to wire is still a legitimate outcome, not a failure: the nodes run in stage and then list order, and
        /// an unwired input is what an author fills in on C-8's canvas. </summary>
        private static bool TryWireEdge(CityGraph graph, CityGraphNode from, CityGraphNode to)
        {
            if (graph == null || from == null || to == null) { return false; }
            CityNodeDescriptor fromEntry, toEntry;
            string why;
            if (!CityNodeCatalog.TryGetForNode(from, out fromEntry, out why)) { return false; }
            if (!CityNodeCatalog.TryGetForNode(to, out toEntry, out why)) { return false; }
            for (int o = 0; o < fromEntry.outputs.Count; o++)
            {
                string produced = fromEntry.outputs[o].payload;
                if (string.IsNullOrEmpty(produced)) { continue; }
                for (int i = 0; i < toEntry.inputs.Count; i++)
                {
                    if (toEntry.inputs[i].payload != produced) { continue; }
                    CityGraphEdge edge = new CityGraphEdge();
                    edge.fromNodeId = from.id;
                    edge.fromPort = fromEntry.outputs[o].name;
                    edge.toNodeId = to.id;
                    edge.toPort = toEntry.inputs[i].name;
                    graph.edges.Add(edge);
                    return true;
                }
            }
            return false;
        }
        /// <summary> B-27 / D-9. Samples ground readings and portal flags from the terrain, then resolves underground presence. </summary>
        public static SettlementUndergroundReport BuildUnderground(CityAuthoring city, SettlementFitTable fitTable, 
            int orderIndex, string continentKey, int population, string identity, 
            IList<RailAlignmentSample> railMainLine, double turnoutRatio)
        {
            if (city == null || city.sourceRoad == null || city.sourceRoad.spline == null) 
            {
                return new SettlementUndergroundReport { refusal = "No settlement or source road spline to sample." };
            }

            Road sourceRoad = city.sourceRoad;
            SplineC candidateAlignment = sourceRoad.spline;
            ITerrainSurfaceReader surfaceReader = TerrainWriteGateway.Active != null ? TerrainWriteGateway.Active.SurfaceReader : null;

            float surfaceGrade = 0f;
            float waterGapMetres = 0f;
            float waterLevel = 0f; 
            bool portalOnOpenWater = false;
            bool underwaterCaseAvailable = false;
            bool portalGroundValid = true;
            
            float classGradientLimit = 0.05f;
            float surfaceSpanMetres = 150f;

            Vector3 portalMouth = candidateAlignment.nodes[0].pos;
            Vector3 boreDirection = Vector3.forward;
            float boreRadius = 4f; 

            if (surfaceReader != null)
            {
                Vector3 pStart = candidateAlignment.nodes[0].pos;
                Vector3 pEnd = candidateAlignment.nodes[candidateAlignment.nodes.Count - 1].pos;
                float h1 = surfaceReader.SampleHeight(pStart.x, pStart.z);
                float h2 = surfaceReader.SampleHeight(pEnd.x, pEnd.z);
                
                float rise = Mathf.Abs(h2 - h1);
                float run = Vector2.Distance(new Vector2(pStart.x, pStart.z), new Vector2(pEnd.x, pEnd.z));
                if (run > 0f) surfaceGrade = rise / run;

                float firstWater = -1f;
                float lastWater = -1f;
                float currentDist = 0f;
                for (int i = 0; i < candidateAlignment.nodes.Count; i++)
                {
                    Vector3 pt = candidateAlignment.nodes[i].pos;
                    float h = surfaceReader.SampleHeight(pt.x, pt.z);
                    if (i > 0) currentDist += Vector3.Distance(pt, candidateAlignment.nodes[i-1].pos);
                    
                    if (h <= waterLevel)
                    {
                        if (firstWater < 0f) firstWater = currentDist;
                        lastWater = currentDist;
                    }
                }
                if (firstWater >= 0f) waterGapMetres = lastWater - firstWater;

                SettlementRailEntryRow railRow = SettlementRailEntryProbe.Resolve(city, fitTable, railMainLine, turnoutRatio);
                if (railRow.Answered && railRow.entry.resolved)
                {
                    if (railRow.entry.approach == SettlementRailApproach.ThroughLine)
                    {
                        portalMouth = railRow.entry.entryPoint;
                        boreDirection = (railRow.entry.exitPoint - railRow.entry.entryPoint).normalized;
                    }
                    else if (railRow.entry.approach == SettlementRailApproach.Terminus)
                    {
                        portalMouth = railRow.entry.junctionPoint;
                        boreDirection = (railRow.entry.terminusPoint - railRow.entry.junctionPoint).normalized;
                    }
                }

                float portalH = surfaceReader.SampleHeight(portalMouth.x, portalMouth.z);
                portalGroundValid = true;
                
                underwaterCaseAvailable = false;
                portalOnOpenWater = portalH <= waterLevel && !underwaterCaseAvailable;
            }
            else 
            {
                portalGroundValid = false;
            }

            ulong citySeed = (ulong)(uint)city.seed;
            SettlementUndergroundReport report = SettlementUndergroundPresence.Resolve(
                fitTable, orderIndex, continentKey, population, identity, citySeed,
                surfaceGrade, classGradientLimit, waterGapMetres, surfaceSpanMetres,
                portalOnOpenWater, underwaterCaseAvailable, portalGroundValid);

            if (report.metroBuildable && report.portalCuttable && TerrainWriteGateway.Active != null)
            {
                TerrainRegion cutRegion = TerrainRegion.FromFootprint(portalMouth.x, portalMouth.z, boreRadius, boreRadius, portalMouth.y - boreRadius, portalMouth.y + boreRadius);
                TerrainWriteGateway.Active.RequestCutRegion(cutRegion, "settlement.portal", out string cutRefusal);
            }

            return report;
        }
    }
}
#endif

#if UNITY_EDITOR
#region "Imports"
// No bare `using System` on purpose: with UnityEngine imported beside it, `Object` becomes ambiguous, and this file
// destroys a lot of probe objects. System's few members used here are named in full instead.
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion

namespace RoadCityMaster.City.Editor
{
    /// <summary> C-12's gate, and the last thing B-15 asks for: ONE file that asserts what the other C-tasks changed --
    /// the authoring component's fields and their registry ownership, the graph's validation, the bake runner's stage
    /// order and its determinism, the hierarchy's leaf reuse, the siting pass's water and catchment rules, and the
    /// ground scan's promise to measure without writing.
    ///
    /// Nineteen cases in one gate, deliberately: the suite counts gate FILES, so nineteen files would raise its count
    /// by nineteen and say nothing more than this one does.
    ///
    /// Nothing here writes an asset that outlives the run, nothing writes a heightmap value, and every case that
    /// touches the scene destroys what it made in a <c>finally</c>. Report lines are prefixed RCMCITYGRAPH. </summary>
    public static class CityGraphGate
    {
        private const string Prefix = "RCMCITYGRAPH";

        [MenuItem("Window/Road City Master/Testing/City graph, bake, siting and ground scan (nineteen cases)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityGraphGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Prefix).Append(" BEGIN\n");
            List<string> failures = new List<string>();

            CaseAuthoringComponent(builder, failures);
            CaseNoNodeFields(builder, failures);
            CaseCanonicalOrder(builder, failures);
            CaseSeededBakeRepeats(builder, failures);
            CaseLeafReuse(builder, failures);
            CaseRegistryOwnsFields(builder, failures);
            CaseValidateRefusesFour(builder, failures);
            CaseOneDeletePath(builder, failures);
            CaseIdsSurviveReAdd(builder, failures);
            CaseCanvasSurvives(builder, failures);
            CaseSitingRefusesWater(builder, failures);
            CaseVillageConnector(builder, failures);
            CaseSweepIsIdempotent(builder, failures);
            CaseFingerprintsDiffer(builder, failures);
            CaseReillyBreakpoint(builder, failures);
            CaseFootprintDrift(builder, failures);
            CaseScanWritesNothing(builder, failures);
            CaseContentAndSeating(builder, failures);
            CaseNeverInWater(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append(Prefix).Append(" FAIL   ").Append(failure).Append("\n");
            }
            builder.Append(Prefix).Append(" result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append(Prefix).Append(" END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        #region "(a) and (b) -- the authoring component and what it is not allowed to hold"
        /// <summary> (a) The component exists, is a MonoBehaviour, and Unity resolves C-9's inspector for it.
        ///
        /// Discoverability is asked of Unity's own resolution rather than of the <c>CustomEditor</c> attribute, because
        /// what matters is what an author sees when they select the settlement root, and the attribute is only one of the
        /// things that decides that. <c>CreateEditor</c> builds the drawer without drawing it, so this runs headless.
        /// </summary>
        private static void CaseAuthoringComponent(StringBuilder builder, List<string> failures)
        {
            Expect(typeof(MonoBehaviour).IsAssignableFrom(typeof(CityAuthoring)),
                "CityAuthoring is not a MonoBehaviour, so it could not stand on a settlement's root", failures);

            GameObject host = new GameObject("RCMCITYGRAPH probe a");
            UnityEditor.Editor inspector = null;
            try
            {
                CityAuthoring authoring = host.AddComponent<CityAuthoring>();
                if (authoring == null)
                {
                    failures.Add("a CityAuthoring component could not be added to a GameObject at all");
                }
                else
                {
                    inspector = UnityEditor.Editor.CreateEditor(authoring);
                    string drawn = inspector == null ? "(none)" : inspector.GetType().Name;
                    Expect(inspector != null, "Unity resolved no inspector for CityAuthoring", failures);
                    Expect(drawn == "CityAuthoringEditor",
                        "CityAuthoring is drawn by " + drawn + " rather than by CityAuthoringEditor, so the inspector"
                        + " C-9 built is not the one an author would see", failures);
                }
            }
            finally
            {
                if (inspector != null) { Object.DestroyImmediate(inspector); }
                Object.DestroyImmediate(host);
            }

            Line(builder, "authoring component", "CityAuthoring is a MonoBehaviour and CityAuthoringEditor draws it");
        }

        /// <summary> (b) The component carries no field that belongs to a node -- by count and by name.
        ///
        /// The count is written down because it is the only way a gate can notice a field ADDED here: a by-name check
        /// alone passes for ever as fields accumulate. Ten is what C-9 settled on, and moving that figure should be a
        /// decision somebody makes on purpose rather than a side effect.
        ///
        /// The by-name half asks the CATALOGUE rather than a list written here: no field on this component may share a
        /// name with any parameter any node kind declares, because one setting authored in two places is exactly the
        /// confusion B-15 opened with. </summary>
        private static void CaseNoNodeFields(StringBuilder builder, List<string> failures)
        {
            List<string> fields = SerializedFieldNames(typeof(CityAuthoring));
            Expect(fields.Count == 10,
                "CityAuthoring serialises " + fields.Count + " fields rather than the ten C-9 settled on: "
                + Join(fields), failures);

            HashSet<string> parameters = AllNodeParameterNames();
            for (int i = 0; i < fields.Count; i++)
            {
                Expect(!parameters.Contains(fields[i]),
                    "CityAuthoring." + fields[i] + " has the same name as a node parameter, so one setting would be"
                    + " authored in two places", failures);
            }

            Line(builder, "no node fields", "ten serialised fields, none of them the name of a node parameter");
        }
        #endregion

        #region "(c), (d) and (e) -- the bake runner"
        /// <summary> (c) The runner walks a graph in canonical stage order, whatever order the node LIST is in.
        ///
        /// One unblocked, asset-free node per stage is inserted in REVERSE stage order, so a runner that followed the
        /// node list would report the stages backwards. The walk is a PREVIEW of an id no node carries: every node is
        /// stepped around, no shipped code runs, and the stage sequence is still recorded -- which is the claim, since a
        /// stage is observed when it is REACHED rather than when something in it succeeds. A case that also ran the nodes
        /// would assert an order and test five operators at once, and the first null in a deliberately sparse request
        /// would be reported here as a broken order instead of as the missing input it is.
        ///
        /// It is FOUR stages today, not five, and that figure is derived rather than written down. <c>BakeScatter</c>'s
        /// only catalogue entry is scatter exclusion, which is blocked on RM-15 behind RM-14, and
        /// <c>CityGraph.Validate</c> refuses a graph holding a blocked node -- so a fifth node could not be placed and
        /// the graph could not be opened for a bake at all. Writing <c>placed == 4</c> here would then rot the day RM-15
        /// lands, so what is asserted instead is that every stage the catalogue CAN populate got a node, and that each
        /// stage it cannot is empty for a reason the catalogue itself states: entries exist for it and every one of them
        /// is blocked. That is not the same statement twice -- it fails if a stage is left with no entries at all, if the
        /// scatter entry is deleted rather than unblocked, and it starts covering five stages by itself once the block is
        /// lifted. </summary>
        private static void CaseCanonicalOrder(StringBuilder builder, List<string> failures)
        {
            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            try
            {
                List<BakeStage> blocked = new List<BakeStage>();
                CheckEveryUnpopulatedStageIsAKnownBlockage(blocked, failures);

                int placed = InsertOneOrdinaryNodePerStageInReverse(graph);
                int stages = CityBakePipeline.CanonicalOrder.Length;
                Expect(placed == stages - blocked.Count,
                    "the catalogue offers an unblocked, asset-free node for " + placed + " of the " + stages
                    + " bake stages, and " + blocked.Count + " stage(s) are held up by a blocked entry ("
                    + Join(Names(blocked)) + "), so " + (stages - blocked.Count - placed)
                    + " stage(s) were left out of the walk for no stated reason", failures);
                Expect(placed >= stages - 1,
                    "only " + placed + " of the " + stages + " bake stages could be populated, which is too few for"
                    + " this case to be asserting an ORDER at all rather than a pair", failures);

                string validation;
                Expect(graph.Validate(out validation),
                    "a graph holding one ordinary node per stage was refused, so no order could be walked -- "
                    + validation, failures);

                CityBakeRequest request = new CityBakeRequest();
                request.citySeed = 20260905;
                CityBakeWalk walk;
                CityBakeReport report;
                if (!CityBakeRunner.TryBegin(graph, request, "no-node-here-carries-this-id", out walk, out report))
                {
                    failures.Add("the runner refused to open a bake over a graph Validate accepted: " + report.report);
                }
                else { WalkAndAssertStageOrder(walk, placed, failures); }

                CheckReversedSequenceIsRefused(failures);
            }
            finally { Object.DestroyImmediate(graph); }

            Line(builder, "canonical order", "every populated stage inserted backwards still walks LoadOrGenerate first");
        }
        /// <summary> (d) One seed, two bakes, the same result -- and a different seed, a different result.
        ///
        /// The dump leaves out three things on purpose. Milliseconds, because a wall clock is not an output. Node ids,
        /// because <c>CityGraph.MintId</c> is a GUID and a determinism check that compared GUIDs would fail on a
        /// perfectly reproducible bake -- a node's POSITION is its identity here, which is also what the runner seeds
        /// from. And the graph asset, since both bakes are given one rebuilt the same way.
        ///
        /// The nodes are the LoadOrGenerate sources, which read the request and publish it: they run without a terrain,
        /// a city asset or a content catalogue, so what this proves is the runner's own determinism rather than five
        /// operators' tolerance of a sparse request. The DRAW is proved separately, through <c>ContentResolver</c>, whose
        /// pick is <c>DeterministicRandom.ForElement(citySeed, requester)</c> and so varies with the seed and nothing
        /// else -- a resolver that ignored the seed would furnish every city in the world identically. </summary>
        private static void CaseSeededBakeRepeats(StringBuilder builder, List<string> failures)
        {
            string first = BakeSourcesOnce(20260905, failures);
            string second = BakeSourcesOnce(20260905, failures);
            Expect(first == second,
                "two bakes of one graph at seed 20260905 reported different results, so a bake is not reproducible --"
                + "\n  first:  " + first + "\n  second: " + second, failures);

            int distinct = DistinctContentPicksAcrossSeeds(32, 8);
            Expect(distinct >= 2,
                "eight validated candidates drawn for one requester across 32 city seeds returned " + distinct
                + " distinct asset(s), so the draw ignores the city seed", failures);

            Line(builder, "seeded bake repeats", "one seed twice is identical, and 32 seeds draw " + distinct
                + " distinct assets");
        }

        /// <summary> (e) A second bake over one city root REUSES the leaf the first one made.
        ///
        /// Two contexts rather than two calls on one, because the ordinal counter lives on the context: one context asked
        /// twice for the same element id is a lot with two buildings and correctly answers #0000 then #0001, while a
        /// second context is a RE-BAKE and must land back on #0000 and find what is already there. That is the whole
        /// difference between a re-bake and a duplicate.
        ///
        /// The child count is asserted as well as the transform, because two objects of one name under one branch would
        /// still let <c>Find</c> return the first and pass an equality check on its own. </summary>
        private static void CaseLeafReuse(StringBuilder builder, List<string> failures)
        {
            GameObject root = new GameObject("RCMCITYGRAPH probe e");
            try
            {
                CityBakeRequest request = new CityBakeRequest();
                request.cityRoot = root.transform;
                request.citySeed = 7;

                Transform first = ContextOver(request).EnsureObject(GeneratedKind.Building, "lot.1");
                Transform second = ContextOver(request).EnsureObject(GeneratedKind.Building, "lot.1");

                Expect(first != null, "the first bake made no building leaf under a city root that exists", failures);
                Expect(first == second,
                    "a second bake made a new object instead of reusing the first: " + Named(first) + " then "
                    + Named(second) + ", so every re-bake would leave the previous one behind", failures);

                Transform branch = CityHierarchy.EnsureBranch(root.transform,
                    CityHierarchy.BranchFor(GeneratedKind.Building));
                Expect(branch != null && branch.childCount == 1,
                    "the " + CityHierarchy.BranchFor(GeneratedKind.Building) + " branch holds "
                    + (branch == null ? "no branch at all" : branch.childCount + " children")
                    + " after two bakes of one building, rather than the single one it reused", failures);

                string expected = CityHierarchy.NameFor(GeneratedKind.Building, "lot.1", 0);
                Expect(first != null && first.name == expected,
                    "the reused leaf is named " + Named(first) + " rather than " + expected
                    + ", so CityHierarchy.TryParseName cannot read the element it came from", failures);
            }
            finally { Object.DestroyImmediate(root); }

            Line(builder, "leaf reuse", "a second context lands back on #0000 and the branch keeps one child");
        }
        #endregion

        #region "(f) -- one table owns every city setting"
        /// <summary> (f) Every <c>CityAuthoring</c> field and every node parameter has a row in RM-51's field table, and
        /// every city row in that table names something that exists. Both directions, because either alone rots:
        /// rows-only passes when a field is added and left undescribed, and fields-only passes when a parameter is
        /// renamed and its row is left describing a name nothing reads.
        ///
        /// The component key is never typed -- it is the kind's own <c>ToString()</c>, which is the expression C-10's Row
        /// helper builds it from, so renaming a kind moves the rows and this case together. </summary>
        private static void CaseRegistryOwnsFields(StringBuilder builder, List<string> failures)
        {
            List<string> fields = SerializedFieldNames(typeof(CityAuthoring));
            HashSet<string> authoringRows = RowFieldsFor("CityAuthoring");
            for (int i = 0; i < fields.Count; i++)
            {
                Expect(authoringRows.Contains(fields[i]),
                    "CityAuthoring." + fields[i] + " has no row in the field table, so its unit, its range and the"
                    + " source of that range are recorded nowhere", failures);
            }
            foreach (string row in authoringRows)
            {
                Expect(fields.Contains(row),
                    "the field table carries a CityAuthoring row for " + row + ", which is not a serialised field of"
                    + " that component, so the row describes something nothing reads", failures);
            }

            int parameters = 0;
            Dictionary<string, HashSet<string>> declared = ParametersByKindName();
            foreach (KeyValuePair<string, HashSet<string>> pair in declared)
            {
                HashSet<string> rows = RowFieldsFor(pair.Key);
                foreach (string parameter in pair.Value)
                {
                    parameters++;
                    Expect(rows.Contains(parameter), "the node kind " + pair.Key + " declares the parameter "
                        + parameter + ", which has no row in the field table", failures);
                }
                foreach (string row in rows)
                {
                    Expect(pair.Value.Contains(row), "the field table carries a " + pair.Key + " row for " + row
                        + ", which is not a parameter that kind declares", failures);
                }
            }

            Line(builder, "registry owns fields", fields.Count + " authoring fields and " + parameters
                + " node parameters, each with a row, and no row without one");
        }
        #endregion

        #region "(g) and (h) -- what the graph refuses, and the one way a node is deleted"
        /// <summary> (g) Four faults, four refusals, and each refusal has to say which fault it caught.
        ///
        /// A wire's payloads disagreeing, a wire pointing at nothing, a wire running against the bake, and a loop are
        /// four separate readings of "the wiring is wrong", and a Validate that returned false for all four while only
        /// having looked for one would pass a case that merely checked the boolean. So each is asserted through
        /// <c>ExpectRefusal</c>, which also requires the refusal to CONTAIN the phrase that names that fault: a graph
        /// refused for the wrong reason fails here, which is the only way this case notices a check being deleted.
        ///
        /// The four graphs are built by SEARCHING the catalogue for kinds whose ports have the shape each fault needs,
        /// never by naming kinds. A gate that hardcoded, say, TerrainSurface would start failing on the day somebody
        /// gave that kind a second output -- reporting a broken Validate when what changed was the catalogue. </summary>
        private static void CaseValidateRefusesFour(StringBuilder builder, List<string> failures)
        {
            ExpectRefusal(MismatchedPortGraph(failures), "into a port that reads a",
                "a wire carrying one payload into a port that reads another", failures);
            ExpectRefusal(DanglingEdgeGraph(failures), "which no node in this graph carries",
                "an edge pointing at a node id no node in the graph carries", failures);
            ExpectRefusal(CycleGraph(failures), "the wiring contains a cycle",
                "two nodes wired into a loop", failures);
            ExpectRefusal(BackwardStageGraph(failures), "runs backwards through the bake",
                "a wire from a later bake stage into an earlier one", failures);

            Line(builder, "validate refuses four", "mismatched payload, dangling end, cycle and backward stage");
        }
        /// <summary> (h) There is ONE way a node leaves the graph, and a graph that lost a node without losing that
        /// node's wires is refused.
        ///
        /// The first half is asserted structurally, over the canvas type's own declared methods, because the thing worth
        /// preventing is a SECOND delete path being added later: <c>DeleteSelection</c> is declared exactly once and is
        /// an override, its two removers are private, and no other method on that class removes anything. Driving the
        /// real GraphView instead would need an EditorWindow and a skin, which is a headless throw rather than a test.
        ///
        /// The second half is the safety net under that path, and it is asserted on the model where it is provable
        /// without any UI: a node removed while one of its wires was left behind does not quietly survive -- Validate
        /// refuses it. That is what makes a forgotten edge impossible to ship, whoever forgets it. </summary>
        private static void CaseOneDeletePath(StringBuilder builder, List<string> failures)
        {
            System.Type canvas = typeof(CityGraphCanvas);
            const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
                                        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            List<string> removers = new List<string>();
            int deleteSelections = 0;
            MethodInfo[] methods = canvas.GetMethods(Declared);
            for (int i = 0; i < methods.Length; i++)
            {
                string name = methods[i].Name;
                if (name == "DeleteSelection") { deleteSelections++; continue; }
                if (name.Contains("Remove") || name.Contains("Delete")) { removers.Add(name); }
            }

            Expect(deleteSelections == 1,
                "CityGraphCanvas declares " + deleteSelections + " methods named DeleteSelection rather than the one"
                + " implementation the window documents, so deletion has more than one entry point", failures);

            MethodInfo one = canvas.GetMethod("DeleteSelection", Declared);
            Expect(one != null && one.GetBaseDefinition() != one,
                "CityGraphCanvas.DeleteSelection does not override GraphView's, so the dustbin and the right-click menu"
                + " would delete through Unity's own path and never reach NoteGraphChanged", failures);

            for (int i = 0; i < removers.Count; i++)
            {
                MethodInfo remover = canvas.GetMethod(removers[i], Declared);
                Expect(remover != null && remover.IsPrivate,
                    "CityGraphCanvas." + removers[i] + " is reachable from outside the window, so a caller could remove"
                    + " part of the graph without the undo group DeleteSelection opens", failures);
            }
            Expect(removers.Count == 2,
                "CityGraphCanvas declares " + removers.Count + " removal helpers rather than the two the window"
                + " documents (RemoveModelEdge and RemoveModelNode): " + Join(removers), failures);

            CheckWidowedEdgeIsRefused(failures);

            Line(builder, "one delete path", "DeleteSelection overrides once, two private removers, widowed wire refused");
        }
        #endregion
        #region "(i) and (j) -- what survives a delete, and what survives a reload"
        /// <summary> (i) A delete does not renumber the nodes that stayed, and a re-add does not inherit the gone node's
        /// id.
        ///
        /// Both halves matter because a wire is stored as two node IDS and two port names. If ids were positional, then
        /// removing the first node would silently re-point every remaining wire at the wrong node -- the graph would
        /// still validate, and the bake would build the wrong city. And if a re-add could reuse a retired id, a wire
        /// that outlived its node would quietly reattach to whatever was added next, which is the same bug arriving from
        /// the other direction. A GUID gives both properties; this case is what notices if it stops being one. </summary>
        private static void CaseIdsSurviveReAdd(StringBuilder builder, List<string> failures)
        {
            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            try
            {
                CityNodeDescriptor descriptor;
                if (!TryPlainNodeOfStage(BakeStage.LoadOrGenerate, out descriptor))
                {
                    failures.Add("the catalogue offers no unblocked, asset-free LoadOrGenerate node, so a delete and"
                        + " re-add could not be staged at all");
                }
                else
                {
                    CityGraphNode keep = CityNodeCatalog.CreateNode(descriptor);
                    CityGraphNode drop = CityNodeCatalog.CreateNode(descriptor);
                    graph.nodes.Add(keep);
                    graph.nodes.Add(drop);

                    string keptId = keep.id;
                    string droppedId = drop.id;
                    Expect(!string.IsNullOrEmpty(keptId) && keptId != droppedId,
                        "two nodes of one kind were minted with ids " + Quoted(keptId) + " and " + Quoted(droppedId)
                        + ", so a wire cannot say which of them it means", failures);

                    graph.nodes.Remove(drop);
                    CityGraphNode added = CityNodeCatalog.CreateNode(descriptor);
                    graph.nodes.Add(added);

                    Expect(keep.id == keptId,
                        "the surviving node's id changed from " + Quoted(keptId) + " to " + Quoted(keep.id)
                        + " when its neighbour was deleted, so every wire in the graph now points at the wrong node",
                        failures);
                    Expect(added.id != droppedId,
                        "the node added after the delete inherited the deleted node's id " + Quoted(droppedId)
                        + ", so a wire that outlived its node would reattach to the newcomer", failures);
                    Expect(added.id != keptId, "the node added after the delete was minted with an id the graph was"
                        + " already using, " + Quoted(keptId), failures);
                }
            }
            finally { Object.DestroyImmediate(graph); }

            Line(builder, "ids survive re-add", "a delete renumbers nothing and a re-add inherits nothing");
        }
        /// <summary> (j) The canvas layout an author arranged is still there after Unity throws the managed world away.
        ///
        /// A domain reload cannot be triggered from inside a running check, so this asserts the thing a reload actually
        /// depends on: the position is written into the asset FILE and read back from it. The in-memory instance is
        /// discarded and the asset re-imported before reading, so what is compared is what landed on disk rather than
        /// the object that was still in memory -- which is the only version a reload gets to keep.
        ///
        /// The positions are deliberately fractional and negative. A field serialised as an int, or one whose sign was
        /// dropped somewhere, would round-trip a whole number perfectly and fail only for the author who dragged a node
        /// up and to the left. </summary>
        private static void CaseCanvasSurvives(StringBuilder builder, List<string> failures)
        {
            string assetPath = RoadEditorUtility.GetBasePath().Replace('\\', '/') + "/RCMCITYGRAPH_probe_j.asset";
            try
            {
                if (AssetDatabase.LoadAssetAtPath<CityGraph>(assetPath) != null) { AssetDatabase.DeleteAsset(assetPath); }

                CityNodeDescriptor descriptor;
                if (!TryPlainNodeOfStage(BakeStage.LoadOrGenerate, out descriptor))
                {
                    failures.Add("the catalogue offers no unblocked, asset-free LoadOrGenerate node, so no canvas"
                        + " layout could be written to an asset");
                }
                else
                {
                    CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
                    CityGraphNode left = CityNodeCatalog.CreateNode(descriptor);
                    CityGraphNode right = CityNodeCatalog.CreateNode(descriptor);
                    left.canvasPosition = new Vector2(-317.25f, -84.5f);
                    right.canvasPosition = new Vector2(612.75f, 233.125f);
                    graph.nodes.Add(left);
                    graph.nodes.Add(right);
                    string leftId = left.id;

                    AssetDatabase.CreateAsset(graph, assetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    CityGraph reloaded = AssetDatabase.LoadAssetAtPath<CityGraph>(assetPath);

                    if (reloaded == null) { failures.Add("the graph asset did not reload from disk at all"); }
                    else if (reloaded.nodes.Count != 2)
                    {
                        failures.Add("the reloaded graph holds " + reloaded.nodes.Count + " nodes rather than the two"
                            + " that were saved, so a reload loses nodes and not merely their positions");
                    }
                    else { CheckReloadedLayout(reloaded, leftId, failures); }
                }
            }
            finally { AssetDatabase.DeleteAsset(assetPath); }

            Line(builder, "canvas survives", "fractional negative positions come back off disk unchanged");
        }
        #endregion
        #region "(k) and (l) -- the siting pass, before a street exists"
        /// <summary> (k) A road that ends under water proposes no settlement, and the refusal hands the author figures
        /// rather than a verdict.
        ///
        /// The numbers are the point. "This site is unsuitable" tells an author to go hunting; "the ground is at 10.0 m
        /// and the water surface is at 14.0 m, 4.0 m above it" tells them the road end is four metres under and which
        /// way to move it. So this case asserts the refusal CONTAINS the two elevations and the depth between them, and
        /// that they are the figures the probe was given -- a message built from placeholders would pass a check that
        /// only looked for the word "water".
        ///
        /// Nothing is generated either: the pass returns zero accepted, and the refused candidate is still handed back,
        /// because an author who drew a road into a lake has to be told WHICH road. </summary>
        private static void CaseSitingRefusesWater(StringBuilder builder, List<string> failures)
        {
            SettlementOrderCatalog catalog = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            try
            {
                catalog.ResetToDefaults();

                ArrayHeightfield field = new ArrayHeightfield(65, 200.0, 1024.0);
                field.FillFlat(10.0);
                SettlementGroundProbe probe = SettlementGroundProbe.FromSurface(field, 14.0f, 16.0f);

                List<RoadTerminusInput> termini = new List<RoadTerminusInput>();
                termini.Add(TerminusAt(new Vector3(512.0f, 10.0f, 512.0f), SettlementRoleEnum.VillageConnector,
                    "road into the lake"));
                List<SettlementCandidate> candidates = new List<SettlementCandidate>();
                string report;
                int accepted = SettlementSiting.Propose(termini, catalog, probe, candidates, out report);

                Expect(accepted == 0, "the siting pass accepted " + accepted + " site(s) for a road end standing four"
                    + " metres under water, so a settlement would be built in a lake", failures);
                Expect(candidates.Count == 1, "the pass handed back " + candidates.Count + " candidates for one"
                    + " terminus, so the author is not told which road was refused", failures);

                string refusal = candidates.Count == 1 ? candidates[0].refusal : string.Empty;
                Expect(refusal.Contains("stands in water"),
                    "a road end under water was refused with " + Quoted(refusal) + ", which does not say it stands in"
                    + " water", failures);
                Expect(refusal.Contains("10.0 m") && refusal.Contains("14.0 m") && refusal.Contains("4.0 m"),
                    "the refusal " + Quoted(refusal) + " does not carry the ground elevation, the water surface and the"
                    + " depth between them, so the author is told there is a problem but not how deep", failures);
            }
            finally { Object.DestroyImmediate(catalog); }

            Line(builder, "siting refuses water", "a submerged end is refused with its ground, water and depth");
        }
        /// <summary> (l) The role decides the order, the end decides the facing, and the cross-section decides neither.
        ///
        /// Four claims over one dry site. A village connector proposes exactly ONE village -- order 4, the catalogue's
        /// own <c>VillageOrder</c> rather than a 4 typed here. A road end declaring no role proposes nothing at all,
        /// which is what keeps a scene of two hundred ordinary roads from seeding two hundred villages. The site faces
        /// OUTWARD along the road, asserted against <c>RoadTerminus.OutwardDirectionXZ</c> itself and also against the
        /// road's own tangent, so a candidate that copied the inward direction cannot satisfy both.
        ///
        /// And widening the road does not promote the village to a city: the cross-section is carried on the candidate
        /// for the report and is not an input to the order. That is asserted by proposing the same site twice, once as a
        /// single-lane street and once as a dual highway, and requiring the same order out of both. </summary>
        private static void CaseVillageConnector(StringBuilder builder, List<string> failures)
        {
            SettlementOrderCatalog catalog = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            try
            {
                catalog.ResetToDefaults();

                ArrayHeightfield field = new ArrayHeightfield(65, 200.0, 2048.0);
                field.FillFlat(40.0);
                SettlementGroundProbe probe = SettlementGroundProbe.FromSurface(field,
                    float.NegativeInfinity, 16.0f);

                RoadTerminusInput village = TerminusAt(new Vector3(512.0f, 40.0f, 1024.0f),
                    SettlementRoleEnum.VillageConnector, "lane to the village");
                List<SettlementCandidate> candidates = new List<SettlementCandidate>();
                string report;
                SettlementSiting.Propose(Only(village), catalog, probe, candidates, out report);

                Expect(candidates.Count == 1, "one village connector proposed " + candidates.Count
                    + " sites rather than one", failures);
                if (candidates.Count == 1)
                {
                    SettlementCandidate one = candidates[0];
                    Expect(one.orderIndex == SettlementOrderCatalog.VillageOrder,
                        "a village connector proposed order " + one.orderIndex + " (" + one.orderName + ") rather than"
                        + " the catalogue's village order " + SettlementOrderCatalog.VillageOrder, failures);

                    Vector2 outward = RoadTerminus.OutwardDirectionXZ(village.tangentAtEndXZ, village.whichEnd);
                    Expect((one.outwardXZ - outward).magnitude < 1e-4f,
                        "the site faces " + one.outwardXZ + " rather than the outward direction " + outward
                        + " the road's tangent gives at that end", failures);
                    Expect(Vector2.Dot(one.outwardXZ, village.tangentAtEndXZ.normalized) > 0.99f,
                        "the site at a road's End faces " + one.outwardXZ + ", which is not along the road's tangent "
                        + village.tangentAtEndXZ + ", so the settlement would be laid back down the road it came from",
                        failures);
                }

                CheckRolelessAndWidthDoNotMatter(catalog, probe, failures);
            }
            finally { Object.DestroyImmediate(catalog); }

            Line(builder, "village connector", "one village at order 4, facing outward, whatever the cross-section");
        }
        #endregion
        #region "(m) and (n) -- the sweep runs twice, and two cities of one order differ"
        /// <summary> (m) *Build settlements from roads*, pressed twice, builds nothing the second time.
        ///
        /// The button an author reaches for most is the one that has to be safe to press again, because there is no way
        /// to tell by looking whether the last press took. So this runs the real sweep twice over one road and requires
        /// the second run to create nothing, to say so, and to leave the settlement count where the first run left it.
        /// The count is asserted as well as the words: a sweep that created a duplicate and reported "already there"
        /// would satisfy the wording alone.
        ///
        /// The road is hand-built rather than drawn, because <c>RoadAutomation.CreateNodeProgrammatically</c> refuses a
        /// node with no terrain under it and this case is about the sweep, not about the terrain. Two nodes, neither an
        /// intersection, so both ends are free termini -- how MANY a road seeds is not the claim; that the second press
        /// adds none of them is. </summary>
        private static void CaseSweepIsIdempotent(StringBuilder builder, List<string> failures)
        {
            string graphPath;
            bool graphExistedBefore = DefaultGraphAssetExists(out graphPath, failures);
            GameObject host = new GameObject("RCMCITYGRAPH probe m");
            try
            {
                RoadSystem system = host.AddComponent<RoadSystem>();
                AddTwoNodeRoad(system, SettlementRoleEnum.VillageConnector,
                    new Vector3(200.0f, 0.0f, 200.0f), new Vector3(320.0f, 0.0f, 200.0f));

                string firstReport;
                CitySettlementFactory.BuildSettlementsFromRoads(system, out firstReport);
                int afterFirst = system.GetComponentsInChildren<CityAuthoring>().Length;

                string secondReport;
                CitySettlementFactory.BuildSettlementsFromRoads(system, out secondReport);
                int afterSecond = system.GetComponentsInChildren<CityAuthoring>().Length;

                Expect(afterFirst >= 1, "the first sweep over a road with two free ends and a settlement role placed "
                    + afterFirst + " settlements, so there was nothing for a second sweep to duplicate -- "
                    + firstReport, failures);
                Expect(afterFirst == afterSecond,
                    "a second sweep took the settlement count from " + afterFirst + " to " + afterSecond
                    + ", so pressing the button twice duplicates every settlement -- " + secondReport, failures);
                Expect(secondReport.Contains("0 settlements created"),
                    "the second sweep reported " + Quoted(secondReport) + ", which does not say it created nothing",
                    failures);
                Expect(secondReport.Contains(afterFirst + " already there and left untouched"),
                    "the second sweep did not report the " + afterFirst + " existing settlement(s) as already there: "
                    + secondReport, failures);
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (!graphExistedBefore && !string.IsNullOrEmpty(graphPath)) { AssetDatabase.DeleteAsset(graphPath); }
            }

            Line(builder, "sweep is idempotent", "a second press creates nothing and says so");
        }
        /// <summary> (n) Two cities of one order, on two seeds, are not the same city -- and a layout drawn as a capital
        /// reads as a capital rather than as a village.
        ///
        /// The first half is what stops a thousand settlements sharing one plan: <c>DrawTarget</c> is asked for the same
        /// order twice on different seeds and the two targets must sit further apart than
        /// <c>SeedDifferenceThreshold</c>. A threshold is stated rather than "not equal", because two floats differing
        /// in their last bit are not two different cities, and the figure is written down so a change to it is a
        /// decision somebody makes on purpose.
        ///
        /// The second half is the one that could fail while the first passes. A target that varied with the seed but
        /// ignored the ORDER would hand a village a capital's plan. So two twenty-block mixes are built -- one in the
        /// capital's proportions, one in the village's -- and each is required to sit nearer its own order's target
        /// than the other's, on every seed in a fixed list.
        ///
        /// A MIX rather than one shape, because <c>DrawTarget</c>'s plus or minus 35 per cent jitter can invert any
        /// single family weight between two orders: the capital asks for 35 per cent grid and the village for 10, and
        /// a village target drawn high while a capital target draws low crosses them. It cannot invert the whole
        /// vector, and that is what is compared. The mixes are the four family prototypes themselves -- a 56.2 m
        /// square, a 28.1 x 112.5 m strip, a 66.2 x 151.1 m parcel and a 158.1 x 632.5 m field, each derived from its
        /// prototype shape factor rather than guessed -- in the counts nearest each order's own weights: 7/4/6/3 for
        /// the capital, 2/9/3/6 for the village. Every block lands on its own prototype at distance zero and no
        /// closer than 0.36 to its nearest rival, so the measured shares are the counts and nothing else.
        ///
        /// Blocks only -- no nodes, no edges -- because <c>Compare</c> divides by the weight it actually used, so a
        /// measurement carrying shares alone is compared on the family term at full weight instead of having two
        /// absent terms count as agreement. That also makes the margin computable rather than hoped for: with the
        /// jitter multiplicative and the shares renormalised afterwards, a target cannot move further than about 0.18
        /// in half-L1 from its own order's base vector, while the capital's and the village's base vectors sit 0.40
        /// apart -- so the worst simultaneous corner is 0.178 against 0.241, and the strict comparison holds with
        /// about 0.063 to spare. The seed list is fixed so the same arithmetic runs every time, and a mean gap floor
        /// sits beside the per-seed test to catch a change that leaves the ordering right but the separation gone.
        /// </summary>
        private static void CaseFingerprintsDiffer(StringBuilder builder, List<string> failures)
        {
            SettlementOrderCatalog catalog = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            try
            {
                catalog.ResetToDefaults();
                SettlementOrderClass capital = catalog.Get(SettlementOrderCatalog.CapitalOrder);
                SettlementOrderClass village = catalog.Get(SettlementOrderCatalog.VillageOrder);
                if (capital == null || village == null)
                {
                    failures.Add("the default settlement order catalogue holds no capital or no village order, so two"
                        + " orders could not be told apart");
                }
                else
                {
                    SettlementFingerprint one = SettlementFingerprint.DrawTarget(capital, 11UL);
                    SettlementFingerprint two = SettlementFingerprint.DrawTarget(capital, 20260905UL);
                    string why;
                    float apart = SettlementFingerprint.Compare(one, two, out why);
                    Expect(apart > SeedDifferenceThreshold,
                        "two capitals on seeds 11 and 20260905 differ by " + Fmt(apart) + ", which is at or under the "
                        + Fmt(SeedDifferenceThreshold) + " this gate calls a difference, so every capital in the world"
                        + " would be laid out the same way -- " + why, failures);

                    CheckEachLayoutIsNearerItsOwnOrder(capital, village, failures);
                }
            }
            finally { Object.DestroyImmediate(catalog); }

            Line(builder, "fingerprints differ", "two seeds draw two cities, and a grid is not read as a village");
        }
        #endregion
        #region "(o) -- a village inside a city's catchment is that city's suburb"
        /// <summary> (o) A village proposed 1500 m from a city is refused, and the refusal shows the arithmetic.
        ///
        /// Reilly's breakpoint is the one rule in the siting pass an author is likely to disagree with, so the message
        /// has to be checkable rather than authoritative: the separation, the breakpoint measured out from the larger
        /// centre, and the built-up radius it fell inside. This case asserts the refusal carries the separation and the
        /// breakpoint, and it computes the expected breakpoint by calling the shipped formula rather than by writing a
        /// number here -- so the case follows a corrected exponent instead of failing on one.
        ///
        /// Direction matters as much as distance: the CITY keeps its site. A test that only counted refusals would pass a
        /// pass that rejected both ends, which would leave an author with a highway and no city on it.
        ///
        /// The test world is 16 km across because a city's own region is 6.7 km across -- a square of the 45 km2 the
        /// catalogue's reference city is authored to cover. Both regions have to lie wholly on readable ground or the
        /// water tests refuse them for ground the tool cannot read, and a candidate refused there never reaches the
        /// catchment pass at all, which would leave this case asserting nothing. </summary>
        private static void CaseReillyBreakpoint(StringBuilder builder, List<string> failures)
        {
            SettlementOrderCatalog catalog = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            try
            {
                catalog.ResetToDefaults();
                SettlementOrderClass cityOrder = catalog.Get(SettlementOrderCatalog.CityOrder);
                SettlementOrderClass villageOrder = catalog.Get(SettlementOrderCatalog.VillageOrder);

                ArrayHeightfield field = new ArrayHeightfield(129, 200.0, 16384.0);
                field.FillFlat(40.0);
                SettlementGroundProbe probe = SettlementGroundProbe.FromSurface(field,
                    float.NegativeInfinity, 32.0f);

                List<RoadTerminusInput> termini = new List<RoadTerminusInput>();
                termini.Add(TerminusAt(new Vector3(4000.0f, 40.0f, 8192.0f), SettlementRoleEnum.Intercity,
                    "the trunk road"));
                termini.Add(TerminusAt(new Vector3(5500.0f, 40.0f, 8192.0f), SettlementRoleEnum.VillageConnector,
                    "the village lane"));

                List<SettlementCandidate> candidates = new List<SettlementCandidate>();
                string report;
                SettlementSiting.Propose(termini, catalog, probe, candidates, out report);

                if (candidates.Count != 2 || cityOrder == null || villageOrder == null)
                {
                    failures.Add("two termini 1500 m apart produced " + candidates.Count + " candidates over a"
                        + " catalogue that must hold both a city and a village order, so the catchment rule could"
                        + " not be exercised -- " + report);
                }
                else
                {
                    float expected = SettlementSiting.BreakpointFromLargerMetres(1500.0f,
                        cityOrder.referencePopulation, villageOrder.referencePopulation);
                    CheckCatchmentRefusal(candidates, expected, report, failures);
                }
            }
            finally { Object.DestroyImmediate(catalog); }

            Line(builder, "reilly breakpoint", "the village is refused with its separation and the breakpoint");
        }
        #endregion
        #region "(p) and (q) -- the ground scan measures, and measures only"
        /// <summary> (p) The three ways a hand-placed object can disagree with the ground, told apart, with the number.
        ///
        /// A scan that only answered "this needs attention" would send an author to look at a shed and leave them to
        /// work out what is wrong with it. So each reading is asserted as a MOTION and as a distance: a shed floating two
        /// metres over flat ground reads <c>Fell</c> with a worst fall of 2 m, one buried two metres reads <c>Rose</c>
        /// with a worst rise of 2 m, and one on a slope -- where part of the ground is above the base and part below --
        /// reads <c>Reshaped</c>, which is the only reading that says "it rocks" rather than "it sits at the wrong
        /// height".
        ///
        /// The rocking case takes its base from the ground under the footprint's own centre rather than from a figure
        /// typed here, so it is a slope that is being asserted and not an offset.
        ///
        /// The station count is asserted on the same 8 m footprint, because two stations on a small building cannot tell
        /// a level slab from one resting on a single high corner -- the whole reason the scan lays out corners and edges
        /// rather than sampling a centre.
        ///
        /// This is not <c>CityLinkedGroundGate</c>'s fourth sweep again in another file. That one drives the same
        /// <c>GroundDrift</c> verdicts through <c>GroundConformRecord</c>, where every station's remembered height was
        /// SAMPLED from the ground the road was built over; here it comes through <c>FootprintConformRecord</c>, whose
        /// <c>AddStation</c> sets <c>groundYAtBuild</c> to the object's own base elevation instead. That one line is
        /// the difference between asking "has the ground moved since?" and asking "does this object meet the ground at
        /// all?", and it is the second question an author placing a shed is asking. </summary>
        private static void CaseFootprintDrift(StringBuilder builder, List<string> failures)
        {
            ArrayHeightfield low = new ArrayHeightfield(129, 400.0, 1024.0);
            low.FillFlat(8.0);
            GroundDrift fell = DriftOverGround(low, 512.0f, 512.0f, 10.0f, failures);
            Expect(fell.motion == GroundMotion.Fell,
                "a footprint standing 2 m over flat ground reads as " + fell.motion + " rather than Fell, so an object"
                + " hanging in the air is not reported as hanging", failures);
            Expect(Near(fell.worstFallMetres, 2.0, 0.05),
                "the worst fall under a footprint 2 m above the ground reads " + Fmt(fell.worstFallMetres)
                + " m rather than about 2 m", failures);
            Expect(fell.stationsMeasured > 2,
                "an 8 m footprint was measured at " + fell.stationsMeasured + " stations, which cannot tell a level slab"
                + " from one resting on a single high corner", failures);

            ArrayHeightfield high = new ArrayHeightfield(129, 400.0, 1024.0);
            high.FillFlat(12.0);
            GroundDrift rose = DriftOverGround(high, 512.0f, 512.0f, 10.0f, failures);
            Expect(rose.motion == GroundMotion.Rose,
                "a footprint buried 2 m under flat ground reads as " + rose.motion + " rather than Rose", failures);
            Expect(Near(rose.worstRiseMetres, 2.0, 0.05),
                "the worst rise over a footprint buried 2 m reads " + Fmt(rose.worstRiseMetres)
                + " m rather than about 2 m", failures);

            ArrayHeightfield hill = new ArrayHeightfield(129, 400.0, 1024.0);
            hill.FillDome(10.0, 200.0);
            float onTheSlope = hill.SampleHeight(768.0f, 512.0f);
            GroundDrift reshaped = DriftOverGround(hill, 768.0f, 512.0f, onTheSlope, failures);
            Expect(reshaped.motion == GroundMotion.Reshaped,
                "a footprint on a slope, with ground above its base on one side and below it on the other, reads as "
                + reshaped.motion + " rather than Reshaped, so a rocking object is reported as merely mis-levelled --"
                + " worst rise " + Fmt(reshaped.worstRiseMetres) + " m, worst fall "
                + Fmt(reshaped.worstFallMetres) + " m", failures);

            Line(builder, "footprint drift", "floating reads Fell, buried reads Rose, a slope reads Reshaped");
        }
        /// <summary> (q) The scan asks; it does not act.
        ///
        /// This is the promise the whole hand-placed pass rests on. An author's own buildings are theirs, and a scan that
        /// quietly levelled the ground under them would edit a terrain nobody asked it to edit -- so the scan produces
        /// pad REQUESTS and leaves the writing to the pass that owns it.
        ///
        /// It is asserted at the one place every terrain write in this tool has to pass through: the gateway's ledger
        /// counts writes for the whole session, so a scan that touched a single heightmap value moves
        /// <c>TotalAdded</c>. Reading the total before and after and requiring it to be unchanged is a stronger claim
        /// than any inspection of the scan's own code, because it holds for whatever the scan calls.
        ///
        /// A request IS expected: a cube floating two metres over flat ground is exactly what an author wants told about,
        /// and a scan that produced no request would pass a write-nothing check by doing nothing at all. </summary>
        private static void CaseScanWritesNothing(StringBuilder builder, List<string> failures)
        {
            GameObject shed = null;
            try
            {
                ArrayHeightfield field = new ArrayHeightfield(129, 400.0, 1024.0);
                field.FillFlat(8.0);

                shed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shed.name = "RCMCITYGRAPH probe q";
                shed.transform.localScale = new Vector3(8.0f, 4.0f, 8.0f);
                shed.transform.position = new Vector3(512.0f, 12.0f, 512.0f);

                List<Transform> roots = new List<Transform>();
                roots.Add(shed.transform);
                List<PadRequest> requests = new List<PadRequest>();

                long before = TerrainWriteGateway.Active.Ledger.TotalAdded;
                string summary;
                List<FootprintScanFinding> findings = HandPlacedGroundScan.Sweep(roots, field,
                    FootprintScanClass.Default(), PadElevationRule.CentroidSample, requests, out summary);
                long after = TerrainWriteGateway.Active.Ledger.TotalAdded;
                Expect(after == before,
                    "the ground scan added " + (after - before) + " write(s) to the terrain ledger, so a pass that is"
                    + " only allowed to MEASURE the author's own buildings edited the ground under them", failures);
                Expect(findings.Count == 1, "the scan reported on " + findings.Count + " objects rather than the one it"
                    + " was given: " + summary, failures);
                Expect(requests.Count == 1,
                    "a cube standing 2 m over flat ground produced " + requests.Count + " pad requests rather than one,"
                    + " so the scan writes nothing by finding nothing -- " + summary, failures);
                Expect(findings.Count == 1 && findings[0].padRequested && findings[0].lotId == 0,
                    "the finding for a floating object does not carry the pad request the scan made for it, so nothing"
                    + " downstream can tell which object a request belongs to", failures);
            }
            finally { if (shed != null) { Object.DestroyImmediate(shed); } }

            Line(builder, "scan writes nothing", "one pad request, and the terrain ledger does not move");
        }
        #endregion
        #region "(r) and (s) -- the author's content, and the water nothing is placed in"
        /// <summary> (r) The tool refuses to invent content, resolves what the author registered, and pulls it down onto
        /// the ground the way the thing itself demands.
        ///
        /// The refusal is fail-closed by construction and this is what proves it stays that way: an unregistered category
        /// does not borrow from a neighbour or fall back to a default, it refuses with <c>CategoryEmpty</c> and NAMES the
        /// category, because "no content available" leaves an author guessing which of thirty categories they missed.
        ///
        /// Then the seating, which is the division of labour C-17 exists for: the tool decides where a thing goes and how
        /// it meets the ground, the author supplies the prefab. Both halves are asserted on one tilted surface. A tree is
        /// DRIVEN into the ground, so it goes in plumb whatever the slope -- a tree growing perpendicular to a hillside
        /// is the thing that gives a generated world away. A bench RESTS on it and follows the face. The embed is
        /// asserted through the identity that defines it, the seated height being the surface less the embed, so the case
        /// holds whatever figure the embed resolves to.
        ///
        /// No terrain is needed for any of it: the seating takes a height and a normal as numbers. </summary>
        private static void CaseContentAndSeating(StringBuilder builder, List<string> failures)
        {
            ContentCatalog catalog = new ContentCatalog();
            ContentResolution empty = ContentResolver.Resolve(catalog, "vegetation.hedgerow", "lot.7", 20260905UL, null);
            Expect(!empty.resolved,
                "an empty category resolved to " + empty.asset.assetId + ", so the tool invented content the author"
                + " never registered", failures);
            Expect(empty.refusal.reason == ContentRefusalReason.CategoryEmpty,
                "an empty category refused with " + empty.refusal.reason + " rather than CategoryEmpty", failures);
            Expect(empty.refusal.category == "vegetation.hedgerow",
                "the refusal names the category " + Quoted(empty.refusal.category) + " rather than the one that was"
                + " asked for, so an author is not told which category to fill", failures);

            ContentCandidate hedge = new ContentCandidate();
            hedge.assetId = "hedge.beech";
            hedge.category = "vegetation.hedgerow";
            hedge.passedValidation = true;
            catalog.Register(hedge);
            ContentResolution found = ContentResolver.Resolve(catalog, "vegetation.hedgerow", "lot.7", 20260905UL, null);
            Expect(found.resolved && found.asset.assetId == "hedge.beech",
                "one registered, validated asset did not resolve: " + (found.resolved ? found.asset.assetId
                    : found.refusal.reason.ToString()), failures);

            Vector3 slope = new Vector3(Mathf.Sin(10.0f * Mathf.Deg2Rad), Mathf.Cos(10.0f * Mathf.Deg2Rad), 0.0f);
            GroundSeat tree = GroundSeating.SeatAgainst(new Vector3(12.0f, 99.0f, 34.0f), Quaternion.identity,
                GroundContactMode.SkirtToTerrain, GroundSeatingClass.ForDrivenOrGrown(), new Vector2(1.5f, 1.5f),
                0.25f, 40.0f, slope);
            GroundSeat bench = GroundSeating.SeatAgainst(new Vector3(12.0f, 99.0f, 34.0f), Quaternion.identity,
                GroundContactMode.SkirtToTerrain, GroundSeatingClass.ForResting(), new Vector2(1.5f, 1.5f),
                0.25f, 40.0f, slope);
            CheckSeating(tree, bench, failures);

            Line(builder, "content and seating", "an empty category refuses by name, and a tree goes in plumb");
        }
        /// <summary> (s) Never in a lake, never in the sea -- and the difference between a world with no water in it and
        /// a world nobody wired the water up in.
        ///
        /// (k) covered the plain case, a road end standing in water. This covers the three that are easier to get wrong.
        ///
        /// An ISLET is refused even though the road end on it is dry: a city's region is a square of the 45 km2 the
        /// catalogue's reference city covers, and a dome of dry ground about 3 km across cannot hold it. The refusal has
        /// to say so in those terms -- the road end is dry, the settlement would not be -- because an author looking at a
        /// dry road end and a refusal that read "stands in water" would think the tool had misread the ground. The case
        /// does not care which archetype gets measured on an island: the four ceilings the catalogue authors are 5, 20,
        /// 20 and 25 per cent, and the region here is over nine tenths water, so it is refused against any of them.
        ///
        /// SILENCE is refused as a whole pass, before any candidate is built. A float has no null, so a probe nobody set
        /// a water level on carries zero, which reads as a sea at y = 0 rather than as nobody having said -- and the two
        /// lead to opposite decisions on every low-lying site in a world. The case proves that refusal is about the
        /// silence and not about the water by stating that there is NO water, on the same field and the same terminus,
        /// and getting an acceptance; and by stating a level that is not a number, which is refused again. </summary>
        private static void CaseNeverInWater(StringBuilder builder, List<string> failures)
        {
            SettlementOrderCatalog catalog = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            try
            {
                catalog.ResetToDefaults();
                ArrayHeightfield island = new ArrayHeightfield(257, 400.0, 16384.0);
                island.FillDome(0.0, 60.0);
                List<RoadTerminusInput> termini = Only(TerminusAt(new Vector3(8192.0f, 60.0f, 8192.0f),
                    SettlementRoleEnum.Intercity, "the causeway"));

                List<SettlementCandidate> islet = new List<SettlementCandidate>();
                string isletReport;
                int isletAccepted = SettlementSiting.Propose(termini, catalog,
                    SettlementGroundProbe.FromSurface(island, 55.0f, 30.0f), islet, out isletReport);
                Expect(isletAccepted == 0 && islet.Count == 1, "a city proposed on the summit of an island, in water"
                    + " 5 m below it, produced " + islet.Count + " candidate(s) of which " + isletAccepted
                    + " were accepted, rather than one refused -- " + isletReport, failures);
                string why = islet.Count == 1 ? islet[0].refusal : string.Empty;
                Expect(why.Contains("is water") && why.Contains("The road end is dry but the settlement would not be"),
                    "the islet was refused with " + Quoted(why) + ", which does not tell an author that the ground"
                    + " under the road end was fine and the region around it was not", failures);
                SettlementGroundProbe unasked = new SettlementGroundProbe();
                unasked.surface = island;
                unasked.sampleSpacingMetres = 30.0f;
                unasked.maxDrySearchRings = SettlementGroundProbe.DefaultDrySearchRings;
                List<SettlementCandidate> silent = new List<SettlementCandidate>();
                string silentReport;
                int silentAccepted = SettlementSiting.Propose(termini, catalog, unasked, silent, out silentReport);
                Expect(silentAccepted == 0 && silent.Count == 0, "a probe nobody stated a water level on produced "
                    + silent.Count + " candidate(s), " + silentAccepted + " of them accepted: the pass read the float's"
                    + " own default of zero as a sea at y = 0 instead of refusing the silence", failures);
                Expect(silentReport.Contains("no water level was given"), "the pass refused a probe with no water level"
                    + " by saying " + Quoted(silentReport) + ", which does not name the thing that was missing",
                    failures);

                List<SettlementCandidate> stated = new List<SettlementCandidate>();
                string statedReport;
                int statedAccepted = SettlementSiting.Propose(termini, catalog,
                    SettlementGroundProbe.FromSurface(island, float.NegativeInfinity, 30.0f), stated, out statedReport);
                Expect(statedAccepted == 1, "the same terminus over the same ground, with no water STATED rather than"
                    + " left unsaid, was accepted " + statedAccepted + " time(s) rather than once -- so the refusal"
                    + " above was about the ground and not about the silence: " + statedReport, failures);

                List<SettlementCandidate> nonsense = new List<SettlementCandidate>();
                string nonsenseReport;
                SettlementSiting.Propose(termini, catalog, SettlementGroundProbe.FromSurface(island, float.NaN, 30.0f),
                    nonsense, out nonsenseReport);
                Expect(nonsenseReport.Contains("states neither a water surface nor the absence of one"),
                    "a water level of NaN was answered with " + Quoted(nonsenseReport) + " rather than a refusal saying"
                    + " it states neither a water surface nor the absence of one", failures);
            }
            finally { Object.DestroyImmediate(catalog); }

            Line(builder, "never in water", "an islet is refused, and silence about water is not a dry world");
        }
        #endregion
        #region "The primitives every case leans on"
        /// <summary> How far apart two fingerprints of one order must sit before this gate calls them two different
        /// cities. DERIVED rather than picked: two capitals on different seeds carry shares and orientation but no
        /// circuity, so <c>Compare</c> renormalises to (0.5 family + 0.3 orientation) / 0.8 and ordinary jitter puts
        /// them near 0.08 apart. Two hundredths is an order of magnitude below that and several above float noise, so
        /// this fails only if the seed genuinely stopped mattering. </summary>
        private const float SeedDifferenceThreshold = 0.02f;

        /// <summary> Record a failure when the condition does not hold. The message is written as the FAILURE -- what
        /// was expected beside what was actually found -- because it is the only text an author gets. </summary>
        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (condition) { return; }
            failures.Add(failure);
        }

        /// <summary> One line of the passing report: the prefix, a label, then what was found. </summary>
        private static void Line(StringBuilder builder, string label, string value)
        {
            builder.Append(Prefix).Append("   ").Append(label).Append("  ").Append(value).Append("\n");
        }

        /// <summary> A number for a report. Invariant culture, so a comma-decimal machine prints the text a
        /// dot-decimal one does and a log can be compared between two workstations. </summary>
        private static string Fmt(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary> A list of names as one readable phrase. </summary>
        private static string Join(List<string> parts)
        {
            if (parts == null || parts.Count == 0) { return "nothing"; }
            StringBuilder joined = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) { joined.Append(i == parts.Count - 1 ? " and " : ", "); }
                joined.Append(parts[i]);
            }
            return joined.ToString();
        }

        /// <summary> The name of a transform, or a phrase saying there is no transform -- never a null reference in the
        /// middle of a failure message. </summary>
        private static string Named(Transform t)
        {
            return t == null ? "(nothing)" : t.name;
        }

        /// <summary> Text quoted for a failure message, with nothing and empty told apart. </summary>
        private static string Quoted(string text)
        {
            if (text == null) { return "(nothing)"; }
            if (text.Length == 0) { return "(empty)"; }
            return "\"" + text + "\"";
        }

        /// <summary> Two figures within a tolerance of each other. </summary>
        private static bool Near(double a, double b, double tolerance)
        {
            return System.Math.Abs(a - b) <= tolerance;
        }

        /// <summary> Bake stages as their names, for a message that lists them. </summary>
        private static List<string> Names(List<BakeStage> stages)
        {
            List<string> names = new List<string>();
            if (stages == null) { return names; }
            for (int i = 0; i < stages.Count; i++) { names.Add(stages[i].ToString()); }
            return names;
        }
        #endregion
        #region "Finding a node in the catalogue that a gate is allowed to place"
        /// <summary> The catalogue entries a gate may drop into a graph and still have <c>Validate</c> accept it.
        ///
        /// The filter is not a taste: it is the four things <c>CityGraph.CheckNodeAgainstCatalogue</c> refuses. A blocked
        /// entry is refused outright. An operator-asset entry is refused with no asset wired, and <c>CreateNode</c>
        /// never wires one. A canvas-only entry does not execute, so it is not a stage's worker. An element-placement
        /// entry carries a category and belongs to the element pass rather than to a stage's ordinary work. What is left
        /// is what a graph can hold with nothing else authored. </summary>
        private static List<CityNodeDescriptor> PlainEntries()
        {
            List<CityNodeDescriptor> plain = new List<CityNodeDescriptor>();
            IReadOnlyList<CityNodeDescriptor> all = CityNodeCatalog.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                CityNodeDescriptor entry = all[i];
                if (entry == null) { continue; }
                if (entry.IsBlocked || entry.NeedsOperatorAsset || !entry.Executes) { continue; }
                if (!string.IsNullOrEmpty(entry.elementCategoryId)) { continue; }
                plain.Add(entry);
            }
            return plain;
        }

        /// <summary> The first placeable entry of one stage, or false when the catalogue offers none. </summary>
        private static bool TryPlainNodeOfStage(BakeStage stage, out CityNodeDescriptor descriptor)
        {
            List<CityNodeDescriptor> plain = PlainEntries();
            for (int i = 0; i < plain.Count; i++)
            {
                if (plain[i].stage != stage) { continue; }
                descriptor = plain[i];
                return true;
            }
            descriptor = null;
            return false;
        }

        /// <summary> Which stages cannot be populated, and whether the catalogue says why.
        ///
        /// FILLS the list it is handed, so case (c) can derive its node count from it instead of writing a figure down.
        /// A stage with no placeable entry is only acceptable when the catalogue holds entries for it and every one of
        /// them names a blockage: a stage with no entries at all, or one whose entry was deleted rather than unblocked,
        /// is a hole and is reported as one. </summary>
        private static void CheckEveryUnpopulatedStageIsAKnownBlockage(List<BakeStage> blocked, List<string> failures)
        {
            BakeStage[] order = CityBakePipeline.CanonicalOrder;
            IReadOnlyList<CityNodeDescriptor> all = CityNodeCatalog.Entries;
            for (int s = 0; s < order.Length; s++)
            {
                CityNodeDescriptor placeable;
                if (TryPlainNodeOfStage(order[s], out placeable)) { continue; }

                blocked.Add(order[s]);
                int atStage = 0;
                int blockedAtStage = 0;
                List<string> unstated = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null || all[i].stage != order[s]) { continue; }
                    atStage++;
                    if (all[i].IsBlocked) { blockedAtStage++; }
                    else if (all[i].NeedsOperatorAsset) { unstated.Add(all[i].displayName + " (wants an operator asset)"); }
                    else if (!all[i].Executes) { unstated.Add(all[i].displayName + " (canvas only)"); }
                    else { unstated.Add(all[i].displayName + " (an element category)"); }
                }

                Expect(atStage > 0, "the catalogue holds no entry at all for the " + order[s]
                    + " stage, so that stage of every bake is silently empty", failures);
                Expect(atStage > 0 && blockedAtStage == atStage,
                    "the " + order[s] + " stage cannot be populated by shipped code alone, and no entry there names a"
                    + " blockage to say so: " + Join(unstated) + ". A stage that only runs when an author wires"
                    + " something is a decision, so either give the entry a blockedReason or ship a plain one",
                    failures);
            }
        }
        #endregion
        #region "Graphs built to be refused, and the search that finds the ports to build them from"
        /// <summary> Assert that a graph is refused, and that the refusal names the fault. Destroys the graph it is
        /// handed, and treats a graph the search could not build as already reported rather than as a pass. </summary>
        private static void ExpectRefusal(CityGraph graph, string phrase, string what, List<string> failures)
        {
            if (graph == null) { return; }
            try
            {
                string why;
                if (graph.Validate(out why))
                {
                    failures.Add("Validate accepted " + what + ", so a bake would be opened over it");
                    return;
                }
                Expect(why != null && why.Contains(phrase),
                    "Validate refused " + what + ", but without saying so: the refusal reads " + Quoted(why)
                    + " and does not contain " + Quoted(phrase) + ", so an author is told something is wrong and not"
                    + " what", failures);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        /// <summary> A pair of placeable entries whose payloads DISAGREE, at stages that do not, so a wire between them
        /// carries exactly one fault. Searched rather than named, because a kind written down here would start failing
        /// on the day somebody gave that kind another port. </summary>
        private static bool TryFindMismatchedPair(out CityNodeDescriptor from, out string fromPort,
            out CityNodeDescriptor to, out string toPort)
        {
            List<CityNodeDescriptor> plain = PlainEntries();
            for (int a = 0; a < plain.Count; a++)
            {
                for (int o = 0; o < plain[a].outputs.Count; o++)
                {
                    for (int b = 0; b < plain.Count; b++)
                    {
                        if (CityGraph.StageIndex(plain[a].stage) > CityGraph.StageIndex(plain[b].stage)) { continue; }
                        for (int i = 0; i < plain[b].inputs.Count; i++)
                        {
                            if (plain[a].outputs[o].payload == plain[b].inputs[i].payload) { continue; }
                            from = plain[a];
                            fromPort = plain[a].outputs[o].name;
                            to = plain[b];
                            toPort = plain[b].inputs[i].name;
                            return true;
                        }
                    }
                }
            }
            from = null; fromPort = null; to = null; toPort = null;
            return false;
        }
        /// <summary> An entry that can be wired to a second copy of itself with no payload complaint: one of its outputs
        /// carries what one of its inputs reads. That is what makes a two-node loop carry the cycle fault alone.
        /// </summary>
        private static bool TryFindSelfCompatibleEntry(out CityNodeDescriptor entry, out string outPort,
            out string inPort)
        {
            List<CityNodeDescriptor> plain = PlainEntries();
            for (int a = 0; a < plain.Count; a++)
            {
                for (int o = 0; o < plain[a].outputs.Count; o++)
                {
                    for (int i = 0; i < plain[a].inputs.Count; i++)
                    {
                        if (plain[a].outputs[o].payload != plain[a].inputs[i].payload) { continue; }
                        entry = plain[a];
                        outPort = plain[a].outputs[o].name;
                        inPort = plain[a].inputs[i].name;
                        return true;
                    }
                }
            }
            entry = null; outPort = null; inPort = null;
            return false;
        }

        /// <summary> A pair whose stages run the wrong way round: an output from a later stage into an input in an
        /// earlier one. Payload-matching pairs are preferred so the backward stage is the only fault; a mismatched pair
        /// is accepted as a second choice, since the refusal being asserted is still the one about the stage. </summary>
        private static bool TryFindBackwardPair(out CityNodeDescriptor late, out string latePort,
            out CityNodeDescriptor early, out string earlyPort)
        {
            List<CityNodeDescriptor> plain = PlainEntries();
            late = null; latePort = null; early = null; earlyPort = null;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int a = 0; a < plain.Count; a++)
                {
                    for (int b = 0; b < plain.Count; b++)
                    {
                        if (CityGraph.StageIndex(plain[a].stage) <= CityGraph.StageIndex(plain[b].stage)) { continue; }
                        for (int o = 0; o < plain[a].outputs.Count; o++)
                        {
                            for (int i = 0; i < plain[b].inputs.Count; i++)
                            {
                                bool same = plain[a].outputs[o].payload == plain[b].inputs[i].payload;
                                if (pass == 0 && !same) { continue; }
                                late = plain[a]; latePort = plain[a].outputs[o].name;
                                early = plain[b]; earlyPort = plain[b].inputs[i].name;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        /// <summary> One wire, added to the graph. </summary>
        private static void Wire(CityGraph graph, CityGraphNode from, string fromPort, CityGraphNode to, string toPort)
        {
            CityGraphEdge edge = new CityGraphEdge();
            edge.fromNodeId = from.id;
            edge.fromPort = fromPort;
            edge.toNodeId = to.id;
            edge.toPort = toPort;
            graph.edges.Add(edge);
        }

        /// <summary> A graph whose one wire carries a payload into a port that reads another. </summary>
        private static CityGraph MismatchedPortGraph(List<string> failures)
        {
            CityNodeDescriptor from, to;
            string fromPort, toPort;
            if (!TryFindMismatchedPair(out from, out fromPort, out to, out toPort))
            {
                failures.Add("no two placeable catalogue entries carry ports of differing payloads, so a mismatched"
                    + " wire could not be built and the payload rule went untested");
                return null;
            }

            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            CityGraphNode source = CityNodeCatalog.CreateNode(from);
            CityGraphNode sink = CityNodeCatalog.CreateNode(to);
            graph.nodes.Add(source);
            graph.nodes.Add(sink);
            Wire(graph, source, fromPort, sink, toPort);
            return graph;
        }

        /// <summary> A graph holding one node and a wire into an id nothing in it carries. </summary>
        private static CityGraph DanglingEdgeGraph(List<string> failures)
        {
            CityNodeDescriptor descriptor = null;
            List<CityNodeDescriptor> plain = PlainEntries();
            for (int i = 0; i < plain.Count && descriptor == null; i++)
            {
                if (plain[i].outputs.Count > 0) { descriptor = plain[i]; }
            }
            if (descriptor == null)
            {
                failures.Add("no placeable catalogue entry declares an output, so a wire leading nowhere could not be"
                    + " built and the dangling-end rule went untested");
                return null;
            }

            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            CityGraphNode only = CityNodeCatalog.CreateNode(descriptor);
            graph.nodes.Add(only);
            CityGraphEdge edge = new CityGraphEdge();
            edge.fromNodeId = only.id;
            edge.fromPort = descriptor.outputs[0].name;
            edge.toNodeId = "no-node-here-carries-this-id";
            edge.toPort = descriptor.outputs[0].name;
            graph.edges.Add(edge);
            return graph;
        }
        /// <summary> Two nodes of one kind, each feeding the other, so neither can ever be the one that runs first.
        /// </summary>
        private static CityGraph CycleGraph(List<string> failures)
        {
            CityNodeDescriptor entry;
            string outPort, inPort;
            if (!TryFindSelfCompatibleEntry(out entry, out outPort, out inPort))
            {
                failures.Add("no placeable catalogue entry both reads and writes one payload, so a two-node loop could"
                    + " not be built without also mismatching a payload, and the cycle rule went untested");
                return null;
            }

            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            CityGraphNode one = CityNodeCatalog.CreateNode(entry);
            CityGraphNode two = CityNodeCatalog.CreateNode(entry);
            graph.nodes.Add(one);
            graph.nodes.Add(two);
            Wire(graph, one, outPort, two, inPort);
            Wire(graph, two, outPort, one, inPort);
            return graph;
        }

        /// <summary> A wire out of a later bake stage into an earlier one: a value that would have to exist before the
        /// node that produces it has run. </summary>
        private static CityGraph BackwardStageGraph(List<string> failures)
        {
            CityNodeDescriptor late, early;
            string latePort, earlyPort;
            if (!TryFindBackwardPair(out late, out latePort, out early, out earlyPort))
            {
                failures.Add("no placeable catalogue entry of a later bake stage writes anything an earlier stage's"
                    + " entry reads, so a backward wire could not be built and the stage rule went untested");
                return null;
            }

            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            CityGraphNode producer = CityNodeCatalog.CreateNode(late);
            CityGraphNode consumer = CityNodeCatalog.CreateNode(early);
            graph.nodes.Add(producer);
            graph.nodes.Add(consumer);
            Wire(graph, producer, latePort, consumer, earlyPort);
            return graph;
        }
        #endregion
        #region "Walking the stages, and what a walk is allowed to report"
        /// <summary> One placeable node per populatable stage, inserted from the LAST stage to the first, and the count
        /// of them. Backwards on purpose: the node list is then in exactly the wrong order, so a walk that reported the
        /// canonical sequence by echoing the list rather than by walking the stages would be caught. </summary>
        private static int InsertOneOrdinaryNodePerStageInReverse(CityGraph graph)
        {
            BakeStage[] order = CityBakePipeline.CanonicalOrder;
            int placed = 0;
            for (int s = order.Length - 1; s >= 0; s--)
            {
                CityNodeDescriptor descriptor;
                if (!TryPlainNodeOfStage(order[s], out descriptor)) { continue; }
                graph.nodes.Add(CityNodeCatalog.CreateNode(descriptor));
                placed++;
            }
            return placed;
        }

        /// <summary> Pump the walk to its end and assert what it visited.
        ///
        /// The walk was opened with an id no node carries, so every node is stepped around: that is what keeps this a
        /// test of the ORDER. It is asserted rather than assumed -- nothing ran, nothing was passed over by a dispatch,
        /// and the number stepped around equals the number placed -- because a preview that silently ran a node would
        /// otherwise turn this case into a test of five operators against a deliberately empty request. </summary>
        private static void WalkAndAssertStageOrder(CityBakeWalk walk, int placed, List<string> failures)
        {
            while (CityBakeRunner.StepOneStage(walk)) { }

            CityBakeReport report;
            bool finished = CityBakeRunner.Finish(walk, out report);

            Expect(walk.skippedForPreview == placed && report.nodesRun == 0 && report.nodesPassedOver == 0,
                "the walk was opened for a node no graph carries, so all " + placed + " nodes should have been stepped"
                + " around; instead " + walk.skippedForPreview + " were stepped around, " + report.nodesRun
                + " ran and " + report.nodesPassedOver + " were dispatched and refused -- so this case is testing"
                + " operators rather than the order of the stages", failures);

            Expect(report.observedStages.Count == placed,
                "the walk visited " + report.observedStages.Count + " stages (" + Join(Names(report.observedStages))
                + ") for " + placed + " populated ones, so a stage was visited twice or missed", failures);

            string reason;
            Expect(CityBakePipeline.IsInCanonicalOrder(report.observedStages, out reason),
                "the nodes were inserted from the last stage to the first, and the walk followed the list instead of"
                + " the canonical order: " + reason, failures);

            Expect(placed == 0 || report.observedStages[0] == BakeStage.LoadOrGenerate,
                "the first stage the walk reached was " + (report.observedStages.Count == 0 ? "none at all"
                    : report.observedStages[0].ToString()) + " rather than LoadOrGenerate, so a bake would compute"
                + " before it had read anything", failures);

            Expect(finished, "the walk pumped every stage and was still refused at the close: " + report.report,
                failures);
        }
        /// <summary> The order rule as a pure function: a reversed sequence and a repeated stage are refused, and a
        /// SKIPPED stage is not. The last of the three is the one worth pinning -- a city with no scatter to bake has
        /// nothing wrong with it, so an order rule that demanded all five stages would refuse an honest bake.
        /// </summary>
        private static void CheckReversedSequenceIsRefused(List<string> failures)
        {
            BakeStage[] order = CityBakePipeline.CanonicalOrder;
            string reason;

            List<BakeStage> reversed = new List<BakeStage>();
            for (int i = order.Length - 1; i >= 0; i--) { reversed.Add(order[i]); }
            Expect(!CityBakePipeline.IsInCanonicalOrder(reversed, out reason),
                "the stage sequence " + Join(Names(reversed)) + " was accepted as canonical, so a runner that walked"
                + " the bake backwards would report a good bake", failures);

            List<BakeStage> repeated = new List<BakeStage>();
            repeated.Add(order[0]);
            repeated.Add(order[0]);
            Expect(!CityBakePipeline.IsInCanonicalOrder(repeated, out reason),
                "the same stage twice in a row was accepted as canonical, so a stage re-entered halfway through a"
                + " second bake would go unnoticed", failures);

            List<BakeStage> skipped = new List<BakeStage>();
            for (int i = 0; i < order.Length; i += 2) { skipped.Add(order[i]); }
            Expect(CityBakePipeline.IsInCanonicalOrder(skipped, out reason),
                "the sequence " + Join(Names(skipped)) + " was refused as out of order (" + reason + "), so a city"
                + " with nothing to do in a stage could not be baked at all", failures);
        }

        /// <summary> A node removed while one of its wires stayed behind is refused rather than tolerated. Built on the
        /// model, so it holds however the node was removed and whoever forgot the edge. </summary>
        private static void CheckWidowedEdgeIsRefused(List<string> failures)
        {
            CityNodeDescriptor from, to;
            string fromPort, toPort;
            if (!TryFindMismatchedPair(out from, out fromPort, out to, out toPort))
            {
                failures.Add("no two placeable catalogue entries could be wired at all, so a node could not be removed"
                    + " from under a wire and the widowed-edge net went untested");
                return;
            }

            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            try
            {
                CityGraphNode source = CityNodeCatalog.CreateNode(from);
                CityGraphNode sink = CityNodeCatalog.CreateNode(to);
                graph.nodes.Add(source);
                graph.nodes.Add(sink);
                Wire(graph, source, fromPort, sink, toPort);

                graph.nodes.Remove(source);
                string why;
                Expect(!graph.Validate(out why),
                    "a graph that lost the node at one end of a wire, and kept the wire, was accepted -- so a delete"
                    + " that forgot an edge would ship and the bake would read a value from nowhere", failures);
            }
            finally { Object.DestroyImmediate(graph); }
        }
        #endregion
        #region "Reading the model back by reflection, so a rename cannot pass unnoticed"
        /// <summary> Every field of a type that Unity will write into a scene: public instance fields, and private ones
        /// carrying <c>SerializeField</c>. Constants, readonly fields and anything marked <c>NonSerialized</c> are left
        /// out because Unity does not write them either. The base classes are walked rather than skipped, and drop out
        /// on their own -- what MonoBehaviour and Object carry is private and native, so the same filter excludes it.
        /// </summary>
        private static List<string> SerializedFieldNames(System.Type type)
        {
            List<string> names = new List<string>();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsLiteral || field.IsInitOnly || field.IsNotSerialized) { continue; }
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) { continue; }
                names.Add(field.Name);
            }
            return names;
        }

        /// <summary> Every parameter name any node kind declares, from the catalogue rather than from a list here.
        /// </summary>
        private static HashSet<string> AllNodeParameterNames()
        {
            HashSet<string> names = new HashSet<string>();
            IReadOnlyList<CityNodeDescriptor> all = CityNodeCatalog.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) { continue; }
                for (int p = 0; p < all[i].parameters.Count; p++)
                {
                    string name = all[i].parameters[p].Name;
                    if (!string.IsNullOrEmpty(name)) { names.Add(name); }
                }
            }
            return names;
        }

        /// <summary> The field names the inspector table carries for one component key. </summary>
        private static HashSet<string> RowFieldsFor(string component)
        {
            HashSet<string> rows = new HashSet<string>();
            IList<FieldDescriptor> descriptors = InspectorFieldRegistry.ForComponent(component);
            if (descriptors == null) { return rows; }
            for (int i = 0; i < descriptors.Count; i++) { rows.Add(descriptors[i].field); }
            return rows;
        }

        /// <summary> Parameter names per node kind, keyed by the kind's own <c>ToString()</c> -- the same expression the
        /// row table builds its component key from. Entries sharing a kind are unioned, which is what the element
        /// placement entries need: they are one kind wearing many categories. </summary>
        private static Dictionary<string, HashSet<string>> ParametersByKindName()
        {
            Dictionary<string, HashSet<string>> byKind = new Dictionary<string, HashSet<string>>();
            IReadOnlyList<CityNodeDescriptor> all = CityNodeCatalog.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || all[i].parameters.Count == 0) { continue; }
                string key = all[i].kind.ToString();
                HashSet<string> names;
                if (!byKind.TryGetValue(key, out names))
                {
                    names = new HashSet<string>();
                    byKind.Add(key, names);
                }
                for (int p = 0; p < all[i].parameters.Count; p++)
                {
                    string name = all[i].parameters[p].Name;
                    if (!string.IsNullOrEmpty(name)) { names.Add(name); }
                }
            }
            return byKind;
        }
        #endregion
        #region "Baking, and the dump two bakes are compared through"
        /// <summary> Both canvas positions come back off disk on the right nodes. A tolerance rather than equality,
        /// because a position survives a round trip through the asset's text and back, and this case is about a layout
        /// being remembered rather than about the last bit of a float. </summary>
        private static void CheckReloadedLayout(CityGraph reloaded, string leftId, List<string> failures)
        {
            CityGraphNode left = null;
            CityGraphNode right = null;
            for (int i = 0; i < reloaded.nodes.Count; i++)
            {
                if (reloaded.nodes[i] == null) { continue; }
                if (reloaded.nodes[i].id == leftId) { left = reloaded.nodes[i]; }
                else { right = reloaded.nodes[i]; }
            }

            Expect(left != null,
                "no node in the reloaded graph carries the id " + Quoted(leftId) + " the saved one did, so a reload"
                + " re-mints ids and every wire in a saved graph would point at nothing", failures);
            Expect(left != null && Near(left.canvasPosition.x, -317.25, 1e-3) && Near(left.canvasPosition.y, -84.5, 1e-3),
                "the left node came back at " + (left == null ? "(no node)" : left.canvasPosition.ToString())
                + " rather than (-317.25, -84.5), so an author's layout is redrawn every time the graph is opened",
                failures);
            Expect(right != null && Near(right.canvasPosition.x, 612.75, 1e-3)
                && Near(right.canvasPosition.y, 233.125, 1e-3),
                "the right node came back at " + (right == null ? "(no node)" : right.canvasPosition.ToString())
                + " rather than (612.75, 233.125), so only one of the two positions is being written", failures);
        }

        /// <summary> A context over one request, for a case that needs two of them. Written here rather than inline
        /// because the point of case (e) is that the ordinal counter belongs to the CONTEXT: a second context is a
        /// second bake. </summary>
        private static CityBakeContext ContextOver(CityBakeRequest request)
        {
            CityBakeContext context = new CityBakeContext();
            context.request = request;
            return context;
        }
        /// <summary> Bake a graph of the LoadOrGenerate sources once at one seed, and return everything about the result
        /// that a bake is allowed to be judged on.
        ///
        /// The dump carries the verdict, the stages reached, the two node tallies, and per node its KIND, its position
        /// in the dump, whether it ran, how many items it produced and the note it left. It leaves out the milliseconds
        /// and the runner's closing sentence, because a wall clock is not an output, and it leaves out the node ids,
        /// because those are minted fresh for each of the two graphs and comparing them would fail on a bake that is
        /// perfectly reproducible. </summary>
        private static string BakeSourcesOnce(int seed, List<string> failures)
        {
            CityGraph graph = ScriptableObject.CreateInstance<CityGraph>();
            try
            {
                List<CityNodeDescriptor> plain = PlainEntries();
                int sources = 0;
                for (int i = 0; i < plain.Count; i++)
                {
                    if (plain[i].role != CityNodeRole.Source) { continue; }
                    if (plain[i].stage != BakeStage.LoadOrGenerate) { continue; }
                    graph.nodes.Add(CityNodeCatalog.CreateNode(plain[i]));
                    sources++;
                }
                if (sources == 0)
                {
                    failures.Add("the catalogue offers no placeable LoadOrGenerate source, so a bake could not be run"
                        + " at all and reproducibility went untested");
                    return "no sources";
                }

                CityBakeRequest request = new CityBakeRequest();
                request.citySeed = seed;

                CityBakeReport report;
                CityBakeRunner.Run(graph, request, out report);

                StringBuilder dump = new StringBuilder();
                dump.Append("ok=").Append(report.ok ? "yes" : "no");
                dump.Append(" stages=").Append(Join(Names(report.observedStages)));
                dump.Append(" ran=").Append(report.nodesRun).Append(" passed=").Append(report.nodesPassedOver);
                for (int i = 0; i < report.runs.Count; i++)
                {
                    CityNodeRun run = report.runs[i];
                    dump.Append("\n    ").Append(i).Append(' ').Append(run.kind).Append(' ').Append(run.stage);
                    dump.Append(" ran=").Append(run.ran ? "yes" : "no");
                    dump.Append(" items=").Append(run.itemCount);
                    dump.Append(" note=").Append(Quoted(run.note));
                }
                return dump.ToString();
            }
            finally { Object.DestroyImmediate(graph); }
        }
        /// <summary> How many different assets one requester is handed across a run of city seeds, out of a category
        /// holding a given number of validated candidates. One requester and one category throughout, so the only thing
        /// varying is the seed -- which is the claim: two cities furnish differently. </summary>
        private static int DistinctContentPicksAcrossSeeds(int seeds, int candidates)
        {
            const string Category = "content.probe.rcmcitygraph";
            ContentCatalog catalog = new ContentCatalog();
            for (int i = 0; i < candidates; i++)
            {
                ContentCandidate candidate = new ContentCandidate();
                candidate.category = Category;
                candidate.assetId = "probe." + i.ToString(CultureInfo.InvariantCulture);
                candidate.passedValidation = true;
                catalog.Register(candidate);
            }

            HashSet<string> picked = new HashSet<string>();
            for (int s = 0; s < seeds; s++)
            {
                ulong citySeed = (ulong)(1000 + s * 7919);
                ContentResolution resolution = ContentResolver.Resolve(catalog, Category, "lot.probe.1", citySeed);
                if (!resolution.Resolved) { continue; }
                picked.Add(resolution.asset.assetId);
            }
            return picked.Count;
        }
        #endregion
        #region "Road ends, settlements, and the two comparisons the fingerprint is asked for"
        /// <summary> One road end for the siting pass to judge. The road runs east, and it is the road's End that is
        /// offered, so <c>RoadTerminus.OutwardDirectionXZ</c> gives +x -- which is what lets case (l) assert the facing
        /// against the tangent rather than against a direction written down here. The surface height is the centre's own
        /// y, so a terminus is level with the ground it is quoted at unless a case says otherwise. </summary>
        private static RoadTerminusInput TerminusAt(Vector3 endNodeCentre, SettlementRoleEnum role, string roadName)
        {
            RoadTerminusInput terminus = new RoadTerminusInput();
            terminus.roadName = roadName;
            terminus.whichEnd = RoadEnd.End;
            terminus.role = role;
            terminus.crossSection = Road.RoadClassEnum.UrbanTwoLane;
            terminus.endNodeCentre = endNodeCentre;
            terminus.roadSurfaceHeightMetres = endNodeCentre.y;
            terminus.tangentAtEndXZ = new Vector2(1.0f, 0.0f);
            terminus.archetypeOverride = SettlementArchetype.Auto;
            return terminus;
        }

        /// <summary> One terminus as the list the siting pass takes. </summary>
        private static List<RoadTerminusInput> Only(RoadTerminusInput terminus)
        {
            List<RoadTerminusInput> one = new List<RoadTerminusInput>();
            one.Add(terminus);
            return one;
        }

        /// <summary> Two things the siting pass must NOT read: a road with no settlement role seeds nothing, and the
        /// cross-section does not choose the order.
        ///
        /// The second is the one worth pinning. The order comes from the ROLE the author set, so a village lane built as
        /// a motorway is still a village -- otherwise an author widening a road would silently promote the place at the
        /// end of it. Both are offered over the same ground as case (l), so a difference in the answer can only be the
        /// thing being varied. </summary>
        private static void CheckRolelessAndWidthDoNotMatter(SettlementOrderCatalog catalog,
            SettlementGroundProbe probe, List<string> failures)
        {
            RoadTerminusInput roleless = TerminusAt(new Vector3(700.0f, 40.0f, 1024.0f), SettlementRoleEnum.None,
                "an ordinary road");
            List<SettlementCandidate> nothing = new List<SettlementCandidate>();
            string silentReport;
            int seeded = SettlementSiting.Propose(Only(roleless), catalog, probe, nothing, out silentReport);
            Expect(seeded == 0 && nothing.Count == 0,
                "a road with no settlement role seeded " + seeded + " site(s) and " + nothing.Count
                + " candidate(s), so building an ordinary road would plant a town at the end of it", failures);

            RoadTerminusInput motorway = TerminusAt(new Vector3(900.0f, 40.0f, 1024.0f),
                SettlementRoleEnum.VillageConnector, "a village lane built wide");
            motorway.crossSection = Road.RoadClassEnum.Motorway;
            List<SettlementCandidate> wide = new List<SettlementCandidate>();
            string wideReport;
            SettlementSiting.Propose(Only(motorway), catalog, probe, wide, out wideReport);
            Expect(wide.Count == 1 && wide[0].orderIndex == SettlementOrderCatalog.VillageOrder,
                "a village connector built as a motorway proposed order "
                + (wide.Count == 1 ? wide[0].orderIndex + " (" + wide[0].orderName + ")" : "nothing at all")
                + " rather than the catalogue's village order " + SettlementOrderCatalog.VillageOrder
                + ", so widening a road would promote the place at the end of it: " + wideReport, failures);
        }
        /// <summary> Whether the shared default city graph is already in the project, and where it lives.
        ///
        /// Case (m) presses a button that CREATES that asset when it is missing, so the gate has to know whether it was
        /// there beforehand: deleting it afterwards when the author already had one takes a real asset away, and leaving
        /// one behind when they did not means a gate wrote into a project it is only supposed to read.
        ///
        /// The path is composed the way <c>CitySettlementFactory</c> composes it -- the tool's own base path, then the
        /// same folder and file name -- rather than written out flat, so moving the tool folder cannot turn this into a
        /// gate that quietly deletes nothing and reports success. A base path the tool cannot resolve is reported and
        /// then read as "already there", which is the answer that touches no asset. </summary>
        private static bool DefaultGraphAssetExists(out string path, List<string> failures)
        {
            path = string.Empty;
            string root;
            try { root = RoadEditorUtility.GetBasePath().Replace('\\', '/'); }
            catch (System.Exception error)
            {
                failures.Add("the tool could not say where its own folder is, so this case cannot tell whether it made"
                    + " the default city graph or found one already there -- " + error.Message);
                return true;
            }
            if (string.IsNullOrEmpty(root))
            {
                failures.Add("the tool reported an empty base path, so this case cannot tell whether it made the"
                    + " default city graph or found one already there");
                return true;
            }
            path = root + "/City/Graphs/DefaultCityGraph.asset";
            return AssetDatabase.LoadAssetAtPath<CityGraph>(path) != null;
        }
        /// <summary> Puts one road with two free ends under this road system, and gives it a settlement role.
        ///
        /// The nodes are built by hand -- component, parent, <c>spline</c>, <c>idOnSpline</c>, <c>pos</c>, then into the
        /// spline's node list -- rather than through <c>RoadAutomation.CreateNodeProgrammatically</c>, which refuses a
        /// position with no terrain under it (B-13) and would leave this case holding a road of no nodes on a machine
        /// whose scene has no terrain. The sweep reads the node list, each node's <c>pos</c> and <c>isIntersection</c>,
        /// and the road's <c>settlementRole</c>, so those are exactly what is set.
        ///
        /// <c>UpdateRoad</c> is deliberately not called. Building the mesh is not the claim, it would pull the whole
        /// constructor and its terrain sampling in behind it, and the sweep never looks at it. </summary>
        private static Road AddTwoNodeRoad(RoadSystem system, SettlementRoleEnum role, Vector3 start, Vector3 end)
        {
            GameObject roadObject = system.AddRoad(false);
            Road road = roadObject.GetComponent<Road>();
            road.settlementRole = role;
            AddSplineNode(road, 0, start);
            AddSplineNode(road, 1, end);
            return road;
        }

        /// <summary> One node onto the end of a road's spline, at the position given and not an intersection -- so both
        /// ends of a two-node road are free termini and the sweep has somewhere to put a settlement. </summary>
        private static void AddSplineNode(Road road, int idOnSpline, Vector3 position)
        {
            GameObject nodeObject = new GameObject("Node" + (idOnSpline + 1).ToString(CultureInfo.InvariantCulture));
            nodeObject.transform.parent = road.splineObject.transform;
            nodeObject.transform.position = position;
            SplineN node = nodeObject.AddComponent<SplineN>();
            node.spline = road.spline;
            node.idOnSpline = idOnSpline;
            node.pos = position;
            node.isIntersection = false;
            road.spline.nodes.Add(node);
        }
        /// <summary> The candidate proposed at one settlement order, found by that order rather than by its position in
        /// the list -- the list's order follows the termini the case handed in, and a pass that reordered them would
        /// otherwise make this gate assert the wrong candidate's refusal. </summary>
        private static bool TryCandidateOfOrder(List<SettlementCandidate> candidates, int orderIndex,
            out SettlementCandidate found)
        {
            found = default(SettlementCandidate);
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                if (candidates[i].orderIndex != orderIndex) { continue; }
                found = candidates[i];
                return true;
            }
            return false;
        }
        /// <summary> The catchment claim: the city keeps its site, the village inside its catchment loses one, and the
        /// refusal carries both numbers an author would need to argue with it.
        ///
        /// The separation is read back off the two anchors rather than repeated from the case, and both figures are
        /// formatted the way <c>CatchmentRefusalText</c> formats them -- whole metres, through the same default
        /// formatting -- so the substring test cannot fail merely because this file rounded differently or forced a
        /// culture the refusal did not use.
        ///
        /// The neighbour is asserted by identity too. A refusal that named the wrong end would still contain the right
        /// distance, and an author sent to the wrong road is worse served than one sent to none. </summary>
        private static void CheckCatchmentRefusal(List<SettlementCandidate> candidates, float expectedBreakpointMetres,
            string report, List<string> failures)
        {
            SettlementCandidate city, village;
            if (!TryCandidateOfOrder(candidates, SettlementOrderCatalog.CityOrder, out city)
                || !TryCandidateOfOrder(candidates, SettlementOrderCatalog.VillageOrder, out village))
            {
                failures.Add("the two termini did not produce one city candidate and one village candidate, so the"
                    + " catchment rule had nothing to refuse -- " + report);
                return;
            }
            float separation = Vector2.Distance(new Vector2(city.anchor.x, city.anchor.z),
                new Vector2(village.anchor.x, village.anchor.z));

            Expect(city.accepted, "the city 1500 m from a village was itself refused (" + city.refusal + "), so the"
                + " catchment rule rejected the larger centre instead of the smaller one and the author is left with a"
                + " highway and no city on it", failures);
            Expect(!village.accepted, "a village " + Fmt(separation) + " m from a city of "
                + city.proposedPopulation + " people was accepted, so ground inside a larger neighbour's catchment is"
                + " being seeded as a settlement of its own -- " + report, failures);
            Expect(Near(village.breakpointMetres, expectedBreakpointMetres, 0.5),
                "the refused village carries a breakpoint of " + Fmt(village.breakpointMetres) + " m where the shipped"
                + " formula gives " + Fmt(expectedBreakpointMetres) + " m, so the refusal is not reporting the rule it"
                + " was made by", failures);
            Expect(village.refusal.Contains(separation.ToString("0") + " m from"),
                "the refusal does not carry the " + separation.ToString("0") + " m separation it was decided on: "
                + Quoted(village.refusal), failures);
            Expect(village.refusal.Contains(expectedBreakpointMetres.ToString("0") + " m out from it"),
                "the refusal does not carry the breakpoint " + expectedBreakpointMetres.ToString("0")
                + " m measured out from the larger centre, so an author cannot check the arithmetic: "
                + Quoted(village.refusal), failures);
            Expect(village.catchmentNeighbour == city.Identity,
                "the refusal names " + Quoted(village.catchmentNeighbour) + " as the neighbour that swallowed the site"
                + " rather than " + Quoted(city.Identity) + ", so it sends the author to the wrong road", failures);
        }
        /// <summary> The length-to-width ratio of the rectangle whose shape factor is exactly the one asked for.
        ///
        /// The shape factor of a rectangle is 4A/(pi d squared) with d its diagonal, so for width w and length kw it is
        /// 4k/(pi(1 + k squared)). Setting c = 4/(pi phi) turns that into k squared - ck + 1 = 0, whose larger root is
        /// the ratio wanted. Solved rather than searched, so the block built from it lands ON its family's prototype
        /// instead of near it, and the case's margins are the ones the arithmetic says they are. </summary>
        private static double AspectForShapeFactor(double shapeFactor)
        {
            double c = 4.0 / (System.Math.PI * shapeFactor);
            double discriminant = c * c - 4.0;
            if (discriminant < 0.0) { discriminant = 0.0; }
            return (c + System.Math.Sqrt(discriminant)) / 2.0;
        }

        /// <summary> The shape factor of one family's prototype, read from the fingerprint's own published constants so
        /// a change to the plane moves this gate's blocks with it. </summary>
        private static double ProbeShapeFactor(int family)
        {
            if (family == 0) { return SettlementFingerprint.PhiOfSquare; }
            if (family == 2) { return SettlementFingerprint.PhiSplit; }
            return SettlementFingerprint.PhiOfQuarterStrip;
        }

        /// <summary> The area of one family's prototype: the published area split, moved by that family's offset along
        /// the log-area axis -- half a decade below the split for the two small families, at the split for the mixed
        /// one, a decade above it for the large one. </summary>
        private static double ProbeAreaM2(int family)
        {
            double dex = family == 2 ? 0.0 : (family == 3 ? 1.0 : -0.5);
            return SettlementFingerprint.AreaSplitM2 * System.Math.Pow(10.0, dex);
        }

        /// <summary> One rectangular block of a given area and ratio, laid flat and clear of its neighbours.
        ///
        /// Only the polygon is filled: the fingerprint reads area and diagonal off it and nothing else, and a block's
        /// boundary node list belongs to a network this case does not have. The length is taken from the width AFTER the
        /// width has been rounded to a float, so the ratio -- which is what fixes the shape factor -- survives exactly
        /// while the area moves by a part in ten million, where it cannot reach the log-area axis. </summary>
        private static Block RectangleBlock(int id, double areaM2, double aspect, float offsetX)
        {
            float width = (float)System.Math.Sqrt(areaM2 / aspect);
            float length = (float)(aspect * width);
            Block block = new Block();
            block.id = id;
            block.polygon.Clear();
            block.polygon.Add(new Vector3(offsetX, 0.0f, 0.0f));
            block.polygon.Add(new Vector3(offsetX + width, 0.0f, 0.0f));
            block.polygon.Add(new Vector3(offsetX + width, 0.0f, length));
            block.polygon.Add(new Vector3(offsetX, 0.0f, length));
            return block;
        }
        /// <summary> How many blocks of each family a mix in this order's proportions holds, over a fixed block count.
        ///
        /// Largest remainder, not a rounded guess: floor each exact share, then hand the leftover blocks to the families
        /// with the largest fractions. That is the integer mix nearest the order's own weights, and it keeps the case
        /// honest if somebody retunes a weight -- the mix follows the catalogue instead of this file. </summary>
        private static int[] MixCountsFor(SettlementOrderClass order, int blockCount)
        {
            int[] counts = new int[4];
            double[] remainder = new double[4];
            int assigned = 0;
            for (int i = 0; i < 4; i++)
            {
                double exact = order.NormalisedFamilyWeight((StreetPatternFamily)i) * blockCount;
                counts[i] = (int)System.Math.Floor(exact);
                remainder[i] = exact - counts[i];
                assigned += counts[i];
            }
            while (assigned < blockCount)
            {
                int best = 0;
                for (int i = 1; i < 4; i++) { if (remainder[i] > remainder[best]) { best = i; } }
                counts[best]++;
                remainder[best] = -1.0;
                assigned++;
            }
            return counts;
        }

        /// <summary> The block list one mix describes: that many copies of each family's prototype rectangle, spaced a
        /// kilometre apart so no two share a vertex. Spacing is cosmetic -- area and diagonal are both unaffected by
        /// where a block stands -- but a list of coincident rings would not look like a city to anyone reading it in the
        /// debugger. </summary>
        private static List<Block> MixFor(int[] counts)
        {
            List<Block> blocks = new List<Block>();
            for (int family = 0; family < 4; family++)
            {
                double areaM2 = ProbeAreaM2(family);
                double aspect = AspectForShapeFactor(ProbeShapeFactor(family));
                for (int n = 0; n < counts[family]; n++)
                {
                    blocks.Add(RectangleBlock(blocks.Count + 1, areaM2, aspect, blocks.Count * 1000.0f));
                }
            }
            return blocks;
        }
        /// <summary> That a mix reads back as the mix that was built, before anything is concluded from comparing it.
        ///
        /// Three separate claims, because each of them failing would leave the comparison meaningless in a different
        /// way. Every block measured: a ring the fingerprint skipped would quietly shrink the sample. Each share equal
        /// to its own count over the total: a prototype rectangle landing on the wrong family would still produce four
        /// shares summing to one, and the comparison would then be between two mixes nobody built. Each share within one
        /// block's granularity of the order's own weight: that is what makes this the CAPITAL's mix rather than an
        /// arbitrary one. And no orientation and no circuity, because the whole margin argument rests on the family term
        /// being the only term <c>Compare</c> weighs. </summary>
        private static bool CheckMixReadsAsBuilt(SettlementFingerprint measured, int[] counts,
            SettlementOrderClass order, string what, int blockCount, List<string> failures)
        {
            bool ok = true;
            if (measured.blocksMeasured != blockCount)
            {
                failures.Add("the " + what + "'s mix was built from " + blockCount + " blocks and only "
                    + measured.blocksMeasured + " were measured, so the shares compared below are not the mix built");
                ok = false;
            }
            double granularity = 0.5 / blockCount + 0.001;
            for (int i = 0; i < 4; i++)
            {
                StreetPatternFamily family = (StreetPatternFamily)i;
                double intended = (double)counts[i] / blockCount;
                if (!Near(measured.Share(family), intended, 0.0005))
                {
                    failures.Add("the " + what + "'s mix holds " + counts[i] + " of " + blockCount + " blocks built to"
                        + " the " + family + " prototype, and reads " + Fmt(measured.Share(family)) + " of that family"
                        + " rather than " + Fmt(intended) + " -- so that prototype rectangle lands on a different"
                        + " family and this comparison would prove nothing");
                    ok = false;
                }
                if (!Near(measured.Share(family), order.NormalisedFamilyWeight(family), granularity))
                {
                    failures.Add("the " + what + "'s mix reads " + Fmt(measured.Share(family)) + " of the " + family
                        + " family where its own order asks for " + Fmt(order.NormalisedFamilyWeight(family))
                        + ", further out than " + blockCount + " blocks can round, so it is not that order's mix");
                    ok = false;
                }
            }
            if (measured.HasOrientation || measured.HasCircuity)
            {
                failures.Add("the " + what + "'s blocks-only measurement reports "
                    + (measured.HasOrientation ? "an orientation order" : "a circuity")
                    + " it measured nothing for, so Compare weighs a term this case cannot reason about");
                ok = false;
            }
            return ok;
        }
        /// <summary> The floor the average separation between the two orders has to clear.
        ///
        /// The per-seed test is an ORDERING, and an ordering survives a change that collapses the two orders to nearly
        /// the same plan -- 0.301 against 0.300 still passes it. The arithmetic in the case's own note puts the worst
        /// simultaneous corner at 0.178 against 0.241, an average gap of about 0.31 across a seed list, so a floor of
        /// 0.10 sits well clear of the real figure and well above the noise the jitter can produce. </summary>
        private const double MinimumMeanOrderGap = 0.10;

        /// <summary> The seeds the ordering is required to hold on. Fixed rather than drawn, so this case runs the same
        /// arithmetic on every machine and a failure is reproducible by seed instead of by luck. Small primes, a few
        /// powers-of-two neighbours, and some large values, because <c>DrawTarget</c>'s mixer is the thing being leaned
        /// on and a list of consecutive small integers would only exercise one corner of it. </summary>
        private static readonly ulong[] OrderComparisonSeeds =
        {
            1UL, 2UL, 3UL, 5UL, 7UL, 11UL, 13UL, 101UL, 977UL, 4099UL, 65537UL, 1000003UL,
            8675309UL, 20260905UL, 20261231UL, 2147483647UL
        };

        /// <summary> A layout drawn in one order's proportions reads as that order, on every seed in the list.
        /// </summary>
        private static void CheckEachLayoutIsNearerItsOwnOrder(SettlementOrderClass capital,
            SettlementOrderClass village, List<string> failures)
        {
            const int BlockCount = 20;
            int[] capitalCounts = MixCountsFor(capital, BlockCount);
            int[] villageCounts = MixCountsFor(village, BlockCount);
            SettlementFingerprint asCapital = SettlementFingerprint.Measure(MixFor(capitalCounts), null, null);
            SettlementFingerprint asVillage = SettlementFingerprint.Measure(MixFor(villageCounts), null, null);

            bool capitalReads = CheckMixReadsAsBuilt(asCapital, capitalCounts, capital, "capital", BlockCount, failures);
            bool villageReads = CheckMixReadsAsBuilt(asVillage, villageCounts, village, "village", BlockCount, failures);
            if (!capitalReads || !villageReads) { return; }

            double capitalGapTotal = 0.0, villageGapTotal = 0.0;
            for (int s = 0; s < OrderComparisonSeeds.Length; s++)
            {
                ulong seed = OrderComparisonSeeds[s];
                SettlementFingerprint capitalTarget = SettlementFingerprint.DrawTarget(capital, seed);
                SettlementFingerprint villageTarget = SettlementFingerprint.DrawTarget(village, seed);
                string ownWhy, otherWhy;
                float capitalToOwn = SettlementFingerprint.Compare(asCapital, capitalTarget, out ownWhy);
                float capitalToOther = SettlementFingerprint.Compare(asCapital, villageTarget, out otherWhy);
                Expect(capitalToOwn < capitalToOther,
                    "on seed " + seed + " a layout in the capital's own proportions sits " + Fmt(capitalToOwn)
                    + " from the capital's target and " + Fmt(capitalToOther) + " from the village's, so a capital's"
                    + " plan is read as a village's -- " + ownWhy, failures);
                capitalGapTotal += capitalToOther - capitalToOwn;

                float villageToOwn = SettlementFingerprint.Compare(asVillage, villageTarget, out ownWhy);
                float villageToOther = SettlementFingerprint.Compare(asVillage, capitalTarget, out otherWhy);
                Expect(villageToOwn < villageToOther,
                    "on seed " + seed + " a layout in the village's own proportions sits " + Fmt(villageToOwn)
                    + " from the village's target and " + Fmt(villageToOther) + " from the capital's, so a village would"
                    + " be handed a capital's plan -- " + ownWhy, failures);
                villageGapTotal += villageToOther - villageToOwn;
            }

            double capitalMeanGap = capitalGapTotal / OrderComparisonSeeds.Length;
            double villageMeanGap = villageGapTotal / OrderComparisonSeeds.Length;
            Expect(capitalMeanGap >= MinimumMeanOrderGap,
                "a capital's layout averages only " + Fmt(capitalMeanGap) + " nearer its own order than the village's"
                + " across " + OrderComparisonSeeds.Length + " seeds, under the " + Fmt(MinimumMeanOrderGap)
                + " this gate requires, so the two orders are drawing nearly the same city", failures);
            Expect(villageMeanGap >= MinimumMeanOrderGap,
                "a village's layout averages only " + Fmt(villageMeanGap) + " nearer its own order than the capital's"
                + " across " + OrderComparisonSeeds.Length + " seeds, under the " + Fmt(MinimumMeanOrderGap)
                + " this gate requires, so the two orders are drawing nearly the same city", failures);
        }
        #endregion
        #region "Ground under a hand-placed object, and the two ways a thing meets a slope"
        /// <summary> The drift under one 8 m square footprint standing at a given base elevation.
        ///
        /// Eight metres is a shed, which is the smallest thing an author places that still has corners far enough apart
        /// to rock. The ring is built here rather than passed in because all three readings the case wants are the same
        /// footprint over three different grounds, and a case that also had to lay out its own rings would bury that.
        ///
        /// The record is laid out and then asserted to HAVE stations before the ground is read: a ring the scan could not
        /// station returns <c>NeverRecorded</c>, which is not one of the three verdicts under test and would otherwise
        /// read as a plain verdict mismatch instead of as a scan that measured nothing. </summary>
        private static GroundDrift DriftOverGround(ArrayHeightfield field, float centreX, float centreZ,
            float baseElevationMetres, List<string> failures)
        {
            const float HalfSideMetres = 4.0f;
            List<Vector3> ring = new List<Vector3>();
            ring.Add(new Vector3(centreX - HalfSideMetres, baseElevationMetres, centreZ - HalfSideMetres));
            ring.Add(new Vector3(centreX + HalfSideMetres, baseElevationMetres, centreZ - HalfSideMetres));
            ring.Add(new Vector3(centreX + HalfSideMetres, baseElevationMetres, centreZ + HalfSideMetres));
            ring.Add(new Vector3(centreX - HalfSideMetres, baseElevationMetres, centreZ + HalfSideMetres));

            FootprintScanClass cls = FootprintScanClass.Default();
            FootprintConformRecord record = FootprintConformRecord.Record("RCMCITYGRAPH footprint",
                "RCMCITYGRAPH/probe", ring, baseElevationMetres, cls);
            if (!record.HasStations)
            {
                failures.Add("an " + Fmt(HalfSideMetres * 2.0f) + " m square footprint was laid out with no stations on"
                    + " it, so nothing could be read about the ground under a hand-placed object");
                return default(GroundDrift);
            }
            return record.Evaluate(field, cls.drift);
        }
        /// <summary> The two ways one object can meet sloping ground, read off the SAME slope so the only thing that can
        /// account for a difference is the seating class.
        ///
        /// A tree grows up and a pole is driven in: both stand plumb on a hillside, and a tree leaning with the slope is
        /// the plainest sign of a scattered world. A bench rests on whatever is under it, so it takes the slope. Both are
        /// seated against the same 10 degree face here, and the tilts have to differ by the whole of it.
        ///
        /// Both also ask to be sunk 0.25 m, which is deeper than seating is allowed to bury anything, so both have to come
        /// back at the 0.15 m cap: an asked-for depth is a request, not an instruction, and a quarter of a metre of a
        /// bench's legs underground would read as a bench sinking rather than as a bench. </summary>
        private static void CheckSeating(GroundSeat tree, GroundSeat bench, List<string> failures)
        {
            const float ExpectedEmbedMetres = 0.150f;
            const float ExpectedBenchTiltDegrees = 10.0f;
            const float ExpectedSurfaceMetres = 40.0f;

            Expect(tree.seated && bench.seated,
                "seating against a hand-written height and normal reported nothing seated, so the decision cannot be"
                + " exercised without a terrain under it: tree " + tree.seated + ", bench " + bench.seated, failures);
            Expect(Near(tree.surfaceHeightMetres, ExpectedSurfaceMetres, 0.0001)
                && Near(bench.surfaceHeightMetres, ExpectedSurfaceMetres, 0.0001),
                "the surface a seat was measured from came back as " + Fmt(tree.surfaceHeightMetres) + " m and "
                + Fmt(bench.surfaceHeightMetres) + " m rather than the " + Fmt(ExpectedSurfaceMetres)
                + " m it was handed", failures);
            Expect(Near(tree.embedMetres, ExpectedEmbedMetres, 0.0001)
                && Near(bench.embedMetres, ExpectedEmbedMetres, 0.0001),
                "a request to sink both 0.25 m was honoured at " + Fmt(tree.embedMetres) + " m and "
                + Fmt(bench.embedMetres) + " m rather than clamped to the " + Fmt(ExpectedEmbedMetres)
                + " m seating is allowed to bury", failures);
            Expect(Near(tree.position.y, tree.surfaceHeightMetres - tree.embedMetres, 0.0001)
                && Near(bench.position.y, bench.surfaceHeightMetres - bench.embedMetres, 0.0001),
                "a seat was not put its own embed below its own surface, so the figures it reports cannot be checked"
                + " against where the object actually stands: tree " + Fmt(tree.position.y) + " m, bench "
                + Fmt(bench.position.y) + " m", failures);
            Expect(Near(tree.tiltDegrees, 0.0, 0.001),
                "a tree was leaned " + Fmt(tree.tiltDegrees) + " degrees over with the hillside instead of grown plumb",
                failures);
            Expect(Near(bench.tiltDegrees, ExpectedBenchTiltDegrees, 0.05),
                "a bench on a " + Fmt(ExpectedBenchTiltDegrees) + " degree face came out at " + Fmt(bench.tiltDegrees)
                + " degrees, so it neither lies on the ground nor stands up off it", failures);
            Expect(HeadingIsUnchanged(tree) && HeadingIsUnchanged(bench),
                "seating turned an object on the spot, so the ground overruled the operator on which way it faces",
                failures);
        }
        /// <summary> Whether a seat left the heading alone. Only the horizontal part is compared, because a bench laid on
        /// a slope has a forward that dips -- what the operator chose was which way it faces on the map, and that is the
        /// component the ground is not allowed to touch. A seat whose forward ends up straight up or straight down has no
        /// heading left to compare, and reads as changed. </summary>
        private static bool HeadingIsUnchanged(GroundSeat seat)
        {
            Vector3 forward = seat.rotation * Vector3.forward;
            Vector3 flat = new Vector3(forward.x, 0.0f, forward.z);
            if (flat.sqrMagnitude < 0.000001f) { return false; }
            return Vector3.Angle(flat.normalized, Vector3.forward) < 0.05f;
        }
        #endregion
    }
}
#endif

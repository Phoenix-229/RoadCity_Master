#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadCityMaster.City.Editor
{
    /// <summary> B-15 / C-9. The city's inspector.
    ///
    /// Two halves, as the register fixes them. The FIXED half is the four scene-side answers a city cannot get from
    /// anywhere else -- where it stands, what content it may draw from, what to press, and what came out. The
    /// SELECTED-NODE half is the parameter sheet of whichever node the graph window has selected, which is what keeps
    /// this inspector's length bounded by the largest single node instead of by the size of the graph: a hundred-node
    /// city and a three-node city draw the same number of parameter rows.
    ///
    /// Nothing is laid out by hand. Every editable figure goes through <see cref="LegacyInspectorShell"/>, so this
    /// inspector inherits the product header, the build line, the four RM-51 groups, the unit column, the inline
    /// out-of-range message, focus-out commit and the per-field reset icon rather than re-solving any of them. Where the
    /// registry does not own a field yet, the shell says so by returning false and the field is drawn through the same
    /// commit buffer, so no control on this inspector commits per keystroke.
    ///
    /// The five sections are the order an author works in -- where, what, from what, build, what came out -- and they
    /// stay five. The third pass added the settlement order and archetype to *Where* and the scan roots to *From what*
    /// rather than opening a sixth section, because an author looking for "where is it" should not have to know that a
    /// settlement's order is a different kind of answer from its position. </summary>
    [CustomEditor(typeof(CityAuthoring))]
    public sealed class CityAuthoringEditor : UnityEditor.Editor
    {
        #region "What this inspector owns"
        /// <summary> The registry key for this component's fields. The registry is keyed by the component's type name
        /// as a string so that a renamed field is a compile error in one file and a registry row is not a second place
        /// the type name is spelled. </summary>
        private const string ComponentName = "CityAuthoring";

        /// <summary> B-19's commit buffer. Every control on this inspector goes through it, which is what makes a
        /// figure commit on Enter and on focus leaving rather than on each keystroke. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();

        /// <summary> The prefab verdicts, kept between repaints. Validating a library means loading every prefab in it
        /// and walking its renderers; doing that inside a repaint would make the inspector cost of a city proportional
        /// to the size of its content folders. The cache is filled by the button in *From what* and re-filled per asset
        /// only when that asset's file has changed since it was judged. </summary>
        private readonly ContentValidationCache contentCache = new ContentValidationCache();

        /// <summary> The graph asset, wrapped for editing. A node's parameters live inside the GRAPH, not inside the
        /// city, so the selected-node half edits a second serialised object and applies it separately. Rebuilt when the
        /// author points the city at a different graph. </summary>
        private SerializedObject graphObject;

        /// <summary> What the last bake said, kept so the sentence survives the repaints after the bake finished. The
        /// report is taken from the ticket exactly once -- <c>TryTakeReport</c> hands it over and forgets it -- so it has
        /// to be held here or it would be read by whichever repaint happened to be first and then lost. </summary>
        private string lastBakeReport = string.Empty;
        private bool lastBakeOk;

        /// <summary> The refusal from the last press of Bake that never became a bake, kept for the same reason. </summary>
        private string lastRefusal = string.Empty;

        /// <summary> How many assets the last content check judged, and how long ago in author terms. Shown so a report
        /// of "0 failed" cannot be mistaken for "nothing was checked". </summary>
        private int contentCheckedCount = -1;

        /// <summary> C-18. What the last hand-placed sweep said, kept between repaints for the same reason the bake
        /// report is: the sweep runs on a button press and the sentence has to outlive that one frame. The per-object
        /// lines are held separately from the summary so the summary can be shown on its own when there are many. </summary>
        private string lastScanSummary = string.Empty;
        private readonly List<string> lastScanFindings = new List<string>();
        #endregion


        #region "Lifecycle"
        private void OnEnable()
        {
            // The graph window changes the selection; this inspector draws it. Without this the author would click a
            // node and see the previous node's parameters until something else happened to cause a repaint.
            CityGraphSelection.Changed += Repaint;
            SceneView.duringSceneGui += OnScene;
        }

        private void OnDisable()
        {
            CityGraphSelection.Changed -= Repaint;
            SceneView.duringSceneGui -= OnScene;
        }
        #endregion

        #region "The five sections"
        public override void OnInspectorGUI()
        {
            fields.BeginFrame();
            serializedObject.Update();

            CityAuthoring city = target as CityAuthoring;
            if (city == null) { return; }

            SyncGraphObject(city);
            if (graphObject != null) { graphObject.Update(); }
            TakeAnyFinishedReport(city);

            LegacyInspectorShell.Header("City");

            DrawWhere(city);
            DrawWhat(city);
            DrawFromWhat(city);
            DrawBuild(city);
            DrawWhatCameOut(city);

            serializedObject.ApplyModifiedProperties();
            if (graphObject != null) { graphObject.ApplyModifiedProperties(); }
        }

        /// <summary> Keep the graph's serialised view pointed at the graph the city names. Comparing the wrapped target
        /// rather than rebuilding every frame matters: a fresh SerializedObject each repaint would drop the expanded
        /// state of every parameter row and would fight the author's own edit mid-commit. </summary>
        private void SyncGraphObject(CityAuthoring city)
        {
            CityGraph graph = city.graph;
            if (graph == null) { graphObject = null; return; }
            if (graphObject != null && graphObject.targetObject == graph) { return; }
            graphObject = new SerializedObject(graph);
        }

        /// <summary> Take the finished bake's report if this city's bake is the one that finished. The ticket hands a
        /// report over once and forgets it, and it names its owner, so a city never shows the report of the city baking
        /// beside it. </summary>
        private void TakeAnyFinishedReport(CityAuthoring city)
        {
            CityBakeReport report;
            string owner;
            if (!CityBakeTicket.TryTakeReport(out report, out owner)) { return; }
            if (report == null) { return; }
            if (!string.Equals(owner, OwnerKey(city), StringComparison.Ordinal)) { return; }

            lastBakeOk = report.ok;
            lastBakeReport = report.report;
            lastRefusal = string.Empty;
        }
        #endregion

        #region "Where"
        /// <summary> Section 1. Where the settlement stands, and what kind of settlement the ground says it is.
        ///
        /// The order and the archetype are read-outs of a measurement, not settings, with one exception: the archetype
        /// OVERRIDE is a setting, and it is drawn beside the measured value so that disagreeing with the ground looks
        /// like disagreeing with the ground. </summary>
        private void DrawWhere(CityAuthoring city)
        {
            LegacyInspectorShell.SectionHeader("Where",
                "The ground this settlement stands on: the graph it runs, the seed it runs with, the rectangle it"
                + " occupies, the road end it grew from, and the character the ground was measured to have.");

            Row("graph", "Graph", "Which city graph asset this settlement runs. One graph may be shared by many"
                + " settlements: the graph is the recipe, the seed and the rectangle are what make each one different.");
            Row("seed", "Seed", "The one generator input. The same seed, graph and rectangle produce the same city on"
                + " every machine and every re-bake.");
            Row("regionCentre", "Region centre",
                "The Transform the rectangle is measured from. Drag that Transform and the settlement moves with it.");
            Row("regionXZ", "Region (local X/Z)",
                "The rectangle the settlement occupies, in metres, relative to the region centre. Drag its corners in"
                + " the Scene view rather than typing them, unless a figure has to be exact.");
            Row("sourceRoad", "Source road", "The road whose free end this settlement grew from. It is what gives the"
                + " settlement its order -- a capital, a city, a town or a village -- because the role is declared on"
                + " the road, not here.");
            Row("sourceEnd", "Source end", "Which end of that road. A road has two, and a settlement stands at one.");
            Row("archetypeOverride", "Archetype override",
                "Auto means measure it from the ground. Any other value is a disagreement with the measurement, and the"
                + " measured value is shown below so the disagreement is visible.");

            DrawWhereMeasured(city);
        }

        /// <summary> The figures under *Where*, all of them measured and none of them editable, drawn in the one group
        /// RM-64 allows a computed value to appear in.
        ///
        /// The siting verdict is READ, never recomputed here. C-4's pass needs a suitability raster, a ground probe and
        /// an order catalogue; running it inside a repaint would put a full site test on the author's mouse movement, and
        /// a figure recomputed on a repaint is a figure that can disagree with the bake that produced the city. What the
        /// bake measured is on the graph's own nodes, so the verdict shown is the verdict that ran. </summary>
        private void DrawWhereMeasured(CityAuthoring city)
        {
            bool open = LegacyInspectorShell.BeginMeasured(ComponentName);
            if (open)
            {
                Rect world;
                string regionWhy;
                if (city.TryRegionWorldXZ(out world, out regionWhy))
                {
                    LegacyInspectorShell.Measured("Region in world",
                        SettlementSiting.PlaceXZ(world.xMin, world.yMin) + " to "
                        + SettlementSiting.PlaceXZ(world.xMax, world.yMax), "");
                    LegacyInspectorShell.Measured("Region size",
                        Figure(world.width) + " × " + Figure(world.height), "m");
                    LegacyInspectorShell.Measured("Region area",
                        Figure(world.width * world.height), "m²");
                }
                else
                {
                    EditorGUILayout.HelpBox(regionWhy, MessageType.Warning);
                }

                SettlementRoleEnum role;
                string roleWhy;
                if (city.TryGetSourceRole(out role, out roleWhy))
                {
                    LegacyInspectorShell.Measured("Order, from the road's role",
                        ObjectNames.NicifyVariableName(role.ToString()), "");
                }
                else
                {
                    LegacyInspectorShell.Measured("Order, from the road's role", "not readable", "");
                    EditorGUILayout.HelpBox(roleWhy, MessageType.Info);
                }

                LegacyInspectorShell.Measured("Archetype in force",
                    ObjectNames.NicifyVariableName(city.archetypeOverride.ToString()), "");
                DrawSitingVerdict(city);
                DrawGroundLine();
            }
            LegacyInspectorShell.EndMeasured(open);
        }

        /// <summary> C-4's verdict, as the bake recorded it. The site test and the site rank write their figures onto
        /// their own nodes, and -1 in both is the sentinel for "this node has never run", so a city that has never been
        /// baked says exactly that instead of showing a zero that reads like a measurement. </summary>
        private void DrawSitingVerdict(CityAuthoring city)
        {
            if (city.graph == null || city.graph.nodes == null)
            {
                LegacyInspectorShell.Measured("Siting verdict", "no graph, so nothing has measured this site", "");
                return;
            }

            int reported = 0;
            for (int i = 0; i < city.graph.nodes.Count; i++)
            {
                CityGraphNode node = city.graph.nodes[i];
                if (node == null) { continue; }
                if (node.kind != CityNodeKind.SiteTest && node.kind != CityNodeKind.SiteRank) { continue; }

                reported++;
                string label = node.DisplayName();
                if (node.lastRunMilliseconds < 0.0 && node.lastRunItemCount < 0)
                {
                    LegacyInspectorShell.Measured(label, "not run yet", "");
                    continue;
                }

                LegacyInspectorShell.Measured(label, node.lastRunItemCount.ToString(CultureInfo.InvariantCulture)
                    + (node.lastRunItemCount == 1 ? " site" : " sites") + ", "
                    + node.lastRunMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms", "");
                if (!string.IsNullOrEmpty(node.lastRunNote)) { EditorGUILayout.HelpBox(node.lastRunNote, MessageType.None); }
            }

            if (reported == 0)
            {
                LegacyInspectorShell.Measured("Siting verdict", "this graph holds no site test", "");
                EditorGUILayout.HelpBox("Nothing in this graph tests the site, so the rectangle above is taken as given."
                    + " Add a Site test node to have the ground judged, and a Site rank node to have several candidate"
                    + " rectangles ordered against each other.", MessageType.Info);
            }
        }

        /// <summary> Which ground the bake will write to, named rather than assumed. A city whose terrain provider is not
        /// connected still bakes -- it just conforms nothing -- and the difference between "conformed" and "wrote to a
        /// standalone terrain" is the kind of thing an author should not have to find out from the result. </summary>
        private void DrawGroundLine()
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            if (gateway == null)
            {
                LegacyInspectorShell.Measured("Ground", "no write path installed", "");
                EditorGUILayout.HelpBox("No terrain write path is installed, so nothing this bake produces will change"
                    + " the ground. A Terrain Linker component in the scene installs one when it acquires a provider,"
                    + " and the standalone path is installed for a scene that has its own Unity terrain.",
                    MessageType.Info);
                return;
            }

            LegacyInspectorShell.Measured("Ground", gateway.PathLine(), "");
            LegacyInspectorShell.Measured("Provider",
                string.IsNullOrEmpty(gateway.ProviderName) ? "none named" : gateway.ProviderName, "");
        }
        #endregion

        #region "What -- the selected node's parameters"
        /// <summary> Section 2. The parameters of the one node the graph window has selected.
        ///
        /// This is the whole reason the inspector does not grow with the city. A graph of a hundred nodes has a hundred
        /// parameter sheets, and an inspector that drew them all would be a mile long and would make the author scroll
        /// past ninety-nine of them to reach the one they clicked. The graph window owns "which node", this owns "what
        /// the node is set to", and the two meet at <see cref="CityGraphSelection"/> and nowhere else. </summary>
        private void DrawWhat(CityAuthoring city)
        {
            LegacyInspectorShell.SectionHeader("What",
                "The settings of the node selected in the city graph window. Select a different node there and this"
                + " section follows it.");

            if (city.graph == null)
            {
                EditorGUILayout.HelpBox("This settlement names no graph, so there is nothing to set. Assign one above,"
                    + " or create one through Assets > Create > Road City Master > City Graph.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Open the city graph", LegacyInspectorShell.CommandButtonStyle()))
            {
                CityGraphWindow.Open(city.graph);
            }

            if (CityGraphSelection.Graph != city.graph)
            {
                EditorGUILayout.HelpBox("No node of this city's graph is selected. Open the graph and click a node --"
                    + " its settings appear here.", MessageType.Info);
                return;
            }

            CityGraphNode node = CityGraphSelection.Node;
            if (node == null)
            {
                EditorGUILayout.HelpBox("The selected node is no longer in the graph. Click another one.",
                    MessageType.Info);
                return;
            }

            DrawSelectedNode(city, node);
        }

        /// <summary> The selected node's identity, then its parameters in the order the catalogue declares them.
        ///
        /// The parameters are edited on the GRAPH asset, through the graph's own serialised view, because that is where
        /// they live. Editing them through the city would be editing a copy. </summary>
        private void DrawSelectedNode(CityAuthoring city, CityGraphNode node)
        {
            CityNodeDescriptor descriptor;
            string why;
            bool known = CityNodeCatalog.TryGetForNode(node, out descriptor, out why);

            EditorGUILayout.LabelField(node.DisplayName(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(node.stage.ToString()) + "  ·  "
                + ObjectNames.NicifyVariableName(node.kind.ToString()), EditorStyles.miniLabel);

            if (!known)
            {
                EditorGUILayout.HelpBox(why, MessageType.Warning);
                return;
            }

            if (descriptor.IsBlocked)
            {
                EditorGUILayout.HelpBox("This kind is blocked: " + descriptor.blockedReason, MessageType.Warning);
            }

            if (descriptor.NeedsOperatorAsset && node.operatorAsset == null)
            {
                EditorGUILayout.HelpBox("This node needs its " + descriptor.owner + " asset and has none, so the bake"
                    + " will step around it. Delete the node and add it again to have one attached.",
                    MessageType.Warning);
            }

            if (graphObject == null) { return; }

            int index = city.graph.IndexOfNode(node.id);
            if (index < 0) { return; }

            SerializedProperty nodes = graphObject.FindProperty("nodes");
            if (nodes == null || index >= nodes.arraySize) { return; }

            SerializedProperty parameters = nodes.GetArrayElementAtIndex(index).FindPropertyRelative("parameters");
            if (parameters == null || parameters.arraySize == 0)
            {
                EditorGUILayout.HelpBox("This node has nothing to set. It takes what it needs from its inputs.",
                    MessageType.None);
                return;
            }

            for (int p = 0; p < parameters.arraySize; p++)
            {
                DrawParameter(node, descriptor, parameters.GetArrayElementAtIndex(p));
            }
        }

        /// <summary> One parameter row, drawn the way every other measured field in the product is drawn: the value, the
        /// unit column beside it, and the out-of-range sentence beneath it rather than squeezed into the row.
        ///
        /// It cannot go through <see cref="InspectorFieldDrawer.Draw"/>, and the reason is structural rather than a
        /// preference: that method resolves its property with <c>so.FindProperty(d.field)</c>, which reaches a top-level
        /// field of the inspected object. A node parameter is four levels down inside another asset. So the property is
        /// resolved here and handed to the same two public pieces of that drawer's presentation -- the commit buffer's
        /// <c>Property</c> and the unit column -- which keeps the column width and the commit rule in one place each.
        ///
        /// The label, the unit and the limits come from the registry (C-10) when it owns the parameter, and the
        /// catalogue's own sentence is the fallback tooltip. Nothing about a unit or a band is written here: a parameter
        /// that wants a unit is a registry row that has not been added yet, and that is a one-line fix in one file rather
        /// than a hunt through the inspectors. </summary>
        private void DrawParameter(CityGraphNode node, CityNodeDescriptor descriptor, SerializedProperty parameter)
        {
            if (parameter == null) { return; }

            SerializedProperty nameProp = parameter.FindPropertyRelative("name");
            SerializedProperty kindProp = parameter.FindPropertyRelative("kind");
            if (nameProp == null || kindProp == null) { return; }

            string parameterName = nameProp.stringValue;
            SerializedProperty valueProp = ValuePropertyOf(parameter, kindProp.intValue);
            if (valueProp == null) { return; }

            FieldDescriptor registered = LegacyInspectorShell.Descriptor(node.kind.ToString(), parameterName);
            string label = registered != null ? registered.label : ObjectNames.NicifyVariableName(parameterName);
            string tooltip = registered != null ? registered.tooltip : CatalogueSummary(descriptor, parameterName);
            string unit = registered != null ? registered.UnitLabel() : string.Empty;

            EditorGUILayout.BeginHorizontal();
            fields.Property(valueProp, new GUIContent(label, tooltip));
            InspectorFieldDrawer.Unit(unit, tooltip);
            EditorGUILayout.EndHorizontal();

            if (registered == null || !registered.range.IsBounded) { return; }

            double numeric;
            if (!TryNumeric(valueProp, out numeric)) { return; }

            ValidationResult verdict = registered.Validate(numeric);
            if (!verdict.ok) { EditorGUILayout.HelpBox(verdict.message, MessageType.Warning); }
        }

        /// <summary> Which of the parameter struct's five value fields this kind actually uses. The struct carries all
        /// five so that changing a parameter's kind does not lose the author's other entries, but exactly one is live at
        /// a time and drawing the wrong one would show a stale 0. </summary>
        private static SerializedProperty ValuePropertyOf(SerializedProperty parameter, int kind)
        {
            switch ((CityGraphParameterKind)kind)
            {
                case CityGraphParameterKind.Number: return parameter.FindPropertyRelative("number");
                case CityGraphParameterKind.Text: return parameter.FindPropertyRelative("text");
                case CityGraphParameterKind.Flag: return parameter.FindPropertyRelative("flag");
                case CityGraphParameterKind.Vector: return parameter.FindPropertyRelative("vector");
                case CityGraphParameterKind.Asset: return parameter.FindPropertyRelative("asset");
                default: return null;
            }
        }

        /// <summary> The catalogue's own sentence for a parameter, used as the tooltip until the registry owns the row.
        /// The catalogue holds a summary per parameter but no unit and no range, which is why it is a fallback rather
        /// than the source: an author who hovers learns what the number means, and the moment the registry gains the
        /// row the same hover also gains the limit. </summary>
        private static string CatalogueSummary(CityNodeDescriptor descriptor, string parameterName)
        {
            if (descriptor == null || descriptor.parameters == null) { return string.Empty; }
            for (int i = 0; i < descriptor.parameters.Count; i++)
            {
                if (descriptor.parameters[i].Name != parameterName) { continue; }
                return descriptor.parameters[i].summary ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary> A parameter's value as a number, when it is one. A range on a text or asset parameter is a registry
        /// mistake rather than something to report at the author, so a non-numeric property simply declines. </summary>
        private static bool TryNumeric(SerializedProperty value, out double numeric)
        {
            numeric = 0.0;
            if (value == null) { return false; }
            if (value.propertyType == SerializedPropertyType.Float) { numeric = value.doubleValue; return true; }
            if (value.propertyType == SerializedPropertyType.Integer) { numeric = value.longValue; return true; }
            return false;
        }
        #endregion

        #region "From what -- the content the bake may choose from"
        /// <summary> Section 3. The libraries the bake picks buildings and props out of, and the scene objects it is
        /// allowed to read as already-standing content.
        ///
        /// The prefab report is inline for one reason, and it is the reason B-15 asks for it: a prefab that the contract
        /// will refuse is refused DURING a bake today, which means the author learns about a wrong pivot from a building
        /// that is half-buried in a finished city. Here the same verdict is available before the bake, per asset, with
        /// RM-48's own reason.
        ///
        /// It is never computed on a repaint. Validation loads every GameObject in every category, and an inspector whose
        /// cost grows with the size of the author's content folder is an inspector that punishes having content. The
        /// author asks for the check; the cache remembers the answer against each asset's own file stamp, so asking again
        /// after changing one prefab re-checks one prefab. </summary>
        private void DrawFromWhat(CityAuthoring city)
        {
            LegacyInspectorShell.SectionHeader("From what",
                "The content this settlement may build from: the prefab libraries it chooses out of, and the scene"
                + " objects it is allowed to treat as already standing.");

            Row("prefabLibraries", "Prefab libraries",
                "The category sets the bake chooses assets from. Leaving this empty is legal -- the bake then produces"
                + " the network and the blocks and places nothing on them.");
            Row("scanRoots", "Scan roots",
                "Scene objects the bake reads rather than replaces: content that already stands on this ground. Leaving"
                + " it empty means nothing is scanned, which is deliberate -- a sweep that fell back to the whole"
                + " settlement would ask for ground to be levelled under objects nobody named.");

            DrawScanRootFigures(city);
            DrawContentReport(city);
        }

        /// <summary> What the scan roots resolve to, which is not always what the list says: empty slots and repeats are
        /// dropped, so a list with a hole in it resolves to fewer roots than it has rows. </summary>
        private void DrawScanRootFigures(CityAuthoring city)
        {
            List<Transform> roots = city.ResolvedScanRoots();
            int listed = city.scanRoots != null ? city.scanRoots.Count : 0;
            int resolved = roots != null ? roots.Count : 0;

            EditorGUILayout.LabelField(resolved == 0
                ? "Scanning nothing."
                : (resolved == 1 ? "Scanning 1 root: " : "Scanning " + resolved.ToString(CultureInfo.InvariantCulture)
                    + " roots: ") + NamesOf(roots), EditorStyles.miniLabel);

            if (listed > resolved)
            {
                EditorGUILayout.HelpBox((listed - resolved).ToString(CultureInfo.InvariantCulture)
                    + " of the rows above is empty or points at an object that is gone, and is being skipped.",
                    MessageType.Warning);
            }
        }

        /// <summary> The libraries and their per-category standing, plus the one button that fills it in. </summary>
        private void DrawContentReport(CityAuthoring city)
        {
            List<CategoryContentSet> libraries = city.ResolvedLibraries();
            if (libraries == null || libraries.Count == 0)
            {
                EditorGUILayout.HelpBox("No library is named, so this settlement will build its network and its blocks"
                    + " and place nothing on them. That is a legal city -- streets and empty plots -- and it is what you"
                    + " get until a category set is assigned above.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Check this content now", LegacyInspectorShell.CommandButtonStyle()))
            {
                CheckContent(libraries);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(contentCheckedCount < 0
                ? "not checked yet"
                : contentCheckedCount.ToString(CultureInfo.InvariantCulture) + " asset(s) checked, "
                    + contentCache.RecordedCount.ToString(CultureInfo.InvariantCulture) + " remembered",
                EditorStyles.miniLabel, GUILayout.Width(240f));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < libraries.Count; i++)
            {
                CategoryContentSet library = libraries[i];
                if (library == null) { continue; }

                EditorGUILayout.LabelField(library.name + " -- "
                    + library.Count.ToString(CultureInfo.InvariantCulture) + " category(ies)", EditorStyles.boldLabel);

                IList<CategoryFoldoutView> views = library.BuildAllViews(contentCache);
                if (views == null || views.Count == 0)
                {
                    EditorGUILayout.LabelField("    this set names no category", EditorStyles.miniLabel);
                    continue;
                }

                for (int v = 0; v < views.Count; v++) { DrawCategoryRow(views[v]); }
            }
        }

        /// <summary> One category's line, and every refusal under it spelled out. A failed asset is shown with RM-48's
        /// own reason rather than a count, because a count tells the author that something is wrong and the reason tells
        /// them which prefab to open. </summary>
        private void DrawCategoryRow(CategoryFoldoutView view)
        {
            EditorGUILayout.LabelField("    " + view.Headline(), EditorStyles.miniLabel);

            if (view.assets == null) { return; }
            for (int a = 0; a < view.assets.Length; a++)
            {
                if (view.assets[a].state != ContentValidationState.Failed) { continue; }
                EditorGUILayout.HelpBox(view.assets[a].Describe(), MessageType.Warning);
            }

            if (view.failedCount > 0)
            {
                EditorGUILayout.HelpBox(view.failedCount.ToString(CultureInfo.InvariantCulture)
                    + " asset(s) in " + view.category + " will be passed over by the bake. The city still builds; those"
                    + " plots take another asset from the same category, or stay empty if none is left.",
                    MessageType.Warning);
            }
        }

        /// <summary> Validate every asset in every named category against the prefab contract, and remember each verdict
        /// against that asset's own file stamp so the next press re-checks only what changed.
        ///
        /// The category rule is <see cref="PrefabCategoryRule.Default"/> for the category's own name: the per-category
        /// LOD requirement and collider policy live there, and restating them here would be a second opinion about the
        /// same rule. </summary>
        private void CheckContent(List<CategoryContentSet> libraries)
        {
            PrefabContract contract = PrefabContract.Default();
            int checkedCount = 0;

            for (int i = 0; i < libraries.Count; i++)
            {
                CategoryContentSet library = libraries[i];
                if (library == null) { continue; }

                IList<string> categories = library.Categories();
                if (categories == null) { continue; }

                for (int c = 0; c < categories.Count; c++)
                {
                    CategoryContentEntry entry;
                    if (!library.TryGetEntry(categories[c], out entry)) { continue; }
                    checkedCount += CheckOneCategory(entry, contract);
                }
            }

            contentCheckedCount = checkedCount;
        }

        /// <summary> One category's assets, skipping the ones whose remembered verdict is still current. Returns how many
        /// were actually loaded and judged, which is the figure the author sees beside the button. </summary>
        private int CheckOneCategory(CategoryContentEntry entry, PrefabContract contract)
        {
            string[] paths = entry.ResolvedAssetPaths();
            if (paths == null || paths.Length == 0) { return 0; }

            PrefabCategoryRule rule = PrefabCategoryRule.Default(entry.category);
            int judged = 0;

            for (int p = 0; p < paths.Length; p++)
            {
                string path = paths[p];
                if (string.IsNullOrEmpty(path)) { continue; }

                long stamp = StampOf(path);
                if (!contentCache.NeedsRecompute(path, stamp)) { continue; }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                PrefabValidationReport report = PrefabContractValidator.Validate(asset, path, contract, rule);
                contentCache.Record(path, report.Passed, FirstReason(report), stamp);
                judged++;
            }

            return judged;
        }

        /// <summary> The one refusal an author reads first. A report holds every violation on purpose -- one pass finds
        /// them all rather than making the author fix and re-run five times -- but the cache holds one sentence, so the
        /// count comes with it and the rest are found by opening the asset. </summary>
        private static string FirstReason(PrefabValidationReport report)
        {
            if (report == null || report.violations == null || report.violations.Count == 0) { return string.Empty; }
            string first = report.violations[0].message;
            if (report.violations.Count == 1) { return first; }
            return first + " (and " + (report.violations.Count - 1).ToString(CultureInfo.InvariantCulture) + " more)";
        }

        /// <summary> An asset's file stamp, which is what makes the remembered verdict safe to trust. The editor's working
        /// directory is the project folder, so a project-relative asset path is a usable file path; an asset that cannot
        /// be stamped reads 0, which never matches a recorded stamp and so is simply re-checked. </summary>
        private static long StampOf(string assetPath)
        {
            try
            {
                return File.Exists(assetPath) ? File.GetLastWriteTimeUtc(assetPath).Ticks : 0L;
            }
            catch (IOException)
            {
                return 0L;
            }
        }

        /// <summary> The scan roots named, short enough to sit on one line. Three names and a count reads better than
        /// twelve names wrapped over four lines, and the list itself is directly above for the full answer. </summary>
        private static string NamesOf(List<Transform> roots)
        {
            if (roots == null || roots.Count == 0) { return "nothing"; }

            string joined = string.Empty;
            int shown = roots.Count < 3 ? roots.Count : 3;
            for (int i = 0; i < shown; i++)
            {
                if (roots[i] == null) { continue; }
                if (joined.Length > 0) { joined += ", "; }
                joined += roots[i].name;
            }
            if (roots.Count > shown)
            {
                joined += " and " + (roots.Count - shown).ToString(CultureInfo.InvariantCulture) + " more";
            }
            return joined;
        }
        #endregion

        #region "Build -- one Bake, one Clear, and every reason a stage will not run"
        /// <summary> Section 4. The two commands, and the truth about whether they will do anything.
        ///
        /// A Bake button that is always enabled and then reports a refusal is the shape this replaces: the reasons a bake
        /// cannot run are knowable before it is pressed, so they are shown before it is pressed and the button is
        /// disabled while any of them stands.
        ///
        /// The bake goes through <see cref="CityBakeTicket"/> and never through <see cref="CityBakeRunner.Run"/>. The
        /// runner bakes in one call, which holds the editor still for the length of a city and offers no cancel; the
        /// ticket walks one stage per frame on a Background Tasks entry with a progress bar and a Cancel that works.
        /// </summary>
        private void DrawBuild(CityAuthoring city)
        {
            LegacyInspectorShell.SectionHeader("Build",
                "Bake turns everything above into streets, blocks, lots and buildings. Clear removes what the last bake"
                + " produced and leaves everything you authored alone.");

            bool building = LegacyInspectorShell.BuildLine(OwnerKey(city));

            string notReady;
            bool ready = city.ReadyToBake(out notReady);
            if (!ready) { EditorGUILayout.HelpBox(notReady, MessageType.Warning); }

            DrawBlockedStages(city);
            DrawCatalogueLine();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!ready || building))
            {
                if (GUILayout.Button("Bake", LegacyInspectorShell.CommandButtonStyle())) { StartBake(city); }
            }
            using (new EditorGUI.DisabledScope(building || city.cityData == null))
            {
                if (GUILayout.Button("Clear", LegacyInspectorShell.CommandButtonStyle())) { ClearBake(city); }
            }
            EditorGUILayout.EndHorizontal();

            DrawScanCommand(city, building);

            if (!string.IsNullOrEmpty(lastRefusal))
            {
                EditorGUILayout.HelpBox(lastRefusal, MessageType.Error);
            }
            if (!string.IsNullOrEmpty(lastBakeReport))
            {
                EditorGUILayout.HelpBox(lastBakeReport, lastBakeOk ? MessageType.Info : MessageType.Error);
            }
        }

        /// <summary> C-18. The third command: measure the ground under the author's own hand-placed objects and say what
        /// was found, without baking anything.
        ///
        /// It sits in this section rather than in *From what* because it is a command and this is where the commands are,
        /// and it is separate from Bake because the two answer different questions -- Bake asks what the city should be,
        /// this asks whether what already stands there is standing properly. It never writes a heightmap value and never
        /// produces a pad request: the levelling half is the scan node's, inside a bake, where the road and the pads are
        /// moving the ground anyway. Pressed here it is a report and nothing else, which is why it is safe to press at
        /// any time.
        ///
        /// Disabled with its reason shown, following this section's rule that a command which cannot run says so before
        /// it is pressed rather than after. </summary>
        private void DrawScanCommand(CityAuthoring city, bool building)
        {
            List<Transform> roots = city.ResolvedScanRoots();
            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            ITerrainSurfaceReader ground = gateway != null ? gateway.SurfaceReader : null;

            string why = string.Empty;
            if (roots.Count == 0)
            {
                why = "Name at least one scan root in *From what* to measure the ground under your own objects.";
            }
            else if (ground == null)
            {
                why = "There is no terrain gateway in this scene, so there is no ground to measure against.";
            }

            using (new EditorGUI.DisabledScope(building || why.Length > 0))
            {
                if (GUILayout.Button("Scan hand-placed objects", LegacyInspectorShell.CommandButtonStyle()))
                {
                    RunHandPlacedScan(roots, ground);
                }
            }
            if (why.Length > 0) { EditorGUILayout.LabelField(why, EditorStyles.miniLabel); }

            if (!string.IsNullOrEmpty(lastScanSummary))
            {
                EditorGUILayout.HelpBox(lastScanSummary, lastScanFindings.Count > 0
                    ? MessageType.Warning
                    : MessageType.Info);
                for (int i = 0; i < lastScanFindings.Count; i++)
                {
                    EditorGUILayout.LabelField(lastScanFindings[i], EditorStyles.miniLabel);
                }
            }
        }

        /// <summary> Run the sweep and keep what it said. The tolerances are the class defaults rather than the scan
        /// node's parameters on purpose: this button exists so an author can check a scene that has no scan node in its
        /// graph yet, and reading a node that may not be there would make the command conditional on the graph. </summary>
        private void RunHandPlacedScan(List<Transform> roots, ITerrainSurfaceReader ground)
        {
            lastScanSummary = string.Empty;
            lastScanFindings.Clear();

            string summary;
            List<FootprintScanFinding> findings = HandPlacedGroundScan.Sweep(roots, ground,
                FootprintScanClass.Default(), PadElevationRule.CentroidSample, null, out summary);
            lastScanSummary = summary;

            for (int i = 0; i < findings.Count; i++)
            {
                if (!findings[i].NeedsAttention) { continue; }
                if (lastScanFindings.Count >= ScanFindingLimit)
                {
                    lastScanFindings.Add((findings.Count - i).ToString(CultureInfo.InvariantCulture)
                        + " more were not listed. The rest are in the bake report when the scan node runs.");
                    break;
                }
                lastScanFindings.Add(findings[i].Describe());
            }
        }

        /// <summary> How many per-object lines the inspector will draw. An inspector that grew a line per object would
        /// push the sections below it off the screen for a scene with a thousand props in it. </summary>
        private const int ScanFindingLimit = 12;

        /// <summary> Every node the bake will pass over, named with its stage and its reason.
        ///
        /// Walked in the canonical stage order rather than in graph order, because the answer an author wants is "how far
        /// does this bake get", and that reads down the pipeline. A blocked node is not an error: a graph is allowed to be
        /// half-wired while it is being built, and the bake produces everything up to the hole. </summary>
        private void DrawBlockedStages(CityAuthoring city)
        {
            if (city.graph == null || city.graph.nodes == null) { return; }

            int blocked = 0;
            for (int s = 0; s < CityBakePipeline.CanonicalOrder.Length; s++)
            {
                BakeStage stage = CityBakePipeline.CanonicalOrder[s];
                for (int i = 0; i < city.graph.nodes.Count; i++)
                {
                    CityGraphNode node = city.graph.nodes[i];
                    if (node == null || node.stage != stage) { continue; }

                    CityNodeDescriptor descriptor;
                    string reason;
                    if (CityNodeCatalog.TryGetForNode(node, out descriptor, out reason) && !descriptor.IsBlocked)
                    {
                        if (!descriptor.NeedsOperatorAsset || node.operatorAsset != null) { continue; }
                        reason = "it needs an operator asset and none is assigned, so this node is passed over";
                    }
                    else if (descriptor != null && descriptor.IsBlocked)
                    {
                        reason = descriptor.blockedReason;
                    }

                    blocked++;
                    EditorGUILayout.HelpBox(ObjectNames.NicifyVariableName(stage.ToString()) + " -- "
                        + node.DisplayName() + ": " + reason, MessageType.Warning);
                }
            }

            if (blocked > 0)
            {
                EditorGUILayout.LabelField(blocked.ToString(CultureInfo.InvariantCulture)
                    + " node(s) will be passed over. The bake still runs and produces everything else.",
                    EditorStyles.miniLabel);
            }
        }

        /// <summary> Press Bake. Builds the request and hands it to the ticket; nothing here bakes anything itself.
        ///
        /// This is the first place in the shipped product that constructs a <see cref="CityBakeRequest"/> -- until now
        /// only gate fixtures did -- so it is also the place where every field of it has to be answered honestly. The
        /// three suitability sources are left null on purpose: nothing outside the tests implements them yet, and the
        /// dispatch reports a dropped factor per node rather than inventing a figure for it. </summary>
        private void StartBake(CityAuthoring city)
        {
            lastRefusal = string.Empty;
            lastBakeReport = string.Empty;
            lastBakeOk = false;

            string refusal;
            if (!CityBakeTicket.Enqueue(city.graph, BuildRequest(city), out refusal))
            {
                lastRefusal = refusal;
            }
        }

        /// <summary> Everything the bake needs to know, read once, on the main thread, before the walk starts. </summary>
        private CityBakeRequest BuildRequest(CityAuthoring city)
        {
            CityBakeRequest request = new CityBakeRequest();
            request.cityRoot = city.transform;
            request.citySeed = city.seed;
            request.city = city.cityData;

            Rect region;
            string regionWhy;
            if (city.TryRegionWorldXZ(out region, out regionWhy)) { request.region = region; }

            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            if (gateway != null) { request.surface = gateway.SurfaceReader; }

            // C-18. The roots the hand-placed sweep walks, resolved here rather than in the runner so the empty slots
            // and the duplicates are already gone: a list with the same root twice would otherwise produce two pad
            // requests for one building.
            request.scanRoots = city.ResolvedScanRoots();

            request.zones = ZoneCatalog.Authored();
            request.roadClasses = ResolveRoadClasses();
            request.content = BuildContentCatalog(city);
            request.settlementOrder = ResolveSettlementOrder(city);
            request.settlementFit = ResolveSettlementFit();

            // The stamp's three strings. The timestamp is read HERE and not in the runner, which forbids the clock so
            // that one bake stamps every node with one instant rather than each node with its own.
            request.generatorVersion = CityBakePipeline.GeneratorVersion;
            request.sourceCityAssetGuid = GuidOf(city.cityData);
            request.bakedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            return request;
        }

        /// <summary> An asset's GUID, or empty for an asset that has never been saved. Empty is a legal answer: the
        /// ticket falls back to the graph's instance id for the owner, which lasts exactly as long as an unsaved asset
        /// does. </summary>
        private static string GuidOf(UnityEngine.Object asset)
        {
            if (asset == null) { return string.Empty; }
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary> The project's road class catalogue, or a defaults-only one held for this bake alone.
        ///
        /// A transient catalogue is not a silent substitute: <see cref="RoadClassCatalog.ResetToDefaults"/> is the same
        /// table an author gets from a fresh asset, so the bake is correct, and the section says which of the two was
        /// used. A bake refused for having no catalogue would be worse -- the author has nothing to fix but an asset
        /// nobody told them to create. </summary>
        private static RoadClassCatalog ResolveRoadClasses()
        {
            string[] guids = AssetDatabase.FindAssets("t:RoadClassCatalog");
            for (int i = 0; i < guids.Length; i++)
            {
                RoadClassCatalog found = AssetDatabase.LoadAssetAtPath<RoadClassCatalog>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (found != null) { return found; }
            }

            RoadClassCatalog transient = ScriptableObject.CreateInstance<RoadClassCatalog>();
            transient.ResetToDefaults();
            return transient;
        }

        /// <summary> Which order of place this is, as an integer the element nodes read to pick one of their four rows.
        ///
        /// The mapping from the road's role to the order is the settlement catalogue's, never a switch here: the
        /// catalogue is what an author edits, and a second copy of the table in an inspector would be free to disagree
        /// with it. A road that declares no role leaves the order at 0, which is what the catalogue's own refusal
        /// means -- no order was looked up -- rather than a fabricated village. </summary>
        private static int ResolveSettlementOrder(CityAuthoring city)
        {
            SettlementRoleEnum role;
            string roleWhy;
            if (!city.TryGetSourceRole(out role, out roleWhy)) { return 0; }

            string[] guids = AssetDatabase.FindAssets("t:SettlementOrderCatalog");
            for (int i = 0; i < guids.Length; i++)
            {
                SettlementOrderCatalog catalogue = AssetDatabase.LoadAssetAtPath<SettlementOrderCatalog>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (catalogue == null) { continue; }

                SettlementOrderClass order;
                string orderWhy;
                if (catalogue.TryOrderFor(role, out order, out orderWhy)) { return order.orderIndex; }
            }
            return 0;
        }

        /// <summary> The settlement fit table this bake reads for its railway, metro and transit answers, resolved the
        /// way every other catalogue is: the author's own asset when one exists, and an honest labelled placeholder when
        /// none does, so a bake is never refused for the want of a table nobody was told to make.
        ///
        /// A FITTED table wins over a built one. An author who imported a real station dump fitted curves into an asset
        /// on purpose, and <see cref="SettlementFitTable.CreateAuthored"/> would quietly overwrite those with shipped
        /// placeholders; so the first saved <see cref="SettlementFitTable"/> is used as-is, and the placeholder is built
        /// only when the project holds none.
        ///
        /// The placeholder is built FROM a settlement order catalogue and not from nothing, because
        /// <see cref="SettlementFitTable.CreateAuthored"/> returns a table with no rail or metro rows for a null
        /// catalogue -- and a table with no rows makes the rail and underground nodes refuse, which reads as a broken
        /// tool rather than the missing asset it is. So a catalogue is resolved first. </summary>
        private static SettlementFitTable ResolveSettlementFit()
        {
            string[] tableGuids = AssetDatabase.FindAssets("t:SettlementFitTable");
            for (int i = 0; i < tableGuids.Length; i++)
            {
                SettlementFitTable found = AssetDatabase.LoadAssetAtPath<SettlementFitTable>(
                    AssetDatabase.GUIDToAssetPath(tableGuids[i]));
                if (found != null) { return found; }
            }
            return SettlementFitTable.CreateAuthored(ResolveSettlementOrderCatalog());
        }

        /// <summary> One settlement order catalogue for the fit table to be built from: the author's own asset when one
        /// exists, else a defaults-only one held for this bake alone. Distinct from <see cref="ResolveSettlementOrder"/>,
        /// which reads an order INDEX out of whichever catalogue answers a role; this returns the catalogue OBJECT so
        /// <see cref="SettlementFitTable.CreateAuthored"/> has the four bands to shape its placeholder rows around. The
        /// two do not share a body because one loops for a match and the other takes the first asset. </summary>
        private static SettlementOrderCatalog ResolveSettlementOrderCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:SettlementOrderCatalog");
            for (int i = 0; i < guids.Length; i++)
            {
                SettlementOrderCatalog found = AssetDatabase.LoadAssetAtPath<SettlementOrderCatalog>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (found != null) { return found; }
            }

            SettlementOrderCatalog transient = ScriptableObject.CreateInstance<SettlementOrderCatalog>();
            transient.ResetToDefaults();
            return transient;
        }

        /// <summary> One catalogue out of every library this settlement names, so two sets that both hold a category
        /// contribute to the same pool. Only an asset with a remembered PASS is placeable, which is the cache's rule and
        /// the reason the report above and the bake can never disagree. </summary>
        private ContentCatalog BuildContentCatalog(CityAuthoring city)
        {
            ContentCatalog catalog = new ContentCatalog();
            List<CategoryContentSet> libraries = city.ResolvedLibraries();
            if (libraries == null) { return catalog; }

            for (int i = 0; i < libraries.Count; i++)
            {
                if (libraries[i] == null) { continue; }
                libraries[i].RegisterInto(catalog, contentCache);
            }
            return catalog;
        }

        /// <summary> Which authored catalogues this bake will read, named before it runs. A bake that quietly falls back
        /// to built-in road classes and a bake that reads the author's own asset produce different streets, and that is
        /// not a difference to discover afterwards from the result. </summary>
        private static void DrawCatalogueLine()
        {
            string[] guids = AssetDatabase.FindAssets("t:RoadClassCatalog");
            EditorGUILayout.LabelField(guids != null && guids.Length > 0
                ? "Road classes: this project's own catalogue."
                : "Road classes: built-in defaults -- this project holds no Road Class Catalogue asset. Create one"
                    + " through Assets > Create to change the widths, lane counts and design speeds the streets get.",
                EditorStyles.miniLabel);
        }

        /// <summary> Press Clear. Removes what the last bake produced and nothing else.
        ///
        /// Two halves, because a bake writes in two places: the derived lists on the <see cref="CityData"/> asset, and the
        /// scene objects under the five branches. Both go through <see cref="Undo"/>, so Clear is one Ctrl+Z -- the whole
        /// point of a Clear an author can trust is that pressing it by mistake costs nothing.
        ///
        /// The authored fields are untouched: the seed, the rectangle, the graph and the libraries are what the author
        /// typed, and a Clear that reset them would be a Clear nobody dares press. </summary>
        private void ClearBake(CityAuthoring city)
        {
            lastRefusal = string.Empty;
            lastBakeReport = string.Empty;
            lastBakeOk = false;

            if (city.cityData != null)
            {
                Undo.RecordObject(city.cityData, "Clear city");
                city.cityData.ClearDerived();
                EditorUtility.SetDirty(city.cityData);
            }

            IList<CityBranch> branches = CityHierarchy.All();
            int removed = 0;
            for (int i = 0; i < branches.Count; i++)
            {
                Transform branch = city.transform.Find(CityHierarchy.BranchName(branches[i]));
                if (branch == null) { continue; }
                Undo.DestroyObjectImmediate(branch.gameObject);
                removed++;
            }

            lastBakeOk = true;
            lastBakeReport = "Cleared. " + removed.ToString(CultureInfo.InvariantCulture)
                + " generated branch(es) removed, and the city data's derived lists emptied. Everything you authored is"
                + " untouched.";
        }
        #endregion

        #region "What came out -- the counts, the stamp, and a way into the hierarchy"
        /// <summary> Section 5. What the last bake actually produced.
        ///
        /// Every figure here is read from the <see cref="CityData"/> asset and none of it is recomputed, so the section
        /// answers "what is on disk" rather than "what would a bake produce now" -- which is the question that makes a
        /// stale city findable. The figures are drawn through the shell's measured row, so they are disabled and read as
        /// answers; they are not put behind the measured foldout, because they are the whole content of a section the
        /// author opened on purpose and a second click to see them would hide the section behind itself. </summary>
        private void DrawWhatCameOut(CityAuthoring city)
        {
            LegacyInspectorShell.SectionHeader("What came out",
                "What the last bake produced, read from the city data asset rather than measured again: the counts, the"
                + " stamp that says which build made them, and a way into the objects themselves.");

            Row("cityData", "City data",
                "The asset the bake writes its result into. One per settlement: sharing one between two settlements"
                + " means the second bake overwrites the first one's city.");

            if (city.cityData == null)
            {
                EditorGUILayout.HelpBox("No city data asset is assigned, so there is nothing to show and a bake has"
                    + " nowhere to put its result. Create one through Assets > Create > Road City Master > City Data.",
                    MessageType.Info);
                return;
            }

            DrawCounts(city.cityData);
            DrawStamp(city);
            DrawBranchButtons(city);
        }

        /// <summary> The five counts, in the order the pipeline produces them. </summary>
        private static void DrawCounts(CityData data)
        {
            LegacyInspectorShell.Measured("Network nodes", Count(data.nodes == null ? -1 : data.nodes.Count), "");
            LegacyInspectorShell.Measured("Network edges", Count(data.edges == null ? -1 : data.edges.Count), "");
            LegacyInspectorShell.Measured("Blocks", Count(data.blocks == null ? -1 : data.blocks.Count), "");
            LegacyInspectorShell.Measured("Lots", Count(data.lots == null ? -1 : data.lots.Count), "");
            LegacyInspectorShell.Measured("Buildings", Count(data.buildings == null ? -1 : data.buildings.Count), "");

            if (data.nodes != null && data.nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("This city data holds nothing yet. Press Bake above.", MessageType.None);
            }
        }

        /// <summary> The bake stamp, and the one verdict it can honestly give.
        ///
        /// The stamp is rebuilt from the asset rather than read off it, because the stamp a bake produces is published on
        /// the bus for the runtime stage and is not stored on the <see cref="CityData"/>. What the asset does keep is the
        /// three fields the stamp is made of that matter to a verdict -- seed, generator version, schema version -- and
        /// <see cref="BakeStamp.Evaluate"/> ignores the timestamp anyway.
        ///
        /// So the bake TIME is not shown, and is said to be unrecorded rather than filled in from the asset's file date:
        /// a file date is when the asset was last written, which a re-import changes and a bake does not. </summary>
        private static void DrawStamp(CityAuthoring city)
        {
            CityData data = city.cityData;
            BakeStamp stamp = BakeStamp.Create(data, data.generatorVersion, GuidOf(data), string.Empty);

            LegacyInspectorShell.Measured("Baked with seed", stamp.seed.ToString(CultureInfo.InvariantCulture), "");
            LegacyInspectorShell.Measured("Baked by generator",
                string.IsNullOrEmpty(stamp.generatorVersion) ? "not recorded" : stamp.generatorVersion, "");
            LegacyInspectorShell.Measured("This build's generator", CityBakePipeline.GeneratorVersion, "");
            LegacyInspectorShell.Measured("Schema", stamp.schemaVersion.ToString(CultureInfo.InvariantCulture)
                + " of " + CityData.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture), "");

            if (stamp.seed != city.seed)
            {
                EditorGUILayout.HelpBox("This city was baked with seed "
                    + stamp.seed.ToString(CultureInfo.InvariantCulture) + " and the settlement now asks for "
                    + city.seed.ToString(CultureInfo.InvariantCulture)
                    + ". What you are looking at is the older city until you bake again.", MessageType.Warning);
            }

            BakeStaleness verdict = stamp.Evaluate(CityBakePipeline.GeneratorVersion, CityData.CurrentSchemaVersion);

            // A city that has never been baked carries no generator version at all, and an empty string sorts below
            // every real one -- so asking for a verdict would answer "baked by an older generator" about a bake that
            // never happened. The schema verdict is still worth having, because a fresh asset written by an older build
            // of the tool can be schema-stale before it is ever baked.
            if (string.IsNullOrEmpty(stamp.generatorVersion) && verdict != BakeStaleness.StaleSchema)
            {
                LegacyInspectorShell.Measured("Standing", "never baked", "");
                return;
            }

            switch (verdict)
            {
                case BakeStaleness.UpToDate:
                    LegacyInspectorShell.Measured("Standing", "up to date", "");
                    break;
                case BakeStaleness.StaleGenerator:
                    EditorGUILayout.HelpBox("This city was baked by an older generator than this build. It will still"
                        + " load; bake again to get what this build produces.", MessageType.Warning);
                    break;
                case BakeStaleness.NewerGenerator:
                    EditorGUILayout.HelpBox("This city was baked by a NEWER generator than this build. Do not bake over"
                        + " it with this build unless you mean to -- the newer bake knows things this one does not.",
                        MessageType.Warning);
                    break;
                default:
                    EditorGUILayout.HelpBox("This city was written against schema "
                        + stamp.schemaVersion.ToString(CultureInfo.InvariantCulture) + " and this build reads schema "
                        + CityData.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)
                        + ". Bake again: the counts above may not mean what they say.", MessageType.Error);
                    break;
            }
        }

        /// <summary> A way into the objects, one row per branch.
        ///
        /// The branches are LOOKED UP and never ensured. <see cref="CityHierarchy.EnsureBranch"/> would create the one it
        /// could not find, and an inspector that creates scene objects while drawing is an inspector that makes a scene
        /// dirty for being looked at. A branch that is not there says so, with what it would hold. </summary>
        private void DrawBranchButtons(CityAuthoring city)
        {
            IList<CityBranch> branches = CityHierarchy.All();
            for (int i = 0; i < branches.Count; i++)
            {
                string name = CityHierarchy.BranchName(branches[i]);
                Transform found = city.transform.Find(name);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(found == null))
                {
                    if (GUILayout.Button(found == null ? name + " -- not built" : name,
                        LegacyInspectorShell.CommandButtonStyle()))
                    {
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found.gameObject);
                    }
                }
                EditorGUILayout.LabelField(found == null
                    ? CityHierarchy.BranchPurpose(branches[i])
                    : found.childCount.ToString(CultureInfo.InvariantCulture) + " object(s)",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary> A count, with a missing list told apart from an empty one. An empty list means the bake produced
        /// none of these; a null list means the asset does not have the field at all, which is a different problem and
        /// should not read as a zero. </summary>
        private static string Count(int value)
        {
            return value < 0 ? "not present" : value.ToString(CultureInfo.InvariantCulture);
        }
        #endregion

        #region "Helpers"
        /// <summary> One authored field, drawn once.
        ///
        /// The registry is asked FIRST, and where it owns the field the drawer draws it -- that is RM-102's rule, and the
        /// gate asserts an inspector does not draw a registry-owned field itself. The label and the tooltip passed in are
        /// therefore the answer for a field the registry does not own yet; the moment C-10 registers one, the same call
        /// starts producing a unit column and an inline out-of-range message and stops using them.
        ///
        /// The registry is asked through <see cref="LegacyInspectorShell.Descriptor"/> rather than by reading
        /// <see cref="LegacyInspectorShell.Field"/>'s result, because that result answers a different question: it is
        /// false both for a field the registry does not own and for an owned field on a pass where nothing was
        /// committed. </summary>
        private void Row(string field, string label, string tooltip)
        {
            if (LegacyInspectorShell.Descriptor(ComponentName, field) != null)
            {
                LegacyInspectorShell.Field(serializedObject, ComponentName, field, fields);
                return;
            }

            SerializedProperty prop = serializedObject.FindProperty(field);
            if (prop == null) { return; }
            fields.Property(prop, new GUIContent(label, tooltip));
        }

        /// <summary> A measured number, formatted one way for the whole inspector. Two decimals at most, because a
        /// region 500.0000001 m wide is a float artefact and not a measurement, and none where the value is whole.
        /// </summary>
        private static string Figure(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary> The build-queue owner this settlement's bakes are filed under.
        ///
        /// Derived by asking <see cref="CityBakeTicket.OwnerOf"/> rather than by spelling the rule again here: the rule
        /// (the city asset's GUID, or the graph's instance id for an unsaved graph) is what makes two cities bake side by
        /// side and one city supersede its own earlier bake, and a second copy of it in an inspector would be free to
        /// disagree and quietly break both. </summary>
        private string OwnerKey(CityAuthoring city)
        {
            CityBakeRequest probe = new CityBakeRequest();
            probe.sourceCityAssetGuid = GuidOf(city.cityData);
            return CityBakeTicket.OwnerOf(city.graph, probe);
        }

        /// <summary> How small a corner drag may make the region. Below this the rectangle encloses no useful ground and
        /// <see cref="CityAuthoring.TryRegionWorldXZ"/> starts refusing it -- at which point the handles this method
        /// draws disappear and the author has no way to drag it back. So the floor is enforced HERE, on the drag, rather
        /// than discovered afterwards as a section that stopped drawing.
        ///
        /// AUTHORED: 10 m is one small building plot, which is the smallest thing a settlement can be said to occupy.
        /// </summary>
        private const float MinimumRegionMetres = 10f;

        private readonly Vector3[] rectangle = new Vector3[4];

        /// <summary> The region as a rectangle on the ground, with a handle on each corner.
        ///
        /// Corners only, and deliberately: WHERE the rectangle sits is the <see cref="CityAuthoring.regionCentre"/>
        /// Transform's business, and it already has Unity's own move tool. A second way to move the same thing is how two
        /// controls end up disagreeing about one position. What the Transform cannot express is the rectangle's SIZE, and
        /// that is what these four handles are for.
        ///
        /// Each corner owns the two edges that meet at it, so dragging one leaves the opposite corner where it was --
        /// which is what makes a rectangle resizable without also sliding. </summary>
        private void OnScene(SceneView view)
        {
            CityAuthoring city = target as CityAuthoring;
            if (city == null) { return; }

            Rect world;
            string why;
            if (!city.TryRegionWorldXZ(out world, out why)) { return; }

            Vector3 centre = city.CentreWorld();
            float y = centre.y;

            rectangle[0] = new Vector3(world.xMin, y, world.yMin);
            rectangle[1] = new Vector3(world.xMax, y, world.yMin);
            rectangle[2] = new Vector3(world.xMax, y, world.yMax);
            rectangle[3] = new Vector3(world.xMin, y, world.yMax);

            Handles.DrawSolidRectangleWithOutline(rectangle,
                new Color(0.25f, 0.6f, 1f, 0.05f), new Color(0.25f, 0.6f, 1f, 0.9f));
            Handles.Label(new Vector3(world.center.x, y, world.yMax),
                Figure(world.width) + " × " + Figure(world.height) + " m");

            EditorGUI.BeginChangeCheck();
            Vector3 a = CornerHandle(rectangle[0]);
            Vector3 b = CornerHandle(rectangle[1]);
            Vector3 c = CornerHandle(rectangle[2]);
            Vector3 d = CornerHandle(rectangle[3]);
            if (!EditorGUI.EndChangeCheck()) { return; }

            ApplyCornerDrag(city, world, centre, a, b, c, d);
        }

        /// <summary> One corner dot, sized in screen space so it stays grabbable at any zoom. </summary>
        private static Vector3 CornerHandle(Vector3 position)
        {
            return Handles.FreeMoveHandle(position, HandleUtility.GetHandleSize(position) * 0.08f,
                Vector3.zero, Handles.DotHandleCap);
        }

        /// <summary> Fold whichever corner moved back into <see cref="CityAuthoring.regionXZ"/>.
        ///
        /// Only the two edges belonging to the moved corner change; the height a corner was dragged to is DISCARDED,
        /// because the region is a footprint on the ground and a rectangle with a lifted corner is not one. The result is
        /// written centre-relative, the inverse of <see cref="CityAuthoring.TryRegionWorldXZ"/>, so moving the centre
        /// Transform afterwards carries the region with it instead of leaving it behind. </summary>
        private void ApplyCornerDrag(CityAuthoring city, Rect world, Vector3 centre,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float xMin = world.xMin, xMax = world.xMax, zMin = world.yMin, zMax = world.yMax;

            // Vector3's equality is approximate, which is exactly the test wanted here: an untouched handle hands back
            // the position it was given, and a drag of less than a rounding error is not a drag.
            if (a != new Vector3(world.xMin, centre.y, world.yMin)) { xMin = a.x; zMin = a.z; }
            if (b != new Vector3(world.xMax, centre.y, world.yMin)) { xMax = b.x; zMin = b.z; }
            if (c != new Vector3(world.xMax, centre.y, world.yMax)) { xMax = c.x; zMax = c.z; }
            if (d != new Vector3(world.xMin, centre.y, world.yMax)) { xMin = d.x; zMax = d.z; }

            // Keep the floor by moving the dragged edge, never the one the author left alone.
            if (xMax - xMin < MinimumRegionMetres)
            {
                if (xMin != world.xMin) { xMin = xMax - MinimumRegionMetres; }
                else { xMax = xMin + MinimumRegionMetres; }
            }
            if (zMax - zMin < MinimumRegionMetres)
            {
                if (zMin != world.yMin) { zMin = zMax - MinimumRegionMetres; }
                else { zMax = zMin + MinimumRegionMetres; }
            }

            Undo.RecordObject(city, "Resize city region");
            city.regionXZ = new Rect(xMin - centre.x, zMin - centre.z, xMax - xMin, zMax - zMin);
            EditorUtility.SetDirty(city);
            Repaint();
        }
    }
}
#endif

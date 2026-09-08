// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GVNode = UnityEditor.Experimental.GraphView.Node;
using GVPort = UnityEditor.Experimental.GraphView.Port;
#endregion


// The two aliases above are for reading, not for a name clash: in this file "node" and "port" mean a CityGraphNode and
// a catalogue port sheet entry far more often than they mean GraphView's widgets, and untangling which of the two a
// bare Node meant on line 400 is not work a reader should have to do.
namespace RoadCityMaster.City.Editor
{
    /// <summary> B-15 / C-8. Which node the author has selected on the canvas, for the inspector to draw the parameter
    /// sheet of.
    ///
    /// A static rather than a message from the window to the inspector, because the two are views of one graph and
    /// neither owns the other: the window can be closed while the inspector is open, and the inspector redraws on its
    /// own schedule. A <see cref="CityGraphNode"/> is not a <c>UnityEngine.Object</c>, so Unity's own
    /// <c>Selection</c> cannot carry it -- this is the one thing that can.
    ///
    /// Editor-only, and deliberately not serialised: a selection is what an author is doing right now, and one restored
    /// across a domain reload would point the inspector at a node the canvas is no longer showing. </summary>
    public static class CityGraphSelection
    {
        #region "The selection"
        /// <summary> The graph the selected node belongs to, or null. </summary>
        public static CityGraph Graph { get; private set; }

        /// <summary> The selected node's id, or empty. The id and not the node, so a node deleted while the inspector
        /// was looking elsewhere resolves to nothing rather than to an object no graph holds. </summary>
        public static string NodeId { get; private set; }

        /// <summary> Raised when either changes, so an inspector repaints without polling. </summary>
        public static event Action Changed;

        public static void Set(CityGraph graph, string nodeId)
        {
            string id = string.IsNullOrEmpty(nodeId) ? string.Empty : nodeId;
            if (Graph == graph && string.Equals(NodeId, id, StringComparison.Ordinal)) { return; }

            Graph = graph;
            NodeId = id;
            if (Changed != null) { Changed(); }
        }

        public static void Clear() { Set(null, string.Empty); }

        /// <summary> The selected node, or null when nothing is selected or the graph no longer holds it. </summary>
        public static CityGraphNode Node
        {
            get
            {
                if (Graph == null || string.IsNullOrEmpty(NodeId)) { return null; }

                CityGraphNode node;
                return Graph.TryGetNode(NodeId, out node) ? node : null;
            }
        }
        #endregion
    }


    /// <summary> B-15 / C-8. The city graph canvas: the window an author lays a city out in.
    ///
    /// It edits topology only -- add a node, delete a node, connect, disconnect, move. It never edits a parameter. That
    /// is the inspector's half (C-9), and the two are views of the one <see cref="CityGraph"/> rather than two models of
    /// it: everything here writes straight into <c>graph.nodes</c> and <c>graph.edges</c> and nothing is cached that the
    /// asset does not already hold.
    ///
    /// There is no Bake button in this window. One Bake, in the inspector -- two would be two execution paths, which is
    /// the mistake C-6 and C-7 exist to prevent.
    ///
    /// Written on <c>UnityEditor.Experimental.GraphView</c>, following Universe Creator's four graph files as a pattern
    /// and sharing no code with them: RCM and UC are two products in two repositories, and a shared editor base class
    /// would be a link between them that neither the linker contract nor a customer's package layout allows. </summary>
    public sealed class CityGraphWindow : EditorWindow
    {
        #region "Window state"
        /// <summary> Serialised so the window still shows the same city after a domain reload -- a script recompile with
        /// the canvas open otherwise leaves an empty window over an author's half-laid-out city. </summary>
        [SerializeField] private CityGraph graph;

        private CityGraphCanvas canvas;
        private VisualElement emptyState;
        private Label countLabel;
        private Label validationLabel;
        #endregion


        #region "Opening"
        [MenuItem("Window/Road City Master/City graph", false, 3)]
        public static void Open()
        {
            CityGraphWindow window = GetWindow<CityGraphWindow>();
            window.titleContent = new GUIContent("City graph");
            window.minSize = new Vector2(560f, 320f);
            window.Show();
        }

        /// <summary> Opens the window on one graph, for the inspector's Open graph button and for a double-click on the
        /// asset. </summary>
        public static void Open(CityGraph target)
        {
            Open();
            CityGraphWindow window = GetWindow<CityGraphWindow>();
            window.SetGraph(target);
        }
        #endregion


        #region "Building the window"
        /// <summary> CreateGUI and not OnEnable, because the root visual element does not exist yet in OnEnable and a
        /// canvas added there is silently dropped. </summary>
        private void CreateGUI()
        {
            rootVisualElement.Add(BuildToolbar());

            canvas = new CityGraphCanvas(this);
            canvas.style.flexGrow = 1f;
            rootVisualElement.Add(canvas);

            emptyState = BuildEmptyState();
            rootVisualElement.Add(emptyState);

            Rebuild();
        }

        private void OnEnable()
        {
            // An undo that removed a node has to be redrawn: the graph is the model and it has already changed by the
            // time this runs, so the canvas is rebuilt from it rather than trying to undo the widgets.
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            Rebuild();
        }

        private VisualElement BuildToolbar()
        {
            Toolbar bar = new Toolbar();

            ObjectField field = new ObjectField("Graph");
            field.objectType = typeof(CityGraph);
            field.allowSceneObjects = false;
            field.value = graph;
            field.style.minWidth = 260f;
            field.RegisterValueChangedCallback(delegate (ChangeEvent<UnityEngine.Object> evt)
            {
                SetGraph(evt.newValue as CityGraph);
            });
            bar.Add(field);

            ToolbarButton frame = new ToolbarButton(delegate ()
            {
                if (canvas != null) { canvas.FrameAll(); }
            });
            frame.text = "Frame all";
            bar.Add(frame);

            ToolbarButton validate = new ToolbarButton(delegate () { RunValidation(); });
            validate.text = "Validate";
            bar.Add(validate);

            countLabel = new Label(string.Empty);
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            countLabel.style.marginLeft = 8f;
            bar.Add(countLabel);

            validationLabel = new Label(string.Empty);
            validationLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            validationLabel.style.marginLeft = 8f;
            validationLabel.style.flexGrow = 1f;
            validationLabel.style.overflow = Overflow.Hidden;
            bar.Add(validationLabel);

            return bar;
        }

        private VisualElement BuildEmptyState()
        {
            VisualElement panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 0f;
            panel.style.right = 0f;
            panel.style.top = 22f;
            panel.style.bottom = 0f;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;

            // Ignore, or the panel eats the drop of a graph asset dragged onto the window it is covering.
            panel.pickingMode = PickingMode.Ignore;

            Label message = new Label("Pick a city graph above, or select one in the project window.");
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            message.style.opacity = 0.6f;
            panel.Add(message);

            return panel;
        }
        #endregion


        #region "The graph on show"
        /// <summary> The graph the canvas is editing, or null. </summary>
        public CityGraph Graph { get { return graph; } }

        /// <summary> Puts one graph on the canvas. Safe before CreateGUI has run -- the field is set either way and the
        /// canvas picks it up when it is built. </summary>
        public void SetGraph(CityGraph target)
        {
            graph = target;
            CityGraphSelection.Clear();
            Rebuild();
        }

        /// <summary> Redraws the canvas from the graph. The one refresh path: every mutation ends here rather than
        /// patching widgets, because a graph edited by the inspector, by an undo, or by
        /// <see cref="CitySettlementFactory"/> has no way to tell this window which widget went stale. </summary>
        public void Rebuild()
        {
            if (canvas != null) { canvas.Populate(graph); }
            if (emptyState != null) { emptyState.style.display = graph == null ? DisplayStyle.Flex : DisplayStyle.None; }

            NoteGraphChanged();
        }

        /// <summary> Refreshes the toolbar's read-outs after a change, without redrawing the canvas. </summary>
        public void NoteGraphChanged()
        {
            if (countLabel != null)
            {
                countLabel.text = graph == null
                    ? string.Empty
                    : graph.nodes.Count + (graph.nodes.Count == 1 ? " node, " : " nodes, ")
                      + graph.edges.Count + (graph.edges.Count == 1 ? " wire" : " wires");
            }

            if (validationLabel != null) { validationLabel.text = string.Empty; }
        }

        /// <summary> Runs the graph's own validator and shows the first problem. The validator and not a second set of
        /// rules here: a window that judged a graph differently from the baker would send an author to fix a wire the
        /// bake was happy with. </summary>
        private void RunValidation()
        {
            if (validationLabel == null) { return; }

            if (graph == null) { validationLabel.text = string.Empty; return; }

            string reason;
            validationLabel.text = graph.Validate(out reason) ? "valid" : reason;
        }
        #endregion
    }


    /// <summary> B-15 / C-8. The canvas itself: a <c>GraphView</c> over one <see cref="CityGraph"/>.
    ///
    /// Every edit here is topology, and every edit writes into the graph asset immediately -- there is no apply step and
    /// no shadow copy of the node list, because a shadow copy is how a canvas and an inspector come to disagree about
    /// what a city is.
    ///
    /// Deletion has exactly one implementation, <see cref="DeleteSelection"/>. The dustbin, the right-click menu and the
    /// Delete key are three affordances that all call it: a second delete path is how an edge survives the node it was
    /// attached to. </summary>
    public sealed class CityGraphCanvas : GraphView
    {
        #region "Canvas state"
        private const float NodeWidth = 220f;
        private const float NodeHeight = 132f;

        private readonly CityGraphWindow owner;
        private CityGraph graph;
        private VisualElement trashBin;
        private CityGraphSearchProvider searchProvider;

        /// <summary> Where the mouse was, in panel space. Tracked rather than read from the event that wants it, because
        /// the two callers -- the right-click menu and the search window it opens -- both run after the mouse event that
        /// triggered them has been handled. </summary>
        private Vector2 lastWorldMouse;
        #endregion


        #region "Building the canvas"
        public CityGraphCanvas(CityGraphWindow window)
        {
            owner = window;

            SetupZoom(0.3f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new GridBackground();
            grid.StretchToParentSize();
            Insert(0, grid);

            graphViewChanged = OnGraphViewChanged;

            BuildTrashBin();

            // TrickleDown for both: SelectionDragger captures the pointer while a node is being dragged, so a bubbling
            // handler on the canvas never sees the move that carried the node over the bin, nor the release on it.
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
        }
        #endregion


        #region "The dustbin"
        /// <summary> The dustbin in the top-right corner. Added to the view and not to
        /// <c>contentViewContainer</c>, so it holds its corner while the canvas is panned and zoomed -- a bin that slid
        /// away with the content would be a bin an author has to hunt for.
        ///
        /// <c>PickingMode.Ignore</c> because it must never steal a drag: it is a place on the screen that a dragged node
        /// can be released over, not a button. Hit-testing is done by hand against its layout for the same reason.
        /// </summary>
        private void BuildTrashBin()
        {
            trashBin = new VisualElement();
            trashBin.pickingMode = PickingMode.Ignore;
            trashBin.style.position = Position.Absolute;
            trashBin.style.right = 12f;
            trashBin.style.top = 12f;
            trashBin.style.width = 74f;
            trashBin.style.height = 58f;
            trashBin.style.alignItems = Align.Center;
            trashBin.style.justifyContent = Justify.Center;
            trashBin.style.borderTopWidth = 1f;
            trashBin.style.borderBottomWidth = 1f;
            trashBin.style.borderLeftWidth = 1f;
            trashBin.style.borderRightWidth = 1f;
            trashBin.style.borderTopLeftRadius = 4f;
            trashBin.style.borderTopRightRadius = 4f;
            trashBin.style.borderBottomLeftRadius = 4f;
            trashBin.style.borderBottomRightRadius = 4f;

            Label caption = new Label("Drop to\ndelete");
            caption.pickingMode = PickingMode.Ignore;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            trashBin.Add(caption);

            Add(trashBin);
            SetTrashBinHot(false);
        }

        private void SetTrashBinHot(bool hot)
        {
            if (trashBin == null) { return; }

            Color border = hot ? new Color(0.85f, 0.3f, 0.25f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);
            trashBin.style.borderTopColor = border;
            trashBin.style.borderBottomColor = border;
            trashBin.style.borderLeftColor = border;
            trashBin.style.borderRightColor = border;
            trashBin.style.backgroundColor = hot
                ? new Color(0.35f, 0.12f, 0.10f, 0.85f)
                : new Color(0.16f, 0.16f, 0.16f, 0.65f);
        }
        #endregion


        #region "Mouse"
        private void OnMouseDown(MouseDownEvent evt)
        {
            lastWorldMouse = evt.mousePosition;
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            lastWorldMouse = evt.mousePosition;

            bool dragging = evt.pressedButtons != 0 && selection.Count > 0;
            SetTrashBinHot(dragging && IsOverTrashBin(evt.localMousePosition));
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            lastWorldMouse = evt.mousePosition;

            bool dropped = evt.button == 0 && selection.Count > 0 && IsOverTrashBin(evt.localMousePosition);
            SetTrashBinHot(false);
            if (!dropped) { return; }

            // Deferred by one frame, and the release deliberately left to propagate: SelectionDragger is still holding
            // the pointer and is about to write the drop position onto the nodes it moved. Deleting them from under it
            // now would have it finish a drag on elements that no longer have a parent.
            schedule.Execute(delegate () { DeleteSelection(); }).ExecuteLater(0);
        }

        private bool IsOverTrashBin(Vector2 localMouse)
        {
            return trashBin != null && trashBin.layout.Contains(localMouse);
        }
        #endregion


        #region "Drawing the graph"
        /// <summary> Draws one graph, replacing whatever was on the canvas. Called on open, on a graph change and after
        /// an undo -- the graph is the model, so the cheapest correct refresh is to draw it again. </summary>
        public void Populate(CityGraph target)
        {
            graph = target;

            List<GraphElement> existing = graphElements.ToList();
            for (int i = 0; i < existing.Count; i++) { RemoveElement(existing[i]); }

            if (graph == null) { return; }

            Dictionary<string, CityGraphNodeView> views = new Dictionary<string, CityGraphNodeView>();

            for (int n = 0; n < graph.nodes.Count; n++)
            {
                CityGraphNode node = graph.nodes[n];
                if (node == null || string.IsNullOrEmpty(node.id)) { continue; }

                CityGraphNodeView view = new CityGraphNodeView(node);
                Vector2 position = node.canvasPosition == Vector2.zero ? FallbackPosition(n) : node.canvasPosition;
                view.SetPosition(new Rect(position, new Vector2(NodeWidth, NodeHeight)));

                view.RegisterCallback<MouseDownEvent>(delegate (MouseDownEvent evt) { Select(node); },
                    TrickleDown.TrickleDown);

                AddElement(view);
                views[node.id] = view;
            }

            for (int e = 0; e < graph.edges.Count; e++)
            {
                CityGraphEdge edge = graph.edges[e];
                if (edge == null) { continue; }

                CityGraphNodeView fromView;
                CityGraphNodeView toView;
                if (!views.TryGetValue(edge.fromNodeId ?? string.Empty, out fromView)) { continue; }
                if (!views.TryGetValue(edge.toNodeId ?? string.Empty, out toView)) { continue; }

                GVPort output = fromView.FindPort(edge.fromPort, false);
                GVPort input = toView.FindPort(edge.toPort, true);

                // An edge naming a port the catalogue no longer declares is left undrawn rather than guessed at. It is
                // still in the model, and Validate is what tells the author about it -- drawing it on the nearest port
                // would show a city that is wired differently from the one that would bake.
                if (output == null || input == null) { continue; }

                AddElement(output.ConnectTo(input));
            }
        }

        /// <summary> Where a node with no saved position is drawn: a reading order grid by list index. Not written back
        /// to the asset, so opening a window never dirties a city -- the layout is stable because the node order is, and
        /// the first drag saves a real position. </summary>
        private static Vector2 FallbackPosition(int index)
        {
            int column = index % 5;
            int row = index / 5;
            return new Vector2(60f + column * (NodeWidth + 60f), 60f + row * (NodeHeight + 60f));
        }
        #endregion


        #region "Selection"
        /// <summary> Selecting a node on the canvas selects it in the inspector. The graph asset is made Unity's active
        /// object as well, because the node sheet is drawn by the graph's own inspector and a node selected while some
        /// other object is active would have nowhere to appear. </summary>
        private void Select(CityGraphNode node)
        {
            if (graph == null || node == null) { return; }

            CityGraphSelection.Set(graph, node.id);
            if (Selection.activeObject != graph) { Selection.activeObject = graph; }
        }
        #endregion


        #region "Wires"
        /// <summary> Which ports a wire being dragged may end on. Asked of <see cref="CityGraph.CanConnect"/>, so the
        /// canvas refuses exactly what the baker would refuse -- payload mismatch, a backwards stage, a duplicate, a
        /// loop -- rather than keeping a second copy of the rules that drifts from the first. </summary>
        public override List<GVPort> GetCompatiblePorts(GVPort startPort, NodeAdapter nodeAdapter)
        {
            List<GVPort> compatible = new List<GVPort>();
            if (graph == null || startPort == null) { return compatible; }

            CityGraphNodeView startView = startPort.GetFirstAncestorOfType<CityGraphNodeView>();
            if (startView == null) { return compatible; }

            ports.ForEach(delegate (GVPort candidate)
            {
                if (candidate == null || candidate == startPort) { return; }
                if (candidate.direction == startPort.direction) { return; }

                CityGraphNodeView otherView = candidate.GetFirstAncestorOfType<CityGraphNodeView>();
                if (otherView == null || otherView == startView) { return; }

                bool startIsOutput = startPort.direction == Direction.Output;
                CityGraphEdge probe = BuildEdge(startIsOutput ? startPort : candidate,
                    startIsOutput ? candidate : startPort);
                if (probe == null) { return; }

                string reason;
                if (graph.CanConnect(probe, out reason)) { compatible.Add(candidate); }
            });

            return compatible;
        }

        /// <summary> The model edge two widget ports describe, or null when either end cannot be read. </summary>
        private static CityGraphEdge BuildEdge(GVPort output, GVPort input)
        {
            if (output == null || input == null) { return null; }

            CityGraphNodeView fromView = output.GetFirstAncestorOfType<CityGraphNodeView>();
            CityGraphNodeView toView = input.GetFirstAncestorOfType<CityGraphNodeView>();
            if (fromView == null || toView == null) { return null; }

            CityGraphEdge edge = new CityGraphEdge();
            edge.fromNodeId = fromView.NodeId;
            edge.fromPort = output.portName;
            edge.toNodeId = toView.NodeId;
            edge.toPort = input.portName;
            return edge;
        }
        #endregion


        #region "Committing a change"
        /// <summary> The one place a canvas gesture becomes a change to the asset. GraphView reports what the author did
        /// after it has done it, so the work here is to write the same thing into the model -- and, for a wire, to say no
        /// before the widget is kept. </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (graph == null) { return change; }

            bool touched = false;

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(graph, "Move city nodes");
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    CityGraphNodeView view = change.movedElements[i] as CityGraphNodeView;
                    if (view == null) { continue; }

                    CityGraphNode node;
                    if (!graph.TryGetNode(view.NodeId, out node) || node == null) { continue; }

                    node.canvasPosition = view.GetPosition().position;
                    touched = true;
                }
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                List<Edge> refused = new List<Edge>();

                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    Edge widget = change.edgesToCreate[i];
                    CityGraphEdge edge = widget == null ? null : BuildEdge(widget.output, widget.input);

                    string reason;
                    if (edge == null || !graph.CanConnect(edge, out reason)) { refused.Add(widget); continue; }

                    if (!touched) { Undo.RecordObject(graph, "Connect city nodes"); }
                    graph.edges.Add(edge);
                    touched = true;
                }

                // A wire GetCompatiblePorts would have refused can still arrive here -- a paste, or a drop on a port the
                // drag never highlighted -- so it is dropped from the widget list too, and the canvas shows no wire.
                for (int i = 0; i < refused.Count; i++) { change.edgesToCreate.Remove(refused[i]); }
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                // No Undo.RecordObject here: every removal reaches this list through DeleteSelection, which has already
                // opened one undo group. Recording again would split a node and its wires into two undo steps.
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    Edge widget = change.elementsToRemove[i] as Edge;
                    if (widget != null)
                    {
                        if (RemoveModelEdge(widget)) { touched = true; }
                        continue;
                    }

                    CityGraphNodeView view = change.elementsToRemove[i] as CityGraphNodeView;
                    if (view != null && RemoveModelNode(view.NodeId)) { touched = true; }
                }
            }

            if (touched)
            {
                EditorUtility.SetDirty(graph);
                owner.NoteGraphChanged();
            }

            return change;
        }

        private bool RemoveModelEdge(Edge widget)
        {
            CityGraphEdge target = BuildEdge(widget.output, widget.input);
            if (target == null) { return false; }

            for (int i = 0; i < graph.edges.Count; i++)
            {
                CityGraphEdge edge = graph.edges[i];
                if (edge == null) { continue; }
                if (!string.Equals(edge.fromNodeId, target.fromNodeId, StringComparison.Ordinal)) { continue; }
                if (!string.Equals(edge.toNodeId, target.toNodeId, StringComparison.Ordinal)) { continue; }
                if (!string.Equals(edge.fromPort, target.fromPort, StringComparison.Ordinal)) { continue; }
                if (!string.Equals(edge.toPort, target.toPort, StringComparison.Ordinal)) { continue; }

                graph.edges.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary> Removes one node, its wires and its operator sub-asset. The wires go with it in the same step: an
        /// edge whose node is gone is exactly the state <c>CheckEdgeEnds</c> refuses, and leaving one behind would turn a
        /// delete into a broken graph rather than a smaller one. </summary>
        private bool RemoveModelNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) { return false; }

            int index = graph.IndexOfNode(nodeId);
            if (index < 0) { return false; }

            CityGraphNode node = graph.nodes[index];

            for (int i = graph.edges.Count - 1; i >= 0; i--)
            {
                CityGraphEdge edge = graph.edges[i];
                if (edge == null) { graph.edges.RemoveAt(i); continue; }

                bool touchesNode = string.Equals(edge.fromNodeId, nodeId, StringComparison.Ordinal)
                                   || string.Equals(edge.toNodeId, nodeId, StringComparison.Ordinal);
                if (touchesNode) { graph.edges.RemoveAt(i); }
            }

            graph.nodes.RemoveAt(index);

            // The operator lives inside the graph asset, so a deleted node that left its operator behind would leave an
            // orphan sub-asset in the file forever. DestroyObjectImmediate through Undo, so one undo brings both back.
            UnityEngine.Object operatorAsset = node == null ? null : node.operatorAsset as UnityEngine.Object;
            if (operatorAsset != null) { Undo.DestroyObjectImmediate(operatorAsset); }

            if (CityGraphSelection.Graph == graph
                && string.Equals(CityGraphSelection.NodeId, nodeId, StringComparison.Ordinal))
            {
                CityGraphSelection.Clear();
            }

            return true;
        }
        #endregion


        #region "Deleting"
        /// <summary> The one delete. The dustbin, the right-click menu and the Delete key all arrive here, and the whole
        /// removal -- nodes, their wires, their operator sub-assets -- is collapsed into a single undo step, so one
        /// Ctrl+Z puts back exactly what one gesture took away. </summary>
        public override EventPropagation DeleteSelection()
        {
            if (graph == null || selection.Count == 0) { return EventPropagation.Stop; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete from city graph");
            Undo.RecordObject(graph, "Delete from city graph");

            EventPropagation result = base.DeleteSelection();

            Undo.CollapseUndoOperations(group);
            owner.NoteGraphChanged();
            return result;
        }
        #endregion


        #region "The right-click menu"
        /// <summary> Add node on empty canvas, Delete on a selection. The base menu is deliberately not called: it offers
        /// its own Delete, and two Delete entries in one menu invite the reading that they do different things. </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (graph == null)
            {
                evt.menu.AppendAction("Pick a city graph first", null, DropdownMenuAction.Status.Disabled);
                return;
            }

            bool onEmptyCanvas = evt.target == this || evt.target == contentViewContainer;
            if (onEmptyCanvas)
            {
                evt.menu.AppendAction("Add node", delegate (DropdownMenuAction action) { OpenSearchWindow(); });
                evt.menu.AppendSeparator();
            }

            if (selection.Count > 0)
            {
                evt.menu.AppendAction("Delete", delegate (DropdownMenuAction action) { DeleteSelection(); });
            }
            else
            {
                evt.menu.AppendAction("Delete", null, DropdownMenuAction.Status.Disabled);
            }
        }
        #endregion


        #region "Adding a node"
        private void OpenSearchWindow()
        {
            if (searchProvider == null)
            {
                searchProvider = ScriptableObject.CreateInstance<CityGraphSearchProvider>();
                searchProvider.Bind(this);
            }

            // The window's own rect plus the mouse inside it, rather than GUIUtility: Event.current is not there to be
            // read inside a UIElements callback, and this runs from a menu action after the click that opened it.
            Vector2 screenPoint = owner.position.position + lastWorldMouse;
            SearchWindow.Open(new SearchWindowContext(screenPoint, 0f, 0f), searchProvider);
        }

        /// <summary> Adds one catalogue entry to the graph, at the mouse. Goes through
        /// <see cref="CityNodeCatalog.CreateNode"/> and nowhere else, so a node made here carries the same minted id and
        /// the same copied parameter defaults as one made by the settlement factory. </summary>
        public void AddNode(CityNodeDescriptor descriptor)
        {
            if (graph == null || descriptor == null) { return; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add city node");
            Undo.RecordObject(graph, "Add city node");

            CityGraphNode node = CityNodeCatalog.CreateNode(descriptor);
            if (node == null) { return; }

            node.canvasPosition = contentViewContainer.WorldToLocal(lastWorldMouse);
            graph.nodes.Add(node);

            if (descriptor.NeedsOperatorAsset) { AttachOperator(node, descriptor); }

            EditorUtility.SetDirty(graph);
            Undo.CollapseUndoOperations(group);

            owner.Rebuild();
            Select(node);
        }

        /// <summary> Creates the operator a kind needs and stores it inside the graph asset, the way
        /// <see cref="CitySettlementFactory"/> does. An operator whose class cannot be found is simply not attached: the
        /// node is still added, and <see cref="CityGraph.Validate"/> is what tells the author the operator is missing --
        /// which is better than refusing to add the node and saying nothing. </summary>
        private void AttachOperator(CityGraphNode node, CityNodeDescriptor descriptor)
        {
            Type type = ResolveOperatorType(descriptor.owner);
            if (type == null) { return; }

            ScriptableObject op = ScriptableObject.CreateInstance(type);
            if (op == null) { return; }

            op.name = descriptor.displayName;
            AssetDatabase.AddObjectToAsset(op, graph);
            Undo.RegisterCreatedObjectUndo(op, "Add city node");
            node.operatorAsset = op;
        }

        /// <summary> The operator class a descriptor's owner names. Looked up over the assembly
        /// <see cref="ICityOperator"/> itself lives in, because RCM's runtime and editor code are one assembly and every
        /// shipped operator is in it. </summary>
        private static Type ResolveOperatorType(string owner)
        {
            if (string.IsNullOrEmpty(owner)) { return null; }

            Type[] candidates = typeof(ICityOperator).Assembly.GetTypes();
            for (int i = 0; i < candidates.Length; i++)
            {
                Type candidate = candidates[i];
                if (candidate.IsAbstract || !string.Equals(candidate.Name, owner, StringComparison.Ordinal)) { continue; }
                if (!typeof(ScriptableObject).IsAssignableFrom(candidate)) { continue; }
                if (!typeof(ICityOperator).IsAssignableFrom(candidate)) { continue; }

                return candidate;
            }

            return null;
        }
        #endregion
    }


    /// <summary> B-15 / C-8. One node on the canvas.
    ///
    /// The ports are built from the catalogue's port sheet and keyed by NAME, because that is what
    /// <see cref="CityGraphEdge"/> stores: keying by index would move an author's wires the day a port is inserted above
    /// another.
    ///
    /// The last-run figures on the face are read, never computed here. C-6 writes them onto the node as it bakes, and a
    /// canvas that recomputed a timing would be reporting on a bake that never happened. </summary>
    public sealed class CityGraphNodeView : GVNode
    {
        #region "Node view state"
        private readonly Dictionary<string, GVPort> inputPorts = new Dictionary<string, GVPort>();
        private readonly Dictionary<string, GVPort> outputPorts = new Dictionary<string, GVPort>();

        /// <summary> The id of the node this view draws. The id and not the node, so nothing here can be mistaken for a
        /// second copy of the model. </summary>
        public string NodeId { get; private set; }
        #endregion


        #region "Building the face"
        public CityGraphNodeView(CityGraphNode node)
        {
            NodeId = node == null ? string.Empty : node.id;
            title = node == null ? "(missing)" : node.DisplayName();

            CityNodeDescriptor descriptor = null;
            string why;
            if (node != null) { CityNodeCatalog.TryGetForNode(node, out descriptor, out why); }

            AddStageBadge(node);
            BuildPorts(descriptor);
            AddBlockedNotice(descriptor);
            AddLastRun(node);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary> The stage the node runs in, on its title bar. Read from the node and not from the catalogue, because
        /// the node is what the baker walks -- a node whose stage was edited away from its entry's should look wrong here
        /// rather than look right and bake differently. </summary>
        private void AddStageBadge(CityGraphNode node)
        {
            BakeStage stage = node == null ? BakeStage.LoadOrGenerate : node.stage;

            Label badge = new Label(ObjectNames.NicifyVariableName(stage.ToString()));
            badge.style.unityTextAlign = TextAnchor.MiddleRight;
            badge.style.marginRight = 6f;
            badge.style.opacity = 0.65f;
            badge.style.fontSize = 9f;
            titleContainer.Add(badge);
        }

        /// <summary> The ports the catalogue declares, in sheet order. Every port is instantiated with the same CLR type:
        /// what may connect to what is decided by <see cref="CityGraph.CanConnect"/> over the sheet's payload strings, and
        /// a per-payload CLR type here would add a second, cruder rule that could refuse a wire the baker accepts.
        /// </summary>
        private void BuildPorts(CityNodeDescriptor descriptor)
        {
            if (descriptor == null) { return; }

            if (descriptor.inputs != null)
            {
                for (int i = 0; i < descriptor.inputs.Count; i++)
                {
                    string portName = descriptor.inputs[i].name;
                    if (string.IsNullOrEmpty(portName) || inputPorts.ContainsKey(portName)) { continue; }

                    GVPort port = InstantiatePort(Orientation.Horizontal, Direction.Input, GVPort.Capacity.Single,
                        typeof(float));
                    port.portName = portName;
                    port.tooltip = descriptor.inputs[i].payload;
                    inputContainer.Add(port);
                    inputPorts[portName] = port;
                }
            }

            if (descriptor.outputs == null) { return; }

            for (int i = 0; i < descriptor.outputs.Count; i++)
            {
                string portName = descriptor.outputs[i].name;
                if (string.IsNullOrEmpty(portName) || outputPorts.ContainsKey(portName)) { continue; }

                // Multi on the way out, Single on the way in: one result can feed several nodes, but an input that took
                // two wires would leave the baker choosing between them.
                GVPort port = InstantiatePort(Orientation.Horizontal, Direction.Output, GVPort.Capacity.Multi,
                    typeof(float));
                port.portName = portName;
                port.tooltip = descriptor.outputs[i].payload;
                outputContainer.Add(port);
                outputPorts[portName] = port;
            }
        }

        /// <summary> One port by name, or null. </summary>
        public GVPort FindPort(string portName, bool asInput)
        {
            if (string.IsNullOrEmpty(portName)) { return null; }

            GVPort port;
            Dictionary<string, GVPort> sheet = asInput ? inputPorts : outputPorts;
            return sheet.TryGetValue(portName, out port) ? port : null;
        }

        /// <summary> A blocked kind is drawn, labelled with the reason, rather than hidden. An author who placed one
        /// before it was blocked needs to see it to remove it, and one reading the catalogue needs to know the kind exists
        /// and why it does nothing yet. </summary>
        private void AddBlockedNotice(CityNodeDescriptor descriptor)
        {
            if (descriptor == null || !descriptor.IsBlocked) { return; }

            Label notice = new Label(string.IsNullOrEmpty(descriptor.blockedReason)
                ? "blocked"
                : "blocked: " + descriptor.blockedReason);
            notice.style.whiteSpace = WhiteSpace.Normal;
            notice.style.marginLeft = 6f;
            notice.style.marginRight = 6f;
            notice.style.marginTop = 2f;
            notice.style.color = new Color(0.95f, 0.65f, 0.3f, 1f);
            notice.style.fontSize = 10f;
            extensionContainer.Add(notice);
        }

        /// <summary> What the last bake measured on this node. Read from the node's own fields, which C-6 wrote as it ran
        /// it; -1 in both is the never-run sentinel. </summary>
        private void AddLastRun(CityGraphNode node)
        {
            if (node == null) { return; }

            Label figures = new Label();
            figures.style.marginLeft = 6f;
            figures.style.marginRight = 6f;
            figures.style.marginTop = 2f;
            figures.style.marginBottom = 2f;
            figures.style.fontSize = 10f;
            figures.style.opacity = 0.75f;
            figures.style.whiteSpace = WhiteSpace.Normal;

            if (node.lastRunMilliseconds < 0.0 && node.lastRunItemCount < 0)
            {
                figures.text = "not baked yet";
            }
            else
            {
                string note = string.IsNullOrEmpty(node.lastRunNote) ? string.Empty : "\n" + node.lastRunNote;
                figures.text = node.lastRunMilliseconds.ToString("0.0") + " ms, "
                               + node.lastRunItemCount + (node.lastRunItemCount == 1 ? " item" : " items") + note;
            }

            extensionContainer.Add(figures);
        }
        #endregion
    }


    /// <summary> B-15 / C-8. The Add node search tree.
    ///
    /// Three levels: the bake stage, then the catalogue's own <c>menuSection</c>, then the entries. Stage first so the
    /// tree reads in the order a city is built; section second because stage alone is unusable -- nearly every entry of
    /// the roughly 180 in the catalogue is <c>LoadOrGenerate</c>, and one flat list of 170 items is a list nobody reads.
    ///
    /// The sections are the manifest's own, not a second grouping invented here: an element the author added to a new
    /// section appears in that section on this canvas the moment the manifest is rebuilt. </summary>
    public sealed class CityGraphSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        #region "The tree"
        private CityGraphCanvas canvas;

        public void Bind(CityGraphCanvas target) { canvas = target; }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
            tree.Add(new SearchTreeGroupEntry(new GUIContent("Add city node"), 0));

            IReadOnlyList<CityNodeDescriptor> entries = CityNodeCatalog.Entries;
            BakeStage[] order = CityBakePipeline.CanonicalOrder;

            for (int s = 0; s < order.Length; s++)
            {
                BakeStage stage = order[s];
                List<string> sections = SectionsOf(entries, stage);
                if (sections.Count == 0) { continue; }

                tree.Add(new SearchTreeGroupEntry(new GUIContent(ObjectNames.NicifyVariableName(stage.ToString())), 1));

                for (int c = 0; c < sections.Count; c++)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(sections[c]), 2));

                    for (int e = 0; e < entries.Count; e++)
                    {
                        CityNodeDescriptor descriptor = entries[e];
                        if (descriptor == null || descriptor.stage != stage) { continue; }
                        if (!string.Equals(SectionOf(descriptor), sections[c], StringComparison.Ordinal)) { continue; }

                        SearchTreeEntry leaf = new SearchTreeEntry(new GUIContent(LabelOf(descriptor)));
                        leaf.level = 3;
                        leaf.userData = descriptor;
                        tree.Add(leaf);
                    }
                }
            }

            return tree;
        }

        /// <summary> The sections that have at least one entry in one stage, in catalogue order. </summary>
        private static List<string> SectionsOf(IReadOnlyList<CityNodeDescriptor> entries, BakeStage stage)
        {
            List<string> sections = new List<string>();
            if (entries == null) { return sections; }

            for (int i = 0; i < entries.Count; i++)
            {
                CityNodeDescriptor descriptor = entries[i];
                if (descriptor == null || descriptor.stage != stage) { continue; }

                string section = SectionOf(descriptor);
                if (!sections.Contains(section)) { sections.Add(section); }
            }

            return sections;
        }

        private static string SectionOf(CityNodeDescriptor descriptor)
        {
            return string.IsNullOrEmpty(descriptor.menuSection) ? "Other" : descriptor.menuSection;
        }

        /// <summary> A blocked entry is listed and labelled rather than left out: an author looking for a kind that is in
        /// the manual needs to find out here that it is blocked, not conclude the search box is broken. </summary>
        private static string LabelOf(CityNodeDescriptor descriptor)
        {
            string name = string.IsNullOrEmpty(descriptor.displayName) ? descriptor.kind.ToString() : descriptor.displayName;
            return descriptor.IsBlocked ? name + "  (blocked)" : name;
        }
        #endregion


        #region "Choosing an entry"
        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            CityNodeDescriptor descriptor = entry == null ? null : entry.userData as CityNodeDescriptor;
            if (descriptor == null || canvas == null) { return false; }

            canvas.AddNode(descriptor);
            return true;
        }
        #endregion
    }
}
#endif

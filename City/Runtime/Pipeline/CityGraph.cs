#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-2. What kind of value one node parameter holds. Five kinds, because five is what the node
    /// catalogue actually needs and a sixth would be a guess: a measurement, a key, a switch, a pair of metres, and a
    /// reference to another asset.
    ///
    /// The alternative -- one <c>string</c> holding a serialised blob -- was rejected on purpose. A blob cannot be
    /// drawn as a field with a unit and a band, which is the whole of C-10's rule, and a typo in one becomes a silent
    /// default at bake time rather than a refusal an author can read. </summary>
    public enum CityGraphParameterKind
    {
        /// <summary> A measurement or a count, held as a <c>double</c>. Integers up to 2^53 are exact in a double, so
        /// a lane count and a metre width do not need separate kinds. </summary>
        Number = 0,

        /// <summary> A key or a name -- a manifest category id, a zone id. Never a file path: an asset is
        /// <see cref="Asset"/>, so a moved file is Unity's problem to track and not a string that silently rots.
        /// </summary>
        Text = 1,

        /// <summary> An on/off decision. </summary>
        Flag = 2,

        /// <summary> A pair of numbers in one field: a band's low and high, or an XZ extent in metres. </summary>
        Vector = 3,

        /// <summary> A reference to another ASSET -- a catalogue, a prefab, a curve. Never to a scene object: this
        /// graph is an asset, Unity nulls a scene reference held by one on load, and the scene-side answers are
        /// <see cref="CityAuthoring"/>'s six, not a node's. </summary>
        Asset = 4
    }


    /// <summary> One authored parameter value on one node. It is per NODE and not per operator asset, because two
    /// nodes routinely wrap the same operator with different values -- one element-placement node placing benches and
    /// another placing bins is the case C-5's manifest-driven catalogue turns into 140 nodes and no new files.
    ///
    /// The kind is carried with the value and checked on the way out, so asking a text parameter for a number is a
    /// named refusal rather than a zero that reaches the ground as a real width. </summary>
    [Serializable]
    public struct CityGraphParameter
    {
        /// <summary> The port-side name the node's own code looks this value up by. </summary>
        public string name;

        /// <summary> Which of the five fields below carries the value. </summary>
        public CityGraphParameterKind kind;

        /// <summary> The value when <see cref="kind"/> is <see cref="CityGraphParameterKind.Number"/>. </summary>
        public double number;

        /// <summary> The value when <see cref="kind"/> is <see cref="CityGraphParameterKind.Text"/>. </summary>
        public string text;

        /// <summary> The value when <see cref="kind"/> is <see cref="CityGraphParameterKind.Flag"/>. </summary>
        public bool flag;

        /// <summary> The value when <see cref="kind"/> is <see cref="CityGraphParameterKind.Vector"/>. </summary>
        public Vector2 vector;

        /// <summary> The value when <see cref="kind"/> is <see cref="CityGraphParameterKind.Asset"/>. </summary>
        public UnityEngine.Object asset;

        /// <summary> A number parameter. </summary>
        public static CityGraphParameter OfNumber(string name, double value)
        {
            CityGraphParameter p = new CityGraphParameter();
            p.name = name; p.kind = CityGraphParameterKind.Number; p.number = value; p.text = string.Empty;
            return p;
        }

        /// <summary> A text parameter. </summary>
        public static CityGraphParameter OfText(string name, string value)
        {
            CityGraphParameter p = new CityGraphParameter();
            p.name = name; p.kind = CityGraphParameterKind.Text; p.text = value == null ? string.Empty : value;
            return p;
        }

        /// <summary> A flag parameter. </summary>
        public static CityGraphParameter OfFlag(string name, bool value)
        {
            CityGraphParameter p = new CityGraphParameter();
            p.name = name; p.kind = CityGraphParameterKind.Flag; p.flag = value; p.text = string.Empty;
            return p;
        }

        /// <summary> A two-number parameter. </summary>
        public static CityGraphParameter OfVector(string name, Vector2 value)
        {
            CityGraphParameter p = new CityGraphParameter();
            p.name = name; p.kind = CityGraphParameterKind.Vector; p.vector = value; p.text = string.Empty;
            return p;
        }

        /// <summary> An asset-reference parameter. </summary>
        public static CityGraphParameter OfAsset(string name, UnityEngine.Object value)
        {
            CityGraphParameter p = new CityGraphParameter();
            p.name = name; p.kind = CityGraphParameterKind.Asset; p.asset = value; p.text = string.Empty;
            return p;
        }

        /// <summary> Whether this parameter holds the kind being asked for, and if not, which kind it does hold. Every
        /// reader below goes through here, so a kind mismatch is one sentence written once. </summary>
        private bool KindIs(CityGraphParameterKind wanted, out string reason)
        {
            if (kind == wanted) { reason = string.Empty; return true; }
            reason = "the parameter named " + (string.IsNullOrEmpty(name) ? "(unnamed)" : name) + " holds a "
                     + kind + " value and a " + wanted + " one was asked for, so the node and the value it was"
                     + " authored with disagree about what this field is";
            return false;
        }

        /// <summary> The number, or a refusal naming the kind actually held. </summary>
        public bool TryGetNumber(out double value, out string reason)
        {
            value = number;
            return KindIs(CityGraphParameterKind.Number, out reason);
        }

        /// <summary> The text, or a refusal naming the kind actually held. </summary>
        public bool TryGetText(out string value, out string reason)
        {
            value = text == null ? string.Empty : text;
            return KindIs(CityGraphParameterKind.Text, out reason);
        }

        /// <summary> The flag, or a refusal naming the kind actually held. </summary>
        public bool TryGetFlag(out bool value, out string reason)
        {
            value = flag;
            return KindIs(CityGraphParameterKind.Flag, out reason);
        }

        /// <summary> The pair, or a refusal naming the kind actually held. </summary>
        public bool TryGetVector(out Vector2 value, out string reason)
        {
            value = vector;
            return KindIs(CityGraphParameterKind.Vector, out reason);
        }

        /// <summary> The referenced asset, or a refusal naming the kind actually held. A wired kind with a null
        /// reference still refuses, because a missing catalogue is not a default. </summary>
        public bool TryGetAsset(out UnityEngine.Object value, out string reason)
        {
            value = asset;
            if (!KindIs(CityGraphParameterKind.Asset, out reason)) { return false; }
            if (asset == null)
            {
                reason = "the asset parameter named " + (string.IsNullOrEmpty(name) ? "(unnamed)" : name)
                         + " references nothing, either because it was never wired or because the asset it pointed at"
                         + " was deleted";
                return false;
            }
            return true;
        }

        /// <summary> This parameter as one line for a report or a tooltip. </summary>
        public string Describe()
        {
            string head = (string.IsNullOrEmpty(name) ? "(unnamed)" : name) + " = ";
            switch (kind)
            {
                case CityGraphParameterKind.Number: return head + number.ToString("0.####");
                case CityGraphParameterKind.Text:   return head + (string.IsNullOrEmpty(text) ? "(empty)" : text);
                case CityGraphParameterKind.Flag:   return head + (flag ? "on" : "off");
                case CityGraphParameterKind.Vector: return head + vector.x.ToString("0.####") + " / "
                                                           + vector.y.ToString("0.####");
                case CityGraphParameterKind.Asset:  return head + (asset == null ? "(nothing)" : asset.name);
            }
            return head + "(unknown kind)";
        }
    }


    /// <summary> B-15 / C-2. One node on the graph: which stage it runs in, which operator asset runs it, what it was
    /// authored with, and where it sits on the canvas.
    ///
    /// The <see cref="id"/> is the identity, and edges reference ids rather than list indices ON PURPOSE. An edge
    /// holding an index would be rewired by deleting an earlier node -- the list shifts, the index still resolves, and
    /// the graph silently means something else. That failure leaves no error to read, which is the worst kind this
    /// register keeps finding, so the id is minted once by <see cref="CityGraph.MintId"/> and never reused.
    ///
    /// Nothing scene-specific is stored here. No Transform, no GameObject, no region: two cities in one scene run the
    /// same graph asset with different seeds and different regions, and a scene reference held by an asset is nulled
    /// by Unity on load anyway. The scene-side answers are <see cref="CityAuthoring"/>'s six. </summary>
    [Serializable]
    public sealed class CityGraphNode
    {
        /// <summary> The stable identity, a GUID string minted by <see cref="CityGraph.MintId"/> when the node is
        /// added and never rewritten afterwards. It orders NOTHING: execution order is the stage's position in
        /// <c>CityBakePipeline.CanonicalOrder</c> and, within one stage, the author's own list order -- so a bake is
        /// reproducible even though the id itself was minted from a GUID. </summary>
        public string id = string.Empty;

        /// <summary> The author's own label for this node, which is why it is per node and not read off the operator
        /// asset's name: <i>Place benches</i> and <i>Place bins</i> are two nodes wrapping one
        /// <c>ElementPlacementNode</c>, and a canvas that showed both as "ElementPlacementNode" would be unreadable.
        /// Empty means the window falls back to the operator asset's name. </summary>
        public string title = string.Empty;

        /// <summary> Which catalogue entry this node is: the identity C-5's <see cref="CityNodeCatalog"/> holds the
        /// ports, the parameters, the stage and the role for. It is the node's KIND and not a preference, which is why
        /// <see cref="CityNodeKind.Unassigned"/> is reported by <see cref="CityGraph.Validate"/> rather than treated as
        /// a node that quietly does nothing.
        ///
        /// It is stored and not derived from <see cref="operatorAsset"/>, because most kinds have no asset at all --
        /// a source node is filled by the runner and a port-sheet node calls shipped code, and asking the asset would
        /// leave both unidentifiable. An element placement node's kind is <see cref="CityNodeKind.ElementPlacement"/>
        /// for all 140 of them; WHICH category it places is the <c>category</c> parameter, so the kind stays a small
        /// enum and the manifest stays the one list of categories. </summary>
        public CityNodeKind kind = CityNodeKind.Unassigned;

        /// <summary> Which bake stage this node runs in. It is stored rather than looked up on every read so that a
        /// graph written against a later build still says where its nodes were meant to run, and it is validated rather
        /// than trusted: <see cref="CityGraph.Validate"/> refuses an edge that runs backwards through
        /// <c>CityBakePipeline.CanonicalOrder</c>, and refuses a node whose stage disagrees with the stage its
        /// <see cref="kind"/> declares in the catalogue. That second check is C-5's, and it replaced the older comment
        /// here which said the stage was free to differ: it is not, and the reason is
        /// <c>CityBakePipeline</c>'s own -- a node that publishes footprints is useless in the stage after the terrain
        /// has already been conformed to them. <see cref="CityNodeCatalog.CreateNode"/> writes this field from the
        /// catalogue, so a node dropped from the search window is right without the author choosing. </summary>
        public BakeStage stage = BakeStage.LoadOrGenerate;

        /// <summary> The operator asset this node runs. Typed as <c>ScriptableObject</c> and not as
        /// <c>ICityOperator</c> because Unity serialises an asset reference and not an interface; read it through
        /// <see cref="TryGetOperator"/>, which is where the cast and its refusal live. </summary>
        public ScriptableObject operatorAsset;

        /// <summary> What this node was authored with. Per node, so two nodes may share an operator asset and differ
        /// only here -- which is how C-5's 140 element categories become 140 nodes and no new files. </summary>
        public List<CityGraphParameter> parameters = new List<CityGraphParameter>();

        /// <summary> Where the node sits on C-8's canvas, IN THE ASSET rather than in editor-session state. Session
        /// state does not survive a domain reload, and B-25 is the entry that records what that costs: a layout an
        /// author arranged and a script recompile that scattered it. Serialising it here means the arrangement is part
        /// of the graph, saved with it, and reviewable in a diff. </summary>
        public Vector2 canvasPosition;

        /// <summary> How long this node took on the last bake, in milliseconds. WRITTEN BY C-6's runner, read by C-8's
        /// window; it is not an author's field and C-8 draws it read-only. Negative means never run. </summary>
        public double lastRunMilliseconds = -1.0;

        /// <summary> How many items this node produced on the last bake -- lots, buildings, placements. Written by
        /// C-6's runner, read by C-8's window. Negative means never run. </summary>
        public int lastRunItemCount = -1;

        /// <summary> What the last bake had to say about this node: empty when it simply ran, otherwise the reason it
        /// was skipped or refused, in the runner's own words so the window does not have to invent one. </summary>
        public string lastRunNote = string.Empty;

        /// <summary> The label to draw: the author's <see cref="title"/> if there is one, the operator asset's name if
        /// not, and the id's first characters if there is neither. </summary>
        public string DisplayName()
        {
            if (!string.IsNullOrEmpty(title)) { return title; }
            if (operatorAsset != null) { return operatorAsset.name; }
            return string.IsNullOrEmpty(id) ? "(unidentified node)" : "node " + id;
        }

        /// <summary> The operator this node runs, or a refusal that names why it cannot be run: no asset wired, or an
        /// asset that is not an operator at all. Both are real -- an author can drag any ScriptableObject into an
        /// object field, and a deleted operator asset leaves the reference null. </summary>
        public bool TryGetOperator(out ICityOperator op, out string reason)
        {
            op = null;
            if (operatorAsset == null)
            {
                reason = "the node " + DisplayName() + " wires no operator asset, so there is nothing to run it and its"
                         + " ports have no types to check against the nodes it is joined to";
                return false;
            }
            op = operatorAsset as ICityOperator;
            if (op == null)
            {
                reason = "the node " + DisplayName() + " wires " + operatorAsset.name + ", which is a "
                         + operatorAsset.GetType().Name + " and not a city operator, so it declares neither an input"
                         + " nor an output type";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> The named parameter, or a refusal saying it was never authored on this node. Callers get a named
        /// absence rather than a default-constructed value that would read as a real zero. </summary>
        public bool TryGetParameter(string parameterName, out CityGraphParameter parameter, out string reason)
        {
            parameter = new CityGraphParameter();
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].name == parameterName)
                    {
                        parameter = parameters[i];
                        reason = string.Empty;
                        return true;
                    }
                }
            }
            reason = "the node " + DisplayName() + " carries no parameter named " + parameterName
                     + ", so the value it would have been run with was never authored";
            return false;
        }
    }


    /// <summary> B-15 / C-2. One wire: an output port on one node joined to an input port on another. Four strings and
    /// nothing else -- no cached type, no cached direction, no cached stage. Anything cached here would be a second
    /// copy of what the two nodes already say, free to disagree with them the moment an author changes one.
    ///
    /// The node ends are IDS, never list indices, for the reason <see cref="CityGraphNode.id"/> gives. </summary>
    [Serializable]
    public sealed class CityGraphEdge
    {
        /// <summary> The id of the node the value comes from. </summary>
        public string fromNodeId = string.Empty;

        /// <summary> The name of the output port on the from-node. Names rather than indices, so reordering an
        /// operator's ports cannot silently move a wire. </summary>
        public string fromPort = string.Empty;

        /// <summary> The id of the node the value goes to. </summary>
        public string toNodeId = string.Empty;

        /// <summary> The name of the input port on the to-node. </summary>
        public string toPort = string.Empty;

        /// <summary> This edge as one line for a report. </summary>
        public string Describe()
        {
            return (string.IsNullOrEmpty(fromNodeId) ? "(nothing)" : fromNodeId) + "."
                   + (string.IsNullOrEmpty(fromPort) ? "(unnamed port)" : fromPort) + " -> "
                   + (string.IsNullOrEmpty(toNodeId) ? "(nothing)" : toNodeId) + "."
                   + (string.IsNullOrEmpty(toPort) ? "(unnamed port)" : toPort);
        }
    }

    /// <summary> B-15 / C-2. The graph an author wires and a bake runs: a list of nodes, a list of edges, and the
    /// verdict on whether the two of them make sense together.
    ///
    /// It is an ASSET and holds nothing scene-specific, which is what lets one graph drive every settlement in a
    /// region -- the same wiring, a different seed and a different rect per city, from <see cref="CityAuthoring"/>'s
    /// six scene-side answers. A graph that held a Transform or a region would be a graph per city.
    ///
    /// <see cref="Validate"/> lives HERE, on the asset, and not in the editor assembly. C-6's runner has to be able to
    /// refuse a broken graph in batchmode, where no editor window has ever been opened, and a validator behind an
    /// editor-only wall would leave the headless path either trusting the graph or growing a second copy of the checks.
    /// One validator, callable from both.
    ///
    /// The canvas positions are serialised here too, for the reason <see cref="CityGraphNode.canvasPosition"/> gives:
    /// B-25 records what session-only state costs when a domain reload discards it. </summary>
    [CreateAssetMenu(fileName = "CityGraph", menuName = "Road City Master/City Graph", order = 3)]
    public sealed class CityGraph : ScriptableObject
    {
        /// <summary> The schema this asset was written with, so a later reader can tell an old graph from a broken one
        /// rather than silently misreading it. </summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary> Which schema version this asset carries. </summary>
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary> The nodes, in the author's own order. Within one <see cref="BakeStage"/> this list order IS the
        /// execution order, which is why it is an ordered list and not a set: two placement nodes in one stage have to
        /// run in a decided sequence for the bake to be reproducible. </summary>
        public List<CityGraphNode> nodes = new List<CityGraphNode>();

        /// <summary> The wires. </summary>
        public List<CityGraphEdge> edges = new List<CityGraphEdge>();

        /// <summary> A fresh node id. A GUID, minted once when a node is added and never reused, so that deleting a
        /// node cannot hand its identity to another one and quietly inherit its wires. Nothing in a bake depends on the
        /// VALUE -- see <see cref="CityGraphNode.id"/> -- so this is authoring identity and not a random number
        /// entering the generator. </summary>
        public static string MintId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary> Where the node with this id sits in <see cref="nodes"/>, or -1. </summary>
        public int IndexOfNode(string id)
        {
            if (string.IsNullOrEmpty(id) || nodes == null) { return -1; }
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].id == id) { return i; }
            }
            return -1;
        }

        /// <summary> The node with this id, or false. </summary>
        public bool TryGetNode(string id, out CityGraphNode node)
        {
            int index = IndexOfNode(id);
            node = index < 0 ? null : nodes[index];
            return node != null;
        }

        /// <summary> Where a stage sits in <c>CityBakePipeline.CanonicalOrder</c>, or -1 when it is not in it at all.
        /// The canonical array is asked rather than the enum cast to an int, because the ORDER is what
        /// <c>CityBakePipeline</c> owns; reading the underlying number here would be a second copy of it that a
        /// renumbered enum could put out of step. </summary>
        public static int StageIndex(BakeStage stage)
        {
            BakeStage[] order = CityBakePipeline.CanonicalOrder;
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] == stage) { return i; }
            }
            return -1;
        }

        /// <summary> Whether this graph can be run, and every reason it cannot -- all of them, not the first, because
        /// an author fixing one problem per bake attempt is the workflow this is meant to replace.
        ///
        /// Four things are asserted, and they are the four the register names: every edge's endpoints exist; every wire
        /// type-matches through the operators' <c>InputType</c>/<c>OutputType</c>; no cycle; and every edge runs FORWARD
        /// through <c>CityBakePipeline.CanonicalOrder</c>, since a wire from a later stage to an earlier one asks for a
        /// value that will not exist when it is read. Alongside them are the structural ones without which the four
        /// cannot be checked at all -- a node with no id, two nodes with the same id, a node whose asset is not an
        /// operator, one input port fed twice.
        ///
        /// A fifth family joined them with C-5, and it is a family rather than a fifth item because it is one question
        /// asked of every node and every wire: does this agree with the catalogue? Each node is resolved against
        /// <see cref="CityNodeCatalog"/> by its <see cref="CityGraphNode.kind"/>, and a node the catalogue does not know,
        /// a node in a stage its kind does not run in, a node missing the operator asset its kind requires, a port name
        /// no side declares and a wire touching a node that does not execute are all reported. The reason it belongs
        /// here rather than in the window that creates nodes is that a graph outlives the window: the asset can be
        /// hand-edited, merged, or written by an older build, and validation is the only place that reads it again.
        ///
        /// It reads and reports. It never repairs: a validator that quietly fixed a graph would be deciding what the
        /// author meant. </summary>
        public bool Validate(out string report)
        {
            List<string> problems = new List<string>();
            int nodeCount = nodes == null ? 0 : nodes.Count;
            int edgeCount = edges == null ? 0 : edges.Count;

            CheckNodes(problems);
            CheckEdges(problems);
            CheckForCycles(problems);

            StringBuilder builder = new StringBuilder();
            builder.Append("the city graph ").Append(string.IsNullOrEmpty(name) ? "(unnamed)" : name)
                   .Append(" holds ").Append(nodeCount).Append(" node").Append(nodeCount == 1 ? "" : "s")
                   .Append(" and ").Append(edgeCount).Append(" edge").Append(edgeCount == 1 ? "" : "s");
            if (schemaVersion != CurrentSchemaVersion)
            {
                problems.Add("the asset was written with schema version " + schemaVersion + " and this build reads"
                             + " version " + CurrentSchemaVersion + ", so it is an older or a newer graph rather than"
                             + " a broken one");
            }
            if (problems.Count == 0)
            {
                builder.Append(", every node matches a catalogue entry it agrees with, and every edge joins two nodes"
                               + " that exist, names ports they both have, type-matches, and runs forward through the"
                               + " bake stages with no cycle");
                report = builder.ToString();
                return true;
            }
            builder.Append(", and cannot be run:");
            for (int i = 0; i < problems.Count; i++)
            {
                builder.Append("\n  ").Append(i + 1).Append(". ").Append(problems[i]);
            }
            report = builder.ToString();
            return false;
        }

        /// <summary> B-15 / C-8. Whether one wire could be added to this graph, and the first reason it could not.
        ///
        /// The window needs this WHILE a wire is being dragged: a port that refuses an illegal connection under the
        /// pointer is a rule the author learns in one gesture, and a graph that accepts it and fails one bake later is a
        /// rule they learn from a log. What it must not be is a second copy of the rules -- so this runs the same five
        /// per-edge checks <see cref="CheckEdges"/> runs, on a candidate edge that is not in the list yet, and returns
        /// their first complaint. A rule added to a check below is enforced here the day it is added.
        ///
        /// The cycle question is asked differently and cannot be otherwise: <see cref="CheckForCycles"/> answers "does
        /// this graph contain a loop", and what is wanted here is "would this wire make one". Those have the same answer
        /// exactly when the far end can already reach the near end, so that is what is walked. Following existing edges
        /// only, since the candidate is the edge being asked about. </summary>
        public bool CanConnect(CityGraphEdge candidate, out string reason)
        {
            reason = string.Empty;
            if (candidate == null) { reason = "there is no wire to add"; return false; }

            List<string> problems = new List<string>();
            CityGraphNode from, to;
            bool haveFrom = TryGetNode(candidate.fromNodeId, out from);
            bool haveTo = TryGetNode(candidate.toNodeId, out to);

            if (!haveFrom || !haveTo)
            {
                reason = "one end of the wire is not a node in this graph";
                return false;
            }
            if (string.IsNullOrEmpty(candidate.fromPort) || string.IsNullOrEmpty(candidate.toPort))
            {
                reason = "one end of the wire names no port";
                return false;
            }
            if (from == to)
            {
                reason = from.DisplayName() + " cannot feed itself: that asks for its own output before it has run";
                return false;
            }

            if (edges != null) { CheckEdgeIsNotADuplicate(edges.Count, candidate, problems); }
            CheckEdgeEndsExecute(candidate, from, to, problems);
            CheckEdgePortNames(candidate, from, to, problems);
            CheckEdgePortTypes(candidate, from, to, problems);
            CheckEdgeRunsForward(candidate, from, to, problems);

            if (problems.Count == 0 && Reaches(candidate.toNodeId, candidate.fromNodeId))
            {
                problems.Add("the wire would close a loop: " + to.DisplayName() + " already feeds "
                             + from.DisplayName() + " through the graph, and a bake walks it once and forwards");
            }

            if (problems.Count == 0) { return true; }
            reason = problems[0];
            return false;
        }

        /// <summary> Whether following existing edges from one node ever arrives at another. Breadth-first over node
        /// indices, visited-marked, so a graph that already holds a loop is walked once rather than for ever -- this is
        /// asked while a wire is being dragged, and a graph with an existing fault must still answer. </summary>
        private bool Reaches(string startNodeId, string targetNodeId)
        {
            if (nodes == null || edges == null) { return false; }
            int start = IndexOfNode(startNodeId);
            int target = IndexOfNode(targetNodeId);
            if (start < 0 || target < 0) { return false; }
            if (start == target) { return true; }

            bool[] seen = new bool[nodes.Count];
            List<int> frontier = new List<int>();
            frontier.Add(start);
            seen[start] = true;

            while (frontier.Count > 0)
            {
                int at = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                for (int e = 0; e < edges.Count; e++)
                {
                    CityGraphEdge edge = edges[e];
                    if (edge == null) { continue; }
                    if (IndexOfNode(edge.fromNodeId) != at) { continue; }
                    int next = IndexOfNode(edge.toNodeId);
                    if (next < 0 || seen[next]) { continue; }
                    if (next == target) { return true; }
                    seen[next] = true;
                    frontier.Add(next);
                }
            }
            return false;
        }

        /// <summary> The node-side checks: an id to reference, a unique one, a stage the pipeline knows, agreement with
        /// the catalogue entry the node's kind names, and no parameter named twice.
        ///
        /// The operator demand became CONDITIONAL with C-5, and that is the one behavioural change here. Before it,
        /// every node was asked for an operator asset, which was right while every node was an operator and wrong the
        /// moment the catalogue named sources, shipped-code nodes and comments: a terrain source is filled by the runner
        /// and a comment runs nothing, so demanding an asset of either would refuse a graph that is correct.
        /// <see cref="CityNodeCatalog.NeedsOperatorAsset"/> is the single place that rule lives, and it is asked here
        /// rather than answered here. </summary>
        private void CheckNodes(List<string> problems)
        {
            if (nodes == null) { return; }
            for (int i = 0; i < nodes.Count; i++)
            {
                CityGraphNode node = nodes[i];
                if (node == null)
                {
                    problems.Add("slot " + i + " in the node list is empty, which happens when a node is deleted by"
                                 + " clearing the slot instead of removing it -- remove the row");
                    continue;
                }
                if (string.IsNullOrEmpty(node.id))
                {
                    problems.Add("the node at slot " + i + " (" + node.DisplayName() + ") carries no id, so no edge can"
                                 + " reference it and nothing can be wired to it. An id is minted by CityGraph.MintId"
                                 + " when the node is added");
                }
                else
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (nodes[j] != null && nodes[j].id == node.id)
                        {
                            problems.Add("the nodes at slots " + j + " and " + i + " (" + nodes[j].DisplayName()
                                         + " and " + node.DisplayName() + ") share the id " + node.id + ", so an edge"
                                         + " naming it means either of them. Ids are minted once and never copied, so"
                                         + " this is a duplicated node rather than a coincidence");
                            break;
                        }
                    }
                }
                if (StageIndex(node.stage) < 0)
                {
                    problems.Add("the node " + node.DisplayName() + " runs in the stage " + node.stage + ", which is"
                                 + " not one of the stages CityBakePipeline.CanonicalOrder names, so there is no point"
                                 + " in the bake at which it would run");
                }
                ICityOperator op;
                string why;
                if (CityNodeCatalog.NeedsOperatorAsset(node.kind))
                {
                    if (!node.TryGetOperator(out op, out why)) { problems.Add(why); }
                }
                else if (node.operatorAsset != null)
                {
                    problems.Add("the node " + node.DisplayName() + " wires the operator asset "
                                 + node.operatorAsset.name + ", but a node of the kind " + node.kind + " runs shipped"
                                 + " code or is filled by the runner and never reads one, so the asset is an authored"
                                 + " value that does nothing -- clear the field, or use the node kind that does run an"
                                 + " operator asset");
                }
                CheckNodeAgainstCatalogue(node, problems);
                CheckParameterNames(node, problems);
            }
        }

        /// <summary> Every parameter on one node has a name, and no name appears twice -- a second parameter under the
        /// same name is a value an author typed that nothing will ever read. </summary>
        private static void CheckParameterNames(CityGraphNode node, List<string> problems)
        {
            if (node.parameters == null) { return; }
            for (int i = 0; i < node.parameters.Count; i++)
            {
                string parameterName = node.parameters[i].name;
                if (string.IsNullOrEmpty(parameterName))
                {
                    problems.Add("the node " + node.DisplayName() + " carries an unnamed parameter at slot " + i
                                 + ", which no operator can look up, so whatever was authored into it is unreachable");
                    continue;
                }
                for (int j = 0; j < i; j++)
                {
                    if (node.parameters[j].name == parameterName)
                    {
                        problems.Add("the node " + node.DisplayName() + " carries two parameters named "
                                     + parameterName + " (slots " + j + " and " + i + "), and only the first would"
                                     + " ever be read -- the second is an authored value that silently does nothing");
                        break;
                    }
                }
            }
        }

        /// <summary> One node against the catalogue entry its <see cref="CityGraphNode.kind"/> names: that the entry
        /// exists at all, that it is not an entry still waiting on work that does not exist, and that the node's stage is
        /// the stage that entry runs in.
        ///
        /// The stage check is the one worth a reason, because a wrong stage does not look like a bug. Every node runs,
        /// every one produces what it should, and the city still comes out wrong: a footprint published in the stage
        /// AFTER the terrain was conformed to footprints conforms nothing, and a scatter baked before the terrain moved
        /// is scattered on ground that no longer exists. That is the "individually correct, collectively wrong" failure
        /// <c>CityBakePipeline</c>'s own summary names, and the only cheap place to catch it is here, before a bake
        /// rather than in a screenshot after one.
        ///
        /// A stage the pipeline does not know AT ALL is reported by the caller, so this method stays quiet about it --
        /// two complaints about one field would leave an author fixing it twice. </summary>
        private static void CheckNodeAgainstCatalogue(CityGraphNode node, List<string> problems)
        {
            CityNodeDescriptor descriptor;
            string reason;
            if (!CityNodeCatalog.TryGetForNode(node, out descriptor, out reason))
            {
                problems.Add(reason);
                return;
            }
            if (descriptor.IsBlocked)
            {
                problems.Add("the node " + node.DisplayName() + " is a " + descriptor.displayName + " node, which"
                             + " cannot run yet: " + descriptor.blockedReason);
            }
            if (descriptor.stage != node.stage && StageIndex(node.stage) >= 0)
            {
                problems.Add("the node " + node.DisplayName() + " is authored into the " + node.stage + " stage, and a "
                             + descriptor.displayName + " node runs in the " + descriptor.stage + " stage. It would run,"
                             + " and report success, at the wrong point in the bake -- so this is reported here rather"
                             + " than left to be found in a city that is wrong for no visible reason");
            }
        }

        /// <summary> The edge-side checks: both ends resolve, both ports are named, both port names are ports the
        /// catalogue says those nodes HAVE, both ends are nodes that execute, nothing feeds itself, no wire is
        /// duplicated, and no input port is fed twice. Type-matching and stage direction follow in their own methods,
        /// and only for the edges whose ends were found -- reporting a type mismatch on an edge that points at nothing
        /// would be a second complaint about one problem. </summary>
        private void CheckEdges(List<string> problems)
        {
            if (edges == null) { return; }
            for (int i = 0; i < edges.Count; i++)
            {
                CityGraphEdge edge = edges[i];
                if (edge == null)
                {
                    problems.Add("slot " + i + " in the edge list is empty -- remove the row rather than clearing it");
                    continue;
                }
                CityGraphNode from, to;
                bool haveFrom = TryGetNode(edge.fromNodeId, out from);
                bool haveTo = TryGetNode(edge.toNodeId, out to);
                if (!haveFrom)
                {
                    problems.Add("the edge " + edge.Describe() + " comes from the node id " + edge.fromNodeId
                                 + ", which no node in this graph carries. Either the node it came from was deleted and"
                                 + " left the wire behind, or the id was edited by hand");
                }
                if (!haveTo)
                {
                    problems.Add("the edge " + edge.Describe() + " goes to the node id " + edge.toNodeId
                                 + ", which no node in this graph carries");
                }
                if (string.IsNullOrEmpty(edge.fromPort) || string.IsNullOrEmpty(edge.toPort))
                {
                    problems.Add("the edge " + edge.Describe() + " leaves one of its two port names blank, so there is"
                                 + " no port for the value to leave by or arrive at");
                }
                if (haveFrom && haveTo && from == to)
                {
                    problems.Add("the edge " + edge.Describe() + " joins the node " + from.DisplayName() + " to itself,"
                                 + " which asks for its own output before it has run");
                }
                CheckEdgeIsNotADuplicate(i, edge, problems);
                if (!haveFrom || !haveTo) { continue; }
                CheckEdgeEndsExecute(edge, from, to, problems);
                CheckEdgePortNames(edge, from, to, problems);
                CheckEdgePortTypes(edge, from, to, problems);
                CheckEdgeRunsForward(edge, from, to, problems);
            }
        }

        /// <summary> No wire is laid twice, and no input port is fed by two wires. The second is the substantive one: an
        /// operator reads ONE input, so two edges arriving at one port make the value delivered depend on which edge the
        /// runner happens to visit first -- a graph that bakes differently for a reason nothing in it records. </summary>
        private void CheckEdgeIsNotADuplicate(int index, CityGraphEdge edge, List<string> problems)
        {
            for (int j = 0; j < index; j++)
            {
                CityGraphEdge earlier = edges[j];
                if (earlier == null) { continue; }
                if (earlier.toNodeId != edge.toNodeId || earlier.toPort != edge.toPort) { continue; }
                if (string.IsNullOrEmpty(edge.toNodeId) || string.IsNullOrEmpty(edge.toPort)) { continue; }
                if (earlier.fromNodeId == edge.fromNodeId && earlier.fromPort == edge.fromPort)
                {
                    problems.Add("the edge " + edge.Describe() + " is wired twice, at slots " + j + " and " + index
                                 + ", so one value would be delivered to one port twice");
                    return;
                }
                problems.Add("two edges arrive at the same input port: " + earlier.Describe() + " and "
                             + edge.Describe() + ". An operator reads one input, so which of the two it gets would"
                             + " depend on the order the runner walked the edge list in");
                return;
            }
        }

        /// <summary> A wire may only touch nodes that run. A comment group and a comment note are canvas-only -- they
        /// exist to tell a human something -- so an edge into or out of one carries a value nothing will ever produce or
        /// read. It is reported rather than ignored because an author who wired it believed it did something.
        /// </summary>
        private static void CheckEdgeEndsExecute(CityGraphEdge edge, CityGraphNode from, CityGraphNode to,
            List<string> problems)
        {
            if (!CityNodeCatalog.Executes(from.kind))
            {
                problems.Add("the edge " + edge.Describe() + " leaves " + from.DisplayName() + ", which is a note on the"
                             + " canvas rather than a node that runs, so it produces no value for the wire to carry");
            }
            if (!CityNodeCatalog.Executes(to.kind))
            {
                problems.Add("the edge " + edge.Describe() + " arrives at " + to.DisplayName() + ", which is a note on"
                             + " the canvas rather than a node that runs, so nothing would ever read what it delivers");
            }
        }

        /// <summary> Both port names name ports those two nodes actually HAVE, asked of C-5's catalogue because that is
        /// the only place a node's ports are written down. This is the check the graph could not make at all before the
        /// catalogue existed: a wire could name any string, validation had nothing to compare it to, and a mistyped port
        /// therefore passed every check here and failed one bake later as a value that never arrived.
        ///
        /// The direction is checked with the name, since a wire leaving an INPUT port is as wrong as one naming a port
        /// that does not exist. A blank name is passed over -- <see cref="CheckEdges"/> has already reported it -- and so
        /// is a node whose entry cannot be resolved, which <see cref="CheckNodeAgainstCatalogue"/> has.
        ///
        /// When BOTH ends declare the named port, their declared PAYLOADS are compared too, and that comparison is the
        /// authority for such a wire rather than the operators' CLR types -- see
        /// <see cref="CheckEdgePortTypes"/> for why the two answers differ and which one is right where. </summary>
        private static void CheckEdgePortNames(CityGraphEdge edge, CityGraphNode from, CityGraphNode to,
            List<string> problems)
        {
            CityNodeDescriptor fromEntry, toEntry;
            string why;
            bool haveFromEntry = CityNodeCatalog.TryGetForNode(from, out fromEntry, out why);
            bool haveToEntry = CityNodeCatalog.TryGetForNode(to, out toEntry, out why);
            string leaves = string.Empty, arrives = string.Empty;
            bool knowLeaves = false, knowArrives = false;
            if (haveFromEntry && !string.IsNullOrEmpty(edge.fromPort))
            {
                knowLeaves = fromEntry.TryGetPortPayload(edge.fromPort, false, out leaves);
                if (!knowLeaves)
                {
                    problems.Add("the edge " + edge.Describe() + " leaves " + from.DisplayName() + " by an output port"
                                 + " named " + edge.fromPort + ", which that node does not have. Its ports are: "
                                 + fromEntry.PortLine());
                }
            }
            if (haveToEntry && !string.IsNullOrEmpty(edge.toPort))
            {
                knowArrives = toEntry.TryGetPortPayload(edge.toPort, true, out arrives);
                if (!knowArrives)
                {
                    problems.Add("the edge " + edge.Describe() + " arrives at " + to.DisplayName() + " on an input port"
                                 + " named " + edge.toPort + ", which that node does not have. Its ports are: "
                                 + toEntry.PortLine());
                }
            }
            if (knowLeaves && knowArrives && leaves != arrives
                && !string.IsNullOrEmpty(leaves) && !string.IsNullOrEmpty(arrives))
            {
                problems.Add("the edge " + edge.Describe() + " carries a " + leaves + " out of " + from.DisplayName()
                             + " into a port that reads a " + arrives + ", so the value would arrive as something the"
                             + " node on the far end does not read");
            }
        }

        /// <summary> The value leaving one node is a value the next one can read, asked of the operators themselves
        /// through <c>OutputType</c> and <c>InputType</c> rather than of the port names, because a name is a label an
        /// author chose and a type is what the code will actually be handed. Assignability, not equality: an operator
        /// reading a base type legitimately accepts a derived one.
        ///
        /// A node with no operator asset is passed over here, and since C-5 that is the NORMAL case rather than a fault
        /// being kept quiet: a source node and a shipped-code node are correct with no asset, so there is no operator to
        /// ask for a type and nothing to report. Those wires are not left unchecked -- <see cref="CheckEdgePortNames"/>
        /// checks them against the catalogue's declared payload names instead, which is the only thing that describes
        /// them. Where an asset IS required and missing, <see cref="CheckNodes"/> has already said so, and one problem
        /// should produce one line.
        ///
        /// An edge whose two ends BOTH declare the named port is passed over here as well, and that is the one rule in
        /// this file worth reading twice, because it looks like a check being dropped and is not. What travels a wire is
        /// the port's payload; an operator's <c>InputType</c> is the whole struct the RUNNER assembles around that
        /// payload. <c>BlockSubdivisionOperator</c> reads a <c>BlockSubdivisionInput</c> and the wire that feeds it
        /// carries a <c>StreetNetwork</c> -- the network is a FIELD of that input, and the runner fills the rest. Asking
        /// CLR assignability there refuses the single wire the default graph is supposed to have, which is why
        /// <c>CitySettlementFactory</c> could not wire its own two nodes before C-5 and says so in its own summary. So
        /// the payload comparison is the authority where the catalogue describes both ends, and this method is the
        /// authority where it does not -- an older graph, or a kind this build no longer knows. </summary>
        private static void CheckEdgePortTypes(CityGraphEdge edge, CityGraphNode from, CityGraphNode to,
            List<string> problems)
        {
            ICityOperator fromOperator, toOperator;
            string why;
            if (BothEndsDeclareThePorts(edge, from, to)) { return; }
            if (!from.TryGetOperator(out fromOperator, out why)) { return; }
            if (!to.TryGetOperator(out toOperator, out why)) { return; }
            Type produced = fromOperator.OutputType;
            Type wanted = toOperator.InputType;
            if (produced == null || wanted == null)
            {
                problems.Add("the edge " + edge.Describe() + " joins " + from.DisplayName() + " to " + to.DisplayName()
                             + ", and one of the two declares no port type, so there is nothing to check the wire"
                             + " against");
                return;
            }
            if (!wanted.IsAssignableFrom(produced))
            {
                problems.Add("the edge " + edge.Describe() + " carries a " + produced.Name + " out of "
                             + from.DisplayName() + " into " + to.DisplayName() + ", which reads a " + wanted.Name
                             + ". The two do not match, so the value would arrive as something the operator cannot"
                             + " use");
            }
        }

        /// <summary> Whether the catalogue describes both ends of this wire by name, which is the condition under which
        /// the payload comparison in <see cref="CheckEdgePortNames"/> has already answered the question and the CLR type
        /// comparison must not answer it a second time with a different answer. </summary>
        private static bool BothEndsDeclareThePorts(CityGraphEdge edge, CityGraphNode from, CityGraphNode to)
        {
            CityNodeDescriptor fromEntry, toEntry;
            string why;
            if (!CityNodeCatalog.TryGetForNode(from, out fromEntry, out why)) { return false; }
            if (!CityNodeCatalog.TryGetForNode(to, out toEntry, out why)) { return false; }
            return fromEntry.DeclaresPort(edge.fromPort, false) && toEntry.DeclaresPort(edge.toPort, true);
        }

        /// <summary> Every edge runs forward through <c>CityBakePipeline.CanonicalOrder</c>. Two nodes in the SAME stage
        /// may be wired to each other -- that is an ordinary chain within a stage, resolved by the author's list order --
        /// but a wire from a later stage into an earlier one asks for a value the bake will not have produced yet. That
        /// is the failure <c>CityBakePipeline</c>'s own doc describes: individually correct, collectively wrong.
        ///
        /// A stage the pipeline does not name is passed over, since <see cref="CheckNodes"/> has already said so.
        /// </summary>
        private static void CheckEdgeRunsForward(CityGraphEdge edge, CityGraphNode from, CityGraphNode to,
            List<string> problems)
        {
            int fromIndex = StageIndex(from.stage);
            int toIndex = StageIndex(to.stage);
            if (fromIndex < 0 || toIndex < 0) { return; }
            if (fromIndex > toIndex)
            {
                problems.Add("the edge " + edge.Describe() + " runs backwards through the bake: " + from.DisplayName()
                             + " runs in " + from.stage + " and " + to.DisplayName() + " runs in " + to.stage
                             + ", which is earlier, so the value would be read before the stage that produces it has"
                             + " run");
            }
        }

        /// <summary> No cycle, by peeling off the nodes that nothing feeds until none is left to peel -- Kahn's
        /// topological sort, counting rather than sorting, since only the verdict is wanted here. Whatever remains is
        /// held by a loop, and those are the nodes named in the report: an author who wired a loop needs to know WHICH
        /// nodes are in it, not that one exists somewhere.
        ///
        /// Self-edges are left to <see cref="CheckEdges"/>, and edges with an end that resolves to nothing are skipped
        /// -- both are already reported, and counting them here would turn one mistake into two lines. </summary>
        private void CheckForCycles(List<string> problems)
        {
            if (nodes == null || edges == null || nodes.Count == 0) { return; }
            int count = nodes.Count;
            int[] waitingOn = new int[count];
            List<int>[] feeds = new List<int>[count];
            for (int i = 0; i < count; i++) { feeds[i] = new List<int>(); }
            for (int e = 0; e < edges.Count; e++)
            {
                CityGraphEdge edge = edges[e];
                if (edge == null) { continue; }
                int fromIndex = IndexOfNode(edge.fromNodeId);
                int toIndex = IndexOfNode(edge.toNodeId);
                if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) { continue; }
                feeds[fromIndex].Add(toIndex);
                waitingOn[toIndex]++;
            }
            List<int> ready = new List<int>();
            for (int i = 0; i < count; i++) { if (waitingOn[i] == 0) { ready.Add(i); } }
            int settled = 0;
            while (ready.Count > 0)
            {
                int at = ready[ready.Count - 1];
                ready.RemoveAt(ready.Count - 1);
                settled++;
                for (int k = 0; k < feeds[at].Count; k++)
                {
                    int next = feeds[at][k];
                    waitingOn[next]--;
                    if (waitingOn[next] == 0) { ready.Add(next); }
                }
            }
            if (settled == count) { return; }
            StringBuilder held = new StringBuilder();
            int named = 0;
            for (int i = 0; i < count; i++)
            {
                if (waitingOn[i] <= 0) { continue; }
                if (named > 0) { held.Append(", "); }
                held.Append(nodes[i] == null ? "(empty slot)" : nodes[i].DisplayName());
                named++;
            }
            problems.Add("the wiring contains a cycle, and these " + named + " node" + (named == 1 ? "" : "s")
                         + " can never run because each waits on another inside the loop: " + held + ". A bake walks"
                         + " the graph once and forwards, so a loop has no node to start at");
        }
    }
}

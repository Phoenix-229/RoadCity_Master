#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-5. What KIND of node this is -- the one thing a <see cref="CityGraphNode"/> carries that the
    /// runner dispatches on.
    ///
    /// It is an enum and not the operator asset's type for the reason the catalogue exists: most node types are port
    /// sheets over static code that already ships and passes gates
    /// (<c>SuitabilityMapBuilder</c>, <c>CorridorSolver</c>, <c>PlanarFaceFinder</c>, <c>LotSubdivider</c>,
    /// <c>BuildableLandTest</c>, <c>FootprintConform</c>, <c>CityChunkGrid</c> and the rest). Wrapping each of those in
    /// a ScriptableObject operator asset an author has to create and drag would be twenty-eight assets and twenty-eight
    /// files carrying no algorithm -- so the node names its kind, the runner (C-6) calls the shipped code, and only the
    /// four types that genuinely ARE operator assets (<c>StreetNetworkOperator</c>, <c>BlockSubdivisionOperator</c>,
    /// <c>SiteTestOperator</c>, <c>SiteRankOperator</c>) carry one.
    ///
    /// The values are never renumbered. They are serialised inside every graph asset an author saves, and renumbering
    /// would silently turn one node into another. Append at the end. </summary>
    public enum CityNodeKind
    {
        /// <summary> A node whose kind was never set -- the default, and always a problem the graph reports rather than
        /// a node that does nothing quietly. </summary>
        Unassigned = 0,

        // --- Source nodes. Not operators: the runner fills these from CityAuthoring before the walk, which is how the
        // graph gets scene state without an operator breaking the purity contract its own doc states.
        Region = 1,
        TerrainSurface = 2,
        WaterSurface = 3,
        RoadClassSource = 4,
        ZoneCatalogueSource = 5,
        PrefabLibrary = 6,
        CitySeedSource = 7,

        /// <summary> B-27 / D-6, D-8, D-9. The fitted settlement table the rail-presence, transit and underground nodes
        /// read. A source like the prefab library: it computes nothing and hands on the asset the request carries, so
        /// the three consumers read ONE table and cannot disagree about how many stations a size of place gets.
        /// </summary>
        SettlementFitSource = 8,

        // --- Stage 0, LoadOrGenerate: the city itself.
        SuitabilityRaster = 10,
        SiteTest = 11,
        SiteRank = 12,
        CorridorSolve = 13,
        StreetNetwork = 14,
        PlanarFaces = 15,
        BlockInset = 16,
        BlockSubdivision = 17,
        LotSubdivision = 18,
        BuildableLand = 19,
        ZonePaint = 20,
        PrefabValidate = 21,
        BuildingPlacement = 22,
        GradeSeparationNode = 23,
        SurfaceType = 24,
        PolesAndSpans = 25,
        Rail = 26,
        Ropeway = 27,

        /// <summary> One element category from <c>PrefabManifest</c> placed by rule: the 140 categories in 22 sections
        /// are 140 catalogue ENTRIES of this single kind, distinguished by the category parameter on the node, which is
        /// why the author's "even a hundred in each group" costs one enum value and no new file. </summary>
        ElementPlacement = 28,

        // --- Stage 0 still: the settlement register B-27 adds. Infrastructure like Rail and Ropeway above, placed in
        // the free stage-0 slots because the enum is append-only within a range and 27/28 were the last ones taken.
        // Each reads the fitted settlement table and answers one register question while the plan is still being made.
        /// <summary> B-27 / D-6. Whether one settlement has a SURFACE railway, and if so how many stations and how much
        /// track, from the fitted presence curve. A port sheet over <c>SettlementRailPresence</c>. </summary>
        SettlementRail = 29,

        /// <summary> B-27 / D-8. The stopping-place figures for one settlement: bus stations by order, bus stops along a
        /// frontage, rail stations along a run, and whether a stop and a station are one interchange. A port sheet over
        /// <c>TransitPlacement</c>. </summary>
        Transit = 30,

        /// <summary> B-27 / D-9. Whether one settlement has a metro, whether its ground forces an underground road, and
        /// whether the metro's portal can be cut. A port sheet over <c>SettlementUndergroundPresence</c>. </summary>
        Underground = 31,

        /// <summary> B-27 / D-10. Which port a site can hold: a water port whose vessel class is decided by the navigable
        /// depth, or a dry port / shipment yard decided by the rail and road present. A port sheet over
        /// <c>PortSiting</c>. </summary>
        Port = 32,

        /// <summary> B-27 / D-11. The land-use composition for one settlement's order: the residential, commercial,
        /// market and industrial shares of developed ground, read from the fit. A port sheet over
        /// <c>SettlementLandUse</c>. </summary>
        LandUseShares = 33,

        /// <summary> B-27 / D-12. The cross-section of a river bank where it passes through a settlement -- its height,
        /// its slope-or-wall setback chosen by the frontage left for it, and the railing along the pavement-side top. A
        /// port sheet over <c>RiverBank</c>. </summary>
        RiverBank = 34,

        /// <summary> B-27 / D-12. A crossing of the river: the deck family a span is given, its support stations, and the
        /// piers that would land in the water. A port sheet over <c>RiverBank</c>'s crossing resolver, itself wrapping
        /// <c>BridgeTypes</c>. </summary>
        RiverCrossing = 35,

        /// <summary> B-27 / D-13. A dam across a river: the body figured from the valley depth, the reservoir it
        /// impounds found by flooding the real terrain upstream, the spillway and outlet discharges, and the tailwater
        /// below. The one feature that cannot be finished on this side of the linker, so it states a request Universe
        /// Creator answers. A port sheet over <c>DamWorks</c>. </summary>
        Dam = 36,

        // --- Stages 1 to 4: the four stages after the city exists.
        FootprintPublish = 40,
        FootprintConformNode = 41,
        BuildingPadConformNode = 42,
        ScatterExclusion = 43,
        ChunkGrid = 44,
        SpawnTable = 45,
        Streaming = 46,
        BakeStampNode = 47,

        /// <summary> C-18. The sweep over the author's own hand-placed objects: measures the ground under each one and
        /// reports what it found, and only where an author asks for it produces the pad requests that would level them.
        /// </summary>
        HandPlacedScan = 48,

        // --- Canvas-only. These never execute, and the graph refuses a wire to either of them.
        OverrideMarker = 60,
        CommentGroup = 61
    }
    /// <summary> B-15 / C-5. What the runner does with a node of this kind, which is not the same question as what
    /// stage it runs in. Four answers, and each one changes what the graph is allowed to demand of the node. </summary>
    public enum CityNodeRole
    {
        /// <summary> Filled by the runner from <c>CityAuthoring</c> before the walk. It has outputs and no inputs, runs
        /// no code of its own, and is the only way scene state legitimately reaches an operator: inside its input.
        /// </summary>
        Source = 0,

        /// <summary> Runs a <c>CityOperator</c> ASSET the author wires into the node. Only four kinds are this, and a
        /// node of one of them with no asset wired is a problem the graph reports. </summary>
        OperatorAsset = 1,

        /// <summary> Runs shipped static code the runner calls directly -- the twenty-eight port sheets. No asset, no
        /// new algorithm, and the code it calls is named in <see cref="CityNodeDescriptor.owner"/> so an author reading
        /// the canvas can go and read it. </summary>
        ShippedCode = 2,

        /// <summary> Never executes. It exists on the canvas to say something to a human: which objects survive a
        /// re-bake, or where one part of a large graph ends. The graph refuses a wire to it. </summary>
        CanvasOnly = 3
    }
    /// <summary> B-15 / C-5. One port on one node kind: the name a wire names, and the payload that travels through it.
    ///
    /// The payload is a TYPE NAME as text, not a <c>System.Type</c>. Two reasons, and the second is the load-bearing
    /// one: the catalogue is a table read by an editor window that draws it, and a table of reflected types would drag
    /// every stage's assembly into the window's first paint. And a source node's payload is sometimes not a type this
    /// assembly can name at all -- <c>ITerrainSurfaceReader</c> over a provider the linker supplies. Type-checking a
    /// wire between two OPERATOR nodes is still done against the operators' own <c>InputType</c>/<c>OutputType</c>,
    /// where a real type is available; <see cref="CityGraph"/> checks port NAMES against this table, which is the check
    /// it could not make at all before. </summary>
    public struct CityNodePort
    {
        /// <summary> The port name a <see cref="CityGraphEdge"/> names. Stable: renaming one breaks saved graphs, so a
        /// port is added rather than renamed. </summary>
        public string name;

        /// <summary> The payload's type name, as an author would read it -- <c>SuitabilityRasterClass</c>,
        /// <c>SiteChoice</c>, <c>List&lt;List&lt;Vector3&gt;&gt;</c>. </summary>
        public string payload;

        /// <summary> What travels through it, in one clause, for the canvas tooltip. </summary>
        public string summary;

        public CityNodePort(string name, string payload, string summary)
        {
            this.name = name;
            this.payload = payload;
            this.summary = summary;
        }
    }
    /// <summary> B-15 / C-5. One parameter a node kind declares, as the DEFAULT VALUE itself plus the sentence that says
    /// what it is for.
    ///
    /// The default is a <see cref="CityGraphParameter"/> -- the same struct the node stores -- rather than a second
    /// name/kind/value union declared here. A separate spec type would be two descriptions of one value, free to
    /// disagree about which kind a parameter is the moment either changed, and the register's most repeated finding is
    /// exactly that. So the specification of a parameter IS an instance of it, carrying the value an author gets before
    /// they touch anything. </summary>
    public struct CityNodeParameter
    {
        /// <summary> The parameter's name, kind and default value, in the node's own storage type. </summary>
        public CityGraphParameter defaultValue;

        /// <summary> What the value does, in one sentence, for the inspector and the manual. </summary>
        public string summary;

        /// <summary> Whether the node cannot run without an authored value. A required parameter left at its default is
        /// reported by the runner rather than silently accepted -- but most parameters here are not required, because
        /// their default is the shipped behaviour. </summary>
        public bool required;

        public CityNodeParameter(CityGraphParameter defaultValue, string summary, bool required)
        {
            this.defaultValue = defaultValue;
            this.summary = summary;
            this.required = required;
        }

        /// <summary> The parameter's name, read from the value it defaults to. </summary>
        public string Name { get { return defaultValue.name; } }
    }
    /// <summary> B-15 / C-5. One entry in the node catalogue: everything the editor needs to offer a node kind in a
    /// search tree, and everything the runner needs to know what it may demand of a node of that kind.
    ///
    /// It carries no algorithm and calls nothing. That is the point of C-5: the graph's job was never to hold a second
    /// city model, it was to bring code that already passes gates within an author's reach, so an entry NAMES the
    /// shipped code in <see cref="owner"/> and stops there. An entry whose upstream genuinely does not exist -- scatter
    /// exclusion, which waits on RM-15 behind RM-14 -- says so in <see cref="blockedReason"/> and is offered anyway,
    /// greyed, because an author who cannot find the node concludes the tool forgot about scatter. </summary>
    public sealed class CityNodeDescriptor
    {
        /// <summary> Which kind this describes. Unique across the catalogue except for
        /// <see cref="CityNodeKind.ElementPlacement"/>, whose 140 entries differ by
        /// <see cref="elementCategoryId"/>. </summary>
        public CityNodeKind kind;

        /// <summary> The name the search tree shows, and the default <c>CityGraphNode.title</c>. </summary>
        public string displayName = string.Empty;

        /// <summary> The search tree's group for this entry -- the author's "in their own group". For an element entry
        /// it is the manifest SECTION, so the 22 sections become the 22 groups without a second list to maintain.
        /// </summary>
        public string menuSection = string.Empty;

        /// <summary> The stage a node of this kind runs in. Authored onto the node as well, and CHECKED against this:
        /// the same operator is legitimately wanted at two points in a chain, but not in a stage its own inputs have not
        /// been produced in yet. </summary>
        public BakeStage stage = BakeStage.LoadOrGenerate;

        /// <summary> What the runner does with it. </summary>
        public CityNodeRole role = CityNodeRole.ShippedCode;

        /// <summary> The shipped code this entry is a port sheet over, named so the canvas can say it and an author can
        /// go and read it. Empty only for the canvas-only kinds, which stand for nothing but themselves. </summary>
        public string owner = string.Empty;

        /// <summary> Why this node cannot run yet, in the words an author should read, or empty when it can. Exactly one
        /// entry carries this in the shipped catalogue. A blocked node reports its reason; it never logs
        /// <i>not implemented</i>, which tells an author nothing about whose work it waits on. </summary>
        public string blockedReason = string.Empty;

        /// <summary> The <c>PrefabManifest</c> category id this entry places, for
        /// <see cref="CityNodeKind.ElementPlacement"/> entries, and empty for every other kind. </summary>
        public string elementCategoryId = string.Empty;

        /// <summary> The ports, which are the port sheet. </summary>
        public List<CityNodePort> inputs = new List<CityNodePort>();
        public List<CityNodePort> outputs = new List<CityNodePort>();

        /// <summary> The parameters a node of this kind is created carrying. </summary>
        public List<CityNodeParameter> parameters = new List<CityNodeParameter>();

        /// <summary> Whether this entry is waiting on work that does not exist yet. </summary>
        public bool IsBlocked { get { return !string.IsNullOrEmpty(blockedReason); } }

        /// <summary> Whether a node of this kind must have an operator asset wired into it. </summary>
        public bool NeedsOperatorAsset { get { return role == CityNodeRole.OperatorAsset; } }

        /// <summary> Whether a node of this kind executes at all. </summary>
        public bool Executes { get { return role != CityNodeRole.CanvasOnly; } }

        /// <summary> The catalogue key for this entry: the kind, plus the element category when there is one. Unique
        /// across the catalogue, and what a saved node is matched back to its entry by. </summary>
        public string EntryKey
        {
            get
            {
                return string.IsNullOrEmpty(elementCategoryId)
                    ? kind.ToString()
                    : kind.ToString() + ":" + elementCategoryId;
            }
        }

        /// <summary> Whether this kind declares a port of that name, asked separately for the two directions because a
        /// wire arriving at an output port is as wrong as one naming a port that does not exist. </summary>
        public bool DeclaresPort(string portName, bool asInput)
        {
            List<CityNodePort> ports = asInput ? inputs : outputs;
            if (ports == null || string.IsNullOrEmpty(portName)) { return false; }
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].name == portName) { return true; }
            }
            return false;
        }

        /// <summary> The payload a named port carries, as the table's own type NAME. Asked by
        /// <c>CityGraph.Validate</c>, which compares the two ends of a wire: the payload is what actually travels, and it
        /// is not always the operator's <c>InputType</c> -- a block subdivision operator reads a
        /// <c>BlockSubdivisionInput</c> that the runner assembles AROUND the <c>StreetNetwork</c> the wire delivered, so
        /// comparing CLR types there would refuse the one wire the catalogue exists to describe. </summary>
        public bool TryGetPortPayload(string portName, bool asInput, out string payload)
        {
            payload = string.Empty;
            List<CityNodePort> ports = asInput ? inputs : outputs;
            if (ports == null || string.IsNullOrEmpty(portName)) { return false; }
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].name != portName) { continue; }
                payload = ports[i].payload == null ? string.Empty : ports[i].payload;
                return true;
            }
            return false;
        }

        /// <summary> The names of every port on this entry, joined for a report line. </summary>
        public string PortLine()
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < inputs.Count; i++)
            {
                text.Append(i == 0 ? "in " : ", ").Append(inputs[i].name).Append(" (").Append(inputs[i].payload)
                    .Append(')');
            }
            if (inputs.Count > 0 && outputs.Count > 0) { text.Append(" -> "); }
            for (int i = 0; i < outputs.Count; i++)
            {
                text.Append(i == 0 ? "out " : ", ").Append(outputs[i].name).Append(" (").Append(outputs[i].payload)
                    .Append(')');
            }
            if (inputs.Count == 0 && outputs.Count == 0) { text.Append("no ports"); }
            return text.ToString();
        }

        /// <summary> One line for the manual and the catalogue report: the entry, its group, its stage, what it wires,
        /// and its ports. </summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append(displayName).Append(" [").Append(menuSection).Append("] stage ").Append(stage.ToString());
            text.Append(", ").Append(role.ToString());
            if (!string.IsNullOrEmpty(owner)) { text.Append(" over ").Append(owner); }
            text.Append(": ").Append(PortLine());
            if (parameters.Count > 0)
            {
                text.Append(", ").Append(parameters.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(parameters.Count == 1 ? " parameter" : " parameters");
            }
            if (IsBlocked) { text.Append(". BLOCKED: ").Append(blockedReason); }
            return text.ToString();
        }
    }
    /// <summary> B-15 / C-5. The node catalogue: every node kind the graph can hold, as data.
    ///
    /// This is the table C-8's search window builds its tree from, the table C-6's runner dispatches on, and the table
    /// <see cref="CityGraph.Validate"/> checks a saved node's ports and stage against. One table, three consumers -- so
    /// a node kind that is added here appears in the window, runs in the runner and validates, with nothing else to
    /// remember.
    ///
    /// The element entries are GENERATED from <c>PrefabManifest</c> rather than listed: every category in it becomes one
    /// <see cref="CityNodeKind.ElementPlacement"/> entry with four parameter rows -- capital, city, town, village -- so a
    /// category an artist adds to the manifest tomorrow is offered in the search tree without a code change. That is why
    /// the author's <i>"even 100 each in their own group"</i> costs one enum value: the enumeration was already done, in
    /// data, with variants and metadata needs.
    ///
    /// Every count this class reports is COUNTED from the entries, never written down beside them. A count in a comment
    /// is a second copy of the table, free to disagree with it the moment a row is added. </summary>
    public static class CityNodeCatalog
    {
        private static List<CityNodeDescriptor> _entries;

        /// <summary> The four settlement orders' parameter prefixes, in <c>SettlementOrderCatalog</c>'s index order --
        /// index 1 is the capital, 4 the village. Exposed so the runner reads a parameter under the SAME name the
        /// catalogue wrote it under; two places composing that name independently is the failure this avoids. </summary>
        private static readonly string[] OrderPrefixes = { "capital", "city", "town", "village" };

        /// <summary> The parameter-name prefix for one settlement order index, or empty when the index is not one of the
        /// four <c>SettlementOrderCatalog</c> holds. </summary>
        public static string OrderParameterPrefix(int orderIndex)
        {
            int slot = orderIndex - SettlementOrderCatalog.CapitalOrder;
            return slot >= 0 && slot < OrderPrefixes.Length ? OrderPrefixes[slot] : string.Empty;
        }

        /// <summary> Every entry, built once and cached: the hand-written kinds in stage order, then one per manifest
        /// category. </summary>
        public static IReadOnlyList<CityNodeDescriptor> Entries
        {
            get
            {
                if (_entries == null) { _entries = BuildAll(); }
                return _entries;
            }
        }

        /// <summary> How many node TYPES were hand-written -- every entry that is not an element category. </summary>
        public static int HandWrittenTypeCount
        {
            get
            {
                int count = 0;
                IReadOnlyList<CityNodeDescriptor> all = Entries;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].kind != CityNodeKind.ElementPlacement) { count++; }
                }
                return count;
            }
        }

        /// <summary> How many element entries the manifest produced. </summary>
        public static int ElementEntryCount { get { return Entries.Count - HandWrittenTypeCount; } }

        /// <summary> The entry for a kind that has exactly one, or false. Element placement has 140 and is asked for
        /// through <see cref="TryGetElement"/> instead, so asking for it here is a refusal rather than the first of the
        /// hundred and forty. </summary>
        public static bool TryGet(CityNodeKind kind, out CityNodeDescriptor descriptor)
        {
            descriptor = null;
            if (kind == CityNodeKind.ElementPlacement || kind == CityNodeKind.Unassigned) { return false; }
            IReadOnlyList<CityNodeDescriptor> all = Entries;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].kind == kind) { descriptor = all[i]; return true; }
            }
            return false;
        }

        /// <summary> The element entry for one manifest category id, or false when the manifest holds no such category
        /// -- which is what an author sees after deleting a category a saved graph still places. </summary>
        public static bool TryGetElement(string categoryId, out CityNodeDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrEmpty(categoryId)) { return false; }
            IReadOnlyList<CityNodeDescriptor> all = Entries;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].kind == CityNodeKind.ElementPlacement && all[i].elementCategoryId == categoryId)
                {
                    descriptor = all[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary> The entry a saved node belongs to, and a reason when it belongs to none: an unset kind, a kind this
        /// build no longer knows, or an element node naming a category the manifest has since lost. The reason is the
        /// one the graph reports, so an author reads what to fix rather than that something is null. </summary>
        public static bool TryGetForNode(CityGraphNode node, out CityNodeDescriptor descriptor, out string reason)
        {
            descriptor = null;
            if (node == null)
            {
                reason = "an empty node slot describes no node kind at all";
                return false;
            }
            if (node.kind == CityNodeKind.Unassigned)
            {
                reason = "the node " + node.DisplayName() + " does not say what kind of node it is, so nothing knows"
                         + " what to run for it. Kinds are set when the node is created from the catalogue";
                return false;
            }
            if (node.kind == CityNodeKind.ElementPlacement)
            {
                CityGraphParameter parameter;
                string why;
                if (!node.TryGetParameter(ElementCategoryParameter, out parameter, out why))
                {
                    reason = why + " -- an element placement node is one manifest category, named by that parameter";
                    return false;
                }
                string categoryId;
                if (!parameter.TryGetText(out categoryId, out why)) { reason = why; return false; }
                if (!TryGetElement(categoryId, out descriptor))
                {
                    reason = "the node " + node.DisplayName() + " places the element category " + categoryId
                             + ", which this build's prefab manifest does not hold -- either the category was renamed"
                             + " or this graph was written against a later manifest";
                    return false;
                }
                reason = string.Empty;
                return true;
            }
            if (!TryGet(node.kind, out descriptor))
            {
                reason = "the node " + node.DisplayName() + " is of the kind " + node.kind + ", which this build's node"
                         + " catalogue does not hold, so nothing knows what to run for it";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> The name of the parameter an element placement node names its manifest category in. Public because
        /// the runner, the window and the graph's own validation all read it, and a literal repeated in three files is
        /// three chances to mistype it. </summary>
        public const string ElementCategoryParameter = "category";

        /// <summary> Whether a node of this kind must carry an operator asset. Asked of the catalogue rather than of the
        /// node, because the node holds no such rule -- and asked by <see cref="CityGraph.Validate"/>, which before C-5
        /// had to demand an operator of every node and would have refused every source and every port sheet. </summary>
        public static bool NeedsOperatorAsset(CityNodeKind kind)
        {
            CityNodeDescriptor descriptor;
            if (kind == CityNodeKind.ElementPlacement) { return false; }
            return TryGet(kind, out descriptor) && descriptor.NeedsOperatorAsset;
        }

        /// <summary> Whether a node of this kind executes, or only says something to a human on the canvas. An unknown
        /// kind answers true, so an unrecognised node is reported by the runner rather than quietly skipped. </summary>
        public static bool Executes(CityNodeKind kind)
        {
            CityNodeDescriptor descriptor;
            if (kind == CityNodeKind.ElementPlacement) { return true; }
            return !TryGet(kind, out descriptor) || descriptor.Executes;
        }
        /// <summary> Turns one entry into a node ready to drop on the canvas: a fresh id, the entry's title, its stage,
        /// its kind, and a copy of every parameter row at its default. THE one place that does this, so C-8's search
        /// window cannot create a node the runner then cannot read -- a window that filled the parameters itself would be
        /// a second copy of every default, and the first divergence would show up as a missing parameter at bake time.
        ///
        /// <c>CityGraphParameter</c> is a struct, so the copies are copies: editing one node's spacing never reaches the
        /// catalogue's default or another node's. </summary>
        public static CityGraphNode CreateNode(CityNodeDescriptor descriptor)
        {
            CityGraphNode node = new CityGraphNode();
            node.id = CityGraph.MintId();
            if (descriptor == null) { return node; }
            node.kind = descriptor.kind;
            node.title = descriptor.displayName;
            node.stage = descriptor.stage;
            node.parameters.Clear();
            for (int i = 0; i < descriptor.parameters.Count; i++)
            {
                node.parameters.Add(descriptor.parameters[i].defaultValue);
            }
            return node;
        }

        /// <summary> The whole catalogue as text, for the manual (C-20) and for the gate that checks it: the counts, then
        /// one line per entry. </summary>
        public static string Report()
        {
            IReadOnlyList<CityNodeDescriptor> all = Entries;
            StringBuilder text = new StringBuilder();
            text.Append("city node catalogue: ").Append(all.Count.ToString(CultureInfo.InvariantCulture));
            text.Append(" entries -- ").Append(HandWrittenTypeCount.ToString(CultureInfo.InvariantCulture));
            text.Append(" node types and ").Append(ElementEntryCount.ToString(CultureInfo.InvariantCulture));
            text.Append(" element categories from the prefab manifest");
            for (int i = 0; i < all.Count; i++)
            {
                text.Append('\n').Append("  ").Append(all[i].Describe());
            }
            return text.ToString();
        }
        /// <summary> Builds every entry, in the order the search tree shows them: the sources a graph starts from, the
        /// stage-0 nodes that plan the city, the later stages' nodes, the two canvas-only ones, then the manifest's
        /// element categories. Called once, by <see cref="Entries"/>.
        ///
        /// The port names are load-bearing, not decoration. <c>CityGraph</c> could already refuse a wire whose payload
        /// types disagree, but it had no way to refuse a wire naming a port that does not exist -- there was nowhere that
        /// said what ports a node has. This is that place, so C-5 buys the graph a check it could not make at all before.
        /// </summary>
        private static List<CityNodeDescriptor> BuildAll()
        {
            List<CityNodeDescriptor> list = new List<CityNodeDescriptor>();
            AddSources(list);
            AddStageZero(list);
            AddLaterStages(list);
            AddCanvasOnly(list);
            AddElements(list);
            return list;
        }

        /// <summary> Adds one entry and hands it back, so its ports and parameters are hung on it at the point it is
        /// declared rather than in a second table keyed by kind. </summary>
        private static CityNodeDescriptor Add(List<CityNodeDescriptor> list, CityNodeKind kind, string displayName,
            string menuSection, BakeStage stage, CityNodeRole role, string owner)
        {
            CityNodeDescriptor entry = new CityNodeDescriptor();
            entry.kind = kind;
            entry.displayName = displayName;
            entry.menuSection = menuSection;
            entry.stage = stage;
            entry.role = role;
            entry.owner = owner;
            list.Add(entry);
            return entry;
        }
        /// <summary> One input port on an entry. </summary>
        private static void In(CityNodeDescriptor entry, string name, string payload, string summary)
        {
            entry.inputs.Add(new CityNodePort(name, payload, summary));
        }

        /// <summary> One output port on an entry. </summary>
        private static void Out(CityNodeDescriptor entry, string name, string payload, string summary)
        {
            entry.outputs.Add(new CityNodePort(name, payload, summary));
        }

        /// <summary> One numeric parameter, at the default a node is created carrying. </summary>
        private static void Number(CityNodeDescriptor entry, string name, double value, string summary, bool required)
        {
            entry.parameters.Add(new CityNodeParameter(CityGraphParameter.OfNumber(name, value), summary, required));
        }

        /// <summary> One text parameter. </summary>
        private static void Text(CityNodeDescriptor entry, string name, string value, string summary, bool required)
        {
            entry.parameters.Add(new CityNodeParameter(CityGraphParameter.OfText(name, value), summary, required));
        }

        /// <summary> One on/off parameter. </summary>
        private static void Flag(CityNodeDescriptor entry, string name, bool value, string summary, bool required)
        {
            entry.parameters.Add(new CityNodeParameter(CityGraphParameter.OfFlag(name, value), summary, required));
        }
        /// <summary> The search tree's groups. Named once here so the window, the manual and the report agree on the
        /// spelling of a heading an author reads. </summary>
        private const string SourceSection = "Sources";
        private const string PlanSection = "Plan";
        private const string StreetSection = "Streets and blocks";
        private const string LandSection = "Buildings and land";
        private const string InfrastructureSection = "Infrastructure";
        private const string TerrainSection = "Terrain and footprints";
        private const string RuntimeSection = "Runtime and streaming";
        private const string CanvasSection = "Canvas";

        /// <summary> The eight sources: what a graph reads before it computes anything. Each one has outputs and no
        /// inputs, and each names the asset or contract it reads, so an author can see that the region a corridor is
        /// solved in and the region a site is tested in are the SAME region, read once. </summary>
        private static void AddSources(List<CityNodeDescriptor> list)
        {
            CityNodeDescriptor entry = Add(list, CityNodeKind.Region, "Region", SourceSection,
                BakeStage.LoadOrGenerate, CityNodeRole.Source, "CityData");
            Out(entry, "region", "Rect", "the city's rectangle in world XZ metres, from the asset being baked");

            entry = Add(list, CityNodeKind.TerrainSurface, "Terrain surface", SourceSection, BakeStage.LoadOrGenerate,
                CityNodeRole.Source, "ITerrainSurfaceReader");
            Out(entry, "terrain", "ITerrainSurfaceReader",
                "the live ground: height and normal at a world XZ, and the chunks that are loaded");
            Out(entry, "provider", "IPCGDataProvider",
                "the same ground seen through the linker's provider, which is what the street, buildable-land and pad"
                + " passes read. Two ports rather than one because the two are different interfaces on the same"
                + " surface, and a single port would make one of those passes unwireable rather than merely"
                + " inconvenient");

            entry = Add(list, CityNodeKind.WaterSurface, "Water surface", SourceSection, BakeStage.LoadOrGenerate,
                CityNodeRole.Source, "CityData");
            Out(entry, "waterSurfaceY", "float",
                "the water level in world metres, or negative infinity where this world has no water");
            Flag(entry, "hasWater", false,
                "off means this region has no water at all, and every water test becomes a no-op", true);
            Number(entry, "waterSurfaceY", 0.0,
                "the water level in world Y metres, read only when hasWater is on", false);

            entry = Add(list, CityNodeKind.RoadClassSource, "Road class catalogue", SourceSection,
                BakeStage.LoadOrGenerate, CityNodeRole.Source, "RoadClassCatalog");
            Out(entry, "roadClasses", "RoadClassCatalog", "every functional road class this city may build");
            Out(entry, "roadClass", "RoadClass", "one class out of the catalogue, the one classId names");
            Number(entry, "classId", RoadClassCatalog.ArterialId,
                "which class of the catalogue this source hands on, by RoadClass.id -- the same int an edge stores."
                + " The catalogue's baseline ids are 0 arterial, 1 collector, 2 local", true);
            entry = Add(list, CityNodeKind.ZoneCatalogueSource, "Zone catalogue", SourceSection,
                BakeStage.LoadOrGenerate, CityNodeRole.Source, "ZoneCatalog");
            Out(entry, "zones", "ZoneCatalog", "every zone type this city may paint, keyed by ZoneTypeClass.zoneId");
            Out(entry, "zone", "ZoneTypeClass", "one zone out of the catalogue, the one zoneId names");
            Text(entry, "zoneId", string.Empty,
                "which zone this source hands on, by its zoneId. A zone the catalogue does not define resolves to"
                + " nothing rather than to a default -- the catalogue is fail-closed on purpose", false);

            entry = Add(list, CityNodeKind.PrefabLibrary, "Prefab library", SourceSection, BakeStage.LoadOrGenerate,
                CityNodeRole.Source, "ContentCatalog");
            Out(entry, "content", "ContentCatalog",
                "the author's prefabs, by category -- what the placement nodes ask for and never create");

            entry = Add(list, CityNodeKind.CitySeedSource, "City seed", SourceSection, BakeStage.LoadOrGenerate,
                CityNodeRole.Source, "CityData.seed and CitySeed");
            Out(entry, "citySeed", "int",
                "the city's seed, read from the asset being baked so no two nodes can disagree about it. Every node"
                + " that needs randomness derives its own stream from this through CitySeed, and never draws from"
                + " System.Random or the clock");

            entry = Add(list, CityNodeKind.SettlementFitSource, "Settlement fit table", SourceSection,
                BakeStage.LoadOrGenerate, CityNodeRole.Source, "CityBakeRequest.settlementFit and SettlementFitTable");
            Out(entry, "settlementFit", "SettlementFitTable",
                "the fitted settlement table -- the population/presence curves and the rail, transit and underground"
                + " rows the register nodes read. One table handed to all three so a place's railway, its metro and its"
                + " station counts are drawn from the same figures and cannot disagree. The editor fills it from a"
                + " found t:SettlementFitTable asset, or from an authored placeholder when none has been imported, so"
                + " the port is never null and a register node refuses for \"no fit table\" only when an author clears"
                + " this source on purpose");
        }
        /// <summary> The twenty-one nodes that plan the city, all in <see cref="BakeStage.LoadOrGenerate"/> because the
        /// plan is decided before a single footprint is published. Four of them carry an operator asset -- the four that
        /// have authored settings worth saving beside a project; the other seventeen are port sheets over shipped static
        /// code, which is why C-5 adds no operator files. </summary>
        private static void AddStageZero(List<CityNodeDescriptor> list)
        {
            CityNodeDescriptor entry = Add(list, CityNodeKind.SuitabilityRaster, "Suitability raster", PlanSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "SuitabilityMapBuilder.Build");
            In(entry, "region", "Rect", "the rectangle to raster, which fixes the origin and the cell counts");
            In(entry, "terrain", "ITerrainSurfaceReader", "the ground the slope and earthwork resistances are read from");
            In(entry, "roadClass", "RoadClass", "the class whose gradient limit decides what counts as steep here");
            In(entry, "waterSurfaceY", "float", "the water level, or negative infinity for a world with no water");
            Out(entry, "suitability", "SuitabilityRasterClass",
                "one 0..1 resistance per cell, the factor that bound it, and that factor's own measurement");
            Out(entry, "legend", "string", "what the raster measured, in the words the corridor report quotes");
            Number(entry, "metresPerCell", 16.0,
                "the cell size in metres. Sixteen is the shipped default: fine enough to see a bank, coarse enough"
                + " that a city-sized region is thousands of cells and not millions", true);
            Number(entry, "slopeWeight", 0.45,
                "how much the ground's grade counts, against the other four. The five defaults are the authored ones"
                + " and they sum to one", true);
            Number(entry, "earthworkWeight", 0.25, "how much cut and fill volume counts", true);
            Number(entry, "waterWeight", 0.15, "how much nearness to water counts", true);
            Number(entry, "landUseWeight", 0.10, "how much an existing land use counts", true);
            Number(entry, "networkWeight", 0.05, "how much distance from the existing network counts", true);

            entry = Add(list, CityNodeKind.SiteTest, "Site test", PlanSection, BakeStage.LoadOrGenerate,
                CityNodeRole.OperatorAsset, "SiteTestOperator");
            In(entry, "suitability", "SuitabilityRasterClass", "the raster every candidate's ground is read from");
            In(entry, "candidateRegions", "List<Rect>",
                "the regions to judge -- in a settlement bake, one per road end the author drew");
            In(entry, "terrain", "ITerrainSurfaceReader", "the ground, for the heights the water rule compares");
            In(entry, "waterSurfaceY", "float", "the water level the freeboard is measured from");
            In(entry, "zone", "ZoneTypeClass", "the zone whose slope limit and lot minimum a candidate must satisfy");
            In(entry, "roadClass", "RoadClass", "the class whose gradient limit is the other half of that comparison");
            In(entry, "network", "ISuitabilityNetworkSource", "how far a candidate is from the roads already drawn");
            Out(entry, "verdicts", "SiteVerdictSet",
                "one verdict per candidate: accepted or refused, with the measurement that decided it");
            entry = Add(list, CityNodeKind.SiteRank, "Site rank", PlanSection, BakeStage.LoadOrGenerate,
                CityNodeRole.OperatorAsset, "SiteRankOperator");
            In(entry, "verdicts", "SiteVerdictSet", "every candidate the site test judged");
            Out(entry, "choice", "SiteChoice",
                "the site the settlement is built on, and every runner-up in order with the factor that cost it");
            Out(entry, "region", "Rect", "the chosen site's rectangle, which the rest of the graph plans inside");

            entry = Add(list, CityNodeKind.CorridorSolve, "Corridor solve", PlanSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "CorridorSolver.Solve");
            In(entry, "suitability", "SuitabilityRasterClass", "the raster the route is priced against");
            In(entry, "fromCell", "Vector2", "the start cell, as integer cell x and z");
            In(entry, "toCell", "Vector2", "the end cell, as integer cell x and z");
            Out(entry, "corridor", "CorridorPlan",
                "the cheapest route through the raster, its cost, and the factor that shaped it");
            Number(entry, "toleranceFraction", CorridorSolver.DefaultToleranceFraction,
                "how much dearer than the cheapest route an alternative may be and still be offered. The shipped"
                + " default is five per cent", true);

            entry = Add(list, CityNodeKind.StreetNetwork, "Street network", StreetSection,
                BakeStage.LoadOrGenerate, CityNodeRole.OperatorAsset, "StreetNetworkOperator");
            In(entry, "terrain", "IPCGDataProvider", "the ground the streets are grown across");
            In(entry, "region", "Rect", "the rectangle the streets fill -- the chosen site, not the whole world");
            Out(entry, "network", "StreetNetwork",
                "the streets as nodes and edges, each edge carrying the road class it was grown as");
            entry = Add(list, CityNodeKind.PlanarFaces, "Planar faces", StreetSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "PlanarFaceFinder.FindFaces");
            In(entry, "network", "StreetNetwork", "the streets whose enclosed faces are the city's blocks");
            Out(entry, "faces", "List<List<int>>",
                "each face as a ring of network node indices, in the order they were discovered -- which is fixed, so"
                + " two bakes of one network number the blocks identically");

            entry = Add(list, CityNodeKind.BlockInset, "Block inset", StreetSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "PolygonInset.TryInset");
            In(entry, "polygon", "List<Vector3>", "the face to inset -- a street centreline ring, not yet buildable");
            In(entry, "insetPerEdge", "List<float>",
                "how far to pull each edge in, one metre figure per edge, so a block on an arterial loses more than the"
                + " same block's local-street edge");
            Out(entry, "block", "List<Vector3>", "the buildable block, or nothing when the inset consumed the face");
            Number(entry, "fallbackInsetMetres", 0.0,
                "the inset used for every edge when no per-edge widths are wired in. Zero means each edge is inset by"
                + " its own street's half width plus its verge, which is the figure that is actually true", false);

            entry = Add(list, CityNodeKind.BlockSubdivision, "Block subdivision", StreetSection,
                BakeStage.LoadOrGenerate, CityNodeRole.OperatorAsset, "BlockSubdivisionOperator");
            In(entry, "network", "StreetNetwork", "the streets whose faces become blocks and then lots");
            Out(entry, "subdivision", "BlockSubdivisionResult", "the blocks, the lots, and the report of what was done");
            Out(entry, "blocks", "List<Block>", "the inset blocks alone, for a graph that only wants the blocks");
            Out(entry, "lots", "List<Lot>", "the lots alone, each carrying its parent block's id");

            entry = Add(list, CityNodeKind.LotSubdivision, "Lot subdivision", StreetSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "LotSubdivider.Subdivide");
            In(entry, "block", "List<Vector3>", "one buildable block to cut into lots");
            In(entry, "faceSeed", "int", "this block's own seed, from CitySeed, so its cuts are its own and repeatable");
            Out(entry, "lots", "List<List<Vector3>>", "the lot polygons, in a fixed order");
            Number(entry, "targetAreaM2", 0.0,
                "the lot size aimed at, in square metres. Zero means the painted zone's own lot size at the intensity"
                + " painted there, which is the figure the zone catalogue already states", false);
            Number(entry, "splitJitter", 0.0,
                "how far a cut may wander from the even split, 0..1, so a street of lots is not a comb. Zero is an"
                + " even split", true);
            entry = Add(list, CityNodeKind.BuildableLand, "Buildable land", LandSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "BuildableLandTest.Evaluate");
            In(entry, "lot", "List<Vector3>", "the lot to judge");
            In(entry, "terrain", "IPCGDataProvider", "the ground it is judged on");
            In(entry, "waterSurfaceY", "float", "the water level the freeboard rule is measured from");
            In(entry, "occupied", "List<List<Vector3>>",
                "the footprints already claimed -- road corridors, built lots, rail, easements");
            In(entry, "criteria", "BuildabilityCriteria",
                "the zone's own limits: grade, minimum area, freeboard and pad relief");
            Out(entry, "verdict", "LotVerdict", "buildable, reshaped or rejected, with the number that decided it");
            Out(entry, "lot", "List<Vector3>",
                "the polygon that would actually be built: the whole lot, or the reshaped remnant when a bank was cut"
                + " off it");
            Number(entry, "sampleSpacingMetres", 0.0,
                "how finely the lot is sampled. Zero means the terrain's own heightmap spacing, which is the only"
                + " spacing that tells you something new -- finer costs time and reveals nothing", false);

            entry = Add(list, CityNodeKind.ZonePaint, "Zone paint", LandSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "ZonePaint.ResolveCell and ResolveLotByMajority");
            In(entry, "zones", "ZoneCatalog", "the zone types that may be painted here");
            In(entry, "lotCellZoneIds", "List<string>", "the zone id under each of this lot's cells");
            Out(entry, "resolution", "ZoneResolution",
                "the zone this lot is, or a refusal saying it straddles a boundary and by how much");
            Number(entry, "straddleTieFraction", 0.0,
                "how far ahead the leading zone must be, as a share of the lot, before it wins. Zero means a bare"
                + " majority decides and only a dead-even split is refused as straddling", true);

            entry = Add(list, CityNodeKind.PrefabValidate, "Prefab validate", LandSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "ContentResolver.Resolve over PrefabContract");
            In(entry, "content", "ContentCatalog", "the author's prefabs, by category");
            Out(entry, "resolution", "ContentResolution",
                "the prefab this category resolves to, or a refusal naming what the author has not supplied yet."
                + " The scene half of the contract is checked by the editor's PrefabContractValidator; this node"
                + " reads the catalogue only, which is why it runs in a bake with no scene open");
            Text(entry, "category", string.Empty,
                "which category to resolve, by its manifest id. Empty is a refusal, not a default -- a placement that"
                + " does not say what it needs cannot be given anything", true);
            Text(entry, "requester", string.Empty,
                "who is asking, for the refusal message. Empty names the node instead", false);

            entry = Add(list, CityNodeKind.BuildingPlacement, "Building placement", LandSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "BuildingPlacement.ComputePlacements");
            In(entry, "requests", "List<PlacementRequest>", "one request per lot: its polygon, its zone and its prefab");
            Out(entry, "buildings", "List<BuildingInstance>",
                "where each building stands and how it is turned, in lot-id order so a re-bake matches");
            Out(entry, "verdicts", "List<PlacementVerdict>", "why any lot got no building");
            entry = Add(list, CityNodeKind.GradeSeparationNode, "Grade separation", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "GradeSeparation");
            In(entry, "network", "StreetNetwork", "the streets whose crossings are found");
            In(entry, "roadClasses", "RoadClassCatalog",
                "the classes, for the ranks that decide which road of a pair is the one raised");
            In(entry, "separationClass", "GradeSeparationClass",
                "the clearance and approach rules -- authored there, not here, so a flyover and a bridge agree");
            Out(entry, "crossings", "List<GradeSeparation.PlanCrossing>",
                "every place two roads cross in plan, and where along each");
            Out(entry, "separations", "List<GradeSeparation.SeparationResolve>",
                "for each crossing: which road rises, by how much, and over what approach length its gradient allows");

            entry = Add(list, CityNodeKind.SurfaceType, "Surface type", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "SurfaceType");
            In(entry, "surface", "SurfaceTypeClass", "sealed or unsealed, and what that implies");
            In(entry, "flattenedHalfWidthMetres", "float", "how wide the flattened formation is on this road");
            Out(entry, "paintHalfWidthMetres", "double", "how much of that width is the sealed carriageway");
            Out(entry, "emitsLaneMarkings", "bool", "whether a road of this surface is marked at all");
            Out(entry, "requiresSideDitch", "bool", "whether it needs a ditch to shed water");

            entry = Add(list, CityNodeKind.PolesAndSpans, "Poles and spans", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "DistributionPole and TransmissionLine");
            In(entry, "poleClass", "DistributionPoleClass", "the span length and the clearances a pole run keeps");
            In(entry, "lineClass", "TransmissionLineClass", "the conductor's sag and its statutory clearance");
            In(entry, "groundHeights", "List<double>",
                "the ground under the run, as evenly spaced heights from its start to its end -- two for a single"
                + " span, more for a longer run. Their own spacing sets the density each span is then checked at, so"
                + " the clearance test is taken at the resolution the author supplied and not at one invented here");
            In(entry, "intersectionExclusions", "List<Vector2>",
                "the stretches along the run a pole may not stand in, each as a start and an end station in metres --"
                + " a junction, a crossing, a driveway. Optional: with none wired, nothing is excluded");
            In(entry, "existingFurniture", "List<float>",
                "the stations, in metres along the run, already taken by other furniture. Optional, and the port the"
                + " nudge above reads: with none wired there is nothing to nudge off and every pole stands where its"
                + " span put it");
            Out(entry, "poles", "List<Vector3>",
                "a placement slot per pole, nudged off the furniture it would have stood in. In the RUN's own frame --"
                + " x is metres along the run, y is the ground there, z is zero -- because no port on this node carries"
                + " the run's world curve, and a world position would be an invention rather than a reading");
            Out(entry, "spans", "List<Catenary>", "each conductor's real curve, from its sag ratio -- never a straight");
            Out(entry, "clearance", "TransmissionLine.ClearanceResult",
                "whether every span clears the ground it crosses, and where the worst point is");
            Number(entry, "runLengthMetres", 0.0,
                "how long the pole run is. Zero means one span of the pole class, which is the shortest run that has a"
                + " span in it at all", false);

            entry = Add(list, CityNodeKind.Rail, "Rail", InfrastructureSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "RailTrack");
            In(entry, "trackClass", "RailTrackClass", "gauge, cant limits, minimum radius and ruling gradient");
            In(entry, "curveRadiusMetres", "float", "the radius being checked");
            Out(entry, "appliedCantMillimetres", "double", "the cant this curve is built with");
            Out(entry, "radiusCheck", "RailTrack.RadiusCheck",
                "whether the radius is buildable, and the reason when it is not");
            Number(entry, "designSpeedKmh", 0.0,
                "the speed the cant is computed for. Zero means the track class's own design speed", false);

            entry = Add(list, CityNodeKind.Ropeway, "Ropeway", InfrastructureSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "Ropeway");
            In(entry, "ropewayClass", "RopewayClass", "sag ratio, ground clearance, cabin spacing and tower rules");
            In(entry, "groundHeights", "List<double>",
                "the ground under the line, as evenly spaced heights from one end to the other, on the same terms as a"
                + " pole run's");
            In(entry, "turnAnglesDeg", "List<double>",
                "how far the line turns at each tower, in degrees, in tower order. Optional: with none wired every"
                + " tower is read as straight-through, which is what a line drawn as one alignment is");
            Out(entry, "towers", "List<Vector3>",
                "a placement slot per tower. In the RUN's own frame, on the same terms and for the same reason as a"
                + " pole run's slots");
            Out(entry, "towerTypes", "List<TowerType>",
                "the tower each of those slots takes -- suspension where the line runs straight through, angle where it"
                + " turns past the authored threshold. A separate port because a slot is a position and cannot carry a"
                + " type, and the two lists are index-matched");
            Out(entry, "rope", "Catenary",
                "the rope's real curve over the FIRST span. One span rather than all of them because a Catenary is one"
                + " curve; the clearance below is the tightest span's, so a line whose worst span fails still reports"
                + " that failure");
            Out(entry, "cabins", "int", "how many cabins the line carries at its authored spacing");
            Out(entry, "clearance", "TransmissionLine.ClearanceResult", "whether the rope clears the ground beneath it");
            Number(entry, "runLengthMetres", 0.0,
                "how long the line is, end to end. The cabin count is read from it, so a line of no length carries no"
                + " cabins and the pass refuses rather than reporting zero as a result", false);
            Number(entry, "spanLengthMetres", 0.0,
                "how far apart the towers stand. Zero means one span for the whole line, which is what a ropeway with"
                + " no intermediate tower is", false);

            entry = Add(list, CityNodeKind.SettlementRail, "Settlement rail", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "SettlementRailPresence");
            In(entry, "settlementFit", "SettlementFitTable", "the fitted table the presence curve is read from");
            Out(entry, "rail", "SettlementRailReport",
                "the whole answer: served or not, the odds behind it, the station count, spacing and track length, and"
                + " which curve it rests on -- so an unserved place shows the probability that produced the no");
            Out(entry, "served", "bool",
                "whether this settlement has a railway -- the decision alone, for a wire that only branches on it");
            Out(entry, "stationCount", "int", "how many stations, and zero when unserved rather than a fabricated one");
            Out(entry, "trackLengthMetres", "float", "how much track this settlement implies in metres, zero unserved");
            Number(entry, "orderIndex", 0.0,
                "which settlement order the curve is read for: 1 capital, 2 city, 3 town, 4 village. Zero means the"
                + " order this bake was launched as, so a single-settlement bake needs no figure here", false);
            Number(entry, "population", 0.0,
                "the population the presence curve is asked about. Below forty the answer is no railway without a draw,"
                + " so zero is an honest \"too small to ask\" and not a refusal", true);
            Text(entry, "continentKey", string.Empty,
                "which continent bucket to read, by key. Empty means the world curve, which is also the fallback when"
                + " the named bucket holds no row", false);
            Text(entry, "identity", string.Empty,
                "the settlement's STABLE identity, the string the presence draw is keyed on -- not its display name, or"
                + " the railway would move when the name is edited. Empty refuses, because a draw with no key would"
                + " change every bake", true);

            entry = Add(list, CityNodeKind.Transit, "Transit stops", InfrastructureSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "TransitPlacement");
            Out(entry, "busStations", "int", "how many bus stations (stages) this settlement order gets");
            Out(entry, "busStopRun", "BusStopRun",
                "the bus stops along the frontage: the count and the interval they actually fell out at");
            Out(entry, "railStationRun", "RailStationRun",
                "the rail stations along the run: the count and the interval, for the service named");
            Out(entry, "interchange", "InterchangeResult",
                "whether a stop and a station are one place, and the paved walk between them when they are");
            Number(entry, "orderIndex", 0.0,
                "which settlement order the bus-station count is read for: 1 capital, 2 city, 3 town, 4 village. Zero"
                + " means the order this bake was launched as. An order off the ladder yields no count, not a guess",
                false);
            Number(entry, "frontageMetres", 0.0,
                "the run of street frontage the bus stops are spaced along, in metres. Zero leaves the bus-stop count"
                + " unanswered -- there is no street to place one on -- while the other figures still resolve", false);
            Flag(entry, "suburban", false,
                "off spaces bus stops at the urban nominal, on at the wider suburban one; a stop every urban interval"
                + " on a suburban road would halt the bus more than it moved it", false);
            Number(entry, "railRunLengthMetres", 0.0,
                "the length of rail line the stations are spaced along, in metres. Zero leaves the rail-station count"
                + " unanswered", false);
            Number(entry, "railService", 0.0,
                "which rail service spaces those stations: 0 metro (~1 km), 1 commuter (2-5 km), 2 intercity"
                + " (20-60 km). It is how often the line stops, not what it is laid to", false);
            Flag(entry, "testInterchange", false,
                "on tests whether a bus stop and a rail station are one interchange, from the two figures below; off"
                + " leaves the interchange unanswered", false);
            Number(entry, "busToRailMetres", 0.0,
                "how far a bus stop stands from a rail station, in metres, read only when the interchange test is on",
                false);
            Number(entry, "walkBudgetMetres", TransitPlacement.DefaultInterchangeWalkMetres,
                "the walk inside which a stop and a station are one interchange -- the standard 400 m five-minute"
                + " pedestrian catchment a transit authority plans bus access to a station within", false);

            entry = Add(list, CityNodeKind.Underground, "Underground", InfrastructureSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "SettlementUndergroundPresence");
            In(entry, "settlementFit", "SettlementFitTable", "the fitted table the metro presence is read from");
            Out(entry, "underground", "SettlementUndergroundReport",
                "the whole answer: the metro, the underground-road reading and the portal verdict, each with the"
                + " figures behind it -- three independent answers rather than one verdict standing in for all three");
            Out(entry, "metroBuildable", "bool",
                "whether a metro actually gets built here: the fit served it AND the portal can be cut");
            Out(entry, "undergroundRoadPresent", "bool",
                "whether the ground forces a road tunnel -- relief the surface road cannot climb, or a water gap it"
                + " cannot span");
            Out(entry, "reliefSeverity", "double",
                "how hard the surface road would find the grade, 0 on the flat to 1 at the road class's ruling limit");
            Number(entry, "orderIndex", 0.0,
                "which settlement order the metro curve is read for: 1 capital, 2 city, 3 town, 4 village. Zero means"
                + " the order this bake was launched as", false);
            Number(entry, "population", 0.0, "the population the metro presence curve is asked about", true);
            Text(entry, "continentKey", string.Empty,
                "which continent bucket to read, by key. Empty means the world curve", false);
            Text(entry, "identity", string.Empty,
                "the settlement's STABLE identity, keying the metro draw exactly as surface rail is keyed. Empty"
                + " refuses, because a draw with no key would change every bake", true);
            Number(entry, "surfaceSlopeFraction", 0.0,
                "the steepest grade a surface road would climb here, as a rise/run fraction. Read against the road"
                + " gradient below to decide whether relief alone forces a tunnel", false);
            Number(entry, "roadMaxGradientFraction", 0.0,
                "the road class's ruling gradient, as a rise/run fraction. Zero leaves relief unjudged -- \"the surface"
                + " cannot climb\" is a claim that needs the limit it fails against", false);
            Number(entry, "waterGapMetres", 0.0, "the width of a water crossing on the route, in metres", false);
            Number(entry, "surfaceMaxCrossingMetres", 0.0,
                "the longest structure the surface network can span, in metres. A water gap wider than this forces the"
                + " road under; both zero leaves the water reason unjudged", false);
            Flag(entry, "portalOnWater", false,
                "whether the metro's portal falls in open water, which needs B-18's underwater case before it can be"
                + " cut", false);
            Flag(entry, "underwaterCaseAvailable", false,
                "whether that underwater case is available on this bake", false);
            Flag(entry, "portalGroundValid", true,
                "whether the portal site returns ground at all. Off refuses the portal -- there is nothing for a hole"
                + " to be cut into", false);

            entry = Add(list, CityNodeKind.Port, "Port", InfrastructureSection, BakeStage.LoadOrGenerate,
                CityNodeRole.ShippedCode, "PortSiting");
            Out(entry, "port", "PortSitingReport",
                "the whole answer: which kind of port was tested, whether it can stand, and for a water port the vessel"
                + " class the depth admits -- carried beside the depth it rests on, so a barge quay on a shallow lake"
                + " shows the lake was the reason and not the tool");
            Out(entry, "sited", "bool", "whether the port can stand here at all");
            Out(entry, "admittedClass", "VesselClass",
                "for a water port, the largest vessel the navigable depth floats; None when the water is too shallow or"
                + " the kind tested is not a water port");
            Number(entry, "kind", 0.0,
                "which member of the port family to test: 0 a water port decided by depth, 1 a dry port needing rail and"
                + " a trunk road, 2 a shipment yard needing only rail", false);
            Number(entry, "navigableDepthMetres", 0.0,
                "for a water port, the navigable depth read at the quay line, in metres. The depth alone decides the"
                + " vessel: 2.5 m floats an inland barge, 12.04 m a Panamax, 20.1 m a Suezmax", false);
            Flag(entry, "waterReachable", false,
                "for a water port, whether a sea, lake or navigable river was reachable from the site. Off refuses --"
                + " there is no water for a quay to face", false);
            Flag(entry, "railPresent", false,
                "for a dry port or shipment yard, whether a rail spur reaches the site", false);
            Flag(entry, "trunkRoadPresent", false,
                "for a dry port, whether a trunk road reaches the site to clear the containers it lands", false);

            entry = Add(list, CityNodeKind.LandUseShares, "Land-use shares", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "SettlementLandUse");
            In(entry, "settlementFit", "SettlementFitTable",
                "the fitted table the land-use composition is read from -- a Settlement fit source feeds it");
            Out(entry, "shares", "LandUseShares",
                "the whole answer: the residential, commercial, market and industrial shares of developed ground, whether"
                + " the fit was real, and whether the world bucket stood in for a missing continent one");
            Out(entry, "residentialShare", "double", "the fraction of developed ground that is residential");
            Out(entry, "commercialShare", "double",
                "the fraction that is GENERAL commercial -- market ground carried separately, not folded in here");
            Out(entry, "marketShare", "double",
                "the fraction that is market / open trading ground -- the use D-11 distinguishes from general commercial");
            Out(entry, "industrialShare", "double", "the fraction that is industrial");
            Number(entry, "orderIndex", 0.0,
                "which settlement order to read: 1 capital, 2 city, 3 town, 4 village. Zero reads the order this bake was"
                + " launched as", false);
            Text(entry, "continentKey", "",
                "which continent bucket to read, by key. Empty reads the world composition", false);

            entry = Add(list, CityNodeKind.RiverBank, "River bank", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "RiverBank");
            Out(entry, "bank", "BankCrossSection",
                "the whole cross-section: the treatment chosen, the bank height, and the slope-or-wall setback its top"
                + " sits back from the water");
            Out(entry, "treatment", "BankTreatment",
                "which treatment the frontage got: grassed, armoured, or -- when no slope fits -- a retaining wall");
            Out(entry, "bankHeightMetres", "double", "the bank height bed to top: the water depth plus the freeboard");
            Out(entry, "horizontalRunMetres", "double",
                "the top's horizontal setback from the water: the slope run, or the wall base -- the number the frontage"
                + " must fit");
            Out(entry, "railing", "BankRailing", "the pavement-side railing: its post count and spacing, or why none");
            Out(entry, "railPostCount", "int", "how many railing posts the pavement-side top gets, zero when none");
            Number(entry, "waterDepthMetres", 0.0,
                "the design water depth at this station, in metres. The bank rises a freeboard above it; a non-positive"
                + " depth is refused as no channel to bank", false);
            Number(entry, "availableFrontageMetres", 0.0,
                "how much horizontal room the settlement leaves for the bank on this side. A slope that does not fit"
                + " falls to a steeper one, then to a wall -- the choice is width, not taste", false);
            Number(entry, "preferredTreatment", 0.0,
                "the treatment to try first: 0 grassed earth, 1 armoured, 2 a retaining wall outright", false);
            Number(entry, "railingSpacingMetres", 2.0,
                "the spacing of the instanced railing posts along the top, in metres", false);
            Number(entry, "topLengthMetres", 0.0,
                "the length of bank top to rail, in metres. Zero rails nothing", false);
            Flag(entry, "pavementOnThisSide", true,
                "whether this side carries the pavement. The railing is placed on the pavement side only", false);

            entry = Add(list, CityNodeKind.RiverCrossing, "River crossing", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "RiverBank");
            Out(entry, "crossing", "RiverCrossing",
                "the whole answer: the deck family the span was given, its support stations, and how many piers land in"
                + " the water -- refused if any pier the type has no water foundation for does");
            Out(entry, "deckType", "BridgeDeckType", "the deck family the span was given: beam, arch, suspension, ...");
            Out(entry, "supportCount", "int", "how many supports the type places, the two abutments included");
            Out(entry, "intermediatePierCount", "int", "how many supports fall between the abutments");
            Out(entry, "piersInWater", "int", "how many supports land inside the wetted width");
            Out(entry, "piersRefused", "int",
                "how many of those the chosen type has no water foundation for -- any above zero refuses the crossing");
            Number(entry, "spanMetres", 0.0,
                "the crossing span, in metres. A span outside the chosen type's authored range is refused, so a 15 m"
                + " crossing is never given a cable-stayed deck", false);
            Number(entry, "bridgeType", -1.0,
                "which bridge type to use: -1 selects the simplest shipped type the span fits, 0 beam, 1 arch,"
                + " 2 suspension", false);
            Number(entry, "wettedCentreMetres", 0.0,
                "where the river's wetted band is centred along the span, in metres from the start", false);
            Number(entry, "wettedWidthMetres", 0.0,
                "how wide the river runs under the crossing, in metres. A support inside this band lands in water", false);

            entry = Add(list, CityNodeKind.Dam, "Dam", InfrastructureSection,
                BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, "DamWorks");
            In(entry, "terrain", "ITerrainSurfaceReader",
                "the ground the valley section is read across and the reservoir is flooded over. Wired from the linker's"
                + " provider, or taken from the bake's own surface reader when nothing is wired");
            Out(entry, "request", "DamRequest",
                "the whole request Universe Creator answers: the body, the impounded reservoir, the valley section, the"
                + " spillway and outlet discharges, and the tailwater. Until UC's update runs, the dam stands in an"
                + " unchanged river");
            Out(entry, "reservoirSurfaceLevelMetres", "double", "the level the water is held to: crest minus freeboard");
            Out(entry, "reservoirAreaSquareMetres", "double", "the flooded surface area found upstream, in square metres");
            Out(entry, "storageVolumeCubicMetres", "double", "the water impounded above the real ground, in cubic metres");
            Out(entry, "structuralHeightMetres", "double", "how tall the dam stands: crest above the valley floor");
            Out(entry, "spillwayDischargeCumecs", "double", "what the spillway passes at full reservoir, by the weir law");
            Out(entry, "outletDischargeCumecs", "double", "what the outlet passes by the orifice law; zero if no bore");
            Out(entry, "tailwaterLevelMetres", "double", "the water level below the dam: toe ground plus its depth");
            Out(entry, "crestSpansValley", "bool", "true when the crest reaches both abutments; false means too short");
            Out(entry, "reservoirReachedLimit", "bool",
                "true when the flood fill hit its budget before the water ran out, so the valley is likely open at this"
                + " level and the extent is a lower bound");
            Number(entry, "centreX", 0.0, "the dam centre's world X, where the crest line is centred", true);
            Number(entry, "centreZ", 0.0, "the dam centre's world Z", true);
            Number(entry, "axisHeadingDeg", 0.0,
                "the compass heading the crest runs along, in degrees (0 = +Z, 90 = +X)", false);
            Number(entry, "upstreamHeadingDeg", 0.0,
                "the heading towards the reservoir, in degrees. The fill floods this side of the crest line only", true);
            Number(entry, "crestLevelMetres", 0.0,
                "the top of the dam, as a world height. The reservoir is held a freeboard below it", true);
            Number(entry, "freeboardMetres", 1.5,
                "the gap left between full reservoir and crest, in metres. Below 1.5 m the body is refused", false);
            Number(entry, "crestLengthMetres", 0.0,
                "how far the crest runs across the valley, in metres. Read against the abutments the section finds", true);
            Number(entry, "crestWidthMetres", 8.0, "the crest's front-to-back width, in metres", false);
            Number(entry, "upstreamRunPerRise", 0.05,
                "the upstream face batter as run per rise. A gravity dam's upstream face is near-vertical", false);
            Number(entry, "downstreamRunPerRise", 0.75,
                "the downstream face batter as run per rise. About 0.75 for a concrete gravity dam", false);
            Number(entry, "spillwayType", 0.0,
                "which spillway form: 0 ogee overflow, 1 chute, 2 broad-crested. Sets the weir coefficient", false);
            Number(entry, "spillwayCrestLevelMetres", 0.0,
                "the spillway crest level, as a world height. At or above the reservoir it passes nothing", false);
            Number(entry, "spillwayWidthMetres", 0.0, "the spillway's clear width, in metres", false);
            Number(entry, "outletInvertLevelMetres", 0.0, "the low-level outlet's invert, as a world height", false);
            Number(entry, "outletBoreDiameterMetres", 0.0,
                "the outlet bore's diameter, in metres. Zero means no outlet works", false);
            Number(entry, "downstreamWaterDepthMetres", 0.0,
                "the river's natural depth below the dam, in metres, added to the toe ground for the tailwater", false);
            Number(entry, "cellSizeMetres", 5.0,
                "the flood-fill resolution, in metres. Smaller resolves the shore more finely for more samples", false);
            Number(entry, "maxFloodCells", 120000.0,
                "the flood-fill cell budget. Hitting it is reported, not hidden -- the reservoir becomes a lower bound",
                false);
            Number(entry, "valleySamples", 33.0, "how many points the valley section is read at across the crest", false);
        }
        /// <summary> The eight nodes of the four stages after the plan: publishing footprints, conforming the ground to
        /// them, keeping scatter out of them, and producing the runtime form. Their stages are the ones
        /// <see cref="BakeStage"/> declares, and the graph refuses a wire that runs backwards through them -- so a node
        /// here can read the plan, and no node of the plan can read these. </summary>
        private static void AddLaterStages(List<CityNodeDescriptor> list)
        {
            CityNodeDescriptor entry = Add(list, CityNodeKind.FootprintPublish, "Footprint publish", TerrainSection,
                BakeStage.PublishFootprints, CityNodeRole.ShippedCode, "FootprintMask");
            In(entry, "region", "Rect", "the rectangle the mask covers");
            In(entry, "footprints", "List<List<Vector3>>",
                "everything the city has claimed: carriageways, lots, pads, rail, easements");
            Out(entry, "mask", "FootprintMaskClass",
                "coverage 0..1 per pixel with a feathered edge, which is what the terrain stage and the scatter stage"
                + " both read -- one mask, so they cannot disagree about where the city is");
            Number(entry, "metresPerPixel", 4.0,
                "the mask's resolution. Four metres is the shipped default: a pixel is smaller than a lot and larger"
                + " than a kerb", true);
            Number(entry, "featherWidthMetres", 8.0,
                "how wide the soft edge is, so scatter thins towards the city instead of stopping at a line", true);

            entry = Add(list, CityNodeKind.FootprintConformNode, "Footprint conform", TerrainSection,
                BakeStage.ConformTerrain, CityNodeRole.ShippedCode, "FootprintConform.Conform");
            In(entry, "cells", "List<ConformCell>", "each cell the mask covers, with the height it must reach");
            In(entry, "mask", "FootprintMaskClass",
                "the mask those cells were read from. Optional, and wired only to make the cell area below mean what it"
                + " says: without it, a cell area of zero has nothing to fall back to and the pass refuses rather than"
                + " quoting volumes computed against an area of nothing");
            Out(entry, "report", "ConformReport", "how much ground moved, and how much of it was cut against fill");
            Out(entry, "removals", "List<ConformRemoval>", "per cell, what was taken away or added, in metres");
            Number(entry, "cellAreaM2", 0.0,
                "the area one cell stands for, for the volume figures. Zero means the mask's own pixel area, which is"
                + " the area that is actually true", false);

            entry = Add(list, CityNodeKind.BuildingPadConformNode, "Building pad conform", TerrainSection,
                BakeStage.ConformTerrain, CityNodeRole.ShippedCode, "BuildingPadConform.ComputePads");
            In(entry, "requests", "List<PadRequest>", "one per building: its footprint and the rule for its level");
            In(entry, "terrain", "IPCGDataProvider", "the ground the pads are cut into");
            In(entry, "padParams", "PadConformParams", "the freeboard, the batter and the pad's elevation rule");
            Out(entry, "pads", "List<PadRecord>",
                "each pad's level and the ground it moves, in lot-id order -- which is why a re-bake produces the same"
                + " pads whatever order the lots arrived in");

            entry = Add(list, CityNodeKind.HandPlacedScan, "Hand-placed scan", TerrainSection,
                BakeStage.ConformTerrain, CityNodeRole.ShippedCode, "HandPlacedGroundScan.Sweep");
            In(entry, "terrain", "ITerrainSurfaceReader",
                "the ground under the author's objects. Left unwired it falls back to the reader the bake was given,"
                + " which is the usual case: this port is for scanning against a surface other than the live one");
            Out(entry, "findings", "List<FootprintScanFinding>",
                "one per object: what the ground under it does, with a number and a unit, in the order the objects were"
                + " found");
            Out(entry, "padRequests", "List<PadRequest>",
                "the levelling this sweep asks for, empty unless produceGroundRequests is on -- wire it to the building"
                + " pad conform's requests, which is the only thing that turns a request into moved ground");
            Flag(entry, "produceGroundRequests", false,
                "off: report what is wrong and change nothing. On, the objects outside tolerance ask for a pad and an"
                + " apron. Off by default because the footprint is a rectangle around the object's bounds, which is"
                + " generous for anything turned on the spot -- generous is right for a report and wrong for a cut",
                false);
            Number(entry, "supportToleranceMetres", 0.025,
                "how far the ground may stand from an object's base before it is worth saying so. The finished-formation"
                + " earthworks tolerance, and the same figure the road side uses", false);
            Number(entry, "visibleMismatchMetres", 0.150,
                "the gap a person sees rather than measures -- one kerb upstand. Past this the finding stops being"
                + " advice", false);
            Number(entry, "maximumStationSpacingMetres", 3.0,
                "the widest gap between two ground samples under one object. The station spacing is otherwise DERIVED"
                + " from the footprint, so this is a ceiling and not the spacing", false);
            Number(entry, "maximumObjects", 4096.0,
                "how many objects one sweep looks at. A guard against naming a scene root instead of a container:"
                + " reaching it is reported, not silently accepted", false);
            Flag(entry, "includeInterior", true,
                "sample inside the footprint as well as around its edge. On, because a bump under the middle of a slab"
                + " lifts no corner: all four read low together, which an edge-only sweep calls a hollow", false);
            Text(entry, "contactMode", "SkirtToTerrain",
                "how these objects meet the ground: OnPad, SkirtToTerrain or Piled. Piled objects are meant to stand"
                + " clear, so the gap under them is not judged -- the route a bridge deck already takes", false);
            Text(entry, "elevationRule", "CentroidSample",
                "the level a requested pad is cut to: CentroidSample or MinCornerPlusFreeboard. The pad conform reads"
                + " this from the request, so it is set here rather than guessed there", false);

            entry = Add(list, CityNodeKind.ScatterExclusion, "Scatter exclusion", TerrainSection,
                BakeStage.BakeScatter, CityNodeRole.ShippedCode, "RM-15's scatter exclusion pass");
            In(entry, "mask", "FootprintMaskClass", "where the city is, so scatter is kept out of it");
            Out(entry, "exclusion", "FootprintMaskClass", "the mask the scatter pass subtracts");
            entry.blockedReason = "the scatter exclusion pass is RM-15, and RM-15 waits on RM-14, which is not written"
                                  + " yet. This node is in the catalogue so a graph can be wired now and not rewritten"
                                  + " later; until RM-15 lands it reports this sentence and does nothing";
            entry = Add(list, CityNodeKind.ChunkGrid, "Chunk grid", RuntimeSection, BakeStage.ProduceRuntime,
                CityNodeRole.ShippedCode, "CityChunkGrid");
            In(entry, "position", "Vector3", "a world position to place in the grid");
            Out(entry, "chunk", "ChunkCoord", "which chunk that position falls in");
            Out(entry, "centre", "Vector3", "that chunk's centre, for the streaming distance");
            Number(entry, "chunkSizeMetres", 100.0,
                "the chunk edge in metres. A hundred is the streaming config's own default, and the two must agree --"
                + " wire the config in rather than typing a second figure here", true);

            entry = Add(list, CityNodeKind.SpawnTable, "Spawn table", RuntimeSection, BakeStage.ProduceRuntime,
                CityNodeRole.ShippedCode, "CitySpawnTable.Build");
            In(entry, "city", "CityData", "the baked city: its buildings, its lots and its seed");
            Out(entry, "spawnTable", "CitySpawnTable",
                "what to instantiate in each chunk -- the runtime form Q-6 chose: prefab instances, not a mesh");
            Number(entry, "chunkSizeMetres", 100.0, "the same chunk edge the grid and the streaming config use", true);

            entry = Add(list, CityNodeKind.Streaming, "Streaming", RuntimeSection, BakeStage.ProduceRuntime,
                CityNodeRole.ShippedCode, "CityStreamingScheduler and CityStreamingConfig");
            In(entry, "config", "CityStreamingConfig",
                "chunk size, load ring, the three LOD bands and the per-frame instantiation budget");
            In(entry, "spawnTable", "CitySpawnTable", "what stands in each chunk");
            In(entry, "cameraChunk", "ChunkCoord", "where the viewer is");
            Out(entry, "chunksToLoad", "List<ChunkCoord>", "the chunks inside the load ring, nearest first");
            Out(entry, "instantiateThisFrame", "List<int>",
                "the building indices this frame's budget allows, in the order they were queued -- a budget rather than"
                + " a burst, so arriving in a new chunk does not drop a frame");

            entry = Add(list, CityNodeKind.BakeStampNode, "Bake stamp", RuntimeSection, BakeStage.ProduceRuntime,
                CityNodeRole.ShippedCode, "BakeStamp.Create");
            In(entry, "city", "CityData", "the city being stamped");
            Out(entry, "stamp", "BakeStamp",
                "what produced this bake: the generator version, the source asset and its seed, so a stale bake can be"
                + " recognised as stale instead of trusted");
        }
        /// <summary> The two nodes that say something to a person and nothing to the bake. They have no ports at all, and
        /// the graph refuses any wire that touches them -- a comment that could be wired would eventually be wired, and
        /// then the graph's order would depend on a comment.
        ///
        /// The override marker is here, and not among the stages, because marking a lot hand-owned is not something a
        /// bake does: <c>CityEditOps.MarkOverridden</c> is called by the editor when the author drags a handle, and the
        /// bake then honours the flag on <c>CityData</c>. What this node adds is the sentence on the canvas -- so an
        /// author who wonders why a re-bake left one block alone can read the answer next to the node that produced it,
        /// rather than discovering it in a data field. </summary>
        private static void AddCanvasOnly(List<CityNodeDescriptor> list)
        {
            CityNodeDescriptor entry = Add(list, CityNodeKind.OverrideMarker, "Override marker", CanvasSection,
                BakeStage.LoadOrGenerate, CityNodeRole.CanvasOnly, "CityEditOps.MarkOverridden and CityData");
            Text(entry, "element", "lot",
                "which kind of thing is hand-owned: block, lot or lotCorner. The bake preserves every overridden block"
                + " and lot and recomputes everything else, which is the whole definition of a re-bake", true);
            Number(entry, "id", 0.0, "that element's id -- a Block.id or a Lot.id, the same id the scene handles use",
                true);
            Text(entry, "note", string.Empty, "why it is hand-owned, for the next person to open this graph", false);

            entry = Add(list, CityNodeKind.CommentGroup, "Comment", CanvasSection, BakeStage.LoadOrGenerate,
                CityNodeRole.CanvasOnly, string.Empty);
            Text(entry, "title", "Comment", "the heading drawn on the group", true);
            Text(entry, "body", string.Empty, "the note itself", false);
        }
        /// <summary> The element entries: one per manifest category, GENERATED rather than hand-written.
        ///
        /// <c>PrefabManifest.Categories</c> is the element list -- every category in every section -- so this method
        /// adds them all and the catalogue grows by no new file. The grouping is the manifest's own <c>section</c>, so
        /// the search tree's element groups and the manifest's sections cannot drift apart: there is one list of
        /// section names in the tool and it is the manifest's.
        ///
        /// Every entry is the same node type, <see cref="CityNodeKind.ElementPlacement"/>, told apart by its
        /// <c>category</c> parameter. That is why <c>TryGet</c> refuses that kind and <see cref="TryGetElement"/>
        /// answers for it: the kind alone does not name an entry, the kind and the category together do.
        ///
        /// An entry outputs a placement SLOT and never a prefab. <c>PrefabPlacementSpec</c> is the whole division of
        /// labour B-15 states: the tool places the slot, the author's own prefab fills it. It is also why a category
        /// holding no assets in this build still leaves a wireable graph -- the slot is computed either way, and RM-55
        /// refuses the BUILD by name rather than this node silently placing nothing. </summary>
        private static void AddElements(List<CityNodeDescriptor> list)
        {
            IReadOnlyList<ManifestCategory> categories = PrefabManifest.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ManifestCategory category = categories[i];
                CityNodeDescriptor entry = Add(list, CityNodeKind.ElementPlacement, category.displayName,
                    category.section, BakeStage.LoadOrGenerate, CityNodeRole.ShippedCode, category.consumedBy);
                entry.elementCategoryId = category.categoryId;
                In(entry, "frontage", "List<Vector3>",
                    "the line this category is placed along: a street frontage, a park path, a platform edge. Point"
                    + " order is the direction of travel along it, and stations are measured in plan metres");
                In(entry, "content", "ContentCatalog",
                    "the author's prefabs, so a slot can name a variant that actually exists");
                In(entry, "streetSideXZ", "Vector2",
                    "any point on the side of the frontage the instances should FACE, in plan metres -- the same trick"
                    + " BuildingPlacement uses the lot centroid for. Unwired, they face the left of travel and say so");
                In(entry, "seed", "int",
                    "the item seed, from CitySeed.ForItem, so the same frontage places the same way every rebuild");
                In(entry, "terrain", "ITerrainSurfaceReader",
                    "the ground to seat these on, from the Terrain surface node. Wire it AFTER the road and pad conform"
                    + " in the bake order or a bench is pulled to the ground the verge beside it used to have."
                    + " Unwired, the reader the bake itself carries is used and the node says so");
                Out(entry, "slots", "List<PrefabPlacementSpec>",
                    "what each instance is: the category, the variant the seeded draw chose, and how it meets the"
                    + " ground. A spec carries no transform, which is why the two ports below exist rather than one");
                Out(entry, "positions", "List<Vector3>",
                    "where each instance stands, in world metres on the frontage line, index-matched to the slots");
                Out(entry, "facings", "List<Quaternion>",
                    "which way each instance turns: across the frontage and away from it, index-matched to the slots");
                Text(entry, ElementCategoryParameter, category.categoryId,
                    "which manifest category this node places. It is the node's identity rather than a preference,"
                    + " which is why it is required and why the search window writes it", true);
                Flag(entry, "alignToSurfaceNormal", false,
                    "whether these lie on the local slope instead of standing plumb. On for a bench, a bin, a picnic"
                    + " table, a paving slab -- anything that RESTS on the ground. Off for anything that grows or is"
                    + " driven into it: a tree, a lamp column, a post, a sign, a bollard. Off is the default because a"
                    + " stiff bench reads as stiff and a tilted tree reads as broken", false);
                AddOrderRows(entry, category);
            }
        }
        /// <summary> The four settlement orders' four parameter rows on one element entry, named through
        /// <see cref="OrderParameterPrefix"/> so the runner reads each row under the same name the catalogue wrote.
        ///
        /// Per order rather than per world because a bench stands at one spacing on a capital's boulevard and at
        /// another on a village lane, and an author must be able to say which without editing the graph twice.
        ///
        /// A rate or spacing of ZERO is UNAUTHORED, and the runner refuses the node and names the row to fill. The tool
        /// ships no per-kilometre table and no per-order spacing table -- a grep of the whole runtime finds neither --
        /// so reading zero as "the rule's own figure" would be a promise to fetch a number from a place that does not
        /// exist, and reading it as "none" would place a bench every zero metres. Refusing is the only answer that is
        /// true: how many benches belong on a capital's boulevard is a design decision, and the author is the only one
        /// here who holds it. <c>present</c> defaults to on for the mirror-image reason: the manifest records which
        /// FEATURES consume a category (<c>consumedBy</c>) and nothing anywhere records which ORDERS do, so switching an
        /// order off is an authored exclusion and cannot honestly be a shipped default. A node wired with every order
        /// off would place nothing, which is not a default, it is a mistake waiting to be reported.
        ///
        /// C-17's <c>embedDepthMetres</c> is the one row here that HAS a shipped answer for its default, which is why it
        /// defaults to -1 rather than 0: the depth a base needs is geometry -- the footprint radius times the tangent of
        /// the local slope -- and a tool that can compute a figure should not make an author type it. Zero is left
        /// meaning zero, because "sit exactly on the surface" is a thing an author can mean. </summary>
        private static void AddOrderRows(CityNodeDescriptor entry, ManifestCategory category)
        {
            for (int order = SettlementOrderCatalog.CapitalOrder; order <= SettlementOrderCatalog.VillageOrder; order++)
            {
                string prefix = OrderParameterPrefix(order);
                if (string.IsNullOrEmpty(prefix)) { continue; }
                Flag(entry, prefix + ".present", true,
                    "whether a " + prefix + " places " + category.displayName + " at all. Off is an authored"
                    + " exclusion: the manifest records which features consume this category, not which orders do",
                    false);
                Number(entry, prefix + ".perKilometreOfFrontage", 0.0,
                    "how many of these per kilometre of frontage in a " + prefix
                    + ". Required when this order is present: 0 is unauthored and the bake refuses by name", false);
                Number(entry, prefix + ".minSpacingMetres", 0.0,
                    "the closest two of these may stand in a " + prefix
                    + ", in metres. Required when this order is present: 0 is unauthored and the bake refuses by name",
                    false);
                Number(entry, prefix + ".embedDepthMetres", -1.0,
                    "how far below the ground the base of one of these sits in a " + prefix
                    + ", in metres. Per order because a village's verge is rougher than a capital's kerbline. Leave it"
                    + " at -1 and the depth is worked out from the prefab's own footprint radius and the local slope,"
                    + " which is the answer that removes the daylight under a base rather than a number picked here."
                    + " 0 means sit exactly on the surface; the tool refuses to sink one deeper than 0.15 m, which is"
                    + " about a kerb upstand and past which it is being buried rather than seated", false);
            }
        }
    }
}

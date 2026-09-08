#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-6. The half of the bake that knows what one node MEANS: it takes a node whose inputs are on
    /// the bus and calls the shipped code the catalogue's owner names, or the operator asset the node wires.
    ///
    /// Split from <see cref="CityBakeRunner"/> because the two answer different questions. The runner answers "in what
    /// order, timed how, recorded where" and never mentions a road class; this answers "what does a rail node actually
    /// call" and never mentions a stopwatch. Kept in one switch rather than spread across the shipped classes so that
    /// the whole of the graph's contact with shipped code is in one file a reader can check against the catalogue --
    /// a per-class Bake method would put the same knowledge in thirty places and let one of them drift.
    ///
    /// A node that cannot run is PASSED OVER with a sentence, never thrown and never logged as unimplemented. The
    /// distinction matters: "nothing is wired into the port block" is a fact about the graph and the author can act on
    /// it, whereas "not implemented" would be a fact about the tool that no author can act on. Exactly one entry in the
    /// catalogue is genuinely unbuilt, it says so in its own <c>blockedReason</c>, and that sentence is what this
    /// reports for it. </summary>
    public static class CityNodeDispatch
    {
        /// <summary> Runs one node. Returns false when the node was passed over, with the reason in
        /// <paramref name="note"/>; false is not a failure, and the runner counts it separately for that reason.
        ///
        /// <paramref name="itemCount"/> is negative when the node produces no countable list -- a raster, a verdict, a
        /// stamp -- which is a different statement from producing an empty one. </summary>
        public static bool Execute(CityBakeContext context, CityGraphNode node, int operatorSeed, out int itemCount,
            out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (context == null || node == null)
            {
                note = "there is no node to run";
                return false;
            }

            CityNodeDescriptor descriptor;
            string why;
            if (!CityNodeCatalog.TryGetForNode(node, out descriptor, out why))
            {
                note = why;
                return false;
            }

            if (descriptor.IsBlocked)
            {
                note = descriptor.blockedReason;
                return false;
            }

            if (!CityNodeCatalog.Executes(node.kind))
            {
                note = "a " + descriptor.displayName.ToLowerInvariant() + " says something to a person and nothing to"
                       + " the bake, so there is nothing to run";
                return false;
            }

            if (CityNodeCatalog.NeedsOperatorAsset(node.kind))
            {
                return RunOperatorAsset(context, node, operatorSeed, out itemCount, out note);
            }

            return RunShippedCode(context, node, descriptor, operatorSeed, out itemCount, out note);
        }

        #region "Operator assets"

        /// <summary> The four kinds that run an author's ScriptableObject rather than shipped code: the street network,
        /// the block subdivision, the site test and the site rank.
        ///
        /// The input is assembled per kind and by CONSTRUCTOR, not by matching port names to field names. A name match
        /// would look general and be wrong on the first case that matters: the street network's port is called
        /// <c>region</c> and the field it fills is called <c>regionXZ</c>, and the block subdivision's field is
        /// <c>readonly</c> and cannot be assigned at all. Four explicit cases are longer and cannot silently fill the
        /// wrong field. </summary>
        private static bool RunOperatorAsset(CityBakeContext context, CityGraphNode node, int operatorSeed,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            ICityOperator op;
            string why;
            if (!node.TryGetOperator(out op, out why))
            {
                note = why;
                return false;
            }

            object input;
            if (!TryBuildOperatorInput(context, node, out input, out note)) { return false; }

            object output;
            if (!TryInvokeOperator(op, input, operatorSeed, out output, out note)) { return false; }

            return PublishOperatorOutput(context, node, output, out itemCount, out note);
        }

        /// <summary> Builds the one object the operator's <c>Execute</c> reads, from the values on the node's input
        /// ports. Returns false with the missing port named, which is the sentence an author can act on. </summary>
        private static bool TryBuildOperatorInput(CityBakeContext context, CityGraphNode node, out object input,
            out string note)
        {
            input = null;
            note = string.Empty;

            switch (node.kind)
            {
                case CityNodeKind.StreetNetwork:
                {
                    IPCGDataProvider ground;
                    Rect region;
                    if (!TryInput(context, node, "terrain", out ground, out note)) { return false; }
                    if (!TryInput(context, node, "region", out region, out note)) { return false; }
                    input = new StreetNetworkInput(ground, region);
                    return true;
                }

                case CityNodeKind.BlockSubdivision:
                {
                    StreetNetwork net;
                    if (!TryInput(context, node, "network", out net, out note)) { return false; }
                    input = new BlockSubdivisionInput(net);
                    return true;
                }

                case CityNodeKind.SiteRank:
                {
                    SiteVerdictSet verdicts;
                    if (!TryInput(context, node, "verdicts", out verdicts, out note)) { return false; }
                    input = verdicts;
                    return true;
                }

                case CityNodeKind.SiteTest:
                    return TryBuildSiteTestInput(context, node, out input, out note);
            }

            note = "the kind " + node.kind + " is wired to an operator asset, but this runner does not know what input"
                   + " to build for it";
            return false;
        }

        /// <summary> The site test's input, which is the one operator input with more parts than a constructor takes.
        /// Its own <c>IsUsable</c> is asked before the operator runs, so the refusal an author reads is the operator's
        /// sentence rather than this runner's paraphrase of it.
        ///
        /// The suitability raster and the candidate regions are required -- there is no site test without a map and
        /// somewhere to test. The rest are optional in the same way the suitability builder treats them: a missing
        /// zone or road class narrows what the test can say, and the operator says so itself. </summary>
        private static bool TryBuildSiteTestInput(CityBakeContext context, CityGraphNode node, out object input,
            out string note)
        {
            input = null;
            note = string.Empty;

            SuitabilityRasterClass map;
            List<Rect> candidates;
            if (!TryInput(context, node, "suitability", out map, out note)) { return false; }
            if (!TryInput(context, node, "candidateRegions", out candidates, out note)) { return false; }

            SiteTestInput built = new SiteTestInput();
            built.suitability = map;
            built.candidateRegionsXZ = candidates;

            string ignored;
            ITerrainSurfaceReader surface;
            if (TryInput(context, node, "terrain", out surface, out ignored)) { built.surface = surface; }

            float waterY;
            if (TryInput(context, node, "waterSurfaceY", out waterY, out ignored)) { built.waterSurfaceY = waterY; }

            ZoneTypeClass zone;
            if (TryInput(context, node, "zone", out zone, out ignored)) { built.zone = zone; }

            RoadClass roadClass;
            if (TryInput(context, node, "roadClass", out roadClass, out ignored)) { built.roadClass = roadClass; }

            ISuitabilityNetworkSource network;
            if (TryInput(context, node, "network", out network, out ignored)) { built.network = network; }

            string unusable;
            if (!built.IsUsable(out unusable))
            {
                note = unusable;
                return false;
            }

            input = built;
            return true;
        }

        /// <summary> Calls the operator's <c>Execute</c> by reflection, because there is no non-generic one to call.
        ///
        /// <see cref="ICityOperator"/> exposes only the two Types, and <c>CityOperator&lt;TIn,TOut&gt;.Execute</c> is
        /// declared on the generic class, so a caller holding the interface cannot invoke it directly. The alternative
        /// -- adding a non-generic <c>Execute(object, int)</c> to the operator base -- was rejected because it would
        /// change what every operator an author writes has to implement, in order to serve the runner; and because an
        /// <c>object</c> in that signature would move the type check from the compiler to run time for all of them, not
        /// just for the four the graph runs today.
        ///
        /// The cost is one reflective call per operator NODE per bake, not per item: a graph with four operator nodes
        /// pays four lookups, and the work inside each call is unaffected.
        ///
        /// An exception out of an author's operator is reported as that node's note rather than allowed to end the bake.
        /// One badly authored operator asset should cost its own node and nothing else -- the report names the operator
        /// and repeats its message, so the failure is not silent either. </summary>
        private static bool TryInvokeOperator(ICityOperator op, object input, int operatorSeed, out object output,
            out string note)
        {
            output = null;
            note = string.Empty;

            MethodInfo execute = op.GetType().GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance, null,
                new Type[] { op.InputType, typeof(int) }, null);

            if (execute == null)
            {
                note = "the operator asset " + op.GetType().Name + " has no Execute that takes a "
                       + op.InputType.Name + " and a seed, so it cannot be run from a graph";
                return false;
            }

            try
            {
                output = execute.Invoke(op, new object[] { input, operatorSeed });
            }
            catch (TargetInvocationException wrapper)
            {
                Exception cause = wrapper.InnerException == null ? wrapper : wrapper.InnerException;
                note = "the operator asset " + op.GetType().Name + " stopped on this input: " + cause.Message;
                return false;
            }

            return true;
        }

        /// <summary> Puts what the operator returned onto the ports the catalogue says it has, and counts what it made.
        ///
        /// The note on a clean run is empty; it is filled only when the operator produced less than it was asked for --
        /// segments it rejected, blocks that collapsed, a site it refused to choose. That is the one thing a person
        /// reading a bake wants explained, and it is a statement about what did NOT happen, which is what the field is
        /// for. </summary>
        private static bool PublishOperatorOutput(CityBakeContext context, CityGraphNode node, object output,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            switch (node.kind)
            {
                case CityNodeKind.StreetNetwork:
                {
                    StreetNetwork net = output as StreetNetwork;
                    if (net == null) { note = WrongOutput(node, "a StreetNetwork"); return false; }
                    context.bus.Publish(node.id, "network", net);
                    itemCount = net.edges.Count;
                    if (net.report.accepted < net.report.proposed) { note = net.report.ToString(); }
                    return true;
                }

                case CityNodeKind.BlockSubdivision:
                {
                    BlockSubdivisionResult result = output as BlockSubdivisionResult;
                    if (result == null) { note = WrongOutput(node, "a BlockSubdivisionResult"); return false; }
                    context.bus.Publish(node.id, "subdivision", result);
                    context.bus.Publish(node.id, "blocks", result.blocks);
                    context.bus.Publish(node.id, "lots", result.lots);
                    itemCount = result.lots.Count;
                    if (result.report.collapsed > 0) { note = result.report.ToString(); }
                    return true;
                }

                case CityNodeKind.SiteTest:
                {
                    SiteVerdictSet verdicts = output as SiteVerdictSet;
                    if (verdicts == null) { note = WrongOutput(node, "a SiteVerdictSet"); return false; }
                    context.bus.Publish(node.id, "verdicts", verdicts);
                    itemCount = verdicts.verdicts.Count;
                    if (!verdicts.ran) { note = verdicts.refusalReason; }
                    return true;
                }

                case CityNodeKind.SiteRank:
                {
                    SiteChoice choice = output as SiteChoice;
                    if (choice == null) { note = WrongOutput(node, "a SiteChoice"); return false; }
                    context.bus.Publish(node.id, "choice", choice);
                    if (choice.chosen) { context.bus.Publish(node.id, "region", choice.chosenRegionXZ); }
                    itemCount = choice.rankings.Count;
                    if (!choice.chosen) { note = choice.refusalReason; }
                    return true;
                }
            }

            note = "the kind " + node.kind + " ran an operator asset, but this runner does not know which port its"
                   + " output leaves by";
            return false;
        }

        /// <summary> The one sentence for an operator whose output is not the type its node's port declares. It can only
        /// happen when an operator asset of the wrong kind is dropped on a node, which the graph's own validation does
        /// not catch: validation compares the node to the catalogue, and a node holding the wrong asset still matches
        /// its own catalogue entry. </summary>
        private static string WrongOutput(CityGraphNode node, string expected)
        {
            return "the operator asset on " + node.DisplayName() + " returned something that is not " + expected
                   + ", so the wrong kind of operator is wired to this node";
        }

        #endregion

        #region "Shipped code"

        /// <summary> The kinds that call shipped classes rather than an author's asset.
        ///
        /// Remarks -- a parameter that was missing and the figure that stood in for it -- are folded into the note only
        /// when the node RAN. On a refusal the note is the one sentence the author has to act on, and appending "and a
        /// default was used" to it would bury that sentence under a lesser one. </summary>
        private static bool RunShippedCode(CityBakeContext context, CityGraphNode node, CityNodeDescriptor descriptor,
            int operatorSeed, out int itemCount, out string note)
        {
            List<string> remarks = new List<string>();
            bool ran = RunByKind(context, node, descriptor, operatorSeed, remarks, out itemCount, out note);

            if (ran && remarks.Count > 0)
            {
                note = string.IsNullOrEmpty(note) ? Join(remarks) : note + "; " + Join(remarks);
            }

            return ran;
        }

        /// <summary> The one switch. Every kind the catalogue marks as shipped code appears here exactly once, in the
        /// order the catalogue declares them, so a reader can hold the two open side by side and see that neither has
        /// an entry the other lacks. The grouping is by the part of the pipeline a kind belongs to, so "what does a rail
        /// node do" is answered by one short method rather than by a position in a three-hundred-line switch. </summary>
        private static bool RunByKind(CityBakeContext context, CityGraphNode node, CityNodeDescriptor descriptor,
            int operatorSeed, List<string> remarks, out int itemCount, out string note)
        {
            switch (node.kind)
            {
                case CityNodeKind.Region:
                case CityNodeKind.TerrainSurface:
                case CityNodeKind.WaterSurface:
                case CityNodeKind.RoadClassSource:
                case CityNodeKind.ZoneCatalogueSource:
                case CityNodeKind.PrefabLibrary:
                case CityNodeKind.CitySeedSource:
                case CityNodeKind.SettlementFitSource:
                    return RunSource(context, node, remarks, out itemCount, out note);

                case CityNodeKind.SuitabilityRaster:
                case CityNodeKind.CorridorSolve:
                    return RunPlan(context, node, remarks, out itemCount, out note);

                case CityNodeKind.PlanarFaces:
                case CityNodeKind.BlockInset:
                case CityNodeKind.LotSubdivision:
                    return RunBlocks(context, node, operatorSeed, remarks, out itemCount, out note);

                case CityNodeKind.BuildableLand:
                case CityNodeKind.ZonePaint:
                case CityNodeKind.PrefabValidate:
                case CityNodeKind.BuildingPlacement:
                    return RunLand(context, node, remarks, out itemCount, out note);

                case CityNodeKind.GradeSeparationNode:
                case CityNodeKind.SurfaceType:
                case CityNodeKind.PolesAndSpans:
                case CityNodeKind.Rail:
                case CityNodeKind.Ropeway:
                    return RunInfrastructure(context, node, remarks, out itemCount, out note);

                case CityNodeKind.SettlementRail:
                case CityNodeKind.Transit:
                case CityNodeKind.Underground:
                    return RunSettlement(context, node, remarks, out itemCount, out note);

                case CityNodeKind.Port:
                    return RunPort(context, node, remarks, out itemCount, out note);

                case CityNodeKind.LandUseShares:
                    return RunLandUse(context, node, remarks, out itemCount, out note);

                case CityNodeKind.RiverBank:
                    return RunRiverBank(context, node, remarks, out itemCount, out note);

                case CityNodeKind.RiverCrossing:
                    return RunRiverCrossing(context, node, remarks, out itemCount, out note);

                case CityNodeKind.Dam:
                    return RunDam(context, node, remarks, out itemCount, out note);

                case CityNodeKind.ElementPlacement:
                    return RunElementPlacement(context, node, descriptor, operatorSeed, remarks, out itemCount,
                        out note);

                case CityNodeKind.FootprintPublish:
                case CityNodeKind.FootprintConformNode:
                case CityNodeKind.BuildingPadConformNode:
                case CityNodeKind.HandPlacedScan:
                case CityNodeKind.ChunkGrid:
                case CityNodeKind.SpawnTable:
                case CityNodeKind.Streaming:
                case CityNodeKind.BakeStampNode:
                    return RunLaterStages(context, node, remarks, out itemCount, out note);
            }

            itemCount = -1;
            note = "the catalogue says a " + descriptor.displayName.ToLowerInvariant() + " runs " + descriptor.owner
                   + ", but this runner has no case for it";
            return false;
        }

        /// <summary> The seven sources. A source does not compute: it hands on what the request carries, and refuses
        /// when the request carries nothing rather than publishing a null for a later stage to trip over. The refusal
        /// an author then reads names the graph -- "this bake carries no ground" -- instead of surfacing three stages
        /// away, wherever that null happened to be dereferenced first. </summary>
        private static bool RunSource(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;
            CityBakeRequest request = context.request;

            switch (node.kind)
            {
                case CityNodeKind.Region:
                    if (request.region.width <= 0f || request.region.height <= 0f)
                    {
                        note = "this bake carries no region: the rectangle measures "
                               + request.region.width.ToString(CultureInfo.InvariantCulture) + " by "
                               + request.region.height.ToString(CultureInfo.InvariantCulture)
                               + " metres, and a city with no area has nowhere to put a street";
                        return false;
                    }
                    context.bus.Publish(node.id, "region", request.region);
                    return true;

                case CityNodeKind.TerrainSurface:
                    if (request.surface == null && request.terrain == null)
                    {
                        note = "this bake carries no ground, so neither the terrain port nor the provider port has"
                               + " anything to hand on";
                        return false;
                    }
                    if (request.surface != null) { context.bus.Publish(node.id, "terrain", request.surface); }
                    else { remarks.Add("no height reader was supplied, so the terrain port published nothing"); }
                    if (request.terrain != null) { context.bus.Publish(node.id, "provider", request.terrain); }
                    else { remarks.Add("no linker provider was supplied, so the provider port published nothing"); }
                    return true;

                case CityNodeKind.WaterSurface:
                    if (!Flag(node, "hasWater", false, remarks))
                    {
                        context.bus.Publish(node.id, "waterSurfaceY", float.NegativeInfinity);
                        note = "this region carries no water, so the water level was published as negative infinity --"
                               + " which is what the port promises for a world with no water, and what makes every"
                               + " water test downstream a no-op rather than a comparison against zero";
                        return true;
                    }
                    context.bus.Publish(node.id, "waterSurfaceY", (float)Number(node, "waterSurfaceY", 0.0, remarks));
                    return true;

                case CityNodeKind.RoadClassSource:
                    if (request.roadClasses == null || request.roadClasses.classes == null
                        || request.roadClasses.classes.Count == 0)
                    {
                        note = "this bake carries no road class catalogue, so there is no class for a street to be"
                               + " built as";
                        return false;
                    }
                    context.bus.Publish(node.id, "roadClasses", request.roadClasses);
                    itemCount = request.roadClasses.classes.Count;
                    {
                        int classId = (int)Number(node, "classId", RoadClassCatalog.ArterialId, remarks);
                        RoadClass chosen;
                        if (request.roadClasses.TryGet(classId, out chosen))
                        {
                            context.bus.Publish(node.id, "roadClass", chosen);
                        }
                        else
                        {
                            remarks.Add("the catalogue defines no class with id "
                                        + classId.ToString(CultureInfo.InvariantCulture)
                                        + ", so the roadClass port published nothing and only the catalogue was handed"
                                        + " on");
                        }
                    }
                    return true;

                case CityNodeKind.ZoneCatalogueSource:
                    if (request.zones == null || request.zones.Count == 0)
                    {
                        note = "this bake carries no zone catalogue, so there is no zone for a lot to be painted as";
                        return false;
                    }
                    context.bus.Publish(node.id, "zones", request.zones);
                    itemCount = request.zones.Count;
                    {
                        string zoneId = Text(node, "zoneId", string.Empty, remarks);
                        if (string.IsNullOrEmpty(zoneId))
                        {
                            remarks.Add("no zoneId was named, so the zone port published nothing and only the"
                                        + " catalogue was handed on");
                        }
                        else
                        {
                            ZoneTypeClass zone = request.zones.Resolve(zoneId);
                            if (zone != null) { context.bus.Publish(node.id, "zone", zone); }
                            else
                            {
                                remarks.Add("the catalogue defines no zone called " + zoneId + ", so the zone port"
                                            + " published nothing -- the catalogue is fail-closed and does not"
                                            + " substitute a default zone for one an author misspelled");
                            }
                        }
                    }
                    return true;

                case CityNodeKind.PrefabLibrary:
                    if (request.content == null)
                    {
                        note = "this bake carries no prefab library, so the placement passes would have nothing to ask"
                               + " for -- and they never create content of their own";
                        return false;
                    }
                    context.bus.Publish(node.id, "content", request.content);
                    return true;

                case CityNodeKind.CitySeedSource:
                    context.bus.Publish(node.id, "citySeed", request.citySeed);
                    return true;

                case CityNodeKind.SettlementFitSource:
                    if (request.settlementFit == null)
                    {
                        note = "this bake carries no settlement fit table, so the rail, transit and underground nodes"
                               + " have no curves to read -- the editor fills it from a found asset or an authored"
                               + " placeholder, so a null here means the source was cleared on purpose";
                        return false;
                    }
                    context.bus.Publish(node.id, "settlementFit", request.settlementFit);
                    return true;
            }

            note = "this runner treats " + node.kind + " as a source, but has no case that publishes it";
            return false;
        }

        /// <summary> The two planning passes that price the ground and route across it. </summary>
        private static bool RunPlan(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.SuitabilityRaster)
            {
                Rect region;
                if (!TryInput(context, node, "region", out region, out note)) { return false; }
                ITerrainSurfaceReader surface;
                if (!TryInput(context, node, "terrain", out surface, out note)) { return false; }
                RoadClass cls;
                if (!TryInput(context, node, "roadClass", out cls, out note)) { return false; }

                float metresPerCell = (float)Number(node, "metresPerCell", 16.0, remarks);
                if (metresPerCell <= 0f)
                {
                    note = "a cell size of " + metresPerCell.ToString(CultureInfo.InvariantCulture)
                           + " metres cannot raster anything: set metresPerCell above zero";
                    return false;
                }

                SuitabilityWeights weights = new SuitabilityWeights();
                weights.slopeWeight = (float)Number(node, "slopeWeight", 0.45, remarks);
                weights.earthworkWeight = (float)Number(node, "earthworkWeight", 0.25, remarks);
                weights.waterWeight = (float)Number(node, "waterWeight", 0.15, remarks);
                weights.landUseWeight = (float)Number(node, "landUseWeight", 0.10, remarks);
                weights.networkWeight = (float)Number(node, "networkWeight", 0.05, remarks);
                if (!weights.IsUsable)
                {
                    note = "all five weights are zero, so every cell would price the same and the raster would advise"
                           + " nothing: give at least one factor a weight";
                    return false;
                }

                if (context.request.water == null)
                {
                    float unusedLevel;
                    string unusedReason;
                    if (TryInput(context, node, "waterSurfaceY", out unusedLevel, out unusedReason))
                    {
                        remarks.Add("a water level is wired in, but this bake carries no per-cell water source, so the"
                                    + " water factor was dropped from the legend rather than priced off one flat height"
                                    + " -- a single level says where the water sits, not how near each cell is to it");
                    }
                }

                int widthCells = Mathf.Max(1, Mathf.CeilToInt(region.width / metresPerCell));
                int depthCells = Mathf.Max(1, Mathf.CeilToInt(region.height / metresPerCell));

                string legend;
                SuitabilityRasterClass map = SuitabilityMapBuilder.Build(widthCells, depthCells, metresPerCell,
                    region.xMin, region.yMin, cls, weights, surface, context.request.water, context.request.landUse,
                    context.request.network, out legend);

                context.bus.Publish(node.id, "suitability", map);
                context.bus.Publish(node.id, "legend", legend);
                itemCount = SuitabilityRaster.CellCount(map);
                return true;
            }

            if (node.kind == CityNodeKind.CorridorSolve)
            {
                SuitabilityRasterClass map;
                if (!TryInput(context, node, "suitability", out map, out note)) { return false; }
                Vector2 fromCell;
                if (!TryInput(context, node, "fromCell", out fromCell, out note)) { return false; }
                Vector2 toCell;
                if (!TryInput(context, node, "toCell", out toCell, out note)) { return false; }

                double tolerance = Number(node, "toleranceFraction", CorridorSolver.DefaultToleranceFraction, remarks);
                CorridorPlan plan = CorridorSolver.Solve(map, Mathf.RoundToInt(fromCell.x), Mathf.RoundToInt(fromCell.y),
                    Mathf.RoundToInt(toCell.x), Mathf.RoundToInt(toCell.y), tolerance);

                context.bus.Publish(node.id, "corridor", plan);
                itemCount = plan.pathCells != null ? plan.pathCells.Count : 0;
                if (!plan.reached) { note = plan.Summary(); }
                return true;
            }

            note = "this runner treats " + node.kind + " as a planning pass, but has no case that runs it";
            return false;
        }

        /// <summary> The three passes that turn streets into blocks and blocks into lots. </summary>
        private static bool RunBlocks(CityBakeContext context, CityGraphNode node, int operatorSeed,
            List<string> remarks, out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.PlanarFaces)
            {
                StreetNetwork net;
                if (!TryInput(context, node, "network", out net, out note)) { return false; }

                List<List<int>> faces = PlanarFaceFinder.FindFaces(net);
                context.bus.Publish(node.id, "faces", faces);
                itemCount = faces != null ? faces.Count : 0;
                if (itemCount == 0)
                {
                    note = "these streets enclose nothing, so there are no blocks to inset: a network needs a closed"
                           + " loop before it has an inside";
                }
                return true;
            }

            if (node.kind == CityNodeKind.BlockInset)
            {
                List<Vector3> polygon;
                if (!TryInput(context, node, "polygon", out polygon, out note)) { return false; }
                if (polygon.Count < 3)
                {
                    note = "a face of " + polygon.Count.ToString(CultureInfo.InvariantCulture)
                           + " corners is not a block, so there is no inside to pull in";
                    return false;
                }

                List<float> insetPerEdge;
                string wiringNote;
                bool wired = TryInput(context, node, "insetPerEdge", out insetPerEdge, out wiringNote)
                             && insetPerEdge != null && insetPerEdge.Count == polygon.Count;

                if (!wired)
                {
                    double fallback = Number(node, "fallbackInsetMetres", 0.0, remarks);
                    if (fallback <= 0.0)
                    {
                        note = "no per-edge inset arrived and fallbackInsetMetres is zero, which stands for each"
                               + " edge's own street half width plus its verge -- and no port on this node carries a"
                               + " street to read that from. Either wire insetPerEdge, one metre figure per corner, or"
                               + " set fallbackInsetMetres to the inset you mean";
                        return false;
                    }

                    if (insetPerEdge != null && insetPerEdge.Count != polygon.Count)
                    {
                        remarks.Add("the wired inset list carries "
                                    + insetPerEdge.Count.ToString(CultureInfo.InvariantCulture) + " figures for "
                                    + polygon.Count.ToString(CultureInfo.InvariantCulture) + " edges, so it was set"
                                    + " aside and fallbackInsetMetres was used for every edge instead");
                    }

                    insetPerEdge = new List<float>(polygon.Count);
                    for (int i = 0; i < polygon.Count; i++) { insetPerEdge.Add((float)fallback); }
                }

                List<Vector3> block;
                if (!PolygonInset.TryInset(polygon, insetPerEdge, out block))
                {
                    note = "the inset consumed this face: every edge met another before it had any width left, so"
                           + " there is no block inside it. That is a block too narrow for the streets around it, not a"
                           + " fault in the inset";
                    return true;
                }

                context.bus.Publish(node.id, "block", block);
                itemCount = block != null ? block.Count : 0;
                return true;
            }

            if (node.kind == CityNodeKind.LotSubdivision)
            {
                List<Vector3> block;
                if (!TryInput(context, node, "block", out block, out note)) { return false; }

                double targetArea = Number(node, "targetAreaM2", 0.0, remarks);
                if (targetArea <= 0.0)
                {
                    note = "targetAreaM2 is zero, which stands for the painted zone's own lot size -- and no port on"
                           + " this node carries a zone to read that from. Either wire this block through a zone paint"
                           + " first and set the figure it resolves, or state the lot size you mean here";
                    return false;
                }

                int faceSeed;
                string seedNote;
                if (!TryInput(context, node, "faceSeed", out faceSeed, out seedNote))
                {
                    faceSeed = operatorSeed;
                    remarks.Add("no face seed arrived, so this block was cut from the node's own operator seed. Every"
                                + " block wired from one node then cuts the same way, which is repeatable but not"
                                + " varied");
                }

                List<List<Vector3>> lots = LotSubdivider.Subdivide(block, (float)targetArea,
                    (float)Number(node, "splitJitter", 0.0, remarks), faceSeed);

                context.bus.Publish(node.id, "lots", lots);
                itemCount = lots != null ? lots.Count : 0;
                if (itemCount == 0)
                {
                    note = "this block cut into no lots: it is smaller than one lot of the size asked for, so there is"
                           + " nothing to divide";
                }
                return true;
            }

            note = "this runner treats " + node.kind + " as a block pass, but has no case that runs it";
            return false;
        }

        /// <summary> The four passes that judge land, name it, and put a building on it. </summary>
        private static bool RunLand(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.BuildableLand)
            {
                List<Vector3> lot;
                if (!TryInput(context, node, "lot", out lot, out note)) { return false; }
                IPCGDataProvider terrain;
                if (!TryInput(context, node, "terrain", out terrain, out note)) { return false; }

                float waterSurfaceY;
                string waterNote;
                if (!TryInput(context, node, "waterSurfaceY", out waterSurfaceY, out waterNote))
                {
                    waterSurfaceY = float.NegativeInfinity;
                }

                List<List<Vector3>> occupied;
                string occupiedNote;
                if (!TryInput(context, node, "occupied", out occupied, out occupiedNote))
                {
                    occupied = new List<List<Vector3>>();
                }

                BuildabilityCriteria criteria;
                string criteriaNote;
                if (!TryInput(context, node, "criteria", out criteria, out criteriaNote))
                {
                    criteria = BuildabilityCriteria.Default();
                    remarks.Add("no criteria arrived, so this lot was judged against the shipped defaults rather than"
                                + " against its own zone's limits");
                }

                // Zero is passed through on purpose: Evaluate floors the spacing at its own minimum, so "the terrain's
                // own heightmap spacing" is honoured by the pass that knows it rather than guessed at here.
                LotVerdict verdict = BuildableLandTest.Evaluate(lot, terrain,
                    (float)Number(node, "sampleSpacingMetres", 0.0, remarks), waterSurfaceY, occupied, criteria);

                context.bus.Publish(node.id, "verdict", verdict);
                if (verdict.result == Buildability.Buildable) { context.bus.Publish(node.id, "lot", lot); }
                else if (verdict.result == Buildability.Reshaped)
                {
                    context.bus.Publish(node.id, "lot", verdict.reshapedPolygon);
                }
                itemCount = verdict.result == Buildability.Rejected ? 0 : 1;
                if (verdict.result != Buildability.Buildable) { note = verdict.reason; }
                return true;
            }

            if (node.kind == CityNodeKind.ZonePaint)
            {
                List<string> perCell;
                if (!TryInput(context, node, "lotCellZoneIds", out perCell, out note)) { return false; }

                ZoneResolution resolution = ZonePaint.ResolveLotByMajority(perCell,
                    Number(node, "straddleTieFraction", 0.0, remarks));

                ZoneCatalog zones;
                string zonesNote;
                if (TryInput(context, node, "zones", out zones, out zonesNote) && zones != null
                    && !resolution.refused && !string.IsNullOrEmpty(resolution.zoneId)
                    && zones.Resolve(resolution.zoneId) == null)
                {
                    remarks.Add("the cells under this lot name the zone " + resolution.zoneId
                                + ", which the wired catalogue does not define -- the majority was resolved, but"
                                + " nothing downstream will find limits for it");
                }

                context.bus.Publish(node.id, "resolution", resolution);
                itemCount = perCell.Count;
                if (resolution.refused) { note = resolution.reason; }
                return true;
            }

            if (node.kind == CityNodeKind.PrefabValidate)
            {
                ContentCatalog catalog;
                if (!TryInput(context, node, "content", out catalog, out note)) { return false; }

                string category = Text(node, "category", string.Empty, remarks);
                if (string.IsNullOrEmpty(category))
                {
                    note = "this node names no category, and an empty category is a refusal rather than a default: a"
                           + " placement that does not say what it needs cannot be given anything";
                    return false;
                }

                string requester = Text(node, "requester", string.Empty, remarks);
                if (string.IsNullOrEmpty(requester)) { requester = node.DisplayName(); }

                ContentResolution resolution = ContentResolver.Resolve(catalog, category, requester,
                    (ulong)(uint)context.request.citySeed);

                context.bus.Publish(node.id, "resolution", resolution);
                List<ContentCandidate> candidates = catalog.Candidates(category);
                itemCount = candidates != null ? candidates.Count : 0;
                if (!resolution.resolved) { note = resolution.refusal.Describe(); }
                return true;
            }

            if (node.kind == CityNodeKind.BuildingPlacement)
            {
                List<PlacementRequest> requests;
                if (!TryInput(context, node, "requests", out requests, out note)) { return false; }

                List<PlacementVerdict> verdicts;
                List<BuildingInstance> buildings = BuildingPlacement.ComputePlacements(requests, out verdicts);

                context.bus.Publish(node.id, "buildings", buildings);
                context.bus.Publish(node.id, "verdicts", verdicts);
                itemCount = buildings != null ? buildings.Count : 0;

                for (int i = 0; buildings != null && i < buildings.Count; i++)
                {
                    BuildingInstance instance = buildings[i];
                    Transform slot = context.EnsureObject(GeneratedKind.Building,
                        CityHierarchy.ElementIdForLot(instance.lotId));

                    // Null once means null every time: the request carries no city root, which is how a data-only bake
                    // runs. Breaking out beats testing the same field on every building.
                    if (slot == null) { break; }

                    slot.position = instance.position;
                    slot.rotation = instance.rotation;
                    slot.localScale = instance.scale;
                }

                if (itemCount == 0 && requests.Count > 0)
                {
                    note = "none of the " + requests.Count.ToString(CultureInfo.InvariantCulture)
                           + " lots took a building; the verdicts port says why for each";
                }
                return true;
            }

            note = "this runner treats " + node.kind + " as a land pass, but has no case that runs it";
            return false;
        }

        /// <summary> The five infrastructure passes. Three of them are long enough to read on their own and have a
        /// method each below. </summary>
        private static bool RunInfrastructure(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.SurfaceType)
            {
                SurfaceTypeClass surface;
                if (!TryInput(context, node, "surface", out surface, out note)) { return false; }
                float halfWidth;
                if (!TryInput(context, node, "flattenedHalfWidthMetres", out halfWidth, out note)) { return false; }

                context.bus.Publish(node.id, "paintHalfWidthMetres",
                    SurfaceType.PaintHalfWidthMetres(halfWidth, surface));
                context.bus.Publish(node.id, "emitsLaneMarkings", SurfaceType.EmitsLaneMarkings(surface));
                context.bus.Publish(node.id, "requiresSideDitch", SurfaceType.RequiresSideDitch(surface));
                return true;
            }

            if (node.kind == CityNodeKind.Rail)
            {
                RailTrackClass trackClass;
                if (!TryInput(context, node, "trackClass", out trackClass, out note)) { return false; }
                float radius;
                if (!TryInput(context, node, "curveRadiusMetres", out radius, out note)) { return false; }

                double speed = Number(node, "designSpeedKmh", 0.0, remarks);
                if (speed <= 0.0) { speed = trackClass.designSpeedKmh; }

                RailTrack.RadiusCheck check = RailTrack.CheckRadius(radius, trackClass);
                context.bus.Publish(node.id, "radiusCheck", check);
                context.bus.Publish(node.id, "appliedCantMillimetres",
                    RailTrack.AppliedCantMillimetres(speed, radius, trackClass));
                if (!check.accepted) { note = RailTrack.DescribeRadiusRejection(check); }
                return true;
            }

            if (node.kind == CityNodeKind.GradeSeparationNode)
            {
                return RunGradeSeparation(context, node, remarks, out itemCount, out note);
            }

            if (node.kind == CityNodeKind.PolesAndSpans)
            {
                return RunPolesAndSpans(context, node, remarks, out itemCount, out note);
            }

            if (node.kind == CityNodeKind.Ropeway)
            {
                return RunRopeway(context, node, remarks, out itemCount, out note);
            }

            note = "this runner treats " + node.kind + " as an infrastructure pass, but has no case that runs it";
            return false;
        }

        /// <summary> The three settlement-register passes B-27 adds: whether a place has a surface railway, its
        /// stopping-place figures, and whether it has a metro and an underground road. Each reads the fitted settlement
        /// table off its input port, takes the settlement's own figures from its parameters, and publishes the
        /// resolver's report to the bus -- the same division the rail and ropeway passes above draw between a shipped
        /// resolver and the node that feeds it. None of them place a prefab or touch the scene; where each stop and
        /// station LANDS is the builder's, on the terms D-5's track and D-7's masts already set.
        ///
        /// The order parameter falls back to the bake's own <see cref="CityBakeRequest.settlementOrder"/> when it is
        /// left at zero, so a single-settlement bake wires nothing there and a multi-settlement graph sets it per node.
        /// The seed is the CITY seed, reinterpreted the way every other draw in this file reinterprets it, so a
        /// settlement's railway is a property of the settlement and not of an operator's place in the stage list.
        /// </summary>
        private static bool RunSettlement(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;
            ulong citySeed = (ulong)(uint)context.request.citySeed;

            if (node.kind == CityNodeKind.SettlementRail)
            {
                SettlementFitTable fit;
                if (!TryInput(context, node, "settlementFit", out fit, out note)) { return false; }

                int orderIndex = (int)Number(node, "orderIndex", 0.0, remarks);
                if (orderIndex <= 0) { orderIndex = context.request.settlementOrder; }
                int population = (int)Number(node, "population", 0.0, remarks);
                string continentKey = Text(node, "continentKey", string.Empty, remarks);
                string identity = Text(node, "identity", string.Empty, remarks);

                SettlementRailReport report = SettlementRailPresence.Resolve(fit, orderIndex, continentKey,
                    population, identity, citySeed);

                context.bus.Publish(node.id, "rail", report);
                context.bus.Publish(node.id, "served", report.resolved && report.served);
                context.bus.Publish(node.id, "stationCount", report.stationCount);
                context.bus.Publish(node.id, "trackLengthMetres", report.trackLengthMetres);
                itemCount = report.resolved ? (report.served ? report.stationCount : 0) : -1;
                note = report.Describe();
                return true;
            }

            if (node.kind == CityNodeKind.Transit)
            {
                return RunTransit(context, node, remarks, out itemCount, out note);
            }

            if (node.kind == CityNodeKind.Underground)
            {
                SettlementFitTable fit;
                if (!TryInput(context, node, "settlementFit", out fit, out note)) { return false; }

                int orderIndex = (int)Number(node, "orderIndex", 0.0, remarks);
                if (orderIndex <= 0) { orderIndex = context.request.settlementOrder; }
                int population = (int)Number(node, "population", 0.0, remarks);
                string continentKey = Text(node, "continentKey", string.Empty, remarks);
                string identity = Text(node, "identity", string.Empty, remarks);

                double surfaceSlope = Number(node, "surfaceSlopeFraction", 0.0, remarks);
                double roadMaxGradient = Number(node, "roadMaxGradientFraction", 0.0, remarks);
                double waterGap = Number(node, "waterGapMetres", 0.0, remarks);
                double surfaceMaxCrossing = Number(node, "surfaceMaxCrossingMetres", 0.0, remarks);
                bool portalOnWater = Flag(node, "portalOnWater", false, remarks);
                bool underwaterCase = Flag(node, "underwaterCaseAvailable", false, remarks);
                bool portalGroundValid = Flag(node, "portalGroundValid", true, remarks);

                SettlementUndergroundReport report = SettlementUndergroundPresence.Resolve(fit, orderIndex,
                    continentKey, population, identity, citySeed, surfaceSlope, roadMaxGradient, waterGap,
                    surfaceMaxCrossing, portalOnWater, underwaterCase, portalGroundValid);

                context.bus.Publish(node.id, "underground", report);
                context.bus.Publish(node.id, "metroBuildable", report.metroBuildable);
                context.bus.Publish(node.id, "undergroundRoadPresent", report.undergroundRoadPresent);
                context.bus.Publish(node.id, "reliefSeverity", report.reliefSeverity);
                itemCount = report.resolved ? (report.metroBuildable ? report.metro.stationCount : 0) : -1;
                note = report.Describe();
                return true;
            }

            note = "this runner treats " + node.kind + " as a settlement pass, but has no case that runs it";
            return false;
        }

        /// <summary> B-27 / D-10. Which port a site can hold, from <see cref="PortSiting"/>: a water port whose vessel
        /// class is decided by the navigable depth, or a dry port / shipment yard decided by the rail and road present.
        /// The kind parameter picks which question is asked, and the figures for the other kinds are ignored -- a graph
        /// testing a dry port wires no depth, a graph testing a quay wires no rail. Every answer is the resolver's,
        /// unmodified: this runner reads the parameters, calls the one resolver for the chosen kind, and publishes the
        /// report with its headline answers. Deterministic with no draw -- the depth decides the vessel, so no seed is
        /// read (C-06 holds trivially: the same figures give the same port every bake). </summary>
        private static bool RunPort(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            int kind = (int)Number(node, "kind", 0.0, remarks);
            PortSitingReport report;
            if (kind == (int)PortKind.DryPort)
            {
                bool rail = Flag(node, "railPresent", false, remarks);
                bool road = Flag(node, "trunkRoadPresent", false, remarks);
                report = PortSiting.ResolveDryPort(rail, road);
            }
            else if (kind == (int)PortKind.ShipmentYard)
            {
                bool rail = Flag(node, "railPresent", false, remarks);
                report = PortSiting.ResolveShipmentYard(rail);
            }
            else
            {
                double depth = Number(node, "navigableDepthMetres", 0.0, remarks);
                bool waterReachable = Flag(node, "waterReachable", false, remarks);
                report = PortSiting.ResolveWaterPort(depth, waterReachable);
            }

            context.bus.Publish(node.id, "port", report);
            context.bus.Publish(node.id, "sited", report.resolved);
            context.bus.Publish(node.id, "admittedClass", report.admittedClass);
            itemCount = report.resolved ? 1 : -1;
            note = report.Describe();
            return true;
        }

        /// <summary> The land-use composition for one settlement's order, from <see cref="SettlementLandUse"/>: the
        /// residential, commercial, market and industrial shares of its developed ground, read from the fit and not a
        /// typed table. It reads the fitted table off its input and the order/bucket off its parameters, publishes the
        /// whole shares plus each share on its own port, and fails only when the fit cannot answer. No seed is drawn --
        /// the composition is read, so C-06 holds with no stream. It PLACES nothing: the zoning pass paints the zones;
        /// this answers only how much of each, which the task says must come from the fit and not a typed table. </summary>
        private static bool RunLandUse(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            SettlementFitTable fit;
            if (!TryInput(context, node, "settlementFit", out fit, out note)) { return false; }

            int orderIndex = (int)Number(node, "orderIndex", 0.0, remarks);
            if (orderIndex <= 0) { orderIndex = context.request.settlementOrder; }
            string continentKey = Text(node, "continentKey", "", remarks);

            SettlementLandUse.LandUseShares shares = SettlementLandUse.Resolve(fit, orderIndex, continentKey);

            context.bus.Publish(node.id, "shares", shares);
            context.bus.Publish(node.id, "residentialShare", (double)shares.residential);
            context.bus.Publish(node.id, "commercialShare", (double)shares.commercial);
            context.bus.Publish(node.id, "marketShare", (double)shares.market);
            context.bus.Publish(node.id, "industrialShare", (double)shares.industrial);
            itemCount = shares.resolved ? 1 : -1;
            note = shares.Describe();
            return true;
        }

        /// <summary> The cross-section of a river bank where it runs through a settlement, from <see cref="RiverBank"/>:
        /// its height, the slope-or-wall setback the frontage forces, and the pavement-side railing. Reads the design
        /// depth, the frontage left, and the preferred treatment off its parameters and publishes both the whole
        /// cross-section and each headline figure, plus the railing. No seed: the bank follows the river's depth, read
        /// the same every bake. It shapes no ground -- the strip conformed into the terrain is the build half; this
        /// answers what the bank's dimensions ARE. </summary>
        private static bool RunRiverBank(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            double depth = Number(node, "waterDepthMetres", 0.0, remarks);
            double frontage = Number(node, "availableFrontageMetres", 0.0, remarks);
            int preferred = (int)Number(node, "preferredTreatment", 0.0, remarks);
            BankTreatment treatment = preferred == (int)BankTreatment.Armoured ? BankTreatment.Armoured
                : preferred == (int)BankTreatment.RetainingWall ? BankTreatment.RetainingWall
                : BankTreatment.GrassedEarth;

            BankCrossSection bank = RiverBank.ResolveBank(treatment, depth, frontage);

            double topLength = Number(node, "topLengthMetres", 0.0, remarks);
            double spacing = Number(node, "railingSpacingMetres", 2.0, remarks);
            bool pavement = Flag(node, "pavementOnThisSide", true, remarks);
            BankRailing railing = RiverBank.ResolveRailing(topLength, spacing, pavement);

            context.bus.Publish(node.id, "bank", bank);
            context.bus.Publish(node.id, "treatment", bank.treatment);
            context.bus.Publish(node.id, "bankHeightMetres", bank.bankHeightMetres);
            context.bus.Publish(node.id, "horizontalRunMetres", bank.horizontalRunMetres);
            context.bus.Publish(node.id, "railing", railing);
            context.bus.Publish(node.id, "railPostCount", railing.postCount);
            itemCount = bank.resolved ? 1 : -1;
            note = bank.Describe() + "; " + railing.Describe();
            return true;
        }

        /// <summary> A crossing of the river, from <see cref="RiverBank.ResolveCrossing"/> over <see cref="BridgeTypes"/>:
        /// the deck family the span is given, its support stations, and the piers that land in the water. The bridge
        /// type is chosen by span when the parameter is left on auto, so a short span gets a beam and a long one a
        /// suspension, and a span no type fits is refused rather than forced. A pier in water the type has no foundation
        /// for refuses the crossing and is reported. No seed: the span and the wetted width decide it. </summary>
        private static bool RunRiverCrossing(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            double span = Number(node, "spanMetres", 0.0, remarks);
            int typeChoice = (int)Number(node, "bridgeType", -1.0, remarks);
            double wettedCentre = Number(node, "wettedCentreMetres", 0.0, remarks);
            double wettedWidth = Number(node, "wettedWidthMetres", 0.0, remarks);

            // 0/1/2 pick beam/arch/suspension; anything else (the -1 default) selects the simplest type the span fits.
            BridgeTypeSpec spec;
            switch (typeChoice)
            {
                case 0: spec = BridgeTypeSpec.Beam(); break;
                case 1: spec = BridgeTypeSpec.Arch(); break;
                case 2: spec = BridgeTypeSpec.Suspension(); break;
                default: spec = RiverBank.SelectTypeForSpan(span); break;
            }

            RiverCrossing crossing = RiverBank.ResolveCrossing(span, spec, wettedCentre, wettedWidth);

            context.bus.Publish(node.id, "crossing", crossing);
            context.bus.Publish(node.id, "deckType", crossing.deckType);
            context.bus.Publish(node.id, "supportCount", crossing.supportCount);
            context.bus.Publish(node.id, "intermediatePierCount", crossing.intermediatePierCount);
            context.bus.Publish(node.id, "piersInWater", crossing.piersInWater);
            context.bus.Publish(node.id, "piersRefused", crossing.piersRefused);
            itemCount = crossing.resolved ? crossing.supportCount : -1;
            note = crossing.Describe();
            return true;
        }

        /// <summary> D-13. The dam figured on this side of the linker and stated as a request for Universe Creator to
        /// answer. The valley and the reservoir are read off the terrain the "terrain" port carries -- the linker's
        /// provider, or the bake's own surface reader when nothing is wired -- so a dam with no ground to read refuses
        /// rather than inventing a valley. Everything else is closed-form: the body, the weir and orifice discharges,
        /// and the flood-filled extent. Nothing here touches the terrain or the water; the note says the dam stands in
        /// an unchanged river until the author runs UC's update. </summary>
        private static bool RunDam(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            ITerrainSurfaceReader ground;
            string groundNote;
            if (!TryInput(context, node, "terrain", out ground, out groundNote)) { ground = context.request.surface; }
            if (ground == null)
            {
                note = "there is no ground to read: nothing is wired to this dam's terrain port and the bake was given"
                       + " no surface reader either, so the valley and reservoir cannot be found";
                return false;
            }

            double centreX = Number(node, "centreX", 0.0, remarks);
            double centreZ = Number(node, "centreZ", 0.0, remarks);
            double axisHeading = Number(node, "axisHeadingDeg", 0.0, remarks);
            double upstreamHeading = Number(node, "upstreamHeadingDeg", 0.0, remarks);
            double crestLevel = Number(node, "crestLevelMetres", 0.0, remarks);
            double freeboard = Number(node, "freeboardMetres", DamWorks.MinFreeboardMetres, remarks);
            double crestLength = Number(node, "crestLengthMetres", 0.0, remarks);
            double crestWidth = Number(node, "crestWidthMetres", 8.0, remarks);
            double upstreamRun = Number(node, "upstreamRunPerRise", 0.05, remarks);
            double downstreamRun = Number(node, "downstreamRunPerRise", 0.75, remarks);
            int typeChoice = (int)Number(node, "spillwayType", 0.0, remarks);
            double spillwayCrest = Number(node, "spillwayCrestLevelMetres", 0.0, remarks);
            double spillwayWidth = Number(node, "spillwayWidthMetres", 0.0, remarks);
            double outletInvert = Number(node, "outletInvertLevelMetres", 0.0, remarks);
            double outletBore = Number(node, "outletBoreDiameterMetres", 0.0, remarks);
            double downstreamDepth = Number(node, "downstreamWaterDepthMetres", 0.0, remarks);
            double cellSize = Number(node, "cellSizeMetres", DamWorks.DefaultCellSizeMetres, remarks);
            int maxCells = (int)Number(node, "maxFloodCells", DamWorks.DefaultMaxFloodCells, remarks);
            int valleySamples = (int)Number(node, "valleySamples", DamWorks.DefaultValleySamples, remarks);

            // 0/1/2 pick the spillway form; anything else falls back to the ogee overflow.
            SpillwayType spillwayType;
            switch (typeChoice)
            {
                case 1: spillwayType = SpillwayType.Chute; break;
                case 2: spillwayType = SpillwayType.BroadCrested; break;
                default: spillwayType = SpillwayType.OgeeOverflow; break;
            }

            DamRequest request = DamWorks.Resolve(ground, centreX, centreZ, axisHeading, upstreamHeading, crestLevel,
                freeboard, crestLength, crestWidth, upstreamRun, downstreamRun, spillwayType, spillwayCrest,
                spillwayWidth, outletInvert, outletBore, downstreamDepth, cellSize, maxCells, valleySamples);

            context.bus.Publish(node.id, "request", request);
            context.bus.Publish(node.id, "reservoirSurfaceLevelMetres", request.profile.reservoirSurfaceLevelMetres);
            context.bus.Publish(node.id, "reservoirAreaSquareMetres", request.reservoir.surfaceAreaSquareMetres);
            context.bus.Publish(node.id, "storageVolumeCubicMetres", request.reservoir.storageVolumeCubicMetres);
            context.bus.Publish(node.id, "structuralHeightMetres", request.profile.structuralHeightMetres);
            context.bus.Publish(node.id, "spillwayDischargeCumecs", request.spillway.dischargeCumecs);
            context.bus.Publish(node.id, "outletDischargeCumecs", request.outlet.dischargeCumecs);
            context.bus.Publish(node.id, "tailwaterLevelMetres", request.tailwaterLevelMetres);
            context.bus.Publish(node.id, "crestSpansValley", request.valley.crestSpansValley);
            context.bus.Publish(node.id, "reservoirReachedLimit", request.reservoir.reachedSearchLimit);
            itemCount = request.resolved ? request.reservoir.floodedCellCount : -1;
            note = request.Describe();
            return true;
        }

        /// <summary> The stopping-place figures for one settlement, from <see cref="TransitPlacement"/>: bus stations by
        /// order, bus stops along a frontage, rail stations along a run, and whether a stop and a station interchange.
        /// Each sub-answer is published independently, so a graph that only wants the bus-station count reads it without
        /// wiring a frontage, and a figure left unasked -- a zero frontage, a zero run, the interchange test off --
        /// publishes nothing on its port and says so in the note rather than a fabricated zero. The node runs as long as
        /// ANY figure resolved; it fails only when a caller asked for none. </summary>
        private static bool RunTransit(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;
            List<string> lines = new List<string>();
            bool any = false;

            int orderIndex = (int)Number(node, "orderIndex", 0.0, remarks);
            if (orderIndex <= 0) { orderIndex = context.request.settlementOrder; }
            int busStations = TransitPlacement.BusStationCount(orderIndex);
            if (busStations > 0)
            {
                context.bus.Publish(node.id, "busStations", busStations);
                lines.Add(busStations + (busStations == 1 ? " bus station" : " bus stations") + " for order "
                          + orderIndex + ".");
                any = true;
            }
            else
            {
                lines.Add("no bus stations: order " + orderIndex + " is off the settlement ladder.");
            }

            double frontage = Number(node, "frontageMetres", 0.0, remarks);
            if (frontage > 0.0)
            {
                TransitPlacement.StopContext stopContext = Flag(node, "suburban", false, remarks)
                    ? TransitPlacement.StopContext.Suburban : TransitPlacement.StopContext.Urban;
                BusStopRun stops = TransitPlacement.CountStopsAlongFrontage(frontage, stopContext, null);
                context.bus.Publish(node.id, "busStopRun", stops);
                lines.Add(stops.Describe());
                any = true;
            }

            double railRun = Number(node, "railRunLengthMetres", 0.0, remarks);
            if (railRun > 0.0)
            {
                TransitPlacement.RailService service = RailServiceOf(Number(node, "railService", 0.0, remarks));
                RailStationRun rail = TransitPlacement.CountStationsAlongRun(railRun, service, null);
                context.bus.Publish(node.id, "railStationRun", rail);
                lines.Add(rail.Describe());
                any = true;
            }

            if (Flag(node, "testInterchange", false, remarks))
            {
                double busToRail = Number(node, "busToRailMetres", 0.0, remarks);
                double walkBudget = Number(node, "walkBudgetMetres",
                    TransitPlacement.DefaultInterchangeWalkMetres, remarks);
                InterchangeResult interchange = TransitPlacement.ResolveInterchange(busToRail, walkBudget);
                context.bus.Publish(node.id, "interchange", interchange);
                lines.Add(interchange.Describe());
                any = true;
            }

            if (!any)
            {
                note = "no transit figure was asked for: the order is off the ladder, no frontage or rail run was"
                       + " given, and the interchange test is off, so this node published nothing";
                return false;
            }
            note = string.Join(" ", lines.ToArray());
            return true;
        }

        /// <summary> The rail service a number parameter names: 0 metro, 1 commuter, anything else intercity. A small
        /// mapper rather than a cast to the enum, so a value off the three reads as the longest-spaced service instead
        /// of an undefined enum value the spacing switch would fall through. </summary>
        private static TransitPlacement.RailService RailServiceOf(double value)
        {
            int index = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (index <= 0) { return TransitPlacement.RailService.Metro; }
            if (index == 1) { return TransitPlacement.RailService.Commuter; }
            return TransitPlacement.RailService.Intercity;
        }

        /// <summary> Every place two streets cross without sharing a node, and what it would take to fly one over the
        /// other. A pair that shares a node is skipped: that is an intersection, and reporting it as a crossing would
        /// bury the handful of real flyovers under one entry per junction. </summary>
        private static bool RunGradeSeparation(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            StreetNetwork net;
            if (!TryInput(context, node, "network", out net, out note)) { return false; }
            GradeSeparationClass separationClass;
            if (!TryInput(context, node, "separationClass", out separationClass, out note)) { return false; }

            RoadClassCatalog catalog;
            string catalogNote;
            if (!TryInput(context, node, "roadClasses", out catalog, out catalogNote))
            {
                remarks.Add("no road class catalogue arrived, so each raised road's approach was tested against the"
                            + " shipped eight per cent gradient rather than against its own class limit");
            }

            List<GradeSeparation.PlanCrossing> crossings = new List<GradeSeparation.PlanCrossing>();
            List<GradeSeparation.SeparationResolve> separations = new List<GradeSeparation.SeparationResolve>();
            int flagged = 0;

            for (int i = 0; i < net.edges.Count; i++)
            {
                NetworkEdge first = net.edges[i];
                Vector3 a = net.nodes[first.a].position;
                Vector3 b = net.nodes[first.b].position;

                for (int j = i + 1; j < net.edges.Count; j++)
                {
                    NetworkEdge second = net.edges[j];
                    if (second.a == first.a || second.a == first.b || second.b == first.a || second.b == first.b)
                    {
                        continue;
                    }

                    Vector3 c = net.nodes[second.a].position;
                    Vector3 d = net.nodes[second.b].position;
                    GradeSeparation.PlanCrossing crossing = GradeSeparation.DetectCrossing(a.x, a.z, b.x, b.z,
                        c.x, c.z, d.x, d.z);
                    if (!crossing.crosses) { continue; }

                    crossings.Add(crossing);

                    // The rank is NEGATED on purpose. ChooseRaisedRoad reads a higher rank as the more major road and
                    // moves the minor one; RoadClass ids run the other way, arterial 0 to local 2. Passing the id
                    // straight through would raise the arterial over the local street, which is backwards.
                    int raised = GradeSeparation.ChooseRaisedRoad(-first.roadClassId, i, -second.roadClassId, j);
                    Vector3 raisedFrom = raised == 0 ? a : c;
                    Vector3 raisedTo = raised == 0 ? b : d;
                    double station = raised == 0 ? crossing.paramAlongAb : crossing.paramAlongCd;
                    int raisedClassId = raised == 0 ? first.roadClassId : second.roadClassId;

                    double raisedLength = Math.Sqrt((raisedTo.x - raisedFrom.x) * (raisedTo.x - raisedFrom.x)
                                                    + (raisedTo.z - raisedFrom.z) * (raisedTo.z - raisedFrom.z));

                    // The approach cannot run past the end of the road it is on, so the shorter side of the crossing
                    // is what is actually available -- a crossing near one end has a short approach on that side.
                    double available = raisedLength * Math.Min(station, 1.0 - station);

                    double maxGradient = 0.08;
                    RoadClass raisedClass;
                    if (catalog != null && catalog.TryGet(raisedClassId, out raisedClass))
                    {
                        maxGradient = raisedClass.maxGradient;
                    }

                    GradeSeparation.SeparationResolve resolve = GradeSeparation.ResolveSeparation(
                        separationClass.verticalClearanceMetres, maxGradient, available);
                    separations.Add(resolve);
                    if (resolve.flagged) { flagged++; }
                }
            }

            context.bus.Publish(node.id, "crossings", crossings);
            context.bus.Publish(node.id, "separations", separations);
            itemCount = crossings.Count;

            if (flagged > 0)
            {
                note = flagged.ToString(CultureInfo.InvariantCulture) + " of "
                       + crossings.Count.ToString(CultureInfo.InvariantCulture) + " crossings could not be separated:"
                       + " the road to be raised has less approach on one side than its own gradient limit needs, and"
                       + " the separations port quotes the length each one wanted";
            }
            return true;
        }

        /// <summary> One distribution pole run: where the poles stand, the real curve of the conductor between them,
        /// and whether that curve clears the ground.
        ///
        /// The conductor's attachment height is derived, not authored, because no shipped class carries a pole height:
        /// it is the ground plus the statutory clearance plus the span's own sag, so the cable reaches the clearance at
        /// mid-span rather than at the pole. Hanging it from the ground itself would fail every clearance check by
        /// exactly the sag. </summary>
        private static bool RunPolesAndSpans(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            DistributionPoleClass poleClass;
            if (!TryInput(context, node, "poleClass", out poleClass, out note)) { return false; }
            TransmissionLineClass lineClass;
            if (!TryInput(context, node, "lineClass", out lineClass, out note)) { return false; }
            List<double> groundHeights;
            if (!TryInput(context, node, "groundHeights", out groundHeights, out note)) { return false; }
            if (groundHeights.Count < 2)
            {
                note = "the clearance check needs the ground at both ends of the run at least, and "
                       + groundHeights.Count.ToString(CultureInfo.InvariantCulture) + " height was wired in";
                return false;
            }

            double runLength = Number(node, "runLengthMetres", 0.0, remarks);
            if (runLength <= 0.0)
            {
                runLength = poleClass.spanLengthMetres;
                remarks.Add("no run length was given, so the run is one span of "
                            + runLength.ToString(CultureInfo.InvariantCulture)
                            + " metres -- the shortest run that has a span in it at all");
            }

            List<Vector2> exclusions;
            string exclusionNote;
            if (!TryInput(context, node, "intersectionExclusions", out exclusions, out exclusionNote))
            {
                exclusions = new List<Vector2>();
            }

            List<float> furniture;
            string furnitureNote;
            if (!TryInput(context, node, "existingFurniture", out furniture, out furnitureNote))
            {
                furniture = new List<float>();
            }

            List<float> stations = DistributionPoleRun.PlanPoleStations((float)runLength, poleClass, exclusions,
                furniture);

            List<Vector3> poles = new List<Vector3>(stations.Count);
            for (int i = 0; i < stations.Count; i++)
            {
                poles.Add(new Vector3(stations[i], (float)GroundAt(groundHeights, stations[i], runLength), 0f));
            }

            List<Catenary> spans = new List<Catenary>();
            TransmissionLine.ClearanceResult worst = new TransmissionLine.ClearanceResult();
            worst.clears = true;
            worst.worstClearanceMetres = double.PositiveInfinity;
            worst.worstStationIndex = -1;

            for (int i = 0; i + 1 < stations.Count; i++)
            {
                double from = stations[i];
                double span = stations[i + 1] - from;
                if (span <= 0.0) { continue; }

                double sag = span * lineClass.sagRatio;
                Catenary cable = TransmissionLine.Solve(span,
                    GroundAt(groundHeights, from, runLength) + lineClass.minGroundClearanceMetres + sag,
                    GroundAt(groundHeights, from + span, runLength) + lineClass.minGroundClearanceMetres + sag,
                    lineClass.sagRatio);
                spans.Add(cable);

                TransmissionLine.ClearanceResult result = TransmissionLine.SpanClearance(cable,
                    SampleGround(groundHeights, from, span, runLength), lineClass.minGroundClearanceMetres);
                if (result.worstClearanceMetres < worst.worstClearanceMetres) { worst = result; }
            }

            context.bus.Publish(node.id, "poles", poles);
            context.bus.Publish(node.id, "spans", spans);
            context.bus.Publish(node.id, "clearance", worst);
            itemCount = poles.Count;

            if (spans.Count == 0)
            {
                note = "this run carries " + poles.Count.ToString(CultureInfo.InvariantCulture) + " pole"
                       + (poles.Count == 1 ? "" : "s") + " and so no span: the clearance reported is the vacuous one,"
                       + " not a passing check";
            }
            else if (!worst.clears)
            {
                note = "the tightest span clears the ground by only "
                       + worst.worstClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture) + " metres against "
                       + lineClass.minGroundClearanceMetres.ToString(CultureInfo.InvariantCulture)
                       + " required: shorten the span, or raise the ground under it";
            }
            return true;
        }

        /// <summary> One ropeway line: its towers, the rope over the first span, its cabin count, and the tightest
        /// clearance any span keeps. The tower tops are derived exactly as a pole run's conductor is, and for the same
        /// reason -- RopewayClass carries clearances, not heights. </summary>
        private static bool RunRopeway(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            RopewayClass cls;
            if (!TryInput(context, node, "ropewayClass", out cls, out note)) { return false; }
            List<double> groundHeights;
            if (!TryInput(context, node, "groundHeights", out groundHeights, out note)) { return false; }
            if (groundHeights.Count < 2)
            {
                note = "the clearance check needs the ground at both ends of the line at least, and "
                       + groundHeights.Count.ToString(CultureInfo.InvariantCulture) + " height was wired in";
                return false;
            }

            double runLength = Number(node, "runLengthMetres", 0.0, remarks);
            if (runLength <= 0.0)
            {
                note = "this line has no length, so it carries no cabins and spans nothing. A ropeway is refused"
                       + " rather than reported as zero: set runLengthMetres to the line's end-to-end length";
                return false;
            }

            double spanLength = Number(node, "spanLengthMetres", 0.0, remarks);
            if (spanLength <= 0.0)
            {
                spanLength = runLength;
                remarks.Add("no span length was given, so the line runs as one span end to end -- a ropeway with no"
                            + " intermediate tower");
            }

            List<double> turnAngles;
            string angleNote;
            if (!TryInput(context, node, "turnAnglesDeg", out turnAngles, out angleNote))
            {
                turnAngles = new List<double>();
            }

            int spanCount = TransmissionLine.SpanCount(runLength, spanLength);
            List<Vector3> towers = new List<Vector3>(spanCount + 1);
            List<TowerType> towerTypes = new List<TowerType>(spanCount + 1);
            for (int i = 0; i <= spanCount; i++)
            {
                double station = Math.Min(i * spanLength, runLength);
                towers.Add(new Vector3((float)station, (float)GroundAt(groundHeights, station, runLength), 0f));
                double angle = i < turnAngles.Count ? turnAngles[i] : 0.0;
                towerTypes.Add(Ropeway.TowerTypeAtNode(angle, cls));
            }

            Catenary first = new Catenary();
            TransmissionLine.ClearanceResult worst = new TransmissionLine.ClearanceResult();
            worst.clears = true;
            worst.worstClearanceMetres = double.PositiveInfinity;
            worst.worstStationIndex = -1;

            for (int i = 0; i + 1 < towers.Count; i++)
            {
                double from = towers[i].x;
                double span = towers[i + 1].x - from;
                if (span <= 0.0) { continue; }

                double sag = span * cls.sagRatio;
                Catenary rope = Ropeway.SolveRope(span,
                    GroundAt(groundHeights, from, runLength) + cls.minGroundClearanceMetres + sag,
                    GroundAt(groundHeights, from + span, runLength) + cls.minGroundClearanceMetres + sag, cls);
                if (i == 0) { first = rope; }

                TransmissionLine.ClearanceResult result = Ropeway.SpanClearance(rope,
                    SampleGround(groundHeights, from, span, runLength), cls);
                if (result.worstClearanceMetres < worst.worstClearanceMetres) { worst = result; }
            }

            int cabins = Ropeway.CabinCount(runLength, cls);
            context.bus.Publish(node.id, "towers", towers);
            context.bus.Publish(node.id, "towerTypes", towerTypes);
            context.bus.Publish(node.id, "rope", first);
            context.bus.Publish(node.id, "cabins", cabins);
            context.bus.Publish(node.id, "clearance", worst);
            itemCount = towers.Count;

            if (!worst.clears)
            {
                note = "the tightest span clears the ground by only "
                       + worst.worstClearanceMetres.ToString("0.##", CultureInfo.InvariantCulture) + " metres against "
                       + cls.minGroundClearanceMetres.ToString(CultureInfo.InvariantCulture)
                       + " required: raise the towers by shortening the span, or move the line off that ground";
            }
            else if (cabins == 0)
            {
                note = "this line is shorter than one cabin spacing, so it carries no cabin";
            }
            return true;
        }

        /// <summary> The ground at one station along a run, from heights taken evenly from its start to its end.
        /// Linear between two samples: the samples are a survey, and interpolating them reads it, where fitting a curve
        /// through them would invent ground nobody measured. </summary>
        private static double GroundAt(IList<double> samples, double stationMetres, double runLengthMetres)
        {
            if (samples == null || samples.Count == 0) { return 0.0; }
            if (samples.Count == 1 || runLengthMetres <= 0.0) { return samples[0]; }

            double t = stationMetres / runLengthMetres;
            if (t <= 0.0) { return samples[0]; }
            if (t >= 1.0) { return samples[samples.Count - 1]; }

            double position = t * (samples.Count - 1);
            int lower = (int)Math.Floor(position);
            if (lower >= samples.Count - 1) { return samples[samples.Count - 1]; }

            double fraction = position - lower;
            return samples[lower] * (1.0 - fraction) + samples[lower + 1] * fraction;
        }

        /// <summary> The ground under one span, resampled at the density the run's own heights were supplied at, laid
        /// out the way the shipped clearance test reads them: station 0 at the first tower, the last at the second.
        /// Resampling at the author's own density rather than at a fixed count keeps the check as fine as the survey
        /// and no finer. </summary>
        private static List<double> SampleGround(IList<double> samples, double fromMetres, double spanMetres,
            double runLengthMetres)
        {
            int count = 2;
            double spacing = runLengthMetres / (samples.Count - 1);
            if (spacing > 0.0 && !double.IsInfinity(spacing))
            {
                count = Math.Max(2, (int)Math.Round(spanMetres / spacing) + 1);
            }

            List<double> underSpan = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                underSpan.Add(GroundAt(samples, fromMetres + spanMetres * ((double)i / (count - 1)), runLengthMetres));
            }
            return underSpan;
        }

        /// <summary> One element category placed along a frontage: the benches, bins, lamps, stops and signs the
        /// manifest names, all through the ONE node kind their catalogue entry shares.
        ///
        /// The two per-order figures are REQUIRED here rather than defaulted. A grep of the whole runtime finds no
        /// per-kilometre table and no per-order spacing table, so there is nothing for a zero to defer to: this runner
        /// refuses and names the row, because inventing "one bench every 40 m" and calling it real-world would be a
        /// lie the author cannot see. The rate sets the spacing and the minimum can only WIDEN it -- a rate that asks
        /// for them closer together than the minimum allows loses to the minimum, and the difference is reported.
        ///
        /// Instances stand at half-spacing in from each end (station = spacing * (i + 0.5)), so a frontage never
        /// plants an object exactly on a corner shared with the next frontage along.
        ///
        /// C-17 ADDED THE PULL TO GROUND. The frontage line gives a plan station and a height measured along the
        /// CENTRELINE; a bench belongs on the verge beside it, which is a different height and often a different slope.
        /// <see cref="GroundSeating.Seat"/> reads the surface at the station and returns both, and the two rows that feed
        /// it -- <c>alignToSurfaceNormal</c> per element, <c>&lt;order&gt;.embedDepthMetres</c> per order -- are read here
        /// and decided there. When no reader can be found the planned position is kept unchanged and SAID, because a
        /// bench at a guessed height cannot be told apart from one at a measured height. </summary>
        private static bool RunElementPlacement(CityBakeContext context, CityGraphNode node,
            CityNodeDescriptor descriptor, int operatorSeed, List<string> remarks, out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            List<Vector3> frontage;
            if (!TryInput(context, node, "frontage", out frontage, out note)) { return false; }
            ContentCatalog catalog;
            if (!TryInput(context, node, "content", out catalog, out note)) { return false; }

            string category = Text(node, CityNodeCatalog.ElementCategoryParameter, descriptor.elementCategoryId,
                remarks);
            if (string.IsNullOrEmpty(category))
            {
                note = "this node carries no " + CityNodeCatalog.ElementCategoryParameter
                       + ", and the catalogue entry it was made from names none either, so there is nothing to place";
                return false;
            }

            double frontageMetres = FrontageLength(frontage);
            if (frontageMetres <= 0.0)
            {
                note = "the frontage handed in has no length (" + frontage.Count
                       + " point(s)), so there is no line to place " + descriptor.displayName.ToLowerInvariant()
                       + " along";
                return false;
            }

            string prefix = CityNodeCatalog.OrderParameterPrefix(context.request.settlementOrder);
            if (string.IsNullOrEmpty(prefix))
            {
                note = "settlement order " + context.request.settlementOrder.ToString(CultureInfo.InvariantCulture)
                       + " is not one of the four orders, so this node cannot tell which spacing row to read";
                return false;
            }

            List<PrefabPlacementSpec> slots = new List<PrefabPlacementSpec>();
            List<Vector3> positions = new List<Vector3>();
            List<Quaternion> facings = new List<Quaternion>();

            if (!Flag(node, prefix + ".present", true, remarks))
            {
                context.bus.Publish(node.id, "slots", slots);
                context.bus.Publish(node.id, "positions", positions);
                context.bus.Publish(node.id, "facings", facings);
                itemCount = 0;
                note = "a " + prefix + " places no " + descriptor.displayName.ToLowerInvariant()
                       + ", which this graph says on purpose rather than by omission";
                return true;
            }

            double perKilometre = Number(node, prefix + ".perKilometreOfFrontage", 0.0, remarks);
            if (perKilometre <= 0.0)
            {
                note = "set " + prefix + ".perKilometreOfFrontage on this node: the tool holds no per-kilometre figure"
                       + " for " + descriptor.displayName.ToLowerInvariant() + " in a " + prefix
                       + ", so a rate of 0 has nothing to fall back to";
                return false;
            }

            double minSpacing = Number(node, prefix + ".minSpacingMetres", 0.0, remarks);
            if (minSpacing <= 0.0)
            {
                note = "set " + prefix + ".minSpacingMetres on this node: 0 would let the rate stand two "
                       + descriptor.displayName.ToLowerInvariant() + " in the same place, and the tool holds no"
                       + " shipped minimum to use instead";
                return false;
            }

            double spacing = 1000.0 / perKilometre;
            if (spacing < minSpacing)
            {
                remarks.Add("the rate asked for one every " + spacing.ToString("0.##", CultureInfo.InvariantCulture)
                            + " m, closer than the " + minSpacing.ToString("0.##", CultureInfo.InvariantCulture)
                            + " m minimum, so the minimum won and fewer were placed than the rate asked for");
                spacing = minSpacing;
            }

            // Instances are spread EVENLY over the frontage at the rate's own count, rather than stepped off from one
            // end at exactly the requested spacing. Stepping leaves whatever is left over as a ragged gap at the far
            // end; spreading gives the same density with equal margins at both ends, which is how a block of street
            // furniture actually reads. The count is rounded to nearest so a 30 m frontage at one per 40 m gets one
            // rather than none, and the minimum spacing then trims the count until it is honoured exactly.
            int count = (int)Math.Round(frontageMetres / spacing, MidpointRounding.AwayFromZero);
            if (count < 1) { count = 1; }
            while (count > 1 && frontageMetres / count < minSpacing - 1e-6) { count--; }
            double actualSpacing = frontageMetres / count;

            Vector2 streetSide;
            string sideNote;
            bool haveSide = TryInput(context, node, "streetSideXZ", out streetSide, out sideNote);
            if (!haveSide)
            {
                streetSide = Vector2.zero;
                remarks.Add("nothing said which side of the frontage the street is on, so these face the LEFT of the"
                            + " frontage's own direction of travel seen from above; wire streetSideXZ to say otherwise");
            }

            int itemSeedBase;
            string seedNote;
            if (!TryInput(context, node, "seed", out itemSeedBase, out seedNote))
            {
                itemSeedBase = operatorSeed;
            }

            // THE GROUND, and the order it has to be read in. The wired port comes first because an author who wired a
            // reader meant that one; the bake's own reader stands in second, because the graph these nodes are commonly
            // dropped into has no Terrain surface node at all and an un-pulled bench is the whole defect C-17 removes.
            // The two cannot disagree: CityNodeKind.TerrainSurface publishes request.surface unchanged, so the fallback
            // is the same object the wire would have carried. What neither can fix is bake ORDER -- a reader read before
            // the road and pad conform describes the ground the verge USED to have.
            ITerrainSurfaceReader surface;
            string surfaceNote;
            if (!TryInput(context, node, "terrain", out surface, out surfaceNote)) { surface = context.request.surface; }

            // HOW IT MEETS THAT GROUND. Per element for the alignment, because a tree is plumb on a slope and a bench is
            // not; per ORDER for the depth, because a village's verge is rougher than a capital's kerbline. Both rows are
            // read here and neither is decided here: GroundSeating owns the arithmetic and the two clamps.
            bool alignToSurface = Flag(node, "alignToSurfaceNormal", false, remarks);
            double embedDepth = Number(node, prefix + ".embedDepthMetres", GroundSeating.DeriveEmbedFromFootprint,
                remarks);
            GroundSeatingClass seating = alignToSurface
                ? GroundSeatingClass.ForResting()
                : GroundSeatingClass.ForDrivenOrGrown();

            ContentRefusal firstRefusal = new ContentRefusal();
            int refused = 0;
            int unseated = 0;
            string firstSeatNote = string.Empty;

            for (int i = 0; i < count; i++)
            {
                Vector3 position;
                Vector3 tangent;
                if (!FrontagePoint(frontage, actualSpacing * (i + 0.5), out position, out tangent)) { continue; }

                // The facing normal: the tangent turned a quarter turn to the LEFT of travel in plan (PlanXZ's own
                // Perpendicular, so the tool has one definition of that turn and not two), flipped toward the authored
                // street side when there is one -- the same test BuildingPlacement runs against a lot centroid.
                PlanXZ along = new PlanXZ(tangent.x, tangent.z).Normalised;
                PlanXZ left = along.Perpendicular;
                if (haveSide)
                {
                    PlanXZ toStreet = new PlanXZ(streetSide.x - position.x, streetSide.y - position.z);
                    if (PlanXZ.Dot(left, toStreet) < 0.0) { left = left * -1.0; }
                }
                Vector3 forward = new Vector3((float)left.x, 0f, (float)left.z);
                if (forward.sqrMagnitude <= 1e-12f) { forward = Vector3.forward; }

                string requester = node.DisplayName() + "#" + i.ToString("D4", CultureInfo.InvariantCulture);
                int itemSeed = CitySeed.ForItem(itemSeedBase, i);
                ContentResolution resolution = ContentResolver.Resolve(catalog, category, requester,
                    (ulong)(uint)itemSeed);
                if (!resolution.resolved)
                {
                    if (refused == 0) { firstRefusal = resolution.refusal; }
                    refused++;
                    continue;
                }

                int variantIndex = VariantIndexOf(catalog, category, resolution.asset.assetId);
                Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

                // THE PULL TO GROUND. Until C-17 this line was slot.position = position with the frontage line's own Y,
                // which put a bench at the height of the centreline it was measured from rather than on the verge beside
                // it. The footprint is Vector2.zero here for the reason the remark below gives, so the embed falls back to
                // the class minimum and GroundSeating says so in its note rather than inventing a depth from nothing.
                GroundSeat seat = GroundSeating.Seat(surface, position, facing, GroundContactMode.SkirtToTerrain,
                    seating, Vector2.zero, (float)embedDepth);
                if (!seat.seated) { unseated++; }
                if (firstSeatNote.Length == 0 && seat.note.Length > 0) { firstSeatNote = seat.note; }

                // footprintSize and heightMetres stay ZERO: the content catalogue records an assetId, a category, a
                // validation verdict and variant tags, and no measured bounds at all. Writing a guessed 1 m box here
                // would be read downstream as a measurement.
                slots.Add(new PrefabPlacementSpec(category, variantIndex, Vector2.zero, 0f,
                    GroundContactMode.SkirtToTerrain));
                positions.Add(seat.position);
                facings.Add(seat.rotation);

                Transform slot = context.EnsureObject(GeneratedKind.Furniture, category);
                if (slot == null) { break; }   // data-only bake: null once means null every time
                slot.position = seat.position;
                slot.rotation = seat.rotation;
            }

            context.bus.Publish(node.id, "slots", slots);
            context.bus.Publish(node.id, "positions", positions);
            context.bus.Publish(node.id, "facings", facings);
            itemCount = slots.Count;

            if (slots.Count > 0)
            {
                remarks.Add("every slot's footprint and height are 0: the content catalogue records an asset's id,"
                            + " category, validation verdict and tags, and no measured bounds, so nothing here knows"
                            + " how big the author's prefab is");
            }

            // THE GROUND, REPORTED. Silence when everything seated: an author does not need to be told that the normal
            // thing happened. The two failures are different faults with different fixes, so they are different
            // sentences -- no reader at all is a graph or bake-order problem, and a reader that had nothing to read at
            // some points is a streaming or region problem.
            if (slots.Count > 0 && surface == null)
            {
                remarks.Add("this bake carried no ground reader, so not one of these was pulled to the ground: they"
                            + " stand at the height of the frontage line they were measured along. Wire the Terrain"
                            + " surface node's terrain port into this node, downstream of the road and pad conform");
            }
            else if (unseated > 0)
            {
                remarks.Add(unseated.ToString(CultureInfo.InvariantCulture) + " of "
                            + slots.Count.ToString(CultureInfo.InvariantCulture) + " had no ground under them to read,"
                            + " so those stand at the frontage line's own height rather than on the surface");
            }
            if (firstSeatNote.Length > 0) { remarks.Add("seating: " + firstSeatNote); }

            if (refused > 0)
            {
                note = refused.ToString(CultureInfo.InvariantCulture) + " of "
                       + count.ToString(CultureInfo.InvariantCulture) + " slots were left empty -- "
                       + firstRefusal.Describe();
            }
            else if (slots.Count == 0)
            {
                note = "no " + descriptor.displayName.ToLowerInvariant() + " was placed along "
                       + frontageMetres.ToString("0.#", CultureInfo.InvariantCulture)
                       + " m of frontage, which no rate or spacing here explains";
            }
            return true;
        }

        /// <summary> The plan length of a frontage polyline in metres. Plan rather than slope length because a rate
        /// per kilometre of frontage is a planning figure measured on the map, and because a 10 percent street grade
        /// changes the answer by half a percent -- far less than the rounding to whole benches. </summary>
        private static double FrontageLength(IList<Vector3> frontage)
        {
            if (frontage == null || frontage.Count < 2) { return 0.0; }

            double total = 0.0;
            for (int i = 1; i < frontage.Count; i++)
            {
                total += PlanXZ.Distance(new PlanXZ(frontage[i].x, frontage[i].z),
                    new PlanXZ(frontage[i - 1].x, frontage[i - 1].z));
            }
            return total;
        }

        /// <summary> The point at one plan station along a frontage, with the segment direction it sits on. Height is
        /// interpolated along the segment, so an instance sits on the frontage line the streets handed in rather than
        /// on a re-sampled terrain -- if that line follows the kerb, so does the bench. </summary>
        private static bool FrontagePoint(IList<Vector3> frontage, double stationMetres, out Vector3 position,
            out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.forward;
            if (frontage == null || frontage.Count < 2) { return false; }

            double walked = 0.0;
            for (int i = 1; i < frontage.Count; i++)
            {
                Vector3 from = frontage[i - 1];
                Vector3 to = frontage[i];
                double length = PlanXZ.Distance(new PlanXZ(from.x, from.z), new PlanXZ(to.x, to.z));
                if (length <= 0.0) { continue; }

                if (stationMetres <= walked + length || i == frontage.Count - 1)
                {
                    double t = (stationMetres - walked) / length;
                    if (t < 0.0) { t = 0.0; }
                    if (t > 1.0) { t = 1.0; }
                    position = Vector3.LerpUnclamped(from, to, (float)t);
                    tangent = to - from;
                    return true;
                }
                walked += length;
            }
            return false;
        }

        /// <summary> Which index in the category's own stable list the chosen asset sits at, or -1 when it cannot be
        /// found. The resolver hands back an assetId and <c>PrefabPlacementSpec</c> records an index, so the index is
        /// LOOKED UP in the same sorted list the draw ran over rather than guessed from the draw. </summary>
        private static int VariantIndexOf(ContentCatalog catalog, string category, string assetId)
        {
            if (catalog == null || string.IsNullOrEmpty(assetId)) { return -1; }

            List<ContentCandidate> candidates = catalog.Candidates(category);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].assetId, assetId, StringComparison.Ordinal)) { return i; }
            }
            return -1;
        }

        /// <summary> The stages after the city plan: the mask the terrain and scatter passes share, the two conform
        /// passes, and the four runtime nodes. </summary>
        private static bool RunLaterStages(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.FootprintPublish)
            {
                Rect region;
                if (!TryInput(context, node, "region", out region, out note)) { return false; }
                List<List<Vector3>> footprints;
                if (!TryInput(context, node, "footprints", out footprints, out note)) { return false; }

                double metresPerPixel = Number(node, "metresPerPixel", 4.0, remarks);
                if (metresPerPixel <= 0.0)
                {
                    note = "metresPerPixel is " + metresPerPixel.ToString(CultureInfo.InvariantCulture)
                           + " on this node: a mask pixel has to stand for some ground";
                    return false;
                }

                double feather = Number(node, "featherWidthMetres", 8.0, remarks);
                if (feather < 0.0)
                {
                    remarks.Add("a feather width of " + feather.ToString(CultureInfo.InvariantCulture)
                                + " m is not a width, so the edge was left hard");
                    feather = 0.0;
                }

                int widthPixels = Mathf.Max(1, Mathf.CeilToInt(region.width / (float)metresPerPixel));
                int depthPixels = Mathf.Max(1, Mathf.CeilToInt(region.height / (float)metresPerPixel));
                FootprintMaskClass mask = new FootprintMaskClass(widthPixels, depthPixels, (float)metresPerPixel);
                mask.originXMetres = region.xMin;
                mask.originZMetres = region.yMin;
                mask.featherWidthMetres = (float)feather;

                int stamped = RasteriseFootprints(mask, footprints, feather, remarks);
                context.bus.Publish(node.id, "mask", mask);
                itemCount = stamped;
                if (stamped == 0)
                {
                    note = "nothing was stamped into the mask, so the terrain and scatter passes will read it as open"
                           + " country: this city has claimed no ground yet";
                }
                return true;
            }

            if (node.kind == CityNodeKind.FootprintConformNode)
            {
                List<ConformCell> cells;
                if (!TryInput(context, node, "cells", out cells, out note)) { return false; }

                double cellAreaM2 = Number(node, "cellAreaM2", 0.0, remarks);
                if (cellAreaM2 <= 0.0)
                {
                    // The catalogue promises zero means "the mask's own pixel area". That promise is only keepable
                    // when the mask is wired, so an unwired one refuses here rather than quoting volumes computed
                    // against an area of nothing.
                    FootprintMaskClass mask;
                    string maskNote;
                    if (!TryInput(context, node, "mask", out mask, out maskNote) || mask == null
                        || mask.metresPerPixel <= 0f)
                    {
                        note = "cellAreaM2 is 0 and no mask is wired, so there is no pixel area to read it from: wire"
                               + " the mask these cells came from, or type the area one cell stands for";
                        return false;
                    }
                    cellAreaM2 = (double)mask.metresPerPixel * mask.metresPerPixel;
                    remarks.Add("the cell area came from the mask's own pixel, "
                                + cellAreaM2.ToString("0.##", CultureInfo.InvariantCulture) + " m^2");
                }

                IList<ConformRemoval> removals;
                ConformReport report = FootprintConform.Conform(cells, cellAreaM2, out removals);
                context.bus.Publish(node.id, "report", report);
                context.bus.Publish(node.id, "removals", removals);
                itemCount = removals != null ? removals.Count : 0;
                return true;
            }

            if (node.kind == CityNodeKind.BuildingPadConformNode)
            {
                List<PadRequest> requests;
                if (!TryInput(context, node, "requests", out requests, out note)) { return false; }
                IPCGDataProvider terrain;
                if (!TryInput(context, node, "terrain", out terrain, out note)) { return false; }
                PadConformParams padParams;
                if (!TryInput(context, node, "padParams", out padParams, out note)) { return false; }

                List<PadRecord> pads = BuildingPadConform.ComputePads(requests, terrain, padParams);
                context.bus.Publish(node.id, "pads", pads);
                itemCount = pads != null ? pads.Count : 0;
                return true;
            }

            if (node.kind == CityNodeKind.HandPlacedScan)
            {
                return RunHandPlacedScan(context, node, remarks, out itemCount, out note);
            }

            return RunRuntimeStage(context, node, remarks, out itemCount, out note);
        }

        /// <summary> C-18. Measures the ground under the objects the author placed by hand, and reports it.
        ///
        /// WHY IT LIVES IN THE BAKE at all, when there is also a button for it: a scan that only ran on demand would
        /// answer for the scene as it was the last time somebody remembered to press it, and the ground moves during the
        /// bake -- the road conform and the pad conform both cut it. Running here means the verdict describes the ground
        /// the author will actually see.
        ///
        /// IT NEVER FAILS THE BAKE OVER WHAT IT FINDS. A hanging shed is the author's business, not a broken graph, so
        /// findings are remarks and the node returns true. The one thing it does refuse is having no ground to measure
        /// against, because then it would publish a list of passes that mean nothing.
        ///
        /// The per-object lines are capped. The summary carries the totals, and a thousand identical remarks would push
        /// the rest of the bake report out of the window it is read in. </summary>
        private static bool RunHandPlacedScan(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            ITerrainSurfaceReader ground;
            string groundNote;
            if (!TryInput(context, node, "terrain", out ground, out groundNote)) { ground = context.request.surface; }
            if (ground == null)
            {
                note = "there is no ground to measure against: nothing is wired to this node's terrain port and the bake"
                       + " was given no surface reader either";
                return false;
            }

            List<Transform> roots = context.request.scanRoots;
            if (roots == null || roots.Count == 0)
            {
                remarks.Add("no scan roots were named, so nothing was scanned. Name the parents of the objects to check"
                            + " under Scan roots on the city; an empty list scans nothing on purpose, because a sweep"
                            + " that defaulted to the whole scene would ask for ground to be levelled under objects"
                            + " nobody mentioned");
                context.bus.Publish(node.id, "findings", new List<FootprintScanFinding>());
                context.bus.Publish(node.id, "padRequests", new List<PadRequest>());
                itemCount = 0;
                return true;
            }

            FootprintScanClass cls = FootprintScanClass.Default();
            cls.drift.supportToleranceMetres = Number(node, "supportToleranceMetres",
                cls.drift.supportToleranceMetres, remarks);
            cls.drift.visibleMismatchMetres = Number(node, "visibleMismatchMetres",
                cls.drift.visibleMismatchMetres, remarks);
            cls.maximumStationSpacingMetres = Number(node, "maximumStationSpacingMetres",
                cls.maximumStationSpacingMetres, remarks);
            cls.maximumObjects = (int)Number(node, "maximumObjects", cls.maximumObjects, remarks);
            cls.includeInterior = Flag(node, "includeInterior", cls.includeInterior, remarks);
            cls.contactMode = ContactModeFrom(Text(node, "contactMode", "SkirtToTerrain", remarks), remarks);
            PadElevationRule rule = ElevationRuleFrom(Text(node, "elevationRule", "CentroidSample", remarks), remarks);
            bool produce = Flag(node, "produceGroundRequests", false, remarks);

            List<PadRequest> padRequests = produce ? new List<PadRequest>() : null;
            string summary;
            List<FootprintScanFinding> findings = HandPlacedGroundScan.Sweep(roots, ground, cls, rule, padRequests,
                out summary);
            remarks.Add(summary);

            int listed = 0;
            int held = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (!findings[i].NeedsAttention) { continue; }
                if (listed < HandPlacedRemarkLimit) { remarks.Add(findings[i].Describe()); listed++; }
                else { held++; }
            }
            if (held > 0)
            {
                remarks.Add(held.ToString(CultureInfo.InvariantCulture) + " further objects need attention and are in"
                            + " the findings list rather than in this report");
            }

            context.bus.Publish(node.id, "findings", findings);
            context.bus.Publish(node.id, "padRequests", padRequests != null ? padRequests : new List<PadRequest>());
            itemCount = findings.Count;
            return true;
        }

        /// <summary> How many hand-placed objects get a line of their own in the bake report. Twenty is enough to see a
        /// pattern and few enough to leave room for the rest of the bake. </summary>
        private const int HandPlacedRemarkLimit = 20;

        /// <summary> The contact mode named by the node's text parameter. Compared rather than reflected over, so an
        /// unknown word produces a sentence naming the three that work instead of an exception. </summary>
        private static GroundContactMode ContactModeFrom(string text, List<string> remarks)
        {
            if (string.Equals(text, "OnPad", StringComparison.OrdinalIgnoreCase)) { return GroundContactMode.OnPad; }
            if (string.Equals(text, "Piled", StringComparison.OrdinalIgnoreCase)) { return GroundContactMode.Piled; }
            if (string.Equals(text, "SkirtToTerrain", StringComparison.OrdinalIgnoreCase))
            {
                return GroundContactMode.SkirtToTerrain;
            }
            remarks.Add("contactMode was \"" + text + "\", which is not one of OnPad, SkirtToTerrain or Piled, so"
                        + " SkirtToTerrain was used");
            return GroundContactMode.SkirtToTerrain;
        }

        /// <summary> The pad elevation rule named by the node's text parameter, on the same terms as
        /// <see cref="ContactModeFrom"/>. </summary>
        private static PadElevationRule ElevationRuleFrom(string text, List<string> remarks)
        {
            if (string.Equals(text, "MinCornerPlusFreeboard", StringComparison.OrdinalIgnoreCase))
            {
                return PadElevationRule.MinCornerPlusFreeboard;
            }
            if (string.Equals(text, "CentroidSample", StringComparison.OrdinalIgnoreCase))
            {
                return PadElevationRule.CentroidSample;
            }
            remarks.Add("elevationRule was \"" + text + "\", which is not one of CentroidSample or"
                        + " MinCornerPlusFreeboard, so CentroidSample was used");
            return PadElevationRule.CentroidSample;
        }

        /// <summary> Stamps every footprint into the mask, visiting ONLY the pixels inside each footprint's own
        /// bounding box grown by the feather's reach. A whole-mask sweep per footprint would be the same picture at
        /// several thousand times the cost -- a city of two thousand lots over a 4 km mask is billions of point-in-
        /// polygon tests, and every one of them outside the lot's box is a test whose answer is already known.
        ///
        /// Overlaps take the HIGHER coverage rather than adding: two lots sharing a boundary are still just "city
        /// here", and adding would drive the shared strip past 1 and pull the terrain twice. </summary>
        private static int RasteriseFootprints(FootprintMaskClass mask, IList<List<Vector3>> footprints, double feather,
            List<string> remarks)
        {
            if (footprints == null || mask == null || mask.coverage == null) { return 0; }

            double metresPerPixel = mask.metresPerPixel;
            // The ramp reaches zero half a feather OUTSIDE the boundary; one pixel of slack so the outermost ramped
            // pixel centre is still visited.
            double margin = feather * 0.5 + metresPerPixel;
            int stamped = 0;
            int skipped = 0;

            for (int f = 0; f < footprints.Count; f++)
            {
                List<Vector3> polygon = footprints[f];
                if (polygon == null || polygon.Count < 3) { skipped++; continue; }

                float minX = polygon[0].x, maxX = polygon[0].x, minZ = polygon[0].z, maxZ = polygon[0].z;
                for (int i = 1; i < polygon.Count; i++)
                {
                    if (polygon[i].x < minX) { minX = polygon[i].x; }
                    if (polygon[i].x > maxX) { maxX = polygon[i].x; }
                    if (polygon[i].z < minZ) { minZ = polygon[i].z; }
                    if (polygon[i].z > maxZ) { maxZ = polygon[i].z; }
                }

                int px0 = Mathf.FloorToInt((float)((minX - margin - mask.originXMetres) / metresPerPixel));
                int px1 = Mathf.CeilToInt((float)((maxX + margin - mask.originXMetres) / metresPerPixel));
                int pz0 = Mathf.FloorToInt((float)((minZ - margin - mask.originZMetres) / metresPerPixel));
                int pz1 = Mathf.CeilToInt((float)((maxZ + margin - mask.originZMetres) / metresPerPixel));
                if (px1 < 0 || pz1 < 0 || px0 > mask.widthPixels - 1 || pz0 > mask.heightPixels - 1)
                {
                    skipped++;
                    continue;
                }
                if (px0 < 0) { px0 = 0; }
                if (pz0 < 0) { pz0 = 0; }
                if (px1 > mask.widthPixels - 1) { px1 = mask.widthPixels - 1; }
                if (pz1 > mask.heightPixels - 1) { pz1 = mask.heightPixels - 1; }

                StampBox(mask, polygon, feather, px0, px1, pz0, pz1);
                stamped++;
            }

            if (skipped > 0)
            {
                remarks.Add(skipped.ToString(CultureInfo.InvariantCulture) + " of "
                            + footprints.Count.ToString(CultureInfo.InvariantCulture)
                            + " footprints were not stamped: they had fewer than three corners or fell outside the"
                            + " mask's own rectangle");
            }
            return stamped;
        }

        /// <summary> One footprint's coverage over one pixel box, at the pixel CENTRES. The signed distance is positive
        /// inside and negative outside, which is the convention <see cref="FootprintMask.FeatherCoverage"/> reads, so
        /// the feather here is the shipped ramp rather than a second one written to look like it. </summary>
        private static void StampBox(FootprintMaskClass mask, IList<Vector3> polygon, double feather,
            int px0, int px1, int pz0, int pz1)
        {
            double metresPerPixel = mask.metresPerPixel;
            for (int pz = pz0; pz <= pz1; pz++)
            {
                double worldZ = mask.originZMetres + (pz + 0.5) * metresPerPixel;
                int row = pz * mask.widthPixels;
                for (int px = px0; px <= px1; px++)
                {
                    double worldX = mask.originXMetres + (px + 0.5) * metresPerPixel;
                    Vector2 point = new Vector2((float)worldX, (float)worldZ);

                    float distance = Geometry2D.DistancePointToPolygonBoundary(polygon, point);
                    double signed = Geometry2D.Contains(polygon, point) ? distance : -distance;
                    double coverage = FootprintMask.FeatherCoverage(signed, feather);
                    if (coverage <= 0.0) { continue; }

                    int index = row + px;
                    if (coverage > mask.coverage[index]) { mask.coverage[index] = (float)coverage; }
                }
            }
        }

        /// <summary> The four runtime nodes: the chunk a position falls in, the spawn table, one frame of streaming,
        /// and the stamp that lets a stale bake be recognised as stale. </summary>
        private static bool RunRuntimeStage(CityBakeContext context, CityGraphNode node, List<string> remarks,
            out int itemCount, out string note)
        {
            itemCount = -1;
            note = string.Empty;

            if (node.kind == CityNodeKind.ChunkGrid)
            {
                Vector3 position;
                if (!TryInput(context, node, "position", out position, out note)) { return false; }

                float chunkSize = (float)Number(node, "chunkSizeMetres", 100.0, remarks);
                if (chunkSize <= 0f)
                {
                    note = "chunkSizeMetres is " + chunkSize.ToString(CultureInfo.InvariantCulture)
                           + " on this node: a chunk has to have an edge before a position can fall inside one";
                    return false;
                }

                ChunkCoord chunk = CityChunkGrid.ChunkOf(position, chunkSize);
                context.bus.Publish(node.id, "chunk", chunk);
                context.bus.Publish(node.id, "centre", CityChunkGrid.ChunkCentre(chunk, chunkSize));
                itemCount = 1;
                return true;
            }

            if (node.kind == CityNodeKind.SpawnTable)
            {
                CityData city;
                if (!TryInput(context, node, "city", out city, out note)) { return false; }

                float chunkSize = (float)Number(node, "chunkSizeMetres", 100.0, remarks);
                if (chunkSize <= 0f)
                {
                    note = "chunkSizeMetres is " + chunkSize.ToString(CultureInfo.InvariantCulture)
                           + " on this node, so every building would land in the same chunk";
                    return false;
                }

                CitySpawnTable table = CitySpawnTable.Build(city, chunkSize);
                context.bus.Publish(node.id, "spawnTable", table);
                itemCount = table.items.Count;
                if (itemCount == 0)
                {
                    note = "this city has no saved buildings, so the spawn table is empty and a loader would"
                           + " instantiate nothing";
                }
                return true;
            }

            if (node.kind == CityNodeKind.Streaming)
            {
                CityStreamingConfig config;
                if (!TryInput(context, node, "config", out config, out note)) { return false; }
                CitySpawnTable spawnTable;
                if (!TryInput(context, node, "spawnTable", out spawnTable, out note)) { return false; }
                ChunkCoord cameraChunk;
                if (!TryInput(context, node, "cameraChunk", out cameraChunk, out note)) { return false; }
                if (config == null)
                {
                    note = "no streaming config arrived, and the ring radius, the chunk edge and the per-frame budget"
                           + " are all in it";
                    return false;
                }

                List<ChunkCoord> chunksToLoad = LoadRingNearestFirst(cameraChunk, config.loadRingRadius);

                // ONE frame of the schedule, from a scheduler that starts empty. A graph node has no frames to persist
                // across: what this reports is the FIRST slice a loader would take of a freshly queued ring, which is
                // the figure an author is checking the budget against. The runtime loader keeps its own scheduler.
                CityStreamingScheduler scheduler = new CityStreamingScheduler();
                scheduler.EnqueueRange(spawnTable.ResidentBuildingIndices(cameraChunk, config.loadRingRadius));
                int queued = scheduler.PendingCount;
                List<int> thisFrame = scheduler.Pump(config.instantiationBudgetPerFrame);

                context.bus.Publish(node.id, "chunksToLoad", chunksToLoad);
                context.bus.Publish(node.id, "instantiateThisFrame", thisFrame);
                itemCount = chunksToLoad.Count;
                if (queued > thisFrame.Count)
                {
                    remarks.Add(queued.ToString(CultureInfo.InvariantCulture) + " buildings are resident and the"
                                + " budget takes " + thisFrame.Count.ToString(CultureInfo.InvariantCulture)
                                + " a frame, so this ring fills over "
                                + Mathf.CeilToInt(queued / (float)Mathf.Max(1, config.instantiationBudgetPerFrame))
                                    .ToString(CultureInfo.InvariantCulture) + " frames");
                }
                return true;
            }

            if (node.kind == CityNodeKind.BakeStampNode)
            {
                CityData city;
                if (!TryInput(context, node, "city", out city, out note)) { return false; }

                if (string.IsNullOrEmpty(context.request.generatorVersion))
                {
                    remarks.Add("this bake carries no generator version, so the stamp cannot say which build made it"
                                + " and a later bake cannot tell whether this one is stale");
                }
                BakeStamp stamp = BakeStamp.Create(city, context.request.generatorVersion,
                    context.request.sourceCityAssetGuid, context.request.bakedAtUtc);
                context.bus.Publish(node.id, "stamp", stamp);
                itemCount = 1;
                return true;
            }

            note = "the catalogue lists " + node.kind + " as shipped code in a later stage, but this runner has no case"
                   + " for it";
            return false;
        }

        /// <summary> The chunks inside the load ring, NEAREST FIRST. The ring is a Chebyshev square (a radius of 2 is a
        /// 5x5 neighbourhood, the same shape <see cref="CityChunkGrid.IsWithinLoadRing"/> tests), and ties at the same
        /// ring distance are broken by x then z -- so the order is fixed rather than whatever the scan produced, and
        /// two bakes of the same city queue their chunks identically. </summary>
        private static List<ChunkCoord> LoadRingNearestFirst(ChunkCoord cameraChunk, int loadRingRadius)
        {
            List<ChunkCoord> ring = new List<ChunkCoord>();
            int radius = loadRingRadius < 0 ? 0 : loadRingRadius;

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    ChunkCoord candidate = new ChunkCoord(cameraChunk.x + dx, cameraChunk.z + dz);
                    if (CityChunkGrid.IsWithinLoadRing(cameraChunk, candidate, radius)) { ring.Add(candidate); }
                }
            }

            ChunkCoord centre = cameraChunk;
            ring.Sort(delegate(ChunkCoord a, ChunkCoord b)
            {
                int byRing = CityChunkGrid.RingDistance(centre, a).CompareTo(CityChunkGrid.RingDistance(centre, b));
                if (byRing != 0) { return byRing; }
                int byX = a.x.CompareTo(b.x);
                return byX != 0 ? byX : a.z.CompareTo(b.z);
            });
            return ring;
        }

// DISPATCH-SHIPPED-TAIL
        #endregion

        #region "Reading a node"

        /// <summary> Reads one input port and casts it to what the caller needs.
        ///
        /// The cast is checked here rather than trusted, even though the graph already refuses a wire whose two declared
        /// payloads disagree: validation compares what the catalogue SAYS travels the wire, and this is what actually
        /// arrived. The two agree in every wired graph and the check costs one type test, which is the right price for
        /// the one case where they do not. </summary>
        private static bool TryInput<T>(CityBakeContext context, CityGraphNode node, string portName, out T value,
            out string note)
        {
            value = default(T);

            object arrived;
            if (!context.bus.TryReadInput(context.graph, node, portName, out arrived, out note)) { return false; }

            if (arrived is T)
            {
                value = (T)arrived;
                note = string.Empty;
                return true;
            }

            note = "the port " + portName + " on " + node.DisplayName() + " received "
                   + (arrived == null ? "nothing" : "a " + arrived.GetType().Name) + " where a " + typeof(T).Name
                   + " is needed";
            return false;
        }

        /// <summary> One number parameter, with the catalogue's own default when the node does not carry it.
        ///
        /// A missing parameter is RECORDED rather than swallowed. The graph's validation checks that every parameter a
        /// node carries has a name and that no name is doubled, but not that every parameter the catalogue declares is
        /// present -- so a hand-edited graph can reach here short of one, and an author who is told which default was
        /// used can fix it. Silence would leave them comparing two bakes and guessing. </summary>
        private static double Number(CityGraphNode node, string name, double fallback, List<string> remarks)
        {
            CityGraphParameter parameter;
            string why;
            double value;

            if (node.TryGetParameter(name, out parameter, out why) && parameter.TryGetNumber(out value, out why))
            {
                return value;
            }

            remarks.Add("the parameter " + name + " could not be read (" + why + "), so " + fallback + " was used");
            return fallback;
        }

        /// <summary> One text parameter, on the same terms as <see cref="Number"/>. </summary>
        private static string Text(CityGraphNode node, string name, string fallback, List<string> remarks)
        {
            CityGraphParameter parameter;
            string why;
            string value;

            if (node.TryGetParameter(name, out parameter, out why) && parameter.TryGetText(out value, out why))
            {
                return value;
            }

            remarks.Add("the parameter " + name + " could not be read (" + why + "), so "
                        + (string.IsNullOrEmpty(fallback) ? "nothing" : fallback) + " was used");
            return fallback;
        }

        /// <summary> One switch parameter, on the same terms as <see cref="Number"/>. </summary>
        private static bool Flag(CityGraphNode node, string name, bool fallback, List<string> remarks)
        {
            CityGraphParameter parameter;
            string why;
            bool value;

            if (node.TryGetParameter(name, out parameter, out why) && parameter.TryGetFlag(out value, out why))
            {
                return value;
            }

            remarks.Add("the parameter " + name + " could not be read (" + why + "), so " + (fallback ? "on" : "off")
                        + " was used");
            return fallback;
        }

        /// <summary> The remarks as one sentence, or nothing when there are none. Semicolons rather than newlines,
        /// because this lands in a one-line field on the node. </summary>
        private static string Join(List<string> remarks)
        {
            if (remarks == null || remarks.Count == 0) { return string.Empty; }
            return string.Join("; ", remarks.ToArray());
        }

        #endregion
    }
}

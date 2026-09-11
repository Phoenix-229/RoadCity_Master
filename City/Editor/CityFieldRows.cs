// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using RoadCityMaster.City;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-15 / C-10. The city half of RM-51's field table: every <see cref="City.CityAuthoring"/> field and
    /// every NODE PARAMETER the city catalogue declares, each with its foldout, its unit, its range and the SOURCE of
    /// that range. <see cref="InspectorFieldRegistry.All"/> seeds these on top of the road's own rows, so one table
    /// answers for both halves of the tool and one gate proves both.
    ///
    /// It is a separate file under City/Editor rather than more lines inside the shipped registry because the asmref
    /// beside it puts City/Editor and Scripts/Editor in ONE assembly: the rows can live next to the city code they
    /// describe and still cost the registry a single call.
    ///
    /// The four foldouts were named for a road, and a city node is not a road field, so the mapping is stated once
    /// here rather than decided row by row: a SOURCE node describes what the region IS; a planning or runtime node
    /// describes what is GENERATED from it; streetscape and rail furniture is what it LOOKS LIKE; and anything that
    /// writes terrain is what it DOES TO THE WORLD.
    ///
    /// Two kinds of name are handled differently on purpose. A component key is never typed -- it comes from
    /// <see cref="CityNodeKind"/> through <see cref="Row(List{FieldDescriptor}, CityNodeKind, string, string,
    /// FieldGroup, FieldUnit, FieldRange, string)"/>, and the settlement-order rows are generated through the same
    /// public <see cref="CityNodeCatalog.OrderParameterPrefix"/> the catalogue itself wrote them with, so neither can
    /// drift. A PARAMETER name is typed, because its unit, its range and the citation for that range are per-parameter
    /// knowledge no loop can derive -- which is exactly why C-12's gate asserts that every parameter the catalogue
    /// declares has a row here, rather than trusting this file to be complete.
    ///
    /// <see cref="FieldRange.None"/> appears often and is deliberate, not an omission: where no code constant and no
    /// real-world practice fixes a limit -- a seed, a note, a water level that may be below zero -- C-15's rule is to
    /// record no range rather than invent one an author would then have to fight. </summary>
    internal static class CityFieldRows
    {
        /// <summary> The component key CityAuthoringEditor looks its own fields up under. Spelled once. </summary>
        private const string Component = "CityAuthoring";

        /// <summary> One row for a node kind. The component key is the kind's own name, so renaming a kind moves its
        /// rows with it and cannot leave them orphaned behind a stale string. </summary>
        private static void Row(List<FieldDescriptor> f, CityNodeKind kind, string field, string label,
            FieldGroup group, FieldUnit unit, FieldRange range, string tooltip)
        {
            f.Add(new FieldDescriptor(kind.ToString(), field, label, group, unit, range, tooltip));
        }

        /// <summary> One row for a plain component -- in practice the authoring MonoBehaviour, which is not a node.
        /// </summary>
        private static void Row(List<FieldDescriptor> f, string component, string field, string label,
            FieldGroup group, FieldUnit unit, FieldRange range, string tooltip)
        {
            f.Add(new FieldDescriptor(component, field, label, group, unit, range, tooltip));
        }

        /// <summary> A figure quoted inside a range's source sentence, so a citation names the constant's real value
        /// and cannot fall out of step with it. </summary>
        private static string Figure(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary> The range of the many city parameters whose ZERO is not a measurement but a deliberate "say
        /// nothing here": the operator then uses the figure its own class or archetype carries. So the limit is a
        /// floor of zero rather than a floor of the smallest useful value -- forbidding zero would remove the only
        /// way an author has to leave the decision where it already lives. </summary>
        private static FieldRange ZeroMeansOperatorDefault()
        {
            return FieldRange.AtLeast(0.0,
                "AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own "
                + "documented way of saying the operator should use the figure its class or archetype carries");
        }

        /// <summary> The range of C-17's embed depth, the one city parameter whose NEGATIVE value is meaningful.
        /// The floor is -1 because that is <c>GroundSeating.DeriveEmbedFromFootprint</c>: it asks for the depth to be
        /// worked out from the prefab's footprint radius and the local slope rather than typed. The ceiling is
        /// <c>GroundSeatingClass.maximumEmbedMetres</c> -- <see cref="GroundDriftClass.visibleMismatchMetres"/> --
        /// about one kerb upstand, and the depth past which the runtime clamps anyway, so a wider range would
        /// describe something the code does not do. </summary>
        private static FieldRange EmbedDepth()
        {
            return FieldRange.Between(-1.0, 0.150,
                "GroundSeating.DeriveEmbedFromFootprint is -1 and GroundSeatingClass clamps at 0.150 m, which is "
                + "GroundDriftClass.visibleMismatchMetres -- roughly one kerb upstand");
        }

        /// <summary> The range of a streaming chunk edge. Its floor is observed behaviour rather than an authored
        /// choice -- <see cref="CityChunkGrid"/> substitutes one metre for any non-positive size, so a smaller figure
        /// is not obeyed and a range that allowed it would be describing something the code does not do. There is no
        /// ceiling: one chunk for a whole village is a legitimate answer. </summary>
        private static FieldRange ChunkEdge()
        {
            return FieldRange.AtLeast(1.0,
                "CityChunkGrid substitutes 1 m for a non-positive chunk size, so one metre is the smallest edge the "
                + "grid itself will honour; the streaming config ships 100 m");
        }

        /// <summary> The range of a WEIGHT that is one share of a single decision: zero to one, with the shipped
        /// default named. The five suitability weights are the case, and the citation is arithmetic rather than
        /// authored -- the figures the catalogue ships sum to 1.0, which is what makes each one a share of the whole
        /// and not a magnitude that could sensibly exceed it. </summary>
        private static FieldRange Share(double shipped)
        {
            return FieldRange.Between(0.0, 1.0,
                "a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one "
                + "ships " + Figure(shipped));
        }

        /// <summary> Add every city row to the registry's list, in the order an author meets them. </summary>
        public static void Seed(List<FieldDescriptor> f)
        {
            if (f == null) { return; }
            // --- CityAuthoring: what the settlement IS ---
            // The labels are the ones the inspector was already drawing. Registering a field hands its label and its
            // tooltip over to the drawer, so any wording invented here would silently change what an author reads.
            Row(f, Component, "graph", "Graph", FieldGroup.WhatItIs, FieldUnit.Reference, FieldRange.None(),
                "Which city graph asset this settlement runs. One graph may be shared by many settlements: the graph "
                + "is the recipe, the seed and the rectangle are what make each one different.");
            Row(f, Component, "seed", "Seed", FieldGroup.WhatItIs, FieldUnit.None, FieldRange.None(),
                "The one generator input. The same seed, graph and rectangle produce the same city on every machine "
                + "and every re-bake.");
            Row(f, Component, "regionCentre", "Region centre", FieldGroup.WhatItIs, FieldUnit.Reference,
                FieldRange.None(),
                "The Transform the rectangle is measured from. Drag that Transform and the settlement moves with it.");
            Row(f, Component, "authoredAlignment", "Authored alignment", FieldGroup.WhatItIs,
                FieldUnit.Reference, FieldRange.None(),
                "The optional horizontal plan the author owns. A finished corridor may guide it, but the plan remains "
                + "serialized here until the author installs it as a road.");
            // No range: a rectangle is four numbers and Validate takes one. Its floor is enforced where it can be
            // enforced usefully -- on the Scene handles, by CityAuthoringEditor's own minimum -- because a rectangle
            // dragged to nothing takes its own handles away with it.
            Row(f, Component, "regionXZ", "Region (local X/Z)", FieldGroup.WhatItIs, FieldUnit.Metres,
                FieldRange.None(),
                "The rectangle the settlement occupies, in metres, relative to the region centre. Drag its corners in "
                + "the Scene view rather than typing them, unless a figure has to be exact.");
            Row(f, Component, "sourceRoad", "Source road", FieldGroup.WhatItIs, FieldUnit.Reference, FieldRange.None(),
                "The road whose free end this settlement grew from. It is what gives the settlement its order -- a "
                + "capital, a city, a town or a village -- because the role is declared on the road, not here.");
            Row(f, Component, "sourceEnd", "Source end", FieldGroup.WhatItIs, FieldUnit.None, FieldRange.None(),
                "Which end of that road. A road has two, and a settlement stands at one.");
            Row(f, Component, "archetypeOverride", "Archetype override", FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.None(),
                "Auto means measure it from the ground. Any other value is a disagreement with the measurement, and "
                + "the measured value is shown below so the disagreement is visible.");

            // --- CityAuthoring: what it LOOKS like (where the content comes from) ---
            Row(f, Component, "prefabLibraries", "Prefab libraries", FieldGroup.WhatItLooksLike, FieldUnit.Reference,
                FieldRange.None(),
                "The category sets the bake chooses assets from. Leaving this empty is legal -- the bake then produces "
                + "the network and the blocks and places nothing on them.");
            Row(f, Component, "scanRoots", "Scan roots", FieldGroup.WhatItLooksLike, FieldUnit.Reference,
                FieldRange.None(),
                "Scene objects the bake reads rather than replaces: content that already stands on this ground. "
                + "Leaving it empty means the settlement's own root is scanned.");

            // --- CityAuthoring: what is GENERATED from it ---
            Row(f, Component, "cityData", "City data", FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Reference,
                FieldRange.None(),
                "The asset the bake writes its result into. One per settlement: sharing one between two settlements "
                + "means the second bake overwrites the first one's city.");

            SeedSourceNodes(f);
            SeedPlanNodes(f);
            SeedStreetAndLandNodes(f);
            SeedInfrastructureNodes(f);
            SeedTerrainAndRuntimeNodes(f);
            SeedCanvasNodes(f);
            SeedElementPlacement(f);
            SeedHandPlacedChildren(f);
        }

        /// <summary> Source nodes: what the region IS before an operator touches it. </summary>
        private static void SeedSourceNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.WaterSurface, "hasWater", "Has water", FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.None(),
                "Whether this region has standing water at all. Off means the water level below is ignored rather "
                + "than treated as a sea at zero, which is what a dry inland site needs.");
            // No range. A water surface is a world height and the world's zero is arbitrary: a lake in a valley floor
            // sits below it, so a minimum of zero would refuse the commonest case.
            Row(f, CityNodeKind.WaterSurface, "waterSurfaceY", "Water level", FieldGroup.WhatItIs, FieldUnit.Metres,
                FieldRange.None(),
                "The height standing water reaches, in world metres. Ground below this is water rather than "
                + "buildable land, so it decides where the settlement's shoreline runs.");
            Row(f, CityNodeKind.RoadClassSource, "classId", "Road class", FieldGroup.WhatItIs, FieldUnit.Index,
                FieldRange.AtLeast(RoadClassCatalog.ArterialId,
                    "RoadClassCatalog's own ids, which start at Arterial (" + Figure(RoadClassCatalog.ArterialId)
                    + "); Collector is " + Figure(RoadClassCatalog.CollectorId) + " and Local is "
                    + Figure(RoadClassCatalog.LocalId)),
                "Which class in the road catalogue this source hands the graph. The class is what carries the lane "
                + "widths and the design speed, so it decides the dimensions of every street the city then builds.");
            Row(f, CityNodeKind.ZoneCatalogueSource, "zoneId", "Zone", FieldGroup.WhatItIs, FieldUnit.Reference,
                FieldRange.None(),
                "Which zone in the zone catalogue this source hands the graph. Left empty the graph reads the "
                + "catalogue's unzoned entry, which is land with no land use declared rather than an error.");
        }

        /// <summary> Planning nodes: the reasoning that turns ground into a plan. </summary>
        private static void SeedPlanNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.SuitabilityRaster, "metresPerCell", "Cell size", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Metres, FieldRange.Between(1.0, 64.0,
                    "AUTHORED band around the 16 m the catalogue ships: below a metre the raster is finer than the "
                    + "terrain it samples and costs memory for no new information, above 64 m a whole city block "
                    + "falls inside one cell and the ranking can no longer tell one site from its neighbour"),
                "How much ground each suitability cell covers. Smaller cells rank sites more finely and cost memory "
                + "and time squared, because halving this quadruples the number of cells.");
            // The five weights are a share of one decision, and the shipped figures sum to 1.0. That sum is the
            // citation: it is why each one is a fraction of the whole and not an arbitrary magnitude.
            Row(f, CityNodeKind.SuitabilityRaster, "slopeWeight", "Slope weight", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Fraction, Share(0.45),
                "How much steep ground counts against a site. The heaviest of the five as shipped, because slope is "
                + "what decides whether a street can be built at all rather than merely built expensively.");
            Row(f, CityNodeKind.SuitabilityRaster, "earthworkWeight", "Earthwork weight",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Fraction, Share(0.25),
                "How much cut and fill volume counts against a site. Raise it where moving ground is the expensive "
                + "part of building here, lower it where the machinery is already on site.");
            Row(f, CityNodeKind.SuitabilityRaster, "waterWeight", "Water weight", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Fraction, Share(0.15),
                "How much nearness to water counts. It cuts both ways in real settlement: a river is why a town is "
                + "there and also what floods it, so this is the dial between the two.");
            Row(f, CityNodeKind.SuitabilityRaster, "landUseWeight", "Land use weight",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Fraction, Share(0.10),
                "How much an existing land use counts. Raise it to make the plan respect what the ground is already "
                + "used for rather than rank it as empty buildable land.");
            Row(f, CityNodeKind.SuitabilityRaster, "networkWeight", "Network weight",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Fraction, Share(0.05),
                "How much distance from the existing network counts. The lightest as shipped, because the corridor "
                + "solve that follows is what actually joins the site to the roads around it.");
            Row(f, CityNodeKind.CorridorSolve, "toleranceFraction", "Corridor tolerance",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Fraction, FieldRange.Between(0.0, 1.0,
                    "a fraction of the corridor's own cost, so one is the whole of it; the solver ships "
                    + Figure(CorridorSolver.DefaultToleranceFraction)),
                "How far above the cheapest corridor the solve may still accept a route. A little slack lets it "
                + "prefer a straighter line over a marginally cheaper crooked one; zero demands the cheapest exactly.");
        }

        /// <summary> Street and land nodes: blocks, lots, and what may be built on them. </summary>
        private static void SeedStreetAndLandNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.BlockInset, "fallbackInsetMetres", "Fallback inset",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How far a block's edge is pulled back from the street where the road class does not say. An inset is "
                + "the footway and verge between the carriageway and the first building line.");
            Row(f, CityNodeKind.LotSubdivision, "targetAreaM2", "Target lot area", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.SquareMetres, ZeroMeansOperatorDefault(),
                "The lot size the subdivision aims at. It is a target and not a guarantee: a block divides into whole "
                + "lots, so the achieved areas fall either side of this figure.");
            Row(f, CityNodeKind.LotSubdivision, "splitJitter", "Split jitter", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Fraction, FieldRange.Between(0.0, 1.0,
                    "AUTHORED: a fraction of the split's own spacing, so one whole lot width is the most a split can "
                    + "be moved and still be the same split"),
                "How much lot widths are allowed to vary within a block. Zero gives every lot the same frontage, "
                + "which reads as a planned estate; a little variation reads as a street that grew.");
            Row(f, CityNodeKind.BuildableLand, "sampleSpacingMetres", "Sample spacing",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How far apart the ground is tested when deciding what is buildable. Closer sampling catches a small "
                + "rock outcrop or a ditch that wider sampling steps over.");
            Row(f, CityNodeKind.ZonePaint, "straddleTieFraction", "Straddle tie", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Fraction, FieldRange.Between(0.0, 1.0,
                    "AUTHORED: a share of one lot's area, so the whole lot is the most that can lie on either side "
                    + "of a zone boundary"),
                "How a lot that lies across a zone boundary is decided. It is the share of its area that has to fall "
                + "in one zone before that zone takes it, rather than the boundary cutting the lot in two.");
            Row(f, CityNodeKind.PrefabValidate, "category", "Category", FieldGroup.WhatItLooksLike,
                FieldUnit.Reference, FieldRange.None(),
                "Which content category this node checks before the bake places any of it. The same category key the "
                + "libraries, the manifest and the resolver use, so all four agree about what was checked.");
            Row(f, CityNodeKind.PrefabValidate, "requester", "Requester", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "Who asked for the check, recorded so a failure report names the node that wanted the content rather "
                + "than only the content that failed.");
        }

        /// <summary> Infrastructure nodes: the poles, wires and rails a settlement is strung with. Their figures are
        /// authored on the CLASS components (RailTrack, DistributionPole, TransmissionLineClass) rather than restated
        /// here, which is why every range below is a floor of zero and not a band: the node parameter is an override
        /// for one run, and leaving it at zero is how an author defers to the class. </summary>
        private static void SeedInfrastructureNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.PolesAndSpans, "runLengthMetres", "Run length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How long a pole run this node strings, measured along the street it follows. Zero takes the length "
                + "from the street itself, which is what puts poles down a whole road rather than part of one.");
            Row(f, CityNodeKind.Rail, "designSpeedKmh", "Design speed", FieldGroup.WhatItLooksLike,
                FieldUnit.KmPerHour, ZeroMeansOperatorDefault(),
                "The speed this line's geometry is built for. It is what decides the curve radii and the cant, so "
                + "raising it makes the alignment straighter and flatter rather than making trains faster.");
            Row(f, CityNodeKind.Ropeway, "runLengthMetres", "Run length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How far the ropeway travels from end to end. Zero takes the distance between the two stations the "
                + "node is wired to rather than fixing a length here.");
            Row(f, CityNodeKind.Ropeway, "spanLengthMetres", "Span length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How far apart the towers stand along that run. It is the figure that decides how many towers a "
                + "ropeway needs and how deeply the cable sags between them.");
        }

        /// <summary> Terrain and runtime nodes: what the city writes into the ground, and how it is streamed back.
        /// </summary>
        private static void SeedTerrainAndRuntimeNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.FootprintPublish, "metresPerPixel", "Footprint resolution",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres, FieldRange.Between(0.5, 16.0,
                    "AUTHORED band around the 4 m the catalogue ships: a pixel finer than half a metre resolves a "
                    + "kerb the conform cannot honour anyway, and one coarser than 16 m is wider than a street, so "
                    + "the footprint would stop describing the thing it is masking"),
                "How finely the city's footprints are rasterised for the terrain conform. Finer costs memory and "
                + "conform time; coarser rounds off the corners of pads and street edges.");
            Row(f, CityNodeKind.FootprintPublish, "featherWidthMetres", "Feather width",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres, ZeroMeansOperatorDefault(),
                "How wide the blend is between conformed ground and the ground around it. Zero is a hard step at the "
                + "footprint edge; a few metres is what makes a pad read as graded rather than stamped.");
            Row(f, CityNodeKind.FootprintConformNode, "cellAreaM2", "Cell area", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.SquareMetres, ZeroMeansOperatorDefault(),
                "The ground area one conform cell covers, used to turn a height change into a cut or fill volume. "
                + "Zero reads the area from the terrain itself rather than assuming one here.");
            // C-18. The hand-placed sweep. Its two tolerances are the road's own figures rather than a second opinion,
            // so the ranges READ GroundDriftClass instead of authoring a band of their own -- a row that quoted the
            // numbers would be free to fall out of step with them.
            GroundDriftClass driftClass = GroundDriftClass.Default();
            FootprintScanClass scanClass = FootprintScanClass.Default();
            Row(f, CityNodeKind.HandPlacedScan, "produceGroundRequests", "Ask for levelling",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.None, FieldRange.None(),
                "Off, the sweep only reports what it finds. On, objects outside tolerance ask for a flattened pad and "
                + "an apron. Off by default because the footprint used is a rectangle around the object's bounds, "
                + "which is generous for anything turned on the spot.");
            Row(f, CityNodeKind.HandPlacedScan, "supportToleranceMetres", "Support tolerance",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres,
                FieldRange.Between(0.001, driftClass.visibleMismatchMetres,
                    "GroundDriftClass.supportToleranceMetres is " + Figure(driftClass.supportToleranceMetres)
                    + " m, the finished-formation earthworks tolerance; the ceiling is "
                    + "GroundDriftClass.visibleMismatchMetres, because a support tolerance looser than the visible one "
                    + "would swap the two verdicts round"),
                "How far the ground may stand from an object's base before it is worth saying so. The same figure the "
                + "road side uses for its own ground checks.");
            Row(f, CityNodeKind.HandPlacedScan, "visibleMismatchMetres", "Visible mismatch",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres, FieldRange.Between(0.010, 1.0,
                    "GroundDriftClass.visibleMismatchMetres is " + Figure(driftClass.visibleMismatchMetres)
                    + " m, one kerb upstand; AUTHORED band around it, since a gap of a whole metre is a storey and "
                    + "stops being a mismatch"),
                "The gap a person sees rather than measures. Past this, a finding stops being advice.");
            Row(f, CityNodeKind.HandPlacedScan, "maximumStationSpacingMetres", "Station spacing ceiling",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Metres,
                FieldRange.Between(scanClass.minimumStationSpacingMetres, driftClass.stationSpacingMetres,
                    "the floor is FootprintScanClass.minimumStationSpacingMetres, which is one post of a "
                    + "1025-resolution heightmap over a 500 m tile; the ceiling is "
                    + "GroundDriftClass.stationSpacingMetres, past which an object would be sampled more coarsely "
                    + "than a road"),
                "The widest gap between two ground samples under one object. The spacing is otherwise worked out from "
                + "the footprint itself, so this is a ceiling and not the spacing.");
            Row(f, CityNodeKind.HandPlacedScan, "maximumObjects", "Object limit",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.None, FieldRange.AtLeast(1.0,
                    "AUTHORED: a sweep looks at at least one object, and there is no ceiling because reaching the "
                    + "limit is reported rather than silently accepted"),
                "How many objects one sweep looks at. A guard against naming a scene root instead of a container.");
            Row(f, CityNodeKind.HandPlacedScan, "includeInterior", "Sample the inside",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.None, FieldRange.None(),
                "Sample inside the footprint as well as around its edge. On, because a bump under the middle of a "
                + "slab lifts no corner: all four read low together, which an edge-only sweep calls a hollow.");
            Row(f, CityNodeKind.HandPlacedScan, "contactMode", "How it meets the ground",
                FieldGroup.WhatItIs, FieldUnit.None, FieldRange.None(),
                "OnPad, SkirtToTerrain or Piled. Piled objects are meant to stand clear, so the gap under them is not "
                + "judged -- the same exemption a bridge deck already has.");
            Row(f, CityNodeKind.HandPlacedScan, "elevationRule", "Level a pad is cut to",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.None, FieldRange.None(),
                "CentroidSample or MinCornerPlusFreeboard. Only used when levelling is asked for, and read by the "
                + "building pad conform out of the request rather than decided there.");
            // The floor is not authored: CityChunkGrid substitutes 1 m for any non-positive size, so a metre is the
            // smallest edge the grid itself will honour and anything below it is silently ignored rather than obeyed.
            Row(f, CityNodeKind.ChunkGrid, "chunkSizeMetres", "Chunk size", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Metres, ChunkEdge(),
                "The edge of one streaming chunk. Smaller chunks load and unload in finer steps and multiply the "
                + "number of chunks; larger ones cost less bookkeeping and more work per load.");
            Row(f, CityNodeKind.SpawnTable, "chunkSizeMetres", "Chunk size", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Metres, ChunkEdge(),
                "The chunk edge the spawn table files its rows under. It has to be the same figure the grid and the "
                + "streaming config use, or a chunk will be asked for rows that were filed under a different one.");
        }

        /// <summary> Canvas nodes: the two that never execute. They still carry fields an author types into, so they
        /// still belong in the table -- a comment with no unit column and no hover sentence is the inconsistency
        /// RM-51 exists to remove. </summary>
        private static void SeedCanvasNodes(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.OverrideMarker, "element", "Element", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.None, FieldRange.None(),
                "Which kind of element is hand-owned here -- a lot or a block. It names the id below, so the two are "
                + "read together rather than an id standing on its own.");
            Row(f, CityNodeKind.OverrideMarker, "id", "Element id", FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Index,
                FieldRange.AtLeast(0.0,
                    "AUTHORED: element ids are handed out from zero upward, so a negative id names nothing the bake "
                    + "could find"),
                "That element's id -- the same Block.id or Lot.id the Scene handles show, which is how a marker "
                + "survives a re-bake that moves the geometry around it.");
            Row(f, CityNodeKind.OverrideMarker, "note", "Note", FieldGroup.WhatIsGeneratedFromIt, FieldUnit.None,
                FieldRange.None(),
                "Why this element is hand-owned, for whoever opens the graph next. The bake never reads it; it is the "
                + "one place the reason for an override can be written down beside the override.");
            Row(f, CityNodeKind.CommentGroup, "title", "Title", FieldGroup.WhatIsGeneratedFromIt, FieldUnit.None,
                FieldRange.None(),
                "The heading drawn on the comment group, which is what a reader sees before they read the note "
                + "itself. Keep it short enough to sit on one line at any zoom.");
            Row(f, CityNodeKind.CommentGroup, "body", "Body", FieldGroup.WhatIsGeneratedFromIt, FieldUnit.None,
                FieldRange.None(),
                "The note itself. It documents the graph for the next person to open it and has no effect whatever on "
                + "what a bake produces.");
        }
        /// <summary> The element-placement rows. One node KIND carries all 140 content categories, so its parameters
        /// are two per-node rows plus the same four shapes repeated per settlement order -- and those names are
        /// GENERATED through the same public <see cref="CityNodeCatalog.OrderParameterPrefix"/> the catalogue wrote
        /// them with. Typing "capital.minSpacingMetres" sixteen times would be sixteen chances for the registry to
        /// describe a parameter the runner never reads. </summary>
        private static void SeedElementPlacement(List<FieldDescriptor> f)
        {
            Row(f, CityNodeKind.ElementPlacement, CityNodeCatalog.ElementCategoryParameter, "Category",
                FieldGroup.WhatItLooksLike, FieldUnit.Reference, FieldRange.None(),
                "Which content category this node places. It is what makes one node kind serve every category in the "
                + "manifest, so the rows below are read as this category's rules and nobody else's.");
            Row(f, CityNodeKind.ElementPlacement, "alignToSurfaceNormal", "Lie on the slope",
                FieldGroup.WhatItLooksLike, FieldUnit.None, FieldRange.None(),
                "Whether these follow the ground's slope instead of standing plumb. On for anything that RESTS on the "
                + "ground and would show daylight under one end otherwise: a bench, a bin, a picnic table, a paving "
                + "slab. Off for anything that grows or is driven into it: a tree, a lamp column, a post, a sign. Off "
                + "is the shipped answer because a stiff bench reads as stiff and a tilted tree reads as broken.");

            for (int order = SettlementOrderCatalog.CapitalOrder; order <= SettlementOrderCatalog.VillageOrder; order++)
            {
                string prefix = CityNodeCatalog.OrderParameterPrefix(order);
                if (string.IsNullOrEmpty(prefix)) { continue; }
                string ofOrder = " in a " + prefix;

                Row(f, CityNodeKind.ElementPlacement, prefix + ".present", Title(prefix) + ": present",
                    FieldGroup.WhatItLooksLike, FieldUnit.None, FieldRange.None(),
                    "Whether this category appears at all" + ofOrder + ". Turning it off is how a category stops "
                    + "being placed at one order without changing anything about the others.");
                Row(f, CityNodeKind.ElementPlacement, prefix + ".perKilometreOfFrontage",
                    Title(prefix) + ": per km of frontage", FieldGroup.WhatItLooksLike, FieldUnit.PerKilometre,
                    ZeroMeansOperatorDefault(),
                    "How many of this category stand per kilometre of street frontage" + ofOrder + ". A density "
                    + "rather than a count, so a long street gets more of them than a short one without being told.");
                Row(f, CityNodeKind.ElementPlacement, prefix + ".minSpacingMetres",
                    Title(prefix) + ": minimum spacing", FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                    ZeroMeansOperatorDefault(),
                    "The closest two of this category may stand" + ofOrder + ". It is what stops a high density from "
                    + "crowding a short frontage: the density asks for a number, this refuses to bunch them.");
                Row(f, CityNodeKind.ElementPlacement, prefix + ".embedDepthMetres",
                    Title(prefix) + ": embed depth", FieldGroup.WhatItLooksLike, FieldUnit.Metres, EmbedDepth(),
                    "How far below the ground the base of one of these sits" + ofOrder + ". Per order because a "
                    + "village's verge is rougher than a capital's kerbline. Leave it at -1 and the depth is worked "
                    + "out from the prefab's own footprint radius and the local slope, which is what actually removes "
                    + "the daylight under a base; 0 means sit exactly on the surface.");
            }
        }

        /// <summary> B-27's hand-placed children: components the AUTHOR adds under a road they drew themselves, not
        /// nodes a graph runs. Their rows belong here for the same reason the node rows do -- a field and the row that
        /// describes it are two edits, and a component whose fields never reach the registry is a component drawn
        /// without units, without ranges and without a sentence explaining what it does.
        ///
        /// The paths are dotted because the answers live on a nested parameter block: the electrical figures stay on
        /// <c>DistributionPoleClass</c> and the pole's dimensions on <c>PoleGeometryClass</c>, so the span length has
        /// one home and is reached at <c>settings.electrical.spanLengthMetres</c> rather than copied onto the
        /// component. <see cref="InspectorFieldDrawer"/> resolves a dotted path the same way the road's own
        /// <c>terrainFootprint.shoulderMetres</c> is resolved. </summary>
        private static void SeedHandPlacedChildren(List<FieldDescriptor> f)
        {
            const string PowerLine = "RoadPowerLine";

            Row(f, PowerLine, "settings.enabled", "Carries a line", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Whether this road carries a distribution line at all. Off leaves the component in place with its "
                + "answers intact and plants nothing, which is how a run is taken out without losing how it was set.");
            Row(f, PowerLine, "settings.circuit", "Circuit class", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Which column of the clearance table the spans are judged against. Primary is an open supply "
                + "conductor of 750 V to 22 kV and needs 5.6 m over a road; Secondary is 0 to 750 V -- a neutral or a "
                + "service drop -- and is allowed 4.7 m. Setting this wrong is the one field here that turns a "
                + "refusal into a pass without moving a pole.");
            Row(f, PowerLine, "settings.side", "Frontage side", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Which side of the road the poles stand on. A real run stays on one side for its whole length rather "
                + "than crossing, so this is one answer for the run and not a per-pole choice.");
            Row(f, PowerLine, "settings.offsetBeyondEdgeMetres", "Offset beyond carriageway",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(0.5, 9.0,
                    "AASHTO Roadside Design Guide clear zone: 0.5 m is a pole set behind an urban kerb, and 9 m "
                    + "covers the widest clear zone tabulated for a high-speed, high-volume road on fill"),
                "How far from the edge of the carriageway the pole centres stand. About 1 m is the urban figure, just "
                + "behind the kerb line; a rural or high-speed frontage wants the clear zone for its design speed, "
                + "which is several metres more.");
            Row(f, PowerLine, "settings.startStationMetres", "Run starts at", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.AtLeast(0.0, "AUTHORED: stations are measured from the road's own start"),
                "Where along the road the run begins, in metres from the road's start. Moving this slides the whole "
                + "run rather than renumbering it, because a pole's station is measured from the run's own start.");
            Row(f, PowerLine, "settings.endStationMetres", "Run ends at", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.AtLeast(-1.0, "AUTHORED: -1 means run to the end of the road"),
                "Where along the road the run ends. Leave it at -1 to follow the road to its end, so extending the "
                + "road extends the line with it.");
            Row(f, PowerLine, "settings.electrical.spanLengthMetres", "Span length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(30.0, 90.0,
                    "Overhead distribution practice: 40 m nominal, down to 30 m on dense urban frontage and up to "
                    + "90 m on open rural line"),
                "The nominal distance between poles. It is the number the run is quantised to before any pole is "
                + "moved aside for furniture, and it is the length the wire's sag is computed over.");
            Row(f, PowerLine, "settings.phaseCount", "Phases", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.Between(1.0, 3.0,
                    "Utility practice: an overhead distribution circuit is single-phase, two-phase or three-phase"),
                "How many phase conductors the cross-arm carries. They are centred on the pole at the arm's pin "
                + "spacing, so three sit at the middle pin and one either side.");
            Row(f, PowerLine, "settings.secondaryRack", "Secondary rack", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Whether every pole carries a secondary rack below the neutral -- the low-voltage conductors that "
                + "feed the buildings. On for a street that is served from this line; off for a line that only "
                + "passes through.");
            Row(f, PowerLine, "settings.communicationStrung", "Communication strand", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "Whether a telephone or cable strand shares these poles, in the joint-use space NESC Rule 238 keeps "
                + "below the supply conductors. On for a street, which is what joint use looks like in a city; off "
                + "for a rural primary run, which carries nothing but the utility's own wires. The strand hangs the "
                + "separation below the LOWEST supply attachment on the pole -- the secondary where there is a rack, "
                + "the neutral where there is not -- so switching the rack off lifts the strand with it.");
            Row(f, PowerLine, "settings.transformerEveryNPoles", "Transformer every", FieldGroup.WhatItLooksLike,
                FieldUnit.None,
                FieldRange.AtLeast(0.0,
                    "AUTHORED: 0 places none; a pole-mounted distribution transformer commonly serves four to ten "
                    + "spans of secondary"),
                "How often a transformer appears along the run, counted in poles. A transformer never arrives alone: "
                + "the pole that carries one also gets its fused cut-out and its surge arrester, because that is how "
                + "one is actually connected.");
            Row(f, PowerLine, "settings.guyTurnDegrees", "Guy above turn of", FieldGroup.WhatItLooksLike,
                FieldUnit.Degrees,
                FieldRange.Between(0.0, 90.0,
                    "AUTHORED: below the threshold an intermediate pole is in balance and needs no guy; 0 guys every "
                    + "pole and 90 guys none but the two ends"),
                "How sharply the line must turn at a pole before that pole is guyed. The two ends of the run are "
                + "always guyed whatever this says, because a dead-end takes the conductors' full unbalanced pull.");
            Row(f, PowerLine, "centrelineSampleSpacingMetres", "Curve sample spacing",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(1.0, 25.0,
                    "AUTHORED: fine enough that a span end lands on the curve at any radius a road is built to, "
                    + "coarse enough that a kilometre of road is hundreds of points and not thousands"),
                "How finely the road's curve is read before the run is planned. It affects where poles land on a "
                + "bend and how precisely a bridge or tunnel span is excluded, not the spacing between poles.");
            Row(f, PowerLine, "settings.timberMaterial", "Timber material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material the pole shafts, cross-arms and braces wear. The tool draws the timber and never makes a "
                + "material for it -- nothing in this tool creates a material -- so this slot is yours. Left empty, "
                + "the run still builds and passes its clearances; it just renders in Unity's missing-material pink.");
            Row(f, PowerLine, "settings.hardwareMaterial", "Hardware material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material for everything bolted to the timber: insulators, the transformer can, the cut-out and "
                + "arrester, the guys. It is a second slot rather than one shared with the timber because glass on a "
                + "creosoted pole is the difference a viewer reads first, and every mesh here has both submeshes.");
            Row(f, PowerLine, "settings.conductorMaterial", "Conductor material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material the strung wires wear. A bare ACSR conductor is dull grey and almost unlit; a material "
                + "with a strong specular highlight will read as a row of bright white lines from any distance.");

            SeedRailAlignment(f);
            SeedTransmissionRun(f);
        }

        /// <summary> B-27 / D-5's rows: a railway, placed the way a road is placed.
        ///
        /// Split into its own method rather than run on from the pole line's because the two components share
        /// nothing but the mechanism -- one is a run of poles along a road that already exists, the other is an
        /// alignment of its own with its own staked centreline -- and a single method carrying both would be a
        /// method nobody could read to find out which rows belong to which component.
        ///
        /// The ranges here are where rail stops being road, and they carry the reason in their own source text.
        /// Radius is in the HUNDREDS of metres where a road's is in the tens; the ruling gradient is in PER MILLE,
        /// an order of magnitude gentler than a road's per cent; and the banking is CANT in MILLIMETRES rather
        /// than superelevation as a percentage. Those are three different units for three quantities the road
        /// system also has, which is exactly why <c>FieldUnit</c> gained <c>Millimetres</c> and <c>PerMille</c>
        /// instead of a "mm" typed into a label: a figure whose unit lives in its label is a figure a converted
        /// inspector prints without one.
        ///
        /// What has NO row here is the plan itself. The staked centreline is a point list the author drags in
        /// the scene, not a number they type, and a row for it would be a row drawing an array of vectors in a
        /// foldout headed "what it looks like". The same goes for the station stops: D-8 owns them, and until it
        /// does they are a list the author edits through Unity's own default drawer. </summary>
        private static void SeedRailAlignment(List<FieldDescriptor> f)
        {
            const string Rail = "RailAlignment";

            Row(f, Rail, "settings.enabled", "Carries a railway", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Whether this alignment is staked at all. Off keeps every answer below and stakes nothing, which "
                + "is how a line is taken out of a scene without losing how it was set.");
            Row(f, Rail, "settings.trackCount", "Tracks", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.Between(1.0, 2.0,
                    "AUTHORED: one or two tracks staked from one centreline; three or more is a yard, which is "
                    + "laid out from its own geometry rather than offset from a line"),
                "How many tracks the formation carries. Two are placed either side of the centreline at the track "
                + "centres below, so the formation widens rather than the line moving. More than two is a yard and not "
                + "a line: the outer tracks of a six-track formation staked from one centreline would sit on a radius "
                + "nobody checked.");
            Row(f, Rail, "settings.electrified", "Overhead line", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Whether the line is wired. It does not change the track: what it changes is the clearance a "
                + "bridge over the railway has to leave, which is the contact wire height plus its electrical "
                + "clearance rather than the loading gauge alone.");
            Row(f, Rail, "settings.sampleSpacingMetres", "Sample spacing", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.5, 25.0,
                    "AUTHORED: fine enough that the tightest curve any class allows is cut into chords whose "
                    + "departure from the arc is under the ballast's own tolerance, coarse enough that ten "
                    + "kilometres of mainline is thousands of samples and not hundreds of thousands"),
                "How finely the alignment is sampled along its length. A chord's worst departure from the true "
                + "arc is about the spacing squared over eight times the radius, so 5 m on a 300 m mainline curve "
                + "is 10 mm and invisible, while the same 5 m on a 25 m tramway curve is 125 mm of rail cutting "
                + "the corner. The tramway column sets it to 1 m for that reason.");

            Row(f, Rail, "settings.track.minimumRadiusMetres", "Minimum curve radius", FieldGroup.WhatItIs,
                FieldUnit.Metres,
                FieldRange.Between(18.0, 7000.0,
                    "BOStrab 18 m exceptional for street tramway at the low end; UIC 703 gives 4000 m for 300 km/h and "
                    + "the widest built high-speed radii reach about 7000 m"),
                "The tightest curve this line may be staked to. A curve tighter than this is REFUSED rather than "
                + "kinked, and the refusal names the radius it needed: a kinked rail is the kind of error that "
                + "otherwise survives all the way to a screenshot. It is the one field here whose value is an order of "
                + "magnitude away from the road equivalent, and deliberately not wired to it.");
            Row(f, Rail, "settings.track.maxGradientPerMille", "Ruling gradient", FieldGroup.WhatItIs,
                FieldUnit.PerMille,
                FieldRange.Between(0.0, 60.0,
                    "Mainline ruling gradients sit near 15 per mille; BOStrab allows 40 per mille normal and 60 "
                    + "exceptional for a street tramway, which is the steepest adhesion railway in ordinary practice"),
                "The steepest the line may climb or fall, in per mille -- 15 per mille is 1.5 %. Rail climbs an order "
                + "of magnitude gentler than road, which is why this is its own number and not the road's percentage. "
                + "It is never violated: the vertical profile is swept under this limit, so what a gentle ruling "
                + "gradient costs is EARTH, and the earthwork is what refuses.");
            Row(f, Rail, "settings.track.designSpeedKmh", "Design speed", FieldGroup.WhatItIs,
                FieldUnit.KmPerHour,
                FieldRange.Between(20.0, 350.0,
                    "Street tramway running speeds start near 20 km/h; 350 km/h is the top of built high-speed "
                    + "practice"),
                "The speed the line is designed for. It sets the cant each curve wants, through E = (G V^2)/(127 R), "
                + "and the length of the transition into it. It is not a promise the whole line can be run at: a curve "
                + "too tight for this speed is buildable and ordinary, and what it gets is a speed restriction the "
                + "figures below report.");
            Row(f, Rail, "settings.track.maxAppliedCantMillimetres", "Maximum cant", FieldGroup.WhatItIs,
                FieldUnit.Millimetres,
                FieldRange.Between(0.0, 200.0,
                    "EN 13803: 150 mm is the common standard-gauge maximum and 180 mm is permitted on ballasted "
                    + "high-speed track; 200 mm is above anything in ordinary practice"),
                "How far the outer rail may be raised on a curve. The track is banked to the cant the speed and radius "
                + "want, capped here -- so this is the field that decides whether a tight curve is banked to "
                + "equilibrium or left with a deficiency. Street track is capped far lower than a mainline, because a "
                + "carriageway banked 150 mm across the gauge is a carriageway a road vehicle crosses on two wheels.");
            Row(f, Rail, "settings.track.maxCantDeficiencyMillimetres", "Maximum cant deficiency",
                FieldGroup.WhatItIs, FieldUnit.Millimetres,
                FieldRange.Between(0.0, 180.0,
                    "EN 13803: about 110 mm for conventional passenger stock, falling to 100 mm above 200 km/h; up to "
                    + "180 mm for tilting stock"),
                "How far the applied cant may fall short of what the speed wants before the curve needs a speed "
                + "restriction. It is the part of the curve's demand the banking does not answer, which is the part a "
                + "passenger feels sideways. Together with the maximum cant it fixes the speed any given radius can be "
                + "run at.");
            Row(f, Rail, "settings.track.maxCantRateMillimetresPerSecond", "Maximum cant rate",
                FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.Between(20.0, 70.0,
                    "Common practice: 55 mm/s permissible and 35 mm/s desirable for conventional stock, tightened to "
                    + "about 50 mm/s on high-speed line"),
                "How fast the outer rail may rise UNDER A TRAIN, in millimetres per second. It is one of the three "
                + "rules that decide how long a transition has to be, and the one that ties that length to the speed. "
                + "Lower it and every transition on the line grows.");
            Row(f, Rail, "settings.track.maxCantDeficiencyRateMillimetresPerSecond", "Maximum deficiency rate",
                FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.Between(20.0, 70.0,
                    "Common practice: about 55 mm/s for conventional stock, tightened to 50 mm/s above 200 km/h"),
                "The same limit applied to the cant DEFICIENCY -- the sideways force building on a passenger as the "
                + "curve tightens. A separate figure from the cant rate because a curve can be short of cant without "
                + "being short of cant rate, and the two then ask for different transition lengths. The longer wins.");
            Row(f, Rail, "settings.track.maxCantGradientMillimetresPerMetre", "Maximum cant gradient",
                FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.Between(0.5, 4.0,
                    "Common practice: 2.5 mm/m is 1 in 400, the usual permissible maximum, against 1 in 600 desirable; "
                    + "high-speed line tightens to 2 mm/m"),
                "How steeply the outer rail may climb ALONG the track, in millimetres of cant per metre. That is a "
                + "twist limit on the track itself rather than a comfort limit on the passenger, which is why it is a "
                + "third figure and not a restatement of the other two -- and it is the rule that still bites at low "
                + "speed, where both rate limits go slack.");

            Row(f, Rail, "settings.track.trackGaugeMetres", "Track gauge", FieldGroup.WhatItIs, FieldUnit.Metres,
                FieldRange.Between(0.6, 1.7,
                    "1.435 m is international standard gauge; 0.6 m covers narrow-gauge industrial track and 1.668 m "
                    + "Iberian broad gauge"),
                "The running-edge to running-edge distance between the rails. It is the identity of the track type and "
                + "where the two rails are drawn, but it is not the figure the cant equation uses -- that is the "
                + "rail-centre gauge below.");
            Row(f, Rail, "settings.track.cantFormulaGaugeMillimetres", "Cant formula gauge", FieldGroup.WhatItIs,
                FieldUnit.Millimetres,
                FieldRange.Between(600.0, 1800.0,
                    "1500 mm for standard gauge: the 1435 mm nominal gauge plus about 65 mm across the two rail heads, "
                    + "which is the rail-centre to rail-centre distance the equilibrium-cant equation is written with"),
                "The gauge figure the cant equation uses: rail centre to rail centre, not running edge to running "
                + "edge. It is a separate field from the gauge above so the formula reads as the textbook one, and "
                + "changing the gauge without changing this is how a re-gauged line keeps computing standard-gauge "
                + "cant.");

            Row(f, Rail, "settings.formation.trackCentresMetres", "Track centres", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(3.0, 5.0,
                    "3.0 m for double-track street tramway, 4.0 m conventional mainline, 4.5 m LGV practice"),
                "How far apart the two tracks of a double line are, centre to centre. It widens the formation rather "
                + "than moving the line, and it is the figure that makes a tramway's pair narrower than a mainline's "
                + "and a high-speed pair wider.");
            Row(f, Rail, "settings.formation.sleeperSpacingMetres", "Sleeper spacing", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.5, 0.8,
                    "Standard-gauge practice: sleepers are laid at about 600 to 700 mm centres"),
                "The distance between sleeper centres along the track. It sets how many sleepers a run carries, which "
                + "is both a figure the plan reports and the single largest contributor to the built object count.");
            Row(f, Rail, "settings.formation.sleeperLengthMetres", "Sleeper length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(1.5, 3.0,
                    "Standard gauge: 2.6 m mainline concrete or timber sleeper; tramway and narrow-gauge sleepers are "
                    + "shorter"),
                "How far a sleeper reaches across the track. With the shoulder either side it is what sets the width "
                + "of the ballast crown, so it is a formation width figure as much as a timber size.");
            Row(f, Rail, "settings.formation.sleeperWidthMetres", "Sleeper width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.15, 0.35, "Standard-gauge practice: about 250 to 300 mm across the sleeper"),
                "How wide one sleeper is along the track. Purely how it is drawn -- nothing is judged against it.");
            Row(f, Rail, "settings.formation.sleeperHeightMetres", "Sleeper depth", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.12, 0.3, "Standard-gauge practice: about 150 to 250 mm deep"),
                "How deep one sleeper is. It sits in the ballast crown with its top flush, so this is how far down "
                + "into the ballast the timber reaches rather than how far it stands above it.");
            Row(f, Rail, "settings.formation.ballastShoulderMetres", "Ballast shoulder", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.2, 0.6,
                    "Standard-gauge practice: about 300 to 400 mm of shoulder beyond the sleeper end, widened to about "
                    + "500 mm on high-speed line"),
                "How far the ballast reaches beyond the end of a sleeper before it starts falling away. It is what "
                + "holds the track laterally in the real thing, and here it is the last term in the formation width.");
            Row(f, Rail, "settings.formation.ballastDepthMetres", "Ballast depth", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.15, 0.45,
                    "Standard-gauge practice: about 250 to 300 mm under the sleeper, deepened to about 350 mm on "
                    + "high-speed line"),
                "How deep the ballast is beneath the sleeper. It lifts the whole track above the ground it was matched "
                + "to, so raising it raises the rail head and everything measured from it.");
            Row(f, Rail, "settings.formation.ballastSideSlopeRun", "Ballast side slope", FieldGroup.WhatItLooksLike,
                FieldUnit.Multiplier,
                FieldRange.Between(1.0, 2.5,
                    "Ballast is shaped to about 1 in 1.5, which is near the angle of repose of crushed stone"),
                "How far out the ballast falls for every metre it falls down, so 1.5 is one and a half across for one "
                + "down. Flatter than the stone's angle of repose is a shoulder that would not stand up in the real "
                + "thing.");

            Row(f, Rail, "settings.formation.railHeightMetres", "Rail height", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.1, 0.2,
                    "UIC 60 is 172 mm tall; Ri60 grooved tramway rail is 180 mm"),
                "How tall one rail is from the foot to the running surface. The rail head sits this far above the "
                + "ballast crown, and every height on the line -- the contact wire, the loading gauge, a crossing "
                + "panel -- is measured from the top of it.");
            Row(f, Rail, "settings.formation.railHeadWidthMetres", "Rail head width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.04, 0.09, "UIC 60 head is 72 mm across; Ri60's running head is 56 mm"),
                "How wide the polished running surface of the rail is. It is what a viewer actually sees of the steel "
                + "at any distance, because the web and the foot are in shadow.");
            Row(f, Rail, "settings.formation.railFootWidthMetres", "Rail foot width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.1, 0.2, "UIC 60 foot is 150 mm across; Ri60's is 180 mm"),
                "How wide the base of the rail is where it sits on the sleeper. Together with the head width and the "
                + "height it is the whole rail section the sweep draws.");

            Row(f, Rail, "settings.formation.loadingGaugeHeightMetres", "Loading gauge height",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres,
                FieldRange.Between(3.5, 7.0,
                    "UIC GC is 4.65 m above rail; a container or piggyback envelope reaches about 6.15 m, and a tram "
                    + "envelope is nearer 4.4 m"),
                "How high above the rail head nothing may intrude. It is the envelope the vehicle sweeps, and on an "
                + "unwired line it is what a bridge over the railway has to clear.");
            Row(f, Rail, "settings.formation.contactWireHeightMetres", "Contact wire height",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres,
                FieldRange.Between(4.0, 6.5,
                    "EN 50119: 5.08 m nominal on high-speed line, about 5.3 m on conventional 25 kV line, and about "
                    + "5.5 m for a tramway trolley wire over a public road"),
                "How high the contact wire hangs above the rail head. On a wired line the clearance a structure must "
                + "leave is this plus the electrical clearance below, not the loading gauge -- which is why wiring a "
                + "line can make a bridge that used to clear it too low.");
            Row(f, Rail, "settings.formation.electricalClearanceMetres", "Electrical clearance",
                FieldGroup.WhatItDoesToTheWorld, FieldUnit.Metres,
                FieldRange.Between(0.1, 0.5,
                    "EN 50119 static air clearance: about 270 mm at 25 kV AC, about 150 mm at 750 V DC"),
                "How much air a structure must leave above the contact wire. It is a function of the voltage and not "
                + "of the wire, which is why a 750 V tramway needs a little over half what a 25 kV mainline does.");

            Row(f, Rail, "settings.formation.flangewayWidthMetres", "Flangeway width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.03, 0.11,
                    "Level-crossing practice: 65 to 100 mm of flangeway beside a flat-bottom rail; a grooved tramway "
                    + "rail's own groove is about 42 mm"),
                "How wide the gap left beside each rail through a level crossing. It is what the wheel flange runs in, "
                + "and it is the reason a crossing panel is four ribbons of surface rather than one slab.");
            Row(f, Rail, "settings.formation.flangewayDepthMetres", "Flangeway depth", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.03, 0.08,
                    "Level-crossing practice: about 40 to 50 mm below the rail head; a grooved rail's groove is about "
                    + "38 mm"),
                "How far below the rail head the flangeway is cut. Shallower than the flange and the wheel would ride "
                + "on the road surface instead of the rail.");
            Row(f, Rail, "settings.formation.crossingMarginMetres", "Crossing margin", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.0, 2.0,
                    "AUTHORED: the crossing surface is carried a little past the road edge so a vehicle's wheel is on "
                    + "the panel before it reaches the rail"),
                "How far past each edge of the road the crossing panel is carried. It is multiplied by the skew: a "
                + "crossing at 30 degrees needs twice the panel length of a square one for the same road width, so a "
                + "generous margin on a skewed crossing grows faster than it looks.");

            Row(f, Rail, "settings.formation.maxEmbankmentMetres", "Maximum embankment",
                FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres,
                FieldRange.Between(1.0, 20.0,
                    "AUTHORED: past a certain height the real answer to a valley is a viaduct rather than more fill; "
                    + "street track sits on almost none"),
                "How high the formation may be built up above the ground before the line is REFUSED. It is the price "
                + "of a gentle ruling gradient: the profile cannot break the gradient, so where the ground falls away "
                + "the fill is what grows, and this is the point at which growing fill becomes the wrong answer.");
            Row(f, Rail, "settings.formation.maxCuttingMetres", "Maximum cutting", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres,
                FieldRange.Between(1.0, 20.0,
                    "AUTHORED: past a certain depth the real answer to a hill is a tunnel rather than a deeper "
                    + "cutting"),
                "How deep the formation may be cut into the ground before the line is refused. The mirror of the "
                + "embankment limit, and refused rather than reported for the same reason: a forty-metre cutting is "
                + "not a railway anybody built, it is a tunnel somebody should have.");

            Row(f, Rail, "roadSampleSpacingMetres", "Road sample spacing", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(1.0, 25.0,
                    "AUTHORED: matched to the railway's own default sampling so neither side of a level crossing is "
                    + "the coarse one"),
                "How finely each road's curve is read before it is offered to the level-crossing search. It affects "
                + "where a crossing is found and how precisely a bridged or tunnelled stretch of road is excluded from "
                + "the search, not the railway's own geometry.");

            Row(f, Rail, "settings.ballastMaterial", "Ballast material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the ballast shoulder wears. The tool draws the stone and never makes a material for it, "
                + "so this slot is yours. Left empty the line still builds and still passes its clearances; that much "
                + "of it renders in Unity's missing-material pink.");
            Row(f, Rail, "settings.sleeperMaterial", "Sleeper material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the sleepers wear. A separate slot from the ballast because creosoted timber and precast "
                + "concrete are the same geometry with completely different weathering, and which one a line is laid "
                + "with is the author's answer rather than the tool's.");
            Row(f, Rail, "settings.railMaterial", "Rail material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the steel wears. A rail is polished on the head and rusted down the web, which is the "
                + "one thing a viewer uses to tell a used line from a new one.");
            Row(f, Rail, "settings.crossingMaterial", "Crossing panel material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material a level-crossing panel wears. A fourth slot because a crossing surface is neither "
                + "ballast nor timber nor steel: it is rubber or concrete, laid by the road authority rather than by "
                + "the railway. It is only asked for when the line actually crosses a road.");
            Row(f, Rail, "settings.ohleMastMaterial", "Mast material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the OHLE mast and foundation wear. Left empty the line still builds and renders in Unity's "
                + "missing-material pink.");
            Row(f, Rail, "settings.ohleBracketMaterial", "Bracket material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the bracket and fittings wear. Left empty the line still builds and renders in Unity's "
                + "missing-material pink.");
            Row(f, Rail, "settings.ohleWireMaterial", "Wire material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the catenary, droppers and contact wire wear. A separate slot because wire is polished "
                + "copper or alloy, which catches light completely differently from a steel mast.");
            Row(f, Rail, "settings.ohleMastPrefab", "Mast prefab", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Optional authored mast to replace the generated steel tube. If given, this prefab is placed at each "
                + "mast position, scaled 1:1, oriented with +X facing the track. The wires are still generated.");
            Row(f, Rail, "settings.ohleWirePrefab", "Wire prefab", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Optional authored catenary wire prefab. Rare, but offered so authors can override the generated wire.");
        }

        /// <summary> B-27 / D-14's rows: a cross-country transmission run, placed as a polyline the author drags. It is
        /// the third hand-placed child and the second overhead LINE, so its electrical figures read like the pole
        /// line's raised an order of magnitude -- a span in the hundreds of metres, a clearance of several metres, a
        /// catenary the sag speaks for -- while its steel is a lattice tower's catalogue rather than a timber pole's.
        ///
        /// The route has NO row, for the same reason the rail's staked centreline has none: it is a list of world
        /// corners the author drags in the Scene view, not a number typed into a foldout, and a row for it would be a
        /// row drawing an array of vectors under a heading that reads "what it looks like".
        ///
        /// The split between the two nested classes is where the identity lives. The electrical answers -- the span,
        /// the sag, the statutory clearance, the angle-tower threshold -- are what the line IS, the engineer's figures,
        /// so they sit under that heading the way the rail's radius and gradient do. The steel dimensions are what one
        /// LOOKS like, the buyer's shape, so they sit with the materials that dress it. </summary>
        private static void SeedTransmissionRun(List<FieldDescriptor> f)
        {
            const string Run = "TransmissionLineRun";

            Row(f, Run, "settings.enabled", "Carries a line", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "Whether this run strings a line at all. Off leaves the component in place with its route and its "
                + "answers intact and plants nothing, the same way a pole run is switched out without losing how it "
                + "was set.");

            // --- what the line IS: the electrical engineer's figures ---
            Row(f, Run, "settings.electrical.conductorName", "Conductor", FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.None(),
                "Which conductor the line is strung with, by name. It is a label the plan carries rather than a figure "
                + "it computes from -- the sag below is the honest handle on the cable shape -- so it documents the run "
                + "for whoever reads it next.");
            Row(f, Run, "settings.electrical.spanLengthMetres", "Span length", FieldGroup.WhatItIs, FieldUnit.Metres,
                FieldRange.Between(80.0, 600.0,
                    "sub-transmission lattice spans run 200-400 m typically; 80 m is a tight angle span on a corner "
                    + "and 600 m is a long river or valley crossing before it wants a special anchor tower"),
                "The nominal distance between towers. Each straight leg is divided into a whole number of equal spans "
                + "of about this length, so it is what decides how many towers a leg carries and how far the cable "
                + "sags between them.");
            Row(f, Run, "settings.electrical.sagRatio", "Sag ratio", FieldGroup.WhatItIs, FieldUnit.Fraction,
                FieldRange.Between(0.01, 0.08,
                    "published tabulated sag is 2-5 % of span at everyday temperature; below 1 % is a tension no "
                    + "conductor is strung to and above 8 % is a hot-day or ice-load extreme, not a still-air figure"),
                "How deeply the cable hangs, as a fraction of the span. It is the one honest authored handle on the "
                + "curve: the catenary constant is tension over weight, which no author knows, but sag as a share of "
                + "span is tabulated per conductor. The earth wire is a separate mechanical member and has its own "
                + "ratio below.");
            Row(f, Run, "settings.electrical.shieldWireSagRatio", "Earth-wire sag ratio",
                FieldGroup.WhatItIs, FieldUnit.Fraction,
                FieldRange.Between(0.01, 0.05,
                    "earth-wire sag is a separate authored cable figure; 1-4 % is the common still-air range for a "
                    + "shield wire, and the shipped 2 % default sits inside it"),
                "How deeply the earth wire hangs, as a fraction of its span. It is not copied from the phase "
                + "conductor: the shield wire has its own mass and stringing tension, so its catenary is solved from "
                + "this ratio.");
            Row(f, Run, "settings.electrical.minGroundClearanceMetres", "Ground clearance", FieldGroup.WhatItIs,
                FieldUnit.Metres,
                FieldRange.Between(5.5, 12.0,
                    "statutory HV clearance to open ground is jurisdictional and voltage-dependent, roughly 6-10 m "
                    + "for sub-transmission; the default is 7.5 m"),
                "The least air the lowest cable may leave above the ground beneath it. A span whose sag brings it "
                + "closer than this is REFUSED, not flattened for -- the remedy is a taller tower, a shorter span or a "
                + "reroute, and the refusal names which span and by how much.");
            Row(f, Run, "settings.electrical.angleTowerThresholdDeg", "Angle-tower threshold", FieldGroup.WhatItIs,
                FieldUnit.Degrees,
                FieldRange.Between(1.0, 30.0,
                    "AUTHORED: below a degree every slight bend would call for a braced angle tower; beyond about 30 "
                    + "degrees even a heavy suspension tower cannot hold the sideways pull, so the choice is moot"),
                "How sharply the route must turn at a corner before that corner takes a braced ANGLE tower rather than "
                + "a suspension tower. The two ends of the run are always strain towers whatever this says, because a "
                + "dead-end carries the conductors' full one-sided pull.");
            Row(f, Run, "settings.phaseCount", "Phases", FieldGroup.WhatItIs, FieldUnit.None,
                FieldRange.Between(1.0, 3.0,
                    "a single-circuit horizontal tower carries the three phases of one circuit across its one arm; a "
                    + "count of one or two is a partial or split build of that same arm"),
                "How many phase conductors the arm carries. They are centred on the tower at the phase spacing below, "
                + "so three sit at the centre and either end of the arm, and each hangs from its own insulator string.");

            // --- what it LOOKS like: the lattice steel and the materials that dress it ---
            Row(f, Run, "settings.tower.baseWidthMetres", "Base width", FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(3.0, 12.0,
                    "a lattice tower's base is roughly a sixth to an eighth of its height; 3-12 m spans a short "
                    + "sub-transmission tower to a tall heavy-angle one"),
                "The square spread of the four legs at ground. It is the footprint the four-foot seating reads: the "
                + "run samples the ground under each corner of this square and seats the tower base on the lowest.");
            Row(f, Run, "settings.tower.conductorCrossArmHeightMetres", "Cross-arm height",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(10.0, 45.0,
                    "sub-transmission cross-arms sit about 15-30 m above the base; 10-45 m covers a short tower to a "
                    + "tall crossing tower"),
                "How high the cross-arm centre stands above the tower base. The lowest phase hangs an insulator-length "
                + "below it on a suspension tower, and the statutory clearance is measured from there down through the "
                + "sag, so this is the single figure that lifts or lowers the whole line.");
            Row(f, Run, "settings.tower.crossArmHalfLengthMetres", "Cross-arm half-length",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(2.0, 10.0,
                    "half an arm holding three phases at 4 m spacing with an end margin is about 4.6 m; 2-10 m spans a "
                    + "narrow to a wide horizontal arm"),
                "Half the cross-arm, measured from the tower centre to one tip. It has to be long enough to hold the "
                + "phases at their spacing with a margin; an arm too short for its phases crowds them to the tip, "
                + "which the inspector reports rather than hides.");
            Row(f, Run, "settings.tower.phaseSpacingMetres", "Phase spacing", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(2.0, 8.0,
                    "horizontal phase-to-phase spacing is about 3.5-4.5 m at 132 kV to hold the air gap under "
                    + "conductor swing; it widens with voltage"),
                "How far apart the phases sit along the arm. It is set by the voltage and the swing of the cable in "
                + "wind, not by the tower, which is why a higher-voltage line on the same steel needs a longer arm.");
            Row(f, Run, "settings.tower.shieldWirePeakAboveCrossArmMetres", "Earth-wire peak",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(1.0, 8.0,
                    "the peak has to stand high enough that the line down to the outer phase keeps it inside the "
                    + "roughly 30-degree protective angle; 3.6 m over a 9 m arm does that"),
                "How far the earth-wire peak stands above the phases. The earth wire rides here, above everything, so "
                + "that its cone of protection covers the phases from a lightning strike -- which is the whole reason "
                + "it is up there and not on the arm.");
            Row(f, Run, "settings.tower.suspensionInsulatorLengthMetres", "Suspension insulator",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(0.8, 4.0,
                    "an I-string of about 9-10 cap-and-pin discs at 146 mm plus end hardware is about 1.5-1.7 m; it "
                    + "lengthens with voltage"),
                "The vertical string a suspension tower hangs its phase from. On a suspension tower the phase sits "
                + "this far BELOW the arm, so it is the figure that sets where the lowest cable actually starts, and "
                + "the clearance is measured from there.");
            Row(f, Run, "settings.tower.strainInsulatorLengthMetres", "Strain insulator",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(1.0, 6.0,
                    "a tension string is longer than a suspension one but lies along the pull; about 2-3 m at "
                    + "sub-transmission voltages"),
                "The string an angle or terminal tower takes the pull on. It lies roughly HORIZONTAL along the "
                + "conductor rather than hanging down, so it does not lower the phase -- an angle tower attaches its "
                + "phase at the arm. Held as its own figure so the mesh draws the right string, not to move the "
                + "attachment.");
            Row(f, Run, "settings.tower.legFaceMetres", "Leg face", FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(0.05, 0.5,
                    "AUTHORED: the corner leg is the heaviest angle section on the tower, a few tens of millimetres "
                    + "to about half a metre across the face on a large tower"),
                "How wide the four corner legs are drawn. It changes the tower's heft, not any attachment height, but "
                + "a leg drawn too thin reads as a pylon of wire and one too thick as a solid mast.");
            Row(f, Run, "settings.tower.braceFaceMetres", "Brace face", FieldGroup.WhatItLooksLike, FieldUnit.Metres,
                FieldRange.Between(0.03, 0.3,
                    "AUTHORED: the bracing is lighter than the legs it ties, typically half to two-thirds their face"),
                "How wide the diagonal bracing and the cross-arm web members are drawn. Lighter than the legs, because "
                + "the bracing carries the shear and the legs carry the load, and a tower drawn with equal members "
                + "reads as a solid frame rather than a truss.");
            Row(f, Run, "settings.tower.conductorDiameterMetres", "Conductor diameter", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.005, 0.05,
                    "a medium ACSR such as Lynx is about 19.5 mm; 5-50 mm spans a light conductor to a large "
                    + "high-voltage or bundled one"),
                "How thick the phase conductors are drawn. A bare ACSR conductor is dull grey and almost unlit, so the "
                + "diameter is most of what a viewer sees of it against the sky.");
            Row(f, Run, "settings.tower.shieldWireDiameterMetres", "Earth-wire diameter", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres,
                FieldRange.Between(0.003, 0.03,
                    "a 7-strand steel earth wire is about 9.8 mm; an OPGW with fibre inside is a little larger"),
                "How thick the earth wire is drawn. Thinner than the phases it protects, which is one of the cues that "
                + "reads the topmost wire as the shield wire rather than a fourth phase.");
            Row(f, Run, "settings.tower.wireSides", "Wire sides", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.Between(3.0, 16.0,
                    "AUTHORED mesh tessellation: three is the leanest tube that still reads as round in silhouette, "
                    + "sixteen is smoother than a cable is ever seen at line distances"),
                "How many sides each wire's tube is built with. It is a pure cost-versus-smoothness dial on the mesh: "
                + "a cable seen at a hundred metres needs very few, and every extra side multiplies across every span "
                + "of a long line.");
            Row(f, Run, "settings.tower.shieldWireCount", "Earth wires", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.Between(1.0, 2.0,
                    "one earth wire is the ordinary single-circuit case; two are used where a wider protective angle "
                    + "or an OPGW-plus-earth pair is wanted"),
                "How many earth wires ride the top. One sits on a central peak; two spread out over the outer phases, "
                + "which is where a pair sits to keep the outer phases inside the protective angle. The peaks are "
                + "drawn to match, so a wire always meets the steel it is strung to.");

            Row(f, Run, "settings.steelMaterial", "Steel material", FieldGroup.WhatItLooksLike, FieldUnit.None,
                FieldRange.None(),
                "The material the galvanised lattice wears -- legs, bracing, arm and peak. The tool draws the steel "
                + "and never makes a material for it, so this slot is yours. Left empty the run still builds and "
                + "passes its clearances; the steel renders in Unity's missing-material pink.");
            Row(f, Run, "settings.insulatorMaterial", "Insulator material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material the insulator strings wear -- ceramic or glass, never the steel's grey. It is a second "
                + "slot rather than one shared with the steel for the same reason the pole splits its hardware from "
                + "its timber: an insulator is the tower's one non-steel part and a galvanised insulator reads as "
                + "wrong at any distance.");
            Row(f, Run, "settings.conductorMaterial", "Conductor material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material the phase conductors wear. A bare conductor is dull grey and almost unlit; a material "
                + "with a strong specular highlight will read as a row of bright white lines from any distance.");
            Row(f, Run, "settings.shieldWireMaterial", "Earth-wire material", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.None(),
                "The material the earth wire wears. A fourth slot because it is often a different steel from the "
                + "phases -- and an OPGW is glass and aluminium, not the conductor's dull grey -- so which one the "
                + "topmost wire is strung with is the author's answer rather than the tool's.");
        }

        /// <summary> A settlement-order prefix as it reads at the head of a label -- "capital" becomes "Capital". The
        /// prefixes are the catalogue's parameter names, which are deliberately lower case; a label is not. </summary>
        private static string Title(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) { return string.Empty; }
            return char.ToUpperInvariant(prefix[0]) + prefix.Substring(1);
        }
    }
}
#endif

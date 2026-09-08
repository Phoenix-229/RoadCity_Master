// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's field table: the constrained, value-bearing inspector fields on the road tool's runtime
    /// components, each mapped to its foldout, unit, range-with-source, and the sentence an author reads on hover. This
    /// is the machine-readable form of the FieldCensus's unit column -- the drawers read it to place a field, label its
    /// unit, describe it, and validate it inline; the gate reads it to prove those things hold. Only fields that
    /// ACTUALLY EXIST on the components are listed (verified against Road.cs and RoadTerrain.cs); every numeric limit
    /// is a real-world value or an AUTHORED default (C-15), never a guess.
    ///
    /// B-5 added the hover column (fault 8) and brought desiredRampHeight in here (fault 4): a real cross-section
    /// dimension drawn as a one-off field in the old flat layout carried no unit, no band and no explanation while
    /// every field beside it carried all three. The exhaustive per-field foldout wiring of all 103/110 fields is the
    /// deferred scene half; this seeds the load-bearing constrained fields inline validation applies to. </summary>
    public static class InspectorFieldRegistry
    {
        /// <summary> The display name -- headers, window titles, menu paths. The identifier RoadCityMaster is separate
        /// (folders/asmdef/namespaces cannot carry a space or ampersand). No script may carry the old third-party name
        /// at all (C-05); this const is the only product string the model exposes. </summary>
        public const string ProductDisplayName = "Road & City Master";

        private static List<FieldDescriptor> _all;

        /// <summary> Every seeded descriptor, built once. </summary>
        public static IList<FieldDescriptor> All()
        {
            if (_all != null) { return _all; }
            List<FieldDescriptor> f = new List<FieldDescriptor>();

            // --- Road: what it IS (identity / cross-section counts) ---
            // RM-94: the band and its citation are NOT typed here. RoadFieldLimits (runtime) owns them, because the
            // clamp in Road.UpdateRoad has to run in the runtime assembly and cannot read this editor-only registry.
            // Typing them twice is what let the two disagree in the first place.
            f.Add(new FieldDescriptor("Road", "laneWidth", "Lane width", FieldGroup.WhatItIs,
                FieldUnit.Metres, FieldRange.Between(
                    RoadFieldLimits.LaneWidthMinMetres, RoadFieldLimits.LaneWidthMaxMetres,
                    RoadFieldLimits.LaneWidthSource),
                "Width of one running lane. Every lane in both directions is built to this width, so the travelled "
                + "way is this times the lane count."));
            // The lane count is authored PER DIRECTION, not as a total: a road with an odd number of lanes
            // (a climbing or passing lane) cannot be described by one number, and neither can a one-way.
            // The inspector still OFFERS a total in its quick-pick buttons because that is how a road is
            // described out loud; these two are the fields the total resolves into.
            f.Add(new FieldDescriptor("Road", "lanesForward", "Lanes forward", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.AtLeast(1, "AUTHORED: a carriageway has at least one lane"),
                "Running lanes in the road's own direction of travel. A climbing or passing lane is counted here, "
                + "which is why the two directions are authored separately."));
            f.Add(new FieldDescriptor("Road", "lanesBackward", "Lanes backward", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.AtLeast(-1,
                "AUTHORED: 0 on a one-way; -1 means unsplit legacy data the model halves on load"),
                "Running lanes against the road's direction of travel. Zero on a one-way; on a road saved before the "
                + "counts were split per direction it reads -1 until the model halves the old total on load."));
            // Whether 0 backward lanes is right or wrong depends on this flag, which is why that limit is a
            // SectionRules cross-field rule and not part of the range above.
            f.Add(new FieldDescriptor("Road", "isOneWay", "One-way", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, 1,
                "AUTHORED: a carriageway either carries both directions or one; there is no third state"),
                "The carriageway carries traffic in the road's direction only. It is what makes a backward lane "
                + "count of zero correct rather than a fault."));

            // The design class. The bound is DERIVED from the enum rather than typed, so adding a class cannot leave
            // a stale ceiling behind that would flag the new class as corrupt data. Validation of a dropdown is not
            // idle: a serialised enum whose member was later removed comes back as an out-of-range integer.
            f.Add(new FieldDescriptor("Road", "roadClass", "Road class", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, System.Enum.GetValues(typeof(Road.RoadClassEnum)).Length - 1,
                "AUTHORED: one of the declared design classes; each carries its own lane, width, speed and grade "
                + "defaults with their sources in RoadClassDefaults"),
                "The design class this road is built to. Applying a class writes its lane count, lane and shoulder "
                + "widths, median, design speed and grade ceiling together, so the section stays internally "
                + "consistent."));
            // appliedRoadClass is deliberately absent: it is [HideInInspector] bookkeeping that records which
            // class's defaults were last written, and an author editing it would silently change what a later
            // class change is allowed to refresh.

            // B-15 / C-13. The road's place in the SETTLEMENT network, which is not its cross-section and not the
            // functional class the city generator gives to streets it makes itself. It sits here, immediately under
            // roadClass, because an author classifying a road they have just drawn is answering both questions in the
            // same breath -- and because putting the two side by side is what makes it obvious they are different
            // questions. The bound is DERIVED from the enum for the same reason as roadClass above: a serialised enum
            // whose member was later removed comes back as an out-of-range integer.
            f.Add(new FieldDescriptor("Road", "settlementRole", "Settlement role", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0,
                System.Enum.GetValues(typeof(SettlementRoleEnum)).Length - 1,
                "AUTHORED: none, main highway, intercities, intercities connecting towns, or intercities connecting "
                + "villages -- the orders of place a hand-drawn network connects"),
                "What this road is in the network BETWEEN settlements, which decides what order of place may be "
                + "proposed where it ends: a main highway carries the capital and the big cities, a village connector "
                + "reaches a village. It is not the cross-section (that is the road class above) and it is not the "
                + "arterial/collector/local class the generator gives to streets inside a settlement. Left at None a "
                + "road takes no part in settlement placement and is built exactly as before."));

            // The median, as the two fields it really is: what separates the directions, and how wide that is.
            // A painted marker separates without occupying width, so the type decides whether the width counts --
            // MedianWidthEffective is where that arithmetic lives.
            f.Add(new FieldDescriptor("Road", "centerDivider", "Median type", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, System.Enum.GetValues(typeof(CenterDividerTypeEnum)).Length - 1,
                "AUTHORED: none, concrete barrier, barrier with glare screens, wire rope, or painted markers"),
                "What physically separates the two directions. Painted markers separate without occupying any width, "
                + "so this is also what decides whether the median width below counts in the cross-section."));
            f.Add(new FieldDescriptor("Road", "medianWidth", "Median width", FieldGroup.WhatItIs,
                FieldUnit.Metres, FieldRange.Between(0.0, 30.0,
                "AUTHORED ceiling: 0 is no physical median and rural depressed medians reach ~30 m. What a median of "
                + "a given width can actually HOLD is not decided here -- MedianContent's cited band table owns that, "
                + "from painted flush below 1.2 m up to trough-and-planting at 15 m"),
                "Clear distance between the inner edges of the two carriageways. It buys the room the median type "
                + "needs to stand in, and widens the whole road by that much."));

            // The design speed. Every taper length in the tool is computed from this figure, so it is not decoration:
            // a wrong speed here produces a lane that opens over the wrong distance. The flat bounds below are the
            // widest real posted limits; whether the value suits THIS road's class is a SectionRules cross-field rule.
            f.Add(new FieldDescriptor("Road", "speedLimitKPH", "Design speed", FieldGroup.WhatItIs,
                FieldUnit.KmPerHour, FieldRange.Between(5.0, 140.0,
                "AUTHORED bounds spanning real posted limits, from ~5 km/h shared space to the 140 km/h of the "
                + "fastest posted motorways; 0 or less falls back to the road class default"),
                "The speed the geometry is dimensioned for, not a signpost. Lane tapers, superelevation runoff and "
                + "sight distance are all computed from it, so changing it changes how far ahead the road opens."));

            // --- Road: what it LOOKS like (furniture / shoulders) ---
            // RM-110, B-10. The shoulder is a TWO-TIER figure, and the tooltip says so, because the defect the author
            // reported was a value between the tiers: an edge strip is 0.6-1.2 m and a stopped-vehicle refuge is 3.0 m,
            // and the widths in between are neither. The per-class numbers live in City.RoadClassDefaults, which cites
            // both tiers; the 0-4 m here is only the authoring bound, kept wider than the table so a verge or a
            // widened refuge stays reachable -- the class-change report is what tells an author when they have left
            // the cited band, rather than a clamp refusing the number.
            f.Add(new FieldDescriptor("Road", "shoulderWidth", "Shoulder width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, FieldRange.Between(0.0, 4.0,
                "AUTHORED authoring bound; the cited tiers are 0.6–1.2 m edge strip and 3.0 m refuge (AASHTO), "
                + "per class in City.RoadClassDefaults; widened here for verges"),
                "Width of the strip outside the running lanes, on each side. It is where a stopped vehicle stands and "
                + "where the edge line is painted, and it widens the road beyond the travelled way. Below about 3 m a "
                + "stopped vehicle still overhangs the lane, and above about half the lane width the strip gets driven "
                + "as a lane — so pick a narrow strip or a full refuge, not a width between them."));
            // RM-110, B-9. The shoulder's cross-fall has shaped the mesh since RM-94 and no author could reach it: the
            // only figure arriving at the geometry was the constant normal. The band cited here is not typed in, it is
            // read from the same constants the clamp uses, so a number the inspector says is allowed is a number the
            // geometry will accept -- the split RM-94 found between a declared range and a different clamp cannot
            // reopen while both come from one place.
            f.Add(new FieldDescriptor("Road", "shoulderCrossSlope", "Shoulder cross-fall", FieldGroup.WhatItLooksLike,
                FieldUnit.Fraction, FieldRange.Between(
                CrossSection.CarriagewaySection.ShoulderCrossSlopeMinimum,
                CrossSection.CarriagewaySection.ShoulderCrossSlopeMaximum,
                CrossSection.CarriagewaySection.ShoulderCrossSlopeSource),
                "How steeply the shoulder falls away from the carriageway edge, as a fraction rather than a percentage: "
                + "0.04 is a 4 % fall, the normal figure for a paved bituminous shoulder. Steepen it to drain a shoulder "
                + "faster, flatten it to meet an existing verge; either way it is clamped into the band above, and 0 "
                + "means the normal figure rather than a flat shoulder."));
            // B-5, fault 4. desiredRampHeight was drawn straight into the old flat layout as a bare FloatField
            // captioned "Ramp Height:" -- no unit, no band, no explanation of what it shapes. It is a real
            // cross-section dimension, so it belongs here with the rest of them rather than in a one-off draw.
            // The band is the one the project's own field census already recorded for it; it is not invented here.
            f.Add(new FieldDescriptor("Road", "desiredRampHeight", "Ramp height", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, FieldRange.Between(0.0, 2.0,
                "AUTHORED: 0–2 m ramp and kerb rise, the band recorded for this field in the field census; 0 leaves a "
                + "vertical edge and the tool's own default is 0.35 m"),
                "Vertical drop from the outer shoulder edge down to the outer edge of the verge ramp. It is what gives "
                + "the road edge a graded fall rather than a vertical step into the surrounding ground."));

            // --- Road: what it DOES to the world (terrain interaction / grade) ---
            f.Add(new FieldDescriptor("Road", "maxGrade", "Max grade", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Fraction, FieldRange.Between(0.0, 0.15,
                "AASHTO max grade 6–12% by terrain and design speed; 15% hard ceiling"),
                "Steepest rise or fall the centreline is allowed to take, as a fraction rather than a percentage: 0.08 "
                + "is an 8 % grade. It only binds while grade enforcement is switched on."));
            f.Add(new FieldDescriptor("Road", "matchTerrainSubtraction", "Terrain match drop", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(0.0, 2.0,
                "AUTHORED: vertical offset the carriageway sinks into matched terrain"),
                "How far below the road surface the matched terrain is set down. It keeps the ground from breaking "
                + "through the carriageway where the two meet; terrain is never lifted above the road."));

            // B-12, registered in B-14. The verge, and the field that turned City.TerrainFootprint from live gated
            // maths with no caller into part of the build. It is a nested path -- FieldDescriptor takes what
            // SerializedObject.FindProperty takes, and "terrainFootprint.shoulderMetres" resolves because
            // TerrainFootprintClass is [Serializable] -- so it needs no RoadEditor change: LegacyInspectorShell
            // .AllGroups draws every registered row this group owns.
            //
            // It is NOT Road.shoulderWidth, and the label says verge rather than shoulder for that reason. That one is
            // a mesh the road emits and a vehicle can stop on; this one is unbuilt ground held flat past the edge of
            // everything the road emits, before the batter starts falling away. Both exist on a real road and they are
            // different widths.
            f.Add(new FieldDescriptor("Road", "terrainFootprint.shoulderMetres", "Flat verge beyond section",
                FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(0.0, 3.0,
                "AUTHORED: the graded shoulder is wider than the usable one, and AASHTO's Roadside Design Guide "
                + "rounds the break of slope rather than cutting it square, so there is always some flat ground "
                + "beyond the surfaced edge; 0.6 m is ordinary practice for the berm before an embankment slope and "
                + "the tool's own default is 0.5 m"),
                "Ground held flat beyond the outer edge of the built cross-section, on each side, before the side "
                + "slope breaks. This is unbuilt verge, not the driveable shoulder above: the road's own shoulder "
                + "mesh is already inside the section this is measured from. Widen it where a road needs a graded "
                + "margin -- a guard rail, a drainage berm, a footway to come; 0 breaks the slope at the very edge of "
                + "the formation, which nothing is built like. It is taken out of Match heights distance rather than "
                + "added past it, so widening the verge shortens the side slope's reach instead of disturbing more "
                + "ground."));

            // B-12. The two controls that decide how much ground the road disturbs -- and, before they existed, the
            // reason it disturbed the wrong amount. The deformation used to reach a FIXED distance and be written
            // flat at road level, so a 0.2 m fill and a 3 m fill claimed the same ground and both ended in a step:
            // the bulge the author reported. The reach is now a consequence of these two, and the band cited for the
            // gradient is the one City.CutSlopeClass already works in -- run per rise, the same units, so the
            // inspector and the march cannot drift apart the way RM-94 found a declared range drifting from a clamp.
            f.Add(new FieldDescriptor("Road", "batterRunPerRise", "Batter gradient", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Multiplier, FieldRange.Between(1.0, 6.0,
                "AUTHORED bounds, from a 1:1 rock cut to a 6:1 fully recoverable embankment; AASHTO's Roadside "
                + "Design Guide is what makes the flat end matter -- 4:1 and flatter is recoverable, 3:1 traversable "
                + "but not recoverable, steeper than 3:1 critical. 1.5:1 is ordinary stable-soil practice and is the "
                + "figure City.CutSlopeClass carries"),
                "Horizontal metres of side slope for every vertical metre between the carriageway edge and the "
                + "natural ground, so 1.5 is a 1.5:1 batter and 4 is a gentle 4:1 one. It is what decides HOW WIDE "
                + "the disturbed ground is: the slope runs out until it meets the ground, so a flatter gradient "
                + "reaches further and a steeper one barely leaves the shoulder. Flatten it towards 4:1 where a "
                + "vehicle leaving the road has to be able to recover."));
            f.Add(new FieldDescriptor("Road", "batterMaxReachMetres", "Batter max reach", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(0.0, 60.0,
                "AUTHORED ceiling each side, the same 60 m bound the splat paint half-width carries; it is capped "
                + "again at run time by half of Match heights distance, which is the figure that sizes the write "
                + "buffers"),
                "The furthest the side slope is allowed to travel from the carriageway edge, on each side. It is a "
                + "stop, not a target: the reach is normally whatever the gradient needs to meet the ground, and "
                + "this only binds where a road sits high above a valley -- rather than reshape the hillside the "
                + "slope stops here and the station is counted as handed back, which in real practice is where you "
                + "would bench, retain or re-align instead."));

            // --- Road: what is GENERATED from it (build quality) ---
            f.Add(new FieldDescriptor("Road", "roadDefinition", "Segment resolution", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Metres, FieldRange.Between(
                    RoadFieldLimits.SegmentResolutionMinMetres, RoadFieldLimits.SegmentResolutionMaxMetres,
                    RoadFieldLimits.SegmentResolutionSource),
                "Spacing between the cross-sections the road mesh is built from. A smaller figure follows a curve more "
                + "closely and costs triangles; a larger one visibly facets tight bends."));

            // --- RoadTerrain: what it DOES to the world (splat paint) ---
            f.Add(new FieldDescriptor("RoadTerrain", "splatWidth", "Splat paint half-width", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(1.0, 60.0,
                "AUTHORED: paint distance from carriageway; RM-38 flags the 30 m default as over-wide"),
                "How far out from the carriageway edge the road texture is painted onto the terrain, on each side. "
                + "Wider paint covers more ground per road and takes longer to write."));

            // --- RoadTerrain: what it LOOKS like (splat resolution) ---
            f.Add(new FieldDescriptor("RoadTerrain", "splatResoWidth", "Splat resolution W", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.Between(512, 4096,
                "AUTHORED: power-of-two splat map resolution"),
                "Pixels across the terrain's splat map. Higher resolves the painted road edge more crisply and costs "
                + "terrain memory; keep it a power of two."));
            f.Add(new FieldDescriptor("RoadTerrain", "splatResoHeight", "Splat resolution H", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.Between(512, 4096,
                "AUTHORED: power-of-two splat map resolution"),
                "Pixels down the terrain's splat map. Higher resolves the painted road edge more crisply and costs "
                + "terrain memory; keep it a power of two."));

            // --- RoadIntersection: what it IS (B-16's junction form) ---
            f.Add(new FieldDescriptor("RoadIntersection", "junctionType", "Junction form", FieldGroup.WhatItIs,
                FieldUnit.Index, FieldRange.Between(0,
                System.Enum.GetValues(typeof(City.JunctionTypeEnum)).Length - 1,
                "DERIVED: JunctionTypeEnum member count"),
                "What shape this junction takes: priority and signalised meet on one surface and differ only in who "
                + "waits, a roundabout circulates around an island instead, and an interchange carries one road over "
                + "the other. The form decides which stop controls are even available."));

            // --- RoadIntersection: what it LOOKS like (the roundabout's own dimensions) ---
            f.Add(new FieldDescriptor("RoadIntersection", "roundaboutInscribedDiameterMetres",
                "Inscribed circle diameter", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, FieldRange.Between(13.0, 80.0,
                "NCHRP 672: mini-roundabout 13-27 m, single-lane 27-46 m; upper bound AUTHORED for multilane"),
                "The outer circle the legs trim to. Too small and the circulating centreline turns tighter than a "
                + "design vehicle can track, which is refused rather than drawn."));
            f.Add(new FieldDescriptor("RoadIntersection", "roundaboutEntryWidthMetres", "Entry width",
                FieldGroup.WhatItLooksLike, FieldUnit.Metres, FieldRange.Between(4.0, 5.0,
                "NCHRP 672 single-lane entry width range"),
                "Width of one approach lane at the yield line. The circulatory carriageway is sized from it, so it "
                + "sets the ring width and therefore how much island is left in the middle."));
            f.Add(new FieldDescriptor("RoadIntersection", "roundaboutSplitterIslandLengthMetres",
                "Splitter island length", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, FieldRange.AtLeast(15.0, "NCHRP 672: at least 15 m, desirably about 30 m"),
                "Length of the raised island between the entry and exit lanes on the shortest leg. It channels the "
                + "entry and gives someone crossing somewhere to stand halfway."));

            // --- RoadIntersection: what it DOES to the world (headroom the flyover demands) ---
            f.Add(new FieldDescriptor("RoadIntersection", "interchangeVerticalClearanceMetres",
                "Vertical clearance", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(4.9, 8.0,
                "AUTHORED to AASHTO highway-over-highway practice (~5.0 m plus resurfacing allowance); rail and "
                + "electrified rail are greater"),
                "Headroom the raised road must leave over the road beneath it. This is what sets how far it climbs, "
                + "and so how much approach length it needs on each side before the tool will build it."));

            // --- RoadIntersection: what is GENERATED from it (ramps and the deck span) ---
            f.Add(new FieldDescriptor("RoadIntersection", "rampAlignment", "Ramp alignment",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Index, FieldRange.Between(0,
                System.Enum.GetValues(typeof(City.RampAlignmentKind)).Length - 1,
                "DERIVED: RampAlignmentKind member count"),
                "How the connecting ramp is laid out: direct off the mainline, semi-direct crossing and returning, or "
                + "a full loop. The choice sets how far the ramp turns and so how long it has to be."));
            f.Add(new FieldDescriptor("RoadIntersection", "rampRadiusMetres", "Ramp radius",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Metres, FieldRange.AtLeast(
                City.InterchangeRamp.MinimumRadiusMetres(City.InterchangeRampClass.Default(),
                    City.SuperelevationClass.Default()),
                "DERIVED: InterchangeRamp.MinimumRadiusMetres, which is the larger of the class's 50 m house minimum "
                + "and the radius the ramp design speed physically allows -- 89.5 m at 50 km/h, so the physics governs "
                + "and quoting the house 50 m here would advertise a floor the resolver refuses"),
                "Plan radius asked of the ramp curve. Below the radius its design speed allows the ramp is refused, "
                + "because a curve that cannot be driven at the speed it is signed for is not a ramp."));
            f.Add(new FieldDescriptor("RoadIntersection", "flyoverSpanLengthMetres", "Deck span",
                FieldGroup.WhatIsGeneratedFromIt, FieldUnit.Metres, FieldRange.AtLeast(7.0,
                "AUTHORED: at least the narrowest crossing carriageway (two 3.5 m lanes) or the deck cannot span it"),
                "How long a stretch of the raised road is marked as bridge. The deck itself is your bridge prefab "
                + "loaded onto that span's nodes, not a generated mesh."));

            // --- The city half. C-10's rows live beside the city code they describe (City/Editor compiles into this
            // same assembly), so the table stays one table without this file growing a second tool inside it.
            CityFieldRows.Seed(f);

            _all = f;
            return _all;
        }

        /// <summary> All descriptors for one component, in seeded order (the drawer walks these per foldout). </summary>
        public static IList<FieldDescriptor> ForComponent(string component)
        {
            List<FieldDescriptor> outList = new List<FieldDescriptor>();
            foreach (FieldDescriptor d in All()) { if (d.component == component) { outList.Add(d); } }
            return outList;
        }
    }
}
#endif

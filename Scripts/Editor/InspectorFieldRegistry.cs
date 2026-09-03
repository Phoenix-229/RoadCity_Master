#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's field table: the constrained, value-bearing inspector fields on the road tool's runtime
    /// components, each mapped to its foldout, unit, and range-with-source. This is the machine-readable form of the
    /// FieldCensus's unit column -- the drawers read it to place a field, label its unit, and validate it inline; the
    /// gate reads it to prove those three things hold. Only fields that ACTUALLY EXIST on the components are listed
    /// (verified against Road.cs and RoadTerrain.cs); every numeric limit is a real-world value or an AUTHORED default
    /// (C-15), never a guess. The exhaustive per-field foldout wiring of all 103/110 fields is the deferred scene half;
    /// this seeds the load-bearing constrained fields inline validation applies to. </summary>
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
                    RoadFieldLimits.LaneWidthSource)));
            // The lane count is authored PER DIRECTION, not as a total: a road with an odd number of lanes
            // (a climbing or passing lane) cannot be described by one number, and neither can a one-way.
            // The inspector still OFFERS a total in its quick-pick buttons because that is how a road is
            // described out loud; these two are the fields the total resolves into.
            f.Add(new FieldDescriptor("Road", "lanesForward", "Lanes forward", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.AtLeast(1, "AUTHORED: a carriageway has at least one lane")));
            f.Add(new FieldDescriptor("Road", "lanesBackward", "Lanes backward", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.AtLeast(-1,
                "AUTHORED: 0 on a one-way; -1 means unsplit legacy data the model halves on load")));
            // Whether 0 backward lanes is right or wrong depends on this flag, which is why that limit is a
            // SectionRules cross-field rule and not part of the range above.
            f.Add(new FieldDescriptor("Road", "isOneWay", "One-way", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, 1,
                "AUTHORED: a carriageway either carries both directions or one; there is no third state")));

            // The design class. The bound is DERIVED from the enum rather than typed, so adding a class cannot leave
            // a stale ceiling behind that would flag the new class as corrupt data. Validation of a dropdown is not
            // idle: a serialised enum whose member was later removed comes back as an out-of-range integer.
            f.Add(new FieldDescriptor("Road", "roadClass", "Road class", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, System.Enum.GetValues(typeof(Road.RoadClassEnum)).Length - 1,
                "AUTHORED: one of the declared design classes; each carries its own lane, width, speed and grade "
                + "defaults with their sources in RoadClassDefaults")));
            // appliedRoadClass is deliberately absent: it is [HideInInspector] bookkeeping that records which
            // class's defaults were last written, and an author editing it would silently change what a later
            // class change is allowed to refresh.

            // The median, as the two fields it really is: what separates the directions, and how wide that is.
            // A painted marker separates without occupying width, so the type decides whether the width counts --
            // MedianWidthEffective is where that arithmetic lives.
            f.Add(new FieldDescriptor("Road", "centerDivider", "Median type", FieldGroup.WhatItIs,
                FieldUnit.None, FieldRange.Between(0, System.Enum.GetValues(typeof(CenterDividerTypeEnum)).Length - 1,
                "AUTHORED: none, concrete barrier, barrier with glare screens, wire rope, or painted markers")));
            f.Add(new FieldDescriptor("Road", "medianWidth", "Median width", FieldGroup.WhatItIs,
                FieldUnit.Metres, FieldRange.Between(0.0, 30.0,
                "AUTHORED ceiling: 0 is no physical median and rural depressed medians reach ~30 m. What a median of "
                + "a given width can actually HOLD is not decided here -- MedianContent's cited band table owns that, "
                + "from painted flush below 1.2 m up to trough-and-planting at 15 m")));

            // The design speed. Every taper length in the tool is computed from this figure, so it is not decoration:
            // a wrong speed here produces a lane that opens over the wrong distance. The flat bounds below are the
            // widest real posted limits; whether the value suits THIS road's class is a SectionRules cross-field rule.
            f.Add(new FieldDescriptor("Road", "speedLimitKPH", "Design speed", FieldGroup.WhatItIs,
                FieldUnit.KmPerHour, FieldRange.Between(5.0, 140.0,
                "AUTHORED bounds spanning real posted limits, from ~5 km/h shared space to the 140 km/h of the "
                + "fastest posted motorways; 0 or less falls back to the road class default")));

            // --- Road: what it LOOKS like (furniture / shoulders) ---
            f.Add(new FieldDescriptor("Road", "shoulderWidth", "Shoulder width", FieldGroup.WhatItLooksLike,
                FieldUnit.Metres, FieldRange.Between(0.0, 4.0,
                "AUTHORED: shoulder 0.6–3.0 m typical (AASHTO); bound raised for wide verges")));

            // --- Road: what it DOES to the world (terrain interaction / grade) ---
            f.Add(new FieldDescriptor("Road", "maxGrade", "Max grade", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Fraction, FieldRange.Between(0.0, 0.15,
                "AASHTO max grade 6–12% by terrain and design speed; 15% hard ceiling")));
            f.Add(new FieldDescriptor("Road", "matchTerrainSubtraction", "Terrain match drop", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(0.0, 2.0,
                "AUTHORED: vertical offset the carriageway sinks into matched terrain")));

            // --- Road: what is GENERATED from it (build quality) ---
            f.Add(new FieldDescriptor("Road", "roadDefinition", "Segment resolution", FieldGroup.WhatIsGeneratedFromIt,
                FieldUnit.Metres, FieldRange.Between(
                    RoadFieldLimits.SegmentResolutionMinMetres, RoadFieldLimits.SegmentResolutionMaxMetres,
                    RoadFieldLimits.SegmentResolutionSource)));

            // --- RoadTerrain: what it DOES to the world (splat paint) ---
            f.Add(new FieldDescriptor("RoadTerrain", "splatWidth", "Splat paint half-width", FieldGroup.WhatItDoesToTheWorld,
                FieldUnit.Metres, FieldRange.Between(1.0, 60.0,
                "AUTHORED: paint distance from carriageway; RM-38 flags the 30 m default as over-wide")));

            // --- RoadTerrain: what it LOOKS like (splat resolution) ---
            f.Add(new FieldDescriptor("RoadTerrain", "splatResoWidth", "Splat resolution W", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.Between(512, 4096,
                "AUTHORED: power-of-two splat map resolution")));
            f.Add(new FieldDescriptor("RoadTerrain", "splatResoHeight", "Splat resolution H", FieldGroup.WhatItLooksLike,
                FieldUnit.None, FieldRange.Between(512, 4096,
                "AUTHORED: power-of-two splat map resolution")));

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

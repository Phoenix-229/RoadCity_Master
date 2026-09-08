// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's gate: proves the inspector metadata core without a scene or a repaint. It asserts that every
    /// numeric measured field carries a unit label; that every bounded field names the SOURCE of its limit; that an
    /// out-of-range value produces an inline message naming both the offending limit and its source (never a silent
    /// clamp); that an in-range value passes; that every field falls in exactly one of the four foldout groups and is
    /// retrievable per component; that the only product string the model exposes is the display name "Road & City
    /// Master" (C-05); and that the table is deterministic on repeat (C-06). RM-93 extends it with the section
    /// controls RM-88/89 added -- road class, one-way, median type and width, design speed -- proving each is
    /// registered against a field that really exists on Road, and with the two CROSS-FIELD limits that no FieldRange
    /// can express: 0 backward lanes is legal only on a one-way, and a design speed is judged against the class the
    /// road was dimensioned as. The OnInspectorGUI drawing -- foldout
    /// rendering, tooltip display, no-scroll layout -- is the deferred scene half. Report lines are prefixed
    /// RCMINSPECTOR. </summary>
    public static class InspectorMetadataGate
    {
        [MenuItem("Window/Road City Master/Testing/Inspector metadata (units, ranges, sources)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.InspectorMetadataGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMINSPECTOR BEGIN\n");
            List<string> failures = new List<string>();

            CaseNumericFieldsHaveUnits(builder, failures);
            CaseBoundedFieldsNameSource(builder, failures);
            CaseOutOfRangeNamesLimitAndSource(builder, failures);
            CaseInRangePasses(builder, failures);
            CaseGroupingAndLookup(builder, failures);
            CaseTheNewSectionControlsAreSurfaced(builder, failures);
            CaseCrossFieldLimitsNameTheirSource(builder, failures);
            CaseProductNameIsDisplayName(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMINSPECTOR FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMINSPECTOR result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMINSPECTOR END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Every field with a measured unit (m, °, km/h, %, fraction, ×) renders a non-empty unit token, so no
        /// numeric label ships bare. Counts/indices/refs are exempt by design. </summary>
        private static void CaseNumericFieldsHaveUnits(StringBuilder builder, List<string> failures)
        {
            int measured = 0;
            foreach (FieldDescriptor d in InspectorFieldRegistry.All())
            {
                if (d.IsNumericMeasured())
                {
                    measured++;
                    Expect(!string.IsNullOrEmpty(d.UnitLabel()),
                        "units: measured field " + d.component + "." + d.field + " has no unit label", failures);
                }
            }
            Expect(measured > 0, "units: no measured fields in the registry (setup)", failures);
            builder.Append("RCMINSPECTOR ").Append("units-on-numeric".PadRight(22))
                   .Append("every measured field carries a unit token\n");
        }

        /// <summary> Every bounded field names where its limit comes from -- a standard or an AUTHORED default -- so the
        /// inline message can cite it rather than showing a bare number. </summary>
        private static void CaseBoundedFieldsNameSource(StringBuilder builder, List<string> failures)
        {
            foreach (FieldDescriptor d in InspectorFieldRegistry.All())
            {
                if (d.range.IsBounded)
                {
                    Expect(!string.IsNullOrEmpty(d.range.source),
                        "source: bounded field " + d.component + "." + d.field + " names no limit source", failures);
                }
            }
            builder.Append("RCMINSPECTOR ").Append("bounded-has-source".PadRight(22))
                   .Append("every range names the source of its limit\n");
        }

        /// <summary> An out-of-range value yields a message that names BOTH the offending limit and its source -- the
        /// inline text the drawer shows at the field, never a silent clamp. Checks an over-max and an under-min. </summary>
        private static void CaseOutOfRangeNamesLimitAndSource(StringBuilder builder, List<string> failures)
        {
            FieldDescriptor grade = Find("Road", "maxGrade");
            Expect(grade != null, "range: Road.maxGrade missing from registry (setup)", failures);
            if (grade != null)
            {
                ValidationResult over = grade.Validate(0.20);
                Expect(!over.ok, "range: a grade of 0.20 above the 0.15 ceiling was accepted", failures);
                Expect(over.message != null && over.message.Contains("maximum") && over.message.Contains("0.15"),
                    "range: the over-max message did not name the limit", failures);
                Expect(over.message != null && over.message.Contains(grade.range.source),
                    "range: the over-max message did not name the source", failures);
            }

            FieldDescriptor lane = Find("Road", "laneWidth");
            Expect(lane != null, "range: Road.laneWidth missing from registry (setup)", failures);
            if (lane != null)
            {
                ValidationResult under = lane.Validate(2.0);
                Expect(!under.ok, "range: a 2.0 m lane below the 2.5 m minimum was accepted", failures);
                Expect(under.message != null && under.message.Contains("minimum") && under.message.Contains("2.5"),
                    "range: the under-min message did not name the limit", failures);
                Expect(under.message != null && under.message.Contains(lane.range.source),
                    "range: the under-min message did not name the source", failures);
            }
            builder.Append("RCMINSPECTOR ").Append("range-message".PadRight(22))
                   .Append("out-of-range names the limit and its source\n");
        }

        /// <summary> An in-range value passes with no message. </summary>
        private static void CaseInRangePasses(StringBuilder builder, List<string> failures)
        {
            FieldDescriptor lane = Find("Road", "laneWidth");
            if (lane != null)
            {
                ValidationResult ok = lane.Validate(3.5);
                Expect(ok.ok, "in-range: a valid 3.5 m lane was rejected", failures);
            }
            FieldDescriptor grade = Find("Road", "maxGrade");
            if (grade != null)
            {
                Expect(grade.Validate(0.08).ok, "in-range: the default 0.08 grade was rejected", failures);
            }
            builder.Append("RCMINSPECTOR ").Append("in-range-passes".PadRight(22))
                   .Append("a valid value validates with no message\n");
        }

        /// <summary> Every field belongs to exactly one of the four foldout groups and is retrievable per component, so
        /// the drawer can walk a component's fields group by group. </summary>
        private static void CaseGroupingAndLookup(StringBuilder builder, List<string> failures)
        {
            foreach (FieldDescriptor d in InspectorFieldRegistry.All())
            {
                bool known = d.group == FieldGroup.WhatItIs || d.group == FieldGroup.WhatItLooksLike
                    || d.group == FieldGroup.WhatItDoesToTheWorld || d.group == FieldGroup.WhatIsGeneratedFromIt;
                Expect(known, "group: " + d.component + "." + d.field + " has no valid foldout group", failures);
                Expect(!string.IsNullOrEmpty(d.label), "group: " + d.component + "." + d.field + " has an empty label", failures);
            }
            IList<FieldDescriptor> road = InspectorFieldRegistry.ForComponent("Road");
            Expect(road.Count > 0, "group: no Road fields returned by ForComponent (setup)", failures);
            IList<FieldDescriptor> terr = InspectorFieldRegistry.ForComponent("RoadTerrain");
            Expect(terr.Count > 0, "group: no RoadTerrain fields returned by ForComponent (setup)", failures);
            builder.Append("RCMINSPECTOR ").Append("grouping-lookup".PadRight(22))
                   .Append("every field has a group and is retrievable per component\n");
        }

        /// <summary> RM-93. The controls RM-88 and RM-89 added to the model are actually reachable by an author: each is
        /// registered, sits in the "what it is" foldout because each one is part of the road's identity rather than its
        /// appearance, carries the right unit, and -- the check that matters -- names a field that REALLY EXISTS on
        /// Road. A descriptor naming a field that was renamed or never added draws nothing at all: the drawer's
        /// FindProperty returns null and returns false, silently, which is exactly the failure a census cannot see.
        /// The lane-count and lane-width descriptors were already registered under RM-51 and are re-checked here rather
        /// than re-added, so the section is proved complete in one place. </summary>
        private static void CaseTheNewSectionControlsAreSurfaced(StringBuilder builder, List<string> failures)
        {
            string[] fields = { "roadClass", "isOneWay", "centerDivider", "medianWidth", "speedLimitKPH",
                                "laneWidth", "lanesForward", "lanesBackward" };
            FieldUnit[] units = { FieldUnit.None, FieldUnit.None, FieldUnit.None, FieldUnit.Metres,
                                  FieldUnit.KmPerHour, FieldUnit.Metres, FieldUnit.None, FieldUnit.None };

            for (int i = 0; i < fields.Length; i++)
            {
                FieldDescriptor d = Find("Road", fields[i]);
                Expect(d != null, "surfaced: Road." + fields[i] + " is not registered, so it never draws", failures);
                if (d == null) { continue; }

                Expect(d.group == FieldGroup.WhatItIs,
                    "surfaced: Road." + fields[i] + " is in the " + d.group + " foldout, not WhatItIs", failures);
                Expect(d.unit == units[i],
                    "surfaced: Road." + fields[i] + " carries unit " + d.unit + ", expected " + units[i], failures);
                Expect(d.range.IsBounded,
                    "surfaced: Road." + fields[i] + " has no bounds, so nothing can be validated inline", failures);

                System.Reflection.FieldInfo info = typeof(Road).GetField(fields[i]);
                Expect(info != null,
                    "surfaced: Road." + fields[i] + " is registered but no such serialised field exists on Road",
                    failures);
            }

            // The one field deliberately NOT surfaced. Registering it would let an author edit the record of which
            // class's defaults were last applied, which decides what a later class change may refresh.
            Expect(Find("Road", "appliedRoadClass") == null,
                "surfaced: appliedRoadClass is registered, but it is HideInInspector bookkeeping", failures);

            builder.Append("RCMINSPECTOR ").Append("section-controls".PadRight(22))
                   .Append("class, one-way, median and design speed are all reachable\n");
        }

        /// <summary> RM-93. The two limits that need more than one field to decide. Neither can be a FieldRange, so
        /// neither is covered by the range cases above: 0 backward lanes is correct on a one-way and wrong on a
        /// two-way, and 130 km/h is inside every road's posted-speed range while being far above what an urban
        /// two-lane street was dimensioned for. Both messages must name the limit AND where it comes from, same rule
        /// as a flat range. The final loop is the strongest clause here: every class's own default speed must pass its
        /// own check, which would fail immediately if the tolerance were ever made negative or the table edited to
        /// contradict itself. </summary>
        private static void CaseCrossFieldLimitsNameTheirSource(StringBuilder builder, List<string> failures)
        {
            ValidationResult twoWayZero = SectionRules.LanesBackward(0, false);
            Expect(!twoWayZero.ok, "cross: 0 backward lanes on a two-way road was accepted", failures);
            Expect(twoWayZero.message != null && twoWayZero.message.Contains("minimum 1")
                && twoWayZero.message.Contains("two-way"),
                "cross: the two-way zero-lane message did not name the limit and why", failures);

            Expect(SectionRules.LanesBackward(0, true).ok,
                "cross: 0 backward lanes on a ONE-WAY road was rejected, but that is what one-way means", failures);
            Expect(SectionRules.LanesBackward(LaneModel.Unsplit, false).ok,
                "cross: the unsplit sentinel was rejected, which would flag every legacy road on load", failures);
            Expect(!SectionRules.LanesBackward(-2, false).ok,
                "cross: a lane count of -2 was accepted", failures);
            Expect(SectionRules.LanesBackward(1, false).ok,
                "cross: 1 backward lane on a two-way road was rejected", failures);

            Road.RoadClassEnum urban = Road.RoadClassEnum.UrbanTwoLane;
            double urbanDesign = RoadClassDefaults.For(urban).DesignSpeedKPH;
            double urbanCeiling = urbanDesign + SectionRules.DesignSpeedOverrunToleranceKPH;

            ValidationResult tooFast = SectionRules.DesignSpeed(urbanCeiling + 10.0, urban);
            Expect(!tooFast.ok, "cross: a speed above the class ceiling was accepted", failures);
            Expect(tooFast.message != null
                && tooFast.message.Contains(urbanCeiling.ToString(CultureInfo.InvariantCulture))
                && tooFast.message.Contains(urban.ToString()),
                "cross: the over-speed message did not name the ceiling and the class", failures);
            Expect(tooFast.message != null
                && tooFast.message.Contains(RoadClassDefaults.For(urban).DesignSpeedCite.Reference),
                "cross: the over-speed message did not carry the class table's own source", failures);

            Expect(SectionRules.DesignSpeed(urbanCeiling, urban).ok,
                "cross: a speed exactly at the ceiling was rejected; the band is inclusive", failures);

            int checkedClasses = 0;
            foreach (Road.RoadClassEnum c in System.Enum.GetValues(typeof(Road.RoadClassEnum)))
            {
                checkedClasses++;
                Expect(SectionRules.DesignSpeed(RoadClassDefaults.For(c).DesignSpeedKPH, c).ok,
                    "cross: road class " + c + " fails its OWN default design speed", failures);
            }
            Expect(checkedClasses > 0, "cross: no road classes were checked (setup)", failures);

            builder.Append("RCMINSPECTOR ").Append("cross-field".PadRight(22))
                   .Append("one-way and per-class speed limits name their source (")
                   .Append(checkedClasses).Append(" classes)\n");
        }

        /// <summary> The only product string the model exposes is the display name "Road & City Master" -- the header
        /// text (C-05: no script carries the old third-party name). </summary>
        private static void CaseProductNameIsDisplayName(StringBuilder builder, List<string> failures)
        {
            Expect(InspectorFieldRegistry.ProductDisplayName == "Road & City Master",
                "name: the product display string is not \"Road & City Master\"", failures);
            builder.Append("RCMINSPECTOR ").Append("product-name".PadRight(22))
                   .Append("header string is the display name only\n");
        }

        /// <summary> The table is identical on repeat: same count and same first field (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            IList<FieldDescriptor> a = InspectorFieldRegistry.All();
            IList<FieldDescriptor> b = InspectorFieldRegistry.All();
            Expect(a.Count == b.Count, "determinism: the registry count changed between calls", failures);
            if (a.Count > 0 && b.Count > 0)
            {
                Expect(a[0].component == b[0].component && a[0].field == b[0].field,
                    "determinism: the first field changed between calls", failures);
            }
            builder.Append("RCMINSPECTOR ").Append("determinism".PadRight(22))
                   .Append("the field table is identical on repeat\n");
        }

        // --- helpers ---

        private static FieldDescriptor Find(string component, string field)
        {
            foreach (FieldDescriptor d in InspectorFieldRegistry.All())
            {
                if (d.component == component && d.field == field) { return d; }
            }
            return null;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif


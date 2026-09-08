// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-88's headless proof of the road-class taxonomy: the pure lookup in <see cref="RoadClassDefaults"/>
    /// returns a COMPLETE default set for EVERY <see cref="Road.RoadClassEnum"/> value (total), each of the five
    /// headline numbers (lane width, lane count, shoulder width, design speed, max grade) traces to a named reference
    /// or an AUTHORED note (nothing unknown, C-15), the lookup is byte-identical on repeat (C-06), selecting a class
    /// preserves a value the author explicitly overrode (the pure merge), and the upgrade is non-breaking: the new
    /// `roadClass` field is ADDED beside the lane fields rather than substituted for them, an old road's authored lane
    /// count still reaches the road through RM-89's migration attribute, and the default class is the 2-lane road the
    /// tool ships today. </summary>
    public static class CityRoadClassGate
    {
        private const string Prefix = "RCMROADCLASS";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Road class taxonomy")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            _cases = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CaseLookupIsTotal(failures, sb);
            CaseEveryDefaultTraces(failures, sb);
            CaseUpgradeIsNonBreaking(failures);
            CaseOverridePreserved(failures);
            CaseLookupIsDeterministic(failures);

            for (int i = 0; i < failures.Count; i++) { sb.AppendLine(Prefix + " FAIL   " + failures[i]); }
            sb.AppendLine(Prefix + " result " + (failures.Count == 0 ? "PASS" : "FAIL") + "  cases " + _cases
                + "  failures " + failures.Count);
            sb.Append(Prefix + " END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(message); }
        }

        /// <summary> Total: iterate every enum value and require For() to return a Complete profile, and require the
        /// register to hold exactly one row per enum value (no gap, no phantom, no duplicate). </summary>
        private static void CaseLookupIsTotal(List<string> failures, StringBuilder sb)
        {
            Array values = Enum.GetValues(typeof(Road.RoadClassEnum));
            int incomplete = 0;
            HashSet<Road.RoadClassEnum> seen = new HashSet<Road.RoadClassEnum>();
            foreach (Road.RoadClassEnum c in values)
            {
                RoadClassProfile p = RoadClassDefaults.For(c);
                if (!p.Complete)
                {
                    incomplete++;
                    failures.Add("class returns no complete default set: " + c);
                }
            }
            foreach (RoadClassProfile p in RoadClassDefaults.All()) { seen.Add(p.Class); }

            Expect(incomplete == 0, Num(incomplete) + " road classes lack a complete default set.", failures);
            Expect(RoadClassDefaults.All().Count == values.Length,
                "the register must hold exactly one row per enum value (" + Num(values.Length) + "), found "
                + Num(RoadClassDefaults.All().Count) + ".", failures);
            Expect(seen.Count == values.Length, "every enum value must appear exactly once in the register.", failures);

            sb.AppendLine(Prefix + " total  enumValues " + Num(values.Length) + "  rows "
                + Num(RoadClassDefaults.All().Count) + "  incomplete " + Num(incomplete));
        }

        /// <summary> Traceability (C-15 / § 10.18): every one of the five headline numbers on every class is a
        /// well-formed audit row — a cited reference for Matches, a note for Authored — so nothing is left unknown and
        /// no Matches stands without a source. The count is never written here: the case walks whatever
        /// <see cref="RoadClassProfile.Citations"/> yields, so the shoulder column RM-110 added is audited without this
        /// gate changing. The verdict tally is reported. </summary>
        private static void CaseEveryDefaultTraces(List<string> failures, StringBuilder sb)
        {
            int malformed = 0, matches = 0, authored = 0, other = 0;
            foreach (RoadClassProfile p in RoadClassDefaults.All())
                foreach (NumberAudit n in p.Citations())
                {
                    if (!n.IsWellFormed())
                    {
                        malformed++;
                        if (malformed <= 8) failures.Add("malformed default row on " + p.Class + ": field '" + n.Field + "'.");
                    }
                    if (n.Verdict == AuditVerdict.Matches) matches++;
                    else if (n.Verdict == AuditVerdict.Authored) authored++;
                    else other++;
                }
            Expect(malformed == 0, "every default must trace to a reference or an AUTHORED note; " + Num(malformed)
                + " were malformed.", failures);
            Expect(other == 0, Num(other) + " default rows carry a Wrong verdict (a class default is Matches or "
                + "Authored, never Wrong).", failures);
            sb.AppendLine(Prefix + " trace  matches " + Num(matches) + "  authored " + Num(authored));
        }

        /// <summary> Non-breaking upgrade (§ 10.20): the `roadClass` field is ADDED, not substituted. Proven the two
        /// ways observable without a scene: an old road's authored lane count still arrives -- RM-89 replaced the
        /// single directionless `laneAmount` with a per-direction pair, and `lanesForward` carries
        /// `[FormerlySerializedAs("laneAmount")]`, which is what makes Unity hand the old value to the new field
        /// instead of dropping it; the new `roadClass` field exists, is public and serialised, and is of the enum
        /// type; and the default class the field ships with (UrbanTwoLane) is a 2-lane profile — today's road — so an
        /// upgraded road that takes the default is not reshaped.
        ///
        /// This case used to assert that the literal `laneAmount` field still existed. It no longer does, by design,
        /// so asserting its presence would now fail for the right reason and hide the wrong one. What actually
        /// matters — that no author loses data on upgrade — is asserted directly instead. </summary>
        private static void CaseUpgradeIsNonBreaking(List<string> failures)
        {
            Type t = typeof(Road);

            FieldInfo lanesForward = t.GetField("lanesForward", BindingFlags.Public | BindingFlags.Instance);
            Expect(lanesForward != null && lanesForward.FieldType == typeof(int),
                "'lanesForward' must exist on Road as an int (it is where an old road's lane count lands).", failures);
            bool migrated = false;
            if (lanesForward != null)
            {
                object[] former = lanesForward.GetCustomAttributes(
                    typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false);
                for (int i = 0; i < former.Length; i++)
                {
                    if (((UnityEngine.Serialization.FormerlySerializedAsAttribute)former[i]).oldName == "laneAmount")
                    {
                        migrated = true;
                    }
                }
            }
            Expect(migrated, "'lanesForward' must carry [FormerlySerializedAs(\"laneAmount\")] so a road serialised "
                + "before RM-89 keeps its authored lane count instead of silently reverting to the default.", failures);

            FieldInfo roadClass = t.GetField("roadClass", BindingFlags.Public | BindingFlags.Instance);
            bool serialisedEnum = roadClass != null
                && roadClass.FieldType == typeof(Road.RoadClassEnum)
                && !Attribute.IsDefined(roadClass, typeof(NonSerializedAttribute));
            Expect(serialisedEnum, "the new 'roadClass' field must exist, be a serialised public RoadClassEnum.", failures);

            RoadClassProfile def = RoadClassDefaults.For(Road.RoadClassEnum.UrbanTwoLane);
            Expect(def.Complete && def.LaneCount == 2,
                "the default class (UrbanTwoLane) must be a complete 2-lane profile matching today's road.", failures);
        }

        /// <summary> The pure override-preserving merge: a field the author moved off the outgoing class's default is
        /// kept when the class changes; a field still at that default is refreshed to the incoming class's value. </summary>
        private static void CaseOverridePreserved(List<string> failures)
        {
            // Author overrode: current (7.0) differs from previous default (3.25) -> kept, not clobbered by new (3.75).
            double kept = RoadClassDefaults.MergeField(3.25, 7.0, 3.75);
            Expect(Math.Abs(kept - 7.0) < 1e-9, "an explicit override must survive a class change (got "
                + kept.ToString(CultureInfo.InvariantCulture) + ").", failures);

            // Untouched: current equals previous default (3.25) -> refreshed to the new class's default (3.75).
            double refreshed = RoadClassDefaults.MergeField(3.25, 3.25, 3.75);
            Expect(Math.Abs(refreshed - 3.75) < 1e-9, "an untouched field must refresh to the new class default (got "
                + refreshed.ToString(CultureInfo.InvariantCulture) + ").", failures);
        }

        /// <summary> C-06: the whole lookup renders byte-identically on repeat — fixed arrays, no clock or dictionary
        /// order. </summary>
        private static void CaseLookupIsDeterministic(List<string> failures)
        {
            Expect(RoadClassDefaults.ToTable() == RoadClassDefaults.ToTable(),
                "the road-class lookup must be deterministic across calls.", failures);
        }

        private static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }


    }
}
#endif

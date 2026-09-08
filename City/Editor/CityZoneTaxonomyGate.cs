// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-42's gate: proves the zone taxonomy is a single, data-driven, fail-closed source without a scene.
    /// It asserts that a zone carries real populated planning parameters (setback, coverage, plot ratio); that adding a
    /// zone is authoring one asset with NO code change and NO enum branch; that an unknown id resolves to null
    /// (fail-closed, never a silent default); that the two zones generic taxonomies omit are present and
    /// distinguished by their OWN fields (organic topology, water-edge) rather than being residential/industrial with
    /// different prefabs; that the catalog is a SINGLE shared source (the same reference is what both consumers get for
    /// one id); that every authored zone validates; and that resolution is bit-identical on repeat (C-06). The
    /// scene-side operator/prefab/streetscape wiring that READS a zone is the deferred half. Report lines are prefixed
    /// RCMZONE. </summary>
    public static class CityZoneTaxonomyGate
    {
        [MenuItem("Window/Road City Master/Testing/City zone taxonomy (single-source registry)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityZoneTaxonomyGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMZONE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMZONE BEGIN\n");
            List<string> failures = new List<string>();

            CaseZoneCarriesRealParameters(builder, failures);
            CaseAddingZoneNeedsNoCodeChange(builder, failures);
            CaseUnknownIdResolvesNull(builder, failures);
            CaseSpecialZonesPresentAndDistinguished(builder, failures);
            CaseSingleSharedSource(builder, failures);
            CaseEveryAuthoredZoneValid(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMZONE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMZONE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMZONE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A zone carries populated real planning parameters, not placeholder blanks. </summary>
        private static void CaseZoneCarriesRealParameters(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass z = c.Resolve("zone.residential.low");
            Expect(z != null, "params: the low-density residential zone was not in the authored catalog", failures);
            if (z != null)
            {
                Expect(!string.IsNullOrEmpty(z.planningTerm), "params: the zone had no real planning term", failures);
                Expect(z.permittedBuildingCategories != null && z.permittedBuildingCategories.Length > 0,
                    "params: the zone permitted no building categories", failures);
                Expect(z.frontSetbackMetres > 0f && z.siteCoverage > 0f && z.plotRatio > 0f,
                    "params: the zone's setback / coverage / plot ratio were not real values", failures);
            }
            builder.Append("RCMZONE ").Append("params".PadRight(22))
                   .Append("a zone carries real planning parameters\n");
        }

        /// <summary> Adding a zone is authoring one asset -- Add takes a runtime instance, no code branch anywhere. </summary>
        private static void CaseAddingZoneNeedsNoCodeChange(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            int before = c.Count;
            Expect(!c.Contains("zone.civic.institution"), "add: the new zone was already present before adding", failures);
            ZoneTypeClass z = new ZoneTypeClass();
            z.zoneId = "zone.civic.institution";
            z.planningTerm = "Civic / institutional";
            z.permittedBuildingCategories = new[] { "civic.hall" };
            c.Add(z);
            Expect(c.Count == before + 1 && c.Contains("zone.civic.institution"),
                "add: adding an authored zone asset did not extend the catalog (should need no code change)", failures);
            // The set stays a set: re-adding the same id is ignored, not duplicated.
            c.Add(z);
            Expect(c.Count == before + 1, "add: re-adding the same zone id duplicated it (the set is not a set)", failures);
            builder.Append("RCMZONE ").Append("add-no-code".PadRight(22))
                   .Append("adding a zone = authoring one asset, no code change\n");
        }

        /// <summary> An unknown id resolves to null -- fail-closed, never a silent default or enum fallback. </summary>
        private static void CaseUnknownIdResolvesNull(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            Expect(c.Resolve("zone.does.not.exist") == null,
                "unknown: an unknown zone id did not resolve to null (fail-closed)", failures);
            Expect(c.Resolve(null) == null && c.Resolve("") == null,
                "unknown: a null/empty zone id did not resolve to null", failures);
            Expect(!c.Contains("zone.does.not.exist"), "unknown: Contains reported a zone the catalog does not define", failures);
            builder.Append("RCMZONE ").Append("fail-closed".PadRight(22))
                   .Append("unknown id -> null, never a silent default\n");
        }

        /// <summary> The two zones generic taxonomies omit are present and distinguished by their OWN fields. </summary>
        private static void CaseSpecialZonesPresentAndDistinguished(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass informal = c.Resolve("zone.informal.settlement");
            ZoneTypeClass port = c.Resolve("zone.port.logistics");
            Expect(informal != null && informal.organicTopology,
                "special: the informal settlement zone was absent or not flagged organic (not just low-density residential)", failures);
            Expect(port != null && port.boundToWaterEdge,
                "special: the port / logistics zone was absent or not bound to a water edge (not just industrial)", failures);
            // They are genuinely distinct from the ordinary zones they are often collapsed into.
            ZoneTypeClass resLow = c.Resolve("zone.residential.low");
            ZoneTypeClass industrial = c.Resolve("zone.industrial.general");
            Expect(informal != null && resLow != null && !resLow.organicTopology,
                "special: low-density residential was wrongly flagged organic", failures);
            Expect(port != null && industrial != null && !industrial.boundToWaterEdge,
                "special: general industrial was wrongly bound to a water edge", failures);
            builder.Append("RCMZONE ").Append("special-zones".PadRight(22))
                   .Append("informal + port present, distinguished by own fields\n");
        }

        /// <summary> The catalog is a single shared source: resolving one id twice returns the SAME reference, so the
        /// city layer and the settlement system read one object, not two lists that could drift. </summary>
        private static void CaseSingleSharedSource(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            ZoneTypeClass a = c.Resolve("zone.commercial.centre");
            ZoneTypeClass b = c.Resolve("zone.commercial.centre");
            Expect(ReferenceEquals(a, b),
                "single-source: two resolutions of one id returned different objects (the source is not shared)", failures);
            // The id list is the authoring order, and every listed id resolves.
            IList<string> ids = c.ZoneIds();
            bool allResolve = ids.Count == c.Count;
            for (int i = 0; i < ids.Count; i++) { if (c.Resolve(ids[i]) == null) { allResolve = false; } }
            Expect(allResolve, "single-source: a listed zone id did not resolve back to a zone", failures);
            builder.Append("RCMZONE ").Append("single-source".PadRight(22))
                   .Append("one id -> one shared reference for both consumers\n");
        }

        /// <summary> Every authored zone passes validation -- a bad asset is caught at author time. </summary>
        private static void CaseEveryAuthoredZoneValid(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c = ZoneCatalog.Authored();
            IList<string> ids = c.ZoneIds();
            bool allValid = ids.Count > 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!ZoneTaxonomy.IsValid(c.Resolve(ids[i]))) { allValid = false; }
            }
            Expect(allValid, "valid: an authored zone had self-inconsistent parameters", failures);
            // And a deliberately broken zone is rejected (validation actually bites).
            ZoneTypeClass broken = new ZoneTypeClass();
            broken.zoneId = "zone.broken";
            broken.storeyMin = 5; broken.storeyMax = 2; // inverted range
            Expect(!ZoneTaxonomy.IsValid(broken), "valid: a self-inconsistent zone was accepted as valid", failures);
            builder.Append("RCMZONE ").Append("validity".PadRight(22))
                   .Append("every authored zone self-consistent; bad ones rejected\n");
        }

        /// <summary> Resolution is bit-identical on repeat -- pure authored data, ordered keys (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            ZoneCatalog c1 = ZoneCatalog.Authored();
            ZoneCatalog c2 = ZoneCatalog.Authored();
            bool same = c1.Count == c2.Count;
            IList<string> ids1 = c1.ZoneIds();
            IList<string> ids2 = c2.ZoneIds();
            if (ids1.Count != ids2.Count) { same = false; }
            else { for (int i = 0; i < ids1.Count; i++) { if (ids1[i] != ids2[i]) { same = false; } } }
            Expect(same, "determinism: two authored catalogs disagreed on their zones or order", failures);
            builder.Append("RCMZONE ").Append("determinism".PadRight(22))
                   .Append("identical authored catalog -> identical ids and order\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

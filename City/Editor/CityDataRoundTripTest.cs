// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-08's gate: proves a <see cref="CityData"/> survives a save, a genuine reload
    /// from disk, and a re-save BYTE-FOR-BYTE; that no field is dropped or reordered on the way; and
    /// that <see cref="CityData.ClearDerived"/> draws the saved-versus-derived line exactly where
    /// the data model claims -- dropping every non-overridden block and lot while keeping the seed,
    /// the network, the buildings, and every overridden entry.
    ///
    /// It does NOT test that a re-bake reproduces the derived geometry from the seed: no generator
    /// exists yet (RM-09/RM-10), so that half of the round trip cannot be written until it does.
    /// What this gate locks down now is the serialisation contract the generator will depend on. </summary>
    public static class CityDataRoundTripTest
    {
        /// <summary> Name of the throwaway asset this test writes under the tool base and deletes in
        /// a finally. Kept out of any content folder so a crashed run leaves nothing an author would
        /// mistake for real data. </summary>
        private const string tempAssetName = "__citydata_roundtrip_temp.asset";


        [MenuItem("Window/Road City Master/Testing/City data round-trip")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityDataRoundTripTest.RunBatch. Exits 1 on failure so a build
        /// script can gate on it. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }


        /// <summary> Runs the whole round trip. Report lines are prefixed RCMROUNDTRIP so a log grep
        /// can read the verdict without wading through Unity's surrounding output. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMROUNDTRIP BEGIN\n");
            List<string> failures = new List<string>();

            // C-04: the path is derived at run time from the tool root and the project's data folder,
            // never hard-coded.
            string assetPath = RoadEditorUtility.GetBasePath().Replace('\\', '/') + "/" + tempAssetName;
            string fullPath = Application.dataPath + assetPath.Substring("Assets".Length);
            builder.Append("RCMROUNDTRIP asset  ").Append(assetPath).Append("\n");

            try
            {
                // A crashed previous run could have left the temp asset behind; start clean.
                if (AssetDatabase.LoadAssetAtPath<CityData>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                CityData city = BuildFixture();
                AssetDatabase.CreateAsset(city, assetPath);
                AssetDatabase.SaveAssets();
                string text1 = File.ReadAllText(fullPath);

                // Force a genuine deserialise from disk rather than reusing the in-memory instance,
                // so the checks below judge what was written, not what is still in memory.
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                CityData reloaded = AssetDatabase.LoadAssetAtPath<CityData>(assetPath);

                if (reloaded == null)
                {
                    failures.Add("asset did not reload from disk");
                }
                else
                {
                    CheckReloaded(reloaded, failures);

                    // A lossless, stable round trip writes back the same bytes it read.
                    EditorUtility.SetDirty(reloaded);
                    AssetDatabase.SaveAssets();
                    string text2 = File.ReadAllText(fullPath);
                    if (text1 != text2)
                    {
                        failures.Add("re-saved bytes differ from first save (lengths " +
                                     text1.Length + " vs " + text2.Length + ")");
                    }

                    CheckClearDerived(reloaded, failures);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            foreach (string failure in failures)
            {
                builder.Append("RCMROUNDTRIP FAIL   ").Append(failure).Append("\n");
            }

            builder.Append("RCMROUNDTRIP result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMROUNDTRIP END");

            report = builder.ToString();
            return failures.Count == 0;
        }


        /// <summary> Builds a city that exercises every list and both override states, so the checks
        /// below have something to lose if serialisation drops or reorders a field. Two blocks (one
        /// overridden) and three lots (one overridden) let <see cref="CheckClearDerived"/> assert
        /// exact survivor counts. </summary>
        private static CityData BuildFixture()
        {
            CityData city = ScriptableObject.CreateInstance<CityData>();
            city.seed = 1337;
            city.generatorVersion = "roundtrip-fixture";

            city.nodes.Add(new NetworkNode { position = new Vector3(0f, 0f, 0f) });
            city.nodes.Add(new NetworkNode { position = new Vector3(10f, 0f, 0f) });
            city.nodes.Add(new NetworkNode { position = new Vector3(10f, 0f, 10f) });
            city.nodes.Add(new NetworkNode { position = new Vector3(0f, 0f, 10f) });

            city.edges.Add(new NetworkEdge { a = 0, b = 1, roadClassId = 1, isDivided = false, medianWidth = 0f });
            city.edges.Add(new NetworkEdge { a = 1, b = 2, roadClassId = 2, isDivided = true, medianWidth = 3.5f });
            city.edges.Add(new NetworkEdge { a = 2, b = 3, roadClassId = 1, isDivided = false, medianWidth = 0f });

            Block overriddenBlock = new Block { id = 101, isOverridden = true };
            overriddenBlock.boundaryNodes.AddRange(new[] { 0, 1, 2, 3 });
            overriddenBlock.polygon.Add(new Vector3(0.5f, 0f, 0.5f));
            city.blocks.Add(overriddenBlock);

            Block derivedBlock = new Block { id = 102, isOverridden = false };
            derivedBlock.boundaryNodes.AddRange(new[] { 1, 2, 3 });
            city.blocks.Add(derivedBlock);

            Lot overriddenLot = new Lot { id = 201, blockId = 101, zoneId = 5, isOverridden = true };
            overriddenLot.polygon.Add(new Vector3(1f, 0f, 1f));
            city.lots.Add(overriddenLot);
            city.lots.Add(new Lot { id = 202, blockId = 102, zoneId = 0, isOverridden = false });
            city.lots.Add(new Lot { id = 203, blockId = 102, zoneId = 3, isOverridden = false });

            city.buildings.Add(new BuildingInstance
            {
                lotId = 201,
                prefabCategory = "residential",
                variantIndex = 2,
                position = new Vector3(1f, 0f, 1f),
                rotation = Quaternion.Euler(0f, 90f, 0f),
                scale = Vector3.one
            });
            city.buildings.Add(new BuildingInstance
            {
                lotId = 203,
                prefabCategory = "commercial",
                variantIndex = 0,
                position = new Vector3(5f, 0f, 5f),
                rotation = Quaternion.identity,
                scale = Vector3.one
            });

            return city;
        }


        /// <summary> Asserts the deserialised city carries every field the fixture put in: list
        /// counts, the schema/seed/version scalars, the one non-trivial edge, and the override flags
        /// in order. Anything a serialiser silently dropped shows up here. </summary>
        private static void CheckReloaded(CityData city, List<string> failures)
        {
            Expect(city.schemaVersion == CityData.CurrentSchemaVersion, "schemaVersion changed", failures);
            Expect(city.seed == 1337, "seed not preserved", failures);
            Expect(city.generatorVersion == "roundtrip-fixture", "generatorVersion not preserved", failures);

            Expect(city.nodes.Count == 4, "node count " + city.nodes.Count + " != 4", failures);
            Expect(city.edges.Count == 3, "edge count " + city.edges.Count + " != 3", failures);
            Expect(city.blocks.Count == 2, "block count " + city.blocks.Count + " != 2", failures);
            Expect(city.lots.Count == 3, "lot count " + city.lots.Count + " != 3", failures);
            Expect(city.buildings.Count == 2, "building count " + city.buildings.Count + " != 2", failures);

            if (city.edges.Count == 3)
            {
                NetworkEdge divided = city.edges[1];
                Expect(divided.isDivided && divided.medianWidth == 3.5f,
                       "divided-edge fields not preserved", failures);
            }

            if (city.blocks.Count == 2)
            {
                Expect(city.blocks[0].isOverridden && !city.blocks[1].isOverridden,
                       "block override flags not preserved in order", failures);
                Expect(city.blocks[0].boundaryNodes.Count == 4,
                       "overridden block boundaryNodes not preserved", failures);
            }

            if (city.lots.Count == 3)
            {
                Expect(city.lots[0].isOverridden && !city.lots[1].isOverridden && !city.lots[2].isOverridden,
                       "lot override flags not preserved in order", failures);
            }
        }


        /// <summary> Asserts <see cref="CityData.ClearDerived"/> keeps exactly what a re-bake
        /// preserves and drops exactly what it recomputes: the two non-overridden lots and the one
        /// non-overridden block go; the overridden entries, the network, the buildings and the seed
        /// stay. </summary>
        private static void CheckClearDerived(CityData city, List<string> failures)
        {
            int nodesBefore = city.nodes.Count;
            int edgesBefore = city.edges.Count;
            int buildingsBefore = city.buildings.Count;

            city.ClearDerived();

            Expect(city.blocks.Count == 1 && city.blocks[0].isOverridden,
                   "ClearDerived did not leave exactly the overridden block", failures);
            Expect(city.lots.Count == 1 && city.lots[0].isOverridden,
                   "ClearDerived did not leave exactly the overridden lot", failures);
            Expect(city.nodes.Count == nodesBefore, "ClearDerived touched the nodes", failures);
            Expect(city.edges.Count == edgesBefore, "ClearDerived touched the edges", failures);
            Expect(city.buildings.Count == buildingsBefore, "ClearDerived touched the buildings", failures);
            Expect(city.seed == 1337, "ClearDerived touched the seed", failures);
        }


        /// <summary> Records a failure message when <paramref name="condition"/> is false. </summary>
        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(failureMessage);
            }
        }
    }
}
#endif

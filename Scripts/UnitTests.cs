// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#region "Imports"
using UnityEngine;
using System.Collections.Generic;
using RoadCityMaster.Roads;
#endregion


namespace RoadCityMaster.Tests
{
#if UNITY_EDITOR
    public static class UnitTests
    {
        private static RoadSystem roadSystem;


        /// <summary> WARNING: You should only call this on an empty scene. The Road &amp; City Master team is not responsbile for loss of data or work if this function is called by user. </summary>
        public static void RoadCityMasterUnitTests()
        {
            CleanupAllTests();

            //Create new road system and turn off updates:
            GameObject roadSystemObject = new GameObject("RoadCityMasterSystem1");
            //Add road system component.
            roadSystem = roadSystemObject.AddComponent<RoadSystem>();
            roadSystem.isAllowingRoadUpdates = false;

            int numTests = 10;
            double totalTestTime = 0f;
            for (int index = 1; index <= numTests; index++)
            {
                UnityEngine.Debug.Log("Running test " + index);
                double testTime = RunTest(index);
                totalTestTime += testTime;
                UnityEngine.Debug.Log("Test " + index + " complete.  Test time: " + testTime + "ms");
            }
            UnityEngine.Debug.Log("All tests completed.  Total test time: " + totalTestTime + "ms");

            //Turn updates back on and update road:
            roadSystem.isAllowingRoadUpdates = true;
            roadSystem.UpdateAllRoads();
        }


        public static void RoadCityMasterUnitTest1To5()
        {
            CleanupTest(1);

            //Create new road system and turn off updates:
            GameObject roadSystemObject = new GameObject("RoadCityMasterSystem1");
            //Add road system component.
            roadSystem = roadSystemObject.AddComponent<RoadSystem>();
            roadSystem.isAllowingRoadUpdates = false;

            int numTests = 5;
            double totalTestTime = 0f;
            for (int index = 1; index <= numTests; index++)
            {
                UnityEngine.Debug.Log("Running test " + index);
                double testTime = RunTest(index);
                totalTestTime += testTime;
                UnityEngine.Debug.Log("Test " + index + " complete.  Test time: " + testTime + "ms");
            }
            UnityEngine.Debug.Log("All tests completed.  Total test time: " + totalTestTime + "ms");

            //Turn updates back on and update road:
            roadSystem.isAllowingRoadUpdates = true;
            roadSystem.UpdateAllRoads();
        }


        private static long RunTest(int _testID)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            switch (_testID)
            {
                case 1:
                    //Bridges
                    RoadCityMasterUnitTest1();
                    break;
                case 2:
                    //2L intersections
                    RoadCityMasterUnitTest2();
                    break;
                case 3:
                    //4L intersections
                    RoadCityMasterUnitTest3();
                    break;
                case 4:
                    //Large suspension bridge
                    RoadCityMasterUnitTest4();
                    break;
                case 5:
                    //Long road:
                    RoadCityMasterUnitTest5();
                    break;
                case 6:
                    //Very Long road:
                    RoadCityMasterUnitTest6();
                    break;
                case 7:
                    // Double intersection:
                    RoadCityMasterUnitTest7();
                    break;
                case 8:
                    // Road loop:
                    RoadCityMasterUnitTest8();
                    break;
                case 9:
                    // 3 Intersections with end nodes
                    RoadCityMasterUnitTest9();
                    break;
                case 10:
                    // Road Intersection with itself
                    RoadCityMasterUnitTest10();
                    break;
            }
            stopwatch.Stop();
            long testTime = stopwatch.ElapsedMilliseconds;
            return testTime;
        }


        public static void CleanupAllTests()
        {
            Debug.Log("Cleaning up tests");
            CleanupTest(1);

            CleanupTest(6);
            CleanupTest(7);
            CleanupTest(8);
            CleanupTest(9);
            CleanupTest(10);
        }


        public static void CleanupTest(int _index)
        {
            //Get the existing road system, if it exists:
            GameObject roadSystem = (GameObject)GameObject.Find("RoadCityMasterSystem" + _index);
            DestroyTerrainHistory(roadSystem);
            Object.DestroyImmediate(roadSystem);
            FlattenTerrains();
        }


        private static void DestroyTerrainHistory(GameObject _roadSystem)
        {
            //Destroy the terrain histories:
            if (_roadSystem != null)
            {
                Road[] roads = _roadSystem.GetComponents<Road>();
                foreach (Road road in roads)
                {
                    Terraforming.TerrainsReset(road);
                }
            }
        }


        /// <summary> Reset all terrains to 0,0 </summary>
        private static void FlattenTerrains()
        {
            // RM-57: even the test harness goes through the gateway, so the search for a terrain write outside it finds
            // nothing anywhere in the tool. Flattening a whole terrain names the whole tile as the region it changed.
            City.TerrainWriteGateway gateway = City.TerrainWriteGateway.Active;
            gateway.BeginBuild("unit test: flatten terrains");
            Terrain[] allTerrains = EngineIntegration.FindObjectsByType<Terrain>();
            foreach (Terrain terrain in allTerrains)
            {
                if (terrain.terrainData == null)
                {
                    continue;
                }
                int resolution = terrain.terrainData.heightmapResolution;
                gateway.WriteHeights(terrain, new float[resolution, resolution],
                    City.HeightmapRegion.WholeTile(terrain), "unit test");
            }
            gateway.EndBuild();
        }


        private static void RoadCityMasterUnitTest1()
        {
            //Create node locations:
            List<Vector3> nodeLocations = new List<Vector3>();
            int maxCount = 18;
            float mod = 100f;
            Vector3 vector = new Vector3(50f, 40f, 50f);
            for (int index = 0; index < maxCount; index++)
            {
                //tLocs.Add(xVect + new Vector3(tMod * Mathf.Pow((float)i / ((float)MaxCount * 0.15f), 2f), 1f*((float)i*1.25f), tMod * i));
                nodeLocations.Add(vector + new Vector3(mod * Mathf.Pow((float)index / ((float)25 * 0.15f), 2f), 0f, mod * index));
            }

            //Get road system create road:
            Road testRoad = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);

            //Bridge0: (Arch)
            testRoad.spline.nodes[4].isBridgeStart = true;
            testRoad.spline.nodes[4].isBridgeMatched = true;
            testRoad.spline.nodes[7].isBridgeEnd = true;
            testRoad.spline.nodes[7].isBridgeMatched = true;
            testRoad.spline.nodes[4].bridgeCounterpartNode = testRoad.spline.nodes[7];
            testRoad.spline.nodes[4].LoadWizardObjectsFromLibrary("Arch12m-2L", true, true);

            //Bridge1: (Federal causeway)
            testRoad.spline.nodes[8].isBridgeStart = true;
            testRoad.spline.nodes[8].isBridgeMatched = true;
            testRoad.spline.nodes[8].bridgeCounterpartNode = testRoad.spline.nodes[10];
            testRoad.spline.nodes[8].LoadWizardObjectsFromLibrary("Causeway1-2L", true, true);
            testRoad.spline.nodes[10].isBridgeEnd = true;
            testRoad.spline.nodes[10].isBridgeMatched = true;

            //Bridge2: (Steel)
            testRoad.spline.nodes[11].isBridgeStart = true;
            testRoad.spline.nodes[11].isBridgeMatched = true;
            testRoad.spline.nodes[11].bridgeCounterpartNode = testRoad.spline.nodes[13];
            testRoad.spline.nodes[11].LoadWizardObjectsFromLibrary("Steel-2L", true, true);
            testRoad.spline.nodes[13].isBridgeEnd = true;
            testRoad.spline.nodes[13].isBridgeMatched = true;

            //Bridge3: (Causeway)
            testRoad.spline.nodes[14].isBridgeStart = true;
            testRoad.spline.nodes[14].isBridgeMatched = true;
            testRoad.spline.nodes[16].isBridgeEnd = true;
            testRoad.spline.nodes[16].isBridgeMatched = true;
            testRoad.spline.nodes[14].bridgeCounterpartNode = testRoad.spline.nodes[16];
            testRoad.spline.nodes[14].LoadWizardObjectsFromLibrary("Causeway4-2L", true, true);
        }


        /// <summary> Create 2L intersections: </summary>
        private static void RoadCityMasterUnitTest2()
        {
            //Create node locations:
            float startLocX = 800f;
            float startLocY = 200f;
            float startLocYSep = 200f;
            float height = 20f;
            Road road1 = null;
            Road road2 = null;

            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();
            for (int index = 0; index < 9; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (index * 200f), height, 600f));
            }
            road1 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);

            //Get road system, create road #1:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX, height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            //UnitTest_IntersectionHelper(bRoad, tRoad, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Get road system, create road #2:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 2f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            //UnitTest_IntersectionHelper(bRoad, tRoad, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.TurnLane);

            //Get road system, create road #3:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 4f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            //UnitTest_IntersectionHelper(bRoad, tRoad, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.BothTurnLanes);

            //Get road system, create road #4:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 6f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            //UnitTest_IntersectionHelper(bRoad, tRoad, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.TurnLane);

            //Get road system, create road #4:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 8f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            //UnitTest_IntersectionHelper(bRoad, tRoad, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.TurnLane);

            RoadAutomation.CreateIntersectionsProgrammaticallyForRoad(road1, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Now count road intersections, if not 5 throw error
            int intersctionsCount = 0;
            foreach (SplineN node in road1.spline.nodes)
            {
                if (node.isIntersection)
                {
                    intersctionsCount += 1;
                }
            }

            if (intersctionsCount != 5)
            {
                Debug.LogError("Unit Test #2 failed: " + intersctionsCount.ToString() + " intersections instead of 5.");
            }
        }


        /// <summary> This will create an intersection if two nodes overlap on the road. Only good if the roads only overlap once. </summary>
        private static void UnitTestIntersectionHelper(Road _road1, Road _road2, RoadIntersection.iStopTypeEnum _iStopType, RoadIntersection.RoadTypeEnum _roadType)
        {
            SplineN nodeInter1 = null;
            SplineN nodeInter2 = null;

            // Loop through every node of _road1 and _road2
            // Finds the first nodes which are close enough for a intersection
            foreach (SplineN node in _road1.spline.nodes)
            {
                foreach (SplineN node2 in _road2.spline.nodes)
                {
                    if (RootUtils.IsApproximately(Vector3.Distance(node.transform.position, node2.transform.position), 0f, 0.05f))
                    {
                        nodeInter1 = node;
                        nodeInter2 = node2;
                        break;
                    }
                }
            }


            if (nodeInter1 != null && nodeInter2 != null)
            {
                GameObject IntersectionsObject = Intersections.CreateIntersection(nodeInter1, nodeInter2);
                RoadIntersection roadIntersction = IntersectionsObject.GetComponent<RoadIntersection>();
                roadIntersction.intersectionStopType = _iStopType;
                roadIntersction.roadType = _roadType;
            }
        }


        /// <summary> Create 4L intersections: </summary>
        private static void RoadCityMasterUnitTest3()
        {
            //Create node locations:
            float startLocX = 200f;
            float startLocY = 2500f;
            float startLocYSep = 300f;
            float height = 20f;
            Road road1;
            Road road2;

            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();
            for (int index = 0; index < 9; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (index * startLocYSep), height, startLocY + (startLocYSep * 2f)));
            }
            road1 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road1.SetLaneTotal(4);


            //Get road system, create road #1:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX, height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road2.SetLaneTotal(4);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);


            //Get road system, create road #2:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 2f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road2.SetLaneTotal(4);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);


            //Get road system, create road #3:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 4f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road2.SetLaneTotal(4);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);


            //Get road system, create road #4:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 6f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road2.SetLaneTotal(4);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);


            //Get road system, create road #5:
            nodeLocations.Clear();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(startLocX + (startLocYSep * 8f), height, startLocY + (index * startLocYSep)));
            }
            road2 = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
            road2.SetLaneTotal(4);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.TrafficLight1, RoadIntersection.RoadTypeEnum.NoTurnLane);


            //Now count road intersections, if not 5 throw error
            int intersectionsCount = 0;
            foreach (SplineN node in road1.spline.nodes)
            {
                if (node.isIntersection)
                {
                    intersectionsCount += 1;
                }
            }
            if (intersectionsCount != 5)
            {
                Debug.LogError("Unit Test #3 failed: " + intersectionsCount.ToString() + " intersections instead of 5.");
            }
        }


        //Large suspension bridge:
        private static void RoadCityMasterUnitTest4()
        {
            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();
            for (int index = 0; index < 5; index++)
            {
                nodeLocations.Add(new Vector3(3500f, 90f, 200f + (800f * index)));
            }

            Road testRoad = RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);

            //Suspension bridge:
            testRoad.spline.nodes[1].isBridgeStart = true;
            testRoad.spline.nodes[1].isBridgeMatched = true;
            testRoad.spline.nodes[3].isBridgeEnd = true;
            testRoad.spline.nodes[3].isBridgeMatched = true;
            testRoad.spline.nodes[1].bridgeCounterpartNode = testRoad.spline.nodes[3];
            testRoad.spline.nodes[1].LoadWizardObjectsFromLibrary("SuspL-2L", true, true);
        }


        //Long road
        private static void RoadCityMasterUnitTest5()
        {
            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();
            for (int index = 0; index < 48; index++)
            {
                nodeLocations.Add(new Vector3(3000f, 40f, 10f + (79f * index)));
            }
            for (int index = 0; index < 35; index++)
            {
                nodeLocations.Add(new Vector3(2900f - (79f * index), 30f, 3960f));
            }
            for (int index = 0; index < 40; index++)
            {
                nodeLocations.Add(new Vector3(30, 30f, 3960f - (79f * index)));
            }
            RoadAutomation.CreateRoadProgrammatically(roadSystem, ref nodeLocations);
        }


        /// <summary> Very long road </summary>
        public static void RoadCityMasterUnitTest6()
        {
            CleanupTest(6);

            GameObject unitSystem6Object = new GameObject("RoadCityMasterSystem6");
            RoadSystem unitSystem6 = unitSystem6Object.AddComponent<RoadSystem>();
            unitSystem6.isAllowingRoadUpdates = false;

            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();
            for (int index = 0; index < 50; index++)
            {
                nodeLocations.Add(new Vector3(6000f, 40f, 10f + (79f * index)));
            }
            for (int index = 0; index < 50; index++)
            {
                nodeLocations.Add(new Vector3(5900f - (79f * index), 30f, 5950f));
            }
            for (int index = 0; index < 50; index++)
            {
                nodeLocations.Add(new Vector3(-200f, 30f, 5950f - (79f * index)));
            }
            for (int index = 0; index < 50; index++)
            {
                nodeLocations.Add(new Vector3(-200f + (79f * index), 30f, 10f));
            }
            nodeLocations.Add(new Vector3(5995f, 40f, 10f));
            RoadAutomation.CreateRoadProgrammatically(unitSystem6, ref nodeLocations);


            //Turn updates back on and update road:
            unitSystem6.isAllowingRoadUpdates = true;
            unitSystem6.UpdateAllRoads();
        }


        /// <summary> Create 2L double intersections: </summary>
        public static void RoadCityMasterUnitTest7()
        {
            CleanupTest(7);

            GameObject unitSystem7Object = new GameObject("RoadCityMasterSystem7");
            RoadSystem unitSystem7 = unitSystem7Object.AddComponent<RoadSystem>();
            unitSystem7.isAllowingRoadUpdates = false;

            Road road1;
            Road road2;
            Road road3;

            //Create base road:
            List<Vector3> nodeLocations = new List<Vector3>();

            nodeLocations.Add(new Vector3(678f, 0.03f, 244f));
            nodeLocations.Add(new Vector3(660f, 0.03f, 347f));
            nodeLocations.Add(new Vector3(639f, 0.03f, 401f));
            //Intersection1
            nodeLocations.Add(new Vector3(577f, 0.03f, 398f));
            //Intersection2
            nodeLocations.Add(new Vector3(406f, 0.03f, 396f));
            nodeLocations.Add(new Vector3(402f, 0.03f, 516f));
            nodeLocations.Add(new Vector3(417f, 0.03f, 626f));

            road1 = RoadAutomation.CreateRoadProgrammatically(unitSystem7, ref nodeLocations);
            road1.SetLaneTotal(2);

            //Get road system, create road #1:
            nodeLocations.Clear();

            nodeLocations.Add(new Vector3(581f, 0.03f, 237f));
            nodeLocations.Add(new Vector3(580f, 0.03f, 360f));
            //Intersection1
            nodeLocations.Add(new Vector3(577f, 0.03f, 398f));
            nodeLocations.Add(new Vector3(551f, 0.03f, 440f));
            nodeLocations.Add(new Vector3(564f, 0.03f, 485f));

            road2 = RoadAutomation.CreateRoadProgrammatically(unitSystem7, ref nodeLocations);
            road2.SetLaneTotal(2);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Get road system, create road #2:
            nodeLocations.Clear();
            nodeLocations.Add(new Vector3(357f, 0.03f, 347f));
            //Intersection2
            nodeLocations.Add(new Vector3(406f, 0.03f, 396f));
            nodeLocations.Add(new Vector3(494f, 0.03f, 450f));

            road3 = RoadAutomation.CreateRoadProgrammatically(unitSystem7, ref nodeLocations);
            road3.SetLaneTotal(2);
            UnitTestIntersectionHelper(road1, road3, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Turn updates back on and update road:
            unitSystem7.isAllowingRoadUpdates = true;
            unitSystem7.UpdateAllRoads();
        }


        /// <summary> Creates a self closed road; road loop </summary>
        public static void RoadCityMasterUnitTest8()
        {
            CleanupTest(8);

            //Create base road:
            Road road;
            List<Vector3> nodeLocations = new List<Vector3>();

            nodeLocations.Add(new Vector3(575f, 0.03f, 550f));
            nodeLocations.Add(new Vector3(525f, 0.03f, 600f));
            nodeLocations.Add(new Vector3(575f, 0.03f, 650f));
            nodeLocations.Add(new Vector3(625f, 0.03f, 600f));
            nodeLocations.Add(new Vector3(575f, 0.03f, 550f));

            GameObject unitSystem8Object = new GameObject("RoadCityMasterSystem8");
            RoadSystem unitSystem8 = unitSystem8Object.AddComponent<RoadSystem>();
            unitSystem8.isAllowingRoadUpdates = false;
            road = RoadAutomation.CreateRoadProgrammatically(unitSystem8, ref nodeLocations);
            road.SetLaneTotal(2);

            // creates a intersection instead of a road connection
            //UnitTestIntersectionHelper(road1, road1, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            road.spline.ActivateEndNodeConnection(road.spline.nodes[road.spline.nodes.Count - 1], road.spline.nodes[0]);

            //Turn updates back on and update road:
            unitSystem8.isAllowingRoadUpdates = true;
            unitSystem8.UpdateAllRoads();
        }


        /// <summary> Create 3 intersections with end node situations: </summary>
        public static void RoadCityMasterUnitTest9()
        {
            CleanupTest(9);

            GameObject unitSystem9Object = new GameObject("RoadCityMasterSystem9");
            RoadSystem unitSystem9 = unitSystem9Object.AddComponent<RoadSystem>();
            unitSystem9.isAllowingRoadUpdates = false;

            Road road1;
            Road road2;
            Road road3;
            Road road4;

            //Create base road #1:
            List<Vector3> nodeLocations = new List<Vector3>();

            nodeLocations.Add(new Vector3(724f, 0.03f, 220f));
            nodeLocations.Add(new Vector3(711f, 0.03f, 345f));
            nodeLocations.Add(new Vector3(686f, 0.03f, 475f));
            nodeLocations.Add(new Vector3(679f, 0.03f, 560f));

            road1 = RoadAutomation.CreateRoadProgrammatically(unitSystem9, ref nodeLocations);
            road1.SetLaneTotal(2);

            //Get road system, create road #2:
            nodeLocations.Clear();

            nodeLocations.Add(new Vector3(762f, 0.03f, 345f));
            nodeLocations.Add(new Vector3(744f, 0.03f, 345f));
            //Intersection1
            nodeLocations.Add(new Vector3(711f, 0.03f, 345f));

            road2 = RoadAutomation.CreateRoadProgrammatically(unitSystem9, ref nodeLocations);
            road2.SetLaneTotal(2);
            UnitTestIntersectionHelper(road1, road2, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Get road system, create road #3:
            nodeLocations.Clear();

            nodeLocations.Add(new Vector3(615f, 0.03f, 475f));
            nodeLocations.Add(new Vector3(640f, 0.03f, 475f));
            //Intersection2
            nodeLocations.Add(new Vector3(686f, 0.03f, 475f));

            road3 = RoadAutomation.CreateRoadProgrammatically(unitSystem9, ref nodeLocations);
            road3.SetLaneTotal(2);
            UnitTestIntersectionHelper(road1, road3, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Get road system, create road #4:
            nodeLocations.Clear();
            nodeLocations.Add(new Vector3(615f, 0.03f, 560f));
            nodeLocations.Add(new Vector3(640f, 0.03f, 560f));
            //Intersection3
            nodeLocations.Add(new Vector3(679f, 0.03f, 560f));
            nodeLocations.Add(new Vector3(725f, 0.03f, 560f));

            road4 = RoadAutomation.CreateRoadProgrammatically(unitSystem9, ref nodeLocations);
            road4.SetLaneTotal(2);
            UnitTestIntersectionHelper(road1, road4, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Turn updates back on and update road:
            unitSystem9.isAllowingRoadUpdates = true;
            unitSystem9.UpdateAllRoads();
        }


        /// <summary> Creates a self closed road; road loop </summary>
        public static void RoadCityMasterUnitTest10()
        {
            CleanupTest(10);

            //Create base road:
            Road road;
            List<Vector3> nodeLocations = new List<Vector3>();

            nodeLocations.Add(new Vector3(480f, 0.03f, 178f));
            nodeLocations.Add(new Vector3(412f, 0.03f, 246f));
            nodeLocations.Add(new Vector3(357f, 0.03f, 202f));
            nodeLocations.Add(new Vector3(364f, 0.03f, 140f));
            nodeLocations.Add(new Vector3(418f, 0.03f, 142f));
            nodeLocations.Add(new Vector3(412f, 0.03f, 246f));


            GameObject unitSystem10Object = new GameObject("RoadCityMasterSystem10");
            RoadSystem unitSystem10 = unitSystem10Object.AddComponent<RoadSystem>();
            unitSystem10.isAllowingRoadUpdates = false;
            road = RoadAutomation.CreateRoadProgrammatically(unitSystem10, ref nodeLocations);
            road.SetLaneTotal(2);
            UnitTestIntersectionHelper(road, road, RoadIntersection.iStopTypeEnum.None, RoadIntersection.RoadTypeEnum.NoTurnLane);

            //Turn updates back on and update road:
            unitSystem10.isAllowingRoadUpdates = true;
            unitSystem10.UpdateAllRoads();
        }
    }
#endif
}

#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
using RoadCityMaster;
#endregion


namespace RoadCityMaster
{
    /// <summary> Provides the menu items inside the editor.
    ///
    /// RM-108 cut this menu from fourteen top-level rows to five. The rule applied was: a row earns its place in
    /// Window/Road City Master only if there is nothing an author could select to reach it instead. Creating the first
    /// road system qualifies -- no RoadSystem exists yet, so no inspector can offer it. Add road, Update all roads and
    /// Help do not: all three were already buttons in <see cref="RoadSystemEditor"/>, drawn on the object they act on,
    /// so the menu rows were a second way to press the same button. The methods stay public and unchanged, because the
    /// inspector, the tests and any author's own editor script call them directly; only the attributes are gone. </summary>
    public class EditorMenu
    {
        /// <summary> Creates the road system.
        ///
        /// Keeps its menu item: this is the one action that cannot come from an inspector, because before it runs there
        /// is no RoadSystem in the scene to select. </summary>
        [MenuItem("Window/Road City Master/Create road system", false, 0)]
        public static void CreateRoadSystem()
        {
            Object[] allRoadSystemObjects = EngineIntegration.FindObjectsByType<RoadSystem>();
            int nextCount = (allRoadSystemObjects.Length + 1);
            allRoadSystemObjects = null;

            GameObject newRoadSystemObject = new GameObject("RoadCityMasterSystem" + nextCount.ToString());
            RoadSystem newRoadSystem = newRoadSystemObject.AddComponent<RoadSystem>();
            //Add road for new road system.
            newRoadSystem.AddRoad(true);

            GameObject masterIntersectionsObject = new GameObject("Intersections");
            masterIntersectionsObject.transform.parent = newRoadSystemObject.transform;
        }


        /// <summary> Add road to gameobject. Not sure if this is necessary.
        ///
        /// No menu item: RM-108. The RoadSystem inspector's own "Add road" button calls
        /// <c>roadSystem.AddRoad()</c> on the system an author has selected, which is both fewer clicks and
        /// unambiguous about which system gains the road. This entry point differs only in picking the FIRST
        /// RoadSystem in the scene for you, which is a guess in any scene holding more than one, so it is kept for
        /// scripted use and no longer offered as a command. </summary>
        public static void AddRoad()
        {
            Object[] allRoadSystemObjects = EngineIntegration.FindObjectsByType<RoadSystem>();
            if (allRoadSystemObjects != null && allRoadSystemObjects.Length == 0)
            {
                CreateRoadSystem();
                return;
            }
            else
            {
                RoadSystem firstRoadSystem = (RoadSystem)allRoadSystemObjects[0];
                Selection.activeGameObject = firstRoadSystem.AddRoad();
            }
        }


        /// <summary> Updates all roads. Used when things get out of sync.
        ///
        /// No menu item: RM-108. "Update all roads" is a button in the RoadSystem inspector. </summary>
        public static void UpdateAllRoads()
        {
            Road[] allRoadObjects = EngineIntegration.FindObjectsByType<Road>();

            int roadCount = allRoadObjects.Length;

            Road singleRoad = null;
            SplineC[] tPiggys = null;
            if (roadCount > 1)
            {
                tPiggys = new SplineC[roadCount - 1];
            }

            for (int count = 0; count < roadCount; count++)
            {
                singleRoad = allRoadObjects[count];
                if (count > 0)
                {
                    tPiggys[count - 1] = singleRoad.spline;
                }
            }

            singleRoad = allRoadObjects[0];
            if (tPiggys != null && tPiggys.Length > 0)
            {
                singleRoad.PiggyBacks = tPiggys;
            }
            singleRoad.RequestUpdate();
        }


        /// <summary> Show the help screen.
        ///
        /// No menu item: RM-108. The RoadSystem inspector draws it as a "Quick help" button beside "Manual", which is
        /// where an author reads about the object in front of them. The window itself is three paragraphs of
        /// orientation -- the full text lives in RoadCityMasterManual.html -- so a top-level menu row for it sat beside
        /// Open Manual offering strictly less. </summary>
        public static void ShowHelpWindow()
        {
            HelpWindow helpWindow = EditorWindow.GetWindow<HelpWindow>();
            helpWindow.Initialize();
        }


        /// <summary> WARNING: Only call this on an empty scene that has some terrains on it. We are not responsbile for data loss if this function is called by the user. </summary>
        [MenuItem("Window/Road City Master/Testing/Run all unit tests (caution)")]
        public static void TestProgram()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your RoadSystem 1, 6, 7, 8, 9 and 10 and will create a lot of test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTests();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 1-5 (caution)")]
        public static void Test1To5()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your first RoadSystem and will create a lot of test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest1To5();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 6 (caution)")]
        public static void Test6()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your sixth RoadSystem and will create a lot of test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest6();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 7 (caution)")]
        public static void Test7()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your seventh RoadSystem and will create a lot of test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest7();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 8 (caution)")]
        public static void Test8()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your eigth RoadSystem and will create a test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest8();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 9 (caution)")]
        public static void Test9()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your RoadCityMasterSystem9 and will create test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest9();
            }
        }


        [MenuItem("Window/Road City Master/Testing/Run unit test 10 (caution)")]
        public static void Test10()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your RoadCityMasterSystem10 and will create test roads.", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.RoadCityMasterUnitTest10();
            }
        }


        /// <summary> WARNING: Only call this on an empty scene that has some terrains on it. We are not responsbile for data loss if this function is called by the user. </summary>
        [MenuItem("Window/Road City Master/Testing/Clean up tests (caution)")]
        public static void TestCleanup()
        {
            if (EditorUtility.DisplayDialog("Warning !", "This will delete your RoadSystem 1, 6, 7, 8, 9 and 10", "OK", "Cancel"))
            {
                RoadCityMaster.Tests.UnitTests.CleanupAllTests();
            }
        }


        /// <summary> Get code line count for the RCM project </summary>
        [MenuItem("Window/Road City Master/Testing/Get line count of RCM")]
        public static void TestCodeCount()
        {
            string mainDir = System.IO.Path.Combine(System.Environment.CurrentDirectory, RoadEditorUtility.GetBasePathForIO());
            string[] files = System.IO.Directory.GetFiles(mainDir, "*.cs", System.IO.SearchOption.AllDirectories);
            int lineCount = 0;
            foreach (string file in files)
            {
                lineCount += System.IO.File.ReadAllLines(file).Length;
            }
            Debug.Log(string.Format("{0:n0}", lineCount) + " lines of code in Road & City Master.");
        }


        /// <summary> Keeps its menu item: a report has to be reachable when the tool is too broken to open an
        /// inspector, which is exactly when someone needs to send one. </summary>
        [MenuItem("Window/Road City Master/Report a Bug", false, 101)]
        public static void ReportBug()
        {
            // RM-102. This sent bug reports to the upstream project's issue tracker -- a third party maintaining a
            // tool this one has diverged from, who cannot fix code they do not have. It goes to this project now,
            // and the address lives in one place: EditorUtilities.ProjectRepositoryURL.
            EditorUtilities.OpenProjectIssues();
        }
    }
}
#endif

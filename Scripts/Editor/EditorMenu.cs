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
    /// inspector, the tests and any author's own editor script call them directly; only the attributes are gone.
    ///
    /// C-3 adds a sixth row, <i>Create city</i>, and it earns its place by the same test: a settlement is a sibling of
    /// the roads under a <c>RoadSystem</c> parent, so in a scene that has none there is nothing an author could select to
    /// reach it. Every other case is the <i>Add city</i> button on the road system's own inspector, and both press the
    /// same implementation in <c>CitySettlementFactory</c> rather than carrying a copy each. </summary>
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


        /// <summary> Creates one settlement, and the road system to hold it when the scene has none yet.
        ///
        /// This row resolves WHICH road system and nothing else; the creation itself -- the GameObject, the
        /// <c>CityAuthoring</c>, the five branches, the graph, the seed, the one undo step -- is
        /// <c>CitySettlementFactory.CreateCity</c>, the same method the inspector button presses.
        ///
        /// It refuses rather than guessing when the scene holds several road systems and none of them is selected:
        /// putting the city under the first one found would be the tool choosing a parent the author did not. A city
        /// added this way records no source road, so it carries no order until the author draws the network and sweeps
        /// it -- which the report says, because a settlement with no order is otherwise a silent surprise at bake time.
        /// </summary>
        [MenuItem("Window/Road City Master/Create city", false, 1)]
        public static void CreateCity()
        {
            RoadSystem system;
            string reason;
            if (!TryResolveSystemForCity(out system, out reason))
            {
                Debug.LogWarning("Road & City Master: " + reason + ".");
                return;
            }
            RoadCityMaster.City.CityAuthoring created;
            if (!RoadCityMaster.City.Editor.CitySettlementFactory.CreateCity(system, null,
                    RoadCityMaster.City.Editor.CitySettlementFactory.DefaultPlacementPoint(), null,
                    RoadCityMaster.City.RoadEnd.Start, true, out created, out reason))
            {
                Debug.LogWarning("Road & City Master: " + reason + ".");
                return;
            }
            Debug.Log("Road & City Master: added the settlement " + created.name + " under " + system.name
                      + ". It records no source road, so it has no order yet -- a settlement's order comes from the"
                      + " settlement role on the road at the terminus it was sited from. Draw the network, set those"
                      + " roles, and Build settlements from roads places the settlements that carry one.");
        }


        /// <summary> Show the help screen.
        ///
        /// No menu item, and the reason is not the one RM-108 gave. RM-108 deleted the menu row on the stated ground
        /// that "the RoadSystem inspector draws it as a Quick help button beside Manual" -- which was false when it was
        /// written: nothing in the tool called this method and nothing else opened <see cref="HelpWindow"/>, so the
        /// window was unreachable from the product. B-14 supplied the button the claim described, in
        /// <c>RoadSystemEditor.cs</c>, and <see cref="LegacyInspectorGate"/> case 9 now asserts both hops -- the
        /// inspector calls <c>EditorMenu.ShowHelpWindow(</c>, and this method opens the window. That makes the
        /// no-menu-row decision correct on its own terms: an author reads about the object in front of them, and the
        /// full text lives in the HTML manual that Open Manual opens. This stays the only opener. </summary>
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


        /// <summary> Which road system a menu-created settlement belongs under: the selection first, then the only one in
        /// the scene, and a new one when the scene has none. Several with nothing selected is a refusal, not a pick.
        ///
        /// The selection is asked FIRST and through <c>GetComponentInParent</c>, so selecting a road answers it as well
        /// as selecting the system -- an author working on a road is working on its system. </summary>
        private static bool TryResolveSystemForCity(out RoadSystem system, out string reason)
        {
            system = null;
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                system = selected.GetComponentInParent<RoadSystem>();
                if (system != null) { reason = string.Empty; return true; }
            }
            Object[] allRoadSystemObjects = EngineIntegration.FindObjectsByType<RoadSystem>();
            int count = allRoadSystemObjects == null ? 0 : allRoadSystemObjects.Length;
            if (count == 0)
            {
                CreateRoadSystem();
                allRoadSystemObjects = EngineIntegration.FindObjectsByType<RoadSystem>();
                count = allRoadSystemObjects == null ? 0 : allRoadSystemObjects.Length;
            }
            if (count == 1)
            {
                system = (RoadSystem)allRoadSystemObjects[0];
                reason = string.Empty;
                return true;
            }
            if (count == 0)
            {
                reason = "no road system exists and one could not be created, so there is nothing to put a settlement"
                         + " under";
                return false;
            }
            reason = "this scene holds " + count + " road systems and none of them is selected, so which one the"
                     + " settlement belongs under is the author's answer and not the tool's. Select the road system --"
                     + " or any road under it -- and press Add city on its inspector";
            return false;
        }
    }
}
#endif

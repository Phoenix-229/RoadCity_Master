#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    public class HelpWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorStyles.label.wordWrap = true;
            EditorStyles.miniLabel.wordWrap = true;

            // RM-102. This window used to be four buttons and four raw URL labels, all of them pointing at the
            // upstream project's GitHub -- a wiki, a repository and an issue tracker belonging to a third party who
            // documents a tool this one has diverged from. An author who followed them read the wrong manual and
            // filed bugs where nobody responsible for this code would see them. The manual that ships beside the
            // tool is now the only manual, and the repository links below are this project's own, written once in
            // EditorUtilities.ProjectRepositoryURL and read from there so the address cannot drift between windows.
            EditorGUILayout.LabelField(InspectorFieldRegistry.ProductDisplayName + " — help", EditorStyles.boldLabel);
            GUILayout.Space(12f);
            EditorGUILayout.LabelField("The manual ships with the tool. It covers every inspector field, what each one does to the world, and the workflows -- roads, intersections, grade separations, bridges, tunnels and the city layer.");
            GUILayout.Space(6f);

            if (GUILayout.Button("Open the manual", EditorStyles.toolbarButton, GUILayout.Width(310f)))
            {
                EditorUtilities.OpenOfflineManual();
            }

            GUILayout.Space(12f);

            EditorGUILayout.LabelField("Every inspector in the tool has its own Manual button, which opens this same file.", EditorStyles.miniLabel);

            GUILayout.Space(12f);

            EditorGUILayout.LabelField("This project's repository. A bug or a suggestion filed here reaches the people who maintain THIS code.");
            GUILayout.Space(4f);

            if (GUILayout.Button("Open the project repository", EditorStyles.toolbarButton, GUILayout.Width(310f)))
            {
                EditorUtilities.OpenProjectRepository();
            }
            EditorGUILayout.LabelField(EditorUtilities.ProjectRepositoryURL, EditorStyles.miniLabel);

            GUILayout.Space(4f);

            if (GUILayout.Button("Report a bug or suggest a feature", EditorStyles.toolbarButton, GUILayout.Width(310f)))
            {
                EditorUtilities.OpenProjectIssues();
            }
            EditorGUILayout.LabelField(EditorUtilities.ProjectRepositoryURL + "/issues", EditorStyles.miniLabel);

            GUILayout.Space(12f);
        }


        public void Initialize()
        {
            Rect rect = new Rect(340, 170, 400, 400);
            position = rect;
            Show();
            titleContent.text = "Help Info";
        }
    }
}
#endif

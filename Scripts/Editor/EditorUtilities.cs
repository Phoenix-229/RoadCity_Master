#if UNITY_EDITOR
using UnityEngine;


namespace RoadCityMaster
{
    public class EditorUtilities
    {
        /// <summary> This project's own repository. RM-102: every online link in the tool pointed at the upstream
        /// project's GitHub -- its wiki, its repository, its issue tracker -- and this system has diverged from that
        /// tool far enough that those pages describe different behaviour and its maintainers cannot act on a bug
        /// filed against code they do not have. Declared once, so moving the repository is one edit. The clone
        /// address is this same URL with <c>.git</c> on the end; a browser wants the form written here. </summary>
        public const string ProjectRepositoryURL = "https://github.com/Phoenix-229/RoadCity_Master";


        /// <summary> Opens this project's repository. </summary>
        public static void OpenProjectRepository()
        {
            Application.OpenURL(ProjectRepositoryURL);
        }


        /// <summary> Opens this project's issue tracker, where a bug in THIS tool can be acted on. </summary>
        public static void OpenProjectIssues()
        {
            Application.OpenURL(ProjectRepositoryURL + "/issues");
        }


        /// <summary> Opens the manual shipped with the tool. RM-102 corrected the file name: it still pointed at the
        /// upstream project's offline manual, a file that does not exist here, so every Manual button in every
        /// inspector opened nothing. The file is <c>RoadCityMasterManual.html</c> at the tool root, and this is the one
        /// place that names it. </summary>
        public static void OpenOfflineManual()
        {
            Application.OpenURL(System.Environment.CurrentDirectory.Replace(@"\", "/") + "/" + RoadEditorUtility.GetBasePath() + "/RoadCityMasterManual.html");
        }


        /// <summary> Loads the _texture from _path if necessary </summary>
        public static void LoadTexture<T>(ref T _texture, string _path) where T : Texture
        {
            _texture = EngineIntegration.LoadAssetFromPath<T>(_path);
        }


        public static void DrawLine(float _spacing = 4f, float _size = 1f)
        {
            //Horizontal bar
            GUILayout.Space(_spacing);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(_size));
            GUILayout.Space(_spacing);
        }
    }
}
#endif

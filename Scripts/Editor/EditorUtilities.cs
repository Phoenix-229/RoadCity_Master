// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
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
        /// inspector opened nothing.
        ///
        /// B-14: it built its own path and opened it blind. Three separate ways for the seven Manual buttons -- the
        /// most-drawn control in the tool -- to answer a click with nothing at all: the file may have been trimmed out
        /// of the project, the tool folder may have been installed somewhere other than where CurrentDirectory plus
        /// GetBasePath expects it, and a project path containing a space (this one is "Universe Builder") is not a
        /// legal URI, so the raw string handed to OpenURL could be cut at the space by the shell that receives it.
        /// <see cref="Documentation.OpenManual"/> already had all three answered -- the tool root resolved from the root
        /// asmdef's GUID, File.Exists checked, and the path turned into an encoded file:// URI -- and warns by name
        /// rather than throwing in each case. So this forwards to it instead of being a second, worse copy of it: one
        /// resolver, one existence check, one message, and the file name declared in one place. </summary>
        public static void OpenOfflineManual()
        {
            Documentation.OpenManual();
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

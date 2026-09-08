// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
#endregion


namespace RoadCityMaster
{
    public static class TerrainHistoryUtility
    {
        //http://forum.unity3d.com/threads/32647-C-Sharp-Binary-Serialization
        //http://answers.unity3d.com/questions/363477/c-how-to-setup-a-binary-serialization.html

        // Terrain history is written with BinaryFormatter, which records the namespace-qualified
        // type name. Files saved before the RoadArchitect -> RoadCityMaster rename therefore name
        // types that no longer exist, and the binder is the only place that can be repaired.
        private const string legacyNamespacePrefix = "RoadArchitect.";
        private const string namespacePrefix = "RoadCityMaster.";

        // === This is required to guarantee a fixed serialization assembly name, which Unity likes to randomize on each compile
        // Do not change this
        public sealed class VersionDeserializationBinder : SerializationBinder
        {
            public override System.Type BindToType(string assemblyName, string typeName)
            {
                if (!string.IsNullOrEmpty(assemblyName) && !string.IsNullOrEmpty(typeName))
                {
                    System.Type typeToDeserialize = null;
                    // Replace rather than StartsWith: the root type is a generic List, so the old
                    // namespace also appears inside the bracketed type argument.
                    if (typeName.Contains(legacyNamespacePrefix))
                    {
                        typeName = typeName.Replace(legacyNamespacePrefix, namespacePrefix);
                    }
                    assemblyName = System.Reflection.Assembly.GetExecutingAssembly().FullName;
                    // The following line of code returns the type.
                    typeToDeserialize = System.Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
                    return typeToDeserialize;
                }
                return null;
            }
        }


        /// <summary> Saves the Terrain History to disk </summary>
        public static void SaveTerrainHistory(List<TerrainHistoryMaker> _obj, Road _road)
        {
            string path = CheckNonAssetDirTH() + GetRoadTHFilename(ref _road);
            if (string.IsNullOrEmpty(path) || path.Length < 2)
            {
                return;
            }
            Stream stream = File.Open(path, FileMode.Create);
            BinaryFormatter bformatter = new BinaryFormatter();
            bformatter.Binder = new VersionDeserializationBinder();
            bformatter.Serialize(stream, _obj);
            _road.TerrainHistoryByteSize = (stream.Length * 0.001f).ToString("n0") + " kb";
            stream.Close();
        }


        /// <summary> Deletes the Terrain History from disk </summary>
        public static void DeleteTerrainHistory(Road _road)
        {
            string path = CheckNonAssetDirTH() + GetRoadTHFilename(ref _road);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }


        /// <summary> Loads the Terrain History from disk </summary>
        public static List<TerrainHistoryMaker> LoadTerrainHistory(Road _road)
        {
            string path = CheckNonAssetDirTH() + GetRoadTHFilename(ref _road);
            if (string.IsNullOrEmpty(path) || path.Length < 2)
            {
                return null;
            }
            if (!File.Exists(path))
            {
                return null;
            }
            List<TerrainHistoryMaker> result;
            Stream stream = File.Open(path, FileMode.Open);
            BinaryFormatter bFormatter = new BinaryFormatter();
            bFormatter.Binder = new VersionDeserializationBinder();

            result = (List<TerrainHistoryMaker>)bFormatter.Deserialize(stream);

            stream.Close();
            return result;
        }


        /// <summary> Generates the Terrain History file name </summary>
        private static string GetRoadTHFilename(ref Road _road)
        {
            string sceneName;
            
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            sceneName = sceneName.Replace("/", "");
            sceneName = sceneName.Replace(".", "");
            // Both names collapse to "RAS", new one first: the second Replace is what keeps a scene
            // saved before the rename pointing at its existing .th file on disk.
            string roadName = _road.roadSystem.transform.name
                                  .Replace("RoadCityMasterSystem", "RAS")
                                  .Replace("RoadArchitectSystem", "RAS")
                              + "-" + _road.transform.name;
            return sceneName + "-" + roadName + ".th";
        }


        /// <summary> Returns the path to the Road &amp; City Master folder where Terrain History is saved </summary>
        public static string GetDirBase()
        {
            return UnityEngine.Application.dataPath.Replace("/Assets", "/RoadCityMaster/");
        }


        /// <summary> Returns the pre-rename base path, used only by the one-time folder migration </summary>
        private static string GetLegacyDirBase()
        {
            return UnityEngine.Application.dataPath.Replace("/Assets", "/RoadArchitect/");
        }


        // Terrain history lives outside Assets/, so no .meta GUID protects it — the folder is found
        // by name, and renaming that name orphans every .th file already on disk. Move the old folder
        // across once, before anything creates the new one.
        private static bool hasCheckedLegacyDirBase = false;


        private static void MigrateLegacyDirBase()
        {
            if (hasCheckedLegacyDirBase)
            {
                return;
            }
            hasCheckedLegacyDirBase = true;

            string legacyPath = GetLegacyDirBase().TrimEnd('/');
            string path = GetDirBase().TrimEnd('/');
            if (!Directory.Exists(legacyPath) || Directory.Exists(path))
            {
                return;
            }
            try
            {
                Directory.Move(legacyPath, path);
            }
            catch (IOException exception)
            {
                UnityEngine.Debug.LogWarning("Terrain history: could not move \"" + legacyPath
                    + "\" to \"" + path + "\", so history saved before the rename stays unreachable. "
                    + exception.Message);
            }
        }


        /// <summary> Returns the path where Terrain History is saved </summary>
        public static string GetTHDir()
        {
            MigrateLegacyDirBase();

            string path = GetDirBase() + "TerrainHistory/";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }


        /// <summary> Checks if the Road &amp; City Master folder exists </summary>
        public static string CheckRoadCityMasterDirectory()
        {
            MigrateLegacyDirBase();

            string path = GetDirBase();
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            if (Directory.Exists(path))
            {
                return path + "/";
            }
            else
            {
                return "";
            }
        }


        /// <summary> Returns RoadCityMaster/TerrainHistory path or empty </summary>
        public static string CheckNonAssetDirTH()
        {
            CheckRoadCityMasterDirectory();

            string path = GetTHDir();
            if (Directory.Exists(path))
            {
                return path;
            }
            else
            {
                return "";
            }
        }
    }
}

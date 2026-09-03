using UnityEngine;


namespace RoadCityMaster
{
    public class WizardObject
    {
        public Texture2D thumb;
        public string thumbString;
        public string displayName;
        public string desc;
        public bool isDefault;
        public bool isBridge;
        public string fileName;
        public string FullPath;
        public int sortID = 0;


        public string ConvertToString()
        {
            WizardObjectLibrary WOL = new WizardObjectLibrary();
            WOL.LoadFrom(this);
            return RootUtils.GetString<WizardObjectLibrary>(WOL);
        }


        public void LoadDataFromWOL(WizardObjectLibrary _wizardObjLib)
        {
            thumbString = _wizardObjLib.thumbString;
            displayName = _wizardObjLib.displayName;
            desc = _wizardObjLib.desc;
            isDefault = _wizardObjLib.isDefault;
            fileName = _wizardObjLib.fileName;
            isBridge = _wizardObjLib.isBridge;
        }


        /// <summary> The group-preset header of a <c>.rao</c>, or null when the file holds none.
        ///
        /// The file read, the separator list and the record walk were all written out here AND in
        /// <c>RoadUtility.LoadNodeObjects</c> — two copies of the same four separators and the same unguarded
        /// <c>File.ReadAllText</c>, which is how one moved library came out of one path as an <c>IOException</c> through
        /// OnGUI and out of the other as a preset that simply never appeared. Both now go through
        /// <c>RoadUtility.ReadRaoRecords</c>, and the record walk lives once in <c>RaoFormat.TryReadFile</c>.
        ///
        /// A file with no header is NOT an error and is NOT logged: <c>Wizard.LoadGroupObjs</c> hands every <c>.rao</c>
        /// in the directory to this method in order to find out which of them are group presets, so a plain section
        /// library answering "no header here" is the system working. A file that cannot be READ, or one whose header is
        /// damaged, is refused by name. </summary>
        public static WizardObject LoadFromLibrary(string _path)
        {
            string reason;
            WizardObjectLibrary WOL;
            if (!WizardObjectLibrary.WOLFromFile(_path, out WOL, out reason))
            {
                if (reason != null)
                {
                    Debug.LogError(RaoFormat.Refusal(System.IO.Path.GetFileName(_path), -1, reason));
                }
                return null;
            }

            WizardObject WO = new WizardObject();
            WO.LoadDataFromWOL(WOL);
            return WO;
        }


        [System.Serializable]
        public class WizardObjectLibrary : IRaoRecord
        {
            /// <summary> The `.rao` format stamp — see <c>RaoFormat</c>. Zero on purpose; `LoadFrom` stamps it on save.
            /// See the note on <c>SplinatedMeshMaker.SplinatedMeshLibraryMaker.formatVersion</c>. </summary>
            public int formatVersion = 0;

            public string thumbString;
            public string displayName;
            public string desc;
            public bool isDefault;
            public bool isBridge;
            public string fileName;


            /// <summary> The stamp the file this record came from declared — see <c>IRaoRecord</c>. </summary>
            public int RaoVersion()
            {
                return formatVersion;
            }


            public void LoadFrom(WizardObject _wizardObj)
            {
                formatVersion = RaoFormat.CurrentVersion;
                thumbString = _wizardObj.thumbString;
                displayName = _wizardObj.displayName;
                desc = _wizardObj.desc;
                isDefault = _wizardObj.isDefault;
                fileName = _wizardObj.fileName;
                isBridge = _wizardObj.isBridge;
            }


            /// <summary> One record of a `.rao` read as a group-preset header, or null when the record is not one.
            /// <paramref name="_reason"/> is null for "different kind of record" and non-null only for a genuine
            /// failure — see <c>RaoFormat</c>. </summary>
            public static WizardObjectLibrary WOLFromData(string _data, string _origin, out string _reason)
            {
                WizardObjectLibrary WOL;
                if (!RaoFormat.TryRead<WizardObjectLibrary>(_data, _origin,
                    RaoFormat.RootWizard, out WOL, out _reason))
                {
                    return null;
                }
                return WOL;
            }


            /// <summary> A whole file read as a group preset. False with a <c>null</c> reason means "this <c>.rao</c>
            /// is not a group preset", which is the ordinary answer while scanning a directory; false with a reason
            /// means the file could not be read or its header is damaged. </summary>
            public static bool WOLFromFile(string _path, out WizardObjectLibrary _value, out string _reason)
            {
                return RaoFormat.TryReadFile<WizardObjectLibrary>(_path, RaoFormat.RootWizard, false,
                    out _value, out _reason);
            }
        }
    }
}

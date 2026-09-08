// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> What the project's own package files say is installed, as opposed to what the port assumed.
    /// <see cref="found"/> false means nothing could be read, and <see cref="problem"/> then says why in a sentence
    /// an author can act on. <see cref="sourceFile"/> is PROJECT-RELATIVE: it is a claim about which file was read
    /// and it has to stay meaningful on a machine that is not this one (C-04). </summary>
    public struct InstalledUrp
    {
        public bool found;
        public string version;
        public string sourceFile;
        public string problem;
    }

    /// <summary> One fact read out of the project, with the file and line it was read from. </summary>
    public sealed class PipelineFact
    {
        public readonly string setting;
        public readonly string value;
        public readonly string source;

        public PipelineFact(string setting, string value, string source)
        {
            this.setting = setting;
            this.value = value;
            this.source = source;
        }
    }


    /// <summary> A gotcha this project has already paid for once, recorded so RM-68 starts from facts. </summary>
    public sealed class PortGotcha
    {
        public readonly string rule;
        public readonly string cost;

        public PortGotcha(string rule, string cost)
        {
            this.rule = rule;
            this.cost = cost;
        }
    }


    /// <summary> A material path something loads that did NOT resolve, and the fix that was applied to it. Found by the
    /// audit rather than looked for: a path-loaded material that does not resolve returns null, and a null material
    /// renders magenta -- which is the same symptom a Built-in shader produces under URP, from a different cause. Every
    /// entry here is now RESOLVED: `path` is the path that resolves today, `fix` says what was done, and RCMSHADER
    /// requires the file to exist, so an entry cannot sit in this table describing work that was not done. </summary>
    public sealed class MissingMaterialReference
    {
        public readonly string path;
        public readonly string loadedBy;
        public readonly string note;
        public readonly string fix;

        public MissingMaterialReference(string path, string loadedBy, string note, string fix)
        {
            this.path = path;
            this.loadedBy = loadedBy;
            this.note = note;
            this.fix = fix;
        }
    }
    /// <summary> RM-67's second half: the project as it actually is, not as the port assumes it to be.
    ///
    /// Every value here was read out of the project's own files and carries the line it came from, because "URP" is not
    /// a specification -- a port validated against one URP version can fail on another, and stripping can remove a
    /// variant a shader relies on and leave a material that works in the editor and not in a build (Q-20).
    /// </summary>
    public static class PipelineAudit
    {
        /// <summary> The recorded target for the whole port: RM-67, RM-68 and RM-91 were all written against this
        /// URP. RM-92 is what stops it being a claim -- <see cref="PipelineVersionGate"/> reads the version the
        /// project actually has and fails if the major differs. </summary>
        public const string TargetUrpVersion = "17.5.0";

        /// <summary> The package whose version decides whether this port is correct. Matched with its closing quote
        /// included, because `com.unity.render-pipelines.universal-config` is a DIFFERENT package that shares this
        /// string as a prefix and appears beside it in `packages-lock.json`. A prefix match would read the config
        /// package's version and report it as URP's. </summary>
        public const string UrpPackageId = "com.unity.render-pipelines.universal";

        /// <summary> Where the version is read from, project-relative and in the order they are consulted: the
        /// authored dependency first, then the resolved lock file. Both are files the project already carries, so
        /// establishing the version needs no network call and no Package Manager round-trip -- which matters because
        /// a gate that reached the network would fail in a batchmode build on a machine with no route out. </summary>
        public const string ManifestRelativePath = "Packages/manifest.json";
        public const string LockRelativePath = "Packages/packages-lock.json";

        /// <summary> The line `Packages/manifest.json` carries the URP dependency on, 1-based, as cited by
        /// <see cref="BuildFacts"/>. Typed here so the citation can be checked rather than trusted: if someone adds a
        /// dependency above it, the citation is wrong and the gate says so instead of the document lying quietly.
        /// </summary>
        public const int ManifestUrpLine = 3;

        /// <summary> Pulls the URP version out of a package file's TEXT. A pure string function on purpose: the
        /// version is a JSON string value, and reading it this way means the parse can be proved against synthetic
        /// text with no project on disk, no Package Manager and no network -- which is the only way a gate can
        /// establish that the reader is right rather than merely that it agreed with itself.
        ///
        /// Deliberately not `JsonUtility`: it needs a serialisable type per shape, and `manifest.json` and
        /// `packages-lock.json` nest the version differently. Scanning for the key is shape-independent, and the key
        /// is matched WITH its closing quote so the `-config` package cannot be mistaken for URP itself.
        ///
        /// Returns "" when the key is absent, which is a real answer and not an error: the manifest may not name URP
        /// at all if it arrived as an indirect dependency, and the lock file is then the place to look. </summary>
        public static string ParseUrpVersion(string _json)
        {
            if (string.IsNullOrEmpty(_json)) { return ""; }

            string key = "\"" + UrpPackageId + "\"";
            int at = _json.IndexOf(key, System.StringComparison.Ordinal);
            if (at < 0) { return ""; }

            // From the key, the version is the next quoted string after a colon. In `manifest.json` that is the
            // value itself; in `packages-lock.json` the key opens an object whose first member is "version", and
            // walking quote-pairs reaches the same place without the reader needing to know which file it holds.
            int cursor = at + key.Length;
            for (int guard = 0; guard < 8; guard++)
            {
                int open = _json.IndexOf('"', cursor);
                if (open < 0) { return ""; }
                int close = _json.IndexOf('"', open + 1);
                if (close < 0) { return ""; }

                string token = _json.Substring(open + 1, close - open - 1);
                cursor = close + 1;

                if (token == "version") { continue; }        // the lock file's member name, not its value
                if (MajorOf(token) >= 0) { return token; }   // a version-shaped token is the answer
                return "";                                   // anything else means the shape changed
            }
            return "";
        }

        /// <summary> The major version as an integer, or -1 when the string is not version-shaped. The major is what
        /// decides whether a port is correct: `Core.hlsl` include paths, the lens-flare asset type and the
        /// shadow-caster declarations all move between URP majors, so 17.5 to 17.6 is a patch this tool survives and
        /// 17 to 18 is a port that has to be redone. </summary>
        public static int MajorOf(string _version)
        {
            if (string.IsNullOrEmpty(_version)) { return -1; }

            int end = _version.IndexOf('.');
            string head = end < 0 ? _version : _version.Substring(0, end);
            if (head.Length == 0) { return -1; }

            int major;
            if (!int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out major)) { return -1; }
            return major;
        }

        /// <summary> The oldest engine this tool supports, and the reason the legacy scripts could stop carrying
        /// version guards at all. Every call the RM-104 sweep settled on -- <c>PhysicsMaterial</c>,
        /// <c>FindObjectsByType</c>, <c>StaticEditorFlags.ContributeGI</c>, <c>hierarchyChanged</c>,
        /// <c>detailResolution</c> -- is the Unity 6 spelling, so an older editor does not merely warn, it fails to
        /// compile. Stating the floor as 6000.4 rather than the 2018 the vendored code assumed is what lets those
        /// guards go: a `#if` that can only ever take one branch is dead code that names removed APIs.
        ///
        /// The project's own editor is newer than the floor (6000.5.2f1), which is the point -- the floor is the
        /// oldest engine the code is CORRECT on, not the one it happens to be opened in. </summary>
        public const int MinimumEngineMajor = 6000;
        public const int MinimumEngineMinor = 4;
        public const string MinimumEngineVersion = "6000.4";


        /// <summary> Splits an engine version string such as `6000.5.2f1` into its major and minor and says whether it
        /// is at or above the supported floor. Returns false with both numbers -1 when the string is not
        /// version-shaped, because an unreadable version is not evidence of a supported one. Text in, numbers out, so
        /// the comparison can be proved against strings with no editor of that version to hand. </summary>
        public static bool EngineMeetsFloor(string _unityVersion, out int _major, out int _minor)
        {
            _major = -1;
            _minor = -1;
            if (string.IsNullOrEmpty(_unityVersion)) { return false; }

            int firstDot = _unityVersion.IndexOf('.');
            if (firstDot < 1) { return false; }

            if (!int.TryParse(_unityVersion.Substring(0, firstDot), NumberStyles.None,
                CultureInfo.InvariantCulture, out _major))
            {
                _major = -1;
                return false;
            }

            int cursor = firstDot + 1;
            int end = cursor;
            while (end < _unityVersion.Length && _unityVersion[end] >= '0' && _unityVersion[end] <= '9') { end++; }
            if (end == cursor) { _major = -1; return false; }

            if (!int.TryParse(_unityVersion.Substring(cursor, end - cursor), NumberStyles.None,
                CultureInfo.InvariantCulture, out _minor))
            {
                _major = -1;
                _minor = -1;
                return false;
            }

            if (_major > MinimumEngineMajor) { return true; }
            if (_major < MinimumEngineMajor) { return false; }
            return _minor >= MinimumEngineMinor;
        }


        /// <summary> Reads the URP version the project actually has, from the files listed above, given the project
        /// root. The root is PASSED IN rather than resolved here so the reader can be pointed at a fixture directory
        /// by a test; the caller derives the real one from <see cref="CompileLog.ProjectRoot"/>, which is
        /// itself derived from `Application.dataPath` and never typed (C-04). </summary>
        public static InstalledUrp ReadInstalledUrp(string _projectRoot)
        {
            InstalledUrp result = new InstalledUrp();
            result.found = false;
            result.version = "";
            result.sourceFile = "";
            result.problem = "";

            if (string.IsNullOrEmpty(_projectRoot))
            {
                result.problem = "the project root did not resolve, so no package file could be located";
                return result;
            }

            string[] candidates = { ManifestRelativePath, LockRelativePath };
            List<string> tried = new List<string>();

            for (int i = 0; i < candidates.Length; i++)
            {
                string full = Path.Combine(_projectRoot, candidates[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                {
                    tried.Add(candidates[i] + " (not present)");
                    continue;
                }

                string version = ParseUrpVersion(File.ReadAllText(full));
                if (version.Length == 0)
                {
                    tried.Add(candidates[i] + " (present, but names no " + UrpPackageId + " version)");
                    continue;
                }

                result.found = true;
                result.version = version;
                result.sourceFile = candidates[i];
                return result;
            }

            result.problem = "no URP version could be read. Tried, in order: " + string.Join("; ", tried.ToArray())
                + ". A URP-only tool (C-03) cannot establish that its shaders and its lens flare target the "
                + "installed pipeline if the pipeline's version is unreadable.";
            return result;
        }

        /// <summary> URP's Lit shader GUID, read from the package's own
        /// `Shaders/Lit.shader.meta:2`. It is typed here because a gate that reads raw `.mat` text needs the literal
        /// rather than a Shader object; `RCMSHADER` cross-checks this constant against what
        /// `Shader.Find("Universal Render Pipeline/Lit")` actually resolves to, so a stale literal FAILS instead of
        /// quietly passing. Nothing here mints a GUID: every material this port writes gets its own from Unity on
        /// import. </summary>
        public const string UrpLitShaderGuid = "933532a4fcc9baf4fa0491de14d08ed7";

        /// <summary> The name every ported material resolves through, so the GUID above is never the only route. </summary>
        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        /// <summary> The engine's own GUID for the Built-in resource shaders. `fileID: 46` inside it is Standard and
        /// `fileID: 45` is Standard (specular setup); both draw magenta under URP, which is the first of RM-67's two
        /// causes. No ported material may still name it. </summary>
        public const string BuiltInResourceShaderGuid = "0000000000000000f000000000000000";

        private static List<PipelineFact> _facts;
        private static List<PortGotcha> _gotchas;
        private static List<MissingMaterialReference> _missing;

        public static IList<PipelineFact> Facts()
        {
            if (_facts == null) { _facts = BuildFacts(); }
            return new List<PipelineFact>(_facts);
        }

        public static IList<PortGotcha> Gotchas()
        {
            if (_gotchas == null) { _gotchas = BuildGotchas(); }
            return new List<PortGotcha>(_gotchas);
        }

        public static IList<MissingMaterialReference> MissingReferences()
        {
            if (_missing == null) { _missing = BuildMissing(); }
            return new List<MissingMaterialReference>(_missing);
        }

        private static List<PipelineFact> BuildFacts()
        {
            List<PipelineFact> list = new List<PipelineFact>();
            list.Add(new PipelineFact("Universal render pipeline package", TargetUrpVersion,
                "`Packages/manifest.json:3` and `Packages/packages-lock.json:55`"));
            list.Add(new PipelineFact("URP config package", "com.unity.render-pipelines.universal-config "
                + TargetUrpVersion, "`Packages/packages-lock.json:64` to `:65`"));
            list.Add(new PipelineFact("Render pipeline asset in force",
                "`Assets/UniverseCreator/Settings/UC_URP_Pipeline.asset`",
                "`ProjectSettings/GraphicsSettings.asset:49`, `m_CustomRenderPipeline` guid "
                + "`7a2f2dcaa3b8d5d47a898e35c7cdfbd2`"));
            list.Add(new PipelineFact("Renderer", "Universal renderer, `m_RendererType: 1`",
                "`UC_URP_Pipeline.asset:17`"));
            list.Add(new PipelineFact("SRP batcher", "enabled, `m_UseSRPBatcher: 1`", "`UC_URP_Pipeline.asset:72`"));
            list.Add(new PipelineFact("MSAA", "`m_MSAA: 1`", "`UC_URP_Pipeline.asset:28`"));
            list.Add(new PipelineFact("Terrain holes", "supported, `m_SupportsTerrainHoles: 1`",
                "`UC_URP_Pipeline.asset:25`"));
            list.Add(new PipelineFact("Main light shadows",
                "supported at 2048, `m_MainLightShadowsSupported: 1`", "`UC_URP_Pipeline.asset:45` to `:46`"));
            list.Add(new PipelineFact("Additional light shadows",
                "NOT supported, `m_AdditionalLightShadowsSupported: 0`", "`UC_URP_Pipeline.asset:49`"));
            list.Add(new PipelineFact("Shadow distance and cascades",
                "50 m, one cascade, `m_ShadowDistance: 50` and `m_ShadowCascadeCount: 1`",
                "`UC_URP_Pipeline.asset:57` to `:58`"));
            list.Add(new PipelineFact("Strip unused shader variants", "ON, `m_StripUnusedVariants: 1`",
                "`Assets/UniversalRenderPipelineGlobalSettings.asset:23`"));
            list.Add(new PipelineFact("Strip runtime debug shaders", "ON, `m_StripRuntimeDebugShaders: 1`",
                "`Assets/UniversalRenderPipelineGlobalSettings.asset:19`"));
            list.Add(new PipelineFact("Strip screen coordinate override variants",
                "ON, `m_StripScreenCoordOverrideVariants: 1`",
                "`Assets/UniversalRenderPipelineGlobalSettings.asset:24`"));
            list.Add(new PipelineFact("Strip unused post-processing variants",
                "off, `m_StripUnusedPostProcessingVariants: 0`",
                "`Assets/UniversalRenderPipelineGlobalSettings.asset:22`"));
            list.Add(new PipelineFact("Built-in stripping settings",
                "all left automatic: `m_LightmapStripping: 0`, `m_FogStripping: 0`, `m_InstancingStripping: 0`, "
                + "`m_BrgStripping: 0`", "`ProjectSettings/GraphicsSettings.asset:55` to `:58`"));
            list.Add(new PipelineFact("Always-included shaders",
                "three, all from Unity's built-in resources; none of the tool's own",
                "`ProjectSettings/GraphicsSettings.asset:29` to `:32`"));
            return list;
        }

        private static List<MissingMaterialReference> BuildMissing()
        {
            List<MissingMaterialReference> list = new List<MissingMaterialReference>();
            list.Add(new MissingMaterialReference("Materials/RoadIntersection.mat",
                "`Scripts/Intersections.cs:639`, through `RoadEditorUtility.SetRoadMaterial`",
                "the intersection surface itself. Nothing of that name existed under `Materials/`, so the renderer was "
                + "given null on every intersection build",
                "AUTHORED as URP Lit opaque against the asphalt recipe -- `Textures/Road1Diffuse` and "
                + "`Road1Normal` at 4x4, `_BaseColor` 0.7238806 grey copied from `Materials/Road1.mat` so the "
                + "intersection reads as the same asphalt as the road that meets it, smoothness 0.2, metallic 0. No "
                + "`.meta` was hand-written: Unity mints the GUID on import"));
            list.Add(new MissingMaterialReference("Materials/InterText.mat",
                "`Scripts/Intersections.cs:777`, through `RoadEditorUtility.SetRoadMaterial`",
                "the four `outer` apron quads that extend each intersection into its approach legs -- built by "
                + "`CreateIntersectionMeshOuterInternal`, UV 0..1 per quad. Absent, so the apron was built and then "
                + "rendered with no material",
                "AUTHORED as URP Lit opaque on the same asphalt recipe, but at `Road1.mat`'s own 4x2 albedo and 8x4 "
                + "normal tiling, because the apron runs ALONG each leg and must not show a tiling seam where it meets "
                + "the road surface"));
            list.Add(new MissingMaterialReference("Materials/InterMainPlate1.mat",
                "`Scripts/RoadConstructorBufferMaker.cs:1650` and `:1677`",
                "the intersection main plate, both the back and front mesh halves. Absent",
                "AUTHORED as URP Lit opaque on the asphalt recipe at 4x4, identical to the intersection surface, "
                + "because the plate and the surface are the same pavement seen from two mesh halves"));
            list.Add(new MissingMaterialReference("Materials/Markers/RoadDetailOverlay1.mat",
                "`Editor/Library/ExtrudedObjects/ESOSidewalk.rao:9`, `<SplinatedMaterial2String>`",
                "a WRONG path rather than a missing asset: the entry named `Materials/RoadDetailOverlay1.mat` while the "
                + "file has always been at `Materials/Markers/RoadDetailOverlay1.mat`. RM-29's sidewalk wiring loads "
                + "this entry, so it got a null second material -- the one item RM-66 marked wire up meeting the one "
                + "path that was wrong",
                "FIXED IN PLACE: the `.rao` entry now names `Assets/RoadCityMaster/Materials/Markers/"
                + "RoadDetailOverlay1.mat`. No asset was authored and none moved, because the asset was never the "
                + "problem -- one library string was"));
            return list;
        }

        private static List<PortGotcha> BuildGotchas()
        {
            List<PortGotcha> list = new List<PortGotcha>();
            list.Add(new PortGotcha(
                "A URP shader block opens `HLSLPROGRAM` and closes `ENDHLSL`. A `CGPROGRAM` / `ENDCG` pair is a "
                + "Built-in tell, and all ten shaders in this tool have one",
                "the whole file fails to compile, and the error names the closing token rather than the opening one"));
            list.Add(new PortGotcha(
                "A pass name may not be empty. `Name \"\"` is a PARSE error, not a warning",
                "the shader is dropped and every material on it renders magenta, which reads as a pipeline problem"));
            list.Add(new PortGotcha(
                "A custom `ShadowCaster` pass must declare its own vertex and fragment entry points and pragma set; "
                + "there is no Built-in pass to inherit",
                "shadows disappear silently -- the material renders correctly and casts nothing"));
            list.Add(new PortGotcha(
                "A cutout material needs its keyword and its surface state set together -- `_ALPHATEST_ON` alone is "
                + "not enough in URP, which also reads `_Surface` and the render queue override",
                "the material renders opaque, so 37 of the 74 marking materials would lose their cutout"));
            return list;
        }

        public static string FactsTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Setting | Value in this project | Read from |\n");
            builder.Append("| --- | --- | --- |\n");
            IList<PipelineFact> all = Facts();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| ").Append(all[i].setting).Append(" | ").Append(all[i].value)
                       .Append(" | ").Append(all[i].source).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }

        public static string GotchasTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Rule | What it costs when rediscovered |\n");
            builder.Append("| --- | --- |\n");
            IList<PortGotcha> all = Gotchas();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| ").Append(all[i].rule).Append(" | ").Append(all[i].cost).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }

        public static string MissingTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Path loaded | Loaded by | What was actually wrong | Fix applied |\n");
            builder.Append("| --- | --- | --- | --- |\n");
            IList<MissingMaterialReference> all = MissingReferences();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| `").Append(all[i].path).Append("` | ").Append(all[i].loadedBy)
                       .Append(" | ").Append(all[i].note).Append(" | ").Append(all[i].fix).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }
    }
}
#endif

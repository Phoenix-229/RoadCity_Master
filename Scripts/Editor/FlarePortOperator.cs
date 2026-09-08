#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-68's third file kind: the lens flare. `Flares/SodiumBulb.flare` is a Built-in `--- !u!121 Flare`,
    /// and URP draws nothing at all for it -- the field it was assigned to, `Light.flare`, is dead under a Scriptable
    /// Render Pipeline. There is no in-place edit that fixes that, because the asset CLASS is wrong, not its contents.
    ///
    /// So this operator mints the URP asset the flare has to become: a `LensFlareDataSRP` at `Flares/SodiumBulb.asset`,
    /// carrying the one element <see cref="SodiumFlare"/> defines -- the Built-in file's geometry, with the LED
    /// luminaire's cited colour temperature in place of its sodium emitter. Unity creates it, which is the only reason
    /// this is an operator and not a hand-written YAML file -- a `LensFlareDataSRP` asset references the package's
    /// script by GUID, and hand-authoring that reference is exactly the kind of GUID forgery RM-68 forbids.
    ///
    /// Re-running it is safe and is the intended way to change the flare: the asset is loaded and rewritten in place
    /// when it already exists, so its own GUID and `.meta` survive, and `LensFlareComponentSRP` references on lights
    /// already built into a scene keep resolving.
    ///
    /// The Built-in `.flare` and its `.tga` are left on disk. They are the record of what the sodium fixture was --
    /// the source of every GEOMETRY number in <see cref="SodiumFlare"/>, and of the amber that class documents
    /// replacing -- and deleting them would destroy the evidence for both the port and the emitter change. Nothing
    /// loads them any more: the one line that did, `IntersectionObjects.AdjustLightPrefab`, now attaches the URP
    /// component instead. </summary>
    public static class FlarePortOperator
    {
        private const string Prefix = "RCMFLARE";

        // Setup, not a `Materials` submenu of its own. RM-108 moved the two URP material-port rows into Setup and left
        // this one behind, which produced a submenu holding exactly one item -- and its priority, 402, already sat in
        // the same contiguous run as the two it was separated from (400 and 401), so the split was an oversight rather
        // than a grouping. Folding it back makes 400/401/402 one run under one heading and removes a submenu the
        // manual would otherwise have to describe.
        private const string MenuMint = "Window/Road City Master/Setup/Port the lens flare to URP";

        [MenuItem(MenuMint, false, 402)]
        public static void MintFromMenu()
        {
            string report;
            bool ok = Mint(out report);
            Debug.Log(report);
            if (!ok) { Debug.LogWarning(Prefix + " the flare port reported failures; see the report above."); }
        }

        /// <summary> Batchmode entry point. </summary>
        public static void MintBatch()
        {
            string report;
            bool ok = Mint(out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary> The tool's `Flares` folder, with separators `AssetDatabase` accepts. </summary>
        public static string FlaresFolder()
        {
            string root = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(root)) { return ""; }
            return root.Replace('\\', '/') + "/Flares";
        }

        /// <summary> Where the URP data asset lives. Empty when the tool's base path cannot be resolved. </summary>
        public static string DataAssetPath()
        {
            string root = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(root)) { return ""; }
            return root.Replace('\\', '/') + SodiumFlare.DataAssetRelativePath;
        }

        /// <summary> The flare image, unchanged by the port. </summary>
        public static string TexturePath()
        {
            string root = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(root)) { return ""; }
            return root.Replace('\\', '/') + SodiumFlare.TextureRelativePath;
        }

        /// <summary> Creates the URP flare data asset, or rewrites the one already there, and then reads it back off
        /// disk and checks it. Reading it back matters: a `ScriptableObject` that was never serialised would pass every
        /// assertion in memory and still be an empty asset on disk. </summary>
        public static bool Mint(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder builder = new StringBuilder();
            builder.Append(Prefix).Append(" BEGIN\n");

            string assetPath = DataAssetPath();
            string texturePath = TexturePath();

            if (string.IsNullOrEmpty(assetPath))
            {
                failures.Add("the tool's base path did not resolve, so no flare asset could be minted");
            }
            else
            {
                // The legacy image is no longer drawn: the LED glow is procedural, so its absence is not a failure.
                // It is still loaded and reported, because it is the sodium asset's record and the legacy `.flare`
                // still references it -- if it ever vanishes, that is worth seeing in the report, not worth failing.
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                builder.Append(Prefix).Append(texture == null ? " note    legacy image absent at " : " note    legacy image on disk (not drawn) at ")
                    .Append(texturePath).Append('\n');

                LensFlareDataSRP data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(assetPath);
                bool created = data == null;
                if (created)
                {
                    data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                    SodiumFlare.Apply(data, texture);
                    AssetDatabase.CreateAsset(data, assetPath);
                }
                else
                {
                    SodiumFlare.Apply(data, texture);
                    EditorUtility.SetDirty(data);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                builder.Append(Prefix).Append(created ? " minted  " : " rewrote ").Append(assetPath).Append('\n');
                VerifyOnDisk(assetPath, builder, failures);
            }

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append(Prefix).Append(" FAIL ").Append(failures[i]).Append('\n');
            }
            builder.Append(Prefix).Append(" result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(Prefix).Append(" END\n");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Reads the minted asset back and asserts the one element survived the round trip, field by field,
        /// against the constants <see cref="SodiumFlare"/> owns: the geometry the Built-in file described, and the LED
        /// luminaire's cited colour temperature that replaced its amber emitter. </summary>
        private static void VerifyOnDisk(string _assetPath, StringBuilder _builder, List<string> _failures)
        {
            LensFlareDataSRP data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(_assetPath);
            if (data == null)
            {
                _failures.Add("nothing loads as a LensFlareDataSRP at " + _assetPath + " after saving");
                return;
            }

            if (data.elements == null || data.elements.Length != 1)
            {
                int count = data.elements == null ? 0 : data.elements.Length;
                _failures.Add("the asset has " + count.ToString(CultureInfo.InvariantCulture)
                    + " elements; the Built-in flare had exactly one");
                return;
            }

            LensFlareDataElementSRP element = data.elements[0];
            if (element == null)
            {
                _failures.Add("the asset's single element is null");
                return;
            }

            // Every per-property comparison lives in SodiumFlare.VerifyElement, beside the constants it compares
            // against. What is asked HERE is narrower than what the gate asks with the same call: this element was
            // just serialised to disk and read back, so a disagreement means the round-trip lost something, not that
            // the builder is wrong. What stays local is the asset-shaped part above -- that something loads at all,
            // and that it holds exactly the one element the Built-in file had.
            SodiumFlare.VerifyElement(element, _failures);

            _builder.Append(Prefix).Append(" element circle  scale ")
                .Append(element.uniformScale.ToString("0.####", CultureInfo.InvariantCulture))
                .Append("  tint ").Append(element.tint.ToString())
                .Append("  additive  no light-colour modulation  no auto-rotate\n");
        }


    }
}
#endif

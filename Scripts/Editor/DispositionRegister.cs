// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> What a search for references actually returned. <c>NotApplicable</c> is a real answer, not a gap: a
    /// static class cannot be serialised into a scene, so that search cannot run on it -- but the reason it cannot is
    /// recorded, because "not applicable" typed without a reason is how an unrun search hides. </summary>
    public enum SearchOutcome
    {
        NoReference = 0,
        Referenced = 1,
        NotApplicable = 2,
    }

    /// <summary> RM-66's three dispositions. There is deliberately no fourth for "delete later without a reason". </summary>
    public enum Disposition
    {
        Remove = 0,
        WireUp = 1,
        KeepWithReason = 2,
    }

    public enum ItemKind
    {
        Type = 0,
        Method = 1,
        Field = 2,
        Asset = 3,
    }

    /// <summary> One flagged item, its four searches with the evidence behind each, and the judgement. </summary>
    public sealed class DispositionEntry
    {
        public readonly string item;
        public readonly ItemKind kind;
        public readonly string path;
        public readonly SearchOutcome code;
        public readonly string codeEvidence;
        public readonly SearchOutcome scenePrefab;
        public readonly string scenePrefabEvidence;
        public readonly SearchOutcome serialised;
        public readonly string serialisedEvidence;
        public readonly SearchOutcome stringOrName;
        public readonly string stringEvidence;
        public readonly Disposition disposition;
        public readonly string reason;

        /// <summary> The task that CARRIED OUT this disposition, or null while it is only recorded. One string rather
        /// than a bool beside it, because "performed by nobody" is not a state a decision record should be able to
        /// hold. Only a <c>Remove</c> may name one; <see cref="DispositionGate"/> case 6 asserts that, and asserts the
        /// tree agrees -- a performed removal's member must be GONE from the file that used to hold it. </summary>
        public readonly string performedBy;

        public DispositionEntry(string item, ItemKind kind, string path,
            SearchOutcome code, string codeEvidence,
            SearchOutcome scenePrefab, string scenePrefabEvidence,
            SearchOutcome serialised, string serialisedEvidence,
            SearchOutcome stringOrName, string stringEvidence,
            Disposition disposition, string reason)
            : this(item, kind, path, code, codeEvidence, scenePrefab, scenePrefabEvidence, serialised,
                serialisedEvidence, stringOrName, stringEvidence, disposition, reason, null)
        {
        }

        public DispositionEntry(string item, ItemKind kind, string path,
            SearchOutcome code, string codeEvidence,
            SearchOutcome scenePrefab, string scenePrefabEvidence,
            SearchOutcome serialised, string serialisedEvidence,
            SearchOutcome stringOrName, string stringEvidence,
            Disposition disposition, string reason, string performedBy)
        {
            this.item = item;
            this.kind = kind;
            this.path = path;
            this.code = code;
            this.codeEvidence = codeEvidence;
            this.scenePrefab = scenePrefab;
            this.scenePrefabEvidence = scenePrefabEvidence;
            this.serialised = serialised;
            this.serialisedEvidence = serialisedEvidence;
            this.stringOrName = stringOrName;
            this.stringEvidence = stringEvidence;
            this.disposition = disposition;
            this.reason = reason;
            this.performedBy = performedBy;
        }

        /// <summary> True once the removal has actually been carried out in the tree. </summary>
        public bool Performed
        {
            get { return !string.IsNullOrEmpty(performedBy); }
        }

        /// <summary> True when no search found a reference. A <c>NotApplicable</c> is neither a hit nor a miss. </summary>
        public bool NothingFoundAReference
        {
            get
            {
                return code != SearchOutcome.Referenced && scenePrefab != SearchOutcome.Referenced
                    && serialised != SearchOutcome.Referenced && stringOrName != SearchOutcome.Referenced;
            }
        }

        /// <summary> The two searches that must have actually RUN before anything may be removed: a code search and a
        /// string-or-name search. Neither can be "not applicable" for any item that exists in the tree. </summary>
        public bool BothDecisiveSearchesRan
        {
            get
            {
                return code != SearchOutcome.NotApplicable && stringOrName != SearchOutcome.NotApplicable;
            }
        }

        public SearchOutcome[] Outcomes()
        {
            return new SearchOutcome[] { code, scenePrefab, serialised, stringOrName };
        }

        public string[] Evidence()
        {
            return new string[] { codeEvidence, scenePrefabEvidence, serialisedEvidence, stringEvidence };
        }
    }

    /// <summary> RM-66's disposition list: every item RM-49's passes flagged as unreferenced, each with the FOUR
    /// searches recorded and a judgement of <c>remove</c>, <c>wire up</c> or <c>keep with reason</c>.
    ///
    /// The four searches are not ceremony. This tree has both failure modes in it already. `Prefabs/Sidewalk.prefab`
    /// has zero GUID references anywhere under `Assets/` and would be deleted by any reference sweep, yet
    /// `Editor/Library/ExtrudedObjects/ESOSidewalk.rao` names it as an asset-path STRING; and the `.rao` libraries are
    /// discovered by filename convention (`"ESO" + name + ".rao"`), so nothing anywhere references the library file
    /// itself. Meanwhile RM-29's sidewalk is the opposite mistake waiting to happen: present, unreferenced by code, and
    /// a feature an author would ask for -- so the disposition is to wire it up, not to delete it.
    ///
    /// This register RECORDS decisions, and records which of them have been CARRIED OUT: <c>performedBy</c> names the
    /// task that did it, and nothing else in the register moves when it is set. The removals stay a separate, revertible
    /// change per disposition group, which is why <see cref="DispositionGate"/> asserts that every named path still
    /// exists -- and, for the ones already carried out, that what was removed is really gone. </summary>
    public static class DispositionRegister
    {
        private static List<DispositionEntry> _entries;

        public static IList<DispositionEntry> All()
        {
            if (_entries != null) { return new List<DispositionEntry>(_entries); }
            List<DispositionEntry> e = new List<DispositionEntry>();

            e.Add(new DispositionEntry("Prefabs/Sidewalk.prefab", ItemKind.Asset, "Prefabs/Sidewalk.prefab",
                SearchOutcome.NoReference,
                "no `.cs` file names the prefab; the only `Sidewalk` code hits are the new City layer's own "
                + "`City/Runtime/Network/Sidewalk.cs`, `CitySidewalkGate.cs` and `GeometryProvenance.cs`",
                SearchOutcome.NoReference,
                "GUID `e531c3de6aaa1ac42ae95709665658ca` has zero hits in any `.prefab`, `.unity` or `.asset` under "
                + "`Assets/`",
                SearchOutcome.NoReference,
                "no `.rao` library carries the GUID; the libraries are XML and store paths, not GUIDs",
                SearchOutcome.Referenced,
                "HIT: `Editor/Library/ExtrudedObjects/ESOSidewalk.rao` line 3, "
                + "`<CurrentSplinationString>Assets/RoadCityMaster/Prefabs/Sidewalk.prefab`",
                Disposition.WireUp,
                "The reference case. Load-bearing by string path, so a GUID sweep would call it orphaned and deleting "
                + "it would break the sidewalk library entry silently. RM-29 owns the wiring and the inspector field "
                + "that makes it reachable without hand-loading the library."));

            e.Add(new DispositionEntry("Mesh/SidewalkCap.FBX", ItemKind.Asset, "Mesh/SidewalkCap.FBX",
                SearchOutcome.NoReference,
                "no `.cs` file names the mesh",
                SearchOutcome.NoReference,
                "no prefab or scene under `Assets/` references its GUID",
                SearchOutcome.NoReference,
                "no serialised asset references its GUID",
                SearchOutcome.Referenced,
                "HIT: `ESOSidewalk.rao` lines 4 and 5, `<CurrentSplinationCap1String>` and `Cap2String`, both "
                + "`Assets/RoadCityMaster/Mesh/SidewalkCap.FBX`",
                Disposition.WireUp,
                "The end cap of the same feature. It travels with RM-29's sidewalk: wiring one without the other "
                + "produces an extrusion with open ends, which is the defect RM-26 refuses elsewhere."));

            e.Add(new DispositionEntry("Editor/Library/ExtrudedObjects/ESOSidewalk.rao", ItemKind.Asset,
                "Editor/Library/ExtrudedObjects/ESOSidewalk.rao",
                SearchOutcome.NoReference,
                "no `.cs` file names the file; `SplinatedMeshMaker.cs:347` and `:353` BUILD the name as "
                + "`\"ESO\" + objectName + \".rao\"`, so the literal never appears",
                SearchOutcome.NotApplicable,
                "a `.rao` is an XML library file, never referenced from a scene or prefab -- it is read by the load "
                + "window, not serialised into a GameObject",
                SearchOutcome.NoReference,
                "no other `.rao` references it; each is independent",
                SearchOutcome.Referenced,
                "HIT by convention: `SplinatedMeshMaker.cs:347/353/357` compose the path from the object name, and "
                + "`EdgeObjectMaker.cs:267` enumerates the library folder by filename prefix",
                Disposition.WireUp,
                "Discovered by filename convention, so RENAMING it breaks the entry with no compile error. RM-29's "
                + "wiring must reach it through the same convention rather than a new hard-coded path."));

            e.Add(new DispositionEntry("Editor/Library/Thumbs/Sidewalk.png", ItemKind.Asset,
                "Editor/Library/Thumbs/Sidewalk.png",
                SearchOutcome.NoReference,
                "no `.cs` file names the image",
                SearchOutcome.NoReference,
                "no prefab or scene references its GUID",
                SearchOutcome.Referenced,
                "HIT: `ESOSidewalk.rao` line 172, "
                + "`<thumbString>Assets/RoadCityMaster/Editor/Library/Thumbs/Sidewalk.png`",
                SearchOutcome.Referenced,
                "read back through `EdgeObjectMaker.thumbString` and written by `Scripts/Editor/SaveWindow.cs:53` via "
                + "`EngineIntegration.GetAssetPath`, i.e. a stored path string, never a typed reference",
                Disposition.KeepWithReason,
                "The library window's thumbnail for the sidewalk entry. Small, live, and referenced only by a stored "
                + "path -- exactly the shape of asset a reference sweep deletes."));

            e.Add(new DispositionEntry("RoadCityMaster.asmdef", ItemKind.Asset, "RoadCityMaster.asmdef",
                SearchOutcome.NoReference,
                "no `.cs` file names the asmdef",
                SearchOutcome.NoReference,
                "no scene or prefab references it; an asmdef is never a serialised dependency",
                SearchOutcome.NoReference,
                "no `.rao` or `ScriptableObject` names it",
                SearchOutcome.Referenced,
                "HIT: `Scripts/RoadEditorUtility.cs:35` passes the literal GUID "
                + "`e9c7aa1199abeb64c82a4831b3c7286f` -- the GUID of `RoadCityMaster.asmdef.meta` -- to "
                + "`AssetDatabase.GUIDToAssetPath`, then takes the tool's base path from the result",
                Disposition.KeepWithReason,
                "The single most load-bearing file in the tree with no ordinary reference of any kind. The whole tool "
                + "self-locates through it, so moving the folder is safe and renaming or regenerating the `.meta` is "
                + "not. Every path-loaded asset below depends on this one lookup succeeding."));

            e.Add(new DispositionEntry("Flares/SodiumBulb.flare", ItemKind.Asset, "Flares/SodiumBulb.flare",
                SearchOutcome.NoReference,
                "RM-49 found `Scripts/IntersectionObjects.cs:796`, "
                + "`light.flare = EngineIntegration.LoadAssetFromPath<Flare>(basePath + \"/Flares/SodiumBulb.flare\")`. "
                + "RM-68 deleted that line: URP reads nothing from `Light.flare`, so it loaded an asset to assign it to "
                + "a dead field. No `.cs` file names the `.flare` any more",
                SearchOutcome.NoReference,
                "`Prefabs/TunnelLight.prefab` line 56 has `m_Flare: {fileID: 0}` -- the prefab never carried it, which "
                + "is why deleting the assignment left no prefab to repair",
                SearchOutcome.NoReference,
                "no serialised asset references its GUID `d3b176ff91de5b14db8c62f5ec3f211a`",
                SearchOutcome.Referenced,
                "HIT as prose, not as a load: `Scripts/SodiumFlare.cs` names `Flares/SodiumBulb.flare` in its header as "
                + "the source of every constant it carries, and `Scripts/Editor/ShaderInventory.cs` inventories it as "
                + "the superseded row beside `Flares/SodiumBulb.asset`",
                Disposition.KeepWithReason,
                "Superseded by RM-68 rather than deleted. The `LensFlareDataSRP` at `Flares/SodiumBulb.asset` now draws "
                + "the bulb, and this file is where every number in it came from -- element size 25, `m_UseLightColor: "
                + "0`, `m_Rotate: 0`, `m_UseFog: 1`. Deleting it would leave the ported values with no source, and it "
                + "costs nothing on disk because no code path loads it."));

            e.Add(new DispositionEntry("RoadCityMaster.RigidBody", ItemKind.Type, "Scripts/RigidBody.cs",
                SearchOutcome.Referenced,
                "HIT: `Scripts/IntersectionObjects.cs:499`, `_obj.AddComponent<RoadCityMaster.RigidBody>()` -- added "
                + "at build time, so no field and no inspector names the type",
                SearchOutcome.NoReference,
                "no prefab or scene carries the component; it is only ever added in code. B-21 re-ran this by GUID: "
                + "`2759b117-0f87beb4-ebaadcee-989329b7` has zero hits in any `.prefab`, `.unity` or `.asset`",
                SearchOutcome.NoReference,
                "no serialised asset names it",
                SearchOutcome.NoReference,
                "no string or `AddComponent(\"RigidBody\")` by name; the generic overload is used",
                Disposition.KeepWithReason,
                "Live, but only just: its whole remaining behaviour is `Awake()` destroying the `Rigidbody` on the same "
                + "object. That is worth a comment rather than a deletion, because the removal it performs is what "
                + "keeps dropped intersection objects from falling -- B-21 wrote that comment while performing the "
                + "removal recorded in the row below. RM-102 may fold it into the placer."));

            e.Add(new DispositionEntry("RigidBody.minCollisionVelocity", ItemKind.Field, "Scripts/RigidBody.cs",
                SearchOutcome.NoReference,
                "the only read was `Scripts/RigidBody.cs:33`, INSIDE the commented-out block that spanned lines 23 to "
                + "79; no live line read it. Both are gone: the field and that block were removed together",
                SearchOutcome.NoReference,
                "no `.prefab`, `.unity` or `.asset` under `Assets/` serialised `minCollisionVelocity`, so no saved "
                + "value was lost -- B-21 re-ran this search by field name before deleting, and it returned nothing",
                SearchOutcome.NoReference,
                "no serialised library names it",
                SearchOutcome.NoReference,
                "no string names it; it is not read through `SerializedObject.FindProperty` either",
                Disposition.Remove,
                "Scaffolding, not an unfinished feature: the abandoned collision-sleep experiment that read it was "
                + "commented out above it. The field and that block are ONE disposition group -- removing the field "
                + "alone would have left a comment referring to something that no longer exists -- so both went in one "
                + "edit, together with the two commented `isForcedSleeping` and `isIgnoringRigidBody` declarations that "
                + "only that block read. `Scripts/RigidBody.cs` stays: the TYPE is live, as the row above records.",
                "B-21"));

            e.Add(new DispositionEntry("Navigation.InitResetNavigationData", ItemKind.Method, "Scripts/Navigation.cs",
                SearchOutcome.Referenced,
                "HIT, but internal only: `Scripts/Navigation.cs:11`, the first statement of "
                + "`UpdateConnectedNodes()`. No caller outside the file exists anywhere under `Assets/`",
                SearchOutcome.NotApplicable,
                "a static method cannot be serialised into a scene or prefab",
                SearchOutcome.NotApplicable,
                "a static method cannot appear in a serialised asset",
                SearchOutcome.NoReference,
                "no `Invoke`, `SendMessage` or reflection call names it",
                Disposition.KeepWithReason,
                "Keep the code, narrow the promise: it is used, so it is not dead, but `public` advertises an entry "
                + "point nothing outside the file uses. Its visibility is the finding, not its existence."));

            e.Add(new DispositionEntry("RoadCityMaster.Navigation", ItemKind.Type, "Scripts/Navigation.cs",
                SearchOutcome.Referenced,
                "HIT: `Scripts/Intersections.cs:337`, `Navigation.UpdateConnectedNodes()`. The other apparent hit, "
                + "`Scripts/Spline/SplineN.cs:50`, is the comment `//Navigation:` and is NOT a reference",
                SearchOutcome.NotApplicable,
                "a static class cannot be serialised",
                SearchOutcome.NotApplicable,
                "a static class cannot appear in a serialised asset",
                SearchOutcome.NoReference,
                "no string or reflection use",
                Disposition.KeepWithReason,
                "Live. Recorded because a case-insensitive or comment-blind scan reports two references here and one "
                + "of them is prose -- the same mistake that makes a scan's raw count untrustworthy as evidence."));

            e.Add(new DispositionEntry("RoadCityMaster.ThreadedJob", ItemKind.Type, "Scripts/ThreadedJob.cs",
                SearchOutcome.Referenced,
                "HIT three times as a BASE CLASS: `Scripts/RoadCalcs1.cs:6`, `Scripts/RoadCalcs2.cs:6` and "
                + "`Scripts/Terrain/TerrainCalcs.cs:6`",
                SearchOutcome.NoReference,
                "not a component; nothing serialises it",
                SearchOutcome.NoReference,
                "no serialised asset names it",
                SearchOutcome.NoReference,
                "no string or reflection use",
                Disposition.KeepWithReason,
                "Live, and recorded because inheritance is the reference shape a call-site search misses: nothing ever "
                + "writes `ThreadedJob.` anywhere, so a member-usage scan reports it as unused."));

            e.Add(new DispositionEntry("RoadCityMaster.RenderQueue", ItemKind.Type, "Scripts/RenderQueue.cs",
                SearchOutcome.NoReference,
                "zero hits outside its own declaration: `\\bRenderQueue\\b` across every `.cs` in the tool returns "
                + "only `Scripts/RenderQueue.cs` itself, and no constant of it is read anywhere. The apparent hit at "
                + "`Scripts/Editor/MaterialPortRecipes.cs:420` is the serialised key `m_CustomRenderQueue` quoted "
                + "inside a recipe's evidence, not a use of this type",
                SearchOutcome.NotApplicable,
                "a static class of `const int` cannot be serialised",
                SearchOutcome.NoReference,
                "no serialised asset names it",
                SearchOutcome.Referenced,
                "HIT, and B-21 found it by re-running the search rather than trusting this row: the FILE is named as a "
                + "string literal at `City/Runtime/Licensing/UpstreamProvenance.cs:98`, in the frozen vendor set that "
                + "`CityUpstreamProvenanceGate` case 1 asserts is on disk exactly once. No material, shader or other "
                + "string names it; queue values are set through `material.renderQueue`",
                Disposition.KeepWithReason,
                "The type is dead and the FILE is load-bearing, which is why this row changed from remove to keep. Its "
                + "layers are `ParallaxLayers`, `Tracks`, `Routes` and `IndicatorRings`, belonging to a 2D map-style "
                + "renderer, and its own comment says setting `material.renderQueue` directly is easier -- which is "
                + "what RM-68's URP port does, so the road tool loses no capability by never calling it. But deleting "
                + "the file fails `CityUpstreamProvenanceGate` cases 1 and 2 and the written vendor count, and the only "
                + "way to pass them again is to edit `_vendorFiles`, which is documented as a constant of history "
                + "rather than a tuning value. Removing a file to keep a licensing record accurate is not a trade this "
                + "pass may make: the deadness is recorded here instead."));

            e.Add(new DispositionEntry("Materials/Concrete2Diffuse.mat", ItemKind.Asset,
                "Materials/Concrete2Diffuse.mat",
                SearchOutcome.Referenced,
                "HIT three times, found by re-running the search in B-21 rather than trusting the earlier `no .cs file "
                + "names the material`: `Scripts/Editor/AssetIdentityRegister.cs:320` lists it among the sanctioned "
                + "identities, `Scripts/Editor/MaterialPortRecipes.cs:249` is recipe R6 and names it as the concrete "
                + "material the URP port CONVERTED, and `Scripts/Editor/ShaderInventory.cs:233` records it on "
                + "`fileID: 46` inside the concrete family whose count is asserted as 12",
                SearchOutcome.NoReference,
                "GUID `a09bfe33f517abd47820653eeb108e28` has zero hits in any `.prefab` or `.unity` under `Assets/`",
                SearchOutcome.NoReference,
                "no `.rao` library names it; the sidewalk and road entries name `Road1.mat` and "
                + "`RoadDetailOverlay1.mat`",
                SearchOutcome.NoReference,
                "no FBX in the tree contains the literal `Concrete2Diffuse`, so no FBX slot resolves it BY NAME -- the "
                + "check RM-48 requires before any material may be called orphaned",
                Disposition.KeepWithReason,
                "Recorded as a removal deferred to RM-67, and RM-67 is why it is now a keep: the shader inventory did "
                + "read every material in the tree, and this one was PORTED. Three registers now name it -- a "
                + "sanctioned identity, a performed conversion recipe, and a family count of 12 -- so deleting the "
                + "file would falsify a record of work that was actually done, in three places at once, to save one "
                + "`.mat` on disk. `Concrete2.mat` exists beside it and is referenced; that makes this a duplicate by "
                + "content, not an orphan, and a duplicate whose port is on the record stays."));

            _entries = e;
            return new List<DispositionEntry>(_entries);
        }

        public static string OutcomeText(SearchOutcome outcome)
        {
            if (outcome == SearchOutcome.Referenced) { return "referenced"; }
            if (outcome == SearchOutcome.NotApplicable) { return "not applicable"; }
            return "no reference";
        }

        public static string DispositionText(Disposition disposition)
        {
            if (disposition == Disposition.Remove) { return "remove"; }
            if (disposition == Disposition.WireUp) { return "wire up"; }
            return "keep with reason";
        }

        public static string KindText(ItemKind kind)
        {
            if (kind == ItemKind.Type) { return "type"; }
            if (kind == ItemKind.Method) { return "method"; }
            if (kind == ItemKind.Field) { return "field"; }
            return "asset";
        }

        private static string Cell(SearchOutcome outcome, string evidence)
        {
            return OutcomeText(outcome) + " -- " + evidence;
        }

        /// <summary> The Disposition cell: the judgement, and -- once it has been carried out -- the task that carried
        /// it out. Inside the existing cell rather than a ninth column, so the table stays eight columns wide and
        /// <see cref="DispositionGate"/> case 8's pipe count is untouched. </summary>
        public static string DispositionCell(DispositionEntry d)
        {
            string cell = "**" + DispositionText(d.disposition) + "**";
            if (d.Performed) { cell += " (performed, " + d.performedBy + ")"; }
            return cell;
        }

        /// <summary> The disposition list as the document holds it: one row per item, all four searches in their own
        /// columns so the evidence cannot be summarised away. </summary>
        public static string Table()
        {
            string t = "| Item | Kind | Code search | Scene and prefab search | Serialised asset search "
                + "| String or name search | Disposition | Why |\n"
                + "| --- | --- | --- | --- | --- | --- | --- | --- |";
            foreach (DispositionEntry d in All())
            {
                t += "\n| `" + d.item + "` | " + KindText(d.kind)
                    + " | " + Cell(d.code, d.codeEvidence)
                    + " | " + Cell(d.scenePrefab, d.scenePrefabEvidence)
                    + " | " + Cell(d.serialised, d.serialisedEvidence)
                    + " | " + Cell(d.stringOrName, d.stringEvidence)
                    + " | " + DispositionCell(d) + " | " + d.reason + " |";
            }
            return t;
        }

        public static int CountWith(Disposition disposition)
        {
            int n = 0;
            foreach (DispositionEntry d in All()) { if (d.disposition == disposition) { n++; } }
            return n;
        }
    }

    /// <summary> Splices <see cref="DispositionRegister.Table()"/> into <c>Docs/Disposition.md</c>, through the one
    /// marker splicer the module owns (<see cref="TerrainFeatureTableWriter"/>) rather than a seventh copy of it. The
    /// path is built from <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
    public static class DispositionDocWriter
    {
        public const string TableBegin = "<!-- disposition-table:begin";
        public const string TableEnd = "<!-- disposition-table:end -->";

        /// <summary> The one folder every scaffolding document lives in, moved OUT of the Unity Assets tree at the
        /// author's request (2026-09-19): these .md are build-time records, not shipped tool content, so importing
        /// them only fills the project with metas. It sits beside Assets in the project root -- reachable by the
        /// writers and the gates, but not by Unity's importer. Every DocPath() in the tool resolves through here so
        /// the location is defined once. </summary>
        public static string DocsRoot()
        {
            System.IO.DirectoryInfo parent = System.IO.Directory.GetParent(Application.dataPath);
            string root = parent == null ? Application.dataPath : parent.FullName;
            return Path.Combine(root, "RoadCityMaster_Docs");
        }

        public static string DocPath()
        {
            return Path.Combine(DocsRoot(), "Disposition.md");
        }

        /// <summary> The tool root, for the gate's check that every item the register names still exists. This is the
        /// CODE root inside Assets and does NOT move with the docs -- the source scans read it. </summary>
        public static string ToolRoot()
        {
            return Path.Combine(Application.dataPath, "RoadCityMaster");
        }

        public static string WantedBody()
        {
            return "\n\n" + DispositionRegister.Table() + "\n";
        }

        [MenuItem("Window/Road City Master/Documentation/Write the disposition list")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(DocPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        public static bool WriteInto(string path, out string message)
        {
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!TerrainFeatureTableWriter.FindBody(existing, TableBegin, TableEnd, out bodyStart, out bodyEnd,
                out message))
            {
                return false;
            }

            string wanted = WantedBody();
            string current = existing.Substring(bodyStart, bodyEnd - bodyStart);
            if (TerrainFeatureTableWriter.Normalise(current) == TerrainFeatureTableWriter.Normalise(wanted))
            {
                message = "The disposition table in '" + path + "' already matches the register; nothing written.";
                return true;
            }

            File.WriteAllText(path, existing.Substring(0, bodyStart) + wanted + existing.Substring(bodyEnd));
            message = "Rewrote the disposition table in '" + path + "'.";
            return true;
        }

        public static bool ReadTableFrom(string path, out string table, out string message)
        {
            table = string.Empty;
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!TerrainFeatureTableWriter.FindBody(existing, TableBegin, TableEnd, out bodyStart, out bodyEnd,
                out message))
            {
                return false;
            }

            table = existing.Substring(bodyStart, bodyEnd - bodyStart);
            message = "Read " + table.Length + " characters of disposition table from '" + path + "'.";
            return true;
        }

        public static bool ReadWholeDoc(string path, out string contents, out string message)
        {
            return TerrainFeatureTableWriter.ReadDocument(path, out contents, out message);
        }
    }
}
#endif

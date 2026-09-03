#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> What a rule covers. The kinds are the five things anyone actually does to an asset, plus the check
    /// that follows all of them, because a procedure with no verification step is a preference. </summary>
    public enum IdentityAction
    {
        Move,
        Rename,
        Delete,
        EditInPlace,
        CreateOrReplace,
        Verify,
    }


    /// <summary> One rule of the asset-identity procedure. Four cells: what you are doing, what to do, why the engine
    /// requires it, and what getting it wrong costs -- the last because a rule with no cost attached is one a hurried
    /// change skips. </summary>
    public struct IdentityRule
    {
        public readonly IdentityAction action;
        public readonly string rule;
        public readonly string why;
        public readonly string cost;

        public IdentityRule(IdentityAction action, string rule, string why, string cost)
        {
            this.action = action;
            this.rule = rule;
            this.why = why;
            this.cost = cost;
        }
    }


    /// <summary> A GUID that is allowed to appear as a typed literal in this tool's source. The rule is that GUIDs are
    /// minted by Unity and never typed; these are the exceptions, and each has to earn it by naming what it points at,
    /// where it is typed, and why no object reference could have been used instead. Anything else the gate finds in a
    /// script is a hand-authored identity. </summary>
    public struct SanctionedGuid
    {
        public readonly string guid;
        public readonly string names;
        public readonly string typedIn;
        public readonly string whyTyped;

        public SanctionedGuid(string guid, string names, string typedIn, string whyTyped)
        {
            this.guid = guid;
            this.names = names;
            this.typedIn = typedIn;
            this.whyTyped = whyTyped;
        }
    }


    /// <summary> RM-69's register: the asset-identity procedure, written down rather than remembered.
    ///
    /// Unity's asset identity lives in `.meta` files, so a GUID is a durable name and a file path is not. Every
    /// reference in every scene, prefab, material and `ScriptableObject` points at a GUID, which is why carrying the
    /// `.meta` with its asset preserves the whole dependency graph for free and recreating it breaks all of it at once
    /// -- while leaving the project looking fine until something is loaded.
    ///
    /// This tool has a specific instance of that: `Scripts/RoadEditorUtility.cs:35` locates the tool's own folder by
    /// asking `AssetDatabase.GUIDToAssetPath` for the asmdef's GUID and falls through to a hard-coded folder list if it
    /// fails. So the asmdef must be RENAMED, never recreated, and <see cref="AsmdefGuid"/> must survive the whole
    /// module. `AssetIdentityGate` is what proves it did.
    /// </summary>
    public static class AssetIdentityRegister
    {
        /// <summary> The asmdef GUID `Scripts/RoadEditorUtility.cs:35` asks for by name. Not a value to change: it is
        /// the identity that has to be preserved, and this constant exists so the gate can check the same string the
        /// runtime asks for rather than a copy of it. </summary>
        public const string AsmdefGuid = "e9c7aa1199abeb64c82a4831b3c7286f";

        /// <summary> The file whose `.meta` carries <see cref="AsmdefGuid"/>, tool-relative. </summary>
        public const string AsmdefRelativePath = "RoadCityMaster.asmdef";

        /// <summary> The one script allowed to type <see cref="AsmdefGuid"/> on a code path. </summary>
        public const string SelfLocationFile = "Scripts/RoadEditorUtility.cs";

        private static List<IdentityRule> rules;
        private static List<SanctionedGuid> guids;

        public static IList<IdentityRule> Rules()
        {
            if (rules == null) { rules = BuildRules(); }
            return rules;
        }

        public static IList<SanctionedGuid> SanctionedGuids()
        {
            if (guids == null) { guids = BuildGuids(); }
            return guids;
        }

        public static string ActionText(IdentityAction action)
        {
            switch (action)
            {
                case IdentityAction.Move: return "move";
                case IdentityAction.Rename: return "rename";
                case IdentityAction.Delete: return "delete";
                case IdentityAction.EditInPlace: return "edit in place";
                case IdentityAction.CreateOrReplace: return "create or replace";
                default: return "verify";
            }
        }

        public static string RulesTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Doing | The rule | Why the engine requires it | What getting it wrong costs |\n");
            builder.Append("| --- | --- | --- | --- |\n");
            IList<IdentityRule> all = Rules();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| ").Append(ActionText(all[i].action))
                       .Append(" | ").Append(all[i].rule)
                       .Append(" | ").Append(all[i].why)
                       .Append(" | ").Append(all[i].cost).Append(" |");
                if (i < all.Count - 1) { builder.Append('\n'); }
            }
            return builder.ToString();
        }

        public static string GuidsTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| GUID | Names | Typed in | Why a literal rather than a reference |\n");
            builder.Append("| --- | --- | --- | --- |\n");
            IList<SanctionedGuid> all = SanctionedGuids();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| `").Append(all[i].guid)
                       .Append("` | ").Append(all[i].names)
                       .Append(" | ").Append(all[i].typedIn)
                       .Append(" | ").Append(all[i].whyTyped).Append(" |");
                if (i < all.Count - 1) { builder.Append('\n'); }
            }
            return builder.ToString();
        }

        private static List<SanctionedGuid> BuildGuids()
        {
            List<SanctionedGuid> list = new List<SanctionedGuid>();
            AddGuids(list);
            return list;
        }

        private static List<IdentityRule> BuildRules()
        {
            List<IdentityRule> list = new List<IdentityRule>();
            AddMoveAndRename(list);
            AddDeleteAndEdit(list);
            AddVerify(list);
            return list;
        }

        /// <summary> The two operations that change a path. Both are safe from inside the editor and both are traps
        /// outside it, for the same reason: the `.meta` is a separate file and a file manager will happily move one of
        /// the pair. </summary>
        private static void AddMoveAndRename(List<IdentityRule> list)
        {
            list.Add(new IdentityRule(IdentityAction.Move,
                "Move assets from inside the editor, or `AssetDatabase.MoveAsset`. Outside the editor, move the file "
                + "AND its `.meta` together, never the file alone",
                "the editor moves both halves and rewrites nothing else, because the GUID inside the `.meta` is the "
                + "name every reference actually holds; the path is only where the engine last saw it",
                "the asset re-imports as a NEW asset with a fresh GUID and every scene, prefab and material that "
                + "referenced it resolves to nothing -- silently, until something is loaded"));

            list.Add(new IdentityRule(IdentityAction.Rename,
                "Rename in place. Never delete and re-add as a way of renaming, and never recreate a `.meta` you could "
                + "have carried",
                "a rename touches the path only; delete-and-re-add mints a new GUID even when the bytes are identical, "
                + "and Unity has no way to know the two files were meant to be the same asset",
                "RM-01 to RM-04 renamed this tool's folder and asmdef. A recreated asmdef `.meta` would have changed "
                + AsmdefGuid + ", and `" + SelfLocationFile + "` asks for exactly that string to find its own folder -- "
                + "so the tool would have fallen through to its hard-coded folder list and located itself wrongly or "
                + "not at all"));

            list.Add(new IdentityRule(IdentityAction.Rename,
                "The EXISTING asmdef is renamed, never recreated. A genuinely new asmdef -- RM-06's editor assembly "
                + "split -- is a new asset and takes the new GUID Unity mints for it, which is correct",
                "\"the GUID must not change\" applies to identities something already references. A new assembly "
                + "definition is referenced by nothing yet, so its fresh GUID breaks nothing",
                "conflating the two produces the opposite mistake in each direction: a preserved GUID on a new asmdef "
                + "collides, and a fresh GUID on the old one breaks self-location"));
        }

        /// <summary> Deletion and in-place editing. Both leave the `.meta` alone -- deletion by removing it with its
        /// asset, editing by not touching it at all -- and both are where an orphan gets left behind. </summary>
        private static void AddDeleteAndEdit(List<IdentityRule> list)
        {
            list.Add(new IdentityRule(IdentityAction.Delete,
                "Delete the asset AND its `.meta`, leaving no orphaned `.meta` behind. Anything the disposition list "
                + "marks keep-with-reason keeps its GUID untouched",
                "an orphaned `.meta` is a claim that an asset exists; the editor logs it and, worse, will re-attach it "
                + "to the next file that lands on that path, handing a new asset an old identity",
                "RM-66's removals are the pass that would leave them. A stray `.meta` re-adopted by an unrelated file "
                + "gives that file another asset's references, which reads as data corruption rather than as a bad "
                + "delete"));

            list.Add(new IdentityRule(IdentityAction.EditInPlace,
                "An asset that is EDITED keeps its `.meta` and its GUID untouched -- `.mat`, `.shader`, "
                + "`.shadergraph`, `.hlsl`, `.rao`, `.prefab` alike",
                "editing bytes does not change identity, and the whole point of the GUID is that it does not have to",
                "RM-68 rewrote all 471 materials in place for exactly this reason: 336 sign faces are reached by a "
                + "composed string path, `Concrete2` is resolved BY NAME inside 101 FBX files, and "
                + "`Scripts/IntersectionObjects.cs` builds a material path at runtime. A new GUID would satisfy every "
                + "shader check and break all of that with no compile error"));

            list.Add(new IdentityRule(IdentityAction.CreateOrReplace,
                "An asset that is CREATED or REPLACED takes the GUID Unity mints on import. No `.meta` is authored by "
                + "hand and no GUID literal is typed into an asset or a script",
                "Unity is the only writer that can guarantee a GUID is unique in this project; a hand-typed one is a "
                + "guess that a collision will not happen",
                "RM-68's `Flares/SodiumBulb.asset` and RM-67's three authored materials all went in through "
                + "`AssetDatabase.CreateAsset`. Hand-writing either `.meta` would have produced an asset whose "
                + "identity no import could reconcile"));

            list.Add(new IdentityRule(IdentityAction.CreateOrReplace,
                "When a replacement does introduce a new GUID, repoint the old references through the editor before "
                + "the change is called done",
                "a replacement is only finished when nothing resolves to the asset that went away",
                "left undone this is indistinguishable from a broken move, and it is found by the same check -- which "
                + "is why the verification step below covers both"));
        }

        /// <summary> The step that makes the rest of it a procedure rather than advice. </summary>
        private static void AddVerify(List<IdentityRule> list)
        {
            list.Add(new IdentityRule(IdentityAction.Verify,
                "Take the backup or commit RM-04 requires BEFORE the first structural change of a session",
                "a broken GUID reference cannot be repaired by reversing the file operation: the references are gone, "
                + "not misplaced",
                "this tool's folder carries no history of its own inside the project, so a bad rename has no undo. "
                + "That single fact is why this task is a gate rather than a note"));

            list.Add(new IdentityRule(IdentityAction.Verify,
                "After any structural change, run `RCMMETA` -- Window > Road City Master > Testing > Asset identity "
                + "and GUID discipline -- then reopen the project, let it import, and read the console",
                "the gate finds orphaned `.meta` files, assets with no `.meta`, duplicate GUIDs and dead GUID "
                + "references by reading the files; the import log finds the ones only a load reveals",
                "a broken reference is silent in the file system and loud only at import, so skipping the reopen moves "
                + "the discovery to whoever opens the scene next"));

            list.Add(new IdentityRule(IdentityAction.Verify,
                "Self-location is part of the check: `AssetDatabase.GUIDToAssetPath(" + AsmdefGuid + ")` must still "
                + "return a path ending in `" + AsmdefRelativePath + "`",
                "`" + SelfLocationFile + "` builds every runtime asset path in the tool from the folder it finds this "
                + "way, so this one lookup is upstream of every material, prefab and library load",
                "when it fails the tool falls through to a hard-coded list of plausible folders and either finds the "
                + "wrong copy or throws -- and neither failure names the rename that caused it"));
        }

        /// <summary> Every typed GUID literal this tool's source is allowed to contain, and the case each makes for
        /// itself. All seven are READS of an identity that already exists -- a live lookup, a text comparison, or a
        /// citation in evidence prose. None of them authors an identity, which is the line this register draws: Unity
        /// mints GUIDs, and a script may quote one but never invent one. </summary>
        private static void AddGuids(List<SanctionedGuid> list)
        {
            list.Add(new SanctionedGuid(AsmdefGuid,
                "`" + AsmdefRelativePath + "`, whose folder is the tool's own root",
                "`" + SelfLocationFile + ":35` as a live lookup, and quoted as prose in "
                + "`Scripts/Editor/AssetIdentityRegister.cs` and `Scripts/Editor/DispositionRegister.cs`",
                "the tool has to locate itself BEFORE it can load any asset, so there is no asset reference available "
                + "to use instead -- the GUID is the only name that survives the folder being renamed or moved. It is "
                + "read, never written, and `RCMMETA` case 2 proves it still resolves"));

            list.Add(new SanctionedGuid(PipelineAudit.UrpLitShaderGuid,
                "URP's `Universal Render Pipeline/Lit` shader, inside the render-pipelines package",
                "`Scripts/Editor/PipelineAudit.cs`, compared against raw `.mat` text by `MaterialPortGate` and "
                + "`ShaderInventoryGate`, and quoted in `Scripts/Editor/MaterialPortRecipes.cs` and "
                + "`Scripts/Editor/ShaderInventory.cs`",
                "the port's gates read `.mat` files as TEXT, where there is no `Shader` object to ask. The literal is "
                + "checked against `Shader.Find(\"" + PipelineAudit.UrpLitShaderName + "\")` by `RCMSHADER` case 12, so "
                + "a stale copy fails loudly instead of quietly matching nothing. `MaterialPortOperator` itself "
                + "assigns the shader OBJECT and never writes this string"));

            list.Add(new SanctionedGuid(PipelineAudit.BuiltInResourceShaderGuid,
                "the engine's own built-in resource library, where Built-in `Standard` lives as `fileID: 46` and "
                + "`Standard (Specular setup)` as `fileID: 45`",
                "`Scripts/Editor/PipelineAudit.cs` and quoted in `Scripts/Editor/ShaderInventory.cs`",
                "it is a sentinel, not an asset this project owns, and every use of it is a NEGATIVE assertion: no "
                + "`.mat` in the tree may still contain it. A gate proving an absence has to be able to name the thing "
                + "that must be absent"));

            list.Add(new SanctionedGuid("7a2f2dcaa3b8d5d47a898e35c7cdfbd2",
                "`Assets/UniverseCreator/Settings/UC_URP_Pipeline.asset`, the render pipeline asset in force",
                "`Scripts/Editor/PipelineAudit.cs:119`, inside the SOURCE cell of a pipeline fact",
                "it is a citation, not a reference: the fact records that `ProjectSettings/GraphicsSettings.asset:49` "
                + "names this GUID as `m_CustomRenderPipeline`, so a reader can open that line and disagree. Quoting "
                + "the GUID is what makes the evidence checkable; the asset itself belongs to Universe Creator and R-5 "
                + "forbids this task touching it"));

            list.Add(new SanctionedGuid("d3b176ff91de5b14db8c62f5ec3f211a",
                "`Flares/SodiumBulb.flare`, the Built-in lens flare RM-68 superseded",
                "`Scripts/Editor/DispositionRegister.cs`, inside the asset-search evidence cell of its disposition "
                + "entry",
                "the entry's claim is that NO serialised asset references this GUID -- the search that justified "
                + "keeping the file as a record rather than wiring it up. An evidence cell that says \"the GUID has "
                + "zero hits\" without naming the GUID cannot be re-run by anyone else"));

            list.Add(new SanctionedGuid("e531c3de6aaa1ac42ae95709665658ca",
                "`Prefabs/Sidewalk.prefab`",
                "`Scripts/Editor/DispositionRegister.cs:135`, inside a scene-and-prefab search evidence cell",
                "same reason as the flare: the disposition is a zero-hit finding, and a zero-hit finding is only "
                + "falsifiable if the string that was searched for is written down"));

            list.Add(new SanctionedGuid("a09bfe33f517abd47820653eeb108e28",
                "`Materials/Concrete2Diffuse.mat`",
                "`Scripts/Editor/DispositionRegister.cs:325`, inside a scene-and-prefab search evidence cell",
                "the third zero-hit citation. It is also the reason this register lists citations at all rather than "
                + "banning every literal: the alternative is evidence cells that assert a search happened without "
                + "saying what was searched"));
        }
    }
}
#endif

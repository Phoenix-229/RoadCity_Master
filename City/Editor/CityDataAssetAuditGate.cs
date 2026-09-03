#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-86's headless proof of the data-asset audit: the register in <see cref="DataAssetAuditRegister"/>
    /// names every value-bearing DATA asset in the tool tree (the .rao object libraries, the .physicMaterial surfaces
    /// and the .flare with its .tga), it names ONLY assets that are really on disk, and every number it carries is
    /// classified into
    /// exactly one of the three C-15 verdicts with the evidence its verdict requires — a reference for Matches and
    /// Wrong, a corrected target for Wrong, a note for Authored. Nothing is left "unknown" and no Matches stands
    /// without a cited source. Correcting the Wrong ones is RM-87; this gate only proves the inventory is complete,
    /// well-formed and deterministic. </summary>
    public static class CityDataAssetAuditGate
    {
        private const string Prefix = "RCMDATAAUDIT";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Data asset audit")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            _cases = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CaseCountsAreExact(failures, sb);
            CaseEveryNumberIsWellFormed(failures, sb);
            CaseRegisterCoversTreeBothWays(failures, sb);
            CaseKnownDefectIsCaught(failures);
            CaseInventoryIsDeterministic(failures);

            for (int i = 0; i < failures.Count; i++) { sb.AppendLine(Prefix + " FAIL   " + failures[i]); }
            sb.AppendLine(Prefix + " result " + (failures.Count == 0 ? "PASS" : "FAIL") + "  cases " + _cases
                + "  failures " + failures.Count);
            sb.Append(Prefix + " END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(message); }
        }

        /// <summary> The inventory names exactly the assets the doc says exist: 30 EdgeObject + 23 ExtrudedObject
        /// + 57 Group .rao (= 110 .rao) + 4 .physicMaterial + 1 .flare + 1 .tga = 116, each with at least one
        /// number. </summary>
        private static void CaseCountsAreExact(List<string> failures, StringBuilder sb)
        {
            IReadOnlyList<AssetAudit> all = DataAssetAuditRegister.All();
            int edge = 0, extruded = 0, group = 0, physic = 0, flare = 0, texture = 0, empty = 0;
            foreach (AssetAudit a in all)
            {
                switch (a.Family)
                {
                    case "EdgeObject": edge++; break;
                    case "ExtrudedObject": extruded++; break;
                    case "Group": group++; break;
                    case "PhysicMaterial": physic++; break;
                    case "Flare": flare++; break;
                    case "Texture": texture++; break;
                }
                if (a.Numbers == null || a.Numbers.Count == 0) { empty++; }
            }

            Expect(all.Count == 116, "the inventory must hold 116 assets, found " + Num(all.Count) + ".", failures);
            Expect(edge == 30, "expected 30 EdgeObject .rao, found " + Num(edge) + ".", failures);
            Expect(extruded == 23, "expected 23 ExtrudedObject .rao, found " + Num(extruded) + ".", failures);
            Expect(group == 57, "expected 57 Group .rao, found " + Num(group) + ".", failures);
            Expect(physic == 4, "expected 4 .physicMaterial, found " + Num(physic) + ".", failures);
            Expect(flare == 1, "expected 1 .flare, found " + Num(flare) + ".", failures);
            Expect(texture == 1, "expected 1 .tga, found " + Num(texture) + ".", failures);
            Expect(empty == 0, "every audited asset must carry at least one classified number; "
                + Num(empty) + " were empty.", failures);

            sb.AppendLine(Prefix + " counts assets " + Num(all.Count) + "  edge " + Num(edge)
                + "  extruded " + Num(extruded) + "  group " + Num(group)
                + "  physic " + Num(physic) + "  flare " + Num(flare) + "  texture " + Num(texture));
        }

        /// <summary> C-15/§10.18: every number is well-formed for its verdict — a field and value always, a reference
        /// for Matches and Wrong (a note for Authored), and a corrected target for every Wrong. Nothing "unknown",
        /// no Matches without a source. The verdict tally is reported. </summary>
        private static void CaseEveryNumberIsWellFormed(List<string> failures, StringBuilder sb)
        {
            int malformed = 0;
            foreach (AssetAudit a in DataAssetAuditRegister.All())
                foreach (NumberAudit n in a.Numbers)
                    if (!n.IsWellFormed())
                    {
                        malformed++;
                        if (malformed <= 8)
                            failures.Add("malformed audit row in " + a.RelPath + ": field '" + n.Field + "'.");
                    }
            Expect(malformed == 0, "every number must be well-formed for its verdict; " + Num(malformed)
                + " were not.", failures);

            int matches, wrong, authored;
            DataAssetAuditRegister.CountByVerdict(out matches, out wrong, out authored);
            sb.AppendLine(Prefix + " verdict matches " + Num(matches) + "  wrong " + Num(wrong)
                + "  authored " + Num(authored));
        }

        /// <summary> The register names ONLY real files and names ALL of them: the set of
        /// .rao/.physicMaterial/.flare/.tga
        /// on disk under the tool tree equals the set the register lists (root-relative, Ordinal). A file present on
        /// disk but absent from the register is an un-audited number; a register entry with no file is a phantom. </summary>
        private static void CaseRegisterCoversTreeBothWays(List<string> failures, StringBuilder sb)
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");

            HashSet<string> onDisk = new HashSet<string>(System.StringComparer.Ordinal);
            List<string> files = new List<string>();
            Collect(root, files);
            for (int i = 0; i < files.Count; i++) { onDisk.Add(ToRootRelative(root, files[i])); }

            HashSet<string> inRegister = new HashSet<string>(System.StringComparer.Ordinal);
            const string toolPrefix = "Assets/RoadCityMaster/";
            foreach (AssetAudit a in DataAssetAuditRegister.All())
            {
                string rel = a.RelPath.StartsWith(toolPrefix, System.StringComparison.Ordinal)
                    ? a.RelPath.Substring(toolPrefix.Length) : a.RelPath;
                inRegister.Add(rel);
            }

            int missingFromRegister = 0, phantom = 0;
            foreach (string d in onDisk)
                if (!inRegister.Contains(d))
                {
                    missingFromRegister++;
                    if (missingFromRegister <= 8) failures.Add("on disk but not audited: " + d);
                }
            foreach (string r in inRegister)
                if (!onDisk.Contains(r))
                {
                    phantom++;
                    if (phantom <= 8) failures.Add("audited but not on disk: " + r);
                }

            Expect(onDisk.Count > 0, "the tool tree must contain at least one data asset to audit.", failures);
            Expect(missingFromRegister == 0, Num(missingFromRegister)
                + " data assets on disk are not named in the register.", failures);
            Expect(phantom == 0, Num(phantom) + " register entries name no file on disk.", failures);

            sb.AppendLine(Prefix + " tree   onDisk " + Num(onDisk.Count) + "  inRegister " + Num(inRegister.Count));
        }

        /// <summary> The audit actually found the one known defect: the Pavement surface's friction is Wrong against a
        /// cited value, and its correction target is named for RM-87. If this stops failing, either the defect was
        /// silently fixed elsewhere or the audit stopped looking — both worth a red gate. </summary>
        private static void CaseKnownDefectIsCaught(List<string> failures)
        {
            IReadOnlyList<DataAssetAuditRegister.WrongNumber> wrong = DataAssetAuditRegister.WrongNumbers();
            bool dyn = false, sta = false;
            foreach (DataAssetAuditRegister.WrongNumber w in wrong)
            {
                if (!w.RelPath.EndsWith("Pavement.physicMaterial", System.StringComparison.Ordinal)) continue;
                if (w.Number.Field == "dynamicFriction") { dyn = string.IsNullOrEmpty(w.Number.Corrected) == false; }
                if (w.Number.Field == "staticFriction") { sta = string.IsNullOrEmpty(w.Number.Corrected) == false; }
            }
            Expect(dyn, "Pavement.dynamicFriction must be flagged Wrong with a correction for RM-87.", failures);
            Expect(sta, "Pavement.staticFriction must be flagged Wrong with a correction for RM-87.", failures);
        }

        /// <summary> C-06: the inventory is built from fixed arrays in fixed order, so its rendered table is
        /// byte-identical on repeat — no dictionary-order or clock dependence. </summary>
        private static void CaseInventoryIsDeterministic(List<string> failures)
        {
            string a = DataAssetAuditRegister.ToInventoryTable();
            string b = DataAssetAuditRegister.ToInventoryTable();
            Expect(a == b, "the inventory table must be deterministic across builds.", failures);
        }

        /// <summary> Self-contained enumeration of the value-bearing DATA assets for the runtime-assembly gate:
        /// recursive, skips Unity-hidden folders (`.` prefix or `~` suffix), matches only the audited extensions,
        /// sorted Ordinal for determinism. The `.tga` counts: its import settings (colour space, wrap, filtering,
        /// alpha, mips) are real numbers that decide how it is sampled, so they are audited like any other. </summary>
        private static void Collect(string dir, List<string> found)
        {
            if (!Directory.Exists(dir)) { return; }
            foreach (string file in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(file);
                if (ext == ".rao" || ext == ".physicMaterial" || ext == ".flare" || ext == ".tga") { found.Add(file); }
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (string.IsNullOrEmpty(name) || name[0] == '.' || name.EndsWith("~")) { continue; }
                Collect(sub, found);
            }
        }

        private static string ToRootRelative(string root, string full)
        {
            string r = root.Replace('\\', '/');
            string f = full.Replace('\\', '/');
            if (f.StartsWith(r, System.StringComparison.OrdinalIgnoreCase))
            {
                string rel = f.Substring(r.Length);
                if (rel.Length > 0 && rel[0] == '/') { rel = rel.Substring(1); }
                return rel;
            }
            return f;
        }

        private static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }
    }
}
#endif

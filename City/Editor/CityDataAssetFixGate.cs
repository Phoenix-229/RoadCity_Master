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
    /// <summary> RM-87's headless proof that the audit's corrections actually landed: every number RM-86 flagged
    /// <see cref="AuditVerdict.Wrong"/> now holds, ON DISK, the corrected value the register cites (not the old wrong
    /// one); the correction traces to a named reference; the edited asset keeps its `.meta` (the RM-69 identity rule);
    /// and no value the audit marked <see cref="AuditVerdict.Authored"/> was silently changed to a fabricated "real"
    /// figure. The proof of a data change is the before/after value beside the citation — this gate IS that proof.
    /// It reads the plain-text (YAML) assets directly; a Wrong number living in a format that is not safe to hand-edit
    /// would be reported as owed to the tool's own serializer rather than silently passed. </summary>
    public static class CityDataAssetFixGate
    {
        private const string Prefix = "RCMDATAFIX";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Data asset corrections")]
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

            CaseEveryWrongNumberIsCorrectedOnDisk(failures, sb);
            CaseEveryEditedAssetKeepsItsMeta(failures);
            CaseAuthoredDefaultsWereNotFabricated(failures, sb);
            CaseCheckIsDeterministic(failures);

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

        /// <summary> The core RM-87 proof: for every number the register flags Wrong, read the real asset on disk and
        /// show the field now holds the cited corrected value (within tolerance) and no longer the old wrong one. Each
        /// Wrong row must carry both a reference and a corrected target (traceability); a Wrong number in a format that
        /// cannot be hand-parsed is reported as owed to the tool's serializer, not silently passed. </summary>
        private static void CaseEveryWrongNumberIsCorrectedOnDisk(List<string> failures, StringBuilder sb)
        {
            IReadOnlyList<DataAssetAuditRegister.WrongNumber> wrong = DataAssetAuditRegister.WrongNumbers();
            int corrected = 0, stillWrong = 0, unparsable = 0, untraceable = 0;

            foreach (DataAssetAuditRegister.WrongNumber w in wrong)
            {
                if (string.IsNullOrEmpty(w.Number.Reference) || string.IsNullOrEmpty(w.Number.Corrected))
                {
                    untraceable++;
                    failures.Add("Wrong row lacks reference or corrected target: " + w.RelPath + " " + w.Number.Field);
                    continue;
                }

                string full = ToAbsolute(w.RelPath);
                double target, onDisk, oldWrong;
                if (!TryParseLeadingNumber(w.Number.Corrected, out target)
                    || !TryParseLeadingNumber(w.Number.Value, out oldWrong)
                    || !TryReadScalar(full, w.Number.Field, out onDisk))
                {
                    unparsable++;
                    failures.Add("could not verify correction (needs the tool's serializer?): " + w.RelPath
                        + " " + w.Number.Field);
                    continue;
                }

                bool matchesTarget = System.Math.Abs(onDisk - target) <= 0.001;
                bool stillOld = System.Math.Abs(onDisk - oldWrong) <= 0.000001;
                if (matchesTarget && !stillOld) { corrected++; }
                else
                {
                    stillWrong++;
                    failures.Add(w.RelPath + " " + w.Number.Field + " on disk = "
                        + onDisk.ToString(CultureInfo.InvariantCulture) + ", expected corrected "
                        + target.ToString(CultureInfo.InvariantCulture) + " (was wrong " + w.Number.Value + ").");
                }
            }

            Expect(wrong.Count > 0, "the audit must have flagged at least one Wrong number for RM-87 to correct.",
                failures);
            Expect(stillWrong == 0, Num(stillWrong) + " flagged numbers still hold the wrong value on disk.", failures);
            Expect(unparsable == 0, Num(unparsable) + " Wrong numbers could not be verified from the file.", failures);
            Expect(untraceable == 0, Num(untraceable) + " Wrong rows do not trace to a reference + target.", failures);

            sb.AppendLine(Prefix + " fix    wrongRows " + Num(wrong.Count) + "  corrected " + Num(corrected)
                + "  stillWrong " + Num(stillWrong) + "  unparsable " + Num(unparsable));
        }

        /// <summary> RM-69/RM-68 identity rule: each asset we edited to fix a Wrong number still has its `.meta`
        /// sibling on disk, so its GUID and every scene/prefab/library reference to it survive the correction. </summary>
        private static void CaseEveryEditedAssetKeepsItsMeta(List<string> failures)
        {
            HashSet<string> edited = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (DataAssetAuditRegister.WrongNumber w in DataAssetAuditRegister.WrongNumbers()) { edited.Add(w.RelPath); }

            int missingMeta = 0;
            foreach (string rel in edited)
            {
                string meta = ToAbsolute(rel) + ".meta";
                if (!File.Exists(meta))
                {
                    missingMeta++;
                    failures.Add("edited asset lost its .meta (GUID identity at risk): " + rel);
                }
            }
            Expect(missingMeta == 0, Num(missingMeta) + " edited assets are missing their .meta.", failures);
        }

        /// <summary> § 10.18 the other way: a value the register did NOT flag Wrong must not have silently drifted on
        /// disk. The real-world pass corrected friction (Wrong -> per-surface real, μs >= μk) and the channelizing /
        /// marker spacings (Wrong -> MUTCD figures), all verified above; every OTHER number the register states — the
        /// inelastic bounciness, the 30 m streetlight pitch and 1 m glare-screen pitch it certifies as already real,
        /// and the placement and meshing values it leaves author-owned — must still read its stated value on disk.
        /// Non-numeric rows (a lane-scaled preset, a composite lateral span) simply do not parse and are skipped. </summary>
        private static void CaseAuthoredDefaultsWereNotFabricated(List<string> failures, StringBuilder sb)
        {
            int checkedRows = 0, drifted = 0;
            foreach (AssetAudit a in DataAssetAuditRegister.All())
            {
                string full = ToAbsolute(a.RelPath);
                foreach (NumberAudit n in a.Numbers)
                {
                    if (n.Verdict == AuditVerdict.Wrong) continue; // Wrong rows are meant to change; checked above.
                    double stated, onDisk;
                    if (!TryParseLeadingNumber(n.Value, out stated) || !TryReadScalar(full, n.Field, out onDisk))
                        continue;
                    checkedRows++;
                    if (System.Math.Abs(onDisk - stated) > 0.001)
                    {
                        drifted++;
                        failures.Add("non-Wrong default changed on disk: " + a.RelPath + " " + n.Field + " = "
                            + onDisk.ToString(CultureInfo.InvariantCulture) + ", register states "
                            + n.Value + ".");
                    }
                }
            }
            Expect(drifted == 0, Num(drifted) + " non-Wrong defaults were silently changed on disk.", failures);
            sb.AppendLine(Prefix + " authored checkedOnDisk " + Num(checkedRows) + "  drifted " + Num(drifted));
        }

        /// <summary> C-06: the correction check reads the same files and reaches the same verdict twice. </summary>
        private static void CaseCheckIsDeterministic(List<string> failures)
        {
            string r1, r2;
            Run2(out r1);
            Run2(out r2);
            Expect(r1 == r2, "the correction check must be deterministic across runs.", failures);
        }

        // A side-effect-free replay of the core check, for the determinism case (does not touch _cases/failures).
        private static void Run2(out string signature)
        {
            StringBuilder sb = new StringBuilder();
            foreach (DataAssetAuditRegister.WrongNumber w in DataAssetAuditRegister.WrongNumbers())
            {
                double onDisk;
                bool ok = TryReadScalar(ToAbsolute(w.RelPath), w.Number.Field, out onDisk);
                sb.Append(w.RelPath).Append('|').Append(w.Number.Field).Append('|').Append(ok ? '1' : '0')
                  .Append('|').Append(ok ? onDisk.ToString(CultureInfo.InvariantCulture) : "-").Append('\n');
            }
            signature = sb.ToString();
        }

        // ------------------------------------------------------------------ helpers ------

        private static string ToAbsolute(string relPath)
        {
            const string assets = "Assets/";
            string tail = relPath.StartsWith(assets, System.StringComparison.Ordinal)
                ? relPath.Substring(assets.Length) : relPath;
            return Path.Combine(Application.dataPath, tail.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary> Reads a named scalar for an asset. Text assets are read directly; a BINARY asset (the flare's
        /// .tga) carries its settings in the sibling `.meta` instead, so that is where its fields are looked up — the
        /// import settings really are the asset's editable numbers, and this is what proves them on disk. </summary>
        private static bool TryReadScalar(string fullPath, string field, out double value)
        {
            string ext = Path.GetExtension(fullPath);
            if (ext == ".tga" || ext == ".png" || ext == ".psd" || ext == ".fbx" || ext == ".FBX")
                return TryReadScalarFrom(fullPath + ".meta", field, out value);
            return TryReadScalarFrom(fullPath, field, out value);
        }

        /// <summary> Reads a named scalar out of a hand-editable data asset, in either of the two plain-text shapes the
        /// tool tree actually uses: a `.physicMaterial`'s or `.meta`'s YAML (`dynamicFriction: 0.6`) or a `.rao`'s XML
        /// (`&lt;meterSep&gt;9&lt;/meterSep&gt;`). Returns false if the file or field is absent or the value is not
        /// numeric — the signal that a format needs the tool's own serializer instead of a hand edit. </summary>
        private static bool TryReadScalarFrom(string fullPath, string field, out double value)
        {
            value = 0;
            if (!File.Exists(fullPath)) return false;
            string yamlKey = field + ":";
            string openTag = "<" + field + ">";
            foreach (string raw in File.ReadAllLines(fullPath))
            {
                string line = raw.Trim();

                if (line.StartsWith(yamlKey, System.StringComparison.Ordinal))
                {
                    string rhs = line.Substring(yamlKey.Length).Trim();
                    return double.TryParse(rhs, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                }

                if (line.StartsWith(openTag, System.StringComparison.Ordinal))
                {
                    string body = line.Substring(openTag.Length);
                    int close = body.IndexOf('<');
                    if (close < 0) continue;
                    return double.TryParse(body.Substring(0, close).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value);
                }
            }
            return false;
        }

        /// <summary> Pulls the leading numeric token out of a register value or corrected-target string (handles a
        /// leading "~" and a trailing parenthetical, e.g. "~0.7 (dry asphalt, sliding)" -> 0.7). </summary>
        private static bool TryParseLeadingNumber(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0;
            while (i < s.Length && (s[i] == '~' || s[i] == ' ')) i++;
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == '-' || s[i] == '+'
                   || s[i] == 'e' || s[i] == 'E')) i++;
            if (i == start) return false;
            return double.TryParse(s.Substring(start, i - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }
    }
}
#endif

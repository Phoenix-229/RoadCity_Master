// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
// Authored for this project. See LICENSE.md and THIRD-PARTY-NOTICES.md.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-86 (§ 10.46): the audit of every value-bearing DATA asset in the tool against a real-world
    /// reference. "Data asset" here means an editable-as-code file that holds a physical, dimensional or optical
    /// NUMBER — the 110 <c>.rao</c> object libraries, the 4 <c>.physicMaterial</c> surfaces and the 1 <c>.flare</c>
    /// — as opposed to the render assets (<c>.mat</c>/<c>.shader</c>/HLSL) that RM-67/68 own. Each number carries
    /// exactly one of three verdicts, per C-15: <see cref="AuditVerdict.Matches"/> a cited reference,
    /// <see cref="AuditVerdict.Wrong"/> against a cited reference (with the correct value named so RM-87 has the
    /// target in hand), or <see cref="AuditVerdict.Authored"/> — no external standard is adopted for it, so a stated
    /// default stands with that verdict recorded. § 10.18 governs here exactly as it governs code maths: fabricating
    /// a standard is forbidden as loudly as guessing a value, so a placement offset, a UV tolerance or a stylized
    /// bridge-preset rise is AUTHORED, not forced to "match" an invented spec.
    ///
    /// This is the inventory (RM-86's deliverable) as rows in code rather than a prose file, following RM-58 and
    /// RM-62: the register is the single source of truth, RM-87 reads <see cref="WrongNumbers"/> from it, and
    /// <see cref="CityDataAssetAuditGate"/> proves the register names every such asset in the tree and that no number
    /// is left unclassified. Pure — no <c>UnityEngine</c>/<c>UnityEditor</c> — so it compiles into the runtime
    /// assembly and the gate can enumerate it by reflection. </summary>
    public enum AuditVerdict
    {
        /// <summary> The stored value equals (within reason) a named real-world reference. Requires a reference. </summary>
        Matches,
        /// <summary> The stored value disagrees with a named reference. Requires both the reference and the correct
        /// value, which becomes RM-87's target. </summary>
        Wrong,
        /// <summary> No real-world standard is adopted for this number; a stated default stands. Legitimate under
        /// C-15 — it is NOT "unknown". Carries a note explaining why no standard governs it. </summary>
        Authored
    }


    /// <summary> One classified number inside a data asset. <see cref="Reference"/> is required for
    /// <see cref="AuditVerdict.Matches"/> and <see cref="AuditVerdict.Wrong"/>; <see cref="Corrected"/> is required
    /// only for <see cref="AuditVerdict.Wrong"/> (it is RM-87's target). For <see cref="AuditVerdict.Authored"/>,
    /// <see cref="Reference"/> carries the note that explains why no standard governs the value. Immutable. </summary>
    public readonly struct NumberAudit
    {
        public readonly string Field;
        public readonly string Value;
        public readonly AuditVerdict Verdict;
        public readonly string Reference;
        public readonly string Corrected;

        public NumberAudit(string field, string value, AuditVerdict verdict, string reference, string corrected)
        {
            Field = field; Value = value; Verdict = verdict; Reference = reference; Corrected = corrected;
        }

        /// <summary> The number matches a cited reference. </summary>
        public static NumberAudit Matches(string field, string value, string reference)
            => new NumberAudit(field, value, AuditVerdict.Matches, reference, null);

        /// <summary> The number is wrong against a cited reference; <paramref name="corrected"/> is RM-87's target. </summary>
        public static NumberAudit Wrong(string field, string value, string corrected, string reference)
            => new NumberAudit(field, value, AuditVerdict.Wrong, reference, corrected);

        /// <summary> No standard is adopted; the stated default stands. <paramref name="note"/> says why. </summary>
        public static NumberAudit Authored(string field, string value, string note)
            => new NumberAudit(field, value, AuditVerdict.Authored, note, null);

        /// <summary> True when the row is internally complete for its verdict: a field and value always; a reference
        /// for every verdict (the note stands in for Authored); a corrected value for Wrong. The gate fails on any
        /// row that is not well-formed, which is how "nothing left unknown" is enforced. </summary>
        public bool IsWellFormed()
        {
            if (string.IsNullOrEmpty(Field) || string.IsNullOrEmpty(Value)) return false;
            if (string.IsNullOrEmpty(Reference)) return false;
            if (Verdict == AuditVerdict.Wrong && string.IsNullOrEmpty(Corrected)) return false;
            return true;
        }
    }


    /// <summary> One audited asset: its AssetDatabase-relative path (forward slashes, under
    /// <c>Assets/RoadCityMaster/</c>), its family, and every classified number it holds. </summary>
    public readonly struct AssetAudit
    {
        public readonly string RelPath;
        public readonly string Family;
        public readonly IReadOnlyList<NumberAudit> Numbers;

        public AssetAudit(string relPath, string family, IReadOnlyList<NumberAudit> numbers)
        {
            RelPath = relPath; Family = family; Numbers = numbers;
        }
    }
}

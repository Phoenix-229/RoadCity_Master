// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-103's headless proof that changing what a road IS, on a road that already exists, is a safe
    /// operation: an author's overridden value survives a class change, a value they never touched follows the new
    /// class, changing class or surface and changing it back leaves the road EXACTLY as it was (member-by-member, no
    /// tolerance), the median follows the divider that decides it, and a value the cited table does not sanction is
    /// REPORTED with both numbers and the source of the limit rather than quietly clamped.
    ///
    /// It tests the same code an author's inspector runs. <c>Road.ApplyRoadClassDefaults</c> reads its fields into a
    /// <see cref="RoadTypeState"/>, calls <see cref="RoadTypeChange.Merge"/> and writes the result back, so the merge
    /// proved here is the merge that happens in the scene — there is no second "test" copy to drift from it. What this
    /// gate cannot see is the rebuild: whether the meshes of the previous type are gone is a question about a scene,
    /// and it is answered by the ordering in <c>Road.UpdateRoad</c>, which deletes the road's mainMeshes child before
    /// it decides whether the new class generates any. </summary>
    public static class RoadTypeChangeGate
    {
        private const string Prefix = "RCMROADTYPE";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Road type change")]
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

            CaseAnOverrideSurvivesAClassChange(failures);
            CaseUntouchedValuesFollowTheNewClass(failures);
            CaseClassChangeRoundTripsExactly(failures, sb);
            CaseSurfaceRoundTripsWithNoResidue(failures);
            CaseMedianFollowsTheDivider(failures);
            CaseConflictNamesBothValuesAndNeverClamps(failures, sb);
            CaseNothingIsReportedWhenNothingWasOverridden(failures);
            CaseTheChangeIsDeterministic(failures);

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


        #region "Seeds"
        /// <summary> A road sitting exactly on a class's defaults, built the way the tool builds one: a hard apply (no
        /// overrides preserved) IS the definition of "at the defaults", so the seed cannot disagree with the table. </summary>
        private static RoadTypeState AtDefaults(Road.RoadClassEnum c)
        {
            RoadTypeState seed = new RoadTypeState(c, 1, 1, false, 3.25f, true, 1f, CenterDividerTypeEnum.Markers,
                0f, 50f, true, 0.08f, true, SurfaceTypeClass.Sealed());
            return RoadTypeChange.Merge(seed, c, c, false);
        }


        private static RoadTypeState WithLaneWidth(RoadTypeState s, float laneWidth)
        {
            return new RoadTypeState(s.roadClass, s.lanesForward, s.lanesBackward, s.isOneWay, laneWidth,
                s.isShouldersEnabled, s.shoulderWidth, s.centerDivider, s.medianWidth, s.speedLimitKPH,
                s.isMaxGradeEnabled, s.maxGrade, s.isGeneratingRoadMesh, s.surface);
        }


        private static RoadTypeState WithSurface(RoadTypeState s, SurfaceTypeClass surface)
        {
            return new RoadTypeState(s.roadClass, s.lanesForward, s.lanesBackward, s.isOneWay, s.laneWidth,
                s.isShouldersEnabled, s.shoulderWidth, s.centerDivider, s.medianWidth, s.speedLimitKPH,
                s.isMaxGradeEnabled, s.maxGrade, s.isGeneratingRoadMesh, surface);
        }


        private static string Num(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }
        #endregion


        #region "Cases"
        /// <summary> A value the author moved off the outgoing class's default is theirs, and a class change does not
        /// take it away. This is the whole reason a road's type can be changed after the fact at all: without it, an
        /// author who had tuned a street to 3.0 m lanes would lose that work the moment they reclassified it. </summary>
        private static void CaseAnOverrideSurvivesAClassChange(List<string> failures)
        {
            RoadTypeState tuned = WithLaneWidth(AtDefaults(Road.RoadClassEnum.UrbanTwoLane), 3.0f);
            RoadTypeState after = RoadTypeChange.Merge(tuned, Road.RoadClassEnum.Motorway,
                Road.RoadClassEnum.UrbanTwoLane, true);

            Expect(after.laneWidth == 3.0f,
                "an authored 3.0 m lane must survive becoming a motorway (got " + Num(after.laneWidth) + " m).",
                failures);
            Expect(after.speedLimitKPH == 120f,
                "a value the author never touched must follow the new class (design speed got "
                + Num(after.speedLimitKPH) + " km/h, the motorway row says 120).", failures);
        }


        /// <summary> Everything untouched follows the incoming class — the numbers, the flags and the divider — because
        /// a class change that only moved some of them would leave a road that is neither of the two classes. </summary>
        private static void CaseUntouchedValuesFollowTheNewClass(List<string> failures)
        {
            RoadTypeState after = RoadTypeChange.Merge(AtDefaults(Road.RoadClassEnum.UrbanTwoLane),
                Road.RoadClassEnum.Motorway, Road.RoadClassEnum.UrbanTwoLane, true);

            Expect(after.laneWidth == 3.75f, "motorway lanes must come from the table (got "
                + Num(after.laneWidth) + " m, the row says 3.75).", failures);
            Expect(after.lanesForward + after.lanesBackward == 6, "the motorway row's 6 lanes must reach the road (got "
                + (after.lanesForward + after.lanesBackward).ToString(CultureInfo.InvariantCulture) + ").", failures);
            Expect(after.lanesForward == 3 && after.lanesBackward == 3,
                "6 lanes on a two-way road must split 3 and 3.", failures);
            Expect(after.centerDivider == CenterDividerTypeEnum.K_Rail,
                "the motorway row's barrier must reach the road (got " + after.centerDivider + ").", failures);
            Expect(after.maxGrade == 0.04f, "the motorway row's 4% grade limit must reach the road (got "
                + Num(after.maxGrade) + ").", failures);
        }


        /// <summary> Changing a road's class and changing it straight back leaves the road EXACTLY as it was — every
        /// member, under <c>==</c>, with no tolerance. A round trip is the honest test of an override-preserving merge
        /// because it fails in both directions: a refreshed value that is mistaken for an author's override sticks on
        /// the way back, and an override that is not recognised is lost on the way out. Asserted both on a road sitting
        /// on its defaults and on one the author has tuned. </summary>
        private static void CaseClassChangeRoundTripsExactly(List<string> failures, StringBuilder sb)
        {
            RoadTypeState start = AtDefaults(Road.RoadClassEnum.Arterial);
            RoadTypeState away = RoadTypeChange.Merge(start, Road.RoadClassEnum.HighwayDual,
                Road.RoadClassEnum.Arterial, true);
            RoadTypeState back = RoadTypeChange.Merge(away, Road.RoadClassEnum.Arterial,
                Road.RoadClassEnum.HighwayDual, true);

            bool exact = back == start;
            Expect(exact, "an arterial made a dual highway and made an arterial again must be the road it started as, "
                + "member for member.", failures);
            if (!exact)
            {
                sb.AppendLine(Prefix + " note   was " + start.Signature());
                sb.AppendLine(Prefix + " note   now " + back.Signature());
            }

            RoadTypeState tuned = WithLaneWidth(start, 3.1f);
            RoadTypeState tunedAway = RoadTypeChange.Merge(tuned, Road.RoadClassEnum.Motorway,
                Road.RoadClassEnum.Arterial, true);
            RoadTypeState tunedBack = RoadTypeChange.Merge(tunedAway, Road.RoadClassEnum.Arterial,
                Road.RoadClassEnum.Motorway, true);

            bool tunedExact = tunedBack == tuned;
            Expect(tunedExact, "the same round trip on a road whose lanes the author had tuned to 3.1 m must also be "
                + "lossless, and must still be 3.1 m at the end (got " + Num(tunedBack.laneWidth) + " m).", failures);
            if (!tunedExact)
            {
                sb.AppendLine(Prefix + " note   was " + tuned.Signature());
                sb.AppendLine(Prefix + " note   now " + tunedBack.Signature());
            }
        }


        /// <summary> Surface is its own axis. The cited class table has no surface column, so a class change carries
        /// the author's surface through untouched, and choosing a surface moves nothing about the class's geometry.
        /// Sealed to unsealed and back must therefore leave no residue at all. </summary>
        private static void CaseSurfaceRoundTripsWithNoResidue(List<string> failures)
        {
            RoadTypeState sealedRoad = AtDefaults(Road.RoadClassEnum.Collector);
            RoadTypeState unsealed = RoadTypeChange.Merge(WithSurface(sealedRoad, SurfaceTypeClass.Unsealed()),
                Road.RoadClassEnum.Collector, Road.RoadClassEnum.Collector, true);
            RoadTypeState resealed = RoadTypeChange.Merge(WithSurface(unsealed, SurfaceTypeClass.Sealed()),
                Road.RoadClassEnum.Collector, Road.RoadClassEnum.Collector, true);

            Expect(!SurfaceType.ValuesEqual(sealedRoad.surface, unsealed.surface),
                "choosing an unsealed surface must actually change the road's surface.", failures);
            Expect(unsealed.laneWidth == sealedRoad.laneWidth && unsealed.speedLimitKPH == sealedRoad.speedLimitKPH
                && unsealed.centerDivider == sealedRoad.centerDivider,
                "changing the surface must not move the class's geometry (lanes got " + Num(unsealed.laneWidth)
                + " m, was " + Num(sealedRoad.laneWidth) + " m).", failures);
            Expect(resealed == sealedRoad,
                "sealing an unsealed road again must leave no residue of the unsealed surface.", failures);
        }


        /// <summary> The median is not an independent number the author can leave stale: it is decided by the divider
        /// that occupies it. A concrete barrier is 0.61 m wide at its base (Caltrans Type K), so a road carrying one
        /// cannot have a narrower median than the thing standing in it; paint occupies nothing; and a road with no
        /// opposing traffic has nothing to divide, so its median is zero however wide the author left it. </summary>
        private static void CaseMedianFollowsTheDivider(List<string> failures)
        {
            RoadTypeState barrier = AtDefaults(Road.RoadClassEnum.Motorway);
            Expect(barrier.medianWidth >= 0.61f, "a motorway's concrete barrier needs at least its own 0.61 m base "
                + "(median got " + Num(barrier.medianWidth) + " m).", failures);

            RoadTypeState painted = AtDefaults(Road.RoadClassEnum.UrbanTwoLane);
            Expect(painted.medianWidth == 0f, "a painted centre line occupies no median (got "
                + Num(painted.medianWidth) + " m).", failures);

            RoadTypeState squeezed = RoadTypeChange.Merge(
                new RoadTypeState(Road.RoadClassEnum.Motorway, 3, 3, false, 3.75f, true, 3.5f,
                    CenterDividerTypeEnum.K_Rail, 0.2f, 120f, true, 0.04f, true, SurfaceTypeClass.Sealed()),
                Road.RoadClassEnum.Motorway, Road.RoadClassEnum.Motorway, true);
            Expect(squeezed.medianWidth >= 0.61f, "a median narrower than the barrier standing in it must be widened "
                + "to fit it, not left at 0.2 m (got " + Num(squeezed.medianWidth) + " m).", failures);

            RoadTypeState oneWay = RoadTypeChange.Merge(
                new RoadTypeState(Road.RoadClassEnum.Motorway, 3, 0, true, 3.75f, true, 3.5f,
                    CenterDividerTypeEnum.K_Rail, 5f, 120f, true, 0.04f, true, SurfaceTypeClass.Sealed()),
                Road.RoadClassEnum.Motorway, Road.RoadClassEnum.Motorway, true);
            Expect(oneWay.medianWidth == 0f, "a one-way road has no opposing traffic to divide, so an authored 5 m "
                + "median must go to zero (got " + Num(oneWay.medianWidth) + " m).", failures);
        }


        /// <summary> A value the cited table does not sanction is REPORTED — with the author's number, the number the
        /// class asks for, the band it falls outside and the reference that band came from — and the road keeps the
        /// author's number. Silently clamping 12 m lanes to 3.75 m would hide a decision the author made from the only
        /// person who can judge it. </summary>
        private static void CaseConflictNamesBothValuesAndNeverClamps(List<string> failures, StringBuilder sb)
        {
            RoadTypeState wide = WithLaneWidth(AtDefaults(Road.RoadClassEnum.UrbanTwoLane), 12f);
            List<TypeOverride> rows = RoadTypeChange.Overrides(wide, Road.RoadClassEnum.UrbanTwoLane,
                Road.RoadClassEnum.Motorway);

            TypeOverride conflict = default(TypeOverride);
            bool found = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].isConflict) { conflict = rows[i]; found = true; break; }
            }

            Expect(found, "a 12 m lane is wider than any row of the class table uses and must be reported as a "
                + "conflict (" + rows.Count.ToString(CultureInfo.InvariantCulture) + " rows, none in conflict).",
                failures);
            if (found)
            {
                string msg = conflict.Message();
                Expect(conflict.field == "laneWidth", "the conflict must name the field it is about (got "
                    + conflict.field + ").", failures);
                Expect(msg.Contains("12"), "the conflict must state the author's own number: " + msg, failures);
                Expect(msg.Contains("3.75"), "the conflict must state what the new class asks for: " + msg, failures);
                Expect(!string.IsNullOrEmpty(conflict.limit),
                    "the conflict must state the band the value falls outside: " + msg, failures);
                Expect(!string.IsNullOrEmpty(conflict.source),
                    "the conflict must cite where that band comes from: " + msg, failures);
                sb.AppendLine(Prefix + " note   " + msg);
            }

            RoadTypeState after = RoadTypeChange.Merge(wide, Road.RoadClassEnum.Motorway,
                Road.RoadClassEnum.UrbanTwoLane, true);
            Expect(after.laneWidth == 12f, "a reported value must be left exactly as the author set it, never quietly "
                + "clamped (got " + Num(after.laneWidth) + " m instead of 12).", failures);
        }


        /// <summary> The other half of the report: a road sitting on its class's defaults owns nothing, so changing its
        /// class must say nothing at all. A report that fired on every field would train an author to ignore it, and
        /// the one line that mattered would go past unread with the rest. </summary>
        private static void CaseNothingIsReportedWhenNothingWasOverridden(List<string> failures)
        {
            List<TypeOverride> rows = RoadTypeChange.Overrides(AtDefaults(Road.RoadClassEnum.Collector),
                Road.RoadClassEnum.Collector, Road.RoadClassEnum.Arterial);

            StringBuilder spurious = new StringBuilder();
            for (int i = 0; i < rows.Count; i++) { spurious.Append(' ').Append(rows[i].Message()); }

            Expect(rows.Count == 0, "a collector still on its own defaults owns nothing, so becoming an arterial must "
                + "report nothing (got " + rows.Count.ToString(CultureInfo.InvariantCulture) + " rows:"
                + spurious + ").", failures);
            Expect(RoadTypeChange.OverrideReport(AtDefaults(Road.RoadClassEnum.Collector),
                Road.RoadClassEnum.Collector, Road.RoadClassEnum.Arterial).Length == 0,
                "with nothing overridden the report text must be empty, not a heading with no rows under it.", failures);
        }


        /// <summary> C-06: the same change, asked for twice, gives the same road and the same words. A merge that read
        /// a dictionary's enumeration order or a set could reorder its report between two runs of the same edit, and an
        /// author diffing a scene would see churn that no one authored. </summary>
        private static void CaseTheChangeIsDeterministic(List<string> failures)
        {
            RoadTypeState seed = WithLaneWidth(AtDefaults(Road.RoadClassEnum.UrbanFourLane), 3.4f);

            string first = RoadTypeChange.Merge(seed, Road.RoadClassEnum.Motorway,
                Road.RoadClassEnum.UrbanFourLane, true).Signature();
            string second = RoadTypeChange.Merge(seed, Road.RoadClassEnum.Motorway,
                Road.RoadClassEnum.UrbanFourLane, true).Signature();
            Expect(first == second, "the same class change must produce the same road twice (C-06).", failures);

            string report1 = RoadTypeChange.OverrideReport(seed, Road.RoadClassEnum.UrbanFourLane,
                Road.RoadClassEnum.Motorway);
            string report2 = RoadTypeChange.OverrideReport(seed, Road.RoadClassEnum.UrbanFourLane,
                Road.RoadClassEnum.Motorway);
            Expect(report1 == report2, "the override report must be identical on a repeat, line for line (C-06).",
                failures);
            Expect(report1.Length > 0, "an authored 3.4 m lane IS an override and must appear in the report.",
                failures);
        }
        #endregion
    }
}
#endif

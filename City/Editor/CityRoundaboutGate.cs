// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-28's gate: proves the NCHRP-672 roundabout SIZING and the refusal paths without a scene. It
    /// asserts that the circulatory width follows the entry-width relation and the central island falls out of the
    /// inscribed circle minus the two circulatory widths (and is flagged invalid when it would be non-positive);
    /// that the authored radii and splitter length sit inside the guide's ranges; that legs closer than the minimum
    /// separation are REFUSED with the offending angle reported while evenly spaced legs pass; that an entry with
    /// too large a fastest-path radius fails the DEFLECTION requirement; that the legs resolve to one circulatory
    /// plane and a leg that would kink to reach it is flagged with its gradient; and that the sizing is
    /// bit-identical on repeat (C-06). The structural fork (SplineC has no closed loop, so the circulatory way is
    /// arcs/mesh) and all scene geometry are the deferred half. Report lines are prefixed RCMROUND. </summary>
    public static class CityRoundaboutGate
    {
        [MenuItem("Window/Road City Master/Testing/City roundabout (NCHRP sizing)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityRoundaboutGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMROUND report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMROUND BEGIN\n");
            List<string> failures = new List<string>();

            CaseCirculatoryAndIsland(builder, failures);
            CaseAuthoredRangesFromGuide(builder, failures);
            CaseLegSeparationRefuses(builder, failures);
            CaseEntryDeflection(builder, failures);
            CaseSplitterIsland(builder, failures);
            CasePlaneResolveFlagsKink(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMROUND FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMROUND result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMROUND END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Circulatory width = factor x entry width; central island = ICD - 2 x circulatory width, valid
        /// when positive and flagged invalid when the ring would swallow the island. </summary>
        private static void CaseCirculatoryAndIsland(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default(); // ICD 40, entry 4.5, factor 1.1
            double circ = RoundaboutGeometry.CirculatoryWidthMetres(cls);
            Expect(Math.Abs(circ - 1.1 * 4.5) < 1e-9,
                "sizing: circulatory width was not factor x entry width", failures);
            double island = RoundaboutGeometry.CentralIslandDiameterMetres(cls);
            Expect(Math.Abs(island - (40.0 - 2.0 * circ)) < 1e-9,
                "sizing: central island was not ICD minus two circulatory widths", failures);
            Expect(RoundaboutGeometry.CentralIslandIsValid(cls), "sizing: a valid single-lane island read invalid",
                failures);

            RoundaboutClass tooSmall = RoundaboutClass.Default();
            tooSmall.inscribedCircleDiameterMetres = 8f; // ring wider than the circle allows
            Expect(!RoundaboutGeometry.CentralIslandIsValid(tooSmall),
                "sizing: an impossible island (ICD too small) was not flagged invalid", failures);
            builder.Append("RCMROUND ").Append("sizing".PadRight(22))
                   .Append("circulatory + central island sized from ICD/entry (NCHRP)\n");
        }

        /// <summary> The authored radii and splitter length sit inside the cited NCHRP single-lane ranges, and the
        /// exit radius is not tighter than the entry radius. </summary>
        private static void CaseAuthoredRangesFromGuide(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            Expect(cls.inscribedCircleDiameterMetres >= 27f && cls.inscribedCircleDiameterMetres <= 46f,
                "authored: ICD is outside the NCHRP single-lane 27-46 m range", failures);
            Expect(cls.entryRadiusMetres >= 10f && cls.entryRadiusMetres <= 30f,
                "authored: entry radius is outside the NCHRP 10-30 m range", failures);
            Expect(cls.exitRadiusMetres >= cls.entryRadiusMetres,
                "authored: exit radius is tighter than the entry radius", failures);
            Expect(cls.minSplitterIslandLengthMetres >= 15f,
                "authored: minimum splitter island is below the NCHRP 15 m floor", failures);
            builder.Append("RCMROUND ").Append("authored-ranges".PadRight(22))
                   .Append("radii + splitter length within NCHRP ranges\n");
        }

        /// <summary> Evenly spaced legs pass; legs closer than the minimum separation are refused and the offending
        /// angle is reported. </summary>
        private static void CaseLegSeparationRefuses(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default(); // min separation 45 deg
            RoundaboutGeometry.LegSeparationCheck even = RoundaboutGeometry.CheckLegSeparations(
                new List<double> { 0.0, 90.0, 180.0, 270.0 }, cls);
            Expect(even.accepted && Math.Abs(even.worstSeparationDeg - 90.0) < 1e-9,
                "legs: four evenly spaced legs (90 deg apart) were refused", failures);

            RoundaboutGeometry.LegSeparationCheck shallow = RoundaboutGeometry.CheckLegSeparations(
                new List<double> { 0.0, 20.0, 180.0 }, cls);
            Expect(!shallow.accepted, "legs: two legs only 20 deg apart were not refused", failures);
            Expect(Math.Abs(shallow.worstSeparationDeg - 20.0) < 1e-9,
                "legs: the refusal did not report the 20 deg offending separation", failures);
            builder.Append("RCMROUND ").Append("leg-separation".PadRight(22))
                   .Append("even legs pass, too-shallow legs refused with the angle\n");
        }

        /// <summary> Deflection: a tight fastest-path radius passes; too large a radius (a straight, fast path)
        /// fails; a fully deflected path (R1 <= 0) trivially passes. </summary>
        private static void CaseEntryDeflection(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default(); // cap 100 m
            Expect(RoundaboutGeometry.EntryDeflectionAdequate(60.0, cls),
                "deflect: a 60 m fastest-path radius under the cap was rejected", failures);
            Expect(!RoundaboutGeometry.EntryDeflectionAdequate(150.0, cls),
                "deflect: a 150 m fastest-path radius over the cap was accepted (too fast)", failures);
            Expect(RoundaboutGeometry.EntryDeflectionAdequate(0.0, cls),
                "deflect: a fully deflected path was rejected", failures);
            builder.Append("RCMROUND ").Append("entry-deflection".PadRight(22))
                   .Append("fastest-path radius capped so the entry slows a driver\n");
        }

        /// <summary> Splitter island adequacy against the class minimum. </summary>
        private static void CaseSplitterIsland(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            Expect(RoundaboutGeometry.SplitterIslandAdequate(20.0, cls),
                "splitter: a 20 m island above the minimum was rejected", failures);
            Expect(!RoundaboutGeometry.SplitterIslandAdequate(10.0, cls),
                "splitter: a 10 m island below the 15 m minimum was accepted", failures);
            builder.Append("RCMROUND ").Append("splitter-island".PadRight(22))
                   .Append("splitter island length checked against the NCHRP floor\n");
        }

        /// <summary> The legs resolve to one circulatory plane (the mean elevation); a leg whose approach gradient
        /// change to reach it exceeds the limit is flagged with its gradient rather than kinked. </summary>
        private static void CasePlaneResolveFlagsKink(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default(); // max approach gradient change 4 %
            RoundaboutGeometry.PlaneResolve level = RoundaboutGeometry.ResolveCirculatoryPlane(
                new List<double> { 10.0, 10.0, 10.0, 10.0 },
                new List<double> { 50.0, 50.0, 50.0, 50.0 }, cls);
            Expect(Math.Abs(level.planeElevationMetres - 10.0) < 1e-9 && level.allLegsWithinGradient,
                "plane: a level set of legs did not resolve flat and within gradient", failures);

            RoundaboutGeometry.PlaneResolve stepped = RoundaboutGeometry.ResolveCirculatoryPlane(
                new List<double> { 0.0, 0.0, 0.0, 20.0 },
                new List<double> { 50.0, 50.0, 50.0, 50.0 }, cls);
            Expect(Math.Abs(stepped.planeElevationMetres - 5.0) < 1e-9,
                "plane: the circulatory plane was not the mean leg elevation", failures);
            Expect(!stepped.allLegsWithinGradient && stepped.worstLegIndex == 3,
                "plane: a leg that would kink to reach the plane was not flagged as the worst", failures);
            builder.Append("RCMROUND ").Append("one-plane".PadRight(22))
                   .Append("legs resolve to one plane, a kinking leg is flagged\n");
        }

        /// <summary> The sizing is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            double a = RoundaboutGeometry.CentralIslandDiameterMetres(cls);
            double b = RoundaboutGeometry.CentralIslandDiameterMetres(cls);
            RoundaboutGeometry.PlaneResolve p1 = RoundaboutGeometry.ResolveCirculatoryPlane(
                new List<double> { 1.0, 3.0, 5.0 }, new List<double> { 40.0, 40.0, 40.0 }, cls);
            RoundaboutGeometry.PlaneResolve p2 = RoundaboutGeometry.ResolveCirculatoryPlane(
                new List<double> { 1.0, 3.0, 5.0 }, new List<double> { 40.0, 40.0, 40.0 }, cls);
            Expect(a == b && p1.planeElevationMetres == p2.planeElevationMetres
                   && p1.worstGradientChangePercent == p2.worstGradientChangePercent,
                "determinism: two identical roundabout sizings produced different values", failures);
            builder.Append("RCMROUND ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical sizing\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

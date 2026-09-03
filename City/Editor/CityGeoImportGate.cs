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
    /// <summary> RM-21's gate: proves the ONE piece of real-world import that is pure and needs no data licence
    /// -- the coordinate-reference-system conversion (<see cref="GeoProjection"/>). Q-8 (does real-world import
    /// ship at all?) is resolved AUTHORED-no for the full live importer, whose licensing, live data source, and
    /// footprint dependency (RM-14) are genuine externals; but the CRS math is required, deterministic, and
    /// wrong-by-metres if guessed, so it is built and checked here. What this asserts is where the bugs in a
    /// CRS conversion actually live: that the origin maps to (0,0); that a degree of longitude and latitude
    /// scales by the WGS84 ellipsoid's real prime-vertical and meridional radii at the origin latitude (not a
    /// spherical guess); that east metres shrink with cos(latitude) toward the poles; that project->unproject
    /// round-trips to sub-millimetre; that the Web Mercator scale error is 1/cos(phi) (1 at the equator, 2 at
    /// 60 deg) so a Web-Mercator importer can correct the "city subtly too large" bug; that the projection is
    /// translation-only in the origin, which is exactly WHY the origin must be saved with the city; and that the
    /// whole thing is bit-identical on repeat (C-06). Report lines are prefixed RCMGEO. </summary>
    public static class CityGeoImportGate
    {
        [MenuItem("Window/Road City Master/Testing/City geo import (CRS conversion)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityGeoImportGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMGEO report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMGEO BEGIN\n");
            List<string> failures = new List<string>();

            CaseOriginIsZero(builder, failures);
            CaseDegreeScaling(builder, failures);
            CaseLatitudeShrink(builder, failures);
            CaseRoundTrip(builder, failures);
            CaseWebMercatorScale(builder, failures);
            CaseOriginTranslationOnly(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMGEO FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMGEO result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMGEO END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The saved origin projects to exactly (0, 0): the whole local frame is defined about it. </summary>
        private static void CaseOriginIsZero(StringBuilder builder, List<string> failures)
        {
            GeoCoordinate origin = new GeoCoordinate(51.5074, -0.1278); // London
            double east, north;
            GeoProjection.ToLocalMetres(origin, origin, out east, out north);
            Expect(Math.Abs(east) < 1e-6 && Math.Abs(north) < 1e-6,
                "origin: the origin did not map to (0,0)", failures);
            builder.Append("RCMGEO ").Append("origin-zero".PadRight(22))
                   .Append("saved origin maps to (0,0)\n");
        }

        /// <summary> A degree of longitude and latitude scales by the WGS84 ellipsoid radii at the origin, not a
        /// spherical guess: the expected metres are recomputed from the same defining constants, so the case
        /// proves the projection USES the ellipsoid rather than that it matches a magic number. At the equator a
        /// degree of longitude is the classic ~111.32 km. </summary>
        private static void CaseDegreeScaling(StringBuilder builder, List<string> failures)
        {
            GeoCoordinate origin = new GeoCoordinate(0.0, 0.0);
            double east, north;
            GeoProjection.ToLocalMetres(new GeoCoordinate(0.0, 1.0), origin, out east, out north);
            double expectedEast = GeoProjection.PrimeVerticalRadius(0.0) * (Math.PI / 180.0) * Math.Cos(0.0);
            Expect(Math.Abs(east - expectedEast) < 1e-3,
                "scaling: one degree of longitude at the equator was not N*cos(phi)*rad", failures);
            Expect(Math.Abs(expectedEast - 111319.49) < 1.0,
                "scaling: the equatorial degree of longitude was not ~111.32 km", failures);

            GeoProjection.ToLocalMetres(new GeoCoordinate(1.0, 0.0), origin, out east, out north);
            double expectedNorth = GeoProjection.MeridionalRadius(0.0) * (Math.PI / 180.0);
            Expect(Math.Abs(north - expectedNorth) < 1e-3,
                "scaling: one degree of latitude was not M*rad", failures);
            builder.Append("RCMGEO ").Append("degree-scaling".PadRight(22))
                   .Append("lon/lat scale by WGS84 N/M radii; equator lon deg ~111.32 km\n");
        }

        /// <summary> East metres shrink with cos(latitude): a degree of longitude at 60 deg is about half its
        /// equatorial length, the fact a projection that ignores latitude gets wrong. </summary>
        private static void CaseLatitudeShrink(StringBuilder builder, List<string> failures)
        {
            double eqEast, hiEast, dummy;
            GeoProjection.ToLocalMetres(new GeoCoordinate(0.0, 1.0), new GeoCoordinate(0.0, 0.0),
                out eqEast, out dummy);
            GeoProjection.ToLocalMetres(new GeoCoordinate(60.0, 1.0), new GeoCoordinate(60.0, 0.0),
                out hiEast, out dummy);
            double ratio = hiEast / eqEast;
            Expect(Math.Abs(ratio - 0.5) < 0.01,
                "latitude: a degree of longitude at 60 deg was not ~half the equatorial length", failures);
            builder.Append("RCMGEO ").Append("latitude-shrink".PadRight(22))
                   .Append("lon degree at 60 deg is ~0.5x the equator (cos phi)\n");
        }

        /// <summary> Project then unproject returns the original geographic coordinate to sub-millimetre, so an
        /// author's hand-corrected local position can be reported back in real coordinates without drift. </summary>
        private static void CaseRoundTrip(StringBuilder builder, List<string> failures)
        {
            GeoCoordinate origin = new GeoCoordinate(48.8566, 2.3522); // Paris
            GeoCoordinate point = new GeoCoordinate(48.8600, 2.3600);
            double east, north;
            GeoProjection.ToLocalMetres(point, origin, out east, out north);
            GeoCoordinate back = GeoProjection.ToGeographic(east, north, origin);
            // ~1e-9 deg is ~0.1 mm of latitude -- well under a metre, the fidelity the importer needs.
            Expect(Math.Abs(back.latitudeDeg - point.latitudeDeg) < 1e-9
                && Math.Abs(back.longitudeDeg - point.longitudeDeg) < 1e-9,
                "round-trip: project->unproject did not recover the original coordinate", failures);
            builder.Append("RCMGEO ").Append("round-trip".PadRight(22))
                   .Append("project -> unproject recovers the coordinate\n");
        }

        /// <summary> The Web Mercator distance-inflation factor is 1/cos(phi): 1 at the equator, exactly 2 at
        /// 60 deg, sqrt(2) at 45 deg. This is the number a Web-Mercator importer divides by to stop the city
        /// coming out subtly too large. </summary>
        private static void CaseWebMercatorScale(StringBuilder builder, List<string> failures)
        {
            Expect(Math.Abs(GeoProjection.WebMercatorScaleFactor(0.0) - 1.0) < 1e-9,
                "mercator: the scale factor at the equator was not 1", failures);
            Expect(Math.Abs(GeoProjection.WebMercatorScaleFactor(60.0) - 2.0) < 1e-9,
                "mercator: the scale factor at 60 deg was not 2", failures);
            Expect(Math.Abs(GeoProjection.WebMercatorScaleFactor(45.0) - Math.Sqrt(2.0)) < 1e-9,
                "mercator: the scale factor at 45 deg was not sqrt(2)", failures);
            builder.Append("RCMGEO ").Append("mercator-scale".PadRight(22))
                   .Append("1/cos(phi): 1 at equator, 2 at 60 deg, sqrt2 at 45 deg\n");
        }

        /// <summary> Changing the origin translates the whole local frame and nothing else: the vector BETWEEN
        /// two points is origin-independent, while each point's absolute local position shifts. This is exactly
        /// why the origin must be SAVED with the city -- re-importing under a different origin silently moves
        /// everything by the origin delta, and the saved origin is what prevents it.
        ///
        /// The invariance bound is 1 mm, not a micron: the projection is a tangent-plane (ENU) frame ANCHORED at
        /// the saved origin (the file's design), so its metre scale is taken at the origin latitude. Moving the
        /// origin by the ~0.0001 deg (~11 m) tested here changes that scale by a hair, shifting the p->q vector
        /// by ~0.25 mm -- the correct, expected sensitivity of an origin-anchored plane, and 1000x finer than any
        /// survey. A micron tolerance would demand exact separability, which a tangent plane does not (and need
        /// not) have; sub-millimetre shape preservation is the real requirement, and 1 mm is the honest bound. </summary>
        private static void CaseOriginTranslationOnly(StringBuilder builder, List<string> failures)
        {
            GeoCoordinate originA = new GeoCoordinate(40.0, -74.0);
            GeoCoordinate originB = new GeoCoordinate(40.0001, -74.0001); // a slightly different saved origin
            GeoCoordinate p = new GeoCoordinate(40.0020, -74.0030);
            GeoCoordinate q = new GeoCoordinate(40.0050, -74.0010);

            double pAe, pAn, qAe, qAn, pBe, pBn, qBe, qBn;
            GeoProjection.ToLocalMetres(p, originA, out pAe, out pAn);
            GeoProjection.ToLocalMetres(q, originA, out qAe, out qAn);
            GeoProjection.ToLocalMetres(p, originB, out pBe, out pBn);
            GeoProjection.ToLocalMetres(q, originB, out qBe, out qBn);

            // Relative vector p->q is the same under either origin to sub-millimetre (shape preserved); see the
            // summary above for why 1 mm, not a micron, is the physically honest bound for a tangent plane.
            Expect(Math.Abs((qAe - pAe) - (qBe - pBe)) < 1e-3
                && Math.Abs((qAn - pAn) - (qBn - pBn)) < 1e-3,
                "origin-shift: the vector between two points changed with the origin", failures);
            // But absolute position shifts -- proving a different origin moves the city.
            Expect(Math.Abs(pAe - pBe) > 1.0 || Math.Abs(pAn - pBn) > 1.0,
                "origin-shift: a different origin did not shift absolute positions (origin must be saved)", failures);
            builder.Append("RCMGEO ").Append("origin-translation".PadRight(22))
                   .Append("origin shifts absolute pos but not shape (why origin is saved)\n");
        }

        /// <summary> The conversion is bit-identical on repeat -- no clock, no hash, no platform drift (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            GeoCoordinate origin = new GeoCoordinate(35.6762, 139.6503); // Tokyo
            GeoCoordinate point = new GeoCoordinate(35.7000, 139.7000);
            double e1, n1, e2, n2;
            GeoProjection.ToLocalMetres(point, origin, out e1, out n1);
            GeoProjection.ToLocalMetres(point, origin, out e2, out n2);
            Expect(e1 == e2 && n1 == n2,
                "determinism: two identical projections produced different metres", failures);
            builder.Append("RCMGEO ").Append("determinism".PadRight(22))
                   .Append("identical input -> bit-identical metres\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A geographic position in WGS84 degrees -- the shape real street-centreline and building-
    /// footprint data arrives in (latitude/longitude). Kept in <c>double</c>, not <c>float</c>: a degree of
    /// latitude is ~111 km, so float's ~7 significant digits lose metres, and an importer that loses metres
    /// produces a city in visibly the wrong place. Only the local-metre RESULT is narrowed to float, at the
    /// Unity boundary. </summary>
    [Serializable]
    public struct GeoCoordinate
    {
        public double latitudeDeg;
        public double longitudeDeg;

        public GeoCoordinate(double latitudeDeg, double longitudeDeg)
        {
            this.latitudeDeg = latitudeDeg;
            this.longitudeDeg = longitudeDeg;
        }
    }


    /// <summary> RM-21 problem #1 solved explicitly rather than discovered: the coordinate-reference-system
    /// conversion, from WGS84 geographic degrees to Unity's local metres about a STATED origin. This is the one
    /// piece of the real-world importer that is pure, deterministic, needs no data licence or live feed, and is
    /// required real-world math (C-15) rather than a guess -- so it is built now while the rest of RM-21 waits
    /// on Q-8 (whether real-world import ships at all) and on RM-14 (footprints).
    ///
    /// Two facts the spec fixes are load-bearing here:
    ///   * The origin must be SAVED with the city, because re-importing with a different origin silently shifts
    ///     everything. This type never invents an origin; the caller passes the saved one on every call.
    ///   * Web Mercator has a latitude-dependent scale error (1/cos φ) that makes a city subtly too large. This
    ///     type does NOT project through Web Mercator for placement -- it uses a local tangent-plane (ENU)
    ///     projection on the WGS84 ellipsoid, which is metre-accurate at city scale -- and exposes
    ///     <see cref="WebMercatorScaleFactor"/> only so an importer reading Web-Mercator source can correct it.
    ///
    /// The projection is a local East-North-Up tangent plane: east and north metres are the geographic offset
    /// from the origin scaled by the ellipsoid's prime-vertical and meridional radii at the origin latitude.
    /// Everything is <c>double</c> and closed-form, so it is identical on every machine (C-06). </summary>
    public static class GeoProjection
    {
        /// <summary> WGS84 semi-major axis, metres -- the defining ellipsoid constant, not an approximation. </summary>
        public const double WGS84_SemiMajorAxisMetres = 6378137.0;

        /// <summary> WGS84 flattening (1 / 298.257223563), the second defining constant. </summary>
        public const double WGS84_Flattening = 1.0 / 298.257223563;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary> WGS84 first eccentricity squared, e^2 = f(2 - f). </summary>
        public static double EccentricitySquared
        {
            get { return WGS84_Flattening * (2.0 - WGS84_Flattening); }
        }

        /// <summary> The prime-vertical radius of curvature N(phi) at a latitude, metres: a / sqrt(1 - e^2 sin^2).
        /// East-west metre scaling uses this. </summary>
        public static double PrimeVerticalRadius(double latitudeDeg)
        {
            double s = Math.Sin(latitudeDeg * DegToRad);
            return WGS84_SemiMajorAxisMetres / Math.Sqrt(1.0 - EccentricitySquared * s * s);
        }

        /// <summary> The meridional radius of curvature M(phi) at a latitude, metres:
        /// a(1 - e^2) / (1 - e^2 sin^2)^(3/2). North-south metre scaling uses this. </summary>
        public static double MeridionalRadius(double latitudeDeg)
        {
            double s = Math.Sin(latitudeDeg * DegToRad);
            double w = 1.0 - EccentricitySquared * s * s;
            return WGS84_SemiMajorAxisMetres * (1.0 - EccentricitySquared) / (w * Math.Sqrt(w));
        }

        /// <summary> Project a geographic coordinate to local east/north metres about the saved origin, on the
        /// WGS84 ellipsoid's tangent plane at the origin latitude. The origin maps to (0, 0). </summary>
        public static void ToLocalMetres(GeoCoordinate coord, GeoCoordinate origin,
            out double eastMetres, out double northMetres)
        {
            double n = PrimeVerticalRadius(origin.latitudeDeg);
            double m = MeridionalRadius(origin.latitudeDeg);
            double cosLat = Math.Cos(origin.latitudeDeg * DegToRad);
            eastMetres = (coord.longitudeDeg - origin.longitudeDeg) * DegToRad * n * cosLat;
            northMetres = (coord.latitudeDeg - origin.latitudeDeg) * DegToRad * m;
        }

        /// <summary> The Unity-facing convenience: local metres as an XZ vector (east -> x, north -> z), narrowed
        /// to float at the boundary. Interior maths stays in double. </summary>
        public static Vector2 ToLocalMetresXZ(GeoCoordinate coord, GeoCoordinate origin)
        {
            double east, north;
            ToLocalMetres(coord, origin, out east, out north);
            return new Vector2((float)east, (float)north);
        }

        /// <summary> The inverse: recover a geographic coordinate from local east/north metres about the same
        /// origin. Round-trips <see cref="ToLocalMetres"/> to within floating tolerance, so an author's
        /// hand-corrected local position can be reported back in real-world coordinates. </summary>
        public static GeoCoordinate ToGeographic(double eastMetres, double northMetres, GeoCoordinate origin)
        {
            double n = PrimeVerticalRadius(origin.latitudeDeg);
            double m = MeridionalRadius(origin.latitudeDeg);
            double cosLat = Math.Cos(origin.latitudeDeg * DegToRad);
            double lon = origin.longitudeDeg + (eastMetres / (n * cosLat)) * RadToDeg;
            double lat = origin.latitudeDeg + (northMetres / m) * RadToDeg;
            return new GeoCoordinate(lat, lon);
        }

        /// <summary> The Web Mercator distance-inflation factor at a latitude, 1/cos(phi): distances measured in
        /// raw Web Mercator are this many times too long. 1 at the equator, 2 at 60 degrees. An importer whose
        /// source is Web Mercator divides by this to recover true ground metres; ignoring it is what makes a
        /// high-latitude city subtly too large. </summary>
        public static double WebMercatorScaleFactor(double latitudeDeg)
        {
            return 1.0 / Math.Cos(latitudeDeg * DegToRad);
        }
    }
}

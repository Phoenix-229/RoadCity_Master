#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One placed scatter instance in the city's world plane (metres). The bake stores these concrete
    /// positions; nothing is a density number the play scene has to re-evaluate (RM-47's baked-not-runtime rule). </summary>
    public struct ScatterPoint
    {
        public double x;
        public double z;
        public ScatterPoint(double x, double z) { this.x = x; this.z = z; }
    }

    /// <summary> A rectangular keep-out taken from real geometry -- a carriageway, a building footprint, a driveway,
    /// a cleared junction. Exclusion comes from the geometry itself, not a second painted mask (RM-47). </summary>
    public struct ScatterRect
    {
        public double minX, minZ, maxX, maxZ;
        public ScatterRect(double minX, double minZ, double maxX, double maxZ)
        {
            this.minX = minX; this.minZ = minZ; this.maxX = maxX; this.maxZ = maxZ;
        }
        public bool Contains(double x, double z)
        {
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }
    }

    /// <summary> RM-47's verifiable core: resolve density-driven placement into a CONCRETE, ordered instance list at
    /// bake time, seeded per element so it never depends on the order the pass walked the city. Two units, both from
    /// the real trade: LINEAR is a spacing in metres with jitter as a fraction of the spacing (a street's trees at
    /// meterSep, matching EdgeObjectMaker); AREA is instances per 100 m^2 with a minimum-separation rejection (verge
    /// or plaza planting). Exclusion is by geometry, not a mask. Thinning by preset drops instances from the ordered
    /// list -- deterministic and reversible -- rather than re-scattering at a lower density, which is neither. The
    /// actual placement into streaming cells, prefab-reference resolution, and the true geometry sources are the
    /// deferred scene half; this is the placement maths and its counts. </summary>
    public static class ScatterBake
    {
        /// <summary> Place instances along a line of the given length: one per spacing cell, centred, with a jitter of
        /// +/- (jitterFraction * spacing), clamped into [0, length]. Returns the along-line distances in metres, in
        /// increasing nominal order. Seeded from the city seed and the ELEMENT id, so a street's row is identical
        /// whether that street is scattered first or last. </summary>
        public static IList<double> ScatterAlongLine(ulong citySeed, string elementId,
            double lengthMetres, double spacingMetres, double jitterFraction)
        {
            List<double> alongs = new List<double>();
            if (lengthMetres <= 0.0 || spacingMetres <= 0.0) { return alongs; }
            if (jitterFraction < 0.0) { jitterFraction = 0.0; }

            DeterministicRandom rng = DeterministicRandom.ForElement(citySeed, elementId);
            double half = 0.5 * spacingMetres;
            for (int i = 0; ; i++)
            {
                double nominal = half + i * spacingMetres;
                if (nominal > lengthMetres) { break; }
                double u = rng.NextDouble();               // one draw per cell, in nominal order
                double jitter = (u * 2.0 - 1.0) * jitterFraction * spacingMetres;
                double along = nominal + jitter;
                if (along < 0.0) { along = 0.0; }
                if (along > lengthMetres) { along = lengthMetres; }
                alongs.Add(along);
            }
            return alongs;
        }

        /// <summary> Place instances over a rectangle at the given areal density (instances per 100 m^2), rejecting any
        /// candidate closer than minSeparationMetres to an already-accepted instance. Candidates are drawn from the
        /// per-element stream, so the accepted set is identical regardless of when this area is baked. </summary>
        public static IList<ScatterPoint> ScatterOverArea(ulong citySeed, string elementId,
            double widthMetres, double depthMetres, double instancesPer100m2, double minSeparationMetres)
        {
            List<ScatterPoint> points = new List<ScatterPoint>();
            if (widthMetres <= 0.0 || depthMetres <= 0.0 || instancesPer100m2 <= 0.0) { return points; }

            double areaM2 = widthMetres * depthMetres;
            int target = (int)Math.Round(areaM2 / 100.0 * instancesPer100m2, MidpointRounding.AwayFromZero);
            if (target <= 0) { return points; }

            DeterministicRandom rng = DeterministicRandom.ForElement(citySeed, elementId);
            double minSep2 = minSeparationMetres > 0.0 ? minSeparationMetres * minSeparationMetres : 0.0;
            int maxAttempts = target * 40 + 64;  // bounded so the bake always terminates
            for (int attempt = 0; attempt < maxAttempts && points.Count < target; attempt++)
            {
                double x = rng.NextRange(0.0, widthMetres);
                double z = rng.NextRange(0.0, depthMetres);
                if (minSep2 > 0.0 && !FarEnough(points, x, z, minSep2)) { continue; }
                points.Add(new ScatterPoint(x, z));
            }
            return points;
        }

        private static bool FarEnough(List<ScatterPoint> pts, double x, double z, double minSep2)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                double dx = pts[i].x - x;
                double dz = pts[i].z - z;
                if (dx * dx + dz * dz < minSep2) { return false; }
            }
            return true;
        }

        /// <summary> Remove any point that falls inside a geometry keep-out, preserving the order of the survivors.
        /// Exclusion is read from the geometry (carriageway/footprint/driveway/junction), never a second mask. </summary>
        public static IList<ScatterPoint> FilterExcluded(IList<ScatterPoint> points, IList<ScatterRect> exclusions)
        {
            List<ScatterPoint> kept = new List<ScatterPoint>(points == null ? 0 : points.Count);
            if (points == null) { return kept; }
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsExcluded(points[i].x, points[i].z, exclusions)) { kept.Add(points[i]); }
            }
            return kept;
        }

        public static bool IsExcluded(double x, double z, IList<ScatterRect> exclusions)
        {
            if (exclusions == null) { return false; }
            for (int i = 0; i < exclusions.Count; i++)
            {
                if (exclusions[i].Contains(x, z)) { return true; }
            }
            return false;
        }

        /// <summary> Thin an ordered instance list to (about) keepFraction of its length by DROPPING evenly-spaced
        /// entries -- a deterministic function of position in the list, so it is reversible (thinning to 1.0 restores
        /// every instance, and the survivors of any fraction are always a prefix-stable subset of a denser pass).
        /// This is not a re-scatter at lower density, which would move every instance. </summary>
        public static IList<T> Thin<T>(IList<T> ordered, double keepFraction)
        {
            List<T> kept = new List<T>();
            if (ordered == null || ordered.Count == 0) { return kept; }
            if (keepFraction <= 0.0) { return kept; }
            if (keepFraction >= 1.0) { kept.AddRange(ordered); return kept; }

            // Keep index i when the running "kept so far" quota crosses an integer -- an even stride with no RNG.
            long prev = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                long now = (long)Math.Floor((i + 1) * keepFraction);
                if (now > prev) { kept.Add(ordered[i]); }
                prev = now;
            }
            return kept;
        }
    }
}

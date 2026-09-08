#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-11's buildable-land test: given a candidate lot polygon and the live terrain, it
    /// returns whether the lot is buildable, must be reshaped to a smaller buildable remnant, or is
    /// rejected -- and, for anything but a clean pass, WHY, with a number the author can read.
    ///
    /// It runs four separate tests, because each has a different remedy when it fails:
    /// slope (reshape to the buildable part, or reject if too little remains), water (reject; never
    /// reshape into a shoreline sliver), occupancy (reject when the lot overlaps a road corridor or an
    /// already-built lot), and relief (reject when the height range a pad would have to absorb is too
    /// large). Slope and relief are deliberately distinct: a uniform hillside is high slope and low
    /// relief; a small gully is moderate slope and high relief, and only relief tells a pad what it
    /// must move.
    ///
    /// Order of remedies matters. Water and occupancy are absolute rejections. Slope is tried next,
    /// because reshaping to the buildable part is what a too-steep whole lot deserves -- and relief is
    /// judged LAST, on the polygon that would actually be built (the whole lot, or the reshaped
    /// remnant), so clipping off a steep bank also relieves the pad instead of the lot being condemned
    /// for relief it no longer has.
    ///
    /// It is a pure function of its arguments -- it reads the terrain provider but never a scene, and
    /// draws no random numbers -- so the same lot on the same terrain always earns the same verdict
    /// (C-06). The provider does the sampling; this class only decides. </summary>
    public static class BuildableLandTest
    {
        private const float MinSpacing = 0.5f;   // never sample finer than this, whatever the caller passes

        /// <summary> One terrain reading inside or on the lot: its world position (with real height)
        /// and the ground grade there, as a percentage. </summary>
        private struct Sample
        {
            public Vector3 pos;
            public float slopePercent;
        }

        /// <summary> Tests <paramref name="lotPolygon"/> for buildability against
        /// <paramref name="criteria"/>, sampling <paramref name="terrain"/> on a grid whose spacing is
        /// <paramref name="sampleSpacing"/> (tie it to the terrain heightmap resolution -- finer tells
        /// you nothing new and costs time, coarser misses real features). <paramref name="waterSurfaceY"/>
        /// is the world Y of the water surface in this region, or <see cref="float.NegativeInfinity"/>
        /// when there is no water (the water test is then a no-op). <paramref name="occupiedFootprints"/>
        /// are the XZ polygons already claimed -- road corridors, built lots, rail, easements -- or null
        /// when nothing is claimed yet. </summary>
        public static LotVerdict Evaluate(
            IList<Vector3> lotPolygon,
            IPCGDataProvider terrain,
            float sampleSpacing,
            float waterSurfaceY,
            IList<List<Vector3>> occupiedFootprints,
            BuildabilityCriteria criteria)
        {
            LotVerdict verdict = new LotVerdict();
            verdict.reshapedPolygon = null;

            // A lot that is not a polygon, or is under the zone minimum, is not land -- reject early.
            if (lotPolygon == null || lotPolygon.Count < 3)
            {
                verdict.result = Buildability.Rejected;
                verdict.reason = "lot is not a polygon (fewer than three vertices)";
                return verdict;
            }
            float lotArea = Geometry2D.Area(lotPolygon);
            if (lotArea < criteria.minLotArea)
            {
                verdict.result = Buildability.Rejected;
                verdict.reason = "lot area " + lotArea.ToString("F0") + "m^2 is under zone minimum " +
                                 criteria.minLotArea.ToString("F0") + "m^2";
                return verdict;
            }

            float spacing = Mathf.Max(MinSpacing, sampleSpacing);
            List<Sample> samples = new List<Sample>();
            Gather(lotPolygon, terrain, spacing, samples);
            float maxSlope, meanSlope, minHeight, maxHeight;
            Aggregate(samples, out maxSlope, out meanSlope, out minHeight, out maxHeight);
            verdict.maxSlope = maxSlope;
            verdict.meanSlope = meanSlope;
            verdict.reliefMetres = maxHeight - minHeight;

            // --- WATER: absolute reject, ahead of any reshape, so a lot is never reshaped into a dry
            // sliver perched over a lake. The comparison itself lives in WaterRejects, because the site
            // test (B-15 / C-4) has to apply the same rule to a whole candidate site, and two copies of a
            // freeboard comparison are two things free to disagree about what counts as dry land.
            string waterReason;
            if (WaterRejects(minHeight, waterSurfaceY, criteria, out waterReason))
            {
                verdict.result = Buildability.Rejected;
                verdict.reason = waterReason;
                return verdict;
            }

            // --- OCCUPANCY: absolute reject when the lot sits on ground another footprint owns.
            if (occupiedFootprints != null)
            {
                for (int i = 0; i < occupiedFootprints.Count; i++)
                {
                    List<Vector3> footprint = occupiedFootprints[i];
                    if (footprint != null && footprint.Count >= 3 &&
                        Geometry2D.PolygonsOverlap(lotPolygon, footprint))
                    {
                        verdict.result = Buildability.Rejected;
                        verdict.reason =
                            "lot overlaps an existing footprint (road corridor, built lot, rail or easement)";
                        return verdict;
                    }
                }
            }

            // --- SLOPE: choose the polygon that would actually be built. Within the grade limit that is
            // the whole lot; over it, the reshaped buildable remnant, or a rejection if too little remains.
            List<Vector3> candidate;
            List<Sample> candidateSamples;
            Buildability outcome;
            if (maxSlope <= criteria.maxSlopePercent)
            {
                candidate = new List<Vector3>(lotPolygon);
                candidateSamples = samples;
                outcome = Buildability.Buildable;
            }
            else if (TryReshapeForSlope(lotPolygon, terrain, spacing, samples, criteria,
                         out candidate, out candidateSamples))
            {
                outcome = Buildability.Reshaped;
            }
            else
            {
                return RejectSlope(criteria, ref verdict);
            }

            // --- RELIEF: judged on the built polygon. A reshape that removed the steep part also
            // removed its relief, so this reads the remnant's range, not the original lot's.
            float cMax, cMean, cMin, cMaxH;
            Aggregate(candidateSamples, out cMax, out cMean, out cMin, out cMaxH);
            float candidateRelief = cMaxH - cMin;
            verdict.maxSlope = cMax;
            verdict.meanSlope = cMean;
            verdict.reliefMetres = candidateRelief;
            if (candidateRelief > criteria.maxReliefMetres)
            {
                verdict.result = Buildability.Rejected;
                verdict.reason = "relief " + candidateRelief.ToString("F1") +
                    "m exceeds zone pad limit " + criteria.maxReliefMetres.ToString("F1") + "m";
                return verdict;
            }

            verdict.result = outcome;
            if (outcome == Buildability.Reshaped)
            {
                verdict.reshapedPolygon = candidate;
                verdict.reason = "reshaped to a " + Geometry2D.Area(candidate).ToString("F0") +
                    "m^2 buildable remnant; original max slope " + maxSlope.ToString("F0") +
                    "% (whole lot) exceeded zone max " + criteria.maxSlopePercent.ToString("F0") + "%";
            }
            else
            {
                verdict.reason = "buildable: max slope " + cMax.ToString("F0") + "% within zone max " +
                    criteria.maxSlopePercent.ToString("F0") + "%, relief " +
                    candidateRelief.ToString("F1") + "m within " +
                    criteria.maxReliefMetres.ToString("F1") + "m";
            }
            return verdict;
        }
        /// <summary> THE water rule, in one place: ground within the authored freeboard of the water surface
        /// is not buildable land, and a world with no water (<see cref="float.NegativeInfinity"/>) rejects
        /// nothing. Freeboard is a margin rather than a bare comparison against the level, because a pad
        /// level with a lake is a pad that floods.
        ///
        /// It takes the LOWEST ground of whatever is being judged -- a lot here, a candidate site in B-15's
        /// site test -- because the lowest point is the one the water reaches first. Public so that both
        /// callers apply the same comparison and report it in the same words; the alternative was a second
        /// freeboard test in the city layer, free to drift from this one. </summary>
        public static bool WaterRejects(float lowestGroundY, float waterSurfaceY,
            BuildabilityCriteria criteria, out string reason)
        {
            if (float.IsNegativeInfinity(waterSurfaceY) || float.IsNaN(waterSurfaceY)
                || float.IsPositiveInfinity(waterSurfaceY))
            {
                reason = string.Empty;
                return false;
            }
            if (float.IsNaN(lowestGroundY) || float.IsInfinity(lowestGroundY))
            {
                reason = "the ground here could not be read, so it cannot be shown to stand clear of the " +
                    "water surface at " + waterSurfaceY.ToString("F1") + "m";
                return true;
            }
            if (lowestGroundY > waterSurfaceY + criteria.waterFreeboard)
            {
                reason = string.Empty;
                return false;
            }
            reason = "ground at " + lowestGroundY.ToString("F1") + "m is within the " +
                criteria.waterFreeboard.ToString("F1") + "m freeboard of the water surface at " +
                waterSurfaceY.ToString("F1") + "m";
            return true;
        }

        /// <summary> The slope remedy: clip the lot by a single half-plane that removes the steep
        /// region and keeps the buildable side, and hand back the remnant plus its fresh samples when
        /// the remnant is large enough. The cut line is perpendicular to the axis from the buildable
        /// samples' centroid to the steep samples' centroid, so it separates the two.
        ///
        /// Stated limit: one straight cut. It reshapes a lot whose steepness is a corner or an edge --
        /// the common case of a lot running up onto a bank -- and it correctly REJECTS (returns false,
        /// rather than faking a reshape) a lot with a steep blob in its middle or steepness on two
        /// opposite sides, which a single half-plane cannot excise. Those are exactly the cases the
        /// grid sampling exists to catch, and a rejection with a number is the honest answer. </summary>
        private static bool TryReshapeForSlope(IList<Vector3> lotPolygon, IPCGDataProvider terrain,
            float spacing, List<Sample> samples, BuildabilityCriteria criteria,
            out List<Vector3> remnant, out List<Sample> remnantSamples)
        {
            remnant = null;
            remnantSamples = null;

            Vector2 steepCentroid = Vector2.zero;
            Vector2 buildableCentroid = Vector2.zero;
            int steepCount = 0, buildableCount = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 xz = Geometry2D.XZ(samples[i].pos);
                if (samples[i].slopePercent > criteria.maxSlopePercent)
                {
                    steepCentroid += xz;
                    steepCount++;
                }
                else
                {
                    buildableCentroid += xz;
                    buildableCount++;
                }
            }

            // Nothing buildable to keep, or nothing steep to remove: a single cut cannot help.
            if (buildableCount == 0 || steepCount == 0)
            {
                return false;
            }

            steepCentroid /= steepCount;
            buildableCentroid /= buildableCount;
            Vector2 axis = steepCentroid - buildableCentroid;
            if (axis.sqrMagnitude < 1e-6f)
            {
                return false;   // steep and buildable share a centroid: steepness is interior, not to one side
            }
            axis.Normalize();
            Vector2 lineDir = new Vector2(-axis.y, axis.x);   // perpendicular to the buildable->steep axis
            Vector2 lineP = (steepCentroid + buildableCentroid) * 0.5f;
            // Keep the side the buildable centroid is on: ClipByHalfPlane keeps Cross(dir, p-lineP) >= 0.
            if (Geometry2D.Cross(lineDir, buildableCentroid - lineP) < 0f)
            {
                lineDir = -lineDir;
            }

            List<Vector3> clipped = new List<Vector3>();
            if (!Geometry2D.ClipByHalfPlane(lotPolygon, lineP, lineDir, clipped))
            {
                return false;
            }
            if (Geometry2D.Area(clipped) < criteria.minLotArea)
            {
                return false;
            }

            // Re-measure the remnant: the cut must actually have brought the grade within the limit,
            // or the reshape achieved nothing and this is a rejection, not a reshape.
            List<Sample> clippedSamples = new List<Sample>();
            Gather(clipped, terrain, spacing, clippedSamples);
            float rMax, rMean, rMin, rMaxH;
            Aggregate(clippedSamples, out rMax, out rMean, out rMin, out rMaxH);
            if (rMax > criteria.maxSlopePercent)
            {
                return false;
            }

            remnant = clipped;
            remnantSamples = clippedSamples;
            return true;
        }

        /// <summary> Fills the verdict for a lot too steep to reshape and returns it. Reports the
        /// whole-lot grade already stored on the verdict. </summary>
        private static LotVerdict RejectSlope(BuildabilityCriteria criteria, ref LotVerdict verdict)
        {
            verdict.result = Buildability.Rejected;
            verdict.reason = "max slope " + verdict.maxSlope.ToString("F0") + "% exceeds zone max " +
                criteria.maxSlopePercent.ToString("F0") +
                "% and no buildable remnant of at least " + criteria.minLotArea.ToString("F0") +
                "m^2 survives a reshape";
            return verdict;
        }

        /// <summary> Samples the terrain across a polygon: at each vertex, at the centroid, on a grid
        /// at <paramref name="spacing"/> through the interior, and along each edge at the same spacing.
        /// The interior grid catches a feature the polygon straddles (a boulder, a knoll); the edge
        /// samples catch the bank a retaining wall would have to stand on, which a four-corner test
        /// misses. </summary>
        private static void Gather(IList<Vector3> polygon, IPCGDataProvider terrain, float spacing,
            List<Sample> samples)
        {
            int n = polygon.Count;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 v = polygon[i];
                AddSample(terrain, v.x, v.z, samples);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
                centroid += new Vector2(v.x, v.z);
            }
            centroid /= n;
            AddSample(terrain, centroid.x, centroid.y, samples);

            for (float x = minX; x <= maxX; x += spacing)
            {
                for (float z = minZ; z <= maxZ; z += spacing)
                {
                    if (Geometry2D.Contains(polygon, new Vector2(x, z)))
                    {
                        AddSample(terrain, x, z, samples);
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[(i + 1) % n];
                float len = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                int steps = Mathf.FloorToInt(len / spacing);
                for (int k = 1; k < steps; k++)
                {
                    float t = (float)k / steps;
                    AddSample(terrain, Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.z, b.z, t), samples);
                }
            }
        }

        /// <summary> Reads one point: real height and the ground grade there, and stores it. </summary>
        private static void AddSample(IPCGDataProvider terrain, float x, float z, List<Sample> samples)
        {
            float h = terrain.SampleHeight(x, z);
            Vector3 normal = terrain.SampleNormal(x, z);
            Sample s;
            s.pos = new Vector3(x, h, z);
            s.slopePercent = SlopePercent(normal);
            samples.Add(s);
        }

        /// <summary> Ground grade as a percentage (rise/run * 100) from a surface normal:
        /// tan(theta) * 100 where theta is the angle off vertical. A near-horizontal normal (a wall)
        /// clamps to a large finite grade rather than dividing by zero. </summary>
        private static float SlopePercent(Vector3 normal)
        {
            float ny = Mathf.Max(normal.y, 1e-4f);
            float tan = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
            return tan * 100f;
        }

        /// <summary> Reduces the samples to the four numbers the verdict reports: the maximum and mean
        /// grade, and the minimum and maximum height (whose difference is the relief). </summary>
        private static void Aggregate(List<Sample> samples, out float maxSlope, out float meanSlope,
            out float minHeight, out float maxHeight)
        {
            maxSlope = 0f; meanSlope = 0f; minHeight = float.MaxValue; maxHeight = float.MinValue;
            if (samples.Count == 0)
            {
                minHeight = 0f; maxHeight = 0f;
                return;
            }
            float sum = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                float slope = samples[i].slopePercent;
                maxSlope = Mathf.Max(maxSlope, slope);
                sum += slope;
                minHeight = Mathf.Min(minHeight, samples[i].pos.y);
                maxHeight = Mathf.Max(maxHeight, samples[i].pos.y);
            }
            meanSlope = sum / samples.Count;
        }
    }
}
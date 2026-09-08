#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How far the ground is allowed to move under a finished road before somebody has to be told.
    ///
    /// Both figures are specification figures rather than chosen ones, because "close enough" is the kind of number that
    /// gets picked to make a test pass:
    ///
    /// * <see cref="supportToleranceMetres"/> is the ordinary finished-formation elevation tolerance for highway
    ///   earthworks, +/- 25 mm. Ground within that of where the road was built is ground the road is still bearing on --
    ///   it is the tolerance the formation was BUILT to, so treating a smaller difference as a defect would be reporting
    ///   the specification as an error.
    /// * <see cref="visibleMismatchMetres"/> is 150 mm, one standard kerb upstand and about one pavement course. At that
    ///   size the mismatch is not a survey quantity any more: the shoulder either shows daylight under it or has
    ///   disappeared into the hill, and a player standing on the road can see which.
    ///
    /// Between the two the road is supported but out of tolerance -- worth rebuilding, not urgent. That is a real third
    /// answer, and collapsing it into either neighbour is what produces both "refresh everything after every edit" and
    /// "nothing was reported until the road was visibly floating".
    ///
    /// <see cref="stationSpacingMetres"/> is 20 m, the metric highway station. Profiles have been drawn and checked at
    /// that interval for as long as there have been profiles, and it is short enough that a single re-graded hillside
    /// cannot hide between two stations. </summary>
    public sealed class GroundDriftClass
    {
        public double supportToleranceMetres = 0.025;
        public double visibleMismatchMetres = 0.150;
        public double stationSpacingMetres = 20.0;

        /// <summary> A ceiling on stations per road, so a fifty kilometre trunk route does not turn a verdict into an
        /// allocation. Reaching it widens the spacing, which LOSES detail, so the effective spacing is reported rather
        /// than assumed -- a verdict measured at 90 m stations is a weaker statement than one measured at 20 m and the
        /// report has to say so. </summary>
        public int maximumStations = 512;

        public static GroundDriftClass Default()
        {
            return new GroundDriftClass();
        }
    }


    /// <summary> Which way the ground went. Named from the road's point of view, because that is the sentence an author
    /// needs: a road with ground below it is FLOATING and a road with ground above it is BURIED, and the fix differs --
    /// the first shows daylight and a gap the wheels drop into, the second has vanished. </summary>
    public enum GroundMotion
    {
        Unchanged = 0,
        Fell = 1,
        Rose = 2,

        /// <summary> Both, beyond tolerance, at different stations: the hill was reshaped rather than raised or lowered.
        /// Reported separately because a uniform shift can sometimes be answered by moving the road and a reshape never
        /// can. </summary>
        Reshaped = 3,
    }


    /// <summary> What the comparison concluded. The order the verdicts are TESTED in matters as much as the values: an
    /// absent record and an unreachable terrain must never be able to come out as agreement, which is the failure mode
    /// where a road silently stops being checked and the report keeps saying everything is fine. </summary>
    public enum GroundVerdict
    {
        /// <summary> Nothing was recorded when this road was built, so nothing can be concluded now. Not a pass.
        /// </summary>
        NeverRecorded = 0,

        /// <summary> The record exists but the ground cannot be read -- provider disconnected, terrain deleted, chunk
        /// unloaded. Also not a pass: the honest answer is that the question could not be asked. </summary>
        GroundUnavailable = 1,

        Agrees = 2,

        /// <summary> Out of the formation tolerance but not yet visible. Rebuild when convenient. </summary>
        RefreshAdvised = 3,

        /// <summary> Visibly floating or buried. This road must be rebuilt before the scene is shown to anybody.
        /// </summary>
        RefreshRequired = 4,
    }


    /// <summary> One measured point on a finished road: where it is, what the ground was when the road was conformed to
    /// it, and what the road surface itself ended up at. Stations on a bridge or in a tunnel are recorded too but marked
    /// <see cref="grounded"/> false, because a bridge deck standing clear of the ground is the deck doing its job and
    /// flagging it as floating would bury every real finding under one false one per span. </summary>
    public struct GroundStation
    {
        public float worldX;
        public float worldZ;
        public float groundYAtBuild;
        public float roadYAtBuild;
        public bool grounded;
    }


    /// <summary> The worst thing the comparison found, and where. </summary>
    public struct GroundDrift
    {
        public GroundVerdict verdict;
        public GroundMotion motion;

        /// <summary> The largest rise and the largest fall, both as positive metres, kept apart so
        /// <see cref="GroundMotion.Reshaped"/> can be told from a shift. </summary>
        public double worstRiseMetres;
        public double worstFallMetres;

        /// <summary> The station index and world position of the worst reading, so the author is sent to a place rather
        /// than to a road. -1 when there was nothing to measure. </summary>
        public int worstStation;
        public float worstX;
        public float worstZ;

        public int stationsMeasured;
        public int stationsUnreadable;
        public double effectiveStationSpacingMetres;

        public double WorstAbsoluteMetres
        {
            get { return worstRiseMetres > worstFallMetres ? worstRiseMetres : worstFallMetres; }
        }

        public bool NeedsRefresh
        {
            get { return verdict == GroundVerdict.RefreshAdvised || verdict == GroundVerdict.RefreshRequired; }
        }

        public string Describe(string roadName)
        {
            string where = worstStation < 0 ? "nowhere in particular"
                : "station " + worstStation.ToString(CultureInfo.InvariantCulture) + " at ("
                    + worstX.ToString("F1", CultureInfo.InvariantCulture) + ", "
                    + worstZ.ToString("F1", CultureInfo.InvariantCulture) + ")";
            StringBuilder b = new StringBuilder();
            b.Append(roadName).Append(": ").Append(verdict.ToString()).Append(", ground ")
             .Append(motion.ToString().ToLowerInvariant());
            if (motion == GroundMotion.Fell || motion == GroundMotion.Reshaped)
            {
                b.Append(", road floating by up to ")
                 .Append(worstFallMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m");
            }
            if (motion == GroundMotion.Rose || motion == GroundMotion.Reshaped)
            {
                b.Append(", road buried by up to ")
                 .Append(worstRiseMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m");
            }
            b.Append(", worst at ").Append(where)
             .Append("; measured ").Append(stationsMeasured.ToString(CultureInfo.InvariantCulture))
             .Append(" stations at ")
             .Append(effectiveStationSpacingMetres.ToString("F1", CultureInfo.InvariantCulture)).Append(" m");
            if (stationsUnreadable > 0)
            {
                b.Append(", ").Append(stationsUnreadable.ToString(CultureInfo.InvariantCulture))
                 .Append(" unreadable");
            }
            return b.ToString();
        }
    }


    /// <summary> RM-99: the ground a road was built against, remembered, so that a host which regenerates its terrain
    /// underneath a finished road produces either a re-conformed road or a report naming the roads to refresh -- and
    /// never a road left floating over new ground, buried in it, or with a tunnel mouth opening onto a hill that no
    /// longer has a hole in it.
    ///
    /// The record is deliberately a PROFILE and not a checksum. A hash answers "did anything change" and then leaves the
    /// author to find out where, which on a five kilometre road is the entire job; a profile answers "how far, which way,
    /// and at which station", which is the answer that can be acted on. It is also why the comparison is cheap enough to
    /// run on every road at once: one height query per twenty metres.
    ///
    /// This class decides and never acts, the same division <see cref="BakeStamp"/> keeps for content bakes and for the
    /// same reason -- a verdict that rebuilds the thing it is judging cannot be trusted to have judged it, and a verdict
    /// that rebuilds during terrain regeneration is a write into ground the host is in the middle of owning.
    /// <see cref="BakeStamp"/> answers a different question (was this content baked by the current generator) and is not
    /// extended here: a road can be perfectly up to date against its generator and still be hanging in the air.
    /// </summary>
    public sealed class GroundConformRecord
    {
        /// <summary> The road this record belongs to, by name, because that is what an author can find in a hierarchy.
        /// </summary>
        public string roadName = string.Empty;

        public readonly List<GroundStation> stations = new List<GroundStation>();

        /// <summary> The spacing actually used, which is the class spacing unless the road was long enough to hit the
        /// station ceiling. Carried on the record rather than recomputed, so a verdict cannot claim a resolution the
        /// measurement did not have. </summary>
        public double effectiveStationSpacingMetres;

        public bool HasStations { get { return stations.Count > 0; } }

        /// <summary> Walk a finished road and record what it was standing on.
        ///
        /// Called at the end of a build, with the same surface reader the build itself used -- in standalone that is the
        /// tile, in connected mode it is the provider, and taking it as a parameter is what lets the identical record be
        /// made either way. Both ends of the road are included: the first and last station are t = 0 and t = 1 exactly,
        /// because a road whose far end is over a cliff that got re-graded is precisely the case a sampling scheme that
        /// stops short would miss. </summary>
        public static GroundConformRecord Record(SplineC spline, string roadName, ITerrainSurfaceReader ground,
            GroundDriftClass cls)
        {
            GroundConformRecord record = new GroundConformRecord();
            GroundDriftClass c = cls != null ? cls : GroundDriftClass.Default();
            record.roadName = string.IsNullOrEmpty(roadName) ? "unnamed road" : roadName;
            record.effectiveStationSpacingMetres = c.stationSpacingMetres;
            if (spline == null || ground == null) { return record; }

            double length = spline.distance;
            if (length <= 0.0) { return record; }

            double spacing = c.stationSpacingMetres > 0.0 ? c.stationSpacingMetres : 20.0;
            int intervals = (int)System.Math.Ceiling(length / spacing);
            if (intervals < 1) { intervals = 1; }
            if (c.maximumStations > 1 && intervals + 1 > c.maximumStations)
            {
                intervals = c.maximumStations - 1;
            }
            record.effectiveStationSpacingMetres = length / intervals;

            for (int i = 0; i <= intervals; i++)
            {
                float t = (float)((double)i / intervals);
                Vector3 onRoad = spline.GetSplineValue(t, false);
                GroundStation station = new GroundStation();
                station.worldX = onRoad.x;
                station.worldZ = onRoad.z;
                station.roadYAtBuild = onRoad.y;
                station.groundYAtBuild = ground.SampleHeight(onRoad.x, onRoad.z);
                station.grounded = !InSpan(spline.BridgeParams, t) && !InSpan(spline.TunnelParams, t);
                record.stations.Add(station);
            }
            return record;
        }

        /// <summary> Ask the ground where it is NOW and say what that means for this road.
        ///
        /// Nothing is written and nothing is rebuilt. A reader that answers with the base of the world for a chunk it has
        /// not loaded would otherwise read as a hundred metre collapse, so an unreadable station is COUNTED rather than
        /// measured: the sentinel is the same one the tool uses everywhere for "no answer here", and a road whose ground
        /// is entirely unreadable comes back as <see cref="GroundVerdict.GroundUnavailable"/> instead of as a disaster.
        /// </summary>
        public GroundDrift Evaluate(ITerrainSurfaceReader ground, GroundDriftClass cls)
        {
            GroundDrift drift = new GroundDrift();
            GroundDriftClass c = cls != null ? cls : GroundDriftClass.Default();
            drift.worstStation = -1;
            drift.effectiveStationSpacingMetres = effectiveStationSpacingMetres;

            if (stations.Count == 0)
            {
                drift.verdict = GroundVerdict.NeverRecorded;
                return drift;
            }
            if (ground == null)
            {
                drift.verdict = GroundVerdict.GroundUnavailable;
                return drift;
            }

            int grounded = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                GroundStation station = stations[i];
                if (!station.grounded) { continue; }
                grounded++;

                float now = ground.SampleHeight(station.worldX, station.worldZ);
                if (float.IsNaN(now) || float.IsInfinity(now))
                {
                    drift.stationsUnreadable++;
                    continue;
                }

                drift.stationsMeasured++;
                double delta = (double)now - station.groundYAtBuild;
                double magnitude = delta < 0.0 ? -delta : delta;
                if (delta > 0.0 && delta > drift.worstRiseMetres) { drift.worstRiseMetres = delta; }
                if (delta < 0.0 && -delta > drift.worstFallMetres) { drift.worstFallMetres = -delta; }
                if (magnitude >= drift.WorstAbsoluteMetres)
                {
                    drift.worstStation = i;
                    drift.worstX = station.worldX;
                    drift.worstZ = station.worldZ;
                }
            }

            if (grounded == 0 || drift.stationsMeasured == 0)
            {
                // Either the whole road is bridge and tunnel -- in which case there is no ground to have moved and the
                // road is exactly as supported as it was -- or nothing could be read. Those are different answers.
                drift.verdict = grounded == 0 ? GroundVerdict.Agrees : GroundVerdict.GroundUnavailable;
                drift.motion = GroundMotion.Unchanged;
                return drift;
            }

            bool rose = drift.worstRiseMetres > c.supportToleranceMetres;
            bool fell = drift.worstFallMetres > c.supportToleranceMetres;
            drift.motion = rose && fell ? GroundMotion.Reshaped
                : rose ? GroundMotion.Rose
                : fell ? GroundMotion.Fell
                : GroundMotion.Unchanged;

            double worst = drift.WorstAbsoluteMetres;
            drift.verdict = worst >= c.visibleMismatchMetres ? GroundVerdict.RefreshRequired
                : worst > c.supportToleranceMetres ? GroundVerdict.RefreshAdvised
                : GroundVerdict.Agrees;
            return drift;
        }

        private static bool InSpan(List<SplineSpan> spans, float t)
        {
            if (spans == null) { return false; }
            for (int i = 0; i < spans.Count; i++)
            {
                if (t >= spans[i].start && t <= spans[i].end) { return true; }
            }
            return false;
        }
    }
}

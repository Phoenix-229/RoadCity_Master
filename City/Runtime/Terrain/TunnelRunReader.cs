#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-98. Reading the author's tunnels off a spline and stating them as
    /// <see cref="TunnelLinkerBridge.TunnelRun"/>s.
    ///
    /// The tunnel runs already exist: an author marks a node as a tunnel start and another as its end, and
    /// <see cref="SplineC.TunnelParams"/> is the list of (start time, end time) pairs the spline itself built from
    /// those flags. This file does NOT re-derive that list -- walking the nodes again would be a second implementation
    /// of the same traversal, and the two would eventually disagree about a run whose end node was deleted. It reads
    /// the spline's own list and measures each entry.
    ///
    /// Everything it measures, it measures rather than assumes:
    ///
    ///   * The centre, the road level and the direction come from sampling the spline at the run's mid time, so a
    ///     tunnel on a curve is described at the point it actually passes under the hill.
    ///   * The corridor half-width comes from <see cref="Road.LaneBandsAt"/>, which is the section at that station --
    ///     auxiliary lane included. A portal notch as wide as the road's nominal width would be too narrow at a run
    ///     that happens to start inside a widening.
    ///   * The ground over the crown comes from the terrain, through <see cref="ITerrainSurfaceReader"/>, which is the
    ///     same call in both modes: the tool's own sampler standalone, the host's provider when connected. It is the
    ///     TIGHTEST sample along the run, never the average, because cover is a minimum and an average lets one
    ///     shallow station through.
    ///
    /// Two things it cannot measure and therefore takes as arguments. The water surface, because the provider surface
    /// (§ 10.8) has no water query and no RM task may add one (R-5) -- so water arrives the same way it arrives at
    /// <see cref="BuildableLandTest"/>, as an elevation with negative infinity meaning "no water here", which keeps one
    /// convention for the whole tool. And whether a run under water is a bored tube or an immersed one, because that is
    /// a construction decision an engineer makes, not a fact about the ground; guessing it would silently change which
    /// arithmetic the run is judged by. </summary>
    public static class TunnelRunReader
    {
        #region "What one run reads as"
        /// <summary> One tunnel run, with the spline parameters it came from kept alongside so a caller can go back to
        /// the nodes -- to report a refusal against the node the author clicked, or to place the portal structures.
        /// </summary>
        public struct SplineTunnelRun
        {
            public TunnelLinkerBridge.TunnelRun run;

            /// <summary> Spline time at the tunnel start node, and at its matched end node. </summary>
            public float startTime;
            public float endTime;

            /// <summary> The tightest ground sample's position, for a message that says WHERE the cover was short.
            /// </summary>
            public Vector3 tightestGroundPoint;

            /// <summary> How many samples were taken along the run. Zero means the run was too short to sample and the
            /// figures came from its mid point alone. </summary>
            public int groundSampleCount;

            public string Describe()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "tunnel t[{0:0.####}..{1:0.####}], {2:0.#} m long, road at {3:0.##} m, tightest ground {4:0.##} m "
                    + "from {5} sample(s){6}",
                    startTime, endTime, run.lengthMetres, run.roadSurfaceElevationMetres,
                    run.tightestGroundElevationMetres, groundSampleCount,
                    run.underWater ? (run.immersedTube ? ", immersed tube" : ", under water") : string.Empty);
            }
        }
        #endregion


        #region "Reading"
        /// <summary> Every tunnel run on one spline, measured.
        ///
        /// <paramref name="ground"/> may be null -- there is no terrain reader in a unit test, and a caller that only
        /// wants the geometry should not have to invent one. When it is null the ground is taken to sit at the road
        /// level, which makes every run read as having no cover at all; that is deliberately the pessimistic answer,
        /// because a missing terrain reader must not produce a run that passes the cover test by default.
        ///
        /// <paramref name="samplesPerRun"/> is how many stations the ground is sampled at. The default of 21 puts a
        /// sample every 15 m on a 300 m tunnel, which is finer than the terrain's own sample spacing on any tile a road
        /// is built at, so nothing hides between two of them. </summary>
        public static List<SplineTunnelRun> Read(SplineC spline, Road road, ITerrainSurfaceReader ground,
            float waterSurfaceY, bool immersedTube, int samplesPerRun)
        {
            List<SplineTunnelRun> runs = new List<SplineTunnelRun>();
            if (spline == null || spline.TunnelParams == null || road == null) { return runs; }
            if (samplesPerRun < 2) { samplesPerRun = 2; }

            double pavement = TerrainBuildSettings.Cover.pavementBuildUpMetres;

            for (int i = 0; i < spline.TunnelParams.Count; i++)
            {
                KeyValuePair<float, float> pair = spline.TunnelParams[i];
                float t0 = pair.Key;
                float t1 = pair.Value;
                if (t1 <= t0) { continue; }

                SplineTunnelRun entry = new SplineTunnelRun();
                entry.startTime = t0;
                entry.endTime = t1;

                float mid = (t0 + t1) * 0.5f;
                Vector3 centre = spline.GetSplineValue(mid);
                Vector3 tangent = spline.GetSplineValue(mid, true);

                entry.run.centreX = centre.x;
                entry.run.centreZ = centre.z;
                entry.run.roadSurfaceElevationMetres = centre.y;
                entry.run.pavementThicknessMetres = pavement;
                entry.run.lengthMetres = (t1 - t0) * (spline.distance > 0f ? spline.distance : 0f);

                // Which pair of faces of the opening the host leaves open. Measured from the tangent at the middle of
                // the run rather than from the chord between the portals: on a curve the chord can point closer to the
                // other axis than the road ever does.
                entry.run.alongX = Mathf.Abs(tangent.x) >= Mathf.Abs(tangent.z);

                entry.run.corridorHalfWidthMetres = CorridorHalfWidth(road, spline, mid);

                double tightest;
                Vector3 tightestAt;
                int taken = SampleTightestGround(spline, ground, t0, t1, samplesPerRun, centre.y, out tightest,
                    out tightestAt);
                entry.run.tightestGroundElevationMetres = tightest;
                entry.tightestGroundPoint = tightestAt;
                entry.groundSampleCount = taken;

                // Under water when the ground the tunnel passes under is itself below the water plane -- that is a bed,
                // not a hillside, and it is the bed that decides which minimum applies.
                entry.run.underWater = !float.IsNegativeInfinity(waterSurfaceY) && tightest < waterSurfaceY;
                entry.run.immersedTube = entry.run.underWater && immersedTube;
                entry.run.burialDepthMetres = entry.run.immersedTube
                    ? BurialDepthMetres(tightest, centre.y, pavement) : 0.0;

                runs.Add(entry);
            }
            return runs;
        }

        /// <summary> The half-width of the works at a station, from the section that is actually there. Uses the wider
        /// of the two edges, because the notch and the trench are symmetrical boxes about the centre line and a box
        /// sized to the narrower side would leave the wider one outside the region the host was told about. </summary>
        private static double CorridorHalfWidth(Road road, SplineC spline, float time)
        {
            float distanceAlong = time * (spline.distance > 0f ? spline.distance : 0f);
            float leftEdge, rightEdge;
            road.LaneBandsAt(distanceAlong, out leftEdge, out rightEdge);
            double left = Mathf.Abs(leftEdge);
            double right = Mathf.Abs(rightEdge);
            double half = left > right ? left : right;
            if (half <= 0.0) { half = road.RoadWidth() * 0.5; }
            return half;
        }

        /// <summary> The lowest ground along the run, and where it was. Sampled at even spline times, which are even
        /// distances closely enough for this purpose on any alignment a tunnel is built on. </summary>
        private static int SampleTightestGround(SplineC spline, ITerrainSurfaceReader ground, float t0, float t1,
            int samples, float fallbackElevation, out double tightest, out Vector3 tightestAt)
        {
            tightest = fallbackElevation;
            tightestAt = spline.GetSplineValue((t0 + t1) * 0.5f);
            if (ground == null) { return 0; }

            bool any = false;
            float step = (t1 - t0) / (samples - 1);
            for (int s = 0; s < samples; s++)
            {
                float t = t0 + step * s;
                Vector3 p = spline.GetSplineValue(t);
                float h = ground.SampleHeight(p.x, p.z);
                if (!any || h < tightest)
                {
                    tightest = h;
                    tightestAt = new Vector3(p.x, h, p.z);
                    any = true;
                }
            }
            return any ? samples : 0;
        }

        /// <summary> How deep an immersed tube's roof sits below the bed, from the levels rather than from a table: the
        /// bed elevation less the tube's own outside top, which is the invert plus the tube. Clamped at zero because a
        /// tube whose roof is above the bed is not buried at all, and a negative burial would widen the trench
        /// backwards. </summary>
        private static double BurialDepthMetres(double bedElevation, double roadSurfaceElevation, double pavement)
        {
            double crown = TunnelTerrainPolicy.BoreCrownElevationMetres(roadSurfaceElevation, pavement,
                TerrainBuildSettings.Tunnel);
            double depth = bedElevation - crown;
            return depth > 0.0 ? depth : 0.0;
        }
        #endregion


        #region "Planning them all"
        /// <summary> Plan every run on a spline, in one call, so a caller that only wants to know "will these tunnels
        /// work" does not have to repeat the loop. The plans come back in the same order as the runs. </summary>
        public static List<TunnelLinkerBridge.TunnelLinkPlan> PlanAll(List<SplineTunnelRun> runs, TunnelMode mode)
        {
            List<TunnelLinkerBridge.TunnelLinkPlan> plans = new List<TunnelLinkerBridge.TunnelLinkPlan>();
            if (runs == null) { return plans; }
            for (int i = 0; i < runs.Count; i++)
            {
                plans.Add(TunnelLinkerBridge.Plan(runs[i].run, mode, TerrainBuildSettings.Tunnel,
                    TerrainBuildSettings.Cover, TerrainBuildSettings.Separation));
            }
            return plans;
        }
        #endregion
    }
}

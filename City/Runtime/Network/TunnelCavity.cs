#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which world the tunnel is being built into. In CONNECTED mode a terrain provider can open a real
    /// cavity; in STANDALONE mode none of the cavity calls exist, so the honest behaviour (RM-58) is to build the
    /// lining and portals as visible geometry and SAY once that the bore is not cut -- never to fake a hole by
    /// pushing terrain down. </summary>
    public enum TunnelMode
    {
        Connected = 0,
        Standalone = 1
    }

    /// <summary> The outcome of resolving a tunnel build. </summary>
    public enum TunnelBuildOutcome
    {
        CutAndLined = 0,             // connected: cavity opened, lining + portals seated
        LinedButNotCut = 1,          // standalone: geometry only, reported plainly
        RefusedInsufficientCover = 2 // under water without the minimum sealed cover -> refuse, name the station
    }

    /// <summary> RM-37's AUTHORED tunnel constants. The lining OVERSIZE is what stops a z-fighting sliver of
    /// terrain showing through the bore (the square-hole precedent from this project's own water work); the
    /// minimum WATER COVER between the bore crown and a lake/river bed is the rule that makes a tunnel under water
    /// legal at all. Values AUTHORED from ordinary tunnelling practice (C-15), retunable per project. </summary>
    [Serializable]
    public sealed class TunnelClass
    {
        /// <summary> AUTHORED default clear bore radius of a road tunnel (metres). </summary>
        public float boreRadiusMetres = 4f;

        /// <summary> How much larger the lining's outer surface is than the cavity, so no terrain sliver shows
        /// through and the ring closes cleanly at the portal. Must be positive. Authored as an exact decimal
        /// (double) so the ratio the planner means -- 0.15 m, not float 0.15000000596... -- is what the lining
        /// maths uses; a float here injected ~6e-9 m of representation error into the outer radius. </summary>
        public double liningOversizeMetres = 0.15;

        /// <summary> Minimum cover between the bore crown and a water bed above it; below this a tunnel under
        /// water is refused rather than left to flood or tear the surface. </summary>
        public float minWaterCoverMetres = 3f;

        public static TunnelClass Default()
        {
            return new TunnelClass();
        }
    }

    /// <summary> A deterministic ledger of the cavities a tunnel has opened through the connected terrain
    /// provider. The RELEASE path is the one implementers forget -- a tunnel that opens a cavity and never
    /// releases it leaves an orphaned hole that nothing owns -- so the tool tracks every opening and can report
    /// any that outlive their tunnel. Ordered list, no hashing, so two runs agree (C-06). </summary>
    public sealed class TunnelCavityLedger
    {
        private readonly List<int> openCavityIds = new List<int>();

        /// <summary> Record that a cavity was opened (RequestTunnelOpening). Idempotent per id. </summary>
        public void RequestOpening(int cavityId)
        {
            if (!openCavityIds.Contains(cavityId)) { openCavityIds.Add(cavityId); }
        }

        /// <summary> Record that a cavity was released (ReleaseTunnelOpening). Returns whether it was open. </summary>
        public bool ReleaseOpening(int cavityId)
        {
            return openCavityIds.Remove(cavityId);
        }

        /// <summary> How many opened cavities have not been released. </summary>
        public int OpenCount { get { return openCavityIds.Count; } }

        /// <summary> Whether any opened cavity outlived its tunnel -- an orphaned hole the release path missed. </summary>
        public bool HasOrphanedCavities { get { return openCavityIds.Count > 0; } }
    }

    /// <summary> RM-37's verifiable core: the pure tunnel decisions, without a scene. The lining is sized so it
    /// encloses the cavity (no terrain sliver); the release path is tracked so no cavity is orphaned; the
    /// under-water rule computes the cover between bore crown and bed and refuses, naming the station, where the
    /// cover runs out; and the mode decides whether the bore is cut at all. Requesting/releasing the real cavity
    /// through IPCGDataProvider, the swept lining tube, the seated portal headwalls and the terrain restore on
    /// delete are the deferred scene half. </summary>
    public static class TunnelCavity
    {
        /// <summary> The lining's outer radius: the clear bore plus the authored oversize, so the lining is always
        /// larger than the cavity it sits in. </summary>
        public static double LiningOuterRadiusMetres(double boreRadiusMetres, TunnelClass cls)
        {
            return boreRadiusMetres + cls.liningOversizeMetres;
        }

        /// <summary> Whether the lining encloses the cavity (its outer surface is strictly larger than the bore),
        /// which is what prevents a z-fighting sliver of terrain showing through. </summary>
        public static bool LiningEnclosesCavity(double liningOuterRadiusMetres, double boreRadiusMetres)
        {
            return liningOuterRadiusMetres > boreRadiusMetres + 1e-9;
        }

        /// <summary> The cover over the bore at a station: the water-bed elevation minus the bore-crown elevation.
        /// Positive means bed above crown (real cover); non-positive means the bore breaches the bed. </summary>
        public static double WaterCoverMetres(double bedElevationMetres, double boreCrownElevationMetres)
        {
            return bedElevationMetres - boreCrownElevationMetres;
        }

        /// <summary> The result of scanning the cover along an under-water reach of the bore. </summary>
        public struct CoverScan
        {
            public bool sealedLegal;            // cover stays at or above the minimum the whole way
            public double worstCoverMetres;
            public int failingStationIndex;     // where the cover first ran out, or -1
        }

        /// <summary> Walk the under-water stations, compute the cover at each, and report the tightest. A reach
        /// whose worst cover is below the authored minimum is NOT legal and names the station where the cover ran
        /// out -- the refusal the author cannot see from above the water. </summary>
        public static CoverScan ScanWaterCover(IList<double> bedElevations, IList<double> boreCrownElevations,
            TunnelClass cls)
        {
            CoverScan r = new CoverScan();
            r.worstCoverMetres = double.PositiveInfinity;
            r.failingStationIndex = -1;
            if (bedElevations == null || boreCrownElevations == null
                || bedElevations.Count == 0 || bedElevations.Count != boreCrownElevations.Count)
            {
                r.sealedLegal = false;
                return r;
            }
            for (int i = 0; i < bedElevations.Count; i++)
            {
                double cover = WaterCoverMetres(bedElevations[i], boreCrownElevations[i]);
                if (cover < r.worstCoverMetres) { r.worstCoverMetres = cover; }
                if (cover < cls.minWaterCoverMetres && r.failingStationIndex < 0) { r.failingStationIndex = i; }
            }
            r.sealedLegal = r.worstCoverMetres >= cls.minWaterCoverMetres;
            return r;
        }

        /// <summary> Resolve what the build does. Standalone always yields visible geometry with no cut (reported,
        /// never faked). Connected under water without adequate cover is refused. Connected otherwise cuts and
        /// lines the bore. </summary>
        public static TunnelBuildOutcome ResolveBuild(TunnelMode mode, bool underWater, CoverScan waterScan)
        {
            if (mode == TunnelMode.Standalone) { return TunnelBuildOutcome.LinedButNotCut; }
            if (underWater && !waterScan.sealedLegal) { return TunnelBuildOutcome.RefusedInsufficientCover; }
            return TunnelBuildOutcome.CutAndLined;
        }
    }
}

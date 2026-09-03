#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The four hub types this contract covers. A hub is an AUTHORED anchor (its building, platform layout
    /// and cranes are prefabs, never generated); what the tool contributes is the CONTRACT between the hub and
    /// everything around it -- which a validator can be trusted with and a generator cannot. </summary>
    public enum HubKind
    {
        RailStation = 0,
        BusStation = 1,
        Port = 2,
        Airport = 3
    }

    /// <summary> RM-43's airport approach surface: the published geometric volume rising away from a runway end. It is
    /// a defined slope over a defined length with a defined divergence each side, so a validator can be written from
    /// the standard ALONE -- build the surface, sample terrain and known structures inside it, report penetrations
    /// with heights and positions. AUTHORED (C-15) from the aerodrome obstacle-limitation-surface standard the project
    /// adopts (ICAO Annex 14, Approach Surface); the values below are a representative non-precision-runway set and
    /// MUST be transcribed from the project's adopted code number/edition, not taken from here. NOT a taste judgement:
    /// a runway placed across a valley is unusable, and this says so at the moment of placement. </summary>
    [Serializable]
    public sealed class ApproachSurfaceClass
    {
        /// <summary> Half the inner-edge width (metres): the surface's inner edge is this wide each side of the
        /// extended runway centreline. AUTHORED representative value. </summary>
        public float innerEdgeHalfWidthMetres = 75f;

        /// <summary> Distance from the runway end to the surface's inner edge (metres): the surface does not start at
        /// the runway end itself. </summary>
        public float distanceFromRunwayEndMetres = 60f;

        /// <summary> Divergence each side as a fraction: the half-width grows by this much per metre along the
        /// surface (e.g. 0.15 == 15% each side). Authored as an exact decimal (double) so the fraction the
        /// standard states -- 0.15, not float 0.15000000596... -- is what the half-width maths uses; a float
        /// here injected ~6e-6 m of representation error at the surface's far end. </summary>
        public double divergenceEachSide = 0.15;

        /// <summary> Surface slope (rise over run): the surface rises this much per metre along its length. AUTHORED
        /// ~2% (1:50). </summary>
        public float slope = 0.02f;

        /// <summary> Length of the sloping surface (metres), measured along the ground from the inner edge outward. </summary>
        public float lengthMetres = 3000f;

        public static ApproachSurfaceClass Default() { return new ApproachSurfaceClass(); }
    }

    /// <summary> RM-43's authored requirement contract for one hub: what the surrounding network MUST provide for the
    /// hub to be buildable at a site. AUTHORED (C-15) representative figures; a project sets these from the traffic
    /// its hubs imply. Not every field applies to every kind -- a port ignores the approach surface, an airport
    /// ignores navigable water. </summary>
    [Serializable]
    public sealed class HubRequirementsClass
    {
        public HubKind kind = HubKind.RailStation;

        /// <summary> Zone / road-class category key the network must provide within access distance (prefab category
        /// key, not a hard-coded type). Empty means no road-class requirement. </summary>
        public string requiredRoadCategory = "";

        /// <summary> How far the required road may be from the hub and still count as access (metres). </summary>
        public float accessDistanceMetres = 100f;

        /// <summary> Minimum forecourt / apron depth the hub needs in front of it (metres); 0 if none. </summary>
        public float forecourtDepthMetres = 0f;

        /// <summary> Whether the hub must sit against navigable water (the port case). </summary>
        public bool requiresNavigableWater = false;

        /// <summary> Minimum water depth at the quay edge the hub's vessels imply (metres); only if it needs water. </summary>
        public float minWaterDepthMetres = 0f;

        /// <summary> Whether the hub has clear-approach-surface requirements (the airport case). </summary>
        public bool requiresClearApproachSurface = false;

        /// <summary> The AUTHORED per-kind requirement set. Representative figures held for the Settlement Master
        /// review; the MECHANISM (declared contract + validator) does not change when they are replaced. </summary>
        public static HubRequirementsClass For(HubKind kind)
        {
            HubRequirementsClass r = new HubRequirementsClass();
            r.kind = kind;
            switch (kind)
            {
                case HubKind.RailStation:
                    r.requiredRoadCategory = "road.arterial";
                    r.accessDistanceMetres = 120f;
                    r.forecourtDepthMetres = 25f;
                    break;
                case HubKind.BusStation:
                    r.requiredRoadCategory = "road.arterial";
                    r.accessDistanceMetres = 60f;
                    r.forecourtDepthMetres = 40f; // multiple approach lanes + a turning path (ties to RM-28)
                    break;
                case HubKind.Port:
                    r.requiredRoadCategory = "road.arterial";
                    r.accessDistanceMetres = 200f;
                    r.forecourtDepthMetres = 60f;
                    r.requiresNavigableWater = true;
                    r.minWaterDepthMetres = 10f;
                    break;
                case HubKind.Airport:
                    r.requiredRoadCategory = "road.arterial";
                    r.accessDistanceMetres = 500f;
                    r.forecourtDepthMetres = 80f;
                    r.requiresClearApproachSurface = true;
                    break;
            }
            return r;
        }
    }

    /// <summary> The facts a scene supplies about a candidate site: whether the required road is within reach, how
    /// much forecourt depth exists, water adjacency and depth, and the result of sampling the approach surface. In the
    /// scene half these come from measuring the network and terrain; the validator turns them into a report and is
    /// scene-free so it is testable. </summary>
    public struct HubSiteFacts
    {
        public bool hasRequiredRoadWithinAccess;
        public double forecourtDepthAvailableMetres;
        public bool adjacentToNavigableWater;
        public double waterDepthMetres;
        public int approachObstructionCount;
        public double worstPenetrationMetres;
    }

    /// <summary> A single approach-surface obstruction, named by its along/lateral position and how far it penetrates
    /// the surface -- so the report can name the obstruction rather than just count it. </summary>
    public struct ApproachPenetration
    {
        public bool penetrates;
        public double alongMetres;
        public double lateralMetres;
        public double surfaceHeightMetres;
        public double penetrationMetres;
    }

    /// <summary> The validation report for one hub site: accepted only if nothing is unmet; the unmet requirements are
    /// listed in a fixed order so the report diffs between sessions (C-06) and RM-70 can assert on it. </summary>
    public struct HubValidationResult
    {
        public bool accepted;
        public IList<string> unmetRequirements;
    }

    /// <summary> RM-43's verifiable core: the hub-to-surroundings CONTRACT, without a scene. It declares what each hub
    /// requires and validates a site against it, producing a deterministic report; and it builds the airport approach
    /// surface from the standard so an obstruction is a real geometric penetration, not a guess. No hub silently
    /// accepts an impossible site. The prefab building, the platform layout and the crane -- and the scene sampling
    /// that fills HubSiteFacts -- are the deferred authored/scene half. </summary>
    public static class TransportHub
    {
        /// <summary> The surface half-width at a distance measured from the RUNWAY END. Zero before the surface's
        /// inner edge and beyond its far end (the point is not under the surface there). </summary>
        public static double ApproachHalfWidthAt(double alongFromRunwayEndMetres, ApproachSurfaceClass cls)
        {
            if (cls == null) { return 0.0; }
            double into = alongFromRunwayEndMetres - cls.distanceFromRunwayEndMetres;
            if (into < 0.0 || into > cls.lengthMetres) { return 0.0; }
            double w = cls.innerEdgeHalfWidthMetres + cls.divergenceEachSide * into;
            return w > 0.0 ? w : 0.0;
        }

        /// <summary> The surface elevation above the runway-end datum at a distance from the runway end. Zero before
        /// the inner edge; held at its far-end value beyond the surface (only meaningful with a footprint test). </summary>
        public static double ApproachSurfaceHeightAt(double alongFromRunwayEndMetres, ApproachSurfaceClass cls)
        {
            if (cls == null) { return 0.0; }
            double into = alongFromRunwayEndMetres - cls.distanceFromRunwayEndMetres;
            if (into <= 0.0) { return 0.0; }
            if (into > cls.lengthMetres) { into = cls.lengthMetres; }
            return cls.slope * into;
        }

        /// <summary> Whether a point (along the extended centreline, lateral from it) lies under the surface footprint. </summary>
        public static bool IsUnderApproachSurface(double alongFromRunwayEndMetres, double lateralMetres,
            ApproachSurfaceClass cls)
        {
            double half = ApproachHalfWidthAt(alongFromRunwayEndMetres, cls);
            if (half <= 0.0) { return false; }
            double lat = lateralMetres < 0.0 ? -lateralMetres : lateralMetres;
            return lat <= half;
        }

        /// <summary> Evaluate one obstacle against the approach surface: an obstacle whose top rises above the surface
        /// at its footprint position penetrates it. Outside the footprint it does not penetrate (it is not under the
        /// surface). </summary>
        public static ApproachPenetration EvaluateApproachObstacle(double alongFromRunwayEndMetres,
            double lateralMetres, double obstacleHeightAboveDatumMetres, ApproachSurfaceClass cls)
        {
            ApproachPenetration p = new ApproachPenetration();
            p.alongMetres = alongFromRunwayEndMetres;
            p.lateralMetres = lateralMetres;
            if (!IsUnderApproachSurface(alongFromRunwayEndMetres, lateralMetres, cls))
            {
                p.penetrates = false;
                p.surfaceHeightMetres = 0.0;
                p.penetrationMetres = 0.0;
                return p;
            }
            p.surfaceHeightMetres = ApproachSurfaceHeightAt(alongFromRunwayEndMetres, cls);
            p.penetrationMetres = obstacleHeightAboveDatumMetres - p.surfaceHeightMetres;
            p.penetrates = p.penetrationMetres > 0.0;
            return p;
        }

        /// <summary> Validate a candidate site against a hub's declared requirements. The report lists every unmet
        /// requirement in a fixed order, by name, and accepts the site only if the list is empty -- no hub silently
        /// accepts an impossible site. </summary>
        public static HubValidationResult Validate(HubRequirementsClass req, HubSiteFacts facts)
        {
            List<string> unmet = new List<string>();
            HubValidationResult result = new HubValidationResult();
            if (req == null)
            {
                result.accepted = false;
                unmet.Add("no requirements were declared for this hub");
                result.unmetRequirements = unmet;
                return result;
            }

            if (!string.IsNullOrEmpty(req.requiredRoadCategory) && !facts.hasRequiredRoadWithinAccess)
            {
                unmet.Add("no road of class '" + req.requiredRoadCategory + "' within "
                          + req.accessDistanceMetres.ToString("0.#") + " m access distance");
            }
            if (req.forecourtDepthMetres > 0f && facts.forecourtDepthAvailableMetres + 1e-9 < req.forecourtDepthMetres)
            {
                unmet.Add("forecourt depth " + facts.forecourtDepthAvailableMetres.ToString("0.#")
                          + " m is below the required " + req.forecourtDepthMetres.ToString("0.#") + " m");
            }
            if (req.requiresNavigableWater)
            {
                if (!facts.adjacentToNavigableWater)
                {
                    unmet.Add("not adjacent to navigable water");
                }
                else if (facts.waterDepthMetres + 1e-9 < req.minWaterDepthMetres)
                {
                    unmet.Add("water depth " + facts.waterDepthMetres.ToString("0.#")
                              + " m is below the required " + req.minWaterDepthMetres.ToString("0.#") + " m");
                }
            }
            if (req.requiresClearApproachSurface && facts.approachObstructionCount > 0)
            {
                unmet.Add("approach surface obstructed by " + facts.approachObstructionCount
                          + " obstruction(s), worst penetration " + facts.worstPenetrationMetres.ToString("0.#") + " m");
            }

            result.unmetRequirements = unmet;
            result.accepted = unmet.Count == 0;
            return result;
        }
    }
}

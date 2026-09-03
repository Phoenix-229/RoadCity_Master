#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.Roads;   // RoadAutomation: the legacy programmatic-creation entry the installer builds through
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> What installing an alignment did: the road it built, how many nodes it put down, and the fit report it
    /// stands on. The report is carried through so an author who installs from a menu sees the same "worst departure ..."
    /// measurement the gate asserts on -- a road that quietly missed its tolerance would otherwise look identical to one
    /// that met it. </summary>
    public struct AlignmentInstallResult
    {
        public Road road;
        public int nodeCount;
        public string report;
    }


    /// <summary> The one place RM-75's authored geometry becomes real nodes.
    ///
    /// The pipeline is deliberately one-directional: an authored <see cref="HorizontalAlignment"/> is RESOLVED into
    /// tangents, transitions and arcs, FITTED to a dense polyline (RM-75's rule -- the spline is fitted to the plan, not
    /// replaced by it), and only here are those points turned into the SplineN nodes the rest of the tool already knows
    /// how to build a road from. Everything downstream -- the construction chain, the arc-length mapping, the edge
    /// objects, the intersections -- is untouched, because it still receives a road made of nodes and never learns the
    /// nodes came from a clothoid.
    ///
    /// The fitter leaves Y at zero on purpose: the horizontal alignment has no opinion about elevation. This installer is
    /// the layer that gives the points a height, sampling the SAME reader the corridor and the overlay sampled, so the
    /// road sits on the ground the analysis was run against rather than on a plane. A null reader lays the road flat, so
    /// the operator still runs in a scene with no terrain wired up.
    ///
    /// EVERYTHING it does is one undo step. An author who installs an alignment and dislikes it presses undo once and the
    /// whole road -- system, road object, every node -- is gone; a half-removed road would be worse than none. </summary>
    public static class AlignmentInstaller
    {
        /// <summary> Nodes below this height are lifted to it, matching RoadAutomation's own floor so an installed road
        /// behaves exactly like a hand-drawn one. </summary>
        public const float MinimumNodeHeightMetres = 0.03f;

        /// <summary> Resolve, fit and install a plan as a new road under a (possibly new) road system, in one undo step.
        ///
        /// The system is created if none is passed, so the common case -- an author with an empty scene and a plan --
        /// is one call. Road updates are suspended across the build the way RoadAutomation documents they must be, then
        /// restored and run once, so a hundred-node alignment costs one rebuild rather than a hundred. </summary>
        public static AlignmentInstallResult Install(HorizontalAlignment plan, RoadClass cls,
            SuperelevationClass superelevation, RoadSystem system, ITerrainSurfaceReader reader, out string report)
        {
            AlignmentInstallResult result = new AlignmentInstallResult();
            report = string.Empty;

            if (plan == null || plan.PointCount < 2)
            {
                report = "nothing to install: an alignment needs at least two authored points";
                result.report = report;
                return result;
            }

            AlignmentSolution solution = AlignmentResolver.Resolve(plan, cls, superelevation);
            AlignmentFit fit = AlignmentFitter.Fit(solution, plan.chordToleranceMetres);
            if (fit.PointCount < 2)
            {
                report = "nothing to install: the plan resolved to no drawable geometry";
                result.report = report;
                return result;
            }

            List<Vector3> lifted = LiftToSurface(fit, reader);

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install horizontal alignment");

            bool createdSystem = system == null;
            if (createdSystem)
            {
                GameObject systemObject = new GameObject("RoadCityMasterSystem");
                Undo.RegisterCreatedObjectUndo(systemObject, "Install horizontal alignment");
                system = systemObject.AddComponent<RoadSystem>();
            }

            bool previousAllow = system.isAllowingRoadUpdates;
            system.isAllowingRoadUpdates = false;

            Road road = RoadAutomation.CreateRoadProgrammatically(system, ref lifted);
            if (road != null)
            {
                Undo.RegisterCreatedObjectUndo(road.gameObject, "Install horizontal alignment");
            }

            system.isAllowingRoadUpdates = previousAllow;
            if (previousAllow && road != null) { road.UpdateRoad(); }

            Undo.CollapseUndoOperations(group);

            result.road = road;
            result.nodeCount = lifted.Count;
            result.report = ReportFor(solution, fit, lifted, reader);
            report = result.report;
            return result;
        }

        /// <summary> Lift the fitted polyline onto the surface. The fitter deliberately left Y at zero; here each point
        /// is given the terrain height under it, floored the same way a hand-placed node is, so nothing installed sits
        /// below the ground or under water at Y = 0. A null reader means "no terrain to sample", which lays the road on
        /// the plane rather than refusing to build it. </summary>
        private static List<Vector3> LiftToSurface(AlignmentFit fit, ITerrainSurfaceReader reader)
        {
            List<Vector3> lifted = new List<Vector3>(fit.PointCount);
            for (int i = 0; i < fit.points.Count; i++)
            {
                Vector3 p = fit.points[i];
                float y = reader != null ? reader.SampleHeight(p.x, p.z) : 0f;
                if (y < MinimumNodeHeightMetres) { y = MinimumNodeHeightMetres; }
                lifted.Add(new Vector3(p.x, y, p.z));
            }
            return lifted;
        }

        /// <summary> The install in words: what the fit measured, how many nodes went down, and how far they climb.
        /// The rise is stated because it is the one thing the horizontal plan could not know and the reader just
        /// supplied -- an author reads it to see whether the alignment they drew on the flat crosses ground it should
        /// not. </summary>
        private static string ReportFor(AlignmentSolution solution, AlignmentFit fit, List<Vector3> lifted,
            ITerrainSurfaceReader reader)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("installed ").Append(lifted.Count.ToString(CultureInfo.InvariantCulture)).Append(" nodes over ");
            sb.Append(Number(solution.lengthMetres)).Append(" m\n  ").Append(fit.report);

            if (reader != null && lifted.Count > 0)
            {
                float low = lifted[0].y;
                float high = lifted[0].y;
                for (int i = 1; i < lifted.Count; i++)
                {
                    if (lifted[i].y < low) { low = lifted[i].y; }
                    if (lifted[i].y > high) { high = lifted[i].y; }
                }
                sb.Append("\n  lifted onto the surface between ").Append(Number(low)).Append(" m and ");
                sb.Append(Number(high)).Append(" m");
            }
            else
            {
                sb.Append("\n  no terrain reader supplied, so the road was laid flat at the minimum node height");
            }
            return sb.ToString();
        }

        private static string Number(double value)
        {
            if (double.IsInfinity(value)) { return "no limit"; }
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif

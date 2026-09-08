// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.City;
using RoadCityMaster.City.Editor;
#endregion


namespace RoadCityMaster
{
    /// <summary> B-28's editor-side handoff. A bake owns the raster and corridor only until it finishes; this bridge
    /// moves those typed outputs into the existing scene tools and leaves the serialized alignment on the authoring
    /// component. It is the only caller of the four hand-over methods, so an overlay cannot appear enabled with no
    /// payload behind it. </summary>
    public static class CityBakeOutputBridge
    {
        /// <summary> Applies all outputs available for one city. Missing optional outputs are reported rather than
        /// replaced with a map or plan invented by the bridge. </summary>
        public static bool Apply(CityAuthoring city, out string note)
        {
            note = string.Empty;
            if (city == null || city.graph == null)
            {
                note = "there is no city graph whose finished outputs can be handed to the scene tools";
                return false;
            }

            string owner = CityBakeTicket.OwnerOf(city.graph, new CityBakeRequest());
            return Apply(city, owner, out note);
        }

        /// <summary> The owner-aware form used by the inspector, whose request may carry a source asset GUID. </summary>
        public static bool Apply(CityAuthoring city, string owner, out string note)
        {
            note = string.Empty;
            if (city == null || city.graph == null)
            {
                note = "there is no city graph whose finished outputs can be handed to the scene tools";
                return false;
            }

            SuitabilityRasterClass map = null;
            string legend = string.Empty;
            CorridorPlan corridor = null;
            CityGraphNode rasterNode = Find(city.graph, CityNodeKind.SuitabilityRaster);
            CityGraphNode corridorNode = Find(city.graph, CityNodeKind.CorridorSolve);
            object value;
            if (rasterNode != null && CityBakeTicket.TryTakeOutput(owner, rasterNode.id, "suitability", out value))
            {
                map = value as SuitabilityRasterClass;
                object legendValue;
                if (CityBakeTicket.TryTakeOutput(owner, rasterNode.id, "legend", out legendValue))
                {
                    legend = legendValue as string ?? string.Empty;
                }
            }
            if (corridorNode != null && CityBakeTicket.TryTakeOutput(owner, corridorNode.id, "corridor", out value))
            {
                corridor = value as CorridorPlan;
            }

            if (map == null && corridor == null)
            {
                note = "the finished bake exposed neither a suitability raster nor a corridor plan";
                return false;
            }

            if (map != null) { SuitabilityOverlay.SetMap(map, legend, new RoadClass(), null); }
            SuitabilityOverlay.SetPlan(corridor);
            AlignmentHandles.SetCorridor(map, corridor, null);
            AlignmentHandles.SetPlan(city.authoredAlignment, new RoadClass(), SuperelevationClass.Default(), city);
            EditorUtility.SetDirty(city);
            note = "finished suitability and corridor outputs handed to the overlay and alignment handles";
            return true;
        }

        /// <summary> The author-facing way in to the existing installer. It consumes the serialized plan rather than
        /// constructing one in the bridge, and returns the installer's measured report unchanged. </summary>
        public static AlignmentInstallResult InstallOwnedAlignment(CityAuthoring city,
            ITerrainSurfaceReader reader, out string report)
        {
            if (city == null || city.authoredAlignment == null)
            {
                report = "nothing to install: this city has no authored alignment plan";
                return new AlignmentInstallResult();
            }
            return AlignmentInstaller.Install(city.authoredAlignment, new RoadClass(),
                SuperelevationClass.Default(), null, reader, out report);
        }

        private static CityGraphNode Find(CityGraph graph, CityNodeKind kind)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] != null && graph.nodes[i].kind == kind) { return graph.nodes[i]; }
            }
            return null;
        }
    }
}
#endif
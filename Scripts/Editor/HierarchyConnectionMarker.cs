// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-53's core. Terrain mode is invisible today and its consequences are large: connected, the tool cuts
    /// terrain through a provider; standalone, it terraforms directly and cannot cut a bore, which an author reads as a
    /// bug. This puts the answer on the hierarchy row, where the author is already looking. The vocabulary is
    /// deliberately three words wide -- connected, standalone, and "a linker is entered but the provider it names is
    /// missing", the case that otherwise degrades silently into standalone behaviour. The state is CACHED and pushed on
    /// change, never probed per repaint: the hierarchy redraws constantly and a provider lookup per row per frame is a
    /// measurable editor stall in a scene with many roads. Editor-only, so no runtime code depends on it. </summary>

    /// <summary> The three states worth distinguishing on a row. </summary>
    public enum ConnectionMarkerState { Standalone, Connected, LinkerBroken }

    /// <summary> What the row draws: a short glyph and the tooltip behind it. </summary>
    public struct MarkerView
    {
        public ConnectionMarkerState state;
        public string glyph;
        public string tooltip;
    }

    /// <summary> The pure resolver plus the push-on-change cache the row draws from. </summary>
    public static class HierarchyConnectionMarker
    {
        private static readonly Dictionary<EntityId, MarkerView> _cache = new Dictionary<EntityId, MarkerView>();
        private static int _probeCount;   // counts resolves; a per-repaint probe would make this climb with redraws

        /// <summary> Resolve the marker from the three facts that decide it. Pure: no scene lookup, no provider probe.
        /// A linker entered whose provider cannot be resolved is NOT reported as connected and NOT silently reported as
        /// standalone -- it is its own state, so a deleted provider is fixed in the moment. </summary>
        public static MarkerView Resolve(bool linkerEntered, bool providerResolved, string providerName)
        {
            _probeCount++;
            MarkerView v = new MarkerView();
            if (linkerEntered && !providerResolved)
            {
                v.state = ConnectionMarkerState.LinkerBroken;
                v.glyph = "!";
                string named = string.IsNullOrEmpty(providerName) ? "the named provider" : providerName;
                v.tooltip = "A terrain linker is entered but " + named
                          + " is missing: terrain cuts will not run. Fix the linker reference.";
                return v;
            }
            if (linkerEntered && providerResolved)
            {
                v.state = ConnectionMarkerState.Connected;
                v.glyph = "=";
                string name = string.IsNullOrEmpty(providerName) ? "an unnamed provider" : providerName;
                v.tooltip = "Connected to " + name + ": terrain cuts, tunnel cavities and building pads are available.";
                return v;
            }
            v.state = ConnectionMarkerState.Standalone;
            v.glyph = "~";
            v.tooltip = "Standalone: direct terraform only; terrain cuts, tunnel cavities and building pads "
                      + "are unavailable.";
            return v;
        }

        /// <summary> Push a resolved state into the cache, keyed by the object's EntityId (Unity 6.5's stable object
        /// identity; GetInstanceID is obsolete). Called when the linker changes, not per repaint. </summary>
        public static void Push(EntityId entityId, MarkerView view)
        {
            _cache[entityId] = view;
            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary> Read the cached state for a row. Returns false on a miss so the row draws nothing rather than
        /// triggering a lookup mid-repaint. </summary>
        public static bool TryGet(EntityId entityId, out MarkerView view)
        {
            return _cache.TryGetValue(entityId, out view);
        }

        /// <summary> Drop a cached row (object deleted or scene unloaded). </summary>
        public static void Forget(EntityId entityId) { _cache.Remove(entityId); }

        /// <summary> Clear everything -- used by the gate and on scene change. </summary>
        public static void Clear() { _cache.Clear(); _probeCount = 0; }

        /// <summary> How many times the resolver has run. The gate uses this to prove drawing a row does NOT resolve. </summary>
        public static int ProbeCount { get { return _probeCount; } }

        /// <summary> The marker must never contradict RM-52's panel: Connected agrees only with a Connected status, and
        /// both Standalone and LinkerBroken agree only with a Standalone one (a broken linker is not a connection). </summary>
        public static bool AgreesWith(ConnectionMarkerState state, TerrainProviderMode panelMode)
        {
            if (state == ConnectionMarkerState.Connected) { return panelMode == TerrainProviderMode.Connected; }
            return panelMode == TerrainProviderMode.Standalone;
        }

        /// <summary> Draw the cached glyph at the right-hand end of a hierarchy row. Registered once; reads the cache
        /// only. Visual placement is for the author to confirm in the editor. Uses Unity 6.5's EntityId-keyed hierarchy
        /// callback (the int-keyed hierarchyWindowItemOnGUI is obsolete) and keys the cache by the same EntityId the
        /// callback delivers, so the row never touches the obsolete GetInstanceID and never probes the scene. </summary>
        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnRow;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnRow;
        }

        private static void OnRow(EntityId entityId, Rect selectionRect)
        {
            MarkerView view;
            if (!TryGet(entityId, out view)) { return; }
            Rect r = new Rect(selectionRect.xMax - 18f, selectionRect.y, 16f, selectionRect.height);
            GUI.Label(r, new GUIContent(view.glyph, view.tooltip));
        }
    }
}
#endif

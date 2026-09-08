// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Deciding the mode once, from what is actually in the scene, and installing the result.
    ///
    /// This is the other half of the one-mode-decision rule. The gateway refuses to be talked into changing mode, so
    /// somebody has to make the decision, once, from evidence: is there a terrain provider in this scene or not. That
    /// happens here and nowhere else.
    ///
    /// It is a scan, so it is deliberately NOT something that happens per frame or per road. It runs once when a scene
    /// finishes loading, and again whenever a caller says the scene changed -- an inspector button, a gate, a tool that
    /// just created the link component. Between those moments the answer is cached, because the answer cannot change
    /// on its own.
    ///
    /// Ordering is by hierarchy path rather than by the order the engine happens to return objects in, so a scene with
    /// two providers picks the same one every session (C-06). </summary>
    public static class TerrainGatewayBootstrap
    {
        private static string lastDecision = "not decided yet";
        private static bool decided;

        /// <summary> What the last scan concluded, in words, for the status panel and the inspector. </summary>
        public static string LastDecision { get { return lastDecision; } }

        /// <summary> Whether a scan has happened at all. </summary>
        public static bool Decided { get { return decided; } }

        /// <summary> Run once after a scene loads. One scan per scene load is cheap and is what makes connected mode
        /// engage without the user wiring anything up; scanning more often than that would be waste, since the answer
        /// cannot change unless the scene does. </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            Refresh();
        }

        /// <summary> Scan the scene, decide the mode, install the gateway. Returns true if a provider was found and
        /// connected mode is now in force. Safe to call repeatedly; each call re-decides from scratch, which is what
        /// makes it the right thing to call after adding or removing a link component. </summary>
        public static bool Refresh()
        {
            decided = true;

            // RM-73: an ENTERED reference outranks any scan. A linker component in the scene means the author has
            // stated which object owns the terrain -- or has stated, by leaving it empty, that nothing does -- and a
            // stated answer is never overruled by a guessed one. The scan below survives only as the zero-wiring
            // convenience for a scene that has no linker at all.
            TerrainLinker linker = FindLinker();
            if (linker != null)
            {
                bool connected = linker.Acquire();
                if (!connected) { FeedTerrains(); }
                lastDecision = linker.Report.sentence;
                return connected;
            }

            string providerPath;
            IPCGDataProvider provider = FindProvider(out providerPath);

            if (provider == null)
            {
                TerrainWriteGateway.Install(TerrainWriteGateway.ForStandalone());
                FeedTerrains();
                lastDecision = "Standalone: no terrain provider in this scene, so the road tool shapes the terrain "
                    + "itself, exactly as it does on its own. To change that, "
                    + TerrainFeatureAvailability.WhatToConnect + ".";
                return false;
            }

            TerrainWriteGateway.Install(TerrainWriteGateway.ForProvider(provider, providerPath));
            lastDecision = "Connected to '" + providerPath + "': that component owns the terrain, and every change "
                + "the road tool needs is requested from it rather than written directly.";
            return true;
        }

        /// <summary> The linker in this scene, or null. Chosen by hierarchy path for the same reason the provider is
        /// (C-06), and disabled ones are ignored: a linker an author switched off is not a statement about the
        /// terrain. </summary>
        private static TerrainLinker FindLinker()
        {
            TerrainLinker[] found = Object.FindObjectsByType<TerrainLinker>();
            TerrainLinker best = null;
            string bestPath = string.Empty;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || !found[i].enabled) { continue; }
                string path = PathOf(found[i].transform);
                if (best != null && string.CompareOrdinal(path, bestPath) >= 0) { continue; }
                best = found[i];
                bestPath = path;
            }
            return best;
        }

        /// <summary> Force standalone regardless of what is in the scene, for a user who wants the tool to shape the
        /// terrain itself even though a provider is present. Stated rather than silent: the decision is reported the
        /// same way a scan's is. </summary>
        public static void UseStandalone()
        {
            decided = true;
            TerrainWriteGateway.Install(TerrainWriteGateway.ForStandalone());
            FeedTerrains();
            lastDecision = "Standalone by choice: a terrain provider is available but the road tool has been told to "
                + "shape the terrain itself. Features that need a provider will say so.";
        }

        /// <summary> The provider in this scene, or null. Chosen by hierarchy path so two providers resolve the same
        /// way every session. </summary>
        private static IPCGDataProvider FindProvider(out string providerPath)
        {
            providerPath = string.Empty;
            // No sort mode: the `FindObjectsSortMode` overload is obsolete from Unity 6 (CS0618) because the order it
            // promised can no longer be maintained. Nothing is lost here -- this method never relied on the returned
            // order, it picks the lowest hierarchy path below, which is what makes the choice deterministic (C-06).
            MonoBehaviour[] candidates = Object.FindObjectsByType<MonoBehaviour>();
            IPCGDataProvider best = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                IPCGDataProvider asProvider = candidates[i] as IPCGDataProvider;
                if (asProvider == null || !candidates[i].enabled) { continue; }
                string path = PathOf(candidates[i].transform);
                if (best != null && string.CompareOrdinal(path, providerPath) >= 0) { continue; }
                best = asProvider;
                providerPath = path;
            }
            return best;
        }

        /// <summary> Hand the scene's terrains to the direct sink, so standalone sampling has something to read. The
        /// sink does not look for them itself: what it samples must be what it was given (C-06). Sorted by path for
        /// the same reason. </summary>
        private static void FeedTerrains()
        {
            DirectTerrainSink sink = TerrainWriteGateway.Active.Sink as DirectTerrainSink;
            if (sink == null) { return; }

            // Same obsolete overload as `FindProvider`, and the same reason it costs nothing: the list is sorted by
            // hierarchy path on the next two lines, so the engine's return order was never load-bearing.
            Terrain[] found = Object.FindObjectsByType<Terrain>();
            List<Terrain> ordered = new List<Terrain>(found.Length);
            for (int i = 0; i < found.Length; i++) { ordered.Add(found[i]); }
            ordered.Sort(CompareByPath);
            sink.UseTerrains(ordered);
        }

        private static int CompareByPath(Terrain a, Terrain b)
        {
            if (a == null) { return b == null ? 0 : 1; }
            if (b == null) { return -1; }
            return string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform));
        }

        /// <summary> A transform's full hierarchy path. Stable across sessions, unlike an instance id. Public because
        /// RM-73's linker names the entered provider the same way, and two implementations of "where is this object"
        /// is how the panel and the marker end up disagreeing. </summary>
        public static string PathOf(Transform transform)
        {
            if (transform == null) { return string.Empty; }
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}

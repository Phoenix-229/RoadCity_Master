// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-109. The edit-mode frame source for the shared build queue.
    ///
    /// The queue is a static object with no frames of its own: <see cref="City.BuildDispatch.Pump"/> performs at most
    /// one phase transition and must be called again for the next, which is exactly what keeps a long list of roads
    /// spread across frames instead of freezing the editor in one blocking pass. Something therefore has to call it
    /// every frame, and until this file existed the only caller in edit mode was <see cref="RoadSystem"/> -- a scene
    /// component, subscribing from <c>OnEnable</c>.
    ///
    /// That was the whole of B-1. A queue whose driver depends on a scene component can be stopped by the component
    /// being absent, disabled, not yet loaded, ordered after the road that queued the build, or simply not receiving
    /// the edit-mode message it subscribed from -- and when it stops, nothing errors. The build sits at the head of the
    /// queue with its progress row live and its seconds counter climbing, forever, which is what an author sees as "the
    /// road is always loading and never stops" and why only the <c>Spline</c> object appeared: the mesh had been asked
    /// for and never built. The road then materialised on clicking a spline node because the node inspector calls the
    /// synchronous <c>UpdateRoad</c> directly (<c>SplineNEditor.cs:414</c>), stepping around the dead queue entirely.
    ///
    /// A static queue gets a static driver. <c>[InitializeOnLoad]</c> runs once per domain load, before any scene
    /// component is consulted, so from here on no road's build can be blocked by the state, the ordering or the absence
    /// of anything in the scene. This removes the failure class, not this one instance of it.
    ///
    /// Cost when there is nothing to build: one delegate call and two integer reads per editor frame.
    /// <see cref="City.BuildDispatch.Pump"/> returns immediately when it is neither generating nor holding anything
    /// pending, so an idle project pays nothing measurable and nothing is left spinning. The idle test is deliberately
    /// NOT repeated here -- one definition of "there is work" lives in the dispatcher, so the driver cannot come to a
    /// different conclusion than the thing it is driving.
    ///
    /// Play mode is not this driver's. <c>EditorApplication.update</c> keeps firing during play-in-editor, so pumping
    /// here as well as from <see cref="RoadSystem"/> would perform two transitions a frame in the editor and one in a
    /// player, making a build that behaves differently in the two. Edit mode is driven here; play mode is driven by
    /// <see cref="RoadSystem"/>, in the editor and in a player alike. Exactly one owner in every configuration.
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildPumpDriver
    {
        /// <summary> Runs once per domain load. The <c>-=</c> before the <c>+=</c> is insurance rather than ceremony:
        /// it costs nothing, and it means a second initialisation can never leave two subscriptions pumping the same
        /// queue twice a frame. A named static method is required for that to work at all -- <c>-=</c> on an anonymous
        /// delegate removes a different instance than <c>+=</c> added, and so removes nothing. </summary>
        static BuildPumpDriver()
        {
            EditorApplication.update -= PumpEditorFrame;
            EditorApplication.update += PumpEditorFrame;
        }


        private static void PumpEditorFrame()
        {
            if (UnityEngine.Application.isPlaying) { return; }
            City.BuildDispatch.Shared.Pump();
        }
    }
}
#endif

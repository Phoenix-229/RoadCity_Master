// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A terrain provider as a COMPONENT, which is what RM-73's linker verifies against.
    ///
    /// `FakePcgTerrain` is a plain class and cannot be entered into a component field, so it cannot exercise the one
    /// thing this gate is about: a reference an author entered into a scene. This is that provider, with the two states
    /// that decide the verification -- ready, and present-but-not-yet-loaded, which is a real state for one editor tick
    /// during scene load and the only failure the linker retries.
    ///
    /// WHY IT IS IN City/Runtime AND NOT BESIDE THE OTHER TEST DOUBLES IN City/Editor/TestSupport
    /// -------------------------------------------------------------------------------------------
    /// Unity refuses to attach a MonoBehaviour that was compiled into an editor assembly:
    /// <c>AddComponent</c> logs "Can't add script behaviour ... because it is an editor script" and returns null,
    /// so the gate that wants a provider in a scene got a null reference instead. The other doubles beside it are
    /// plain classes and are unaffected; this one is a component, so it has to be compiled into the runtime
    /// assembly to be attachable at all.
    ///
    /// Being in the runtime assembly is not the same as shipping: the whole file is inside
    /// <c>#if UNITY_EDITOR</c>, so the type does not exist in a player build and nothing shipped can reference it.
    /// The assembly-boundary audit judges files under an Editor folder, and this is not one, so the move is
    /// invisible to it.
    ///
    /// The namespace follows the folder as everything else here does -- <c>City/Runtime/**</c> is
    /// <c>RoadCityMaster.City</c>. The two gates that use it are in <c>RoadCityMaster.City.Editor</c>, which is
    /// nested inside that, so they still resolve the name with no import. </summary>
    public sealed class FakeProviderComponent : MonoBehaviour, IPCGDataProvider
    {
        /// <summary> Off means the provider declares no loaded region: alive, implements the contract, and has nowhere
        /// inside its own bounds to answer a probe. </summary>
        public bool regionsLoaded = true;

        /// <summary> Answer a probe with a value that is not a number, which a provider mid-rebuild can genuinely do
        /// and which must be treated as "did not answer" rather than as a height of zero. </summary>
        public bool answerNotANumber;

        /// <summary> Throw instead of answering, so the gate can prove the linker reports rather than propagates.
        /// </summary>
        public bool throwOnSample;

        public int sampleCalls;
        public int boundsCalls;

        /// <summary> Where the last probe asked. The linker promises to probe inside the provider's own bounds and to
        /// choose the same one every session; without recording the point, that promise is untestable. </summary>
        public float lastSampleX;
        public float lastSampleZ;

        public float SampleHeight(float worldX, float worldZ)
        {
            sampleCalls++;
            lastSampleX = worldX;
            lastSampleZ = worldZ;
            if (throwOnSample) { throw new System.InvalidOperationException("provider not ready"); }
            if (answerNotANumber) { return float.NaN; }
            return 12.5f;
        }

        public Vector3 SampleNormal(float worldX, float worldZ) { return Vector3.up; }

        public List<Bounds> GetActiveChunkBounds()
        {
            boundsCalls++;
            List<Bounds> bounds = new List<Bounds>();
            if (!regionsLoaded) { return bounds; }
            // Two regions, deliberately entered in the order that is NOT the probe order, so the gate can prove the
            // probe point is chosen by lowest centre rather than by list position (C-06).
            bounds.Add(new Bounds(new Vector3(800f, 0f, 0f), new Vector3(400f, 200f, 400f)));
            bounds.Add(new Bounds(new Vector3(0f, 0f, 0f), new Vector3(400f, 200f, 400f)));
            return bounds;
        }

        public void NotifyCutRegion(Bounds worldBounds) { }
        public int RequestTunnelOpening(Bounds worldBounds) { return 1; }
        public void ReleaseTunnelOpening(int handle) { }
    }
}
#endif

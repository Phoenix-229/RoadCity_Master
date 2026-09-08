// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-73's states, and the reason there are four rather than two.
    ///
    /// "Connected or not" is the answer an author gets from most integrations, and it is the answer that hides the two
    /// failures worth naming. A reference that was entered and no longer resolves must NOT read as standalone -- the
    /// tool would then quietly stop cutting terrain and look like a bug in the road. And a provider that resolves but
    /// speaks a surface this tool was not written against must NOT read as connected -- routing work through a guessed
    /// subset of a changed contract is how a scene gets half-carved. </summary>
    public enum ProviderLinkState
    {
        /// <summary> Nothing entered. Standalone, legitimately, and the tool says so. </summary>
        NotEntered = 0,

        /// <summary> Entered, verified, probed, and in force. </summary>
        Connected = 1,

        /// <summary> Entered, but what it names is gone, is not a terrain provider, or would not answer a probe.
        /// </summary>
        EnteredButBroken = 2,

        /// <summary> Entered and alive, but the provider surface is not the one this tool understands. Its own state so
        /// the message can carry both version numbers instead of guessing which calls still work. </summary>
        InterfaceVersionMismatch = 3
    }


    /// <summary> The trivial question asked at acquire time, and its answer. A height inside the provider's own
    /// declared bounds is the cheapest call on the contract that cannot succeed by accident. </summary>
    public struct ProviderLinkProbe
    {
        public bool answered;
        public float height;
        public int declaredRegions;
        public string note;
    }


    /// <summary> What one acquire attempt concluded. This is the single shape RM-52's status panel and RM-53's
    /// hierarchy marker both read: the marker wants three facts (a reference was entered, it resolved, its name) and
    /// the panel wants the mode and the name, so neither has to repeat the verification to find out. </summary>
    public struct ProviderLinkReport
    {
        public ProviderLinkState state;

        /// <summary> The provider's own name, read from the connected object rather than typed here -- the same
        /// property that lets a second path-authoring tool occupy the socket without a rename (C-05). </summary>
        public string providerName;

        /// <summary> Hierarchy path, for the two-providers-in-one-scene case and for a stable log line. </summary>
        public string providerPath;

        public int surfaceMembers;
        public int understoodMembers;
        public ProviderLinkProbe probe;

        /// <summary> Holds the gateway still owns at the moment of the report. Visible state, not private
        /// bookkeeping: RM-52 shows it and RM-72 asserts on it. </summary>
        public int holdCount;

        /// <summary> One sentence an author can act on. </summary>
        public string sentence;

        /// <summary> True whenever a reference was entered at all, however badly it turned out -- RM-53's first fact.
        /// </summary>
        public bool LinkerEntered
        {
            get { return state != ProviderLinkState.NotEntered; }
        }

        /// <summary> True only when work may actually be routed -- RM-53's second fact. A mismatch is entered and
        /// alive and still not resolved, which is the whole point of separating the two. </summary>
        public bool ProviderResolved
        {
            get { return state == ProviderLinkState.Connected; }
        }

        /// <summary> The mode this report implies. Only a verified link is connected; every failure is standalone in
        /// behaviour while being reported as its own state in words. </summary>
        public TerrainProviderMode Mode
        {
            get
            {
                return state == ProviderLinkState.Connected
                    ? TerrainProviderMode.Connected : TerrainProviderMode.Standalone;
            }
        }

        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(state.ToString()).Append("  provider '").Append(providerName)
              .Append("' at '").Append(providerPath).Append("'  surface ")
              .Append(surfaceMembers.ToString(CultureInfo.InvariantCulture)).Append('/')
              .Append(understoodMembers.ToString(CultureInfo.InvariantCulture))
              .Append("  probe ").Append(probe.answered)
              .Append("  regions ").Append(probe.declaredRegions.ToString(CultureInfo.InvariantCulture))
              .Append("  holds ").Append(holdCount.ToString(CultureInfo.InvariantCulture))
              .Append("\n  ").Append(sentence);
            return sb.ToString();
        }
    }


    /// <summary> Acquire and verify, in the road tool's own assembly, with nothing thrown.
    ///
    /// The direction of the connection is the design decision this file defends. The alternative -- the road tool
    /// scans the scene for a component that looks like a provider -- gives no answer with two candidates, no answer
    /// when the intended one is disabled, and a plausible wrong answer when a test object was left behind. An entered
    /// reference is unambiguous, is serialised with the scene so it survives a reload, and makes the author's intent
    /// explicit. It also makes the failures namable, which is what RM-52's panel and RM-53's marker are built on.
    ///
    /// Verification is what turns a reference into a contract, and it runs in a fixed order so the state it produces
    /// is the FIRST thing that was wrong rather than the last: is it still there, is it a provider at all, is its
    /// surface the one this tool understands, and does it answer a trivial question. Nothing here throws: a provider
    /// present but not yet ready is a real state during scene load, and an exception on load is the version of this
    /// failure that costs an afternoon to diagnose. </summary>
    public static class ProviderLinkContract
    {
        /// <summary> How many members of the provider surface this tool was written against. Not a guess and not a
        /// version string the provider has to volunteer: it is read off the interface at runtime and compared, so a
        /// contract that grows a member is reported as drift instead of being silently half-used. Bumped in ONE place
        /// when the tool learns the new member. </summary>
        public const int UnderstoodMemberCount = 6;

        /// <summary> The name shown when the provider offers none of its own. </summary>
        public const string UnnamedProvider = "an unnamed provider";

        /// <summary> The members the surface actually publishes right now. </summary>
        public static int SurfaceMemberCount()
        {
            MethodInfo[] methods = typeof(IPCGDataProvider).GetMethods();
            return methods != null ? methods.Length : 0;
        }

        /// <summary> Verify one entered reference and say what it is. `remembered` is the provider name this link
        /// recorded the last time it succeeded, serialised with the scene: without it a reference that has since been
        /// deleted is indistinguishable from a field nobody ever filled in, and the two need different sentences.
        /// </summary>
        public static ProviderLinkReport Verify(Object entered, string enteredPath, string remembered, int holdCount)
        {
            ProviderLinkReport r = new ProviderLinkReport();
            r.providerPath = enteredPath != null ? enteredPath : string.Empty;
            r.understoodMembers = UnderstoodMemberCount;
            r.surfaceMembers = SurfaceMemberCount();
            r.holdCount = holdCount;

            // Unity's own == , which is false for a destroyed object as well as for a null one. That is the whole
            // reason this takes UnityEngine.Object rather than object: a plain null test would call a deleted
            // provider alive.
            if (entered == null)
            {
                if (string.IsNullOrEmpty(remembered))
                {
                    r.state = ProviderLinkState.NotEntered;
                    r.providerName = string.Empty;
                    r.sentence = "Standalone: no terrain provider is entered, so the road tool shapes the terrain "
                        + "itself. To change that, " + TerrainFeatureAvailability.WhatToConnect + ".";
                    return r;
                }
                r.state = ProviderLinkState.EnteredButBroken;
                r.providerName = remembered;
                r.sentence = "A terrain provider was entered and '" + remembered + "' is no longer in the scene. "
                    + "Terrain cuts, tunnel openings and building pads will not run until the reference is fixed; the "
                    + "tool is NOT falling back to standalone behind your back.";
                return r;
            }

            r.providerName = string.IsNullOrEmpty(entered.name) ? UnnamedProvider : entered.name;
            if (string.IsNullOrEmpty(r.providerPath)) { r.providerPath = r.providerName; }

            IPCGDataProvider provider = entered as IPCGDataProvider;
            if (provider == null)
            {
                r.state = ProviderLinkState.EnteredButBroken;
                r.sentence = "'" + r.providerName + "' was entered as the terrain provider but does not implement the "
                    + "provider contract, so nothing can be requested from it. Enter the component that owns the "
                    + "terrain.";
                return r;
            }

            if (r.surfaceMembers != r.understoodMembers)
            {
                r.state = ProviderLinkState.InterfaceVersionMismatch;
                r.sentence = "Provider found ('" + r.providerName + "'), its contract publishes "
                    + r.surfaceMembers.ToString(CultureInfo.InvariantCulture) + " members, this tool was written "
                    + "against " + r.understoodMembers.ToString(CultureInfo.InvariantCulture)
                    + ". No work is routed through a contract this tool only partly understands -- update the tool or "
                    + "the provider so the two agree.";
                return r;
            }

            r.probe = Probe(provider);
            if (!r.probe.answered)
            {
                r.state = ProviderLinkState.EnteredButBroken;
                r.sentence = "'" + r.providerName + "' is entered and implements the contract but did not answer a "
                    + "height query: " + r.probe.note + ". This is normal for one editor tick during scene load and "
                    + "is retried once; if it persists, the provider is present but not ready.";
                return r;
            }

            r.state = ProviderLinkState.Connected;
            r.sentence = "Connected to '" + r.providerName + "': that component owns the terrain, and every change "
                + "the road tool needs is requested from it rather than written directly.";
            return r;
        }

        /// <summary> The trivial question. A height inside the provider's OWN declared bounds, because a height at an
        /// arbitrary point can be answered by a stub that returns zero, while a provider that declares where its
        /// ground is and then answers inside it has demonstrably resolved real data.
        ///
        /// A provider that declares no loaded region yet has not failed -- it is mid-load, which is precisely the case
        /// <see cref="ShouldRetry"/> exists for. The bounds are chosen by lowest centre rather than by list order so
        /// the same probe point is used every session (C-06). </summary>
        public static ProviderLinkProbe Probe(IPCGDataProvider provider)
        {
            ProviderLinkProbe p = new ProviderLinkProbe();
            if (provider == null) { p.note = "no provider to ask"; return p; }

            System.Collections.Generic.List<Bounds> regions;
            try { regions = provider.GetActiveChunkBounds(); }
            catch (System.Exception e) { p.note = "asking for its loaded regions threw " + e.GetType().Name; return p; }

            if (regions == null || regions.Count == 0)
            {
                p.note = "it declares no loaded terrain region yet, so there is nowhere inside its own bounds to ask";
                return p;
            }
            p.declaredRegions = regions.Count;

            int chosen = 0;
            for (int i = 1; i < regions.Count; i++)
            {
                Vector3 c = regions[i].center;
                Vector3 best = regions[chosen].center;
                if (c.x < best.x || (c.x == best.x && c.z < best.z)) { chosen = i; }
            }

            Vector3 at = regions[chosen].center;
            float height;
            try { height = provider.SampleHeight(at.x, at.z); }
            catch (System.Exception e)
            {
                p.note = "sampling a height inside its own bounds threw " + e.GetType().Name;
                return p;
            }

            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                p.note = "it answered with a height that is not a number";
                return p;
            }

            p.answered = true;
            p.height = height;
            p.note = "answered " + height.ToString("0.###", CultureInfo.InvariantCulture) + " m inside its own bounds";
            return p;
        }

        /// <summary> Whether this failure is the one worth one more attempt on the next editor tick. Only the probe
        /// failure qualifies: a reference that is gone, an object that is not a provider and a contract this tool does
        /// not understand will all fail identically a tick later, and retrying them just delays the message. A probe
        /// that failed because nothing is loaded yet is the one case where a tick changes the answer. </summary>
        public static bool ShouldRetry(ProviderLinkReport report)
        {
            return report.state == ProviderLinkState.EnteredButBroken
                && !string.IsNullOrEmpty(report.probe.note);
        }

        /// <summary> What disconnecting says and what it does, in one place so the panel, the marker and the gate all
        /// quote the same sentence. The ORDER of release is the gateway's, not this file's: it unwinds its holds in
        /// reverse acquisition order, so a pad taken after a cut region is given back first. Restating that order here
        /// would be a second implementation of it, and two of them is how one gets missed. </summary>
        public const string DisconnectSentence =
            "Disconnected: everything the road tool was holding -- building pads, cut regions, tunnel openings -- has "
            + "been given back in reverse order of acquisition, the terrain is in its natural state, and the tool now "
            + "shapes it directly.";
    }
}

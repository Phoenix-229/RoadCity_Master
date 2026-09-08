// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-52's data core: the persisted result of the last build, and the outstanding refusals it produced.
    /// Every § 10 task that chooses "report rather than emit something wrong" needs somewhere for that report to
    /// land, and a console line is not it -- console output scrolls and is gone next session. This model is that
    /// place: it survives a scene reload (it is [Serializable], stored on the road system), it is STAMPED with the
    /// time and seed of the build that produced it so a stale report is never shown as current, and it is never empty
    /// (the mode line always answers "connected or standalone"). The GUI panel and the jump-to-object selection are
    /// the editor half; this is the invariant-bearing data behind them. </summary>

    /// <summary> Whether a terrain provider is connected. Connected: the road tool can cut terrain, request tunnel
    /// cavities and building pads. Standalone: it terraforms directly and cannot cut a bore -- the difference § 10.24
    /// exists to make visible so standalone behaviour is not read as a bug. </summary>
    public enum TerrainProviderMode { Standalone, Connected }

    /// <summary> The outcome of the last build. None: nothing built yet. </summary>
    public enum BuildOutcome { None, Success, RefusedSome, Failed }

    /// <summary> One refusal: a task declined to emit something wrong, naming the object and the station (metres along
    /// the road) it came from, so the panel can list it and select the object. </summary>
    [Serializable]
    public struct RefusalEntry
    {
        public string objectId;    // stable id used to select the object in the scene
        public string objectName;  // human label shown in the list
        public double stationMetres; // where along the road it occurred
        public string reason;      // why it was refused, in the author's words

        public RefusalEntry(string objectId, string objectName, double stationMetres, string reason)
        {
            this.objectId = objectId; this.objectName = objectName;
            this.stationMetres = stationMetres; this.reason = reason;
        }
    }

    /// <summary> The persisted status the panel reads. Serialisable so it round-trips with the scene. </summary>
    [Serializable]
    public class BuildStatus
    {
        public TerrainProviderMode mode = TerrainProviderMode.Standalone;
        public string providerName = "";           // set only when connected
        public BuildOutcome lastOutcome = BuildOutcome.None;
        public long lastBuildUtcTicks = 0;          // 0 => never built
        public ulong lastBuildSeed = 0;
        public List<RefusalEntry> refusals = new List<RefusalEntry>();

        /// <summary> The first line: the mode, the provider when connected, and what is unavailable when not. Never
        /// empty -- this is what guarantees the panel always says something.
        ///
        /// What it names as unavailable is READ from the availability table rather than typed here (§ 10.24, RM-58).
        /// The hand-written version of this line claimed terrain cuts were unavailable standalone, which was wrong --
        /// standalone cuts terrain perfectly well, it just cannot hold the cut against a regenerating provider. That is
        /// exactly the drift a second copy of the list produces, so there is now only one copy. </summary>
        public string ModeLine()
        {
            if (mode == TerrainProviderMode.Connected)
            {
                string name = string.IsNullOrEmpty(providerName) ? "an unnamed provider" : providerName;
                return "Connected to " + name + ": that component owns the terrain, every change is requested from it, "
                     + "and every feature in the availability table is available.";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Standalone (no provider connected): the road tool shapes the terrain itself, exactly as it does "
                + "on its own.");

            IList<City.TerrainFeatureRow> missing =
                City.TerrainFeatureAvailability.UnavailableIn(TerrainProviderMode.Standalone);
            if (missing.Count > 0)
            {
                sb.Append(" These are unavailable: ");
                for (int i = 0; i < missing.Count; i++)
                {
                    if (i > 0) { sb.Append(i + 1 == missing.Count ? " and " : ", "); }
                    sb.Append(LowerFirst(missing[i].title));
                }
                sb.Append(". To change that, ").Append(City.TerrainFeatureAvailability.WhatToConnect).Append('.');
            }
            return sb.ToString();
        }

        /// <summary> A table title reads as a heading ("Tunnel cavity kept open"); mid-sentence it has to read as
        /// prose. Only the first letter changes, so an id or a proper noun later in the title survives. </summary>
        private static string LowerFirst(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        /// <summary> The last-build line, always stamped so a stale result is never shown as current. </summary>
        public string LastBuildLine()
        {
            if (lastOutcome == BuildOutcome.None || lastBuildUtcTicks == 0)
            {
                return "No build recorded yet.";
            }
            string when = new DateTime(lastBuildUtcTicks, DateTimeKind.Utc)
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            string outcome;
            switch (lastOutcome)
            {
                case BuildOutcome.Success: outcome = "succeeded"; break;
                case BuildOutcome.RefusedSome: outcome = "succeeded with " + refusals.Count + " refusal(s)"; break;
                default: outcome = "failed"; break;
            }
            return "Last build " + outcome + " at " + when
                 + " (seed " + lastBuildSeed.ToString(CultureInfo.InvariantCulture) + ").";
        }

        /// <summary> A stamp exists iff a build has been recorded. The panel uses this to refuse showing a stale result
        /// as current. </summary>
        public bool HasStamp() { return lastBuildUtcTicks != 0 && lastOutcome != BuildOutcome.None; }

        /// <summary> The panel is never empty: at minimum it has the mode line. </summary>
        public bool IsEverEmpty() { return false; }

        /// <summary> Record a completed build, stamping it with the moment and seed. </summary>
        public void RecordBuild(DateTime utcNow, ulong seed, BuildOutcome outcome, IList<RefusalEntry> newRefusals)
        {
            lastBuildUtcTicks = utcNow.Ticks;
            lastBuildSeed = seed;
            refusals = new List<RefusalEntry>();
            if (newRefusals != null) { refusals.AddRange(newRefusals); }
            if (outcome == BuildOutcome.Success && refusals.Count > 0) { outcome = BuildOutcome.RefusedSome; }
            lastOutcome = outcome;
        }

        /// <summary> The full panel text: mode, last build, then every refusal with its object and station. </summary>
        public string RenderText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ModeLine()).Append('\n');
            sb.Append(LastBuildLine());
            for (int i = 0; i < refusals.Count; i++)
            {
                RefusalEntry r = refusals[i];
                sb.Append("\n  • ").Append(r.objectName)
                  .Append(" @ ").Append(r.stationMetres.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m: ")
                  .Append(r.reason);
            }
            return sb.ToString();
        }
    }
}

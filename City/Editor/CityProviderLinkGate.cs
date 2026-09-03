#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-73's gate: the linker's four states, its one retry, and the release path.
    ///
    /// The three failures are the point. A reference that was entered and no longer resolves must read as its own
    /// state, not as standalone -- otherwise the tool quietly stops cutting terrain and the author reads it as a bug in
    /// the road. A provider that resolves but publishes a contract this tool was not written against must read as its
    /// own state too, rather than as connected with a guessed subset of calls. And disconnecting must give back every
    /// pad, cut region and tunnel opening it held, because those are owned by the provider until released and a leaked
    /// one is invisible.
    ///
    /// It runs on real components in the open scene -- a linker cannot be tested without one, since the whole subject
    /// is a reference an author entered -- and destroys every object it made. Report lines are prefixed RCMLINK.
    /// </summary>
    public static class CityProviderLinkGate
    {
        [MenuItem("Window/Road City Master/Testing/Terrain provider link (acquire, verify, release)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityProviderLinkGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMLINK report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMLINK BEGIN\n");
            List<string> failures = new List<string>();

            CaseNothingEntered(builder, failures);
            CaseProviderReportedByName(builder, failures);
            CaseEnteredButNotAProvider(builder, failures);
            CaseProviderDeleted(builder, failures);
            CaseNotReadyIsRetriedOnce(builder, failures);
            CaseReadyOnTheRetry(builder, failures);
            CaseProviderThrowsIsReported(builder, failures);
            CaseContractFingerprint(builder, failures);
            CaseReleaseGivesEverythingBack(builder, failures);
            CaseEnteredReferenceOutranksTheScan(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMLINK FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMLINK result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMLINK END");

            report = builder.ToString();
            TerrainWriteGateway.Uninstall();
            return failures.Count == 0;
        }

        #region "Scene helpers -- a linker cannot be tested without a scene object, since a reference is the subject"
        private static TerrainLinker NewLinker(string name)
        {
            GameObject host = new GameObject(name);
            TerrainLinker linker = host.AddComponent<TerrainLinker>();
            // Acquire is called explicitly by each case, so the moment of the attempt is the case's, not the engine's.
            linker.acquireOnEnable = false;
            return linker;
        }

        private static FakeProviderComponent NewProvider(string name)
        {
            GameObject host = new GameObject(name);
            FakeProviderComponent provider = host.AddComponent<FakeProviderComponent>();

            // See the same guard in CityProviderSocketGate: AddComponent returns null instead of throwing when the
            // behaviour is an editor script, and a null provider handed to the linker stalls the suite silently.
            if (provider == null)
            {
                throw new System.InvalidOperationException(
                    "the fake provider could not be attached to " + name +
                    " -- FakeProviderComponent must be compiled into the runtime assembly, not an Editor folder");
            }
            return provider;
        }

        private static void Discard(Component first, Component second)
        {
            if (first != null) { Object.DestroyImmediate(first.gameObject); }
            if (second != null) { Object.DestroyImmediate(second.gameObject); }
        }
        #endregion


        /// <summary> Nothing entered is not a failure: it is standalone, stated, with the one instruction that changes
        /// it. </summary>
        private static void CaseNothingEntered(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link empty");
            try
            {
                bool connected = linker.Acquire();
                ProviderLinkReport r = linker.Report;
                Expect(!connected, "an empty linker reported a connection", failures);
                Expect(r.state == ProviderLinkState.NotEntered,
                    "an empty linker is not NotEntered but " + r.state.ToString(), failures);
                Expect(!r.LinkerEntered, "an empty linker claims a reference was entered", failures);
                Expect(r.Mode == TerrainProviderMode.Standalone, "an empty linker is not standalone", failures);
                Expect(r.sentence.Contains(TerrainFeatureAvailability.WhatToConnect),
                    "the standalone sentence does not say what to connect", failures);
                Expect(TerrainWriteGateway.Active != null
                    && TerrainWriteGateway.Active.Mode == TerrainProviderMode.Standalone,
                    "an empty linker did not install a standalone gateway", failures);
                builder.Append("RCMLINK nothing entered  ").Append(r.state.ToString()).Append('\n');
            }
            finally { Discard(linker, null); }
        }

        /// <summary> The connected case, and the two things it must report: the provider's OWN name, and a probe that
        /// was answered inside the provider's own declared bounds -- at the lowest-centre region rather than the first
        /// one in the list, so the probe point is the same every session (C-06). </summary>
        private static void CaseProviderReportedByName(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link connected");
            FakeProviderComponent provider = NewProvider("rcm provider ready");
            try
            {
                linker.enteredProvider = provider;
                bool connected = linker.Acquire();
                ProviderLinkReport r = linker.Report;
                Expect(connected && r.state == ProviderLinkState.Connected,
                    "an entered, ready provider did not connect: " + r.state.ToString(), failures);
                Expect(r.ProviderResolved && r.LinkerEntered,
                    "a connected link does not report both entered and resolved", failures);
                Expect(r.providerName == "rcm provider ready",
                    "the provider was not reported by its own name but as '" + r.providerName + "'", failures);
                Expect(r.probe.answered && r.probe.declaredRegions == 2,
                    "the probe did not answer inside the provider's two declared regions", failures);
                Expect(provider.lastSampleX == 0f && provider.lastSampleZ == 0f,
                    "the probe did not choose the lowest-centre region (asked at x "
                    + provider.lastSampleX.ToString("0.#", CultureInfo.InvariantCulture) + ")", failures);
                Expect(TerrainWriteGateway.Active.Mode == TerrainProviderMode.Connected
                    && TerrainWriteGateway.Active.ProviderName == r.providerPath,
                    "the gateway was not installed for the entered provider", failures);
                builder.Append("RCMLINK connected  ").Append(r.providerName)
                       .Append("  probe ").Append(r.probe.note).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> Something entered that is not a provider at all. Broken, said in words, never connected.
        /// </summary>
        private static void CaseEnteredButNotAProvider(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link wrong type");
            TerrainLinker notAProvider = NewLinker("rcm not a provider");
            try
            {
                linker.enteredProvider = notAProvider;
                bool connected = linker.Acquire();
                ProviderLinkReport r = linker.Report;
                Expect(!connected && r.state == ProviderLinkState.EnteredButBroken,
                    "a non-provider was accepted: " + r.state.ToString(), failures);
                Expect(r.LinkerEntered && !r.ProviderResolved,
                    "a non-provider does not read as entered-but-unresolved", failures);
                Expect(r.sentence.Contains("does not implement"),
                    "the refusal does not say the entered object is not a provider", failures);
                builder.Append("RCMLINK wrong type  ").Append(r.state.ToString()).Append('\n');
            }
            finally { Discard(linker, notAProvider); }
        }

        /// <summary> The state § 10.32 exists for: a reference that WAS good and whose provider is now gone. It must
        /// name the missing provider and must not degrade into a plain standalone sentence. </summary>
        private static void CaseProviderDeleted(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link deleted");
            FakeProviderComponent provider = NewProvider("rcm provider vanishing");
            try
            {
                linker.enteredProvider = provider;
                bool first = linker.Acquire();
                Object.DestroyImmediate(provider.gameObject);
                provider = null;

                bool second = linker.Acquire();
                ProviderLinkReport r = linker.Report;
                Expect(first, "the provider did not connect before being deleted", failures);
                Expect(!second && r.state == ProviderLinkState.EnteredButBroken,
                    "a deleted provider did not report EnteredButBroken but " + r.state.ToString(), failures);
                Expect(r.LinkerEntered, "a deleted provider read as nothing ever entered", failures);
                Expect(r.providerName == "rcm provider vanishing"
                    && r.sentence.Contains("rcm provider vanishing"),
                    "the broken-link sentence does not name the provider that went missing", failures);
                Expect(r.sentence.Contains("NOT falling back"),
                    "the broken-link sentence does not say the tool is not silently standalone", failures);
                builder.Append("RCMLINK deleted  ").Append(r.state.ToString()).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> A provider that is present but has loaded nothing yet. This is the ONLY failure worth a second
        /// attempt, and it gets exactly one: a genuinely dead provider must be reported, not retried forever.
        /// </summary>
        private static void CaseNotReadyIsRetriedOnce(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link not ready");
            FakeProviderComponent provider = NewProvider("rcm provider loading");
            try
            {
                provider.regionsLoaded = false;
                linker.enteredProvider = provider;
                bool connected = linker.Acquire();
                ProviderLinkReport r = linker.Report;

                Expect(!connected && r.state == ProviderLinkState.EnteredButBroken,
                    "a provider with nothing loaded was accepted: " + r.state.ToString(), failures);
                Expect(ProviderLinkContract.ShouldRetry(r), "a not-ready provider was not marked worth retrying",
                    failures);
                Expect(linker.RetryPending, "the one retry was not armed", failures);

                bool firstTick = linker.TickRetry();
                Expect(firstTick && linker.RetriesUsed == 1,
                    "the retry did not run exactly once", failures);
                Expect(linker.Report.state == ProviderLinkState.EnteredButBroken,
                    "a still-not-ready provider connected on the retry", failures);
                Expect(!linker.RetryPending && !linker.TickRetry(),
                    "the retry re-armed itself, so a dead provider would be retried forever", failures);
                builder.Append("RCMLINK not ready  retries ")
                       .Append(linker.RetriesUsed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> The reason the retry exists at all: a provider that finishes loading one tick later connects,
        /// rather than being reported broken for the rest of the session. </summary>
        private static void CaseReadyOnTheRetry(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link late");
            FakeProviderComponent provider = NewProvider("rcm provider late");
            try
            {
                provider.regionsLoaded = false;
                linker.enteredProvider = provider;
                linker.Acquire();
                Expect(linker.RetryPending, "the late provider did not arm the retry", failures);

                provider.regionsLoaded = true;
                linker.TickRetry();
                Expect(linker.Report.state == ProviderLinkState.Connected,
                    "a provider ready on the retry did not connect: " + linker.Report.state.ToString(), failures);
                Expect(TerrainWriteGateway.Active.Mode == TerrainProviderMode.Connected,
                    "the retry connected the report but not the gateway", failures);
                builder.Append("RCMLINK ready on retry  ").Append(linker.Report.probe.note).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> A provider that throws, and a provider that answers with a non-number. Both are reported; neither
        /// escapes as an exception, because an exception during scene load is the version of this failure that costs an
        /// afternoon to diagnose. </summary>
        private static void CaseProviderThrowsIsReported(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link throwing");
            FakeProviderComponent provider = NewProvider("rcm provider throwing");
            try
            {
                provider.throwOnSample = true;
                linker.enteredProvider = provider;
                try
                {
                    linker.Acquire();
                    Expect(linker.Report.state == ProviderLinkState.EnteredButBroken,
                        "a throwing provider was accepted", failures);
                    Expect(linker.Report.probe.note.Contains("InvalidOperationException"),
                        "the report does not name what the provider threw", failures);
                }
                catch (System.Exception e)
                {
                    Expect(false, "the provider's exception escaped acquire as " + e.GetType().Name, failures);
                }

                provider.throwOnSample = false;
                provider.answerNotANumber = true;
                linker.Acquire();
                Expect(linker.Report.state == ProviderLinkState.EnteredButBroken
                    && linker.Report.probe.note.Contains("not a number"),
                    "a height that is not a number was treated as an answer", failures);
                builder.Append("RCMLINK throwing  ").Append(linker.Report.probe.note).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> The contract fingerprint. This tool was written against a surface of a known size, and it compares
        /// that number against the surface it is actually compiled against rather than trusting a version string the
        /// provider volunteers. If this case fails, the provider contract has GROWN or SHRUNK and the failure is the
        /// intended outcome: the tool must be taught the new member instead of routing work through a contract it half
        /// understands. </summary>
        private static void CaseContractFingerprint(StringBuilder builder, List<string> failures)
        {
            int surface = ProviderLinkContract.SurfaceMemberCount();
            Expect(surface == ProviderLinkContract.UnderstoodMemberCount,
                "the provider contract publishes " + surface.ToString(CultureInfo.InvariantCulture)
                + " members and this tool understands "
                + ProviderLinkContract.UnderstoodMemberCount.ToString(CultureInfo.InvariantCulture)
                + ": teach the linker the new member, then bump UnderstoodMemberCount", failures);

            // A mismatch is its own state, and the thing that matters about it is that it is neither of the other two:
            // entered (so RM-53 does not draw standalone) and unresolved (so no work is routed).
            ProviderLinkReport mismatch = new ProviderLinkReport();
            mismatch.state = ProviderLinkState.InterfaceVersionMismatch;
            Expect(mismatch.LinkerEntered && !mismatch.ProviderResolved
                && mismatch.Mode == TerrainProviderMode.Standalone,
                "a version mismatch does not read as entered, unresolved and standalone-in-behaviour", failures);
            Expect(!ProviderLinkContract.ShouldRetry(mismatch),
                "a version mismatch is retried, which cannot change the answer", failures);
            builder.Append("RCMLINK contract  surface ").Append(surface.ToString(CultureInfo.InvariantCulture))
                   .Append(" understood ")
                   .Append(ProviderLinkContract.UnderstoodMemberCount.ToString(CultureInfo.InvariantCulture))
                   .Append('\n');
        }

        /// <summary> The half implementers omit. Everything requested through the gateway is owned by the provider
        /// until released, so disconnecting must give back the pad, the cut region and the tunnel opening, and the link
        /// must be able to say what it holds while it holds them. </summary>
        private static void CaseReleaseGivesEverythingBack(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link release");
            FakeProviderComponent provider = NewProvider("rcm provider release");
            try
            {
                linker.enteredProvider = provider;
                linker.Acquire();

                TerrainWriteGateway gateway = TerrainWriteGateway.Active;
                TerrainRegion pad = TerrainRegion.FromFootprint(20.0, 20.0, 8.0, 6.0, 0.0, 5.0);
                TerrainRegion cut = TerrainRegion.FromFootprint(60.0, 20.0, 30.0, 5.0, 0.0, 5.0);
                TerrainRegion bore = TerrainRegion.FromFootprint(140.0, 20.0, 6.0, 6.0, -8.0, 4.0);
                string refusal;
                gateway.RequestBuildingPad(pad, "rcm-link-gate.pad", out refusal);
                gateway.RequestCutRegion(cut, "rcm-link-gate.cut", out refusal);
                gateway.RequestTunnelOpening(bore, "rcm-link-gate.bore", out refusal);

                IList<string> held = linker.HeldDescriptions();
                Expect(held.Count == 3, "the link cannot enumerate what it holds (saw "
                    + held.Count.ToString(CultureInfo.InvariantCulture) + " of 3)", failures);

                int released = linker.Release();
                Expect(released == 3, "disconnecting released "
                    + released.ToString(CultureInfo.InvariantCulture) + " holds, not 3", failures);
                Expect(linker.ReleasedOnLastDisconnect == 3, "the release count was not reported", failures);
                Expect(TerrainWriteGateway.Active.Mode == TerrainProviderMode.Standalone,
                    "disconnecting did not return the tool to standalone", failures);
                Expect(TerrainWriteGateway.Active.OutstandingCount == 0,
                    "the tool is standalone and still holding provider-owned ground", failures);
                Expect(linker.Report.sentence == ProviderLinkContract.DisconnectSentence,
                    "disconnecting did not report the one disconnect sentence", failures);
                builder.Append("RCMLINK release  held ")
                       .Append(held.Count.ToString(CultureInfo.InvariantCulture)).Append(" released ")
                       .Append(released.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        /// <summary> RM-73's design decision, as a test: a STATED answer is never overruled by a scanned one. A linker
        /// in the scene with nothing entered means the author has taken the decision, so the tool stays standalone even
        /// though a provider is sitting right there -- and entering that provider connects. </summary>
        private static void CaseEnteredReferenceOutranksTheScan(StringBuilder builder, List<string> failures)
        {
            TerrainLinker linker = NewLinker("rcm link precedence");
            FakeProviderComponent provider = NewProvider("rcm provider unclaimed");
            try
            {
                bool scanned = TerrainGatewayBootstrap.Refresh();
                Expect(!scanned, "a scan overruled an empty linker and connected anyway", failures);
                Expect(TerrainGatewayBootstrap.LastDecision == linker.Report.sentence,
                    "the decision reported was the scan's, not the linker's", failures);

                linker.enteredProvider = provider;
                bool stated = TerrainGatewayBootstrap.Refresh();
                Expect(stated && TerrainWriteGateway.Active.Mode == TerrainProviderMode.Connected,
                    "entering the provider did not connect through the bootstrap", failures);
                Expect(TerrainGatewayBootstrap.LastDecision.Contains("rcm provider unclaimed"),
                    "the connected decision does not name the entered provider", failures);
                builder.Append("RCMLINK precedence  ").Append(linker.Report.state.ToString()).Append('\n');
            }
            finally { Discard(linker, provider); }
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }


    }
}
#endif

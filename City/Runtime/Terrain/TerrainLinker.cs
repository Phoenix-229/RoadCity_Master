#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-73: the road tool's side of the handshake. One component, four states, and a release path that
    /// actually runs.
    ///
    /// An author enters the component that owns the terrain here (or enters THIS component into the terrain owner's
    /// socket -- the reference is one-directional either way, and this is the end that verifies). Acquire, verify, use,
    /// release is the whole lifecycle, and the half implementers omit is the last one: everything the tool requested
    /// through RM-57's gateway is owned by the provider until released, so disconnecting, deleting a road or unloading
    /// the tool must give it all back or the scene keeps a pad and a tunnel hole nobody can find.
    ///
    /// What it deliberately does not do is search. <see cref="ProviderLinkContract"/> carries that argument; the short
    /// version is that a scan has no answer with two candidates and a wrong answer with a leftover test object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainLinker : MonoBehaviour, UniverseCreator.Integration.IPathToolLink
    {
        [Tooltip("The component that owns the terrain. Entered by hand: this tool never guesses which object in the "
               + "scene is the terrain provider.")]
        public MonoBehaviour enteredProvider;

        [Tooltip("Acquire on enable. Off leaves the tool standalone until Acquire is called, for a scene that wants "
               + "the connection made at a moment of its own choosing.")]
        public bool acquireOnEnable = true;

        /// <summary> The provider name from the last successful acquire, serialised WITH the scene. Without it a
        /// reference that has since been deleted is indistinguishable from a field nobody ever filled in, and those
        /// two need different sentences (RM-53's third state versus plain standalone). </summary>
        [HideInInspector]
        public string rememberedProviderName = string.Empty;

        private ProviderLinkReport report;
        private bool retryPending;
        private int retriesUsed;
        private int releasedOnLastDisconnect;

        /// <summary> What the last acquire concluded. RM-52's panel and RM-53's marker both read this rather than
        /// re-verifying. </summary>
        public ProviderLinkReport Report { get { return report; } }

        /// <summary> How many holds were still outstanding the last time this link released. Non-zero is not an error
        /// -- it is the count that proves the release path ran and had something to give back. </summary>
        public int ReleasedOnLastDisconnect { get { return releasedOnLastDisconnect; } }

        /// <summary> Whether one retry is still armed. Reported so a gate can prove the retry is used once and not in
        /// a loop. </summary>
        public bool RetryPending { get { return retryPending; } }

        public int RetriesUsed { get { return retriesUsed; } }

        private void OnEnable()
        {
            if (acquireOnEnable) { Acquire(); }
        }

        private void OnDisable()
        {
            Release();
        }

        /// <summary> Verify what was entered and install the result. Returns true only when work may actually be
        /// routed through a provider. Never throws: every failure becomes a state and a sentence, because an exception
        /// during scene load is the version of this failure that costs an afternoon to diagnose. </summary>
        public bool Acquire()
        {
            retryPending = false;
            report = ProviderLinkContract.Verify(enteredProvider, PathOf(enteredProvider), rememberedProviderName,
                HeldCount());

            if (report.state == ProviderLinkState.Connected)
            {
                rememberedProviderName = report.providerName;
                TerrainWriteGateway.Install(TerrainWriteGateway.ForProvider(
                    enteredProvider as UniverseCreator.Integration.IPCGDataProvider, report.providerPath));
                return true;
            }

            // Every failure behaves as standalone -- the tool still has to be able to build -- while being REPORTED as
            // its own state. That split is the point of RM-73: silent fallback is what makes a broken linker read as a
            // bug in the road instead of a bug in the wiring.
            TerrainWriteGateway.Install(TerrainWriteGateway.ForStandalone());
            if (ProviderLinkContract.ShouldRetry(report) && retriesUsed == 0) { retryPending = true; }
            return false;
        }

        /// <summary> The one retry, on the next tick. A provider present but not ready is a real state during scene
        /// load; a second attempt a tick later is the cheapest way to tell that apart from a provider that is broken,
        /// and it is capped at one so a genuinely dead provider is reported rather than retried forever. </summary>
        private void Update()
        {
            TickRetry();
        }

        /// <summary> The retry, driven by <see cref="Update"/> at runtime and called directly by RM-73's gate, which
        /// runs in the editor where Update does not tick. One code path either way -- a test-only copy of the retry
        /// would be testing itself. Returns true if an attempt was made. </summary>
        public bool TickRetry()
        {
            if (!retryPending) { return false; }
            retryPending = false;
            retriesUsed++;
            Acquire();
            return true;
        }

        /// <summary> Give everything back and return to standalone. Returns how many holds were released. The ORDER is
        /// the gateway's -- reverse acquisition -- and is not restated here; see
        /// <see cref="ProviderLinkContract.DisconnectSentence"/>. </summary>
        public int Release()
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            releasedOnLastDisconnect = gateway != null ? gateway.ReleaseAll() : 0;
            retryPending = false;

            TerrainWriteGateway.Install(TerrainWriteGateway.ForStandalone());
            report = new ProviderLinkReport();
            report.state = ProviderLinkState.NotEntered;
            report.providerName = string.Empty;
            report.providerPath = string.Empty;
            report.understoodMembers = ProviderLinkContract.UnderstoodMemberCount;
            report.surfaceMembers = ProviderLinkContract.SurfaceMemberCount();
            report.sentence = ProviderLinkContract.DisconnectSentence;
            return releasedOnLastDisconnect;
        }

        /// <summary> What the link is currently holding, in words. Delegated to the gateway, which owns the list:
        /// keeping a second copy here is how the two drift. RM-52 shows this and RM-72 asserts on it, which is why it
        /// is visible state rather than private bookkeeping. </summary>
        public IList<string> HeldDescriptions()
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            return gateway != null ? gateway.DescribeOutstanding() : new List<string>();
        }

        private static int HeldCount()
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            return gateway != null ? gateway.OutstandingCount : 0;
        }

        /// <summary> The entered object's hierarchy path, or empty when nothing is entered. Stable across sessions,
        /// unlike an instance id, and it is what tells two providers of the same name apart. </summary>
        private static string PathOf(MonoBehaviour component)
        {
            if (component == null) { return string.Empty; }
            return TerrainGatewayBootstrap.PathOf(component.transform);
        }

        #region "The socket's side of the same reference (UC-46)"
        // The world side has a slot this component is entered into, and what it displays back is read from here rather
        // than guessed. Everything below is already computed for this link's own report; publishing it costs nothing and
        // is what makes the integration debuggable from whichever end the author is standing at.

        /// <summary> This tool's own name, handed over as DATA so the world side never hard-codes another product's
        /// name. It is also why a second path-authoring tool can occupy the same socket unchanged. </summary>
        public string PathToolName { get { return "Road & City Master"; } }

        /// <summary> The provider surface this tool was written against, so the world side can compare it with the
        /// surface it actually publishes and report drift instead of half-using a changed contract. </summary>
        public int UnderstoodProviderMemberCount { get { return ProviderLinkContract.UnderstoodMemberCount; } }

        /// <summary> Whether work is actually being routed. Entered and connected are two different facts, and the
        /// socket shows both. </summary>
        public bool IsConnectedToProvider { get { return report.state == ProviderLinkState.Connected; } }

        /// <summary> Pads, cut regions and tunnel openings outstanding right now -- the gateway's count, not a second
        /// tally kept here. </summary>
        public int HeldRegionCount { get { return HeldCount(); } }

        /// <summary> The sentence from the last acquire, verbatim. This link knows why it is in the state it is in;
        /// letting the other side paraphrase would produce a second, worse explanation. </summary>
        public string StatusSentence { get { return string.IsNullOrEmpty(report.sentence) ? "not yet acquired"
            : report.sentence; } }

        /// <summary> Give everything back, on the world side's asking. The same release path <see cref="Release"/>
        /// runs -- clearing the socket and disconnecting the link are the same event seen from two ends, and two
        /// implementations of it is how one of them forgets the tunnel openings. </summary>
        public int ReleaseAllHeldRegions()
        {
            return Release();
        }
        #endregion
    }
}

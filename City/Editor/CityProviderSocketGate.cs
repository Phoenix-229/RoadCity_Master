#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> UC-46's gate: the socket on the world side, seen from the tool that plugs into it.
    ///
    /// The socket is the other end of RM-73's reference and it has to answer three questions an author will actually
    /// ask: was the thing I entered accepted, what does it call itself, and how much of my terrain is it holding right
    /// now. It also has to answer them WITHOUT naming a product in its own source -- the name arrives as data from the
    /// link -- which is why one case asserts on the wording of the empty-socket sentence rather than only on its state.
    ///
    /// The fourth thing it must do is the one that corrupts scenes when omitted: clearing the socket asks for every held
    /// region back BEFORE the reference is dropped. A reference dropped first is a pad nobody can find.
    ///
    /// Report lines are prefixed RCMSOCKET. </summary>
    public static class CityProviderSocketGate
    {
        [MenuItem("Window/Road City Master/Testing/Provider socket (the world side of the link)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityProviderSocketGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSOCKET BEGIN\n");
            List<string> failures = new List<string>();

            CaseEmptySocketSaysNothingIsConnected(builder, failures);
            CaseSurfaceCountsAgreeAcrossTheTwoProducts(builder, failures);
            CaseEnteredLinkIsAcceptedAndNamesItself(builder, failures);
            CaseSomethingThatIsNotALink(builder, failures);
            CaseHeldRegionsAreVisibleThroughTheSocket(builder, failures);
            CaseClearReleasesBeforeItEmpties(builder, failures);
            CaseVanishedLinkIsNamedNotForgotten(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSOCKET FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSOCKET result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSOCKET END");

            report = builder.ToString();
            TerrainWriteGateway.Uninstall();
            return failures.Count == 0;
        }

        #region "Scene helpers"
        private static PathProviderSocket NewSocket(string name)
        {
            GameObject host = new GameObject(name);
            return host.AddComponent<PathProviderSocket>();
        }

        private static TerrainLinker NewLinker(string name)
        {
            GameObject host = new GameObject(name);
            TerrainLinker linker = host.AddComponent<TerrainLinker>();
            linker.acquireOnEnable = false;
            return linker;
        }

        private static FakeProviderComponent NewProvider(string name)
        {
            GameObject host = new GameObject(name);
            FakeProviderComponent provider = host.AddComponent<FakeProviderComponent>();

            // AddComponent RETURNS NULL rather than throwing when the behaviour cannot be attached -- Unity refuses any
            // MonoBehaviour compiled into an editor assembly, logs "Can't add script behaviour", and carries on. Handing
            // that null to the linker cost this suite a twenty-minute stall with nothing in the log to say why, so the
            // fixture now fails where it broke and names the cause. The fake lives in City/Runtime for this reason.
            if (provider == null)
            {
                throw new System.InvalidOperationException(
                    "the fake provider could not be attached to " + name +
                    " -- FakeProviderComponent must be compiled into the runtime assembly, not an Editor folder");
            }
            return provider;
        }

        /// <summary> A socket, a linker and a provider, wired and acquired: the ordinary working arrangement three of
        /// these cases start from. </summary>
        private static void Connect(out PathProviderSocket socket, out TerrainLinker linker,
            out FakeProviderComponent provider, string label)
        {
            socket = NewSocket("rcm socket " + label);
            linker = NewLinker("rcm link " + label);
            provider = NewProvider("rcm provider " + label);
            linker.enteredProvider = provider;
            linker.Acquire();
            socket.enteredPathTool = linker;
        }

        private static void Discard(Component a, Component b, Component c)
        {
            if (a != null) { Object.DestroyImmediate(a.gameObject); }
            if (b != null) { Object.DestroyImmediate(b.gameObject); }
            if (c != null) { Object.DestroyImmediate(c.gameObject); }
        }
        #endregion


        /// <summary> An empty socket is not a failure and must not read like one. It also must not name a product: the
        /// world side never hard-codes who plugs into it, which is what lets a different path tool occupy the same slot.
        /// </summary>
        private static void CaseEmptySocketSaysNothingIsConnected(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket = NewSocket("rcm socket empty");
            try
            {
                PathLinkStatus s = socket.Status();
                Expect(s.state == PathLinkState.Empty, "an empty socket is not Empty but " + s.state.ToString(),
                    failures);
                Expect(!s.SocketFilled && !s.Working, "an empty socket claims to be filled or working", failures);
                Expect(s.heldRegions == 0, "an empty socket reports holding ground", failures);
                Expect(!s.sentence.Contains("Road") && !s.sentence.Contains("City Master"),
                    "the empty-socket sentence names a product, which the world side must never do", failures);
                Expect(s.sentence.Contains("behaves exactly as"),
                    "the empty-socket sentence does not say the world is unchanged with nothing entered", failures);

                // Purity: reading the socket must not decide or install anything, so two reads agree and neither
                // touches the tool's gateway.
                PathLinkStatus again = socket.Status();
                Expect(again.state == s.state && again.sentence == s.sentence,
                    "reading the socket twice gave two answers", failures);
                builder.Append("RCMSOCKET empty  ").Append(s.state.ToString()).Append('\n');
            }
            finally { Discard(socket, null, null); }
        }

        /// <summary> The fingerprint, checked from both ends. If this fails, one product's idea of the provider surface
        /// has moved and the socket will refuse links until the two agree -- which is the intended outcome, not a bug in
        /// this gate. </summary>
        private static void CaseSurfaceCountsAgreeAcrossTheTwoProducts(StringBuilder builder, List<string> failures)
        {
            int published = PathProviderSocket.PublishedProviderMemberCount;
            int understood = ProviderLinkContract.UnderstoodMemberCount;
            Expect(published == understood,
                "the world publishes " + published.ToString(CultureInfo.InvariantCulture)
                + " provider members and this tool understands " + understood.ToString(CultureInfo.InvariantCulture)
                + ": teach the tool the new member, then bump UnderstoodMemberCount", failures);
            builder.Append("RCMSOCKET surface  published ").Append(published.ToString(CultureInfo.InvariantCulture))
                   .Append(" understood ").Append(understood.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        /// <summary> The working case: entered, accepted, and reporting the tool's OWN name rather than the name of the
        /// GameObject it happens to sit on. </summary>
        private static void CaseEnteredLinkIsAcceptedAndNamesItself(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket;
            TerrainLinker linker;
            FakeProviderComponent provider;
            Connect(out socket, out linker, out provider, "accepted");
            try
            {
                PathLinkStatus s = socket.Status();
                Expect(s.state == PathLinkState.Accepted,
                    "an entered link was not accepted but read as " + s.state.ToString(), failures);
                Expect(s.Working && s.toolReportsConnected,
                    "an accepted link connected to a provider does not read as working", failures);
                Expect(s.toolName == linker.PathToolName && s.toolName != linker.gameObject.name,
                    "the socket reported '" + s.toolName + "' rather than the name the tool gives itself", failures);
                Expect(s.sentence.Contains(linker.StatusSentence),
                    "the socket paraphrased the tool instead of quoting what it reported", failures);
                Expect(s.understoodMembers == s.publishedMembers,
                    "an accepted link disagrees with the published provider surface", failures);
                builder.Append("RCMSOCKET accepted  ").Append(s.toolName).Append('\n');
            }
            finally { Discard(socket, linker, provider); }
        }

        /// <summary> A provider is not a link. Entering the wrong kind of component is reported, never ignored: an
        /// ignored mistake looks identical to an empty socket and the author has no way to tell. </summary>
        private static void CaseSomethingThatIsNotALink(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket = NewSocket("rcm socket wrong");
            FakeProviderComponent provider = NewProvider("rcm provider miscast");
            try
            {
                socket.enteredPathTool = provider;
                PathLinkStatus s = socket.Status();
                Expect(s.state == PathLinkState.NotALink,
                    "a non-link was accepted into the socket as " + s.state.ToString(), failures);
                Expect(s.SocketFilled && !s.Working,
                    "a non-link does not read as filled-but-not-working", failures);
                Expect(s.sentence.Contains("does not implement"),
                    "the refusal does not say what is wrong with what was entered", failures);
                builder.Append("RCMSOCKET not a link  ").Append(s.state.ToString()).Append('\n');
            }
            finally { Discard(socket, provider, null); }
        }

        /// <summary> What the socket is for, beyond reporting a connection: an author standing on the world side can see
        /// how much of their terrain the other tool is holding open, without opening the other tool's inspector.
        /// </summary>
        private static void CaseHeldRegionsAreVisibleThroughTheSocket(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket;
            TerrainLinker linker;
            FakeProviderComponent provider;
            Connect(out socket, out linker, out provider, "holding");
            try
            {
                Expect(socket.Status().heldRegions == 0, "a fresh link is already holding ground", failures);

                TerrainWriteGateway gateway = TerrainWriteGateway.Active;
                string refusal;
                gateway.RequestBuildingPad(TerrainRegion.FromFootprint(10.0, 10.0, 9.0, 7.0, 0.0, 4.0),
                    "rcm-socket-gate.pad", out refusal);
                gateway.RequestTunnelOpening(TerrainRegion.FromFootprint(90.0, 10.0, 6.0, 6.0, -9.0, 3.0),
                    "rcm-socket-gate.bore", out refusal);

                PathLinkStatus s = socket.Status();
                Expect(s.heldRegions == 2, "the socket reports "
                    + s.heldRegions.ToString(CultureInfo.InvariantCulture) + " held regions, not 2", failures);
                Expect(s.heldRegions == linker.HeldRegionCount,
                    "the socket and the link disagree about what is held", failures);
                builder.Append("RCMSOCKET holding  ")
                       .Append(s.heldRegions.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            finally { Discard(socket, linker, provider); }
        }

        /// <summary> The half that corrupts scenes when it is omitted. Clearing asks for the ground back first and only
        /// then drops the reference, and a cleared socket reads as EMPTY rather than as a broken link: clearing is a
        /// decision, not a fault. </summary>
        private static void CaseClearReleasesBeforeItEmpties(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket;
            TerrainLinker linker;
            FakeProviderComponent provider;
            Connect(out socket, out linker, out provider, "clearing");
            try
            {
                TerrainWriteGateway gateway = TerrainWriteGateway.Active;
                string refusal;
                gateway.RequestBuildingPad(TerrainRegion.FromFootprint(30.0, 30.0, 8.0, 8.0, 0.0, 4.0),
                    "rcm-socket-gate.pad2", out refusal);
                gateway.RequestCutRegion(TerrainRegion.FromFootprint(70.0, 30.0, 24.0, 6.0, 0.0, 4.0),
                    "rcm-socket-gate.cut2", out refusal);
                Expect(socket.Status().heldRegions == 2, "the two holds were not taken", failures);

                int released = socket.Clear();
                Expect(released == 2, "clearing released " + released.ToString(CultureInfo.InvariantCulture)
                    + " regions, not 2 -- the reference was dropped before the ground was given back", failures);
                Expect(socket.enteredPathTool == null, "clearing did not empty the socket", failures);
                Expect(TerrainWriteGateway.Active.OutstandingCount == 0,
                    "the socket is empty and the tool is still holding provider-owned ground", failures);
                Expect(linker.Report.sentence == ProviderLinkContract.DisconnectSentence,
                    "clearing the socket did not run the tool's own disconnect path", failures);

                PathLinkStatus s = socket.Status();
                Expect(s.state == PathLinkState.Empty,
                    "a deliberately cleared socket reads as " + s.state.ToString() + " rather than empty", failures);
                builder.Append("RCMSOCKET cleared  released ")
                       .Append(released.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            finally { Discard(socket, linker, provider); }
        }

        /// <summary> A link that was accepted and is now gone. The socket remembers the name across a load -- which is
        /// what the banking call below stands in for -- so a deleted link is reported as a deleted link instead of
        /// silently becoming an empty socket, and the author is told to look for what it left behind. </summary>
        private static void CaseVanishedLinkIsNamedNotForgotten(StringBuilder builder, List<string> failures)
        {
            PathProviderSocket socket;
            TerrainLinker linker;
            FakeProviderComponent provider;
            Connect(out socket, out linker, out provider, "vanishing");
            try
            {
                // What a scene load does to a socket with a link in it: bank the accepted name. Toggling `enabled` was
                // the earlier stand-in for that and is not a dependable one -- the engine makes no promise about the
                // enable callbacks for a behaviour that is not marked to execute in edit mode, and a batchmode session
                // is edit mode. So the case calls the method OnEnable itself calls. What is under test is that the name
                // is banked and outlives the link, not which of Unity's callbacks delivered it.
                socket.RememberAcceptedLink();
                Expect(socket.rememberedToolName == linker.PathToolName,
                    "opening a scene with a link entered did not bank the tool's name", failures);
                Expect(socket.Status().state == PathLinkState.Accepted,
                    "a scene reopened with a link entered did not read as connected", failures);

                string expectedName = linker.PathToolName;
                Object.DestroyImmediate(linker.gameObject);
                linker = null;

                PathLinkStatus s = socket.Status();
                Expect(s.state == PathLinkState.Vanished,
                    "a deleted link read as " + s.state.ToString() + " rather than as missing", failures);
                Expect(s.SocketFilled, "a deleted link read as a socket nobody ever filled in", failures);
                Expect(s.sentence.Contains(expectedName),
                    "the missing-link sentence does not name what went missing", failures);
                Expect(s.sentence.Contains("left behind"),
                    "the missing-link sentence does not warn that its holds could not be released", failures);
                builder.Append("RCMSOCKET vanished  ").Append(s.state.ToString()).Append('\n');
            }
            finally { Discard(socket, linker, provider); }
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

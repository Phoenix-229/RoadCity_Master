#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UniverseCreator.Nodes;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-101. Proves that the build reports itself into Unity's own background task area and that the Cancel
    /// button on one entry cancels one build.
    ///
    /// RM-100 moved building off the blocking path; this section made it visible. Those are separate claims and they
    /// fail separately, which is why this is a separate gate: RM-100's gate can pass with a build that runs in the
    /// background and tells the author nothing, and a progress panel can be perfectly drawn over a dispatcher that
    /// still blocks.
    ///
    /// Eight sweeps, and what each one would catch:
    ///
    /// 1. The modal bar, tool-wide. RM-100's gate reads <c>Road.cs</c> only, because that was where the bar was raised
    ///    from. That is no longer enough: the file that raised the other one -- a demo window with a seconds field and
    ///    a button -- has been deleted, and the wrapper it called has gone with it. A modal bar reintroduced anywhere,
    ///    in a wizard or an operator, is the freeze coming back, so the scan is over every <c>.cs</c> file in the tool.
    /// 2. Identity. A panel redrawn every frame has to know whether the entry it drew last frame is the same build. The
    ///    label cannot answer that -- one road rebuilt twice produces the same label twice -- so ids are handed out per
    ///    build and never reused.
    /// 3. The ticket list, which is literally what the panel draws: the running build first, then the queue in the
    ///    order it will run. Get that wrong and the author watches the fourth road's bar while the first road builds.
    /// 4. One cancel button, one build. Cancelling the third of five must stop the third and leave four queued in
    ///    order, and cancelling the running one must ask it to stop rather than conclude it from under its own worker.
    /// 5. Outcomes. The dispatcher does not stop between builds, so an entry that vanishes has to be explained by a
    ///    recorded result -- otherwise a failed build gets a green tick, which is worse than no panel at all. Includes
    ///    the bound: nobody collects outcomes in a player build or a batch pass, so the channel cannot grow forever.
    /// 6. The reporter itself, read as source, because it lives in the editor assembly and this gate is compiled into
    ///    the runtime one. What is checked there is exactly what cannot be checked by calling it: the subscription is a
    ///    named method, the three passes run in the order that stops an entry being reaped before its outcome arrives,
    ///    and every finish goes through the existence guard.
    /// 7. One owner, one entry. A slider being dragged asks for a build per frame, and all but the last of them are
    ///    thrown away; the queue and the panel both have to collapse to the one that will actually produce a road,
    ///    without touching a second road that happens to carry the same name.
    /// 8. The build's report of its own decisions, which is the other half of "the build tells the author what it did".
    ///    The panel says a build ran; it cannot say the build quietly gave up on something. B-12's terraform truncates
    ///    a side slope that cannot reach natural ground within the authored reach and counts each one; B-14 made that
    ///    count reach the console. This sweep is source-read for the same reason 6 is -- driving a terraform needs a
    ///    Road on a Terrain -- and it covers the count being both REPORTED and RIGHT: reported from both build
    ///    branches, and not zeroed once per terrain tile, which on a road crossing two tiles would report the last
    ///    tile's truncations as though they were all of them. </summary>
    public static class CityBuildProgressGate
    {
        #region "Entry"
        // UnityEngine and UnityEditor are both reached fully qualified: this file imports neither, and inside
        // namespace RoadCityMaster.City.Editor the bare identifier `Editor` binds to the NAMESPACE, so adding
        // `using UnityEditor;` beside `using System;` would make Object and Progress ambiguous (CS0104).
        [UnityEditor.MenuItem("Window/Road City Master/Testing/City build progress and cancel")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { UnityEngine.Debug.Log(report); }
            else { UnityEngine.Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityBuildProgressGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            UnityEngine.Debug.Log(report);
            UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder measured = new StringBuilder();

            SweepNoModalBarAnywhere(failures, measured);
            SweepEveryBuildHasAnIdentity(failures, measured);
            SweepTicketsAreWhatThePanelDraws(failures, measured);
            SweepCancelOneLeavesTheRest(failures, measured);
            SweepOutcomesExplainEveryEnding(failures, measured);
            SweepTheReporterCannotDrift(failures, measured);
            SweepOneOwnerOneEntry(failures, measured);
            SweepTruncatedBatterIsReported(failures, measured);

            StringBuilder text = new StringBuilder();
            text.Append("RM-101 progress and cancel\n").Append(measured.ToString());
            if (failures.Count == 0)
            {
                text.Append("  PASS\n");
                report = text.ToString();
                return true;
            }
            for (int i = 0; i < failures.Count; i++)
            {
                text.Append("  FAIL: ").Append(failures[i]).Append("\n");
            }
            report = text.ToString();
            return false;
        }


        private static void Expect(bool condition, string complaint, List<string> failures)
        {
            if (!condition) { failures.Add(complaint); }
        }
        #endregion


        #region "Reading the tool's own source"
        /// <summary> Where the tool is on disk. Derived from this file's own asset path rather than assumed, so the gate
        /// still works when the folder is moved or vendored under a different parent. </summary>
        private static string ToolRoot()
        {
            string[] found = UnityEditor.AssetDatabase.FindAssets("CityBuildProgressGate");
            for (int i = 0; i < found.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(found[i]);
                if (path.EndsWith("City/Editor/CityBuildProgressGate.cs", StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - "City/Editor/CityBuildProgressGate.cs".Length);
                }
            }
            return "Assets/RoadCityMaster/";
        }


        private static string ReadSource(string relativePath)
        {
            string full = Path.Combine(ToolRoot(), relativePath);
            if (!File.Exists(full)) { return null; }
            return File.ReadAllText(full);
        }


        private static bool SourceExists(string relativePath)
        {
            return File.Exists(Path.Combine(ToolRoot(), relativePath));
        }


        /// <summary> True when <paramref name="needle"/> appears on a line that is not a comment. The same rule RM-57's
        /// ownership sweep uses, and for the same reason: a gate that failed on the word inside the paragraph explaining
        /// why the word is banned is a gate somebody deletes. </summary>
        private static bool AppearsInCode(string source, string needle, out int lineNumber)
        {
            lineNumber = 0;
            if (string.IsNullOrEmpty(source)) { return false; }
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) { continue; }
                if (trimmed.StartsWith("*", StringComparison.Ordinal)) { continue; }
                if (trimmed.StartsWith("/*", StringComparison.Ordinal)) { continue; }
                if (lines[i].IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    lineNumber = i + 1;
                    return true;
                }
            }
            return false;
        }


        /// <summary> How many non-comment lines carry <paramref name="needle"/>. A count rather than a presence test
        /// where the sweep's claim is about a single call site: "the finish is guarded" is only true if there is one
        /// finish. </summary>
        private static int CodeLineCount(string source, string needle)
        {
            if (string.IsNullOrEmpty(source)) { return 0; }
            int total = 0;
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) { continue; }
                if (trimmed.StartsWith("*", StringComparison.Ordinal)) { continue; }
                if (trimmed.StartsWith("/*", StringComparison.Ordinal)) { continue; }
                if (lines[i].IndexOf(needle, StringComparison.Ordinal) >= 0) { total++; }
            }
            return total;
        }
        #endregion


        #region "Sweep 1 -- no modal bar anywhere in the tool"
        /// <summary> <c>EditorUtility.DisplayProgressBar</c> and its cancelable twin are modal: they take the editor's
        /// input, repaint nothing else, and have to be cleared on every exit path. A tool that raises one has not moved
        /// building into the background, whatever else it does -- so the ban is tool-wide rather than per-file.
        ///
        /// Two files are skipped, both of them gates, because they carry the banned words inside the complaint strings
        /// that report the ban. A gate that failed on its own error message is a gate somebody switches off. </summary>
        private static void SweepNoModalBarAnywhere(List<string> failures, StringBuilder measured)
        {
            string root = ToolRoot();
            string[] files;
            try { files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories); }
            catch (Exception e) { files = new string[0]; failures.Add("the tool's source is unreadable: " + e.Message); }

            string[] quoteTheWords = new string[] { "CityBuildProgressGate.cs", "CityBuildDispatchGate.cs" };
            string[] banned = new string[]
            {
                "DisplayProgressBar", "DisplayCancelableProgressBar", "EditorUtility.ClearProgressBar",
            };

            int scanned = 0;
            int skipped = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                bool quoting = false;
                for (int q = 0; q < quoteTheWords.Length; q++)
                {
                    if (string.Equals(name, quoteTheWords[q], StringComparison.Ordinal)) { quoting = true; }
                }
                if (quoting) { skipped++; continue; }

                string source;
                try { source = File.ReadAllText(files[i]); }
                catch (Exception) { continue; }
                scanned++;

                for (int b = 0; b < banned.Length; b++)
                {
                    int line;
                    Expect(!AppearsInCode(source, banned[b], out line),
                        "the modal progress bar is back: " + name + ":" + line + " uses " + banned[b] + ". That bar"
                        + " owns the editor's input until it is cleared, so the author cannot cancel, cannot look at the"
                        + " scene and cannot tell a slow build from a hung one", failures);
                }
            }

            Expect(scanned > 0, "the tool-wide scan read no files at all, so it proved nothing", failures);

            // The two files RM-101 removed, and the one it added.
            Expect(!SourceExists("Scripts/Editor/EditorProgressWindow.cs"),
                "Scripts/Editor/EditorProgressWindow.cs is back. It is a vendor demo: a seconds field, a button that"
                + " raises a modal bar for that many seconds, and a private Init() no menu item ever called", failures);
            int declared;
            Expect(!AppearsInCode(ReadSource("Scripts/EngineIntegration.cs"), "ClearProgressBar", out declared),
                "EngineIntegration.cs:" + declared + " still wraps ClearProgressBar. Nothing needs clearing once there"
                + " is no modal bar, and leaving the wrapper invites raising one and clearing it 'properly'", failures);
            Expect(SourceExists("Scripts/Editor/BuildProgressReporter.cs"),
                "Scripts/Editor/BuildProgressReporter.cs is missing, so the build now reports nowhere at all", failures);
            Expect(!SourceExists("City/Editor/BuildProgressReporter.cs")
                && !SourceExists("Scripts/BuildProgressReporter.cs"),
                "there is more than one BuildProgressReporter.cs. Two of them would open two entries per build and"
                + " race for the same outcome, and each would draw half the queue", failures);

            measured.Append("  no modal bar in ").Append(scanned).Append(" .cs files (").Append(skipped)
                    .Append(" gates skipped, they quote the words)\n");
        }
        #endregion


        #region "Sweep 2 -- every build has an identity, and no identity comes round twice"
        /// <summary> A progress panel is redrawn from scratch every frame, so the only way it can tell "the same build,
        /// further along" from "the next build, starting" is an id. The label cannot: two roads called Road4 are two
        /// builds, and one road rebuilt twice is also two. Ids therefore come from a counter that only goes up, and a
        /// request the dispatcher refused gets none -- it never became a build, and an entry drawn for it would never be
        /// closed by an outcome. </summary>
        private static void SweepEveryBuildHasAnIdentity(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();

            BuildRequest nothingToDo = new BuildRequest();
            nothingToDo.label = "no work";
            Expect(!dispatch.Enqueue(nothingToDo), "the dispatcher queued a build with neither a worker nor a step",
                failures);
            Expect(nothingToDo.id == 0,
                "a refused request was given an id, so a panel would open an entry for a build that will never run and"
                + " will never be closed", failures);

            BuildRequest first = Staged("road A");
            BuildRequest second = Staged("road B");
            BuildRequest third = Staged("road A");
            Expect(dispatch.Enqueue(first) && dispatch.Enqueue(second) && dispatch.Enqueue(third),
                "the dispatcher refused a well-formed staged build", failures);

            Expect(first.id > 0, "a queued build has no id", failures);
            Expect(second.id == first.id + 1 && third.id == second.id + 1,
                "ids are not handed out in order: " + first.id + ", " + second.id + ", " + third.id, failures);
            Expect(first.label == third.label && first.id != third.id,
                "two builds of the same road share an identity, so the panel cannot tell the rebuild from the first"
                + " build", failures);

            Expect(dispatch.PumpUntilIdle(60), "three staged builds did not all finish", failures);

            BuildRequest afterwards = Staged("road A");
            dispatch.Enqueue(afterwards);
            Expect(afterwards.id > third.id,
                "an id came round again once the queue had drained. An outcome recorded against the old build would"
                + " then close the new build's entry", failures);

            measured.Append("  ids ").Append(first.id).Append(", ").Append(second.id).Append(", ").Append(third.id)
                    .Append(" then ").Append(afterwards.id).Append(" after draining; refused request kept id 0\n");
        }


        /// <summary> A build that finishes in one main-thread step. Enough for every claim in this gate that is about
        /// bookkeeping rather than about work, and it needs no thread, so the gate cannot pass on a fast machine and
        /// fail on a slow one. </summary>
        private static BuildRequest Staged(string label)
        {
            BuildRequest request = new BuildRequest();
            request.label = label;
            request.step = delegate { return false; };
            return request;
        }


        /// <summary> The same, for a build that declares whose it is. </summary>
        private static BuildRequest Staged(string label, string owner)
        {
            BuildRequest request = Staged(label);
            request.owner = owner;
            return request;
        }
        #endregion


        #region "Sweep 3 -- the ticket list is what the panel draws"
        /// <summary> The running build first, then the queue in the order it will run. This is not presentation: an
        /// author who clicked Refresh on five roads is watching to see which one is being built, and a list in queue
        /// order with the running build somewhere in the middle tells them the wrong one.
        ///
        /// The list is the caller's, cleared here, and refilled -- so a surface polling sixty times a second allocates
        /// once for the session. That is checked by handing in a list that already has something in it. </summary>
        private static void SweepTicketsAreWhatThePanelDraws(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();
            int steps = 0;

            BuildRequest running = new BuildRequest();
            running.label = "road being built";
            running.step = delegate
            {
                steps++;
                dispatch.Report(0.5f);
                return steps < 5;
            };

            BuildRequest waiting1 = Staged("road waiting 1");
            BuildRequest waiting2 = Staged("road waiting 2");
            BuildRequest waiting3 = Staged("road waiting 3");
            dispatch.Enqueue(running);
            dispatch.Enqueue(waiting1);
            dispatch.Enqueue(waiting2);
            dispatch.Enqueue(waiting3);

            List<BuildTicket> tickets = new List<BuildTicket>();
            tickets.Add(new BuildTicket());
            tickets.Add(new BuildTicket());

            dispatch.CopyTickets(tickets);
            Expect(tickets.Count == 4,
                "the queue is four builds deep and the panel was handed " + tickets.Count + " entries, so an author who"
                + " asked for four roads is shown a different number of them", failures);

            dispatch.Pump();
            dispatch.Pump();
            dispatch.CopyTickets(tickets);

            bool shaped = tickets.Count == 4;
            Expect(shaped, "the ticket list lost or gained an entry once a build started", failures);
            if (shaped)
            {
                Expect(tickets[0].id == running.id && tickets[0].isRunning,
                    "the build in flight is not the first entry, so the panel's top bar belongs to a road that has not"
                    + " started", failures);
                Expect(tickets[0].progress > 0.49f && tickets[0].progress < 0.51f,
                    "the running build reported 0.5 and the ticket carries " + tickets[0].progress, failures);
                Expect(tickets[1].id == waiting1.id && tickets[2].id == waiting2.id && tickets[3].id == waiting3.id,
                    "the waiting builds are not in the order they will run", failures);
                Expect(!tickets[1].isRunning && !tickets[2].isRunning && !tickets[3].isRunning,
                    "a queued build claims to be running, which draws a bar at 0% for a build that has not begun --"
                    + " indistinguishable from a build that has begun and is getting nowhere", failures);
                Expect(tickets[3].progress == 0f, "a build that has not started reports progress", failures);
                Expect(!string.IsNullOrEmpty(tickets[0].label), "a ticket carries no label to draw", failures);
            }

            dispatch.CopyTickets(null);

            Expect(dispatch.PumpUntilIdle(200), "four staged builds did not all finish", failures);
            dispatch.CopyTickets(tickets);
            Expect(tickets.Count == 0,
                "the dispatcher is idle and still hands out " + tickets.Count + " tickets, so the panel keeps a bar open"
                + " for a build that has ended", failures);

            measured.Append("  tickets: running first at ").Append(steps).Append(" steps, then 3 queued in order,")
                    .Append(" 0 when idle\n");
        }
        #endregion


        #region "Sweep 4 -- one cancel button, one build"
        /// <summary> Each entry in the editor's panel has its own Cancel button, so cancel takes an id. Cancelling the
        /// third of five must stop the third and leave the other four queued, in order -- the whole-queue
        /// <c>Cancel()</c> is a different button and is RM-100's.
        ///
        /// A waiting build is dropped outright and still gets its finish, because it may already have claimed a terrain
        /// opening. A running build is only asked to stop: concluding it inside Cancel would run the teardown while the
        /// worker was still inside the build, which is the bug the legacy <c>Thread.Abort</c> path had. </summary>
        private static void SweepCancelOneLeavesTheRest(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();
            int finishedThird = 0;

            BuildRequest[] queue = new BuildRequest[5];
            for (int i = 0; i < queue.Length; i++)
            {
                queue[i] = Staged("road " + i);
                dispatch.Enqueue(queue[i]);
            }
            queue[2].finish = delegate { finishedThird++; };

            Expect(!dispatch.Cancel(0) && !dispatch.Cancel(-1) && !dispatch.Cancel(queue[4].id + 100),
                "the dispatcher reported that it had cancelled an id it does not hold, so the panel would close an entry"
                + " that is still building", failures);

            Expect(dispatch.Cancel(queue[2].id), "cancelling a queued build reported that it was not there", failures);
            Expect(dispatch.QueuedCount == 4,
                "cancelling one of five left " + dispatch.QueuedCount + " queued", failures);
            Expect(finishedThird == 1,
                "a dropped build did not get its give-it-back call, so anything it had claimed -- a terrain opening, a"
                + " reserved name -- is claimed forever", failures);

            List<BuildTicket> tickets = new List<BuildTicket>();
            dispatch.CopyTickets(tickets);
            bool ordered = tickets.Count == 4
                && tickets[0].id == queue[0].id && tickets[1].id == queue[1].id
                && tickets[2].id == queue[3].id && tickets[3].id == queue[4].id;
            Expect(ordered,
                "the survivors were reordered by the cancel. An author who cancels the third of five must not find the"
                + " remaining four rearranged", failures);
            Expect(dispatch.CancelledCount == 1, "a cancelled build was not counted", failures);

            // The build in flight. Asked to stop, not concluded here.
            BuildDispatch inFlight = new BuildDispatch();
            int ran = 0;
            int gaveBack = 0;

            BuildRequest building = new BuildRequest();
            building.label = "road being cancelled";
            building.step = delegate { ran++; return ran < 500; };
            building.finish = delegate { gaveBack++; };
            BuildRequest behind = Staged("road behind it");
            inFlight.Enqueue(building);
            inFlight.Enqueue(behind);

            inFlight.Pump();
            inFlight.Pump();
            Expect(inFlight.IsGenerating && !inFlight.StopRequested,
                "the first build did not start, so there was nothing to cancel", failures);

            int atCancel = ran;
            Expect(inFlight.Cancel(building.id),
                "cancelling the build in flight reported that it was not there", failures);
            Expect(inFlight.IsGenerating,
                "Cancel concluded the running build from inside the button's own call, so the teardown runs while the"
                + " build is still inside its step -- the same class of bug as the Thread.Abort this section removed",
                failures);
            Expect(inFlight.StopRequested,
                "the cancel never reached the build's stop token, so a long build carries on to the end and the button"
                + " is decoration", failures);

            Expect(inFlight.PumpUntilIdle(800), "a cancelled build never came back", failures);
            Expect(ran <= atCancel + 1,
                "the cancelled build kept stepping after it was told to stop: step " + atCancel + " to " + ran,
                failures);
            Expect(gaveBack == 1, "a cancelled build did not get its give-it-back call", failures);
            Expect(inFlight.CompletedCount == 1,
                "cancelling one entry emptied the queue: the build behind it never ran", failures);

            FinishedBuild ended;
            bool tookFirst = inFlight.TryTakeOutcome(out ended);
            Expect(tookFirst && ended.id == building.id && ended.result == BuildResult.Cancelled,
                "the cancelled build's ending was not recorded as cancelled, so its entry closes as something else --"
                + " a green tick, or nothing at all", failures);
            bool tookSecond = inFlight.TryTakeOutcome(out ended);
            Expect(tookSecond && ended.id == behind.id && ended.result == BuildResult.Completed,
                "the build behind the cancelled one did not complete", failures);

            measured.Append("  cancel by id: 5 queued minus 1 leaves 4 in order; the running build stopped at step ")
                    .Append(ran).Append(" of 500 and the one behind it still ran\n");
        }
        #endregion


        #region "Sweep 5 -- every ending is explained, and the explanations are bounded"
        /// <summary> The dispatcher does not stop between builds, so an entry that disappears from the ticket list is not
        /// self-explanatory: it could have finished, been refused, or thrown. A surface that guessed would draw a green
        /// tick on a build that failed, which is worse than no panel -- so every ending records a result, and a refusal
        /// is a result of its own rather than a failure. An unassigned spline is not a bug and showing it in red teaches
        /// an author to ignore the panel.
        ///
        /// The channel is bounded because in a player build, or in a batch pass, nobody collects. Sixty-four is enough
        /// that an editor polling every frame never loses one, and the oldest is dropped rather than the newest: the
        /// entry an author is still watching is the recent one. </summary>
        private static void SweepOutcomesExplainEveryEnding(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();
            FinishedBuild outcome;
            Expect(!dispatch.TryTakeOutcome(out outcome),
                "a fresh dispatcher handed out an outcome for a build that never ran", failures);
            Expect(outcome.id == 0 && dispatch.PendingOutcomeCount == 0,
                "the outcome handed back on an empty channel is not empty, so a poller closes an entry at random",
                failures);

            BuildRequest done = Staged("road that builds");

            BuildRequest refused = new BuildRequest();
            refused.label = "road with one node";
            refused.prepare = delegate { return false; };
            refused.step = delegate { return false; };

            BuildRequest brokenPrepare = new BuildRequest();
            brokenPrepare.label = "road with no spline";
            brokenPrepare.prepare = delegate { throw new InvalidOperationException("spline is not assigned"); };
            brokenPrepare.step = delegate { return false; };

            BuildRequest brokenStep = new BuildRequest();
            brokenStep.label = "road that throws";
            brokenStep.step = delegate { throw new InvalidOperationException("two nodes at one place"); };

            dispatch.Enqueue(done);
            dispatch.Enqueue(refused);
            dispatch.Enqueue(brokenPrepare);
            dispatch.Enqueue(brokenStep);
            Expect(dispatch.PumpUntilIdle(200), "four builds with four different endings did not all end", failures);

            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == done.id
                && outcome.result == BuildResult.Completed && outcome.message.Length == 0,
                "a build that finished was not recorded as completed", failures);
            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == refused.id
                && outcome.result == BuildResult.Refused,
                "a build refused before it started was not recorded as a refusal. Reported as a failure it turns an"
                + " unassigned spline into a red entry and a console error", failures);
            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == brokenPrepare.id
                && outcome.result == BuildResult.Failed && outcome.message == "spline is not assigned",
                "a build that threw while reading the scene did not carry its own message out, so the panel has nothing"
                + " to show and the author has to read the console to find out which road it was", failures);
            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == brokenStep.id
                && outcome.result == BuildResult.Failed && outcome.message == "two nodes at one place",
                "a build that threw mid-stage did not carry its own message out", failures);
            Expect(dispatch.PendingOutcomeCount == 0, "the channel kept an outcome nobody recorded", failures);

            // The bound. Seventy builds, sixty-four kept, and the ones kept are the last sixty-four.
            BuildDispatch many = new BuildDispatch();
            BuildRequest[] all = new BuildRequest[70];
            for (int i = 0; i < all.Length; i++)
            {
                all[i] = Staged("road " + i);
                many.Enqueue(all[i]);
            }
            Expect(many.PumpUntilIdle(400), "seventy staged builds did not all finish", failures);
            Expect(many.PendingOutcomeCount == 64,
                "the outcome channel holds " + many.PendingOutcomeCount + " after seventy uncollected builds. Unbounded,"
                + " it grows for as long as the session lasts with nobody reading it", failures);
            Expect(many.TryTakeOutcome(out outcome) && outcome.id == all[6].id,
                "the bound dropped the wrong end: the oldest outcomes must go, because the entry an author is still"
                + " watching is the recent one", failures);

            measured.Append("  endings: completed, refused, threw in prepare, threw in step -- each recorded once;")
                    .Append(" 70 uncollected capped at 64, oldest dropped\n");
        }
        #endregion


        #region "Sweep 6 -- the reporter cannot drift"
        /// <summary> The reporter is in the editor assembly and this gate compiles into the runtime one, so it is read as
        /// source. That is not a weaker check here: what is asserted is exactly what calling it could not prove -- the
        /// subscription is a named method, the three passes run in the one order that is safe, and every finish is
        /// guarded. Each of those is a bug that costs a domain reload to notice and looks like nothing while it is
        /// wrong. </summary>
        private static void SweepTheReporterCannotDrift(List<string> failures, StringBuilder measured)
        {
            string source = ReadSource("Scripts/Editor/BuildProgressReporter.cs");
            if (source == null)
            {
                failures.Add("Scripts/Editor/BuildProgressReporter.cs cannot be read, so nothing reports the build");
                return;
            }

            int subtract = 0, add = 0, at = 0;
            Expect(AppearsInCode(source, "[InitializeOnLoad]", out at),
                "the reporter is never loaded, so it polls nothing until something else happens to touch the class",
                failures);
            Expect(AppearsInCode(source, "EditorApplication.update -= Poll", out subtract)
                && AppearsInCode(source, "EditorApplication.update += Poll", out add) && subtract < add,
                "the frame callback is not detached before it is attached, so the editor accumulates one live"
                + " subscription per domain reload and the panel is driven several times a frame", failures);
            Expect(!AppearsInCode(source, "EditorApplication.update += delegate", out at),
                "the frame callback is an anonymous delegate at line " + at + ", which -= cannot remove: it removes a"
                + " different instance than += added, and therefore removes nothing", failures);

            int outcomesAt = 0, ticketsAt = 0, reapAt = 0;
            bool ordered = AppearsInCode(source, "TryTakeOutcome", out outcomesAt)
                && AppearsInCode(source, "CopyTickets", out ticketsAt)
                && AppearsInCode(source, "dispatch.IsIdle", out reapAt);
            Expect(ordered, "the poll no longer drains outcomes, reads tickets and reaps", failures);
            Expect(ordered && outcomesAt < ticketsAt && ticketsAt < reapAt,
                "the poll's passes are out of order (" + outcomesAt + ", " + ticketsAt + ", " + reapAt + "). Reaping"
                + " before the outcomes are drained writes off a build that finished between two frames as cancelled,"
                + " one frame before the outcome that says it succeeded arrives", failures);
            Expect(CodeLineCount(source, "TryTakeOutcome") == 1 && CodeLineCount(source, "CopyTickets") == 1,
                "the watch surface is read from more than one place in the reporter, so the pass ordering above is not"
                + " the whole story", failures);
            Expect(AppearsInCode(source, "dispatch.IsIdle && dispatch.PendingOutcomeCount == 0", out at),
                "the reap is not gated on the dispatcher holding nothing at all. Ungated, it closes the entry of a build"
                + " whose outcome has not been collected yet", failures);

            Expect(CodeLineCount(source, "Progress.Finish(") == 1
                && AppearsInCode(source, "if (Progress.Exists(progressId)) { Progress.Finish(progressId, status); }",
                    out at),
                "a progress item is finished without the existence check. Ids do not survive a domain reload and"
                + " finishing an unknown one throws, so a reload during a build would fill the console", failures);
            Expect(AppearsInCode(source, "Progress.Start(InspectorFieldRegistry.ProductDisplayName", out at),
                "the panel entry is not titled from the product name, so a rename leaves the old name in the one place"
                + " an author looks while waiting", failures);
            Expect(AppearsInCode(source, "Progress.RegisterCancelCallback", out at),
                "the entry has no Cancel button wired, so the panel shows a build it cannot stop", failures);
            Expect(AppearsInCode(source, "BuildDispatch.Shared.Cancel(buildId)", out at),
                "the Cancel button does not cancel by id", failures);
            Expect(!AppearsInCode(source, "Shared.Cancel()", out at),
                "one entry's Cancel button calls the whole-queue cancel at line " + at + ", so cancelling the third of"
                + " five roads would drop the other four", failures);
            Expect(AppearsInCode(source, "-1f", out at),
                "a build that has not started is not reported as indeterminate. A 0% bar claims the build is running and"
                + " getting nowhere", failures);
            Expect(CodeLineCount(source, "Debug.LogError") == 1,
                "a failure is logged from " + CodeLineCount(source, "Debug.LogError") + " places in a method that runs"
                + " every editor frame. Once per frame for as long as the editor is open is not a log, it is a flood",
                failures);

            // The pull direction. Nothing outside the editor folder may name the reporter: the runtime assembly cannot
            // reference the editor one, so a name there is either a compile error or a second reporter.
            string[] files;
            try { files = Directory.GetFiles(ToolRoot(), "*.cs", SearchOption.AllDirectories); }
            catch (Exception) { files = new string[0]; }
            int outside = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string normalised = files[i].Replace('\\', '/');
                if (normalised.IndexOf("/Scripts/Editor/", StringComparison.Ordinal) >= 0) { continue; }
                if (normalised.EndsWith("City/Editor/CityBuildProgressGate.cs", StringComparison.Ordinal)) { continue; }
                string other;
                try { other = File.ReadAllText(files[i]); }
                catch (Exception) { continue; }
                int line;
                if (AppearsInCode(other, "BuildProgressReporter", out line))
                {
                    outside++;
                    failures.Add(Path.GetFileName(files[i]) + ":" + line + " names BuildProgressReporter. The build is"
                        + " watched by polling, not by being told: a runtime file that names an editor class either does"
                        + " not compile into a player or is a second reporter opening a second entry per build");
                }
            }

            measured.Append("  reporter: named callback detached before attach, outcomes then tickets then reap,")
                    .Append(" finish guarded, cancel by id, ").Append(outside).Append(" runtime references\n");
        }
        #endregion


        #region "Sweep 7 -- one owner, one entry"
        /// <summary> RM-101's last clause: re-starting a build for the same object retires its previous entry instead of
        /// stacking a second one.
        ///
        /// The case is an author dragging a lane-width slider. Every change asks for a build, so within a second the
        /// queue holds a dozen builds of one road, each result thrown away by the next, and the author waits for all
        /// twelve to see the last. The panel is worse: twelve entries, all saying the same road is being built.
        ///
        /// It is solved at the dispatcher, not in the panel. Hiding the extra entries would leave the extra builds
        /// running, and a panel that lies about how much work is queued is the thing progress reporting exists to stop.
        ///
        /// Identity is the owner, not the label. The vendor names roads per road system, so a scene with two systems
        /// holds two "Road0" -- superseding on the name would cancel one author's build because a different road
        /// matched. A request that declares no owner falls back to its label, which is right for a one-off. </summary>
        private static void SweepOneOwnerOneEntry(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();

            Expect(dispatch.Supersede(null) == 0 && dispatch.Supersede(string.Empty) == 0
                && dispatch.Supersede("nothing is queued under this") == 0,
                "supersede claims to have replaced something in an empty queue", failures);

            int gaveBack = 0;
            BuildRequest firstA = Staged("road Road4", "road:4");
            BuildRequest onlyB = Staged("road Road9", "road:9");
            BuildRequest secondA = Staged("road Road4", "road:4");
            firstA.finish = delegate { gaveBack++; };
            secondA.finish = delegate { gaveBack++; };
            dispatch.Enqueue(firstA);
            dispatch.Enqueue(onlyB);
            dispatch.Enqueue(secondA);

            Expect(dispatch.Supersede("road:4") == 2,
                "a third build of one road did not replace the two of it already waiting", failures);
            Expect(dispatch.QueuedCount == 1, "superseding one owner left " + dispatch.QueuedCount + " builds queued;"
                + " the other road's build must not be touched", failures);
            Expect(gaveBack == 2, "a dropped build did not get its finish, so whatever it had claimed is still claimed"
                + " (" + gaveBack + " of 2)", failures);
            Expect(dispatch.CancelledCount == 2,
                "a superseded build was not counted as cancelled (" + dispatch.CancelledCount + " of 2)", failures);

            FinishedBuild outcome;
            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == firstA.id
                && outcome.result == BuildResult.Cancelled,
                "the first superseded build has no cancelled outcome, so its entry would be reaped rather than closed",
                failures);
            Expect(dispatch.TryTakeOutcome(out outcome) && outcome.id == secondA.id
                && outcome.result == BuildResult.Cancelled,
                "the second superseded build has no cancelled outcome", failures);
            Expect(!dispatch.TryTakeOutcome(out outcome),
                "superseding recorded an outcome for a build it did not replace", failures);
            // Two road systems, both naming their first road "Road0". One label, two owners, two builds.
            BuildDispatch named = new BuildDispatch();
            BuildRequest inSystemOne = Staged("road Road0", "road:101");
            BuildRequest inSystemTwo = Staged("road Road0", "road:202");
            named.Enqueue(inSystemOne);
            named.Enqueue(inSystemTwo);
            Expect(named.Supersede("road:101") == 1 && named.QueuedCount == 1,
                "superseding one road cancelled another road that happened to share its name", failures);

            // No owner declared: the label stands in, which is what a one-off with a unique name wants.
            BuildDispatch unowned = new BuildDispatch();
            unowned.Enqueue(Staged("terrain rebuild"));
            unowned.Enqueue(Staged("terrain rebuild"));
            Expect(unowned.Supersede("terrain rebuild") == 2,
                "a request that declared no owner is not reachable by its label, so nothing can supersede it", failures);

            // A duplicate already in flight is asked to stop, never concluded from here.
            BuildDispatch running = new BuildDispatch();
            int ran = 0;
            BuildRequest building = Staged("road Road4", "road:4");
            building.step = delegate { ran++; return ran < 500; };
            BuildRequest other = Staged("road Road9", "road:9");
            running.Enqueue(building);
            running.Enqueue(other);
            running.Pump();
            running.Pump();
            Expect(running.Supersede("road:4") == 1, "a build in flight was not replaced by a newer build of it",
                failures);
            Expect(running.IsGenerating, "supersede concluded the running build from inside its caller's call, which"
                + " runs the teardown while the worker is still inside the build", failures);
            Expect(running.StopRequested, "the superseded build in flight was not asked to stop, so it carries on and"
                + " applies a road the author has already replaced", failures);
            running.PumpUntilIdle(900);
            Expect(running.TryTakeOutcome(out outcome) && outcome.id == building.id
                && outcome.result == BuildResult.Cancelled,
                "the superseded build in flight did not end as cancelled", failures);
            Expect(running.TryTakeOutcome(out outcome) && outcome.id == other.id
                && outcome.result == BuildResult.Completed,
                "superseding one road emptied the queue: the build behind it never ran", failures);

            // The ticket carries the owner, or the panel cannot group by it.
            BuildDispatch tickets = new BuildDispatch();
            tickets.Enqueue(Staged("road Road4", "road:4"));
            tickets.Enqueue(Staged("terrain rebuild"));
            List<BuildTicket> drawn = new List<BuildTicket>();
            tickets.CopyTickets(drawn);
            Expect(drawn.Count == 2 && drawn[0].owner == "road:4" && drawn[1].owner == "terrain rebuild",
                "a ticket does not carry the owner it belongs to, so a progress surface has nothing to group by and"
                + " falls back to one entry per build", failures);
            // The road has to ask for the supersede, and before it queues -- afterwards would cancel the build it just
            // asked for. This is source-read rather than called: driving it needs a Road in a scene with a terrain.
            string road = ReadSource("Scripts/Road.cs");
            int supersedeAt = 0, queueAt = 0;
            bool asks = road != null && AppearsInCode(road, "Shared.Supersede(owner)", out supersedeAt)
                && AppearsInCode(road, "Shared.EnqueueStaged(", out queueAt);
            Expect(asks, "Road.RequestUpdate does not supersede its own earlier builds before queueing, so dragging a"
                + " slider fills the queue with builds of one road whose results are each thrown away", failures);
            Expect(asks && supersedeAt < queueAt,
                "the supersede is at line " + supersedeAt + " and the queueing at " + queueAt + ": superseding after"
                + " queueing cancels the build just asked for", failures);

            string reporter = ReadSource("Scripts/Editor/BuildProgressReporter.cs");
            int retireAt = 0, startAt = 0, at2 = 0;
            bool retires = reporter != null
                && AppearsInCode(reporter, "RetireOlder(ticket.owner, ticket.id)", out retireAt)
                && AppearsInCode(reporter, "Progress.Start(", out startAt);
            Expect(retires, "the reporter opens an entry without retiring the same owner's older one, so the panel"
                + " stacks an entry per build even though only the last of them will produce a road", failures);
            Expect(retires && retireAt < startAt,
                "the retire is at line " + retireAt + " and the new item at " + startAt + ": retiring afterwards"
                + " finishes the item that was just opened", failures);
            Expect(reporter != null && CodeLineCount(reporter, "Progress.Start(") == 1,
                "an entry is opened from more than one place in the reporter, so the retire above does not cover every"
                + " entry the panel shows", failures);
            Expect(reporter != null && CodeLineCount(reporter, "RetireOlder") == 2,
                "the retire is defined and called from " + (reporter == null ? 0 : CodeLineCount(reporter, "RetireOlder"))
                + " places rather than defined once and called once", failures);
            Expect(reporter != null && AppearsInCode(reporter, "entry.owner = ticket.owner;", out at2),
                "an entry does not record whose build it is, so the retire has nothing to match on", failures);
            Expect(reporter != null && AppearsInCode(reporter, "if (entry.buildId >= keepBuildId) { continue; }", out at2),
                "the retire is not restricted to OLDER builds. Ids only rise, so without that test two entries for one"
                + " owner retire each other on alternate frames and the panel flickers instead of settling", failures);

            measured.Append("  supersede: 2 queued duplicates dropped, 1 in flight asked to stop, same-name roads")
                    .Append(" untouched, ticket carries owner, reporter retires older entry\n");
        }


        /// <summary> B-14. The build's report of a decision it made on its own: a side slope that could not reach
        /// natural ground inside the authored reach, truncated, and the ground below it left flat.
        ///
        /// Source-read rather than driven, for sweep 6's reason -- a terraform needs a Road on a Terrain, and this gate
        /// is compiled into the runtime assembly while <c>Road.cs</c>'s build path is editor-guarded. What is asserted
        /// is the shape a warning has to have to be worth anything: it exists, it comes from BOTH build branches, and
        /// the number in it is the whole road's.
        ///
        /// The both-branches test is written as an equality against <c>StoreTerrainHistory</c> rather than by naming
        /// the two methods, because that is the invariant that survives a third branch being added: whatever
        /// terraforms takes the result into the Terrain and stores its history, and whatever stores its history has to
        /// say what it gave up on. Naming <c>ConstructRoad2</c> and <c>UpdateRoadNoMultiThreading</c> would pass on the
        /// day a third path is written with no report in it. </summary>
        private static void SweepTruncatedBatterIsReported(List<string> failures, StringBuilder measured)
        {
            string road = ReadSource("Scripts/Road.cs");
            string threaded = ReadSource("Scripts/Terrain/TerraformingThreaded.cs");
            string terraforming = ReadSource("Scripts/Terrain/Terraforming.cs");
            if (road == null || threaded == null || terraforming == null)
            {
                failures.Add("one of Road.cs, Terrain/TerraformingThreaded.cs or Terrain/Terraforming.cs cannot be"
                    + " read, so whether a truncated side slope is reported cannot be established");
                return;
            }

            int at = 0;
            Expect(AppearsInCode(road, "Threading.TerraformingThreaded.BatterHandBackCount", out at),
                "nothing reads BatterHandBackCount. The terraform counts every station and side whose batter was cut"
                + " off before it reached the ground and then the figure is discarded, so the tool decides to leave a"
                + " flat shelf under the road, records that it did, and tells the author nothing", failures);

            int reports = CodeLineCount(road, "ReportTruncatedBatters();");
            int stores = CodeLineCount(road, "StoreTerrainHistory();");
            Expect(reports == stores, "the terrain result is taken into the Terrain from " + stores + " place(s) but"
                + " the truncated-batter warning is raised from " + reports + ". One branch terraforming quietly is"
                + " worse than neither doing it: the warning appears for a road built with the threaded path and not"
                + " for the same road built synchronously, which reads as an intermittent bug rather than a decision",
                failures);
            Expect(stores > 1, "there is only " + stores + " terraform result being stored, so this sweep's"
                + " both-branches claim is no longer checking anything -- if a build branch was removed, say so here",
                failures);
            Expect(CodeLineCount(road, "ReportTruncatedBatters") == reports + 1,
                "the reporter is not defined exactly once for its call sites, so two branches may be warning in two"
                + " different wordings about the same thing", failures);

            Expect(AppearsInCode(road, "Debug.LogWarning(\"Road & City Master: the side slope on ", out at),
                "the truncated-slope report does not open with the product name, or is not a warning. It must be a"
                + " warning and not an error -- truncating is a legitimate outcome, and real practice benches, retains"
                + " or re-aligns at that point, which is the author's call and not the tool's", failures);
            Expect(AppearsInCode(road, "batterMaxReachMetres.ToString(", out at)
                && AppearsInCode(road, "batterRunPerRise.ToString(", out at),
                "the report does not quote the two fields that decide the truncation, so an author who reads it knows"
                + " something was cut off but not which control lets them do anything about it", failures);

            // The count has to bracket the whole road, not the last of its terrain tiles.
            Expect(CodeLineCount(threaded, "ResetBatterHandBackCount();") == 0,
                "the batter count is reset from inside TerraformingThreaded. DoRects runs once per terrain tile, so a"
                + " road crossing two tiles has the first tile's truncations zeroed before the second tile starts and"
                + " the reported figure is the last tile's alone", failures);
            int armAt = 0, resetAt = 0, immediateAt = 0;
            bool armed = AppearsInCode(terraforming, "City.TerrainBuildSettings.ApplyToWorker();", out armAt)
                && AppearsInCode(terraforming, "Threading.TerraformingThreaded.ResetBatterHandBackCount();", out resetAt)
                && AppearsInCode(terraforming, "Threading.TerrainCalcsStatic.RunMe(", out immediateAt);
            Expect(armed, "the reset does not sit beside the call that arms the worker in Terraforming.cs, which is the"
                + " one main-thread moment that covers the threaded branch and the immediate branch alike", failures);
            Expect(armed && armAt < resetAt && resetAt < immediateAt,
                "the reset is at line " + resetAt + ", the worker armed at " + armAt + " and the immediate branch at "
                + immediateAt + ": a reset after the branch zeroes the count the branch just produced", failures);
            Expect(CodeLineCount(terraforming, "ResetBatterHandBackCount") == 1,
                "the count is reset from more than one place in Terraforming.cs, so the bracket around one road's"
                + " terraform is not a bracket", failures);

            // What the count is ABOUT: the slope starts at the edge of what the road emits, or the sentence in the
            // warning is not true of the road on screen.
            Expect(AppearsInCode(threaded, "City.TerrainFootprint.FlattenedHalfWidthMetres(", out at),
                "the flattened band is not sized through City.TerrainFootprint, the class that owns the relationship"
                + " between the built section and the ground held flat around it", failures);
            Expect(AppearsInCode(threaded, "_spline.road.CrossSectionWidth() * 0.5f", out at),
                "the flattened band is not measured from the emitted cross-section. RoadWidth() is laneWidth times the"
                + " lane count -- running lanes only -- so a band sized from it stops flat ground at the edge of the"
                + " lanes and breaks the slope underneath the road's own shoulder and median meshes", failures);
            Expect(AppearsInCode(threaded, "if (Sep > outerBound) { Sep = outerBound; }", out at),
                "the flattened half-width is not clamped to the outer bound the write buffers are sized from, so a"
                + " wide enough authored verge writes past the end of them", failures);

            measured.Append("  truncated batter: counted once per road (reset beside ApplyToWorker, not per tile),")
                    .Append(" warned from all ").Append(stores).Append(" terraform branch(es), band from the emitted")
                    .Append(" section through TerrainFootprint\n");
        }
        #endregion
    }
}
#endif

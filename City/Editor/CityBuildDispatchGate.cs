#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UniverseCreator.Nodes;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-100: building without the editor freezing.
    ///
    /// The author's complaint was that clicking to create a spline road takes the editor away until the road is
    /// finished. That has two separate causes and this gate has to hold both of them down, because fixing one and
    /// claiming the other is how the freeze comes back:
    ///
    /// 1. The MODAL progress bar. <c>EditorUtility.DisplayProgressBar</c> takes input away and repaints nothing else,
    ///    and the road build was ALREADY staged across editor frames -- so for the whole life of this tool the editor
    ///    could have stayed usable and the only thing stopping it was the window put up to say it was busy. Sweep 4
    ///    reads the source and requires that bar and its five teardown calls to be gone, because this is the kind of
    ///    thing that gets helpfully reinstated by somebody debugging a build six months from now.
    ///
    /// 2. Nothing serialised the builds. Two roads asked to refresh at once both started, into per-build buffers and a
    ///    terrain gateway that accumulates one build's regions. Sweep 2 requires the queue to run them one at a time
    ///    and requires their output to come back un-interleaved.
    ///
    /// And two things must not have been broken in the process. A build stopped halfway must apply NOTHING -- sweep 1,
    /// which is RM-98's all-or-nothing clause reaching up into the dispatcher. And the queued path must not become a
    /// second source of geometry: sweep 5 requires the queued front door to call the same construction method the
    /// synchronous one does, which is what makes byte-identical output a property of the code rather than a claim about
    /// it (C-06).
    ///
    /// Sweep 3 is the off-thread rule. It reads every <c>ThreadFunction</c> body in the tool and requires none of them
    /// to touch a <c>UnityEngine.Object</c>, because a <c>UnityEngine.Object</c>'s <c>==</c> and <c>.name</c> marshal
    /// into native code and the failure is not an exception -- it is a crash, or a deadlock, on somebody else's
    /// machine. </summary>
    public static class CityBuildDispatchGate
    {
        #region "Entry"
        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder measured = new StringBuilder();

            SweepCancelledBuildAppliesNothing(failures, measured);
            SweepNoInterleaving(failures, measured);
            SweepWorkerTouchesNoUnityObject(failures, measured);
            SweepNoModalBar(failures, measured);
            SweepOneConstructionPath(failures, measured);
            SweepRefusalIsNotFailure(failures, measured);

            StringBuilder text = new StringBuilder();
            text.Append("RM-100 background building\n").Append(measured.ToString());
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
            string[] found = UnityEditor.AssetDatabase.FindAssets("CityBuildDispatchGate");
            for (int i = 0; i < found.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(found[i]);
                if (path.EndsWith("City/Editor/CityBuildDispatchGate.cs", StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - "City/Editor/CityBuildDispatchGate.cs".Length);
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


        /// <summary> True when <paramref name="needle"/> appears on a line that is not a comment. The same rule
        /// RM-57's ownership sweep uses, and for the same reason: a gate that failed on the word inside the paragraph
        /// explaining why the word is banned is a gate somebody deletes. </summary>
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
        #endregion


        #region "Sweep 1 -- a build stopped halfway applies nothing"
        /// <summary> RM-98 said a write that begins must finish or leave nothing. This is the same clause one level up:
        /// the apply is where every mesh, every parented object and every terrain write happens, so a build that is
        /// stopped must never reach it -- and the give-it-back call must run anyway, or a cancelled build keeps the
        /// host's tunnel opening for the rest of the session.
        ///
        /// Both endings are checked, cancel and throw, because they are separate branches and only one of them is the
        /// one people test. </summary>
        private static void SweepCancelledBuildAppliesNothing(List<string> failures, StringBuilder measured)
        {
            // Cancelled while the worker is running.
            BuildDispatch dispatch = new BuildDispatch();
            int applied = 0;
            int finished = 0;
            bool sawStop = false;

            BuildRequest request = new BuildRequest();
            request.label = "cancelled road";
            request.work = delegate (StopToken stop)
            {
                for (int i = 0; i < 2000; i++)
                {
                    if (stop.StopRequested) { sawStop = true; return; }
                    System.Threading.Thread.Sleep(1);
                }
            };
            request.apply = delegate { applied++; };
            request.finish = delegate { finished++; };

            Expect(dispatch.Enqueue(request), "the dispatcher refused a well-formed build", failures);
            dispatch.Pump();
            Expect(dispatch.IsGenerating, "a queued build did not start when pumped", failures);
            Expect(dispatch.Phase == BuildPhase.Working, "a started build did not report that it was working", failures);

            dispatch.Cancel();
            Expect(dispatch.PumpUntilIdle(400), "a cancelled build never came back", failures);
            Expect(sawStop, "the worker was never told to stop, so a long build would have run to the end", failures);
            Expect(applied == 0,
                "a cancelled build still ran its apply -- that is a half-built road, orphaned children and modified"
                + " terrain", failures);
            Expect(finished == 1, "a cancelled build did not get its give-it-back call", failures);
            Expect(dispatch.Phase == BuildPhase.Cancelled, "a cancelled build did not report itself cancelled",
                failures);
            Expect(dispatch.CancelledCount == 1, "a cancelled build was not counted", failures);
            Expect(dispatch.CompletedCount == 0, "a cancelled build was counted as completed", failures);

            // The worker threw.
            BuildDispatch thrower = new BuildDispatch();
            int appliedAfterThrow = 0;
            int finishedAfterThrow = 0;
            thrower.Enqueue(new BuildRequest
            {
                label = "faulted road",
                work = delegate (StopToken stop) { throw new InvalidOperationException("two nodes at one place"); },
                apply = delegate { appliedAfterThrow++; },
                finish = delegate { finishedAfterThrow++; },
            });
            Expect(thrower.PumpUntilIdle(400), "a faulted build never came back", failures);
            Expect(appliedAfterThrow == 0, "a build whose worker threw still applied its result", failures);
            Expect(finishedAfterThrow == 1, "a faulted build did not get its give-it-back call", failures);
            Expect(thrower.Phase == BuildPhase.Failed, "a faulted build did not report itself failed", failures);
            Expect(thrower.Failure != null && thrower.Failure.Message == "two nodes at one place",
                "the dispatcher lost the exception the worker threw, or reported the AggregateException wrapping it"
                + " instead", failures);

            // A queue dropped by a cancel still gives back what its builds were holding.
            BuildDispatch dropped = new BuildDispatch();
            int finishedDropped = 0;
            for (int i = 0; i < 3; i++)
            {
                dropped.Enqueue(new BuildRequest
                {
                    label = "queued road " + i.ToString(CultureInfo.InvariantCulture),
                    work = delegate (StopToken stop) { },
                    finish = delegate { finishedDropped++; },
                });
            }
            Expect(dropped.QueuedCount == 3, "the queue lost a request", failures);
            dropped.Cancel();
            Expect(dropped.QueuedCount == 0, "cancelling left builds in the queue, which would carry on producing roads"
                + " after the author pressed cancel", failures);
            Expect(finishedDropped == 3,
                "a dropped build never gave back what it was holding -- a terrain opening leaked per cancel", failures);

            measured.Append("  cancel: applies ").Append(applied.ToString(CultureInfo.InvariantCulture))
                    .Append(", finishes ").Append(finished.ToString(CultureInfo.InvariantCulture))
                    .Append("; throw: applies ").Append(appliedAfterThrow.ToString(CultureInfo.InvariantCulture))
                    .Append("; dropped queue gave back ")
                    .Append(finishedDropped.ToString(CultureInfo.InvariantCulture)).Append(" of 3\n");
        }
        #endregion


        #region "Sweep 2 -- two roads at once do not interleave"
        /// <summary> Two builds refreshed together must not run together. The reason is not tidiness: the buffers a road
        /// build fills are per-build, and its terrain writes accumulate one build's regions in the gateway, so two in
        /// flight write over each other's working state and the result is a road with another road's shoulder in it.
        ///
        /// The proof is a shared log. Each build appends its own start and end marker, and the assertion is that no
        /// marker from one appears between the two markers of the other -- which is what "did not interleave" means and
        /// is stronger than counting completions. A counter would pass while both ran side by side. </summary>
        private static void SweepNoInterleaving(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();
            List<string> log = new List<string>();
            object logHandle = new object();
            int liveWorkers = 0;
            int highWater = 0;

            for (int i = 0; i < 4; i++)
            {
                string name = "road" + i.ToString(CultureInfo.InvariantCulture);
                dispatch.Enqueue(new BuildRequest
                {
                    label = name,
                    work = delegate (StopToken stop)
                    {
                        lock (logHandle)
                        {
                            liveWorkers++;
                            if (liveWorkers > highWater) { highWater = liveWorkers; }
                            log.Add(name + ":begin");
                        }
                        System.Threading.Thread.Sleep(6);
                        lock (logHandle)
                        {
                            log.Add(name + ":end");
                            liveWorkers--;
                        }
                    },
                    apply = delegate { lock (logHandle) { log.Add(name + ":apply"); } },
                });
            }

            Expect(dispatch.QueuedCount == 4, "the dispatcher did not queue all four builds", failures);
            Expect(dispatch.PumpUntilIdle(2000), "four queued builds did not all finish", failures);
            Expect(dispatch.CompletedCount == 4, "not every queued build completed", failures);
            Expect(highWater == 1,
                "more than one worker ran at a time, so two builds were writing over each other's working state",
                failures);

            // begin, end, apply -- in that order, three at a time, for each build in turn.
            Expect(log.Count == 12, "the build log is the wrong length, so a stage did not run", failures);
            bool ordered = log.Count == 12;
            for (int i = 0; ordered && i < 4; i++)
            {
                string name = "road" + i.ToString(CultureInfo.InvariantCulture);
                ordered = log[i * 3] == name + ":begin"
                       && log[i * 3 + 1] == name + ":end"
                       && log[i * 3 + 2] == name + ":apply";
            }
            Expect(ordered,
                "the builds interleaved: a marker from one build appeared inside another's begin-to-apply span",
                failures);

            // Queue order is the order they were asked for. A road that was asked for first and built third is a road
            // an author watches not appear while a later one does.
            BuildDispatch fifo = new BuildDispatch();
            List<string> order = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                string name = "r" + i.ToString(CultureInfo.InvariantCulture);
                fifo.Enqueue(name, null, delegate (StopToken stop) { }, delegate { order.Add(name); });
            }
            fifo.PumpUntilIdle(500);
            Expect(order.Count == 3 && order[0] == "r0" && order[1] == "r1" && order[2] == "r2",
                "the queue did not build roads in the order they were asked for", failures);

            measured.Append("  serialisation: peak concurrent workers ")
                    .Append(highWater.ToString(CultureInfo.InvariantCulture)).Append(" of 4 queued, ")
                    .Append(log.Count.ToString(CultureInfo.InvariantCulture)).Append(" markers in strict order\n");
        }
        #endregion


        #region "Sweep 3 -- the worker path touches no UnityEngine.Object"
        /// <summary> The rule that cannot be tested by running it, because the failure it prevents is a native crash on
        /// a machine that is not this one. So it is read out of the source instead: every <c>ThreadFunction</c> body in
        /// the tool, scanned for the accesses that marshal into the engine.
        ///
        /// <c>==</c> is on the list and looks harmless. It is not: <c>UnityEngine.Object</c> overloads it to ask native
        /// code whether the object is alive, which is exactly the call that must not happen off the main thread. The
        /// list is deliberately short and specific rather than a ban on the word "Unity" -- a stage may hold a
        /// <c>Vector3</c>, and <c>Mathf</c> is arithmetic. </summary>
        private static void SweepWorkerTouchesNoUnityObject(List<string> failures, StringBuilder measured)
        {
            string[] workerFiles = new string[]
            {
                "Scripts/Terrain/TerrainCalcs.cs",
                "Scripts/RoadCalcs1.cs",
                "Scripts/RoadCalcs2.cs",
            };
            string[] banned = new string[]
            {
                ".gameObject", ".transform", "GetComponent", "Instantiate(", "DestroyImmediate(",
                "new GameObject(", "AddComponent",
            };

            int scanned = 0;
            for (int f = 0; f < workerFiles.Length; f++)
            {
                string source = ReadSource(workerFiles[f]);
                if (source == null)
                {
                    failures.Add("a calculation stage named by this gate is missing: " + workerFiles[f]);
                    continue;
                }

                string body = ThreadFunctionBody(source);
                if (body == null)
                {
                    failures.Add(workerFiles[f] + " has no ThreadFunction, so this gate is checking nothing in it");
                    continue;
                }
                scanned++;

                for (int b = 0; b < banned.Length; b++)
                {
                    int line;
                    if (AppearsInCode(body, banned[b], out line))
                    {
                        failures.Add(workerFiles[f] + "'s ThreadFunction reaches into the engine with '" + banned[b]
                            + "' -- off the main thread that is a native call, and it fails as a crash rather than as"
                            + " an exception");
                    }
                }
            }

            Expect(scanned == workerFiles.Length, "not every calculation stage was scanned", failures);

            // The dispatcher itself must stay free of the editor -- not conditionally, at all. A player build that
            // rebuilds a road links this file.
            string dispatchSource = ReadSource("City/Runtime/Pipeline/BuildDispatch.cs");
            Expect(dispatchSource != null, "BuildDispatch.cs is missing", failures);
            if (dispatchSource != null)
            {
                int line;
                Expect(!AppearsInCode(dispatchSource, "UnityEditor", out line),
                    "BuildDispatch.cs references UnityEditor on line " + line.ToString(CultureInfo.InvariantCulture)
                    + ", which breaks a player build", failures);
                Expect(!AppearsInCode(dispatchSource, "using UnityEngine", out line),
                    "BuildDispatch.cs imports UnityEngine, which is the first step towards a worker touching a scene",
                    failures);
                Expect(dispatchSource.IndexOf("UniverseCreator.Nodes", StringComparison.Ordinal) >= 0,
                    "BuildDispatch.cs does not use the host's own StopToken, so this tool has invented a second"
                    + " cancellation type and a linked build now has two ways to be abandoned", failures);
            }

            measured.Append("  worker purity: ").Append(scanned.ToString(CultureInfo.InvariantCulture))
                    .Append(" ThreadFunction bodies scanned against ")
                    .Append(banned.Length.ToString(CultureInfo.InvariantCulture)).Append(" engine accesses\n");
        }


        /// <summary> The text between <c>ThreadFunction</c>'s opening brace and its matching close. Brace counting
        /// rather than a regular expression, because a stage's body contains braces and the point is to scan the whole
        /// body and nothing else. </summary>
        private static string ThreadFunctionBody(string source)
        {
            if (string.IsNullOrEmpty(source)) { return null; }
            int at = source.IndexOf("ThreadFunction()", StringComparison.Ordinal);
            while (at >= 0)
            {
                int open = source.IndexOf('{', at);
                if (open < 0) { return null; }
                int depth = 0;
                for (int i = open; i < source.Length; i++)
                {
                    if (source[i] == '{') { depth++; }
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { return source.Substring(open + 1, i - open - 1); }
                    }
                }
                at = source.IndexOf("ThreadFunction()", at + 1, StringComparison.Ordinal);
            }
            return null;
        }
        #endregion


        #region "Sweep 4 -- the modal bar is gone"
        /// <summary> The actual cause of the freeze, and the one a future change is most likely to undo, because
        /// reinstating a progress bar looks like an improvement. <c>DisplayProgressBar</c> is modal: it owns the
        /// editor's input for its lifetime. The build around it was already spread across editor frames, so removing
        /// the bar is most of the fix on its own. </summary>
        private static void SweepNoModalBar(List<string> failures, StringBuilder measured)
        {
            string road = ReadSource("Scripts/Road.cs");
            Expect(road != null, "Road.cs is missing", failures);
            if (road == null) { return; }

            int line;
            Expect(!AppearsInCode(road, "DisplayProgressBar", out line),
                "Road.cs raises a modal progress bar again on line " + line.ToString(CultureInfo.InvariantCulture)
                + " -- that is the freeze the author reported", failures);
            Expect(!AppearsInCode(road, "ClearProgressBar", out line),
                "Road.cs still tears down a modal progress bar on line "
                + line.ToString(CultureInfo.InvariantCulture) + ", so one is being raised somewhere", failures);
            Expect(AppearsInCode(road, "RoadPublishProgress", out line),
                "Road.cs no longer publishes its progress figure, so a progress surface has nothing to read", failures);

            // The runtime side has to be readable without the editor: an inspector, a background task area and a player
            // all ask the same two questions the host's own object answers.
            Expect(AppearsInCode(road, "public bool IsGenerating", out line),
                "Road no longer says whether it is building, which is the one thing every progress surface asks",
                failures);
            Expect(AppearsInCode(road, "public System.Threading.Tasks.Task CurrentTask", out line),
                "Road no longer offers its worker, so a progress surface would have to poll a flag in a loop", failures);
            Expect(AppearsInCode(road, "public void CancelBuild", out line),
                "Road offers no cancel, so RM-101's cancel button would have nothing to call", failures);

            // The pump must exist on both sides. Before this task there was none at runtime at all: EditorUpdate was
            // registered inside #if UNITY_EDITOR and Update was itself editor-only, so a road asked to rebuild in a
            // player set its building flag and then waited for a frame that never arrived.
            Expect(AppearsInCode(road, "public bool AdvanceBuild", out line),
                "the staged build pump is no longer callable from outside the editor path", failures);

            string system = ReadSource("Scripts/RoadSystem.cs");
            Expect(system != null, "RoadSystem.cs is missing", failures);
            if (system != null)
            {
                Expect(system.IndexOf("BuildDispatch.Shared.Pump", StringComparison.Ordinal) >= 0,
                    "nothing pumps the build queue, so a queued road would never be built", failures);
                // RM-109 moved the edit-mode driver out of this file altogether, and that move is what this case now
                // asserts. The defect it hunts has not changed -- an anonymous delegate cannot be matched by any later
                // `-=`, because the `-=` writes a second, different closure, so every OnEnable adds a subscription no
                // OnDisable can remove and a scene opened a dozen times ticks a dozen dead pumps. What changed is where
                // that can happen. A scene component is the wrong owner for a static queue for a reason that has
                // nothing to do with delegates: it can be absent, disabled, loaded after the road that queued the
                // build, or simply not sent the edit-mode message it subscribes from, and a driver that stops does not
                // error -- the build sits at the head of the queue with its progress row live and its counter climbing.
                // So the requirement here is the absence of an editor subscription in this file, and the presence of a
                // play-mode guard on the pump it does own; the editor side is checked against its own owner below.
                Expect(system.IndexOf("SubscribeEditorTick", StringComparison.Ordinal) < 0
                    && system.IndexOf("EditorApplication", StringComparison.Ordinal) < 0,
                    "RoadSystem.cs subscribes to an editor tick again. A scene component cannot own the driver of a"
                    + " static queue: absent, disabled or loaded late, it stops pumping without erroring and every"
                    + " queued build hangs at the head of the queue", failures);
                Expect(system.IndexOf("if (!Application.isPlaying) { return; }", StringComparison.Ordinal) >= 0,
                    "the pump this component owns is not confined to play mode, so play-in-editor is pumped by this"
                    + " and by the editor driver both -- two phase transitions a frame in the editor against one in a"
                    + " player, which is a build that progresses at a different rate in the two", failures);
            }

            // The editor-side owner. Static like the queue it drives, and reached from a domain load rather than from a
            // scene, so no road's build can be blocked by the state, the ordering or the absence of anything in a scene.
            string driver = ReadSource("Scripts/Editor/BuildPumpDriver.cs");
            Expect(driver != null,
                "Scripts/Editor/BuildPumpDriver.cs is gone, so nothing pumps the queue in edit mode and every queued"
                + " build waits for a frame that never arrives", failures);
            if (driver != null)
            {
                Expect(driver.IndexOf("[InitializeOnLoad]", StringComparison.Ordinal) >= 0,
                    "the editor pump driver is never loaded, so it subscribes to nothing until something else happens"
                    + " to touch the class", failures);
                Expect(driver.IndexOf("EditorApplication.update += PumpEditorFrame", StringComparison.Ordinal) >= 0,
                    "the editor pump is subscribed by something other than a named method -- '-=' on an anonymous"
                    + " delegate removes a different instance than '+=' added and therefore removes nothing", failures);
                Expect(driver.IndexOf("EditorApplication.update -= PumpEditorFrame", StringComparison.Ordinal)
                    < driver.IndexOf("EditorApplication.update += PumpEditorFrame", StringComparison.Ordinal)
                    && driver.IndexOf("EditorApplication.update -= PumpEditorFrame", StringComparison.Ordinal) >= 0,
                    "nothing detaches the same named method before attaching it, so a second initialisation leaves two"
                    + " subscriptions pumping the same queue twice a frame", failures);
                Expect(driver.IndexOf("update += delegate", StringComparison.Ordinal) < 0
                    && driver.IndexOf("update += ()", StringComparison.Ordinal) < 0,
                    "the editor pump is subscribed as an anonymous delegate, which no later removal can match",
                    failures);
                Expect(driver.IndexOf("Application.isPlaying", StringComparison.Ordinal) >= 0,
                    "the editor driver pumps during play-in-editor as well, which with RoadSystem's own pump performs"
                    + " two phase transitions a frame in the editor and one in a player", failures);
            }

            measured.Append("  modal bar: absent; runtime surface: IsGenerating, CurrentTask, CancelBuild, AdvanceBuild"
                + " present\n");
            measured.Append("  pump ownership: BuildPumpDriver in edit mode from a domain load, RoadSystem in play mode"
                + " only, one owner per configuration\n");
        }
        #endregion


        #region "Sweep 5 -- one construction path, so the two cannot differ"
        /// <summary> C-06 says the async path may not become a second source of nondeterminism. Comparing two built
        /// roads would be the weak form of that test: it passes on whatever road happened to be built and says nothing
        /// about the next one. The strong form is that the two paths call the same mathematics in the same order, and
        /// that is a property of the source, so it is read out of the source.
        ///
        /// The tool builds a road as nine landmark calls. Synchronously they are nine statements in a row inside
        /// <c>UpdateRoadNoMultiThreading</c>. Staged they are spread across <c>UpdateRoad</c>'s tail, three
        /// <c>ThreadFunction</c> bodies and <c>ConstructRoad2</c>, <c>3</c> and <c>4</c> -- the workers and the applies
        /// alternating. This sweep extracts the landmark sequence from both and requires them to be the same list in the
        /// same order.
        ///
        /// That is why <c>TerrainCalcsStatic.RunMe</c> and <c>TerrainCalcs.ThreadFunction</c> both call
        /// <c>TerraformingThreaded.DoRects</c> rather than each carrying its own loop, and it is what makes the two
        /// paths' geometry identical rather than similar. If somebody adds a stage to one path and forgets the other,
        /// the sequences stop matching and this fails on the next run rather than on somebody's terrain. </summary>
        private static void SweepOneConstructionPath(List<string> failures, StringBuilder measured)
        {
            string road = ReadSource("Scripts/Road.cs");
            if (road == null) { return; }

            int line;
            Expect(AppearsInCode(road, "public void RequestUpdate", out line),
                "there is no queued front door onto the road build", failures);

            string front = MethodBody(road, "public void RequestUpdate");
            Expect(front != null, "RequestUpdate has no body this gate can read", failures);
            if (front != null)
            {
                Expect(front.IndexOf("UpdateRoad(updateType)", StringComparison.Ordinal) >= 0,
                    "the queued path does not call UpdateRoad, so it is a second construction implementation and the"
                    + " two can drift apart", failures);
                Expect(front.IndexOf("EnqueueStaged", StringComparison.Ordinal) >= 0,
                    "the queued path does not go through the dispatcher", failures);
                Expect(front.IndexOf("isUsingMultithreading", StringComparison.Ordinal) >= 0,
                    "the queued path ignores the multithreading switch, so switching it off no longer builds inline",
                    failures);
            }

            // The eight landmarks, in the order a road is built. Named by the mathematics they run rather than by the
            // stage that happens to hold them, because the stage boundaries are exactly what differs between the paths.
            string[] landmarks = new string[]
            {
                "DoRects", "ProcessRoadTerrainHook2", "StoreTerrainHistory", "RoadJobPrelim",
                "RoadJob1", "MeshSetup1", "RoadJob2", "MeshSetup2", "ConstructionCleanup",
            };

            string syncBody = MethodBody(road, "private void UpdateRoadNoMultiThreading");
            Expect(syncBody != null, "the synchronous path is gone, so C-06 can no longer be compared at all", failures);

            // Synchronously, Hook1 is called with _isMultithreaded false and forwards to TerrainCalcsStatic.RunMe.
            // Splice RunMe in where the call is, so the sequence being compared is the mathematics rather than the
            // wrapper's name.
            string runMe = MethodBody(ReadSource("Scripts/Terrain/TerrainCalcsStatic.cs"),
                "public static void RunMe");
            Expect(runMe != null, "the immediate terrain stage is missing", failures);

            string terrainWorker = ThreadFunctionBody(ReadSource("Scripts/Terrain/TerrainCalcs.cs"));
            string roadWorker1 = ThreadFunctionBody(ReadSource("Scripts/RoadCalcs1.cs"));
            string roadWorker2 = ThreadFunctionBody(ReadSource("Scripts/RoadCalcs2.cs"));
            string stage2 = MethodBody(road, "private void ConstructRoad2");
            string stage3 = MethodBody(road, "private void ConstructRoad3");
            string stage4 = MethodBody(road, "private void ConstructRoad4");

            if (syncBody == null || runMe == null || terrainWorker == null || roadWorker1 == null
                || roadWorker2 == null || stage2 == null || stage3 == null || stage4 == null)
            {
                failures.Add("a stage this gate compares is missing, so the two paths were not compared at all");
                return;
            }

            string synchronous = Sequence(runMe + "\n" + syncBody, landmarks);
            string staged = Sequence(
                terrainWorker + "\n" + stage2 + "\n" + roadWorker1 + "\n" + stage3 + "\n" + roadWorker2 + "\n" + stage4,
                landmarks);

            Expect(synchronous == staged,
                "the synchronous and staged paths run different mathematics in different orders, so the same road built"
                + " the two ways cannot be byte-identical (C-06). Synchronous: " + synchronous + " -- staged: " + staged,
                failures);
            Expect(synchronous.Length > 0, "neither path ran any of the landmark stages, so this comparison proved"
                + " nothing", failures);

            // Both terrain branches must reach the SAME rect solver. Two copies of that loop is the one way the two
            // paths could keep matching sequences while producing different ground.
            Expect(terrainWorker.IndexOf("DoRects", StringComparison.Ordinal) >= 0
                && runMe.IndexOf("DoRects", StringComparison.Ordinal) >= 0,
                "the threaded and immediate terrain stages no longer share TerraformingThreaded.DoRects, so each has"
                + " its own copy of the rect solver and the two will drift", failures);

            // The worker's cached design figures are applied once, before the branch, not inside one arm of it.
            string hook1 = MethodBody(ReadSource("Scripts/Terrain/Terraforming.cs"),
                "private static void ProcessRoadTerrainHook1Do");
            if (hook1 != null)
            {
                int applyAt = hook1.IndexOf("ApplyToWorker", StringComparison.Ordinal);
                int branchAt = hook1.IndexOf("if (_isMultithreaded)", StringComparison.Ordinal);
                Expect(applyAt >= 0, "the worker's design figures are never applied, so a stage reads stale settings",
                    failures);
                Expect(branchAt >= 0, "the terrain stage no longer branches on the multithreading switch", failures);
                Expect(applyAt >= 0 && branchAt >= 0 && applyAt < branchAt,
                    "the design figures are applied inside one arm of the threaded/immediate branch, so the two paths"
                    + " set up differently -- which is how they quietly diverge", failures);
            }

            // The dispatcher's own determinism: the same work, queued the same way, twice, must come back the same.
            string firstRun = RunOrder();
            string secondRun = RunOrder();
            Expect(firstRun == secondRun,
                "the queue produced a different order on two identical runs: '" + firstRun + "' then '" + secondRun
                + "'", failures);

            measured.Append("  C-06: landmark sequence '").Append(synchronous)
                    .Append("' identical down both paths, queue order '").Append(firstRun).Append("' twice\n");
        }


        /// <summary> The landmark names present in <paramref name="source"/>, in the order they first appear, joined for
        /// comparison and for the report. First appearance rather than every appearance, because a stage may mention a
        /// call twice -- inside a branch and again in its else -- and the thing being compared is the order of the
        /// mathematics, not how many times it is written down. </summary>
        private static string Sequence(string source, string[] landmarks)
        {
            List<KeyValuePair<int, string>> found = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < landmarks.Length; i++)
            {
                int at = source.IndexOf(landmarks[i], StringComparison.Ordinal);
                if (at >= 0) { found.Add(new KeyValuePair<int, string>(at, landmarks[i])); }
            }
            found.Sort(delegate (KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
                return a.Key.CompareTo(b.Key);
            });

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < found.Count; i++)
            {
                if (i > 0) { text.Append(" > "); }
                text.Append(found[i].Value);
            }
            return text.ToString();
        }


        private static string RunOrder()
        {
            BuildDispatch dispatch = new BuildDispatch();
            StringBuilder order = new StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                string name = i.ToString(CultureInfo.InvariantCulture);
                dispatch.Enqueue("build " + name, null, delegate (StopToken stop) { }, delegate { order.Append(name); });
            }
            dispatch.PumpUntilIdle(1000);
            return order.ToString();
        }


        private static string MethodBody(string source, string signature)
        {
            if (string.IsNullOrEmpty(source)) { return null; }
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) { return null; }
            int open = source.IndexOf('{', at);
            if (open < 0) { return null; }
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') { depth++; }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) { return source.Substring(open + 1, i - open - 1); }
                }
            }
            return null;
        }
        #endregion


        #region "Sweep 6 -- a refusal is not a failure, and a staged build is pumped"
        /// <summary> Two things that are easy to get backwards.
        ///
        /// An unassigned spline or a road with fewer than two nodes is a refusal: the build declines before it starts,
        /// nothing was applied, and nothing went wrong. Reporting that as a failure trains an author to ignore the
        /// report, which is worse than saying nothing. So the counters are separate and this sweep proves the split.
        ///
        /// And the staged path -- the one a road actually uses, because a road's stages already alternate between a
        /// worker and a main-thread apply -- has to reach the same ending as the worker path: applied when it finished,
        /// not applied when it was stopped, finished either way. </summary>
        private static void SweepRefusalIsNotFailure(List<string> failures, StringBuilder measured)
        {
            BuildDispatch dispatch = new BuildDispatch();
            int applied = 0;
            int finished = 0;
            dispatch.Enqueue(new BuildRequest
            {
                label = "unassigned spline",
                prepare = delegate { return false; },
                work = delegate (StopToken stop) { applied += 100; },
                apply = delegate { applied++; },
                finish = delegate { finished++; },
            });
            Expect(dispatch.PumpUntilIdle(50), "a refused build left the dispatcher busy", failures);
            Expect(applied == 0, "a refused build ran its worker or its apply", failures);
            Expect(finished == 1, "a refused build did not get its give-it-back call", failures);
            Expect(dispatch.RefusedCount == 1, "a refusal was not counted", failures);
            Expect(dispatch.FailedCount == 0,
                "a refusal was reported as a failure, which is how an author learns to ignore the report", failures);
            Expect(dispatch.Phase != BuildPhase.Failed, "a refusal left the dispatcher saying a build had failed",
                failures);

            // A throwing prepare IS a failure -- the distinction is between declining and breaking.
            BuildDispatch broken = new BuildDispatch();
            broken.Enqueue(new BuildRequest
            {
                label = "broken prepare",
                prepare = delegate { throw new NullReferenceException("no road system"); },
                work = delegate (StopToken stop) { },
            });
            broken.PumpUntilIdle(50);
            Expect(broken.FailedCount == 1, "a prepare that threw was not counted as a failure", failures);
            Expect(broken.RefusedCount == 0, "a prepare that threw was counted as a polite refusal", failures);

            // Staged: pumped on the main thread, one step per pump, applied at the end.
            BuildDispatch staged = new BuildDispatch();
            int steps = 0;
            int stagedApplied = 0;
            int stagedFinished = 0;
            BuildRequest request = new BuildRequest();
            request.label = "staged road";
            request.step = delegate { steps++; return steps < 3; };
            request.apply = delegate { stagedApplied++; };
            request.finish = delegate { stagedFinished++; };
            Expect(staged.Enqueue(request), "the dispatcher refused a staged build that has no pool worker", failures);
            Expect(staged.PumpUntilIdle(50), "a staged build never finished", failures);
            Expect(steps == 3, "the staged build was pumped " + steps.ToString(CultureInfo.InvariantCulture)
                + " times rather than three", failures);
            Expect(stagedApplied == 1, "a staged build that finished did not apply", failures);
            Expect(stagedFinished == 1, "a staged build did not get its give-it-back call", failures);
            Expect(staged.CompletedCount == 1, "a staged build was not counted as completed", failures);

            // Staged and cancelled: the road case that matters, because this is where a half-built road would appear.
            BuildDispatch stopped = new BuildDispatch();
            int stoppedApplied = 0;
            int stoppedFinished = 0;
            int stoppedSteps = 0;
            BuildRequest longBuild = new BuildRequest();
            longBuild.label = "cancelled staged road";
            longBuild.step = delegate { stoppedSteps++; return true; };
            longBuild.apply = delegate { stoppedApplied++; };
            longBuild.finish = delegate { stoppedFinished++; };
            stopped.Enqueue(longBuild);
            stopped.Pump();
            stopped.Pump();
            stopped.Cancel();
            Expect(stopped.PumpUntilIdle(50), "a cancelled staged build never came back", failures);
            Expect(stoppedApplied == 0,
                "a staged build cancelled mid-flight still applied, which is a partial road on the terrain", failures);
            Expect(stoppedFinished == 1, "a cancelled staged build did not give back what it was holding", failures);
            Expect(stopped.Phase == BuildPhase.Cancelled, "a cancelled staged build did not report itself cancelled",
                failures);

            // A step that throws must be caught: its caller is EditorApplication.update, where an exception is
            // swallowed and the build would be left half-started with IsGenerating stuck true for the session.
            BuildDispatch throwing = new BuildDispatch();
            int throwingFinished = 0;
            BuildRequest bad = new BuildRequest();
            bad.label = "throwing step";
            bad.step = delegate { throw new InvalidOperationException("spline went null"); };
            bad.finish = delegate { throwingFinished++; };
            throwing.Enqueue(bad);
            throwing.PumpUntilIdle(50);
            Expect(throwing.FailedCount == 1, "a staged build whose step threw was not recorded as failed", failures);
            Expect(!throwing.IsGenerating,
                "a staged build whose step threw left the dispatcher saying it was still building", failures);
            Expect(throwingFinished == 1, "a staged build whose step threw did not give back what it was holding",
                failures);

            Expect(!dispatch.Enqueue(new BuildRequest { label = "nothing to do" }),
                "the dispatcher queued a build with neither a worker nor a step, which would show as a road that never"
                + " appears", failures);

            measured.Append("  refusals ").Append(dispatch.RefusedCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" counted apart from failures; staged pumped ")
                    .Append(steps.ToString(CultureInfo.InvariantCulture))
                    .Append(" times, cancelled staged applied ")
                    .Append(stoppedApplied.ToString(CultureInfo.InvariantCulture)).Append("\n");
        }
        #endregion
    }
}
#endif

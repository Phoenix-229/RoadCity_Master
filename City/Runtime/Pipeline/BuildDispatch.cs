#region "Imports"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniverseCreator.Nodes;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which part of a build is running. Read from the main thread only. </summary>
    public enum BuildPhase
    {
        Idle = 0,

        /// <summary> On the main thread, reading Unity objects and copying what the worker needs into plain data. This
        /// is the only phase allowed to touch a <c>UnityEngine.Object</c> before the apply. </summary>
        Preparing = 1,

        /// <summary> On a pool thread, arithmetic on plain arrays. </summary>
        Working = 2,

        /// <summary> Back on the main thread, creating meshes, writing terrain and parenting objects. </summary>
        Applying = 3,

        Complete = 4,

        /// <summary> Stopped on request. Nothing was applied, so nothing was half-built. </summary>
        Cancelled = 5,

        /// <summary> The worker threw. Nothing was applied. <see cref="BuildDispatch.Failure"/> holds what. </summary>
        Failed = 6,
    }


    /// <summary> One build waiting its turn or in flight. Three delegates rather than a subclass per build, because the
    /// three phases are already three separate bodies of code in the builder and the only thing this type adds is WHERE
    /// each of them is allowed to run. </summary>
    public sealed class BuildRequest
    {
        /// <summary> What to call it in a progress surface and in a log. </summary>
        public string label = string.Empty;

        /// <summary> RM-101. Who this build is for, when "the same thing built again" has to be recognised.
        ///
        /// The label cannot carry that. Two <see cref="UnityEngine.GameObject"/>s are allowed the same name, and the
        /// vendor names roads per road system, so a scene with two systems holds two "Road0" -- and a supersede keyed
        /// on the label would cancel a build of one while the author edited the other. Set it to something that
        /// identifies the object, an instance id being the obvious one, and leave it empty when a build has no owner
        /// worth tracking: the label stands in, which is right for a one-off with a unique name. </summary>
        public string owner = string.Empty;

        /// <summary> RM-101. Identity, handed out by <see cref="BuildDispatch.Enqueue(BuildRequest)"/> and never
        /// reused. A progress surface polls a list of these once a frame and needs to know whether the entry it drew
        /// last frame is the same build or the next one -- and the label cannot answer that, because two roads called
        /// "road Road4" are two builds and one road rebuilt twice is also two. </summary>
        public int id;

        /// <summary> Main thread. Validate, and copy every setting the worker needs into plain data. Return false to
        /// abandon the build before it starts -- an unassigned spline, a road with two nodes at the same place -- which
        /// is a refusal, not a failure, and must not leave a phase saying <see cref="BuildPhase.Failed"/>. </summary>
        public Func<bool> prepare;

        /// <summary> Pool thread. Touches no <c>UnityEngine.Object</c>. Given the stop token so a long build can check
        /// it between spans and unwind instead of finishing work that is about to be thrown away. </summary>
        public Action<StopToken> work;

        /// <summary> Main thread, polled once per <see cref="BuildDispatch.Pump"/> call, returning true while there is
        /// more to do. For a build that is ALREADY staged across frames with its own workers inside it -- which is what
        /// a road is: the tail of <c>Road.UpdateRoad</c> reads the scene and starts the terrain worker, and
        /// <c>ConstructRoad2</c>, <c>3</c> and <c>4</c> are the main-thread applies between the next two workers.
        ///
        /// Wrapping such a build in <see cref="work"/> instead would mean the pool thread sitting in a loop watching a
        /// <c>MonoBehaviour</c>'s fields to find out whether the main thread had finished a stage yet, which is exactly
        /// the off-thread Unity access this whole task exists to remove. So a staged build keeps its own pump and the
        /// dispatcher's contribution is the part it was missing: one build at a time, a progress figure, a cancel that
        /// reaches it, and an apply that is skipped when it was cancelled. </summary>
        public Func<bool> step;

        /// <summary> Main thread, and only if the worker finished without being stopped and without throwing. Creates
        /// meshes, writes terrain, parents objects. </summary>
        public Action apply;

        /// <summary> Main thread, always, after everything else -- including after a cancel or a failure. This is where
        /// a build gives back what it was holding: terrain openings, claimed regions, temporary objects. Optional.
        /// </summary>
        public Action finish;
    }


    /// <summary> RM-101. How a build ended. Separate from <see cref="BuildPhase"/>, which says what the dispatcher is
    /// doing right now: this says what happened to one particular build, which is what a progress entry has to be
    /// finished with after the dispatcher has already moved on to the next one. </summary>
    public enum BuildResult
    {
        /// <summary> Applied. </summary>
        Completed = 0,

        /// <summary> Stopped, by the author or by a shutdown. Nothing was applied. </summary>
        Cancelled = 1,

        /// <summary> Threw. Nothing was applied and the message is worth showing once. </summary>
        Failed = 2,

        /// <summary> The prepare pass said there was nothing to build. Not an error, and reporting it as one teaches an
        /// author to ignore the report. </summary>
        Refused = 3,
    }


    /// <summary> RM-101. A snapshot of one queued or running build, for a progress surface to draw.
    ///
    /// A struct, and copied into a caller-owned list, for one reason: this is read once per editor frame for every
    /// build in the queue, and a class would make that a per-frame allocation whose only purpose is to be collected.
    /// It carries no reference to the request, so holding one cannot keep a finished build's delegates -- and its
    /// closures, and whatever they captured -- alive. </summary>
    public struct BuildTicket
    {
        /// <summary> Matches <see cref="BuildRequest.id"/>. </summary>
        public int id;

        /// <summary> What to show. </summary>
        public string label;

        /// <summary> RM-101. <see cref="BuildRequest.owner"/>, or the label when the request left it empty. A progress
        /// surface groups by this to keep one entry per object: an author who edits a road twice while it builds must
        /// end with one entry, not a column of them. </summary>
        public string owner;

        /// <summary> True for the one build in flight, false for one still waiting its turn. </summary>
        public bool isRunning;

        /// <summary> 0 to 1 for the running build, 0 for a waiting one. </summary>
        public float progress;
    }


    /// <summary> RM-101. One finished build, waiting to be collected by whoever is reporting progress.
    ///
    /// This exists because the dispatcher does not stop between builds. By the time an editor frame arrives, the build
    /// whose entry is still on screen may have finished, been replaced, and the next one started -- so a poller that
    /// reads only the live state has no way to learn whether the entry that vanished succeeded, was cancelled or threw,
    /// and would have to guess. Guessing here means drawing a green tick on a build that failed.
    ///
    /// Named for the thing rather than for the word "outcome": RM-52's <see cref="BuildOutcome"/> in the root namespace
    /// is a persisted four-value enum answering "how did the last build end", and this is a record of ONE build taken
    /// off the queue. Two types of the same name in one product resolve differently depending on which namespace the
    /// reader is in, which is how a file in <c>Scripts/Editor</c> came to pass an enum to a method wanting this struct.
    /// The tool already prefixes for this reason -- see <c>TunnelBuildOutcome</c>. </summary>
    public struct FinishedBuild
    {
        public int id;
        public string label;
        public BuildResult result;

        /// <summary> The failure's base message, or empty. Base message rather than the whole exception because this is
        /// destined for one line in a progress entry; the full stack is logged by whoever wants it. </summary>
        public string message;
    }


    /// <summary> RM-100. The one place a road, an intersection run or a city block is built without the editor stopping.
    ///
    /// The author's complaint was precise: clicking to create a spline road locks the editor until the build finishes,
    /// so a session becomes a sequence of waits. The cause is that construction runs inside the inspector's own call
    /// stack, from the first node to the last mesh, and nothing about road geometry requires that. The mathematics is
    /// arithmetic on plain arrays. Only three things genuinely need the main thread -- reading Unity objects before
    /// starting, creating meshes and writing terrain at the end, and touching the scene graph -- and separating those
    /// three from everything else is the whole task.
    ///
    /// The contract is COPIED from <c>UniverseCreatorObject.RegenerateAsync</c> rather than invented, and that is a
    /// technical decision rather than a stylistic one. The host already solves the hard parts in this exact engine
    /// version: a main-thread prepare pass, a snapshot into plain data precisely because a <c>UnityEngine.Object</c>'s
    /// <c>==</c> operator and <c>.name</c> marshal into native code and will crash or deadlock off the main thread,
    /// <c>Task.Run</c> with a stop token, and not one <c>UnityEditor</c> reference in the runtime file so player builds
    /// still link. Those are the failure modes this engine actually punishes. Two systems in one project with two
    /// different threading models is also how a deadlock arrives during a linked build, which is the second reason to
    /// have one -- so the token here is the host's own <see cref="StopToken"/>, not a second cancellation type.
    ///
    /// Builds are QUEUED rather than run side by side. Refreshing two roads at once must not interleave into each
    /// other's output, and the cheapest guarantee of that is not locking -- it is one worker at a time, because the
    /// buffers the road builder fills are per-build and its terrain writes go through a gateway that accumulates one
    /// build's regions. Roads are seconds of work each, not minutes, so the throughput lost is not worth the class of
    /// bug avoided.
    ///
    /// There is deliberately no <c>UnityEditor</c> reference anywhere in this file, conditional or otherwise. The
    /// editor drives <see cref="Pump"/> from <c>EditorApplication.update</c> and the progress surface (RM-101) polls
    /// <see cref="CopyTickets"/>, <see cref="TryTakeOutcome"/> and <see cref="Cancel(int)"/> from the editor assembly --
    /// exactly the split the host keeps between <c>UniverseCreatorObject</c> and <c>AsyncProgressUI</c>. A player build
    /// that regenerates a road at runtime pumps it from an <c>Update</c> and behaves identically. </summary>
    public sealed class BuildDispatch
    {
        #region "The shared dispatcher"
        private static BuildDispatch shared;

        /// <summary> The dispatcher the tool's own build entry points use. One queue for the project, so two inspectors
        /// clicking Refresh cannot produce two interleaved builds. A test that needs isolation constructs its own with
        /// <c>new</c> rather than reaching for this. </summary>
        public static BuildDispatch Shared
        {
            get
            {
                if (shared == null) { shared = new BuildDispatch(); }
                return shared;
            }
        }

        /// <summary> Drop the shared dispatcher. For a test that queued work and must not leave it for the next test,
        /// and for a domain reload that wants to start from nothing. Does not cancel -- call <see cref="Cancel"/>
        /// first if that is what is meant, because silently discarding a queue whose builds are holding terrain
        /// openings would leak them. </summary>
        public static void ResetShared()
        {
            shared = null;
        }
        #endregion


        #region "State"
        private readonly Queue<BuildRequest> pending = new Queue<BuildRequest>();

        /// <summary> RM-101. Finished builds a progress surface has not collected yet. Bounded, and the oldest is
        /// dropped when it is full, because in a player build -- or in a batch pass with no editor attached -- nobody
        /// ever collects and an unbounded list would be a leak that grows with every road the session builds. Losing
        /// the oldest notification when 64 have gone uncollected costs a progress entry its final status; keeping them
        /// all costs memory forever. </summary>
        private readonly Queue<FinishedBuild> outcomes = new Queue<FinishedBuild>();

        private const int outcomeCapacity = 64;

        private int nextId;

        private volatile bool isGenerating;
        private volatile float progress;
        private BuildRequest current;
        private Task currentTask;
        private Func<bool> currentStep;
        private StopToken currentStop;
        private Exception failure;
        private BuildPhase phase = BuildPhase.Idle;
        private int completed;
        private int cancelled;
        private int failed;
        private int refused;

        /// <summary> True from the moment the worker is handed off until <see cref="Pump"/> has finished applying.
        /// Volatile because the worker clears nothing and the main thread reads it every editor frame; the host's own
        /// flag is volatile for the same reason. </summary>
        public bool IsGenerating { get { return isGenerating; } }

        /// <summary> The task in flight, or null. Exposed so a progress surface can watch the work without owning it.
        /// </summary>
        public Task CurrentTask { get { return currentTask; } }

        public BuildPhase Phase { get { return phase; } }

        /// <summary> What the current or last build is called. Empty when nothing has run. </summary>
        public string Label { get { return current != null ? current.label : string.Empty; } }

        /// <summary> What the worker threw, or null. Cleared when the next build starts. </summary>
        public Exception Failure { get { return failure; } }

        public int QueuedCount { get { return pending.Count; } }

        public int CompletedCount { get { return completed; } }

        public int CancelledCount { get { return cancelled; } }

        public int FailedCount { get { return failed; } }

        /// <summary> Builds whose prepare pass said no. Counted separately from failures because an unassigned spline is
        /// not an error and reporting it as one trains an author to ignore the report. </summary>
        public int RefusedCount { get { return refused; } }

        /// <summary> True when there is nothing running and nothing waiting -- which is the condition a test waits on
        /// and a batch pass checks before it looks at the result. </summary>
        public bool IsIdle { get { return !isGenerating && pending.Count == 0; } }

        /// <summary> How far along the current build says it is, 0 to 1. Written by the worker, read by the main thread,
        /// which is why it is a float in a volatile-guarded pair rather than a struct: a torn read of a progress figure
        /// is a wrong number in a progress bar, and a torn read of anything larger is a crash.
        ///
        /// It is advisory. A build that reports nothing sits at 0 until it finishes, which is honest -- the alternative
        /// is a bar that animates on a timer and tells the author a build is progressing when it has deadlocked.
        /// </summary>
        public float Progress
        {
            get { return progress; }
        }

        /// <summary> Called from the worker. Clamped here rather than at every call site, because a stage that computes
        /// its fraction from a span count will hand over 1.0000001 and a progress surface that trusts it will draw
        /// outside its own rectangle. </summary>
        public void Report(float fraction)
        {
            progress = fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
        }

        /// <summary> Called from the worker. True once the author has asked for this build to stop, so a long stage can
        /// check it between spans and unwind. Safe from any thread and safe when nothing is running. </summary>
        public bool StopRequested
        {
            get { StopToken s = currentStop; return s != null && s.StopRequested; }
        }
        #endregion


        #region "Queueing"
        /// <summary> Queue a build. Returns immediately; nothing runs until <see cref="Pump"/> is called on the main
        /// thread, which is what makes the caller's own stack unwind before any work starts. A request with neither a
        /// worker nor a step is rejected rather than queued -- a build with nothing to compute is a caller bug, and
        /// swallowing it would show as a road that never appears. </summary>
        public bool Enqueue(BuildRequest request)
        {
            if (request == null) { return false; }
            if (request.work == null && request.step == null) { return false; }
            request.id = ++nextId;
            pending.Enqueue(request);
            return true;
        }

        /// <summary> The common case, spelled out at the call site so the three phases are visible there. </summary>
        public bool Enqueue(string label, Func<bool> prepare, Action<StopToken> work, Action apply)
        {
            BuildRequest request = new BuildRequest();
            request.label = string.IsNullOrEmpty(label) ? "build" : label;
            request.prepare = prepare;
            request.work = work;
            request.apply = apply;
            return Enqueue(request);
        }

        /// <summary> Queue a build that pumps itself on the main thread -- a road, whose stages already alternate between
        /// worker and apply. <paramref name="step"/> is called once per <see cref="Pump"/> and returns true while it has
        /// more to do. </summary>
        public bool EnqueueStaged(string label, Func<bool> prepare, Func<bool> step, Action finish)
        {
            return EnqueueStaged(label, string.Empty, prepare, step, finish);
        }

        /// <summary> RM-101. The same, for a caller that has an owner to declare -- see
        /// <see cref="BuildRequest.owner"/>. Kept as an overload rather than a second method so there is one queueing
        /// path for a staged build: the four-argument form is this one with no owner. </summary>
        public bool EnqueueStaged(string label, string owner, Func<bool> prepare, Func<bool> step, Action finish)
        {
            BuildRequest request = new BuildRequest();
            request.label = string.IsNullOrEmpty(label) ? "build" : label;
            request.owner = string.IsNullOrEmpty(owner) ? string.Empty : owner;
            request.prepare = prepare;
            request.step = step;
            request.finish = finish;
            return Enqueue(request);
        }

        /// <summary> Ask the build in flight to stop, and drop everything still waiting.
        ///
        /// The queue is dropped rather than left because a cancel means "stop what I asked for", and a queue that
        /// carried on producing roads after the author pressed cancel is the behaviour that makes people stop trusting
        /// the button. Each dropped request still gets its <c>finish</c> call, on the main thread inside
        /// <see cref="Pump"/>, so a queued build that had already claimed something gives it back. </summary>
        public void Cancel()
        {
            StopToken s = currentStop;
            if (s != null) { s.RequestStop(); }
            while (pending.Count > 0)
            {
                BuildRequest dropped = pending.Dequeue();
                cancelled++;
                Record(dropped, BuildResult.Cancelled, string.Empty);
                RunFinish(dropped);
            }
        }


        /// <summary> RM-101. Cancel one build by id -- the Cancel button on one entry in the editor's background task
        /// panel, which must stop that build and leave the others alone. Returns true when the id was found.
        ///
        /// A build in flight is asked to stop and is NOT concluded here. The stop is cooperative: the worker unwinds at
        /// its next check and the ending runs from <see cref="Pump"/>, on the main thread, with the apply skipped.
        /// Concluding it here would run the teardown while a worker was still inside the build, which is the same class
        /// of bug as the legacy <c>Thread.Abort</c> path this section removed. A build still waiting is dropped
        /// outright, and still gets its finish, because it may already have claimed a terrain opening. </summary>
        public bool Cancel(int id)
        {
            if (id <= 0) { return false; }

            BuildRequest running = current;
            if (isGenerating && running != null && running.id == id)
            {
                StopToken s = currentStop;
                if (s != null) { s.RequestStop(); }
                return true;
            }

            int waiting = pending.Count;
            if (waiting == 0) { return false; }

            // Rotate the queue rather than rebuild it, so the survivors keep their order -- an author who cancels the
            // third of five builds must not find the remaining four reordered.
            bool found = false;
            for (int i = 0; i < waiting; i++)
            {
                BuildRequest queued = pending.Dequeue();
                if (!found && queued != null && queued.id == id)
                {
                    found = true;
                    cancelled++;
                    Record(queued, BuildResult.Cancelled, string.Empty);
                    RunFinish(queued);
                    continue;
                }
                pending.Enqueue(queued);
            }
            return found;
        }


        /// <summary> RM-101. Replace whatever is already building or queued for this owner with the request about to be
        /// queued, and say how many were replaced. Matching is on <see cref="BuildRequest.owner"/>, falling back to the
        /// label when a request declared none, so two roads that happen to share a name are still two owners.
        ///
        /// The case is an author dragging a width slider while the road is building: every change asks for a build, and
        /// without this the queue fills with builds of the same road whose results are each thrown away by the next one.
        /// The author waits for all of them and sees only the last. Worse for the progress panel, which the host solves
        /// the same way in <c>AsyncProgressUI.Track</c>: several entries for one object stack up, so the panel says a
        /// road is being built four times when it is being built once.
        ///
        /// Queued duplicates are dropped and get their finish. A duplicate in flight is only asked to stop -- for the
        /// same reason <see cref="Cancel(int)"/> only asks: concluding it here would run the teardown while a worker was
        /// still inside the build. It ends from the pump, applies nothing, and the replacement starts after it.
        ///
        /// A superseded build is recorded as <see cref="BuildResult.Cancelled"/> rather than under a result of its own.
        /// It produced no road, which is what cancelled means here, and a fifth result would oblige every reader to
        /// handle one more case in order to draw the same grey entry. </summary>
        public int Supersede(string owner)
        {
            if (string.IsNullOrEmpty(owner)) { return 0; }

            int replaced = 0;

            BuildRequest running = current;
            if (isGenerating && running != null && OwnerOf(running) == owner)
            {
                StopToken s = currentStop;
                if (s != null) { s.RequestStop(); }
                replaced++;
            }

            int waiting = pending.Count;
            for (int i = 0; i < waiting; i++)
            {
                BuildRequest queued = pending.Dequeue();
                if (queued != null && OwnerOf(queued) == owner)
                {
                    replaced++;
                    cancelled++;
                    Record(queued, BuildResult.Cancelled, string.Empty);
                    RunFinish(queued);
                    continue;
                }
                pending.Enqueue(queued);
            }

            return replaced;
        }


        /// <summary> RM-101. The identity to group a request under: its owner, or its label when it declared none.
        /// One place, so a supersede and a ticket can never disagree about what "the same thing" is. </summary>
        private static string OwnerOf(BuildRequest request)
        {
            if (request == null) { return string.Empty; }
            return string.IsNullOrEmpty(request.owner) ? request.label : request.owner;
        }
        #endregion


        #region "Watching from outside"
        /// <summary> RM-101. Fill <paramref name="into"/> with what is running and what is waiting, running build
        /// first. The caller owns the list and it is cleared here, so a progress surface allocates once for the session
        /// and re-reads it every frame without producing garbage.
        ///
        /// A snapshot rather than an event, because the reporting surface is an editor class that cannot be referenced
        /// from this assembly -- the same split the host keeps between <c>UniverseCreatorObject</c> and
        /// <c>AsyncProgressUI</c>. Pull scales down to nothing when nobody is watching; push would need this file to
        /// know that a watcher exists. </summary>
        public void CopyTickets(List<BuildTicket> into)
        {
            if (into == null) { return; }
            into.Clear();

            BuildRequest running = current;
            if (isGenerating && running != null)
            {
                BuildTicket ticket = new BuildTicket();
                ticket.id = running.id;
                ticket.label = running.label;
                ticket.owner = OwnerOf(running);
                ticket.isRunning = true;
                ticket.progress = progress;
                into.Add(ticket);
            }

            foreach (BuildRequest queued in pending)
            {
                if (queued == null) { continue; }
                BuildTicket ticket = new BuildTicket();
                ticket.id = queued.id;
                ticket.label = queued.label;
                ticket.owner = OwnerOf(queued);
                ticket.isRunning = false;
                ticket.progress = 0f;
                into.Add(ticket);
            }
        }


        /// <summary> RM-101. Take the next finished build's outcome, oldest first. Returns false when there is nothing
        /// waiting. Taking removes it, so two watchers would each see half -- which is deliberate: one surface reports
        /// progress, and a second one wanting the same information should read the counters instead of racing for the
        /// notifications. </summary>
        public bool TryTakeOutcome(out FinishedBuild outcome)
        {
            if (outcomes.Count == 0)
            {
                outcome = new FinishedBuild();
                return false;
            }
            outcome = outcomes.Dequeue();
            return true;
        }


        /// <summary> How many finished builds have not been collected. </summary>
        public int PendingOutcomeCount { get { return outcomes.Count; } }


        private void Record(BuildRequest request, BuildResult result, string message)
        {
            if (request == null) { return; }

            FinishedBuild outcome = new FinishedBuild();
            outcome.id = request.id;
            outcome.label = request.label;
            outcome.result = result;
            outcome.message = message == null ? string.Empty : message;

            while (outcomes.Count >= outcomeCapacity) { outcomes.Dequeue(); }
            outcomes.Enqueue(outcome);
        }
        #endregion


        #region "The pump"
        /// <summary> Advance the dispatcher. Call from the main thread, every frame, from wherever the caller's frames
        /// come from -- <c>EditorApplication.update</c> in the editor, an <c>Update</c> in a player. Returns true while
        /// there is anything left to do, so a caller that wants to spin to completion can.
        ///
        /// One phase transition per call at most. That is not laziness: applying a finished build and starting the next
        /// one in the same frame is how a "background" build turns back into a blocking one when a queue is long, and
        /// the author would see the editor freeze again with a different implementation underneath. </summary>
        public bool Pump()
        {
            if (isGenerating)
            {
                if (currentStep != null) { return PumpStep(); }
                Task t = currentTask;
                if (t == null || !t.IsCompleted) { return true; }
                FinishCurrent(t);
                return !IsIdle;
            }

            if (pending.Count == 0) { return false; }
            StartNext();
            return true;
        }

        /// <summary> One frame of a build that pumps itself. A throw is caught rather than allowed out, because the
        /// caller is <c>EditorApplication.update</c> and an exception there is swallowed by the editor with the build
        /// left half-started and <see cref="IsGenerating"/> stuck true for the rest of the session. </summary>
        private bool PumpStep()
        {
            Func<bool> s = currentStep;
            bool more;
            try
            {
                more = s != null && s();
            }
            catch (Exception e)
            {
                Conclude(current, e, false);
                return !IsIdle;
            }

            StopToken stop = currentStop;
            if (stop != null && stop.StopRequested)
            {
                Conclude(current, null, true);
                return !IsIdle;
            }

            if (more) { return true; }
            Conclude(current, null, false);
            return !IsIdle;
        }

        /// <summary> Pump until idle, with a bound. For a batch pass and for a gate, neither of which has frames.
        /// Returns true if it reached idle. The bound is in pump calls rather than seconds because a test that fails
        /// on a slow machine and passes on a fast one is worse than no test. </summary>
        public bool PumpUntilIdle(int maximumPumps)
        {
            int limit = maximumPumps > 0 ? maximumPumps : 1;
            for (int i = 0; i < limit; i++)
            {
                if (!Pump() && IsIdle) { return true; }
                Task t = currentTask;
                if (isGenerating && t != null && !t.IsCompleted)
                {
                    // Wait, but do not let a failing worker throw out of the pump. Task.Wait rethrows what the worker
                    // threw, which here would escape past the dispatcher's own failure handling and lose the build's
                    // finish call -- the exception is picked up from t.IsFaulted on the next pump instead.
                    try { t.Wait(); }
                    catch (AggregateException) { }
                }
            }
            return IsIdle;
        }

        private void StartNext()
        {
            BuildRequest request = pending.Dequeue();
            current = request;
            failure = null;
            progress = 0f;

            // Main thread. Read the Unity objects and copy out what the worker needs -- this is the only chance.
            phase = BuildPhase.Preparing;
            bool go;
            try
            {
                go = request.prepare == null || request.prepare();
            }
            catch (Exception e)
            {
                failure = e;
                phase = BuildPhase.Failed;
                failed++;
                Record(request, BuildResult.Failed, e.GetBaseException().Message);
                RunFinish(request);
                return;
            }

            if (!go)
            {
                phase = BuildPhase.Idle;
                refused++;
                Record(request, BuildResult.Refused, string.Empty);
                RunFinish(request);
                return;
            }

            // Local copies so the closure captures values rather than fields the next Enqueue is about to overwrite.
            StopToken stop = new StopToken();
            Action<StopToken> work = request.work;
            currentStop = stop;
            isGenerating = true;
            phase = BuildPhase.Working;

            if (work == null)
            {
                // Staged: the build drives itself from Pump, on the main thread, because its own stages already
                // alternate between a worker and an apply.
                currentStep = request.step;
                currentTask = null;
                return;
            }

            currentStep = null;
            currentTask = Task.Run(() => work(stop));
        }

        private void FinishCurrent(Task t)
        {
            Exception thrown = null;
            if (t.IsFaulted)
            {
                // Unwrap once: Task.Run wraps whatever the worker threw in an AggregateException, and the inner
                // exception is the one with the author's stack trace in it.
                AggregateException agg = t.Exception;
                thrown = agg != null && agg.InnerException != null ? agg.InnerException : (Exception)agg;
            }
            bool stopped = t.IsCanceled || (currentStop != null && currentStop.StopRequested);
            Conclude(current, thrown, stopped);
        }

        /// <summary> The one ending, shared by the worker path and the staged path. Runs the apply only when the build
        /// neither threw nor was stopped, then always runs the finish. </summary>
        private void Conclude(BuildRequest request, Exception thrown, bool stopped)
        {
            isGenerating = false;
            currentTask = null;
            currentStep = null;
            currentStop = null;

            if (thrown != null)
            {
                failure = thrown;
                phase = BuildPhase.Failed;
                failed++;
                Record(request, BuildResult.Failed, thrown.GetBaseException().Message);
                RunFinish(request);
                return;
            }

            if (stopped)
            {
                // Nothing is applied. That is the all-or-nothing clause: no partial road, no orphaned children, no
                // modified terrain -- because the apply is where all three of those would have happened.
                phase = BuildPhase.Cancelled;
                cancelled++;
                Record(request, BuildResult.Cancelled, string.Empty);
                RunFinish(request);
                return;
            }

            phase = BuildPhase.Applying;
            try
            {
                if (request != null && request.apply != null) { request.apply(); }
                phase = BuildPhase.Complete;
                progress = 1f;
                completed++;
                Record(request, BuildResult.Completed, string.Empty);
            }
            catch (Exception e)
            {
                failure = e;
                phase = BuildPhase.Failed;
                failed++;
                Record(request, BuildResult.Failed, e.GetBaseException().Message);
            }
            RunFinish(request);
        }

        /// <summary> Give back whatever the build was holding, whatever happened. A throw here is swallowed after being
        /// recorded, because a teardown that throws must not stop the NEXT build's teardown from running -- that is how
        /// one failed build leaves the tool holding terrain openings for the rest of the session. </summary>
        private void RunFinish(BuildRequest request)
        {
            if (request == null || request.finish == null) { return; }
            try
            {
                request.finish();
            }
            catch (Exception e)
            {
                if (failure == null) { failure = e; }
            }
        }
        #endregion


        #region "Describing it"
        public string StatusSentence()
        {
            if (IsIdle && phase == BuildPhase.Idle) { return "idle"; }
            string what = string.IsNullOrEmpty(Label) ? "a build" : Label;
            switch (phase)
            {
                case BuildPhase.Preparing: return "reading settings for " + what;
                case BuildPhase.Working: return "building " + what + " in the background";
                case BuildPhase.Applying: return "installing " + what;
                case BuildPhase.Cancelled: return what + " was cancelled; nothing was applied";
                case BuildPhase.Failed:
                    return what + " failed: " + (failure != null ? failure.Message : "no reason recorded");
                case BuildPhase.Complete:
                    return pending.Count > 0
                        ? what + " done, " + pending.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " waiting"
                        : what + " done";
            }
            return "idle";
        }
        #endregion
    }
}

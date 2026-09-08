// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.City;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-101. Reports the build queue into Unity's own background task area, at the bottom right of the
    /// editor, and routes that panel's Cancel button back into the build.
    ///
    /// This is modelled on the host's <c>AsyncProgressUI</c> deliberately, because that small class encodes four bugs
    /// already paid for in this exact engine version:
    ///
    /// 1. <c>UnityEditor.Progress</c> is editor-only, so it cannot live beside the build code without breaking player
    ///    builds. Hence the split: <see cref="BuildDispatch"/> exposes tickets and outcomes with no
    ///    <c>UnityEditor</c> reference anywhere in it, and this file -- which is in the editor assembly and cannot ship
    ///    -- polls them.
    /// 2. A worker thread may not touch the <c>Progress</c> API at all. So completion is detected here, on the main
    ///    thread, from <c>EditorApplication.update</c>, rather than reported from inside the task.
    /// 3. Progress ids do not survive a domain reload and finishing an unknown id throws, so every finish goes through
    ///    <see cref="FinishSafe"/>'s <c>Progress.Exists</c> guard.
    /// 4. Re-registering without retiring the previous entry stacks bars in the panel. Entries are keyed by
    ///    <see cref="BuildTicket.id"/>, which is per build rather than per road, and grouped by
    ///    <see cref="BuildTicket.owner"/>: a newer build for the same owner retires the older one's item, so a road
    ///    edited three times while it builds shows one entry, not three at once.
    ///
    /// What replaced what: the tool used to raise <c>EditorUtility.DisplayProgressBar</c> from inside the build loop.
    /// That bar is MODAL -- it takes the editor's input and repaints nothing else -- so it was not a progress report,
    /// it was the freeze the author complained about, wearing the costume of a progress report. It needed clearing on
    /// every exit path, which is why <c>ClearProgressBar</c> was called from five places. There is no teardown here at
    /// all: an entry is finished from the outcome the dispatcher recorded, so there is no exit path to forget. </summary>
    [InitializeOnLoad]
    public static class BuildProgressReporter
    {
        #region "State"
        private sealed class Entry
        {
            public int buildId;
            public string owner;
            public int progressId;
            public bool wasRunning;

            /// <summary> RM-101. True once a newer build for the same owner arrived and this entry's item was finished.
            /// The entry stays in the list rather than being removed, so the ticket for this build -- still queued for a
            /// frame or two while the dispatcher unwinds it -- is recognised as this one and does not open a second
            /// item. It leaves when its outcome arrives, like any other. </summary>
            public bool retired;
        }

        /// <summary> One per open progress item. A list rather than a dictionary because it holds as many items as
        /// there are queued builds -- single digits -- and it is walked in full every editor frame, which a list does
        /// without allocating an enumerator. </summary>
        private static readonly List<Entry> shown = new List<Entry>();

        /// <summary> Reused across frames. <see cref="BuildDispatch.CopyTickets"/> clears and refills it, so polling
        /// the queue sixty times a second allocates nothing. </summary>
        private static readonly List<BuildTicket> tickets = new List<BuildTicket>();
        #endregion


        static BuildProgressReporter()
        {
            // Named method, not a lambda: -= on an anonymous delegate removes a different instance than += added and
            // therefore removes nothing, which is how an editor accumulates one dead subscription per domain reload.
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }


        #region "The poll"
        /// <summary> Once per editor frame, in three passes and in this order, because the order is what stops an entry
        /// being reaped one frame before the outcome that explains it arrives. </summary>
        private static void Poll()
        {
            BuildDispatch dispatch = BuildDispatch.Shared;

            // 1. Close the entries whose builds have ended, with the status the dispatcher actually recorded. This is
            //    first so that a build which finished since the last frame is reported as what it was, rather than
            //    being noticed as "no longer in the queue" by pass 3 and written off as cancelled.
            FinishedBuild outcome;
            while (dispatch.TryTakeOutcome(out outcome)) { Close(outcome); }

            // 2. Open an entry for anything queued or running that has none, and report where it is. The index is passed
            //    because it IS the answer to "where is it": CopyTickets hands them over in queue order, so 0 is the head
            //    and there is nothing in front of it.
            dispatch.CopyTickets(tickets);
            for (int i = 0; i < tickets.Count; i++) { Show(tickets[i], i); }

            // 3. Reap. Only when the dispatcher holds nothing at all, running, queued or uncollected -- at which point
            //    an entry we are still holding cannot be explained by anything still to come, so it is stale: a
            //    ResetShared, or a notification dropped because sixty-four of them went uncollected during a batch
            //    pass. Cancelled rather than succeeded, because we do not know that it succeeded.
            if (shown.Count > 0 && dispatch.IsIdle && dispatch.PendingOutcomeCount == 0)
            {
                for (int i = shown.Count - 1; i >= 0; i--)
                {
                    FinishSafe(shown[i].progressId, Progress.Status.Canceled);
                    shown.RemoveAt(i);
                }
            }
        }
        #endregion


        #region "One entry"
        /// <summary> Open the entry for this ticket if it has none, then say where the build is. A waiting build gets an
        /// entry immediately rather than when its turn comes, because an author who clicked Refresh on five roads asked
        /// for five builds and a panel showing one of them looks like four clicks were lost. </summary>
        private static void Show(BuildTicket ticket, int queuePosition)
        {
            Entry entry = Find(ticket.id);
            if (entry == null)
            {
                // RM-101. One object, one item. A newer build for this owner retires the older one's item here rather
                // than adding to it, so an author who edits a road four times while it builds ends with one entry
                // reaching 100% instead of a column of four saying the same road is being built four times.
                RetireOlder(ticket.owner, ticket.id);

                entry = new Entry();
                entry.buildId = ticket.id;
                entry.owner = ticket.owner;
                entry.progressId = Progress.Start(InspectorFieldRegistry.ProductDisplayName,
                    Label(ticket), Progress.Options.None);
                entry.wasRunning = false;

                // The panel's Cancel button routes into the dispatcher by id, so cancelling the third of five builds
                // stops that one and leaves the rest queued. Returning true tells Unity the request was accepted; the
                // stop itself is cooperative and the build ends from the pump, on the main thread, with its apply
                // skipped -- which is what makes cancel leave the terrain unmodified rather than half written.
                int buildId = ticket.id;
                Progress.RegisterCancelCallback(entry.progressId, delegate
                {
                    BuildDispatch.Shared.Cancel(buildId);
                    return true;
                });

                shown.Add(entry);
            }

            // A retired build is on its way out -- superseded, and being unwound by the dispatcher. It has no item left
            // to report into, and re-opening one would undo the retire a frame after it happened.
            if (entry.retired) { return; }

            if (!Progress.Exists(entry.progressId)) { return; }

            if (!ticket.isRunning)
            {
                // RM-109. B-8. Say which of the two waits this is, because they mean opposite things. A build with
                // others in front of it is waiting normally and the count is the useful part. A build at the HEAD of the
                // queue has nothing in front of it: if it is still not running, the pump is not being called, and the
                // old message -- "waiting for the build in front of it", printed for every ticket including the first --
                // blamed a build that did not exist while the real fault was that nothing was driving the queue at all.
                //
                // -1 is Unity's indeterminate bar. Correct for a build that has not started: a 0% bar claims the build
                // is running and getting nowhere.
                string wait = queuePosition > 0
                    ? "queued -- " + queuePosition.ToString() + (queuePosition == 1 ? " build" : " builds") + " ahead"
                    : "next to build";
                Progress.Report(entry.progressId, -1f, wait);
                return;
            }

            if (!entry.wasRunning)
            {
                Progress.SetDescription(entry.progressId, "building");
                entry.wasRunning = true;
            }
            Progress.Report(entry.progressId, ticket.progress);
        }


        /// <summary> Close the entry for a build that has ended. A failure logs its base message ONCE, here, rather than
        /// from the pump -- which runs every frame and would have written the same line into the console for as long as
        /// the author left the editor open. </summary>
        private static void Close(FinishedBuild outcome)
        {
            Entry entry = Find(outcome.id);
            if (entry == null) { return; }

            shown.Remove(entry);

            // RM-109. B-2. A retired entry's item was REMOVED when the newer build superseded this one, so there is no
            // item left to give a status to. Returning here is what stops the outcome the dispatcher still reports for a
            // superseded build -- BuildResult.Cancelled, correctly, from its point of view -- from re-opening a red row
            // for a build the author never cancelled and never saw start.
            if (entry.retired) { return; }

            switch (outcome.result)
            {
                case BuildResult.Completed:
                    FinishSafe(entry.progressId, Progress.Status.Succeeded);
                    break;

                case BuildResult.Cancelled:
                    FinishSafe(entry.progressId, Progress.Status.Canceled);
                    break;

                case BuildResult.Refused:
                    // Not a failure. An unassigned spline or a road with fewer than two nodes is a refusal, and showing
                    // it in red teaches an author to ignore the panel.
                    Describe(entry.progressId, "nothing to build");
                    FinishSafe(entry.progressId, Progress.Status.Canceled);
                    break;

                default:
                    Describe(entry.progressId, outcome.message);
                    FinishSafe(entry.progressId, Progress.Status.Failed);
                    Debug.LogError(InspectorFieldRegistry.ProductDisplayName + ": " + Name(outcome.label)
                        + " failed -- " + outcome.message);
                    break;
            }
        }
        #endregion


        #region "Small parts"
        private static Entry Find(int buildId)
        {
            for (int i = 0; i < shown.Count; i++)
            {
                if (shown[i].buildId == buildId) { return shown[i]; }
            }
            return null;
        }


        /// <summary> RM-101. Retire the item of every entry for this owner whose build is older than
        /// <paramref name="keepBuildId"/>, and mark it retired.
        ///
        /// RM-109, B-2. REMOVED, not finished as cancelled. A superseded build is not a build that was stopped; it is a
        /// build that no longer exists, because a newer request for the same road replaced it before it ran. Reporting
        /// that as `Cancelled` put a red row in Background Tasks for every slider drag and node move, left it there
        /// until the author found *Clear Inactive*, and made coalescing -- the feature that stops forty builds queueing
        /// behind one road -- read as the tool aborting the author's work over and over. `Cancelled` is now reserved for
        /// a stop the author actually asked for, through the panel's own X.
        ///
        /// Only OLDER ids, because build ids only ever rise -- so this cannot turn into two entries retiring each other
        /// on alternate frames, which is what a plain "any other entry for this owner" test would do if a caller ever
        /// queued a second build without superseding the first. </summary>
        private static void RetireOlder(string owner, int keepBuildId)
        {
            if (string.IsNullOrEmpty(owner)) { return; }

            for (int i = 0; i < shown.Count; i++)
            {
                Entry entry = shown[i];
                if (entry.retired) { continue; }
                if (entry.buildId >= keepBuildId) { continue; }
                if (entry.owner != owner) { continue; }

                RemoveSafe(entry.progressId);
                entry.retired = true;
            }
        }


        /// <summary> Take an item out of the panel altogether, as opposed to finishing it with a status. Unity keeps a
        /// finished item visible until *Clear Inactive*; a removed one leaves nothing behind, which is what a build that
        /// was replaced before it ran should leave. Finish first when the item is still live, because Unity will not
        /// remove a running item -- through <see cref="FinishSafe"/> rather than directly, so that there is one place in
        /// this file that finishes an item and one place that carries the existence check it needs. That also means a
        /// finish that throws no longer abandons the removal: the state being reached here is "the item is gone", and the
        /// remove below is the half that actually reaches it. </summary>
        private static void RemoveSafe(int progressId)
        {
            try
            {
                if (!Progress.Exists(progressId)) { return; }
                if (Progress.GetStatus(progressId) == Progress.Status.Running)
                {
                    FinishSafe(progressId, Progress.Status.Canceled);
                }
                if (Progress.Exists(progressId)) { Progress.Remove(progressId); }
            }
            catch (System.Exception)
            {
                // Nothing actionable: the item is already gone, which is the state this was trying to reach.
            }
        }


        private static string Label(BuildTicket ticket)
        {
            return Name(ticket.label);
        }


        private static string Name(string label)
        {
            return string.IsNullOrEmpty(label) ? "build" : label;
        }


        private static void Describe(int progressId, string text)
        {
            if (string.IsNullOrEmpty(text)) { return; }
            if (!Progress.Exists(progressId)) { return; }
            Progress.SetDescription(progressId, text);
        }


        /// <summary> Progress ids go stale across a domain reload and finishing an unknown one throws, so the existence
        /// check is not defensive padding -- it is the difference between a reload during a build being invisible and a
        /// reload during a build filling the console. </summary>
        private static void FinishSafe(int progressId, Progress.Status status)
        {
            try
            {
                if (Progress.Exists(progressId)) { Progress.Finish(progressId, status); }
            }
            catch (System.Exception)
            {
                // Nothing actionable: the item is already gone, which is the state this was trying to reach.
            }
        }
        #endregion
    }
}
#endif

#region "Imports"
using System;
using System.Threading.Tasks;
using UniverseCreator.Nodes;
#endregion


namespace RoadCityMaster.Threading
{
    /// <summary> RM-100. The base class the road builder's three calculation stages run on, moved onto the host's
    /// threading contract.
    ///
    /// The shape of this class is unchanged on purpose -- <c>Start</c>, <c>IsDone</c>, <c>Update</c>,
    /// <c>ThreadFunction</c>, <c>OnFinished</c> and <c>Abort</c> all still mean what they meant, because
    /// <see cref="TerrainCalcs"/>, <see cref="RoadCalcs1"/> and <see cref="RoadCalcs2"/> derive from it and rewriting
    /// three working calculation stages to change how they are STARTED would be changing the road mathematics to fix a
    /// threading bug. What changed is everything underneath:
    ///
    /// * It was a raw <c>System.Threading.Thread</c>. It is now <c>Task.Run</c>, which is what
    ///   <c>UniverseCreatorObject.RegenerateAsync</c> uses. Two systems in one project with two threading models is how
    ///   a deadlock arrives during a linked build, and the pool also stops a scene of forty roads from asking the OS for
    ///   forty threads.
    ///
    /// * <c>Abort</c> called <c>Thread.Abort</c>, which throws <c>PlatformNotSupportedException</c> on every runtime
    ///   Unity 6 ships. Every existing call site would therefore have thrown -- and they are all reached, because the
    ///   builder calls <c>Abort</c> on a job it has just seen FINISH, using it as "dispose" rather than as "kill". So
    ///   <c>Abort</c> is now cooperative: it requests a stop and returns. A finished job is unaffected, which is what
    ///   those call sites always meant, and a running one unwinds at its next check instead of being torn down in the
    ///   middle of writing an array.
    ///
    /// * The stop is the host's own <see cref="StopToken"/> rather than a second cancellation type invented here. A
    ///   linked build that has to be abandoned is abandoned by one mechanism from the graph node down to the road mesh.
    ///
    /// * An exception used to be lost. On a raw thread it killed the thread with <c>IsDone</c> still false, so the
    ///   builder sat in its pump until the 180-second guess expired and reported a timeout for what was a null
    ///   reference. It is now caught, kept in <see cref="Failure"/>, and <c>IsDone</c> is still set -- so the stage
    ///   completes, the pump advances, and the caller can report the actual fault. </summary>
    public class ThreadedJob
    {
        #region "State"
        private readonly object handle = new object();
        private bool isDone = false;
        private Task task = null;
        private StopToken stop = null;
        private Exception failure = null;

        /// <summary> Set once the worker has stopped running, whether it finished the work, was asked to stop, or threw.
        /// It has always meant "there is nothing more to wait for" rather than "it succeeded", and the pump in
        /// <c>Road.EditorUpdate</c> reads it that way. </summary>
        public bool IsDone
        {
            get { lock (handle) { return isDone; } }
            set { lock (handle) { isDone = value; } }
        }

        /// <summary> What the worker threw, or null. Read after <see cref="IsDone"/> on the main thread. </summary>
        public Exception Failure
        {
            get { lock (handle) { return failure; } }
        }

        public bool HasFailed
        {
            get { return Failure != null; }
        }

        /// <summary> The task, so a caller that must genuinely wait can await or join it rather than spin. Null before
        /// <see cref="Start"/>. Exposed for the same reason the host exposes <c>CurrentTask</c>: the editor progress
        /// surface has to be able to see the work without owning it. </summary>
        public Task Task
        {
            get { return task; }
        }

        /// <summary> True once somebody has asked this job to stop. A long stage should check it between spans and
        /// return early; a stage that does not check it simply runs to the end, which is the old behaviour and is safe.
        /// </summary>
        public bool StopRequested
        {
            get { StopToken s = stop; return s != null && s.StopRequested; }
        }
        #endregion


        #region "Running"
        /// <summary> Start the worker. The caller is on the main thread and has already snapshotted everything the
        /// stage needs -- that is the <c>Setup</c> call each subclass takes -- because from here on the stage may not
        /// touch a <c>UnityEngine.Object</c>. </summary>
        public virtual void Start()
        {
            Start(null);
        }

        /// <summary> Start under a stop token the caller already owns, so one cancel can reach a whole build rather
        /// than one stage of it. A null token gets a private one, which keeps <see cref="Abort"/> working for callers
        /// that do not have a build-wide token. </summary>
        public virtual void Start(StopToken sharedStop)
        {
            lock (handle)
            {
                isDone = false;
                failure = null;
                stop = sharedStop != null ? sharedStop : new StopToken();
            }
            task = Task.Run((Action)Run);
        }

        /// <summary> Ask the worker to stop and return immediately.
        ///
        /// This does not block and does not tear anything down. A job that has already finished is unchanged, which is
        /// how every existing call site uses it. A job still running sees the request at its next
        /// <see cref="StopRequested"/> check, or runs to completion if it makes none -- and either way the result is
        /// discarded by the caller that aborted it, so a stage without checks costs time and never correctness.
        /// </summary>
        public virtual void Abort()
        {
            StopToken s = stop;
            if (s != null) { s.RequestStop(); }
        }

        protected virtual void ThreadFunction() { }

        protected virtual void OnFinished() { }

        /// <summary> Pump from the main thread. Returns true exactly once the worker has stopped, and calls
        /// <see cref="OnFinished"/> on the main thread before saying so -- which is where a stage is allowed to touch
        /// Unity again. </summary>
        public virtual bool Update()
        {
            if (IsDone)
            {
                OnFinished();
                return true;
            }
            return false;
        }

        private void Run()
        {
            try
            {
                ThreadFunction();
            }
            catch (Exception e)
            {
                lock (handle) { failure = e; }
            }
            finally
            {
                IsDone = true;
            }
        }
        #endregion
    }
}

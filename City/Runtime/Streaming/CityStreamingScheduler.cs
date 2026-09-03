#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The book-keeping half of RM-17's answer to the load spike: a FIFO queue of building indices a
    /// loader must instantiate, drained a bounded number PER FRAME. Streaming a chunk of a hundred buildings in
    /// one frame is the hitch the gate tests for; the loader enqueues the chunk's indices here and calls
    /// <see cref="Pump"/> once a frame with the AUTHORED per-frame budget, so the hundred instantiations spread
    /// over several frames instead of stalling one.
    ///
    /// This type owns ONLY the scheduling -- which indices come out, in what order, at what rate. Actually
    /// instantiating a prefab is the scene work the loader does with each returned index; that is the
    /// author-confirmed remainder, not something a headless gate can assert. The scheduling itself is pure and
    /// deterministic: FIFO, every enqueued index emerges exactly once, and a budget of B drains N items in
    /// ceil(N / B) pumps -- all provable without a scene (RM-70). </summary>
    public sealed class CityStreamingScheduler
    {
        private readonly Queue<int> pending = new Queue<int>();

        /// <summary> How many indices are still waiting to be instantiated. </summary>
        public int PendingCount { get { return pending.Count; } }

        /// <summary> True when everything enqueued has been drained. </summary>
        public bool IsComplete { get { return pending.Count == 0; } }

        /// <summary> Queue one building index for instantiation. </summary>
        public void Enqueue(int buildingIndex)
        {
            pending.Enqueue(buildingIndex);
        }

        /// <summary> Queue a batch of indices in order (e.g. a chunk's resident set). Null is a no-op. </summary>
        public void EnqueueRange(IList<int> buildingIndices)
        {
            if (buildingIndices == null) { return; }
            for (int i = 0; i < buildingIndices.Count; i++)
            {
                pending.Enqueue(buildingIndices[i]);
            }
        }

        /// <summary> Drain up to <paramref name="budget"/> indices for this frame and return them in FIFO order.
        /// The loader instantiates one prefab per returned index. A budget of zero or less returns an empty list
        /// and drains nothing, so a paused loader never loses work. </summary>
        public List<int> Pump(int budget)
        {
            List<int> taken = new List<int>();
            if (budget <= 0) { return taken; }
            while (taken.Count < budget && pending.Count > 0)
            {
                taken.Add(pending.Dequeue());
            }
            return taken;
        }

        /// <summary> Forget all pending work -- used when the camera jumps and the resident set is recomputed
        /// from scratch rather than diffed. </summary>
        public void Clear()
        {
            pending.Clear();
        }
    }
}

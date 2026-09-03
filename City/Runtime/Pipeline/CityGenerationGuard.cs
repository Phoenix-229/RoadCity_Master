#region "Imports"
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The runtime half of § 10.3's ruling, made assertable: GENERATION NEVER RUNS AT PLAY TIME.
    /// A baked city already carries its data (RM-08 serialises the derived geometry), so at play time there
    /// is nothing to generate -- only a loader. This guard is the single gate every city generation entry
    /// point calls before doing work; in play mode it is closed, and any attempt to generate is refused and
    /// RECORDED so RM-20's / RM-70's self-test can assert the second half of the gate ("no generator ran")
    /// rather than assume it.
    ///
    /// The flag is a gate, not a generation input: it decides WHETHER generation runs, never WHAT it
    /// produces, so it does not touch determinism (C-06). The editor bake command opens the gate; a runtime
    /// loader closes it on entering play mode. </summary>
    public static class CityGenerationGuard
    {
        /// <summary> Whether a generation entry point may run. True in the editor (where baking and
        /// authoring generate freely); a runtime loader sets it false on entering play mode. </summary>
        public static bool GenerationAllowed = true;

        /// <summary> How many times generation was refused since the last <see cref="EnterPlayMode"/> --
        /// the self-test asserts this is zero after a play-mode pass over a baked city. </summary>
        public static int BlockedAttempts { get; private set; }

        /// <summary> The context string of the most recent refused attempt, for the self-test's report. </summary>
        public static string LastBlockedContext { get; private set; }

        /// <summary> Close the gate for play mode and reset the counters. A runtime loader calls this. </summary>
        public static void EnterPlayMode()
        {
            GenerationAllowed = false;
            BlockedAttempts = 0;
            LastBlockedContext = null;
        }

        /// <summary> Open the gate for edit-time authoring and baking. </summary>
        public static void EnterEditMode()
        {
            GenerationAllowed = true;
        }

        /// <summary> A generation entry point calls this first. Returns true when it may proceed; returns
        /// false -- and records the attempt -- when the gate is closed, so the caller aborts instead of
        /// generating over a baked city at play time. </summary>
        public static bool RequireGenerationAllowed(string context)
        {
            if (GenerationAllowed)
            {
                return true;
            }
            BlockedAttempts++;
            LastBlockedContext = context;
            return false;
        }
    }
}

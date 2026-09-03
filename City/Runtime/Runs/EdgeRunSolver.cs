#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One resolved end of a run: what it is, how much length it eats, and -- for a barrier -- RM-60's
    /// full terminal (placement and all). <see cref="RunEndKind.None"/> with <see cref="refused"/> set is the only
    /// way an end can fail to resolve; a run carrying it is not built, so a bare end is never emitted. </summary>
    public struct RunEnd
    {
        public RunEndKind kind;
        public float lengthMetres;
        public bool refused;
        public string refusal;

        /// <summary> Valid only for a barrier family run: RM-60's terminal, so the placement, anti-sink height and
        /// outward facing are that code's, not a second copy. </summary>
        public BarrierTerminal barrier;
    }


    /// <summary> One fully terminated sub-run: a station interval with two ends and a quantised body. A segment is
    /// refused -- and never placed -- if either end could not be resolved, if it is shorter than its own two ends,
    /// or if its body could not be quantised without stretching a part. </summary>
    public struct EdgeRunSegment
    {
        public float startStationMetres;
        public float endStationMetres;
        public RunEnd startEnd;
        public RunEnd endEnd;
        public RunQuantisation body;
        public bool refused;
        public string refusal;

        public float LengthMetres { get { return endStationMetres - startStationMetres; } }
    }
    /// <summary> Everything one run needs to be solved: its type, how long it is, where it is interrupted, and --
    /// for a barrier -- the RM-60 site its terminals are sized against. Grouped so adding a condition does not touch
    /// every call site. </summary>
    public struct EdgeRunRequest
    {
        public EdgeRunType type;
        public float totalLengthMetres;
        public List<RunOpening> openings;

        /// <summary> The RM-60 site the barrier terminals are chosen from (room, backslope, speed, node centres).
        /// Ignored for fence and kerb runs, whose ends are typed lengths. </summary>
        public MedianSite barrierSite;
    }


    /// <summary> The whole answer for one run: the fully terminated sub-runs it split into, and whether any of them
    /// was refused. Built whole; nothing carries between two calls, so re-solving after moving an opening cannot
    /// leave a stale segment behind. </summary>
    public struct EdgeRunSolution
    {
        public List<EdgeRunSegment> segments;
        public bool refused;
        public string refusal;
        public string report;

        public int SegmentCount { get { return segments != null ? segments.Count : 0; } }
    }
    /// <summary> Turns one run into fully terminated sub-runs. Three real rules, run in order: an opening splits the
    /// run into gaps (segmentation); each gap gets a real end at both extremities or is refused (termination); the
    /// body between the ends is filled with whole parts by RM-76's quantiser (no stretch). Barrier ends are RM-60's
    /// -- <see cref="MedianContent.PlanTerminal"/> -- so a roadside guardrail, a bridge parapet and a median barrier
    /// all end through one mechanism; fence and kerb ends are the type's own authored set, and an empty set is a
    /// fail-closed refusal, not a bare panel. </summary>
    public static class EdgeRunSolver
    {
        private const float Epsilon = 1e-4f;

        public static EdgeRunSolution Solve(EdgeRunRequest request)
        {
            EdgeRunSolution solution = new EdgeRunSolution();
            solution.segments = new List<EdgeRunSegment>();
            solution.refusal = "";

            if (request.type == null || request.totalLengthMetres <= Epsilon)
            {
                solution.refused = true;
                solution.refusal = "the run has no type or no length.";
                solution.report = Report(request, solution);
                return solution;
            }

            List<Vector2> intervals = BuildSubRuns(request.totalLengthMetres, request.openings);
            for (int i = 0; i < intervals.Count; i++)
            {
                EdgeRunSegment seg = ResolveSegment(request, intervals[i].x, intervals[i].y, i, intervals.Count);
                solution.segments.Add(seg);
                if (seg.refused && !solution.refused)
                {
                    solution.refused = true;
                    solution.refusal = seg.refusal;
                }
            }
            solution.report = Report(request, solution);
            return solution;
        }

        /// <summary> The gaps between the openings, clipped to the run, overlapping openings merged. Each gap is a
        /// sub-run; the openings themselves carry no run. Returned as [start=x, end=y] intervals. </summary>
        private static List<Vector2> BuildSubRuns(float total, List<RunOpening> openings)
        {
            List<Vector2> holes = new List<Vector2>();
            if (openings != null)
            {
                for (int i = 0; i < openings.Count; i++)
                {
                    float a = Mathf.Max(0f, openings[i].startStationMetres);
                    float b = Mathf.Min(total, openings[i].endStationMetres);
                    if (b > a + Epsilon) { holes.Add(new Vector2(a, b)); }
                }
            }
            holes.Sort((p, q) => p.x.CompareTo(q.x));

            List<Vector2> merged = new List<Vector2>();
            for (int i = 0; i < holes.Count; i++)
            {
                if (merged.Count > 0 && holes[i].x <= merged[merged.Count - 1].y + Epsilon)
                {
                    Vector2 m = merged[merged.Count - 1];
                    if (holes[i].y > m.y) { m.y = holes[i].y; }
                    merged[merged.Count - 1] = m;
                }
                else { merged.Add(holes[i]); }
            }

            List<Vector2> result = new List<Vector2>();
            float cursor = 0f;
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].x > cursor + Epsilon) { result.Add(new Vector2(cursor, merged[i].x)); }
                if (merged[i].y > cursor) { cursor = merged[i].y; }
            }
            if (cursor < total - Epsilon) { result.Add(new Vector2(cursor, total)); }
            return result;
        }
        private static EdgeRunSegment ResolveSegment(EdgeRunRequest request, float start, float end, int index,
            int count)
        {
            EdgeRunSegment seg = new EdgeRunSegment();
            seg.startStationMetres = start;
            seg.endStationMetres = end;
            seg.refusal = "";
            float length = end - start;

            seg.startEnd = ResolveEnd(request, RoadEnd.Start, length);
            seg.endEnd = ResolveEnd(request, RoadEnd.End, length);

            if (seg.startEnd.refused || seg.endEnd.refused)
            {
                seg.refused = true;
                seg.refusal = seg.startEnd.refused ? seg.startEnd.refusal : seg.endEnd.refusal;
                return seg;
            }

            float minViable = seg.startEnd.lengthMetres + seg.endEnd.lengthMetres;
            if (length + Epsilon < minViable)
            {
                seg.refused = true;
                seg.refusal = "sub-run " + Int(index + 1) + " of " + Int(count) + " is " + Metres(length)
                    + ", shorter than its own two ends (" + Metres(seg.startEnd.lengthMetres) + " + "
                    + Metres(seg.endEnd.lengthMetres) + "). A run this short is not installed; remove it or "
                    + "lengthen it.";
                return seg;
            }

            float body = length - seg.startEnd.lengthMetres - seg.endEnd.lengthMetres;
            seg.body = EdgeRunQuantiser.Quantise(request.type, body);
            if (seg.body.refused)
            {
                seg.refused = true;
                seg.refusal = seg.body.refusal;
            }
            return seg;
        }

        private static RunEnd ResolveEnd(EdgeRunRequest request, RoadEnd whichEnd, float subRunLength)
        {
            if (request.type.family == EdgeRunFamily.Barrier)
            {
                return ResolveBarrierEnd(request, whichEnd, subRunLength);
            }
            return ResolveTypedEnd(request, whichEnd);
        }

        /// <summary> A barrier end is RM-60's terminal, sized against the site -- but the room a terminal has can
        /// never exceed the sub-run's own length, so a short sub-run whose flare will not fit is refused by RM-60's
        /// own ladder rather than by a second rule here. </summary>
        private static RunEnd ResolveBarrierEnd(EdgeRunRequest request, RoadEnd whichEnd, float subRunLength)
        {
            MedianSite site = request.barrierSite;
            float room = site.approachLengthMetres < subRunLength ? site.approachLengthMetres : subRunLength;
            site.approachLengthMetres = room > 0f ? room : 0f;
            BarrierTerminal terminal = MedianContent.PlanTerminal(site, whichEnd);

            RunEnd runEnd = new RunEnd();
            runEnd.barrier = terminal;
            runEnd.lengthMetres = terminal.lengthMetres;
            runEnd.refused = terminal.refused;
            runEnd.refusal = terminal.refusal;
            runEnd.kind = MapBarrierKind(terminal.treatment);
            return runEnd;
        }

        private static RunEndKind MapBarrierKind(TerminalTreatment treatment)
        {
            switch (treatment)
            {
                case TerminalTreatment.FlaredEnd: return RunEndKind.BarrierFlared;
                case TerminalTreatment.BuriedInBackslope: return RunEndKind.BarrierBuried;
                case TerminalTreatment.CrashCushion: return RunEndKind.BarrierCrashCushion;
                default: return RunEndKind.None;
            }
        }

        /// <summary> A fence or kerb end is the type's most-preferred authored option. An empty set fails closed:
        /// the run is refused, not ended mid-panel. </summary>
        private static RunEnd ResolveTypedEnd(EdgeRunRequest request, RoadEnd whichEnd)
        {
            RunEnd runEnd = new RunEnd();
            EdgeRunType type = request.type;
            if (type.allowedEnds == null || type.allowedEnds.Count == 0)
            {
                runEnd.kind = RunEndKind.None;
                runEnd.refused = true;
                runEnd.refusal = "the " + type.displayName + " run type has no end treatments defined, so its ends "
                    + "cannot be resolved. A run of this type must not end mid-part; author at least one end "
                    + "treatment for it.";
                return runEnd;
            }
            RunEndOption chosen = type.allowedEnds[0];
            runEnd.kind = chosen.kind;
            runEnd.lengthMetres = chosen.lengthMetres;
            return runEnd;
        }
        private static string Report(EdgeRunRequest request, EdgeRunSolution solution)
        {
            string name = request.type != null ? request.type.displayName : "run";
            int n = solution.SegmentCount;
            string head = name + " run of " + Metres(request.totalLengthMetres) + ": " + Int(n) + " sub-run"
                + (n == 1 ? "" : "s") + (solution.refused ? " -- REFUSED" : "") + ".";
            if (solution.segments == null) { return head; }

            string body = "";
            for (int i = 0; i < solution.segments.Count; i++)
            {
                EdgeRunSegment s = solution.segments[i];
                body += "\n  #" + Int(i + 1) + " [" + Metres(s.startStationMetres) + " to "
                    + Metres(s.endStationMetres) + "] ";
                if (s.refused)
                {
                    body += "REFUSED: " + s.refusal;
                }
                else
                {
                    string closing = s.body.closingPartLengthMetres > 0f
                        ? " + closing " + Metres(s.body.closingPartLengthMetres) : "";
                    body += KindName(s.startEnd.kind) + " -> " + Int(s.body.wholePartCount) + " parts" + closing
                        + " -> " + KindName(s.endEnd.kind) + "; " + s.body.note;
                }
            }
            return head + body;
        }

        private static string KindName(RunEndKind kind)
        {
            return kind.ToString();
        }

        private static string Metres(float value) { return Number(value) + " m"; }

        private static string Int(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

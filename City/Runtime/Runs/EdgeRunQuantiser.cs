#region "Imports"
using System;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> How a run's body was filled with whole parts and where its remainder went. Produced by
    /// <see cref="EdgeRunQuantiser"/> and carried into the segment; <see cref="note"/> is the stated place the
    /// remainder was absorbed, which is the "record the choice" half of the task. A quantisation that could not
    /// honestly fill the body carries <see cref="refused"/> instead of a stretched part. </summary>
    public struct RunQuantisation
    {
        public int wholePartCount;

        /// <summary> The spacing actually used along the body. Equals the type's part length except under
        /// DistributeSpacing, where it is nudged within tolerance. </summary>
        public float partSpacingMetres;

        /// <summary> A single short closing part's length, or 0 when the policy did not place one. </summary>
        public float closingPartLengthMetres;

        /// <summary> How much EACH end treatment was lengthened to take up remainder, or 0. </summary>
        public float endAbsorbMetresPerEnd;

        public RunRemainderPolicy policyUsed;
        public bool refused;
        public string refusal;
        public string note;

        /// <summary> The body length this quantisation actually covers, for the round-trip check. </summary>
        public float BodyLengthCoveredMetres
        {
            get { return wholePartCount * partSpacingMetres + closingPartLengthMetres + 2f * endAbsorbMetresPerEnd; }
        }
    }
    /// <summary> Fills a run body with whole parts of the type's fixed length and disposes of the remainder by the
    /// type's stated policy -- never by scaling a part. The three honest policies are the three real ways a fitter
    /// closes a run: lengthen the ends within their range, nudge the spacing within a tolerance, or add one short
    /// closing part. Each refuses, with the numbers named, where its own rule cannot swallow the remainder, and the
    /// solver turns that into a refused segment rather than a visible stretch. </summary>
    public static class EdgeRunQuantiser
    {
        private const float Epsilon = 1e-4f;

        public static RunQuantisation Quantise(EdgeRunType type, float bodyLengthMetres)
        {
            RunQuantisation q = new RunQuantisation();
            q.partSpacingMetres = type != null ? type.bodyPartLengthMetres : 0f;
            q.policyUsed = type != null ? type.remainderPolicy : RunRemainderPolicy.ClosingPart;
            q.note = "";
            q.refusal = "";

            if (type == null || type.bodyPartLengthMetres <= Epsilon)
            {
                q.refused = true;
                q.refusal = "the run type has no positive part length, so its body cannot be quantised.";
                return q;
            }
            if (bodyLengthMetres <= Epsilon)
            {
                q.refused = true;
                q.refusal = "the run has no body length left for parts once its ends are placed.";
                return q;
            }

            float part = type.bodyPartLengthMetres;
            int whole = Mathf.FloorToInt(bodyLengthMetres / part + Epsilon);
            float remainder = bodyLengthMetres - whole * part;
            if (remainder < 0f) { remainder = 0f; }

            if (remainder <= Epsilon)
            {
                q.wholePartCount = whole;
                q.partSpacingMetres = part;
                q.note = "fits exactly in " + Int(whole) + " parts of " + Metres(part) + ".";
                return q;
            }

            switch (type.remainderPolicy)
            {
                case RunRemainderPolicy.DistributeSpacing: return Distribute(type, bodyLengthMetres, whole, q);
                case RunRemainderPolicy.AbsorbInEnds: return Absorb(type, whole, part, remainder, q);
                default: return Close(type, whole, part, remainder, q);
            }
        }
        private static RunQuantisation Distribute(EdgeRunType type, float bodyLengthMetres, int whole,
            RunQuantisation q)
        {
            float part = type.bodyPartLengthMetres;
            int low = whole < 1 ? 1 : whole;
            int high = whole + 1;
            int best = low;
            float bestDev = Mathf.Abs(bodyLengthMetres / low - part);
            float devHigh = Mathf.Abs(bodyLengthMetres / high - part);
            if (devHigh < bestDev) { best = high; }

            float spacing = bodyLengthMetres / best;
            float fraction = Mathf.Abs(spacing / part - 1f);
            q.partSpacingMetres = spacing;
            if (fraction <= type.spacingTolerance + Epsilon)
            {
                q.wholePartCount = best;
                q.note = "the remainder is distributed as a " + Percent(fraction) + " spacing change over "
                    + Int(best) + " parts (spacing " + Metres(spacing) + " against the " + Metres(part)
                    + " part, within the " + Percent(type.spacingTolerance) + " tolerance).";
                return q;
            }
            q.refused = true;
            q.refusal = "a body of " + Metres(bodyLengthMetres) + " cannot be filled with " + Metres(part)
                + " parts without a " + Percent(fraction) + " spacing change, past the "
                + Percent(type.spacingTolerance) + " tolerance. Change the run length, or use the closing part.";
            return q;
        }

        private static RunQuantisation Absorb(EdgeRunType type, int whole, float part, float remainder,
            RunQuantisation q)
        {
            float perEnd = remainder * 0.5f;
            q.wholePartCount = whole;
            q.partSpacingMetres = part;
            if (perEnd <= type.endAdjustmentRangeMetres + Epsilon)
            {
                q.endAbsorbMetresPerEnd = perEnd;
                q.note = "the " + Metres(remainder) + " remainder is taken up by lengthening each end by "
                    + Metres(perEnd) + " (within the " + Metres(type.endAdjustmentRangeMetres) + " end range).";
                return q;
            }
            q.refused = true;
            q.refusal = "the " + Metres(remainder) + " remainder needs " + Metres(perEnd)
                + " added to each end, past the " + Metres(type.endAdjustmentRangeMetres)
                + " each end may take. Change the run length, or use the closing part.";
            return q;
        }
        private static RunQuantisation Close(EdgeRunType type, int whole, float part, float remainder,
            RunQuantisation q)
        {
            float closing = remainder;
            int parts = whole;
            q.partSpacingMetres = part;
            if (closing + Epsilon < type.closingPartMinLengthMetres)
            {
                if (parts >= 1)
                {
                    parts -= 1;
                    closing = remainder + part;
                    q.note = "one full part was dropped so the closing part is " + Metres(closing)
                        + " rather than a " + Metres(remainder) + " offcut below the "
                        + Metres(type.closingPartMinLengthMetres) + " minimum.";
                }
                else
                {
                    q.refused = true;
                    q.refusal = "the whole run is " + Metres(remainder) + ", below the "
                        + Metres(type.closingPartMinLengthMetres) + " minimum closing part and too short for even "
                        + "one " + Metres(part) + " part. Lengthen the run.";
                    return q;
                }
            }
            else
            {
                q.note = "a " + Metres(closing) + " closing part carries the remainder after " + Int(parts)
                    + " full parts of " + Metres(part) + ".";
            }
            q.wholePartCount = parts;
            q.closingPartLengthMetres = closing;
            return q;
        }

        private static string Metres(float value) { return Number(value) + " m"; }

        private static string Percent(float fraction) { return Number(fraction * 100f) + " %"; }

        private static string Int(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

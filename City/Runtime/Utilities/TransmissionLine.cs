#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A solved two-point catenary in the span's own plane: x runs horizontally from the first tower
    /// (x = 0) to the second (x = span), y is height. The curve is y(x) = a*cosh((x - x0)/a) + c, the exact
    /// shape a hanging cable takes under its own weight -- NOT a parabola, which is the approximation that looks
    /// right on a level span and visibly wrong on a long or inclined one. <see cref="a"/> is the catenary
    /// constant (H/w); <see cref="x0"/> is where the curve bottoms out; <see cref="c"/> the vertical offset.
    /// Everything is <c>double</c> and the solver runs a FIXED iteration count, so two bakes cannot disagree in
    /// the last bit (C-06). </summary>
    public struct Catenary
    {
        public double a;
        public double x0;
        public double c;
        public double spanMetres;
        public double height1;
        public double height2;

        /// <summary> Cable height at a horizontal distance x from the first tower. </summary>
        public double HeightAt(double x)
        {
            return a * Math.Cosh((x - x0) / a) + c;
        }

        /// <summary> The horizontal position of the cable's lowest point, clamped to the span: on an inclined
        /// span it shifts toward the lower tower and is NOT at mid-span, which is exactly why the symmetric
        /// shortcut is wrong here. </summary>
        public double LowestX()
        {
            if (x0 < 0.0) { return 0.0; }
            if (x0 > spanMetres) { return spanMetres; }
            return x0;
        }

        /// <summary> The cable's lowest height over the span. </summary>
        public double LowestHeight()
        {
            return HeightAt(LowestX());
        }
    }


    /// <summary> RM-23's verifiable core: the real catenary maths of an overhead line, plus the two other pure
    /// decisions the placer needs -- which tower type a node gets, and whether a span clears the ground. The
    /// cable is the half of RM-23 with genuine maths in it (the spec's words), and it is deterministic and
    /// scene-free, so it is built and gated here; the mesh (one mesh per span run, a minimum screen-space width,
    /// an explicit LOD story) and the prefab/EdgeObjectMaker placement -- including the "a road that makes no
    /// road" carrier question -- are the author-confirmed scene remainder, deferred.
    ///
    /// The authored input is the SAG RATIO, never the catenary constant a: an author knows a cable sags ~3 % of
    /// its span, never its H/w. The sag equation s = a*(cosh(L/2a) - 1) has no closed-form inverse, so a is
    /// found by bisection -- safe because the expression is monotonic in a -- with a fixed iteration count. On
    /// an inclined span the same physical a holds and only the vertex shifts, found from the two-point
    /// constraint. </summary>
    public static class TransmissionLine
    {
        /// <summary> Fixed bisection iterations -- enough to reach double precision, and FIXED (not an epsilon)
        /// so the result is identical on every machine and every run (C-06). </summary>
        public const int SolverIterations = 80;

        /// <summary> Solve the catenary constant a from the level-span sag relation s = a*(cosh(L/2a) - 1),
        /// where s = sagRatio * L. Monotonic decreasing in a (a small -> sag huge, a large -> sag -> 0), so
        /// bisection is safe; brackets are seeded from the parabolic estimate a0 = L^2 / (8s) and widened. </summary>
        public static double SolveCatenaryConstant(double spanMetres, double sagRatio)
        {
            double sag = sagRatio * spanMetres;
            if (sag <= 0.0 || spanMetres <= 0.0) { return double.PositiveInfinity; }

            double a0 = (spanMetres * spanMetres) / (8.0 * sag); // parabolic first guess
            double lo = a0 * 0.1;   // smaller a -> larger sag -> Sag(lo) > sag
            double hi = a0 * 10.0;  // larger  a -> smaller sag -> Sag(hi) < sag

            for (int i = 0; i < SolverIterations; i++)
            {
                double mid = 0.5 * (lo + hi);
                double sagMid = mid * (Math.Cosh(spanMetres / (2.0 * mid)) - 1.0);
                if (sagMid > sag) { lo = mid; } else { hi = mid; } // sag decreasing in a
            }
            return 0.5 * (lo + hi);
        }

        /// <summary> Solve the full two-point catenary between towers at heights h1 (x = 0) and h2 (x = L). The
        /// physical constant a comes from the sag ratio (above); the vertex x0 then satisfies
        /// h2 - h1 = a*(cosh((L - x0)/a) - cosh(x0/a)), solved in CLOSED FORM via the identity
        /// cosh A - cosh B = 2 sinh((A+B)/2) sinh((A-B)/2). The vertex is allowed to fall OUTSIDE [0, L] on a
        /// steep inclined span (clamping it there would discard the two-point constraint and fail to reproduce
        /// the higher tower's height); c then falls out of the first endpoint. </summary>
        public static Catenary Solve(double spanMetres, double height1, double height2, double sagRatio)
        {
            double a = SolveCatenaryConstant(spanMetres, sagRatio);

            // x0 from the two-point constraint h2 - h1 = a*(cosh((L-x0)/a) - cosh(x0/a)), solved in CLOSED FORM
            // via cosh A - cosh B = 2 sinh((A+B)/2) sinh((A-B)/2), so (L-2x0)/(2a) = asinh((h2-h1)/(2a sinh(L/2a)))
            // and x0 = L/2 - a*asinh(...). The vertex is ALLOWED to fall outside [0, L] on a steep inclined span --
            // LowestX clamps only for reporting -- because clamping x0 here would discard the two-point constraint
            // and fail to reproduce the higher tower's height. asinh is written via log (no Math.Asinh on Mono).
            double x0;
            if (double.IsInfinity(a) || a <= 0.0)
            {
                x0 = spanMetres * 0.5; // degenerate (no sag): symmetric vertex
            }
            else
            {
                double sinhHalfSpan = Math.Sinh(spanMetres / (2.0 * a));
                double y = (height2 - height1) / (2.0 * a * sinhHalfSpan);
                double asinhY = Math.Log(y + Math.Sqrt(y * y + 1.0));
                x0 = spanMetres * 0.5 - a * asinhY;
            }

            double c = height1 - a * Math.Cosh(x0 / a);
            Catenary cat = new Catenary();
            cat.a = a;
            cat.x0 = x0;
            cat.c = c;
            cat.spanMetres = spanMetres;
            cat.height1 = height1;
            cat.height2 = height2;
            return cat;
        }

        /// <summary> Which tower a node gets, from the spline's turn angle there against the AUTHORED threshold.
        /// A straight-through node (turn under the threshold) is a suspension tower; a real direction change is
        /// an angle tower. Negative/absolute-value safe. </summary>
        public static TowerType ChooseTowerType(double turnAngleDeg, double angleThresholdDeg)
        {
            return Math.Abs(turnAngleDeg) >= angleThresholdDeg ? TowerType.Angle : TowerType.Suspension;
        }

        /// <summary> Even tower spacing along a run of a given length: ceil(length / span) spans, so the last
        /// span is at most the class span rather than a long overrun, and the count of towers is spans + 1.
        /// Returns the number of SPANS (>= 1 for any positive length). </summary>
        public static int SpanCount(double runLengthMetres, double spanLengthMetres)
        {
            if (runLengthMetres <= 0.0 || spanLengthMetres <= 0.0) { return 0; }
            return (int)Math.Ceiling(runLengthMetres / spanLengthMetres);
        }

        /// <summary> The result of a ground-clearance test on one span. </summary>
        public struct ClearanceResult
        {
            public bool clears;
            public double worstClearanceMetres;
            public int worstStationIndex;
        }

        /// <summary> Sample the solved cable against ground heights taken at evenly spaced stations across the
        /// span (station 0 at the first tower, the last at the second) and report the tightest clearance. A span
        /// that fails is FLAGGED (clears = false), never silently flattened -- the author decides. Supplying the
        /// ground heights keeps this pure; the real terrain sampling through IPCGDataProvider is the deferred
        /// scene half. </summary>
        public static ClearanceResult SpanClearance(Catenary cat, IList<double> groundHeights,
            double minClearanceMetres)
        {
            ClearanceResult r = new ClearanceResult();
            r.clears = true;
            r.worstClearanceMetres = double.PositiveInfinity;
            r.worstStationIndex = -1;
            if (groundHeights == null || groundHeights.Count < 2) { return r; }

            int last = groundHeights.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                double x = cat.spanMetres * ((double)i / last);
                double clearance = cat.HeightAt(x) - groundHeights[i];
                if (clearance < r.worstClearanceMetres)
                {
                    r.worstClearanceMetres = clearance;
                    r.worstStationIndex = i;
                }
            }
            r.clears = r.worstClearanceMetres >= minClearanceMetres;
            return r;
        }
    }
}

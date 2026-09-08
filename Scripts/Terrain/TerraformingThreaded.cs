using System.Collections.Generic;
using UnityEngine;


namespace RoadCityMaster.Threading
{
    public static class TerraformingThreaded
    {
        public class TerrainBoundsMaker
        {
            public List<Construction3DTri> triList;
            public Construction2DRect constructRect;
            public float MinI = 0f;
            public float MaxI = 1f;
        }


        #region "RM-98: how much ground a tunnel keeps over it"
        private static float tunnelGroundTopAboveRoad = DefaultTunnelGroundTopAboveRoad();

        /// <summary> Metres of ground that must sit above the carriageway wherever a segment is a tunnel: the traffic
        /// clearance, the structural roof, and the cover over it.
        ///
        /// It is cached in a field rather than computed per sample because terraforming runs off the main thread over
        /// tens of thousands of samples, and the policy call allocates its fallback setting objects. It is a
        /// <c>float</c> because everything it is compared against here is: the figure itself is derived in
        /// <c>double</c> in the City layer and narrowed once, at this boundary. </summary>
        public static float TunnelGroundTopAboveRoad { get { return tunnelGroundTopAboveRoad; } }

        /// <summary> Point the threaded terraforming at a project's own tunnel settings. Call this on the main thread
        /// before a build starts -- reading the setting objects from a worker thread is what it exists to avoid.
        ///
        /// The land figure is the one taken. An under-water reach is not decided here: whether a bore may run under a
        /// river at all is the City layer's refusal (<c>TunnelCavity.ResolveBuild</c>), and a reach that was refused
        /// never reaches this file. </summary>
        public static void UseTunnelCover(City.TunnelClass tunnel, City.TunnelCoverClass cover,
            City.GradeSeparationClass separation)
        {
            tunnelGroundTopAboveRoad = (float)City.TunnelTerrainPolicy.TunnelGroundTopAboveRoadMetres(
                false, tunnel, cover, separation);
        }

        /// <summary> The figure a project that has set nothing gets: the authored defaults of the same three classes,
        /// so the vendored path and the City layer agree before anyone touches an inspector. </summary>
        private static float DefaultTunnelGroundTopAboveRoad()
        {
            return (float)City.TunnelTerrainPolicy.TunnelGroundTopAboveRoadMetres(
                false, new City.TunnelClass(), City.TunnelCoverClass.Default(), new City.GradeSeparationClass());
        }
        #endregion


        /*
        private static Vector3 ProcessLineHeights_PrevVect = new Vector3(0f, 0f, 0f);


        public static float ProcessLineHeights(SplineC tSpline, ref Vector3 tVect, ref Vector3 POS, float tDistance, Terraforming.TempTerrainData TTD, float PrevDesiredHeight)
        {
            Vector3 ShoulderR_rVect = new Vector3(0f, 0f, 0f);
            Vector3 ShoulderL_lVect = new Vector3(0f, 0f, 0f);

            float DesiredHeight = ProcessLineHeights_GetDesiredHeight(tVect, ref TTD, ref tSpline);
            float nResult = 0f;
            bool bIntersection = tSpline.IsNearIntersection(ref tVect, ref nResult);
            if(bIntersection)
            {
                if(nResult < tVect.y)
                {
                    tVect.y = nResult;
                    DesiredHeight = ProcessLineHeights_GetDesiredHeight(tVect, ref TTD, ref tSpline);
                }
            }

            int x1 = 0, y1 = 0;
            GetTempHeights_Coordinates(ref tVect, ref TTD, out x1, out y1);

            bool bOverride = false;
            int StepMod = (int) (1 / TTD.HMRatio);
            for(float i = tDistance; i >= 1f; i -= StepMod)
            {
                ShoulderR_rVect = (tVect + new Vector3(i * POS.normalized.z, 0, i * -POS.normalized.x));
                GetTempHeights_Coordinates(ref ShoulderR_rVect, ref TTD, out x1, out y1);
                if(TTD.heights[x1, y1] > DesiredHeight)
                {
                    bOverride = true;
                }
                if(bOverride || !TTD.tHeights[x1, y1])
                {
                    TTD.tHeights[x1, y1] = true;
                    TTD.cX[TTD.cI] = x1;
                    TTD.cY[TTD.cI] = y1;
                    TTD.cH[TTD.cI] = DesiredHeight;
                    TTD.oldH[TTD.cI] = TTD.heights[x1, y1];
                    TTD.cI += 1;
                }
                bOverride = false;

                ShoulderL_lVect = (tVect + new Vector3(i * -POS.normalized.z, 0, i * POS.normalized.x));
                GetTempHeights_Coordinates(ref ShoulderL_lVect, ref TTD, out x1, out y1);
                if(TTD.heights[x1, y1] > DesiredHeight)
                {
                    bOverride = true;
                }
                if(bOverride || !TTD.tHeights[x1, y1])
                {
                    TTD.tHeights[x1, y1] = true;
                    TTD.cX[TTD.cI] = x1;
                    TTD.cY[TTD.cI] = y1;
                    TTD.cH[TTD.cI] = DesiredHeight;
                    TTD.oldH[TTD.cI] = TTD.heights[x1, y1];
                    TTD.cI += 1;
                }
                bOverride = false;
            }

            GetTempHeights_Coordinates(ref tVect, ref TTD, out x1, out y1);
            if(TTD.heights[x1, y1] > DesiredHeight || (tVect.y < ProcessLineHeights_PrevVect.y))
            {
                bOverride = true;
            }
            if(bOverride || !TTD.tHeights[x1, y1])
            {
                TTD.tHeights[x1, y1] = true;
                TTD.cX[TTD.cI] = x1;
                TTD.cY[TTD.cI] = y1;
                if(tDistance > 15f && TTD.HMRatio > 0.24f)
                {
                    TTD.cH[TTD.cI] = DesiredHeight - 0.0002f;
                }
                else
                {
                    TTD.cH[TTD.cI] = DesiredHeight;
                }
                TTD.oldH[TTD.cI] = TTD.heights[x1, y1];
                TTD.cI += 1;
            }

            ProcessLineHeights_PrevVect = tVect;
            return DesiredHeight;
        }


        private static float ProcessLineHeights_GetDesiredHeight(Vector3 tVect, ref Terraforming.TempTerrainData TTD, ref SplineC tSpline)
        {
            return ((((tVect - TTD.TerrainPos).y) - tSpline.tRoad.opt_TerrainSubtract_Alt) / TTD.TerrainSize.y);
        }
        */


        /// <summary> Writes heightmap location of _vector into _x and _y </summary>
        private static void GetTempHeightsCoordinates(ref Vector3 _vector, ref Terraforming.TempTerrainData _TTD, out int _x, out int _y)
        {
            // B-17. Through RoadNodeGround.ToHeightmapCell, which is this same arithmetic and nothing else. It is
            // shared because the terrain history has to be READ back with the mapping that WROTE it: a second copy
            // that rounded where this one truncates would miss the recorded cell about half the time, and a miss there
            // silently restores the ratchet it exists to remove. Still unclamped -- every caller below treats an index
            // at or past the resolution as "off this tile" and bails, and that is deliberate.
            RoadNodeGround.ToHeightmapCell(_vector, _TTD.TerrainPos, _TTD.TerrainSize, _TTD.HM, out _x, out _y);
        }


        /// <summary> Writes heightmap location of _vector into _x and _y </summary>
        private static void GetTempDetailsCoordinates(ref Vector3 _vector, ref Terraforming.TempTerrainData _TTD, out int _x, out int _y)
        {
            //Get the normalized position of this game object relative to the terrain:
            Vector3 tempCoord = (_vector - _TTD.TerrainPos);

            Vector3 coord;
            coord.x = tempCoord.x / _TTD.TerrainSize.x;
            coord.y = tempCoord.y / _TTD.TerrainSize.y;
            coord.z = tempCoord.z / _TTD.TerrainSize.z;

            //Get the position of the terrain heightmap where this game object is:
            _y = (int)(coord.x * _TTD.DetailMaxIndex);
            _x = (int)(coord.z * _TTD.DetailMaxIndex);
        }


        #region "B-12: the batter, and how wide the ground it needs is"
        /// <summary> Scratch buffer for the outward march, reused across stations. The march runs per vertex per
        /// station -- tens of thousands of times per road -- so a fresh List each time would be a collection storm on
        /// the worker thread. <see cref="City.CutSlope.MarchToCatch"/> reads it as an <c>IList&lt;double&gt;</c> and
        /// takes its length as the step count, so the buffer is sized exactly and only re-sized when the step count
        /// changes, which is once per road/terrain pair. It is [ThreadStatic] because terraforming may run on more
        /// than one worker and the buffer must not be shared between them. </summary>
        [System.ThreadStatic] private static double[] marchBuffer;

        private static int batterHandBackCount;

        private static int cutFloorCount;

        private static int worstCutShortfallMillimetres;

        /// <summary> How many station/side batters failed to reach natural ground within the authored maximum during
        /// the most recent terraforming pass, and were therefore cut off at that maximum. It is a COUNT, not a
        /// decision: real practice benches, retains or re-aligns instead, and which of those to do is the author's
        /// call. It exists so the build report can say the slope was truncated rather than leave the author to notice
        /// a flat edge; the per-station listing belongs with B-14's progress and reporting surface, not here.
        /// Incremented with Interlocked because terraforming may run on more than one worker.
        ///
        /// B-14 read it: <c>Road.ConstructRoad2</c>, on the main thread, where the terraform's results are taken into
        /// the Terrain. Until then nothing anywhere called this getter, so a truncated batter was counted and thrown
        /// away -- the author's only evidence was a flat edge in the Scene view they had to spot themselves. </summary>
        public static int BatterHandBackCount { get { return batterHandBackCount; } }

        /// <summary> Zero the hand-back count at the start of a build.
        ///
        /// B-14 moved the call OUT of <see cref="DoRectsDo"/>, which reset it per TERRAIN. A road crossing two tiles
        /// runs DoRects once per tile, so every truncated batter on the first tile was zeroed before the second tile
        /// started and the count that survived to be reported was the last tile's alone. It is now zeroed once per
        /// road terraform, beside <c>City.TerrainBuildSettings.ApplyToWorker()</c> in <c>Terraforming.cs</c> -- the
        /// same main-thread moment that arms the worker for both the threaded and the immediate branch, so the count
        /// brackets exactly the pass whose result <c>ConstructRoad2</c> reads. </summary>
        public static void ResetBatterHandBackCount()
        {
            System.Threading.Interlocked.Exchange(ref batterHandBackCount, 0);
        }

        /// <summary> How many ground samples this build wanted to cut below the lowest height their tile can hold, and
        /// the worst of those shortfalls in millimetres.
        ///
        /// B-17: where a road is cut deeper than a terrain's own base plane, the ground physically cannot follow it
        /// down. That is a legitimate outcome -- the author can lower the tile, extend its height range, or raise the
        /// road -- but it was reached by silently clamping, and the author's only evidence was terrain standing above
        /// their carriageway. Counted here, said out loud by <c>Road.ReportRefusedCuts</c>, on the same terms B-14 gave
        /// the truncated batter.
        ///
        /// Millimetres, and an int, so the worst can be raised from several workers with a lock-free
        /// compare-and-exchange; a float maximum has no atomic form. </summary>
        public static int CutFloorCount { get { return cutFloorCount; } }

        /// <summary> The deepest a sample fell short of its tile's floor, in metres. </summary>
        public static float WorstCutShortfallMetres
        {
            get { return worstCutShortfallMillimetres * 0.001f; }
        }

        /// <summary> Zero both, once per road terraform, beside <see cref="ResetBatterHandBackCount"/> -- and for the
        /// reason recorded there: zeroing per TERRAIN would throw away the first tile's count. </summary>
        public static void ResetCutFloorCount()
        {
            System.Threading.Interlocked.Exchange(ref cutFloorCount, 0);
            System.Threading.Interlocked.Exchange(ref worstCutShortfallMillimetres, 0);
        }

        /// <summary> Count one sample the tile could not go low enough for, and raise the worst shortfall if this is
        /// it. A zero shortfall is the ordinary case and costs one comparison. </summary>
        private static void RecordCutFloor(float shortfallMetres)
        {
            if (shortfallMetres <= 0f) { return; }
            System.Threading.Interlocked.Increment(ref cutFloorCount);

            int millimetres = (int)(shortfallMetres * 1000f);
            int seen = worstCutShortfallMillimetres;
            while (millimetres > seen)
            {
                int was = System.Threading.Interlocked.CompareExchange(ref worstCutShortfallMillimetres,
                    millimetres, seen);
                if (was == seen) { break; }
                seen = was;
            }
        }

        /// <summary> The natural (pre-build) ground elevation in WORLD space under a point, or false where it cannot
        /// be read: no snapshot, or a point off this terrain. Reads
        /// <see cref="Terraforming.TempTerrainData.naturalHeights"/>, never <c>heights</c> -- the live array has
        /// already been written by earlier segments, and measuring a batter against ground the tool itself raised is
        /// what would make the result depend on build order. </summary>
        private static bool NaturalWorldHeight(ref Vector3 _world, ref Terraforming.TempTerrainData _TTD,
            out float _worldY)
        {
            _worldY = 0f;
            if (_TTD.naturalHeights == null) { return false; }
            int x1, y1;
            GetTempHeightsCoordinates(ref _world, ref _TTD, out x1, out y1);
            if (x1 < 0 || y1 < 0 || x1 >= _TTD.TerrainMaxIndex || y1 >= _TTD.TerrainMaxIndex) { return false; }
            _worldY = RoadNodeGround.FromHeightmap01(_TTD.naturalHeights[x1, y1], _TTD.TerrainPos.y,
                _TTD.TerrainSize.y);
            return true;
        }

        /// <summary> How far out from the carriageway edge the ground has to travel, on ONE side, at ONE station,
        /// for the batter to reach natural ground -- B-12's `lateral reach = height difference / batter gradient`,
        /// found by the march that <c>City/Runtime/Terrain/CutSlope.cs</c> already owns rather than by a second copy
        /// of the ratio maths here.
        ///
        /// The march is written for the CUT direction: it steps outward, raises the slope at the authored ratio, and
        /// catches at the first step where the slope has reached the hillside. Fill is the same problem mirrored in
        /// the horizontal plane through the road edge, so where the ground falls away the samples are reflected about
        /// that plane (natural' = 2*edge - natural) and the identical march returns the identical run. That is a
        /// transform of the input, not a second model.
        ///
        /// The step is the heightmap's own sample spacing: marching finer than the terrain can represent resolves
        /// nothing. Where the batter would pass the authored maximum the reach is the maximum and the station is
        /// reported through <see cref="BatterHandBackCount"/>, so a road perched over a valley hands the decision back
        /// instead of reshaping the hillside. </summary>
        private static float BatterReachMetres(float _bandEdgeWorldY, ref Vector3 _edgeWorld,
            ref Vector3 _outwardUnit, Road _road, ref Terraforming.TempTerrainData _TTD, float _maxReachMetres)
        {
            float step = _TTD.MetersPerHM;
            if (step <= 0.001f) { step = 1f; }
            if (_maxReachMetres <= step || _road == null || _road.batterRunPerRise <= 0f)
            {
                return _maxReachMetres > 0f ? _maxReachMetres : 0f;
            }

            int steps = (int)(_maxReachMetres / step);
            if (steps < 1) { steps = 1; }
            if (marchBuffer == null || marchBuffer.Length != steps) { marchBuffer = new double[steps]; }

            // Sample natural ground outward, one heightmap step at a time. A step that cannot be read (off the
            // terrain) is filled with the band-edge elevation, which catches immediately -- the batter stops at the
            // terrain boundary rather than marching into nothing.
            bool falling = false;
            for (int i = 0; i < steps; i++)
            {
                Vector3 at = _edgeWorld + (_outwardUnit * (step * (i + 1)));
                float natural;
                if (!NaturalWorldHeight(ref at, ref _TTD, out natural)) { natural = _bandEdgeWorldY; }
                if (i == 0) { falling = natural < _bandEdgeWorldY; }
                // Fill: mirror the sample about the horizontal plane through the road edge so the rising march
                // solves the falling case unchanged.
                marchBuffer[i] = falling ? ((2.0 * _bandEdgeWorldY) - natural) : natural;
            }

            City.CutSlopeClass cls = new City.CutSlopeClass();
            cls.slopeRunPerRise = _road.batterRunPerRise;
            cls.stepMetres = step;
            cls.maxSlopeHeightMetres = _maxReachMetres / _road.batterRunPerRise;
            City.CutSlope.CatchResult r = City.CutSlope.MarchToCatch(_bandEdgeWorldY, marchBuffer, cls);

            if (r.exceededMax || !r.caught) { System.Threading.Interlocked.Increment(ref batterHandBackCount); }
            float reach = r.caught ? (float)r.catchRunMetres : _maxReachMetres;
            if (reach < step) { reach = step; }
            if (reach > _maxReachMetres) { reach = _maxReachMetres; }
            return reach;
        }

        /// <summary> How many rings the batter is emitted in. One ring is a single quad per side -- a straight ramp,
        /// which is what the tool used to write and what leaves a crease where it meets the flat band and another
        /// where it meets natural ground. More rings let <see cref="City.TerrainFootprint.DeformedHeightMetres"/>'s
        /// S-curve actually appear in the geometry. Ring spacing finer than the heightmap resolves nothing, so the
        /// count follows the reach measured in heightmap steps, floored at 2 and capped at 6: every tri here is
        /// scanned per terrain sample by AdjustedTerrainVectTri, so the cap is a cost ceiling, and 6 segments put
        /// the ramp within a few centimetres of the true curve over a 25 m batter. </summary>
        private static int BatterRingCount(float _reachMetres, float _stepMetres)
        {
            if (_reachMetres <= 0.001f) { return 1; }
            float step = _stepMetres > 0.001f ? _stepMetres : 1f;
            int n = (int)System.Math.Ceiling(_reachMetres / step);
            if (n < 2) { n = 2; }
            if (n > 6) { n = 6; }
            return n;
        }

        /// <summary> Emit one side's batter as <paramref name="_rings"/> quads running outward from the carriageway
        /// edge to the daylight line, each ring's height taken from
        /// <see cref="City.TerrainFootprint.DeformedHeightMetres"/> at that ring's real lateral distance in metres.
        /// The flat band is the flattened half-width and the daylight line is the blend outer half-width, which is
        /// exactly the contract that method is written to: road height out to the flattened edge, a smoothstep with
        /// zero derivative at BOTH ends across the blend, natural height beyond. With <paramref name="_rings"/> = 1
        /// this reduces to the two tris the tool emitted before, so the subdivision is a generalisation of the old
        /// geometry rather than a replacement for it. The two stations may have different reaches -- the ground is
        /// not obliged to be at the same height at both ends of a segment -- so each is parameterised on its own
        /// fraction of its own reach and the ring is the straight line between them. </summary>
        private static void AddBatterRings(ref List<Construction3DTri> _tris, int _rings, float _sep, float _i,
            float _i2, ref Vector3 _centre1, ref Vector3 _dir1, float _reach1, float _innerY1, float _outerY1,
            ref Vector3 _centre2, ref Vector3 _dir2, float _reach2, float _innerY2, float _outerY2)
        {
            if (_rings < 1) { _rings = 1; }
            double flat = _sep;
            double outer1 = _sep + _reach1;
            double outer2 = _sep + _reach2;

            Vector3 a1 = _centre1 + (_dir1 * _sep);
            a1.y = _innerY1;
            Vector3 a2 = _centre2 + (_dir2 * _sep);
            a2.y = _innerY2;

            for (int ring = 1; ring <= _rings; ring++)
            {
                float t = (float)ring / (float)_rings;

                double lateral1 = _sep + (_reach1 * t);
                Vector3 b1 = _centre1 + (_dir1 * (float)lateral1);
                b1.y = (float)City.TerrainFootprint.DeformedHeightMetres(_innerY1, _outerY1, lateral1, flat, outer1);

                double lateral2 = _sep + (_reach2 * t);
                Vector3 b2 = _centre2 + (_dir2 * (float)lateral2);
                b2.y = (float)City.TerrainFootprint.DeformedHeightMetres(_innerY2, _outerY2, lateral2, flat, outer2);

                _tris.Add(new Construction3DTri(b1, a1, b2, _i, _i2));
                _tris.Add(new Construction3DTri(b2, a1, a2, _i, _i2));

                a1 = b1;
                a2 = b2;
            }
        }
        #endregion


        //Privatized for obfuscate:
        public static void DoRects(SplineC _spline, Terraforming.TempTerrainData _TTD)
        {
            DoRectsDo(ref _spline, ref _TTD);
        }


        private static void DoRectsDo(ref SplineC _spline, ref Terraforming.TempTerrainData _TTD)
        {
            // B-12, wired in B-14. The flat band is the EMITTED CROSS-SECTION plus an authored verge beyond it, through
            // City.TerrainFootprint -- the class that owns this relationship and, until this line, had nothing in the
            // build path calling it.
            //
            // It was `RoadWidth() * 0.5f` alone, and RoadWidth() is `laneWidth * LaneTotal()`: the running lanes and
            // nothing else. So the ground stopped being flat at the edge of the running lanes and the batter broke from
            // there -- underneath the road's own shoulder meshes, which are emitted outside that figure, and underneath
            // the median on a divided road. The shoulder was left standing over the slope it was meant to sit on, which
            // is why a batter that met the carriageway looked like a kerb dropped into a field. CrossSectionWidth() is
            // lanes + median + a shoulder each side -- what the mesh actually emits -- and FlattenedHalfWidthMetres adds
            // the authored verge past it, because no road is built with its formation ending at the break of slope.
            //
            // Per STATION -- Road.CrossSectionWidthAt, where an auxiliary lane opens across its taper -- is deliberately
            // not taken here. _sep is one scalar for the whole road by the time CreateTris reads it, so making the band
            // vary along the spline is a change to the geometry the author judges on screen, not to the width arithmetic
            // this entry is about; it also allocates a List<LaneBand> per station, on the terraform worker.
            float legacyBandHalf = _spline.road.RoadWidth() * 0.5f;
            City.TerrainFootprintClass footprint = _spline.road.terrainFootprint;
            if (footprint == null) { footprint = City.TerrainFootprintClass.Default(); }
            float Sep = (float)City.TerrainFootprint.FlattenedHalfWidthMetres(
                _spline.road.CrossSectionWidth() * 0.5f, footprint);
            // B-12. HeightSep is the OUTER BOUND OF THE SEARCH, not the width of the deformation. It used to be
            // both, which is the whole defect: the ground beside the carriageway was carried out to this fixed
            // distance and written flat at road level, so a 0.2 m fill and a 3 m fill claimed identical ground and
            // each ended in a step. The ground the batter actually claims is now derived per station and per side in
            // CreateTris and never exceeds (HeightSep - Sep). The bound stays tied to matchHeightsDistance because
            // that figure -- not the batter -- is what sized the write buffers in Terraforming.cs, so capping the
            // batter by it keeps the allocation contract exactly as it was.
            //
            // B-14 states that bound once, here, and spends it in order: the flat band takes the ground it needs, the
            // batter gets what is left. Widening the band above therefore cannot move HeightSep for any authored
            // section or verge -- the outer reach is `legacyBandHalf + matchHeightsDistance * 0.5f` whatever those
            // figures are, so the buffers Terraforming.cs sizes are the same size for the same road as before. The
            // allocation contract is arithmetic here rather than an observation about the widths that happen to ship.
            float outerBound = legacyBandHalf + (_spline.road.matchHeightsDistance * 0.5f);
            if (Sep > outerBound) { Sep = outerBound; }
            float maxBatterReach = _spline.road.batterMaxReachMetres;
            if (maxBatterReach < 0f) { maxBatterReach = 0f; }
            float lookLimit = outerBound - Sep;
            if (maxBatterReach > lookLimit) { maxBatterReach = lookLimit; }
            float HeightSep = Sep + maxBatterReach;
            List<TerrainBoundsMaker> TBMList = new List<TerrainBoundsMaker>();
            //List<RoadUtility.Construction3DTri> triList = new List<RoadUtility.Construction3DTri>();
            List<Construction2DRect> TreerectList = new List<Construction2DRect>();
            float tStep = _spline.road.roadDefinition / _spline.distance;
            //tStep *= 0.5f;

            //Start initializing the loop. Convuluted to handle special control nodes, so roads don't get rendered where they aren't supposed to, while still preserving the proper curvature.
            float FinalMax = 1f;
            float StartMin = 0f;
            if (_spline.isSpecialEndControlNode)
            {
                //If control node, start after the control node:
                FinalMax = _spline.nodes[_spline.GetNodeCount() - 2].time;
            }
            if (_spline.isSpecialStartControlNode)
            {
                //If ends in control node, end construction before the control node:
                StartMin = _spline.nodes[1].time;
            }
            //Storage of incremental start values for the road connection mesh construction at the end of this function.
            //float RoadConnection_StartMin1 = StartMin;
            //Storage of incremental end values for the road connection mesh construction at the end of this function.
            //float RoadConnection_FinalMax1 = FinalMax;
            if (_spline.isSpecialEndNodeIsStartDelay)
            {
                //If there's a start delay (in meters), delay the start of road construction: Due to special control nodes for road connections or 3 way intersections.
                StartMin += (_spline.specialEndNodeDelayStart / _spline.distance);
            }
            else if (_spline.isSpecialEndNodeIsEndDelay)
            {
                //If there's a end delay (in meters), cut early the end of road construction: Due to special control nodes for road connections or 3 way intersections.
                FinalMax -= (_spline.specialEndNodeDelayEnd / _spline.distance);
            }
            //Storage of incremental start values for the road connection mesh construction at the end of this function.
            //float RoadConnection_StartMin2 = StartMin;
            //Storage of incremental end values for the road connection mesh construction at the end of this function.
            //float RoadConnection_FinalMax2 = FinalMax;

            FinalMax = FinalMax + tStep;

            Vector3 tVect1 = default(Vector3);
            Vector3 tVect2 = default(Vector3);
            Vector3 POS1 = default(Vector3);
            Vector3 POS2 = default(Vector3);
            if (FinalMax > 1f)
            {
                FinalMax = 1f;
            }

            float tNext = 0f;
            float fValue1, fValue2;
            float fValueMod = _spline.road.roadDefinition / _spline.distance;
            bool bIsPastInter = false;
            float tIntStrength = 0f;
            float tIntStrength2 = 0f;
            //bool bMaxIntersection = false;
            SplineN xNode = null;
            float tIntHeight = 0f;
            float tIntHeight2 = 0f;
            RoadIntersection roadIntersection = null;
            float T1SUB = 0f;
            float T2SUB = 0f;
            bool bIntStr1_Full = false;
            bool bIntStr1_FullPrev = false;
            bool bIntStr1_FullNext = false;
            bool bIntStr2_Full = false;
            bool bIntStr2_FullPrev = false;
            bool bIntStr2_FullNext = false;
            Vector3 tVect3 = default(Vector3);
            List<int[]> tXYs = new List<int[]>();
            float TreeClearDist = _spline.road.clearTreesDistance;
            if (TreeClearDist < _spline.road.RoadWidth())
            {
                TreeClearDist = _spline.road.RoadWidth();
            }
            Construction2DRect tRect = null;
            float tGrade = 0f;
            for (float index = StartMin; index < FinalMax; index += tStep)
            {
                if (_spline.road.isHeightModificationEnabled)
                {
                    if (index > 1f)
                    {
                        break;
                    }
                    tNext = index + tStep;
                    if (tNext > 1f)
                    {
                        break;
                    }

                    _spline.GetSplineValueBoth(index, out tVect1, out POS1);

                    if (tNext <= 1f)
                    {
                        _spline.GetSplineValueBoth(tNext, out tVect2, out POS2);
                    }
                    else
                    {
                        _spline.GetSplineValueBoth(1f, out tVect2, out POS2);
                    }

                    //Determine if intersection:
                    bIsPastInter = false;
                    //If past intersection
                    tIntStrength = _spline.IntersectionStrength(ref tVect1, ref tIntHeight, ref roadIntersection, ref bIsPastInter, ref index, ref xNode);
                    //if(IsApproximately(tIntStrength,1f,0.001f) || tIntStrength > 1f)
                    //{
                    //	bMaxIntersection = true;
                    //}
                    //else
                    //{
                    //	bMaxIntersection = false;	
                    //}

                    tIntStrength2 = _spline.IntersectionStrength(ref tVect2, ref tIntHeight2, ref roadIntersection, ref bIsPastInter, ref index, ref xNode);
                    if (tIntStrength2 > 1f)
                    {
                        tIntStrength2 = 1f;
                    }

                    T1SUB = tVect1.y;
                    T2SUB = tVect2.y;

                    if (tIntStrength > 1f)
                    {
                        tIntStrength = 1f;
                    }
                    if (tIntStrength >= 0f)
                    {
                        // || IsApproximately(tIntStrength,0f,0.01f)){
                        if (RootUtils.IsApproximately(tIntStrength, 1f, 0.01f))
                        {
                            T1SUB = tIntHeight;
                            bIntStr1_Full = true;
                            bIntStr1_FullNext = false;
                        }
                        else
                        {
                            bIntStr1_Full = false;
                            bIntStr1_FullNext = (tIntStrength2 >= 1f);
                            if (!RootUtils.IsApproximately(tIntStrength, 0f, 0.01f))
                            {
                                T1SUB = (tIntStrength * tIntHeight) + ((1 - tIntStrength) * tVect1.y);
                            }
                            //if(tIntStrength <= 0f)
                            //{
                            //  T1SUB = (tIntStrength*tIntHeight) + ((1-tIntStrength)*tVect1.y);
                            //}
                        }

                        if ((bIntStr1_Full && !bIntStr1_FullPrev) || (!bIntStr1_Full && bIntStr1_FullNext))
                        {
                            tGrade = _spline.GetCurrentNode(index).gradeToPrevValue;
                            if (tGrade < 0f)
                            {
                                T1SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, (tGrade / 20f) * -1f);
                            }
                            else
                            {
                                T1SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, tGrade / 20f);
                            }

                            //if(tGrade < 0f)
                            //{
                            //	T1SUB *= -1f;
                            //}
                        }
                        else if (bIntStr1_Full && !bIntStr1_FullNext)
                        {
                            tGrade = _spline.GetCurrentNode(index).gradeToNextValue;
                            if (tGrade < 0f)
                            {
                                T1SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, (tGrade / 20f) * -1f);
                            }
                            else
                            {
                                T1SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, tGrade / 20f);
                            }
                            //if(tGrade < 0f)
                            //{
                            //	T1SUB *= -1f;
                            //}
                        }
                        else
                        {
                            T1SUB -= 0.02f;
                        }
                        bIntStr1_FullPrev = bIntStr1_Full;
                    }

                    if (tIntStrength2 >= 0f || RootUtils.IsApproximately(tIntStrength2, 0f, 0.01f))
                    {
                        //if(!IsApproximately(tIntStrength,1f,0.01f)){ 
                        if (RootUtils.IsApproximately(tIntStrength, 1f, 0.01f))
                        {
                            bIntStr2_Full = true;
                            T2SUB = tIntHeight2;
                        }
                        else
                        {
                            bIntStr2_Full = false;
                            if (!RootUtils.IsApproximately(tIntStrength2, 0f, 0.01f))
                            {
                                T2SUB = (tIntStrength2 * tIntHeight) + ((1 - tIntStrength2) * tVect2.y);
                            }
                            //if(tIntStrength2 <= 0f)
                            //{
                            //T2SUB = (tIntStrength2*tIntHeight) + ((1-tIntStrength2)*tVect2.y);
                            //}
                        }

                        if ((bIntStr2_Full && !bIntStr2_FullPrev))
                        {
                            tGrade = _spline.GetCurrentNode(index).gradeToPrevValue;
                            if (tGrade < 0f)
                            {
                                T2SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, (tGrade / 20f) * -1f);
                            }
                            else
                            {
                                T2SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, tGrade / 20f);
                            }
                            //T2SUB -= tIntHeight2 - tVect2.y;
                        }
                        else if (bIntStr2_Full && !bIntStr2_FullNext)
                        {
                            tGrade = _spline.GetCurrentNode(index).gradeToNextValue;
                            if (tGrade < 0f)
                            {
                                T2SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, (tGrade / 20f) * -1f);
                            }
                            else
                            {
                                T2SUB -= Mathf.Lerp(0.02f, roadIntersection.gradeMod, tGrade / 20f);
                            }
                            //if(tGrade < 0f)
                            //{
                            //	T2SUB *= -1f;
                            //}
                            //T2SUB -= tIntHeight2 - tVect2.y;
                        }
                        else if (!bIntStr2_Full)
                        {
                            if (tNext + tStep < 1f)
                            {
                                tVect3 = _spline.GetSplineValue(tNext + tStep, false);
                                tIntStrength2 = _spline.IntersectionStrength(ref tVect3, ref tIntHeight2, ref roadIntersection, ref bIsPastInter, ref index, ref xNode);
                            }
                            else
                            {
                                tIntStrength2 = 0f;
                            }

                            if (tIntStrength2 >= 1f)
                            {
                                T2SUB -= 0.06f;
                            }
                            else
                            {
                                T2SUB -= 0.02f;
                            }
                        }
                        else
                        {
                            T2SUB -= 0.02f;
                        }
                        bIntStr2_FullPrev = bIntStr2_Full;
                    }

                    fValue1 = index - fValueMod;
                    fValue2 = index + fValueMod;
                    if (fValue1 < 0)
                    {
                        fValue1 = 0;
                    }
                    if (fValue2 > 1)
                    {
                        fValue2 = 1;
                    }

                    tXYs.Add(CreateTris(fValue1, fValue2, ref tVect1, ref POS1, ref tVect2, ref POS2, Sep, ref TBMList, ref T1SUB, ref T2SUB, ref _TTD, HeightSep, ref _spline));

                    //Details and trees:
                    tRect = SetDetailCoords(index, ref tVect1, ref POS1, ref tVect2, ref POS2, _spline.road.clearDetailsDistance, TreeClearDist, ref _TTD, ref _spline);
                    if (_spline.road.isTreeModificationEnabled && tRect != null)
                    {
                        TreerectList.Add(tRect);
                    }
                }
                else
                {
                    if (index > 1f)
                    {
                        break;
                    }
                    tNext = index + tStep;
                    if (tNext > 1f)
                    {
                        break;
                    }

                    _spline.GetSplineValueBoth(index, out tVect1, out POS1);

                    if (tNext <= 1f)
                    {
                        _spline.GetSplineValueBoth(tNext, out tVect2, out POS2);
                    }
                    else
                    {
                        _spline.GetSplineValueBoth(1f, out tVect2, out POS2);
                    }

                    //Details and trees:
                    tRect = SetDetailCoords(index, ref tVect1, ref POS1, ref tVect2, ref POS2, _spline.road.clearDetailsDistance, TreeClearDist, ref _TTD, ref _spline);
                    if (_spline.road.isTreeModificationEnabled && tRect != null)
                    {
                        TreerectList.Add(tRect);
                    }
                }
            }

            RootUtils.StartProfiling(_spline.road, "DoRectsTree");
            if (_spline.road.isTreeModificationEnabled && TreerectList != null && TreerectList.Count > 0)
            {
                int tCount = _TTD.TreeSize;
                int jCount = TreerectList.Count;
                Vector3 tVect3D = default(Vector3);
                Vector2 tVect2D = default(Vector2);
                TreeInstance tTree;
                for (int i = 0; i < tCount; i++)
                {
                    tTree = _TTD.TreesCurrent[i];

                    tVect3D = tTree.position;
                    // B-17. A TreeInstance position is normalised 0..1 in the terrain's OWN axes, so .x scales by the
                    // terrain's X length and .z by its Z length. These two were crossed, which -- like the coordinate
                    // conversion at the bottom of this file -- is invisible on a square terrain and moves every tree
                    // by the difference on any other. The two axes are read back into tVect2D two lines down and
                    // tested against the road's rectangles, so a crossed pair spares the trees that should be cleared
                    // and clears the ones that should be spared.
                    tVect3D.x *= _TTD.TerrainSize.x;
                    tVect3D.y *= _TTD.TerrainSize.y;
                    tVect3D.z *= _TTD.TerrainSize.z;
                    tVect3D += _TTD.TerrainPos;
                    tVect2D.x = tVect3D.x;
                    tVect2D.y = tVect3D.z;

                    for (int j = 0; j < jCount; j++)
                    {
                        if (TreerectList[j].Contains(ref tVect2D))
                        {
                            _TTD.TreesOld.Add(_TTD.TreesCurrent[i]);
                            tTree = _TTD.TreesCurrent[i];
                            tTree.prototypeIndex = -2;
                            _TTD.TreesCurrent[i] = tTree;
                            _TTD.treesCount += 1;
                            break;
                        }
                    }
                }
                _TTD.TreesCurrent.RemoveAll(item => item.prototypeIndex < -1);
            }
            RootUtils.EndProfiling(_spline.road);

            if (!_spline.road.isHeightModificationEnabled)
            {
                return;
            }

            ////Temp testing:
            //tSpline.mNodes[22].tTriList = new List<RoadUtility.Construction3DTri>();
            //int tCount = triList.Count;
            //for(int i=0;i<tCount;i++)
            //{
            //	tSpline.mNodes[22].tTriList.Add(triList[i]);	
            //}
            //tSpline.mNodes[22].tHMList = new List<Vector3>();


            float tFloat = -1f;
            // B-12. `Sep = RoadWidth() * 1.5f;` stood here and was never read again -- the loop below works from
            // tXYs and TBMList, and ProcessCoordinateGrabber takes no separation. It was removed rather than left
            // looking like it widened something, because a second, wider separation appearing at this point is
            // precisely what makes the deformation width hard to reason about.
            int k = 0;
            int[] tXY = null;
            int tXYsCount = tXYs.Count;
            bool isBridge = false;
            bool isTunnel = false;
            for (float index = StartMin; index < FinalMax; index += tStep)
            {
                if (TBMList.Count > 0)
                {
                    if (TBMList[0].MaxI < index)
                    {
                        CleanupTris(index, ref TBMList);
                    }
                }
                else
                {
                    break;
                }

                //If in bridge mode:
                if (_spline.IsInBridgeTerrain(index))
                {
                    isBridge = true;
                }
                else
                {
                    isBridge = false;
                }
                //If in tunnel mode:
                if (_spline.IsInTunnelTerrain(index))
                {
                    isTunnel = true;
                }
                else
                {
                    isTunnel = false;
                }

                if (k < tXYsCount)
                {
                    tXY = tXYs[k];
                    tFloat = ProcessCoordinateGrabber(ref index, ref _spline, ref _TTD, ref TBMList, ref tXY, isBridge, isTunnel);
                    if (!RootUtils.IsApproximately(tFloat, 0f, 0.0001f))
                    {
                        _spline.HeightHistory.Add(new KeyValuePair<float, float>(index, tFloat));
                    }
                }
                else
                {
                    break;
                }
                k += 1;
            }
        }


        private static void CleanupTris(float _currentI, ref List<TerrainBoundsMaker> _terrainBoundsList)
        {
            int mCount = _terrainBoundsList.Count;
            int LastIndexToRemove = -1;
            for (int index = 0; index < mCount; index++)
            {
                if (_terrainBoundsList[index].MaxI < _currentI)
                {
                    LastIndexToRemove = index;
                }
                else
                {
                    break;
                }
            }
            if (LastIndexToRemove >= 0)
            {
                _terrainBoundsList.RemoveRange(0, LastIndexToRemove);
            }
            //			
            //mCount = rectList.Count;
            //LastIndexToRemove = -1;
            //for(int i=0;i<mCount;i++)
            //{
            //  if(tList[i].MaxI < CurrentI)
            //  {
            //	    LastIndexToRemove = i;
            //  }
            //  else
            //  {
            //	    break;
            //  }
            //}
            //if(LastIndexToRemove >= 0)
            //{
            //	rectList.RemoveRange(0,LastIndexToRemove);
            //}
        }


        private static int[] CreateTris(float _i, float _i2, ref Vector3 _vect1, ref Vector3 _POS1, ref Vector3 _vect2, ref Vector3 _POS2, float _sep, ref List<TerrainBoundsMaker> _list, ref float _T1SUB, ref float _T2SUB, ref Terraforming.TempTerrainData _TTD, float _heightSep, ref SplineC _spline)
        {
            Vector3 lVect1 = (_vect1 + new Vector3(_sep * -_POS1.normalized.z, 0, _sep * _POS1.normalized.x));
            Vector3 rVect1 = (_vect1 + new Vector3(_sep * _POS1.normalized.z, 0, _sep * -_POS1.normalized.x));
            Vector3 lVect2 = (_vect2 + new Vector3(_sep * -_POS2.normalized.z, 0, _sep * _POS2.normalized.x));
            Vector3 rVect2 = (_vect2 + new Vector3(_sep * _POS2.normalized.z, 0, _sep * -_POS2.normalized.x));

            lVect1.y = _T1SUB;
            rVect1.y = _T1SUB;
            lVect2.y = _T2SUB;
            rVect2.y = _T2SUB;

            TerrainBoundsMaker TBM = new TerrainBoundsMaker();
            TBM.triList = new List<Construction3DTri>();

            TBM.triList.Add(new Construction3DTri(lVect1, rVect1, lVect2, _i, _i2));
            TBM.triList.Add(new Construction3DTri(lVect2, rVect1, rVect2, _i, _i2));

            // B-12. The outer ring is a BATTER that daylights, not a shelf. Each of the four corners gets its own
            // outward reach -- whatever the authored gradient needs to travel from the carriageway edge down (or up)
            // to natural ground at that corner -- and then sits AT natural ground rather than at road level. Two
            // things follow. Ground that is already close to road level is barely touched, which is the reported
            // defect gone: there is no longer a fixed apron to bulge. And because the far corner is on natural
            // ground, the triangle between it and the road edge IS the batter plane, so the interpolation that
            // follows produces a slope instead of a step. The reach is bounded by what DoRectsDo allowed, which is
            // what keeps the write buffers sized as they were.
            Vector3 lDir1 = new Vector3(-_POS1.normalized.z, 0f, _POS1.normalized.x);
            Vector3 rDir1 = new Vector3(_POS1.normalized.z, 0f, -_POS1.normalized.x);
            Vector3 lDir2 = new Vector3(-_POS2.normalized.z, 0f, _POS2.normalized.x);
            Vector3 rDir2 = new Vector3(_POS2.normalized.z, 0f, -_POS2.normalized.x);

            float maxReach = _heightSep - _sep;
            if (maxReach < 0f) { maxReach = 0f; }
            Road road = _spline != null ? _spline.road : null;
            float lReach1 = BatterReachMetres(lVect1.y, ref lVect1, ref lDir1, road, ref _TTD, maxReach);
            float rReach1 = BatterReachMetres(rVect1.y, ref rVect1, ref rDir1, road, ref _TTD, maxReach);
            float lReach2 = BatterReachMetres(lVect2.y, ref lVect2, ref lDir2, road, ref _TTD, maxReach);
            float rReach2 = BatterReachMetres(rVect2.y, ref rVect2, ref rDir2, road, ref _TTD, maxReach);

            Vector3 lVect1far = (_vect1 + (lDir1 * (_sep + lReach1)));
            Vector3 rVect1far = (_vect1 + (rDir1 * (_sep + rReach1)));
            Vector3 lVect2far = (_vect2 + (lDir2 * (_sep + lReach2)));
            Vector3 rVect2far = (_vect2 + (rDir2 * (_sep + rReach2)));

            // The far corner takes NATURAL ground, so the batter has something to daylight against. Where natural
            // ground cannot be read -- no snapshot, or a corner off the terrain -- it falls back to the road-edge
            // height, which is exactly the old behaviour, so an unreadable sample degrades instead of throwing.
            float naturalY;
            if (!NaturalWorldHeight(ref lVect1far, ref _TTD, out naturalY)) { naturalY = lVect1.y; }
            lVect1far.y = naturalY;
            if (!NaturalWorldHeight(ref lVect2far, ref _TTD, out naturalY)) { naturalY = lVect2.y; }
            lVect2far.y = naturalY;
            if (!NaturalWorldHeight(ref rVect1far, ref _TTD, out naturalY)) { naturalY = rVect1.y; }
            rVect1far.y = naturalY;
            if (!NaturalWorldHeight(ref rVect2far, ref _TTD, out naturalY)) { naturalY = rVect2.y; }
            rVect2far.y = naturalY;

            // B-12. The batter's PROFILE, not just its width. A single quad from the road edge to the daylight line
            // is a straight ramp, and a straight ramp meets the flat band at an angle and natural ground at another
            // -- two creases, which is what TerrainFootprint was written to remove ("not a linear ramp, which leaves
            // a visible crease"). So each side is emitted in rings and every ring's height comes from
            // TerrainFootprint.DeformedHeightMetres at that ring's real lateral distance, with the carriageway edge
            // as the flattened half-width and the daylight line as the blend outer half-width. The curve's
            // derivative is zero at both ends, so the batter leaves the shoulder and arrives at natural ground
            // tangentially. The WIDTH still comes from the march above -- this changes the shape of the slope, not
            // how far it travels.
            int lRings = BatterRingCount(lReach1 > lReach2 ? lReach1 : lReach2, _TTD.MetersPerHM);
            int rRings = BatterRingCount(rReach1 > rReach2 ? rReach1 : rReach2, _TTD.MetersPerHM);
            AddBatterRings(ref TBM.triList, lRings, _sep, _i, _i2, ref _vect1, ref lDir1, lReach1, lVect1.y,
                lVect1far.y, ref _vect2, ref lDir2, lReach2, lVect2.y, lVect2far.y);
            AddBatterRings(ref TBM.triList, rRings, _sep, _i, _i2, ref _vect1, ref rDir1, rReach1, rVect1.y,
                rVect1far.y, ref _vect2, ref rDir2, rReach2, rVect2.y, rVect2far.y);

            TBM.constructRect = new Construction2DRect(new Vector2(lVect1far.x, lVect1far.z), new Vector2(rVect1far.x, rVect1far.z), new Vector2(rVect2far.x, rVect2far.z), new Vector2(lVect2far.x, lVect2far.z), 0f);
            //tRect.MinI = i;
            //tRect.MaxI = i2;

            TBM.MinI = _i;
            TBM.MaxI = _i2;

            _list.Add(TBM);

            int[] Xs = new int[4];
            int[] Ys = new int[4];

            int x1, y1;
            GetTempHeightsCoordinates(ref lVect1far, ref _TTD, out x1, out y1);
            Xs[0] = x1;
            Ys[0] = y1;
            GetTempHeightsCoordinates(ref lVect2far, ref _TTD, out x1, out y1);
            Xs[1] = x1;
            Ys[1] = y1;
            GetTempHeightsCoordinates(ref rVect1far, ref _TTD, out x1, out y1);
            Xs[2] = x1;
            Ys[2] = y1;
            GetTempHeightsCoordinates(ref rVect2far, ref _TTD, out x1, out y1);
            Xs[3] = x1;
            Ys[3] = y1;

            int Min = Mathf.Min(Xs);
            int Max = Mathf.Max(Xs);
            Xs[0] = Min - 2;
            Xs[2] = Max + 2;
            Min = Mathf.Min(Ys);
            Max = Mathf.Max(Ys);
            Xs[1] = Min - 2;
            Xs[3] = Max + 2;

            return Xs;
        }


        private static Construction2DRect SetDetailCoords(float _param, ref Vector3 _vect1, ref Vector3 _POS1, ref Vector3 _vect2, ref Vector3 POS2, float _sep, float _treeSep, ref Terraforming.TempTerrainData _TTD, ref SplineC _spline)
        {
            Vector3 lVect1far = default(Vector3);
            Vector3 rVect1far = default(Vector3);
            Vector3 lVect2far = default(Vector3);
            Vector3 rVect2far = default(Vector3);

            bool isInBridge = _spline.IsInBridgeTerrain(_param);
            bool isInTunnel = _spline.IsInTunnelTerrain(_param);
            int x2, y2, x3, y3;
            GetTempHeightsCoordinates(ref _vect1, ref _TTD, out x2, out y2);
            if (x2 >= _TTD.HM)
            {
                x2 = -1;
            }
            if (y2 >= _TTD.HM)
            {
                y2 = -1;
            }
            if (x2 < 0)
            {
                x2 = -1;
            }
            if (y2 < 0)
            {
                y2 = -1;
            }
            if (x2 == -1)
            {
                return null;
            }
            if (y2 == -1)
            {
                return null;
            }

            // B-17. The tile's own base plane has to be added back before a heightmap sample can be compared with a
            // world Y. Without it this difference carries the terrain object's elevation as an error, so on a tile at
            // Y = -200 every tree and detail near a bridge or a tunnel was measured 200 m out and cleared, or spared,
            // for the wrong reason. Same omission the write path had; same fix -- and through the same conversion, so
            // there is one place where a heightmap sample becomes a world height.
            float tDiff1 = RoadNodeGround.FromHeightmap01(_TTD.heights[x2, y2], _TTD.TerrainPos.y,
                _TTD.TerrainSize.y) - _vect1.y;
            GetTempHeightsCoordinates(ref _vect2, ref _TTD, out x3, out y3);
            if (x3 >= _TTD.HM)
            {
                x3 = -1;
            }
            if (y3 >= _TTD.HM)
            {
                y3 = -1;
            }
            if (x3 < 0)
            {
                x3 = -1;
            }
            if (y3 < 0)
            {
                y3 = -1;
            }
            if (x3 == -1)
            {
                return null;
            }
            if (y3 == -1)
            {
                return null;
            }
            float tDiff2 = RoadNodeGround.FromHeightmap01(_TTD.heights[x3, y3], _TTD.TerrainPos.y,
                _TTD.TerrainSize.y) - _vect2.y;



            Construction2DRect tRect = null;
            if (_spline.road.isTreeModificationEnabled)
            {
                bool isQuit = false;
                if (x2 == -1)
                {
                    isQuit = true;
                }
                if (y2 == -1)
                {
                    isQuit = true;
                }

                if (isInBridge && !isQuit)
                {
                    if (tDiff1 < 0f)
                    {
                        tDiff1 *= -1f;
                    }
                    if (tDiff2 < 0f)
                    {
                        tDiff2 *= -1f;
                    }
                    if (tDiff1 > _spline.road.clearTreesDistanceHeight)
                    {
                        isQuit = true;
                    }
                    if (tDiff2 > _spline.road.clearTreesDistanceHeight)
                    {
                        isQuit = true;
                    }
                }
                if (isInTunnel && !isQuit)
                {
                    if (tDiff1 < 0f)
                    {
                        if ((tDiff1 * -1f) > _spline.road.clearTreesDistanceHeight)
                        {
                            isQuit = true;
                        }
                    }
                    else
                    {
                        if (tDiff1 > (_spline.road.clearTreesDistanceHeight * 0.1f))
                        {
                            isQuit = true;
                        }
                    }
                    if (tDiff2 < 0f)
                    {
                        if ((tDiff2 * -1f) > _spline.road.clearTreesDistanceHeight)
                        {
                            isQuit = true;
                        }
                    }
                    else
                    {
                        if (tDiff2 > (_spline.road.clearTreesDistanceHeight * 0.1f))
                        {
                            isQuit = true;
                        }
                    }
                }

                if (!isQuit)
                {
                    _treeSep = _treeSep * 0.5f;
                    lVect1far = (_vect1 + new Vector3(_treeSep * -_POS1.normalized.z, 0, _treeSep * _POS1.normalized.x));
                    rVect1far = (_vect1 + new Vector3(_treeSep * _POS1.normalized.z, 0, _treeSep * -_POS1.normalized.x));
                    lVect2far = (_vect2 + new Vector3(_treeSep * -POS2.normalized.z, 0, _treeSep * POS2.normalized.x));
                    rVect2far = (_vect2 + new Vector3(_treeSep * POS2.normalized.z, 0, _treeSep * -POS2.normalized.x));
                    tRect = new Construction2DRect(new Vector2(lVect1far.x, lVect1far.z), new Vector2(rVect1far.x, rVect1far.z), new Vector2(rVect2far.x, rVect2far.z), new Vector2(lVect2far.x, lVect2far.z), 0f);
                }
            }

            if (_spline.road.isDetailModificationEnabled)
            {
                if (isInBridge || isInTunnel)
                {
                    if (tDiff1 < 0f)
                    {
                        tDiff1 *= -1f;
                    }
                    if (tDiff2 < 0f)
                    {
                        tDiff2 *= -1f;
                    }

                    bool isQuit = false;
                    if (x2 == -1)
                    {
                        isQuit = true;
                    }
                    if (y2 == -1)
                    {
                        isQuit = true;
                    }

                    if (tDiff1 > _spline.road.clearDetailsDistanceHeight)
                    {
                        isQuit = true;
                    }
                    if (tDiff2 > _spline.road.clearDetailsDistanceHeight)
                    {
                        isQuit = true;
                    }

                    if (isQuit)
                    {
                        return tRect;
                    }
                }

                _sep = _sep * 0.5f;

                lVect1far = (_vect1 + new Vector3(_sep * -_POS1.normalized.z, 0, _sep * _POS1.normalized.x));
                rVect1far = (_vect1 + new Vector3(_sep * _POS1.normalized.z, 0, _sep * -_POS1.normalized.x));
                lVect2far = (_vect2 + new Vector3(_sep * -POS2.normalized.z, 0, _sep * POS2.normalized.x));
                rVect2far = (_vect2 + new Vector3(_sep * POS2.normalized.z, 0, _sep * -POS2.normalized.x));

                int[] Xs = new int[4];
                int[] Ys = new int[4];

                int x1, y1;
                GetTempDetailsCoordinates(ref lVect1far, ref _TTD, out x1, out y1);
                Xs[0] = x1;
                Ys[0] = y1;
                GetTempDetailsCoordinates(ref lVect2far, ref _TTD, out x1, out y1);
                Xs[1] = x1;
                Ys[1] = y1;
                GetTempDetailsCoordinates(ref rVect1far, ref _TTD, out x1, out y1);
                Xs[2] = x1;
                Ys[2] = y1;
                GetTempDetailsCoordinates(ref rVect2far, ref _TTD, out x1, out y1);
                Xs[3] = x1;
                Ys[3] = y1;

                //if(TTD.DetailLayersCount == 1 && x1 > 0 && y1 > 0)
                //{
                //	Debug.Log(Xs[0]+","+Ys[0] + " " + Xs[1]+","+Ys[1]);
                //}

                int MinX = Mathf.Min(Xs);
                int MinY = Mathf.Min(Ys);
                int MaxX = Mathf.Max(Xs);
                int MaxY = Mathf.Max(Ys);

                if (MinX >= _TTD.DetailMaxIndex)
                {
                    MinX = _TTD.DetailMaxIndex - 1;
                }
                if (MinY >= _TTD.DetailMaxIndex)
                {
                    MinY = _TTD.DetailMaxIndex - 1;
                }
                if (MaxX >= _TTD.DetailMaxIndex)
                {
                    MaxX = _TTD.DetailMaxIndex - 1;
                }
                if (MaxY >= _TTD.DetailMaxIndex)
                {
                    MaxY = _TTD.DetailMaxIndex - 1;
                }

                if (MinX < 0)
                {
                    MinX = 0;
                }
                if (MinY < 0)
                {
                    MinY = 0;
                }
                if (MaxX < 0)
                {
                    MaxX = 0;
                }
                if (MaxY < 0)
                {
                    MaxY = 0;
                }

                //int DetailI = 0;
                RootUtils.StartProfiling(_spline.road, "Dorectsdetails");
                int tInt = 0;
                for (int index = MinX; index <= MaxX; index++)
                {
                    for (int k = MinY; k <= MaxY; k++)
                    {
                        //Bitfield for identification:
                        tInt = k;
                        tInt = tInt << 16;
                        tInt = tInt | (ushort)index;
                        if (!_TTD.DetailHasProcessed.Contains(tInt))
                        {
                            //for(int h=0;h<TTD.DetailLayersCount;h++)
                            //{
                            //  if(TTD.DetailLayersSkip.Contains(h))
                            //  {
                            //      continue;
                            //  }
                            //	if(!TTD.DetailHasProcessed[h][i,k]){// && TTD.DetailValues[h][i,k] > 0){

                            _TTD.MainDetailsX.Add((ushort)index);
                            _TTD.MainDetailsY.Add((ushort)k);

                            //DetailI = TTD.DetailsI[h];

                            //TTD.DetailsX[h].Add((ushort)i);
                            //TTD.DetailsY[h].Add((ushort)k);

                            //TTD.DetailsX[h][DetailI] = (ushort)i;	
                            //TTD.DetailsY[h][DetailI] = (ushort)k;
                            //TTD.OldDetailsValue[h][DetailI] = (ushort)TTD.DetailValues[h][i,k];
                            //TTD.DetailValues[h][i,k] = 0;

                            //TTD.DetailsI[h]+=1;

                            //}
                            _TTD.DetailHasProcessed.Add(tInt);
                        }
                    }
                }
                RootUtils.EndProfiling(_spline.road);
            }

            return tRect;
        }


        private static float ProcessCoordinateGrabber(ref float _param, ref SplineC _spline, ref Terraforming.TempTerrainData _TTD, ref List<TerrainBoundsMaker> _terrainList, ref int[] _XY, bool _isBridge, bool _isTunnel)
        {
            int MinX = _XY[0];
            int MinY = _XY[1];
            int MaxX = _XY[2];
            int MaxY = _XY[3];

            if (MinX >= _TTD.TerrainMaxIndex)
            {
                MinX = _TTD.TerrainMaxIndex - 1;
            }
            if (MinY >= _TTD.TerrainMaxIndex)
            {
                MinY = _TTD.TerrainMaxIndex - 1;
            }
            if (MaxX >= _TTD.TerrainMaxIndex)
            {
                MaxX = _TTD.TerrainMaxIndex - 1;
            }
            if (MaxY >= _TTD.TerrainMaxIndex)
            {
                MaxY = _TTD.TerrainMaxIndex - 1;
            }

            if (MinX < 0)
            {
                MinX = 0;
            }
            if (MinY < 0)
            {
                MinY = 0;
            }
            if (MaxX < 0)
            {
                MaxX = 0;
            }
            if (MaxY < 0)
            {
                MaxY = 0;
            }

            Vector3 xVect = default(Vector3);
            bool isAdjusted = false;
            float tHeight = -1f;
            float tReturnFloat = 0f;

            for (int index = MinX; index <= MaxX; index++)
            {
                for (int k = MinY; k <= MaxY; k++)
                {
                    if (_TTD.tHeights[index, k] != true)
                    {
                        if (_TTD.cX.Length <= _TTD.Count)
                        {
                            break;
                        }

                        xVect = ConvertTerrainCoordToWorldVect(index, k, _TTD.heights[index, k], ref _TTD);
                        AdjustedTerrainVectTri(ref _param, out isAdjusted, out tHeight, ref xVect, ref _terrainList, _isBridge, _isTunnel);

                        if (isAdjusted)
                        {
                            // B-12. The subtraction sets the matched ground just under the road SURFACE so it cannot
                            // poke through the carriageway. On the batter that reasoning runs out: near the daylight
                            // line the surface already is natural ground, and subtracting there digs a trench along
                            // the road -- the same step the fix exists to remove, only moved outward. So the
                            // subtraction may not carry the written height PAST natural ground. Under the flat band
                            // the height is nowhere near natural and this never binds; it touches the last sample or
                            // two, which is what feathering was supposed to mean. In a CUT (road below natural) the
                            // ground must keep coming down, so the clamp deliberately does not apply.
                            float rawHeight = tHeight;
                            tHeight -= _spline.road.matchTerrainSubtraction;
                            if (_TTD.naturalHeights != null)
                            {
                                float naturalWorldY = RoadNodeGround.FromHeightmap01(
                                    _TTD.naturalHeights[index, k], _TTD.TerrainPos.y, _TTD.TerrainSize.y);
                                if (rawHeight > naturalWorldY && tHeight < naturalWorldY)
                                {
                                    tHeight = naturalWorldY;
                                }
                            }
                            // B-17. Clamped to the tile's OWN base plane, not to world Y = 0, and the shortfall is
                            // counted rather than swallowed -- see RoadNodeGround.ClampToGroundFloor.
                            float shortfall;
                            tHeight = RoadNodeGround.ClampToGroundFloor(tHeight, _TTD.TerrainPos.y, out shortfall);
                            RecordCutFloor(shortfall);
                            xVect.y = tHeight;
                            tHeight = RoadNodeGround.ToHeightmap01(tHeight, _TTD.TerrainPos.y, _TTD.TerrainSize.y);

                            //Set height values:
                            _TTD.tHeights[index, k] = true;
                            _TTD.cX[_TTD.Count] = (ushort)index;
                            _TTD.cY[_TTD.Count] = (ushort)k;
                            _TTD.oldH[_TTD.Count] = _TTD.heights[index, k];
                            _TTD.heights[index, k] = tHeight;
                            _TTD.Count += 1;

                            tReturnFloat = xVect.y;
                        }
                    }
                    else
                    {
                        xVect = ConvertTerrainCoordToWorldVect(index, k, _TTD.heights[index, k], ref _TTD);
                        AdjustedTerrainVectTri(ref _param, out isAdjusted, out tHeight, ref xVect, ref _terrainList, _isBridge, _isTunnel);

                        if (isAdjusted)
                        {
                            // B-12. Same clamp as the write path above: the subtraction must not carry the reported
                            // height past natural ground, or the two paths would disagree about the same cell.
                            float rawHeight = tHeight;
                            tHeight -= _spline.road.matchTerrainSubtraction;
                            if (_TTD.naturalHeights != null)
                            {
                                float naturalWorldY = RoadNodeGround.FromHeightmap01(
                                    _TTD.naturalHeights[index, k], _TTD.TerrainPos.y, _TTD.TerrainSize.y);
                                if (rawHeight > naturalWorldY && tHeight < naturalWorldY)
                                {
                                    tHeight = naturalWorldY;
                                }
                            }
                            // B-17. And the same floor, for the same reason: this branch reports the height of a cell
                            // the write path already claimed, so a different floor here would have the two disagreeing
                            // about the same sample. The shortfall is not counted twice -- the cell was counted when it
                            // was written.
                            float alreadyWritten;
                            tHeight = RoadNodeGround.ClampToGroundFloor(tHeight, _TTD.TerrainPos.y,
                                out alreadyWritten);
                            tReturnFloat = tHeight;
                        }
                    }
                }
            }

            if (_isBridge && RootUtils.IsApproximately(tReturnFloat, 0f, 0.0001f))
            {
                tReturnFloat = _spline.GetSplineValue(_param, false).y;
            }

            return tReturnFloat;
        }


        private static Vector3 ConvertTerrainCoordToWorldVect(int _x, int _y, float _height, ref Terraforming.TempTerrainData _TTD)
        {
            //Get the normalized position of this game object relative to the terrain:
            // B-17. The FIRST heightmap index runs along the terrain's Z axis and the second along X -- which is what
            // GetTempHeightsCoordinates in this same file has always assumed, dividing by TerrainSize.z to get the
            // first index and TerrainSize.x to get the second. This method had the two lengths the other way round, so
            // it was the inverse of that lookup only on a SQUARE terrain, where the two lengths are equal and the
            // error cancels. On a 2000 x 1000 terrain it placed every converted sample at the wrong station, in
            // proportion to how far from square the tile is.
            float x1 = _x / ((float)_TTD.HM - 1f);
            x1 = x1 * _TTD.TerrainSize.z;

            float z1 = _y / ((float)_TTD.HM - 1f);
            z1 = z1 * _TTD.TerrainSize.x;

            float y1 = _height * _TTD.TerrainSize.y;

            Vector3 xVect = new Vector3(z1, y1, x1);
            xVect += _TTD.TerrainPos;

            return xVect;
        }


        private static void AdjustedTerrainVectTri(ref float _param, out bool _isAdjusted, out float _height, ref Vector3 _vect, ref List<TerrainBoundsMaker> _terrainList, bool _isBridge, bool _isTunnel)
        {
            float OrigHeight = _vect.y;
            int mCount = _terrainList.Count;
            int tCount = 0;
            Construction3DTri tTri;
            TerrainBoundsMaker TBM;
            _isAdjusted = false;
            _height = 0f;
            Vector2 t2D = new Vector2(_vect.x, _vect.z);
            for (int index = 0; index < mCount; index++)
            {
                TBM = _terrainList[index];
                if (_param < TBM.MinI)
                {
                    return;
                }
                if (_param > TBM.MaxI)
                {
                    continue;
                }
                if (TBM.constructRect.Contains(ref t2D))
                {
                    tCount = TBM.triList.Count;
                    for (int k = 0; k < tCount; k++)
                    {
                        tTri = TBM.triList[k];
                        if (tTri.Contains2D(ref t2D))
                        {
                            _height = tTri.LinePlaneIntersection(ref _vect).y;
                            if (_isBridge)
                            {
                                if (OrigHeight > (_height - 0.03f))
                                {
                                    _height -= 0.03f;
                                    _isAdjusted = true;
                                    return;
                                }
                            }
                            else if (_isTunnel)
                            {
                                // RM-98. The ground over a tunnel is raised to real cover, and NEVER lowered.
                                //
                                // What was here was `_height + 0.03f` -- three centimetres, which is a rendering nudge
                                // to stop the road mesh poking through the terrain, not a depth of ground. Two things
                                // followed from it. Where the terrain sat below the road it was raised to three
                                // centimetres over the carriageway, so the "tunnel" was a road with a crust on it.
                                // Where the terrain sat higher it was left alone, which was right, but only by an
                                // accident of the comparison rather than by a rule anyone could point at -- and a hill
                                // standing half a metre above the road passed that test while still looking sliced open.
                                //
                                // TunnelGroundTopAboveRoadMetres is the rule: the traffic clearance, the structural
                                // roof, and the cover over it, read from the same two setting classes the City layer
                                // reads (§ 10.24), so the two halves of the tool cannot disagree about how deep a
                                // tunnel is. The comparison keeps its original shape, so a sample whose ground is
                                // already deeper than that falls through untouched exactly as before -- that is the
                                // hill staying intact, and it is now a stated rule instead of a side effect.
                                float tunnelGroundTop = _height + TunnelGroundTopAboveRoad;
                                if (OrigHeight < tunnelGroundTop)
                                {
                                    _height = tunnelGroundTop;
                                    _isAdjusted = true;
                                    return;
                                }
                            }
                            else
                            {
                                _isAdjusted = true;
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}

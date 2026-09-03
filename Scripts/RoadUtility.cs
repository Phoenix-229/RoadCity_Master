#region "Imports"
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
#endregion


namespace RoadCityMaster
{
    //Generic http://www.fhwa.dot.gov/bridge/bridgerail/br053504.cfm
    public enum RailingTypeEnum { None, Generic1, Generic2, K_Rail, WBeam };
    public enum RailingSubTypeEnum { Both, Left, Right };
    public enum SignPlacementSubTypeEnum { Center, Left, Right };
    public enum CenterDividerTypeEnum { None, K_Rail, KRail_Blinds, Wire, Markers };
    public enum EndCapTypeEnum { None, WBeam, Barrels3Static, Barrels3Rigid, Barrels7Static, Barrels7Rigid };
    public enum RoadUpdateTypeEnum { Full, Intersection, Railing, CenterDivider, Bridges };
    public enum AxisTypeEnum { X, Y, Z };


    public static class RoadUtility
    {
        public const string FileSepString = "\n#### RoadCityMaster ####\n";
        public const string FileSepStringCRLF = "\r\n#### RoadCityMaster ####\r\n";
        // The separator is a record marker written into every .rao on disk, not just a display string.
        // Presets shipped before the rename — and every group a user saved before it — still carry the
        // old marker, so both spellings must remain readable. Writing only ever uses the new pair.
        public const string FileSepStringLegacy = "\n#### RoadArchitect ####\n";
        public const string FileSepStringLegacyCRLF = "\r\n#### RoadArchitect ####\r\n";


        /// <summary> The four spellings of the record separator a `.rao` on disk may use: the current marker and the
        /// pre-rename one, each in LF and CRLF. Handed out as a fresh array because <c>string.Split</c> takes one and
        /// a shared static could be written to by a caller. This exists so the list is stated ONCE — it was previously
        /// built by hand in both readers, which is two places to forget the legacy pair. </summary>
        public static string[] RecordSeparators()
        {
            return new string[] { FileSepString, FileSepStringCRLF, FileSepStringLegacy, FileSepStringLegacyCRLF };
        }


        /// <summary> Reads a `.rao` off disk and splits it into records, or returns null having set
        /// <paramref name="_reason"/> to a sentence an author can act on. Both readers used to call
        /// <c>File.ReadAllText</c> unguarded, so a library file that had been moved, renamed or was still being written
        /// threw an <c>IOException</c> out through the inspector's OnGUI — which Unity reports as a stack trace with the
        /// file name buried in it, if it reports it at all. An empty file is refused here too rather than returning zero
        /// records: zero records is indistinguishable from "the preset contained nothing", and silently adding nothing
        /// to the node is the substitution RM-55 forbids. </summary>
        public static string[] ReadRaoRecords(string _filePath, out string _reason)
        {
            _reason = null;
            if (string.IsNullOrEmpty(_filePath))
            {
                _reason = "was asked for with no path";
                return null;
            }
            if (!File.Exists(_filePath))
            {
                _reason = "does not exist — it has been moved, renamed, or was never installed";
                return null;
            }

            string fileData;
            try
            {
                fileData = File.ReadAllText(_filePath);
            }
            catch (IOException exception)
            {
                _reason = "could not be read: " + exception.Message;
                return null;
            }
            catch (System.UnauthorizedAccessException exception)
            {
                _reason = "could not be read: " + exception.Message;
                return null;
            }

            string[] records = fileData.Split(RecordSeparators(), System.StringSplitOptions.RemoveEmptyEntries);
            if (records.Length < 1)
            {
                _reason = "is empty — there are no records in it";
                return null;
            }
            return records;
        }


        /// <summary> Returns closest terrain to _vect </summary>
        public static Terrain GetTerrain(Vector3 _vect)
        {
            Terrain terrain;
            //Sphere cast 5m first. Then raycast down 1000m, then up 1000m.
            Collider[] colliders = Physics.OverlapSphere(_vect, 10f);
            if (colliders != null)
            {
                int collidersLength = colliders.Length;
                for (int index = 0; index < collidersLength; index++)
                {
                    terrain = colliders[index].transform.GetComponent<Terrain>();
                    if (terrain)
                    {
                        colliders = null;
                        return terrain;
                    }
                }
                colliders = null;
            }

            RaycastHit[] hits;
            hits = Physics.RaycastAll(_vect, Vector3.down, 1000f);
            int hitsLength = 0;
            if (hits != null)
            {
                hitsLength = hits.Length;
                for (int index = 0; index < hitsLength; index++)
                {
                    terrain = hits[index].collider.transform.GetComponent<Terrain>();
                    if (terrain)
                    {
                        hits = null;
                        return terrain;
                    }
                }
                hits = null;
            }

            hits = Physics.RaycastAll(_vect, Vector3.up, 1000f);
            if (hits != null)
            {
                hitsLength = hits.Length;
                for (int i = 0; i < hitsLength; i++)
                {
                    terrain = hits[i].collider.transform.GetComponent<Terrain>();
                    if (terrain)
                    {
                        hits = null;
                        return terrain;
                    }
                }
                hits = null;
            }
            return null;
        }


        #region "Terrain history"
        public static void ConstructRoadStoreTerrainHistory(ref Road _road)
        {
            Object[] allTerrains = EngineIntegration.FindObjectsByType<RoadTerrain>();

            HashSet<int> terrainIDs = new HashSet<int>();
            foreach (RoadTerrain terrain in allTerrains)
            {
                terrainIDs.Add(terrain.UID);
            }

            if (_road.TerrainHistory == null)
            {
                _road.TerrainHistory = new List<TerrainHistoryMaker>();
            }

            if (_road.TerrainHistory.Count > 0)
            {
                //Delete unnecessary terrain histories:
                foreach (TerrainHistoryMaker THMaker in _road.TerrainHistory)
                {
                    if (!terrainIDs.Contains(THMaker.terrainID))
                    {
                        THMaker.Nullify();
                        _road.TerrainHistory.Remove(THMaker);
                    }
                }
            }


            TerrainHistoryMaker TH;
            RoadTerrain roadTerrain;
            foreach (Terraforming.TempTerrainData TTD in _road.EditorTTDList)
            {
                roadTerrain = null;
                //Get terrainID:
                foreach (RoadTerrain terrain in allTerrains)
                {
                    if (terrain.UID == TTD.uID)
                    {
                        roadTerrain = terrain;
                    }
                }

                if (roadTerrain == null)
                {
                    continue;
                }

                TH = null;
                int THCount = _road.TerrainHistory.Count;
                bool isContainingTID = false;
                for (int index = 0; index < THCount; index++)
                {
                    if (_road.TerrainHistory[index].terrainID == roadTerrain.UID)
                    {
                        isContainingTID = true;
                        TH = _road.TerrainHistory[index];
                        break;
                    }
                }

                if (!isContainingTID)
                {
                    TerrainHistoryMaker THMaker = new TerrainHistoryMaker();
                    THMaker.terrainID = roadTerrain.UID;
                    _road.TerrainHistory.Add(THMaker);
                    TH = THMaker;
                }

                
                if (TH == null)
                {
                    continue;
                }

                //Heights:
                if (_road.isHeightModificationEnabled)
                {
                    if (TTD.cX != null && TTD.cY != null)
                    {
                        TH.x1 = new int[TTD.Count];
                        System.Array.Copy(TTD.cX, 0, TH.x1, 0, TTD.Count);
                        TH.y1 = new int[TTD.Count];
                        System.Array.Copy(TTD.cY, 0, TH.y1, 0, TTD.Count);
                        TH.height = new float[TTD.Count];
                        System.Array.Copy(TTD.oldH, 0, TH.height, 0, TTD.Count);
                        TH.Count = TTD.Count;
                        TH.heightmapResolution = TTD.TerrainMaxIndex;
                    }
                }
                else
                {
                    TH.x1 = null;
                    TH.y1 = null;
                    TH.height = null;
                    TH.Count = 0;
                }

                //Details:
                if (_road.isDetailModificationEnabled)
                {
                    int TotalSize = 0;
                    for (int i = 0; i < TTD.DetailLayersCount; i++)
                    {
                        TotalSize += TTD.detailsCount[i];
                    }

                    TH.detailsX = new int[TotalSize];
                    TH.detailsY = new int[TotalSize];
                    TH.detailsOldValue = new int[TotalSize];

                    int RunningIndex = 0;
                    int cLength;
                    for (int index = 0; index < TTD.DetailLayersCount; index++)
                    {
                        cLength = TTD.detailsCount[index];
                        if (cLength < 1)
                        {
                            continue;
                        }
                        System.Array.Copy(TTD.DetailsX[index].ToArray(), 0, TH.detailsX, RunningIndex, cLength);
                        System.Array.Copy(TTD.DetailsY[index].ToArray(), 0, TH.detailsY, RunningIndex, cLength);
                        System.Array.Copy(TTD.OldDetailsValue[index].ToArray(), 0, TH.detailsOldValue, RunningIndex, cLength);
                        RunningIndex += TTD.detailsCount[index];
                    }

                    //TH.detailsX = TTD.detailsX;
                    //TH.detailsY = TTD.detailsY;
                    //TH.detailsOldValue = TTD.OldDetailsValue;
                    TH.detailsCount = TTD.detailsCount;
                    TH.detailLayersCount = TTD.DetailLayersCount;
                }
                else
                {
                    TH.detailsX = null;
                    TH.detailsY = null;
                    TH.detailsOldValue = null;
                    TH.detailsCount = null;
                    TH.detailLayersCount = 0;
                }

                //Trees:
                if (_road.isTreeModificationEnabled)
                {
                    if (TTD.TreesOld != null)
                    {
                        TH.MakeRATrees(ref TTD.TreesOld);
                        TTD.TreesOld.Clear();
                        TTD.TreesOld = null;
                        TH.treesCount = TTD.treesCount;
                    }
                }
                else
                {
                    TH.oldTrees = null;
                    TH.treesCount = 0;
                }
            }
        }


        /// <summary> Clears the terrain history of _road </summary>
        public static void ResetTerrainHistory(ref Road _road)
        {
            if (_road.TerrainHistory != null)
            {
                _road.TerrainHistory.Clear();
                _road.TerrainHistory = null;
            }
        }
        #endregion


        public static void SaveNodeObjects(ref Splination.SplinatedMeshMaker[] _splinatedObjects, ref EdgeObjects.EdgeObjectMaker[] _edgeObjects, ref WizardObject _wizardObj)
        {
            //Splinated objects first:
            RootUtils.CheckCreateSpecialLibraryDirs();
            string libraryPath = RootUtils.GetDirLibrary();
            string filePath = Path.Combine(Path.Combine(libraryPath, "Groups"), _wizardObj.fileName + ".rao");
            if (_wizardObj.isDefault)
            {
                filePath = Path.Combine(Path.Combine(libraryPath, "Groups"), "Default");
                filePath = Path.Combine(filePath, _wizardObj.fileName + ".rao");
            }
            StringBuilder builder = new StringBuilder(32768);

            //Wizard object:
            builder.Append(_wizardObj.ConvertToString());
            builder.Append(FileSepString);

            int sCount = _splinatedObjects.Length;
            Splination.SplinatedMeshMaker SMM = null;
            for (int index = 0; index < sCount; index++)
            {
                SMM = _splinatedObjects[index];
                builder.Append(SMM.ConvertToString());
                builder.Append(FileSepString);
            }

            int eCount = _edgeObjects.Length;
            EdgeObjects.EdgeObjectMaker EOM = null;
            for (int index = 0; index < eCount; index++)
            {
                EOM = _edgeObjects[index];
                builder.Append(EOM.ConvertToString());
                builder.Append(FileSepString);
            }

            File.WriteAllText(filePath, builder.ToString());
        }


        /// <summary> Loads a saved group preset onto <paramref name="_node"/> — its cross-sections and its edge objects.
        ///
        /// Returns true when the whole preset was applied. On refusal it returns false, having added NOTHING to the node
        /// and logged one sentence naming the file and what was wrong. That all-or-nothing rule is the point: the old
        /// code walked the records, offered each to both readers, and on a double null simply moved to the next one, so a
        /// truncated preset produced a road with a railing and no kerb, or a kerb and no posts, with nothing written
        /// anywhere. Records are therefore validated in full BEFORE any of them is applied — the loop below reads first
        /// and builds second. </summary>
        public static bool LoadNodeObjects(string _fileName, SplineN _node, bool _isDefault = false, bool _isBridge = false)
        {
            string filePath;
            RootUtils.CheckCreateSpecialLibraryDirs();
            string libraryPath = RootUtils.GetDirLibrary();
            if (_isDefault)
            {
                filePath = Path.Combine(Path.Combine(libraryPath, "Groups"), "Default");
                filePath = Path.Combine(filePath, _fileName + ".rao");
            }
            else
            {
                filePath = Path.Combine(Path.Combine(libraryPath, "Groups"), _fileName + ".rao");
            }

            string reason;
            string[] fileSplitted = ReadRaoRecords(filePath, out reason);
            if (fileSplitted == null)
            {
                Debug.LogError(RaoFormat.Refusal(_fileName + ".rao", -1, reason));
                return false;
            }


            Splination.SplinatedMeshMaker SMM;
            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker SLM;
            EdgeObjects.EdgeObjectMaker EOM;
            EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker ELM;
            int fileSplitCount = fileSplitted.Length;

            string origin = _fileName + ".rao";
            List<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker> sections =
                new List<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>();
            List<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker> edges =
                new List<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>();

            // ---- Pass one: read every record, apply none ----
            // A group preset legitimately holds three kinds of record, so a record of the wrong kind is skipped in
            // silence. Everything else is a refusal: XML that is not well-formed, a root element the tool does not
            // write, or a record of the right kind that will not deserialise.
            for (int index = 0; index < fileSplitCount; index++)
            {
                RaoFormat.RecordKind kind = RaoFormat.KindOf(fileSplitted[index]);

                if (kind == RaoFormat.RecordKind.Malformed)
                {
                    Debug.LogError(RaoFormat.Refusal(origin, index,
                        "is not well-formed XML — the file is truncated, damaged, or was still being written"));
                    return false;
                }
                if (kind == RaoFormat.RecordKind.Unrecognised)
                {
                    Debug.LogError(RaoFormat.Refusal(origin, index, "has root element <"
                        + RaoFormat.RootElementName(fileSplitted[index]) + ">, which is not one this tool writes"));
                    return false;
                }
                // The wizard header carries the preset's name and thumbnail, which belong to the browser, not a node.
                if (kind == RaoFormat.RecordKind.WizardHeader) { continue; }

                if (kind == RaoFormat.RecordKind.SplinatedSection)
                {
                    SLM = Splination.SplinatedMeshMaker.SLMFromData(fileSplitted[index], origin, out reason);
                    if (SLM == null)
                    {
                        Debug.LogError(RaoFormat.Refusal(origin, index, reason));
                        return false;
                    }
                    sections.Add(SLM);
                    continue;
                }

                ELM = EdgeObjects.EdgeObjectMaker.ELMFromData(fileSplitted[index], origin, out reason);
                if (ELM == null)
                {
                    Debug.LogError(RaoFormat.Refusal(origin, index, reason));
                    return false;
                }
                edges.Add(ELM);
            }

            // ---- Pass two: apply. Nothing here can fail, because pass one already read all of it ----
            int sectionCount = sections.Count;
            for (int index = 0; index < sectionCount; index++)
            {
                SLM = sections[index];
                SMM = _node.AddSplinatedObject();
                SMM.LoadFromLibraryBulk(ref SLM);
                SMM.isToggled = false;
                if (_isBridge && _node.isBridgeStart && _node.isBridgeMatched && _node.bridgeCounterpartNode != null)
                {
                    SMM.StartTime = _node.time;
                    SMM.EndTime = _node.bridgeCounterpartNode.time;
                    SMM.StartPos = _node.spline.GetSplineValue(SMM.StartTime);
                    SMM.EndPos = _node.spline.GetSplineValue(SMM.EndTime);
                }
            }

            int edgeCount = edges.Count;
            for (int index = 0; index < edgeCount; index++)
            {
                ELM = edges[index];
                EOM = _node.AddEdgeObject();
                EOM.LoadFromLibraryBulk(ref ELM);
                EOM.isToggled = false;
                if (!EOM.isSingle && _isBridge && _node.isBridgeStart && _node.isBridgeMatched && _node.bridgeCounterpartNode != null)
                {
                    EOM.startTime = _node.time;
                    EOM.endTime = _node.bridgeCounterpartNode.time;
                    EOM.startPos = _node.spline.GetSplineValue(EOM.startTime);
                    EOM.endPos = _node.spline.GetSplineValue(EOM.endTime);
                }
                else if (EOM.isSingle && _isBridge && _node.bridgeCounterpartNode != null && _node.isBridgeStart)
                {
                    float tDist = (EOM.singleOnlyBridgePercent * (_node.bridgeCounterpartNode.dist - _node.dist) + _node.dist);
                    EOM.singlePosition = _node.spline.TranslateDistBasedToParam(tDist);
                    EOM.startPos = _node.spline.GetSplineValue(EOM.singlePosition);
                    EOM.endPos = _node.spline.GetSplineValue(EOM.singlePosition);
                }
            }

            _node.SetupSplinatedMeshes();
            _node.SetupEdgeObjects();
            return true;
        }


        #region "Splat maps"
        /// <summary> Returns a splat map texture encoded as png </summary>
        public static byte[] MakeSplatMap(Terrain _terrain, Color _BG, Color _FG, int _width, int _height, float _splatWidth, bool _isSkippingBridge, bool _isSkippingTunnel, string _roadUID = "")
        {
            Texture2D texture = new Texture2D(_width, _height, TextureFormat.RGB24, false);

            //Set background color:
            Color[] backgroundColors = new Color[_width * _height];
            int backgroundCount = backgroundColors.Length;
            for (int i = 0; i < backgroundCount; i++)
            {
                backgroundColors[i] = _BG;
            }
            texture.SetPixels(0, 0, _width, _height, backgroundColors);


            Object[] roadObjects;
            if (_roadUID != "")
            {
                roadObjects = new Object[1];
                Object[] roads = EngineIntegration.FindObjectsByType<Road>();
                foreach (Road road in roads)
                {
                    if (string.CompareOrdinal(road.UID, _roadUID) == 0)
                    {
                        roadObjects[0] = road;
                        break;
                    }
                }
            }
            else
            {
                roadObjects = EngineIntegration.FindObjectsByType<Road>();
            }


            Vector3 terrainPos = _terrain.transform.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            foreach (Road road in roadObjects)
            {
                SplineC spline = road.spline;
                int tCount = spline.RoadDefKeysArray.Length;

                Vector3 POS1 = default(Vector3);
                Vector3 POS2 = default(Vector3);

                Vector3 tVect = default(Vector3);
                Vector3 tVect2 = default(Vector3);
                Vector3 lVect1 = default(Vector3);
                Vector3 lVect2 = default(Vector3);
                Vector3 rVect1 = default(Vector3);
                Vector3 rVect2 = default(Vector3);

                int x1, y1;
                int[] tX = new int[4];
                int[] tY = new int[4];
                int MinX = -1;
                int MaxX = -1;
                int MinY = -1;
                int MaxY = -1;
                int xDiff = -1;
                int yDiff = -1;
                float p1 = 0f;
                float p2 = 0f;
                bool isXBad = false;
                bool isYBad = false;
                for (int i = 0; i < (tCount - 1); i++)
                {
                    isXBad = false;
                    isYBad = false;
                    p1 = spline.TranslateInverseParamToFloat(spline.RoadDefKeysArray[i]);
                    p2 = spline.TranslateInverseParamToFloat(spline.RoadDefKeysArray[i + 1]);

                    //Skip bridges:
                    if (_isSkippingBridge)
                    {
                        if (spline.IsInBridgeTerrain(p1))
                        {
                            continue;
                        }
                    }

                    //Skip tunnels:
                    if (_isSkippingTunnel)
                    {
                        if (spline.IsInTunnelTerrain(p1))
                        {
                            continue;
                        }
                    }

                    spline.GetSplineValueBoth(p1, out tVect, out POS1);
                    spline.GetSplineValueBoth(p2, out tVect2, out POS2);
                    lVect1 = (tVect + new Vector3(_splatWidth * -POS1.normalized.z, 0, _splatWidth * POS1.normalized.x));
                    rVect1 = (tVect + new Vector3(_splatWidth * POS1.normalized.z, 0, _splatWidth * -POS1.normalized.x));
                    lVect2 = (tVect2 + new Vector3(_splatWidth * -POS2.normalized.z, 0, _splatWidth * POS2.normalized.x));
                    rVect2 = (tVect2 + new Vector3(_splatWidth * POS2.normalized.z, 0, _splatWidth * -POS2.normalized.x));

                    TranslateWorldVectToCustom(_width, _height, lVect1, ref terrainPos, ref terrainSize, out x1, out y1);
                    tX[0] = x1;
                    tY[0] = y1;
                    TranslateWorldVectToCustom(_width, _height, rVect1, ref terrainPos, ref terrainSize, out x1, out y1);
                    tX[1] = x1;
                    tY[1] = y1;
                    TranslateWorldVectToCustom(_width, _height, lVect2, ref terrainPos, ref terrainSize, out x1, out y1);
                    tX[2] = x1;
                    tY[2] = y1;
                    TranslateWorldVectToCustom(_width, _height, rVect2, ref terrainPos, ref terrainSize, out x1, out y1);
                    tX[3] = x1;
                    tY[3] = y1;

                    MinX = Mathf.Min(tX);
                    MaxX = Mathf.Max(tX);
                    MinY = Mathf.Min(tY);
                    MaxY = Mathf.Max(tY);


                    if (MinX < 0)
                    {
                        MinX = 0;
                        isXBad = true;
                    }
                    if (MaxX < 0)
                    {
                        MaxX = 0;
                        isXBad = true;
                    }
                    if (MinY < 0)
                    {
                        MinY = 0;
                        isYBad = true;
                    }
                    if (MaxY < 0)
                    {
                        MaxY = 0;
                        isYBad = true;
                    }

                    if (MinX > (_width - 1))
                    {
                        MinX = (_width - 1);
                        isXBad = true;
                    }
                    if (MaxX > (_width - 1))
                    {
                        MaxX = (_width - 1);
                        isXBad = true;
                    }
                    if (MinY > (_height - 1))
                    {
                        MinY = (_height - 1);
                        isYBad = true;
                    }
                    if (MaxY > (_height - 1))
                    {
                        MaxY = (_height - 1);
                        isYBad = true;
                    }

                    if (isXBad && isYBad)
                    {
                        continue;
                    }

                    xDiff = MaxX - MinX;
                    yDiff = MaxY - MinY;

                    Color[] colors = new Color[xDiff * yDiff];
                    int colorCount = colors.Length;
                    for (int j = 0; j < colorCount; j++)
                    {
                        colors[j] = _FG;
                    }

                    if (xDiff > 0 && yDiff > 0)
                    {
                        texture.SetPixels(MinX, MinY, xDiff, yDiff, colors);
                    }
                }
            }

            texture.Apply();
            byte[] imageBytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return imageBytes;
        }


        /// <summary> Writes _vect location into _x1 and _y1 relative to the terrain on a 2D map </summary>
        private static void TranslateWorldVectToCustom(int _width, int _height, Vector3 _vect, ref Vector3 _pos, ref Vector3 _size, out int _x1, out int _y1)
        {
            //Get the normalized position of this game object relative to the terrain:
            _vect -= _pos;

            _vect.x = _vect.x / _size.x;
            _vect.z = _vect.z / _size.z;

            //Get the position of the terrain heightmap where this game object is:
            _x1 = (int) (_vect.x * _width);
            _y1 = (int) (_vect.z * _height);
        }
        #endregion
    }
}

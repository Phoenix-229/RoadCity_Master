using UnityEngine;


namespace RoadCityMaster
{
    public static class Construction
    {
        // Cosmetic display index for new node GameObjects. It used to be re-derived on every single node
        // creation by a scene-wide FindObjectsByType<SplineN>() search, so the cost of adding one node
        // scaled with the size of the whole scene -- intolerable once a city fills it. The world count is
        // needed only to label the object, never as identity (idOnSpline is identity), so it is seeded once
        // per session from a single search and then incremented locally: O(scene) once, O(1) thereafter.
        private static int worldNodeNameIndex = -1;

        /// <summary> The next cosmetic index for a new node's name. Seeds from one scene search the first
        /// time it is asked (preserving the original "roughly the Nth node in the scene" naming), then just
        /// counts up, so node creation no longer pays a scene-wide search each time. Shared with
        /// RoadAutomation's programmatic insert, which the city bake drives in bulk. </summary>
        internal static int NextNodeNameIndex()
        {
            if (worldNodeNameIndex < 0)
            {
                worldNodeNameIndex = EngineIntegration.FindObjectsByType<SplineN>().Length;
            }
            return worldNodeNameIndex++;
        }

        /// <summary> Creates a node and performs validation checks </summary>
        public static SplineN CreateNode(Road _road, bool _isSpecialEndNode = false, Vector3 _vectorSpecialLoc = default(Vector3), bool _isInterNode = false)
        {
            GameObject nodeObj = new GameObject("Node" + NextNodeNameIndex().ToString());

            if (!_isInterNode)
            {
                EngineIntegration.RegisterUndo(nodeObj, "Created node");
            }


            SplineN node = nodeObj.AddComponent<SplineN>();

            if (_isSpecialEndNode)
            {
                node.isSpecialEndNode = true;
                nodeObj.transform.position = _vectorSpecialLoc;
            }
            else
            {
                nodeObj.transform.position = _road.editorMousePos;
                //This helps prevent double clicks:
                int nodeCount = _road.spline.GetNodeCount();
                for (int index = 0; index < nodeCount; index++)
                {
                    if (Vector3.Distance(_road.editorMousePos, _road.spline.nodes[index].pos) < 5f)
                    {
                        Object.DestroyImmediate(nodeObj);
                        return null;
                    }
                }
                //End double click prevention
            }


            //Lift the node clear of the ground under it. RM-94: this compared against absolute world Y and clamped to
            //0.03 m, so a node placed in a valley or below sea level was teleported to 3 cm above the scene origin.
            nodeObj.transform.position = RoadNodeGround.Lift(nodeObj.transform.position);

            nodeObj.transform.parent = _road.splineObject.transform;
            node.idOnSpline = _road.spline.GetNodeCount() + 1;
            node.spline = _road.spline;

            //Enforce max road grade:
            if (_road.isMaxGradeEnabled && !_isSpecialEndNode)
            {
                node.EnsureGradeValidity(-1, true);
            }

            if (!_isInterNode && !_isSpecialEndNode)
            {
                // RM-100. Queued, not immediate: adding a node is the interactive act the author complained about, and
                // adding several in quick succession must not start several builds on top of each other.
                _road.RequestUpdate();
            }
            return node;
        }


        /// <summary> Insert
        /// Detect closest node (if end node, auto select other node)
        /// Determine which node is closest (up or down) on spline
        /// Place node, adjust all id on splines
        /// Setup spline </summary>
        public static SplineN InsertNode(Road _road, bool _isForcedLoc = false, Vector3 _forcedLoc = default(Vector3), bool _isPreNode = false, int _insertIndex = -1, bool _isSpecialEndNode = false, bool _isInterNode = false)
        {
            GameObject nodeObj;
            int nodeNameIndex = NextNodeNameIndex();
            if (!_isForcedLoc)
            {
                nodeObj = new GameObject("Node" + nodeNameIndex.ToString());
            }
            else if (_isForcedLoc && !_isSpecialEndNode)
            {
                nodeObj = new GameObject("Node" + nodeNameIndex.ToString() + "Ignore");
            }
            else
            {
                nodeObj = new GameObject("Node" + nodeNameIndex.ToString());
            }


            if (!_isInterNode)
            {
                EngineIntegration.RegisterUndo(nodeObj, "Inserted node");
            }


            if (!_isForcedLoc)
            {
                nodeObj.transform.position = _road.editorMousePos;

                //This helps prevent double clicks:
                int nodeCount = _road.spline.GetNodeCount();
                for (int index = 0; index < nodeCount; index++)
                {
                    if (Vector3.Distance(_road.editorMousePos, _road.spline.nodes[index].pos) < 15f)
                    {
                        Object.DestroyImmediate(nodeObj);
                        return null;
                    }
                }
                //End double click prevention
            }
            else
            {
                nodeObj.transform.position = _forcedLoc;
            }
            //Lift the node clear of the ground under it. RM-94: this compared against absolute world Y and clamped to
            //0.03 m, so a node placed in a valley or below sea level was teleported to 3 cm above the scene origin.
            nodeObj.transform.position = RoadNodeGround.Lift(nodeObj.transform.position);
            nodeObj.transform.parent = _road.splineObject.transform;

            int childCount = _road.spline.nodes.Count;
            //float mDistance = 50000f;
            //float tDistance = 0f;

            float param;
            if (!_isForcedLoc)
            {
                param = _road.spline.GetClosestParam(_road.editorMousePos, false, true);
            }
            else
            {
                param = _road.spline.GetClosestParam(_forcedLoc, false, true);
            }
            bool isEndInsert = false;
            bool isZeroInsert = false;
            int iStart = 0;
            if (RootUtils.IsApproximately(param, 0f, 0.0001f))
            {
                isZeroInsert = true;
                iStart = 0;
            }
            else if (RootUtils.IsApproximately(param, 1f, 0.0001f))
            {
                isEndInsert = true;
            }

            if (_isForcedLoc)
            {
                iStart = _insertIndex;
            }
            else
            {
                for (int index = 0; index < childCount; index++)
                {
                    SplineN xNode = _road.spline.nodes[index];
                    if (!isZeroInsert && !isEndInsert)
                    {
                        if (param > xNode.time)
                        {
                            iStart = xNode.idOnSpline + 1;
                        }
                    }
                }
            }

            if (isEndInsert)
            {
                iStart = _road.spline.nodes.Count;
            }
            else
            {
                for (int i = iStart; i < childCount; i++)
                {
                    _road.spline.nodes[i].idOnSpline += 1;
                }
            }

            // Create new node
            SplineN node = nodeObj.AddComponent<SplineN>();
            if (_isForcedLoc && !_isSpecialEndNode)
            {
                node.isBridge = true;
                node.isIgnore = true;
                //tNode.bIsBridge_PreNode = bIsPreNode;
                //tNode.bIsBridge_PostNode = !bIsPreNode;	
            }
            node.spline = _road.spline;
            node.idOnSpline = iStart;
            node.isSpecialEndNode = _isSpecialEndNode;
            if (!_isForcedLoc)
            {
                node.pos = _road.editorMousePos;
            }
            else
            {
                node.pos = _forcedLoc;
            }

            _road.spline.nodes.Insert(iStart, node);

            //Enforce maximum road grade:
            if (!_isForcedLoc && !_isSpecialEndNode && _road.isMaxGradeEnabled)
            {
                node.EnsureGradeValidity(iStart);
            }

            if (!_isInterNode && !_isSpecialEndNode)
            {
                if (!_isForcedLoc)
                {
                    _road.RequestUpdate();
                }
            }

            return node;
        }
    }
}

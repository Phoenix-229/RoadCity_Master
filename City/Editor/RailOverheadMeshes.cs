// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using RoadCityMaster.City.Utilities;

namespace RoadCityMaster.City.Editor
{
    public struct OHLEMastMeshKey : IEquatable<OHLEMastMeshKey>
    {
        public float baseDiameter;
        public float topDiameter;
        public float bracketTubeDiameter;
        public float foundationDepth;
        public float mastTopAboveMessenger;
        
        public float railHeadAboveGround;
        public float messengerHeight;
        public float stagger;
        public int staggerSign;
        public float halfFormationWidth;

        public bool Equals(OHLEMastMeshKey other)
        {
            return baseDiameter == other.baseDiameter
                && topDiameter == other.topDiameter
                && bracketTubeDiameter == other.bracketTubeDiameter
                && foundationDepth == other.foundationDepth
                && mastTopAboveMessenger == other.mastTopAboveMessenger
                && railHeadAboveGround == other.railHeadAboveGround
                && messengerHeight == other.messengerHeight
                && stagger == other.stagger
                && staggerSign == other.staggerSign
                && halfFormationWidth == other.halfFormationWidth;
        }

        public override bool Equals(object obj)
        {
            return obj is OHLEMastMeshKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + baseDiameter.GetHashCode();
                hash = hash * 31 + topDiameter.GetHashCode();
                hash = hash * 31 + bracketTubeDiameter.GetHashCode();
                hash = hash * 31 + foundationDepth.GetHashCode();
                hash = hash * 31 + mastTopAboveMessenger.GetHashCode();
                hash = hash * 31 + railHeadAboveGround.GetHashCode();
                hash = hash * 31 + messengerHeight.GetHashCode();
                hash = hash * 31 + stagger.GetHashCode();
                hash = hash * 31 + staggerSign.GetHashCode();
                hash = hash * 31 + halfFormationWidth.GetHashCode();
                return hash;
            }
        }
    }

    public static class OHLEMastMesh
    {
        private static readonly Dictionary<OHLEMastMeshKey, Mesh> Cache = new Dictionary<OHLEMastMeshKey, Mesh>();

        public static Mesh GetOrCreate(OHLEMastMeshKey key)
        {
            if (Cache.TryGetValue(key, out Mesh existing) && existing != null) { return existing; }
            Mesh built = Build(key);
            Cache[key] = built;
            return built;
        }

        public static void ClearCache()
        {
            foreach (KeyValuePair<OHLEMastMeshKey, Mesh> entry in Cache)
            {
                if (entry.Value == null) { continue; }
                if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(entry.Value); continue; }
                UnityEngine.Object.Destroy(entry.Value);
            }
            Cache.Clear();
        }

        private static Mesh Build(OHLEMastMeshKey key)
        {
            MeshScratch m = new MeshScratch();
            
            float shaftHeight = key.railHeadAboveGround + key.messengerHeight + key.mastTopAboveMessenger + key.foundationDepth;
            m.AddTaperedTube(new Vector3(0f, -key.foundationDepth, 0f), shaftHeight, key.baseDiameter, key.topDiameter, 8, 0);
            m.AddBox(new Vector3(0f, -key.foundationDepth * 0.5f, 0f), new Vector3(0.3f, key.foundationDepth * 0.5f, 0.3f), 0);
            
            float bracketY = key.railHeadAboveGround + key.messengerHeight;
            float trackCentreX = key.halfFormationWidth + RailOverheadPlanner.MastClearOfFormationMetres;
            float contactWireX = trackCentreX - key.stagger * key.staggerSign;
            
            Vector3 armStart = new Vector3(key.baseDiameter * 0.5f, bracketY, 0f);
            Vector3 armEnd = new Vector3(contactWireX, bracketY, 0f);
            m.AddSegment(armStart, armEnd, key.bracketTubeDiameter, 6, 1);
            
            Vector3 stayStart = new Vector3(key.baseDiameter * 0.5f, bracketY - 1f, 0f);
            if (stayStart.y > 0f) 
            {
                m.AddSegment(stayStart, armEnd, key.bracketTubeDiameter * 0.7f, 6, 1);
            }

            m.AddBox(armEnd, new Vector3(0.03f, 0.05f, 0.03f), 1);
            return m.ToMesh("OHLEMast");
        }
    }

    public static class OHLEWireMesh
    {
        public static void BuildSpan(
            RailOverheadSpan span,
            RailOverheadMast fromMast,
            RailOverheadMast toMast,
            Vector3 p1CatW, Vector3 p2CatW,
            Vector3 p1ConW, Vector3 p2ConW,
            Vector3 origin,
            RailOverheadClass overheadClass,
            out Mesh spanMesh)
        {
            MeshScratch m = new MeshScratch();
            
            float step = overheadClass.dropperSpacingMetres > 0f ? overheadClass.dropperSpacingMetres : 5f;
            int segments = Mathf.CeilToInt(span.spanMetres / step);
            if (segments < 1) segments = 1;
            
            float catRadius = overheadClass.messengerWireDiameterMetres > 0f ? overheadClass.messengerWireDiameterMetres * 0.5f : 0.00525f;
            float conRadius = overheadClass.contactWireDiameterMetres > 0f ? overheadClass.contactWireDiameterMetres * 0.5f : 0.00612f;
            float drpRadius = overheadClass.dropperWireDiameterMetres > 0f ? overheadClass.dropperWireDiameterMetres * 0.5f : 0.0025f;
            
            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;
                float x1 = t1 * span.spanMetres;
                float x2 = t2 * span.spanMetres;
                
                Vector3 cat1 = Vector3.Lerp(p1CatW, p2CatW, t1);
                cat1.y = Mathf.Lerp(p1CatW.y, p2CatW.y, t1) - Mathf.Lerp(fromMast.messengerHeightMetres, toMast.messengerHeightMetres, t1) + (float)span.messenger.CurveY(x1);
                
                Vector3 cat2 = Vector3.Lerp(p1CatW, p2CatW, t2);
                cat2.y = Mathf.Lerp(p1CatW.y, p2CatW.y, t2) - Mathf.Lerp(fromMast.messengerHeightMetres, toMast.messengerHeightMetres, t2) + (float)span.messenger.CurveY(x2);
                
                Vector3 con1 = Vector3.Lerp(p1ConW, p2ConW, t1);
                Vector3 con2 = Vector3.Lerp(p1ConW, p2ConW, t2);
                
                m.AddSegment(cat1 - origin, cat2 - origin, catRadius * 2f, 4, 0);
                m.AddSegment(con1 - origin, con2 - origin, conRadius * 2f, 4, 1);
                
                if (i > 0)
                {
                    float dropperLength = cat1.y - con1.y;
                    if (dropperLength >= overheadClass.minDropperMetres)
                    {
                        m.AddSegment(cat1 - origin, con1 - origin, drpRadius * 2f, 4, 0);
                    }
                }
            }
            
            spanMesh = m.ToMesh("OHLEWire");
        }
    }
}
#endif

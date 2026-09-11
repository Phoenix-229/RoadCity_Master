// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> D-14. The lattice transmission tower as geometry, built once per parameter set and handed out by
    /// reference -- the tower analog of <see cref="PoleMeshFactory"/>, and here for the same reasons that one is.
    ///
    /// The tool had the maths of a transmission line -- the catenary, the clearance, the tower attachment heights --
    /// and no tower. A lattice tower is neither an author's prefab nor a sweep of the vendored mesh makers along a
    /// spline; it is a small steel truss whose proportions come from a structural table, and a cross-country line has
    /// dozens of them, so the two things that matter are the same two the pole factory states: it is built FROM the
    /// table, and it is built ONCE.
    ///
    /// CACHED BY PARAMETER SET. The cache is keyed by <see cref="TowerMeshKey"/> -- the shape's own
    /// <see cref="TransmissionTowerClass.ShapeHash"/> plus the three things that change the STEEL a shape resolves to:
    /// the phase count (how many insulator strings, and where they hang across the arm), the tower type and whether it
    /// is a terminal (a suspension I-string hangs the phase down; an angle or terminal strain string lies along the
    /// pull and does not). Two suspension towers of one shape are one mesh and one instanced draw; a suspension and an
    /// angle tower of that shape are two, because their insulators are genuinely different geometry.
    ///
    /// TWO SUBMESHES, steel then insulator. Submesh 0 is the galvanised lattice -- legs, bracing, cross-arm truss and
    /// the earth-wire peak -- which survives to the far LOD as a silhouette; submesh 1 is the insulator strings, the
    /// tower's one non-steel part, dropped at distance. One mesh with two submeshes rather than two meshes, because the
    /// tower is one instance and splitting it would double the draw calls the cache exists to avoid. It is the same
    /// division the distribution pole makes between its timber and its hardware.
    ///
    /// WHERE THIS LIVES. The tower's ARITHMETIC -- its dimensions (<see cref="TransmissionTowerClass"/>) and the heights
    /// its wires attach at (<see cref="TransmissionTowerAttachments"/>) -- is in the runtime assembly, because the
    /// planner and the gate read it headlessly. The VERTICES are here, in the editor assembly, because a transmission
    /// run is BAKED: the meshes are built once when the author presses build and saved with the scene, so a shipped
    /// player carries no mesh builder. That is the division <see cref="TransmissionLineRun"/> states for itself, and it
    /// is what keeps <c>City/Runtime</c> free of any mesh builder.
    ///
    /// The mesh is built with its BASE PLANE at the local origin and +Y up, the cross-arm along local X and the line
    /// along local Z, so a caller seats a tower by placing the origin at the lowest of its four feet and rotating it to
    /// the line direction: the buried setting depth is modelled below the origin, which is what makes a tower on a
    /// slope look keyed in rather than resting on the surface. </summary>
    public static class TowerMeshFactory
    {
        private static readonly Dictionary<TowerMeshKey, Mesh> Cache = new Dictionary<TowerMeshKey, Mesh>();

        /// <summary> How many meshes the cache is holding. For D-15's per-chunk ceiling reporting and for the gate:
        /// asking twice for the same tower must not make two. </summary>
        public static int CachedMeshCount { get { return Cache.Count; } }

        /// <summary> The mesh for one parameter set, built on first ask and returned by reference afterwards. The
        /// returned mesh is SHARED: a caller assigns it to a <c>MeshFilter.sharedMesh</c> and never modifies it, the
        /// same bargain Unity's own primitive meshes make. </summary>
        public static Mesh Get(TowerMeshKey key, TransmissionTowerClass shape)
        {
            Mesh existing;
            if (Cache.TryGetValue(key, out existing) && existing != null) { return existing; }
            Mesh built = Build(key, shape != null ? shape : TransmissionTowerClass.Default());
            Cache[key] = built;
            return built;
        }

        /// <summary> Drop every cached mesh and destroy it. Destroys rather than merely forgetting, because a Mesh is an
        /// unmanaged allocation and a forgotten one is a leak the leak check would find. </summary>
        public static void ClearCache()
        {
            foreach (KeyValuePair<TowerMeshKey, Mesh> entry in Cache)
            {
                if (entry.Value == null) { continue; }
                if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(entry.Value); continue; }
                UnityEngine.Object.Destroy(entry.Value);
            }
            Cache.Clear();
        }

        /// <summary> Build one tower. Deterministic (C-06): a fixed sequence of members in a fixed order, so the vertex
        /// buffer is identical for identical parameters -- which is also what makes the cache meaningful. </summary>
        private static Mesh Build(TowerMeshKey key, TransmissionTowerClass s)
        {
            MeshScratch m = new MeshScratch();
            TransmissionTowerAttachments a = TransmissionTowerAttachments.For(s, key.phaseCount, key.type,
                key.isTerminal);

            float halfBase = s.baseWidthMetres * 0.5f;
            float topHalf = halfBase * 0.30f;               // the lattice waist, ~a third of the base spread
            float armY = a.crossArmY;
            float peakY = a.shieldWireY;
            float armHalf = s.crossArmHalfLengthMetres;
            float depth = (float)s.SettingDepthMetres();    // buried length below the base plane

            BuildBodyAndBracing(m, halfBase, topHalf, armY, depth, s.legFaceMetres, s.braceFaceMetres);
            BuildCrossArm(m, armHalf, armY, topHalf, s.legFaceMetres, s.braceFaceMetres);
            BuildShieldPeak(m, a, armY, peakY, topHalf, s.braceFaceMetres, s.shieldWireCount);
            BuildInsulators(m, a, key.type, key.isTerminal);

            return m.ToMesh("RCM Transmission Tower");
        }

        // ------------------------------------------------------------------ the steel body

        /// <summary> The four tapered legs, the horizontal belts that tie them at each panel, and the X-bracing on all
        /// four faces. The legs run from the four feet at full base spread, through the underground setting depth, up
        /// to the waist square at the cross-arm; the panels are sized so a ~28 m tower divides into a believable number
        /// of bays rather than a fixed count that would look coarse on a tall tower and busy on a short one. </summary>
        private static void BuildBodyAndBracing(MeshScratch m, float halfBase, float topHalf, float armY, float depth,
            float legFace, float braceFace)
        {
            Vector2[] corners = { new Vector2(-1f, -1f), new Vector2(1f, -1f),
                new Vector2(1f, 1f), new Vector2(-1f, 1f) };

            for (int c = 0; c < 4; c++)
            {
                Vector3 foot = LegPoint(corners[c], -depth, halfBase, topHalf, armY);
                Vector3 top = LegPoint(corners[c], armY, halfBase, topHalf, armY);
                m.AddSegmentBox(foot, top, legFace, 0);
            }

            int panels = Mathf.Clamp(Mathf.RoundToInt(armY / 4.5f), 3, 8);
            for (int p = 0; p < panels; p++)
            {
                float yLo = armY * p / panels;
                float yHi = armY * (p + 1) / panels;
                for (int c = 0; c < 4; c++)
                {
                    Vector2 ca = corners[c];
                    Vector2 cb = corners[(c + 1) % 4];
                    Vector3 aLo = LegPoint(ca, yLo, halfBase, topHalf, armY);
                    Vector3 aHi = LegPoint(ca, yHi, halfBase, topHalf, armY);
                    Vector3 bLo = LegPoint(cb, yLo, halfBase, topHalf, armY);
                    Vector3 bHi = LegPoint(cb, yHi, halfBase, topHalf, armY);
                    m.AddSegmentBox(aLo, bHi, braceFace, 0);   // the two crossing diagonals of the panel
                    m.AddSegmentBox(bLo, aHi, braceFace, 0);
                    m.AddSegmentBox(aHi, bHi, braceFace, 0);   // the belt closing the top of the panel
                }
            }
        }

        /// <summary> One leg's XZ at a given height, tapered linearly from the base spread at ground to the waist at the
        /// cross-arm; below ground the leg stays at full spread, so the buried footing is vertical. </summary>
        private static Vector3 LegPoint(Vector2 corner, float y, float halfBase, float topHalf, float armY)
        {
            float f = armY <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, y) / armY);
            float half = Mathf.Lerp(halfBase, topHalf, f);
            return new Vector3(corner.x * half, y, corner.y * half);
        }

        // ------------------------------------------------------------------ the cross-arm

        /// <summary> The cross-arm as a shallow horizontal truss along local X: a top chord at the arm level, a bottom
        /// chord a little below, and a web of verticals and diagonals between them. A truss rather than a plain box beam
        /// because the trussed arm is the silhouette that reads as a transmission tower and not a goalpost. </summary>
        private static void BuildCrossArm(MeshScratch m, float armHalf, float armY, float topHalf, float legFace,
            float braceFace)
        {
            if (armHalf <= 0f) { return; }
            float armDepth = Mathf.Clamp(armHalf * 0.14f, 0.5f, 1.2f);
            float chord = legFace * 0.8f;
            float lowY = armY - armDepth;

            m.AddSegmentBox(new Vector3(-armHalf, armY, 0f), new Vector3(armHalf, armY, 0f), chord, 0);
            m.AddSegmentBox(new Vector3(-armHalf, lowY, 0f), new Vector3(armHalf, lowY, 0f), chord, 0);

            int bays = Mathf.Max(2, Mathf.RoundToInt(armHalf / 1.2f));
            for (int i = -bays; i <= bays; i++)
            {
                float x = armHalf * i / bays;
                m.AddSegmentBox(new Vector3(x, armY, 0f), new Vector3(x, lowY, 0f), braceFace, 0);   // vertical
                if (i < bays)
                {
                    float xNext = armHalf * (i + 1) / bays;
                    m.AddSegmentBox(new Vector3(x, lowY, 0f), new Vector3(xNext, armY, 0f), braceFace, 0); // diagonal
                }
            }
        }

        // ------------------------------------------------------------------ the earth-wire peak

        /// <summary> The peak(s) the earth wire rides, above the phases. One central A-frame for a single shield wire;
        /// two, spread out over the outer phases, for a pair -- which is where twin earth wires sit to keep the outer
        /// phases inside the protective angle. The apex offsets match the ones the run strings the shield wire at, so
        /// the wire meets the steel it is drawn to. </summary>
        private static void BuildShieldPeak(MeshScratch m, TransmissionTowerAttachments a, float armY, float peakY,
            float topHalf, float braceFace, int shieldWireCount)
        {
            float[] apexOffsets = ShieldApexOffsets(a, shieldWireCount);
            for (int i = 0; i < apexOffsets.Length; i++)
            {
                float ax = apexOffsets[i];
                Vector3 apex = new Vector3(ax, peakY, 0f);
                m.AddSegmentBox(new Vector3(ax - topHalf * 0.6f, armY, 0f), apex, braceFace, 0);
                m.AddSegmentBox(new Vector3(ax + topHalf * 0.6f, armY, 0f), apex, braceFace, 0);
                m.AddSegmentBox(new Vector3(ax, armY, 0f), apex, braceFace, 0);
            }
        }

        /// <summary> Where the shield wires sit across the top, by count: one on the centreline, two over the outer
        /// phases. Public so the run's builder strings each shield wire at exactly the offset the peak was drawn at,
        /// rather than the two guessing the same number apart. One and two are the real cases the shape documents; a
        /// count above two is drawn as the pair rather than inventing a spacing the class never specified. </summary>
        public static float[] ShieldApexOffsets(TransmissionTowerAttachments a, int shieldWireCount)
        {
            int count = shieldWireCount < 1 ? 1 : shieldWireCount;
            float outer = 0f;
            if (a.phaseOffsets != null)
            {
                for (int i = 0; i < a.phaseOffsets.Length; i++)
                {
                    float abs = Mathf.Abs(a.phaseOffsets[i]);
                    if (abs > outer) { outer = abs; }
                }
            }
            if (count <= 1) { return new float[] { 0f }; }
            return new float[] { -outer, outer };
        }

        // ------------------------------------------------------------------ the insulator strings

        /// <summary> An insulator string at every phase, in submesh 1. A SUSPENSION tower hangs a vertical I-string
        /// from the arm and the phase sits at its foot; an ANGLE or terminal tower takes the pull on a strain string
        /// that lies along the line (local Z) at arm level, so the phase attaches at the arm. Each string is a slender
        /// rod, and the suspension string carries a few cap-and-pin discs so it reads as ceramic rather than as a
        /// dropper. </summary>
        private static void BuildInsulators(MeshScratch m, TransmissionTowerAttachments a, TowerType type,
            bool isTerminal)
        {
            if (a.phaseOffsets == null) { return; }
            bool strain = isTerminal || type == TowerType.Angle;
            float length = a.insulatorLengthMetres;
            if (length <= 0f) { return; }
            float discDia = 0.16f;
            float rodDia = discDia * 0.35f;

            for (int i = 0; i < a.phaseOffsets.Length; i++)
            {
                float px = a.phaseOffsets[i];
                if (strain)
                {
                    // A strain string lies along the pull, roughly horizontal; drawn along local Z at arm level. It does
                    // not lower the phase, which attaches at the arm -- exactly what the attachment arithmetic reports.
                    m.AddSegment(new Vector3(px, a.crossArmY, -length * 0.5f),
                        new Vector3(px, a.crossArmY, length * 0.5f), rodDia * 1.4f, 6, 1);
                    continue;
                }
                // A suspension I-string: a rod from the arm down to the conductor, with a stack of discs along it.
                float topY = a.crossArmY;
                float botY = a.conductorAttachY;
                m.AddSegment(new Vector3(px, topY, 0f), new Vector3(px, botY, 0f), rodDia, 6, 1);
                int discs = Mathf.Clamp(Mathf.RoundToInt(length / 0.15f), 4, 12);
                for (int d = 0; d < discs; d++)
                {
                    float t = (d + 0.5f) / discs;
                    float y = Mathf.Lerp(topY, botY, t);
                    m.AddTaperedTube(new Vector3(px, y - 0.025f, 0f), 0.05f, discDia, discDia * 0.7f, 6, 1);
                }
            }
        }
    }


    /// <summary> The parameter set one cached tower mesh is built from: the shape's own hash, plus the three things
    /// that change the steel a shape resolves to. A struct with real equality (not an int hash) so two distinct towers
    /// can never collide into one cache entry, the same care <c>PoleMeshKey</c> takes. </summary>
    public struct TowerMeshKey : IEquatable<TowerMeshKey>
    {
        public int shapeHash;
        public int phaseCount;
        public TowerType type;
        public bool isTerminal;

        /// <summary> The key for a shape, phase count and tower role. Deterministic (C-06): the shape hash is the
        /// shape's own integer hash, and the rest are the integers that decide the insulator geometry. </summary>
        public static TowerMeshKey For(TransmissionTowerClass shape, int phaseCount, TowerType type, bool isTerminal)
        {
            TowerMeshKey key = new TowerMeshKey();
            key.shapeHash = (shape != null ? shape : TransmissionTowerClass.Default()).ShapeHash();
            key.phaseCount = phaseCount < 0 ? 0 : phaseCount;
            key.type = type;
            key.isTerminal = isTerminal;
            return key;
        }

        public bool Equals(TowerMeshKey other)
        {
            return shapeHash == other.shapeHash && phaseCount == other.phaseCount
                && type == other.type && isTerminal == other.isTerminal;
        }

        public override bool Equals(object obj)
        {
            return obj is TowerMeshKey && Equals((TowerMeshKey)obj);
        }

        // determinism-exempt
        public override int GetHashCode()
        {
            int h = 17;
            h = h * 31 + shapeHash;
            h = h * 31 + phaseCount;
            h = h * 31 + (int)type;
            h = h * 31 + (isTerminal ? 1 : 0);
            return h;
        }
    }
}
#endif

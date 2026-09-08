#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-2. The pole itself, as geometry, built once per parameter set and handed out by reference.
    ///
    /// The tool had the maths for a pole line and no pole. Everything visible in <c>City/</c> until now was either a
    /// prefab the author supplied or a sweep of the vendored mesh makers along a spline; a distribution pole is
    /// neither -- it is a small assembly whose proportions come from a product table, and there are thousands of
    /// them, so the two things that matter are that it is built from the table and that it is built once.
    ///
    /// CACHED BY PARAMETER SET, which is D-15's affordability claim and not an optimisation added afterwards: N poles
    /// of the same specification are one mesh and one instanced draw call, so a rural kilometre's 25 poles cost the
    /// same as one. The cache is keyed by <see cref="PoleMeshKey"/> -- see its own summary for why the fields are
    /// exactly the ones the geometry reads.
    ///
    /// TWO SUBMESHES, timber then hardware. Submesh 0 is the shaft and the cross-arm, which want a bark-scaled wood
    /// material and survive to the far LOD as a silhouette; submesh 1 is everything bolted on -- insulators, the
    /// transformer, the cut-out, the arrester, the racks, the ground wire and the guys -- which wants a metal and
    /// ceramic material and is dropped entirely at distance. One mesh with two submeshes rather than two meshes,
    /// because the pole is one instance and splitting it would double the draw calls it exists to avoid.
    ///
    /// WHERE THIS LIVES, and why it is not with the numbers. A pole's arithmetic -- its dimensions
    /// (<see cref="PoleGeometryClass"/>), its parameter set (<see cref="PoleMeshKey"/>) and the heights its fittings
    /// sit at (<see cref="PoleAttachments"/>) -- is in the RUNTIME assembly, because the planner and the clearance
    /// checks are pure arithmetic that a gate asserts headlessly. The VERTICES are here, in the editor assembly,
    /// because a pole line is BAKED: the meshes are built once when the author presses build and saved with the scene,
    /// so a shipped player has no reason to carry a mesh builder at all. That is the same division
    /// <see cref="RoadPowerLine"/> states for itself -- it computes and refuses, and never draws.
    ///
    /// It is also what makes the tool's own claim about itself true rather than nearly true.
    /// <c>CityGeometryProvenanceGate.CaseNoSecondMeshBuilderExists</c> asserts that no file under <c>City/Runtime</c>
    /// builds a mesh, because the four vendored chain components and the two city operators are meant to be the only
    /// mesh builders in the tool. With the buffer sitting in the runtime folder that assertion was one grep away from
    /// failing; with it here, <c>City/Runtime</c> genuinely contains no mesh builder and the register's account of
    /// where geometry comes from is the account of what the code does.
    ///
    /// The mesh is built with its BUTT at the origin and +Y up, so a caller seats a pole by placing the origin at
    /// the ground point less the setting depth: the buried length is modelled, which is what makes a pole on a slope
    /// look driven in rather than resting on the surface, and it is why <see cref="PoleMeshKey.exposedLengthMetres"/>
    /// is in the key -- a pole shortened to hold a clearance is a different mesh, never a scaled one, because scaling
    /// a pole makes it fat. </summary>
    public static class PoleMeshFactory
    {
        private static readonly Dictionary<PoleMeshKey, Mesh> Cache = new Dictionary<PoleMeshKey, Mesh>();

        /// <summary> How many meshes the cache is holding. For D-15's per-chunk ceiling reporting and for the gate:
        /// asking twice for the same pole must not make two. </summary>
        public static int CachedMeshCount { get { return Cache.Count; } }

        /// <summary> The mesh for one parameter set, built on first ask and returned by reference afterwards.
        ///
        /// The returned mesh is SHARED. A caller assigns it to a <c>MeshFilter.sharedMesh</c> and never modifies it;
        /// modifying it would change every pole in the scene, which is the same bargain Unity's own primitive meshes
        /// make. </summary>
        public static Mesh Get(PoleMeshKey key, PoleGeometryClass shape)
        {
            Mesh existing;
            if (Cache.TryGetValue(key, out existing) && existing != null) { return existing; }
            Mesh built = Build(key, shape != null ? shape : PoleGeometryClass.Default());
            Cache[key] = built;
            return built;
        }

        /// <summary> Drop every cached mesh and destroy it. Called when the geometry class is edited or a scene is
        /// unloaded. Destroys rather than merely forgetting, because a Mesh is an unmanaged allocation and a
        /// forgotten one is a leak the leak check would find. </summary>
        public static void ClearCache()
        {
            foreach (KeyValuePair<PoleMeshKey, Mesh> entry in Cache)
            {
                if (entry.Value == null) { continue; }
                if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(entry.Value); continue; }
                UnityEngine.Object.Destroy(entry.Value);
            }
            Cache.Clear();
        }

        /// <summary> Build one pole. Deterministic (C-06): a fixed sequence of primitives in a fixed order, so the
        /// vertex buffer is identical for identical parameters -- which is also what makes the cache meaningful.
        /// </summary>
        private static Mesh Build(PoleMeshKey key, PoleGeometryClass s)
        {
            MeshScratch m = new MeshScratch();
            PoleAttachments at = PoleAttachments.For(key, s);
            float total = key.settingDepthMetres + key.exposedLengthMetres;

            // The shaft: one tapered tube from butt to tip, in submesh 0. The taper is the ANSI table's, and the
            // buried length is part of it -- see the class summary for why.
            m.AddTaperedTube(Vector3.zero, total, s.buttDiameterMetres, s.topDiameterMetres, s.shaftSides, 0);

            bool hasArm = (key.fittings & PoleFittings.CrossArm) != 0;
            if (hasArm)
            {
                m.AddBox(new Vector3(0f, at.armY, 0f),
                    new Vector3(key.crossArmLengthMetres * 0.5f, s.crossArmHeightMetres * 0.5f,
                        s.crossArmDepthMetres * 0.5f), 0);
                AddBraces(m, s, key, at.armY);
            }

            // Insulators. On an arm they stand on it; with no arm the phases sit on a pole-top bracket, which is what
            // a single-phase tap really carries, and the tool draws it as the short arm it is rather than pretending
            // the pole has none.
            float insulatorBase = hasArm ? at.armY + s.crossArmHeightMetres * 0.5f : at.topY;
            for (int i = 0; i < at.phaseOffsets.Length; i++)
            {
                m.AddTaperedTube(new Vector3(at.phaseOffsets[i], insulatorBase, 0f), s.insulatorHeightMetres,
                    s.insulatorDiameterMetres, s.insulatorDiameterMetres * 0.55f, s.insulatorSides, 1);
            }
            if (!hasArm && at.phaseOffsets.Length > 0)
            {
                m.AddBox(new Vector3(0f, at.topY - s.crossArmHeightMetres * 0.5f, 0f),
                    new Vector3(s.rackWidthMetres, s.crossArmHeightMetres * 0.4f,
                        s.crossArmDepthMetres * 0.4f), 1);
            }

            if ((key.fittings & PoleFittings.NeutralRack) != 0)
            {
                m.AddBox(new Vector3(0f, at.neutralY, 0f),
                    new Vector3(s.rackWidthMetres * 0.5f, 0.04f, 0.04f), 1);
            }
            if ((key.fittings & PoleFittings.SecondaryRack) != 0)
            {
                m.AddBox(new Vector3(0f, at.secondaryY, 0f),
                    new Vector3(s.rackWidthMetres * 0.5f, 0.10f, 0.05f), 1);
            }

            // A communication strand is lagged to the pole face rather than racked: one clamp, well below the supply,
            // in the joint-use space. Small, but it is the fitting that says this pole is shared with another utility.
            if ((key.fittings & PoleFittings.Communication) != 0)
            {
                m.AddBox(new Vector3(0f, at.communicationY, 0f), new Vector3(0.06f, 0.05f, 0.05f), 1);
            }

            if ((key.fittings & PoleFittings.Transformer) != 0)
            {
                float baseY = at.transformerTopY - s.transformerHeightMetres;
                float standoff = (s.topDiameterMetres + s.transformerDiameterMetres) * 0.5f;
                m.AddTaperedTube(new Vector3(0f, baseY, standoff * 0.6f), s.transformerHeightMetres,
                    s.transformerDiameterMetres, s.transformerDiameterMetres, 10, 1);
            }
            if ((key.fittings & PoleFittings.Cutout) != 0)
            {
                m.AddTaperedTube(new Vector3(-s.rackWidthMetres * 0.5f, at.armY - 0.45f, 0.10f),
                    s.cutoutHeightMetres, s.cutoutDiameterMetres, s.cutoutDiameterMetres * 0.8f, 6, 1);
            }
            if ((key.fittings & PoleFittings.Arrester) != 0)
            {
                m.AddTaperedTube(new Vector3(s.rackWidthMetres * 0.5f, at.armY - 0.45f, 0.10f),
                    s.arresterHeightMetres, s.arresterDiameterMetres, s.arresterDiameterMetres, 6, 1);
            }

            // The ground wire runs from the arm down the shaft and into the earth -- the last part matters, because a
            // ground wire that stops at the ground line is the detail that reads as a model rather than a pole.
            if ((key.fittings & PoleFittings.GroundWire) != 0)
            {
                float x = s.buttDiameterMetres * 0.5f + s.groundWireDiameterMetres;
                m.AddSegment(new Vector3(x, at.armY, 0f), new Vector3(x, 0f, 0f),
                    s.groundWireDiameterMetres, s.wireSides, 1);
            }

            // Guys leave the pole near the arm and reach the ground at 45 degrees, the angle a real down-guy is set
            // at: the lead equals the height, so the anchor is as far out as the attachment is up. Placed on opposite
            // sides for two, then the quarters, so the count reads as a direction and not as decoration.
            for (int g = 0; g < key.guyCount; g++)
            {
                float angle = key.guyCount == 1 ? 0f : 360f * g / key.guyCount;
                float rad = angle * Mathf.Deg2Rad;
                float top = at.armY - 0.15f;
                float lead = top - at.groundY;
                Vector3 from = new Vector3(Mathf.Sin(rad) * s.topDiameterMetres * 0.5f, top,
                    Mathf.Cos(rad) * s.topDiameterMetres * 0.5f);
                Vector3 to = new Vector3(Mathf.Sin(rad) * lead, at.groundY, Mathf.Cos(rad) * lead);
                m.AddSegment(from, to, s.guyDiameterMetres, s.wireSides, 1);
            }

            Mesh mesh = m.ToMesh("RCM Distribution Pole");
            return mesh;
        }

        /// <summary> The two knee braces under a cross-arm. A real brace runs from the arm down to the pole at 45
        /// degrees, so a 28 in brace reaches about 20 in along each leg; drawn as a thin box rotated into that line
        /// rather than as a triangle, because at street distance the brace is a stroke and a box costs eight
        /// vertices. </summary>
        private static void AddBraces(MeshScratch m, PoleGeometryClass s, PoleMeshKey key, float armY)
        {
            float reach = s.braceLengthMetres * 0.7071f;
            float halfArm = key.crossArmLengthMetres * 0.5f;
            float outer = Mathf.Min(reach, halfArm * 0.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 armEnd = new Vector3(side * outer, armY - s.crossArmHeightMetres * 0.5f, 0f);
                Vector3 poleFoot = new Vector3(0f, armY - s.crossArmHeightMetres * 0.5f - reach, 0f);
                m.AddSegmentBox(armEnd, poleFoot, s.braceThicknessMetres, 0);
            }
        }
    }

    /// <summary> B-27 / D-1. A hanging wire as geometry: the curve <see cref="ConductorCurve"/> sampled, turned into a
    /// tube.
    ///
    /// Nothing here decides where a conductor goes or how far it sags -- <see cref="TransmissionLine.Solve"/> owns the
    /// curve, the run that strings the wire owns the attachments, and <see cref="ConductorCurve.Sample"/> owns the
    /// points. This class only turns those points into triangles, which is why it can be the one wire in the tool: a
    /// pole line, a railway's contact wire and a transmission span differ in their numbers and not in what a wire is.
    ///
    /// It is here beside the pole factory rather than beside the curve for the reason given above -- the vertices of a
    /// baked run belong to the editor -- and because both halves write into the same
    /// <see cref="MeshScratch"/> buffer through its <see cref="MeshScratch.AddPolylineTube"/>. One buffer, so a
    /// conductor and a pole cannot disagree about winding, submeshes or normals. </summary>
    public static class ConductorMesh
    {
        /// <summary> Every conductor handed in, as one mesh, built RELATIVE to <paramref name="origin"/>.
        ///
        /// The points <see cref="ConductorCurve.Sample"/> reports are world points, and a world-space mesh dropped on a
        /// transform at the scene origin is how the vendored makers build road geometry. A wire is the one case where
        /// that is not good enough: a float carries about a millimetre of resolution ten kilometres from the origin,
        /// and a conductor is a tube five millimetres thick, so a wire that far out would wobble by most of its own
        /// radius. Subtracting the piece's own origin here, and placing the object AT that origin, costs a subtraction
        /// per point, puts every vertex back within a few hundred metres of zero, and gives the piece a transform that
        /// says where it is -- so an author who clicks a wire in the hierarchy is not sent to the scene origin.
        ///
        /// Submesh 0 carries all of it. The buffer always writes two submeshes so that a pole's renderer has a fixed
        /// material array; a conductor mesh fills only the first, and the second stays empty and draws nothing, which
        /// costs a slot and no geometry. </summary>
        public static Mesh Build(IList<ConductorSpan> spans, Vector3 origin, float diameter, int sides, string name)
        {
            MeshScratch buffer = new MeshScratch();
            if (spans != null)
            {
                for (int i = 0; i < spans.Count; i++)
                {
                    List<Vector3> points = ConductorCurve.Sample(spans[i], ConductorCurve.SegmentsPerSpan);
                    for (int p = 0; p < points.Count; p++) { points[p] = points[p] - origin; }
                    buffer.AddPolylineTube(points, diameter, sides, 0);
                }
            }
            return buffer.ToMesh(string.IsNullOrEmpty(name) ? "RCM Conductors" : name);
        }
    }


    /// <summary> The vertex buffers one pole is assembled into, with the primitives a pole is made of.
    ///
    /// Internal and deliberately small. It is not a general mesh library -- the tool already has the vendored
    /// splinated mesh makers for anything swept along an alignment, and a second general one would be exactly the
    /// duplication the house rules forbid. What is here is the four shapes a pole LINE needs and nothing else: a
    /// tapered tube, a box, a tube or box between two points, and a tube along a polyline -- the conductor, which is
    /// the one thing on a pole line that is not part of a pole and is still the same round tube.
    ///
    /// The one design decision worth stating is that WINDING IS DERIVED, not written. Every face is emitted through
    /// <see cref="AddTriangleOriented"/>, which is told the direction the face should look and flips the triangle
    /// when the vertex order it was handed would face the other way. Getting a winding order right by reasoning about
    /// handedness is the kind of thing that is wrong once in twenty faces and shows up as a single black insulator
    /// nobody can find; deriving it costs two cross products per face at build time, and build time happens once per
    /// parameter set because the result is cached. </summary>
    internal sealed class MeshScratch
    {
        public const int SubmeshCount = 2;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles = new List<int>[SubmeshCount];

        public MeshScratch()
        {
            for (int i = 0; i < SubmeshCount; i++) { triangles[i] = new List<int>(); }
        }

        /// <summary> A tapered tube standing on <paramref name="baseCentre"/> and rising <paramref name="height"/>:
        /// the pole shaft, an insulator, the transformer can. The top is capped and the base is not -- a pole's butt
        /// is underground and a can's bottom is against the pole, so the two fans would never be seen. </summary>
        public void AddTaperedTube(Vector3 baseCentre, float height, float baseDiameter, float topDiameter,
            int sides, int submesh)
        {
            if (height <= 0f) { return; }
            if (sides < 3) { sides = 3; }
            float rb = baseDiameter * 0.5f;
            float rt = topDiameter * 0.5f;
            float lean = (rb - rt) / height;
            int stride = sides + 1;
            int start = vertices.Count;

            for (int r = 0; r < 2; r++)
            {
                float y = baseCentre.y + (r == 0 ? 0f : height);
                float radius = r == 0 ? rb : rt;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = 2f * Mathf.PI * i / sides;
                    Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    vertices.Add(new Vector3(baseCentre.x + dir.x * radius, y, baseCentre.z + dir.z * radius));
                    normals.Add(new Vector3(dir.x, lean, dir.z).normalized);
                    uvs.Add(new Vector2((float)i / sides, y));
                }
            }

            for (int i = 0; i < sides; i++)
            {
                float mid = 2f * Mathf.PI * (i + 0.5f) / sides;
                Vector3 outward = new Vector3(Mathf.Sin(mid), lean, Mathf.Cos(mid));
                AddQuad(start + i, start + i + 1, start + stride + i + 1, start + stride + i, outward, submesh);
            }

            if (rt > 0.0005f)
            {
                AddFan(new Vector3(baseCentre.x, baseCentre.y + height, baseCentre.z), rt, sides, Vector3.up,
                    submesh);
            }
        }

        /// <summary> A flat disc, used to cap a tube. </summary>
        public void AddFan(Vector3 centre, float radius, int sides, Vector3 outward, int submesh)
        {
            if (sides < 3) { sides = 3; }
            int hub = vertices.Count;
            vertices.Add(centre);
            normals.Add(outward);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices.Add(centre + dir * radius);
                normals.Add(outward);
                uvs.Add(new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                AddTriangleOriented(hub, hub + 1 + i, hub + 2 + i, outward, submesh);
            }
        }

        /// <summary> An axis-aligned box: the cross-arm, a rack, a bracket. Six faces with their own vertices, so the
        /// edges stay sharp -- a shared-vertex box is a rounded box. </summary>
        public void AddBox(Vector3 centre, Vector3 halfExtents, int submesh)
        {
            AddOrientedBox(centre, Vector3.right * halfExtents.x, Vector3.up * halfExtents.y,
                Vector3.forward * halfExtents.z, submesh);
        }

        /// <summary> A box from three half-extent vectors, which need not be axis aligned. This is the one the braces
        /// use: the long axis is the brace's own line. </summary>
        public void AddOrientedBox(Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, int submesh)
        {
            AddBoxFace(centre, ax, ay, az, submesh);
            AddBoxFace(centre, -ax, ay, -az, submesh);
            AddBoxFace(centre, ay, az, ax, submesh);
            AddBoxFace(centre, -ay, az, -ax, submesh);
            AddBoxFace(centre, az, ax, ay, submesh);
            AddBoxFace(centre, -az, ax, -ay, submesh);
        }

        /// <summary> One face of a box: its outward half-axis and the two half-axes that span it. </summary>
        private void AddBoxFace(Vector3 centre, Vector3 outHalf, Vector3 uHalf, Vector3 vHalf, int submesh)
        {
            Vector3 outward = outHalf.sqrMagnitude > 0f ? outHalf.normalized : Vector3.up;
            Vector3 face = centre + outHalf;
            int start = vertices.Count;
            AddFaceVertex(face - uHalf - vHalf, outward, 0f, 0f);
            AddFaceVertex(face + uHalf - vHalf, outward, uHalf.magnitude * 2f, 0f);
            AddFaceVertex(face + uHalf + vHalf, outward, uHalf.magnitude * 2f, vHalf.magnitude * 2f);
            AddFaceVertex(face - uHalf + vHalf, outward, 0f, vHalf.magnitude * 2f);
            AddQuad(start, start + 1, start + 2, start + 3, outward, submesh);
        }

        private void AddFaceVertex(Vector3 position, Vector3 normal, float u, float v)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(new Vector2(u, v));
        }

        /// <summary> A round tube between two points: a wire, a guy, the ground wire. Sides default low because at any
        /// distance a wire is a line and its silhouette is its diameter, not its roundness. </summary>
        public void AddSegment(Vector3 from, Vector3 to, float diameter, int sides, int submesh)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length <= 1e-5f || diameter <= 0f) { return; }
            if (sides < 3) { sides = 3; }
            Vector3 forward = axis / length;
            Vector3 right, up;
            Frame(forward, out right, out up);

            float radius = diameter * 0.5f;
            int stride = sides + 1;
            int start = vertices.Count;
            for (int r = 0; r < 2; r++)
            {
                Vector3 centre = r == 0 ? from : to;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = 2f * Mathf.PI * i / sides;
                    Vector3 dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices.Add(centre + dir * radius);
                    normals.Add(dir);
                    uvs.Add(new Vector2((float)i / sides, r == 0 ? 0f : length));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                float mid = 2f * Mathf.PI * (i + 0.5f) / sides;
                Vector3 outward = right * Mathf.Cos(mid) + up * Mathf.Sin(mid);
                AddQuad(start + i, start + i + 1, start + stride + i + 1, start + stride + i, outward, submesh);
            }
        }

        /// <summary> A round tube along a polyline, with the rings SHARED between neighbouring segments: a conductor,
        /// hanging.
        ///
        /// The fourth primitive, and the only one that is not part of a pole. It is here rather than in a buffer of its
        /// own because a conductor is the same round tube <see cref="AddSegment"/> makes -- the only difference is that
        /// it bends -- and a second class holding a second copy of the ring generation, the derived winding and
        /// <see cref="ToMesh"/> is exactly the duplication this class's own summary forbids. <see cref="AddSegment"/>
        /// stays for the guys and the ground wire: a straight run is framed once, and framing it once is right.
        ///
        /// Rings are SHARED, so a twelve-segment span costs thirteen rings and not twenty-four, and the tube reads as
        /// one wire rather than as a line of separate pipes butted together. Each ring is framed from the direction the
        /// wire is going AT that ring -- the average of the two segments meeting there -- which keeps the section
        /// perpendicular to the curve at a kink instead of pinching it. A repeated point produces a ring of zero length
        /// rather than a missing one, so the index arithmetic below never has to skip anything. </summary>
        public void AddPolylineTube(IList<Vector3> points, float diameter, int sides, int submesh)
        {
            if (points == null || points.Count < 2 || diameter <= 0f) { return; }
            if (sides < 3) { sides = 3; }

            float radius = diameter * 0.5f;
            int stride = sides + 1;
            int start = vertices.Count;
            float travelled = 0f;
            Vector3 carried = Vector3.zero;

            for (int p = 0; p < points.Count; p++)
            {
                if (p > 0) { travelled += (points[p] - points[p - 1]).magnitude; }
                Vector3 forward = PolylineTangent(points, p, carried);
                carried = forward;

                Vector3 right, up;
                Frame(forward, out right, out up);
                for (int i = 0; i <= sides; i++)
                {
                    float angle = 2f * Mathf.PI * i / sides;
                    Vector3 dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices.Add(points[p] + dir * radius);
                    normals.Add(dir);
                    uvs.Add(new Vector2((float)i / sides, travelled));
                }
            }

            for (int r = 1; r < points.Count; r++)
            {
                int a = start + (r - 1) * stride;
                int b = start + r * stride;
                for (int i = 0; i < sides; i++)
                {
                    Vector3 outward = normals[a + i] + normals[a + i + 1] + normals[b + i] + normals[b + i + 1];
                    AddQuad(a + i, a + i + 1, b + i + 1, b + i, outward, submesh);
                }
            }
        }

        /// <summary> A square-section bar between two points: a knee brace, a flat. </summary>
        public void AddSegmentBox(Vector3 from, Vector3 to, float thickness, int submesh)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length <= 1e-5f || thickness <= 0f) { return; }
            Vector3 forward = axis / length;
            Vector3 right, up;
            Frame(forward, out right, out up);
            AddOrientedBox(from + axis * 0.5f, forward * (length * 0.5f), up * (thickness * 0.5f),
                right * (thickness * 0.5f), submesh);
        }

        /// <summary> Which way a polyline is going at one of its own points: the average of the segments that meet
        /// there, so the ring at a kink faces the corner and not one of its two arms. A point with no usable neighbour
        /// -- a repeat, or the whole line degenerate -- keeps the direction carried in from the last ring, and only a
        /// polyline with no length at all falls through to world forward. </summary>
        private static Vector3 PolylineTangent(IList<Vector3> points, int index, Vector3 carried)
        {
            Vector3 sum = Vector3.zero;
            if (index > 0)
            {
                Vector3 back = points[index] - points[index - 1];
                if (back.sqrMagnitude > 1e-10f) { sum += back.normalized; }
            }
            if (index + 1 < points.Count)
            {
                Vector3 ahead = points[index + 1] - points[index];
                if (ahead.sqrMagnitude > 1e-10f) { sum += ahead.normalized; }
            }
            if (sum.sqrMagnitude > 1e-10f) { return sum.normalized; }
            if (carried.sqrMagnitude > 1e-10f) { return carried.normalized; }
            return Vector3.forward;
        }

        /// <summary> Two axes perpendicular to a direction, chosen deterministically: the world up unless the
        /// direction is nearly vertical, in which case world forward. A frame picked from whichever axis happens to be
        /// least parallel would rotate a wire's texture as its slope crossed the threshold. </summary>
        private static void Frame(Vector3 forward, out Vector3 right, out Vector3 up)
        {
            Vector3 reference = Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up;
            right = Vector3.Cross(reference, forward);
            if (right.sqrMagnitude < 1e-8f) { right = Vector3.right; }
            right = right.normalized;
            up = Vector3.Cross(forward, right).normalized;
        }

        /// <summary> Two triangles for a quad given in order round its rim, oriented to face
        /// <paramref name="outward"/>. </summary>
        private void AddQuad(int a, int b, int c, int d, Vector3 outward, int submesh)
        {
            AddTriangleOriented(a, b, c, outward, submesh);
            AddTriangleOriented(a, c, d, outward, submesh);
        }

        /// <summary> One triangle, wound so that it faces <paramref name="outward"/>.
        ///
        /// Unity treats the winding whose Cross(p1 - p0, p2 - p0) points along the surface normal as the front face
        /// -- which is checkable rather than remembered: Unity's own Quad primitive has vertices (-0.5,-0.5,0),
        /// (0.5,0.5,0), (0.5,-0.5,0) on its first triangle and a normal of (0,0,-1), and that cross product is
        /// (0,0,-1). So the test below is a comparison against the stored normal, and a face handed in the wrong
        /// order is reversed rather than left to render black. </summary>
        private void AddTriangleOriented(int i0, int i1, int i2, Vector3 outward, int submesh)
        {
            List<int> tris = triangles[submesh < 0 ? 0 : (submesh >= SubmeshCount ? SubmeshCount - 1 : submesh)];
            Vector3 geometric = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
            if (Vector3.Dot(geometric, outward) >= 0f)
            {
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
            }
            else
            {
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
            }
        }

        /// <summary> A CROSS-SECTION swept along a line of frames: the ballast prism, a rail, a crossing panel.
        ///
        /// This is the one method D-5's rail body needs that a pole line never did, and it is here rather than in the
        /// rail file so that there is one sweeper and not two. A pole is an assembly of boxes and tubes; a railway is a
        /// PROFILE dragged along a curve, which is the same idea the vendored <c>SplinatedMeshMaker</c> applies to a
        /// road -- but that one sweeps along a <c>SplineC</c>, and a rail alignment is not one: it is a list of samples
        /// carrying their own CANT, so the frame at each station is rolled and cannot come from a spline's own basis.
        ///
        /// <paramref name="section"/> is written in the frame plane, x across and y up, and is OPEN: it produces a
        /// ribbon, not a solid. That is deliberate -- a ballast prism's underside is against the ground and a crossing
        /// panel's is against the ballast, so the faces a closed section would add are faces nothing can see. A caller
        /// wanting a closed profile (a rail's own outline) simply repeats the first point last.
        ///
        /// Section points are taken LEFT TO RIGHT, so each segment's outward normal is its own direction turned a
        /// quarter turn anticlockwise and the ribbon faces up. UVs are in METRES -- u across the section, v along the
        /// sweep -- because every material this carries is a tiling one and a normalised UV would stretch the ballast
        /// of a long piece. </summary>
        public void AddSweptStrip(IList<Vector3> origins, IList<Vector3> rights, IList<Vector3> ups,
            IList<Vector2> section, int submesh)
        {
            if (origins == null || rights == null || ups == null || section == null) { return; }
            if (origins.Count < 2 || section.Count < 2) { return; }

            int across = section.Count;
            int start = vertices.Count;
            float travelled = 0f;

            for (int r = 0; r < origins.Count; r++)
            {
                if (r > 0) { travelled += (origins[r] - origins[r - 1]).magnitude; }
                float u = 0f;
                for (int i = 0; i < across; i++)
                {
                    if (i > 0) { u += (section[i] - section[i - 1]).magnitude; }
                    Vector2 flat = SectionNormal(section, i);
                    Vector3 normal = (rights[r] * flat.x + ups[r] * flat.y).normalized;
                    vertices.Add(origins[r] + rights[r] * section[i].x + ups[r] * section[i].y);
                    normals.Add(normal);
                    uvs.Add(new Vector2(u, travelled));
                }
            }

            for (int r = 1; r < origins.Count; r++)
            {
                int a = start + (r - 1) * across;
                int b = start + r * across;
                for (int i = 0; i < across - 1; i++)
                {
                    Vector3 outward = normals[a + i] + normals[a + i + 1] + normals[b + i] + normals[b + i + 1];
                    AddQuad(a + i, a + i + 1, b + i + 1, b + i, outward, submesh);
                }
            }
        }

        /// <summary> The outward normal of a section at one of its points, in the section's own plane: the average of
        /// the segments meeting there, each turned a quarter turn anticlockwise from its direction. Averaging rather
        /// than picking one is what makes the shoulder of a ballast prism read as a fold and not as a crease with two
        /// different lightings on its two sides. </summary>
        private static Vector2 SectionNormal(IList<Vector2> section, int index)
        {
            Vector2 sum = Vector2.zero;
            if (index > 0) { sum += Perpendicular(section[index] - section[index - 1]); }
            if (index < section.Count - 1) { sum += Perpendicular(section[index + 1] - section[index]); }
            if (sum.sqrMagnitude < 1e-10f) { return Vector2.up; }
            return sum.normalized;
        }

        /// <summary> A direction turned a quarter turn anticlockwise, so a section written left to right faces up.
        /// </summary>
        private static Vector2 Perpendicular(Vector2 d)
        {
            Vector2 p = new Vector2(-d.y, d.x);
            return p.sqrMagnitude < 1e-10f ? Vector2.zero : p.normalized;
        }

        /// <summary> Hand the buffers to a Mesh. Two submeshes always, even when the fittings submesh is empty, so a
        /// renderer's material array is the same length for every pole and a caller never has to ask which shape it
        /// got. Bounds and tangents are recalculated; UV2 is not generated, because nothing here is lightmapped --
        /// the poles stream in and out with the chunk. </summary>
        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            if (vertices.Count > 65534) { mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = SubmeshCount;
            for (int i = 0; i < SubmeshCount; i++)
            {
                mesh.SetTriangles(triangles[i], i, true);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
#endif

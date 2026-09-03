#region "Imports"
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A world-space box describing WHICH terrain a modification touched. It exists so the gateway can do
    /// the one thing RM-57 singles out as easy to get wrong: accumulate the affected region across a whole build and
    /// notify the provider ONCE with the union, instead of once per segment. Under-notify and a road appears with
    /// trees still standing in it; over-notify and every build rebuilds the world.
    ///
    /// The maths is in <c>double</c> and touches nothing but its arguments, so a union is bit-identical however many
    /// segments contributed and in whatever order (C-06). <see cref="ToBounds"/> is the only place the engine's
    /// float <see cref="Bounds"/> appears, at the moment the region is handed to the provider. </summary>
    public struct TerrainRegion
    {
        /// <summary> False while nothing has been added: an empty region is NOT a zero-sized box at the origin, and
        /// notifying with one would tell the provider to heal the wrong place. </summary>
        public bool hasArea;

        public double minX;
        public double minY;
        public double minZ;
        public double maxX;
        public double maxY;
        public double maxZ;

        /// <summary> WHAT is supposed to happen to the ground in this box -- RM-98/RM-99.
        ///
        /// This field is the fix for the author's report that choosing a tunnel cuts the hill the tunnel was supposed to
        /// pass under. A region is the only thing that crosses the linker, and a region that says nothing but "this
        /// ground changed" is a region the host is entitled to conform: it healed the corridor, and the hill went with
        /// it. Carrying the treatment means a bored span crosses as <see cref="GroundTreatment.LeaveIntact"/> and both
        /// sides can honour it without either one reaching into the other's code.
        ///
        /// It defaults to <see cref="GroundTreatment.Conform"/> for every existing caller, so a region built the way
        /// regions were built before RM-98 behaves exactly as it did. </summary>
        public GroundTreatment treatment;

        /// <summary> A region that has been told about nothing yet. </summary>
        public static TerrainRegion Empty
        {
            get
            {
                TerrainRegion r;
                r.hasArea = false;
                r.minX = 0.0; r.minY = 0.0; r.minZ = 0.0;
                r.maxX = 0.0; r.maxY = 0.0; r.maxZ = 0.0;
                r.treatment = GroundTreatment.Conform;
                return r;
            }
        }

        /// <summary> A region containing exactly one world point. </summary>
        public static TerrainRegion FromPoint(double x, double y, double z)
        {
            TerrainRegion r;
            r.hasArea = true;
            r.minX = x; r.maxX = x;
            r.minY = y; r.maxY = y;
            r.minZ = z; r.maxZ = z;
            r.treatment = GroundTreatment.Conform;
            return r;
        }

        /// <summary> A region from explicit extents, normalised so a caller cannot create an inverted box. </summary>
        public static TerrainRegion FromExtents(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            TerrainRegion r;
            r.hasArea = true;
            r.minX = x1 < x2 ? x1 : x2; r.maxX = x1 < x2 ? x2 : x1;
            r.minY = y1 < y2 ? y1 : y2; r.maxY = y1 < y2 ? y2 : y1;
            r.minZ = z1 < z2 ? z1 : z2; r.maxZ = z1 < z2 ? z2 : z1;
            r.treatment = GroundTreatment.Conform;
            return r;
        }

        /// <summary> The same box, labelled with what the ground is supposed to do. Written as a copy rather than a
        /// setter so a region can be built by the existing factories and then labelled at the one place that knows --
        /// the tunnel policy -- without every factory growing a treatment argument. </summary>
        public TerrainRegion WithTreatment(GroundTreatment groundTreatment)
        {
            TerrainRegion r = this;
            r.treatment = groundTreatment;
            return r;
        }

        /// <summary> A region over a horizontal footprint with an explicit vertical span -- the shape a road cut,
        /// a building pad or a splat patch actually has. </summary>
        public static TerrainRegion FromFootprint(double centreX, double centreZ, double halfX, double halfZ,
            double minHeight, double maxHeight)
        {
            double hx = halfX < 0.0 ? -halfX : halfX;
            double hz = halfZ < 0.0 ? -halfZ : halfZ;
            return FromExtents(centreX - hx, minHeight, centreZ - hz, centreX + hx, maxHeight, centreZ + hz);
        }

        /// <summary> A region covering a whole terrain tile, from its world origin and size. </summary>
        public static TerrainRegion FromTerrainTile(Vector3 terrainWorldPosition, Vector3 terrainSize)
        {
            return FromExtents(terrainWorldPosition.x, terrainWorldPosition.y, terrainWorldPosition.z,
                terrainWorldPosition.x + terrainSize.x,
                terrainWorldPosition.y + terrainSize.y,
                terrainWorldPosition.z + terrainSize.z);
        }

        /// <summary> This region grown to also contain <paramref name="other"/>. Unioning with an empty region is
        /// the identity, in both directions, which is what makes an accumulation over a build order-independent.
        ///
        /// The union's treatment is the MORE PROTECTIVE of the two, and that direction is deliberate. A box that covers
        /// both a hill a tunnel runs under and the open approach beside it covers ground that must not be cut, so the
        /// only safe label for the whole box is the one that forbids cutting. Taking the other direction is how a
        /// hundred-node road with one tunnel in it ends up announced as one big cut and the hill disappears -- which is
        /// exactly the defect this field exists to stop. Callers that need the approach conformed announce it as its own
        /// region rather than folding it into the protected one; <see cref="TunnelLinkerBridge"/> does that split.
        /// </summary>
        public TerrainRegion Union(TerrainRegion other)
        {
            if (!other.hasArea) { return this; }
            if (!hasArea) { return other; }

            TerrainRegion r;
            r.hasArea = true;
            r.minX = minX < other.minX ? minX : other.minX;
            r.minY = minY < other.minY ? minY : other.minY;
            r.minZ = minZ < other.minZ ? minZ : other.minZ;
            r.maxX = maxX > other.maxX ? maxX : other.maxX;
            r.maxY = maxY > other.maxY ? maxY : other.maxY;
            r.maxZ = maxZ > other.maxZ ? maxZ : other.maxZ;
            r.treatment = TunnelTerrainPolicy.MoreProtective(treatment, other.treatment);
            return r;
        }

        /// <summary> This region grown to contain one more world point. </summary>
        public TerrainRegion Encapsulate(double x, double y, double z)
        {
            return Union(FromPoint(x, y, z));
        }

        /// <summary> This region grown outward on every side, for the skirt a provider needs to blend into. </summary>
        public TerrainRegion Expanded(double metres)
        {
            if (!hasArea || metres <= 0.0) { return this; }
            TerrainRegion r = this;
            r.minX -= metres; r.minZ -= metres; r.minY -= metres;
            r.maxX += metres; r.maxZ += metres; r.maxY += metres;
            return r;
        }

        /// <summary> True when the two boxes share any volume (touching faces count). </summary>
        public bool Intersects(TerrainRegion other)
        {
            if (!hasArea || !other.hasArea) { return false; }
            return minX <= other.maxX && maxX >= other.minX
                && minY <= other.maxY && maxY >= other.minY
                && minZ <= other.maxZ && maxZ >= other.minZ;
        }

        /// <summary> True when every part of <paramref name="other"/> lies inside this region. </summary>
        public bool Contains(TerrainRegion other)
        {
            if (!other.hasArea) { return true; }
            if (!hasArea) { return false; }
            return minX <= other.minX && maxX >= other.maxX
                && minY <= other.minY && maxY >= other.maxY
                && minZ <= other.minZ && maxZ >= other.maxZ;
        }

        public double SizeX { get { return hasArea ? maxX - minX : 0.0; } }
        public double SizeY { get { return hasArea ? maxY - minY : 0.0; } }
        public double SizeZ { get { return hasArea ? maxZ - minZ : 0.0; } }

        /// <summary> Horizontal footprint area in square metres. </summary>
        public double FootprintArea { get { return SizeX * SizeZ; } }

        /// <summary> The engine box to hand the provider. Called ONCE per notification, at the boundary. </summary>
        public Bounds ToBounds()
        {
            if (!hasArea) { return new Bounds(Vector3.zero, Vector3.zero); }
            Vector3 centre = new Vector3(
                (float)((minX + maxX) * 0.5), (float)((minY + maxY) * 0.5), (float)((minZ + maxZ) * 0.5));
            Vector3 size = new Vector3((float)SizeX, (float)SizeY, (float)SizeZ);
            return new Bounds(centre, size);
        }

        /// <summary> A region from an engine box, for callers that already hold one. </summary>
        public static TerrainRegion FromBounds(Bounds bounds)
        {
            Vector3 lo = bounds.min;
            Vector3 hi = bounds.max;
            return FromExtents(lo.x, lo.y, lo.z, hi.x, hi.y, hi.z);
        }

        /// <summary> One line naming the box, for the ledger and the gateway's report. The treatment is appended only
        /// when it is not the ordinary <see cref="GroundTreatment.Conform"/>, so every description written before RM-98
        /// -- and every gate that compares one -- reads exactly as it did. </summary>
        public string Describe()
        {
            if (!hasArea) { return "(no region)"; }
            string box = string.Format(CultureInfo.InvariantCulture,
                "x[{0:0.###}..{1:0.###}] y[{2:0.###}..{3:0.###}] z[{4:0.###}..{5:0.###}]",
                minX, maxX, minY, maxY, minZ, maxZ);
            return treatment == GroundTreatment.Conform ? box : box + " " + treatment.ToString();
        }
    }
}

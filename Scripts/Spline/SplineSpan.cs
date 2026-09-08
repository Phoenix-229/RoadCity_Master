namespace RoadCityMaster
{
    /// <summary> One bridge run, or one tunnel run, on a spline: the pair of spline times it starts and ends at.
    ///
    /// B-25. <see cref="SplineC.BridgeParams"/> and <see cref="SplineC.TunnelParams"/> held these spans as
    /// <c>KeyValuePair&lt;float, float&gt;</c>, which Unity cannot serialize under any attribute -- it is a BCL struct
    /// whose <c>Key</c> and <c>Value</c> are readonly PROPERTIES, and Unity serializes fields. Both lists were
    /// therefore marked <c>[System.NonSerialized]</c> and lived only as long as the pass that built them:
    /// <see cref="SplineC.Setup"/> fills them, <c>SplineC</c> has no Awake or Start, and <c>Road.cs</c> calls Setup()
    /// only while constructing a road. On a reopened scene both lists were null, so every bridge and tunnel query in
    /// <c>SplineC</c>, <c>TunnelRunReader</c> and <c>GroundConformRecord</c> took its null branch and answered "no
    /// bridge, no tunnel" for roads that have both. Two plain float fields do serialize, so a span is now written into
    /// the scene with the road that produced it and survives the reload.
    ///
    /// A struct, and not the two parallel arrays that <c>RoadDefKeysArray</c> / <c>RoadDefValuesArray</c> use beside
    /// the declaration: parallel arrays are that author's answer to serializing a Dictionary, where the two sides are
    /// a set of keys and a set of values. Here the two floats are one range, and every reader takes them together --
    /// splitting them would let a start survive without its end. </summary>
    [System.Serializable]
    public struct SplineSpan
    {
        /// <summary> Spline time the run starts at: 0 at the first node, 1 at the last, the same scale as
        /// <c>SplineN.time</c>. </summary>
        public float start;

        /// <summary> Spline time the run ends at, on that same scale. </summary>
        public float end;


        public SplineSpan(float _start, float _end)
        {
            start = _start;
            end = _end;
        }
    }
}

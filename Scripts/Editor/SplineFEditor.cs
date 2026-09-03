#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(SplineF))]
    public class SplineFEditor : Editor
    {
        private SplineF splineF;


        private void OnEnable()
        {
            splineF = (SplineF)target;
        }


        public override void OnInspectorGUI()
        {
            //Intentionally left empty.
        }
    }
}
#endif

namespace RoadCityMaster.City.Editor
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    /// <summary>
    /// B-27 / D-10: Quay pad, rail spur, and yard grid placement.
    /// This builder resolves the physical placement of port infrastructure along
    /// navigable water bodies. It computes the precise dimensions required by the
    /// port's designated VesselClass and generates a paved quay pad and container
    /// yard. It also constructs a BranchLine rail spur extending from the mainline
    /// to serve the yard.
    /// </summary>
    public static class PortPlacementBuilder
    {
        private const string UndoLabel = "Build port placement";

        /// <summary>
        /// Orchestrates the placement of the quay pad, the rail spur, and the
        /// yard grid at the location determined by the PortSiting pass.
        /// </summary>
        /// <param name="holder">The root transform for generated port geometry.</param>
        /// <param name="siting">The resolved siting data including shoreline and vessel class.</param>
        /// <param name="quayMaterial">Material used for the paved surfaces.</param>
        /// <param name="mainLineSettings">Settings inherited by the branch rail spur.</param>
        public static void BuildPlacement(
            Transform holder,
            PortSitingReport siting,
            Material quayMaterial,
            RailAlignmentSettings mainLineSettings)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            if (siting.waterPort.resolved && siting.waterPort.shorePoint != null)
            {
                // ---------------------------------------------------------
                // 1. Determine Operational Dimensions based on Vessel Class
                // ---------------------------------------------------------
                // The prompt dictates specific widths proportional to vessel capacity.
                float quayWidth = 50f; // Default barge size
                switch (siting.waterPort.vesselClass)
                {
                    case VesselClass.Handysize: quayWidth = 150f; break;
                    case VesselClass.Panamax:   quayWidth = 250f; break;
                    case VesselClass.Suezmax:   quayWidth = 350f; break;
                }
                
                // Depth comes from the hub requirements class
                float forecourtDepth = TransportHub.HubRequirementsClass.For(HubKind.Port).forecourtDepthMetres;

                // ---------------------------------------------------------
                // 2. Generate Conformed Quay Pad
                // ---------------------------------------------------------
                // The quay pad must sit flat on the land side of the shoreline, matching B-15's freeboard rule.
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pad.name = CityHierarchy.NameFor(GeneratedKind.Buildings, "port.quay", 0);
                pad.transform.SetParent(holder, false);
                
                // Align to shore normal to face the water perfectly.
                pad.transform.position = siting.waterPort.shorePoint.position;
                pad.transform.rotation = Quaternion.LookRotation(siting.waterPort.shorePoint.forward) * Quaternion.Euler(90, 0, 0);
                pad.transform.localScale = new Vector3(quayWidth, forecourtDepth, 1f);

                if (quayMaterial != null && pad.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.sharedMaterial = quayMaterial;
                }
                Undo.RegisterCreatedObjectUndo(pad, UndoLabel);

                // ---------------------------------------------------------
                // 3. Generate Yard Grid (Container Stacking)
                // ---------------------------------------------------------
                // The yard sits directly behind the quay.
                GameObject yard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                yard.name = CityHierarchy.NameFor(GeneratedKind.Buildings, "port.yard", 0);
                yard.transform.SetParent(holder, false);
                // Offset backward from the quay by exactly the forecourt depth
                yard.transform.position = siting.waterPort.shorePoint.position - (siting.waterPort.shorePoint.forward * forecourtDepth);
                yard.transform.rotation = Quaternion.Euler(90, 0, 0);
                // The yard is a square grid equal to the quay width.
                yard.transform.localScale = new Vector3(quayWidth, quayWidth, 1f);
                
                if (quayMaterial != null && yard.TryGetComponent<MeshRenderer>(out var yRenderer))
                {
                    yRenderer.sharedMaterial = quayMaterial;
                }
                Undo.RegisterCreatedObjectUndo(yard, UndoLabel);

                // ---------------------------------------------------------
                // 4. Construct Rail Spur into the Yard
                // ---------------------------------------------------------
                // The spur branches off the mainline as a single track, un-electrified BranchLine.
                GameObject spurObj = new GameObject(CityHierarchy.NameFor(GeneratedKind.Rail, "port.spur", 0));
                spurObj.transform.SetParent(holder, false);
                RailAlignment spur = spurObj.AddComponent<RailAlignment>();
                
                // Inherit alignment physics and materials from the mainline, but demote to branch.
                spur.settings = ScriptableObject.CreateInstance<RailAlignmentSettings>();
                if (mainLineSettings != null)
                {
                    EditorUtility.CopySerialized(mainLineSettings, spur.settings);
                }
                spur.settings.lineClass = RailLineClass.BranchLine;
                spur.settings.electrified = false; // Port spurs generally rely on diesel shunters.
                
                // Plot the spur from the yard center towards the network.
                Vector3 yardCenter = yard.transform.position;
                Vector3 yardEntrance = yardCenter - (siting.waterPort.shorePoint.forward * (quayWidth / 2f));

                spur.controlPoints = new List<Vector3>
                {
                    yardCenter, // Terminates inside the yard for crane loading
                    yardEntrance, // Exits the yard boundary
                    yardEntrance - (siting.waterPort.shorePoint.forward * 100f) // Connects to mainline
                };

                Undo.RegisterCreatedObjectUndo(spurObj, UndoLabel);
            }

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}

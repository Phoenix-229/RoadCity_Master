#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Every generated document in one call.
    ///
    /// Seven registers splice tables into seven markdown files, and a gate for each checks that what is on disk still
    /// matches the register that generated it. Each writer has its own menu item, which is right for an author changing
    /// one table -- and wrong for a headless verification run, where a register edited in code and a document left
    /// unwritten reads as a gate failure rather than as the missing click it is. RM-68 hit exactly that: the shader
    /// inventory rows changed, and `ShaderInventoryGate` case 11 would have reported drift against a document nothing
    /// had regenerated.
    ///
    /// So this is the one place that knows the full set, and being one short of the full set is the same defect as
    /// never having the list at all: the availability table was the register nobody added here, and it is the one that
    /// drifted. Adding a register means adding it here, and a gate that reports drift after this has run is reporting a
    /// real disagreement between code and disk.
    ///
    /// Two of the writers live in the city layer's own namespace and one of those writes the SAME table into two
    /// documents -- this side's manual and the world side's -- because an integrator reading one and a user reading the
    /// other must not be told different things about the same feature. They are named in full here rather than imported,
    /// so a reader of this list can see which side of the tool each document belongs to.
    ///
    /// Deliberately NOT named `Run(out string)`: `RcmGateSuite` discovers gates by that signature, and a document writer
    /// is not a gate. It writes.
    /// </summary>
    public static class RcmDocumentWriters
    {
        private const string Prefix = "RCMDOCS";
        private const string MenuWrite = "Window/Road City Master/Documentation/Write every generated document";

        [MenuItem(MenuWrite, false, 500)]
        public static void WriteAllFromMenu()
        {
            string report;
            bool ok = WriteAll(out report);
            Debug.Log(report);
            if (ok) { AssetDatabase.Refresh(); }
            else { Debug.LogWarning(Prefix + " at least one document did not write; see the report above."); }
        }

        /// <summary> Batchmode entry point. </summary>
        public static void WriteAllBatch()
        {
            string report;
            bool ok = WriteAll(out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary> The shape every writer shares. A delegate rather than reflection: the compiler then checks that
        /// each writer still exists and still takes a path and an out message. </summary>
        private delegate bool WriteDocument(string path, out string message);

        /// <summary> Writes every register's document -- seven registers, and one of them writes two files, so the
        /// count of documents is not the count of writers and neither number is hard-coded here. Every writer is
        /// called even when an earlier one fails, because "which documents are stale" is the question the report has
        /// to answer, and stopping at the first failure hides the rest.
        /// </summary>
        public static bool WriteAll(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Prefix).Append(" BEGIN\n");
            List<string> failures = new List<string>();

            WriteOne("disposition list", DispositionDocWriter.DocPath(),
                DispositionDocWriter.WriteInto, builder, failures);
            WriteOne("shader and material inventory", ShaderInventoryWriter.DocPath(),
                ShaderInventoryWriter.WriteInto, builder, failures);
            WriteOne("material port record", MaterialPortWriter.DocPath(),
                MaterialPortWriter.WriteInto, builder, failures);
            WriteOne("inspector consistency checklist", InspectorConsistencyDocWriter.ChecklistPath(),
                InspectorConsistencyDocWriter.WriteInto, builder, failures);
            WriteOne("tool manual", ToolManualWriter.ManualPath(),
                ToolManualWriter.WriteInto, builder, failures);
            WriteOne("geometry provenance table",
                RoadCityMaster.City.Editor.GeometryProvenanceTableWriter.ManualPath(),
                RoadCityMaster.City.Editor.GeometryProvenanceTableWriter.WriteInto, builder, failures);

            // The availability table is the one register with TWO documents to keep in step -- the city layer's manual
            // and the world side's own -- and it was the one register missing from this list, which is precisely how it
            // came to be six failures deep in drift while the other documents stayed current. The paths come from the
            // writer rather than from here, so a third consumer is added there once and is written by this loop with no
            // edit on this side.
            string[] availabilityDocuments = RoadCityMaster.City.Editor.TerrainFeatureTableWriter.DocumentPaths();
            for (int i = 0; i < availabilityDocuments.Length; i++)
            {
                WriteOne("terrain availability table", availabilityDocuments[i],
                    RoadCityMaster.City.Editor.TerrainFeatureTableWriter.WriteInto, builder, failures);
            }

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append(Prefix).Append(" FAIL ").Append(failures[i]).Append('\n');
            }
            builder.Append(Prefix).Append(" result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(Prefix).Append(" END\n");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> One writer, named in the report so a stale document says which menu item owns it. </summary>
        private static void WriteOne(string name, string path, WriteDocument writer, StringBuilder builder,
            List<string> failures)
        {
            string message;
            bool written = writer(path, out message);
            builder.Append(Prefix).Append(written ? " wrote  " : " FAILED ").Append(name).Append("  ").Append(message)
                .Append('\n');
            if (!written) { failures.Add(name + ": " + message); }
        }


    }
}
#endif

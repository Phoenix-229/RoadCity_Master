#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-105's gate: proves the <c>.rao</c> library format is a format — versioned, round-tripping, and
    /// refusing what it cannot read — rather than three deserialise calls in a try/catch.
    ///
    /// Every case here exists because of a specific way the old readers lost work silently.
    ///
    /// ROUND TRIP is first because it is the claim the format lives or dies by: a library written by this build and read
    /// back by this build must produce the same bytes. If it does not, the tool is quietly editing the author's presets
    /// every time one is opened and saved, and the drift is invisible until a shape comes out wrong months later. The
    /// case is run twice, once stamped and once with the stamp at zero, because those are the two shapes on disk.
    ///
    /// THE SHIPPED LIBRARY is read in full. 110 files went out with the tool, and none of them has a version stamp; if
    /// the version rule were written the wrong way round — refusing what is older instead of what is newer — this case
    /// is the one that would go red, and it would go red on every file at once rather than on whichever preset an author
    /// happened to open first.
    ///
    /// THE THREE REFUSALS are the point of the whole exercise. A record of another KIND must come back as silence, since
    /// a group preset legitimately holds a header, sections and edge objects and each is offered to every reader in
    /// turn; a MALFORMED record must come back with the file named and the cause stated; a record from a NEWER build
    /// must be refused by number rather than loaded with every unknown element defaulted to zero. The old code answered
    /// all three with the same <c>null</c>, which is how a corrupt cross-section was retried as an edge object, failed
    /// again, and vanished from the author's road with nothing written anywhere.
    ///
    /// THE BOM case looks like pedantry and is not. <c>RootUtils.GetString</c> writes through <c>Encoding.UTF8</c> and
    /// therefore emits a byte-order mark, so a record held in memory begins with U+FEFF while the same record read from
    /// disk does not. The discriminator has to answer identically for both, or it disagrees with the deserialiser about
    /// a file both can read.
    ///
    /// THE SCHEMA case is what keeps <c>RaoSchema</c> honest. A schema is only worth reading if it still describes what
    /// the tool writes, and <c>XmlSerializer</c> writes out every public field it is given without being asked, so the
    /// day someone adds a field to a record class the format changes and no other case in this gate notices — the round
    /// trip still round-trips, the shipped library still reads. So the schema does not assert its element names, it
    /// reflects them off the record types, and this case requires the documented families to partition that reflected
    /// set exactly: nothing unclaimed, nothing claimed twice, no name left behind by a field that has been removed.
    /// The failure lands on whoever adds the field, while they still know what it is and what it is measured in.
    ///
    /// Report lines are prefixed RCMRAO. </summary>
    public static class RaoLibraryGate
    {
        [MenuItem("Window/Road City Master/Testing/Library format (.rao version, round trip, refusals)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.RaoLibraryGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }


        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMRAO BEGIN\n");
            List<string> failures = new List<string>();

            CaseAWrittenRecordReadsBackByteIdentical(builder, failures);
            CaseEveryShippedLibraryReads(builder, failures);
            CaseAnUnstampedRecordIsLegacyAndKeepsItsFields(builder, failures);
            CaseARecordOfAnotherKindIsSilence(builder, failures);
            CaseAMalformedRecordIsRefusedWithACause(builder, failures);
            CaseANewerVersionIsRefusedByNumber(builder, failures);
            CaseTheDiscriminatorIgnoresAByteOrderMark(builder, failures);
            CaseTheSchemaPartitionsWhatIsWritten(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMRAO FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMRAO result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMRAO END");

            report = builder.ToString();
            return failures.Count == 0;
        }


        // ---- round trip ----

        /// <summary> Written, read, written again — the same bytes both times, for all three record kinds, both stamped
        /// and unstamped. This is the claim that makes the format a format: if a preset does not survive a load and a
        /// save, the tool is rewriting the author's work behind their back. </summary>
        private static void CaseAWrittenRecordReadsBackByteIdentical(StringBuilder builder, List<string> failures)
        {
            int stamped = RaoFormat.CurrentVersion;
            int checks = 0;

            for (int pass = 0; pass < 2; pass++)
            {
                int stamp = pass == 0 ? stamped : 0;
                string label = pass == 0 ? "stamped" : "unstamped";

                Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker section =
                    new Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker();
                section.formatVersion = stamp;
                section.CurrentSplinationString = "Assets/RoadCityMaster/Editor/Library/Kerb.fbx";
                section.displayName = "Kerb 150 mm";
                section.desc = "Barrier kerb, 150 mm reveal";
                section.CapHeightOffset1 = 0.15f;
                section.MinMaxMod = 0.002f;
                section.isBridge = false;
                RoundTrip("section " + label, RaoFormat.RootSplinatedSection,
                    RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(section),
                    builder, failures, ref checks);

                EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker edge =
                    new EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker();
                edge.formatVersion = stamp;
                edge.edgeObjectString = "Assets/RoadCityMaster/Editor/Library/Post.fbx";
                edge.displayName = "Guide post";
                edge.desc = "Delineator post at 40 m";
                edge.isBridge = false;
                RoundTrip("edge object " + label, RaoFormat.RootEdgeObject,
                    RootUtils.GetString<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>(edge),
                    builder, failures, ref checks);

                WizardObject.WizardObjectLibrary header = new WizardObject.WizardObjectLibrary();
                header.formatVersion = stamp;
                header.displayName = "Two lane rural";
                header.desc = "Kerb, line marking and guide posts";
                header.fileName = "TwoLaneRural";
                header.isBridge = false;
                RoundTrip("wizard header " + label, RaoFormat.RootWizard,
                    RootUtils.GetString<WizardObject.WizardObjectLibrary>(header),
                    builder, failures, ref checks);
            }

            builder.Append("RCMRAO ").Append("round trip".PadRight(26))
                   .Append(Fmt("{0} records written, read and rewritten byte-identical", checks))
                   .Append("\n");
        }


        /// <summary> Reads one written record with the reader that owns its root element and rewrites it, then compares.
        /// The comparison is on the whole string rather than field by field on purpose: a field-by-field check only ever
        /// proves the fields somebody remembered to list survive, and the fields nobody lists are exactly the ones that
        /// go missing. </summary>
        private static void RoundTrip(string label, string expectedRoot, string written,
            StringBuilder builder, List<string> failures, ref int checks)
        {
            string reason;
            string rewritten;

            if (expectedRoot == RaoFormat.RootSplinatedSection)
            {
                Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker read =
                    Splination.SplinatedMeshMaker.SLMFromData(written, label, out reason);
                if (read == null)
                {
                    failures.Add(Fmt("round trip: the {0} did not read back — {1}", label,
                        reason == null ? "the reader took it for another kind of record" : reason));
                    return;
                }
                rewritten = RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(read);
            }
            else if (expectedRoot == RaoFormat.RootEdgeObject)
            {
                EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker read =
                    EdgeObjects.EdgeObjectMaker.ELMFromData(written, label, out reason);
                if (read == null)
                {
                    failures.Add(Fmt("round trip: the {0} did not read back — {1}", label,
                        reason == null ? "the reader took it for another kind of record" : reason));
                    return;
                }
                rewritten = RootUtils.GetString<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>(read);
            }
            else
            {
                WizardObject.WizardObjectLibrary read =
                    WizardObject.WizardObjectLibrary.WOLFromData(written, label, out reason);
                if (read == null)
                {
                    failures.Add(Fmt("round trip: the {0} did not read back — {1}", label,
                        reason == null ? "the reader took it for another kind of record" : reason));
                    return;
                }
                rewritten = RootUtils.GetString<WizardObject.WizardObjectLibrary>(read);
            }

            if (rewritten != written)
            {
                failures.Add(Fmt("round trip: the {0} changed on rewrite — {1}", label,
                    FirstDifference(written, rewritten)));
                return;
            }
            checks++;
        }


        // ---- the library that shipped ----

        /// <summary> Every <c>.rao</c> under <c>Editor/Library/</c> is opened, split into records, and each record read
        /// by the reader its root element names. Not one may refuse. None of these files carries a version stamp, so
        /// this case is also the one that would notice if the version rule were ever written the wrong way round and
        /// started refusing what is OLDER than the build rather than newer. </summary>
        private static void CaseEveryShippedLibraryReads(StringBuilder builder, List<string> failures)
        {
            string libraryPath = RootUtils.GetDirLibrary();
            if (!Directory.Exists(libraryPath))
            {
                failures.Add(Fmt("shipped library: {0} does not exist", libraryPath));
                return;
            }

            string[] files = Directory.GetFiles(libraryPath, "*.rao", SearchOption.AllDirectories);
            int recordCount = 0;
            int sections = 0;
            int edges = 0;
            int headers = 0;
            int roundTrips = 0;

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string origin = Path.GetFileName(files[fileIndex]);
                string reason;
                string[] records = RoadUtility.ReadRaoRecords(files[fileIndex], out reason);
                if (records == null)
                {
                    failures.Add(Fmt("shipped library: {0} {1}", origin, reason));
                    continue;
                }

                for (int index = 0; index < records.Length; index++)
                {
                    recordCount++;
                    RaoFormat.RecordKind kind = RaoFormat.KindOf(records[index]);
                    if (kind == RaoFormat.RecordKind.Malformed)
                    {
                        failures.Add(Fmt("shipped library: {0} record {1} is not well-formed XML", origin, index + 1));
                        continue;
                    }
                    if (kind == RaoFormat.RecordKind.Unrecognised)
                    {
                        failures.Add(Fmt("shipped library: {0} record {1} has root <{2}>, which the tool does not own",
                            origin, index + 1, RaoFormat.RootElementName(records[index])));
                        continue;
                    }

                    if (kind == RaoFormat.RecordKind.SplinatedSection)
                    {
                        Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker read =
                            Splination.SplinatedMeshMaker.SLMFromData(records[index], origin, out reason);
                        if (read == null)
                        {
                            failures.Add(Fmt("shipped library: {0} record {1} {2}", origin, index + 1, reason));
                        }
                        else
                        {
                            sections++;
                            roundTrips++;
                            ShippedRoundTrip(kind, read, origin, index + 1, failures);
                        }
                    }
                    else if (kind == RaoFormat.RecordKind.EdgeObject)
                    {
                        EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker read =
                            EdgeObjects.EdgeObjectMaker.ELMFromData(records[index], origin, out reason);
                        if (read == null)
                        {
                            failures.Add(Fmt("shipped library: {0} record {1} {2}", origin, index + 1, reason));
                        }
                        else
                        {
                            edges++;
                            roundTrips++;
                            ShippedRoundTrip(kind, read, origin, index + 1, failures);
                        }
                    }
                    else
                    {
                        WizardObject.WizardObjectLibrary read =
                            WizardObject.WizardObjectLibrary.WOLFromData(records[index], origin, out reason);
                        if (read == null)
                        {
                            failures.Add(Fmt("shipped library: {0} record {1} {2}", origin, index + 1, reason));
                        }
                        else
                        {
                            headers++;
                            roundTrips++;
                            ShippedRoundTrip(kind, read, origin, index + 1, failures);
                        }
                    }
                }
            }

            if (files.Length < 1) { failures.Add("shipped library: no .rao files were found to read"); }

            builder.Append("RCMRAO ").Append("shipped library".PadRight(26))
                   .Append(Fmt("{0} files, {1} records: {2} sections, {3} edge objects, {4} group headers, "
                       + "{5} round-tripped", files.Length, recordCount, sections, edges, headers, roundTrips))
                   .Append("\n");
        }


        /// <summary> One record out of a shipped library, saved, reloaded and saved again: the two saves must be the
        /// same bytes.
        ///
        /// The task's clause reads "load, save, load reproduces identical values, and the second file is byte-identical
        /// to the first apart from the version stamp", and this is the honest form of it. A shipped file's own bytes
        /// cannot be compared with a rewrite: those files were written years ago with no stamp, in whatever element
        /// order and indentation that build emitted, and any element the author left at its default is absent from the
        /// text but present in the object. So the FIRST save is the file the clause means, and the requirement is that
        /// saving again changes nothing — because if the second save differs from the first, the tool is editing the
        /// author's preset a little more each time it is opened, and the drift is invisible until a shape comes out
        /// wrong months later. Comparing the whole string rather than a list of fields is deliberate for the same
        /// reason as in <see cref="RoundTrip"/>: a field-by-field check only proves the fields somebody remembered to
        /// list survive. </summary>
        private static void ShippedRoundTrip(RaoFormat.RecordKind _kind, object _read, string _origin,
            int _recordNumber, List<string> failures)
        {
            string first;
            string second;
            string reason;

            if (_kind == RaoFormat.RecordKind.SplinatedSection)
            {
                first = RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(_read);
                Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker again =
                    Splination.SplinatedMeshMaker.SLMFromData(first, _origin, out reason);
                if (again == null)
                {
                    failures.Add(Fmt("shipped library: {0} record {1} was saved and would not load back — {2}",
                        _origin, _recordNumber, reason == null ? "the saved record read as another kind" : reason));
                    return;
                }
                second = RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(again);
            }
            else if (_kind == RaoFormat.RecordKind.EdgeObject)
            {
                first = RootUtils.GetString<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>(_read);
                EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker again =
                    EdgeObjects.EdgeObjectMaker.ELMFromData(first, _origin, out reason);
                if (again == null)
                {
                    failures.Add(Fmt("shipped library: {0} record {1} was saved and would not load back — {2}",
                        _origin, _recordNumber, reason == null ? "the saved record read as another kind" : reason));
                    return;
                }
                second = RootUtils.GetString<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>(again);
            }
            else
            {
                first = RootUtils.GetString<WizardObject.WizardObjectLibrary>(_read);
                WizardObject.WizardObjectLibrary again =
                    WizardObject.WizardObjectLibrary.WOLFromData(first, _origin, out reason);
                if (again == null)
                {
                    failures.Add(Fmt("shipped library: {0} record {1} was saved and would not load back — {2}",
                        _origin, _recordNumber, reason == null ? "the saved record read as another kind" : reason));
                    return;
                }
                second = RootUtils.GetString<WizardObject.WizardObjectLibrary>(again);
            }

            if (first != second)
            {
                failures.Add(Fmt("shipped library: {0} record {1} did not survive a save and a reload — {2}",
                    _origin, _recordNumber, FirstDifference(first, second)));
            }
        }


        // ---- the version rule ----

        /// <summary> A record with no <c>formatVersion</c> element at all — which is every library that shipped before
        /// RM-105 — loads, is treated as <see cref="RaoFormat.LegacyVersion"/>, and keeps every field that IS present.
        /// The second half matters as much as the first: "we accept old files" is worth nothing if accepting them means
        /// reading them as blank. </summary>
        private static void CaseAnUnstampedRecordIsLegacyAndKeepsItsFields(StringBuilder builder,
            List<string> failures)
        {
            string unstamped = WithoutStamp(SampleSection(RaoFormat.CurrentVersion));

            Expect(unstamped.IndexOf("<formatVersion>", System.StringComparison.Ordinal) < 0,
                "legacy: the test record still carries a stamp, so this case proves nothing", failures);

            string reason;
            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker read =
                Splination.SplinatedMeshMaker.SLMFromData(unstamped, "unstamped.rao", out reason);

            if (read == null)
            {
                failures.Add(Fmt("legacy: an unstamped record was refused — {0}",
                    reason == null ? "the reader took it for another kind of record" : reason));
                builder.Append("RCMRAO ").Append("legacy files".PadRight(26)).Append("refused\n");
                return;
            }

            Expect(read.RaoVersion() == 0,
                Fmt("legacy: the stamp read as {0}, not 0 — the absent-stamp signal has been overwritten by a field "
                    + "initialiser", read.RaoVersion()), failures);

            int effective;
            string versionReason;
            bool readable = RaoFormat.VersionIsReadable(read.RaoVersion(), out effective, out versionReason);
            Expect(readable, Fmt("legacy: an unstamped record was called unreadable — {0}", versionReason), failures);
            Expect(effective == RaoFormat.LegacyVersion,
                Fmt("legacy: an unstamped record was treated as version {0}, not {1}",
                    effective, RaoFormat.LegacyVersion), failures);

            Expect(read.displayName == "Kerb 150 mm",
                Fmt("legacy: displayName came back as \"{0}\" — elements that ARE present are not being populated",
                    read.displayName), failures);
            Expect(Mathf.Abs(read.CapHeightOffset1 - 0.15f) < 1e-6f,
                Fmt("legacy: CapHeightOffset1 came back as {0:R}, not 0.15", read.CapHeightOffset1), failures);

            builder.Append("RCMRAO ").Append("legacy files".PadRight(26))
                   .Append(Fmt("no stamp reads as version {0} with fields intact", RaoFormat.LegacyVersion))
                   .Append("\n");
        }


        // ---- the three refusals ----

        /// <summary> A section handed to the edge-object reader, and an edge object handed to the section reader, must
        /// both come back null with a NULL reason. This is the distinction the format class exists for: a group preset
        /// holds all three kinds and every record is offered to every reader, so "not my kind" has to be silence. If it
        /// ever becomes a refusal, loading any group preset fills the console with errors about files that are
        /// perfectly fine. </summary>
        private static void CaseARecordOfAnotherKindIsSilence(StringBuilder builder, List<string> failures)
        {
            string section = SampleSection(RaoFormat.CurrentVersion);
            string edge = SampleEdgeObject(RaoFormat.CurrentVersion);
            string reason;

            EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker asEdge =
                EdgeObjects.EdgeObjectMaker.ELMFromData(section, "section.rao", out reason);
            Expect(asEdge == null, "kind: a section was read as an edge object", failures);
            Expect(reason == null,
                Fmt("kind: a section offered to the edge reader produced a refusal instead of silence — \"{0}\"",
                    reason), failures);

            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker asSection =
                Splination.SplinatedMeshMaker.SLMFromData(edge, "edge.rao", out reason);
            Expect(asSection == null, "kind: an edge object was read as a section", failures);
            Expect(reason == null,
                Fmt("kind: an edge object offered to the section reader produced a refusal instead of silence — \"{0}\"",
                    reason), failures);

            WizardObject.WizardObjectLibrary asHeader =
                WizardObject.WizardObjectLibrary.WOLFromData(section, "section.rao", out reason);
            Expect(asHeader == null, "kind: a section was read as a group header", failures);
            Expect(reason == null,
                Fmt("kind: a section offered to the header reader produced a refusal instead of silence — \"{0}\"",
                    reason), failures);

            builder.Append("RCMRAO ").Append("wrong kind".PadRight(26))
                   .Append("skipped in silence, no refusal raised\n");
        }


        /// <summary> A truncated record is refused, and the refusal says the file is not well-formed rather than
        /// producing an empty object. The old reader answered this case with the same null it used for "wrong kind", so
        /// a half-written preset was retried as the other kind, failed again, and was dropped without a word. </summary>
        private static void CaseAMalformedRecordIsRefusedWithACause(StringBuilder builder, List<string> failures)
        {
            string whole = SampleSection(RaoFormat.CurrentVersion);
            string truncated = whole.Substring(0, whole.Length / 2);
            string reason;

            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker read =
                Splination.SplinatedMeshMaker.SLMFromData(truncated, "truncated.rao", out reason);

            Expect(read == null, "malformed: a truncated record produced an object", failures);
            Expect(reason != null, "malformed: a truncated record was refused in silence, so the caller has nothing "
                + "to tell the author", failures);
            Expect(reason != null && reason.IndexOf("well-formed", System.StringComparison.Ordinal) >= 0,
                Fmt("malformed: the reason does not say the file is not well-formed — \"{0}\"", reason), failures);

            Expect(RaoFormat.KindOf(truncated) == RaoFormat.RecordKind.Malformed,
                "malformed: KindOf did not report a truncated record as Malformed", failures);
            Expect(RaoFormat.RootElementName(truncated) == null,
                "malformed: RootElementName answered for a truncated record", failures);

            string refusal = RaoFormat.Refusal("truncated.rao", 2, reason);
            Expect(refusal.IndexOf("truncated.rao", System.StringComparison.Ordinal) >= 0,
                "malformed: the refusal sentence does not name the file", failures);
            Expect(refusal.IndexOf("record 3", System.StringComparison.Ordinal) >= 0,
                Fmt("malformed: the refusal sentence does not name the record — \"{0}\"", refusal), failures);
            Expect(refusal.IndexOf("Nothing was added", System.StringComparison.Ordinal) >= 0,
                "malformed: the refusal does not state that nothing was added to the scene", failures);

            builder.Append("RCMRAO ").Append("malformed record".PadRight(26))
                   .Append("refused, file and record named\n");
        }


        /// <summary> A record stamped one higher than this build is refused, and the refusal states both numbers. This
        /// is the clause the stamp was added for: without it a newer file loads, every element this build has never
        /// heard of defaults to zero, and the author gets a kerb of no height that reads as a geometry bug for as long
        /// as it takes somebody to find it. </summary>
        private static void CaseANewerVersionIsRefusedByNumber(StringBuilder builder, List<string> failures)
        {
            int future = RaoFormat.CurrentVersion + 1;
            string reason;

            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker section =
                Splination.SplinatedMeshMaker.SLMFromData(SampleSection(future), "future.rao", out reason);
            Expect(section == null, Fmt("newer version: a version {0} section was loaded by a build that reads up "
                + "to {1}", future, RaoFormat.CurrentVersion), failures);
            Expect(reason != null, "newer version: a section from a newer build was refused in silence", failures);
            Expect(reason != null
                && reason.IndexOf(future.ToString(CultureInfo.InvariantCulture),
                    System.StringComparison.Ordinal) >= 0
                && reason.IndexOf(RaoFormat.CurrentVersion.ToString(CultureInfo.InvariantCulture),
                    System.StringComparison.Ordinal) >= 0,
                Fmt("newer version: the reason does not state both {0} and {1} — \"{2}\"",
                    future, RaoFormat.CurrentVersion, reason), failures);

            EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker edge =
                EdgeObjects.EdgeObjectMaker.ELMFromData(SampleEdgeObject(future), "future.rao", out reason);
            Expect(edge == null, "newer version: an edge object from a newer build was loaded", failures);
            Expect(reason != null, "newer version: an edge object from a newer build was refused in silence", failures);

            int effective;
            string versionReason;
            Expect(!RaoFormat.VersionIsReadable(future, out effective, out versionReason),
                Fmt("newer version: VersionIsReadable accepted {0}", future), failures);
            Expect(RaoFormat.VersionIsReadable(RaoFormat.CurrentVersion, out effective, out versionReason),
                Fmt("newer version: VersionIsReadable refused {0}, the version this build writes",
                    RaoFormat.CurrentVersion), failures);

            builder.Append("RCMRAO ").Append("newer version".PadRight(26))
                   .Append(Fmt("version {0} refused, {1} accepted, both numbers stated",
                       future, RaoFormat.CurrentVersion))
                   .Append("\n");
        }


        // ---- the byte-order mark ----

        /// <summary> The discriminator answers the same for a record with a byte-order mark and one without. Records
        /// written in memory by <c>RootUtils.GetString</c> carry U+FEFF; the same record read back through
        /// <c>File.ReadAllText</c> does not, because that call strips it. A discriminator built on a
        /// <c>StringReader</c> throws on the first form and succeeds on the second, which would leave it disagreeing
        /// with the deserialiser about a file both can read. </summary>
        private static void CaseTheDiscriminatorIgnoresAByteOrderMark(StringBuilder builder, List<string> failures)
        {
            string withMark = SampleSection(RaoFormat.CurrentVersion);
            // Written as a code point rather than the character itself: a literal U+FEFF here is invisible in every
            // editor and the next person to read this line would have no way of knowing what is being compared.
            char byteOrderMark = (char)0xFEFF;
            bool hadMark = withMark.Length > 0 && withMark[0] == byteOrderMark;

            string withoutMark = hadMark ? withMark.Substring(1) : withMark;

            Expect(hadMark, "byte-order mark: RootUtils.GetString no longer emits one, so this case is no longer "
                + "exercising the path it was written for — check the encoding it serialises through", failures);

            Expect(RaoFormat.RootElementName(withMark) == RaoFormat.RootSplinatedSection,
                Fmt("byte-order mark: the root of a marked record read as \"{0}\"",
                    RaoFormat.RootElementName(withMark)), failures);
            Expect(RaoFormat.RootElementName(withoutMark) == RaoFormat.RootSplinatedSection,
                Fmt("byte-order mark: the root of an unmarked record read as \"{0}\"",
                    RaoFormat.RootElementName(withoutMark)), failures);

            string reason;
            Expect(Splination.SplinatedMeshMaker.SLMFromData(withMark, "marked.rao", out reason) != null,
                Fmt("byte-order mark: a marked record did not read — {0}", reason), failures);
            Expect(Splination.SplinatedMeshMaker.SLMFromData(withoutMark, "unmarked.rao", out reason) != null,
                Fmt("byte-order mark: an unmarked record did not read — {0}", reason), failures);

            builder.Append("RCMRAO ").Append("byte-order mark".PadRight(26))
                   .Append("marked and unmarked records read identically\n");
        }


        // ---- the schema ----

        /// <summary> The documented schema describes what the serialiser actually writes, exactly: every element of
        /// every record class is claimed by one family and one only, and no family names an element that no longer
        /// exists.
        ///
        /// This is the case that keeps RM-105's "documented schema" clause true after RM-105 closes. A schema is easy
        /// to write once and impossible to keep, because <c>XmlSerializer</c> needs no permission to write a new public
        /// field — add one for RM-31's generalised cross-section and every other case in this gate still passes, the
        /// round trip included, while the description of the format silently stops covering it. Requiring a PARTITION
        /// rather than a subset is what gives the check teeth in both directions: an unclassified field fails because
        /// nobody said what it is or what it is measured in, and a stale name fails because it describes a format the
        /// tool no longer writes. Either way the build says so before an author is misled by the document. </summary>
        private static void CaseTheSchemaPartitionsWhatIsWritten(StringBuilder builder, List<string> failures)
        {
            string[] roots = RaoSchema.Roots();
            Expect(roots.Length == 3, "case8: the schema describes " + roots.Length + " roots; the format has three",
                failures);

            int described = 0;
            for (int r = 0; r < roots.Length; r++)
            {
                string root = roots[r];
                System.Type recordType = RaoSchema.RecordTypeFor(root);
                RaoSchema.Family[] families = RaoSchema.FamiliesFor(root);
                if (recordType == null || families == null)
                {
                    failures.Add("case8: <" + root + "> has no record type or no families in RaoSchema");
                    continue;
                }

                string[] elements = RaoSchema.ElementNames(recordType);
                Dictionary<string, string> claimed = new Dictionary<string, string>(elements.Length);
                for (int f = 0; f < families.Length; f++)
                {
                    RaoSchema.Family family = families[f];
                    Expect(family.members.Length > 0,
                        "case8: <" + root + "> family '" + family.name + "' claims no elements", failures);
                    Expect(family.note != null && family.note.Length > 40,
                        "case8: <" + root + "> family '" + family.name + "' carries no explanation; a family name on "
                        + "its own says nothing about units or meaning", failures);

                    for (int m = 0; m < family.members.Length; m++)
                    {
                        string member = family.members[m];
                        if (claimed.ContainsKey(member))
                        {
                            failures.Add("case8: <" + root + "> element '" + member + "' is claimed by both '"
                                + claimed[member] + "' and '" + family.name + "'; a reader following the schema would "
                                + "be told two different things about the same value");
                            continue;
                        }
                        claimed.Add(member, family.name);
                    }
                }

                HashSet<string> live = new HashSet<string>(elements);
                for (int e = 0; e < elements.Length; e++)
                {
                    Expect(claimed.ContainsKey(elements[e]),
                        "case8: <" + root + "> writes element '" + elements[e] + "', which no family in RaoSchema "
                        + "claims; it will appear in every saved file with nothing to say what it is or what it is "
                        + "measured in", failures);
                }
                // Walked family by family rather than over the dictionary: a Dictionary's enumeration order is not
                // promised, and a gate whose failure lines come out in a different order on a second run is exactly
                // what CaseDeterministic exists to catch.
                for (int f = 0; f < families.Length; f++)
                {
                    string[] members = families[f].members;
                    for (int m = 0; m < members.Length; m++)
                    {
                        Expect(live.Contains(members[m]),
                            "case8: <" + root + "> family '" + families[f].name + "' names element '" + members[m]
                            + "', which the record class no longer has; the schema is describing a format the tool "
                            + "does not write", failures);
                    }
                }

                described += elements.Length;
                builder.Append(Fmt("RCMRAO {0} {1} elements, {2} families, all classified\n",
                    root.PadRight(26), elements.Length, families.Length));
            }

            string document = RaoSchema.Describe();
            Expect(document.Length > 2000,
                "case8: the generated schema is only " + document.Length + " characters long", failures);
            for (int r = 0; r < roots.Length; r++)
            {
                Expect(document.IndexOf(roots[r], System.StringComparison.Ordinal) >= 0,
                    "case8: the schema document never names <" + roots[r] + ">", failures);
            }
            Expect(document.IndexOf(RoadUtility.FileSepString.Trim(), System.StringComparison.Ordinal) >= 0,
                "case8: the schema document does not print the record separator a reader needs to split a file",
                failures);
            Expect(document.IndexOf("METRES", System.StringComparison.Ordinal) >= 0,
                "case8: the schema document does not state its units", failures);
            Expect(document.IndexOf(
                    RaoFormat.CurrentVersion.ToString(CultureInfo.InvariantCulture), System.StringComparison.Ordinal) >= 0,
                "case8: the schema document does not state the version this tool writes", failures);

            builder.Append(Fmt("RCMRAO {0} {1} elements described, {2} characters generated from the types\n",
                "schema".PadRight(26), described, document.Length));
        }


        // ---- determinism ----

        /// <summary> The same record read twice produces the same bytes, and the discriminator gives the same answer on
        /// repeat. A format whose reader is not deterministic cannot be gated at all: every other case here would pass
        /// and fail at random. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            string record = SampleSection(RaoFormat.CurrentVersion);
            string reason;

            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker first =
                Splination.SplinatedMeshMaker.SLMFromData(record, "determinism.rao", out reason);
            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker second =
                Splination.SplinatedMeshMaker.SLMFromData(record, "determinism.rao", out reason);

            if (first == null || second == null)
            {
                failures.Add(Fmt("determinism: the test record did not read — {0}", reason));
                return;
            }

            string firstText = RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(first);
            string secondText = RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(second);
            Expect(firstText == secondText,
                Fmt("determinism: two reads of one record rewrote differently — {0}",
                    FirstDifference(firstText, secondText)), failures);

            Expect(RaoFormat.RootElementName(record) == RaoFormat.RootElementName(record),
                "determinism: the discriminator gave two answers for one record", failures);

            builder.Append("RCMRAO ").Append("determinism".PadRight(26))
                   .Append("two reads of one record are identical\n");
        }


        // ---- helpers ----

        /// <summary> A section record with values distinctive enough that a field silently dropped by the round trip
        /// shows up as a difference rather than as another zero. </summary>
        private static string SampleSection(int _stamp)
        {
            Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker section =
                new Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker();
            section.formatVersion = _stamp;
            section.CurrentSplinationString = "Assets/RoadCityMaster/Editor/Library/Kerb.fbx";
            section.displayName = "Kerb 150 mm";
            section.desc = "Barrier kerb, 150 mm reveal";
            section.CapHeightOffset1 = 0.15f;
            section.MinMaxMod = 0.002f;
            return RootUtils.GetString<Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker>(section);
        }


        private static string SampleEdgeObject(int _stamp)
        {
            EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker edge =
                new EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker();
            edge.formatVersion = _stamp;
            edge.edgeObjectString = "Assets/RoadCityMaster/Editor/Library/Post.fbx";
            edge.displayName = "Guide post";
            edge.desc = "Delineator post at 40 m";
            return RootUtils.GetString<EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker>(edge);
        }


        /// <summary> The same record with its <c>formatVersion</c> element cut out, which is what every library that
        /// shipped before RM-105 looks like. The element is removed rather than set to zero because those two are
        /// different files: one has no stamp, the other has a stamp that reads zero, and the reader has to treat both as
        /// legacy without being able to tell them apart. </summary>
        private static string WithoutStamp(string _record)
        {
            const string openTag = "<formatVersion>";
            const string closeTag = "</formatVersion>";

            int open = _record.IndexOf(openTag, System.StringComparison.Ordinal);
            if (open < 0) { return _record; }
            int close = _record.IndexOf(closeTag, open, System.StringComparison.Ordinal);
            if (close < 0) { return _record; }

            int cutFrom = open;
            while (cutFrom > 0 && _record[cutFrom - 1] != '\n') { cutFrom--; }
            int cutTo = close + closeTag.Length;
            while (cutTo < _record.Length && (_record[cutTo] == '\r' || _record[cutTo] == '\n')) { cutTo++; }

            return _record.Substring(0, cutFrom) + _record.Substring(cutTo);
        }


        /// <summary> Where two records first disagree, with a few characters either side. A gate that says only "the
        /// bytes differ" leaves the reader to diff two 4 kB XML documents by eye. </summary>
        private static string FirstDifference(string _left, string _right)
        {
            int shortest = _left.Length < _right.Length ? _left.Length : _right.Length;
            int index = 0;
            while (index < shortest && _left[index] == _right[index]) { index++; }

            if (index >= shortest && _left.Length == _right.Length) { return "no difference found"; }

            string leftPart = index < _left.Length
                ? _left.Substring(index, System.Math.Min(48, _left.Length - index))
                : "(end of record)";
            string rightPart = index < _right.Length
                ? _right.Substring(index, System.Math.Min(48, _right.Length - index))
                : "(end of record)";

            return Fmt("first difference at character {0}: written \"{1}\", rewritten \"{2}\"",
                index, leftPart.Replace("\n", "\\n").Replace("\r", ""),
                rightPart.Replace("\n", "\\n").Replace("\r", ""));
        }


        private static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }


        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

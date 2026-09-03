using System.IO;
using System.Text;
using System.Xml;


namespace RoadCityMaster
{
    /// <summary> A class that can be the root of a <c>.rao</c> record, and can therefore be asked what format version
    /// the file it came from declared. Deliberately a METHOD rather than a property: <c>XmlSerializer</c> serialises
    /// public properties as elements but ignores methods entirely, so a property here would silently add an element to
    /// every file the tool writes and the round-trip gate would fail on a field nobody meant to add. </summary>
    public interface IRaoRecord
    {
        int RaoVersion();
    }


    /// <summary> The on-disk shape of a <c>.rao</c> library file, and the only place that decides whether one may be
    /// loaded.
    ///
    /// ---- WHAT A .rao ACTUALLY IS ----
    /// Despite the extension, a <c>.rao</c> is one or more UTF-8 XML documents concatenated end to end, separated by
    /// <c>RoadUtility.FileSepString</c> — a line of its own reading <c>#### RoadCityMaster ####</c>. A one-record file
    /// is a legal <c>.rao</c> too, which is why every <c>EOM*.rao</c> and <c>ESO*.rao</c> opens as plain XML while the
    /// group and bridge presets under <c>Groups/</c> do not.
    ///
    /// Each record's ROOT ELEMENT NAME is what it is. There are exactly three:
    ///   <c>WizardObjectLibrary</c>       — the header of a group preset: display name, description, thumbnail path
    ///   <c>SplinatedMeshLibraryMaker</c> — an extruded cross-section: kerb, barrier, railing, sidewalk (<c>ESO</c>)
    ///   <c>EdgeObjectLibraryMaker</c>    — a repeated edge object: post, sign, lamp, barrel (<c>EOM</c>)
    ///
    /// ---- WHY THIS CLASS EXISTS ----
    /// All three readers were <c>try { Deserialize } catch { return null; }</c>, and every caller used that null as a
    /// TYPE TEST: try the record as a section; if null, try it as an edge object; if null, <c>continue</c>. Two
    /// different facts arrived as the same null — "this record is a different kind" and "this record is broken" — so a
    /// corrupt section was retried as an edge object, failed again, and was silently DROPPED. The author got a group
    /// with a piece missing: no message, no file name, no way to tell a truncated file from an empty one. Reading the
    /// root element first splits those two questions apart, so a wrong kind is skipped in silence (correct — a group
    /// file legitimately holds all three) and a broken record is REFUSED with the file name and the reason (RM-55: a
    /// file that cannot be loaded must refuse, never substitute).
    ///
    /// ---- THE VERSION STAMP ----
    /// Every record written from now on carries <c>formatVersion</c> as its first element. The rules:
    ///   absent  → <see cref="LegacyVersion"/>. Every field the reader knows about is populated from what is present;
    ///             fields the file predates keep the value the class declares. This is safe in exactly one direction,
    ///             which is why it is the ONLY direction allowed.
    ///   ≤ <see cref="CurrentVersion"/> → loaded.
    ///   &gt; <see cref="CurrentVersion"/> → REFUSED, by name and number. This is the clause that earns the stamp: a
    ///             reader with no version silently defaults the fields it has never heard of to zero, which yields a
    ///             kerb of no height and a barrier of no width, and reads as a geometry bug for as long as it takes
    ///             someone to find it. Refusing costs the author one message; guessing costs them a day.
    ///
    /// The stamp is deliberately NOT initialised to <see cref="CurrentVersion"/> in the library classes.
    /// <c>XmlSerializer</c> constructs the object first — field initialisers run — and only then overwrites the
    /// elements the file actually contains, so a non-zero initialiser would erase the one signal that says "this file
    /// has no stamp". It is set to <see cref="CurrentVersion"/> on the way OUT, in each <c>Setup</c>/<c>LoadFrom</c>,
    /// and left at zero on the way in.
    ///
    /// ---- BOM ----
    /// <c>RootUtils.GetString</c> serialises through an <c>XmlTextWriter</c> over <c>Encoding.UTF8</c>, which emits a
    /// byte-order mark, so a record held in memory begins with U+FEFF while the same record read back through
    /// <c>File.ReadAllText</c> does not — that call detects the encoding and strips it. Both must parse. This class
    /// therefore hands the reader BYTES, exactly as <c>RootUtils</c> does, rather than a <c>StringReader</c>: a
    /// <c>StringReader</c> over a leading U+FEFF throws "data at the root level is invalid", and the discriminator
    /// would then disagree with the deserialiser about a file both can read. </summary>
    public static class RaoFormat
    {
        /// <summary> What an unstamped file is treated as. Every <c>.rao</c> shipped before RM-105 is this. </summary>
        public const int LegacyVersion = 1;

        /// <summary> What this build writes, and the highest it will read. Raise this — never reuse it — when the
        /// meaning of an existing element changes or a new element becomes load-bearing. </summary>
        public const int CurrentVersion = 2;

        public const string RootWizard = "WizardObjectLibrary";
        public const string RootSplinatedSection = "SplinatedMeshLibraryMaker";
        public const string RootEdgeObject = "EdgeObjectLibraryMaker";


        /// <summary> The three kinds of record a <c>.rao</c> may hold, plus the two answers that are not a kind:
        /// <c>Unrecognised</c> (well-formed XML whose root is none of ours — a foreign file with our extension) and
        /// <c>Malformed</c> (not well-formed XML at all — truncated, half-written, or not XML). They are separate
        /// because they deserve different treatment: one is the wrong file, the other is a damaged one. </summary>
        public enum RecordKind
        {
            Malformed = 0,
            Unrecognised = 1,
            WizardHeader = 2,
            SplinatedSection = 3,
            EdgeObject = 4
        }


        /// <summary> The name of a record's root element, or <c>null</c> when the text is not well-formed XML.
        ///
        /// The name comes from the first element, but the answer is withheld until the reader has streamed to the end
        /// of the record, because a truncated file has a perfectly good opening tag. Stopping at the first element
        /// called a half-written preset a section and handed it to the deserialiser, which then refused it in an XML
        /// parser's words -- "Unexpected end of file has occurred", with a line and a column -- instead of saying the
        /// file is not well-formed. This is still a streaming pass: no object graph, no members bound, nothing kept but
        /// one name, so it remains far cheaper than the deserialisation it is deciding whether to attempt, which is
        /// what "must not cost a full read" was there to protect. DTD processing is prohibited and the resolver removed
        /// — a section file is content, and content is never allowed to name an external entity for the loader to go
        /// and fetch. </summary>
        public static string RootElementName(string _record)
        {
            if (string.IsNullOrEmpty(_record)) { return null; }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;
            settings.CloseInput = true;

            try
            {
                using (MemoryStream stream = new MemoryStream(new UTF8Encoding().GetBytes(_record)))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    if (reader.MoveToContent() != XmlNodeType.Element) { return null; }
                    string root = reader.Name;
                    while (reader.Read()) { }
                    return root;
                }
            }
            catch (XmlException) { return null; }
            catch (IOException) { return null; }
        }


        /// <summary> Which of the three kinds this record is — or <c>Malformed</c> when it is not XML, or
        /// <c>Unrecognised</c> when it is XML the tool does not own. </summary>
        public static RecordKind KindOf(string _record)
        {
            string root = RootElementName(_record);
            if (root == null) { return RecordKind.Malformed; }
            if (root == RootSplinatedSection) { return RecordKind.SplinatedSection; }
            if (root == RootEdgeObject) { return RecordKind.EdgeObject; }
            if (root == RootWizard) { return RecordKind.WizardHeader; }
            return RecordKind.Unrecognised;
        }


        /// <summary> Reads one record of the expected kind. Returns false and fills <paramref name="_reason"/> with a
        /// sentence naming the file when the record cannot be trusted; returns false with a <c>null</c> reason when the
        /// record is simply a DIFFERENT kind, which is not an error — a group preset legitimately holds a wizard header
        /// followed by sections and edge objects, and each is offered to every reader in turn.
        ///
        /// <paramref name="_expectedRoot"/> is passed rather than derived from <typeparamref name="T"/> because the
        /// library classes are nested (<c>SplinatedMeshMaker.SplinatedMeshLibraryMaker</c>), so
        /// <c>typeof(T).Name</c> is right today only by coincidence and would silently stop matching the moment one is
        /// renamed or wrapped. The element name is the file format; the class name is an implementation detail.
        ///
        /// The version test lives HERE, after the deserialise, rather than in each of the three readers: it was written
        /// out three times in the first draft of this class and three copies of one rule is three chances for one of
        /// them to be forgotten the next time the format moves. <typeparamref name="T"/> is constrained to
        /// <see cref="IRaoRecord"/> for no other reason than to make the stamp reachable from here. </summary>
        public static bool TryRead<T>(string _record, string _origin, string _expectedRoot,
            out T _value, out string _reason) where T : class, IRaoRecord
        {
            _value = null;
            _reason = null;

            // One scan, two answers. `KindOf` is `RootElementName` plus a comparison, so asking both would stream the
            // record twice to learn the same name; the name IS the kind here, since the only kind this method acts on
            // is "not well-formed", which is exactly the null.
            string root = RootElementName(_record);
            if (root == null)
            {
                _reason = "is not well-formed XML — the file is truncated, half-written, or not a .rao at all";
                return false;
            }
            if (root != _expectedRoot) { return false; }

            try
            {
                _value = RootUtils.LoadData<T>(ref _record);
            }
            catch (System.InvalidOperationException exception)
            {
                _value = null;
                _reason = "declares itself a <" + root + "> but does not match that shape: "
                    + InnermostMessage(exception);
                return false;
            }
            catch (XmlException exception)
            {
                _value = null;
                _reason = "declares itself a <" + root + "> but the XML failed mid-record: " + exception.Message;
                return false;
            }

            if (_value == null)
            {
                _reason = "read as an empty <" + root + "> — there is nothing in it to build from";
                return false;
            }

            int effective;
            if (!VersionIsReadable(_value.RaoVersion(), out effective, out _reason))
            {
                _value = null;
                return false;
            }
            return true;
        }


        /// <summary> Reads a WHOLE library file and returns the first record in it of the expected kind. This is the
        /// entry point for the single-record libraries — every <c>ESO*.rao</c> cross-section and <c>EOM*.rao</c> edge
        /// object under <c>Editor/Library/</c> — which were previously loaded by <c>RootUtils.LoadXML</c>: an
        /// unguarded <c>File.OpenText</c> straight into the deserialiser, with no existence check, no version check and
        /// no refusal. A renamed or half-written library therefore threw an <c>IOException</c> or an
        /// <c>InvalidOperationException</c> out through <c>OnGUI</c>, and on the paths that caught nothing at all the
        /// caller dereferenced the null one line later. Both failures named no file.
        ///
        /// A single-record file is split by the separator exactly like a group preset, because a one-record file IS a
        /// legal multi-record file with one record in it — that way there is one reader for the format, not two that
        /// have to be kept agreeing. Records of another kind are stepped over in silence for the same reason they are in
        /// <see cref="TryRead{T}"/>; a record of the right kind that will not read is a refusal, and running out of
        /// records without finding one is a refusal WHEN the caller says the record must be there
        /// (<paramref name="_mustContainOne"/>). It is not always there: <c>Wizard.LoadGroupObjs</c> offers every
        /// <c>.rao</c> in a directory to the wizard-header reader precisely to find out which of them are group presets,
        /// so "this file holds no header" is that caller's ordinary answer and comes back as false with a <c>null</c>
        /// reason — the same convention <see cref="TryRead{T}"/> already uses for a record of another kind. A caller
        /// that asked for a named section file, by contrast, cannot proceed without one and gets a sentence. </summary>
        public static bool TryReadFile<T>(string _filePath, string _expectedRoot, bool _mustContainOne,
            out T _value, out string _reason) where T : class, IRaoRecord
        {
            _value = null;

            string[] records = RoadUtility.ReadRaoRecords(_filePath, out _reason);
            if (records == null) { return false; }

            string origin = Path.GetFileName(_filePath);
            int recordCount = records.Length;
            for (int index = 0; index < recordCount; index++)
            {
                string recordReason;
                T candidate;
                if (TryRead<T>(records[index], origin, _expectedRoot, out candidate, out recordReason))
                {
                    _value = candidate;
                    _reason = null;
                    return true;
                }
                if (recordReason != null) { _reason = recordReason; return false; }
            }

            _reason = _mustContainOne
                ? "holds no <" + _expectedRoot + "> record — it is a .rao, but not the kind that was asked for"
                : null;
            return false;
        }


        /// <summary> <c>XmlSerializer</c> reports everything as "There is an error in XML document (line, column)" and
        /// hides the sentence an author could act on — the element that was wrong — in the inner exception. This walks
        /// to the innermost one and uses that, appending the outer position, because a refusal is only worth anything if
        /// it says what to look at. </summary>
        private static string InnermostMessage(System.Exception _exception)
        {
            System.Exception walker = _exception;
            while (walker.InnerException != null) { walker = walker.InnerException; }
            if (walker == _exception) { return _exception.Message; }
            return walker.Message + " (" + _exception.Message + ")";
        }


        /// <summary> Whether a record's stamp is one this build may read, and what version it should be treated as.
        /// A zero or negative stamp is an unstamped legacy file, not a corrupt one — every <c>.rao</c> shipped before
        /// RM-105 has no stamp at all, and refusing those would refuse the whole library. </summary>
        public static bool VersionIsReadable(int _stamp, out int _effective, out string _reason)
        {
            _reason = null;
            _effective = _stamp <= 0 ? LegacyVersion : _stamp;

            if (_effective > CurrentVersion)
            {
                _reason = "is format version " + _effective + " and this build of Road & City Master reads up to "
                    + CurrentVersion + " — it was written by a newer version of the tool. Loading it would default "
                    + "every element this build has never heard of to zero, which produces geometry the author never "
                    + "authored; update the tool instead";
                return false;
            }
            return true;
        }


        /// <summary> The one sentence an author sees when a library file is refused. Record index is included because a
        /// group preset holds several records and "the third record" is the difference between a five-second fix and a
        /// hunt through a file nobody has opened in a year. </summary>
        public static string Refusal(string _origin, int _recordIndex, string _what)
        {
            string where = _recordIndex < 0
                ? _origin
                : _origin + ", record " + (_recordIndex + 1);
            return "Road & City Master refused to load a library file. " + where + " " + _what
                + ". Nothing was added to the scene.";
        }
    }
}

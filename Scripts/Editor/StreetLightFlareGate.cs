#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-91's gate: proves the street light's flare is drawn the way URP draws flares, and that the numbers
    /// in it are the cited ones rather than defaults.
    ///
    /// This gate is READ-ONLY and deliberately separate from <see cref="FlarePortOperator"/>. The operator MINTS the
    /// `LensFlareDataSRP` asset, which is a write to disk, and a write has no business self-registering into a suite
    /// that is supposed to be safe to run at any moment. So the operator's entry point is called `Mint`, which
    /// <see cref="RcmGateSuite"/> does not discover, and everything that can be checked without writing is checked
    /// here.
    ///
    /// What it can and cannot see. Whether a flare actually appears on screen in play mode is an author check -- a
    /// gate cannot look at a rendered frame, and this one does not pretend to. What it CAN establish is every
    /// precondition for that frame: the code selects the SRP component rather than the dead Built-in field, the
    /// component is reused rather than stacked, a missing asset yields no flare rather than an empty one, and the
    /// element the builder produces still matches the cited fixture.
    ///
    /// Where this gate deliberately stops, so that no fact has two owners. That the flare files are on disk is
    /// `ShaderInventoryGate`'s case 5, because the inventory is the thing that claims they are. That the superseded
    /// `.flare` keeps its `.meta` and GUID is `AssetIdentityGate`'s, which enforces RM-69 for every serialised asset
    /// rather than for this one. And the property-by-property comparison against the fixture is
    /// <see cref="SodiumFlare.VerifyElement"/>, called from here on the builder's output and called from the mint
    /// operator on the asset it has just written -- one spec, two subjects, no second copy to drift.
    ///
    /// One number here disagrees with RM-91's own task text, and the disagreement is the point. The task was written
    /// expecting the flare to keep the sodium lamp's ~2000 K amber. RM-86 / RM-87 audited the fixture against real
    /// roadway practice and found the emitter itself had to change: new and retrofit installations are LED, not
    /// high-pressure sodium, so the flare is 4000 K neutral white (ANSI C78.377's nominal bin, IES RP-8 practice).
    /// Asserting the amber here would lock in a fixture no authority installs any more, so what is asserted is that
    /// the tint IS the cited temperature -- whatever <see cref="SodiumFlare.ColourTemperatureKelvin"/> says -- and
    /// that it is neither the legacy amber nor an untinted white default.
    ///
    /// Report lines are prefixed RCMFLARE. </summary>
    public static class StreetLightFlareGate
    {
        [MenuItem("Window/Road City Master/Testing/Street light flare", false, 1091)]
        private static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMFLARE BEGIN\n");
            List<string> failures = new List<string>();

            CaseElementMatchesTheCitedFixture(builder, failures);
            CaseTintIsTheCitedColourTemperature(builder, failures);
            CaseGeometryCarriedOverFromTheBuiltInFile(builder, failures);
            CaseAttachSelectsTheSrpComponent(builder, failures);
            CaseMissingAssetGivesNoFlare(builder, failures);
            CaseApplyIsIdempotent(builder, failures);
            CaseNoCodePathAssignsTheDeadField(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMFLARE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMFLARE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMFLARE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The element the builder produces must match the cited fixture, property for property. The
        /// comparison itself is <see cref="SodiumFlare.VerifyElement"/> -- it lives beside the constants it compares
        /// against, and the editor-side mint operator calls the same method on the asset it has just written, so the
        /// spec is stated once and both the in-memory builder and the on-disk asset are held to it. What this case
        /// adds is the question the operator cannot ask: whether <see cref="SodiumFlare.BuildBulbElement"/> still
        /// satisfies that spec at all, with no Unity project open and nothing minted. </summary>
        private static void CaseElementMatchesTheCitedFixture(StringBuilder builder, List<string> failures)
        {
            LensFlareDataElementSRP e = SodiumFlare.BuildBulbElement(null);

            List<string> mismatches = new List<string>();
            bool matched = SodiumFlare.VerifyElement(e, mismatches);
            for (int i = 0; i < mismatches.Count; i++)
            {
                failures.Add("element: " + mismatches[i]);
            }
            Expect(matched || mismatches.Count > 0,
                "element: the shared verifier reported failure without saying why, which makes it useless as a "
                + "report.", failures);

            Expect(e.visible, "element: the flare element must be visible or nothing draws.", failures);

            builder.Append("RCMFLARE trace  element  ").Append(e.flareType)
                   .Append("  blend ").Append(e.blendMode)
                   .Append("  tint ").Append(e.tintColorType)
                   .Append("  vs cited fixture: ").Append(matched ? "matches" : "MISMATCH").Append("\n");
        }

        /// <summary> Whether the cited colour temperature is a colour temperature a roadway luminaire actually has.
        /// The element-matches-the-constant half is the shared verifier's job; this case interrogates the CONSTANT,
        /// which nothing else does. Two wrong answers are specifically hunted: the 2100 K sodium amber the legacy
        /// asset carried, and a flat untinted white meaning the conversion was never applied. Because the bounds are
        /// expressed against <see cref="SodiumFlare.ColourTemperatureKelvin"/> rather than a literal, moving the tool
        /// to the 3000 K dark-sky bin stays legal -- moving one of the pair without the other does not. </summary>
        private static void CaseTintIsTheCitedColourTemperature(StringBuilder builder, List<string> failures)
        {
            Color tint = SodiumFlare.LedWhite;

            Expect(tint.a >= 0.999f,
                "tint: a fully opaque emitter colour is what the constant states; alpha is " + F(tint.a) + ".",
                failures);

            // The Planckian locus runs warm-to-cool: below neutral, red exceeds green exceeds blue, and the SPREAD
            // between red and blue is what a colour temperature IS. Both wrong answers fail on that spread.
            float spread = tint.r - tint.b;
            Expect(spread > 0.001f,
                "tint: a street-lamp CCT is at or below neutral white, so red must exceed blue on the Planckian "
                + "locus; spread is " + F(spread) + ".", failures);
            Expect(spread < 0.60f,
                "tint: the spread between red and blue is " + F(spread) + ", which is the near-monochromatic amber "
                + "of a 2100 K sodium arc, not the " + F(SodiumFlare.ColourTemperatureKelvin)
                + " K LED the audited fixture is. RM-86 / RM-87 changed the emitter deliberately; this looks like "
                + "it was changed back.", failures);
            Expect(tint.g < 0.999f || tint.b < 0.999f,
                "tint: an exactly white (1,1,1) tint means the cited colour temperature was never applied.",
                failures);
            Expect(SodiumFlare.ColourTemperatureKelvin >= 2700f && SodiumFlare.ColourTemperatureKelvin <= 5000f,
                "tint: the cited CCT is " + F(SodiumFlare.ColourTemperatureKelvin) + " K, outside the range ANSI "
                + "C78.377 bins a white roadway LED into (3000 K dark-sky through 4000 K neutral). A value outside "
                + "it is not a luminaire this tool has a citation for.", failures);

            builder.Append("RCMFLARE trace  tint  ").Append(F(SodiumFlare.ColourTemperatureKelvin))
                   .Append(" K -> ").Append(C(tint))
                   .Append("  r-b spread ").Append(F(spread)).Append("\n");
        }

        /// <summary> The geometry that carried over from the Built-in file unchanged, because halo shape is not
        /// emitter physics. The size conversion is the one with a unit change in it: Built-in element size is in
        /// HUNDREDTHS of screen height and URP's uniform scale is in screen height, so <c>m_Size: 25</c> is 0.25 and
        /// not 25. A raw 25 would fill the screen. The shared verifier checks the ELEMENT carries the scale constant;
        /// what is checked here is that the constant is the right NUMBER, which is a different claim and the one that
        /// would go wrong if the unit were misread. </summary>
        private static void CaseGeometryCarriedOverFromTheBuiltInFile(StringBuilder builder, List<string> failures)
        {
            LensFlareDataElementSRP e = SodiumFlare.BuildBulbElement(null);

            Expect(Approx(SodiumFlare.ElementUniformScale, 0.25f),
                "geometry: the Built-in m_Size of 25 is in hundredths of screen height and URP's uniformScale is in "
                + "screen height, so it converts to 0.25; the constant says "
                + F(SodiumFlare.ElementUniformScale) + ". A raw 25 would fill the screen.", failures);
            Expect(Approx(e.position, 0f),
                "geometry: m_Position was 0 -- the glow sits on the lamp, not offset along the flare axis -- got "
                + F(e.position) + ".", failures);
            Expect(Approx(e.fallOff, 1f),
                "geometry: a diffusing lamp lens has no hard rim, so the circle's falloff is full; got "
                + F(e.fallOff) + ".", failures);
            Expect(Approx(e.edgeOffset, 0f),
                "geometry: the glow is a filled disc, not a ring, so there is no edge inset; got "
                + F(e.edgeOffset) + ".", failures);
            Expect(!e.inverseSDF,
                "geometry: inverting the distance field would draw the hole instead of the disc.", failures);
            Expect(SodiumFlare.ZoomDisposition != "",
                "geometry: Built-in's m_Zoom has no per-element equivalent in URP, and the omission must stay "
                + "recorded as a decision rather than become an oversight.", failures);

            builder.Append("RCMFLARE trace  geometry  scale ").Append(F(e.uniformScale))
                   .Append("  falloff ").Append(F(e.fallOff))
                   .Append("  zoom: ").Append(SodiumFlare.ZoomDisposition).Append("\n");
        }

        /// <summary> The port's whole point: the flare is a component on the light, not a value in the dead
        /// <c>Light.flare</c> field. Also the bug that would be invisible until someone counted draws -- the
        /// generator re-runs this every time an intersection is rebuilt, so <c>Attach</c> must REUSE the component
        /// it finds. Two components would draw the glow twice, at double brightness, and nothing would report
        /// it. </summary>
        private static void CaseAttachSelectsTheSrpComponent(StringBuilder builder, List<string> failures)
        {
            GameObject probe = new GameObject("RCMFLARE probe");
            try
            {
                Light light = probe.AddComponent<Light>();
                light.type = LightType.Point;

                LensFlareDataSRP data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                try
                {
                    SodiumFlare.Apply(data, null);

                    LensFlareComponentSRP first = SodiumFlare.Attach(light, data);
                    Expect(first != null,
                        "attach: attaching to a point light with a real data asset must produce a component.",
                        failures);
                    if (first == null) { return; }

                    Expect(first.lensFlareData == data,
                        "attach: the component must point at the data asset it was given.", failures);
                    Expect(first.gameObject == probe,
                        "attach: the component belongs on the light's own GameObject, which is where URP's "
                        + "data-driven lens flare pass looks for it.", failures);
                    Expect(first.useOcclusion && first.environmentOcclusion,
                        "attach: m_UseFog: 1 meant a lamp seen through fog dims, and URP only consults the "
                        + "environment term when occlusion is on -- both must be set, got useOcclusion="
                        + first.useOcclusion + " environmentOcclusion=" + first.environmentOcclusion + ".",
                        failures);
                    Expect(first.allowOffScreen == SodiumFlare.AllowOffScreen,
                        "attach: Built-in's m_Fade: 1 faded the flare at the screen edge, which is URP's "
                        + "off-screen draw left OFF.", failures);

                    LensFlareComponentSRP second = SodiumFlare.Attach(light, data);
                    Expect(second == first,
                        "attach: a second attach must REUSE the existing component. The generator runs this every "
                        + "time an intersection is rebuilt, and a stacked second component would draw the glow "
                        + "twice at double brightness with nothing reporting it.", failures);

                    LensFlareComponentSRP[] all = probe.GetComponents<LensFlareComponentSRP>();
                    Expect(all.Length == 1,
                        "attach: exactly one flare component must exist after two attaches, found " + all.Length
                        + ".", failures);

                    builder.Append("RCMFLARE trace  attach  components after 2 attaches ")
                           .Append(all.Length).Append("\n");
                }
                finally
                {
                    Object.DestroyImmediate(data);
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary> A missing data asset must give NO flare, not an empty one. An empty flare component is the worse
        /// failure of the two: it looks configured in the inspector and draws nothing, so an author debugging a dark
        /// street light would be looking at a component that appears correct. </summary>
        private static void CaseMissingAssetGivesNoFlare(StringBuilder builder, List<string> failures)
        {
            GameObject probe = new GameObject("RCMFLARE probe null");
            try
            {
                Light light = probe.AddComponent<Light>();
                light.type = LightType.Point;

                Expect(SodiumFlare.Attach(light, null) == null,
                    "missing asset: attaching with no data must return null -- no flare rather than an empty one, "
                    + "which would look configured in the inspector and draw nothing.", failures);
                Expect(probe.GetComponent<LensFlareComponentSRP>() == null,
                    "missing asset: no component may be added when there is no data for it to point at.", failures);
                Expect(SodiumFlare.Attach(null, null) == null,
                    "missing asset: attaching to no light must return null rather than throw.", failures);

                builder.Append("RCMFLARE trace  missing  no component added, null returned\n");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary> The mint operator can be run twice -- from the menu, then again in a batch -- so writing the
        /// flare into an asset must leave ONE element, not append a second. Two stacked circles are a double-bright
        /// glow that reads as a lighting bug rather than as a re-run. </summary>
        private static void CaseApplyIsIdempotent(StringBuilder builder, List<string> failures)
        {
            LensFlareDataSRP data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            try
            {
                SodiumFlare.Apply(data, null);
                int afterFirst = data.elements == null ? 0 : data.elements.Length;
                SodiumFlare.Apply(data, null);
                int afterSecond = data.elements == null ? 0 : data.elements.Length;

                Expect(afterFirst == 1,
                    "apply: the Built-in asset had one element, so one must be written, got " + afterFirst + ".",
                    failures);
                Expect(afterSecond == 1,
                    "apply: applying twice must still leave one element -- the operator is runnable from a menu and "
                    + "from a batch -- got " + afterSecond + ".", failures);

                builder.Append("RCMFLARE trace  apply  elements after 1x ").Append(afterFirst)
                       .Append(", after 2x ").Append(afterSecond).Append("\n");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // Deliberately NOT asserted here, to keep one owner per fact:
        //
        //   * that `Flares/SodiumBulb.flare`, its `.tga` and the minted `Flares/SodiumBulb.asset` are all on disk --
        //     ShaderInventoryGate case 5 owns it, because the inventory is the thing that CLAIMS they are;
        //   * that the legacy `.flare` keeps its `.meta` and GUID so no reference broke -- AssetIdentityGate owns it
        //     generically for every serialised asset, `.flare` included, which is RM-69's rule and not a flare rule.
        //
        // A second copy of either check here would pass or fail in lockstep with the first until the day one of them
        // was edited, which is the whole failure mode this note exists to avoid.

        /// <summary> The load path must no longer depend on the Built-in flare system. Checked at the SOURCE rather
        /// than by reflection, because what matters is that no line of code assigns <c>Light.flare</c> -- a field
        /// that still exists and still compiles under URP, and simply never draws. A gate that only inspected
        /// runtime types could not tell the difference between "we stopped assigning it" and "we assign it and it
        /// does nothing". </summary>
        private static void CaseNoCodePathAssignsTheDeadField(StringBuilder builder, List<string> failures)
        {
            string root = RoadEditorUtility.GetBasePath();
            if (!Directory.Exists(root))
            {
                failures.Add("dead field: the tool root '" + root + "' does not exist, so the source sweep could "
                    + "not run. A sweep that silently scanned nothing is worse than one that says so.");
                return;
            }

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            List<string> offenders = new List<string>();
            int scanned = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                scanned++;
                if (AssignsTheDeadField(text))
                {
                    offenders.Add(files[i].Substring(root.Length).Replace('\\', '/'));
                }
            }

            Expect(scanned > 0,
                "dead field: no .cs files were scanned under '" + root + "', so this case proved nothing.",
                failures);
            Expect(offenders.Count == 0,
                "dead field: " + offenders.Count + " file(s) still assign Light.flare, which URP never reads, so "
                + "they load an asset in order to put it in a field nothing draws: "
                + string.Join(", ", offenders.ToArray()), failures);

            builder.Append("RCMFLARE trace  dead field  ").Append(scanned)
                   .Append(" .cs scanned, ").Append(offenders.Count).Append(" assignment(s) of Light.flare\n");
        }

        /// <summary> Whether a file contains a real assignment to the dead field, as opposed to a mention of one.
        ///
        /// The distinction is the whole point of this case and a whole-file <c>Contains</c> cannot make it: the port's
        /// record of what it deleted is prose, and prose is what RM-68 was asked to leave behind. Two files say the
        /// words for good reasons -- <c>DispositionRegister</c> quotes the removed line so a reader knows what went,
        /// and this gate has to spell the needles out in order to hunt them -- and both would be reported as the very
        /// breach they document.
        ///
        /// So the scan is per line, and a line is a mention when it is comment-only, or when a quote opens before the
        /// needle: inside a string literal the words are data, not an assignment the compiler will emit. A genuine
        /// <c>light.flare = f;</c> has no quote ahead of it and is still caught. The needles are assembled from
        /// fragments for the same reason <c>ShipAuditSweeps</c> assembles its own -- otherwise this method is itself
        /// the first hit, on the line that defines what a hit is. </summary>
        private static bool AssignsTheDeadField(string text)
        {
            string[] needles =
            {
                "light" + ".flare =",
                "_light" + ".flare =",
                ".flare" + " = EngineIntegration.Load",
            };

            string[] lines = ShipAuditSweeps.SplitLines(text);
            for (int i = 0; i < lines.Length; i++)
            {
                if (ShipAuditSweeps.IsCommentOnly(lines[i])) { continue; }
                for (int n = 0; n < needles.Length; n++)
                {
                    int at = lines[i].IndexOf(needles[n], System.StringComparison.Ordinal);
                    if (at < 0) { continue; }
                    if (QuotedBefore(lines[i], at)) { continue; }
                    return true;
                }
            }
            return false;
        }

        /// <summary> Whether a quote opens somewhere on the line before <paramref name="at"/>. Cheap on purpose: the
        /// question is "is this text or is this code", and one quote earlier on the line answers it for every shape
        /// either tree actually contains. </summary>
        private static bool QuotedBefore(string line, int at)
        {
            for (int i = 0; i < at && i < line.Length; i++)
            {
                if (line[i] == '"') { return true; }
            }
            return false;
        }

        private static bool Approx(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0005f;
        }

        private static string C(Color c)
        {
            return "(" + F(c.r) + ", " + F(c.g) + ", " + F(c.b) + ", " + F(c.a) + ")";
        }

        private static string F(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif

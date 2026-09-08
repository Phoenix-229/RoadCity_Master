// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
// Authored for this project. See LICENSE.md and THIRD-PARTY-NOTICES.md.
#region "Imports"
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-86: the inventory itself — every data asset in the tool tree with each of its physical numbers
    /// classified. Built once, deterministically, from arrays of the real on-disk filenames (verified by
    /// <see cref="CityDataAssetAuditGate"/> against the tree, both ways). The evidence is recorded per number, not
    /// per file: a <c>.physicMaterial</c> named "Pavement" is not evidence its friction is a pavement's — the stored
    /// coefficient is, read against a cited value; a <c>.rao</c> cross-section offset is a preset placement, not a
    /// carriageway width, so it is AUTHORED not forced to match a lane standard. The heights and widths the § 10.46
    /// brief anticipated in the <c>.rao</c> libraries live in the referenced <c>.fbx</c> meshes (not editable-as-code
    /// text); the only height-like numbers the <c>.rao</c> itself carries are collision proxies, recorded here as
    /// AUTHORED proxies with the real-world figure named for context so RM-87 is not misled into "correcting" a
    /// proxy to a rail-top spec. </summary>
    public static class DataAssetAuditRegister
    {
        internal const string ToolRoot = "Assets/RoadCityMaster";

        // ---- Reference / note strings (one per recurring rationale, so the wording is written once). ----

        internal const string RefPlacement =
            "Author-tunable placement/spacing default in the object library; the author overrides it per road, so no "
            + "fixed external standard governs the library's starting value (C-15 AUTHORED, not a guess).";

        internal const string RefLateralSpan =
            "startTime/endTime are the fractional lateral band the run occupies in the cross-section — an authored "
            + "placement, not a dimensioned width; the carriageway geometry is owned by RM-31/RM-59/RM-96.";

        internal const string RefTuning =
            "Meshing/UV tolerance (MinMaxMod, stretchUVThreshold, VerticalMeshCutoffOffset); a solver tuning value "
            + "with no real-world dimensional standard.";

        internal const string RefProxyAuto =
            "isSimpleCollisionAutomatic=true: this extent is a collision proxy computed from the referenced mesh, not "
            + "an independently authored figure; the object's true dimensions are the .fbx mesh geometry.";

        internal const string RefPreset =
            "Measured across the lane variants, the railing offset is half-width = 3.5 + 2.5 x lanes (2L 8.5, 4L 13.5, "
            + "6L 18.5) and the arch ribs sit 2 m inboard of it (6.5 / 11.5 / 16.5). The datum these are measured to is "
            + "the deck EDGE, which is modelled into BridgeBase9On{2,4,6}Lanes.FBX -- one mesh per lane count -- so the "
            + "offsets are matched to the mesh, and recomputing them from a carriageway width would slide the railing "
            + "off the deck. Worth recording rather than correcting: the 2.5 m per lane increment is shallower than a "
            + "real 3.5 m lane, so these presets are generous at 2 lanes (17 m deck) and tight at 6 (37 m). The "
            + "cross-section engine (RM-96) sizes carriageways from real lane widths and must not inherit this slope.";

        internal const string RefPresetRise =
            "The span-to-rise ratio of an arch IS a real engineering figure -- masonry and concrete highway arches run "
            + "from semicircular (rise = span/2) down to shallow arches near span/6 to span/8 -- but it is not stored "
            + "here. Evidence: Arch12m, Arch24m and Arch48m carry byte-identical placement numbers (verticalRaise 63, "
            + "VerticalCutoff -7.7/-7.8) and differ ONLY in the mesh they name (ArchBase12m/24m/48m.FBX), so span and "
            + "rise are both modelled into the .fbx. The numbers in this file lift the arch clear and clip it below the "
            + "deck; they carry no rise, so there is nothing here to hold to the ratio.";

        internal const string RefRestitution =
            "Paved and earthen road surfaces are effectively inelastic underfoot/under-wheel; coefficient of "
            + "restitution ~= 0 is correct for all four surfaces.";

        internal const string RefFrictionSurface =
            "Dry rubber-on-surface friction is surface-specific and static (breakaway) friction is always >= dynamic "
            + "(sliding): dry brick paving ~0.6 sliding / ~0.7 peak, dry stone-sett cobblestone ~0.5 / ~0.6, dry "
            + "compacted dirt/gravel ~0.45 / ~0.55. A single blanket 0.4 with static == dynamic understated traction "
            + "and violated the physical law mu_s >= mu_k, so per-surface real coefficients replace it.";

        internal const string RefStreetlight =
            "Roadway-lighting pole spacing guideline ~= 3x mounting height (IES/AASHTO); ~30 m suits ~10 m poles on "
            + "local/collector roads.";

        internal const string RefChannelizingSpacing =
            "MUTCD channelizing-device spacing (cones, drums): the maximum spacing in a taper equals the posted speed "
            + "in mph expressed in FEET, and twice that along tangent sections. At this tool's 50 km/h collector/"
            + "arterial design speed (RoadClassDefaults) that is ~30 mph -> 30 ft ~= 9 m in a taper. The stored 4 m was "
            + "less than half the tightest real figure, which reads as a decorative parade of cones rather than a lane "
            + "closure a road authority actually sets out.";

        internal const string RefMarkerSpacing =
            "Raised pavement markers standing in for or supplementing a centre line sit on the standard broken-line "
            + "cycle, which MUTCD sets at 40 ft (12.2 m) -- a 10 ft stripe plus a 30 ft gap. The stored 3 m put four "
            + "markers inside one real cycle.";

        internal const string RefGlareScreen =
            "A barrier-top glare screen is specified by the cutoff angle at which it blocks opposing headlights, and "
            + "the blade pitch that yields the usual ~20 degree cutoff on a median barrier is of order 1 m, so the "
            + "stored 1 m stands as a real screen pitch rather than an arbitrary default.";

        internal const string RefSingleFallback =
            "isSingle=true: this library entry places ONE object at singlePosition, so meterSep is the fallback used "
            + "only if the author switches the entry to a repeating run. No installed spacing standard governs a value "
            + "that nothing in the shipped configuration reads.";

        internal const string RefFlareSize =
            "Lens-flare halo screen size in Unity flare units; an appearance value with no adopted optical standard.";

        internal const string RefFlareEmitter =
            "This Built-in .flare is SUPERSEDED, not corrected in place: URP draws nothing for the Light.flare field it "
            + "was written for, and its element is plain white over an amber SodiumBulb.tga, so the fixture it depicts "
            + "is a high-pressure sodium lamp -- an arc discharge dominated by the sodium D-line near 589 nm, "
            + "near-monochromatic amber at ~2100 K, ~100-120 lm/W. Roadway authorities now install phosphor-converted "
            + "white LED (~130-150 lm/W, real colour rendering at night), so the live flare is SodiumFlare.cs -> a "
            + "LensFlareDataSRP minted at Flares/SodiumBulb.asset, drawn as a procedural circle tinted to the 4000 K "
            + "ANSI C78.377 neutral-white bin used under IES RP-8 practice. The amber lives only in the .tga, and a "
            + "tint multiply cannot brighten a texture back to white, so the emitter change had to leave this file: it "
            + "is kept unedited as the record of the fixture that was replaced.";

        internal const string RefFrictionWrong =
            "Rubber tire on dry asphalt has a published coefficient of friction ~0.7 sliding / ~0.9 peak; a static "
            + "coefficient of 2 (and dynamic of 1) exceeds any real tire-pavement value.";

        // ---- The real on-disk filenames (no extension). Verified against the tree by the gate, both directions. ----

        internal static readonly string[] EdgeObjects =
        {
            "EOMAtten", "EOMGreenBlinder", "EOMRoadBarrel3Rigid", "EOMRoadBarrel3Static", "EOMRoadBarrel7Rigid",
            "EOMRoadBarrel7Static", "EOMRoadBarrelRigid", "EOMRoadBarrelStatic", "EOMRoadConBarrelRigid",
            "EOMRoadConBarrelStatic", "EOMRoadReflector", "EOMSign330", "EOMSign396", "EOMSign617-Small", "EOMSign617",
            "EOMSign861-Small", "EOMSign861", "EOMSign988-Small", "EOMSign988", "EOMSignDiamond",
            "EOMSignRightTurnOnly", "EOMSignSquare-Small", "EOMSignSquare", "EOMStopSign", "EOMStreetLightDouble",
            "EOMStreetLightSingle", "EOMTrafficConeRigid", "EOMTrafficConeStatic", "EOMWarningSign1", "EOMWarningSign2"
        };

        internal static readonly string[] ExtrudedObjects =
        {
            "ESOCableBarrier", "ESOCableBarrier-Light", "ESOKRail", "ESORailing1", "ESORailing2", "ESORailing3",
            "ESORailing4L", "ESORailing4R", "ESORailing4-LightL", "ESORailing4-LightR", "ESORailing5", "ESORailing6",
            "ESORailing7", "ESORailing8", "ESORailing9", "ESORailingBase1", "ESORailingBase2", "ESORumbleStrip",
            "ESOSidewalk", "ESOWBeam1L", "ESOWBeam1R", "ESOWBeam2L", "ESOWBeam2R"
        };

        internal static readonly string[] GroupFamilies =
        {
            "Arch12m", "Arch24m", "Arch48m", "Causeway1", "Causeway2", "Causeway3", "Causeway4", "Grid",
            "Group-Fancy1", "Group-KRailLights", "Group-Rumblestrips", "Group-WBeamLeftTurn", "SemiArch1",
            "SemiArch2", "SemiArch3", "Steel", "SuspL", "SuspS", "Tunnel"
        };

        internal static readonly string[] LaneVariants = { "2L", "4L", "6L" };

        internal static readonly string[] PhysicMaterials = { "Brick", "Cobblestone", "Dirt", "Pavement" };

        internal const string FlareName = "SodiumBulb";

        internal static readonly string[] Textures = { "SodiumBulb" };

        internal const string RefTextureColourSpace =
            "URP lights and composites in LINEAR space, so an 8-bit colour texture must be imported as sRGB or every "
            + "sample is read as raw linear data and the glow's brightness curve is wrong. This importer block predates "
            + "the field entirely (Unity 4 era, serializedVersion 2), so sRGBTexture: 1 was added.";

        internal const string RefTextureHaloSampling =
            "Sampling settings a halo sprite requires, corrected from the legacy -1 'unset' sentinels: wrapMode Clamp "
            + "(1) because a glow that repeats tiles visibly at its edge, filterMode Bilinear (1) so it reads as light "
            + "and not as pixel blocks, alphaIsTransparency 1 so colour is dilated into transparent pixels instead of "
            + "leaving a dark fringe, mip-maps off because a screen-space flare is never minified, and nPOTScale 0 so "
            + "the source is sampled as authored rather than rescaled to a power of two.";

        internal const string RefTexturePigmentSuperseded =
            "The pigment in this file is a high-pressure sodium lamp's ~2100 K amber. It cannot be repainted by a text "
            + "edit (binary image data) and it no longer needs to be: the LED flare that replaced it is drawn as a "
            + "procedural circle tinted to 4000 K, so this texture is out of the render path entirely and is kept only "
            + "as the record of the fixture that was replaced. See RefFlareEmitter.";


        // ------------------------------------------------------------------ build ------

        private static List<AssetAudit> _all;

        /// <summary> Every audited asset, built once and cached. Deterministic: fixed arrays, fixed order. </summary>
        public static IReadOnlyList<AssetAudit> All()
        {
            if (_all != null) return _all;
            var list = new List<AssetAudit>(EdgeObjects.Length + ExtrudedObjects.Length
                + GroupFamilies.Length * LaneVariants.Length + PhysicMaterials.Length + 1);

            foreach (string n in EdgeObjects)
                list.Add(new AssetAudit(ToolRoot + "/Editor/Library/EdgeObjects/" + n + ".rao", "EdgeObject", EdgeObjectRows(n)));
            foreach (string n in ExtrudedObjects)
                list.Add(new AssetAudit(ToolRoot + "/Editor/Library/ExtrudedObjects/" + n + ".rao", "ExtrudedObject", ExtrudedRows(n)));
            foreach (string fam in GroupFamilies)
                foreach (string v in LaneVariants)
                    list.Add(new AssetAudit(ToolRoot + "/Editor/Library/Groups/Default/" + fam + "-" + v + ".rao", "Group", GroupRows(fam, v)));
            foreach (string n in PhysicMaterials)
                list.Add(new AssetAudit(ToolRoot + "/Physics/" + n + ".physicMaterial", "PhysicMaterial", PhysicRows(n)));
            list.Add(new AssetAudit(ToolRoot + "/Flares/" + FlareName + ".flare", "Flare", FlareRows()));
            foreach (string n in Textures)
                list.Add(new AssetAudit(ToolRoot + "/Flares/" + n + ".tga", "Texture", TextureRows()));

            _all = list;
            return _all;
        }

        // ------------------------------------------------------------------ physic + flare ------

        private static NumberAudit[] PhysicRows(string name)
        {
            var bounce = NumberAudit.Matches("bounciness", "0", RefRestitution);
            if (name == "Pavement")
                return new[]
                {
                    NumberAudit.Wrong("dynamicFriction", "1", "~0.7 (dry asphalt, sliding)", RefFrictionWrong),
                    NumberAudit.Wrong("staticFriction", "2", "~0.9 (dry asphalt, peak)", RefFrictionWrong),
                    bounce
                };
            if (name == "Brick")
                return new[]
                {
                    NumberAudit.Wrong("dynamicFriction", "0.4", "0.6 (dry brick paving, sliding)", RefFrictionSurface),
                    NumberAudit.Wrong("staticFriction", "0.4", "0.7 (dry brick paving, peak)", RefFrictionSurface),
                    bounce
                };
            if (name == "Cobblestone")
                return new[]
                {
                    NumberAudit.Wrong("dynamicFriction", "0.4", "0.5 (dry stone setts, sliding)", RefFrictionSurface),
                    NumberAudit.Wrong("staticFriction", "0.4", "0.6 (dry stone setts, peak)", RefFrictionSurface),
                    bounce
                };
            // Dirt
            return new[]
            {
                NumberAudit.Wrong("dynamicFriction", "0.4", "0.45 (dry compacted earth/gravel, sliding)", RefFrictionSurface),
                NumberAudit.Wrong("staticFriction", "0.4", "0.55 (dry compacted earth/gravel, peak)", RefFrictionSurface),
                bounce
            };
        }

        private static NumberAudit[] TextureRows()
            => new[]
            {
                NumberAudit.Matches("sRGBTexture", "1", RefTextureColourSpace),
                NumberAudit.Matches("wrapMode", "1", RefTextureHaloSampling),
                NumberAudit.Matches("filterMode", "1", RefTextureHaloSampling),
                NumberAudit.Matches("alphaIsTransparency", "1", RefTextureHaloSampling),
                NumberAudit.Matches("enableMipMap", "0", RefTextureHaloSampling),
                NumberAudit.Matches("nPOTScale", "0", RefTextureHaloSampling),
                NumberAudit.Authored("pigment", "HPS ~2100 K amber (binary)", RefTexturePigmentSuperseded)
            };

        private static NumberAudit[] FlareRows()
            => new[]
            {
                NumberAudit.Authored("m_Size", "25", RefFlareSize),
                NumberAudit.Authored("m_Color x SodiumBulb.tga", "white element over amber texture", RefFlareEmitter)
            };

        // ------------------------------------------------------------------ edge objects ------

        private static NumberAudit[] EdgeObjectRows(string name)
        {
            var rows = new List<NumberAudit>(4);

            // meterSep is the installed longitudinal spacing of a REPEATING run. Only the eight isSingle=false entries
            // actually produce one, so only those eight are held to a real spacing standard; the twenty-two
            // isSingle=true entries place one object and never read it (RefSingleFallback).
            if (name == "EOMStreetLightSingle" || name == "EOMStreetLightDouble")
                rows.Add(NumberAudit.Matches("meterSep", "30", RefStreetlight));
            else if (name == "EOMTrafficConeRigid" || name == "EOMTrafficConeStatic"
                     || name == "EOMRoadConBarrelRigid" || name == "EOMRoadConBarrelStatic")
                rows.Add(NumberAudit.Wrong("meterSep", "4", "9 (MUTCD taper spacing at 50 km/h)", RefChannelizingSpacing));
            else if (name == "EOMRoadReflector")
                rows.Add(NumberAudit.Wrong("meterSep", "3", "12 (MUTCD 40 ft line cycle)", RefMarkerSpacing));
            else if (name == "EOMGreenBlinder")
                rows.Add(NumberAudit.Matches("meterSep", "1", RefGlareScreen));
            else
                rows.Add(NumberAudit.Authored("meterSep", "5", RefSingleFallback));

            rows.Add(NumberAudit.Authored("singlePosition", EdgeSinglePosition(name), RefPlacement));

            if (name == "EOMStopSign")
                rows.Add(NumberAudit.Authored("customRotation.y", "-90",
                    "Turns the sign to face oncoming traffic; a functional orientation, not a dimensioned standard."));
            if (name == "EOMSignRightTurnOnly")
                rows.Add(NumberAudit.Authored("horizontalSep", "5", RefPlacement));
            if (name == "EOMRoadBarrelRigid" || name == "EOMRoadBarrel3Rigid" || name == "EOMRoadBarrel7Rigid"
                || name == "EOMRoadConBarrelRigid")
                rows.Add(NumberAudit.Authored("verticalRaise", "0.05",
                    "Physics spawn clearance, not an installed height: these four are exactly the barrels whose prefab "
                    + "carries a Rigidbody, and a dynamic body born coplanar with the pavement collider starts "
                    + "interpenetrated -- the solver then pushes it out, which jitters or launches it. A ~5 cm gap lets "
                    + "gravity settle the ballasted drum onto the surface in the first steps instead, which is standard "
                    + "practice for spawning dynamic bodies. The static twins set no raise and need none."));

            return rows.ToArray();
        }

        /// <summary> What each edge-object library actually stores in <c>singlePosition</c>, which the register states
        /// so the fix gate can prove nothing drifted. Three values exist across the thirty libraries: the shared
        /// default, one hand-set offset, and 0.121 for the nine the upstream author moved.
        ///
        /// <c>EOMSign617</c> is deliberately NOT in the 0.121 group, and it was, which cost the fix gate a failure that
        /// looked like a changed asset. It carries the shared default on disk, exactly like the other ten
        /// <c>EOMSign*</c> libraries and — the tell — exactly like its own <c>EOMSign617-Small</c> twin, which was never
        /// listed. A pair does not get moved one half at a time, so the mismatch was this table's, not the file's. The
        /// row is <see cref="AuditVerdict.Authored"/>: a placement offset adopts no standard (§ 10.18), so the value
        /// the file carries is the value the register must state, and stating a different one is fabricating a default
        /// rather than recording one. </summary>
        private static string EdgeSinglePosition(string name)
        {
            if (name == "EOMSignRightTurnOnly") return "0.17";
            if (name == "EOMAtten" || name == "EOMStopSign"
                || name == "EOMWarningSign1" || name == "EOMWarningSign2"
                || name.StartsWith("EOMRoadBarrel")) return "0.121";
            return "0.145852223";
        }

        // ------------------------------------------------------------------ extruded objects ------

        internal const string RefProxyManual =
            "Hand-set collision proxy (isSimpleCollisionAutomatic=false); the object's true dimensions are its .fbx "
            + "mesh, and a proxy adopts no rail-top spec, so the stated extent stands (not corrected to the spec).";

        internal const string RefRumbleBearing =
        "A rumble strip bears on the pavement -- there is no air gap under one. Milled and rolled strips are formed "
        + "INTO the surface; a profiled strip is laid ON it, its own 6-13 mm section (1/2 in typical) carrying the "
        + "height. verticalRaise is an extra lift added on top of the mesh's own section (SplinatedMeshMaker: "
        + "tVect.y += verticalRaise), so the only real value for it is 0: the mesh supplies the profile. The stored "
        + "0.03 floated the strip 30 mm clear of the road, above even the 25 mm vertical discontinuity AASHTO treats "
        + "as an edge-drop hazard, and unlike the rigid barrels this asset is static with a trigger collider, so no "
        + "physics spawn clearance justifies it.";

    private static NumberAudit[] ExtrudedRows(string name)
        {
            var rows = new List<NumberAudit>(5);

            bool cable = name == "ESOCableBarrier" || name == "ESOCableBarrier-Light";
            rows.Add(NumberAudit.Authored("lateralSpan(startTime/endTime)",
                cable ? "0.5287458 / 0.7630875" : "0.1208441 / 0.335251182", RefLateralSpan));

            bool fineMod = cable || name == "ESORailing4L" || name == "ESORailing4R"
                || name == "ESORailing4-LightL" || name == "ESORailing4-LightR";
            rows.Add(NumberAudit.Authored("MinMaxMod", fineMod ? "0.002" : "0.1", RefTuning));

            bool manual = ExtrudedManualCollision(name);
            string top = ExtrudedCollisionTop(name);
            rows.Add(NumberAudit.Authored("collisionTopHeight(m)", top,
                (manual ? RefProxyManual : RefProxyAuto) + ExtrudedBarrierContext(name)));

            if (name == "ESORumbleStrip")
                rows.Add(NumberAudit.Wrong("verticalRaise", "0.03", "0 (bears on the pavement)", RefRumbleBearing));

            if (ExtrudedEdgeShift(name) != null)
                rows.Add(NumberAudit.Authored("horizontalSep", ExtrudedEdgeShift(name), RefPlacement));

            return rows.ToArray();
        }

        private static bool ExtrudedManualCollision(string n)
            => n == "ESORailing2" || n == "ESORailing4L" || n == "ESORailing4R"
               || n == "ESORailing4-LightL" || n == "ESORailing4-LightR"
               || n == "ESOWBeam1L" || n == "ESOWBeam1R" || n == "ESOWBeam2L" || n == "ESOWBeam2R";

        private static string ExtrudedCollisionTop(string n)
        {
            switch (n)
            {
                case "ESOCableBarrier": case "ESOCableBarrier-Light": case "ESOKRail": return "1.145";
                case "ESORailing1": return "0.653";
                case "ESORailing2": case "ESORailingBase1": return "0.80";
                case "ESORailing3": return "0.77";
                case "ESORailing4L": case "ESORailing4R":
                case "ESORailing4-LightL": case "ESORailing4-LightR": return "1.5097";
                case "ESORailing5": return "1.20";
                case "ESORailing6": return "1.037";
                case "ESORailing7": return "1.261";
                case "ESORailing8": case "ESORailing9": case "ESOSidewalk": return "1.007";
                case "ESORailingBase2": return "0.25";
                case "ESORumbleStrip": return "~0 (flat marker)";
                case "ESOWBeam1L": case "ESOWBeam1R": case "ESOWBeam2L": case "ESOWBeam2R": return "0.754";
                default: return "0";
            }
        }

        private static string ExtrudedBarrierContext(string n)
        {
            if (n == "ESOKRail") return " Cf. concrete safety-shape (Jersey/K-rail) standard height 0.81 m / tall 1.07 m.";
            if (n.StartsWith("ESOWBeam")) return " Cf. MASH W-beam guardrail rail-top height 0.79 m.";
            if (n.StartsWith("ESOCableBarrier")) return " Cf. high-tension cable barrier top wire ~0.78-0.99 m.";
            return string.Empty;
        }

        private static string ExtrudedEdgeShift(string n)
        {
            if (n == "ESORailing4-LightR" || n == "ESORailing4R" || n == "ESOWBeam1R" || n == "ESOWBeam2R") return "5";
            if (n == "ESORailing4-LightL" || n == "ESORailing4L" || n == "ESOWBeam1L" || n == "ESOWBeam2L") return "-5";
            return null;
        }

        // ------------------------------------------------------------------ group families ------

        private static NumberAudit[] GroupRows(string fam, string variant)
        {
            var rows = new List<NumberAudit>(2);

            // Every group is a composite laid out per lane count (2L/4L/6L), and each variant names its own deck mesh,
            // so the offsets are measured to that mesh's edge rather than computed from a carriageway width (RM-96).
            rows.Add(NumberAudit.Authored("presetLateralOffset(" + variant + ")", "deck-edge matched", RefPreset));

            // The spanning bridge families carry a signature rise/drop profile (arch crown, suspension tower/cable,
            // tunnel lift). The span-to-rise ratio is real but is modelled into the .fbx: all three arch spans share
            // identical placement numbers and differ only in the mesh they name, so no rise is stored here.
            if (GroupHasRise(fam))
                rows.Add(NumberAudit.Authored("presetRise", "profile-shape", RefPresetRise));

            return rows.ToArray();
        }

        private static bool GroupHasRise(string fam)
            => fam.StartsWith("Arch") || fam.StartsWith("SemiArch") || fam.StartsWith("Susp")
               || fam == "Steel" || fam == "Tunnel";

        // ------------------------------------------------------------------ accessors (RM-87 + inventory) ------

        /// <summary> One wrong number located inside its asset — RM-87's worklist. </summary>
        public readonly struct WrongNumber
        {
            public readonly string RelPath;
            public readonly NumberAudit Number;
            public WrongNumber(string relPath, NumberAudit number) { RelPath = relPath; Number = number; }
        }

        /// <summary> Every number classified <see cref="AuditVerdict.Wrong"/>, with its asset path. RM-87 reads this
        /// and corrects each to its <see cref="NumberAudit.Corrected"/> target, preserving the .meta/GUID. </summary>
        public static IReadOnlyList<WrongNumber> WrongNumbers()
        {
            var list = new List<WrongNumber>();
            foreach (AssetAudit a in All())
                foreach (NumberAudit n in a.Numbers)
                    if (n.Verdict == AuditVerdict.Wrong)
                        list.Add(new WrongNumber(a.RelPath, n));
            return list;
        }

        /// <summary> Tally of the three verdicts across every number in the inventory. </summary>
        public static void CountByVerdict(out int matches, out int wrong, out int authored)
        {
            matches = wrong = authored = 0;
            foreach (AssetAudit a in All())
                foreach (NumberAudit n in a.Numbers)
                    switch (n.Verdict)
                    {
                        case AuditVerdict.Matches: matches++; break;
                        case AuditVerdict.Wrong: wrong++; break;
                        default: authored++; break;
                    }
        }

        /// <summary> The RM-86 deliverable rendered as a plain-text inventory table: one row per classified number,
        /// asset path + field + value + verdict + reference/correction. Deterministic (fixed order). </summary>
        public static string ToInventoryTable()
        {
            var sb = new StringBuilder(16 * 1024);
            sb.Append("Asset\tField\tValue\tVerdict\tReference / Correction\n");
            foreach (AssetAudit a in All())
                foreach (NumberAudit n in a.Numbers)
                {
                    string tail = n.Verdict == AuditVerdict.Wrong
                        ? "correct: " + n.Corrected + " — " + n.Reference
                        : n.Reference;
                    sb.Append(a.RelPath).Append('\t')
                      .Append(n.Field).Append('\t')
                      .Append(n.Value).Append('\t')
                      .Append(n.Verdict).Append('\t')
                      .Append(tail).Append('\n');
                }
            return sb.ToString();
        }
    }
}

#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Need = RoadCityMaster.City.PrefabMetadataNeed;
using Scope = RoadCityMaster.City.ManifestScope;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Whether a manifest category ships in v1. IN SCOPE is the default; RAIL rows ship only if Q-15 puts
    /// rail in scope; OUT OF SCOPE rows are carried ONLY so nobody authors content for them (a turnout is not a
    /// road intersection). The scope is data, not a comment, so a build can ask "is this category even meant to
    /// exist" and the gate can prove the out-of-scope rows are never build-required. </summary>
    public enum ManifestScope
    {
        InScope = 0,
        RailOnlyQ15 = 1,
        OutOfScope = 2
    }


    /// <summary> The placement metadata a prefab must carry BEYOND RM-48's base contract (pivot, forward axis,
    /// metric scale, URP materials, LOD, collider policy). Each is something the tool needs at placement time and
    /// cannot infer later, so it is required at import rather than guessed. A category declares which of these its
    /// content must carry; <see cref="VariantTag"/> is implied wherever a category lists required variants, because
    /// the tool selects between them by rule and the rule needs a label to match. </summary>
    [System.Flags]
    public enum PrefabMetadataNeed
    {
        None = 0,
        FootprintSizeAndShape = 1,
        AttachmentPoints = 2,
        PartLength = 4,
        VariantTag = 8,
        GroundContactMode = 16,
        FrontageSideAndOffset = 32
    }


    /// <summary> One row of the manifest: a single lookup the tool performs. It names the category, the required
    /// variants (a category is not populated until each named variant resolves, because the tool picks between them
    /// by rule, not at random), the features that consume it, the placement metadata its content must carry, and
    /// its scope. A category may hold zero assets in the shipped tool -- that is EMPTY, reported honestly, and a
    /// build that needs it is refused by name under RM-55, never given a placeholder. </summary>
    public struct ManifestCategory
    {
        public string section;
        public string displayName;
        public string categoryId;
        public string[] requiredVariants;
        public string consumedBy;
        public PrefabMetadataNeed metadata;
        public ManifestScope scope;

        public bool RequiresBuild { get { return scope == ManifestScope.InScope; } }
        public int RequiredVariantCount { get { return requiredVariants != null ? requiredVariants.Length : 0; } }
    }
    /// <summary> One row of § 10.38's final "what every prefab must carry" table: a placement-metadata need beyond
    /// RM-48's base contract, paired with the reason the tool requires it at import time rather than inferring it
    /// later. The <see cref="need"/> is the single <see cref="PrefabMetadataNeed"/> bit a category's flags are
    /// tested against. </summary>
    public struct PlacementMetadataNote
    {
        public PrefabMetadataNeed need;
        public string displayName;
        public string why;

        public PlacementMetadataNote(PrefabMetadataNeed need, string displayName, string why)
        {
            this.need = need;
            this.displayName = displayName;
            this.why = why;
        }
    }

    /// <summary> The complete, ordered prefab manifest of § 10.38: every category the tool will ever ask for, as
    /// data rather than prose. This is the single promise the tool makes about the content it consumes -- one row
    /// per lookup, ordered by dependency so the run parts that unblock RM-76 come first and the building sets (the
    /// largest volume, the last needed) come last. Nothing here permits a placeholder: a category with no content
    /// is EMPTY, and a build that needs it is refused by name. The list is authored by hand from the task's tables,
    /// which is the honest answer to "what prefabs do we need" -- a city's worth of infrastructure is a lot of
    /// objects, and each row is one an artist can start filling. </summary>
    public static class PrefabManifest
    {
        private static List<ManifestCategory> _categories;

        /// <summary> Every manifest category, built once and cached. The order is the task's order, which is the
        /// dependency order, so a consumer that walks the list authors content in the order it unblocks features.
        /// </summary>
        public static IReadOnlyList<ManifestCategory> Categories
        {
            get
            {
                if (_categories == null) { _categories = BuildAll(); }
                return _categories;
            }
        }

        /// <summary> The categories a shipped build may actually require: IN SCOPE only. Rail (Q-15) and out-of-
        /// scope rows are excluded, so a populated-completeness check never fails on content the tool was never
        /// meant to carry. </summary>
        public static List<ManifestCategory> BuildRequiredCategories()
        {
            List<ManifestCategory> list = new List<ManifestCategory>();
            IReadOnlyList<ManifestCategory> all = Categories;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].RequiresBuild) { list.Add(all[i]); }
            }
            return list;
        }

        /// <summary> Finds a category by its stable id, or false. The scan is linear and ordered so the first
        /// definition wins; ids are unique by construction (section slug + name slug). </summary>
        public static bool TryGet(string categoryId, out ManifestCategory category)
        {
            IReadOnlyList<ManifestCategory> all = Categories;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].categoryId == categoryId) { category = all[i]; return true; }
            }
            category = new ManifestCategory();
            return false;
        }

        /// <summary> A category record with its id derived deterministically from section and name, and the
        /// VariantTag need implied whenever it lists required variants (the tool picks between variants by rule, so
        /// the rule needs a label). Every other metadata need is stated explicitly by the caller. </summary>
        private static ManifestCategory Cat(string section, string name, string[] variants, string consumedBy,
            PrefabMetadataNeed metadata, ManifestScope scope)
        {
            ManifestCategory c = new ManifestCategory();
            c.section = section;
            c.displayName = name;
            c.categoryId = Slug(section) + "/" + Slug(name);
            c.requiredVariants = variants ?? new string[0];
            c.consumedBy = consumedBy;
            c.metadata = metadata | (variants != null && variants.Length > 0
                ? PrefabMetadataNeed.VariantTag : PrefabMetadataNeed.None);
            c.scope = scope;
            return c;
        }

        /// <summary> A stable, culture-invariant slug: lower-case, runs of non-alphanumeric folded to a single
        /// dash, no leading or trailing dash. Deterministic (C-06) -- no hashing, no locale. </summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }
            StringBuilder sb = new StringBuilder(text.Length);
            bool pendingDash = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = char.ToLowerInvariant(text[i]);
                bool alnum = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
                if (alnum)
                {
                    if (pendingDash && sb.Length > 0) { sb.Append('-'); }
                    pendingDash = false;
                    sb.Append(ch);
                }
                else { pendingDash = true; }
            }
            return sb.ToString();
        }
        private static string[] V(params string[] variants) { return variants; }

        /// <summary> The whole manifest, transcribed row by row from § 10.38 in the task's own order. Each Cat call
        /// is one documented row; the metadata flags are read off that row's Notes (run parts carry PartLength,
        /// towers and mounts carry AttachmentPoints, pad-seated sites carry FootprintSizeAndShape, buildings carry
        /// ground contact and frontage), and VariantTag is added automatically wherever variants are listed. Nothing
        /// is invented: a row with no stated extra need carries only what its variants imply. </summary>
        private static List<ManifestCategory> BuildAll()
        {
            List<ManifestCategory> m = new List<ManifestCategory>();

            // Road furniture and safety -- consumed by RM-26, RM-60, RM-76.
            const string sFurniture = "Road furniture and safety";
            m.Add(Cat(sFurniture, "Guardrail run",
                V("start flare", "body section", "end flare", "buried-in-backslope end", "crash-cushion end",
                    "transition-to-parapet"), "RM-76", Need.PartLength, Scope.InScope));
            m.Add(Cat(sFurniture, "Concrete barrier run",
                V("body", "end taper", "gap/opening end", "transition"), "RM-76", Need.PartLength, Scope.InScope));
            m.Add(Cat(sFurniture, "Median barrier", V("body", "both end treatments"), "RM-60, RM-76",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sFurniture, "Pedestrian guard fence", V("post", "panel", "corner", "end post", "gate"),
                "RM-76", Need.PartLength, Scope.InScope));
            m.Add(Cat(sFurniture, "Bollard", V("fixed", "removable", "lit", "heavy vehicle-stopping"), "RM-26",
                Need.None, Scope.InScope));
            m.Add(Cat(sFurniture, "Delineator / marker post", V("post", "chevron board", "curve chevron set"),
                "RM-26", Need.None, Scope.InScope));
            m.Add(Cat(sFurniture, "Splitter island", V("nose piece", "kerb run", "end taper"), "RM-61",
                Need.FootprintSizeAndShape | Need.PartLength, Scope.InScope));
            m.Add(Cat(sFurniture, "Crash cushion", V("narrow", "wide"), "RM-60", Need.None, Scope.InScope));
            m.Add(Cat(sFurniture, "Cattle grid / stock gate", V("grid", "gate", "wing fence"), "RM-26",
                Need.None, Scope.InScope));
            // Fences, walls and boundaries -- consumed by RM-76, RM-34, lot frontages.
            const string sFences = "Fences, walls and boundaries";
            m.Add(Cat(sFences, "Chain-link fence",
                V("post", "panel", "corner post", "strainer post", "gate", "end post"), "RM-76",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sFences, "Timber / picket fence", V("post", "panel", "corner", "gate"), "RM-76",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sFences, "Masonry compound wall", V("pillar", "panel", "cap", "gate", "pedestrian gate"),
                "RM-76", Need.PartLength, Scope.InScope));
            m.Add(Cat(sFences, "Noise barrier", V("panel", "post", "end panel", "transparent panel"), "RM-76",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sFences, "Retaining structure face",
                V("gravity", "cantilever", "MSE panel", "gabion", "cap unit"), "RM-34", Need.PartLength,
                Scope.InScope));
            m.Add(Cat(sFences, "Hedge / planted boundary", V("run segment", "end", "gate gap"), "RM-47",
                Need.PartLength, Scope.InScope));

            // Kerbs, footways and crossings -- consumed by RM-29, RM-76.
            const string sKerbs = "Kerbs, footways and crossings";
            m.Add(Cat(sKerbs, "Kerb", V("straight", "radius return", "dropped", "transition", "channel"),
                "RM-29, RM-76", Need.PartLength, Scope.InScope));
            m.Add(Cat(sKerbs, "Footway surface", V("paving strip", "corner panel", "driveway crossing panel"),
                "RM-29", Need.PartLength, Scope.InScope));
            m.Add(Cat(sKerbs, "Tactile paving", V("crossing pad", "hazard strip", "platform edge"), "RM-29",
                Need.None, Scope.InScope));
            m.Add(Cat(sKerbs, "Crossing furniture", V("zebra beacon", "push-button pole", "refuge island", "ramp"),
                "RM-29", Need.None, Scope.InScope));
            m.Add(Cat(sKerbs, "Steps and ramps", V("step run", "landing", "handrail", "ramp run"), "RM-29",
                Need.PartLength, Scope.InScope));
            // Drainage -- consumed by RM-30, RM-60.
            const string sDrain = "Drainage";
            m.Add(Cat(sDrain, "Culvert", V("barrel section", "headwall", "wingwall", "apron", "end grate"),
                "RM-30", Need.PartLength, Scope.InScope));
            m.Add(Cat(sDrain, "Gully / inlet", V("grate", "kerb inlet", "catch pit cover"), "RM-30",
                Need.None, Scope.InScope));
            m.Add(Cat(sDrain, "Manhole", V("cover", "chamber ring", "shaft"), "RM-30", Need.None, Scope.InScope));
            m.Add(Cat(sDrain, "Ditch lining", V("unlined profile", "stone-pitched", "concrete channel"), "RM-30",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sDrain, "Outfall", V("headwall", "scour protection", "energy dissipator"), "RM-30",
                Need.None, Scope.InScope));

            // Traffic control and signage -- consumed by RM-32, RM-43, junctions.
            const string sTraffic = "Traffic control and signage";
            m.Add(Cat(sTraffic, "Traffic signal",
                V("pole", "mast arm", "vehicle head", "pedestrian head", "controller cabinet"), "RM-32",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sTraffic, "Sign assembly",
                V("post single", "post twin", "blank small/medium/large", "gantry"), "RM-32, RM-43",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sTraffic, "Street name sign", V("pole-mounted", "wall-mounted"), "RM-43",
                Need.None, Scope.InScope));
            m.Add(Cat(sTraffic, "Overhead gantry", V("portal", "cantilever", "sign panel", "lighting"), "RM-43",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sTraffic, "Toll / checkpoint", V("booth", "canopy", "barrier arm", "island"), "RM-43",
                Need.FootprintSizeAndShape, Scope.InScope));
            // Lighting -- consumed by RM-47, RM-24, tunnels and hubs.
            const string sLight = "Lighting";
            m.Add(Cat(sLight, "Street light",
                V("single arm", "twin arm", "short urban", "tall highway", "bracket-mounted"), "RM-47",
                Need.None, Scope.InScope));
            m.Add(Cat(sLight, "High mast", V("mast", "head ring"), "RM-47", Need.None, Scope.InScope));
            m.Add(Cat(sLight, "Amenity light", V("bollard light", "wall light", "park light"), "RM-47",
                Need.None, Scope.InScope));
            m.Add(Cat(sLight, "Tunnel luminaire", V("linear unit", "portal unit"), "RM-37", Need.None,
                Scope.InScope));

            // Electrical distribution and transmission -- consumed by RM-23, RM-24.
            const string sElec = "Electrical distribution and transmission";
            m.Add(Cat(sElec, "Transmission tower", V("suspension", "angle", "terminal / dead-end"), "RM-23",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sElec, "Insulator assembly", V("suspension string", "tension string", "post insulator"),
                "RM-23", Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sElec, "Distribution pole",
                V("wood", "concrete", "steel", "one/two/three crossarm", "terminal"), "RM-24",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sElec, "Pole-mounted equipment", V("transformer", "switch", "recloser", "capacitor bank"),
                "RM-24", Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sElec, "Ground-mounted equipment", V("pad transformer", "kiosk substation", "meter cabinet"),
                "RM-24", Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sElec, "Guy and anchor", V("guy wire", "anchor block", "guy guard"), "RM-24", Need.None,
                Scope.InScope));
            m.Add(Cat(sElec, "Service drop hardware", V("bracket", "service head", "meter box"), "RM-24",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sElec, "Substation compound",
                V("fence run", "gate", "gantry", "transformer bay", "control building"), "RM-24",
                Need.FootprintSizeAndShape, Scope.InScope));
            // Telecommunications -- consumed by RM-24, RM-47, the author's cell-tower requirement.
            const string sTele = "Telecommunications";
            m.Add(Cat(sTele, "Cell tower", V("lattice", "monopole", "guyed mast", "rooftop mast"), "RM-24",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sTele, "Antenna set", V("panel antenna", "microwave dish", "whip", "mounting frame"),
                "RM-24", Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sTele, "Equipment shelter", V("cabinet", "container shelter", "generator", "fuel tank"),
                "RM-24", Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sTele, "Signal booster / repeater", V("cabinet", "pole-mounted unit", "small-cell head"),
                "RM-47", Need.None, Scope.InScope));
            m.Add(Cat(sTele, "Fibre and copper street plant", V("cabinet", "joint chamber cover", "duct marker"),
                "RM-47", Need.None, Scope.InScope));

            // Water, sewer and gas -- consumed by § 10.12, hubs, and civic zones.
            const string sWater = "Water, sewer and gas";
            m.Add(Cat(sWater, "Elevated water tank", V("tower", "tank", "ladder", "pipework"), "§ 10.12",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sWater, "Ground reservoir", V("tank", "valve house", "fence run"), "§ 10.12",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sWater, "Pump / lift station", V("building", "pipework", "cabinet"), "§ 10.12",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sWater, "Street water plant", V("hydrant", "valve box", "standpipe", "water point"),
                "§ 10.12", Need.None, Scope.InScope));
            m.Add(Cat(sWater, "Sewer plant", V("manhole", "vent column", "chamber"), "§ 10.12", Need.None,
                Scope.InScope));
            // Bridges -- consumed by RM-33, RM-35, RM-59.
            const string sBridge = "Bridges";
            m.Add(Cat(sBridge, "Girder / beam bridge",
                V("deck segment", "girder", "pier", "pier cap", "abutment", "bearing", "expansion joint"),
                "RM-33", Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Arch bridge", V("arch ring segment", "spandrel", "abutment", "deck over arch"),
                "RM-33", Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Suspension bridge",
                V("tower", "main cable saddle", "hanger", "deck segment", "anchorage"), "RM-33",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Cable-stayed bridge", V("pylon", "stay", "stay anchor", "deck segment"), "RM-33",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Truss bridge", V("end post", "panel", "top chord", "portal bracing", "deck"),
                "RM-33", Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Bridge edge",
                V("parapet run parts", "railing run parts", "transition to guardrail"), "RM-76",
                Need.PartLength, Scope.InScope));
            m.Add(Cat(sBridge, "Footbridge", V("span", "stair tower", "lift shaft", "ramp run"), "RM-59",
                Need.PartLength, Scope.InScope));

            // Tunnels -- consumed by RM-37.
            const string sTunnel = "Tunnels";
            m.Add(Cat(sTunnel, "Portal", V("headwall", "wingwall", "canopy", "sound hood"), "RM-37", Need.None,
                Scope.InScope));
            m.Add(Cat(sTunnel, "Lining", V("ring segment", "invert", "sidewall", "cross-passage door frame"),
                "RM-37", Need.PartLength, Scope.InScope));
            m.Add(Cat(sTunnel, "Tunnel services", V("luminaire", "fan", "emergency niche", "cabinet", "sign"),
                "RM-37", Need.None, Scope.InScope));
            m.Add(Cat(sTunnel, "Cut-and-cover", V("roof slab", "wall panel", "portal transition"), "RM-37",
                Need.PartLength, Scope.InScope));
            // Rail -- consumed by RM-25, RM-43. Ships only if Q-15 puts rail in scope.
            const string sRail = "Rail";
            m.Add(Cat(sRail, "Track",
                V("rail-and-sleeper panel", "ballast profile", "slab-track panel", "check rail"), "RM-25",
                Need.PartLength, Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Level crossing", V("road panel set", "barrier", "warning light", "pedestrian gate"),
                "RM-25, RM-76", Need.None, Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Buffer stop and end of line", V("buffer", "sand drag", "end marker"), "RM-25",
                Need.None, Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Lineside furniture",
                V("signal", "km post", "cabinet", "cable trough", "mileage board"), "RM-25", Need.None,
                Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Electrification", V("catenary mast", "portal", "wire run", "tensioner", "feeder"),
                "RM-25, RM-35", Need.PartLength, Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Platform and station",
                V("platform edge run", "surface", "canopy", "station building", "footbridge"), "RM-25, RM-43",
                Need.PartLength | Need.FootprintSizeAndShape, Scope.RailOnlyQ15));
            m.Add(Cat(sRail, "Turnouts and crossings", null, "RM-25", Need.None, Scope.OutOfScope));

            // Aerial ropeway -- consumed by RM-36.
            const string sRope = "Aerial ropeway";
            m.Add(Cat(sRope, "Ropeway tower", V("suspension", "angle", "tall", "short"), "RM-36",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sRope, "Sheave assembly", V("carrier set", "guide", "saddle"), "RM-36",
                Need.AttachmentPoints, Scope.InScope));
            m.Add(Cat(sRope, "Terminal station",
                V("drive station", "return station", "boarding platform", "docking rail"), "RM-36",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sRope, "Cabin", V("gondola cabin", "chair", "cargo carrier"), "RM-36", Need.None,
                Scope.InScope));
            m.Add(Cat(sRope, "Foundation", V("tower base", "anchor block"), "RM-36",
                Need.FootprintSizeAndShape, Scope.InScope));
            // Junctions and roundabouts -- consumed by RM-28, RM-61.
            const string sJunction = "Junctions and roundabouts";
            m.Add(Cat(sJunction, "Central island",
                V("mound cap", "planting bed", "hardstanding", "monument plinth"), "RM-61", Need.None,
                Scope.InScope));
            m.Add(Cat(sJunction, "Truck apron", V("apron run", "kerb transition"), "RM-61", Need.PartLength,
                Scope.InScope));
            m.Add(Cat(sJunction, "Approach island", V("nose", "kerb run", "sign assembly", "lighting"), "RM-61",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sJunction, "Junction markings",
                V("yield line", "give-way triangles", "lane arrows", "box junction"), "RM-32", Need.None,
                Scope.InScope));

            // Transport hubs and industry -- consumed by RM-43.
            const string sHub = "Transport hubs and industry";
            m.Add(Cat(sHub, "Bus stop", V("pole and flag", "shelter", "bay markings", "layby kerb"), "RM-43",
                Need.None, Scope.InScope));
            m.Add(Cat(sHub, "Bus station",
                V("concourse building", "stand canopy", "stand marking set", "driver facility"), "RM-43",
                Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sHub, "Port",
                V("quay edge unit", "bollard", "fender", "ship-to-shore crane", "yard crane", "container stack",
                    "warehouse", "gate house", "weighbridge"), "RM-43", Need.FootprintSizeAndShape, Scope.InScope));
            m.Add(Cat(sHub, "Airport",
                V("terminal", "hangar", "control tower", "apron stand marking", "apron light", "windsock",
                    "approach light mast", "PAPI unit", "fence run"), "RM-43", Need.FootprintSizeAndShape,
                Scope.InScope));
            m.Add(Cat(sHub, "Fuel and service",
                V("forecourt canopy", "pump island", "tank vent", "shop", "truck stop", "wash bay"), "RM-43",
                Need.FrontageSideAndOffset, Scope.InScope));
            m.Add(Cat(sHub, "Industry", V("shed", "silo", "tank", "conveyor", "stack", "yard fence", "gate"),
                "RM-43", Need.FootprintSizeAndShape, Scope.InScope));
            // Streetscape and public space -- consumed by RM-47, RM-24, RM-42.
            const string sStreet = "Streetscape and public space";
            m.Add(Cat(sStreet, "Seating and waste", V("bench", "seat", "litter bin", "recycling set"), "RM-47",
                Need.None, Scope.InScope));
            m.Add(Cat(sStreet, "Planting furniture", V("planter", "tree grate", "tree guard", "raised bed"),
                "RM-47", Need.None, Scope.InScope));
            m.Add(Cat(sStreet, "Commerce",
                V("market stall", "kiosk", "informal shop", "awning", "vending unit", "table set"), "RM-42",
                Need.FrontageSideAndOffset, Scope.InScope));
            m.Add(Cat(sStreet, "Advertising and civic",
                V("billboard", "banner", "pole flag", "notice board", "statue", "fountain", "clock"), "RM-42",
                Need.None, Scope.InScope));
            m.Add(Cat(sStreet, "Mobility",
                V("cycle rack", "bike-share dock", "EV charge point", "parking meter", "taxi rank sign",
                    "parking bay markings"), "RM-42", Need.None, Scope.InScope));
            m.Add(Cat(sStreet, "Access control", V("guard booth", "gate house", "boom barrier", "driveway gate"),
                "RM-42", Need.FrontageSideAndOffset, Scope.InScope));
            m.Add(Cat(sStreet, "Utility street plant",
                V("telecom cabinet", "transformer kiosk", "post box", "public toilet", "water point"), "RM-24",
                Need.FrontageSideAndOffset, Scope.InScope));
            // Buildings by zone -- consumed by RM-42, RM-22, RM-55.
            const string sBuild = "Buildings by zone";
            Need buildBase = Need.FootprintSizeAndShape | Need.FrontageSideAndOffset;
            Need buildGround = buildBase | Need.GroundContactMode;
            m.Add(Cat(sBuild, "Residential low density",
                V("detached", "semi-detached", "compound house", "flat / stepped / stilt ground contact"),
                "RM-42", buildGround, Scope.InScope));
            m.Add(Cat(sBuild, "Residential mid and high", V("walk-up block", "apartment tower", "corner block"),
                "RM-42", buildBase, Scope.InScope));
            m.Add(Cat(sBuild, "Informal dwelling",
                V("single room", "extended", "shopfront-and-dwelling", "roof variants"), "RM-42", buildGround,
                Scope.InScope));
            m.Add(Cat(sBuild, "Retail and mixed",
                V("shop-house", "retail strip", "corner shop", "supermarket", "mall"), "RM-42", buildBase,
                Scope.InScope));
            m.Add(Cat(sBuild, "Office and civic",
                V("office block", "tower", "school", "clinic", "police post", "place of worship", "hall",
                    "market hall"), "RM-42", buildBase, Scope.InScope));
            m.Add(Cat(sBuild, "Industrial and logistics",
                V("workshop", "factory", "warehouse", "depot", "yard office"), "RM-42", buildBase, Scope.InScope));
            m.Add(Cat(sBuild, "Hospitality", V("hotel", "guest house", "restaurant", "bar"), "RM-42", buildBase,
                Scope.InScope));
            m.Add(Cat(sBuild, "Ancillary",
                V("garage", "outbuilding", "water tank stand", "boundary gate", "stairs", "veranda"), "RM-42",
                Need.FootprintSizeAndShape, Scope.InScope));
            // Vegetation -- consumed by RM-47, RM-48, RM-60.
            const string sVeg = "Vegetation";
            m.Add(Cat(sVeg, "Street tree", V("young", "mature"), "RM-47", Need.None, Scope.InScope));
            m.Add(Cat(sVeg, "Verge and median planting", V("shrub", "grass clump", "hedge run", "ground cover"),
                "RM-47", Need.None, Scope.InScope));
            m.Add(Cat(sVeg, "Park and amenity", V("park tree", "ornamental shrub", "flower bed"), "RM-47",
                Need.None, Scope.InScope));
            m.Add(Cat(sVeg, "Site vegetation", V("boundary tree line", "windbreak row", "orchard row"), "RM-47",
                Need.None, Scope.InScope));

            // Site works and pads -- consumed by RM-22, lot frontages.
            const string sSite = "Site works and pads";
            m.Add(Cat(sSite, "Driveway", V("apron", "crossing panel", "gate", "ramp"), "RM-76", Need.None,
                Scope.InScope));
            m.Add(Cat(sSite, "Pad edge",
                V("crib wall", "low retaining unit", "step run", "embankment cap"), "RM-22", Need.PartLength,
                Scope.InScope));
            m.Add(Cat(sSite, "Hardstanding",
                V("parking bay set", "yard surface", "forecourt surface", "marking set"), "RM-22", Need.None,
                Scope.InScope));
            m.Add(Cat(sSite, "Compound", V("fence run", "gate", "guard booth", "lighting"), "RM-22",
                Need.FootprintSizeAndShape, Scope.InScope));

            return m;
        }

        /// <summary> The six placement-metadata needs of § 10.38's final table, each with the reason the tool
        /// requires it at import rather than inferring it later. This is the authoritative "why" behind a
        /// category's <see cref="PrefabMetadataNeed"/> flags, so a report can explain a requirement, not just
        /// name it. </summary>
        public static IReadOnlyList<PlacementMetadataNote> MetadataNeeds()
        {
            List<PlacementMetadataNote> notes = new List<PlacementMetadataNote>();
            notes.Add(new PlacementMetadataNote(Need.FootprintSizeAndShape, "Footprint size and shape",
                "RM-22 sizes the pad from it; lot fitting rejects what will not fit."));
            notes.Add(new PlacementMetadataNote(Need.AttachmentPoints, "Attachment points",
                "Tower crossarms, sheave saddles, sign mounts, stay anchors -- placement without them is guesswork."));
            notes.Add(new PlacementMetadataNote(Need.PartLength, "Part length",
                "RM-76's quantisation; a run cannot close without it."));
            notes.Add(new PlacementMetadataNote(Need.VariantTag, "Variant tag",
                "The tool selects by rule (turn angle, end type, ground contact), so the rule needs a label."));
            notes.Add(new PlacementMetadataNote(Need.GroundContactMode, "Ground-contact mode",
                "Flat, stepped or stilts -- decides whether a building needs a pad or spans a slope."));
            notes.Add(new PlacementMetadataNote(Need.FrontageSideAndOffset, "Frontage side and offset",
                "Which face meets the street, and how far back it sits."));
            return notes;
        }
    }
}

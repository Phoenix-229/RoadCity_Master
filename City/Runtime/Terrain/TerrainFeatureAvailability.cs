#region "Imports"
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which mode a feature needs, and the honest middle category.
    ///
    /// "Works better connected" is a real answer and pretending otherwise is what makes people think a mode is
    /// broken. A road cut standalone is a genuine road cut -- it is simply limited to the terrain tiles the tool can
    /// reach, where connected it participates in whatever the provider owns. </summary>
    public enum TerrainFeatureSupport
    {
        /// <summary> Identical either way. The provider is not involved and its absence changes nothing. </summary>
        BothModes = 0,
        /// <summary> Works in both, but connected mode does it over more ground or keeps it through regeneration.
        /// </summary>
        BetterConnected = 1,
        /// <summary> Cannot be done without a provider. Standalone refuses it in words rather than faking it.
        /// </summary>
        ConnectedOnly = 2
    }


    /// <summary> One row of the availability table. Every string is authored here and nowhere else, because the same
    /// row is read by the inspector, by the road tool's manual and by Universe Creator's documentation, and three
    /// hand-maintained copies would disagree within a month. </summary>
    public struct TerrainFeatureRow
    {
        /// <summary> Stable identifier. Used by the inspector to ask about one feature; never shown to a user.
        /// </summary>
        public string id;
        /// <summary> The feature's name, as a person would say it. </summary>
        public string title;
        public TerrainFeatureSupport support;
        /// <summary> What actually happens with no provider connected. For a <see cref="TerrainFeatureSupport
        /// .ConnectedOnly"/> feature this is the refusal, and it must name what to connect. </summary>
        public string standalone;
        /// <summary> What happens with a provider connected. </summary>
        public string connected;

        /// <summary> The one sentence to show for a given mode. </summary>
        public string Sentence(TerrainProviderMode mode)
        {
            return mode == TerrainProviderMode.Connected ? connected : standalone;
        }

        /// <summary> Whether this feature is usable at all in the given mode -- the thing an inspector greys a
        /// control out on. </summary>
        public bool AvailableIn(TerrainProviderMode mode)
        {
            return mode == TerrainProviderMode.Connected || support != TerrainFeatureSupport.ConnectedOnly;
        }
    }


    /// <summary> The availability table, as data.
    ///
    /// § 10.24's rule is that no provider-only feature silently does nothing: it says, in one sentence, what to
    /// connect. That sentence has to come from somewhere a machine can read, so it lives here rather than in three
    /// prose documents. <see cref="ToMarkdown"/> is what the two documentation consumers render; the inspector reads
    /// the rows directly. </summary>
    public static class TerrainFeatureAvailability
    {
        /// <summary> What to connect, written once. Every refusal ends with this, so the instruction is identical
        /// wherever a user meets it. </summary>
        public const string WhatToConnect =
            "connect a terrain provider by adding the Universe Creator terrain link component to the object that "
            + "generates your terrain";

        private static readonly List<TerrainFeatureRow> rows = Build();

        public static int Count { get { return rows.Count; } }
        public static TerrainFeatureRow Row(int index) { return rows[index]; }
        public static IList<TerrainFeatureRow> Rows { get { return rows.AsReadOnly(); } }

        /// <summary> One row by id. <paramref name="found"/> is false for an unknown id, and the returned row's
        /// title says so rather than being blank -- a typo in a call site should be visible in the inspector.
        /// </summary>
        public static TerrainFeatureRow Find(string id, out bool found)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) { found = true; return rows[i]; }
            }
            found = false;
            TerrainFeatureRow missing;
            missing.id = id;
            missing.title = "unknown feature '" + id + "'";
            missing.support = TerrainFeatureSupport.BothModes;
            missing.standalone = "this feature is not in the availability table";
            missing.connected = missing.standalone;
            return missing;
        }

        /// <summary> The sentence for one feature in one mode, for a control that needs to explain itself.
        /// </summary>
        public static string SentenceFor(string id, TerrainProviderMode mode)
        {
            bool found;
            TerrainFeatureRow row = Find(id, out found);
            return row.Sentence(mode);
        }

        /// <summary> Whether a control should be usable. Unknown ids stay usable: the table is documentation, and a
        /// gap in it must not disable working functionality. </summary>
        public static bool AvailableIn(string id, TerrainProviderMode mode)
        {
            bool found;
            TerrainFeatureRow row = Find(id, out found);
            return !found || row.AvailableIn(mode);
        }

        /// <summary> Everything the given mode cannot do, for the status panel's one-time summary. </summary>
        public static IList<TerrainFeatureRow> UnavailableIn(TerrainProviderMode mode)
        {
            List<TerrainFeatureRow> unavailable = new List<TerrainFeatureRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].AvailableIn(mode)) { unavailable.Add(rows[i]); }
            }
            return unavailable;
        }

        private static string SupportLabel(TerrainFeatureSupport support)
        {
            if (support == TerrainFeatureSupport.ConnectedOnly) { return "needs a provider"; }
            if (support == TerrainFeatureSupport.BetterConnected) { return "better connected"; }
            return "both modes";
        }

        /// <summary> The table as a markdown block, for the road tool's manual and for Universe Creator's
        /// documentation. Generated rather than transcribed, so the two documents cannot drift apart. </summary>
        public static string ToMarkdown()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("| Feature | Availability | Standalone | Connected |\n");
            sb.Append("| --- | --- | --- | --- |\n");
            for (int i = 0; i < rows.Count; i++)
            {
                sb.Append("| ").Append(rows[i].title)
                    .Append(" | ").Append(SupportLabel(rows[i].support))
                    .Append(" | ").Append(rows[i].standalone)
                    .Append(" | ").Append(rows[i].connected)
                    .Append(" |\n");
            }
            return sb.ToString();
        }

        /// <summary> A stable fingerprint of the table, so a gate can assert the three consumers saw one table.
        /// </summary>
        public static string Signature()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) { sb.Append('\n'); }
                sb.Append(rows[i].id).Append('|').Append((int)rows[i].support);
            }
            return sb.ToString();
        }

        private static TerrainFeatureRow Make(string id, string title, TerrainFeatureSupport support,
            string standalone, string connected)
        {
            TerrainFeatureRow row;
            row.id = id;
            row.title = title;
            row.support = support;
            row.standalone = standalone;
            row.connected = connected;
            return row;
        }

        /// <summary> The table itself. Eleven rows, each one a thing a user can actually try. The refusals are written
        /// as what happens, not as an error: "the shell is placed but the surface is not opened" tells someone what
        /// they will see, where "not supported" leaves them guessing whether it failed.
        ///
        /// RM-107 added the eleventh, `bridge.deck`. It was missing rather than excluded: a bridge is the one
        /// structure that crosses terrain WITHOUT cutting it, so it is not covered by `road.cut`, and its footings
        /// raise the same held-ground question as a building pad. The row is BetterConnected, not ConnectedOnly --
        /// standalone still builds the bridge; what it cannot do is hold the footings or answer what a pier stands on
        /// outside the loaded tiles. </summary>
        private static List<TerrainFeatureRow> Build()
        {
            List<TerrainFeatureRow> table = new List<TerrainFeatureRow>();

            table.Add(Make("road.cut", "Road cut into terrain", TerrainFeatureSupport.BetterConnected,
                "the terrain under the road is lowered or raised directly, on whichever terrain tiles are in the "
                    + "scene",
                "the cut is requested from the provider, which shapes it into ground it owns and keeps it through "
                    + "regeneration"));

            table.Add(Make("road.restore", "Terrain restored when a road is deleted",
                TerrainFeatureSupport.BetterConnected,
                "the heightfield stored before the road was built is written back, so the restore is only as good as "
                    + "that stored copy",
                "the provider is told which ground changed and regenerates its natural surface there, with no stored "
                    + "copy to go stale"));

            table.Add(Make("road.details", "Grass and detail cleared along the road", TerrainFeatureSupport.BothModes,
                "detail layers under the road are cleared and restored on delete",
                "the same clearing, announced to the provider so it does not repopulate the corridor"));

            table.Add(Make("road.trees", "Trees removed from the road corridor", TerrainFeatureSupport.BothModes,
                "trees standing in the corridor are removed and put back on delete",
                "the same removal, announced with the corridor so the provider leaves it clear"));

            table.Add(Make("road.splat", "Terrain texture painted under the road", TerrainFeatureSupport.BothModes,
                "the splat map is painted under the road surface",
                "the same painting, announced to the provider with the painted region"));

            table.Add(Make("tunnel.cavity", "Tunnel cavity kept open through terrain",
                TerrainFeatureSupport.ConnectedOnly,
                "the tunnel shell is placed and terrain holes are punched, but nothing holds them open if the "
                    + "terrain is regenerated -- to keep a cavity open, " + WhatToConnect,
                "the opening is held by the provider and survives regeneration; it is released when the tunnel is "
                    + "deleted"));

            table.Add(Make("bridge.deck", "Bridge deck carried over terrain",
                TerrainFeatureSupport.BetterConnected,
                "the deck spans the ground untouched and the abutments and pier footings are terraformed once; "
                    + "whether a pier lands in water can only be answered for terrain tiles in the scene",
                "the footings are requested as held ground the provider keeps flat, and the provider answers what "
                    + "each support stands on, including ground no tile covers yet"));

            table.Add(Make("building.pad", "Building pads held against a shared heightfield",
                TerrainFeatureSupport.ConnectedOnly,
                "a pad is terraformed once and stays until something else changes that ground -- to hold a pad "
                    + "against regeneration, " + WhatToConnect,
                "the pad is a held request, so the provider keeps that ground flat and gives it back on delete"));

            table.Add(Make("terrain.crosschunk", "Building and roads across streamed terrain chunks",
                TerrainFeatureSupport.ConnectedOnly,
                "only terrain tiles already loaded in the scene can be read or changed; a road leaving them stops "
                    + "affecting the ground -- for streamed terrain, " + WhatToConnect,
                "the provider reports which chunks are active, and ground outside them is requested rather than "
                    + "assumed absent"));

            table.Add(Make("terrain.sampling", "Roads following the natural ground",
                TerrainFeatureSupport.BetterConnected,
                "height and slope are sampled from the terrain tiles in the scene",
                "height and slope come from the provider, which knows ground that has not been built into a tile "
                    + "yet"));

            table.Add(Make("city.blocks", "City blocks, lots and street furniture",
                TerrainFeatureSupport.BothModes,
                "generated from the road network; the terrain is only read, never owned",
                "identical, and the ground it sits on is requested through the same gateway as everything else"));

            return table;
        }
    }
}
